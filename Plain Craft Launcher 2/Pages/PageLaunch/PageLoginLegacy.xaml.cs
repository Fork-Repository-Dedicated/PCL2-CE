using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{

    public partial class PageLoginLegacy
    {

        public PageLoginLegacy()
        {
            this.InitializeComponent();
            this.Skin.Loader = PageLaunchLeft.SkinLegacy;
            this.Loaded += PageLoginLegacy_Loaded;
        }
        private void PageLoginLegacy_Loaded(object sender, RoutedEventArgs e)
        {
            this.Skin.Loader.Start();
        }

        public bool IsReloaded = false;
        /// <summary>
    /// 刷新页面显示的所有信息。
    /// </summary>
        public void Reload(bool KeepInput)
        {
            if (KeepInput && IsReloaded) // 避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            {
                // 保留输入，只刷新下拉框列表
                string Input = this.ComboName.Text.Trim();
                this.ComboName.ItemsSource = Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginLegacyName"), "", false)) ? null : ModBase.Setup.Get("LoginLegacyName").ToString().Split("¨");
                this.ComboName.Text = Input;
            }
            // 不保留输入，刷新列表后自动选择第一项
            else if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LoginLegacyName"), "", false)))
            {
                this.ComboName.ItemsSource = (IEnumerable)null;
            }
            else
            {
                this.ComboName.ItemsSource = ModBase.Setup.Get("LoginLegacyName").ToString().Split("¨");
                this.ComboName.Text = ModBase.Setup.Get("LoginLegacyName").ToString().BeforeFirst("¨").Trim();
            }
            IsReloaded = true;
        }
        /// <summary>
    /// 获取当前页面的登录信息。
    /// </summary>
        public static ModLaunch.McLoginData GetLoginData()
        {
            string UserName = ModMain.FrmLoginLegacy is null ? "" : ModMain.FrmLoginLegacy.ComboName.Text.Replace("¨", "").Trim();
            return new ModLaunch.McLoginLegacy() { UserName = UserName, SkinType = Conversions.ToInteger(ModBase.Setup.Get("LaunchSkinType")), SkinName = Conversions.ToString(ModBase.Setup.Get("LaunchSkinID")) };
        }
        /// <summary>
    /// 当前页面的登录信息是否有效。
    /// </summary>
        public static string IsVaild(ModLaunch.McLoginLegacy LoginData)
        {
            if (string.IsNullOrEmpty(LoginData.UserName.Trim()))
                return "玩家名不能为空！";
            if (LoginData.UserName.Contains("\""))
                return "玩家名不能包含英文引号！";
            if (ModMinecraft.McVersionCurrent is not null && (ModMinecraft.McVersionCurrent.Version.McCodeMain == 20 && ModMinecraft.McVersionCurrent.Version.McCodeSub >= 3 || ModMinecraft.McVersionCurrent.Version.McCodeMain > 20) && LoginData.UserName.Trim().Length > 16)
            {
                return "自 1.20.3 起，玩家名至多只能包含 16 个字符！";
            }
            return "";
        }
        public string IsVaild()
        {
            return IsVaild((ModLaunch.McLoginLegacy)GetLoginData());
        }

        private void ComboName_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                int Index = ((MyTextBox)this.ComboName.Template.FindName("PART_EditableTextBox", this.ComboName)).CaretIndex;
                if (Index == this.ComboName.Text.Length || Index == 0)
                    e.Handled = true;
            }
        }
        private void ComboLegacy_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchSkinType"), 0, false)))
                PageLaunchLeft.SkinLegacy.Start(IsForceRestart: true);
            this.HintChinese.Visibility = ModBase.RegexCheck(this.ComboName.Text, "^[0-9A-Za-z_]*$") ? Visibility.Collapsed : Visibility.Visible;
        }
        private void Skin_Click()
        {
            if (Conversions.ToBoolean((ModBase.Setup.Get("UiHiddenPageSetup") || ModBase.Setup.Get("UiHiddenSetupLaunch")) && !PageSetupUI.HiddenForceShow))
            {
                ModMain.Hint("启动设置已被禁用！", ModMain.HintType.Critical);
            }
            else
            {
                ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch);
            } // 切换到皮肤设置页面
        }

    }
}