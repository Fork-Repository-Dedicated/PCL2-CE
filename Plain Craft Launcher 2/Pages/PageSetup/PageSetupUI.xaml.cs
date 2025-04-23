using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageSetupUI
    {

        public new bool IsLoaded = false;

        public PageSetupUI()
        {
            this.Loaded += PageSetupUI_Loaded;
            this.Loaded += (_, __) => HiddenRefresh();
        }

        private void PageSetupUI_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();
            ModSecret.ThemeCheckAll(true);

            if (ModSecret.ThemeDontClick != 0)
            {
                string NewText;
                switch (ModSecret.ThemeDontClick)
                {
                    case 1:
                        {
                            NewText = "眼瞎白";
                            break;
                        }
                    case 2:
                        {
                            NewText = "真·滑稽彩";
                            break;
                        }

                    default:
                        {
                            NewText = "？？？";
                            break;
                        }
                }
                foreach (var Control in this.PanLauncherTheme.Children)
                {
                    if (Control is MyRadioBox && ((MyRadioBox)Control).IsEnabled)
                    {
                        ((MyRadioBox)Control).Text = NewText;
                    }
                }
            }

            ModAnimation.AniControlEnabled += 1;
            Reload(); // #4826，在每次进入页面时都刷新一下
            ModAnimation.AniControlEnabled -= 1;

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

            SliderLoad();

            this.PanLauncherHide.Visibility = Visibility.Visible;

            // 设置解锁

            if (!this.RadioLauncherTheme8.IsEnabled)
                this.LabLauncherTheme8Copy.ToolTip = "社区版不包含主题功能，请使用官方快照版";
            this.RadioLauncherTheme8.ToolTip = "社区版不包含主题功能，请使用官方快照版";
            if (!this.RadioLauncherTheme9.IsEnabled)
                this.LabLauncherTheme9Copy.ToolTip = "社区版不包含主题功能，请使用官方快照版";
            this.RadioLauncherTheme9.ToolTip = "社区版不包含主题功能，请使用官方快照版";
            // 极客蓝的处理在 ThemeCheck 中

        }
        public void Reload()
        {
            try
            {

                // 启动器
                this.SliderLauncherOpacity.Value = Conversions.ToInteger(ModBase.Setup.Get("UiLauncherTransparent"));
                this.SliderLauncherHue.Value = Conversions.ToInteger(ModBase.Setup.Get("UiLauncherHue"));
                this.SliderLauncherSat.Value = Conversions.ToInteger(ModBase.Setup.Get("UiLauncherSat"));
                this.SliderLauncherDelta.Value = Conversions.ToInteger(ModBase.Setup.Get("UiLauncherDelta"));
                this.SliderLauncherLight.Value = Conversions.ToInteger(ModBase.Setup.Get("UiLauncherLight"));
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectLessEqual(ModBase.Setup.Get("UiLauncherTheme"), 14, false)))
                    ((MyRadioBox)this.FindName(Conversions.ToString(Operators.ConcatenateObject("RadioLauncherTheme", ModBase.Setup.Get("UiLauncherTheme"))))).Checked = true;
                this.CheckLauncherLogo.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiLauncherLogo"));
                this.CheckLauncherEmail.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiLauncherEmail"));
                this.ComboDarkMode.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("UiDarkMode"));
                this.ComboUiFont.Items.Clear();
                this.ComboUiFont.Items.Add(new MyComboBoxItem()
                {
                    Content = new TextBlock()
                    {
                        Text = "默认",
                        FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI"),
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Left
                    },
                    Tag = ""
                });
                foreach (var Font in Fonts.SystemFontFamilies)
                    this.ComboUiFont.Items.Add(new MyComboBoxItem()
                    {
                        Content = new TextBlock()
                        {
                            Text = Font.Source,
                            FontFamily = new FontFamily(Font.Source),
                            IsHitTestVisible = false,
                            HorizontalAlignment = HorizontalAlignment.Left
                        },
                        Tag = Font.Source
                    });
                if (string.IsNullOrWhiteSpace(Conversions.ToString(ModBase.Setup.Get("UiFont"))))
                {
                    this.ComboUiFont.SelectedIndex = 0;
                }
                else
                {
                    this.ComboUiFont.SelectedIndex = this.ComboUiFont.Items.IndexOf(ComboUiFont.Items.Cast<MyComboBoxItem>().Where(x => Operators.ConditionalCompareObjectEqual(x.Tag, ModBase.Setup.Get("UiFont"), false)).FirstOrDefault());
                }

                // 背景图片
                this.SliderBackgroundOpacity.Value = Conversions.ToInteger(ModBase.Setup.Get("UiBackgroundOpacity"));
                this.SliderBackgroundBlur.Value = Conversions.ToInteger(ModBase.Setup.Get("UiBackgroundBlur"));
                this.ComboBackgroundSuit.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("UiBackgroundSuit"));
                this.CheckBackgroundColorful.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiBackgroundColorful"));
                BackgroundRefresh(false, false);

                // 标题栏
                ((MyRadioBox)this.FindName(Conversions.ToString(Operators.ConcatenateObject("RadioLogoType", ModBase.Setup.Get("UiLogoType"))))).Checked = true;
                this.CheckLogoLeft.Visibility = this.RadioLogoType0.Checked ? Visibility.Visible : Visibility.Collapsed;
                this.PanLogoText.Visibility = this.RadioLogoType2.Checked ? Visibility.Visible : Visibility.Collapsed;
                this.PanLogoChange.Visibility = this.RadioLogoType3.Checked ? Visibility.Visible : Visibility.Collapsed;
                this.TextLogoText.Text = Conversions.ToString(ModBase.Setup.Get("UiLogoText"));
                this.CheckLogoLeft.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiLogoLeft"));

                // 背景音乐
                this.CheckMusicRandom.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiMusicRandom"));
                this.CheckMusicAuto.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiMusicAuto"));
                this.CheckMusicStop.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiMusicStop"));
                this.CheckMusicStart.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiMusicStart"));
                this.CheckMusicSMTC.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiMusicSMTC"));
                this.SliderMusicVolume.Value = Conversions.ToInteger(ModBase.Setup.Get("UiMusicVolume"));
                MusicRefreshUI();

                // 主页
                try
                {
                    this.ComboCustomPreset.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("UiCustomPreset"));
                }
                catch
                {
                    ModBase.Setup.Reset("UiCustomPreset");
                } ((MyRadioBox)this.FindName(Conversions.ToString(Operators.ConcatenateObject("RadioCustomType", ModBase.Setup.Load("UiCustomType"))))).Checked = true;
                this.TextCustomNet.Text = Conversions.ToString(ModBase.Setup.Get("UiCustomNet"));

                // 功能隐藏
                this.CheckHiddenPageDownload.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenPageDownload"));
                this.CheckHiddenPageLink.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenPageLink"));
                this.CheckHiddenPageSetup.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenPageSetup"));
                this.CheckHiddenPageOther.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenPageOther"));
                this.CheckHiddenFunctionSelect.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenFunctionSelect"));
                this.CheckHiddenFunctionModUpdate.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenFunctionModUpdate"));
                this.CheckHiddenFunctionHidden.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenFunctionHidden"));
                this.CheckHiddenSetupLaunch.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenSetupLaunch"));
                this.CheckHiddenSetupUI.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenSetupUi"));
                this.CheckHiddenSetupLink.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenSetupLink"));
                this.CheckHiddenSetupSystem.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenSetupSystem"));
                this.CheckHiddenOtherAbout.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenOtherAbout"));
                this.CheckHiddenOtherFeedback.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenOtherFeedback"));
                this.CheckHiddenOtherVote.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenOtherVote"));
                this.CheckHiddenOtherHelp.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenOtherHelp"));
                this.CheckHiddenOtherTest.Checked = Conversions.ToBoolean(ModBase.Setup.Get("UiHiddenOtherTest"));
            }

            catch (NullReferenceException ex)
            {
                ModBase.Log(ex, "个性化设置项存在异常，已被自动重置", ModBase.LogLevel.Msgbox);
                Reset();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重载个性化设置时出错", ModBase.LogLevel.Feedback);
            }
        }

        // 初始化
        public void Reset()
        {
            try
            {
                ModBase.Setup.Reset("UiLauncherTransparent");
                ModBase.Setup.Reset("UiLauncherTheme");
                ModBase.Setup.Reset("UiLauncherLogo");
                ModBase.Setup.Reset("UiLauncherHue");
                ModBase.Setup.Reset("UiLauncherSat");
                ModBase.Setup.Reset("UiLauncherDelta");
                ModBase.Setup.Reset("UiLauncherLight");
                ModBase.Setup.Reset("UiLauncherEmail");
                ModBase.Setup.Reset("UiBackgroundColorful");
                ModBase.Setup.Reset("UiBackgroundOpacity");
                ModBase.Setup.Reset("UiBackgroundBlur");
                ModBase.Setup.Reset("UiBackgroundSuit");
                ModBase.Setup.Reset("UiDarkMode");
                ModBase.Setup.Reset("UiFont");
                ModBase.Setup.Reset("UiLogoType");
                ModBase.Setup.Reset("UiLogoText");
                ModBase.Setup.Reset("UiLogoLeft");
                ModBase.Setup.Reset("UiMusicVolume");
                ModBase.Setup.Reset("UiMusicStop");
                ModBase.Setup.Reset("UiMusicStart");
                ModBase.Setup.Reset("UiMusicRandom");
                ModBase.Setup.Reset("UiMusicSMTC");
                ModBase.Setup.Reset("UiMusicAuto");
                ModBase.Setup.Reset("UiCustomType");
                ModBase.Setup.Reset("UiCustomPreset");
                ModBase.Setup.Reset("UiCustomNet");
                ModBase.Setup.Reset("UiHiddenPageDownload");
                ModBase.Setup.Reset("UiHiddenPageLink");
                ModBase.Setup.Reset("UiHiddenPageSetup");
                ModBase.Setup.Reset("UiHiddenPageOther");
                ModBase.Setup.Reset("UiHiddenFunctionSelect");
                ModBase.Setup.Reset("UiHiddenFunctionModUpdate");
                ModBase.Setup.Reset("UiHiddenFunctionHidden");
                ModBase.Setup.Reset("UiHiddenSetupLaunch");
                ModBase.Setup.Reset("UiHiddenSetupUi");
                ModBase.Setup.Reset("UiHiddenSetupLink");
                ModBase.Setup.Reset("UiHiddenSetupSystem");
                ModBase.Setup.Reset("UiHiddenOtherAbout");
                ModBase.Setup.Reset("UiHiddenOtherFeedback");
                ModBase.Setup.Reset("UiHiddenOtherVote");
                ModBase.Setup.Reset("UiHiddenOtherHelp");
                ModBase.Setup.Reset("UiHiddenOtherTest");

                ModBase.Log("[Setup] 已初始化个性化设置！");
                ModMain.Hint("已初始化个性化设置", ModMain.HintType.Finish, false);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化个性化设置失败", ModBase.LogLevel.Msgbox);
            }

            Reload();
        }

        // 将控件改变路由到设置改变
        private static void SliderChange(MySlider sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Value);
        }
        private static void ComboChange(MyComboBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.SelectedIndex);
        }
        private static void CheckBoxChange(MyCheckBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Checked);
        }
        private static void TextBoxChange(MyTextBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Text);
        }
        private static void RadioBoxChange(MyRadioBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModBase.Setup.Set(sender.Tag.ToString().Split("/")[0], ModBase.Val(sender.Tag.ToString().Split("/")[1]));
        }

        private void ComboFontChange(MyComboBox sender, object e)
        {
            if (ModAnimation.AniControlEnabled == 0)
            {
                if (sender.SelectedIndex == 0)
                {
                    ModBase.Setup.Set("UiFont", "");
                    ModBase.SetLaunchFont();
                }
                else
                {
                    ModBase.Setup.Set("UiFont", ((dynamic)sender.SelectedItem).Tag);
                    ModBase.SetLaunchFont(Conversions.ToString(((dynamic)sender.SelectedItem).Tag));
                }
            }
        }

        // 背景图片
        private void BtnUIBgOpen_Click(object sender, EventArgs e)
        {
            ModBase.OpenExplorer(ModBase.Path + @"PCL\Pictures\");
        }
        private void BtnBackgroundRefresh_Click(object sender, EventArgs e)
        {
            BackgroundRefresh(true, true);
        }
        public void BackgroundRefreshUI(bool Show, int Count)
        {
            if (this.PanBackgroundOpacity == null)
                return;
            if (Show)
            {
                this.PanBackgroundOpacity.Visibility = Visibility.Visible;
                this.PanBackgroundBlur.Visibility = Visibility.Visible;
                this.PanBackgroundSuit.Visibility = Visibility.Visible;
                this.BtnBackgroundClear.Visibility = Visibility.Visible;
                this.CardBackground.Title = "背景图片（" + Count + " 张）";
            }
            else
            {
                this.PanBackgroundOpacity.Visibility = Visibility.Collapsed;
                this.PanBackgroundBlur.Visibility = Visibility.Collapsed;
                this.PanBackgroundSuit.Visibility = Visibility.Collapsed;
                this.BtnBackgroundClear.Visibility = Visibility.Collapsed;
                this.CardBackground.Title = "背景图片";
            }
            this.CardBackground.TriggerForceResize();
        }
        private void BtnBackgroundClear_Click(object sender, EventArgs e)
        {
            if (ModMain.MyMsgBox("即将删除背景图片文件夹中的所有文件。" + Constants.vbCrLf + "此操作不可撤销，是否确定？", "警告", Button2: "取消", IsWarn: true) == 1)
            {
                ModBase.DeleteDirectory(ModBase.Path + @"PCL\Pictures");
                BackgroundRefresh(false, true);
                ModMain.Hint("背景图片已清空！", ModMain.HintType.Finish);
            }
        }
        /// <summary>
    /// 刷新背景图片及设置页 UI。
    /// </summary>
    /// <param name="IsHint">是否显示刷新提示。</param>
    /// <param name="Refresh">是否刷新图片显示。</param>
        public static void BackgroundRefresh(bool IsHint, bool Refresh)
        {
            try
            {

                // 获取可用的图片文件
                Directory.CreateDirectory(ModBase.Path + @"PCL\Pictures\");
                var Pic = new List<string>();
                foreach (var File in ModBase.EnumerateFiles(ModBase.Path + @"PCL\Pictures\"))
                {
                    if (File.Extension.ToLower() != ".ini" && File.Extension.ToLower() != ".db") // 文件夹可能会被加入 .ini 和 thumbs.db
                    {
                        Pic.Add(File.FullName);
                    }
                }
                // 加载
                if (!Pic.Any())
                {
                    if (Refresh)
                    {
                        if (ModMain.FrmMain.ImgBack.Visibility == Visibility.Collapsed)
                        {
                            if (IsHint)
                                ModMain.Hint("未检测到可用背景图片！", ModMain.HintType.Critical);
                        }
                        else
                        {
                            ModMain.FrmMain.ImgBack.Visibility = Visibility.Collapsed;
                            if (IsHint)
                                ModMain.Hint("背景图片已清除！", ModMain.HintType.Finish);
                        }
                    }
                    if (!(ModMain.FrmSetupUI == null))
                        ModMain.FrmSetupUI.BackgroundRefreshUI(false, 0);
                }
                else
                {
                    if (Refresh)
                    {
                        string Address = ModBase.RandomOne(Pic);
                        try
                        {
                            ModBase.Log("[UI] 加载背景图片：" + Address);
                            ModMain.FrmMain.ImgBack.Background = new MyBitmap(Address);
                            ModBase.Setup.Load("UiBackgroundSuit", true);
                            ModMain.FrmMain.ImgBack.Visibility = Visibility.Visible;
                            if (IsHint)
                                ModMain.Hint("背景图片已刷新：" + ModBase.GetFileNameFromPath(Address), ModMain.HintType.Finish, false);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("参数无效"))
                            {
                                ModBase.Log("刷新背景图片失败，该图片文件可能并非标准格式。" + Constants.vbCrLf + "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。" + Constants.vbCrLf + "文件：" + Address, ModBase.LogLevel.Msgbox);
                            }
                            else
                            {
                                ModBase.Log(ex, "刷新背景图片失败（" + Address + "）", ModBase.LogLevel.Msgbox);
                            }
                        }
                    }
                    if (!(ModMain.FrmSetupUI == null))
                        ModMain.FrmSetupUI.BackgroundRefreshUI(true, Pic.Count);
                }
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新背景图片时出现未知错误", ModBase.LogLevel.Feedback);
            }
        }

        // 顶部栏
        private void BtnLogoChange_Click(object sender, EventArgs e)
        {
            string FileName = ModBase.SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.webp)|*.png;*.jpg;*.gif;*.webp", "选择图片");
            if (string.IsNullOrEmpty(FileName))
                return;
            try
            {
                // 拷贝文件
                File.Delete(ModBase.Path + @"PCL\Logo.png");
                ModBase.CopyFile(FileName, ModBase.Path + @"PCL\Logo.png");
                // 设置当前显示
                ModMain.FrmMain.ImageTitleLogo.Source = ModBase.Path + @"PCL\Logo.png";
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("参数无效"))
                {
                    ModBase.Log("改变标题栏图片失败，该图片文件可能并非标准格式。" + Constants.vbCrLf + "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。", ModBase.LogLevel.Msgbox);
                }
                else
                {
                    ModBase.Log(ex, "设置标题栏图片失败", ModBase.LogLevel.Msgbox);
                }
                ModMain.FrmMain.ImageTitleLogo.Source = (string)null;
            }
        }
        private void RadioLogoType3_Check(object sender, ModBase.RouteEventArgs e)
        {
            if (!(ModAnimation.AniControlEnabled == 0 && e.RaiseByMouse))
                return;
            Refresh:
            ;

            // 已有图片则不再选择
            if (File.Exists(ModBase.Path + @"PCL\Logo.png"))
            {
                try
                {
                    ModMain.FrmMain.ImageTitleLogo.Source = ModBase.Path + @"PCL\Logo.png";
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("参数无效"))
                    {
                        ModBase.Log("调整标题栏图片失败，该图片文件可能并非标准格式。" + Constants.vbCrLf + "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。", ModBase.LogLevel.Msgbox);
                    }
                    else
                    {
                        ModBase.Log(ex, "调整标题栏图片失败", ModBase.LogLevel.Msgbox);
                    }
                    ModMain.FrmMain.ImageTitleLogo.Source = (string)null;
                    e.Handled = true;
                    try
                    {
                        File.Delete(ModBase.Path + @"PCL\Logo.png");
                    }
                    catch (Exception exx)
                    {
                        ModBase.Log(exx, "清理错误的标题栏图片失败", ModBase.LogLevel.Msgbox);
                    }
                }
                return;
            }
            // 没有图片则要求选择
            string FileName = ModBase.SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.webp)|*.png;*.jpg;*.gif;*.webp", "选择图片");
            if (string.IsNullOrEmpty(FileName))
            {
                ModMain.FrmMain.ImageTitleLogo.Source = (string)null;
                e.Handled = true;
            }
            else
            {
                try
                {
                    // 拷贝文件
                    File.Delete(ModBase.Path + @"PCL\Logo.png");
                    ModBase.CopyFile(FileName, ModBase.Path + @"PCL\Logo.png");
                    goto Refresh;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "复制标题栏图片失败", ModBase.LogLevel.Msgbox);
                }
            }
        }
        private void BtnLogoDelete_Click(object sender, EventArgs e)
        {
            try
            {
                File.Delete(ModBase.Path + @"PCL\Logo.png");
                this.RadioLogoType1.SetChecked(true, true);
                ModMain.Hint("标题栏图片已清空！", ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "清空标题栏图片失败", ModBase.LogLevel.Msgbox);
            }
        }

        // 背景音乐
        private void BtnMusicOpen_Click(object sender, EventArgs e)
        {
            ModBase.OpenExplorer(ModBase.Path + @"PCL\Musics\");
        }
        private void BtnMusicRefresh_Click(object sender, EventArgs e)
        {
            ModMusic.MusicRefreshPlay(true);
        }
        public void MusicRefreshUI()
        {
            if (this.PanBackgroundOpacity is null)
                return;
            if (ModMusic.MusicAllList.Any())
            {
                this.PanMusicVolume.Visibility = Visibility.Visible;
                this.PanMusicDetail.Visibility = Visibility.Visible;
                this.BtnMusicClear.Visibility = Visibility.Visible;
                this.CardMusic.Title = "背景音乐（" + ModBase.EnumerateFiles(ModBase.Path + @"PCL\Musics\").Count() + " 首）";
            }
            else
            {
                this.PanMusicVolume.Visibility = Visibility.Collapsed;
                this.PanMusicDetail.Visibility = Visibility.Collapsed;
                this.BtnMusicClear.Visibility = Visibility.Collapsed;
                this.CardMusic.Title = "背景音乐";
            }
            this.CardMusic.TriggerForceResize();
        }
        private void BtnMusicClear_Click(object sender, EventArgs e)
        {
            if (ModMain.MyMsgBox("即将删除背景音乐文件夹中的所有文件。" + Constants.vbCrLf + "此操作不可撤销，是否确定？", "警告", Button2: "取消", IsWarn: true) == 1)
            {
                ModBase.RunInThread(() =>
        {
            ModMain.Hint("正在删除背景音乐……");
            // 停止播放音乐
            ModMusic.MusicNAudio = null;
            ModMusic.MusicWaitingList = new List<string>();
            ModMusic.MusicAllList = new List<string>();
            Thread.Sleep(200);
            // 删除文件
            try
            {
                ModBase.DeleteDirectory(ModBase.Path + @"PCL\Musics");
                ModMusic.DisableSMTCSupport();
                ModMain.Hint("背景音乐已删除！", ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除背景音乐失败", ModBase.LogLevel.Msgbox);
            }
            try
            {
                Directory.CreateDirectory(ModBase.Path + @"PCL\Musics");
                ModBase.RunInUi(() => ModMusic.MusicRefreshPlay(false));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重建背景音乐文件夹失败", ModBase.LogLevel.Msgbox);
            }
        });
            }
        }
        private void CheckMusicStart_Change()
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (this.CheckMusicStart.Checked)
                this.CheckMusicStop.Checked = false;
        }
        private void CheckMusicStop_Change()
        {
            if (ModAnimation.AniControlEnabled != 0)
                return;
            if (this.CheckMusicStop.Checked)
                this.CheckMusicStart.Checked = false;
        }

        // 主页
        private void BtnCustomFile_Click(object sender, EventArgs e)
        {
            try
            {
                if (File.Exists(ModBase.Path + @"PCL\Custom.xaml"))
                {
                    if (ModMain.MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn: true) == 2)
                        return;
                }
                ModBase.WriteFile(ModBase.Path + @"PCL\Custom.xaml", ModBase.GetResources("Custom"));
                ModMain.Hint("教学文件已生成！", ModMain.HintType.Finish);
                ModBase.OpenExplorer(ModBase.Path + @"PCL\Custom.xaml");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "生成教学文件失败", ModBase.LogLevel.Feedback);
            }
        }
        private void BtnCustomRefresh_Click()
        {
            ModMain.FrmLaunchRight.ForceRefresh();
            ModMain.Hint("已刷新主页！", ModMain.HintType.Finish);
        }
        private void BtnCustomTutorial_Click(object sender, EventArgs e)
        {
            ModMain.MyMsgBox("1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。" + Constants.vbCrLf + "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" + Constants.vbCrLf + "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" + Constants.vbCrLf + Constants.vbCrLf + "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" + Constants.vbCrLf + "直接将自定义主页文件拖进 PCL 窗口也可以快捷加载。", "主页自定义教程");
        }

        // 主题
        private void LabLauncherTheme5Unlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.RadioLauncherTheme5Gray.Opacity -= 0.23d;
            this.RadioLauncherTheme5.Opacity += 0.23d;
            ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.RadioLauncherTheme5Gray, 1d, (int)Math.Round(1000d * ModAnimation.AniSpeed)), ModAnimation.AaOpacity(this.RadioLauncherTheme5, (double)-1, (int)Math.Round(1000d * ModAnimation.AniSpeed)) }, "ThemeUnlock");
            if (this.RadioLauncherTheme5Gray.Opacity < 0.08d)
            {
                ModSecret.ThemeUnlock(5, UnlockHint: "隐藏主题 玄素黑 已解锁！");
                ModAnimation.AniStop("ThemeUnlock");
                this.RadioLauncherTheme5.Checked = true;
            }
        }
        private void LabLauncherTheme11Click_MouseLeftButtonUp()
        {
            if (this.LabLauncherTheme11Click.Visibility == Visibility.Collapsed || (this.LabLauncherTheme11Click.ToolTip ?? "").ToString().Contains("点击"))
            {
                if (ModMain.MyMsgBox("1. 不爬取或攻击相关服务或网站，不盗取相关账号，没有谜题可以或需要以此来解决。" + Constants.vbCrLf + "2. 不得篡改或损毁相关公开信息，请尽量让它们保持原状。" + Constants.vbCrLf + "3. 在你感到迷茫的时候，看看回声洞可能会给你带来惊喜。" + Constants.vbCrLf + Constants.vbCrLf + "若违规，可能会被从任意相关群中踢出！", "解密游戏的基本规则", "我知道了", "恕我拒绝") == 1)
                {
                    ModMain.MyMsgBox("你需要用自己的智慧来找到下一步的线索……" + Constants.vbCrLf + @"初始线索：gnp.dorC61\60\20\0202\moc.x1xa.2s\\:sp" + "T".ToLower() + "th", "解密游戏"); // 防止触发病毒检测规则
                }
            }
        }
        private void LabLauncherTheme8Copy_MouseRightButtonUp()
        {
            ModBase.OpenWebsite("https://afdian.com/a/LTCat");
        }
        private void LabLauncherTheme9Copy_MouseRightButtonUp()
        {
            PageOtherLeft.TryFeedback();
        }

        // 主题自定义
        private void RadioLauncherTheme14_Change(object sender, ModBase.RouteEventArgs e)
        {
            if (this.RadioLauncherTheme14.Checked)
            {
                if (this.LabLauncherHue.Visibility == Visibility.Visible)
                    return;
                this.LabLauncherHue.Visibility = Visibility.Visible;
                this.SliderLauncherHue.Visibility = Visibility.Visible;
                this.LabLauncherSat.Visibility = Visibility.Visible;
                this.SliderLauncherSat.Visibility = Visibility.Visible;
                this.LabLauncherDelta.Visibility = Visibility.Visible;
                this.SliderLauncherDelta.Visibility = Visibility.Visible;
                this.LabLauncherLight.Visibility = Visibility.Visible;
                this.SliderLauncherLight.Visibility = Visibility.Visible;
            }
            else
            {
                if (this.LabLauncherHue.Visibility == Visibility.Collapsed)
                    return;
                this.LabLauncherHue.Visibility = Visibility.Collapsed;
                this.SliderLauncherHue.Visibility = Visibility.Collapsed;
                this.LabLauncherSat.Visibility = Visibility.Collapsed;
                this.SliderLauncherSat.Visibility = Visibility.Collapsed;
                this.LabLauncherDelta.Visibility = Visibility.Collapsed;
                this.SliderLauncherDelta.Visibility = Visibility.Collapsed;
                this.LabLauncherLight.Visibility = Visibility.Collapsed;
                this.SliderLauncherLight.Visibility = Visibility.Collapsed;
            }
            this.CardLauncher.TriggerForceResize();
        }
        private void HSL_Change()
        {
            if (ModAnimation.AniControlEnabled != 0 || this.SliderLauncherSat is null || !this.SliderLauncherSat.IsLoaded)
                return;
            ModSecret.ThemeRefresh();
        }

        #region 功能隐藏

        private static bool _HiddenForceShow = false;
        /// <summary>
    /// 是否强制显示被禁用的功能。
    /// </summary>
        public static bool HiddenForceShow
        {
            get
            {
                return _HiddenForceShow;
            }
            set
            {
                _HiddenForceShow = value;
                HiddenRefresh();
            }
        }

        /// <summary>
    /// 更新功能隐藏带来的显示变化。
    /// </summary>
        public static void HiddenRefresh()
        {
            if (ModMain.FrmMain.PanTitleSelect is null || !ModMain.FrmMain.PanTitleSelect.IsLoaded)
                return;
            try
            {
                // 顶部栏
                if (!HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenPageDownload") && (bool)ModBase.Setup.Get("UiHiddenPageLink") && (bool)ModBase.Setup.Get("UiHiddenPageSetup") && (bool)ModBase.Setup.Get("UiHiddenPageOther"))
                {
                    // 顶部栏已被全部隐藏
                    ModMain.FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 顶部栏未被全部隐藏
                    ModMain.FrmMain.PanTitleSelect.Visibility = Visibility.Visible;
                    ModMain.FrmMain.BtnTitleSelect1.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenPageDownload") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmMain.BtnTitleSelect2.Visibility = !(ModBase.VersionBranchName == "Debug") ? Visibility.Collapsed : Visibility.Visible; // If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenPageLink"), Visibility.Collapsed, Visibility.Visible)
                    ModMain.FrmMain.BtnTitleSelect3.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenPageSetup") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmMain.BtnTitleSelect4.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenPageOther") ? Visibility.Collapsed : Visibility.Visible;
                }
                // 功能
                ModMain.FrmLaunchLeft.RefreshButtonsUI();
                if (ModMain.FrmSetupUI is not null)
                {
                    ModMain.FrmSetupUI.CardSwitch.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenFunctionHidden") ? Visibility.Collapsed : Visibility.Visible;
                }
                // 设置子页面
                if (ModMain.FrmSetupLeft is not null)
                {
                    ModMain.FrmSetupLeft.ItemLaunch.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenSetupLaunch") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmSetupLeft.ItemUI.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenSetupUi") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmSetupLeft.ItemLink.Visibility = Visibility.Collapsed; // If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenSetupLink"), Visibility.Collapsed, Visibility.Visible)
                    ModMain.FrmSetupLeft.ItemSystem.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenSetupSystem") ? Visibility.Collapsed : Visibility.Visible;
                    // 隐藏左边选择卡
                    int AvaliableCount = 0;
                    if (!(bool)ModBase.Setup.Get("UiHiddenSetupLaunch"))
                        AvaliableCount += 1;
                    if (!(bool)ModBase.Setup.Get("UiHiddenSetupUi"))
                        AvaliableCount += 1;
                    if (!(bool)ModBase.Setup.Get("UiHiddenSetupLink"))
                        AvaliableCount += 1;
                    if (!(bool)ModBase.Setup.Get("UiHiddenSetupSystem"))
                        AvaliableCount += 1;
                    ModMain.FrmSetupLeft.PanItem.Visibility = AvaliableCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
                }
                // 更多子页面
                int OtherAvaliableCount = 0;
                if (!(bool)ModBase.Setup.Get("UiHiddenOtherHelp"))
                    OtherAvaliableCount += 1;
                if (!(bool)ModBase.Setup.Get("UiHiddenOtherAbout"))
                    OtherAvaliableCount += 1;
                if (!(bool)ModBase.Setup.Get("UiHiddenOtherTest"))
                    OtherAvaliableCount += 1;
                if (!(bool)ModBase.Setup.Get("UiHiddenOtherFeedback"))
                    OtherAvaliableCount += 1;
                if (!(bool)ModBase.Setup.Get("UiHiddenOtherVote"))
                    OtherAvaliableCount += 1;
                if (ModMain.FrmOtherLeft is not null)
                {
                    ModMain.FrmOtherLeft.ItemHelp.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenOtherHelp") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemFeedback.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenOtherFeedback") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemVote.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenOtherVote") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemAbout.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenOtherAbout") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemTest.Visibility = !HiddenForceShow && (bool)ModBase.Setup.Get("UiHiddenOtherTest") ? Visibility.Collapsed : Visibility.Visible;
                    // 隐藏左边选择卡
                    ModMain.FrmOtherLeft.PanItem.Visibility = OtherAvaliableCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
                }
                if (OtherAvaliableCount == 1 && !HiddenForceShow)
                {
                    if (!(bool)ModBase.Setup.Get("UiHiddenOtherHelp"))
                    {
                        ModMain.FrmMain.BtnTitleSelect4.Text = "帮助";
                    }
                    else if (!(bool)ModBase.Setup.Get("UiHiddenOtherAbout"))
                    {
                        ModMain.FrmMain.BtnTitleSelect4.Text = "关于";
                    }
                    else
                    {
                        ModMain.FrmMain.BtnTitleSelect4.Text = "百宝箱";
                    }
                }
                else
                {
                    ModMain.FrmMain.BtnTitleSelect4.Text = "更多";
                }
                // 各个页面的入口
                if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.VersionSelect)
                    ModMain.FrmSelectRight.BtnEmptyDownload_Loaded();
                if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.Launch)
                    ModMain.FrmLaunchLeft.RefreshButtonsUI();
                if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.VersionSetup && ModMain.FrmVersionModDisabled is not null)
                    ModMain.FrmVersionModDisabled.BtnDownload_Loaded();
                // 备注
                if (ModMain.FrmSetupUI is not null)
                    ModMain.FrmSetupUI.CardSwitch.Title = HiddenForceShow ? "功能隐藏（已暂时关闭，按 F12 以重新启用）" : "功能隐藏";
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新功能隐藏项目失败", ModBase.LogLevel.Feedback);
            }
        }

        // UI 协同改变
        private void HiddenSetupMain()
        {
            // 设置主页面
            if (this.CheckHiddenPageSetup.Checked)
            {
                // 开启
                this.CheckHiddenSetupLaunch.Checked = true;
                this.CheckHiddenSetupSystem.Checked = true;
                this.CheckHiddenSetupLink.Checked = true;
                this.CheckHiddenSetupUI.Checked = true;
            }
            // 关闭
            else if ((bool)ModBase.Setup.Get("UiHiddenSetupLaunch") && (bool)ModBase.Setup.Get("UiHiddenSetupUi") && (bool)ModBase.Setup.Get("UiHiddenSetupSystem") && (bool)ModBase.Setup.Get("UiHiddenSetupLink"))
            {
                this.CheckHiddenSetupLaunch.Checked = false;
                this.CheckHiddenSetupSystem.Checked = false;
                this.CheckHiddenSetupLink.Checked = false;
                this.CheckHiddenSetupUI.Checked = false;
            }
        }
        private void HiddenSetupSub()
        {
            // 设置子页面
            if ((bool)ModBase.Setup.Get("UiHiddenSetupLaunch") && (bool)ModBase.Setup.Get("UiHiddenSetupUi") && (bool)ModBase.Setup.Get("UiHiddenSetupSystem") && (bool)ModBase.Setup.Get("UiHiddenSetupLink"))
            {
                // 已被全部隐藏
                this.CheckHiddenPageSetup.Checked = true;
            }
            else
            {
                // 未被全部隐藏
                this.CheckHiddenPageSetup.Checked = false;
            }
        }
        private void HiddenOtherMain()
        {
            // 更多主页面
            if (this.CheckHiddenPageOther.Checked)
            {
                // 开启
                this.CheckHiddenOtherAbout.Checked = true;
                this.CheckHiddenOtherTest.Checked = true;
                this.CheckHiddenOtherFeedback.Checked = true;
                this.CheckHiddenOtherVote.Checked = true;
                this.CheckHiddenOtherHelp.Checked = true;
            }
            // 关闭
            else if ((bool)ModBase.Setup.Get("UiHiddenOtherHelp") && (bool)ModBase.Setup.Get("UiHiddenOtherAbout") && (bool)ModBase.Setup.Get("UiHiddenOtherTest") && (bool)ModBase.Setup.Get("UiHiddenOtherVote") && (bool)ModBase.Setup.Get("UiHiddenOtherFeedback"))
            {
                this.CheckHiddenOtherAbout.Checked = false;
                this.CheckHiddenOtherTest.Checked = false;
                this.CheckHiddenOtherFeedback.Checked = false;
                this.CheckHiddenOtherVote.Checked = false;
                this.CheckHiddenOtherHelp.Checked = false;
            }
        }
        private void HiddenOtherSub(object sender, bool user)
        {
            // 更多子页面（有具体内容的）
            if ((bool)ModBase.Setup.Get("UiHiddenOtherHelp") && (bool)ModBase.Setup.Get("UiHiddenOtherAbout") && (bool)ModBase.Setup.Get("UiHiddenOtherTest"))
            {
                // 已被全部隐藏
                this.CheckHiddenPageOther.Checked = true;
            }
            else
            {
                // 未被全部隐藏
                this.CheckHiddenPageOther.Checked = false;
            }
            // 修改无具体内容的项
            if (!user)
                return;
            if ((bool)ModBase.Setup.Get("UiHiddenOtherHelp") && (bool)ModBase.Setup.Get("UiHiddenOtherAbout") && (bool)ModBase.Setup.Get("UiHiddenOtherTest"))
            {
                this.CheckHiddenOtherFeedback.Checked = true;
                this.CheckHiddenOtherVote.Checked = true;
            }
        }
        private void HiddenOtherNet(object sender, bool user)
        {
            // 更多子页面（无具体内容的）
            if (!user)
                return;
            if ((bool)ModBase.Setup.Get("UiHiddenOtherHelp") && (bool)ModBase.Setup.Get("UiHiddenOtherAbout") && (bool)ModBase.Setup.Get("UiHiddenOtherTest") && (!(bool)ModBase.Setup.Get("UiHiddenOtherFeedback") || !(bool)ModBase.Setup.Get("UiHiddenOtherVote")))
            {
                this.CheckHiddenOtherAbout.Checked = false;
                this.CheckHiddenOtherTest.Checked = false;
                this.CheckHiddenOtherHelp.Checked = false;
            }
        }

        // 警告提示
        private void HiddenHint(object sender, bool user)
        {
            if (Conversions.ToBoolean(ModAnimation.AniControlEnabled == 0 && ((dynamic)sender).Checked))
                ModMain.Hint("按 F12 即可暂时关闭功能隐藏设置。千万别忘了，要不然设置就改不回来了……");
        }

        #endregion

        // 赞助
        private void BtnLauncherDonate_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://afdian.com/a/LTCat");
        }

        // 滑动条
        private void SliderLoad()
        {
            this.SliderMusicVolume.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(Math.Ceiling((decimal)Operators.MultiplyObject(v, 0.1d)), "%"));
            this.SliderLauncherOpacity.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(Math.Round((decimal)Operators.AddObject(40, Operators.MultiplyObject(v, 0.1d))), "%"));
            this.SliderLauncherHue.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, "°"));
            this.SliderLauncherSat.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, "%"));
            this.SliderLauncherDelta.GetHintText = new Func<int, object>((Value) => { if (Value > 90) { return "+" + (Value - 90); } else if (Value == 90) { return 0; } else { return Value - 90; } });
            this.SliderLauncherLight.GetHintText = new Func<int, object>((Value) => { if (Value > 20) { return "+" + (Value - 20); } else if (Value == 20) { return 0; } else { return Value - 20; } });
            this.SliderBackgroundOpacity.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(Math.Round((decimal)Operators.MultiplyObject(v, 0.1d)), "%"));
            this.SliderBackgroundBlur.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, " 像素"));
        }

    }
}