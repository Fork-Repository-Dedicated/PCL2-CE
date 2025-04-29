using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageLaunchLeft
    {

        // 加载当前版本
        private bool IsLoad = false;
        private bool IsLoadFinished = false;

        public PageLaunchLeft()
        {
            this.Loaded += (_, __) => PageLaunchLeft_Loaded();
        }
        public void PageLaunchLeft_Loaded()
        {
            if (IsLoad)
                RefreshPage(true, false);

            this.AprilPosTrans.X = 0d;
            this.AprilPosTrans.Y = 0d;

            if (IsLoad)
                return;
            IsLoad = true;
            ModAnimation.AniControlEnabled += 1;

            // 开始按钮
            ModMinecraft.McVersionListLoader.LoadingStateChanged += (_, __) => RefreshButtonsUI();
            ModMinecraft.McFolderListLoader.LoadingStateChanged += (_, __) => RefreshButtonsUI();
            RefreshButtonsUI();

            // 加载版本
            ModBase.RunInNewThread(() =>
        {
            // 自动整合包安装：准备
            string PackInstallPath = null;
            if (File.Exists(ModBase.Path + "modpack.zip"))
                PackInstallPath = ModBase.Path + "modpack.zip";
            if (File.Exists(ModBase.Path + "modpack.mrpack"))
                PackInstallPath = ModBase.Path + "modpack.mrpack";
            if (PackInstallPath is not null)
            {
                ModBase.Log("[Launch] 需自动安装整合包：" + PackInstallPath, ModBase.LogLevel.Debug);
                ModBase.Setup.Set("LaunchFolderSelect", @"$.minecraft\");
                if (!Directory.Exists(ModBase.Path + @".minecraft\"))
                {
                    Directory.CreateDirectory(ModBase.Path + @".minecraft\");
                    Directory.CreateDirectory(ModBase.Path + @".minecraft\versions\");
                    ModMinecraft.McFolderLauncherProfilesJsonCreate(ModBase.Path + @".minecraft\");
                }
                PageSelectLeft.AddFolder(ModBase.Path + @".minecraft\", ModBase.GetFolderNameFromPath(ModBase.Path), false);
                ModMinecraft.McFolderListLoader.WaitForExit();
            }
            // 确认 Minecraft 文件夹存在
            ModMinecraft.PathMcFolder = ModBase.Setup.Get("LaunchFolderSelect").ToString().Replace("$", ModBase.Path);
            if (string.IsNullOrEmpty(ModMinecraft.PathMcFolder) || !Directory.Exists(ModMinecraft.PathMcFolder))
            {
                // 无效的文件夹
                if (string.IsNullOrEmpty(ModMinecraft.PathMcFolder))
                {
                    ModBase.Log("[Launch] 没有已储存的 Minecraft 文件夹");
                }
                else
                {
                    ModBase.Log("[Launch] Minecraft 文件夹无效，该文件夹已不存在：" + ModMinecraft.PathMcFolder, ModBase.LogLevel.Debug);
                }
                ModMinecraft.McFolderListLoader.WaitForExit(IsForceRestart: true);
                ModBase.Setup.Set("LaunchFolderSelect", ModMinecraft.McFolderList[0].Path.Replace(ModBase.Path, "$"));
            }
            ModBase.Log("[Launch] Minecraft 文件夹：" + ModMinecraft.PathMcFolder);
            if (Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugDelay")))
                Thread.Sleep(ModBase.RandomInteger(500, 3000));
            // 自动整合包安装
            if (PackInstallPath is not null)
            {
                try
                {
                    var InstallLoader = ModModpack.ModpackInstall(PackInstallPath);
                    ModBase.Log("[Launch] 自动安装整合包已开始：" + PackInstallPath);
                    InstallLoader.WaitForExit();
                    if (InstallLoader.State == ModBase.LoadState.Finished)
                    {
                        ModBase.Log("[Launch] 自动安装整合包成功，清理安装包：" + PackInstallPath);
                        if (File.Exists(PackInstallPath))
                            File.Delete(PackInstallPath);
                    }
                }
                catch (ModBase.CancelledException ex)
                {
                    ModBase.Log(ex, "自动安装整合包被用户取消：" + PackInstallPath);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "自动安装整合包失败：" + PackInstallPath, ModBase.LogLevel.Msgbox);
                }
            }
            // 确认 Minecraft 版本存在
            string Selection = Conversions.ToString(ModBase.Setup.Get("LaunchVersionSelect"));
            var Version = string.IsNullOrEmpty(Selection) ? null : new ModMinecraft.McVersion(Selection);
            if (Version is null || !Version.Path.StartsWithF(ModMinecraft.PathMcFolder) || !Version.Check())
            {
                // 无效的版本
                ModBase.Log("[Launch] 当前选择的 Minecraft 版本无效：" + (Version is null ? "null" : Version.Path), Version == null ? ModBase.LogLevel.Normal : ModBase.LogLevel.Debug);
                if (!(ModMinecraft.McVersionListLoader.State == ModBase.LoadState.Finished))
                    ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\", WaitForExit: true);
                if (!ModMinecraft.McVersionList.Any() || ModMinecraft.McVersionList.First().Value[0].Logo.Contains("RedstoneBlock"))
                {
                    Version = null;
                    ModBase.Setup.Set("LaunchVersionSelect", "");
                    ModBase.Log("[Launch] 无可用 Minecraft 版本");
                }
                else
                {
                    Version = ModMinecraft.McVersionList.First().Value[0];
                    ModBase.Setup.Set("LaunchVersionSelect", Version.Name);
                    ModBase.Log("[Launch] 自动选择 Minecraft 版本：" + Version.Path);
                }
            }
            ModBase.RunInUi(() =>
        {
                ModMinecraft.McVersionCurrent = Version; // 绕这一圈是为了避免 McVersionCheck 触发第二次版本改变
                IsLoadFinished = true;
                RefreshButtonsUI();
                RefreshPage(false, false); // 有可能选择的版本变化了，需要重新刷新
                if (string.IsNullOrEmpty(ModLaunch.McLoginAble()))
                    ModLaunch.McLoginLoader.Start(); // 自动登录
            });
        }, "Version Check", ThreadPriority.AboveNormal);

            // 改变页面
            ModLaunch.McLoginType LoginType = (ModLaunch.McLoginType)Conversions.ToInteger(ModBase.Setup.Get("LoginType"));
            if (LoginType == ModLaunch.McLoginType.Legacy || LoginType == ModLaunch.McLoginType.Ms)
                ((MyRadioButton)this.FindName("RadioLoginType" + ((int)LoginType).ToString())).Checked = true;
            RefreshPage(false, false);

            ModAnimation.AniControlEnabled -= 1;
        }

        #region 切换大页面

        /// <summary>
    /// 切换至启动中页面。
    /// </summary>
        public void PageChangeToLaunching()
        {
            // 修改登陆方式
            switch (ModBase.Setup.Get("LoginType"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, ModLaunch.McLoginType.Legacy, false):
                    {
                        if (PageLinkLobby.HiperState == ModBase.LoadState.Finished)
                        {
                            this.LabLaunchingMethod.Text = "联机离线登录";
                        }
                        else
                        {
                            this.LabLaunchingMethod.Text = "离线登录";
                        }

                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, ModLaunch.McLoginType.Ms, false):
                    {
                        this.LabLaunchingMethod.Text = "正版登录";
                        break;
                    }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, ModLaunch.McLoginType.Nide, false):
                    {
                        this.LabLaunchingMethod.Text = "统一通行证";
                        break;
                    }
                case var case3 when Operators.ConditionalCompareObjectEqual(case3, ModLaunch.McLoginType.Auth, false):
                    {
                        this.LabLaunchingMethod.Text = "Authlib-Injector";
                        break;
                    }
            }
            // 初始化页面
            this.LabLaunchingName.Text = ModMinecraft.McVersionCurrent.Name;
            this.LabLaunchingStage.Text = "初始化";
            this.LabLaunchingTitle.Text = ModLaunch.CurrentLaunchOptions?.SaveBatch is null ? "正在启动游戏" : "正在导出启动脚本";
            this.LabLaunchingProgress.Text = "0.00 %";
            this.LabLaunchingProgress.Opacity = 1d;
            this.LabLaunchingDownload.Visibility = Visibility.Visible;
            this.LabLaunchingProgressLeft.Opacity = 0.6d;
            this.LabLaunchingDownload.Visibility = Visibility.Visible;
            this.LabLaunchingDownload.Text = "0 B/s";
            this.LabLaunchingDownload.Opacity = 0d;
            this.LabLaunchingDownload.Visibility = Visibility.Collapsed;
            this.LabLaunchingDownloadLeft.Opacity = 0d;
            this.LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed;
            this.ProgressLaunchingFinished.Width = new GridLength(0d, GridUnitType.Star);
            this.ProgressLaunchingUnfinished.Width = new GridLength(1d, GridUnitType.Star);
            this.PanLaunchingInfo.Width = double.NaN; // 重置宽度改变动画
            ModLaunch.McLaunchProcess = null;
            ModLaunch.McLaunchWatcher = null;
            // 初始化其他页面
            this.PanInput.IsHitTestVisible = false;
            this.PanLaunching.IsHitTestVisible = false;
            this.LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Run;
            this.PanLaunching.Visibility = Visibility.Visible;
            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.PanInput, 0d, 50), ModAnimation.AaOpacity(this.PanInput, -this.PanInput.Opacity, 110, Ease: new ModAnimation.AniEaseInFluent(), After: true), ModAnimation.AaScaleTransform(this.PanInput, 1.2d - ((ScaleTransform)this.PanInput.RenderTransform).ScaleX, 160), ModAnimation.AaOpacity(this.PanLaunching, 1d - this.PanLaunching.Opacity, 150, 100), ModAnimation.AaScaleTransform(this.PanLaunching, 1d - ((ScaleTransform)this.PanLaunching.RenderTransform).ScaleX, 500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => this.PanLaunching.IsHitTestVisible = true, 150) }, "Launch State Page"); // 略作延迟，这样如果预检测失败，不会出现奇怪的弹一下的动画
        }
        /// <summary>
    /// 切换至登录页面。
    /// </summary>
        public void PageChangeToLogin()
        {
            ((dynamic)PageGet(PageCurrent)).Reload(KeepInput: false);
            this.PanInput.IsHitTestVisible = false;
            this.PanLaunching.IsHitTestVisible = false;
            this.LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Stop;
            this.PanInput.Visibility = Visibility.Visible;
            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.PanLaunching, -this.PanLaunching.Opacity, 150), ModAnimation.AaScaleTransform(this.PanLaunching, 0.8d - ((ScaleTransform)this.PanLaunching.RenderTransform).ScaleX, 150, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaOpacity(this.PanInput, 1d - this.PanInput.Opacity, 250, 50), ModAnimation.AaScaleTransform(this.PanInput, 1d - ((ScaleTransform)this.PanInput.RenderTransform).ScaleX, 300, 50, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => this.PanInput.IsHitTestVisible = true, 200) }, "Launch State Page", true);
        }

        #endregion

        #region 切换登录页面

        private enum PageType
        {
            None,
            Legacy,
            Nide,
            NideSkin,
            Auth,
            AuthSkin,
            Ms,
            MsSkin
        }
        /// <summary>
    /// 当前页面的种类。
    /// </summary>
        private PageType PageCurrent = PageType.None;

        private object PageGet(PageType Type)
        {
            switch (Type)
            {
                case PageType.Legacy:
                    {
                        if (ModMain.FrmLoginLegacy == null)
                            ModMain.FrmLoginLegacy = new PageLoginLegacy();
                        return ModMain.FrmLoginLegacy;
                    }
                case PageType.Nide:
                    {
                        if (ModMain.FrmLoginNide == null)
                            ModMain.FrmLoginNide = new PageLoginNide();
                        return ModMain.FrmLoginNide;
                    }
                case PageType.NideSkin:
                    {
                        if (ModMain.FrmLoginNideSkin == null)
                            ModMain.FrmLoginNideSkin = new PageLoginNideSkin();
                        return ModMain.FrmLoginNideSkin;
                    }
                case PageType.Auth:
                    {
                        if (ModMain.FrmLoginAuth == null)
                            ModMain.FrmLoginAuth = new PageLoginAuth();
                        return ModMain.FrmLoginAuth;
                    }
                case PageType.AuthSkin:
                    {
                        if (ModMain.FrmLoginAuthSkin == null)
                            ModMain.FrmLoginAuthSkin = new PageLoginAuthSkin();
                        return ModMain.FrmLoginAuthSkin;
                    }
                case PageType.Ms:
                    {
                        if (ModMain.FrmLoginMs == null)
                            ModMain.FrmLoginMs = new PageLoginMs();
                        return ModMain.FrmLoginMs;
                    }
                case PageType.MsSkin:
                    {
                        if (ModMain.FrmLoginMsSkin == null)
                            ModMain.FrmLoginMsSkin = new PageLoginMsSkin();
                        return ModMain.FrmLoginMsSkin;
                    }

                default:
                    {
                        throw new ArgumentOutOfRangeException("Type", "即将切换的登录分页编号越界");
                    }
            }
        }
        /// <summary>
    /// 切换现有登录页面种类，返回新页面的实例。
    /// </summary>
    /// <param name="Type">新页面的种类。</param>
    /// <param name="Anim">是否显示动画。</param>
        private object PageChange(PageType Type, bool Anim)
        {
            object PageNew = ModMain.FrmLoginMs; // 初始化一个东西，避免在执行时出现异常导致雪崩
            try
            {

                #region 确定更改的页面实例并实例化
                if (PageCurrent == Type)
                    return PageNew;
                PageNew = PageGet(Type);
                #endregion

                #region 切换页面
                ModAnimation.AniStop("FrmLogin PageChange");
                // 清除页面关联性
                if (!(PageNew == null) && !(((dynamic)PageNew).Parent == null))
                    ((dynamic)PageNew).SetValue(ContentPresenter.ContentProperty, (object)null);
                if (Anim)
                {
                    // 动画
                    // 执行动画
                    this.Dispatcher.Invoke(() => ModAnimation.AniStart(new[] {
                                                     ModAnimation.AaOpacity(this.PanLogin, -this.PanLogin.Opacity, 100, Ease: new ModAnimation.AniEaseOutFluent()),
                                                     ModAnimation.AaCode(() =>
                        {
                                                                ModAnimation.AniControlEnabled += 1;
                                                                this.PanLogin.Children.Clear();
                                                                this.PanLogin.Children.Add((UIElement)PageNew);
                                                                ModAnimation.AniControlEnabled -= 1;
                                                            }, 100),
                                                     ModAnimation.AaOpacity(this.PanLogin, 1d, 100, 120, new ModAnimation.AniEaseInFluent())
                                                                                   }, "FrmLogin PageChange"), System.Windows.Threading.DispatcherPriority.Render);
                }
                else
                {
                    // 无动画
                    ModAnimation.AniControlEnabled += 1;
                    this.PanLogin.Children.Clear();
                    this.PanLogin.Children.Add((UIElement)PageNew);
                    ModAnimation.AniControlEnabled -= 1;
                }
                #endregion

                PageCurrent = Type;
                return PageNew;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "切换登录分页失败（" + ModBase.GetStringFromEnum(Type) + "）", ModBase.LogLevel.Feedback);
                return PageNew;
            }
        }

        /// <summary>
    /// 确认当前显示的子页面正确，并刷新该页面。
    /// </summary>
        public void RefreshPage(bool KeepInput, bool Anim)
        {
            // 获取页面的可用种类并回写缓存
            PageType Type;
            int LoginPageType;
            if (ModMinecraft.McVersionCurrent is not null)
            {
                LoginPageType = Conversions.ToInteger(ModBase.Setup.Get("VersionServerLogin", Version: ModMinecraft.McVersionCurrent));
                // 缓存当前版本的页面种类，下一次打开 McVersionCurrent 为空时才能加载出正确的页面
                ModBase.Setup.Set("LoginPageType", LoginPageType);
            }
            else
            {
                LoginPageType = Conversions.ToInteger(ModBase.Setup.Get("LoginPageType"));
            }
            switch (LoginPageType)
            {
                case 0: // 正版或离线
                    {
                    UnknownType:
                        ;

                        if (this.RadioLoginType5.Checked)
                        {
                            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheMsV2Access"), "", false)))
                            {
                                Type = PageType.Ms;
                            }
                            else
                            {
                                Type = PageType.MsSkin;
                            }
                            ModBase.Setup.Set("LoginType", ModLaunch.McLoginType.Ms);
                        }
                        else
                        {
                            Type = PageType.Legacy;
                            ModBase.Setup.Set("LoginType", ModLaunch.McLoginType.Legacy);
                        }
                        this.PanType.Visibility = Visibility.Visible;
                        this.PanTypeOne.Visibility = Visibility.Collapsed;
                        this.RadioLoginType5.Visibility = Visibility.Visible;
                        this.RadioLoginType0.Visibility = Visibility.Visible;
                        break;
                    }
                case 1: // 仅正版
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheMsV2Access"), "", false)))
                        {
                            Type = PageType.Ms;
                        }
                        else
                        {
                            Type = PageType.MsSkin;
                        }
                        ModBase.Setup.Set("LoginType", ModLaunch.McLoginType.Ms);
                        this.PanType.Visibility = Visibility.Collapsed;
                        this.PanTypeOne.Visibility = Visibility.Visible;
                        this.PathTypeOne.Data = (Geometry)new GeometryConverter().ConvertFromString(ModBase.Logo.IconButtonShield);
                        this.LabTypeOne.Text = "正版登录";
                        this.RadioLoginType5.Visibility = Visibility.Visible;
                        this.RadioLoginType0.Visibility = Visibility.Collapsed;
                        break;
                    }
                case 2: // 仅离线
                    {
                        Type = PageType.Legacy;
                        ModBase.Setup.Set("LoginType", ModLaunch.McLoginType.Legacy);
                        this.PanType.Visibility = Visibility.Collapsed;
                        this.PanTypeOne.Visibility = Visibility.Visible;
                        this.PathTypeOne.Data = (Geometry)new GeometryConverter().ConvertFromString(ModBase.Logo.IconButtonOffline);
                        this.LabTypeOne.Text = "离线登录";
                        break;
                    }
                case 3: // 统一通行证
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheNideAccess"), "", false)))
                        {
                            Type = PageType.Nide;
                        }
                        else
                        {
                            Type = PageType.NideSkin;
                        }
                        ModBase.Setup.Set("LoginType", ModLaunch.McLoginType.Nide);
                        this.PanType.Visibility = Visibility.Collapsed;
                        this.PanTypeOne.Visibility = Visibility.Visible;
                        this.PathTypeOne.Data = (Geometry)new GeometryConverter().ConvertFromString(ModBase.Logo.IconButtonCard);
                        this.LabTypeOne.Text = "统一通行证登录";
                        break;
                    }
                case 4: // Authlib-Injector
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheAuthAccess"), "", false)))
                        {
                            Type = PageType.Auth;
                        }
                        else
                        {
                            Type = PageType.AuthSkin;
                        }
                        ModBase.Setup.Set("LoginType", ModLaunch.McLoginType.Auth);
                        this.PanType.Visibility = Visibility.Collapsed;
                        this.PanTypeOne.Visibility = Visibility.Visible;
                        this.PathTypeOne.Data = (Geometry)new GeometryConverter().ConvertFromString(ModBase.Logo.IconButtonCard);
                        this.LabTypeOne.Text = Conversions.ToString(ModMinecraft.McVersionCurrent is null ? ModBase.Setup.Get("CacheAuthServerName") : ModBase.Setup.Get("VersionServerAuthName", Version: ModMinecraft.McVersionCurrent));
                        if (string.IsNullOrEmpty(this.LabTypeOne.Text))
                            this.LabTypeOne.Text = "第三方登录";
                        break;
                    }

                default:
                    {
                        ModBase.Log("[Control] 未知的登录页面：" + LoginPageType, ModBase.LogLevel.Hint);
                        goto UnknownType;
                        break;
                    }
            }
            // 刷新页面
            if (PageCurrent == Type)
                return;
            ((dynamic)PageChange(Type, Anim)).Reload(KeepInput);
            MyRadioButton Control = (MyRadioButton)this.FindName(Conversions.ToString(Operators.ConcatenateObject("RadioLoginType", ModBase.Setup.Get("LoginType"))));
            if (Control is not null)
                Control.Checked = true;
        }
        private void RadioLoginType_Change(object sender, bool raiseByMouse)
        {
            if (raiseByMouse)
                RefreshPage(true, true);
        }

        #endregion

        #region 皮肤

        // 微软正版皮肤
        public static ModLoader.LoaderTask<ModBase.EqualableList<string>, string> SkinMs = new ModLoader.LoaderTask<ModBase.EqualableList<string>, string>("Loader Skin Ms", SkinMsLoad, SkinMsInput, ThreadPriority.AboveNormal);
        private static ModBase.EqualableList<string> SkinMsInput()
        {
            // 获取名称
            return new ModBase.EqualableList<string>() { Conversions.ToString(ModBase.Setup.Get("CacheMsV2Name")), Conversions.ToString(ModBase.Setup.Get("CacheMsV2Uuid")) };
        }
        private static void SkinMsLoad(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Data)
        {
            // 清空已有皮肤
            // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
            ModBase.RunInUi(() => { if (ModMain.FrmLoginMsSkin is not null && ModMain.FrmLoginMsSkin.Skin is not null) ModMain.FrmLoginMsSkin.Skin.Clear(); });
            // 获取 Url
            string UserName = Data.Input[0];
            string Uuid = Data.Input[1];
            if (string.IsNullOrEmpty(UserName))
            {
                Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(UserName))) + ".png";
                ModBase.Log("[Minecraft] 获取微软正版皮肤失败，ID 为空");
                goto Finish;
            }
            try
            {
                string Result = ModMinecraft.McSkinGetAddress(Uuid, "Ms");
                if (Data.IsAborted)
                    throw new ThreadInterruptedException("当前任务已取消：" + UserName);
                Result = ModMinecraft.McSkinDownload(Result);
                if (Data.IsAborted)
                    throw new ThreadInterruptedException("当前任务已取消：" + UserName);
                Data.Output = Result;
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "ThreadInterruptedException")
                {
                    Data.Output = "";
                    return;
                }
                else if (ModBase.GetExceptionSummary(ex).Contains("429"))
                {
                    Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(UserName))) + ".png";
                    ModBase.Log("[Minecraft] 获取正版皮肤失败（" + UserName + "）：获取皮肤太过频繁，请 5 分钟后再试！", ModBase.LogLevel.Hint);
                }
                else if (ModBase.GetExceptionSummary(ex).Contains("未设置自定义皮肤"))
                {
                    Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(UserName))) + ".png";
                    ModBase.Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载");
                }
                else
                {
                    Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(UserName))) + ".png";
                    ModBase.Log(ex, "获取微软正版皮肤失败（" + UserName + "）", ModBase.LogLevel.Hint);
                }
            }

        Finish:
            ;

            // 刷新显示
            if (ModMain.FrmLoginMsSkin is not null)
            {
                ModBase.RunInUi(ModMain.FrmLoginMsSkin.Skin.Load);
            }
            else if (!Data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            {
                Data.Input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
            }
        }

        // 离线皮肤
        public static ModLoader.LoaderTask<ModBase.EqualableList<string>, string> SkinLegacy = new ModLoader.LoaderTask<ModBase.EqualableList<string>, string>("Loader Skin Legacy", SkinLegacyLoad, SkinLegacyInput, ThreadPriority.AboveNormal);
        private static ModBase.EqualableList<string> SkinLegacyInput()
        {
            // 根据类型判断输入
            int Type = Conversions.ToInteger(ModBase.Setup.Get("LaunchSkinType"));
            switch (Type)
            {
                case 0:
                    {
                        if (ModMain.FrmLoginLegacy is not null && ModMain.FrmLoginLegacy.IsReloaded)
                        {
                            return new ModBase.EqualableList<string>() { 0.ToString(), ModMain.FrmLoginLegacy.ComboName.Text.Trim() ?? "" };
                        }
                        else if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginLegacyName"), "", false)))
                        {
                            return new ModBase.EqualableList<string>() { 0.ToString(), "" };
                        }
                        else
                        {
                            return new ModBase.EqualableList<string>() { 0.ToString(), ModBase.Setup.Get("LoginLegacyName").ToString().BeforeFirst("¨") ?? "" };
                        }
                    }
                case 3:
                    {
                        return new ModBase.EqualableList<string>() { 3.ToString(), Conversions.ToString(ModBase.Setup.Get("LaunchSkinID")) };
                    }

                default:
                    {
                        return new ModBase.EqualableList<string>() { Type.ToString() };
                    }
            }
        }
        private static void SkinLegacyLoad(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Data)
        {
            // 清空已有皮肤
            ModBase.RunInUi(() => { if (ModMain.FrmLoginLegacy is not null && ModMain.FrmLoginLegacy.Skin is not null) ModMain.FrmLoginLegacy.Skin.Clear(); });
            // 获取 Url
            switch (Data.Input[0] ?? "") // 默认
            {
                case "0":
                    {
                        Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(Data.Input[1]))) + ".png"; // Steve
                        break;
                    }

                case "1":
                    {
                    UseDefault:
                        ;

                        Data.Output = ModBase.PathImage + "Skins/Steve.png"; // Alex
                        break;
                    }

                case "2":
                    {
                        Data.Output = ModBase.PathImage + "Skins/Alex.png"; // 正版
                        break;
                    }

                case "3":
                    {
                        string ID = Data.Input[1];
                        try
                        {
                            if (ID.Count() < 2)
                            {
                                Data.Output = ModBase.PathImage + "Skins/Steve.png";
                            }
                            else
                            {
                                string Result = Conversions.ToString(ModLaunch.McLoginMojangUuid(ID, true));
                                if (Data.IsAborted)
                                    throw new ThreadInterruptedException("当前任务已取消：" + ID);
                                Result = ModMinecraft.McSkinGetAddress(Result, "Mojang");
                                if (Data.IsAborted)
                                    throw new ThreadInterruptedException("当前任务已取消：" + ID);
                                Result = ModMinecraft.McSkinDownload(Result);
                                if (Data.IsAborted)
                                    throw new ThreadInterruptedException("当前任务已取消：" + ID);
                                Data.Output = Result;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.GetType().Name == "ThreadInterruptedException")
                            {
                                Data.Output = "";
                                return;
                            }
                            else if (ModBase.GetExceptionSummary(ex).Contains("429"))
                            {
                                Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(ID))) + ".png";
                                ModBase.Log("获取离线登录使用的正版皮肤失败（" + ID + "）：获取皮肤太过频繁，请 5 分钟后再试！");
                            }
                            else
                            {
                                Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(ID))) + ".png";
                                ModBase.Log(ex, "获取离线登录使用的正版皮肤失败（" + ID + "）");
                            }
                        } // 自定义

                        break;
                    }

                case "4":
                    {
                        if (!File.Exists(ModBase.PathAppdata + "CustomSkin.png"))
                        {
                            ModMain.Hint("未找到离线皮肤自定义文件，可能它已被删除。PCL 将使用默认的 Steve 皮肤！");
                            ModBase.Setup.Set("LaunchSkinType", 1);
                            goto UseDefault;
                        }
                        Data.Output = ModBase.PathAppdata + "CustomSkin.png";
                        break;
                    }
            }
            // 刷新显示
            if (ModMain.FrmLoginLegacy is not null)
            {
                ModBase.RunInUi(ModMain.FrmLoginLegacy.Skin.Load);
            }
            else if (!Data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            {
                Data.Input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
            }
        }

        // 统一通行证皮肤
        public static ModLoader.LoaderTask<ModBase.EqualableList<string>, string> SkinNide = new ModLoader.LoaderTask<ModBase.EqualableList<string>, string>("Loader Skin Nide", SkinNideLoad, SkinNideInput, ThreadPriority.AboveNormal);
        private static ModBase.EqualableList<string> SkinNideInput()
        {
            // 获取名称
            return new ModBase.EqualableList<string>() { Conversions.ToString(ModBase.Setup.Get("CacheNideName")), Conversions.ToString(ModBase.Setup.Get("CacheNideUuid")) };
        }
        private static void SkinNideLoad(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Data)
        {
            // 清空已有皮肤
            // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
            ModBase.RunInUi(() => { if (ModMain.FrmLoginNideSkin is not null && ModMain.FrmLoginNideSkin.Skin is not null) ModMain.FrmLoginNideSkin.Skin.Clear(); });
            // 获取 Url
            string UserName = Data.Input[0];
            string Uuid = Data.Input[1];
            if (string.IsNullOrEmpty(UserName))
            {
                Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Conversions.ToString(ModLaunch.McLoginLegacyUuid(UserName))) + ".png";
                ModBase.Log("[Minecraft] 获取统一通行证皮肤失败，ID 为空");
                goto Finish;
            }
            try
            {
                string Result = ModMinecraft.McSkinGetAddress(Uuid, "Nide");
                if (Data.IsAborted)
                    throw new ThreadInterruptedException("当前任务已取消：" + UserName);
                Result = ModMinecraft.McSkinDownload(Result);
                if (Data.IsAborted)
                    throw new ThreadInterruptedException("当前任务已取消：" + UserName);
                Data.Output = Result;
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "ThreadInterruptedException")
                {
                    Data.Output = "";
                    return;
                }
                else if (ModBase.GetExceptionSummary(ex).Contains("429"))
                {
                    Data.Output = ModBase.PathImage + "Skins/Steve.png";
                    ModBase.Log("[Minecraft] 获取统一通行证皮肤失败（" + UserName + "）：获取皮肤太过频繁，请 5 分钟后再试！", ModBase.LogLevel.Hint);
                }
                else if (ModBase.GetExceptionSummary(ex).Contains("未设置自定义皮肤"))
                {
                    Data.Output = ModBase.PathImage + "Skins/Steve.png";
                    ModBase.Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载");
                }
                else
                {
                    Data.Output = ModBase.PathImage + "Skins/Steve.png";
                    ModBase.Log(ex, "获取统一通行证皮肤失败（" + UserName + "）", ModBase.LogLevel.Hint);
                }
            }

        Finish:
            ;

            // 刷新显示
            if (ModMain.FrmLoginNideSkin is not null)
            {
                ModBase.RunInUi(ModMain.FrmLoginNideSkin.Skin.Load);
            }
            else if (!Data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            {
                Data.Input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
            }
        }

        // Authlib-Injector 皮肤
        public static ModLoader.LoaderTask<ModBase.EqualableList<string>, string> SkinAuth = new ModLoader.LoaderTask<ModBase.EqualableList<string>, string>("Loader Skin Auth", SkinAuthLoad, SkinAuthInput, ThreadPriority.AboveNormal);
        private static ModBase.EqualableList<string> SkinAuthInput()
        {
            // 获取名称
            return new ModBase.EqualableList<string>() { Conversions.ToString(ModBase.Setup.Get("CacheAuthName")), Conversions.ToString(ModBase.Setup.Get("CacheAuthUuid")) };
        }
        private static void SkinAuthLoad(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Data)
        {
            // 清空已有皮肤
            // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
            ModBase.RunInUi(() => { if (ModMain.FrmLoginAuthSkin is not null && ModMain.FrmLoginAuthSkin.Skin is not null) ModMain.FrmLoginAuthSkin.Skin.Clear(); });
            // 获取 Url
            string UserName = Data.Input[0];
            string Uuid = Data.Input[1];
            if (string.IsNullOrEmpty(UserName))
            {
                Data.Output = ModBase.PathImage + "Skins/Steve.png";
                ModBase.Log("[Minecraft] 获取 Authlib-Injector 皮肤失败，ID 为空");
                goto Finish;
            }
            try
            {
                string Result = ModMinecraft.McSkinGetAddress(Uuid, "Auth");
                if (Data.IsAborted)
                    throw new ThreadInterruptedException("当前任务已取消：" + UserName);
                Result = ModMinecraft.McSkinDownload(Result);
                if (Data.IsAborted)
                    throw new ThreadInterruptedException("当前任务已取消：" + UserName);
                Data.Output = Result;
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "ThreadInterruptedException")
                {
                    Data.Output = "";
                    return;
                }
                else if (ModBase.GetExceptionSummary(ex).Contains("429"))
                {
                    Data.Output = ModBase.PathImage + "Skins/Steve.png";
                    ModBase.Log("[Minecraft] 获取 Authlib-Injector 皮肤失败（" + UserName + "）：获取皮肤太过频繁，请 5 分钟后再试！", ModBase.LogLevel.Hint);
                }
                else if (ModBase.GetExceptionSummary(ex).Contains("未设置自定义皮肤"))
                {
                    Data.Output = ModBase.PathImage + "Skins/Steve.png";
                    ModBase.Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载");
                }
                else
                {
                    Data.Output = ModBase.PathImage + "Skins/Steve.png";
                    ModBase.Log(ex, "获取 Authlib-Injector 皮肤失败（" + UserName + "）", ModBase.LogLevel.Hint);
                }
            }

        Finish:
            ;

            // 刷新显示
            if (ModMain.FrmLoginAuthSkin is not null)
            {
                ModBase.RunInUi(ModMain.FrmLoginAuthSkin.Skin.Load);
            }
            else if (!Data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            {
                Data.Input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
            }
        }

        // 全部皮肤加载器
        // 需要放在其中元素的后面，否则会因为它提前被加载而莫名其妙变成 Nothing
        public static List<ModLoader.LoaderTask<ModBase.EqualableList<string>, string>> SkinLoaders = new List<ModLoader.LoaderTask<ModBase.EqualableList<string>, string>>() { SkinMs, SkinLegacy, SkinNide, SkinAuth };

        #endregion

        // 版本选择按钮
        private void BtnVersion_Click(object sender, EventArgs e)
        {
            if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
                return;
            ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.VersionSelect);
        }
        // 启动按钮
        public void LaunchButtonClick()
        {
            if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading || !this.BtnLaunch.IsEnabled || ModMain.FrmMain.PageRight is not null && ModMain.FrmMain.PageRight.PageState != MyPageRight.PageStates.ContentStay && ModMain.FrmMain.PageRight.PageState != MyPageRight.PageStates.ContentEnter)
                return;
            // 愚人节处理
            if (ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup)
            {
                ModSecret.ThemeUnlock(12, false, "隐藏主题 滑稽彩 已解锁！");
                ModMain.IsAprilGiveup = true;
                ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleX = 1d;
                ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleY = 1d;
                ModMain.FrmLaunchLeft.AprilPosTrans.X = 0d;
                ModMain.FrmLaunchLeft.AprilPosTrans.Y = 0d;
                ModMain.FrmMain.BtnExtraApril.ShowRefresh();
            }
            // 实际的启动
            if (this.BtnLaunch.Text == "启动游戏")
            {
                if (File.Exists(ModMinecraft.McVersionCurrent.Path + ".pclignore"))
                {
                    ModMain.Hint("当前版本正在安装，无法启动！", ModMain.HintType.Critical);
                    return;
                }
                ModLaunch.McLaunchStart();
            }
            else if (this.BtnLaunch.Text == "下载游戏")
            {
                ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
            }
        }
        private int BtnLaunchState = 0;
        private ModMinecraft.McVersion BtnLaunchVersion = null;
        public void RefreshButtonsUI()
        {
            if (!this.BtnLaunch.IsLoaded)
                return;
            // 获取当前状态
            int CurrentState;
            if (!IsLoadFinished || ModMinecraft.McVersionListLoader.State == ModBase.LoadState.Loading || ModMinecraft.McFolderListLoader.State == ModBase.LoadState.Loading)
            {
                CurrentState = 0;
            }
            else if (ModMinecraft.McVersionCurrent is null)
            {
                if ((bool)ModBase.Setup.Get("UiHiddenPageDownload") && !PageSetupUI.HiddenForceShow)
                {
                    CurrentState = 1;
                }
                else
                {
                    CurrentState = 2;
                }
            }
            else
            {
                CurrentState = 3;
            }
            // 更新状态
            if (CurrentState == BtnLaunchState && ((ModMinecraft.McVersionCurrent is null ? "" : ModMinecraft.McVersionCurrent.Path) ?? "") == ((BtnLaunchVersion is null ? "" : BtnLaunchVersion.Path) ?? ""))
                goto ExitRefresh;
            BtnLaunchVersion = ModMinecraft.McVersionCurrent;
            BtnLaunchState = CurrentState;
            switch (CurrentState)
            {
                case 0:
                    {
                        ModBase.Log("[Minecraft] 启动按钮：正在加载 Minecraft 版本");
                        ModMain.FrmLaunchLeft.BtnLaunch.Text = "正在加载";
                        ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = false;
                        ModMain.FrmLaunchLeft.LabVersion.Text = "正在加载中，请稍候";
                        ModMain.FrmLaunchLeft.BtnVersion.IsEnabled = false;
                        ModMain.FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed;
                        break;
                    }
                case 1:
                    {
                        ModBase.Log("[Minecraft] 启动按钮：无 Minecraft 版本，下载已禁用");
                        ModMain.FrmLaunchLeft.BtnLaunch.Text = "启动游戏";
                        ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = false;
                        ModMain.FrmLaunchLeft.LabVersion.Text = "未找到可用的游戏版本";
                        ModMain.FrmLaunchLeft.BtnVersion.IsEnabled = true;
                        ModMain.FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed;
                        break;
                    }
                case 2:
                    {
                        ModBase.Log("[Minecraft] 启动按钮：无 Minecraft 版本，要求下载");
                        ModMain.FrmLaunchLeft.BtnLaunch.Text = "下载游戏";
                        ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = true;
                        ModMain.FrmLaunchLeft.LabVersion.Text = "未找到可用的游戏版本";
                        ModMain.FrmLaunchLeft.BtnVersion.IsEnabled = true;
                        ModMain.FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed;
                        break;
                    }
                case 3:
                    {
                        ModBase.Log("[Minecraft] 启动按钮：Minecraft 版本：" + ModMinecraft.McVersionCurrent.Path);
                        ModMain.FrmLaunchLeft.BtnLaunch.Text = "启动游戏";
                        ModMain.FrmLaunchLeft.BtnVersion.IsEnabled = true;
                        ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = true;
                        ModMain.FrmLaunchLeft.LabVersion.Text = ModMinecraft.McVersionCurrent.Name;
                        break;
                    }
                    // FrmLaunchLeft.BtnMore.Visibility = Visibility.Visible '由功能隐藏设置修改
            }

        ExitRefresh:
            ;

            // 功能隐藏
            ModMain.FrmLaunchLeft.BtnVersion.Visibility = !PageSetupUI.HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenFunctionSelect") ? Visibility.Collapsed : Visibility.Visible;
            if (CurrentState == 3)
            {
                ModMain.FrmLaunchLeft.BtnMore.Visibility = ModMain.FrmLaunchLeft.BtnVersion.Visibility;
            }
        }
        // 取消按钮
        private void BtnCancel_Click()
        {
            if (ModLaunch.McLaunchLoaderReal is not null)
            {
                ModLaunch.McLaunchLoaderReal.Abort();
                ModLaunch.McLaunchLog("已取消启动");
                try
                {
                    if (ModLaunch.McLaunchWatcher is not null)
                    {
                        ModLaunch.McLaunchWatcher.Kill();
                    }
                    else if (ModLaunch.McLaunchProcess is not null)
                    {
                        if (!ModLaunch.McLaunchProcess.HasExited)
                            ModLaunch.McLaunchProcess.Kill();
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "取消启动结束进程失败", ModBase.LogLevel.Hint);
                }
            }
        }
        // 版本设置按钮
        private void BtnMore_Click(object sender, EventArgs e)
        {
            if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
                return;
            ModMinecraft.McVersionCurrent.Load();
            PageVersionLeft.Version = ModMinecraft.McVersionCurrent;
            if (File.Exists(ModMinecraft.McVersionCurrent.Path + ".pclignore"))
            {
                ModMain.Hint("当前版本正在安装，暂无法进行版本设置！", ModMain.HintType.Critical);
                return;
            }
            ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.VersionSetup, 0);
        }
        /// <summary>
    /// 每 0.2s 执行一次，刷新启动的数据 UI 显示。
    /// </summary>
        public void LaunchingRefresh()
        {
            try
            {
                if (ModLaunch.McLaunchLoaderReal.State == ModBase.LoadState.Aborted)
                    return;
                // 阶段状态获取
                bool IsLaunched = false; // 是否已经启动游戏，只是在等待窗口
                do
                {
                    try
                    {
                        bool exitTry = false;
                        foreach (var Loader in ModLaunch.McLaunchLoaderReal.GetLoaderList(false))
                        {
                            if (Loader.State == ModBase.LoadState.Loading || Loader.State == ModBase.LoadState.Waiting)
                            {
                                this.LabLaunchingStage.Text = Loader.Name;
                                IsLaunched = Loader.Name == "等待游戏窗口出现" || Loader.Name == "结束处理";
                                exitTry = true;
                                break;
                            }
                        }

                        if (exitTry)
                        {
                            break;
                        }
                        this.LabLaunchingStage.Text = "已完成";
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "获取是否启动完成失败，可能是由于启动状态改变导致集合已修改");
                        return;
                    }
                }
                while (false);
                if (ModAnimation.AniIsRun("Launch State Page"))
                    IsLaunched = false; // 等待页面切换动画完成
                                        // 计算应显示的进度
                double ActualProgress = ModLaunch.McLaunchLoaderReal.Progress;
                if (ActualProgress >= ShowProgress)
                    ShowProgress += (ActualProgress - ShowProgress) * 0.2d + 0.005d; // 向实际进度靠一点
                if (ActualProgress <= ShowProgress)
                    ShowProgress = ActualProgress; // 原来或处理后变得比实际进度高，直接回退
                if (IsLaunched)
                    ShowProgress = 1d; // 如果已经完成了，就不卖关子了
                                       // 文本
                this.LabLaunchingTitle.Text = IsLaunched ? "已启动游戏" : ModLaunch.CurrentLaunchOptions.SaveBatch is null ? "正在启动游戏" : "正在导出启动脚本";
                this.LabLaunchingProgress.Text = ModBase.StrFillNum(ShowProgress * 100d, 2) + " %";
                bool HasLaunchDownloader = false;
                try
                {
                    foreach (var Loader in ModNet.NetManager.Tasks)
                    {
                        if (Loader.RealParent is not null && Loader.RealParent.Name == "Minecraft 启动" && Loader.State == ModBase.LoadState.Loading)
                            HasLaunchDownloader = true;
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取 Minecraft 启动下载器失败，可能是因为启动被取消");
                    HasLaunchDownloader = false;
                }
                this.LabLaunchingDownload.Text = ModBase.GetString(ModNet.NetManager.Speed) + "/s";
                // 进度改变动画
                var AnimList = new List<ModAnimation.AniData>() { ModAnimation.AaGridLengthWidth(this.ProgressLaunchingFinished, ShowProgress - this.ProgressLaunchingFinished.Width.Value, 260, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaGridLengthWidth(this.ProgressLaunchingUnfinished, 1d - ShowProgress - this.ProgressLaunchingUnfinished.Width.Value, 260, Ease: new ModAnimation.AniEaseOutFluent()) };
                bool IsDownloadStateChanged = HasLaunchDownloader == (this.LabLaunchingDownload.Visibility == Visibility.Collapsed);
                if (IsDownloadStateChanged)
                {
                    this.LabLaunchingDownload.Visibility = Visibility.Visible;
                    this.LabLaunchingDownloadLeft.Visibility = Visibility.Visible;
                    AnimList.AddRange(new[] { ModAnimation.AaOpacity(this.LabLaunchingDownload, (double)(HasLaunchDownloader ? 1 : 0) - this.LabLaunchingDownload.Opacity, 100), ModAnimation.AaOpacity(this.LabLaunchingDownloadLeft, (HasLaunchDownloader ? 0.5d : 0d) - this.LabLaunchingDownloadLeft.Opacity, 100), ModAnimation.AaCode(() => { if (!HasLaunchDownloader) { this.LabLaunchingDownload.Visibility = Visibility.Collapsed; this.LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed; } }, 110) });
                }
                bool IsProgressStateChanged = !IsLaunched == (this.LabLaunchingProgress.Visibility == Visibility.Collapsed);
                if (IsProgressStateChanged)
                {
                    this.LabLaunchingProgress.Visibility = Visibility.Visible;
                    this.LabLaunchingProgressLeft.Visibility = Visibility.Visible;
                    AnimList.AddRange(new[] { ModAnimation.AaOpacity(this.LabLaunchingProgress, (double)(!IsLaunched ? 1 : 0) - this.LabLaunchingProgress.Opacity, 100), ModAnimation.AaOpacity(this.LabLaunchingProgressLeft, (!IsLaunched ? 0.5d : 0d) - this.LabLaunchingProgressLeft.Opacity, 100) });
                }
                ModAnimation.AniStart(AnimList, "Launching Progress");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新启动信息失败", ModBase.LogLevel.Feedback);
            }
        }
        private double ShowProgress = 0d;
        // 尺寸改变动画
        private bool IsWidthAnimating = false;
        private double ActualUsedWidth;
        private void PanLaunchingInfo_SizeChangedW(object sender, SizeChangedEventArgs e)
        {
            double DeltaWidth = e.NewSize.Width - e.PreviousSize.Width;
            if (e.PreviousSize.Width == 0d || IsWidthAnimating || Math.Abs(DeltaWidth) < 1d || this.PanLaunchingInfo.ActualWidth == 0d)
                return;
            ModAnimation.AniStart(new[] {
            ModAnimation.AaWidth(this.PanLaunchingInfo, DeltaWidth, 180, Ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() =>
                {
                       IsWidthAnimating = false;
                       this.PanLaunchingInfo.Width = ActualUsedWidth;
                   }, After: true)
        }, "Launching Info Width");
            IsWidthAnimating = true;
            ActualUsedWidth = this.PanLaunchingInfo.Width;
            this.PanLaunchingInfo.Width = e.PreviousSize.Width;
        }
        private bool IsHeightAnimating = false;
        private double ActualUsedHeight;
        private void PanLaunchingInfo_SizeChangedH(object sender, SizeChangedEventArgs e)
        {
            double DeltaHeight = e.NewSize.Height - e.PreviousSize.Height;
            if (e.PreviousSize.Height == 0d || IsHeightAnimating || Math.Abs(DeltaHeight) < 1d || this.PanLaunchingInfo.ActualHeight == 0d)
                return;
            ModAnimation.AniStart(new[] {
            ModAnimation.AaHeight(this.PanLaunchingInfo, DeltaHeight, 180, Ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() =>
                {
                       IsHeightAnimating = false;
                       this.PanLaunchingInfo.Height = ActualUsedHeight;
                   }, After: true)
        }, "Launching Info Height");
            IsHeightAnimating = true;
            ActualUsedHeight = this.PanLaunchingInfo.Height;
            this.PanLaunchingInfo.Height = e.PreviousSize.Height;
        }

    }
}