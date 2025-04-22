using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageVersionOverall
    {

        private bool IsLoad = false;

        public PageVersionOverall()
        {
            this.Loaded += PageSetupLaunch_Loaded;
        }
        private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 更新设置
            this.ItemDisplayLogoCustom.Tag = @"PCL\Logo.png";
            Reload();

            // 非重复加载部分
            if (IsLoad)
                return;
            IsLoad = true;
            this.PanDisplay.TriggerForceResize();

        }

        public MyListItem ItemVersion;
        /// <summary>
    /// 确保当前页面上的信息已正确显示。
    /// </summary>
        private void Reload()
        {
            ModAnimation.AniControlEnabled += 1;

            // 刷新设置项目
            this.ComboDisplayType.SelectedIndex = Conversions.ToInteger(ModBase.ReadIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "DisplayType", ((int)ModMinecraft.McVersionCardType.Auto).ToString()));
            this.BtnDisplayStar.Text = PageVersionLeft.Version.IsStar ? "从收藏夹中移除" : "加入收藏夹";
            this.BtnFolderMods.Visibility = PageVersionLeft.Version.Modable ? Visibility.Visible : Visibility.Collapsed;
            // 刷新版本显示
            this.PanDisplayItem.Children.Clear();
            ItemVersion = PageSelectRight.McVersionListItem(PageVersionLeft.Version);
            ItemVersion.IsHitTestVisible = false;
            this.PanDisplayItem.Children.Add(ItemVersion);
            ModMain.FrmMain.PageNameRefresh();
            // 刷新版本图标
            this.ComboDisplayLogo.SelectedIndex = 0;
            string Logo = ModBase.ReadIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "Logo", "");
            bool LogoCustom = Conversions.ToBoolean(ModBase.ReadIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "LogoCustom", "False"));
            if (LogoCustom)
            {
                foreach (MyComboBoxItem Selection in this.ComboDisplayLogo.Items)
                {
                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(Selection.Tag, Logo, false)) || Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(Selection.Tag, @"PCL\Logo.png", false)) && Logo.EndsWith(@"PCL\Logo.png"))
                    {
                        this.ComboDisplayLogo.SelectedItem = Selection;
                        break;
                    }
                }
            }

            ModAnimation.AniControlEnabled -= 1;
        }

        #region 卡片：个性化

        // 版本分类
        private void ComboDisplayType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(IsLoad && ModAnimation.AniControlEnabled == 0))
                return;
            if (this.ComboDisplayType.SelectedIndex != 1)
            {
                // 改为不隐藏
                try
                {
                    // 若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                    ModBase.WriteIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "DisplayType", this.ComboDisplayType.SelectedIndex.ToString());
                    PageVersionLeft.Version.DisplayType = (ModMinecraft.McVersionCardType)Conversions.ToInteger(ModBase.ReadIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "DisplayType", ((int)ModMinecraft.McVersionCardType.Auto).ToString()));
                    ModMain.FrmVersionLeft.RefreshModDisabled();

                    ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "VersionCache", ""); // 要求刷新缓存
                    ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "修改版本分类失败（" + PageVersionLeft.Version.Name + "）", ModBase.LogLevel.Feedback);
                }
                Reload(); // 更新 “打开 Mod 文件夹” 按钮
            }
            else
            {
                // 改为隐藏
                try
                {
                    if (Conversions.ToBoolean(!ModBase.Setup.Get("HintHide")))
                    {
                        if (ModMain.MyMsgBox("确认要从版本列表中隐藏该版本吗？隐藏该版本后，它将不再出现于 PCL 显示的版本列表中。" + Constants.vbCrLf + "此后，在版本列表页面按下 F11 才可以查看被隐藏的版本。", "隐藏版本提示", Button2: "取消") != 1)
                        {
                            this.ComboDisplayType.SelectedIndex = 0;
                            return;
                        }
                        ModBase.Setup.Set("HintHide", true);
                    }
                    ModBase.WriteIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "DisplayType", ((int)ModMinecraft.McVersionCardType.Hidden).ToString());
                    ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "VersionCache", ""); // 要求刷新缓存
                    ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "隐藏版本 " + PageVersionLeft.Version.Name + " 失败", ModBase.LogLevel.Feedback);
                }
            }
        }

        // 更改描述
        private void BtnDisplayDesc_Click(object sender, EventArgs e)
        {
            try
            {
                string OldInfo = ModBase.ReadIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "CustomInfo");
                string NewInfo = ModMain.MyMsgBoxInput("更改描述", "修改版本的描述文本，留空则使用 PCL 的默认描述。", OldInfo, new System.Collections.ObjectModel.Collection<ValidateType>(), "默认描述");
                if (NewInfo is not null && (OldInfo ?? "") != (NewInfo ?? ""))
                    ModBase.WriteIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "CustomInfo", NewInfo);
                PageVersionLeft.Version = new ModMinecraft.McVersion(PageVersionLeft.Version.Name).Load();
                Reload();
                ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "版本 " + PageVersionLeft.Version.Name + " 描述更改失败", ModBase.LogLevel.Msgbox);
            }
        }

        // 重命名版本
        private void BtnDisplayRename_Click(object sender, EventArgs e)
        {
            try
            {
                // 确认输入的新名称
                string OldName = PageVersionLeft.Version.Name;
                string OldPath = PageVersionLeft.Version.Path;
                // 修改此部分的同时修改快速安装的版本名检测*
                string NewName = ModMain.MyMsgBoxInput("重命名版本", "", OldName, new System.Collections.ObjectModel.Collection<ValidateType>() { new ValidateFolderName(ModMinecraft.PathMcFolder + "versions", IgnoreCase: false) });
                if (string.IsNullOrWhiteSpace(NewName))
                    return;
                string NewPath = ModMinecraft.PathMcFolder + @"versions\" + NewName + @"\";
                // 获取临时中间名，以防止仅修改大小写的重命名失败
                string TempName = NewName + "_temp";
                string TempPath = ModMinecraft.PathMcFolder + @"versions\" + TempName + @"\";
                bool IsCaseChangedOnly = (NewName.ToLower() ?? "") == (OldName.ToLower() ?? "");
                // 重新加载版本 Json 信息，避免 HMCL 项被合并
                JObject JsonObject;
                try
                {
                    JsonObject = (JObject)ModBase.GetJson(ModBase.ReadFile(PageVersionLeft.Version.Path + PageVersionLeft.Version.Name + ".json"));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "重命名读取 Json 时失败");
                    JsonObject = PageVersionLeft.Version.JsonObject;
                }
                // 重命名主文件夹
                My.MyWpfExtension.Computer.FileSystem.RenameDirectory(OldPath, TempName);
                My.MyWpfExtension.Computer.FileSystem.RenameDirectory(TempPath, NewName);
                // 清理 ini 缓存
                ModBase.IniClearCache(PageVersionLeft.Version.Path + @"PCL\Setup.ini");
                // 遍历重命名所有文件与文件夹
                foreach (DirectoryInfo Entry in new DirectoryInfo(NewPath).EnumerateDirectories())
                {
                    if (!Entry.Name.Contains(OldName))
                        continue;
                    if (IsCaseChangedOnly)
                    {
                        My.MyWpfExtension.Computer.FileSystem.RenameDirectory(Entry.FullName, Entry.Name + "_temp");
                        My.MyWpfExtension.Computer.FileSystem.RenameDirectory(Entry.FullName + "_temp", Entry.Name.Replace(OldName, NewName));
                    }
                    else
                    {
                        ModBase.DeleteDirectory(NewPath + Entry.Name.Replace(OldName, NewName));
                        My.MyWpfExtension.Computer.FileSystem.RenameDirectory(Entry.FullName, Entry.Name.Replace(OldName, NewName));
                    }
                }
                foreach (FileInfo Entry in new DirectoryInfo(NewPath).EnumerateFiles())
                {
                    if (!Entry.Name.Contains(OldName))
                        continue;
                    if (IsCaseChangedOnly)
                    {
                        My.MyWpfExtension.Computer.FileSystem.RenameFile(Entry.FullName, Entry.Name + "_temp");
                        My.MyWpfExtension.Computer.FileSystem.RenameFile(Entry.FullName + "_temp", Entry.Name.Replace(OldName, NewName));
                    }
                    else
                    {
                        if (File.Exists(NewPath + Entry.Name.Replace(OldName, NewName)))
                            File.Delete(NewPath + Entry.Name.Replace(OldName, NewName));
                        My.MyWpfExtension.Computer.FileSystem.RenameFile(Entry.FullName, Entry.Name.Replace(OldName, NewName));
                    }
                }
                // 替换版本设置文件中的路径
                if (File.Exists(NewPath + @"PCL\Setup.ini"))
                {
                    ModBase.WriteFile(NewPath + @"PCL\Setup.ini", ModBase.ReadFile(NewPath + @"PCL\Setup.ini").Replace(OldPath, NewPath));
                }
                // 更改已选中的版本
                if ((ModBase.ReadIni(ModMinecraft.PathMcFolder + "PCL.ini", "Version") ?? "") == (OldName ?? ""))
                {
                    ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "Version", NewName);
                }
                // 更改版本 Json
                if (File.Exists(NewPath + NewName + ".json"))
                {
                    try
                    {
                        JsonObject["id"] = NewName;
                        ModBase.WriteFile(NewPath + NewName + ".json", JsonObject.ToString());
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "重命名版本 Json 失败");
                    }
                }
                // 刷新与提示
                ModMain.Hint("重命名成功！", ModMain.HintType.Finish);
                PageVersionLeft.Version = new ModMinecraft.McVersion(NewName).Load();
                if (!(ModMinecraft.McVersionCurrent == null) && ModMinecraft.McVersionCurrent.Equals(PageVersionLeft.Version))
                    ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "Version", NewName);
                Reload();
                ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重命名版本失败", ModBase.LogLevel.Msgbox);
            }
        }

        // 版本图标
        private void ComboDisplayLogo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(IsLoad && ModAnimation.AniControlEnabled == 0))
                return;
            // 选择 自定义 时修改图片
            try
            {
                if (object.ReferenceEquals(this.ComboDisplayLogo.SelectedItem, this.ItemDisplayLogoCustom))
                {
                    string FileName = ModBase.SelectFile("常用图片文件(*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif", "选择图片");
                    if (string.IsNullOrEmpty(FileName))
                    {
                        Reload(); // 还原选项
                        return;
                    }
                    File.Delete(PageVersionLeft.Version.Path + @"PCL\Logo.png");
                    Directory.CreateDirectory(PageVersionLeft.Version.Path + "PCL"); // 虽然不知道为啥，有时候真没这文件夹
                    ModBase.CopyFile(FileName, PageVersionLeft.Version.Path + @"PCL\Logo.png");
                }
                else
                {
                    File.Delete(PageVersionLeft.Version.Path + @"PCL\Logo.png");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "更改自定义版本图标失败（" + PageVersionLeft.Version.Name + "）", ModBase.LogLevel.Feedback);
            }
            // 进行更改
            try
            {
                string NewLogo = Conversions.ToString(((dynamic)this.ComboDisplayLogo.SelectedItem).Tag);
                ModBase.WriteIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "Logo", NewLogo);
                ModBase.WriteIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "LogoCustom", Conversions.ToString(!string.IsNullOrEmpty(NewLogo)));
                // 刷新显示
                ModBase.WriteIni(ModMinecraft.PathMcFolder + "PCL.ini", "VersionCache", ""); // 要求刷新缓存
                PageVersionLeft.Version = new ModMinecraft.McVersion(PageVersionLeft.Version.Name).Load();
                Reload();
                ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "更改版本图标失败（" + PageVersionLeft.Version.Name + "）", ModBase.LogLevel.Feedback);
            }
        }

        // 收藏夹
        private void BtnDisplayStar_Click(object sender, EventArgs e)
        {
            try
            {
                ModBase.WriteIni(PageVersionLeft.Version.Path + @"PCL\Setup.ini", "IsStar", Conversions.ToString(!PageVersionLeft.Version.IsStar));
                PageVersionLeft.Version = new ModMinecraft.McVersion(PageVersionLeft.Version.Name).Load();
                Reload();
                ModMinecraft.McVersionListForceRefresh = true;
                ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "版本 " + PageVersionLeft.Version.Name + " 收藏状态更改失败", ModBase.LogLevel.Msgbox);
            }
        }

        #endregion

        #region 卡片：快捷方式

        // 版本文件夹
        private void BtnFolderVersion_Click()
        {
            OpenVersionFolder(PageVersionLeft.Version);
        }
        public static void OpenVersionFolder(ModMinecraft.McVersion Version)
        {
            ModBase.OpenExplorer(Version.Path);
        }

        // 存档文件夹
        private void BtnFolderSaves_Click()
        {
            string FolderPath = PageVersionLeft.Version.PathIndie + @"saves\";
            Directory.CreateDirectory(FolderPath);
            ModBase.OpenExplorer(FolderPath);
        }

        // Mod 文件夹
        private void BtnFolderMods_Click()
        {
            string FolderPath = PageVersionLeft.Version.PathIndie + @"mods\";
            Directory.CreateDirectory(FolderPath);
            ModBase.OpenExplorer(FolderPath);
        }

        #endregion

        #region 卡片：管理

        // 导出启动脚本
        private void BtnManageScript_Click()
        {
            try
            {
                // 弹窗要求指定脚本的保存位置
                string SavePath = ModBase.SelectSaveFile("选择脚本保存位置", "启动 " + PageVersionLeft.Version.Name + ".bat", "批处理文件(*.bat)|*.bat");
                if (string.IsNullOrEmpty(SavePath))
                    return;
                // 检查中断（等玩家选完弹窗指不定任务就结束了呢……）
                if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
                {
                    ModMain.Hint("请在当前启动任务结束后再试！", ModMain.HintType.Critical);
                    return;
                }
                // 生成脚本
                if (ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions() { SaveBatch = SavePath, Version = PageVersionLeft.Version }))
                {
                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginType"), ModLaunch.McLoginType.Legacy, false)))
                    {
                        ModMain.Hint("正在导出启动脚本……");
                    }
                    else
                    {
                        ModMain.Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）");
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "导出启动脚本失败（" + PageVersionLeft.Version.Name + "）", ModBase.LogLevel.Msgbox);
            }
        }

        // 补全文件
        private void BtnManageCheck_Click(object sender, EventArgs e)
        {
            try
            {
                // 忽略文件检查提示
                if (Conversions.ToBoolean(ModMinecraft.ShouldIgnoreFileCheck(PageVersionLeft.Version)))
                {
                    ModMain.Hint("请先关闭 [版本设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！", ModMain.HintType.Info);
                    return;
                }
                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
                {
                    if ((OngoingLoader.Name ?? "") != (PageVersionLeft.Version.Name + " 文件补全" ?? ""))
                        continue;
                    ModMain.Hint("正在处理中，请稍候！", ModMain.HintType.Critical);
                    return;
                }
                // 启动
                var Loader = new ModLoader.LoaderCombo<string>(PageVersionLeft.Version.Name + " 文件补全", ModDownload.DlClientFix(PageVersionLeft.Version, true, ModDownload.AssetsIndexExistsBehaviour.AlwaysDownload));
                Loader.OnStateChanged = new Action(() => { switch (Loader.State) { case ModBase.LoadState.Finished: { ModMain.Hint(Loader.Name + "成功！", ModMain.HintType.Finish); break; } case ModBase.LoadState.Failed: { ModMain.Hint(Loader.Name + "失败：" + ModBase.GetExceptionSummary(Loader.Error), ModMain.HintType.Critical); break; } case ModBase.LoadState.Aborted: { ModMain.Hint(Loader.Name + "已取消！", ModMain.HintType.Info); break; } } });
                Loader.Start(PageVersionLeft.Version.Name);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "尝试补全文件失败（" + PageVersionLeft.Version.Name + "）", ModBase.LogLevel.Msgbox);
            }
        }

        // 重置
        private void BtnManageRestore_Click(object sender, EventArgs e)
        {
            try
            {
                var CurrentVersion = PageVersionLeft.Version.Version;
                if (!(CurrentVersion.McCodeMain == 99) && ModMinecraft.VersionSortInteger(CurrentVersion.McName, "1.5.2") == -1 && CurrentVersion.HasForge)
                {
                    ModMain.Hint("该版本暂不支持重置！", ModMain.HintType.Info);
                    return;
                }
                // 确认操作
                if (ModMain.MyMsgBox("你确定要重置版本 " + PageVersionLeft.Version.Name + " 吗？" + Constants.vbCrLf + "PCL 将会尝试重新从互联网获取此版本的资源文件信息，并重新执行自动安装。" + Constants.vbCrLf + Constants.vbCrLf + "本功能尚处于测试阶段，可能不稳定。", "版本重置确认", "确认", "取消") == 2)
                    return;

                // 备份版本核心文件
                ModBase.CopyFile(PageVersionLeft.Version.Path + PageVersionLeft.Version.Name + ".json", PageVersionLeft.Version.Path + @"PCLInstallBackups\" + PageVersionLeft.Version.Name + ".json");
                ModBase.CopyFile(PageVersionLeft.Version.Path + PageVersionLeft.Version.Name + ".jar", PageVersionLeft.Version.Path + @"PCLInstallBackups\" + PageVersionLeft.Version.Name + ".jar");
                // 提交安装申请
                var Request = new ModDownloadLib.McInstallRequest()
                {
                    TargetVersionName = PageVersionLeft.Version.Name,
                    TargetVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{PageVersionLeft.Version.Name}\",
                    MinecraftName = CurrentVersion.McName,
                    OptiFineEntry = CurrentVersion.HasOptiFine ? new ModDownload.DlOptiFineListEntry() { Inherit = CurrentVersion.McName, NameDisplay = CurrentVersion.McName + " " + CurrentVersion.OptiFineVersion } : null,
                    ForgeEntry = CurrentVersion.HasForge ? new ModDownload.DlForgeVersionEntry(CurrentVersion.ForgeVersion, null, Inherit: CurrentVersion.McName) { Category = "installer" } : null,
                    NeoForgeEntry = CurrentVersion.HasNeoForge ? new ModDownload.DlNeoForgeListEntry(CurrentVersion.NeoForgeVersion) { ForgeType = (ModDownload.DlForgelikeEntry.ForgelikeType)1, VersionName = CurrentVersion.NeoForgeVersion, Inherit = CurrentVersion.McName } : null,
                    CleanroomEntry = CurrentVersion.HasCleanroom ? new ModDownload.DlCleanroomListEntry(CurrentVersion.CleanroomVersion) { ForgeType = (ModDownload.DlForgelikeEntry.ForgelikeType)2, VersionName = CurrentVersion.CleanroomVersion, Inherit = CurrentVersion.McName } : null,
                    FabricVersion = CurrentVersion.HasFabric ? CurrentVersion.FabricVersion : null,
                    QuiltVersion = CurrentVersion.HasQuilt ? CurrentVersion.QuiltVersion : null,
                    LiteLoaderEntry = CurrentVersion.HasLiteLoader ? new ModDownload.DlLiteLoaderListEntry() { Inherit = CurrentVersion.McName } : null
                };
                // .MinecraftJson = CurrentVersion.McName,
                if (!ModDownloadLib.McInstall(Request, "重置"))
                    return;
                ModMain.FrmMain.PageChange(new FormMain.PageStackData() { Page = FormMain.PageType.Launch });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重置版本 " + PageVersionLeft.Version.Name + " 失败", ModBase.LogLevel.Msgbox);
            }
        }

        // 测试游戏
        private void BtnManageTest_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions() { Version = PageVersionLeft.Version, Test = true });
                ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Launch);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "测试游戏失败", ModBase.LogLevel.Feedback);
            }
        }

        // 删除版本
        // 修改此代码时，同时修改 PageSelectRight 中的代码
        private void BtnManageDelete_Click(object sender, EventArgs e)
        {
            try
            {
                bool IsShiftPressed = My.MyWpfExtension.Computer.Keyboard.ShiftKeyDown;
                bool IsHintIndie = PageVersionLeft.Version.State != ModMinecraft.McVersionState.Error && (PageVersionLeft.Version.PathIndie ?? "") != (ModMinecraft.PathMcFolder ?? "");
                switch (ModMain.MyMsgBox($"你确定要{(IsShiftPressed ? "永久" : "")}删除版本 {PageVersionLeft.Version.Name} 吗？" + (IsHintIndie ? Constants.vbCrLf + "由于该版本开启了版本隔离，删除版本时该版本对应的存档、资源包、Mod 等文件也将被一并删除！" : ""), "版本删除确认", Button2: "取消", IsWarn: IsHintIndie || IsShiftPressed))
                {
                    case 1:
                        {
                            ModBase.IniClearCache(PageVersionLeft.Version.Path + @"PCL\Setup.ini");
                            if (IsShiftPressed)
                            {
                                ModBase.DeleteDirectory(PageVersionLeft.Version.Path);
                                ModMain.Hint("版本 " + PageVersionLeft.Version.Name + " 已永久删除！", ModMain.HintType.Finish);
                            }
                            else
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(PageVersionLeft.Version.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                                ModMain.Hint("版本 " + PageVersionLeft.Version.Name + " 已删除到回收站！", ModMain.HintType.Finish);
                            }

                            break;
                        }
                    case 2:
                        {
                            return;
                        }
                }
                ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                ModMain.FrmMain.PageBack();
            }
            catch (OperationCanceledException ex)
            {
                ModBase.Log(ex, "删除版本 " + PageVersionLeft.Version.Name + " 被主动取消");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除版本 " + PageVersionLeft.Version.Name + " 失败", ModBase.LogLevel.Msgbox);
            }
        }

        #endregion

    }
}