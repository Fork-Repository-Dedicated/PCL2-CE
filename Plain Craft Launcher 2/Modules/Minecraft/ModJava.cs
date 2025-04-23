using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public static class ModJava
    {
        public static int JavaListCacheVersion = 7;

        /// <summary>
    /// 目前所有可用的 Java。
    /// </summary>
        public static List<JavaEntry> JavaList = new List<JavaEntry>();

        public class JavaEntry
        {

            // 路径
            /// <summary>
        /// Java.exe 文件的完整路径。
        /// </summary>
            public string PathJava
            {
                get
                {
                    return PathFolder + "java.exe";
                }
            }
            /// <summary>
        /// Javaw.exe 文件的完整路径。
        /// </summary>
            public string PathJavaw
            {
                get
                {
                    return PathFolder + "javaw.exe";
                }
            }
            /// <summary>
        /// Javaw.exe 文件所在文件夹的路径，以 \ 结尾。
        /// </summary>
            public string PathFolder;
            /// <summary>
        /// 是否为用户手动导入的 Java。
        /// </summary>
            public bool IsUserImport;
            /// <summary>
        /// 是否使用此 Java
        /// </summary>
            public bool IsEnabled = true;

            // 版本信息
            /// <summary>
        /// Java 的详细版本。若不足 4 位会在前方补 1，例如 1.16.0.1。
        /// 其大版本号为 Minor。
        /// </summary>
            public Version Version;
            /// <summary>
        /// Java 的大版本号。
        /// </summary>
            public int VersionCode
            {
                get
                {
                    return Version.Minor;
                }
            }
            /// <summary>
        /// 是否为 Java Runtime Environment。
        /// </summary>
            public bool IsJre;
            /// <summary>
        /// 是否为 64 位 Java。
        /// </summary>
            public bool Is64Bit;
            /// <summary>
        /// 是否已设置环境变量。
        /// </summary>
            public bool HasEnvironment
            {
                get
                {
                    if (PathFolder is null || PathEnv is null)
                        return false;
                    return PathEnv.Replace(@"\", "").Replace("/", "").ContainsF(PathFolder.Replace(@"\", ""), true);
                }
            }

            // 序列化
            public JObject ToJson()
            {
                return new JObject(new[] { new JProperty("Path", PathFolder), new JProperty("VersionString", Version.ToString()), new JProperty("IsJre", IsJre), new JProperty("Is64Bit", Is64Bit), new JProperty("IsUserImport", IsUserImport), new JProperty("IsEnabled", IsEnabled) });
            }
            public static JavaEntry FromJson(JObject Data)
            {
                return new JavaEntry((string)Data["Path"], (bool)Data["IsUserImport"])
                {
                    Version = new Version((string)Data["VersionString"]),
                    IsJre = (bool)Data["IsJre"],
                    Is64Bit = (bool)Data["Is64Bit"],
                    IsEnabled = (bool)(Data["IsEnabled"] ?? true)
                };
            }
            /// <summary>
        /// 转化为用户友好的字符串输出。
        /// </summary>
            public override string ToString()
            {
                string VersionString = Version.ToString();
                if (VersionString.StartsWithF("1."))
                    VersionString = Strings.Mid(VersionString, 3);
                return (IsJre ? "JRE " : "JDK ") + VersionCode + " (" + VersionString + ")" + (Is64Bit ? "" : "，32 位") + (IsUserImport ? "，手动导入" : "") + "：" + PathFolder;
            }

            // 构造
            /// <summary>
        /// 输入 javaw.exe 文件所在文件夹的路径，不限制结尾。
        /// </summary>
            public JavaEntry(string Folder, bool IsUserImport)
            {
                if (!Folder.EndsWithF(@"\"))
                    Folder += @"\";
                PathFolder = Folder.Replace("/", @"\");
                this.IsUserImport = IsUserImport;
            }

            // 方法
            private bool IsChecked = false;
            /// <summary>
        /// 检查并获取 Java 详细信息。在 Java 存在异常时抛出错误。
        /// </summary>
            public void Check()
            {
                if (IsChecked)
                    return;
                string Output = null;
                try
                {
                    // 确定文件存在
                    if (!File.Exists(PathJavaw))
                    {
                        throw new FileNotFoundException("未找到 javaw.exe 文件", PathJavaw);
                    }
                    if (!File.Exists(PathFolder + "java.exe"))
                    {
                        throw new FileNotFoundException("未找到 java.exe 文件", PathFolder + "java.exe");
                    }
                    IsJre = !File.Exists(PathFolder + "javac.exe");
                    // 运行 -version
                    Output = ModBase.ShellAndGetOutput(PathFolder + "java.exe", "-version", 15000).ToLower();
                    if (string.IsNullOrEmpty(Output))
                        throw new ApplicationException("尝试运行该 Java 失败");
                    if (ModBase.ModeDebug)
                        ModBase.Log("[Java] Java 检查输出：" + PathFolder + "java.exe" + Constants.vbCrLf + Output);
                    if (Output.Contains("/lib/ext exists"))
                        throw new ApplicationException("无法运行该 Java，请在删除 Java 文件夹中的 /lib/ext 文件夹后再试");
                    // 获取详细信息
                    string VersionString = (ModBase.RegexSeek(Output, "(?<=version \")[^\"]+") ?? ModBase.RegexSeek(Output, "(?<=openjdk )[0-9]+") ?? "").Replace("_", ".").Split("-").First();
                    if (VersionString.Split(".").Count() > 4)
                        VersionString = VersionString.Replace(".0.", "."); // #3493，VersionString = "21.0.2.0.2"
                    while (VersionString.Split(".").Count() < 4)
                    {
                        if (VersionString.StartsWithF("1."))
                        {
                            VersionString = VersionString + ".0";
                        }
                        else
                        {
                            VersionString = "1." + VersionString;
                        }
                    }
                    if (string.IsNullOrEmpty(VersionString))
                        throw new ApplicationException($"未找到该 Java 的版本号{(Output.Length < 500 ? $"{Constants.vbCrLf}输出为：{Constants.vbCrLf}{Output}" : "")}");
                    Version = new Version(VersionString);
                    if (Version.Minor == 0)
                    {
                        ModBase.Log("[Java] 疑似 X.0.X.X 格式版本号：" + Version.ToString());
                        Version = new Version(1, Version.Major, Version.Build, Version.Revision);
                    }
                    Is64Bit = Output.Contains("64-bit");
                    if (Version.Minor <= 4 || Version.Minor >= 100)
                        throw new ApplicationException("分析详细信息失败，获取的版本为 " + Version.ToString());
                    // 基于 #3649，在 64 位系统上禁用 32 位 Java
                    if (!Is64Bit && !ModBase.Is32BitSystem)
                        throw new Exception("该 Java 为 32 位版本，请安装 64 位的 Java");
                    // 基于 #2249 发现 JRE 17 似乎也导致了 Forge 安装失败，干脆禁用更多版本的 JRE
                    if (IsJre && VersionCode >= 16)
                        throw new Exception("由于高版本 JRE 对游戏的兼容性很差，因此不再允许使用。你可以使用对应版本的 JDK，而非 JRE！");
                }
                catch (ApplicationException ex)
                {
                    throw ex;
                }
                catch (ThreadInterruptedException ex)
                {
                    throw ex;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    throw new ApplicationException($"与系统交互时出现错误。来自：{ex.Source}，错误代码：{ex.HResult}", ex);
                }
                catch (Exception ex)
                {
                    ModBase.Log("[Java] 检查失败的 Java 输出：" + PathFolder + "java.exe" + Constants.vbCrLf + (Output ?? "无程序输出"));
                    throw new Exception("检查 Java 失败（" + (PathJavaw ?? "Nothing") + "）", ex);
                }
                IsChecked = true;
            }

        }

        /// <summary>
    /// Path 环境变量。
    /// </summary>
        private static string PathEnv
        {
            get
            {
                if (_PathEnv is null)
                    _PathEnv = Environment.GetEnvironmentVariable("Path") ?? "";
                return _PathEnv;
            }
        }
        private static string _PathEnv = null;

        /// <summary>
    /// JAVA_HOME 环境变量。
    /// </summary>
        private static string PathJavaHome
        {
            get
            {
                if (_PathJavaHome is null)
                    _PathJavaHome = Environment.GetEnvironmentVariable("JAVA_HOME") ?? "";
                return _PathJavaHome;
            }
        }
        private static string _PathJavaHome = null;

        /// <summary>
    /// 初始化 Java 列表，但除非没有 Java，否则不进行检查。
    /// </summary>
        public static void JavaListInit()
        {
            JavaList = new List<JavaEntry>();
            try
            {
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectLess(ModBase.Setup.Get("CacheJavaListVersion"), JavaListCacheVersion, false)))
                {
                    // 不使用缓存
                    ModBase.Log("[Java] 要求 Java 列表缓存更新");
                    ModBase.Setup.Set("CacheJavaListVersion", JavaListCacheVersion);
                }
                else
                {
                    // 使用缓存
                    foreach (var JsonEntry in (IEnumerable)ModBase.GetJson(Conversions.ToString(ModBase.Setup.Get("LaunchArgumentJavaAll"))))
                        JavaList.Add(JavaEntry.FromJson((JObject)JsonEntry));
                }
                if (!JavaList.Any())
                {
                    ModBase.Log("[Java] 初始化未找到可用的 Java，将自动触发搜索", ModBase.LogLevel.Developer);
                    JavaSearchLoader.Start(0);
                }
                else
                {
                    ModBase.Log("[Java] 缓存中有 " + JavaList.Count + " 个可用的 Java：");
                    JavaList.ForEach(j => ModBase.Log($"[Java]  - {j}"));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化 Java 列表失败", ModBase.LogLevel.Feedback);
                ModBase.Setup.Set("LaunchArgumentJavaAll", "[]");
            }
        }

        /// <summary>
    /// 防止多个需要 Java 的部分同时要求下载 Java（#3797）。
    /// </summary>
        public static object JavaLock = new object();
        /// <summary>
    /// 根据要求返回最适合的 Java，若找不到则返回 Nothing。
    /// 最小与最大版本在与输入相同时也会通过。
    /// 必须在工作线程调用，且必须包括 SyncLock JavaLock。
    /// </summary>
        public static JavaEntry JavaSelect(string CancelException, Version MinVersion = null, Version MaxVersion = null, ModMinecraft.McVersion RelatedVersion = null)
        {
            try
            {
                var AllowedJavaList = new List<JavaEntry>();

                // 添加特定的 Java
                var JavaPreList = new Dictionary<string, bool>();
                if (ModMinecraft.PathMcFolder.Split(@"\").Count() > 3 && !ModMinecraft.PathMcFolder.Contains(@"AppData\Roaming"))
                {
                    JavaSearchFolder(ModBase.GetPathFromFullPath(ModMinecraft.PathMcFolder), ref JavaPreList, false, true); // Minecraft 文件夹的父文件夹（如果不是根目录或 %APPDATA% 的话）
                }
                JavaSearchFolder(ModMinecraft.PathMcFolder, ref JavaPreList, false, true); // Minecraft 文件夹
                JavaPreList = JavaPreList.Where(j => !j.Key.Contains(@".minecraft\runtime")).ToDictionary(j => j.Key, j => j.Value); // 排除官启自带 Java（#4286）
                if (RelatedVersion is not null)
                    JavaSearchFolder(RelatedVersion.Path, ref JavaPreList, false, true); // 所选版本文件夹
                var TargetJavaList = new List<JavaEntry>();
                foreach (var Entry in JavaPreList)
                    TargetJavaList.Add(new JavaEntry(Entry.Key, Entry.Value));

                // 检查特定的 Java
                if (TargetJavaList.Any())
                {
                    TargetJavaList = JavaCheckList(TargetJavaList);
                    ModBase.Log("[Java] 检查后找到 " + TargetJavaList.Count + " 个特定路径下的 Java：");
                    foreach (var Java in TargetJavaList)
                        ModBase.Log($"[Java]  - {Java}");
                }

                #region 添加用户指定的 Java，储存到 UserJava 中

                JavaEntry UserJava = null;

                // 获取版本独立设置中指定的 Java
                string VersionSelect = "";
                if (RelatedVersion is not null)
                {
                    VersionSelect = Conversions.ToString(ModBase.Setup.Get("VersionArgumentJavaSelect", Version: RelatedVersion));
                    if (VersionSelect.StartsWithF("{"))
                    {
                        try
                        {
                            UserJava = JavaEntry.FromJson((JObject)ModBase.GetJson(VersionSelect));
                            UserJava.Check();
                        }
                        catch (ThreadInterruptedException ex)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            UserJava = null;
                            ModBase.Setup.Reset("VersionArgumentJavaSelect", Version: RelatedVersion);
                            ModBase.Log(ex, "版本独立设置中指定的 Java 已无法使用，此设置已重置", ModBase.LogLevel.Hint);
                        }
                    }
                }

                // 获取全局设置中指定的 Java
                if (UserJava is null && !string.IsNullOrEmpty(VersionSelect) && Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("LaunchArgumentJavaSelect"), "", false)))
                {
                    try
                    {
                        UserJava = JavaEntry.FromJson((JObject)ModBase.GetJson(Conversions.ToString(ModBase.Setup.Get("LaunchArgumentJavaSelect"))));
                        UserJava.Check();
                    }
                    catch (ThreadInterruptedException ex)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        UserJava = null;
                        ModBase.Setup.Reset("LaunchArgumentJavaSelect");
                        ModBase.Log(ex, "全局设置中指定的 Java 已无法使用，此设置已重置", ModBase.LogLevel.Hint);
                    }
                }

                // 添加到特定 Java 列表
                if (UserJava is not null)
                {
                    ModBase.Log($"[Java] 用户指定的 Java：{UserJava}");
                    TargetJavaList.Add(UserJava);
                }

            #endregion

            RetryGet:
                ;

                // 等待进行中的搜索结束
                if (JavaSearchLoader.State != ModBase.LoadState.Finished && JavaSearchLoader.State != ModBase.LoadState.Waiting)
                    JavaSearchLoader.WaitForExit();
                switch (JavaSearchLoader.State)
                {
                    case ModBase.LoadState.Failed:
                        {
                            throw JavaSearchLoader.Error;
                        }
                    case ModBase.LoadState.Aborted:
                        {
                            throw new ThreadInterruptedException("Java 搜索加载器已中断");
                        }
                }

                // 生成完整的 Java 列表
                var AllJavaList = new List<JavaEntry>();
                AllJavaList.AddRange(TargetJavaList);
                AllJavaList.AddRange(JavaList);

                // 禁用用户不希望使用的 Java
                AllJavaList = AllJavaList.Where(i => i.IsEnabled).ToList();

                // 根据选定条件进行过滤
                foreach (var Java in AllJavaList)
                {
                    if (MinVersion is not null && Java.Version < MinVersion)
                        continue;
                    if (MaxVersion is not null && Java.Version > MaxVersion)
                        continue;
                    if (Java.Is64Bit && ModBase.Is32BitSystem)
                        continue;
                    AllowedJavaList.Add(Java);
                }

                // 若未找到适合的 Java，尝试触发搜索
                if (!AllowedJavaList.Any() && JavaSearchLoader.State == ModBase.LoadState.Waiting)
                {
                    ModBase.Log("[Java] 未找到满足条件的 Java，尝试进行搜索");
                    JavaSearchLoader.Start(IsForceRestart: true);
                    goto RetryGet;
                }

                #region 检查用户指定的 Java 是否可用

                // 确保指定的 Java 可用
                if (UserJava is null)
                    goto ExitUserJavaCheck;
                if (AllowedJavaList.Any(j => (j.PathFolder ?? "") == (UserJava.PathFolder ?? "")))
                {
                    ModBase.Log("[Java] 使用用户指定的 Java：" + UserJava.PathFolder);
                    AllowedJavaList = new List<JavaEntry>() { UserJava };
                    goto UserPass;
                }

                // 指定的 Java 不可用，弹窗要求选择
                ModBase.Log("[Java] 发现用户指定的不兼容 Java：" + UserJava.ToString());
                ModBase.Log($"[Java] 目前实际可用的 Java 列表：");
                foreach (var Java in AllowedJavaList)
                    ModBase.Log($"[Java]  - {Java}");
                string Requirement = "";
                bool ShowRevision = false;
                if ((MinVersion is null || MinVersion.Minor == 0) && MaxVersion is not null && MaxVersion.Minor < 999)
                {
                    ShowRevision = MaxVersion.MinorRevision < 999;
                    Requirement = "最高兼容到 Java " + MaxVersion.Minor + (ShowRevision ? "." + MaxVersion.MajorRevision + "." + MaxVersion.MinorRevision : "");
                }
                else if (MinVersion is not null && MinVersion.Minor > 0 && (MaxVersion is null || MaxVersion.Minor >= 999))
                {
                    ShowRevision = MinVersion.MinorRevision > 0 || MinVersion.MajorRevision > 0;
                    Requirement = "至少需要 Java " + MinVersion.Minor + (ShowRevision ? "." + MinVersion.MajorRevision + "." + MinVersion.MinorRevision : "");
                }
                else if (MinVersion is not null && MinVersion.Minor > 0 && MaxVersion is not null && MaxVersion.Minor < 999)
                {
                    ShowRevision = MinVersion.MinorRevision > 0 || MinVersion.MajorRevision > 0 || MaxVersion.MinorRevision < 999;
                    string Left = MinVersion.Minor + (ShowRevision ? "." + MinVersion.MajorRevision + "." + MinVersion.MinorRevision : "");
                    string Right = MaxVersion.Minor + (ShowRevision ? "." + MaxVersion.MajorRevision + "." + MaxVersion.MinorRevision : "");
                    Requirement = "需要 Java " + ((Left ?? "") == (Right ?? "") ? Left : Left + " ~ " + Right);
                }
                string JavaCurrent = UserJava.VersionCode + (ShowRevision ? "." + UserJava.Version.MajorRevision + "." + UserJava.Version.MinorRevision : "");
                if (Conversions.ToBoolean(RelatedVersion is not null && (bool)ModBase.Setup.Get("VersionAdvanceJava", RelatedVersion)))
                {
                    // 直接跳过弹窗
                    ModBase.Log("[Java] 设置中指定了使用 Java " + JavaCurrent + "，但当前版本" + Requirement + "，这可能会导致游戏崩溃！", ModBase.LogLevel.Debug);
                    AllowedJavaList = new List<JavaEntry>() { UserJava };
                }
                else
                {
                    switch (ModMain.MyMsgBox("你在设置中手动指定了使用 Java " + JavaCurrent + "，但当前" + Requirement + "。" + Constants.vbCrLf + "如果强制使用该 Java，可能导致游戏崩溃。" + Constants.vbCrLf + "你也可以将 游戏 Java 设置修改为 自动选择合适的 Java。" + Constants.vbCrLf + Constants.vbCrLf + " - 指定的 Java：" + UserJava.ToString(), "Java 兼容性警告", "让 PCL 自动选择", "强制使用该 Java", "取消"))
                    {
                        case 1: // 让 PCL 自动选择
                            {
                                break;
                            }
                        case 2: // 强制使用指定的 Java
                            {
                                ModBase.Log("[Java] 已强制使用用户指定的不兼容 Java");
                                AllowedJavaList = new List<JavaEntry>() { UserJava };
                                break;
                            }
                        case 3: // 取消启动
                            {
                                throw new Exception(CancelException);
                            }
                    }
                }

            ExitUserJavaCheck:
                ;

                #endregion

                // 若依然未找到适合的 Java，直接返回
                if (!AllowedJavaList.Any())
                    return null;

                // 优先使用特定目录下的 Java
                foreach (var Java in AllowedJavaList)
                {
                    // 如果在官启文件夹启动，会将官启自带 Java 错误视作 MC 文件夹指定 Java，导致了 #2054 的第二例
                    if (Java.PathFolder.Contains(@".minecraft\cache\java"))
                        continue;
                    if (TargetJavaList.Contains(Java))
                    {
                        // 直接使用指定的 Java
                        AllowedJavaList = new List<JavaEntry>() { Java };
                        ModBase.Log("[Java] 优先使用特定路径下的 Java：" + Java.ToString());
                        goto UserPass;
                    }
                }

            UserPass:
                ;


                // 对适合的 Java 进行排序
                AllowedJavaList = AllowedJavaList.Sort(JavaSorter);
                ModBase.Log($"[Java] 排序后的 Java 优先顺序：");
                foreach (var Java in AllowedJavaList)
                    ModBase.Log($"[Java]  - {Java}");

                // 检查选定的 Java，若测试失败则尝试进行搜索
                var SelectedJava = AllowedJavaList.First();
                try
                {
                    SelectedJava.Check();
                }
                catch (ThreadInterruptedException ex)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException is not null && ex.InnerException is ThreadInterruptedException)
                        throw ex.InnerException;
                    ModBase.Log(ex, "最终选定的 Java 已无法使用，尝试进行搜索");
                    AllowedJavaList = new List<JavaEntry>();
                    JavaSearchLoader.Start(IsForceRestart: true);
                    goto RetryGet;
                }

                // 返回
                ModBase.Log("[Java] 最终选定的 Java：" + AllowedJavaList.First().ToString());
                return SelectedJava;
            }

            catch (ThreadInterruptedException ex)
            {
                ModBase.Log(ex, "查找符合条件的 Java 时出现加载器中断");
                return null;
            }
            catch (Exception ex)
            {
                if (ex.Message == "$$")
                    throw ex;
                ModBase.Log(ex, "查找符合条件的 Java 失败", ModBase.LogLevel.Feedback);
                return null;
            }
        }
        /// <summary>
    /// 是否强制指定了 64 位 Java。如果没有强制指定，返回是否安装了 64 位 Java。
    /// </summary>
        public static bool JavaIs64Bit(ModMinecraft.McVersion RelatedVersion = null)
        {
            try
            {
                // 检查强制指定
                string UserSetup = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentJavaSelect"));
                if (RelatedVersion is not null)
                {
                    string UserSetupVersion = Conversions.ToString(ModBase.Setup.Get("VersionArgumentJavaSelect", Version: RelatedVersion));
                    if (UserSetupVersion != "使用全局设置")
                        UserSetup = UserSetupVersion;
                }
                if (!string.IsNullOrEmpty(UserSetup))
                {
                    JavaEntry UserJava = null;
                    try
                    {
                        UserJava = JavaEntry.FromJson((JObject)ModBase.GetJson(UserSetup));
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "版本指定的 Java 信息已损坏，已重置版本设置中指定的 Java");
                        ModBase.Setup.Set("VersionArgumentJavaSelect", "使用全局设置", Version: RelatedVersion);
                        goto NoUserJava;
                    }
                    foreach (var Java in JavaList)
                    {
                        if ((Java.PathFolder ?? "") == (UserJava.PathFolder ?? ""))
                            return UserJava.Is64Bit;
                    }
                }

            NoUserJava:
                ;

                // 检查列表
                foreach (var Java in JavaList)
                {
                    if (Java.Is64Bit)
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "检查 Java 类别时出错", ModBase.LogLevel.Feedback);
                ModBase.Setup.Set("LaunchArgumentJavaSelect", "");
                return true;
            }
        }
        /// <summary>
    /// 将 Java 按照适用性排序。
    /// </summary>
        public static bool JavaSorter(JavaEntry Left, JavaEntry Right)
        {
            // 1. 尽量在当前文件夹或当前 Minecraft 文件夹
            string ProgramPathParent;
            string MinecraftPathParent = "";
            ProgramPathParent = (new DirectoryInfo(ModBase.Path).Parent ?? new DirectoryInfo(ModBase.Path)).FullName;
            if (!string.IsNullOrEmpty(ModMinecraft.PathMcFolder))
                MinecraftPathParent = (new DirectoryInfo(ModMinecraft.PathMcFolder).Parent ?? new DirectoryInfo(ModMinecraft.PathMcFolder)).FullName;
            if (Left.PathFolder.StartsWithF(ProgramPathParent) && !Right.PathFolder.StartsWithF(ProgramPathParent))
                return true;
            if (!Left.PathFolder.StartsWithF(ProgramPathParent) && Right.PathFolder.StartsWithF(ProgramPathParent))
                return false;
            if (!string.IsNullOrEmpty(ModMinecraft.PathMcFolder))
            {
                if (Left.PathFolder.StartsWithF(MinecraftPathParent) && !Right.PathFolder.StartsWithF(MinecraftPathParent))
                    return true;
                if (!Left.PathFolder.StartsWithF(MinecraftPathParent) && Right.PathFolder.StartsWithF(MinecraftPathParent))
                    return false;
            }
            // 2. 尽量使用 64 位
            if (Left.Is64Bit && !Right.Is64Bit)
                return true;
            if (!Left.Is64Bit && Right.Is64Bit)
                return false;
            // 3. 尽量不使用 JDK
            if (Left.IsJre && !Right.IsJre)
                return true;
            if (!Left.IsJre && Right.IsJre)
                return false;
            // 4. Java 大版本
            if (Left.VersionCode != Right.VersionCode)
            {
                // Java  7   8   9  10  11  12 13 14 15  16  17  18  19  20  21  22  23...
                int[] Weight = new[] { 0, 1, 2, 3, 4, 5, 6, 14, 30, 10, 12, 15, 13, 9, 8, 7, 11, 31, 29, 16, 17, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18 };
                return Weight.ElementAtOrDefault(Left.VersionCode) >= Weight.ElementAtOrDefault(Right.VersionCode);
            }
            // 5. 最次级版本号更接近 51
            return Math.Abs(Left.Version.Revision - 51) <= Math.Abs(Right.Version.Revision - 51);
        }

        #region 搜索

        /// <summary>
    /// 模糊搜索并获取所有可用的 Java，并在结束后更新设置页面显示。输出将直接写入 JavaList。
    /// </summary>
        public static ModLoader.LoaderTask<int, int> JavaSearchLoader = new ModLoader.LoaderTask<int, int>("查找 Java", JavaSearchLoaderSub) { ProgressWeight = 2d };
        private static void JavaSearchLoaderSub(ModLoader.LoaderTask<int, int> Loader)
        {
            if (ModMain.FrmSetupLaunch is not null)
            {
                ModBase.RunInUiWait(() =>
        {
            ModMain.FrmSetupLaunch.ComboArgumentJava.Items.Clear();
            ModMain.FrmSetupLaunch.ComboArgumentJava.Items.Add(new ComboBoxItem() { Content = "加载中……", IsSelected = true });
        });
            }
            if (ModMain.FrmVersionSetup is not null)
            {
                ModBase.RunInUiWait(() =>
        {
            ModMain.FrmVersionSetup.ComboArgumentJava.Items.Clear();
            ModMain.FrmVersionSetup.ComboArgumentJava.Items.Add(new ComboBoxItem() { Content = "加载中……", IsSelected = true });
        });
            }

            try
            {

                // 可能包含 Java 的文件夹列表，以 “\” 结尾，且仅包含 “\”
                // Key：文件夹地址
                // Value: 是否为玩家手动导入
                var JavaPreList = new Dictionary<string, bool>();

                #region 模糊查找可能可用的 Java

                // 查找环境变量中的 Java
                foreach (string PathInEnv in (PathEnv + ";" + PathJavaHome).Replace(@"\\", @"\").Replace("/", @"\").Split(";"))
                {
                    PathInEnv = PathInEnv.Trim(" \"".ToCharArray());
                    if (string.IsNullOrEmpty(PathInEnv))
                        continue;
                    if (!PathInEnv.EndsWithF(@"\"))
                        PathInEnv += @"\";
                    // 粗略检查有效性
                    if (File.Exists(PathInEnv + "javaw.exe"))
                        JavaPreList[PathInEnv] = false;
                }
                // 细致搜索
                if (Conversions.ToBoolean(ModBase.Setup.Get("LaunchArgumentJavaTraversal")))
                {
                    // 查找磁盘中的 Java
                    foreach (DriveInfo Disk in DriveInfo.GetDrives())
                    {
                        if (Disk.DriveType == DriveType.Network)
                            continue; // 跳过网络驱动器（#3705）
                        JavaSearchFolder(Disk.Name, ref JavaPreList, false);
                    }

                    // 查找 AppData 文件夹中的 Java
                    JavaSearchFolder(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\", ref JavaPreList, false);
                    JavaSearchFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\", ref JavaPreList, false);
                }
                // 查找 AppData 中 .minecraft 目录下的 Java
                JavaSearchFolder(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\", ref JavaPreList, false);
                // 查找启动器目录中的 Java
                JavaSearchFolder(ModBase.Path, ref JavaPreList, false, IsFullSearch: true);
                // 查找所选 Minecraft 文件夹中的 Java
                if (!string.IsNullOrWhiteSpace(ModMinecraft.PathMcFolder) && (ModBase.Path ?? "") != (ModMinecraft.PathMcFolder ?? ""))
                {
                    JavaSearchFolder(ModMinecraft.PathMcFolder, ref JavaPreList, false, IsFullSearch: true);
                }

                // 若不全为符号链接，则清除符号链接的地址
                var JavaWithoutReparse = new Dictionary<string, bool>();
                foreach (var Pair in JavaPreList)
                {
                    string Folder = Pair.Key.Replace(@"\\", @"\").Replace("/", @"\");
                    FileSystemInfo Info = new FileInfo(Folder + "javaw.exe");
                    bool continueFor = false;
                    do
                    {
                        if (Info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            ModBase.Log("[Java] 位于 " + Folder + " 的 Java 包含符号链接");
                            continueFor = true;
                            break;
                        }
                        Info = Info is FileInfo ? ((FileInfo)Info).Directory : ((DirectoryInfo)Info).Parent;
                    }
                    while (Info is not null);
                    if (continueFor)
                    {
                        continue;
                    }
                    ModBase.Log("[Java] 位于 " + Folder + " 的 Java 不含符号链接");
                    JavaWithoutReparse.Add(Pair.Key, Pair.Value);
                }
                if (JavaWithoutReparse.Any())
                    JavaPreList = JavaWithoutReparse;

                // 若不全为特殊引用，则清除特殊引用的地址
                var JavaWithoutInherit = new Dictionary<string, bool>();
                foreach (var Pair in JavaPreList)
                {
                    if (Pair.Key.Contains("java8path_target_") || Pair.Key.Contains("javapath_target_") || Pair.Key.Contains("javatmp"))
                    {
                        ModBase.Log("[Java] 位于 " + Pair.Key + " 的 Java 包含特殊引用");
                    }
                    else
                    {
                        ModBase.Log("[Java] 位于 " + Pair.Key + " 的 Java 不含特殊引用");
                        JavaWithoutInherit.Add(Pair.Key, Pair.Value);
                    }
                }
                if (JavaWithoutInherit.Any())
                    JavaPreList = JavaWithoutInherit;

                #endregion

                #region 添加玩家手动导入的 Java

                string ImportedJava = Conversions.ToString(ModBase.Setup.Get("LaunchArgumentJavaAll"));
                try
                {
                    foreach (var JavaJsonObject in (IEnumerable)ModBase.GetJson(ImportedJava))
                    {
                        var Entry = JavaEntry.FromJson((JObject)JavaJsonObject);
                        if (Entry.IsUserImport)
                            JavaPreList[Entry.PathFolder] = true;
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "Java 列表已损坏，无法获取用户导入的 Java", ModBase.LogLevel.Feedback);
                    ModBase.Setup.Set("LaunchArgumentJavaAll", "[]");
                }

                #endregion

                // 确保可用并获取详细信息，转入正式列表
                var NewJavaList = new List<JavaEntry>();
                foreach (var Entry in JavaPreList.Distinct((a, b) => (a.Key.ToLower() ?? "") == (b.Key.ToLower() ?? ""))) // #794
                    NewJavaList.Add(new JavaEntry(Entry.Key, Entry.Value));
                NewJavaList = JavaCheckList(NewJavaList).Sort(JavaSorter);

                #region 同步原有的启用信息
                try
                {
                    foreach (var JavaJsonObject in (IEnumerable)ModBase.GetJson(ImportedJava))
                    {
                        var Target = NewJavaList.Find(j => (j.PathFolder ?? "") == (JavaEntry.FromJson((JObject)JavaJsonObject).PathFolder ?? ""));
                        if (Target is not null)
                        {
                            Target.IsEnabled = JavaEntry.FromJson((JObject)JavaJsonObject).IsEnabled;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "Java 列表已损坏，无法获取原有 Java 启用情况", ModBase.LogLevel.Feedback);
                    ModBase.Setup.Set("LaunchArgumentJavaAll", "[]");
                }
                #endregion

                // 修改设置项
                var AllList = new JArray();
                foreach (var Java in NewJavaList)
                    AllList.Add(Java.ToJson());
                ModBase.Setup.Set("LaunchArgumentJavaAll", AllList.ToString(Newtonsoft.Json.Formatting.None));
                JavaList = NewJavaList;
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "搜索 Java 时出错", ModBase.LogLevel.Feedback);
                JavaList = new List<JavaEntry>();
            }

            ModBase.Log("[Java] Java 搜索完成，发现 " + JavaList.Count + " 个 Java");
            if (ModMain.FrmSetupLaunch is not null)
                ModBase.RunInUi(() => ModMain.FrmSetupLaunch.RefreshJavaComboBox());
            if (ModMain.FrmVersionSetup is not null)
                ModBase.RunInUi(() => ModMain.FrmVersionSetup.RefreshJavaComboBox());
        }

        /// <summary>
    /// 多线程检查列表中的所有 Java 项。
    /// </summary>
        private static List<JavaEntry> JavaCheckList(List<JavaEntry> JavaEntries)
        {
            List<JavaEntry> JavaCheckListRet = default;
            ModBase.Log("[Java] 开始确认列表 Java 状态，共 " + JavaEntries.Count + " 项");
            JavaCheckListRet = new List<JavaEntry>();
            var ListLock = new object();

            // 启动检查线程
            var CheckThreads = new List<Thread>();
            foreach (var Entry in JavaEntries)
            {
                var CheckThread = new Thread(() => { try { Entry.Check(); if (ModBase.ModeDebug) ModBase.Log("[Java]  - " + Entry.ToString()); lock (ListLock) JavaCheckListRet.Add(Entry); } catch (ThreadInterruptedException ex) { } catch (Exception ex) { if (Entry.IsUserImport) { ModBase.Log(ex, "位于 " + Entry.PathFolder + " 的 Java 存在异常，将被自动移除", ModBase.LogLevel.Hint); } else { ModBase.Log(ex, "位于 " + Entry.PathFolder + " 的 Java 存在异常"); } } });
                CheckThreads.Add(CheckThread);
                CheckThread.Start();
            }

        // 等待构造线程完成
        Wait:
            ;

            Thread.Sleep(10);
            foreach (var CheckThread in CheckThreads)
            {
                if (CheckThread.IsAlive)
                    goto Wait;
            }

            return JavaCheckListRet;
        }
        /// <summary>
    /// 模糊搜索指定文件夹下的 Java，并只进行粗略的检查。这不会搜索全部路径。
    /// </summary>
    /// <param name="OriginalPath">开始搜索的起始路径，不限制结尾。</param>
    /// <param name="IsFullSearch">搜索当前文件夹下的全部文件夹（此参数不会传递到子文件夹）。</param>
        private static void JavaSearchFolder(string OriginalPath, ref Dictionary<string, bool> Results, bool Source, bool IsFullSearch = false)
        {
            try
            {
                ModBase.Log("[Java] 开始" + (IsFullSearch ? "完全" : "部分") + "遍历查找：" + OriginalPath);
                JavaSearchFolder(new DirectoryInfo(ModBase.ShortenPath(OriginalPath)), ref Results, Source, IsFullSearch);
            }
            catch (UnauthorizedAccessException ex)
            {
                ModBase.Log("[Java] 遍历查找 Java 时遭遇无权限的文件夹：" + OriginalPath);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "遍历查找 Java 时出错（" + OriginalPath + "）");
            }
        }
        /// <summary>
    /// 模糊搜索指定文件夹下的 Java，并只进行粗略的检查。这不会搜索全部路径。
    /// </summary>
    /// <param name="OriginalPath">开始搜索的起始路径，不限制结尾。</param>
    /// <param name="IsFullSearch">搜索当前文件夹下的全部文件夹（此参数不会传递到子文件夹）。</param>
        private static void JavaSearchFolder(DirectoryInfo OriginalPath, ref Dictionary<string, bool> Results, bool Source, bool IsFullSearch = false)
        {
            try
            {
                // 确认目录存在
                if (!OriginalPath.Exists)
                    return;
                string Path = OriginalPath.FullName.Replace(@"\\", @"\");
                if (!Path.EndsWithF(@"\"))
                    Path += @"\";
                // 若该目录有 Java，则加入结果
                if (File.Exists(Path + "javaw.exe"))
                    Results[Path] = Source;
                // 查找其下的所有文件夹
                // 不应使用网易的 Java：https://github.com/Hex-Dragon/PCL2/issues/1279#issuecomment-2761489121
                string[] Keywords = new[] { "java", "jdk", "env", "环境", "run", "软件", "jre", "mc", "dragon", "soft", "cache", "temp", "corretto", "roaming", "users", "craft", "program", "世界", "net", "游戏", "oracle", "game", "file", "data", "jvm", "服务", "server", "客户", "client", "整合", "应用", "运行", "前置", "mojang", "官启", "新建文件夹", "eclipse", "microsoft", "hotspot", "runtime", "x86", "x64", "forge", "原版", "optifine", "官方", "启动", "hmcl", "mod", "高清", "download", "launch", "程序", "path", "version", "baka", "pcl", "zulu", "local", "packages", "4297127d64ec6", "1.", "启动" };
                foreach (DirectoryInfo FolderInfo in OriginalPath.EnumerateDirectories())
                {
                    if (FolderInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue; // 跳过符号链接
                    string SearchEntry = ModBase.GetFolderNameFromPath(FolderInfo.Name).ToLower(); // 用于搜索的字符串
                    if (IsFullSearch || OriginalPath.Name.ToLower() == "users" || ModBase.Val(SearchEntry) > 0d || Keywords.Any(w => SearchEntry.Contains(w)) || SearchEntry == "bin")
                    {
                        JavaSearchFolder(FolderInfo, ref Results, Source);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                ModBase.Log("[Java] 遍历查找 Java 时遭遇无权限的文件夹：" + OriginalPath.FullName);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "遍历查找 Java 时出错（" + OriginalPath.FullName + "）");
            }
        }

        #endregion

        #region 下载

        /// <summary>
    /// 提示 Java 缺失，并弹窗确认是否自动下载。返回玩家选择是否下载。
    /// </summary>
        public static bool JavaDownloadConfirm(string VersionDescription, bool ForcedManualDownload = false)
        {
            if (ForcedManualDownload)
            {
                ModMain.MyMsgBox($"PCL 未找到 {VersionDescription}。" + Constants.vbCrLf + $"请自行搜索并安装 {VersionDescription}，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。", "未找到 Java");
                return false;
            }
            else
            {
                return ModMain.MyMsgBox($"PCL 未找到 {VersionDescription}，是否需要 PCL 自动下载？" + Constants.vbCrLf + $"如果你已经安装了 {VersionDescription}，请在 设置 → 启动选项 → 游戏 Java 中手动导入。", "未找到 Java", "自动下载", "取消") == 1;
            }
        }

        /// <summary>
    /// 获取下载 Java 8/14/17/21 的加载器。需要开启 IsForceRestart 以正常刷新 Java 列表。
    /// </summary>
        public static ModLoader.LoaderCombo<int> JavaFixLoaders(int Version)
        {
            var JavaDownloadLoader = new ModNet.LoaderDownload("下载 Java 文件", new List<ModNet.NetFile>()) { ProgressWeight = 10d };
            var Loader = new ModLoader.LoaderCombo<int>($"下载 Java {Version}", new[] { new ModLoader.LoaderTask<int, List<ModNet.NetFile>>("获取 Java 下载信息", JavaFileList) { ProgressWeight = 2d }, JavaDownloadLoader, JavaSearchLoader });
            JavaDownloadLoader.OnStateChangedThread += (Raw, NewState, OldState) => { if ((NewState == ModBase.LoadState.Failed || NewState == ModBase.LoadState.Aborted) && LastJavaBaseDir is not null) { ModBase.Log($"[Java] 由于下载未完成，清理未下载完成的 Java 文件：{LastJavaBaseDir}", ModBase.LogLevel.Debug); ModBase.DeleteDirectory(LastJavaBaseDir); } else if (NewState == ModBase.LoadState.Finished) { LastJavaBaseDir = null; } };
            JavaDownloadLoader.HasOnStateChangedThread = true;
            return Loader;
        }
        private static string LastJavaBaseDir = null; // 用于在下载中断或失败时删除未完成下载的 Java 文件夹，防止残留只下了一半但 -version 能跑的 Java
        private static void JavaFileList(ModLoader.LoaderTask<int, List<ModNet.NetFile>> Loader)
        {
            ModBase.Log("[Java] 开始获取 Java 下载信息");
            string IndexFileStr = ModNet.NetGetCodeByLoader(new[] { "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json", "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json" }, IsJson: true);
            // 获取下载地址
            JObject MainEntry = (JObject)((JObject)ModBase.GetJson(IndexFileStr))[$"windows-x{(ModBase.Is32BitSystem ? "86" : "64")}"];
            var Entries = MainEntry.Children().Reverse().SelectMany((JProperty e) => ((JArray)e.Value).Select(v => new KeyValuePair<string, JObject>(e.Name, (JObject)v))); // 选择最靠后的一项（最新）
            var TargetEntry = Entries.First(t => t.Value["version"][(object)"name"].ToString().StartsWithF(Loader.Input.ToString()));
            string Address = (string)TargetEntry.Value["manifest"]["url"];
            ModBase.Log($"[Java] 准备下载 Java {TargetEntry.Value["version"]["name"]}（{TargetEntry.Key}）：{Address}");
            // 获取文件列表
            string ListFileStr = ModNet.NetGetCodeByLoader(new[] { Address.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com"), Address }, IsJson: true);
            LastJavaBaseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\runtime\" + TargetEntry.Key + @"\";
            var Results = new List<ModNet.NetFile>();
            foreach (JProperty File in ((JObject)ModBase.GetJson(ListFileStr))["files"])
            {
                if (((JObject)File.Value)["downloads"]?["raw"] is null)
                    continue;
                JObject Info = (JObject)((JObject)File.Value)["downloads"]["raw"];
                var Checker = new ModBase.FileChecker(ActualSize: (long)Info["size"], Hash: (string)Info["sha1"]);
                if (Checker.Hash == "12976a6c2b227cbac58969c1455444596c894656" || Checker.Hash == "c80e4bab46e34d02826eab226a4441d0970f2aba" || Checker.Hash == "84d2102ad171863db04e7ee22a259d1f6c5de4a5")
                {
                    // 跳过 3 个无意义大量重复文件（#3827）
                    continue;
                }
                if (Checker.Check(LastJavaBaseDir + File.Name) is null)
                    continue; // 跳过已存在的文件
                string Url = (string)Info["url"];
                Results.Add(new ModNet.NetFile(new[] { Url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com"), Url }, LastJavaBaseDir + File.Name, Checker));
            }
            Loader.Output = Results;
            ModBase.Log($"[Java] 需要下载 {Results.Count} 个文件，目标文件夹：{LastJavaBaseDir}");
        }

        #endregion

    }
}