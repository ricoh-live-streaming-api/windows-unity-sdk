/*
 * Copyright 2022 RICOH Company, Ltd. All rights reserved.
 */
using com.ricoh.livestreaming;
using com.ricoh.livestreaming.unity;
using com.ricoh.livestreaming.webrtc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class AppBehaviour : BehaviorBase
{
    public GameObject scrollViewContent;
    public GameObject baseContent;
    private Vector2 remoteViewOriginalSize;

    public GameObject localRenderTarget;
    private Vector2 localViewOriginalSize;

    public GameObject connectButton;
    public InputField roomIDEdit;
    public InputField bitrateEdit;
    public Button bitrateButton;

    public DropdownVideoCapturer videoCapturerDropdown;
    public DropdownCapability capabilityDropdown;
    public DropdownAudioInput audioInputDropdown;
    public DropdownAudioOutput audioOutputDropdown;
    public DropdownMuteType micMuteDropdown;
    public DropdownMuteType videoMuteDropdown;
    public DropdownVideoCodecType videoCodecDropdown;
    public DropdownSendReceive sendReceiveDropdown;
    public DropdownIceServersProtocol dropdownIceServersProtocol;
    public Button deviceDropdownRefreshButton;

    private UnityRenderer cappellaRenderer;

    private Dictionary<string, RemoteView> remoteTracks = new Dictionary<string, RemoteView>();
    private Dictionary<string, VideoFrameSize> updateFrameSizeMap = new Dictionary<string, VideoFrameSize>();
    private Dictionary<string, AudioTrack> remoteAudioTracks = new Dictionary<string, AudioTrack>();

    private IntPtr hWnd;    // own window handle
    private VideoDeviceCapturer videoDeviceCapturer;
    protected override VideoCapturer VideoCapturer => videoDeviceCapturer;
    protected override RoomSpec.Type RoomType => roomTypeDropdown.Type;
    protected override int MaxBitrateKbps => int.TryParse(bitrateEdit.text, out int bitrate) ? bitrate : 500;
    private SendingVideoOption.VideoCodecType VideoCodecType => videoCodecDropdown.Type;
    private bool IsSendingEnabled => sendReceiveDropdown.IsSendingEnabled;
    private bool IsReceivingEnabled => sendReceiveDropdown.IsReceivingEnabled;
    private IceServersProtocol IceServersProtocol => dropdownIceServersProtocol.Type;

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpszClass, string lpszTitle);

    public override void Awake()
    {
        base.Awake();

        videoCodecDropdown.Initialize();
        sendReceiveDropdown.Initialize();
        dropdownIceServersProtocol.Initialize();
    }

    // Start is called before the first frame update
    void Start()
    {
        Logger.Info("Start Bidir Scene.");

        // Get own window handle.
        hWnd = FindWindow(null, Application.productName);
        windowProcedureHook = new WindowProcedureHook(hWnd);
        windowProcedureHook.AddListener(new WindowProcedureHookListener(this));

        UnityUIContext = SynchronizationContext.Current;

        cappellaRenderer = new UnityRenderer();

        remoteViewOriginalSize = Utils.GetRectSize(baseContent);
        localViewOriginalSize = Utils.GetRectSize(localRenderTarget);

        var image = localRenderTarget.GetComponent<RawImage>();
        image.texture = new Texture2D((int)localViewOriginalSize.x, (int)localViewOriginalSize.y);

        // set previous value of RoomID
        // (get from Secrets when not saved)
        roomIDEdit.text = (string.IsNullOrEmpty(userData.RoomID) ? Secrets.GetInstance().RoomId : userData.RoomID);

        // Set max send bitrate of video. 
        bitrateEdit.text = Secrets.GetInstance().VideoBitrate.ToString();

        HasLocalVideoTrack = true;
        InitializeClient(new ClientListener(this));
        InitializeDevice();

        // Set MuteType dropdown.
        micMuteDropdown.Initialize(OnMicMuteValueChanged);
        videoMuteDropdown.Initialize(OnVideoMuteValueChanged);
    }

    // Update is called once per frame
    void Update()
    {
        lock (frameLockObject)
        {
            if (updateFrameSizeMap.Count != 0)
            {
                // Processed when the frame size is updated.
                // Resize View while keeping aspect ratio.
                foreach (var id in updateFrameSizeMap.Keys)
                {
                    var frameSize = updateFrameSizeMap[id];
                    if (remoteTracks.ContainsKey(id))
                    {
                        var remoteView = remoteTracks[id];
                        remoteView.CreateTexture(frameSize.Width, frameSize.Height);

                        Utils.AdjustAspect(remoteView.GetRawImage(), remoteViewOriginalSize, frameSize.Width, frameSize.Height);
                    }

                    if (RenderLocalVideoTrack != null && RenderLocalVideoTrack.Id == id)
                    {
                        var image = localRenderTarget.GetComponent<UnityEngine.UI.RawImage>();
                        if (image.texture != null)
                        {
                            MonoBehaviour.Destroy(image.texture);
                        }
                        image.texture = Utils.CreateTexture(frameSize.Width, frameSize.Height);

                        Utils.AdjustAspect(image, localViewOriginalSize, frameSize.Width, frameSize.Height);
                    }
                }
                updateFrameSizeMap.Clear();
            }

            // Draw Video frame to Texture.
            // Remote View
            foreach (var view in remoteTracks.Values)
            {
                var texture = view.GetTexture();
                if (texture == null)
                {
                    texture = view.CreateTexture((int)remoteViewOriginalSize.x, (int)remoteViewOriginalSize.y);
                }
                cappellaRenderer.RenderToTexture(texture, view.GetVideoTrack());
            }
            // Local View
            if (RenderLocalVideoTrack != null)
            {
                var image = localRenderTarget.GetComponent<RawImage>();
                cappellaRenderer.RenderToTexture(image.texture, RenderLocalVideoTrack);
            }
        }
    }

    public void OnApplicationFinishButtonClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnConnectButtonClick()
    {
        Logger.Debug("OnConnectButtonClick()");

        if (client.GetState() == ConnectionState.Idle)
        {
            SetConnectButtonText("Connecting...", false);
            Connect();
        }
        else
        {
            SetConnectButtonText("Disconnecting...", false);
            Disconnect();
        }
    }

    protected override void InitializeDevice()
    {
        // Input/Output device selector setting.
        audioInputDropdown.Initialize(OnAudioInputDropdownValueChanged);
        audioOutputDropdown.Initialize(OnAudioOutputDropdownValueChanged);
        videoCapturerDropdown.Initialize(capabilityDropdown);
        capabilityDropdown.Initialize(videoCapturerDropdown, OnVideoDropdownValueChanged);
    }

    protected override void SetDevice(Client client)
    {
        client.SetAudioInputDevice(audioInputDropdown.DeviceName);
        client.SetAudioOutputDevice(audioOutputDropdown.DeviceName);
    }

    protected override void SetConnectButtonText(string buttonText, bool interactable)
    {
        UnityUIContext.Post(__ =>
        {
            var button = connectButton.GetComponent<Button>();
            button.GetComponentInChildren<Text>().text = buttonText;
            button.interactable = interactable;
        }, null);
    }

    private void Connect()
    {
        string roomId = roomIDEdit.text;
        var task = Task.Run(() =>
        {
            lock (clientLockObject)
            {
                try
                {
                    bool isH264Supported = CodecUtil.IsH264Supported();
                    Logger.Debug("IsH264Supported = " + isH264Supported);

                    var roomSpec = new RoomSpec(RoomType);

                    var accessToken = JwtAccessToken.CreateAccessToken(
                        Secrets.GetInstance().ClientSecret,
                        roomId,
                        roomSpec);

                    var sendingVideoOption = new SendingVideoOption()
                        .SetCodec(VideoCodecType)
                        .SetMaxBitrateKbps(MaxBitrateKbps);

                    var option = new Option()
                        .SetMeta(ConnectionMetadata)
                        .SetSendingOption(new SendingOption(sendingVideoOption, IsSendingEnabled))
                        .SetReceivingOption(new ReceivingOption(IsReceivingEnabled))
                        .SetIceServersProtocol(IceServersProtocol);

                    if (IsSendingEnabled)
                    {
                        videoDeviceCapturer = new VideoDeviceCapturer(
                            capabilityDropdown.DeviceName,
                            capabilityDropdown.Width,
                            capabilityDropdown.Height,
                            capabilityDropdown.FrameRate);

                        var constraints = new MediaStreamConstraints()
                            .SetVideoCapturer(videoDeviceCapturer)
                            .SetAudio(audioInputDropdown.Exists | audioOutputDropdown.Exists);

                        var stream = client.GetUserMedia(constraints);

                        localLSTracks.Clear();

                        foreach (var track in stream.GetAudioTracks())
                        {
                            var trackOption = new LSTrackOption()
                                .SetMeta(AudioTrackMetadata)
                                .SetMuteType(micMuteDropdown.Type);

                            localLSTracks.Add(new LSTrack(track, stream, trackOption));
                        }

                        foreach (var track in stream.GetVideoTracks())
                        {
                            var trackOption = new LSTrackOption()
                                .SetMeta(VideoTrackMetadata)
                                .SetMuteType(videoMuteDropdown.Type);

                            localLSTracks.Add(new LSTrack(track, stream, trackOption));
                        }

                        option.SetLocalLSTracks(localLSTracks);
                    }

                    client.Connect(Secrets.GetInstance().ClientId, accessToken, option);
                    userData.RoomID = roomId;
                }
                catch (SDKException e)
                {
                    Logger.Error($"Failed to Connect. code={e.Detail.Code}", e);
                    ErrorHandling();
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to Connect.", e);
                    ErrorHandling();
                }
            }
        });

        void ErrorHandling()
        {
            SetConnectButtonText("Connect", true);
            // VideoDeviceCapturerをReleaseしないと再Connect時にGetUserMediaが失敗する
            videoDeviceCapturer?.Release();
            localLSTracks.Clear();
        }
    }

    private void Disconnect()
    {
        var task = Task.Run(() =>
        {
            lock (clientLockObject)
            {
                try
                {
                    client.Disconnect();
                    RenderLocalVideoTrack = null;
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to Disconnect.", e);
                    SetConnectButtonText("Connect", true);
                }
            }
        });
    }

    private class ClientListener : ClientListenerBase
    {
        public ClientListener(AppBehaviour app) : base(app) { }

        protected override void ClearRemoteTracks()
        {
            AppBehaviour appBehaviour = app as AppBehaviour;
            foreach (var view in appBehaviour.remoteTracks.Values)
            {
                MonoBehaviour.Destroy(view.GetTexture());
                Destroy(view.GetContent());
            }

            appBehaviour.remoteTracks.Clear();
            appBehaviour.remoteAudioTracks.Clear();
            appBehaviour.RenderLocalVideoTrack = null;
        }

        protected override void AddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, Dictionary<string, object> metadata)
        {
            AppBehaviour appBehaviour = app as AppBehaviour;
            if (mediaStreamTrack is VideoTrack videoTrack)
            {
                videoTrack.AddSink();
                videoTrack.SetEventListener(new VideoTrackListener(appBehaviour));

                var content = Instantiate(appBehaviour.baseContent, Vector3.zero, Quaternion.identity);
                content.name = string.Format("track_{0}", videoTrack.Id);
                content.transform.SetParent(appBehaviour.scrollViewContent.transform, false);
                content.SetActive(true);

                // すでにconnectionIdと紐づいたViewがある場合は破棄する
                RemoveRemoteTrackByConnectionId(connectionId);

                var remoteView = new RemoteView(connectionId, videoTrack, content, stream.Id, metadata)
                {
                    OnVideoReceiveToggleChangedAction = appBehaviour.OnVideoRequirementChanged
                };

                // SFU のみ Video 受信選択用 UI を表示
                remoteView.SetVideoReceiveEnabled(appBehaviour.RoomType == RoomSpec.Type.Sfu);

                appBehaviour.remoteTracks.Add(videoTrack.Id, remoteView);

                // 同じStreamIDのAudioTrackを探す。
                // 見つかったらAudioTrackをRemoteViewに設定する
                if (appBehaviour.remoteAudioTracks.ContainsKey(stream.Id))
                {
                    remoteView.SetAudioTrack(appBehaviour.remoteAudioTracks[stream.Id]);
                    appBehaviour.remoteAudioTracks.Remove(stream.Id);
                }
            }
            else if (mediaStreamTrack is AudioTrack audioTrack)
            {
                // 既に作成済みのRemoteViewから同じStreamIDのVideoTrackを探す。
                // 見つかったらAudioTrackをRemoteViewに設定する
                bool foundView = false;
                foreach (var view in appBehaviour.remoteTracks.Values)
                {
                    if (view.GetStreamId() == stream.Id)
                    {
                        view.SetAudioTrack(audioTrack);
                        foundView = true;
                        break;
                    }
                }

                if (!foundView)
                {
                    appBehaviour.remoteAudioTracks.Add(stream.Id, audioTrack);
                }
            }
        }

        protected override void RemoveRemoteTrackByConnectionId(string connectionId)
        {
            AppBehaviour appBehaviour = app as AppBehaviour;
            foreach (var trackId in appBehaviour.remoteTracks.Keys)
            {
                var remoteView = appBehaviour.remoteTracks[trackId];
                if (remoteView.GetConnectionId() == connectionId)
                {
                    MonoBehaviour.Destroy(remoteView.GetTexture());
                    appBehaviour.RemoveRemoteView(trackId);
                    break;
                }
            }
        }

        protected override VideoTrack.IListener CreateVideoTrackListener(BehaviorBase app)
        {
            return new VideoTrackListener(app as AppBehaviour);
        }
    }

    private void RemoveRemoteView(string trackId)
    {
        if (remoteTracks.ContainsKey(trackId))
        {
            RemoteView remoteView = remoteTracks[trackId];
            Destroy(remoteView.GetContent());
            remoteTracks.Remove(trackId);
        }
    }

    private class VideoTrackListener : VideoTrack.IListener
    {
        private readonly AppBehaviour app;

        public VideoTrackListener(AppBehaviour app)
        {
            this.app = app;
        }

        public void OnFrameSizeChanged(string id, int width, int height)
        {
            lock (frameLockObject)
            {
                Logger.Info(String.Format("OnFrameSizeChanged(id={0} width={1} height={2})", id, width, height));
                app.updateFrameSizeMap.Add(id, new VideoFrameSize(width, height));
            }
        }
    }

    private void OnDeviceDropdownRefreshButtonClick()
    {
        videoCapturerDropdown.Refresh();
        audioInputDropdown.Refresh();
        audioOutputDropdown.Refresh();
    }

    private void OnVideoDropdownValueChanged(string deviceName, int width, int height, int frameRate)
    {
        if (client.GetState() != ConnectionState.Open)
        {
            return;
        }

        videoDeviceCapturer?.Release();

        videoDeviceCapturer = new VideoDeviceCapturer(deviceName, width, height, frameRate);

        var constraints = new MediaStreamConstraints()
            .SetVideoCapturer(videoDeviceCapturer);

        try
        {
            var stream = client.GetUserMedia(constraints);
            var videoTrack = stream.GetVideoTracks()[0];
            client.ReplaceMediaStreamTrack(
                GetLSTrack(MediaStreamTrack.TrackType.Video),
                videoTrack);
            videoTrack.AddSink();
            RenderLocalVideoTrack = videoTrack;
        }
        catch (SDKException e)
        {
            Logger.Error($"Failed to ReplaceMediaStreamTrack. code={e.Detail.Code}", e);
            videoDeviceCapturer?.Release();
        }
        catch (Exception e)
        {
            Logger.Error("Failed to ReplaceMediaStreamTrack.", e);
            videoDeviceCapturer?.Release();
        }
    }

    public void OnUpdateConnectMetaButtonClick()
    {
        try
        {
            client.UpdateMeta(new ReadOnlyDictionary<string, object>(ConnectionMetadata));
        }
        catch (SDKException e)
        {
            Logger.Error($"Failed to UpdateMeta. code={e.Detail.Code}", e);
        }
        catch (Exception e)
        {
            Logger.Error("Failed to UpdateMeta.", e);
        }
    }

    public void OnUpdateAudioTrackMetaButtonClick()
    {
        try
        {
            client.UpdateTrackMeta(
                GetLSTrack(MediaStreamTrack.TrackType.Audio),
                new ReadOnlyDictionary<string, object>(AudioTrackMetadata));
        }
        catch (SDKException e)
        {
            Logger.Error($"Failed to UpdateTrackMeta. code={e.Detail.Code}", e);
        }
    }

    public void OnUpdateVideoTrackMetaButtonClick()
    {
        try
        {
            client.UpdateTrackMeta(
                GetLSTrack(MediaStreamTrack.TrackType.Video),
                new ReadOnlyDictionary<string, object>(VideoTrackMetadata));
        }
        catch (SDKException e)
        {
            Logger.Error($"Failed to UpdateTrackMeta. code={e.Detail.Code}", e);
        }
    }

    private class WindowProcedureHookListener : WindowProcedureHook.IListener
    {
        private readonly AppBehaviour app;

        public WindowProcedureHookListener(AppBehaviour app)
        {
            this.app = app;
        }

        public void OnDevicesChanged(WindowProcedureHook.DeviceType type)
        {
            Logger.Debug(string.Format("OnDevicesChanged(type ={0})", type));

            app.UnityUIContext.Post(__ =>
            {

                switch (type)
                {
                    case WindowProcedureHook.DeviceType.Audio:
                        app.audioInputDropdown.Refresh();
                        app.audioOutputDropdown.Refresh();
                        break;
                    case WindowProcedureHook.DeviceType.VideoCapture:
                        app.videoCapturerDropdown.Refresh();
                        break;
                    default:
                        // nothing to do.
                        break;
                }
            }, null);
        }
    }

}
