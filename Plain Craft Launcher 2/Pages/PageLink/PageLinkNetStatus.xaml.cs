using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Makaretu.Nat;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using static PCL.ModLink;
using STUN;

namespace PCL
{
    public partial class PageLinkNetStatus
    {
        public int NetQualityCounter = 0;

        public string NATType = null;
        public string NATTypeFriendly = null;
        public string UPnPStatusFriendly = null;

        public enum IPSupportStatus
        {
            Open,
            Supported,
            Unsupported
        }
        public IPSupportStatus IPv4Status = default;
        public string IPv4StatusFriendly = null;
        public IPSupportStatus IPv6Status = default;
        public string IPv6StatusFriendly = null;

        public static string PublicIPv4Address = null;

        public void NetStatusTest()
        {
            if (Convert.ToBoolean(ModBase.ReadReg("LinkFirstTimeNetTest", "True")))
            {
                ModMain.MyMsgBox($"你似乎是第一次打开 PCL 的联机模块。为了正常运行联机模块，PCL 接下来会申请 Windows 防火墙权限。{Constants.vbCrLf}{Constants.vbCrLf}请在接下来出现的弹窗中点击 \"允许\"。", "首次联机提示", "我知道了", ForceWait: true);
                var TestTcpListener = TcpListener.Create(Conversions.ToInteger("5600"));
                TestTcpListener.Start();
                Thread.Sleep(200);
                TestTcpListener.Stop();
                ModBase.WriteReg("LinkFirstTimeNetTest", "False");
            }

            ModBase.RunInUi(() =>
                {
                    ModMain.FrmLinkLeft.NetStatusUpdate("正在检测...");

                    this.LabNetStatusNATTitle.Text = "NAT 类型：正在检测";
                    this.LabNetStatusNATDesc.Text = "正在检测 NAT 类型，这可能需要几秒钟";

                    this.LabNetStatusPingTitle.Text = "Ping 值：正在检测";
                    this.LabNetStatusPingDesc.Text = "正在检测 Ping 值，这可能需要几秒钟";

                    this.LabNetStatusIPv6Title.Text = "IP 版本：正在检测";
                    this.LabNetStatusIPv6Desc.Text = "正在检测 IP 版本，这可能需要几秒钟";
                });

            ModBase.RunInNewThread(() =>
                {
                    NATTest();
                    PingTest();
                    IPTest();
                    ChangeNetQualityText();
                });
        }
        public void NATTest()
        {
            // IPv4 NAT 类型检测
            string STUNServerDomain = "stun.miwifi.com"; // 指定 STUN 服务器
            ModBase.Log("[STUN] 指定的 STUN 服务器: " + STUNServerDomain);
            try
            {
                string STUNServerIP = Dns.GetHostAddresses(STUNServerDomain)[0].ToString(); // 解析 STUN 服务器 IP
                ModBase.Log("[STUN] 解析目标 STUN 服务器 IP: " + STUNServerIP);
                var STUNServerEndPoint = new IPEndPoint(IPAddress.Parse(STUNServerIP), 3478); // 设置 IPEndPoint

                STUNClient.ReceiveTimeout = 500; // 设置超时
                ModBase.Log("[STUN] 开始进行 NAT 测试");
                var STUNTestResult = STUNClient.Query(STUNServerEndPoint, STUNQueryType.ExactNAT, true); // 进行 STUN 测试

                if (!(STUNTestResult.QueryError == STUNQueryError.Success))
                {
                    ModBase.Log("[STUN] NAT 测试失败");
                    NATType = "TestFailed";
                    throw new Exception();
                    return;
                }

                NATType = STUNTestResult.NATType.ToString();
                ModBase.Log("[STUN] NAT 检测完成，本地 NAT 类型为: " + NATType);

                if (NATType == "OpenInternet")
                    IPv4Status = (IPSupportStatus)Conversions.ToInteger("Public");
            }
            catch (Exception ex)
            {
                ModBase.Log("[STUN] 进行 NAT 测试失败: " + ex.ToString());
                NATType = "TestFailed";
            }

            // UPnP 映射测试
            CreateUPnPMapping();
            Thread.Sleep(500); // 因为异步不会处理直接硬等 0.5s
            if (UPnPStatus == UPnPStatusType.Enabled)
            {
                UPnPStatusFriendly = "已启用";
                RemoveUPnPMapping();
                Thread.Sleep(500);
                if (UPnPStatus == UPnPStatusType.Failed)
                {
                    UPnPStatusFriendly = "异常";
                }
            }
            else if (UPnPStatus == UPnPStatusType.Unsupported)
            {
                UPnPStatusFriendly = "不兼容";
            }
            else
            {
                UPnPStatusFriendly = "异常";
            }

            ModBase.RunInUi(() => ChangeNATText());
        }
        public void PingTest()
        {
            var PingSender = new Ping();
            PingReply PingReplied = null;
            string PingRtt = null;
            string PingServerDomain = "www.baidu.com"; // 指定 Ping 服务器
            ModBase.Log("[Ping] Ping 目标服务器: " + PingServerDomain);
            try
            {
                string PingServerIP = Dns.GetHostAddresses(PingServerDomain)[0].ToString(); // 解析 Ping 服务器 IP
                ModBase.Log("[Ping] 解析 Ping 目标服务器 IP: " + PingServerIP);

                ModBase.Log("[Ping] 开始进行 Ping 测试");
                PingReplied = PingSender.Send(PingServerIP);
                if (PingReplied.Status == IPStatus.Success)
                {
                    PingRtt = PingReplied.RoundtripTime.ToString();
                }

                ModBase.Log($"[Ping] Ping 测试完成，Ping 值: {PingRtt} ms");

                if (Conversions.ToDouble(PingRtt) >= 100d)
                {
                    NetQualityCounter -= 1;
                }
            }
            catch (Exception ex)
            {
                ModBase.Log("[Ping] 进行 Ping 测试失败: " + ex.ToString());
            }

            ModBase.RunInUi(() =>
                {
                    this.LabNetStatusPingTitle.Text = $"Ping 值：{PingRtt} ms";
                    this.LabNetStatusPingDesc.Text = $"{(Conversions.ToDouble(PingRtt) >= 100d ? "当前网络延迟较高，可能会影响联机体验" : "当前网络延迟较低")}{Constants.vbCrLf}Ping 值可以反映你的网络延迟水平，一般来说越低越好。";
                });
        }
        public void IPTest()
        {
            // IP 检测
            ModBase.Log("[IP] 开始进行 IP 检测");
            bool TaskCompleted = false;

            ModBase.RunInNewThread(() =>
                {
                    bool V4PubDetected = default;
                    bool V6PubDetected = default;

                    try
                    {
                        foreach (var ip in NatDiscovery.GetIPAddresses())
                        {
                            if (!V4PubDetected && ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
                            {
                                if (ip.IsPublic())
                                {
                                    ModMain.Hint("Public v4: " + ip.ToString());
                                    IPv4Status = IPSupportStatus.Open;
                                    ModBase.Log("[IP] 检测到 IPv4 公网地址");
                                    V4PubDetected = true;
                                    continue;
                                }
                                else if (ip.IsPrivate())
                                {
                                    IPv4Status = IPSupportStatus.Supported;
                                    ModBase.Log("[IP] 检测到 IPv4 支持");
                                    continue;
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            if (!V6PubDetected && ip.AddressFamily == AddressFamily.InterNetworkV6) // IPv6
                            {
                                if (ip.IsPublic())
                                {
                                    IPv6Status = IPSupportStatus.Open;
                                    ModBase.Log("[IP] 检测到 IPv6 公网地址");
                                    V6PubDetected = true;
                                    continue;
                                }
                                else if (ip.IsPrivate())
                                {
                                    IPv6Status = IPSupportStatus.Supported;
                                    ModBase.Log("[IP] 检测到 IPv6 支持");
                                    continue;
                                }
                                else if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Teredo || ip.IsIPv4MappedToIPv6)
                                {
                                    continue;
                                }
                            }
                        }

                        if (IPv4Status == default(int))
                            IPv4Status = IPSupportStatus.Unsupported; // 致敬每一位勇士
                        if (IPv6Status == default(int))
                            IPv6Status = IPSupportStatus.Unsupported; // 如果轮了一圈出来还是没 IPv6 地址，那就是没有

                        ModBase.Log($"[IP] IP 检测完成，IPv4 支持情况: {IPv4Status}，IPv6 支持情况: {IPv6Status}");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log("[IP] 检测 IP 版本支持失败: " + ex.ToString());
                    }
                    finally
                    {
                        TaskCompleted = true;
                    }
                }, "IPStatus");

            while (!TaskCompleted)
                Thread.Sleep(200);

            ModBase.Log($"[IP] IP 检测完成，IPv4 支持情况: {IPv4Status.ToString()}，IPv6 支持情况: {IPv6Status.ToString()}");

            ModBase.RunInUi(() => ChangeIPText());
        }
        public void ChangeNetQualityText()
        {
            Thread.Sleep(200);
            string NetQualityText = null;
            if (NetQualityCounter >= 4)
            {
                NetQualityText = "网络优秀";
            }
            else if (NetQualityCounter >= 2)
            {
                NetQualityText = "网络良好";
            }
            else
            {
                NetQualityText = "网络较差";
            }

            ModBase.Log($"[Link] 最终网络质量指数: {NetQualityCounter}，判定网络质量: {NetQualityText}");

            ModBase.RunInUi(() => ModMain.FrmLinkLeft.NetStatusUpdate(NetQualityText));
        }
        public void ChangeNATText()
        {
            string NATTypeDesc = null;

            switch (NATType ?? "")
            {
                case var @case when @case == "OpenInternet":
                    {
                        NATTypeFriendly = "开放";
                        NATTypeDesc = "当前网络环境不会影响联机体验，适合作为大厅创建者";
                        NetQualityCounter += 3;
                        break;
                    }
                case var case1 when case1 == "FullCone":
                    {
                        NATTypeFriendly = "中等（完全圆锥）";
                        NATTypeDesc = "当前网络环境不会影响联机体验，适合作为大厅创建者";
                        NetQualityCounter += 3;
                        break;
                    }
                case var case2 when case2 == "Restricted":
                    {
                        NATTypeFriendly = "中等（受限圆锥）";
                        NATTypeDesc = "这可能会影响您的联机体验";
                        NetQualityCounter += 2;
                        break;
                    }
                case var case3 when case3 == "PortRestricted":
                    {
                        NATTypeFriendly = "中等（端口受限圆锥）";
                        NATTypeDesc = "部分路由器和防火墙设置可能会影响您的联机体验";
                        NetQualityCounter += 1;
                        break;
                    }
                case var case4 when case4 == "Symmetric":
                    {
                        NATTypeFriendly = "严格（对称）";
                        NATTypeDesc = "这将严重影响您的联机体验";
                        break;
                    }
                case var case5 when case5 == "SymmetricUDPFirewall":
                    {
                        NATTypeFriendly = "严格（对称 + 防火墙）";
                        NATTypeDesc = "这将严重影响您的联机体验";
                        break;
                    }
                case var case6 when case6 == "Unspecified":
                    {
                        NATTypeFriendly = "未知";
                        NATTypeDesc = "这将严重影响您的联机体验";
                        break;
                    }
                case var case7 when case7 == "TestFailed":
                    {
                        NATTypeFriendly = "测试失败";
                        NATTypeDesc = "这将严重影响您的联机体验，请检查你的防火墙和互联网连接";
                        break;
                    }
            }

            if (!(NATType == "TestFailed"))
            {
                this.LabNetStatusNATTitle.Text = "NAT 类型：" + NATTypeFriendly.Substring(0, 2) + (UPnPStatusFriendly == "已启用" ? " + UPnP" : "");
                this.LabNetStatusNATDesc.Text = $"当前 NAT 类型为 {NATTypeFriendly}，UPnP {UPnPStatusFriendly}{Constants.vbCrLf}{NATTypeDesc}";
            }
            else
            {
                this.LabNetStatusNATTitle.Text = "NAT 类型：测试失败";
                this.LabNetStatusNATDesc.Text = $"NAT 测试失败，UPnP {UPnPStatusFriendly}{Constants.vbCrLf}{NATTypeDesc}";
            }
        }
        public void ChangeIPText()
        {
            switch (IPv4Status)
            {
                case var @case when @case == IPSupportStatus.Open:
                    {
                        IPv4StatusFriendly = "公网";
                        NetQualityCounter += 2;
                        break;
                    }
                case var case1 when case1 == IPSupportStatus.Supported:
                    {
                        IPv4StatusFriendly = "支持";
                        break;
                    }
                case var case2 when case2 == IPSupportStatus.Unsupported:
                    {
                        IPv4StatusFriendly = "不支持";
                        break;
                    }
            }

            switch (IPv6Status)
            {
                case var case3 when case3 == IPSupportStatus.Open:
                    {
                        IPv6StatusFriendly = "公网";
                        NetQualityCounter += 2;
                        break;
                    }
                case var case4 when case4 == IPSupportStatus.Supported:
                    {
                        IPv6StatusFriendly = "支持";
                        NetQualityCounter += 1;
                        break;
                    }
                case var case5 when case5 == IPSupportStatus.Unsupported:
                    {
                        IPv6StatusFriendly = "不支持";
                        break;
                    }
            }

            string IPStatusTitle = null;
            string IPStatusDesc = null;
            if (!(IPv6Status == IPSupportStatus.Unsupported) && !(IPv4Status == IPSupportStatus.Unsupported))
            {
                IPStatusTitle = "IPv6 优先";
                IPStatusDesc = "你的网络环境支持 IPv6，这会让连接更加顺利。";
            }
            else if (IPv6Status == IPSupportStatus.Unsupported && !(IPv4Status == IPSupportStatus.Unsupported))
            {
                IPStatusTitle = "仅 IPv4";
                IPStatusDesc = "支持 IPv6 很可能会让连接更加顺利。你可以尝试调整光猫和路由器设置以获取 IPv6 地址。";
            }
            else if (!(IPv6Status == IPSupportStatus.Unsupported) && IPv4Status == IPSupportStatus.Unsupported)
            {
                IPStatusTitle = "仅 IPv6";
                IPStatusDesc = "你的网络环境仅支持 IPv6，你可真勇敢...";
            }
            else
            {
                IPStatusTitle = "你真的连上网了么...";
                IPStatusDesc = "你的网络既不支持 IPv4 也不支持 IPv6，请检查你的防火墙和互联网连接。";
            }

            this.LabNetStatusIPv6Title.Text = "IP 版本：" + IPStatusTitle;
            this.LabNetStatusIPv6Desc.Text = $"本地 IPv4 状态: {IPv4StatusFriendly}，本地 IPv6 状态: {IPv6StatusFriendly}。{Constants.vbCrLf}{IPStatusDesc}";
        }
    }
}