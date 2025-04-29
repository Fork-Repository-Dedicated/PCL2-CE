using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadCompDetail
    {
        private MyCompItem CompItem = null;

        #region 加载器

        private ModLoader.LoaderTask<int, List<ModComp.CompFile>> CompFileLoader;

        public PageDownloadCompDetail()
        {
            CompFileLoader = new ModLoader.LoaderTask<int, List<ModComp.CompFile>>("Comp File", (Task) => Task.Output = ModComp.CompFilesGet(Project.Id, Project.FromCurseForge));
            this.Initialized += PageDownloadCompDetail_Inited;
            this.Loaded += PageDownloadCompDetail_Loaded;
            this.PageEnter += Init;
        }

        // 初始化加载器信息
        private void PageDownloadCompDetail_Inited(object sender, EventArgs e)
        {
            Project = (ModComp.CompProject)ModMain.FrmMain.PageCurrent.Additional(0);
            TargetVersion = Conversions.ToString(ModMain.FrmMain.PageCurrent.Additional(2));
            TargetLoader = (ModComp.CompLoaderType)Conversions.ToInteger(ModMain.FrmMain.PageCurrent.Additional(3));
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanMain, this.CardIntro, CompFileLoader, (_) => Load_OnFinish());
        }
        private void PageDownloadCompDetail_Loaded(object sender, EventArgs e)
        {
            // Initialized 只会执行一次
            Project = (ModComp.CompProject)ModMain.FrmMain.PageCurrent.Additional(0);
            TargetVersion = Conversions.ToString(ModMain.FrmMain.PageCurrent.Additional(2));
            TargetLoader = (ModComp.CompLoaderType)Conversions.ToInteger(ModMain.FrmMain.PageCurrent.Additional(3));
        }
        private ModComp.CompProject Project;
        private string TargetVersion;
        private ModComp.CompLoaderType TargetLoader;
        // 自动重试
        private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
        {
            switch (CompFileLoader.State)
            {
                case ModBase.LoadState.Failed:
                    {
                        string ErrorMessage = "";
                        if (CompFileLoader.Error is not null)
                            ErrorMessage = CompFileLoader.Error.Message;
                        if (ErrorMessage.Contains("不是有效的 Json 文件"))
                        {
                            ModBase.Log("[Comp] 下载的文件 Json 列表损坏，已自动重试", ModBase.LogLevel.Debug);
                            this.PageLoaderRestart();
                        }

                        break;
                    }
            }
        }
        // 结果 UI 化
        private class CardSorter : IComparer<string>
        {
            public string Topmost = "";
            public int Compare(string x, string y)
            {
                // 相同
                if ((x ?? "") == (y ?? ""))
                    return 0;
                // 置顶
                if ((x ?? "") == (Topmost ?? ""))
                    return -1;
                if ((y ?? "") == (Topmost ?? ""))
                    return 1;
                // 特殊版本
                bool IsXSpecial = x.EndsWithF("版本");
                bool IsYSpecial = y.EndsWithF("版本");
                if (IsXSpecial && IsYSpecial)
                    return x.CompareTo(y);
                if (IsXSpecial)
                    return 1;
                if (IsYSpecial)
                    return -1;
                // 比较版本号
                int VersionCodeSort = -ModMinecraft.VersionSortInteger(x.Replace(x.BeforeFirst(" ") + " ", ""), y.Replace(y.BeforeFirst(" ") + " ", ""));
                if (VersionCodeSort != 0)
                    return VersionCodeSort;
                // 比较全部
                return -ModMinecraft.VersionSortInteger(x, y);
            }
            public CardSorter(string Topmost = "")
            {
                this.Topmost = Topmost ?? "";
            }
        }

        private string VersionFilter;
        private bool IsMajorVersionFilter; // 是否按大版本号筛选（1.21 / 1.20 / 1.19 / ...）而非小版本号（1.21.1 / 1.21 / 1.20.4 / ...）
        private void Load_OnFinish()
        {
            // 初始化筛选器
            List<string> VersionFilters;

            // 按小版本号筛选？
            IsMajorVersionFilter = false;
            VersionFilters = CompFileLoader.Output.SelectMany(v => v.GameVersions).Select(v => GetGroupedVersionName(v, IsMajorVersionFilter, true)).Distinct().OrderByDescending(s => s, new ModMinecraft.VersionComparer()).ToList();
            // 按大版本号筛选？
            if (VersionFilters.Count >= 9)
            {
                IsMajorVersionFilter = true;
                VersionFilters = CompFileLoader.Output.SelectMany(v => v.GameVersions).Select(v => GetGroupedVersionName(v, IsMajorVersionFilter, true)).Distinct().OrderByDescending(s => s, new ModMinecraft.VersionComparer()).ToList();
            }

            // UI 化筛选器
            this.PanFilter.Children.Clear();
            if (VersionFilters.Count < 2)
            {
                this.CardFilter.Visibility = Visibility.Collapsed;
                VersionFilter = null;
            }
            else
            {
                this.CardFilter.Visibility = Visibility.Visible;
                VersionFilters.Insert(0, "全部");
                // 转化为按钮
                foreach (string Version in VersionFilters)
                {
                    var NewButton = new MyRadioButton() { Text = Version, Margin = new Thickness(2d, 0d, 2d, 0d), ColorType = MyRadioButton.ColorState.Highlight };
                    NewButton.LabText.Margin = new Thickness(-2, 0d, 8d, 0d);
                    NewButton.Check += (sender, raiseByMouse) =>
        {
            VersionFilter = sender.Text == "全部" ? null : sender.Text;
            UpdateFilterResult();
        };
                    this.PanFilter.Children.Add(NewButton);
                }
                // 自动选择
                MyRadioButton ToCheck = null;
                if (!string.IsNullOrEmpty(TargetVersion))
                {
                    var TargetFile = CompFileLoader.Output.FirstOrDefault(v => v.GameVersions.Contains(TargetVersion));
                    if (TargetFile is not null)
                    {
                        string TargetGroup = GetGroupedVersionName(TargetVersion, IsMajorVersionFilter, true);
                        foreach (MyRadioButton Button in this.PanFilter.Children)
                        {
                            if ((Button.Text ?? "") != (TargetGroup ?? ""))
                                continue;
                            ToCheck = Button;
                            break;
                        }
                    }
                }
                if (ToCheck is null)
                    ToCheck = (MyRadioButton)this.PanFilter.Children[0];
                ToCheck.Checked = true;
            }

            // 更新筛选结果（文件列表 UI 化）
            UpdateFilterResult();
        }
        private void UpdateFilterResult()
        {
            string TargetCardName = !string.IsNullOrEmpty(TargetVersion) || TargetLoader != ModComp.CompLoaderType.Any ? $"所选版本：{(TargetLoader != ModComp.CompLoaderType.Any ? TargetLoader.ToString() + " " : "")}{TargetVersion}" : "";
            // 归类到卡片下
            var Dict = new SortedDictionary<string, List<ModComp.CompFile>>(new CardSorter(TargetCardName));
            Dict.Add("其他版本", new List<ModComp.CompFile>());
            var SupportedLoaders = new List<int>((IEnumerable<int>)Enum.GetValues(typeof(ModComp.CompLoaderType)));
            foreach (ModComp.CompFile Version in CompFileLoader.Output)
            {
                foreach (var GameVersion in Version.GameVersions)
                {
                    // 检查是否符合版本筛选器
                    if (VersionFilter is not null && (GetGroupedVersionName(GameVersion, IsMajorVersionFilter, true) ?? "") != (VersionFilter ?? ""))
                        continue;
                    // 决定添加到哪个卡片
                    string Ver = GetGroupedVersionName(GameVersion, false, false);
                    // 遍历加入的加载器列表
                    var Loaders = new List<string>();
                    if (Project.ModLoaders.Count > 1 && Project.Type == ModComp.CompType.Mod && Ver.StartsWith("1.")) // 至少有两个加载器
                                                                                                                      // 是 Mod
                                                                                                                      // 不是 “快照版本” 之类的
                    {
                        foreach (var Loader in Version.ModLoaders)
                        {
                            if (Conversions.ToBoolean(Loader == ModComp.CompLoaderType.Quilt && (bool)ModBase.Setup.Get("ToolDownloadIgnoreQuilt")))
                                continue;
                            if (SupportedLoaders.Contains((int)Loader))
                                Loaders.Add(Loader.ToString() + " ");
                        }
                    }
                    if (!Loaders.Any())
                        Loaders.Add(""); // 保底加一个空的，确保它在一张卡片里
                                         // 实际添加
                    foreach (var Loader in Loaders)
                    {
                        string TargetCard = Loader + Ver;
                        if (!Dict.ContainsKey(TargetCard))
                            Dict.Add(TargetCard, new List<ModComp.CompFile>());
                        if (!Dict[TargetCard].Contains(Version))
                            Dict[TargetCard].Add(Version);
                    }
                }
            }
            // 添加筛选的版本的卡片
            if (!string.IsNullOrEmpty(TargetCardName))
            {
                Dict.Add(TargetCardName, new List<ModComp.CompFile>());
                foreach (ModComp.CompFile Version in CompFileLoader.Output)
                {
                    if (Version.GameVersions.Contains(TargetVersion) && (TargetLoader == ModComp.CompLoaderType.Any || Version.ModLoaders.Contains(TargetLoader)))
                    {
                        // 检查是否符合版本筛选器
                        if (VersionFilter is not null && !Version.GameVersions.Any(v => (GetGroupedVersionName(v, IsMajorVersionFilter, true) ?? "") == (VersionFilter ?? "")))
                            continue;
                        if (!Dict[TargetCardName].Contains(Version))
                            Dict[TargetCardName].Add(Version);
                    }
                }
            }
            // 转化为 UI
            try
            {
                this.PanResults.Children.Clear();
                foreach (KeyValuePair<string, List<ModComp.CompFile>> Pair in Dict)
                {
                    if (!Pair.Value.Any())
                        continue;
                    // 增加卡片
                    var NewCard = new MyCard() { Title = Pair.Key, Margin = new Thickness(0d, 0d, 0d, 15d) }; // FUTURE: Res
                    var NewStack = new StackPanel() { Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d), Tag = Pair.Value };
                    NewCard.Children.Add(NewStack);
                    NewCard.InstallMethod = new Action<StackPanel>((Stack) =>
                        {
                            Stack.Tag = ModBase.Sort((List<ModComp.CompFile>)Stack.Tag, (a, b) => a.ReleaseDate > b.ReleaseDate);
                            if (Project.Type == ModComp.CompType.ModPack)
                            {
                                bool BadDisplayName = ((List<ModComp.CompFile>)Stack.Tag).Distinct((a, b) => (a.DisplayName ?? "") == (b.DisplayName ?? "")).Count != ((List<ModComp.CompFile>)Stack.Tag).Count;
                                foreach (var item in (IEnumerable)Stack.Tag)
                                    Stack.Children.Add(((ModComp.CompFile)item).ToListItem((_, __) => ModMain.FrmDownloadCompDetail.Install_Click(), ModMain.FrmDownloadCompDetail.Save_Click, BadDisplayName: BadDisplayName));
                            }
                            else
                            {
                                ModComp.CompFilesCardPreload(Stack, (List<ModComp.CompFile>)Stack.Tag);
                                bool BadDisplayName = ((List<ModComp.CompFile>)Stack.Tag).Distinct((a, b) => (a.DisplayName ?? "") == (b.DisplayName ?? "")).Count != ((List<ModComp.CompFile>)Stack.Tag).Count;
                                foreach (var item in (IEnumerable)Stack.Tag)
                                    Stack.Children.Add(((ModComp.CompFile)item).ToListItem(ModMain.FrmDownloadCompDetail.Save_Click, BadDisplayName: BadDisplayName));
                            }
                        });
                    NewCard.SwapControl = NewStack;
                    this.PanResults.Children.Add(NewCard);
                    // 确定卡片是否展开
                    if ((Pair.Key ?? "") == (TargetCardName ?? "") || ModMain.FrmMain.PageCurrent.Additional is not null && ((List<string>)ModMain.FrmMain.PageCurrent.Additional(1)).Contains(NewCard.Title)) // #2761
                    {
                        NewCard.StackInstall();
                    }
                    else
                    {
                        NewCard.IsSwaped = true;
                    }
                    // 增加提示
                    if (Pair.Key == "其他版本")
                    {
                        NewStack.Children.Add(new MyHint() { Text = "由于版本信息更新缓慢，可能无法识别刚更新的 MC 版本，只需等待几天即可自动恢复正常。", IsWarn = false, Margin = new Thickness(0d, 0d, 0d, 7d) });
                    }
                }
                // 如果只有一张卡片，展开第一张卡片
                if (this.PanResults.Children.Count == 1)
                {
                    ((MyCard)this.PanResults.Children[0]).IsSwaped = false;
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化工程下载列表出错", ModBase.LogLevel.Feedback);
            }
        }
        private string GetGroupedVersionName(string Name, bool MajorOnly, bool FoldOldRelease)
        {
            if (Name is null)
            {
                return "其他版本";
            }
            else if (Name.Contains("w"))
            {
                return "快照版本";
            }
            else if (Name.StartsWith("1.0") || !Name.StartsWith("1.") || FoldOldRelease && ModBase.Val(Name.Split(".")[1]) < 10d)
            {
                return "远古版本";
            }
            else
            {
                return MajorOnly ? "1." + Name.Split(".")[1].BeforeFirst(" ") : Name;
            }
        }

        #endregion
        private bool IsFirstInit = true;
        public void Init()
        {
            ModAnimation.AniControlEnabled += 1;
            Project = (ModComp.CompProject)ModMain.FrmMain.PageCurrent.Additional(0);
            this.PanBack.ScrollToHome();
            // 重启加载器
            if (IsFirstInit)
            {
                // 在 Me.Initialized 已经初始化了加载器，不再重复初始化
                IsFirstInit = false;
            }
            else
            {
                this.PageLoaderRestart(IsForceRestart: true);
            }
            // 放置当前工程
            if (CompItem is not null)
                this.PanIntro.Children.Remove(CompItem);
            CompItem = Project.ToCompItem(true, true);
            CompItem.CanInteraction = false;
            CompItem.Margin = new Thickness(-7, -7, 0d, 8d);
            this.PanIntro.Children.Insert(0, CompItem);

            // 决定按钮显示
            this.BtnIntroWeb.Text = Project.FromCurseForge ? "转到 CurseForge" : "转到 Modrinth";
            this.BtnIntroWiki.Visibility = Project.WikiId == 0 ? Visibility.Collapsed : Visibility.Visible;

            ModAnimation.AniControlEnabled -= 1;
        }

        // 整合包下载（安装）
        public void Install_Click(MyListItem sender, EventArgs e)
        {
            try
            {

                // 获取基本信息
                ModComp.CompFile File = (ModComp.CompFile)sender.Tag;
                string LoaderName = $"{(Project.FromCurseForge ? "CurseForge" : "Modrinth")} 整合包下载：{Project.TranslatedName} ";

                // 获取版本名
                string PackName = Project.TranslatedName.Replace(".zip", "").Replace(".rar", "").Replace(".mrpack", "").Replace(@"\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
                var Validate = new ValidateFolderName(ModMinecraft.PathMcFolder + "versions");
                if (!string.IsNullOrEmpty(Validate.Validate(PackName)))
                    PackName = "";
                string VersionName = ModMain.MyMsgBoxInput("输入版本名称", "", PackName, new System.Collections.ObjectModel.Collection<ValidateType>() { Validate });
                if (string.IsNullOrEmpty(VersionName))
                    return;

                // 构造步骤加载器
                var Loaders = new List<ModLoader.LoaderBase>();
                string Target = $@"{ModMinecraft.PathMcFolder}versions\{VersionName}\原始整合包.{(Project.FromCurseForge ? "zip" : "mrpack")}";
                string LogoFileAddress = MyImage.GetTempPath(CompItem.Logo);
                Loaders.Add(new ModNet.LoaderDownload("下载整合包文件", new List<ModNet.NetFile>() { File.ToNetFile(Target) }) { ProgressWeight = 10d, Block = true });
                Loaders.Add(new ModLoader.LoaderTask<int, int>("准备安装整合包", () => ModModpack.ModpackInstall(Target, VersionName, System.IO.File.Exists(LogoFileAddress) ? LogoFileAddress : null)) { ProgressWeight = 0.1d });

                // 启动
                var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders)
                {
                    OnStateChanged = new Action<ModLoader.LoaderBase>(MyLoader =>
                            {
                                switch (MyLoader.State)
                                {
                                    case ModBase.LoadState.Failed:
                                        {
                                            ModMain.Hint(MyLoader.Name + "失败：" + ModBase.GetExceptionSummary(MyLoader.Error), ModMain.HintType.Critical);
                                            break;
                                        }
                                    case ModBase.LoadState.Aborted:
                                        {
                                            ModMain.Hint(MyLoader.Name + "已取消！", ModMain.HintType.Info);
                                            break;
                                        }
                                    case ModBase.LoadState.Loading:
                                        {
                                            return; // 不重新加载版本列表
                                        }
                                }
                                ModDownloadLib.McInstallFailedClearFolder(MyLoader);
                            })
                };
                Loader.Start(ModMinecraft.PathMcFolder + @"versions\" + VersionName + @"\");
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "下载资源整合包失败", ModBase.LogLevel.Feedback);
            }
        }
        // Mod、资源包下载；整合包另存为
        public static string CachedFolder = null; // 仅在本次缓存的下载文件夹
        public void Save_Click(object sender, EventArgs e)
        {
            ModComp.CompFile File = (ModComp.CompFile)(sender is MyListItem ? sender : ((dynamic)sender).Parent).Tag;
            ModBase.RunInNewThread(() =>
        {
            try
            {
                string Desc = "资源";
                switch (Project.Type)
                {
                    case ModComp.CompType.ModPack:
                        {
                            Desc = "整合包";
                            break;
                        }
                    case ModComp.CompType.Mod:
                        {
                            Desc = "Mod ";
                            break;
                        }
                    case ModComp.CompType.ResourcePack:
                        {
                            Desc = "资源包";
                            break;
                        }
                    case ModComp.CompType.Shader:
                        {
                            Desc = "光影包";
                            break;
                        }
                }
                // 确认默认保存位置
                string DefaultFolder = null;
                string ResourceName = "";
                switch (Project.Type)
                {
                    case ModComp.CompType.Mod:
                        {
                            ResourceName = "mods";
                            break;
                        }
                    case ModComp.CompType.ResourcePack:
                        {
                            ResourceName = "resourcepacks";
                            break;
                        }
                    case ModComp.CompType.Shader:
                        {
                            ResourceName = "shaderpacks";
                            break;
                        }
                }
                var IsVersionSuitable = default(Func<ModMinecraft.McVersion, bool>);
                if (Project.Type == ModComp.CompType.Mod)
                {
                    // 获取 Mod 所需的加载器种类
                    bool? AllowForge = default;
                    bool? AllowFabric = default;
                    if (File.ModLoaders.Any()) // 从文件中获取
                    {
                        AllowForge = File.ModLoaders.Contains(ModComp.CompLoaderType.Forge) || File.ModLoaders.Contains(ModComp.CompLoaderType.NeoForge);
                        AllowFabric = File.ModLoaders.Contains(ModComp.CompLoaderType.Fabric);
                    }
                    else if (Project.ModLoaders.Any()) // 从工程中获取
                    {
                        AllowForge = Project.ModLoaders.Contains(ModComp.CompLoaderType.Forge) || File.ModLoaders.Contains(ModComp.CompLoaderType.NeoForge);
                        AllowFabric = Project.ModLoaders.Contains(ModComp.CompLoaderType.Fabric);
                    }
                    if ((((AllowForge is not null ? !AllowForge : false) is var arg2 && arg2.HasValue && !arg2.Value ? false : AllowFabric is not null ? arg2 : false) is var arg3 && arg3.HasValue && !arg3.Value ? false : !(!AllowFabric is { } arg4) ? null : arg4 ? arg3 : false) == true)
                    {
                        AllowForge = default(bool?);
                        AllowFabric = default(bool?);
                    }
                    ModBase.Log(Conversions.ToString(Operators.ConcatenateObject(Operators.ConcatenateObject(Operators.ConcatenateObject("[Comp] 允许 Forge：", AllowForge), "，允许 Fabric："), AllowFabric)));
                    // 判断某个版本是否符合 Mod 要求
                    IsVersionSuitable = new Func<ModMinecraft.McVersion, bool>(Version =>
        {
                        if (!Version.IsLoaded)
                            Version.Load();
                        if (!Version.Modable)
                            return false;
                        if (File.GameVersions.Any(v => v.Contains(".")) && !File.GameVersions.Any(v => v.Contains(".") && (v ?? "") == (Version.Version.McName ?? "")))
                            return false;
                        if (AllowForge is null || AllowFabric is null)
                            return true;
                        if ((!AllowForge.HasValue || AllowForge.Value) && (Version.Version.HasForge || Version.Version.HasNeoForge) && AllowForge.HasValue)
                            return true;
                        if ((!AllowFabric.HasValue || AllowFabric.Value) && Version.Version.HasFabric && AllowFabric.HasValue)
                            return true;
                        return false;
                    });
                }
                else if (new[] { ModComp.CompType.ResourcePack, ModComp.CompType.Shader }.Contains(Project.Type))
                {
                    // 判断某个版本是否符合资源包和光影要求
                    IsVersionSuitable = new Func<ModMinecraft.McVersion, bool>(Version =>
        {
                        if (!Version.IsLoaded)
                            Version.Load();
                        if (File.GameVersions.Any(v => v.Contains(".") && (v ?? "") == (Version.Version.McName ?? "")))
                            return true;
                        return false;
                    });
                }
                if (!string.IsNullOrWhiteSpace(ResourceName) && IsVersionSuitable is not null)
                {
                    // 获取常规资源默认下载位置
                    if (CachedFolder is not null)
                    {
                        DefaultFolder = CachedFolder;
                        ModBase.Log("[Comp] 使用上次下载时的文件夹作为默认下载位置");
                    }
                    else if (ModMinecraft.McVersionCurrent is not null && IsVersionSuitable(ModMinecraft.McVersionCurrent))
                    {
                        DefaultFolder = ModMinecraft.McVersionCurrent.PathIndie + $@"{ResourceName}\";
                        Directory.CreateDirectory(DefaultFolder);
                        ModBase.Log($"[Comp] 使用当前版本的 {ResourceName} 文件夹作为默认下载位置（{ModMinecraft.McVersionCurrent.Name}）");
                    }
                    else
                    {
                        bool NeedLoad = ModMinecraft.McVersionListLoader.State != ModBase.LoadState.Finished;
                        if (NeedLoad)
                        {
                            ModMain.Hint("正在查找适合的游戏版本……");
                            ModLoader.LoaderFolderRun(ModMinecraft.McVersionListLoader, ModMinecraft.PathMcFolder, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\", WaitForExit: true);
                        }
                        var SuitableVersions = new List<ModMinecraft.McVersion>();
                        foreach (ModMinecraft.McVersion Version in ModMinecraft.McVersionList.Values.SelectMany(l => l))
                        {
                            if (IsVersionSuitable(Version))
                                SuitableVersions.Add(Version);
                        }
                        if (!SuitableVersions.Any())
                        {
                            DefaultFolder = ModMinecraft.PathMcFolder;
                            if (NeedLoad)
                            {
                                ModMain.Hint("当前 MC 文件夹中没有找到适合此资源文件的版本！");
                            }
                            else
                            {
                                ModBase.Log("[Comp] 由于当前版本不兼容，使用当前的 MC 文件夹作为默认下载位置");
                            }
                        }
                        else // 选择资源数量最多的版本
                        {
                            var SelectedVersion = SuitableVersions.OrderBy(v =>
        {
                                var Info = new DirectoryInfo(v.PathIndie + $@"{ResourceName}\");
                                return Info.Exists ? Info.GetFiles().Length : -1;
                            }).LastOrDefault();
                            DefaultFolder = SelectedVersion.PathIndie + $@"{ResourceName}\";
                            Directory.CreateDirectory(DefaultFolder);
                            ModBase.Log("[Comp] 使用适合的游戏版本作为默认下载位置（" + SelectedVersion.Name + "）");
                        }
                    }
                }
                // 获取基本信息
                string FileName;
                if ((Project.TranslatedName ?? "") == (Project.RawName ?? ""))
                {
                    FileName = File.FileName;
                }
                else
                {
                    string ChineseName = Project.TranslatedName.BeforeFirst(" (").BeforeFirst(" - ").Replace(@"\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
                    switch (ModBase.Setup.Get("ToolDownloadTranslateV2"))
                    {
                        case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                            {
                                FileName = $"【{ChineseName}】{File.FileName}";
                                break;
                            }
                        case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                            {
                                FileName = $"[{ChineseName}] {File.FileName}";
                                break;
                            }
                        case var case2 when Operators.ConditionalCompareObjectEqual(case2, 2, false):
                            {
                                FileName = $"{ChineseName}-{File.FileName}";
                                break;
                            }
                        case var case3 when Operators.ConditionalCompareObjectEqual(case3, 3, false):
                            {
                                FileName = $"{File.FileName}-{ChineseName}";
                                break;
                            }

                        default:
                            {
                                FileName = File.FileName;
                                break;
                            }
                    }
                }
                ModBase.RunInUi(() =>
        {
            // 弹窗要求选择保存位置
                    string Target;
                    Target = ModBase.SelectSaveFile("选择保存位置", FileName, Desc + "文件|" + (Project.Type == ModComp.CompType.Mod ? File.FileName.EndsWith(".litemod") ? "*.litemod" : "*.jar" : File.FileName.EndsWith(".mrpack") ? "*.mrpack" : "*.zip"), DefaultFolder);
                    if (!Target.Contains(@"\"))
                        return;
            // 构造步骤加载器
                    string LoaderName = Desc + "下载：" + ModBase.GetFileNameWithoutExtentionFromPath(Target) + " ";
                    if ((Target ?? "") != (DefaultFolder ?? "") && Project.Type == ModComp.CompType.Mod)
                        CachedFolder = ModBase.GetPathFromFullPath(Target);
                    var Loaders = new List<ModLoader.LoaderBase>();
                    Loaders.Add(new ModNet.LoaderDownload("下载文件", new List<ModNet.NetFile>() { File.ToNetFile(Target) }) { ProgressWeight = 6d, Block = true });
            // 启动
                    var Loader = new ModLoader.LoaderCombo<int>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
                    Loader.Start(1);
                    ModLoader.LoaderTaskbarAdd(Loader);
                    ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                    ModMain.FrmMain.BtnExtraDownload.Ribble();
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "保存资源文件失败", ModBase.LogLevel.Feedback);
            }
        }, "Download CompDetail Save");
        }

        private void BtnIntroWeb_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite(Project.Website);
        }
        private void BtnIntroWiki_Click(object sender, EventArgs e)
        {
            ModBase.OpenWebsite("https://www.mcmod.cn/class/" + Project.WikiId + ".html");
        }
        private void BtnIntroCopy_Click(object sender, EventArgs e)
        {
            ModBase.ClipboardSet(CompItem.LabTitle.Text + CompItem.LabTitleRaw.Text);
        }
        private void BtnFavorites_Click(object sender, EventArgs e)
        {
            ModComp.CompFavorites.ShowMenu(Project, (UIElement)sender);
        }
        private void BtnIntroLinkCopy_Click(object sender, EventArgs e)
        {
            ModComp.CompClipboard.CurrentText = Project.Website;
            ModBase.ClipboardSet(Project.Website);
        }
        // 翻译简介
        private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            ModMain.Hint($"正在获取 {Project.TranslatedName} 的简介译文……");
            string ChineseDescription = await Project.ChineseDescription;
            if (ChineseDescription is null)
                return;
            ModMain.MyMsgBox($"原文：{Project.Description}{Environment.NewLine}译文：{ChineseDescription}");
        }
    }
}