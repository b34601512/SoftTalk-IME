using System.Text.Json;
using System.Net;
using SoftTalkIme.Core.Models;
using SoftTalkIme.Core.Search;
using SoftTalkIme.Core.Storage;
using SoftTalkIme.Core.Sync;

return await SoftTalkImeCli.RunAsync(args);

internal static class SoftTalkImeCli
{
    public static Task<int> RunAsync(string[] args)
    {
        var command = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "help";
        try
        {
            var exitCode = command switch
            {
                "self-test" => RunSelfTest(),
                "search" => RunSearch(args.Skip(1).ToArray()),
                "help" or "--help" or "-h" => PrintHelp(),
                _ => Fail($"未知命令：{command}"),
            };
            return Task.FromResult(exitCode);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[失败] {exception.Message}");
            return Task.FromResult(1);
        }
    }

    private static int RunSelfTest()
    {
        CliSelfTests.RunAll();
        Console.WriteLine("SELF_TEST_PASSED");
        return 0;
    }

    private static int RunSearch(string[] args)
    {
        if (args.Length < 2)
        {
            return Fail("用法：search <snapshot.json> <query>");
        }

        var snapshot = new KnowledgeSnapshotStore().LoadOrEmpty(args[0]);
        var query = string.Join(' ', args.Skip(1));
        var hits = KnowledgeSearchEngine.Search(snapshot, query);
        foreach (var hit in hits)
        {
            Console.WriteLine($"{hit.Entry.Id}\t{hit.Score:0.##}\t{hit.CategoryPath}\t{hit.Entry.Question}\t{hit.Entry.Answer}");
        }
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("SoftTalk-IME CLI");
        Console.WriteLine("  self-test                         运行无需 GUI 的核心自测");
        Console.WriteLine("  search <snapshot.json> <query>   检索本地话术快照");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }
}

internal static class CliSelfTests
{
    public static void RunAll()
    {
        TestSyncHeadDecision();
        TestSnapshotReducerAndSearch();
        TestPinyinSearchFallback();
        TestSnapshotDelete();
        TestAtomicSnapshotStore();
        TestPollInterval();
        TestIncrementalSyncPagination();
        TestSyncWorkerSavesOnlyAfterSuccess();
        TestHttpTransportIsReadOnly();
        TestSyncWorkerKeepsOldSnapshotOnFailure();
        TestUsageStatisticsAndRanking();
    }

    private static void TestSyncHeadDecision()
    {
        using var document = JsonDocument.Parse("{\"scopes\":{\"team_phrases\":5,\"private_phrases\":2}}");
        var snapshot = new KnowledgeSnapshot();
        snapshot.ScopeSequences[SyncConstants.TeamScope] = 4;
        snapshot.ScopeSequences[SyncConstants.PrivateScope] = 2;
        var changed = SyncDecision.FindChangedScopes(SyncHead.Parse(document.RootElement), snapshot);
        Assert(changed.SequenceEqual(new[] { SyncConstants.TeamScope }), "head 变化判断错误");
    }

    private static void TestSnapshotReducerAndSearch()
    {
        using var document = JsonDocument.Parse(SamplePageJson());
        var snapshot = KnowledgeSnapshotReducer.ApplyPage(new KnowledgeSnapshot(), SyncConstants.TeamScope, document.RootElement);
        var hits = KnowledgeSearchEngine.Search(snapshot, "退款");
        Assert(hits.Count == 1, "话术检索数量错误");
        Assert(hits[0].Entry.Question.Contains("退款", StringComparison.Ordinal), "话术检索内容错误");
        Assert(hits[0].CategoryPath == "售后 > 退款", "分类路径错误");
    }

    private static void TestSnapshotDelete()
    {
        using var initial = JsonDocument.Parse(SamplePageJson());
        var snapshot = KnowledgeSnapshotReducer.ApplyPage(new KnowledgeSnapshot(), SyncConstants.TeamScope, initial.RootElement);
        using var deletion = JsonDocument.Parse("""
        {
          "table_batches": [
            { "table_name": "st_faq", "records": [
              { "uuid": "faq-1", "deleted_at": "2026-08-09 13:00:00" }
            ]}
          ]
        }
        """);
        var next = KnowledgeSnapshotReducer.ApplyPage(snapshot, SyncConstants.TeamScope, deletion.RootElement);
        Assert(next.Entries.Count == 0, "删除同步记录未从本地快照移除");
    }

    private static void TestPinyinSearchFallback()
    {
        using var document = JsonDocument.Parse(SamplePageJson());
        var snapshot = KnowledgeSnapshotReducer.ApplyPage(new KnowledgeSnapshot(), SyncConstants.TeamScope, document.RootElement);
        var entry = snapshot.Entries["faq-1"];
        Assert(entry.PinyinIndexText.Contains("tuikuanz", StringComparison.Ordinal), "同步缺少本地全拼索引");
        Assert(entry.PinyinIndexText.Contains("tkzmcl", StringComparison.Ordinal), "同步缺少本地首字母索引");
        Assert(KnowledgeSearchEngine.Search(snapshot, "tuikuan").Count == 1, "全拼检索失败");
        Assert(KnowledgeSearchEngine.Search(snapshot, "tk").Count == 1, "首字母检索失败");

        var legacySnapshot = new KnowledgeSnapshot();
        legacySnapshot.Entries["legacy"] = new KnowledgeEntry(
            "legacy", "退款怎么处理", "请提供订单号", "", "team", 0, 1);
        Assert(KnowledgeSearchEngine.Search(legacySnapshot, "tuikuan").Count == 1, "旧快照拼音回退失败");

        using var remoteIndexDocument = JsonDocument.Parse("""
        {
          "table_batches": [{ "table_name": "st_faq", "records": [
            { "uuid": "remote", "question": "退款", "answer": "处理", "pinyin_index_text": "custom-index" }
          ]}]
        }
        """);
        var remoteIndexSnapshot = KnowledgeSnapshotReducer.ApplyPage(
            new KnowledgeSnapshot(),
            SyncConstants.TeamScope,
            remoteIndexDocument.RootElement);
        Assert(remoteIndexSnapshot.Entries["remote"].PinyinIndexText == "custom-index", "远端拼音索引未优先使用");
    }

    private static void TestAtomicSnapshotStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SoftTalkImeSelfTest-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "snapshot.json");
        try
        {
            var snapshot = new KnowledgeSnapshot();
            snapshot.ScopeSequences[SyncConstants.TeamScope] = 7;
            new KnowledgeSnapshotStore().SaveAtomic(path, snapshot);
            var loaded = new KnowledgeSnapshotStore().LoadOrEmpty(path);
            Assert(loaded.ScopeSequences[SyncConstants.TeamScope] == 7, "快照原子保存/读取错误");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void TestPollInterval()
    {
        Assert(SyncConstants.PollInterval == TimeSpan.FromMinutes(1), "同步轮询间隔不是 1 分钟");
    }

    private static void TestIncrementalSyncPagination()
    {
        var transport = new RecordingSyncTransport();
        var snapshot = new KnowledgeSnapshot();
        snapshot.ScopeSequences[SyncConstants.TeamScope] = 1;
        snapshot.ScopeSequences[SyncConstants.PrivateScope] = 0;
        var result = new KnowledgeSyncCoordinator(transport)
            .PollOnceAsync(snapshot)
            .GetAwaiter()
            .GetResult();

        Assert(result.UpdatedScopes.SequenceEqual(new[] { SyncConstants.TeamScope }), "同步错误拉取未变化的同步域");
        Assert(result.Snapshot.ScopeSequences[SyncConstants.TeamScope] == 2, "同步未推进到固定水位");
        Assert(result.Snapshot.Entries.Count == 2, "增量分页记录数量错误");
        Assert(transport.CurrentStateCalls.Count == 2, "增量同步分页次数错误");
        Assert(transport.CurrentStateCalls[0].AfterSequence == 1, "增量同步起点错误");
        Assert(transport.CurrentStateCalls[1].SyncSequence == 2, "后续分页未复用固定水位");
    }

    private static void TestSyncWorkerSavesOnlyAfterSuccess()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SoftTalkImeWorker-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "snapshot.json");
        try
        {
            var store = new KnowledgeSnapshotStore();
            var initial = new KnowledgeSnapshot();
            initial.ScopeSequences[SyncConstants.TeamScope] = 1;
            store.SaveAtomic(path, initial);
            var worker = new KnowledgeSyncWorker(
                new KnowledgeSyncCoordinator(new RecordingSyncTransport()),
                store,
                path);

            worker.PollAndSaveAsync().GetAwaiter().GetResult();
            var saved = store.LoadOrEmpty(path);
            Assert(saved.ScopeSequences[SyncConstants.TeamScope] == 2, "同步 worker 未原子保存新快照");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void TestHttpTransportIsReadOnly()
    {
        var handler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://sync.example.test"),
        };
        var transport = new HttpReadOnlyKnowledgeSyncTransport(httpClient, "read-only-token", "test-client");

        transport.FetchHeadAsync(SyncConstants.FormalScopes).GetAwaiter().GetResult().Dispose();
        transport.FetchCurrentStateAsync(
            SyncConstants.TeamScope,
            afterSequence: 3,
            pageCursor: null,
            syncSequence: 4).GetAwaiter().GetResult().Dispose();

        Assert(handler.Requests.Count == 2, "只读同步请求数量错误");
        Assert(handler.Requests.All(request => request.Method == HttpMethod.Post), "只读同步出现非 POST 请求");
        Assert(
            handler.Requests.Select(request => request.Path).SequenceEqual(
                new[] { SyncConstants.HeadPath, SyncConstants.CurrentStatePath }),
            "只读同步访问了非约定接口");
        Assert(handler.Requests.All(request => request.Authorization == "Bearer read-only-token"), "只读同步令牌未正确传递");
        Assert(handler.Requests.All(request => request.ClientVersion == "test-client"), "客户端版本请求头缺失");
        Assert(handler.Requests.All(request => request.ProtocolVersion == SyncConstants.ProtocolVersion), "同步协议版本请求头缺失");
        Assert(handler.Requests.All(request => request.SchemaVersion == SyncConstants.SchemaVersion), "同步数据版本请求头缺失");
        Assert(!handler.Requests.Any(request => request.Path.Contains("write", StringComparison.OrdinalIgnoreCase)), "只读同步触碰写入接口");
    }

    private static void TestSyncWorkerKeepsOldSnapshotOnFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SoftTalkImeWorkerFailure-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "snapshot.json");
        try
        {
            var store = new KnowledgeSnapshotStore();
            var initial = new KnowledgeSnapshot();
            initial.ScopeSequences[SyncConstants.TeamScope] = 9;
            initial.Entries["old"] = new KnowledgeEntry(
                "old", "旧话术", "旧答案", "", SyncConstants.TeamScope, 0, 1);
            store.SaveAtomic(path, initial);
            var before = File.ReadAllText(path);
            var worker = new KnowledgeSyncWorker(
                new KnowledgeSyncCoordinator(new FailingSyncTransport()),
                store,
                path);

            var failed = false;
            try
            {
                worker.PollAndSaveAsync().GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Assert(failed, "同步失败没有向调用方报告异常");
            Assert(File.ReadAllText(path) == before, "同步失败覆盖了旧快照");
            Assert(store.LoadOrEmpty(path).ScopeSequences[SyncConstants.TeamScope] == 9, "同步失败未保留旧版本号");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void TestUsageStatisticsAndRanking()
    {
        var snapshot = new KnowledgeSnapshot();
        snapshot.Entries["faq-a"] = new KnowledgeEntry(
            "faq-a", "退款怎么处理", "请提供订单号", "", "team", 0, 1);
        snapshot.Entries["faq-b"] = new KnowledgeEntry(
            "faq-b", "退款怎么处理", "请提供订单号和截图", "", "team", 0, 2);
        snapshot.Entries["faq-low"] = new KnowledgeEntry(
            "faq-low", "售后流程", "退款会在三个工作日内处理", "", "team", 0, 0);

        var statistics = new KnowledgeUsageStatistics();
        statistics.RecordUse("faq-a");
        for (var index = 0; index < 5; index++)
        {
            statistics.RecordUse("faq-b");
        }
        for (var index = 0; index < 100; index++)
        {
            statistics.RecordUse("faq-low");
        }

        var hits = KnowledgeSearchEngine.Search(
            snapshot,
            "退款",
            usageCounts: statistics.Counts);
        Assert(hits[0].Entry.Id == "faq-b", "同等相关性下未按使用次数排序");
        Assert(hits[1].Entry.Id == "faq-a", "使用次数排序破坏了同等相关性顺序");
        Assert(hits[2].Entry.Id == "faq-low", "低相关性话术被使用次数错误置顶");

        var directory = Path.Combine(Path.GetTempPath(), $"SoftTalkImeUsage-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "usage-stats.json");
        try
        {
            var store = new KnowledgeUsageStatisticsStore();
            store.SaveAtomic(path, statistics);
            var loaded = store.LoadOrEmpty(path);
            Assert(loaded.GetCount("faq-b") == 5, "使用次数原子保存/读取错误");
            File.WriteAllText(path, "{ not-json }");
            Assert(store.LoadOrEmpty(path).GetCount("faq-b") == 0, "损坏统计文件未安全回退");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string SamplePageJson()
    {
        return """
        {
          "table_batches": [
            { "table_name": "st_category", "records": [
              { "uuid": "cat-1", "parent_uuid": "", "level": 1, "phrase_scope": "team", "phrase_set_no": 0, "name": "售后", "sort_order": 1 },
              { "uuid": "cat-2", "parent_uuid": "cat-1", "level": 2, "phrase_scope": "team", "phrase_set_no": 0, "name": "退款", "sort_order": 1 }
            ]},
            { "table_name": "st_faq", "records": [
              { "uuid": "faq-1", "question": "退款怎么处理", "answer": "请提供订单号", "category_uuid": "cat-2", "phrase_scope": "team", "phrase_set_no": 0, "sort_order": 1 }
            ]}
          ]
        }
        """;
    }

    private sealed class RecordingSyncTransport : IReadOnlyKnowledgeSyncTransport
    {
        public List<(string Scope, long AfterSequence, string? PageCursor, long? SyncSequence)> CurrentStateCalls { get; } = new();

        public Task<JsonDocument> FetchHeadAsync(IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JsonDocument.Parse("""
            { "scopes": { "team_phrases": 2, "private_phrases": 0 } }
            """));
        }

        public Task<JsonDocument> FetchCurrentStateAsync(
            string scope,
            long afterSequence,
            string? pageCursor,
            long? syncSequence,
            CancellationToken cancellationToken = default)
        {
            CurrentStateCalls.Add((scope, afterSequence, pageCursor, syncSequence));
            var json = pageCursor is null
                ? """
                {
                  "sync_seq": 2,
                  "has_more": true,
                  "next_page_cursor": "st_faq:1",
                  "object_total": 1,
                  "table_batches": [{ "table_name": "st_faq", "records": [
                    { "uuid": "faq-2", "question": "退款进度", "answer": "请稍候", "phrase_scope": "team", "phrase_set_no": 0, "sort_order": 2 }
                  ]}]
                }
                """
                : """
                {
                  "sync_seq": 2,
                  "has_more": false,
                  "next_page_cursor": "",
                  "object_total": 1,
                  "table_batches": [{ "table_name": "st_faq", "records": [
                    { "uuid": "faq-3", "question": "退款时效", "answer": "一般三个工作日", "phrase_scope": "team", "phrase_set_no": 0, "sort_order": 3 }
                  ]}]
                }
                """;
            return Task.FromResult(JsonDocument.Parse(json));
        }
    }

    private sealed class FailingSyncTransport : IReadOnlyKnowledgeSyncTransport
    {
        public Task<JsonDocument> FetchHeadAsync(
            IReadOnlyList<string> scopes,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<JsonDocument>(new InvalidOperationException("模拟同步失败"));
        }

        public Task<JsonDocument> FetchCurrentStateAsync(
            string scope,
            long afterSequence,
            string? pageCursor,
            long? syncSequence,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<JsonDocument>(new InvalidOperationException("模拟同步失败"));
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<RecordedHttpRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new RecordedHttpRequest(
                request.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                ReadHeader(request, "X-SoftTalk-Client-Version"),
                ReadHeader(request, "X-SoftTalk-Sync-Protocol-Version"),
                ReadHeader(request, "X-SoftTalk-Sync-Schema-Version"),
                requestBody));

            var responseBody = request.RequestUri?.AbsolutePath == SyncConstants.HeadPath
                ? "{\"scopes\":{\"team_phrases\":4,\"private_phrases\":0}}"
                : "{\"sync_seq\":4,\"has_more\":false,\"object_total\":0,\"table_batches\":[]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody),
            };
        }

        private static string ReadHeader(HttpRequestMessage request, string name)
        {
            return request.Headers.TryGetValues(name, out var values)
                ? values.Single()
                : string.Empty;
        }
    }

    private sealed record RecordedHttpRequest(
        HttpMethod Method,
        string Path,
        string Authorization,
        string ClientVersion,
        string ProtocolVersion,
        string SchemaVersion,
        string Body);
}
