using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using static PCL.ModLink;

namespace PCL
{
    public partial class PageLinkLobby
    {
        public const char RequestVersion = '2';

        // 记录的启动情况
        public static bool IsHost = default;
        public static string LobbyServerLink = null;

        static PageLinkLobby()
        {


            #region 加载步骤

            InitLoader = new ModLoader.LoaderCombo<int>("HiPer 初始化", new[] { new ModLoader.LoaderTask<int, int>("检查网络环境", InitCheck) { ProgressWeight = 0.5d } });
        }

        public PageLinkLobby()
        {
            this.Initialized += (_, __) => LoaderInit();
            this.Loaded += (_, __) => OnLoaded();
            this.PageEnter += PageLinkLobby_OnPageEnter;
        }

        #region 初始化

        // 加载器初始化
        private void LoaderInit()
        {
            this.PageLoaderInit(this.Load, this.PanLoad, this.PanContent, this.PanAlways, InitLoader, AutoRun: false);
            // 注册自定义的 OnStateChanged
            InitLoader.OnStateChangedUi += OnLoadStateChanged;
        }

        private bool IsLoad = false;
        private void OnLoaded()
        {
            // FormMain.EndProgramForce(Result.Aborted)
            if (IsLoad)
                return;
            IsLoad = true;
            // 启动监视线程
            // If Not IsWatcherStarted Then RunInNewThread(AddressOf WatcherThread, "Hiper Watcher")
        }

        private static ModLoader.LoaderCombo<int> _InitLoader;

        public static ModLoader.LoaderCombo<int> InitLoader
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _InitLoader;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                _InitLoader = value;
            }
        }
        private static void InitCheck(ModLoader.LoaderTask<int, int> Task)
        {
        }

        #endregion

        #region 进程管理

        private static ModBase.LoadState _HiperState = ModBase.LoadState.Waiting;
        public static ModBase.LoadState HiperState
        {
            get
            {
                return _HiperState;
            }
            set
            {
                _HiperState = value;
                ModBase.RunInUi(() => { if (ModMain.FrmLinkLeft is not null) ModMain.FrmLinkLeft.ItemLobby.Buttons.ElementAtOrDefault(0).Visibility = HiperState == ModBase.LoadState.Finished || HiperState == ModBase.LoadState.Loading ? Visibility.Visible : Visibility.Collapsed; });
            }
        }

        private static string HiperIp = null;
        private static int HiperProcessId = -1;
        private static int McbProcessId = -1;
        private static DateTime HiperCertTime = DateTime.Now;

        /// <summary>
    /// 启动程序，并等待初始化完成后退出运行，同时更新 HiperIp。
    /// 若启动失败，则会直接抛出异常。
    /// 若程序正在运行，则会先停止其运行。
    /// </summary>
        public static void HiperStart(ModLoader.LoaderTask<int, int> Task)
        {
        }

        // Hiper 日志
        private static void HiperLogLine(string Content, ModLoader.LoaderTask<int, int> Task)
        {
        }
        private static string PossibleFailReason = null;

        #endregion

        #region 监视线程

        // 主 Timer 线程
        private bool IsWatcherStarted = false;
        private void StartWatcherThread()
        {
            ModBase.RunInNewThread(() =>
                {
                    if (IsHost)
                    {
                        ModBase.Log($"[Link] 本机角色：大厅创建者，隐藏 Ping 信息和连接类型信息");
                        ModBase.RunInUi(() =>
         {
                            this.SplitLineBeforePing.Visibility = Visibility.Collapsed;
                            this.BtnFinishPing.Visibility = Visibility.Collapsed;
                            this.SplitLineBeforeType.Visibility = Visibility.Collapsed;
                            this.BtnConnectType.Visibility = Visibility.Collapsed;
                        });
                        return;
                    }
                    ModBase.Log("[Link] 本机角色：加入者，开始获取 Ping 信息和连接类型信息");
                    ModBase.Log("[Link] 启动 EasyTier 监视");
                    IsWatcherStarted = true;
                    while (ETProcess is not null)
                    {
                        var ETCliProcess = new Process()
                        {
                            StartInfo = new ProcessStartInfo()
                            {
                                FileName = $@"{ETPath}\easytier-cli.exe",
                                WorkingDirectory = ModLink.ETPath,
                                Arguments = ETProcess.StartInfo.Arguments,
                                ErrorDialog = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                RedirectStandardInput = true,
                                StandardOutputEncoding = Encoding.UTF8
                            },
                            EnableRaisingEvents = true
                        };
                        string ETCliOutput = null;
                        string Ping = null;
                        string ConnectType = null;
                        string ConnectTypeOriginal = null;

                        ETCliProcess.StartInfo.Arguments = "peer";
                        ETCliProcess.Start();
                        ETCliOutput = ETCliProcess.StandardOutput.ReadToEnd();
                        // Log($"[Link] 获取到 EasyTier Cli 信息: {vbCrLf}" + ETCliOutput)
                        if (!ETCliOutput.Contains("10.114.51.41/24"))
                        {
                            ModBase.Log("[Link] 未找到大厅创建者 IP, 判定该大厅未被创建");
                            ModMain.Hint("该大厅不存在", ModMain.HintType.Critical);
                            ModBase.RunInUi(() => CurrentSubpage = Subpages.PanSelect);
                            ExitEasyTier();
                            return;
                        }

                        Ping = ETCliOutput.Split("│ 10.114.51.41/24 │")[1].Split("│")[2].Trim().Split(".")[0];
                        var PingSender = new Ping();
                        PingReply PingReplied = null;
                        string PingRtt = null;
                        try
                        {
                            PingReplied = PingSender.Send("10.114.51.41");
                            if (PingReplied.Status == IPStatus.Success)
                            {
                                PingRtt = PingReplied.RoundtripTime.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log("[Ping] 进行 Ping 测试失败: " + ex.ToString());
                        }
                        // Log($"[Link] 与大厅创建者之间的 Ping 值: {PingRtt} ms")

                        ConnectTypeOriginal = ETCliOutput.Split("│ 10.114.51.41/24 │")[1].Split("│")[1].Trim();
                        if (ConnectTypeOriginal.Contains("peer") || ConnectTypeOriginal.Contains("p2p"))
                        {
                            ConnectType = "P2P";
                        }
                        else if (ConnectTypeOriginal.Contains("relay"))
                        {
                            ConnectType = "中继";
                        }
                        else if (ConnectTypeOriginal.Contains("Local"))
                        {
                            ConnectType = "本机";
                        }
                        // Log("[Link] 与大厅创建者的连接类型为... 本机？你是怎么做到的.jpg")
                        else
                        {
                            ConnectType = "未知";
                        }
                        // Log($"[Link] 与大厅创建者的连接类型原始输出: {ConnectTypeOriginal}, 判定为类型: {ConnectType}")
                        ModBase.RunInUi(() =>
         {
                            this.LabFinishPing.Text = PingRtt + "ms";
                            this.SplitLineBeforePing.Visibility = Visibility.Visible;
                            this.BtnFinishPing.Visibility = Visibility.Visible;
                            this.LabConnectType.Text = ConnectType;
                            this.SplitLineBeforeType.Visibility = Visibility.Visible;
                            this.BtnConnectType.Visibility = Visibility.Visible;
                        });
                        // ETCliProcess.Kill()
                        Thread.Sleep(15000);
                    }
                    ModBase.Log("[Link] EasyTier 监视线程已退出");
                    IsWatcherStarted = false;
                }, "EasyTier Status Watcher", ThreadPriority.BelowNormal);
        }

        // 每 1 秒执行的 Timer
        private void WatcherTimer1()
        {
            if (HiperState != ModBase.LoadState.Finished)
                return;
            ModBase.RunInUi(() =>
                {
                    // 网络质量
                    int QualityScore = 0;
                    // QualityScore -= Math.Ceiling((Math.Min(0, 600) + Math.Min(PingNodes, 600)) / 80)
                    switch (QualityScore)
                    {
                        case var @case when @case >= -1:
                            {
                                this.LabFinishQuality.Text = "优秀";
                                break;
                            }
                        case var case1 when case1 >= -2:
                            {
                                this.LabFinishQuality.Text = "优良";
                                break;
                            }
                        case var case2 when case2 >= -3:
                            {
                                this.LabFinishQuality.Text = "良好";
                                break;
                            }
                        case var case3 when case3 >= -5:
                            {
                                this.LabFinishQuality.Text = "一般";
                                break;
                            }
                        case var case4 when case4 >= -7:
                            {
                                this.LabFinishQuality.Text = "较差";
                                break;
                            }

                        default:
                            {
                                this.LabFinishQuality.Text = "很差";
                                break;
                            }
                    }
                    // Ping
                    if (HostPing != -1)
                    {
                        if (ModMain.FrmLinkLobby is not null && ModMain.FrmLinkLobby.LabFinishPing.IsLoaded)
                        {
                            ModMain.FrmLinkLobby.LabFinishPing.Text = HostPing + "ms";
                        }
                    }
                });
        }
        // 每 15 秒执行的 Timer
        private static int HostPing = -1;
        private void WatcherTimer15()
        {
        }

        #endregion

        #region PanSelect | 种类选择页面

        public string LocalPort = null;
        // 创建房间
        private void BtnSelectCreate_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            LocalPort = ModMain.MyMsgBoxInput("输入端口号", HintText: "例如：25565");
            if (string.IsNullOrEmpty(LocalPort))
                return;
            IsHost = true;
            ModBase.RunInNewThread(() =>
                {
                    // CreateNATTranversal(LocalPort)
                    LaunchEasyTier(true);
                    Thread.Sleep(1000);
                    StartWatcherThread();
                });
            // If ETProcess IsNot Nothing Then LabFinishId.Text = ETNetworkName.Replace("PCLCELobby", "")
            // ModLink.CreateUPnPMapping(LocalPort)
            CurrentSubpage = Subpages.PanFinish;
        }
        private void RoomCreate(int Port)
        {
            // 记录信息
            IsHost = true;
            // 启动
            InitLoader.Start(IsForceRestart: true);
        }

        public string JoinedLobbyId = null;
        // 加入房间
        private void BtnSelectJoin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!ModBase.IsAdmin())
            {
                ModMain.MyMsgBox($"现阶段如果作为加入方加入大厅，需要以管理员身份启动 PCL。{Constants.vbCrLf}请退出启动器，然后右键点击程序，选择 ⌈以管理员身份运行⌋，然后继续操作。", "需要管理员权限", "我知道了", ForceWait: true);
                return;
            }
            JoinedLobbyId = ModMain.MyMsgBoxInput("输入大厅编号", HintText: "例如：01509230");
            if (string.IsNullOrEmpty(JoinedLobbyId))
                return;
            ModBase.RunInNewThread(() =>
                {
                    LaunchEasyTier(false, JoinedLobbyId);
                    Thread.Sleep(1000);
                    StartWatcherThread();
                });
            CurrentSubpage = Subpages.PanFinish;
            // If ETProcess IsNot Nothing Then LabFinishId.Text = ETNetworkName.Replace("PCLCELobby", "")
        }
        private void RoomJoin(string Ip, int Port)
        {
            // 记录信息
            IsHost = false;
            // 启动
            InitLoader.Start(IsForceRestart: true);
        }

        #endregion

        #region PanLoad | 加载中页面

        // 承接状态切换的 UI 改变
        private void OnLoadStateChanged(ModLoader.LoaderBase Loader, ModBase.LoadState NewState, ModBase.LoadState OldState)
        {
        }
        private static string LoadStep = "准备初始化";
        private static void SetLoadDesc(string Intro, string Step)
        {
            ModBase.Log("[Hiper] 连接步骤：" + Intro);
            LoadStep = Step;
            ModBase.RunInUiWait(() =>
                {
                    if (ModMain.FrmLinkLobby is null || !ModMain.FrmLinkLobby.LabLoadDesc.IsLoaded)
                        return;
                    ModMain.FrmLinkLobby.LabLoadDesc.Text = Intro;
                    ModMain.FrmLinkLobby.UpdateProgress();
                });
        }

        // 承接重试
        private void CardLoad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!(InitLoader.State == ModBase.LoadState.Failed))
                return;
            InitLoader.Start(IsForceRestart: true);
        }

        // 取消加载
        private void CancelLoad()
        {
            if (InitLoader.State == ModBase.LoadState.Loading)
            {
                CurrentSubpage = Subpages.PanSelect;
                InitLoader.Abort();
            }
            else
            {
                InitLoader.State = ModBase.LoadState.Waiting;
            }
        }

        // 进度改变
        private void UpdateProgress(double Value = -1)
        {
            if (Value == -1)
                Value = InitLoader.Progress;
            double DisplayingProgress = this.ColumnProgressA.Width.Value;
            if (Math.Round(Value - DisplayingProgress, 3) == 0d)
                return;
            if (DisplayingProgress > Value)
            {
                this.ColumnProgressA.Width = new GridLength(Value, GridUnitType.Star);
                this.ColumnProgressB.Width = new GridLength(1d - Value, GridUnitType.Star);
                ModAnimation.AniStop("Hiper Progress");
            }
            else
            {
                double NewProgress = Value == 1d ? 1d : (Value - DisplayingProgress) * 0.2d + DisplayingProgress;
                ModAnimation.AniStart(new[] { ModAnimation.AaGridLengthWidth(this.ColumnProgressA, NewProgress - this.ColumnProgressA.Width.Value, 300, Ease: new ModAnimation.AniEaseOutFluent()), ModAnimation.AaGridLengthWidth(this.ColumnProgressB, 1d - NewProgress - this.ColumnProgressB.Width.Value, 300, Ease: new ModAnimation.AniEaseOutFluent()) }, "Hiper Progress");
            }
        }
        private void CardResized()
        {
            this.RectProgressClip.Rect = new Rect(0d, 0d, this.CardLoad.ActualWidth, 12d);
        }

        #endregion

        #region PanFinish | 加载完成页面

        public static string PublicIPPort = null;

        // 复制 IP
        private void BtnFinishId_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ModBase.ClipboardSet(this.LabFinishId.Text);
        }

        // 退出
        private void BtnFinishExit_Click(object sender, EventArgs e)
        {
            if (ModMain.MyMsgBox("你确定要退出大厅吗？", "确认退出", "确定", "取消", IsWarn: true) == 1)
            {
                CurrentSubpage = Subpages.PanSelect;
                ExitEasyTier();
                // RemoveNATTranversal()
                // ModLink.RemoveUPnPMapping()
                // LocalPort = Nothing
                return;
            }
        }

        // 复制联机码
        private void BtnFinishCopy_Click(object sender, EventArgs e)
        {
            ModBase.ClipboardSet(ETNetworkName.Replace("PCLCELobby", ""));
        }

        // Ping 房主
        private void BtnFinishPing_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) // Handles BtnFinishPing.MouseLeftButtonUp
        {
            this.LabFinishPing.Text = "检测中";
            if (TaskPingHost.State == ModBase.LoadState.Loading)
                return;
            TaskPingHost.Start(true, IsForceRestart: true);
        }
        private static ModLoader.LoaderTask<bool, int> TaskPingHost = new ModLoader.LoaderTask<bool, int>("HiPer Ping Host", (Task) => HostPing = -1);

        #endregion

        #region 子页面管理

        public enum Subpages
        {
            PanSelect,
            PanFinish
        }
        private Subpages _CurrentSubpage = Subpages.PanSelect;
        public Subpages CurrentSubpage
        {
            get
            {
                return _CurrentSubpage;
            }
            set
            {
                if (_CurrentSubpage == value)
                    return;
                _CurrentSubpage = value;
                ModBase.Log("[Link] 子页面更改为 " + ModBase.GetStringFromEnum(value));
                this.PageOnContentExit();
            }
        }

        private void PageLinkLobby_OnPageEnter()
        {
            ModMain.FrmLinkLobby.PanSelect.Visibility = CurrentSubpage == Subpages.PanSelect ? Visibility.Visible : Visibility.Collapsed;
            ModMain.FrmLinkLobby.PanFinish.Visibility = CurrentSubpage == Subpages.PanFinish ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void HiperExit(bool ExitToCertPage)
        {
            ModBase.Log("[Hiper] 要求退出 Hiper（当前加载器状态为 " + ModBase.GetStringFromEnum(InitLoader.State) + "）");
            if (InitLoader.State == ModBase.LoadState.Loading)
                InitLoader.Abort();
            if (InitLoader.State == ModBase.LoadState.Failed)
                InitLoader.State = ModBase.LoadState.Waiting;
            ModBase.RunInUi(() =>
                {
                    if (ModMain.FrmLinkLobby is null || !ModMain.FrmLinkLobby.IsLoaded)
                        return;
                    ModMain.FrmLinkLobby.CurrentSubpage = Subpages.PanSelect;
                    ModMain.FrmLinkLobby.PageOnContentExit();
                });
        }

        #endregion

    }
}