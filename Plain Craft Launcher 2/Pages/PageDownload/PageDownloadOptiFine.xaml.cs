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
    public partial class PageDownloadOptiFine
    {
        public PageDownloadOptiFine()
        {
            this.Initialized += (_, __) => LoaderInit();
            this.Loaded += (_, __) => Init();
        }

        private void LoaderInit()
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanMain, this.CardTip, ModDownload.DlOptiFineListLoader, (_) => Load_OnFinish());
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
                var Dict = new Dictionary<string, List<ModDownload.DlOptiFineListEntry>>();
                Dict.Add("快照版本", new List<ModDownload.DlOptiFineListEntry>());
                for (int VersionCode = 50; VersionCode >= 0; VersionCode -= 1)
                    Dict.Add("1." + VersionCode, new List<ModDownload.DlOptiFineListEntry>());
                foreach (ModDownload.DlOptiFineListEntry Version in ModDownload.DlOptiFineListLoader.Output.Value)
                {
                    if (Version.Inherit.StartsWith("1."))
                    {
                        string MainVersion = "1." + Version.NameDisplay.Split(".")[1].Split(" ")[0];
                        if (Dict.ContainsKey(MainVersion))
                        {
                            Dict[MainVersion].Add(Version);
                        }
                        else
                        {
                            Dict["快照版本"].Add(Version);
                        }
                    }
                    else
                    {
                        Dict["快照版本"].Add(Version);
                    }
                }
                // 清空当前
                this.PanMain.Children.Clear();
                // 转化为 UI
                foreach (KeyValuePair<string, List<ModDownload.DlOptiFineListEntry>> Pair in Dict)
                {
                    if (!Pair.Value.Any())
                        continue;
                    // 增加卡片
                    var NewCard = new MyCard() { Title = Pair.Key + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                    var NewStack = new StackPanel() { Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d), Tag = Pair.Value };
                    NewCard.Children.Add(NewStack);
                    NewCard.SwapControl = NewStack;
                    NewCard.IsSwaped = true;
                    NewCard.InstallMethod = new Action<StackPanel>((Stack) =>
                        {
                            Stack.Tag = ModBase.Sort((List<ModDownload.DlOptiFineListEntry>)Stack.Tag, (a, b) => ModMinecraft.VersionSortBoolean(a.NameDisplay, b.NameDisplay));
                            foreach (var item in (IEnumerable)Stack.Tag)
                                Stack.Children.Add(ModDownloadLib.OptiFineDownloadListItem((ModDownload.DlOptiFineListEntry)item, ModDownloadLib.OptiFineSave_Click, true));
                        });
                    this.PanMain.Children.Add(NewCard);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 OptiFine 版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        private void BtnWeb_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://www.optifine.net/");
        }

    }
}