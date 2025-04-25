using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class MyMsgLogin
    {
        private JObject Data;
        private string UserCode; // 需要用户在网页上输入的设备代码
        private string DeviceCode; // 用于轮询的设备代码
        private string Website; // 验证网页的网址
        private string OAuthUrl = ""; // OAuth 轮询验证地址


        #region 弹窗

        private readonly ModMain.MyMsgBoxConverter MyConverter;
        private readonly int Uuid = ModBase.GetUuid();

        public MyMsgLogin(ModMain.MyMsgBoxConverter Converter)
        {
            try
            {
                this.InitializeComponent();
                this.Btn1.Name = this.Btn1.Name + ModBase.GetUuid();
                this.Btn2.Name = this.Btn2.Name + ModBase.GetUuid();
                this.Btn3.Name = this.Btn3.Name + ModBase.GetUuid();
                MyConverter = Converter;
                this.ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
                Data = (JObject)Converter.Content;
                OAuthUrl = Conversions.ToString(Converter.AuthUrl);
                Init();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "登录弹窗初始化失败", ModBase.LogLevel.Hint);
            }

            base.Loaded += Load;
        }

        private void Load(object sender, EventArgs e)
        {
            try
            {
                // 动画
                this.Opacity = 0d;
                ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsg, Panel.BackgroundProperty, (MyConverter.IsWarn ? new ModBase.MyColor(140d, 80d, 0d, 0d) : new ModBase.MyColor(90d, 0d, 0d, 0d)) - ModMain.FrmMain.PanMsg.Background, 200), "PanMsg Background");
                ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this, 1d, 120, 60), ModAnimation.AaDouble(i => this.TransformPos.Y = Conversions.ToDouble(this.TransformPos.Y + (double)i), -this.TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), ModAnimation.AaDouble(i => this.TransformRotate.Angle = Conversions.ToDouble(this.TransformRotate.Angle + (double)i), -this.TransformRotate.Angle, 300, 60, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)) }, "MyMsgBox " + Uuid);
                // 记录日志
                ModBase.Log("[Control] 登录弹窗：" + this.LabTitle.Text + Constants.vbCrLf + this.LabCaption.Text);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "登录弹窗加载失败", ModBase.LogLevel.Hint);
            }
        }
        private void Close()
        {
            // 动画
            ModAnimation.AniStart(new[] { ModAnimation.AaCode(() => { if (!ModMain.WaitingMyMsgBox.Any()) { ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsg, Panel.BackgroundProperty, new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsg.Background, 200, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))); } }, 30), ModAnimation.AaOpacity(this, -this.Opacity, 80, 20), ModAnimation.AaDouble(i => this.TransformPos.Y = Conversions.ToDouble(this.TransformPos.Y + (double)i), 20d - this.TransformPos.Y, 150, 0, new ModAnimation.AniEaseOutFluent()), ModAnimation.AaDouble(i => this.TransformRotate.Angle = Conversions.ToDouble(this.TransformRotate.Angle + (double)i), 6d - this.TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => ((Grid)this.Parent).Children.Remove(this), After: true) }, "MyMsgBox " + Uuid);
        }

        // 实现回车和 Esc 的接口（#4857）
        public void Btn1_Click()
        {
        }
        public void Btn3_Click()
        {
            Finished(new ThreadInterruptedException());
        }

        private void Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.GetPosition(this.ShapeLine).Y <= 2d)
                ModMain.FrmMain.DragMove();
        }

        #endregion

        private void Finished(object Result)
        {
            if (MyConverter.IsExited)
                return;
            MyConverter.IsExited = true;
            MyConverter.Result = Result;
            ModBase.RunInUi(Close);
            Thread.Sleep(200);
            ModMain.FrmMain.ShowWindowToTop();
        }

        private void Init()
        {
            UserCode = (string)Data["user_code"];
            DeviceCode = (string)Data["device_code"];
            if (Data["verification_uri_complete"] is not null)
            {
                Website = (string)Data["verification_uri_complete"];
                this.LabCaption.Text = $"登录网页将自动开启，授权码将自动填充。" + Constants.vbCrLf + Constants.vbCrLf + $"如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。" + Constants.vbCrLf + $"如果没有自动填充，请在页面内粘贴此授权码 {UserCode} （将自动复制）" + Constants.vbCrLf + $"你也可以用其他设备打开 {Website} 并输入授权码。";
            }
            else
            {
                Website = (string)Data["verification_uri"];
                this.LabCaption.Text = $"登录网页将自动开启，请在网页中输入授权码 {UserCode}（将自动复制）。" + Constants.vbCrLf + Constants.vbCrLf + $"如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。" + Constants.vbCrLf + $"你也可以用其他设备打开 {Website} 并输入上述授权码。";
            }
            // 设置 UI
            this.LabTitle.Text = "登录 Minecraft";
            this.Btn1.EventData = Website;
            this.Btn2.EventData = UserCode;
            // 启动工作线程
            ModBase.RunInNewThread(WorkThread, "MyMsgLogin");
        }

        private void WorkThread()
        {
            Thread.Sleep(3000);
            if (MyConverter.IsExited)
                return;
            ModBase.OpenWebsite(Website);
            ModBase.ClipboardSet(UserCode);
            Thread.Sleep((Data["interval"].ToObject<int>() - 1) * 1000);
            // 轮询
            int UnknownFailureCount = 0;
            while (!MyConverter.IsExited)
            {
                try
                {
                    string Scope = "";
                    string ClientId = "";
                    if (OAuthUrl.ToLower().Contains("microsoftonline.com"))
                    {
                        ClientId = ModSecret.OAuthClientId;
                        Scope = "scope=XboxLive.signin%20offline_access";
                    }
                    else
                    {
                        ClientId = ModSecret.LittleSkinClientId;
                    }
                    string Result = ModNet.NetRequestOnce(OAuthUrl, "POST", "grant_type=urn:ietf:params:oauth:grant-type:device_code" + "&" + "client_id=" + ClientId + "&" + "device_code=" + DeviceCode + "&" + Scope, "application/x-www-form-urlencoded", 5000 + UnknownFailureCount * 5000, MakeLog: false);
                    // 获取结果
                    JObject ResultJson = (JObject)ModBase.GetJson(Result);
                    ModLaunch.McLaunchLog($"令牌过期时间：{ResultJson["expires_in"]} 秒");
                    ModMain.Hint("网页登录成功！", ModMain.HintType.Finish);
                    Finished(new[] { ResultJson["access_token"].ToString(), ResultJson["refresh_token"].ToString() });
                    return;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("authorization_declined") | ex.Message.Contains("access_denied"))
                    {
                        Finished(new Exception("$你拒绝了 PCL 申请的权限……"));
                        return;
                    }
                    else if (ex.Message.Contains("expired_token"))
                    {
                        Finished(new Exception("$登录用时太长啦，重新试试吧！"));
                        return;
                    }
                    else if (ex.Message.Contains("AADSTS70000")) // 可能不能判 “invalid_grant”，见 #269
                    {
                        Finished(new ModBase.RestartException());
                        return;
                    }
                    else if (ex.Message.Contains("authorization_pending"))
                    {
                        Thread.Sleep(2000);
                    }
                    else if (UnknownFailureCount <= 2)
                    {
                        UnknownFailureCount += 1;
                        ModBase.Log(ex, $"登录轮询第 {UnknownFailureCount} 次失败");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        Finished(new Exception("登录轮询失败", ex));
                        return;
                    }
                }
            }
        }

    }
}