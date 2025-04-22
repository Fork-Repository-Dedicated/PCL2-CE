using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public static class ModModpack
    {

        // 触发整合包安装的外部接口
        /// <summary>
    /// 弹窗要求选择一个整合包文件并进行安装。
    /// </summary>
        public static void ModpackInstall()
        {
            string File = ModBase.SelectFile("整合包文件(*.rar;*.zip;*.mrpack)|*.rar;*.zip;*.mrpack", "选择整合包压缩文件"); // 选择整合包文件
            if (string.IsNullOrEmpty(File))
                return;
            ModBase.RunInThread(() => { try { ModpackInstall(File); } catch (ModBase.CancelledException ex) { } catch (Exception ex) { ModBase.Log(ex, "手动安装整合包失败", ModBase.LogLevel.Msgbox); } });
        }
        /// <summary>
    /// 构建并启动安装给定的整合包文件的加载器，并返回该加载器。若失败则抛出异常。
    /// 必须在工作线程执行。
    /// </summary>
    /// <exception cref="CancelledException" />
        public static ModLoader.LoaderCombo<string> ModpackInstall(string File, string VersionName = null, string Logo = null)
        {
            ModBase.Log("[ModPack] 整合包安装请求：" + (File ?? "null"));
            ZipArchive Archive = null;
            string ArchiveBaseFolder = "";
            try
            {
                // 字符校验
                string TargetFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\";
                if (TargetFolder.Contains("!") || TargetFolder.Contains(";"))
                {
                    ModMain.Hint("游戏路径中不能含有感叹号或分号：" + TargetFolder, ModMain.HintType.Critical);
                    throw new ModBase.CancelledException();
                }
                // 获取整合包种类与关键 Json
                int PackType = -1;
                do
                {
                    try
                    {
                        Archive = new ZipArchive(new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.Read));
                        // 从根目录判断整合包类型
                        if (Archive.GetEntry("mcbbs.packmeta") is not null)
                        {
                            PackType = 3;
                            break;
                        } // MCBBS 整合包（优先于 manifest.json 判断）
                        if (Archive.GetEntry("mmc-pack.json") is not null)
                        {
                            PackType = 2;
                            break;
                        } // MMC 整合包（优先于 manifest.json 判断，#4194）
                        if (Archive.GetEntry("modrinth.index.json") is not null)
                        {
                            PackType = 4;
                            break;
                        } // Modrinth 整合包
                        if (Archive.GetEntry("manifest.json") is not null)
                        {
                            JObject Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Archive.GetEntry("manifest.json").Open(), Encoding.UTF8));
                            if (Json["addons"] is null)
                            {
                                PackType = 0;
                                break; // CurseForge 整合包
                            }
                            else
                            {
                                PackType = 3;
                                break;
                            } // MCBBS 整合包
                        }
                        if (Archive.GetEntry("modpack.json") is not null)
                        {
                            PackType = 1;
                            break;
                        } // HMCL 整合包
                        if (Archive.GetEntry("modpack.zip") is not null || Archive.GetEntry("modpack.mrpack") is not null)
                        {
                            PackType = 9;
                            break;
                        } // 带启动器的压缩包
                          // 从一级目录判断整合包类型
                        bool exitTry = false;
                        foreach (var Entry in Archive.Entries)
                        {
                            string[] FullNames = Entry.FullName.Split("/");
                            ArchiveBaseFolder = FullNames[0] + "/";
                            // 确定为一级目录下
                            if (FullNames.Count() != 2)
                                continue;
                            // 判断是否为关键文件
                            if (FullNames[1] == "mcbbs.packmeta")
                            {
                                PackType = 3;
                                exitTry = true;
                                break;
                            } // MCBBS 整合包（优先于 manifest.json 判断）
                            if (FullNames[1] == "mmc-pack.json")
                            {
                                PackType = 2;
                                exitTry = true;
                                break;
                            } // MMC 整合包（优先于 manifest.json 判断，#4194）
                            if (FullNames[1] == "modrinth.index.json")
                            {
                                PackType = 4;
                                exitTry = true;
                                break;
                            } // Modrinth 整合包
                            if (FullNames[1] == "manifest.json")
                            {
                                JObject Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Entry.Open(), Encoding.UTF8));
                                if (Json["addons"] is null)
                                {
                                    PackType = 0;
                                    exitTry = true;
                                    break; // CurseForge 整合包
                                }
                                else
                                {
                                    PackType = 3;
                                    ArchiveBaseFolder = "overrides/";
                                    exitTry = true;
                                    break;
                                } // MCBBS 整合包
                            }
                            if (FullNames[1] == "modpack.json")
                            {
                                PackType = 1;
                                exitTry = true;
                                break;
                            } // HMCL 整合包
                            if (FullNames[1] == "modpack.zip" || FullNames[1] == "modpack.mrpack")
                            {
                                PackType = 9;
                                exitTry = true;
                                break;
                            } // 带启动器的压缩包
                        }

                        if (exitTry)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ModBase.GetExceptionDetail(ex, true).Contains("Error.WinIOError"))
                        {
                            throw new Exception("打开整合包文件失败", ex);
                        }
                        else if (File.EndsWithF(".rar", true))
                        {
                            throw new Exception("PCL 无法处理 rar 格式的压缩包，请在解压后重新压缩为 zip 格式再试", ex);
                        }
                        else
                        {
                            throw new Exception("打开整合包文件失败，文件可能损坏或为不支持的压缩包格式", ex);
                        }
                    }
                }
                while (false);
                // 执行对应的安装方法
                switch (PackType)
                {
                    case 0:
                        {
                            ModBase.Log("[ModPack] 整合包种类：CurseForge");
                            return InstallPackCurseForge(File, Archive, ArchiveBaseFolder, VersionName, Logo);
                        }
                    case 1:
                        {
                            ModBase.Log("[ModPack] 整合包种类：HMCL");
                            return InstallPackHMCL(File, Archive, ArchiveBaseFolder);
                        }
                    case 2:
                        {
                            ModBase.Log("[ModPack] 整合包种类：MMC");
                            return InstallPackMMC(File, Archive, ArchiveBaseFolder);
                        }
                    case 3:
                        {
                            ModBase.Log("[ModPack] 整合包种类：MCBBS");
                            return InstallPackMCBBS(File, Archive, ArchiveBaseFolder, VersionName);
                        }
                    case 4:
                        {
                            ModBase.Log("[ModPack] 整合包种类：Modrinth");
                            return InstallPackModrinth(File, Archive, ArchiveBaseFolder, VersionName, Logo);
                        }
                    case 9:
                        {
                            ModBase.Log("[ModPack] 整合包种类：带启动器的压缩包");
                            return InstallPackLauncherPack(File, Archive, ArchiveBaseFolder);
                        }

                    default:
                        {
                            ModBase.Log("[ModPack] 整合包种类：未能识别，假定为压缩包");
                            return InstallPackCompress(File, Archive);
                        }
                }
            }
            finally
            {
                if (Archive is not null)
                    Archive.Dispose();
            }
        }

        private static void ExtractModpackFiles(string InstallTemp, string FileAddress, ModLoader.LoaderBase Loader, double LoaderProgressDelta)
        {
            // 解压文件
            int RetryCount = 1;
            var Encode = Encoding.GetEncoding("GB18030");
            try
            {
            Retry:
                ;

                // 完全不知道为啥会出现文件正在被另一进程使用的问题，总之多试试
                ModBase.DeleteDirectory(InstallTemp);
                ModBase.ExtractFile(FileAddress, InstallTemp, Encode, ProgressIncrementHandler: Delta => Loader.Progress += Delta * LoaderProgressDelta);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "第 " + RetryCount + " 次解压尝试失败");
                if (ex is ArgumentException)
                {
                    Encode = Encoding.UTF8;
                    ModBase.Log("[ModPack] 已切换压缩包解压编码为 UTF8");
                }
                if (RetryCount < 5)
                {
                    Thread.Sleep(RetryCount * 2000);
                    if (Loader is not null && Loader.LoadingState != MyLoading.MyLoadingState.Run)
                        return;
                    RetryCount += 1;
                    goto Retry;
                }
                else
                {
                    throw new Exception("解压整合包文件失败", ex);
                }
            }
        }

        #region 不同类型整合包的安装方法

        // CurseForge
        private static ModLoader.LoaderCombo<string> InstallPackCurseForge(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder, string VersionName = null, string Logo = null)
        {

            // 读取 Json 文件
            JObject Json;
            try
            {
                Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "manifest.json").Open()));
            }
            catch (Exception ex)
            {
                throw new Exception("CurseForge 整合包安装信息存在问题", ex);
            }
            if (Json["minecraft"] is null || Json["minecraft"]["version"] is null)
                throw new Exception("CurseForge 整合包未提供 Minecraft 版本信息");

            // 获取版本名
            if (VersionName is null)
            {
                VersionName = (string)(Json["name"] ?? "");
                var Validate = new ValidateFolderName(ModMinecraft.PathMcFolder + "versions");
                if (!string.IsNullOrEmpty(Validate.Validate(VersionName)))
                    VersionName = "";
                if (string.IsNullOrEmpty(VersionName))
                    VersionName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new System.Collections.ObjectModel.Collection<ValidateType>() { Validate });
                if (string.IsNullOrEmpty(VersionName))
                    throw new ModBase.CancelledException();
            }

            // 获取 Mod API 版本信息
            string ForgeVersion = null;
            string NeoForgeVersion = null;
            string FabricVersion = null;
            string QuiltVersion = null;
            foreach (var Entry in Json["minecraft"]["modLoaders"] ?? Array.Empty<JToken>())
            {
                string Id = (Entry["id"] ?? "").ToString().ToLower();
                if (Id.StartsWithF("forge-"))
                {
                    // Forge 指定
                    if (Id.Contains("recommended"))
                        throw new Exception("该整合包版本过老，已不支持进行安装！");
                    ModBase.Log("[ModPack] 整合包 Forge 版本：" + Id);
                    ForgeVersion = Id.Replace("forge-", "");
                }
                else if (Id.StartsWithF("neoforge-"))
                {
                    // NeoForge 指定
                    ModBase.Log("[ModPack] 整合包 NeoForge 版本：" + Id);
                    NeoForgeVersion = Id.Replace("neoforge-", "");
                }
                else if (Id.StartsWithF("fabric-"))
                {
                    // Fabric 指定
                    try
                    {
                        ModBase.Log("[ModPack] 整合包 Fabric 版本：" + Id);
                        FabricVersion = Id.Replace("fabric-", "");
                        break;
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取整合包 Fabric 版本失败：" + Id);
                    }
                }
                else if (Id.StartsWithF("quilt-"))
                {
                    // Quilt 指定
                    try
                    {
                        ModBase.Log("[ModPack] 整合包 Quilt 版本：" + Id);
                        QuiltVersion = Id.Replace("quilt-", "");
                        break;
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取整合包 Quilt 版本失败：" + Id);
                    }
                }
            }
            // 解压与配置文件
            string InstallTemp = ModMain.RequestTaskTempFolder();
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            string OverrideHome = (string)(Json["overrides"] ?? "");
            if (!string.IsNullOrEmpty(OverrideHome))
            {
                InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6d);
            Task.Progress = 0.6d;
            // 复制结果
            ModBase.Log("[ModPack] 整合包 override 目标：" + OverrideHome);
            string OverrideRoot = InstallTemp + ArchiveBaseFolder + (OverrideHome == "." || OverrideHome == "./" ? "" : OverrideHome); // #5613
            if (Directory.Exists(OverrideRoot))
            {
                string CopyTarget = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}";
                ModBase.CopyDirectory(OverrideRoot, CopyTarget, Delta => Task.Progress += Delta * 0.35d);
                ModBase.Log($"[ModPack] 整合包 override 复制：{OverrideRoot} -> {CopyTarget}");
            }
            else
            {
                ModBase.Log($"[ModPack] 整合包中未找到 overrides 文件夹：{OverrideRoot}");
            }
            Task.Progress = 0.95d;
            // 开启版本隔离
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndie", 1.ToString());
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndieV2", Conversions.ToString(true));
        })
                {
                    ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
                    Block = false
                }); // 每 6M 需要 1s
            }
            // 获取 Mod 列表
            var ModList = new List<int>();
            var ModOptionalList = new List<int>();
            foreach (var ModEntry in Json["files"] ?? Array.Empty<JToken>())
            {
                if (ModEntry["projectID"] is null || ModEntry["fileID"] is null)
                {
                    ModMain.Hint("某项 Mod 缺少必要信息，已跳过：" + ModEntry.ToString());
                    continue;
                }
                ModList.Add((int)ModEntry["fileID"]);
                if (ModEntry["required"] is not null && !ModEntry["required"].ToObject<bool>())
                    ModOptionalList.Add((int)ModEntry["fileID"]);
            }
            if (ModList.Any())
            {
                var ModDownloadLoaders = new List<ModLoader.LoaderBase>();
                // 获取 Mod 下载信息
                ModDownloadLoaders.Add(new ModLoader.LoaderTask<int, JArray>("获取 Mod 下载信息", (Task) =>
        {
            Task.Output = (JArray)ModBase.GetJson(ModDownload.DlModRequest("https://api.curseforge.com/v1/mods/files", "POST", "{\"fileIds\": [" + ModList.Join(",") + "]}", "application/json"))("data");
            // 如果文件已被删除，则 API 会跳过那一项
            if (ModList.Count > Task.Output.Count)
                throw new Exception("整合包中的部分 Mod 版本已被 Mod 作者删除，所以没法继续安装了，请向整合包作者反馈该问题");
        })
                {
                    ProgressWeight = ModList.Count / 10d
                }); // 每 10 Mod 需要 1s
                    // 构造 NetFile
                ModDownloadLoaders.Add(new ModLoader.LoaderTask<JArray, List<ModNet.NetFile>>("构造 Mod 下载信息", (Task) =>
        {
            var FileList = new Dictionary<int, ModNet.NetFile>();
            foreach (var ModJson in Task.Input)
            {
                int Id = ModJson["id"].ToObject<int>();
                // 跳过重复的 Mod（疑似 CurseForge Bug）
                if (FileList.ContainsKey(Id))
                    continue;
                // 可选 Mod 提示
                if (ModOptionalList.Contains(Id))
                {
                    if (ModMain.MyMsgBox("是否要下载整合包中的可选文件 " + ModJson["displayName"].ToString() + "？", "下载可选文件", "是", "否") == 2)
                    {
                        continue;
                    }
                }
                // 建立 CompFile
                var File = new ModComp.CompFile((JObject)ModJson, ModComp.CompType.Mod);
                if (!File.Available)
                    continue;
                // 根据 modules 和文件名后缀判断资源类型
                string TargetFolder;
                if (ModJson["modules"].Any()) // modules 可能返回 null（#1006）
                {
                    var ModuleNames = ((JArray)ModJson["modules"]).Select(l => l["name"].ToString()).ToList();
                    if (ModuleNames.Contains("META-INF") || ModuleNames.Contains("mcmod.info") || File.FileName.EndsWithF(".jar", true))
                    {
                        TargetFolder = "mods";
                    }
                    else if (ModuleNames.Contains("pack.mcmeta"))
                    {
                        TargetFolder = "resourcepacks";
                    }
                    else
                    {
                        TargetFolder = "shaderpacks";
                    }
                }
                else
                {
                    TargetFolder = "mods";
                }
                // 实际的添加
                FileList.Add(Id, File.ToNetFile($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\{TargetFolder}\"));
                Task.Progress += 1d / (1 + ModList.Count);
            }
            Task.Output = FileList.Values.ToList();
        })
                {
                    ProgressWeight = ModList.Count / 200d,
                    Show = false
                }); // 每 200 Mod 需要 1s
                    // 下载 Mod 文件
                ModDownloadLoaders.Add(new ModNet.LoaderDownload("下载 Mod", new List<ModNet.NetFile>()) { ProgressWeight = ModList.Count * 1.5d }); // 每个 Mod 需要 1.5s
                                                                                                                                                   // 构造加载器
                InstallLoaders.Add(new ModLoader.LoaderCombo<int>("下载 Mod（主加载器）", ModDownloadLoaders) { Show = false, ProgressWeight = ModDownloadLoaders.Sum(l => l.ProgressWeight) });
            }

            // 构造加载器
            var Request = new ModDownloadLib.McInstallRequest()
            {
                TargetVersionName = VersionName,
                TargetVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\",
                MinecraftName = Json["minecraft"]["version"].ToString(),
                ForgeVersion = ForgeVersion,
                NeoForgeVersion = NeoForgeVersion,
                FabricVersion = FabricVersion,
                QuiltVersion = QuiltVersion
            };
            var MergeLoaders = ModDownloadLib.McInstallLoader(Request, true);
            // 构造 Libraries 加载器
            var LoadersLib = new List<ModLoader.LoaderBase>();
            LoadersLib.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析游戏支持库文件（副加载器）", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionName))) { ProgressWeight = 1d, Show = false });
            LoadersLib.Add(new ModNet.LoaderDownload("下载游戏支持库文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 7d, Show = false });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载游戏支持库文件", LoadersLib) { ProgressWeight = 8d });
            Loaders.Add(new ModLoader.LoaderTask<string, string>("最终整理文件", (Task) =>
        {
            // 设置图标
            string VersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\";
            if (Logo is not null && File.Exists(Logo))
            {
                File.Copy(Logo, VersionFolder + @"PCL\Logo.png", true);
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "Logo", @"PCL\Logo.png");
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "LogoCustom", "True");
                ModBase.Log("[ModPack] 已设置整合包 Logo：" + Logo);
            }
            // 删除原始整合包文件
            foreach (string Target in new[] { VersionFolder + "原始整合包.zip", VersionFolder + "原始整合包.mrpack" })
            {
                if (File.Exists(Target))
                {
                    ModBase.Log("[ModPack] 删除原始整合包文件：" + Target);
                    File.Delete(Target);
                }
            }
            if (File.Exists(FileAddress) && ModBase.GetFileNameWithoutExtentionFromPath(FileAddress) == "modpack")
            {
                ModBase.Log("[ModPack] 删除安装整合包文件：" + FileAddress);
                File.Delete(FileAddress);
            }
        })
            {
                ProgressWeight = 0.1d,
                Show = false
            });

            // 重复任务检查
            string LoaderName = "CurseForge 整合包安装：" + VersionName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            Loader.Start(Request.TargetVersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModBase.RunInUi(() => ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.DownloadManager));
            return Loader;
        }

        // Modrinth
        private static ModLoader.LoaderCombo<string> InstallPackModrinth(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder, string VersionName = null, string Logo = null)
        {

            // 读取 Json 文件
            JObject Json;
            try
            {
                Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "modrinth.index.json").Open()));
            }
            catch (Exception ex)
            {
                throw new Exception("Modrinth 整合包安装信息存在问题", ex);
            }
            if (Json["dependencies"] is null || Json["dependencies"]["minecraft"] is null)
                throw new Exception("Modrinth 整合包未提供 Minecraft 版本信息");
            // 获取 Mod API 版本信息
            string MinecraftVersion = null;
            string ForgeVersion = null;
            string NeoForgeVersion = null;
            string FabricVersion = null;
            string QuiltVersion = null;
            foreach (JProperty Entry in Json["dependencies"] ?? Array.Empty<JToken>())
            {
                switch (Entry.Name.ToLower() ?? "")
                {
                    case "minecraft":
                        {
                            MinecraftVersion = Entry.Value.ToString();
                            break;
                        }
                    case "forge": // eg. 14.23.5.2859 / 1.19-41.1.0
                        {
                            ForgeVersion = Entry.Value.ToString();
                            ModBase.Log("[ModPack] 整合包 Forge 版本：" + ForgeVersion);
                            break;
                        }
                    case "neoforge":
                    case "neo-forge": // eg. 20.6.98-beta
                        {
                            NeoForgeVersion = Entry.Value.ToString();
                            ModBase.Log("[ModPack] 整合包 NeoForge 版本：" + NeoForgeVersion);
                            break;
                        }
                    case "fabric-loader": // eg. 0.14.14
                        {
                            FabricVersion = Entry.Value.ToString();
                            ModBase.Log("[ModPack] 整合包 Fabric 版本：" + FabricVersion);
                            break;
                        }
                    case "quilt-loader": // eg. 0.26.0
                        {
                            QuiltVersion = Entry.Value.ToString();
                            ModBase.Log("[ModPack] 整合包 Quilt 版本：" + QuiltVersion);
                            break;
                        }

                    default:
                        {
                            ModMain.Hint($"无法安装整合包，其中出现了未知的 Mod 加载器 {Entry.Name}（版本为 {Entry.Value.ToString()}）！", ModMain.HintType.Critical);
                            break;
                        }
                }
            }
            // 获取版本名
            if (VersionName is null)
            {
                VersionName = (string)(Json["name"] ?? "");
                var Validate = new ValidateFolderName(ModMinecraft.PathMcFolder + "versions");
                if (!string.IsNullOrEmpty(Validate.Validate(VersionName)))
                    VersionName = "";
                if (string.IsNullOrEmpty(VersionName))
                    VersionName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new System.Collections.ObjectModel.Collection<ValidateType>() { Validate });
                if (string.IsNullOrEmpty(VersionName))
                    throw new ModBase.CancelledException();
            }
            // 解压和配置文件
            string InstallTemp = ModMain.RequestTaskTempFolder();
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6d);
            Task.Progress = 0.6d;
            // 复制 overrides 文件夹和 client-overrides 文件夹
            if (Directory.Exists(InstallTemp + ArchiveBaseFolder + "overrides"))
            {
                ModBase.CopyDirectory(InstallTemp + ArchiveBaseFolder + "overrides", ModMinecraft.PathMcFolder + @"versions\" + VersionName, Delta => Task.Progress += Delta * 0.25d);
            }
            else
            {
                ModBase.Log("[ModPack] 整合包中未找到 override 目录，已跳过");
            }
            Task.Progress = 0.85d;
            if (Directory.Exists(InstallTemp + ArchiveBaseFolder + "client-overrides"))
            {
                ModBase.CopyDirectory(InstallTemp + ArchiveBaseFolder + "client-overrides", ModMinecraft.PathMcFolder + @"versions\" + VersionName, Delta => Task.Progress += Delta * 0.1d);
            }
            Task.Progress = 0.95d;
            // 开启版本隔离
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndie", 1.ToString());
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndieV2", Conversions.ToString(true));
        })
            {
                ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 获取下载文件列表
            var FileList = new List<ModNet.NetFile>();
            foreach (var File in Json["files"] ?? Array.Empty<JToken>())
            {
                // 检查是否需要该文件
                if (File["env"] is not null)
                {
                    switch (File["env"]["client"].ToString() ?? "")
                    {
                        case "optional":
                            {
                                if (ModMain.MyMsgBox("是否要下载可选文件 " + ModBase.GetFileNameFromPath(File["path"].ToString()) + "？", "下载可选文件", "是", "否") == 2)
                                {
                                    continue;
                                }

                                break;
                            }
                        case "unsupported":
                            {
                                continue;
                            }
                    }
                }
                // 添加下载文件
                var Urls = File["downloads"].Select(t => t.ToString().Replace("://edge.forgecdn", "://media.forgecdn")).ToList();
                Urls.AddRange(Urls.Select(u => ModDownload.DlSourceModGet(u)).ToList());
                Urls = Urls.Distinct().ToList();
                FileList.Add(new ModNet.NetFile(Urls, ModMinecraft.PathMcFolder + @"versions\" + VersionName + @"\" + File["path"].ToString(), new ModBase.FileChecker(ActualSize: File["fileSize"].ToObject<long>(), Hash: File["hashes"]["sha1"].ToString()), true));
            }
            if (FileList.Any())
            {
                InstallLoaders.Add(new ModNet.LoaderDownload("下载额外文件", FileList) { ProgressWeight = FileList.Count * 1.5d }); // 每个 Mod 需要 1.5s
            }

            // 构造加载器
            var Request = new ModDownloadLib.McInstallRequest()
            {
                TargetVersionName = VersionName,
                TargetVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\",
                MinecraftName = MinecraftVersion,
                ForgeVersion = ForgeVersion,
                NeoForgeVersion = NeoForgeVersion,
                FabricVersion = FabricVersion,
                QuiltVersion = QuiltVersion
            };
            var MergeLoaders = ModDownloadLib.McInstallLoader(Request, true);
            // 构造 Libraries 加载器
            var LoadersLib = new List<ModLoader.LoaderBase>();
            LoadersLib.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析游戏支持库文件（副加载器）", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionName))) { ProgressWeight = 1d, Show = false });
            LoadersLib.Add(new ModNet.LoaderDownload("下载游戏支持库文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 7d, Show = false });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载游戏支持库文件", LoadersLib) { ProgressWeight = 8d });
            Loaders.Add(new ModLoader.LoaderTask<string, string>("最终整理文件", (Task) =>
        {
            // 设置图标
            string VersionFolder = ModMinecraft.PathMcFolder + @"versions\" + VersionName + @"\";
            if (Logo is not null && File.Exists(Logo))
            {
                File.Copy(Logo, VersionFolder + @"PCL\Logo.png", true);
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "Logo", @"PCL\Logo.png");
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "LogoCustom", "True");
                ModBase.Log("[ModPack] 已设置整合包 Logo：" + Logo);
            }
            // 删除原始整合包文件
            foreach (string Target in new[] { VersionFolder + "原始整合包.zip", VersionFolder + "原始整合包.mrpack" })
            {
                if (File.Exists(Target))
                {
                    ModBase.Log("[ModPack] 删除原始整合包文件：" + Target);
                    File.Delete(Target);
                }
            }
            if (File.Exists(FileAddress) && ModBase.GetFileNameWithoutExtentionFromPath(FileAddress) == "modpack")
            {
                ModBase.Log("[ModPack] 删除安装整合包文件：" + FileAddress);
                File.Delete(FileAddress);
            }
        })
            {
                ProgressWeight = 0.1d,
                Show = false
            });

            // 重复任务检查
            string LoaderName = $"Modrinth 整合包安装：{VersionName} ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            Loader.Start(Request.TargetVersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModBase.RunInUi(() => ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.DownloadManager));
            return Loader;
        }

        // HMCL
        private static ModLoader.LoaderCombo<string> InstallPackHMCL(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder)
        {
            // 读取 Json 文件
            JObject Json;
            try
            {
                Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "modpack.json").Open(), Encoding.UTF8));
            }
            catch (Exception ex)
            {
                throw new Exception("HMCL 整合包安装信息存在问题", ex);
            }
            // 获取版本名
            string VersionName = (string)(Json["name"] ?? "");
            var Validate = new ValidateFolderName(ModMinecraft.PathMcFolder + "versions");
            if (!string.IsNullOrEmpty(Validate.Validate(VersionName)))
                VersionName = "";
            if (string.IsNullOrEmpty(VersionName))
                VersionName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new System.Collections.ObjectModel.Collection<ValidateType>() { Validate });
            if (string.IsNullOrEmpty(VersionName))
                throw new ModBase.CancelledException();
            // 解压与配置文件
            string InstallTemp = ModMain.RequestTaskTempFolder();
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6d);
            Task.Progress = 0.6d;
            // 复制结果
            if (Directory.Exists(InstallTemp + ArchiveBaseFolder + "minecraft"))
            {
                ModBase.CopyDirectory(InstallTemp + ArchiveBaseFolder + "minecraft", ModMinecraft.PathMcFolder + @"versions\" + VersionName, Delta => Task.Progress += Delta * 0.35d);
            }
            else
            {
                ModBase.Log("[ModPack] 整合包中未找到 minecraft overrides 目录，已跳过");
            }
            Task.Progress = 0.95d;
            // 开启版本隔离
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndie", 1.ToString());
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndieV2", Conversions.ToString(true));
        })
            {
                ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 构造加载器
            if (Json["gameVersion"] is null)
                throw new Exception("该 HMCL 整合包未提供游戏版本信息，无法安装！");
            var Request = new ModDownloadLib.McInstallRequest()
            {
                TargetVersionName = VersionName,
                TargetVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\",
                MinecraftName = Json["gameVersion"].ToString()
            };
            var MergeLoaders = ModDownloadLib.McInstallLoader(Request, true);
            // 构造 Libraries 加载器（为了使得 Mods 下载结束后再构造，这样才会下载 JumpLoader 文件）
            var LoadersLib = new List<ModLoader.LoaderBase>();
            LoadersLib.Add(new ModLoader.LoaderTask<string, string>("重命名版本 Json（副加载器）", () =>
        {
            string RealFileName = ModMinecraft.PathMcFolder + @"versions\" + VersionName + @"\" + VersionName + ".json";
            string OldFileName = ModMinecraft.PathMcFolder + @"versions\" + VersionName + @"\pack.json";
            if (File.Exists(OldFileName))
            {
                // 修改 id
                var FileJson = ModBase.GetJson(ModBase.ReadFile(OldFileName));
                FileJson("id") = VersionName;
                // 替换文件
                File.Delete(OldFileName);
                ModBase.WriteFile(RealFileName, FileJson.ToString());
                ModBase.Log("[ModPack] 已重命名版本 Json：" + RealFileName);
            }
        })
            {
                ProgressWeight = 0.1d,
                Show = false
            });
            LoadersLib.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析游戏支持库文件（副加载器）", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionName))) { ProgressWeight = 1d, Show = false });
            LoadersLib.Add(new ModNet.LoaderDownload("下载游戏支持库文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 7d, Show = false });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>() { new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, Block = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) }, new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) }, new ModLoader.LoaderCombo<string>("下载游戏支持库文件", LoadersLib) { ProgressWeight = 8d } };

            // 重复任务检查
            string LoaderName = "HMCL 整合包安装：" + VersionName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            // If Archive IsNot Nothing Then Archive.Dispose() '解除占用，以免在加载器中触发 “正由另一进程使用，因此该进程无法访问此文件”
            Loader.Start(Request.TargetVersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModBase.RunInUi(() => ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.DownloadManager));
            return Loader;
        }

        // MMC
        private static ModLoader.LoaderCombo<string> InstallPackMMC(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder)
        {
            // 读取 Json 文件
            JObject PackJson;
            string PackInstance;
            try
            {
                PackJson = (JObject)ModBase.GetJson(ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "mmc-pack.json").Open(), Encoding.UTF8));
                PackInstance = ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "instance.cfg").Open(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("MMC 整合包安装信息存在问题", ex);
            }
            // 获取版本名
            string VersionName = PackInstance.RegexSeek(@"(?<=\nname\=)[^\n]+") ?? "";
            var Validate = new ValidateFolderName(ModMinecraft.PathMcFolder + "versions");
            if (!string.IsNullOrEmpty(Validate.Validate(VersionName)))
                VersionName = "";
            if (string.IsNullOrEmpty(VersionName))
                VersionName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new System.Collections.ObjectModel.Collection<ValidateType>() { Validate });
            if (string.IsNullOrEmpty(VersionName))
                throw new ModBase.CancelledException();
            // 解压、配置设置文件
            string InstallTemp = ModMain.RequestTaskTempFolder();
            string SetupFile = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini";
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6d);
            Task.Progress = 0.6d;
            // 复制结果
            if (Directory.Exists(InstallTemp + ArchiveBaseFolder + ".minecraft"))
            {
                ModBase.CopyDirectory(InstallTemp + ArchiveBaseFolder + ".minecraft", ModMinecraft.PathMcFolder + @"versions\" + VersionName, Delta => Task.Progress += Delta * 0.35d);
            }
            else
            {
                ModBase.Log("[ModPack] 整合包中未找到 overrides .minecraft 目录，已跳过");
            }
            Task.Progress = 0.95d;
            // 开启版本隔离
            ModBase.WriteIni(SetupFile, "VersionArgumentIndie", 1.ToString());
            ModBase.WriteIni(SetupFile, "VersionArgumentIndieV2", Conversions.ToString(true));
            // 读取 MMC 设置文件（#2655）
            try
            {
                string MMCSetupFile = InstallTemp + ArchiveBaseFolder + "instance.cfg";
                if (File.Exists(MMCSetupFile))
                {
                    // 将其中的等号替换为冒号，以符合 ini 文件格式
                    var Lines = new List<string>();
                    foreach (var Line in ModBase.ReadFile(MMCSetupFile).Split(new[] { Constants.vbCr, Constants.vbLf }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!Line.Contains("="))
                            continue;
                        Lines.Add(Line.BeforeFirst("=") + ":" + Line.AfterFirst("="));
                    }
                    ModBase.WriteFile(MMCSetupFile, Lines.Join(Constants.vbCrLf));
                    // 读取文件
                    if (Conversions.ToBoolean(ModBase.ReadIni(MMCSetupFile, "OverrideCommands", Conversions.ToString(false))))
                    {
                        string PreLaunchCommand = ModBase.ReadIni(MMCSetupFile, "PreLaunchCommand");
                        if (!string.IsNullOrEmpty(PreLaunchCommand))
                        {
                            PreLaunchCommand = PreLaunchCommand.Replace(@"\""", "\"").Replace("$INST_JAVA", "{java}javaw.exe").Replace(@"$INST_MC_DIR\", "{minecraft}").Replace("$INST_MC_DIR", "{minecraft}").Replace(@"$INST_DIR\", "{verpath}").Replace("$INST_DIR", "{verpath}").Replace("$INST_ID", "{name}").Replace("$INST_NAME", "{name}");
                            ModBase.WriteIni(SetupFile, "VersionAdvanceRun", PreLaunchCommand);
                            ModBase.Log("[ModPack] 迁移 MultiMC 版本独立设置：启动前执行命令：" + PreLaunchCommand);
                        }
                    }
                    if (Conversions.ToBoolean(ModBase.ReadIni(MMCSetupFile, "JoinServerOnLaunch", Conversions.ToString(false))))
                    {
                        string ServerAddress = ModBase.ReadIni(MMCSetupFile, "JoinServerOnLaunchAddress").Replace(@"\""", "\"");
                        ModBase.WriteIni(SetupFile, "VersionServerEnter", ServerAddress);
                        ModBase.Log("[ModPack] 迁移 MultiMC 版本独立设置：自动进入服务器：" + ServerAddress);
                    }
                    if (Conversions.ToBoolean(ModBase.ReadIni(MMCSetupFile, "IgnoreJavaCompatibility", Conversions.ToString(false))))
                    {
                        ModBase.WriteIni(SetupFile, "VersionAdvanceJava", Conversions.ToString(true));
                        ModBase.Log("[ModPack] 迁移 MultiMC 版本独立设置：忽略 Java 兼容性警告");
                    }
                    string Logo = ModBase.ReadIni(MMCSetupFile, "iconKey", "");
                    if (!string.IsNullOrEmpty(Logo) && File.Exists($"{InstallTemp}{ArchiveBaseFolder}{Logo}.png"))
                    {
                        ModBase.WriteIni(SetupFile, "LogoCustom", Conversions.ToString(true));
                        ModBase.WriteIni(SetupFile, "Logo", @"PCL\Logo.png");
                        ModBase.CopyFile($"{InstallTemp}{ArchiveBaseFolder}{Logo}.png", $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Logo.png");
                        ModBase.Log($"[ModPack] 迁移 MultiMC 版本独立设置：版本图标（{Logo}.png）");
                    }
                    // JVM 参数
                    string JvmArgs = ModBase.ReadIni(MMCSetupFile, "JvmArgs", "");
                    if (!string.IsNullOrEmpty(JvmArgs))
                    {
                        if (Conversions.ToBoolean(ModBase.ReadIni(MMCSetupFile, "OverrideJavaArgs", Conversions.ToString(false))))
                        {
                            ModBase.WriteIni(SetupFile, "VersionAdvanceJvm", JvmArgs);
                            ModBase.Log("[ModPack] 迁移 MultiMC 版本独立设置：JVM 参数（覆盖）：" + JvmArgs);
                        }
                        else
                        {
                            JvmArgs = Conversions.ToString(JvmArgs + Operators.ConcatenateObject(" ", ModBase.Setup.Get("LaunchAdvanceJvm")));
                            ModBase.WriteIni(SetupFile, "VersionAdvanceJvm", JvmArgs);
                            ModBase.Log("[ModPack] 迁移 MultiMC 版本独立设置：JVM 参数（追加）：" + JvmArgs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"读取 MMC 配置文件失败（{InstallTemp}{ArchiveBaseFolder}instance.cfg）");
            }
        })
            {
                ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 构造版本安装请求
            if (PackJson["components"] is null)
                throw new Exception("该 MMC 整合包未提供游戏版本信息，无法安装！");
            var Request = new ModDownloadLib.McInstallRequest() { TargetVersionName = VersionName, TargetVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\" };
            foreach (var Component in PackJson["components"])
            {
                switch ((Component["uid"] ?? "").ToString() ?? "")
                {
                    case "org.lwjgl":
                        {
                            ModBase.Log("[ModPack] 已跳过 LWJGL 项");
                            break;
                        }
                    case "net.minecraft":
                        {
                            Request.MinecraftName = (string)Component["version"];
                            break;
                        }
                    case "net.minecraftforge":
                        {
                            Request.ForgeVersion = (string)Component["version"];
                            break;
                        }
                    case "net.neoforged":
                        {
                            Request.NeoForgeVersion = (string)Component["version"];
                            break;
                        }
                    case "net.fabricmc.fabric-loader":
                        {
                            Request.FabricVersion = (string)Component["version"];
                            break;
                        }
                    case "org.quiltmc.quilt-loader":
                        {
                            Request.QuiltVersion = (string)Component["version"];
                            break;
                        }
                }
            }
            // 构造加载器
            var MergeLoaders = ModDownloadLib.McInstallLoader(Request, true);
            // 构造 Libraries 加载器
            var LoadersLib = new List<ModLoader.LoaderBase>();
            LoadersLib.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析游戏支持库文件（副加载器）", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionName))) { ProgressWeight = 1d, Show = false });
            LoadersLib.Add(new ModNet.LoaderDownload("下载游戏支持库文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 7d, Show = false });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, Block = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载游戏支持库文件", LoadersLib) { ProgressWeight = 8d });

            // 重复任务检查
            string LoaderName = "MMC 整合包安装：" + VersionName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            Loader.Start(Request.TargetVersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModBase.RunInUi(() => ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.DownloadManager));
            return Loader;
        }

        // MCBBS
        private static ModLoader.LoaderCombo<string> InstallPackMCBBS(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder, string VersionName = null)
        {
            // 读取 Json 文件
            JObject Json;
            try
            {
                var Entry = Archive.GetEntry(ArchiveBaseFolder + "mcbbs.packmeta") ?? Archive.GetEntry(ArchiveBaseFolder + "manifest.json");
                Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Entry.Open(), Encoding.UTF8));
            }
            catch (Exception ex)
            {
                throw new Exception("MCBBS 整合包安装信息存在问题", ex);
            }
            // 获取版本名
            if (VersionName is null)
            {
                VersionName = (string)(Json["name"] ?? "");
                var Validate = new ValidateFolderName(ModMinecraft.PathMcFolder + "versions");
                if (!string.IsNullOrEmpty(Validate.Validate(VersionName)))
                    VersionName = "";
                if (string.IsNullOrEmpty(VersionName))
                    VersionName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new System.Collections.ObjectModel.Collection<ValidateType>() { Validate });
                if (string.IsNullOrEmpty(VersionName))
                    throw new ModBase.CancelledException();
            }
            // 解压与配置文件
            string InstallTemp = ModMain.RequestTaskTempFolder();
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6d);
            Task.Progress = 0.6d;
            // 复制结果
            if (Directory.Exists(InstallTemp + ArchiveBaseFolder + "overrides"))
            {
                ModBase.CopyDirectory(InstallTemp + ArchiveBaseFolder + "overrides", ModMinecraft.PathMcFolder + @"versions\" + VersionName, Delta => Task.Progress += 0.35d * Delta);
            }
            else
            {
                ModBase.Log("[ModPack] 整合包中未找到 overrides 目录，已跳过");
            }
            Task.Progress = 0.95d;
            // 开启版本隔离
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndie", 1.ToString());
            ModBase.WriteIni($@"{ModMinecraft.PathMcFolder}versions\{VersionName}\PCL\Setup.ini", "VersionArgumentIndieV2", Conversions.ToString(true));
        })
            {
                ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 构造加载器
            if (Json["addons"] is null)
                throw new Exception("该 MCBBS 整合包未提供游戏版本附加信息，无法安装！");
            var Addons = new Dictionary<string, string>();
            foreach (var Entry in Json["addons"])
                Addons.Add((string)Entry["id"], (string)Entry["version"]);
            if (!Addons.ContainsKey("game"))
            {
                ModMain.Hint("该整合包未提供游戏版本信息，无法安装！", ModMain.HintType.Critical);
                return null;
            }
            var Request = new ModDownloadLib.McInstallRequest()
            {
                TargetVersionName = VersionName,
                TargetVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\",
                MinecraftName = Addons["game"],
                OptiFineVersion = Addons.ContainsKey("optifine") ? Addons["optifine"] : null,
                ForgeVersion = Addons.ContainsKey("forge") ? Addons["forge"] : null,
                NeoForgeVersion = Addons.ContainsKey("neoforge") ? Addons["neoforge"] : null,
                FabricVersion = Addons.ContainsKey("fabric") ? Addons["fabric"] : null,
                QuiltVersion = Addons.ContainsKey("quilt") ? Addons["quilt"] : null
            };
            var MergeLoaders = ModDownloadLib.McInstallLoader(Request, true);
            // 构造 Libraries 加载器
            var LoadersLib = new List<ModLoader.LoaderBase>();
            LoadersLib.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析游戏支持库文件（副加载器）", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionName))) { ProgressWeight = 1d, Show = false });
            LoadersLib.Add(new ModNet.LoaderDownload("下载游戏支持库文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 7d, Show = false });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, Block = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载游戏支持库文件", LoadersLib) { ProgressWeight = 8d });

            // 重复任务检查
            string LoaderName = "MCBBS 整合包安装：" + VersionName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            // If Archive IsNot Nothing Then Archive.Dispose() '解除占用，以免在加载器中触发 “正由另一进程使用，因此该进程无法访问此文件”
            Loader.Start(Request.TargetVersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModBase.RunInUi(() => ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.DownloadManager));
            return Loader;
        }

        // 带启动器的压缩包
        private static ModLoader.LoaderCombo<string> InstallPackLauncherPack(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder)
        {
            // 获取解压路径
            ModMain.MyMsgBox("接下来请选择一个空文件夹，它会被安装到这个文件夹里。", "安装", "继续", ForceWait: true);
            string TargetFolder = ModBase.SelectFolder("选择安装目标（必须是一个空文件夹）");
            if (string.IsNullOrEmpty(TargetFolder))
                throw new ModBase.CancelledException();
            if (Directory.GetFileSystemEntries(TargetFolder).Length > 0)
            {
                ModMain.Hint("请选择一个空文件夹作为安装目标！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }
            // 解压
            var Loader = new ModLoader.LoaderCombo<string>("解压压缩包", new[] {
                        new ModLoader.LoaderTask<string, int>("解压压缩包", (Task) =>
                {
                ExtractModpackFiles(TargetFolder, FileAddress, Task, 0.9d);
                Thread.Sleep(400); // 避免文件争用
                // 查找解压后的 exe 文件
                string Launcher = null;
                foreach (var ExeFile in Directory.GetFiles(TargetFolder, "*.exe", SearchOption.TopDirectoryOnly))
                    {
                    var Info = FileVersionInfo.GetVersionInfo(ExeFile);
                    ModBase.Log($"[Modpack] 文件 {ExeFile} 的产品名标识为 {Info.ProductName}");
                    if (Info.ProductName == "Plain Craft Launcher")
                        {
                        Launcher = ExeFile;
                        ModBase.Log($"[Modpack] 发现整合包附带的 PCL 启动器：{ExeFile}");
                        }
                                            else if ((Info.ProductName.ContainsF("Launcher", true) || Info.ProductName.ContainsF("启动", true)) && !(Info.ProductName == "Plain Craft Launcher Admin Manager"))
                        {
                        if (Launcher is null)
                            {
                            Launcher = ExeFile;
                            ModBase.Log($"[Modpack] 发现整合包附带的疑似第三方启动器：{ExeFile}");
                        }
                    }
                }
                Task.Progress = 0.95d;
                // 尝试使用附带的启动器打开
                if (Launcher is not null)
                    {
                    ModBase.Log("[Modpack] 找到压缩包中附带的启动器：" + Launcher);
                    if (ModMain.MyMsgBox($"整合包里似乎自带了启动器，是否换用它继续安装？{Constants.vbCrLf}即将打开：{Launcher}", "换用整合包启动器？", "换用", "不换用") == 1)
                        {
                        ModBase.OpenExplorer(TargetFolder);
                        ModBase.ShellOnly(Launcher, "--wait"); // 要求等待已有的 PCL 退出
                        ModBase.Log("[Modpack] 为换用整合包中的启动器启动，强制结束程序");
                        ModMain.FrmMain.EndProgram(false);
                        return;
                    }
                    }
                else
                    {
                    ModBase.Log("[Modpack] 未找到压缩包中附带的启动器");
                }
                ModBase.OpenExplorer(TargetFolder);
                // 加入文件夹列表
                string VersionName = ModBase.GetFolderNameFromPath(TargetFolder);
                Directory.CreateDirectory(TargetFolder + @".minecraft\");
                                                        PageSelectLeft.AddFolder(TargetFolder + @".minecraft\" + ArchiveBaseFolder.Replace("/", @"\").TrimStart('\\'), VersionName, false); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
                // 调用 modpack 文件进行安装
                string ModpackFile = Directory.GetFiles(TargetFolder, "modpack.*", SearchOption.AllDirectories).First();
                ModBase.Log("[Modpack] 调用 modpack 文件继续安装：" + ModpackFile);
                ModpackInstall(ModpackFile);
            })
        });
            Loader.Start(TargetFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            return Loader;
        }

        // 普通压缩包
        private static ModLoader.LoaderCombo<string> InstallPackCompress(string FileAddress, ZipArchive Archive)
        {
            // 尝试定位 .minecraft 文件夹：寻找形如 “/versions/XXX/XXX.json” 的路径
            System.Text.RegularExpressions.Match Match = null;
            var Regex = new System.Text.RegularExpressions.Regex(@"^.*\/(?=versions\/(?<ver>[^\/]+)\/(\k<ver>)\.json$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (var Entry in Archive.Entries)
            {
                var EntryMatch = Regex.Match("/" + Entry.FullName);
                if (EntryMatch.Success)
                {
                    Match = EntryMatch;
                    break;
                }
            }
            if (Match is null)
                throw new Exception("未能找到适合的文件结构，这可能不是一个 MC 压缩包"); // 没有匹配
            string ArchiveBaseFolder = Match.Value.Replace("/", @"\").TrimStart('\\'); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
            string VersionName = Match.Groups[1].Value;
            ModBase.Log("[ModPack] 检测到压缩包的 .minecraft 根目录：" + ArchiveBaseFolder + "，命中的版本名：" + VersionName);
            // 获取解压路径
            ModMain.MyMsgBox("接下来请选择一个空文件夹，它会被安装到这个文件夹里。", "安装", "继续", ForceWait: true);
            string TargetFolder = ModBase.SelectFolder("选择安装目标（必须是一个空文件夹）");
            if (string.IsNullOrEmpty(TargetFolder))
                throw new ModBase.CancelledException();
            if (TargetFolder.Contains("!") || TargetFolder.Contains(";"))
            {
                ModMain.Hint("Minecraft 文件夹路径中不能含有感叹号或分号！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }
            if (Directory.GetFileSystemEntries(TargetFolder).Length > 0)
            {
                ModMain.Hint("请选择一个空文件夹作为安装目标！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }
            // 解压
            var Loader = new ModLoader.LoaderCombo<string>("解压压缩包", new[] {
                        new ModLoader.LoaderTask<string, int>("解压压缩包", (Task) =>
                {
                ExtractModpackFiles(TargetFolder, FileAddress, Task, 0.95d);
                // 加入文件夹列表
                PageSelectLeft.AddFolder(TargetFolder + ArchiveBaseFolder, ModBase.GetFolderNameFromPath(TargetFolder), false);
                Thread.Sleep(400); // 避免文件争用
                ModBase.RunInUi(() => ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.VersionSelect));
            })
        })
            {
                OnStateChanged = ModDownloadLib.McInstallState
            };
            Loader.Start(TargetFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            return Loader;
        }

        #endregion

    }
}