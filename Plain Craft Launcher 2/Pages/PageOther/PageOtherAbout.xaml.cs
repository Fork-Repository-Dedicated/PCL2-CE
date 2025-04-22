using System;
using System.Windows;
using System.Windows.Input;

namespace PCL
{
    public partial class PageOtherAbout
    {

        private new bool IsLoaded = false;

        public PageOtherAbout()
        {
            this.Loaded += PageOtherAbout_Loaded;
        }
        private void PageOtherAbout_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

            this.ItemAboutPcl.Info = this.ItemAboutPcl.Info.Replace("%VERSION%", ModBase.VersionBaseName).Replace("%VERSIONCODE%", ModBase.VersionCode.ToString()).Replace("%BRANCH%", ModBase.VersionBranchName).Replace("%COMMIT_HASH%", ModBase.CommitHashShort).Replace("%UPSTREAM_VERSION%", ModBase.UpstreamVersion);

        }

        private void BtnAboutBmclapi_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://afdian.com/a/bangbang93");
        }
        private void BtnAboutWiki_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://www.mcmod.cn");
        }

        private void ImgPCLCommunity_Click(object sender, MouseButtonEventArgs e)
        {
            ModAnimation.AniStart(new[] { ModAnimation.AaRotateTransform(sender, 360d) });
        }

        // 彩蛋
        private int ClickCount = 0;
        private void ImgPCLLogo_Click(object sender, MouseButtonEventArgs e)
        {
            if (ClickCount < 200)
            {
                ClickCount += 1;
                switch (ClickCount)
                {
                    case 5:
                        {
                            ModMain.Hint("点这个很好玩么……");
                            break;
                        }
                    case 15:
                        {
                            ModMain.Hint("还点？");
                            break;
                        }
                    case 25:
                        {
                            switch (ModMain.MyMsgBox("你现在是不是超无聊的？", "咕咕咕？", Button1: "是的", Button2: "并不是"))
                            {
                                case 2:
                                    {
                                        ModMain.Hint("那你还点啥……真是搞不懂。");
                                        break;
                                    }
                            }

                            break;
                        }
                    case 50:
                        {
                            ModMain.Hint("嗯，加油吧，嗯……");
                            break;
                        }
                    case 75:
                        {
                            ModMain.Hint("隐藏主题 混乱黄 已……嗯不对，这是 PCL2 社区版，应该没有这玩意……");
                            break;
                        }
                    case 100:
                        {
                            ModMain.Hint("你咋还这么无聊啊？");
                            break;
                        }
                    case 130:
                        {
                            ModMain.Hint("后面什么都没有了哦！");
                            break;
                        }
                    case 150:
                        {
                            switch (ModMain.MyMsgBox("你真的不累么？", "温馨提示", "累死了", "真的不累"))
                            {
                                case 1:
                                    {
                                        ModMain.Hint("那你就别点了喂……后面真的真的真的什么都没有了！");
                                        break;
                                    }
                                case 2:
                                    {
                                        switch (ModMain.MyMsgBox("你真的真的不累么？", "超温馨的温馨提示", "累死了", "真的真的不累"))
                                        {
                                            case 1:
                                                {
                                                    ModMain.Hint("那你就别点了喂……后面真的真的真的什么都没有了！");
                                                    break;
                                                }
                                            case 2:
                                                {
                                                    switch (ModMain.MyMsgBox("你真的真的真的不累么？", "超超超温馨的温馨提示", "累死了", "真的真的真的不累"))
                                                    {
                                                        case 1:
                                                            {
                                                                ModMain.Hint("那你就别点了喂……后面真的真的真的什么都没有了！");
                                                                break;
                                                            }
                                                        case 2:
                                                            {
                                                                ModMain.Hint("好吧……不过后面是真的啥也没了，不用点了真的。");
                                                                break;
                                                            }
                                                    }

                                                    break;
                                                }
                                        }

                                        break;
                                    }
                            }

                            break;
                        }
                    case 200:
                        {
                            ModMain.Hint("还点，还点就不让你点了……");
                            this.ImgPCLLogo.IsHitTestVisible = false;
                            return;
                        }
                }
                var rand = new Random();
                int mx = rand.Next(-1, 1);
                if (mx == 0)
                    mx = 1;
                int my = rand.Next(-1, 1);
                if (my == 0)
                    my = 1;
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateX(sender, mx, Time: 0), ModAnimation.AaTranslateY(sender, my, Time: 0), ModAnimation.AaTranslateX(sender, -mx, Time: 0, Delay: 100), ModAnimation.AaTranslateY(sender, -my, Time: 0, Delay: 100) });
            }
        }

    }
}