using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class Application
    {
        public Application()
        {
            Startup += Application_Startup;
            SessionEnding += Application_SessionEnding;
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }

        /* TODO ERROR: Skipped IfDirectiveTrivia
        #If DEBUG Then
        *//* TODO ERROR: Skipped DisabledTextTrivia
            ''' <summary>
            ''' 用于开始程序时的一些测试。
            ''' </summary>
            Private Sub Test()
                Try
                    ModDevelop.Start()
                Catch ex As Exception
                    Log(ex, "开发者模式测试出错", LogLevel.Msgbox)
                End Try
            End Sub
        *//* TODO ERROR: Skipped EndIfDirectiveTrivia
        #End If
        */
        // 开始
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                ModSecret.SecretOnApplicationStart();
                // 检查参数调用
                if (e.Args.Length > 0)
                {
                    if (e.Args[0] == "--update")
                    {
                        // 自动更新
                        ModSecret.UpdateReplace(Conversions.ToInteger(e.Args[1]), e.Args[2].Trim('"'), e.Args[3].Trim('"'), Conversions.ToBoolean(e.Args[4]));
                        Environment.Exit((int)ModBase.ProcessReturnValues.TaskDone);
                    }
                    else if (e.Args[0] == "--gpu")
                    {
                        // 调整显卡设置
                        try
                        {
                            ModMain.SetGPUPreference(e.Args[1].Trim('"'));
                            Environment.Exit((int)ModBase.ProcessReturnValues.TaskDone);
                        }
                        catch (Exception ex)
                        {
                            Environment.Exit((int)ModBase.ProcessReturnValues.Fail);
                        }
                    }
                    else if (e.Args[0].StartsWithF("--memory"))
                    {
                        // 内存优化
                        ulong Ram = My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory;
                        try
                        {
                            PageOtherTest.MemoryOptimizeInternal(false);
                        }
                        catch (Exception ex)
                        {
                            Interaction.MsgBox(ex.Message, MsgBoxStyle.Critical, "内存优化失败");
                            Environment.Exit(-1);
                        }
                        if (My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory < Ram) // 避免 ULong 相减出现负数
                        {
                            Environment.Exit(0);
                        }
                        else
                        {
                            Environment.Exit((int)Math.Round((My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory - Ram) / 1024d));
                        } // 返回清理的内存量（K）
                        /* TODO ERROR: Skipped IfDirectiveTrivia
                        #If DEBUG Then
                        *//* TODO ERROR: Skipped DisabledTextTrivia
                                            '制作更新包
                                        ElseIf e.Args(0) = "--edit1" Then
                                            ExeEdit(e.Args(1), True)
                                            Environment.Exit(ProcessReturnValues.TaskDone)
                                        ElseIf e.Args(0) = "--edit2" Then
                                            ExeEdit(e.Args(1), False)
                                            Environment.Exit(ProcessReturnValues.TaskDone)
                        *//* TODO ERROR: Skipped EndIfDirectiveTrivia
                        #End If
                        */
                    }
                }
                // 初始化文件结构
                Directory.CreateDirectory(ModBase.Path + @"PCL\Pictures");
                Directory.CreateDirectory(ModBase.Path + @"PCL\Musics");
                try
                {
                    Directory.CreateDirectory(ModBase.PathTemp);
                    if (!ModBase.CheckPermission(ModBase.PathTemp))
                        throw new Exception("PCL 没有对 " + ModBase.PathTemp + " 的访问权限");
                }
                catch (Exception ex)
                {
                    if ((ModBase.PathTemp ?? "") == (System.IO.Path.GetTempPath() + @"PCL\" ?? ""))
                    {
                        ModMain.MyMsgBox("PCL 无法访问缓存文件夹，可能导致程序出错或无法正常使用！" + Constants.vbCrLf + "错误原因：" + ModBase.GetExceptionDetail(ex), "缓存文件夹不可用");
                    }
                    else
                    {
                        ModMain.MyMsgBox("手动设置的缓存文件夹不可用，PCL 将使用默认缓存文件夹。" + Constants.vbCrLf + "错误原因：" + ModBase.GetExceptionDetail(ex), "缓存文件夹不可用");
                        ModBase.Setup.Set("SystemSystemCache", "");
                        ModBase.PathTemp = System.IO.Path.GetTempPath() + @"PCL\";
                    }
                }
                Directory.CreateDirectory(ModBase.PathTemp + "Cache");
                Directory.CreateDirectory(ModBase.PathTemp + "Download");
                Directory.CreateDirectory(ModBase.PathAppdata);
                // 检测单例
                /* TODO ERROR: Skipped IfDirectiveTrivia
                #If Not DEBUG Then
                */
                bool ShouldWaitForExit = e.Args.Length > 0 && e.Args[0] == "--wait"; // 要求等待已有的 PCL 退出
                int WaitRetryCount = 0;
            WaitRetry:
                ;

                string argClassName = null;
                string argWindowName = "Plain Craft Launcher Community Edition ";
                var WindowHwnd = ModMain.FindWindow(ref argClassName, ref argWindowName);
                if (WindowHwnd == IntPtr.Zero)
                {
                    string argClassName1 = null;
                    string argWindowName1 = "Plain Craft Launcher 2 Community Edition ";
                    ModMain.FindWindow(ref argClassName1, ref argWindowName1);
                }
                if (WindowHwnd != IntPtr.Zero)
                {
                    if (ShouldWaitForExit && WaitRetryCount < 20) // 至多等待 10 秒
                    {
                        WaitRetryCount += 1;
                        Thread.Sleep(500);
                        goto WaitRetry;
                    }
                    // 将已有的 PCL 窗口拖出来
                    ModMain.ShowWindowToTop(WindowHwnd);
                    // 播放提示音并退出
                    Interaction.Beep();
                    Environment.Exit((int)ModBase.ProcessReturnValues.Cancel);
                }
                /* TODO ERROR: Skipped EndIfDirectiveTrivia
                #End If
                */            // 设置 ToolTipService 默认值
                ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(300));
                ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(400));
                ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(9999999));
                ToolTipService.PlacementProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(System.Windows.Controls.Primitives.PlacementMode.Bottom));
                ToolTipService.HorizontalOffsetProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(8.0d));
                ToolTipService.VerticalOffsetProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(4.0d));
                // 设置初始窗口
                if (Conversions.ToBoolean(ModBase.Setup.Get("UiLauncherLogo")))
                {
                    ModMain.FrmStart = new SplashScreen(@"Images\icon.ico");
                    ModMain.FrmStart.Show(false, true);
                }
                // 日志初始化
                ModBase.LogStart();
                // 添加日志
                ModBase.Log($"[Start] 程序版本：{ModBase.VersionBaseName} ({ModBase.VersionBranchName}, {ModBase.VersionCode}{(string.IsNullOrEmpty(ModBase.CommitHash) ? "" : $"，#{ModBase.CommitHash}")})");
                ModBase.Log($"[Start] 识别码：{ModBase.UniqueAddress}");
                ModBase.Log($"[Start] 程序路径：{ModBase.PathWithName}");
                ModBase.Log($"[Start] 系统版本：{Environment.OSVersion.Version}, 架构：{RuntimeInformation.OSArchitecture}");
                ModBase.Log($"[Start] 系统编码：{Encoding.Default.HeaderName} ({Encoding.Default.CodePage}, GBK={ModBase.IsGBKEncoding})");
                ModBase.Log($"[Start] 管理员权限：{ModBase.IsAdmin()}");
                // 检测异常环境
                if (ModBase.Path.Contains(System.IO.Path.GetTempPath()) || ModBase.Path.Contains(@"AppData\Local\Temp\"))
                {
                    ModMain.MyMsgBox("请将 PCL 从压缩包中解压之后再使用！" + Constants.vbCrLf + "在当前环境下运行可能会导致丢失游戏存档或设置，部分功能也可能无法使用！", "环境警告", "我知道了", IsWarn: true);
                }
                if (ModBase.Is32BitSystem)
                {
                    ModMain.MyMsgBox("PCL 和新版 Minecraft 均不再支持 32 位系统，部分功能将无法使用。" + Constants.vbCrLf + "非常建议重装为 64 位系统后再进行游戏！", "环境警告", "我知道了", IsWarn: true);
                }
                if (!(ModBase.Val(Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", "528049").ToString().AfterFirst("(").BeforeFirst(")")) >= 533320d))
                {
                    ModMain.MyMsgBox($"PCL CE 不再支持你当前使用的系统，部分功能将无法使用。{Constants.vbCrLf}PCL CE 要求系统版本至少为 Windows 10 20H2 且安装有 .NET Framework 4.8.1。{Constants.vbCrLf}非常建议升级到最新版本的 Windows 10 或 Windows 11！", "环境警告", "我知道了", IsWarn: true);
                }
                // 设置初始化
                ModBase.Setup.Load("SystemDebugMode");
                ModBase.Setup.Load("SystemDebugAnim");
                ModBase.Setup.Load("ToolDownloadThread");
                ModBase.Setup.Load("ToolDownloadCert");
                // 释放资源
                Directory.CreateDirectory(ModBase.PathPure + "CE");
                string arglpPathName = ModBase.PathPure + "CE";
                Application.SetDllDirectory(ref arglpPathName);
                ModBase.WriteFile(ModBase.PathPure + @"CE\" + "libwebp.dll", ModBase.GetResources("libwebp64"));
                // 网络配置初始化
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                ServicePointManager.DefaultConnectionLimit = 1024;
                // 设置字体
                string TargetFont = Conversions.ToString(ModBase.Setup.Get("UiFont"));
                if (!string.IsNullOrEmpty(TargetFont))
                {
                    try
                    {
                        var Font = Fonts.SystemFontFamilies.FirstOrDefault(x => x.FamilyNames.Values.Contains(TargetFont));
                        if (Font is null)
                        {
                            ModBase.Setup.Reset("UiFont");
                        }
                        else
                        {
                            ModBase.SetLaunchFont(TargetFont);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "字体加载失败", ModBase.LogLevel.Hint);
                        ModBase.Setup.Reset("UiFont");
                    }
                }
                // 计时
                ModBase.Log("[Start] 第一阶段加载用时：" + (ModBase.GetTimeTick() - ModBase.ApplicationStartTick) + " ms");
                ModBase.ApplicationStartTick = ModBase.GetTimeTick();
                // 执行测试
                /* TODO ERROR: Skipped IfDirectiveTrivia
                #If DEBUG Then
                *//* TODO ERROR: Skipped DisabledTextTrivia
                            Test()
                *//* TODO ERROR: Skipped EndIfDirectiveTrivia
                #End If
                */
                ModAnimation.AniControlEnabled += 1;
            }
            catch (Exception ex)
            {
                string FilePath = null;
                try
                {
                    FilePath = ModBase.PathWithName;
                }
                catch
                {
                }
                Interaction.MsgBox(ModBase.GetExceptionDetail(ex, true) + Constants.vbCrLf + "PCL 所在路径：" + (string.IsNullOrEmpty(FilePath) ? "获取失败" : FilePath), MsgBoxStyle.Critical, "PCL 初始化错误");
                FormMain.EndProgramForce(ModBase.ProcessReturnValues.Exception);
            }
        }

        // 结束
        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            ModMain.FrmMain.EndProgram(false);
        }

        // 异常
        private bool IsCritErrored = false;
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ;
#error Cannot convert OnErrorResumeNextStatementSyntax - see comment for details
            /* Cannot convert OnErrorResumeNextStatementSyntax, CONVERSION ERROR: Conversion for OnErrorResumeNextStatement not implemented, please report this issue in 'On Error Resume Next' at character 12704


                        Input:
                                On Error Resume Next

                         */
            e.Handled = true;
            if (ModBase.IsProgramEnded)
                return;
            if (IsCritErrored)
            {
                // 在汇报错误后继续引发错误，知道这次压不住了
                FormMain.EndProgramForce(ModBase.ProcessReturnValues.Exception);
                return;
            }
            IsCritErrored = true;
            string ExceptionString = ModBase.GetExceptionDetail(e.Exception, true);
            if (ExceptionString.Contains("System.Windows.Threading.Dispatcher.Invoke") || ExceptionString.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") || ExceptionString.Contains(".NET Framework") || ExceptionString.Contains("未能加载文件或程序集")) // “自动错误判断” 的结果分析
            {
                ModBase.OpenWebsite("https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net481-offline-installer");
                Interaction.MsgBox("你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8.1！", MsgBoxStyle.Information, "运行环境错误");
                FormMain.EndProgramForce(ModBase.ProcessReturnValues.Cancel);
            }
            else
            {
                ModBase.FeedbackInfo();
                ModBase.Log(e.Exception, "程序出现未知错误", ModBase.LogLevel.Assert, "锟斤拷烫烫烫");
            }
        }

        [DllImport("kernel32", EntryPoint = "SetDllDirectoryA")]
        private static extern bool SetDllDirectory(string lpPathName);


        // 切换窗口

        // 控件模板事件
        private void MyIconButton_Click(object sender, EventArgs e)
        {
            switch (ModBase.Setup.Get("LoginType"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, ModLaunch.McLoginType.Ms, false):
                    {
                        // 微软
                        JObject MsJson = (JObject)ModBase.GetJson(Conversions.ToString(ModBase.Setup.Get("LoginMsJson")));
                        MsJson.Remove(Conversions.ToString(((dynamic)sender).Tag));
                        ModBase.Setup.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None));
                        if (object.ReferenceEquals(ModMain.FrmLoginMs.ComboAccounts.SelectedItem, ((dynamic)sender).Parent))
                            ModMain.FrmLoginMs.ComboAccounts.SelectedIndex = 0;
                        ModMain.FrmLoginMs.ComboAccounts.Items.Remove(((dynamic)sender).Parent);
                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, ModLaunch.McLoginType.Legacy, false):
                    {
                        // 离线
                        var Names = new List<string>();
                        Names.AddRange(ModBase.Setup.Get("LoginLegacyName").ToString().Split("¨"));
                        Names.Remove(Conversions.ToString(((dynamic)sender).Tag));
                        ModBase.Setup.Set("LoginLegacyName", Names.Join("¨"));
                        ModMain.FrmLoginLegacy.ComboName.ItemsSource = Names;
                        ModMain.FrmLoginLegacy.ComboName.Text = Names.Any() ? Names[0] : "";
                        break;
                    }

                default:
                    {
                        // 第三方
                        string Token = ModBase.GetStringFromEnum((Enum)ModBase.Setup.Get("LoginType"));
                        var Dict = new Dictionary<string, string>();
                        var Names = new List<string>();
                        var Passs = new List<string>();
                        if (Conversions.ToBoolean(!Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("Login" + Token + "Email"), "", false)))
                            Names.AddRange(ModBase.Setup.Get("Login" + Token + "Email").ToString().Split("¨"));
                        if (Conversions.ToBoolean(!Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("Login" + Token + "Pass"), "", false)))
                            Passs.AddRange(ModBase.Setup.Get("Login" + Token + "Pass").ToString().Split("¨"));
                        for (int i = 0, loopTo = Names.Count - 1; i <= loopTo; i++)
                            Dict.Add(Names[i], Passs[i]);
                        Dict.Remove(Conversions.ToString(((dynamic)sender).Tag));
                        ModBase.Setup.Set("Login" + Token + "Email", Dict.Keys.Join("¨"));
                        ModBase.Setup.Set("Login" + Token + "Pass", Dict.Values.Join("¨"));
                        switch (Token ?? "")
                        {
                            case "Nide":
                                {
                                    ModMain.FrmLoginNide.ComboName.ItemsSource = Dict.Keys;
                                    ModMain.FrmLoginNide.ComboName.Text = Dict.Keys.Any() ? Dict.Keys.ElementAtOrDefault(0) : "";
                                    ModMain.FrmLoginNide.TextPass.Password = Dict.Values.Any() ? Dict.Values.ElementAtOrDefault(0) : "";
                                    break;
                                }
                            case "Auth":
                                {
                                    ModMain.FrmLoginAuth.ComboName.ItemsSource = Dict.Keys;
                                    ModMain.FrmLoginAuth.ComboName.Text = Dict.Keys.Any() ? Dict.Keys.ElementAtOrDefault(0) : "";
                                    ModMain.FrmLoginAuth.TextPass.Password = Dict.Values.Any() ? Dict.Values.ElementAtOrDefault(0) : "";
                                    break;
                                }
                        }

                        break;
                    }
            }
        }

        public static List<Border> ShowingTooltips = new List<Border>();
        private void TooltipLoaded(Border sender, EventArgs e)
        {
            ShowingTooltips.Add(sender);
        }
        private void TooltipUnloaded(Border sender, RoutedEventArgs e)
        {
            ShowingTooltips.Remove(sender);
        }

    }
}