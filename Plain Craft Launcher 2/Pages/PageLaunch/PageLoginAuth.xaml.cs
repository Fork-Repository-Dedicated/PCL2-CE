using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public partial class PageLoginAuth
    {
        private bool IsFirstLoad = true;

        public PageLoginAuth()
        {
            this.Loaded += (_, __) => ReloadRegisterButton();
        }
        /// <summary>
    /// 刷新页面显示的所有信息。
    /// </summary>
        public void Reload(bool KeepInput)
        {
            // 记住密码
            this.CheckRemember.Checked = Conversions.ToBoolean(ModBase.Setup.Get("LoginRemember"));
            if (KeepInput && !IsFirstLoad) // 避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            {
                // 保留输入，只刷新下拉框列表
                string Input = this.ComboName.Text;
                this.ComboName.ItemsSource = Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginAuthEmail"), "", false)) ? null : ModBase.Setup.Get("LoginAuthEmail").ToString().Split("¨");
                this.ComboName.Text = Input;
            }
            // 不保留输入，刷新列表后自动选择第一项
            else if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginAuthEmail"), "", false)))
            {
                this.ComboName.ItemsSource = (IEnumerable)null;
            }
            else
            {
                this.ComboName.ItemsSource = ModBase.Setup.Get("LoginAuthEmail").ToString().Split("¨");
                this.ComboName.Text = ModBase.Setup.Get("LoginAuthEmail").ToString().BeforeFirst("¨");
                if (Conversions.ToBoolean(ModBase.Setup.Get("LoginRemember")))
                    this.TextPass.Password = ModBase.Setup.Get("LoginAuthPass").ToString().BeforeFirst("¨").Trim();
            }
            IsFirstLoad = false;
        }
        /// <summary>
    /// 获取当前页面的登录信息。
    /// </summary>
        public static ModLaunch.McLoginServer GetLoginData()
        {
            string Server = Conversions.ToString(Operators.ConcatenateObject(ModMinecraft.McVersionCurrent == null ? ModBase.Setup.Get("CacheAuthServerServer") : ModBase.Setup.Get("VersionServerAuthServer", Version: ModMinecraft.McVersionCurrent), "/authserver"));
            if (ModMain.FrmLoginAuth is null)
            {
                return new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth) { Token = "Auth", BaseUrl = Server, UserName = "", Password = "", Description = "Authlib-Injector", Type = ModLaunch.McLoginType.Auth };
            }
            else
            {
                return new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth) { Token = "Auth", BaseUrl = Server, UserName = ModMain.FrmLoginAuth.ComboName.Text.Replace("¨", "").Trim(), Password = ModMain.FrmLoginAuth.TextPass.Password.Replace("¨", "").Trim(), Description = "Authlib-Injector", Type = ModLaunch.McLoginType.Auth };
            }
        }
        /// <summary>
    /// 当前页面的登录信息是否有效。
    /// </summary>
        public static string IsVaild(ModLaunch.McLoginServer LoginData)
        {
            if (!string.IsNullOrEmpty(""))
                return "";
            if (string.IsNullOrEmpty(LoginData.UserName))
                return "账号不能为空！";
            if (string.IsNullOrEmpty(LoginData.Password))
                return "密码不能为空！";
            return "";
        }
        public string IsVaild()
        {
            return IsVaild(GetLoginData());
        }

        // 保存输入信息
        private void ComboName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)sender).Text, "", false)))
                this.TextPass.Password = "";
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set("CacheAuthAccess", "");  // 迫使其不进行 Validate
        }
        private void TextPass_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set("CacheAuthAccess", "");
        }
        private void ComboName_SelectionChanged(MyComboBox sender, SelectionChangedEventArgs e)
        {
            if (Conversions.ToBoolean(sender.SelectedIndex == -1 || !ModBase.Setup.Get("LoginRemember")))
            {
                this.TextPass.Password = "";
            }
            else
            {
                this.TextPass.Password = ModBase.Setup.Get("LoginAuthPass").ToString().Split("¨")[sender.SelectedIndex].Trim();
            }
        }
        private void CheckBoxChange(MyCheckBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Checked);
        }

        // 链接处理
        private void ComboName_TextChanged()
        {
            this.BtnLink.Content = string.IsNullOrEmpty(this.ComboName.Text) ? "注册账号" : "找回密码";
        }
        private void Btn_Click(object sender, EventArgs e)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.BtnLink.Content, "注册账号", false)))
            {
                ModBase.OpenWebsite(Conversions.ToString(ModMinecraft.McVersionCurrent is not null ? ModBase.Setup.Get("VersionServerAuthRegister", Version: ModMinecraft.McVersionCurrent) : ModBase.Setup.Get("CacheAuthServerRegister")));
            }
            else
            {
                string Website = Conversions.ToString(ModMinecraft.McVersionCurrent is not null ? ModBase.Setup.Get("VersionServerAuthRegister", Version: ModMinecraft.McVersionCurrent) : ModBase.Setup.Get("CacheAuthServerRegister"));
                ModBase.OpenWebsite(Website.Replace("/auth/register", "/auth/forgot"));
            }
        }
        // 切换注册按钮可见性
        private void ReloadRegisterButton()
        {
            string Address = Conversions.ToString(ModMinecraft.McVersionCurrent is not null ? ModBase.Setup.Get("VersionServerAuthRegister", Version: ModMinecraft.McVersionCurrent) : ModBase.Setup.Get("CacheAuthServerRegister"));
            this.BtnLink.Visibility = string.IsNullOrEmpty(new ValidateHttp().Validate(Address)) ? Visibility.Visible : Visibility.Collapsed;
        }

    }
}