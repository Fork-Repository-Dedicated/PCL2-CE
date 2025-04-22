using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageDownloadCompFavorites
    {

        #region 加载器信息
        // 加载器信息
        public ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>> Loader;

        public PageDownloadCompFavorites()
        {
            Loader = new ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>>("CompProject Favorites", CompFavoritesGet, LoaderInput);
            this.Initialized += PageDownloadCompFavorites_Inited;
            this.Loaded += PageDownloadCompFavorites_Loaded;
            this.KeyDown += Page_KeyDown;
        }

        private void PageDownloadCompFavorites_Inited(object sender, EventArgs e)
        {
            RefreshFavTargets();
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanContent, (FrameworkElement)null, Loader, (_) => Load_OnFinish(), this.LoaderInput);
        }
        private void PageDownloadCompFavorites_Loaded(object sender, EventArgs e)
        {
            Items_SetSelectAll(false);
            RefreshBar();
            if (Loader.Input is not null && !Loader.Input.Count.Equals(CurrentFavTarget.Favs.Count))
            {
                RefreshFavTargets();
            }
        }

        private List<string> LoaderInput()
        {
            var TargetList = default(List<string>);
            try
            {
                TargetList = CurrentFavTarget.Favs.Distinct().ToList();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Favorites] 加载收藏夹列表时出错");
            }
            return (List<string>)TargetList.Clone(); // 复制而不是直接引用！
        }
        private void CompFavoritesGet(ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>> Task)
        {
            Task.Output = ModComp.CompRequest.GetCompProjectsByIds(Task.Input);
        }
        #endregion

        private List<MyListItem> CompItemList = new List<MyListItem>();
        private List<MyListItem> SelectedItemList = new List<MyListItem>();
        private ModComp.CompFavorites.FavData CurrentFavTarget
        {
            get
            {
                MyComboBoxItem SelectedItem = (MyComboBoxItem)this.ComboTargetFav.SelectedItem;
                if (SelectedItem is null)
                {
                    ModBase.Log("[Favorites] 异常：未选择收藏夹");
                    SelectedItem = (MyComboBoxItem)this.ComboTargetFav.Items.GetItemAt(0);
                }
                return ModComp.CompFavorites.FavoritesList.Where(e => Operators.ConditionalCompareObjectEqual(e.Id, SelectedItem.Tag, false)).First();
            }
        }

        #region UI 化 - 自适应卡片
        public class CompListItemContainer // 用来存储自动依据类型生成的卡片及其相关信息
        {
            public MyCard Card { get; set; }
            public StackPanel ContentList { get; set; }
            public string Title { get; set; }
            public int CompType { get; set; }
        }

        private List<CompListItemContainer> ItemList = new List<CompListItemContainer>();

        /// <summary>
    /// 刷新收藏夹列表
    /// </summary>
        private void RefreshFavTargets()
        {
            this.ComboTargetFav.Items.Clear();
            foreach (var Target in ModComp.CompFavorites.FavoritesList)
            {
                var Item = new MyComboBoxItem()
                {
                    Content = Target.Name,
                    Tag = Target.Id
                };
                this.ComboTargetFav.Items.Add(Item);
            }
            if (this.ComboTargetFav.SelectedIndex == -1)
            {
                this.ComboTargetFav.SelectedIndex = 0; // 默认选择第一个
            }
        }

        /// <summary>
    /// 返回适合当前工程项目的卡片记录
    /// </summary>
    /// <param name="Type">工程项目类型</param>
    /// <returns></returns>
        private CompListItemContainer GetSuitListContainer(int Type)
        {
            if (ItemList.Any(e => e.CompType.Equals(Type)))
            {
                return ItemList.First(e => e.CompType.Equals(Type));
            }
            else
            {
                var NewItem = new CompListItemContainer()
                {
                    Card = new MyCard()
                    {
                        CanSwap = true,
                        Margin = new Thickness(0d, 0d, 0d, 15d)
                    },
                    ContentList = new StackPanel()
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(12d, 38d, 12d, 12d)
                    },
                    CompType = Type
                };
                switch (Type)
                {
                    case -1:
                        {
                            NewItem.Title = "搜索结果 ({0})"; // 搜索结果
                            break;
                        }
                    case (int)ModComp.CompType.Mod:
                        {
                            NewItem.Title = "Mod ({0})";
                            break;
                        }
                    case (int)ModComp.CompType.ModPack:
                        {
                            NewItem.Title = "整合包 ({0})";
                            break;
                        }
                    case (int)ModComp.CompType.ResourcePack:
                        {
                            NewItem.Title = "资源包 ({0})";
                            break;
                        }
                    case (int)ModComp.CompType.Shader:
                        {
                            NewItem.Title = "光影包 ({0})";
                            break;
                        }

                    default:
                        {
                            NewItem.Title = "未分类类型 ({0})";
                            break;
                        }
                }
                NewItem.Card.Title = string.Format(NewItem.Title, 0);
                NewItem.Card.Children.Add(NewItem.ContentList);
                ItemList.Add(NewItem);
                return NewItem;
            }
        }

        private void RefreshContent()
        {
            foreach (var item in ItemList) // 清除逻辑父子关系
                item.ContentList.Children.Clear();
            this.PanContentList.Children.Clear();
            var DataSource = IsSearching ? SearchResult : CompItemList;
            foreach (MyListItem item in DataSource)
                GetSuitListContainer(IsSearching ? -1 : (int)((ModComp.CompProject)item.Tag).Type).ContentList.Children.Add(item);
            foreach (var item in ItemList)
            {
                if (item.ContentList.Children.Count == 0)
                    continue;
                this.PanContentList.Children.Add(item.Card);
            }
        }

        private void RefreshCardTitle()
        {
            foreach (var item in ItemList)
                item.Card.Title = string.Format(item.Title, CompItemList.Where(e => (int)((ModComp.CompProject)e.Tag).Type == item.CompType).Count());
            if (!ItemList.Any(e => e.CompType.Equals(-1)))
                return;
            var SearchItem = ItemList.First(e => e.CompType.Equals(-1));
            if (SearchItem is not null)
            {
                SearchItem.Card.Title = string.Format(SearchItem.Title, SearchResult.Count);
            }
        }

        #endregion

        #region UI 化 - 加载主逻辑

        // 结果 UI 化
        private void Load_OnFinish()
        {
            ItemList.Clear();
            try
            {
                AllowSearch = false;
                this.PanSearchBox.Text = string.Empty;
                AllowSearch = true;
                CompItemList.Clear();
                bool SomeGetFail = Loader.Input.Count != Loader.Output.Count;
                this.HintGetFail.Visibility = SomeGetFail ? Visibility.Visible : Visibility.Collapsed;
                foreach (var item in Loader.Output)
                {
                    var CompItem = item.ToListItem();
                    ListItemBuild(CompItem);
                    CompItemList.Add(CompItem);
                }
                if (CompItemList.Any()) // 有收藏
                {
                    if (!IsSearching)
                    {
                        this.PanSearchBox.Visibility = Visibility.Visible;
                        this.PanContentList.Visibility = Visibility.Visible;
                        this.CardNoContent.Visibility = Visibility.Collapsed;
                    }
                }
                else // 没有收藏
                {
                    this.PanSearchBox.Visibility = Visibility.Collapsed;
                    this.PanContentList.Visibility = Visibility.Collapsed;
                    this.CardNoContent.Visibility = Visibility.Visible;
                }

                // If SomeGetFail Then
                // Dim FailList As New List(Of MyListItem)
                // Dim FailIds = Loader.Input.Except(Loader.Output.Select(Function(e) e.Id))
                // For Each Id In FailIds
                // Dim FailItem As New MyListItem
                // FailItem.Title = $"{Id}"
                // FailItem.Info = "此资源获取失败，可能在线资源被删除或者未获取成功"
                // FailItem.Tag = Id

                // ListItemBuild(FailItem)

                // FailList.Add(FailItem)
                // Next
                // CompItemList.AddRange(FailList)
                // End If

                RefreshContent();
                RefreshCardTitle();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化收藏夹列表出错", ModBase.LogLevel.Feedback);
            }
        }

        private void ListItemBuild(MyListItem CompItem)
        {
            CompItem.Type = MyListItem.CheckType.CheckBox;
            string CompId = ((ModComp.CompProject)CompItem.Tag).Id;
            // ----备注----
            string Notes = "";
            CurrentFavTarget.Notes.TryGetValue(CompId, out Notes);
            var NoteItem = new Run() { Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148)) };
            if (!string.IsNullOrWhiteSpace(Notes))
            {
                NoteItem.Text = $" ({Notes})";
            }
            CompItem.LabTitle.Inlines.Add(NoteItem);
            // ----添加按钮----
            // 修改备注按钮
            var Btn_EditNote = new MyIconButton();
            Btn_EditNote.Logo = ModBase.Logo.IconButtonEdit;
            Btn_EditNote.ToolTip = "修改备注";
            ToolTipService.SetPlacement(Btn_EditNote, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(Btn_EditNote, 30d);
            ToolTipService.SetHorizontalOffset(Btn_EditNote, 2d);
            Btn_EditNote.Click += (sender, e) =>
                {
                    CurrentFavTarget.Notes.TryGetValue(CompId, out Notes);
                    string DesiredNote = ModMain.MyMsgBoxInput("修改备注", DefaultInput: Notes);
                    CurrentFavTarget.Notes[CompId] = DesiredNote;
                    NoteItem.Text = string.IsNullOrWhiteSpace(DesiredNote) ? "" : $" ({DesiredNote})";
                    ModComp.CompFavorites.Save();
                };
            // 删除按钮
            var Btn_Delete = new MyIconButton();
            Btn_Delete.Logo = ModBase.Logo.IconButtonLikeFill;
            Btn_Delete.ToolTip = "取消收藏";
            ToolTipService.SetPlacement(Btn_Delete, System.Windows.Controls.Primitives.PlacementMode.Center);
            ToolTipService.SetVerticalOffset(Btn_Delete, 30d);
            ToolTipService.SetHorizontalOffset(Btn_Delete, 2d);
            Btn_Delete.Click += (sender, e) =>
                {
                    Items_CancelFavorites(CompItem);
                    RefreshContent();
                    RefreshCardTitle();
                    RefreshBar();
                };
            CompItem.Buttons = new[] { Btn_EditNote, Btn_Delete };
            // ---操作逻辑---
            // 右键查看详细信息界面
            if (CompItem.Tag is ModComp.CompProject)
            {
                CompItem.MouseRightButtonUp += (object sender, global::System.EventArgs e) => global::PCL.ModMain.FrmMain.PageChange(new global::PCL.FormMain.PageStackData()
                {
                    Page = global::PCL.FormMain.PageType.CompDetail,
                    Additional = new[] { CompItem.Tag, new global::System.Collections.Generic.List<string>(), string.Empty, global::PCL.ModComp.CompLoaderType.Any }
                });
            }
            // ---其它事件---
            CompItem.Changed += ItemCheckStatusChanged;
        }

        #endregion

        #region UI 化 - 选择操作

        private int BottomBarShownCount = 0;

        private void RefreshBar()
        {
            int NewCount = SelectedItemList.Count;
            bool Selected = NewCount > 0;
            if (Selected)
                this.LabSelect.Text = $"已选择 {NewCount} 个收藏项目"; // 取消所有选择时不更新数字
                                                               // 更新显示状态
            if (ModAnimation.AniControlEnabled == 0)
            {
                this.PanContentList.Margin = new Thickness(0d, 0d, 0d, Selected ? 80 : 0);
                if (Selected)
                {
                    // 仅在数量增加时播放出现/跳跃动画
                    if (BottomBarShownCount >= NewCount)
                    {
                        BottomBarShownCount = NewCount;
                        return;
                    }
                    else
                    {
                        BottomBarShownCount = NewCount;
                    }
                    // 出现/跳跃动画
                    this.CardSelect.Visibility = Visibility.Visible;
                    ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.CardSelect, 1d - this.CardSelect.Opacity, 60), ModAnimation.AaTranslateY(this.CardSelect, (double)-27 - this.TransSelect.Y, 120, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaTranslateY(this.CardSelect, 3d, 150, 120, Ease: new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaTranslateY(this.CardSelect, (double)-1, 90, 270, Ease: new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)) }, "CompFavorites Sidebar");
                }
                else
                {
                    // 不重复播放隐藏动画
                    if (BottomBarShownCount == 0)
                        return;
                    BottomBarShownCount = 0;
                    // 隐藏动画
                    ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.CardSelect, -this.CardSelect.Opacity, 90), ModAnimation.AaTranslateY(this.CardSelect, (double)-10 - this.TransSelect.Y, 90, Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => this.CardSelect.Visibility = Visibility.Collapsed, After: true) }, "CompFavorites Sidebar");
                }
            }
            else
            {
                ModAnimation.AniStop("CompFavorites Sidebar");
                BottomBarShownCount = NewCount;
                if (Selected)
                {
                    this.CardSelect.Visibility = Visibility.Visible;
                    this.CardSelect.Opacity = 1d;
                    this.TransSelect.Y = (double)-25;
                }
                else
                {
                    this.CardSelect.Visibility = Visibility.Collapsed;
                    this.CardSelect.Opacity = 0d;
                    this.TransSelect.Y = (double)-10;
                }
            }
        }

        #endregion

        #region 事件
        // 选中状态改变
        private void ItemCheckStatusChanged(object sender, ModBase.RouteEventArgs e)
        {
            MyListItem SenderItem = (MyListItem)sender;
            if (SelectedItemList.Contains(SenderItem))
                SelectedItemList.Remove(SenderItem);
            if (SenderItem.Checked)
                SelectedItemList.Add(SenderItem);
            RefreshBar();
        }

        // 自动重试
        private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
        {
            switch (Loader.State)
            {
                case ModBase.LoadState.Failed:
                    {
                        string ErrorMessage = "";
                        if (Loader.Error is not null)
                            ErrorMessage = Loader.Error.Message;
                        if (ErrorMessage.Contains("不是有效的 json 文件"))
                        {
                            ModBase.Log("[Download] 下载的工程列表 JSON 文件损坏，已自动重试", ModBase.LogLevel.Debug);
                            this.PageLoaderRestart();
                        }

                        break;
                    }
            }
        }

        private void Btn_FavoritesCancel_Clicked(object sender, ModBase.RouteEventArgs e)
        {
            foreach (var Items in SelectedItemList.Clone())
                Items_CancelFavorites(Items);
            if (CompItemList.Any())
            {
                RefreshContent();
                RefreshCardTitle();
            }
            else
            {
                Loader.Start();
            }
            RefreshBar();
        }

        private void Btn_SelectCancel_Clicked(object sender, ModBase.RouteEventArgs e)
        {
            Items_SetSelectAll(false);
        }

        private void Btn_FavoritesShare_Clicked(object sender, ModBase.RouteEventArgs e)
        {
            try
            {
                ModBase.ClipboardSet(ModComp.CompFavorites.GetShareCode(SelectedItemList.Select(i => ((ModComp.CompProject)i.Tag).Id).ToList()));
                Items_SetSelectAll(false);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[CompFavourites] 分享收藏时发生错误", ModBase.LogLevel.Hint);
            }
        }

        private void Btn_FavoritesDownload_Clicked(object sender, ModBase.RouteEventArgs e)
        {
            try
            {
                if (SelectedItemList.Count == 1)
                {
                    ModMain.Hint("要不……你直接进详情页里下载吧……");
                    return;
                }
                if (1 != ModMain.MyMsgBox($"批量下载功能仍旧处于测试状态{Constants.vbCrLf}使用此功能下载模组不会自动下载前置项。{Constants.vbCrLf}请在下载前仔细思考自己的需求，并仔细检查自己的选择，避免下载错误导致时间和网络流量的浪费。", "确定使用此功能？", Button1: "继续", Button2: "算了", IsWarn: true))
                    return;
                var SupportedModLoader = new List<ModComp.CompLoaderType>();
                bool LoaderFirstSet = true;
                bool HasMod = false;
                foreach (var Item in SelectedItemList) // 获取共同支持的 ModLoader
                {
                    ModComp.CompProject Proj = (ModComp.CompProject)Item.Tag;
                    if (Proj.Type == ModComp.CompType.Mod)
                    {
                        HasMod = true;
                        if (LoaderFirstSet)
                        {
                            LoaderFirstSet = false;
                            SupportedModLoader = Proj.ModLoaders;
                        }
                        else
                        {
                            SupportedModLoader = SupportedModLoader.Intersect(Proj.ModLoaders).ToList();
                        }
                    }
                }
                // 检查是否有共同支持的 ModLoader
                if (HasMod && SupportedModLoader.Count == 0)
                {
                    ModMain.Hint("所选模组不支持相同的加载器", ModMain.HintType.Critical);
                    return;
                }
                // 要求选择版本
                var DesiredModLoader = ModComp.CompLoaderType.Any;
                if (HasMod && SupportedModLoader.Count > 0)
                {
                    if (SupportedModLoader.Count > 0)
                    {
                        var MSelection = new List<IMyRadio>();
                        foreach (var i in SupportedModLoader)
                            MSelection.Add(new MyRadioBox() { Text = i.ToString() });
                        var SelectedModLoaderStr = ModMain.MyMsgBoxSelect(MSelection, "选择期望的加载器", Button2: "取消");
                        if (SelectedModLoaderStr is null)
                            return;
                        DesiredModLoader = SupportedModLoader[(int)SelectedModLoaderStr];
                    }
                }
                ModMain.Hint("请稍后，正在查询详细版本支持中，这可能需要一段时间……");
                // 输入 Ids，输出合适版本
                var GetInfoAndDownloadLoader = new List<ModLoader.LoaderBase>();
                GetInfoAndDownloadLoader.Add(new ModLoader.LoaderTask<List<string>, List<ModNet.NetFile>>("查询资源信息", (Ts) =>
{
var AllFiles = new List<List<ModComp.CompFile>>();
var SuitVersion = new List<string>();
bool VersionFirstSet = true;
    // 工程支持的全部版本获取
List<string> GetAllVersionList(List<List<string>> Ls)
{
var AllVersionList = new List<string>();
foreach (var i in Ls)
AllVersionList.AddRange(i);
return AllVersionList.Distinct().ToList();
};
    // 获取多个工程之间支持的版本的交集
int FinishedTasks = 0;
foreach (var Item in Ts.Input)
{
ModBase.RunInNewThread(() => { try { AllFiles.Add(ModComp.CompFilesGet(Item, ModComp.CompRequest.IsFromCurseForge(Item)).Where(i => i.Type != ModComp.CompType.Mod || i.ModLoaders.Contains(DesiredModLoader)).ToList()); } catch (Exception ex) { ModBase.Log(ex, $"获取 {Item} 的下载信息失败", ModBase.LogLevel.Hint); } finally { FinishedTasks += 1; } });
}
while (FinishedTasks != Ts.Input.Count)
Thread.Sleep(200);
    // 求取共同的版本
foreach (var Item in AllFiles)
{
var Current = GetAllVersionList(Item.Select(i => i.GameVersions).ToList());
        // Log(Current.Join(","))
if (VersionFirstSet)
{
VersionFirstSet = false;
SuitVersion = Current;
}
else
{
SuitVersion = SuitVersion.Intersect(Current).ToList();
}
        // Log(SuitVersion.Join(","))
if (SuitVersion.Count == 0)
{
ModMain.Hint("不存在指定加载器并且同版本的资源", ModMain.HintType.Critical);
Ts.Abort();
return;
}
}
    // 要求用户选择希望下载的版本
object SelectedVersion = null;
ModBase.RunInUiWait(() =>
{
var Selection = new List<IMyRadio>();
foreach (var i in SuitVersion)
Selection.Add(new MyRadioBox() { Text = i });
SelectedVersion = ModMain.MyMsgBoxSelect(Selection, "选择期望的游戏版本", Button2: "取消");
if (SelectedVersion is null)
{
Ts.Abort();
return;
}
});
string SelectedVersionStr = SuitVersion[Conversions.ToInteger(SelectedVersion)];
ModMain.Hint($"已选择 {SelectedVersionStr} 版本，下面请选择保存位置");
string SaveFolder = ModBase.SelectFolder();
if (string.IsNullOrWhiteSpace(SaveFolder))
{
Ts.Abort();
return;
}
var Res = new List<ModNet.NetFile>();
foreach (var Target in AllFiles)
{
        // 获取有期望版本号的文件
var FinalChoices = Target.Where(i => i.GameVersions.Contains(SelectedVersionStr)).ToList();
        // 按照发布日期排序
FinalChoices = FinalChoices.Sort((ModComp.CompFile a, ModComp.CompFile b) => a.ReleaseDate > b.ReleaseDate);
        // 选择最新版本进行下载
Res.Add(FinalChoices.First().ToNetFile(SaveFolder + FinalChoices.First().FileName));
}
Ts.Output = Res;
})
                {
                    ProgressWeight = 2d
                });
                GetInfoAndDownloadLoader.Add(new ModNet.LoaderDownload("批量下载合适资源", new List<ModNet.NetFile>()) { ProgressWeight = 8d });
                var CheckLoader = new ModLoader.LoaderCombo<List<string>>($"批量下载资源({ModBase.GetUuid()})", GetInfoAndDownloadLoader) { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
                CheckLoader.Start(SelectedItemList.Select(i => ((ModComp.CompProject)i.Tag).Id).ToList());
                ModLoader.LoaderTaskbarAdd(CheckLoader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
                Items_SetSelectAll(false);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "批量下载收藏时发生错误", ModBase.LogLevel.Hint);
            }
        }

        private void Items_SetSelectAll(bool TargetStatus)
        {
            if (IsSearching)
            {
                foreach (MyListItem Item in SearchResult)
                    Item.Checked = TargetStatus;
            }
            else
            {
                foreach (MyListItem Item in CompItemList)
                    Item.Checked = TargetStatus;
            }
            SelectedItemList = CompItemList.Where(e => e.Checked).ToList();
        }

        private void Items_CancelFavorites(MyListItem Item)
        {
            try
            {
                CompItemList.Remove(Item);
                if (SelectedItemList.Contains(Item))
                    SelectedItemList.Remove(Item);
                if (SearchResult.Contains(Item))
                    SearchResult.Remove(Item);
                CurrentFavTarget.Favs.Remove(Conversions.ToString(((dynamic)Item.Tag).Id));
                ModComp.CompFavorites.Save();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[CompFavourites] 移除收藏时发生错误");
            }
        }

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (My.MyWpfExtension.Computer.Keyboard.CtrlKeyDown && e.Key == Key.A)
                Items_SetSelectAll(true);
        }

        private void Manage_Click(object sender, MouseButtonEventArgs e)
        {
            var Body = new ContextMenu();
            var NewItem = new MyMenuItem()
            {
                Header = "分享当前收藏夹",
                Icon = ModBase.Logo.IconButtonShare
            };
            NewItem.Click += () => { try { if (CurrentFavTarget.Favs.Count == 0) { ModMain.Hint("分享了个寂寞啊！"); return; } ModBase.ClipboardSet(ModComp.CompFavorites.GetShareCode(CurrentFavTarget.Favs)); } catch (Exception ex) { ModBase.Log(ex, "[Favourites] 分享收藏时发生错误", ModBase.LogLevel.Hint); } };
            Body.Items.Add(NewItem);
            NewItem = new MyMenuItem()
            {
                Header = "导入收藏",
                Icon = ModBase.Logo.IconButtonAdd
            };
            NewItem.Click += () => { try { string ClipData = ModMain.MyMsgBoxInput("输入分享的收藏", HintText: "例如 [\"23333\"]"); if (string.IsNullOrWhiteSpace(ClipData)) return; var NewFavs = ModComp.CompFavorites.GetIdsByShareCode(ClipData); if (NewFavs.Count == 0) { ModMain.Hint("分享了个寂寞啊！"); return; } int UserWant = ModMain.MyMsgBox("你希望将分享的收藏加入到当前收藏夹还是新的收藏夹中？", Button1: "新的收藏夹", Button2: "当前收藏夹"); switch (UserWant) { case 1: { string NewFavName = ModMain.MyMsgBoxInput("新收藏夹名称", "请输入新收藏夹名称"); if (string.IsNullOrWhiteSpace(NewFavName)) return; ModComp.CompFavorites.FavoritesList.Add(ModComp.CompFavorites.GetNewFav(NewFavName, NewFavs)); ModComp.CompFavorites.Save(); RefreshFavTargets(); this.ComboTargetFav.SelectedIndex = this.ComboTargetFav.Items.Count - 1; break; } case 2: { CurrentFavTarget.Favs.AddRange(NewFavs); CurrentFavTarget.Favs.Distinct(); ModComp.CompFavorites.Save(); Loader.Start(IsForceRestart: true); break; } } } catch (Exception ex) { ModBase.Log(ex, "解析分享数据失败", ModBase.LogLevel.Hint); } };
            Body.Items.Add(NewItem);
            NewItem = new MyMenuItem()
            {
                Header = "新建收藏夹",
                Icon = ModBase.Logo.IconButtonCreate
            };
            NewItem.Click += () =>
                {
                    string NewFavName = ModMain.MyMsgBoxInput("新建收藏夹", "请输入新收藏夹名称");
                    if (string.IsNullOrWhiteSpace(NewFavName))
                        return;
                    ModComp.CompFavorites.FavoritesList.Add(ModComp.CompFavorites.GetNewFav(NewFavName, null));
                    ModComp.CompFavorites.Save();
                    RefreshFavTargets();
                    this.ComboTargetFav.SelectedIndex = this.ComboTargetFav.Items.Count - 1;
                };
            Body.Items.Add(NewItem);
            NewItem = new MyMenuItem()
            {
                Header = "重命名收藏夹名称",
                Icon = ModBase.Logo.IconButtonEdit
            };
            NewItem.Click += () =>
                {
                    string newName = ModMain.MyMsgBoxInput("输入新名称", DefaultInput: CurrentFavTarget.Name);
                    if (string.IsNullOrWhiteSpace(newName) || (CurrentFavTarget.Name ?? "") == (newName ?? ""))
                        return;
                    CurrentFavTarget.Name = newName;
                    ModComp.CompFavorites.Save();
                    RefreshFavTargets();
                };
            Body.Items.Add(NewItem);
            NewItem = new MyMenuItem()
            {
                Header = "删除当前收藏夹",
                Icon = ModBase.Logo.IconButtonDelete
            };
            NewItem.Click += () =>
                {
                    if (ModComp.CompFavorites.FavoritesList.Count == 1)
                    {
                        ModMain.Hint("您不能删除最后一个收藏夹");
                        return;
                    }
                    string content = $"确认删除 {CurrentFavTarget.Name} 收藏夹？" + Constants.vbCrLf + Constants.vbCrLf;
                    content += $"此收藏夹有 {CurrentFavTarget.Favs.Count} 个收藏项目" + Constants.vbCrLf;
                    content += "收藏夹 ID 为 " + CurrentFavTarget.Id + Constants.vbCrLf;
                    content += "此操作不可逆！";
                    int res = ModMain.MyMsgBox(content, "删除确认", IsWarn: true, Button1: "否", Button2: "是", Button3: "否");
                    if (res == 2)
                    {
                        ModComp.CompFavorites.FavoritesList.Remove(CurrentFavTarget);
                        ModComp.CompFavorites.Save();
                        ModMain.Hint("已删除收藏夹", ModMain.HintType.Finish);
                        RefreshFavTargets();
                        this.ComboTargetFav.SelectedIndex = 0;
                    }
                };
            Body.Items.Add(NewItem);
            Body.PlacementTarget = (UIElement)sender;
            Body.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            Body.IsOpen = true;
        }

        private void ComboTargetFav_Selected(object sender, RoutedEventArgs e)
        {
            if (this.ComboTargetFav.SelectedItem is null)
                return;
            Items_SetSelectAll(false);
            Loader.Start(IsForceRestart: true);
        }

        private void HintGetFail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string Content = "由于在线资源被删除或者网络问题等因素导致以下资源未获取成功（以资源的 ID 展示）" + Constants.vbCrLf + Constants.vbCrLf;
            var FailIds = Loader.Input.Except(Loader.Output.Select(i => i.Id).ToList()).ToList();
            foreach (var Id in FailIds)
                Content += $" - {Id}" + Constants.vbCrLf;
            ModMain.MyMsgBox(Content, "部分收藏项目获取失败", Button2: "复制这些 ID", Button3: "移除这些收藏", Button2Action: () => ModBase.ClipboardSet(FailIds.Join(Constants.vbCrLf)), Button3Action: () =>
{
foreach (var Id in FailIds)
CurrentFavTarget.Favs.Remove(Id);
ModComp.CompFavorites.Save();
ModMain.Hint("已移除相关收藏", ModMain.HintType.Finish);
});
        }

        #endregion

        #region 搜索

        private bool IsSearching
        {
            get
            {
                return !string.IsNullOrWhiteSpace(this.PanSearchBox.Text);
            }
        }

        private bool AllowSearch = true;
        private List<MyListItem> SearchResult = new List<MyListItem>();
        public void SearchRun()
        {
            if (!AllowSearch)
                return;
            if (IsSearching)
            {
                // 构造请求
                var QueryList = new List<ModBase.SearchEntry<MyListItem>>();
                foreach (MyListItem Item in CompItemList)
                {
                    if (!(Item.Tag is ModComp.CompProject))
                        continue;
                    ModComp.CompProject Entry = (ModComp.CompProject)Item.Tag;
                    var SearchSource = new List<KeyValuePair<string, double>>();
                    SearchSource.Add(new KeyValuePair<string, double>(Entry.RawName, 1d));
                    if (Entry.Description is not null && !string.IsNullOrEmpty(Entry.Description))
                    {
                        SearchSource.Add(new KeyValuePair<string, double>(Entry.Description, 0.4d));
                    }
                    if ((Entry.TranslatedName ?? "") != (Entry.RawName ?? ""))
                        SearchSource.Add(new KeyValuePair<string, double>(Entry.TranslatedName, 1d));
                    SearchSource.Add(new KeyValuePair<string, double>(string.Join("", Entry.Tags), 0.2d));
                    QueryList.Add(new ModBase.SearchEntry<MyListItem>() { Item = Item, SearchSource = SearchSource });
                }
                // 进行搜索
                SearchResult = ModBase.Search(QueryList, PanSearchBox.Text, MaxBlurCount: 6, MinBlurSimilarity: 0.35d).Select(r => r.Item).ToList();
            }
            RefreshContent();
            RefreshCardTitle();
        }

        #endregion

    }
}