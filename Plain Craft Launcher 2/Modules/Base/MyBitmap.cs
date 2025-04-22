using System;
// 一个万能的自动图片类型转换工具类

using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCL
{

    public class MyBitmap
    {

        /// <summary>
    /// 位图缓存。
    /// </summary>
        public static System.Collections.Concurrent.ConcurrentDictionary<string, MyBitmap> BitmapCache = new System.Collections.Concurrent.ConcurrentDictionary<string, MyBitmap>();

        /// <summary>
    /// 存储的图片
    /// </summary>
        public System.Drawing.Bitmap Pic;

        // 自动类型转换
        // 支持的类：Image，ImageSource，Bitmap，ImageBrush，BitmapSource
        public static implicit operator MyBitmap(System.Drawing.Image Image)
        {
            if (Image is null)
                return null;
            return new MyBitmap(Image);
        }
        public static implicit operator System.Drawing.Image(MyBitmap Image)
        {
            if (Image is null)
                return null;
            return Image.Pic;
        }
        public static implicit operator MyBitmap(ImageSource Image)
        {
            if (Image is null)
                return null;
            return new MyBitmap(Image);
        }
        public static implicit operator ImageSource(MyBitmap Image)
        {
            if (Image is null)
                return null;
            var Bitmap = Image.Pic;
            var rect = new System.Drawing.Rectangle(0, 0, Bitmap.Width, Bitmap.Height);
            var bitmapData = Bitmap.LockBits(rect, ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int size = rect.Width * rect.Height * 4;
                return BitmapSource.Create(Bitmap.Width, Bitmap.Height, (double)Bitmap.HorizontalResolution, (double)Bitmap.VerticalResolution, PixelFormats.Bgra32, null, bitmapData.Scan0, size, bitmapData.Stride);
            }
            finally
            {
                Bitmap.UnlockBits(bitmapData);
            }
        }
        public static implicit operator MyBitmap(System.Drawing.Bitmap Image)
        {
            if (Image is null)
                return null;
            return new MyBitmap(Image);
        }
        public static implicit operator System.Drawing.Bitmap(MyBitmap Image)
        {
            if (Image is null)
                return null;
            return Image.Pic;
        }
        public static implicit operator MyBitmap(ImageBrush Image)
        {
            if (Image is null)
                return null;
            return new MyBitmap(Image);
        }
        public static implicit operator ImageBrush(MyBitmap Image)
        {
            if (Image is null)
                return null;
            return new ImageBrush(new MyBitmap(Image.Pic));
        }

        // 构造函数
        public MyBitmap()
        {
        }
        public MyBitmap(string FilePathOrResourceName)
        {
            do
            {
                try
                {
                    FilePathOrResourceName = FilePathOrResourceName.Replace("pack://application:,,,/images/", ModBase.PathImage);
                    if (FilePathOrResourceName.StartsWithF(ModBase.PathImage))
                    {
                        // 使用缓存
                        if (BitmapCache.ContainsKey(FilePathOrResourceName))
                        {
                            Pic = BitmapCache[FilePathOrResourceName].Pic;
                        }
                        else
                        {
                            Pic = new MyBitmap((ImageSource)new ImageSourceConverter().ConvertFromString(FilePathOrResourceName));
                            BitmapCache.TryAdd(FilePathOrResourceName, Pic);
                        }
                    }
                    else
                    {
                        // 使用这种自己接管 FileStream 的方法加载才能解除文件占用
                        using (var InputStream = new FileStream(FilePathOrResourceName, FileMode.Open))
                        {
                            // 判断是否为 WebP 文件头
                            var Header = new byte[2];
                            InputStream.Read(Header, 0, 2);
                            InputStream.Seek(0L, SeekOrigin.Begin);
                            if (Header[0] == 82 && Header[1] == 73)
                            {
                                // 读取 WebP
                                var FileBytes = new byte[(int)(InputStream.Length - 1L + 1)];
                                InputStream.Read(FileBytes, 0, FileBytes.Length);
                                Pic = WebPDecoder.DecodeFromBytes(FileBytes); // 将代码隔离在另外一个类中，这样只要不走进这个分支就不会加载 Imazen.WebP.dll
                            }
                            else
                            {
                                Pic = new System.Drawing.Bitmap(InputStream);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Pic = (System.Drawing.Bitmap)My.MyWpfExtension.Application.TryFindResource(FilePathOrResourceName);
                    if (Pic is null)
                    {
                        Pic = new System.Drawing.Bitmap(1, 1);
                        throw new Exception($"加载 MyBitmap 失败（{FilePathOrResourceName}）", ex);
                    }
                    else
                    {
                        ModBase.Log(ex, $"指定类型有误的 MyBitmap 加载（{FilePathOrResourceName}）", ModBase.LogLevel.Developer);
                        break;
                    }
                }
            }
            while (false);
        }
        public MyBitmap(ImageSource Image)
        {
            using (var MS = new MemoryStream())
            {
                var Encoder = new PngBitmapEncoder();
                Encoder.Frames.Add(BitmapFrame.Create((BitmapSource)Image));
                Encoder.Save(MS);
                Pic = new System.Drawing.Bitmap(MS);
            }
        }
        public MyBitmap(System.Drawing.Image Image)
        {
            Pic = (System.Drawing.Bitmap)Image;
        }
        public MyBitmap(System.Drawing.Bitmap Image)
        {
            Pic = Image;
        }
        public MyBitmap(ImageBrush Image)
        {
            using (var MS = new MemoryStream())
            {
                var Encoder = new BmpBitmapEncoder();
                Encoder.Frames.Add(BitmapFrame.Create((BitmapSource)Image.ImageSource));
                Encoder.Save(MS);
                Pic = new System.Drawing.Bitmap(MS);
            }
        }
        public class WebPDecoder // 将代码隔离在另外一个类中，这样只要不调用这个方法就不会加载 Imazen.WebP.dll
        {
            public static System.Drawing.Bitmap DecodeFromBytes(byte[] Bytes)
            {
                if (ModBase.Is32BitSystem)
                    throw new Exception("不支持在 32 位系统下加载 WebP 图片。");
                var Decoder = new Imazen.WebP.SimpleDecoder();
                return Decoder.DecodeFromBytes(Bytes, Bytes.Length);
            }
        }

        /// <summary>
    /// 获取裁切的图片，这个方法不会导致原对象改变且会返回一个新的对象。
    /// </summary>
        public MyBitmap Clip(int X, int Y, int Width, int Height)
        {
            var bmp = new System.Drawing.Bitmap(Width, Height, Pic.PixelFormat);
            bmp.SetResolution(Pic.HorizontalResolution, Pic.VerticalResolution);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.TranslateTransform(-X, -Y);
                g.DrawImage(Pic, new System.Drawing.Rectangle(0, 0, Pic.Width, Pic.Height));
            }
            return bmp;
        }

        /// <summary>
    /// 获取旋转或翻转后的图片，这个方法不会导致原对象改变且会返回一个新的对象。
    /// </summary>
        public MyBitmap RotateFlip(System.Drawing.RotateFlipType Type)
        {
            var bmp = new System.Drawing.Bitmap(Pic);
            bmp.SetResolution(Pic.HorizontalResolution, Pic.VerticalResolution);
            bmp.RotateFlip(Type);
            return bmp;
        }

        /// <summary>
    /// 将图像保存到文件。
    /// </summary>
        public void Save(string FilePath)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)this));
            using (var fileStream = new FileStream(FilePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }

    }
}