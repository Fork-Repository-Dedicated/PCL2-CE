using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageSelectRight
    {
        public PageSelectRight()
        {
            this.Loaded += PageSelectRight_Loaded;
            this.Initialized += (_, __) => LoaderInit();
        }

        // 窗口基础
        private void PageSelectRight_Loaded(object sender, RoutedEventArgs e)
        {
            ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.RunOnUpdated, MaxDepth: 1, ExtraPath: @"versions\");
            this.PanBack.ScrollToHome();
        }
        private void LoaderInit()
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanAllBack, (FrameworkElement)null, ModMinecraft.McVersionListLoader, (_) => this.McVersionListUI(), AutoRun: false);
        }
        private void Load_Click(object sender, MouseButtonEventArgs e)
        {
            if (ModMinecraft.McVersionListLoader.State == ModBase.LoadState.Failed)
            {
                ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
        }

        // 窗口属性
        /// <summary>
    /// 是否显示隐藏的 Minecraft 版本。
    /// </summary>
        public bool ShowHidden = false;

        #region 结果 UI 化

        private void McVersionListUI(ModLoader.LoaderTask<string, int> Loader)
        {
            try
            {
                string Path = Loader.Input;
                // 加载 UI
                this.PanMain.Children.Clear();

                foreach (KeyValuePair<ModMinecraft.McVersionCardType, List<ModMinecraft.McVersion>> Card in ModMinecraft.McVersionList.ToArray())
                {
                    // 确认是否为隐藏版本显示状态
                    if (Card.Key == ModMinecraft.McVersionCardType.Hidden ^ ShowHidden)
                        continue;
                    #region 确认卡片名称
                    string CardName = "";
                    switch (Card.Key)
                    {
                        case ModMinecraft.McVersionCardType.OriginalLike:
                            {
                                CardName = "常规版本";
                                break;
                            }
                        case ModMinecraft.McVersionCardType.API:
                            {
                                bool IsForgeExists = false;
                                bool IsNeoForgeExists = false;
                                bool IsFabricExists = false;
                                bool IsQuiltExists = false;
                                bool IsLiteExists = false;
                                bool IsCleanroomExists = false;
                                foreach (ModMinecraft.McVersion Version in Card.Value)
                                {
                                    if (Version.Version.HasFabric)
                                        IsFabricExists = true;
                                    if (Version.Version.HasQuilt)
                                        IsQuiltExists = true;
                                    if (Version.Version.HasLiteLoader)
                                        IsLiteExists = true;
                                    if (Version.Version.HasForge)
                                        IsForgeExists = true;
                                    if (Version.Version.HasNeoForge)
                                        IsNeoForgeExists = true;
                                    if (Version.Version.HasCleanroom)
                                        IsCleanroomExists = true;
                                }
                                if ((IsLiteExists ? 1 : 0) + (IsForgeExists ? 1 : 0) + (IsFabricExists ? 1 : 0) + (IsNeoForgeExists ? 1 : 0) + (IsQuiltExists ? 1 : 0) + (IsCleanroomExists ? 1 : 0) > 1)
                                {
                                    CardName = "可安装 Mod";
                                }
                                else if (IsForgeExists)
                                {
                                    CardName = "Forge 版本";
                                }
                                else if (IsNeoForgeExists)
                                {
                                    CardName = "NeoForge 版本";
                                }
                                else if (IsCleanroomExists)
                                {
                                    CardName = "Cleanroom 版本";
                                }
                                else if (IsLiteExists)
                                {
                                    CardName = "LiteLoader 版本";
                                }
                                else if (IsQuiltExists)
                                {
                                    CardName = "Quilt 版本";
                                }
                                else
                                {
                                    CardName = "Fabric 版本";
                                }

                                break;
                            }
                        case ModMinecraft.McVersionCardType.Error:
                            {
                                CardName = "错误的版本";
                                break;
                            }
                        case ModMinecraft.McVersionCardType.Hidden:
                            {
                                CardName = "隐藏的版本";
                                break;
                            }
                        case ModMinecraft.McVersionCardType.Rubbish:
                            {
                                CardName = "不常用版本";
                                break;
                            }
                        case ModMinecraft.McVersionCardType.Star:
                            {
                                CardName = "收藏夹";
                                break;
                            }
                        case ModMinecraft.McVersionCardType.Fool:
                            {
                                CardName = "愚人节版本";
                                break;
                            }

                        default:
                            {
                                throw new ArgumentException("未知的卡片种类（" + ((int)Card.Key).ToString() + "）");
                            }
                    }
                    #endregion
                    // 建立控件
                    string CardTitle = CardName + (CardName == "收藏夹" ? "" : " (" + Card.Value.Count + ")");
                    var NewCard = new MyCard() { Title = CardTitle, Margin = new Thickness(0d, 0d, 0d, 15d) };
                    var NewStack = new StackPanel() { Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d), Tag = Card.Value };
                    NewCard.Children.Add(NewStack);
                    NewCard.SwapControl = NewStack;
                    this.PanMain.Children.Add(NewCard);
                    // 确定卡片是否展开
                    void PutMethod(StackPanel Stack) { foreach (var item in (IEnumerable)Stack.Tag) Stack.Children.Add(McVersionListItem((ModMinecraft.McVersion)item)); };
                    if (Card.Key == ModMinecraft.McVersionCardType.Rubbish || Card.Key == ModMinecraft.McVersionCardType.Error || Card.Key == ModMinecraft.McVersionCardType.Fool)
                    {
                        NewCard.IsSwaped = true;
                        NewCard.InstallMethod = PutMethod;
                    }
                    else
                    {
                        MyCard.StackInstall(ref NewStack, PutMethod);
                    }
                }

                // 若只有一个卡片，则强制展开
                if (this.PanMain.Children.Count == 1 && ((MyCard)this.PanMain.Children[0]).IsSwaped)
                {
                    ((MyCard)this.PanMain.Children[0]).IsSwaped = false;
                }

                // 判断应该显示哪一个页面
                if (this.PanMain.Children.Count == 0)
                {
                    this.PanEmpty.Visibility = Visibility.Visible;
                    this.PanBack.Visibility = Visibility.Collapsed;
                    if (ShowHidden)
                    {
                        this.LabEmptyTitle.Text = "无隐藏版本";
                        this.LabEmptyContent.Text = "没有版本被隐藏，你可以在版本设置的版本分类选项中隐藏版本。" + Constants.vbCrLf + "再次按下 F11 即可退出隐藏版本查看模式。";
                        this.BtnEmptyDownload.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        this.LabEmptyTitle.Text = "无可用版本";
                        this.LabEmptyContent.Text = "未找到任何版本的游戏，请先下载任意版本的游戏。" + Constants.vbCrLf + "若有已存在的游戏，请在左边的列表中选择添加文件夹，选择 .minecraft 文件夹将其导入。";
                        this.BtnEmptyDownload.Visibility = Conversions.ToBoolean((bool)ModBase.Setup.Get("UiHiddenPageDownload") && !PageSetupUI.HiddenForceShow) ? Visibility.Collapsed : Visibility.Visible;
                    }
                }
                else
                {
                    this.PanBack.Visibility = Visibility.Visible;
                    this.PanEmpty.Visibility = Visibility.Collapsed;
                }
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "将版本列表转换显示时失败", ModBase.LogLevel.Feedback);
            }
        }
        public static MyListItem McVersionListItem(ModMinecraft.McVersion Version)
        {
            var NewItem = new MyListItem() { Title = Version.Name, Info = Version.Info, Height = 42d, Tag = Version, SnapsToDevicePixels = true, Type = MyListItem.CheckType.Clickable };
            try
            {
                if (Version.Logo.EndsWith(@"PCL\Logo.png"))
                {
                    NewItem.Logo = Version.Path + @"PCL\Logo.png"; // 修复老版本中，存储的自定义 Logo 使用完整路径，导致移动后无法加载的 Bug
                }
                else
                {
                    NewItem.Logo = Version.Logo;
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "加载版本图标失败", ModBase.LogLevel.Hint);
                NewItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png";
            }
            NewItem.ContentHandler = McVersionListContent;
            return NewItem;
        }
        private static void McVersionListContent(MyListItem sender, EventArgs e)
        {
            ModMinecraft.McVersion Version = (ModMinecraft.McVersion)sender.Tag;
            // 注册点击事件
            sender.Click += (_, __) => PageSelectRight.Item_Click();
            // 图标按钮
            var BtnStar = new MyIconButton();
            if (Version.IsStar)
            {
                BtnStar.ToolTip = "取消收藏";
                ToolTipService.SetPlacement(BtnStar, System.Windows.Controls.Primitives.PlacementMode.Center);
                ToolTipService.SetVerticalOffset(BtnStar, 30d);
                ToolTipService.SetHorizontalOffset(BtnStar, 2d);
                BtnStar.LogoScale = 1.1d;
                BtnStar.Logo = ModBase.Logo.IconButtonLikeFill;
            }
            else
            {
                BtnStar.ToolTip = "收藏";
                ToolTipService.SetPlacement(BtnStar, System.Windows.Controls.Primitives.PlacementMode.Center);
                ToolTipService.SetVerticalOffset(BtnStar, 30d);
                ToolTipService.SetHorizontalOffset(BtnStar, 2d);
                BtnStar.LogoScale = 1.1d;
                BtnStar.Logo = ModBase.Logo.IconButtonLikeLine;
            }
            BtnStar.Click += () =>
                {
                    ModBase.WriteIni(Version.Path + @"PCL\Setup.ini", "IsStar", Conversions.ToString(!Version.IsStar));
                    ModMinecraft.McVersionListForceRefresh = true;
                    ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                };
            var BtnDel = new MyIconButton() { LogoScale = 1.1d, Logo = ModBase.Logo.IconButtonDelete };
            BtnDel.ToolTip = "删除";
            ToolTipService.SetPlacement(BtnDel, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnDel, 30d);
            ToolTipService.SetHorizontalOffset(BtnDel, 2d);
            BtnDel.Click += () => DeleteVersion(sender, Version);
            if (Version.State != ModMinecraft.McVersionState.Error)
            {
                var BtnCont = new MyIconButton() { LogoScale = 1.1d, Logo = ModBase.Logo.IconButtonSetup };
                BtnCont.ToolTip = "设置";
                ToolTipService.SetPlacement(BtnCont, System.Windows.Controls.Primitives.PlacementMode.Center);
                ToolTipService.SetVerticalOffset(BtnCont, 30d);
                ToolTipService.SetHorizontalOffset(BtnCont, 2d);
                BtnCont.Click += () =>
        {
            PageVersionLeft.Version = Version;
            ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.VersionSetup, 0);
        };
                sender.MouseRightButtonUp += () =>
        {
            global::PCL.PageVersionLeft.Version = Version;
            global::PCL.ModMain.FrmMain.PageChange((global::PCL.FormMain.PageStackData)global::PCL.FormMain.PageType.VersionSetup, 0);
        };
                sender.Buttons = new[] { BtnStar, BtnDel, BtnCont };
            }
            else
            {
                var BtnCont = new MyIconButton() { LogoScale = 1.15d, Logo = ModBase.Logo.IconButtonOpen };
                BtnCont.ToolTip = "打开文件夹";
                ToolTipService.SetPlacement(BtnCont, System.Windows.Controls.Primitives.PlacementMode.Center);
                ToolTipService.SetVerticalOffset(BtnCont, 30d);
                ToolTipService.SetHorizontalOffset(BtnCont, 2d);
                BtnCont.Click += () => PageVersionOverall.OpenVersionFolder(Version);
                sender.MouseRightButtonUp += () => global::PCL.PageVersionOverall.OpenVersionFolder(Version);
                sender.Buttons = new[] { BtnStar, BtnDel, BtnCont };
            }
        }

        #endregion

        #region 页面事件

        // 点击选项
        public static void Item_Click(MyListItem sender, EventArgs e)
        {
            ModMinecraft.McVersion Version = (ModMinecraft.McVersion)sender.Tag;
            if (new ModMinecraft.McVersion(Version.Path).Check())
            {
                // 正常版本
                ModMinecraft.McVersionCurrent = Version;
                ModBase.Setup.Set("LaunchVersionSelect", ModMinecraft.McVersionCurrent.Name);
                ModMain.FrmMain.PageBack();
            }
            else
            {
                // 错误版本
                PageVersionOverall.OpenVersionFolder(Version);
            }
        }

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
        }

        // 修改此代码时，同时修改 PageVersionOverall 中的代码
        public static void DeleteVersion(MyListItem Item, ModMinecraft.McVersion Version)
        {
            try
            {
                bool IsShiftPressed = My.MyWpfExtension.Computer.Keyboard.ShiftKeyDown;
                bool IsHintIndie = Version.State != ModMinecraft.McVersionState.Error && (Version.PathIndie ?? "") != (ModMinecraft.PathMcFolder ?? "");
                switch (ModMain.MyMsgBox($"你确定要{(IsShiftPressed ? "永久" : "")}删除版本 {Version.Name} 吗？" + (IsHintIndie ? Constants.vbCrLf + "由于该版本开启了版本隔离，删除版本时该版本对应的存档、资源包、Mod 等文件也将被一并删除！" : ""), "版本删除确认", Button2: "取消", IsWarn: true))
                {
                    case 1:
                        {
                            ModBase.IniClearCache(Version.PathIndie + "options.txt");
                            ModBase.IniClearCache(Version.Path + @"PCL\Setup.ini");
                            if (IsShiftPressed)
                            {
                                ModBase.DeleteDirectory(Version.Path);
                                ModMain.Hint("版本 " + Version.Name + " 已永久删除！", ModMain.HintType.Finish);
                            }
                            else
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(Version.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                                ModMain.Hint("版本 " + Version.Name + " 已删除到回收站！", ModMain.HintType.Finish);
                            }

                            break;
                        }
                    case 2:
                        {
                            return;
                        }
                }
                // 从 UI 中移除
                if (Version.DisplayType == ModMinecraft.McVersionCardType.Hidden || !Version.IsStar)
                {
                    // 仅出现在当前卡片
                    StackPanel Parent = (StackPanel)Item.Parent;
                    if (Parent.Children.Count > 2) // 当前的项目与一个占位符
                    {
                        // 删除后还有剩
                        MyCard Card = (MyCard)Parent.Parent;
                        Card.Title = Card.Title.Replace((Parent.Children.Count - 1).ToString(), (Parent.Children.Count - 2).ToString()); // 有一个占位符
                        Parent.Children.Remove(Item);
                        if (ModMinecraft.McVersionCurrent is not null && (Version.Path ?? "") == (ModMinecraft.McVersionCurrent.Path ?? ""))
                        {
                            // 删除当前版本就更改选择
                            ModMinecraft.McVersionCurrent = (ModMinecraft.McVersion)((MyListItem)Parent.Children[0]).Tag;
                        }
                        ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.UpdateOnly, MaxDepth: 1, ExtraPath: @"versions\");
                    }
                    else
                    {
                        // 删除后没剩了
                        ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                    }
                }
                else
                {
                    // 同时出现在当前卡片与收藏夹
                    ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                }
            }
            catch (OperationCanceledException ex)
            {
                ModBase.Log(ex, "删除版本 " + Version.Name + " 被主动取消");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除版本 " + Version.Name + " 失败", ModBase.LogLevel.Msgbox);
            }
        }

        public void BtnEmptyDownload_Loaded()
        {
            var NewVisibility = (bool)ModBase.Setup.Get("UiHiddenPageDownload") && !PageSetupUI.HiddenForceShow || ShowHidden ? Visibility.Collapsed : Visibility.Visible;
            if (this.BtnEmptyDownload.Visibility != NewVisibility)
            {
                this.BtnEmptyDownload.Visibility = NewVisibility;
                this.PanLoad.TriggerForceResize();
            }
        }

        #endregion

    }
}