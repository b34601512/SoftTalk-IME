using System.Runtime.InteropServices;

namespace SoftTalkIme.Tsf;

internal static class TsfHResults
{
    public const int SOk = 0;
    public const int EInvalidArg = unchecked((int)0x80070057);
    public const int EFail = unchecked((int)0x80004005);
}

internal static class TsfConstants
{
    public const uint TfEsSync = 0x00000001;
    public const uint TfEsReadWrite = 0x00000006;
    public const int VirtualKeyBackspace = 0x08;
    public const int VirtualKeyEnter = 0x0D;
    public const int VirtualKeyEscape = 0x1B;
    public const int VirtualKeySpace = 0x20;
    public const int VirtualKeyF1 = 0x70;
    public const int VirtualKeyF9 = 0x78;
    public const int VirtualKeyControl = 0x11;
    public const int VirtualKeyShift = 0x10;
}

[ComVisible(true)]
[Guid("AA80E7F7-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITfTextInputProcessor
{
    [PreserveSig]
    int Activate(nint threadManager, uint clientId);

    [PreserveSig]
    int Deactivate();
}

[ComVisible(true)]
[Guid("AA80E7F5-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITfKeyEventSink
{
    [PreserveSig]
    int OnSetFocus(int foreground);

    [PreserveSig]
    int OnTestKeyDown(nint context, nuint virtualKey, nint keyData, out int eaten);

    [PreserveSig]
    int OnTestKeyUp(nint context, nuint virtualKey, nint keyData, out int eaten);

    [PreserveSig]
    int OnKeyDown(nint context, nuint virtualKey, nint keyData, out int eaten);

    [PreserveSig]
    int OnKeyUp(nint context, nuint virtualKey, nint keyData, out int eaten);

    [PreserveSig]
    int OnPreservedKey(nint context, ref Guid command, out int eaten);
}

[ComVisible(true)]
[Guid("EA1EA137-19DF-11D7-A6D2-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITfUIElement
{
    [PreserveSig]
    int GetDescription([MarshalAs(UnmanagedType.BStr)] out string description);

    [PreserveSig]
    int GetGUID(out Guid elementGuid);

    [PreserveSig]
    int Show(int show);

    [PreserveSig]
    int IsShown(out int show);
}

[ComVisible(true)]
[Guid("EA1EA138-19DF-11D7-A6D2-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITfCandidateListUIElement : ITfUIElement
{
    [PreserveSig]
    int GetUpdatedFlags(out uint flags);

    [PreserveSig]
    int GetDocumentMgr(out nint documentManager);

    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetSelection(out uint index);

    [PreserveSig]
    int GetString(uint index, [MarshalAs(UnmanagedType.BStr)] out string text);

    [PreserveSig]
    int GetPageIndex(
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] uint[]? index,
        uint size,
        out uint pageCount);

    [PreserveSig]
    int SetPageIndex(
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] uint[]? index,
        uint pageCount);

    [PreserveSig]
    int GetCurrentPage(out uint page);
}

[ComVisible(true)]
[Guid("85FAD185-58CE-497A-9460-355366B64B9A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITfCandidateListUIElementBehavior
{
    [PreserveSig]
    int SetSelection(uint index);

    [PreserveSig]
    int FinalizeCandidate();

    [PreserveSig]
    int Abort();
}

[ComVisible(true)]
[Guid("AA80E803-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfEditSession
{
    [PreserveSig]
    int DoEditSession(uint editCookie);
}

[ComImport]
[Guid("AA80E7F0-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfKeystrokeManagerNative
{
    [PreserveSig]
    int AdviseKeyEventSink(
        uint clientId,
        [MarshalAs(UnmanagedType.Interface)] ITfKeyEventSink sink,
        [MarshalAs(UnmanagedType.Bool)] bool foreground);

    [PreserveSig]
    int UnadviseKeyEventSink(uint clientId);
}

[ComImport]
[Guid("AA80E7FD-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfContextNative
{
    [PreserveSig]
    int RequestEditSession(
        uint clientId,
        [MarshalAs(UnmanagedType.Interface)] ITfEditSession editSession,
        uint flags,
        out int sessionResult);

    [PreserveSig]
    int InWriteSession(uint clientId, out int writeSession);

    [PreserveSig]
    int GetSelection(uint editCookie, uint index, uint count, nint selection, out uint fetched);

    [PreserveSig]
    int SetSelection(uint editCookie, uint count, nint selection);

    [PreserveSig]
    int GetStart(uint editCookie, out nint range);

    [PreserveSig]
    int GetEnd(uint editCookie, out nint range);

    [PreserveSig]
    int GetActiveView(out nint view);

    [PreserveSig]
    int EnumViews(out nint views);

    [PreserveSig]
    int GetStatus(out nint status);

    [PreserveSig]
    int GetProperty(ref Guid propertyGuid, out nint property);

    [PreserveSig]
    int GetAppProperty(ref Guid propertyGuid, out nint property);

    [PreserveSig]
    int TrackProperties(
        nint propertyGuids,
        uint propertyCount,
        nint appPropertyGuids,
        uint appPropertyCount,
        out nint property);

    [PreserveSig]
    int EnumProperties(out nint properties);

    [PreserveSig]
    int GetDocumentMgr(out nint documentManager);

    [PreserveSig]
    int CreateRangeBackup(uint editCookie, nint range, out nint backup);
}

[ComImport]
[Guid("EA1EA135-19DF-11D7-A6D2-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfUIElementManagerNative
{
    [PreserveSig]
    int BeginUIElement(
        [MarshalAs(UnmanagedType.Interface)] ITfUIElement element,
        ref int show,
        out uint elementId);

    [PreserveSig]
    int UpdateUIElement(uint elementId);

    [PreserveSig]
    int EndUIElement(uint elementId);
}

[ComImport]
[Guid("55CE16BA-3014-41C1-9CEB-FADE1446AC6C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfInsertAtSelectionNative
{
    [PreserveSig]
    int InsertTextAtSelection(
        uint editCookie,
        uint flags,
        [MarshalAs(UnmanagedType.LPWStr)] string text,
        int characterCount,
        out nint range);
}

internal static class TsfNativeMethods
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    public static bool IsKeyDown(int virtualKey)
    {
        return (GetKeyState(virtualKey) & 0x8000) != 0;
    }
}
