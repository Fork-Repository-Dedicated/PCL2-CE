using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageSetupLaunch
    {

        private bool IsLoad = false;

        public PageSetupLaunch()
        {
            this.Loaded += PageSetupLaunch_Loaded;
        }

        private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();
            RefreshRam(false);
            if (ModMinecraft.McVersionCurrent is null)
            {
                this.BtnSwitch.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.BtnSwitch.Visibility = Visibility.Visible;
            }

            // 非重复加载部分
            if (IsLoad)
                return;
            IsLoad = true;

            ModAnimation.AniControlEnabled += 1;
            Reload();
            ModAnimation.AniControlEnabled -= 1;

            // 内存自动刷新
            var timer = new System.Windows.Threading.DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 1) };
            timer.Tick += (_, __) => RefreshRam();
            timer.Start();

        }
        public void Reload()
        {
            try
            {

                // 离线皮肤
                ((MyRadioBox)this.FindName(Conversions.ToString(Operators.ConcatenateObject("RadioSkinType", ModBase.Setup.Load("LaunchSkinType"))))).Checked = true;
                this.TextSkinID.Text = Conversions.ToString(ModBase.Setup.Get("LaunchSkinID"));

                // 启动参数
                this.TextArgumentTitle.Text = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentTitle"));
                this.TextArgumentInfo.Text = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentInfo"));
                this.ComboArgumentIndieV2.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("LaunchArgumentIndieV2"));
                this.ComboArgumentVisibie.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("LaunchArgumentVisible"));
                this.ComboArgumentPriority.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("LaunchArgumentPriority"));
                this.ComboArgumentWindowType.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("LaunchArgumentWindowType"));
                this.TextArgumentWindowWidth.Text = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentWindowWidth"));
                this.TextArgumentWindowHeight.Text = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentWindowHeight"));
                this.CheckArgumentRam.Checked = Conversions.ToBoolean(ModBase.Setup.Get("LaunchArgumentRam"));
                this.CheckArgumentJavaTraversal.Checked = Conversions.ToBoolean(ModBase.Setup.Get("LaunchArgumentJavaTraversal"));
                RefreshJavaComboBox();

                // 游戏内存
                ((MyRadioBox)this.FindName(Conversions.ToString(Operators.ConcatenateObject("RadioRamType", ModBase.Setup.Load("LaunchRamType"))))).Checked = true;
                this.SliderRamCustom.Value = Conversions.ToInteger(ModBase.Setup.Get("LaunchRamCustom"));

                // 高级设置
                this.TextAdvanceJvm.Text = Conversions.ToString(ModBase.Setup.Get("LaunchAdvanceJvm"));
                this.TextAdvanceGame.Text = Conversions.ToString(ModBase.Setup.Get("LaunchAdvanceGame"));
                this.TextAdvanceRun.Text = Conversions.ToString(ModBase.Setup.Get("LaunchAdvanceRun"));
                this.CheckAdvanceRunWait.Checked = Conversions.ToBoolean(ModBase.Setup.Get("LaunchAdvanceRunWait"));
                this.CheckAdvanceGraphicCard.Checked = Conversions.ToBoolean(ModBase.Setup.Get("LaunchAdvanceGraphicCard"));
                if (ModBase.IsArm64System)
                {
                    this.CheckAdvanceDisableJLW.Checked = true;
                    this.CheckAdvanceDisableJLW.IsEnabled = false;
                    this.CheckAdvanceDisableJLW.ToolTip = "在启动游戏时不使用 Java Wrapper 进行包装。&#xa;由于系统为 ARM64 架构，Java Wrapper 已被强制禁用。";
                }
                else
                {
                    this.CheckAdvanceDisableJLW.Checked = Conversions.ToBoolean(ModBase.Setup.Get("LaunchAdvanceDisableJLW"));
                }
            }

            catch (NullReferenceException ex)
            {
                ModBase.Log(ex, "启动设置项存在异常，已被自动重置", ModBase.LogLevel.Msgbox);
                Reset();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重载启动设置时出错", ModBase.LogLevel.Feedback);
            }
        }

        // 初始化
        public void Reset()
        {
            try
            {
                ModBase.Setup.Reset("LaunchArgumentTitle");
                ModBase.Setup.Reset("LaunchArgumentInfo");
                ModBase.Setup.Reset("LaunchArgumentIndieV2");
                ModBase.Setup.Reset("LaunchArgumentVisible");
                ModBase.Setup.Reset("LaunchArgumentWindowType");
                ModBase.Setup.Reset("LaunchArgumentWindowWidth");
                ModBase.Setup.Reset("LaunchArgumentWindowHeight");
                ModBase.Setup.Reset("LaunchArgumentPriority");
                ModBase.Setup.Reset("LaunchArgumentRam");
                ModBase.Setup.Reset("LaunchArgumentJavaTraversal");
                ModBase.Setup.Reset("LaunchRamType");
                ModBase.Setup.Reset("LaunchRamCustom");
                ModBase.Setup.Reset("LaunchSkinType");
                ModBase.Setup.Reset("LaunchSkinID");
                ModBase.Setup.Reset("LaunchAdvanceJvm");
                ModBase.Setup.Reset("LaunchAdvanceGame");
                ModBase.Setup.Reset("LaunchAdvanceRun");
                ModBase.Setup.Reset("LaunchAdvanceRunWait");
                ModBase.Setup.Reset("LaunchAdvanceDisableJLW");
                ModBase.Setup.Reset("LaunchAdvanceGraphicCard");
                ModBase.Setup.Reset("LaunchArgumentJavaAll");
                ModBase.Setup.Reset("LaunchArgumentJavaSelect");
                ModJava.JavaSearchLoader.Start(IsForceRestart: true);

                ModBase.Log("[Setup] 已初始化启动设置");
                ModMain.Hint("已初始化启动设置！", ModMain.HintType.Finish, false);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化启动设置失败", ModBase.LogLevel.Msgbox);
            }

            Reload();
        }

        // 将控件改变路由到设置改变
        private static void RadioBoxChange(MyRadioBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(sender.Tag.ToString().Split("/")[0], ModBase.Val(sender.Tag.ToString().Split("/")[1]));
        }
        private static void TextBoxChange(MyTextBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Text);
        }
        private static void SliderChange(MySlider sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Value);
        }
        private static void ComboChange(MyComboBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.SelectedIndex);
        }
        private static void CheckBoxChange(MyCheckBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Checked);
        }

        #region 离线皮肤

        private void BtnSkinChange_Click(object sender, EventArgs e)
        {
            var SkinInfo = ModMinecraft.McSkinSelect();
            if (!SkinInfo.IsVaild)
                return;
            ChangeSkin(SkinInfo);
        }
        private void RadioSkinType3_Check(object sender, ModBase.RouteEventArgs e)
        {
            if (!(ModAnimation.AniControlEnabled == 0 && e.RaiseByMouse))
                return;
            // 已有图片则不再选择
            if (File.Exists(ModBase.PathAppdata + "CustomSkin.png"))
                return;
            // 没有图片则要求选择
            var SkinInfo = ModMinecraft.McSkinSelect();
            if (!SkinInfo.IsVaild)
            {
                e.Handled = true;
                return;
            }
            // 正式改变
            if (!ChangeSkin(SkinInfo))
                e.Handled = true;
        }
        // 返回是否成功改变
        private bool ChangeSkin(ModMinecraft.McSkinInfo SkinInfo)
        {
            bool ChangeSkinRet = default;
            try
            {
                // 拷贝文件
                File.Delete(ModBase.PathAppdata + "CustomSkin.png");
                ModBase.CopyFile(SkinInfo.LocalFile, ModBase.PathAppdata + "CustomSkin.png");
                // 将单层皮肤扩展到双层
                var Bitmap = new MyBitmap(ModBase.PathAppdata + "CustomSkin.png");
                if (Bitmap.Pic.Width == 64 && Bitmap.Pic.Height == 32)
                {
                    System.Drawing.Image Img = Bitmap;
                    var NewBitmap = new System.Drawing.Bitmap(64, 64);
                    using (var g = System.Drawing.Graphics.FromImage(NewBitmap))
                    {
                        g.DrawImageUnscaled(Img, new System.Drawing.Point(0, 0));
                    }
                    File.Delete(ModBase.PathAppdata + "CustomSkin.png");
                    NewBitmap.Save(ModBase.PathAppdata + "CustomSkin.png");
                }
                // 更新设置
                ModBase.Setup.Set("LaunchSkinSlim", SkinInfo.IsSlim);
                ChangeSkinRet = true;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "改变离线皮肤失败", ModBase.LogLevel.Msgbox);
                ChangeSkinRet = false;
            }
            finally
            {
                // 设置当前显示
                PageLaunchLeft.SkinLegacy.Start(IsForceRestart: true);
            }

            return ChangeSkinRet;
        }
        private void BtnSkinDelete_Click(object sender, EventArgs e)
        {
            try
            {
                File.Delete(ModBase.PathAppdata + "CustomSkin.png");
                this.RadioSkinType0.SetChecked(true, true);
                ModMain.Hint("离线皮肤已清空！", ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "清空离线皮肤失败", ModBase.LogLevel.Msgbox);
            }
        }
        private void BtnSkinSave_Click(object sender, EventArgs e)
        {
            MySkin.Save(PageLaunchLeft.SkinLegacy);
        }
        private void BtnSkinCache_Click(object sender, EventArgs e)
        {
            MySkin.RefreshCache(null);
        }

        #endregion

        #region 游戏内存

        public void RamType(int Type)
        {
            if (this.SliderRamCustom is null)
                return;
            this.SliderRamCustom.IsEnabled = Type == 1;
        }

        /// <summary>
    /// 刷新 UI 上的 RAM 显示。
    /// </summary>
        public void RefreshRam(bool ShowAnim)
        {
            if (this.LabRamGame is null || this.LabRamUsed is null || ModMain.FrmMain.PageCurrent != (FormMain.PageStackData)FormMain.PageType.Setup || ModMain.FrmSetupLeft.PageID != FormMain.PageSubType.SetupLaunch)
                return;
            // 获取内存情况
            double RamGame = Math.Round(GetRam(ModMinecraft.McVersionCurrent, false), 5);
            double RamTotal = Math.Round(My.MyWpfExtension.Computer.Info.TotalPhysicalMemory / 1024d / 1024d / 1024d, 1);
            double RamAvailable = Math.Round(My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory / 1024d / 1024d / 1024d, 1);
            double RamGameActual = Math.Round(Math.Min(RamGame, RamAvailable), 5);
            double RamUsed = Math.Round(RamTotal - RamAvailable, 5);
            double RamEmpty = Math.Round(ModBase.MathClamp(RamTotal - RamUsed - RamGame, 0d, 1000d), 1);
            // 设置最大可用内存
            if (RamTotal <= 1.5d)
            {
                this.SliderRamCustom.MaxValue = (int)Math.Round(Math.Max(Math.Floor((RamTotal - 0.3d) / 0.1d), 1d));
            }
            else if (RamTotal <= 8d)
            {
                this.SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((RamTotal - 1.5d) / 0.5d) + 12d);
            }
            else if (RamTotal <= 16d)
            {
                this.SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((RamTotal - 8d) / 1d) + 25d);
            }
            else
            {
                this.SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((RamTotal - 16d) / 2d) + 33d);
            }
            // 设置文本
            this.LabRamGame.Text = Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(RamGame == Math.Floor(RamGame) ? RamGame + ".0" : RamGame, " GB"), RamGame != RamGameActual ? Operators.ConcatenateObject(Operators.ConcatenateObject(" (可用 ", RamGameActual == Math.Floor(RamGameActual) ? RamGameActual + ".0" : RamGameActual), " GB)") : ""));
            this.LabRamUsed.Text = Conversions.ToString(Operators.ConcatenateObject(RamUsed == Math.Floor(RamUsed) ? RamUsed + ".0" : RamUsed, " GB"));
            this.LabRamTotal.Text = Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(" / ", RamTotal == Math.Floor(RamTotal) ? RamTotal + ".0" : RamTotal), " GB"));
            this.LabRamWarn.Visibility = RamGame == 1d && !ModJava.JavaIs64Bit() && !ModBase.Is32BitSystem && ModJava.JavaList.Any() ? Visibility.Visible : Visibility.Collapsed;
            if (ShowAnim)
            {
                // 宽度动画
                ModAnimation.AniStart(new[] { ModAnimation.AaGridLengthWidth(this.ColumnRamUsed, RamUsed - this.ColumnRamUsed.Width.Value, 800, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), ModAnimation.AaGridLengthWidth(this.ColumnRamGame, RamGameActual - this.ColumnRamGame.Width.Value, 800, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), ModAnimation.AaGridLengthWidth(this.ColumnRamEmpty, RamEmpty - this.ColumnRamEmpty.Width.Value, 800, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)) }, "SetupLaunch Ram Grid");
            }
            else
            {
                // 宽度设置
                this.ColumnRamUsed.Width = new GridLength(RamUsed, GridUnitType.Star);
                this.ColumnRamGame.Width = new GridLength(RamGameActual, GridUnitType.Star);
                this.ColumnRamEmpty.Width = new GridLength(RamEmpty, GridUnitType.Star);
            }
        }
        private void RefreshRam()
        {
            RefreshRam(true);
        }

        private int RamTextLeft = 2;
        private int RamTextRight = 1;
        /// <summary>
    /// 刷新 UI 上的文本位置。
    /// </summary>
        private void RefreshRamText()
        {
            // 获取宽度信息
            double RectUsedWidth = this.RectRamUsed.ActualWidth;
            double TotalWidth = this.PanRamDisplay.ActualWidth;
            double LabGameWidth = this.LabRamGame.ActualWidth;
            double LabUsedWidth = this.LabRamUsed.ActualWidth;
            double LabTotalWidth = this.LabRamTotal.ActualWidth;
            double LabGameTitleWidth = this.LabRamGameTitle.ActualWidth;
            double LabUsedTitleWidth = this.LabRamUsedTitle.ActualWidth;
            // 左侧
            int Left;
            if (RectUsedWidth - 30d < LabUsedWidth || RectUsedWidth - 30d < LabUsedTitleWidth)
            {
                // 全写不下了
                Left = 0;
            }
            else if (RectUsedWidth - 25d < LabUsedWidth + LabTotalWidth)
            {
                // 显示不下完整数据
                Left = 1;
            }
            else
            {
                // 正常
                Left = 2;
            }
            if (RamTextLeft != Left)
            {
                RamTextLeft = Left;
                switch (Left)
                {
                    case 0:
                        {
                            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.LabRamUsed, -this.LabRamUsed.Opacity, 100), ModAnimation.AaOpacity(this.LabRamTotal, -this.LabRamTotal.Opacity, 100), ModAnimation.AaOpacity(this.LabRamUsedTitle, -this.LabRamUsedTitle.Opacity, 100) }, "SetupLaunch Ram TextLeft");
                            break;
                        }
                    case 1:
                        {
                            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.LabRamUsed, 1d - this.LabRamUsed.Opacity, 100), ModAnimation.AaOpacity(this.LabRamTotal, -this.LabRamTotal.Opacity, 100), ModAnimation.AaOpacity(this.LabRamUsedTitle, 0.7d - this.LabRamUsedTitle.Opacity, 100) }, "SetupLaunch Ram TextLeft");
                            break;
                        }
                    case 2:
                        {
                            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.LabRamUsed, 1d - this.LabRamUsed.Opacity, 100), ModAnimation.AaOpacity(this.LabRamTotal, 1d - this.LabRamTotal.Opacity, 100), ModAnimation.AaOpacity(this.LabRamUsedTitle, 0.7d - this.LabRamUsedTitle.Opacity, 100) }, "SetupLaunch Ram TextLeft");
                            break;
                        }
                }
            }
            // 右侧
            int Right;
            if (TotalWidth < LabGameWidth + 2d + RectUsedWidth || TotalWidth < LabGameTitleWidth + 2d + RectUsedWidth)
            {
                // 挤到最右边
                Right = 0;
            }
            else
            {
                // 正常情况
                Right = 1;
            }
            if (Right == 0)
            {
                if (ModAnimation.AniControlEnabled == 0 && (RamTextRight != Right || ModAnimation.AniIsRun("SetupLaunch Ram TextRight")))
                {
                    // 需要动画
                    ModAnimation.AniStart(new[] { ModAnimation.AaX(this.LabRamGame, TotalWidth - LabGameWidth - this.LabRamGame.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaX(this.LabRamGameTitle, TotalWidth - LabGameTitleWidth - this.LabRamGameTitle.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)) }, "SetupLaunch Ram TextRight");
                }
                else
                {
                    // 不需要动画
                    ModAnimation.AniStop("SetupLaunch Ram TextRight");
                    this.LabRamGame.Margin = new Thickness(TotalWidth - LabGameWidth, 3d, 0d, 0d);
                    this.LabRamGameTitle.Margin = new Thickness(TotalWidth - LabGameTitleWidth, 0d, 0d, 5d);
                }
            }
            else if (ModAnimation.AniControlEnabled == 0 && (RamTextRight != Right || ModAnimation.AniIsRun("SetupLaunch Ram TextRight")))
            {
                // 需要动画
                ModAnimation.AniStart(new[] { ModAnimation.AaX(this.LabRamGame, 2d + RectUsedWidth - this.LabRamGame.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaX(this.LabRamGameTitle, 2d + RectUsedWidth - this.LabRamGameTitle.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)) }, "SetupLaunch Ram TextRight");
            }
            else
            {
                // 不需要动画
                ModAnimation.AniStop("SetupLaunch Ram TextRight");
                this.LabRamGame.Margin = new Thickness(2d + RectUsedWidth, 3d, 0d, 0d);
                this.LabRamGameTitle.Margin = new Thickness(2d + RectUsedWidth, 0d, 0d, 5d);
            }
            RamTextRight = Right;
        }

        /// <summary>
    /// 获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
        public static double GetRam(ModMinecraft.McVersion Version, bool UseVersionJavaSetup, bool? Is32BitJava = default)
        {

            // ------------------------------------------
            // 修改下方代码时需要一并修改 PageVersionSetup
            // ------------------------------------------

            var RamGive = default(double);
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchRamType"), 0, false)))
            {
                // 自动配置
                double RamAvailable = Math.Round(My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory / 1024d / 1024d / 1024d * 10d) / 10d;
                // 确定需求的内存值
                double RamMininum; // 无论如何也需要保证的最低限度内存
                double RamTarget1; // 估计能勉强带动了的内存
                double RamTarget2; // 估计没啥问题了的内存
                double RamTarget3; // 放一百万个材质和 Mod 和光影需要的内存
                if (Version is not null && !Version.IsLoaded)
                    Version.Load();
                if (Version is not null && Version.Modable)
                {
                    // 可安装 Mod 的版本
                    var ModDir = new DirectoryInfo(Version.PathIndie + @"mods\");
                    int ModCount = ModDir.Exists ? ModDir.GetFiles().Length : 0;
                    RamMininum = 0.5d + ModCount / 150d;
                    RamTarget1 = 1.5d + ModCount / 90d;
                    RamTarget2 = 2.7d + ModCount / 50d;
                    RamTarget3 = 4.5d + ModCount / 25d;
                }
                else if (Version is not null && Version.Version.HasOptiFine)
                {
                    // OptiFine 版本
                    RamMininum = 0.5d;
                    RamTarget1 = 1.5d;
                    RamTarget2 = 3d;
                    RamTarget3 = 5d;
                }
                else
                {
                    // 普通版本
                    RamMininum = 0.5d;
                    RamTarget1 = 1.5d;
                    RamTarget2 = 2.5d;
                    RamTarget3 = 4d;
                }
                double RamDelta;
                // 预分配内存，阶段一，0 ~ T1，100%
                RamDelta = RamTarget1;
                RamGive += Math.Min(RamAvailable, RamDelta);
                RamAvailable -= RamDelta;
                if (RamAvailable < 0.1d)
                    goto PreFin;
                // 预分配内存，阶段二，T1 ~ T2，70%
                RamDelta = RamTarget2 - RamTarget1;
                RamGive += Math.Min(RamAvailable * 0.7d, RamDelta);
                RamAvailable -= RamDelta / 0.7d;
                if (RamAvailable < 0.1d)
                    goto PreFin;
                // 预分配内存，阶段三，T2 ~ T3，40%
                RamDelta = RamTarget3 - RamTarget2;
                RamGive += Math.Min(RamAvailable * 0.4d, RamDelta);
                RamAvailable -= RamDelta / 0.4d;
                if (RamAvailable < 0.1d)
                    goto PreFin;
                // 预分配内存，阶段四，T3 ~ T3 * 2，15%
                RamDelta = RamTarget3;
                RamGive += Math.Min(RamAvailable * 0.15d, RamDelta);
                RamAvailable -= RamDelta / 0.15d;
                if (RamAvailable < 0.1d)
                    goto PreFin;
                PreFin:
                ;

                // 不低于最低值
                RamGive = Math.Round(Math.Max(RamGive, RamMininum), 1);
            }
            else
            {
                // 手动配置
                int Value = Conversions.ToInteger(ModBase.Setup.Get("LaunchRamCustom"));
                if (Value <= 12)
                {
                    RamGive = Value * 0.1d + 0.3d;
                }
                else if (Value <= 25)
                {
                    RamGive = (Value - 12) * 0.5d + 1.5d;
                }
                else if (Value <= 33)
                {
                    RamGive = (Value - 25) * 1 + 8;
                }
                else
                {
                    RamGive = (Value - 33) * 2 + 16;
                }
            }
            // 若使用 32 位 Java，则限制为 1G
            if (Is32BitJava ?? !ModJava.JavaIs64Bit(UseVersionJavaSetup ? Version : null))
                RamGive = Math.Min(1d, RamGive);
            return RamGive;
        }

        #endregion

        #region Java 选择

        // 刷新 Java 下拉框显示
        public void RefreshJavaComboBox()
        {
            if (this.ComboArgumentJava is null)
                return;
            // 初始化列表
            this.ComboArgumentJava.Items.Clear();
            this.ComboArgumentJava.Items.Add(new MyComboBoxItem() { Content = "自动选择合适的 Java", Tag = "自动选择" });
            // 更新列表
            MyComboBoxItem SelectedItem = null;
            string SelectedBySetup = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentJavaSelect"));
            try
            {
                foreach (var Java in ModJava.JavaList.Clone().OrderByDescending(v => v.VersionCode))
                {
                    var ItemGrid = new Grid();
                    ItemGrid.Children.Add(new TextBlock()
                    {
                        Text = Java.ToString(),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        IsHitTestVisible = false
                    });
                    var BtnJavaED = new MyIconButton()
                    {
                        Logo = Java.IsEnabled ? ModBase.Logo.IconButtonStop : ModBase.Logo.IconButtonCheck,
                        LogoScale = 1.2d,
                        ToolTip = Java.IsEnabled ? "禁用" : "启用",
                        MaxHeight = 20d,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    ItemGrid.Children.Add(BtnJavaED);
                    var ListItem = new MyComboBoxItem() { Content = ItemGrid, ToolTip = Java.PathFolder, Tag = Java };
                    ToolTipService.SetHorizontalOffset(BtnJavaED, 20d);
                    ToolTipService.SetHorizontalOffset(ListItem, 400d);
                    this.ComboArgumentJava.Items.Add(ListItem);
                    BtnJavaED.Click += () =>
                        {
                            var TargetJava = ModJava.JavaList.Find(j => (j.PathFolder ?? "") == (Java.PathFolder ?? ""));
                            if (TargetJava is null)
                                return;
                            Java.IsEnabled = !Java.IsEnabled;
                            TargetJava.IsEnabled = Java.IsEnabled;
                            BtnJavaED.Logo = TargetJava.IsEnabled ? ModBase.Logo.IconButtonStop : ModBase.Logo.IconButtonCheck;
                            BtnJavaED.ToolTip = TargetJava.IsEnabled ? "禁用" : "启用";
                            var NewJavaList = new JArray();
                            foreach (var Item in ModJava.JavaList)
                                NewJavaList.Add(Item.ToJson());
                            ModBase.Setup.Set("LaunchArgumentJavaAll", NewJavaList.ToString(Newtonsoft.Json.Formatting.None));
                        };
                    // 判断人为选中
                    if (string.IsNullOrEmpty(SelectedBySetup))
                        continue;
                    if ((ModJava.JavaEntry.FromJson((JObject)ModBase.GetJson(SelectedBySetup)).PathFolder ?? "") == (Java.PathFolder ?? ""))
                        SelectedItem = ListItem;
                }
            }
            catch (Exception ex)
            {
                ModBase.Setup.Set("LaunchArgumentJavaSelect", "");
                ModBase.Log(ex, "更新设置 Java 下拉框失败", ModBase.LogLevel.Feedback);
            }
            // 更新选择项
            if (SelectedItem is null && ModJava.JavaList.Any())
                SelectedItem = (MyComboBoxItem)this.ComboArgumentJava.Items[0]; // 选中 “自动选择”
            this.ComboArgumentJava.SelectedItem = SelectedItem;
            // 结束处理
            if (SelectedItem is null)
            {
                this.ComboArgumentJava.Items.Clear();
                this.ComboArgumentJava.Items.Add(new ComboBoxItem() { Content = "未找到可用的 Java", IsSelected = true });
            }
            RefreshRam(true);
        }
        // 阻止在特定情况下展开下拉框
        private void ComboArgumentJava_DropDownOpened(object sender, EventArgs e)
        {
            if (this.ComboArgumentJava.SelectedItem is null || Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)this.ComboArgumentJava.Items[0]).Content, "未找到可用的 Java", false)) || Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)this.ComboArgumentJava.Items[0]).Content, "加载中……", false)))
            {
                this.ComboArgumentJava.IsDropDownOpen = false;
            }
        }

        // 下拉框选择更改
        private void JavaSelectionUpdate()
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            // Java 不可用时也不清空，会导致刷新时找不到对象
            if (this.ComboArgumentJava.SelectedItem is null || ((dynamic)this.ComboArgumentJava.SelectedItem).Tag is null)
                return;
            // 设置新的 Java
            var SelectedJava = ((dynamic)this.ComboArgumentJava.SelectedItem).Tag;
            if ("自动选择".Equals(SelectedJava))
            {
                // 选择 “自动”
                ModBase.Setup.Set("LaunchArgumentJavaSelect", "");
                ModBase.Log("[Java] 修改 Java 选择设置：自动选择");
            }
            else
            {
                // 选择指定项
                ModBase.Setup.Set("LaunchArgumentJavaSelect", ((JObject)SelectedJava.ToJson()).ToString(Newtonsoft.Json.Formatting.None));
                ModBase.Log("[Java] 修改 Java 选择设置：" + SelectedJava.ToString());
            }
            RefreshRam(true);
        }

        // 手动选择
        private void BtnArgumentJavaSelect_Click(object sender, EventArgs e)
        {
            if (ModJava.JavaSearchLoader.State == ModBase.LoadState.Loading)
            {
                ModMain.Hint("正在搜索 Java，请稍候！", ModMain.HintType.Critical);
                return;
            }
            // 选择 Java
            string JavaSelected = ModBase.SelectFile("javaw.exe|javaw.exe", "选择 bin 文件夹中的 javaw.exe 文件");
            if (string.IsNullOrEmpty(JavaSelected))
                return;
            JavaSelected = ModBase.GetPathFromFullPath(JavaSelected);
            try
            {
                // 验证 Java 可用
                var NewEntry = new ModJava.JavaEntry(JavaSelected, true);
                NewEntry.Check();
                // 加入列表
                var JavaNewList = new JArray() { NewEntry.ToJson() };
                foreach (var JsonEntry in (IEnumerable)ModBase.GetJson(Conversions.ToString(ModBase.Setup.Get("LaunchArgumentJavaAll"))))
                {
                    var Entry = ModJava.JavaEntry.FromJson((JObject)JsonEntry);
                    if ((Entry.PathFolder ?? "") == (NewEntry.PathFolder ?? ""))
                        continue;
                    JavaNewList.Add(JsonEntry);
                }
                ModBase.Setup.Set("LaunchArgumentJavaAll", JavaNewList.ToString(Newtonsoft.Json.Formatting.None));
                // 重新加载列表
                ModJava.JavaSearchLoader.Start(IsForceRestart: true);
                ModMain.Hint("已将该 Java 加入 Java 列表！", ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "该 Java 存在异常，无法使用", ModBase.LogLevel.Msgbox, "异常的 Java");
                return;
            }
        }
        // 自动查找
        private void BtnArgumentJavaSearch_Click(object sender, EventArgs e)
        {
            if (ModJava.JavaSearchLoader.State == ModBase.LoadState.Loading)
            {
                ModMain.Hint("正在搜索 Java，请稍候！", ModMain.HintType.Critical);
                return;
            }
            ModBase.RunInThread(() =>
        {
            ModMain.Hint("正在搜索 Java！");
            ModJava.JavaSearchLoader.WaitForExit(IsForceRestart: true);
            if (!ModJava.JavaList.Any())
            {
                ModMain.Hint("未找到可用的 Java！", ModMain.HintType.Critical);
            }
            else
            {
                ModMain.Hint("已找到 " + ModJava.JavaList.Count + " 个 Java，请检查下拉框查看列表！", ModMain.HintType.Finish);
            }
        });
        }

        #endregion

        #region 其他选项

        private void WindowTypeUIRefresh()
        {
            if (this.ComboArgumentWindowType is null)
                return;
            if (this.ComboArgumentWindowType.SelectedIndex == 3 && this.LabArgumentWindowMiddle is not null && this.LabArgumentWindowMiddle.Visibility == Visibility.Collapsed)
            {
                this.LabArgumentWindowMiddle.Visibility = Visibility.Visible;
                this.TextArgumentWindowHeight.Visibility = Visibility.Visible;
                this.TextArgumentWindowWidth.Visibility = Visibility.Visible;
            }
            else if (this.ComboArgumentWindowType.SelectedIndex != 3 && this.LabArgumentWindowMiddle is not null && this.LabArgumentWindowMiddle.Visibility == Visibility.Visible)
            {
                this.LabArgumentWindowMiddle.Visibility = Visibility.Collapsed;
                this.TextArgumentWindowHeight.Visibility = Visibility.Collapsed;
                this.TextArgumentWindowWidth.Visibility = Visibility.Collapsed;
            }
        }

        // 可见性选择直接关闭的警告
        private void ComboArgumentVisibie_SizeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (this.ComboArgumentVisibie.SelectedIndex == 0)
            {
                if (ModMain.MyMsgBox("若在游戏启动后立即关闭启动器，崩溃检测、更改游戏标题等功能将失效。" + Constants.vbCrLf + "如果想保留这些功能，可以选择让启动器在游戏启动后隐藏，游戏退出后自动关闭。", "提醒", "继续", "取消") == 2)
                {
                    this.ComboArgumentVisibie.SelectedItem = e.RemovedItems[0];
                }
            }
        }

        // 开启自动内存优化的警告
        private void CheckArgumentRam_Change()
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (!this.CheckArgumentRam.Checked)
                return;
            if (ModMain.MyMsgBox("内存优化会显著延长启动耗时，建议仅在内存不足时开启。" + Constants.vbCrLf + "如果你在使用机械硬盘，这还可能导致一小段时间的严重卡顿。" + (ModBase.IsAdmin() ? "" : $"{Constants.vbCrLf}{Constants.vbCrLf}每次启动游戏，PCL 都需要申请管理员权限以进行内存优化。{Constants.vbCrLf}若想自动授予权限，可以右键 PCL，打开 属性 → 兼容性 → 以管理员身份运行此程序。"), "提醒", "确定", "取消") == 2)
            {
                this.CheckArgumentRam.Checked = false;
            }
        }

        // 版本隔离提示
        private void ComboArgumentIndie_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            ModMain.MyMsgBox("默认策略只会对今后新安装的版本生效。" + Constants.vbCrLf + "已有版本的隔离策略需要在它的版本设置中调整。");
        }

        #endregion

        #region 高级设置

        private void TextAdvanceRun_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.CheckAdvanceRunWait.Visibility = string.IsNullOrEmpty(this.TextAdvanceRun.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        // JVM 参数重设
        private void TextAdvanceJvm_TextChanged()
        {
            this.BtnAdvanceJvmReset.Visibility = (this.TextAdvanceJvm.Text ?? "") == (ModBase.Setup.GetDefault("LaunchAdvanceJvm") ?? "") ? Visibility.Hidden : Visibility.Visible;
        }
        private void BtnAdvanceJvmReset_Click(object sender, EventArgs e)
        {
            ModBase.Setup.Reset("LaunchAdvanceJvm");
            Reload();
        }

        #endregion

        // 切换到版本独立设置
        private void BtnSwitch_Click(object sender, MouseButtonEventArgs e)
        {
            ModMinecraft.McVersionCurrent.Load();
            PageVersionLeft.Version = ModMinecraft.McVersionCurrent;
            ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.VersionSetup, FormMain.PageSubType.VersionSetup);
        }

    }
}