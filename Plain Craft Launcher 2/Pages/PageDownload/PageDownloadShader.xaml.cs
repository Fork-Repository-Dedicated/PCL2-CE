using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadShader
    {

        public const int PageSize = 40;

        // 加载器信息
        public static ModLoader.LoaderTask<ModComp.CompProjectRequest, int> Loader = new ModLoader.LoaderTask<ModComp.CompProjectRequest, int>("CompProject Shader", ModComp.CompProjectsGet, LoaderInput) { ReloadTimeout = 60 * 1000 };
        public static ModComp.CompProjectStorage Storage = new ModComp.CompProjectStorage();
        public static int Page = 0;

        public PageDownloadShader()
        {
            this.Initialized += PageDownloadShader_Inited;
        }
        private void PageDownloadShader_Inited(object sender, EventArgs e)
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanContent, this.PanAlways, Loader, (_) => Load_OnFinish(), PageDownloadShader.LoaderInput);
            if (ModDownloadLib.McVersionHighest == -1)
                ModDownloadLib.McVersionHighest = Math.Max(ModDownloadLib.McVersionHighest, int.Parse(((MyComboBoxItem)this.TextSearchVersion.Items[1]).Content.ToString().Split(".")[1]));
        }
        private static ModComp.CompProjectRequest LoaderInput()
        {
            var Request = new ModComp.CompProjectRequest(ModComp.CompType.Shader, Storage, (Page + 1) * PageSize);
            if (ModMain.FrmDownloadShader is not null)
            {
                Request.SearchText = ModMain.FrmDownloadShader.TextSearchName.Text;
                Request.GameVersion = ModMain.FrmDownloadShader.TextSearchVersion.Text == "全部 (也可自行输入)" ? (string)null : ModMain.FrmDownloadShader.TextSearchVersion.Text.Contains(".") || ModMain.FrmDownloadShader.TextSearchVersion.Text.Contains("w") ? ModMain.FrmDownloadShader.TextSearchVersion.Text : (string)null;
                Request.Tag = Conversions.ToString(((dynamic)ModMain.FrmDownloadShader.ComboSearchTag.SelectedItem).Tag);
                Request.Source = (ModComp.CompSourceType)Math.Round(ModBase.Val(((dynamic)ModMain.FrmDownloadShader.ComboSearchSource.SelectedItem).Tag));
            }
            return Request;
        }

        // 结果 UI 化
        private void Load_OnFinish()
        {
            try
            {
                ModBase.Log($"[Comp] 开始可视化光影包列表，已储藏 {Storage.Results.Count} 个结果，当前在第 {Page + 1} 页");
                // 列表项
                this.PanProjects.Children.Clear();
                for (int i = Math.Min(Page * PageSize, Storage.Results.Count - 1), loopTo = Math.Min((Page + 1) * PageSize - 1, Storage.Results.Count - 1); i <= loopTo; i++)
                    this.PanProjects.Children.Add(Storage.Results[i].ToCompItem(Loader.Input.GameVersion is null, false));
                // 页码
                this.CardPages.Visibility = Storage.Results.Count > 40 || Storage.CurseForgeOffset < Storage.CurseForgeTotal || Storage.ModrinthOffset < Storage.ModrinthTotal ? Visibility.Visible : Visibility.Collapsed;
                this.LabPage.Text = (Page + 1).ToString();
                this.BtnPageFirst.IsEnabled = Page > 1;
                this.BtnPageFirst.Opacity = this.BtnPageFirst.IsEnabled ? 1d : 0.2d;
                this.BtnPageLeft.IsEnabled = Page > 0;
                this.BtnPageLeft.Opacity = this.BtnPageLeft.IsEnabled ? 1d : 0.2d;
                this.BtnPageRight.IsEnabled = Storage.Results.Count > PageSize * (Page + 1) || Storage.CurseForgeOffset < Storage.CurseForgeTotal || Storage.ModrinthOffset < Storage.ModrinthTotal;
                this.BtnPageRight.Opacity = this.BtnPageRight.IsEnabled ? 1d : 0.2d;
                // 错误信息
                if (Storage.ErrorMessage is null)
                {
                    this.HintError.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.HintError.Visibility = Visibility.Visible;
                    this.HintError.Text = Storage.ErrorMessage;
                }
                // 强制返回顶部
                this.PanBack.ScrollToTop();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化光影包列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 自动重试
        private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
        {
            switch (Loader.State)
            {
                case ModBase.LoadState.Failed:
                    {
                        string ErrorMessage = "";
                        if (Loader.Error is not null)
                            ErrorMessage = Loader.Error.Message;
                        if (ErrorMessage.Contains("不是有效的 JSON 文件"))
                        {
                            ModBase.Log("[Download] 下载的光影包列表 JSON 文件损坏，已自动重试", ModBase.LogLevel.Debug);
                            this.PageLoaderRestart();
                        }

                        break;
                    }
            }
        }

        // 切换页码

        private void BtnPageFirst_Click(object sender, RoutedEventArgs e)
        {
            ChangePage(0);
        }
        private void BtnPageLeft_Click(object sender, RoutedEventArgs e)
        {
            ChangePage(Page - 1);
        }
        private void BtnPageRight_Click(object sender, RoutedEventArgs e)
        {
            ChangePage(Page + 1);
        }
        private void ChangePage(int NewPage)
        {
            this.CardPages.IsEnabled = false;
            Page = NewPage;
            ModMain.FrmMain.BackToTop();
            ModBase.Log($"[Download] 光影包切换到第 {Page + 1} 页");
            ModBase.RunInThread(() =>
                {
                    Thread.Sleep(100); // 等待向上滚的动画结束
                    ModBase.RunInUi(() => this.CardPages.IsEnabled = true);
                    Loader.Start();
                });
        }

        #region 搜索

        // 搜索按钮
        private void StartNewSearch()
        {
            Page = 0;
            object argInput = LoaderInput();
            if (Loader.ShouldStart(ref argInput))
                Storage = new ModComp.CompProjectStorage(); // 避免连续搜索两次使得 CompProjectStorage 引用丢失（#1311）
            Loader.Start();
        }
        private void EnterTrigger(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                StartNewSearch();
        }

        // 重置按钮
        private void BtnSearchReset_Click(object sender, EventArgs e)
        {
            this.TextSearchName.Text = "";
            this.TextSearchVersion.Text = "全部 (也可自行输入)";
            this.TextSearchVersion.SelectedIndex = 0;
            this.ComboSearchSource.SelectedIndex = 0;
            this.ComboSearchTag.SelectedIndex = 0;
            Loader.LastFinishedTime = 0L; // 要求强制重新开始
        }

        #endregion

    }
}