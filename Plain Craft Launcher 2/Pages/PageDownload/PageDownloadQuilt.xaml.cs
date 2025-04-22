using System;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadQuilt
    {
        public PageDownloadQuilt()
        {
            this.Initialized += (_, __) => LoaderInit();
            this.Loaded += (_, __) => Init();
        }

        private void LoaderInit()
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.CardVersions, this.CardTip, ModDownload.DlQuiltListLoader, (_) => Load_OnFinish());
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
                JArray Versions = (JArray)ModDownload.DlQuiltListLoader.Output.Value["installer"];
                this.PanVersions.Children.Clear();
                foreach (var Version in Versions)
                    this.PanVersions.Children.Add(ModDownloadLib.QuiltDownloadListItem((JObject)Version, (_, __) => this.Quilt_Selected()));
                this.CardVersions.Title = "版本列表 (" + Versions.Count + ")";
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Quilt 版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        private void Quilt_Selected(MyListItem sender, EventArgs e)
        {
            ModDownloadLib.McDownloadQuiltLoaderSave((JObject)sender.Tag);
        }

        private void BtnWeb_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://quiltmc.org");
        }

    }
}