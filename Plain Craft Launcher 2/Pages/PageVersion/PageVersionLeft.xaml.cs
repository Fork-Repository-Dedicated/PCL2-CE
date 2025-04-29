using System;
using System.Windows;
using System.Windows.Controls;
using static PCL.FormMain;

namespace PCL
{
    public partial class PageVersionLeft : IRefreshable
    {

        /// <summary>
    /// 当前显示设置的 MC 版本。
    /// </summary>
        public static ModMinecraft.McVersion Version = null;

        public PageVersionLeft()
        {
            this.Loaded += (_, __) => RefreshModDisabled();
        }

        public void RefreshModDisabled()
        {
            if (Version is not null && Version.Modable)
            {
                this.ItemMod.Visibility = Visibility.Visible;
                this.ItemModDisabled.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.ItemMod.Visibility = Visibility.Collapsed;
                this.ItemModDisabled.Visibility = Visibility.Visible;
            }
        }

        #region 页面切换

        /// <summary>
    /// 当前页面的编号。从 0 开始计算。
    /// </summary>
        public FormMain.PageSubType PageID = FormMain.PageSubType.Default;

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
                case FormMain.PageSubType.VersionOverall:
                    {
                        if (ModMain.FrmVersionOverall is null)
                            ModMain.FrmVersionOverall = new PageVersionOverall();
                        return ModMain.FrmVersionOverall;
                    }
                case FormMain.PageSubType.VersionMod:
                    {
                        if (ModMain.FrmVersionMod is null)
                            ModMain.FrmVersionMod = new PageVersionCompResource(ModComp.CompType.Mod);
                        return ModMain.FrmVersionMod;
                    }
                case FormMain.PageSubType.VersionModDisabled:
                    {
                        if (ModMain.FrmVersionModDisabled is null)
                            ModMain.FrmVersionModDisabled = new PageVersionModDisabled();
                        return ModMain.FrmVersionModDisabled;
                    }
                case FormMain.PageSubType.VersionSetup:
                    {
                        if (ModMain.FrmVersionSetup == null)
                            ModMain.FrmVersionSetup = new PageVersionSetup();
                        return ModMain.FrmVersionSetup;
                    }
                case FormMain.PageSubType.VersionWorld:
                    {
                        if (ModMain.FrmVersionWorld is null)
                            ModMain.FrmVersionWorld = new PageVersionWorld();
                        return ModMain.FrmVersionWorld;
                    }
                case FormMain.PageSubType.VersionScreenshot:
                    {
                        if (ModMain.FrmVersionScreenshot is null)
                            ModMain.FrmVersionScreenshot = new PageVersionScreenshot();
                        return ModMain.FrmVersionScreenshot;
                    }
                case FormMain.PageSubType.VersionResourcePack:
                    {
                        if (ModMain.FrmVersionResourcePack is null)
                            ModMain.FrmVersionResourcePack = new PageVersionCompResource(ModComp.CompType.ResourcePack);
                        return ModMain.FrmVersionResourcePack;
                    }
                case FormMain.PageSubType.VersionShader:
                    {
                        if (ModMain.FrmVersionShader is null)
                            ModMain.FrmVersionShader = new PageVersionCompResource(ModComp.CompType.Shader);
                        return ModMain.FrmVersionShader;
                    }
                case FormMain.PageSubType.VersionInstall:
                    {
                        if (ModMain.FrmVersionInstall is null)
                            ModMain.FrmVersionInstall = new PageVersionInstall();
                        return ModMain.FrmVersionInstall;
                    }
                case FormMain.PageSubType.VersionExport:
                    {
                        if (ModMain.FrmVersionExport is null)
                            ModMain.FrmVersionExport = new PageVersionExport();
                        return ModMain.FrmVersionExport;
                    }

                default:
                    {
                        throw new Exception("未知的版本设置子页面种类：" + ((int)ID).ToString());
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

        public void Refresh(object sender, EventArgs e) // 由边栏按钮匿名调用
        {
            Refresh((FormMain.PageSubType)Math.Round(ModBase.Val(((dynamic)sender).Tag)));
        }
        public void Refresh()
        {
            Refresh(ModMain.FrmMain.PageCurrentSub);
        }
        public void Refresh(FormMain.PageSubType SubType)
        {
            switch (SubType)
            {
                case FormMain.PageSubType.VersionMod:
                    {
                        PageVersionCompResource.Refresh(ModComp.CompType.Mod);
                        break;
                    }
                case FormMain.PageSubType.VersionScreenshot:
                    {
                        PageVersionScreenshot.Refresh();
                        break;
                    }
                case FormMain.PageSubType.VersionWorld:
                    {
                        PageVersionWorld.Refresh();
                        break;
                    }
                case FormMain.PageSubType.VersionResourcePack:
                    {
                        PageVersionCompResource.Refresh(ModComp.CompType.ResourcePack);
                        break;
                    }
                case FormMain.PageSubType.VersionShader:
                    {
                        PageVersionCompResource.Refresh(ModComp.CompType.Shader);
                        break;
                    }
                case FormMain.PageSubType.VersionInstall:
                    {
                        ModDownload.DlClientListLoader.Start(IsForceRestart: true);
                        ModDownload.DlOptiFineListLoader.Start(IsForceRestart: true);
                        ModDownload.DlForgeListLoader.Start(IsForceRestart: true);
                        ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: true);
                        ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: true);
                        ModDownload.DlFabricListLoader.Start(IsForceRestart: true);
                        ModDownload.DlFabricApiLoader.Start(IsForceRestart: true);
                        ModDownload.DlQuiltListLoader.Start(IsForceRestart: true);
                        ModDownload.DlQSLLoader.Start(IsForceRestart: true);
                        ModDownload.DlOptiFabricLoader.Start(IsForceRestart: true);
                        this.ItemInstall.Checked = true;
                        ModMain.FrmVersionInstall.GetCurrentInfo();
                        break;
                    }
                case FormMain.PageSubType.VersionExport:
                    {
                        if (ModMain.FrmVersionExport is not null)
                            ModMain.FrmVersionExport.RefreshAll();
                        this.ItemExport.Checked = true;
                        break;
                    }
            }
        }

        public void Reset(object sender, EventArgs e)
        {
            if (ModMain.MyMsgBox("是否要初始化该版本的版本独立设置？该操作不可撤销。", "初始化确认", Button2: "取消", IsWarn: true) == 1)
            {
                if (ModMain.FrmVersionSetup == null)
                    ModMain.FrmVersionSetup = new PageVersionSetup();
                ModMain.FrmVersionSetup.Reset();
                this.ItemSetup.Checked = true;
            }
        }

    }
}