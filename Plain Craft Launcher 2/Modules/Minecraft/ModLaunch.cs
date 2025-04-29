using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public static class ModLaunch
    {

        #region 开始

        public static McLaunchOptions CurrentLaunchOptions = null;
        public class McLaunchOptions
        {
            /// <summary>
        /// 强制指定在启动后进入的服务器 IP。
        /// 默认值：Nothing。使用版本设置的值。
        /// </summary>
            public string ServerIp = null;
            /// <summary>
        /// 指定在启动之后进入的存档名称。
        /// 默认值：Nothing。使用版本设置的值。
        /// </summary>
            public string WorldName = null;
            /// <summary>
        /// 将启动脚本保存到该地址，然后取消启动。这同时会改变启动时的提示等。
        /// 默认值：Nothing。不保存。
        /// </summary>
            public string SaveBatch = null;
            /// <summary>
        /// 强行指定启动的 MC 版本。
        /// 默认值：Nothing。使用 McVersionCurrent。
        /// </summary>
            public ModMinecraft.McVersion Version = null;
            /// <summary>
        /// 额外的启动参数。
        /// </summary>
            public List<string> ExtraArgs = new List<string>();
            /// <summary>
        /// 是否为 “测试游戏” 按钮启动的游戏。
        /// 如果是，则显示游戏实时日志。
        /// </summary>
            public bool Test = false;
        }
        /// <summary>
    /// 尝试启动 Minecraft。必须在 UI 线程调用。
    /// 返回是否实际开始了启动（如果没有，则一定弹出了错误提示）。
    /// </summary>
        public static bool McLaunchStart(McLaunchOptions Options = null)
        {
            CurrentLaunchOptions = Options ?? new McLaunchOptions();
            // 预检查
            if (!ModBase.RunInUi())
                throw new Exception("McLaunchStart 必须在 UI 线程调用！");
            if (McLaunchLoader.State == ModBase.LoadState.Loading)
            {
                ModMain.Hint("已有游戏正在启动中！", ModMain.HintType.Critical);
                return false;
            }
            // 强制切换需要启动的版本
            if (CurrentLaunchOptions.Version is not null && ModMinecraft.McVersionCurrent != CurrentLaunchOptions.Version)
            {
                McLaunchLog("在启动前切换到版本 " + CurrentLaunchOptions.Version.Name);
                // 检查版本
                CurrentLaunchOptions.Version.Load();
                if (CurrentLaunchOptions.Version.State == ModMinecraft.McVersionState.Error)
                {
                    ModMain.Hint("无法启动 Minecraft：" + CurrentLaunchOptions.Version.Info, ModMain.HintType.Critical);
                    return false;
                }
                // 切换版本
                ModMinecraft.McVersionCurrent = CurrentLaunchOptions.Version;
                ModBase.Setup.Set("LaunchVersionSelect", ModMinecraft.McVersionCurrent.Name);
                ModMain.FrmLaunchLeft.RefreshButtonsUI();
                ModMain.FrmLaunchLeft.RefreshPage(false, false);
            }
            ModMain.FrmMain.AprilGiveup();
            // 禁止进入版本选择页面（否则就可以在启动中切换 McVersionCurrent 了）
            ModMain.FrmMain.PageStack = ModMain.FrmMain.PageStack.Where(p => p.Page != FormMain.PageType.VersionSelect).ToList();
            // 实际启动加载器
            McLaunchLoader.Start(Options, IsForceRestart: true);
            return true;
        }

        /// <summary>
    /// 记录启动日志。
    /// </summary>
        public static void McLaunchLog(string Text)
        {
            Text = ModSecret.SecretFilter(Text, '*');
            ModBase.RunInUi(() => ModMain.FrmLaunchRight.LabLog.Text += Constants.vbCrLf + "[" + ModBase.GetTimeNow() + "] " + Text);
            ModBase.Log("[Launch] " + Text);
        }

        // 启动状态切换
        public static ModLoader.LoaderTask<McLaunchOptions, object> McLaunchLoader = new ModLoader.LoaderTask<McLaunchOptions, object>("Loader Launch", McLaunchStart) { OnStateChanged = (_) => ModLaunch.McLaunchState() };
        public static ModLoader.LoaderCombo<object> McLaunchLoaderReal;
        public static Process McLaunchProcess;
        public static ModWatcher.Watcher McLaunchWatcher;
        private static void McLaunchState(ModLoader.LoaderTask<McLaunchOptions, object> Loader)
        {
            switch (McLaunchLoader.State)
            {
                case ModBase.LoadState.Finished:
                case ModBase.LoadState.Failed:
                case ModBase.LoadState.Waiting:
                case ModBase.LoadState.Aborted:
                    {
                        ModMain.FrmLaunchLeft.PageChangeToLogin();
                        break;
                    }
                case ModBase.LoadState.Loading:
                    {
                        // 在预检测结束后再触发动画
                        ModMain.FrmLaunchRight.LabLog.Text = "";
                        break;
                    }
            }
        }
        /// <summary>
    /// 指定启动中断时的提示文本。若不为 Nothing 则会显示为绿色。
    /// </summary>
        private static string AbortHint = null;

        // 实际的启动方法
        private static void McLaunchStart(ModLoader.LoaderTask<McLaunchOptions, object> Loader)
        {
            // 开始动画
            ModBase.RunInUiWait(ModMain.FrmLaunchLeft.PageChangeToLaunching);
            // 预检测（预检测的错误将直接抛出）
            try
            {
                McLaunchPrecheck();
                McLaunchLog("预检测已通过");
            }
            catch (Exception ex)
            {
                if (!ex.Message.StartsWithF("$$"))
                    ModMain.Hint(ex.Message, ModMain.HintType.Critical);
                throw;
            }
            // 正式加载
            try
            {
                // 构造主加载器
                var Loaders = new List<ModLoader.LoaderBase>() { new ModLoader.LoaderTask<int, int>("获取 Java", McLaunchJava) { ProgressWeight = 4d, Block = false }, McLoginLoader, new ModLoader.LoaderCombo<string>("补全文件", ModDownload.DlClientFix(ModMinecraft.McVersionCurrent, false, ModDownload.AssetsIndexExistsBehaviour.DownloadInBackground)) { ProgressWeight = 15d, Show = false }, new ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>>("获取启动参数", McLaunchArgumentMain) { ProgressWeight = 2d }, new ModLoader.LoaderTask<List<ModMinecraft.McLibToken>, int>("解压文件", McLaunchNatives) { ProgressWeight = 2d }, new ModLoader.LoaderTask<int, int>("预启动处理", (_) => McLaunchPrerun()) { ProgressWeight = 1d }, new ModLoader.LoaderTask<int, int>("执行自定义命令", McLaunchCustom) { ProgressWeight = 1d }, new ModLoader.LoaderTask<int, Process>("启动进程", McLaunchRun) { ProgressWeight = 2d }, new ModLoader.LoaderTask<Process, int>("等待游戏窗口出现", McLaunchWait) { ProgressWeight = 1d }, new ModLoader.LoaderTask<int, int>("结束处理", (_) => McLaunchEnd()) { ProgressWeight = 1d } }; // .ProgressWeight = 15, .Block = False
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          // 内存优化
                switch (ModBase.Setup.Get("VersionRamOptimize", Version: ModMinecraft.McVersionCurrent))
                {
                    case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 全局
                        {
                            if (Conversions.ToBoolean(ModBase.Setup.Get("LaunchArgumentRam"))) // 使用全局设置
                            {
                                ((ModLoader.LoaderCombo<string>)Loaders[2]).Block = false;
                                Loaders.Insert(3, new ModLoader.LoaderTask<int, int>("内存优化", McLaunchMemoryOptimize) { ProgressWeight = 30d });
                            }

                            break;
                        }
                    case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 开启
                        {
                            ((ModLoader.LoaderCombo<string>)Loaders[2]).Block = false;
                            Loaders.Insert(3, new ModLoader.LoaderTask<int, int>("内存优化", McLaunchMemoryOptimize) { ProgressWeight = 30d });
                            break;
                        }
                    case var case2 when Operators.ConditionalCompareObjectEqual(case2, 2, false): // 关闭
                        {
                            break;
                        }
                }
                var LaunchLoader = new ModLoader.LoaderCombo<object>("Minecraft 启动", Loaders) { Show = false };
                if (McLoginLoader.State == ModBase.LoadState.Finished)
                    McLoginLoader.State = ModBase.LoadState.Waiting; // 要求重启登录主加载器，它会自行决定是否启动副加载器
                                                                     // 等待加载器执行并更新 UI
                McLaunchLoaderReal = LaunchLoader;
                AbortHint = null;
                LaunchLoader.Start();
                // 任务栏进度条
                ModLoader.LoaderTaskbarAdd(LaunchLoader);
                while (LaunchLoader.State == ModBase.LoadState.Loading)
                {
                    ModMain.FrmLaunchLeft.Dispatcher.Invoke(ModMain.FrmLaunchLeft.LaunchingRefresh);
                    Thread.Sleep(200);
                }
                ModMain.FrmLaunchLeft.Dispatcher.Invoke(ModMain.FrmLaunchLeft.LaunchingRefresh);
                // 成功与失败处理
                switch (LaunchLoader.State)
                {
                    case ModBase.LoadState.Finished:
                        {
                            ModMain.Hint(ModMinecraft.McVersionCurrent.Name + " 启动成功！", ModMain.HintType.Finish);
                            break;
                        }
                    case ModBase.LoadState.Aborted:
                        {
                            if (AbortHint is null)
                            {
                                ModMain.Hint(CurrentLaunchOptions?.SaveBatch is null ? "已取消启动！" : "已取消导出启动脚本！", ModMain.HintType.Info);
                            }
                            else
                            {
                                ModMain.Hint(AbortHint, ModMain.HintType.Finish);
                            }

                            break;
                        }
                    case ModBase.LoadState.Failed:
                        {
                            throw LaunchLoader.Error;
                        }

                    default:
                        {
                            throw new Exception("错误的状态改变：" + ModBase.GetStringFromEnum(LaunchLoader.State));
                        }
                }
            }
            catch (Exception ex)
            {
                var CurrentEx = ex;
            NextInner:
                ;

                if (CurrentEx.Message.StartsWithF("$"))
                {
                    // 若有以 $ 开头的错误信息，则以此为准显示提示
                    // 若错误信息为 $$，则不提示
                    if (!(CurrentEx.Message == "$$"))
                        ModMain.MyMsgBox(CurrentEx.Message.TrimStart('$'), CurrentLaunchOptions?.SaveBatch is null ? "启动失败" : "导出启动脚本失败");
                    throw;
                }
                else if (CurrentEx.InnerException is not null)
                {
                    // 检查下一级错误
                    CurrentEx = CurrentEx.InnerException;
                    goto NextInner;
                }
                else
                {
                    // 没有特殊处理过的错误信息
                    McLaunchLog("错误：" + ModBase.GetExceptionDetail(ex));
                    ModBase.Log(ex, CurrentLaunchOptions?.SaveBatch is null ? "Minecraft 启动失败" : "导出启动脚本失败", ModBase.LogLevel.Msgbox, CurrentLaunchOptions?.SaveBatch is null ? "启动失败" : "导出启动脚本失败");
                    throw;
                }
            }
        }

        #endregion

        #region 内存优化

        private static void McLaunchMemoryOptimize(ModLoader.LoaderTask<int, int> Loader)
        {
            McLaunchLog("内存优化开始");
            bool Finished = false;
            ModBase.RunInNewThread(() =>
        {
            PageOtherTest.MemoryOptimize(false);
            Finished = true;
        }, "Launch Memory Optimize");
            while (!Finished && !Loader.IsAborted)
            {
                if (Loader.Progress < 0.7d)
                {
                    Loader.Progress += 0.007d; // 10s
                }
                else
                {
                    Loader.Progress += (0.95d - Loader.Progress) * 0.02d;
                } // 最快 += 0.005
                Thread.Sleep(100);
            }
        }

        #endregion

        #region 预检测

        private static void McLaunchPrecheck()
        {
            if (Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugDelay")))
                Thread.Sleep(ModBase.RandomInteger(100, 2000));
            // 检查路径
            if (ModMinecraft.McVersionCurrent.PathIndie.Contains("!") || ModMinecraft.McVersionCurrent.PathIndie.Contains(";"))
                throw new Exception("游戏路径中不可包含 ! 或 ;（" + ModMinecraft.McVersionCurrent.PathIndie + "）");
            if (ModMinecraft.McVersionCurrent.Path.Contains("!") || ModMinecraft.McVersionCurrent.Path.Contains(";"))
                throw new Exception("游戏路径中不可包含 ! 或 ;（" + ModMinecraft.McVersionCurrent.Path + "）");
            // 检查版本
            if (ModMinecraft.McVersionCurrent is null)
                throw new Exception("未选择 Minecraft 版本！");
            ModMinecraft.McVersionCurrent.Load();
            if (ModMinecraft.McVersionCurrent.State == ModMinecraft.McVersionState.Error)
                throw new Exception("Minecraft 存在问题：" + ModMinecraft.McVersionCurrent.Info);
            // 检查输入信息
            string CheckResult = "";
            ModBase.RunInUiWait(() => CheckResult = McLoginAble(McLoginInput()));
            if (!string.IsNullOrEmpty(CheckResult))
                throw new ArgumentException(CheckResult);
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If BETA Then
            *//* TODO ERROR: Skipped DisabledTextTrivia
                    '求赞助
                    If CurrentLaunchOptions?.SaveBatch Is Nothing Then '保存脚本时不提示
                        RunInNewThread(
                        Sub()
                            Select Case Setup.Get("SystemLaunchCount")
                                Case 10, 20, 40, 60, 80, 100, 120, 150, 200, 250, 300, 350, 400, 500, 600, 700, 800, 900, 1000, 1200, 1400, 1600, 1800, 2000
                                    If MyMsgBox("PCL 已经为你启动了 " & Setup.Get("SystemLaunchCount") & " 次游戏啦！" & vbCrLf &
                                                "如果 PCL 还算好用的话，能不能考虑赞助一下 PCL……" & vbCrLf &
                                                "如果没有大家的支持，PCL 很难在免费、无任何广告的情况下维持数年的更新（磕头）……！",
                                                Setup.Get("SystemLaunchCount") & " 次启动！", "支持 PCL！", "但是我拒绝") = 1 Then
                                        OpenWebsite("https://afdian.com/a/LTCat")
                                    End If
                            End Select
                        End Sub, "Donate")
                    End If
            *//* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */        // 正版购买提示
            if (Conversions.ToBoolean(CurrentLaunchOptions?.SaveBatch is null && !(bool)ModBase.Setup.Get("HintBuy") && Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("LoginType"), McLoginType.Ms, false))) // 保存脚本时不提示
            {
                if (ModBase.IsSystemLanguageChinese())
                {
                    ModBase.RunInNewThread(() => { switch (ModBase.Setup.Get("SystemLaunchCount")) { case var @case when Operators.ConditionalCompareObjectEqual(@case, 3, false): case var case1 when Operators.ConditionalCompareObjectEqual(case1, 8, false): case var case2 when Operators.ConditionalCompareObjectEqual(case2, 15, false): case var case3 when Operators.ConditionalCompareObjectEqual(case3, 30, false): case var case4 when Operators.ConditionalCompareObjectEqual(case4, 50, false): case var case5 when Operators.ConditionalCompareObjectEqual(case5, 70, false): case var case6 when Operators.ConditionalCompareObjectEqual(case6, 90, false): case var case7 when Operators.ConditionalCompareObjectEqual(case7, 110, false): case var case8 when Operators.ConditionalCompareObjectEqual(case8, 130, false): case var case9 when Operators.ConditionalCompareObjectEqual(case9, 180, false): case var case10 when Operators.ConditionalCompareObjectEqual(case10, 220, false): case var case11 when Operators.ConditionalCompareObjectEqual(case11, 280, false): case var case12 when Operators.ConditionalCompareObjectEqual(case12, 330, false): case var case13 when Operators.ConditionalCompareObjectEqual(case13, 380, false): case var case14 when Operators.ConditionalCompareObjectEqual(case14, 450, false): case var case15 when Operators.ConditionalCompareObjectEqual(case15, 550, false): case var case16 when Operators.ConditionalCompareObjectEqual(case16, 660, false): case var case17 when Operators.ConditionalCompareObjectEqual(case17, 750, false): case var case18 when Operators.ConditionalCompareObjectEqual(case18, 880, false): case var case19 when Operators.ConditionalCompareObjectEqual(case19, 950, false): case var case20 when Operators.ConditionalCompareObjectEqual(case20, 1100, false): case var case21 when Operators.ConditionalCompareObjectEqual(case21, 1300, false): case var case22 when Operators.ConditionalCompareObjectEqual(case22, 1500, false): case var case23 when Operators.ConditionalCompareObjectEqual(case23, 1700, false): case var case24 when Operators.ConditionalCompareObjectEqual(case24, 1900, false): { if (ModMain.MyMsgBox(Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(Operators.ConcatenateObject(Operators.ConcatenateObject(Operators.ConcatenateObject(Operators.ConcatenateObject(Operators.ConcatenateObject("你已经启动了 ", ModBase.Setup.Get("SystemLaunchCount")), " 次 Minecraft 啦！"), Constants.vbCrLf), "如果觉得 Minecraft 还不错，可以购买正版支持一下，毕竟开发游戏也真的很不容易……不要一直白嫖啦。"), Constants.vbCrLf), Constants.vbCrLf), "在登录一次正版账号后，就不会再出现这个提示了！")), "考虑一下正版？", "支持正版游戏！", "下次一定") == 1) { ModBase.OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj"); } break; } } }, "Buy Minecraft");
                }
                else if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginType"), McLoginType.Legacy, false)))
                {
                    switch (ModMain.MyMsgBox("你必须先登录正版账号，才能进行离线登录！", "正版验证", "购买正版", "试玩", "返回", Button1Action: () => ModBase.OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj")))
                    {
                        case 2:
                            {
                                ModMain.Hint("游戏将以试玩模式启动！", ModMain.HintType.Critical);
                                CurrentLaunchOptions.ExtraArgs.Add("--demo");
                                break;
                            }
                        case 3:
                            {
                                throw new Exception("$$");
                            }
                    }
                }
            }
        }

        #endregion

        #region 主登录模块

        // 登录方式
        public enum McLoginType
        {
            Legacy = 0,
            Nide = 2,
            Auth = 3,
            Ms = 5
        }

        // 各个登录方式的对应数据
        public abstract class McLoginData
        {
            /// <summary>
        /// 登录方式。
        /// </summary>
            public McLoginType Type;
            public override bool Equals(object obj)
            {
                return obj is not null && obj.GetHashCode() == GetHashCode();
            }
        }
        public class McLoginServer : McLoginData
        {

            /// <summary>
        /// 登录用户名。
        /// </summary>
            public string UserName;
            /// <summary>
        /// 登录密码。
        /// </summary>
            public string Password;
            /// <summary>
        /// 登录服务器基础地址。
        /// </summary>
            public string BaseUrl;
            /// <summary>
        /// 登录所使用的标识符，目前只可能为 “Auth” 或 “Nide”，用于存储缓存等。
        /// </summary>
            public string Token;
            /// <summary>
        /// 登录方式的描述字符串，如 “正版”、“统一通行证”。
        /// </summary>
            public string Description;
            /// <summary>
        /// 是否在本次登录中强制要求玩家重新选择角色，目前仅对 Authlib-Injector 生效。
        /// </summary>
            public bool ForceReselectProfile = false;

            public McLoginServer(McLoginType Type)
            {
                this.Type = Type;
            }
            public override int GetHashCode()
            {
                return (int)Math.Round(ModBase.GetHash(UserName + Password + BaseUrl + Token + ((int)Type).ToString()) % (decimal)int.MaxValue);
            }

        }
        public class McLoginMs : McLoginData
        {

            /// <summary>
        /// 缓存的 OAuth Refresh Token。若没有则为空字符串。
        /// </summary>
            public string OAuthRefreshToken = "";
            public string AccessToken = "";
            public string Uuid = "";
            public string UserName = "";
            public string ProfileJson = "";

            public McLoginMs()
            {
                Type = McLoginType.Ms;
            }
            public override int GetHashCode()
            {
                return (int)Math.Round(ModBase.GetHash(OAuthRefreshToken + AccessToken + Uuid + UserName + ProfileJson) % (decimal)int.MaxValue);
            }
        }
        public class McLoginLegacy : McLoginData
        {
            /// <summary>
        /// 登录用户名。
        /// </summary>
            public string UserName;
            /// <summary>
        /// 皮肤种类。
        /// </summary>
            public int SkinType;
            /// <summary>
        /// 若采用正版皮肤，则为该皮肤名。
        /// </summary>
            public string SkinName;

            public McLoginLegacy()
            {
                Type = McLoginType.Legacy;
            }
            public override int GetHashCode()
            {
                return (int)Math.Round(ModBase.GetHash(UserName + SkinType + SkinName + ((int)Type).ToString()) % (decimal)int.MaxValue);
            }
        }

        // 登录返回结果
        public struct McLoginResult
        {
            public string Name;
            public string Uuid;
            public string AccessToken;
            public string Type;
            public string ClientToken;
            /// <summary>
        /// 进行微软登录时返回的 profile 信息。
        /// </summary>
            public string ProfileJson;
        }

        /// <summary>
    /// 根据登录信息获取玩家的 MC 用户名。如果无法获取则返回 Nothing。
    /// </summary>
        public static string McLoginName()
        {
            // 根据当前登录方式优先返回
            switch (ModBase.Setup.Get("LoginType"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, McLoginType.Ms, false):
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("CacheMsV2Name"), "", false)))
                            return Conversions.ToString(ModBase.Setup.Get("CacheMsV2Name"));
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, McLoginType.Legacy, false):
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("LoginLegacyName"), "", false)))
                            return ModBase.Setup.Get("LoginLegacyName").ToString().BeforeFirst("¨");
                        break;
                    }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, McLoginType.Nide, false):
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("CacheNideName"), "", false)))
                            return Conversions.ToString(ModBase.Setup.Get("CacheNideName"));
                        break;
                    }
                case var case3 when Operators.ConditionalCompareObjectEqual(case3, McLoginType.Auth, false):
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("CacheAuthName"), "", false)))
                            return Conversions.ToString(ModBase.Setup.Get("CacheAuthName"));
                        break;
                    }
            }
            // 查找所有可能的项
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("CacheMsV2Name"), "", false)))
                return Conversions.ToString(ModBase.Setup.Get("CacheMsV2Name"));
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("CacheNideName"), "", false)))
                return Conversions.ToString(ModBase.Setup.Get("CacheNideName"));
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("CacheAuthName"), "", false)))
                return Conversions.ToString(ModBase.Setup.Get("CacheAuthName"));
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("LoginLegacyName"), "", false)))
                return ModBase.Setup.Get("LoginLegacyName").ToString().BeforeFirst("¨");
            return null;
        }
        /// <summary>
    /// 当前是否可以进行登录。若不可以则会返回错误原因。
    /// </summary>
        public static string McLoginAble()
        {
            switch (ModBase.Setup.Get("LoginType"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, McLoginType.Ms, false):
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheMsV2OAuthRefresh"), "", false)))
                        {
                            return ModMain.FrmLoginMs.IsVaild();
                        }
                        else
                        {
                            return "";
                        }
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, McLoginType.Legacy, false):
                    {
                        return ModMain.FrmLoginLegacy.IsVaild();
                    }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, McLoginType.Nide, false):
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheNideAccess"), "", false)))
                        {
                            return ModMain.FrmLoginNide.IsVaild();
                        }
                        else
                        {
                            return "";
                        }
                    }
                case var case3 when Operators.ConditionalCompareObjectEqual(case3, McLoginType.Auth, false):
                    {
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheAuthAccess"), "", false)))
                        {
                            return ModMain.FrmLoginAuth.IsVaild();
                        }
                        else
                        {
                            return "";
                        }
                    }

                default:
                    {
                        return "未知的登录方式";
                    }
            }
        }
        /// <summary>
    /// 登录输入是否可以进行登录。若不可以则会返回错误原因。
    /// </summary>
        public static string McLoginAble(McLoginData LoginData)
        {
            switch (LoginData.Type)
            {
                case McLoginType.Ms:
                    {
                        return PageLoginMs.IsVaild((McLoginMs)LoginData);
                    }
                case McLoginType.Legacy:
                    {
                        return PageLoginLegacy.IsVaild((McLoginLegacy)LoginData);
                    }
                case McLoginType.Nide:
                    {
                        return PageLoginNide.IsVaild((McLoginServer)LoginData);
                    }
                case McLoginType.Auth:
                    {
                        return PageLoginAuth.IsVaild((McLoginServer)LoginData);
                    }

                default:
                    {
                        return "未知的登录方式";
                    }
            }
        }

        // 登录主模块加载器
        public static ModLoader.LoaderTask<McLoginData, McLoginResult> McLoginLoader = new ModLoader.LoaderTask<McLoginData, McLoginResult>("登录", McLoginStart, McLoginInput, ThreadPriority.BelowNormal) { ReloadTimeout = 1, ProgressWeight = 15d, Block = false };
        public static McLoginData McLoginInput()
        {
            McLoginData LoginData = null;
            McLoginType LoginType = (McLoginType)Conversions.ToInteger(ModBase.Setup.Get("LoginType"));
            try
            {
                switch (LoginType)
                {
                    case McLoginType.Legacy:
                        {
                            LoginData = PageLoginLegacy.GetLoginData();
                            break;
                        }
                    case McLoginType.Ms:
                        {
                            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheMsV2OAuthRefresh"), "", false)))
                            {
                                LoginData = PageLoginMs.GetLoginData();
                            }
                            else
                            {
                                LoginData = PageLoginMsSkin.GetLoginData();
                            }

                            break;
                        }
                    case McLoginType.Nide:
                        {
                            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheNideAccess"), "", false)))
                            {
                                LoginData = PageLoginNide.GetLoginData();
                            }
                            else
                            {
                                LoginData = PageLoginNideSkin.GetLoginData();
                            }

                            break;
                        }
                    case McLoginType.Auth:
                        {
                            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("CacheAuthAccess"), "", false)))
                            {
                                LoginData = PageLoginAuth.GetLoginData();
                            }
                            else
                            {
                                LoginData = PageLoginAuthSkin.GetLoginData();
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取登录输入信息失败（" + ModBase.GetStringFromEnum(LoginType) + "）", ModBase.LogLevel.Feedback);
            }
            return LoginData;
        }
        private static void McLoginStart(ModLoader.LoaderTask<McLoginData, McLoginResult> Data)
        {
            McLaunchLog("登录加载已开始");
            // 校验登录信息
            string CheckResult = McLoginAble(Data.Input);
            if (!string.IsNullOrEmpty(CheckResult))
                throw new ArgumentException(CheckResult);
            // 获取对应加载器
            ModLoader.LoaderBase Loader = null;
            switch (Data.Input.Type)
            {
                case McLoginType.Ms:
                    {
                        Loader = McLoginMsLoader;
                        break;
                    }
                case McLoginType.Legacy:
                    {
                        Loader = McLoginLegacyLoader;
                        break;
                    }
                case McLoginType.Nide:
                    {
                        Loader = McLoginNideLoader;
                        break;
                    }
                case McLoginType.Auth:
                    {
                        Loader = McLoginAuthLoader;
                        break;
                    }
            }
            // 尝试加载
            Loader.WaitForExit(Data.Input, McLoginLoader, Data.IsForceRestarting);
            Data.Output = (McLoginResult)((object)Loader).Output;
            ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true, false)); // 刷新自动填充列表
            McLaunchLog("登录加载已结束");
        }

        #endregion

        #region 分方式登录模块

        // 各个登录方式的主对象与输入构造
        public static ModLoader.LoaderTask<McLoginMs, McLoginResult> McLoginMsLoader = new ModLoader.LoaderTask<McLoginMs, McLoginResult>("Loader Login Ms", McLoginMsStart) { ReloadTimeout = 1 };
        public static ModLoader.LoaderTask<McLoginLegacy, McLoginResult> McLoginLegacyLoader = new ModLoader.LoaderTask<McLoginLegacy, McLoginResult>("Loader Login Legacy", McLoginLegacyStart);
        public static ModLoader.LoaderTask<McLoginServer, McLoginResult> McLoginNideLoader = new ModLoader.LoaderTask<McLoginServer, McLoginResult>("Loader Login Nide", McLoginServerStart) { ReloadTimeout = 1000 * 60 * 10 };
        public static ModLoader.LoaderTask<McLoginServer, McLoginResult> McLoginAuthLoader = new ModLoader.LoaderTask<McLoginServer, McLoginResult>("Loader Login Auth", McLoginServerStart) { ReloadTimeout = 1000 * 60 * 10 };

        // 主加载函数，返回所有需要的登录信息
        private static long McLoginMsRefreshTime = 0L; // 上次刷新登录的时间
        private static void McLoginMsStart(ModLoader.LoaderTask<McLoginMs, McLoginResult> Data)
        {
            var Input = Data.Input;
            string LogUsername = Input.UserName;
            McLaunchLog("登录方式：正版（" + (string.IsNullOrEmpty(LogUsername) ? "尚未登录" : LogUsername) + "）");
            Data.Progress = 0.05d;
            // 检查是否已经登录完成
            if (!Data.IsForceRestarting && !string.IsNullOrEmpty(Input.AccessToken) && McLoginMsRefreshTime > 0L && ModBase.GetTimeTick() - McLoginMsRefreshTime < 1000 * 60 * 10) // 不要求强行重启
                                                                                                                                                                                   // 已经登录过了
                                                                                                                                                                                   // 完成时间在 10 分钟内
            {
                Data.Output = new McLoginResult() { AccessToken = Input.AccessToken, Name = Input.UserName, Uuid = Input.Uuid, Type = "Microsoft", ClientToken = Input.Uuid, ProfileJson = Input.ProfileJson };
                goto SkipLogin;
            }
            // 尝试登录
            string[] OAuthTokens;
            if (string.IsNullOrEmpty(Input.OAuthRefreshToken))
            {
            // 无 RefreshToken
            Relogin:
                ;

                OAuthTokens = MsLoginStep1New(Data);
            }
            else
            {
                // 有 RefreshToken
                OAuthTokens = MsLoginStep1Refresh(Input.OAuthRefreshToken);
                if (OAuthTokens[0] == "Relogin")
                    goto Relogin;
            } // 要求重新打开登录网页认证
            if (Data.IsAborted)
                throw new ThreadInterruptedException();
            Data.Progress = 0.25d;
            if (Data.IsAborted)
                throw new ThreadInterruptedException();
            string OAuthAccessToken = OAuthTokens[0];
            string OAuthRefreshToken = OAuthTokens[1];
            string XBLToken = MsLoginStep2(OAuthAccessToken);
            Data.Progress = 0.4d;
            if (Data.IsAborted)
                throw new ThreadInterruptedException();
            string[] Tokens = MsLoginStep3(XBLToken);
            Data.Progress = 0.55d;
            if (Data.IsAborted)
                throw new ThreadInterruptedException();
            string AccessToken = MsLoginStep4(Tokens);
            Data.Progress = 0.7d;
            if (Data.IsAborted)
                throw new ThreadInterruptedException();
            MsLoginStep5(AccessToken);
            Data.Progress = 0.85d;
            if (Data.IsAborted)
                throw new ThreadInterruptedException();
            string[] Result = MsLoginStep6(AccessToken);
            Data.Progress = 0.98d;
            // 输出登录结果
            ModBase.Setup.Set("CacheMsV2OAuthRefresh", OAuthRefreshToken);
            ModBase.Setup.Set("CacheMsV2Access", AccessToken);
            ModBase.Setup.Set("CacheMsV2Uuid", Result[0]);
            ModBase.Setup.Set("CacheMsV2Name", Result[1]);
            ModBase.Setup.Set("CacheMsV2ProfileJson", Result[2]);
            JObject MsJson = (JObject)ModBase.GetJson(Conversions.ToString(ModBase.Setup.Get("LoginMsJson")));
            MsJson.Remove(Input.UserName); // 如果更改了玩家名……
            MsJson[Result[1]] = OAuthRefreshToken;
            ModBase.Setup.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None));
            Data.Output = new McLoginResult() { AccessToken = AccessToken, Name = Result[1], Uuid = Result[0], Type = "Microsoft", ClientToken = Result[0], ProfileJson = Result[2] };
            // 结束
            McLoginMsRefreshTime = ModBase.GetTimeTick();
            McLaunchLog("微软登录完成");
        SkipLogin:
            ;

            ModBase.Setup.Set("HintBuy", true); // 关闭正版购买提示
            if (ModSecret.ThemeUnlock(10, false))
                ModMain.MyMsgBox("感谢你对正版游戏的支持！" + Constants.vbCrLf + "隐藏主题 跳票红 已解锁！", "提示");
        }
        private static void McLoginServerStart(ModLoader.LoaderTask<McLoginServer, McLoginResult> Data)
        {
            var Input = Data.Input;
            bool NeedRefresh = false;
            bool WasRefreshed = false;
            string LogUsername = Input.UserName;
            if (LogUsername.Contains("@") && (bool)ModBase.Setup.Get("UiLauncherEmail"))
            {
                LogUsername = ModMinecraft.AccountFilter(LogUsername);
            }
            McLaunchLog("登录方式：" + Input.Description + "（" + LogUsername + "）");
            Data.Progress = 0.05d;
            // 尝试登录
            if (!Data.Input.ForceReselectProfile && Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("Cache" + Input.Token + "Username"), Data.Input.UserName, false)) && Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("Cache" + Input.Token + "Pass"), Data.Input.Password, false)) && Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("Cache" + Input.Token + "Access"), "", false)) && Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("Cache" + Input.Token + "Client"), "", false)) && Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("Cache" + Input.Token + "Uuid"), "", false)) && Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("Cache" + Input.Token + "Name"), "", false)))
            {
                // 尝试验证登录
                try
                {
                    if (Data.IsAborted)
                        throw new ThreadInterruptedException();
                    McLoginRequestValidate(ref Data);
                    goto LoginFinish;
                }
                catch (Exception ex)
                {
                    string AllMessage = ModBase.GetExceptionDetail(ex);
                    McLaunchLog("验证登录失败：" + AllMessage);
                    if ((AllMessage.Contains("超时") || AllMessage.Contains("imeout")) && !AllMessage.Contains("403"))
                    {
                        McLaunchLog("已触发超时登录失败");
                        throw new Exception("$登录失败：连接登录服务器超时。" + Constants.vbCrLf + "请检查你的网络状况是否良好，或尝试使用 VPN！");
                    }
                }
                Data.Progress = 0.25d;
            // 尝试刷新登录
            Refresh:
                ;

                try
                {
                    if (Data.IsAborted)
                        throw new ThreadInterruptedException();
                    McLoginRequestRefresh(ref Data, NeedRefresh);
                    goto LoginFinish;
                }
                catch (Exception ex)
                {
                    McLaunchLog("刷新登录失败：" + ModBase.GetExceptionDetail(ex));
                    if (WasRefreshed)
                        throw new Exception("二轮刷新登录失败", ex);
                }
                Data.Progress = NeedRefresh ? 0.85d : 0.45d;
            }
            // 尝试普通登录
            try
            {
                if (Data.IsAborted)
                    throw new ThreadInterruptedException();
                NeedRefresh = McLoginRequestLogin(ref Data);
            }
            catch (Exception ex)
            {
                McLaunchLog("登录失败：" + ModBase.GetExceptionDetail(ex));
                throw;
            }
            if (NeedRefresh)
            {
                McLaunchLog("重新进行刷新登录");
                WasRefreshed = true;
                Data.Progress = 0.65d;
                goto Refresh;
            }

        LoginFinish:
            ;

            Data.Progress = 0.95d;
            // 保存启动记录
            var Dict = new Dictionary<string, string>();
            var Emails = new List<string>();
            var Passwords = new List<string>();
            try
            {
                if (Conversions.ToBoolean(!Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("Login" + Input.Token + "Email"), "", false)))
                    Emails.AddRange(ModBase.Setup.Get("Login" + Input.Token + "Email").ToString().Split("¨"));
                if (Conversions.ToBoolean(!Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("Login" + Input.Token + "Pass"), "", false)))
                    Passwords.AddRange(ModBase.Setup.Get("Login" + Input.Token + "Pass").ToString().Split("¨"));
                for (int i = 0, loopTo = Emails.Count - 1; i <= loopTo; i++)
                    Dict.Add(Emails[i], Passwords[i]);
                Dict.Remove(Input.UserName);
                Emails = new List<string>(Dict.Keys);
                Emails.Insert(0, Input.UserName);
                Passwords = new List<string>(Dict.Values);
                Passwords.Insert(0, Input.Password);
                ModBase.Setup.Set("Login" + Input.Token + "Email", Emails.Join("¨"));
                ModBase.Setup.Set("Login" + Input.Token + "Pass", Passwords.Join("¨"));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "保存启动记录失败", ModBase.LogLevel.Hint);
                ModBase.Setup.Set("Login" + Input.Token + "Email", "");
                ModBase.Setup.Set("Login" + Input.Token + "Pass", "");
            }
        }
        private static void McLoginLegacyStart(ModLoader.LoaderTask<McLoginLegacy, McLoginResult> Data)
        {
            var Input = Data.Input;
            McLaunchLog("登录方式：离线（" + Input.UserName + "）");
            Data.Progress = 0.1d;
            {
                ref var withBlock = ref Data.Output;
                withBlock.Name = Input.UserName;
                withBlock.Uuid = McLoginLegacyUuidWithCustomSkin(Input.UserName, Input.SkinType, Input.SkinName);
                withBlock.Type = "Legacy";
            }
            // 将结果扩展到所有项目中
            Data.Output.AccessToken = Data.Output.Uuid;
            Data.Output.ClientToken = Data.Output.Uuid;
            // 保存启动记录
            var Names = new List<string>();
            if (Conversions.ToBoolean(!Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginLegacyName"), "", false)))
                Names.AddRange(ModBase.Setup.Get("LoginLegacyName").ToString().Split("¨"));
            Names.Remove(Input.UserName);
            Names.Insert(0, Input.UserName);
            ModBase.Setup.Set("LoginLegacyName", Names.ToArray().Join("¨"));
        }

        // Server 登录：三种验证方式的请求
        private static void McLoginRequestValidate(ref ModLoader.LoaderTask<McLoginServer, McLoginResult> Data)
        {
            McLaunchLog("验证登录开始（Validate, " + Data.Input.Token + "）");
            // 提前缓存信息，否则如果在登录请求过程中退出登录，设置项目会被清空，导致输出存在空值
            string AccessToken = Conversions.ToString(ModBase.Setup.Get("Cache" + Data.Input.Token + "Access"));
            string ClientToken = Conversions.ToString(ModBase.Setup.Get("Cache" + Data.Input.Token + "Client"));
            string Uuid = Conversions.ToString(ModBase.Setup.Get("Cache" + Data.Input.Token + "Uuid"));
            string Name = Conversions.ToString(ModBase.Setup.Get("Cache" + Data.Input.Token + "Name"));
            // 发送登录请求
            var RequestData = new JObject(new JProperty("accessToken", AccessToken), new JProperty("clientToken", ClientToken), new JProperty("requestUser", true));
            ModNet.NetRequestRetry(Url: Data.Input.BaseUrl + "/validate", Method: "POST", Data: RequestData.ToString(0), Headers: new Dictionary<string, string>() { { "Accept-Language", "zh-CN" } }, ContentType: "application/json; charset=utf-8"); // 没有返回值的
                                                                                                                                                                                                                                                        // 将登录结果输出
            Data.Output.AccessToken = AccessToken;
            Data.Output.ClientToken = ClientToken;
            Data.Output.Uuid = Uuid;
            Data.Output.Name = Name;
            Data.Output.Type = Data.Input.Token;
            // 不更改缓存，直接结束
            McLaunchLog("验证登录成功（Validate, " + Data.Input.Token + "）");
        }
        private static void McLoginRequestRefresh(ref ModLoader.LoaderTask<McLoginServer, McLoginResult> Data, bool RequestUser)
        {
            var RefreshInfo = new JObject();
            var SelectProfile = new JObject();
            SelectProfile.Add(new JProperty("name", ModBase.Setup.Get("Cache" + Data.Input.Token + "Name")));
            SelectProfile.Add(new JProperty("id", ModBase.Setup.Get("Cache" + Data.Input.Token + "Uuid")));
            RefreshInfo.Add("selectedProfile", SelectProfile);
            RefreshInfo.Add(new JProperty("accessToken", ModBase.Setup.Get("Cache" + Data.Input.Token + "Access")));
            RefreshInfo.Add(new JProperty("requestUser", true));


            McLaunchLog("刷新登录开始（Refresh, " + Data.Input.Token + "）");
            JObject LoginJson = (JObject)ModBase.GetJson(ModNet.NetRequestRetry(Url: Data.Input.BaseUrl + "/refresh", Method: "POST", Data: RefreshInfo.ToString(0), Headers: new Dictionary<string, string>() { { "Accept-Language", "zh-CN" } }, ContentType: "application/json; charset=utf-8"));
            // 将登录结果输出
            if (LoginJson["selectedProfile"] is null)
                throw new Exception(Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject("选择的角色 ", ModBase.Setup.Get("Cache" + Data.Input.Token + "Name")), " 无效！")));
            Data.Output.AccessToken = LoginJson["accessToken"].ToString();
            Data.Output.ClientToken = LoginJson["clientToken"].ToString();
            Data.Output.Uuid = LoginJson["selectedProfile"]["id"].ToString();
            Data.Output.Name = LoginJson["selectedProfile"]["name"].ToString();
            Data.Output.Type = Data.Input.Token;
            // 保存缓存
            ModBase.Setup.Set("Cache" + Data.Input.Token + "Access", Data.Output.AccessToken);
            ModBase.Setup.Set("Cache" + Data.Input.Token + "Client", Data.Output.ClientToken);
            ModBase.Setup.Set("Cache" + Data.Input.Token + "Uuid", Data.Output.Uuid);
            ModBase.Setup.Set("Cache" + Data.Input.Token + "Name", Data.Output.Name);
            ModBase.Setup.Set("Cache" + Data.Input.Token + "Username", Data.Input.UserName);
            ModBase.Setup.Set("Cache" + Data.Input.Token + "Pass", Data.Input.Password);
            McLaunchLog("刷新登录成功（Refresh, " + Data.Input.Token + "）");
        }
        private static bool McLoginRequestLogin(ref ModLoader.LoaderTask<McLoginServer, McLoginResult> Data)
        {
            try
            {
                bool NeedRefresh = false;
                McLaunchLog("登录开始（Login, " + Data.Input.Token + "）");
                var RequestData = new JObject(new JProperty("agent", new JObject(new JProperty("name", "Minecraft"), new JProperty("version", 1))), new JProperty("username", Data.Input.UserName), new JProperty("password", Data.Input.Password), new JProperty("requestUser", true));
                JObject LoginJson = (JObject)ModBase.GetJson(ModNet.NetRequestRetry(Url: Data.Input.BaseUrl + "/authenticate", Method: "POST", Data: RequestData.ToString(0), Headers: new Dictionary<string, string>() { { "Accept-Language", "zh-CN" } }, ContentType: "application/json; charset=utf-8"));
                // 检查登录结果
                if (LoginJson["availableProfiles"].Count() == 0)
                {
                    if (Data.Input.ForceReselectProfile)
                        ModMain.Hint("你还没有创建角色，无法更换！", ModMain.HintType.Critical);
                    throw new Exception("$你还没有创建角色，请在创建角色后再试！");
                }
                else if (Data.Input.ForceReselectProfile && LoginJson["availableProfiles"].Count() == 1)
                {
                    ModMain.Hint("你的账户中只有一个角色，无法更换！", ModMain.HintType.Critical);
                }
                string SelectedName = null;
                string SelectedId = null;
                if ((LoginJson["selectedProfile"] is null || Data.Input.ForceReselectProfile) && LoginJson["availableProfiles"].Count() > 1)
                {
                    // 要求选择档案；优先从缓存读取
                    NeedRefresh = true;
                    string CacheId = Conversions.ToString(ModBase.Setup.Get("Cache" + Data.Input.Token + "Uuid"));
                    foreach (var Profile in LoginJson["availableProfiles"])
                    {
                        if ((Profile["id"].ToString() ?? "") == (CacheId ?? ""))
                        {
                            SelectedName = Profile["name"].ToString();
                            SelectedId = Profile["id"].ToString();
                            McLaunchLog("根据缓存选择的角色：" + SelectedName);
                        }
                    }
                    // 缓存无效，要求玩家选择
                    if (SelectedName is null)
                    {
                        McLaunchLog("要求玩家选择角色");
                        ModBase.RunInUiWait(() =>
        {
            var SelectionControl = new List<IMyRadio>();
            var SelectionJson = new List<JToken>();
            foreach (var Profile in LoginJson["availableProfiles"])
            {
                SelectionControl.Add(new MyRadioBox() { Text = Profile["name"].ToString() });
                SelectionJson.Add(Profile);
            }
            int SelectedIndex = (int)ModMain.MyMsgBoxSelect(SelectionControl, "选择使用的角色");
            SelectedName = SelectionJson[SelectedIndex]["name"].ToString();
            SelectedId = SelectionJson[SelectedIndex]["id"].ToString();
        });
                        McLaunchLog("玩家选择的角色：" + SelectedName);
                    }
                }
                else
                {
                    SelectedName = LoginJson["selectedProfile"]["name"].ToString();
                    SelectedId = LoginJson["selectedProfile"]["id"].ToString();
                }
                // 将登录结果输出
                Data.Output.AccessToken = LoginJson["accessToken"].ToString();
                Data.Output.ClientToken = LoginJson["clientToken"].ToString();
                Data.Output.Name = SelectedName;
                Data.Output.Uuid = SelectedId;
                Data.Output.Type = Data.Input.Token;
                // 保存缓存
                ModBase.Setup.Set("Cache" + Data.Input.Token + "Access", Data.Output.AccessToken);
                ModBase.Setup.Set("Cache" + Data.Input.Token + "Client", Data.Output.ClientToken);
                ModBase.Setup.Set("Cache" + Data.Input.Token + "Uuid", Data.Output.Uuid);
                ModBase.Setup.Set("Cache" + Data.Input.Token + "Name", Data.Output.Name);
                ModBase.Setup.Set("Cache" + Data.Input.Token + "Username", Data.Input.UserName);
                ModBase.Setup.Set("Cache" + Data.Input.Token + "Pass", Data.Input.Password);
                McLaunchLog("登录成功（Login, " + Data.Input.Token + "）");
                return NeedRefresh;
            }
            catch (Exception ex)
            {
                string AllMessage = ModBase.GetExceptionSummary(ex);
                ModBase.Log(ex, "登录失败原始错误信息", ModBase.LogLevel.Normal);
                // 读取服务器返回的错误
                if (ex is ModNet.ResponsedWebException)
                {
                    string ErrorMessage = null;
                    try
                    {
                        ErrorMessage = Conversions.ToString(ModBase.GetJson(((ModNet.ResponsedWebException)ex).Response)("errorMessage"));
                    }
                    catch
                    {
                    }
                    if (!string.IsNullOrWhiteSpace(ErrorMessage))
                    {
                        if (ErrorMessage.Contains("密码错误") || ErrorMessage.ContainsF("Incorrect username or password", true))
                        {
                            // 密码错误，退出登录 (#5090)
                            McLaunchLog("密码错误，退出登录");
                            switch (Data.Input.Type)
                            {
                                case McLoginType.Auth:
                                    {
                                        ModBase.RunInUi(PageLoginAuthSkin.ExitLogin);
                                        break;
                                    }
                                case McLoginType.Nide:
                                    {
                                        ModBase.RunInUi(PageLoginNideSkin.ExitLogin);
                                        break;
                                    }
                            }
                        }
                        throw new Exception("$登录失败：" + ErrorMessage);
                    }
                }
                // 通用关键字检测
                if (AllMessage.Contains("403"))
                {
                    switch (Data.Input.Type)
                    {
                        case McLoginType.Auth:
                            {
                                throw new Exception("$登录失败，以下为可能的原因：" + Constants.vbCrLf + " - 输入的账号或密码错误。" + Constants.vbCrLf + " - 登录尝试过于频繁，导致被暂时屏蔽。请不要操作，等待 10 分钟后再试。" + Constants.vbCrLf + " - 只注册了账号，但没有在皮肤站新建角色。");
                            }
                        case McLoginType.Nide:
                            {
                                throw new Exception("$登录失败，以下为可能的原因：" + Constants.vbCrLf + " - 输入的账号或密码错误。" + Constants.vbCrLf + " - 密码错误次数过多，导致被暂时屏蔽。请不要操作，等待 10 分钟后再试。" + Constants.vbCrLf + (Data.Input.UserName.Contains("@") ? "" : " - 登录账号应为邮箱或统一通行证账号，而非游戏角色 ID。" + Constants.vbCrLf) + " - 只注册了账号，但没有加入对应服务器。");
                            }
                    }
                }
                else if (AllMessage.Contains("超时") || AllMessage.Contains("imeout") || AllMessage.Contains("网络请求失败"))
                {
                    throw new Exception("$登录失败：连接登录服务器超时。" + Constants.vbCrLf + "请检查你的网络状况是否良好，或尝试使用 VPN！");
                }
                else if (ex.Message.StartsWithF("$"))
                {
                    throw;
                }
                else
                {
                    throw new Exception("登录失败：" + ex.Message, ex);
                }
                return false;
            }
        }

        // 微软登录步骤 1，原始登录：获取 DeviceCode 并开启登录网页
        private static string[] MsLoginStep1New(ModLoader.LoaderTask<McLoginMs, McLoginResult> Data)
        {
        // 参考：https://learn.microsoft.com/zh-cn/entra/identity-platform/v2-oauth2-device-code

        // 初始请求
        Retry:
            ;

            McLaunchLog("开始微软登录步骤 1/6（原始登录）");
            JObject PrepareJson = (JObject)ModBase.GetJson(ModNet.NetRequestRetry("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", "POST", $"client_id={ModSecret.OAuthClientId}&tenant=/consumers&scope=XboxLive.signin%20offline_access", "application/x-www-form-urlencoded"));
            McLaunchLog("网页登录地址：" + PrepareJson["verification_uri"].ToString());

            // 弹窗
            var Converter = new ModMain.MyMsgBoxConverter() { Content = PrepareJson, ForceWait = true, Type = ModMain.MyMsgBoxType.Login };
            ModMain.WaitingMyMsgBox.Add(Converter);
            while (Converter.Result is null)
                Thread.Sleep(100);
            if (Converter.Result is ModBase.RestartException)
            {
                if (ModMain.MyMsgBox($"请在登录时选择 {ModBase.vbLQ}其他登录方法{ModBase.vbRQ}，然后选择 {ModBase.vbLQ}使用我的密码{ModBase.vbRQ}。{Constants.vbCrLf}如果没有该选项，请选择 {ModBase.vbLQ}设置密码{ModBase.vbRQ}，设置完毕后再登录。", "需要使用密码登录", "重新登录", "设置密码", "取消", Button2Action: () => ModBase.OpenWebsite("https://account.live.com/password/Change")) == 1)
                {
                    goto Retry;
                }
                else
                {
                    throw new Exception("$$");
                }
            }
            else if (Converter.Result is Exception)
            {
                throw (Exception)Converter.Result;
            }
            else
            {
                return (string[])Converter.Result;
            }
        }
        // 微软登录步骤 1，刷新登录：从 OAuth Code 或 OAuth RefreshToken 获取 {OAuth AccessToken, OAuth RefreshToken}
        private static string[] MsLoginStep1Refresh(string Code)
        {
            McLaunchLog("开始微软登录步骤 1/6（刷新登录）");

            string Result;
            try
            {
                Result = Conversions.ToString(ModNet.NetRequestMultiple("https://login.live.com/oauth20_token.srf", "POST", $"client_id={ModSecret.OAuthClientId}&refresh_token={Uri.EscapeDataString(Code)}&grant_type=refresh_token&scope=XboxLive.signin%20offline_access", "application/x-www-form-urlencoded", 2));
            }
            catch (Exception ex)
            {
                if (ex.Message.ContainsF("must sign in again", true) || ex.Message.ContainsF("password expired", true) || ex.Message.Contains("refresh_token") && ex.Message.Contains("is not valid")) // #269
                {
                    return new[] { "Relogin", "" };
                }
                else
                {
                    throw;
                }
            }

            JObject ResultJson = (JObject)ModBase.GetJson(Result);
            string AccessToken = ResultJson["access_token"].ToString();
            string RefreshToken = ResultJson["refresh_token"].ToString();
            return new[] { AccessToken, RefreshToken };
        }
        // 微软登录步骤 2：从 OAuth AccessToken 获取 XBLToken
        private static string MsLoginStep2(string AccessToken)
        {
            McLaunchLog("开始微软登录步骤 2/6");

            string Request = @"{
           ""Properties"": {
               ""AuthMethod"": ""RPS"",
               ""SiteName"": ""user.auth.xboxlive.com"",
               ""RpsTicket"": """ + (AccessToken.StartsWithF("d=") ? "" : "d=") + AccessToken + @"""
           },
           ""RelyingParty"": ""http://auth.xboxlive.com"",
           ""TokenType"": ""JWT""
        }";
            string Result = Conversions.ToString(ModNet.NetRequestMultiple("https://user.auth.xboxlive.com/user/authenticate", "POST", Request, "application/json", 3));

            JObject ResultJson = (JObject)ModBase.GetJson(Result);
            string XBLToken = ResultJson["Token"].ToString();
            return XBLToken;
        }
        // 微软登录步骤 3：从 XBLToken 获取 {XSTSToken, UHS}
        private static string[] MsLoginStep3(string XBLToken)
        {
            McLaunchLog("开始微软登录步骤 3/6");

            string Request = @"{
                                    ""Properties"": {
                                        ""SandboxId"": ""RETAIL"",
                                        ""UserTokens"": [
                                            """ + XBLToken + @"""
                                        ]
                                    },
                                    ""RelyingParty"": ""rp://api.minecraftservices.com/"",
                                    ""TokenType"": ""JWT""
                                 }";
            string Result;
            try
            {
                Result = Conversions.ToString(ModNet.NetRequestMultiple("https://xsts.auth.xboxlive.com/xsts/authorize", "POST", Request, "application/json", 3));
            }
            catch (WebException ex)
            {
                // 参考 https://github.com/PrismarineJS/prismarine-auth/blob/master/src/common/Constants.js
                if (ex.Message.Contains("2148916227"))
                {
                    ModMain.MyMsgBox("该账号似乎已被微软封禁，无法登录。", "登录失败", "我知道了", IsWarn: true);
                    throw new Exception("$$");
                }
                else if (ex.Message.Contains("2148916233"))
                {
                    if (ModMain.MyMsgBox("你尚未注册 Xbox 账户，请在注册后再登录。", "登录提示", "注册", "取消") == 1)
                    {
                        ModBase.OpenWebsite("https://signup.live.com/signup");
                    }
                    throw new Exception("$$");
                }
                else if (ex.Message.Contains("2148916235"))
                {
                    ModMain.MyMsgBox($"你的网络所在的国家或地区无法登录微软账号。{Constants.vbCrLf}请尝试使用加速器或 VPN。", "登录失败", "我知道了");
                    throw new Exception("$$");
                }
                else if (ex.Message.Contains("2148916238"))
                {
                    if (ModMain.MyMsgBox("该账号年龄不足，你需要先修改出生日期，然后才能登录。" + Constants.vbCrLf + "该账号目前填写的年龄是否在 13 岁以上？", "登录提示", "13 岁以上", "12 岁以下", "我不知道") == 1)
                    {
                        ModBase.OpenWebsite("https://account.live.com/editprof.aspx");
                        ModMain.MyMsgBox("请在打开的网页中修改账号的出生日期（至少改为 18 岁以上）。" + Constants.vbCrLf + "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！", "登录提示");
                    }
                    else
                    {
                        ModBase.OpenWebsite("https://support.microsoft.com/zh-cn/account-billing/如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a");
                        ModMain.MyMsgBox("请根据打开的网页的说明，修改账号的出生日期（至少改为 18 岁以上）。" + Constants.vbCrLf + "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！", "登录提示");
                    }
                    throw new Exception("$$");
                }
                else
                {
                    throw;
                }
            }

            JObject ResultJson = (JObject)ModBase.GetJson(Result);
            string XSTSToken = ResultJson["Token"].ToString();
            string UHS = ResultJson["DisplayClaims"]["xui"][0]["uhs"].ToString();
            return new[] { XSTSToken, UHS };
        }
        // 微软登录步骤 4：从 {XSTSToken, UHS} 获取 Minecraft AccessToken
        private static string MsLoginStep4(string[] Tokens)
        {
            McLaunchLog("开始微软登录步骤 4/6");

            string Request = new JObject(new JProperty("identityToken", $"XBL3.0 x={Tokens[1]};{Tokens[0]}")).ToString(0);
            string Result;
            try
            {
                Result = ModNet.NetRequestRetry("https://api.minecraftservices.com/authentication/login_with_xbox", "POST", Request, "application/json");
            }
            catch (WebException ex)
            {
                string Message = ModBase.GetExceptionSummary(ex);
                if (Message.Contains("(429)"))
                {
                    ModBase.Log(ex, "微软登录第 5 步汇报 429");
                    throw new Exception("$登录尝试太过频繁，请等待几分钟后再试！");
                }
                else if (Message.Contains("(403)"))
                {
                    ModBase.Log(ex, "微软登录第 5 步汇报 403");
                    throw new Exception("$当前 IP 的登录尝试异常。" + Constants.vbCrLf + "如果你使用了 VPN 或加速器，请把它们关掉或更换节点后再试！");
                }
                else
                {
                    throw;
                }
            }

            JObject ResultJson = (JObject)ModBase.GetJson(Result);
            string AccessToken = ResultJson["access_token"].ToString();
            return AccessToken;
        }
        // 微软登录步骤 5：验证微软账号是否持有 MC，这也会刷新 XGP
        private static void MsLoginStep5(string AccessToken)
        {
            McLaunchLog("开始微软登录步骤 5/6");

            string Result = Conversions.ToString(ModNet.NetRequestMultiple("https://api.minecraftservices.com/entitlements/mcstore", "GET", "", "application/json", 2, new Dictionary<string, string>() { { "Authorization", "Bearer " + AccessToken } }));
            try
            {
                JObject ResultJson = (JObject)ModBase.GetJson(Result);
                if (!(ResultJson.ContainsKey("items") && ResultJson["items"].Any()))
                {
                    switch (ModMain.MyMsgBox("你尚未购买正版 Minecraft，或者 Xbox Game Pass 已到期。", "登录失败", "购买 Minecraft", "取消"))
                    {
                        case 1:
                            {
                                ModBase.OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
                                break;
                            }
                    }
                    throw new Exception("$$");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "微软登录第 6 步异常：" + Result);
                throw;
            }
        }
        // 微软登录步骤 6：从 Minecraft AccessToken 获取 {UUID, UserName, ProfileJson}
        private static string[] MsLoginStep6(string AccessToken)
        {
            McLaunchLog("开始微软登录步骤 6/6");

            string Result;
            try
            {
                Result = Conversions.ToString(ModNet.NetRequestMultiple("https://api.minecraftservices.com/minecraft/profile", "GET", "", "application/json", 2, new Dictionary<string, string>() { { "Authorization", "Bearer " + AccessToken } }));
            }
            catch (WebException ex)
            {
                string Message = ModBase.GetExceptionSummary(ex);
                if (Message.Contains("(429)"))
                {
                    ModBase.Log(ex, "微软登录第 7 步汇报 429");
                    throw new Exception("$登录尝试太过频繁，请等待几分钟后再试！");
                }
                else if (Message.Contains("(404)"))
                {
                    ModBase.Log(ex, "微软登录第 7 步汇报 404");
                    ModBase.RunInNewThread(() => { switch (ModMain.MyMsgBox("请先创建 Minecraft 玩家档案，然后再重新登录。", "登录失败", "创建档案", "取消")) { case 1: { ModBase.OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile"); break; } } }, "Login Failed: Create Profile");
                    throw new Exception("$$");
                }
                else
                {
                    throw;
                }
            }
            JObject ResultJson = (JObject)ModBase.GetJson(Result);
            string UUID = ResultJson["id"].ToString();
            string UserName = ResultJson["name"].ToString();
            return new[] { UUID, UserName, Result };
        }

        // 返回符合离线皮肤设置的 UUID
        private static string McLoginLegacyUuidWithCustomSkin(string UserName, int SkinType, string SkinName)
        {
            string Uuid = Conversions.ToString(McLoginLegacyUuid(UserName));
            // 根据离线皮肤获取实际使用的 Uuid
            switch (SkinType)
            {
                case 0:
                    {
                        break;
                    }
                // 默认，不需要处理
                case 1:
                    {
                        // Steve
                        while (ModMinecraft.McSkinSex(Uuid) != "Steve")
                        {
                            if (Uuid.EndsWithF("FFFFF"))
                                Uuid = Uuid.Substring(0, 27) + "00000";
                            Uuid = Uuid.Substring(0, 27) + (long.Parse(Uuid.Substring(27), System.Globalization.NumberStyles.AllowHexSpecifier) + 1L).ToString("X").PadLeft(5, '0');
                        }

                        break;
                    }
                case 2:
                    {
                        // Alex
                        while (ModMinecraft.McSkinSex(Uuid) != "Alex")
                        {
                            if (Uuid.EndsWithF("FFFFF"))
                                Uuid = Uuid.Substring(0, 27) + "00000";
                            Uuid = Uuid.Substring(0, 27) + (long.Parse(Uuid.Substring(27), System.Globalization.NumberStyles.AllowHexSpecifier) + 1L).ToString("X").PadLeft(5, '0');
                        }

                        break;
                    }
                case 3:
                    {
                        // 使用正版用户名
                        try
                        {
                            if (!string.IsNullOrEmpty(SkinName) && ModMinecraft.McVersionCurrent is not null && ModMinecraft.McVersionCurrent.Version.McCodeMain < 20) // 1.20+ 或快照版不能使用该项（#3746）
                            {
                                ModBase.Log("[Skin] 由于离线皮肤设置，使用正版 UUID：" + SkinName);
                                Uuid = Conversions.ToString(McLoginMojangUuid(SkinName, false));
                            }
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "离线启动时使用的正版皮肤获取失败");
                            ModMain.MyMsgBox("由于设置的离线启动时使用的正版皮肤获取失败，游戏将以无皮肤的方式启动。" + Constants.vbCrLf + "请检查你的网络是否通畅，或尝试使用 VPN！" + Constants.vbCrLf + Constants.vbCrLf + "详细的错误信息：" + ex.Message, "皮肤获取失败");
                        }

                        break;
                    }
                case 4:
                    {
                        // 自定义
                        while ((ModMinecraft.McSkinSex(Uuid) ?? "") != ((Conversions.ToBoolean(ModBase.Setup.Get("LaunchSkinSlim")) ? "Alex" : "Steve") ?? ""))
                        {
                            if (Uuid.EndsWithF("FFFFF"))
                                Uuid = Uuid.Substring(0, 27) + "00000";
                            Uuid = Uuid.Substring(0, 27) + (long.Parse(Uuid.Substring(27), System.Globalization.NumberStyles.AllowHexSpecifier) + 1L).ToString("X").PadLeft(5, '0');
                        }

                        break;
                    }
            }
            return Uuid;
        }
        // 根据用户名返回对应 UUID，需要多线程
        public static object McLoginMojangUuid(string Name, bool ThrowOnNotFound)
        {
            if (Name.Trim().Length == 0)
                return ModBase.StrFill("", "0", 32);
            // 从缓存获取
            string Uuid = ModBase.ReadIni(ModBase.PathTemp + @"Cache\Uuid\Mojang.ini", Name, "");
            if (Strings.Len(Uuid) == 32)
                return Uuid;
            // 从官网获取
            try
            {
                JObject GotJson = (JObject)ModNet.NetGetCodeByRequestRetry("https://api.mojang.com/users/profiles/minecraft/" + Name, IsJson: true);
                if (GotJson is null)
                    throw new FileNotFoundException("正版玩家档案不存在（" + Name + "）");
                Uuid = (string)(GotJson["id"] ?? "");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从官网获取正版 Uuid 失败（" + Name + "）");
                if (!ThrowOnNotFound && ex.GetType().Name == "FileNotFoundException")
                {
                    Uuid = Conversions.ToString(McLoginLegacyUuid(Name)); // 玩家档案不存在
                }
                else
                {
                    throw new Exception("从官网获取正版 Uuid 失败", ex);
                }
            }
            // 写入缓存
            if (!(Strings.Len(Uuid) == 32))
                throw new Exception("获取的正版 Uuid 长度不足（" + Uuid + "）");
            ModBase.WriteIni(ModBase.PathTemp + @"Cache\Uuid\Mojang.ini", Name, Uuid);
            return Uuid;
        }
        public static object McLoginLegacyUuid(string Name)
        {
            string FullUuid = ModBase.StrFill(Name.Length.ToString("X"), "0", 16) + ModBase.StrFill(ModBase.GetHash(Name).ToString("X"), "0", 16);
            return FullUuid.Substring(0, 12) + "3" + FullUuid.Substring(13, 3) + "9" + FullUuid.Substring(17, 15);
        }

        #endregion

        #region Java 处理

        public static ModJava.JavaEntry McLaunchJavaSelected = null;
        private static void McLaunchJava(ModLoader.LoaderTask<int, int> Task)
        {
            var MinVer = new Version(0, 0, 0, 0);
            var MaxVer = new Version(999, 999, 999, 999);

            // MC 大版本检测
            if (ModMinecraft.McVersionCurrent.ReleaseTime >= new DateTime(2024, 4, 2) && ModMinecraft.McVersionCurrent.Version.McCodeMain == 99 || ModMinecraft.McVersionCurrent.Version.McCodeMain > 20 && ModMinecraft.McVersionCurrent.Version.McCodeMain != 99 || ModMinecraft.McVersionCurrent.Version.McCodeMain == 20 && ModMinecraft.McVersionCurrent.Version.McCodeSub >= 5)
            {
                // 1.20.5+（24w14a+）：至少 Java 21
                MinVer = new Version(1, 21, 0, 0);
            }
            else if (ModMinecraft.McVersionCurrent.ReleaseTime >= new DateTime(2021, 11, 16) && ModMinecraft.McVersionCurrent.Version.McCodeMain == 99 || ModMinecraft.McVersionCurrent.Version.McCodeMain >= 18 && ModMinecraft.McVersionCurrent.Version.McCodeMain != 99)
            {
                // 1.18 pre2+：至少 Java 17
                MinVer = new Version(1, 17, 0, 0);
            }
            else if (ModMinecraft.McVersionCurrent.ReleaseTime >= new DateTime(2021, 5, 11) && ModMinecraft.McVersionCurrent.Version.McCodeMain == 99 || ModMinecraft.McVersionCurrent.Version.McCodeMain >= 17 && ModMinecraft.McVersionCurrent.Version.McCodeMain != 99)
            {
                // 1.17+ (21w19a+)：至少 Java 16
                MinVer = new Version(1, 16, 0, 0);
            }
            else if (ModMinecraft.McVersionCurrent.ReleaseTime.Year >= 2017) // Minecraft 1.12 与 1.11 的分界线正好是 2017 年，太棒了
            {
                // 1.12+：至少 Java 8
                MinVer = new Version(1, 8, 0, 0);
            }
            else if (ModMinecraft.McVersionCurrent.ReleaseTime <= new DateTime(2013, 5, 1) && ModMinecraft.McVersionCurrent.ReleaseTime.Year >= 2001) // 避免某些版本的 1960 癌
            {
                // 1.5.2-：最高 Java 12
                MaxVer = new Version(1, 12, 999, 999);
            }
            if (ModMinecraft.McVersionCurrent.JsonVersion?["java_version"] is not null)
            {
                int RecommendedJava = ModMinecraft.McVersionCurrent.JsonVersion["java_version"].ToObject<int>();
                McLaunchLog("Mojang 推荐使用 Java " + RecommendedJava);
                if (RecommendedJava >= 22)
                    MinVer = new Version(1, RecommendedJava, 0, 0); // 潜在的向后兼容
            }

            // OptiFine 检测
            if (ModMinecraft.McVersionCurrent.Version.HasOptiFine)
            {
                if (ModMinecraft.McVersionCurrent.Version.McCodeMain <= 7 && ModMinecraft.McVersionCurrent.Version.McCodeMain > 0)
                {
                    // <1.7：至多 Java 8
                    MaxVer = new Version(1, 8, 999, 999);
                }
                else if (ModMinecraft.McVersionCurrent.Version.McCodeMain >= 8 && ModMinecraft.McVersionCurrent.Version.McCodeMain <= 11)
                {
                    // 1.8 - 1.11：必须恰好 Java 8
                    MinVer = new Version(1, 8, 0, 0);
                    MaxVer = new Version(1, 8, 999, 999);
                }
                else if (ModMinecraft.McVersionCurrent.Version.McCodeMain == 12)
                {
                    // 1.12：最高 Java 8
                    MaxVer = new Version(1, 8, 999, 999);
                }
            }

            // Forge 检测
            if (ModMinecraft.McVersionCurrent.Version.HasForge)
            {
                if (ModMinecraft.McVersionCurrent.Version.McName == "1.7.2")
                {
                    // 1.7.2：必须 Java 7
                    MinVer = new Version(1, 7, 0, 0) > MinVer ? new Version(1, 7, 0, 0) : MinVer;
                    MaxVer = new Version(1, 7, 999, 999) < MaxVer ? new Version(1, 7, 999, 999) : MaxVer;
                }
                else if (ModMinecraft.McVersionCurrent.Version.McCodeMain <= 12 && ModMinecraft.McVersionCurrent.Version.McCodeMain > 0)
                {
                    // <=1.12：Java 8
                    MaxVer = new Version(1, 8, 999, 999);
                }
                else if (ModMinecraft.McVersionCurrent.Version.McCodeMain <= 14 && ModMinecraft.McVersionCurrent.Version.McCodeMain >= 13)
                {
                    // 1.13 - 1.14：Java 8 - 10
                    MinVer = new Version(1, 8, 0, 0) > MinVer ? new Version(1, 8, 0, 0) : MinVer;
                    MaxVer = new Version(1, 10, 999, 999) < MaxVer ? new Version(1, 10, 999, 999) : MaxVer;
                }
                else if (ModMinecraft.McVersionCurrent.Version.McCodeMain == 15)
                {
                    // 1.15：Java 8 - 15
                    MinVer = new Version(1, 8, 0, 0) > MinVer ? new Version(1, 8, 0, 0) : MinVer;
                    MaxVer = new Version(1, 15, 999, 999) < MaxVer ? new Version(1, 15, 999, 999) : MaxVer;
                }
                else if (ModMinecraft.VersionSortBoolean(ModMinecraft.McVersionCurrent.Version.ForgeVersion, "34.0.0") && ModMinecraft.VersionSortBoolean("36.2.25", ModMinecraft.McVersionCurrent.Version.ForgeVersion))
                {
                    // 1.16，Forge 34.X ~ 36.2.25：最高 Java 8u320
                    MaxVer = new Version(1, 8, 0, 320) < MaxVer ? new Version(1, 8, 0, 320) : MaxVer;
                }
                else if (ModMinecraft.McVersionCurrent.Version.McCodeMain >= 18 && ModMinecraft.McVersionCurrent.Version.McCodeMain < 19 && ModMinecraft.McVersionCurrent.Version.HasOptiFine) // #305
                {
                    // 1.18：若安装了 OptiFine，最高 Java 18
                    MaxVer = new Version(1, 18, 999, 999) < MaxVer ? new Version(1, 18, 999, 999) : MaxVer;
                }
            }

            // Cleanroom 检测
            if (ModMinecraft.McVersionCurrent.Version.HasCleanroom)
            {
                // 需要至少 Java 21
                MinVer = new Version(1, 21, 0, 0) > MinVer ? new Version(1, 21, 0, 0) : MinVer;
            }

            // Fabric 检测
            if (ModMinecraft.McVersionCurrent.Version.HasFabric)
            {
                if (ModMinecraft.McVersionCurrent.Version.McCodeMain >= 15 && ModMinecraft.McVersionCurrent.Version.McCodeMain <= 16 && ModMinecraft.McVersionCurrent.Version.McCodeMain != -1)
                {
                    // 1.15 - 1.16：Java 8+
                    MinVer = new Version(1, 8, 0, 0) > MinVer ? new Version(1, 8, 0, 0) : MinVer;
                }
                else if (ModMinecraft.McVersionCurrent.Version.McCodeMain >= 18 && ModMinecraft.McVersionCurrent.Version.McCodeMain < 99)
                {
                    // 1.18+：Java 17+
                    MinVer = new Version(1, 17, 0, 0) > MinVer ? new Version(1, 17, 0, 0) : MinVer;
                }
            }

            // 统一通行证检测
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginType"), McLoginType.Nide, false)))
            {
                // 至少 Java 8u101
                MinVer = new Version(1, 8, 0, 141) > MinVer ? new Version(1, 8, 0, 141) : MinVer;
            }

            lock (ModJava.JavaLock)
            {

                // 选择 Java
                McLaunchLog("Java 版本需求：最低 " + MinVer.ToString() + "，最高 " + MaxVer.ToString());
                McLaunchJavaSelected = ModJava.JavaSelect("$$", MinVer, MaxVer, ModMinecraft.McVersionCurrent);
                if (Task.IsAborted)
                    return;
                if (McLaunchJavaSelected is not null)
                {
                    McLaunchLog("选择的 Java：" + McLaunchJavaSelected.ToString());
                    return;
                }

                // 无合适的 Java
                if (Task.IsAborted)
                    return; // 中断加载会导致 JavaSelect 异常地返回空值，误判找不到 Java
                McLaunchLog("无合适的 Java，需要确认是否自动下载");
                string JavaCode;
                if (MinVer >= new Version(1, 22, 0, 0)) // 潜在的向后兼容
                {
                    JavaCode = MinVer.Minor.ToString();
                    if (!ModJava.JavaDownloadConfirm("Java " + JavaCode))
                        throw new Exception("$$");
                }
                else if (MinVer >= new Version(1, 21, 0, 0))
                {
                    JavaCode = 21.ToString();
                    if (!ModJava.JavaDownloadConfirm("Java 21"))
                        throw new Exception("$$");
                }
                else if (MinVer >= new Version(1, 9, 0, 0))
                {
                    JavaCode = 17.ToString();
                    if (!ModJava.JavaDownloadConfirm("Java 17"))
                        throw new Exception("$$");
                }
                else if (MaxVer < new Version(1, 8, 0, 0))
                {
                    JavaCode = 7.ToString();
                    if (!ModJava.JavaDownloadConfirm("Java 7", true))
                        throw new Exception("$$");
                }
                else if (MinVer > new Version(1, 8, 0, 140) && MaxVer < new Version(1, 8, 0, 321))
                {
                    JavaCode = "8u141";
                    if (!ModJava.JavaDownloadConfirm("Java 8.0.141 ~ 8.0.320", true))
                        throw new Exception("$$");
                }
                else if (MinVer > new Version(1, 8, 0, 140))
                {
                    JavaCode = "8u141";
                    if (!ModJava.JavaDownloadConfirm("Java 8.0.141 或更高版本的 Java 8", true))
                        throw new Exception("$$");
                }
                else if (MaxVer < new Version(1, 8, 0, 321))
                {
                    JavaCode = 8.ToString();
                    if (!ModJava.JavaDownloadConfirm("Java 8.0.320 或更低版本的 Java 8"))
                        throw new Exception("$$");
                }
                else
                {
                    JavaCode = 8.ToString();
                    if (!ModJava.JavaDownloadConfirm("Java 8"))
                        throw new Exception("$$");
                }

                // 开始自动下载
                var JavaLoader = ModJava.JavaFixLoaders(Conversions.ToInteger(JavaCode));
                try
                {
                    JavaLoader.Start(JavaCode, IsForceRestart: true);
                    while (JavaLoader.State == ModBase.LoadState.Loading && !Task.IsAborted)
                    {
                        Task.Progress = JavaLoader.Progress;
                        Thread.Sleep(10);
                    }
                }
                finally
                {
                    JavaLoader.Abort(); // 确保取消时中止 Java 下载
                }

                // 检查下载结果
                if (ModJava.JavaSearchLoader.State != ModBase.LoadState.Loading)
                    ModJava.JavaSearchLoader.State = ModBase.LoadState.Waiting; // 2872#
                McLaunchJavaSelected = ModJava.JavaSelect("$$", MinVer, MaxVer, ModMinecraft.McVersionCurrent);
                if (Task.IsAborted)
                    return;
                if (McLaunchJavaSelected is not null)
                {
                    McLaunchLog("选择的 Java：" + McLaunchJavaSelected.ToString());
                }
                else
                {
                    ModMain.Hint("没有可用的 Java，已取消启动！", ModMain.HintType.Critical);
                    throw new Exception("$$");
                }

            }
        }
        /// <summary>
    /// 指定 Java 使用高性能显卡
    /// </summary>
    /// <param name="JavawPath"></param>
        public static void ModifyJavaGPUPreferences(string JavawPath)
        {
            if (!(ModBase.ReadReg(JavawPath, "GpuPreference=0;", Path: @"Microsoft\DirectX\UserGpuPreferences") == "GpuPreference=2;"))
            {
                ModBase.WriteReg(JavawPath, "GpuPreference=2;", Path: @"Microsoft\DirectX\UserGpuPreferences");
            }
        }

        #endregion

        #region 启动参数

        private static string McLaunchArgument;

        /// <summary>
    /// 释放 Java Wrapper 并返回完整文件路径。
    /// </summary>
        public static string ExtractJavaWrapper()
        {
            string WrapperPath = ModBase.PathPure + "JavaWrapper.jar";
            ModBase.Log("[Java] 选定的 Java Wrapper 路径：" + WrapperPath);
            lock (ExtractJavaWrapperLock) // 避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
            {
                try
                {
                    ModBase.WriteFile(WrapperPath, ModBase.GetResources("JavaWrapper"));
                }
                catch (Exception ex)
                {
                    if (File.Exists(WrapperPath))
                    {
                        // 因为未知原因 Java Wrapper 可能变为只读文件（#4243）
                        ModBase.Log(ex, "Java Wrapper 文件释放失败，但文件已存在，将在删除后尝试重新生成", ModBase.LogLevel.Developer);
                        try
                        {
                            File.Delete(WrapperPath);
                            ModBase.WriteFile(WrapperPath, ModBase.GetResources("JavaWrapper"));
                        }
                        catch (Exception ex2)
                        {
                            ModBase.Log(ex2, "Java Wrapper 文件重新释放失败，将尝试更换文件名重新生成", ModBase.LogLevel.Developer);
                            WrapperPath = ModBase.PathPure + "JavaWrapper2.jar";
                            try
                            {
                                ModBase.WriteFile(WrapperPath, ModBase.GetResources("JavaWrapper"));
                            }
                            catch (Exception ex3)
                            {
                                throw new FileNotFoundException("释放 Java Wrapper 最终尝试失败", ex3);
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException("释放 Java Wrapper 失败", ex);
                    }
                }
            }
            return WrapperPath;
        }
        private static object ExtractJavaWrapperLock = new object();

        // 主方法，合并 Jvm、Game、Replace 三部分的参数数据
        private static void McLaunchArgumentMain(ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>> Loader)
        {
            McLaunchLog("开始获取 Minecraft 启动参数");
            // 获取基准字符串与参数信息
            string Arguments;
            if (ModMinecraft.McVersionCurrent.JsonObject["arguments"] is not null && ModMinecraft.McVersionCurrent.JsonObject["arguments"]["jvm"] is not null)
            {
                McLaunchLog("获取新版 JVM 参数");
                Arguments = McLaunchArgumentsJvmNew(ModMinecraft.McVersionCurrent);
                McLaunchLog("新版 JVM 参数获取成功：");
                McLaunchLog(Arguments);
            }
            else
            {
                McLaunchLog("获取旧版 JVM 参数");
                Arguments = McLaunchArgumentsJvmOld(ModMinecraft.McVersionCurrent);
                McLaunchLog("旧版 JVM 参数获取成功：");
                McLaunchLog(Arguments);
            }
            if (!string.IsNullOrEmpty((string)ModMinecraft.McVersionCurrent.JsonObject["minecraftArguments"])) // 有的版本是空字符串
            {
                McLaunchLog("获取旧版 Game 参数");
                Arguments += " " + McLaunchArgumentsGameOld(ModMinecraft.McVersionCurrent);
                McLaunchLog("旧版 Game 参数获取成功");
            }
            if (ModMinecraft.McVersionCurrent.JsonObject["arguments"] is not null && ModMinecraft.McVersionCurrent.JsonObject["arguments"]["game"] is not null)
            {
                McLaunchLog("获取新版 Game 参数");
                Arguments += " " + McLaunchArgumentsGameNew(ModMinecraft.McVersionCurrent);
                McLaunchLog("新版 Game 参数获取成功");
            }
            // 编码参数（#5818、#5892）
            if (McLaunchJavaSelected.VersionCode > 8)
            {
                if (!Arguments.Contains("-Dfile.encoding="))
                    Arguments += " -Dfile.encoding=UTF-8";
                if (!Arguments.Contains("-Dstdout.encoding="))
                    Arguments += " -Dstdout.encoding=UTF-8";
                if (!Arguments.Contains("-Dstderr.encoding="))
                    Arguments += " -Dstderr.encoding=UTF-8";
            }
            // 替换参数
            var ReplaceArguments = McLaunchArgumentsReplace(ModMinecraft.McVersionCurrent, ref Loader);
            if (string.IsNullOrWhiteSpace(ReplaceArguments["${version_type}"]))
            {
                // 若自定义信息为空，则去掉该部分
                Arguments = Arguments.Replace(" --versionType ${version_type}", "");
                ReplaceArguments["${version_type}"] = "\"\"";
            }
            foreach (KeyValuePair<string, string> entry in ReplaceArguments)
                Arguments = Arguments.Replace(entry.Key, entry.Value.Contains(" ") || entry.Value.Contains(@":\") ? "\"" + entry.Value + "\"" : entry.Value);
            // MJSB
            Arguments = Arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=\"Windows 10\"");
            // 全屏
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchArgumentWindowType"), 0, false)))
                Arguments += " --fullscreen";
            // 由 Option 传入的额外参数
            foreach (var Arg in CurrentLaunchOptions.ExtraArgs)
                Arguments += " " + Arg.Trim();
            // 进存档
            string WorldName = CurrentLaunchOptions.WorldName;
            if (WorldName is not null)
            {
                Arguments += $" --quickPlaySingleplayer \"{WorldName}\"";
            }
            // 进服
            string Server = Conversions.ToString(string.IsNullOrEmpty(CurrentLaunchOptions.ServerIp) ? ModBase.Setup.Get("VersionServerEnter", ModMinecraft.McVersionCurrent) : CurrentLaunchOptions.ServerIp);
            if (WorldName is not null && Server.Length > 0)
            {
                if (ModMinecraft.McVersionCurrent.ReleaseTime > new DateTime(2023, 4, 4))
                {
                    // QuickPlay
                    Arguments += $" --quickPlayMultiplayer \"{Server}\"";
                }
                else
                {
                    // 老版本
                    if (Server.Contains(":"))
                    {
                        // 包含端口号
                        Arguments += " --server " + Server.Split(":")[0] + " --port " + Server.Split(":")[1];
                    }
                    else
                    {
                        // 不包含端口号
                        Arguments += " --server " + Server + " --port 25565";
                    }
                    if (ModMinecraft.McVersionCurrent.Version.HasOptiFine)
                        ModMain.Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", ModMain.HintType.Critical);
                }
            }
            // 自定义
            string ArgumentGame = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceGame", Version: ModMinecraft.McVersionCurrent));
            Arguments = Conversions.ToString(Arguments + Operators.ConcatenateObject(" ", string.IsNullOrEmpty(ArgumentGame) ? ModBase.Setup.Get("LaunchAdvanceGame") : ArgumentGame));
            // 输出
            McLaunchLog("Minecraft 启动参数：");
            McLaunchLog(Arguments);
            McLaunchArgument = Arguments;
        }

        // Jvm 部分（第一段）
        private static string McLaunchArgumentsJvmOld(ModMinecraft.McVersion Version)
        {
            // 存储以空格为间隔的启动参数列表
            var DataList = new List<string>();

            // 输出固定参数
            DataList.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
            string ArgumentJvm = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceJvm", Version: ModMinecraft.McVersionCurrent));
            if (string.IsNullOrEmpty(ArgumentJvm))
                ArgumentJvm = Conversions.ToString(ModBase.Setup.Get("LaunchAdvanceJvm"));
            if (!ArgumentJvm.Contains("-Dlog4j2.formatMsgNoLookups=true"))
                ArgumentJvm += " -Dlog4j2.formatMsgNoLookups=true";
            ArgumentJvm = ArgumentJvm.Replace(" -XX:MaxDirectMemorySize=256M", ""); // #3511 的清理
            DataList.Insert(0, ArgumentJvm); // 可变 JVM 参数
            DataList.Add("-Xmn" + Math.Floor(PageVersionSetup.GetRam(ModMinecraft.McVersionCurrent, !McLaunchJavaSelected.Is64Bit) * 1024d * 0.15d) + "m");
            DataList.Add("-Xmx" + Math.Floor(PageVersionSetup.GetRam(ModMinecraft.McVersionCurrent, !McLaunchJavaSelected.Is64Bit) * 1024d) + "m");
            DataList.Add("\"-Djava.library.path=" + GetNativesFolder() + "\"");
            DataList.Add("-cp ${classpath}"); // 把支持库添加进启动参数表

            // 统一通行证
            if (McLoginLoader.Output.Type == "Nide")
            {
                DataList.Insert(0, Conversions.ToString(Operators.ConcatenateObject("-Dnide8auth.client=true -javaagent:\"" + ModBase.PathAppdata + "nide8auth.jar\"=", ModBase.Setup.Get("VersionServerNide", Version: ModMinecraft.McVersionCurrent))));
            }
            // Authlib-Injector
            if (McLoginLoader.Output.Type == "Auth")
            {
                string Server = Conversions.ToString(McLoginLoader.Input.Type == McLoginType.Legacy ? "http://hiperauth.tech/api/yggdrasil-hiper/" : ModBase.Setup.Get("VersionServerAuthServer", ModMinecraft.McVersionCurrent)); // HiPer 登录
                try
                {
                    string Response = Conversions.ToString(ModNet.NetGetCodeByRequestRetry(Server, Encoding.UTF8));
                    DataList.Insert(0, "-javaagent:\"" + ModBase.PathPure + "authlib-injector.jar\"=" + Server + " -Dauthlibinjector.side=client" + " -Dauthlibinjector.yggdrasil.prefetched=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)));
                }
                catch (Exception ex)
                {
                    throw new Exception("无法连接到第三方登录服务器（" + (Server ?? null) + "）", ex);
                }
            }

            // 添加 Java Wrapper 作为主 Jar
            if (Conversions.ToBoolean(!(bool)ModBase.Setup.Get("LaunchAdvanceDisableJLW") && !(bool)ModBase.Setup.Get("VersionAdvanceDisableJLW", ModMinecraft.McVersionCurrent)))
            {
                if (McLaunchJavaSelected.VersionCode >= 9)
                    DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
                DataList.Add("-Doolloo.jlw.tmpdir=\"" + ModBase.PathPure.TrimEnd('\\') + "\"");
                DataList.Add("-jar \"" + ExtractJavaWrapper() + "\"");
            }

            // 添加 MainClass
            if (Version.JsonObject["mainClass"] is null)
            {
                throw new Exception("版本 json 中没有 mainClass 项！");
            }
            else
            {
                DataList.Add((string)Version.JsonObject["mainClass"]);
            }

            return DataList.Join(" ");
        }
        private static string McLaunchArgumentsJvmNew(ModMinecraft.McVersion Version)
        {
            var DataList = new List<string>();

            // 获取 Json 中的 DataList
            var CurrentVersion = Version;
        NextVersion:
            ;

            if (CurrentVersion.JsonObject["arguments"] is not null && CurrentVersion.JsonObject["arguments"]["jvm"] is not null)
            {
                foreach (JToken SubJson in CurrentVersion.JsonObject["arguments"]["jvm"])
                {
                    if (SubJson.Type == JTokenType.String)
                    {
                        // 字符串类型
                        DataList.Add(SubJson.ToString());
                    }
                    // 非字符串类型
                    else if (ModMinecraft.McJsonRuleCheck(SubJson["rules"]))
                    {
                        // 满足准则
                        if (SubJson["value"].Type == JTokenType.String)
                        {
                            DataList.Add(SubJson["value"].ToString());
                        }
                        else
                        {
                            foreach (JToken value in SubJson["value"])
                                DataList.Add(value.ToString());
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(CurrentVersion.InheritVersion))
            {
                CurrentVersion = new ModMinecraft.McVersion(CurrentVersion.InheritVersion);
                goto NextVersion;
            }

            // 内存、Log4j 防御参数等
            ModSecret.SecretLaunchJvmArgs(ref DataList);

            // 统一通行证
            if (McLoginLoader.Output.Type == "Nide")
            {
                DataList.Insert(0, Conversions.ToString(Operators.ConcatenateObject("-javaagent:\"" + ModBase.PathAppdata + "nide8auth.jar\"=", ModBase.Setup.Get("VersionServerNide", Version: ModMinecraft.McVersionCurrent))));
            }
            // Authlib-Injector
            if (McLoginLoader.Output.Type == "Auth")
            {
                string Server = Conversions.ToString(McLoginLoader.Input.Type == McLoginType.Legacy ? "http://hiperauth.tech/api/yggdrasil-hiper/" : ModBase.Setup.Get("VersionServerAuthServer", Version: ModMinecraft.McVersionCurrent)); // HiPer 登录
                try
                {
                    string Response = Conversions.ToString(ModNet.NetGetCodeByRequestRetry(Server, Encoding.UTF8));
                    DataList.Insert(0, "-javaagent:\"" + ModBase.PathPure + "authlib-injector.jar\"=" + Server + " -Dauthlibinjector.side=client" + " -Dauthlibinjector.yggdrasil.prefetched=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)));
                }
                catch (Exception ex)
                {
                    throw new Exception("无法连接到第三方登录服务器（" + (Server ?? null) + "）", ex);
                }
            }

            // 添加 Java Wrapper 作为主 Jar
            if (Conversions.ToBoolean(!(bool)ModBase.Setup.Get("LaunchAdvanceDisableJLW") && !(bool)ModBase.Setup.Get("VersionAdvanceDisableJLW", ModMinecraft.McVersionCurrent)))
            {
                if (McLaunchJavaSelected.VersionCode >= 9)
                    DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
                DataList.Add("-Doolloo.jlw.tmpdir=\"" + ModBase.PathPure.TrimEnd('\\') + "\"");
                DataList.Add("-jar \"" + ExtractJavaWrapper() + "\"");
            }

            // 将 "-XXX" 与后面 "XXX" 合并到一起
            // 如果不合并，会导致 Forge 1.17 启动无效，它有两个 --add-exports，进一步导致其中一个在后面被去重
            var DeDuplicateDataList = new List<string>();
            for (int i = 0, loopTo = DataList.Count - 1; i <= loopTo; i++)
            {
                string CurrentEntry = DataList[i];
                if (DataList[i].StartsWithF("-"))
                {
                    while (i < DataList.Count - 1)
                    {
                        if (DataList[i + 1].StartsWithF("-"))
                        {
                            break;
                        }
                        else
                        {
                            i += 1;
                            CurrentEntry += " " + DataList[i];
                        }
                    }
                }
                DeDuplicateDataList.Add(CurrentEntry.Trim().Replace("McEmu= ", "McEmu="));
            }

            // #3511 的清理
            DeDuplicateDataList.Remove("-XX:MaxDirectMemorySize=256M");

            // 去重
            string Result = DeDuplicateDataList.Distinct().ToList().Join(" ");

            // 添加 MainClass
            if (Version.JsonObject["mainClass"] is null)
            {
                throw new Exception("版本 json 中没有 mainClass 项！");
            }
            else
            {
                Result += " " + Version.JsonObject["mainClass"].ToString();
            }

            return Result;
        }

        // Game 部分（第二段）
        private static string McLaunchArgumentsGameOld(ModMinecraft.McVersion Version)
        {
            var DataList = new List<string>();

            // 本地化 Minecraft 启动信息
            string BasicString = Version.JsonObject["minecraftArguments"].ToString();
            if (!BasicString.Contains("--height"))
                BasicString += " --height ${resolution_height} --width ${resolution_width}";
            DataList.Add(BasicString);

            string Result = DataList.Join(" ");

            // 特别改变 OptiFineTweaker
            if ((Version.Version.HasForge || Version.Version.HasLiteLoader) && Version.Version.HasOptiFine)
            {
                // 把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
                if (Result.Contains("--tweakClass optifine.OptiFineForgeTweaker"))
                {
                    ModBase.Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" + Result);
                    Result = Result.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "").Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") + " --tweakClass optifine.OptiFineForgeTweaker";
                }
                if (Result.Contains("--tweakClass optifine.OptiFineTweaker"))
                {
                    ModBase.Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" + Result);
                    Result = Result.Replace(" --tweakClass optifine.OptiFineTweaker", "").Replace("--tweakClass optifine.OptiFineTweaker ", "") + " --tweakClass optifine.OptiFineForgeTweaker";
                    try
                    {
                        ModBase.WriteFile(Version.Path + Version.Name + ".json", ModBase.ReadFile(Version.Path + Version.Name + ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"));
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "替换 OptiFineForge TweakClass 失败");
                    }
                }
            }

            return Result;
        }
        private static string McLaunchArgumentsGameNew(ModMinecraft.McVersion Version)
        {
            string McLaunchArgumentsGameNewRet = default;
            var DataList = new List<string>();

            // 获取 Json 中的 DataList
            var CurrentVersion = Version;
        NextVersion:
            ;

            if (CurrentVersion.JsonObject["arguments"] is not null && CurrentVersion.JsonObject["arguments"]["game"] is not null)
            {
                foreach (JToken SubJson in CurrentVersion.JsonObject["arguments"]["game"])
                {
                    if (SubJson.Type == JTokenType.String)
                    {
                        // 字符串类型
                        DataList.Add(SubJson.ToString());
                    }
                    // 非字符串类型
                    else if (ModMinecraft.McJsonRuleCheck(SubJson["rules"]))
                    {
                        // 满足准则
                        if (SubJson["value"].Type == JTokenType.String)
                        {
                            DataList.Add(SubJson["value"].ToString());
                        }
                        else
                        {
                            foreach (JToken value in SubJson["value"])
                                DataList.Add(value.ToString());
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(CurrentVersion.InheritVersion))
            {
                CurrentVersion = new ModMinecraft.McVersion(CurrentVersion.InheritVersion);
                goto NextVersion;
            }

            // 将 "-XXX" 与后面 "XXX" 合并到一起
            // 如果不进行合并 Impact 会启动无效，它有两个 --tweakclass
            var DeDuplicateDataList = new List<string>();
            for (int i = 0, loopTo = DataList.Count - 1; i <= loopTo; i++)
            {
                string CurrentEntry = DataList[i];
                if (DataList[i].StartsWithF("-"))
                {
                    while (i < DataList.Count - 1)
                    {
                        if (DataList[i + 1].StartsWithF("-"))
                        {
                            break;
                        }
                        else
                        {
                            i += 1;
                            CurrentEntry += " " + DataList[i];
                        }
                    }
                }
                DeDuplicateDataList.Add(CurrentEntry);
            }
            // 去重
            McLaunchArgumentsGameNewRet = DeDuplicateDataList.Distinct().ToList().Join(" ");

            // 特别改变 OptiFineTweaker
            if ((Version.Version.HasForge || Version.Version.HasLiteLoader) && Version.Version.HasOptiFine)
            {
                // 把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
                if (McLaunchArgumentsGameNewRet.Contains("--tweakClass optifine.OptiFineForgeTweaker"))
                {
                    ModBase.Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" + McLaunchArgumentsGameNewRet);
                    McLaunchArgumentsGameNewRet = McLaunchArgumentsGameNewRet.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "").Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") + " --tweakClass optifine.OptiFineForgeTweaker";
                }
                if (McLaunchArgumentsGameNewRet.Contains("--tweakClass optifine.OptiFineTweaker"))
                {
                    ModBase.Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" + McLaunchArgumentsGameNewRet);
                    McLaunchArgumentsGameNewRet = McLaunchArgumentsGameNewRet.Replace(" --tweakClass optifine.OptiFineTweaker", "").Replace("--tweakClass optifine.OptiFineTweaker ", "") + " --tweakClass optifine.OptiFineForgeTweaker";
                    try
                    {
                        ModBase.WriteFile(Version.Path + Version.Name + ".json", ModBase.ReadFile(Version.Path + Version.Name + ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"));
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "替换 OptiFineForge TweakClass 失败");
                    }
                }
            }

            return McLaunchArgumentsGameNewRet;
        }

        // 替换 Arguments
        private static Dictionary<string, string> McLaunchArgumentsReplace(ModMinecraft.McVersion Version, ref ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>> Loader)
        {
            var GameArguments = new Dictionary<string, string>();

            // 基础参数
            GameArguments.Add("${classpath_separator}", ";");
            GameArguments.Add("${natives_directory}", ModBase.ShortenPath(GetNativesFolder()));
            GameArguments.Add("${library_directory}", ModBase.ShortenPath(ModMinecraft.PathMcFolder + "libraries"));
            GameArguments.Add("${libraries_directory}", ModBase.ShortenPath(ModMinecraft.PathMcFolder + "libraries"));
            GameArguments.Add("${launcher_name}", "PCL");
            GameArguments.Add("${launcher_version}", ModBase.VersionCode.ToString());
            GameArguments.Add("${version_name}", Version.Name);
            string ArgumentInfo = Conversions.ToString(ModBase.Setup.Get("VersionArgumentInfo", Version: ModMinecraft.McVersionCurrent));
            GameArguments.Add("${version_type}", Conversions.ToString(string.IsNullOrEmpty(ArgumentInfo) ? ModBase.Setup.Get("LaunchArgumentInfo") : ArgumentInfo));
            GameArguments.Add("${game_directory}", ModBase.ShortenPath(Strings.Left(ModMinecraft.McVersionCurrent.PathIndie, ModMinecraft.McVersionCurrent.PathIndie.Count() - 1)));
            GameArguments.Add("${assets_root}", ModBase.ShortenPath(ModMinecraft.PathMcFolder + "assets"));
            GameArguments.Add("${user_properties}", "{}");
            GameArguments.Add("${auth_player_name}", McLoginLoader.Output.Name);
            GameArguments.Add("${auth_uuid}", McLoginLoader.Output.Uuid);
            GameArguments.Add("${auth_access_token}", McLoginLoader.Output.AccessToken);
            GameArguments.Add("${access_token}", McLoginLoader.Output.AccessToken);
            GameArguments.Add("${auth_session}", McLoginLoader.Output.AccessToken);
            GameArguments.Add("${user_type}", "msa"); // #1221

            // 窗口尺寸参数
            Size GameSize;
            switch (ModBase.Setup.Get("LaunchArgumentWindowType"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 2, false):
                    {
                        var Result = default(Size);
                        ModBase.RunInUiWait(() => Result = new Size(ModBase.GetPixelSize(ModMain.FrmMain.PanForm.ActualWidth), ModBase.GetPixelSize(ModMain.FrmMain.PanForm.ActualHeight)));
                        GameSize = Result;
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 3, false):
                    {
                        GameSize = new Size(Conversions.ToDouble(Math.Max(100, Operators.SubtractObject(ModBase.Setup.Get("LaunchArgumentWindowWidth"), 2))), Conversions.ToDouble(Math.Max(100, Operators.SubtractObject(ModBase.Setup.Get("LaunchArgumentWindowHeight"), 2))));
                        break;
                    }

                default:
                    {
                        GameSize = new Size(875 - 2, 540 - 2);
                        break;
                    }
            }
            GameSize.Height -= 29.5d * ModBase.DPI / 96d; // 标题栏高度
            if (ModMinecraft.McVersionCurrent.Version.McCodeMain <= 12 && McLaunchJavaSelected.VersionCode <= 8 && McLaunchJavaSelected.Version.Revision >= 200 && McLaunchJavaSelected.Version.Revision <= 321 && !ModMinecraft.McVersionCurrent.Version.HasOptiFine && !ModMinecraft.McVersionCurrent.Version.HasForge)
            {
                // 修复 #3463：1.12.2-，JRE 8u200~321 下窗口大小为设置大小的 DPI% 倍
                McLaunchLog($"已应用窗口大小过大修复（{McLaunchJavaSelected.Version.Revision}）");
                GameSize.Width /= ModBase.DPI / 96d;
                GameSize.Height /= ModBase.DPI / 96d;
            }
            GameArguments.Add("${resolution_width}", Math.Round(GameSize.Width).ToString());
            GameArguments.Add("${resolution_height}", Math.Round(GameSize.Height).ToString());

            // Assets 相关参数
            GameArguments.Add("${game_assets}", ModBase.ShortenPath(ModMinecraft.PathMcFolder + @"assets\virtual\legacy")); // 1.5.2 的 pre-1.6 资源索引应与 legacy 合并
            GameArguments.Add("${assets_index_name}", ModMinecraft.McAssetsGetIndexName(Version));

            // 支持库参数
            var LibList = ModMinecraft.McLibListGet(Version, true);
            Loader.Output = LibList;
            var CpStrings = new List<string>();
            string OptiFineCp = null;
            foreach (ModMinecraft.McLibToken Library in LibList)
            {
                if (Library.IsNatives)
                    continue;
                if (Library.Name is not null && Library.Name.Contains("com.cleanroommc:cleanroom")) // Cleanroom 的主 Jar 必须放在 ClassPath 第一位
                {
                    CpStrings.Insert(0, Library.LocalPath + ";");
                }
                if (Library.Name is not null && Library.Name == "optifine:OptiFine")
                {
                    OptiFineCp = Library.LocalPath;
                }
                else
                {
                    CpStrings.Add(Library.LocalPath);
                }
            }
            if (OptiFineCp is not null)
                CpStrings.Insert(CpStrings.Count - 2, OptiFineCp);
            GameArguments.Add("${classpath}", CpStrings.Select(c => ModBase.ShortenPath(c)).Join(";"));

            return GameArguments;
        }

        #endregion

        #region 解压 Natives

        private static void McLaunchNatives(ModLoader.LoaderTask<List<ModMinecraft.McLibToken>, int> Loader)
        {

            // 创建文件夹
            string Target = GetNativesFolder() + @"\";
            Directory.CreateDirectory(Target);

            // 解压文件
            McLaunchLog("正在解压 Natives 文件");
            var ExistFiles = new List<string>();
            foreach (ModMinecraft.McLibToken Native in Loader.Input)
            {
                if (!Native.IsNatives)
                    continue;
                ZipArchive Zip;
                try
                {
                    Zip = new ZipArchive(new FileStream(Native.LocalPath, FileMode.Open));
                }
                catch (InvalidDataException ex)
                {
                    ModBase.Log(ex, "打开 Natives 文件失败（" + Native.LocalPath + "）");
                    File.Delete(Native.LocalPath);
                    throw new Exception("无法打开 Natives 文件（" + Native.LocalPath + "），该文件可能已损坏，请重新尝试启动游戏");
                }
                foreach (var Entry in Zip.Entries)
                {
                    string FileName = Entry.FullName;
                    if (FileName.EndsWithF(".dll", true))
                    {
                        // 实际解压文件的步骤
                        string FilePath = Target + FileName;
                        ExistFiles.Add(FilePath);
                        var OriginalFile = new FileInfo(FilePath);
                        if (OriginalFile.Exists)
                        {
                            if (OriginalFile.Length == Entry.Length)
                            {
                                if (ModBase.ModeDebug)
                                    McLaunchLog("无需解压：" + FilePath);
                                continue;
                            }
                            // 删除原文件
                            try
                            {
                                File.Delete(FilePath);
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                McLaunchLog("删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" + FilePath);
                                McLaunchLog("实际的错误信息：" + ModBase.GetExceptionSummary(ex));
                                break;
                            }
                        }
                        // 解压新文件
                        ModBase.WriteFile(FilePath, Entry.Open());
                        McLaunchLog("已解压：" + FilePath);
                    }
                }
                if (Zip is not null)
                    Zip.Dispose();
            }

            // 删除多余文件
            foreach (string FileName in Directory.GetFiles(Target))
            {
                if (ExistFiles.Contains(FileName))
                    continue;
                try
                {
                    McLaunchLog("删除：" + FileName);
                    File.Delete(FileName);
                }
                catch (UnauthorizedAccessException ex)
                {
                    McLaunchLog("删除多余文件访问被拒绝，跳过删除步骤");
                    McLaunchLog("实际的错误信息：" + ModBase.GetExceptionSummary(ex));
                    return;
                }
            }

        }
        /// <summary>
    /// 获取 Natives 文件夹路径，不以 \ 结尾。
    /// </summary>
        private static string GetNativesFolder()
        {
            string Result = ModMinecraft.McVersionCurrent.Path + ModMinecraft.McVersionCurrent.Name + "-natives";
            if (ModBase.IsGBKEncoding || Result.IsASCII())
                return Result;
            Result = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\bin\natives";
            if (Result.IsASCII())
                return Result;
            return ModBase.OsDrive + @"ProgramData\PCL\natives";
        }

        #endregion

        #region 启动与前后处理

        private static void McLaunchPrerun()
        {

            // 要求 Java 使用高性能显卡
            if (Conversions.ToBoolean(ModBase.Setup.Get("LaunchAdvanceGraphicCard")))
            {
                try
                {
                    ModMain.SetGPUPreference(McLaunchJavaSelected.PathJavaw);
                    ModMain.SetGPUPreference(ModBase.PathWithName);
                }
                catch (Exception ex)
                {
                    if (ModBase.IsAdmin())
                    {
                        ModBase.Log(ex, "直接调整显卡设置失败");
                    }
                    else
                    {
                        ModBase.Log(ex, "直接调整显卡设置失败，将以管理员权限重启 PCL 再次尝试");
                        try
                        {
                            if (ModBase.RunAsAdmin($"--gpu \"{McLaunchJavaSelected.PathJavaw}\"") == (int)ModBase.ProcessReturnValues.TaskDone)
                            {
                                McLaunchLog("以管理员权限重启 PCL 并调整显卡设置成功");
                            }
                            else
                            {
                                throw new Exception("调整过程中出现异常");
                            }
                        }
                        catch (Exception exx)
                        {
                            ModBase.Log(exx, "调整显卡设置失败，Minecraft 可能会使用默认显卡运行", ModBase.LogLevel.Hint);
                        }
                    }
                }
            }

            // 更新 launcher_profiles.json
            do
            {
                try
                {
                    // 确保可用
                    if (!(McLoginLoader.Output.Type == "Microsoft"))
                        break;
                    ModMinecraft.McFolderLauncherProfilesJsonCreate(ModMinecraft.PathMcFolder);
                    // 构建需要替换的 Json 对象
                    string ReplaceJsonString = @"
            {
              ""authenticationDatabase"": {
                ""00000111112222233333444445555566"": {
                  ""username"": """ + McLoginLoader.Output.Name.Replace("\"", "-") + @""",
                  ""profiles"": {
                    ""66666555554444433333222221111100"": {
                        ""displayName"": """ + McLoginLoader.Output.Name + @"""
                    }
                  }
                }
              },
              ""clientToken"": """ + McLoginLoader.Output.ClientToken + @""",
              ""selectedUser"": {
                ""account"": ""00000111112222233333444445555566"", 
                ""profile"": ""66666555554444433333222221111100""
              }
            }";
                    JObject ReplaceJson = (JObject)ModBase.GetJson(ReplaceJsonString);
                    // 更新文件
                    JObject Profiles = (JObject)ModBase.GetJson(ModBase.ReadFile(ModMinecraft.PathMcFolder + "launcher_profiles.json"));
                    Profiles.Merge(ReplaceJson);
                    ModBase.WriteFile(ModMinecraft.PathMcFolder + "launcher_profiles.json", Profiles.ToString(), Encoding: Encoding.GetEncoding("GB18030"));
                    McLaunchLog("已更新 launcher_profiles.json");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "更新 launcher_profiles.json 失败，将在删除文件后重试");
                    try
                    {
                        File.Delete(ModMinecraft.PathMcFolder + "launcher_profiles.json");
                        ModMinecraft.McFolderLauncherProfilesJsonCreate(ModMinecraft.PathMcFolder);
                        // 构建需要替换的 Json 对象
                        string ReplaceJsonString = @"
                    {
                      ""authenticationDatabase"": {
                        ""00000111112222233333444445555566"": {
                          ""username"": """ + McLoginLoader.Output.Name.Replace("\"", "-") + @""",
                          ""profiles"": {
                            ""66666555554444433333222221111100"": {
                                ""displayName"": """ + McLoginLoader.Output.Name + @"""
                            }
                          }
                        }
                      },
                      ""clientToken"": """ + McLoginLoader.Output.ClientToken + @""",
                      ""selectedUser"": {
                        ""account"": ""00000111112222233333444445555566"", 
                        ""profile"": ""66666555554444433333222221111100""
                      }
                    }";
                        JObject ReplaceJson = (JObject)ModBase.GetJson(ReplaceJsonString);
                        // 更新文件
                        JObject Profiles = (JObject)ModBase.GetJson(ModBase.ReadFile(ModMinecraft.PathMcFolder + "launcher_profiles.json"));
                        Profiles.Merge(ReplaceJson);
                        ModBase.WriteFile(ModMinecraft.PathMcFolder + "launcher_profiles.json", Profiles.ToString(), Encoding: Encoding.GetEncoding("GB18030"));
                        McLaunchLog("已在删除后更新 launcher_profiles.json");
                    }
                    catch (Exception exx)
                    {
                        ModBase.Log(exx, "更新 launcher_profiles.json 失败", ModBase.LogLevel.Feedback);
                    }
                }
            }
            while (false);

            // 设置 Java 选项为高性能
            ModifyJavaGPUPreferences(McLaunchJavaSelected.PathJavaw);

            // 更新 options.txt
            string SetupFileAddress = ModMinecraft.McVersionCurrent.PathIndie + "options.txt";
            if (!File.Exists(SetupFileAddress))
            {
                // Yosbr Mod 兼容（#2385）：https://www.curseforge.com/minecraft/mc-mods/yosbr
                string YosbrFileAddress = ModMinecraft.McVersionCurrent.PathIndie + @"config\yosbr\options.txt";
                if (File.Exists(YosbrFileAddress))
                {
                    McLaunchLog("将修改 Yosbr Mod 中的 options.txt");
                    SetupFileAddress = YosbrFileAddress;
                    ModBase.WriteIni(SetupFileAddress, "lang", "none"); // 忽略默认语言
                }
            }
            try
            {
                // 语言
                // 1.0-     ：没有语言选项
                // 1.1 ~ 5  ：zh_CN 时正常，zh_cn 时崩溃（最后两位字母必须大写，否则将会 NPE 崩溃）
                // 1.6 ~ 10 ：zh_CN 时正常，zh_cn 时自动切换为英文
                // 1.11 ~ 12：zh_cn 时正常，zh_CN 时虽然显示了中文但语言设置会错误地显示选择英文
                // 1.13+    ：zh_cn 时正常，zh_CN 时自动切换为英文
                string CurrentLang = ModBase.ReadIni(SetupFileAddress, "lang", "none");
                string RequiredLang = CurrentLang == "none" || !Directory.Exists(ModMinecraft.McVersionCurrent.PathIndie + "saves") ? Conversions.ToBoolean(ModBase.Setup.Get("ToolHelpChinese")) ? "zh_cn" : "en_us" : CurrentLang.ToLower(); // #3844，整合包可能已经自带了 options.txt
                if (ModMinecraft.McVersionCurrent.Version.McCodeMain < 12) // 注意老版本（包含 MC 1.1）的 McCodeMain 可能为 -1
                {
                    // 将最后两位改为大写，前面的部分保留
                    RequiredLang = RequiredLang.Substring(0, RequiredLang.Length - 2) + RequiredLang.Substring(RequiredLang.Length - 2).ToUpper();
                }
                if ((CurrentLang ?? "") == (RequiredLang ?? ""))
                {
                    McLaunchLog($"需要的语言为 {RequiredLang}，当前语言为 {CurrentLang}，无需修改");
                }
                else
                {
                    ModBase.WriteIni(SetupFileAddress, "lang", "-"); // 触发缓存更改，避免删除后重新下载残留缓存
                    ModBase.WriteIni(SetupFileAddress, "lang", RequiredLang);
                    McLaunchLog($"已将语言从 {CurrentLang} 修改为 {RequiredLang}");
                }
                // '如果是初次设置，一并修改 forceUnicodeFont
                // If Setup.Get("ToolHelpChinese") AndAlso (CurrentLang = "none" OrElse Not Directory.Exists(McVersionCurrent.PathIndie & "saves")) Then
                // WriteIni(SetupFileAddress, "forceUnicodeFont", "true")
                // McLaunchLog("已开启 forceUnicodeFont")
                // End If
                // 窗口
                switch (ModBase.Setup.Get("LaunchArgumentWindowType"))
                {
                    case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 全屏
                        {
                            ModBase.WriteIni(SetupFileAddress, "fullscreen", "true");
                            break;
                        }
                    case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 默认
                                                                                                  // 其他
                        {
                            break;
                        }

                    default:
                        {
                            ModBase.WriteIni(SetupFileAddress, "fullscreen", "false");
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "更新 options.txt 失败", ModBase.LogLevel.Hint);
            }

            // 离线皮肤 Alex 警告
            if (Conversions.ToBoolean(ModMinecraft.McVersionCurrent.Version.McCodeMain <= 7 && ModMinecraft.McVersionCurrent.Version.McCodeMain >= 2 && McLoginLoader.Input.Type == McLoginType.Legacy && (Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchSkinType"), 2, false) || Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchSkinType"), 4, false) && (bool)ModBase.Setup.Get("LaunchSkinSlim")))) // 1.2 ~ 1.7
                                                                                                                                                                                                                                                                                                                                                                                                                                      // 离线登录
                                                                                                                                                                                                                                                                                                                                                                                                                                      // 强制 Alex
                                                                                                                                                                                                                                                                                                                                                                                                                                      // 或选用 Alex 皮肤
            {
                ModMain.Hint("此 Minecraft 版本尚不支持 Alex 皮肤，你的皮肤可能会显示为 Steve！", ModMain.HintType.Critical);
            }

            // 离线皮肤资源包
            try
            {
                Directory.CreateDirectory(ModMinecraft.McVersionCurrent.PathIndie + @"resourcepacks\");
                string ZipFileAddress = ModMinecraft.McVersionCurrent.PathIndie + @"resourcepacks\PCL2 Skin.zip";
                bool NewTypeSetup = ModMinecraft.McVersionCurrent.Version.McCodeMain >= 13 || ModMinecraft.McVersionCurrent.Version.McCodeMain < 6;
                if (McLoginLoader.Input.Type == McLoginType.Legacy && Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchSkinType"), 4, false)) && File.Exists(ModBase.PathAppdata + "CustomSkin.png"))
                {
                    Directory.CreateDirectory(ModBase.PathTemp);
                    string MetaFileAddress = ModBase.PathTemp + "pack.mcmeta";
                    string PackPicAddress = ModBase.PathTemp + "pack.png";
                    int PackFormat;
                    switch (ModMinecraft.McVersionCurrent.Version.McCodeMain)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                            {
                                // 更早的版本没有资源包；如果判断失败该值为 -1，不会跑到这
                                McLaunchLog("Minecraft 版本过老，尚不支持自定义离线皮肤");
                                goto IgnoreCustomSkin;
                                break;
                            }
                        case 6:
                        case 7:
                        case 8:
                            {
                                PackFormat = 1;
                                break;
                            }
                        case 9:
                        case 10:
                            {
                                PackFormat = 2;
                                break;
                            }
                        case 11:
                        case 12:
                            {
                                PackFormat = 3;
                                break;
                            }
                        case 13:
                        case 14:
                            {
                                PackFormat = 4;
                                break;
                            }
                        case 15:
                            {
                                PackFormat = 5;
                                break;
                            }
                        case 16:
                            {
                                PackFormat = 6;
                                break;
                            }
                        case 17:
                            {
                                PackFormat = 7;
                                break;
                            }
                        case 18:
                            {
                                if (ModMinecraft.McVersionCurrent.Version.McCodeSub <= 2)
                                {
                                    PackFormat = 8;
                                }
                                else
                                {
                                    PackFormat = 9;
                                }

                                break;
                            }
                        case 19:
                            {
                                if (ModMinecraft.McVersionCurrent.Version.McCodeSub <= 3)
                                {
                                    PackFormat = 9;
                                }
                                else
                                {
                                    PackFormat = 12;
                                }

                                break;
                            }
                        case 20:
                            {
                                if (ModMinecraft.McVersionCurrent.Version.McCodeSub <= 1)
                                {
                                    PackFormat = 15;
                                }
                                else
                                {
                                    PackFormat = 17;
                                } // 快照版是 99

                                break;
                            }

                        default:
                            {
                                PackFormat = 17;
                                break;
                            }
                            // https://zh.minecraft.wiki/w/数据包#数据包版本
                    }
                    McLaunchLog("正在构建自定义皮肤资源包，格式为：" + PackFormat);
                    // 准备文件
                    var Bit = new MyBitmap(ModBase.PathImage + "Heads/Logo.png");
                    Bit.Save(PackPicAddress);
                    ModBase.WriteFile(MetaFileAddress, "{\"pack\":{\"pack_format\":" + PackFormat + ",\"description\":\"PCL 自定义离线皮肤资源包\"}}");
                    var Skin = new MyBitmap(ModBase.PathAppdata + "CustomSkin.png");
                    if ((ModMinecraft.McVersionCurrent.Version.McCodeMain == 6 || ModMinecraft.McVersionCurrent.Version.McCodeMain == 7) && Skin.Pic.Height == 64)
                    {
                        McLaunchLog("该 Minecraft 版本不支持双层皮肤，已进行裁剪");
                        Skin = Skin.Clip(0, 0, 64, 32);
                    }
                    Skin.Save(ModBase.Path + @"PCL\CustomSkin_Cliped.png");
                    // 构建压缩文件
                    using (var ZipFile = new FileStream(ZipFileAddress, FileMode.Create))
                    {
                        using (var ZipAr = new ZipArchive(ZipFile, ZipArchiveMode.Create))
                        {
                            ZipAr.CreateEntryFromFile(MetaFileAddress, "pack.mcmeta");
                            ZipAr.CreateEntryFromFile(PackPicAddress, "pack.png");
                            // 1.19.3+ 使用复杂版本的替换
                            bool IsOldType;
                            switch (ModMinecraft.McVersionCurrent.Version.McCodeMain)
                            {
                                case var case2 when case2 < 19:
                                    {
                                        IsOldType = true;
                                        break;
                                    }
                                case 19:
                                    {
                                        IsOldType = ModMinecraft.McVersionCurrent.Version.McCodeSub <= 2;
                                        break;
                                    }

                                default:
                                    {
                                        IsOldType = false;
                                        break;
                                    }
                            }
                            if (IsOldType)
                            {
                                ZipAr.CreateEntryFromFile(ModBase.Path + @"PCL\CustomSkin_Cliped.png", "assets/minecraft/textures/entity/" + (Conversions.ToBoolean(ModBase.Setup.Get("LaunchSkinSlim")) ? "alex.png" : "steve.png"));
                            }
                            else
                            {
                                foreach (var SkinName in new[] { "alex", "ari", "efe", "kai", "makena", "noor", "steve", "sunny", "zuri" })
                                    ZipAr.CreateEntryFromFile(ModBase.Path + @"PCL\CustomSkin_Cliped.png", $"assets/minecraft/textures/entity/player/{(Conversions.ToBoolean(ModBase.Setup.Get("LaunchSkinSlim")) ? "slim" : "wide")}/{SkinName}.png");
                            }
                        }
                    }
                    File.Delete(ModBase.Path + @"PCL\CustomSkin_Cliped.png");
                    // 更改设置文件
                    ModBase.IniClearCache(SetupFileAddress);
                    string EnabledResourcePack = ModBase.ReadIni(SetupFileAddress, "resourcePacks", "[]").TrimStart('[').TrimEnd(']');
                    if (NewTypeSetup)
                    {
                        if (string.IsNullOrEmpty(EnabledResourcePack))
                            EnabledResourcePack = "\"vanilla\"";
                        var EnabledResourcePacks = new List<string>(EnabledResourcePack.Split(","));
                        var NewResourcePacks = new List<string>();
                        foreach (var Res in EnabledResourcePacks)
                        {
                            if (Res != "\"file/PCL2 Skin.zip\"" && !string.IsNullOrEmpty(Res))
                                NewResourcePacks.Add(Res);
                        }
                        NewResourcePacks.Add("\"file/PCL2 Skin.zip\"");
                        string Result = "[" + NewResourcePacks.Join(",") + "]";
                        ModBase.WriteIni(SetupFileAddress, "resourcePacks", Result);
                    }
                    else
                    {
                        var EnabledResourcePacks = new List<string>(EnabledResourcePack.Split(","));
                        var NewResourcePacks = new List<string>();
                        foreach (var Res in EnabledResourcePacks)
                        {
                            if (Res != "\"PCL2 Skin.zip\"" && !string.IsNullOrEmpty(Res))
                                NewResourcePacks.Add(Res);
                        }
                        NewResourcePacks.Add("\"PCL2 Skin.zip\"");
                        string Result = "[" + NewResourcePacks.Join(",") + "]";
                        ModBase.WriteIni(SetupFileAddress, "resourcePacks", Result);
                    }

                IgnoreCustomSkin:
                    ;
                }

                else if (File.Exists(ZipFileAddress))
                {
                    McLaunchLog("正在清空自定义皮肤资源包");
                    // 删除压缩文件
                    File.Delete(ZipFileAddress);
                    // 更改设置文件
                    ModBase.IniClearCache(SetupFileAddress);
                    string EnabledResourcePack = ModBase.ReadIni(SetupFileAddress, "resourcePacks", "[]").TrimStart('[').TrimEnd(']');
                    if (NewTypeSetup)
                    {
                        if (string.IsNullOrEmpty(EnabledResourcePack))
                            EnabledResourcePack = "\"vanilla\"";
                        var EnabledResourcePacks = new List<string>(EnabledResourcePack.Split(","));
                        EnabledResourcePacks.Remove("\"file/PCL2 Skin.zip\"");
                        string Result = "[" + EnabledResourcePacks.Join(",") + "]";
                        ModBase.WriteIni(SetupFileAddress, "resourcePacks", Result);
                    }
                    else
                    {
                        var EnabledResourcePacks = new List<string>(EnabledResourcePack.Split(","));
                        EnabledResourcePacks.Remove("\"PCL2 Skin.zip\"");
                        string Result = "[" + EnabledResourcePacks.Join(",") + "]";
                        ModBase.WriteIni(SetupFileAddress, "resourcePacks", Result);
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "离线皮肤资源包设置失败", ModBase.LogLevel.Hint);
            }

        }
        private static void McLaunchCustom(ModLoader.LoaderTask<int, int> Loader)
        {

            // 获取自定义命令
            string CustomCommandGlobal = Conversions.ToString(ModBase.Setup.Get("LaunchAdvanceRun"));
            if (!string.IsNullOrEmpty(CustomCommandGlobal))
                CustomCommandGlobal = ArgumentReplace(CustomCommandGlobal, true);
            string CustomCommandVersion = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceRun", Version: ModMinecraft.McVersionCurrent));
            if (!string.IsNullOrEmpty(CustomCommandVersion))
                CustomCommandVersion = ArgumentReplace(CustomCommandVersion, true);

            // 输出 bat
            try
            {
                string CmdString = $"{(McLaunchJavaSelected.VersionCode > 8 ? "chcp 65001>nul" + Constants.vbCrLf : "")}" + "@echo off" + Constants.vbCrLf + $"title 启动 - {ModMinecraft.McVersionCurrent.Name}" + Constants.vbCrLf + "echo 游戏正在启动，请稍候。" + Constants.vbCrLf + $"set APPDATA=\"{ModBase.ShortenPath(ModMinecraft.McVersionCurrent.PathIndie)}\"" + Constants.vbCrLf + $"cd /D \"{ModBase.ShortenPath(ModMinecraft.McVersionCurrent.PathIndie)}\"" + Constants.vbCrLf + CustomCommandGlobal + Constants.vbCrLf + CustomCommandVersion + Constants.vbCrLf + $"\"{McLaunchJavaSelected.PathJava}\" {McLaunchArgument}" + Constants.vbCrLf + "echo 游戏已退出。" + Constants.vbCrLf + "pause";
                ModBase.WriteFile(CurrentLaunchOptions.SaveBatch ?? ModBase.Path + @"PCL\LatestLaunch.bat", ModSecret.SecretFilter(CmdString, 'F'), Encoding: Encoding.Default.Equals(Encoding.UTF8) ? Encoding.UTF8 : Encoding.GetEncoding("GB18030"));
                if (CurrentLaunchOptions.SaveBatch is not null)
                {
                    McLaunchLog("导出启动脚本完成，强制结束启动过程");
                    AbortHint = "导出启动脚本成功！";
                    ModBase.OpenExplorer(CurrentLaunchOptions.SaveBatch);
                    Loader.Parent.Abort();
                    return; // 导出脚本完成
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "输出启动脚本失败");
                if (CurrentLaunchOptions.SaveBatch is not null)
                    throw ex; // 直接触发启动失败
            }

            // 执行自定义命令
            if (!string.IsNullOrEmpty(CustomCommandGlobal))
            {
                McLaunchLog("正在执行全局自定义命令：" + CustomCommandGlobal);
                var CustomProcess = new Process();
                try
                {
                    CustomProcess.StartInfo.FileName = "cmd.exe";
                    CustomProcess.StartInfo.Arguments = "/c \"" + CustomCommandGlobal + "\"";
                    CustomProcess.StartInfo.WorkingDirectory = ModBase.ShortenPath(ModMinecraft.PathMcFolder);
                    CustomProcess.StartInfo.UseShellExecute = false;
                    CustomProcess.StartInfo.CreateNoWindow = true;
                    CustomProcess.Start();
                    if (Conversions.ToBoolean(ModBase.Setup.Get("LaunchAdvanceRunWait")))
                    {
                        while (!CustomProcess.HasExited && !Loader.IsAborted)
                            Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "执行全局自定义命令失败", ModBase.LogLevel.Hint);
                }
                finally
                {
                    if (!CustomProcess.HasExited && Loader.IsAborted)
                    {
                        McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程"); // #1183
                        CustomProcess.Kill();
                    }
                }
            }
            if (!string.IsNullOrEmpty(CustomCommandVersion))
            {
                McLaunchLog("正在执行版本自定义命令：" + CustomCommandVersion);
                var CustomProcess = new Process();
                try
                {
                    CustomProcess.StartInfo.FileName = "cmd.exe";
                    CustomProcess.StartInfo.Arguments = "/c \"" + CustomCommandVersion + "\"";
                    CustomProcess.StartInfo.WorkingDirectory = ModBase.ShortenPath(ModMinecraft.PathMcFolder);
                    CustomProcess.StartInfo.UseShellExecute = false;
                    CustomProcess.StartInfo.CreateNoWindow = true;
                    CustomProcess.Start();
                    if (Conversions.ToBoolean(ModBase.Setup.Get("VersionAdvanceRunWait", Version: ModMinecraft.McVersionCurrent)))
                    {
                        while (!CustomProcess.HasExited && !Loader.IsAborted)
                            Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "执行版本自定义命令失败", ModBase.LogLevel.Hint);
                }
                finally
                {
                    if (!CustomProcess.HasExited && Loader.IsAborted)
                    {
                        McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程"); // #1183
                        CustomProcess.Kill();
                    }
                }
            }

        }
        private static void McLaunchRun(ModLoader.LoaderTask<int, Process> Loader)
        {

            // 启动信息
            var GameProcess = new Process();
            var StartInfo = new ProcessStartInfo(McLaunchJavaSelected.PathJavaw);

            // 设置环境变量
            var Paths = new List<string>(StartInfo.EnvironmentVariables["Path"].Split(";"));
            Paths.Add(ModBase.ShortenPath(McLaunchJavaSelected.PathFolder));
            StartInfo.EnvironmentVariables["Path"] = Paths.Distinct().ToList().Join(";");
            StartInfo.EnvironmentVariables["appdata"] = ModBase.ShortenPath(ModMinecraft.PathMcFolder);

            // 设置其他参数
            StartInfo.StandardErrorEncoding = McLaunchJavaSelected.VersionCode > 8 ? Encoding.UTF8 : null;
            StartInfo.StandardOutputEncoding = McLaunchJavaSelected.VersionCode > 8 ? Encoding.UTF8 : null;
            StartInfo.WorkingDirectory = ModBase.ShortenPath(ModMinecraft.McVersionCurrent.PathIndie);
            StartInfo.UseShellExecute = false;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.CreateNoWindow = false;
            StartInfo.Arguments = McLaunchArgument;
            GameProcess.StartInfo = StartInfo;

            // 开始进程
            GameProcess.Start();
            McLaunchLog("已启动游戏进程：" + McLaunchJavaSelected.PathJavaw);
            if (Loader.IsAborted)
            {
                McLaunchLog("由于取消启动，已强制结束游戏进程"); // #1631
                GameProcess.Kill();
                return;
            }
            Loader.Output = GameProcess;
            McLaunchProcess = GameProcess;
            // 进程优先级处理
            try
            {
                GameProcess.PriorityBoostEnabled = true;
                switch (ModBase.Setup.Get("LaunchArgumentPriority"))
                {
                    case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 高
                        {
                            GameProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                            break;
                        }
                    case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false): // 低
                        {
                            GameProcess.PriorityClass = ProcessPriorityClass.BelowNormal; // 中
                            break;
                        }

                    default:
                        {
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "设置进程优先级失败", ModBase.LogLevel.Feedback);
            }

        }
        private static void McLaunchWait(ModLoader.LoaderTask<Process, int> Loader)
        {

            // 输出信息
            McLaunchLog("");
            McLaunchLog("~ 基础参数 ~");
            McLaunchLog("PCL 版本：" + ModBase.VersionBaseName + " (" + ModBase.VersionCode + ")");
            McLaunchLog("游戏版本：" + ModMinecraft.McVersionCurrent.Version.ToString() + "（识别为 1." + ModMinecraft.McVersionCurrent.Version.McCodeMain + "." + ModMinecraft.McVersionCurrent.Version.McCodeSub + "）");
            McLaunchLog("资源版本：" + ModMinecraft.McAssetsGetIndexName(ModMinecraft.McVersionCurrent));
            McLaunchLog("版本继承：" + (string.IsNullOrEmpty(ModMinecraft.McVersionCurrent.InheritVersion) ? "无" : ModMinecraft.McVersionCurrent.InheritVersion));
            McLaunchLog("分配的内存：" + PageVersionSetup.GetRam(ModMinecraft.McVersionCurrent, !McLaunchJavaSelected.Is64Bit) + " GB（" + Math.Round(PageVersionSetup.GetRam(ModMinecraft.McVersionCurrent, !McLaunchJavaSelected.Is64Bit) * 1024d) + " MB）");
            McLaunchLog("MC 文件夹：" + ModMinecraft.PathMcFolder);
            McLaunchLog("版本文件夹：" + ModMinecraft.McVersionCurrent.Path);
            McLaunchLog("版本隔离：" + ((ModMinecraft.McVersionCurrent.PathIndie ?? "") == (ModMinecraft.McVersionCurrent.Path ?? "")));
            McLaunchLog("HMCL 格式：" + ModMinecraft.McVersionCurrent.IsHmclFormatJson);
            McLaunchLog("Java 信息：" + (McLaunchJavaSelected is not null ? McLaunchJavaSelected.ToString() : "无可用 Java"));
            McLaunchLog("环境变量：" + (McLaunchJavaSelected is not null ? McLaunchJavaSelected.HasEnvironment ? "已设置" : "未设置" : "未设置"));
            McLaunchLog("Natives 文件夹：" + GetNativesFolder());
            McLaunchLog("");
            McLaunchLog("~ 登录参数 ~");
            McLaunchLog("玩家用户名：" + McLoginLoader.Output.Name);
            McLaunchLog("AccessToken：" + McLoginLoader.Output.AccessToken);
            McLaunchLog("ClientToken：" + McLoginLoader.Output.ClientToken);
            McLaunchLog("UUID：" + McLoginLoader.Output.Uuid);
            McLaunchLog("登录方式：" + McLoginLoader.Output.Type);
            McLaunchLog("");

            // 获取窗口标题
            string WindowTitle = Conversions.ToString(ModBase.Setup.Get("VersionArgumentTitle", Version: ModMinecraft.McVersionCurrent));
            if (string.IsNullOrEmpty(WindowTitle))
                WindowTitle = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentTitle"));
            WindowTitle = ArgumentReplace(WindowTitle, false);

            // 初始化等待
            var Watcher = new ModWatcher.Watcher(Loader, ModMinecraft.McVersionCurrent, WindowTitle, CurrentLaunchOptions.Test);
            McLaunchWatcher = Watcher;

            // 显示实时日志
            if (CurrentLaunchOptions.Test)
            {
                if (ModMain.FrmLogLeft is null)
                    ModBase.RunInUiWait(() => ModMain.FrmLogLeft = new PageLogLeft());
                if (ModMain.FrmLogRight is null)
                    ModBase.RunInUiWait(() => ModMain.FrmLogRight = new PageLogRight());
                ModMain.FrmLogLeft.Add(Watcher);
                McLaunchLog("已显示游戏实时日志");
            }

            // 等待
            while (Watcher.State == ModWatcher.Watcher.MinecraftState.Loading)
                Thread.Sleep(100);
            if (Watcher.State == ModWatcher.Watcher.MinecraftState.Crashed)
            {
                throw new Exception("$$");
            }

        }
        private static void McLaunchEnd()
        {
            McLaunchLog("开始启动结束处理");

            // 暂停或开始音乐播放
            if (Conversions.ToBoolean(ModBase.Setup.Get("UiMusicStop")))
            {
                ModBase.RunInUi(() => { if (ModMusic.MusicPause()) ModBase.Log("[Music] 已根据设置，在启动后暂停音乐播放"); });
            }
            else if (Conversions.ToBoolean(ModBase.Setup.Get("UiMusicStart")))
            {
                ModBase.RunInUi(() => { if (ModMusic.MusicResume()) ModBase.Log("[Music] 已根据设置，在启动后开始音乐播放"); });
            }

            // 启动器可见性
            McLaunchLog(Conversions.ToString(Operators.ConcatenateObject("启动器可见性：", ModBase.Setup.Get("LaunchArgumentVisible"))));
            switch (ModBase.Setup.Get("LaunchArgumentVisible"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        // 直接关闭
                        McLaunchLog("已根据设置，在启动后关闭启动器");
                        ModBase.RunInUi(() => ModMain.FrmMain.EndProgram(false));
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false):
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, 3, false):
                    {
                        // 隐藏
                        McLaunchLog("已根据设置，在启动后隐藏启动器");
                        ModBase.RunInUi(() => ModMain.FrmMain.Hidden = true);
                        break;
                    }
                case var case3 when Operators.ConditionalCompareObjectEqual(case3, 4, false):
                    {
                        // 最小化
                        McLaunchLog("已根据设置，在启动后最小化启动器");
                        ModBase.RunInUi(() => ModMain.FrmMain.WindowState = WindowState.Minimized);
                        break;
                    }
                case var case4 when Operators.ConditionalCompareObjectEqual(case4, 5, false):
                    {
                        break;
                    }
                    // 啥都不干
            }

            // 启动计数
            ModBase.Setup.Set("SystemLaunchCount", Operators.AddObject(ModBase.Setup.Get("SystemLaunchCount"), 1));

        }

        /// <summary>
    /// 在启动结束时，对 PCL 约定的替换标记进行处理。
    /// </summary>
        private static string ArgumentReplace(string Raw, bool ReplaceTimeAndDate)
        {
            if (Raw is null)
                return null;
            // 路径替换
            Raw = Raw.Replace("{minecraft}", ModMinecraft.PathMcFolder);
            Raw = Raw.Replace("{verpath}", ModMinecraft.McVersionCurrent.Path);
            Raw = Raw.Replace("{verindie}", ModMinecraft.McVersionCurrent.PathIndie);
            Raw = Raw.Replace("{java}", McLaunchJavaSelected.PathFolder);
            // 普通替换
            Raw = Raw.Replace("{user}", McLoginLoader.Output.Name);
            Raw = Raw.Replace("{uuid}", McLoginLoader.Output.Uuid);
            if (ReplaceTimeAndDate) // 设置窗口标题时需要动态替换日期和时间
            {
                Raw = Raw.Replace("{date}", DateTime.Now.ToString("yyyy/M/d"));
                Raw = Raw.Replace("{time}", DateTime.Now.ToString("HH:mm:ss"));
            }
            switch (McLoginLoader.Input.Type)
            {
                case McLoginType.Legacy:
                    {
                        if (PageLinkLobby.HiperState == ModBase.LoadState.Finished)
                        {
                            Raw = Raw.Replace("{login}", "联机离线");
                        }
                        else
                        {
                            Raw = Raw.Replace("{login}", "离线");
                        }

                        break;
                    }
                case McLoginType.Ms:
                    {
                        Raw = Raw.Replace("{login}", "正版");
                        break;
                    }
                case McLoginType.Nide:
                    {
                        Raw = Raw.Replace("{login}", "统一通行证");
                        break;
                    }
                case McLoginType.Auth:
                    {
                        Raw = Raw.Replace("{login}", "Authlib-Injector");
                        break;
                    }
            }
            Raw = Raw.Replace("{name}", ModMinecraft.McVersionCurrent.Name);
            if (new[] { "unknown", "old", "pending" }.Contains(ModMinecraft.McVersionCurrent.Version.McName.ToLower()))
            {
                Raw = Raw.Replace("{version}", ModMinecraft.McVersionCurrent.Name);
            }
            else
            {
                Raw = Raw.Replace("{version}", ModMinecraft.McVersionCurrent.Version.McName);
            }
            Raw = Raw.Replace("{path}", ModBase.Path);
            return Raw;
        }

        #endregion

    }
}