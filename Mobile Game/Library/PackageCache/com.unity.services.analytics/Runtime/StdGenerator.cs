using System;
using System.Runtime.CompilerServices;
using Unity.Services.Analytics.Internal;
using Unity.Services.Analytics.Platform;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.Services.Analytics.Tests")]

namespace Unity.Services.Analytics.Data
{
    interface ICommonData
    {
        string Version { get; }
        string GameBundleId { get; }
        string ProjectId { get; }
        string Platform { get; }
        string BuildGUID { get; }
        string Idfv { get; }
        string GameStoreId { get; }

        bool HasVolume { get; }
        float Volume { get; }
        double BatteryLevel { get; }
    }

    interface IDeviceData
    {
        string CpuType { get; }
        string GpuType { get; }
        int CpuCores { get; }
        int RamTotal { get; }
        int ScreenWidth { get; }
        int ScreenHeight { get; }
        float ScreenDpi { get; }
    }

    class DeviceDataWrapper : IDeviceData
    {
        public string CpuType { get { return SystemInfo.processorType; } }
        public string GpuType { get { return SystemInfo.graphicsDeviceName; } }
        public int CpuCores { get { return SystemInfo.processorCount; } }
        public int RamTotal { get { return SystemInfo.systemMemorySize; } }
        public int ScreenWidth { get { return Screen.width; } }
        public int ScreenHeight { get { return Screen.height; } }
        public float ScreenDpi { get { return Screen.dpi; } }
    }

    class CommonDataWrapper : ICommonData
    {
        public string Version { get; }
        public string GameBundleId { get; }
        public string ProjectId { get; }
        public string Platform { get; }
        public string BuildGUID { get; }
        public string Idfv { get; }
        public string GameStoreId { get; }

        public bool HasVolume { get; }
        public float Volume { get { return DeviceVolumeProvider.GetDeviceVolume(); } }
        public double BatteryLevel { get { return SystemInfo.batteryLevel; } }

        public CommonDataWrapper(string cloudProjectId)
        {
            Version = Application.version;
            ProjectId = cloudProjectId;
            GameBundleId = Application.identifier;
            Platform = GetPlatform();
            BuildGUID = Application.buildGUID;
            Idfv = SystemInfo.deviceUniqueIdentifier;

            // TODO: We never worked out what GameStoreId actually means and it is not possible for the developer to set it anyway.
            GameStoreId = null;

            HasVolume = DeviceVolumeProvider.VolumeAvailable;
        }

        static string GetPlatform()
        {
            // NOTE: Assumes we're only supporting Unity LTS
            // The string values for each platform are an enum in the schema so they MUST be consistent.
            // Yes, this means that adding new platforms requires both schema and SDK changes.
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return "MAC_CLIENT";
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return "PC_CLIENT";
                case RuntimePlatform.IPhonePlayer:
                    return "IOS";
                case RuntimePlatform.Android:
                    return "ANDROID";
                case RuntimePlatform.WebGLPlayer:
                    return "WEB";
                case RuntimePlatform.WSAPlayerX64:
                case RuntimePlatform.WSAPlayerX86:
                case RuntimePlatform.WSAPlayerARM:
                    return (SystemInfo.deviceType == DeviceType.Handheld)
                        ? "WINDOWS_MOBILE"
                        : "PC_CLIENT";
                case RuntimePlatform.PS4:
                    return "PS4";
                case RuntimePlatform.XboxOne:
                    return "XBOXONE";
                case RuntimePlatform.tvOS:
                    return "IOS_TV";
                case RuntimePlatform.Switch:
                    return "SWITCH";
                default:
                    return "UNKNOWN";
            }
        }
    }

    interface IDataGenerator
    {
        void GameRunning(string callingMethodIdentifier);
        void SdkStartup(string callingMethodIdentifier);
        void NewPlayer(string callingMethodIdentifier, string deviceModel);
        void GameStarted(string callingMethodIdentifier, string idLocalProject, string osVersion, bool isTiny, bool debugDevice, string userLocale);
        void GameEnded(string callingMethodIdentifier, DataGenerator.SessionEndState quitState);
        void ClientDevice(string callingMethodIdentifier);

        void PushCommonParams(string callingMethodIdentifier);

        void PushEvent(string callingMethodIdentifier, Event e);
        void PushEmptyEvent(string name);
    }

    class DataGenerator : IDataGenerator
    {
        // Keep the enum values in Caps!
        // We stringify the values.
        // These values aren't listed as an enum the Schema, but they are listed
        // values here http://go/UA2_Spreadsheet
        internal enum SessionEndState
        {
            PAUSED,
            KILLEDINBACKGROUND,
            KILLEDINFOREGROUND,
            QUIT,
        }

        readonly IBuffer m_Buffer;
        readonly ICommonData m_CommonData;
        readonly IDeviceData m_DeviceData;

        public DataGenerator(IBuffer buffer, ICommonData staticData, IDeviceData deviceData)
        {
            m_Buffer = buffer;
            m_CommonData = staticData;
            m_DeviceData = deviceData;
        }

        public void SdkStartup(string callingMethodIdentifier)
        {
            m_Buffer.PushStandardEventStart("sdkStart", 1);
            m_Buffer.PushString("sdkVersion", SdkVersion.SDK_VERSION);

            PushCommonParams(callingMethodIdentifier);

            m_Buffer.PushString("sdkName", "com.unity.services.analytics");

            m_Buffer.PushEndEvent();
        }

        public void GameRunning(string callingMethodIdentifier)
        {
            m_Buffer.PushStandardEventStart("gameRunning", 1);

            PushCommonParams(callingMethodIdentifier);

            m_Buffer.PushEndEvent();
        }

        public void NewPlayer(string callingMethodIdentifier, string deviceModel)
        {
            m_Buffer.PushStandardEventStart("newPlayer", 1);

            PushCommonParams(callingMethodIdentifier);

            m_Buffer.PushString("deviceModel", deviceModel);

            m_Buffer.PushEndEvent();
        }

        public void GameStarted(string callingMethodIdentifier, string idLocalProject, string osVersion, bool isTiny, bool debugDevice, string userLocale)
        {
            m_Buffer.PushStandardEventStart("gameStarted", 1);

            PushCommonParams(callingMethodIdentifier);

            m_Buffer.PushString("userLocale", userLocale);
            if (!String.IsNullOrEmpty(idLocalProject))
            {
                m_Buffer.PushString("idLocalProject", idLocalProject);
            }
            m_Buffer.PushString("osVersion", osVersion);
            m_Buffer.PushBool("isTiny", isTiny);
            m_Buffer.PushBool("debugDevice", debugDevice);

            m_Buffer.PushEndEvent();
        }

        public void GameEnded(string callingMethodIdentifier, SessionEndState quitState)
        {
            m_Buffer.PushStandardEventStart("gameEnded", 1);

            PushCommonParams(callingMethodIdentifier);

            m_Buffer.PushString("sessionEndState", quitState.ToString());

            m_Buffer.PushEndEvent();
        }

        public void ClientDevice(string callingMethodIdentifier)
        {
            m_Buffer.PushStandardEventStart("clientDevice", 1);

            PushCommonParams(callingMethodIdentifier);

            m_Buffer.PushString("cpuType", m_DeviceData.CpuType);
            m_Buffer.PushString("gpuType", m_DeviceData.GpuType);
            m_Buffer.PushInt64("cpuCores", m_DeviceData.CpuCores);
            m_Buffer.PushInt64("ramTotal", m_DeviceData.RamTotal);
            m_Buffer.PushInt64("screenWidth", m_DeviceData.ScreenWidth);
            m_Buffer.PushInt64("screenHeight", m_DeviceData.ScreenHeight);
            m_Buffer.PushInt64("screenResolution", (int)m_DeviceData.ScreenDpi);

            m_Buffer.PushEndEvent();
        }

        public void PushCommonParams(string callingMethodIdentifier)
        {
            m_Buffer.PushString("sdkMethod", callingMethodIdentifier);
            m_Buffer.PushString("clientVersion", m_CommonData.Version);
            m_Buffer.PushDouble("batteryLoad", m_CommonData.BatteryLevel);
            m_Buffer.PushString("platform", m_CommonData.Platform);

            if (!string.IsNullOrEmpty(m_CommonData.GameStoreId))
            {
                m_Buffer.PushString("gameStoreID", m_CommonData.GameStoreId);
            }

            if (!string.IsNullOrEmpty(m_CommonData.GameBundleId))
            {
                m_Buffer.PushString("gameBundleID", m_CommonData.GameBundleId);
            }

            if (!string.IsNullOrEmpty(m_CommonData.Idfv))
            {
                m_Buffer.PushString("idfv", m_CommonData.Idfv);
            }

            if (!string.IsNullOrEmpty(m_CommonData.BuildGUID))
            {
                m_Buffer.PushString("buildGUUID", m_CommonData.BuildGUID);
            }

            // TODO: It is not possible to set this externally, and the back-end will use GeoIP anyway?
            //if (!string.IsNullOrEmpty(m_UserCountry))
            //{
            //    // Schema: Optional, IsEnum
            //    m_Buffer.PushString(m_UserCountry, "userCountry");
            //}

            if (m_CommonData.HasVolume)
            {
                m_Buffer.PushDouble("deviceVolume", m_CommonData.Volume);
            }

            if (!string.IsNullOrEmpty(m_CommonData.ProjectId))
            {
                m_Buffer.PushString("projectID", m_CommonData.ProjectId);
            }
        }

        public void PushEvent(string callingMethodIdentifier, Event e)
        {
            e.Validate();

            if (e.StandardEvent)
            {
                m_Buffer.PushStandardEventStart(e.Name, e.EventVersion);
                PushCommonParams(callingMethodIdentifier);
            }
            else
            {
                m_Buffer.PushCustomEventStart(e.Name);
            }

            e.Serialize(m_Buffer);

            // Clear the event object now that we've serialised it, so it can be reused/object pooled/etc.
            e.Reset();

            m_Buffer.PushEndEvent();
        }

        public void PushEmptyEvent(string name)
        {
            m_Buffer.PushCustomEventStart(name);
            m_Buffer.PushEndEvent();
        }
    }
}
