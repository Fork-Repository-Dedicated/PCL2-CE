using System;
using System.Windows;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public partial class PageSetupLink
    {

        private new bool IsLoaded = false;

        public PageSetupLink()
        {
            this.Loaded += PageSetupLink_Loaded;
        }

        private void PageSetupLink_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

            ModAnimation.AniControlEnabled += 1;
            Reload();
            ModAnimation.AniControlEnabled -= 1;

        }
        public void Reload()
        {
            this.TextLinkRelay.Text = Conversions.ToString(ModBase.Setup.Get("LinkName"));
        }

        // 初始化
        public void Reset()
        {
            try
            {
                ModBase.Setup.Reset("LinkName");

                ModBase.Log("[Setup] 已初始化联机页设置");
                ModMain.Hint("已初始化联机页设置！", ModMain.HintType.Finish, false);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化联机页设置失败", ModBase.LogLevel.Msgbox);
            }

            Reload();
        }

        // 将控件改变路由到设置改变
        private static void TextBoxChange(MyTextBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Text);
        }

    }
}