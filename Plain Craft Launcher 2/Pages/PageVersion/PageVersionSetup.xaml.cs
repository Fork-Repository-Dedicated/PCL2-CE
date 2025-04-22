using System;
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
    public partial class PageVersionSetup
    {

        private new bool IsLoaded = false;

        public PageVersionSetup()
        {
            this.Loaded += PageSetupSystem_Loaded;
        }

        private void PageSetupSystem_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();
            RefreshRam(false);

            // 由于各个版本不同，每次都需要重新加载
            ModAnimation.AniControlEnabled += 1;
            Reload();
            ModAnimation.AniControlEnabled -= 1;

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

            // 内存自动刷新
            var timer = new System.Windows.Threading.DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 1) };
            timer.Tick += (_, __) => RefreshRam();
            timer.Start();

        }
        public void Reload()
        {
            try
            {

                // 启动参数
                this.TextArgumentTitle.Text = Conversions.ToString(ModBase.Setup.Get("VersionArgumentTitle", Version: PageVersionLeft.Version));
                this.TextArgumentInfo.Text = Conversions.ToString(ModBase.Setup.Get("VersionArgumentInfo", Version: PageVersionLeft.Version));
                string _unused = PageVersionLeft.Version.PathIndie; // 触发自动判定
                this.ComboArgumentIndieV2.SelectedIndex = Conversions.ToBoolean(ModBase.Setup.Get("VersionArgumentIndieV2", Version: PageVersionLeft.Version)) ? 0 : 1;
                RefreshJavaComboBox();

                // 游戏内存
                ((MyRadioBox)this.FindName(Conversions.ToString(Operators.ConcatenateObject("RadioRamType", ModBase.Setup.Load("VersionRamType", Version: PageVersionLeft.Version))))).Checked = true;
                this.SliderRamCustom.Value = Conversions.ToInteger(ModBase.Setup.Get("VersionRamCustom", Version: PageVersionLeft.Version));
                this.ComboRamOptimize.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("VersionRamOptimize", Version: PageVersionLeft.Version));

                // 服务器
                this.TextServerEnter.Text = Conversions.ToString(ModBase.Setup.Get("VersionServerEnter", Version: PageVersionLeft.Version));
                this.ComboServerLogin.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("VersionServerLogin", Version: PageVersionLeft.Version));
                ComboServerLoginLast = this.ComboServerLogin.SelectedIndex;
                this.ServerLogin(this.ComboServerLogin.SelectedIndex);
                this.TextServerNide.Text = Conversions.ToString(ModBase.Setup.Get("VersionServerNide", Version: PageVersionLeft.Version));
                this.TextServerAuthServer.Text = Conversions.ToString(ModBase.Setup.Get("VersionServerAuthServer", Version: PageVersionLeft.Version));
                this.TextServerAuthName.Text = Conversions.ToString(ModBase.Setup.Get("VersionServerAuthName", Version: PageVersionLeft.Version));
                this.TextServerAuthRegister.Text = Conversions.ToString(ModBase.Setup.Get("VersionServerAuthRegister", Version: PageVersionLeft.Version));

                // 高级设置
                this.TextAdvanceJvm.Text = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceJvm", Version: PageVersionLeft.Version));
                this.TextAdvanceGame.Text = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceGame", Version: PageVersionLeft.Version));
                this.TextAdvanceRun.Text = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceRun", Version: PageVersionLeft.Version));
                this.CheckAdvanceRunWait.Checked = Conversions.ToBoolean(ModBase.Setup.Get("VersionAdvanceRunWait", Version: PageVersionLeft.Version));
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionAdvanceAssets", Version: PageVersionLeft.Version), 2, false)))
                {
                    ModBase.Log("[Setup] 已迁移老版本的关闭文件校验设置");
                    ModBase.Setup.Reset("VersionAdvanceAssets", Version: PageVersionLeft.Version);
                    ModBase.Setup.Set("VersionAdvanceAssetsV2", true, Version: PageVersionLeft.Version);
                }
                this.CheckAdvanceAssetsV2.Checked = Conversions.ToBoolean(ModBase.Setup.Get("VersionAdvanceAssetsV2", Version: PageVersionLeft.Version));
                this.CheckAdvanceJava.Checked = Conversions.ToBoolean(ModBase.Setup.Get("VersionAdvanceJava", Version: PageVersionLeft.Version));
                if (ModBase.IsArm64System)
                {
                    this.CheckAdvanceDisableJLW.Checked = true;
                    this.CheckAdvanceDisableJLW.IsEnabled = false;
                    this.CheckAdvanceDisableJLW.ToolTip = "在启动游戏时不使用 Java Wrapper 进行包装。&#xa;由于系统为 ARM64 架构，Java Wrapper 已被强制禁用。";
                }
                else
                {
                    this.CheckAdvanceDisableJLW.Checked = Conversions.ToBoolean(ModBase.Setup.Get("VersionAdvanceDisableJLW", Version: PageVersionLeft.Version));
                }
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "重载版本独立设置时出错", ModBase.LogLevel.Feedback);
            }
        }

        // 初始化
        public void Reset()
        {
            try
            {

                ModBase.Setup.Reset("VersionServerEnter", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionServerLogin", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionServerNide", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionServerAuthServer", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionServerAuthRegister", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionServerAuthName", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionArgumentTitle", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionArgumentInfo", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionArgumentIndieV2", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionRamType", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionRamCustom", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionRamOptimize", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceJvm", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceGame", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceAssets", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceAssetsV2", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceJava", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceDisableJlw", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceRun", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceRunWait", Version: PageVersionLeft.Version);
                ModBase.Setup.Reset("VersionAdvanceDisableJLW", Version: PageVersionLeft.Version);

                ModBase.Setup.Reset("VersionArgumentJavaSelect", Version: PageVersionLeft.Version);
                ModJava.JavaSearchLoader.Start(IsForceRestart: true);

                ModBase.Log("[Setup] 已初始化版本独立设置");
                ModMain.Hint("已初始化版本独立设置！", ModMain.HintType.Finish, false);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化版本独立设置失败", ModBase.LogLevel.Msgbox);
            }

            Reload();
        }

        // 将控件改变路由到设置改变
        private static void RadioBoxChange(MyRadioBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(sender.Tag.ToString().Split("/")[0], ModBase.Val(sender.Tag.ToString().Split("/")[1]), Version: PageVersionLeft.Version);
        }
        private static void TextBoxChange(MyTextBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
            {
                // #3194，不能删减 /
                // Dim HandledText As String = sender.Text
                // If sender.Tag = "VersionServerAuthServer" OrElse sender.Tag = "VersionServerAuthRegister" Then HandledText = HandledText.TrimEnd("/")
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Text, Version: PageVersionLeft.Version);
            }
        }
        private static void SliderChange(MySlider sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Value, Version: PageVersionLeft.Version);
        }
        private static void ComboChange(MyComboBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.SelectedIndex, Version: PageVersionLeft.Version);
        }
        private static void CheckBoxLikeComboChange(MyComboBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.SelectedIndex == 0, Version: PageVersionLeft.Version);
        }
        private static void CheckBoxChange(MyCheckBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Checked, Version: PageVersionLeft.Version);
        }

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
            if (this.LabRamGame is null || this.LabRamUsed is null || ModMain.FrmMain.PageCurrent != (FormMain.PageStackData)FormMain.PageType.VersionSetup || ModMain.FrmVersionLeft.PageID != FormMain.PageSubType.VersionSetup)
                return;
            // 获取内存情况
            double RamGame = Math.Round(GetRam(PageVersionLeft.Version), 5);
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
            this.LabRamWarn.Visibility = RamGame == 1d && !ModJava.JavaIs64Bit(PageVersionLeft.Version) && !ModBase.Is32BitSystem && ModJava.JavaList.Any() ? Visibility.Visible : Visibility.Collapsed;
            if (ShowAnim)
            {
                // 宽度动画
                ModAnimation.AniStart(new[] { ModAnimation.AaGridLengthWidth(this.ColumnRamUsed, RamUsed - this.ColumnRamUsed.Width.Value, 800, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), ModAnimation.AaGridLengthWidth(this.ColumnRamGame, RamGameActual - this.ColumnRamGame.Width.Value, 800, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), ModAnimation.AaGridLengthWidth(this.ColumnRamEmpty, RamEmpty - this.ColumnRamEmpty.Width.Value, 800, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)) }, "VersionSetup Ram Grid");
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
                            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.LabRamUsed, -this.LabRamUsed.Opacity, 100), ModAnimation.AaOpacity(this.LabRamTotal, -this.LabRamTotal.Opacity, 100), ModAnimation.AaOpacity(this.LabRamUsedTitle, -this.LabRamUsedTitle.Opacity, 100) }, "VersionSetup Ram TextLeft");
                            break;
                        }
                    case 1:
                        {
                            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.LabRamUsed, 1d - this.LabRamUsed.Opacity, 100), ModAnimation.AaOpacity(this.LabRamTotal, -this.LabRamTotal.Opacity, 100), ModAnimation.AaOpacity(this.LabRamUsedTitle, 0.7d - this.LabRamUsedTitle.Opacity, 100) }, "VersionSetup Ram TextLeft");
                            break;
                        }
                    case 2:
                        {
                            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.LabRamUsed, 1d - this.LabRamUsed.Opacity, 100), ModAnimation.AaOpacity(this.LabRamTotal, 1d - this.LabRamTotal.Opacity, 100), ModAnimation.AaOpacity(this.LabRamUsedTitle, 0.7d - this.LabRamUsedTitle.Opacity, 100) }, "VersionSetup Ram TextLeft");
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
                if (ModAnimation.AniControlEnabled == 0 && (RamTextRight != Right || ModAnimation.AniIsRun("VersionSetup Ram TextRight")))
                {
                    // 需要动画
                    ModAnimation.AniStart(new[] { ModAnimation.AaX(this.LabRamGame, TotalWidth - LabGameWidth - this.LabRamGame.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaX(this.LabRamGameTitle, TotalWidth - LabGameTitleWidth - this.LabRamGameTitle.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)) }, "VersionSetup Ram TextRight");
                }
                else
                {
                    // 不需要动画
                    this.LabRamGame.Margin = new Thickness(TotalWidth - LabGameWidth, 3d, 0d, 0d);
                    this.LabRamGameTitle.Margin = new Thickness(TotalWidth - LabGameTitleWidth, 0d, 0d, 5d);
                }
            }
            else if (ModAnimation.AniControlEnabled == 0 && (RamTextRight != Right || ModAnimation.AniIsRun("VersionSetup Ram TextRight")))
            {
                // 需要动画
                ModAnimation.AniStart(new[] { ModAnimation.AaX(this.LabRamGame, 2d + RectUsedWidth - this.LabRamGame.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaX(this.LabRamGameTitle, 2d + RectUsedWidth - this.LabRamGameTitle.Margin.Left, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)) }, "VersionSetup Ram TextRight");
            }
            else
            {
                // 不需要动画
                this.LabRamGame.Margin = new Thickness(2d + RectUsedWidth, 3d, 0d, 0d);
                this.LabRamGameTitle.Margin = new Thickness(2d + RectUsedWidth, 0d, 0d, 5d);
            }
            RamTextRight = Right;
        }

        /// <summary>
    /// 获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
        public static double GetRam(ModMinecraft.McVersion Version, bool? Is32BitJava = default)
        {
            // 跟随全局设置
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionRamType", Version: Version), 2, false)))
            {
                return PageSetupLaunch.GetRam(Version, true, Is32BitJava);
            }

            // ------------------------------------------
            // 修改下方代码时需要一并修改 PageSetupLaunch
            // ------------------------------------------

            // 使用当前版本的设置
            var RamGive = default(double);
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionRamType", Version: Version), 0, false)))
            {
                // 自动配置
                double RamAvailable = Math.Round(My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory / 1024d / 1024d / 1024d * 10d) / 10d;
                // 确定需求的内存值
                double RamMininum; // 无论如何也需要保证的最低限度内存
                double RamTarget1; // 估计能勉强带动了的内存
                double RamTarget2; // 估计没啥问题了的内存
                double RamTarget3; // 安装过多附加组件需要的内存
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
                int Value = Conversions.ToInteger(ModBase.Setup.Get("VersionRamCustom", Version: Version));
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
            if (Is32BitJava ?? !ModJava.JavaIs64Bit(PageVersionLeft.Version))
                RamGive = Math.Min(1d, RamGive);
            return RamGive;
        }

        #endregion

        #region 服务器

        // 全局
        private int ComboServerLoginLast;
        private void ComboServerLogin_Changed()
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            this.ServerLogin(this.ComboServerLogin.SelectedIndex);
            // 检查是否输入正确，正确才触发设置改变
            if (this.ComboServerLogin.SelectedIndex == 3 && !this.TextServerNide.IsValidated)
                return;
            if (this.ComboServerLogin.SelectedIndex == 4 && !this.TextServerAuthServer.IsValidated)
                return;
            // 检查结果是否发生改变，未改变则不触发设置改变
            if (ComboServerLoginLast == this.ComboServerLogin.SelectedIndex)
                return;
            // 触发
            ComboServerLoginLast = this.ComboServerLogin.SelectedIndex;
            PageVersionSetup.ComboChange(this.ComboServerLogin, (object)null);
        }
        public void ServerLogin(int Type)
        {
            if (this.LabServerNide is null)
                return;
            this.LabServerNide.Visibility = Type == 3 ? Visibility.Visible : Visibility.Collapsed;
            this.TextServerNide.Visibility = Type == 3 ? Visibility.Visible : Visibility.Collapsed;
            this.PanServerNide.Visibility = Type == 3 ? Visibility.Visible : Visibility.Collapsed;
            this.LabServerAuthName.Visibility = Type == 4 ? Visibility.Visible : Visibility.Collapsed;
            this.TextServerAuthName.Visibility = Type == 4 ? Visibility.Visible : Visibility.Collapsed;
            this.LabServerAuthRegister.Visibility = Type == 4 ? Visibility.Visible : Visibility.Collapsed;
            this.TextServerAuthRegister.Visibility = Type == 4 ? Visibility.Visible : Visibility.Collapsed;
            this.LabServerAuthServer.Visibility = Type == 4 ? Visibility.Visible : Visibility.Collapsed;
            this.TextServerAuthServer.Visibility = Type == 4 ? Visibility.Visible : Visibility.Collapsed;
            this.BtnServerAuthLittle.Visibility = Type == 4 ? Visibility.Visible : Visibility.Collapsed;
            this.CardServer.TriggerForceResize();
            // 避免微软登录、离线登录和第三方登录：统一通行证出现此提示
            if (!(Type == 4))
            {
                this.LabServerAuthServerSecurity.Visibility = Visibility.Collapsed;
                this.LabServerAuthServerSecurityCL.Visibility = Visibility.Collapsed;
                this.LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed;
            }
            // 如果开头为 http:// 给予警告
            else if (this.TextServerAuthServer.Text.StartsWithF("https://") && Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("ToolDownloadCert"), "False", false)))
            {
                this.LabServerAuthServerSecurity.Visibility = Visibility.Collapsed;
                this.LabServerAuthServerSecurityVerify.Visibility = Visibility.Visible;
                this.LabServerAuthServerSecurityCL.Visibility = Visibility.Visible;
            }
            else if (this.TextServerAuthServer.Text.StartsWithF("http://"))
            {
                this.LabServerAuthServerSecurity.Visibility = Visibility.Visible;
                this.LabServerAuthServerSecurityCL.Visibility = Visibility.Visible;
                this.LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed;
            }

            else
            {
                this.LabServerAuthServerSecurity.Visibility = Visibility.Collapsed;
                this.LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed;
                this.LabServerAuthServerSecurityCL.Visibility = Visibility.Collapsed;
            }
        }

        // 统一通行证
        private void BtnServerNideWeb_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://login.mc-user.com:233/server/intro");
        }

        // LittleSkin
        private void BtnServerAuthLittle_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.TextServerAuthServer.Text) && this.TextServerAuthServer.Text != "https://littleskin.cn/api/yggdrasil" && ModMain.MyMsgBox("即将把第三方登录设置覆盖为 LittleSkin 登录。" + Constants.vbCrLf + "除非你是服主，或者服主要求你这样做，否则请不要继续。" + Constants.vbCrLf + Constants.vbCrLf + "是否确实需要覆盖当前设置？", "设置覆盖确认", "继续", "取消") == 2)
                return;
            this.TextServerAuthServer.Text = "https://littleskin.cn/api/yggdrasil";
            this.TextServerAuthRegister.Text = "https://littleskin.cn/auth/register";
            this.TextServerAuthName.Text = "LittleSkin 登录";
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
            this.ComboArgumentJava.Items.Add(new MyComboBoxItem() { Content = "跟随全局设置", Tag = "使用全局设置" });
            this.ComboArgumentJava.Items.Add(new MyComboBoxItem() { Content = "自动选择合适的 Java", Tag = "自动选择" });
            // 更新列表
            MyComboBoxItem SelectedItem = null;
            string SelectedBySetup = Conversions.ToString(ModBase.Setup.Get("VersionArgumentJavaSelect", Version: PageVersionLeft.Version));
            try
            {
                foreach (var Java in ModJava.JavaList.Clone().OrderByDescending(v => v.VersionCode))
                {
                    var ListItem = new MyComboBoxItem() { Content = Java.ToString(), ToolTip = Java.PathFolder, Tag = Java };
                    ToolTipService.SetHorizontalOffset(ListItem, 400d);
                    this.ComboArgumentJava.Items.Add(ListItem);
                    // 判断人为选中
                    if (string.IsNullOrEmpty(SelectedBySetup) || SelectedBySetup == "使用全局设置")
                        continue;
                    if ((ModJava.JavaEntry.FromJson((JObject)ModBase.GetJson(SelectedBySetup)).PathFolder ?? "") == (Java.PathFolder ?? ""))
                        SelectedItem = ListItem;
                }
            }
            catch (Exception ex)
            {
                ModBase.Setup.Set("VersionArgumentJavaSelect", "使用全局设置", Version: PageVersionLeft.Version);
                ModBase.Log(ex, "更新版本设置 Java 下拉框失败", ModBase.LogLevel.Feedback);
            }
            // 更新选择项
            if (SelectedItem is null && ModJava.JavaList.Any())
            {
                if (string.IsNullOrEmpty(SelectedBySetup))
                {
                    SelectedItem = (MyComboBoxItem)this.ComboArgumentJava.Items[1]; // 选中 “自动选择”
                }
                else
                {
                    SelectedItem = (MyComboBoxItem)this.ComboArgumentJava.Items[0];
                } // 选中 “跟随全局设置”
            }
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
            if ("使用全局设置".Equals(SelectedJava))
            {
                // 选择 “自动”
                ModBase.Setup.Set("VersionArgumentJavaSelect", "使用全局设置", Version: PageVersionLeft.Version);
                ModBase.Log("[Java] 修改版本 Java 选择设置：跟随全局设置");
            }
            else if ("自动选择".Equals(SelectedJava))
            {
                // 选择 “自动”
                ModBase.Setup.Set("VersionArgumentJavaSelect", "", Version: PageVersionLeft.Version);
                ModBase.Log("[Java] 修改版本 Java 选择设置：自动选择");
            }
            else
            {
                // 选择指定项
                ModBase.Setup.Set("VersionArgumentJavaSelect", ((JObject)SelectedJava.ToJson()).ToString(Newtonsoft.Json.Formatting.None), Version: PageVersionLeft.Version);
                ModBase.Log("[Java] 修改版本 Java 选择设置：" + SelectedJava.ToString());
            }
            RefreshRam(true);
        }

        #endregion

        #region 其他设置

        // 版本隔离警告
        private bool IsReverting = false;
        private void ComboArgumentIndieV2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (IsReverting)
                return;
            if (ModMain.MyMsgBox("调整版本隔离后，你可能得把游戏存档、Mod 等文件手动迁移到新的游戏文件夹中。" + Constants.vbCrLf + "如果修改后发现存档消失，把这项设置改回来就能恢复。" + Constants.vbCrLf + "如果你不会迁移存档，不建议修改这项设置！", "警告", "我知道我在做什么", "取消", IsWarn: true) == 2)
            {
                IsReverting = true;
                this.ComboArgumentIndieV2.SelectedItem = e.RemovedItems[0];
                IsReverting = false;
            }
        }

        #endregion

        #region 高级设置

        private void TextAdvanceRun_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.CheckAdvanceRunWait.Visibility = string.IsNullOrEmpty(this.TextAdvanceRun.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        // 切换到全局设置
        private void BtnSwitch_Click(object sender, MouseButtonEventArgs e)
        {
            ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch);
        }

    }
}