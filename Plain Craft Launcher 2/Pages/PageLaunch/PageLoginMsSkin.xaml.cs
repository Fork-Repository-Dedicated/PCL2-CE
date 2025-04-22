using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageLoginMsSkin
    {

        public PageLoginMsSkin()
        {
            this.InitializeComponent();
            this.Skin.Loader = PageLaunchLeft.SkinMs;
            this.Loaded += PageLoginLegacy_Loaded;
        }
        private void PageLoginLegacy_Loaded(object sender, RoutedEventArgs e)
        {
            this.Skin.Loader.Start();
        }

        /// <summary>
    /// 刷新页面显示的所有信息。
    /// </summary>
        public void Reload(bool KeepInput)
        {
            this.TextName.Text = Conversions.ToString(ModBase.Setup.Get("CacheMsV2Name"));
            // 皮肤在 Loaded 加载
        }
        /// <summary>
    /// 获取当前页面的登录信息。
    /// </summary>
        public static ModLaunch.McLoginMs GetLoginData()
        {
            if (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Finished)
            {
                return new ModLaunch.McLoginMs() { OAuthRefreshToken = Conversions.ToString(ModBase.Setup.Get("CacheMsV2OAuthRefresh")), UserName = Conversions.ToString(ModBase.Setup.Get("CacheMsV2Name")), AccessToken = Conversions.ToString(ModBase.Setup.Get("CacheMsV2Access")), Uuid = Conversions.ToString(ModBase.Setup.Get("CacheMsV2Uuid")), ProfileJson = Conversions.ToString(ModBase.Setup.Get("CacheMsV2ProfileJson")) };
            }
            else
            {
                return new ModLaunch.McLoginMs() { OAuthRefreshToken = Conversions.ToString(ModBase.Setup.Get("CacheMsV2OAuthRefresh")), UserName = Conversions.ToString(ModBase.Setup.Get("CacheMsV2Name")) };
            }
        }

        #region 下边栏其他内容

        // 显示/隐藏控制
        private void ShowPanel(object sender, MouseEventArgs e)
        {
            ModAnimation.AniStart(ModAnimation.AaOpacity(this.PanButtons, 1d - this.PanButtons.Opacity, 120), "PageLoginMsSkin Button");
        }
        public void HidePanel()
        {
            if (this.BtnEdit.ContextMenu.IsOpen || this.BtnSkin.ContextMenu.IsOpen || this.PanData.IsMouseOver)
                return;
            ModAnimation.AniStart(ModAnimation.AaOpacity(this.PanButtons, -this.PanButtons.Opacity, 120), "PageLoginMsSkin Button");
        }

        // 修改账号信息
        private void BtnEdit_Click(object sender, EventArgs e)
        {
            this.BtnEdit.ContextMenu.IsOpen = true;
        }
        public void BtnEditPassword_Click(object sender, RoutedEventArgs e)
        {
            ModBase.OpenWebsite("https://account.live.com/password/Change");
        }
        public void BtnEditName_Click(object sender, RoutedEventArgs e)
        {
            ModBase.OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
        }

        // 退出登录
        private void BtnExit_Click()
        {
            ModBase.Setup.Set("CacheMsV2OAuthRefresh", "");
            ModBase.Setup.Set("CacheMsV2Access", "");
            ModBase.Setup.Set("CacheMsV2ProfileJson", "");
            ModBase.Setup.Set("CacheMsV2Uuid", "");
            ModBase.Setup.Set("CacheMsV2Name", "");
            ModLaunch.McLoginMsLoader.Abort();
            ModMain.FrmLaunchLeft.RefreshPage(false, true);
        }

        #endregion

        #region 皮肤/披风

        // 展开
        private void BtnSkin_Click(object sender, RoutedEventArgs e)
        {
            this.BtnSkin.ContextMenu.IsOpen = true;
        }

        // 修改皮肤
        private bool IsChanging = false;
        public void BtnSkinEdit_Click(object sender, RoutedEventArgs e)
        {
            // 检查条件，获取新皮肤
            if (IsChanging)
            {
                ModMain.Hint("正在更改皮肤中，请稍候！");
                return;
            }
            if (ModLaunch.McLoginLoader.State == ModBase.LoadState.Failed)
            {
                ModMain.Hint("登录失败，无法更改皮肤！", ModMain.HintType.Critical);
                return;
            }
            var SkinInfo = ModMinecraft.McSkinSelect();
            if (!SkinInfo.IsVaild)
                return;
            ModMain.Hint("正在更改皮肤……");
            IsChanging = true;
            // 开始实际获取


            // 获取新皮肤地址
            ModBase.RunInNewThread(async () => { try { Retry:; if (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Loading) ModLaunch.McLoginMsLoader.WaitForExit(); string AccessToken = Conversions.ToString(ModBase.Setup.Get("CacheMsV2Access")); string Uuid = Conversions.ToString(ModBase.Setup.Get("CacheMsV2Uuid")); var Client = new System.Net.Http.HttpClient() { Timeout = new TimeSpan(0, 0, 30) }; Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken); Client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*")); Client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("MojangSharp", "0.1")); var Contents = new System.Net.Http.MultipartFormDataContent() { { new System.Net.Http.StringContent(SkinInfo.IsSlim ? "slim" : "classic"), "variant" }, { new System.Net.Http.ByteArrayContent(ModBase.ReadFileBytes(SkinInfo.LocalFile)), "file", ModBase.GetFileNameFromPath(SkinInfo.LocalFile) } }; string Result = await (await Client.PostAsync(new Uri("https://api.minecraftservices.com/minecraft/profile/skins"), Contents)).Content.ReadAsStringAsync(); if (Result.Contains("request requires user authentication")) { ModMain.Hint("正在登录，将在登录完成后继续更改皮肤……"); ModLaunch.McLoginMsLoader.Start(GetLoginData(), IsForceRestart: true); goto Retry; } else if (Result.Contains("\"error\"")) { ModMain.Hint(Conversions.ToString(Operators.ConcatenateObject("更改皮肤失败：", ModBase.GetJson(Result)("error"))), ModMain.HintType.Critical); return; } ModBase.Log("[Skin] 皮肤修改返回值：" + Constants.vbCrLf + Result); JObject ResultJson = (JObject)ModBase.GetJson(Result); if (ResultJson.ContainsKey("errorMessage")) throw new Exception(ResultJson["errorMessage"].ToString()); foreach (JObject Skin in ResultJson["skins"]) { if (Skin["state"].ToString() == "ACTIVE") { MySkin.ReloadCache((string)Skin["url"]); return; } } throw new Exception("未知错误（" + Result + "）"); } catch (Exception ex) { if (ex.GetType().Equals(typeof(System.Threading.Tasks.TaskCanceledException))) { ModMain.Hint("更改皮肤失败：与 Mojang 皮肤服务器的连接超时，请检查你的网络是否通畅！", ModMain.HintType.Critical); } else { ModBase.Log(ex, "更改皮肤失败", ModBase.LogLevel.Hint); } } finally { IsChanging = false; } }, "Ms Skin Upload"); // 等待登录结束
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           // #5309
        }

        // 保存皮肤
        public void BtnSkinSave_Click(object sender, RoutedEventArgs e)
        {
            this.Skin.BtnSkinSave_Click();
        }

        // 刷新头像
        public void BtnSkinRefresh_Click(object sender, RoutedEventArgs e)
        {
            this.Skin.RefreshClick();
        }

        // 修改披风
        public void BtnSkinCape_Click(object sender, RoutedEventArgs e)
        {
            this.Skin.BtnSkinCape_Click();
        }

        #endregion

    }
}