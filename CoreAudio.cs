// CoreAudio 互操作层（接口定义参考 EarTrumpet, MIT License, https://github.com/File-New-Project/EarTrumpet）
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VolumeCtrlLite
{
    internal enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    internal enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }
    [Flags]
    internal enum DeviceState { ACTIVE = 0x1, DISABLED = 0x2, NOTPRESENT = 0x4, UNPLUGGED = 0x8, ALL = 0xF }
    internal enum STGM { STGM_READ = 0 }
    internal enum AudioSessionState { Inactive = 0, Active = 1, Expired = 2 }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        IMMDeviceCollection EnumAudioEndpoints(EDataFlow dataFlow, DeviceState dwStateMask);
        [return: MarshalAs(UnmanagedType.Interface)]
        IMMDevice GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role);
        [return: MarshalAs(UnmanagedType.Interface)]
        IMMDevice GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId);
        void RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] object pClient);
        void UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] object pClient);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        uint GetCount();
        [return: MarshalAs(UnmanagedType.Interface)]
        IMMDevice Item(uint index);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        void Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [return: MarshalAs(UnmanagedType.Interface)]
        IPropertyStore OpenPropertyStore(STGM stgmAccess);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetId();
        DeviceState GetState();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public UIntPtr pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public ushort varType;
        [FieldOffset(2)] public ushort wReserved1;
        [FieldOffset(4)] public ushort wReserved2;
        [FieldOffset(6)] public ushort wReserved3;
        [FieldOffset(8)] public IntPtr pwszVal;
        [FieldOffset(8)] public uint uintVal;
        [FieldOffset(8)] public ulong ulVal;
        [FieldOffset(8)] public IntPtr pclsidVal;
        [FieldOffset(8)] public IntPtr pointerVal;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint cProps);
        [PreserveSig]
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        PropVariant GetValue(ref PROPERTYKEY key);
        [PreserveSig]
        int SetValue(ref PROPERTYKEY key, ref PropVariant pv);
        [PreserveSig]
        int Commit();
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr pNotify);
        void UnregisterControlChangeNotify(IntPtr pNotify);
        uint GetChannelCount();
        void SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        void SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        void GetMasterVolumeLevel(out float pfLevelDB);
        void GetMasterVolumeLevelScalar(out float pfLevel);
        void SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        void SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        void GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        float GetChannelVolumeLevelScalar(uint nChannel);
        void SetMute(int bMute, ref Guid pguidEventContext);
        int GetMute();
        void GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        void VolumeStepUp(ref Guid pguidEventContext);
        void VolumeStepDown(ref Guid pguidEventContext);
        void QueryHardwareSupport(out uint pdwHardwareSupportMask);
        void GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        void GetAudioSessionControl(ref Guid AudioSessionGuid, uint StreamFlags, [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl SessionControl);
        void GetSimpleAudioVolume(ref Guid AudioSessionGuid, uint StreamFlags, [MarshalAs(UnmanagedType.Interface)] out ISimpleAudioVolume AudioVolume);
        [return: MarshalAs(UnmanagedType.Interface)]
        IAudioSessionEnumerator GetSessionEnumerator();
        void RegisterSessionNotification(IntPtr SessionNotification);
        void UnregisterSessionNotification(IntPtr SessionNotification);
        void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr duckNotification);
        void UnregisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        int GetCount();
        [return: MarshalAs(UnmanagedType.Interface)]
        IAudioSessionControl GetSession(int SessionCount);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
    {
        AudioSessionState GetState();
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName();
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetIconPath();
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        Guid GetGroupingParam();
        void SetGroupingParam(ref Guid Override, ref Guid EventContext);
        void RegisterAudioSessionNotification(IntPtr NewNotifications);
        void UnregisterAudioSessionNotification(IntPtr NewNotifications);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        AudioSessionState GetState();
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName();
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetIconPath();
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        Guid GetGroupingParam();
        void SetGroupingParam(ref Guid Override, ref Guid EventContext);
        void RegisterAudioSessionNotification(IntPtr NewNotifications);
        void UnregisterAudioSessionNotification(IntPtr NewNotifications);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetSessionIdentifier();
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetSessionInstanceIdentifier();
        [PreserveSig]
        int GetProcessId(out uint processId);
        [PreserveSig]
        int IsSystemSoundsSession();
        void SetDuckingPreference(int optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        void SetMasterVolume(float fLevel, ref Guid EventContext);
        void GetMasterVolume(out float pfLevel);
        void SetMute(int bMute, ref Guid EventContext);
        int GetMute();
    }

    [ComImport, Guid("1C158861-B533-4B30-B1CF-E853E51C59B8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IChannelAudioVolume
    {
        uint GetChannelCount();
        [PreserveSig]
        int SetChannelVolume(uint index, float level, ref Guid eventContext);
        [PreserveSig]
        int GetChannelVolume(uint index, out float level);
        [PreserveSig]
        int SetAllVolumes(uint count, IntPtr afLevels, ref Guid eventContext);
        [PreserveSig]
        int GetAllVolumes(uint count, IntPtr afLevels);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        float GetPeakValue();
        uint GetMeteringChannelCount();
        [PreserveSig]
        int GetChannelsPeakValues(uint channelCount, IntPtr afPeakValues);
        void QueryHardwareSupport(out uint pdwHardwareSupportMask);
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    internal class PolicyConfigClient { }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfigWin7
    {
        void Unused1();
        void Unused2();
        void Unused3();
        void Unused4();
        void Unused5();
        void Unused6();
        void Unused7();
        void Unused8();
        void GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ref PROPERTYKEY pkey, ref PropVariant pv);
        void SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ref PROPERTYKEY pkey, ref PropVariant pv);
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ERole eRole);
        void SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, [MarshalAs(UnmanagedType.I2)] short isVisible);
    }

    internal static class Ole32
    {
        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PropVariant pvar);
    }

    internal static class Combase
    {
        [DllImport("combase.dll", PreserveSig = false)]
        public static extern void RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            [Out, MarshalAs(UnmanagedType.IInspectable)] out Object factory);

        [DllImport("combase.dll", PreserveSig = false)]
        public static extern void WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string src,
            [In] uint length,
            [Out] out IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = false)]
        public static extern void WindowsDeleteString(IntPtr hstring);
    }

    // IAudioPolicyConfigFactory（Win11 21H2+ 变体）。用 InterfaceIsIUnknown 时需补 IInspectable 的 3 个方法。
    [ComImport, Guid("ab3d4648-e242-459f-b02f-541c70306324")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioPolicyConfigFactory21H2
    {
        int GetIids(out uint iidCount, out IntPtr iids);
        int GetRuntimeClassName(out IntPtr className);
        int GetTrustLevel(out int trustLevel);
        int __incomplete__add_CtxVolumeChange();
        int __incomplete__remove_CtxVolumeChanged();
        int __incomplete__add_RingerVibrateStateChanged();
        int __incomplete__remove_RingerVibrateStateChange();
        int __incomplete__SetVolumeGroupGainForId();
        int __incomplete__GetVolumeGroupGainForId();
        int __incomplete__GetActiveVolumeGroupForEndpointId();
        int __incomplete__GetVolumeGroupsForEndpoint();
        int __incomplete__GetCurrentVolumeContext();
        int __incomplete__SetVolumeGroupMuteForId();
        int __incomplete__GetVolumeGroupMuteForId();
        int __incomplete__SetRingerVibrateState();
        int __incomplete__GetRingerVibrateState();
        int __incomplete__SetPreferredChatApplication();
        int __incomplete__ResetPreferredChatApplication();
        int __incomplete__GetPreferredChatApplication();
        int __incomplete__GetCurrentChatApplications();
        int __incomplete__add_ChatContextChanged();
        int __incomplete__remove_ChatContextChanged();
        [PreserveSig]
        int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, IntPtr deviceId);
        [PreserveSig]
        int GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, [Out, MarshalAs(UnmanagedType.HString)] out string deviceId);
        [PreserveSig]
        int ClearAllPersistedApplicationDefaultEndpoints();
    }

    internal static class AudioRouting
    {
        private const string RenderToken = "\\\\?\\SWD#MMDEVAPI#";
        private const string RenderInterface = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        private static IAudioPolicyConfigFactory21H2 _factory;
        private static bool _failed;

        private static IAudioPolicyConfigFactory21H2 GetFactory()
        {
            if (_factory != null || _failed) return _factory;
            try
            {
                Guid iid = typeof(IAudioPolicyConfigFactory21H2).GUID;
                object factory;
                Combase.RoGetActivationFactory("Windows.Media.Internal.AudioPolicyConfig", ref iid, out factory);
                _factory = factory as IAudioPolicyConfigFactory21H2;
                if (_factory == null) _failed = true;
            }
            catch
            {
                _failed = true;
            }
            return _factory;
        }

        public static bool IsSupported
        {
            get { return GetFactory() != null; }
        }

        /// <summary>把指定进程的音频会话固定到某个输出设备（不修改系统默认设备）。</summary>
        public static bool SetAppDevice(int processId, string deviceId)
        {
            return SetAppDevice(processId, deviceId, EDataFlow.eRender);
        }

        /// <summary>把指定进程的音频会话固定到某个设备；flow 为 eCapture 时改的是该应用的输入设备。</summary>
        public static bool SetAppDevice(int processId, string deviceId, EDataFlow flow)
        {
            IAudioPolicyConfigFactory21H2 factory = GetFactory();
            if (factory == null || processId <= 0) return false;
            IntPtr hstring = IntPtr.Zero;
            try
            {
                string full = RenderToken + deviceId + RenderInterface;
                Combase.WindowsCreateString(full, (uint)full.Length, out hstring);
                int hr = factory.SetPersistedDefaultAudioEndpoint((uint)processId, flow, ERole.eMultimedia, hstring);
                factory.SetPersistedDefaultAudioEndpoint((uint)processId, flow, ERole.eConsole, hstring);
                return hr == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hstring != IntPtr.Zero)
                {
                    try { Combase.WindowsDeleteString(hstring); } catch { }
                }
            }
        }
    }

    internal class DeviceInfo
    {
        public string Id;
        public string Name;
        public bool IsDefault;
    }

    internal class SessionInfo
    {
        public string Id;
        public string Name;
        public string ProcessName;
        public int ProcessId;
        public bool IsSystemSounds;
        public float Volume;
        public bool IsMuted;
        public ISimpleAudioVolume Control;
        public IChannelAudioVolume Channels;
        public IAudioMeterInformation Meter;
        public uint ChannelCount;
    }

    internal static class Audio
    {
        private static IMMDeviceEnumerator _enumerator;

        private static IMMDeviceEnumerator Enumerator
        {
            get
            {
                if (_enumerator == null)
                    _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                return _enumerator;
            }
        }

        private static readonly PROPERTYKEY PKEY_Device_FriendlyName = new PROPERTYKEY
        {
            fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            pid = (UIntPtr)14
        };

        private static string GetFriendlyName(IMMDevice device)
        {
            IPropertyStore store = null;
            try
            {
                store = device.OpenPropertyStore(STGM.STGM_READ);
                PROPERTYKEY key = PKEY_Device_FriendlyName;
                PropVariant pv = store.GetValue(ref key);
                try
                {
                    if (pv.varType == 31 && pv.pwszVal != IntPtr.Zero)
                        return Marshal.PtrToStringUni(pv.pwszVal);
                }
                finally
                {
                    Ole32.PropVariantClear(ref pv);
                }
            }
            catch { }
            finally
            {
                if (store != null) Marshal.ReleaseComObject(store);
            }
            return null;
        }

        private static T Activate<T>(IMMDevice device) where T : class
        {
            Guid iid = typeof(T).GUID;
            object obj;
            device.Activate(ref iid, 1 /*CLSCTX_INPROC_SERVER*/, IntPtr.Zero, out obj);
            return obj as T;
        }

        public static List<DeviceInfo> GetOutputDevices()
        {
            List<DeviceInfo> list = new List<DeviceInfo>();
            string defaultId = GetDefaultOutputDeviceId();
            IMMDeviceCollection collection = null;
            try
            {
                collection = Enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceState.ACTIVE);
                uint count = collection.GetCount();
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice dev = null;
                    try
                    {
                        dev = collection.Item(i);
                        if (dev == null) continue;
                        DeviceInfo info = new DeviceInfo();
                        info.Id = dev.GetId();
                        info.Name = GetFriendlyName(dev);
                        if (string.IsNullOrEmpty(info.Name)) info.Name = "输出设备 " + (i + 1);
                        info.IsDefault = (info.Id == defaultId);
                        list.Add(info);
                    }
                    finally
                    {
                        if (dev != null) Marshal.ReleaseComObject(dev);
                    }
                }
            }
            finally
            {
                if (collection != null) Marshal.ReleaseComObject(collection);
            }
            return list;
        }

        public static string GetDefaultOutputDeviceId()
        {
            IMMDevice dev = null;
            try
            {
                dev = Enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                return dev == null ? null : dev.GetId();
            }
            catch { return null; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
            }
        }

        public static IAudioEndpointVolume GetDefaultEndpointVolume()
        {
            return GetEndpointVolumeForDevice(null);
        }

        public static IAudioEndpointVolume GetEndpointVolumeForDevice(string deviceId)
        {
            IMMDevice dev = null;
            try
            {
                if (string.IsNullOrEmpty(deviceId))
                    dev = Enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                else
                    dev = Enumerator.GetDevice(deviceId);
                if (dev == null) return null;
                return Activate<IAudioEndpointVolume>(dev);
            }
            catch { return null; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
            }
        }

        public static List<SessionInfo> GetSessions()
        {
            return GetSessionsForDevice(null);
        }

        public static List<SessionInfo> GetSessionsForDevice(string deviceId)
        {
            List<SessionInfo> list = new List<SessionInfo>();
            IMMDevice dev = null;
            try
            {
                if (string.IsNullOrEmpty(deviceId))
                    dev = Enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                else
                    dev = Enumerator.GetDevice(deviceId);
                if (dev == null) return list;
                IAudioSessionManager2 mgr = Activate<IAudioSessionManager2>(dev);
                if (mgr == null) return list;

                IAudioSessionEnumerator sessionEnum = null;
                try
                {
                    sessionEnum = mgr.GetSessionEnumerator();
                    if (sessionEnum == null) return list;
                    int count = sessionEnum.GetCount();
                    for (int i = 0; i < count; i++)
                    {
                        IAudioSessionControl ctl = null;
                        try
                        {
                            ctl = sessionEnum.GetSession(i);
                            if (ctl == null) continue;
                            if (ctl.GetState() == AudioSessionState.Expired) continue;

                            IAudioSessionControl2 ctl2 = ctl as IAudioSessionControl2;
                            if (ctl2 == null) continue;

                            string sessionId = null;
                            try { sessionId = ctl2.GetSessionInstanceIdentifier(); }
                            catch { }

                            bool isSystemSounds = ctl2.IsSystemSoundsSession() == 0;
                            uint pid = 0;
                            try { ctl2.GetProcessId(out pid); }
                            catch { }

                            ISimpleAudioVolume simple = ctl as ISimpleAudioVolume;
                            if (simple == null) continue;

                            float level = 0f;
                            int mute = 0;
                            try
                            {
                                simple.GetMasterVolume(out level);
                                mute = simple.GetMute();
                            }
                            catch { continue; }

                            string procName = null;
                            if (!isSystemSounds && pid > 0)
                            {
                                try
                                {
                                    using (Process proc = Process.GetProcessById((int)pid))
                                    {
                                        procName = proc.ProcessName;
                                    }
                                }
                                catch { }
                            }

                            SessionInfo info = new SessionInfo();
                            info.Id = string.IsNullOrEmpty(sessionId) ? ("pid:" + pid + ":" + i) : sessionId;
                            info.ProcessId = (int)pid;
                            info.ProcessName = procName;
                            info.IsSystemSounds = isSystemSounds;
                            info.Volume = level;
                            info.IsMuted = mute != 0;
                            info.Control = GetUniqueInterface<ISimpleAudioVolume>(ctl);
                            info.Channels = GetUniqueInterface<IChannelAudioVolume>(ctl);
                            info.Meter = GetSessionMeter(ctl);
                            if (info.Channels != null)
                            {
                                try { info.ChannelCount = info.Channels.GetChannelCount(); }
                                catch { info.ChannelCount = 0; }
                            }
                            info.Name = ResolveName(isSystemSounds, procName);
                            list.Add(info);
                        }
                        catch { }
                        finally
                        {
                            if (ctl != null) Marshal.ReleaseComObject(ctl);
                        }
                    }
                }
                finally
                {
                    if (sessionEnum != null) Marshal.ReleaseComObject(sessionEnum);
                    if (mgr != null) Marshal.ReleaseComObject(mgr);
                }
            }
            catch { }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
            }
            return list;
        }

        private static string ResolveName(bool isSystemSounds, string procName)
        {
            if (isSystemSounds) return "系统声音";
            if (!string.IsNullOrEmpty(procName))
            {
                if (procName.Length == 1) return procName.ToUpper();
                return char.ToUpper(procName[0]) + procName.Substring(1);
            }
            return "系统";
        }

        public static List<DeviceInfo> GetInputDevices()
        {
            List<DeviceInfo> list = new List<DeviceInfo>();
            string defaultId = GetDefaultInputDeviceId();
            IMMDeviceCollection collection = null;
            try
            {
                collection = Enumerator.EnumAudioEndpoints(EDataFlow.eCapture, DeviceState.ACTIVE);
                uint count = collection.GetCount();
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice dev = null;
                    try
                    {
                        dev = collection.Item(i);
                        if (dev == null) continue;
                        DeviceInfo info = new DeviceInfo();
                        info.Id = dev.GetId();
                        info.Name = GetFriendlyName(dev);
                        if (string.IsNullOrEmpty(info.Name)) info.Name = "麦克风 " + (i + 1);
                        info.IsDefault = info.Id == defaultId;
                        list.Add(info);
                    }
                    finally
                    {
                        if (dev != null) Marshal.ReleaseComObject(dev);
                    }
                }
            }
            finally
            {
                if (collection != null) Marshal.ReleaseComObject(collection);
            }
            return list;
        }

        public static string GetDefaultInputDeviceId()
        {
            IMMDevice dev = null;
            try
            {
                dev = Enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications);
                return dev == null ? null : dev.GetId();
            }
            catch { return null; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
            }
        }

        public static IAudioEndpointVolume GetDefaultInputEndpointVolume()
        {
            IMMDevice dev = null;
            try
            {
                dev = Enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications);
                if (dev == null) return null;
                return Activate<IAudioEndpointVolume>(dev);
            }
            catch { return null; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
            }
        }

        private static T GetUniqueInterface<T>(object source) where T : class
        {
            if (source == null) return null;
            try
            {
                IntPtr unk = Marshal.GetIUnknownForObject(source);
                try
                {
                    IntPtr ptr;
                    Guid iid = typeof(T).GUID;
                    int hr = Marshal.QueryInterface(unk, ref iid, out ptr);
                    if (hr == 0 && ptr != IntPtr.Zero)
                    {
                        try { return Marshal.GetUniqueObjectForIUnknown(ptr) as T; }
                        finally { Marshal.Release(ptr); }
                    }
                }
                finally { Marshal.Release(unk); }
            }
            catch { }
            return null;
        }

        private static IAudioMeterInformation GetSessionMeter(object session)
        {
            if (session == null) return null;
            try
            {
                IntPtr unk = Marshal.GetIUnknownForObject(session);
                try
                {
                    IntPtr ptr;
                    Guid iid = typeof(IAudioMeterInformation).GUID;
                    int hr = Marshal.QueryInterface(unk, ref iid, out ptr);
                    if (hr == 0 && ptr != IntPtr.Zero)
                    {
                        try
                        {
                            // 创建独立 RCW，避免 ctl 被 ReleaseComObject 后失效
                            object unique = Marshal.GetUniqueObjectForIUnknown(ptr);
                            return unique as IAudioMeterInformation;
                        }
                        finally { Marshal.Release(ptr); }
                    }
                }
                finally { Marshal.Release(unk); }
            }
            catch { }
            return null;
        }

        public static IAudioMeterInformation GetDeviceMeter(string deviceId)
        {
            IMMDevice dev = null;
            try
            {
                if (string.IsNullOrEmpty(deviceId))
                    dev = Enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                else
                    dev = Enumerator.GetDevice(deviceId);
                if (dev == null) return null;
                return Activate<IAudioMeterInformation>(dev);
            }
            catch { return null; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
            }
        }

        public static bool SetEndpointMute(IAudioEndpointVolume endpoint, bool mute)
        {
            if (endpoint == null) return false;
            try
            {
                Guid g = Guid.Empty;
                endpoint.SetMute(mute ? 1 : 0, ref g);
                return true;
            }
            catch { return false; }
        }

        public static float GetEndpointVolumeScalar(IAudioEndpointVolume endpoint)
        {
            if (endpoint == null) return 0f;
            try
            {
                float level;
                endpoint.GetMasterVolumeLevelScalar(out level);
                return level;
            }
            catch { return 0f; }
        }

        public static bool GetEndpointMute(IAudioEndpointVolume endpoint)
        {
            if (endpoint == null) return false;
            try { return endpoint.GetMute() != 0; }
            catch { return false; }
        }

        public static bool SetEndpointVolumeScalar(IAudioEndpointVolume endpoint, float level)
        {
            if (endpoint == null) return false;
            try
            {
                Guid g = Guid.Empty;
                endpoint.SetMasterVolumeLevelScalar(Math.Max(0f, Math.Min(1f, level)), ref g);
                return true;
            }
            catch { return false; }
        }

        public static bool SetDefaultDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            IPolicyConfigWin7 policy = null;
            try
            {
                policy = (IPolicyConfigWin7)new PolicyConfigClient();
                policy.SetDefaultEndpoint(deviceId, ERole.eConsole);
                policy.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                policy.SetDefaultEndpoint(deviceId, ERole.eCommunications);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (policy != null) Marshal.ReleaseComObject(policy);
            }
        }
    }
}





