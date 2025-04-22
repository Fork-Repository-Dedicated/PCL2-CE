using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PCL
{
    public partial class PageLogRight
    {
        public PageLogRight()
        {
            this.Initialized += (_, __) => Init();
            this.Loaded += PageLogRight_Loaded;
        }
        public void Init()
        {
            this.PanLogCard.Inlines.Clear();
            this.PanLogCard.Inlines.Add(new Run("实时日志"));
            this.PanLogCard.Inlines.Add(new Run(" | "));
            LabDebug = new Run("0 Debug") { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushDebug"] };
            this.PanLogCard.Inlines.Add(LabDebug);
            this.PanLogCard.Inlines.Add(new Run(" | "));
            LabInfo = new Run("0 Info") { Foreground = (Brush)System.Windows.Application.Current.Resources[ModSecret.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"] };
            this.PanLogCard.Inlines.Add(LabInfo);
            this.PanLogCard.Inlines.Add(new Run(" | "));
            LabWarn = new Run("0 Warn") { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushWarn"] };
            this.PanLogCard.Inlines.Add(LabWarn);
            this.PanLogCard.Inlines.Add(new Run(" | "));
            LabError = new Run("0 Error") { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushError"] };
            this.PanLogCard.Inlines.Add(LabError);
            this.PanLogCard.Inlines.Add(new Run(" | "));
            LabFatal = new Run("0 Fatal") { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushFatal"] };
            this.PanLogCard.Inlines.Add(LabFatal);
        }
        public void Refresh()
        {
            // 初始化
            if (ModMain.FrmLogLeft.CurrentLog is null || ModMain.FrmLogLeft.CurrentUuid <= 0 || ModMain.FrmLogLeft.ShownLogs.Count == 0)
            {
                ModMain.FrmMain.PageChange(ModMain.FrmMain.PageCurrent);
                return;
            }
            this.PanAllBack.Visibility = Visibility.Visible;
            this.CardOperation.Visibility = Visibility.Visible;
            this.BtnOperationKill.IsEnabled = !ModMain.FrmLogLeft.CurrentLog.GameProcess.HasExited;
            // 绑定日志输出
            this.PanLog.Document = ModMain.FrmLogLeft.FlowDocuments[ModMain.FrmLogLeft.CurrentUuid];
            // 绑定事件
            ModMain.FrmLogLeft.CurrentLog.LogOutput += OnLogOutput;
            ModMain.FrmLogLeft.CurrentLog.GameExit += OnGameExit;
            RefreshLabText();
        }

        private void RefreshLabText()
        {
            // 刷新计数器

            LabFatal.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountFatal} Fatal";
            LabError.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountError} Error";
            LabWarn.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountWarn} Warn";
            LabInfo.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountInfo} Info";
            LabDebug.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountDebug} Debug";
        }

        public Run LabDebug = null;
        public Run LabInfo = null;
        public Run LabWarn = null;
        public Run LabError = null;
        public Run LabFatal = null;

        private void PageLogRight_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }
        private void OnLogOutput(ModWatcher.Watcher sender, ModWatcher.LogOutputEventArgs e)
        {
            ModBase.RunInUi(() => { if (ModMain.FrmLogLeft.CurrentLog is not null) { if (this.CheckAutoScroll.Checked) this.PanBack.ScrollToBottom(); RefreshLabText(); } });
        }

        #region 卡片按钮
        private void BtnOperationClear_Click(object sender, ModBase.RouteEventArgs e)
        {
            ModMain.FrmLogLeft.FlowDocuments[ModMain.FrmLogLeft.CurrentUuid].Blocks.Clear();
        }

        private void BtnOperationExport_Click(object sender, ModBase.RouteEventArgs e)
        {
            // TODO(i18n): 文本 @ 文件选择弹窗 - 窗口标题 & 类型选择器选项
            string SavePath = ModBase.SelectSaveFile("选择导出位置", $"游戏日志 - {ModMain.FrmLogLeft.CurrentLog.Version.Name}.log", "游戏日志(*.log)|*.log");
            if (SavePath.Length < 3)
                return;
            File.WriteAllLines(SavePath, ModMain.FrmLogLeft.CurrentLog.FullLog);
            // TODO(i18n): 文本 @ 左下角提示 - 导出成功提示
            ModMain.Hint("日志已导出！", ModMain.HintType.Finish);
            ModBase.OpenExplorer(SavePath);
        }

        private void BtnOperationKill_Click(object sender, ModBase.RouteEventArgs e)
        {
            if (ModMain.FrmLogLeft.CurrentLog.State <= ModWatcher.Watcher.MinecraftState.Running)
            {
                ModMain.FrmLogLeft.CurrentLog.Kill();
                // TODO(i18n): 文本 @ 左下角提示 - 客户端关闭提示
                ModMain.Hint($"已关闭游戏 {ModMain.FrmLogLeft.CurrentLog.Version.Name}！", ModMain.HintType.Finish);
            }
        }

        private void OnGameExit()
        {
            ModBase.RunInUi(() => this.BtnOperationKill.IsEnabled = false);
        }
        #endregion

    }
}