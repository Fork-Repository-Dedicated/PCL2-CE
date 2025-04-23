using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public class ModSetup
    {

        /// <summary>
    /// 设置的更新号。
    /// </summary>
        public const int VersionSetup = 1;
        /// <summary>
    /// 设置列表。
    /// </summary>
        private readonly Dictionary<string, SetupEntry> SetupDict = new Dictionary<string, SetupEntry>() { { "Identify", new SetupEntry("", Source: SetupSource.Registry) }, { "WindowHeight", new SetupEntry(550) }, { "WindowWidth", new SetupEntry(900) }, { "HintDownloadThread", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintNotice", new SetupEntry(0, Source: SetupSource.Registry) }, { "HintDownload", new SetupEntry(0, Source: SetupSource.Registry) }, { "HintInstallBack", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintHide", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintHandInstall", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintBuy", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintClearRubbish", new SetupEntry(0, Source: SetupSource.Registry) }, { "HintUpdateMod", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintCustomCommand", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintCustomWarn", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintMoreAdvancedSetup", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintIndieSetup", new SetupEntry(false, Source: SetupSource.Registry) }, { "HintExportConfig", new SetupEntry(false, Source: SetupSource.Registry) }, { "SystemEula", new SetupEntry(false, Source: SetupSource.Registry) }, { "SystemCount", new SetupEntry(0, Source: SetupSource.Registry, Encoded: true) }, { "SystemLaunchCount", new SetupEntry(0, Source: SetupSource.Registry, Encoded: true) }, { "SystemLastVersionReg", new SetupEntry(0, Source: SetupSource.Registry, Encoded: true) }, { "SystemHighestSavedBetaVersionReg", new SetupEntry(0, Source: SetupSource.Registry, Encoded: true) }, { "SystemHighestBetaVersionReg", new SetupEntry(0, Source: SetupSource.Registry, Encoded: true) }, { "SystemHighestAlphaVersionReg", new SetupEntry(0, Source: SetupSource.Registry, Encoded: true) }, { "SystemSetupVersionReg", new SetupEntry(VersionSetup, Source: SetupSource.Registry) }, { "SystemSetupVersionIni", new SetupEntry(VersionSetup) }, { "SystemDebugMode", new SetupEntry(false, Source: SetupSource.Registry) }, { "SystemDebugAnim", new SetupEntry(9, Source: SetupSource.Registry) }, { "SystemDebugDelay", new SetupEntry(false, Source: SetupSource.Registry) }, { "SystemDebugSkipCopy", new SetupEntry(false, Source: SetupSource.Registry) }, { "SystemSystemCache", new SetupEntry("", Source: SetupSource.Registry) }, { "SystemSystemUpdate", new SetupEntry(0) }, { "SystemSystemUpdateBranch", new SetupEntry(0) }, { "SystemSystemServer", new SetupEntry(0) }, { "SystemSystemActivity", new SetupEntry(0) }, { "SystemHttpProxy", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "SystemDisableHardwareAcceleration", new SetupEntry(false, Source: SetupSource.Registry) }, { "CacheExportConfig", new SetupEntry("", Source: SetupSource.Registry) }, { "CacheSavedPageUrl", new SetupEntry("", Source: SetupSource.Registry) }, { "CacheSavedPageVersion", new SetupEntry("", Source: SetupSource.Registry) }, { "CacheMsOAuthRefresh", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsAccess", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsProfileJson", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsUuid", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsName", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsV2Migrated", new SetupEntry(false, Source: SetupSource.Registry) }, { "CacheMsV2OAuthRefresh", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsV2Access", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsV2ProfileJson", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsV2Uuid", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheMsV2Name", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheNideAccess", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheNideClient", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheNideUuid", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheNideName", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheNideUsername", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheNidePass", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheNideServer", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthAccess", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthClient", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthUuid", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthName", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthUsername", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthPass", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthServerServer", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthServerName", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthServerRegister", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthRefresh", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthIDToken", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheAuthAPIToken", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "CacheDownloadFolder", new SetupEntry("", Source: SetupSource.Registry) }, { "CacheJavaListVersion", new SetupEntry(0, Source: SetupSource.Registry) }, { "CacheAnnounceVersion", new SetupEntry(0, Source: SetupSource.Registry) }, { "CompFavorites", new SetupEntry("[]", Source: SetupSource.Registry) }, { "LoginRemember", new SetupEntry(true, Source: SetupSource.Registry, Encoded: true) }, { "LoginLegacyName", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "LoginMsJson", new SetupEntry("{}", Source: SetupSource.Registry, Encoded: true) }, { "LoginNideEmail", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "LoginNidePass", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "LoginAuthEmail", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "LoginAuthPass", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "LoginType", new SetupEntry(ModLaunch.McLoginType.Legacy, Source: SetupSource.Registry) }, { "LoginPageType", new SetupEntry(0) }, { "LaunchSkinID", new SetupEntry("", Source: SetupSource.Registry) }, { "LaunchSkinType", new SetupEntry(0, Source: SetupSource.Registry) }, { "LaunchSkinSlim", new SetupEntry(false, Source: SetupSource.Registry) }, { "LaunchVersionSelect", new SetupEntry("") }, { "LaunchFolderSelect", new SetupEntry("") }, { "LaunchFolders", new SetupEntry("", Source: SetupSource.Registry) }, { "LaunchArgumentTitle", new SetupEntry("") }, { "LaunchArgumentInfo", new SetupEntry("PCL") }, { "LaunchArgumentJavaSelect", new SetupEntry("", Source: SetupSource.Registry) }, { "LaunchArgumentJavaAll", new SetupEntry("[]", Source: SetupSource.Registry) }, { "LaunchArgumentIndie", new SetupEntry(0) }, { "LaunchArgumentIndieV2", new SetupEntry(4) }, { "LaunchArgumentVisible", new SetupEntry(5, Source: SetupSource.Registry) }, { "LaunchArgumentPriority", new SetupEntry(1, Source: SetupSource.Registry) }, { "LaunchArgumentWindowWidth", new SetupEntry(854) }, { "LaunchArgumentWindowHeight", new SetupEntry(480) }, { "LaunchArgumentWindowType", new SetupEntry(1) }, { "LaunchArgumentRam", new SetupEntry(false, Source: SetupSource.Registry) }, { "LaunchAdvanceJvm", new SetupEntry("-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true") }, { "LaunchArgumentJavaTraversal", new SetupEntry(false, Source: SetupSource.Registry) }, { "LaunchAdvanceGame", new SetupEntry("") }, { "LaunchAdvanceRun", new SetupEntry("") }, { "LaunchAdvanceRunWait", new SetupEntry(true) }, { "LaunchAdvanceDisableJLW", new SetupEntry(false) }, { "LaunchAdvanceGraphicCard", new SetupEntry(true, Source: SetupSource.Registry) }, { "LaunchRamType", new SetupEntry(0) }, { "LaunchRamCustom", new SetupEntry(15) }, { "LinkEula", new SetupEntry(false, Source: SetupSource.Registry) }, { "LinkName", new SetupEntry("", Source: SetupSource.Registry) }, { "LinkFirstTimeNetTest", new SetupEntry(true, Source: SetupSource.Registry) }, { "ToolHelpChinese", new SetupEntry(true, Source: SetupSource.Registry) }, { "ToolDownloadThread", new SetupEntry(63, Source: SetupSource.Registry) }, { "ToolDownloadSpeed", new SetupEntry(42, Source: SetupSource.Registry) }, { "ToolDownloadVersion", new SetupEntry(0, Source: SetupSource.Registry) }, { "ToolDownloadTranslate", new SetupEntry(0, Source: SetupSource.Registry) }, { "ToolDownloadTranslateV2", new SetupEntry(1, Source: SetupSource.Registry) }, { "ToolDownloadIgnoreQuilt", new SetupEntry(true, Source: SetupSource.Registry) }, { "ToolDownloadClipboard", new SetupEntry(false, Source: SetupSource.Registry) }, { "ToolDownloadCert", new SetupEntry(true, Source: SetupSource.Registry) }, { "ToolDownloadMod", new SetupEntry(1, Source: SetupSource.Registry) }, { "ToolModLocalNameStyle", new SetupEntry(0, Source: SetupSource.Registry) }, { "ToolUpdateAlpha", new SetupEntry(0, Source: SetupSource.Registry, Encoded: true) }, { "ToolUpdateRelease", new SetupEntry(false, Source: SetupSource.Registry) }, { "ToolUpdateSnapshot", new SetupEntry(false, Source: SetupSource.Registry) }, { "ToolUpdateReleaseLast", new SetupEntry("", Source: SetupSource.Registry) }, { "ToolUpdateSnapshotLast", new SetupEntry("", Source: SetupSource.Registry) }, { "UiLauncherTransparent", new SetupEntry(600) }, { "UiLauncherHue", new SetupEntry(180) }, { "UiLauncherSat", new SetupEntry(80) }, { "UiLauncherDelta", new SetupEntry(90) }, { "UiLauncherLight", new SetupEntry(20) }, { "UiLauncherTheme", new SetupEntry(0) }, { "UiLauncherThemeGold", new SetupEntry("", Source: SetupSource.Registry, Encoded: true) }, { "UiLauncherThemeHide", new SetupEntry("0|1|2|3|4", Source: SetupSource.Registry, Encoded: true) }, { "UiLauncherThemeHide2", new SetupEntry("0|1|2|3|4", Source: SetupSource.Registry, Encoded: true) }, { "UiLauncherLogo", new SetupEntry(true) }, { "UiLauncherEmail", new SetupEntry(false) }, { "UiLauncherCEHint", new SetupEntry(true, Source: SetupSource.Registry) }, { "UiBackgroundColorful", new SetupEntry(true) }, { "UiBackgroundOpacity", new SetupEntry(1000) }, { "UiBackgroundBlur", new SetupEntry(0) }, { "UiBackgroundSuit", new SetupEntry(0) }, { "UiCustomType", new SetupEntry(0) }, { "UiCustomPreset", new SetupEntry(0) }, { "UiCustomNet", new SetupEntry("") }, { "UiDarkMode", new SetupEntry(2, Source: SetupSource.Registry) }, { "UiLogoType", new SetupEntry(1) }, { "UiLogoText", new SetupEntry("") }, { "UiLogoLeft", new SetupEntry(false) }, { "UiMusicVolume", new SetupEntry(500) }, { "UiMusicStop", new SetupEntry(false) }, { "UiMusicStart", new SetupEntry(false) }, { "UiMusicRandom", new SetupEntry(true) }, { "UiMusicSMTC", new SetupEntry(true) }, { "UiMusicAuto", new SetupEntry(true) }, { "UiHiddenPageDownload", new SetupEntry(false) }, { "UiHiddenPageLink", new SetupEntry(false) }, { "UiHiddenPageSetup", new SetupEntry(false) }, { "UiHiddenPageOther", new SetupEntry(false) }, { "UiHiddenFunctionSelect", new SetupEntry(false) }, { "UiHiddenFunctionModUpdate", new SetupEntry(false) }, { "UiHiddenFunctionHidden", new SetupEntry(false) }, { "UiHiddenSetupLaunch", new SetupEntry(false) }, { "UiHiddenSetupUi", new SetupEntry(false) }, { "UiHiddenSetupLink", new SetupEntry(false) }, { "UiHiddenSetupSystem", new SetupEntry(false) }, { "UiHiddenOtherHelp", new SetupEntry(false) }, { "UiHiddenOtherFeedback", new SetupEntry(false) }, { "UiHiddenOtherVote", new SetupEntry(false) }, { "UiHiddenOtherAbout", new SetupEntry(false) }, { "UiHiddenOtherTest", new SetupEntry(false) }, { "UiAniFPS", new SetupEntry(59, Source: SetupSource.Registry) }, { "UiFont", new SetupEntry("") }, { "VersionAdvanceJvm", new SetupEntry("", Source: SetupSource.Version) }, { "VersionAdvanceGame", new SetupEntry("", Source: SetupSource.Version) }, { "VersionAdvanceAssets", new SetupEntry(0, Source: SetupSource.Version) }, { "VersionAdvanceAssetsV2", new SetupEntry(false, Source: SetupSource.Version) }, { "VersionAdvanceJava", new SetupEntry(false, Source: SetupSource.Version) }, { "VersionAdvanceDisableJlw", new SetupEntry(false, Source: SetupSource.Version) }, { "VersionAdvanceRun", new SetupEntry("", Source: SetupSource.Version) }, { "VersionAdvanceRunWait", new SetupEntry(true, Source: SetupSource.Version) }, { "VersionAdvanceDisableJLW", new SetupEntry(false, Source: SetupSource.Version) }, { "VersionRamType", new SetupEntry(2, Source: SetupSource.Version) }, { "VersionRamCustom", new SetupEntry(15, Source: SetupSource.Version) }, { "VersionRamOptimize", new SetupEntry(0, Source: SetupSource.Version) }, { "VersionArgumentTitle", new SetupEntry("", Source: SetupSource.Version) }, { "VersionArgumentInfo", new SetupEntry("", Source: SetupSource.Version) }, { "VersionArgumentIndie", new SetupEntry(-1, Source: SetupSource.Version) }, { "VersionArgumentIndieV2", new SetupEntry(false, Source: SetupSource.Version) }, { "VersionArgumentJavaSelect", new SetupEntry("使用全局设置", Source: SetupSource.Version) }, { "VersionServerEnter", new SetupEntry("", Source: SetupSource.Version) }, { "VersionServerLogin", new SetupEntry(0, Source: SetupSource.Version) }, { "VersionServerNide", new SetupEntry("", Source: SetupSource.Version) }, { "VersionServerAuthRegister", new SetupEntry("", Source: SetupSource.Version) }, { "VersionServerAuthName", new SetupEntry("", Source: SetupSource.Version) }, { "VersionServerAuthServer", new SetupEntry("", Source: SetupSource.Version) } }; // {UserName: OAuthToken, ...}
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        // 避免与 PCL1 设置冲突（UiLauncherOpacity）

        #region Register 存储

        private LocalJsonFileConfig LocalRegisterData = new LocalJsonFileConfig(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $@"\.{ModSecret.RegFolder}\Config.json");

        public class LocalJsonFileConfig
        {
            private readonly JObject _ConfigData;
            private string _ConfigFilePath;

            public LocalJsonFileConfig(string JsonFilePath)
            {
                _ConfigFilePath = JsonFilePath;
                if (File.Exists(JsonFilePath))
                {
                    try
                    {
                        string JsonText = ModBase.ReadFile(JsonFilePath);
                        _ConfigData = JObject.Parse(JsonText);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取配置项数据失败", ModBase.LogLevel.Feedback);
                    }
                }
                else
                {
                    _ConfigData = new JObject();
                }
            }

            private void Save()
            {
                ModBase.WriteFile(_ConfigFilePath, _ConfigData.ToString());
            }

            private readonly object _SetLock = new object();
            public void Set(string key, string value)
            {
                lock (_SetLock)
                {
                    _ConfigData[key] = value;
                    Save();
                }
            }

            public string Get(string Key)
            {
                if (_ConfigData.ContainsKey(Key))
                {
                    return (string)_ConfigData[Key];
                }
                else
                {
                    return null;
                }
            }

            public void Remove(string key)
            {
                _ConfigData.Remove(key);
                Save();
            }

            public bool Contains(string key)
            {
                return _ConfigData.ContainsKey(key);
            }

            public JObject RawJObject
            {
                get
                {
                    return _ConfigData;
                }
            }
        }
        #endregion

        #region 基础

        private enum SetupSource
        {
            Normal,
            Registry,
            Version
        }
        private class SetupEntry
        {

            public bool Encoded;
            public object DefaultValue;
            public object DefaultValueEncoded;
            public object Value;
            public SetupSource Source;

            /// <summary>
        /// 加载状态：0/未读取  1/已读取未处理  2/已处理
        /// 我也不知道当年写这坨的时候为啥没用 Enum……
        /// </summary>
            public byte State = 0;
            public Type Type;

            public SetupEntry(object Value, SetupSource Source = SetupSource.Normal, bool Encoded = false)
            {
                try
                {
                    DefaultValue = Value;
                    this.Encoded = Conversions.ToBoolean(Encoded);
                    this.Value = Value;
                    this.Source = (SetupSource)Conversions.ToInteger(Source);
                    Type = (Value ?? new object()).GetType();
                    DefaultValueEncoded = Conversions.ToBoolean(Encoded) ? ModSecret.SecretEncrypt(Conversions.ToString(Value)) : Value;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "初始化 SetupEntry 失败", ModBase.LogLevel.Feedback); // #5095 的 fallback
                }
            }

        }

        /// <summary>
    /// 改变某个设置项的值。
    /// </summary>
        public void Set(string Key, object Value, bool ForceReload = false, ModMinecraft.McVersion Version = null)
        {
            Set(Key, Value, SetupDict[Key], ForceReload, Version);
        }
        private void Set(string Key, object Value, SetupEntry E, bool ForceReload, ModMinecraft.McVersion Version)
        {
            try
            {

                Value = Conversion.CTypeDynamic(Value, E.Type);
                if (E.State == 2)
                {
                    // 如果已应用，且值相同，则无需再次更改
                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(E.Value, Value, false)) && !ForceReload)
                        return;
                }
                // 如果未应用，则直接更改并应用
                else if (E.Source != SetupSource.Version)
                    E.State = 2;
                // 设置新值
                E.Value = Value;
                // 写入值
                if (E.Encoded)
                {
                    try
                    {
                        if (Value is null)
                            Value = "";
                        Value = ModSecret.SecretEncrypt(Conversions.ToString(Value));
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "加密设置失败：" + Key, ModBase.LogLevel.Developer);
                    }
                }
                switch (E.Source)
                {
                    case SetupSource.Normal:
                        {
                            ModBase.WriteIni("Setup", Key, Conversions.ToString(Value));
                            break;
                        }
                    case SetupSource.Registry:
                        {
                            LocalRegisterData.Set(Key, Conversions.ToString(Value));
                            break;
                        }
                    case SetupSource.Version:
                        {
                            if (Version is null)
                                throw new Exception($"更改版本设置 {Key} 时未提供目标版本");
                            ModBase.WriteIni(Version.Path + @"PCL\Setup.ini", Key, Conversions.ToString(Value));
                            break;
                        }
                }
                // 应用
                // 例如 VersionServerLogin 要求在设置之后再引发事件
                var Method = typeof(ModSetup).GetMethod(Key);
                if (Method is not null)
                    Method.Invoke(this, new[] { Value });
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject("设置设置项时出错（" + Key + ", ", Value), "）")), ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
    /// 应用某个设置项的值。
    /// </summary>
        public object Load(string Key, bool ForceReload = false, ModMinecraft.McVersion Version = null)
        {
            return Load(Key, SetupDict[Key], ForceReload, Version);
        }
        private object Load(string Key, SetupEntry E, bool ForceReload, ModMinecraft.McVersion Version)
        {
            // 如果已经应用过，则什么也不干
            if (E.State == 2 && !ForceReload)
                return E.Value;
            // 读取，应用并设置状态
            Read(Key, ref E, Version);
            if (E.Source != SetupSource.Version)
                E.State = 2;
            var Method = typeof(ModSetup).GetMethod(Key);
            if (Method is not null)
                Method.Invoke(this, new[] { E.Value });
            return E.Value;
        }

        /// <summary>
    /// 获取某个设置项的值。
    /// </summary>
        public object Get(string Key, ModMinecraft.McVersion Version = null)
        {
            if (!SetupDict.ContainsKey(Key))
                throw new KeyNotFoundException("未找到设置项：" + Key) { Source = Key };
            return Get(Key, SetupDict[Key], Version);
        }
        private object Get(string Key, SetupEntry E, ModMinecraft.McVersion Version)
        {
            // 获取强制值
            string Force = ForceValue(Key);
            if (Force is not null)
            {
                E.Value = Conversion.CTypeDynamic(Force, E.Type);
                E.State = 1;
            }
            // 如果尚未读取过，则读取
            if (E.State == 0)
            {
                Read(Key, ref E, Version);
                if (E.Source != SetupSource.Version)
                    E.State = 1;
            }
            // 返回现在的值
            return E.Value;
        }

        /// <summary>
    /// 初始化某个设置项的值。
    /// </summary>
        public void Reset(string Key, bool ForceReload = false, ModMinecraft.McVersion Version = null)
        {
            var E = SetupDict[Key];
            Set(Key, E.DefaultValue, E, ForceReload, Version);
            switch (SetupDict[Key].Source)
            {
                case SetupSource.Normal:
                    {
                        ModBase.DeleteIniKey("Setup", Key);
                        break;
                    }
                case SetupSource.Registry:
                    {
                        ModBase.DeleteReg(Key);
                        LocalRegisterData.Remove(Key); // SetupSource.Version
                        break;
                    }

                default:
                    {
                        if (Version is null)
                            throw new Exception($"重置版本设置 {Key} 时未提供目标版本");
                        ModBase.DeleteIniKey(Version.Path + @"PCL\Setup.ini", Key);
                        break;
                    }
            }
        }
        /// <summary>
    /// 获取某个设置项的默认值。
    /// </summary>
        public string GetDefault(string Key)
        {
            return Conversions.ToString(SetupDict[Key].DefaultValue);
        }
        /// <summary>
    /// 某个设置项是否从未被设置过。
    /// </summary>
        public bool IsUnset(string Key, ModMinecraft.McVersion Version = null)
        {
            switch (SetupDict[Key].Source)
            {
                case SetupSource.Normal:
                    {
                        return !ModBase.HasIniKey("Setup", Key);
                    }
                case SetupSource.Registry:
                    {
                        return !ModBase.HasReg(Key) && !LocalRegisterData.Contains(Key); // SetupSource.Version
                    }

                default:
                    {
                        if (Version is null)
                            throw new Exception($"判断版本设置 {Key} 是否存在时未提供目标版本");
                        return !ModBase.HasIniKey(Version.Path + @"PCL\Setup.ini", Key);
                    }
            }
        }

        /// <summary>
    /// 读取设置。
    /// </summary>
        private void Read(string Key, ref SetupEntry E, ModMinecraft.McVersion Version)
        {
            try
            {
                if (!(E.State == 0))
                    return;
                string SourceValue = null; // 先用 String 储存，避免类型转换
                switch (E.Source)
                {
                    case SetupSource.Normal:
                        {
                            SourceValue = ModBase.ReadIni("Setup", Key, Conversions.ToString(E.DefaultValueEncoded));
                            break;
                        }
                    case SetupSource.Registry:
                        {
                            string OldSourceData = ModBase.ReadReg(Key);
                            if (!string.IsNullOrWhiteSpace(OldSourceData))
                            {
                                if (LocalRegisterData.Contains(Key)) // 如果本地配置文件中已经存在该项，则不覆盖
                                {
                                    OldSourceData = LocalRegisterData.Get(Key);
                                }
                                else
                                {
                                    if (E.Encoded)
                                        OldSourceData = ModSecret.SecretEncrypt(ModSecret.SecretDecrptyOld(OldSourceData));
                                    LocalRegisterData.Set(Key, OldSourceData);
                                    ModBase.DeleteReg(Key);
                                    SourceValue = OldSourceData;
                                }
                            }
                            else
                            {
                                SourceValue = LocalRegisterData.Get(Key);
                            }
                            if (string.IsNullOrEmpty(SourceValue))
                            {
                                SourceValue = Conversions.ToString(E.DefaultValueEncoded);
                            }

                            break;
                        }
                    case SetupSource.Version:
                        {
                            if (Version is null)
                            {
                                throw new Exception("读取版本设置 " + Key + " 时未提供目标版本");
                            }
                            else
                            {
                                SourceValue = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", Key, Conversions.ToString(E.DefaultValueEncoded));
                            }

                            break;
                        }
                }
                if (E.Encoded)
                {
                    if (SourceValue.Equals(E.DefaultValueEncoded))
                    {
                        SourceValue = Conversions.ToString(E.DefaultValue);
                    }
                    else
                    {
                        try
                        {
                            SourceValue = ModSecret.SecretDecrypt(SourceValue);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "解密设置失败：" + Key, ModBase.LogLevel.Developer);
                            SourceValue = Conversions.ToString(E.DefaultValue);
                            ModBase.Setup.Set(Key, E.DefaultValue, true);
                        }
                    }
                }
                E.Value = Conversion.CTypeDynamic(SourceValue, E.Type);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取设置失败：" + Key, ModBase.LogLevel.Hint);
                E.Value = Conversion.CTypeDynamic(E.DefaultValue, E.Type);
            }
        }

        // 对部分设置强制赋值
        private string ForceValue(string Key)
        {
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If BETA Then
            *//* TODO ERROR: Skipped DisabledTextTrivia
                    If Key = "UiLauncherTheme" Then Return "0"
            *//* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
            if (Key == "UiHiddenPageLink")
                return Conversions.ToString(false);
            if (Key == "UiHiddenSetupLink")
                return Conversions.ToString(false);
            return null;
        }

        #endregion

        #region Launch

        // 切换选择
        public void LaunchVersionSelect(string Value)
        {
            ModBase.Log("[Setup] 当前选择的 Minecraft 版本：" + Value);
            ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "Version", ModMinecraft.McVersionCurrent == null ? "" : ModMinecraft.McVersionCurrent.Name);
        }
        public void LaunchFolderSelect(string Value)
        {
            ModBase.Log("[Setup] 当前选择的 Minecraft 文件夹：" + Value.ToString().Replace("$", ModBase.Path));
            ModMinecraft.PathMcFolder = Value.ToString().Replace("$", ModBase.Path);
        }

        // 游戏内存
        public void LaunchRamType(int Type)
        {
            if (ModMain.FrmSetupLaunch is null)
                return;
            ModMain.FrmSetupLaunch.RamType(Type);
        }

        // 离线皮肤
        public void LaunchSkinType(int Value)
        {
            ModBase.RunInUi(() =>
                {
                    if (!(ModMain.FrmSetupLaunch == null))
                    {
                        switch (Value)
                        {
                            case 0:
                            case 1:
                            case 2: // 默认
                                {
                                    ModMain.FrmSetupLaunch.PanSkinID.Visibility = Visibility.Collapsed;
                                    ModMain.FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Collapsed;
                                    break;
                                }
                            case 3: // 正版
                                {
                                    ModMain.FrmSetupLaunch.PanSkinID.Visibility = Visibility.Visible;
                                    ModMain.FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Collapsed;
                                    break;
                                }
                            case 4: // 自定义
                                {
                                    ModMain.FrmSetupLaunch.PanSkinID.Visibility = Visibility.Collapsed;
                                    ModMain.FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Visible;
                                    break;
                                }
                        }
                        ModMain.FrmSetupLaunch.CardSkin.TriggerForceResize();
                    }
                    PageLaunchLeft.SkinLegacy.Start();
                });
        }
        public void LaunchSkinID(string Value)
        {
            PageLaunchLeft.SkinLegacy.Start();
        }

        #endregion

        #region Tool

        public void ToolDownloadThread(int Value)
        {
            ModNet.NetTaskThreadLimit = Value + 1;
        }
        public void ToolDownloadCert(bool Value)
        {
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback( new Func<object, System.Security.Cryptography.X509Certificates.X509Certificate, System.Security.Cryptography.X509Certificates.X509Chain, System.Net.Security.SslPolicyErrors, bool>((Sender, Certificate, Chain, Failure) =>
        {
            HttpWebRequest Request = Sender as HttpWebRequest;
            if (Failure == System.Net.Security.SslPolicyErrors.None)
                return true; // 已通过验证
            // 基于 #3018 和 #5879，只在访问正版登录 API 时跳过证书验证
            ModBase.Log($"[System] 未通过 SSL 证书验证（{Failure}），提供的证书为 {Certificate?.Subject}，URL：{Request?.Address}", ModBase.LogLevel.Debug);
            if (Request is null)
            {
                return !Value;
            }
            else if (Request.Address.Host.Contains("xboxlive") || Request.Address.Host.Contains("minecraftservices"))
            {
                return !Value; // 根据设置决定是否忽略错误
            }
            else
            {
                return false;
            }
        }));
        }
        public void ToolDownloadSpeed(int Value)
        {
            if (Value <= 14)
            {
                ModNet.NetTaskSpeedLimitHigh = (long)Math.Round((Value + 1) * 0.1d * 1024d * 1024d);
            }
            else if (Value <= 31)
            {
                ModNet.NetTaskSpeedLimitHigh = (long)Math.Round((Value - 11) * 0.5d * 1024d * 1024d);
            }
            else if (Value <= 41)
            {
                ModNet.NetTaskSpeedLimitHigh = (Value - 21) * 1024 * 1024L;
            }
            else
            {
                ModNet.NetTaskSpeedLimitHigh = -1;
            }
        }

        #endregion

        #region UI

        // 启动器
        public void UiLauncherTransparent(int Value)
        {
            ModMain.FrmMain.Opacity = Value / 1000d + 0.4d;
        }
        /* TODO ERROR: Skipped IfDirectiveTrivia
        #If Not BETA Then
        */
        public void UiLauncherTheme(int Value)
        {
            ModSecret.ThemeRefresh(Value);
        }
        /* TODO ERROR: Skipped EndIfDirectiveTrivia
        #End If
        */
        public void UiBackgroundColorful(bool Value)
        {
            ModSecret.ThemeRefresh();
        }

        // 背景图片
        public void UiBackgroundOpacity(int Value)
        {
            ModMain.FrmMain.ImgBack.Opacity = Value / 1000d;
        }
        public void UiBackgroundBlur(int Value)
        {
            if (Value == 0)
            {
                ModMain.FrmMain.ImgBack.Effect = (System.Windows.Media.Effects.Effect)null;
            }
            else
            {
                ModMain.FrmMain.ImgBack.Effect = new System.Windows.Media.Effects.BlurEffect() { Radius = Value + 1 };
            }
            ModMain.FrmMain.ImgBack.Margin = new Thickness(-(Value + 1) / 1.8d);
        }
        public void UiBackgroundSuit(int Value)
        {
            if (ModMain.FrmMain.ImgBack.Background == null)
                return;
            double Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
            double Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
            if (Value == 0)
            {
                // 智能：当图片较小时平铺，较大时适应
                if (Width < ModMain.FrmMain.PanMain.ActualWidth / 2d && Height < ModMain.FrmMain.PanMain.ActualHeight / 2d)
                {
                    Value = 4; // 平铺
                }
                else
                {
                    Value = 2;
                } // 适应
            } ((ImageBrush)ModMain.FrmMain.ImgBack.Background).TileMode = TileMode.None;
            ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Viewport = new Rect(0d, 0d, 1d, 1d);
            ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            switch (Value)
            {
                case 1: // 居中
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Center;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Center;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                        ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                        ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                        break;
                    }
                case 2: // 适应
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.UniformToFill;
                        ModMain.FrmMain.ImgBack.Width = double.NaN;
                        ModMain.FrmMain.ImgBack.Height = double.NaN;
                        break;
                    }
                case 3: // 拉伸
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.Fill;
                        ModMain.FrmMain.ImgBack.Width = double.NaN;
                        ModMain.FrmMain.ImgBack.Height = double.NaN;
                        break;
                    }
                case 4: // 平铺
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).TileMode = TileMode.Tile;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Viewport = new Rect(0d, 0d, ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width, ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height);
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ViewportUnits = BrushMappingMode.Absolute;
                        ModMain.FrmMain.ImgBack.Width = double.NaN;
                        ModMain.FrmMain.ImgBack.Height = double.NaN;
                        break;
                    }
                case 5: // 左上
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                        ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                        ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                        break;
                    }
                case 6: // 右上
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                        ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                        ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                        break;
                    }
                case 7: // 左下
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                        ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                        ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                        break;
                    }
                case 8: // 右下
                    {
                        ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right;
                        ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom;
                        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                        ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                        ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                        break;
                    }
            }
        }

        // 主页
        public void UiCustomType(int Value)
        {
            if (ModMain.FrmSetupUI is null)
                return;
            switch (Value)
            {
                case 0: // 无
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed;
                        break;
                    }
                case 1: // 本地
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Conversions.ToBoolean(ModBase.Setup.Get("HintCustomWarn")) ? Visibility.Collapsed : Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{Constants.vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。";
                        ModMain.FrmSetupUI.HintCustom.EventType = "";
                        ModMain.FrmSetupUI.HintCustom.EventData = "";
                        break;
                    }
                case 2: // 联网
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Conversions.ToBoolean(ModBase.Setup.Get("HintCustomWarn")) ? Visibility.Collapsed : Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{Constants.vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！";
                        ModMain.FrmSetupUI.HintCustom.EventType = "打开网页";
                        ModMain.FrmSetupUI.HintCustom.EventData = "https://github.com/Hex-Dragon/PCL2/discussions/2528";
                        break;
                    }
                case 3: // 预设
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed;
                        break;
                    }
            }
            ModMain.FrmSetupUI.CardCustom.TriggerForceResize();
        }
        // 颜色模式
        public void UiDarkMode(int Value)
        {
            if (Value == 0)
            {
                ModSecret.IsDarkMode = false;
            }
            else if (Value == 1)
            {
                ModSecret.IsDarkMode = true;
            }
            else
            {
                ModSecret.IsDarkMode = ModBase.IsSystemInDarkMode();
            }
            ModSecret.ThemeRefresh();
        }
        // 顶部栏
        public void UiLogoType(int Value)
        {
            switch (Value)
            {
                case 0: // 无
                    {
                        ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.CELogo.Visibility = Visibility.Collapsed;
                        if (!(ModMain.FrmSetupUI == null))
                        {
                            ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Visible;
                            ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                            ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                        }

                        break;
                    }
                case 1: // 默认
                    {
                        ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Visible;
                        ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.CELogo.Visibility = Visibility.Visible;
                        if (!(ModMain.FrmSetupUI == null))
                        {
                            ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                            ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                            ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                        }

                        break;
                    }
                case 2: // 文本
                    {
                        ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Visible;
                        ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.CELogo.Visibility = Visibility.Visible;
                        if (!(ModMain.FrmSetupUI == null))
                        {
                            ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                            ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Visible;
                            ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                        }
                        ModBase.Setup.Load("UiLogoText", true);
                        break;
                    }
                case 3: // 图片
                    {
                        ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed;
                        ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Visible;
                        ModMain.FrmMain.CELogo.Visibility = Visibility.Visible;
                        if (!(ModMain.FrmSetupUI == null))
                        {
                            ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                            ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                            ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Visible;
                        }
                        try
                        {
                            ModMain.FrmMain.ImageTitleLogo.Source = ModBase.Path + @"PCL\Logo.png";
                        }
                        catch (Exception ex)
                        {
                            ModMain.FrmMain.ImageTitleLogo.Source = (string)null;
                            ModBase.Log(ex, "显示标题栏图片失败", ModBase.LogLevel.Msgbox);
                        }

                        break;
                    }
            }
            ModBase.Setup.Load("UiLogoLeft", true);
            if (!(ModMain.FrmSetupUI == null))
                ModMain.FrmSetupUI.CardLogo.TriggerForceResize();
        }
        public void UiLogoText(string Value)
        {
            ModMain.FrmMain.LabTitleLogo.Text = Value;
        }
        public void UiLogoLeft(bool Value)
        {
            ModMain.FrmMain.PanTitleMain.ColumnDefinitions[0].Width = new GridLength(Value && Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("UiLogoType"), 0, false) ? 0 : 1, GridUnitType.Star);
        }

        // 功能隐藏
        public void UiHiddenPageLink(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenPageDownload(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenPageSetup(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenPageOther(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenFunctionSelect(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenFunctionModUpdate(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenFunctionHidden(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenSetupLaunch(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenSetupUi(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenSetupLink(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenSetupSystem(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenOtherHelp(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenOtherFeedback(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenOtherVote(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenOtherAbout(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }
        public void UiHiddenOtherTest(bool Value)
        {
            PageSetupUI.HiddenRefresh();
        }


        #endregion

        #region System

        // 调试选项
        public void SystemDebugMode(bool Value)
        {
            ModBase.ModeDebug = Value;
        }
        public void SystemDebugAnim(int Value)
        {
            ModAnimation.AniSpeed = Value >= 30 ? 200d : ModBase.MathClamp(Value * 0.1d + 0.1d, 0.1d, 3d);
        }

        #endregion

        #region Version

        // 游戏内存
        public void VersionRamType(int Type)
        {
            if (ModMain.FrmVersionSetup is null)
                return;
            ModMain.FrmVersionSetup.RamType(Type);
        }

        // 服务器
        public void VersionServerLogin(int Type)
        {
            if (ModMain.FrmVersionSetup is null)
                return;
            // 为第三方登录清空缓存以更新描述
            ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "VersionCache", "");
            if (PageVersionLeft.Version is null)
                return;
            PageVersionLeft.Version = new ModMinecraft.McVersion(PageVersionLeft.Version.Name).Load();
            ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
        }

        #endregion

    }
}