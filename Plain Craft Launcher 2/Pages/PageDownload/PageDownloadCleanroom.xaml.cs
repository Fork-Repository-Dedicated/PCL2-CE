using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadCleanroom
    {
        public PageDownloadCleanroom()
        {
            this.Initialized += (_, __) => LoaderInit();
            this.Loaded += (_, __) => Init();
        }

        private void LoaderInit()
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanMain, this.CardTip, ModDownload.DlCleanroomListLoader, (_) => Load_OnFinish());
        }
        private void Init()
        {
            this.PanBack.ScrollToHome();
        }

        private void Load_OnFinish()
        {
            // 结果数据化
            try
            {
                // 归类
                var Dict = ModDownload.DlCleanroomListLoader.Output.Value.GroupBy(d => d.Inherit).OrderByDescending(g => g.Key).ToDictionary(g => g.Key, g => g.ToList());
                // 清空当前
                this.PanMain.Children.Clear();
                // 转化为 UI
                foreach (KeyValuePair<string, List<ModDownload.DlCleanroomListEntry>> Pair in Dict)
                {
                    if (!Pair.Value.Any())
                        continue;
                    // 增加卡片
                    var NewCard = new MyCard() { Title = Pair.Key + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                    var NewStack = new StackPanel() { Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d), Tag = Pair.Value };
                    NewCard.Children.Add(NewStack);
                    NewCard.SwapControl = NewStack;
                    NewCard.IsSwaped = true;
                    NewCard.InstallMethod = new Action<StackPanel>((Stack) => { foreach (var item in (IEnumerable)Stack.Tag) Stack.Children.Add(ModDownloadLib.CleanroomDownloadListItem((ModDownload.DlCleanroomListEntry)item, ModDownloadLib.CleanroomSave_Click, true)); });
                    this.PanMain.Children.Add(NewCard);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Cleanroom 版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 介绍栏
        private void BtnWeb_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://cleanroommc.com/zh/");
        }

    }
}