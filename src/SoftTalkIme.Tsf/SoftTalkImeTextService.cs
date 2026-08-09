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
    private readonly KnowledgeSyncWorker? _syncWorker;
    private readonly HttpClient? _syncHttpClient;
    private KnowledgeSnapshot _snapshot;
    private ITfKeystrokeManagerNative? _keystrokeManager;
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
        _snapshot = new KnowledgeSnapshotStore().LoadOrEmpty(_snapshotPath);
        (_syncWorker, _syncHttpClient) = CreateSyncWorker(_snapshotPath);
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
            _sessionArmed = true;
            ClearQuery();
            eaten = 1;
            return TsfHResults.SOk;
        }

        if (!_sessionArmed)
        {
            return TsfHResults.SOk;
        }

        if (key is >= 'A' and <= 'Z')
        {
            _query += char.ToLowerInvariant((char)key);
            RefreshHits();
            eaten = 1;
            return TsfHResults.SOk;
        }

        if (key == TsfConstants.VirtualKeyBackspace)
        {
            if (_query.Length > 0)
            {
                _query = _query[..^1];
                RefreshHits();
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

    private void RefreshHits()
    {
        _hits = KnowledgeSearchEngine.Search(Volatile.Read(ref _snapshot), _query);
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
        var baseUrl = Environment.GetEnvironmentVariable("SOFTTALK_IME_SYNC_BASE_URL");
        var token = Environment.GetEnvironmentVariable("SOFTTALK_IME_SYNC_TOKEN");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAddress))
        {
            return (null, null);
        }

        var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(20),
        };
        var transport = new HttpReadOnlyKnowledgeSyncTransport(client, token);
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

    private void ResetSession()
    {
        _sessionArmed = false;
        ClearQuery();
    }

    private bool ShouldEatSessionKey(int key)
    {
        if (!_sessionArmed)
        {
            return false;
        }

        if (key is >= 'A' and <= 'Z'
            || key == TsfConstants.VirtualKeyBackspace
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

    private static void ReleaseComObject(object value)
    {
        if (Marshal.IsComObject(value))
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
