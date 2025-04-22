using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageLaunchRight : IRefreshable
    {
        public PageLaunchRight()
        {
            OnlineLoader = new ModLoader.LoaderTask<string, int>("自定义主页获取", OnlineLoaderSub) { ReloadTimeout = 10 * 60 * 1000 };
            this.Loaded += (_, __) => Init();
            this.Loaded += (_, __) => Refresh();
        }

        private void Init()
        {
            this.PanBack.ScrollToHome();
            this.PanScroll = this.PanBack; // 不知道为啥不能在 XAML 设置
            this.PanLog.Visibility = ModBase.ModeDebug ? Visibility.Visible : Visibility.Collapsed;
            // 社区版提示
            this.PanHint.Visibility = Conversions.ToBoolean(ModBase.Setup.Get("UiLauncherCEHint")) ? Visibility.Visible : Visibility.Collapsed;
            this.LabHint1.Text = "社区版包含未在官方主线版本发布的功能，仅用于尝鲜。请不要向官方仓库反馈社区版的问题哦！";
            this.LabHint2.Text = $"若要永久隐藏此提示，请参阅 README。";
        }

        // 暂时关闭快照版提示
        private void BtnHintClose_Click(object sender, EventArgs e)
        {
            ModAnimation.AniDispose(this.PanHint, true);
        }

        #region 自定义主页

        /// <summary>
    /// 刷新自定义主页。
    /// </summary>
        private void Refresh()
        {
            ModBase.RunInNewThread(() => { try { lock (RefreshLock) RefreshReal(); } catch (Exception ex) { ModBase.Log(ex, "加载 PCL 主页自定义信息失败", ModBase.LogLevel.Msgbox); } }, $"刷新自定义主页 #{ModBase.GetUuid()}");
        }
        private void RefreshReal()
        {
            string Content = "";
            string Url;
            switch (ModBase.Setup.Get("UiCustomType"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 1, false):
                    {
                        // 加载本地文件
                        ModBase.Log("[Page] 主页自定义数据来源：本地文件");
                        Content = ModBase.ReadFile(ModBase.Path + @"PCL\Custom.xaml"); // ReadFile 会进行存在检测
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false):
                    {
                        Url = Conversions.ToString(ModBase.Setup.Get("UiCustomNet"));
                    Download:
                        ;

                        // 加载联网文件
                        if (string.IsNullOrWhiteSpace(Url))
                            break;
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(Url, ModBase.Setup.Get("CacheSavedPageUrl"), false)) && File.Exists(ModBase.PathTemp + @"Cache\Custom.xaml"))
                        {
                            // 缓存可用
                            ModBase.Log("[Page] 主页自定义数据来源：联网缓存文件");
                            Content = ModBase.ReadFile(ModBase.PathTemp + @"Cache\Custom.xaml");
                            // 后台更新缓存
                            OnlineLoader.Start(Url);
                        }
                        else
                        {
                            // 缓存不可用
                            ModBase.Log("[Page] 主页自定义数据来源：联网全新下载");
                            ModMain.Hint("正在加载主页……");
                            ModBase.RunInUiWait(() => LoadContent("")); // 在加载结束前清空页面
                            ModBase.Setup.Set("CacheSavedPageVersion", "");
                            OnlineLoader.Start(Url); // 下载完成后将会再次触发更新
                            return;
                        }

                        break;
                    }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, 3, false):
                    {
                        switch (ModBase.Setup.Get("UiCustomPreset"))
                        {
                            case var case3 when Operators.ConditionalCompareObjectEqual(case3, 0, false):
                                {
                                    ModBase.Log("[Page] 主页预设：你知道吗");
                                    Content = @"
                            <local:MyCard Title=""你知道吗？"" Margin=""0,0,0,15"">
                                <TextBlock Margin=""25,38,23,15"" FontSize=""13.5"" IsHitTestVisible=""False"" Text=""{hint}"" TextWrapping=""Wrap"" Foreground=""{DynamicResource ColorBrush1}"" />
                                <local:MyIconButton Height=""22"" Width=""22"" Margin=""9"" VerticalAlignment=""Top"" HorizontalAlignment=""Right"" 
                                    EventType=""刷新主页"" EventData=""/""
                                    Logo=""M875.52 148.48C783.36 56.32 655.36 0 512 0 291.84 0 107.52 138.24 30.72 332.8l122.88 46.08C204.8 230.4 348.16 128 512 128c107.52 0 199.68 40.96 271.36 112.64L640 384h384V0L875.52 148.48zM512 896c-107.52 0-199.68-40.96-271.36-112.64L384 640H0v384l148.48-148.48C240.64 967.68 368.64 1024 512 1024c220.16 0 404.48-138.24 481.28-332.8L870.4 645.12C819.2 793.6 675.84 896 512 896z"" />
                            </local:MyCard>";
                                    break;
                                }
                            case var case4 when Operators.ConditionalCompareObjectEqual(case4, 1, false):
                                {
                                    ModBase.Log("[Page] 主页预设：回声洞");
                                    Content = @"
                            <local:MyCard Title=""回声洞"" Margin=""0,0,0,15"">
                                <TextBlock Margin=""25,38,23,15"" FontSize=""13.5"" IsHitTestVisible=""False"" Text=""{cave}"" TextWrapping=""Wrap"" Foreground=""{DynamicResource ColorBrush1}"" />
                                <local:MyIconButton Height=""22"" Width=""22"" Margin=""9"" VerticalAlignment=""Top"" HorizontalAlignment=""Right"" 
                                    EventType=""刷新主页"" EventData=""/""
                                    Logo=""M875.52 148.48C783.36 56.32 655.36 0 512 0 291.84 0 107.52 138.24 30.72 332.8l122.88 46.08C204.8 230.4 348.16 128 512 128c107.52 0 199.68 40.96 271.36 112.64L640 384h384V0L875.52 148.48zM512 896c-107.52 0-199.68-40.96-271.36-112.64L384 640H0v384l148.48-148.48C240.64 967.68 368.64 1024 512 1024c220.16 0 404.48-138.24 481.28-332.8L870.4 645.12C819.2 793.6 675.84 896 512 896z"" />
                            </local:MyCard>";
                                    break;
                                }
                            case var case5 when Operators.ConditionalCompareObjectEqual(case5, 2, false):
                                {
                                    ModBase.Log("[Page] 主页预设：Minecraft 新闻");
                                    Url = "http://pcl.mcnews.thestack.top";
                                    goto Download;
                                    break;
                                }
                            case var case6 when Operators.ConditionalCompareObjectEqual(case6, 3, false):
                                {
                                    ModBase.Log("[Page] 主页预设：简单主页");
                                    Url = "https://raw.gitcode.com/mfn233/PCL-Mainpage/raw/main/Custom.xaml";
                                    goto Download;
                                    break;
                                }
                            case var case7 when Operators.ConditionalCompareObjectEqual(case7, 4, false):
                                {
                                    ModBase.Log("[Page] 主页预设：每日整合包推荐");
                                    Url = "https://pclsub.sodamc.com/";
                                    goto Download;
                                    break;
                                }
                            case var case8 when Operators.ConditionalCompareObjectEqual(case8, 5, false):
                                {
                                    ModBase.Log("[Page] 主页预设：Minecraft 皮肤推荐");
                                    Url = "https://forgepixel.com/pcl_sub_file";
                                    goto Download;
                                    break;
                                }
                            case var case9 when Operators.ConditionalCompareObjectEqual(case9, 6, false):
                                {
                                    ModBase.Log("[Page] 主页预设：OpenBMCLAPI 仪表盘 Lite");
                                    Url = "https://pcl-bmcl.milu.ink/";
                                    goto Download;
                                    break;
                                }
                            case var case10 when Operators.ConditionalCompareObjectEqual(case10, 7, false):
                                {
                                    ModBase.Log("[Page] 主页预设：主页市场");
                                    Url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml";
                                    goto Download;
                                    break;
                                }
                            case var case11 when Operators.ConditionalCompareObjectEqual(case11, 8, false):
                                {
                                    ModBase.Log("[Page] 主页预设：更新日志");
                                    Url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Joker2184/UpdateHomepage.xaml";
                                    goto Download;
                                    break;
                                }
                            case var case12 when Operators.ConditionalCompareObjectEqual(case12, 9, false):
                                {
                                    ModBase.Log("[Page] 主页预设：PCL 新功能说明书");
                                    Url = "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml";
                                    goto Download;
                                    break;
                                }
                            case var case13 when Operators.ConditionalCompareObjectEqual(case13, 10, false):
                                {
                                    ModBase.Log("[Page] 主页预设：OpenMCIM Dashboard");
                                    Url = "https://files.mcimirror.top/PCL";
                                    goto Download;
                                    break;
                                }
                            case var case14 when Operators.ConditionalCompareObjectEqual(case14, 11, false):
                                {
                                    ModBase.Log("[Page] 主页预设：杂志主页");
                                    Url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml";
                                    goto Download;
                                    break;
                                }
                        }

                        break;
                    }
            }
            ModBase.RunInUi(() => LoadContent(Content));
        }
        private object RefreshLock = new object();

        // 联网获取自定义主页文件
        private ModLoader.LoaderTask<string, int> OnlineLoader;
        private void OnlineLoaderSub(ModLoader.LoaderTask<string, int> Task)
        {
            string Address = Task.Input; // #3721 中连续触发两次导致内容变化
            try
            {
                // 获取版本校验地址
                string VersionAddress;
                if (Address.Contains(".xaml"))
                {
                    VersionAddress = Address.Replace(".xaml", ".xaml.ini");
                }
                else
                {
                    VersionAddress = Address.BeforeFirst("?");
                    if (!VersionAddress.EndsWith("/"))
                        VersionAddress += "/";
                    VersionAddress += "version";
                    if (Address.Contains("?"))
                        VersionAddress += Address.AfterLast("?");
                }
                // 校验版本
                string Version = "";
                bool NeedDownload = true;
                try
                {
                    Version = Conversions.ToString(ModNet.NetGetCodeByRequestOnce(VersionAddress, Timeout: 10000));
                    if (Version.Length > 1000)
                        throw new Exception($"获取的自定义主页版本过长（{Version.Length} 字符）");
                    string CurrentVersion = Conversions.ToString(ModBase.Setup.Get("CacheSavedPageVersion"));
                    if (!string.IsNullOrEmpty(Version) && !string.IsNullOrEmpty(CurrentVersion) && (Version ?? "") == (CurrentVersion ?? ""))
                    {
                        ModBase.Log($"[Page] 当前缓存的自定义主页已为最新，当前版本：{Version}，检查源：{VersionAddress}");
                        NeedDownload = false;
                    }
                    else
                    {
                        ModBase.Log($"[Page] 需要下载联网自定义主页，当前版本：{Version}，检查源：{VersionAddress}");
                    }
                }
                catch (Exception exx)
                {
                    ModBase.Log(exx, $"联网获取自定义主页版本失败", ModBase.LogLevel.Developer);
                    ModBase.Log($"[Page] 无法检查联网自定义主页版本，将直接下载，检查源：{VersionAddress}");
                }
                // 实际下载
                if (NeedDownload)
                {
                    string FileContent = Conversions.ToString(ModNet.NetGetCodeByRequestRetry(Address));
                    ModBase.Log($"[Page] 已联网下载自定义主页，内容长度：{FileContent.Length}，来源：{Address}");
                    ModBase.Setup.Set("CacheSavedPageUrl", Address);
                    ModBase.Setup.Set("CacheSavedPageVersion", Version);
                    ModBase.WriteFile(ModBase.PathTemp + @"Cache\Custom.xaml", FileContent);
                }
                // 要求刷新
                Refresh();
            }
            catch (Exception ex)
            {
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheSavedPageVersion"), "", false)))
                {
                    ModBase.Log(ex, $"联网下载自定义主页失败（{Address}）", ModBase.LogLevel.Msgbox);
                }
                else
                {
                    ModBase.Log(ex, $"联网下载自定义主页失败（{Address}）");
                }
            }
        }

        /// <summary>
    /// 立即强制刷新自定义主页。
    /// 必须在 UI 线程调用。
    /// </summary>
        public void ForceRefresh()
        {
            ModBase.Log("[Page] 要求强制刷新自定义主页");
            ClearCache();
            // 实际的刷新
            if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.Launch)
            {
                this.PanBack.ScrollToHome();
                Refresh();
            }
            else
            {
                ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Launch);
            }
        }

        void IRefreshable.Refresh() => ForceRefresh();

        /// <summary>
    /// 清空自定义主页缓存信息。
    /// </summary>
        private void ClearCache()
        {
            LoadedContentHash = -1;
            OnlineLoader.Input = "";
            ModBase.Setup.Set("CacheSavedPageUrl", "");
            ModBase.Setup.Set("CacheSavedPageVersion", "");
            ModBase.Log("[Page] 已清空自定义主页缓存");
        }

        /// <summary>
    /// 从文本内容中加载自定义主页。
    /// 必须在 UI 线程调用。
    /// </summary>
        private void LoadContent(string Content)
        {
            lock (LoadContentLock)
            {
                // 如果加载目标内容一致则不加载
                int Hash = Content.GetHashCode();
                if (Hash == LoadedContentHash)
                    return;
                LoadedContentHash = Hash;
                // 实际加载内容
                this.PanCustom.Children.Clear();
                if (string.IsNullOrWhiteSpace(Content))
                {
                    ModBase.Log($"[Page] 实例化：清空自定义主页 UI，来源为空");
                    return;
                }
                try
                {
                    Content = ModMain.HelpArgumentReplace(Content);
                    if (Content.Contains("xmlns"))
                        Content = Content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", ""); // 禁止声明命名空间
                    Content = "<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=mscorlib\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">" + Content + "</StackPanel>";
                    ModBase.Log($"[Page] 实例化：加载自定义主页 UI 开始，最终内容长度：{Content.Count()}");
                    this.PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(Content));
                }
                catch (UnauthorizedAccessException ex)
                {
                    ModBase.Log(ex, "加载失败的自定义主页内容：" + Constants.vbCrLf + Content);
                    if (ModMain.MyMsgBox(ex.Message, "加载自定义主页失败", "重试", "取消") == 1)
                    {
                        goto Refresh; // 防止 SyncLock 死锁
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "加载失败的自定义主页内容：" + Constants.vbCrLf + Content);
                    if (ModMain.MyMsgBox($"自定义主页内容编写有误，请根据下列错误信息进行检查：{Constants.vbCrLf}{ModBase.GetExceptionSummary(ex)}", "加载自定义主页失败", "重试", "取消") == 1)
                    {
                        goto Refresh; // 防止 SyncLock 死锁
                    }
                }
                ModBase.Log($"[Page] 实例化：加载自定义主页 UI 完成");
            }
            return;
        Refresh:
            ;

            ForceRefresh();
        }
        private int LoadedContentHash = -1;
        private object LoadContentLock = new object();

        #endregion

    }
}