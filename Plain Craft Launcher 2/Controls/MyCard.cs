using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{


    // 控件
    public class MyCard : Grid
    {
        private readonly Grid MainGrid;
        public SystemDropShadowChrome MainChrome { get; private set; }
        private readonly Border MainBorder;
        private bool IsThemeChanging = false;
        public UIElement BorderChild
        {
            get
            {
                return MainBorder.Child;
            }
            set
            {
                MainBorder.Child = value;
            }
        }
        private TextBlock _MainTextBlock;
        public TextBlock MainTextBlock
        {
            get
            {
                Init(); // 当父级触发 Loaded 时，本卡片可能尚未触发 Loaded（该事件从父级向子级调用），因此这会是 null。手动触发以确保控件已加载。
                return _MainTextBlock;
            }
            set
            {
                _MainTextBlock = value;
            }
        }
        private System.Windows.Shapes.Path _MainSwap;
        public System.Windows.Shapes.Path MainSwap
        {
            get
            {
                Init();
                return _MainSwap;
            }
            set
            {
                _MainSwap = value;
            }
        }

        // 属性
        public int Uuid = ModBase.GetUuid();
        public InlineCollection Inlines
        {
            get
            {
                return MainTextBlock.Inlines;
            }
        }
        public CornerRadius CornerRadius
        {
            get
            {
                return MainChrome.CornerRadius;
            }
            set
            {
                MainChrome.CornerRadius = value;
                MainBorder.CornerRadius = value;
            }
        }
        public string Title
        {
            get
            {
                return Conversions.ToString(GetValue(TitleProperty));
            }
            set
            {
                SetValue(TitleProperty, value);
                if (_MainTextBlock is not null)
                    MainTextBlock.Text = value;
            }
        }
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(MyCard), new PropertyMetadata(""));

        private async void _ThemeChanged(object sender, bool e)
        {
            if (e)
            {
                IsThemeChanging = true;
                ModAnimation.AniStart(new[] { ModAnimation.AaColor(MainBorder, Border.BackgroundProperty, new ModBase.MyColor(235d, 43d, 43d, 43d) - MainBorder.Background, 300) }, "MyCard Theme " + Uuid);
                await Task.Delay(300);
                IsThemeChanging = false;
            }
            else
            {
                IsThemeChanging = true;
                ModAnimation.AniStart(new[] { ModAnimation.AaColor(MainBorder, Border.BackgroundProperty, new ModBase.MyColor(205d, 255d, 255d, 255d) - MainBorder.Background, 300) }, "MyCard Theme " + Uuid);
                await Task.Delay(300);
                IsThemeChanging = false;

            }
        }

        // UI 建立
        public MyCard()
        {
            ModSecret.ThemeChanged += _ThemeChanged;
            MainChrome = new SystemDropShadowChrome() { Margin = new Thickness(-9.5d, -9, 0.5d, -0.5d), Opacity = 0.1d, CornerRadius = new CornerRadius(6d) };
            MainChrome.SetResourceReference(SystemDropShadowChrome.ColorProperty, "ColorObject1");
            Children.Insert(0, MainChrome);
            MainBorder = new Border() { Background = new SolidColorBrush(Color.FromArgb((byte)(ModSecret.IsDarkMode ? 235 : 205), (byte)(ModSecret.IsDarkMode ? 43 : 255), (byte)(ModSecret.IsDarkMode ? 43 : 255), (byte)(ModSecret.IsDarkMode ? 43 : 255))), CornerRadius = new CornerRadius(6d), IsHitTestVisible = false };
            Children.Insert(1, MainBorder);
            MainGrid = new Grid();
            Children.Add(MainGrid);
            Loaded += (_, __) => Init();
            MouseEnter += MyCard_MouseEnter;
            MouseLeave += MyCard_MouseLeave;
            SizeChanged += MySizeChanged;
            MouseLeftButtonDown += MyCard_MouseLeftButtonDown;
            MouseLeftButtonUp += MyCard_MouseLeftButtonUp;
            MouseLeave += MyCard_MouseLeave_Swap;
        }
        private bool IsLoad = false;
        private void Init()
        {
            if (IsLoad)
                return;
            IsLoad = true;
            // 初次加载限定
            if (MainTextBlock is null)
            {
                MainTextBlock = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(15d, 12d, 0d, 0d), FontWeight = FontWeights.Bold, FontSize = 13d, IsHitTestVisible = false };
                MainTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
                MainTextBlock.SetBinding(TextBlock.TextProperty, new Binding("Title") { Source = this, Mode = BindingMode.OneWay });
                MainGrid.Children.Add(MainTextBlock);
            }
            if (CanSwap || SwapControl is not null)
            {
                if (SwapControl is null && Children.Count > 3)
                    SwapControl = Children[3];
                MainSwap = new System.Windows.Shapes.Path() { HorizontalAlignment = HorizontalAlignment.Right, Stretch = Stretch.Uniform, Height = 6d, Width = 10d, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0d, 17d, 16d, 0d), Data = (Geometry)new GeometryConverter().ConvertFromString("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"), RenderTransform = new RotateTransform(180d), RenderTransformOrigin = new Point(0.5d, 0.5d) };
                MainSwap.SetResourceReference(Shape.FillProperty, "ColorBrush1");
                MainGrid.Children.Add(MainSwap);
            }
            // 改变默认的折叠
            if (IsSwaped && SwapControl is not null)
            {
                MainSwap.RenderTransform = new RotateTransform(SwapLogoRight ? 270 : 0);
                // 取消由于高度变化被迫触发的高度动画
                bool RawUseAnimation = UseAnimation;
                UseAnimation = false;
                Height = SwapedHeight;
                ModAnimation.AniStop("MyCard Height " + Uuid);
                IsHeightAnimating = false;
                ModBase.RunInUi(() => UseAnimation = RawUseAnimation, true);
            }
        }
        public void StackInstall()
        {
            StackPanel argStack = (StackPanel)SwapControl;
            StackInstall(ref argStack, InstallMethod);
            SwapControl = argStack;
            TriggerForceResize();
        }
        public static void StackInstall(ref StackPanel Stack, Action<StackPanel> InstallMethod)
        {
            if (Stack.Tag is null)
                return;
            try
            {
                InstallMethod(Stack);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[MyCard] InstallMethod 调用失败");
            }
            Stack.Children.Add(new FrameworkElement() { Height = 18d }); // 下边距，同时适应折叠
            Stack.Tag = null;
        }

        // 事件
        public bool HasMouseAnimation { get; set; } = true;
        private void MyCard_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!HasMouseAnimation)
                return;
            var AniList = new List<ModAnimation.AniData>();
            if (!(MainTextBlock == null))
                AniList.Add(ModAnimation.AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush2", 150));
            if (!(MainSwap == null))
                AniList.Add(ModAnimation.AaColor(MainSwap, Shape.FillProperty, "ColorBrush2", 150));
            AniList.AddRange(new[] { ModAnimation.AaColor(MainChrome, SystemDropShadowChrome.ColorProperty, "ColorObject2", 180), ModAnimation.AaColor(MainBorder, Border.BackgroundProperty, new ModBase.MyColor(ModSecret.IsDarkMode ? 245 : 230, ModSecret.IsDarkMode ? 48 : 255, ModSecret.IsDarkMode ? 48 : 255, ModSecret.IsDarkMode ? 48 : 255) - MainBorder.Background, 180), ModAnimation.AaOpacity(MainChrome, 0.3d - MainChrome.Opacity, 180) });
            if (!IsThemeChanging)
                ModAnimation.AniStart(AniList, "MyCard Mouse " + Uuid);
        }
        private void MyCard_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!HasMouseAnimation)
                return;
            var AniList = new List<ModAnimation.AniData>();
            if (!(MainTextBlock == null))
                AniList.Add(ModAnimation.AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush1", 250));
            if (!(MainSwap == null))
                AniList.Add(ModAnimation.AaColor(MainSwap, Shape.FillProperty, "ColorBrush1", 250));
            AniList.AddRange(new[] { ModAnimation.AaColor(MainChrome, SystemDropShadowChrome.ColorProperty, "ColorObject1", 300), ModAnimation.AaColor(MainBorder, Border.BackgroundProperty, new ModBase.MyColor(ModSecret.IsDarkMode ? 235 : 205, ModSecret.IsDarkMode ? 43 : 255, ModSecret.IsDarkMode ? 43 : 255, ModSecret.IsDarkMode ? 43 : 255) - MainBorder.Background, 300), ModAnimation.AaOpacity(MainChrome, 0.1d - MainChrome.Opacity, 300) });
            if (!IsThemeChanging)
                ModAnimation.AniStart(AniList, "MyCard Mouse " + Uuid);
        }

        #region 高度改变动画

        /// <summary>
    /// 是否启用高度改变动画。
    /// </summary>
        public bool UseAnimation { get; set; } = true;
        private bool IsHeightAnimating = false;
        private double ActualUsedHeight; // 回滚实际高度（例如 NaN）
        private void MySizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!UseAnimation)
                return;
            double DeltaHeight = (IsSwaped ? SwapedHeight : e.NewSize.Height) - e.PreviousSize.Height;
            // 卡片的进入时动画已被页面通用切换动画替代
            if (e.PreviousSize.Height == 0d || IsHeightAnimating || Math.Abs(DeltaHeight) < 1d || ActualHeight == 0d)
                return;
            StartHeightAnimation(DeltaHeight, e.PreviousSize.Height, false);
        }
        private void StartHeightAnimation(double DeltaHeight, double PreviousHeight, bool IsLoadAnimation)
        {
            if (IsHeightAnimating || ModMain.FrmMain is null)
                return; // 避免 XAML 设计器出错

            var AnimList = new List<ModAnimation.AniData>();
            if (DeltaHeight > 10d || DeltaHeight < -10 && !(SwapControl == null)) // 如果不是需要折叠的卡片，高度减小时的弹跳会吞掉按钮下边框
            {
                // 高度增加较大，使用弹起动画
                double Delta = ModBase.MathClamp(Math.Abs(DeltaHeight) * 0.05d, 3d, 10d) * Math.Sign(DeltaHeight);
                AnimList.AddRange(new[] { ModAnimation.AaHeight(this, DeltaHeight + Delta, 300, IsLoadAnimation ? 30 : 0, (ModAnimation.AniEase)(DeltaHeight > ModMain.FrmMain.Height ? new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.ExtraStrong) : new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong))), ModAnimation.AaHeight(this, -Delta, 150, 260, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)) });
            }
            else
            {
                // 普通的改变就行啦
                AnimList.AddRange(new[] { ModAnimation.AaHeight(this, DeltaHeight, (int)Math.Round(ModBase.MathClamp(Math.Abs(DeltaHeight) * 4d, 150d, 250d)), Ease: new ModAnimation.AniEaseOutFluent()) });
            }
            AnimList.Add(ModAnimation.AaCode(() =>
        {
            IsHeightAnimating = false;
            Height = ActualUsedHeight;
            if (IsSwaped)
                ((dynamic)SwapControl).Visibility = Visibility.Collapsed;
        }, After: true));
            ModAnimation.AniStart(AnimList, "MyCard Height " + Uuid);

            IsHeightAnimating = true;
            ActualUsedHeight = IsSwaped ? SwapedHeight : Height;
            Height = PreviousHeight;
        }
        /// <summary>
    /// 通知 MyCard，控件内容已改变，需要中断动画并更新高度。
    /// </summary>
        public void TriggerForceResize()
        {
            Height = IsSwaped ? SwapedHeight : double.NaN;
            ModAnimation.AniStop("MyCard Height " + Uuid);
            IsHeightAnimating = false;
        }

        #endregion

        #region 折叠

        // 若设置了 CanSwap，或 SwapControl 不为空，则判定为会进行折叠
        // 这是因为不能直接在 XAML 中设置 SwapControl
        public object SwapControl;
        public bool CanSwap { get; set; } = false;

        /// <summary>
    /// 数据转为列表项的转换方法
    /// </summary>
    /// <returns></returns>
        public Action<StackPanel> InstallMethod { get; set; }

        /// <summary>
    /// 是否已被折叠。
    /// </summary>
        public bool IsSwaped
        {
            get
            {
                return _IsSwaped;
            }
            set
            {
                if (_IsSwaped == value)
                    return;
                _IsSwaped = value;
                if (SwapControl is null)
                    return;
                // 展开
                if (!IsSwaped && SwapControl is StackPanel)
                {
                    StackPanel argStack = (StackPanel)SwapControl;
                    StackInstall(ref argStack, InstallMethod);
                    SwapControl = argStack;
                }
                // 若尚未加载，会在 Loaded 事件中触发无动画的折叠，不需要在这里进行
                if (!IsLoaded)
                    return;
                // 更新高度
                ((dynamic)SwapControl).Visibility = Visibility.Visible;
                TriggerForceResize();
                // 改变箭头
                ModAnimation.AniStart(ModAnimation.AaRotateTransform(MainSwap, (_IsSwaped ? SwapLogoRight ? 270 : 0 : 180) - ((RotateTransform)MainSwap.RenderTransform).Angle, 400, Ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), "MyCard Swap " + Uuid, true);
            }
        }
        private bool _IsSwaped = false;

        public bool SwapLogoRight { get; set; } = false;
        private bool IsMouseDown = false;
        public event PreviewSwapEventHandler PreviewSwap;

        public delegate void PreviewSwapEventHandler(object sender, ModBase.RouteEventArgs e);
        public event SwapEventHandler Swap;

        public delegate void SwapEventHandler(object sender, ModBase.RouteEventArgs e);
        public const int SwapedHeight = 40;
        private void MyCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            double Pos = Mouse.GetPosition(this).Y;
            if (!IsSwaped && (SwapControl is null || Pos > (IsSwaped ? SwapedHeight : SwapedHeight - 6) || Pos == 0d && !IsMouseDirectlyOver))
                return; // 检测点击位置；或已经不在可视树上的误判
            IsMouseDown = true;
        }
        private void MyCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsMouseDown)
                return;
            IsMouseDown = false;

            double Pos = Mouse.GetPosition(this).Y;
            if (!IsSwaped && (SwapControl is null || Pos > (IsSwaped ? SwapedHeight : SwapedHeight - 6) || Pos == 0d && !IsMouseDirectlyOver))
                return; // 检测点击位置；或已经不在可视树上的误判

            var ee = new ModBase.RouteEventArgs(true);
            PreviewSwap?.Invoke(this, ee);
            if (ee.Handled)
            {
                IsMouseDown = false;
                return;
            }

            IsSwaped = !IsSwaped;
            ModBase.Log("[Control] " + (IsSwaped ? "折叠卡片" : "展开卡片") + (Title is null ? "" : "：" + Title));
            Swap?.Invoke(this, ee);
        }
        private void MyCard_MouseLeave_Swap(object sender, MouseEventArgs e)
        {
            IsMouseDown = false;
        }

        #endregion

    }

    public static partial class ModAnimation
    {
        public static void AniDispose(MyCard Control, bool RemoveFromChildren, ParameterizedThreadStart CallBack = null)
        {
            if (Control.IsHitTestVisible)
            {
                Control.IsHitTestVisible = false;
                AniStart(new[] {
                AaScaleTransform(Control, -0.08d, 200, Ease: new AniEaseInFluent()),
                AaOpacity(Control, -1, 200, Ease: new AniEaseOutFluent()),
                AaHeight(Control, -Control.ActualHeight, 150, 100, new AniEaseOutFluent()),
                                ModAnimation.AaCode(() =>
                    {
                    if (RemoveFromChildren)
                        {
                        if (Control.Parent is null)
                                return;
                        ((object)Control.Parent).Children.Remove(Control);
                        }
                    else
                        {
                        Control.Visibility = Visibility.Collapsed;
                    }
                    if (CallBack is not null)
                            CallBack(Control);
                }, After: true)
            }, "MyCard Dispose " + Control.Uuid);
            }
            else
            {
                if (RemoveFromChildren)
                {
                    if (Control.Parent is null)
                        return;
                    ((object)Control.Parent).Children.Remove(Control);
                }
                else
                {
                    Control.Visibility = Visibility.Collapsed;
                }
                if (CallBack is not null)
                    CallBack(Control);
            }
        }
    }
}