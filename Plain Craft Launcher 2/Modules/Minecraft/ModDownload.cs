using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public static class ModDownload
    {

        #region DlClient* | Minecraft 客户端

        /// <summary>
    /// 返回某 Minecraft 版本对应的原版主 Jar 文件的下载信息，要求对应依赖版本已存在。
    /// 失败则抛出异常，不需要下载则返回 Nothing。
    /// </summary>
        public static ModNet.NetFile DlClientJarGet(ModMinecraft.McVersion Version, bool ReturnNothingOnFileUseable)
        {
            // 获取底层继承版本
            try
            {
                while (!string.IsNullOrEmpty(Version.InheritVersion))
                    Version = new ModMinecraft.McVersion(Version.InheritVersion);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取底层继承版本失败");
            }
            // 检查 Json 是否标准
            if (Version.JsonObject["downloads"] is null || Version.JsonObject["downloads"]["client"] is null || Version.JsonObject["downloads"]["client"]["url"] is null)
            {
                throw new Exception("底层版本 " + Version.Name + " 中无 Jar 文件下载信息");
            }
            // 检查文件
            var Checker = new ModBase.FileChecker(MinSize: 1024L, ActualSize: (long)(Version.JsonObject["downloads"]["client"]["size"] ?? -1), Hash: (string)Version.JsonObject["downloads"]["client"]["sha1"]);
            if (ReturnNothingOnFileUseable && Checker.Check(Version.Path + Version.Name + ".jar") is null)
                return null; // 通过校验
                             // 返回下载信息
            string JarUrl = (string)Version.JsonObject["downloads"]["client"]["url"];
            return new ModNet.NetFile(DlSourceLauncherOrMetaGet(JarUrl), Version.Path + Version.Name + ".jar", Checker);
        }

        /// <summary>
    /// 返回某 Minecraft 版本对应的原版主 AssetIndex 文件的下载信息，要求对应依赖版本已存在。
    /// 若未找到，则会返回 Legacy 资源文件或 Nothing。
    /// </summary>
        public static ModNet.NetFile DlClientAssetIndexGet(ModMinecraft.McVersion Version)
        {
            // 获取底层继承版本
            while (!string.IsNullOrEmpty(Version.InheritVersion))
                Version = new ModMinecraft.McVersion(Version.InheritVersion);
            // 获取信息
            var IndexInfo = ModMinecraft.McAssetsGetIndex(Version, true, true);
            string IndexAddress = ModMinecraft.PathMcFolder + @"assets\indexes\" + IndexInfo["id"].ToString() + ".json";
            ModBase.Log("[Download] 版本 " + Version.Name + " 对应的资源文件索引为 " + IndexInfo["id"].ToString());
            string IndexUrl = (string)(IndexInfo["url"] ?? "");
            if (string.IsNullOrEmpty(IndexUrl))
            {
                return null;
            }
            else
            {
                return new ModNet.NetFile(DlSourceLauncherOrMetaGet(IndexUrl), IndexAddress, new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true));
            }
        }

        /// <summary>
    /// 构造补全某 Minecraft 版本的所有文件的加载器列表。失败会抛出异常。
    /// </summary>
        public static List<ModLoader.LoaderBase> DlClientFix(ModMinecraft.McVersion Version, bool CheckAssetsHash, AssetsIndexExistsBehaviour AssetsIndexBehaviour)
        {
            var Loaders = new List<ModLoader.LoaderBase>();

            #region 下载支持库文件
            if (Conversions.ToBoolean(ModMinecraft.ShouldIgnoreFileCheck(Version)))
            {
                ModBase.Log("[Download] 已跳过所有 Libraries 检查");
            }
            else
            {
                var LoadersLib = new List<ModLoader.LoaderBase>() { new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析缺失支持库文件", (Task) => Task.Output = ModMinecraft.McLibFix(Version)) { ProgressWeight = 1d }, new ModNet.LoaderDownload("下载支持库文件", new List<ModNet.NetFile>()) { ProgressWeight = 15d } };
                // 构造加载器
                Loaders.Add(new ModLoader.LoaderCombo<string>("下载支持库文件（主加载器）", LoadersLib) { Block = false, Show = false, ProgressWeight = 16d });
            }
            #endregion

            #region 下载资源文件
            if (Conversions.ToBoolean(ModMinecraft.ShouldIgnoreFileCheck(Version)))
            {
                ModBase.Log("[Download] 已跳过所有 Assets 检查");
            }
            else
            {
                var LoadersAssets = new List<ModLoader.LoaderBase>();
                // 获取资源文件索引地址
                LoadersAssets.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析资源文件索引地址", (Task) => { try { var IndexFile = DlClientAssetIndexGet(Version); var IndexFileInfo = new FileInfo(IndexFile.LocalPath); if (AssetsIndexBehaviour != AssetsIndexExistsBehaviour.AlwaysDownload && IndexFile.Check.Check(IndexFile.LocalPath) is null) { Task.Output = new List<ModNet.NetFile>(); } else { Task.Output = new List<ModNet.NetFile>() { IndexFile }; } } catch (Exception ex) { throw new Exception("分析资源文件索引地址失败", ex); } }) { ProgressWeight = 0.5d, Show = false });
                // 下载资源文件索引
                LoadersAssets.Add(new ModNet.LoaderDownload("下载资源文件索引", new List<ModNet.NetFile>()) { ProgressWeight = 2d });
                // 要求独立更新索引
                if (AssetsIndexBehaviour == AssetsIndexExistsBehaviour.DownloadInBackground)
                {
                    var LoadersAssetsUpdate = new List<ModLoader.LoaderBase>();
                    string TempAddress = null;
                    string RealAddress = null;
                    LoadersAssetsUpdate.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("后台分析资源文件索引地址", (Task) =>
        {
            var BackAssetsFile = DlClientAssetIndexGet(Version);
            RealAddress = BackAssetsFile.LocalPath;
            TempAddress = ModBase.PathTemp + @"Cache\" + BackAssetsFile.LocalName;
            BackAssetsFile.LocalPath = TempAddress;
            Task.Output = new List<ModNet.NetFile>() { BackAssetsFile };
            // 检查是否需要更新：每天只更新一次
            if (File.Exists(RealAddress) && Math.Abs((File.GetLastWriteTime(RealAddress).Date - DateTime.Now.Date).TotalDays) < 1d)
            {
                ModBase.Log("[Download] 无需更新资源文件索引，取消");
                Task.Abort();
            }
        }));
                    LoadersAssetsUpdate.Add(new ModNet.LoaderDownload("后台下载资源文件索引", new List<ModNet.NetFile>()));
                    LoadersAssetsUpdate.Add(new ModLoader.LoaderTask<List<ModNet.NetFile>, string>("后台复制资源文件索引", (Task) =>
        {
            ModBase.CopyFile(TempAddress, RealAddress);
            ModLaunch.McLaunchLog("后台更新资源文件索引成功：" + TempAddress);
        }));
                    var Updater = new ModLoader.LoaderCombo<string>("后台更新资源文件索引", LoadersAssetsUpdate);
                    ModBase.Log("[Download] 开始后台检查资源文件索引");
                    Updater.Start();
                }
                // 获取资源文件地址
                LoadersAssets.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析缺失资源文件", (Task) =>
        {
            ModLoader.LoaderBase argProgressFeed = Task;
            Task.Output = ModMinecraft.McAssetsFixList(Version, CheckAssetsHash, ref argProgressFeed);
            Task = (ModLoader.LoaderTask<string, List<ModNet.NetFile>>)argProgressFeed;
        })
                {
                    ProgressWeight = 3d
                });
                // 下载资源文件
                LoadersAssets.Add(new ModNet.LoaderDownload("下载资源文件", new List<ModNet.NetFile>()) { ProgressWeight = 25d });
                // 构造加载器
                Loaders.Add(new ModLoader.LoaderCombo<string>("下载资源文件（主加载器）", LoadersAssets) { Block = false, Show = false, ProgressWeight = 30.5d });
            }
            #endregion

            return Loaders;
        }
        public enum AssetsIndexExistsBehaviour
        {
            /// <summary>
        /// 如果文件存在，则不进行下载。
        /// </summary>
            DontDownload,
            /// <summary>
        /// 如果文件存在，则启动新的下载加载器进行独立的更新。
        /// </summary>
            DownloadInBackground,
            /// <summary>
        /// 如果文件存在，也同样进行下载。
        /// </summary>
            AlwaysDownload
        }

        #endregion

        #region DlClientList | Minecraft 客户端 版本列表

        // 主加载器
        public struct DlClientListResult
        {
            /// <summary>
        /// 数据来源名称，如“Mojang”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的 Json 数据。
        /// </summary>
            public JObject Value;
            // ''' <summary>
            // ''' 官方源的失败原因。若没有则为 Nothing。
            // ''' </summary>
            // Public OfficialError As Exception
        }
        /// <summary>
    /// Minecraft 客户端 版本列表，主加载器。
    /// 若要求镜像源必须包含某个版本，则将该版本 ID 作为输入（#5195）。
    /// </summary>
        public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListLoader = new ModLoader.LoaderTask<string, DlClientListResult>("DlClientList Main", DlClientListMain);
        private static void DlClientListMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>(DlClientListBmclapiLoader, 30), new KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>(DlClientListMojangLoader, 30 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>(DlClientListMojangLoader, 5), new KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>(DlClientListBmclapiLoader, 5 + 30) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>(DlClientListMojangLoader, 60), new KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>(DlClientListBmclapiLoader, 60 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        // 各个下载源的分加载器
        /// <summary>
    /// Minecraft 客户端 版本列表，Mojang 官方源加载器。
    /// </summary>
        public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListMojangLoader = new ModLoader.LoaderTask<string, DlClientListResult>("DlClientList Mojang", DlClientListMojangMain);
        private static bool IsNewClientVersionHinted = false;
        private static void DlClientListMojangMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
        {
            JObject Json = (JObject)ModNet.NetGetCodeByRequestRetry("https://launchermeta.mojang.com/mc/game/version_manifest.json", IsJson: true);
            try
            {
                JArray Versions = (JArray)Json["versions"];
                if (Versions.Count < 200)
                    throw new Exception("获取到的版本列表长度不足（" + Json.ToString() + "）");
                // 添加 UVMC 项
                string CacheFilePath = ModBase.PathTemp + @"Cache\uvmc-download.json";
                if (!File.Exists(CacheFilePath))
                {
                    try
                    {
                        JObject UnlistedJson = (JObject)ModNet.NetGetCodeByRequestRetry("https://zkitefly.github.io/unlisted-versions-of-minecraft/version_manifest.json", IsJson: true);
                        File.WriteAllText(CacheFilePath, UnlistedJson.ToString());
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log("[Download] 未列出的版本官方源下载失败: " + ex.Message);
                    }
                }
                else
                {
                    JObject CachedJson = (JObject)ModBase.GetJson(ModBase.ReadFile(CacheFilePath));
                    Versions.Merge(CachedJson["versions"]);
                }
                // 返回
                Loader.Output = new DlClientListResult() { IsOfficial = true, SourceName = "Mojang 官方源", Value = Json };
                // 解析更新提示（Release）
                string Version = (string)Json["latest"]["release"];
                if (((bool)ModBase.Setup.Get("ToolUpdateRelease")) && !Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("ToolUpdateReleaseLast"), "", false) && Version is not null && !Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("ToolUpdateReleaseLast"), Version, false))
                {
                    ModMinecraft.McDownloadClientUpdateHint(Version, Json);
                    IsNewClientVersionHinted = true;
                }
                ModDownloadLib.McVersionHighest = Conversions.ToInteger(Version.Split(".")[1]);
                ModBase.Setup.Set("ToolUpdateReleaseLast", Version);
                // 解析更新提示（Snapshot）
                Version = (string)Json["latest"]["snapshot"];
                if (((bool)ModBase.Setup.Get("ToolUpdateSnapshot") && !Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("ToolUpdateSnapshotLast"), "", false)) && Version is not null && !Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("ToolUpdateSnapshotLast"), Version, false) && !IsNewClientVersionHinted)
                {
                    ModMinecraft.McDownloadClientUpdateHint(Version, Json);
                }
                ModBase.Setup.Set("ToolUpdateSnapshotLast", Version ?? "Nothing");
            }
            catch (Exception ex)
            {
                throw new Exception("Minecraft 官方源版本列表解析失败", ex);
            }
        }
        /// <summary>
    /// Minecraft 客户端 版本列表，BMCLAPI 源加载器。
    /// </summary>
        public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListBmclapiLoader = new ModLoader.LoaderTask<string, DlClientListResult>("DlClientList Bmclapi", DlClientListBmclapiMain);
        private static void DlClientListBmclapiMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
        {
            JObject Json = (JObject)ModNet.NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", IsJson: true);
            try
            {
                JArray Versions = (JArray)Json["versions"];
                if (Versions.Count < 200)
                    throw new Exception("获取到的版本列表长度不足（" + Json.ToString() + "）");
                // 添加 UVMC 项
                string CacheFilePath = ModBase.PathTemp + @"Cache\uvmc-download.json";
                if (!File.Exists(CacheFilePath))
                {
                    try
                    {
                        JObject UnlistedJson = (JObject)ModNet.NetGetCodeByRequestRetry("https://raw.gitcode.com/zkitefly/unlisted-versions-of-minecraft/raw/main/version_manifest.json", IsJson: true);
                        File.WriteAllText(CacheFilePath, UnlistedJson.ToString());
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log("[Download] 未列出的版本镜像源下载失败: " + ex.Message);
                    }
                }
                else
                {
                    JObject CachedJson = (JObject)ModBase.GetJson(ModBase.ReadFile(CacheFilePath));
                    Versions.Merge(CachedJson["versions"]);
                }
                // 检查是否有要求的版本（#5195）
                if (!string.IsNullOrEmpty(Loader.Input))
                {
                    string Id = Loader.Input;
                    try
                    {
                        if (!DlClientListLoader.Output.Value["versions"].Any(v => (string)v["id"] == Id))
                        {
                            throw new Exception("BMCLAPI 源未包含目标版本 " + Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log("检查 BMCLAPI 包含版本失败: " + ex.ToString());
                    }
                }
                // 返回
                Loader.Output = new DlClientListResult() { IsOfficial = false, SourceName = "BMCLAPI", Value = Json };
            }
            catch (Exception ex)
            {
                throw new Exception("Minecraft BMCLAPI 版本列表解析失败（" + Json.ToString() + "）", ex);
            }
        }

        /// <summary>
    /// 获取某个版本的 Json 下载地址，若失败则返回 Nothing。必须在工作线程执行。
    /// </summary>
        public static object DlClientListGet(string Id)
        {
            try
            {
                // 确认版本格式标准
                Id = Id.Replace("_", "-"); // 1.7.10_pre4 在版本列表中显示为 1.7.10-pre4
                if (Id != "1.0" && Id.EndsWithF(".0"))
                    Id = Strings.Left(Id, Id.Length - 2); // OptiFine 1.8 的下载会触发此问题，显示版本为 1.8.0
                                                          // 获取 Minecraft 版本列表
                switch (DlClientListLoader.State)
                {
                    case ModBase.LoadState.Finished:
                        {
                            // 从当前的结果获取目标版本…
                            foreach (JObject Version in DlClientListLoader.Output.Value["versions"])
                            {
                                if ((string)Version["id"] == Id)
                                    return Version["url"].ToString();
                            }
                            // …如果没有，则重新尝试获取（在版本刚更新时可能出现这种情况，#5195）
                            DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                            break;
                        }
                    case ModBase.LoadState.Loading:
                        {
                            DlClientListLoader.WaitForExit(Id);
                            break;
                        }
                    case ModBase.LoadState.Failed:
                    case ModBase.LoadState.Aborted:
                    case ModBase.LoadState.Waiting:
                        {
                            DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                            break;
                        }
                }
                // 重新查找版本
                foreach (JObject Version in DlClientListLoader.Output.Value["versions"])
                {
                    if ((string)Version["id"] == Id)
                        return Version["url"].ToString();
                }
                ModBase.Log($"未发现版本 {Id} 的 json 下载地址，版本列表返回为：{Constants.vbCrLf}{DlClientListLoader.Output.Value.ToString()}", ModBase.LogLevel.Debug);
                return null;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"获取版本 {Id} 的 json 下载地址失败");
                return null;
            }
        }

        #endregion

        #region DlOptiFineList | OptiFine 版本列表

        public struct DlOptiFineListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public List<DlOptiFineListEntry> Value;
        }

        public class DlOptiFineListEntry
        {
            /// <summary>
        /// 显示名称，已去除 HD_U 字样，如“1.12.2 C8”。
        /// </summary>
            public string NameDisplay;
            /// <summary>
        /// 原始文件名称，如“preview_OptiFine_1.11_HD_U_E1_pre.jar”。
        /// </summary>
            public string NameFile;
            /// <summary>
        /// 对应的版本名称，如“1.13.2-OptiFine_HD_U_E6”。
        /// </summary>
            public string NameVersion;
            /// <summary>
        /// 是否为测试版。
        /// </summary>
            public bool IsPreview;
            /// <summary>
        /// 对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
            public string Inherit
            {
                get
                {
                    return _inherit;
                }
                set
                {
                    if (value.EndsWithF(".0"))
                        value = Strings.Left(value, value.Length - 2);
                    _inherit = value;
                }
            }
            private string _inherit;
            /// <summary>
        /// 发布时间，格式为“yyyy/mm/dd”。OptiFine 源无此数据。
        /// </summary>
            public string ReleaseTime;
            /// <summary>
        /// 需要的最低 Forge 版本。空字符串为无限制，Nothing 为不兼容，“28.1.56” 表示版本号，“1161” 表示版本号的最后一位。
        /// </summary>
            public string RequiredForgeVersion;
        }

        /// <summary>
    /// OptiFine 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListLoader = new ModLoader.LoaderTask<int, DlOptiFineListResult>("DlOptiFineList Main", DlOptiFineListMain);
        private static void DlOptiFineListMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>(DlOptiFineListBmclapiLoader, 30), new KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>(DlOptiFineListOfficialLoader, 30 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>(DlOptiFineListOfficialLoader, 5), new KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>(DlOptiFineListBmclapiLoader, 5 + 30) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>(DlOptiFineListOfficialLoader, 60), new KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>(DlOptiFineListBmclapiLoader, 60 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// OptiFine 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListOfficialLoader = new ModLoader.LoaderTask<int, DlOptiFineListResult>("DlOptiFineList Official", DlOptiFineListOfficialMain);
        private static void DlOptiFineListOfficialMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
        {
            string Result = ModNet.NetGetCodeByClient("https://optifine.net/downloads", Encoding.Default);
            if (Result.Length < 200)
                throw new Exception("获取到的版本列表长度不足（" + Result + "）");
            try
            {
                // 获取所有版本信息
                var Forge = Result.RegexSearch("(?<=colForge'>)[^<]*");
                var ReleaseTime = Result.RegexSearch("(?<=colDate'>)[^<]+");
                var Name = Result.RegexSearch("(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar\")");
                if (!(ReleaseTime.Count == Name.Count))
                    throw new Exception("版本与发布时间数据无法对应");
                if (!(Forge.Count == Name.Count))
                    throw new Exception("版本与 Forge 兼容数据无法对应");
                if (ReleaseTime.Count < 10)
                    throw new Exception("获取到的版本数量不足（" + Result + "）");
                // 转化为列表输出
                var Versions = new List<DlOptiFineListEntry>();
                for (int i = 0, loopTo = ReleaseTime.Count - 1; i <= loopTo; i++)
                {
                    Name[i] = Name[i].Replace("_", " ");
                    var Entry = new DlOptiFineListEntry()
                    {
                        NameDisplay = Name[i].Replace("HD U ", "").Replace(".0 ", " "),
                        ReleaseTime = (new[] { ReleaseTime[i].Split(".")[2], ReleaseTime[i].Split(".")[1], ReleaseTime[i].Split(".")[0] }).Join("/"),
                        IsPreview = Name[i].ContainsF("pre", true),
                        Inherit = Name[i].ToString().Split(" ")[0],
                        NameFile = (Name[i].ContainsF("pre", true) ? "preview_" : "") + "OptiFine_" + Name[i].Replace(" ", "_") + ".jar",
                        RequiredForgeVersion = Forge[i].Replace("Forge ", "").Replace("#", "")
                    };
                    if (Entry.RequiredForgeVersion.Contains("N/A"))
                        Entry.RequiredForgeVersion = null;
                    Entry.NameVersion = Entry.Inherit + "-OptiFine_" + Name[i].ToString().Replace(" ", "_").Replace(Entry.Inherit + "_", "");
                    Versions.Add(Entry);
                }
                Loader.Output = new DlOptiFineListResult() { IsOfficial = true, SourceName = "OptiFine 官方源", Value = Versions };
            }
            catch (Exception ex)
            {
                throw new Exception("OptiFine 官方源版本列表解析失败（" + Result + "）", ex);
            }
        }

        /// <summary>
    /// OptiFine 版本列表，BMCLAPI。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListBmclapiLoader = new ModLoader.LoaderTask<int, DlOptiFineListResult>("DlOptiFineList Bmclapi", DlOptiFineListBmclapiMain);
        private static void DlOptiFineListBmclapiMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
        {
            JArray Json = (JArray)ModNet.NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/optifine/versionList", IsJson: true);
            try
            {
                var Versions = new List<DlOptiFineListEntry>();
                foreach (JObject Token in Json)
                {
                    var Entry = new DlOptiFineListEntry()
                    {
                        NameDisplay = (Token["mcversion"].ToString() + Token["type"].ToString().Replace("HD_U", "").Replace("_", " ") + " " + Token["patch"].ToString()).Replace(".0 ", " "),
                        ReleaseTime = "",
                        IsPreview = Token["patch"].ToString().ContainsF("pre", true),
                        Inherit = Token["mcversion"].ToString(),
                        NameFile = Token["filename"].ToString(),
                        RequiredForgeVersion = (Token["forge"] ?? "").ToString().Replace("Forge ", "").Replace("#", "")
                    };
                    if (Entry.RequiredForgeVersion.Contains("N/A"))
                        Entry.RequiredForgeVersion = null;
                    Entry.NameVersion = Entry.Inherit + "-OptiFine_" + (Token["type"].ToString() + " " + Token["patch"].ToString()).Replace(".0 ", " ").Replace(" ", "_").Replace(Entry.Inherit + "_", "");
                    Versions.Add(Entry);
                }
                Loader.Output = new DlOptiFineListResult() { IsOfficial = false, SourceName = "BMCLAPI", Value = Versions };
            }
            catch (Exception ex)
            {
                throw new Exception("OptiFine BMCLAPI 版本列表解析失败（" + Json.ToString() + "）", ex);
            }
        }

        #endregion

        #region DlForgeList | Forge Minecraft 版本列表

        public struct DlForgeListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public List<string> Value;
        }

        /// <summary>
    /// Forge 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListLoader = new ModLoader.LoaderTask<int, DlForgeListResult>("DlForgeList Main", DlForgeListMain);
        private static void DlForgeListMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>(DlForgeListBmclapiLoader, 30), new KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>(DlForgeListOfficialLoader, 30 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>(DlForgeListOfficialLoader, 5), new KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>(DlForgeListBmclapiLoader, 5 + 30) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>(DlForgeListOfficialLoader, 60), new KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>(DlForgeListBmclapiLoader, 60 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// Forge 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListOfficialLoader = new ModLoader.LoaderTask<int, DlForgeListResult>("DlForgeList Official", DlForgeListOfficialMain);
        private static void DlForgeListOfficialMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
        {
            string Result = Conversions.ToString(ModNet.NetGetCodeByRequestRetry("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", Encoding.Default, "text/html", UseBrowserUserAgent: true));
            if (Result.Length < 200)
                throw new Exception("获取到的版本列表长度不足（" + Result + "）");
            // 获取所有版本信息
            var Names = Result.RegexSearch("(?<=a href=\"index_)[0-9.]+(_pre[0-9]?)?(?=.html)");
            Names.Add("1.2.4"); // 1.2.4 不会被匹配上
            if (Names.Count < 10)
                throw new Exception("获取到的版本数量不足（" + Result + "）");
            Loader.Output = new DlForgeListResult() { IsOfficial = true, SourceName = "Forge 官方源", Value = Names };
        }

        /// <summary>
    /// Forge 版本列表，BMCLAPI。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListBmclapiLoader = new ModLoader.LoaderTask<int, DlForgeListResult>("DlForgeList Bmclapi", DlForgeListBmclapiMain);
        private static void DlForgeListBmclapiMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
        {
            string Result = Conversions.ToString(ModNet.NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/forge/minecraft", Encoding.Default));
            if (Result.Length < 200)
                throw new Exception("获取到的版本列表长度不足（" + Result + "）");
            // 获取所有版本信息
            var Names = Result.RegexSearch("[0-9.]+(_pre[0-9]?)?");
            if (Names.Count < 10)
                throw new Exception("获取到的版本数量不足（" + Result + "）");
            Loader.Output = new DlForgeListResult() { IsOfficial = false, SourceName = "BMCLAPI", Value = Names };
        }

        #endregion

        #region DlForgeVersion | Forge 版本列表

        public abstract class DlForgelikeEntry
        {
            /// <summary>
        /// Forgelike 种类。Forge、NeoForge、Cleanroom。
        /// </summary>
            public ForgelikeType ForgeType;
            public enum ForgelikeType
            {
                Forge,
                NeoForge,
                Cleanroom
            }
            /// <summary>
        /// 加载器名称。Forge 或 NeoForge。
        /// </summary>
            public string LoaderName
            {
                get
                {
                    return (int)ForgeType == 1 ? "NeoForge" : "Forge";
                }
            }
            /// <summary>
        /// 文件扩展名。不以小数点开头。
        /// </summary>
            public string FileExtension
            {
                get
                {
                    if (ForgeType == 0)
                    {
                        return ((DlForgeVersionEntry)this).Category == "installer" ? "jar" : "zip";
                    }
                    else
                    {
                        return "jar";
                    }
                }
            }
            /// <summary>
        /// Forge：MC 版本是否小于 1.13。
        /// NeoForge：MC 版本是否为 1.20.1。
        /// Cleanroom：固定为 False。
        /// </summary>
            public bool IsLegacy
            {
                get
                {
                    // Cleanroom 始终为 False
                    if ((int)ForgeType == 2)
                        return false;
                    // 虽然很抽象，但确实可以这样判断
                    // Forge：1.13+ 的版本号首位都大于 20
                    // NeoForge：1.20.1 的版本号首位人为规定为 19 开头
                    return Version.Major < 20;
                }
            }
            /// <summary>
        /// 标准化后的版本号，仅可用于比较与排序。
        /// 格式：Major.Minor.Build.Revision
        /// Forge：如 “50.1.9.0”（最后一位固定为 0）、“14.22.1.2478”（Legacy）。
        /// NeoForge：如 “20.4.30.0”（最后一位固定为 0）、“19.47.1.99”（Legacy：第一位固定为 19）。
        /// Cleanroom：如 “0.2.4.1”（Alpha：最后一位固定为 1）。
        /// </summary>
            public Version Version;
            /// <summary>
        /// 可对玩家显示的非格式化版本名。
        /// Forge：如 “50.1.9”、“14.22.1.2478”（Legacy）。
        /// NeoForge：如 “20.4.30-beta”、“47.1.99”（Legacy）。
        /// Cleanroom：如 “0.2.4-alpha”。
        /// </summary>
            public string VersionName;
            /// <summary>
        /// 对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
            public string Inherit;
        }

        public class DlForgeVersionEntry : DlForgelikeEntry
        {
            /// <summary>
        /// 发布时间，格式为“yyyy/MM/dd HH:mm”。
        /// </summary>
            public string ReleaseTime;
            /// <summary>
        /// 文件的 MD5 或 SHA1（BMCLAPI 的老版本是 MD5，新版本是 SHA1；官方源总是 MD5）。
        /// </summary>
            public string Hash = null;
            /// <summary>
        /// 是否为推荐版本。
        /// </summary>
            public bool IsRecommended;
            /// <summary>
        /// 安装类型。有 installer、client、universal 三种。
        /// </summary>
            public string Category;
            /// <summary>
        /// 用于下载的文件版本名。可能在 Version 的基础上添加了分支。
        /// </summary>
            public string FileVersion;

            public DlForgeVersionEntry(string Version, string Branch, string Inherit)
            {
                // 司马版本的特殊处理
                if (Version == "11.15.1.2318" || Version == "11.15.1.1902" || Version == "11.15.1.1890")
                    Branch = "1.8.9";
                if (Branch is null && Inherit == "1.7.10" && Conversions.ToDouble(Version.Split(".")[3]) >= 1300d)
                    Branch = "1.7.10";
                // 为 DlForgelikeEntry 提供所有信息
                ForgeType = 0;
                VersionName = Version;
                this.Version = new Version(Version);
                this.Inherit = Inherit;
                FileVersion = Version + (Branch is null ? "" : "-" + Branch);
            }
        }

        /// <summary>
    /// Forge 版本列表，主加载器。
    /// </summary>
        public static void DlForgeVersionMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
        {
            var DlForgeVersionOfficialLoader = new ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>("DlForgeVersion Official", DlForgeVersionOfficialMain);
            var DlForgeVersionBmclapiLoader = new ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>("DlForgeVersion Bmclapi", DlForgeVersionBmclapiMain);
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>(DlForgeVersionBmclapiLoader, 30), new KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>(DlForgeVersionOfficialLoader, 30 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>(DlForgeVersionOfficialLoader, 5), new KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>(DlForgeVersionBmclapiLoader, 5 + 30) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>(DlForgeVersionOfficialLoader, 60), new KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>(DlForgeVersionBmclapiLoader, 60 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// Forge 版本列表，官方源。
    /// </summary>
        public static void DlForgeVersionOfficialMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
        {
            string Result;
            try
            {
                Result = ModNet.NetGetCodeByLoader("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_" + Loader.Input.Replace("-", "_") + ".html", UseBrowserUserAgent: true); // 兼容 Forge 1.7.10-pre4，#4057
            }
            catch (Exception ex)
            {
                if (ModBase.GetExceptionSummary(ex).Contains("(404)"))
                {
                    throw new Exception("不可用");
                }
                else
                {
                    throw;
                }
            }
            if (Result.Length < 1000)
                throw new Exception("获取到的版本列表长度不足（" + Result + "）");
            var Versions = new List<DlForgeVersionEntry>();
            try
            {
                // 分割版本信息
                string[] VersionCodes = Strings.Mid(Result, 1, Result.LastIndexOfF("</table>")).Split("<td class=\"download-version");
                // 获取所有版本信息
                for (int i = 1, loopTo = VersionCodes.Count() - 1; i <= loopTo; i++)
                {
                    string VersionCode = VersionCodes[i];
                    try
                    {
                        // 基础信息获取
                        string Name = VersionCode.RegexSeek(@"(?<=[^(0-9)]+)[0-9\.]+");
                        bool IsRecommended = VersionCode.Contains("fa promo-recommended");
                        string Inherit = Loader.Input;
                        // 分支获取
                        string Branch = VersionCode.RegexSeek($"(?<=-{Name}-)[^-\"]+(?=-[a-z]+.[a-z]{{3}})");
                        if (string.IsNullOrWhiteSpace(Branch))
                            Branch = null;
                        // 发布时间获取
                        string ReleaseTimeOriginal = VersionCode.RegexSeek("(?<=\"download-time\" title=\")[^\"]+");
                        string[] ReleaseTimeSplit = ReleaseTimeOriginal.Split(" -:".ToCharArray()); // 原格式："2021-02-15 03:24:02"
                        var ReleaseDate = new DateTime(Conversions.ToInteger(ReleaseTimeSplit[0]), Conversions.ToInteger(ReleaseTimeSplit[1]), Conversions.ToInteger(ReleaseTimeSplit[2]), Conversions.ToInteger(ReleaseTimeSplit[3]), Conversions.ToInteger(ReleaseTimeSplit[4]), Conversions.ToInteger(ReleaseTimeSplit[5]), 0, DateTimeKind.Utc); // 年月日
                                                                                                                                                                                                                                                                                                                                                     // 时分秒
                                                                                                                                                                                                                                                                                                                                                     // 以 UTC 时间作为标准
                        string ReleaseTime = ReleaseDate.ToLocalTime().ToString("yyyy'/'MM'/'dd HH':'mm"); // 时区与格式转换
                                                                                                           // 分类与 MD5 获取
                        string MD5;
                        string Category;
                        if (VersionCode.Contains("classifier-installer\""))
                        {
                            // 类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                            VersionCode = VersionCode.Substring(VersionCode.IndexOfF("installer.jar"));
                            MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                            Category = "installer";
                        }
                        else if (VersionCode.Contains("classifier-universal\""))
                        {
                            // 类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                            VersionCode = VersionCode.Substring(VersionCode.IndexOfF("universal.zip"));
                            MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                            Category = "universal";
                        }
                        else if (VersionCode.Contains("client.zip"))
                        {
                            // 类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                            VersionCode = VersionCode.Substring(VersionCode.IndexOfF("client.zip"));
                            MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                            Category = "client";
                        }
                        else
                        {
                            // 没有任何下载（1.6.4 有一部分这种情况）
                            continue;
                        }
                        // 添加进列表
                        Versions.Add(new DlForgeVersionEntry(Name, Branch, Inherit) { Category = Category, IsRecommended = IsRecommended, Hash = MD5.Trim(Conversions.ToChar(Constants.vbCr), Conversions.ToChar(Constants.vbLf)), ReleaseTime = ReleaseTime });
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Forge 官方源版本信息提取失败（" + VersionCode + "）", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Forge 官方源版本列表解析失败（" + Result + "）", ex);
            }
            if (!Versions.Any())
                throw new Exception("不可用");
            Loader.Output = Versions;
        }

        /// <summary>
    /// Forge 版本列表，BMCLAPI。
    /// </summary>
        public static void DlForgeVersionBmclapiMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
        {
            JArray Json = (JArray)ModNet.NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/forge/minecraft/" + Loader.Input.Replace("-", "_"), IsJson: true); // 兼容 Forge 1.7.10-pre4，#4057
            var Versions = new List<DlForgeVersionEntry>();
            try
            {
                string Recommended = ModDownloadLib.McDownloadForgeRecommendedGet(Loader.Input);
                foreach (JObject Token in Json)
                {
                    // 分类与 Hash 获取
                    string Hash = null;
                    string Category = "unknown";
                    int Proi = -1;
                    foreach (JObject File in Token["files"])
                    {
                        switch (File["category"].ToString() ?? "")
                        {
                            case "installer":
                                {
                                    if (File["format"].ToString() == "jar")
                                    {
                                        // 类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                                        Hash = (string)File["hash"];
                                        Category = "installer";
                                        Proi = 2;
                                    }

                                    break;
                                }
                            case "universal":
                                {
                                    if (Proi <= 1 && File["format"].ToString() == "zip")
                                    {
                                        // 类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                                        Hash = (string)File["hash"];
                                        Category = "universal";
                                        Proi = 1;
                                    }

                                    break;
                                }
                            case "client":
                                {
                                    if (Proi <= 0 && File["format"].ToString() == "zip")
                                    {
                                        // 类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                                        Hash = (string)File["hash"];
                                        Category = "client";
                                        Proi = 0;
                                    }

                                    break;
                                }
                        }
                    }
                    // 获取 Entry
                    string Branch = (string)Token["branch"];
                    string Name = (string)Token["version"];
                    // 基础信息获取
                    var Entry = new DlForgeVersionEntry(Name, Branch, Loader.Input) { Hash = Hash, Category = Category, IsRecommended = (Recommended ?? "") == (Name ?? "") };
                    string[] TimeSplit = Token["modified"].ToString().Split('-', 'T', ':', '.', ' ', '/');
                    Entry.ReleaseTime = Token["modified"].ToObject<DateTime>().ToLocalTime().ToString("yyyy'/'MM'/'dd HH':'mm");
                    // 添加项
                    Versions.Add(Entry);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Forge BMCLAPI 版本列表解析失败（" + Json.ToString() + "）", ex);
            }
            if (!Versions.Any())
                throw new Exception("不可用");
            Loader.Output = Versions;
        }

        #endregion

        #region DlNeoForgeList | NeoForge 版本列表

        public struct DlNeoForgeListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 所有版本的列表。已经按从新到老排序。
        /// </summary>
            public List<DlNeoForgeListEntry> Value;
        }

        public class DlNeoForgeListEntry : DlForgelikeEntry
        {
            /// <summary>
        /// 是否是 Beta 版。
        /// </summary>
            public bool IsBeta;
            /// <summary>
        /// API 使用的原始版本字符串，如 “20.4.30-beta”、“1.20.1-47.1.99”（Legacy）。
        /// </summary>
            public string ApiName;
            /// <summary>
        /// 文件在官网的基础地址，不包含后缀。
        /// </summary>
            public string UrlBase
            {
                get
                {
                    string PackageName = IsLegacy ? "forge" : "neoforge";
                    return $"https://maven.neoforged.net/releases/net/neoforged/{PackageName}/{ApiName}/{PackageName}-{ApiName}";
                }
            }

            public DlNeoForgeListEntry(string ApiName)
            {
                ForgeType = (ForgelikeType)1;
                this.ApiName = ApiName;
                IsBeta = ApiName.Contains("beta");
                if (ApiName.Contains("1.20.1")) // 1.20.1-47.1.99
                {
                    VersionName = ApiName.Replace("1.20.1-", "");
                    Version = new Version("19." + VersionName);
                    Inherit = "1.20.1";
                }
                else // 20.4.30-beta
                {
                    VersionName = ApiName;
                    Version = new Version(ApiName.BeforeFirst("-"));
                    Inherit = $"1.{Version.Major}" + (Version.Minor == 0 ? "" : "." + Version.Minor);
                }
            }
        }

        /// <summary>
    /// NeoForge 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListLoader = new ModLoader.LoaderTask<int, DlNeoForgeListResult>("DlNeoForgeList Main", DlNeoForgeListMain);
        private static void DlNeoForgeListMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>(DlNeoForgeListBmclapiLoader, 30), new KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>(DlNeoForgeListOfficialLoader, 30 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>(DlNeoForgeListOfficialLoader, 5), new KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>(DlNeoForgeListBmclapiLoader, 5 + 30) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>(DlNeoForgeListOfficialLoader, 60), new KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>(DlNeoForgeListBmclapiLoader, 60 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// NeoForge 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListOfficialLoader = new ModLoader.LoaderTask<int, DlNeoForgeListResult>("DlNeoForgeList Official", DlNeoForgeListOfficialMain);
        private static void DlNeoForgeListOfficialMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> Loader)
        {
            // 获取版本列表 JSON
            string ResultLatest = ModNet.NetGetCodeByLoader("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", UseBrowserUserAgent: true, IsJson: true);
            string ResultLegacy = ModNet.NetGetCodeByLoader("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", UseBrowserUserAgent: true, IsJson: true);
            if (ResultLatest.Length < 100 || ResultLegacy.Length < 100)
                throw new Exception("获取到的版本列表长度不足（" + ResultLatest + "）");
            // 解析
            try
            {
                Loader.Output = new DlNeoForgeListResult()
                {
                    IsOfficial = true,
                    SourceName = "NeoForge 官方源",
                    Value = GetNeoForgeEntries(ResultLatest, ResultLegacy)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("NeoForge 官方源版本列表解析失败（" + ResultLatest + Constants.vbCrLf + Constants.vbCrLf + ResultLegacy + "）", ex);
            }
        }

        /// <summary>
    /// NeoForge 版本列表，BMCLAPI。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListBmclapiLoader = new ModLoader.LoaderTask<int, DlNeoForgeListResult>("DlNeoForgeList Bmclapi", DlNeoForgeListBmclapiMain);
        public static void DlNeoForgeListBmclapiMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> Loader)
        {
            // 获取版本列表 JSON
            string ResultLatest = ModNet.NetGetCodeByLoader("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge", UseBrowserUserAgent: true, IsJson: true);
            string ResultLegacy = ModNet.NetGetCodeByLoader("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge", UseBrowserUserAgent: true, IsJson: true);
            if (ResultLatest.Length < 100 || ResultLegacy.Length < 100)
                throw new Exception("获取到的版本列表长度不足（" + ResultLatest + "）");
            // 解析
            try
            {
                Loader.Output = new DlNeoForgeListResult()
                {
                    IsOfficial = true,
                    SourceName = "BMCLAPI",
                    Value = GetNeoForgeEntries(ResultLatest, ResultLegacy)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("NeoForge BMCLAPI 版本列表解析失败（" + ResultLatest + Constants.vbCrLf + Constants.vbCrLf + ResultLegacy + "）", ex);
            }
        }

        private static List<DlNeoForgeListEntry> GetNeoForgeEntries(string LatestJson, string LatestLegacyJson)
        {
            var VersionNames = (LatestLegacyJson + LatestJson).RegexSearch(@"(?<="")(1\.20\.1-)?\d+\.\d+\.\d+(-beta)?(?="")"); // 我寻思直接正则就行.jpg
            var Versions = VersionNames.Where(name => name != "47.1.82").Select(name => new DlNeoForgeListEntry(name)).ToList(); // 这个版本虽然在版本列表中，但不能下载
            if (!Versions.Any())
                throw new Exception("不可用");
            Versions = Versions.OrderByDescending(a => a.Version).ToList();
            return Versions;
        }

        #endregion

        #region DlCleanroomList | Cleanroom 版本列表

        public struct DlCleanroomListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 所有版本的列表。已经按从新到老排序。
        /// </summary>
            public List<DlCleanroomListEntry> Value;
        }

        public class DlCleanroomListEntry : DlForgelikeEntry
        {
            /// <summary>
        /// 是否是 Beta 版。
        /// </summary>
            public bool IsBeta;
            /// <summary>
        /// API 使用的原始版本字符串，如 “0.2.4-alpha”。
        /// </summary>
            public string ApiName;
            /// <summary>
        /// 文件在官网的基础地址，不包含后缀。
        /// </summary>
            public string UrlBase
            {
                get
                {
                    return $"https://github.com/CleanroomMC/Cleanroom/releases/download/{ApiName}/cleanroom-{ApiName}";
                }
            }

            public DlCleanroomListEntry(string ApiName)
            {
                ForgeType = (ForgelikeType)1;
                this.ApiName = ApiName;
                IsBeta = ApiName.Contains("alpha");
                VersionName = ApiName;
                Version = new Version(ApiName.BeforeFirst("-"));
                Inherit = "1.12.2";
            }
        }

        /// <summary>
    /// Cleanroom 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlCleanroomListResult> DlCleanroomListLoader = new ModLoader.LoaderTask<int, DlCleanroomListResult>("DlCleanroomList Main", DlCleanroomListMain);
        private static void DlCleanroomListMain(ModLoader.LoaderTask<int, DlCleanroomListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>(DlCleanroomListOfficialLoader, 30) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>(DlCleanroomListOfficialLoader, 5) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>(DlCleanroomListOfficialLoader, 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// Cleanroom 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlCleanroomListResult> DlCleanroomListOfficialLoader = new ModLoader.LoaderTask<int, DlCleanroomListResult>("DlCleanroomList Official", DlCleanroomListOfficialMain);
        private static void DlCleanroomListOfficialMain(ModLoader.LoaderTask<int, DlCleanroomListResult> Loader)
        {
            // 获取版本列表 JSON
            string ResultLatest = Conversions.ToString(ModNet.NetGetCodeByRequestRetry("https://api.github.com/repos/CleanroomMC/Cleanroom/releases", UseBrowserUserAgent: true));
            if (ResultLatest.Length < 100)
                throw new Exception("获取到的版本列表长度不足（" + ResultLatest + "）");
            // 解析
            try
            {
                Loader.Output = new DlCleanroomListResult()
                {
                    IsOfficial = true,
                    SourceName = "Cleanroom 官方源",
                    Value = GetCleanroomEntries(ResultLatest)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Cleanroom 官方源版本列表解析失败（" + ResultLatest + "）", ex);
            }
        }

        private static List<DlCleanroomListEntry> GetCleanroomEntries(string LatestJson)
        {
            var Versions = new List<DlCleanroomListEntry>();
            var Json = JArray.Parse(LatestJson);
            foreach (JObject Token in Json)
                Versions.Add(new DlCleanroomListEntry(Token["tag_name"].ToString()) { ForgeType = (DlForgelikeEntry.ForgelikeType)2 });
            if (!Versions.Any())
                throw new Exception("没有可用版本");
            Versions = Versions.OrderByDescending(a => a.Version).ToList();
            return Versions;
        }

        #endregion

        #region DlLiteLoaderList | LiteLoader 版本列表

        public struct DlLiteLoaderListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public List<DlLiteLoaderListEntry> Value;
            /// <summary>
        /// 官方源的失败原因。若没有则为 Nothing。
        /// </summary>
            public Exception OfficialError;
        }

        public class DlLiteLoaderListEntry
        {
            /// <summary>
        /// 实际的文件名，如“liteloader-installer-1.12-00-SNAPSHOT.jar”。
        /// </summary>
            public string FileName;
            /// <summary>
        /// 是否为测试版。
        /// </summary>
            public bool IsPreview;
            /// <summary>
        /// 对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
            public string Inherit;
            /// <summary>
        /// 是否为 1.7 及更早的远古版。
        /// </summary>
            public bool IsLegacy;
            /// <summary>
        /// 发布时间，格式为“yyyy/mm/dd HH:mm”。
        /// </summary>
            public string ReleaseTime;
            /// <summary>
        /// 文件的 MD5。
        /// </summary>
            public string MD5;
            /// <summary>
        /// 对应的 Json 项。
        /// </summary>
            public JToken JsonToken;
        }

        /// <summary>
    /// LiteLoader 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListLoader = new ModLoader.LoaderTask<int, DlLiteLoaderListResult>("DlLiteLoaderList Main", DlLiteLoaderListMain);
        private static void DlLiteLoaderListMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>(DlLiteLoaderListBmclapiLoader, 30), new KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>(DlLiteLoaderListOfficialLoader, 30 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>(DlLiteLoaderListOfficialLoader, 5), new KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>(DlLiteLoaderListBmclapiLoader, 5 + 30) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>(DlLiteLoaderListOfficialLoader, 60), new KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>(DlLiteLoaderListBmclapiLoader, 60 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// LiteLoader 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListOfficialLoader = new ModLoader.LoaderTask<int, DlLiteLoaderListResult>("DlLiteLoaderList Official", DlLiteLoaderListOfficialMain);
        private static void DlLiteLoaderListOfficialMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
        {
            JObject Result = (JObject)ModNet.NetGetCodeByRequestRetry("https://dl.liteloader.com/versions/versions.json", IsJson: true);
            try
            {
                JObject Json = (JObject)Result["versions"];
                var Versions = new List<DlLiteLoaderListEntry>();
                foreach (KeyValuePair<string, JToken> Pair in Json)
                {
                    if (Pair.Key.StartsWithF("1.6") || Pair.Key.StartsWithF("1.5"))
                        continue;
                    var RealEntry = (Pair.Value["artefacts"] ?? Pair.Value["snapshots"])["com.mumfrey:liteloader"]["latest"];
                    Versions.Add(new DlLiteLoaderListEntry()
                    {
                        Inherit = Pair.Key,
                        IsLegacy = Conversions.ToDouble(Pair.Key.Split(".")[1]) < 8d,
                        IsPreview = RealEntry["stream"].ToString().ToLower() == "snapshot",
                        FileName = "liteloader-installer-" + Pair.Key + (Pair.Key == "1.8" || Pair.Key == "1.9" ? ".0" : "") + "-00-SNAPSHOT.jar",
                        MD5 = (string)RealEntry["md5"],
                        ReleaseTime = ModBase.GetLocalTime(ModBase.GetDate((int)RealEntry["timestamp"])).ToString("yyyy'/'MM'/'dd HH':'mm"),
                        JsonToken = RealEntry
                    });
                }
                Loader.Output = new DlLiteLoaderListResult() { IsOfficial = true, SourceName = "LiteLoader 官方源", Value = Versions };
            }
            catch (Exception ex)
            {
                throw new Exception("LiteLoader 官方源版本列表解析失败（" + Result.ToString() + "）", ex);
            }
        }

        /// <summary>
    /// LiteLoader 版本列表，BMCLAPI。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListBmclapiLoader = new ModLoader.LoaderTask<int, DlLiteLoaderListResult>("DlLiteLoaderList Bmclapi", DlLiteLoaderListBmclapiMain);
        private static void DlLiteLoaderListBmclapiMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
        {
            JObject Result = (JObject)ModNet.NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json", IsJson: true);
            try
            {
                JObject Json = (JObject)Result["versions"];
                var Versions = new List<DlLiteLoaderListEntry>();
                foreach (KeyValuePair<string, JToken> Pair in Json)
                {
                    if (Pair.Key.StartsWithF("1.6") || Pair.Key.StartsWithF("1.5"))
                        continue;
                    var RealEntry = (Pair.Value["artefacts"] ?? Pair.Value["snapshots"])["com.mumfrey:liteloader"]["latest"];
                    Versions.Add(new DlLiteLoaderListEntry()
                    {
                        Inherit = Pair.Key,
                        IsLegacy = Conversions.ToDouble(Pair.Key.Split(".")[1]) < 8d,
                        IsPreview = RealEntry["stream"].ToString().ToLower() == "snapshot",
                        FileName = "liteloader-installer-" + Pair.Key + (Pair.Key == "1.8" || Pair.Key == "1.9" ? ".0" : "") + "-00-SNAPSHOT.jar",
                        MD5 = (string)RealEntry["md5"],
                        ReleaseTime = ModBase.GetLocalTime(ModBase.GetDate((int)RealEntry["timestamp"])).ToString("yyyy'/'MM'/'dd HH':'mm"),
                        JsonToken = RealEntry
                    });
                }
                Loader.Output = new DlLiteLoaderListResult() { IsOfficial = false, SourceName = "BMCLAPI", Value = Versions };
            }
            catch (Exception ex)
            {
                throw new Exception("LiteLoader BMCLAPI 版本列表解析失败（" + Result.ToString() + "）", ex);
            }
        }

        #endregion

        #region DlFabricList | Fabric 列表

        public struct DlFabricListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public JObject Value;
        }

        /// <summary>
    /// Fabric 列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListLoader = new ModLoader.LoaderTask<int, DlFabricListResult>("DlFabricList Main", DlFabricListMain);
        private static void DlFabricListMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>(DlFabricListBmclapiLoader, 30), new KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>(DlFabricListOfficialLoader, 30 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>(DlFabricListOfficialLoader, 5), new KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>(DlFabricListBmclapiLoader, 5 + 30) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>(DlFabricListOfficialLoader, 60), new KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>(DlFabricListBmclapiLoader, 60 + 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// Fabric 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListOfficialLoader = new ModLoader.LoaderTask<int, DlFabricListResult>("DlFabricList Official", DlFabricListOfficialMain);
        private static void DlFabricListOfficialMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
        {
            JObject Result = (JObject)ModNet.NetGetCodeByRequestRetry("https://meta.fabricmc.net/v2/versions", IsJson: true);
            try
            {
                var Output = new DlFabricListResult() { IsOfficial = true, SourceName = "Fabric 官方源", Value = Result };
                if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                    throw new Exception("获取到的列表缺乏必要项");
                Loader.Output = Output;
            }
            catch (Exception ex)
            {
                throw new Exception("Fabric 官方源版本列表解析失败（" + Result.ToString() + "）", ex);
            }
        }

        /// <summary>
    /// Fabric 列表，BMCLAPI。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListBmclapiLoader = new ModLoader.LoaderTask<int, DlFabricListResult>("DlFabricList Bmclapi", DlFabricListBmclapiMain);
        private static void DlFabricListBmclapiMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
        {
            JObject Result = (JObject)ModNet.NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/fabric-meta/v2/versions", IsJson: true);
            try
            {
                var Output = new DlFabricListResult() { IsOfficial = false, SourceName = "BMCLAPI", Value = Result };
                if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                    throw new Exception("获取到的列表缺乏必要项");
                Loader.Output = Output;
            }
            catch (Exception ex)
            {
                throw new Exception("Fabric BMCLAPI 版本列表解析失败（" + Result.ToString() + "）", ex);
            }
        }

        /// <summary>
    /// Fabric API 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlFabricApiLoader = new ModLoader.LoaderTask<int, List<ModComp.CompFile>>("Fabric API List Loader", (Task) => Task.Output = ModComp.CompFilesGet("fabric-api", false));

        /// <summary>
    /// OptiFabric 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlOptiFabricLoader = new ModLoader.LoaderTask<int, List<ModComp.CompFile>>("OptiFabric List Loader", (Task) => Task.Output = ModComp.CompFilesGet("322385", true));

        #endregion

        #region DlQuiltList | Quilt 列表

        public struct DlQuiltListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public JObject Value;
        }

        /// <summary>
    /// Quilt 列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlQuiltListResult> DlQuiltListLoader = new ModLoader.LoaderTask<int, DlQuiltListResult>("DlQuiltList Main", DlQuiltListMain);
        private static void DlQuiltListMain(ModLoader.LoaderTask<int, DlQuiltListResult> Loader)
        {
            switch (ModBase.Setup.Get("ToolDownloadVersion"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, 30), new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, 60) }, Loader.IsForceRestarting);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, 5), new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, 35) }, Loader.IsForceRestarting);
                        break;
                    }

                default:
                    {
                        DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, 60), new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, 60) }, Loader.IsForceRestarting);
                        break;
                    }
            }
        }

        /// <summary>
    /// Quilt 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlQuiltListResult> DlQuiltListOfficialLoader = new ModLoader.LoaderTask<int, DlQuiltListResult>("DlQuiltList Official", DlQuiltListOfficialMain);
        private static void DlQuiltListOfficialMain(ModLoader.LoaderTask<int, DlQuiltListResult> Loader)
        {
            JObject Result = (JObject)ModNet.NetGetCodeByRequestRetry("https://meta.quiltmc.org/v3/versions", IsJson: true);
            try
            {
                var Output = new DlQuiltListResult() { IsOfficial = true, SourceName = "Quilt 官方源", Value = Result };
                if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                    throw new Exception("获取到的列表缺乏必要项");
                Loader.Output = Output;
            }
            catch (Exception ex)
            {
                throw new Exception("Quilt 官方源版本列表解析失败（" + Result.ToString() + "）", ex);
            }
        }

        // ''' <summary>
        // ''' TODO: Quilt 列表，BMCLAPI。
        // ''' </summary>
        // Public DlQuiltListBmclapiLoader As New LoaderTask(Of Integer, DlQuiltListResult)("DlQuiltList Bmclapi", AddressOf DlQuiltListBmclapiMain)
        // Private Sub DlQuiltListBmclapiMain(Loader As LoaderTask(Of Integer, DlQuiltListResult))
        // Dim Result As JObject = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/Quilt-meta/v2/versions", IsJson:=True)
        // Try
        // Dim Output = New DlQuiltListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Result}
        // If Output.Value("game") Is Nothing OrElse Output.Value("loader") Is Nothing OrElse Output.Value("installer") Is Nothing Then Throw New Exception("获取到的列表缺乏必要项")
        // Loader.Output = Output
        // Catch ex As Exception
        // Throw New Exception("Quilt BMCLAPI 版本列表解析失败（" & Result.ToString & "）", ex)
        // End Try
        // End Sub

        /// <summary>
    /// QSL 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlQSLLoader = new ModLoader.LoaderTask<int, List<ModComp.CompFile>>("QSL List Loader", (Task) => Task.Output = ModComp.CompFilesGet("qsl", false));
        #endregion

        #region DlMod | Mod 镜像源请求

        /// <summary>
    /// 对可能涉及 Mod 镜像源的请求进行处理，返回字符串或 JObject。
    /// 调用 NetGetCodeByRequest，会进行重试。
    /// </summary>
        public static object DlModRequest(string Url, bool IsJson = false)
        {
            var Urls = new List<KeyValuePair<string, int>>();
            Urls.Add(new KeyValuePair<string, int>(Url, 5));
            Urls.Add(new KeyValuePair<string, int>(Url, 20));
            // Dim McimUrl As String = DlSourceModGet(Url)
            // If McimUrl <> Url Then
            // Select Case Setup.Get("ToolDownloadMod")
            // Case 0
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
            // Case 1
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
            // Case Else
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
            // End Select
            // End If
            string Exs = "";
            foreach (var Source in Urls)
            {
                try
                {
                    return ModNet.NetGetCodeByRequestOnce(Source.Key, Encode: Encoding.UTF8, Timeout: Source.Value * 1000, IsJson: IsJson, UseBrowserUserAgent: true);
                }
                catch (Exception ex)
                {
                    Exs += ex.Message + Constants.vbCrLf;
                }
            }
            throw new Exception(Exs);
        }

        /// <summary>
    /// 对可能涉及 Mod 镜像源的请求进行处理。
    /// 调用 NetRequest，会进行重试。
    /// </summary>
        public static string DlModRequest(string Url, string Method, string Data, string ContentType)
        {
            var Urls = new List<KeyValuePair<string, int>>();
            Urls.Add(new KeyValuePair<string, int>(Url, 5));
            Urls.Add(new KeyValuePair<string, int>(Url, 20));
            // Dim McimUrl As String = DlSourceModGet(Url)
            // If McimUrl <> Url Then
            // Select Case Setup.Get("ToolDownloadMod")
            // Case 0
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
            // Case 1
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
            // Case Else
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
            // Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
            // Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
            // End Select
            // End If
            string Exs = "";
            foreach (var Source in Urls)
            {
                try
                {
                    return ModNet.NetRequestOnce(Source.Key, Method, Data, ContentType, Timeout: Source.Value * 1000);
                }
                catch (Exception ex)
                {
                    Exs += ex.Message + Constants.vbCrLf;
                }
            }
            throw new Exception(Exs);
        }

        #endregion

        #region DlSource | 镜像下载源

        public static string[] DlSourceResourceGet(string Original)
        {
            Original = Original.Replace("http://resources.download.minecraft.net", "https://resources.download.minecraft.net");
            return new[] { Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/assets").Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/assets").Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets"), Original };
        }

        public static string[] DlSourceLibraryGet(string Original)
        {
            if (new[] { "minecraftforge", "fabricmc", "neoforged" }.Any(k => Original.Contains(k))) // 不添加原版源
            {
                return new[] { Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven").Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven").Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven").Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", "https://raw.gitcode.com/zkitefly/unlisted-versions-of-minecraft/raw/main"), Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries").Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries").Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries").Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", "https://raw.gitcode.com/zkitefly/unlisted-versions-of-minecraft/raw/main") };
            }
            else
            {
                return new[] { Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven").Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven").Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven").Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", "https://raw.gitcode.com/zkitefly/unlisted-versions-of-minecraft/raw/main"), Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries").Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries").Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries").Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", "https://raw.gitcode.com/zkitefly/unlisted-versions-of-minecraft/raw/main"), Original };
            }
        }

        public static string DlSourceModGet(string Original)
        {
            return Original;
            // Return Original.
            // Replace("api.modrinth.com", "mod.mcimirror.top/modrinth").
            // Replace("staging-api.modrinth.com", "mod.mcimirror.top/modrinth").
            // Replace("cdn.modrinth.com", "mod.mcimirror.top").
            // Replace("api.curseforge.com", "mod.mcimirror.top/curseforge").
            // Replace("edge.forgecdn.net", "mod.mcimirror.top").
            // Replace("mediafilez.forgecdn.net", "mod.mcimirror.top").
            // Replace("media.forgecdn.net", "mod.mcimirror.top")
        }

        public static string[] DlSourceLauncherOrMetaGet(string Original)
        {
            if (Original is null)
                throw new Exception("无对应的 json 下载地址");
            return new[] { Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com").Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com").Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com").Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com").Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft", "https://raw.gitcode.com/zkitefly/unlisted-versions-of-minecraft/raw/main"), Original };
        }

        private static void DlSourceLoader<InputType, OutputType>(ModLoader.LoaderTask<InputType, OutputType> MainLoader, List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList, bool IsForceRestart = false)
        {
            int WaitCycle = 0;
            while (true)
            {
                // 检查状态
                bool BeforeLoadersAllFailed = true;
                foreach (var SubLoader in LoaderList)
                {
                    if (WaitCycle == 0) // 判断是否可以不加载，直接使用已经加载好的结果
                    {
                        if (IsForceRestart)
                            continue; // 强制刷新，不行
                        if (SubLoader.Key.Input is null ^ MainLoader.Input is null || SubLoader.Key.Input is not null && !SubLoader.Key.Input.Equals(MainLoader.Input))
                            continue; // 父子加载器的输入不一样，也不行
                    }
                    if (SubLoader.Key.State != ModBase.LoadState.Failed)
                        BeforeLoadersAllFailed = false;
                    if (SubLoader.Key.State == ModBase.LoadState.Finished)
                    {
                        // 检查加载器成功
                        MainLoader.Output = SubLoader.Key.Output;
                        DlSourceLoaderAbort(LoaderList);
                        return;
                    }
                    else if (BeforeLoadersAllFailed)
                    {
                        // 此前的加载器全部失败，直接启动后续加载器
                        if (WaitCycle < SubLoader.Value * 100)
                            WaitCycle = SubLoader.Value * 100;
                    }
                }
                // 第一轮时：既然不直接使用已经加载好的结果，那就启动第一个加载器
                if (WaitCycle == 0)
                {
                    LoaderList.First().Key.Start(MainLoader.Input, IsForceRestart);
                    foreach (var Loader in LoaderList.Skip(1))
                        Loader.Key.State = ModBase.LoadState.Waiting; // 将其他源标记为未启动，以确保可以切换下载源（#184）
                }
                // 检查加载器失败或超时
                for (int i = 0, loopTo = LoaderList.Count - 1; i <= loopTo; i++)
                {
                    if (WaitCycle != LoaderList[i].Value * 100)
                        continue;
                    if (i < LoaderList.Count - 1 && !LoaderList.All(l => l.Key.State == ModBase.LoadState.Failed))
                    {
                        // 若还有下一个源，则启动下一个源
                        LoaderList[i + 1].Key.Start(MainLoader.Input, IsForceRestart);
                    }
                    else
                    {
                        // 若没有，则失败
                        Exception ErrorInfo = null;
                        for (int ii = 0, loopTo1 = LoaderList.Count - 1; ii <= loopTo1; ii++)
                        {
                            LoaderList[ii].Key.Input = default; // 重置输入，以免以同样的输入“重试加载”时直接失败
                            if (LoaderList[ii].Key.Error is not null)
                            {
                                if (ErrorInfo is null || LoaderList[ii].Key.Error.Message.Contains("不可用"))
                                {
                                    ErrorInfo = LoaderList[ii].Key.Error;
                                }
                            }
                        }
                        if (ErrorInfo is null)
                            ErrorInfo = new TimeoutException("下载源连接超时");
                        DlSourceLoaderAbort(LoaderList);
                        throw ErrorInfo;
                    }
                    break;
                }
                // 计时
                Thread.Sleep(10);
                WaitCycle += 1;
                // 检查父加载器中断
                if (MainLoader.IsAborted)
                {
                    DlSourceLoaderAbort(LoaderList);
                    return;
                }
            }
        }
        private static void DlSourceLoaderAbort<InputType, OutputType>(List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList)
        {
            foreach (var Loader in LoaderList)
            {
                if (Loader.Key.State == ModBase.LoadState.Loading)
                    Loader.Key.Abort();
            }
        }

        #endregion

    }
}