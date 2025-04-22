using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL
{
    public class MyPageLeft : Grid
    {
        private int Uuid = ModBase.GetUuid();

        // 执行逐个进入动画的控件
        public FrameworkElement AnimatedControl
        {
            get
            {
                var res = GetValue(AnimatedControlProperty);
                if (res is null)
                    ModBase.Log($"[MyPageLeft] 获取到 AnimatedControl 的值为 null", ModBase.LogLevel.Debug);
                return (FrameworkElement)res;
            }
            set
            {
                SetValue(AnimatedControlProperty, value);
            }
        }
        public static DependencyProperty AnimatedControlProperty = DependencyProperty.Register("AnimatedControl", typeof(FrameworkElement), typeof(MyPageLeft));

        public void TriggerShowAnimation()
        {
            if (AnimatedControl is null)
            {
                // 缩放动画
                if (!(RenderTransform is ScaleTransform))
                {
                    RenderTransform = new ScaleTransform(0.96d, 0.96d);
                    RenderTransformOrigin = new Point(0.5d, 0.5d);
                }
                Opacity = 0d;
                ModAnimation.AniStart(new[] { ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX, 400, Ease: new ModAnimation.AniEaseOutBack((ModAnimation.AniEasePower)2)), ModAnimation.AaOpacity(this, 1d, 100) }, "PageLeft PageChange " + Uuid);
            }
            else
            {
                // 逐个进入动画
                var AniList = new List<ModAnimation.AniData>();
                int Id = 0;
                int Delay = 0;
                foreach (FrameworkElement Element in GetAllAnimControls(true))
                {
                    if (Element.Visibility == Visibility.Collapsed)
                    {
                        // 还原之前的隐藏动画可能导致的改变（#2436）
                        Element.Opacity = 1d;
                        Element.RenderTransform = new TranslateTransform(0d, 0d);
                        if (Element is MyListItem)
                            ((MyListItem)Element).IsMouseOverAnimationEnabled = true;
                    }
                    else
                    {
                        Element.Opacity = 0d;
                        Element.RenderTransform = new TranslateTransform(-25, 0d);
                        if (Element is MyListItem)
                            ((MyListItem)Element).IsMouseOverAnimationEnabled = false;
                        AniList.Add(ModAnimation.AaOpacity(Element, Element is TextBlock ? 0.6d : 1d, 200, Delay, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                        AniList.Add(ModAnimation.AaTranslateX(Element, 5d, 200, Delay, new ModAnimation.AniEaseOutFluent()));
                        AniList.Add(ModAnimation.AaTranslateX(Element, 20d, 300, Delay, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
                        if (Element is MyListItem)
                        {
                            AniList.Add(ModAnimation.AaCode(() =>
                                {
                                    ((MyListItem)Element).IsMouseOverAnimationEnabled = true;
                                    ((MyListItem)Element).RefreshColor(this, new EventArgs());
                                }, Delay + 280));
                        }
                        Delay = (int)Math.Round(Delay + Math.Max(8, 20 - Id) * 2.5d);
                        Id += 1;
                    }
                }
                ModAnimation.AniStart(AniList, "PageLeft PageChange " + Uuid);
            }
        }
        public void TriggerHideAnimation()
        {
            if (AnimatedControl is null)
            {
                // 缩放动画
                if (!(RenderTransform is ScaleTransform))
                {
                    RenderTransform = new ScaleTransform(1d, 1d);
                    RenderTransformOrigin = new Point(0.5d, 0.5d);
                }
                ModAnimation.AniStart(new[] { ModAnimation.AaScaleTransform(this, 0.95d - ((ScaleTransform)RenderTransform).ScaleX, 130, Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaOpacity(this, -Opacity, 100, 30) }, "PageLeft PageChange " + Uuid);
            }
            else
            {
                // 逐个退出动画
                var AniList = new List<ModAnimation.AniData>();
                int Id = 0;
                var Controls = GetAllAnimControls();
                foreach (FrameworkElement Element in Controls)
                {
                    AniList.Add(ModAnimation.AaOpacity(Element, -Element.Opacity, 60, (int)Math.Round(80d / Controls.Count * Id)));
                    AniList.Add(ModAnimation.AaTranslateX(Element, -6, 60, (int)Math.Round(80d / Controls.Count * Id)));
                    Id += 1;
                }
                ModAnimation.AniStart(AniList, "PageLeft PageChange " + Uuid);
            }
        }

        // 遍历获取所有需要生成动画的控件
        private List<FrameworkElement> GetAllAnimControls(bool IgnoreInvisibility = false)
        {
            var AllControls = new List<FrameworkElement>();
            GetAllAnimControls(AnimatedControl, ref AllControls, IgnoreInvisibility);
            return AllControls;
        }
        private void GetAllAnimControls(FrameworkElement Element, ref List<FrameworkElement> AllControls, bool IgnoreInvisibility)
        {
            if (!IgnoreInvisibility && Element.Visibility == Visibility.Collapsed)
                return;
            if (Element is MyTextButton)
            {
                AllControls.Add(Element);
            }
            else if (Element is MyListItem)
            {
                AllControls.Add(Element);
            }
            else if (Element is ContentControl)
            {
                GetAllAnimControls((FrameworkElement)((ContentControl)Element).Content, ref AllControls, IgnoreInvisibility);
            }
            else if (Element is Panel)
            {
                foreach (FrameworkElement Element2 in ((Panel)Element).Children)
                    GetAllAnimControls(Element2, ref AllControls, IgnoreInvisibility);
            }
            else
            {
                AllControls.Add(Element);
            }
        }

    }

    public interface IRefreshable
    {
        void Refresh();
    }
}