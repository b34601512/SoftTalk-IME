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
