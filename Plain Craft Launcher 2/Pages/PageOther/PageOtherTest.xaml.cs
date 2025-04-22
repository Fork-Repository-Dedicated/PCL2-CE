using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{

    public partial class PageOtherTest
    {
        public PageOtherTest()
        {
            Loaded += (object sender, global::System.Windows.RoutedEventArgs e) => MeLoaded();
            this.InitializeComponent();
        }
        private void MeLoaded()
        {
            this.BtnDownloadStart.IsEnabled = false;

            this.TextDownloadFolder.Text = Conversions.ToString(ModBase.Setup.Get("CacheDownloadFolder"));
            this.TextDownloadFolder.Validate();

            if (!string.IsNullOrEmpty(this.TextDownloadFolder.ValidateResult) || string.IsNullOrEmpty(this.TextDownloadFolder.Text))
            {
                this.TextDownloadFolder.Text = ModBase.Path + @"PCL\MyDownload\";
            }

            this.TextDownloadFolder.Validate();
            this.TextDownloadName.Validate();
        }
        private void StartButtonRefresh()
        {
            this.BtnDownloadStart.IsEnabled = string.IsNullOrEmpty(this.TextDownloadFolder.ValidateResult) && string.IsNullOrEmpty(this.TextDownloadUrl.ValidateResult) && string.IsNullOrEmpty(this.TextDownloadName.ValidateResult);

            this.BtnDownloadOpen.IsEnabled = string.IsNullOrEmpty(this.TextDownloadFolder.ValidateResult);
        }
        private void SaveCacheDownloadFolder()
        {
            ModBase.Setup.Set("CacheDownloadFolder", this.TextDownloadFolder.Text);
            this.TextDownloadName.Validate();
        }
        private static void DownloadState(ModLoader.LoaderCombo<int> Loader)
        {
            try
            {
                switch (Loader.State)
                {
                    case ModBase.LoadState.Finished:
                        {
                            ModMain.Hint(Loader.Name + "完成！", ModMain.HintType.Finish, true);
                            Interaction.Beep();
                            break;
                        }
                    case ModBase.LoadState.Failed:
                        {
                            ModBase.Log(Loader.Error, Loader.Name + "失败", ModBase.LogLevel.Msgbox, "出现错误");
                            Interaction.Beep();
                            break;
                        }
                    case ModBase.LoadState.Aborted:
                        {
                            ModMain.Hint(Loader.Name + "已取消！", ModMain.HintType.Info, true);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
            }
        }

        public static void StartCustomDownload(string Url, string FileName, string Folder = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Folder))
                {
                    Folder = ModBase.SelectSaveFile("选择文件保存位置", FileName, null, null);
                    if (!Folder.Contains(@"\"))
                    {
                        return;
                    }
                    if (Folder.EndsWith(FileName))
                    {
                        Folder = Strings.Mid(Folder, 1, Folder.Length - FileName.Length);
                    }
                }
                Folder = Folder.Replace("/", @"\").TrimEnd(new char[] { '\\' }) + @"\";
                try
                {
                    Directory.CreateDirectory(Folder);
                    ModBase.CheckPermissionWithException(Folder);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "访问文件夹失败（" + Folder + "）", ModBase.LogLevel.Hint, "出现错误");
                    return;
                }
                ModBase.Log("[Download] 自定义下载文件名：" + FileName, ModBase.LogLevel.Normal, "出现错误");
                ModBase.Log("[Download] 自定义下载文件目标：" + Folder, ModBase.LogLevel.Normal, "出现错误");
                int uuid = ModBase.GetUuid();
                var loaderDownload = new ModNet.LoaderDownload("自定义下载文件：" + FileName + " ", new List<ModNet.NetFile>() { new ModNet.NetFile(new string[] { Url }, Folder + FileName, null, true) });
                var loaderCombo = new ModLoader.LoaderCombo<int>("自定义下载 (" + uuid.ToString() + ") ", new ModLoader.LoaderBase[] { loaderDownload }) { OnStateChanged = (_) => PageOtherTest.DownloadState() };
                loaderCombo.Start();
                LoaderTaskbarAdd<int>(loaderCombo);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "开始自定义下载失败", ModBase.LogLevel.Feedback, "出现错误");
            }
        }
        public static void Jrrp()
        {
            ModMain.Hint("为便于维护，社区版中不包含百宝箱功能……");
        }
        public static void RubbishClear()
        {
            ModBase.RunInUi(() => { if (!(ModMain.FrmOtherTest == null) && !(ModMain.FrmOtherTest.BtnClear == null)) { ModMain.FrmOtherTest.BtnClear.IsEnabled = false; } });

            // 清理的文件数量
            // 所有 Minecraft 文件夹


            // 寻找所有 Minecraft 文件夹

            // 删除 Minecraft 的缓存
            // 删除日志和崩溃报告并计数

            // 删除 Natives 文件

            // 删除 PCL 的缓存


            ModBase.RunInNewThread(() => { try { if (!ModWatcher.HasRunningMinecraft | ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading) { if (ModNet.HasDownloadingTask()) { ModMain.Hint("请在所有下载任务完成后再来清理吧……"); return; } if (!ModMinecraft.McFolderList.Any()) { ModMinecraft.McFolderListLoader.Start(); } ModBase.Log(string.Format("[Test] 当前缓存文件夹：{0}，默认缓存文件夹：{1}", ModBase.PathTemp, System.IO.Path.GetTempPath() + @"PCL\")); if (string.Compare(ModBase.PathTemp, System.IO.Path.GetTempPath() + @"PCL\") == 0) { if (Conversions.ToBoolean(Operators.ConditionalCompareObjectLessEqual(ModBase.Setup.Get("HintClearRubbish"), 2, false))) { if (ModMain.MyMsgBox("即将清理游戏日志、错误报告、缓存等文件。" + Constants.vbCrLf + "虽然应该没人往这些地方放重要文件，但还是问一下，是否确认继续？" + Constants.vbCrLf + Constants.vbCrLf + "在完成清理后，PCL 将自动重启。", "清理确认", "确定", "取消") == 2) { return; } ModBase.Setup.Set("HintClearRubbish", Operators.AddObject(ModBase.Setup.Get("HintClearRubbish"), 1)); } } else if (ModMain.MyMsgBox("即将清理游戏日志、错误报告、缓存等文件。" + Constants.vbCrLf + Constants.vbCrLf + "你已将缓存文件夹手动修改为：" + ModBase.PathTemp + Constants.vbCrLf + "清理过程中，将删除该文件夹中的所有内容，且无法恢复。请确认其中没有除了 PCL 缓存以外的重要文件！" + Constants.vbCrLf + Constants.vbCrLf + "在完成清理后，PCL 将自动重启。", "清理确认", "确定", "取消") == 2) { return; } int num = 0; var cleanMcFolderList = new List<DirectoryInfo>(); if (!ModMinecraft.McFolderList.Any()) { ModMinecraft.McFolderListLoader.WaitForExit(); } foreach (ModMinecraft.McFolder mcFolder in ModMinecraft.McFolderList) { cleanMcFolderList.Add(new DirectoryInfo(mcFolder.Path)); var dirInfo = new DirectoryInfo(mcFolder.Path + "versions"); if (dirInfo.Exists) { foreach (DirectoryInfo item in dirInfo.EnumerateDirectories()) cleanMcFolderList.Add(item); } } foreach (DirectoryInfo dirInfo in cleanMcFolderList) { num += ModBase.DeleteDirectory(dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"crash-reports\", true); num += ModBase.DeleteDirectory(dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"logs\", true); foreach (FileInfo fileInfo in dirInfo.EnumerateFiles("*")) { if (fileInfo.Name.StartsWith("hs_err_pid") || fileInfo.Name.EndsWith(".log") || fileInfo.Name == "WailaErrorOutput.txt") { fileInfo.Delete(); num += 1; } } foreach (DirectoryInfo dirInfo2 in dirInfo.EnumerateDirectories()) { if ((dirInfo2.Name ?? "") == (dirInfo2.Name + "-natives" ?? "") || dirInfo2.Name == "natives-windows-x86_64") { num += ModBase.DeleteDirectory(dirInfo2.FullName, true); } } } num += ModBase.DeleteDirectory(ModBase.PathTemp, true); num += ModBase.DeleteDirectory(ModBase.OsDrive + @"ProgramData\PCL\", true); ModMain.MyMsgBox(string.Format("清理了 {0} 个文件！", num) + Constants.vbCrLf + "PCL 即将自动重启……", "缓存已清理", "确定", "", "", false, true, true, null, null, null); Process.Start(new ProcessStartInfo(ModBase.PathWithName)); FormMain.EndProgramForce(ModBase.ProcessReturnValues.Success); } ModMain.Hint("请先关闭所有运行中的游戏……"); } catch (Exception ex) { ModBase.Log(ex, "清理垃圾失败", ModBase.LogLevel.Hint, "出现错误"); } finally { ModBase.RunInUiWait(() => { if (!(ModMain.FrmOtherTest == null) && !(ModMain.FrmOtherTest.BtnClear == null)) { ModMain.FrmOtherTest.BtnClear.IsEnabled = true; } }); } }, "Rubbish Clear");
        }
        [StructLayout(LayoutKind.Sequential)]
        private class TokenPrivileges
        {
            public int PrivilegeCount = 1;
            public LUID Luid;
            public int Attributes;
        }
        private struct LUID
        {
            public int LowPart;
            public int HighPart;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_FILECACHE_INFORMATION
        {
            public UIntPtr CurrentSize;
            public UIntPtr PeakSize;
            public uint PageFaultCount;
            public UIntPtr MinimumWorkingSet;
            public UIntPtr MaximumWorkingSet;
            public UIntPtr CurrentSizeIncludingTransitionInPages;
            public UIntPtr PeakSizeIncludingTransitionInPages;
            public uint TransitionRePurposeCount;
            public uint Flags;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_COMBINE_INFORMATION_EX
        {
            public IntPtr Handle;
            public UIntPtr PagesCombined;
            public uint Flags;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetCurrentProcess();
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern bool OpenProcessToken(HandleRef ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue([MarshalAs(UnmanagedType.LPTStr)] string lpSystemName, [MarshalAs(UnmanagedType.LPTStr)] string lpName, out LUID lpLuid);
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern bool AdjustTokenPrivileges(HandleRef TokenHandle, bool DisableAllPrivileges, TokenPrivileges NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);
        [DllImport("ntdll.dll", CharSet = CharSet.Ansi)]
        private static extern uint NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);
        private static object IsMemoryOptimizing;
        public static void MemoryOptimize(bool ShowHint)
        {
            if (Conversions.ToBoolean(IsMemoryOptimizing))
            {
                if (ShowHint)
                {
                    ModMain.Hint("内存优化尚未结束，请稍等！", ModMain.HintType.Info, true);
                    return;
                }
            }
            else
            {
                IsMemoryOptimizing = true;
                long num;
                if (ModBase.IsAdmin())
                {
                    num = (long)My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory;
                    try
                    {
                        MemoryOptimizeInternal(ShowHint);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "内存优化失败", ShowHint ? ModBase.LogLevel.Hint : ModBase.LogLevel.Debug, "出现错误");
                        return;
                    }
                    finally
                    {
                        IsMemoryOptimizing = false;
                    }
                    num = Convert.ToInt64(decimal.Subtract(new decimal(My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory), new decimal(num)));
                }
                else
                {
                    ModBase.Log("[Test] 没有管理员权限，将以命令行方式进行内存优化");
                    try
                    {
                        num = ModBase.RunAsAdmin("--memory") * 1024L;
                    }
                    catch (Exception ex2)
                    {
                        ModBase.Log(ex2, "命令行形式内存优化失败");
                        if (ShowHint)
                        {
                            ModMain.Hint(string.Concat(new string[] { "获取管理员权限失败，请尝试右键 PCL，选择 ", Conversions.ToString(ModBase.vbLQ), "以管理员身份运行", Conversions.ToString(ModBase.vbRQ), "！" }), ModMain.HintType.Critical, true);
                        }
                        return;
                    }
                    finally
                    {
                        IsMemoryOptimizing = false;
                    }
                    if (num < 0L)
                    {
                        return;
                    }
                }
                string MemAfter = ModBase.GetString((long)My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory);
                ModBase.Log(string.Format("[Test] 内存优化完成，可用内存改变量：{0}，大致剩余内存：{1}", ModBase.GetString(num), MemAfter));
                if (num > 0L)
                {
                    if (ShowHint)
                    {
                        ModMain.Hint(string.Format("内存优化完成，可用内存增加了 {0}，目前剩余内存 {1}！", ModBase.GetString((long)Math.Round(Math.Round(num * 0.8d))), MemAfter), ModMain.HintType.Finish, true);
                        return;
                    }
                }
                else if (ShowHint)
                {
                    ModMain.Hint(string.Format("内存优化完成，已经优化到了最佳状态，目前剩余内存 {0}！", MemAfter), ModMain.HintType.Info, true);
                }
            }
        }
        public static void MemoryOptimizeInternal(bool ShowHint)
        {
            if (!ModBase.IsAdmin())
            {
                throw new Exception("内存优化功能需要管理员权限！" + Constants.vbCrLf + "如果需要自动以管理员身份启动 PCL，可以右键 PCL，打开 属性 → 兼容性 → 以管理员身份运行此程序。");
            }
            ModBase.Log("[Test] 获取内存优化权限");

            // 提权部分
            try
            {
                var processId = GetCurrentProcess();
                LUID luid1 = default;
                LUID luid2 = default;
                IntPtr hToken = (IntPtr)0;
                if (PageOtherTest.OpenProcessToken(new HandleRef(null, processId), 32, ref hToken))
                {
                    string arglpSystemName = null;
                    string arglpName = "SeProfileSingleProcessPrivilege";
                    PageOtherTest.LookupPrivilegeValue(ref arglpSystemName, ref arglpName, ref luid1);
                    string arglpSystemName1 = null;
                    string arglpName1 = "SeIncreaseQuotaPrivilege";
                    PageOtherTest.LookupPrivilegeValue(ref arglpSystemName1, ref arglpName1, ref luid2);

                    var tokenPrivileges1 = new TokenPrivileges();
                    tokenPrivileges1.Luid = luid1;
                    tokenPrivileges1.Attributes = 2;
                    var tokenPrivileges2 = new TokenPrivileges();
                    tokenPrivileges2.Luid = luid2;
                    tokenPrivileges2.Attributes = 2;

                    AdjustTokenPrivileges(new HandleRef(null, hToken), false, tokenPrivileges1, 0, IntPtr.Zero, IntPtr.Zero);
                    AdjustTokenPrivileges(new HandleRef(null, hToken), false, tokenPrivileges2, 0, IntPtr.Zero, IntPtr.Zero);

                    CloseHandle(hToken);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("获取内存优化权限失败（错误代码：{0}）", Marshal.GetLastWin32Error()));
            }

            if (ShowHint)
            {
                ModMain.Hint("正在进行内存优化……", ModMain.HintType.Info, true);
            }

            // 内存优化部分
            string NowType = "None";
            try
            {
                int info;
                var scfi = default(SYSTEM_FILECACHE_INFORMATION);
                var combineInfoEx = default(MEMORY_COMBINE_INFORMATION_EX);
                GCHandle _gcHandle;

                NowType = "MemoryEmptyWorkingSets";
                info = 2;
                _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
                NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
                _gcHandle.Free();
                NowType = "SystemFileCacheInformation";
                scfi.MaximumWorkingSet = (UIntPtr)uint.MaxValue;
                scfi.MinimumWorkingSet = (UIntPtr)uint.MaxValue;
                _gcHandle = GCHandle.Alloc(scfi, GCHandleType.Pinned);
                NtSetSystemInformation(81, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(scfi));
                _gcHandle.Free();
                NowType = "MemoryFlushModifiedList";
                info = 3;
                _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
                NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
                _gcHandle.Free();
                NowType = "MemoryPurgeStandbyList";
                info = 4;
                _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
                NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
                _gcHandle.Free();
                NowType = "MemoryPurgeLowPriorityStandbyList";
                info = 5;
                _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
                NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
                _gcHandle.Free();
                NowType = "SystemRegistryReconciliationInformation";
                NtSetSystemInformation(155, new IntPtr(default(int)), 0);
                NowType = "SystemCombinePhysicalMemoryInformation";
                _gcHandle = GCHandle.Alloc(combineInfoEx, GCHandleType.Pinned);
                NtSetSystemInformation(130, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(combineInfoEx));
                _gcHandle.Free();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("内存优化操作 {0} 失败（错误代码：{1}）", NowType));
            }

        }
        public static string GetRandomCave()
        {
            return "为便于维护，社区版中不包含百宝箱功能……";
        }
        public static string GetRandomHint()
        {
            return "为便于维护，社区版中不包含百宝箱功能……";
        }
        public static string GetRandomPresetHint()
        {
            return "为便于维护，社区版中不包含百宝箱功能……";
        }

        private void TextDownloadUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(this.TextDownloadName.Text) || string.IsNullOrEmpty(this.TextDownloadUrl.Text))
                {
                    return;
                }
                this.TextDownloadName.Text = ModBase.GetFileNameFromPath(WebUtility.UrlDecode(this.TextDownloadUrl.Text));
            }
            catch
            {
            }
        }

        private void MyTextButton_Click(object sender, EventArgs e)
        {
            string text = ModBase.SelectFolder("选择文件夹");
            if (!string.IsNullOrEmpty(text))
            {
                this.TextDownloadFolder.Text = text;
            }
        }

        private void BtnDownloadOpen_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string text = this.TextDownloadFolder.Text;
                Directory.CreateDirectory(text);
                Process.Start(text);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "打开下载文件夹失败", ModBase.LogLevel.Debug, "出现错误");
            }
        }

        private void BtnDownloadStart_Click(object sender, MouseButtonEventArgs e)
        {
            PageOtherTest.StartCustomDownload(this.TextDownloadUrl.Text, this.TextDownloadName.Text, this.TextDownloadFolder.Text);
            this.TextDownloadUrl.Text = "";
            this.TextDownloadUrl.Validate();
            this.TextDownloadUrl.ForceShowAsSuccess();
            this.TextDownloadName.Text = "";
            this.TextDownloadName.Validate();
            this.TextDownloadName.ForceShowAsSuccess();
            StartButtonRefresh();
        }

        private void TextDownloadUrl_ValidateChanged(object sender, EventArgs e)
        {
            StartButtonRefresh();
        }
        private void TextDownloadFolder_ValidateChanged(object sender, EventArgs e)
        {
            StartButtonRefresh();
        }
        private void TextDownloadName_ValidateChanged(object sender, EventArgs e)
        {
            StartButtonRefresh();
        }
        private void BtnClear_Click(object sender, MouseButtonEventArgs e)
        {
            RubbishClear();
        }
        private void BtnMemory_Click(object sender, MouseButtonEventArgs e)
        {
            ModBase.RunInThread(() => MemoryOptimize(true));
        }
    }
}