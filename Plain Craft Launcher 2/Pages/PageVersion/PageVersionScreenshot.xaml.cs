using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class PageVersionScreenshot : IRefreshable
    {
        public PageVersionScreenshot()
        {
            this.Loaded += PageSetupLaunch_Loaded;
        }
        private void RefreshSelf()
        {
            Refresh();
        }

        void IRefreshable.Refresh() => RefreshSelf();
        public static async void Refresh()
        {
            if (ModMain.FrmVersionScreenshot is not null)
                await ModMain.FrmVersionScreenshot.Reload();
            ModMain.FrmVersionLeft.ItemScreenshot.Checked = true;
            ModMain.Hint("正在刷新……", Log: false);
        }

        private bool IsLoad = false;
        private async void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();
            ScreenshotPath = PageVersionLeft.Version.PathIndie + @"screenshots\";
            if (!Directory.Exists(ScreenshotPath))
                Directory.CreateDirectory(ScreenshotPath);
            await Reload();

            // 非重复加载部分
            if (IsLoad)
                return;
            IsLoad = true;

        }

        private List<string> FileList = new List<string>();
        private string ScreenshotPath;

        /// <summary>
    /// 确保当前页面上的信息已正确显示。
    /// </summary>
        public async System.Threading.Tasks.Task Reload()
        {
            ModAnimation.AniControlEnabled += 1;
            this.PanBack.ScrollToHome();
            await LoadFileList();
            ModAnimation.AniControlEnabled -= 1;
        }

        private void RefreshTip()
        {
            if (FileList.Count.Equals(0))
            {
                this.PanNoPic.Visibility = Visibility.Visible;
                this.PanContent.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.PanNoPic.Visibility = Visibility.Collapsed;
                this.PanContent.Visibility = Visibility.Visible;
            }
        }

        private async System.Threading.Tasks.Task LoadFileList()
        {
            ModBase.Log("[Screenshot] 刷新截图文件");
            FileList.Clear();
            if (Directory.Exists(ScreenshotPath))
                FileList = Directory.EnumerateFiles(ScreenshotPath, "*", SearchOption.TopDirectoryOnly).ToList();
            string[] AllowedSuffix = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tiff" };
            FileList = FileList.Where(e => AllowedSuffix.Contains(new FileInfo(e).Extension.ToLower())).ToList();
            this.PanList.Children.Clear();
            RefreshTip();
            FileList = FileList.Where(e => !e.ContainsF(@"\debug\")).ToList(); // 排除资源包调试输出
            FileList = FileList.Sort((a, b) => new FileInfo(a).CreationTime > new FileInfo(b).CreationTime);
            ModBase.Log("[Screenshot] 共发现 " + FileList.Count + " 个截图文件");
            if (FileList.Count == 0)
                return;
            await ListAppend(20, 0);
        }

        private async void RequireAppend()
        {
            if (!_AppendLock && this.PanBack.VerticalOffset + this.PanBack.ViewportHeight >= this.PanBack.ExtentHeight)
            {
                await ListAppend();
            }
        }

        private bool _AppendLock = false;
        private int _Offset = 0;
        private async System.Threading.Tasks.Task ListAppend(int Count = 20, int Offset = -1)
        {
            _AppendLock = true;
            if (Offset == -1)
            {
                if (_Offset * Count > FileList.Count)
                    return;
                Offset = _Offset + 1;
                _Offset += 1;
            }
            else
            {
                _Offset = Offset;
            }
            if (Count * Offset > FileList.Count)
                return;
            for (int j = Count * Offset, loopTo = Count * (Offset + 1) - 1; j <= loopTo; j++)
            {
                if (j >= FileList.Count)
                    break;
                string i = FileList.ElementAt(j);
                try
                {
                    if (!File.Exists(i))
                        continue; // 文件在加载途中消失了
                    if (File.GetAttributes(i).HasFlag(FileAttributes.Hidden))
                        continue; // 隐藏文件
                    if (new FileInfo(i).Length == 0L)
                        continue; // 空文件
                    var myCard = new MyCard()
                    {
                        Height = double.NaN, // 允许高度自适应
                        Width = double.NaN,  // 允许宽度自适应
                        Margin = new Thickness(7d),
                        Tag = i,
                        ToolTip = i.Replace(ScreenshotPath, "") // 适配高清截图模组
                    };
                    var grid = new Grid();
                    myCard.Children.Add(grid);

                    grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(9d) });
                    grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(120d) });
                    grid.RowDefinitions.Add(new RowDefinition());

                    // 图片
                    var image = new Image();
                    image.Source = await System.Threading.Tasks.Task.Run(() =>
                        {
                            var bitmapImage = new BitmapImage();
                            string loadSource = i;
                            using (var fs = new FileStream(i, FileMode.Open, FileAccess.Read))
                            {
                                var Header = new byte[2];
                                fs.Read(Header, 0, 2);
                                fs.Seek(0L, SeekOrigin.Begin);
                                if (Header[0] == 82 && Header[1] == 73)
                                {
                                    // WebP 格式，需要转换
                                    var FileBytes = new byte[(int)(fs.Length - 1L + 1)];
                                    fs.Read(FileBytes, 0, FileBytes.Length);
                                    var Pic = MyBitmap.WebPDecoder.DecodeFromBytes(FileBytes);
                                    string picTempPath = ModBase.PathTemp + @"Screenshot\";
                                    Directory.CreateDirectory(picTempPath);
                                    loadSource = picTempPath + ModBase.GetHash(i) + ".png";
                                    Pic.Save(loadSource);
                                }
                            }
                            using (var fs = new FileStream(loadSource, FileMode.Open, FileAccess.Read))
                            {
                                bitmapImage.BeginInit();
                                bitmapImage.DecodePixelHeight = 200;
                                bitmapImage.DecodePixelWidth = 400;
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.StreamSource = fs;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze();
                            }
                            return bitmapImage;
                        });
                    image.Stretch = Stretch.Uniform; // 使图片自适应控件大小
                    Grid.SetRow(image, 1);
                    grid.Children.Add(image);

                    // 按钮
                    var stackPanel = new StackPanel();
                    stackPanel.Orientation = Orientation.Horizontal;
                    stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
                    stackPanel.Margin = new Thickness(3d, 5d, 3d, 5d);
                    Grid.SetRow(stackPanel, 2);
                    grid.Children.Add(stackPanel);

                    var btnOpen = new MyIconTextButton()
                    {
                        Name = "BtnOpen",
                        Text = "打开",
                        LogoScale = 0.8d,
                        Logo = ModBase.Logo.IconButtonOpen,
                        Tag = i
                    };
                    btnOpen.Click += (_, __) => this.btnOpen_Click();
                    stackPanel.Children.Add(btnOpen);
                    var btnDelete = new MyIconTextButton()
                    {
                        Name = "BtnDelete",
                        Text = "删除",
                        LogoScale = 0.8d,
                        Logo = ModBase.Logo.IconButtonDelete,
                        Tag = i
                    };
                    btnDelete.Click += (_, __) => this.btnDelete_Click();
                    stackPanel.Children.Add(btnDelete);
                    var btnCopy = new MyIconTextButton()
                    {
                        Name = "BtnCopy",
                        Text = "复制",
                        LogoScale = 0.8d,
                        Logo = ModBase.Logo.IconButtonCopy,
                        Tag = i
                    };
                    btnCopy.Click += (_, __) => this.btnCopy_Click();
                    stackPanel.Children.Add(btnCopy);
                    this.PanList.Children.Add(myCard);
                    myCard.Opacity = 0d;
                    ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(myCard, 1d, 200) });
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"[Screenshot] 创建 {i} 截图预览失败，图像可能损坏");
                }
            }
            _AppendLock = false;
        }

        private void RemoveItem(string Path)
        {
            try
            {
                foreach (var i in this.PanList.Children)
                {
                    if (((MyCard)i).Tag.Equals(Path))
                    {
                        this.PanList.Children.Remove((UIElement)i);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "未能找到对应 UI");
            }
        }

        private string GetPathFromSender(MyIconTextButton sender)
        {
            return Conversions.ToString(sender.Tag);
        }

        private void btnOpen_Click(MyIconTextButton sender, EventArgs e)
        {
            ModBase.OpenExplorer(GetPathFromSender(sender));
        }
        private void btnDelete_Click(MyIconTextButton sender, EventArgs e)
        {
            ModBase.Path = GetPathFromSender(sender);
            RemoveItem(ModBase.Path);
            try
            {
                File.Delete(ModBase.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                ModMain.Hint("已将截图移至回收站！");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除截图失败！", ModBase.LogLevel.Hint);
            }
        }
        private void btnCopy_Click(MyIconTextButton sender, EventArgs e)
        {
            string imagePath = GetPathFromSender(sender);
            if (File.Exists(imagePath))
            {
                int TryTime = 0;
                while (TryTime <= 5)
                {
                    try
                    {
                        ModBase.Log("[Screenshot] 尝试复制" + imagePath + "到剪贴板");
                        Clipboard.SetImage(new BitmapImage(new Uri(imagePath)));
                        ModMain.Hint("已复制截图到剪贴板！");
                        TryTime = 6;
                        return;
                    }
                    catch (Exception ex)
                    {
                        TryTime += 1;
                        ModBase.Log(ex, $"[Screenshot]第 {TryTime} 次复制尝试失败");
                    }
                }
                ModMain.Hint("截图复制失败！", ModMain.HintType.Critical);
            }
            else
            {
                ModMain.Hint("截图文件不存在！");
            }
        }

        private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
        {
            if (!Directory.Exists(ScreenshotPath))
                Directory.CreateDirectory(ScreenshotPath);
            ModBase.OpenExplorer(ScreenshotPath);
        }
    }
}