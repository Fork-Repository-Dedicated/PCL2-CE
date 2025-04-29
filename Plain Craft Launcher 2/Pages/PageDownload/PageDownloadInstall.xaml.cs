using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadInstall
    {
        public PageDownloadInstall()
        {
            this.Initialized += (_, __) => LoaderInit();
            this.Loaded += (_, __) => Init();
        }

        private void LoaderInit()
        {
            this.DisabledPageAnimControls.Add(this.BtnStart);
            this.PageLoaderInit(this.LoadMinecraft, this.PanLoad, this.PanAllBack, (FrameworkElement)null, ModDownload.DlClientListLoader, (_) => LoadMinecraft_OnFinish());
        }

        private bool IsLoad = false;
        private void Init()
        {
            this.PanBack.ScrollToHome();
            ModDownload.DlOptiFineListLoader.Start();
            ModDownload.DlLiteLoaderListLoader.Start();
            ModDownload.DlFabricListLoader.Start();
            ModDownload.DlQuiltListLoader.Start();
            ModDownload.DlNeoForgeListLoader.Start();
            ModDownload.DlCleanroomListLoader.Start();

            // 重载预览
            this.TextSelectName.ValidateRules = new System.Collections.ObjectModel.Collection<ValidateType>() { new ValidateFolderName(ModMinecraft.PathMcFolder + "versions") };
            this.TextSelectName.Validate();
            SelectReload();

            // 非重复加载部分
            if (IsLoad)
                return;
            IsLoad = true;

            ModDownloadLib.McDownloadForgeRecommendedRefresh();

            this.LoadOptiFine.State = ModDownload.DlOptiFineListLoader;
            this.LoadLiteLoader.State = ModDownload.DlLiteLoaderListLoader;
            this.LoadFabric.State = ModDownload.DlFabricListLoader;
            this.LoadFabricApi.State = ModDownload.DlFabricApiLoader;
            this.LoadQuilt.State = ModDownload.DlQuiltListLoader;
            this.LoadQSL.State = ModDownload.DlQSLLoader;
            this.LoadNeoForge.State = ModDownload.DlNeoForgeListLoader;
            this.LoadCleanroom.State = ModDownload.DlCleanroomListLoader;
            this.LoadOptiFabric.State = ModDownload.DlOptiFabricLoader;
        }

        #region 页面切换

        // 页面切换动画
        public bool IsInSelectPage = false;
        private bool IsFirstLoaded = false;
        private void EnterSelectPage()
        {
            if (IsInSelectPage)
                return;
            IsInSelectPage = true;

            this.PanInner.Margin = new Thickness(25d, 10d, 25d, 40d);

            AutoSelectedFabricApi = false;
            AutoSelectedQSL = false;
            AutoSelectedOptiFabric = false;
            IsSelectNameEdited = false;
            this.PanSelect.Visibility = Visibility.Visible;
            this.PanSelect.IsHitTestVisible = true;
            this.PanMinecraft.IsHitTestVisible = false;
            this.PanBack.IsHitTestVisible = false;
            this.PanBack.ScrollToHome();

            this.DisabledPageAnimControls.Remove(this.BtnStart);
            this.BtnStart.Show = true;
            this.CardOptiFine.IsSwaped = true;
            this.CardLiteLoader.IsSwaped = true;
            this.CardForge.IsSwaped = true;
            this.CardNeoForge.IsSwaped = true;
            this.CardCleanroom.IsSwaped = true;
            this.CardFabric.IsSwaped = true;
            this.CardFabricApi.IsSwaped = true;
            this.CardQuilt.IsSwaped = true;
            this.CardQSL.IsSwaped = true;
            this.CardOptiFabric.IsSwaped = true;

            if (Conversions.ToBoolean(!(bool)ModBase.Setup.Get("HintInstallBack")))
            {
                ModBase.Setup.Set("HintInstallBack", true);
                ModMain.Hint("点击 Minecraft 项即可返回游戏主版本选择页面！");
            }

            // 如果在选择页面按了刷新键，选择页的东西可能会由于动画被隐藏，但不会由于加载结束而再次显示，因此这里需要手动恢复
            foreach (var Control in this.GetAllAnimControls(this.PanSelect))
            {
                Control.Opacity = 1d;
                if (Control.RenderTransform is null || Control.RenderTransform is TranslateTransform)
                {
                    Control.RenderTransform = new TranslateTransform();
                }
            }

            // 启动 Forge 加载
            if (SelectedMinecraftId.StartsWith("1."))
            {
                var ForgeLoader = new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>("DlForgeVersion " + SelectedMinecraftId, ModDownload.DlForgeVersionMain);
                this.LoadForge.State = ForgeLoader;
                ForgeLoader.Start(SelectedMinecraftId);
            }

            // 启动 Fabric API、QSL、OptiFabric 加载
            ModDownload.DlFabricApiLoader.Start();
            ModDownload.DlQSLLoader.Start();
            ModDownload.DlOptiFabricLoader.Start();

            ModAnimation.AniStart(new[] {
            ModAnimation.AaOpacity(this.PanMinecraft, -this.PanMinecraft.Opacity, 90, 10),
            ModAnimation.AaTranslateX(this.PanMinecraft, (double)-50 - ((TranslateTransform)this.PanMinecraft.RenderTransform).X, 100, 10),
                        ModAnimation.AaCode(() =>
                {
                this.PanBack.ScrollToHome();
                this.TextSelectName.Validate();
                OptiFine_Loaded();
                LiteLoader_Loaded();
                Forge_Loaded();
                NeoForge_Loaded();
                Cleanroom_Loaded();
                Fabric_Loaded();
                FabricApi_Loaded();
                Quilt_Loaded();
                QSL_Loaded();
                OptiFabric_Loaded();
                SelectReload();
            }, After: true),
            ModAnimation.AaOpacity(this.PanSelect, 1d - this.PanSelect.Opacity, 90, 100),
            ModAnimation.AaTranslateX(this.PanSelect, -((TranslateTransform)this.PanSelect.RenderTransform).X, 200, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                        ModAnimation.AaCode(() =>
                {
                this.PanMinecraft.Visibility = Visibility.Collapsed;
                this.PanBack.IsHitTestVisible = true;
                // 初始化 Binding
                if (IsFirstLoaded)
                        return;
                IsFirstLoaded = true;
                this.BtnOptiFineClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardOptiFine.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnLiteLoaderClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardLiteLoader.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnForgeClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardForge.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnNeoForgeClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardNeoForge.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnCleanroomClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardCleanroom.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnFabricClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardFabric.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnFabricApiClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardFabricApi.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnQuiltClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardQuilt.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnQSLClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardQSL.MainTextBlock, Mode = BindingMode.OneWay });
                this.BtnOptiFabricClearInner.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this.CardOptiFabric.MainTextBlock, Mode = BindingMode.OneWay });
            }, After: true)
        }, "FrmDownloadInstall SelectPageSwitch", true);
        }
        public void ExitSelectPage()
        {
            if (!IsInSelectPage)
                return;
            IsInSelectPage = false;

            this.PanInner.Margin = new Thickness(25d, 10d, 25d, 25d);

            this.DisabledPageAnimControls.Add(this.BtnStart);
            this.BtnStart.Show = false;
            SelectClear(); // 清除已选择项
            this.PanMinecraft.Visibility = Visibility.Visible;
            this.PanSelect.IsHitTestVisible = false;
            this.PanMinecraft.IsHitTestVisible = true;
            this.PanBack.IsHitTestVisible = false;
            this.PanBack.ScrollToHome();

            ModAnimation.AniStart(new[] {
            ModAnimation.AaOpacity(this.PanSelect, -this.PanSelect.Opacity, 90, 10),
            ModAnimation.AaTranslateX(this.PanSelect, 50d - ((TranslateTransform)this.PanSelect.RenderTransform).X, 100, 10),
            ModAnimation.AaCode(() => this.PanBack.ScrollToHome(), After: true),
            ModAnimation.AaOpacity(this.PanMinecraft, 1d - this.PanMinecraft.Opacity, 90, 100),
            ModAnimation.AaTranslateX(this.PanMinecraft, -((TranslateTransform)this.PanMinecraft.RenderTransform).X, 200, 100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                        ModAnimation.AaCode(() =>
                {
                this.PanSelect.Visibility = Visibility.Collapsed;
                this.PanBack.IsHitTestVisible = true;
            }, After: true)
        }, "FrmDownloadInstall SelectPageSwitch");
        }
        public void MinecraftSelected(MyListItem sender, MouseButtonEventArgs e)
        {
            SelectedMinecraftId = sender.Title;
            SelectedMinecraftJsonUrl = sender.Tag("url").ToString();
            SelectedMinecraftIcon = sender.Logo;
            EnterSelectPage();
        }

        #endregion

        #region 选择

        // Minecraft
        private string SelectedMinecraftId;
        private string SelectedMinecraftJsonUrl;
        private string SelectedMinecraftIcon;

        // OptiFine
        private ModDownload.DlOptiFineListEntry SelectedOptiFine = null;
        private void SetOptiFineInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanOptiFineInfo.Tag, IsShow, false)))
                return;
            this.PanOptiFineInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanOptiFineInfo, -((TranslateTransform)this.PanOptiFineInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanOptiFineInfo, 1d - this.PanOptiFineInfo.Opacity, 100, 90) }, "SetOptiFineInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanOptiFineInfo, 6d - ((TranslateTransform)this.PanOptiFineInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanOptiFineInfo, -this.PanOptiFineInfo.Opacity, 100) }, "SetOptiFineInfoShow");
            }
        }

        // Mod Loader 统一判断，内容应为 Forge / NeoForge / Fabric / Quilt / Cleanroom
        private string SelectedLoaderName = null;

        // Mod Loader API 统一判断，内容应为 Fabric API 或 QFAPI / QSL
        private string SelectedAPIName = null;

        // LiteLoader
        private ModDownload.DlLiteLoaderListEntry SelectedLiteLoader = null;
        private void SetLiteLoaderInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanLiteLoaderInfo.Tag, IsShow, false)))
                return;
            this.PanLiteLoaderInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanLiteLoaderInfo, -((TranslateTransform)this.PanLiteLoaderInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanLiteLoaderInfo, 1d - this.PanLiteLoaderInfo.Opacity, 100, 90) }, "SetLiteLoaderInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanLiteLoaderInfo, 6d - ((TranslateTransform)this.PanLiteLoaderInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanLiteLoaderInfo, -this.PanLiteLoaderInfo.Opacity, 100) }, "SetLiteLoaderInfoShow");
            }
        }

        // Forge
        private ModDownload.DlForgeVersionEntry SelectedForge = null;
        private void SetForgeInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanForgeInfo.Tag, IsShow, false)))
                return;
            this.PanForgeInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanForgeInfo, -((TranslateTransform)this.PanForgeInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanForgeInfo, 1d - this.PanForgeInfo.Opacity, 100, 90) }, "SetForgeInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanForgeInfo, 6d - ((TranslateTransform)this.PanForgeInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanForgeInfo, -this.PanForgeInfo.Opacity, 100) }, "SetForgeInfoShow");
            }
        }

        // Cleanroom
        private ModDownload.DlCleanroomListEntry SelectedCleanroom = null;
        private void SetCleanroomInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanCleanroomInfo.Tag, IsShow, false)))
                return;
            this.PanCleanroomInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanCleanroomInfo, -((TranslateTransform)this.PanCleanroomInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanCleanroomInfo, 1d - this.PanCleanroomInfo.Opacity, 100, 90) }, "SetCleanroomInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanCleanroomInfo, 6d - ((TranslateTransform)this.PanCleanroomInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanCleanroomInfo, -this.PanCleanroomInfo.Opacity, 100) }, "SetCleanroomInfoShow");
            }
        }

        // NeoForge
        private ModDownload.DlNeoForgeListEntry SelectedNeoForge = null;
        private void SetNeoForgeInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanNeoForgeInfo.Tag, IsShow, false)))
                return;
            this.PanNeoForgeInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanNeoForgeInfo, -((TranslateTransform)this.PanNeoForgeInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanNeoForgeInfo, 1d - this.PanNeoForgeInfo.Opacity, 100, 90) }, "SetNeoForgeInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanNeoForgeInfo, 6d - ((TranslateTransform)this.PanNeoForgeInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanNeoForgeInfo, -this.PanNeoForgeInfo.Opacity, 100) }, "SetNeoForgeInfoShow");
            }
        }

        // Fabric
        private string SelectedFabric = null;
        private void SetFabricInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanFabricInfo.Tag, IsShow, false)))
                return;
            this.PanFabricInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanFabricInfo, -((TranslateTransform)this.PanFabricInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanFabricInfo, 1d - this.PanFabricInfo.Opacity, 100, 90) }, "SetFabricInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanFabricInfo, 6d - ((TranslateTransform)this.PanFabricInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanFabricInfo, -this.PanFabricInfo.Opacity, 100) }, "SetFabricInfoShow");
            }
        }

        // FabricApi
        private ModComp.CompFile SelectedFabricApi = null;
        private void SetFabricApiInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanFabricApiInfo.Tag, IsShow, false)))
                return;
            this.PanFabricApiInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanFabricApiInfo, -((TranslateTransform)this.PanFabricApiInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanFabricApiInfo, 1d - this.PanFabricApiInfo.Opacity, 100, 90) }, "SetFabricApiInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanFabricApiInfo, 6d - ((TranslateTransform)this.PanFabricApiInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanFabricApiInfo, -this.PanFabricApiInfo.Opacity, 100) }, "SetFabricApiInfoShow");
            }
        }

        // Quilt
        private string SelectedQuilt = null;
        private void SetQuiltInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanQuiltInfo.Tag, IsShow, false)))
                return;
            this.PanQuiltInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanQuiltInfo, -((TranslateTransform)this.PanQuiltInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanQuiltInfo, 1d - this.PanQuiltInfo.Opacity, 100, 90) }, "SetQuiltInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanQuiltInfo, 6d - ((TranslateTransform)this.PanQuiltInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanQuiltInfo, -this.PanQuiltInfo.Opacity, 100) }, "SetQuiltInfoShow");
            }
        }

        // QSL
        private ModComp.CompFile SelectedQSL = null;
        private void SetQSLInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanQSLInfo.Tag, IsShow, false)))
                return;
            this.PanQSLInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanQSLInfo, -((TranslateTransform)this.PanQSLInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanQSLInfo, 1d - this.PanQSLInfo.Opacity, 100, 90) }, "SetQSLInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanQSLInfo, 6d - ((TranslateTransform)this.PanQSLInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanQSLInfo, -this.PanQSLInfo.Opacity, 100) }, "SetQSLInfoShow");
            }
        }

        // OptiFabric
        private ModComp.CompFile SelectedOptiFabric = null;
        private void SetOptiFabricInfoShow(string IsShow)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(this.PanOptiFabricInfo.Tag, IsShow, false)))
                return;
            this.PanOptiFabricInfo.Tag = IsShow;
            if (IsShow == "True")
            {
                // 显示信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanOptiFabricInfo, -((TranslateTransform)this.PanOptiFabricInfo.RenderTransform).Y, 270, 100, Ease: new ModAnimation.AniEaseOutBack()), ModAnimation.AaOpacity(this.PanOptiFabricInfo, 1d - this.PanOptiFabricInfo.Opacity, 100, 90) }, "SetOptiFabricInfoShow");
            }
            else
            {
                // 隐藏信息栏
                ModAnimation.AniStart(new[] { ModAnimation.AaTranslateY(this.PanOptiFabricInfo, 6d - ((TranslateTransform)this.PanOptiFabricInfo.RenderTransform).Y, 200), ModAnimation.AaOpacity(this.PanOptiFabricInfo, -this.PanOptiFabricInfo.Opacity, 100) }, "SetOptiFabricInfoShow");
            }
        }

        private bool IsReloading = false; // #3742 中，LoadOptiFineGetError 会初始化 LoadOptiFine，触发事件 LoadOptiFine.StateChanged，导致再次调用 SelectReload
                                          /// <summary>
    /// 重载已选择的项目的显示。
    /// </summary>
        private void SelectReload()
        {
            if (SelectedMinecraftId is null || IsReloading)
                return;
            IsReloading = true;
            // 主预览
            SelectNameUpdate();
            this.ImgLogo.Source = GetSelectLogo();
            // OptiFine
            string OptiFineError = LoadOptiFineGetError();
            this.CardOptiFine.MainSwap.Visibility = OptiFineError is null ? Visibility.Visible : Visibility.Collapsed;
            if (OptiFineError is not null)
                this.CardOptiFine.IsSwaped = true; // 例如在同时展开卡片时选择了不兼容项则强制折叠
            SetOptiFineInfoShow(Conversions.ToString(this.CardOptiFine.IsSwaped));
            if (SelectedOptiFine is null)
            {
                this.BtnOptiFineClear.Visibility = Visibility.Collapsed;
                this.ImgOptiFine.Visibility = Visibility.Collapsed;
                this.LabOptiFine.Text = OptiFineError ?? "可以添加";
                this.LabOptiFine.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                this.BtnOptiFineClear.Visibility = Visibility.Visible;
                this.ImgOptiFine.Visibility = Visibility.Visible;
                this.LabOptiFine.Text = SelectedOptiFine.NameDisplay.Replace(SelectedMinecraftId + " ", "");
                this.LabOptiFine.Foreground = ModSecret.ColorGray1;
            }
            // LiteLoader
            if (!SelectedMinecraftId.Contains("1.") || ModBase.Val(SelectedMinecraftId.Split(".")[1]) > 12d)
            {
                this.CardLiteLoader.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.CardLiteLoader.Visibility = Visibility.Visible;
                string LiteLoaderError = LoadLiteLoaderGetError();
                this.CardLiteLoader.MainSwap.Visibility = LiteLoaderError is null ? Visibility.Visible : Visibility.Collapsed;
                if (LiteLoaderError is not null)
                    this.CardLiteLoader.IsSwaped = true; // 例如在同时展开卡片时选择了不兼容项则强制折叠
                SetLiteLoaderInfoShow(Conversions.ToString(this.CardLiteLoader.IsSwaped));
                if (SelectedLiteLoader is null)
                {
                    this.BtnLiteLoaderClear.Visibility = Visibility.Collapsed;
                    this.ImgLiteLoader.Visibility = Visibility.Collapsed;
                    this.LabLiteLoader.Text = LiteLoaderError ?? "可以添加";
                    this.LabLiteLoader.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnLiteLoaderClear.Visibility = Visibility.Visible;
                    this.ImgLiteLoader.Visibility = Visibility.Visible;
                    this.LabLiteLoader.Text = SelectedLiteLoader.Inherit;
                    this.LabLiteLoader.Foreground = ModSecret.ColorGray1;
                }
            }
            // Forge
            string ForgeError = LoadForgeGetError();
            this.CardForge.MainSwap.Visibility = ForgeError is null ? Visibility.Visible : Visibility.Collapsed;
            if (ForgeError is not null)
                this.CardForge.IsSwaped = true;
            SetForgeInfoShow(Conversions.ToString(this.CardForge.IsSwaped));
            if (SelectedForge is null)
            {
                this.BtnForgeClear.Visibility = Visibility.Collapsed;
                this.ImgForge.Visibility = Visibility.Collapsed;
                this.LabForge.Text = ForgeError ?? "可以添加";
                this.LabForge.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                this.BtnForgeClear.Visibility = Visibility.Visible;
                this.ImgForge.Visibility = Visibility.Visible;
                this.LabForge.Text = SelectedForge.VersionName;
                this.LabForge.Foreground = ModSecret.ColorGray1;
            }
            // Cleanroom
            if (SelectedMinecraftId == "1.12.2")
            {
                this.CardCleanroom.Visibility = Visibility.Visible;
                string CleanroomError = LoadCleanroomGetError();
                this.CardCleanroom.MainSwap.Visibility = CleanroomError is null ? Visibility.Visible : Visibility.Collapsed;
                if (CleanroomError is not null)
                    this.CardCleanroom.IsSwaped = true;
                SetCleanroomInfoShow(Conversions.ToString(this.CardCleanroom.IsSwaped));
                if (SelectedCleanroom is null)
                {
                    this.BtnCleanroomClear.Visibility = Visibility.Collapsed;
                    this.ImgCleanroom.Visibility = Visibility.Collapsed;
                    this.LabCleanroom.Text = CleanroomError ?? "可以添加";
                    this.LabCleanroom.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnCleanroomClear.Visibility = Visibility.Visible;
                    this.ImgCleanroom.Visibility = Visibility.Visible;
                    this.LabCleanroom.Text = SelectedCleanroom.VersionName;
                    this.LabCleanroom.Foreground = ModSecret.ColorGray1;
                }
            }
            else
            {
                this.CardCleanroom.Visibility = Visibility.Collapsed;
            }
            // NeoForge
            if (!SelectedMinecraftId.Contains("1.") || ModBase.Val(SelectedMinecraftId.Split(".")[1]) <= 19d)
            {
                this.CardNeoForge.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.CardNeoForge.Visibility = Visibility.Visible;
                string NeoForgeError = LoadNeoForgeGetError();
                this.CardNeoForge.MainSwap.Visibility = NeoForgeError is null ? Visibility.Visible : Visibility.Collapsed;
                if (NeoForgeError is not null)
                    this.CardNeoForge.IsSwaped = true;
                SetNeoForgeInfoShow(Conversions.ToString(this.CardNeoForge.IsSwaped));
                if (SelectedNeoForge is null)
                {
                    this.BtnNeoForgeClear.Visibility = Visibility.Collapsed;
                    this.ImgNeoForge.Visibility = Visibility.Collapsed;
                    this.LabNeoForge.Text = NeoForgeError ?? "可以添加";
                    this.LabNeoForge.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnNeoForgeClear.Visibility = Visibility.Visible;
                    this.ImgNeoForge.Visibility = Visibility.Visible;
                    this.LabNeoForge.Text = SelectedNeoForge.VersionName;
                    this.LabNeoForge.Foreground = ModSecret.ColorGray1;
                }
            }
            // Fabric
            if (SelectedMinecraftId.Contains("1.") && ModBase.Val(SelectedMinecraftId.Split(".")[1]) <= 13d)
            {
                this.CardFabric.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.CardFabric.Visibility = Visibility.Visible;
                string FabricError = LoadFabricGetError();
                this.CardFabric.MainSwap.Visibility = FabricError is null ? Visibility.Visible : Visibility.Collapsed;
                if (FabricError is not null)
                    this.CardFabric.IsSwaped = true;
                SetFabricInfoShow(Conversions.ToString(this.CardFabric.IsSwaped));
                if (SelectedFabric is null)
                {
                    this.BtnFabricClear.Visibility = Visibility.Collapsed;
                    this.ImgFabric.Visibility = Visibility.Collapsed;
                    this.LabFabric.Text = FabricError ?? "可以添加";
                    this.LabFabric.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnFabricClear.Visibility = Visibility.Visible;
                    this.ImgFabric.Visibility = Visibility.Visible;
                    this.LabFabric.Text = SelectedFabric.Replace("+build", "");
                    this.LabFabric.Foreground = ModSecret.ColorGray1;
                }
            }
            // FabricApi
            if (SelectedFabric is null && SelectedQuilt is null)
            {
                this.CardFabricApi.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.CardFabricApi.Visibility = Visibility.Visible;
                string FabricApiError = LoadFabricApiGetError();
                this.CardFabricApi.MainSwap.Visibility = FabricApiError is null ? Visibility.Visible : Visibility.Collapsed;
                if (FabricApiError is not null || SelectedFabric is null && SelectedQuilt is null)
                    this.CardFabricApi.IsSwaped = true;
                SetFabricApiInfoShow(Conversions.ToString(this.CardFabricApi.IsSwaped));
                if (SelectedFabricApi is null)
                {
                    this.BtnFabricApiClear.Visibility = Visibility.Collapsed;
                    this.ImgFabricApi.Visibility = Visibility.Collapsed;
                    this.LabFabricApi.Text = FabricApiError ?? "可以添加";
                    this.LabFabricApi.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnFabricApiClear.Visibility = Visibility.Visible;
                    this.ImgFabricApi.Visibility = Visibility.Visible;
                    this.LabFabricApi.Text = SelectedFabricApi.DisplayName.Split("]")[1].Replace("Fabric API ", "").Replace(" build ", ".").Split("+").First().Trim();
                    this.LabFabricApi.Foreground = ModSecret.ColorGray1;
                }
            }
            // Quilt
            if (SelectedMinecraftId.Contains("1.") && ModBase.Val(SelectedMinecraftId.Split(".")[1]) <= 14d && !SelectedMinecraftId.Contains("1.14.4"))
            {
                this.CardQuilt.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.CardQuilt.Visibility = Visibility.Visible;
                string QuiltError = LoadQuiltGetError();
                this.CardQuilt.MainSwap.Visibility = QuiltError is null ? Visibility.Visible : Visibility.Collapsed;
                if (QuiltError is not null)
                    this.CardQuilt.IsSwaped = true;
                SetQuiltInfoShow(Conversions.ToString(this.CardQuilt.IsSwaped));
                if (SelectedQuilt is null)
                {
                    this.BtnQuiltClear.Visibility = Visibility.Collapsed;
                    this.ImgQuilt.Visibility = Visibility.Collapsed;
                    this.LabQuilt.Text = QuiltError ?? "点击选择";
                    this.LabQuilt.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnQuiltClear.Visibility = Visibility.Visible;
                    this.ImgQuilt.Visibility = Visibility.Visible;
                    this.LabQuilt.Text = SelectedQuilt.Replace("+build", "");
                    this.LabQuilt.Foreground = ModSecret.ColorGray1;
                }
            }
            // QSL
            if (SelectedQuilt is null)
            {
                this.CardQSL.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.CardQSL.Visibility = Visibility.Visible;
                string QSLError = LoadQSLGetError();
                this.CardQSL.MainSwap.Visibility = QSLError is null ? Visibility.Visible : Visibility.Collapsed;
                if (QSLError is not null || SelectedQuilt is null)
                    this.CardQSL.IsSwaped = true;
                SetQSLInfoShow(Conversions.ToString(this.CardQSL.IsSwaped));
                if (SelectedQSL is null)
                {
                    this.BtnQSLClear.Visibility = Visibility.Collapsed;
                    this.ImgQSL.Visibility = Visibility.Collapsed;
                    this.LabQSL.Text = QSLError ?? "点击选择";
                    this.LabQSL.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnQSLClear.Visibility = Visibility.Visible;
                    this.ImgQSL.Visibility = Visibility.Visible;
                    this.LabQSL.Text = SelectedQSL.DisplayName.Split("]")[1].Trim();
                    this.LabQSL.Foreground = ModSecret.ColorGray1;
                }
            }
            // OptiFabric
            if (SelectedFabric is null || SelectedOptiFine is null)
            {
                this.CardOptiFabric.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.CardOptiFabric.Visibility = Visibility.Visible;
                string OptiFabricError = LoadOptiFabricGetError();
                this.CardOptiFabric.MainSwap.Visibility = OptiFabricError is null ? Visibility.Visible : Visibility.Collapsed;
                if (OptiFabricError is not null || SelectedFabric is null)
                    this.CardOptiFabric.IsSwaped = true;
                SetOptiFabricInfoShow(Conversions.ToString(this.CardOptiFabric.IsSwaped));
                if (SelectedOptiFabric is null)
                {
                    this.BtnOptiFabricClear.Visibility = Visibility.Collapsed;
                    this.ImgOptiFabric.Visibility = Visibility.Collapsed;
                    this.LabOptiFabric.Text = OptiFabricError ?? "可以添加";
                    this.LabOptiFabric.Foreground = ModSecret.ColorGray4;
                }
                else
                {
                    this.BtnOptiFabricClear.Visibility = Visibility.Visible;
                    this.ImgOptiFabric.Visibility = Visibility.Visible;
                    this.LabOptiFabric.Text = SelectedOptiFabric.DisplayName.ToLower().Replace("optifabric-", "").Replace(".jar", "").Trim().TrimStart('v');
                    this.LabOptiFabric.Foreground = ModSecret.ColorGray1;
                }
            }
            // 主警告
            if (SelectedFabric is not null && SelectedFabricApi is null)
            {
                this.HintFabricAPI.Visibility = Visibility.Visible;
            }
            else
            {
                this.HintFabricAPI.Visibility = Visibility.Collapsed;
            }
            if (SelectedQuilt is not null && SelectedQSL is null && SelectedFabricApi is null)
            {
                this.HintQSL.Visibility = Visibility.Visible;
            }
            else
            {
                this.HintQSL.Visibility = Visibility.Collapsed;
            }
            if (SelectedQuilt is not null && SelectedFabricApi is not null && ModDownload.DlQSLLoader.Output is not null)
            {
                foreach (var Version in ModDownload.DlQSLLoader.Output)
                {
                    if (IsSuitableQSL(Version.GameVersions, SelectedMinecraftId))
                    {
                        this.HintQuiltFabricAPI.Visibility = Visibility.Visible;
                        break;
                    }
                    else
                    {
                        this.HintQuiltFabricAPI.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                this.HintQuiltFabricAPI.Visibility = Visibility.Collapsed;
            }
            if (SelectedFabric is not null && SelectedOptiFine is not null && SelectedOptiFabric is null)
            {
                if (SelectedMinecraftId.StartsWith("1.14") || SelectedMinecraftId.StartsWith("1.15"))
                {
                    this.HintOptiFabric.Visibility = Visibility.Collapsed;
                    this.HintOptiFabricOld.Visibility = Visibility.Visible;
                }
                else
                {
                    this.HintOptiFabric.Visibility = Visibility.Visible;
                    this.HintOptiFabricOld.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                this.HintOptiFabric.Visibility = Visibility.Collapsed;
                this.HintOptiFabricOld.Visibility = Visibility.Collapsed;
            }
            if (SelectedMinecraftId.Contains("1.") && ModBase.Val(SelectedMinecraftId.Split(".")[1]) >= 16d && SelectedOptiFine is not null && (SelectedForge is not null || SelectedFabric is not null))
            {
                this.HintModOptiFine.Visibility = Visibility.Visible;
            }
            else
            {
                this.HintModOptiFine.Visibility = Visibility.Collapsed;
            }
            // 结束
            IsReloading = false;
        }
        /// <summary>
    /// 清空已选择的项目。
    /// </summary>
        private void SelectClear()
        {
            SelectedMinecraftId = null;
            SelectedMinecraftJsonUrl = null;
            SelectedMinecraftIcon = null;
            SelectedOptiFine = null;
            SelectedLiteLoader = null;
            SelectedLoaderName = null;
            SelectedAPIName = null;
            SelectedForge = null;
            SelectedNeoForge = null;
            SelectedCleanroom = null;
            SelectedFabric = null;
            SelectedFabricApi = null;
            SelectedQuilt = null;
            SelectedQSL = null;
            SelectedOptiFabric = null;
        }

        /// <summary>
    /// 获取版本图标。
    /// </summary>
        private string GetSelectLogo()
        {
            if (SelectedFabric is not null)
            {
                return "pack://application:,,,/images/Blocks/Fabric.png";
            }
            else if (SelectedForge is not null)
            {
                return "pack://application:,,,/images/Blocks/Anvil.png";
            }
            else if (SelectedNeoForge is not null)
            {
                return "pack://application:,,,/images/Blocks/NeoForge.png";
            }
            else if (SelectedLiteLoader is not null)
            {
                return "pack://application:,,,/images/Blocks/Egg.png";
            }
            else if (SelectedOptiFine is not null)
            {
                return "pack://application:,,,/images/Blocks/GrassPath.png";
            }
            else
            {
                return SelectedMinecraftIcon;
            }
        }

        // 版本名处理
        /// <summary>
    /// 获取默认版本名。
    /// </summary>
        private string GetSelectName()
        {
            string Name = SelectedMinecraftId;
            if (SelectedFabric is not null)
            {
                Name += "-Fabric_" + SelectedFabric.Replace("+build", "");
            }
            if (SelectedQuilt is not null)
            {
                Name += "-Quilt_" + SelectedQuilt;
            }
            if (SelectedForge is not null)
            {
                Name += "-Forge_" + SelectedForge.VersionName;
            }
            if (SelectedNeoForge is not null)
            {
                Name += "-NeoForge_" + SelectedNeoForge.VersionName;
            }
            if (SelectedCleanroom is not null)
            {
                Name += "-Cleanroom_" + SelectedCleanroom.VersionName;
            }
            if (SelectedLiteLoader is not null)
            {
                Name += "-LiteLoader";
            }
            if (SelectedOptiFine is not null)
            {
                Name += "-OptiFine_" + SelectedOptiFine.NameDisplay.Replace(SelectedMinecraftId + " ", "").Replace(" ", "_");
            }
            return Name;
        }
        private bool IsSelectNameEdited = false;
        private bool IsSelectNameChanging = false;
        private void SelectNameUpdate()
        {
            if (IsSelectNameEdited || IsSelectNameChanging)
                return;
            IsSelectNameChanging = true;
            this.TextSelectName.Text = GetSelectName();
            IsSelectNameChanging = false;
        }
        private void TextSelectName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsSelectNameChanging)
                return;
            IsSelectNameEdited = true;
            SelectReload();
        }
        private void TextSelectName_ValidateChanged(object sender, EventArgs e)
        {
            this.BtnStart.IsEnabled = this.TextSelectName.IsValidated;
        }

        #endregion

        #region 加载器

        // 结果数据化
        private void LoadMinecraft_OnFinish()
        {
            ExitSelectPage(); // 返回
            do
            {
                try
                {
                    var Dict = new Dictionary<string, List<JObject>>() { { "正式版", new List<JObject>() }, { "预览版", new List<JObject>() }, { "远古版", new List<JObject>() }, { "愚人节版", new List<JObject>() } };
                    JArray Versions = (JArray)ModDownload.DlClientListLoader.Output.Value["versions"];
                    foreach (JObject Version in Versions)
                    {
                        // 确定分类
                        string Type = (string)Version["type"];
                        switch (Type ?? "")
                        {
                            case "release":
                                {
                                    Type = "正式版";
                                    break;
                                }
                            case "snapshot":
                                {
                                    Type = "预览版";
                                    // Mojang 误分类
                                    if (Version["id"].ToString().StartsWith("1.") && !Version["id"].ToString().ToLower().Contains("combat") && !Version["id"].ToString().ToLower().Contains("rc") && !Version["id"].ToString().ToLower().Contains("experimental") && !Version["id"].ToString().ToLower().Equals("1.2") && !Version["id"].ToString().ToLower().Contains("pre"))
                                    {
                                        Type = "正式版";
                                        Version["type"] = "release";
                                    }
                                    // 愚人节版本
                                    switch (Version["id"].ToString().ToLower() ?? "")
                                    {
                                        case "2point0_blue":
                                        case "2point0_red":
                                        case "2point0_purple":
                                        case "2.0_blue":
                                        case "2.0_red":
                                        case "2.0_purple":
                                        case "2.0":
                                            {
                                                Type = "愚人节版";
                                                Version["id"] = Version["id"].ToString().Replace("point", ".");
                                                Version["type"] = "special";
                                                Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"]));
                                                break;
                                            }
                                        case "20w14infinite":
                                        case "20w14∞":
                                            {
                                                Type = "愚人节版";
                                                Version["id"] = "20w14∞";
                                                Version["type"] = "special";
                                                Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"]));
                                                break;
                                            }
                                        case "3d shareware v1.34":
                                        case "1.rv-pre1":
                                        case "15w14a":
                                        case var @case when @case == "2.0":
                                        case "22w13oneblockatatime":
                                        case "23w13a_or_b":
                                        case "24w14potato":
                                            {
                                                Type = "愚人节版";
                                                Version["type"] = "special";
                                                Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"])); // 4/1 自动视作愚人节版
                                                break;
                                            }

                                        default:
                                            {
                                                var ReleaseDate = Version["releaseTime"].Value<DateTime>().ToUniversalTime().AddHours(2d);
                                                if (ReleaseDate.Month == 4 && ReleaseDate.Day == 1)
                                                {
                                                    Type = "愚人节版";
                                                    Version["type"] = "special";
                                                }

                                                break;
                                            }
                                    }

                                    break;
                                }
                            case "special":
                                {
                                    // 已被处理的愚人节版
                                    Type = "愚人节版";
                                    break;
                                }

                            default:
                                {
                                    Type = "远古版";
                                    break;
                                }
                        }
                        // 加入辞典
                        Dict[Type].Add(Version);
                    }
                    // 排序
                    foreach (var Pair in Dict.ToList())
                        Dict[Pair.Key] = Pair.Value.OrderByDescending(j => j["releaseTime"].Value<DateTime>()).ToList();
                    // 清空当前
                    this.PanMinecraft.Children.Clear();
                    // 添加最新版本
                    var CardInfo = new MyCard() { Title = "最新版本", Margin = new Thickness(0d, 15d, 0d, 15d) };
                    var TopestVersions = new List<JObject>();
                    JObject Release = (JObject)Dict["正式版"][0].DeepClone();
                    Release["lore"] = "最新正式版，发布于 " + Release["releaseTime"].Value<DateTime>().ToString("yyyy'/'MM'/'dd HH':'mm");
                    TopestVersions.Add(Release);
                    if (Dict["正式版"][0]["releaseTime"].Value<DateTime>() < Dict["预览版"][0]["releaseTime"].Value<DateTime>())
                    {
                        JObject Snapshot = (JObject)Dict["预览版"][0].DeepClone();
                        Snapshot["lore"] = "最新预览版，发布于 " + Snapshot["releaseTime"].Value<DateTime>().ToString("yyyy'/'MM'/'dd HH':'mm");
                        TopestVersions.Add(Snapshot);
                    }
                    var PanInfo = new StackPanel() { Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d), Tag = TopestVersions };
                    void StackInstall(StackPanel Stack) { foreach (var item in (IEnumerable)Stack.Tag) Stack.Children.Add(ModDownloadLib.McDownloadListItem((JObject)item, (sender, e) => ModMain.FrmDownloadInstall.MinecraftSelected((MyListItem)sender, e), false)); };
                    MyCard.StackInstall(ref PanInfo, StackInstall);
                    CardInfo.Children.Add(PanInfo);
                    this.PanMinecraft.Children.Insert(0, CardInfo);
                    // 添加其他版本
                    foreach (KeyValuePair<string, List<JObject>> Pair in Dict)
                    {
                        if (!Pair.Value.Any())
                            continue;
                        // 增加卡片
                        var NewCard = new MyCard() { Title = Pair.Key + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                        var NewStack = new StackPanel() { Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d), Tag = Pair.Value };
                        NewCard.Children.Add(NewStack);
                        NewCard.SwapControl = NewStack;
                        // 不能使用 AddressOf，这导致了 #535，原因完全不明，疑似是编译器 Bug
                        NewCard.InstallMethod = StackInstall;
                        NewCard.IsSwaped = true;
                        this.PanMinecraft.Children.Add(NewCard);
                    }
                    // 自动选择版本
                    if (McVersionWaitingForSelect is null)
                        break;
                    ModBase.Log("[Download] 自动选择 MC 版本：" + McVersionWaitingForSelect);
                    foreach (JObject Version in Versions)
                    {
                        if ((Version["id"].ToString() ?? "") != (McVersionWaitingForSelect ?? ""))
                            continue;
                        var Item = ModDownloadLib.McDownloadListItem(Version, () => { }, false);
                        MinecraftSelected(Item, null);
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "可视化安装版本列表出错", ModBase.LogLevel.Feedback);
                }
            }
            while (false);
        }
        /// <summary>
    /// 当 MC 版本列表加载完时，立即自动选择的版本。用于外部调用。
    /// </summary>
        public static string McVersionWaitingForSelect = null;

        #endregion

        #region OptiFine 列表

        /// <summary>
    /// 获取 OptiFine 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadOptiFineGetError()
        {
            if (SelectedLoaderName == "NeoForge" || SelectedLoaderName == "Quilt")
                return $"与 {SelectedLoaderName} 不兼容";
            if (this.LoadOptiFine is null || this.LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "加载中……";
            if (this.LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadOptiFine.State).Error.Message));
            // 是否有 Cleanroom
            if (SelectedCleanroom is not null)
                return "与 Cleanroom 不兼容";
            // 检查 Forge 1.13 - 1.14.3：全部不兼容
            if (SelectedLoaderName == "Forge" && ModMinecraft.VersionSortInteger(SelectedMinecraftId, "1.13") >= 0 && ModMinecraft.VersionSortInteger("1.14.3", SelectedMinecraftId) >= 0)
            {
                return "与 Forge 不兼容";
            }
            // 检查 Forge 版本
            bool HasAny = false;
            bool HasRequiredVersion = false;
            foreach (ModDownload.DlOptiFineListEntry OptiFineVersion in ModDownload.DlOptiFineListLoader.Output.Value)
            {
                if (!OptiFineVersion.NameDisplay.StartsWith(SelectedMinecraftId + " "))
                    continue; // 不是同一个大版本
                HasAny = true;
                if (SelectedForge is null)
                    return null; // 未选择 Forge
                if (Conversions.ToBoolean(IsOptiFineSuitForForge(OptiFineVersion, SelectedForge)))
                    return null; // 该版本可用
                if (OptiFineVersion.RequiredForgeVersion is not null)
                    HasRequiredVersion = true;
            }
            if (!HasAny)
            {
                return "不可用";
            }
            else if (HasRequiredVersion)
            {
                return "仅兼容特定版本的 Forge";
            }
            else
            {
                return "与 Forge 不兼容";
            }
        }

        // 检查某个 OptiFine 是否与某个 Forge 兼容
        private bool IsOptiFineSuitForForge(ModDownload.DlOptiFineListEntry OptiFine, ModDownload.DlForgeVersionEntry Forge)
        {
            if ((Forge.Inherit ?? "") != (OptiFine.Inherit ?? ""))
                return false; // 不是同一个大版本
            if (OptiFine.RequiredForgeVersion is null)
                return false; // 不兼容 Forge
            if (string.IsNullOrWhiteSpace(OptiFine.RequiredForgeVersion))
                return true; // #4183
            if (OptiFine.RequiredForgeVersion.Contains(".")) // XX.X.XXX
            {
                return ModMinecraft.VersionSortInteger(Forge.Version.ToString(), OptiFine.RequiredForgeVersion) == 0;
            }
            else // XXXX
            {
                return Forge.Version.Revision == Conversions.ToDouble(OptiFine.RequiredForgeVersion);
            }
        }

        // 限制展开
        private void CardOptiFine_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadOptiFineGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 OptiFine 版本列表。
    /// </summary>
        private void OptiFine_Loaded()
        {
            try
            {
                if (ModDownload.DlOptiFineListLoader.State != ModBase.LoadState.Finished)
                    return;

                // 获取版本列表
                var Versions = new List<ModDownload.DlOptiFineListEntry>();
                foreach (ModDownload.DlOptiFineListEntry Version in ModDownload.DlOptiFineListLoader.Output.Value)
                {
                    if (Conversions.ToBoolean(SelectedForge is not null && !IsOptiFineSuitForForge(Version, SelectedForge)))
                        continue;
                    if (Version.NameDisplay.StartsWith(SelectedMinecraftId + " "))
                        Versions.Add(Version);
                }
                if (!Versions.Any())
                    return;
                // 排序
                Versions = Versions.Sort((Left, Right) =>
        {
            if (!Left.IsPreview && Right.IsPreview)
                return true;
            if (Left.IsPreview && !Right.IsPreview)
                return false;
            return ModMinecraft.VersionSortBoolean(Left.NameDisplay, Right.NameDisplay);
        });
                // 可视化
                this.PanOptiFine.Children.Clear();
                foreach (var Version in Versions)
                    this.PanOptiFine.Children.Add(ModDownloadLib.OptiFineDownloadListItem(Version, (_, __) => this.OptiFine_Selected(), false));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 OptiFine 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void OptiFine_Selected(MyListItem sender, EventArgs e)
        {
            SelectedOptiFine = (ModDownload.DlOptiFineListEntry)sender.Tag;
            if (Conversions.ToBoolean(SelectedForge is not null && !IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge)))
                SelectedForge = null;
            OptiFabric_Loaded();
            Forge_Loaded();
            NeoForge_Loaded();
            this.CardOptiFine.IsSwaped = true;
            SelectReload();
        }
        private void OptiFine_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedOptiFine = null;
            SelectedOptiFabric = null;
            AutoSelectedOptiFabric = false;
            this.CardOptiFine.IsSwaped = true;
            e.Handled = true;
            Forge_Loaded();
            NeoForge_Loaded();
            SelectReload();
        }

        #endregion

        #region LiteLoader 列表

        /// <summary>
    /// 获取 LiteLoader 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadLiteLoaderGetError()
        {
            if (!SelectedMinecraftId.Contains("1.") || ModBase.Val(SelectedMinecraftId.Split(".")[1]) > 12d)
                return "不可用";
            if (this.LoadLiteLoader is null || this.LoadLiteLoader.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "加载中……";
            if (this.LoadLiteLoader.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadLiteLoader.State).Error.Message));
            foreach (ModDownload.DlLiteLoaderListEntry Version in ModDownload.DlLiteLoaderListLoader.Output.Value)
            {
                if ((Version.Inherit ?? "") == (SelectedMinecraftId ?? ""))
                    return null;
            }
            return "不可用";
        }

        // 限制展开
        private void CardLiteLoader_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadLiteLoaderGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 LiteLoader 版本列表。
    /// </summary>
        private void LiteLoader_Loaded()
        {
            try
            {
                if (ModDownload.DlLiteLoaderListLoader.State != ModBase.LoadState.Finished)
                    return;
                // 获取版本列表
                var Versions = new List<ModDownload.DlLiteLoaderListEntry>();
                foreach (ModDownload.DlLiteLoaderListEntry Version in ModDownload.DlLiteLoaderListLoader.Output.Value)
                {
                    if ((Version.Inherit ?? "") == (SelectedMinecraftId ?? ""))
                        Versions.Add(Version);
                }
                if (!Versions.Any())
                    return;
                // 可视化
                this.PanLiteLoader.Children.Clear();
                foreach (var Version in Versions)
                    this.PanLiteLoader.Children.Add(ModDownloadLib.LiteLoaderDownloadListItem(Version, (_, __) => this.LiteLoader_Selected(), false));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 LiteLoader 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void LiteLoader_Selected(MyListItem sender, EventArgs e)
        {
            SelectedLiteLoader = (ModDownload.DlLiteLoaderListEntry)sender.Tag;
            this.CardLiteLoader.IsSwaped = true;
            SelectReload();
        }
        private void LiteLoader_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedLiteLoader = null;
            this.CardLiteLoader.IsSwaped = true;
            e.Handled = true;
            SelectReload();
        }

        #endregion

        #region Forge 列表

        /// <summary>
    /// 获取 Forge 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadForgeGetError()
        {
            if (!SelectedMinecraftId.StartsWith("1."))
                return "不可用";
            if (!this.LoadForge.State.IsLoader)
                return "加载中……";
            ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>> Loader = (ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)this.LoadForge.State;
            if ((SelectedMinecraftId ?? "") != (Loader.Input ?? ""))
                return "加载中……";
            if (Loader.State == ModBase.LoadState.Loading)
                return "加载中……";
            if (Loader.State == ModBase.LoadState.Failed)
            {
                string ErrorMessage = Loader.Error.Message;
                if (ErrorMessage.Contains("不可用"))
                {
                    return "不可用";
                }
                else
                {
                    return "获取版本列表失败：" + ErrorMessage;
                }
            }
            if (Loader.State != ModBase.LoadState.Finished)
                return "获取版本列表失败：未知错误，状态为 " + ModBase.GetStringFromEnum(Loader.State);
            bool NotSuitForOptiFine = false;
            foreach (var Version in Loader.Output)
            {
                if (Version.Category == "universal" || Version.Category == "client")
                    continue; // 跳过无法自动安装的版本
                if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Forge"))
                    return $"与 {SelectedLoaderName} 不兼容";
                if (SelectedOptiFine is not null && ModMinecraft.VersionSortInteger(SelectedMinecraftId, "1.13") >= 0 && ModMinecraft.VersionSortInteger("1.14.3", SelectedMinecraftId) >= 0)
                {
                    return "与 OptiFine 不兼容"; // 1.13 ~ 1.14.3 OptiFine 检查
                }
                if (Conversions.ToBoolean(SelectedOptiFine is not null && !IsOptiFineSuitForForge(SelectedOptiFine, Version)))
                {
                    NotSuitForOptiFine = true; // 与 OptiFine 不兼容
                    continue;
                }
                return null;
            }
            return NotSuitForOptiFine ? "与 OptiFine 不兼容" : "该版本不支持自动安装";
        }

        // 限制展开
        private void CardForge_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadForgeGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 Forge 版本列表。
    /// </summary>
        private void Forge_Loaded()
        {
            try
            {
                if (!this.LoadForge.State.IsLoader)
                    return;
                ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>> Loader = (ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)this.LoadForge.State;
                if ((SelectedMinecraftId ?? "") != (Loader.Input ?? ""))
                    return;
                if (Loader.State != ModBase.LoadState.Finished)
                    return;
                // 获取要显示的版本
                var Versions = Loader.Output.ToList(); // 复制数组，以免 Output 在实例化后变空
                if (!Loader.Output.Any())
                    return;
                this.PanForge.Children.Clear();
                Versions = Versions.Where(v =>
        {
            if (v.Category == "universal" || v.Category == "client")
                return false; // 跳过无法自动安装的版本
            if (Conversions.ToBoolean(SelectedOptiFine is not null && !IsOptiFineSuitForForge(SelectedOptiFine, v)))
                return false;
            return true;
        }).OrderByDescending(v => v.Version).ToList();
                ModDownloadLib.ForgeDownloadListItemPreload(this.PanForge, Versions, (_, __) => this.Forge_Selected(), false);
                foreach (var Version in Versions)
                    this.PanForge.Children.Add(ModDownloadLib.ForgeDownloadListItem(Version, (_, __) => this.Forge_Selected(), false));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Forge 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void Forge_Selected(MyListItem sender, EventArgs e)
        {
            SelectedForge = (ModDownload.DlForgeVersionEntry)sender.Tag;
            SelectedLoaderName = "Forge";
            this.CardForge.IsSwaped = true;
            if (Conversions.ToBoolean(SelectedOptiFine is not null && !IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge)))
                SelectedOptiFine = null;
            OptiFine_Loaded();
            SelectReload();
        }
        private void Forge_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedForge = null;
            SelectedLoaderName = null;
            this.CardForge.IsSwaped = true;
            e.Handled = true;
            OptiFine_Loaded();
            SelectReload();
        }

        #endregion

        #region NeoForge 列表

        /// <summary>
    /// 获取 NeoForge 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadNeoForgeGetError()
        {
            if (!SelectedMinecraftId.StartsWith("1."))
                return "不可用";
            if (SelectedOptiFine is not null)
                return "与 OptiFine 不兼容";
            if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "NeoForge"))
                return $"与 {SelectedLoaderName} 不兼容";
            if (this.LoadNeoForge is null || this.LoadNeoForge.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "加载中……";
            if (this.LoadNeoForge.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadNeoForge.State).Error.Message));
            if (ModDownload.DlNeoForgeListLoader.Output.Value.Any(v => (v.Inherit ?? "") == (SelectedMinecraftId ?? "")))
            {
                return null;
            }
            else
            {
                return "不可用";
            }
        }

        // 限制展开
        private void CardNeoForge_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadNeoForgeGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 NeoForge 版本列表。
    /// </summary>
        private void NeoForge_Loaded()
        {
            try
            {
                // 获取版本列表
                if (ModDownload.DlNeoForgeListLoader.State != ModBase.LoadState.Finished)
                    return;
                var Versions = ModDownload.DlNeoForgeListLoader.Output.Value.Where(v => (v.Inherit ?? "") == (SelectedMinecraftId ?? "")).ToList();
                if (!Versions.Any())
                    return;
                // 可视化
                this.PanNeoForge.Children.Clear();
                ModDownloadLib.NeoForgeDownloadListItemPreload(this.PanNeoForge, Versions, (_, __) => this.NeoForge_Selected(), false);
                foreach (var Version in Versions)
                    this.PanNeoForge.Children.Add(ModDownloadLib.NeoForgeDownloadListItem(Version, (_, __) => this.NeoForge_Selected(), false));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 NeoForge 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void NeoForge_Selected(MyListItem sender, EventArgs e)
        {
            SelectedNeoForge = (ModDownload.DlNeoForgeListEntry)sender.Tag;
            SelectedLoaderName = "NeoForge";
            this.CardNeoForge.IsSwaped = true;
            OptiFine_Loaded();
            SelectReload();
        }
        private void NeoForge_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedNeoForge = null;
            SelectedLoaderName = null;
            this.CardNeoForge.IsSwaped = true;
            e.Handled = true;
            OptiFine_Loaded();
            SelectReload();
        }

        #endregion

        #region Cleanroom 列表

        /// <summary>
    /// 获取 Cleanroom 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadCleanroomGetError()
        {
            if (!SelectedMinecraftId.StartsWith("1."))
                return "没有可用版本";
            if (SelectedOptiFine is not null)
                return "与 OptiFine 不兼容";
            if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Cleanroom"))
                return $"与 {SelectedLoaderName} 不兼容";
            if (this.LoadCleanroom is null || this.LoadCleanroom.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "正在获取版本列表……";
            if (this.LoadCleanroom.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadCleanroom.State).Error.Message));
            return null;
            // If DlCleanroomListLoader.Output.Value.Any(Function(v) v.Inherit = SelectedMinecraftId) Then
            // Return Nothing
            // Else
            // Return "没有可用版本"
            // End If
        }

        // 限制展开
        private void CardCleanroom_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadCleanroomGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 Cleanroom 版本列表。
    /// </summary>
        private void Cleanroom_Loaded()
        {
            try
            {
                // 获取版本列表
                if (ModDownload.DlCleanroomListLoader.State != ModBase.LoadState.Finished)
                    return;
                var Versions = ModDownload.DlCleanroomListLoader.Output.Value.Where(v => (v.Inherit ?? "") == (SelectedMinecraftId ?? "")).ToList();
                if (!Versions.Any())
                    return;
                // 可视化
                this.PanCleanroom.Children.Clear();
                ModDownloadLib.CleanroomDownloadListItemPreload(this.PanCleanroom, Versions, (_, __) => this.Cleanroom_Selected(), false);
                foreach (var Version in Versions)
                    this.PanCleanroom.Children.Add(ModDownloadLib.CleanroomDownloadListItem(Version, (_, __) => this.Cleanroom_Selected(), false));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Cleanroom 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void Cleanroom_Selected(MyListItem sender, EventArgs e)
        {
            SelectedCleanroom = (ModDownload.DlCleanroomListEntry)sender.Tag;
            SelectedLoaderName = "Cleanroom";
            this.CardCleanroom.IsSwaped = true;
            OptiFine_Loaded();
            SelectReload();
        }
        private void Cleanroom_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedCleanroom = null;
            SelectedLoaderName = null;
            this.CardCleanroom.IsSwaped = true;
            e.Handled = true;
            OptiFine_Loaded();
            SelectReload();
        }

        #endregion

        #region Fabric 列表

        /// <summary>
    /// 获取 Fabric 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadFabricGetError()
        {
            if (this.LoadFabric is null || this.LoadFabric.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "加载中……";
            if (this.LoadFabric.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadFabric.State).Error.Message));
            foreach (JObject Version in ModDownload.DlFabricListLoader.Output.Value["game"])
            {
                if ((Version["version"].ToString() ?? "") == (SelectedMinecraftId.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") ?? ""))
                {
                    if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Fabric"))
                        return $"与 {SelectedLoaderName} 不兼容";
                    return null;
                }
            }
            return "不可用";
        }

        // 限制展开
        private void CardFabric_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadFabricGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 Fabric 版本列表。
    /// </summary>
        private void Fabric_Loaded()
        {
            try
            {
                if (ModDownload.DlFabricListLoader.State != ModBase.LoadState.Finished)
                    return;
                // 获取版本列表
                JArray Versions = (JArray)ModDownload.DlFabricListLoader.Output.Value["loader"];
                if (!Versions.Any())
                    return;
                // 可视化
                this.PanFabric.Children.Clear();
                this.PanFabric.Tag = Versions;
                this.CardFabric.SwapControl = this.PanFabric;
                this.CardFabric.InstallMethod = new Action<StackPanel>((Stack) => { foreach (var item in (IEnumerable)Stack.Tag) Stack.Children.Add(ModDownloadLib.FabricDownloadListItem((JObject)item, (_, __) => ModMain.FrmDownloadInstall.Fabric_Selected())); });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Fabric 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        public void Fabric_Selected(MyListItem sender, EventArgs e)
        {
            SelectedFabric = sender.Tag("version").ToString();
            SelectedLoaderName = "Fabric";
            FabricApi_Loaded();
            OptiFabric_Loaded();
            this.CardFabric.IsSwaped = true;
            SelectReload();
        }
        private void Fabric_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedFabric = null;
            SelectedFabricApi = null;
            AutoSelectedFabricApi = false;
            SelectedOptiFabric = null;
            AutoSelectedOptiFabric = false;
            SelectedLoaderName = null;
            SelectedAPIName = null;
            this.CardFabric.IsSwaped = true;
            e.Handled = true;
            SelectReload();
        }

        #endregion

        #region Fabric API 列表

        /// <summary>
    /// 从显示名判断该 API 是否与某版本适配。
    /// </summary>
        public static bool IsSuitableFabricApi(string DisplayName, string MinecraftVersion)
        {
            try
            {
                if (DisplayName is null || MinecraftVersion is null)
                    return false;
                DisplayName = DisplayName.ToLower();
                MinecraftVersion = MinecraftVersion.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3").ToLower();
                if (DisplayName.StartsWith("[" + MinecraftVersion + "]"))
                    return true;
                if (!DisplayName.Contains("/") || !DisplayName.Contains("]"))
                    return false;
                // 直接的判断（例如 1.18.1/22w03a）
                foreach (string Part in DisplayName.BeforeFirst("]").TrimStart("[").Split("/"))
                {
                    if ((Part ?? "") == (MinecraftVersion ?? ""))
                        return true;
                }
                // 将版本名分割语素（例如 1.16.4/5）
                var Lefts = DisplayName.BeforeFirst("]").RegexSearch("[a-z/]+|[0-9/]+");
                var Rights = MinecraftVersion.BeforeFirst("]").RegexSearch("[a-z/]+|[0-9/]+");
                // 对每段进行判断
                int i = 0;
                while (true)
                {
                    // 两边均缺失，感觉是一个东西
                    if (Lefts.Count - 1 < i && Rights.Count - 1 < i)
                        return true;
                    // 确定两边是否一致
                    string LeftValue = Lefts.Count - 1 < i ? "-1" : Lefts[i];
                    string RightValue = Rights.Count - 1 < i ? "-1" : Rights[i];
                    if (!LeftValue.Contains("/"))
                    {
                        if ((LeftValue ?? "") != (RightValue ?? ""))
                            return false;
                    }
                    // 左边存在斜杠
                    else if (!LeftValue.Contains(RightValue))
                        return false;
                    i += 1;
                }
                return true;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "判断 Fabric API 版本适配性出错（" + DisplayName + ", " + MinecraftVersion + "）");
                return false;
            }
        }

        /// <summary>
    /// 获取 FabricApi 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadFabricApiGetError()
        {
            if (this.LoadFabricApi is null || this.LoadFabricApi.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "加载中……";
            if (this.LoadFabricApi.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadFabricApi.State).Error.Message));
            if (SelectedAPIName is not null && !ReferenceEquals(SelectedAPIName, "Fabric API"))
                return $"与 {SelectedAPIName} 不兼容";
            if (ModDownload.DlFabricApiLoader.Output is null)
            {
                if (SelectedFabric is null && SelectedQuilt is null)
                    return "需要安装 Fabric / Quilt";
                return "加载中……";
            }
            foreach (var Version in ModDownload.DlFabricApiLoader.Output)
            {
                if (!IsSuitableFabricApi(Version.DisplayName, SelectedMinecraftId))
                    continue;
                if (SelectedFabric is null && SelectedQuilt is null)
                    return "需要安装 Fabric / Quilt";
                return null;
            }
            return "不可用";
        }

        // 限制展开
        private void CardFabricApi_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadFabricApiGetError() is not null)
                e.Handled = true;
        }

        private bool AutoSelectedFabricApi = false;
        /// <summary>
    /// 尝试重新可视化 FabricApi 版本列表。
    /// </summary>
        private void FabricApi_Loaded()
        {
            try
            {
                if (ModDownload.DlFabricApiLoader.State != ModBase.LoadState.Finished)
                    return;
                if (SelectedMinecraftId is null || SelectedFabric is null && SelectedQuilt is null)
                    return;
                // 获取版本列表
                var Versions = new List<ModComp.CompFile>();
                foreach (var Version in ModDownload.DlFabricApiLoader.Output)
                {
                    if (IsSuitableFabricApi(Version.DisplayName, SelectedMinecraftId))
                    {
                        if (!Version.DisplayName.StartsWith("["))
                        {
                            ModBase.Log("[Download] 已特判修改 Fabric API 显示名：" + Version.DisplayName, ModBase.LogLevel.Debug);
                            Version.DisplayName = "[" + SelectedMinecraftId + "] " + Version.DisplayName;
                        }
                        Versions.Add(Version);
                    }
                }
                if (!Versions.Any())
                    return;
                Versions = Versions.OrderByDescending(v => v.ReleaseDate).ToList();
                // 可视化
                this.PanFabricApi.Children.Clear();
                foreach (var Version in Versions)
                {
                    if (!IsSuitableFabricApi(Version.DisplayName, SelectedMinecraftId))
                        continue;
                    this.PanFabricApi.Children.Add(ModDownloadLib.FabricApiDownloadListItem(Version, (_, __) => this.FabricApi_Selected()));
                }
                // 自动选择 Fabric API
                if (!AutoSelectedFabricApi && SelectedQuilt is null || SelectedQuilt is not null && ReferenceEquals(LoadQSLGetError(), "没有可用版本"))
                {
                    AutoSelectedFabricApi = true;
                    ModBase.Log($"[Download] 已自动选择 Fabric API：{((MyListItem)this.PanFabricApi.Children[0]).Title}");
                    FabricApi_Selected((MyListItem)this.PanFabricApi.Children[0], null);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Fabric API 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void FabricApi_Selected(MyListItem sender, EventArgs e)
        {
            SelectedFabricApi = (ModComp.CompFile)sender.Tag;
            SelectedAPIName = "Fabric API";
            this.CardFabricApi.IsSwaped = true;
            SelectReload();
        }
        private void FabricApi_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedFabricApi = null;
            SelectedAPIName = null;
            this.CardFabricApi.IsSwaped = true;
            e.Handled = true;
            SelectReload();
        }

        #endregion

        #region Quilt 列表

        /// <summary>
    /// 获取 Quilt 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadQuiltGetError()
        {
            if (this.LoadQuilt is null || this.LoadQuilt.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "加载中……";
            if (this.LoadQuilt.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadQuilt.State).Error.Message));
            foreach (JObject Version in ModDownload.DlQuiltListLoader.Output.Value["game"])
            {
                if ((Version["version"].ToString() ?? "") == (SelectedMinecraftId.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") ?? ""))
                {
                    if (SelectedOptiFine is not null)
                        return "与 OptiFine 不兼容";
                    if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Quilt"))
                        return $"与 {SelectedLoaderName} 不兼容";
                    return null;
                }
            }
            return "不可用";
        }

        // 限制展开
        private void CardQuilt_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadQuiltGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 Quilt 版本列表。
    /// </summary>
        private void Quilt_Loaded()
        {
            try
            {
                if (ModDownload.DlQuiltListLoader.State != ModBase.LoadState.Finished)
                    return;
                // 获取版本列表
                JArray Versions = (JArray)ModDownload.DlQuiltListLoader.Output.Value["loader"];
                if (!Versions.Any())
                    return;
                // 可视化
                this.PanQuilt.Children.Clear();
                this.PanQuilt.Tag = Versions;
                this.CardQuilt.SwapControl = this.PanQuilt;
                this.CardQuilt.InstallMethod = new Action<StackPanel>((Stack) => { foreach (var item in (IEnumerable)Stack.Tag) Stack.Children.Add(ModDownloadLib.QuiltDownloadListItem((JObject)item, (_, __) => ModMain.FrmDownloadInstall.Quilt_Selected())); });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 Quilt 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        public void Quilt_Selected(MyListItem sender, EventArgs e)
        {
            SelectedQuilt = sender.Tag("version").ToString();
            SelectedLoaderName = "Quilt";
            FabricApi_Loaded();
            QSL_Loaded();
            this.CardQuilt.IsSwaped = true;
            SelectReload();
        }
        private void Quilt_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedQuilt = null;
            SelectedQSL = null;
            SelectedFabricApi = null;
            SelectedLoaderName = null;
            SelectedAPIName = null;
            this.CardQuilt.IsSwaped = true;
            e.Handled = true;
            SelectReload();
        }

        #endregion

        #region QSL 列表

        /// <summary>
    /// 从显示名判断该 API 是否与某版本适配。
    /// </summary>
        public static bool IsSuitableQSL(List<string> SupportVersions, string MinecraftVersion)
        {
            try
            {
                if (SupportVersions.Contains(MinecraftVersion))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "判断 QSL 版本适配性出错（" + SupportVersions.ToString() + ", " + MinecraftVersion + "）");
                return false;
            }
        }

        /// <summary>
    /// 获取 QSL 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadQSLGetError()
        {
            if (this.LoadQSL is null || this.LoadQSL.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "正在获取版本列表……";
            if (this.LoadQSL.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadQSL.State).Error.Message));
            if (SelectedAPIName is not null && !ReferenceEquals(SelectedAPIName, "QFAPI / QSL"))
                return $"与 {SelectedAPIName} 不兼容";
            if (ModDownload.DlQSLLoader.Output is null)
            {
                if (SelectedQuilt is null)
                    return "需要安装 Quilt";
                return "正在获取版本列表……";
            }
            foreach (var Version in ModDownload.DlQSLLoader.Output)
            {
                if (!IsSuitableQSL(Version.GameVersions, SelectedMinecraftId))
                    continue;
                if (SelectedQuilt is null)
                    return "需要安装 Quilt";
                return null;
            }
            return "没有可用版本";
        }

        // 限制展开
        private void CardQSL_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadQSLGetError() is not null)
                e.Handled = true;
        }

        private bool AutoSelectedQSL = false;
        /// <summary>
    /// 尝试重新可视化 QSL 版本列表。
    /// </summary>
        private void QSL_Loaded()
        {
            try
            {
                if (ModDownload.DlQSLLoader.State != ModBase.LoadState.Finished)
                    return;
                if (SelectedMinecraftId is null || SelectedQuilt is null)
                    return;
                // 获取版本列表
                var Versions = new List<ModComp.CompFile>();
                foreach (var Version in ModDownload.DlQSLLoader.Output)
                {
                    if (IsSuitableQSL(Version.GameVersions, SelectedMinecraftId))
                    {
                        if (!Version.DisplayName.StartsWith("["))
                        {
                            ModBase.Log("[Download] 已特判修改 QSL 显示名：" + Version.DisplayName, ModBase.LogLevel.Debug);
                            Version.DisplayName = "[" + SelectedMinecraftId + "] " + Version.DisplayName;
                        }
                        Versions.Add(Version);
                    }
                }
                if (!Versions.Any())
                    return;
                Versions = ModBase.Sort(Versions, (a, b) => a.ReleaseDate > b.ReleaseDate);
                // 可视化
                this.PanQSL.Children.Clear();
                foreach (var Version in Versions)
                {
                    if (!IsSuitableQSL(Version.GameVersions, SelectedMinecraftId))
                        continue;
                    this.PanQSL.Children.Add(ModDownloadLib.QSLDownloadListItem(Version, (_, __) => this.QSL_Selected()));
                }
                // 自动选择 QSL
                if (!AutoSelectedQSL)
                {
                    AutoSelectedQSL = true;
                    ModBase.Log($"[Download] 已自动选择 QSL：{((MyListItem)this.PanQSL.Children[0]).Title}");
                    QSL_Selected((MyListItem)this.PanQSL.Children[0], null);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 QSL 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void QSL_Selected(MyListItem sender, EventArgs e)
        {
            SelectedQSL = (ModComp.CompFile)sender.Tag;
            SelectedAPIName = "QFAPI / QSL";
            this.CardQSL.IsSwaped = true;
            SelectReload();
        }
        private void QSL_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedQSL = null;
            SelectedAPIName = null;
            this.CardQSL.IsSwaped = true;
            e.Handled = true;
            SelectReload();
        }

        #endregion

        #region OptiFabric 列表

        /// <summary>
    /// 从显示名判断该 Mod 是否与某版本适配。
    /// </summary>
        private bool IsSuitableOptiFabric(ModComp.CompFile ModFile, string MinecraftVersion)
        {
            try
            {
                if (MinecraftVersion is null)
                    return false;
                return ModFile.GameVersions.Contains(MinecraftVersion);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "判断 OptiFabric 版本适配性出错（" + MinecraftVersion + "）");
                return false;
            }
        }

        private bool AutoSelectedOptiFabric = false;
        /// <summary>
    /// 获取 OptiFabric 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
        private string LoadOptiFabricGetError()
        {
            if (SelectedMinecraftId.StartsWith("1.14") || SelectedMinecraftId.StartsWith("1.15"))
                return "不兼容老版本 Fabric，请手动下载 OptiFabric Origins";
            if (this.LoadOptiFabric is null || this.LoadOptiFabric.State.LoadingState == MyLoading.MyLoadingState.Run)
                return "加载中……";
            if (this.LoadOptiFabric.State.LoadingState == MyLoading.MyLoadingState.Error)
                return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：", ((object)this.LoadOptiFabric.State).Error.Message));
            if (ModDownload.DlOptiFabricLoader.Output is null)
            {
                if (SelectedFabric is null && SelectedOptiFine is null)
                    return "需要安装 OptiFine 与 Fabric";
                if (SelectedFabric is null)
                    return "需要安装 Fabric";
                if (SelectedOptiFine is null)
                    return "需要安装 OptiFine";
                return "加载中……";
            }
            foreach (var Version in ModDownload.DlOptiFabricLoader.Output)
            {
                if (!IsSuitableOptiFabric(Version, SelectedMinecraftId))
                    continue; // 2135#
                if (SelectedFabric is null && SelectedOptiFine is null)
                    return "需要安装 OptiFine 与 Fabric";
                if (SelectedFabric is null)
                    return "需要安装 Fabric";
                if (SelectedOptiFine is null)
                    return "需要安装 OptiFine";
                return null; // 通过检查
            }
            return "不可用";
        }

        // 限制展开
        private void CardOptiFabric_PreviewSwap(object sender, ModBase.RouteEventArgs e)
        {
            if (LoadOptiFabricGetError() is not null)
                e.Handled = true;
        }

        /// <summary>
    /// 尝试重新可视化 OptiFabric 版本列表。
    /// </summary>
        private void OptiFabric_Loaded()
        {
            try
            {
                if (ModDownload.DlOptiFabricLoader.State != ModBase.LoadState.Finished)
                    return;
                if (SelectedMinecraftId is null || SelectedFabric is null || SelectedOptiFine is null)
                    return;
                // 获取版本列表
                var Versions = new List<ModComp.CompFile>();
                foreach (var Version in ModDownload.DlOptiFabricLoader.Output)
                {
                    if (IsSuitableOptiFabric(Version, SelectedMinecraftId))
                        Versions.Add(Version);
                }
                if (!Versions.Any())
                    return;
                // 排序
                Versions = Versions.OrderByDescending(v => v.ReleaseDate).ToList();
                // 可视化
                this.PanOptiFabric.Children.Clear();
                foreach (var Version in Versions)
                {
                    if (!IsSuitableOptiFabric(Version, SelectedMinecraftId))
                        continue;
                    this.PanOptiFabric.Children.Add(ModDownloadLib.OptiFabricDownloadListItem(Version, (_, __) => this.OptiFabric_Selected()));
                }
                // 自动选择 OptiFabric
                if (!AutoSelectedOptiFabric && !(SelectedMinecraftId.StartsWith("1.14") || SelectedMinecraftId.StartsWith("1.15"))) // 1.14~15 不自动选择
                {
                    AutoSelectedOptiFabric = true;
                    ModBase.Log($"[Download] 已自动选择 OptiFabric：{((MyListItem)this.PanOptiFabric.Children[0]).Title}");
                    OptiFabric_Selected((MyListItem)this.PanOptiFabric.Children[0], null);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化 OptiFabric 安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        }

        // 选择与清除
        private void OptiFabric_Selected(MyListItem sender, EventArgs e)
        {
            SelectedOptiFabric = (ModComp.CompFile)sender.Tag;
            this.CardOptiFabric.IsSwaped = true;
            SelectReload();
        }
        private void OptiFabric_Clear(object sender, MouseButtonEventArgs e)
        {
            SelectedOptiFabric = null;
            this.CardOptiFabric.IsSwaped = true;
            e.Handled = true;
            SelectReload();
        }

        #endregion

        #region 安装

        private void TextSelectName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && this.BtnStart.IsEnabled)
                BtnStart_Click();
        }
        private void BtnStart_Click()
        {
            // 确认版本隔离
            if ((SelectedForge is not null || SelectedNeoForge is not null || SelectedFabric is not null || SelectedQuilt is not null) && (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchArgumentIndieV2"), 0, false)) || Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("LaunchArgumentIndieV2"), 2, false))))
            {
                if (ModMain.MyMsgBox("你尚未开启版本隔离，这会导致多个版本共用同一个 Mod 文件夹。" + Constants.vbCrLf + "因此游戏可能会因为读取到与当前版本不符的 Mod 而崩溃。" + Constants.vbCrLf + "推荐在开始下载前，在 设置 → 启动选项 → 版本隔离 中开启版本隔离！", "版本隔离提示", "取消下载", "继续") == 1)
                {
                    return;
                }
            }
            // 提交安装申请
            string VersionName = this.TextSelectName.Text;
            var Request = new ModDownloadLib.McInstallRequest()
            {
                TargetVersionName = VersionName,
                TargetVersionFolder = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\",
                MinecraftJson = SelectedMinecraftJsonUrl,
                MinecraftName = SelectedMinecraftId,
                OptiFineEntry = SelectedOptiFine,
                ForgeEntry = SelectedForge,
                NeoForgeEntry = SelectedNeoForge,
                CleanroomEntry = SelectedCleanroom,
                FabricVersion = SelectedFabric,
                FabricApi = SelectedFabricApi,
                QuiltVersion = SelectedQuilt,
                QSL = SelectedQSL,
                OptiFabric = SelectedOptiFabric,
                LiteLoaderEntry = SelectedLiteLoader
            };
            if (!ModDownloadLib.McInstall(Request))
                return;
            // 返回，这样在再次进入安装页面时这个版本就会显示文件夹已重复
            ExitSelectPage();
        }

        #endregion

    }
}