using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class PageLinkIoi
    {
        public const int RequestVersion = 4;
        public const int IoiVersion = 10; // 由于已关闭更新渠道，在提升 IoiVersion 时必须提升 RequestVersion
        public static string PathIoi = ModBase.PathAppdata + @"联机模块\IOI 联机模块.exe";

        #region 进程管理

        private static string IoiId;
        private static string IoiPassword;
        private static Process IoiProcess = null;
        private static ModBase.LoadState IoiState = ModBase.LoadState.Waiting;

        /// <summary>
    /// 若 Ioi 正在运行，则结束 Ioi 进程，同时初始化状态数据。返回是否关闭了对应进程。
    /// </summary>
        public static bool IoiStop(bool SleepWhenKilled)
        {
            return false;
        }
        /// <summary>
    /// 启动 Ioi，并等待初始化完成后退出运行，同时更新 IoiId 与 IoiPassword。
    /// 正常初始化返回 True，需要更新返回 False，其余情况抛出异常。
    /// 若 Ioi 正在运行，则会先停止其运行。
    /// </summary>
        public static bool IoiStart()
        {
            return false;
        }

        // Ioi 日志
        private static void IoiLogLine(string Content)
        {
        }
        private static int LogLinesCount = 0;
        private static string LastPortsId = ""; // 上一个收到 portssub 的 ID，用于记录端口

        #endregion

        #region 时钟

        // UI 线程刷新
        private static string UserListIdentifyCache = "";
        private static string RoomListIdentifyCache = "";
        public void RefreshUi()
        {
        }

        // 工作线程刷新
        public void RefreshWorker()
        {
        }

        #endregion

        #region 发送请求

        /// <summary>
    /// 发送 Portsub 请求并等待获取控制台端口。进度将从 0 变化至 80%。
    /// </summary>
        private static void SendPortsubRequest(LinkUserIoi User)
        {
        }

        /// <summary>
    /// 向控制台发送 Connect 请求。
    /// </summary>
        private static void SendConnectRequest(LinkUserIoi User)
        {
            var RawJson = new JObject();
            RawJson["version"] = RequestVersion;
            RawJson["name"] = GetPlayerName();
            RawJson["id"] = IoiId;
            RawJson["type"] = "connect";
            User.Send(RawJson);
        }

        /// <summary>
    /// 向控制台发送 Update 请求。
    /// </summary>
        private static void SendUpdateRequest(LinkUserIoi User, int Stage, long Unique = -1)
        {
            if (Unique == -1)
                Unique = ModBase.GetTimeTick();
            var RawJson = new JObject();
            RawJson["name"] = GetPlayerName();
            RawJson["id"] = IoiId;
            RawJson["type"] = "update";
            RawJson["stage"] = Stage;
            RawJson["unique"] = Unique;
            if (Stage < 3)
            {
                var Rooms = new JArray();
                foreach (var Room in RoomListForMe)
                {
                    var RoomObject = new JObject();
                    RoomObject["name"] = Room.DisplayName;
                    RoomObject["port"] = Room.Port;
                    Rooms.Add(RoomObject);
                }
                RawJson["rooms"] = Rooms;
                User.PingPending[Unique] = DateTime.Now;
            }
            User.Send(RawJson);
        }

        /// <summary>
    /// 尝试发送断开请求，并将其从用户列表中移除。
    /// </summary>
        private static void SendDisconnectRequest(LinkUserIoi User, string Message = null, bool IsError = false)
        {
        }

        #endregion

        #region 左边栏操作

        // 刷新连接
        private static void BtnListRefresh_Click(MyIconButton sender, EventArgs e)
        {
        }
        // 断开连接
        private static void BtnListDisconnect_Click(MyIconButton sender, EventArgs e)
        {
        }
        // 复制联机码
        public static void BtnLeftCopy_Click()
        {
        }

        #endregion

        #region 玩家名

        /// <summary>
    /// 获取当前的玩家名。
    /// </summary>
        public static string GetPlayerName()
        {
            // 自动生成玩家名
            if (AutogenPlayerName is null)
            {
                if (IsPlayerNameValid(ModLaunch.McLoginName()))
                {
                    AutogenPlayerName = ModLaunch.McLoginName();
                }
                else
                {
                    AutogenPlayerName = "玩家 " + ((int)Math.Round(ModBase.GetHash(ModBase.UniqueAddress ?? "") % 1048576m)).ToString("x5").ToUpper();
                }
            }
            // 获取玩家自定义的名称
            string CustomName = ModBase.Setup.Get("LinkName").ToString().Trim();
            if (!string.IsNullOrEmpty(CustomName))
            {
                if (IsPlayerNameValid(CustomName))
                {
                    return CustomName.Trim();
                }
                else
                {
                    ModMain.Hint("你所设置的玩家名存在异常，已被重置！", ModMain.HintType.Critical);
                    ModBase.Setup.Set("LinkName", "");
                }
            }
            // 使用自动生成的玩家名
            return AutogenPlayerName;
        }
        private static string AutogenPlayerName = null; // 并非由玩家自定义，而是自动生成的玩家名
                                                        /// <summary>
    /// 检查某个玩家名是否合法。
    /// </summary>
        private static bool IsPlayerNameValid(string Name)
        {
            return true;
        }

        #endregion

        #region 请求核心

        /// <summary>
    /// 启动 Socket 监听核心。
    /// </summary>
        public static void StartSocketListener()
        {
        }

        #endregion

        #region 用户核心

        // 用户基类
        public abstract class LinkUserBase : IDisposable
        {

            // 基础数据
            public int Uuid = ModBase.GetUuid();
            public string Id;
            public string DisplayName;

            // 请求管理
            public Socket Socket = null;
            public void Send(JObject Request)
            {
            }
            public Thread ListenerThread = null;
            public void StartListener()
            {
            }
            public void BindSocket(Socket Socket)
            {
                if (this.Socket is not null)
                    throw new Exception("该用户已经绑定了 Socket");
                this.Socket = Socket;
                StartListener();
            }

            // Ping
            // 0：与 Ping 计算无关，不回应
            // 1：A to B，2：B to A，3：A to B
            public Dictionary<long, DateTime> PingPending = new Dictionary<long, DateTime>();
            public Queue<int> PingRecord = new Queue<int>();

            // 心跳包
            public DateTime LastSend = DateTime.Now;
            public DateTime LastReceive = DateTime.Now;

            // 类型转换
            public LinkUserBase(string Id, string DisplayName)
            {
                this.Id = Id;
                this.DisplayName = DisplayName;
                ModBase.Log("[IOI] 无通信包的新用户对象：" + ToString());
            }
            public LinkUserBase(string Id, string DisplayName, Socket Socket)
            {
                this.Id = Id;
                this.DisplayName = DisplayName;
                this.Socket = Socket;
                ModBase.Log("[IOI] 新用户对象：" + ToString());
                StartListener();
            }
            public override string ToString()
            {
                return DisplayName + " @ " + Id + " #" + Uuid;
            }
            public static implicit operator string(LinkUserBase User)
            {
                return User.ToString();
            }

            // 释放资源
            public bool IsDisposed = false;
            protected virtual void Dispose(bool IsDisposing)
            {
                if (Socket is not null)
                    Socket.Dispose();
                if (ListenerThread is not null && ListenerThread.IsAlive)
                    ListenerThread.Interrupt();
            }
            public void Dispose()
            {
                if (!IsDisposed)
                {
                    IsDisposed = true;
                    Dispose(true);
                }
                GC.SuppressFinalize(this);
            }
        }

        // 用户对象
        public static Dictionary<string, LinkUserIoi> UserList = new Dictionary<string, LinkUserIoi>();
        public class LinkUserIoi : LinkUserBase
        {
            public LinkUserIoi(string Id, string DisplayName, Socket Socket) : base(Id, DisplayName, Socket)
            {
            }
            public LinkUserIoi(string Id, string DisplayName) : base(Id, DisplayName)
            {
            }

            // 基础数据
            public Dictionary<int, string> Ports = new Dictionary<int, string>();
            public List<RoomEntry> Rooms = new List<RoomEntry>();

            // 进度与 UI
            public double Progress = 0d;
            public Thread RelativeThread = null;

            public string GetDescription()
            {
                return Progress < 1d ? "正在连接，" + Math.Round(Progress * 100d) + "%" : "已连接，" + (!PingRecord.Any() ? "检查延迟中" : Math.Round(PingRecord.Average()) + "ms");
            }
            public MyListItem ToListItem()
            {
                var Item = new MyListItem()
                {
                    Title = DisplayName,
                    Height = 42d,
                    Tag = this,
                    Type = MyListItem.CheckType.None,
                    Logo = "pack://application:,,,/images/Blocks/Grass.png"
                };
                // 绑定图标按钮
                var BtnRefresh = new MyIconButton() { Logo = ModBase.Logo.IconButtonRefresh, LogoScale = 0.85d, ToolTip = "刷新", Tag = this };
                BtnRefresh.Click += (_, __) => PageLinkIoi.BtnListRefresh_Click();
                ToolTipService.SetPlacement(BtnRefresh, System.Windows.Controls.Primitives.PlacementMode.Bottom);
                ToolTipService.SetHorizontalOffset(BtnRefresh, (double)-10);
                ToolTipService.SetVerticalOffset(BtnRefresh, 5d);
                ToolTipService.SetInitialShowDelay(BtnRefresh, 200);
                var BtnClose = new MyIconButton() { Logo = ModBase.Logo.IconButtonCross, LogoScale = 0.85d, ToolTip = "断开", Tag = this };
                BtnClose.Click += (_, __) => PageLinkIoi.BtnListDisconnect_Click();
                ToolTipService.SetPlacement(BtnClose, System.Windows.Controls.Primitives.PlacementMode.Bottom);
                ToolTipService.SetHorizontalOffset(BtnClose, (double)-10);
                ToolTipService.SetVerticalOffset(BtnClose, 5d);
                ToolTipService.SetInitialShowDelay(BtnClose, 200);
                Item.Buttons = new[] { BtnRefresh, BtnClose };
                // 刷新并返回
                RefreshUi(Item);
                return Item;
            }
            public void RefreshUi(MyListItem RelatedListItem)
            {
                RelatedListItem.Title = DisplayName;
                RelatedListItem.Info = GetDescription();
                RelatedListItem.Buttons.ElementAtOrDefault(0).Visibility = Progress == 1d ? Visibility.Visible : Visibility.Collapsed;
            }

            // 释放
            protected override void Dispose(bool IsDisposing)
            {
                ModBase.Log("[IOI] 用户资源释放（IOI, " + DisplayName + "）");
                if (RelativeThread is not null && RelativeThread.IsAlive)
                    RelativeThread.Interrupt();
                UserList.Remove(Id);
                base.Dispose(IsDisposing);
            }
        }

        // 房间对象
        private static List<RoomEntry> RoomListForMe = new List<RoomEntry>();

        private List<RoomEntry> GetRoomList()
        {
            var RoomList = new List<RoomEntry>(RoomListForMe);
            for (int i = 0, loopTo = UserList.Count - 1; i <= loopTo; i++)
            {
                if (i > UserList.Count - 1)
                    break;
                RoomList.AddRange(UserList.Values.ElementAtOrDefault(i).Rooms);
            }
            return RoomList;
        }
        public class RoomEntry
        {

            // 基础数据
            public int Port;
            public string DisplayName;
            public LinkUserIoi User = null; // 若 IsOwner = True，则此项为 Nothing
            public bool IsOwner;
            public string Ip
            {
                get
                {
                    if (IsOwner)
                    {
                        return "localhost:" + Port;
                    }
                    else
                    {
                        return User.Ports[Port] + ":" + Port;
                    }
                }
            }

            // 类型转换
            public RoomEntry(int Port, string DisplayName, LinkUserIoi User = null)
            {
                IsOwner = User is null;
                this.User = User;
                this.DisplayName = DisplayName;
                this.Port = Port;
            }
            public override string ToString()
            {
                return DisplayName + " - " + Port + " - " + IsOwner;
            }
            public static implicit operator string(RoomEntry Room)
            {
                return Room.ToString();
            }
            public static int SelectPort(RoomEntry Room)
            {
                return Room.Port;
            }

            // UI
            public string GetDescription()
            {
                if (IsOwner)
                {
                    return "由我创建，端口 " + Port;
                }
                else
                {
                    return "由 " + User.DisplayName + " 创建，端口 " + Port;
                }
            }
            public MyListItem ToListItem()
            {
                var Item = new MyListItem()
                {
                    Title = DisplayName,
                    Height = 42d,
                    Info = GetDescription(),
                    Tag = this,
                    Type = IsOwner ? MyListItem.CheckType.None : MyListItem.CheckType.Clickable,
                    Logo = "pack://application:,,,/images/Blocks/" + (IsOwner ? "GrassPath" : "Grass") + ".png"
                };
                if (IsOwner)
                {
                    // 绑定图标按钮
                    var BtnEdit = new MyIconButton() { Logo = ModBase.Logo.IconButtonEdit, LogoScale = 1d, ToolTip = "修改名称", Tag = this };
                    BtnEdit.Click += (_, __) => PageLinkIoi.BtnRoomEdit_Click();
                    ToolTipService.SetPlacement(BtnEdit, System.Windows.Controls.Primitives.PlacementMode.Bottom);
                    ToolTipService.SetHorizontalOffset(BtnEdit, (double)-22);
                    ToolTipService.SetVerticalOffset(BtnEdit, 5d);
                    ToolTipService.SetInitialShowDelay(BtnEdit, 200);
                    var BtnClose = new MyIconButton() { Logo = ModBase.Logo.IconButtonCross, LogoScale = 0.85d, ToolTip = "关闭", Tag = this };
                    BtnClose.Click += (_, __) => PageLinkIoi.BtnRoomClose_Click();
                    ToolTipService.SetPlacement(BtnClose, System.Windows.Controls.Primitives.PlacementMode.Bottom);
                    ToolTipService.SetHorizontalOffset(BtnClose, (double)-10);
                    ToolTipService.SetVerticalOffset(BtnClose, 5d);
                    ToolTipService.SetInitialShowDelay(BtnClose, 200);
                    Item.Buttons = new[] { BtnEdit, BtnClose };
                }
                else
                {
                    // 绑定点击事件
                    Item.Click += (_, __) => PageLinkIoi.BtnRoom_Click();
                }
                return Item;
            }
            public void RefreshUi(MyListItem RelatedListItem)
            {
                RelatedListItem.Title = DisplayName;
                RelatedListItem.Info = GetDescription();
            }

        }

        #endregion

        // 正向与反向连接
        public static void BtnLeftCreate_Click()
        {
        }
        private static void SendPortsubBack(LinkUserIoi User, int TargetVersion)
        {
        }

        // 创建房间
        private void LinkCreate()
        {
        }
        private static void SendUpdateRequestToAllUsers()
        {
            for (int i = 0, loopTo = UserList.Count - 1; i <= loopTo; i++)
            {
                if (i > UserList.Count - 1)
                    break;
                var User = UserList.Values.ElementAtOrDefault(i);
                if (User.Progress < 1d)
                    continue;
                try
                {
                    SendUpdateRequest(User, 1); // 不需要使用多线程，发送实际会瞬间完成
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "发送全局刷新请求失败（" + User.DisplayName + "）");
                }
            }
        }
        // 修改房间名称
        private static void BtnRoomEdit_Click(MyIconButton sender, EventArgs e)
        {
        }
        // 加入房间
        private static void BtnRoom_Click(MyListItem sender, EventArgs e)
        {
            RoomEntry Room = (RoomEntry)sender.Tag;
            if (ModMain.MyMsgBox("请在多人游戏页面点击直接连接，输入 " + Room.Ip + " 以进入服务器！", "加入房间", "复制地址", "确定") == 1)
            {
                ModBase.ClipboardSet(Room.Ip);
            }
        }
        // 关闭房间
        private static void BtnRoomClose_Click(MyIconButton sender, EventArgs e)
        {
        }

        // 获取数据包
        public static void ReceiveJson(JObject JsonData, Socket NewSocket = null)
        {
        }
        /// <summary>
    /// 从用户列表中移除一位用户。提示信息视作该用户主动离开。
    /// </summary>
        public static void UserRemove(LinkUserIoi User, bool ShowLeaveMessage)
        {
        }

        public static void ModuleStopManually()
        {
        }

    }
}