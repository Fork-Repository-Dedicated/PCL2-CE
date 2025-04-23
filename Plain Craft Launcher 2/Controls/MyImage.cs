using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public class MyImage : Image
    {

        #region 公开属性

        /// <summary>
    /// 网络图片的缓存有效期。
    /// 在这个时间后，才会重新尝试下载图片。
    /// </summary>
        public TimeSpan FileCacheExpiredTime = new TimeSpan(7, 0, 0, 0); // 7 天

        /// <summary>
    /// 是否允许将网络图片存储到本地用作缓存。
    /// </summary>
        public bool EnableCache
        {
            get
            {
                return Conversions.ToBoolean(GetValue(EnableCacheProperty));
            }
            set
            {
                SetValue(EnableCacheProperty, value);
            }
        }
        public static new readonly DependencyProperty EnableCacheProperty = DependencyProperty.Register("EnableCache", typeof(bool), typeof(MyImage), new PropertyMetadata(true));

        /// <summary>
    /// 与 Image 的 Source 类似。
    /// 若输入以 http 开头的字符串，则会尝试下载图片然后显示，图片会保存为本地缓存。
    /// 支持 WebP 格式的图片。
    /// </summary>
        public new string Source // 覆写 Image 的 Source 属性
        {
            get
            {
                return _Source;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = null;
                if ((_Source ?? "") == (value ?? ""))
                    return;
                _Source = value;
                if (!IsInitialized)
                    return; // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
                Load();
            }
        }
        private string _Source = "";
        public static new readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(string), typeof(MyImage), new PropertyMetadata(new PropertyChangedCallback((sender, e) => { if (sender is not null) ((MyImage)sender).Source = e.NewValue.ToString(); })));

        /// <summary>
    /// 当 Source 首次下载失败时，会从该备用地址加载图片。
    /// </summary>
        public string FallbackSource
        {
            get
            {
                return _FallbackSource;
            }
            set
            {
                _FallbackSource = value;
            }
        }
        private string _FallbackSource = null;

        /// <summary>
    /// 正在下载网络图片时显示的本地图片。
    /// </summary>
        public string LoadingSource
        {
            get
            {
                return _LoadingSource;
            }
            set
            {
                _LoadingSource = value;
            }
        }
        private string _LoadingSource = "pack://application:,,,/images/Icons/NoIcon.png";

        #endregion

        /// <summary>
    /// 实际被呈现的图片地址。
    /// </summary>
        public string ActualSource
        {
            get
            {
                return _ActualSource;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = null;
                if ((_ActualSource ?? "") == (value ?? ""))
                    return;
                _ActualSource = value;
                try
                {
                    var Bitmap = value is null ? null : new MyBitmap(value); // 在这里先触发可能的文件读取，尽量避免在 UI 线程中读取文件
                    ModBase.RunInUiWait(() => base.Source = Bitmap);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"加载图片失败（{value}）");
                    try
                    {
                        if (value.StartsWithF(ModBase.PathTemp) && File.Exists(value))
                            File.Delete(value);
                    }
                    catch
                    {
                    }
                }
            }
        }
        private string _ActualSource = null;

        public MyImage()
        {
            Initialized += (_, __) => Load();
        }

        private void Load()
        // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
        {
            // 空
            if (Source is null)
            {
                ActualSource = null;
                return;
            }
            // 本地图片
            if (!Source.StartsWithF("http"))
            {
                ActualSource = Source;
                return;
            }
            // 从缓存加载网络图片
            string Url = Source;
            bool Retried = false;
            string TempPath = GetTempPath(Url);
            var TempFile = new FileInfo(TempPath);
            bool EnableCache = this.EnableCache;
            if (EnableCache && TempFile.Exists)
            {
                ActualSource = TempPath;
                if (DateTime.Now - TempFile.LastWriteTime < FileCacheExpiredTime)
                    return; // 无需刷新缓存
            }
            ModBase.RunInNewThread(() =>
        {
            string TempDownloadingPath = null;
            try
            {
            RetryStart:

                // 下载
                ActualSource = LoadingSource; // 显示加载中图片
                TempDownloadingPath = TempPath + ModBase.RandomInteger(0, 10000000);
                Directory.CreateDirectory(ModBase.GetPathFromFullPath(TempPath)); // 重新实现下载，以避免携带 Header（#5072）
                using (var Client = new WebClient())
                {
                    Client.Proxy = (IWebProxy)ModNet.GetProxy();
                    Client.DownloadFile(Url, TempDownloadingPath);
                }
                if ((Url ?? "") != (Source ?? "") && (Url ?? "") != (FallbackSource ?? ""))
                {
                    // 已经更换了地址
                    File.Delete(TempDownloadingPath);
                }
                else if (EnableCache)
                {
                    // 保存缓存并显示
                    if (File.Exists(TempPath))
                        File.Delete(TempPath);
                    FileSystem.Rename(TempDownloadingPath, TempPath);
                    ModBase.RunInUi(() => ActualSource = TempPath);
                }
                else
                {
                    // 直接显示
                    ModBase.RunInUiWait(() => ActualSource = TempDownloadingPath);
                    File.Delete(TempDownloadingPath);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (TempPath is not null)
                        File.Delete(TempPath);
                    if (TempDownloadingPath is not null)
                        File.Delete(TempDownloadingPath);
                }
                catch
                {
                }
                if (!Retried)
                {
                    // 更换备用地址
                    ModBase.Log(ex, $"下载图片可重试地失败（{Url}）", ModBase.LogLevel.Developer);
                    Retried = true;
                    Url = FallbackSource ?? Source;
                    // 空
                    if (Url is null)
                    {
                        ActualSource = null;
                        return;
                    }
                    // 本地图片
                    if (!Url.StartsWithF("http"))
                    {
                        ActualSource = Url;
                        return;
                    }
                    // 从缓存加载网络图片
                    TempPath = GetTempPath(Url);
                    TempFile = new FileInfo(TempPath);
                    if (EnableCache && TempFile.Exists)
                    {
                        ActualSource = TempPath;
                        if (DateTime.Now - TempFile.CreationTime < FileCacheExpiredTime)
                            return; // 无需刷新缓存
                    }
                    // 下载
                    if ((Source ?? "") == (Url ?? ""))
                        Thread.Sleep(1000); // 延迟 1s 重试
                    goto RetryStart;
                }
                else
                {
                    ModBase.Log(ex, $"下载图片失败（{Url}）", ModBase.LogLevel.Hint);
                }
            }
        }, "MyImage PicLoader " + ModBase.GetUuid() + "#", ThreadPriority.BelowNormal);
        }
        public static string GetTempPath(string Url)
        {
            return $@"{ModBase.PathTemp}MyImage\{ModBase.GetHash(Url)}.png";
        }

    }
}