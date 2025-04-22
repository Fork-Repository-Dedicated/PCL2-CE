using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public class MyScrollViewer : ScrollViewer
    {

        public double DeltaMult { get; set; } = 1d;


        private double RealOffset;

        public MyScrollViewer()
        {
            PreviewMouseWheel += MyScrollViewer_PreviewMouseWheel;
            ScrollChanged += MyScrollViewer_ScrollChanged;
            IsVisibleChanged += MyScrollViewer_IsVisibleChanged;
            Loaded += (_, __) => Load();
            PreviewGotKeyboardFocus += MyScrollViewer_PreviewGotKeyboardFocus;
        }
        private void MyScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0 || ActualHeight == 0d || ScrollableHeight == 0d)
                return;
            var SourceType = e.Source.GetType();
            if (((dynamic)Content).TemplatedParent is null && (typeof(ComboBox).IsAssignableFrom(SourceType) && ((ComboBox)e.Source).IsDropDownOpen || typeof(TextBox).IsAssignableFrom(SourceType) && ((TextBox)e.Source).AcceptsReturn || typeof(ComboBoxItem).IsAssignableFrom(SourceType) || e.Source is CheckBox))
            {
                // 如果当前是在对有滚动条的下拉框或文本框执行，则不接管操作
                return;
            }
            e.Handled = true;
            PerformVerticalOffsetDelta(-e.Delta);
            // 关闭 Tooltip (#2552)
            foreach (var TooltipBorder in Application.ShowingTooltips)
                ModAnimation.AniStart(ModAnimation.AaOpacity(TooltipBorder, -1, 100), $"Hide Tooltip {ModBase.GetUuid()}");
        }
        public void PerformVerticalOffsetDelta(double Delta)
        {
            ModAnimation.AniStart(ModAnimation.AaDouble((double AnimDelta) =>
{
RealOffset = ModBase.MathClamp(RealOffset + AnimDelta, 0d, ExtentHeight - ActualHeight);
ScrollToVerticalOffset(RealOffset);
}, Delta * DeltaMult, 300, 0, new ModAnimation.AniEaseOutFluent((ModAnimation.AniEasePower)6), After: false));
        }
        private void MyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            RealOffset = VerticalOffset;
            if (ModMain.FrmMain is not null && (Conversions.ToBoolean(e.VerticalChange) || Conversions.ToBoolean(e.ViewportHeightChange)))
                ModMain.FrmMain.BtnExtraBack.ShowRefresh();
        }
        private void MyScrollViewer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ModMain.FrmMain.BtnExtraBack.ShowRefresh();
        }

        public MyScrollBar ScrollBar;
        private void Load()
        {
            ScrollBar = (MyScrollBar)GetTemplateChild("PART_VerticalScrollBar");
        }

        private void MyScrollViewer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus is not null && e.NewFocus is MySlider)
                e.Handled = true; // #3854，阻止获得焦点时自动滚动
        }
    }
}