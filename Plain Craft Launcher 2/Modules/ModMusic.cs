using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Windows.Media;
using Windows.Storage.Streams;

namespace PCL
{
    public static class ModMusic
    {

        #region 播放列表

        /// <summary>
    /// 接下来要播放的音乐文件路径。未初始化时为 Nothing。
    /// </summary>
        public static List<string> MusicWaitingList = null;
        /// <summary>
    /// 全部音乐文件路径。未初始化时为 Nothing。
    /// </summary>
        public static List<string> MusicAllList = null;
        /// <summary>
    /// 初始化音乐播放列表。
    /// </summary>
    /// <param name="ForceReload">强制全部重新载入列表。</param>
    /// <param name="PreventFirst">在重载列表时避免让某项成为第一项。</param>
        private static void MusicListInit(bool ForceReload, string PreventFirst = null)
        {
            if (ForceReload)
                MusicAllList = null;
            try
            {
                // 初始化全部可用音乐列表
                if (MusicAllList is null)
                {
                    MusicAllList = new List<string>();
                    Directory.CreateDirectory(ModBase.Path + @"PCL\Musics\");
                    foreach (var File in ModBase.EnumerateFiles(ModBase.Path + @"PCL\Musics\"))
                    {
                        // 文件夹可能会被加入 .ini 文件夹配置文件、一些乱七八糟的 .jpg 文件啥的
                        string Ext = File.Extension.ToLower();
                        if (new[] { ".ini", ".jpg", ".txt", ".cfg", ".lrc", ".db", ".png" }.Contains(Ext))
                            continue;
                        MusicAllList.Add(File.FullName);
                    }
                }
                // 打乱顺序播放
                MusicWaitingList = (List<string>)(Conversions.ToBoolean(ModBase.Setup.Get("UiMusicRandom")) ? ModBase.Shuffle(new List<string>(MusicAllList)) : new List<string>(MusicAllList));
                if (PreventFirst is not null && (MusicWaitingList.FirstOrDefault() ?? "") == (PreventFirst ?? ""))
                {
                    // 若需要避免成为第一项的为第一项，则将它放在最后
                    MusicWaitingList.RemoveAt(0);
                    MusicWaitingList.Add(PreventFirst);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始化音乐列表失败", ModBase.LogLevel.Feedback);
            }
        }
        /// <summary>
    /// 获取下一首播放的音乐路径并将其从列表中移除。
    /// 如果没有，可能会返回 Nothing。
    /// </summary>
        private static string DequeueNextMusicAddress()
        {
            string DequeueNextMusicAddressRet = default;
            // 初始化，确保存在音乐
            if (MusicAllList is null || !MusicAllList.Any() || !MusicWaitingList.Any())
                MusicListInit(false);
            // 出列下一个音乐，如果出列结束则生成新列表
            if (MusicWaitingList.Any())
            {
                DequeueNextMusicAddressRet = MusicWaitingList[0];
                MusicWaitingList.RemoveAt(0);
            }
            else
            {
                DequeueNextMusicAddressRet = null;
            }
            if (!MusicWaitingList.Any())
                MusicListInit(false, DequeueNextMusicAddressRet);
            return DequeueNextMusicAddressRet;
        }

        #endregion

        #region UI 控制

        /// <summary>
    /// 刷新背景音乐按钮 UI 与设置页 UI。
    /// </summary>
        private static void MusicRefreshUI()
        {

            // 无背景音乐
            // 有背景音乐

            ModBase.RunInUi(() => { try { if (!MusicAllList.Any()) { ModMain.FrmMain.BtnExtraMusic.Show = false; } else { ModMain.FrmMain.BtnExtraMusic.Show = true; string ToolTipText; if (MusicState == MusicStates.Pause) { ModMain.FrmMain.BtnExtraMusic.Logo = ModBase.Logo.IconPlay; ModMain.FrmMain.BtnExtraMusic.LogoScale = 0.8d; ToolTipText = "已暂停：" + ModBase.GetFileNameWithoutExtentionFromPath(MusicCurrent); if (MusicAllList.Count > 1) { ToolTipText += Constants.vbCrLf + "左键恢复播放，右键播放下一曲。"; } else { ToolTipText += Constants.vbCrLf + "左键恢复播放，右键重新从头播放。"; } } else { ModMain.FrmMain.BtnExtraMusic.Logo = ModBase.Logo.IconMusic; ModMain.FrmMain.BtnExtraMusic.LogoScale = 1d; ToolTipText = "正在播放：" + ModBase.GetFileNameWithoutExtentionFromPath(MusicCurrent); if (MusicAllList.Count > 1) { ToolTipText += Constants.vbCrLf + "左键暂停，右键播放下一曲。"; } else { ToolTipText += Constants.vbCrLf + "左键暂停，右键重新从头播放。"; } } ModMain.FrmMain.BtnExtraMusic.ToolTip = ToolTipText; ToolTipService.SetVerticalOffset(ModMain.FrmMain.BtnExtraMusic, (double)(ToolTipText.Contains(Constants.vbLf) ? 10 : 16)); } if (ModMain.FrmSetupUI is not null) ModMain.FrmSetupUI.MusicRefreshUI(); } catch (Exception ex) { ModBase.Log(ex, "刷新背景音乐 UI 失败", ModBase.LogLevel.Feedback); } });
        }

        /// <summary>
    /// 让音乐在暂停、播放间切换，并显示提示文本。
    /// </summary>
        public static void MusicControlPause()
        {
            if (MusicNAudio is null)
            {
                ModMain.Hint("音乐播放尚未开始！", ModMain.HintType.Critical);
            }
            else
            {
                switch (MusicState)
                {
                    case MusicStates.Pause:
                        {
                            MusicResume();
                            break;
                        }
                    case MusicStates.Play:
                        {
                            MusicPause();
                            break;
                        }

                    default:
                        {
                            ModBase.Log("[Music] 音乐目前为停止状态，已强制尝试开始播放", ModBase.LogLevel.Debug);
                            MusicRefreshPlay(false);
                            break;
                        }
                }
            }
        }

        /// <summary>
    /// 播放下一曲，并显示提示文本。
    /// </summary>
        public static void MusicControlNext()
        {
            if (MusicAllList.Count == 1)
            {
                MusicStartPlay(MusicCurrent);
                ModMain.Hint("重新播放：" + ModBase.GetFileNameFromPath(MusicCurrent), ModMain.HintType.Finish);
            }
            else
            {
                string Address = DequeueNextMusicAddress();
                if (Address is null)
                {
                    ModMain.Hint("没有可以播放的音乐！", ModMain.HintType.Critical);
                }
                else
                {
                    MusicStartPlay(Address);
                    ModMain.Hint("正在播放：" + ModBase.GetFileNameFromPath(Address), ModMain.HintType.Finish);
                }
            }
            MusicRefreshUI();
        }

        #endregion

        #region 主状态控制

        /// <summary>
    /// 获取当前的音乐播放状态。
    /// </summary>
        public static MusicStates MusicState
        {
            get
            {
                if (MusicNAudio is null)
                    return MusicStates.Stop;
                switch (((dynamic)MusicNAudio).PlaybackState)
                {
                    case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // NAudio.Wave.PlaybackState.Stopped
                        {
                            return MusicStates.Stop;
                        }
                    case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false): // NAudio.Wave.PlaybackState.Paused
                        {
                            return MusicStates.Pause;
                        }

                    default:
                        {
                            return MusicStates.Play;
                        }
                }
            }
        }
        public enum MusicStates
        {
            Stop,
            Play,
            Pause
        }

        // 重载与开始

        /// <summary>
    /// 重载播放列表并尝试开始播放背景音乐。
    /// </summary>
    /// <param name="ShowHint">是否显示刷新提示。</param>
        public static void MusicRefreshPlay(bool ShowHint, bool IsFirstLoad = false)
        {
            try
            {

                MusicListInit(true);
                if (!MusicAllList.Any())
                {
                    if (MusicNAudio is null)
                    {
                        if (ShowHint)
                            ModMain.Hint("未检测到可用的背景音乐！", ModMain.HintType.Critical);
                    }
                    else
                    {
                        MusicNAudio = null;
                        if (ShowHint)
                            ModMain.Hint("背景音乐已清除！", ModMain.HintType.Finish);
                    }
                }
                else
                {
                    string Address = DequeueNextMusicAddress();
                    if (Address is null)
                    {
                        if (ShowHint)
                            ModMain.Hint("没有可以播放的音乐！", ModMain.HintType.Critical);
                    }
                    else
                    {
                        try
                        {
                            MusicStartPlay(Address, IsFirstLoad);
                            if (ShowHint)
                                ModMain.Hint("背景音乐已刷新：" + ModBase.GetFileNameFromPath(Address), ModMain.HintType.Finish, false);
                        }
                        catch
                        {
                        }
                    }
                }
                MusicRefreshUI();
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新背景音乐播放失败", ModBase.LogLevel.Feedback);
            }
        }
        /// <summary>
    /// 开始播放音乐。
    /// </summary>
        private static void MusicStartPlay(string Address, bool IsFirstLoad = false)
        {
            if (Address is null)
                return;
            if (_smtc is null || !_smtc.IsEnabled)
                EnableSMTCSupport();
            ModBase.Log("[Music] 播放开始：" + Address);
            MusicCurrent = Address;
            UpdateSMTCInfo();
            ModBase.RunInNewThread(() => MusicLoop(IsFirstLoad), "Music", ThreadPriority.BelowNormal);
        }

        // 播放与暂停

        /// <summary>
    /// 暂停音乐播放，返回是否成功切换了状态。
    /// </summary>
        public static bool MusicPause()
        {
            if (MusicState == MusicStates.Play)
            {
                ModBase.RunInThread(() =>
        {
            ModBase.Log("[Music] 已暂停播放");
            MusicNAudio?.Pause();
            SetSMTCStatus();
            MusicRefreshUI();
        });
                return true;
            }
            else
            {
                ModBase.Log($"[Music] 无需暂停播放，当前状态为 {MusicState}");
                return false;
            }
        }
        /// <summary>
    /// 继续音乐播放，返回是否成功切换了状态。
    /// </summary>
        public static bool MusicResume()
        {
            if (MusicState == MusicStates.Play || !MusicAllList.Any())
            {
                ModBase.Log($"[Music] 无需继续播放，当前状态为 {MusicState}");
                return false;
            }
            else
            {
                ModBase.RunInThread(() =>
        {
            ModBase.Log("[Music] 已恢复播放");
            try
            {
                MusicNAudio?.Play(); // https://github.com/Hex-Dragon/PCL2/pull/5415#issuecomment-2751135223
            }
            catch
            {
                MusicNAudio?.Stop();
                MusicNAudio?.Play();
            }
            SetSMTCStatus();
            MusicRefreshUI();
        });
                return true;
            }
        }

        #endregion

        #region SMTC 控件
        private readonly static object _player = Environment.OSVersion.Version.ToString().Substring(0, 4) == "10.0" ? new Windows.Media.Playback.MediaPlayer() : null;
        private static SystemMediaTransportControls _smtc = null;

        /// <summary>
    /// 启用 SMTC 支持
    /// </summary>
        public static void EnableSMTCSupport()
        {
            if (!(Environment.OSVersion.Version.ToString().Substring(0, 4) == "10.0"))
            {
                ModBase.Log("[SMTC] 当前系统不支持 SMTC 控件，不进行 SMTC 控件初始化");
                return;
            }
            if (Conversions.ToBoolean(!(bool)ModBase.Setup.Get("UiMusicSMTC")))
            {
                ModBase.Log("[SMTC] 用户已关闭 SMTC 支持，不进行初始化");
                return;
            }
            ModBase.Log("[SMTC] 初始化 SMTC 支持");
            // 初始化
            ((dynamic)_player).CommandManager.IsEnabled = false;
            _smtc = (SystemMediaTransportControls)((dynamic)_player).SystemMediaTransportControls;
            _smtc.IsEnabled = true;

            // 设置可交互性
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = false; // 暂时没有上一首

            // 绑定事件处理
            _smtc.ButtonPressed += _smtc_ButtonPressed;
        }

        /// <summary>
    /// 关闭 SMTC 支持
    /// </summary>
        public static void DisableSMTCSupport()
        {
            if (_smtc is not null && _smtc.IsEnabled)
            {
                ModBase.Log("[SMTC] 移除 SMTC 信息源");
                _smtc.IsEnabled = false;
                _smtc = null;
            }
            else
            {
                ModBase.Log("[SMTC] 未添加 SMTC 信息，无需移除");
            }
        }

        /// <summary>
    /// 更新 SMTC 信息
    /// </summary>
        public async static void UpdateSMTCInfo()
        {
            if (_smtc is null)
                return;
            ModBase.Log($"[SMTC] 更新 SMTC 媒体信息，文件路径: {MusicCurrent}");
            var Updater = _smtc.DisplayUpdater;

            try
            {
                var File = TagLib.File.Create(MusicCurrent);

                Updater.AppMediaId = "Plain Craft Launcher 2 CE"; // 媒体来源信息
                Updater.Type = MediaPlaybackType.Music; // 指定媒体类型

                string Artist = File.Tag.FirstPerformer;
                string AlbumArtist = File.Tag.FirstAlbumArtist;
                string AlbumTitle = File.Tag.Album;
                string Title = File.Tag.Title;
                var Thumbnail = File.Tag.Pictures.FirstOrDefault();

                if (string.IsNullOrEmpty(Artist))
                {
                    Artist = "";
                }
                if (string.IsNullOrEmpty(AlbumArtist))
                {
                    AlbumArtist = Artist;
                }
                if (string.IsNullOrEmpty(AlbumTitle))
                {
                    AlbumTitle = "";
                }
                if (string.IsNullOrEmpty(Title))
                {
                    Title = "";
                }

                Updater.MusicProperties.Artist = Artist;
                Updater.MusicProperties.AlbumArtist = AlbumArtist;
                Updater.MusicProperties.AlbumTitle = AlbumTitle;
                Updater.MusicProperties.Title = Title;

                if (Thumbnail is not null)
                {
                    var MemStream = new InMemoryRandomAccessStream();
                    using (var Writer = new DataWriter(MemStream))
                    {
                        Writer.WriteBytes(Thumbnail.Data.Data);
                        await Writer.StoreAsync();
                        Writer.DetachStream();
                    }
                    var ThumbailStream = RandomAccessStreamReference.CreateFromStream(MemStream);

                    Updater.Thumbnail = ThumbailStream;
                }
                else
                {
                    Updater.Thumbnail = null;
                }
            }
            catch
            {
            }

            // 生效设置
            Updater.Update();
        }

        /// <summary>
    /// 设置 SMTC 媒体播放状态
    /// </summary>
        public static void SetSMTCStatus()
        {
            if (_smtc is null)
                return;
            if (MusicState == MusicStates.Play)
            {
                ModBase.Log("[SMTC] 更新 SMTC 播放状态为：Playing");
                _smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            }
            else if (MusicState == MusicStates.Pause)
            {
                ModBase.Log("[SMTC] 更新 SMTC 播放状态为：Paused");
                _smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
            }
            else
            {
                ModBase.Log("[SMTC] 更新 SMTC 播放状态为：Stopped");
                _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
            }
        }

        /// <summary>
    /// 响应 SMTC 交互
    /// </summary>
        public static void _smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            if (_smtc is null)
                return;
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    {
                        ModBase.Log("[SMTC] 收到 SMTC 控件事件，切换播放状态为：Playing");
                        MusicResume();
                        break;
                    }
                case SystemMediaTransportControlsButton.Pause:
                    {
                        ModBase.Log("[SMTC] 收到 SMTC 控件事件，切换播放状态为：Paused");
                        MusicPause();
                        break;
                    }
                case SystemMediaTransportControlsButton.Next:
                    {
                        ModBase.Log("[SMTC] 收到 SMTC 控件事件，切换到下一曲");
                        MusicControlNext();
                        break;
                    }
            }
        }

        /// <summary>
    /// 更新 SMTC 时间线属性
    /// </summary>
    /// <param name="CurrentTime">当前播放进度</param>
    /// <param name="TotalTime">曲目全长</param>
        public static void UpdateSMTCTimeline(TimeSpan CurrentTime, TimeSpan TotalTime)
        {
            var Properties = new SystemMediaTransportControlsTimelineProperties()
            {
                StartTime = TimeSpan.FromSeconds(0d),
                MinSeekTime = TimeSpan.FromSeconds(0d),
                Position = CurrentTime,
                MaxSeekTime = CurrentTime,
                EndTime = TotalTime
            };

            _smtc.UpdateTimelineProperties(Properties);
        }

        /// <summary>
    /// 以 700 ms 为刷新间隔的 SMTC 时间线更新
    /// </summary>
        public static void SMTCTimelineUpdater(NAudio.Wave.WaveOutEvent CurrentWave, NAudio.Wave.WaveStream Reader)
        {
            if (_smtc is null)
                return;
            ModBase.RunInNewThread(() =>
                {
                    while (CurrentWave.Equals(MusicNAudio) && CurrentWave.PlaybackState == NAudio.Wave.PlaybackState.Playing && _smtc is not null)
                    {
                        ModBase.RunInNewThread(() => UpdateSMTCTimeline(Reader.CurrentTime, Reader.TotalTime));
                        Thread.Sleep(700);
                    }
                    while (CurrentWave.Equals(MusicNAudio) && CurrentWave.PlaybackState == NAudio.Wave.PlaybackState.Paused && _smtc is not null)
                        Thread.Sleep(700);
                    if (!CurrentWave.Equals(MusicNAudio))
                        return;
                    SMTCTimelineUpdater(CurrentWave, Reader);
                });
        }
        #endregion

        /// <summary>
    /// 当前正在播放的 NAudio.Wave.WaveOutEvent。
    /// </summary>
        public static object MusicNAudio = null;
        /// <summary>
    /// 当前播放的音乐地址。
    /// </summary>
        private static string MusicCurrent = "";
        /// <summary>
    /// 在 MusicUuid 不变的前提下，持续播放某地址的音乐，且在播放结束后随机播放下一曲。
    /// </summary>
        private static void MusicLoop(bool IsFirstLoad = false)
        {
            NAudio.Wave.WaveOutEvent CurrentWave = null;
            NAudio.Wave.WaveStream Reader = null;
            try
            {
                // 开始播放
                CurrentWave = new NAudio.Wave.WaveOutEvent();
                MusicNAudio = CurrentWave;
                CurrentWave.DeviceNumber = -1;
                Reader = new NAudio.Wave.AudioFileReader(MusicCurrent);
                CurrentWave.Init(Reader);
                CurrentWave.Play();
                // 第一次打开的暂停
                if (Conversions.ToBoolean(IsFirstLoad && !(bool)ModBase.Setup.Get("UiMusicAuto")))
                {
                    CurrentWave.Pause();
                    EnableSMTCSupport(); // 启用 SMTC 支持
                    UpdateSMTCInfo(); // 更新 SMTC 媒体信息
                }
                SetSMTCStatus();
                MusicRefreshUI();
                // 停止条件：播放完毕或变化
                int PreviousVolume = 0;
                SMTCTimelineUpdater(CurrentWave, Reader); // 启动 SMTC 时间轴更新
                while (CurrentWave.Equals(MusicNAudio) && !(CurrentWave.PlaybackState == NAudio.Wave.PlaybackState.Stopped))
                {
                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(ModBase.Setup.Get("UiMusicVolume"), PreviousVolume, false)))
                    {
                        // 更新音量
                        PreviousVolume = Conversions.ToInteger(ModBase.Setup.Get("UiMusicVolume"));
                        CurrentWave.Volume = (float)(PreviousVolume / 1000d);
                    }
                    // 更新进度条
                    double Percent = Reader.CurrentTime.TotalMilliseconds / Reader.TotalTime.TotalMilliseconds;
                    ModBase.RunInUi(() => ModMain.FrmMain.BtnExtraMusic.Progress = Percent);
                    // 检查 SMTC 状态
                    if ((bool)ModBase.Setup.Get("UiMusicSMTC") && _smtc is null)
                    {
                        EnableSMTCSupport();
                        UpdateSMTCInfo();
                        SetSMTCStatus();
                        SMTCTimelineUpdater(CurrentWave, Reader);
                    }
                    if (!(bool)ModBase.Setup.Get("UiMusicSMTC") && _smtc is not null)
                        DisableSMTCSupport();
                    Thread.Sleep(100);
                }
                // 当前音乐已播放结束，继续下一曲
                if (CurrentWave.PlaybackState == NAudio.Wave.PlaybackState.Stopped && MusicAllList.Any())
                    MusicStartPlay(DequeueNextMusicAddress());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "播放音乐出现内部错误（" + MusicCurrent + "）", ModBase.LogLevel.Developer);
                if (ex is NAudio.MmException && ex.Message.Contains("AlreadyAllocated"))
                {
                    ModMain.Hint("你的音频设备正被其他程序占用。请在关闭占用的程序后重启 PCL，才能恢复音乐播放功能！", ModMain.HintType.Critical);
                    Thread.Sleep(1000000000);
                }
                if (ex is NAudio.MmException && (ex.Message.Contains("NoDriver") || ex.Message.Contains("BadDeviceId")))
                {
                    ModMain.Hint("由于音频设备变更，音乐播放功能在重启 PCL 后才能恢复！", ModMain.HintType.Critical);
                    Thread.Sleep(1000000000);
                }
                if (ex.Message.Contains("Got a frame at sample rate") || ex.Message.Contains("does not support changes to"))
                {
                    ModMain.Hint("播放音乐失败（" + ModBase.GetFileNameFromPath(MusicCurrent) + "）：PCL 不支持播放音频属性在中途发生变化的音乐", ModMain.HintType.Critical);
                }
                else if (!(MusicCurrent.EndsWithF(".wav", true) || MusicCurrent.EndsWithF(".mp3", true) || MusicCurrent.EndsWithF(".flac", true)) || ex.Message.Contains("0xC00D36C4")) // #5096：不支持给定的 URL 的字节流类型。 (异常来自 HRESULT:0xC00D36C4)
                {
                    ModMain.Hint("播放音乐失败（" + ModBase.GetFileNameFromPath(MusicCurrent) + "）：PCL 可能不支持此音乐格式，请将格式转换为 .wav、.mp3 或 .flac 后再试", ModMain.HintType.Critical);
                }
                else
                {
                    ModBase.Log(ex, "播放音乐失败（" + ModBase.GetFileNameFromPath(MusicCurrent) + "）", ModBase.LogLevel.Hint);
                }
                // 将播放错误的音乐从列表中移除
                MusicAllList.Remove(MusicCurrent);
                MusicWaitingList.Remove(MusicCurrent);
                MusicRefreshUI();
                // 等待 2 秒后继续播放
                Thread.Sleep(2000);
                if (ex is FileNotFoundException)
                {
                    MusicRefreshPlay(true, IsFirstLoad);
                }
                else
                {
                    MusicStartPlay(DequeueNextMusicAddress(), IsFirstLoad);
                }
            }
            finally
            {
                if (CurrentWave is not null)
                    CurrentWave.Dispose();
                if (Reader is not null)
                    Reader.Dispose();
                MusicRefreshUI();
            }
        }

    }
}