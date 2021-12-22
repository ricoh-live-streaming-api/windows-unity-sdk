using com.ricoh.livestreaming;
using com.ricoh.livestreaming.unity;
using com.ricoh.livestreaming.webrtc;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public abstract class BehaviorBase : MonoBehaviour
{
    public SynchronizationContext UnityUIContext { get; protected set; }
    public RTCStatsLogger StatsLogger { get; protected set; }
    public VideoTrack RenderLocalVideoTrack { get; protected set; }
    public bool HasLocalVideoTrack { get; protected set; } = true;

    protected static readonly object frameLockObject = new object();
    protected static readonly object clientLockObject = new object();
    protected static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    protected Client client;
    protected string logFilePath;
    protected virtual VideoCapturer VideoCapturer { get; }
    protected List<LSTrack> localLSTracks = new List<LSTrack>();
    protected UserData userData;
    protected string userDataFilePath;
    protected WindowProcedureHook windowProcedureHook;

    /// <summary>
    /// <see cref="client"/>を新しく生成し、初期化処理を行う。
    /// </summary>
    /// <param name="listener">クライアントに設定するリスナー</param>
    protected void InitializeClient(IClientListener listener)
    {
        client = new Client();
        client.SetEventListener(listener);
        SetDevice(client);
    }

    /// <summary>
    /// webrtcのログの設定を行う
    /// </summary>
    protected void SetConfigWebrtcLog()
    {
        logFilePath = Application.persistentDataPath + "/logs";
        uint logSize = 10 * 1024 * 1024;
        WebrtcLog.Create(logFilePath, "webrtc_", logSize);
    }

    /// <summary>
    /// AudioInputDropdownの変更イベント
    /// </summary>
    /// <param name="deviceName">変更後のデバイス名</param>
    protected virtual void OnAudioInputDropdownValueChanged(string deviceName)
    {
        client.SetAudioInputDevice(deviceName);

        if (client.GetState() != ConnectionState.Open)
        {
            return;
        }

        var track = localLSTracks.Find(item => item.MediaStreamTrack.Type == MediaStreamTrack.TrackType.Audio);
        if (track == null)
        {
            Logger.Info("Not found audio track");
            return;
        }

        var constraints = new MediaStreamConstraints()
            .SetAudio(true);

        try
        {
            var stream = client.GetUserMedia(constraints);
            client.ReplaceMediaStreamTrack(track, stream.GetAudioTracks()[0]);
        }
        catch (Exception e)
        {
            Logger.Error("Failed to replace media stream.", e);
        }
    }

    /// <summary>
    /// AudioOutputDropdownの変更イベント
    /// </summary>
    /// <param name="deviceName">変更後のデバイス名</param>
    protected virtual void OnAudioOutputDropdownValueChanged(string deviceName)
    {
        client.SetAudioOutputDevice(deviceName);
    }

    /// <summary>
    /// Mic mute変更イベント
    /// </summary>
    /// <param name="muteType">変更する<see cref="MuteType"/></param>
    protected void OnMicMuteValueChanged(MuteType muteType)
    {
        if (client.GetState() == ConnectionState.Open)
        {
            ChangeMute(muteType, MediaStreamTrack.TrackType.Audio);
        }
    }

    /// <summary>
    /// Video mute変更イベント
    /// </summary>
    /// <param name="muteType">変更する<see cref="MuteType"/></param>
    protected void OnVideoMuteValueChanged(MuteType muteType)
    {
        if (client.GetState() == ConnectionState.Open)
        {
            ChangeMute(muteType, MediaStreamTrack.TrackType.Video);
        }
    }

    /// <summary>
    /// Clientにmute状態の変化を通知する
    /// </summary>
    /// <param name="muteType">変更する<see cref="MuteType"/></param>
    /// <param name="trackType">Mute対象の<see cref="MediaStreamTrack.TrackType"/></param>
    private void ChangeMute(MuteType muteType, MediaStreamTrack.TrackType trackType)
    {
        var track = localLSTracks.Find(item => item.MediaStreamTrack.Type == trackType);
        if (track != null)
        {
            client?.ChangeMute(track, muteType);
        }
    }

    /// <summary>
    /// VideoRequirement変更イベント
    /// </summary>
    /// <param name="connectionId">Videoを要求するリモートトラックのコネクションID</param>
    /// <param name="isVideoReceive">true : Videoを要求する, false : Videoを要求しない</param>
    public virtual void OnVideoRequirementChanged(string connectionId, bool isVideoReceive)
    {
        try
        {
            client.ChangeMediaRequirements(
                connectionId,
                isVideoReceive ? VideoRequirement.Required : VideoRequirement.Unrequired);
        }
        catch (SDKException e)
        {
            Logger.Error($"Failed to ChangeMediaRequirements. code={e.Detail.Code}", e);
        }
        catch (Exception e)
        {
            Logger.Error("Failed to ChangeMediaRequirements.", e);
        }
    }

    /// <summary>
    /// アプリ終了時に呼び出されるUnityのコールバック
    /// </summary>
    public virtual void OnApplicationQuit()
    {
        Logger.Debug("OnApplicationQuit()");

        UserDataSerializer.Save(userData, userDataFilePath);
        windowProcedureHook.Term();

        client.Disconnect();
        Task.Run(async () =>
        {
            if (client.GetState() != ConnectionState.Idle)
            {
                while (client.GetState() != ConnectionState.Closed)
                {
                    await Task.Delay(15);
                }
            }
        }).Wait(100);
        client.Dispose();
        client = null;
    }

    /// <summary>
    /// 各種デバイスを初期化する処理
    /// アプリ起動時に1回のみ呼び出す
    /// </summary>
    protected abstract void InitializeDevice();

    /// <summary>
    /// 各種デバイスを設定する処理
    /// </summary>
    /// <param name="client">デバイスに紐づけるクライアント</param>
    protected abstract void SetDevice(Client client);

    /// <summary>
    /// Connect ボタンに表示する文字列を設定する
    /// </summary>
    /// <param name="buttonText">表示文字列</param>
    /// <param name="interactable">true : ボタン操作可, false : ボタン操作不可</param>
    protected abstract void SetConnectButtonText(string buttonText, bool interactable);

    protected abstract class ClientListenerBase : IClientListener
    {
        protected readonly BehaviorBase app;

        private System.Threading.Timer statsOutpuTimer;

        public ClientListenerBase(BehaviorBase app)
        {
            this.app = app;
        }

        public virtual void OnAddLocalTrack(MediaStreamTrack mediaStreamTrack, MediaStream stream)
        {
            Logger.Debug($"OnAddLocalTrack() trackID={mediaStreamTrack.Id}");

            app.UnityUIContext.Post(__ =>
            {
                lock (frameLockObject)
                {
                    if (app.HasLocalVideoTrack && mediaStreamTrack is VideoTrack videoTrack)
                    {
                        videoTrack.SetEventListener(CreateVideoTrackListener(app));
                        videoTrack.AddSink();
                        app.RenderLocalVideoTrack = videoTrack;
                    }
                }
            }, null);
        }
        public virtual void OnAddRemoteConnection(string connectionId, Dictionary<string, object> metadata)
        {
            var metadataStr = "";
            foreach (var m in metadata)
            {
                metadataStr += $"({m.Key}, {m.Value})";
            }

            Logger.Debug($"OnAddRemoteConnection() connectionId={connectionId} metadata={metadataStr}");
        }
        public virtual void OnAddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, Dictionary<string, object> metadata, MuteType muteType)
        {
            Logger.Debug($"OnAddRemoteTrack() connectionId={connectionId}, streamID={stream.Id}, trackID={mediaStreamTrack.Id}, muteType={muteType}");
            foreach (KeyValuePair<string, object> pair in metadata)
            {
                Logger.Debug($"key={pair.Key} value={pair.Value}");
            }

            app.UnityUIContext.Post(__ =>
            {
                lock (frameLockObject)
                {
                    AddRemoteTrack(connectionId, stream, mediaStreamTrack, metadata);
                }
            }, null);
        }
        public virtual void OnClosed()
        {
            Logger.Debug("OnClosed()");

            app.UnityUIContext.Post(__ =>
            {
                lock (clientLockObject)
                {
                    if (statsOutpuTimer != null)
                    {
                        statsOutpuTimer.Change(Timeout.Infinite, Timeout.Infinite); // stops timer.
                        statsOutpuTimer.Dispose();
                        statsOutpuTimer = null;
                    }

                    if (app.StatsLogger != null)
                    {
                        app.StatsLogger.Dispose();
                        app.StatsLogger = null;
                    }

                    app.VideoCapturer?.Release();

                    // 切断した場合、Clientを再生成する
                    app.client?.Dispose();
                    app.client = null;
                    app.InitializeClient(this);
                }
            }, null);

            app.SetConnectButtonText("Connect", true);
        }
        public virtual void OnClosing()
        {
            Logger.Debug("OnClosing()");
            app.SetConnectButtonText("Disconnecting...", false);
            app.UnityUIContext.Post(__ =>
            {
                lock (frameLockObject)
                {
                    ClearRemoteTracks();
                }
            }, null);
        }
        public virtual void OnConnecting()
        {
            Logger.Debug("OnConnecting()");
        }
        public virtual void OnError(SDKErrorEvent error)
        {
            Logger.Debug($"OnError() code={error.Detail.Code}, type={error.Detail.Type}, error={error.Detail.Error}, detail={error.ToReportString()}");
        }
        public virtual void OnOpen()
        {
            Logger.Debug("OnOpen()");
            app.UnityUIContext.Post(__ =>
            {
                app.StatsLogger = new RTCStatsLogger(Utils.CreateFilePath(app.logFilePath));
                
                // starts logging stats.
                TimerCallback rtcStatLog = state =>
                {
                    lock (clientLockObject)
                    {
                        if ((app.client != null) && (app.StatsLogger != null))
                        {
                            var statsReports = app.client.GetStats();
                            foreach (var report in statsReports)
                            {
                                app.StatsLogger.Log(report.Key, report.Value);
                            }
                        }
                    }
                };
                statsOutpuTimer = new System.Threading.Timer(rtcStatLog, null, 500, 1000);
            }, null);
            app.SetConnectButtonText("Disconnect", true);
        }

        public virtual void OnRemoveRemoteConnection(string connectionId, Dictionary<string, object> metadata, List<MediaStreamTrack> mediaStreamTracks)
        {
            var metadataStr = "";
            foreach (var m in metadata)
            {
                metadataStr += $"({m.Key}, {m.Value})";
            }

            var mediaStreamTracksStr = "";
            foreach (var track in mediaStreamTracks)
            {
                mediaStreamTracksStr += $"({track.Id}, {track.Type})";
            }

            Logger.Debug($"OnRemoveRemoteConnection() connectionId={connectionId} metadata={metadataStr} mediaStreamTrack={mediaStreamTracksStr}");

            app.UnityUIContext.Post(__ =>
            {
                lock (frameLockObject)
                {
                    RemoveRemoteTrackByConnectionId(connectionId);
                }
            }, null);
        }

        public virtual void OnUpdateRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack track, Dictionary<string, object> metadata)
        {
            var metadataStr = "";
            foreach (var m in metadata)
            {
                metadataStr += $"({m.Key}, {m.Value})";
            }
            Logger.Debug($"OnUpdateRemoteTrack() connectionId={connectionId}, streamID={stream.Id}, trackID={track.Id} metadata={metadataStr}");
        }

        public virtual void OnUpdateMute(string connectionId, MediaStream stream, MediaStreamTrack track, MuteType muteType)
        {
            Logger.Debug($"OnUpdateMute() connectionId={connectionId}, streamID={stream.Id}, trackID={track.Id}, muteType={muteType}");
        }

        public virtual void OnUpdateRemoteConnection(string connectionId, Dictionary<string, object> metadata)
        {
            Logger.Debug($"OnUpdateRemoteConnection() connectionId={connectionId}");
            foreach (KeyValuePair<string, object> pair in metadata)
            {
                Logger.Debug($"key={pair.Key} value={pair.Value}");
            }
        }

        public virtual void OnChangeStability(string connectionId, Stability stability)
        {
            Logger.Debug($"OnChangeStability() connectionId={connectionId} stability={stability}");
        }

        /// <summary>
        /// リモートトラックのクリアを行う。
        /// <see cref="OnClosed"/>で実行される。
        /// </summary>
        protected abstract void ClearRemoteTracks();

        // TODO リモートトラックの処理も共通化したい。WatchのみListではないことを考慮しないといけない。
        /// <summary>
        /// リモートトラックの追加を行う。
        /// <see cref="OnAddRemoteTrack"/>で実行される。
        /// </summary>
        /// <param name="connectionId">追加するリモートトラックのコネクションID</param>
        /// <param name="stream">追加するストリーム</param>
        /// <param name="track">追加するリモートトラック</param>
        /// <param name="metadata">追加するリモートトラックのメタデータ</param>
        protected abstract void AddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack track, Dictionary<string, object> metadata);

        /// <summary>
        /// リモートトラックの削除を行う。
        /// <see cref="OnRemoveRemoteConnection(string, Dictionary{string, object})"/>で実行される。
        /// </summary>
        /// <param name="connectionId"></param>
        protected abstract void RemoveRemoteTrackByConnectionId(string connectionId);

        protected abstract VideoTrack.IListener CreateVideoTrackListener(BehaviorBase app);

    }
}
