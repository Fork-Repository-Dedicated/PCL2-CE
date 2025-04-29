using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageVersionCompResource : IRefreshable
    {
        #region 初始化

        private ModComp.CompType CurrentCompType = ModComp.CompType.Mod;

        private MyLocalCompItem.SwipeSelect CurrentSwipSelect;

        public PageVersionCompResource(ModComp.CompType LoadCompType)
        {
            CurrentCompType = LoadCompType;
            CurrentSwipSelect = new MyLocalCompItem.SwipeSelect() { TargetFrm = this };

            // 此调用是设计器所必需的。
            this.InitializeComponent();

            // 在 InitializeComponent() 调用之后添加任何初始化。

            if (new[] { ModComp.CompType.Shader, ModComp.CompType.ResourcePack }.Contains(CurrentCompType))
            {
                this.BtnSelectEnable.Visibility = Visibility.Collapsed;
                this.BtnSelectDisable.Visibility = Visibility.Collapsed;
            }

            this.Loaded += (_, __) => PageOther_Loaded();
            this.Initialized += (_, __) => LoaderInit();
            this.PageExit += UnselectedAllWithAnimation;
            this.KeyDown += PageVersionMod_KeyDown;

        }

        private ModLocalComp.CompLocalLoaderData GetRequireLoaderData()
        {
            var res = new ModLocalComp.CompLocalLoaderData();
            res.GameVersion = PageVersionLeft.Version;
            res.Frm = this;
            var RequireLoaders = new List<ModComp.CompLoaderType>();
            switch (CurrentCompType)
            {
                case ModComp.CompType.Mod:
                    {
                        RequireLoaders = ModLocalComp.GetCurrentVersionModLoader();
                        break;
                    }
                case ModComp.CompType.ResourcePack:
                    {
                        RequireLoaders = new[] { ModComp.CompLoaderType.Minecraft }.ToList();
                        break;
                    }
                case ModComp.CompType.Shader:
                    {
                        RequireLoaders = new[] { ModComp.CompLoaderType.OptiFine, ModComp.CompLoaderType.Iris, ModComp.CompLoaderType.Vanilla, ModComp.CompLoaderType.Canvas }.ToList();
                        break;
                    }
            }
            res.Loaders = RequireLoaders;
            res.CompPath = PageVersionLeft.Version.PathIndie + ModLocalComp.GetPathNameByCompType(CurrentCompType) + @"\";
            return res;
        }

        private bool IsLoad = false;
        public void PageOther_Loaded()
        {

            if (ModMain.FrmMain.PageLast.Page != FormMain.PageType.CompDetail)
                this.PanBack.ScrollToHome();
            ModAnimation.AniControlEnabled += 1;
            SelectedMods.Clear();
            ReloadCompFileList();
            ChangeAllSelected(false);
            ModAnimation.AniControlEnabled -= 1;

            // 非重复加载部分
            if (IsLoad)
                return;
            IsLoad = true;

            // 调整按钮边距（这玩意儿没法从 XAML 改）
            foreach (MyRadioButton Btn in this.PanFilter.Children)
                Btn.LabText.Margin = new Thickness(-2, 0d, 8d, 0d);

            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If DEBUG Then
            *//* TODO ERROR: Skipped DisabledTextTrivia
                    BtnManageCheck.Visibility = Visibility.Visible
            *//* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
        }
        /// <summary>
    /// 刷新 Mod 列表。
    /// </summary>
        public void ReloadCompFileList(bool ForceReload = false)
        {
            if (LoaderRun(ForceReload ? ModLoader.LoaderFolderRunType.ForceRun : ModLoader.LoaderFolderRunType.RunOnUpdated))
            {
                ModBase.Log($"[System] 已刷新 {CurrentCompType} 列表");
                Filter = FilterType.All;
                this.PanBack.ScrollToHome();
                this.SearchBox.Text = "";
            }
        }
        // 强制刷新
        private void RefreshSelf()
        {
            Refresh(CurrentCompType);
        }

        void IRefreshable.Refresh() => RefreshSelf();
        public static void Refresh(ModComp.CompType WhichPage)
        {
            // 强制刷新
            try
            {
                ModComp.CompProjectCache.Clear();
                ModComp.CompFilesCache.Clear();
                File.Delete(ModBase.PathTemp + @"Cache\LocalComp.json");
                ModBase.Log("[CompResource] 由于点击刷新按钮，清理本地工程信息缓存");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "强制刷新时清理本地工程信息缓存失败");
            }
            switch (WhichPage)
            {
                case ModComp.CompType.Mod:
                    {
                        if (ModMain.FrmVersionMod is not null)
                            ModMain.FrmVersionMod.ReloadCompFileList(true); // 无需 Else，还没加载刷个鬼的新
                        ModMain.FrmVersionLeft.ItemMod.Checked = true;
                        break;
                    }
                case ModComp.CompType.ResourcePack:
                    {
                        if (ModMain.FrmVersionResourcePack is not null)
                            ModMain.FrmVersionResourcePack.ReloadCompFileList(true);
                        ModMain.FrmVersionLeft.ItemResourcePack.Checked = true;
                        break;
                    }
                case ModComp.CompType.Shader:
                    {
                        if (ModMain.FrmVersionShader is not null)
                            ModMain.FrmVersionShader.ReloadCompFileList(true);
                        ModMain.FrmVersionLeft.ItemShader.Checked = true;
                        break;
                    }
            }
            ModMain.Hint("正在刷新……", Log: false);
        }

        private void LoaderInit()
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanAllBack, (FrameworkElement)null, ModLocalComp.CompResourceListLoader, (_) => LoadUIFromLoaderOutput(), () => CurrentCompType, AutoRun: false);
        }
        private void Load_Click(object sender, MouseButtonEventArgs e)
        {
            if (ModLocalComp.CompResourceListLoader.State == ModBase.LoadState.Failed)
            {
                LoaderRun(ModLoader.LoaderFolderRunType.ForceRun);
            }
        }
        public bool LoaderRun(ModLoader.LoaderFolderRunType Type)
        {
            string CompResourcePath = PageVersionLeft.Version.PathIndie + ModLocalComp.GetPathNameByCompType(CurrentCompType) + @"\";
            return ModLoader.LoaderFolderRun(ModLocalComp.CompResourceListLoader, CompResourcePath, Type, LoaderInput: GetRequireLoaderData());
        }

        #endregion

        #region UI 化

        /// <summary>
    /// 已加载的 Mod UI 缓存，不确保按显示顺序排列。Key 为 Mod 的 RawFileName。
    /// </summary>
        public Dictionary<string, MyLocalCompItem> ModItems = new Dictionary<string, MyLocalCompItem>();
        /// <summary>
    /// 将加载器结果的 Mod 列表加载为 UI。
    /// </summary>
        private void LoadUIFromLoaderOutput()
        {
            try
            {
                // 判断应该显示哪一个页面
                if (ModLocalComp.CompResourceListLoader.Output.Any())
                {
                    this.PanBack.Visibility = Visibility.Visible;
                    this.PanEmpty.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.PanEmpty.Visibility = Visibility.Visible;
                    this.PanBack.Visibility = Visibility.Collapsed;
                    return;
                }
                // 修改缓存
                ModItems.Clear();
                foreach (ModLocalComp.LocalCompFile ModEntity in ModLocalComp.CompResourceListLoader.Output)
                    ModItems[ModEntity.RawFileName] = BuildLocalCompItem(ModEntity);
                // 显示结果
                Filter = FilterType.All;
                this.SearchBox.Text = ""; // 这会触发结果刷新，所以需要在 ModItems 更新之后，详见 #3124 的视频
                RefreshUI();
                SetSortMethod(SortMethod.ModName);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"加载 {CurrentCompType} 列表 UI 失败", ModBase.LogLevel.Feedback);
            }
        }
        private MyLocalCompItem BuildLocalCompItem(ModLocalComp.LocalCompFile Entry)
        {
            ModAnimation.AniControlEnabled += 1;
            var NewItem = new MyLocalCompItem()
            {
                SnapsToDevicePixels = true,
                Entry = Entry,
                ButtonHandler = BuildLocalCompItemBtnHandler,
                Checked = SelectedMods.Contains(Entry.RawFileName)
            };
            NewItem.CurrentSwipe = CurrentSwipSelect;
            Entry.OnCompUpdate += (_) => NewItem.Refresh();
            // AddHandler Entry.OnCompUpdate, Sub() RunInUi(Sub() DoSort())
            NewItem.Refresh();
            ModAnimation.AniControlEnabled -= 1;
            return NewItem;
        }
        private void BuildLocalCompItemBtnHandler(MyLocalCompItem sender, EventArgs e)
        {
            // 点击事件
            sender.Changed += (_, __) => this.CheckChanged();
            sender.Click += (MyLocalCompItem ss, EventArgs ee) => ss.Checked = !ss.Checked;
            // 图标按钮
            var BtnOpen = new MyIconButton() { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonOpen, Tag = sender };
            BtnOpen.ToolTip = "打开文件位置";
            ToolTipService.SetPlacement(BtnOpen, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnOpen, 30d);
            ToolTipService.SetHorizontalOffset(BtnOpen, 2d);
            BtnOpen.Click += (_, __) => this.Open_Click();
            var BtnCont = new MyIconButton() { LogoScale = 1d, Logo = ModBase.Logo.IconButtonInfo, Tag = sender };
            BtnCont.ToolTip = "详情";
            ToolTipService.SetPlacement(BtnCont, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnCont, 30d);
            ToolTipService.SetHorizontalOffset(BtnCont, 2d);
            BtnCont.Click += Info_Click;
            sender.MouseRightButtonUp += this.Info_Click;
            var BtnDelete = new MyIconButton() { LogoScale = 1d, Logo = ModBase.Logo.IconButtonDelete, Tag = sender };
            BtnDelete.ToolTip = "删除";
            ToolTipService.SetPlacement(BtnDelete, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnDelete, 30d);
            ToolTipService.SetHorizontalOffset(BtnDelete, 2d);
            BtnDelete.Click += (_, __) => this.Delete_Click();
            if (CurrentCompType != ModComp.CompType.Mod || sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable)
            {
                sender.Buttons = new[] { BtnCont, BtnOpen, BtnDelete };
            }
            else
            {
                var BtnED = new MyIconButton() { LogoScale = 1d, Logo = sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine ? ModBase.Logo.IconButtonStop : ModBase.Logo.IconButtonCheck, Tag = sender };
                BtnED.ToolTip = sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine ? "禁用" : "启用";
                ToolTipService.SetPlacement(BtnED, System.Windows.Controls.Primitives.PlacementMode.Center);
                ToolTipService.SetVerticalOffset(BtnED, 30d);
                ToolTipService.SetHorizontalOffset(BtnED, 2d);
                BtnED.Click += (_, __) => this.ED_Click();
                sender.Buttons = new[] { BtnCont, BtnOpen, BtnED, BtnDelete };
            }
        }

        /// <summary>
    /// 刷新整个 UI。
    /// </summary>
        public void RefreshUI()
        {
            if (this.PanList is null)
                return;
            var ShowingMods = (IsSearching ? SearchResult : ModLocalComp.CompResourceListLoader.Output ?? new List<ModLocalComp.LocalCompFile>()).Where(m => CanPassFilter(m)).ToList();
            // 重新列出列表
            ModAnimation.AniControlEnabled += 1;
            if (ShowingMods.Any())
            {
                this.PanList.Visibility = Visibility.Visible;
                this.PanList.Children.Clear();
                foreach (var TargetMod in ShowingMods)
                {
                    var Item = ModItems[TargetMod.RawFileName];
                    Item.Checked = SelectedMods.Contains(TargetMod.RawFileName); // 更新选中状态
                    this.PanList.Children.Add(Item);
                }
            }
            else
            {
                this.PanList.Visibility = Visibility.Collapsed;
            }
            ModAnimation.AniControlEnabled -= 1;
            SelectedMods = SelectedMods.Where(m => ShowingMods.Any(s => (s.RawFileName ?? "") == (m ?? ""))).ToList(); // 取消选中已经不显示的 Mod
            RefreshBars();
        }

        /// <summary>
    /// 刷新顶栏和底栏显示。
    /// </summary>
        public void RefreshBars()
        {
            // -----------------
            // 顶部栏
            // -----------------

            // 计数
            int AnyCount = 0;
            int EnabledCount = 0;
            int DisabledCount = 0;
            int UpdateCount = 0;
            int UnavalialeCount = 0;
            var ItemSource = IsSearching ? SearchResult : ModLocalComp.CompResourceListLoader.Output ?? new List<ModLocalComp.LocalCompFile>();
            foreach (var ModItem in ItemSource)
            {
                AnyCount += 1;
                if (ModItem.CanUpdate)
                    UpdateCount += 1;
                if (ModItem.State.Equals(ModLocalComp.LocalCompFile.LocalFileStatus.Fine))
                    EnabledCount += 1;
                if (ModItem.State.Equals(ModLocalComp.LocalCompFile.LocalFileStatus.Disabled))
                    DisabledCount += 1;
                if (ModItem.State.Equals(ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable))
                    UnavalialeCount += 1;
            }
            // 显示
            this.BtnFilterAll.Text = (IsSearching ? "搜索结果" : "全部") + $" ({AnyCount})";
            this.BtnFilterCanUpdate.Text = $"可更新 ({UpdateCount})";
            this.BtnFilterCanUpdate.Visibility = Filter == FilterType.CanUpdate || UpdateCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            this.BtnFilterEnabled.Text = $"启用 ({EnabledCount})";
            this.BtnFilterEnabled.Visibility = Filter == FilterType.Enabled || EnabledCount > 0 && EnabledCount < AnyCount ? Visibility.Visible : Visibility.Collapsed;
            this.BtnFilterDisabled.Text = $"禁用 ({DisabledCount})";
            this.BtnFilterDisabled.Visibility = Filter == FilterType.Disabled || DisabledCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            this.BtnFilterError.Text = $"错误 ({UnavalialeCount})";
            this.BtnFilterError.Visibility = Filter == FilterType.Unavailable || UnavalialeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            // 查找重复项目
            var DuplicateItems = ItemSource.GroupBy(m => { if (m.Comp is null) { return ":Nothing:"; } else { return m.Comp.Id; } }).Where(g => g.Count() > 1 && g.First().Comp is not null).SelectMany(g => g).ToList();
            this.BtnFilterDuplicate.Text = $"重复 ({DuplicateItems.Count})";
            this.BtnFilterDuplicate.Visibility = Filter == FilterType.Duplicate || DuplicateItems.Any() ? Visibility.Visible : Visibility.Collapsed;

            // -----------------
            // 底部栏
            // -----------------

            // 计数
            int NewCount = SelectedMods.Count;
            bool Selected = NewCount > 0;
            if (Selected)
                this.LabSelect.Text = $"已选择 {NewCount} 个文件"; // 取消所有选择时不更新数字
                                                             // 按钮可用性
            if (Selected)
            {
                bool HasUpdate = false;
                bool HasEnabled = false;
                bool HasDisabled = false;
                foreach (var ModEntity in ModLocalComp.CompResourceListLoader.Output)
                {
                    if (SelectedMods.Contains(ModEntity.RawFileName))
                    {
                        if (ModEntity.CanUpdate)
                            HasUpdate = true;
                        if (ModEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                        {
                            HasEnabled = true;
                        }
                        else if (ModEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
                        {
                            HasDisabled = true;
                        }
                    }
                }
                this.BtnSelectDisable.IsEnabled = HasEnabled;
                this.BtnSelectEnable.IsEnabled = HasDisabled;
                this.BtnSelectUpdate.IsEnabled = HasUpdate;
            }
            // 更新显示状态
            if (ModAnimation.AniControlEnabled == 0)
            {
                this.PanListBack.Margin = new Thickness(0d, 0d, 0d, Selected ? 95 : 15);
                if (Selected)
                {
                    // 仅在数量增加时播放出现/跳跃动画
                    if (BottomBarShownCount >= NewCount)
                    {
                        BottomBarShownCount = NewCount;
                        return;
                    }
                    else
                    {
                        BottomBarShownCount = NewCount;
                    }
                    // 出现/跳跃动画
                    this.CardSelect.Visibility = Visibility.Visible;
                    ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.CardSelect, 1d - this.CardSelect.Opacity, 60), ModAnimation.AaTranslateY(this.CardSelect, (double)-27 - this.TransSelect.Y, 120, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaTranslateY(this.CardSelect, 3d, 150, 120, Ease: new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaTranslateY(this.CardSelect, (double)-1, 90, 270, Ease: new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)) }, "Mod Sidebar");
                }
                else
                {
                    // 不重复播放隐藏动画
                    if (BottomBarShownCount == 0)
                        return;
                    BottomBarShownCount = 0;
                    // 隐藏动画
                    ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.CardSelect, -this.CardSelect.Opacity, 90), ModAnimation.AaTranslateY(this.CardSelect, (double)-10 - this.TransSelect.Y, 90, Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => this.CardSelect.Visibility = Visibility.Collapsed, After: true) }, "Mod Sidebar");
                }
            }
            else
            {
                ModAnimation.AniStop("Mod Sidebar");
                BottomBarShownCount = NewCount;
                if (Selected)
                {
                    this.CardSelect.Visibility = Visibility.Visible;
                    this.CardSelect.Opacity = 1d;
                    this.TransSelect.Y = (double)-25;
                }
                else
                {
                    this.CardSelect.Visibility = Visibility.Collapsed;
                    this.CardSelect.Opacity = 0d;
                    this.TransSelect.Y = (double)-10;
                }
            }
        }
        private int BottomBarShownCount = 0;

        #endregion

        #region 管理

        /// <summary>
    /// 打开 Mods 文件夹。
    /// </summary>
        private void BtnManageOpen_Click(object sender, EventArgs e)
        {
            try
            {
                string CompFilePath = PageVersionLeft.Version.PathIndie + ModLocalComp.GetPathNameByCompType(CurrentCompType) + @"\";
                Directory.CreateDirectory(CompFilePath);
                ModBase.OpenExplorer(CompFilePath);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "打开 Mods 文件夹失败", ModBase.LogLevel.Msgbox);
            }
        }

        /* TODO ERROR: Skipped IfDirectiveTrivia
        #If DEBUG Then
        *//* TODO ERROR: Skipped DisabledTextTrivia
            ''' <summary>
            ''' 检查 Mod。
            ''' </summary>
            Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
                Try
                    Dim Result = McModCheck(PageVersionLeft.Version, CompModLoader.Output)
                    If Result.Any Then
                        MyMsgBox(Join(Result, vbCrLf & vbCrLf), "Mod 检查结果")
                    Else
                        Hint("Mod 检查完成，未发现任何问题！", HintType.Finish)
                    End If
                Catch ex As Exception
                    Log(ex, "进行 Mod 检查时出错", LogLevel.Feedback)
                End Try
            End Sub
        *//* TODO ERROR: Skipped EndIfDirectiveTrivia
        #End If
        */
        /// <summary>
    /// 全选。
    /// </summary>
        private void BtnManageSelectAll_Click(object sender, MouseButtonEventArgs e)
        {
            ChangeAllSelected(SelectedMods.Count < this.PanList.Children.Count);
        }

        /// <summary>
    /// 安装 Mod。
    /// </summary>
        private void BtnManageInstall_Click(object sender, MouseButtonEventArgs e)
        {
            string[] FileList = null;
            switch (CurrentCompType)
            {
                case ModComp.CompType.Mod:
                    {
                        FileList = ModBase.SelectFiles("Mod 文件(*.jar;*.litemod;*.disabled;*.old)|*.jar;*.litemod;*.disabled;*.old", "选择要安装的 Mod");
                        break;
                    }
                case ModComp.CompType.ResourcePack:
                    {
                        FileList = ModBase.SelectFiles("资源包文件(*.zip)|*.zip", "选择要安装的资源包");
                        break;
                    }
                case ModComp.CompType.Shader:
                    {
                        FileList = ModBase.SelectFiles("光影包文件(*.zip)|*.zip", "选择要安装的光影包");
                        break;
                    }
            }
            if (FileList is null || !FileList.Any())
                return;
            InstallMods(FileList);
        }
        /// <summary>
    /// 尝试安装 Mod。
    /// 返回输入的文件是否为一个 Mod 文件，仅用于判断拖拽行为。
    /// </summary>
        public static bool InstallMods(IEnumerable<string> FilePathList)
        {
            string Extension = FilePathList.First().AfterLast(".").ToLower();
            // 检查文件扩展名
            if (!new[] { "jar", "litemod", "disabled", "old" }.Any(t => (t ?? "") == (Extension ?? "")))
                return false;
            ModBase.Log("[System] 文件为 jar/litemod 格式，尝试作为 Mod 安装");
            // 检查回收站：回收站中的文件有错误的文件名
            if (FilePathList.First().Contains(@":\$RECYCLE.BIN\"))
            {
                ModMain.Hint("请先将文件从回收站还原，再尝试安装！", ModMain.HintType.Critical);
                return true;
            }
            // 获取并检查目标版本
            var TargetVersion = ModMinecraft.McVersionCurrent;
            if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.VersionSetup)
                TargetVersion = PageVersionLeft.Version;
            if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.VersionSelect || TargetVersion is null || !TargetVersion.Modable)
            {
                // 正在选择版本，或当前版本不能安装 Mod
                ModMain.Hint("若要安装 Mod，请先选择一个可以安装 Mod 的版本！");
            }
            else if (!(ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.VersionSetup && ModMain.FrmMain.PageCurrentSub == FormMain.PageSubType.VersionMod))
            {
                // 未处于 Mod 管理页面
                if (ModMain.MyMsgBox($"是否要将这{(FilePathList.Count() == 1 ? "个" : "些")}文件作为 Mod 安装到 {TargetVersion.Name}？", "Mod 安装确认", "确定", "取消") == 1)
                    goto Install;
            }
            else
            {
            // 处于 Mod 管理页面
            Install:
                ;

                try
                {
                    foreach (var ModFile in FilePathList)
                    {
                        string NewFileName = ModBase.GetFileNameFromPath(ModFile).Replace(".disabled", "").Replace(".old", "");
                        if (!NewFileName.Contains("."))
                            NewFileName += ".jar"; // #4227
                        ModBase.CopyFile(ModFile, TargetVersion.PathIndie + @"mods\" + NewFileName);
                    }
                    if (FilePathList.Count() == 1)
                    {
                        ModMain.Hint($"已安装 {ModBase.GetFileNameFromPath(FilePathList.First()).Replace(".disabled", "").Replace(".old", "")}！", ModMain.HintType.Finish);
                    }
                    else
                    {
                        ModMain.Hint($"已安装 {FilePathList.Count()} 个 Mod！", ModMain.HintType.Finish);
                    }
                    // 刷新列表
                    if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.VersionSetup && ModMain.FrmMain.PageCurrentSub == FormMain.PageSubType.VersionMod)
                    {
                        ModLoader.LoaderFolderRun(ModLocalComp.CompResourceListLoader, TargetVersion.PathIndie + @"mods\", ModLoader.LoaderFolderRunType.ForceRun, LoaderInput: ModMain.FrmVersionMod?.GetRequireLoaderData());
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "复制 Mod 文件失败", ModBase.LogLevel.Msgbox);
                }
            }
            return true;
        }

        private void BtnManageInfoExport_Click(object sender, MouseButtonEventArgs e)
        {
            int Choice = ModMain.MyMsgBox("TXT 格式：仅导出当前的模组文件名称信息，通常足够他人获取已安装的模组信息" + Constants.vbCrLf + "CSV 格式：导出详细的模组信息，包括其文件名，Mod ID，文件内版本信息等详细信息", Title: "选择导出模式", Button1: "TXT 格式", Button2: "CSV 格式", Button3: "取消");
            void ExportText(string Content, string FileName) { try { string savePath = ModBase.SelectSaveFile("选择保存位置", FileName, "文本文件(*.txt)|*.txt|CSV 文件(*.csv)|*.csv"); if (string.IsNullOrWhiteSpace(savePath)) return; File.WriteAllText(savePath, Content, Encoding.UTF8); ModBase.OpenExplorer(savePath); } catch (Exception ex) { ModBase.Log(ex, "导出模组信息失败", ModBase.LogLevel.Msgbox); } };
            switch (Choice)
            {
                case 1: // TXT
                    {
                        var ExportContent = new List<string>();
                        foreach (var ModEntity in ModLocalComp.CompResourceListLoader.Output)
                            ExportContent.Add(ModEntity.FileName);
                        ExportText(ExportContent.Join(Constants.vbCrLf), PageVersionLeft.Version.Name + "已安装的模组信息.txt");
                        break;
                    }

                case 2: // CSV
                    {
                        var ExportContent = new List<string>();
                        ExportContent.Add("文件名,Mod 名称,Mod 版本,此版本更新时间,Mod ID,Mod 平台工程 ID,Mod 文件大小（字节）,Mod 文件路径");
                        foreach (var ModEntity in ModLocalComp.CompResourceListLoader.Output)
                            ExportContent.Add($"{ModEntity.FileName},{ModEntity.Comp?.TranslatedName},{ModEntity.Version},{ModEntity.CompFile?.ReleaseDate},{ModEntity.ModId},{ModEntity.Comp?.Id},{new FileInfo(ModEntity.Path).Length},{ModEntity.Path}");
                        ExportText(ExportContent.Join(Constants.vbCrLf), PageVersionLeft.Version.Name + "已安装的模组信息.csv");
                        break;
                    }

            }
        }

        /// <summary>
    /// 下载 Mod。
    /// </summary>
        private void BtnManageDownload_Click(object sender, MouseButtonEventArgs e)
        {
            PageDownloadMod.TargetVersion = PageVersionLeft.Version; // 将当前版本设置为筛选器
            switch (CurrentCompType)
            {
                case ModComp.CompType.Mod:
                    {
                        ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Download, FormMain.PageSubType.DownloadMod);
                        break;
                    }
                case ModComp.CompType.ResourcePack:
                    {
                        ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Download, FormMain.PageSubType.DownloadResourcePack);
                        break;
                    }
                case ModComp.CompType.Shader:
                    {
                        ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Download, FormMain.PageSubType.DownloadShader);
                        break;
                    }
            }
        }

        #endregion

        #region 选择

        /// <summary>
    /// 选择的 Mod 的路径（不含 .disabled 和 .old）。
    /// </summary>
        public List<string> SelectedMods = new List<string>();

        // 单项切换选择状态
        public void CheckChanged(MyLocalCompItem sender, ModBase.RouteEventArgs e)
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            // 更新选择了的内容
            string SelectedKey = sender.Entry.RawFileName;
            if (sender.Checked)
            {
                if (!SelectedMods.Contains(SelectedKey))
                    SelectedMods.Add(SelectedKey);
            }
            else
            {
                SelectedMods.Remove(SelectedKey);
            }
            RefreshBars();
        }

        // 切换所有项的选择状态
        private void ChangeAllSelected(bool Value)
        {
            ModAnimation.AniControlEnabled += 1;
            SelectedMods.Clear();
            foreach (MyLocalCompItem Item in ModItems.Values)
            {
                // #4992，Mod 从过滤器看可能不应在列表中，但因为刚切换状态所以依然保留在列表中，所以应该从列表 UI 判断，而非从过滤器判断
                bool ShouldSelected = Value && this.PanList.Children.Contains(Item);
                Item.Checked = ShouldSelected;
                if (ShouldSelected)
                    SelectedMods.Add(Item.Entry.RawFileName);
            }
            ModAnimation.AniControlEnabled -= 1;
            RefreshBars();
        }
        private void UnselectedAllWithAnimation()
        {
            int CacheAniControlEnabled = ModAnimation.AniControlEnabled;
            ModAnimation.AniControlEnabled = 0;
            ChangeAllSelected(false);
            ModAnimation.AniControlEnabled += CacheAniControlEnabled;
        }
        private void PageVersionMod_KeyDown(object sender, KeyEventArgs e)
        {
            if (My.MyWpfExtension.Computer.Keyboard.CtrlKeyDown && e.Key == Key.A)
                ChangeAllSelected(true);
        }

        #endregion

        #region 筛选

        private FilterType _Filter = FilterType.All;
        private FilterType Filter
        {
            get
            {
                return _Filter;
            }
            set
            {
                if (_Filter == value)
                    return;
                _Filter = value;
                switch (value)
                {
                    case FilterType.All:
                        {
                            this.BtnFilterAll.Checked = true;
                            break;
                        }
                    case FilterType.Enabled:
                        {
                            this.BtnFilterEnabled.Checked = true;
                            break;
                        }
                    case FilterType.Disabled:
                        {
                            this.BtnFilterDisabled.Checked = true;
                            break;
                        }
                    case FilterType.CanUpdate:
                        {
                            this.BtnFilterCanUpdate.Checked = true;
                            break;
                        }
                    case FilterType.Duplicate:
                        {
                            this.BtnFilterDuplicate.Checked = true;
                            break;
                        }

                    default:
                        {
                            this.BtnFilterError.Checked = true;
                            break;
                        }
                }
                RefreshUI();
            }
        }
        private enum FilterType : int
        {
            All = 0,
            Enabled = 1,
            Disabled = 2,
            CanUpdate = 3,
            Unavailable = 4,
            Duplicate = 5
        }

        /// <summary>
    /// 检查该 Mod 项是否符合当前筛选的类别。
    /// </summary>
        private bool CanPassFilter(ModLocalComp.LocalCompFile CheckingMod)
        {
            switch (Filter)
            {
                case FilterType.All:
                    {
                        return true;
                    }
                case FilterType.Enabled:
                    {
                        return CheckingMod.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine;
                    }
                case FilterType.Disabled:
                    {
                        return CheckingMod.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled;
                    }
                case FilterType.CanUpdate:
                    {
                        return CheckingMod.CanUpdate;
                    }
                case FilterType.Unavailable:
                    {
                        return CheckingMod.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable;
                    }
                case FilterType.Duplicate:
                    {
                        var ItemSource = IsSearching ? SearchResult : ModLocalComp.CompResourceListLoader.Output ?? new List<ModLocalComp.LocalCompFile>();
                        return ItemSource is not null && ItemSource.Where(m => CheckingMod.Comp is not null && m.Comp is not null && (CheckingMod.Comp.Id ?? "") == (m.Comp.Id ?? "")).Count() > 1;
                    }

                default:
                    {
                        return false;
                    }
            }
        }

        // 点击筛选项触发的改变
        private void ChangeFilter(MyRadioButton sender, bool raiseByMouse)
        {
            Filter = (FilterType)Conversions.ToInteger(sender.Tag);
            RefreshUI();
            DoSort();
        }

        #endregion

        #region 排序
        private SortMethod CurrentSortMethod = SortMethod.FileName;

        private void SetSortMethod(SortMethod Target)
        {
            CurrentSortMethod = Target;
            this.BtnSort.Text = $"排序：{GetSortName(Target)}";
            RefreshUI();
            DoSort();
        }

        private enum SortMethod
        {
            FileName,
            ModName,
            TagNums,
            CreateTime,
            ModFileSize
        }

        private string GetSortName(SortMethod Method)
        {
            switch (Method)
            {
                case SortMethod.FileName:
                    {
                        return "文件名";
                    }
                case SortMethod.ModName:
                    {
                        return "资源名称";
                    }
                case SortMethod.TagNums:
                    {
                        return "标签数量";
                    }
                case SortMethod.CreateTime:
                    {
                        return "加入时间";
                    }
                case SortMethod.ModFileSize:
                    {
                        return "文件大小";
                    }

                default:
                    {
                        return "资源名称";
                    }
            }
            return "";
        }

        private void BtnSortClick(object sender, ModBase.RouteEventArgs e)
        {
            var Body = new ContextMenu();
            foreach (SortMethod i in Enum.GetValues(typeof(SortMethod)))
            {
                var Item = new MyMenuItem();
                Item.Header = GetSortName(i);
                Item.Click += () => SetSortMethod(i);
                Body.Items.Add(Item);
            }
            Body.PlacementTarget = (UIElement)sender;
            Body.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            Body.IsOpen = true;
        }

        private readonly object SortLock = new object();
        private void DoSort()
        {
            lock (SortLock)
            {
                if (this.PanList is null || this.PanList.Children.Count < 2)
                    return;

                // 将子元素转换为可排序的列表
                var items = PanList.Children.OfType<MyLocalCompItem>().ToList();
                var Method = GetSortMethod(CurrentSortMethod);

                // 根据排序类型处理特殊逻辑
                if (CurrentSortMethod == SortMethod.TagNums)
                {
                    // 分离有效和无效项（保持原始相对顺序）
                    var valid = items.Where(i => i.Entry.Comp is not null).ToList();
                    var invalid = items.Except(valid).ToList();

                    // 仅对有效项进行排序
                    valid.Sort((x, y) => Method(y.Entry, x.Entry));

                    // 合并保持无效项的原始顺序
                    items = valid.Concat(invalid).ToList();
                }
                else
                {
                    // 直接进行高效排序
                    items.Sort((x, y) => Method(y.Entry, x.Entry));
                }

                // 批量更新UI元素
                this.PanList.Children.Clear();
                items.ForEach(i => this.PanList.Children.Add(i));
            }
        }

        private Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int> GetSortMethod(SortMethod Method)
        {
            switch (Method)
            {
                case SortMethod.FileName:
                    {
                        return new Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int>((a, b) => string.Compare(b.FileName, a.FileName, StringComparison.OrdinalIgnoreCase));
                    }
                case SortMethod.ModName:
                    {
                        return new Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int>((a, b) => string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
                    }
                case SortMethod.TagNums:
                    {
                        return new Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int>((a, b) => a.Comp.Tags.Count - b.Comp.Tags.Count);
                    }
                case SortMethod.CreateTime:
                    {
                        return new Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int>((a, b) => new FileInfo(a.Path).CreationTime > new FileInfo(b.Path).CreationTime ? 1 : -1);
                    }
                case SortMethod.ModFileSize:
                    {
                        return new Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int>((a, b) => (int)(new FileInfo(a.Path).Length - new FileInfo(b.Path).Length));
                    }

                default:
                    {
                        return new Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int>((a, b) => -Strings.StrComp(a.Name, b.Name));
                    }
            }
        }
        #endregion

        #region 下边栏

        // 启用 / 禁用
        private void BtnSelectED_Click(MyIconTextButton sender, ModBase.RouteEventArgs e)
        {
            EDMods(ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedMods.Contains(m.RawFileName)), !sender.Equals(this.BtnSelectDisable));
            ChangeAllSelected(false);
        }
        private void EDMods(IEnumerable<ModLocalComp.LocalCompFile> ModList, bool IsEnable)
        {
            bool IsSuccessful = true;
            foreach (var ModE in ModList.ToList())
            {
                var ModEntity = ModE; // 仅用于去除迭代变量无法修改的限制
                string NewPath = null;
                if (ModEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine && !IsEnable)
                {
                    // 禁用
                    NewPath = ModEntity.Path + (File.Exists(ModEntity.Path + ".old") ? ".old" : ".disabled");
                }
                else if (ModEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled && IsEnable)
                {
                    // 启用
                    NewPath = ModEntity.RawPath;
                }
                else
                {
                    continue;
                }
                // 重命名
                try
                {
                    if (File.Exists(NewPath))
                    {
                        if (File.Exists(ModEntity.Path))
                        {
                            // 同时存在两个名称的 Mod
                            if ((ModBase.GetFileMD5(ModEntity.Path) ?? "") != (ModBase.GetFileMD5(NewPath) ?? ""))
                            {
                                ModMain.MyMsgBox($"目前同时存在启用和禁用的两个 Mod 文件：{Constants.vbCrLf} - {NewPath}{Constants.vbCrLf} - {ModEntity.Path}{Constants.vbCrLf}{Constants.vbCrLf}注意，这两个文件的内容并不相同。{Constants.vbCrLf}在手动删除或重命名其中一个文件后，才能继续操作。", "存在文件冲突");
                                continue;
                            }
                        }
                        else
                        {
                            // 已经重命名过了
                            ModBase.Log("[Mod] Mod 的状态已被切换", ModBase.LogLevel.Debug);
                            continue;
                        }
                    }
                    File.Delete(NewPath);
                    FileSystem.Rename(ModEntity.Path, NewPath);
                }
                catch (FileNotFoundException ex)
                {
                    ModBase.Log(ex, $"未找到需要重命名的 Mod（{ModEntity.Path ?? "null"}）", ModBase.LogLevel.Feedback);
                    ReloadCompFileList(true);
                    return;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"重命名 Mod 失败（{ModEntity.Path ?? "null"}）");
                    IsSuccessful = false;
                }
                // 更改 Loader 中的列表
                var NewModEntity = new ModLocalComp.LocalCompFile(NewPath);
                NewModEntity.FromJson(ModEntity.ToJson());
                if (ModLocalComp.CompResourceListLoader.Output.Contains(ModEntity))
                {
                    int IndexOfLoader = ModLocalComp.CompResourceListLoader.Output.IndexOf(ModEntity);
                    ModLocalComp.CompResourceListLoader.Output.RemoveAt(IndexOfLoader);
                    ModLocalComp.CompResourceListLoader.Output.Insert(IndexOfLoader, NewModEntity);
                }
                if (SearchResult is not null && SearchResult.Contains(ModEntity)) // #4862
                {
                    int IndexOfResult = SearchResult.IndexOf(ModEntity);
                    SearchResult.Remove(ModEntity);
                    SearchResult.Insert(IndexOfResult, NewModEntity);
                }
                // 更改 UI 中的列表
                var NewItem = BuildLocalCompItem(NewModEntity);
                ModItems[ModEntity.RawFileName] = NewItem;
                int IndexOfUi = this.PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>().FirstOrDefault(i => object.ReferenceEquals(i.Entry, ModEntity)));
                if (IndexOfUi == -1)
                    continue; // 因为未知原因 Mod 的状态已经切换完了
                this.PanList.Children.RemoveAt(IndexOfUi);
                this.PanList.Children.Insert(IndexOfUi, NewItem);
            }
            if (IsSuccessful)
            {
                RefreshBars();
            }
            else
            {
                ModMain.Hint("由于文件被占用，Mod 的状态切换失败，请尝试关闭正在运行的游戏后再试！", ModMain.HintType.Critical);
                ReloadCompFileList(true);
            }
            LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
        }

        // 更新
        private void BtnSelectUpdate_Click()
        {
            var UpdateList = ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedMods.Contains(m.RawFileName) && m.CanUpdate).ToList();
            if (!UpdateList.Any())
                return;
            UpdateResource(UpdateList);
            ChangeAllSelected(false);
        }
        /// <summary>
    /// 记录正在进行 Mod 更新的 mods 文件夹路径。
    /// </summary>
        public static List<string> UpdatingVersions = new List<string>();
        public void UpdateResource(IEnumerable<ModLocalComp.LocalCompFile> ModList)
        {
            // 更新前警告
            if (Conversions.ToBoolean(CurrentCompType == ModComp.CompType.Mod && (!(bool)ModBase.Setup.Get("HintUpdateMod") || ModList.Count() >= 15)))
            {
                if (ModMain.MyMsgBox($"新版本 Mod 可能不兼容旧存档或者其他 Mod，这可能导致游戏崩溃，甚至永久损坏存档！{Constants.vbCrLf}如果你在游玩整合包，请千万不要自行更新 Mod！{Constants.vbCrLf}{Constants.vbCrLf}在更新前，请先备份存档，并检查 Mod 的更新日志。{Constants.vbCrLf}如果更新后出现问题，你也可以在回收站找回更新前的 Mod。", "Mod 更新警告", "我已了解风险，继续更新", "取消", IsWarn: true) == 1)
                {
                    ModBase.Setup.Set("HintUpdateMod", true);
                }
                else
                {
                    return;
                }
            }
            try
            {
                // 构造下载信息
                ModList = ModList.ToList(); // 防止刷新影响迭代器
                var FileList = new List<ModNet.NetFile>();
                var FileCopyList = new Dictionary<string, string>();
                foreach (ModLocalComp.LocalCompFile Entry in ModList)
                {
                    var File = Entry.UpdateFile;
                    if (!File.Available)
                        continue;
                    // 确认更新后的文件名
                    string CurrentReplaceName = Entry.CompFile.FileName.Replace(".jar", "").Replace(".old", "").Replace(".disabled", "");
                    string NewestReplaceName = Entry.UpdateFile.FileName.Replace(".jar", "").Replace(".old", "").Replace(".disabled", "");
                    var CurrentSegs = CurrentReplaceName.Split('-').ToList();
                    var NewestSegs = NewestReplaceName.Split('-').ToList();
                    bool Shortened = false;
                    while (true) // 移除前导相同部分（不能移除所有相同项，这会导致例如 1.2-forge-2 和 1.3-forge-3 中间的 forge 被去掉，导致尝试替换 1.2-2）
                    {
                        if (!CurrentSegs.Any() || !NewestSegs.Any())
                            break;
                        if ((CurrentSegs.First() ?? "") != (NewestSegs.First() ?? ""))
                            break;
                        CurrentSegs.RemoveAt(0);
                        NewestSegs.RemoveAt(0);
                        Shortened = true;
                    }
                    while (true) // 移除后导相同部分
                    {
                        if (!CurrentSegs.Any() || !NewestSegs.Any())
                            break;
                        if ((CurrentSegs.Last() ?? "") != (NewestSegs.Last() ?? ""))
                            break;
                        CurrentSegs.RemoveAt(CurrentSegs.Count - 1);
                        NewestSegs.RemoveAt(NewestSegs.Count - 1);
                        Shortened = true;
                    }
                    if (Shortened && CurrentSegs.Any() && NewestSegs.Any())
                    {
                        CurrentReplaceName = CurrentSegs.Join("-");
                        NewestReplaceName = NewestSegs.Join("-");
                    }
                    // 添加到下载列表
                    string TempAddress = ModBase.PathTemp + @"DownloadedComp\" + Entry.FileName.Replace(CurrentReplaceName, NewestReplaceName);
                    string RealAddress = ModBase.GetPathFromFullPath(Entry.Path) + Entry.FileName.Replace(CurrentReplaceName, NewestReplaceName);
                    FileList.Add(File.ToNetFile(TempAddress));
                    FileCopyList[TempAddress] = RealAddress;
                }
                // 构造加载器
                var InstallLoaders = new List<ModLoader.LoaderBase>();
                var FinishedFileNames = new List<string>();
                InstallLoaders.Add(new ModNet.LoaderDownload("下载新版资源文件", FileList) { ProgressWeight = ModList.Count() * 1.5d }); // 每个 Mod 需要 1.5s
                InstallLoaders.Add(new ModLoader.LoaderTask<int, int>("替换旧版资源文件", () => { try { foreach (ModLocalComp.LocalCompFile Entry in ModList) { if (File.Exists(Entry.Path)) { File.Delete(Entry.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin); } else { ModBase.Log($"[CompUpdate] 未找到更新前的资源文件，跳过对它的删除：{Entry.Path}", ModBase.LogLevel.Debug); } } foreach (KeyValuePair<string, string> Entry in FileCopyList) { if (File.Exists(Entry.Value)) { File.Delete(Entry.Value, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin); ModBase.Log($"[Mod] 更新后的资源文件已存在，将会把它放入回收站：{Entry.Value}", ModBase.LogLevel.Debug); } if (Directory.Exists(ModBase.GetPathFromFullPath(Entry.Value))) { File.Move(Entry.Key, Entry.Value); FinishedFileNames.Add(ModBase.GetFileNameFromPath(Entry.Value)); } else { ModBase.Log($"[Mod] 更新后的目标文件夹已被删除：{Entry.Value}", ModBase.LogLevel.Debug); } } } catch (OperationCanceledException ex) { ModBase.Log(ex, "替换旧版资源文件时被主动取消"); } }));
                // 结束处理
                var Loader = new ModLoader.LoaderCombo<IEnumerable<ModLocalComp.LocalCompFile>>("资源更新：" + PageVersionLeft.Version.Name, InstallLoaders);
                string PathMods = PageVersionLeft.Version.PathIndie + ModLocalComp.GetPathNameByCompType(CurrentCompType) + @"\";
                Loader.OnStateChanged = new Action(() =>
        {
            // 结果提示
            switch (Loader.State)
            {
                case ModBase.LoadState.Finished:
                    {
                        switch (FinishedFileNames.Count)
                        {
                            case 0: // 一般是由于 Mod 文件被占用，然后玩家主动取消
                                {
                                    ModBase.Log($"[CompUpdate] 没有资源被成功更新");
                                    break;
                                }
                            case 1:
                                {
                                    ModMain.Hint($"已成功更新 {FinishedFileNames.Single()}！", ModMain.HintType.Finish);
                                    break;
                                }

                            default:
                                {
                                    ModMain.Hint($"已成功更新 {FinishedFileNames.Count} 个资源！", ModMain.HintType.Finish);
                                    break;
                                }
                        }

                        break;
                    }
                case ModBase.LoadState.Failed:
                    {
                        ModMain.Hint("资源更新失败：" + ModBase.GetExceptionSummary(Loader.Error), ModMain.HintType.Critical);
                        break;
                    }
                case ModBase.LoadState.Aborted:
                    {
                        ModMain.Hint("资源更新已中止！", ModMain.HintType.Info);
                        break;
                    }

                default:
                    {
                        return;
                    }
            }
            ModBase.Log($"[CompUpdate] 已从正在进行资源更新的文件夹列表移除：{PathMods}");
            UpdatingVersions.Remove(PathMods);
            // 清理缓存
            ModBase.RunInNewThread(() => { try { foreach (var TempFile in FileCopyList.Keys) { if (File.Exists(TempFile)) File.Delete(TempFile); } } catch (Exception ex) { ModBase.Log(ex, "清理资源更新缓存失败"); } }, "Clean Comp Update Cache", ThreadPriority.BelowNormal);
        });
                // 启动加载器
                ModBase.Log($"[CompUpdate] 开始更新 {ModList.Count()} 个资源：{PathMods}");
                UpdatingVersions.Add(PathMods);
                Loader.Start();
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
                ReloadCompFileList(true);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化资源更新失败");
            }
        }

        // 删除
        private void BtnSelectDelete_Click()
        {
            DeleteMods(ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedMods.Contains(m.RawFileName)));
            ChangeAllSelected(false);
        }
        private void DeleteMods(IEnumerable<ModLocalComp.LocalCompFile> ModList)
        {
            try
            {
                bool IsSuccessful = true;
                bool IsShiftPressed = My.MyWpfExtension.Computer.Keyboard.ShiftKeyDown;
                // 确认需要删除的文件
                ModList = ModList.SelectMany((Target) => { if (Target.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine) { return new[] { Target.Path, Target.Path + (File.Exists(Target.Path + ".old") ? ".old" : ".disabled") }; } else { return new[] { Target.Path, Target.RawPath }; } }).Distinct().Where(m => File.Exists(m)).Select(m => new ModLocalComp.LocalCompFile(m)).ToList();
                // 实际删除文件
                foreach (var ModEntity in ModList)
                {
                    // 删除
                    try
                    {
                        if (IsShiftPressed)
                        {
                            File.Delete(ModEntity.Path);
                        }
                        else
                        {
                            File.Delete(ModEntity.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        ModBase.Log(ex, "删除资源被主动取消");
                        ReloadCompFileList(true);
                        return;
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"删除资源失败（{ModEntity.Path}）", ModBase.LogLevel.Msgbox);
                        IsSuccessful = false;
                    }
                    // 取消选中
                    SelectedMods.Remove(ModEntity.RawFileName);
                    // 更改 Loader 和 UI 中的列表
                    ModLocalComp.CompResourceListLoader.Output.Remove(ModEntity);
                    SearchResult?.Remove(ModEntity);
                    ModItems.Remove(ModEntity.RawFileName);
                    int IndexOfUi = this.PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>().FirstOrDefault(i => i.Entry.Equals(ModEntity)));
                    if (IndexOfUi >= 0)
                        this.PanList.Children.RemoveAt(IndexOfUi);
                }
                RefreshBars();
                if (!IsSuccessful)
                {
                    ModMain.Hint("由于文件被占用，Mod 删除失败，请尝试关闭正在运行的游戏后再试！", ModMain.HintType.Critical);
                    ReloadCompFileList(true);
                }
                else if (this.PanList.Children.Count == 0)
                {
                    ReloadCompFileList(true); // 删除了全部文件
                }
                else
                {
                    RefreshBars();
                }
                // 显示结果提示
                if (!IsSuccessful)
                    return;
                if (IsShiftPressed)
                {
                    if (ModList.Count() == 1)
                    {
                        ModMain.Hint($"已彻底删除 {ModList.Single().FileName}！", ModMain.HintType.Finish);
                    }
                    else
                    {
                        ModMain.Hint($"已彻底删除 {ModList.Count()} 个文件！", ModMain.HintType.Finish);
                    }
                }
                else if (ModList.Count() == 1)
                {
                    ModMain.Hint($"已将 {ModList.Single().FileName} 删除到回收站！", ModMain.HintType.Finish);
                }
                else
                {
                    ModMain.Hint($"已将 {ModList.Count()} 个文件删除到回收站！", ModMain.HintType.Finish);
                }
            }
            catch (OperationCanceledException ex)
            {
                ModBase.Log(ex, "删除资源被主动取消");
                ReloadCompFileList(true);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除资源出现未知错误", ModBase.LogLevel.Feedback);
                ReloadCompFileList(true);
            }
            LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
        }

        // 取消选择
        private void BtnSelectCancel_Click()
        {
            ChangeAllSelected(false);
        }

        // 收藏
        private void BtnSelectFavorites_Click(object sender, ModBase.RouteEventArgs e)
        {
            var Selected = ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedMods.Contains(m.RawFileName) && m.Comp is not null).Select(i => i.Comp).ToList();
            ModComp.CompFavorites.ShowMenu(Selected, (UIElement)sender);
        }

        // 分享
        private void BtnSelectShare_Click()
        {
            var ShareList = ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedMods.Contains(m.RawFileName) && m.Comp is not null).Select(i => i.Comp.Id).ToList();
            ModBase.ClipboardSet(ModComp.CompFavorites.GetShareCode(ShareList));
            ChangeAllSelected(false);
        }

        #endregion

        #region 单个资源项

        // 详情
        public void Info_Click(object sender, EventArgs e)
        {
            try
            {

                var ModEntry = ((MyLocalCompItem)(sender is MyIconButton ? ((dynamic)sender).Tag : sender)).Entry;
                // 加载失败信息
                if (ModEntry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable)
                {
                    ModMain.MyMsgBox("无法读取此资源的信息。" + Constants.vbCrLf + Constants.vbCrLf + "详细的错误信息：" + ModBase.GetExceptionDetail(ModEntry.FileUnavailableReason), "资源读取失败");
                    return;
                }
                if (ModEntry.Comp is not null)
                {
                    // 跳转到 Mod 下载页面
                    ModMain.FrmMain.PageChange(new FormMain.PageStackData()
                    {
                        Page = FormMain.PageType.CompDetail,
                        Additional = new[] { ModEntry.Comp, new List<string>(), PageVersionLeft.Version.Version.McName, PageVersionLeft.Version.Version.HasForge ? ModComp.CompLoaderType.Forge : PageVersionLeft.Version.Version.HasNeoForge ? ModComp.CompLoaderType.NeoForge : PageVersionLeft.Version.Version.HasFabric ? ModComp.CompLoaderType.Fabric : ModComp.CompLoaderType.Any }
                    });
                }
                else
                {
                    // 获取信息
                    var ContentLines = new List<string>();
                    if (ModEntry.Description is not null)
                        ContentLines.Add(ModEntry.Description + Constants.vbCrLf);
                    if (ModEntry.Authors is not null)
                        ContentLines.Add("作者：" + ModEntry.Authors);
                    ContentLines.Add("文件：" + ModEntry.FileName + "（" + ModBase.GetString(new FileInfo(ModEntry.Path).Length) + "）");
                    if (ModEntry.Version is not null)
                        ContentLines.Add("版本：" + ModEntry.Version);
                    var DebugInfo = new List<string>();
                    if (ModEntry.ModId is not null)
                    {
                        DebugInfo.Add("Mod ID：" + ModEntry.ModId);
                    }
                    if (ModEntry.Dependencies.Any())
                    {
                        DebugInfo.Add("依赖于：");
                        foreach (var Dep in ModEntry.Dependencies)
                            DebugInfo.Add(" - " + Dep.Key + (Dep.Value is null ? "" : "，版本：" + Dep.Value));
                    }
                    if (DebugInfo.Any())
                    {
                        ContentLines.Add("");
                        ContentLines.AddRange(DebugInfo);
                    }
                    // 获取用于搜索的 Mod 名称
                    string ModOriginalName = ModEntry.Name.Replace(" ", "+");
                    string ModSearchName = ModOriginalName.Substring(0, 1);
                    for (int i = 1, loopTo = ModOriginalName.Count() - 1; i <= loopTo; i++)
                    {
                        bool IsLastLower = ModOriginalName[i - 1].ToString().ToLower().Equals(ModOriginalName[i - 1].ToString());
                        bool IsCurrentLower = ModOriginalName[i].ToString().ToLower().Equals(ModOriginalName[i].ToString());
                        if (IsLastLower && !IsCurrentLower)
                        {
                            // 上一个字母为小写，这一个字母为大写
                            ModSearchName += "+";
                        }
                        ModSearchName += Conversions.ToString(ModOriginalName[i]);
                    }
                    ModSearchName = ModSearchName.Replace("++", "+").Replace("pti+Fine", "ptiFine");
                    // 显示
                    if (ModEntry.Url is null)
                    {
                        if (ModMain.MyMsgBox(ContentLines.Join(Constants.vbCrLf), ModEntry.Name, "百科搜索", "返回") == 1)
                        {
                            ModBase.OpenWebsite("https://www.mcmod.cn/s?key=" + ModSearchName + "&site=all&filter=0");
                        }
                    }
                    else
                    {
                        switch (ModMain.MyMsgBox(ContentLines.Join(Constants.vbCrLf), ModEntry.Name, "打开官网", "百科搜索", "返回"))
                        {
                            case 1:
                                {
                                    ModBase.OpenWebsite(ModEntry.Url);
                                    break;
                                }
                            case 2:
                                {
                                    ModBase.OpenWebsite("https://www.mcmod.cn/s?key=" + ModSearchName + "&site=all&filter=0");
                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取资源详情失败", ModBase.LogLevel.Feedback);
            }
        }
        // 打开文件所在的位置
        public void Open_Click(MyIconButton sender, EventArgs e)
        {
            try
            {
                MyLocalCompItem ListItem = (MyLocalCompItem)sender.Tag;
                ModBase.OpenExplorer(ListItem.Entry.Path);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "打开资源文件位置失败", ModBase.LogLevel.Feedback);
            }
        }
        // 删除
        public void Delete_Click(MyIconButton sender, EventArgs e)
        {
            MyLocalCompItem ListItem = (MyLocalCompItem)sender.Tag;
            DeleteMods(new[] { ListItem.Entry });
        }
        // 启用 / 禁用
        public void ED_Click(MyIconButton sender, EventArgs e)
        {
            MyLocalCompItem ListItem = (MyLocalCompItem)sender.Tag;
            EDMods(new[] { ListItem.Entry }, ListItem.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled);
        }

        #endregion

        #region 搜索

        public bool IsSearching
        {
            get
            {
                return !string.IsNullOrWhiteSpace(this.SearchBox.Text);
            }
        }
        private List<ModLocalComp.LocalCompFile> SearchResult;

        public void SearchRun()
        {
            if (IsSearching)
            {
                // 构造请求
                var QueryList = new List<ModBase.SearchEntry<ModLocalComp.LocalCompFile>>();
                foreach (ModLocalComp.LocalCompFile Entry in ModLocalComp.CompResourceListLoader.Output)
                {
                    var SearchSource = new List<KeyValuePair<string, double>>();
                    SearchSource.Add(new KeyValuePair<string, double>(Entry.Name, 1d));
                    SearchSource.Add(new KeyValuePair<string, double>(Entry.FileName, 1d));
                    if (Entry.Version is not null)
                    {
                        SearchSource.Add(new KeyValuePair<string, double>(Entry.Version, 0.2d));
                    }
                    if (Entry.Description is not null && !string.IsNullOrEmpty(Entry.Description))
                    {
                        SearchSource.Add(new KeyValuePair<string, double>(Entry.Description, 0.4d));
                    }
                    if (Entry.Comp is not null)
                    {
                        if ((Entry.Comp.RawName ?? "") != (Entry.Name ?? ""))
                            SearchSource.Add(new KeyValuePair<string, double>(Entry.Comp.RawName, 1d));
                        if ((Entry.Comp.TranslatedName ?? "") != (Entry.Comp.RawName ?? ""))
                            SearchSource.Add(new KeyValuePair<string, double>(Entry.Comp.TranslatedName, 1d));
                        if ((Entry.Comp.Description ?? "") != (Entry.Description ?? ""))
                            SearchSource.Add(new KeyValuePair<string, double>(Entry.Comp.Description, 0.4d));
                        SearchSource.Add(new KeyValuePair<string, double>(string.Join("", Entry.Comp.Tags), 0.2d));
                    }
                    QueryList.Add(new ModBase.SearchEntry<ModLocalComp.LocalCompFile>() { Item = Entry, SearchSource = SearchSource });
                }
                // 进行搜索
                SearchResult = ModBase.Search(QueryList, SearchBox.Text, MaxBlurCount: 6, MinBlurSimilarity: 0.35d).Select(r => r.Item).ToList();
            }
            RefreshUI();
        }

        #endregion

    }
}