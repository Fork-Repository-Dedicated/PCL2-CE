using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageSetupSystem
    {

        private new bool IsLoaded = false;

        public PageSetupSystem()
        {
            this.Loaded += PageSetupSystem_Loaded;
        }

        private void PageSetupSystem_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

            ModAnimation.AniControlEnabled += 1;
            Reload();
            SliderLoad();
            ModAnimation.AniControlEnabled -= 1;

        }
        public void Reload()
        {

            // 下载
            this.SliderDownloadThread.Value = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadThread"));
            this.SliderDownloadSpeed.Value = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadSpeed"));
            this.ComboDownloadVersion.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadVersion"));

            // Mod 与整合包
            this.ComboDownloadTranslateV2.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadTranslateV2"));
            // ComboDownloadMod.SelectedIndex = Setup.Get("ToolDownloadMod")
            this.ComboModLocalNameStyle.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolModLocalNameStyle"));
            this.CheckDownloadIgnoreQuilt.Checked = Conversions.ToBoolean(ModBase.Setup.Get("ToolDownloadIgnoreQuilt"));
            this.CheckDownloadClipboard.Checked = Conversions.ToBoolean(ModBase.Setup.Get("ToolDownloadClipboard"));

            // Minecraft 更新提示
            this.CheckUpdateRelease.Checked = Conversions.ToBoolean(ModBase.Setup.Get("ToolUpdateRelease"));
            this.CheckUpdateSnapshot.Checked = Conversions.ToBoolean(ModBase.Setup.Get("ToolUpdateSnapshot"));

            // 辅助设置
            this.CheckHelpChinese.Checked = Conversions.ToBoolean(ModBase.Setup.Get("ToolHelpChinese"));

            // 系统设置
            this.ComboSystemUpdate.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("SystemSystemUpdate"));
            if (ModBase.Val(Environment.OSVersion.Version.ToString().Split(".")[2]) >= 19042d)
            {
                this.ComboSystemUpdateBranch.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("SystemSystemUpdateBranch"));
            }
            else // 不满足系统要求
            {
                this.ComboSystemUpdateBranch.Items.Clear();
                this.ComboSystemUpdateBranch.Items.Add("Legacy");
                this.ComboSystemUpdateBranch.SelectedIndex = 0;
                this.ComboSystemUpdateBranch.ToolTip = "由于你的 Windows 版本过低，不满足新版本要求，只能获取 Legacy 分支的更新。&#xa;升级到 Windows 10 20H2 或以上版本以获取最新更新。";
                this.ComboSystemUpdateBranch.IsEnabled = false;
            }
            this.ComboSystemActivity.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("SystemSystemActivity"));
            this.ComboSystemServer.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("SystemSystemServer"));
            this.TextSystemCache.Text = Conversions.ToString(ModBase.Setup.Get("SystemSystemCache"));
            this.CheckSystemDisableHardwareAcceleration.Checked = Conversions.ToBoolean(ModBase.Setup.Get("SystemDisableHardwareAcceleration"));
            this.SliderAniFPS.Value = Conversions.ToInteger(ModBase.Setup.Get("UiAniFPS"));

            // 网络
            this.TextSystemHttpProxy.Text = Conversions.ToString(ModBase.Setup.Get("SystemHttpProxy"));
            this.CheckDownloadCert.Checked = Conversions.ToBoolean(ModBase.Setup.Get("ToolDownloadCert"));

            // 调试选项
            this.CheckDebugMode.Checked = Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugMode"));
            this.SliderDebugAnim.Value = Conversions.ToInteger(ModBase.Setup.Get("SystemDebugAnim"));
            this.CheckDebugDelay.Checked = Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugDelay"));
            this.CheckDebugSkipCopy.Checked = Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugSkipCopy"));

        }

        // 初始化
        public void Reset()
        {
            try
            {
                ModBase.Setup.Reset("ToolDownloadThread");
                ModBase.Setup.Reset("ToolDownloadSpeed");
                ModBase.Setup.Reset("ToolDownloadVersion");
                ModBase.Setup.Reset("ToolDownloadTranslateV2");
                ModBase.Setup.Reset("ToolDownloadIgnoreQuilt");
                ModBase.Setup.Reset("ToolDownloadClipboard");
                ModBase.Setup.Reset("ToolDownloadMod");
                ModBase.Setup.Reset("ToolModLocalNameStyle");
                ModBase.Setup.Reset("ToolUpdateRelease");
                ModBase.Setup.Reset("ToolUpdateSnapshot");
                ModBase.Setup.Reset("ToolHelpChinese");
                ModBase.Setup.Reset("SystemDebugMode");
                ModBase.Setup.Reset("SystemDebugAnim");
                ModBase.Setup.Reset("SystemDebugDelay");
                ModBase.Setup.Reset("SystemDebugSkipCopy");
                ModBase.Setup.Reset("SystemSystemCache");
                ModBase.Setup.Reset("SystemSystemUpdate");
                ModBase.Setup.Reset("SystemSystemServer");
                ModBase.Setup.Reset("SystemSystemActivity");
                ModBase.Setup.Reset("SystemDisableHardwareAcceleration");
                ModBase.Setup.Reset("SystemHttpProxy");
                ModBase.Setup.Reset("ToolDownloadCert");
                ModBase.Setup.Reset("UiAniFPS");

                ModBase.Log("[Setup] 已初始化启动器页设置");
                ModMain.Hint("已初始化启动器页设置！", ModMain.HintType.Finish, false);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化启动器页设置失败", ModBase.LogLevel.Msgbox);
            }

            Reload();
        }

        // 将控件改变路由到设置改变
        private static void CheckBoxChange(MyCheckBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Checked);
        }
        private static void SliderChange(MySlider sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Value);
        }
        private static void ComboChange(MyComboBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.SelectedIndex);
        }
        private static void TextBoxChange(MyTextBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Text);
        }

        private void StartClipboardListening()
        {
            if (this.CheckDownloadClipboard.Checked)
            {
                ModBase.RunInNewThread(() => ModComp.CompClipboard.ClipboardListening());
            }
        }

        // 滑动条
        private void SliderLoad()
        {
            this.SliderDownloadThread.GetHintText = new Func<object, object>(v => Operators.AddObject(v, 1));
            this.SliderDownloadSpeed.GetHintText = new Func<object, object>(v => { switch (v) { case var @case when Operators.ConditionalCompareObjectLessEqual(@case, 14, false): { return Operators.ConcatenateObject(Operators.MultiplyObject(Operators.AddObject(v, 1), 0.1d), " M/s"); } case var case1 when Operators.ConditionalCompareObjectLessEqual(case1, 31, false): { return Operators.ConcatenateObject(Operators.MultiplyObject(Operators.SubtractObject(v, 11), 0.5d), " M/s"); } case var case2 when Operators.ConditionalCompareObjectLessEqual(case2, 41, false): { return Operators.ConcatenateObject(Operators.SubtractObject(v, 21), " M/s"); } default: { return "无限制"; } } });
            this.SliderDebugAnim.GetHintText = new Func<object, object>(v => Conversions.ToBoolean(Operators.ConditionalCompareObjectGreater(v, 29, false)) ? "关闭" : Operators.ConcatenateObject(Operators.AddObject(Operators.DivideObject(v, 10), 0.1d), "x"));
            this.SliderAniFPS.GetHintText = new Func<object, string>(v => $"{Operators.AddObject(v, 1)} FPS");
        }
        private void SliderDownloadThread_PreviewChange(object sender, ModBase.RouteEventArgs e)
        {
            if (this.SliderDownloadThread.Value < 100)
                return;
            if (Conversions.ToBoolean(!(bool)ModBase.Setup.Get("HintDownloadThread")))
            {
                ModBase.Setup.Set("HintDownloadThread", true);
                ModMain.MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" + Constants.vbCrLf + "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn: true);
            }
        }

        // 硬件加速
        private void Check_DisableHardwareAcceleration(object sender, bool user)
        {
            ModMain.Hint("此项变更将在重启 PCL 后生效");
        }

        // 调试模式
        private void CheckDebugMode_Change()
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModMain.Hint("部分调试信息将在刷新或启动器重启后切换显示！", Log: false);
        }

        // 自动更新
        private void ComboSystemActivity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (this.ComboSystemActivity.SelectedIndex != 2)
                return;
            if (ModMain.MyMsgBox("若选择此项，即使在将来出现严重问题时，你也无法获取相关通知。" + Constants.vbCrLf + "例如，如果发现某个版本游戏存在严重 Bug，你可能就会因为无法得到通知而导致无法预知的后果。" + Constants.vbCrLf + Constants.vbCrLf + "一般选择 仅在有重要通知时显示公告 就可以让你尽量不受打扰了。" + Constants.vbCrLf + "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn: true) == 2)
            {
                this.ComboSystemActivity.SelectedItem = e.RemovedItems[0];
            }
        }
        private void ComboSystemUpdate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (this.ComboSystemUpdate.SelectedIndex != 3)
                return;
            if (ModMain.MyMsgBox("若选择此项，即使在启动器将来出现严重问题时，你也无法获取更新并获得修复。" + Constants.vbCrLf + "例如，如果官方修改了登录方式，从而导致现有启动器无法登录，你可能就会因为无法更新而无法开始游戏。" + Constants.vbCrLf + Constants.vbCrLf + "一般选择 仅在有重大漏洞更新时显示提示 就可以让你尽量不受打扰了。" + Constants.vbCrLf + "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn: true) == 2)
            {
                this.ComboSystemUpdate.SelectedItem = e.RemovedItems[0];
            }
        }
        private void ComboSystemUpdateBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (this.ComboSystemUpdateBranch.SelectedIndex != 1)
                return;
            if (ModMain.MyMsgBox("你正在切换启动器更新通道到 Fast Ring。" + Constants.vbCrLf + "Fast Ring 可以提供下个版本更新内容的预览，但可能会包含未经充分测试的功能，稳定性欠佳。" + Constants.vbCrLf + Constants.vbCrLf + "在升级到 Fast Ring 版本后，如果你选择切换到 Slow Ring，需要等待下一个 Slow Ring 版本发布，在这期间不会提供更新。" + Constants.vbCrLf + "该选项仅推荐具有一定基础知识和能力的用户选择。如果你正在制作整合包，请使用 Slow Ring！", "继续之前...", "我已知晓", "取消", IsWarn: true) == 2)
            {
                this.ComboSystemUpdateBranch.SelectedItem = e.RemovedItems[0];
            }
            else
            {
                ModSecret.UpdateCheckByButton();
            }
        }
        private void BtnSystemUpdate_Click(object sender, EventArgs e)
        {
            ModSecret.UpdateCheckByButton();
        }
        /// <summary>
    /// 启动器是否已经是最新版？
    /// 若返回 Nothing，则代表无更新缓存文件或出错。
    /// </summary>
        public static bool? IsLauncherNewest()
        {
            try
            {
                // 确认服务器公告是否正常
                string ServerContent = ModBase.ReadFile(ModBase.PathTemp + @"Cache\Notice.cfg");
                if (ServerContent.Split("|").Count() < 3)
                    return default;
                // 确认是否为最新
                /* TODO ERROR: Skipped IfDirectiveTrivia
                #If RELEASE Then
                *//* TODO ERROR: Skipped DisabledTextTrivia
                            Dim NewVersionCode As Integer = ServerContent.Split("|")(2)
                *//* TODO ERROR: Skipped ElseDirectiveTrivia
                #Else
                */
                int NewVersionCode = Conversions.ToInteger(ServerContent.Split("|")[1]);
                /* TODO ERROR: Skipped EndIfDirectiveTrivia
                #End If
                */
                return NewVersionCode <= ModBase.VersionCode;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "确认启动器更新失败", ModBase.LogLevel.Feedback);
                return default;
            }
        }

        #region 导出 / 导入设置

        private void BtnSystemSettingExp_Click(object sender, MouseButtonEventArgs e)
        {
            ModMain.Hint("该功能尚在开发中！");
        }
        private void BtnSystemSettingImp_Click(object sender, MouseButtonEventArgs e)
        {
            ModMain.Hint("该功能尚在开发中！");
        }

        #endregion

    }
}