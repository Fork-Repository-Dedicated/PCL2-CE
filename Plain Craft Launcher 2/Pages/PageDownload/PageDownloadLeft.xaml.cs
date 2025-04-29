using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using static PCL.FormMain;

namespace PCL
{
    public partial class PageDownloadLeft : IRefreshable
    {

        #region 页面切换

        /// <summary>
    /// 当前页面的编号。
    /// </summary>
        public FormMain.PageSubType PageID = FormMain.PageSubType.DownloadInstall;

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
                case FormMain.PageSubType.DownloadInstall:
                    {
                        if (ModMain.FrmDownloadInstall is null)
                            ModMain.FrmDownloadInstall = new PageDownloadInstall();
                        return ModMain.FrmDownloadInstall;
                    }
                case FormMain.PageSubType.DownloadClient:
                    {
                        if (ModMain.FrmDownloadClient is null)
                            ModMain.FrmDownloadClient = new PageDownloadClient();
                        return ModMain.FrmDownloadClient;
                    }
                case FormMain.PageSubType.DownloadOptiFine:
                    {
                        if (ModMain.FrmDownloadOptiFine is null)
                            ModMain.FrmDownloadOptiFine = new PageDownloadOptiFine();
                        return ModMain.FrmDownloadOptiFine;
                    }
                case FormMain.PageSubType.DownloadForge:
                    {
                        if (ModMain.FrmDownloadForge is null)
                            ModMain.FrmDownloadForge = new PageDownloadForge();
                        return ModMain.FrmDownloadForge;
                    }
                case FormMain.PageSubType.DownloadNeoForge:
                    {
                        if (ModMain.FrmDownloadNeoForge is null)
                            ModMain.FrmDownloadNeoForge = new PageDownloadNeoForge();
                        return ModMain.FrmDownloadNeoForge;
                    }
                case FormMain.PageSubType.DownloadCleanroom:
                    {
                        if (ModMain.FrmDownloadCleanroom is null)
                            ModMain.FrmDownloadCleanroom = new PageDownloadCleanroom();
                        return ModMain.FrmDownloadCleanroom;
                    }
                case FormMain.PageSubType.DownloadLiteLoader:
                    {
                        if (ModMain.FrmDownloadLiteLoader is null)
                            ModMain.FrmDownloadLiteLoader = new PageDownloadLiteLoader();
                        return ModMain.FrmDownloadLiteLoader;
                    }
                case FormMain.PageSubType.DownloadFabric:
                    {
                        if (ModMain.FrmDownloadFabric is null)
                            ModMain.FrmDownloadFabric = new PageDownloadFabric();
                        return ModMain.FrmDownloadFabric;
                    }
                case FormMain.PageSubType.DownloadQuilt:
                    {
                        if (ModMain.FrmDownloadQuilt is null)
                            ModMain.FrmDownloadQuilt = new PageDownloadQuilt();
                        return ModMain.FrmDownloadQuilt;
                    }
                case FormMain.PageSubType.DownloadMod:
                    {
                        if (ModMain.FrmDownloadMod is null)
                            ModMain.FrmDownloadMod = new PageDownloadMod();
                        return ModMain.FrmDownloadMod;
                    }
                case FormMain.PageSubType.DownloadPack:
                    {
                        if (ModMain.FrmDownloadPack is null)
                            ModMain.FrmDownloadPack = new PageDownloadPack();
                        return ModMain.FrmDownloadPack;
                    }
                case FormMain.PageSubType.DownloadResourcePack:
                    {
                        if (ModMain.FrmDownloadResourcePack is null)
                            ModMain.FrmDownloadResourcePack = new PageDownloadResourcePack();
                        return ModMain.FrmDownloadResourcePack;
                    }
                case FormMain.PageSubType.DownloadShader:
                    {
                        if (ModMain.FrmDownloadShader is null)
                            ModMain.FrmDownloadShader = new PageDownloadShader();
                        return ModMain.FrmDownloadShader;
                    }
                case FormMain.PageSubType.DownloadCompFavorites:
                    {
                        if (ModMain.FrmDownloadCompFavorites is null)
                            ModMain.FrmDownloadCompFavorites = new PageDownloadCompFavorites();
                        return ModMain.FrmDownloadCompFavorites;
                    }

                default:
                    {
                        throw new Exception("未知的下载子页面种类：" + ((int)ID).ToString());
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

        // 强制刷新
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
                case FormMain.PageSubType.DownloadInstall:
                    {
                        ModDownload.DlClientListLoader.Start(IsForceRestart: true);
                        ModDownload.DlOptiFineListLoader.Start(IsForceRestart: true);
                        ModDownload.DlForgeListLoader.Start(IsForceRestart: true);
                        ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: true);
                        ModDownload.DlCleanroomListLoader.Start(IsForceRestart: true);
                        ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: true);
                        ModDownload.DlFabricListLoader.Start(IsForceRestart: true);
                        ModDownload.DlFabricApiLoader.Start(IsForceRestart: true);
                        ModDownload.DlQuiltListLoader.Start(IsForceRestart: true);
                        ModDownload.DlQSLLoader.Start(IsForceRestart: true);
                        ModDownload.DlOptiFabricLoader.Start(IsForceRestart: true);
                        this.ItemInstall.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadMod:
                    {
                        PageDownloadMod.Storage = new ModComp.CompProjectStorage();
                        PageDownloadMod.Page = 0;
                        ModComp.CompProjectCache.Clear();
                        ModComp.CompFilesCache.Clear();
                        if (ModMain.FrmDownloadMod is not null)
                            ModMain.FrmDownloadMod.PageLoaderRestart();
                        this.ItemMod.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadPack:
                    {
                        PageDownloadPack.Storage = new ModComp.CompProjectStorage();
                        PageDownloadPack.Page = 0;
                        ModComp.CompProjectCache.Clear();
                        ModComp.CompFilesCache.Clear();
                        if (ModMain.FrmDownloadPack is not null)
                            ModMain.FrmDownloadPack.PageLoaderRestart();
                        this.ItemPack.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadResourcePack:
                    {
                        PageDownloadResourcePack.Storage = new ModComp.CompProjectStorage();
                        PageDownloadResourcePack.Page = 0;
                        ModComp.CompProjectCache.Clear();
                        if (ModMain.FrmDownloadResourcePack is not null)
                            ModMain.FrmDownloadResourcePack.PageLoaderRestart();
                        this.ItemResourcePack.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadShader:
                    {
                        PageDownloadShader.Storage = new ModComp.CompProjectStorage();
                        PageDownloadShader.Page = 0;
                        ModComp.CompProjectCache.Clear();
                        if (ModMain.FrmDownloadShader is not null)
                            ModMain.FrmDownloadShader.PageLoaderRestart();
                        this.ItemShader.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadClient:
                    {
                        ModDownload.DlClientListLoader.Start(IsForceRestart: true);
                        this.ItemClient.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadOptiFine:
                    {
                        ModDownload.DlOptiFineListLoader.Start(IsForceRestart: true);
                        this.ItemOptiFine.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadForge:
                    {
                        ModDownload.DlForgeListLoader.Start(IsForceRestart: true);
                        this.ItemForge.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadNeoForge:
                    {
                        ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: true);
                        this.ItemNeoForge.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadCleanroom:
                    {
                        ModDownload.DlCleanroomListLoader.Start(IsForceRestart: true);
                        this.ItemCleanroom.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadLiteLoader:
                    {
                        ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: true);
                        this.ItemLiteLoader.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadFabric:
                    {
                        ModDownload.DlFabricListLoader.Start(IsForceRestart: true);
                        this.ItemFabric.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadQuilt:
                    {
                        ModDownload.DlQuiltListLoader.Start(IsForceRestart: true);
                        this.ItemQuilt.Checked = true;
                        break;
                    }
                case FormMain.PageSubType.DownloadCompFavorites:
                    {
                        if (ModMain.FrmDownloadCompFavorites is not null)
                            ModMain.FrmDownloadCompFavorites.PageLoaderRestart();
                        this.ItemFavorites.Checked = true;
                        break;
                    }
            }
            ModMain.Hint("正在刷新……", Log: false);
        }

        // 点击返回
        private void ItemInstall_Click(object sender, MouseButtonEventArgs e)
        {
            if (!this.ItemInstall.Checked)
                return;
            ModMain.FrmDownloadInstall.ExitSelectPage();
        }

        // 展开手动安装
        private void ItemHand_Click(object sender, ModBase.RouteEventArgs e)
        {
            if (this.ItemHand.Checked == false)
                return;
            e.Handled = true;
            ModAnimation.AniControlEnabled += 1;
            if (!(bool)ModBase.Setup.Get("HintHandInstall"))
            {
                ModBase.Setup.Set("HintHandInstall", true);
                if (ModMain.MyMsgBox("手动安装包功能提供了 OptiFine、Forge 等组件的 .jar 安装文件下载，但无法自动安装。" + Constants.vbCrLf + "在自动安装页面先选择 MC 版本，然后就可以选择 OptiFine、Forge 等组件，让 PCL 自动进行安装了。", "自动安装提示", "返回自动安装", "继续下载手动安装包") == 1)
                {
                    ModMain.FrmMain.PageChange(new FormMain.PageStackData() { Page = FormMain.PageType.Download }, FormMain.PageSubType.DownloadInstall);
                    ModAnimation.AniControlEnabled -= 1;
                    return;
                }
            }
            this.ItemHand.Visibility = Visibility.Collapsed;
            this.LabGame.Visibility = Visibility.Collapsed;
            this.LabHand.Visibility = Visibility.Visible;
            this.ItemClient.Visibility = Visibility.Visible;
            this.ItemOptiFine.Visibility = Visibility.Visible;
            this.ItemFabric.Visibility = Visibility.Visible;
            this.ItemQuilt.Visibility = Visibility.Visible;
            this.ItemForge.Visibility = Visibility.Visible;
            this.ItemNeoForge.Visibility = Visibility.Visible;
            this.ItemCleanroom.Visibility = Visibility.Visible;
            this.ItemLiteLoader.Visibility = Visibility.Visible;
            ModBase.RunInThread(() =>
        {
            Thread.Sleep(20);
            ModBase.RunInUiWait(() => this.ItemClient.SetChecked(true, true, true));
            ModAnimation.AniControlEnabled -= 1;
        });
        }
        // 折叠手动安装
        private void LabHand_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ModAnimation.AniControlEnabled += 1;
            this.ItemHand.Visibility = Visibility.Visible;
            this.LabGame.Visibility = Visibility.Visible;
            this.LabHand.Visibility = Visibility.Collapsed;
            this.ItemClient.Visibility = Visibility.Collapsed;
            this.ItemOptiFine.Visibility = Visibility.Collapsed;
            this.ItemNeoForge.Visibility = Visibility.Collapsed;
            this.ItemCleanroom.Visibility = Visibility.Collapsed;
            this.ItemFabric.Visibility = Visibility.Collapsed;
            this.ItemQuilt.Visibility = Visibility.Collapsed;
            this.ItemForge.Visibility = Visibility.Collapsed;
            this.ItemLiteLoader.Visibility = Visibility.Collapsed;
            ModBase.RunInThread(() =>
        {
            Thread.Sleep(20);
            ModBase.RunInUiWait(() => this.ItemInstall.SetChecked(true, true, true));
            ModAnimation.AniControlEnabled -= 1;
        });
        }

    }
}