using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageOtherFeedback
    {

        public class Feedback
        {
            public string User { get; set; }
            public string Title { get; set; }
            public DateTime Time { get; set; }
            public string Content { get; set; }
            public string Url { get; set; }
            public string ID { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
            public bool Open { get; set; } = true;
        }

        public enum TagID : long
        {
            NewIssue = 4365827012L,
            Bug = 4365944566L,
            Improve = 4365949262L,
            Processing = 4365819896L,
            WaitingResponse = 4365816377L,
            Completed = 4365809832L,
            Decline = 4365654603L,
            NewFeture = 4365949953L,
            Ignored = 4365654601L,
            Duplicate = 4365654597L
        }

        private new bool IsLoaded = false;

        public PageOtherFeedback()
        {
            Loader = new ModLoader.LoaderTask<int, List<Feedback>>("FeedbackList", FeedbackListGet, LoaderInput);
            this.Loaded += PageOtherFeedback_Loaded;
        }
        private void PageOtherFeedback_Loaded(object sender, RoutedEventArgs e)
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanContent, this.PanInfo, Loader, (_) => RefreshList(), this.LoaderInput);
            // 重复加载部分
            this.PanBack.ScrollToHome();
            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

        }

        public ModLoader.LoaderTask<int, List<Feedback>> Loader;

        private int LoaderInput()
        {
            return 0; // awa?
        }

        public void FeedbackListGet(ModLoader.LoaderTask<int, List<Feedback>> Task)
        {
            JArray list;
            list = (JArray)ModNet.NetGetCodeByRequestRetry("https://api.github.com/repos/Hex-Dragon/PCL2/issues?state=all&sort=created&per_page=200", BackupUrl: "https://api.kkgithub.com/repos/Hex-Dragon/PCL2/issues?state=all&sort=created&per_page=200", IsJson: true, UseBrowserUserAgent: true); // 获取近期 200 条数据就够了
            if (list is null)
                throw new Exception("无法获取到内容");
            var res = new List<Feedback>();
            foreach (JObject i in list)
            {
                var item = new Feedback()
                {
                    Title = i["title"].ToString(),
                    Url = i["html_url"].ToString(),
                    Content = i["body"].ToString(),
                    Time = DateTime.Parse(i["created_at"].ToString()),
                    User = i["user"]["login"].ToString(),
                    ID = (string)i["number"],
                    Open = i["state"].ToString().Equals("open")
                };
                JArray thisTags = (JArray)i["labels"];
                foreach (JObject thisTag in thisTags)
                    item.Tags.Add((string)thisTag["id"]);
                res.Add(item);
            }
            Task.Output = res;
        }

        public void RefreshList()
        {
            this.PanListCompleted.Children.Clear();
            this.PanListProcessing.Children.Clear();
            this.PanListWaitingResponse.Children.Clear();
            this.PanListDecline.Children.Clear();
            foreach (var item in Loader.Output)
            {
                var ele = new MyListItem() { Title = item.Title, Type = MyListItem.CheckType.Clickable };
                string StatusDesc = "???";
                if (item.Tags.Contains(((long)TagID.Duplicate).ToString()))
                    continue;
                if (item.Tags.Contains(((long)TagID.NewIssue).ToString()))
                {
                    ele.Logo = ModBase.PathImage + "Blocks/Grass.png";
                    StatusDesc = "未查看";
                }
                if (item.Open)
                {
                    if (item.Tags.Contains(((long)TagID.Processing).ToString()))
                    {
                        ele.Logo = ModBase.PathImage + "Blocks/CommandBlock.png";
                        StatusDesc = "处理中";
                    }
                    if (item.Tags.Contains(((long)TagID.Bug).ToString()))
                    {
                        ele.Logo = ModBase.PathImage + "Blocks/RedstoneBlock.png";
                        StatusDesc = "处理中-Bug";
                    }
                    if (item.Tags.Contains(((long)TagID.Improve).ToString()))
                    {
                        ele.Logo = ModBase.PathImage + "Blocks/Anvil.png";
                        StatusDesc = "处理中-优化";
                    }
                    if (item.Tags.Contains(((long)TagID.WaitingResponse).ToString()))
                    {
                        ele.Logo = ModBase.PathImage + "Blocks/RedstoneLampOff.png";
                        StatusDesc = "等待提交者";
                    }
                    if (item.Tags.Contains(((long)TagID.NewFeture).ToString()))
                    {
                        ele.Logo = ModBase.PathImage + "Blocks/Egg.png";
                        StatusDesc = "处理中-新功能";
                    }
                }
                if (item.Tags.Contains(((long)TagID.Completed).ToString()))
                {
                    ele.Logo = ModBase.PathImage + "Blocks/GrassPath.png";
                    StatusDesc = "已完成";
                }
                if (item.Tags.Contains(((long)TagID.Decline).ToString()))
                {
                    ele.Logo = ModBase.PathImage + "Blocks/CobbleStone.png";
                    StatusDesc = "已拒绝";
                }
                if (item.Tags.Contains(((long)TagID.Ignored).ToString()))
                {
                    ele.Logo = ModBase.PathImage + "Blocks/CobbleStone.png";
                    StatusDesc = "已忽略";
                }
                ele.Info = item.User + " | " + Conversions.ToString(item.Time);
                ele.Tags = StatusDesc;
                ele.Click += () => { switch (ModMain.MyMsgBox($"提交者：{item.User}（{ModBase.GetTimeSpanString(item.Time - DateTime.Now, false)}）{Constants.vbCrLf}状态：{StatusDesc}{Constants.vbCrLf}{Constants.vbCrLf}{item.Content}", "#" + item.ID + " " + item.Title, Button2: "查看详情")) { case 2: { ModBase.OpenWebsite(item.Url); break; } } };
                if (StatusDesc.StartsWithF("处理中"))
                {
                    this.PanListProcessing.Children.Add(ele);
                }
                else if (StatusDesc.Equals("等待提交者"))
                {
                    this.PanListWaitingResponse.Children.Add(ele);
                }
                else if (StatusDesc.Equals("已完成"))
                {
                    this.PanListCompleted.Children.Add(ele);
                }
                else if (StatusDesc.Equals("未查看"))
                {
                    this.PanListNewIssue.Children.Add(ele);
                }
                else if (StatusDesc.Equals("已拒绝"))
                {
                    this.PanListDecline.Children.Add(ele);
                }
                this.PanContentDecline.Visibility = this.PanListDecline.Children.Count.Equals(0) ? Visibility.Collapsed : Visibility.Visible;
                this.PanContentCompleted.Visibility = this.PanListCompleted.Children.Count.Equals(0) ? Visibility.Collapsed : Visibility.Visible;
                this.PanContentNewIssue.Visibility = this.PanListNewIssue.Children.Count.Equals(0) ? Visibility.Collapsed : Visibility.Visible;
                this.PanContentWaitingResponse.Visibility = this.PanListWaitingResponse.Children.Count.Equals(0) ? Visibility.Collapsed : Visibility.Visible;
                this.PanContentProcessing.Visibility = this.PanListProcessing.Children.Count.Equals(0) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void Feedback_Click(object sender, MouseButtonEventArgs e)
        {
            PageOtherLeft.TryFeedback();
        }
    }
}