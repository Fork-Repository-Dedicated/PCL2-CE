using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class MyCompItem
    {

        #region 基础属性
        public int Uuid = ModBase.GetUuid();

        // Logo
        public string Logo
        {
            get
            {
                return this.PathLogo.Source;
            }
            set
            {
                this.PathLogo.Source = value;
            }
        }

        // 标题
        public string Title
        {
            get
            {
                return this.LabTitle.Text;
            }
            set
            {
                if ((this.LabTitle.Text ?? "") == (value ?? ""))
                    return;
                this.LabTitle.Text = value;
            }
        }

        // 副标题
        public string SubTitle
        {
            get
            {
                return (this.LabTitleRaw?.Text) ?? "";
            }
            set
            {
                if ((this.LabTitleRaw.Text ?? "") == (value ?? ""))
                    return;
                this.LabTitleRaw.Text = value;
                this.LabTitleRaw.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        // 描述
        public string Description
        {
            get
            {
                return this.LabInfo.Text;
            }
            set
            {
                if ((this.LabInfo.Text ?? "") == (value ?? ""))
                    return;
                this.LabInfo.Text = value;
            }
        }

        public MyCompItem()
        {
            this.PreviewMouseLeftButtonUp += Button_MouseUp;
            Click += ProjectClick;
            this.PreviewMouseLeftButtonDown += Button_MouseDown;
            this.MouseLeave += Button_MouseLeave;
            this.PreviewMouseLeftButtonUp += Button_MouseLeave;
            this.MouseEnter += RefreshColor;
            this.MouseLeave += RefreshColor;
            this.MouseLeftButtonDown += RefreshColor;
            this.MouseLeftButtonUp += RefreshColor;
        }
        // 指向时扩展描述
        private void LabInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            if (this.IsTextTrimmed(this.LabInfo))
            {
                this.ToolTipInfo.Content = this.LabInfo.Text;
                this.ToolTipInfo.Width = this.LabInfo.ActualWidth + 25d;
                this.LabInfo.ToolTip = this.ToolTipInfo;
            }
            else
            {
                this.LabInfo.ToolTip = (object)null;
            }
        }
        private bool IsTextTrimmed(TextBlock textBlock)
        {
            var typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch);
            var formattedText = new FormattedText(textBlock.Text, Thread.CurrentThread.CurrentCulture, textBlock.FlowDirection, typeface, textBlock.FontSize, textBlock.Foreground, ModBase.DPI);
            return formattedText.Width > textBlock.ActualWidth;
        }

        // Tag
        public List<string> Tags
        {
            set
            {
                this.PanTags.Children.Clear();
                this.PanTags.Visibility = value.Any() ? Visibility.Visible : Visibility.Collapsed;
                foreach (var TagText in value)
                {
                    var NewTag = ModBase.GetObjectFromXML(@"<Border xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                         Background=""#11000000"" Padding=""3,1"" CornerRadius=""3"" Margin=""0,0,3,0"" 
                         SnapsToDevicePixels=""True"" UseLayoutRounding=""False"">
                   <TextBlock Text=""" + TagText + @""" Foreground=""#868686"" FontSize=""11"" />
                </Border>");
                    this.PanTags.Children.Add((UIElement)NewTag);
                }
            }
        }

        #endregion

        #region 点击

        // 触发点击事件
        public event ClickEventHandler Click;

        public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);
        private void Button_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseDown)
            {
                Click?.Invoke(sender, e);
                if (e.Handled)
                    return;
                ModBase.Log("[Control] 按下资源工程列表项：" + this.LabTitle.Text);
            }
        }
        private void ProjectClick(MyCompItem sender, EventArgs e)
        {
            // 记录当前展开的卡片标题（#2712）
            var Titles = new List<string>();
            if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.CompDetail)
            {
                foreach (MyCard Card in ModMain.FrmDownloadCompDetail.PanResults.Children)
                {
                    if (!string.IsNullOrEmpty(Card.Title) && !Card.IsSwaped)
                        Titles.Add(Card.Title);
                }
                ModBase.Log("[Comp] 记录当前已展开的卡片：" + string.Join("、", Titles));
                ModMain.FrmMain.PageCurrent.Additional(1) = Titles;
            }
            // 打开详情页
            string TargetVersion;
            var TargetLoader = default(ModComp.CompLoaderType);
            if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.CompDetail)
            {
                TargetVersion = Conversions.ToString(ModMain.FrmMain.PageCurrent.Additional(2));
                TargetLoader = (ModComp.CompLoaderType)Conversions.ToInteger(ModMain.FrmMain.PageCurrent.Additional(3));
            }
            else if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.Download && ModMain.FrmMain.PageCurrentSub == FormMain.PageSubType.DownloadCompFavorites)
            {
                TargetVersion = "";
                TargetLoader = ModComp.CompLoaderType.Any;
            }
            else
            {
                switch (((ModComp.CompProject)sender.Tag).Type)
                {
                    case ModComp.CompType.Mod:
                        {
                            TargetVersion = PageDownloadMod.Loader.Input.GameVersion ?? "";
                            TargetLoader = PageDownloadMod.Loader.Input.ModLoader;
                            break;
                        }
                    case ModComp.CompType.ModPack:
                        {
                            TargetVersion = PageDownloadPack.Loader.Input.GameVersion ?? "";
                            break;
                        }
                    case ModComp.CompType.Shader:
                        {
                            TargetVersion = PageDownloadShader.Loader.Input.GameVersion ?? ""; // CompType.ResourcePack
                            break;
                        }

                    default:
                        {
                            // FUTURE: Res
                            TargetVersion = ""; // If(PageDownloadResource.Loader.Input.GameVersion, "")
                            break;
                        }
                }
            }
            if (((ModComp.CompProject)sender.Tag).Type != ModComp.CompType.Mod)
                TargetLoader = ModComp.CompLoaderType.Any;
            ModMain.FrmMain.PageChange(new FormMain.PageStackData()
            {
                Page = FormMain.PageType.CompDetail,
                Additional = new[] { sender.Tag, new List<string>(), TargetVersion, TargetLoader }
            });
        }

        // 鼠标点击判定
        private bool IsMouseDown = false;
        private void Button_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.IsMouseOver && CanInteraction)
                IsMouseDown = true;
        }
        private void Button_MouseLeave(object sender, object e)
        {
            IsMouseDown = false;
        }

        #endregion

        #region 后加载指向背景

        private Border _RectBack = null;
        public Border RectBack
        {
            get
            {
                if (_RectBack is null)
                {
                    var Rect = new Border()
                    {
                        Name = "RectBack",
                        CornerRadius = new CornerRadius(3d),
                        RenderTransform = new ScaleTransform(0.8d, 0.8d),
                        RenderTransformOrigin = new Point(0.5d, 0.5d),
                        BorderThickness = new Thickness(ModBase.GetWPFSize(1d)),
                        SnapsToDevicePixels = true,
                        IsHitTestVisible = false,
                        Opacity = 0d
                    };
                    Rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7");
                    Rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
                    Grid.SetColumnSpan(Rect, 999);
                    Grid.SetRowSpan(Rect, 999);
                    this.Children.Insert(0, Rect);
                    _RectBack = Rect;
                    // <!--<Border x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True" 
                    // IsHitTestVisible = "False" Opacity="0" BorderThickness="1" 
                    // Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
                }
                return _RectBack;
            }
        }

        #endregion

        private string StateLast;
        /// <summary>
    /// 是否允许交互。目前仅用于 PageDownloadCompDetail 的顶部栏展示：若关闭碰撞检测，则无法展开 Tooltip。
    /// </summary>
        public bool CanInteraction { get; set; } = true;
        public void RefreshColor(object sender, EventArgs e)
        {
            if (!CanInteraction)
                return;
            // 判断当前颜色
            string StateNew;
            int Time;
            if (this.IsMouseOver)
            {
                if (IsMouseDown)
                {
                    StateNew = "MouseDown";
                    Time = 120;
                }
                else
                {
                    StateNew = "MouseOver";
                    Time = 120;
                }
            }
            else
            {
                StateNew = "Idle";
                Time = 180;
            }
            if ((StateLast ?? "") == (StateNew ?? ""))
                return;
            StateLast = StateNew;
            // 触发颜色动画
            if (this.IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                var Ani = new List<ModAnimation.AniData>();
                if (this.IsMouseOver)
                {
                    Ani.AddRange(new[] { ModAnimation.AaColor(RectBack, Border.BackgroundProperty, IsMouseDown ? "ColorBrush6" : "ColorBrushBg1", Time), ModAnimation.AaOpacity(RectBack, 1d - RectBack.Opacity, Time, Ease: new ModAnimation.AniEaseOutFluent()) });
                    if (IsMouseDown)
                    {
                        Ani.Add(ModAnimation.AaScaleTransform(RectBack, 0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(Time * 1.2d), Ease: new ModAnimation.AniEaseOutFluent()));
                    }
                    else
                    {
                        Ani.Add(ModAnimation.AaScaleTransform(RectBack, 1d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(Time * 1.2d), Ease: new ModAnimation.AniEaseOutFluent()));
                    }
                }
                else
                {
                    Ani.AddRange(new[] { ModAnimation.AaOpacity(RectBack, -RectBack.Opacity, Time), ModAnimation.AaColor(RectBack, Border.BackgroundProperty, IsMouseDown ? "ColorBrush6" : "ColorBrush7", Time), ModAnimation.AaScaleTransform(RectBack, 0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, Time, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaScaleTransform(RectBack, -0.196d, 1, After: true) });
                }
                ModAnimation.AniStart(Ani, "CompItem Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("CompItem Color " + Uuid);
                if (_RectBack is not null)
                    RectBack.Opacity = 0d;
            }
        }

    }
}