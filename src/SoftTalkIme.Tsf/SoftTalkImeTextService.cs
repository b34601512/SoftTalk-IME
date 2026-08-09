using System.Runtime.InteropServices;
using SoftTalkIme.Core.Models;
using SoftTalkIme.Core.Search;
using SoftTalkIme.Core.Storage;
using SoftTalkIme.Core.Sync;

namespace SoftTalkIme.Tsf;

[ComVisible(true)]
[Guid("D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class SoftTalkImeTextService : ITfTextInputProcessor, ITfKeyEventSink
{
    public const string ClassId = "D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41";

    private readonly string _snapshotPath;
    private readonly string _usagePath;
    private readonly KnowledgeUsageStatisticsStore _usageStore;
    private readonly KnowledgeSyncWorker? _syncWorker;
    private readonly HttpClient? _syncHttpClient;
    private KnowledgeSnapshot _snapshot;
    private KnowledgeUsageStatistics _usageStatistics;
    private ITfKeystrokeManagerNative? _keystrokeManager;
    private ITfUIElementManagerNative? _uiElementManager;
    private readonly SoftTalkCandidateList _candidateList;
    private uint _uiElementId;
    private bool _uiElementBegun;
    private nint _lastContext;
    private CancellationTokenSource? _syncCancellation;
    private Task? _syncTask;
    private uint _clientId;
    private bool _active;
    private bool _sessionArmed;
    private string _query = string.Empty;
    private IReadOnlyList<SearchHit> _hits = Array.Empty<SearchHit>();

    public SoftTalkImeTextService()
    {
        _snapshotPath = Environment.GetEnvironmentVariable("SOFTTALK_IME_SNAPSHOT")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoftTalk",
                "IME",
                "knowledge.snapshot.json");
        _usagePath = Environment.GetEnvironmentVariable("SOFTTALK_IME_USAGE_STATS")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoftTalk",
                "IME",
                "usage-stats.json");
        _snapshot = new KnowledgeSnapshotStore().LoadOrEmpty(_snapshotPath);
        _usageStore = new KnowledgeUsageStatisticsStore();
        _usageStatistics = _usageStore.LoadOrEmpty(_usagePath);
        (_syncWorker, _syncHttpClient) = CreateSyncWorker(_snapshotPath);
        _candidateList = new SoftTalkCandidateList(
            index => InsertHit(_lastContext, index),
            ResetSession);
    }

    public int Activate(nint threadManager, uint clientId)
    {
        if (threadManager == 0)
        {
            return TsfHResults.EInvalidArg;
        }

        try
        {
            var manager = (ITfKeystrokeManagerNative)Marshal.GetTypedObjectForIUnknown(
                threadManager,
                typeof(ITfKeystrokeManagerNative));
            var result = manager.AdviseKeyEventSink(clientId, this, foreground: true);
            if (result < 0)
            {
                ReleaseComObject(manager);
                return result;
            }

            _keystrokeManager = manager;
            try
            {
                _uiElementManager = (ITfUIElementManagerNative)Marshal.GetTypedObjectForIUnknown(
                    threadManager,
                    typeof(ITfUIElementManagerNative));
            }
            catch (COMException)
            {
                _uiElementManager = null;
            }
            _clientId = clientId;
            _active = true;
            StartSync();
            return TsfHResults.SOk;
        }
        catch (Exception exception)
        {
            return HResultFromException(exception);
        }
    }

    public int Deactivate()
    {
        try
        {
            var result = TsfHResults.SOk;
            if (_keystrokeManager is not null)
            {
                result = _keystrokeManager.UnadviseKeyEventSink(_clientId);
                ReleaseComObject(_keystrokeManager);
            }

            _keystrokeManager = null;
            _clientId = 0;
            _active = false;
            StopSync();
            ResetSession();
            ReleaseComObject(_uiElementManager);
            _uiElementManager = null;
            return result;
        }
        catch (Exception exception)
        {
            return HResultFromException(exception);
        }
    }

    public int OnSetFocus(int foreground)
    {
        if (foreground == 0)
        {
            ResetSession();
        }

        return TsfHResults.SOk;
    }

    public int OnTestKeyDown(nint context, nuint virtualKey, nint keyData, out int eaten)
    {
        eaten = 0;
        if (!_active)
        {
            return TsfHResults.SOk;
        }

        var key = checked((int)virtualKey);
        if (IsArmHotkey(key))
        {
            eaten = 1;
        }
        else if (ShouldEatSessionKey(key))
        {
            eaten = 1;
        }

        return TsfHResults.SOk;
    }

    public int OnTestKeyUp(nint context, nuint virtualKey, nint keyData, out int eaten)
    {
        eaten = 0;
        return TsfHResults.SOk;
    }

    public int OnKeyDown(nint context, nuint virtualKey, nint keyData, out int eaten)
    {
        eaten = 0;
        if (!_active)
        {
            return TsfHResults.SOk;
        }

        var key = checked((int)virtualKey);
        if (IsArmHotkey(key))
        {
            ResetSession();
            _sessionArmed = true;
            eaten = 1;
            return TsfHResults.SOk;
        }

        if (!_sessionArmed && key is >= 'A' and <= 'Z')
        {
            _sessionArmed = TsfInputActivationPolicy.ShouldAutoArm(
                key,
                SearchHits(char.ToLowerInvariant((char)key).ToString()).Count > 0);
        }

        if (!_sessionArmed)
        {
            return TsfHResults.SOk;
        }

        if (key is >= 'A' and <= 'Z')
        {
            var previousQuery = _query;
            _query += char.ToLowerInvariant((char)key);
            var nextHits = SearchHits(_query);
            var decision = TsfQueryFallbackPolicy.Decide(
                previousQuery,
                _query,
                nextHits.Count > 0);
            if (decision.FallbackText is not null)
            {
                HideCandidates();
                var result = InsertText(context, decision.FallbackText);
                ResetSession();
                eaten = result >= 0 ? 1 : 0;
            }
            else if (!decision.EatKey)
            {
                ResetSession();
            }
            else
            {
                ApplyHits(context, nextHits);
                eaten = 1;
            }

            return TsfHResults.SOk;
        }

        if (key == TsfConstants.VirtualKeyBackspace)
        {
            if (_query.Length > 0)
            {
                _query = _query[..^1];
                RefreshHits(context);
            }
            eaten = 1;
            return TsfHResults.SOk;
        }

        if (key == TsfConstants.VirtualKeyEscape)
        {
            ResetSession();
            eaten = 1;
            return TsfHResults.SOk;
        }

        if (key == TsfConstants.VirtualKeyEnter || key == TsfConstants.VirtualKeySpace)
        {
            eaten = InsertHit(context, 0) >= 0 ? 1 : 0;
            return TsfHResults.SOk;
        }

        if (key is >= TsfConstants.VirtualKeyF1 and <= TsfConstants.VirtualKeyF9)
        {
            var index = key - TsfConstants.VirtualKeyF1;
            eaten = InsertHit(context, index) >= 0 ? 1 : 0;
        }

        return TsfHResults.SOk;
    }

    public int OnKeyUp(nint context, nuint virtualKey, nint keyData, out int eaten)
    {
        eaten = 0;
        return TsfHResults.SOk;
    }

    public int OnPreservedKey(nint context, ref Guid command, out int eaten)
    {
        eaten = 0;
        return TsfHResults.SOk;
    }

    private int InsertHit(nint context, int index)
    {
        if (_hits.Count == 0 || index < 0 || index >= _hits.Count)
        {
            return TsfHResults.EFail;
        }

        var text = _hits[index].Entry.Answer;
        var result = InsertText(context, text);
        if (result >= 0)
        {
            RecordUsage(_hits[index].Entry.Id);
            ResetSession();
        }
        return result;
    }

    private int InsertText(nint context, string text)
    {
        if (context == 0 || string.IsNullOrEmpty(text))
        {
            return TsfHResults.EInvalidArg;
        }

        try
        {
            var nativeContext = (ITfContextNative)Marshal.GetTypedObjectForIUnknown(
                context,
                typeof(ITfContextNative));
            var session = new InsertTextEditSession(context, text);
            var requestResult = nativeContext.RequestEditSession(
                _clientId,
                session,
                TsfConstants.TfEsSync | TsfConstants.TfEsReadWrite,
                out var sessionResult);
            return requestResult < 0 ? requestResult : sessionResult;
        }
        catch (Exception exception)
        {
            return HResultFromException(exception);
        }
    }

    private void RefreshHits(nint context)
    {
        ApplyHits(context, SearchHits(_query));
    }

    private IReadOnlyList<SearchHit> SearchHits(string query)
    {
        return KnowledgeSearchEngine.Search(
            Volatile.Read(ref _snapshot),
            query,
            usageCounts: _usageStatistics.Counts);
    }

    private void ApplyHits(nint context, IReadOnlyList<SearchHit> hits)
    {
        _hits = hits;
        if (_hits.Count == 0)
        {
            HideCandidates();
            return;
        }

        ShowCandidates(context);
    }

    private void StartSync()
    {
        if (_syncWorker is null || _syncTask is { IsCompleted: false })
        {
            return;
        }

        _syncCancellation = new CancellationTokenSource();
        _syncTask = _syncWorker.RunAsync(
            onSuccess: result => Volatile.Write(ref _snapshot, result.Snapshot),
            onError: _ => { },
            cancellationToken: _syncCancellation.Token);
    }

    private void StopSync()
    {
        var cancellation = _syncCancellation;
        _syncCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            _syncTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        finally
        {
            cancellation.Dispose();
            _syncTask = null;
        }
    }

    private static (KnowledgeSyncWorker? Worker, HttpClient? Client) CreateSyncWorker(string snapshotPath)
    {
        if (!SoftTalkImeSyncConnectionResolver.TryResolve(out var connection, out _)
            || connection is null
            || !Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var baseAddress))
        {
            return (null, null);
        }

        var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(20),
        };
        var transport = new HttpReadOnlyKnowledgeSyncTransport(client, connection.Token);
        var worker = new KnowledgeSyncWorker(
            new KnowledgeSyncCoordinator(transport),
            new KnowledgeSnapshotStore(),
            snapshotPath);
        return (worker, client);
    }

    private void ClearQuery()
    {
        _query = string.Empty;
        _hits = Array.Empty<SearchHit>();
    }

    private void RecordUsage(string entryId)
    {
        _usageStatistics.RecordUse(entryId);
        try
        {
            _usageStore.SaveAtomic(_usagePath, _usageStatistics);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ResetSession()
    {
        _sessionArmed = false;
        ClearQuery();
        HideCandidates();
    }

    private void ShowCandidates(nint context)
    {
        if (context == 0)
        {
            return;
        }

        SetLastContext(context);
        var documentManager = GetDocumentManager(context);
        _candidateList.SetDocumentManager(documentManager);
        _candidateList.SetItems(_hits.Select(hit => hit.Entry.Question).ToArray());

        if (_uiElementManager is null)
        {
            return;
        }

        try
        {
            if (!_uiElementBegun)
            {
                var show = 1;
                var result = _uiElementManager.BeginUIElement(
                    _candidateList,
                    ref show,
                    out _uiElementId);
                if (result < 0)
                {
                    return;
                }

                _uiElementBegun = true;
                _candidateList.SetShown(show != 0);
            }
            else
            {
                _uiElementManager.UpdateUIElement(_uiElementId);
            }
        }
        catch (COMException)
        {
            HideCandidates();
        }
    }

    private void HideCandidates()
    {
        if (_uiElementBegun && _uiElementManager is not null)
        {
            try
            {
                _uiElementManager.EndUIElement(_uiElementId);
            }
            catch (COMException)
            {
            }
        }

        _uiElementBegun = false;
        _uiElementId = 0;
        _candidateList.SetShown(false);
        _candidateList.SetItems(Array.Empty<string>());
        _candidateList.SetDocumentManager(0);
        SetLastContext(0);
    }

    private void SetLastContext(nint context)
    {
        if (_lastContext != 0)
        {
            Marshal.Release(_lastContext);
        }

        _lastContext = context;
        if (_lastContext != 0)
        {
            Marshal.AddRef(_lastContext);
        }
    }

    private static nint GetDocumentManager(nint context)
    {
        try
        {
            var nativeContext = (ITfContextNative)Marshal.GetTypedObjectForIUnknown(
                context,
                typeof(ITfContextNative));
            return nativeContext.GetDocumentMgr(out var documentManager) >= 0
                ? documentManager
                : 0;
        }
        catch (COMException)
        {
            return 0;
        }
    }

    private bool ShouldEatSessionKey(int key)
    {
        if (!_sessionArmed)
        {
            return TsfInputActivationPolicy.ShouldAutoArm(
                key,
                SearchHits(char.ToLowerInvariant((char)key).ToString()).Count > 0);
        }

        if (key is >= 'A' and <= 'Z')
        {
            var nextQuery = _query + char.ToLowerInvariant((char)key);
            var decision = TsfQueryFallbackPolicy.Decide(
                _query,
                nextQuery,
                SearchHits(nextQuery).Count > 0);
            if (!decision.EatKey && _query.Length == 0)
            {
                ResetSession();
            }

            return decision.EatKey;
        }

        if (key == TsfConstants.VirtualKeyBackspace
            || key == TsfConstants.VirtualKeyEscape
            )
        {
            return true;
        }

        if (key == TsfConstants.VirtualKeyEnter || key == TsfConstants.VirtualKeySpace)
        {
            return _hits.Count > 0;
        }

        return key is >= TsfConstants.VirtualKeyF1 and <= TsfConstants.VirtualKeyF9
            && key - TsfConstants.VirtualKeyF1 < _hits.Count;
    }

    private static bool IsArmHotkey(int key)
    {
        return key == TsfConstants.VirtualKeySpace
            && TsfNativeMethods.IsKeyDown(TsfConstants.VirtualKeyControl)
            && TsfNativeMethods.IsKeyDown(TsfConstants.VirtualKeyShift);
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private static int HResultFromException(Exception exception)
    {
        return exception is COMException comException
            ? comException.HResult
            : TsfHResults.EFail;
    }
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class InsertTextEditSession : ITfEditSession
{
    private readonly nint _context;
    private readonly string _text;

    public InsertTextEditSession(nint context, string text)
    {
        _context = context;
        _text = text;
    }

    public int DoEditSession(uint editCookie)
    {
        try
        {
            var insertAtSelection = (ITfInsertAtSelectionNative)Marshal.GetTypedObjectForIUnknown(
                _context,
                typeof(ITfInsertAtSelectionNative));
            var result = insertAtSelection.InsertTextAtSelection(
                editCookie,
                flags: 0,
                _text,
                _text.Length,
                out var range);
            if (range != 0)
            {
                Marshal.Release(range);
            }
            return result;
        }
        catch (Exception exception)
        {
            return exception is COMException comException
                ? comException.HResult
                : TsfHResults.EFail;
        }
    }
}
