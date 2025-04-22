using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Makaretu.Nat;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using Open.Nat;

namespace PCL
{

    public class ModLink
    {

        #region MCPing
        public class WorldInfo
        {
            public int Port { get; set; }
            public string VersionName { get; set; }
            public int PlayerMax { get; set; }
            public int PlayerOnline { get; set; }
            public string Description { get; set; }
            public string Favicon { get; set; }

            public override string ToString()
            {
                return $"[MCPing] Version: {VersionName}, Players: {PlayerOnline}/{PlayerMax}, Description: {Description}";
            }
        }

        public class MCPing
        {


            public MCPing(string IP, int Port)
            {
                _IP = IP;
                _Port = Port;
            }

            private string _IP;
            private int _Port;

            /// <summary>
        /// 对疑似 MC 端口进行 MCPing，并返回相关信息
        /// </summary>
            public async System.Threading.Tasks.Task<WorldInfo> GetInfo()
            {
                try
                {
                    // 创建 TCP 客户端并连接到服务器
                    using (var client = new TcpClient(_IP, _Port))
                    {
                        // 向服务器发送握手数据包
                        using (var stream = client.GetStream())
                        {
                            if (!stream.CanWrite || !stream.CanRead)
                                return new WorldInfo();

                            byte[] handshake = BuildHandshake(_IP, _Port);
                            await stream.WriteAsync(handshake, 0, handshake.Length);
                            ModBase.Log($"[MCPing] Send {string.Join(" ", handshake)}", ModBase.LogLevel.Debug);

                            // 向服务器发送查询状态信息的数据包
                            byte[] statusRequest = BuildStatusRequest();
                            await stream.WriteAsync(statusRequest, 0, statusRequest.Length);
                            ModBase.Log($"[MCPing] Send {string.Join(" ", statusRequest)}");

                            // 读取服务器响应的数据
                            var result = new List<byte>();
                            while (true)
                            {
                                var responseBuffer = new byte[1025];
                                int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                                if (bytesRead == 0)
                                    break;
                                result.AddRange(responseBuffer.Take(bytesRead));
                            }

                            ModBase.Log($"[MCPing] Received ({result.Count}) = {string.Join(" ", result)}");
                            // 将响应数据转换为字符串
                            string response = Encoding.UTF8.GetString(result.ToArray(), 0, result.Count);
                            int i = 0;
                            while (i < response.Length && Conversions.ToString(response[i]) != "{" && Conversions.ToString(response[i + 1]) != "\"")
                                i += 1;
                            if (i == response.Length)
                                return new WorldInfo();
                            response = response.Substring(i);
                            ModBase.Log("[MCPing] Server Response: " + response);

                            var j = JObject.Parse(response);

                            return new WorldInfo()
                            {
                                VersionName = (string)j["version"]["name"],
                                PlayerMax = (int)j["players"]["max"],
                                PlayerOnline = (int)j["players"]["online"],
                                Description = (string)j["description"]["text"],
                                Favicon = (string)j["favicon"],
                                Port = _Port
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[MCPing] Error: " + ex.Message);
                }
                return new WorldInfo();
            }


            public byte[] BuildHandshake(string serverIp, int serverPort)
            {
                // 构建握手数据包
                var handshake = new List<byte>();
                handshake.AddRange(GetVarInt(0)); // 数据包 ID 握手包
                handshake.AddRange(GetVarInt(578)); // 协议
                byte[] encodedIP = Encoding.UTF8.GetBytes(serverIp);
                handshake.AddRange(GetVarInt(encodedIP.Length)); // 服务器地址长度
                handshake.AddRange(encodedIP); // 服务器地址
                handshake.AddRange(BitConverter.GetBytes((ushort)serverPort).Reverse()); // 服务器端口
                handshake.AddRange(GetVarInt(1)); // 下一个状态 获取服务器状态

                handshake.InsertRange(0, GetVarInt(handshake.Count));

                return handshake.ToArray();
            }

            public byte[] BuildStatusRequest()
            {
                // 构建状态请求数据包
                var packet = new List<byte>();
                packet.AddRange(GetVarInt(1));
                packet.AddRange(GetVarInt(0));
                return packet.ToArray(); // 状态请求数据包
            }

            private byte[] GetVarInt(int value)
            {
                if (value < 0)
                    return Array.Empty<byte>();
                var result = new List<byte>();
                do
                {
                    byte temp = (byte)(value & 0x7F);
                    value >>= 7;
                    if (value != 0)
                    {
                        temp = (byte)(temp | 0x80);
                    }
                    result.Add(temp);
                }
                while (value != 0);
                return result.ToArray();
            }
        }
        #endregion

        #region 端口查找
        public class PortFinder
        {
            // 定义需要的结构和常量
            [StructLayout(LayoutKind.Sequential)]
            public struct MIB_TCPROW_OWNER_PID
            {
                public int dwState;
                public int dwLocalAddr;
                public int dwLocalPort;
                public int dwRemoteAddr;
                public int dwRemotePort;
                public int dwOwningPid;
            }

            [DllImport("iphlpapi.dll", SetLastError = true)]
            public static extern int GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool bOrder, int ulAf, int TableClass, int reserved);

            public static List<int> GetProcessPort(int dwProcessId)
            {
                var ports = new List<int>();
                var tcpTable = IntPtr.Zero;
                int dwSize = 0;
                int dwRetVal;

                if (dwProcessId == 0)
                {
                    return ports;
                }

                dwRetVal = GetExtendedTcpTable(IntPtr.Zero, ref dwSize, true, 2, 5, 0);
                if (dwRetVal != 0 && dwRetVal != 122) // 122 表示缓冲区不足
                {
                    return ports;
                }

                tcpTable = Marshal.AllocHGlobal(dwSize);
                try
                {
                    if (GetExtendedTcpTable(tcpTable, ref dwSize, true, 2, 5, 0) != 0)
                    {
                        return ports;
                    }

                    var tablePtr = tcpTable;
                    int dwNumEntries = Marshal.ReadInt32(tablePtr);
                    tablePtr = IntPtr.Add(tablePtr, 4);

                    for (int i = 0, loopTo = dwNumEntries - 1; i <= loopTo; i++)
                    {
                        var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(tablePtr);
                        if (row.dwOwningPid == dwProcessId)
                        {
                            ports.Add(row.dwLocalPort >> 8 | (row.dwLocalPort & 0xFF) << 8); // 转换端口号
                        }
                        tablePtr = IntPtr.Add(tablePtr, Marshal.SizeOf<MIB_TCPROW_OWNER_PID>());
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(tcpTable);
                }

                return ports;
            }
        }
        #endregion

        #region UPnP 映射

        public enum UPnPStatusType
        {
            Disabled,
            Enabled,
            Unsupported,
            Failed
        }
        /// <summary>
    /// UPnP 状态，可能值："Disabled", "Enabled", "Unsupported", "Failed"
    /// </summary>
        public static UPnPStatusType UPnPStatus = default;
        public static string UPnPMappingName = "PCL2 CE Link Lobby";
        public static object UPnPDevice = null;
        public static Mapping CurrentUPnPMapping = null;
        public static string UPnPPublicPort = null;

        /// <summary>
    /// 寻找 UPnP 设备并尝试创建一个 UPnP 映射
    /// </summary>
        public static async void CreateUPnPMapping(int LocalPort = 25565, int PublicPort = 10240)
        {
            ModBase.Log($"[UPnP] 尝试创建 UPnP 映射，本地端口：{LocalPort}，远程端口：{PublicPort}，映射名称：{UPnPMappingName}");

            UPnPPublicPort = PublicPort.ToString();
            var UPnPDiscoverer = new NatDiscoverer();
            var cts = new CancellationTokenSource(10000);
            try
            {
                UPnPDevice = await UPnPDiscoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                CurrentUPnPMapping = new Mapping(Protocol.Tcp, LocalPort, PublicPort, UPnPMappingName);
                await ((dynamic)UPnPDevice).CreatePortMapAsync(CurrentUPnPMapping);

                await ((dynamic)UPnPDevice).CreatePortMapAsync(new Mapping(Protocol.Tcp, LocalPort, PublicPort, "PCL2 Link Lobby"));

                UPnPStatus = UPnPStatusType.Enabled;
                ModMain.Hint("UPnP 映射已创建");
            }
            catch (NatDeviceNotFoundException NotFoundEx)
            {
                UPnPStatus = UPnPStatusType.Unsupported;
                CurrentUPnPMapping = null;
                ModBase.Log("[UPnP] 找不到可用的 UPnP 设备");
            }
            catch (Exception ex)
            {
                UPnPStatus = UPnPStatusType.Failed;
                CurrentUPnPMapping = null;
                ModBase.Log("[UPnP] UPnP 映射创建失败: " + ex.ToString());
            }
        }

        /// <summary>
    /// 尝试移除现有 UPnP 映射记录
    /// </summary>
        public static async void RemoveUPnPMapping()
        {
            ModBase.Log($"[UPnP] 尝试移除 UPnP 映射，本地端口：{CurrentUPnPMapping.PrivatePort}，远程端口：{CurrentUPnPMapping.PublicPort}，映射名称：{UPnPMappingName}");

            try
            {
                await ((dynamic)UPnPDevice).DeletePortMapAsync(CurrentUPnPMapping);

                UPnPStatus = UPnPStatusType.Disabled;
                CurrentUPnPMapping = null;
                ModBase.Log("[UPnP] UPnP 映射移除成功");
            }
            catch (Exception ex)
            {
                UPnPStatus = UPnPStatusType.Failed;
                CurrentUPnPMapping = null;
                ModBase.Log("[UPnP] UPnP 映射移除失败: " + ex.ToString());
            }
        }

        #endregion

        #region Minecraft 实例探测
        public static async System.Threading.Tasks.Task<List<WorldInfo>> MCInstanceFinding()
        {
            // Java 进程 PID 查询
            var PIDLookupResult = new List<string>();
            var JavaNames = new List<string>();
            JavaNames.Add("java");
            JavaNames.Add("javaw");

            foreach (var java in JavaNames)
            {
                Process[] JavaProcesses = Process.GetProcessesByName(java);
                ModBase.Log($"[MCDetect] 找到 {java} 进程 {JavaProcesses.Length} 个");

                if (JavaProcesses is null || JavaProcesses.Length == 0)
                {
                    continue;
                }
                else
                {
                    foreach (var p in JavaProcesses)
                    {
                        ModBase.Log("[MCDetect] 检测到 Java 进程，PID: " + p.Id.ToString());
                        PIDLookupResult.Add(p.Id.ToString());
                    }
                }
            }

            var res = new List<WorldInfo>();
            try
            {
                if (!PIDLookupResult.Any())
                    return res;
                var ports = PortFinder.GetProcessPort(int.Parse(PIDLookupResult.First()));
                ModBase.Log($"[MCDetect] 获取到端口数量 {ports.Count}");
                foreach (var port in ports)
                {
                    ModBase.Log($"[MCDetect] 找到疑似端口，开始验证：{port}");
                    var test = new MCPing("127.0.0.1", port);
                    var info = await test.GetInfo();
                    if (!string.IsNullOrWhiteSpace(info.VersionName))
                    {
                        ModBase.Log($"[MCDetect] 端口 {port} 为有效 Minecraft 世界");
                        res.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[MCDetect] 获取端口信息错误", ModBase.LogLevel.Debug);
            }
            return res;
        }
        #endregion

        #region NAT 穿透
        public static List<LeasedEndpoint> NATEndpoints = null;
        /// <summary>
    /// 尝试进行 NAT 映射
    /// </summary>
    /// <param name="localPort">本地端口</param>
        public static async void CreateNATTranversal(string LocalPort)
        {
            ModBase.Log($"开始尝试进行 NAT 穿透，本地端口 {LocalPort}");
            try
            {
                NATEndpoints = new List<LeasedEndpoint>(); // 寻找 NAT 设备
                foreach (var nat in NatDiscovery.GetNats())
                {
                    var lease = await nat.CreatePublicEndpointAsync(ProtocolType.Tcp, Conversions.ToInteger(LocalPort));
                    var endpoint = new LeasedEndpoint(lease);
                    NATEndpoints.Add(endpoint);
                    PageLinkLobby.PublicIPPort = endpoint.ToString();
                    ModBase.Log($"NAT 穿透完成，公网地址: {endpoint}");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log("尝试进行 NAT 穿透失败: " + ex.ToString());
            }

        }

        /// <summary>
    /// 移除 NAT 映射
    /// </summary>
        public static void RemoveNATTranversal()
        {
            ModBase.Log("开始尝试移除 NAT 映射");
            try
            {
                foreach (var endpoint in NATEndpoints)
                    endpoint.Dispose();
                ModBase.Log("NAT 映射已移除");
            }
            catch (Exception ex)
            {
                ModBase.Log("尝试移除 NAT 映射失败: " + ex.ToString());
            }
        }
        #endregion

        #region EasyTier

        public static Process ETProcess = new Process();
        public static string ETNetworkName = "PCLCELobby";
        public static string ETNetworkSecret = "PCLCELobbyDefault";
        public static string ETServer = null; // "tcp://public.easytier.cn:11010"
        public static string ETPath = ModBase.PathTemp + @"EasyTier\easytier-windows-x86_64";
        public static bool IsETRunning = false;

        public static void LaunchEasyTier(bool IsHost, string Name = "PCLCELobby", string Secret = "PCLCELobbyDefault")
        {
            try
            {
                ETProcess = new Process();
                ETProcess.StartInfo = new ProcessStartInfo()
                {
                    FileName = $@"{ETPath}\easytier-core.exe",
                    WorkingDirectory = ETPath,
                    Arguments = ETProcess.StartInfo.Arguments,
                    ErrorDialog = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };
                ETProcess.EnableRaisingEvents = true;
                if (!File.Exists(ETProcess.StartInfo.FileName))
                {
                    ModBase.Log("[Link] EasyTier 不存在，开始下载");
                    DownloadEasyTier(true, IsHost, Name, Secret);
                }
                ModBase.Log($"[Link] EasyTier 路径: {ETProcess.StartInfo.FileName}");

                if (IsHost)
                {
                    ETNetworkName = "PCLCELobby";
                    for (int index = 1; index <= 8; index++) // 生成 8 位随机编号
                        ETNetworkName += ModBase.RandomInteger(0, 9).ToString();
                    ModBase.Log($"[Link] 本机作为创建者创建大厅，EasyTier 网络名称: {ETNetworkName}, 是否自定义网络密钥: {!(Secret == "PCLCELobbyDefault")}");
                    ETProcess.StartInfo.Arguments = $"-i 10.114.51.41 --network-name {ETNetworkName} --network-secret {ETNetworkSecret} -p {ETServer} --no-tun"; // 创建者
                }
                else
                {
                    ETNetworkName = "PCLCELobby" + Name;
                    ModBase.Log($"[Link] 本机作为加入者加入大厅，EasyTier 网络名称: {ETNetworkName}");
                    ETProcess.StartInfo.Arguments = $"-d --network-name {ETNetworkName} --network-secret {ETNetworkSecret} -p {ETServer}"; // 加入者
                    ETProcess.StartInfo.Verb = "runas";
                }

                // 创建防火墙规则
                // Dim FirewallProcess As New Process With {
                // .StartInfo = New ProcessStartInfo With {
                // .Verb = "runas",
                // .FileName = "cmd",
                // .Arguments = $"/c netsh advfirewall firewall add rule name=""PCLCE Lobby - EasyTier"" dir=in action=allow program=""{ETPath}\easytier-core.exe"" protocol=tcp localport={FrmLinkLobby.LocalPort}"
                // }
                // }

                ETProcess.StartInfo.Arguments += $" --enable-kcp-proxy --latency-first --use-smoltcp";
                // AddHandler ETProcess.Exited, AddressOf LaunchEasyTier
                ModBase.Log($"[Link] 启动 EasyTier");
                // Log($"[Link] 启动 EasyTier, 参数: {ETProcess.StartInfo.Arguments}")
                ModBase.RunInUi(() => ModMain.FrmLinkLobby.LabFinishId.Text = ETNetworkName.Replace("PCLCELobby", ""));
                ETProcess.Start();
                IsETRunning = true;
                Thread.Sleep(2000);
            }
            // Log(ETProcess.StandardOutput.ReadToEnd())
            // Log(ETProcess.StandardError.ReadToEnd())
            // If ETProcess.ExitCode = 0 Then
            // Log("[Link] EasyTier 进程已结束，正常退出")
            // End If

            catch (Exception ex)
            {
                ModBase.Log("[Link] 尝试启动 EasyTier 时遇到问题: " + ex.ToString());
                ETProcess = null;
            }
        }

        public static void DownloadEasyTier(bool LaunchAfterDownload = false, bool IsHost = false, string Name = "PCLCELobby", string Secret = "PCLCELobbyDefault")
        {
            string DlTargetPath = ModBase.PathTemp + @"EasyTier\EasyTier.zip";
            // 构造步骤加载器
            // 下载

            // 启动
            // LoaderTaskbarAdd(Loader)
            // FrmMain.BtnExtraDownload.ShowRefresh()
            // FrmMain.BtnExtraDownload.Ribble()
            ModBase.RunInNewThread(() => { try { var Loaders = new List<ModLoader.LoaderBase>(); var Address = new List<string>(); Address.Add("https://ghfast.top/https://github.com/EasyTier/EasyTier/releases/download/v2.2.2/easytier-windows-x86_64-v2.2.2.zip"); Address.Add("https://github.com/EasyTier/EasyTier/releases/download/v2.2.2/easytier-windows-x86_64-v2.2.2.zip"); Loaders.Add(new ModNet.LoaderDownload("下载 EasyTier", new List<ModNet.NetFile>() { new ModNet.NetFile(Address.ToArray(), DlTargetPath, new ModBase.FileChecker(MinSize: 1024 * 64)) }) { ProgressWeight = 15d }); Loaders.Add(new ModLoader.LoaderTask<int, int>("解压文件", () => ModBase.ExtractFile(DlTargetPath, ModBase.PathTemp + "EasyTier"))); Loaders.Add(new ModLoader.LoaderTask<int, int>("清理文件", () => File.Delete(DlTargetPath))); if (LaunchAfterDownload) { Loaders.Add(new ModLoader.LoaderTask<int, int>("启动 EasyTier", () => LaunchEasyTier(IsHost, Name, Secret))); } var Loader = new ModLoader.LoaderCombo<JObject>("EasyTier 下载", Loaders); Loader.Start(); } catch (Exception ex) { ModBase.Log(ex, "[Link] 下载 EasyTier 依赖文件失败", ModBase.LogLevel.Hint); ModMain.Hint("下载 EasyTier 依赖文件失败，请检查网络连接", ModMain.HintType.Critical); } });
        }

        public static void ExitEasyTier()
        {
            try
            {
                ModBase.Log("[Link] 停止 EasyTier");
                ETProcess.Kill();
                IsETRunning = false;
                ETProcess = null;
            }
            catch (Exception ex)
            {
                ModBase.Log("[Link] 尝试停止 EasyTier 进程时遇到问题: " + ex.ToString());
                ETProcess = null;
            }
        }

        #endregion

    }
}