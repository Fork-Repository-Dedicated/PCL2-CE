using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public static class ModEvent
    {

        public static void TryStartEvent(string Type, string Data)
        {
            if (string.IsNullOrWhiteSpace(Type))
                return;
            string[] RealData = new[] { "" };
            if (Data is not null)
                RealData = Data.Split("|");
            StartEvent(Type, RealData);
        }
        public static void StartEvent(string Type, string[] Data)
        {
            try
            {
                ModBase.Log("[Control] 执行自定义事件：" + Type + ", " + Data.Join(", "));
                switch (Type ?? "")
                {

                    case "打开网页":
                        {
                            Data[0] = Data[0].Replace(@"\", "/");
                            if (!Data[0].Contains("://") || Data[0].StartsWithF("file", true)) // 为了支持更多协议（#2200）
                            {
                                ModMain.MyMsgBox("EventData 必须为一个网址。" + Constants.vbCrLf + "如果想要启动程序，请将 EventType 改为 打开文件。", "事件执行失败");
                                return;
                            }
                            ModMain.Hint("正在开启中，请稍候……");
                            ModBase.OpenWebsite(Data[0]);
                            break;
                        }

                    case "打开文件":
                    case "打开帮助":
                    case "执行命令":
                        {
                            ModBase.RunInThread(() =>
                {
                    try
                    {
                        // 确认实际路径
                        string[] ActualPaths = GetEventAbsoluteUrls(Data[0], Type);
                        string Location = ActualPaths[0];
                        string WorkingDir = ActualPaths[1];
                        ModBase.Log($"[Control] 打开类自定义事件实际路径：{Location}，工作目录：{WorkingDir}");
                        // 执行
                        if (Type == "打开帮助")
                        {
                            PageOtherHelp.EnterHelpPage(Location);
                        }
                        else
                        {
                            if (Conversions.ToBoolean(!(bool)ModBase.Setup.Get("HintCustomCommand")))
                            {
                                switch (ModMain.MyMsgBox("即将执行：" + Location + (Data.Length >= 2 ? " " + Data[1] : "") + Constants.vbCrLf + "请在确认该操作没有安全隐患后继续。", "执行确认", "继续", "继续且今后不再要求确认", "取消"))
                                {
                                    case 2:
                                        {
                                            ModBase.Setup.Set("HintCustomCommand", true);
                                            break;
                                        }
                                    case 3:
                                        {
                                            return;
                                        }
                                }
                            }
                            var Info = new ProcessStartInfo()
                            {
                                Arguments = Data.Length >= 2 ? Data[1] : "",
                                FileName = Location,
                                WorkingDirectory = ModBase.ShortenPath(WorkingDir)
                            };
                            Process.Start(Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "执行打开类自定义事件失败", ModBase.LogLevel.Msgbox);
                    }
                });
                            break;
                        }

                    case "启动游戏":
                        {
                            if (Data[0] == @"\current")
                            {
                                if (ModMinecraft.McVersionCurrent is null)
                                {
                                    ModMain.Hint("请先选择一个 Minecraft 版本！", ModMain.HintType.Critical);
                                    return;
                                }
                                else
                                {
                                    Data[0] = ModMinecraft.McVersionCurrent.Name;
                                }
                            }
                            if (ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions() { ServerIp = Data.Length >= 2 ? Data[1] : null, Version = new ModMinecraft.McVersion(Data[0]) }))
                            {
                                ModMain.Hint("正在启动 " + Data[0] + "……");
                            }

                            break;
                        }

                    case "复制文本":
                        {
                            ModBase.ClipboardSet(Data.Join("|"));
                            break;
                        }

                    case "刷新主页":
                        {
                            ModMain.FrmLaunchRight.ForceRefresh();
                            if (string.IsNullOrEmpty(Data[0]))
                                ModMain.Hint("已刷新主页！", ModMain.HintType.Finish);
                            break;
                        }

                    case "刷新帮助":
                        {
                            PageOtherLeft.RefreshHelp();
                            break;
                        }

                    case "今日人品":
                        {
                            PageOtherTest.Jrrp();
                            break;
                        }

                    case "内存优化":
                        {
                            ModBase.RunInThread(() => PageOtherTest.MemoryOptimize(true));
                            break;
                        }

                    case "清理垃圾":
                        {
                            ModBase.RunInThread(() => PageOtherTest.RubbishClear());
                            break;
                        }

                    case "弹出窗口":
                        {
                            ModMain.MyMsgBox(Data[1].Replace(@"\n", Constants.vbCrLf), Data[0].Replace(@"\n", Constants.vbCrLf));
                            break;
                        }

                    case "切换页面":
                        {
                            ModMain.FrmMain.PageChange((FormMain.PageStackData)ModBase.Val(Data[0]), (FormMain.PageSubType)Math.Round(ModBase.Val(Data[1])));
                            break;
                        }

                    case "导入整合包":
                    case "安装整合包":
                        {
                            ModBase.RunInUi(() => ModModpack.ModpackInstall());
                            break;
                        }

                    case "下载文件":
                        {
                            Data[0] = Data[0].Replace(@"\", "/");
                            if (!(Data[0].StartsWithF("http://", true) || Data[0].StartsWithF("https://", true)))
                            {
                                ModMain.MyMsgBox("EventData 必须为以 http:// 或 https:// 开头的网址。" + Constants.vbCrLf + "PCL 不支持其他乱七八糟的下载协议。", "事件执行失败");
                                return;
                            }
                            try
                            {
                                switch (Data.Length)
                                {
                                    case 1:
                                        {
                                            PageOtherTest.StartCustomDownload(Data[0], ModBase.GetFileNameFromPath(Data[0]));
                                            break;
                                        }
                                    case 2:
                                        {
                                            PageOtherTest.StartCustomDownload(Data[0], Data[1]);
                                            break;
                                        }

                                    default:
                                        {
                                            PageOtherTest.StartCustomDownload(Data[0], Data[1], Data[2]);
                                            break;
                                        }
                                }
                            }
                            catch
                            {
                                PageOtherTest.StartCustomDownload(Data[0], "未知");
                            }

                            break;
                        }

                    default:
                        {
                            ModMain.MyMsgBox("未知的事件类型：" + Type + Constants.vbCrLf + "请检查事件类型填写是否正确，或者 PCL 是否为最新版本。", "事件执行失败");
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "事件执行失败", ModBase.LogLevel.Msgbox);
            }
        }

        /// <summary>
    /// 返回自定义事件的绝对 Url。实际返回 {绝对 Url, WorkingDir}。
    /// 失败会抛出异常。
    /// </summary>
        public static string[] GetEventAbsoluteUrls(string RelativeUrl, string EventType)
        {

            // 网页确认
            if (RelativeUrl.StartsWithF("http", true))
            {
                if (ModBase.RunInUi())
                {
                    throw new Exception("能打开联网帮助页面的 MyListItem 必须手动设置 Title、Info 属性！");
                }
                // 获取文件名
                string RawFileName;
                try
                {
                    RawFileName = ModBase.GetFileNameFromPath(RelativeUrl);
                    if (!RawFileName.EndsWithF(".json", true))
                        throw new Exception("未指向 .json 后缀的文件");
                }
                catch (Exception ex)
                {
                    throw new Exception("联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！" + Constants.vbCrLf + "例如：" + Constants.vbCrLf + " - https://www.baidu.com/test.json（填写这个路径）" + Constants.vbCrLf + " - https://www.baidu.com/test.xaml（同时也需要包含这个文件）", ex);
                }
                // 下载文件
                string LocalTemp = ModMain.RequestTaskTempFolder() + RawFileName;
                ModBase.Log("[Event] 转换网络资源：" + RelativeUrl + " -> " + LocalTemp);
                try
                {
                    ModNet.NetDownloadByClient(RelativeUrl, LocalTemp);
                    ModNet.NetDownloadByClient(RelativeUrl.Replace(".json", ".xaml"), LocalTemp.Replace(".json", ".xaml"));
                }
                catch (Exception ex)
                {
                    throw new Exception("下载指定的文件失败！" + Constants.vbCrLf + "注意，联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！" + Constants.vbCrLf + "例如：" + Constants.vbCrLf + " - https://www.baidu.com/test.json（填写这个路径）" + Constants.vbCrLf + " - https://www.baidu.com/test.xaml（同时也需要包含这个文件）", ex);
                }
                RelativeUrl = LocalTemp;
            }
            RelativeUrl = RelativeUrl.Replace("/", @"\").ToLower().TrimStart('\\');

            // 确认实际路径
            string Location;
            string WorkingDir = ModBase.Path + "PCL";
            ModMain.HelpExtract();
            if (RelativeUrl.Contains(@":\"))
            {
                // 绝对路径
                Location = RelativeUrl;
                ModBase.Log("[Control] 自定义事件中由绝对路径" + EventType + "：" + Location);
            }
            else if (File.Exists(ModBase.Path + @"PCL\" + RelativeUrl))
            {
                // 相对 PCL 文件夹的路径
                Location = ModBase.Path + @"PCL\" + RelativeUrl;
                ModBase.Log("[Control] 自定义事件中由相对 PCL 文件夹的路径" + EventType + "：" + Location);
            }
            else if (File.Exists(ModBase.Path + @"PCL\Help\" + RelativeUrl))
            {
                // 相对 PCL 本地帮助文件夹的路径
                Location = ModBase.Path + @"PCL\Help\" + RelativeUrl;
                WorkingDir = ModBase.Path + @"PCL\Help\";
                ModBase.Log("[Control] 自定义事件中由相对 PCL 本地帮助文件夹的路径" + EventType + "：" + Location);
            }
            else if (EventType == "打开帮助" && File.Exists(ModBase.PathTemp + @"Help\" + RelativeUrl))
            {
                // 相对 PCL 自带帮助文件夹的路径
                Location = ModBase.PathTemp + @"Help\" + RelativeUrl;
                WorkingDir = ModBase.PathTemp + @"Help\";
                ModBase.Log("[Control] 自定义事件中由相对 PCL 自带帮助文件夹的路径" + EventType + "：" + Location);
            }
            else if (EventType == "打开文件" || EventType == "执行命令")
            {
                // 直接使用原有路径启动程序
                Location = RelativeUrl;
                ModBase.Log("[Control] 自定义事件中直接" + EventType + "：" + Location);
            }
            else
            {
                // 打开帮助，但是格式不对劲
                throw new FileNotFoundException("未找到 EventData 指向的本地 xaml 文件：" + RelativeUrl, RelativeUrl);
            }

            return new[] { Location, WorkingDir };
        }

    }
}