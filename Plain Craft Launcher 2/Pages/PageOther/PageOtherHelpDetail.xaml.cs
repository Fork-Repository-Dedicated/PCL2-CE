using System;
using System.Windows;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageOtherHelpDetail : IRefreshable
    {
        public ModMain.HelpEntry Entry;

        public PageOtherHelpDetail()
        {
            this.Loaded += PageOtherHelpDetail_Loaded;
        }

        public void Refresh()
        {
            Init(new ModMain.HelpEntry(Entry.RawPath));
        }

        private void PageOtherHelpDetail_Loaded(object sender, RoutedEventArgs e)
        {
            this.PanBack.ScrollToTop();
        }

        /// <summary>
    /// 根据特定帮助项初始化页面 UI，返回是否成功加载。
    /// </summary>
        public bool Init(ModMain.HelpEntry Entry)
        {
            string FileContent = "<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">" + (Entry.XamlContent ?? "") + "</StackPanel>";
            try
            {
                if (string.IsNullOrEmpty(Entry.XamlContent))
                    throw new Exception("帮助 xaml 文件为空");
                this.Entry = Entry;
                this.PanCustom.Children.Clear();
                FileContent = ModMain.HelpArgumentReplace(FileContent);
                this.PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(FileContent));
                return true;
            }
            catch (Exception ex)
            {
                ModBase.Log("[System] 自定义信息内容：" + Constants.vbCrLf + FileContent);
                ModBase.Log(ex, "加载帮助 xaml 文件失败", ModBase.LogLevel.Msgbox);
                return false;
            }
        }

    }
}