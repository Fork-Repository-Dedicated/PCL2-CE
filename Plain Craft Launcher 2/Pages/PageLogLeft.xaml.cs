using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageLogLeft
    {
        public List<KeyValuePair<int, ModWatcher.Watcher>> ShownLogs = new List<KeyValuePair<int, ModWatcher.Watcher>>();
        public Dictionary<int, FlowDocument> FlowDocuments = new Dictionary<int, FlowDocument>();
        public int CurrentUuid;
        public ModWatcher.Watcher CurrentLog;
        public int IsLoading = 0;

        public PageLogLeft()
        {
            this.Loaded += PageLogLeft_Loaded;
            this.Unloaded += PageLogLeft_Unloaded;
        }
        private void PageLogLeft_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
            ModMain.FrmMain.BtnExtraLog.ShowRefresh();
        }
        private void PageLogLeft_Unloaded(object sender, RoutedEventArgs e)
        {
            ModMain.FrmMain.BtnExtraLog.ShowRefresh();
        }
        private void Refresh()
        {
            try
            {
                if (ShownLogs.Count == 0)
                {
                    ModMain.FrmMain.PageChange((FormMain.PageStackData)ModMain.FrmMain.PageCurrentSub);
                    return;
                }
                IsLoading += 1;

                // 创建 UI
                ModMain.FrmLogLeft.PanList.Children.Clear();

                // 测试核心列表
                // TODO(i18n): 文本 @ PageLog 左侧 - 列表标题
                ModMain.FrmLogLeft.PanList.Children.Add(new TextBlock() { Text = "测试版本列表", Margin = new Thickness(13d, 18d, 5d, 4d), Opacity = 0.6d, FontSize = 12d });
                foreach (var item in ShownLogs)
                {
                    // 添加控件
                    int Uuid = item.Key;
                    var Version = item.Value.Version;
                    var Proc = item.Value.GameProcess;
                    var NewItem = new MyListItem() { IsScaleAnimationEnabled = false, Type = MyListItem.CheckType.RadioBox, MinPaddingRight = 30, Title = Version.Name, Info = $"{Version.Version} - {Proc.StartTime:HH:mm:ss}", Height = 40d, Tag = Uuid };
                    NewItem.Changed += ModMain.FrmLogLeft.Version_Change;
                    // Dim KillButton As New MyIconButton With {.Logo = Logo.IconButtonCross, .LogoScale = 0.85}
                    var RemoveButton = new MyIconButton() { Logo = ModBase.Logo.IconButtonDelete, LogoScale = 1.1d };
                    // AddHandler KillButton.Click, AddressOf FrmLogLeft.Kill_Click
                    RemoveButton.Click += (_, __) => ModMain.FrmLogLeft.Remove_Click();
                    NewItem.Buttons = new[] { RemoveButton };
                    if (Uuid == CurrentUuid)
                        NewItem.Checked = true;
                    ModMain.FrmLogLeft.PanList.Children.Add(NewItem);
                }
                IsLoading -= 1;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "构建游戏实时日志 UI 出错", ModBase.LogLevel.Feedback);
            }
        }
        private void OnLogOutput(ModWatcher.Watcher sender, ModWatcher.LogOutputEventArgs e)
        {
            foreach (var item in ShownLogs)
            {
                if (item.Value.GameProcess.Id == sender.GameProcess.Id)
                {
                    int uuid = item.Key;
                    Thickness margin;
                    if (item.Value.GameProcess.HasExited)
                    {
                        margin = new Thickness(0d, 12d, 0d, 0d);
                    }
                    else
                    {
                        margin = new Thickness(0d);
                    }
                    ModBase.RunInUi(() =>
                        {
                            var paragraph = new Paragraph(new Run(e.LogText)) { Foreground = e.Color, Margin = margin };
                            FlowDocuments[uuid].Blocks.Add(paragraph);
                        });
                    return;
                }
            }
        }
        public void Add(ModWatcher.Watcher watcher)
        {
            int uuid = ModBase.GetUuid();
            ShownLogs.Add(new KeyValuePair<int, ModWatcher.Watcher>(uuid, watcher));
            watcher.LogOutput += OnLogOutput;
            ModBase.RunInUi(() => FlowDocuments.Add(uuid, new FlowDocument())); // TODO：在 UI 线程创建
            SelectionChange(uuid);
            ModMain.FrmMain.BtnExtraLog.ShowRefresh();
        }
        public void SelectionChange(int Uuid)
        {
            if (IsLoading > 0)
                return;
            // If CurrentUuid > 0 Then FlowDocuments(CurrentUuid) = FrmLogRight.PanLog.Document
            if (Uuid <= 0)
            {
                CurrentUuid = -1;
                CurrentLog = null;
            }
            else
            {
                foreach (var item in ShownLogs)
                {
                    if (item.Key == Uuid)
                    {
                        CurrentUuid = Uuid;
                        CurrentLog = item.Value;
                        break;
                    }
                }
            }
            ModBase.RunInUi(() =>
                {
                    ModMain.FrmLogRight.Refresh();
                    Refresh();
                });
        }
        public void RemoveItem(int Uuid)
        {
            for (int i = 0, loopTo = ShownLogs.Count - 1; i <= loopTo; i++)
            {
                var item = ShownLogs[i];
                if (item.Key != Uuid)
                    continue;
                ShownLogs.RemoveAt(i);
                if (CurrentUuid == item.Key)
                {
                    if (ShownLogs.Count == 0)
                    {
                        // 没有可以显示的了
                        SelectionChange(-1);
                    }
                    else
                    {
                        SelectionChange(ShownLogs[new[] { new[] { i, ShownLogs.Count - 1 }.Min(), 0 }.Max()].Key);
                    }
                }
                else
                {
                    ModBase.RunInUi(() =>
                        {
                            ModMain.FrmLogRight.Refresh();
                            Refresh();
                        });
                }
                break;
            }
            ModMain.FrmMain.BtnExtraLog.ShowRefresh();
        }
        // Public Sub Kill_Click(sender As Object, e As RoutedEventArgs)
        // Dim Uuid As Integer = (CType(CType(sender, MyIconButton).Parent, MyListItem).Tag)
        // For Each item In ShownLogs
        // If item.Key = Uuid Then
        // item.Value.proc.Kill()
        // End If
        // Next
        // End Sub
        public void Remove_Click(object sender, RoutedEventArgs e)
        {
            RemoveItem(Conversions.ToInteger(((MyListItem)((MyIconButton)sender).Parent).Tag));
        }

        // 点击选项
        public void Version_Change(object sender, ModBase.RouteEventArgs e)
        {
            SelectionChange(Conversions.ToInteger(((MyListItem)sender).Tag));
        }

    }
}