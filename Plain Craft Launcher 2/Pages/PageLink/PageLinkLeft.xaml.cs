using System;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageLinkLeft
    {

        private bool IsLoad = false;
        private bool IsPageSwitched = false; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

        public PageLinkLeft()
        {
            this.Loaded += PageLinkLeft_Loaded;
            this.Unloaded += PageOtherLeft_Unloaded;
        }
        private void PageLinkLeft_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoad)
                return;
            IsLoad = true;
            // 切换默认页面
            if (IsPageSwitched)
                return;
            this.ItemLobby.SetChecked(true, false, false);
        }
        private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
        {
            IsPageSwitched = false;
        }

        #region 页面切换

        /// <summary>
    /// 当前页面的编号。
    /// </summary>
        public FormMain.PageSubType PageID = FormMain.PageSubType.LinkLobby;

        /// <summary>
    /// 勾选事件改变页面。
    /// </summary>
        private void PageCheck(MyListItem sender, ModBase.RouteEventArgs e)
        {
            // 尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
            // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
            if (sender.Tag is not null)
                PageChange((FormMain.PageSubType)Math.Round(ModBase.Val(sender.Tag)));
        }

        public object PageGet(FormMain.PageSubType ID = -1)
        {
            if ((int)ID == -1)
                ID = PageID;
            switch (ID)
            {
                case 0:
                case FormMain.PageSubType.LinkLobby:
                    {
                        if (ModMain.FrmLinkLobby is null)
                            ModMain.FrmLinkLobby = new PageLinkLobby();
                        return ModMain.FrmLinkLobby;
                    }
                case FormMain.PageSubType.LinkIoi:
                    {
                        if (ModMain.FrmLinkIoi is null)
                            ModMain.FrmLinkIoi = new PageLinkIoi();
                        return ModMain.FrmLinkIoi;
                    }
                case FormMain.PageSubType.LinkSetup:
                    {
                        if (ModMain.FrmSetupLink is null)
                            ModMain.FrmSetupLink = new PageSetupLink();
                        return ModMain.FrmSetupLink;
                    }
                case FormMain.PageSubType.LinkHelp:
                    {
                        var page = new PageOtherHelpDetail();
                        var panel = new StackPanel() { Orientation = Orientation.Vertical };
                        var card = new MyCard() { Height = 200d };
                        panel.Children.Add(card);
                        var textblock = new TextBlock()
                        {
                            Text = "暂时没写好 qwq",
                            FontSize = 20d,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        card.Children.Add(textblock);
                        page.PanCustom.Children.Add(panel);
                        if (ModMain.FrmLinkHelp is null)
                            ModMain.FrmLinkHelp = page;
                        return ModMain.FrmLinkHelp;
                    }
                case FormMain.PageSubType.LinkFeedback:
                    {
                        if (ModMain.FrmLinkFeedback is null)
                            ModMain.FrmLinkFeedback = new PageLinkFeedback();
                        return ModMain.FrmLinkFeedback;
                    }
                case FormMain.PageSubType.LinkNetStatus:
                    {
                        if (ModMain.FrmLinkNetStatus is null)
                            ModMain.FrmLinkNetStatus = new PageLinkNetStatus();
                        return ModMain.FrmLinkNetStatus;
                    }

                default:
                    {
                        throw new Exception("未知的更多子页面种类：" + ((int)ID).ToString());
                    }
            }
        }

        /// <summary>
    /// 切换现有页面。
    /// </summary>
        public void PageChange(FormMain.PageSubType ID)
        {
            if (PageID == ID)
                return;
            ModAnimation.AniControlEnabled += 1;
            IsPageSwitched = true;
            try
            {
                PageChangeRun((MyPageRight)PageGet(ID));
                PageID = ID;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "切换分页面失败（ID " + ((int)ID).ToString() + "）", ModBase.LogLevel.Feedback);
            }
            finally
            {
                ModAnimation.AniControlEnabled -= 1;
            }
        }
        private static void PageChangeRun(MyPageRight Target)
        {
            ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
            if (Target.Parent is not null)
                Target.SetValue(ContentPresenter.ContentProperty, null);
            ModMain.FrmMain.PageRight = Target;
            ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnExit();
            ModAnimation.AniStart(new[] {
                         ModAnimation.AaCode(() =>
                {
                                    ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnForceExit();
                                    ModMain.FrmMain.PanMainRight.Child = ModMain.FrmMain.PageRight;
                                    ModMain.FrmMain.PageRight.Opacity = 0d;
                                }, 130),
                         ModAnimation.AaCode(() =>
                {
                                    // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                                    ModMain.FrmMain.PageRight.Opacity = 1d;
                                    ModMain.FrmMain.PageRight.PageOnEnter();
                                }, 30, true)
                     }, "PageLeft PageChange");
        }

        #endregion

        public void Reset(object sender, EventArgs e)
        {
            if (ModMain.MyMsgBox("是否要初始化联机页的所有设置？该操作不可撤销。", "初始化确认", Button2: "取消", IsWarn: true) == 1)
            {
                if (ModMain.FrmSetupLink == null)
                    ModMain.FrmSetupLink = new PageSetupLink();
                ModMain.FrmSetupLink.Reset();
                this.ItemSetup.Checked = true;
            }
        }

        public void Recheck(object sender, EventArgs e)
        {
            if (ModMain.FrmLinkNetStatus == null)
                ModMain.FrmLinkNetStatus = new PageLinkNetStatus();
            ModMain.Hint("正在重新检测网络环境，请稍后...");
            ModMain.FrmLinkNetStatus.NetStatusTest();
            this.ItemNetStatus.Checked = true;
        }
        public void NetStatusUpdate(string Status)
        {
            this.ItemNetStatus.Title = Status;
        }

    }
}