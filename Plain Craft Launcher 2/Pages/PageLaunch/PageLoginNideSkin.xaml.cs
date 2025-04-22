using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public partial class PageLoginNideSkin
    {

        public PageLoginNideSkin()
        {
            this.InitializeComponent();
            this.Skin.Loader = PageLaunchLeft.SkinNide;
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
            this.TextName.Text = Conversions.ToString(ModBase.Setup.Get("CacheNideName"));
            this.TextEmail.Text = Conversions.ToString(ModBase.Setup.Get("CacheNideUsername"));
            this.TextEmail.Visibility = Conversions.ToBoolean(ModBase.Setup.Get("UiLauncherEmail")) ? Visibility.Collapsed : Visibility.Visible;
            // 皮肤在 Loaded 加载
        }
        /// <summary>
    /// 获取当前页面的登录信息。
    /// </summary>
        public static ModLaunch.McLoginServer GetLoginData()
        {
            string Server = Conversions.ToString(ModMinecraft.McVersionCurrent == null ? ModBase.Setup.Get("CacheNideServer") : ModBase.Setup.Get("VersionServerNide", Version: ModMinecraft.McVersionCurrent));
            return new ModLaunch.McLoginServer(ModLaunch.McLoginType.Nide) { Token = "Nide", UserName = Conversions.ToString(ModBase.Setup.Get("CacheNideUsername")), Password = Conversions.ToString(ModBase.Setup.Get("CacheNidePass")), Description = "统一通行证", Type = ModLaunch.McLoginType.Nide, BaseUrl = "https://auth.mc-user.com:233/" + Server + "/authserver" };
        }

        private void PageLoginNideSkin_MouseEnter(object sender, MouseEventArgs e)
        {
            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.BtnEdit, 1d - this.BtnEdit.Opacity, 80), ModAnimation.AaHeight(this.BtnEdit, 25.5d - this.BtnEdit.Height, 140, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaHeight(this.BtnEdit, -1.5d, 50, 140, new ModAnimation.AniEaseInFluent()), ModAnimation.AaOpacity(this.BtnExit, 1d - this.BtnExit.Opacity, 80), ModAnimation.AaHeight(this.BtnExit, 25.5d - this.BtnExit.Height, 140, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaHeight(this.BtnExit, -1.5d, 50, 140, new ModAnimation.AniEaseInFluent()) }, "PageLoginNideSkin Button");
        }
        private void PageLoginNideSkin_MouseLeave(object sender, MouseEventArgs e)
        {
            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.BtnEdit, -this.BtnEdit.Opacity, 120, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaHeight(this.BtnEdit, 14d - this.BtnEdit.Height, 120, Ease: new ModAnimation.AniEaseInFluent()), ModAnimation.AaOpacity(this.BtnExit, -this.BtnExit.Opacity, 120, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaHeight(this.BtnExit, 14d - this.BtnExit.Height, 120, Ease: new ModAnimation.AniEaseInFluent()) }, "PageLoginNideSkin Button");
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://login.mc-user.com:233/account/changepw");
        }
        public static void ExitLogin()
        {
            ModBase.Setup.Set("CacheNideAccess", "");
            ModLaunch.McLoginNideLoader.Input = null; // 防止因为输入的用户名密码相同，直接使用了上次登录的加载器结果
            ModMain.FrmLaunchLeft.RefreshPage(false, true);
        }

        private void Skin_Click(object sender, MouseButtonEventArgs e)
        {
            ModBase.OpenWebsite(Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject("https://login.mc-user.com:233/", ModMinecraft.McVersionCurrent == null ? ModBase.Setup.Get("CacheNideServer") : ModBase.Setup.Get("VersionServerNide", Version: ModMinecraft.McVersionCurrent)), "/skin")));
        }

    }
}