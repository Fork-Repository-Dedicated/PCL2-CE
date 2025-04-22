using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class PageOtherVote
    {
        public class VoteType
        {
            public string Title { get; set; }
            public string Url { get; set; }
            public DateTime Time { get; set; }
            public string Vote { get; set; }
        }

        private new bool IsLoaded = false;

        public PageOtherVote()
        {
            Loader = new ModLoader.LoaderTask<int, List<VoteType>>("VoteList", VoteListGet, LoaderInput);
            this.Loaded += PageOtherFeedback_Loaded;
        }
        private void PageOtherFeedback_Loaded(object sender, RoutedEventArgs e)
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanContent, this.PanInfo, Loader, (_) => LoadList(), this.LoaderInput);
            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

        }
        public ModLoader.LoaderTask<int, List<VoteType>> Loader;

        private int LoaderInput()
        {
            return 0; // awa?
        }

        public void VoteListGet(ModLoader.LoaderTask<int, List<VoteType>> Task)
        {
            var content = ModNet.NetGetCodeByRequestRetry("https://github.com/Hex-Dragon/PCL2/discussions/categories/%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8?discussions_q=is%3Aopen+category%3A%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8+sort%3Atop", BackupUrl: "https://kkgithub.com/Hex-Dragon/PCL2/discussions/categories/%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8?discussions_q=is%3Aopen+category%3A%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8+sort%3Atop", UseBrowserUserAgent: true);
            if (content is null)
                throw new Exception("空内容");

            string pattern = "<div class=\"d-flex flex-auto flex-items-start\">(.*?)<svg aria-hidden=\"true\" height=\"16\" viewBox=\"0 0 16 16\" version=\"1.1\" width=\"16\" data-view-component=\"true\" class=\"octicon octicon-comment color-fg-muted mr-1\">";
            var matches = Regex.Matches(Conversions.ToString(content), pattern, RegexOptions.Singleline);
            var contentList = new List<string>();

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    contentList.Add(match.Groups[1].Value);
                }
            }

            var res = new List<VoteType>();

            foreach (var c in contentList)
            {
                var item = new VoteType();
                // 抓取标题和网址
                pattern = @"<a[^>]*?\bdata-hovercard-type=""discussion""[^>]*?href=""(.*?)""[^>]*?>(.*?)</a>";
                var SingleMatch = Regex.Match(c, pattern, RegexOptions.Singleline);
                // 使用正则表达式匹配
                if (SingleMatch.Success)
                {
                    // 输出匹配到的href和文本内容
                    item.Title = SingleMatch.Groups[2].Value.Trim();
                    item.Url = "https://github.com" + SingleMatch.Groups[1].Value;
                }
                // 抓取时间
                pattern = "<relative-time datetime=\"(.*?)\" class=\"no-wrap\"";
                SingleMatch = Regex.Match(c, pattern, RegexOptions.Singleline);
                if (SingleMatch.Success)
                    item.Time = DateTime.Parse(SingleMatch.Groups[1].Value).ToLocalTime();
                // 抓取票数
                pattern = @"aria-label=""Upvote: (\d+)""";
                SingleMatch = Regex.Match(c, pattern);
                if (SingleMatch.Success)
                {
                    item.Vote = SingleMatch.Groups[1].Value.Trim();
                }
                else
                {
                    item.Vote = "?";
                }

                res.Add(item);
            }
            Task.Output = res;
        }


        public void LoadList()
        {
            this.PanList.Children.Clear();
            foreach (var item in Loader.Output)
            {
                var ele = new MyListItem() { Type = MyListItem.CheckType.Clickable, Title = item.Vote + " 票 | " + item.Title, Info = item.Time.ToString() };
                ele.Click += () => ModBase.OpenWebsite(item.Url);
                this.PanList.Children.Add(ele);
            }
        }

        private void Vote_Click(object sender, MouseButtonEventArgs e)
        {
            PageOtherLeft.TryVote();
        }
    }
}