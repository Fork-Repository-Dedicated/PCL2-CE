using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using static PCL.FormMain;

namespace PCL
{
    public partial class PageOtherLeft
    {

        private bool IsLoad = false;
        private bool IsPageSwitched = false; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次
        private void PageOtherLeft_Loaded(object sender, RoutedEventArgs e)
        {
            // 是否处于隐藏的子页面
            bool IsHiddenPage = false;
            if (this.ItemHelp.Checked && (bool)ModBase.Setup.Get("UiHiddenOtherHelp"))
                IsHiddenPage = true;
            if (this.ItemAbout.Checked && (bool)ModBase.Setup.Get("UiHiddenOtherAbout"))
                IsHiddenPage = true;
            if (this.ItemTest.Checked && (bool)ModBase.Setup.Get("UiHiddenOtherTest"))
                IsHiddenPage = true;
            if (PageSetupUI.HiddenForceShow)
                IsHiddenPage = false;
            // 若页面错误，或尚未加载，则继续
            if (IsLoad && !IsHiddenPage)
                return;
            IsLoad = true;
            // 刷新子页面隐藏情况
            PageSetupUI.HiddenRefresh();
            // 选择第一个未被禁用的子页面
            if (IsPageSwitched)
                return;
            if (!(bool)ModBase.Setup.Get("UiHiddenOtherHelp"))
            {
                this.ItemHelp.SetChecked(true, false, false);
            }
            else if (!(bool)ModBase.Setup.Get("UiHiddenOtherAbout"))
            {
                this.ItemAbout.SetChecked(true, false, false);
            }
            else
            {
                this.ItemTest.SetChecked(true, false, false);
            }
        }
        private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
        {
            IsPageSwitched = false;
        }

        #region 页面切换

        /// <summary>
    /// 当前页面的编号。从 0 开始计算。
    /// </summary>
        public FormMain.PageSubType PageID;
        public PageOtherLeft()
        {
            this.InitializeComponent();
            // 选择第一个未被禁用的子页面
            if (!(bool)ModBase.Setup.Get("UiHiddenOtherHelp"))
            {
                PageID = FormMain.PageSubType.OtherHelp;
            }
            else if (!(bool)ModBase.Setup.Get("UiHiddenOtherAbout"))
            {
                PageID = FormMain.PageSubType.OtherAbout;
            }
            else
            {
                PageID = FormMain.PageSubType.OtherTest;
            }

            this.Loaded += PageOtherLeft_Loaded;
            this.Unloaded += PageOtherLeft_Unloaded;
        }

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

        public object PageGet(FormMain.PageSubType ID = (PageSubType)(-1))
        {
            if ((int)ID == -1)
                ID = PageID;
            switch (ID)
            {
                case FormMain.PageSubType.OtherHelp:
                    {
                        if (ModMain.FrmOtherHelp is null)
                            ModMain.FrmOtherHelp = new PageOtherHelp();
                        return ModMain.FrmOtherHelp;
                    }
                case FormMain.PageSubType.OtherAbout:
                    {
                        if (ModMain.FrmOtherAbout is null)
                            ModMain.FrmOtherAbout = new PageOtherAbout();
                        return ModMain.FrmOtherAbout;
                    }
                case FormMain.PageSubType.OtherTest:
                    {
                        if (ModMain.FrmOtherTest is null)
                            ModMain.FrmOtherTest = new PageOtherTest();
                        return ModMain.FrmOtherTest;
                    }
                case FormMain.PageSubType.OtherFeedback:
                    {
                        if (ModMain.FrmOtherFeedback is null)
                            ModMain.FrmOtherFeedback = new PageOtherFeedback();
                        return ModMain.FrmOtherFeedback;
                    }
                case FormMain.PageSubType.OtherVote:
                    {
                        if (ModMain.FrmOtherVote is null)
                            ModMain.FrmOtherVote = new PageOtherVote();
                        return ModMain.FrmOtherVote;
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

        // 强制刷新
        public void Refresh(object sender, EventArgs e) // 由边栏按钮匿名调用
        {
            switch (ModBase.Val(((dynamic)sender).Tag))
            {
                case (double)FormMain.PageSubType.OtherHelp:
                    {
                        RefreshHelp();
                        this.ItemHelp.Checked = true;
                        break;
                    }
                case (double)FormMain.PageSubType.OtherFeedback:
                    {
                        if (ModMain.FrmOtherFeedback is not null)
                        {
                            ModMain.FrmOtherFeedback.Loader.Start(IsForceRestart: true);
                        }
                        this.ItemFeedback.Checked = true;
                        break;
                    }
                case (double)FormMain.PageSubType.OtherVote:
                    {
                        if (ModMain.FrmOtherVote is not null)
                        {
                            ModMain.FrmOtherVote.Loader.Start(IsForceRestart: true);
                        }
                        this.ItemVote.Checked = true;
                        break;
                    }
            }
            ModMain.Hint("正在刷新……", Log: false);
        }
        public static void RefreshHelp()
        {
            ModMain.FrmOtherHelp.PageLoaderRestart();
            ModMain.FrmOtherHelp.SearchBox.Text = "";
        }

        // 打开网页
        public static void TryFeedback() // Handles ItemFeedback.Click
        {
            ModBase.RunInNewThread(() =>
                {
                    if (!ModBase.CanFeedback(true))
                        return;
                    switch (ModMain.MyMsgBox("在提交新反馈前，建议先搜索反馈列表，以避免重复提交。" + Constants.vbCrLf + "如果无法打开该网页，请尝试使用加速器或 VPN。", "反馈", "提交新反馈", "查看反馈列表", "取消"))
                    {
                        case 1:
                            {
                                ModBase.Feedback(true, false);
                                break;
                            }
                        case 2:
                            {
                                ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
                                break;
                            }
                    }
                });

        }
        public static void TryVote() // Handles ItemVote.Click
        {
            if (ModMain.MyMsgBox("是否要打开新功能投票网页？" + Constants.vbCrLf + "如果无法打开该网页，请尝试使用加速器或 VPN。", "新功能投票", "打开", "取消") == 2)
                return;
            ModBase.OpenWebsite("https://github.com/Hex-Dragon/PCL2/discussions/categories/%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8?discussions_q=category%3A%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8+sort%3Adate_created");
        }

    }
}