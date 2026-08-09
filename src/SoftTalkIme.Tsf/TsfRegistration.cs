using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace SoftTalkIme.Tsf;

public static class TsfRegistration
{
    public static readonly Guid TextServiceClsid = new("D8B1F2B4-9F1D-48A6-93E7-2D8B0F1D6D41");
    public static readonly Guid ProfileId = new("C1E7B9C8-7E3F-45CF-9E2A-3F705C4F0C6B");
    public static readonly Guid TfInputProcessorProfilesClsid = new("33C53A50-F456-4884-B049-85FD643ECFED");
    public static readonly Guid TfCategoryManagerClsid = new("A4B544A1-438D-4B41-9325-869523E2D6C7");
    public static readonly Guid TipKeyboardCategoryId = new("34745C63-B2F0-4784-8B67-5E12C8701A31");

    public const ushort SimplifiedChineseLanguageId = 0x0804;
    public const string Description = "话术精灵输入法";

    public static void Probe()
    {
        RunInSta(ProbeCore);
    }

    public static void Register()
    {
        RunInSta(RegisterCore);
    }

    public static void Unregister()
    {
        RunInSta(UnregisterCore);
    }

    private static void ProbeCore()
    {
        using var profiles = ComObject<ITfInputProcessorProfilesNative>.Create(TfInputProcessorProfilesClsid);
        using var categories = ComObject<ITfCategoryManagerNative>.Create(TfCategoryManagerClsid);
    }

    private static void RegisterCore()
    {
        using var profiles = ComObject<ITfInputProcessorProfilesNative>.Create(TfInputProcessorProfilesClsid);
        using var categories = ComObject<ITfCategoryManagerNative>.Create(TfCategoryManagerClsid);
        var serviceClsid = TextServiceClsid;
        var profileId = ProfileId;
        var keyboardCategory = TipKeyboardCategoryId;

        Check(profiles.Value.Register(ref serviceClsid), "ITfInputProcessorProfiles.Register");
        try
        {
            Check(
                profiles.Value.AddLanguageProfile(
                    ref serviceClsid,
                    SimplifiedChineseLanguageId,
                    ref profileId,
                    Description,
                    (uint)Description.Length,
                    null,
                    0,
                    0),
                "ITfInputProcessorProfiles.AddLanguageProfile");
            Check(
                categories.Value.RegisterCategory(
                    ref serviceClsid,
                    ref keyboardCategory,
                    ref serviceClsid),
                "ITfCategoryMgr.RegisterCategory");
        }
        catch
        {
            profiles.Value.Unregister(ref serviceClsid);
            throw;
        }
    }

    private static void UnregisterCore()
    {
        using var profiles = ComObject<ITfInputProcessorProfilesNative>.Create(TfInputProcessorProfilesClsid);
        using var categories = ComObject<ITfCategoryManagerNative>.Create(TfCategoryManagerClsid);
        var serviceClsid = TextServiceClsid;
        var profileId = ProfileId;
        var keyboardCategory = TipKeyboardCategoryId;

        categories.Value.UnregisterCategory(ref serviceClsid, ref keyboardCategory, ref serviceClsid);
        profiles.Value.RemoveLanguageProfile(
            ref serviceClsid,
            SimplifiedChineseLanguageId,
            ref profileId);
        Check(profiles.Value.Unregister(ref serviceClsid), "ITfInputProcessorProfiles.Unregister");
    }

    private static void Check(int hResult, string operation)
    {
        if (hResult < 0)
        {
            Marshal.ThrowExceptionForHR(hResult);
        }
    }

    private static void RunInSta(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [ComImport]
    [ComVisible(true)]
    [Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfInputProcessorProfilesNative
    {
        [PreserveSig]
        int Register(ref Guid clsid);

        [PreserveSig]
        int Unregister(ref Guid clsid);

        [PreserveSig]
        int AddLanguageProfile(
            ref Guid clsid,
            ushort languageId,
            ref Guid profileId,
            [MarshalAs(UnmanagedType.LPWStr)] string description,
            uint descriptionLength,
            [MarshalAs(UnmanagedType.LPWStr)] string? iconFile,
            uint iconFileLength,
            uint iconIndex);

        [PreserveSig]
        int RemoveLanguageProfile(ref Guid clsid, ushort languageId, ref Guid profileId);
    }

    [ComImport]
    [ComVisible(true)]
    [Guid("C3ACEFB5-F69D-4905-938F-FCADCF4BE830")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfCategoryManagerNative
    {
        [PreserveSig]
        int RegisterCategory(ref Guid clsid, ref Guid categoryId, ref Guid itemId);

        [PreserveSig]
        int UnregisterCategory(ref Guid clsid, ref Guid categoryId, ref Guid itemId);
    }

    private sealed class ComObject<T> : IDisposable
        where T : class
    {
        private readonly object _instance;

        private ComObject(object instance)
        {
            _instance = instance;
            Value = (T)instance;
        }

        public T Value { get; }

        public static ComObject<T> Create(Guid clsid)
        {
            var comType = Type.GetTypeFromCLSID(clsid, throwOnError: true)
                ?? throw new InvalidOperationException($"找不到 COM 类：{clsid}");
            var instance = Activator.CreateInstance(comType)
                ?? throw new InvalidOperationException($"无法创建 COM 类：{clsid}");
            return new ComObject<T>(instance);
        }

        public void Dispose()
        {
            if (Marshal.IsComObject(_instance))
            {
                Marshal.FinalReleaseComObject(_instance);
            }
        }
    }
}
