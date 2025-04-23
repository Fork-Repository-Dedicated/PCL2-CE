using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public static class ModNet
    {
        public const string NetDownloadEnd = ".PCLDownloading";

        private static WebProxy _Proxy { get; set; }
        /// <summary>
        /// 获取 Proxy 代理
        /// </summary>
        /// <returns>返回 WebProxy 或者 Nothing</returns>
        public static object GetProxy()
        {
            string proxy = Conversions.ToString(ModBase.Setup.Get("SystemHttpProxy"));
            if (_Proxy is not null && (_Proxy.Address.AbsoluteUri ?? "") == (proxy ?? ""))
            {
                ModBase.Log("[Net] 当前代理状态：跟随系统代理设置");
                return _Proxy;
            }
            if (!string.IsNullOrWhiteSpace(proxy))
            {
                _Proxy = new WebProxy(proxy, true);
                ModBase.Log("[Net] 当前代理状态：自定义");
                var ProxyUri = new Uri(_Proxy.ToString());
                try
                {
                    // 视作非本地地址
                    if (ProxyUri.IsLoopback || ProxyUri.Host.StartsWithF("192.168.") || ProxyUri.Host.StartsWithF("10.") || ProxyUri.Host.StartsWithF("fe80") || Conversions.ToDouble(ProxyUri.Host.Split(".")[1]) > 16d && Conversions.ToDouble(ProxyUri.Host.Split(".")[1]) < 31d && ProxyUri.Host.StartsWithF("172."))
                        ModBase.Log($"[Net] 使用 {_Proxy} 作为网络代理");
                }
                catch
                {
                }
                return _Proxy;
            }
            ModBase.Log("[Net] 当前代理状态：禁用");
            return null;
        }

        /// <summary>
        /// 测试 Ping。失败则返回 -1。
        /// </summary>
        public static int Ping(string Ip, int Timeout = 10000, bool MakeLog = true)
        {
            System.Net.NetworkInformation.PingReply PingResult;
            try
            {
                PingResult = new System.Net.NetworkInformation.Ping().Send(Ip);
            }
            catch (Exception ex)
            {
                if (MakeLog)
                    ModBase.Log("[Net] Ping " + Ip + " 失败：" + ex.Message);
                return -1;
            }
            if (PingResult.Status == System.Net.NetworkInformation.IPStatus.Success)
            {
                if (MakeLog)
                    ModBase.Log("[Net] Ping " + Ip + " 结束：" + PingResult.RoundtripTime + "ms");
                return (int)PingResult.RoundtripTime;
            }
            else
            {
                if (MakeLog)
                    ModBase.Log("[Net] Ping " + Ip + " 失败");
                return -1;
            }
        }

        /// <summary>
        /// 以 WebClient 获取网页源代码。会进行至多 45 秒 3 次的尝试，允许最长 30s 的超时。
        /// </summary>
        /// <param name="Url">网页的 Url。</param>
        /// <param name="Encoding">网页的编码，通常为 UTF-8。</param>
        public static string NetGetCodeByClient(string Url, Encoding Encoding, string Accept = "application/json, text/javascript, */*; q=0.01", bool UseBrowserUserAgent = false)
        {
            int RetryCount = 0;
            Exception RetryException = null;
            long StartTime = ModBase.GetTimeTick();
            try
            {
            Retry:

                switch (RetryCount)
                {
                    case 0: // 正常尝试
                        {
                            return NetGetCodeByClient(Url, Encoding, 10000, Accept, UseBrowserUserAgent);
                        }
                    case 1: // 慢速重试
                        {
                            Thread.Sleep(500);
                            return NetGetCodeByClient(Url, Encoding, 30000, Accept, UseBrowserUserAgent); // 快速重试
                        }

                    default:
                        {
                            if (ModBase.GetTimeTick() - StartTime > 5500L)
                            {
                                // 若前两次加载耗费 5 秒以上，才进行重试
                                Thread.Sleep(500);
                                return NetGetCodeByClient(Url, Encoding, 4000, Accept, UseBrowserUserAgent);
                            }
                            else
                            {
                                throw RetryException;
                            }
                        }
                }
            }
            catch (Exception ex)
            {
                switch (RetryCount)
                {
                    case 0:
                        {
                            RetryException = ex;
                            RetryCount += 1;
                            goto Retry;
                            break;
                        }
                    case 1:
                        {
                            RetryCount += 1;
                            goto Retry;
                            break;
                        }

                    default:
                        {
                            throw;
                        }
                }
            }
            return string.Empty;
        }
        public static string NetGetCodeByClient(string Url, Encoding Encoding, int Timeout, string Accept, bool UseBrowserUserAgent = false)
        {
            Url = Conversions.ToString(ModSecret.SecretCdnSign(Url));
            ModBase.Log("[Net] 获取客户端网络结果：" + Url + "，最大超时 " + Timeout);
            CookieWebClient Request;
            HttpWebResponse res = null;
            Stream HttpStream = null;
            try
            {
                Request = new CookieWebClient()
                {
                    Encoding = Encoding,
                    Timeout = Timeout
                };
                Request.Headers["Accept"] = Accept;
                Request.Headers["Accept-Language"] = "en-US,en;q=0.5";
                Request.Headers["X-Requested-With"] = "XMLHttpRequest";
                WebClient argClient = Request;
                ModSecret.SecretHeadersSign(Url, ref argClient, UseBrowserUserAgent);
                Request = (CookieWebClient)argClient;
                return Request.DownloadString(Url);
            }
            catch (Exception ex)
            {
                if (ex.GetType().Equals(typeof(WebException)) && ((WebException)ex).Status == WebExceptionStatus.Timeout)
                {
                    throw new TimeoutException("连接服务器超时（" + Url + "）", ex);
                }
                else
                {
                    throw new WebException("获取结果失败，" + ex.Message + "（" + Url + "）", ex);
                }
            }
            finally
            {
                if (!(HttpStream == null))
                    HttpStream.Dispose();
                if (!(res == null))
                    res.Dispose();
            }
        }

        /// <summary>
        /// 以 WebRequest 获取网页源代码或 Json。会进行至多 45 秒 3 次的尝试，允许最长 30s 的超时。
        /// </summary>
        /// <param name="Url">网页的 Url。</param>
        /// <param name="Encode">网页的编码，通常为 UTF-8。</param>
        /// <param name="BackupUrl">如果第一次尝试失败，换用的备用 URL。</param>
        public static object NetGetCodeByRequestRetry(string Url, Encoding Encode = null, string Accept = "", bool IsJson = false, string BackupUrl = null, bool UseBrowserUserAgent = false)
        {
            int RetryCount = 0;
            Exception RetryException = null;
            long StartTime = ModBase.GetTimeTick();
            try
            {
            Retry:
                ;

                switch (RetryCount)
                {
                    case 0: // 正常尝试
                        {
                            return NetGetCodeByRequestOnce(Url, Encode, 10000, IsJson, Accept, UseBrowserUserAgent);
                        }
                    case 1: // 慢速重试
                        {
                            Thread.Sleep(500);
                            return NetGetCodeByRequestOnce(BackupUrl ?? Url, Encode, 30000, IsJson, Accept, UseBrowserUserAgent); // 快速重试
                        }

                    default:
                        {
                            if (ModBase.GetTimeTick() - StartTime > 5500L)
                            {
                                // 若前两次加载耗费 5 秒以上，才进行重试
                                Thread.Sleep(500);
                                return NetGetCodeByRequestOnce(BackupUrl ?? Url, Encode, 4000, IsJson, Accept, UseBrowserUserAgent);
                            }
                            else
                            {
                                throw RetryException;
                            }
                        }
                }
            }
            catch (ThreadInterruptedException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                switch (RetryCount)
                {
                    case 0:
                        {
                            RetryException = ex;
                            RetryCount += 1;
                            goto Retry;
                            break;
                        }
                    case 1:
                        {
                            RetryCount += 1;
                            goto Retry;
                            break;
                        }

                    default:
                        {
                            throw;
                        }
                }
            }
            return string.Empty;
        }
        /// <summary>
        /// 以 WebRequest 获取网页源代码或 Json。会逐渐生成 4 个尝试线程，并在 60s 后超时。
        /// </summary>
        /// <param name="Url">网页的 Url。</param>
        /// <param name="Encode">网页的编码，通常为 UTF-8。</param>
        public static object NetGetCodeByRequestMultiple(string Url, Encoding Encode = null, string Accept = "", bool IsJson = false)
        {
            var Threads = new List<Thread>();
            object RequestResult = null;
            Exception RequestEx = null;
            int FailCount = 0;
            for (int i = 1; i <= 4; i++)
            {
                var th = new Thread(() => { try { RequestResult = NetGetCodeByRequestOnce(Url, Encode, 30000, IsJson, Accept); } catch (Exception ex) { FailCount += 1; RequestEx = ex; } });
                th.Start();
                Threads.Add(th);
                Thread.Sleep(i * 250);
                if (RequestResult is not null)
                    goto RequestFinished;
            }
            while (true)
            {
                if (RequestResult is not null)
                {
                RequestFinished:
                    ;

                    try
                    {
                        foreach (var th in Threads)
                        {
                            if (th.IsAlive)
                                th.Interrupt();
                        }
                    }
                    catch
                    {
                    }
                    return RequestResult;
                }
                else if (FailCount == 4)
                {
                    try
                    {
                        foreach (var th in Threads)
                        {
                            if (th.IsAlive)
                                th.Interrupt();
                        }
                    }
                    catch
                    {
                    }
                    throw RequestEx;
                }
                Thread.Sleep(20);
            }
            throw new Exception("未知错误");
        }
        public static object NetGetCodeByRequestOnce(string Url, Encoding Encode = null, int Timeout = 30000, bool IsJson = false, string Accept = "", bool UseBrowserUserAgent = false)
        {
            if (ModBase.RunInUi() && !Url.Contains("//127."))
                throw new Exception("在 UI 线程执行了网络请求");
            Url = Conversions.ToString(ModSecret.SecretCdnSign(Url));
            ModBase.Log($"[Net] 获取网络结果：{Url}，超时 {Timeout}ms{(IsJson ? "，要求 Json" : "")}");
            HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(Url);
            var Result = new List<byte>();
            try
            {
                if (Url.StartsWithF("https", true))
                    Request.ProtocolVersion = HttpVersion.Version11;
                Request.Timeout = Timeout;
                Request.Accept = Accept;
                ModSecret.SecretHeadersSign(Url, ref Request, UseBrowserUserAgent);
                using (HttpWebResponse res = (HttpWebResponse)Request.GetResponse())
                {
                    using (var HttpStream = res.GetResponseStream())
                    {
                        HttpStream.ReadTimeout = Timeout;
                        byte[] HttpData = new byte[16385];
                        using (var Reader = new StreamReader(HttpStream, Encode ?? Encoding.UTF8))
                        {
                            string ResultString = Reader.ReadToEnd();
                            return IsJson ? ModBase.GetJson(ResultString) : ResultString;
                        }
                    }
                }
            }
            catch (ThreadInterruptedException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex is WebException && ((WebException)ex).Status == WebExceptionStatus.Timeout)
                {
                    throw new TimeoutException($"获取结果失败（{((WebException)ex).Status}，{ex.Message}，{Url}）", ex);
                }
                else
                {
                    throw new WebException($"获取结果失败（{(ex is WebException ? ((int)((WebException)ex).Status).ToString() + "，" : "")}{ex.Message}，{Url}）", ex);
                }
            }
            finally
            {
                Request.Abort();
            }
        }

        /// <summary>
        /// 以多线程下载网页文件的方式获取网页源代码。
        /// </summary>
        /// <param name="Url">网页的 Url。</param>
        public static string NetGetCodeByLoader(string Url, int Timeout = 45000, bool IsJson = false, bool UseBrowserUserAgent = false)
        {
            string NetGetCodeByLoaderRet = default;
            string Temp = ModMain.RequestTaskTempFolder() + "download.txt";
            var NewTask = new LoaderDownload("源码获取 " + ModBase.GetUuid() + "#", new List<NetFile>() { new NetFile(new[] { Url }, Temp, new ModBase.FileChecker() { IsJson = IsJson }, UseBrowserUserAgent) });
            try
            {
                NewTask.WaitForExitTime(Timeout, TimeoutMessage: "连接服务器超时（" + Url + "）");
                NetGetCodeByLoaderRet = ModBase.ReadFile(Temp);
                File.Delete(Temp);
            }
            finally
            {
                NewTask.Abort();
            }

            return NetGetCodeByLoaderRet;
        }
        /// <summary>
        /// 以多线程下载网页文件的方式获取网页源代码。
        /// </summary>
        /// <param name="Urls">网页的 Url 列表。</param>
        public static string NetGetCodeByLoader(IEnumerable<string> Urls, int Timeout = 45000, bool IsJson = false, bool UseBrowserUserAgent = false)
        {
            string NetGetCodeByLoaderRet = default;
            string Temp = ModMain.RequestTaskTempFolder() + "download.txt";
            var NewTask = new LoaderDownload("源码获取 " + ModBase.GetUuid() + "#", new List<NetFile>() { new NetFile(Urls, Temp, new ModBase.FileChecker() { IsJson = IsJson }, UseBrowserUserAgent) });
            try
            {
                NewTask.WaitForExitTime(Timeout, TimeoutMessage: "连接服务器超时（第一下载源：" + Urls.First() + "）");
                NetGetCodeByLoaderRet = ModBase.ReadFile(Temp);
                File.Delete(Temp);
            }
            finally
            {
                NewTask.Abort();
            }

            return NetGetCodeByLoaderRet;
        }

        /// <summary>
        /// 使用 WebClient 从网络中下载文件。这不能下载 CDN 中的文件。
        /// </summary>
        /// <param name="Url">网络 Url。</param>
        /// <param name="LocalFile">下载的本地地址。</param>
        public static void NetDownloadByClient(string Url, string LocalFile, bool UseBrowserUserAgent = false)
        {
            ModBase.Log("[Net] 直接下载文件：" + Url);
            // 初始化
            try
            {
                // 建立目录
                Directory.CreateDirectory(ModBase.GetPathFromFullPath(LocalFile));
                // 尝试删除原文件
                File.Delete(LocalFile);
            }
            catch (Exception ex)
            {
                throw new WebException($"预处理下载文件路径失败（{LocalFile}）", ex);
            }
            // 下载
            using (var Client = new WebClient())
            {
                try
                {
                    var argClient = Client;
                    ModSecret.SecretHeadersSign(Url, ref argClient, UseBrowserUserAgent);
                    Client.DownloadFile(Url, LocalFile);
                }
                catch (Exception ex)
                {
                    File.Delete(LocalFile);
                    throw new WebException($"直接下载文件失败（{Url}）", ex);
                }
            }
        }

        /// <summary>
        /// 简单的多线程下载文件。可以下载 CDN 中的文件。
        /// </summary>
        /// <param name="Url">文件的 Url。</param>
        /// <param name="LocalFile">下载的本地地址。</param>
        public static void NetDownloadByLoader(string Url, string LocalFile, ModLoader.LoaderBase LoaderToSyncProgress = null, ModBase.FileChecker Check = null, bool UseBrowserUserAgent = false)
        {
            var NewTask = new LoaderDownload("文件下载 " + ModBase.GetUuid() + "#", new List<NetFile>() { new NetFile(new[] { Url }, LocalFile, Check, UseBrowserUserAgent) });
            try
            {
                NewTask.WaitForExit(LoaderToSyncProgress: LoaderToSyncProgress);
            }
            catch (Exception ex)
            {
                throw new WebException($"多线程直接下载文件失败（{Url}）", ex);
            }
            finally
            {
                NewTask.Abort();
            }
        }

        /// <summary>
        /// 简单的多线程下载文件。可以下载 CDN 中的文件。
        /// </summary>
        /// <param name="Urls">文件的 Url 列表。</param>
        /// <param name="LocalFile">下载的本地地址。</param>
        public static void NetDownloadByLoader(IEnumerable<string> Urls, string LocalFile, ModLoader.LoaderBase LoaderToSyncProgress = null, ModBase.FileChecker Check = null, bool UseBrowserUserAgent = false)
        {
            var NewTask = new LoaderDownload("文件下载 " + ModBase.GetUuid() + "#", new List<NetFile>() { new NetFile(Urls, LocalFile, Check, UseBrowserUserAgent) });
            try
            {
                NewTask.WaitForExit(LoaderToSyncProgress: LoaderToSyncProgress);
            }
            catch (Exception ex)
            {
                throw new WebException($"多线程直接下载文件失败（第一下载源：" + Urls.First() + "）", ex);
            }
            finally
            {
                NewTask.Abort();
            }
        }

        /// <summary>
        /// 发送一个网络请求并获取返回内容，会重试三次并在最长 45s 后超时。
        /// </summary>
        /// <param name="Url">请求的服务器地址。</param>
        /// <param name="Method">请求方式（POST 或 GET）。</param>
        /// <param name="Data">请求的内容。</param>
        /// <param name="ContentType">请求的套接字类型。</param>
        /// <param name="DontRetryOnRefused">当返回 40x 时不重试。</param>
        public static string NetRequestRetry(string Url, string Method, object Data, string ContentType, bool DontRetryOnRefused = true, Dictionary<string, string> Headers = null)
        {
            int RetryCount = 0;
            Exception RetryException = null;
            long StartTime = ModBase.GetTimeTick();
            try
            {
            Retry:
                ;

                switch (RetryCount)
                {
                    case 0: // 正常尝试
                        {
                            return NetRequestOnce(Url, Method, Data, ContentType, 15000, Headers);
                        }
                    case 1: // 慢速重试
                        {
                            Thread.Sleep(500);
                            return NetRequestOnce(Url, Method, Data, ContentType, 25000, Headers); // 快速重试
                        }

                    default:
                        {
                            if (ModBase.GetTimeTick() - StartTime > 5500L)
                            {
                                // 若前两次加载耗费 5 秒以上，才进行重试
                                Thread.Sleep(500);
                                return NetRequestOnce(Url, Method, Data, ContentType, 4000, Headers);
                            }
                            else
                            {
                                throw RetryException;
                            }
                        }
                }
            }
            catch (ThreadInterruptedException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex.InnerException is not null && ex.InnerException.Message.Contains("(40") && DontRetryOnRefused)
                    throw;
                switch (RetryCount)
                {
                    case 0:
                        {
                            if (ModBase.ModeDebug)
                                ModBase.Log(ex, "[Net] 网络请求第一次失败（" + Url + "）");
                            RetryException = ex;
                            RetryCount += 1;
                            goto Retry;
                            break;
                        }
                    case 1:
                        {
                            if (ModBase.ModeDebug)
                                ModBase.Log(ex, "[Net] 网络请求第二次失败（" + Url + "）");
                            RetryCount += 1;
                            goto Retry;
                            break;
                        }

                    default:
                        {
                            throw;
                        }
                }
            }
        }
        /// <summary>
        /// 同时发送多个网络请求并要求返回内容。
        /// </summary>
        public static object NetRequestMultiple(string Url, string Method, object Data, string ContentType, int RequestCount = 4, Dictionary<string, string> Headers = null, bool MakeLog = true)
        {
            var Threads = new List<Thread>();
            object RequestResult = null;
            Exception RequestEx = null;
            int FailCount = 0;
            for (int i = 1, loopTo = RequestCount; i <= loopTo; i++)
            {
                var th = new Thread(() => { try { RequestResult = NetRequestOnce(Url, Method, Data, ContentType, 30000, Headers, MakeLog); } catch (Exception ex) { FailCount += 1; RequestEx = ex; } });
                th.Start();
                Threads.Add(th);
                Thread.Sleep(i * 250);
                if (RequestResult is not null)
                    goto RequestFinished;
            }
            while (true)
            {
                if (RequestResult is not null)
                {
                RequestFinished:
                    ;

                    foreach (var th in Threads)
                    {
                        if (th.IsAlive)
                            th.Interrupt();
                    }
                    return RequestResult;
                }
                else if (FailCount == RequestCount)
                {
                    foreach (var th in Threads)
                    {
                        if (th.IsAlive)
                            th.Interrupt();
                    }
                    throw RequestEx;
                }
                Thread.Sleep(20);
            }
            throw new Exception("未知错误");
        }
        /// <summary>
        /// 发送一次网络请求并获取返回内容。
        /// </summary>
        public static string NetRequestOnce(string Url, string Method, object Data, string ContentType, int Timeout = 25000, Dictionary<string, string> Headers = null, bool MakeLog = true, bool UseBrowserUserAgent = false)
        {
            if (ModBase.RunInUi() && !Url.Contains("//127."))
                throw new Exception("在 UI 线程执行了网络请求");
            Url = Conversions.ToString(ModSecret.SecretCdnSign(Url));
            if (MakeLog)
                ModBase.Log("[Net] 发起网络请求（" + Method + "，" + Url + "），最大超时 " + Timeout);
            HttpWebRequest Req;
            string Res = "";
            try
            {
                Req = (HttpWebRequest)WebRequest.Create(Url);
                Req.Proxy = (IWebProxy)GetProxy();
                Req.Method = Method;
                byte[] SendData = null;
                if (!(Data == null))
                {
                    if (Data is byte[])
                    {
                        SendData = (byte[])Data;
                    }
                    else if (Data is string)
                    {
                        SendData = new UTF8Encoding(false).GetBytes((string)Data);
                    }
                    else
                    {
                        throw new ArgumentException("Data 参数类型不支持");
                    }
                }
                if (Headers is not null)
                {
                    foreach (var Pair in Headers)
                        Req.Headers.Add(Pair.Key, Pair.Value);
                }
                Req.ContentType = ContentType;
                Req.Timeout = Timeout;
                ModSecret.SecretHeadersSign(Url, ref Req, UseBrowserUserAgent);
                if (Url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                    Req.ProtocolVersion = HttpVersion.Version11;
                if (Method == "POST" || Method == "PUT")
                {
                    if (!(SendData == null))
                    {
                        Req.ContentLength = SendData.Length;
                        using (var DataStream = Req.GetRequestStream())
                        {
                            DataStream.WriteTimeout = Timeout;
                            DataStream.ReadTimeout = Timeout;
                            DataStream.Write(SendData, 0, SendData.Length);
                        }
                    }
                }
                using (var Resp = Req.GetResponse())
                {
                    using (var DataStream = Resp.GetResponseStream())
                    {
                        DataStream.WriteTimeout = Timeout;
                        DataStream.ReadTimeout = Timeout;
                        using (var Reader = new StreamReader(DataStream))
                        {
                            Res = Reader.ReadToEnd();
                        }
                    }
                }
                return Res;
            }
            catch (ThreadInterruptedException ex)
            {
                throw;
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.Timeout)
                {
                    throw new WebException($"连接服务器超时，请检查你的网络环境是否良好（{ex.Message}，{Url}）", ex);
                }
                else
                {
                    Stream RespStream = null;
                    try
                    {
                        if (ex.Response is not null)
                        {
                            RespStream = ex.Response.GetResponseStream();
                            if (RespStream is not null)
                            {
                                using (var Reader = new StreamReader(RespStream))
                                {
                                    Res = Reader.ReadToEnd();
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        if (RespStream is not null)
                            RespStream.Dispose();
                    }
                    if (string.IsNullOrEmpty(Res))
                    {
                        throw new WebException($"网络请求失败（{ex.Status}，{ex.Message}，{Url}）", ex);
                    }
                    else
                    {
                        throw new ResponsedWebException($"服务器返回错误（{ex.Status}，{ex.Message}，{Url}）{Constants.vbCrLf}{Res}", Res, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                var nx = new WebException("网络请求失败（" + Url + "）", ex);
                if (MakeLog && !string.IsNullOrEmpty(Res))
                    ModBase.Log(nx, "NetRequestOnce 请求失败", ModBase.LogLevel.Developer);
                throw nx;
            }
        }
        public class ResponsedWebException : WebException
        {
            /// <summary>
            /// 远程服务器给予的回复。
            /// </summary>
            public new string Response { get; set; }
            public ResponsedWebException(string Message, string Response, Exception InnerException) : base(Message, InnerException)
            {
                this.Response = Response;
            }
        }

        /// <summary>
        /// 最大线程数。
        /// </summary>
        public static int NetTaskThreadLimit;
        /// <summary>
        /// 速度下限。
        /// </summary>
        public static long NetTaskSpeedLimitLow = 256L * 1024L; // 256K/s
        /// <summary>
        /// 速度上限。若无限制则为 -1。
        /// </summary>
        public static long NetTaskSpeedLimitHigh = -1;
        /// <summary>
        /// 基于限速，当前可以下载的剩余量。
        /// </summary>
        public static long NetTaskSpeedLimitLeft = -1;
        private readonly static object NetTaskSpeedLimitLeftLock = new object();
        private static long NetTaskSpeedLimitLeftLast;
        /// <summary>
        /// 正在运行中的线程数。
        /// </summary>
        public static int NetTaskThreadCount = 0;
        private readonly static object NetTaskThreadCountLock = new object();

        /// <summary>
        /// 下载源。
        /// </summary>
        public class NetSource
        {
            public int Id;
            public string Url;
            public int FailCount;
            public Exception Ex;
            public NetThread Thread;
            public bool IsFailed;
            public override string ToString()
            {
                return Url;
            }
        }
        /// <summary>
        /// 下载进度标示。
        /// </summary>
        public enum NetState
        {
            /// <summary>
            /// 尚未进行已存在检查。
            /// </summary>
            WaitForCheck = -1,
            /// <summary>
            /// 尚未开始。
            /// </summary>
            WaitForDownload = 0,
            /// <summary>
            /// 正在连接，尚未获取文件大小。
            /// </summary>
            Connect = 1,
            /// <summary>
            /// 已获取文件大小，尚未有有效下载。
            /// </summary>
            Get = 2,
            /// <summary>
            /// 正在下载。
            /// </summary>
            Download = 3,
            /// <summary>
            /// 正在合并文件。
            /// </summary>
            Merge = 4,
            /// <summary>
            /// 不进行下载，因为已发现现存的文件。
            /// </summary>
            WaitForCopy = 5,
            /// <summary>
            /// 已完成。
            /// </summary>
            Finish = 6,
            /// <summary>
            /// 已失败或中断。
            /// </summary>
            Error = 7
        }
        /// <summary>
        /// 预下载检查行为。
        /// </summary>
        public enum NetPreDownloadBehaviour
        {
            /// <summary>
            /// 当文件已存在时，显示提示以提醒用户是否继续下载。
            /// </summary>
            HintWhileExists,
            /// <summary>
            /// 当文件已存在或正在下载时，直接退出下载函数执行，不对用户进行提示。
            /// </summary>
            ExitWhileExistsOrDownloading,
            /// <summary>
            /// 不进行已存在检查。
            /// </summary>
            IgnoreCheck
        }

        /// <summary>
        /// 下载线程。
        /// </summary>
        public class NetThread : IEnumerable<NetThread>
        {

            /// <summary>
            /// 对应的下载任务。
            /// </summary>
            public NetFile Task;
            /// <summary>
            /// 对应的线程。
            /// </summary>
            public Thread Thread;
            /// <summary>
            /// 链表中的下一个线程。
            /// </summary>
            public NetThread NextThread;
            private IEnumerable<NetThread> Next
            {
                get
                {
                    var CurrentChain = this;
                    while (CurrentChain is not null)
                    {
                        yield return CurrentChain;
                        CurrentChain = CurrentChain.NextThread;
                    }
                }
            }
            public IEnumerator<NetThread> GetEnumerator()
            {
                return Next.GetEnumerator();
            }
            private IEnumerator IEnumerable_GetEnumerator()
            {
                return Next.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => IEnumerable_GetEnumerator();

            /// <summary>
            /// 分配给任务中每个线程（无论其是否失败）的编号。
            /// </summary>
            public int Uuid;
            /// <summary>
            /// 是否为第一个线程。
            /// </summary>
            public bool IsFirstThread
            {
                get
                {
                    return DownloadStart == 0L && Task.FileSize == -2;
                }
            }
            /// <summary>
            /// 该线程的缓存文件。
            /// </summary>
            public string Temp;

            /// <summary>
            /// 线程下载起始位置。
            /// </summary>
            public long DownloadStart;
            /// <summary>
            /// 线程下载结束位置。
            /// </summary>
            public long DownloadEnd
            {
                get
                {
                    lock (Task.LockChain)
                    {
                        if (NextThread is null)
                        {
                            if (Task.IsUnknownSize)
                            {
                                return 5 * 1024 * 1024 * 1024L; // 5G
                            }
                            else
                            {
                                return Task.FileSize - 1L;
                            }
                        }
                        else
                        {
                            return NextThread.DownloadStart - 1L;
                        }
                    }
                }
            }
            /// <summary>
            /// 线程未下载的文件大小。
            /// </summary>
            public long DownloadUndone
            {
                get
                {
                    return DownloadEnd - (DownloadStart + DownloadDone) + 1L;
                }
            }
            /// <summary>
            /// 线程已下载的文件大小。
            /// </summary>
            public long DownloadDone = 0L;

            /// <summary>
            /// 上次记速时的时间。
            /// </summary>
            private long SpeedLastTime = ModBase.GetTimeTick();
            /// <summary>
            /// 上次记速时的已下载大小。
            /// </summary>
            private long SpeedLastDone = 0L;
            /// <summary>
            /// 当前的下载速度，单位为 Byte / 秒。
            /// </summary>
            public long Speed
            {
                get
                {
                    if (ModBase.GetTimeTick() - SpeedLastTime > 200L)
                    {
                        long DeltaTime = ModBase.GetTimeTick() - SpeedLastTime;
                        _Speed = (long)Math.Round((DownloadDone - SpeedLastDone) / (DeltaTime / 1000d));
                        SpeedLastDone = DownloadDone;
                        SpeedLastTime += DeltaTime;
                    }
                    return _Speed;
                }
            }
            private long _Speed = 0L;

            /// <summary>
            /// 线程初始化时的时间。
            /// </summary>
            public long InitTime = ModBase.GetTimeTick();
            /// <summary>
            /// 上次接受到有效数据的时间，-1 表示尚未有有效数据。
            /// </summary>
            public long LastReceiveTime = -1;

            /// <summary>
            /// 当前线程的状态。
            /// </summary>
            public NetState State = NetState.WaitForDownload;
            /// <summary>
            /// 是否已经结束。
            /// </summary>
            public bool IsEnded
            {
                get
                {
                    return State == NetState.Finish || State == NetState.Error;
                }
            }

            /// <summary>
            /// 当前选取的是哪一个 Url。
            /// </summary>
            public NetSource Source;

        }
        /// <summary>
        /// 下载单个文件。
        /// </summary>
        public class NetFile
        {

            #region 属性

            /// <summary>
            /// 所属的文件列表任务。
            /// </summary>
            public ModBase.SafeList<LoaderDownload> Tasks = new ModBase.SafeList<LoaderDownload>();
            /// <summary>
            /// 所有下载源。
            /// </summary>
            public ModBase.SafeList<NetSource> Sources;
            /// <summary>
            /// 用于在第一个线程出错时切换下载源。
            /// </summary>
            private int FirstThreadSource = 0;
            /// <summary>
            /// 所有已经被标记为失败的，但未完整尝试过的，不允许断点续传的下载源。
            /// </summary>
            public ModBase.SafeList<NetSource> SourcesOnce = new ModBase.SafeList<NetSource>();
            /// <summary>
            /// 获取从某个源开始，第一个可用的源。
            /// </summary>
            private NetSource GetSource(int Id = 0)
            {
                if (Id >= Sources.Count || Id < 0)
                    Id = 0;
                lock (LockSource)
                {
                    if (!IsSourceFailed(false))
                    {
                        // 存在多线程可用源
                        var CurrentSource = Sources[Id];
                        while (CurrentSource.IsFailed)
                        {
                            Id += 1;
                            if (Id >= Sources.Count)
                                Id = 0;
                            CurrentSource = Sources[Id];
                        }
                        return CurrentSource;
                    }
                    else if (SourcesOnce.Any())
                    {
                        // 仅存在单线程可用源
                        return SourcesOnce[0];
                    }
                    else
                    {
                        // 没有可用源
                        return null;
                    }
                }
            }
            /// <summary>
            /// 是否已经没有可用源了。
            /// </summary>
            public bool IsSourceFailed(bool AllowOnceSource = true)
            {
                if (AllowOnceSource && SourcesOnce.Any())
                    return false;
                lock (LockSource)
                {
                    foreach (NetSource Source in Sources)
                    {
                        if (!Source.IsFailed)
                            return false;
                    }
                }
                return true;
            }

            /// <summary>
            /// 存储在本地的带文件名的地址。
            /// </summary>
            public string LocalPath = null;
            /// <summary>
            /// 存储在本地的文件名。
            /// </summary>
            public string LocalName = null;

            /// <summary>
            /// 当前的下载状态。
            /// </summary>
            public NetState State = NetState.WaitForCheck;
            /// <summary>
            /// 导致下载失败的原因。
            /// </summary>
            public List<Exception> Ex = new List<Exception>();

            /// <summary>
            /// 作为文件组成部分的线程链表。
            /// 如果没有线程，可以为 Nothing。
            /// </summary>
            public NetThread Threads;

            /// <summary>
            /// 文件的总大小。若为 -2 则为未获取，若为 -1 则为无法获取准确大小。
            /// </summary>
            public long FileSize = -2;
            /// <summary>
            /// 该文件是否无法获取准确大小。
            /// </summary>
            public bool IsUnknownSize = false;
            /// <summary>
            /// 该文件是否不需要分割。
            /// </summary>
            public bool IsNoSplit
            {
                get
                {
                    return IsUnknownSize || FileSize < FilePieceLimit;
                }
            }
            /// <summary>
            /// 为不需要分割的小文件进行临时存储。
            /// </summary>
            private Queue<byte> SmailFileCache;

            /// <summary>
            /// 文件的已下载大小。
            /// </summary>
            public long DownloadDone = 0L;
            private readonly object LockDone = new object();
            /// <summary>
            /// 文件的校验规则。
            /// </summary>
            public ModBase.FileChecker Check;
            /// <summary>
            /// 下载时是否添加浏览器 UA。
            /// </summary>
            public bool UseBrowserUserAgent;

            /// <summary>
            /// 上次记速时的时间。
            /// </summary>
            private long SpeedLastTime = ModBase.GetTimeTick();
            /// <summary>
            /// 上次记速时的已下载大小。
            /// </summary>
            private long SpeedLastDone = 0L;
            /// <summary>
            /// 当前的下载速度，单位为 Byte / 秒。
            /// </summary>
            public long Speed
            {
                get
                {
                    if (ModBase.GetTimeTick() - SpeedLastTime > 200L)
                    {
                        long DeltaTime = ModBase.GetTimeTick() - SpeedLastTime;
                        _Speed = (long)Math.Round((DownloadDone - SpeedLastDone) / (DeltaTime / 1000d));
                        SpeedLastDone = DownloadDone;
                        SpeedLastTime += DeltaTime;
                    }
                    return _Speed;
                }
            }
            private long _Speed = 0L;

            /// <summary>
            /// 该文件是否由本地文件直接拷贝完成。
            /// </summary>
            public bool IsCopy = false;
            /// <summary>
            /// 本文件的显示进度。
            /// </summary>
            public double Progress
            {
                get
                {
                    switch (State)
                    {
                        case NetState.WaitForCheck:
                            {
                                return 0d;
                            }
                        case NetState.WaitForCopy:
                            {
                                return 0.2d;
                            }
                        case NetState.WaitForDownload:
                            {
                                return 0.01d;
                            }
                        case NetState.Connect:
                            {
                                return 0.02d;
                            }
                        case NetState.Get:
                            {
                                return 0.04d;
                            }
                        case NetState.Download:
                            {
                                // 正在下载中，对应 5% ~ 98%
                                double OriginalProgress = IsUnknownSize ? 0.5d : DownloadDone / (double)Math.Max(FileSize, 1L);
                                OriginalProgress = 1d - Math.Pow(1d - OriginalProgress, 0.9d);
                                return OriginalProgress * 0.93d + 0.05d;
                            }
                        case NetState.Merge:
                            {
                                return 0.99d;
                            }
                        case NetState.Finish:
                        case NetState.Error:
                            {
                                return 1d;
                            }

                        default:
                            {
                                throw new ArgumentOutOfRangeException("文件状态未知：" + ((int)State).ToString());
                            }
                    }
                }
            }

            /// <summary>
            /// 各个线程建立连接成功的总次数。
            /// </summary>
            private int ConnectCount = 0;
            /// <summary>
            /// 各个线程建立连接成功的总时间。
            /// </summary>
            private long ConnectTime = 0L;
            /// <summary>
            /// 各个线程建立连接成功的平均时间，单位为毫秒，-1 代表尚未有成功连接。
            /// </summary>
            private int ConnectAverage
            {
                get
                {
                    lock (LockCount)
                        return (int)Math.Round(ConnectCount == 0 ? -1 : ConnectTime / (double)ConnectCount);
                }
            }

            private const long FilePieceLimit = 262144L;
            public readonly object LockCount = new object();
            public readonly object LockState = new object();
            public readonly object LockChain = new object();
            public readonly object LockSource = new object();

            public readonly int Uuid = ModBase.GetUuid();
            public override bool Equals(object obj)
            {
                NetFile @file = obj as NetFile;
                return @file is not null && Uuid == @file.Uuid;
            }

            #endregion

            /// <summary>
            /// 新建一个需要下载的文件。
            /// </summary>
            /// <param name="LocalPath">包含文件名的本地地址。</param>
            public NetFile(IEnumerable<string> Urls, string LocalPath, ModBase.FileChecker Check = null, bool UseBrowserUserAgent = false)
            {
                var Sources = new List<NetSource>();
                int Count = 0;
                Urls = Urls.Distinct().ToArray();
                foreach (string Source in Urls)
                {
                    Sources.Add(new NetSource() { FailCount = 0, Url = Conversions.ToString(ModSecret.SecretCdnSign(Source.Replace(Constants.vbCr, "").Replace(Constants.vbLf, "").Trim())), Id = Count, IsFailed = false, Ex = null });
                    Count += 1;
                }
                this.Sources = Sources;
                this.LocalPath = LocalPath;
                this.Check = Check;
                this.UseBrowserUserAgent = UseBrowserUserAgent;
                LocalName = ModBase.GetFileNameFromPath(LocalPath);
            }

            /// <summary>
            /// 尝试开始一个新的下载线程。
            /// 如果失败，返回 Nothing。
            /// </summary>
            public NetThread TryBeginThread()
            {
                try
                {

                    // 条件检测
                    if (NetTaskThreadCount >= NetTaskThreadLimit || IsSourceFailed() || IsNoSplit && Threads is not null && Threads.State != NetState.Error)
                        return null;
                    if (State >= NetState.Merge || State == NetState.WaitForCheck)
                        return null;
                    lock (LockState)
                    {
                        if (State < NetState.Connect)
                            State = NetState.Connect;
                    }
                    // 初始化参数
                    long StartPosition;
                    NetSource StartSource = null;
                    Thread Th;
                    NetThread ThreadInfo;
                    lock (LockChain)
                    {
                        // 获取线程起点与下载源
                        // 不分割
                        if (IsNoSplit)
                            goto Capture;
                        // 单线程
                        if (IsSourceFailed(false))
                        {
                            // 确认没有其他线程正使用此点
                            if (SourcesOnce[0].Thread is not null && SourcesOnce[0].Thread.State != NetState.Error)
                                return null;
                            // 占用此点
                            Capture:
                            ;

                            SmailFileCache = null;
                            Threads = null;
                            NetManager.DownloadDone -= DownloadDone;
                            lock (LockDone)
                                DownloadDone = 0L;
                            SpeedLastDone = 0L;
                            State = NetState.Get;
                        }
                        // 首个开始点
                        if (Threads is null)
                        {
                            StartPosition = 0L;
                            StartSource = GetSource(FirstThreadSource);
                            FirstThreadSource = StartSource.Id + 1;
                            goto StartThread;
                        }
                        // 寻找失败点
                        foreach (NetThread Thread in Threads)
                        {
                            if (Thread.State == NetState.Error && Thread.DownloadUndone > 0L)
                            {
                                StartPosition = Thread.DownloadStart + Thread.DownloadDone;
                                StartSource = GetSource(Thread.Source.Id + 1);
                                goto StartThread;
                            }
                        }
                        // 是否禁用多线程，以及规定碎片大小
                        string TargetUrl = GetSource().Url;
                        if (TargetUrl.Contains("pcl2-server") || TargetUrl.Contains("bmclapi") || TargetUrl.Contains("github.com") || TargetUrl.Contains("optifine.net") || TargetUrl.Contains("modrinth") || TargetUrl.Contains("gitcode"))
                            return null;
                        // 寻找最大碎片
                        // FUTURE: 下载引擎重做，计算下载源平均链接时间和线程下载速度，按最高时间节省来开启多线程
                        var FilePieceMax = Threads;
                        foreach (NetThread Thread in Threads)
                        {
                            if (Thread.DownloadUndone > FilePieceMax.DownloadUndone)
                                FilePieceMax = Thread;
                        }
                        if (FilePieceMax is null || FilePieceMax.DownloadUndone < FilePieceLimit)
                            return null;
                        StartPosition = (long)Math.Round(FilePieceMax.DownloadEnd - FilePieceMax.DownloadUndone * 0.4d);
                        StartSource = GetSource();

                    // 开始线程
                    StartThread:
                        ;

                        if (StartPosition > FileSize && FileSize >= 0L && !IsUnknownSize || StartPosition < 0L || StartSource == null)
                            return null;
                        // 构建线程
                        int ThreadUuid = ModBase.GetUuid();
                        if (!Tasks.Any())
                            return null; // 由于中断，已没有可用任务
                        Th = new Thread((_) => this.Thread()) { Name = $"NetTask {Tasks[0].Uuid}/{Uuid} Download {ThreadUuid}#", Priority = ThreadPriority.BelowNormal };
                        ThreadInfo = new NetThread() { Uuid = ThreadUuid, DownloadStart = StartPosition, Thread = Th, Source = StartSource, Task = this, State = NetState.WaitForDownload };
                        // 链表处理
                        if (ThreadInfo.IsFirstThread || Threads is null)
                        {
                            Threads = ThreadInfo;
                        }
                        else
                        {
                            var CurrentChain = Threads;
                            while (CurrentChain.DownloadEnd <= StartPosition)
                                CurrentChain = CurrentChain.NextThread;
                            ThreadInfo.NextThread = CurrentChain.NextThread;
                            CurrentChain.NextThread = ThreadInfo;
                        }

                    }
                    // 开始线程
                    lock (NetTaskThreadCountLock)
                        NetTaskThreadCount += 1;
                    lock (LockSource)
                    {
                        if (IsSourceFailed(false))
                            SourcesOnce[0].Thread = ThreadInfo;
                    }
                    Th.Start(ThreadInfo);
                    return ThreadInfo;
                }

                catch (Exception ex)
                {
                    ModBase.Log(ex, "尝试开始下载线程失败（" + (LocalName ?? "Nothing") + "）", ModBase.LogLevel.Hint);
                    return null;
                }
            }
            /// <summary>
            /// 每个下载线程执行的代码。
            /// </summary>
            private void Thread(NetThread Info)
            {
                if (ModBase.ModeDebug || Info.DownloadStart == 0L)
                    ModBase.Log("[Download] " + LocalName + " " + Info.Uuid + "#：开始，起始点 " + Info.DownloadStart + "，" + Info.Source.Url);
                HttpWebRequest HttpRequest;
                Stream ResultStream = null;
                // 部分下载源真的特别慢，并且只需要一个请求，例如 Ping 为 20s，如果增长太慢，就会造成类似 2.5s 5s 7.5s 10s 12.5s... 的极大延迟
                // 延迟过长会导致某些特别慢的链接迟迟不被掐死
                int Timeout = Math.Min(Math.Max(ConnectAverage, 6000) * (1 + Info.Source.FailCount), 30000);
                Info.State = NetState.Connect;
                try
                {
                    int HttpDataCount = 0;
                    if (SourcesOnce.Contains(Info.Source) && !Info.Equals(Info.Source.Thread))
                        goto SourceBreak;
                    // 请求头
                    HttpRequest = (HttpWebRequest)WebRequest.Create(Info.Source.Url);
                    if (Info.Source.Url.StartsWithF("https", true))
                        HttpRequest.ProtocolVersion = HttpVersion.Version11;
                    HttpRequest.Proxy = (IWebProxy)GetProxy();
                    HttpRequest.Timeout = Timeout;
                    HttpRequest.AddRange(Info.DownloadStart);
                    ModSecret.SecretHeadersSign(Info.Source.Url, ref HttpRequest, UseBrowserUserAgent);
                    long ContentLength = 0L;
                    using (HttpWebResponse HttpResponse = (HttpWebResponse)HttpRequest.GetResponse())
                    {
                        if (State == NetState.Error)
                            goto SourceBreak; // 快速中断
                        if (ModBase.ModeDebug && (HttpResponse.ResponseUri.OriginalString ?? "") != (Info.Source.Url ?? ""))
                        {
                            ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：重定向至 {HttpResponse.ResponseUri.OriginalString}");
                        }
                        // '从响应头获取文件名
                        // If Info.IsFirstThread Then
                        // Dim FileName As String = GetFileNameFromResponse(HttpResponse)
                        // If ModeDebug Then Log($"[Download] {LocalName} {Info.Uuid}#：远程文件名：{If(FileName, "未提供")}")
                        // If FileName IsNot Nothing AndAlso LocalName = "待定" Then
                        // LocalName = FileName
                        // Log($"[Download] {LocalName} {Info.Uuid}#：从响应头获取到文件名")
                        // End If
                        // End If
                        // 文件大小校验
                        ContentLength = HttpResponse.ContentLength;
                        if (ContentLength == -1)
                        {
                            if (FileSize > 1L)
                            {
                                if (Info.DownloadStart == 0L)
                                {
                                    ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：文件大小未知，但已从其他下载源获取，不作处理");
                                }
                                else
                                {
                                    ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：ContentLength 返回了 -1，无法确定是否支持分段下载，视作不支持");
                                    goto NotSupportRange;
                                }
                            }
                            else
                            {
                                FileSize = -1;
                                IsUnknownSize = true;
                                ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：文件大小未知");
                            }
                        }
                        else if (ContentLength < 0L)
                        {
                            throw new Exception("获取片大小失败，结果为 " + ContentLength + "。");
                        }
                        else if (Info.IsFirstThread)
                        {
                            if (Check is not null)
                            {
                                if (ContentLength < Check.MinSize && Check.MinSize > 0L)
                                {
                                    throw new Exception($"文件大小不足，获取结果为 {ContentLength}，要求至少为 {Check.MinSize}。");
                                }
                                if (ContentLength != Check.ActualSize && Check.ActualSize > 0L)
                                {
                                    throw new Exception($"文件大小不一致，获取结果为 {ContentLength}，要求必须为 {Check.ActualSize}。");
                                }
                            }
                            FileSize = ContentLength;
                            IsUnknownSize = false;
                            ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：文件大小 {ContentLength}（{ModBase.GetString(ContentLength)}）");
                            // 若文件大小大于 50 M，进行剩余磁盘空间校验
                            if (ContentLength > 50 * 1024 * 1024)
                            {
                                foreach (DriveInfo Drive in DriveInfo.GetDrives())
                                {
                                    string DriveName = Drive.Name.First().ToString();
                                    double RequiredSpace = (ModBase.PathTemp.StartsWithF(DriveName) ? ContentLength * 1.1d : 0d) + (LocalPath.StartsWithF(DriveName) ? ContentLength + 5 * 1024 * 1024 : 0L);
                                    if (Drive.TotalFreeSpace < RequiredSpace)
                                    {
                                        throw new Exception(DriveName + " 盘空间不足，无法进行下载。" + Constants.vbCrLf + "需要至少 " + ModBase.GetString((long)Math.Round(RequiredSpace)) + " 空间，但当前仅剩余 " + ModBase.GetString(Drive.TotalFreeSpace) + "。" + (ModBase.PathTemp.StartsWithF(DriveName) ? Constants.vbCrLf + Constants.vbCrLf + "下载时需要与文件同等大小的空间存放缓存，你可以在设置中调整缓存文件夹的位置。" : ""));
                                    }
                                }
                            }
                        }
                        else if (FileSize < 0L)
                        {
                            throw new Exception("非首线程运行时，尚未获取文件大小");
                        }
                        else if (Info.DownloadStart > 0L && ContentLength == FileSize)
                        {
                        NotSupportRange:
                            ;

                            lock (LockSource)
                            {
                                if (SourcesOnce.Contains(Info.Source))
                                {
                                    goto SourceBreak;
                                }
                                else
                                {
                                    SourcesOnce.Add(Info.Source);
                                }
                            }
                            throw new WebException($"该下载源不支持分段下载：Range 起始于 {Info.DownloadStart}，预期 ContentLength 为 {FileSize - Info.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}");
                        }
                        else if (!(FileSize - Info.DownloadStart == ContentLength))
                        {
                            throw new WebException($"获取到的分段大小不一致：Range 起始于 {Info.DownloadStart}，预期 ContentLength 为 {FileSize - Info.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}");
                        }
                        // Log($"[Download] {LocalName} {Info.Uuid}#：通过大小检查，文件大小 {FileSize}，起始点 {Info.DownloadStart}，ContentLength {ContentLength}")
                        Info.State = NetState.Get;
                        lock (LockState)
                        {
                            if (State < NetState.Get)
                                State = NetState.Get;
                        }
                        // 创建缓存文件
                        if (IsNoSplit)
                        {
                            Info.Temp = null;
                            SmailFileCache = new Queue<byte>();
                        }
                        else
                        {
                            Info.Temp = $@"{ModBase.PathTemp}Download\{Uuid}_{Info.Uuid}_{ModBase.RandomInteger(0, 999999)}.tmp";
                            ResultStream = new FileStream(Info.Temp, FileMode.Create, FileAccess.Write, FileShare.Read);
                        }
                        // 开始下载
                        using (var HttpStream = HttpResponse.GetResponseStream())
                        {
                            HttpStream.ReadTimeout = Timeout;
                            if (Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugDelay")))
                                System.Threading.Thread.Sleep(ModBase.RandomInteger(50, 3000));
                            byte[] HttpData = new byte[16385];
                            HttpDataCount = HttpStream.Read(HttpData, 0, 16384);
                            while ((IsUnknownSize || Info.DownloadUndone > 0L) && HttpDataCount > 0 && !ModBase.IsProgramEnded && State < NetState.Merge && (!Info.Source.IsFailed || Info.Equals(Info.Source.Thread))) // 判断是否下载完成
                            {
                                // 限速
                                while (NetTaskSpeedLimitHigh > 0L && NetTaskSpeedLimitLeft <= 0L)
                                    System.Threading.Thread.Sleep(16);
                                int RealDataCount = (int)(IsUnknownSize ? HttpDataCount : Math.Min(HttpDataCount, Info.DownloadUndone));
                                lock (NetTaskSpeedLimitLeftLock)
                                {
                                    if (NetTaskSpeedLimitHigh > 0L)
                                        NetTaskSpeedLimitLeft -= RealDataCount;
                                }
                                long DeltaTime = ModBase.GetTimeTick() - Info.LastReceiveTime;
                                if (DeltaTime > 1000000L)
                                    DeltaTime = 1L; // 时间刻反转导致出现极大值
                                if (RealDataCount > 0)
                                {
                                    // 有数据
                                    if (Info.DownloadDone == 0L)
                                    {
                                        // 第一次接受到数据
                                        Info.State = NetState.Download;
                                        lock (LockState)
                                        {
                                            if (State < NetState.Download)
                                                State = NetState.Download;
                                        }
                                        lock (LockCount)
                                        {
                                            ConnectCount += 1;
                                            ConnectTime += ModBase.GetTimeTick() - Info.InitTime;
                                        }
                                    }
                                    lock (LockCount)
                                    {
                                        Info.Source.FailCount = 0;
                                        foreach (var Task in Tasks)
                                            Task.FailCount = 0;
                                    }
                                    NetManager.DownloadDone += RealDataCount;
                                    lock (LockDone)
                                        DownloadDone += RealDataCount;
                                    Info.DownloadDone += RealDataCount;
                                    if (IsNoSplit)
                                    {
                                        if (HttpData.Count() == RealDataCount)
                                        {
                                            // SmailFileCache.AddRange(HttpData)
                                            foreach (var B in HttpData)
                                                SmailFileCache.Enqueue(B);
                                        }
                                        else
                                        {
                                            // SmailFileCache.AddRange(HttpData.ToList.GetRange(0, RealDataCount))
                                            for (int i = 0, loopTo = RealDataCount - 1; i <= loopTo; i++)
                                                SmailFileCache.Enqueue(HttpData[i]);
                                        }
                                    }
                                    else
                                    {
                                        ResultStream.Write(HttpData, 0, RealDataCount);
                                    }
                                    // 检查速度是否过慢
                                    if (DeltaTime > 1500L && DeltaTime > RealDataCount) // 数据包间隔大于 1.5s，且速度小于 1.5K/s
                                    {
                                        throw new TimeoutException("由于速度过慢断开链接，下载 " + RealDataCount + " B，消耗 " + DeltaTime + " ms。");
                                    }
                                    Info.LastReceiveTime = ModBase.GetTimeTick();
                                    // 已完成
                                    if (Info.DownloadUndone == 0L && !IsUnknownSize)
                                        break;
                                }
                                else if (Info.LastReceiveTime > 0L && DeltaTime > Timeout)
                                {
                                    // 无数据，且已超时
                                    throw new TimeoutException("操作超时，无数据。");
                                }
                                HttpDataCount = HttpStream.Read(HttpData, 0, 16384);
                            }
                        }
                    }

                SourceBreak:
                    ;

                    if (State == NetState.Error || Info.Source.IsFailed || Info.DownloadUndone > 0L && !IsUnknownSize)
                    {
                        // 被外部中断
                        Info.State = NetState.Error;
                        ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：中断");
                    }
                    else if (HttpDataCount == 0 && Info.DownloadUndone > 0L && !IsUnknownSize)
                    {
                        // 服务器无返回数据
                        throw new Exception($"返回的 ContentLength 过多：ContentLength 为 {ContentLength}，但获取到的总数据量仅为 {Info.DownloadDone}（全文件总数据量 {DownloadDone}）");
                    }
                    else
                    {
                        // 本线程完成
                        Info.State = NetState.Finish;
                        if (ModBase.ModeDebug)
                            ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：完成，已下载 {Info.DownloadDone}");
                    }
                }
                catch (Exception ex)
                {
                    // 状态变更
                    lock (LockCount)
                    {
                        Info.Source.FailCount += 1;
                        foreach (var Task in Tasks)
                            Task.FailCount += 1;
                    }
                    string IsTimeoutString = ModBase.GetExceptionSummary(ex).ToLower().Replace(" ", "");
                    bool IsTimeout = IsTimeoutString.Contains("由于连接方在一段时间后没有正确答复或连接的主机没有反应") || IsTimeoutString.Contains("超时") || IsTimeoutString.Contains("timeout") || IsTimeoutString.Contains("timedout");
                    ModBase.Log("[Download] " + LocalName + " " + Info.Uuid + (IsTimeout ? "#：超时（" + Timeout * 0.001d + "s）" : "#：出错，" + ModBase.GetExceptionDetail(ex)));
                    Info.State = NetState.Error;
                    // '使用该下载源的线程是否没有速度
                    // '下载超时也会导致没有速度，容易误判下载失败，所以已弃用此方法
                    // Dim IsNoSpeed As Boolean = True
                    // SyncLock LockChain
                    // If Threads IsNot Nothing Then
                    // For Each Thread As NetThread In Threads
                    // If Thread.Source.Id = Info.Source.Id AndAlso Thread.Speed > 0 Then
                    // IsNoSpeed = False
                    // Exit For
                    // End If
                    // Next
                    // End If
                    // End SyncLock
                    Info.Source.Ex = ex;
                    // 根据情况判断，是否在多线程下禁用下载源（连续错误过多，或不支持断点续传）
                    if (ex.Message.Contains("该下载源不支持") || ex.Message.Contains("未能解析") || ex.Message.Contains("(404)") || ex.Message.Contains("(502)") || ex.Message.Contains("无返回数据") || ex.Message.Contains("空间不足") || ex.Message.Contains("获取到的分段大小不一致") || ex.Message.Contains("(403)") && !Info.Source.Url.ContainsF("bmclapi") || Info.Source.FailCount >= ModBase.MathClamp(NetTaskThreadLimit, 5d, 30d) && DownloadDone < 1L || Info.Source.FailCount > NetTaskThreadLimit + 2) // BMCLAPI 的部分源在高频率请求下会返回 403，所以不应因此禁用下载源
                    {
                        bool IsThisFail = false;
                        lock (LockSource)
                        {
                            if (Info.Source.Thread is not null && Info.Source.Thread.Equals(Info))
                            {
                                // 单线程下，本线程出错
                                SourcesOnce.RemoveAt(0);
                                goto Wrong;
                            }
                            else if (!Info.Source.IsFailed)
                            {
                            // 多线程下，本线程出错
                            Wrong:
                                ;

                                Info.Source.IsFailed = true;
                                IsThisFail = true;
                            }
                        }
                        // 本线程引发下载源被禁用
                        if (IsThisFail)
                        {
                            ModBase.Log($"[Download] {LocalName} {Info.Uuid}#：下载源被禁用（{Info.Source.Id}）：{Info.Source.Url}");
                            ModBase.Log(ex, "下载源 " + Info.Source.Id + " 已被禁用", ex.Message.Contains("不支持分段下载") || ex.Message.Contains("(404)") || ex.Message.Contains("(416)") ? ModBase.LogLevel.Developer : ModBase.LogLevel.Debug);
                            if (IsSourceFailed())
                            {
                                // 没有可用源
                                ModBase.Log("[Download] 文件 " + LocalName + " 已无可用下载源");
                                Exception ExampleEx = null;
                                lock (LockSource)
                                {
                                    foreach (NetSource Source in Sources)
                                    {
                                        ModBase.Log("[Download] 已禁用的下载源：" + Source.Url);
                                        if (Source.Ex is not null)
                                        {
                                            ExampleEx = Source.Ex;
                                            ModBase.Log(Source.Ex, "下载源禁用原因", ModBase.LogLevel.Developer);
                                        }
                                    }
                                }
                                Fail(ExampleEx);
                            }
                            else if (ex.Message.Contains("空间不足"))
                            {
                                // 没有空间
                                Fail(ex);
                            }
                        }
                    }
                    // 首线程错误
                    if (FileSize == -2)
                    {
                        lock (LockChain)
                            Threads = null;
                    }
                }
                finally
                {
                    if (ResultStream is not null)
                        ResultStream.Dispose();
                    lock (NetTaskThreadCountLock)
                        NetTaskThreadCount -= 1;
                    // 可能在没有下载完的时候开始合并文件了，这造成了大多数合并失败
                    if ((FileSize >= 0L && DownloadDone >= FileSize || FileSize == -1 && DownloadDone > 0L) && State < NetState.Merge)
                        Merge();
                }
            }
            /// <summary>
            /// 从 HTTP 响应头中获取文件名。
            /// 如果没有，返回 Nothing。
            /// </summary>
            private string GetFileNameFromResponse(HttpWebResponse response)
            {
                string header = response.Headers["Content-Disposition"];
                if (string.IsNullOrEmpty(header))
                    return null;
                // attachment; filename="filename.ext"
                if (!header.Contains("filename="))
                    return null;
                return header.AfterLast("filename=").Trim('"', ' ').BeforeFirst(";");
            }

            // 下载文件的最终收束事件
            /// <summary>
            /// 下载完成。合并文件。
            /// </summary>
            private void Merge()
            {
                // 状态判断
                lock (LockState)
                {
                    if (State < NetState.Merge)
                    {
                        State = NetState.Merge;
                    }
                    else
                    {
                        return;
                    }
                }
                int RetryCount = 0;
                Stream MergeFile = null;
                BinaryWriter AddWriter = null;
                try
                {
                Retry:
                    ;

                    lock (LockChain)
                    {
                        // 创建文件夹
                        if (File.Exists(LocalPath))
                            File.Delete(LocalPath);
                        var Info = new FileInfo(LocalPath);
                        Info.Directory.Create();
                        // 合并文件
                        if (IsNoSplit)
                        {
                            // 仅有一个线程，从缓存中输出
                            if (ModBase.ModeDebug)
                                ModBase.Log($"[Download] {LocalName}：下载结束，从缓存输出文件，长度：" + SmailFileCache.Count);
                            MergeFile = new FileStream(LocalPath, FileMode.Create);
                            AddWriter = new BinaryWriter(MergeFile);
                            AddWriter.Write(SmailFileCache.ToArray());
                            AddWriter.Dispose();
                            AddWriter = null;
                            MergeFile.Dispose();
                            MergeFile = null;
                        }
                        else if (Threads.DownloadDone == DownloadDone && Threads.Temp is not null)
                        {
                            // 仅有一个文件，直接复制
                            if (ModBase.ModeDebug)
                                ModBase.Log($"[Download] {LocalName}：下载结束，仅有一个文件，无需合并");
                            ModBase.CopyFile(Threads.Temp, LocalPath);
                        }
                        else
                        {
                            // 有多个线程，合并
                            if (ModBase.ModeDebug)
                                ModBase.Log($"[Download] {LocalName}：下载结束，开始合并文件");
                            MergeFile = new FileStream(LocalPath, FileMode.Create);
                            AddWriter = new BinaryWriter(MergeFile);
                            foreach (NetThread Thread in Threads)
                            {
                                if (Thread.DownloadDone == 0L || Thread.Temp is null)
                                    continue;
                                using (var fs = new FileStream(Thread.Temp, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    using (var TempReader = new BinaryReader(fs))
                                    {
                                        AddWriter.Write(TempReader.ReadBytes((int)Thread.DownloadDone));
                                    }
                                }
                            }
                            AddWriter.Dispose();
                            AddWriter = null;
                            MergeFile.Dispose();
                            MergeFile = null;
                        }
                        // 写入大小要求
                        if (!IsUnknownSize && Check is not null)
                        {
                            if (Check.ActualSize == -1)
                            {
                                Check.ActualSize = FileSize;
                            }
                            else if (Check.ActualSize != FileSize)
                            {
                                throw new Exception($"文件大小不一致：任务要求为 {Check.ActualSize} B，网络获取结果为 {FileSize}B");
                            }
                        }
                        // 检查文件
                        string CheckResult = Check?.Check(LocalPath);
                        if (CheckResult is not null)
                        {
                            ModBase.Log($"[Download] {LocalName} 文件校验失败，下载线程细节：");
                            foreach (NetThread Th in Threads)
                                ModBase.Log($"[Download]     {Th.Uuid}#，状态 {ModBase.GetStringFromEnum(Th.State)}，范围 {Th.DownloadStart}~{Th.DownloadStart + Th.DownloadDone}，完成 {Th.DownloadDone}，剩余 {Th.DownloadUndone}");
                            throw new Exception(CheckResult);
                        }
                        // 后处理
                        if (IsNoSplit)
                        {
                            SmailFileCache = null;
                        }
                        else
                        {
                            foreach (NetThread Thread in Threads)
                            {
                                if (Thread.Temp is not null)
                                    File.Delete(Thread.Temp);
                            }
                        }
                        Finish();
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "合并文件出错（" + LocalName + "）");
                    if (MergeFile is not null)
                    {
                        MergeFile.Dispose();
                        MergeFile = null;
                    }
                    if (AddWriter is not null)
                    {
                        AddWriter.Dispose();
                        AddWriter = null;
                    }
                    // 重试
                    if (RetryCount <= 3)
                    {
                        System.Threading.Thread.Sleep(ModBase.RandomInteger(500, 1000));
                        RetryCount += 1;
                        goto Retry;
                    }
                    Fail(ex);
                }
            }
            /// <summary>
            /// 下载失败。
            /// </summary>
            private void Fail(Exception RaiseEx = null)
            {
                lock (LockState)
                {
                    if (State >= NetState.Finish)
                        return;
                    if (RaiseEx is not null)
                        Ex.Add(RaiseEx);
                    // 凉凉
                    State = NetState.Error;
                }
                InterruptAndDelete();
                foreach (var Task in Tasks)
                    Task.OnFileFail(this);
            }
            /// <summary>
            /// 下载中断。
            /// </summary>
            public void Abort(LoaderDownload CausedByTask)
            {
                // 从特定任务中移除，如果它还属于其他任务，则继续下载
                Tasks.Remove(CausedByTask);
                if (Tasks.Any())
                    return;
                // 确认中断
                lock (LockState)
                {
                    if (State >= NetState.Finish)
                        return;
                    State = NetState.Error;
                }
                InterruptAndDelete();
            }
            private void InterruptAndDelete()
            {
                if (File.Exists(LocalPath))
                    File.Delete(LocalPath);
                lock (NetManager.LockRemain)
                {
                    NetManager.FileRemain -= 1;
                    ModBase.Log($"[Download] {LocalName}：状态 {State}，剩余文件 {NetManager.FileRemain}");
                }
            }

            // 状态改变接口
            /// <summary>
            /// 将该文件设置为已下载完成。
            /// </summary>
            public void Finish(bool PrintLog = true)
            {
                lock (LockState)
                {
                    if (State >= NetState.Finish)
                        return;
                    State = NetState.Finish;
                }
                lock (NetManager.LockRemain)
                {
                    NetManager.FileRemain -= 1;
                    if (PrintLog)
                        ModBase.Log("[Download] " + LocalName + "：已完成，剩余文件 " + NetManager.FileRemain);
                }
                foreach (var Task in Tasks)
                    Task.OnFileFinish(this);
            }

        }
        /// <summary>
        /// 下载一系列文件的加载器。
        /// </summary>
        public class LoaderDownload : ModLoader.LoaderBase
        {

            #region 属性

            /// <summary>
            /// 需要下载的文件。
            /// </summary>
            public ModBase.SafeList<NetFile> Files;
            /// <summary>
            /// 剩余未完成的文件数。（用于减轻 FilesLock 的占用）
            /// </summary>
            private int FileRemain;
            private readonly object FileRemainLock = new object();

            /// <summary>
            /// 用于显示的百分比进度。
            /// </summary>
            public override double Progress
            {
                get
                {
                    if (State >= ModBase.LoadState.Finished)
                        return 1d;
                    if (!Files.Any())
                        return 0d; // 必须返回 0，否则在获取列表的时候会错觉已经下载完了
                    return _Progress;
                }
                set
                {
                    throw new Exception("文件下载不允许指定进度");
                }
            }
            private double _Progress = 0d;

            /// <summary>
            /// 任务中的文件的连续失败计数。
            /// </summary>
            public int FailCount
            {
                get
                {
                    return _FailCount;
                }
                set
                {
                    _FailCount = value;
                    if (State == ModBase.LoadState.Loading && value >= Math.Min(10000d, Math.Max(FileRemain * 5.5d, NetTaskThreadLimit * 5.5d + 3d)))
                    {
                        ModBase.Log("[Download] 由于同加载器中失败次数过多引发强制失败：连续失败了 " + value + " 次", ModBase.LogLevel.Debug);
                        ;
#error Cannot convert OnErrorResumeNextStatementSyntax - see comment for details
                        /* Cannot convert OnErrorResumeNextStatementSyntax, CONVERSION ERROR: Conversion for OnErrorResumeNextStatement not implemented, please report this issue in 'On Error Resume Next' at character 80101


                                                Input:
                                                                    On Error Resume Next

                                                 */
                        var ExList = new List<Exception>();
                        foreach (var File in Files)
                        {
                            foreach (var Source in File.Sources)
                            {
                                if (Source.Ex is not null)
                                {
                                    ExList.Add(Source.Ex);
                                    if (ExList.Count > 10)
                                        goto FinishExCatch;
                                }
                            }
                        }

                    FinishExCatch:
                        ;

                        OnFail(ExList);
                    }
                }
            }
            private int _FailCount = 0;

            #endregion

            /// <summary>
            /// 刷新公开属性。由 NetManager 每 0.1 秒调用一次。
            /// </summary>
            public void RefreshStat()
            {
                // 计算进度
                double NewProgress = 0d;
                double TotalProgress = 0d;
                foreach (var File in Files)
                {
                    if (File.IsCopy)
                    {
                        NewProgress += File.Progress * 0.2d;
                        TotalProgress += 0.2d;
                    }
                    else
                    {
                        NewProgress += File.Progress;
                        TotalProgress += 1d;
                    }
                }
                if (TotalProgress > 0d && !double.IsNaN(TotalProgress))
                    NewProgress /= TotalProgress;
                // 刷新进度
                _Progress = NewProgress;
            }

            public LoaderDownload(string Name, List<NetFile> FileTasks)
            {
                this.Name = Name;
                Files = new ModBase.SafeList<NetFile>(FileTasks);
            }
            public override void Start(object Input = null, bool IsForceRestart = false)
            {
                if (Input is not null)
                    Files = new ModBase.SafeList<NetFile>((IEnumerable<NetFile>)Input);
                // 去重
                var ResultArray = new ModBase.SafeList<NetFile>();
                for (int i = 0, loopTo = Files.Count - 1; i <= loopTo; i++)
                {
                    for (int ii = i + 1, loopTo1 = Files.Count - 1; ii <= loopTo1; ii++)
                    {
                        if ((Files[i].LocalPath ?? "") == (Files[ii].LocalPath ?? ""))
                            goto NextElement;
                    }
                    ResultArray.Add(Files[i]);
                NextElement:
                    ;

                }
                Files = ResultArray;
                // 设置剩余文件数
                lock (FileRemainLock)
                {
                    foreach (var File in Files)
                    {
                        if (File.State != NetState.Finish)
                            FileRemain += 1;
                    }
                }
                State = ModBase.LoadState.Loading;
                // 开始执行
                // 输入检测
                // 文件夹检测
                // 接入下载管理器
                // 将文件分配给多个线程以进行已存在查找
                // 最多 5 个线程，最少每个线程分配 10 个文件
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        if (!Files.Any())
                        { OnFinish(); return; }
                        foreach (NetFile File in Files)
                        {
                            if (File is null) throw new ArgumentException("存在空文件请求！");
                            foreach (NetSource Source in File.Sources)
                            {
                                if (!(Source.Url.StartsWithF("https://", true) || Source.Url.StartsWithF("http://", true)))
                                {
                                    Source.Ex = new ArgumentException("输入的下载链接不正确！");
                                    Source.IsFailed = true;
                                }
                            }
                            if (File.IsSourceFailed()) throw new ArgumentException("输入的下载链接不正确！");
                            if (!File.LocalPath.ToLower().Contains(@":\")) throw new ArgumentException("输入的本地文件地址不正确！");
                            if (File.LocalPath.EndsWithF(@"\")) throw new ArgumentException("请输入含文件名的完整文件路径！");
                            string DirPath = new FileInfo(File.LocalPath).Directory.FullName;
                            if (!Directory.Exists(DirPath)) Directory.CreateDirectory(DirPath);
                        }
                        NetManager.Start(this);
                        var Folders = new List<string>();
                        var FoldersFinal = new List<string>();
                        if (!(bool)ModBase.Setup.Get("SystemDebugSkipCopy"))
                        {
                            Folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\");
                            foreach (var Folder in ModMinecraft.McFolderList) Folders.Add(Folder.Path); Folders = Folders.Distinct().ToList();
                            foreach (var Folder in Folders)
                            {
                                if ((Folder ?? "") != (ModMinecraft.PathMcFolder ?? "") && Directory.Exists(Folder)) FoldersFinal.Add(Folder);
                            }
                        }
                        int FilesPerThread = (int)Math.Round(Math.Max(5d, Files.Count / 10d + 1d));
                        var FilesInThread = new List<NetFile>(); foreach (var File in Files)
                        {
                            FilesInThread.Add(File);
                            if (FilesInThread.Count == FilesPerThread)
                            {
                                var FilesToRun = new List<NetFile>();
                                FilesToRun.AddRange(FilesInThread); ModBase.RunInNewThread(() => StartCopy(FilesToRun, FoldersFinal), "NetTask FileCopy " + Uuid);
                                FilesInThread.Clear();
                            }
                        }
                        if (FilesInThread.Any())
                        {
                            var FilesToRun = new List<NetFile>();
                            FilesToRun.AddRange(FilesInThread);
                            ModBase.RunInNewThread(() => StartCopy(FilesToRun, FoldersFinal), "NetTask FileCopy " + Uuid);
                            FilesInThread.Clear();
                        }
                    }
                    catch (Exception ex) { OnFail(new List<Exception>() { ex }); }
                }, "NetTask " + Uuid + " Main"); // 可能会用于已存在查找的文件夹列表
                                                 // 最终用于查找的列表
                                                 // 在设置中禁用复制
                                                 // 总是添加官启文件夹，因为 HMCL 会把所有文件存在这里
            }
            private void StartCopy(List<NetFile> Files, List<string> FolderList)
            {
                try
                {
                    if (ModBase.ModeDebug)
                        ModBase.Log("[Download] 检查线程分配文件数：" + Files.Count + "，线程名：" + Thread.CurrentThread.Name);
                    // 试图从已存在的 Minecraft 文件夹中寻找目标文件
                    var ExistFiles = new List<KeyValuePair<NetFile, string>>(); // {NetFile, Target As String}
                    foreach (NetFile File in Files)
                    {
                        string ExistFilePath = null;
                        // 判断是否有已存在的文件
                        if (File.Check is not null && ModMinecraft.McFolderList is not null && ModMinecraft.PathMcFolder is not null && File.Check.CanUseExistsFile && File.LocalPath.StartsWithF(ModMinecraft.PathMcFolder))
                        {
                            string Relative = File.LocalPath.Replace(ModMinecraft.PathMcFolder, "");
                            foreach (var Folder in FolderList)
                            {
                                string Target = Folder + Relative;
                                if (File.Check.Check(Target) is null)
                                {
                                    ExistFilePath = Target;
                                    break;
                                }
                            }
                        }
                        // 若存在，则改变状态
                        lock (LockState)
                        {
                            if (ExistFilePath is not null)
                            {
                                File.State = NetState.WaitForCopy;
                                File.IsCopy = true;
                                ExistFiles.Add(new KeyValuePair<NetFile, string>(File, ExistFilePath));
                            }
                            else
                            {
                                File.State = NetState.WaitForDownload;
                                File.IsCopy = false;
                            }
                        }
                    }
                    // 复制已存在的文件
                    foreach (var FileToken in ExistFiles)
                    {
                        var File = FileToken.Key;
                        lock (LockState)
                        {
                            if (File.State > NetState.WaitForCopy)
                                return;
                        }
                        string LocalPath = FileToken.Value;
                        int RetryCount = 0;
                    Retry:
                        ;

                        try
                        {
                            ModBase.Log("[Download] 复制已存在的文件（" + LocalPath + "）");
                            ModBase.CopyFile(LocalPath, File.LocalPath);
                            File.Finish(false);
                        }
                        catch (Exception ex)
                        {
                            RetryCount += 1;
                            ModBase.Log(ex, string.Format("复制已存在的文件失败，重试第 {2} 次（{0} -> {1}）", LocalPath, File.LocalPath, RetryCount));
                            if (RetryCount < 3)
                            {
                                Thread.Sleep(200);
                                goto Retry;
                            }
                            File.State = NetState.WaitForDownload;
                            File.IsCopy = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "下载已存在文件查找失败", ModBase.LogLevel.Feedback);
                }
            }

            public void OnFileFinish(NetFile File)
            {
                // 要求全部文件完成
                lock (FileRemainLock)
                {
                    FileRemain -= 1;
                    if (FileRemain > 0)
                        return;
                }
                OnFinish();
            }
            public void OnFinish()
            {
                RaisePreviewFinish();
                lock (LockState)
                {
                    if (State > ModBase.LoadState.Loading)
                        return;
                    State = ModBase.LoadState.Finished;
                }
            }
            public void OnFileFail(NetFile File)
            {
                // 将下载源的错误加入主错误列表
                foreach (var Source in File.Sources)
                {
                    if (!(Source.Ex == null))
                        File.Ex.Add(Source.Ex);
                }
                OnFail(File.Ex);
            }
            public void OnFail(List<Exception> ExList)
            {
                lock (LockState)
                {
                    if (State > ModBase.LoadState.Loading)
                        return;
                    if (ExList is null || !ExList.Any())
                        ExList = new List<Exception>() { new Exception("未知错误！") };
                    // 寻找第一个不是 404 的下载源
                    var UsefulExs = ExList.Where(e => !e.Message.Contains("(404)")).ToList();
                    Error = UsefulExs.Any() ? UsefulExs[0] : ExList[0];
                    // 获取实际失败的文件
                    foreach (var File in Files)
                    {
                        if (File.State == NetState.Error)
                        {
                            Error = new Exception("文件下载失败：" + File.LocalPath + Constants.vbCrLf + File.Sources.Select(s => s.Ex is null ? s.Url : s.Ex.Message + "（" + s.Url + "）").Join(Constants.vbCrLf), Error);
                            break;
                        }
                    }
                    // 在设置 Error 对象后再更改为失败，避免 WaitForExit 无法捕获错误
                    State = ModBase.LoadState.Failed;
                }
                // 中断所有文件
                foreach (var TaskFile in Files)
                {
                    if (TaskFile.State < NetState.Merge)
                        TaskFile.State = NetState.Error;
                }
                // 在退出同步锁后再进行日志输出
                var ErrOutput = new List<string>();
                foreach (Exception Ex in ExList)
                    ErrOutput.Add(ModBase.GetExceptionDetail(Ex));
                ModBase.Log("[Download] " + ErrOutput.Distinct().ToArray().Join(Constants.vbCrLf));
            }
            public override void Abort()
            {
                lock (LockState)
                {
                    if (State >= ModBase.LoadState.Finished)
                        return;
                    State = ModBase.LoadState.Aborted;
                }
                ModBase.Log("[Download] " + Name + " 已取消！");
                // 中断所有文件
                foreach (var TaskFile in Files)
                    TaskFile.Abort(this);
            }

        }

        public static NetManagerClass NetManager = new NetManagerClass();
        /// <summary>
        /// 下载文件管理。
        /// </summary>
        public class NetManagerClass
        {

            #region 属性

            /// <summary>
            /// 需要下载的文件。为“本地地址 - 文件对象”键值对。
            /// </summary>
            public Dictionary<string, NetFile> Files = new Dictionary<string, NetFile>();
            public readonly object LockFiles = new object();

            /// <summary>
            /// 当前的所有下载任务。
            /// </summary>
            public ModBase.SafeList<LoaderDownload> Tasks = new ModBase.SafeList<LoaderDownload>();

            /// <summary>
            /// 已下载完成的大小。
            /// </summary>
            public long DownloadDone
            {
                get
                {
                    return _DownloadDone;
                }
                set
                {
                    lock (LockDone)
                        _DownloadDone = value;
                }
            }
            private long _DownloadDone = 0L;
            private readonly object LockDone = new object();


            /// <summary>
            /// 尚未完成下载的文件数。
            /// </summary>
            public int FileRemain = 0;
            public readonly object LockRemain = new object();

            /// <summary>
            /// 上次记速时的已下载大小。
            /// </summary>
            private long SpeedLastDone = 0L;
            /// <summary>
            /// 至多最近 30 次下载速度的记录，较新的在前面。
            /// </summary>
            private List<long> SpeedLast = new List<long>();
            // 这些属性由 RefreshStat 刷新
            /// <summary>
            /// 当前的全局下载速度，单位为 Byte / 秒。
            /// </summary>
            public long Speed = 0L;

            public readonly int Uuid = ModBase.GetUuid();

            #endregion

            /// <summary>
            /// 进度与下载速度由下载管理线程每隔约 0.1 秒刷新一次。
            /// </summary>
            private void RefreshStat()
            {
                try
                {
                    long DeltaTime = ModBase.GetTimeTick() - RefreshStatLast;
                    if (DeltaTime == 0L)
                        return;
                    RefreshStatLast += DeltaTime;
                    #region 刷新整体速度
                    // 计算瞬时速度
                    double ActualSpeed = Math.Max(0d, (DownloadDone - SpeedLastDone) / (DeltaTime / 1000d));
                    SpeedLast.Insert(0, (long)Math.Round(ActualSpeed));
                    if (SpeedLast.Count >= 31)
                        SpeedLast.RemoveAt(30);
                    SpeedLastDone = DownloadDone;
                    // 计算用于显示的速度
                    long SpeedSum = 0L;
                    long SpeedDiv = 0L;
                    int Weight = SpeedLast.Count;
                    foreach (var SpeedRecord in SpeedLast)
                    {
                        SpeedSum += SpeedRecord * Weight;
                        SpeedDiv += Weight;
                        Weight -= 1;
                    }
                    Speed = (long)Math.Round(SpeedDiv > 0L ? SpeedSum / (double)SpeedDiv : 0d);
                    // 计算新的速度下限
                    long Limit = 0L;
                    if (SpeedLast.Count >= 10)
                        Limit = (long)Math.Round(SpeedLast.Take(10).Average() * 0.85d); // 取近 1 秒的平均速度的 85%
                    if (Limit > NetTaskSpeedLimitLow)
                    {
                        NetTaskSpeedLimitLow = Limit;
                        ModBase.Log("[Download] " + "速度下限已提升到 " + ModBase.GetString(Limit));
                    }
                    #endregion
                    #region 刷新下载任务属性
                    foreach (var Task in Tasks)
                        Task.RefreshStat();
                }
                #endregion
                catch (Exception ex)
                {
                    ModBase.Log(ex, "刷新下载公开属性失败");
                }
            }
            private long RefreshStatLast;

            /// <summary>
            /// 启动监控线程，用于新增下载线程。
            /// </summary>
            private void StartManager()
            {
                if (IsManagerStarted)
                    return;
                IsManagerStarted = true;
                // 获取文件列表
                // 为等待中的文件开始线程
                // 为进行中的文件追加线程
                // 线程种类计数
                // 新增线程
                void ThreadStarter(int Id) { try { while (true) { Thread.Sleep(20); List<NetFile> AllFiles; lock (LockFiles) { if (Id == 0 && FileRemain == 0 && Files.Any()) Files.Clear(); AllFiles = Files.Values.ToList(); } var WaitingFiles = new List<NetFile>(); var OngoingFiles = new List<NetFile>(); foreach (NetFile File in AllFiles) { if (File.Uuid % 2 == Id) continue; if (File.State == NetState.WaitForDownload) { WaitingFiles.Add(File); } else if (File.State < NetState.Merge) { OngoingFiles.Add(File); } } bool continueWhile = false; foreach (NetFile File in WaitingFiles) { if (NetTaskThreadCount >= NetTaskThreadLimit) { continueWhile = true; break; } var NewThread = File.TryBeginThread(); if (NewThread is not null && NewThread.Source.Url.Contains("bmclapi")) Thread.Sleep(30); } if (continueWhile) { continue; } if (Speed >= NetTaskSpeedLimitLow) continue; bool continueWhile1 = false; foreach (NetFile File in OngoingFiles) { if (NetTaskThreadCount >= NetTaskThreadLimit) { continueWhile1 = true; break; } int PreparingCount = 0; int DownloadingCount = 0; if (File.Threads is not null) { foreach (NetThread Thread in File.Threads.ToList()) { if (Thread.State < NetState.Download) { PreparingCount += 1; } else if (Thread.State == NetState.Download) { DownloadingCount += 1; } } } if (PreparingCount > DownloadingCount) continue; var NewThread = File.TryBeginThread(); if (NewThread is not null && NewThread.Source.Url.Contains("bmclapi")) Thread.Sleep(30); } if (continueWhile1) { continue; } } } catch (Exception ex) { ModBase.Log(ex, $"下载管理启动线程 {Id} 出错", ModBase.LogLevel.Assert); } }
                ; // 0 或 1
                  // 若已完成，则清空
                  // 最大线程数检查
                  // 减少 BMCLAPI 请求频率（目前每分钟限制 4000 次）
                  // 下载速度足够，无需新增
                  // 最大线程数检查
                  // 准备中的线程已多于下载中的线程，不再新增
                  // 减少 BMCLAPI 请求频率（目前每分钟限制 4000 次）
                ModBase.RunInNewThread(() => ThreadStarter(0), "NetManager ThreadStarter 0");
                ModBase.RunInNewThread(() => ThreadStarter(1), "NetManager ThreadStarter 1");
                // 增加限速余量
                // 刷新公开属性
                // 等待直至 80 ms
                ModBase.RunInNewThread(() => { try { long LastLoopTime; NetTaskSpeedLimitLeftLast = ModBase.GetTimeTick(); while (true) { long TimeNow = ModBase.GetTimeTick(); LastLoopTime = TimeNow; if (NetTaskSpeedLimitHigh > 0L) NetTaskSpeedLimitLeft = (long)Math.Round(NetTaskSpeedLimitHigh / 1000d * (TimeNow - NetTaskSpeedLimitLeftLast)); NetTaskSpeedLimitLeftLast = TimeNow; RefreshStat(); while (ModBase.GetTimeTick() - LastLoopTime < 80L) Thread.Sleep(10); } } catch (Exception ex) { ModBase.Log(ex, "下载管理刷新线程出错", ModBase.LogLevel.Assert); } }, "NetManager StatRefresher");
            }
            private bool IsManagerStarted = false;

            // Public FileRemainList As New List(Of String)
            private bool IsDownloadCacheCleared = false;
            /// <summary>
            /// 开始一个下载任务。
            /// </summary>
            public void Start(LoaderDownload Task)
            {
                StartManager();
                // 清理缓存
                if (!IsDownloadCacheCleared)
                {
                    try
                    {
                        ModBase.DeleteDirectory(ModBase.PathTemp + "Download");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "清理下载缓存失败");
                    }
                    IsDownloadCacheCleared = true;
                }
                Directory.CreateDirectory(ModBase.PathTemp + "Download");
                // 文件处理
                lock (LockFiles)
                {
                    // 添加每个文件
                    for (int i = 0, loopTo = Task.Files.Count - 1; i <= loopTo; i++)
                    {
                        var File = Task.Files[i];
                        if (Files.ContainsKey(File.LocalPath))
                        {
                            // 已有该文件
                            if (Files[File.LocalPath].State >= NetState.Finish)
                            {
                                // 该文件已经下载过一次，且下载完成
                                // 将已下载的文件替换成当前文件，重新下载
                                File.Tasks.Add(Task);
                                Files[File.LocalPath] = File;
                                lock (LockRemain)
                                {
                                    FileRemain += 1;
                                    if (ModBase.ModeDebug)
                                        ModBase.Log("[Download] " + File.LocalName + "：已替换列表，剩余文件 " + FileRemain);
                                    // FileRemainList.Add(File.LocalPath)
                                }
                            }
                            else
                            {
                                // 该文件正在下载中
                                // 将当前文件替换成下载中的文件，即两个任务指向同一个文件
                                File = Files[File.LocalPath];
                                File.Tasks.Add(Task);
                            }
                        }
                        else
                        {
                            // 没有该文件
                            File.Tasks.Add(Task);
                            Files.Add(File.LocalPath, File);
                            lock (LockRemain)
                            {
                                FileRemain += 1;
                                if (ModBase.ModeDebug)
                                    ModBase.Log("[Download] " + File.LocalName + "：已加入列表，剩余文件 " + FileRemain);
                                // FileRemainList.Add(File.LocalPath)
                            }
                        }
                        Task.Files[i] = File; // 回设
                    }
                }
                Tasks.Add(Task);
            }

        }

        /// <summary>
        /// 是否有正在进行中、需要在下载管理页面显示的下载任务？
        /// </summary>
        public static bool HasDownloadingTask(bool IgnoreCustomDownload = false)
        {
            foreach (var Task in ModLoader.LoaderTaskbar.ToList())
            {
                if (Task.Show && Task.State == ModBase.LoadState.Loading && (!IgnoreCustomDownload || !Task.Name.ToString().Contains("自定义下载")))
                {
                    return true;
                }
            }
            return false;
        }

    }
}