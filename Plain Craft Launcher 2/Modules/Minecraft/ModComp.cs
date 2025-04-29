using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public static class ModComp
    {

        public enum CompType
        {
            /// <summary>
        /// Mod。
        /// </summary>
            Mod = 0,
            /// <summary>
        /// 整合包。
        /// </summary>
            ModPack = 1,
            /// <summary>
        /// 资源包。
        /// </summary>
            ResourcePack = 2,
            /// <summary>
        /// 光影包。
        /// </summary>
            Shader = 3,
            /// <summary>
        /// 其他。
        /// </summary>
            Other = 4
        }
        public enum CompLoaderType
        {
            // https://docs.curseforge.com/?http#tocS_ModLoaderType
            /// <summary>
        /// 模组加载器
        /// </summary>
            Any = 0,
            /// <summary>
        /// 模组加载器
        /// </summary>
            Forge = 1,
            /// <summary>
        /// 模组加载器
        /// </summary>
            LiteLoader = 3,
            /// <summary>
        /// 模组加载器
        /// </summary>
            Fabric = 4,
            /// <summary>
        /// 模组加载器
        /// </summary>
            Quilt = 5,
            /// <summary>
        /// 模组加载器
        /// </summary>
            NeoForge = 6,
            /// <summary>
        /// 材质包
        /// </summary>
            Minecraft = 7,
            /// <summary>
        /// 光影包
        /// </summary>
            Canvas = 8,
            /// <summary>
        /// 光影包
        /// </summary>
            Iris = 9,
            /// <summary>
        /// 光影包
        /// </summary>
            OptiFine = 10,
            /// <summary>
        /// 光影包
        /// </summary>
            Vanilla = 11
        }
        [Flags]
        public enum CompSourceType
        {
            CurseForge = 1,
            Modrinth = 2,
            Any = CurseForge | Modrinth
        }

        #region CompDatabase | Mod 数据库

        private static List<CompDatabaseEntry> _CompDatabase = null;
        private static List<CompDatabaseEntry> CompDatabase
        {
            get
            {
                if (_CompDatabase is not null)
                    return _CompDatabase;
                // 初始化数据库
                _CompDatabase = new List<CompDatabaseEntry>();
                int i = 0;
                foreach (var Line in ModBase.DecodeBytes(ModBase.GetResources("ModData")).Replace(Constants.vbCrLf, Constants.vbLf).Replace(Constants.vbCr, "").Split(Constants.vbLf))
                {
                    i += 1;
                    if (string.IsNullOrEmpty(Line))
                        continue;
                    foreach (string EntryData in Line.Split("¨"))
                    {
                        var Entry = new CompDatabaseEntry();
                        string[] SplitedLine = EntryData.Split("|");
                        if (SplitedLine[0].StartsWithF("@"))
                        {
                            Entry.CurseForgeSlug = null;
                            Entry.ModrinthSlug = SplitedLine[0].Replace("@", "");
                        }
                        else if (SplitedLine[0].EndsWithF("@"))
                        {
                            Entry.CurseForgeSlug = SplitedLine[0].TrimEnd('@');
                            Entry.ModrinthSlug = Entry.CurseForgeSlug;
                        }
                        else if (SplitedLine[0].Contains("@"))
                        {
                            Entry.CurseForgeSlug = SplitedLine[0].Split("@")[0];
                            Entry.ModrinthSlug = SplitedLine[0].Split("@")[1];
                        }
                        else
                        {
                            Entry.CurseForgeSlug = SplitedLine[0];
                            Entry.ModrinthSlug = null;
                        }
                        Entry.WikiId = i;
                        if (SplitedLine.Count() >= 2)
                        {
                            Entry.ChineseName = SplitedLine[1];
                            if (Entry.ChineseName.Contains("*")) // 处理 *
                            {
                                Entry.ChineseName = Entry.ChineseName.Replace("*", " (" + string.Join(" ", (Entry.CurseForgeSlug ?? Entry.ModrinthSlug).Split("-").Select(w => w.Substring(0, 1).ToUpper() + w.Substring(1, w.Length - 1))) + ")");
                            }
                        }
                        _CompDatabase.Add(Entry);
                    }
                }
                return _CompDatabase;
            }
        }

        private class CompDatabaseEntry
        {
            /// <summary>
        /// McMod 的对应 ID。
        /// </summary>
            public int WikiId;
            /// <summary>
        /// 中文译名。空字符串代表没有翻译。
        /// </summary>
            public string ChineseName = "";
            /// <summary>
        /// CurseForge Slug（例如 advanced-solar-panels）。
        /// </summary>
            public string CurseForgeSlug = null;
            /// <summary>
        /// Modrinth Slug（例如 advanced-solar-panels）。
        /// </summary>
            public string ModrinthSlug = null;

            public override string ToString()
            {
                return (CurseForgeSlug ?? "") + "&" + (ModrinthSlug ?? "") + "|" + WikiId + "|" + ChineseName;
            }
        }

        #endregion

        #region CompProject | 工程信息

        // 类定义

        public class CompProject
        {

            // 源信息

            /// <summary>
        /// 该工程信息来自 CurseForge 还是 Modrinth。
        /// </summary>
            public readonly bool FromCurseForge;
            /// <summary>
        /// 工程的种类。
        /// </summary>
            public readonly CompType Type;
            /// <summary>
        /// 工程的短名。例如 technical-enchant。
        /// </summary>
            public readonly string Slug;
            /// <summary>
        /// CurseForge 工程的数字 ID。Modrinth 工程的乱码 ID。
        /// </summary>
            public readonly string Id;
            /// <summary>
        /// CurseForge 文件列表的数字 ID。Modrinth 工程的此项无效。
        /// </summary>
            public readonly List<int> CurseForgeFileIds;

            // 描述性信息

            /// <summary>
        /// 原始的英文名称。
        /// </summary>
            public readonly string RawName;
            /// <summary>
        /// 英文描述。
        /// </summary>
            public readonly string Description;
            /// <summary>
        /// 来源网站的工程页面网址。确保格式一定标准。
        /// CurseForge：https://www.curseforge.com/minecraft/mc-mods/jei
        /// Modrinth：https://modrinth.com/mod/technical-enchant
        /// </summary>
            public readonly string Website;
            /// <summary>
        /// 最后一次更新的时间。可能为 Nothing。
        /// </summary>
            public readonly DateTime? LastUpdate = default;
            /// <summary>
        /// 下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量！
        /// </summary>
            public readonly int DownloadCount;
            /// <summary>
        /// 支持的 Mod 加载器列表。可能为空。
        /// </summary>
            public readonly List<CompLoaderType> ModLoaders;
            /// <summary>
        /// 描述性标签的内容。已转换为中文。
        /// </summary>
            public readonly List<string> Tags;
            /// <summary>
        /// Logo 图片的下载地址。若为 Nothing 则没有。
        /// </summary>
            public string LogoUrl = null;
            /// <summary>
        /// 游戏大版本列表。例如：18, 16, 15……
        /// </summary>
            public readonly List<int> GameVersions;

            // 数据库信息

            private bool LoadedDatabase = false;
            private CompDatabaseEntry _DatabaseEntry = null;
            /// <summary>
        /// 关联的数据库条目。若为 Nothing 则没有。
        /// </summary>
            private CompDatabaseEntry DatabaseEntry
            {
                get
                {
                    if (!LoadedDatabase)
                    {
                        LoadedDatabase = true;
                        if (Type == CompType.Mod)
                            _DatabaseEntry = CompDatabase.FirstOrDefault(c => ((FromCurseForge ? c.CurseForgeSlug : c.ModrinthSlug) ?? "") == (Slug ?? ""));
                    }
                    return _DatabaseEntry;
                }
                set
                {
                    LoadedDatabase = true;
                    _DatabaseEntry = value;
                }
            }
            /// <summary>
        /// MC 百科的页面 ID。若为 0 则没有。
        /// </summary>
            public int WikiId
            {
                get
                {
                    return DatabaseEntry is null ? 0 : DatabaseEntry.WikiId;
                }
            }
            /// <summary>
        /// 翻译后的中文名。若数据库没有则等同于 RawName。
        /// </summary>
            public string TranslatedName
            {
                get
                {
                    return DatabaseEntry is null || string.IsNullOrEmpty(DatabaseEntry.ChineseName) ? RawName : DatabaseEntry.ChineseName;
                }
            }
            /// <summary>
        /// 中文描述。若为 Nothing 则没有。
        /// </summary>
            public Task<string> ChineseDescription
            {
                get
                {
                    return GetChineseDescriptionAsync();
                }
            }

            private async Task<string> GetChineseDescriptionAsync()
            {
                string @from = FromCurseForge ? "curseforge" : "modrinth";
                string para = FromCurseForge ? "modId" : "project_id";
                string result = null;

                try
                {
                    var jsonObject = await Task.Run(() => ModNet.NetGetCodeByRequestOnce($"https://mod.mcimirror.top/translate/{from}?{para}={Id}", Encode: Encoding.UTF8, IsJson: true));
                    if (Conversions.ToBoolean(((dynamic)jsonObject).ContainsKey("translated")))
                    {
                        result = jsonObject("translated").ToString();
                    }
                    else
                    {
                        ModMain.Hint($"{TranslatedName} 的简介暂无译文！", ModMain.HintType.Critical);
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取中文描述时出现错误！");
                    ModMain.Hint($"获取译文时出现错误，信息：{ex.Message}", ModMain.HintType.Critical);
                }

                return result;
            }

            // 实例化

            /// <summary>
        /// 从工程 Json 中初始化实例。若出错会抛出异常。
        /// </summary>
            public CompProject(JObject Data)
            {
                if (Data.ContainsKey("Tags"))
                {
                    #region CompJson
                    FromCurseForge = (string)Data["DataSource"] == "CurseForge";
                    Type = (CompType)Data["Type"].ToObject<int>();
                    Slug = (string)Data["Slug"];
                    Id = (string)Data["Id"];
                    if (Data.ContainsKey("CurseForgeFileIds"))
                        CurseForgeFileIds = ((JArray)Data["CurseForgeFileIds"]).Select(t => t.ToObject<int>()).ToList();
                    RawName = (string)Data["RawName"];
                    Description = (string)Data["Description"];
                    Website = (string)Data["Website"];
                    if (Data.ContainsKey("LastUpdate"))
                        LastUpdate = (DateTime?)Data["LastUpdate"];
                    DownloadCount = (int)Data["DownloadCount"];
                    if (Data.ContainsKey("ModLoaders"))
                    {
                        ModLoaders = ((JArray)Data["ModLoaders"]).Select(t => (CompLoaderType)t.ToObject<int>()).ToList();
                    }
                    else
                    {
                        ModLoaders = new List<CompLoaderType>();
                    }
                    Tags = ((JArray)Data["Tags"]).Select(t => t.ToString()).ToList();
                    if (Data.ContainsKey("LogoUrl"))
                        LogoUrl = (string)Data["LogoUrl"];
                    if (Data.ContainsKey("GameVersions"))
                    {
                        GameVersions = ((JArray)Data["GameVersions"]).Select(t => t.ToObject<int>()).ToList();
                    }
                    else
                    {
                        GameVersions = new List<int>();
                    }
                }
                #endregion
                else
                {
                    FromCurseForge = Data.ContainsKey("summary");
                    if (FromCurseForge)
                    {
                        #region CurseForge
                        // 简单信息
                        Id = (string)Data["id"];
                        Slug = (string)Data["slug"];
                        RawName = (string)Data["name"];
                        Description = (string)Data["summary"];
                        Website = Data["links"]["websiteUrl"].ToString().TrimEnd('/');
                        LastUpdate = (DateTime?)Data["dateReleased"]; // #1194
                        DownloadCount = (int)Data["downloadCount"];
                        if (Data["logo"].Count() > 0)
                        {
                            if (Data["logo"]["thumbnailUrl"] is null || (string)Data["logo"]["thumbnailUrl"] == "")
                            {
                                LogoUrl = (string)Data["logo"]["url"];
                            }
                            else
                            {
                                LogoUrl = (string)Data["logo"]["thumbnailUrl"];
                            }
                        }
                        // FileIndexes / GameVersions / ModLoaders
                        ModLoaders = new List<CompLoaderType>();
                        var Files = new List<KeyValuePair<int, List<string>>>(); // FileId, GameVersions
                        foreach (var File in Data["latestFiles"] ?? new JArray())
                        {
                            var NewFile = new CompFile((JObject)File, Type);
                            if (!NewFile.Available)
                                continue;
                            ModLoaders.AddRange(NewFile.ModLoaders);
                            var GameVersions = File["gameVersions"].ToObject<List<string>>();
                            if (!GameVersions.Any(v => v.StartsWithF("1.")))
                                continue;
                            Files.Add(new KeyValuePair<int, List<string>>((int)File["id"], GameVersions));
                        }
                        foreach (var File in Data["latestFilesIndexes"] ?? new JArray()) // 这俩玩意儿包含的文件不一样，见 #3599
                        {
                            if (!File["gameVersion"].ToString().StartsWithF("1."))
                                continue;
                            Files.Add(new KeyValuePair<int, List<string>>((int)File["fileId"], new[] { File["gameVersion"].ToString() }.ToList()));
                        }
                        CurseForgeFileIds = Files.Select(f => f.Key).Distinct().ToList();
                        GameVersions = Files.SelectMany(f => f.Value).Where(v => v.StartsWithF("1.")).Select(v => (int)Math.Round(ModBase.Val(v.Split(".")[1].BeforeFirst("-")))).Where(v => v > 0).Distinct().OrderByDescending(v => v).ToList();
                        ModLoaders = ModLoaders.Distinct().OrderBy<CompLoaderType, int>(t => t).ToList();
                        // Type
                        if (Website.Contains("/mc-mods/") || Website.Contains("/mod/"))
                        {
                            Type = CompType.Mod;
                        }
                        else if (Website.Contains("/modpacks/"))
                        {
                            Type = CompType.ModPack;
                        }
                        else if (Website.Contains("/texture-packs/"))
                        {
                            Type = CompType.ResourcePack;
                        }
                        else if (Website.Contains("/shaders/"))
                        {
                            Type = CompType.Shader;
                        }
                        else
                        {
                            Type = CompType.Other;
                        }
                        // Tags
                        Tags = new List<string>();
                        foreach (var Category in (Data["categories"] ?? new JArray()).Select<JToken, int>(t => t["id"]).Distinct().OrderByDescending(c => c)) // 镜像源 API 可能丢失此字段：https://github.com/Hex-Dragon/PCL2/issues/4267#issuecomment-2254590831
                        {
                            switch (Category)
                            {
                                // Mod
                                case 406:
                                    {
                                        Tags.Add("世界元素");
                                        break;
                                    }
                                case 407:
                                    {
                                        Tags.Add("生物群系");
                                        break;
                                    }
                                case 410:
                                    {
                                        Tags.Add("维度");
                                        break;
                                    }
                                case 408:
                                    {
                                        Tags.Add("矿物/资源");
                                        break;
                                    }
                                case 409:
                                    {
                                        Tags.Add("天然结构");
                                        break;
                                    }
                                case 412:
                                    {
                                        Tags.Add("科技");
                                        break;
                                    }
                                case 415:
                                    {
                                        Tags.Add("管道/物流");
                                        break;
                                    }
                                case 4843:
                                    {
                                        Tags.Add("自动化");
                                        break;
                                    }
                                case 417:
                                    {
                                        Tags.Add("能源");
                                        break;
                                    }
                                case 4558:
                                    {
                                        Tags.Add("红石");
                                        break;
                                    }
                                case 436:
                                    {
                                        Tags.Add("食物/烹饪");
                                        break;
                                    }
                                case 416:
                                    {
                                        Tags.Add("农业");
                                        break;
                                    }
                                case 414:
                                    {
                                        Tags.Add("运输");
                                        break;
                                    }
                                case 420:
                                    {
                                        Tags.Add("仓储");
                                        break;
                                    }
                                case 419:
                                    {
                                        Tags.Add("魔法");
                                        break;
                                    }
                                case 422:
                                    {
                                        Tags.Add("冒险");
                                        break;
                                    }
                                case 424:
                                    {
                                        Tags.Add("装饰");
                                        break;
                                    }
                                case 411:
                                    {
                                        Tags.Add("生物");
                                        break;
                                    }
                                case 434:
                                    {
                                        Tags.Add("装备");
                                        break;
                                    }
                                case 423:
                                    {
                                        Tags.Add("信息显示");
                                        break;
                                    }
                                case 435:
                                    {
                                        Tags.Add("服务器");
                                        break;
                                    }
                                case 5191:
                                    {
                                        Tags.Add("改良");
                                        break;
                                    }
                                case 421:
                                    {
                                        Tags.Add("支持库");
                                        break;
                                    }
                                // 整合包
                                case 4484:
                                    {
                                        Tags.Add("多人");
                                        break;
                                    }
                                case 4479:
                                    {
                                        Tags.Add("硬核");
                                        break;
                                    }
                                case 4483:
                                    {
                                        Tags.Add("战斗");
                                        break;
                                    }
                                case 4478:
                                    {
                                        Tags.Add("任务");
                                        break;
                                    }
                                case 4472:
                                    {
                                        Tags.Add("科技");
                                        break;
                                    }
                                case 4473:
                                    {
                                        Tags.Add("魔法");
                                        break;
                                    }
                                case 4475:
                                    {
                                        Tags.Add("冒险");
                                        break;
                                    }
                                case 4476:
                                    {
                                        Tags.Add("探索");
                                        break;
                                    }
                                case 4477:
                                    {
                                        Tags.Add("小游戏");
                                        break;
                                    }
                                case 4471:
                                    {
                                        Tags.Add("科幻");
                                        break;
                                    }
                                case 4736:
                                    {
                                        Tags.Add("空岛");
                                        break;
                                    }
                                case 5128:
                                    {
                                        Tags.Add("原版改良");
                                        break;
                                    }
                                case 4487:
                                    {
                                        Tags.Add("FTB");
                                        break;
                                    }
                                case 4480:
                                    {
                                        Tags.Add("基于地图");
                                        break;
                                    }
                                case 4481:
                                    {
                                        Tags.Add("轻量");
                                        break;
                                    }
                                case 4482:
                                    {
                                        Tags.Add("大型");
                                        break;
                                    }
                                // 光影包
                                case 6553:
                                    {
                                        Tags.Add("写实");
                                        break;
                                    }
                                case 6554:
                                    {
                                        Tags.Add("幻想");
                                        break;
                                    }
                                case 6555:
                                    {
                                        Tags.Add("原版风");
                                        break;
                                    }
                                // 资源包
                                case 5244:
                                    {
                                        Tags.Add("字体包");
                                        break;
                                    }
                                case 5193:
                                    {
                                        Tags.Add("数据包");
                                        break;
                                    }
                                case 399:
                                    {
                                        Tags.Add("蒸汽朋克");
                                        break;
                                    }
                                case 396:
                                    {
                                        Tags.Add("128x");
                                        break;
                                    }
                                case 398:
                                    {
                                        Tags.Add("512x 或更高");
                                        break;
                                    }
                                case 397:
                                    {
                                        Tags.Add("256x");
                                        break;
                                    }
                                case 405:
                                    {
                                        Tags.Add("其他");
                                        break;
                                    }
                                case 395:
                                    {
                                        Tags.Add("64x");
                                        break;
                                    }
                                case 400:
                                    {
                                        Tags.Add("仿真");
                                        break;
                                    }
                                case 393:
                                    {
                                        Tags.Add("16x");
                                        break;
                                    }
                                case 403:
                                    {
                                        Tags.Add("传统");
                                        break;
                                    }
                                case 394:
                                    {
                                        Tags.Add("32x");
                                        break;
                                    }
                                case 404:
                                    {
                                        Tags.Add("动态效果");
                                        break;
                                    }
                                case 4465:
                                    {
                                        Tags.Add("模组支持");
                                        break;
                                    }
                                case 402:
                                    {
                                        Tags.Add("中世纪");
                                        break;
                                    }
                                case 401:
                                    {
                                        Tags.Add("现代");
                                        break;
                                    }

                            }
                        }
                        if (!Tags.Any())
                            Tags.Add("杂项");
                    }
                    #endregion
                    else
                    {
                        #region Modrinth
                        // 简单信息
                        Id = (string)(Data["project_id"] ?? Data["id"]); // 两个 API 会返回的 key 不一样
                        Slug = (string)Data["slug"];
                        RawName = (string)Data["title"];
                        Description = (string)Data["description"];
                        LastUpdate = (DateTime?)Data["date_modified"];
                        DownloadCount = (int)Data["downloads"];
                        LogoUrl = (string)Data["icon_url"];
                        if (string.IsNullOrEmpty(LogoUrl))
                            LogoUrl = null;
                        Website = $"https://modrinth.com/{Data["project_type"]}/{Slug}";
                        // GameVersions
                        // 搜索结果的键为 versions，获取特定工程的键为 game_versions
                        GameVersions = ((JArray)(Data["game_versions"] ?? Data["versions"]) ?? new JArray()).Select(v => v.ToString()).Where(v => v.StartsWithF("1.")).Select<string, int>(v => ModBase.Val(v.Split(".")[1].BeforeFirst("-"))).Where(v => v > 0).Distinct().OrderByDescending(v => v).ToList();
                        // Type
                        switch (Data["project_type"].ToString() ?? "")
                        {
                            case "mod":
                                {
                                    Type = CompType.Mod;
                                    break;
                                }
                            case "modpack":
                                {
                                    Type = CompType.ModPack;
                                    break;
                                }
                            case "resourcepack":
                                {
                                    Type = CompType.ResourcePack;
                                    break;
                                }
                            case "shader":
                                {
                                    Type = CompType.Shader;
                                    break;
                                }

                            default:
                                {
                                    Type = CompType.Other;
                                    break;
                                }
                        }
                        // Tags & ModLoaders
                        Tags = new List<string>();
                        ModLoaders = new List<CompLoaderType>();
                        if (Data?["loaders"] is not null)
                        {
                            foreach (var Category in Data["loaders"].Select(t => t.ToString()))
                            {
                                switch (Category ?? "")
                                {
                                    case "forge":
                                        {
                                            ModLoaders.Add(CompLoaderType.Forge);
                                            break;
                                        }
                                    case "fabric":
                                        {
                                            ModLoaders.Add(CompLoaderType.Fabric);
                                            break;
                                        }
                                    case "quilt":
                                        {
                                            ModLoaders.Add(CompLoaderType.Quilt);
                                            break;
                                        }
                                    case "neoforge":
                                        {
                                            ModLoaders.Add(CompLoaderType.NeoForge);
                                            break;
                                        }
                                }
                            }
                        }
                        foreach (var Category in Data["categories"].Select(t => t.ToString()))
                        {
                            switch (Category ?? "")
                            {
                                // 加载器
                                case "forge":
                                    {
                                        ModLoaders.Add(CompLoaderType.Forge);
                                        break;
                                    }
                                case "fabric":
                                    {
                                        ModLoaders.Add(CompLoaderType.Fabric);
                                        break;
                                    }
                                case "quilt":
                                    {
                                        ModLoaders.Add(CompLoaderType.Quilt);
                                        break;
                                    }
                                case "neoforge":
                                    {
                                        ModLoaders.Add(CompLoaderType.NeoForge);
                                        break;
                                    }
                                // Mod
                                case "worldgen":
                                    {
                                        Tags.Add("世界元素");
                                        break;
                                    }
                                case "technology":
                                    {
                                        Tags.Add("科技");
                                        break;
                                    }
                                case "food":
                                    {
                                        Tags.Add("食物/烹饪");
                                        break;
                                    }
                                case "game-mechanics":
                                    {
                                        Tags.Add("游戏机制");
                                        break;
                                    }
                                case "transportation":
                                    {
                                        Tags.Add("运输");
                                        break;
                                    }
                                case "storage":
                                    {
                                        Tags.Add("仓储");
                                        break;
                                    }
                                case "magic":
                                    {
                                        Tags.Add("魔法");
                                        break;
                                    }
                                case "adventure":
                                    {
                                        Tags.Add("冒险");
                                        break;
                                    }
                                case "decoration":
                                    {
                                        Tags.Add("装饰");
                                        break;
                                    }
                                case "mobs":
                                    {
                                        Tags.Add("生物");
                                        break;
                                    }
                                case "equipment":
                                    {
                                        Tags.Add("装备");
                                        break;
                                    }
                                case "optimization":
                                    {
                                        Tags.Add("性能优化");
                                        break;
                                    }
                                case "social":
                                    {
                                        Tags.Add("服务器");
                                        break;
                                    }
                                case "utility":
                                    {
                                        Tags.Add("改良");
                                        break;
                                    }
                                case "library":
                                    {
                                        Tags.Add("支持库");
                                        break;
                                    }
                                // 整合包
                                case "multiplayer":
                                    {
                                        Tags.Add("多人");
                                        break;
                                    }
                                case var @case when @case == "optimization":
                                    {
                                        Tags.Add("性能优化");
                                        break;
                                    }
                                case "challenging":
                                    {
                                        Tags.Add("硬核");
                                        break;
                                    }
                                case "combat":
                                    {
                                        Tags.Add("战斗");
                                        break;
                                    }
                                case "quests":
                                    {
                                        Tags.Add("任务");
                                        break;
                                    }
                                case var case1 when case1 == "technology":
                                    {
                                        Tags.Add("科技");
                                        break;
                                    }
                                case var case2 when case2 == "magic":
                                    {
                                        Tags.Add("魔法");
                                        break;
                                    }
                                case var case3 when case3 == "adventure":
                                    {
                                        Tags.Add("冒险");
                                        break;
                                    }
                                case "kitchen-sink":
                                    {
                                        Tags.Add("水槽包/大杂烩");
                                        break;
                                    }
                                case "lightweight":
                                    {
                                        Tags.Add("轻量");
                                        break;
                                    }
                                // 光影包
                                case "cartoon":
                                    {
                                        Tags.Add("卡通");
                                        break;
                                    }
                                case "cursed":
                                    {
                                        Tags.Add("Cursed");
                                        break;
                                    }
                                case "fantasy":
                                    {
                                        Tags.Add("幻想");
                                        break;
                                    }
                                case "realistic":
                                    {
                                        Tags.Add("写实");
                                        break;
                                    }
                                case "semi-realistic":
                                    {
                                        Tags.Add("半写实");
                                        break;
                                    }
                                case "vanilla-like":
                                    {
                                        Tags.Add("原版风");
                                        break;
                                    }

                                case "atmosphere":
                                    {
                                        Tags.Add("大气环境");
                                        break;
                                    }
                                case "bloom":
                                    {
                                        Tags.Add("植被");
                                        break;
                                    }
                                case "colored-lighting":
                                    {
                                        Tags.Add("光源着色");
                                        break;
                                    }
                                case "foliage":
                                    {
                                        Tags.Add("树叶");
                                        break;
                                    }
                                case "path-tracing":
                                    {
                                        Tags.Add("路径追踪");
                                        break;
                                    }
                                case "pbr":
                                    {
                                        Tags.Add("PBR");
                                        break;
                                    }
                                case "reflections":
                                    {
                                        Tags.Add("反射");
                                        break;
                                    }
                                case "shadows":
                                    {
                                        Tags.Add("阴影");
                                        break;
                                    }

                                case "potato":
                                    {
                                        Tags.Add("土豆画质");
                                        break;
                                    }
                                case "low":
                                    {
                                        Tags.Add("低性能影响");
                                        break;
                                    }
                                case "medium":
                                    {
                                        Tags.Add("中性能影响");
                                        break;
                                    }
                                case "high":
                                    {
                                        Tags.Add("高性能影响");
                                        break;
                                    }
                                case "screenshot":
                                    {
                                        Tags.Add("极致画质");
                                        break;
                                    }

                                case "canvas":
                                    {
                                        Tags.Add("Canvas");
                                        break;
                                    }
                                case "iris":
                                    {
                                        Tags.Add("Iris");
                                        break;
                                    }
                                case "optifine":
                                    {
                                        Tags.Add("OptiFine");
                                        break;
                                    }
                                case "vanilla":
                                    {
                                        Tags.Add("原版光影");
                                        break;
                                    }
                                // 资源包
                                case "8x-":
                                    {
                                        Tags.Add("8x-");
                                        break;
                                    }
                                case "16x":
                                    {
                                        Tags.Add("16x");
                                        break;
                                    }
                                case "32x":
                                    {
                                        Tags.Add("32x");
                                        break;
                                    }
                                case "48x":
                                    {
                                        Tags.Add("48x");
                                        break;
                                    }
                                case "64x":
                                    {
                                        Tags.Add("64x");
                                        break;
                                    }
                                case "128x":
                                    {
                                        Tags.Add("128x");
                                        break;
                                    }
                                case "256x":
                                    {
                                        Tags.Add("256x");
                                        break;
                                    }
                                case "512x+":
                                    {
                                        Tags.Add("512x+");
                                        break;
                                    }
                                case "audio":
                                    {
                                        Tags.Add("声音");
                                        break;
                                    }
                                case "blocks":
                                    {
                                        Tags.Add("方块");
                                        break;
                                    }
                                case var case4 when case4 == "combat":
                                    {
                                        Tags.Add("战斗");
                                        break;
                                    }
                                case "core-shaders":
                                    {
                                        Tags.Add("核心着色器");
                                        break;
                                    }
                                case var case5 when case5 == "cursed":
                                    {
                                        Tags.Add("Cursed");
                                        break;
                                    }
                                case var case6 when case6 == "decoration":
                                    {
                                        Tags.Add("装饰");
                                        break;
                                    }
                                case "entities":
                                    {
                                        Tags.Add("实体");
                                        break;
                                    }
                                case "environment":
                                    {
                                        Tags.Add("环境");
                                        break;
                                    }
                                case var case7 when case7 == "equipment":
                                    {
                                        Tags.Add("装备");
                                        break;
                                    }
                                case "fonts":
                                    {
                                        Tags.Add("字体");
                                        break;
                                    }
                                case "gui":
                                    {
                                        Tags.Add("GUI");
                                        break;
                                    }
                                case "items":
                                    {
                                        Tags.Add("物品");
                                        break;
                                    }
                                case "locale":
                                    {
                                        Tags.Add("本地化");
                                        break;
                                    }
                                case "modded":
                                    {
                                        Tags.Add("Modded");
                                        break;
                                    }
                                case "models":
                                    {
                                        Tags.Add("模型");
                                        break;
                                    }
                                case var case8 when case8 == "realistic":
                                    {
                                        Tags.Add("写实");
                                        break;
                                    }
                                case "simplistic":
                                    {
                                        Tags.Add("扁平");
                                        break;
                                    }
                                case "themed":
                                    {
                                        Tags.Add("主题");
                                        break;
                                    }
                                case "tweaks":
                                    {
                                        Tags.Add("优化");
                                        break;
                                    }
                                case var case9 when case9 == "utility":
                                    {
                                        Tags.Add("实用");
                                        break;
                                    }
                                case var case10 when case10 == "vanilla-like":
                                    {
                                        Tags.Add("类原生");
                                        break;
                                    }
                            }
                        }
                        if (!Tags.Any())
                            Tags.Add("杂项");
                        Tags.Sort();
                        ModLoaders.Sort();
                        #endregion
                    }
                }
                // 保存缓存
                CompProjectCache[Id] = this;
            }
            /// <summary>
        /// 将当前实例转为可用于保存缓存的 Json。
        /// </summary>
            public JObject ToJson()
            {
                var Json = new JObject();
                Json["DataSource"] = FromCurseForge ? "CurseForge" : "Modrinth";
                Json["Type"] = (int)Type;
                Json["Slug"] = Slug;
                Json["Id"] = Id;
                if (CurseForgeFileIds is not null)
                    Json["CurseForgeFileIds"] = new JArray(CurseForgeFileIds);
                Json["RawName"] = RawName;
                Json["Description"] = Description;
                Json["Website"] = Website;
                if (LastUpdate is not null)
                    Json["LastUpdate"] = LastUpdate;
                Json["DownloadCount"] = DownloadCount;
                if (ModLoaders is not null && ModLoaders.Any())
                    Json["ModLoaders"] = new JArray(ModLoaders.Select(m => (int)m));
                Json["Tags"] = new JArray(Tags);
                if (!string.IsNullOrEmpty(LogoUrl))
                    Json["LogoUrl"] = LogoUrl;
                if (GameVersions.Any())
                    Json["GameVersions"] = new JArray(GameVersions);
                Json["CacheTime"] = DateTime.Now; // 用于检查缓存时间
                return Json;
            }
            /// <summary>
        /// 将当前工程信息实例化为控件。
        /// </summary>
            public MyCompItem ToCompItem(bool ShowMcVersionDesc, bool ShowLoaderDesc)
            {
                // 获取版本描述
                string GameVersionDescription;
                if (GameVersions is null || !GameVersions.Any())
                {
                    GameVersionDescription = "仅快照版本"; // #5412
                }
                else
                {
                    var SpaVersions = new List<string>();
                    bool IsOld = false;
                    for (int i = 0, loopTo = GameVersions.Count - 1; i <= loopTo; i++) // 版本号一定为降序
                    {
                        // 获取当前连续的版本号段
                        int StartVersion = GameVersions[i];
                        int EndVersion = GameVersions[i];
                        if (StartVersion < 10) // 如果支持新版本，则不显示 1.9-
                        {
                            if (SpaVersions.Any() && !IsOld)
                            {
                                break;
                            }
                            else
                            {
                                IsOld = true;
                            }
                        }
                        for (int ii = i + 1, loopTo1 = GameVersions.Count - 1; ii <= loopTo1; ii++)
                        {
                            if (GameVersions[ii] != EndVersion - 1)
                                break;
                            EndVersion = GameVersions[ii];
                            i = ii;
                        }
                        // 将版本号段转为描述文本
                        if (StartVersion == EndVersion)
                        {
                            SpaVersions.Add("1." + StartVersion);
                        }
                        else if (ModDownloadLib.McVersionHighest > -1 && StartVersion >= ModDownloadLib.McVersionHighest)
                        {
                            if (EndVersion < 10)
                            {
                                SpaVersions.Clear();
                                SpaVersions.Add("全版本");
                                break;
                            }
                            else
                            {
                                SpaVersions.Add("1." + EndVersion + "+");
                            }
                        }
                        else if (EndVersion < 10)
                        {
                            SpaVersions.Add("1." + StartVersion + "-");
                            break;
                        }
                        else if (StartVersion - EndVersion == 1)
                        {
                            SpaVersions.Add("1." + StartVersion + ", 1." + EndVersion);
                        }
                        else
                        {
                            SpaVersions.Add("1." + StartVersion + "~1." + EndVersion);
                        }
                    }
                    GameVersionDescription = SpaVersions.Join(", ");
                }
                // 获取 Mod 加载器描述
                string ModLoaderDescriptionFull;
                string ModLoaderDescriptionPart;
                var ModLoadersForDesc = new List<CompLoaderType>(ModLoaders);
                if (Conversions.ToBoolean(ModBase.Setup.Get("ToolDownloadIgnoreQuilt")))
                    ModLoadersForDesc.Remove(CompLoaderType.Quilt);
                switch (ModLoadersForDesc.Count)
                {
                    case 0:
                        {
                            if (ModLoaders.Count == 1)
                            {
                                ModLoaderDescriptionFull = "仅 " + ModLoaders.Single().ToString();
                                ModLoaderDescriptionPart = ModLoaders.Single().ToString();
                            }
                            else
                            {
                                ModLoaderDescriptionFull = "未知";
                                ModLoaderDescriptionPart = "";
                            }

                            break;
                        }
                    case 1:
                        {
                            ModLoaderDescriptionFull = "仅 " + ModLoadersForDesc.Single().ToString();
                            ModLoaderDescriptionPart = ModLoadersForDesc.Single().ToString();
                            break;
                        }

                    default:
                        {
                            int MaxVersion = GameVersions.Any() ? GameVersions.Max() : 99;
                            if (Conversions.ToBoolean(ModLoaders.Contains(CompLoaderType.Forge) && (MaxVersion < 14 || ModLoaders.Contains(CompLoaderType.Fabric)) && (MaxVersion < 20 || ModLoaders.Contains(CompLoaderType.NeoForge)) && (MaxVersion < 14 || ModLoaders.Contains(CompLoaderType.Quilt) || (bool)ModBase.Setup.Get("ToolDownloadIgnoreQuilt"))))
                            {
                                ModLoaderDescriptionFull = "任意";
                                ModLoaderDescriptionPart = "";
                            }
                            else
                            {
                                ModLoaderDescriptionFull = ModLoadersForDesc.Join(" / ");
                                ModLoaderDescriptionPart = ModLoadersForDesc.Join(" / ");
                            }

                            break;
                        }
                }
                // 实例化 UI
                var NewItem = new MyCompItem() { Tag = this, Logo = GetControlLogo() };
                var Title = GetControlTitle(true);
                NewItem.Title = Title.Key;
                if (string.IsNullOrEmpty(Title.Value))
                {
                    ((StackPanel)NewItem.LabTitleRaw.Parent).Children.Remove(NewItem.LabTitleRaw);
                }
                else
                {
                    NewItem.SubTitle = Title.Value;
                }
                NewItem.Tags = Tags;
                NewItem.Description = Description.Replace(Constants.vbCr, "").Replace(Constants.vbLf, "");
                // 下边栏
                if (!ShowMcVersionDesc && !ShowLoaderDesc)
                {
                    // 全部隐藏
                    ((Grid)NewItem.PathVersion.Parent).Children.Remove(NewItem.PathVersion);
                    ((Grid)NewItem.LabVersion.Parent).Children.Remove(NewItem.LabVersion);
                    NewItem.ColumnVersion1.Width = new GridLength(0d);
                    NewItem.ColumnVersion2.MaxWidth = 0d;
                    NewItem.ColumnVersion3.Width = new GridLength(0d);
                }
                else if (ShowMcVersionDesc && ShowMcVersionDesc)
                {
                    // 全部显示
                    NewItem.LabVersion.Text = (string.IsNullOrEmpty(ModLoaderDescriptionPart) ? "" : ModLoaderDescriptionPart + " ") + GameVersionDescription;
                }
                else if (ShowMcVersionDesc)
                {
                    // 仅显示版本
                    NewItem.LabVersion.Text = GameVersionDescription;
                }
                else
                {
                    // 仅显示 Mod 加载器
                    NewItem.LabVersion.Text = ModLoaderDescriptionFull;
                }
                NewItem.LabSource.Text = FromCurseForge ? "CurseForge" : "Modrinth";
                if (LastUpdate is not null)
                {
                    NewItem.LabTime.Text = ModBase.GetTimeSpanString((TimeSpan)(LastUpdate - DateTime.Now), true);
                }
                else
                {
                    NewItem.LabTime.Visibility = Visibility.Collapsed;
                    NewItem.ColumnTime1.Width = new GridLength(0d);
                    NewItem.ColumnTime2.Width = new GridLength(0d);
                    NewItem.ColumnTime3.Width = new GridLength(0d);
                }
                NewItem.LabDownload.Text = Conversions.ToString(DownloadCount > 100000000 ? global::System.Math.Round(DownloadCount / 100000000d, 2) + " 亿" : DownloadCount > 100000 ? global::System.Math.Floor(DownloadCount / 10000d) + " 万" : DownloadCount);
                return NewItem;
            }
            public MyListItem ToListItem()
            {
                var Result = new MyListItem();
                Result.Title = TranslatedName;
                Result.Info = Description.Replace(Constants.vbCr, "").Replace(Constants.vbLf, "");
                Result.Logo = LogoUrl;
                Result.Tags = Tags;
                Result.Tag = this;
                return Result;
            }
            public string GetControlLogo()
            {
                if (string.IsNullOrEmpty(LogoUrl))
                {
                    return ModBase.PathImage + "Icons/NoIcon.png";
                }
                else
                {
                    return LogoUrl;
                }
            }
            public KeyValuePair<string, string> GetControlTitle(bool HasModLoaderDescription)
            {
                // 检查下列代码时可以参考 #1567 的测试例
                string Title = RawName;
                List<string> SubtitleList;
                if ((TranslatedName ?? "") == (RawName ?? ""))
                {
                    // 没有中文翻译
                    // 将所有名称分段
                    var NameLists = TranslatedName.Split(new[] { " | ", " - ", "(", ")", "[", "]", "{", "}" }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim(@" /\".ToCharArray())).Where(w => !string.IsNullOrEmpty(w)).ToList();
                    if (NameLists.Count == 1)
                        goto NoSubtitle;
                    // 查找其中的缩写、Forge/Fabric 等版本标记
                    SubtitleList = new List<string>();
                    var NormalNameList = new List<string>();
                    foreach (var Name in NameLists)
                    {
                        string LowerName = Name.ToLower();
                        if ((Name.ToUpper() ?? "") == (Name ?? "") && Name != "FPS" && Name != "HUD")
                        {
                            // 缩写
                            SubtitleList.Add(Name);
                        }
                        else if ((LowerName.Contains("forge") || LowerName.Contains("fabric") || LowerName.Contains("quilt")) && !LowerName.Replace("forge", "").Replace("fabric", "").Replace("quilt", "").RegexCheck("[a-z]+")) // 去掉关键词后没有其他字母
                        {
                            // Forge/Fabric 等版本标记
                            SubtitleList.Add(Name);
                        }
                        else
                        {
                            // 其他部分
                            NormalNameList.Add(Name);
                        }
                    }
                    // 根据分类后的结果处理
                    if (!NormalNameList.Any() || !SubtitleList.Any())
                        goto NoSubtitle;
                    // 同时包含 NormalName 和 Subtitle
                    Title = NormalNameList.Join(" - ");
                }
                else
                {
                    // 有中文翻译
                    // 尝试将文本分为三段：Title (EnglishName) - Suffix
                    // 检查时注意 Carpet：它没有中文译名，但有 Suffix
                    Title = TranslatedName.BeforeFirst(" (").BeforeFirst(" - ");
                    string Suffix = "";
                    if (TranslatedName.AfterLast(")").Contains(" - "))
                        Suffix = TranslatedName.AfterLast(")").AfterLast(" - ");
                    string EnglishName = TranslatedName;
                    if (!string.IsNullOrEmpty(Suffix))
                        EnglishName = EnglishName.Replace(" - " + Suffix, "");
                    EnglishName = EnglishName.Replace(Title, "").Trim('(', ')', ' ');
                    // 中段的额外信息截取
                    SubtitleList = EnglishName.Split(new[] { " | ", " - ", "(", ")", "[", "]", "{", "}" }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim(" /".ToCharArray())).Where(w => !string.IsNullOrEmpty(w)).ToList();
                    if (SubtitleList.Count > 1 && !SubtitleList.Any(s => s.ToLower().Contains("forge") || s.ToLower().Contains("fabric") || s.ToLower().Contains("quilt")) && !(SubtitleList.Count == 2 && (SubtitleList.Last().ToUpper() ?? "") == (SubtitleList.Last() ?? ""))) // 不是标注 XX 版
                                                                                                                                                                                                                                                                                  // 不是缩写
                    {
                        SubtitleList = new List<string>() { EnglishName }; // 使用原名
                    }
                    // 添加后缀
                    if (!string.IsNullOrEmpty(Suffix))
                        SubtitleList.Add(Suffix);
                }
                SubtitleList = SubtitleList.Distinct().ToList();
                // 设置标题与描述
                string Subtitle = "";
                if (SubtitleList.Any())
                {
                    foreach (var Ex in SubtitleList)
                    {
                        bool IsModLoaderDescription = Ex.ToLower().Contains("forge") || Ex.ToLower().Contains("fabric") || Ex.ToLower().Contains("quilt");
                        // 是否显示 ModLoader 信息
                        if (!HasModLoaderDescription && IsModLoaderDescription)
                            continue;
                        // 去除 “Forge/Fabric” 这一无意义提示
                        if (Ex.Length < 16 && Ex.ToLower().Contains("fabric") && Ex.ToLower().Contains("forge"))
                            continue;
                        // 将 “Forge” 等提示改为 “Forge 版”
                        if (IsModLoaderDescription && !Ex.Contains("版") && Ex.ToLower().Replace("forge", "").Replace("fabric", "").Replace("quilt", "").Length <= 3)
                        {
                            Ex = Ex.Replace("Edition", "").Replace("edition", "").Trim().Capitalize() + " 版";
                        }
                        // 将 “forge” 等词语的首字母大写
                        Ex = Ex.Replace("forge", "Forge").Replace("neo", "Neo").Replace("fabric", "Fabric").Replace("quilt", "Quilt");
                        Subtitle += "  |  " + Ex.Trim();
                    }
                }
                else
                {
                NoSubtitle:
                    ;

                    Subtitle = "";
                }
                return new KeyValuePair<string, string>(Title, Subtitle);
            }

            // 辅助函数

            /// <summary>
        /// 检查是否与某个 Project 是相同的工程，只是在不同的网站。
        /// </summary>
            public bool IsLike(CompProject Project)
            {
                if ((Id ?? "") == (Project.Id ?? ""))
                    return true; // 相同实例
                                 // 提取字符串中的字母和数字
                string GetRaw(string Data)
                {
                    var Result = new StringBuilder();
                    foreach (char r in Data.Where(c => char.IsLetterOrDigit(c)))
                        Result.Append(r);
                    return Result.ToString().ToLower();
                };
                // 来自不同的网站
                if (FromCurseForge == Project.FromCurseForge)
                    return false;
                // Mod 加载器一致
                if (ModLoaders.Count != Project.ModLoaders.Count || ModLoaders.Except(Project.ModLoaders).Any())
                    return false;
                // MC 版本一致
                if (GameVersions.Count != Project.GameVersions.Count || GameVersions.Except(Project.GameVersions).Any())
                    return false;
                // 最近更新时间差距在一周以内
                if (LastUpdate is not null && Project.LastUpdate is not null && Math.Abs((LastUpdate - Project.LastUpdate).Value.TotalDays) > 7d)
                    return false;
                // MCMOD 翻译名 / 原名 / 描述文本 / Slug 的英文部分相同
                if ((TranslatedName ?? "") == (Project.TranslatedName ?? "") || (RawName ?? "") == (Project.RawName ?? "") || (Description ?? "") == (Project.Description ?? "") || (GetRaw(Slug) ?? "") == (GetRaw(Project.Slug) ?? ""))
                {
                    ModBase.Log($"[Comp] 将 {RawName} ({Slug}) 与 {Project.RawName} ({Project.Slug}) 认定为相似工程");
                    // 如果只有一个有 DatabaseEntry，设置给另外一个
                    if (DatabaseEntry is null && Project.DatabaseEntry is not null)
                        DatabaseEntry = Project.DatabaseEntry;
                    if (DatabaseEntry is not null && Project.DatabaseEntry is null)
                        Project.DatabaseEntry = DatabaseEntry;
                    return true;
                }
                return false;
            }

            public override string ToString()
            {
                return $"{Id} ({Slug}): {RawName}";
            }
            public override bool Equals(object obj)
            {
                CompProject project = obj as CompProject;
                return project is not null && (Id ?? "") == (project.Id ?? "");
            }
            public static bool operator ==(CompProject left, CompProject right)
            {
                return EqualityComparer<CompProject>.Default.Equals(left, right);
            }
            public static bool operator !=(CompProject left, CompProject right)
            {
                return !(left == right);
            }

        }

        // 输入与输出

        public class CompProjectRequest
        {

            // 结果要求

            /// <summary>
        /// 加载后应输出到的结果存储器。
        /// </summary>
            public CompProjectStorage Storage;
            /// <summary>
        /// 应当尽量达成的结果数量。
        /// </summary>
            public int TargetResultCount;
            /// <summary>
        /// 根据加载位置记录，是否还可以继续获取内容。
        /// </summary>
            public bool CanContinue
            {
                get
                {
                    if (Tag.StartsWithF("/") || !Source.HasFlag(CompSourceType.CurseForge))
                        Storage.CurseForgeTotal = 0;
                    if (Tag.EndsWithF("/") || !Source.HasFlag(CompSourceType.Modrinth))
                        Storage.ModrinthTotal = 0;
                    if (Storage.CurseForgeTotal == -1 || Storage.ModrinthTotal == -1)
                        return true;
                    return Storage.CurseForgeOffset < Storage.CurseForgeTotal || Storage.ModrinthOffset < Storage.ModrinthTotal;
                }
            }

            // 输入内容

            /// <summary>
        /// 筛选资源种类。
        /// </summary>
            public CompType Type;
            /// <summary>
        /// 筛选资源标签。空字符串代表不限制。格式例如 "406/worldgen"，分别是 CurseForge 和 Modrinth 的 ID。
        /// </summary>
            public string Tag = "";
            /// <summary>
        /// 筛选 Mod 加载器类别。
        /// </summary>
            public CompLoaderType ModLoader = CompLoaderType.Any;
            /// <summary>
        /// 筛选 MC 版本。
        /// </summary>
            public string GameVersion = null;
            /// <summary>
        /// 搜索的文本内容。
        /// </summary>
            public string SearchText = null;
            /// <summary>
        /// 允许的来源。
        /// </summary>
            public CompSourceType Source = CompSourceType.Any;
            /// <summary>
        /// 构造函数。
        /// </summary>
            public CompProjectRequest(CompType Type, CompProjectStorage Storage, int TargetResultCount)
            {
                this.Type = Type;
                this.Storage = Storage;
                this.TargetResultCount = TargetResultCount;
            }

            // 构造请求

            /// <summary>
        /// 获取对应的 CurseForge API 请求链接。若返回 Nothing 则为不进行 CurseForge 请求。
        /// </summary>
            public string GetCurseForgeAddress()
            {
                if (!Source.HasFlag(CompSourceType.CurseForge))
                    return null;
                if (Tag.StartsWithF("/"))
                    Storage.CurseForgeTotal = 0;
                if (Storage.CurseForgeTotal > -1 && Storage.CurseForgeTotal <= Storage.CurseForgeOffset)
                    return null;
                // 应用筛选参数
                string Address = $"https://api.curseforge.com/v1/mods/search?gameId=432&sortField=2&sortOrder=desc&pageSize={CompPageSize}";
                switch (Type)
                {
                    case CompType.Mod:
                        {
                            Address += "&classId=6";
                            break;
                        }
                    case CompType.ModPack:
                        {
                            Address += "&classId=4471";
                            break;
                        }
                    case CompType.ResourcePack:
                        {
                            Address += "&classId=12";
                            break;
                        }
                    case CompType.Shader:
                        {
                            Address += "&classId=6552";
                            break;
                        }
                }
                Address += "&categoryId=" + (string.IsNullOrEmpty(Tag) ? "0" : Tag.BeforeFirst("/"));
                if (ModLoader != CompLoaderType.Any)
                    Address += "&modLoaderType=" + (int)ModLoader;
                if (!string.IsNullOrEmpty(GameVersion))
                    Address += "&gameVersion=" + GameVersion;
                if (!string.IsNullOrEmpty(SearchText))
                    Address += "&searchFilter=" + WebUtility.UrlEncode(SearchText);
                if (Storage.CurseForgeOffset > 0)
                    Address += "&index=" + Storage.CurseForgeOffset;
                return Address;
            }
            /// <summary>
        /// 获取对应的 Modrinth API 请求链接。若返回 Nothing 则为不进行 Modrinth 请求。
        /// </summary>
            public string GetModrinthAddress()
            {
                if (!Source.HasFlag(CompSourceType.Modrinth))
                    return null;
                if (Tag.EndsWithF("/"))
                    Storage.ModrinthTotal = 0;
                if (Storage.ModrinthTotal > -1 && Storage.ModrinthTotal <= Storage.ModrinthOffset)
                    return null;
                // 应用筛选参数
                string Address = $"https://api.modrinth.com/v2/search?limit={CompPageSize}&index=relevance";
                if (!string.IsNullOrEmpty(SearchText))
                    Address += "&query=" + WebUtility.UrlEncode(SearchText);
                if (Storage.ModrinthOffset > 0)
                    Address += "&offset=" + Storage.ModrinthOffset;
                // facets=[["categories:'game-mechanics'"],["categories:'forge'"],["versions:1.19.3"],["project_type:mod"]]
                var Facets = new List<string>();
                Facets.Add($"[\"project_type:{ModBase.GetStringFromEnum(Type).ToLower()}\"]");
                if (!string.IsNullOrEmpty(Tag))
                    Facets.Add($"[\"categories:'{Tag.AfterLast("/")}'\"]");
                if (ModLoader != CompLoaderType.Any)
                    Facets.Add($"[\"categories:'{ModBase.GetStringFromEnum(ModLoader).ToLower()}'\"]");
                if (!string.IsNullOrEmpty(GameVersion))
                    Facets.Add($"[\"versions:'{GameVersion}'\"]");
                Address += "&facets=[" + string.Join(",", Facets) + "]";
                return Address;
            }

            // 相同判断
            public override bool Equals(object obj)
            {
                CompProjectRequest request = obj as CompProjectRequest;
                return request is not null && Type == request.Type && TargetResultCount == request.TargetResultCount && (Tag ?? "") == (request.Tag ?? "") && ModLoader == request.ModLoader && Source == request.Source && (GameVersion ?? "") == (request.GameVersion ?? "") && (SearchText ?? "") == (request.SearchText ?? "");
            }
            public static bool operator ==(CompProjectRequest left, CompProjectRequest right)
            {
                return EqualityComparer<CompProjectRequest>.Default.Equals(left, right);
            }
            public static bool operator !=(CompProjectRequest left, CompProjectRequest right)
            {
                return !(left == right);
            }

        }
        public class CompProjectStorage
        {

            // 加载位置记录

            public int CurseForgeOffset = 0;
            public int CurseForgeTotal = -1;

            public int ModrinthOffset = 0;
            public int ModrinthTotal = -1;

            // 结果列表

            /// <summary>
        /// 可供展示的所有工程的列表。
        /// </summary>
            public List<CompProject> Results = new List<CompProject>();
            /// <summary>
        /// 当前的错误信息。如果没有则为 Nothing。
        /// </summary>
            public string ErrorMessage = null;

        }

        // 实际的获取

        private const int CompPageSize = 40;
        /// <summary>
    /// 已知工程信息的缓存。
    /// </summary>
        public static Dictionary<string, CompProject> CompProjectCache = new Dictionary<string, CompProject>();
        /// <summary>
    /// 根据搜索请求获取一系列的工程列表。需要基于加载器运行。
    /// </summary>
        public static void CompProjectsGet(ModLoader.LoaderTask<CompProjectRequest, int> Task)
        {
            var Storage = Task.Input.Storage; // 避免多线程问题

            if (Task.Input.Storage.Results.Count >= Task.Input.TargetResultCount)
            {
                ModBase.Log($"[Comp] 已有 {Task.Input.Storage.Results.Count} 个结果，多于所需的 {Task.Input.TargetResultCount} 个结果，结束处理");
                return;
            }
            else if (!Task.Input.CanContinue)
            {
                if (!Task.Input.Storage.Results.Any())
                {
                    throw new Exception("没有符合条件的结果");
                }
                else
                {
                    ModBase.Log($"[Comp] 已有 {Task.Input.Storage.Results.Count} 个结果，少于所需的 {Task.Input.TargetResultCount} 个结果，但无法继续获取，结束处理");
                    return;
                }
            }

            #region 拒绝 1.13- Quilt（这个版本根本没有 Quilt）

            if (Task.Input.ModLoader == CompLoaderType.Quilt && ModMinecraft.VersionSortInteger(Task.Input.GameVersion ?? "1.15", "1.14") == -1)
            {
                throw new Exception("Quilt 不支持 Minecraft " + Task.Input.GameVersion);
            }

            #endregion

            #region 处理搜索文本，赋值回 Task.Input.SearchText

            string RawFilter = (Task.Input.SearchText ?? "").Trim();
            Task.Input.SearchText = RawFilter;
            RawFilter = RawFilter.ToLower();
            ModBase.Log("[Comp] 工程列表搜索原始文本：" + RawFilter);

            // 中文请求关键字处理
            bool IsChineseSearch = RawFilter.RegexCheck(@"[\u4e00-\u9fbb]") && !string.IsNullOrEmpty(RawFilter);
            if (IsChineseSearch && Task.Input.Type == CompType.Mod)
            {
                // 构造搜索请求
                var SearchEntries = new List<ModBase.SearchEntry<CompDatabaseEntry>>();
                foreach (var Entry in CompDatabase)
                {
                    if (Entry.ChineseName.Contains("动态的树"))
                        continue; // 这玩意儿附属太多了
                    SearchEntries.Add(new ModBase.SearchEntry<CompDatabaseEntry>()
                    {
                        Item = Entry,
                        SearchSource = new List<KeyValuePair<string, double>>() { new KeyValuePair<string, double>(Entry.ChineseName + (Entry.CurseForgeSlug ?? "") + (Entry.ModrinthSlug ?? ""), 1d) }
                    });
                }
                // 获取搜索结果
                var SearchResults = ModBase.Search(SearchEntries, Task.Input.SearchText, 3);
                if (!SearchResults.Any())
                    throw new Exception("无搜索结果，请尝试搜索英文名称");
                string SearchResult = "";
                for (int i = 0, loopTo = Math.Min(4, SearchResults.Count - 1); i <= loopTo; i++) // 就算全是准确的，也最多只要 5 个
                {
                    if (!SearchResults[i].AbsoluteRight && i >= Math.Min(2, SearchResults.Count - 1))
                        break; // 把 3 个结果拼合以提高准确度
                    if (SearchResults[i].Item.CurseForgeSlug is not null)
                        SearchResult += SearchResults[i].Item.CurseForgeSlug.Replace("-", " ").Replace("/", " ") + " ";
                    if (SearchResults[i].Item.ModrinthSlug is not null)
                        SearchResult += SearchResults[i].Item.ModrinthSlug.Replace("-", " ").Replace("/", " ") + " ";
                    SearchResult += SearchResults[i].Item.ChineseName.AfterLast(" (").TrimEnd(Conversions.ToChar(") ")).BeforeFirst(" - ").Replace(":", "").Replace("(", "").Replace(")", "").ToLower().Replace("/", " ") + " ";
                }
                ModBase.Log("[Comp] 中文搜索原始关键词：" + SearchResult, ModBase.LogLevel.Developer);
                // 去除常见连接词
                string RealFilter = "";
                foreach (var Word in SearchResult.Split(" "))
                {
                    if (new[] { "the", "of", "a", "mod", "and" }.Contains(Word.ToLowerInvariant()) || ModBase.Val(Word) > 0d)
                        continue;
                    if (SearchResult.Split(" ").Count() > 3 && new[] { "ftb" }.Contains(Word.ToLower()))
                        continue;
                    RealFilter += Word.TrimStart(Conversions.ToChar("{[(")).TrimEnd(Conversions.ToChar("}])")) + " ";
                }
                Task.Input.SearchText = RealFilter;
                ModBase.Log("[Comp] 中文搜索最终关键词：" + RealFilter, ModBase.LogLevel.Developer);
            }

            // 驼峰英文请求关键字处理
            string SpacedKeywords = Task.Input.SearchText.RegexReplace("([A-Z]+|[a-z]+?)(?=[A-Z]+[a-z]+[a-z ]*)", "$& ");
            string ConnectedKeywords = Task.Input.SearchText.Replace(" ", "");
            string AllPossibleKeywords = (SpacedKeywords + " " + (IsChineseSearch ? Task.Input.SearchText : ConnectedKeywords + " " + RawFilter)).ToLower();

            // 最终处理关键字：分割、去重
            var RightKeywords = new List<string>();
            foreach (var Keyword in AllPossibleKeywords.Split(" "))
            {
                Keyword = Keyword.Trim('[', ']');
                if (string.IsNullOrEmpty(Keyword))
                    continue;
                if (new[] { "forge", "fabric", "for", "mod", "quilt" }.Contains(Keyword)) // #208
                {
                    ModBase.Log("[Comp] 已跳过搜索关键词：" + Keyword, ModBase.LogLevel.Developer);
                    continue;
                }
                RightKeywords.Add(Keyword);
            }
            if (RawFilter.Length > 0 && !RightKeywords.Any())
            {
                Task.Input.SearchText = RawFilter; // 全都被过滤掉了
            }
            else
            {
                Task.Input.SearchText = RightKeywords.Distinct().ToList().Join(" ").ToLower();
            }

            // 例外项：OptiForge、OptiFabric（拆词后因为包含 Forge/Fabric 导致无法搜到实际的 Mod）
            if (RawFilter.Replace(" ", "").ContainsF("optiforge", true))
                Task.Input.SearchText = "optiforge";
            if (RawFilter.Replace(" ", "").ContainsF("optifabric", true))
                Task.Input.SearchText = "optifabric";
            ModBase.Log("[Comp] 工程列表搜索最终文本：" + Task.Input.SearchText, ModBase.LogLevel.Debug);
            Task.Progress = 0.1d;

            #endregion

            var RealResults = new List<CompProject>();
        Retry:
            ;

            var RawResults = new List<CompProject>();
            Exception Error = null;

            #region 从 CurseForge 和 Modrinth 获取结果列表，存储于 RawResults

            Thread CurseForgeThread = null;
            Thread ModrinthThread = null;
            var ResultsLock = new object();

            try
            {

                // 启动 CurseForge 线程
                string CurseForgeUrl = Task.Input.GetCurseForgeAddress();
                bool CurseForgeFailed = false;
                if (CurseForgeUrl is not null)
                {
                    // 获取工程列表
                    // 更新结果
                    CurseForgeThread = ModBase.RunInNewThread(() => { try { ModBase.Log("[Comp] 开始从 CurseForge 获取工程列表：" + CurseForgeUrl); JObject RequestResult = (JObject)ModDownload.DlModRequest(CurseForgeUrl, IsJson: true); Task.Progress += 0.2d; var ProjectList = new List<CompProject>(); foreach (JObject JsonEntry in RequestResult["data"]) ProjectList.Add(new CompProject(JsonEntry)); lock (ResultsLock) RawResults.AddRange(ProjectList); Storage.CurseForgeOffset += ProjectList.Count; Storage.CurseForgeTotal = RequestResult["pagination"]["totalCount"].ToObject<int>(); ModBase.Log($"[Comp] 从 CurseForge 获取到了 {ProjectList.Count} 个工程（已获取 {Storage.CurseForgeOffset} 个，共 {Storage.CurseForgeTotal} 个）"); } catch (Exception ex) { ModBase.Log(ex, "从 CurseForge 获取工程列表失败"); Storage.CurseForgeTotal = -1; Error = ex; CurseForgeFailed = true; } }, "CurseForge Project Request"); // Storage.CurseForgeOffset
                }

                // 启动 Modrinth 线程
                string ModrinthUrl = Task.Input.GetModrinthAddress();
                bool ModrinthFailed = false;
                if (ModrinthUrl is not null)
                {
                    // 更新结果
                    ModrinthThread = ModBase.RunInNewThread(() => { try { ModBase.Log("[Comp] 开始从 Modrinth 获取工程列表：" + ModrinthUrl); JObject RequestResult = (JObject)ModDownload.DlModRequest(ModrinthUrl, IsJson: true); Task.Progress += 0.2d; var ProjectList = new List<CompProject>(); foreach (JObject JsonEntry in RequestResult["hits"]) ProjectList.Add(new CompProject(JsonEntry)); lock (ResultsLock) { foreach (var Project in ProjectList) { if (Task.Input.Type == CompType.Mod && !Project.ModLoaders.Any()) continue; RawResults.Add(Project); } } Storage.ModrinthOffset += ProjectList.Count; Storage.ModrinthTotal = RequestResult["total_hits"].ToObject<int>(); ModBase.Log($"[Comp] 从 Modrinth 获取到了 {ProjectList.Count} 个工程（已获取 {Storage.ModrinthOffset} 个，共 {Storage.ModrinthTotal} 个）"); } catch (Exception ex) { ModBase.Log(ex, "从 Modrinth 获取工程列表失败"); Storage.ModrinthTotal = -1; Error = ex; ModrinthFailed = true; } }, "Modrinth Project Request"); // 过滤插件（#2458）
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   // Storage.ModrinthOffset
                }

                // 等待线程结束
                if (CurseForgeThread is not null)
                    CurseForgeThread.Join();
                if (Task.IsAborted)
                    return; // 会自动触发 Finally
                if (ModrinthThread is not null)
                    ModrinthThread.Join();
                if (Task.IsAborted)
                    return;

                // 确保存在结果
                Storage.ErrorMessage = null;
                if (!RawResults.Any())
                {
                    if (Error is not null)
                    {
                        throw Error;
                    }
                    else if (IsChineseSearch && Task.Input.Type != CompType.Mod)
                    {
                        throw new Exception($"{(Task.Input.Type == CompType.ModPack ? "整合包" : "资源包")}搜索仅支持英文");
                    }
                    else if (Task.Input.Source == CompSourceType.CurseForge && Task.Input.Tag.StartsWithF("/"))
                    {
                        throw new Exception("CurseForge 不兼容所选的类型");
                    }
                    else if (Task.Input.Source == CompSourceType.Modrinth && Task.Input.Tag.EndsWithF("/"))
                    {
                        throw new Exception("Modrinth 不兼容所选的类型");
                    }
                    else
                    {
                        throw new Exception("没有搜索结果");
                    }
                }
                else if (Error is not null)
                {
                    // 有结果但是有错误
                    if (CurseForgeFailed)
                    {
                        Storage.ErrorMessage = $"无法连接到 CurseForge，所以目前仅显示了来自 Modrinth 的内容，结果可能不全。{Constants.vbCrLf}请尝试使用 VPN 或加速器以改善网络。";
                    }
                    else
                    {
                        Storage.ErrorMessage = $"无法连接到 Modrinth，所以目前仅显示了来自 CurseForge 的内容，结果可能不全。{Constants.vbCrLf}请尝试使用 VPN 或加速器以改善网络。";
                    }
                }
            }
            finally
            {
                if (CurseForgeThread is not null)
                    CurseForgeThread.Interrupt();
                if (ModrinthThread is not null)
                    ModrinthThread.Interrupt();
            }

            #endregion

            #region 提取非重复项，存储于 RealResults

            // 将 Modrinth 排在 CurseForge 的前面，避免加载结束顺序不同导致排名不同
            // 这样做的话，去重后将优先保留 CurseForge 内容
            RawResults = RawResults.Where(x => !x.FromCurseForge).Concat(RawResults.Where(x => x.FromCurseForge)).ToList();
            // RawResults 去重
            RawResults = RawResults.Distinct((a, b) => a.IsLike(b));
            // 已有内容去重
            RawResults = RawResults.Where(r => !RealResults.Any(b => r.IsLike(b)) && !Storage.Results.Any(b => r.IsLike(b))).ToList();
            // 加入列表
            RealResults.AddRange(RawResults);
            ModBase.Log($"[Comp] 去重、筛选后累计新增结果 {RealResults.Count} 个");

            #endregion

            #region 检查结果数量，如果不足且可继续，会继续加载下一页

            if (RealResults.Count + Storage.Results.Count < Task.Input.TargetResultCount)
            {
                ModBase.Log($"[Comp] 总结果数需求最少 {Task.Input.TargetResultCount} 个，仅获得了 {RealResults.Count + Storage.Results.Count} 个");
                if (Task.Input.CanContinue && Error is null) // 如果有下载源失败则不再重试，这时候重试可能导致无限循环
                {
                    ModBase.Log("[Comp] 将继续尝试加载下一页");
                    goto Retry;
                }
                else
                {
                    ModBase.Log("[Comp] 无法继续加载，将强制结束");
                }
            }

            #endregion

            #region 将结果排序并添加

            var Scores = new Dictionary<CompProject, double>(); // 排序分
            if (string.IsNullOrEmpty(Task.Input.SearchText))
            {
                // 如果没有搜索文本，按下载量将结果排序
                foreach (CompProject Result in RealResults)
                    Scores.Add(Result, Result.DownloadCount * (Result.FromCurseForge ? 1 : 5));
            }
            else
            {
                // 如果有搜索文本，按关联度将结果排序
                // 排序分 = 搜索相对相似度 (1) + 下载量权重 (对数，10 亿时为 1) + 有中文名 (0.2)
                var Entry = new List<ModBase.SearchEntry<CompProject>>();
                foreach (CompProject Result in RealResults)
                {
                    Scores.Add(Result, (Result.WikiId > 0 ? 0.2d : 0d) + Math.Log10(Math.Max(Result.DownloadCount, 1) * (Result.FromCurseForge ? 1 : 5)) / 9d);
                    Entry.Add(new ModBase.SearchEntry<CompProject>() { Item = Result, SearchSource = new List<KeyValuePair<string, double>>() { new KeyValuePair<string, double>(IsChineseSearch ? Result.TranslatedName : Result.RawName, 1d), new KeyValuePair<string, double>(Result.Description, 0.05d) } });
                }
                var SearchResult = ModBase.Search(Entry, RawFilter, 101, -1);
                foreach (var OneResult in SearchResult)
                    Scores[OneResult.Item] += OneResult.Similarity / SearchResult[0].Similarity; // 最高 1 分的相似度分
            }
            // 根据排序分得出结果并添加
            Storage.Results.AddRange(Scores.OrderByDescending(s => s.Value).Select(r => r.Key));

            #endregion

        }

        #endregion

        #region CompFile | 文件信息

        // 类定义

        public enum CompFileStatus
        {
            Release = 1, // 枚举值来源：https://docs.curseforge.com/#tocS_FileReleaseType
            Beta = 2,
            Alpha = 3
        }
        public class CompFile
        {

            // 源信息

            /// <summary>
        /// 文件的种类。
        /// </summary>
            public readonly CompType Type;
            /// <summary>
        /// 该文件来自 CurseForge 还是 Modrinth。
        /// </summary>
            public readonly bool FromCurseForge;
            /// <summary>
        /// 用于唯一性鉴别该文件的 ID。CurseForge 中为 123456 的大整数，Modrinth 中为英文乱码的 Version 字段。
        /// </summary>
            public readonly string Id;

            // 描述性信息

            /// <summary>
        /// 文件描述名（并非文件名，是自定义的字段）。对很多 Mod，这会给出 Mod 版本号。
        /// </summary>
            public string DisplayName;
            /// <summary>
        /// 发布时间。
        /// </summary>
            public readonly DateTime ReleaseDate;
            /// <summary>
        /// 下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量，且 CurseForge 可能错误地返回 0。
        /// </summary>
            public readonly int DownloadCount;
            /// <summary>
        /// 支持的 Mod 加载器列表。可能为空。
        /// </summary>
            public readonly List<CompLoaderType> ModLoaders;
            /// <summary>
        /// 支持的游戏版本列表。类型包括："1.18.5"，"1.18"，"1.18 预览版"，"21w15a"，"未知版本"。
        /// </summary>
            public readonly List<string> GameVersions;
            /// <summary>
        /// 发布状态：Release/Beta/Alpha。
        /// </summary>
            public readonly CompFileStatus Status;
            /// <summary>
        /// 发布状态的友好描述。例如："正式版"，"Beta 版"。
        /// </summary>
            public string StatusDescription
            {
                get
                {
                    switch (Status)
                    {
                        case CompFileStatus.Release:
                            {
                                return "正式版";
                            }
                        case CompFileStatus.Beta:
                            {
                                return ModBase.ModeDebug ? "Beta 版" : "测试版";
                            }

                        default:
                            {
                                return ModBase.ModeDebug ? "Alpha 版" : "测试版";
                            }
                    }
                }
            }

            // 下载信息
            /// <summary>
        /// 下载信息是否可用。
        /// </summary>
            public bool Available
            {
                get
                {
                    return FileName is not null && DownloadUrls is not null;
                }
            }
            /// <summary>
        /// 下载的文件名。
        /// </summary>
            public readonly string FileName = null;
            /// <summary>
        /// 文件所有可能的下载源。
        /// </summary>
            public List<string> DownloadUrls;
            /// <summary>
        /// 文件的 SHA1 或 MD5。
        /// </summary>
            public readonly string Hash = null;
            /// <summary>
        /// 该文件的所有依赖工程的原始 ID。
        /// 这些 ID 可能没有加载，在加载后会添加到 Dependencies 中（主要是因为 Modrinth 返回的是字符串 ID 而非 Slug，导致 Project.Id 查询不到）。
        /// </summary>
            public readonly List<string> RawDependencies = new List<string>();
            /// <summary>
        /// 该文件的所有依赖工程的 Project.Id。
        /// </summary>
            public readonly List<string> Dependencies = new List<string>();
            /// <summary>
        /// 获取下载信息。
        /// </summary>
        /// <param name="LocalAddress">目标本地文件夹，或完整的文件路径。会自动判断类型。</param>
            public ModNet.NetFile ToNetFile(string LocalAddress)
            {
                return new ModNet.NetFile(DownloadUrls, LocalAddress + (LocalAddress.EndsWithF(@"\") ? FileName : ""), new ModBase.FileChecker(Hash: Hash), UseBrowserUserAgent: true);
            }

            // 实例化

            /// <summary>
        /// 从文件 Json 中初始化实例。若出错会抛出异常。
        /// </summary>
            public CompFile(JObject Data, CompType Type)
            {
                this.Type = Type;
                if (Data.ContainsKey("FromCurseForge"))
                {
                    #region CompJson
                    FromCurseForge = Data["FromCurseForge"].ToObject<bool>();
                    Id = Data["Id"].ToString();
                    DisplayName = Data["DisplayName"].ToString();
                    ReleaseDate = Data["ReleaseDate"].ToObject<DateTime>();
                    DownloadCount = Data["DownloadCount"].ToObject<int>();
                    Status = (CompFileStatus)Data["Status"].ToObject<int>();
                    if (Data.ContainsKey("FileName"))
                        FileName = Data["FileName"].ToString();
                    if (Data.ContainsKey("DownloadUrls"))
                        DownloadUrls = Data["DownloadUrls"].ToObject<List<string>>();
                    if (Data.ContainsKey("ModLoaders"))
                        ModLoaders = Data["ModLoaders"].ToObject<List<CompLoaderType>>();
                    if (Data.ContainsKey("Hash"))
                        Hash = Data["Hash"].ToString();
                    if (Data.ContainsKey("GameVersions"))
                        GameVersions = Data["GameVersions"].ToObject<List<string>>();
                    if (Data.ContainsKey("RawDependencies"))
                        RawDependencies = Data["RawDependencies"].ToObject<List<string>>();
                    if (Data.ContainsKey("Dependencies"))
                        Dependencies = Data["Dependencies"].ToObject<List<string>>();
                }
                #endregion
                else
                {
                    FromCurseForge = Data.ContainsKey("gameId");
                    if (FromCurseForge)
                    {
                        #region CurseForge
                        // 简单信息
                        Id = (string)Data["id"];
                        DisplayName = Data["displayName"].ToString().Replace("	", "").Trim(' ');
                        ReleaseDate = (DateTime)Data["fileDate"];
                        Status = (CompFileStatus)Data["releaseType"].ToObject<int>();
                        DownloadCount = (int)Data["downloadCount"];
                        FileName = (string)Data["fileName"];
                        Hash = (string)((JArray)Data["hashes"]).ToList().FirstOrDefault(s => s["algo"].ToObject<int>() == 1)?["value"];
                        if (Hash is null)
                            Hash = (string)((JArray)Data["hashes"]).ToList().FirstOrDefault(s => s["algo"].ToObject<int>() == 2)?["value"];
                        // DownloadAddress
                        string Url = Data["downloadUrl"].ToString();
                        if (string.IsNullOrEmpty(Url))
                            Url = $"https://media.forgecdn.net/files/{Conversions.ToInteger(Id.ToString().Substring(0, 4))}/{Conversions.ToInteger(Id.ToString().Substring(4))}/{FileName}";
                        Url = Url.Replace(FileName, WebUtility.UrlEncode(FileName)); // 对文件名进行编码
                        DownloadUrls = HandleCurseForgeDownloadUrls(Url); // 对脑残 CurseForge 的下载地址进行多种修正
                        DownloadUrls.AddRange(DownloadUrls.Select(u => ModDownload.DlSourceModGet(u)).ToList()); // 添加镜像源，这个写法是为了让镜像源排在后面
                        DownloadUrls = DownloadUrls.Distinct().ToList(); // 最终去重
                                                                         // Dependencies
                        if (Type == CompType.Mod)
                        {
                            RawDependencies = Data["dependencies"].Where(d => d["relationType"].ToObject<int>() == 3 && d["modId"].ToObject<int>() != 306612 && d["modId"].ToObject<int>() != 634179).Select(d => d["modId"].ToString()).ToList(); // 种类为依赖
                                                                                                                                                                                                                                                   // 排除 Fabric API 和 Quilt API
                        }
                        // GameVersions
                        var RawVersions = Data["gameVersions"].Select(t => t.ToString().Trim().ToLower()).ToList();
                        GameVersions = RawVersions.Where(v => v.StartsWithF("1.")).Select(v => v.Replace("-snapshot", " 预览版")).ToList();
                        if (GameVersions.Count > 1)
                        {
                            GameVersions = GameVersions.Sort(ModMinecraft.VersionSortBoolean).ToList();
                            if (Type == CompType.ModPack)
                                GameVersions = new List<string>() { GameVersions[0] };
                        }
                        else if (GameVersions.Count == 1)
                        {
                            GameVersions = GameVersions.ToList();
                        }
                        else
                        {
                            GameVersions = new List<string>() { "未知版本" };
                        }
                        // ModLoaders
                        ModLoaders = new List<CompLoaderType>();
                        if (RawVersions.Contains("forge"))
                            ModLoaders.Add(CompLoaderType.Forge);
                        if (RawVersions.Contains("fabric"))
                            ModLoaders.Add(CompLoaderType.Fabric);
                        if (RawVersions.Contains("quilt"))
                            ModLoaders.Add(CompLoaderType.Quilt);
                        if (RawVersions.Contains("neoforge"))
                            ModLoaders.Add(CompLoaderType.NeoForge);
                    }
                    #endregion
                    else
                    {
                        #region Modrinth
                        // 简单信息
                        Id = (string)Data["id"];
                        DisplayName = Data["name"].ToString().Replace("	", "").Trim(' ');
                        ReleaseDate = (DateTime)Data["date_published"];
                        Status = Data["version_type"].ToString() == "release" ? CompFileStatus.Release : Data["version_type"].ToString() == "beta" ? CompFileStatus.Beta : CompFileStatus.Alpha;
                        DownloadCount = (int)Data["downloads"];
                        if (((JArray)Data["files"]).Any()) // 可能为空
                        {
                            var File = Data["files"][0];
                            FileName = (string)File["filename"];
                            DownloadUrls = new List<string>() { (string)File["url"], ModDownload.DlSourceModGet((string)File["url"]) }.Distinct().ToList(); // 同时添加了镜像源
                            Hash = (string)File["hashes"]["sha1"];
                        }
                        // Dependencies
                        if (Type == CompType.Mod)
                        {
                            RawDependencies = Data["dependencies"].Where(d => (string)d["dependency_type"] == "required" && (string)d["project_id"] != "P7dR8mSH" && (string)d["project_id"] != "qvIfYCYJ" && d["project_id"].ToString().Length > 0).Select(d => d["project_id"].ToString()).ToList(); // 种类为依赖
                                                                                                                                                                                                                                                                                                       // 排除 Fabric API 和 Quilt API
                                                                                                                                                                                                                                                                                                       // 有时候真的会空……
                        }
                        // GameVersions
                        var RawVersions = Data["game_versions"].Select(t => t.ToString().Trim().ToLower()).ToList();
                        GameVersions = RawVersions.Where(v => v.StartsWithF("1.") || v.StartsWithF("b1.")).Select(v => v.Contains("-") ? v.BeforeFirst("-") + " 预览版" : v.StartsWithF("b1.") ? "远古版本" : v).ToList();
                        if (GameVersions.Count > 1)
                        {
                            GameVersions = GameVersions.Sort(ModMinecraft.VersionSortBoolean).ToList();
                            if (Type == CompType.ModPack)
                                GameVersions = new List<string>() { GameVersions[0] };
                        }
                        else if (GameVersions.Count == 1)
                        {
                        }
                        // 无需处理
                        else if (RawVersions.Any(v => v.RegexCheck("[0-9]{2}w[0-9]{2}[a-z]{1}")))
                        {
                            GameVersions = RawVersions.Where(v => ModBase.RegexCheck(v, "[0-9]{2}w[0-9]{2}[a-z]{1}")).ToList();
                        }
                        else
                        {
                            GameVersions = new List<string>() { "未知版本" };
                        }
                        // ModLoaders
                        var RawLoaders = Data["loaders"].Select(v => v.ToString()).ToList();
                        ModLoaders = new List<CompLoaderType>();
                        if (RawLoaders.Contains("forge"))
                            ModLoaders.Add(CompLoaderType.Forge);
                        if (RawLoaders.Contains("neoforge"))
                            ModLoaders.Add(CompLoaderType.NeoForge);
                        if (RawLoaders.Contains("fabric"))
                            ModLoaders.Add(CompLoaderType.Fabric);
                        if (RawLoaders.Contains("quilt"))
                            ModLoaders.Add(CompLoaderType.Quilt);
                        #endregion
                    }
                }
            }

            /// <summary>
        /// 重新整理 CurseForge 的下载地址。
        /// </summary>
            public static List<string> HandleCurseForgeDownloadUrls(string Url)
            {
                return new[] { Url.Replace("-service.overwolf.wtf", ".forgecdn.net").Replace("://edge", "://media"), Url.Replace("-service.overwolf.wtf", ".forgecdn.net"), Url.Replace("://edge", "://media"), Url }.Distinct().ToList();
            }

            /// <summary>
        /// 将当前实例转为可用于保存缓存的 Json。
        /// </summary>
            public JObject ToJson()
            {
                var Json = new JObject();
                Json.Add("FromCurseForge", FromCurseForge);
                Json.Add("Id", Id);
                Json.Add("DisplayName", DisplayName);
                Json.Add("ReleaseDate", ReleaseDate);
                Json.Add("DownloadCount", DownloadCount);
                Json.Add("ModLoaders", new JArray(ModLoaders.Select(m => (int)m)));
                Json.Add("GameVersions", new JArray(GameVersions));
                Json.Add("Status", (int)Status);
                if (FileName is not null)
                    Json.Add("FileName", FileName);
                if (DownloadUrls is not null)
                    Json.Add("DownloadUrls", new JArray(DownloadUrls));
                if (Hash is not null)
                    Json.Add("Hash", Hash);
                Json.Add("RawDependencies", new JArray(RawDependencies));
                Json.Add("Dependencies", new JArray(Dependencies));
                return Json;
            }
            /// <summary>
        /// 将当前文件信息实例化为控件。
        /// </summary>
            public MyListItem ToListItem(MyListItem.ClickEventHandler OnClick, MyIconButton.ClickEventHandler OnSaveClick = null, bool BadDisplayName = false)
            {

                // 获取描述信息
                string Title = BadDisplayName ? FileName : DisplayName;
                var Info = new List<string>();
                if ((Title ?? "") != (FileName.BeforeLast(".") ?? ""))
                    Info.Add(FileName.BeforeLast("."));
                switch (Type)
                {
                    case CompType.Mod:
                        {
                            if (Dependencies.Any())
                                Info.Add(Dependencies.Count + " 个前置 Mod");
                            break;
                        }
                    case CompType.ModPack:
                        {
                            if (GameVersions.All(v => v.Contains("w")))
                                Info.Add($"游戏版本 {GameVersions.Join("、")}");
                            break;
                        }
                }
                if (DownloadCount > 0) // CurseForge 的下载次数经常错误地返回 0
                {
                    Info.Add("下载 " + (DownloadCount > 100000 ? Math.Round(DownloadCount / 10000d) + " 万次" : DownloadCount + " 次"));
                }
                Info.Add("更新于 " + ModBase.GetTimeSpanString(ReleaseDate - DateTime.Now, false));
                if (Status != CompFileStatus.Release)
                    Info.Add(StatusDescription);

                // 建立控件
                var NewItem = new MyListItem()
                {
                    Title = Title,
                    SnapsToDevicePixels = true,
                    Height = 42d,
                    Type = MyListItem.CheckType.Clickable,
                    Tag = this,
                    Info = Info.Join("，")
                };
                switch (Status)
                {
                    case CompFileStatus.Release:
                        {
                            NewItem.Logo = ModBase.PathImage + "Icons/R.png";
                            break;
                        }
                    case CompFileStatus.Beta:
                        {
                            NewItem.Logo = ModBase.PathImage + "Icons/B.png"; // Alpha
                            break;
                        }

                    default:
                        {
                            NewItem.Logo = ModBase.PathImage + "Icons/A.png";
                            break;
                        }
                }
                NewItem.Click += OnClick;

                // 建立另存为按钮
                if (OnSaveClick is not null)
                {
                    var BtnSave = new MyIconButton() { Logo = ModBase.Logo.IconButtonSave, ToolTip = "另存为" };
                    ToolTipService.SetPlacement(BtnSave, System.Windows.Controls.Primitives.PlacementMode.Center);
                    ToolTipService.SetVerticalOffset(BtnSave, 30d);
                    ToolTipService.SetHorizontalOffset(BtnSave, 2d);
                    BtnSave.Click += OnSaveClick;
                    NewItem.Buttons = new[] { BtnSave };
                }

                // 结束
                return NewItem;
            }

            // 辅助函数

            public override string ToString()
            {
                return $"{Id}: {FileName}";
            }

        }

        // 获取

        /// <summary>
    /// 已知文件信息的缓存。
    /// </summary>
        public static Dictionary<string, List<CompFile>> CompFilesCache = new Dictionary<string, List<CompFile>>();
        /// <summary>
    /// 获取某个工程下的全部文件列表。
    /// 必须在工作线程执行，失败会抛出异常。
    /// </summary>
        public static List<CompFile> CompFilesGet(string ProjectId, bool FromCurseForge)
        {
            // 获取工程对象
            CompProject TargetProject;
            if (CompProjectCache.ContainsKey(ProjectId)) // 存在缓存
            {
                TargetProject = CompProjectCache[ProjectId];
            }
            else if (FromCurseForge) // CurseForge
            {
                TargetProject = new CompProject((JObject)ModDownload.DlModRequest("https://api.curseforge.com/v1/mods/" + ProjectId, IsJson: true)("data"));
            }
            else // Modrinth
            {
                TargetProject = new CompProject((JObject)ModDownload.DlModRequest("https://api.modrinth.com/v2/project/" + ProjectId, IsJson: true));
            }
            // 获取工程对象的文件列表
            if (!CompFilesCache.ContainsKey(ProjectId)) // 有缓存也不能直接返回，这时候前置 Mod 可能没获取（#5173）
            {
                ModBase.Log("[Comp] 开始获取文件列表：" + ProjectId);
                JArray ResultJsonArray;
                if (FromCurseForge)
                {
                    // CurseForge
                    // HMCL 一次性请求了 10000 个文件，虽然不知道会不会出问题但先这样吧……（#5522）
                    ResultJsonArray = (JArray)ModDownload.DlModRequest($"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=10000", IsJson: true)("data");
                }
                // 之前只请求一部分文件的方法备份如下：
                // If TargetProject.Type = CompType.Mod Then 'Mod 使用每个版本最新的文件
                // ResultJsonArray = GetJson(DlModRequest("https://api.curseforge.com/v1/mods/files", "POST", "{""fileIds"": [" & Join(TargetProject.CurseForgeFileIds, ",") & "]}", "application/json"))("data")
                // Else '否则使用全部文件
                // ResultJsonArray = DlModRequest($"https://api.curseforge.com/v1/mods/{ProjectId}/files?pageSize=999", IsJson:=True)("data")
                // End If
                else
                {
                    // Modrinth
                    ResultJsonArray = (JArray)ModDownload.DlModRequest($"https://api.modrinth.com/v2/project/{ProjectId}/version", IsJson: true);
                }
                CompFilesCache[ProjectId] = ResultJsonArray.Select(a => new CompFile((JObject)a, TargetProject.Type)).Where(a => a.Available).ToList().Distinct((a, b) => (a.Id ?? "") == (b.Id ?? "")); // CurseForge 可能会重复返回相同项（#1330）
            }
            // 获取前置 Mod 列表
            if (TargetProject.Type != CompType.Mod)
                return CompFilesCache[ProjectId];
            var Deps = CompFilesCache[ProjectId].SelectMany(f => f.RawDependencies).Distinct().ToList();
            var UndoneDeps = Deps.Where(f => !CompProjectCache.ContainsKey(f)).ToList();
            // 获取前置 Mod 工程信息
            if (UndoneDeps.Any())
            {
                ModBase.Log($"[Comp] {ProjectId} 文件列表中还需要获取信息的前置 Mod：{UndoneDeps.Join("，")}");
                JArray Projects;
                if (TargetProject.FromCurseForge)
                {
                    Projects = (JArray)ModBase.GetJson(ModDownload.DlModRequest("https://api.curseforge.com/v1/mods", "POST", "{\"modIds\": [" + UndoneDeps.Join(",") + "]}", "application/json"))("data");
                }
                else
                {
                    Projects = (JArray)ModDownload.DlModRequest($"https://api.modrinth.com/v2/projects?ids=[\"{UndoneDeps.Join("\",\"")}\"]", IsJson: true);
                }
                foreach (var Project in Projects)
                    var NewProject = new CompProject((JObject)Project); // 在 New 的时候就添加了缓存
            }
            // 更新前置 Mod 信息
            if (Deps.Any())
            {
                foreach (var DepProject in Deps.Where(id => CompProjectCache.ContainsKey(id)).Select(id => CompProjectCache[id]))
                {
                    foreach (var File in CompFilesCache[ProjectId])
                    {
                        if (File.RawDependencies.Contains(DepProject.Id) && (DepProject.Id ?? "") != (ProjectId ?? ""))
                        {
                            if (!File.Dependencies.Contains(DepProject.Id))
                                File.Dependencies.Add(DepProject.Id);
                        }
                    }
                }
            }
            return CompFilesCache[ProjectId];
        }

        /// <summary>
    /// 预载包含大量 CompFile 的卡片，添加必要的元素和前置 Mod 列表。
    /// </summary>
        public static void CompFilesCardPreload(StackPanel Stack, List<CompFile> Files)
        {
            // 获取卡片对应的前置 ID
            // 如果为整合包就不会有 Dependencies 信息，所以不用管
            var Deps = Files.SelectMany(f => f.Dependencies).Distinct().ToList();
            Deps.Sort();
            if (!Deps.Any())
                return;
            Deps = Deps.Where(dep =>
        {
            if (!CompProjectCache.ContainsKey(dep))
                ModBase.Log($"[Comp] 未找到 ID {dep} 的前置 Mod 信息", ModBase.LogLevel.Debug);
            return CompProjectCache.ContainsKey(dep);
        }).ToList();
            // 添加开头间隔
            Stack.Children.Add(new TextBlock() { Text = "前置 Mod", FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6d, 2d, 0d, 5d) });
            // 添加前置 Mod 列表
            foreach (var Dep in Deps)
            {
                var Item = CompProjectCache[Dep].ToCompItem(false, false);
                Stack.Children.Add(Item);
            }
            // 添加结尾间隔
            Stack.Children.Add(new TextBlock() { Text = "可选版本", FontSize = 14d, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6d, 12d, 0d, 5d) });
        }

        #endregion

        #region CompFavorites | 收藏
        public class CompFavorites
        {

            public static string GetShareCode(List<string> Data)
            {
                try
                {
                    return new JArray(Data).ToString(Newtonsoft.Json.Formatting.None);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[CompFavorites] 生成分享出错");
                }
                return "";
            }

            public static List<string> GetIdsByShareCode(string Code)
            {
                try
                {
                    return JArray.Parse(Code).ToObject<List<string>>();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[CompFavorites] 通过分享获取 ID 出错");
                }
                return new List<string>();
            }

            /// <summary>
        /// 显示收藏菜单。
        /// </summary>
        /// <param name="Project"></param>
        /// <param name="Pos"></param>
            public static void ShowMenu(CompProject Project, UIElement Pos)
            {
                var Body = new ContextMenu();
                foreach (var i in FavoritesList)
                {
                    var Item = new MyMenuItem();
                    Item.MaxWidth = 240d;
                    bool HasFavs = i.Favs.Contains(Project.Id);
                    if (HasFavs)
                    {
                        Item.Header = $"取消收藏 {i.Name}";
                        Item.Icon = ModBase.Logo.IconButtonLikeFill;
                    }
                    else
                    {
                        Item.Header = $"收藏到 {i.Name}";
                        Item.Icon = ModBase.Logo.IconButtonLikeLine;
                    }
                    Item.Click += () => { try { if (HasFavs) { i.Favs.Remove(Project.Id); ModMain.Hint($"已将 {Project.TranslatedName} 从 {i.Name} 中删除", ModMain.HintType.Finish); } else { i.Favs.Add(Project.Id); i.Favs = (List<string>)i.Favs.Distinct(); ModMain.Hint($"已将 {Project.TranslatedName} 添加到 {i.Name} 中", ModMain.HintType.Finish); } Save(); } catch (Exception ex) { ModBase.Log(ex, "[CompFavorites] 改变收藏项出错"); } };
                    Body.Items.Add(Item);
                }
                Body.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                Body.PlacementTarget = Pos;
                Body.IsOpen = true;
            }
            /// <summary>
        /// 显示收藏菜单。
        /// </summary>
            public static void ShowMenu(List<CompProject> Project, UIElement Pos)
            {
                var Body = new ContextMenu();
                foreach (var i in FavoritesList)
                {
                    var Item = new MyMenuItem()
                    {
                        MaxWidth = 240d,
                        Header = $"收藏到 {i.Name}"
                    };
                    Item.Click += () => { try { int Count = i.Favs.Count; i.Favs.AddRange(Project.Select(p => p.Id).AsEnumerable()); i.Favs = i.Favs.Distinct().ToList(); Save(); int SuccessCount = i.Favs.Count - Count; int FailedCount = Project.Count - SuccessCount; ModMain.Hint($"已将 {SuccessCount} 个资源添加到 {i.Name} 中{(FailedCount > 0 ? $"，{FailedCount} 个资源已添加" : "")}！", ModMain.HintType.Finish); } catch (Exception ex) { ModBase.Log(ex, "[CompFavorites] 改变收藏项出错"); } };
                    Body.Items.Add(Item);
                }
                Body.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                Body.PlacementTarget = Pos;
                Body.IsOpen = true;
            }

            public class FavData
            {
                /// <summary>
            /// 收藏夹名称
            /// </summary>
            /// <returns></returns>
                public string Name { get; set; }
                /// <summary>
            /// Guid
            /// </summary>
            /// <returns></returns>
                public string Id { get; set; }
                /// <summary>
            /// 收藏的工程 ID 列表
            /// </summary>
            /// <returns></returns>
                public List<string> Favs { get; set; } = new List<string>();
                /// <summary>
            /// 备注
            /// </summary>
            /// <returns></returns>
                public Dictionary<string, string> Notes { get; set; } = new Dictionary<string, string>();
            }

            private static List<FavData> _FavoritesList;
            /// <summary>
        /// 收藏的工程列表
        /// </summary>
            public static List<FavData> FavoritesList
            {
                get
                {
                    if (_FavoritesList is null)
                    {
                        string RawData = Conversions.ToString(ModBase.Setup.Get("CompFavorites"));
                        List<FavData> RawList = null;
                        List<string> Migrate = null;
                        try
                        {
                            Migrate = JArray.Parse(RawData).ToObject<List<string>>(); // 从旧版本迁移
                        }
                        catch (Exception ex)
                        {
                        }
                        if (Migrate is not null)
                        {
                            RawList = new List<FavData>();
                            RawList.Add(GetNewFav("默认", Migrate));
                        }
                        else
                        {
                            RawList = JArray.Parse(RawData).ToObject<List<FavData>>();
                            if (RawList.Count == 0)
                            {
                                RawList.Add(GetNewFav("默认", null)); // 确保无论如何都要至少有一个
                            }
                        }
                        _FavoritesList = RawList;
                        Save();
                    }
                    return _FavoritesList;
                }
                set
                {
                    _FavoritesList = value;
                    foreach (var item in _FavoritesList)
                        item.Notes = item.Notes.Where(n => !string.IsNullOrWhiteSpace(n.Value)).ToDictionary(n => n.Key, n => n.Value);
                    var RawList = JArray.FromObject(_FavoritesList);
                    ModBase.Setup.Set("CompFavorites", RawList.ToString(Newtonsoft.Json.Formatting.None));
                }
            }

            /// <summary>
        /// 保存收藏夹数据
        /// </summary>
            public static void Save()
            {
                FavoritesList = _FavoritesList;
            }

            /// <summary>
        /// 获取一个新的收藏夹
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="FavList">没有传 Nothing</param>
        /// <returns></returns>
            public static FavData GetNewFav(string Name, List<string> FavList)
            {
                var res = new FavData() { Name = Name, Id = Guid.NewGuid().ToString() };
                if (FavList is null)
                {
                    res.Favs = new List<string>();
                }
                else
                {
                    res.Favs = FavList;
                }
                return res;
            }
        }

        #endregion
        #region CompProject | 项目信息

        public class CompRequest
        {
            /// <summary>
        /// 通过项目 Id 判断是否来自 CurseForge
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
            public static bool IsFromCurseForge(string Id)
            {
                int res = 0;
                return int.TryParse(Id, out res); // CurseForge 数字 ID Modrinth 乱序 ID
            }

            /// <summary>
        /// 通过一堆 ID 从 Modrinth 那获取项目信息 
        /// </summary>
        /// <param name="Ids"></param>
        /// <returns></returns>
            public static List<CompProject> GetListByIdsFromModrinth(List<string> Ids)
            {
                var Res = new List<CompProject>();
                var RawProjectsData = ModDownload.DlModRequest($"https://api.modrinth.com/v2/projects?ids=[\"{Ids.Join("\",\"")}\"]", IsJson: true);
                foreach (var RawData in (IEnumerable)RawProjectsData)
                    Res.Add(new CompProject((JObject)RawData));
                return Res;
            }

            /// <summary>
        /// 通过一堆 ID 从 CurseForge 那获取项目信息 
        /// </summary>
        /// <param name="Ids"></param>
        /// <returns></returns>
            public static List<CompProject> GetListByIdsFromCurseforge(List<string> Ids)
            {
                var Res = new List<CompProject>();
                var RawProjectsData = ModBase.GetJson(ModDownload.DlModRequest("https://api.curseforge.com/v1/mods", "POST", "{\"modIds\": [" + Ids.Join(",") + "]}", "application/json"))("data");
                foreach (var RawData in (IEnumerable)RawProjectsData)
                    Res.Add(new CompProject((JObject)RawData));
                return Res;
            }

            public static List<CompProject> GetCompProjectsByIds(List<string> Input)
            {
                if (!Input.Any())
                    return new List<CompProject>();
                var RawList = Input;
                var ModrinthProjectIds = new List<string>();
                var CurseForgeProjectIds = new List<string>();
                var Res = new List<CompProject>();
                foreach (var Id in RawList)
                {
                    if (IsFromCurseForge(Id))
                    {
                        CurseForgeProjectIds.Add(Id);
                    }
                    else
                    {
                        ModrinthProjectIds.Add(Id);
                    }
                }
                // 在线信息获取
                int FinishedTask = 0;
                int NeedCompleteTask = 0;
                if (CurseForgeProjectIds.Any())
                {
                    NeedCompleteTask += 1;
                    ModBase.RunInNewThread(() => { try { Res.AddRange(GetListByIdsFromCurseforge(CurseForgeProjectIds)); } catch (Exception ex) { ModBase.Log(ex, "[Favorites] 获取 CurseForge 数据失败", ModBase.LogLevel.Hint); } finally { FinishedTask += 1; } }, "Favorites CurseForge");
                }
                if (ModrinthProjectIds.Any())
                {
                    NeedCompleteTask += 1;
                    ModBase.RunInNewThread(() => { try { Res.AddRange(GetListByIdsFromModrinth(ModrinthProjectIds)); } catch (Exception ex) { ModBase.Log(ex, "[Favorites] 获取 Modrinth 数据失败", ModBase.LogLevel.Hint); } finally { FinishedTask += 1; } }, "Favorites Modrinth");
                }
                while (FinishedTask != NeedCompleteTask)
                    Thread.Sleep(50);
                return Res;
            }
        }
        #endregion

        #region CompClipboard | 剪贴板识别
        public class CompClipboard
        {
            // 剪贴板已读取内容
            public static string CurrentText = null;
            // 识别剪贴板内容
            public static void ClipboardListening()
            {
                while (ModBase.Setup.Get("ToolDownloadClipboard"))
                {
                    Thread.Sleep(700);
                    string Text = null;
                    string Slug = null;
                    string ProjectId = null;
                    string CategoryURL = null;
                    object ReturnData = null;
                    ModBase.RunInUiWait(() => Text = My.MyWpfExtension.Computer.Clipboard.GetText());
                    if ((Text ?? "") == (CurrentText ?? ""))
                        continue;
                    CurrentText = Text;
                    Text = Text.Replace("https://", "").Replace("http://", "");

                    if (Text.Contains("curseforge.com/minecraft/")) // e.g. www.curseforge.com/minecraft/mc-mods/jei
                    {
                        var ClassIds = new List<string>() { "6", "4471", "12", "6552" };
                        try
                        {
                            CategoryURL = Text.Split("/")[2];
                            Slug = Text.Split("/")[3];
                            ReturnData = ModDownload.DlModRequest("https://api.curseforge.com/v1/mods/search?gameId=432&slug=" + Slug, IsJson: true); // 获取资源信息
                            string ReceivedClassId = Conversions.ToString(ReturnData("data")(0)("categories")(0)("classId")); // 获取资源的 ClassId

                            // 判断资源的分类是否匹配，不在支持的资源类型中的就直接显示
                            bool IsCategoryMatched = true;
                            string ResClassId = null;
                            if (CategoryURL == "mc-mods" && !(ReceivedClassId == "6"))
                            {
                                IsCategoryMatched = false;
                                ResClassId = "6";
                            }
                            else if (CategoryURL == "modpacks" && !(ReceivedClassId == "4471"))
                            {
                                IsCategoryMatched = false;
                                ResClassId = "4471";
                            }
                            else if (CategoryURL == "texture-packs" && !(ReceivedClassId == "12"))
                            {
                                IsCategoryMatched = false;
                                ResClassId = "12";
                            }
                            else if (CategoryURL == "shaders" && !(ReceivedClassId == "6552"))
                            {
                                IsCategoryMatched = false;
                                ResClassId = "6552";
                            }

                            if (!IsCategoryMatched)
                            {
                                ReturnData = ModDownload.DlModRequest("https://api.curseforge.com/v1/mods/search?gameId=432&slug=" + Slug + "&classId=" + ResClassId, IsJson: true);
                            }

                            ProjectId = Conversions.ToString(ReturnData("data")(0)("id"));
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log("[Clipboard] 获取剪贴板 CurseForge 资源链接 ID 失败: " + ex.ToString(), ModBase.LogLevel.Normal);
                            continue;
                        }
                    }
                    else if (Text.Contains("modrinth.com/")) // e.g. modrinth.com/mod/fabric-api
                    {
                        try
                        {
                            Slug = Text.Split("/")[2];
                            ProjectId = Conversions.ToString(ModDownload.DlModRequest("https://api.modrinth.com/v2/project/" + Slug, IsJson: true)("id"));
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log("[Clipboard] 获取剪贴板 Modrinth 资源链接 ID 失败: " + ex.ToString(), ModBase.LogLevel.Normal);
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    ModBase.Log("[Clipboard] 剪贴板资源 ProjectId: " + ProjectId);

                    if (ModMain.MyMsgBox("PCL 在剪贴板中识别到了资源链接，是否要跳转到该资源的详细信息页面？", "识别到剪贴板资源", "确定", "取消", ForceWait: true) == 1)
                    {
                        ModMain.Hint("正在获取资源信息，请稍等...");
                        var Ids = new List<string>(new[] { ProjectId });
                        var CompProjects = CompRequest.GetCompProjectsByIds(Ids);
                        ModBase.RunInUi(() => ModMain.FrmMain.PageChange(new FormMain.PageStackData()
                        {
                            Page = FormMain.PageType.CompDetail,
                            Additional = new[] { CompProjects.First(), new List<string>(), string.Empty, CompLoaderType.Any }
                        }));
                    }
                }
            }
        }
        #endregion
    }
}