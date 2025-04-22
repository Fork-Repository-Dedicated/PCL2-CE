using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class FormMain
    {

        #region 基础

        // 更新日志
        private void ShowUpdateLog(int LastVersion)
        {
            int FeatureCount = 0;
            int BugCount = 0;
            var FeatureList = new List<KeyValuePair<int, string>>();
            // 统计更新日志条目
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If RELEASE Then
            *//* TODO ERROR: Skipped DisabledTextTrivia
                    If LastVersion < 366 Then '2.10.6
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "迁移配置文件，不再使用注册表，使用 AES 加密部分信息"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "改进了 SMTC 获取信息的方式"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "尝试改进了实时日志的性能"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复 修改版本界面可能会因不支持的 Mod 加载器报错"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "针对不兼容的系统环境给出环境警告"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复 版本修改的加载器选择可能不正确"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "部分字体标题栏 CE 显示不完整"))
                    End If
                    If LastVersion < 365 Then '2.10.5
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "支持导出资源列表信息"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了深色模式下部分 UI 表现错误的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了实时日志大量内容会假死的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了导出整合包没有下载正式版 CE 的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "同步上游 2.9.2 更新内容"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "支持切换字体"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "统一修改页面与下载页面的 UI"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了 Mod 更新到错误版本的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了 Java 下拉框滑动问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了版本修改功能无法使用的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "Cleanroom 错误提示可添加 OptiFine"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "更新日志写错"))
                    End If
                    If LastVersion < 363 Then '2.10.4
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "统一了模组、资源包和光影的管理界面"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了未使用 Java 高性能选项的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了实时日志部分颜色不正确的问题"))
                    End If
                    If LastVersion < 360 Then '2.10.3
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "支持多收藏夹，允许批量下载和分享"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "添加了更新通道机制"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "添加了国内本体更新和公告服务器，感谢 @pysio2007"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "更改 Java Wrapper 启用机制"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复 Authlib 验证可能失败的问题"))
                        FeatureCount += 5
                        BugCount += 7
                    End If
                    If LastVersion < 357 Then
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复第三方登录无效会话问题"))
                        BugCount += 1
                    End If
                    If LastVersion < 356 Then
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复正版验证无法正常进行的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "深色模式优化"))
                    End If
                    If LastVersion < 355 Then
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "同步官方版 2.8.13 代码，详情查阅龙猫专栏"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "已有版本支持自动安装修改"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "支持深色模式"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "播放音乐接入 SMTC，允许使用键盘等控制"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "支持下载更多远古版本 MC"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优先使用高性能显卡启动游戏"))
                        FeatureCount += 5
                        BugCount += 9
                    End If
                    If LastVersion < 354 Then
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "支持识别剪贴板资源链接并提示跳转"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "允许禁用 Java Wrapper"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复版本设置查看截图可能崩溃的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复本体更新可能失败的问题"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "添加 Java 细致搜索开关"))
                        FeatureCount += 7
                        BugCount += 4
                    End If
                    If LastVersion < 353 Then
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "放弃对 Windows 10 1607 以下版本系统的支持 - 前期准备"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化关于页面，查看源代码按钮可以精确到具体提交了"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "允许指定 HTTP 代理"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "百宝箱支持清理游戏垃圾"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "标题栏添加社区版标识"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "反馈链接修改为社区版仓库链接"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复语言标头未遵循规范的问题"))
                        FeatureCount += 4
                        BugCount += 2
                    End If
                    If LastVersion < 352 Then 'Release 2.9.3
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "完整支持 LittleSkin OAuth 登录"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "恢复了百宝箱的部分内容"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "修复了 WebP 图片无法加载问题"))
                        FeatureCount += 6
                        BugCount += 4
                    End If
                    If LastVersion < 350 Then 'Release 2.9.2
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "支持下载资源包和光影包"))
                    End If
                    If LastVersion < 349 Then 'Release 2.9.1
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "添加了本体更新（实验性）"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "关于页面新增了详细的版本信息"))
                    End If
                    If LastVersion < 347 Then 'Release 2.8.12
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "Mod 管理页面添加下载 Mod、安装 Mod 选项"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "Mod 详情页面支持按加载器、游戏版本进行分类和筛选"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "支持安装同时包含 modpack 文件和启动器的懒人包"))
                        FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "优化整合包导入流程"))
                        FeatureCount += 43
                        BugCount += 37
                    End If
            *//* TODO ERROR: Skipped ElseDirectiveTrivia
            #Else
            */        // 5：          FEAT+
                      // 4：     IMP+ FEAT*
                      // 3：BUG+ IMP* FEAT-
                      // 2：BUG* IMP-
                      // 1：BUG-
            if (LastVersion < 367) // 2.10.7
            {
                FeatureList.Add(new KeyValuePair<int, string>(3, "支持 Mod 列表多选收藏"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "修复 资源管理查看存档崩溃"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "更新 UVMC 服务器地址"));
            }
            if (LastVersion < 366) // 2.10.6
            {
                FeatureList.Add(new KeyValuePair<int, string>(4, "迁移配置文件，不再使用注册表，使用 AES 加密部分信息"));
                FeatureList.Add(new KeyValuePair<int, string>(4, "改进了 SMTC 获取信息的方式"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "尝试改进了实时日志的性能"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "修复 修改版本界面可能会因不支持的 Mod 加载器报错"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "针对不兼容的系统环境给出环境警告"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "修复 版本修改的加载器选择可能不正确"));
                FeatureList.Add(new KeyValuePair<int, string>(1, "部分字体标题栏 CE 显示不完整"));
            }
            if (LastVersion < 365) // 2.10.5 365
            {
                FeatureList.Add(new KeyValuePair<int, string>(5, "支持导出资源列表信息"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了深色模式下部分 UI 表现错误的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了实时日志大量内容会假死的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了导出整合包没有下载正式版 CE 的问题"));
            }
            if (LastVersion < 364) // 2.10.5 364
            {
                FeatureList.Add(new KeyValuePair<int, string>(5, "同步上游 2.9.2 更新内容"));
                FeatureList.Add(new KeyValuePair<int, string>(5, "支持切换字体"));
                FeatureList.Add(new KeyValuePair<int, string>(4, "统一修改页面与下载页面的 UI"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了 Mod 更新到错误版本的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了 Java 下拉框滑动问题"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了版本修改功能无法使用的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "Cleanroom 错误提示可添加 OptiFine"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "更新日志写错"));
            }
            if (LastVersion < 363) // 2.10.4 363
            {
                FeatureList.Add(new KeyValuePair<int, string>(5, "统一了模组、资源包和光影的管理界面"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了未使用 Java 高性能选项的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了实时日志部分颜色不正确的问题"));
            }
            if (LastVersion < 362) // 2.10.4 @ 2025.03.08 20:05
            {
                FeatureList.Add(new KeyValuePair<int, string>(5, "同步上游 2.9.1 更新内容"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "新功能 - 支持简单的 Mod 等资源查重"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "新功能 - 允许给收藏夹内的项目添加备注"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "其他 - 加了个彩蛋"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "修复 - 切换了收藏夹依然保留了选中状态"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "修复 - 部分 MC 版本的显示更新日志按钮不正常工作"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "优化 - 砍掉了版本修改的动画"));
                FeatureCount += 5;
                BugCount += 2;
            }
            if (LastVersion < 361) // 2.10.4 @ 2025.02.22 23:50
            {
                FeatureList.Add(new KeyValuePair<int, string>(4, "Cleanroom 自动安装与相关支持"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了始终校验 Libraries 的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "支持检查 Quilt Mod 更新"));
                FeatureCount += 2;
            }
            if (LastVersion < 360)
            {
                FeatureList.Add(new KeyValuePair<int, string>(5, "支持多收藏夹，允许批量下载和分享"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "更改 Java Wrapper 启用机制"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复无法启动游戏的问题"));
            }
            if (LastVersion < 358)
            {
                FeatureList.Add(new KeyValuePair<int, string>(4, "添加了更新通道机制"));
                FeatureList.Add(new KeyValuePair<int, string>(4, "添加了国内本体更新和公告服务器，感谢 @pysio2007"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "Authlib 验证可能失败"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "自动安装不提示 Quilt 与 OptiFine 不兼容"));
                FeatureCount += 4;
                BugCount += 7;
            }
            if (LastVersion < 357)
            {
                FeatureList.Add(new KeyValuePair<int, string>(1, "修复第三方登录无效会话问题"));
                BugCount += 1;
            }
            if (LastVersion < 356)
            {
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复正版验证无法正常进行的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "深色模式优化"));
                FeatureCount += 1;
                BugCount += 1;
            }
            if (LastVersion < 355)
            {
                FeatureList.Add(new KeyValuePair<int, string>(5, "同步官方版 2.8.13 代码，详情查阅龙猫专栏"));
                FeatureList.Add(new KeyValuePair<int, string>(5, "已有版本支持自动安装修改"));
                FeatureList.Add(new KeyValuePair<int, string>(5, "支持深色模式"));
                FeatureList.Add(new KeyValuePair<int, string>(4, "播放音乐接入 SMTC，允许使用键盘等控制"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "支持下载更多远古版本 MC"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "优先使用高性能显卡启动游戏"));
                FeatureCount += 5;
                BugCount += 9;
            }
            if (LastVersion < 354)
            {
                FeatureList.Add(new KeyValuePair<int, string>(4, "支持识别剪贴板资源链接并提示跳转"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "允许禁用 Java Wrapper"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复版本设置查看截图可能崩溃的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复本体更新可能失败的问题"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "添加 Java 细致搜索开关"));
                FeatureCount += 7;
                BugCount += 4;
            }
            if (LastVersion < 353)
            {
                FeatureList.Add(new KeyValuePair<int, string>(4, "放弃对 Windows 10 1607 以下版本系统的支持 - 前期准备"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "优化关于页面，查看源代码按钮可以精确到具体提交了"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "允许指定 HTTP 代理"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "百宝箱支持清理游戏垃圾"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "标题栏添加社区版标识"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "反馈链接修改为社区版仓库链接"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "修复语言标头未遵循规范的问题"));
                FeatureCount += 4;
                BugCount += 2;
            }
            if (LastVersion < 352) // Snapshot 2.9.3
            {
                FeatureList.Add(new KeyValuePair<int, string>(1, "完整支持 LittleSkin OAuh 登录"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "恢复了百宝箱的部分内容"));
                FeatureList.Add(new KeyValuePair<int, string>(3, "修复了 WebP 图片无法加载问题"));
                FeatureCount += 6;
                BugCount += 4;
            }
            if (LastVersion < 350) // Snapshot 2.9.2
            {
                FeatureList.Add(new KeyValuePair<int, string>(5, "支持下载资源包和光影包"));
            }
            if (LastVersion < 349) // Snapshot 2.9.1
            {
                FeatureList.Add(new KeyValuePair<int, string>(4, "添加了本体更新（实验性）"));
                FeatureList.Add(new KeyValuePair<int, string>(2, "关于页面新增了详细的版本信息"));
            }
            if (LastVersion < 346) // Snapshot 2.8.12
            {
                if (LastVersion == 345)
                    FeatureList.Add(new KeyValuePair<int, string>(1, "修复帮助页面报错的 Bug"));
            }
            /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */        // 整理更新日志文本
            var ContentList = new List<string>();
            var SortedFeatures = FeatureList.OrderByDescending(f => f.Key).ToList();
            if (!SortedFeatures.Any() && FeatureCount == 0 && BugCount == 0)
                ContentList.Add("开发团队忘记写更新日志啦！可以去提醒一下……");
            for (int i = 0, loopTo = Math.Min(9, SortedFeatures.Count - 1); i <= loopTo; i++) // 最多取 10 项
                ContentList.Add(SortedFeatures[i].Value);
            if (SortedFeatures.Count > 10)
                FeatureCount += SortedFeatures.Count - 10;
            if (FeatureCount > 0 || BugCount > 0)
            {
                ContentList.Add((FeatureCount > 0 ? FeatureCount + " 项小调整与修改" : "") + (FeatureCount > 0 && BugCount > 0 ? "，" : "") + (BugCount > 0 ? "修复了 " + BugCount + " 个 Bug" : "") + "，详见完整更新日志");
            }
            string Content = "· " + ContentList.Join(Constants.vbCrLf + "· ");
            // 输出更新日志
            ModBase.RunInNewThread(() => { if (ModMain.MyMsgBox(Content, "PCL CE 已更新至 " + ModBase.VersionBranchName + " " + ModBase.VersionBaseName, "确定", "完整更新日志") == 2) { ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases"); } }, "UpdateLog Output");
        }

        // 窗口加载
        private bool IsWindowLoadFinished = false;
        public FormMain()
        {
            ModBase.ApplicationStartTick = ModBase.GetTimeTick();
            // 窗体参数初始化
            ModMain.FrmMain = this;
            ModMain.FrmLaunchLeft = new PageLaunchLeft();
            ModMain.FrmLaunchRight = new PageLaunchRight();
            // 版本号改变
            int LastVersion = Conversions.ToInteger(ModBase.Setup.Get("SystemLastVersionReg"));
            if (LastVersion < ModBase.VersionCode)
            {
                // 触发升级
                UpgradeSub(LastVersion);
            }
            else if (LastVersion > ModBase.VersionCode)
            {
                // 触发降级
                DowngradeSub(LastVersion);
            }
            // 版本隔离设置迁移
            if (ModBase.Setup.IsUnset("LaunchArgumentIndieV2"))
            {
                if (!ModBase.Setup.IsUnset("LaunchArgumentIndie"))
                {
                    ModBase.Log("[Start] 从老 PCL 迁移版本隔离");
                    ModBase.Setup.Set("LaunchArgumentIndieV2", ModBase.Setup.Get("LaunchArgumentIndie"));
                }
                else if (!ModBase.Setup.IsUnset("WindowHeight"))
                {
                    ModBase.Log("[Start] 从老 PCL 升级，但此前未调整版本隔离，使用老的版本隔离默认值");
                    ModBase.Setup.Set("LaunchArgumentIndieV2", ModBase.Setup.GetDefault("LaunchArgumentIndie"));
                }
                else
                {
                    ModBase.Log("[Start] 全新的 PCL，使用新的版本隔离默认值");
                    ModBase.Setup.Set("LaunchArgumentIndieV2", ModBase.Setup.GetDefault("LaunchArgumentIndieV2"));
                }
            }
            // 刷新主题
            ModSecret.ThemeCheckAll(false);
            ModBase.Setup.Load("UiLauncherTheme");
            // 加载 UI
            this.InitializeComponent();
            this.Opacity = 0d;
            // '开启管理员权限下的文件拖拽，但下列代码也没用（#2531）
            // If IsAdmin() Then
            // Log("[Start] PCL 正以管理员权限运行")
            // ChangeWindowMessageFilter(&H233, 1)
            // ChangeWindowMessageFilter(&H4A, 1)
            // ChangeWindowMessageFilter(&H49, 1)
            // End If
            // 切换到首页
            if (!(ModMain.FrmLaunchLeft.Parent == null))
                ModMain.FrmLaunchLeft.SetValue(ContentPresenter.ContentProperty, (object)null);
            if (!(ModMain.FrmLaunchRight.Parent == null))
                ModMain.FrmLaunchRight.SetValue(ContentPresenter.ContentProperty, (object)null);
            this.PanMainLeft.Child = ModMain.FrmLaunchLeft;
            PageLeft = ModMain.FrmLaunchLeft;
            this.PanMainRight.Child = ModMain.FrmLaunchRight;
            PageRight = ModMain.FrmLaunchRight;
            ModMain.FrmLaunchRight.PageState = MyPageRight.PageStates.ContentStay;
            // 模式提醒
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If DEBUG Then
            *//* TODO ERROR: Skipped DisabledTextTrivia
                    Hint("[开发者模式] PCL 正以开发者模式运行，这可能会造成严重的性能下降，请务必立即向开发者反馈此问题！", HintType.Critical)
            *//* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
            if (ModBase.ModeDebug)
                ModMain.Hint("[调试模式] PCL 正以调试模式运行，这可能会导致性能下降，若无必要请不要开启！");
            // 尽早执行的加载池
            ModMinecraft.McFolderListLoader.Start(0); // 为了让下载已存在文件检测可以正常运行，必须跑一次；为了让启动按钮尽快可用，需要尽早执行；为了与 PageLaunchLeft 联动，需要为 0 而不是 GetUuid

            ModBase.Log("[Start] 第二阶段加载用时：" + (ModBase.GetTimeTick() - ModBase.ApplicationStartTick) + " ms");
            this.Loaded += FormMain_Loaded;
            this.Closing += FormMain_Closing;
            this.SizeChanged += (_, __) => FormMain_SizeChanged();
            this.Loaded += (_, __) => FormMain_SizeChanged();
            this.KeyDown += FormMain_KeyDown;
            this.MouseDown += FormMain_MouseDown;
            this.Activated += (_, __) => FormMain_Activated();
            this.PreviewDragOver += FrmMain_PreviewDragOver;
            this.PreviewDrop += FrmMain_Drop;
            this.MouseMove += FormMain_MouseMove;
        }
        private void FormMain_Loaded(object sender, RoutedEventArgs e)
        {
            ModBase.ApplicationStartTick = ModBase.GetTimeTick();
            ModBase.Handle = new WindowInteropHelper(this).Handle;
            // 读取设置
            ModBase.Setup.Load("UiBackgroundOpacity");
            ModBase.Setup.Load("UiBackgroundBlur");
            ModBase.Setup.Load("UiLogoType");
            ModBase.Setup.Load("UiHiddenPageDownload");
            PageSetupUI.BackgroundRefresh(false, true);
            ModMusic.MusicRefreshPlay(false, true);
            // 扩展按钮
            this.BtnExtraDownload.ShowCheck = this.BtnExtraDownload_ShowCheck;
            this.BtnExtraBack.ShowCheck = this.BtnExtraBack_ShowCheck;
            this.BtnExtraApril.ShowCheck = this.BtnExtraApril_ShowCheck;
            this.BtnExtraShutdown.ShowCheck = this.BtnExtraShutdown_ShowCheck;
            this.BtnExtraLog.ShowCheck = this.BtnExtraLog_ShowCheck;
            this.BtnExtraApril.ShowRefresh();
            // 初始化尺寸改变
            var Resizer = new MyResizer(this);
            Resizer.addResizerDown(this.ResizerB);
            Resizer.addResizerLeft(this.ResizerL);
            Resizer.addResizerLeftDown(this.ResizerLB);
            Resizer.addResizerLeftUp(this.ResizerLT);
            Resizer.addResizerRight(this.ResizerR);
            Resizer.addResizerRightDown(this.ResizerRB);
            Resizer.addResizerRightUp(this.ResizerRT);
            Resizer.addResizerUp(this.ResizerT);
            // PLC 彩蛋
            if (ModBase.RandomInteger(1, 1000) == 233)
            {
                this.ShapeTitleLogo.Data = (Geometry)new GeometryConverter().ConvertFromString("M26,29 v-25 h5 a7,7 180 0 1 0,14 h-5 M80,6.5 a10,11.5 180 1 0 0,18   M47,2.5 v24.5 h12   M98,2 v27   M107,2 v27");
            }
            // 加载窗口
            int dark = Conversions.ToInteger(ModBase.Setup.Get("UiDarkMode"));
            switch (dark)
            {
                case 0:
                    {
                        ModSecret.IsDarkMode = false;
                        break;
                    }
                case 1:
                    {
                        ModSecret.IsDarkMode = true;
                        break;
                    }
                case 2:
                    {
                        ModSecret.IsDarkMode = ModBase.IsSystemInDarkMode();
                        break;
                    }
            }

            ModSecret.ThemeRefresh();
            try
            {
                this.Height = Conversions.ToDouble(ModBase.Setup.Get("WindowHeight"));
                this.Width = Conversions.ToDouble(ModBase.Setup.Get("WindowWidth"));
            }
            catch (Exception ex) // 修复 #2019
            {
                ModBase.Log(ex, "读取窗口默认大小失败", ModBase.LogLevel.Hint);
                this.Height = this.MinHeight + 100d;
                this.Width = this.MinWidth + 100d;
            }
            // #If DEBUG Then
            // MinHeight = 50
            // MinWidth = 50
            // #End If
            this.Topmost = false;
            if (ModMain.FrmStart is not null)
                ModMain.FrmStart.Close(new TimeSpan(0, 0, 0, 0, (int)Math.Round(400d / ModAnimation.AniSpeed)));
            // 更改窗口
            this.Top = (ModBase.GetWPFSize(My.MyWpfExtension.Computer.Screen.WorkingArea.Height) - this.Height) / 2d;
            this.Left = (ModBase.GetWPFSize(My.MyWpfExtension.Computer.Screen.WorkingArea.Width) - this.Width) / 2d;
            IsSizeSaveable = true;
            ShowWindowToTop();
            HwndSource HwndSource = (HwndSource)PresentationSource.FromVisual(this);
            HwndSource.AddHook(new HwndSourceHook(WndProc));
            ModAnimation.AniStart(new[] {
            ModAnimation.AaCode(() => ModAnimation.AniControlEnabled -= 1, 50),
            ModAnimation.AaOpacity(this, Conversions.ToDouble(Operators.AddObject(Operators.DivideObject(ModBase.Setup.Get("UiLauncherTransparent"), 1000), 0.4d)), 250, 100),
            ModAnimation.AaDouble(i => this.TransformPos.Y = Conversions.ToDouble(this.TransformPos.Y + i), -this.TransformPos.Y, 600, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaDouble(i => this.TransformRotate.Angle = Conversions.ToDouble(this.TransformRotate.Angle + i), -this.TransformRotate.Angle, 500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaCode(() =>
                {
                this.PanBack.RenderTransform = (Transform)null;
                IsWindowLoadFinished = true;
                ModBase.Log($"[System] DPI：{ModBase.DPI}，系统版本：{Environment.OSVersion.VersionString}，PCL 位置：{ModBase.PathWithName}");
            }, After: true)
        }, "Form Show");
            // Timer 启动
            ModAnimation.AniStart();
            ModMain.TimerMainStart();
            // 加载池
            ModBase.RunInNewThread(() =>
        {
            // EULA 提示
            if (Conversions.ToBoolean(!ModBase.Setup.Get("SystemEula")))
            {
                switch (ModMain.MyMsgBox("在使用 PCL 前，请同意 PCL 的用户协议与免责声明。", "协议授权", "同意", "拒绝", "查看用户协议与免责声明", Button3Action: () => ModBase.OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW")))
                {
                    case 1:
                        {
                            ModBase.Setup.Set("SystemEula", true);
                            break;
                        }
                    case 2:
                        {
                            EndProgram(false);
                            break;
                        }
                }
            }
            // 启动加载器池
            try
            {
                ModJava.JavaListInit(); // 延后到同意协议后再执行，避免在初次启动时进行进程操作
                Thread.Sleep(200);
                ModDownload.DlClientListMojangLoader.Start(1);
                RunCountSub();
                ModSecret.ServerLoader.Start(1);
                ModBase.RunInNewThread(ModMain.TryClearTaskTemp, "TryClearTaskTemp", ThreadPriority.BelowNormal);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化加载池运行失败", ModBase.LogLevel.Feedback);
            }
            // 清理自动更新文件
            try
            {
                if (File.Exists(ModBase.Path + @"PCL\Plain Craft Launcher 2.exe"))
                    File.Delete(ModBase.Path + @"PCL\Plain Craft Launcher 2.exe");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "清理自动更新文件失败");
            }
        }, "Start Loader", ThreadPriority.Lowest);
            // 剪贴板识别
            if (Conversions.ToBoolean(ModBase.Setup.Get("ToolDownloadClipboard")))
                ModBase.RunInNewThread(() => ModComp.CompClipboard.ClipboardListening(), "Clipboard Listener", ThreadPriority.Lowest);

            ModBase.Log("[Start] 第三阶段加载用时：" + (ModBase.GetTimeTick() - ModBase.ApplicationStartTick) + " ms");
        }
        // 根据打开次数触发的事件
        private void RunCountSub()
        {
            ModBase.Setup.Set("SystemCount", Operators.AddObject(ModBase.Setup.Get("SystemCount"), 1));
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If Not BETA Then
            */
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectGreaterEqual(ModBase.Setup.Get("SystemCount"), 99, false)))
            {
                if (ModSecret.ThemeUnlock(6, false))
                {
                    ModMain.MyMsgBox("你已经使用了 99 次 PCL 啦，感谢你长期以来的支持！" + Constants.vbCrLf + "隐藏主题 铁杆粉 已解锁！", "提示");
                }
            }
            /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
        }
        // 升级与降级事件
        private void UpgradeSub(int LastVersionCode)
        {
            ModBase.Log("[Start] 版本号从 " + LastVersionCode + " 升高到 " + ModBase.VersionCode);
            ModBase.Setup.Set("SystemLastVersionReg", ModBase.VersionCode);
            // 检查有记录的最高版本号
            int LowerVersionCode;
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If BETA Then
            *//* TODO ERROR: Skipped DisabledTextTrivia
                    LowerVersionCode = Setup.Get("SystemHighestBetaVersionReg")
                    If LowerVersionCode < VersionCode Then
                        Setup.Set("SystemHighestBetaVersionReg", VersionCode)
                        Log("[Start] 最高版本号从 " & LowerVersionCode & " 升高到 " & VersionCode)
                    End If
            *//* TODO ERROR: Skipped ElseDirectiveTrivia
            #Else
            */
            LowerVersionCode = Conversions.ToInteger(ModBase.Setup.Get("SystemHighestAlphaVersionReg"));
            if (LowerVersionCode < ModBase.VersionCode)
            {
                ModBase.Setup.Set("SystemHighestAlphaVersionReg", ModBase.VersionCode);
                ModBase.Log("[Start] 最高版本号从 " + LowerVersionCode + " 升高到 " + ModBase.VersionCode);
            }
            /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */        // 被移除的窗口设置选项
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchArgumentWindowType"), 5, false)))
                ModBase.Setup.Set("LaunchArgumentWindowType", 1);
            // 修改主题设置项名称
            if (LowerVersionCode <= 207)
            {
                var UnlockedTheme = new List<string>() { "2" };
                UnlockedTheme.AddRange(new List<string>(ModBase.Setup.Get("UiLauncherThemeHide").ToString().Split("|")));
                UnlockedTheme.AddRange(new List<string>(ModBase.Setup.Get("UiLauncherThemeHide2").ToString().Split("|")));
                ModBase.Setup.Set("UiLauncherThemeHide2", UnlockedTheme.Distinct().ToList().Join("|"));
            }
            // 重置欧皇彩
            if (LastVersionCode <= 115 && ModBase.Setup.Get("UiLauncherThemeHide2").ToString().Split("|").Contains("13"))
            {
                var UnlockedTheme = new List<string>(ModBase.Setup.Get("UiLauncherThemeHide2").ToString().Split("|"));
                UnlockedTheme.Remove("13");
                ModBase.Setup.Set("UiLauncherThemeHide2", UnlockedTheme.Join("|"));
                ModMain.MyMsgBox("由于新版 PCL 修改了欧皇彩的解锁方式，你需要重新解锁欧皇彩。" + Constants.vbCrLf + "多谢各位的理解啦！", "重新解锁提醒");
            }
            // 重置滑稽彩
            if (LastVersionCode <= 152 && ModBase.Setup.Get("UiLauncherThemeHide2").ToString().Split("|").Contains("12"))
            {
                var UnlockedTheme = new List<string>(ModBase.Setup.Get("UiLauncherThemeHide2").ToString().Split("|"));
                UnlockedTheme.Remove("12");
                ModBase.Setup.Set("UiLauncherThemeHide2", UnlockedTheme.Join("|"));
                ModMain.MyMsgBox("由于新版 PCL 修改了滑稽彩的解锁方式，你需要重新解锁滑稽彩。" + Constants.vbCrLf + "多谢各位的理解啦！", "重新解锁提醒");
            }
            // 移动自定义皮肤
            if (LastVersionCode <= 161 && File.Exists(ModBase.Path + @"PCL\CustomSkin.png") && !File.Exists(ModBase.PathAppdata + "CustomSkin.png"))
            {
                ModBase.CopyFile(ModBase.Path + @"PCL\CustomSkin.png", ModBase.PathAppdata + "CustomSkin.png");
                ModBase.Log("[Start] 已移动离线自定义皮肤 (162)");
            }
            if (LastVersionCode <= 263 && File.Exists(ModBase.PathTemp + "CustomSkin.png") && !File.Exists(ModBase.PathAppdata + "CustomSkin.png"))
            {
                ModBase.CopyFile(ModBase.PathTemp + "CustomSkin.png", ModBase.PathAppdata + "CustomSkin.png");
                ModBase.Log("[Start] 已移动离线自定义皮肤 (264)");
            }
            // 解除帮助页面的隐藏
            if (LastVersionCode <= 205)
            {
                ModBase.Setup.Set("UiHiddenOtherHelp", false);
                ModBase.Log("[Start] 已解除帮助页面的隐藏");
            }
            // 单向迁移微软登录结果（#4836）
            if (Conversions.ToBoolean(!ModBase.Setup.Get("CacheMsV2Migrated")))
            {
                ModBase.Setup.Set("CacheMsV2Migrated", true);
                ModBase.Setup.Set("CacheMsV2OAuthRefresh", ModBase.Setup.Get("CacheMsOAuthRefresh"));
                ModBase.Setup.Set("CacheMsV2Access", ModBase.Setup.Get("CacheMsAccess"));
                ModBase.Setup.Set("CacheMsV2ProfileJson", ModBase.Setup.Get("CacheMsProfileJson"));
                ModBase.Setup.Set("CacheMsV2Uuid", ModBase.Setup.Get("CacheMsUuid"));
                ModBase.Setup.Set("CacheMsV2Name", ModBase.Setup.Get("CacheMsName"));
                ModBase.Log("[Start] 已从老版本迁移微软登录结果");
            }
            // Mod 命名设置迁移
            if (!ModBase.Setup.IsUnset("ToolDownloadTranslate") && ModBase.Setup.IsUnset("ToolDownloadTranslateV2"))
            {
                ModBase.Setup.Set("ToolDownloadTranslateV2", Operators.AddObject(ModBase.Setup.Get("ToolDownloadTranslate"), 1));
                ModBase.Log("[Start] 已从老版本迁移 Mod 命名设置");
            }
            // 社区版提示
            if (Conversions.ToBoolean(!ModBase.Setup.Get("UiLauncherCEHint")))
                ModSecret.ShowCEAnnounce(true);
            // 输出更新日志
            if (LastVersionCode <= 0)
                return;
            if (LowerVersionCode >= ModBase.VersionCode)
                return;
            ShowUpdateLog(LowerVersionCode);
        }
        private void DowngradeSub(int LastVersionCode)
        {
            ModBase.Log("[Start] 版本号从 " + LastVersionCode + " 降低到 " + ModBase.VersionCode);
            ModBase.Setup.Set("SystemLastVersionReg", ModBase.VersionCode);
        }

        #endregion

        #region 自定义窗口

        // 硬件加速
        protected override void OnSourceInitialized(EventArgs e)
        {
            if (Conversions.ToBoolean(ModBase.Setup.Get("SystemDisableHardwareAcceleration")))
            {
                HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;
                if (hwndSource is not null)
                {
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                }
            }
            base.OnSourceInitialized(e);
        }

        // 关闭
        private void FormMain_Closing(object sender, CancelEventArgs e)
        {
            EndProgram(true);
            e.Cancel = true;
        }
        /// <summary>
    /// 正常关闭程序。程序将在执行此方法后约 0.3s 退出。
    /// </summary>
    /// <param name="SendWarning">是否在还有下载任务未完成时发出警告。</param>
        public void EndProgram(bool SendWarning)
        {
            // 发出警告
            if (SendWarning && ModNet.HasDownloadingTask())
            {
                if (ModMain.MyMsgBox("还有下载任务尚未完成，是否确定退出？", "提示", "确定", "取消") == 1)
                {
                    // 强行结束下载任务
                    ModBase.RunInNewThread(() =>
        {
            ModBase.Log("[System] 正在强行停止任务");
            foreach (ModLoader.LoaderBase Task in ModLoader.LoaderTaskbar.ToList())
                Task.Abort();
        }, "强行停止下载任务");
                }
                else
                {
                    return;
                }
            }
            // 关闭 EasyTier 联机
            if (ModLink.IsETRunning)
                ModLink.ExitEasyTier();
            // 关闭
            ModBase.RunInUiWait(() =>
        {
            this.IsHitTestVisible = false;
            if (this.PanBack.RenderTransform is null)
            {
                var TransformPos = new TranslateTransform(0d, 0d);
                var TransformRotate = new RotateTransform(0d);
                var TransformScale = new ScaleTransform(1d, 1d);
                this.PanBack.RenderTransform = new TransformGroup() { Children = new TransformCollection(new[] { TransformRotate, TransformPos, TransformScale }) };
                ModAnimation.AniStart(new[] {
                    ModAnimation.AaOpacity(this, -this.Opacity, 140, 40, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                                        ModAnimation.AaDouble(i =>
                        {
                        TransformScale.ScaleX = Conversions.ToDouble(TransformScale.ScaleX + i);
                        TransformScale.ScaleY = Conversions.ToDouble(TransformScale.ScaleY + i);
                    }, 0.88d - TransformScale.ScaleX, 180),
                    ModAnimation.AaDouble(i => TransformPos.Y = Conversions.ToDouble(TransformPos.Y + i), 20d - TransformPos.Y, 180, 0, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle = Conversions.ToDouble(TransformRotate.Angle + i), 0.6d - TransformRotate.Angle, 180, 0, new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)),
                                        ModAnimation.AaCode(() =>
                        {
                        this.IsHitTestVisible = false;
                        this.Top = (double)-10000;
                        this.ShowInTaskbar = false;
                    }, 210),
                    ModAnimation.AaCode(() => EndProgramForce(), 230)
            }, "Form Close");
            }
            else
            {
                EndProgramForce();
            }
            ModBase.Log("[System] 收到关闭指令");
        });
        }
        private static bool IsLogShown = false;
        public static void EndProgramForce(ModBase.ProcessReturnValues ReturnCode = ModBase.ProcessReturnValues.Success)
        {
            ;
            // 关闭 EasyTier 联机
#error Cannot convert OnErrorResumeNextStatementSyntax - see comment for details
            /* Cannot convert OnErrorResumeNextStatementSyntax, CONVERSION ERROR: Conversion for OnErrorResumeNextStatement not implemented, please report this issue in 'On Error Resume Next' at character 39968


                        Input:
                                On Error Resume Next

                         */
            if (ModLink.IsETRunning)
                ModLink.ExitEasyTier();
            ModBase.IsProgramEnded = true;
            ModAnimation.AniControlEnabled += 1;
            if (ModSecret.IsUpdateWaitingRestart)
                ModSecret.UpdateRestart(false);
            if (ReturnCode == ModBase.ProcessReturnValues.Exception)
            {
                if (!IsLogShown)
                {
                    ModBase.FeedbackInfo();
                    ModBase.Log("请在 https://github.com/PCL-Community/PCL2-CE/issues 提交错误报告，以便于社区解决此问题！（这也有可能是原版 PCL 的问题）");
                    IsLogShown = true;
                    ModBase.ShellOnly(ModBase.Path + @"PCL\Log-CE1.log");
                }
                Thread.Sleep(500); // 防止 PCL 在记事本打开前就被掐掉
            }
            ModBase.Log("[System] 程序已退出，返回值：" + ModBase.GetStringFromEnum(ReturnCode));
            ModBase.LogFlush();
            if (ReturnCode == ModBase.ProcessReturnValues.Success)
            {
                Process.GetCurrentProcess().Kill();
            }
            else
            {
                Environment.Exit((int)ReturnCode);
                Process.GetCurrentProcess().Kill();
            }
        }
        private void BtnTitleClose_Click(object sender, RoutedEventArgs e)
        {
            EndProgram(true);
        }

        // 移动
        private void FormDragMove(object sender, MouseButtonEventArgs e)
        {
            ;
#error Cannot convert OnErrorResumeNextStatementSyntax - see comment for details
            /* Cannot convert OnErrorResumeNextStatementSyntax, CONVERSION ERROR: Conversion for OnErrorResumeNextStatement not implemented, please report this issue in 'On Error Resume Next' at character 41732


                        Input:
                                On Error Resume Next

                         */
            if (Conversions.ToBoolean(((dynamic)sender).IsMouseDirectlyOver))
                this.DragMove();
        }

        // 改变大小
        /// <summary>
    /// 是否可以向注册表储存尺寸改变信息。以此避免初始化时误储存。
    /// </summary>
        public bool IsSizeSaveable = false;
        private void FormMain_SizeChanged()
        {
            if (IsSizeSaveable)
            {
                ModBase.Setup.Set("WindowHeight", this.Height);
                ModBase.Setup.Set("WindowWidth", this.Width);
            }
            this.RectForm.Rect = new Rect(0d, 0d, this.BorderForm.ActualWidth, this.BorderForm.ActualHeight);
            this.PanForm.Width = this.BorderForm.ActualWidth + 0.001d;
            this.PanForm.Height = this.BorderForm.ActualHeight + 0.001d;
            this.PanMain.Width = this.PanForm.Width;
            this.PanMain.Height = Math.Max(0d, this.PanForm.Height - this.PanTitle.ActualHeight);
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal; // 修复 #1938
        }

        // 标题栏改变大小
        private void PanTitle_SizeChanged()
        {
            this.PanTitleLeft.ColumnDefinitions[0].MaxWidth = this.PanTitleMain.ColumnDefinitions[0].ActualWidth - 30d;
        }

        // 最小化
        private void BtnTitleMin_Click()
        {
            this.WindowState = WindowState.Minimized;
        }

        #endregion

        #region 窗体事件

        // 按键事件
        private void FormMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
                return;
            // 调用弹窗：回车选择第一个，Esc 选择最后一个
            if (this.PanMsg.Children.Count > 0)
            {
                if (e.Key == Key.Enter)
                {
                    ((object)this.PanMsg.Children[0]).Btn1_Click();
                    return;
                }
                else if (e.Key == Key.Escape)
                {
                    object Msg = this.PanMsg.Children[0];
                    if (!(Msg is MyMsgInput) && !(Msg is MyMsgSelect) && Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)Msg).Btn3.Visibility, Visibility.Visible, false)))
                    {
                        ((dynamic)Msg).Btn3_Click();
                    }
                    else if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)Msg).Btn2.Visibility, Visibility.Visible, false)))
                    {
                        ((dynamic)Msg).Btn2_Click();
                    }
                    else
                    {
                        ((dynamic)Msg).Btn1_Click();
                    }
                    return;
                }
            }
            // 按 ESC 返回上一级
            if (e.Key == Key.Escape)
                TriggerPageBack();
            // 更改隐藏版本可见性
            if (e.Key == Key.F11 && PageCurrent == (PageStackData)PageType.VersionSelect)
            {
                ModMain.FrmSelectRight.ShowHidden = !ModMain.FrmSelectRight.ShowHidden;
                ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                return;
            }
            // 更改功能隐藏可见性
            if (e.Key == Key.F12)
            {
                PageSetupUI.HiddenForceShow = !PageSetupUI.HiddenForceShow;
                if (PageSetupUI.HiddenForceShow)
                {
                    ModMain.Hint("功能隐藏设置已暂时关闭！", ModMain.HintType.Finish);
                }
                else
                {
                    ModMain.Hint("功能隐藏设置已重新开启！", ModMain.HintType.Finish);
                }
                PageSetupUI.HiddenRefresh();
                return;
            }
            // 按 F5 刷新页面
            if (e.Key == Key.F5)
            {
                if (PageLeft is IRefreshable)
                    ((IRefreshable)PageLeft).Refresh();
                if (PageRight is IRefreshable)
                    ((IRefreshable)PageRight).Refresh();
                return;
            }
            // 调用启动游戏
            if (e.Key == Key.Enter && PageCurrent == (PageStackData)PageType.Launch)
            {
                if (ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup)
                {
                    ModMain.Hint("木大！");
                }
                else
                {
                    ModMain.FrmLaunchLeft.LaunchButtonClick();
                }
            }
            // 修复按下 Alt 后误认为弹出系统菜单导致的冻结
            if (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt)
                e.Handled = true;
        }
        private void FormMain_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 鼠标侧键返回上一级
            if (ModMain.FrmMain.PanMsg.Children.Count > 0 || ModMain.WaitingMyMsgBox.Any())
                return; // 弹窗中（#5513）
            if (e.ChangedButton == MouseButton.XButton1 || e.ChangedButton == MouseButton.XButton2)
                TriggerPageBack();
        }
        private void TriggerPageBack()
        {
            if (PageCurrent == (PageStackData)PageType.Download && PageCurrentSub == PageSubType.DownloadInstall && ModMain.FrmDownloadInstall.IsInSelectPage)
            {
                ModMain.FrmDownloadInstall.ExitSelectPage();
            }
            else if (PageCurrent == (PageStackData)PageType.VersionSetup && PageCurrentSub == PageSubType.VersionInstall && ModMain.FrmVersionInstall.IsInSelectPage)
            {
                ModMain.FrmVersionInstall.ExitSelectPage();
            }
            else
            {
                PageBack();
            }
        }

        // 切回窗口
        private void FormMain_Activated()
        {
            try
            {
                if (PageCurrent == (PageStackData)PageType.VersionSetup && PageCurrentSub == PageSubType.VersionMod)
                {
                    // Mod 管理自动刷新
                    ModMain.FrmVersionMod.ReloadCompFileList();
                }
                else if (PageCurrent == (PageStackData)PageType.VersionSelect)
                {
                    // 版本选择自动刷新
                    ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.RunOnUpdated, MaxDepth: 1, ExtraPath: @"versions\");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "切回窗口时出错", ModBase.LogLevel.Feedback);
            }
        }

        // 文件拖放
        private void FrmMain_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetFormats().Contains("FileDrop"))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        private void FrmMain_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    // 获取文本
                    try
                    {
                        string Str = Conversions.ToString(e.Data.GetData(DataFormats.Text));
                        ModBase.Log("[System] 接受文本拖拽：" + Str);
                        if (Str.StartsWithF("authlib-injector:yggdrasil-server:"))
                        {
                            // Authlib 拖拽
                            e.Handled = true;
                            string AuthlibServer = WebUtility.UrlDecode(Str.Substring("authlib-injector:yggdrasil-server:".Length));
                            ModBase.Log("[System] Authlib 拖拽：" + AuthlibServer);
                            if (!string.IsNullOrEmpty(new ValidateHttp().Validate(AuthlibServer)))
                            {
                                ModMain.Hint($"输入的 Authlib 验证服务器不符合网址格式（{AuthlibServer}）！", ModMain.HintType.Critical);
                                return;
                            }
                            var TargetVersion = PageCurrent == (PageStackData)PageType.VersionSetup ? PageVersionLeft.Version : ModMinecraft.McVersionCurrent;
                            if (TargetVersion is null)
                            {
                                ModMain.Hint("请先下载游戏，再设置第三方登录！", ModMain.HintType.Critical);
                                return;
                            }
                            if (AuthlibServer == "https://littleskin.cn/api/yggdrasil")
                            {
                                // LittleSkin
                                if (ModMain.MyMsgBox($"是否要在版本 {TargetVersion.Name} 中开启 LittleSkin 登录？" + Constants.vbCrLf + "你可以在 版本设置 → 设置 → 服务器选项 中修改登录方式。", "第三方登录开启确认", "确定", "取消") == 2)
                                {
                                    return;
                                }
                                ModBase.Setup.Set("VersionServerLogin", 4, Version: TargetVersion);
                                ModBase.Setup.Set("VersionServerAuthServer", "https://littleskin.cn/api/yggdrasil", Version: TargetVersion);
                                ModBase.Setup.Set("VersionServerAuthRegister", "https://littleskin.cn/auth/register", Version: TargetVersion);
                                ModBase.Setup.Set("VersionServerAuthName", "LittleSkin 登录", Version: TargetVersion);
                            }
                            else
                            {
                                // 第三方 Authlib 服务器
                                if (ModMain.MyMsgBox($"是否要在版本 {TargetVersion.Name} 中开启第三方登录？" + Constants.vbCrLf + $"登录服务器：{AuthlibServer}" + Constants.vbCrLf + Constants.vbCrLf + "你可以在 版本设置 → 设置 → 服务器选项 中修改登录方式。", "第三方登录开启确认", "确定", "取消") == 2)
                                {
                                    return;
                                }
                                ModBase.Setup.Set("VersionServerLogin", 4, Version: TargetVersion);
                                ModBase.Setup.Set("VersionServerAuthServer", AuthlibServer, Version: TargetVersion);
                                ModBase.Setup.Set("VersionServerAuthRegister", AuthlibServer.Replace("api/yggdrasil", "auth/register"), Version: TargetVersion);
                                ModBase.Setup.Set("VersionServerAuthName", "", Version: TargetVersion);
                            }
                            if (PageCurrent == (PageStackData)PageType.VersionSetup && PageCurrentSub == PageSubType.VersionSetup)
                            {
                                // 正在服务器选项页，需要刷新设置项显示
                                ModMain.FrmVersionSetup.Reload();
                            }
                            else if (PageCurrent == (PageStackData)PageType.Launch)
                            {
                                // 正在主页，需要刷新左边栏
                                ModMain.FrmLaunchLeft.RefreshPage(true, false);
                            }
                        }
                        else if (Str.StartsWithF("file:///"))
                        {
                            // 文件拖拽（例如从浏览器下载窗口拖入）
                            string FilePath = WebUtility.UrlDecode(Str).Substring("file:///".Length).Replace("/", @"\");
                            e.Handled = true;
                            FileDrag(new List<string>() { FilePath });
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "无法接取文本拖拽事件", ModBase.LogLevel.Developer);
                        return;
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // 获取文件并检查
                    var FilePathRaw = e.Data.GetData(DataFormats.FileDrop);
                    if (FilePathRaw is null) // #2690
                    {
                        ModMain.Hint("请将文件解压后再拖入！", ModMain.HintType.Critical);
                        return;
                    }
                    e.Handled = true;
                    FileDrag((IEnumerable<string>)FilePathRaw);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "接取拖拽事件失败", ModBase.LogLevel.Feedback);
            }
        }
        private void FileDrag(IEnumerable<string> FilePathList)
        {
            ModBase.RunInNewThread(() =>
        {
            string FilePath = FilePathList.First();
            ModBase.Log("[System] 接受文件拖拽：" + FilePath + (FilePathList.Any() ? $" 等 {FilePathList.Count()} 个文件" : ""), ModBase.LogLevel.Developer);
            // 基础检查
            if (Directory.Exists(FilePathList.First()) && !File.Exists(FilePathList.First()))
            {
                ModMain.Hint("请拖入一个文件，而非文件夹！", ModMain.HintType.Critical);
                return;
            }
            else if (!File.Exists(FilePathList.First()))
            {
                ModMain.Hint("拖入的文件不存在：" + FilePathList.First(), ModMain.HintType.Critical);
                return;
            }
            // 多文件拖拽
            if (FilePathList.Count() > 1)
            {
                // 必须要求全部为 Jar 文件
                foreach (var File in FilePathList)
                {
                    if (!new[] { "jar", "litemod", "disabled", "old" }.Contains(File.AfterLast(".").ToLower()))
                    {
                        ModMain.Hint("一次请只拖入一个文件！", ModMain.HintType.Critical);
                        return;
                    }
                }
            }
            // 自定义主页
            string Extension = FilePath.AfterLast(".").ToLower();
            if (Extension == "xaml")
            {
                ModBase.Log("[System] 文件后缀为 XAML，作为自定义主页加载");
                if (File.Exists(ModBase.Path + @"PCL\Custom.xaml"))
                {
                    if (ModMain.MyMsgBox("已存在一个自定义主页文件，是否要将它覆盖？", "覆盖确认", "覆盖", "取消") == 2)
                    {
                        return;
                    }
                }
                ModBase.CopyFile(FilePath, ModBase.Path + @"PCL\Custom.xaml");
                ModBase.RunInUi(() =>
        {
                    ModBase.Setup.Set("UiCustomType", 1);
                    ModMain.FrmLaunchRight.ForceRefresh();
                    ModMain.Hint("已加载主页自定义文件！", ModMain.HintType.Finish);
                });
                return;
            }
            // 安装 Mod
            if (PageVersionCompResource.InstallMods(FilePathList))
                return;
            // 处理资源安装
            if (PageCurrent == (PageStackData)PageType.VersionSetup && new[] { "zip" }.Any(i => (i ?? "") == (Extension ?? "")))
            {
                switch (PageCurrentSub)
                {
                    case PageSubType.VersionWorld:
                        {
                            string DestFolder = PageVersionLeft.Version.PathIndie + @"saves\" + ModBase.GetFileNameWithoutExtentionFromPath(FilePath);
                            if (Directory.Exists(DestFolder))
                            {
                                ModMain.Hint("发现同名文件夹，无法粘贴：" + DestFolder, ModMain.HintType.Critical);
                                return;
                            }
                            ModBase.ExtractFile(FilePath, DestFolder);
                            ModMain.Hint($"已导入 {ModBase.GetFileNameWithoutExtentionFromPath(FilePath)}", ModMain.HintType.Finish);
                            if (ModMain.FrmVersionWorld is not null)
                                ModBase.RunInUi(() => ModMain.FrmVersionWorld.Reload());
                            return;
                        }
                    case PageSubType.VersionResourcePack:
                        {
                            string DestFile = PageVersionLeft.Version.PathIndie + @"resourcepacks\" + ModBase.GetFileNameFromPath(FilePath);
                            if (File.Exists(DestFile))
                            {
                                ModMain.Hint("已存在同名文件：" + DestFile, ModMain.HintType.Critical);
                                return;
                            }
                            ModBase.CopyFile(FilePath, DestFile);
                            ModMain.Hint($"已导入 {ModBase.GetFileNameFromPath(FilePath)}", ModMain.HintType.Finish);
                            if (ModMain.FrmVersionResourcePack is not null)
                                ModBase.RunInUi(() => ModMain.FrmVersionResourcePack.ReloadCompFileList());
                            return;
                        }
                    case PageSubType.VersionShader:
                        {
                            string DestFile = PageVersionLeft.Version.PathIndie + @"shaderpacks\" + ModBase.GetFileNameFromPath(FilePath);
                            if (File.Exists(DestFile))
                            {
                                ModMain.Hint("已存在同名文件：" + DestFile, ModMain.HintType.Critical);
                                return;
                            }
                            ModBase.CopyFile(FilePath, DestFile);
                            ModMain.Hint($"已导入 {ModBase.GetFileNameFromPath(FilePath)}", ModMain.HintType.Finish);
                            if (ModMain.FrmVersionShader is not null)
                                ModBase.RunInUi(() => ModMain.FrmVersionShader.ReloadCompFileList());
                            return;
                        }
                }
            }
            // 安装整合包
            if (new[] { "zip", "rar", "mrpack" }.Any(t => (t ?? "") == (Extension ?? ""))) // 部分压缩包是 zip 格式但后缀为 rar，总之试一试
            {
                ModBase.Log("[System] 文件为压缩包，尝试作为整合包安装");
                try
                {
                    ModModpack.ModpackInstall(FilePath);
                    return;
                }
                catch (ModBase.CancelledException ex)
                {
                    return; // 用户主动取消
                }
                catch (Exception ex)
                {
                    // 安装失败，继续往后尝试
                }
            }
            // RAR 处理
            if (Extension == "rar")
            {
                ModMain.Hint("PCL 无法处理 rar 格式的压缩包，请在解压后重新压缩为 zip 格式再试！");
                return;
            }
            // 错误报告分析
            do
            {
                try
                {
                    ModBase.Log("[System] 尝试进行错误报告分析");
                    var Analyzer = new CrashAnalyzer(ModBase.GetUuid());
                    Analyzer.Import(FilePath);
                    if (Analyzer.Prepare() == 0)
                        break;
                    Analyzer.Analyze();
                    Analyzer.Output(true, new List<string>());
                    return;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "自主错误报告分析失败", ModBase.LogLevel.Feedback);
                }
            }
            while (false);
            // 未知操作
            ModMain.Hint("PCL 无法确定应当执行的文件拖拽操作……");
        }, "文件拖拽");
        }

        // 接受到 Windows 窗体事件
        public bool IsSystemTimeChanged = false;
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 30)
            {
                var NowDate = DateTime.Now;
                if (NowDate.Date == ModBase.ApplicationOpenTime.Date)
                {
                    ModBase.Log("[System] 系统时间微调为：" + NowDate.ToLongDateString() + " " + NowDate.ToLongTimeString());
                    IsSystemTimeChanged = false;
                }
                else
                {
                    ModBase.Log("[System] 系统时间修改为：" + NowDate.ToLongDateString() + " " + NowDate.ToLongTimeString());
                    IsSystemTimeChanged = true;
                }
            }
            else if (msg == 400 * 16 + 2)
            {
                ModBase.Log("[System] 收到置顶信息：" + hwnd.ToInt64());
                if (!IsWindowLoadFinished)
                {
                    ModBase.Log("[System] 窗口尚未加载完成，忽略置顶请求");
                    return IntPtr.Zero;
                }
                ShowWindowToTop();
                handled = true;
            }
            else if (msg == 26) // WM_SETTINGCHANGE
            {
                if (Marshal.PtrToStringAuto(lParam) == "ImmersiveColorSet")
                {
                    ModBase.Log($"[System] 系统主题更改，深色模式：{ModBase.IsSystemInDarkMode()}");
                    if (Conversions.ToBoolean(Operators.AndObject(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("UiDarkMode"), 2, false), ModSecret.IsDarkMode != ModBase.IsSystemInDarkMode())))
                    {
                        ModSecret.IsDarkMode = ModBase.IsSystemInDarkMode();
                        ModSecret.ThemeRefresh();
                    }
                }
            }

            return IntPtr.Zero;
        }

        // 窗口隐藏与置顶
        private bool _Hidden = false;
        public bool Hidden
        {
            get
            {
                return _Hidden;
            }
            set
            {
                if (_Hidden == value)
                    return;
                _Hidden = value;
                if (value)
                {
                    // 隐藏
                    this.Left -= 10000d;
                    this.ShowInTaskbar = false;
                    this.Visibility = Visibility.Hidden;
                    ModBase.Log("[System] 窗口已隐藏，位置：(" + this.Left + "," + this.Top + ")");
                }
                else
                {
                    // 取消隐藏
                    if (this.Left < (double)-2000)
                        this.Left += 10000d;
                    ShowWindowToTop();
                }
            }
        }
        /// <summary>
    /// 把当前窗口拖到最前面。
    /// </summary>
        public void ShowWindowToTop()
        {
            ModBase.RunInUi(() =>
        {
            // 这一坨乱七八糟的，别改，改了指不定就炸了，自己电脑还复现不出来
            this.Visibility = Visibility.Visible;
            this.ShowInTaskbar = true;
            this.WindowState = WindowState.Normal;
            Hidden = false;
            this.Topmost = true; // 偶尔 SetForegroundWindow 失效
            this.Topmost = false;
            ModMain.SetForegroundWindow(ModBase.Handle);
            this.Focus();
            ModBase.Log($"[System] 窗口已置顶，位置：({this.Left}, {this.Top}), {this.Width} x {this.Height}");
        });
        }

        #endregion

        #region 切换页面

        // 页面种类与属性
        // 注意，这一枚举在 “切换页面” EventType 中调用，应视作公开 API 的一部分
        /// <summary>
    /// 页面种类。
    /// </summary>
        public enum PageType
        {
            /// <summary>
        /// 启动。
        /// </summary>
            Launch = 0,
            /// <summary>
        /// 下载。
        /// </summary>
            Download = 1,
            /// <summary>
        /// 联机。
        /// </summary>
            Link = 2,
            /// <summary>
        /// 设置。
        /// </summary>
            Setup = 3,
            /// <summary>
        /// 更多。
        /// </summary>
            Other = 4,
            /// <summary>
        /// 版本选择。这是一个副页面。
        /// </summary>
            VersionSelect = 5,
            /// <summary>
        /// 下载管理。这是一个副页面。
        /// </summary>
            DownloadManager = 6,
            /// <summary>
        /// 版本设置。这是一个副页面。
        /// </summary>
            VersionSetup = 7,
            /// <summary>
        /// 资源工程详情。这是一个副页面。
        /// </summary>
            CompDetail = 8,
            /// <summary>
        /// 帮助详情。这是一个副页面。
        /// </summary>
            HelpDetail = 9,
            /// <summary>
        /// 游戏实时日志。这是一个副页面。
        /// </summary>
            GameLog = 10
        }
        /// <summary>
    /// 次要页面种类。其数值必须与 StackPanel 中的下标一致。
    /// </summary>
        public enum PageSubType
        {
            Default = 0,
            DownloadInstall = 1,
            DownloadClient = 4,
            DownloadOptiFine = 5,
            DownloadForge = 6,
            DownloadNeoForge = 7,
            DownloadCleanroom = 16,
            DownloadFabric = 8,
            DownloadQuilt = 10,
            DownloadLiteLoader = 9,
            DownloadMod = 11,
            DownloadPack = 12,
            DownloadResourcePack = 13,
            DownloadShader = 14,
            DownloadCompFavorites = 15,
            SetupLaunch = 0,
            SetupUI = 1,
            SetupSystem = 2,
            SetupLink = 3,
            LinkLobby = 1,
            LinkIoi = 2,
            LinkSetup = 4,
            LinkHelp = 5,
            LinkFeedback = 6,
            LinkNetStatus = 7,
            OtherHelp = 0,
            OtherAbout = 1,
            OtherTest = 2,
            OtherFeedback = 3,
            OtherVote = 4,
            VersionOverall = 0,
            VersionSetup = 1,
            VersionExport = 2,
            VersionWorld = 3,
            VersionScreenshot = 4,
            VersionMod = 5,
            VersionModDisabled = 6,
            VersionResourcePack = 7,
            VersionShader = 8,
            VersionInstall = 9
        }
        /// <summary>
    /// 获取次级页面的名称。若并非次级页面则返回空字符串，故可以以此判断是否为次级页面。
    /// </summary>
        private string PageNameGet(PageStackData Stack)
        {
            switch (Stack.Page)
            {
                case PageType.VersionSelect:
                    {
                        return "版本选择";
                    }
                case PageType.DownloadManager:
                    {
                        return "下载管理";
                    }
                case PageType.GameLog:
                    {
                        return "实时日志";
                    }
                case PageType.VersionSetup:
                    {
                        return "版本设置 - " + (PageVersionLeft.Version is null ? "未知版本" : PageVersionLeft.Version.Name);
                    }
                case PageType.CompDetail:
                    {
                        ModComp.CompProject Project = (ModComp.CompProject)Stack.Additional(0);
                        switch (Project.Type)
                        {
                            case ModComp.CompType.Mod:
                                {
                                    return "Mod 下载 - " + Project.TranslatedName;
                                }
                            case ModComp.CompType.ModPack:
                                {
                                    return "整合包下载 - " + Project.TranslatedName;
                                }
                            case ModComp.CompType.ResourcePack:
                                {
                                    return "资源包下载 - " + Project.TranslatedName;
                                }
                            case ModComp.CompType.Shader:
                                {
                                    return "光影包下载 - " + Project.TranslatedName;
                                }

                            default:
                                {
                                    return "资源下载 - " + Project.TranslatedName;
                                }
                        }

                        break;
                    }
                case PageType.HelpDetail:
                    {
                        ModMain.HelpEntry Entry = (ModMain.HelpEntry)Stack.Additional(0);
                        return Entry.Title;
                    }

                default:
                    {
                        return "";
                    }
            }
        }
        /// <summary>
    /// 刷新次级页面的名称。
    /// </summary>
        public void PageNameRefresh(PageStackData Type)
        {
            this.LabTitleInner.Text = PageNameGet(Type);
        }
        /// <summary>
    /// 刷新次级页面的名称。
    /// </summary>
        public void PageNameRefresh()
        {
            PageNameRefresh(PageCurrent);
        }

        // 页面状态存储
        /// <summary>
    /// 当前的主页面。
    /// </summary>
        public PageStackData PageCurrent = (PageStackData)PageType.Launch;
        /// <summary>
    /// 上一个主页面。
    /// </summary>
        public PageStackData PageLast = (PageStackData)PageType.Launch;
        /// <summary>
    /// 当前的子页面。
    /// </summary>
        public PageSubType PageCurrentSub
        {
            get
            {
                switch (PageCurrent)
                {
                    case 1:
                        {
                            if (ModMain.FrmDownloadLeft is null)
                                ModMain.FrmDownloadLeft = new PageDownloadLeft();
                            return ModMain.FrmDownloadLeft.PageID;
                        }

                    case 3:
                        {
                            if (ModMain.FrmSetupLeft is null)
                                ModMain.FrmSetupLeft = new PageSetupLeft();
                            return ModMain.FrmSetupLeft.PageID;
                        }

                    case 4:
                        {
                            if (ModMain.FrmOtherLeft is null)
                                ModMain.FrmOtherLeft = new PageOtherLeft();
                            return ModMain.FrmOtherLeft.PageID;
                        }

                    case 7:
                        {
                            if (ModMain.FrmVersionLeft is null)
                                ModMain.FrmVersionLeft = new PageVersionLeft();
                            return ModMain.FrmVersionLeft.PageID;
                        }

                    default:
                        {
                            return 0; // 没有子页面
                        }
                }
            }
        }
        /// <summary>
    /// 上层页面的编号堆栈，用于返回。
    /// </summary>
        public List<PageStackData> PageStack = new List<PageStackData>();
        public class PageStackData
        {

            public PageType Page;
            public object Additional;

            public override bool Equals(object other)
            {
                if (other is null)
                    return false;
                if (other is PageStackData)
                {
                    PageStackData PageOther = (PageStackData)other;
                    if (Page != PageOther.Page)
                        return false;
                    if (Additional is null)
                    {
                        return PageOther.Additional is null;
                    }
                    else
                    {
                        return PageOther.Additional is not null && Additional.Equals(PageOther.Additional);
                    }
                }
                else if (other is int)
                {
                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(Page, other, false)))
                        return false;
                    return Additional is null;
                }
                else
                {
                    return false;
                }
            }
            public static bool operator ==(PageStackData left, PageStackData right)
            {
                return EqualityComparer<PageStackData>.Default.Equals(left, right);
            }
            public static bool operator !=(PageStackData left, PageStackData right)
            {
                return !(left == right);
            }
            public static implicit operator PageStackData(PageType Value)
            {
                return new PageStackData() { Page = Value };
            }
            public static implicit operator PageType(PageStackData Value)
            {
                return Value.Page;
            }
        }
        public MyPageLeft PageLeft;
        public MyPageRight PageRight;

        // 引发实际页面切换的入口
        private bool IsChangingPage = false;
        /// <summary>
    /// 切换页面，并引起对应选择 UI 的改变。
    /// </summary>
        public void PageChange(PageStackData Stack, PageSubType SubType = PageSubType.Default)
        {
            if (string.IsNullOrEmpty(PageNameGet(Stack)))
            {
                // 切换到主页面
                PageChangeExit();
                IsChangingPage = true; // 防止下面的勾选直接触发了 PageChangeActual
                ((MyRadioButton)this.PanTitleSelect.Children[Stack]).SetChecked(true, true, string.IsNullOrEmpty(PageNameGet(PageCurrent)));
                IsChangingPage = false;
                switch (Stack.Page)
                {
                    case PageType.Download:
                        {
                            if (ModMain.FrmDownloadLeft is null)
                                ModMain.FrmDownloadLeft = new PageDownloadLeft();
                            foreach (var item in ModMain.FrmDownloadLeft.PanItem.Children)
                            {
                                if (object.ReferenceEquals(item.GetType(), typeof(MyListItem)) && ModBase.Val(((dynamic)item).tag) == (double)SubType)
                                {
                                    ((MyListItem)item).SetChecked(true, true, Stack == PageCurrent);
                                    break;
                                }
                            }

                            break;
                        }
                    case PageType.Setup:
                        {
                            if (ModMain.FrmSetupLeft is null)
                                ModMain.FrmSetupLeft = new PageSetupLeft();
                            ((MyListItem)ModMain.FrmSetupLeft.PanItem.Children[(int)SubType]).SetChecked(true, true, Stack == PageCurrent);
                            break;
                        }
                    case PageType.Other:
                        {
                            if (ModMain.FrmOtherLeft is null)
                                ModMain.FrmOtherLeft = new PageOtherLeft();
                            ((MyListItem)ModMain.FrmOtherLeft.PanItem.Children[(int)SubType]).SetChecked(true, true, Stack == PageCurrent);
                            break;
                        }
                }
                PageChangeActual(Stack, SubType);
            }
            else
            {
                // 切换到次页面
                switch (Stack.Page)
                {
                    case PageType.VersionSetup:
                        {
                            if (ModMain.FrmVersionLeft is null)
                                ModMain.FrmVersionLeft = new PageVersionLeft();
                            foreach (var item in ModMain.FrmVersionLeft.PanItem.Children)
                            {
                                if (object.ReferenceEquals(item.GetType(), typeof(MyListItem)) && ModBase.Val(((dynamic)item).tag) == (double)SubType)
                                {
                                    ((MyListItem)item).SetChecked(true, true, Stack == PageCurrent);
                                    break;
                                }
                            }

                            break;
                        }
                }
                PageChangeActual(Stack, SubType);
            }
        }
        /// <summary>
    /// 通过点击导航栏改变页面。
    /// </summary>
        private void BtnTitleSelect_Click(MyRadioButton sender, bool raiseByMouse)
        {
            if (IsChangingPage)
                return;
            PageChangeActual((PageStackData)ModBase.Val(sender.Tag));
        }
        /// <summary>
    /// 通过点击返回按钮或手动触发返回来改变页面。
    /// </summary>
        public void PageBack()
        {
            if (PageStack.Any())
            {
                PageChangeActual(PageStack[0]);
            }
            else
            {
                PageChange((PageStackData)PageType.Launch);
            }
        }

        // 实际处理页面切换
        /// <summary>
    /// 切换现有页面的实际方法。
    /// </summary>
        private void PageChangeActual(PageStackData Stack, PageSubType SubType = -1)
        {
            if (PageCurrent == Stack && (PageCurrentSub == SubType || (int)SubType == -1))
                return;
            ModAnimation.AniControlEnabled += 1;
            try
            {

                #region 子页面处理
                string PageName = PageNameGet(Stack);
                if (string.IsNullOrEmpty(PageName))
                {
                    // 即将切换到一个顶级页面
                    PageChangeExit();
                }
                // 即将切换到一个子页面
                else if (PageStack.Any())
                {
                    // 子页面 → 另一个子页面，更新
                    ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.LabTitleInner, -this.LabTitleInner.Opacity, 130), ModAnimation.AaCode(() => this.LabTitleInner.Text = PageName, After: true), ModAnimation.AaOpacity(this.LabTitleInner, 1d, 150, 30) }, "FrmMain Titlebar SubLayer");
                    if (PageStack.Contains(Stack))
                    {
                        // 返回到更上层的子页面
                        while (PageStack.Contains(Stack))
                            PageStack.RemoveAt(0);
                    }
                    else
                    {
                        // 进入更深层的子页面
                        PageStack.Insert(0, PageCurrent);
                    }
                }
                else
                {
                    // 主页面 → 子页面，进入
                    this.PanTitleInner.Visibility = Visibility.Visible;
                    this.PanTitleMain.IsHitTestVisible = false;
                    this.PanTitleInner.IsHitTestVisible = true;
                    PageNameRefresh(Stack);
                    ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.PanTitleMain, -this.PanTitleMain.Opacity, 150), ModAnimation.AaX(this.PanTitleMain, 12d - this.PanTitleMain.Margin.Left, 150, Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaOpacity(this.PanTitleInner, 1d - this.PanTitleInner.Opacity, 150, 200), ModAnimation.AaX(this.PanTitleInner, -this.PanTitleInner.Margin.Left, 350, 200, new ModAnimation.AniEaseOutBack()), ModAnimation.AaCode(() => this.PanTitleMain.Visibility = Visibility.Collapsed, After: true) }, "FrmMain Titlebar FirstLayer");
                    PageStack.Insert(0, PageCurrent);
                }
                #endregion

                #region 实际更改页面框架 UI
                PageLast = PageCurrent;
                PageCurrent = Stack;
                switch (Stack.Page)
                {
                    case PageType.Launch: // 启动
                        {
                            this.PageChangeAnim(ModMain.FrmLaunchLeft, ModMain.FrmLaunchRight);
                            break;
                        }
                    case PageType.Download: // 下载
                        {
                            if (ModMain.FrmDownloadLeft is null)
                                ModMain.FrmDownloadLeft = new PageDownloadLeft();
                            // PageGet 方法会在未设置 SubType 时指定默认值，并建立相关页面的实例
                            this.PageChangeAnim(ModMain.FrmDownloadLeft, (FrameworkElement)ModMain.FrmDownloadLeft.PageGet(SubType));
                            break;
                        }
                    case PageType.Link: // 联机
                        {
                            if (ModMain.FrmLinkLeft is null)
                                ModMain.FrmLinkLeft = new PageLinkLeft();
                            this.PageChangeAnim(ModMain.FrmLinkLeft, (FrameworkElement)ModMain.FrmLinkLeft.PageGet(SubType));
                            break;
                        }
                    case PageType.Setup: // 设置
                        {
                            if (ModMain.FrmSetupLeft is null)
                                ModMain.FrmSetupLeft = new PageSetupLeft();
                            this.PageChangeAnim(ModMain.FrmSetupLeft, (FrameworkElement)ModMain.FrmSetupLeft.PageGet(SubType));
                            break;
                        }
                    case PageType.Other: // 更多
                        {
                            if (ModMain.FrmOtherLeft is null)
                                ModMain.FrmOtherLeft = new PageOtherLeft();
                            this.PageChangeAnim(ModMain.FrmOtherLeft, (FrameworkElement)ModMain.FrmOtherLeft.PageGet(SubType));
                            break;
                        }
                    case PageType.GameLog: // 实时日志
                        {
                            if (ModMain.FrmLogLeft is null)
                                ModMain.FrmLogLeft = new PageLogLeft();
                            if (ModMain.FrmLogLeft is null)
                                ModMain.FrmLogRight = new PageLogRight();
                            this.PageChangeAnim(ModMain.FrmLogLeft, ModMain.FrmLogRight);
                            break;
                        }
                    case PageType.VersionSelect: // 版本选择
                        {
                            if (ModMain.FrmSelectLeft is null)
                                ModMain.FrmSelectLeft = new PageSelectLeft();
                            if (ModMain.FrmSelectRight is null)
                                ModMain.FrmSelectRight = new PageSelectRight();
                            this.PageChangeAnim(ModMain.FrmSelectLeft, ModMain.FrmSelectRight);
                            break;
                        }
                    case PageType.DownloadManager: // 下载管理
                        {
                            if (ModMain.FrmSpeedLeft is null)
                                ModMain.FrmSpeedLeft = new PageSpeedLeft();
                            if (ModMain.FrmSpeedRight is null)
                                ModMain.FrmSpeedRight = new PageSpeedRight();
                            this.PageChangeAnim(ModMain.FrmSpeedLeft, ModMain.FrmSpeedRight);
                            break;
                        }
                    case PageType.VersionSetup: // 版本设置
                        {
                            if (ModMain.FrmVersionLeft is null)
                                ModMain.FrmVersionLeft = new PageVersionLeft();
                            this.PageChangeAnim(ModMain.FrmVersionLeft, (FrameworkElement)ModMain.FrmVersionLeft.PageGet(SubType));
                            break;
                        }
                    case PageType.CompDetail: // Mod 信息
                        {
                            if (ModMain.FrmDownloadCompDetail is null)
                                ModMain.FrmDownloadCompDetail = new PageDownloadCompDetail();
                            this.PageChangeAnim(new MyPageLeft(), ModMain.FrmDownloadCompDetail);
                            break;
                        }
                    case PageType.HelpDetail: // 帮助详情
                        {
                            PageChangeAnim(new MyPageLeft(), (FrameworkElement)Stack.Additional(1));
                            break;
                        }
                }
                #endregion

                #region 设置为最新状态
                this.BtnExtraDownload.ShowRefresh();
                this.BtnExtraApril.ShowRefresh();
                #endregion

                ModBase.Log("[Control] 切换主要页面：" + ModBase.GetStringFromEnum(Stack) + ", " + ((int)SubType).ToString());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "切换主要页面失败（ID " + ((int)PageCurrent.Page).ToString() + "）", ModBase.LogLevel.Feedback);
            }
            finally
            {
                ModAnimation.AniControlEnabled -= 1;
            }
        }
        private void PageChangeAnim(FrameworkElement TargetLeft, FrameworkElement TargetRight)
        {
            ModAnimation.AniStop("FrmMain LeftChange");
            ModAnimation.AniStop("PageLeft PageChange"); // 停止左边栏变更导致的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
            ModAnimation.AniControlEnabled += 1;
            // 清除新页面关联性
            if (!(TargetLeft.Parent == null))
                TargetLeft.SetValue(ContentPresenter.ContentProperty, null);
            if (!(TargetRight == null) && !(TargetRight.Parent == null))
                TargetRight.SetValue(ContentPresenter.ContentProperty, null);
            PageLeft = (MyPageLeft)TargetLeft;
            PageRight = (MyPageRight)TargetRight;
            // 触发页面通用动画
            ((MyPageLeft)this.PanMainLeft.Child).TriggerHideAnimation();
            ((MyPageRight)this.PanMainRight.Child).PageOnExit();
            ModAnimation.AniControlEnabled -= 1;
            // 执行动画
            ModAnimation.AniStart(new[] {
                        ModAnimation.AaCode(() =>
                {
                ModAnimation.AniControlEnabled += 1;
                // 把新页面添加进容器
                this.PanMainLeft.Child = PageLeft;
                PageLeft.Opacity = 0d;
                this.PanMainLeft.Background = (Brush)null;
                ModAnimation.AniControlEnabled -= 1;
                ModBase.RunInUi(() => this.PanMainLeft_Resize(this.PanMainLeft.ActualWidth), true);
            }, 130),
                        ModAnimation.AaCode(() =>
                {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageLeft.Opacity = 1d;
                PageLeft.TriggerShowAnimation();
            }, 30, true)
            }, "FrmMain PageChangeLeft");
            ModAnimation.AniStart(new[] {
                        ModAnimation.AaCode(() =>
                {
                ModAnimation.AniControlEnabled += 1;
                ((MyPageRight)this.PanMainRight.Child).PageOnForceExit();
                // 把新页面添加进容器
                this.PanMainRight.Child = PageRight;
                PageRight.Opacity = 0d;
                this.PanMainRight.Background = (Brush)null;
                ModAnimation.AniControlEnabled -= 1;
                ModBase.RunInUi(() => this.BtnExtraBack.ShowRefresh(), true);
            }, 130),
                        ModAnimation.AaCode(() =>
                {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageRight.Opacity = 1d;
                PageRight.PageOnEnter();
            }, 30, true)
            }, "FrmMain PageChangeRight");
        }
        /// <summary>
    /// 退出子界面。
    /// </summary>
        private void PageChangeExit()
        {
            if (PageStack.Any())
            {
                // 子页面 → 主页面，退出
                this.PanTitleMain.Visibility = Visibility.Visible;
                this.PanTitleMain.IsHitTestVisible = true;
                this.PanTitleInner.IsHitTestVisible = false;
                ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.PanTitleInner, -this.PanTitleInner.Opacity, 150), ModAnimation.AaX(this.PanTitleInner, (double)-18 - this.PanTitleInner.Margin.Left, 150, Ease: new ModAnimation.AniEaseInFluent()), ModAnimation.AaOpacity(this.PanTitleMain, 1d - this.PanTitleMain.Opacity, 150, 200), ModAnimation.AaX(this.PanTitleMain, -this.PanTitleMain.Margin.Left, 350, 200, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => this.PanTitleInner.Visibility = Visibility.Collapsed, After: true) }, "FrmMain Titlebar FirstLayer");
                PageStack.Clear();
            }
            else
            {
                // 主页面 → 主页面，无事发生
            }
        }

        // 左边栏改变
        private void PanMainLeft_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;
            PanMainLeft_Resize(e.NewSize.Width);
        }
        private void PanMainLeft_Resize(double NewWidth)
        {
            double Delta = NewWidth - this.RectLeftBackground.Width;
            if (Math.Abs(Delta) > 0.1d && ModAnimation.AniControlEnabled == 0)
            {
                if (this.PanMain.Opacity < 0.1d)
                    this.PanMainLeft.IsHitTestVisible = false; // 避免左边栏指向背景未能完美覆盖左边栏
                if (NewWidth > 0d)
                {
                    // 宽度足够，显示
                    ModAnimation.AniStart(new[] { ModAnimation.AaWidth(this.RectLeftBackground, NewWidth - this.RectLeftBackground.Width, 400, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)), ModAnimation.AaOpacity(this.RectLeftShadow, 1d - this.RectLeftShadow.Opacity, 200), ModAnimation.AaCode(() => this.PanMainLeft.IsHitTestVisible = true, 250) }, "FrmMain LeftChange", true);
                }
                else
                {
                    // 宽度不足，隐藏
                    ModAnimation.AniStart(new[] { ModAnimation.AaWidth(this.RectLeftBackground, -this.RectLeftBackground.Width, 200, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaOpacity(this.RectLeftShadow, -this.RectLeftShadow.Opacity, 200), ModAnimation.AaCode(() => this.PanMainLeft.IsHitTestVisible = true, 170) }, "FrmMain LeftChange", true);
                }
            }
            else
            {
                this.RectLeftBackground.Width = NewWidth;
                this.PanMainLeft.IsHitTestVisible = true;
                ModAnimation.AniStop("FrmMain LeftChange");
            }
        }

        #endregion

        #region 控件拖动

        // 在时钟中调用，使得即使鼠标在窗口外松开，也可以释放控件
        public void DragTick()
        {
            if (ModMain.DragControl is null)
                return;
            if (!(Mouse.LeftButton == MouseButtonState.Pressed))
            {
                DragStop();
            }
        }
        // 在鼠标移动时调用，以改变 Slider 位置
        public void DragDoing()
        {
            if (ModMain.DragControl is null)
                return;
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                ((dynamic)ModMain.DragControl).DragDoing();
            }
            else
            {
                DragStop();
            }
        }
        public void DragStop()
        {
            // 存在其他线程调用的可能性，因此需要确保在 UI 线程运行
            ModBase.RunInUi(() =>
                {
                    if (ModMain.DragControl is null)
                        return;
                    var Control = ModMain.DragControl;
                    ModMain.DragControl = null;
                    ((dynamic)Control).DragStop(); // 控件会在该事件中判断 DragControl，所以得放在后面
                });
        }

        #endregion

        #region 附加按钮

        // 音乐
        private void BtnExtraMusic_Click(object sender, EventArgs e)
        {
            ModMusic.MusicControlPause();
        }
        private void BtnExtraMusic_RightClick(object sender, EventArgs e)
        {
            ModMusic.MusicControlNext();
        }

        // 下载管理
        private void BtnExtraDownload_Click(object sender, EventArgs e)
        {
            PageChange((PageStackData)PageType.DownloadManager);
        }
        private bool BtnExtraDownload_ShowCheck()
        {
            return ModNet.HasDownloadingTask() && !(PageCurrent == (PageStackData)PageType.DownloadManager);
        }

        // 投降
        public void AprilGiveup()
        {
            if (ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup)
            {
                ModMain.Hint("=D", ModMain.HintType.Finish);
                ModMain.IsAprilGiveup = true;
                ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleX = 1d;
                ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleY = 1d;
                this.BtnExtraApril.ShowRefresh();
            }
        }
        public bool BtnExtraApril_ShowCheck()
        {
            return ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup && PageCurrent == (PageStackData)PageType.Launch;
        }

        // 关闭 Minecraft
        public void BtnExtraShutdown_Click()
        {
            try
            {
                if (ModLaunch.McLaunchLoaderReal is not null)
                    ModLaunch.McLaunchLoaderReal.Abort();
                foreach (var Watcher in ModWatcher.McWatcherList)
                    Watcher.Kill();
                ModMain.Hint("已关闭运行中的 Minecraft！", ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "强制关闭所有 Minecraft 失败", ModBase.LogLevel.Feedback);
            }
        }
        public bool BtnExtraShutdown_ShowCheck()
        {
            return ModWatcher.HasRunningMinecraft;
        }

        // 游戏日志
        public void BtnExtraLog_Click()
        {
            PageChange((PageStackData)PageType.GameLog);
        }
        public bool BtnExtraLog_ShowCheck()
        {
            if (ModMain.FrmLogLeft is null || ModMain.FrmLogRight is null || PageCurrent == (PageStackData)PageType.GameLog)
                return false;
            return ModMain.FrmLogLeft.ShownLogs.Count > 0;
        }

        /// <summary>
    /// 返回顶部。
    /// </summary>
        public void BackToTop()
        {
            var RealScroll = BtnExtraBack_GetRealChild();
            if (RealScroll is not null)
            {
                RealScroll.PerformVerticalOffsetDelta(-RealScroll.VerticalOffset);
            }
            else
            {
                ModBase.Log("[UI] 无法返回顶部，未找到合适的 RealScroll", ModBase.LogLevel.Hint);
            }
        }
        private bool BtnExtraBack_ShowCheck()
        {
            var RealScroll = BtnExtraBack_GetRealChild();
            return RealScroll is not null && RealScroll.Visibility == Visibility.Visible && RealScroll.VerticalOffset > this.Height + (double)(this.BtnExtraBack.Show ? 0 : 700);
        }
        private MyScrollViewer BtnExtraBack_GetRealChild()
        {
            if (this.PanMainRight.Child is null || !(this.PanMainRight.Child is MyPageRight))
                return null;
            return ((MyPageRight)this.PanMainRight.Child).PanScroll;
        }

        #endregion

        // 愚人节鼠标位置
        public MouseEventArgs lastMouseArg = null;
        private void FormMain_MouseMove(object sender, MouseEventArgs e)
        {
            lastMouseArg = e;
        }

    }
}