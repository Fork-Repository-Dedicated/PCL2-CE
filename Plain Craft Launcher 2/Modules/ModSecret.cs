using System;
using System.Collections.Generic;
// 由于包含加解密等安全信息，本文件中的部分代码已被删除

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    internal static class ModSecret
    {

        #region 杂项

        /* TODO ERROR: Skipped IfDirectiveTrivia
        #If RELEASE Or BETA Then
        *//* TODO ERROR: Skipped DisabledTextTrivia
            Public Const RegFolder As String = "PCLCE" 'PCL 社区版的注册表与 PCL 的注册表隔离，以防数据冲突
        *//* TODO ERROR: Skipped ElseDirectiveTrivia
        #Else
        */
        public const string RegFolder = "PCLCEDebug"; // 社区开发版的注册表与社区常规版的注册表隔离，以防数据冲突
        /* TODO ERROR: Skipped EndIfDirectiveTrivia
        #End If
        */
        // 用于微软登录的 ClientId
        public const string OAuthClientId = "";
        // CurseForge API Key
        public const string CurseForgeAPIKey = "";
        // LittleSkin OAuth ClientId
        public const string LittleSkinClientId = "";

        internal static void SecretOnApplicationStart()
        {
            // 提升 UI 线程优先级
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            // 确保 .NET Framework 版本
            try
            {
                var VersionTest = new FormattedText("", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First(), 96d, new ModBase.MyColor(), ModBase.DPI);
            }
            catch (UriFormatException ex) // 修复 #3555
            {
                Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User);
                var VersionTest = new FormattedText("", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First(), 96d, new ModBase.MyColor(), ModBase.DPI);
            }
            // 检测当前文件夹权限
            try
            {
                Directory.CreateDirectory(ModBase.Path + "PCL");
            }
            catch (Exception ex)
            {
                Interaction.MsgBox($"PCL 无法创建 PCL 文件夹（{ModBase.Path + "PCL"}），请尝试：" + Constants.vbCrLf + "1. 将 PCL 移动到其他文件夹" + (ModBase.Path.StartsWithF("C:", true) ? "，例如 C 盘和桌面以外的其他位置。" : "。") + Constants.vbCrLf + "2. 删除当前目录中的 PCL 文件夹，然后再试。" + Constants.vbCrLf + "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。", MsgBoxStyle.Critical, "运行环境错误");
                Environment.Exit((int)ModBase.ProcessReturnValues.Cancel);
            }
            if (!ModBase.CheckPermission(ModBase.Path + "PCL"))
            {
                Interaction.MsgBox("PCL 没有对当前文件夹的写入权限，请尝试：" + Constants.vbCrLf + "1. 将 PCL 移动到其他文件夹" + (ModBase.Path.StartsWithF("C:", true) ? "，例如 C 盘和桌面以外的其他位置。" : "。") + Constants.vbCrLf + "2. 删除当前目录中的 PCL 文件夹，然后再试。" + Constants.vbCrLf + "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。", MsgBoxStyle.Critical, "运行环境错误");
                Environment.Exit((int)ModBase.ProcessReturnValues.Cancel);
            }
            // 社区版提示
            if (Conversions.ToBoolean(ModBase.Setup.Get("UiLauncherCEHint")))
                ShowCEAnnounce();
        }
        /// <summary>
    /// 展示社区版提示
    /// </summary>
    /// <param name="IsUpdate">是否为更新时启动</param>
        public static void ShowCEAnnounce(bool IsUpdate = false)
        {
            ModMain.MyMsgBox($@"你正在使用来自 PCL-Community 的 PCL 社区版本，遇到问题请不要向官方仓库反馈！
PCL-Community 及其成员与龙腾猫跃无从属关系，且均不会为您的使用做担保。

如果你是意外下载的社区版，建议下载官方版 PCL 使用。

该版本与官方版本的特性区别：
- 联网通知：暂时没有，在做了在做了.jpg
- 主题切换：不会制作，这是需要赞助解锁的纪念性质的功能
- 百宝箱：部分内容更改和缺失，主线分支没有提供相关内容{(IsUpdate ? $"{Constants.vbCrLf}{Constants.vbCrLf}该提示总会在更新启动器时展示一次。" : "")}", "社区版本说明", "我知道了");
        }

        private static string _RawCodeCache = null;
        private readonly static object _cacheLock = new object();
        /// <summary>
    /// 获取原始的设备标识码
    /// </summary>
    /// <returns></returns>
        internal static string SecretGetRawCode()
        {
            lock (_cacheLock)
            {
                try
                {
                    if (_RawCodeCache is not null)
                        return _RawCodeCache;
                    var rawCode = default(string);
                    var searcher = new ManagementObjectSearcher("select ProcessorId from Win32_Processor"); // 获取 CPU 序列号
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        rawCode = obj["ProcessorId"]?.ToString();
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(rawCode))
                        throw new Exception("获取 CPU 序列号失败");
                    using (var sha256 = SHA256.Create()) // SHA256 加密
                    {
                        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawCode));
                        rawCode = BitConverter.ToString(hash).Replace("-", "");
                    }
                    _RawCodeCache = rawCode;
                    return rawCode;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[System] 获取设备原始标识码失败，使用默认标识码");
                    return "b09675a9351cbd1fd568056781fe3966dd936cc9b94e51ab5cf67eeb7e74c075".ToUpper();
                }
            }
        }

        /// <summary>
    /// 获取设备的短标识码
    /// </summary>
        internal static string SecretGetUniqueAddress()
        {
            string code;
            string rawCode = SecretGetRawCode();
            try
            {
                using (var MD5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] buffer = MD5.ComputeHash(Encoding.UTF8.GetBytes(rawCode));
                    code = BitConverter.ToString(buffer).Replace("-", "");
                }
                code = code.Substring(6, 16);
                code = code.Insert(4, "-").Insert(9, "-").Insert(14, "-");
                return code;
            }
            catch (Exception ex)
            {
                return "PCL2-CECE-GOOD-2025";
            }
        }

        private static string _EncryptKeyCache = null;
        private readonly static object _cacheEncryptKeyLock = new object();
        /// <summary>
    /// 获取 AES 加密密钥
    /// </summary>
    /// <returns></returns>
        internal static string SecretGetEncryptKey()
        {
            lock (_cacheEncryptKeyLock)
            {
                if (_EncryptKeyCache is not null)
                    return _EncryptKeyCache;
                string rawCode = SecretGetRawCode();
                using (var SHA512 = System.Security.Cryptography.SHA512.Create())
                {
                    byte[] hash = SHA512.ComputeHash(Encoding.UTF8.GetBytes(rawCode));
                    string key = BitConverter.ToString(hash).Replace("-", "");
                    key = key.Substring(4, 32);
                    _EncryptKeyCache = key;
                    return key;
                }
            }
        }

        internal static void SecretLaunchJvmArgs(ref List<string> DataList)
        {
            string DataJvmCustom = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceJvm", Version: ModMinecraft.McVersionCurrent));
            DataList.Insert(0, Conversions.ToString(string.IsNullOrEmpty(DataJvmCustom) ? ModBase.Setup.Get("LaunchAdvanceJvm") : DataJvmCustom)); // 可变 JVM 参数
            ModLaunch.McLaunchLog("当前剩余内存：" + Math.Round(My.MyWpfExtension.Computer.Info.AvailablePhysicalMemory / 1024d / 1024d / 1024d * 10d) / 10d + "G");
            DataList.Add("-Xmn" + Math.Floor(PageVersionSetup.GetRam(ModMinecraft.McVersionCurrent) * 1024d * 0.15d) + "m");
            DataList.Add("-Xmx" + Math.Floor(PageVersionSetup.GetRam(ModMinecraft.McVersionCurrent) * 1024d) + "m");
            if (!DataList.Any(d => d.Contains("-Dlog4j2.formatMsgNoLookups=true")))
                DataList.Add("-Dlog4j2.formatMsgNoLookups=true");
        }

        /// <summary>
    /// 打码字符串中的 AccessToken。
    /// </summary>
        internal static string SecretFilter(string Raw, char FilterChar)
        {
            // 打码 "accessToken " 后的内容
            if (Raw.Contains("accessToken "))
            {
                foreach (var Token in Raw.RegexSearch("(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})"))
                    Raw = Raw.Replace(Token, new string(FilterChar, Token.Count()));
            }
            // 打码当前登录的结果
            string AccessToken = ModLaunch.McLoginLoader.Output.AccessToken;
            if (AccessToken is null || AccessToken.Length < 10 || !Raw.ContainsF(AccessToken, true) || (ModLaunch.McLoginLoader.Output.Uuid ?? "") == (ModLaunch.McLoginLoader.Output.AccessToken ?? "")) // UUID 和 AccessToken 一样则不打码
            {
                return Raw;
            }
            else
            {
                return Raw.Replace(AccessToken, Strings.Left(AccessToken, 5) + new string(FilterChar, AccessToken.Length - 10) + Strings.Right(AccessToken, 5));
            }
        }

        #endregion

        #region 网络鉴权

        internal static object SecretCdnSign(string UrlWithMark)
        {
            if (!UrlWithMark.EndsWithF("{CDN}"))
                return UrlWithMark;
            return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20");
        }
        /// <summary>
    /// 设置 Headers 的 UA、Referer。
    /// </summary>
        internal static void SecretHeadersSign(string Url, ref WebClient Client, bool UseBrowserUserAgent = false)
        {
            if (Url.Contains("baidupcs.com") || Url.Contains("baidu.com"))
            {
                Client.Headers["User-Agent"] = "LogStatistic"; // #4951
            }
            else if (UseBrowserUserAgent)
            {
                Client.Headers["User-Agent"] = "PCL2/" + ModBase.UpstreamVersion + "." + ModBase.VersionBranchCode + " PCLCE/" + ModBase.VersionStandardCode + " Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36";
            }
            else
            {
                Client.Headers["User-Agent"] = "PCL2/" + ModBase.UpstreamVersion + "." + ModBase.VersionBranchCode + " PCLCE/" + ModBase.VersionStandardCode;
            }
            Client.Headers["Referer"] = "http://" + ModBase.VersionCode + ".ce.open.pcl2.server/";
            if (Url.Contains("api.curseforge.com"))
                Client.Headers["x-api-key"] = CurseForgeAPIKey;
        }
        /// <summary>
    /// 设置 Headers 的 UA、Referer。
    /// </summary>
        internal static void SecretHeadersSign(string Url, ref HttpWebRequest Request, bool UseBrowserUserAgent = false)
        {
            if (Url.Contains("baidupcs.com") || Url.Contains("baidu.com"))
            {
                Request.UserAgent = "LogStatistic"; // #4951
            }
            else if (UseBrowserUserAgent)
            {
                Request.UserAgent = "PCL2/" + ModBase.UpstreamVersion + "." + ModBase.VersionBranchCode + " PCLCE/" + ModBase.VersionStandardCode + " Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36";
            }
            else
            {
                Request.UserAgent = "PCL2/" + ModBase.UpstreamVersion + "." + ModBase.VersionBranchCode + " PCLCE/" + ModBase.VersionStandardCode;
            }
            Request.Referer = "http://" + ModBase.VersionCode + ".ce.open.pcl2.server/";
            if (Url.Contains("api.curseforge.com"))
                Request.Headers["x-api-key"] = CurseForgeAPIKey;
        }

        #endregion

        #region 字符串加解密

        internal static string SecretDecrptyOld(string SourceString)
        {
            string Key = "00000000";
            byte[] btKey = Encoding.UTF8.GetBytes(Key);
            byte[] btIV = Encoding.UTF8.GetBytes("87160295");
            var des = new DESCryptoServiceProvider();
            using (var MS = new MemoryStream())
            {
                byte[] inData = Convert.FromBase64String(SourceString);
                using (var cs = new CryptoStream(MS, des.CreateDecryptor(btKey, btIV), CryptoStreamMode.Write))
                {
                    cs.Write(inData, 0, inData.Length);
                    cs.FlushFinalBlock();
                    return Encoding.UTF8.GetString(MS.ToArray());
                }
            }
        }

        /// <summary>
    /// 加密字符串（优化版）。
    /// </summary>
        internal static string SecretEncrypt(string SourceString)
        {
            string Key = SecretGetEncryptKey();

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                byte[] salt = new byte[32];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(salt);
                }

                using (var deriveBytes = new Rfc2898DeriveBytes(Key, salt, 1000))
                {
                    aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
                    aes.GenerateIV();
                }

                using (var ms = new MemoryStream())
                {
                    ms.Write(salt, 0, salt.Length);
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(SourceString);
                        cs.Write(data, 0, data.Length);
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        /// <summary>
    /// 解密字符串。
    /// </summary>
        internal static string SecretDecrypt(string SourceString)
        {
            string Key = SecretGetEncryptKey();
            byte[] encryptedData = Convert.FromBase64String(SourceString);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                byte[] salt = new byte[32];
                Array.Copy(encryptedData, 0, salt, 0, salt.Length);

                byte[] iv = new byte[(aes.BlockSize / 8)];
                Array.Copy(encryptedData, salt.Length, iv, 0, iv.Length);
                aes.IV = iv;

                if (encryptedData.Length < salt.Length + iv.Length)
                {
                    throw new ArgumentException("加密数据格式无效或已损坏");
                }

                using (var deriveBytes = new Rfc2898DeriveBytes(Key, salt, 1000))
                {
                    aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
                }

                int cipherTextLength = encryptedData.Length - salt.Length - iv.Length;
                using (var ms = new MemoryStream(encryptedData, salt.Length + iv.Length, cipherTextLength))
                {
                    using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        using (var sr = new StreamReader(cs, Encoding.UTF8))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }

        #endregion

        #region 主题

        public static bool IsDarkMode = false;

        public static ModBase.MyColor ColorDark1 = new ModBase.MyColor(235d, 235d, 235d);
        public static ModBase.MyColor ColorDark2 = new ModBase.MyColor(102d, 204d, 255d);
        public static ModBase.MyColor ColorDark3 = new ModBase.MyColor(51d, 187d, 255d);
        public static ModBase.MyColor ColorDark6 = new ModBase.MyColor(93d, 101d, 103d);
        public static ModBase.MyColor ColorDark7 = new ModBase.MyColor(69d, 75d, 79d);
        public static ModBase.MyColor ColorDark8 = new ModBase.MyColor(59d, 64d, 65d);
        public static ModBase.MyColor ColorLight1 = new ModBase.MyColor(52d, 61d, 74d);
        public static ModBase.MyColor ColorLight2 = new ModBase.MyColor(11d, 91d, 203d);
        public static ModBase.MyColor ColorLight3 = new ModBase.MyColor(19d, 112d, 243d);
        public static ModBase.MyColor ColorLight6 = new ModBase.MyColor(213d, 230d, 253d);
        public static ModBase.MyColor ColorLight7 = new ModBase.MyColor(222d, 236d, 253d);
        public static ModBase.MyColor ColorLight8 = new ModBase.MyColor(234d, 242d, 254d);
        public static ModBase.MyColor Color1 = IsDarkMode ? ColorDark1 : ColorLight1;
        public static ModBase.MyColor Color2 = IsDarkMode ? ColorDark2 : ColorLight2;
        public static ModBase.MyColor Color3 = IsDarkMode ? ColorDark3 : ColorLight3;
        // Public Color2 As New MyColor(11, 91, 203)
        // Public Color3 As New MyColor(19, 112, 243)
        public static ModBase.MyColor Color4 = new ModBase.MyColor(72d, 144d, 245d);
        public static ModBase.MyColor Color5 = new ModBase.MyColor(150d, 192d, 249d);
        public static ModBase.MyColor Color6 = IsDarkMode ? ColorDark6 : ColorLight6;
        public static ModBase.MyColor Color7 = IsDarkMode ? ColorDark7 : ColorLight7;
        public static ModBase.MyColor Color8 = IsDarkMode ? ColorDark8 : ColorLight8;
        public static ModBase.MyColor ColorBg0 = new ModBase.MyColor(150d, 192d, 249d);
        public static ModBase.MyColor ColorBg1 = new ModBase.MyColor(190d, Color7);
        public static ModBase.MyColor ColorGrayDark1 = new ModBase.MyColor(245d, 245d, 245d);
        public static ModBase.MyColor ColorGrayDark2 = new ModBase.MyColor(240d, 240d, 240d);
        public static ModBase.MyColor ColorGrayDark3 = new ModBase.MyColor(235d, 235d, 235d);
        public static ModBase.MyColor ColorGrayDark4 = new ModBase.MyColor(204d, 204d, 204d);
        public static ModBase.MyColor ColorGrayDark5 = new ModBase.MyColor(166d, 166d, 166d);
        public static ModBase.MyColor ColorGrayDark6 = new ModBase.MyColor(140d, 140d, 140d);
        public static ModBase.MyColor ColorGrayDark7 = new ModBase.MyColor(115d, 115d, 115d);
        public static ModBase.MyColor ColorGrayDark8 = new ModBase.MyColor(64d, 64d, 64d);
        public static ModBase.MyColor ColorGrayLight1 = new ModBase.MyColor(64d, 64d, 64d);
        public static ModBase.MyColor ColorGrayLight2 = new ModBase.MyColor(115d, 115d, 115d);
        public static ModBase.MyColor ColorGrayLight3 = new ModBase.MyColor(140d, 140d, 140d);
        public static ModBase.MyColor ColorGrayLight4 = new ModBase.MyColor(166d, 166d, 166d);
        public static ModBase.MyColor ColorGrayLight5 = new ModBase.MyColor(204d, 204d, 204d);
        public static ModBase.MyColor ColorGrayLight6 = new ModBase.MyColor(235d, 235d, 235d);
        public static ModBase.MyColor ColorGrayLight7 = new ModBase.MyColor(240d, 240d, 240d);
        public static ModBase.MyColor ColorGrayLight8 = new ModBase.MyColor(245d, 245d, 245d);
        public static ModBase.MyColor ColorGray1 = IsDarkMode ? ColorGrayDark1 : ColorGrayLight1;
        public static ModBase.MyColor ColorGray2 = IsDarkMode ? ColorGrayDark2 : ColorGrayLight2;
        public static ModBase.MyColor ColorGray3 = IsDarkMode ? ColorGrayDark3 : ColorGrayLight3;
        public static ModBase.MyColor ColorGray4 = IsDarkMode ? ColorGrayDark4 : ColorGrayLight4;
        public static ModBase.MyColor ColorGray5 = IsDarkMode ? ColorGrayDark5 : ColorGrayLight5;
        public static ModBase.MyColor ColorGray6 = IsDarkMode ? ColorGrayDark6 : ColorGrayLight6;
        public static ModBase.MyColor ColorGray7 = IsDarkMode ? ColorGrayDark7 : ColorGrayLight7;
        public static ModBase.MyColor ColorGray8 = IsDarkMode ? ColorGrayDark8 : ColorGrayLight8;
        public static ModBase.MyColor ColorSemiTransparent = new ModBase.MyColor(1d, Color8);

        public static int ThemeNow = -1;
        // Public ColorHue As Integer = If(IsDarkMode, 200, 210), ColorSat As Integer = If(IsDarkMode, 100, 85), ColorLightAdjust As Integer = If(IsDarkMode, 15, 0), ColorHueTopbarDelta As Object = 0
        public static int ColorHue = 210;
        public static int ColorSat = 85;
        public static int ColorLightAdjust = 0;
        public static object ColorHueTopbarDelta = 0;
        public static int ThemeDontClick = 0;

        // 深色模式事件

        // 定义自定义事件
        public static event EventHandler<bool> ThemeChanged;

        // 触发事件的函数
        public static void RaiseThemeChanged(bool isDarkMode)
        {
            ThemeChanged?.Invoke("", isDarkMode);
        }

        public static void ThemeRefresh(int NewTheme = -1)
        {
            RaiseThemeChanged(IsDarkMode);
            ThemeRefreshColor();
            ThemeRefreshMain();
        }
        public static double GetDarkThemeLight(double OriginalLight)
        {
            if (IsDarkMode)
            {
                return OriginalLight * 0.1d;
            }
            else
            {
                return OriginalLight;
            }
        }
        public static void ThemeRefreshColor()
        {
            ColorGray1 = IsDarkMode ? ColorGrayDark1 : ColorGrayLight1;
            ColorGray2 = IsDarkMode ? ColorGrayDark2 : ColorGrayLight2;
            ColorGray3 = IsDarkMode ? ColorGrayDark3 : ColorGrayLight3;
            ColorGray4 = IsDarkMode ? ColorGrayDark4 : ColorGrayLight4;
            ColorGray5 = IsDarkMode ? ColorGrayDark5 : ColorGrayLight5;
            ColorGray6 = IsDarkMode ? ColorGrayDark6 : ColorGrayLight6;
            ColorGray7 = IsDarkMode ? ColorGrayDark7 : ColorGrayLight7;
            ColorGray8 = IsDarkMode ? ColorGrayDark8 : ColorGrayLight8;

            if (IsDarkMode)
            {
                System.Windows.Application.Current.Resources["ColorBrush1"] = new SolidColorBrush(ColorDark1);
                System.Windows.Application.Current.Resources["ColorBrush2"] = new SolidColorBrush(ColorDark2);
                System.Windows.Application.Current.Resources["ColorBrush3"] = new SolidColorBrush(ColorDark3);
                System.Windows.Application.Current.Resources["ColorBrush6"] = new SolidColorBrush(ColorDark6);
                System.Windows.Application.Current.Resources["ColorBrush7"] = new SolidColorBrush(ColorDark7);
                System.Windows.Application.Current.Resources["ColorBrush8"] = new SolidColorBrush(ColorDark8);
                System.Windows.Application.Current.Resources["ColorBrushGray1"] = new SolidColorBrush(ColorGrayDark1);
                System.Windows.Application.Current.Resources["ColorBrushGray2"] = new SolidColorBrush(ColorGrayDark2);
                System.Windows.Application.Current.Resources["ColorBrushGray3"] = new SolidColorBrush(ColorGrayDark3);
                System.Windows.Application.Current.Resources["ColorBrushGray4"] = new SolidColorBrush(ColorGrayDark4);
                System.Windows.Application.Current.Resources["ColorBrushGray5"] = new SolidColorBrush(ColorGrayDark5);
                System.Windows.Application.Current.Resources["ColorBrushGray6"] = new SolidColorBrush(ColorGrayDark6);
                System.Windows.Application.Current.Resources["ColorBrushGray7"] = new SolidColorBrush(ColorGrayDark7);
                System.Windows.Application.Current.Resources["ColorBrushGray8"] = new SolidColorBrush(ColorGrayDark8);
                System.Windows.Application.Current.Resources["ColorBrushHalfWhite"] = new SolidColorBrush(Color.FromArgb(85, 90, 90, 90));
                System.Windows.Application.Current.Resources["ColorBrushBg0"] = new SolidColorBrush(ColorDark2);
                System.Windows.Application.Current.Resources["ColorBrushBg1"] = new SolidColorBrush(Color.FromArgb(190, 90, 90, 90));
                System.Windows.Application.Current.Resources["ColorBrushBackgroundTransparentSidebar"] = new SolidColorBrush(Color.FromArgb(235, 43, 43, 43));
                System.Windows.Application.Current.Resources["ColorBrushTransparent"] = new SolidColorBrush(Color.FromArgb(0, 43, 43, 43));
                System.Windows.Application.Current.Resources["ColorBrushToolTip"] = new SolidColorBrush(Color.FromArgb(229, 90, 90, 90));
                System.Windows.Application.Current.Resources["ColorBrushWhite"] = new SolidColorBrush(Color.FromRgb(43, 43, 43));
                System.Windows.Application.Current.Resources["ColorBrushMsgBox"] = new SolidColorBrush(Color.FromRgb(43, 43, 43));
                System.Windows.Application.Current.Resources["ColorBrushMsgBoxText"] = new SolidColorBrush(ColorDark1);
                System.Windows.Application.Current.Resources["ColorBrushMemory"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }
            else
            {
                System.Windows.Application.Current.Resources["ColorBrush1"] = new SolidColorBrush(ColorLight1);
                System.Windows.Application.Current.Resources["ColorBrush2"] = new SolidColorBrush(ColorLight2);
                System.Windows.Application.Current.Resources["ColorBrush3"] = new SolidColorBrush(ColorLight3);
                System.Windows.Application.Current.Resources["ColorBrush6"] = new SolidColorBrush(ColorLight6);
                System.Windows.Application.Current.Resources["ColorBrush7"] = new SolidColorBrush(ColorLight7);
                System.Windows.Application.Current.Resources["ColorBrush8"] = new SolidColorBrush(ColorLight8);
                System.Windows.Application.Current.Resources["ColorBrushGray1"] = new SolidColorBrush(ColorGrayLight1);
                System.Windows.Application.Current.Resources["ColorBrushGray2"] = new SolidColorBrush(ColorGrayLight2);
                System.Windows.Application.Current.Resources["ColorBrushGray3"] = new SolidColorBrush(ColorGrayLight3);
                System.Windows.Application.Current.Resources["ColorBrushGray4"] = new SolidColorBrush(ColorGrayLight4);
                System.Windows.Application.Current.Resources["ColorBrushGray5"] = new SolidColorBrush(ColorGrayLight5);
                System.Windows.Application.Current.Resources["ColorBrushGray6"] = new SolidColorBrush(ColorGrayLight6);
                System.Windows.Application.Current.Resources["ColorBrushGray7"] = new SolidColorBrush(ColorGrayLight7);
                System.Windows.Application.Current.Resources["ColorBrushGray8"] = new SolidColorBrush(ColorGrayLight8);
                System.Windows.Application.Current.Resources["ColorBrushHalfWhite"] = new SolidColorBrush(Color.FromArgb(85, 255, 255, 255));
                System.Windows.Application.Current.Resources["ColorBrushBg0"] = new SolidColorBrush(ColorBg0);
                System.Windows.Application.Current.Resources["ColorBrushBg1"] = new SolidColorBrush(ColorBg1);
                System.Windows.Application.Current.Resources["ColorBrushBackgroundTransparentSidebar"] = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255));
                System.Windows.Application.Current.Resources["ColorBrushTransparent"] = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                System.Windows.Application.Current.Resources["ColorBrushToolTip"] = new SolidColorBrush(Color.FromArgb(229, 255, 255, 255));
                System.Windows.Application.Current.Resources["ColorBrushWhite"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                System.Windows.Application.Current.Resources["ColorBrushMsgBox"] = new SolidColorBrush(Color.FromRgb(251, 251, 251));
                System.Windows.Application.Current.Resources["ColorBrushMsgBoxText"] = new SolidColorBrush(ColorLight1);
                System.Windows.Application.Current.Resources["ColorBrushMemory"] = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
        }
        public static void ThemeRefreshMain()
        {
            ModBase.RunInUi(() =>
        {
            if (!ModMain.FrmMain.IsLoaded)
                return;
            // 顶部条背景
            var Brush = new LinearGradientBrush() { EndPoint = new Point(1d, 0d), StartPoint = new Point(0d, 0d) };
            if (ThemeNow == 5)
            {
                Brush.GradientStops.Add(new GradientStop() { Offset = 0d, Color = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 25d) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 0.5d, Color = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 15d) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 1d, Color = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 25d) });
                ModMain.FrmMain.PanTitle.Background = Brush;
                ModMain.FrmMain.PanTitle.Background.Freeze();
            }
            else if (!(ThemeNow == 12 || ThemeDontClick == 2))
            {
                if (ColorHueTopbarDelta is int)
                {
                    Brush.GradientStops.Add(new GradientStop() { Offset = 0d, Color = new ModBase.MyColor().FromHSL2(Conversions.ToDouble(Operators.SubtractObject(ColorHue, ColorHueTopbarDelta)), ColorSat, 48 + ColorLightAdjust) });
                    Brush.GradientStops.Add(new GradientStop() { Offset = 0.5d, Color = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 54 + ColorLightAdjust) });
                    Brush.GradientStops.Add(new GradientStop() { Offset = 1d, Color = new ModBase.MyColor().FromHSL2(Conversions.ToDouble(Operators.AddObject(ColorHue, ColorHueTopbarDelta)), ColorSat, 48 + ColorLightAdjust) });
                }
                else
                {
                    Brush.GradientStops.Add(new GradientStop() { Offset = 0d, Color = new ModBase.MyColor().FromHSL2(Conversions.ToDouble(Operators.AddObject(ColorHue, ModSecret.ColorHueTopbarDelta(0))), ColorSat, 48 + ColorLightAdjust) });
                    Brush.GradientStops.Add(new GradientStop() { Offset = 0.5d, Color = new ModBase.MyColor().FromHSL2(Conversions.ToDouble(Operators.AddObject(ColorHue, ModSecret.ColorHueTopbarDelta(1))), ColorSat, 54 + ColorLightAdjust) });
                    Brush.GradientStops.Add(new GradientStop() { Offset = 1d, Color = new ModBase.MyColor().FromHSL2(Conversions.ToDouble(Operators.AddObject(ColorHue, ModSecret.ColorHueTopbarDelta(2))), ColorSat, 48 + ColorLightAdjust) });
                }
                ModMain.FrmMain.PanTitle.Background = Brush;
                ModMain.FrmMain.PanTitle.Background.Freeze();
            }
            else
            {
                Brush.GradientStops.Add(new GradientStop() { Offset = 0d, Color = new ModBase.MyColor().FromHSL2(ColorHue - 21, ColorSat, 53 + ColorLightAdjust) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 0.33d, Color = new ModBase.MyColor().FromHSL2(ColorHue - 7, ColorSat, 47 + ColorLightAdjust) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 0.67d, Color = new ModBase.MyColor().FromHSL2(ColorHue + 7, ColorSat, 47 + ColorLightAdjust) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 1d, Color = new ModBase.MyColor().FromHSL2(ColorHue + 21, ColorSat, 53 + ColorLightAdjust) });
                ModMain.FrmMain.PanTitle.Background = Brush;
            }
            // 主页面背景
            if (Conversions.ToBoolean(ModBase.Setup.Get("UiBackgroundColorful")))
            {
                Brush = new LinearGradientBrush() { EndPoint = new Point(0.1d, 1d), StartPoint = new Point(0.9d, 0d) };
                Brush.GradientStops.Add(new GradientStop() { Offset = -0.1d, Color = new ModBase.MyColor().FromHSL2(ColorHue - 20, Math.Min(60, ColorSat) * 0.5d, GetDarkThemeLight(80d)) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 0.4d, Color = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.9d, GetDarkThemeLight(90d)) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 1.1d, Color = new ModBase.MyColor().FromHSL2(ColorHue + 20, Math.Min(60, ColorSat) * 0.5d, GetDarkThemeLight(80d)) });
                ModMain.FrmMain.PanForm.Background = Brush;
            }
            else
            {
                ModMain.FrmMain.PanForm.Background = new ModBase.MyColor(IsDarkMode ? 20 : 245, IsDarkMode ? 20 : 245, IsDarkMode ? 20 : 245);
            }
            ModMain.FrmMain.PanForm.Background.Freeze();
        });
        }
        internal static void ThemeCheckAll(bool EffectSetup)
        {
        }
        internal static bool ThemeCheckOne(int Id)
        {
            return true;
        }
        internal static bool ThemeUnlock(int Id, bool ShowDoubleHint = true, string UnlockHint = null)
        {
            return false;
        }
        internal static bool ThemeCheckGold(string Code = null)
        {
            return false;
        }
        internal static bool? DonateCodeInput()
        {
            return default;
        }

        #endregion

        #region 更新

        public class SelfUpdateInfo
        {
            public string Server { get; set; }

            public SelfUpdateAssest Latests { get; set; }
        }

        public class SelfUpdateAssest
        {
            public SelfUpdateChannelInfo Slow { get; set; }
            public SelfUpdateChannelInfo Fast { get; set; }
            public SelfUpdateChannelInfo Legacy { get; set; }
        }

        public class SelfUpdateChannelInfo
        {
            public string Version { get; set; }
            public int Code { get; set; }
            public string File { get; set; }
            public string Sha256 { get; set; }
        }

        public static SelfUpdateInfo RemoteVersionData = null;
        public static bool IsLauncherLatest = false;
        public static bool IsUpdateStarted = false;
        public static bool IsUpdateWaitingRestart = false;
        public const string PysioServer = "https://s3.pysio.online/pcl2-ce/";
        public const string GitHubServer = "https://github.com/PCL-Community/PCL2_CE_Server/raw/main/";

        public static void UpdateCheckByButton()
        {
            if (IsUpdateStarted)
            {
                ModMain.Hint("正在检查更新中，请稍后再试……");
                return;
            }
            ModMain.Hint("正在获取更新信息...");
            ModBase.RunInNewThread(() => { try { UpdateLatestVersionInfo(); NoticeUserUpdate(); } catch (Exception ex) { ModBase.Log(ex, "[Update] 获取启动器更新信息失败", ModBase.LogLevel.Hint); ModMain.Hint("获取启动器更新信息失败，请检查网络连接", ModMain.HintType.Critical); } });
        }
        public static void UpdateLatestVersionInfo()
        {
            if (ModBase.RunInUi())
            {
                ModMain.Hint("暂时无法获取更新信息……", ModMain.HintType.Critical);
                ModBase.Log("[System] 获取更新信息失败：在 UI 线程中运行");
            }
            ModBase.Log("[System] 正在获取版本信息");
            IsLauncherLatest = false;
            JObject LatestReleaseInfoJson = null;
            string Server = null;
            string JsonLink = null;
            string AnnounceVersionLink = null;
            bool IsBeta = Conversions.ToBoolean(ModBase.Setup.Get("SystemSystemUpdateBranch"));
            ModBase.Log($"[System] 启动器为 Fast Ring：{IsBeta}");
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("SystemSystemServer"), 0, false))) // Pysio 源
            {
                ModBase.Log("[System] 使用 Pysio 源获取版本信息");
                Server = PysioServer;
            }
            else // GitHub 源
            {
                ModBase.Log("[System] 使用 GitHub 源获取版本信息");
                Server = GitHubServer;
            }
            if (ModBase.IsArm64System)
            {
                JsonLink = Server + "updateARM_v2.json";
            }
            else
            {
                JsonLink = Server + "update_v2.json";
            }
            int CacheAnnounceVer = Conversions.ToInteger(ModNet.NetRequestRetry(Server + "announceVer.ini", "GET", "", "application/x-www-form-urlencoded"));
            ModBase.Setup.Set("CacheAnnounceVersion", CacheAnnounceVer);
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(CacheAnnounceVer, ModBase.Setup.Get("CacheAnnounceVersion"), false)))
            {
                IsLauncherLatest = true;
                return;
            }
            LatestReleaseInfoJson = (JObject)ModBase.GetJson(ModNet.NetRequestRetry(JsonLink, "GET", "", "application/x-www-form-urlencoded"));
            RemoteVersionData = LatestReleaseInfoJson.ToObject<SelfUpdateInfo>();
            ModBase.Log($"[System] 已获取到更新信息：{LatestReleaseInfoJson.ToString(Newtonsoft.Json.Formatting.None)}");
        }

        public static SelfUpdateChannelInfo GetCurrentUpdateChannelInfo()
        {
            if (IsLauncherLatest)
            {
                return new SelfUpdateChannelInfo() { Version = ModBase.VersionBaseName, Code = ModBase.VersionCode, File = ModBase.PathWithName, Sha256 = "" };
            }
            if (RemoteVersionData is null)
            {
                ModBase.Log("[Update] 未获取到远程版本信息，尝试重新获取");
                UpdateLatestVersionInfo();
            }
            SelfUpdateChannelInfo targetChannel = null;
            bool IsBeta = Conversions.ToBoolean(ModBase.Setup.Get("SystemSystemUpdateBranch"));
            if (IsBeta)
            {
                targetChannel = RemoteVersionData.Latests.Fast;
            }
            else
            {
                targetChannel = RemoteVersionData.Latests.Slow;
            }
            return targetChannel;
        }

        public static void NoticeUserUpdate(bool Silent = false)
        {
            var LatestVersion = GetCurrentUpdateChannelInfo();
            if (LatestVersion.Code > ModBase.VersionCode)
            {
                if (!(ModBase.Val(Environment.OSVersion.Version.ToString().Split(".")[2]) >= 19042d) && !LatestVersion.Version.StartsWithF("2.9."))
                {
                    if (ModMain.MyMsgBox($"发现了启动器更新（版本 {LatestVersion.Version}），但是由于你的 Windows 版本过低，不满足新版本要求。{Constants.vbCrLf}你需要更新到 Windows 10 20H2 或更高版本才可以继续更新。", "启动器更新 - 系统版本过低", "升级 Windows 10", "取消", IsWarn: true, ForceWait: true) == 1)
                        ModBase.OpenWebsite("https://www.microsoft.com/zh-cn/software-download/windows10");
                    return;
                }
                if (ModMain.MyMsgBox($"启动器有新版本可用（{ModBase.VersionBaseName} -> {LatestVersion.Version}），是否更新？", "启动器更新", "更新", "取消") == 1)
                {
                    UpdateStart(LatestVersion.Version, false);
                }
            }
            else if (!Silent)
                ModMain.Hint("启动器已是最新版 " + ModBase.VersionBaseName + "，无须更新啦！", ModMain.HintType.Finish);
        }
        public static void UpdateStart(string VersionStr, bool Slient, string ReceivedKey = null, bool ForceValidated = false)
        {
            string DlLink = null;
            DlLink = GetUpdateServerSource();
            string DlTargetPath = ModBase.Path + @"PCL\Plain Craft Launcher 2.exe";
            // 构造步骤加载器
            // 下载
            // 启动
            ModBase.RunInNewThread(() => { try { var Loaders = new List<ModLoader.LoaderBase>(); var Address = new List<string>(); Address.Add(DlLink); Loaders.Add(new ModNet.LoaderDownload("下载更新文件", new List<ModNet.NetFile>() { new ModNet.NetFile(Address.ToArray(), DlTargetPath, new ModBase.FileChecker(MinSize: 1024 * 64)) }) { ProgressWeight = 15d }); if (!Slient) { Loaders.Add(new ModLoader.LoaderTask<int, int>("安装更新", () => UpdateRestart(true))); } var Loader = new ModLoader.LoaderCombo<JObject>("启动器更新", Loaders); Loader.Start(); if (Slient) { IsUpdateWaitingRestart = true; } else { ModLoader.LoaderTaskbarAdd(Loader); ModMain.FrmMain.BtnExtraDownload.ShowRefresh(); ModMain.FrmMain.BtnExtraDownload.Ribble(); } } catch (Exception ex) { ModBase.Log(ex, "[Update] 下载启动器更新文件失败", ModBase.LogLevel.Hint); ModMain.Hint("下载启动器更新文件失败，请检查网络连接", ModMain.HintType.Critical); } });
        }
        public static void UpdateRestart(bool TriggerRestartAndByEnd)
        {
            try
            {
                string fileName = ModBase.Path + @"PCL\Plain Craft Launcher 2.exe";
                if (!File.Exists(fileName))
                {
                    ModBase.Log("[System] 更新失败：未找到更新文件");
                    return;
                }
                // id old new restart
                string text = string.Concat(new string[] { "--update ", Process.GetCurrentProcess().Id.ToString(), " \"", ModBase.PathWithName, "\" \"", fileName, "\" ", Conversions.ToString(TriggerRestartAndByEnd) });
                ModBase.Log("[System] 更新程序启动，参数：" + text, ModBase.LogLevel.Normal, "出现错误");
                Process.Start(new ProcessStartInfo(fileName) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = text });
                if (TriggerRestartAndByEnd)
                {
                    ModMain.FrmMain.EndProgram(false);
                    ModBase.Log("[System] 已由于更新强制结束程序", ModBase.LogLevel.Normal, "出现错误");
                }
            }
            catch (Win32Exception ex)
            {
                ModBase.Log(ex, "自动更新时触发 Win32 错误，疑似被拦截", ModBase.LogLevel.Debug, "出现错误");
                if (ModMain.MyMsgBox(string.Format(@"由于被 Windows 安全中心拦截，或者存在权限问题，导致 PCL 无法更新。{0}请将 PCL 所在文件夹加入白名单，或者手动用 {1}PCL\Plain Craft Launcher 2.exe 替换当前文件！", Constants.vbCrLf, ModBase.Path), "更新失败", "查看帮助", "确定", "", true, true, false, null, null, null) == 1)
                {
                    ModEvent.TryStartEvent("打开帮助", "启动器/Microsoft Defender 添加排除项.json");
                }
            }
        }
        public static void UpdateReplace(int ProcessId, string OldFileName, string NewFileName, bool TriggerRestart)
        {
            try
            {
                var ps = Process.GetProcessById(ProcessId);
                if (!ps.HasExited)
                {
                    ps.Kill();
                }
            }
            catch (Exception ex)
            {
            }
            Exception ex2 = null;
            int num = 0;
            do
            {
                do
                {
                    try
                    {
                        if (File.Exists(OldFileName))
                        {
                            File.Delete(OldFileName);
                        }
                        if (!File.Exists(OldFileName))
                        {
                            break;
                        }
                    }
                    catch (Exception ex3)
                    {
                        ex2 = ex3;
                    }
                    finally
                    {
                        Thread.Sleep(500);
                    }
                }
                while (false);
                num += 1;
            }
            while (num <= 4);
            if (!File.Exists(OldFileName) && File.Exists(NewFileName))
            {
                try
                {
                    ModBase.CopyFile(NewFileName, OldFileName);
                }
                catch (UnauthorizedAccessException ex4)
                {
                    Interaction.MsgBox("PCL 更新失败：权限不足。请手动复制 PCL 文件夹下的新版本程序。" + Constants.vbCrLf + "若 PCL 位于桌面或 C 盘，你可以尝试将其挪到其他文件夹，这可能可以解决权限问题。" + Constants.vbCrLf + ModBase.GetExceptionSummary(ex4), MsgBoxStyle.Critical, "更新失败");
                }
                catch (Exception ex5)
                {
                    Interaction.MsgBox("PCL 更新失败：无法复制新文件。请手动复制 PCL 文件夹下的新版本程序。" + Constants.vbCrLf + ModBase.GetExceptionSummary(ex5), MsgBoxStyle.Critical, "更新失败");
                    return;
                }
                if (TriggerRestart)
                {
                    try
                    {
                        Process.Start(OldFileName);
                    }
                    catch (Exception ex6)
                    {
                        Interaction.MsgBox("PCL 更新失败：无法重新启动。" + Constants.vbCrLf + ModBase.GetExceptionSummary(ex6), MsgBoxStyle.Critical, "更新失败");
                    }
                }
                return;
            }
            if (ex2 is UnauthorizedAccessException)
            {
                Interaction.MsgBox(string.Concat(new string[] { "由于权限不足，PCL 无法完成更新。请尝试：" + Constants.vbCrLf, ModBase.Path.StartsWithF(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), false) || ModBase.Path.StartsWithF(Environment.GetFolderPath(Environment.SpecialFolder.Personal), false) ? " - 将 PCL 文件移动到桌面、文档以外的文件夹（这或许可以一劳永逸地解决权限问题）" + Constants.vbCrLf : "", ModBase.Path.StartsWithF("C", true) ? " - 将 PCL 文件移动到 C 盘以外的文件夹（这或许可以一劳永逸地解决权限问题）" + Constants.vbCrLf : "", " - 右键以管理员身份运行 PCL" + Constants.vbCrLf + " - 手动复制已下载到 PCL 文件夹下的新版本程序，覆盖原程序" + Constants.vbCrLf + Constants.vbCrLf, ModBase.GetExceptionSummary(ex2) }), MsgBoxStyle.Critical, "更新失败");
                return;
            }
            Interaction.MsgBox("PCL 更新失败：无法删除原文件。请手动复制已下载到 PCL 文件夹下的新版本程序覆盖原程序。" + Constants.vbCrLf + ModBase.GetExceptionSummary(ex2), MsgBoxStyle.Critical, "更新失败");
        }
        /// <summary>
    /// 获取更新文件的下载地址。
    /// </summary>
    /// <param name="RequireStable">是否要求稳定版本的下载地址</param>
    /// <returns></returns>
        private static string GetUpdateServerSource(bool RequireStable = false)
        {
            var LatestVersion = RequireStable ? RemoteVersionData.Latests.Slow : GetCurrentUpdateChannelInfo();
            string DlLink = null;
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("SystemSystemServer"), 0, false))) // Pysio 源
            {
                DlLink = PysioServer + LatestVersion.File;
            }
            else // GitHub 源
            {
                DlLink = "https://github.com/PCL-Community/PCL2-CE/releases/download/" + LatestVersion.Version + "/" + LatestVersion.File;
            }
            return DlLink;
        }
        /// <summary>
    /// 确保 PathTemp 下的 Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    /// 如果不是，则下载一个。
    /// </summary>
        internal static void DownloadLatestPCL(ModLoader.LoaderBase LoaderToSyncProgress = null)
        {
            // 注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
            string LatestPCLPath = ModBase.PathTemp + "CE-Latest.exe";
            ModNet.NetDownloadByLoader(GetUpdateServerSource(true), LatestPCLPath, LoaderToSyncProgress);
        }

        #endregion

        #region 联网通知

        public static ModLoader.LoaderTask<int, int> ServerLoader = new ModLoader.LoaderTask<int, int>("PCL 服务", (_) => LoadOnlineInfo(), Priority: ThreadPriority.BelowNormal);

        private static void LoadOnlineInfo()
        {
            switch (ModBase.Setup.Get("SystemSystemUpdate"))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
                    {
                        UpdateLatestVersionInfo();
                        var LatestVersion = GetCurrentUpdateChannelInfo();
                        if (LatestVersion.Code > ModBase.VersionCode)
                        {
                            UpdateStart(LatestVersion.Version, true); // 静默更新
                        }

                        break;
                    }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false):
                    {
                        UpdateLatestVersionInfo();
                        NoticeUserUpdate(true);
                        break;
                    }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, 2, false):
                case var case3 when Operators.ConditionalCompareObjectEqual(case3, 3, false):
                    {
                        return;
                    }
            }
        }

        #endregion

    }
}