using System.Runtime.InteropServices;

namespace SoftTalkIme.Tsf;

[ComVisible(true)]
[Guid("2B78E0A0-4FC7-4B2D-9B49-2B7BB870C501")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftTalkImeComHostProbe
{
    string GetContractVersion();
}

[ComVisible(true)]
[Guid("BFF5D7B2-5DC6-4A3A-B5A8-8A1F89E0C502")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class SoftTalkImeComHostProbe : ISoftTalkImeComHostProbe
{
    public SoftTalkImeComHostProbe()
    {
    }

    public string GetContractVersion()
    {
        return "SoftTalk-IME-ComHost-Probe/v1";
    }
}
