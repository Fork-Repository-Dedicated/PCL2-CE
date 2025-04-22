using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class PageVersionWorld : IRefreshable
    {

        private object QuickPlayFeature = false;

        public PageVersionWorld()
        {
            this.Loaded += PageSetupLaunch_Loaded;
        }

        private void RefreshSelf()
        {
            Refresh();
            CheckQuickPlay();
        }

        void IRefreshable.Refresh() => RefreshSelf();
        public static void Refresh()
        {
            if (ModMain.FrmVersionWorld is not null)
                ModMain.FrmVersionWorld.Reload();
            ModMain.FrmVersionLeft.ItemWorld.Checked = true;
            ModMain.Hint("正在刷新……", Log: false);
        }
        private bool IsLoad = false;
        private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();
            WorldPath = PageVersionLeft.Version.PathIndie + @"saves\";
            if (!Directory.Exists(WorldPath))
                Directory.CreateDirectory(WorldPath);
            Reload();

            // 非重复加载部分
            if (IsLoad)
                return;
            IsLoad = true;
            CheckQuickPlay();
        }

        private List<string> FileList = new List<string>();
        private string WorldPath;

        /// <summary>
    /// 确保当前页面上的信息已正确显示。
    /// </summary>
        public void Reload()
        {
            ModAnimation.AniControlEnabled += 1;
            this.PanBack.ScrollToHome();
            LoadFileList();
            ModAnimation.AniControlEnabled -= 1;
        }

        private void RefreshUI()
        {
            this.PanCard.Title = $"存档列表 ({FileList.Count})";
            if (FileList.Count.Equals(0))
            {
                this.PanNoWorld.Visibility = Visibility.Visible;
                this.PanContent.Visibility = Visibility.Collapsed;
                this.PanNoWorld.UpdateLayout();
            }
            else
            {
                this.PanNoWorld.Visibility = Visibility.Collapsed;
                this.PanContent.Visibility = Visibility.Visible;
                this.PanContent.UpdateLayout();
            }
        }

        private void CheckQuickPlay()
        {
            var VersionJson = PageVersionLeft.Version.JsonObject;
            if (VersionJson["arguments"] is not null && VersionJson["arguments"]["game"] is not null)
            {
                foreach (var Argument in VersionJson["arguments"]["game"])
                {
                    if (Argument.Type == JTokenType.Object && JObject.FromObject(Argument).ContainsKey("value") && Argument["value"].ToString().Contains("--quickPlaySingleplayer"))
                    {
                        QuickPlayFeature = true;
                        break;
                    }
                }
            }
        }

        private void LoadFileList()
        {
            ModBase.Log("[World] 刷新存档文件");
            FileList.Clear();
            FileList = Directory.EnumerateDirectories(WorldPath).ToList();
            if (ModBase.ModeDebug)
                ModBase.Log("[World] 共发现 " + FileList.Count + " 个存档文件夹", ModBase.LogLevel.Debug);
            this.PanList.Children.Clear();
            CheckQuickPlay();

            if (ModBase.ModeDebug)
            {
                if (Conversions.ToBoolean(QuickPlayFeature))
                {
                    ModBase.Log("[World] 该版本支持存档快捷启动", ModBase.LogLevel.Debug);
                }
                else
                {
                    ModBase.Log("[World] 该版本不支持存档快捷启动", ModBase.LogLevel.Debug);
                }
            }

            foreach (var i in FileList)
            {
                string SaveLogo = i + @"\icon.png";
                if (!File.Exists(SaveLogo))
                    SaveLogo = ModBase.PathImage + "Icons/NoIcon.png";
                var worldItem = new MyListItem()
                {
                    Logo = SaveLogo,
                    Title = ModBase.GetFolderNameFromPath(i),
                    Info = $"创建时间：{Directory.GetCreationTime(i).ToString("yyyy'/'MM'/'dd")}，最后修改时间：{Directory.GetLastWriteTime(i).ToString("yyyy'/'MM'/'dd")}",
                    Tag = i
                };
                var BtnOpen = new MyIconButton()
                {
                    Logo = ModBase.Logo.IconButtonOpen,
                    ToolTip = "打开",
                    Tag = i
                };
                BtnOpen.Click += (_, __) => this.BtnOpen_Click();
                var BtnDelete = new MyIconButton()
                {
                    Logo = ModBase.Logo.IconButtonDelete,
                    ToolTip = "删除",
                    Tag = i
                };
                BtnDelete.Click += (_, __) => this.BtnDelete_Click();
                var BtnCopy = new MyIconButton()
                {
                    Logo = ModBase.Logo.IconButtonCopy,
                    ToolTip = "复制",
                    Tag = i
                };
                BtnCopy.Click += (_, __) => this.BtnCopy_Click();
                var BtnInfo = new MyIconButton()
                {
                    Logo = ModBase.Logo.IconButtonInfo,
                    ToolTip = "详情",
                    Tag = i
                };
                BtnInfo.Click += (_, __) => this.BtnInfo_Click();

                var BtnLaunch = new MyIconButton()
                {
                    Logo = ModBase.Logo.IconPlay,
                    ToolTip = "快捷启动",
                    Tag = i
                };
                BtnLaunch.Click += (_, __) => this.BtnQuickPlayWorld_Click();



                if (Conversions.ToBoolean(QuickPlayFeature))
                {
                    worldItem.Buttons = new[] { BtnOpen, BtnDelete, BtnCopy, BtnInfo, BtnLaunch };
                }
                else
                {
                    worldItem.Buttons = new[] { BtnOpen, BtnDelete, BtnCopy, BtnInfo };
                }

                this.PanList.Children.Add(worldItem);
            }
            RefreshUI();
        }

        private string GetPathFromSender(object sender)
        {
            return Conversions.ToString(((dynamic)sender).Tag);
        }

        private void RemoveItem(string Path)
        {
            try
            {
                foreach (var i in this.PanList.Children)
                {
                    if (((MyListItem)i).Tag.Equals(Path))
                    {
                        this.PanList.Children.Remove((MyListItem)i);
                        FileList.Remove(Path);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "未能找到对应 UI");
            }
            RefreshUI();
        }

        private void BtnDelete_Click(object sender, MouseButtonEventArgs e)
        {
            ModBase.Path = GetPathFromSender(sender);
            RemoveItem(ModBase.Path);
            try
            {
                My.MyWpfExtension.Computer.FileSystem.DeleteDirectory(ModBase.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                ModMain.Hint("已将存档移至回收站！");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除存档失败！", ModBase.LogLevel.Hint);
            }
        }
        private void BtnCopy_Click(object sender, MouseButtonEventArgs e)
        {
            string Path = GetPathFromSender(sender);
            try
            {
                if (Directory.Exists(Path))
                {
                    Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { Path });
                    ModMain.Hint("已复制存档文件夹到剪贴板！");
                }
                else
                {
                    ModMain.Hint("存档文件夹不存在！");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "复制失败……", ModBase.LogLevel.Hint);
            }
        }
        private void BtnInfo_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string Path = GetPathFromSender(sender);
                var infos = new List<string>();
                infos.Add("名称：" + ModBase.GetFileNameFromPath(Path));
                infos.Add("创建日期：" + Directory.GetCreationTime(Path).ToString("yyyy'/'MM'/'dd"));
                infos.Add("最后一次修改日期：" + Directory.GetLastWriteTime(Path).ToString("yyyy'/'MM'/'dd"));
                Directory.CreateDirectory(Path + @"\playerdata");
                infos.Add("玩家数量：" + Directory.GetFiles(Path + @"\playerdata", "*.dat", SearchOption.TopDirectoryOnly).Count());
                Directory.CreateDirectory(Path + @"\datapacks");
                infos.Add("数据包数量：" + (Directory.GetDirectories(Path + @"\datapacks").Count() + Directory.GetFiles(Path + @"\datapacks").Count()).ToString());
                ModMain.MyMsgBox(infos.Join(Constants.vbCrLf), "存档详细信息");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取存档详细信息失败……", ModBase.LogLevel.Hint);
            }
        }
        private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
        {
            ModBase.OpenExplorer(WorldPath);
        }
        private void BtnOpen_Click(object sender, MouseButtonEventArgs e)
        {
            ModBase.OpenExplorer(Conversions.ToString(((dynamic)sender).Tag));
        }
        private void BtnPaste_Click(object sender, MouseButtonEventArgs e)
        {
            ModMain.Hint("正在粘贴存档文件夹，这可能一段时间……");
            var files = Clipboard.GetFileDropList();
            int Copied = 0;
            foreach (string i in files)
            {
                try
                {
                    if (Directory.Exists(i))
                    {
                        if (Directory.Exists(WorldPath + ModBase.GetFolderNameFromPath(i)))
                        {
                            ModMain.Hint("发现同名文件夹，无法粘贴：" + ModBase.GetFolderNameFromPath(i));
                        }
                        else
                        {
                            ModBase.CopyDirectory(i, WorldPath + ModBase.GetFolderNameFromPath(i));
                            Copied += 1;
                        }
                    }
                    else
                    {
                        ModMain.Hint("源文件夹不存在或源目标不是文件夹");
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "粘贴存档文件夹失败", ModBase.LogLevel.Hint);
                    continue;
                }
            }
            if (Copied > 0)
                ModMain.Hint("已粘贴 " + Copied + " 个文件夹", ModMain.HintType.Finish);
            LoadFileList();
        }
        private void BtnQuickPlayWorld_Click(object sender, MouseButtonEventArgs e)
        {
            string WorldName = ModBase.GetFileNameFromPath(GetPathFromSender(sender));
            var LaunchOptions = new ModLaunch.McLaunchOptions() { WorldName = WorldName };
            ModLaunch.McLaunchStart(LaunchOptions);
            ModMain.FrmMain.PageChange(new FormMain.PageStackData() { Page = FormMain.PageType.Launch });
        }
    }
}