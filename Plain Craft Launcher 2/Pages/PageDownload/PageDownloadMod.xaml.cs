using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadMod
    {

        public const int PageSize = 40;
        /// <summary>
    /// 在切换到该页面时自动设置的目标版本。
    /// </summary>
        public static ModMinecraft.McVersion TargetVersion = null;

        // 加载器信息
        public static ModLoader.LoaderTask<ModComp.CompProjectRequest, int> Loader = new ModLoader.LoaderTask<ModComp.CompProjectRequest, int>("CompProject Mod", ModComp.CompProjectsGet, LoaderInput) { ReloadTimeout = 60 * 1000 };
        public static ModComp.CompProjectStorage Storage = new ModComp.CompProjectStorage();
        public static int Page = 0;
        private bool IsLoaderInited = false;

        public PageDownloadMod()
        {
            this.Loaded += PageDownloadMod_Inited;
        }
        private void PageDownloadMod_Inited(object sender, EventArgs e)
        {
            // 不知道从 Initialized 改成 Loaded 会不会有问题，但用 Initialized 会导致初始的筛选器修改被覆盖回默认值
            if (TargetVersion is not null)
            {
                // 设置目标
                ResetFilter(); // 重置筛选器
                this.TextSearchVersion.Text = TargetVersion.Version.McName;
                MyComboBoxItem GetTargetItemByName(string Name)
                {
                    foreach (MyComboBoxItem Item in this.ComboSearchLoader.Items)
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(Item.Content, Name, false)))
                            return Item;
                    }
                    return (MyComboBoxItem)this.ComboSearchLoader.Items[0];
                };
                if (TargetVersion.Version.HasForge)
                {
                    this.ComboSearchLoader.SelectedItem = GetTargetItemByName("Forge");
                }
                else if (TargetVersion.Version.HasFabric)
                {
                    this.ComboSearchLoader.SelectedItem = GetTargetItemByName("Fabric");
                }
                else if (TargetVersion.Version.HasNeoForge)
                {
                    this.ComboSearchLoader.SelectedItem = GetTargetItemByName("NeoForge");
                }
                TargetVersion = null;
                // 如果已经完成请求，则重新开始
                if (IsLoaderInited)
                    StartNewSearch();
                this.PanScroll.ScrollToHome();
            }
            // 加载器初始化
            if (IsLoaderInited)
                return;
            IsLoaderInited = true;
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanContent, this.PanAlways, Loader, (_) => Load_OnFinish(), PageDownloadMod.LoaderInput);
            if (ModDownloadLib.McVersionHighest == -1)
                ModDownloadLib.McVersionHighest = Math.Max(ModDownloadLib.McVersionHighest, int.Parse(((MyComboBoxItem)this.TextSearchVersion.Items[1]).Content.ToString().Split(".")[1]));
        }
        private static ModComp.CompProjectRequest LoaderInput()
        {
            var Request = new ModComp.CompProjectRequest(ModComp.CompType.Mod, Storage, (Page + 1) * PageSize);
            if (ModMain.FrmDownloadMod is not null)
            {
                ModComp.CompLoaderType ModLoader = (ModComp.CompLoaderType)Math.Round(ModBase.Val(((dynamic)ModMain.FrmDownloadMod.ComboSearchLoader.SelectedItem).Tag));
                string GameVersion = ModMain.FrmDownloadMod.TextSearchVersion.Text == "全部 (也可自行输入)" ? (string)null : ModMain.FrmDownloadMod.TextSearchVersion.Text.Contains(".") || ModMain.FrmDownloadMod.TextSearchVersion.Text.Contains("w") ? ModMain.FrmDownloadMod.TextSearchVersion.Text : (string)null;
                if (GameVersion is not null && GameVersion.Contains(".") && ModBase.Val(GameVersion.Split(".")[1]) < 14d && ModLoader == ModComp.CompLoaderType.Forge) // 1.14-
                                                                                                                                                                       // 选择了 Forge
                {
                    ModLoader = ModComp.CompLoaderType.Any; // 此时，视作没有筛选 Mod Loader（因为部分老 Mod 没有设置自己支持的加载器）
                }
                Request.SearchText = ModMain.FrmDownloadMod.TextSearchName.Text;
                Request.GameVersion = GameVersion;
                Request.Tag = Conversions.ToString(((dynamic)ModMain.FrmDownloadMod.ComboSearchTag.SelectedItem).Tag);
                Request.ModLoader = ModLoader;
                Request.Source = (ModComp.CompSourceType)Math.Round(ModBase.Val(((dynamic)ModMain.FrmDownloadMod.ComboSearchSource.SelectedItem).Tag));
            }
            return Request;
        }

        // 结果 UI 化
        private void Load_OnFinish()
        {
            try
            {
                ModBase.Log($"[Comp] 开始可视化 Mod 列表，已储藏 {Storage.Results.Count} 个结果，当前在第 {Page + 1} 页");
                // 列表项
                this.PanProjects.Children.Clear();
                for (int i = Math.Min(Page * PageSize, Storage.Results.Count - 1), loopTo = Math.Min((Page + 1) * PageSize - 1, Storage.Results.Count - 1); i <= loopTo; i++)
                    this.PanProjects.Children.Add(Storage.Results[i].ToCompItem(Loader.Input.GameVersion is null, Loader.Input.ModLoader == ModComp.CompLoaderType.Any));
                // 页码
                this.CardPages.Visibility = Storage.Results.Count > 40 || Storage.CurseForgeOffset < Storage.CurseForgeTotal || Storage.ModrinthOffset < Storage.ModrinthTotal ? Visibility.Visible : Visibility.Collapsed;
                this.LabPage.Text = (Page + 1).ToString();
                this.BtnPageFirst.IsEnabled = Page > 1;
                this.BtnPageFirst.Opacity = Page > 1 ? 1d : 0.2d;
                this.BtnPageLeft.IsEnabled = Page > 0;
                this.BtnPageLeft.Opacity = Page > 0 ? 1d : 0.2d;
                bool IsRightEnabled = Storage.Results.Count > PageSize * (Page + 1) || Storage.CurseForgeOffset < Storage.CurseForgeTotal || Storage.ModrinthOffset < Storage.ModrinthTotal; // 由于 WPF 的未知 bug，读取到的 IsEnabled 可能是错误的值（#3319）
                this.BtnPageRight.IsEnabled = IsRightEnabled;
                this.BtnPageRight.Opacity = IsRightEnabled ? 1d : 0.2d;
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
                ModBase.Log(ex, "可视化 Mod 列表出错", ModBase.LogLevel.Feedback);
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
                        if (ErrorMessage.Contains("不是有效的 Json 文件"))
                        {
                            ModBase.Log("[Download] 下载的 Mod 列表 Json 文件损坏，已自动重试", ModBase.LogLevel.Debug);
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
            ModBase.Log($"[Download] Mod 切换到第 {Page + 1} 页");
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
        private void ResetFilter()
        {
            this.TextSearchName.Text = "";
            this.TextSearchVersion.Text = "全部 (也可自行输入)";
            this.TextSearchVersion.SelectedIndex = 0;
            this.ComboSearchSource.SelectedIndex = 0;
            this.ComboSearchTag.SelectedIndex = 0;
            this.ComboSearchLoader.SelectedIndex = 0;
            Loader.LastFinishedTime = 0L; // 要求强制重新开始
        }

        // 版本选择
        // #3067：当下拉菜单展开时，程序会被 WPF 挂起，因而无法更新 Grid 布局，所以必须延迟到下拉菜单收起后才能更新
        private void TextSearchVersion_TextChanged()
        {
            if (!this.TextSearchVersion.IsDropDownOpen)
                UpdateSearchLoaderVisibility();
        }
        private void UpdateSearchLoaderVisibility()
        {
            if (this.TextSearchVersion.Text.Contains(".") || this.TextSearchVersion.Text.Contains("w"))
            {
                this.ComboSearchLoader.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(this.TextSearchVersion, 1);
            }
            else
            {
                this.ComboSearchLoader.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(this.TextSearchVersion, 2);
                this.ComboSearchLoader.SelectedIndex = 0;
            }
        }

        #endregion

    }
}