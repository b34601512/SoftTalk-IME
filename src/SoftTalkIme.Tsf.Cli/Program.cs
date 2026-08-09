using SoftTalkIme.Tsf;

return args.FirstOrDefault()?.Trim().ToLowerInvariant() switch
{
    null or "self-test" => RunSelfTest(),
    "probe-registration" => RunRegistrationProbe(),
    "register" => RunRegistration(args.Skip(1).ToArray(), register: true),
    "unregister" => RunRegistration(args.Skip(1).ToArray(), register: false),
    "help" or "--help" or "-h" => PrintHelp(),
    _ => Fail("未知命令。"),
};

static int RunRegistrationProbe()
{
    TsfRegistration.Probe();
    Console.WriteLine("TSF_REGISTRATION_PROBE_PASSED");
    return 0;
}

static int RunRegistration(string[] args, bool register)
{
    if (!args.Contains("--apply", StringComparer.OrdinalIgnoreCase))
    {
        return Fail("注册或卸载会修改系统状态；请显式传入 --apply。自动化测试不执行此命令。 ");
    }

    if (register)
    {
        TsfRegistration.Register();
        Console.WriteLine("TSF_REGISTERED");
    }
    else
    {
        TsfRegistration.Unregister();
        Console.WriteLine("TSF_UNREGISTERED");
    }

    return 0;
}

static int RunSelfTest()
{
    var finalizedIndex = -1;
    var aborted = false;
    var candidateList = new SoftTalkCandidateList(
        index =>
        {
            finalizedIndex = index;
            return 0;
        },
        () => aborted = true);

    candidateList.SetItems(Enumerable.Range(0, 12).Select(index => $"候选 {index}").ToArray());
    candidateList.GetCount(out var count);
    Assert(count == 9, "候选数量没有限制为 9 条");
    Assert(candidateList.GetString(8, out var last) == 0 && last == "候选 8", "候选文本读取错误");
    Assert(candidateList.GetString(9, out _) == unchecked((int)0x80070057), "越界候选未返回参数错误");
    candidateList.GetUpdatedFlags(out var updatedFlags);
    Assert(updatedFlags != 0, "候选更新没有标记变更");
    candidateList.GetUpdatedFlags(out var clearedFlags);
    Assert(clearedFlags == 0, "候选更新标记未清空");

    Assert(candidateList.SetSelection(2) == 0, "候选选择失败");
    candidateList.GetSelection(out var selection);
    Assert(selection == 2, "候选选择状态错误");
    Assert(candidateList.FinalizeCandidate() == 0 && finalizedIndex == 2, "候选确认回调错误");
    Assert(candidateList.Abort() == 0 && aborted, "候选取消回调错误");

    candidateList.SetItems(Array.Empty<string>());
    candidateList.GetCount(out count);
    candidateList.GetSelection(out selection);
    Assert(count == 0 && selection == 0, "候选清空状态错误");
    TestNoHitFallsBackToNormalInput();
    Console.WriteLine("TSF_CANDIDATE_SELF_TEST_PASSED");
    return 0;
}

static void TestNoHitFallsBackToNormalInput()
{
    var firstMiss = TsfQueryFallbackPolicy.Decide("", "z", hasMatches: false);
    Assert(!firstMiss.EatKey && firstMiss.FallbackText is null, "首个无命中字母没有交还普通输入");

    var laterMiss = TsfQueryFallbackPolicy.Decide("tu", "tux", hasMatches: false);
    Assert(laterMiss.EatKey && laterMiss.FallbackText == "tux", "已有查询无命中时没有回退完整普通文本");

    var match = TsfQueryFallbackPolicy.Decide("t", "tu", hasMatches: true);
    Assert(match.EatKey && match.FallbackText is null, "命中候选时错误回退普通输入");
}

static int PrintHelp()
{
    Console.WriteLine("SoftTalk-IME TSF CLI");
    Console.WriteLine("  self-test    运行无需 GUI 的候选状态自测");
    Console.WriteLine("  probe-registration    只读探测 TSF 官方注册 COM 接口");
    Console.WriteLine("  register --apply      执行 TSF 官方注册（修改系统状态）");
    Console.WriteLine("  unregister --apply    执行 TSF 官方卸载（修改系统状态）");
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
