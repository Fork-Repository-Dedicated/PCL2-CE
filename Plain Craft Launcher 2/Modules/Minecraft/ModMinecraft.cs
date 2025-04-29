using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public static class ModMinecraft
    {

        #region 文件夹

        /// <summary>
    /// 当前的 Minecraft 文件夹路径，以“\”结尾。
    /// </summary>
        public static string PathMcFolder;
        /// <summary>
    /// 当前的 Minecraft 文件夹列表。
    /// </summary>
        public static List<McFolder> McFolderList = new List<McFolder>();

        public class McFolder // 必须是 Class，否则不是引用类型，在 ForEach 中不会得到刷新
        {
            public string Name;
            public string Path;
            public McFolderType Type;
            public override bool Equals(object obj)
            {
                if (!(obj is McFolder))
                    return false;
                McFolder folder = (McFolder)obj;
                return (Name ?? "") == (folder.Name ?? "") && (Path ?? "") == (folder.Path ?? "") && Type == folder.Type;
            }
            public override string ToString()
            {
                return Path;
            }
        }
        public enum McFolderType
        {
            Original,
            RenamedOriginal,
            Custom
        }

        /// <summary>
    /// 加载 Minecraft 文件夹列表。
    /// </summary>
        public static ModLoader.LoaderTask<int, int> McFolderListLoader = new ModLoader.LoaderTask<int, int>("Minecraft Folder List", (_) => McFolderListLoadSub(), Priority: ThreadPriority.AboveNormal);
        private static void McFolderListLoadSub()
        {
            try
            {
                // 初始化
                var CacheMcFolderList = new List<McFolder>();

                #region 读取默认（Original）文件夹，即当前、官启文件夹，可能没有结果

                // 扫描当前文件夹
                try
                {
                    if (Directory.Exists(ModBase.Path + @"versions\"))
                        CacheMcFolderList.Add(new McFolder() { Name = "当前文件夹", Path = ModBase.Path, Type = McFolderType.Original });
                    foreach (DirectoryInfo Folder in new DirectoryInfo(ModBase.Path).GetDirectories())
                    {
                        if (Directory.Exists(Folder.FullName + @"versions\") || Folder.Name == ".minecraft")
                            CacheMcFolderList.Add(new McFolder() { Name = "当前文件夹", Path = Folder.FullName + @"\", Type = McFolderType.Original });
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败");
                }

                // 扫描官启文件夹
                string MojangPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\";
                if ((!CacheMcFolderList.Any() || (MojangPath ?? "") != (CacheMcFolderList[0].Path ?? "")) && Directory.Exists(MojangPath + @"versions\")) // 当前文件夹不是官启文件夹
                                                                                                                                                          // 具有权限且存在 versions 文件夹
                {
                    CacheMcFolderList.Add(new McFolder() { Name = "官方启动器文件夹", Path = MojangPath, Type = McFolderType.Original });
                }

                #endregion

                #region 读取自定义（Custom）文件夹，可能没有结果

                // 格式：TMZ 12>C://xxx/xx/|Test>D://xxx/xx/|名称>路径
                foreach (string Folder in (IEnumerable)((dynamic)ModBase.Setup.Get("LaunchFolders")).Split("|"))
                {
                    if (string.IsNullOrEmpty(Folder))
                        continue;
                    if (!Folder.Contains(">") || !Folder.EndsWithF(@"\"))
                    {
                        ModMain.Hint("无效的 Minecraft 文件夹：" + Folder, ModMain.HintType.Critical);
                        continue;
                    }
                    string Name = Folder.Split(">")[0];
                    string Path = Folder.Split(">")[1];
                    try
                    {
                        ModBase.CheckPermissionWithException(Path);
                        // 若已有该文件夹，则直接重命名；没有则添加
                        bool Renamed = false;
                        foreach (McFolder OriginalFolder in CacheMcFolderList)
                        {
                            if ((OriginalFolder.Path ?? "") == (Path ?? ""))
                            {
                                OriginalFolder.Name = Name;
                                OriginalFolder.Type = McFolderType.RenamedOriginal;
                                Renamed = true;
                            }
                        }
                        if (!Renamed)
                            CacheMcFolderList.Add(new McFolder() { Name = Name, Path = Path, Type = McFolderType.Custom });
                    }
                    catch (Exception ex)
                    {
                        ModMain.MyMsgBox("失效的 Minecraft 文件夹：" + Constants.vbCrLf + Path + Constants.vbCrLf + Constants.vbCrLf + ModBase.GetExceptionSummary(ex), "Minecraft 文件夹失效", IsWarn: true);
                        ModBase.Log(ex, $"无法访问 Minecraft 文件夹 {Path}");
                    }
                }

                // 将自定义文件夹情况同步到设置
                var NewSetup = new List<string>();
                foreach (McFolder Folder in CacheMcFolderList)
                {
                    if (!(Folder.Type == McFolderType.Original))
                        NewSetup.Add(Folder.Name + ">" + Folder.Path);
                }
                if (!NewSetup.Any())
                    NewSetup.Add(""); // 防止 0 元素 Join 返回 Nothing
                ModBase.Setup.Set("LaunchFolders", NewSetup.Join("|"));

                #endregion

                // 若没有可用文件夹，则创建 .minecraft
                if (!CacheMcFolderList.Any())
                {
                    Directory.CreateDirectory(ModBase.Path + @".minecraft\versions\");
                    CacheMcFolderList.Add(new McFolder() { Name = "当前文件夹", Path = ModBase.Path + @".minecraft\", Type = McFolderType.Original });
                }

                foreach (McFolder Folder in CacheMcFolderList)
                    #region 更新 launcher_profiles.json
                    #endregion
                    McFolderLauncherProfilesJsonCreate(Folder.Path);
                if (Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugDelay")))
                    Thread.Sleep(ModBase.RandomInteger(200, 2000));

                // 回设
                McFolderList = CacheMcFolderList;
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "加载 Minecraft 文件夹列表失败", ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
    /// 为 Minecraft 文件夹创建 launcher_profiles.json 文件。
    /// </summary>
        public static void McFolderLauncherProfilesJsonCreate(string Folder)
        {
            try
            {
                if (File.Exists(Folder + "launcher_profiles.json"))
                    return;
                string ResultJson = @"{
    ""profiles"":  {
        ""PCL"": {
            ""icon"": ""Grass"",
            ""name"": ""PCL"",
            ""lastVersionId"": ""latest-release"",
            ""type"": ""latest-release"",
            ""lastUsed"": """ + DateTime.Now.ToString("yyyy'-'MM'-'dd") + "T" + DateTime.Now.ToString("HH':'mm':'ss") + @".0000Z""
        }
    },
    ""selectedProfile"": ""PCL"",
    ""clientToken"": ""23323323323323323323323323323333""
}";
                ModBase.WriteFile(Folder + "launcher_profiles.json", ResultJson, Encoding: Encoding.GetEncoding("GB18030"));
                ModBase.Log("[Minecraft] 已创建 launcher_profiles.json：" + Folder);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "创建 launcher_profiles.json 失败（" + Folder + "）", ModBase.LogLevel.Feedback);
            }
        }

        #endregion

        #region 版本处理

        public const int McVersionCacheVersion = 30;

        private static McVersion _McVersionCurrent;
        private static object _McVersionLast = 0; // 为 0 以保证与 Nothing 不相同，使得 UI 显示可以正常初始化
                                                  /// <summary>
    /// 当前的 Minecraft 版本。
    /// </summary>
        public static McVersion McVersionCurrent
        {
            get
            {
                return _McVersionCurrent;
            }
            set
            {
                if (ReferenceEquals(_McVersionLast, value))
                    return;
                _McVersionCurrent = value; // 由于有可能是 Nothing，导致无法初始化，才得这样弄一圈
                _McVersionLast = value;
                if (value is null)
                    return;
                // 重置缓存的 Mod 文件夹
                PageDownloadCompDetail.CachedFolder = null;
                // 统一通行证重判
                if (ModAnimation.AniControlEnabled == 0 && Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("VersionServerNide", Version: value), ModBase.Setup.Get("CacheNideServer"), false)) && Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: value), 3, false)))
                {
                    ModBase.Setup.Set("CacheNideAccess", "");
                    ModBase.Log("[Launch] 服务器改变，要求重新登录统一通行证");
                }
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: value), 3, false)))
                {
                    ModBase.Setup.Set("CacheNideServer", ModBase.Setup.Get("VersionServerNide", Version: value));
                }
                // Authlib-Injector 重判
                if (ModAnimation.AniControlEnabled == 0 && Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("VersionServerAuthServer", Version: value), ModBase.Setup.Get("CacheAuthServerServer"), false)) && Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: value), 4, false)))
                {
                    ModBase.Setup.Set("CacheAuthAccess", "");
                    ModBase.Log("[Launch] 服务器改变，要求重新登录 Authlib-Injector");
                }
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: value), 4, false)))
                {
                    ModBase.Setup.Set("CacheAuthServerServer", ModBase.Setup.Get("VersionServerAuthServer", Version: value));
                    ModBase.Setup.Set("CacheAuthServerName", ModBase.Setup.Get("VersionServerAuthName", Version: value));
                    ModBase.Setup.Set("CacheAuthServerRegister", ModBase.Setup.Get("VersionServerAuthRegister", Version: value));
                }
            }
        }

        public class McVersion
        {

            /// <summary>
        /// 该版本的版本文件夹，以“\”结尾。
        /// </summary>
            public string Path { get; private set; }
            /// <summary>
        /// 应用版本隔离后，该版本所对应的 Minecraft 根文件夹，以“\”结尾。
        /// </summary>
            public string PathIndie
            {
                get
                {
                    InitPathIndie(Modable);
                    return Conversions.ToBoolean(ModBase.Setup.Get("VersionArgumentIndieV2", Version: this)) ? Path : PathMcFolder;
                }
            }
            /// <summary>
        /// 决定该版本是否应该被隔离。
        /// </summary>
            public void InitPathIndie(bool Modable)
            {
                if (!ModBase.Setup.IsUnset("VersionArgumentIndieV2", Version: this))
                    return;
                bool ShouldBeIndie()
                {
                    // 从老的版本独立设置中迁移：-1 未决定，0 使用全局设置，1 手动开启，2 手动关闭
                    if (!ModBase.Setup.IsUnset("VersionArgumentIndie", Version: this) && Conversions.ToBoolean(Operators.ConditionalCompareObjectGreater(ModBase.Setup.Get("VersionArgumentIndie", Version: this), 0, false)))
                    {
                        ModBase.Log($"[Minecraft] 版本隔离初始化（{Name}）：从老的版本独立设置中迁移");
                        return Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionArgumentIndie", Version: this), 1, false));
                    }
                    // 若版本文件夹下包含 mods 或 saves 文件夹，则自动开启版本隔离
                    var ModFolder = new DirectoryInfo(Path + @"mods\");
                    var SaveFolder = new DirectoryInfo(Path + @"saves\");
                    if (ModFolder.Exists && ModFolder.EnumerateFiles().Any() || SaveFolder.Exists && SaveFolder.EnumerateFiles().Any())
                    {
                        ModBase.Log($"[Minecraft] 版本隔离初始化（{Name}）：版本文件夹下存在 mods 或 saves 文件夹，自动开启");
                        return true;
                    }
                    // 根据全局的默认设置决定是否隔离
                    ModBase.Log($"[Minecraft] 版本隔离初始化（{Name}）：从全局默认设置中（{ModBase.Setup.Get("LaunchArgumentIndieV2")}）判断");
                    switch (ModBase.Setup.Get("LaunchArgumentIndieV2"))
                    {
                        case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 关闭
                            {
                                return false;
                            }
                        case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 仅隔离可安装 Mod 的版本
                            {
                                return Modable;
                            }
                        case var case2 when Operators.ConditionalCompareObjectEqual(case2, 2, false): // 仅隔离非正式版
                            {
                                return State == McVersionState.Fool || State == McVersionState.Old || State == McVersionState.Snapshot;
                            }
                        case var case3 when Operators.ConditionalCompareObjectEqual(case3, 3, false): // 隔离非正式版与可安装 Mod 的版本
                            {
                                return Modable || State == McVersionState.Fool || State == McVersionState.Old || State == McVersionState.Snapshot; // 隔离所有版本
                            }

                        default:
                            {
                                return true;
                            }
                    }
                };
                ModBase.Setup.Set("VersionArgumentIndieV2", ShouldBeIndie(), Version: this);
            }

            /// <summary>
        /// 该版本的版本文件夹名称。
        /// </summary>
            public string Name
            {
                get
                {
                    if (_Name is null && !string.IsNullOrEmpty(Path))
                        _Name = ModBase.GetFolderNameFromPath(Path);
                    return _Name;
                }
            }
            private string _Name = null;

            /// <summary>
        /// 显示的描述文本。
        /// </summary>
            public string Info = "该版本未被加载，请向作者反馈此问题";
            /// <summary>
        /// 该版本的列表检查原始结果，不受自定义影响。
        /// </summary>
            public McVersionState State = McVersionState.Error;
            /// <summary>
        /// 显示的版本图标。
        /// </summary>
            public string Logo;
            /// <summary>
        /// 是否为收藏的版本。
        /// </summary>
            public bool IsStar = false;
            /// <summary>
        /// 强制版本分类，0 为未启用，1 为隐藏，2 及以上为其他普通分类。
        /// </summary>
            public McVersionCardType DisplayType = McVersionCardType.Auto;
            /// <summary>
        /// 该版本是否可以安装 Mod。
        /// </summary>
            public bool Modable
            {
                get
                {
                    if (!IsLoaded)
                        Load();
                    return Version.HasFabric || Version.HasQuilt || Version.HasForge || Version.HasLiteLoader || Version.HasNeoForge || Version.HasCleanroom || DisplayType == McVersionCardType.API; // #223
                }
            }
            /// <summary>
        /// 版本信息。
        /// </summary>
            public McVersionInfo Version
            {
                get
                {
                    if (_Version is null)
                    {
                        _Version = new McVersionInfo();
                        #region 获取游戏版本
                        try
                        {

                            // 获取发布时间并判断是否为老版本
                            try
                            {
                                if (JsonObject["releaseTime"] is null)
                                {
                                    ReleaseTime = new DateTime(1970, 1, 1, 15, 0, 0); // 未知版本也可能显示为 1970 年
                                }
                                else
                                {
                                    ReleaseTime = JsonObject["releaseTime"].ToObject<DateTime>();
                                }
                                if (ReleaseTime.Year > 2000 && ReleaseTime <= new DateTime(2011, 11, 16)) // 2000 年到 2011 年 11 月 16 日之间
                                {
                                    _Version.McName = "Old";
                                    goto VersionSearchFinish;
                                }
                            }
                            catch
                            {
                                ReleaseTime = new DateTime(1970, 1, 1, 15, 0, 0);
                            }
                            // 实验性快照
                            if ((string)(JsonObject["type"] ?? "") == "pending")
                            {
                                _Version.McName = "pending";
                                goto VersionSearchFinish;
                            }
                            // 从 JumpLoader 信息中获取版本号
                            if (HasJumpLoader)
                            {
                                try
                                {
                                    _Version.McName = (string)JsonObject["jumploader"]["jars"]["minecraft"][0]["gameVersion"];
                                    goto VersionSearchFinish;
                                }
                                catch
                                {
                                }
                            }
                            // 从 PCL 下载的版本信息中获取版本号
                            if (JsonObject["clientVersion"] is not null)
                            {
                                _Version.McName = (string)JsonObject["clientVersion"];
                                goto VersionSearchFinish;
                            }
                            // 从 HMCL 下载的版本信息中获取版本号
                            if (JsonObject["patches"] is not null)
                            {
                                foreach (JObject Patch in JsonObject["patches"])
                                {
                                    if ((Patch["id"] ?? "").ToString() == "game" && Patch["version"] is not null)
                                    {
                                        _Version.McName = Patch["version"].ToString();
                                        goto VersionSearchFinish;
                                    }
                                }
                            }
                            // 从 Forge / NeoForge Arguments 中获取版本号
                            if (JsonObject["arguments"] is not null && JsonObject["arguments"]["game"] is not null)
                            {
                                bool Mark = false;
                                foreach (var Argument in JsonObject["arguments"]["game"])
                                {
                                    if (Mark)
                                    {
                                        _Version.McName = Argument.ToString();
                                        goto VersionSearchFinish;
                                    }
                                    if (Argument.ToString() == "--fml.mcVersion")
                                        Mark = true;
                                }
                            }
                            // 从继承版本中获取版本号
                            if (!string.IsNullOrEmpty(InheritVersion))
                            {
                                _Version.McName = (JsonObject["jar"] ?? "").ToString(); // LiteLoader 优先使用 Jar
                                if (string.IsNullOrEmpty(_Version.McName))
                                    _Version.McName = InheritVersion;
                                goto VersionSearchFinish;
                            }
                            // 从下载地址中获取版本号
                            string Regex = (JsonObject["downloads"] ?? "").ToString().RegexSeek("(?<=launcher.mojang.com/mc/game/)[^/]*");
                            if (Regex is not null)
                            {
                                _Version.McName = Regex;
                                goto VersionSearchFinish;
                            }
                            // 从 Forge 版本中获取版本号
                            string LibrariesString = JsonObject["libraries"].ToString();
                            Regex = LibrariesString.RegexSeek("(?<=net.minecraftforge:forge:)1.[0-9+.]+") ?? LibrariesString.RegexSeek("(?<=net.minecraftforge:fmlloader:)1.[0-9+.]+");
                            if (Regex is not null)
                            {
                                _Version.McName = Regex;
                                goto VersionSearchFinish;
                            }
                            // 从 OptiFine 版本中获取版本号
                            Regex = LibrariesString.RegexSeek("(?<=optifine:OptiFine:)1.[0-9+.]+");
                            if (Regex is not null)
                            {
                                _Version.McName = Regex;
                                goto VersionSearchFinish;
                            }
                            // 从 Fabric / Quilt 版本中获取版本号
                            Regex = LibrariesString.RegexSeek("(?<=((fabricmc)|(quiltmc)):intermediary:)[^\"]*");
                            if (Regex is not null)
                            {
                                _Version.McName = Regex;
                                goto VersionSearchFinish;
                            }
                            // 从 Jar 项中获取版本号
                            if (JsonObject["jar"] is not null)
                            {
                                _Version.McName = JsonObject["jar"].ToString();
                                goto VersionSearchFinish;
                            }
                            // 从 jar 文件的 version.json 中获取版本号
                            if (JsonVersion?["name"] is not null)
                            {
                                string JsonVerName = JsonVersion["name"].ToString();
                                if (JsonVerName.Length < 32) // 因为 wiki 说这玩意儿可能是个 hash，虽然我没发现
                                {
                                    _Version.McName = JsonVerName;
                                    ModBase.Log("[Minecraft] 从版本 jar 中的 version.json 获取到版本号：" + JsonVerName);
                                    goto VersionSearchFinish;
                                }
                            }
                            // 非准确的版本判断警告
                            ModBase.Log("[Minecraft] 无法完全确认 MC 版本号的版本：" + Name);
                            // 从文件夹名中获取
                            Regex = Name.RegexSeek(@"([0-9w]{5}[a-z]{1})|(1\.[0-9]+(\.[0-9]+)?(-(pre|rc)[1-9]?| Pre-Release( [1-9]{1})?)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (Regex is not null)
                            {
                                _Version.McName = Regex;
                                goto VersionSearchFinish;
                            }
                            // 从 Json 出现的版本号中获取
                            JObject JsonRaw = (JObject)JsonObject.DeepClone();
                            JsonRaw.Remove("libraries");
                            string JsonRawText = JsonRaw.ToString();
                            Regex = JsonRawText.RegexSeek(@"([0-9w]{5}[a-z]{1})|(1\.[0-9]+(\.[0-9]+)?(-(pre|rc)[1-9]?| Pre-Release( [1-9]{1})?)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (Regex is not null)
                            {
                                _Version.McName = Regex;
                                goto VersionSearchFinish;
                            }
                            // 无法获取
                            _Version.McName = "Unknown";
                            Info = "PCL 无法识别该版本的 MC 版本号";
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "识别 Minecraft 版本时出错");
                            _Version.McName = "Unknown";
                            Info = "无法识别：" + ex.Message;
                        }

                    VersionSearchFinish:
                        ;

                        // 获取版本号
                        if (_Version.McName.StartsWithF("1."))
                        {
                            string[] SplitVersion = _Version.McName.Split(' ', '_', '-', '.');
                            string SplitResult;
                            // 分割获取信息
                            SplitResult = SplitVersion.Count() >= 2 ? SplitVersion[1] : "0";
                            _Version.McCodeMain = Conversions.ToInteger(SplitResult.Length <= 2 ? global::PCL.ModBase.Val(SplitResult) : "0");
                            SplitResult = SplitVersion.Count() >= 3 ? SplitVersion[2] : "0";
                            _Version.McCodeSub = Conversions.ToInteger(SplitResult.Length <= 2 ? global::PCL.ModBase.Val(SplitResult) : "0");
                        }
                        else if (_Version.McName.Contains("w") || _Version.McName == "pending")
                        {
                            _Version.McCodeMain = 99;
                            _Version.McCodeSub = 99;
                        }
                        #endregion
                    }
                    return _Version;
                }
                set
                {
                    _Version = value;
                }
            }
            private McVersionInfo _Version = null;

            /// <summary>
        /// 版本的发布时间。
        /// </summary>
            public DateTime ReleaseTime = new DateTime(1970, 1, 1, 15, 0, 0);

            /// <summary>
        /// 该版本的 Json 文本。
        /// </summary>
            public string JsonText
            {
                get
                {
                    // 快速检查 JSON 是否以 { 开头、} 结尾；忽略空白字符
                    bool FastJsonCheck(string Json)
                    {
                        string TrimedJson = Json.Trim();
                        return TrimedJson.StartsWithF("{") && TrimedJson.EndsWithF("}");
                    };
                    if (_JsonText is null)
                    {
                        string JsonPath = Path + Name + ".json";
                        if (!File.Exists(JsonPath))
                        {
                            // 如果文件夹下只有一个 JSON 文件，则将其作为版本 JSON
                            string[] JsonFiles = Directory.GetFiles(Path, "*.json");
                            if (JsonFiles.Count() == 1)
                            {
                                JsonPath = JsonFiles[0];
                                ModBase.Log("[Minecraft] 未找到同名版本 JSON，自动换用 " + JsonPath, ModBase.LogLevel.Debug);
                            }
                            else
                            {
                                throw new Exception($"未找到版本 JSON 文件：{Path}{Name}.json");
                            }
                        }
                        _JsonText = ModBase.ReadFile(JsonPath);
                        // 如果 ReadFile 失败会返回空字符串；这可能是由于文件被临时占用，故延时后重试
                        if (!FastJsonCheck(_JsonText))
                        {
                            if (ModBase.RunInUi())
                            {
                                ModBase.Log("[Minecraft] 版本 JSON 文件为空或有误，由于代码在主线程运行，将不再进行重试", ModBase.LogLevel.Debug);
                                ModBase.GetJson(_JsonText); // 触发异常
                            }
                            else
                            {
                                ModBase.Log($"[Minecraft] 版本 JSON 文件为空或有误，将在 2s 后重试读取（{JsonPath}）", ModBase.LogLevel.Debug);
                                Thread.Sleep(2000);
                                _JsonText = ModBase.ReadFile(JsonPath);
                                if (!FastJsonCheck(_JsonText))
                                    ModBase.GetJson(_JsonText);
                            } // 触发异常
                        }
                    }
                    return _JsonText;
                }
                set
                {
                    _JsonText = value;
                }
            }
            private string _JsonText = null;
            /// <summary>
        /// 该版本的 Json 对象。
        /// 若 Json 存在问题，在获取该属性时即会抛出异常。
        /// </summary>
            public JObject JsonObject
            {
                get
                {
                    if (_JsonObject is null)
                    {
                        string Text = JsonText; // 触发 JsonText 的 Get 事件
                        try
                        {
                            _JsonObject = (JObject)ModBase.GetJson(Text);
                            // 转换 HMCL 关键项
                            if (_JsonObject.ContainsKey("patches") && !_JsonObject.ContainsKey("time"))
                            {
                                IsHmclFormatJson = true;
                                // 合并 Json
                                // Dim HasOptiFine As Boolean = False, HasForge As Boolean = False
                                JObject CurrentObject = null;
                                var SubjsonList = new List<JObject>();
                                foreach (JObject Subjson in _JsonObject["patches"])
                                    SubjsonList.Add(Subjson);
                                SubjsonList = SubjsonList.Sort((Left, Right) => ModBase.Val((Left["priority"] ?? "0").ToString()) < ModBase.Val((Right["priority"] ?? "0").ToString()));
                                foreach (JObject Subjson in SubjsonList)
                                {
                                    string Id = (string)Subjson["id"];
                                    if (Id is not null)
                                    {
                                        // 合并 Json
                                        ModBase.Log("[Minecraft] 合并 HMCL 分支项：" + Id);
                                        if (CurrentObject is not null)
                                        {
                                            CurrentObject.Merge(Subjson);
                                        }
                                        else
                                        {
                                            CurrentObject = Subjson;
                                        }
                                    }
                                    else
                                    {
                                        ModBase.Log("[Minecraft] 存在为空的 HMCL 分支项");
                                    }
                                }
                                _JsonObject = CurrentObject;
                                // 修改附加项
                                _JsonObject["id"] = Name;
                                if (_JsonObject.ContainsKey("inheritsFrom"))
                                    _JsonObject.Remove("inheritsFrom");
                            }
                            // 与继承版本合并
                            object InheritVersion = null;
                            do
                            {
                                try
                                {
                                    InheritVersion = _JsonObject["inheritsFrom"] is null ? "" : _JsonObject["inheritsFrom"].ToString();
                                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(InheritVersion, Name, false)))
                                    {
                                        ModBase.Log("[Minecraft] 自引用的继承版本：" + Name, ModBase.LogLevel.Debug);
                                        InheritVersion = "";
                                        break;
                                    }

                                Recheck:
                                    ;

                                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(InheritVersion, "", false)))
                                    {
                                        var Inherit = new McVersion(Conversions.ToString(InheritVersion));
                                        // 继续循环
                                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(Inherit.InheritVersion, InheritVersion, false)))
                                            throw new Exception(Conversions.ToString(Operators.ConcatenateObject("版本依赖项出现嵌套：", InheritVersion)));
                                        InheritVersion = Inherit.InheritVersion;
                                        // 合并
                                        Inherit.JsonObject.Merge(_JsonObject);
                                        _JsonObject = Inherit.JsonObject;
                                        goto Recheck;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ModBase.Log(ex, "合并版本依赖项 JSON 失败（" + (InheritVersion ?? "null").ToString() + "）");
                                }
                            }
                            while (false);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("初始化版本 JSON 时失败（" + (Name ?? "null") + "）", ex);
                        }
                        try
                        {
                            // 处理 JumpLoader
                            if (Text.Contains("minecraftforge") && File.Exists(PathIndie + @"config\jumploader.json"))
                            {
                                foreach (var ModFile in Directory.EnumerateFiles(PathIndie + "mods"))
                                {
                                    string FileName = ModBase.GetFileNameFromPath(ModFile);
                                    if (FileName.EndsWithF(".jar", true) && FileName.ContainsF("jumploader", true))
                                    {
                                        ModBase.Log("[Minecraft] 发现 JumpLoader 分支项：" + FileName);
                                        HasJumpLoader = true;
                                        break;
                                    }
                                }
                            }
                            if (HasJumpLoader)
                            {
                                _JsonObject.Remove("jumploader");
                                _JsonObject.Add("jumploader", (JToken)ModBase.GetJson(ModBase.ReadFile(PathIndie + @"config\jumploader.json")));
                            }
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "处理 JumpLoader 失败");
                        }
                    }
                    return _JsonObject;
                }
                set
                {
                    _JsonObject = value;
                }
            }
            private JObject _JsonObject = null;
            /// <summary>
        /// 是否为旧版 Json 格式。
        /// </summary>
            public bool IsOldJson
            {
                get
                {
                    return JsonObject["minecraftArguments"] is not null && (string)JsonObject["minecraftArguments"] != "";
                }
            }
            /// <summary>
        /// Json 是否为 HMCL 格式。
        /// </summary>
            public bool IsHmclFormatJson { get; set; } = false;
            /// <summary>
        /// 是否包含 JumpLoader。
        /// </summary>
            public bool HasJumpLoader { get; set; } = false;

            /// <summary>
        /// 版本 jar 中的 version.json 文件对象。
        /// 若没有则返回 Nothing。
        /// </summary>
            public JObject JsonVersion
            {
                get
                {
                    if (!JsonVersionInited)
                    {
                        JsonVersionInited = true;
                        if (File.Exists(Path + Name + ".jar"))
                        {
                            try
                            {
                                using (var JarArchive = new ZipArchive(new FileStream(Path + Name + ".jar", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                                {
                                    var VersionJson = JarArchive.GetEntry("version.json");
                                    if (VersionJson is not null)
                                    {
                                        using (var VersionJsonStream = new StreamReader(VersionJson.Open()))
                                        {
                                            _JsonVersion = (JObject)ModBase.GetJson(VersionJsonStream.ReadToEnd());
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "从版本 jar 中读取 version.json 失败");
                            }
                        }
                    }
                    return _JsonVersion;
                }
            }
            private bool JsonVersionInited = false;
            private JObject _JsonVersion = null;

            /// <summary>
        /// 该版本的依赖版本。若无依赖版本则为空字符串。
        /// </summary>
            public string InheritVersion
            {
                get
                {
                    if (_InheritVersion is null)
                    {
                        _InheritVersion = (JsonObject["inheritsFrom"] ?? "").ToString();
                        // 由于过老的 LiteLoader 中没有 Inherits（例如 1.5.2），需要手动判断以获取真实继承版本
                        // 此外，由于这里的加载早于版本种类判断，所以需要手动判断是否为 LiteLoader
                        // 如果版本提供了不同的 Jar，代表所需的 Jar 可能已被更改，则跳过 Inherit 替换
                        if (JsonText.Contains("liteloader") && (Version.McName ?? "") != (Name ?? "") && !JsonText.Contains("logging"))
                        {
                            if (((JsonObject["jar"] ?? Version.McName).ToString() ?? "") == (Version.McName ?? ""))
                                _InheritVersion = Version.McName;
                        }
                        // HMCL 版本无 Json
                        if (IsHmclFormatJson)
                            _InheritVersion = "";
                    }
                    return _InheritVersion;
                }
            }
            private string _InheritVersion = null;

            /// <summary></summary>
        /// <param name="Path">版本名，或版本文件夹的完整路径（不规定是否以 \ 结尾）。</param>
            public McVersion(string Path)
            {
                this.Path = (Path.Contains(":") ? "" : PathMcFolder + @"versions\") + Path + (Path.EndsWithF(@"\") ? "" : @"\"); // 补全完整路径
                                                                                                                                 // 补全右划线
            }

            /// <summary>
        /// 检查 Minecraft 版本，若检查通过 State 则为 Original 且返回 True。
        /// </summary>
            public bool Check()
            {

                // 检查文件夹
                if (!Directory.Exists(Path))
                {
                    State = McVersionState.Error;
                    Info = "未找到版本 " + Name;
                    return false;
                }
                // 检查权限
                try
                {
                    Directory.CreateDirectory(Path + @"PCL\");
                    ModBase.CheckPermissionWithException(Path + @"PCL\");
                }
                catch (Exception ex)
                {
                    State = McVersionState.Error;
                    Info = "PCL 没有对该文件夹的访问权限，请右键以管理员身份运行 PCL";
                    ModBase.Log(ex, "没有访问版本文件夹的权限");
                    return false;
                }
                // 确认 Json 可用性
                try
                {
                    var JsonObjCheck = JsonObject;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "版本 JSON 可用性检查失败（" + Path + "）");
                    JsonText = "";
                    JsonObject = null;
                    Info = ex.Message;
                    State = McVersionState.Error;
                    return false;
                }
                // 检查依赖版本
                try
                {
                    if (!string.IsNullOrEmpty(InheritVersion))
                    {
                        if (!File.Exists(ModBase.GetPathFromFullPath(Path) + InheritVersion + @"\" + InheritVersion + ".json"))
                        {
                            State = McVersionState.Error;
                            Info = "需要安装 " + InheritVersion + " 作为前置版本";
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "依赖版本检查出错（" + Name + "）");
                    State = McVersionState.Error;
                    Info = "未知错误：" + ModBase.GetExceptionSummary(ex);
                    return false;
                }

                State = McVersionState.Original;
                return true;
            }
            /// <summary>
        /// 加载 Minecraft 版本的详细信息。不使用其缓存，且会更新缓存。
        /// </summary>
            public McVersion Load()
            {
                try
                {
                    // 检查版本，若出错则跳过数据确定阶段
                    if (!Check())
                        goto ExitDataLoad;
                    #region 确定版本分类
                    switch (Version.McName ?? "") // 在获取 Version.Original 对象时会完成它的加载
                    {
                        case "Unknown":
                            {
                                State = McVersionState.Error;
                                break;
                            }
                        case "Old":
                            {
                                State = McVersionState.Old; // 根据 API 进行筛选
                                break;
                            }

                        default:
                            {
                                string RealJson = JsonObject is not null?
                                    JsonObject.ToString():
                                    JsonText;
                                // 愚人节与快照版本
                                if ((JsonObject["type"] ?? "").ToString() == "fool" || !string.IsNullOrEmpty(GetMcFoolName(Version.McName)))
                                {
                                    State = McVersionState.Fool;
                                }
                                else if (Version.McName.ContainsF("w", true) || Name.ContainsF("combat", true) || Version.McName.ContainsF("rc", true) || Version.McName.ContainsF("pre", true) || Version.McName.ContainsF("experimental", true) || (JsonObject["type"] ?? "").ToString() == "snapshot" || (JsonObject["type"] ?? "").ToString() == "pending")
                                {
                                    State = McVersionState.Snapshot;
                                }
                                // OptiFine
                                if (RealJson.Contains("optifine"))
                                {
                                    State = McVersionState.OptiFine;
                                    Version.HasOptiFine = true;
                                    Version.OptiFineVersion = RealJson.RegexSeek("(?<=HD_U_)[^\":/]+") ?? "未知版本";
                                }
                                // LiteLoader
                                if (RealJson.Contains("liteloader"))
                                {
                                    State = McVersionState.LiteLoader;
                                    Version.HasLiteLoader = true;
                                }
                                // Fabric、Forge、Quilt
                                if (RealJson.Contains("net.fabricmc:fabric-loader"))
                                {
                                    State = McVersionState.Fabric;
                                    Version.HasFabric = true;
                                    Version.FabricVersion = (RealJson.RegexSeek(@"(?<=(net.fabricmc:fabric-loader:))[0-9\.]+(\+build.[0-9]+)?") ?? "未知版本").Replace("+build", "");
                                }
                                else if (RealJson.Contains("org.quiltmc:quilt-loader"))
                                {
                                    State = McVersionState.Quilt;
                                    Version.HasQuilt = true;
                                    Version.QuiltVersion = (RealJson.RegexSeek(@"(?<=(org.quiltmc:quilt-loader:))[0-9\.]+(\+build.[0-9]+)?((-beta.)[0-9]([0-9]?))") ?? "未知版本").Replace("+build", "");
                                }
                                else if (RealJson.Contains("com.cleanroommc:cleanroom:"))
                                {
                                    State = McVersionState.Cleanroom;
                                    Version.HasCleanroom = true;
                                    Version.CleanroomVersion = (RealJson.RegexSeek(@"(?<=(com.cleanroommc:cleanroom:))[0-9\.]+(\+build.[0-9]+)?(-alpha)?") ?? "未知版本").Replace("+build", "");
                                }
                                else if (RealJson.Contains("minecraftforge") && !RealJson.Contains("net.neoforge"))
                                {
                                    State = McVersionState.Forge;
                                    Version.HasForge = true;
                                    Version.ForgeVersion = RealJson.RegexSeek(@"(?<=forge:[0-9\.]+(_pre[0-9]*)?\-)[0-9\.]+");
                                    if (Version.ForgeVersion is null)
                                        Version.ForgeVersion = RealJson.RegexSeek(@"(?<=net\.minecraftforge:minecraftforge:)[0-9\.]+");
                                    if (Version.ForgeVersion is null)
                                        Version.ForgeVersion = RealJson.RegexSeek(@"(?<=net\.minecraftforge:fmlloader:[0-9\.]+-)[0-9\.]+") ?? "未知版本";
                                }
                                else if (RealJson.Contains("net.neoforge"))
                                {
                                    // 1.20.1 JSON 范例："--fml.forgeVersion", "47.1.99"
                                    // 1.20.2+ JSON 范例："--fml.neoForgeVersion", "20.6.119-beta"
                                    State = McVersionState.NeoForge;
                                    Version.HasNeoForge = true;
                                    Version.NeoForgeVersion = RealJson.RegexSeek("(?<=orgeVersion\",[^\"]*?\")[^\"]+(?=\",)") ?? "未知版本";
                                }
                                Version.IsApiLoaded = true;
                                break;
                            }
                    }
                #endregion
                ExitDataLoad:
                    ;

                    // 确定版本图标
                    Logo = ModBase.ReadIni(Path + @"PCL\Setup.ini", "Logo", "");
                    if (string.IsNullOrEmpty(Logo) || !Conversions.ToBoolean(ModBase.ReadIni(Path + @"PCL\Setup.ini", "LogoCustom", Conversions.ToString(false))))
                    {
                        switch (State)
                        {
                            case McVersionState.Original:
                                {
                                    Logo = ModBase.PathImage + "Blocks/Grass.png";
                                    break;
                                }
                            case McVersionState.Snapshot:
                                {
                                    Logo = ModBase.PathImage + "Blocks/CommandBlock.png";
                                    break;
                                }
                            case McVersionState.Old:
                                {
                                    Logo = ModBase.PathImage + "Blocks/CobbleStone.png";
                                    break;
                                }
                            case McVersionState.Forge:
                                {
                                    Logo = ModBase.PathImage + "Blocks/Anvil.png";
                                    break;
                                }
                            case McVersionState.NeoForge:
                                {
                                    Logo = ModBase.PathImage + "Blocks/NeoForge.png";
                                    break;
                                }
                            case McVersionState.Cleanroom:
                                {
                                    Logo = ModBase.PathImage + "Blocks/Cleanroom.png";
                                    break;
                                }
                            case McVersionState.Fabric:
                                {
                                    Logo = ModBase.PathImage + "Blocks/Fabric.png";
                                    break;
                                }
                            case McVersionState.Quilt:
                                {
                                    Logo = ModBase.PathImage + "Blocks/Quilt.png";
                                    break;
                                }
                            case McVersionState.OptiFine:
                                {
                                    Logo = ModBase.PathImage + "Blocks/GrassPath.png";
                                    break;
                                }
                            case McVersionState.LiteLoader:
                                {
                                    Logo = ModBase.PathImage + "Blocks/Egg.png";
                                    break;
                                }
                            case McVersionState.Fool:
                                {
                                    Logo = ModBase.PathImage + "Blocks/GoldBlock.png";
                                    break;
                                }

                            default:
                                {
                                    Logo = ModBase.PathImage + "Blocks/RedstoneBlock.png";
                                    break;
                                }
                        }
                    }
                    // 确定版本描述
                    string CustomInfo = ModBase.ReadIni(Path + @"PCL\Setup.ini", "CustomInfo");
                    Info = !string.IsNullOrEmpty(CustomInfo) ? CustomInfo : GetDefaultDescription();
                    // 确定版本收藏状态
                    IsStar = Conversions.ToBoolean(ModBase.ReadIni(Path + @"PCL\Setup.ini", "IsStar", Conversions.ToString(false)));
                    // 确定版本显示种类
                    DisplayType = (McVersionCardType)Conversions.ToInteger(ModBase.ReadIni(Path + @"PCL\Setup.ini", "DisplayType", ((int)McVersionCardType.Auto).ToString()));
                    // 写入缓存
                    if (Directory.Exists(Path))
                    {
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "State", ((int)State).ToString());
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "Info", Info);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "Logo", Logo);
                    }
                    if (State != McVersionState.Error)
                    {
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "ReleaseTime", ReleaseTime.ToString("yyyy'-'MM'-'dd HH':'mm"));
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionFabric", Version.FabricVersion);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionQuilt", Version.QuiltVersion);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionOptiFine", Version.OptiFineVersion);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionLiteLoader", Conversions.ToString(Version.HasLiteLoader));
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionForge", Version.ForgeVersion);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionNeoForge", Version.NeoForgeVersion);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionCleanroom", Version.CleanroomVersion);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionApiCode", Version.SortCode.ToString());
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionOriginal", Version.McName);
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionOriginalMain", Version.McCodeMain.ToString());
                        ModBase.WriteIni(Path + @"PCL\Setup.ini", "VersionOriginalSub", Version.McCodeSub.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Info = "未知错误：" + ModBase.GetExceptionSummary(ex);
                    Logo = ModBase.PathImage + "Blocks/RedstoneBlock.png";
                    State = McVersionState.Error;
                    ModBase.Log(ex, "加载版本失败（" + Name + "）", ModBase.LogLevel.Feedback);
                }
                finally
                {
                    IsLoaded = true;
                }
                return this;
            }
            /// <summary>
        /// 获取版本的默认描述。
        /// </summary>
            public string GetDefaultDescription()
            {
                string Info = "";
                switch (State)
                {
                    case McVersionState.Snapshot:
                        {
                            if (Version.McName.ContainsF("pre", true))
                            {
                                Info = "预发布版 " + Version.McName;
                            }
                            else if (Version.McName.ContainsF("rc", true))
                            {
                                Info = "发布候选 " + Version.McName;
                            }
                            else if (Version.McName.Contains("experimental") || Version.McName == "pending")
                            {
                                Info = "实验性快照";
                            }
                            else
                            {
                                Info = "快照 " + Version.McName;
                            }

                            break;
                        }
                    case McVersionState.Old:
                        {
                            Info = "远古版本";
                            break;
                        }
                    case McVersionState.Original:
                    case McVersionState.Forge:
                    case McVersionState.NeoForge:
                    case McVersionState.Fabric:
                    case McVersionState.Quilt:
                    case McVersionState.OptiFine:
                    case McVersionState.LiteLoader:
                    case McVersionState.Cleanroom:
                        {
                            Info = Version.ToString();
                            break;
                        }
                    case McVersionState.Fool:
                        {
                            Info = GetMcFoolName(Version.McName);
                            break;
                        }
                    case McVersionState.Error:
                        {
                            return this.Info; // 已有错误信息
                        }

                    default:
                        {
                            Info = "发生了未知错误，请向作者反馈此问题";
                            break;
                        }
                }
                if (!(State == McVersionState.Error))
                {
                    if (HasJumpLoader)
                        Info += ", JumpLoader";
                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: this), 3, false)))
                        Info += ", 统一通行证验证";
                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: this), 4, false)))
                        Info += ", Authlib 验证";
                }
                return Info;
            }

            public bool IsLoaded = false;

            public override bool Equals(object obj)
            {
                McVersion version = obj as McVersion;
                return version is not null && (Path ?? "") == (version.Path ?? "");
            }
            public static bool operator ==(McVersion a, McVersion b)
            {
                if (a is null && b is null)
                    return true;
                if (a is null || b is null)
                    return false;
                return (a.Path ?? "") == (b.Path ?? "");
            }
            public static bool operator !=(McVersion a, McVersion b)
            {
                return !(a == b);
            }

        }
        public enum McVersionState
        {
            Error,
            Original,
            Snapshot,
            Fool,
            OptiFine,
            Old,
            Forge,
            NeoForge,
            LiteLoader,
            Fabric,
            Quilt,
            Cleanroom
        }

        /// <summary>
    /// 某个 Minecraft 实例的版本名、附加组件信息。
    /// </summary>
        public class McVersionInfo
        {

            /// <summary>
        /// 版本的 API 信息是否已加载。
        /// </summary>
            public bool IsApiLoaded = false;

            // 原版

            /// <summary>
        /// 原版版本名。如 1.12.2，16w01a。
        /// </summary>
            public string McName;
            /// <summary>
        /// 原版主版本号，如 12（For 1.12.2），快照则固定为 99。不可用则为 -1。
        /// </summary>
            public int McCodeMain = -1;
            /// <summary>
        /// 原版次版本号，如 2（For 1.12.2），快照则固定为 99。不可用则为 -1。
        /// </summary>
            public int McCodeSub = -1;

            // OptiFine

            /// <summary>
        /// 该版本是否通过 Json 安装了 OptiFine。
        /// </summary>
            public bool HasOptiFine = false;
            /// <summary>
        /// OptiFine 版本号，如 C8、C9_pre10。
        /// </summary>
            public string OptiFineVersion = "";

            // Forge

            /// <summary>
        /// 该版本是否安装了 Forge。
        /// </summary>
            public bool HasForge = false;
            /// <summary>
        /// Forge 版本号，如 31.1.2、14.23.5.2847。
        /// </summary>
            public string ForgeVersion = "";

            // NeoForge

            /// <summary>
        /// 该版本是否安装了 NeoForge。
        /// </summary>
            public bool HasNeoForge = false;
            /// <summary>
        /// NeoForge 版本号，如 21.0.2-beta、47.1.79。
        /// </summary>
            public string NeoForgeVersion = "";

            // Cleanroom

            /// <summary>
        /// 该版本是否安装了 Cleanroom。
        /// </summary>
            public bool HasCleanroom = false;
            /// <summary>
        /// Cleanroom 版本号，如 0.2.4-alpha。
        /// </summary>
            public string CleanroomVersion = "";

            // Fabric

            /// <summary>
        /// 该版本是否安装了 Fabric。
        /// </summary>
            public bool HasFabric = false;
            /// <summary>
        /// Fabric 版本号，如 0.7.2.175。
        /// </summary>
            public string FabricVersion = "";

            // Quilt

            /// <summary>
        /// 该版本是否安装了 Quilt。
        /// </summary>
            public bool HasQuilt = false;
            /// <summary>
        /// Quilt 版本号，如 0.26.1-beta.1、0.26.0。
        /// </summary>
            public string QuiltVersion = "";

            // LiteLoader

            /// <summary>
        /// 该版本是否安装了 LiteLoader。
        /// </summary>
            public bool HasLiteLoader = false;

            // API

            /// <summary>
        /// 生成对此版本信息的用户友好的描述性字符串。
        /// </summary>
            public override string ToString()
            {
                string ToStringRet = default;
                ToStringRet = "";
                if (HasForge)
                    ToStringRet += ", Forge" + (ForgeVersion == "未知版本" ? "" : " " + ForgeVersion);
                if (HasNeoForge)
                    ToStringRet += ", NeoForge" + (NeoForgeVersion == "未知版本" ? "" : " " + NeoForgeVersion);
                if (HasCleanroom)
                    ToStringRet += ", Cleanroom" + (CleanroomVersion == "未知版本" ? "" : " " + CleanroomVersion);
                if (HasFabric)
                    ToStringRet += ", Fabric" + (FabricVersion == "未知版本" ? "" : " " + FabricVersion);
                if (HasQuilt)
                    ToStringRet += ", Quilt" + (QuiltVersion == "未知版本" ? "" : " " + QuiltVersion);
                if (HasOptiFine)
                    ToStringRet += ", OptiFine" + (OptiFineVersion == "未知版本" ? "" : " " + OptiFineVersion);
                if (HasLiteLoader)
                    ToStringRet += ", LiteLoader";
                if (string.IsNullOrEmpty(ToStringRet))
                {
                    return "原版 " + McName;
                }
                else
                {
                    return McName + ToStringRet + (ModBase.ModeDebug ? " (" + SortCode + "#)" : "");
                }
            }

            /// <summary>
        /// 用于排序比较的编号。
        /// </summary>
            public int SortCode
            {
                get
                {
                    if (_SortCode == -2)
                    {
                        // 初始化
                        try
                        {
                            if (HasFabric)
                            {
                                if (FabricVersion == "未知版本")
                                    return 0;
                                string[] SubVersions = FabricVersion.Split(".");
                                if (SubVersions.Length >= 3)
                                {
                                    _SortCode = (int)Math.Round(ModBase.Val(SubVersions[0]) * 10000d + ModBase.Val(SubVersions[1]) * 100d + ModBase.Val(SubVersions[2]));
                                }
                                else
                                {
                                    throw new Exception("无效的 Fabric 版本：" + FabricVersion);
                                }
                            }
                            else if (HasQuilt)
                            {
                                if (QuiltVersion == "未知版本")
                                    return 0;
                                bool IsBeta = QuiltVersion.Contains("-beta");
                                string[] SubVersions = QuiltVersion.Replace("-beta", "").Split(".");
                                if (SubVersions.Length >= 3)
                                {
                                    _SortCode = (int)Math.Round(ModBase.Val(SubVersions[0]) * 10000d + ModBase.Val(SubVersions[1]) * 100d + ModBase.Val(SubVersions[2]) + Conversions.ToDouble(IsBeta));
                                }
                                else
                                {
                                    throw new Exception("无效的 Quilt 版本：" + QuiltVersion);
                                }
                            }
                            else if (HasCleanroom)
                            {
                                if (CleanroomVersion == "未知版本")
                                    return 0;
                                bool IsAlpha = CleanroomVersion.Contains("-alpha");
                                string[] SubVersions = CleanroomVersion.Replace("-alpha", "").Split(".");
                                if (SubVersions.Length >= 3)
                                {
                                    _SortCode = (int)Math.Round(ModBase.Val(SubVersions[0]) * 10000d + ModBase.Val(SubVersions[1]) * 100d + ModBase.Val(SubVersions[2]) + Conversions.ToDouble(IsAlpha));
                                }
                                else
                                {
                                    throw new Exception("无效的 Cleanroom 版本：" + CleanroomVersion);
                                }
                            }
                            else if (HasForge || HasNeoForge)
                            {
                                if (ForgeVersion == "未知版本" && NeoForgeVersion == "未知版本")
                                    return 0;
                                string[] SubVersions = HasForge ? ForgeVersion.Split(".") : NeoForgeVersion.Split(".");
                                if (SubVersions.Length == 4)
                                {
                                    _SortCode = (int)Math.Round(ModBase.Val(SubVersions[0]) * 1000000d + ModBase.Val(SubVersions[1]) * 10000d + ModBase.Val(SubVersions[3]));
                                }
                                else if (SubVersions.Length == 3)
                                {
                                    _SortCode = (int)Math.Round(ModBase.Val(SubVersions[0]) * 1000000d + ModBase.Val(SubVersions[1]) * 10000d + ModBase.Val(SubVersions[2]));
                                }
                                else
                                {
                                    throw new Exception("无效的 Neo/Forge 版本：" + ForgeVersion);
                                }
                            }
                            else if (HasOptiFine)
                            {
                                if (OptiFineVersion == "未知版本")
                                    return 0;
                                // 由对应原版次级版本号（2 位）、字母（2 位）、末尾数字（2 位）、测试标记（2 位，正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）组成
                                _SortCode = (int)Math.Round((McCodeSub >= 0 ? McCodeSub : 0) * 1000000 + (Strings.Asc(Conversions.ToChar(Strings.Left(OptiFineVersion.ToUpper(), 1))) - Strings.Asc('A') + 1) * 10000 + ModBase.Val(Strings.Right(OptiFineVersion, OptiFineVersion.Length - 1).RegexSeek("[0-9]+")) * 100d);                    // 第一段：原版次级版本号（2 位）
                                                                                                                                                                                                                                                                                                                                                // 第二段：字母编号（2 位），如 G2 中的 G（7）
                                                                                                                                                                                                                                                                                                                                                // 第三段：末尾数字（2 位），如 C5 beta4 中的 5
                                                                                                                                                                                                                                                                                                                                                // 第三段：测试标记
                                if (OptiFineVersion.ContainsF("pre", true))
                                    _SortCode += 50;
                                if (OptiFineVersion.ContainsF("pre", true) || OptiFineVersion.ContainsF("beta", true))
                                {
                                    if (ModBase.Val(Strings.Right(OptiFineVersion, 1)) == 0d && Strings.Right(OptiFineVersion, 1) != "0")
                                    {
                                        _SortCode += 1; // 为 pre 或 beta 结尾，视作 1
                                    }
                                    else
                                    {
                                        _SortCode = (int)Math.Round(_SortCode + ModBase.Val(OptiFineVersion.ToLower().RegexSeek("(?<=((pre)|(beta)))[0-9]+")));
                                    }
                                }
                                else
                                {
                                    _SortCode += 99;
                                }
                            }
                            else
                            {
                                _SortCode = -1;
                            }
                        }
                        catch (Exception ex)
                        {
                            _SortCode = -1;
                            ModBase.Log(ex, "获取 API 版本信息失败：" + ToString());
                        }
                    }
                    return _SortCode;
                }
                set
                {
                    _SortCode = value;
                }
            }
            private int _SortCode = -2;

        }

        /// <summary>
    /// 根据版本名获取对应的愚人节版本描述。非愚人节版本会返回空字符串。
    /// </summary>
        public static string GetMcFoolName(string Name)
        {
            Name = Name.ToLower();
            if (Name.StartsWithF("2.0") || Name.StartsWithF("2point0"))
            {
                string Tag = "";
                if (Name.EndsWith("red"))
                {
                    Tag = "（红色版本）";
                }
                else if (Name.EndsWith("blue"))
                {
                    Tag = "（蓝色版本）";
                }
                else if (Name.EndsWith("purple"))
                {
                    Tag = "（紫色版本）";
                }
                return "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！" + Tag;
            }
            else if (Name == "15w14a")
            {
                return "2015 | 作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。";
            }
            else if (Name == "1.rv-pre1")
            {
                return "2016 | 是时候将现代科技带入 Minecraft 了！";
            }
            else if (Name == "3d shareware v1.34")
            {
                return "2019 | 我们从地下室的废墟里找到了这个开发于 1994 年的杰作！";
            }
            else if (Name.StartsWithF("20w14inf") || Name == "20w14∞")
            {
                return "2020 | 我们加入了 20 亿个新的维度，让无限的想象变成了现实！";
            }
            else if (Name == "22w13oneblockatatime")
            {
                return "2022 | 一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！";
            }
            else if (Name == "23w13a_or_b")
            {
                return "2023 | 研究表明：玩家喜欢作出选择——越多越好！";
            }
            else if (Name == "24w14potato")
            {
                return "2024 | 毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！";
            }
            else if (Name == "25w14craftmine")
            {
                return "2025 | 你可以合成任何东西——包括合成你的世界！";
            }
            else
            {
                return "";
            }
        }

        /// <summary>
    /// 当前按卡片分类的所有版本列表。
    /// </summary>
        public static Dictionary<McVersionCardType, List<McVersion>> McVersionList = new Dictionary<McVersionCardType, List<McVersion>>();

        #endregion

        #region 版本列表加载

        /// <summary>
    /// 是否要求本次加载强制刷新版本列表。
    /// </summary>
        public static bool McVersionListForceRefresh = false;
        /// <summary>
    /// 加载 Minecraft 文件夹的版本列表。
    /// </summary>
        public static ModLoader.LoaderTask<string, int> McVersionListLoader = new ModLoader.LoaderTask<string, int>("Minecraft Version List", McVersionListLoad) { ReloadTimeout = 1 };

        /// <summary>
    /// 是否为本次打开 PCL 后第一次加载版本列表。
    /// 这会清理所有 .pclignore 文件，而非跳过这些对应版本。
    /// </summary>
        private static bool IsFirstMcVersionListLoad = true;

        /// <summary>
    /// 开始加载当前 Minecraft 文件夹的版本列表。
    /// </summary>
        private static void McVersionListLoad(ModLoader.LoaderTask<string, int> Loader)
        {
            // 开始加载
            string Path = Loader.Input;
            try
            {
                // 初始化
                McVersionList = new Dictionary<McVersionCardType, List<McVersion>>();

                // 检测缓存是否需要更新
                var FolderList = new List<string>();
                if (Directory.Exists(Path + "versions")) // 不要使用 CheckPermission，会导致写入时间改变，从而使得文件夹被强制刷新
                {
                    try
                    {
                        foreach (DirectoryInfo Folder in new DirectoryInfo(Path + "versions").GetDirectories())
                            FolderList.Add(Folder.Name);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("无法读取版本文件夹，可能是由于没有权限（" + Path + "versions）", ex);
                    }
                }
                // 不可用
                if (!FolderList.Any())
                {
                    ModBase.WriteIni(Path + "PCL.ini", "VersionCache", ""); // 清空缓存
                    goto OnLoaded;
                }
                // 有可用版本
                int FolderListCheck = (int)Math.Round(ModBase.GetHash(McVersionCacheVersion + "#" + FolderList.ToArray().Join("#")) % (decimal)(int.MaxValue - 1)); // 根据文件夹名列表生成辨识码
                if (!McVersionListForceRefresh && ModBase.Val(ModBase.ReadIni(Path + "PCL.ini", "VersionCache")) == FolderListCheck)
                {
                    // 可以使用缓存
                    var Result = McVersionListLoadCache(Path);
                    if (Result is null)
                    {
                        goto Reload;
                    }
                    else
                    {
                        McVersionList = Result;
                    }
                }
                else
                {
                // 文件夹列表不符
                Reload:
                    ;

                    McVersionListForceRefresh = false;
                    ModBase.Log("[Minecraft] 文件夹列表变更，重载所有版本");
                    ModBase.WriteIni(Path + "PCL.ini", "VersionCache", FolderListCheck.ToString());
                    McVersionList = McVersionListLoadNoCache(Path);
                }
                IsFirstMcVersionListLoad = false;

            // 改变当前选择的版本
            OnLoaded:
                ;

                if (Loader.IsAborted)
                    return;
                if (McVersionList.Any(v => v.Key != McVersionCardType.Error))
                {
                    // 尝试读取已储存的选择
                    string SavedSelection = ModBase.ReadIni(Path + "PCL.ini", "Version");
                    if (!string.IsNullOrEmpty(SavedSelection))
                    {
                        foreach (KeyValuePair<McVersionCardType, List<McVersion>> Card in McVersionList)
                        {
                            foreach (McVersion Version in Card.Value)
                            {
                                if ((Version.Name ?? "") == (SavedSelection ?? "") && !(Version.State == McVersionState.Error))
                                {
                                    // 使用已储存的选择
                                    McVersionCurrent = Version;
                                    ModBase.Setup.Set("LaunchVersionSelect", McVersionCurrent.Name);
                                    ModBase.Log("[Minecraft] 选择该文件夹储存的 Minecraft 版本：" + McVersionCurrent.Path);
                                    return;
                                }
                            }
                        }
                    }
                    if (!(McVersionList.First().Value[0].State == McVersionState.Error))
                    {
                        // 自动选择第一项
                        McVersionCurrent = McVersionList.First().Value[0];
                        ModBase.Setup.Set("LaunchVersionSelect", McVersionCurrent.Name);
                        ModBase.Log("[Launch] 自动选择 Minecraft 版本：" + McVersionCurrent.Path);
                    }
                }
                else
                {
                    McVersionCurrent = null;
                    ModBase.Setup.Set("LaunchVersionSelect", "");
                    ModBase.Log("[Minecraft] 未找到可用 Minecraft 版本");
                }
                if (Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugDelay")))
                    Thread.Sleep(ModBase.RandomInteger(200, 3000));
            }
            catch (ThreadInterruptedException ex)
            {
            }
            catch (Exception ex)
            {
                ModBase.WriteIni(Path + "PCL.ini", "VersionCache", ""); // 要求下次重新加载
                ModBase.Log(ex, "加载 .minecraft 版本列表失败", ModBase.LogLevel.Feedback);
            }
        }

        // 获取版本列表
        private static Dictionary<McVersionCardType, List<McVersion>> McVersionListLoadCache(string Path)
        {
            var ResultVersionList = new Dictionary<McVersionCardType, List<McVersion>>();
            try
            {
                int CardCount = Conversions.ToInteger(ModBase.ReadIni(Path + "PCL.ini", "CardCount", (-1).ToString()));
                if (CardCount == -1)
                    return null;
                for (int i = 0, loopTo = CardCount - 1; i <= loopTo; i++)
                {
                    McVersionCardType CardType = (McVersionCardType)Conversions.ToInteger(ModBase.ReadIni(Path + "PCL.ini", "CardKey" + (i + 1), ":"));
                    var VersionList = new List<McVersion>();

                    // 循环读取版本
                    foreach (string Folder in ModBase.ReadIni(Path + "PCL.ini", "CardValue" + (i + 1), ":").Split(":"))
                    {
                        if (string.IsNullOrEmpty(Folder))
                            continue;
                        string VersionFolder = $@"{Path}versions\{Folder}\";
                        if (File.Exists(VersionFolder + ".pclignore"))
                        {
                            if (IsFirstMcVersionListLoad)
                            {
                                ModBase.Log("[Minecraft] 清理残留的忽略项目：" + VersionFolder); // #2781
                                File.Delete(VersionFolder + ".pclignore");
                            }
                            else
                            {
                                ModBase.Log("[Minecraft] 跳过要求忽略的项目：" + VersionFolder);
                                continue;
                            }
                        }
                        try
                        {

                            // 读取单个版本
                            var Version = new McVersion(VersionFolder);
                            VersionList.Add(Version);
                            Version.Info = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "CustomInfo", "");
                            if (string.IsNullOrEmpty(Version.Info))
                                Version.Info = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "Info", Version.Info);
                            Version.Logo = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "Logo", Version.Logo);
                            Version.ReleaseTime = Conversions.ToDate(ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "ReleaseTime", Conversions.ToString(Version.ReleaseTime)));
                            Version.State = (McVersionState)Conversions.ToInteger(ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "State", ((int)Version.State).ToString()));
                            Version.IsStar = Conversions.ToBoolean(ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "IsStar", Conversions.ToString(false)));
                            Version.DisplayType = (McVersionCardType)Conversions.ToInteger(ModBase.ReadIni(Path + @"PCL\Setup.ini", "DisplayType", ((int)McVersionCardType.Auto).ToString()));
                            if (Version.State != McVersionState.Error && ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionOriginal", "Unknown") != "Unknown") // 旧版本可能没有这一项，导致 Version 不加载（#643）
                            {
                                var VersionInfo = new McVersionInfo()
                                {
                                    FabricVersion = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionFabric", ""),
                                    QuiltVersion = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionQuilt", ""),
                                    ForgeVersion = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionForge", ""),
                                    NeoForgeVersion = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionNeoForge", ""),
                                    OptiFineVersion = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionOptiFine", ""),
                                    HasLiteLoader = Conversions.ToBoolean(ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionLiteLoader", Conversions.ToString(false))),
                                    SortCode = Conversions.ToInteger(ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionApiCode", (-1).ToString())),
                                    McName = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionOriginal", "Unknown"),
                                    McCodeMain = Conversions.ToInteger(ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionOriginalMain", (-1).ToString())),
                                    McCodeSub = Conversions.ToInteger(ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "VersionOriginalSub", (-1).ToString())),
                                    IsApiLoaded = true
                                };
                                VersionInfo.HasFabric = VersionInfo.FabricVersion.Any();
                                VersionInfo.HasQuilt = VersionInfo.QuiltVersion.Any();
                                VersionInfo.HasForge = VersionInfo.ForgeVersion.Any();
                                VersionInfo.HasNeoForge = VersionInfo.NeoForgeVersion.Any();
                                VersionInfo.HasOptiFine = VersionInfo.OptiFineVersion.Any();
                                Version.Version = VersionInfo;
                            }

                            // 重新检查错误版本
                            if (Version.State == McVersionState.Error)
                            {
                                // 重新获取版本错误信息
                                string OldDesc = Version.Info;
                                Version.State = McVersionState.Original;
                                Version.Check();
                                // 校验错误原因是否改变
                                string CustomInfo = ModBase.ReadIni(Version.Path + @"PCL\Setup.ini", "CustomInfo");
                                if (Version.State == McVersionState.Original || string.IsNullOrEmpty(CustomInfo) && !((OldDesc ?? "") == (Version.Info ?? "")))
                                {
                                    ModBase.Log("[Minecraft] 版本 " + Version.Name + " 的错误状态已变更，新的状态为：" + Version.Info);
                                    return null;
                                }
                            }

                            // 校验未加载的版本
                            if (string.IsNullOrEmpty(Version.Logo))
                            {
                                ModBase.Log("[Minecraft] 版本 " + Version.Name + " 未被加载");
                                return null;
                            }
                        }

                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "读取版本加载缓存失败（" + Folder + "）", ModBase.LogLevel.Debug);
                            return null;
                        }
                    }

                    if (VersionList.Any())
                        ResultVersionList.Add(CardType, VersionList);
                }
                return ResultVersionList;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取版本缓存失败");
                return null;
            }
        }
        private static Dictionary<McVersionCardType, List<McVersion>> McVersionListLoadNoCache(string Path)
        {
            var VersionList = new List<McVersion>();

            #region 循环加载每个版本的信息
            foreach (DirectoryInfo Folder in new DirectoryInfo(Path + "versions").GetDirectories())
            {
                if (!Folder.Exists || !Folder.EnumerateFiles().Any())
                {
                    ModBase.Log("[Minecraft] 跳过空文件夹：" + Folder.FullName);
                    continue;
                }
                if ((Folder.Name == "cache" || Folder.Name == "BLClient" || Folder.Name == "PCL") && !File.Exists(Folder.FullName + @"\" + Folder.Name + ".json"))
                {
                    ModBase.Log("[Minecraft] 跳过可能不是版本文件夹的项目：" + Folder.FullName);
                    continue;
                }
                string VersionFolder = Folder.FullName + @"\";
                if (File.Exists(VersionFolder + ".pclignore"))
                {
                    if (IsFirstMcVersionListLoad)
                    {
                        ModBase.Log("[Minecraft] 清理残留的忽略项目：" + VersionFolder); // #2781
                        File.Delete(VersionFolder + ".pclignore");
                    }
                    else
                    {
                        ModBase.Log("[Minecraft] 跳过要求忽略的项目：" + VersionFolder);
                        continue;
                    }
                }
                var Version = new McVersion(VersionFolder);
                VersionList.Add(Version);
                Version.Load();
            }
            #endregion

            var ResultVersionList = new Dictionary<McVersionCardType, List<McVersion>>();

            #region 将版本分类到各个卡片
            try
            {

                // 未经过自定义的版本列表
                var VersionListOriginal = new Dictionary<McVersionCardType, List<McVersion>>();

                // 单独列出收藏的版本
                var StaredVersions = new List<McVersion>();
                foreach (McVersion Version in VersionList.ToList())
                {
                    if (Version.IsStar && !(Version.DisplayType == McVersionCardType.Hidden))
                    {
                        StaredVersions.Add(Version);
                        VersionList.Remove(Version);
                    }
                }
                if (StaredVersions.Any())
                    VersionListOriginal.Add(McVersionCardType.Star, StaredVersions);

                // 预先筛选出愚人节和错误的版本
                McVersionFilter(ref VersionList, ref VersionListOriginal, new[] { McVersionState.Error }, McVersionCardType.Error);
                McVersionFilter(ref VersionList, ref VersionListOriginal, new[] { McVersionState.Fool }, McVersionCardType.Fool);

                // 筛选 API 版本
                McVersionFilter(ref VersionList, ref VersionListOriginal, new[] { McVersionState.Forge, McVersionState.NeoForge, McVersionState.LiteLoader, McVersionState.Fabric, McVersionState.Quilt, McVersionState.Cleanroom }, McVersionCardType.API);

                // 将老版本预先分类入不常用，只剩余原版、快照、OptiFine
                var VersionUseful = new List<McVersion>();
                var VersionRubbish = new List<McVersion>();
                McVersionFilter(ref VersionList, new[] { McVersionState.Old }, ref VersionRubbish);

                // 确认最新版本，若为快照则加入常用列表
                McVersion LargestVersion = null; // 最新的版本
                foreach (McVersion Version in VersionList)
                {
                    if (Version.State == McVersionState.Original || Version.State == McVersionState.Snapshot)
                    {
                        if (LargestVersion is null || Version.ReleaseTime > LargestVersion.ReleaseTime)
                            LargestVersion = Version;
                    }
                }
                if (LargestVersion is not null && LargestVersion.State == McVersionState.Snapshot)
                {
                    VersionUseful.Add(LargestVersion);
                    VersionList.Remove(LargestVersion);
                }

                // 将剩余的快照全部拖进不常用列表
                McVersionFilter(ref VersionList, new[] { McVersionState.Snapshot }, ref VersionRubbish);

                // 获取每个大版本下最新的原版与 OptiFine
                var NewerVersion = new Dictionary<string, McVersion>();
                var ExistVersion = new List<int>();
                foreach (McVersion Version in VersionList)
                {
                    if (Version.Version.McCodeMain < 2)
                        continue; // 未获取成功的版本
                    if (!ExistVersion.Contains(Version.Version.McCodeMain))
                        ExistVersion.Add(Version.Version.McCodeMain);
                    if (NewerVersion.ContainsKey(Version.Version.McCodeMain + "-" + ((int)Version.State).ToString()))
                    {
                        if (Version.Version.HasOptiFine)
                        {
                            // OptiFine 根据排序识别号判断
                            if (Version.Version.SortCode > NewerVersion[Version.Version.McCodeMain + "-" + ((int)Version.State).ToString()].Version.SortCode)
                                NewerVersion[Version.Version.McCodeMain + "-" + ((int)Version.State).ToString()] = Version;
                        }
                        // 原版根据发布时间判断
                        else if (Version.ReleaseTime > NewerVersion[Version.Version.McCodeMain + "-" + ((int)Version.State).ToString()].ReleaseTime)
                            NewerVersion[Version.Version.McCodeMain + "-" + ((int)Version.State).ToString()] = Version;
                    }
                    else
                    {
                        NewerVersion.Add(Version.Version.McCodeMain + "-" + ((int)Version.State).ToString(), Version);
                    }
                }

                // 将每个大版本下的最常规版本加入
                foreach (int Code in ExistVersion)
                {
                    if (NewerVersion.ContainsKey(Code + "-" + ((int)McVersionState.OptiFine).ToString()) && NewerVersion.ContainsKey(Code + "-" + ((int)McVersionState.Original).ToString()))
                    {
                        // 同时存在 OptiFine 与原版
                        var OriginalVersion = NewerVersion[Code + "-" + ((int)McVersionState.Original).ToString()];
                        var OptiFineVersion = NewerVersion[Code + "-" + ((int)McVersionState.OptiFine).ToString()];
                        if (OriginalVersion.Version.McCodeSub > OptiFineVersion.Version.McCodeSub)
                        {
                            // 仅在原版比 OptiFine 更新时才加入原版
                            VersionUseful.Add(OriginalVersion);
                            VersionList.Remove(OriginalVersion);
                        }
                        VersionUseful.Add(OptiFineVersion);
                        VersionList.Remove(OptiFineVersion);
                    }
                    else if (NewerVersion.ContainsKey(Code + "-" + ((int)McVersionState.OptiFine).ToString()))
                    {
                        // 没有原版，直接加入 OptiFine
                        VersionUseful.Add(NewerVersion[Code + "-" + ((int)McVersionState.OptiFine).ToString()]);
                        VersionList.Remove(NewerVersion[Code + "-" + ((int)McVersionState.OptiFine).ToString()]);
                    }
                    else if (NewerVersion.ContainsKey(Code + "-" + ((int)McVersionState.Original).ToString()))
                    {
                        // 没有 OptiFine，直接加入原版
                        VersionUseful.Add(NewerVersion[Code + "-" + ((int)McVersionState.Original).ToString()]);
                        VersionList.Remove(NewerVersion[Code + "-" + ((int)McVersionState.Original).ToString()]);
                    }
                }

                // 将剩余的东西添加进去
                VersionRubbish.AddRange(VersionList);
                if (VersionUseful.Any())
                    VersionListOriginal.Add(McVersionCardType.OriginalLike, VersionUseful);
                if (VersionRubbish.Any())
                    VersionListOriginal.Add(McVersionCardType.Rubbish, VersionRubbish);

                // 按照自定义版本分类重新添加
                foreach (var VersionPair in VersionListOriginal)
                {
                    foreach (McVersion Version in VersionPair.Value)
                    {
                        var RealType = Version.DisplayType == 0 || VersionPair.Key == McVersionCardType.Star ? VersionPair.Key : Version.DisplayType;
                        if (!ResultVersionList.ContainsKey(RealType))
                            ResultVersionList.Add(RealType, new List<McVersion>());
                        ResultVersionList[RealType].Add(Version);
                    }
                }
            }

            catch (Exception ex)
            {
                ResultVersionList.Clear();
                ModBase.Log(ex, "分类版本列表失败", ModBase.LogLevel.Feedback);
            }
            #endregion
            #region 对卡片与版本进行排序

            // 对卡片进行整体排序
            var SortedVersionList = new Dictionary<McVersionCardType, List<McVersion>>();
            foreach (string SortRule in new[] { McVersionCardType.Star, McVersionCardType.API, McVersionCardType.OriginalLike, McVersionCardType.Rubbish, McVersionCardType.Fool, McVersionCardType.Error, McVersionCardType.Hidden })
            {
                if (ResultVersionList.ContainsKey((McVersionCardType)Conversions.ToInteger(SortRule)))
                    SortedVersionList.Add((McVersionCardType)Conversions.ToInteger(SortRule), ResultVersionList[(McVersionCardType)Conversions.ToInteger(SortRule)]);
            }
            ResultVersionList = SortedVersionList;

            // 常规版本：快照放在最上面，此后按版本号从高到低排序
            if (ResultVersionList.ContainsKey(McVersionCardType.OriginalLike))
            {
                var OldList = ResultVersionList[McVersionCardType.OriginalLike];
                // 提取快照
                McVersion Snapshot = null;
                foreach (McVersion Version in OldList)
                {
                    if (Version.State == McVersionState.Snapshot)
                    {
                        Snapshot = Version;
                        break;
                    }
                }
                if (!(Snapshot == null))
                    OldList.Remove(Snapshot);
                // 按版本号排序
                var NewList = OldList.OrderByDescending(v => v.Version.McCodeMain).ToList();
                // 回设
                if (!(Snapshot == null))
                    NewList.Insert(0, Snapshot);
                ResultVersionList[McVersionCardType.OriginalLike] = NewList;
            }

            // 不常用版本：按发布时间新旧排序，如果不可用则按名称排序
            if (ResultVersionList.ContainsKey(McVersionCardType.Rubbish))
            {
                ResultVersionList[McVersionCardType.Rubbish] = ResultVersionList[McVersionCardType.Rubbish].Sort((Left, Right) =>
        {
            int LeftYear = Left.ReleaseTime.Year; // + If(Left.State = McVersionState.Original OrElse Left.Version.HasOptiFine, 100, 0)
            int RightYear = Right.ReleaseTime.Year; // + If(Right.State = McVersionState.Original OrElse Left.Version.HasOptiFine, 100, 0)
            if (LeftYear > 2000 && RightYear > 2000)
            {
                if (LeftYear != RightYear)
                {
                    return LeftYear > RightYear;
                }
                else
                {
                    return Left.ReleaseTime > Right.ReleaseTime;
                }
            }
            else if (LeftYear > 2000 && RightYear < 2000)
            {
                return true;
            }
            else if (LeftYear < 2000 && RightYear > 2000)
            {
                return false;
            }
            else
            {
                return Operators.CompareString(Left.Name, Right.Name, false) > 0;
            }
        });
            }

            // API 版本：优先按版本排序，此后【先放 Fabric / Quilt，再放 Neo/Forge（按版本号从高到低排序），最后放 LiteLoader（按名称排序）】
            if (ResultVersionList.ContainsKey(McVersionCardType.API))
            {
                ResultVersionList[McVersionCardType.API] = ResultVersionList[McVersionCardType.API].Sort((Left, Right) =>
        {
            int Basic = VersionSortInteger(Left.Version.McName, Right.Version.McName);
            if (Basic != 0)
            {
                return Basic > 0;
            }
            else if (Left.Version.HasFabric ^ Right.Version.HasFabric)
            {
                return Left.Version.HasFabric;
            }
            else if (Left.Version.HasQuilt ^ Right.Version.HasQuilt)
            {
                return Left.Version.HasQuilt;
            }
            else if (Left.Version.HasNeoForge ^ Right.Version.HasNeoForge)
            {
                return Left.Version.HasNeoForge;
            }
            else if (Left.Version.HasForge ^ Right.Version.HasForge)
            {
                return Left.Version.HasForge;
            }
            else if (!(Left.Version.SortCode != Right.Version.SortCode))
            {
                return Left.Version.SortCode > Right.Version.SortCode;
            }
            else
            {
                return Operators.CompareString(Left.Name, Right.Name, false) > 0;
            }
        });
            }

            #endregion
            #region 保存卡片缓存
            ModBase.WriteIni(Path + "PCL.ini", "CardCount", ResultVersionList.Count.ToString());
            for (int i = 0, loopTo = ResultVersionList.Count - 1; i <= loopTo; i++)
            {
                ModBase.WriteIni(Path + "PCL.ini", "CardKey" + (i + 1), ((int)ResultVersionList.Keys.ElementAtOrDefault(i)).ToString());
                string Value = "";
                foreach (McVersion Version in ResultVersionList.Values.ElementAtOrDefault(i))
                    Value += Version.Name + ":";
                ModBase.WriteIni(Path + "PCL.ini", "CardValue" + (i + 1), Value);
            }
            #endregion
            return ResultVersionList;
        }
        /// <summary>
    /// 筛选特定种类的版本，并直接添加为卡片。
    /// </summary>
    /// <param name="VersionList">用于筛选的列表。</param>
    /// <param name="Formula">需要筛选出的版本类型。-2 代表隐藏的版本。</param>
    /// <param name="CardType">卡片的名称。</param>
        private static void McVersionFilter(ref List<McVersion> VersionList, ref Dictionary<McVersionCardType, List<McVersion>> Target, McVersionState[] Formula, McVersionCardType CardType)
        {
            var KeepList = VersionList.Where(v => Formula.Contains(v.State)).ToList();
            // 加入版本列表，并从剩余中删除
            if (KeepList.Any())
            {
                Target.Add(CardType, KeepList);
                foreach (McVersion Version in KeepList)
                    VersionList.Remove(Version);
            }
        }
        /// <summary>
    /// 筛选特定种类的版本，并增加入一个已有列表中。
    /// </summary>
    /// <param name="VersionList">用于筛选的列表。</param>
    /// <param name="Formula">需要筛选出的版本类型。-2 代表隐藏的版本。</param>
    /// <param name="KeepList">传入需要增加入的列表。</param>
        private static void McVersionFilter(ref List<McVersion> VersionList, McVersionState[] Formula, ref List<McVersion> KeepList)
        {
            KeepList.AddRange(VersionList.Where(v => Formula.Contains(v.State)));
            // 加入版本列表，并从剩余中删除
            if (KeepList.Any())
            {
                foreach (McVersion Version in KeepList)
                    VersionList.Remove(Version);
            }
        }
        public enum McVersionCardType
        {
            Star = -1,
            Auto = 0, // 仅用于强制版本分类的自动
            Hidden = 1,
            API = 2,
            OriginalLike = 3,
            Rubbish = 4,
            Fool = 5,
            Error = 6
        }

        #endregion

        #region 皮肤

        public struct McSkinInfo
        {
            public bool IsSlim;
            public string LocalFile;
            public bool IsVaild;
        }
        /// <summary>
    /// 要求玩家选择一个皮肤文件，并进行相关校验。
    /// </summary>
        public static McSkinInfo McSkinSelect()
        {
            string FileName = ModBase.SelectFile("皮肤文件(*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp", "选择皮肤文件");

            // 验证有效性
            if (string.IsNullOrEmpty(FileName))
                return new McSkinInfo() { IsVaild = false };
            try
            {
                var Image = new MyBitmap(FileName);
                if (Image.Pic.Width != 64 || !(Image.Pic.Height == 32 || Image.Pic.Height == 64))
                {
                    ModMain.Hint("皮肤图片大小应为 64x32 像素或 64x64 像素！", ModMain.HintType.Critical);
                    return new McSkinInfo() { IsVaild = false };
                }
                var FileInfo = new FileInfo(FileName);
                if (FileInfo.Length > 24 * 1024)
                {
                    ModMain.Hint("皮肤文件大小需小于 24 KB，而所选文件大小为 " + Math.Round(FileInfo.Length / 1024d, 2) + " KB", ModMain.HintType.Critical);
                    return new McSkinInfo() { IsVaild = false };
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "皮肤文件存在错误", ModBase.LogLevel.Hint);
                return new McSkinInfo() { IsVaild = false };
            }

            // 获取皮肤种类
            int IsSlim = ModMain.MyMsgBox("此皮肤为 Steve 模型（粗手臂）还是 Alex 模型（细手臂）？", "选择皮肤种类", "Steve 模型", "Alex 模型", "我不知道", HighLight: false);
            if (IsSlim == 3)
            {
                ModMain.Hint("请在皮肤下载页面确认皮肤种类后再使用此皮肤！");
                return new McSkinInfo() { IsVaild = false };
            }

            return new McSkinInfo() { IsVaild = true, IsSlim = IsSlim == 2, LocalFile = FileName };
        }

        /// <summary>
    /// 获取 Uuid 对应的皮肤文件地址，失败将抛出异常。
    /// </summary>
        public static string McSkinGetAddress(string Uuid, string Type)
        {
            if (string.IsNullOrEmpty(Uuid))
                throw new Exception("Uuid 为空。");
            if (Uuid.StartsWithF("00000"))
                throw new Exception("离线 Uuid 无正版皮肤文件。");
            // 尝试读取缓存
            string CacheSkinAddress = ModBase.ReadIni(ModBase.PathTemp + @"Cache\Skin\Index" + Type + ".ini", Uuid);
            if (!string.IsNullOrEmpty(CacheSkinAddress))
                return CacheSkinAddress;
            // 获取皮肤地址
            string Url;
            switch (Type ?? "")
            {
                case "Mojang":
                case "Ms":
                    {
                        Url = "https://sessionserver.mojang.com/session/minecraft/profile/";
                        break;
                    }
                case "Nide":
                    {
                        Url = Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject("https://auth.mc-user.com:233/", McVersionCurrent is null ? ModBase.Setup.Get("CacheNideServer") : ModBase.Setup.Get("VersionServerNide", Version: McVersionCurrent)), "/sessionserver/session/minecraft/profile/"));
                        break;
                    }
                case "Auth":
                    {
                        Url = Conversions.ToString(Operators.ConcatenateObject(McVersionCurrent is null ? ModBase.Setup.Get("CacheAuthServerServer") : ModBase.Setup.Get("VersionServerAuthServer", Version: McVersionCurrent), "/sessionserver/session/minecraft/profile/"));
                        break;
                    }

                default:
                    {
                        throw new ArgumentException("皮肤地址种类无效：" + (Type ?? "null"));
                    }
            }
            var SkinString = ModNet.NetGetCodeByRequestRetry(Url + Uuid);
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(SkinString, "", false)))
                throw new Exception("皮肤返回值为空，可能是未设置自定义皮肤的用户");
            // 处理皮肤地址
            string SkinValue;
            do
            {
                try
                {
                    bool exitTry = false;
                    foreach (var SkinProperty in (IEnumerable)ModBase.GetJson(Conversions.ToString(SkinString))("properties"))
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(SkinProperty("name"), "textures", false)))
                        {
                            SkinValue = Conversions.ToString(SkinProperty("value"));
                            exitTry = true;
                            break;
                        }
                    }

                    if (exitTry)
                    {
                        break;
                    }
                    throw new Exception("未从皮肤返回值中找到符合条件的 Property");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, Conversions.ToString(Operators.ConcatenateObject("无法完成解析的皮肤返回值，可能是未设置自定义皮肤的用户：", SkinString)), ModBase.LogLevel.Developer);
                    throw new Exception("皮肤返回值中不包含皮肤数据项，可能是未设置自定义皮肤的用户", ex);
                }
            }
            while (false);
            SkinString = Encoding.GetEncoding("utf-8").GetString(Convert.FromBase64String(SkinValue));
            JObject SkinJson = (JObject)ModBase.GetJson(Conversions.ToString(((dynamic)SkinString).ToLower));
            if (SkinJson["textures"] is null || SkinJson["textures"]["skin"] is null || SkinJson["textures"]["skin"]["url"] is null)
            {
                throw new Exception("用户未设置自定义皮肤");
            }
            else
            {
                SkinValue = SkinJson["textures"]["skin"]["url"].ToString();
            }
            // 保存缓存
            ModBase.WriteIni(ModBase.PathTemp + @"Cache\Skin\Index" + Type + ".ini", Uuid, SkinValue);
            ModBase.Log("[Skin] UUID " + Uuid + " 对应的皮肤文件为 " + SkinValue);
            return SkinValue;
        }

        private readonly static object McSkinDownloadLock = new object();
        /// <summary>
    /// 从 Url 下载皮肤。返回本地文件路径，失败将抛出异常。
    /// </summary>
        public static string McSkinDownload(string Address)
        {
            string SkinName = ModBase.GetFileNameFromPath(Address);
            string FileAddress = ModBase.PathTemp + @"Cache\Skin\" + ModBase.GetHash(Address) + ".png";
            lock (McSkinDownloadLock)
            {
                if (!File.Exists(FileAddress))
                {
                    ModNet.NetDownloadByClient(Address, FileAddress + ModNet.NetDownloadEnd);
                    File.Delete(FileAddress);
                    FileSystem.Rename(FileAddress + ModNet.NetDownloadEnd, FileAddress);
                    ModBase.Log("[Minecraft] 皮肤下载成功：" + FileAddress);
                }
                return FileAddress;
            }
        }

        /// <summary>
    /// 获取 Uuid 对应的皮肤，返回“Steve”或“Alex”。
    /// </summary>
        public static string McSkinSex(string Uuid)
        {
            if (!(Uuid.Length == 32))
                return "Steve";
            int a = int.Parse(Conversions.ToString(Uuid[7]), System.Globalization.NumberStyles.AllowHexSpecifier);
            int b = int.Parse(Conversions.ToString(Uuid[15]), System.Globalization.NumberStyles.AllowHexSpecifier);
            int c = int.Parse(Conversions.ToString(Uuid[23]), System.Globalization.NumberStyles.AllowHexSpecifier);
            int d = int.Parse(Conversions.ToString(Uuid[31]), System.Globalization.NumberStyles.AllowHexSpecifier);
            return Conversions.ToBoolean((a ^ b ^ c ^ d) % 2) ? "Alex" : "Steve";
            // Math.floorMod(uuid.hashCode(), 18)

            // Public Function hashCode(ByVal str As String) As Integer
            // Dim hash As Integer = 0
            // Dim n As Integer = str.Length
            // If n = 0 Then
            // Return hash
            // End If
            // For i As Integer = 0 To n - 1
            // hash = hash + Asc(str(i)) * (1 << (n - i - 1))
            // Next
            // Return hash
            // End Function
        }

        #endregion

        #region 支持库文件（Libraries）

        public class McLibToken
        {
            /// <summary>
        /// 文件的完整本地路径。
        /// </summary>
            public string LocalPath;
            /// <summary>
        /// 文件大小。若无有效数据即为 0。
        /// </summary>
            public long Size = 0L;
            /// <summary>
        /// 是否为 Natives 文件。
        /// </summary>
            public bool IsNatives = false;
            /// <summary>
        /// 文件的 SHA1。
        /// </summary>
            public string SHA1 = null;
            /// <summary>
        /// 由 Json 提供的 URL，若没有则为 Nothing。
        /// </summary>
            public string Url
            {
                get
                {
                    return _Url;
                }
                set
                {
                    // 孤儿 Forge 作者喜欢把没有 URL 的写个空字符串
                    _Url = string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            private string _Url;
            /// <summary>
        /// 原 Json 中 Name 项除去版本号部分的较前部分。可能为 Nothing。
        /// </summary>
            public string Name
            {
                get
                {
                    if (OriginalName is null)
                        return null;
                    var Splited = new List<string>(OriginalName.Split(":"));
                    Splited.RemoveAt(2); // Java 的此格式下版本号固定为第三段，第四段可能包含架构、分包等其他信息
                    return Splited.Join(":");
                }
            }
            /// <summary>
        /// 原 Json 中的 Name 项。
        /// </summary>
            public string OriginalName;
            /// <summary>
        /// 是否为 JumpLoader 项。
        /// </summary>
            public bool IsJumpLoader = false;

            public override string ToString()
            {
                return (IsNatives ? "[Native] " : "") + ModBase.GetString(Size) + " | " + LocalPath;
            }
        }

        /// <summary>
    /// 检查是否符合 Json 中的 Rules。
    /// </summary>
    /// <param name="RuleToken">Json 中的 "rules" 项目。</param>
        public static bool McJsonRuleCheck(JToken RuleToken)
        {
            if (RuleToken is null)
                return true;

            // 初始化
            bool Required = false;
            foreach (JToken Rule in RuleToken)
            {

                // 单条条件验证
                bool IsRightRule = true; // 是否为正确的规则
                if (Rule["os"] is not null) // 操作系统
                {
                    if (Rule["os"]["name"] is not null) // 操作系统名称
                    {
                        string OsName = Rule["os"]["name"].ToString();
                        if (OsName == "unknown")
                        {
                        }
                        else if (OsName == "windows")
                        {
                            if (Rule["os"]["version"] is not null) // 操作系统版本
                            {
                                string Cr = Rule["os"]["version"].ToString();
                                IsRightRule = IsRightRule && OSVersion.RegexCheck(Cr);
                            }
                        }
                        else
                        {
                            IsRightRule = false;
                        }
                    }
                    if (Rule["os"]["arch"] is not null) // 操作系统架构
                    {
                        IsRightRule = IsRightRule && Rule["os"]["arch"].ToString() == "x86" == ModBase.Is32BitSystem;
                    }
                }
                if (!(Rule["features"] == null)) // 标签
                {
                    IsRightRule = IsRightRule && Rule["features"]["is_demo_user"] == null; // 反选是否为 Demo 用户
                    if (((JObject)Rule["features"]).Children().Any((JProperty j) => j.Name.Contains("quick_play")))
                    {
                        IsRightRule = false; // 不开 Quick Play，让玩家自己加去
                    }
                }

                // 反选确认
                if (Rule["action"].ToString() == "allow")
                {
                    if (IsRightRule)
                        Required = true; // allow
                }
                else if (IsRightRule)
                    Required = false; // disallow

            }
            return Required;
        }
        private static string OSVersion = Environment.OSVersion.Version.ToString();

        /// <summary>
    /// 递归获取 Minecraft 某一版本的完整支持库列表。
    /// </summary>
        public static List<McLibToken> McLibListGet(McVersion Version, bool IncludeVersionJar)
        {
            List<McLibToken> McLibListGetRet = default;

            // 获取当前支持库列表
            ModBase.Log("[Minecraft] 获取支持库列表：" + Version.Name);
            McLibListGetRet = McLibListGetWithJson(Version.JsonObject, JumpLoaderFolder: Version.PathIndie + @".jumploader\");

            // 需要添加原版 Jar
            if (IncludeVersionJar)
            {
                McVersion RealVersion;
                string RequiredJar = Version.JsonObject["jar"]?.ToString();
                if (Version.IsHmclFormatJson || RequiredJar is null)
                {
                    // HMCL 项直接使用自身的 Jar
                    // 根据 Inherit 获取最深层版本
                    var OriginalVersion = Version;
                    // 1.17+ 的 Forge 不寻找 Inherit
                    if (!((Version.Version.HasForge || Version.Version.HasNeoForge) && Version.Version.McCodeMain >= 17))
                    {
                        while (!string.IsNullOrEmpty(OriginalVersion.InheritVersion))
                        {
                            if ((OriginalVersion.InheritVersion ?? "") == (OriginalVersion.Name ?? ""))
                                break;
                            OriginalVersion = new McVersion(PathMcFolder + @"versions\" + OriginalVersion.InheritVersion + @"\");
                        }
                    }
                    // 需要新建对象，否则后面的 Check 会导致 McVersionCurrent 的 State 变回 Original
                    // 复现：启动一个 Snapshot 版本
                    RealVersion = new McVersion(OriginalVersion.Path);
                }
                else
                {
                    // Json 已提供 Jar 字段，使用该字段的信息
                    RealVersion = new McVersion(RequiredJar);
                }
                string ClientUrl;
                string ClientSHA1;
                // 判断需求的版本是否存在
                // 不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
                if (!File.Exists(RealVersion.Path + RealVersion.Name + ".json"))
                {
                    RealVersion = Version;
                    ModBase.Log("[Minecraft] 可能缺少前置版本 " + RealVersion.Name + "，找不到对应的 Json 文件", ModBase.LogLevel.Debug);
                }
                // 获取详细下载信息
                if (RealVersion.JsonObject["downloads"] is not null && RealVersion.JsonObject["downloads"]["client"] is not null)
                {
                    ClientUrl = (string)RealVersion.JsonObject["downloads"]["client"]["url"];
                    ClientSHA1 = (string)RealVersion.JsonObject["downloads"]["client"]["sha1"];
                }
                else
                {
                    ClientUrl = null;
                    ClientSHA1 = null;
                }
                // 把所需的原版 Jar 添加进去
                McLibListGetRet.Add(new McLibToken() { LocalPath = RealVersion.Path + RealVersion.Name + ".jar", Size = 0L, IsNatives = false, Url = ClientUrl, SHA1 = ClientSHA1 });
            }

            return McLibListGetRet;

        }
        /// <summary>
    /// 获取 Minecraft 某一版本忽视继承的支持库列表，即结果中没有继承项。
    /// </summary>
        public static List<McLibToken> McLibListGetWithJson(JObject JsonObject, bool KeepSameNameDifferentVersionResult = false, string CustomMcFolder = null, string JumpLoaderFolder = null)
        {
            CustomMcFolder = CustomMcFolder ?? PathMcFolder;
            var BasicArray = new List<McLibToken>();

            // 添加基础 Json 项
            JArray AllLibs = (JArray)JsonObject["libraries"];
            // 添加 JumpLoader Json 项
            if (JsonObject["jumploader"] is not null && JsonObject["jumploader"]["jars"] is not null && JsonObject["jumploader"]["jars"]["maven"] is not null)
            {
                foreach (var JumpLoaderToken in JsonObject["jumploader"]["jars"]["maven"])
                    AllLibs.Add(JumpLoaderToken);
            }

            // 转换为 LibToken
            foreach (JObject Library in AllLibs.Children())
            {

                // 清理 null 项（BakaXL 会把没有的项序列化为 null，但会被 Newtonsoft 转换为 JValue，导致 Is Nothing = false；这导致了 #409）
                for (int i = Library.Properties().Count() - 1; i >= 0; i -= 1)
                {
                    if (Library.Properties().ElementAtOrDefault(i).Value.Type == JTokenType.Null)
                        Library.Remove(Library.Properties().ElementAtOrDefault(i).Name);
                }

                // 检查是否需要（Rules）
                if (!McJsonRuleCheck(Library["rules"]))
                    continue;

                // 检查 JumpLoader
                bool IsJumpLoader = false;
                if (Library["mavenPath"] is not null)
                {
                    IsJumpLoader = true;
                    if (Library["name"] is null)
                        Library.Add("name", Library["mavenPath"]); // 这里的修改会导致原 Json 内容改变
                    if (Library["repoUrl"] is not null && Library["url"] is null)
                        Library.Add("url", Library["repoUrl"]);
                }

                // 获取根节点下的 url
                string RootUrl = (string)Library["url"];
                if (RootUrl is not null)
                {
                    RootUrl += McLibGet((string)Library["name"], false, true, CustomMcFolder).Replace(@"\", "/");
                }

                // 根据是否本地化处理（Natives）
                if (Library["natives"] is null) // 没有 Natives
                {
                    string LocalPath;
                    if (IsJumpLoader)
                    {
                        LocalPath = McLibGet((string)Library["name"], CustomMcFolder: JumpLoaderFolder ?? CustomMcFolder);
                    }
                    else
                    {
                        LocalPath = McLibGet((string)Library["name"], CustomMcFolder: CustomMcFolder);
                    }
                    try
                    {
                        if (Library["downloads"] is not null && Library["downloads"]["artifact"] is not null)
                        {
                            McLibToken @init = new McLibToken();
                            BasicArray.Add((@init.IsJumpLoader = IsJumpLoader, @init.OriginalName = (string)Library["name"], @init.Url = (string)(RootUrl ?? Library["downloads"]["artifact"]["url"]), @init.LocalPath = Library["downloads"]["artifact"]["path"] is null ? McLibGet((string)Library["name"], CustomMcFolder: CustomMcFolder) : CustomMcFolder + @"libraries\" + Library["downloads"]["artifact"]["path"].ToString().Replace("/", @"\"), @init.Size = (long)Math.Round(ModBase.Val(Library["downloads"]["artifact"]["size"].ToString())), @init.IsNatives = false, @init.SHA1 = Library["downloads"]["artifact"]["sha1"]?.ToString(), @init).@init);
                        }
                        else
                        {
                            BasicArray.Add(new McLibToken() { IsJumpLoader = IsJumpLoader, OriginalName = (string)Library["name"], Url = RootUrl, LocalPath = LocalPath, Size = 0L, IsNatives = false, SHA1 = null });
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "处理实际支持库列表失败（无 Natives，" + (Library["name"] ?? "Nothing").ToString() + "）");
                        BasicArray.Add(new McLibToken() { IsJumpLoader = IsJumpLoader, OriginalName = (string)Library["name"], Url = RootUrl, LocalPath = LocalPath, Size = 0L, IsNatives = false, SHA1 = null });
                    }
                }
                else if (Library["natives"]["windows"] is not null) // 有 Windows Natives
                {
                    try
                    {
                        if (Library["downloads"] is not null && Library["downloads"]["classifiers"] is not null && Library["downloads"]["classifiers"]["natives-windows"] is not null)
                        {
                            BasicArray.Add(new McLibToken()
                            {
                                IsJumpLoader = IsJumpLoader,
                                OriginalName = (string)Library["name"],
                                Url = (string)(RootUrl ?? Library["downloads"]["classifiers"]["natives-windows"]["url"]),
                                LocalPath = Library["downloads"]["classifiers"]["natives-windows"]["path"] is null ? McLibGet((string)Library["name"], CustomMcFolder: CustomMcFolder).Replace(".jar", "-" + Library["natives"]["windows"].ToString() + ".jar").Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32") : CustomMcFolder + @"libraries\" + Library["downloads"]["classifiers"]["natives-windows"]["path"].ToString().Replace("/", @"\"),
                                Size = (long)Math.Round(ModBase.Val(Library["downloads"]["classifiers"]["natives-windows"]["size"].ToString())),
                                IsNatives = true,
                                SHA1 = Library["downloads"]["classifiers"]["natives-windows"]["sha1"].ToString()
                            });
                        }
                        else
                        {
                            BasicArray.Add(new McLibToken() { IsJumpLoader = IsJumpLoader, OriginalName = (string)Library["name"], Url = RootUrl, LocalPath = McLibGet((string)Library["name"], CustomMcFolder: CustomMcFolder).Replace(".jar", "-" + Library["natives"]["windows"].ToString() + ".jar").Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"), Size = 0L, IsNatives = true, SHA1 = null });
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "处理实际支持库列表失败（有 Natives，" + (Library["name"] ?? "Nothing").ToString() + "）");
                        BasicArray.Add(new McLibToken() { IsJumpLoader = IsJumpLoader, OriginalName = (string)Library["name"], Url = RootUrl, LocalPath = McLibGet((string)Library["name"], CustomMcFolder: CustomMcFolder).Replace(".jar", "-" + Library["natives"]["windows"].ToString() + ".jar").Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"), Size = 0L, IsNatives = true, SHA1 = null });
                    }
                }

            }

            // 去重
            var ResultArray = new Dictionary<string, McLibToken>();
            // 测试例：
            // D:\Minecraft\test\libraries\net\neoforged\mergetool\2.0.0\mergetool-2.0.0-api.jar
            // D:\Minecraft\test\libraries\org\apache\commons\commons-collections4\4.2\commons-collections4-4.2.jar
            // D:\Minecraft\test\libraries\com\google\guava\guava\31.1-jre\guava-31.1-jre.jar
            string GetVersion(McLibToken Token) => ModBase.GetFolderNameFromPath(ModBase.GetPathFromFullPath(Token.LocalPath));
            for (int i = 0, loopTo = BasicArray.Count - 1; i <= loopTo; i++)
            {
                string Key = BasicArray[i].Name + BasicArray[i].IsNatives.ToString() + BasicArray[i].IsJumpLoader.ToString();
                if (ResultArray.ContainsKey(Key))
                {
                    string BasicArrayVersion = GetVersion(BasicArray[i]);
                    string ResultArrayVersion = GetVersion(ResultArray[Key]);
                    if ((BasicArrayVersion ?? "") != (ResultArrayVersion ?? "") && KeepSameNameDifferentVersionResult)
                    {
                        ModBase.Log($"[Minecraft] 发现疑似重复的支持库：{BasicArray[i]} ({BasicArrayVersion}) 与 {ResultArray[Key]} ({ResultArrayVersion})");
                        ResultArray.Add(Key + ModBase.GetUuid(), BasicArray[i]);
                    }
                    else
                    {
                        ModBase.Log($"[Minecraft] 发现重复的支持库：{BasicArray[i]} ({BasicArrayVersion}) 与 {ResultArray[Key]} ({ResultArrayVersion})，已忽略其中之一");
                        if (VersionSortBoolean(BasicArrayVersion, ResultArrayVersion))
                        {
                            ResultArray[Key] = BasicArray[i];
                        }
                    }
                }
                else
                {
                    ResultArray.Add(Key, BasicArray[i]);
                }
            }
            return ResultArray.Values.ToList();
        }

        /// <summary>
    /// 获取版本缺失的支持库文件所对应的 NetTaskFile。
    /// </summary>
        public static List<ModNet.NetFile> McLibFix(McVersion Version)
        {
            if (!Version.IsLoaded)
                Version.Load(); // 确保例如 JumpLoader 等项被合并入 Json
            var Result = new List<ModNet.NetFile>();

            // 更新此方法时需要同步更新 Forge 新版自动安装方法！

            // 主 Jar 文件
            try
            {
                var MainJar = ModDownload.DlClientJarGet(Version, true);
                if (MainJar is not null)
                    Result.Add(MainJar);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "版本缺失主 Jar 文件所必须的信息", ModBase.LogLevel.Developer);
            }

            // Library 文件
            Result.AddRange(McLibFixFromLibToken(McLibListGet(Version, false), JumpLoaderFolder: Version.PathIndie + @".jumploader\"));

            // 统一通行证文件
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: Version), 3, false)))
            {
                string TargetFile = ModBase.PathAppdata + "nide8auth.jar";
                JObject DownloadInfo = null;
                // 获取下载信息
                try
                {
                    ModBase.Log("[Minecraft] 开始获取统一通行证下载信息");
                    // 测试链接：https://auth.mc-user.com:233/00000000000000000000000000000000/
                    DownloadInfo = (JObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(new[] { Conversions.ToString(Operators.ConcatenateObject("https://auth.mc-user.com:233/", ModBase.Setup.Get("VersionServerNide", Version: Version))) }, IsJson: true));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取统一通行证下载信息失败");
                }
                // 校验文件
                if (DownloadInfo is not null)
                {
                    var Checker = new ModBase.FileChecker(Hash: DownloadInfo["jarHash"].ToString());
                    if (Checker.Check(TargetFile) is not null)
                    {
                        // 开始下载
                        ModBase.Log("[Minecraft] 统一通行证需要更新：Hash - " + Checker.Hash, ModBase.LogLevel.Developer);
                        Result.Add(new ModNet.NetFile(new[] { "https://login.mc-user.com:233/index/jar" }, TargetFile, Checker));
                    }
                }
            }

            // Authlib-Injector 文件
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionServerLogin", Version: Version), 4, false)))
            {
                string TargetFile = ModBase.PathPure + @"\authlib-injector.jar";
                JObject DownloadInfo = null;
                // 获取下载信息
                try
                {
                    ModBase.Log("[Minecraft] 开始获取 Authlib-Injector 下载信息");
                    DownloadInfo = (JObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(new[] { "https://authlib-injector.yushi.moe/artifact/latest.json", "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json" }, IsJson: true));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取 Authlib-Injector 下载信息失败");
                }
                // 校验文件
                if (DownloadInfo is not null)
                {
                    var Checker = new ModBase.FileChecker(Hash: DownloadInfo["checksums"]["sha256"].ToString());
                    if (Checker.Check(TargetFile) is not null)
                    {
                        // 开始下载
                        string DownloadAddress = DownloadInfo["download_url"].ToString().Replace("bmclapi2.bangbang93.com/mirrors/authlib-injector", "authlib-injector.yushi.moe");
                        ModBase.Log("[Minecraft] Authlib-Injector 需要更新：" + DownloadAddress, ModBase.LogLevel.Developer);
                        Result.Add(new ModNet.NetFile(new[] { DownloadAddress, DownloadAddress.Replace("authlib-injector.yushi.moe", "bmclapi2.bangbang93.com/mirrors/authlib-injector") }, TargetFile, new ModBase.FileChecker(Hash: DownloadInfo["checksums"]["sha256"].ToString())));
                    }
                }
            }

            // 跳过校验
            if (Conversions.ToBoolean(ShouldIgnoreFileCheck(Version)))
            {
                ModBase.Log("[Minecraft] 用户要求尽量忽略文件检查，这可能会保留有误的文件");
                Result = Result.Where(f => { if (File.Exists(f.LocalPath)) { ModBase.Log("[Minecraft] 跳过下载的支持库文件：" + f.LocalPath, ModBase.LogLevel.Debug); return false; } else { return true; } }).ToList();
            }

            return Result;
        }
        /// <summary>
    /// 将 McLibToken 列表转换为 NetFile。无需下载的文件会被自动过滤。
    /// </summary>
        public static List<ModNet.NetFile> McLibFixFromLibToken(List<McLibToken> Libs, string CustomMcFolder = null, string JumpLoaderFolder = null)
        {
            CustomMcFolder = CustomMcFolder ?? PathMcFolder;
            var Result = new List<ModNet.NetFile>();
            // 获取
            foreach (McLibToken Token in Libs)
            {
                // 检查文件
                var Checker = new ModBase.FileChecker(ActualSize: Token.Size == 0L ? -1 : Token.Size, Hash: Token.SHA1);
                if (Checker.Check(Token.LocalPath) is null)
                    continue;
                // 文件不符合，添加下载
                var Urls = new List<string>();
                if (Token.Url is null && Token.Name == "net.minecraftforge:forge:universal")
                {
                    // 特判修复 Forge 部分 universal 文件缺失 URL（#5455）
                    Token.Url = "https://maven.minecraftforge.net" + Token.LocalPath.Replace((Token.IsJumpLoader ? JumpLoaderFolder : CustomMcFolder) + "libraries", "").Replace(@"\", "/");
                }
                if (Token.Url is not null)
                {
                    // 获取 URL 的真实地址
                    Urls.Add(Token.Url);
                    if (Token.Url.Contains("launcher.mojang.com/v1/objects") || Token.Url.Contains("client.txt") || Token.Url.Contains(".tsrg"))
                    {
                        Urls.AddRange(ModDownload.DlSourceLauncherOrMetaGet(Token.Url).ToList()); // Mappings（#4425）
                    }
                    if (Token.Url.Contains("maven"))
                    {
                        Urls.Insert(0, Token.Url.Replace(Strings.Mid(Token.Url, 1, Token.Url.IndexOfF("maven")), "https://bmclapi2.bangbang93.com/").Replace("maven.fabricmc.net", "maven").Replace("maven.minecraftforge.net", "maven").Replace("maven.neoforged.net/releases", "maven"));
                    }
                }
                if (Token.LocalPath.Contains("transformer-discovery-service"))
                {
                    // Transformer 文件释放
                    if (!File.Exists(Token.LocalPath))
                        ModBase.WriteFile(Token.LocalPath, ModBase.GetResources("Transformer"));
                    ModBase.Log("[Download] 已自动释放 Transformer Discovery Service", ModBase.LogLevel.Developer);
                    continue;
                }
                else if (Token.LocalPath.Contains(@"optifine\OptiFine"))
                {
                    // OptiFine 主 Jar
                    string OptiFineBase = Token.LocalPath.Replace((Token.IsJumpLoader ? JumpLoaderFolder : CustomMcFolder) + @"libraries\optifine\OptiFine\", "").Split("_")[0] + "/" + ModBase.GetFileNameFromPath(Token.LocalPath).Replace("-", "_");
                    OptiFineBase = "/maven/com/optifine/" + OptiFineBase;
                    if (OptiFineBase.Contains("_pre"))
                        OptiFineBase = OptiFineBase.Replace("com/optifine/", "com/optifine/preview_");
                    Urls.Add("https://bmclapi2.bangbang93.com" + OptiFineBase);
                }
                else if (Urls.Count <= 2)
                {
                    // 普通文件
                    Urls.AddRange(ModDownload.DlSourceLibraryGet("https://libraries.minecraft.net" + Token.LocalPath.Replace((Token.IsJumpLoader ? JumpLoaderFolder : CustomMcFolder) + "libraries", "").Replace(@"\", "/")));
                }
                Result.Add(new ModNet.NetFile(Urls.Distinct(), Token.LocalPath, Checker));
            }
            // 去重并返回
            return Result.Distinct((a, b) => (a.LocalPath ?? "") == (b.LocalPath ?? ""));
        }
        /// <summary>
    /// 获取对应的支持库文件地址。
    /// </summary>
    /// <param name="Original">原始地址，如 com.mumfrey:liteloader:1.12.2-SNAPSHOT。</param>
    /// <param name="WithHead">是否包含 Lib 文件夹头部，若不包含，则会类似以 com\xxx\ 开头。</param>
        public static string McLibGet(string Original, bool WithHead = true, bool IgnoreLiteLoader = false, string CustomMcFolder = null)
        {
            string McLibGetRet = default;
            CustomMcFolder = CustomMcFolder ?? PathMcFolder;
            string[] Splited = Original.Split(":");
            McLibGetRet = (WithHead ? CustomMcFolder + @"libraries\" : "") + Splited[0].Replace(".", @"\") + @"\" + Splited[1] + @"\" + Splited[2] + @"\" + Splited[1] + "-" + Splited[2] + ".jar";
            // 判断 OptiFine 是否应该使用 installer
            if (McLibGetRet.Contains(@"optifine\OptiFine\1.") && Splited[2].Split(".").Count() > 1)
            {
                int MajorVersion = (int)Math.Round(ModBase.Val(Splited[2].Split(".")[1].BeforeFirst("_")));
                int MinorVersion = (int)Math.Round(Splited[2].Split(".").Count() > 2 ? ModBase.Val(Splited[2].Split(".")[2].BeforeFirst("_")) : 0d);
                if ((MajorVersion == 12 || MajorVersion == 20 && MinorVersion >= 4 || MajorVersion >= 21) && File.Exists($@"{CustomMcFolder}libraries\{Splited[0].Replace(".", @"\")}\{Splited[1]}\{Splited[2]}\{Splited[1]}-{Splited[2]}-installer.jar")) // 仅在 1.12 (无法追溯) 和 1.20.4+ (#5376) 遇到此问题
                {
                    ModLaunch.McLaunchLog("已将 " + Original + " 替换为对应的 Installer 文件");
                    McLibGetRet = McLibGetRet.Replace(".jar", "-installer.jar");
                }
            }

            return McLibGetRet;
        }

        /// <summary>
    /// 检查设置，是否应当忽略文件检查？
    /// </summary>
        public static bool ShouldIgnoreFileCheck(McVersion Version)
        {
            return (bool)ModBase.Setup.Get("VersionAdvanceAssetsV2", Version: Version) || Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionAdvanceAssets", Version: Version), 2, false);
        }

        #endregion

        #region 资源文件（Assets）

        // 获取索引
        /// <summary>
    /// 获取某版本资源文件索引的对应 Json 项，详见版本 Json 中的 assetIndex 项。失败会抛出异常。
    /// </summary>
        public static JToken McAssetsGetIndex(McVersion Version, bool ReturnLegacyOnError = false, bool CheckURLEmpty = false)
        {
            string AssetsName;
            try
            {
                while (true)
                {
                    var Index = Version.JsonObject["assetIndex"];
                    if (Index is not null && Index["id"] is not null)
                        return Index;
                    if (Version.JsonObject["assets"] is not null)
                        AssetsName = Version.JsonObject["assets"].ToString();
                    if (CheckURLEmpty && Index["url"] is not null)
                        return Index;
                    // 下一个版本
                    if (string.IsNullOrEmpty(Version.InheritVersion))
                        break;
                    Version = new McVersion(PathMcFolder + @"versions\" + Version.InheritVersion);
                }
            }
            catch
            {
            }
            // 无法获取到下载地址
            if (ReturnLegacyOnError)
            {
                // 返回 assets 文件名会由于没有下载地址导致全局失败
                // If AssetsName IsNot Nothing AndAlso AssetsName <> "legacy" Then
                // Log("[Minecraft] 无法获取资源文件索引下载地址，使用 assets 项提供的资源文件名：" & AssetsName)
                // Return GetJson("{""id"": """ & AssetsName & """}")
                // Else
                ModBase.Log("[Minecraft] 无法获取资源文件索引下载地址，使用默认的 legacy 下载地址");
                return (JToken)ModBase.GetJson(@"{
                ""id"": ""legacy"",
                ""sha1"": ""c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729"",
                ""size"": 134284,
                ""url"": ""https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json"",
                ""totalSize"": 111220701
            }");
            }
            // End If
            else
            {
                throw new Exception("该版本不存在资源文件索引信息");
            }
        }
        /// <summary>
    /// 获取某版本资源文件索引名，优先使用 assetIndex，其次使用 assets。失败会返回 legacy。
    /// </summary>
        public static string McAssetsGetIndexName(McVersion Version)
        {
            try
            {
                while (true)
                {
                    if (Version.JsonObject["assetIndex"] is not null && Version.JsonObject["assetIndex"]["id"] is not null)
                    {
                        return Version.JsonObject["assetIndex"]["id"].ToString();
                    }
                    if (Version.JsonObject["assets"] is not null)
                    {
                        return Version.JsonObject["assets"].ToString();
                    }
                    if (string.IsNullOrEmpty(Version.InheritVersion))
                        break;
                    Version = new McVersion(PathMcFolder + @"versions\" + Version.InheritVersion);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取资源文件索引名失败");
            }
            return "legacy";
        }

        // 获取列表
        private struct McAssetsToken
        {
            /// <summary>
        /// 文件的完整本地路径。
        /// </summary>
            public string LocalPath;
            /// <summary>
        /// Json 中书写的源路径。例如 minecraft/sounds/mob/stray/death2.ogg 。
        /// </summary>
            public string SourcePath;
            /// <summary>
        /// 文件大小。若无有效数据即为 0。
        /// </summary>
            public long Size;
            /// <summary>
        /// 文件的 Hash 校验码。
        /// </summary>
            public string Hash;

            public override string ToString()
            {
                return ModBase.GetString(Size) + " | " + LocalPath;
            }
        }
        /// <summary>
    /// 获取 Minecraft 的资源文件列表。失败会抛出异常。
    /// </summary>
        private static List<McAssetsToken> McAssetsListGet(McVersion Version)
        {
            string IndexName = McAssetsGetIndexName(Version);
            try
            {

                // 初始化
                if (!File.Exists($@"{PathMcFolder}assets\indexes\{IndexName}.json"))
                    throw new FileNotFoundException("未找到 Asset Index", PathMcFolder + @"assets\indexes\" + IndexName + ".json");
                var Result = new List<McAssetsToken>();
                JObject Json = (JObject)ModBase.GetJson(ModBase.ReadFile($@"{PathMcFolder}assets\indexes\{IndexName}.json"));

                // 读取列表
                foreach (JProperty File in Json["objects"].Children())
                {
                    string LocalPath;
                    if (Json["map_to_resources"] is not null && Json["map_to_resources"].ToObject<bool>())
                    {
                        // Remap
                        LocalPath = Version.PathIndie + @"resources\" + File.Name.Replace("/", @"\");
                    }
                    else if (Json["virtual"] is not null && Json["virtual"].ToObject<bool>())
                    {
                        // Virtual
                        LocalPath = PathMcFolder + @"assets\virtual\legacy\" + File.Name.Replace("/", @"\");
                    }
                    else
                    {
                        // 正常
                        LocalPath = PathMcFolder + @"assets\objects\" + Strings.Left(File.Value["hash"].ToString(), 2) + @"\" + File.Value["hash"].ToString();
                    }
                    Result.Add(new McAssetsToken()
                    {
                        LocalPath = LocalPath,
                        SourcePath = File.Name,
                        Hash = File.Value["hash"].ToString(),
                        Size = Conversions.ToLong(File.Value["size"].ToString())
                    });
                }
                return Result;
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "获取资源文件列表失败：" + IndexName);
                throw;
            }
        }

        // 获取缺失列表
        /// <summary>
    /// 获取版本缺失的资源文件所对应的 NetTaskFile。
    /// </summary>
        public static List<ModNet.NetFile> McAssetsFixList(McVersion Version, bool CheckHash, [Optional] ref ModLoader.LoaderBase ProgressFeed)
        {
            var Result = new List<ModNet.NetFile>();

            List<McAssetsToken> AssetsList;
            try
            {
                AssetsList = McAssetsListGet(Version);
                McAssetsToken Token;
                if (ProgressFeed is not null)
                    ProgressFeed.Progress = 0.04d;
                for (int i = 0, loopTo = AssetsList.Count - 1; i <= loopTo; i++)
                {
                    // 初始化
                    Token = AssetsList[i];
                    if (ProgressFeed is not null)
                        ProgressFeed.Progress = 0.05d + 0.94d * i / AssetsList.Count;
                    // 检查文件是否存在
                    var File = new FileInfo(Token.LocalPath);
                    if (File.Exists && (Token.Size == 0L || Token.Size == File.Length) && (!CheckHash || Token.Hash is null || (Token.Hash ?? "") == (ModBase.GetFileSHA1(Token.LocalPath) ?? "")))
                        continue;
                    // 文件不存在，添加下载
                    Result.Add(new ModNet.NetFile(ModDownload.DlSourceResourceGet("https://resources.download.minecraft.net/" + Strings.Left(Token.Hash, 2) + "/" + Token.Hash), Token.LocalPath, new ModBase.FileChecker(ActualSize: Token.Size == 0L ? -1 : Token.Size, Hash: Token.Hash)));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取版本缺失的资源文件下载列表失败");
            }
            if (ProgressFeed is not null)
                ProgressFeed.Progress = 0.99d;

            return Result;
        }

        #endregion

        /// <summary>
    /// 发送 Minecraft 更新提示。
    /// </summary>
        public static void McDownloadClientUpdateHint(string VersionName, JObject Json)
        {
            try
            {

                // 获取对应版本
                JToken Version = null;
                foreach (var Token in Json["versions"])
                {
                    if (Token["id"] is not null && (Token["id"].ToString() ?? "") == (VersionName ?? ""))
                    {
                        Version = Token;
                        break;
                    }
                }
                // 进行提示
                if (Version is null)
                    return;
                DateTime Time = (DateTime)Version["releaseTime"];
                string MsgBoxText = $"新版本：{VersionName}{Constants.vbCrLf}" + ((DateTime.Now - Time).TotalDays > 1d ? "更新时间：" + Time.ToString() : "更新于：" + ModBase.GetTimeSpanString(Time - DateTime.Now, false));
                int MsgResult = ModMain.MyMsgBox(MsgBoxText, "Minecraft 更新提示", "确定", "下载", (DateTime.Now - Time).TotalHours > 3d ? "更新日志" : "", Button3Action: () => ModDownloadLib.McUpdateLogShow(Version));
                // 弹窗结果
                if (MsgResult == 2)
                {
                    // 下载
                    ModBase.RunInUi(() =>
        {
            PageDownloadInstall.McVersionWaitingForSelect = VersionName;
            ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
        });
                }
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "Minecraft 更新提示发送失败（" + (VersionName ?? "Nothing") + "）", ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
    /// 比较两个版本名的排序，若 Left 较新或相同则返回 True（Left >= Right）。无法比较两个 Pre 的大小。
    /// 支持的格式范例：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    /// </summary>
        public static bool VersionSortBoolean(string Left, string Right)
        {
            return VersionSortInteger(Left, Right) >= 0;
        }
        /// <summary>
    /// 比较两个版本名的排序，若 Left 较新则返回 1，相同则返回 0，Right 较新则返回 -1。
    /// 支持的格式范例：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    /// </summary>
        public static int VersionSortInteger(string Left, string Right)
        {
            if (Left == "未知版本" || Right == "未知版本")
            {
                if (Left == "未知版本" && Right != "未知版本")
                    return 1;
                if (Left == "未知版本" && Right == "未知版本")
                    return 0;
                if (Left != "未知版本" && Right == "未知版本")
                    return -1;
            }
            Left = Left.ToLowerInvariant();
            Right = Right.ToLowerInvariant();
            var Lefts = Left.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+");
            var Rights = Right.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+");
            int i = 0;
            while (true)
            {
                // 两边均缺失，感觉是一个东西
                if (Lefts.Count - 1 < i && Rights.Count - 1 < i)
                {
                    if (Operators.CompareString(Left, Right, false) > 0)
                    {
                        return 1;
                    }
                    else if (Operators.CompareString(Left, Right, false) < 0)
                    {
                        return -1;
                    }
                    else
                    {
                        return 0;
                    }
                }
                // 确定两边的数值
                string LeftValue = Lefts.Count - 1 < i ? "-1" : Lefts[i];
                string RightValue = Rights.Count - 1 < i ? "-1" : Rights[i];
                if ((LeftValue ?? "") == (RightValue ?? ""))
                    goto NextEntry;
                if (LeftValue == "pre" || LeftValue == "snapshot")
                    LeftValue = "-3";
                if (LeftValue == "rc")
                    LeftValue = "-2";
                if (LeftValue == "experimental")
                    LeftValue = "-4";
                double LeftValValue = ModBase.Val(LeftValue);
                if (RightValue == "pre" || RightValue == "snapshot")
                    RightValue = "-3";
                if (RightValue == "rc")
                    RightValue = "-2";
                if (RightValue == "experimental")
                    RightValue = "-4";
                double RightValValue = ModBase.Val(RightValue);
                if (LeftValValue == 0d && RightValValue == 0d)
                {
                    // 如果没有数值则直接比较字符串
                    if (Operators.CompareString(LeftValue, RightValue, false) > 0)
                    {
                        return 1;
                    }
                    else if (Operators.CompareString(LeftValue, RightValue, false) < 0)
                    {
                        return -1;
                    }
                }
                // 如果有数值则比较数值
                // 这会使得一边是数字一边是字母时数字方更大
                else if (LeftValValue > RightValValue)
                {
                    return 1;
                }
                else if (LeftValValue < RightValValue)
                {
                    return -1;
                }

            NextEntry:
                ;

                i += 1;
            }
            return 0;
        }
        /// <summary>
    /// 比较两个版本名的排序器。
    /// </summary>
        public class VersionComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return VersionSortInteger(x, y);
            }
        }

        /// <summary>
    /// 为邮箱地址或手机号账号进行部分打码。
    /// </summary>
        public static string AccountFilter(string Account)
        {
            if (Account.Contains("@"))
            {
                // 是邮箱
                string[] Splits = Account.Split("@");
                // If Splits(0).Count >= 6 Then
                // '前半部分至少 6 位，屏蔽后 4 位
                // Return Mid(Splits(0), 1, Splits(0).Count - 4) & "****" & "@" & Splits(1)
                // Else
                // 前半部分不到 6 位，返回全 *
                return "".PadLeft(Splits[0].Count(), '*') + "@" + Splits[1];
            }
            // End If
            else if (Account.Count() >= 6)
            {
                // 至少 6 位，屏蔽后 4 位
                return Strings.Mid(Account, 1, Account.Count() - 4) + "****";
            }
            else
            {
                // 不到 6 位，返回全 *
                return "".PadLeft(Account.Count(), '*');
            }
        }

    }
}