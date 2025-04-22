using System;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadFabric
    {
        public PageDownloadFabric()
        {
            this.Initialized += (_, __) => LoaderInit();
            this.Loaded += (_, __) => Init();
        }

        private void LoaderInit()
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.CardVersions, this.CardTip, ModDownload.DlFabricListLoader, (_) => Load_OnFinish());
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
                JArray Versions = (JArray)ModDownload.DlFabricListLoader.Output.Value["installer"];
                this.PanVersions.Children.Clear();
                foreach (var Version in Versions)
                    this.PanVersions.Children.Add(ModDownloadLib.FabricDownloadListItem((JObject)Version, (_, __) => this.Fabric_Selected()));
                this.CardVersions.Title = "版本列表 (" + Versions.Count + ")";
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Fabric 版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        private void Fabric_Selected(MyListItem sender, EventArgs e)
        {
            ModDownloadLib.McDownloadFabricLoaderSave((JObject)sender.Tag);
        }

        private void BtnWeb_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://www.fabricmc.net");
        }

    }
}