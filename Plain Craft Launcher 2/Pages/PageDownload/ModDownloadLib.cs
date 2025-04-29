using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public static class ModDownloadLib
    {

        /// <summary>
    /// 如果 OptiFine 与 Forge 同时开始安装，就会导致 Forge 安装失败。
    /// </summary>
        private static object InstallSyncLock = new object();
        /// <summary>
    /// 如果 OptiFine 与 Forge 同时复制原版 Jar，就会导致复制文件时冲突。
    /// </summary>
        private static object VanillaSyncLock = new object();
        /// <summary>
    /// 最高的 Minecraft 大版本号，-1 代表尚未获取。
    /// </summary>
        public static int McVersionHighest = -1;

        #region Minecraft 下载

        /// <summary>
    /// 下载某个 Minecraft 版本，这会创造一个单独的下载任务，失败会跳过执行并要求反馈。
    /// 返回正在下载的任务，若跳过或失败，则返回 Nothing。
    /// </summary>
    /// <param name="Id">所下载的 Minecraft 的版本名。</param>
    /// <param name="JsonUrl">Json 文件的 Mojang 官方地址。</param>
        public static ModLoader.LoaderCombo<string> McDownloadClient(ModNet.NetPreDownloadBehaviour Behaviour, string Id, string JsonUrl = null)
        {
            try
            {
                string VersionFolder = ModMinecraft.PathMcFolder + @"versions\" + Id + @"\";

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"Minecraft {Id} 下载" ?? ""))
                        continue;
                    if (Behaviour == ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                        return (ModLoader.LoaderCombo<string>)OngoingLoader;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return (ModLoader.LoaderCombo<string>)OngoingLoader;
                }

                // 已有版本检查
                if (Behaviour != ModNet.NetPreDownloadBehaviour.IgnoreCheck && File.Exists(VersionFolder + Id + ".json") && File.Exists(VersionFolder + Id + ".jar"))
                {
                    if (Behaviour == ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                        return null;
                    if (ModMain.MyMsgBox("版本 " + Id + " 已存在，是否重新下载？" + Constants.vbCrLf + "这会覆盖版本的 Json 与 Jar 文件，但不会影响版本隔离的文件。", "版本已存在", "继续", "取消") == 1)
                    {
                        File.Delete(VersionFolder + Id + ".jar");
                        File.Delete(VersionFolder + Id + ".json");
                    }
                    else
                    {
                        return null;
                    }
                }

                // 启动
                var Loader = new ModLoader.LoaderCombo<string>("Minecraft " + Id + " 下载", McDownloadClientLoader(Id, JsonUrl)) { OnStateChanged = McInstallState };
                Loader.Start(VersionFolder);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
                return Loader;
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 Minecraft 下载失败", ModBase.LogLevel.Feedback);
                return null;
            }
        }
        /// <summary>
    /// 保存某个 Minecraft 版本的核心文件（仅 Json 与核心 Jar）。
    /// </summary>
    /// <param name="Id">所下载的 Minecraft 的版本名。</param>
    /// <param name="JsonUrl">Json 文件的 Mojang 官方地址。</param>
        public static void McDownloadClientCore(string Id, string JsonUrl, ModNet.NetPreDownloadBehaviour Behaviour)
        {
            try
            {
                string VersionFolder = ModBase.SelectFolder();
                if (!VersionFolder.Contains(@"\"))
                    return;
                VersionFolder = VersionFolder + Id + @"\";

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
                {
                    if ((OngoingLoader.Name ?? "") != ($"Minecraft {Id} 下载" ?? ""))
                        continue;
                    if (Behaviour == ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                        return;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                var Loaders = new List<ModLoader.LoaderBase>();
                // 下载版本 Json 文件
                Loaders.Add(new ModNet.LoaderDownload("下载版本 Json 文件", new List<ModNet.NetFile>() { new ModNet.NetFile(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl), VersionFolder + Id + ".json", new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true)) }) { ProgressWeight = 2d });
                // 获取支持库文件地址
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析核心 Jar 文件下载地址", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionFolder))) { ProgressWeight = 0.5d, Show = false });
                // 下载支持库文件
                Loaders.Add(new ModNet.LoaderDownload("下载核心 Jar 文件", new List<ModNet.NetFile>()) { ProgressWeight = 5d });

                // 启动
                var Loader = new ModLoader.LoaderCombo<string>("Minecraft " + Id + " 下载", Loaders) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(Id);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 Minecraft 下载失败", ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
    /// 获取下载某个 Minecraft 版本的加载器列表。
    /// 它必须安装到 PathMcFolder，但是可以自定义版本名（不过自定义的版本名不会修改 Json 中的 id 项）。
    /// </summary>
        private static List<ModLoader.LoaderBase> McDownloadClientLoader(string Id, string JsonUrl = null, string VersionName = null)
        {
            VersionName = VersionName ?? Id;
            string VersionFolder = ModMinecraft.PathMcFolder + @"versions\" + VersionName + @"\";

            var Loaders = new List<ModLoader.LoaderBase>();

            // 下载版本 Json 文件
            if (JsonUrl is null)
            {
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("获取原版 Json 文件下载地址", (Task) =>
        {
            string JsonAddress = Conversions.ToString(ModDownload.DlClientListGet(Id));
            Task.Output = new List<ModNet.NetFile>() { new ModNet.NetFile(ModDownload.DlSourceLauncherOrMetaGet(JsonAddress), VersionFolder + VersionName + ".json") };
        })
                {
                    ProgressWeight = 2d,
                    Show = false
                });
            }
            Loaders.Add(new ModNet.LoaderDownload(McDownloadClientJsonName, new List<ModNet.NetFile>() { new ModNet.NetFile(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl ?? ""), VersionFolder + VersionName + ".json", new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true)) }) { ProgressWeight = 3d });

            // 下载支持库文件
            var LoadersLib = new List<ModLoader.LoaderBase>();
            LoadersLib.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析原版支持库文件（副加载器）", (Task) =>
        {
            Thread.Sleep(50); // 等待 JSON 文件实际写入硬盘（#3710）
            ModBase.Log("[Download] 开始分析原版支持库文件：" + VersionFolder);
            Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionFolder));
        })
            {
                ProgressWeight = 1d,
                Show = false
            });
            LoadersLib.Add(new ModNet.LoaderDownload("下载原版支持库文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 13d, Show = false });
            Loaders.Add(new ModLoader.LoaderCombo<string>(McDownloadClientLibName, LoadersLib) { Block = false, ProgressWeight = 14d });

            // 下载资源文件
            var LoadersAssets = new List<ModLoader.LoaderBase>();
            LoadersAssets.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析资源文件索引地址（副加载器）", (Task) =>
        {
            try
            {
                var Version = new ModMinecraft.McVersion(VersionFolder);
                Task.Output = new List<ModNet.NetFile>() { ModDownload.DlClientAssetIndexGet(Version) };
            }
            catch (Exception ex)
            {
                throw new Exception("分析资源文件索引地址失败", ex);
            }
            // 顺手添加 Json 项目
            try
            {
                JObject VersionJson = (JObject)ModBase.GetJson(ModBase.ReadFile(VersionFolder + VersionName + ".json"));
                VersionJson.Add("clientVersion", Id);
                ModBase.WriteFile(VersionFolder + VersionName + ".json", VersionJson.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("添加客户端版本失败", ex);
            }
        })
            {
                ProgressWeight = 1d,
                Show = false
            });
            LoadersAssets.Add(new ModNet.LoaderDownload("下载资源文件索引（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 3d, Show = false });
            LoadersAssets.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析所需资源文件（副加载器）", (Task) =>
        {
            ModLoader.LoaderBase argProgressFeed = Task;
            Task.Output = ModMinecraft.McAssetsFixList(new ModMinecraft.McVersion(VersionFolder), true, ref argProgressFeed);
            Task = (ModLoader.LoaderTask<string, List<ModNet.NetFile>>)argProgressFeed;
        })
            {
                ProgressWeight = 3d,
                Show = false
            });
            LoadersAssets.Add(new ModNet.LoaderDownload("下载资源文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 14d, Show = false });
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载原版资源文件", LoadersAssets) { Block = false, ProgressWeight = 21d });

            return Loaders;

        }
        private const string McDownloadClientLibName = "下载原版支持库文件";
        private const string McDownloadClientJsonName = "下载原版 Json 文件";

        #endregion

        #region Minecraft 下载菜单

        public static MyListItem McDownloadListItem(JObject Entry, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 确定图标
            string Logo;
            switch ("type")
            {
                case "release":
                    {
                        Logo = ModBase.PathImage + "Blocks/Grass.png";
                        break;
                    }
                case "snapshot":
                    {
                        Logo = ModBase.PathImage + "Blocks/CommandBlock.png";
                        break;
                    }
                case "special":
                    {
                        Logo = ModBase.PathImage + "Blocks/GoldBlock.png";
                        break;
                    }

                default:
                    {
                        Logo = ModBase.PathImage + "Blocks/CobbleStone.png";
                        break;
                    }
            }
            // 建立控件
            var NewItem = new MyListItem() { Logo = Logo, SnapsToDevicePixels = true, Title = Entry["id"].ToString(), Height = 42d, Type = MyListItem.CheckType.Clickable, Tag = Entry };
            if (Entry["lore"] is null)
            {
                NewItem.Info = Entry["releaseTime"].Value<DateTime>().ToString("yyyy'/'MM'/'dd HH':'mm");
            }
            else
            {
                NewItem.Info = Entry["lore"].ToString();
            }
            if (Entry["url"].ToString().Contains("unlisted-versions-of-minecraft"))
                NewItem.Info = "[UVMC 特供下载] " + NewItem.Info;
            NewItem.Click += OnClick;
            // 建立菜单
            if (IsSaveOnly)
            {
                NewItem.ContentHandler = McDownloadSaveMenuBuild;
            }
            else
            {
                NewItem.ContentHandler = McDownloadMenuBuild;
            }
            // 结束
            return NewItem;
        }
        private static void McDownloadSaveMenuBuild(object sender, EventArgs e)
        {
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += ModDownloadLib.McDownloadMenuLog;
            var BtnServer = new MyIconButton() { LogoScale = 1d, Logo = ModBase.Logo.IconButtonServer, ToolTip = "下载服务端" };
            ToolTipService.SetPlacement(BtnServer, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnServer, 30d);
            ToolTipService.SetHorizontalOffset(BtnServer, 2d);
            BtnServer.Click += ModDownloadLib.McDownloadMenuSaveServer;
            ((dynamic)sender).Buttons = new[] { BtnServer, BtnInfo };
        }
        private static void McDownloadMenuBuild(object sender, EventArgs e)
        {
            var BtnSave = new MyIconButton() { Logo = ModBase.Logo.IconButtonSave, ToolTip = "另存为" };
            ToolTipService.SetPlacement(BtnSave, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnSave, 30d);
            ToolTipService.SetHorizontalOffset(BtnSave, 2d);
            BtnSave.Click +=  ModDownloadLib.McDownloadMenuSave;
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += ModDownloadLib.McDownloadMenuLog;
            var BtnServer = new MyIconButton() { LogoScale = 1d, Logo = ModBase.Logo.IconButtonServer, ToolTip = "下载服务端" };
            ToolTipService.SetPlacement(BtnServer, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnServer, 30d);
            ToolTipService.SetHorizontalOffset(BtnServer, 2d);
            BtnServer.Click += ModDownloadLib.McDownloadMenuSaveServer;
            ((dynamic)sender).Buttons = new[] { BtnSave, BtnInfo, BtnServer };
        }
        private static void McDownloadMenuLog(object sender, EventArgs e)
        {
            JToken Version;
            if (((dynamic)sender).Tag is not null)
            {
                Version = (JToken)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Version = (JToken)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Version = (JToken)((dynamic)sender).Parent.Parent.Tag;
            }
            McUpdateLogShow(Version);
        }
        private static void McDownloadMenuSaveServer(object sender, EventArgs e)
        {
            MyListItem Version;
            if (sender is MyListItem)
            {
                Version = (MyListItem)sender;
            }
            else if (((dynamic)sender).Parent is MyListItem)
            {
                Version = (MyListItem)((dynamic)sender).Parent;
            }
            else
            {
                Version = (MyListItem)((dynamic)sender).Parent.Parent;
            }
            try
            {
                string Id = Version.Title;
                string JsonUrl = ((JObject)Version.Tag)["url"].ToString();
                string VersionFolder = ModBase.SelectFolder();
                if (!VersionFolder.Contains(@"\"))
                    return;
                VersionFolder = VersionFolder + Id + @"\";

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"Minecraft {Id} 服务端下载" ?? ""))
                        continue;
                    ModMain.Hint("该服务端正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                var Loaders = new List<ModLoader.LoaderBase>();
                // 下载版本 JSON 文件
                Loaders.Add(new ModNet.LoaderDownload("下载版本 JSON 文件", new List<ModNet.NetFile>() { new ModNet.NetFile(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl), VersionFolder + Id + ".json", new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true)) }) { ProgressWeight = 2d });
                // 构建服务端
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("构建服务端", (Task) =>
    {
        // 分析服务端 JAR 文件下载地址
        var McVersion = new ModMinecraft.McVersion(VersionFolder);
        if (McVersion.JsonObject["downloads"] is null || McVersion.JsonObject["downloads"]["server"] is null || McVersion.JsonObject["downloads"]["server"]["url"] is null)
        {
            File.Delete(VersionFolder + Id + ".json");
            if (!new DirectoryInfo(VersionFolder).GetFileSystemInfos().Any())
                Directory.Delete(VersionFolder);
            Task.Output = new List<ModNet.NetFile>();
            ModMain.Hint($"Mojang 没有给 Minecraft {Id} 提供官方服务端下载，没法下，撤退！", ModMain.HintType.Critical);
            Thread.Sleep(2000); // 等玩家把上一个提示看完
            Task.Abort();
            return;
        }
        string JarUrl = (string)McVersion.JsonObject["downloads"]["server"]["url"];
        var Checker = new ModBase.FileChecker(MinSize: 1024L, ActualSize: (long)(McVersion.JsonObject["downloads"]["server"]["size"] ?? -1), Hash: (string)McVersion.JsonObject["downloads"]["server"]["sha1"]);
        Task.Output = new List<ModNet.NetFile>() { new ModNet.NetFile(ModDownload.DlSourceLauncherOrMetaGet(JarUrl), VersionFolder + Id + "-server.jar", Checker) };
        // 添加启动脚本
        string Bat = $@"@echo off
title {Id} 原版服务端
echo 如果服务端立即停止，请右键编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java.exe 的路径。
echo 你可以在 PCL 的 [设置 → 启动选项] 中查看已安装的 java，所需的 java.exe 一般在其中的 bin 文件夹下。
echo ------------------------------
echo 如果提示 ""You need to agree to the EULA in order to run the server""，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。
echo ------------------------------
""java"" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar {Id}-server.jar nogui
echo ----------------------
echo 服务端已停止。
pause";
        ModBase.WriteFile(VersionFolder + "Launch Server.bat", Bat, Encoding: Encoding.Default.Equals(Encoding.UTF8) ? Encoding.UTF8 : Encoding.GetEncoding("GB18030"));
        // 删除版本 JSON
        File.Delete(VersionFolder + Id + ".json");
    })
                {
                    ProgressWeight = 0.5d,
                    Show = false
                });
                // 下载服务端文件
                Loaders.Add(new ModNet.LoaderDownload("下载服务端文件", new List<ModNet.NetFile>()) { ProgressWeight = 5d });

                // 启动
                var Loader = new ModLoader.LoaderCombo<string>("Minecraft " + Id + " 服务端下载", Loaders) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(Id);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 Minecraft 服务端下载失败", ModBase.LogLevel.Feedback);
            }
        }
        public static void McDownloadMenuSave(object sender, EventArgs e)
        {
            MyListItem Version;
            if (sender is MyListItem)
            {
                Version = (MyListItem)sender;
            }
            else if (((dynamic)sender).Parent is MyListItem)
            {
                Version = (MyListItem)((dynamic)sender).Parent;
            }
            else
            {
                Version = (MyListItem)((dynamic)sender).Parent.Parent;
            }
            try
            {
                string Id = Version.Title;
                string JsonUrl = ((JObject)Version.Tag)["url"].ToString();
                string VersionFolder = ModBase.SelectFolder();
                if (!VersionFolder.Contains(@"\"))
                    return;
                VersionFolder = VersionFolder + Id + @"\";

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"Minecraft {Id} 下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                var Loaders = new List<ModLoader.LoaderBase>();
                // 下载版本 JSON 文件
                Loaders.Add(new ModNet.LoaderDownload("下载版本 JSON 文件", new List<ModNet.NetFile>() { new ModNet.NetFile(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl), VersionFolder + Id + ".json", new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true)) }) { ProgressWeight = 2d });
                // 获取支持库文件地址
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析核心 JAR 文件下载地址", Task => Task.Output = new List<ModNet.NetFile>() { ModDownload.DlClientJarGet(new ModMinecraft.McVersion(VersionFolder), false) }) { ProgressWeight = 0.5d, Show = false });
                // 下载支持库文件
                Loaders.Add(new ModNet.LoaderDownload("下载核心 JAR 文件", new List<ModNet.NetFile>()) { ProgressWeight = 5d });

                // 启动
                var Loader = new ModLoader.LoaderCombo<string>("Minecraft " + Id + " 下载", Loaders) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(Id);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 Minecraft 下载失败", ModBase.LogLevel.Feedback);
            }
        }
        /// <summary>
    /// 显示某 Minecraft 版本的更新日志。
    /// </summary>
    /// <param name="VersionJson">在 version_manifest.json 中的对应项。</param>
        public static void McUpdateLogShow(JToken VersionJson)
        {
            string WikiName;
            string Id = VersionJson["id"].ToString().ToLower();
            switch (Id ?? "")
            {
                case "3d shareware v1.34":
                    {
                        WikiName = "3D_Shareware_v1.34";
                        break;
                    }
                case "2.0":
                case "2point0":
                    {
                        WikiName = "Java版2.0";
                        break;
                    }
                case "1.rv-pre1":
                    {
                        WikiName = "Java版1.RV-Pre1";
                        break;
                    }
                case "combat test 1":
                case "combat-1":
                case "combat-212796":
                    {
                        WikiName = "Java版1.14.3_-_Combat_Test";
                        break;
                    }
                case "combat test 2":
                case "combat-2":
                case "combat-0":
                    {
                        WikiName = "Java版Combat_Test_2";
                        break;
                    }
                case "combat test 3":
                case "1.14_combat-3":
                    {
                        WikiName = "Java版Combat_Test_3";
                        break;
                    }
                case "combat test 4":
                case "1.15_combat-1":
                    {
                        WikiName = "Java版Combat_Test_4";
                        break;
                    }
                case "combat test 5":
                case "1.15_combat-6":
                    {
                        WikiName = "Java版Combat_Test_5";
                        break;
                    }
                case "combat test 6":
                case "1.16_combat-0":
                    {
                        WikiName = "Java版Combat_Test_6";
                        break;
                    }
                case "combat test 7c":
                case "1.16_combat-3":
                    {
                        WikiName = "Java版Combat_Test_7c";
                        break;
                    }
                case "combat test 8b":
                case "1.16_combat-5":
                    {
                        WikiName = "Java版Combat_Test_8b";
                        break;
                    }
                case "combat test 8c":
                case "1.16_combat-6":
                    {
                        WikiName = "Java版Combat_Test_8c";
                        break;
                    }
                case "1.0.0-rc1":
                    {
                        WikiName = "Java版RC1";
                        break;
                    }
                case "in-20100206-2103":
                    {
                        WikiName = "Java版Indev_20100206";
                        break;
                    }

                default:
                    {
                        if (Id.StartsWith("1.0.0-rc2"))
                        {
                            WikiName = "Java版RC2";
                        }
                        else if (Id.StartsWith("b1.8-pre1"))
                        {
                            WikiName = "Java版Beta_1.8-pre1";
                        }
                        else if (Id.StartsWith("b1.1-"))
                        {
                            WikiName = "Java版Beta_1.1";
                        }
                        else if (Id.StartsWith("a1.2.2"))
                        {
                            WikiName = "Java版Alpha_v1.2.2";
                        }
                        else if (Id.StartsWith("a1.1.0"))
                        {
                            WikiName = "Java版Alpha_v1.1.0";
                        }
                        else if (Id.StartsWith("a1.0.14"))
                        {
                            WikiName = "Java版Alpha_v1.0.14";
                        }
                        else if (Id.StartsWith("a1.0.13_01"))
                        {
                            WikiName = "Java版Alpha_v1.0.13_01";
                        }
                        else if (Id.StartsWith("inf-20100630"))
                        {
                            WikiName = "Java版Infdev_20100630";
                        }
                        else if (Id.StartsWith("in-20100214"))
                        {
                            WikiName = "Java版Indev_20100214";
                        }
                        else if (Id.StartsWith("1.19_deep_dark_experimental_snapshot-") || Id.StartsWith("1_19_deep_dark_experimental_snapshot-"))
                        {
                            WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Java版Deep_Dark_Experimental_Snapshot_");
                        }
                        else if (Id.Contains("b1.9"))
                        {
                            WikiName = "Java版" + Id.Replace("b", "Beta_").Replace("-pre", "_Prerelease_");
                        }
                        else if (Id == "c0.30_01c" || Id == "c0.30_survival" || Id.Contains("生存测试") || Id == "c0.30-1" || Id == "c0.30-2")
                        {
                            WikiName = "Java版Classic_0.30（生存模式）";
                        }
                        else if (Id.StartsWith("c0.31") || Id == "in-20100130")
                        {
                            WikiName = "Java版Indev_0.31_20100130";
                        }
                        else if (Id == "b1.6-tb3")
                        {
                            WikiName = "Java版Beta_1.6_Test_Build_3";
                        }
                        else if ((string)VersionJson["type"] == "release" || (string)VersionJson["type"] == "snapshot" || (string)VersionJson["type"] == "special")
                        {
                            WikiName = (Id.Contains("w") ? "" : "Java版") + Id.Replace(" Pre-Release ", "-pre");
                        }
                        else if (Id.StartsWith("b"))
                        {
                            WikiName = "Java版" + Id.Replace("b", "Beta_").Replace("tb3", "Test_Build_3");
                        }
                        else if (Id.StartsWith("a"))
                        {
                            WikiName = "Java版" + Id.Replace("a", "Alpha_v");
                        }
                        else if (Id.StartsWith("inf-"))
                        {
                            WikiName = "Java版" + Id.Replace("inf-", "Infdev_");
                        }
                        else if (Id.StartsWith("in-"))
                        {
                            WikiName = "Java版" + Id.Replace("in-", "Indev_");
                        }
                        else if (Id.StartsWith("c"))
                        {
                            WikiName = "Java版" + Id.Replace("c", "Classic_").Replace("st", "SURVIVAL_TEST");
                        }
                        else if (Id.StartsWith("rd-"))
                        {
                            WikiName = "Java版Pre-classic_" + Id;
                        }
                        else
                        {
                            ModBase.Log("[Error] 未知的版本格式：" + Id + "。", ModBase.LogLevel.Feedback);
                            return;
                        }

                        break;
                    }
            }
            ModBase.OpenWebsite("https://zh.minecraft.wiki/w/Special:Search?search=" + WikiName.Replace("_experimental-snapshot-", "-exp"));
        }

        #endregion

        #region OptiFine 下载

        public static void McDownloadOptiFine(ModDownload.DlOptiFineListEntry DownloadInfo)
        {
            try
            {
                string Id = DownloadInfo.NameVersion;
                string VersionFolder = ModMinecraft.PathMcFolder + @"versions\" + Id + @"\";
                bool IsNewVersion = ModBase.Val(DownloadInfo.Inherit.Split(".")[1]) >= 14d;
                string Target = IsNewVersion ? ModBase.PathTemp + @"Cache\Code\" + DownloadInfo.NameVersion + "_" + ModBase.GetUuid() : ModMinecraft.PathMcFolder + @"libraries\optifine\OptiFine\" + DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "") + @"\" + DownloadInfo.NameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", "");

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
                {
                    if ((OngoingLoader.Name ?? "") != ($"OptiFine {DownloadInfo.NameDisplay} 下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                // 已有版本检查
                if (File.Exists(VersionFolder + Id + ".json"))
                {
                    if (ModMain.MyMsgBox("版本 " + Id + " 已存在，是否重新下载？" + Constants.vbCrLf + "这会覆盖版本的 Json 和 Jar 文件，但不会影响版本隔离的文件。", "版本已存在", "继续", "取消") == 1)
                    {
                        File.Delete(VersionFolder + Id + ".jar");
                        File.Delete(VersionFolder + Id + ".json");
                    }
                    else
                    {
                        return;
                    }
                }

                // 启动
                var Loader = new ModLoader.LoaderCombo<string>("OptiFine " + DownloadInfo.NameDisplay + " 下载", McDownloadOptiFineLoader(DownloadInfo)) { OnStateChanged = McInstallState };
                Loader.Start(VersionFolder);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 OptiFine 下载失败", ModBase.LogLevel.Feedback);
            }
        }

        private static void McDownloadOptiFineSave(ModDownload.DlOptiFineListEntry DownloadInfo)
        {
            try
            {
                string Id = DownloadInfo.NameVersion;
                string Target = ModBase.SelectSaveFile("选择保存位置", DownloadInfo.NameFile, "OptiFine Jar (*.jar)|*.jar");
                if (!Target.Contains(@"\"))
                    return;

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"OptiFine {DownloadInfo.NameDisplay} 下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                var Loader = new ModLoader.LoaderCombo<ModDownload.DlOptiFineListEntry>("OptiFine " + DownloadInfo.NameDisplay + " 下载", McDownloadOptiFineSaveLoader(DownloadInfo, Target)) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(DownloadInfo);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 OptiFine 下载失败", ModBase.LogLevel.Feedback);
            }
        }
        private static void McDownloadOptiFineInstall(string BaseMcFolderHome, string Target, ModLoader.LoaderTask<List<ModNet.NetFile>, bool> Task, bool UseJavaWrapper)
        {
            // 选择 Java
            ModJava.JavaEntry Java;
            lock (ModJava.JavaLock)
            {
                Java = ModJava.JavaSelect("已取消安装。", new Version(1, 8, 0, 0));
                if (Java is null)
                {
                    if (!ModJava.JavaDownloadConfirm("Java 8 或更高版本"))
                        throw new Exception("由于未找到 Java，已取消安装。");
                    // 开始自动下载
                    var JavaLoader = ModJava.JavaFixLoaders(17);
                    try
                    {
                        JavaLoader.Start(17, IsForceRestart: true);
                        while (JavaLoader.State == ModBase.LoadState.Loading && !Task.IsAborted)
                            Thread.Sleep(10);
                    }
                    finally
                    {
                        JavaLoader.Abort(); // 确保取消时中止 Java 下载
                    }
                    // 检查下载结果
                    Java = ModJava.JavaSelect("已取消安装。", new Version(1, 8, 0, 0));
                    if (Task.IsAborted)
                        return;
                    if (Java is null)
                        throw new Exception("由于未找到 Java，已取消安装。");
                }
            }
            // 添加 Java Wrapper 作为主 Jar
            string Arguments;
            if (UseJavaWrapper && !(bool)ModBase.Setup.Get("LaunchAdvanceDisableJLW"))
            {
                Arguments = $"-Doolloo.jlw.tmpdir=\"{ModBase.PathPure.TrimEnd('\\')}\" -Duser.home=\"{BaseMcFolderHome.TrimEnd('\\')}\" -cp \"{Target}\" -jar \"{ModLaunch.ExtractJavaWrapper()}\" optifine.Installer";
            }
            else
            {
                Arguments = $"-Duser.home=\"{BaseMcFolderHome.TrimEnd('\\')}\" -cp \"{Target}\" optifine.Installer";
            }
            if (Java.VersionCode >= 9)
                Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + Arguments;
            // 开始启动
            lock (InstallSyncLock)
            {
                var Info = new ProcessStartInfo()
                {
                    FileName = Java.PathJavaw,
                    Arguments = Arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = ModBase.ShortenPath(BaseMcFolderHome)
                };
                if (Info.EnvironmentVariables.ContainsKey("appdata"))
                {
                    Info.EnvironmentVariables["appdata"] = BaseMcFolderHome;
                }
                else
                {
                    Info.EnvironmentVariables.Add("appdata", BaseMcFolderHome);
                }
                ModBase.Log("[Download] 开始安装 OptiFine：" + Target);
                int TotalLength = 0;
                var process = new Process() { StartInfo = Info };
                string LastResult = "";
                using (var outputWaitHandle = new AutoResetEvent(false))
                {
                    using (var errorWaitHandle = new AutoResetEvent(false))
                    {
                        process.OutputDataReceived += (sender, e) =>
        {
            try
            {
                if (e.Data is null)
                {
                    outputWaitHandle.Set();
                }
                else
                {
                    LastResult = e.Data;
                    if (ModBase.ModeDebug)
                        ModBase.Log("[Installer] " + LastResult);
                    TotalLength += 1;
                    Task.Progress += 0.9d / 7000d;
                }
            }
            catch (ObjectDisposedException ex)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取 OptiFine 安装器信息失败");
            }
            try
            {
                if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                {
                    ModBase.Log("[Installer] 由于任务取消，已中止 OptiFine 安装");
                    process.Kill();
                }
            }
            catch
            {
            }
        };
                        process.ErrorDataReceived += (sender, e) =>
        {
            try
            {
                if (e.Data is null)
                {
                    errorWaitHandle.Set();
                }
                else
                {
                    LastResult = e.Data;
                    if (ModBase.ModeDebug)
                        ModBase.Log("[Installer] " + LastResult);
                    TotalLength += 1;
                    Task.Progress += 0.9d / 7000d;
                }
            }
            catch (ObjectDisposedException ex)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取 OptiFine 安装器错误信息失败");
            }
            try
            {
                if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                {
                    ModBase.Log("[Installer] 由于任务取消，已中止 OptiFine 安装");
                    process.Kill();
                }
            }
            catch
            {
            }
        };
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        // 等待
                        while (!process.HasExited)
                            Thread.Sleep(10);
                        // 输出
                        outputWaitHandle.WaitOne(10000);
                        errorWaitHandle.WaitOne(10000);
                        process.Dispose();
                        if (TotalLength < 1000 || LastResult.Contains("at "))
                            throw new Exception("安装器运行出错，末行为 " + LastResult);
                    }
                }
            }
        }

        /// <summary>
    /// 获取下载某个 OptiFine 版本的加载器列表。
    /// </summary>
        private static List<ModLoader.LoaderBase> McDownloadOptiFineLoader(ModDownload.DlOptiFineListEntry DownloadInfo, string McFolder = null, ModLoader.LoaderCombo<string> ClientDownloadLoader = null, string ClientFolder = null, bool FixLibrary = true)
        {

            // 参数初始化
            McFolder = McFolder ?? ModMinecraft.PathMcFolder;
            bool IsCustomFolder = (McFolder ?? "") != (ModMinecraft.PathMcFolder ?? "");
            string Id = DownloadInfo.NameVersion;
            string VersionFolder = McFolder + @"versions\" + Id + @"\";
            bool IsNewVersion = DownloadInfo.Inherit.Contains("w") || ModBase.Val(DownloadInfo.Inherit.Split(".")[1]) >= 14d;
            string Target = IsNewVersion ? $"{ModMain.RequestTaskTempFolder()}OptiFine.jar" : $@"{McFolder}libraries\optifine\OptiFine\{DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "")}\{DownloadInfo.NameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", "")}";
            var Loaders = new List<ModLoader.LoaderBase>();

            // 获取下载地址
            Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("获取 OptiFine 主文件下载地址", (Task) =>
        {
            // 启动依赖版本的下载
            if (ClientDownloadLoader is null)
            {
                if (IsCustomFolder)
                    throw new Exception("如果没有指定原版下载器，则不能指定 MC 安装文件夹");
                ClientDownloadLoader = McDownloadClient(ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, DownloadInfo.Inherit);
            }
            Task.Progress = 0.1d;
            var Sources = new List<string>();
            // BMCLAPI 源
            string BmclapiInherit = DownloadInfo.Inherit;
            if (BmclapiInherit == "1.8" || BmclapiInherit == "1.9")
                BmclapiInherit += ".0"; // #4281
            if (DownloadInfo.IsPreview)
            {
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U_" + DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit + " ", "").Replace(" ", "/"));
            }
            else
            {
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U/" + DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit + " ", ""));
            }
            // 官方源
            string PageData;
            try
            {
                PageData = ModNet.NetGetCodeByClient("https://optifine.net/adloadx?f=" + DownloadInfo.NameFile, new UTF8Encoding(false), 15000, "text/html", true);
                Task.Progress = 0.8d;
                Sources.Add("https://optifine.net/" + PageData.RegexSearch(@"downloadx\?f=[^""']+")[0]);
                ModBase.Log("[Download] OptiFine " + DownloadInfo.NameDisplay + " 官方下载地址：" + Sources.Last());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 OptiFine " + DownloadInfo.NameDisplay + " 官方下载地址失败");
            }
            // 构造文件请求
            Task.Output = new List<ModNet.NetFile>() { new ModNet.NetFile(Sources.ToArray(), Target, new ModBase.FileChecker(MinSize: 300 * 1024)) };
        })
            {
                ProgressWeight = 8d
            });
            Loaders.Add(new ModNet.LoaderDownload("下载 OptiFine 主文件", new List<ModNet.NetFile>()) { ProgressWeight = 8d });
            Loaders.Add(new ModLoader.LoaderTask<List<ModNet.NetFile>, bool>("等待原版下载", (Task) =>
        {
            // 等待原版文件下载完成
            if (ClientDownloadLoader is null)
                return;
            var TargetLoaders = ClientDownloadLoader.GetLoaderList().Where(l => (l.Name ?? "") == McDownloadClientLibName || (l.Name ?? "") == McDownloadClientJsonName).Where(l => l.State != ModBase.LoadState.Finished).ToList();
            if (TargetLoaders.Any())
                ModBase.Log("[Download] OptiFine 安装正在等待原版文件下载完成");
            while (TargetLoaders.Any() && !Task.IsAborted)
            {
                TargetLoaders = TargetLoaders.Where(l => l.State != ModBase.LoadState.Finished).ToList();
                Thread.Sleep(50);
            }
            if (Task.IsAborted)
                return;
            // 拷贝原版文件
            if (!IsCustomFolder)
                return;
            lock (VanillaSyncLock)
            {
                string ClientName = ModBase.GetFolderNameFromPath(ClientFolder);
                Directory.CreateDirectory(McFolder + @"versions\" + DownloadInfo.Inherit);
                if (!File.Exists(McFolder + @"versions\" + DownloadInfo.Inherit + @"\" + DownloadInfo.Inherit + ".json"))
                {
                    ModBase.CopyFile($"{ClientFolder}{ClientName}.json", $@"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.json");
                }
                if (!File.Exists(McFolder + @"versions\" + DownloadInfo.Inherit + @"\" + DownloadInfo.Inherit + ".jar"))
                {
                    ModBase.CopyFile($"{ClientFolder}{ClientName}.jar", $@"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.jar");
                }
            }
        })
            {
                ProgressWeight = 0.1d,
                Show = false
            });

            // 安装（新旧方式均需要原版 Jar 和 Json）
            if (IsNewVersion)
            {
                ModBase.Log("[Download] 检测为新版 OptiFine：" + DownloadInfo.Inherit);
                Loaders.Add(new ModLoader.LoaderTask<List<ModNet.NetFile>, bool>("安装 OptiFine（方式 A）", (Task) =>
        {
            string BaseMcFolderHome = ModMain.RequestTaskTempFolder();
            string BaseMcFolder = BaseMcFolderHome + @".minecraft\";
            try
            {
                // 准备安装环境
                if (Directory.Exists(BaseMcFolder + @"versions\" + DownloadInfo.Inherit))
                {
                    ModBase.DeleteDirectory(BaseMcFolder + @"versions\" + DownloadInfo.Inherit);
                }
                Directory.CreateDirectory(BaseMcFolder + @"versions\" + DownloadInfo.Inherit + @"\");
                ModMinecraft.McFolderLauncherProfilesJsonCreate(BaseMcFolder);
                ModBase.CopyFile(McFolder + @"versions\" + DownloadInfo.Inherit + @"\" + DownloadInfo.Inherit + ".json", BaseMcFolder + @"versions\" + DownloadInfo.Inherit + @"\" + DownloadInfo.Inherit + ".json");
                ModBase.CopyFile(McFolder + @"versions\" + DownloadInfo.Inherit + @"\" + DownloadInfo.Inherit + ".jar", BaseMcFolder + @"versions\" + DownloadInfo.Inherit + @"\" + DownloadInfo.Inherit + ".jar");
                Task.Progress = 0.06d;
                // 进行安装
                bool UseJavaWrapper = true;
            Retry:
                ;

                try
                {
                    McDownloadOptiFineInstall(BaseMcFolderHome, Target, Task, UseJavaWrapper);
                }
                catch (Exception ex)
                {
                    if (UseJavaWrapper)
                    {
                        ModBase.Log(ex, "使用 JavaWrapper 安装 OptiFine 失败，将不使用 JavaWrapper 并重试");
                        UseJavaWrapper = false;
                        goto Retry;
                    }
                    else
                    {
                        throw new Exception("运行 OptiFine 安装器失败", ex);
                    }
                }
                Task.Progress = 0.96d;
                // 复制文件
                File.Delete(BaseMcFolder + "launcher_profiles.json");
                ModBase.CopyDirectory(BaseMcFolder, McFolder);
                Task.Progress = 0.98d;
                // 清理文件
                File.Delete(Target);
                ModBase.DeleteDirectory(BaseMcFolderHome);
            }
            catch (Exception ex)
            {
                throw new Exception("安装 OptiFine（方式 A）失败", ex);
            }
        })
                {
                    ProgressWeight = 8d
                });
            }
            else
            {
                ModBase.Log("[Download] 检测为旧版 OptiFine：" + DownloadInfo.Inherit);
                // 新建版本文件夹
                // 复制 Jar 文件
                // 建立 Json 文件
                Loaders.Add(new ModLoader.LoaderTask<List<ModNet.NetFile>, bool>("安装 OptiFine（方式 B）", (Task) =>
                {
                    try
                    {
                        Directory.CreateDirectory(VersionFolder); Task.Progress = 0.1d; if (File.Exists(VersionFolder + Id + ".jar")) File.Delete(VersionFolder + Id + ".jar"); ModBase.CopyFile(McFolder + @"versions\" + DownloadInfo.Inherit + @"\" + DownloadInfo.Inherit + ".jar", VersionFolder + Id + ".jar"); Task.Progress = 0.7d; var InheritVersion = new ModMinecraft.McVersion(McFolder + @"versions\" + DownloadInfo.Inherit); string Json = @"{
    ""id"": """ + Id + @""",
    ""inheritsFrom"": """ + DownloadInfo.Inherit + @""",
    ""time"": """ + (string.IsNullOrEmpty(DownloadInfo.ReleaseTime) ? InheritVersion.ReleaseTime.ToString("yyyy'-'MM'-'dd") : DownloadInfo.ReleaseTime.Replace("/", "-")) + @"T23:33:33+08:00"",
    ""releaseTime"": """ + (string.IsNullOrEmpty(DownloadInfo.ReleaseTime) ? InheritVersion.ReleaseTime.ToString("yyyy'-'MM'-'dd") : DownloadInfo.ReleaseTime.Replace("/", "-")) + @"T23:33:33+08:00"",
    ""type"": ""release"",
    ""libraries"": [
        {""name"": ""optifine:OptiFine:" + DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "") +                                                                 // 输出旧版 Json 格式
@"""},
        {""name"": ""net.minecraft:launchwrapper:1.12""}
    ],
    ""mainClass"": ""net.minecraft.launchwrapper.Launch"","; Task.Progress = 0.8d; if (InheritVersion.IsOldJson)
                        {
                            Json += @"
    ""minimumLauncherVersion"": 18,
    ""minecraftArguments"": """ + InheritVersion.JsonObject["minecraftArguments"].ToString() +                                             // 输出新版 Json 格式
@"  --tweakClass optifine.OptiFineTweaker""
}";
                        }
                        else { Json += @"
    ""minimumLauncherVersion"": ""21"",
    ""arguments"": {
        ""game"": [
            ""--tweakClass"",
            ""optifine.OptiFineTweaker""
        ]
    }
}"; }
                        ModBase.WriteFile(VersionFolder + Id + ".json", Json);
                    }
                    catch (Exception ex) { throw new Exception("安装 OptiFine（方式 B）失败", ex); }
                })
                { ProgressWeight = 1d });
            }

            // 下载支持库
            if (FixLibrary)
            {
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析 OptiFine 支持库文件", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionFolder))) { ProgressWeight = 1d, Show = false });
                Loaders.Add(new ModNet.LoaderDownload("下载 OptiFine 支持库文件", new List<ModNet.NetFile>()) { ProgressWeight = 4d });
            }

            return Loaders;
        }
        /// <summary>
    /// 获取保存某个 OptiFine 版本的加载器列表。
    /// </summary>
        private static List<ModLoader.LoaderBase> McDownloadOptiFineSaveLoader(ModDownload.DlOptiFineListEntry DownloadInfo, string TargetFolder)
        {
            var Loaders = new List<ModLoader.LoaderBase>();
            // 获取下载地址
            Loaders.Add(new ModLoader.LoaderTask<ModDownload.DlOptiFineListEntry, List<ModNet.NetFile>>("获取 OptiFine 下载地址", (Task) =>
        {
            var Sources = new List<string>();
            // BMCLAPI 源
            string BmclapiInherit = DownloadInfo.Inherit;
            if (BmclapiInherit == "1.8" || BmclapiInherit == "1.9")
                BmclapiInherit += ".0"; // #4281
            if (DownloadInfo.IsPreview)
            {
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U_" + DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit + " ", "").Replace(" ", "/"));
            }
            else
            {
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U/" + DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit + " ", ""));
            }
            // 官方源
            string PageData;
            try
            {
                PageData = ModNet.NetGetCodeByClient("https://optifine.net/adloadx?f=" + DownloadInfo.NameFile, new UTF8Encoding(false), 15000, "text/html", true);
                Task.Progress = 0.8d;
                Sources.Add("https://optifine.net/" + PageData.RegexSearch(@"downloadx\?f=[^""']+")[0]);
                ModBase.Log("[Download] OptiFine " + DownloadInfo.NameDisplay + " 官方下载地址：" + Sources.Last());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 OptiFine " + DownloadInfo.NameDisplay + " 官方下载地址失败");
            }
            Task.Progress = 0.9d;
            // 构造文件请求
            Task.Output = new List<ModNet.NetFile>() { new ModNet.NetFile(Sources.ToArray(), TargetFolder, new ModBase.FileChecker(MinSize: 64 * 1024)) };
        })
            {
                ProgressWeight = 6d
            });
            // 下载
            Loaders.Add(new ModNet.LoaderDownload("下载 OptiFine 主文件", new List<ModNet.NetFile>()) { ProgressWeight = 10d, Block = true });
            return Loaders;
        }

        #endregion

        #region OptiFine 下载菜单

        public static MyListItem OptiFineDownloadListItem(ModDownload.DlOptiFineListEntry Entry, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry.NameDisplay,
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = (Entry.IsPreview ? "测试版" : "正式版") + (string.IsNullOrEmpty(Entry.ReleaseTime) ? "" : "，发布于 " + Entry.ReleaseTime) + (Entry.RequiredForgeVersion is null ? "，不兼容 Forge" : string.IsNullOrEmpty(Entry.RequiredForgeVersion) ? "" : "，兼容 Forge " + Entry.RequiredForgeVersion),
                Logo = ModBase.PathImage + "Blocks/GrassPath.png"
            };
            NewItem.Click += OnClick;
            // 建立菜单
            if (IsSaveOnly)
            {
                NewItem.ContentHandler = OptiFineSaveContMenuBuild;
            }
            else
            {
                NewItem.ContentHandler = OptiFineContMenuBuild;
            }
            // 结束
            return NewItem;
        }
        private static void OptiFineSaveContMenuBuild(object sender, EventArgs e)
        {
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += ModDownloadLib.OptiFineLog_Click;
            ((dynamic)sender).Buttons = new[] { BtnInfo };
        }
        private static void OptiFineContMenuBuild(object sender, EventArgs e)
        {
            var BtnSave = new MyIconButton() { Logo = ModBase.Logo.IconButtonSave, ToolTip = "另存为" };
            ToolTipService.SetPlacement(BtnSave, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnSave, 30d);
            ToolTipService.SetHorizontalOffset(BtnSave, 2d);
            BtnSave.Click += ModDownloadLib.OptiFineSave_Click;
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += ModDownloadLib.OptiFineLog_Click;
            ((dynamic)sender).Buttons = new[] { BtnSave, BtnInfo };
        }
        private static void OptiFineLog_Click(object sender, EventArgs e)
        {
            ModDownload.DlOptiFineListEntry Version;
            if (((dynamic)sender).Tag is not null)
            {
                Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            ModBase.OpenWebsite("https://optifine.net/changelog?f=" + Version.NameFile);
        }
        public static void OptiFineSave_Click(object sender, EventArgs e)
        {
            ModDownload.DlOptiFineListEntry Version;
            if (((dynamic)sender).Tag is not null)
            {
                Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            McDownloadOptiFineSave(Version);
        }

        #endregion

        #region LiteLoader 下载

        public static void McDownloadLiteLoader(ModDownload.DlLiteLoaderListEntry DownloadInfo)
        {
            try
            {
                string Id = DownloadInfo.Inherit;
                string Target = ModBase.PathTemp + @"Download\" + Id + "-Liteloader.jar";
                string VersionName = DownloadInfo.Inherit + "-LiteLoader";
                string VersionFolder = ModMinecraft.PathMcFolder + @"versions\" + VersionName + @"\";

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"LiteLoader {Id} 下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                // 已有版本检查
                if (File.Exists(VersionFolder + VersionName + ".json"))
                {
                    if (ModMain.MyMsgBox("版本 " + VersionName + " 已存在，是否重新下载？" + Constants.vbCrLf + "这会覆盖版本的 Json 和 Jar 文件，但不会影响版本隔离的文件。", "版本已存在", "继续", "取消") == 1)
                    {
                        File.Delete(VersionFolder + VersionName + ".jar");
                        File.Delete(VersionFolder + VersionName + ".json");
                    }
                    else
                    {
                        return;
                    }
                }

                // 启动
                var Loader = new ModLoader.LoaderCombo<string>("LiteLoader " + Id + " 下载", McDownloadLiteLoaderLoader(DownloadInfo)) { OnStateChanged = McInstallState };
                Loader.Start(VersionFolder);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 LiteLoader 下载失败", ModBase.LogLevel.Feedback);
            }
        }
        private static void McDownloadLiteLoaderSave(ModDownload.DlLiteLoaderListEntry DownloadInfo)
        {
            try
            {
                string Id = DownloadInfo.Inherit;
                string Target = ModBase.SelectSaveFile("选择保存位置", DownloadInfo.FileName.Replace("-SNAPSHOT", ""), "LiteLoader 安装器 (*.jar)|*.jar");
                if (!Target.Contains(@"\"))
                    return;

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"LiteLoader {Id} 下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                // 构造步骤加载器
                var Loaders = new List<ModLoader.LoaderBase>();
                // 下载
                var Address = new List<string>();
                if (DownloadInfo.IsLegacy)
                {
                    // 老版本
                    switch (DownloadInfo.Inherit ?? "")
                    {
                        case "1.7.10":
                            {
                                Address.Add("https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar");
                                break;
                            }
                        case "1.7.2":
                            {
                                Address.Add("https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar");
                                break;
                            }
                        case "1.6.4":
                            {
                                Address.Add("https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar");
                                break;
                            }
                        case "1.6.2":
                            {
                                Address.Add("https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar");
                                break;
                            }
                        case "1.5.2":
                            {
                                Address.Add("https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar");
                                break;
                            }

                        default:
                            {
                                throw new NotSupportedException("未知的 Minecraft 版本（" + DownloadInfo.Inherit + "）");
                            }
                    }
                }
                else
                {
                    // 官方源
                    Address.Add("http://jenkins.liteloader.com/job/LiteLoaderInstaller%20" + DownloadInfo.Inherit + "/lastSuccessfulBuild/artifact/" + (DownloadInfo.Inherit == "1.8" ? "ant/dist/" : "build/libs/") + DownloadInfo.FileName);
                }
                Loaders.Add(new ModNet.LoaderDownload("下载主文件", new List<ModNet.NetFile>() { new ModNet.NetFile(Address.ToArray(), Target, new ModBase.FileChecker(MinSize: 1024 * 1024)) }) { ProgressWeight = 15d });
                // 启动
                var Loader = new ModLoader.LoaderCombo<ModDownload.DlLiteLoaderListEntry>("LiteLoader " + Id + " 安装器下载", Loaders) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(DownloadInfo);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 LiteLoader 安装器下载失败", ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
    /// 获取下载某个 LiteLoader 版本的加载器列表。
    /// </summary>
        private static List<ModLoader.LoaderBase> McDownloadLiteLoaderLoader(ModDownload.DlLiteLoaderListEntry DownloadInfo, string McFolder = null, ModLoader.LoaderCombo<string> ClientDownloadLoader = null, bool FixLibrary = true)
        {

            // 参数初始化
            McFolder = McFolder ?? ModMinecraft.PathMcFolder;
            bool IsCustomFolder = (McFolder ?? "") != (ModMinecraft.PathMcFolder ?? "");
            string Id = DownloadInfo.Inherit;
            string Target = ModBase.PathTemp + @"Download\" + Id + "-Liteloader.jar";
            string VersionName = DownloadInfo.Inherit + "-LiteLoader";
            string VersionFolder = McFolder + @"versions\" + VersionName + @"\";
            var Loaders = new List<ModLoader.LoaderBase>();

            // 启动依赖版本的下载
            if (ClientDownloadLoader is null)
            {
                Loaders.Add(new ModLoader.LoaderTask<string, string>("启动 LiteLoader 依赖版本下载", () =>
        {
            if (IsCustomFolder)
                throw new Exception("如果没有指定原版下载器，则不能指定 MC 安装文件夹");
            ClientDownloadLoader = McDownloadClient(ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, DownloadInfo.Inherit);
        })
                {
                    ProgressWeight = 0.2d,
                    Show = false,
                    Block = false
                });
            }
            // 安装
            // 新建版本文件夹
            // 构造版本 Json
            // 输出 Json 文件
            Loaders.Add(new ModLoader.LoaderTask<string, string>("安装 LiteLoader", (Task) => { try { Directory.CreateDirectory(VersionFolder); var VersionJson = new JObject(); VersionJson.Add("id", VersionName); VersionJson.Add("time", DateTime.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.CurrentCulture)); VersionJson.Add("releaseTime", DateTime.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.CurrentCulture)); VersionJson.Add("type", "release"); VersionJson.Add("arguments", (JToken)ModBase.GetJson("{\"game\":[\"--tweakClass\",\"" + DownloadInfo.JsonToken["tweakClass"].ToString() + "\"]}")); VersionJson.Add("libraries", DownloadInfo.JsonToken["libraries"]); ((JContainer)VersionJson["libraries"]).Add(ModBase.GetJson("{\"name\": \"com.mumfrey:liteloader:" + DownloadInfo.JsonToken["version"].ToString() + "\",\"url\": \"https://dl.liteloader.com/versions/\"}")); VersionJson.Add("mainClass", "net.minecraft.launchwrapper.Launch"); VersionJson.Add("minimumLauncherVersion", 18); VersionJson.Add("inheritsFrom", DownloadInfo.Inherit); VersionJson.Add("jar", DownloadInfo.Inherit); ModBase.WriteFile(VersionFolder + VersionName + ".json", VersionJson.ToString()); } catch (Exception ex) { throw new Exception("安装新 LiteLoader 版本失败", ex); } }) { ProgressWeight = 1d });
            // 下载支持库
            if (FixLibrary)
            {
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析 LiteLoader 支持库文件", Task => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionFolder))) { ProgressWeight = 1d, Show = false });
                Loaders.Add(new ModNet.LoaderDownload("下载 LiteLoader 支持库文件", new List<ModNet.NetFile>()) { ProgressWeight = 6d });
            }

            return Loaders;
        }

        #endregion

        #region LiteLoader 下载菜单

        public static MyListItem LiteLoaderDownloadListItem(ModDownload.DlLiteLoaderListEntry Entry, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry.Inherit,
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = (Entry.IsPreview ? "测试版" : "稳定版") + (string.IsNullOrEmpty(Entry.ReleaseTime) ? "" : "，发布于 " + Entry.ReleaseTime),
                Logo = ModBase.PathImage + "Blocks/Egg.png"
            };
            NewItem.Click += OnClick;
            // 建立菜单
            if (IsSaveOnly)
            {
                NewItem.ContentHandler = LiteLoaderSaveContMenuBuild;
            }
            else
            {
                NewItem.ContentHandler = LiteLoaderContMenuBuild;
            }
            // 结束
            return NewItem;
        }
        private static void LiteLoaderSaveContMenuBuild(MyListItem sender, EventArgs e)
        {
            if (Conversions.ToBoolean(((dynamic)sender.Tag).IsLegacy))
            {
                sender.Buttons = Array.Empty<MyIconButton>();
            }
            else
            {
                var BtnList = new MyIconButton() { Logo = ModBase.Logo.IconButtonList, ToolTip = "查看全部版本", Tag = sender };
                ToolTipService.SetPlacement(BtnList, System.Windows.Controls.Primitives.PlacementMode.Center);
                ToolTipService.SetVerticalOffset(BtnList, 30d);
                ToolTipService.SetHorizontalOffset(BtnList, 2d);
                BtnList.Click += (_, __) => ModDownloadLib.LiteLoaderAll_Click();
                sender.Buttons = new[] { BtnList };
            }
        }
        private static void LiteLoaderContMenuBuild(MyListItem sender, EventArgs e)
        {
            var BtnSave = new MyIconButton() { Logo = ModBase.Logo.IconButtonSave, ToolTip = "保存安装器", Tag = sender };
            ToolTipService.SetPlacement(BtnSave, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnSave, 30d);
            ToolTipService.SetHorizontalOffset(BtnSave, 2d);
            BtnSave.Click += (_, __) => ModDownloadLib.LiteLoaderSave_Click();
            if (Conversions.ToBoolean(((dynamic)sender.Tag).IsLegacy))
            {
                sender.Buttons = new[] { BtnSave };
            }
            else
            {
                var BtnList = new MyIconButton() { Logo = ModBase.Logo.IconButtonList, ToolTip = "查看全部版本", Tag = sender };
                ToolTipService.SetPlacement(BtnList, System.Windows.Controls.Primitives.PlacementMode.Center);
                ToolTipService.SetVerticalOffset(BtnList, 30d);
                ToolTipService.SetHorizontalOffset(BtnList, 2d);
                BtnList.Click += (_, __) => ModDownloadLib.LiteLoaderAll_Click();
                sender.Buttons = new[] { BtnSave, BtnList };
            }
        }
        private static void LiteLoaderAll_Click(object sender, RoutedEventArgs e)
        {
            ModDownload.DlLiteLoaderListEntry Version;
            if (((dynamic)sender).Tag is ModDownload.DlLiteLoaderListEntry)
            {
                Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
            }
            else
            {
                Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
            }
            ModBase.OpenWebsite("https://jenkins.liteloader.com/view/" + Version.Inherit);
        }
        public static void LiteLoaderSave_Click(object sender, RoutedEventArgs e)
        {
            // ListItem 与小按钮都会调用这个方法
            ModDownload.DlLiteLoaderListEntry Version;
            if (((dynamic)sender).Tag is ModDownload.DlLiteLoaderListEntry)
            {
                Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
            }
            else
            {
                Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
            }
            McDownloadLiteLoaderSave(Version);
        }

        #endregion

        #region Forgelike 下载

        public static void McDownloadForgelikeSave(ModDownload.DlForgelikeEntry Info)
        {
            try
            {
                string Target = ModBase.SelectSaveFile("选择保存位置", $"{Info.LoaderName}-{Info.Inherit}-{Info.VersionName}.{Info.FileExtension}", $"{Info.LoaderName} 安装器 (*.{Info.FileExtension})|*.{Info.FileExtension}");
                string DisplayName = $"{Info.LoaderName} {Info.Inherit} - {Info.VersionName}";
                if (!Target.Contains(@"\"))
                    return;

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"{DisplayName} 下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                // 获取下载地址
                var Files = new List<ModNet.NetFile>();
                if ((int)Info.ForgeType == 1)
                {
                    // NeoForge
                    ModDownload.DlNeoForgeListEntry Neo = (ModDownload.DlNeoForgeListEntry)Info;
                    string Url = Neo.UrlBase + "-installer.jar";
                    Files.Add(new ModNet.NetFile(new[] { Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url }, Target, new ModBase.FileChecker(MinSize: 64 * 1024)));
                }
                else
                {
                    // Forge
                    ModDownload.DlForgeVersionEntry Forge = (ModDownload.DlForgeVersionEntry)Info;
                    Files.Add(new ModNet.NetFile(new[] { $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}", $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}" }, Target, new ModBase.FileChecker(MinSize: 64 * 1024, Hash: Forge.Hash)));
                }

                // 构造加载器
                var Loaders = new List<ModLoader.LoaderBase>();
                Loaders.Add(new ModNet.LoaderDownload("下载主文件", Files) { ProgressWeight = 6d });

                // 启动
                var Loader = new ModLoader.LoaderCombo<ModDownload.DlForgelikeEntry>(DisplayName + " 下载", Loaders) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(Info);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, $"开始 {Info.LoaderName} 安装器下载失败", ModBase.LogLevel.Feedback);
            }
        }

        private static void ForgelikeInjector(string Target, ModLoader.LoaderTask<bool, bool> Task, string McFolder, bool UseJavaWrapper, string ForgeType)
        {
            // 选择 Java
            ModJava.JavaEntry Java;
            lock (ModJava.JavaLock)
            {
                Java = ModJava.JavaSelect("已取消安装。", new Version(1, 8, 0, 60));
                if (Java is null)
                {
                    if (!ModJava.JavaDownloadConfirm("Java 8 或更高版本"))
                        throw new Exception("由于未找到 Java，已取消安装。");
                    // 开始自动下载
                    var JavaLoader = ModJava.JavaFixLoaders(17);
                    try
                    {
                        JavaLoader.Start(17, IsForceRestart: true);
                        while (JavaLoader.State == ModBase.LoadState.Loading && !Task.IsAborted)
                            Thread.Sleep(10);
                    }
                    finally
                    {
                        JavaLoader.Abort(); // 确保取消时中止 Java 下载
                    }
                    // 检查下载结果
                    Java = ModJava.JavaSelect("已取消安装。", new Version(1, 8, 0, 60));
                    if (Task.IsAborted)
                        return;
                    if (Java is null)
                        throw new Exception("由于未找到 Java，已取消安装。");
                }
            }
            // 添加 Java Wrapper 作为主 Jar
            string Arguments;
            if (UseJavaWrapper && !(bool)ModBase.Setup.Get("LaunchAdvanceDisableJLW"))
            {
                Arguments = $@"-Doolloo.jlw.tmpdir=""{ModBase.PathPure.TrimEnd('\\')}"" -cp ""{ModBase.PathTemp}Cache\forge_installer.jar;{Target}"" -jar ""{ModLaunch.ExtractJavaWrapper()}"" com.bangbang93.ForgeInstaller ""{McFolder}";
            }
            else
            {
                Arguments = $@"-cp ""{ModBase.PathTemp}Cache\forge_installer.jar;{Target}"" com.bangbang93.ForgeInstaller ""{McFolder}";
            }
            if (Java.VersionCode >= 9)
                Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + Arguments;
            // 开始启动
            lock (InstallSyncLock)
            {
                var Info = new ProcessStartInfo()
                {
                    FileName = Java.PathJavaw,
                    Arguments = Arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                string LoaderName = ForgeType;
                ModBase.Log($"[Download] 开始安装 {LoaderName}：" + Arguments);
                var process = new Process() { StartInfo = Info };
                var LastResults = new Queue<string>();
                using (var outputWaitHandle = new AutoResetEvent(false))
                {
                    using (var errorWaitHandle = new AutoResetEvent(false))
                    {
                        process.OutputDataReceived += (sender, e) =>
        {
            try
            {
                if (e.Data is null)
                {
                    outputWaitHandle.Set();
                }
                else
                {
                    LastResults.Enqueue(e.Data);
                    if (LastResults.Count > 100)
                        LastResults.Dequeue();
                    ForgelikeInjectorLine(e.Data, Task);
                }
            }
            catch (ObjectDisposedException ex)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"读取 {LoaderName} 安装器信息失败");
            }
            try
            {
                if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                {
                    ModBase.Log($"[Installer] 由于任务取消，已中止 {LoaderName} 安装");
                    process.Kill();
                }
            }
            catch
            {
            }
        };
                        process.ErrorDataReceived += (sender, e) =>
        {
            try
            {
                if (e.Data is null)
                {
                    errorWaitHandle.Set();
                }
                else
                {
                    LastResults.Enqueue(e.Data);
                    if (LastResults.Count > 100)
                        LastResults.Dequeue();
                    ForgelikeInjectorLine(e.Data, Task);
                }
            }
            catch (ObjectDisposedException ex)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"读取 {LoaderName} 安装器错误信息失败");
            }
            try
            {
                if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                {
                    ModBase.Log($"[Installer] 由于任务取消，已中止 {LoaderName} 安装");
                    process.Kill();
                }
            }
            catch
            {
            }
        };
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        // 等待
                        while (!process.HasExited)
                            Thread.Sleep(10);
                        // 输出
                        outputWaitHandle.WaitOne(10000);
                        errorWaitHandle.WaitOne(10000);
                        process.Dispose();
                        // 检查是否安装成功：最后两行中是否有 true（true 可能在倒数第二行，见 #832）
                        if (LastResults.Last() == "true" || LastResults.Count >= 2 && LastResults.ElementAtOrDefault(LastResults.Count - 2) == "true")
                            return;
                        ModBase.Log(LastResults.Join(Constants.vbCrLf));
                        string LastLines = "";
                        for (int i = Math.Max(0, LastResults.Count - 5), loopTo = LastResults.Count - 1; i <= loopTo; i++) // 最后 5 行
                            LastLines += Constants.vbCrLf + LastResults.ElementAtOrDefault(i);
                        throw new Exception($"{LoaderName} 安装器出错，日志结束部分为：" + LastLines);
                    }
                }
            }
        }
        private static void ForgelikeInjectorLine(string Content, ModLoader.LoaderTask<bool, bool> Task)
        {
            switch (Content ?? "")
            {
                case "Extracting json":
                    {
                        ModBase.Log("[Installer] " + Content);
                        Task.Progress = 0.07d;
                        break;
                    }
                case "Downloading libraries":
                    {
                        ModBase.Log("[Installer] " + Content);
                        Task.Progress = 0.08d;
                        break;
                    }
                case "  File exists: Checksum validated.":
                    {
                        if (ModBase.ModeDebug)
                            ModBase.Log("[Installer] " + Content);
                        Task.Progress += 0.003d;
                        break;
                    }
                case "Building Processors":
                    {
                        Task.Progress = 0.18d;
                        break;
                    }
                case "Task: DOWNLOAD_MOJMAPS": // B
                    {
                        Task.Progress = 0.2d;
                        break;
                    }
                case "Task: MERGE_MAPPING": // B
                    {
                        Task.Progress = 0.3d;
                        break;
                    }
                case "Splitting: ":
                    {
                        Task.Progress = 0.35d;
                        break;
                    }
                case "Parameter Annotations": // B
                    {
                        Task.Progress = 0.4d;
                        break;
                    }
                case "Processing Complete": // B
                    {
                        Task.Progress = 0.5d;
                        break;
                    }
                case "log: null": // new
                    {
                        Task.Progress = 0.5d;
                        break;
                    }
                case "Sorting": // new
                    {
                        Task.Progress = 0.65d;
                        break;
                    }
                case "Remapping final jar": // A
                    {
                        Task.Progress = 0.72d;
                        break;
                    }
                case "Remapping jar... 50%": // A
                    {
                        Task.Progress = 0.76d;
                        break;
                    }
                case "Remapping jar... 100%": // A
                    {
                        Task.Progress = 0.81d;
                        break;
                    }
                case "Injecting profile":
                    {
                        Task.Progress = 0.91d;
                        break;
                    }

                default:
                    {
                        if (ModBase.ModeDebug)
                            ModBase.Log("[Installer] " + Content);
                        return;
                    }
            }
            ModBase.Log("[Installer] " + Content);
        }

        /// <summary>
    /// 获取下载某个 Forgelike 版本的加载器列表。
    /// </summary>
        private static List<ModLoader.LoaderBase> McDownloadForgelikeLoader(string ForgeType, string LoaderVersion, string TargetVersion, string Inherit, ModDownload.DlForgelikeEntry Info = null, string McFolder = null, ModLoader.LoaderCombo<string> ClientDownloadLoader = null, string ClientFolder = null)
        {

            // 参数初始化
            McFolder = McFolder ?? ModMinecraft.PathMcFolder;
            if (ForgeType == "NeoForge" && Info is null)
            {
                // 需要传入 API Name，但整合包版本可能不以 1.20.1- 开头，所以需要进行特别处理
                if (Inherit == "1.20.1" && !LoaderVersion.StartsWithF("1.20.1-"))
                {
                    Info = new ModDownload.DlNeoForgeListEntry("1.20.1-" + LoaderVersion);
                }
                else
                {
                    Info = new ModDownload.DlNeoForgeListEntry(LoaderVersion);
                }
            }
            if (ForgeType == "Cleanroom" && Info is null)
            {
                Info = new ModDownload.DlCleanroomListEntry(LoaderVersion);
            }
            if (!(ForgeType == "NeoForge") && LoaderVersion.StartsWithF("1.") && LoaderVersion.Contains("-"))
            {
                // 类似 1.19.3-41.2.8 格式，优先使用 Version 中要求的版本而非 Inherit（例如 1.19.3 却使用了 1.19 的 Forge）
                Inherit = LoaderVersion.BeforeFirst("-");
                LoaderVersion = LoaderVersion.AfterLast("-");
            }
            string LoaderName = ForgeType;
            bool IsCustomFolder = (McFolder ?? "") != (ModMinecraft.PathMcFolder ?? "");
            string InstallerAddress = ModMain.RequestTaskTempFolder() + "forge_installer.jar";
            string VersionFolder = $@"{McFolder}versions\{TargetVersion}\";
            string DisplayName = $"{LoaderName} {Inherit} - {LoaderVersion}";
            var Loaders = new List<ModLoader.LoaderBase>();
            string LibVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{TargetVersion}\"; // 作为 Lib 文件目标的版本文件夹

            // 获取 Forge 下载信息
            if (Info is null)
            {
                Loaders.Add(new ModLoader.LoaderTask<string, string>($"获取 {LoaderName} 详细信息", (Task) =>
        {
            // 获取 Forge 对应 MC 版本列表
            var ForgeLoader = new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>("McDownloadForgeLoader " + Inherit, ModDownload.DlForgeVersionMain);
            ForgeLoader.WaitForExit(Inherit);
            Task.Progress = 0.8d;
            // 查找对应版本
            foreach (var ForgeVersion in ForgeLoader.Output)
            {
                if (ModMinecraft.VersionSortInteger(ForgeVersion.Version.ToString(), LoaderVersion) == 0)
                {
                    Info = ForgeVersion;
                    return;
                }
            }
            throw new Exception($"未能找到 {LoaderName} " + Inherit + "-" + LoaderVersion + " 的详细信息！");
        })
                {
                    ProgressWeight = 3d
                });
            }
            // 下载 Forgelike 主文件
            Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>($"准备下载 {LoaderName}", (Task) =>
        {
            // 启动依赖版本的下载
            if (ClientDownloadLoader is null)
            {
                if (IsCustomFolder)
                    throw new Exception("如果没有指定原版下载器，则不能指定 MC 安装文件夹");
                ClientDownloadLoader = McDownloadClient(ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, Inherit);
            }
            // 添加主文件下载
            var Files = new List<ModNet.NetFile>();
            if (Info.ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge)
            {
                // NeoForge
                ModDownload.DlNeoForgeListEntry Neo = (ModDownload.DlNeoForgeListEntry)Info;
                string Url = Neo.UrlBase + "-installer.jar";
                Files.Add(new ModNet.NetFile(new[] { Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url }, InstallerAddress, new ModBase.FileChecker(MinSize: 64 * 1024)));
            }
            else if (Info.ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom)
            {
                // Cleanroom
                ModDownload.DlCleanroomListEntry Clr = (ModDownload.DlCleanroomListEntry)Info;
                string Url = Clr.UrlBase + "-installer.jar";
                Files.Add(new ModNet.NetFile(new[] { Url }, InstallerAddress, new ModBase.FileChecker(MinSize: 64 * 1024)));
            }
            else
            {
                // Forge
                ModDownload.DlForgeVersionEntry Forge = (ModDownload.DlForgeVersionEntry)Info;
                string FileName = $"{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}/forge-{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}";
                Files.Add(new ModNet.NetFile(new[] { $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{FileName}", $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{FileName}" }, InstallerAddress, new ModBase.FileChecker(MinSize: 64 * 1024, Hash: Forge.Hash)));
            }
            Task.Output = Files;
        })
            {
                ProgressWeight = 0.5d,
                Show = false
            });
            Loaders.Add(new ModNet.LoaderDownload($"下载 {LoaderName} 主文件", new List<ModNet.NetFile>()) { ProgressWeight = 9d });

            // 安装（仅在新版安装时需要原版 Jar）
            if (ForgeType == "NeoForge" || Conversions.ToDouble(LoaderVersion.BeforeFirst(".")) >= 20d)
            {
                ModBase.Log($"[Download] 检测为{(ForgeType == "Forge" ? "新版 Forge" : " " + ForgeType)}：" + LoaderVersion);
                List<ModMinecraft.McLibToken> Libs = null;
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>($"分析 {LoaderName} 支持库文件", (Task) =>
        {
            Task.Output = new List<ModNet.NetFile>();
            ZipArchive Installer = null;
            try
            {
                // 解压并获取、合并两个 Json 的信息
                Installer = new ZipArchive(new FileStream(InstallerAddress, FileMode.Open));
                Task.Progress = 0.2d;
                JObject Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Installer.GetEntry("install_profile.json").Open()));
                JObject Json2 = (JObject)ModBase.GetJson(ModBase.ReadFile(Installer.GetEntry("version.json").Open()));
                Json.Merge(Json2);
                // 获取 Lib 下载信息
                Libs = ModMinecraft.McLibListGetWithJson(Json, true);
                // 添加 Mappings 下载信息
                if (Json["data"] is not null && Json["data"]["MOJMAPS"] is not null)
                {
                    // 下载原版 Json 文件
                    Task.Progress = 0.4d;
                    JObject RawJson = (JObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(ModDownload.DlSourceLauncherOrMetaGet(Conversions.ToString(ModDownload.DlClientListGet(Inherit))), IsJson: true));
                    // [net.minecraft:client:1.17.1-20210706.113038:mappings@txt] 或 @tsrg]
                    string OriginalName = Json["data"]["MOJMAPS"]["client"].ToString().Trim("[]".ToCharArray()).BeforeFirst("@");
                    string Address = ModMinecraft.McLibGet(OriginalName).Replace(".jar", "-mappings." + Json["data"]["MOJMAPS"]["client"].ToString().Trim("[]".ToCharArray()).Split("@")[1]);
                    var ClientMappings = RawJson["downloads"]["client_mappings"];
                    Libs.Add(new ModMinecraft.McLibToken()
                    {
                        IsJumpLoader = false,
                        IsNatives = false,
                        LocalPath = Address,
                        OriginalName = OriginalName,
                        Url = (string)ClientMappings["url"],
                        Size = (long)ClientMappings["size"],
                        SHA1 = (string)ClientMappings["sha1"]
                    });
                    ModBase.Log($"[Download] 需要下载 Mappings：{ClientMappings["url"]} (SHA1: {ClientMappings["sha1"]})");
                }
                Task.Progress = 0.8d;
                // 去除其中的原始 Forgelike 项
                for (int i = 0, loopTo = Libs.Count - 1; i <= loopTo; i++)
                {
                    if (Libs[i].LocalPath.EndsWithF($"{LoaderName.ToLower()}-{Inherit}-{LoaderVersion}.jar") || Libs[i].LocalPath.EndsWithF($"{LoaderName.ToLower()}-{Inherit}-{LoaderVersion}-client.jar"))
                    {
                        ModBase.Log($"[Download] 已从待下载 {LoaderName} 支持库中移除：" + Libs[i].LocalPath, ModBase.LogLevel.Debug);
                        Libs.RemoveAt(i);
                        break;
                    }
                }
                Task.Output = ModMinecraft.McLibFixFromLibToken(Libs, ModMinecraft.PathMcFolder);
            }
            catch (Exception ex)
            {
                throw new Exception($"获取{(ForgeType == "Forge" ? "新版 Forge" : " " + ForgeType)} 支持库列表失败", ex);
            }
            finally
            {
                // 释放文件
                if (Installer is not null)
                    Installer.Dispose();
            }
        })
                {
                    ProgressWeight = 2d
                });
                Loaders.Add(new ModNet.LoaderDownload($"下载 {LoaderName} 支持库文件", new List<ModNet.NetFile>()) { ProgressWeight = 12d });
                Loaders.Add(new ModLoader.LoaderTask<List<ModNet.NetFile>, bool>($"获取 {LoaderName} 支持库文件", (Task) =>
        {
            #region Forgelike 文件
            if (IsCustomFolder)
            {
                foreach (ModMinecraft.McLibToken LibFile in Libs)
                {
                    string RealPath = LibFile.LocalPath.Replace(ModMinecraft.PathMcFolder, McFolder);
                    if (!File.Exists(RealPath))
                    {
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(RealPath));
                        ModBase.CopyFile(LibFile.LocalPath, RealPath);
                    }
                    if (ModBase.ModeDebug)
                        ModBase.Log($"[Download] 复制的 {LoaderName} 支持库文件：" + LibFile.LocalPath);
                }
            }
            #endregion
            #region 原版文件
            // 等待原版文件下载完成
            if (ClientDownloadLoader is null)
                return;
            var TargetLoaders = ClientDownloadLoader.GetLoaderList().Where(l => (l.Name ?? "") == McDownloadClientLibName || (l.Name ?? "") == McDownloadClientJsonName).Where(l => l.State != ModBase.LoadState.Finished).ToList();
            if (TargetLoaders.Any())
                ModBase.Log($"[Download] {LoaderName} 安装正在等待原版文件下载完成");
            while (TargetLoaders.Any() && !Task.IsAborted)
            {
                TargetLoaders = TargetLoaders.Where(l => l.State != ModBase.LoadState.Finished).ToList();
                Thread.Sleep(50);
            }
            if (Task.IsAborted)
                return;
            // 拷贝原版文件
            if (!IsCustomFolder)
                return;
            lock (VanillaSyncLock)
            {
                string ClientName = ModBase.GetFolderNameFromPath(ClientFolder);
                Directory.CreateDirectory(McFolder + @"versions\" + Inherit);
                if (!File.Exists(McFolder + @"versions\" + Inherit + @"\" + Inherit + ".json"))
                {
                    ModBase.CopyFile(ClientFolder + ClientName + ".json", McFolder + @"versions\" + Inherit + @"\" + Inherit + ".json");
                }
                if (!File.Exists(McFolder + @"versions\" + Inherit + @"\" + Inherit + ".jar"))
                {
                    ModBase.CopyFile(ClientFolder + ClientName + ".jar", McFolder + @"versions\" + Inherit + @"\" + Inherit + ".jar");
                }
            }
            #endregion
        })
                {
                    ProgressWeight = 0.1d,
                    Show = false
                });
                Loaders.Add(new ModLoader.LoaderTask<bool, bool>(ForgeType == "Forge" ? "安装 Forge（方式 A）" : "安装 " + ForgeType, (Task) =>
        {
            ;
            try
            {
                // 记录当前文件夹列表（在新建目标文件夹之前）
                ModBase.Log($"[Download] 开始进行 Forgelike 安装：" + InstallerAddress);
                // 解压并获取信息
                var OldList = new DirectoryInfo(McFolder + "versions\\").EnumerateDirectories().Select(i => i.FullName).ToList();
                var Installer = new ZipArchive(new FileStream(InstallerAddress, FileMode.Open));
                // 新建目标版本文件夹
                var Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Installer.GetEntry("install_profile.json").Open()));
                Directory.CreateDirectory(VersionFolder);
                Task.Progress = 0.04d;
                // 释放 launcher_installer.json
                ModMinecraft.McFolderLauncherProfilesJsonCreate(McFolder);
                Task.Progress = 0.05d;
                // 运行 Forge 安装器
                bool UseJavaWrapper = true;
            Retry:
                ;

                try
                {
                    // 释放 Forge 注入器
                    ModBase.WriteFile(ModBase.PathTemp + @"Cache\forge_installer.jar", ModBase.GetResources("ForgeInstaller"));
                    Task.Progress = 0.06d;
                    // 运行注入器
                    ForgelikeInjector(InstallerAddress, Task, McFolder, UseJavaWrapper, ForgeType);
                    Task.Progress = 0.97d;
                }
                catch (Exception ex)
                {
                    if (UseJavaWrapper)
                    {
                        ModBase.Log(ex, $"使用 JavaWrapper 安装 {LoaderName} 失败，将不使用 JavaWrapper 并重试");
                        UseJavaWrapper = false;
                        goto Retry;
                    }
                    else
                    {
                        throw new Exception($"运行 {LoaderName} 安装器失败", ex);
                    }
                    // 拷贝新增的版本 Json
                }
                var DeltaList = new DirectoryInfo(McFolder + "versions\\").EnumerateDirectories().SkipWhile(i => OldList.Contains(i.FullName)).ToList();
                if (DeltaList.Count > 1)
                {
                    // 它可能和 OptiFine 安装同时运行，导致增加的文件不止一个（这导致了 #151）
                    // 也可能是因为 Forge 安装器的 Bug，生成了一个名字错误的文件夹，所以需要检查文件夹是否为空
                    DeltaList = DeltaList.Where(l => l.Name.ContainsF("forge", true) && l.EnumerateFiles().Any()).ToList();
                }
                // 如果没有新增文件夹，那么预测的文件夹名就是正确的
                // 如果只新增 1 个文件夹，那么拷贝 Json 文件
                if (DeltaList.Count == 1)
                {
                    var JsonFile = DeltaList[0].EnumerateFiles().First();
                    ModBase.WriteFile(VersionFolder + TargetVersion + ".json", ModBase.ReadFile(JsonFile.FullName));
                    ModBase.Log($"[Download] 已拷贝新增的版本 Json 文件：{JsonFile.FullName} -> {VersionFolder}{TargetVersion}.json");
                }
                else if (DeltaList.Count > 1)
                {
                    // 新增了多个文件夹
                    ModBase.Log($"[Download] 有多个疑似的新增版本，无法确定：{DeltaList.Select(d => d.Name).Join(";")}");
                }
                else
                {
                    // 没有新增文件夹
                    ModBase.Log("[Download] 未找到新增的版本文件夹");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"安装新 {LoaderName} 版本失败", ex);
            }
            finally
            {
                // 清理文件
                try
                {
                    if (Installer is not null)
                        Installer.Dispose();
                    if (File.Exists(InstallerAddress))
                        File.Delete(InstallerAddress);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"安装 {LoaderName} 清理文件时出错");
                }
            }
        })
                {
                    ProgressWeight = 10d
                });
            }
            else
            {
                ModBase.Log("[Download] 检测为非新版 Forge：" + LoaderVersion);
                Loaders.Add(new ModLoader.LoaderTask<List<ModNet.NetFile>, bool>($"安装 {(ForgeType == "Forge" ? "Forge（方式 B）" : ForgeType)}", (Task) =>
        {
            ZipArchive Installer = null;
            try
            {
                // 解压并获取信息
                Installer = new ZipArchive(new FileStream(InstallerAddress, FileMode.Open));
                Task.Progress = 0.2d;
                JObject Json = (JObject)ModBase.GetJson(ModBase.ReadFile(Installer.GetEntry("install_profile.json").Open()));
                Task.Progress = 0.4d;
                // 新建版本文件夹
                Directory.CreateDirectory(VersionFolder);
                Task.Progress = 0.5d;
                if (Json["install"] is null)
                {
                    // 中版：Legacy 方式 1
                    ModBase.Log("[Download] 开始进行 Forge 安装，Legacy 方式 1：" + InstallerAddress);
                    // 建立 Json 文件
                    JObject JsonVersion = (JObject)ModBase.GetJson(ModBase.ReadFile(Installer.GetEntry(Json["json"].ToString().TrimStart('/')).Open()));
                    JsonVersion["id"] = TargetVersion;
                    ModBase.WriteFile(VersionFolder + TargetVersion + ".json", JsonVersion.ToString());
                    Task.Progress = 0.6d;
                    // 解压支持库文件
                    Installer.Dispose();
                    ModBase.ExtractFile(InstallerAddress, InstallerAddress + @"_unrar\");
                    ModBase.CopyDirectory(InstallerAddress + @"_unrar\maven\", McFolder + @"libraries\");
                    ModBase.DeleteDirectory(InstallerAddress + @"_unrar\");
                }
                else
                {
                    // 旧版：Legacy 方式 2
                    ModBase.Log("[Download] 开始进行 Forge 安装，Legacy 方式 2：" + InstallerAddress);
                    // 解压 Jar 文件
                    string JarAddress = ModMinecraft.McLibGet((string)Json["install"]["path"], CustomMcFolder: McFolder);
                    if (File.Exists(JarAddress))
                        File.Delete(JarAddress);
                    ModBase.WriteFile(JarAddress, Installer.GetEntry((string)Json["install"]["filePath"]).Open());
                    Task.Progress = 0.9d;
                    // 建立 Json 文件
                    Json["versionInfo"]["id"] = TargetVersion;
                    if (Json["versionInfo"]["inheritsFrom"] is null)
                        ((JObject)Json["versionInfo"]).Add("inheritsFrom", Inherit);
                    ModBase.WriteFile(VersionFolder + TargetVersion + ".json", Json["versionInfo"].ToString());
                }
            }
            catch (Exception ex)
            {
                throw new Exception("非新版方式安装 Forge 失败", ex);
            }
            finally
            {
                try
                {
                    // 清理文件
                    if (Installer is not null)
                        Installer.Dispose();
                    if (File.Exists(InstallerAddress))
                        File.Delete(InstallerAddress);
                    if (Directory.Exists(InstallerAddress + @"_unrar\"))
                        ModBase.DeleteDirectory(InstallerAddress + @"_unrar\");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "非新版方式安装 Forge 清理文件时出错");
                }
            }
        })
                {
                    ProgressWeight = 1d
                });
            }

            return Loaders;
        }

        #endregion

        #region Forge 下载菜单

        public static void ForgeDownloadListItemPreload(StackPanel Stack, List<ModDownload.DlForgeVersionEntry> Entries, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 如果只有一个版本，则不特别列出
            if (Entries.Count == 1)
                return;
            // 获取推荐版本与最新版本
            ModDownload.DlForgeVersionEntry FreshVersion = null;
            if (Entries.Any())
            {
                FreshVersion = Entries[0];
            }
            else
            {
                ModBase.Log("[System] 未找到可用的 Forge 版本", ModBase.LogLevel.Debug);
            }
            ModDownload.DlForgeVersionEntry RecommendedVersion = null;
            foreach (var Entry in Entries)
            {
                if (Entry.IsRecommended)
                    RecommendedVersion = Entry;
            }
            // 若推荐版本与最新版本为同一版本，则仅显示推荐版本
            if (FreshVersion is not null && ReferenceEquals(FreshVersion, RecommendedVersion))
                FreshVersion = null;
            // 显示各个版本
            if (RecommendedVersion is not null)
            {
                var Recommended = ForgeDownloadListItem(RecommendedVersion, OnClick, IsSaveOnly);
                Recommended.Info = "推荐版" + (string.IsNullOrEmpty(Recommended.Info) ? "" : "，" + Recommended.Info);
                Stack.Children.Add(Recommended);
            }
            if (FreshVersion is not null)
            {
                var Fresh = ForgeDownloadListItem(FreshVersion, OnClick, IsSaveOnly);
                Fresh.Info = "最新版" + (string.IsNullOrEmpty(Fresh.Info) ? "" : "，" + Fresh.Info);
                Stack.Children.Add(Fresh);
            }
            // 添加间隔
            Stack.Children.Add(new TextBlock() { Text = "全部版本 (" + Entries.Count + ")", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6d, 13d, 0d, 4d) });
        }
        public static MyListItem ForgeDownloadListItem(ModDownload.DlForgeVersionEntry Entry, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry.VersionName,
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = (new[] { string.IsNullOrEmpty(Entry.ReleaseTime) ? "" : "发布于 " + Entry.ReleaseTime, ModBase.ModeDebug ? "种类：" + Entry.Category : "" }).Where(d => !string.IsNullOrEmpty(d)).Join("，"),
                Logo = ModBase.PathImage + "Blocks/Anvil.png"
            };
            NewItem.Click += OnClick;
            // 建立菜单
            if (IsSaveOnly)
            {
                NewItem.ContentHandler = ForgeSaveContMenuBuild;
            }
            else
            {
                NewItem.ContentHandler = ForgeContMenuBuild;
            }
            // 结束
            return NewItem;
        }
        private static void ForgeContMenuBuild(MyListItem sender, EventArgs e)
        {
            var BtnSave = new MyIconButton() { Logo = ModBase.Logo.IconButtonSave, ToolTip = "另存为" };
            ToolTipService.SetPlacement(BtnSave, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnSave, 30d);
            ToolTipService.SetHorizontalOffset(BtnSave, 2d);
            BtnSave.Click += (_, __) => ModDownloadLib.ForgeSave_Click();
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += (_, __) => ModDownloadLib.ForgeLog_Click();
            sender.Buttons = new[] { BtnSave, BtnInfo };
        }
        private static void ForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
        {
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += (_, __) => ModDownloadLib.ForgeLog_Click();
            sender.Buttons = new[] { BtnInfo };
        }
        private static void ForgeLog_Click(object sender, RoutedEventArgs e)
        {
            ModDownload.DlForgeVersionEntry Version;
            if (((dynamic)sender).Tag is not null)
            {
                Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            ModBase.OpenWebsite($"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{Version.Inherit}-{Version.VersionName}/forge-{Version.Inherit}-{Version.VersionName}-changelog.txt");
        }
        public static void ForgeSave_Click(object sender, RoutedEventArgs e)
        {
            ModDownload.DlForgeVersionEntry Version;
            if (((dynamic)sender).Tag is not null)
            {
                Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            McDownloadForgelikeSave(Version);
        }

        #endregion

        #region Forge 推荐版本获取

        /// <summary>
    /// 尝试刷新 Forge 推荐版本缓存。
    /// </summary>
        public static void McDownloadForgeRecommendedRefresh()
        {
            if (IsForgeRecommendedRefreshed)
                return;
            IsForgeRecommendedRefreshed = true;
            // 获取所有推荐版本列表
            // 内容为："1.15.2":"31.2.0"
            // 保存
            ModBase.RunInNewThread(() => { try { ModBase.Log("[Download] 刷新 Forge 推荐版本缓存开始"); string Result = ModNet.NetGetCodeByLoader("https://bmclapi2.bangbang93.com/forge/promos"); if (Result.Length < 1000) throw new Exception("获取的结果过短（" + Result + "）"); JContainer ResultJson = (JContainer)ModBase.GetJson(Result); var RecommendedList = new List<string>(); foreach (JObject Version in ResultJson) { if (Version["name"] is null || Version["build"] is null) continue; string Name = (string)Version["name"]; if (!Name.EndsWithF("-recommended")) continue; RecommendedList.Add("\"" + Name.Replace("-recommended", "\":\"" + Version["build"]["version"].ToString() + "\"")); } if (RecommendedList.Count < 5) throw new Exception("获取的推荐版本数过少（" + Result + "）"); string CacheJson = "{" + RecommendedList.Join(",") + "}"; ModBase.WriteFile(ModBase.PathTemp + @"Cache\ForgeRecommendedList.json", CacheJson); ModBase.Log("[Download] 刷新 Forge 推荐版本缓存成功"); } catch (Exception ex) { ModBase.Log(ex, "刷新 Forge 推荐版本缓存失败"); } }, "ForgeRecommendedRefresh");
        }
        private static bool IsForgeRecommendedRefreshed = false;

        /// <summary>
    /// 尝试获取某个 MC 版本对应的 Forge 推荐版本。如果不可用会返回 Nothing。
    /// </summary>
        public static string McDownloadForgeRecommendedGet(string McVersion)
        {
            try
            {
                if (McVersion is null)
                    return null;
                string List = ModBase.ReadFile(ModBase.PathTemp + @"Cache\ForgeRecommendedList.json");
                if (List is null || string.IsNullOrEmpty(List))
                {
                    ModBase.Log("[Download] 没有 Forge 推荐版本缓存文件");
                    return null;
                }
                JObject Json = (JObject)ModBase.GetJson(List);
                if (Json is null || !(McVersion ?? "null").Contains(".") || !Json.ContainsKey(McVersion))
                    return null;
                return (Json[McVersion] ?? "").ToString();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 Forge 推荐版本失败（" + (McVersion ?? "null") + "）", ModBase.LogLevel.Feedback);
                return null;
            }
        }

        #endregion

        #region NeoForge 下载菜单

        public static void NeoForgeDownloadListItemPreload(StackPanel Stack, List<ModDownload.DlNeoForgeListEntry> Entries, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 如果只有一个版本，则不特别列出
            if (Entries.Count == 1)
                return;
            // 获取最新稳定版和测试版
            ModDownload.DlNeoForgeListEntry FreshStableVersion = null;
            ModDownload.DlNeoForgeListEntry FreshBetaVersion = null;
            if (Entries.Any())
            {
                foreach (var Entry in Entries.ToList())
                {
                    if (Entry.IsBeta)
                    {
                        if (FreshBetaVersion is null)
                            FreshBetaVersion = Entry;
                    }
                    else
                    {
                        FreshStableVersion = Entry;
                        break;
                    }
                }
            }
            else
            {
                ModBase.Log("[System] 未找到可用的 NeoForge 版本", ModBase.LogLevel.Debug);
            }
            // 显示各个版本
            if (FreshStableVersion is not null)
            {
                var Fresh = NeoForgeDownloadListItem(FreshStableVersion, OnClick, IsSaveOnly);
                Fresh.Info = string.IsNullOrEmpty(Fresh.Info) ? "最新稳定版" : "最新" + Fresh.Info;
                Stack.Children.Add(Fresh);
            }
            if (FreshBetaVersion is not null)
            {
                var Fresh = NeoForgeDownloadListItem(FreshBetaVersion, OnClick, IsSaveOnly);
                Fresh.Info = string.IsNullOrEmpty(Fresh.Info) ? "最新测试版" : "最新" + Fresh.Info;
                Stack.Children.Add(Fresh);
            }
            // 添加间隔
            Stack.Children.Add(new TextBlock() { Text = "全部版本 (" + Entries.Count + ")", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6d, 13d, 0d, 4d) });
        }
        public static MyListItem NeoForgeDownloadListItem(ModDownload.DlNeoForgeListEntry Info, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Info.VersionName,
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Info,
                Info = Info.IsBeta ? "测试版" : "稳定版",
                Logo = ModBase.PathImage + "Blocks/NeoForge.png"
            };
            NewItem.Click += OnClick;
            // 建立菜单
            if (IsSaveOnly)
            {
                NewItem.ContentHandler = NeoForgeSaveContMenuBuild;
            }
            else
            {
                NewItem.ContentHandler = NeoForgeContMenuBuild;
            }
            // 结束
            return NewItem;
        }
        private static void NeoForgeContMenuBuild(MyListItem sender, EventArgs e)
        {
            var BtnSave = new MyIconButton() { Logo = ModBase.Logo.IconButtonSave, ToolTip = "另存为" };
            ToolTipService.SetPlacement(BtnSave, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnSave, 30d);
            ToolTipService.SetHorizontalOffset(BtnSave, 2d);
            BtnSave.Click += (_, __) => ModDownloadLib.NeoForgeSave_Click();
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += (_, __) => ModDownloadLib.NeoForgeLog_Click();
            sender.Buttons = new[] { BtnSave, BtnInfo };
        }
        private static void NeoForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
        {
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += (_, __) => ModDownloadLib.NeoForgeLog_Click();
            sender.Buttons = new[] { BtnInfo };
        }
        private static void NeoForgeLog_Click(object sender, RoutedEventArgs e)
        {
            ModDownload.DlNeoForgeListEntry Info;
            if (((dynamic)sender).Tag is not null)
            {
                Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            ModBase.OpenWebsite(Info.UrlBase + "-changelog.txt");
        }
        public static void NeoForgeSave_Click(object sender, RoutedEventArgs e)
        {
            ModDownload.DlNeoForgeListEntry Info;
            if (((dynamic)sender).Tag is not null)
            {
                Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            McDownloadForgelikeSave(Info);
        }

        #endregion

        #region Cleanroom 下载菜单

        public static void CleanroomDownloadListItemPreload(StackPanel Stack, List<ModDownload.DlCleanroomListEntry> Entries, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 获取最新稳定版和测试版
            // Dim FreshStableVersion As DlCleanroomListEntry = Nothing
            ModDownload.DlCleanroomListEntry FreshBetaVersion = null;
            if (Entries.Any())
            {
                FreshBetaVersion = Entries[0];
            }
            else
            {
                ModBase.Log("[System] 未找到可用的 Cleanroom 版本", ModBase.LogLevel.Debug);
            }
            // 显示各个版本
            // If FreshStableVersion IsNot Nothing Then
            // Dim Fresh = NeoForgeDownloadListItem(FreshStableVersion, OnClick, IsSaveOnly)
            // Fresh.Info = If(Fresh.Info = "", "最新稳定版", "最新" & Fresh.Info)
            // Stack.Children.Add(Fresh)
            // End If
            if (FreshBetaVersion is not null)
            {
                var Fresh = CleanroomDownloadListItem(FreshBetaVersion, OnClick, IsSaveOnly);
                Fresh.Info = string.IsNullOrEmpty(Fresh.Info) ? "最新测试版" : "最新" + Fresh.Info;
                Stack.Children.Add(Fresh);
            }
            // 添加间隔
            Stack.Children.Add(new TextBlock() { Text = "全部版本 (" + Entries.Count + ")", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6d, 13d, 0d, 4d) });
        }
        public static MyListItem CleanroomDownloadListItem(ModDownload.DlCleanroomListEntry Info, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Info.VersionName,
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Info,
                Info = Info.IsBeta ? "测试版" : "稳定版",
                Logo = ModBase.PathImage + "Blocks/Cleanroom.png"
            };
            NewItem.Click += OnClick;
            // 建立菜单
            if (IsSaveOnly)
            {
                NewItem.ContentHandler = CleanroomSaveContMenuBuild;
            }
            else
            {
                NewItem.ContentHandler = CleanroomContMenuBuild;
            }
            // 结束
            return NewItem;
        }
        private static void CleanroomContMenuBuild(MyListItem sender, EventArgs e)
        {
            var BtnSave = new MyIconButton() { Logo = ModBase.Logo.IconButtonSave, ToolTip = "另存为" };
            ToolTipService.SetPlacement(BtnSave, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnSave, 30d);
            ToolTipService.SetHorizontalOffset(BtnSave, 2d);
            BtnSave.Click += (_, __) => ModDownloadLib.CleanroomSave_Click();
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += (_, __) => ModDownloadLib.CleanroomLog_Click();
            sender.Buttons = new[] { BtnSave, BtnInfo };
        }
        private static void CleanroomSaveContMenuBuild(MyListItem sender, EventArgs e)
        {
            var BtnInfo = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更新日志" };
            ToolTipService.SetPlacement(BtnInfo, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnInfo, 30d);
            ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
            BtnInfo.Click += (_, __) => ModDownloadLib.CleanroomLog_Click();
            sender.Buttons = new[] { BtnInfo };
        }
        private static void CleanroomLog_Click(object sender, RoutedEventArgs e)
        {
            ModDownload.DlCleanroomListEntry Info;
            if (((dynamic)sender).Tag is not null)
            {
                Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            ModBase.OpenWebsite(Info.UrlBase + "-changelog.txt");
        }
        public static void CleanroomSave_Click(object sender, RoutedEventArgs e)
        {
            ModDownload.DlCleanroomListEntry Info;
            if (((dynamic)sender).Tag is not null)
            {
                Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
            }
            else if (((dynamic)sender).Parent.Tag is not null)
            {
                Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
            }
            else
            {
                Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
            }
            McDownloadForgelikeSave(Info);
        }

        #endregion

        #region Fabric 下载

        public static void McDownloadFabricLoaderSave(JObject DownloadInfo)
        {
            try
            {
                string Url = DownloadInfo["url"].ToString();
                string FileName = ModBase.GetFileNameFromPath(Url);
                string Version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
                string Target = ModBase.SelectSaveFile("选择保存位置", FileName, "Fabric 安装器 (*.jar)|*.jar");
                if (!Target.Contains(@"\"))
                    return;

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
                {
                    if ((OngoingLoader.Name ?? "") != ($"Fabric {Version} 安装器下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                // 构造步骤加载器
                var Loaders = new List<ModLoader.LoaderBase>();
                // 下载
                // BMCLAPI 不支持 Fabric Installer 下载
                var Address = new List<string>();
                Address.Add(Url);
                Loaders.Add(new ModNet.LoaderDownload("下载主文件", new List<ModNet.NetFile>() { new ModNet.NetFile(Address.ToArray(), Target, new ModBase.FileChecker(MinSize: 1024 * 64)) }) { ProgressWeight = 15d });
                // 启动
                var Loader = new ModLoader.LoaderCombo<JObject>("Fabric " + Version + " 安装器下载", Loaders) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(DownloadInfo);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 Fabric 安装器下载失败", ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
    /// 获取下载某个 Fabric 版本的加载器列表。
    /// </summary>
        private static List<ModLoader.LoaderBase> McDownloadFabricLoader(string FabricVersion, string MinecraftName, string McFolder = null, bool FixLibrary = true)
        {

            // 参数初始化
            McFolder = McFolder ?? ModMinecraft.PathMcFolder;
            bool IsCustomFolder = (McFolder ?? "") != (ModMinecraft.PathMcFolder ?? "");
            string Id = "fabric-loader-" + FabricVersion + "-" + MinecraftName;
            string VersionFolder = McFolder + @"versions\" + Id + @"\";
            var Loaders = new List<ModLoader.LoaderBase>();

            // 下载 Json
            MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响版本文件夹名称
            Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("获取 Fabric 主文件下载地址", (Task) =>
        {
            // 启动依赖版本的下载
            if (FixLibrary)
            {
                McDownloadClient(ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName);
            }
            Task.Progress = 0.5d;
            // 构造文件请求
            Task.Output = new List<ModNet.NetFile>() { new ModNet.NetFile(new[] { "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/" + MinecraftName + "/" + FabricVersion + "/profile/json", "https://meta.fabricmc.net/v2/versions/loader/" + MinecraftName + "/" + FabricVersion + "/profile/json" }, VersionFolder + Id + ".json", new ModBase.FileChecker(IsJson: true)) };
        })
            {
                ProgressWeight = 0.5d
            });
            Loaders.Add(new ModNet.LoaderDownload("下载 Fabric 主文件", new List<ModNet.NetFile>()) { ProgressWeight = 2.5d });

            // 下载支持库
            if (FixLibrary)
            {
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析 Fabric 支持库文件", Task => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionFolder))) { ProgressWeight = 1d, Show = false });
                Loaders.Add(new ModNet.LoaderDownload("下载 Fabric 支持库文件", new List<ModNet.NetFile>()) { ProgressWeight = 8d });
            }

            return Loaders;
        }

        #endregion

        #region Fabric 下载菜单

        public static MyListItem FabricDownloadListItem(JObject Entry, MyListItem.ClickEventHandler OnClick)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry["version"].ToString().Replace("+build", ""),
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = Entry["stable"].ToObject<bool>() ? "稳定版" : "测试版",
                Logo = ModBase.PathImage + "Blocks/Fabric.png"
            };
            NewItem.Click += OnClick;
            // 结束
            return NewItem;
        }
        public static MyListItem FabricApiDownloadListItem(ModComp.CompFile Entry, MyListItem.ClickEventHandler OnClick)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry.DisplayName.Split("]")[1].Replace("Fabric API ", "").Replace(" build ", ".").BeforeFirst("+").Trim(),
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = Entry.StatusDescription + "，发布于 " + Entry.ReleaseDate.ToString("yyyy'/'MM'/'dd HH':'mm"),
                Logo = ModBase.PathImage + "Blocks/Fabric.png"
            };
            NewItem.Click += OnClick;
            // 结束
            return NewItem;
        }
        public static MyListItem OptiFabricDownloadListItem(ModComp.CompFile Entry, MyListItem.ClickEventHandler OnClick)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry.DisplayName.ToLower().Replace("optifabric-", "").Replace(".jar", "").Trim().TrimStart('v'),
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = Entry.StatusDescription + "，发布于 " + Entry.ReleaseDate.ToString("yyyy'/'MM'/'dd HH':'mm"),
                Logo = ModBase.PathImage + "Blocks/OptiFabric.png"
            };
            NewItem.Click += OnClick;
            // 结束
            return NewItem;
        }

        #endregion

        #region Quilt 下载

        public static void McDownloadQuiltLoaderSave(JObject DownloadInfo)
        {
            try
            {
                string Url = DownloadInfo["url"].ToString();
                string FileName = ModBase.GetFileNameFromPath(Url);
                string Version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
                string Target = ModBase.SelectSaveFile("选择保存位置", FileName, "Quilt 安装器 (*.jar)|*.jar");
                if (!Target.Contains(@"\"))
                    return;

                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
                {
                    if ((OngoingLoader.Name ?? "") != ($"Quilt {Version} 安装器下载" ?? ""))
                        continue;
                    ModMain.Hint("该版本正在下载中！", ModMain.HintType.Critical);
                    return;
                }

                // 构造步骤加载器
                var Loaders = new List<ModLoader.LoaderBase>();
                // 下载
                // TODO: BMCLAPI 不支持 Quilt Installer 下载
                var Address = new List<string>();
                Address.Add(Url);
                Loaders.Add(new ModNet.LoaderDownload("下载主文件", new List<ModNet.NetFile>() { new ModNet.NetFile(Address.ToArray(), Target, new ModBase.FileChecker(MinSize: 1024 * 64)) }) { ProgressWeight = 15d });
                // 启动
                var Loader = new ModLoader.LoaderCombo<JObject>("Quilt " + Version + " 安装器下载", Loaders) { OnStateChanged = LoaderStateChangedHintOnly };
                Loader.Start(DownloadInfo);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始 Quilt 安装器下载失败", ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
    /// 获取下载某个 Quilt 版本的加载器列表。
    /// </summary>
        private static List<ModLoader.LoaderBase> McDownloadQuiltLoader(string QuiltVersion, string MinecraftName, string McFolder = null, bool FixLibrary = true)
        {

            // 参数初始化
            McFolder = McFolder ?? ModMinecraft.PathMcFolder;
            bool IsCustomFolder = (McFolder ?? "") != (ModMinecraft.PathMcFolder ?? "");
            string Id = "quilt-loader-" + QuiltVersion + "-" + MinecraftName;
            string VersionFolder = McFolder + @"versions\" + Id + @"\";
            var Loaders = new List<ModLoader.LoaderBase>();

            // 下载 Json
            MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响版本文件夹名称
            Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("获取 Quilt 主文件下载地址", (Task) =>
{
    // 启动依赖版本的下载
if (FixLibrary)
{
McDownloadClient(ModNet.NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName);
}
Task.Progress = 0.5d;
    // 构造文件请求
Task.Output = new List<ModNet.NetFile>() { new ModNet.NetFile(new[] { "https://meta.quiltmc.org/v3/versions/loader/" + MinecraftName + "/" + QuiltVersion + "/profile/json" }, VersionFolder + Id + ".json", new ModBase.FileChecker(IsJson: true)) };
    // 新建 mods 文件夹
Directory.CreateDirectory($@"{(Conversions.ToBoolean(McFolder) ? McFolder : ModMinecraft.PathMcFolder)}mods\");
})
            {
                ProgressWeight = 0.5d
            });
            Loaders.Add(new ModNet.LoaderDownload("下载 Quilt 主文件", new List<ModNet.NetFile>()) { ProgressWeight = 2.5d });

            // 下载支持库
            if (FixLibrary)
            {
                Loaders.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析 Quilt 支持库文件", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionFolder))) { ProgressWeight = 1d, Show = false });
                Loaders.Add(new ModNet.LoaderDownload("下载 Quilt 支持库文件", new List<ModNet.NetFile>()) { ProgressWeight = 8d });
            }

            return Loaders;
        }

        #endregion

        #region Quilt 下载菜单

        public static MyListItem QuiltDownloadListItem(JObject Entry, MyListItem.ClickEventHandler OnClick)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry["version"].ToString(),
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = Entry["maven"].ToString().Contains("installer") ? "安装器" : Entry["version"].ToString().Contains("beta") || Entry["version"].ToString().Contains("pre") ? "测试版" : "稳定版",
                Logo = ModBase.PathImage + "Blocks/Quilt.png"
            };
            NewItem.Click += OnClick;
            // 结束
            return NewItem;
        }
        public static MyListItem QSLDownloadListItem(ModComp.CompFile Entry, MyListItem.ClickEventHandler OnClick)
        {
            // 建立控件
            var NewItem = new MyListItem()
            {
                Title = Entry.DisplayName.Split("]")[1].Replace(" build ", ".").Split("+")[0].Trim(),
                SnapsToDevicePixels = true,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Tag = Entry,
                Info = Entry.StatusDescription + "，发布于 " + Entry.ReleaseDate.ToString("yyyy'/'MM'/'dd HH':'mm"),
                Logo = ModBase.PathImage + "Blocks/Quilt.png"
            };
            NewItem.Click += OnClick;
            // 结束
            return NewItem;
        }

        #endregion

        #region 合并安装

        /// <summary>
    /// 安装请求。
    /// </summary>
        public class McInstallRequest
        {

            /// <summary>
        /// 必填。安装目标版本名称。
        /// </summary>
            public string TargetVersionName;
            /// <summary>
        /// 必填。安装目标文件夹。
        /// </summary>
            public string TargetVersionFolder;

            /// <summary>
        /// 必填。欲下载的 Minecraft 的版本名。
        /// </summary>
            public string MinecraftName = null;
            /// <summary>
        /// 可选。欲下载的 Minecraft Json 地址。
        /// </summary>
            public string MinecraftJson = null;

            // 若要下载 OptiFine，则需要在下面两项中完成至少一项
            /// <summary>
        /// 欲下载的 OptiFine 版本名。例如 HD_U_F6_pre1。
        /// </summary>
            public string OptiFineVersion = null;
            /// <summary>
        /// 欲下载的 OptiFine 详细信息。
        /// </summary>
            public ModDownload.DlOptiFineListEntry OptiFineEntry = null;

            // 若要下载 Forge，则需要在下面两项中完成至少一项
            /// <summary>
        /// 欲下载的 Forge 版本名。接受例如 36.1.4 / 14.23.5.2859 / 1.19-41.1.0 的输入。
        /// </summary>
            public string ForgeVersion = null;
            /// <summary>
        /// 欲下载的 Forge。
        /// </summary>
            public ModDownload.DlForgeVersionEntry ForgeEntry = null;

            // 若要下载 NeoForge，则需要在下面两项中完成至少一项
            /// <summary>
        /// 欲下载的 NeoForge 版本名。
        /// </summary>
            public string NeoForgeVersion = null;
            /// <summary>
        /// 欲下载的 NeoForge。
        /// </summary>
            public ModDownload.DlNeoForgeListEntry NeoForgeEntry = null;

            // 若要下载 Cleanroom，则需要在下面两项中完成至少一项
            /// <summary>
        /// 欲下载的 Cleanroom 版本名。
        /// </summary>
            public string CleanroomVersion = null;
            /// <summary>
        /// 欲下载的 Cleanroom。
        /// </summary>
            public ModDownload.DlCleanroomListEntry CleanroomEntry = null;

            /// <summary>
        /// 欲下载的 Fabric Loader 版本名。
        /// </summary>
            public string FabricVersion = null;

            /// <summary>
        /// 欲下载的 Fabric API 信息。
        /// </summary>
            public ModComp.CompFile FabricApi = null;

            /// <summary>
        /// 欲下载的 Quilt Loader 版本名。
        /// </summary>
            public string QuiltVersion = null;

            /// <summary>
        /// 欲下载的 Quilted Fabric API (QFAPI) / Quilt Standard Libraries (QSL) 信息。
        /// </summary>
            public ModComp.CompFile QSL = null;

            /// <summary>
        /// 欲下载的 OptiFabric 信息。
        /// </summary>
            public ModComp.CompFile OptiFabric = null;

            /// <summary>
        /// 欲下载的 LiteLoader 详细信息。
        /// </summary>
            public ModDownload.DlLiteLoaderListEntry LiteLoaderEntry = null;

        }

        /// <summary>
    /// 在加载器状态改变后显示一条提示。
    /// 不会进行任何其他操作。
    /// </summary>
        public static void LoaderStateChangedHintOnly(object Loader)
        {
            switch (((dynamic)Loader).State)
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, ModBase.LoadState.Finished, false):
                    {
                        ModMain.Hint(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Name, "成功！")), ModMain.HintType.Finish);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, ModBase.LoadState.Failed, false):
                    {
                        ModMain.Hint(Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(((dynamic)Loader).Name, "失败："), ModBase.GetExceptionSummary((Exception)((dynamic)Loader).Error))), ModMain.HintType.Critical);
                        break;
                    }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, ModBase.LoadState.Aborted, false):
                    {
                        ModMain.Hint(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Name, "已取消！")), ModMain.HintType.Info);
                        break;
                    }
            }
        }
        /// <summary>
    /// 安装加载器状态改变后进行提示和重载文件夹列表的方法。
    /// </summary>
        public static void McInstallState(object Loader)
        {
            switch (((dynamic)Loader).State)
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, ModBase.LoadState.Finished, false):
                    {
                        ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "VersionCache", ""); // 清空缓存（合并安装会先生成文件夹，这会在刷新时误判为可以使用缓存）
                        ModBase.DeleteDirectory(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, @"PCLInstallBackups\")));
                        ModMain.Hint(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Name, "成功！")), ModMain.HintType.Finish);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, ModBase.LoadState.Failed, false):
                    {
                        ModMain.Hint(Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(((dynamic)Loader).Name, "失败："), ModBase.GetExceptionSummary((Exception)((dynamic)Loader).Error))), ModMain.HintType.Critical);
                        break;
                    }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, ModBase.LoadState.Aborted, false):
                    {
                        ModMain.Hint(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Name, "已取消！")), ModMain.HintType.Info);
                        break;
                    }
                case var case3 when Operators.ConditionalCompareObjectEqual(case3, ModBase.LoadState.Loading, false):
                    {
                        return; // 不重新加载版本列表
                    }
            }
            if (Conversions.ToBoolean(!Operators.ConditionalCompareObjectEqual(((dynamic)Loader).State, ModBase.LoadState.Finished, false) && Directory.Exists(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, @"PCLInstallBackups\"))))) // 版本修改失败回滚
            {
                ModBase.CopyDirectory(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, @"PCLInstallBackups\")), Conversions.ToString(((dynamic)Loader).Input));
                File.Delete(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, ".pclignore")));
            }
            else
            {
                McInstallFailedClearFolder(Loader);
            }
            ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
        }
        public static void McInstallFailedClearFolder(object Loader)
        {
            try
            {
                Thread.Sleep(1000); // 防止存在尚未完全释放的文件，导致清理失败（例如整合包安装）
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)Loader).State, ModBase.LoadState.Failed, false)) || Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)Loader).State, ModBase.LoadState.Aborted, false)))
                {
                    // 删除版本文件夹
                    if (Directory.Exists(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, @"saves\"))) || Directory.Exists(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, @"versions\"))) || Directory.Exists(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, @"mods\"))) || File.Exists(Conversions.ToString(Operators.ConcatenateObject(((dynamic)Loader).Input, "server.dat"))))
                    {
                        ModBase.Log(Conversions.ToString(Operators.ConcatenateObject("[Download] 由于版本已被独立启动，不清理版本文件夹：", ((dynamic)Loader).Input)), ModBase.LogLevel.Developer);
                    }
                    else
                    {
                        ModBase.Log(Conversions.ToString(Operators.ConcatenateObject("[Download] 由于下载失败或取消，清理版本文件夹：", ((dynamic)Loader).Input)), ModBase.LogLevel.Developer);
                        ModBase.DeleteDirectory(Conversions.ToString(((dynamic)Loader).Input));
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "下载失败或取消后清理版本文件夹失败");
            }
        }

        /// <summary>
    /// 进行合并安装。返回是否已经开始安装（例如如果没有安装 Java 则会进行提示并返回 False）
    /// </summary>
        public static bool McInstall(McInstallRequest Request, string Type = "安装")
        {
            try
            {
                var SubLoaders = McInstallLoader(Request, IgnoreDump: Type != "安装");
                if (SubLoaders is null)
                    return false;
                var Loader = new ModLoader.LoaderCombo<string>(Request.TargetVersionName + " " + Type, SubLoaders) { OnStateChanged = McInstallState };

                // 启动
                Loader.Start(Request.TargetVersionFolder);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
                return true;
            }

            catch (ModBase.CancelledException ex)
            {
                return false;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "开始合并安装失败", ModBase.LogLevel.Feedback);
                return false;
            }
        }
        /// <summary>
    /// 获取合并安装加载器列表，并进行前期的缓存清理与 Java 检查工作。
    /// </summary>
    /// <exception cref="CancelledException" />


        public static List<ModLoader.LoaderBase> McInstallLoader(McInstallRequest Request, bool DontFixLibraries = false, bool IgnoreDump = false)
        {

            // 获取缓存目录（安装 Mod 加载器的文件夹不能包含空格）
            string TempMcFolder = ModMain.RequestTaskTempFolder(Request.OptiFineEntry is not null || Request.ForgeEntry is not null || Request.NeoForgeEntry is not null);

            // 获取参数
            string VersionFolder = ModMinecraft.PathMcFolder + @"versions\" + Request.TargetVersionName + @"\";
            if (Directory.Exists(TempMcFolder))
                ModBase.DeleteDirectory(TempMcFolder);
            string OptiFineFolder = null;
            if (Request.OptiFineVersion is not null)
            {
                if (Request.OptiFineVersion.Contains("_HD_U_"))
                    Request.OptiFineVersion = "HD_U_" + Request.OptiFineVersion.AfterLast("_HD_U_"); // #735
                Request.OptiFineEntry = new ModDownload.DlOptiFineListEntry()
                {
                    NameDisplay = Request.MinecraftName + " " + Request.OptiFineVersion.Replace("HD_U_", "").Replace("_", "").Replace("pre", " pre"),
                    Inherit = Request.MinecraftName,
                    IsPreview = Request.OptiFineVersion.ContainsF("pre", true),
                    NameVersion = Request.MinecraftName + "-OptiFine_" + Request.OptiFineVersion,
                    NameFile = (Request.OptiFineVersion.ContainsF("pre", true) ? "preview_" : "") + "OptiFine_" + Request.MinecraftName + "_" + Request.OptiFineVersion + ".jar"
                };
            }
            if (Request.OptiFineEntry is not null)
                OptiFineFolder = TempMcFolder + @"versions\" + Request.OptiFineEntry.NameVersion;
            string ForgeFolder = null;
            if (Request.ForgeEntry is not null)
                Request.ForgeVersion = Request.ForgeVersion ?? Request.ForgeEntry.VersionName;
            if (Request.ForgeVersion is not null)
                ForgeFolder = TempMcFolder + @"versions\forge-" + Request.ForgeVersion;
            string NeoForgeFolder = null;
            if (Request.NeoForgeEntry is not null)
                Request.NeoForgeVersion = Request.NeoForgeVersion ?? Request.NeoForgeEntry.VersionName;
            if (Request.NeoForgeVersion is not null)
                NeoForgeFolder = TempMcFolder + @"versions\neoforge-" + Request.NeoForgeVersion;
            string CleanroomFolder = null;
            if (Request.CleanroomEntry is not null)
                Request.CleanroomVersion = Request.CleanroomVersion ?? Request.CleanroomEntry.VersionName;
            if (Request.CleanroomVersion is not null)
                CleanroomFolder = TempMcFolder + @"versions\cleanroom-" + Request.CleanroomVersion;
            string FabricFolder = null;
            if (Request.FabricVersion is not null)
                FabricFolder = TempMcFolder + @"versions\fabric-loader-" + Request.FabricVersion + "-" + Request.MinecraftName;
            string QuiltFolder = null;
            if (Request.QuiltVersion is not null)
                QuiltFolder = TempMcFolder + @"versions\quilt-loader-" + Request.QuiltVersion + "-" + Request.MinecraftName;
            string LiteLoaderFolder = null;
            if (Request.LiteLoaderEntry is not null)
                LiteLoaderFolder = TempMcFolder + @"versions\" + Request.MinecraftName + "-LiteLoader";

            // 决定版本隔离情况（#5970）
            bool Modable = Request.FabricVersion is not null || Request.ForgeEntry is not null || Request.NeoForgeEntry is not null || Request.LiteLoaderEntry is not null;
            var Version = new ModMinecraft.McVersion(VersionFolder);
            Version.InitPathIndie(Modable);
            string ModsFolder = (Modable ? VersionFolder : ModMinecraft.PathMcFolder) + @"mods\";

            // 判断 OptiFine 是否作为 Mod 进行下载
            bool OptiFineAsMod = Request.OptiFineEntry is not null && Modable; // 选择了 OptiFine 与任意 Mod 加载器
            if (OptiFineAsMod)
            {
                ModBase.Log("[Download] OptiFine 将作为 Mod 进行下载");
                OptiFineFolder = ModsFolder;
            }

            // 记录日志
            if (OptiFineFolder is not null)
                ModBase.Log("[Download] OptiFine 缓存：" + OptiFineFolder);
            if (ForgeFolder is not null)
                ModBase.Log("[Download] Forge 缓存：" + ForgeFolder);
            if (NeoForgeFolder is not null)
                ModBase.Log("[Download] NeoForge 缓存：" + NeoForgeFolder);
            if (CleanroomFolder is not null)
                ModBase.Log("[Download] Cleanroom 缓存：" + CleanroomFolder);
            if (FabricFolder is not null)
                ModBase.Log("[Download] Fabric 缓存：" + FabricFolder);
            if (QuiltFolder is not null)
                ModBase.Log("[Download] Quilt 缓存：" + QuiltFolder);
            if (LiteLoaderFolder is not null)
                ModBase.Log("[Download] LiteLoader 缓存：" + LiteLoaderFolder);
            ModBase.Log("[Download] 对应的原版版本：" + Request.MinecraftName);

            // 重复版本检查
            if (File.Exists(TempMcFolder + Request.TargetVersionName + ".json") && !IgnoreDump)
            {
                ModMain.Hint("版本 " + Request.TargetVersionName + " 已经存在！", ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }

            var LoaderList = new List<ModLoader.LoaderBase>();
            // 添加忽略标识
            LoaderList.Add(new ModLoader.LoaderTask<int, int>("添加忽略标识", () => ModBase.WriteFile(VersionFolder + ".pclignore", "用于临时地在 PCL 的版本列表中屏蔽此版本。")) { Show = false, Block = false });
            // Fabric API
            if (Request.FabricApi is not null)
            {
                LoaderList.Add(new ModNet.LoaderDownload("下载 Fabric API", new List<ModNet.NetFile>() { Request.FabricApi.ToNetFile(ModsFolder) }) { ProgressWeight = 3d, Block = false });
            }
            // Quilted Fabric API (QFAPI) / Quilt Standard Libraries (QSL)
            if (Request.QSL is not null)
            {
                LoaderList.Add(new ModNet.LoaderDownload("下载 QFAPI / QSL", new List<ModNet.NetFile>() { Request.QSL.ToNetFile(ModsFolder) }) { ProgressWeight = 3d, Block = false });
            }
            // OptiFabric
            if (Request.OptiFabric is not null)
            {
                LoaderList.Add(new ModNet.LoaderDownload("下载 OptiFabric", new List<ModNet.NetFile>() { Request.OptiFabric.ToNetFile(ModsFolder) }) { ProgressWeight = 3d, Block = false });
            }
            // 原版
            var ClientLoader = new ModLoader.LoaderCombo<string>("下载原版 " + Request.MinecraftName, McDownloadClientLoader(Request.MinecraftName, Request.MinecraftJson, Request.TargetVersionName)) { Show = false, ProgressWeight = 39d, Block = Request.ForgeVersion is null && Request.OptiFineEntry is null && Request.FabricVersion is null && Request.LiteLoaderEntry is null };
            LoaderList.Add(ClientLoader);
            // OptiFine
            if (Request.OptiFineEntry is not null)
            {
                if (OptiFineAsMod)
                {
                    LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 OptiFine " + Request.OptiFineEntry.NameDisplay, McDownloadOptiFineSaveLoader(Request.OptiFineEntry, OptiFineFolder + Request.OptiFineEntry.NameFile)) { Show = false, ProgressWeight = 16d, Block = Request.ForgeVersion is null && Request.FabricVersion is null && Request.LiteLoaderEntry is null });
                }
                else
                {
                    LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 OptiFine " + Request.OptiFineEntry.NameDisplay, McDownloadOptiFineLoader(Request.OptiFineEntry, TempMcFolder, ClientLoader, Request.TargetVersionFolder, false)) { Show = false, ProgressWeight = 24d, Block = Request.ForgeVersion is null && Request.FabricVersion is null && Request.LiteLoaderEntry is null });
                }
            }
            // Forge
            if (Request.ForgeVersion is not null)
            {
                LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 Forge " + Request.ForgeVersion, McDownloadForgelikeLoader("Forge", Request.ForgeVersion, "forge-" + Request.ForgeVersion, Request.MinecraftName, Request.ForgeEntry, TempMcFolder, ClientLoader, Request.TargetVersionFolder)) { Show = false, ProgressWeight = 25d, Block = Request.FabricVersion is null && Request.LiteLoaderEntry is null && Request.NeoForgeEntry is null });
            }
            // NeoForge
            if (Request.NeoForgeVersion is not null)
            {
                LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 NeoForge " + Request.NeoForgeVersion, McDownloadForgelikeLoader("NeoForge", Request.NeoForgeVersion, "neoforge-" + Request.NeoForgeVersion, Request.MinecraftName, Request.NeoForgeEntry, TempMcFolder, ClientLoader, Request.TargetVersionFolder)) { Show = false, ProgressWeight = 25d, Block = Request.ForgeEntry is null && Request.FabricVersion is null && Request.LiteLoaderEntry is null });
            }
            // Cleanroom
            if (Request.CleanroomVersion is not null)
            {
                LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 Cleanroom " + Request.CleanroomVersion, McDownloadForgelikeLoader("Cleanroom", Request.CleanroomVersion, "cleanroom-" + Request.CleanroomVersion, Request.MinecraftName, Request.CleanroomEntry, TempMcFolder, ClientLoader, Request.TargetVersionFolder)) { Show = false, ProgressWeight = 25d, Block = Request.ForgeEntry is null && Request.FabricVersion is null && Request.LiteLoaderEntry is null });
            }
            // LiteLoader
            if (Request.LiteLoaderEntry is not null)
            {
                LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 LiteLoader " + Request.MinecraftName, McDownloadLiteLoaderLoader(Request.LiteLoaderEntry, TempMcFolder, ClientLoader, false)) { Show = false, ProgressWeight = 1d, Block = Request.FabricVersion is null });
            }
            // Fabric
            if (Request.FabricVersion is not null)
            {
                LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 Fabric " + Request.FabricVersion, McDownloadFabricLoader(Request.FabricVersion, Request.MinecraftName, TempMcFolder, false)) { Show = false, ProgressWeight = 2d, Block = true });
            }
            // Quilt
            if (Request.QuiltVersion is not null)
            {
                LoaderList.Add(new ModLoader.LoaderCombo<string>("下载 Quilt " + Request.QuiltVersion, McDownloadQuiltLoader(Request.QuiltVersion, Request.MinecraftName, TempMcFolder, false)) { Show = false, ProgressWeight = 2d, Block = true });
            }
            // 合并安装
            LoaderList.Add(new ModLoader.LoaderTask<string, string>("安装游戏", (Task) =>
    {
        ModBase.Log("[Test] Clr folder: " + CleanroomFolder);
        ModBase.Log("[Test] Clr version: " + Request.CleanroomVersion);
        InstallMerge(TempMcFolder, TempMcFolder, OptiFineFolder, OptiFineAsMod, ForgeFolder, Request.ForgeVersion, NeoForgeFolder, Request.NeoForgeVersion, CleanroomFolder, Request.CleanroomVersion, FabricFolder, QuiltFolder, LiteLoaderFolder);
        Task.Progress = 0.3d;
        if (Directory.Exists(TempMcFolder + "libraries"))
            ModBase.CopyDirectory(TempMcFolder + "libraries", ModMinecraft.PathMcFolder + "libraries");
        if (Directory.Exists(TempMcFolder + "mods"))
            ModBase.CopyDirectory(TempMcFolder + "mods", ModsFolder);
        // 新建 mods 文件夹
        if (Request.ForgeVersion is not null || Request.FabricVersion is not null || Request.NeoForgeEntry is not null || Request.CleanroomEntry is not null || Request.LiteLoaderEntry is not null)
        {
            Directory.CreateDirectory(ModsFolder);
            ModBase.Log("[Download] 自动创建 mods 文件夹：" + ModsFolder);
        }
    })
            {
                ProgressWeight = 2d,
                Block = true
            });
            // 补全文件
            if (!DontFixLibraries && (Request.OptiFineEntry is not null || Request.ForgeVersion is not null && Conversions.ToDouble(Request.ForgeVersion.Split(".")[0]) >= 20d || Request.NeoForgeVersion is not null || Request.FabricVersion is not null || Request.QuiltVersion is not null || Request.CleanroomVersion is not null || Request.LiteLoaderEntry is not null))
            {
                var LoadersLib = new List<ModLoader.LoaderBase>();
                LoadersLib.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析游戏支持库文件（副加载器）", (Task) => Task.Output = ModMinecraft.McLibFix(new ModMinecraft.McVersion(VersionFolder))) { ProgressWeight = 1d, Show = false });
                LoadersLib.Add(new ModNet.LoaderDownload("下载游戏支持库文件（副加载器）", new List<ModNet.NetFile>()) { ProgressWeight = 7d, Show = false });
                LoaderList.Add(new ModLoader.LoaderCombo<string>("下载游戏支持库文件", LoadersLib) { ProgressWeight = 8d });
            }
            // 删除忽略标识
            LoaderList.Add(new ModLoader.LoaderTask<int, int>("删除忽略标识", () => File.Delete(VersionFolder + ".pclignore")) { Show = false });
            // 总加载器
            return LoaderList;
        }

        /// <summary>
    /// 将多个版本 Json 进行合并，如果目标已存在则直接覆盖。失败会抛出异常。
    /// </summary>
        private static void InstallMerge(string OutputFolder, string MinecraftFolder, string OptiFineFolder = null, bool OptiFineAsMod = false, string ForgeFolder = null, string ForgeVersion = null, string NeoForgeFolder = null, string NeoForgeVersion = null, string CleanroomFolder = null, string CleanroomVersion = null, string FabricFolder = null, string QuiltFolder = null, string LiteLoaderFolder = null)
        {
            ModBase.Log("[Download] 开始进行版本合并，输出：" + OutputFolder + "，Minecraft：" + MinecraftFolder + (OptiFineFolder is not null ? "，OptiFine：" + OptiFineFolder : "") + (ForgeFolder is not null ? "，Forge：" + ForgeFolder : "") + (NeoForgeFolder is not null ? "，NeoForge：" + NeoForgeFolder : "") + (CleanroomFolder is not null ? "，Cleanroom：" + CleanroomFolder : "") + (LiteLoaderFolder is not null ? "，LiteLoader：" + LiteLoaderFolder : "") + (FabricFolder is not null ? "，Fabric：" + FabricFolder : "") + (QuiltFolder is not null ? "，Quilt：" + QuiltFolder : ""));
            Directory.CreateDirectory(OutputFolder);

            bool HasOptiFine = OptiFineFolder is not null && !OptiFineAsMod;
            bool HasForge = ForgeFolder is not null;
            bool HasNeoForge = NeoForgeFolder is not null;
            bool HasCleanroom = CleanroomFolder is not null;
            bool HasLiteLoader = LiteLoaderFolder is not null;
            bool HasFabric = FabricFolder is not null;
            bool HasQuilt = QuiltFolder is not null;
            string OutputName;
            string MinecraftName;
            string OptiFineName;
            string ForgeName;
            string NeoForgeName;
            string CleanroomName;
            string LiteLoaderName;
            string FabricName;
            string QuiltName;
            string OutputJsonPath;
            string MinecraftJsonPath;
            string OptiFineJsonPath = null;
            string ForgeJsonPath = null;
            string NeoForgeJsonPath = null;
            var CleanroomJsonPath = default(string);
            string LiteLoaderJsonPath = null;
            string FabricJsonPath = null;
            string QuiltJsonPath = null;
            string OutputJar;
            string MinecraftJar;
            #region 初始化路径信息
            if (!OutputFolder.EndsWithF(@"\"))
                OutputFolder += @"\";
            OutputName = ModBase.GetFolderNameFromPath(OutputFolder);
            OutputJsonPath = OutputFolder + OutputName + ".json";
            OutputJar = OutputFolder + OutputName + ".jar";

            if (!MinecraftFolder.EndsWithF(@"\"))
                MinecraftFolder += @"\";
            MinecraftName = ModBase.GetFolderNameFromPath(MinecraftFolder);
            MinecraftJsonPath = MinecraftFolder + MinecraftName + ".json";
            MinecraftJar = MinecraftFolder + MinecraftName + ".jar";

            if (HasOptiFine)
            {
                if (!OptiFineFolder.EndsWithF(@"\"))
                    OptiFineFolder += @"\";
                OptiFineName = ModBase.GetFolderNameFromPath(OptiFineFolder);
                OptiFineJsonPath = OptiFineFolder + OptiFineName + ".json";
            }

            if (HasForge)
            {
                if (!ForgeFolder.EndsWithF(@"\"))
                    ForgeFolder += @"\";
                ForgeName = ModBase.GetFolderNameFromPath(ForgeFolder);
                ForgeJsonPath = ForgeFolder + ForgeName + ".json";
            }

            if (HasNeoForge)
            {
                if (!NeoForgeFolder.EndsWithF(@"\"))
                    NeoForgeFolder += @"\";
                NeoForgeName = ModBase.GetFolderNameFromPath(NeoForgeFolder);
                NeoForgeJsonPath = NeoForgeFolder + NeoForgeName + ".json";
            }

            if (HasCleanroom)
            {
                if (!CleanroomFolder.EndsWithF(@"\"))
                    CleanroomFolder += @"\";
                CleanroomName = ModBase.GetFolderNameFromPath(CleanroomFolder);
                CleanroomJsonPath = CleanroomFolder + CleanroomName + ".json";
            }

            if (HasLiteLoader)
            {
                if (!LiteLoaderFolder.EndsWithF(@"\"))
                    LiteLoaderFolder += @"\";
                LiteLoaderName = ModBase.GetFolderNameFromPath(LiteLoaderFolder);
                LiteLoaderJsonPath = LiteLoaderFolder + LiteLoaderName + ".json";
            }

            if (HasFabric)
            {
                if (!FabricFolder.EndsWithF(@"\"))
                    FabricFolder += @"\";
                FabricName = ModBase.GetFolderNameFromPath(FabricFolder);
                FabricJsonPath = FabricFolder + FabricName + ".json";
            }

            if (HasQuilt)
            {
                if (!QuiltFolder.EndsWithF(@"\"))
                    QuiltFolder += @"\";
                QuiltName = ModBase.GetFolderNameFromPath(QuiltFolder);
                QuiltJsonPath = QuiltFolder + QuiltName + ".json";
            }
            #endregion

            JObject OutputJson;
            JObject MinecraftJson;
            JObject OptiFineJson = null;
            JObject ForgeJson = null;
            JObject NeoForgeJson = null;
            JObject CleanroomJson = null;
            JObject LiteLoaderJson = null;
            JObject FabricJson = null;
            JObject QuiltJson = null;
            #region 读取文件并检查文件是否合规
            string MinecraftJsonText = ModBase.ReadFile(MinecraftJsonPath);
            if (!MinecraftJsonText.StartsWithF("{"))
                throw new Exception("Minecraft Json 有误，地址：" + MinecraftJsonPath + "，前段内容：" + MinecraftJsonText.Substring(0, Math.Min(MinecraftJsonText.Length, 1000)));
            MinecraftJson = (JObject)ModBase.GetJson(MinecraftJsonText);

            if (HasOptiFine)
            {
                string OptiFineJsonText = ModBase.ReadFile(OptiFineJsonPath);
                if (!OptiFineJsonText.StartsWithF("{"))
                    throw new Exception("OptiFine Json 有误，地址：" + OptiFineJsonPath + "，前段内容：" + OptiFineJsonText.Substring(0, Math.Min(OptiFineJsonText.Length, 1000)));
                OptiFineJson = (JObject)ModBase.GetJson(OptiFineJsonText);
            }

            if (HasForge)
            {
                string ForgeJsonText = ModBase.ReadFile(ForgeJsonPath);
                if (!ForgeJsonText.StartsWithF("{"))
                    throw new Exception("Forge Json 有误，地址：" + ForgeJsonPath + "，前段内容：" + ForgeJsonText.Substring(0, Math.Min(ForgeJsonText.Length, 1000)));
                ForgeJson = (JObject)ModBase.GetJson(ForgeJsonText);
            }

            if (HasNeoForge)
            {
                string NeoForgeJsonText = ModBase.ReadFile(NeoForgeJsonPath);
                if (!NeoForgeJsonText.StartsWithF("{"))
                    throw new Exception("NeoForge Json 有误，地址：" + NeoForgeJsonPath + "，前段内容：" + NeoForgeJsonText.Substring(0, Math.Min(NeoForgeJsonText.Length, 1000)));
                NeoForgeJson = (JObject)ModBase.GetJson(NeoForgeJsonText);
            }

            if (HasCleanroom)
            {
                string CleanroomJsonText = ModBase.ReadFile(CleanroomJsonPath);
                if (!CleanroomJsonText.StartsWithF("{"))
                    throw new Exception("Cleanroom Json 有误，地址：" + CleanroomJsonPath + "，前段内容：" + CleanroomJsonText.Substring(0, Math.Min(CleanroomJsonText.Length, 1000)));
                CleanroomJson = (JObject)ModBase.GetJson(CleanroomJsonText);
            }

            if (HasLiteLoader)
            {
                string LiteLoaderJsonText = ModBase.ReadFile(LiteLoaderJsonPath);
                if (!LiteLoaderJsonText.StartsWithF("{"))
                    throw new Exception("LiteLoader Json 有误，地址：" + LiteLoaderJsonPath + "，前段内容：" + LiteLoaderJsonText.Substring(0, Math.Min(LiteLoaderJsonText.Length, 1000)));
                LiteLoaderJson = (JObject)ModBase.GetJson(LiteLoaderJsonText);
            }

            if (HasFabric)
            {
                string FabricJsonText = ModBase.ReadFile(FabricJsonPath);
                if (!FabricJsonText.StartsWithF("{"))
                    throw new Exception("Fabric Json 有误，地址：" + FabricJsonPath + "，前段内容：" + FabricJsonText.Substring(0, Math.Min(FabricJsonText.Length, 1000)));
                FabricJson = (JObject)ModBase.GetJson(FabricJsonText);
            }

            if (HasQuilt)
            {
                string QuiltJsonText = ModBase.ReadFile(QuiltJsonPath);
                if (!QuiltJsonText.StartsWithF("{"))
                    throw new Exception("Quilt Json 有误，地址：" + QuiltJsonPath + "，前段内容：" + QuiltJsonText.Substring(0, Math.Min(QuiltJsonText.Length, 1000)));
                QuiltJson = (JObject)ModBase.GetJson(QuiltJsonText);
            }
            #endregion

            #region 处理 JSON 文件
            // 获取 minecraftArguments
            string AllArguments = (MinecraftJson["minecraftArguments"] ?? " ").ToString() + " " + (OptiFineJson is not null ? (OptiFineJson["minecraftArguments"] ?? " ").ToString() : " ") + " " + (ForgeJson is not null ? (ForgeJson["minecraftArguments"] ?? " ").ToString() : " ") + " " + (NeoForgeJson is not null ? (NeoForgeJson["minecraftArguments"] ?? " ").ToString() : " ") + " " + (CleanroomJson is not null ? (CleanroomJson["minecraftArguments"] ?? " ").ToString() : " ") + " " + (LiteLoaderJson is not null ? (LiteLoaderJson["minecraftArguments"] ?? " ").ToString() : " ");
            // 分割参数字符串
            var RawArguments = AllArguments.Split(" ").Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Trim()).ToList();
            var SplitArguments = new List<string>();
            for (int i = 0, loopTo = RawArguments.Count - 1; i <= loopTo; i++)
            {
                if (RawArguments[i].StartsWithF("-"))
                {
                    SplitArguments.Add(RawArguments[i]);
                }
                else if (SplitArguments.Any() && SplitArguments.Last().StartsWithF("-") && !SplitArguments.Last().Contains(" "))
                {
                    SplitArguments[SplitArguments.Count - 1] = SplitArguments.Last() + " " + RawArguments[i];
                }
                else
                {
                    SplitArguments.Add(RawArguments[i]);
                }
            }
            string RealArguments = SplitArguments.Distinct().ToList().Join(" ");
            // 合并
            // 相关讨论见 #2801
            OutputJson = MinecraftJson;
            if (HasOptiFine)
            {
                // 合并 OptiFine
                OptiFineJson.Remove("releaseTime");
                OptiFineJson.Remove("time");
                OutputJson.Merge(OptiFineJson);
            }
            if (HasForge)
            {
                // 合并 Forge
                ForgeJson.Remove("releaseTime");
                ForgeJson.Remove("time");
                OutputJson.Merge(ForgeJson);
            }
            if (HasNeoForge)
            {
                // 合并 NeoForge
                NeoForgeJson.Remove("releaseTime");
                NeoForgeJson.Remove("time");
                OutputJson.Merge(NeoForgeJson);
            }
            if (HasCleanroom)
            {
                // 合并 Cleanroom
                CleanroomJson.Remove("releaseTime");
                CleanroomJson.Remove("time");
                OutputJson.Merge(CleanroomJson);
            }
            if (HasLiteLoader)
            {
                // 合并 LiteLoader
                LiteLoaderJson.Remove("releaseTime");
                LiteLoaderJson.Remove("time");
                OutputJson.Merge(LiteLoaderJson);
            }
            if (HasFabric)
            {
                // 合并 Fabric
                FabricJson.Remove("releaseTime");
                FabricJson.Remove("time");
                OutputJson.Merge(FabricJson);
            }
            if (HasQuilt)
            {
                // 合并 Quilt
                QuiltJson.Remove("releaseTime");
                QuiltJson.Remove("time");
                OutputJson.Merge(QuiltJson);
            }
            // 修改
            if (RealArguments is not null && !string.IsNullOrEmpty(RealArguments.Replace(" ", "")))
                OutputJson["minecraftArguments"] = RealArguments;
            OutputJson.Remove("_comment_");
            OutputJson.Remove("inheritsFrom");
            OutputJson.Remove("jar");
            OutputJson["id"] = OutputName;
            #endregion

            #region 保存
            ModBase.WriteFile(OutputJsonPath, OutputJson.ToString());
            if ((MinecraftJar ?? "") != (OutputJar ?? "")) // 可能是同一个文件
            {
                if (File.Exists(OutputJar))
                    File.Delete(OutputJar);
                ModBase.CopyFile(MinecraftJar, OutputJar);
            }
            ModBase.Log("[Download] 版本合并 " + OutputName + " 完成");
            #endregion

        }

        #endregion

    }
}