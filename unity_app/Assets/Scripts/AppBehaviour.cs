using com.ricoh.livestreaming;
using com.ricoh.livestreaming.unity;
using com.ricoh.livestreaming.webrtc;
using System;
using System.Collections.Generic;
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

    public DropdownVideoCapturer videoCapturerDropdown;
    public DropdownCapability capabilityDropdown;
    public DropdownAudioInput audioInputDropdown;
    public DropdownAudioOutput audioOutputDropdown;
    public DropdownMuteType micMuteDropdown;
    public DropdownMuteType videoMuteDropdown;
    public DropdownRoomType roomTypeDropdown;
    public Button deviceDropdownRefreshButton;

    private UnityRenderer cappellaRenderer;

    private Dictionary<string, RemoteView> remoteTracks = new Dictionary<string, RemoteView>();
    private Dictionary<string, VideoFrameSize> updateFrameSizeMap = new Dictionary<string, VideoFrameSize>();
    private Dictionary<string, AudioTrack> remoteAudioTracks = new Dictionary<string, AudioTrack>();
    private ConnectionState connectionState;

    private IntPtr hWnd;    // own window handle
    private VideoDeviceCapturer videoDeviceCapturer;
    protected override VideoCapturer VideoCapturer
    {
        get { return videoDeviceCapturer; }
    }

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpszClass, string lpszTitle);

    void Awake()
    {
        userDataFilePath = Application.persistentDataPath + "/UserData.json";
        userData = UserDataSerializer.Load(userDataFilePath);

        // Read configuration.
        try
        {
            Secrets.GetInstance();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to read configuration.", ex);
            UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.Abort);
        }

        // Set RoomType dropdown.
        roomTypeDropdown.Initialize();
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

        HasLocalVideoTrack = true;
        InitializeClient(new ClientListener(this));
        InitializeDevice();
        SetConfigWebrtcLog();

        // Set MuteType dropdown.
        micMuteDropdown.Initialize(OnMicMuteValueChanged);
        videoMuteDropdown.Initialize(OnVideoMuteValueChanged);
    }

    // Update is called once per frame
    void Update()
    {
        lock (lockObject)
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

        ConnectionState state = client.GetState();
        if (state != this.connectionState)
        {
            var button = connectButton.GetComponent<Button>();
            switch (state)
            {
                case ConnectionState.Idle:
                    button.GetComponentInChildren<Text>().text = "Connect";
                    button.interactable = true;
                    break;
                case ConnectionState.Open:
                    button.GetComponentInChildren<Text>().text = "Disconnect";
                    button.interactable = true;
                    break;
            }

            this.connectionState = state;
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

        var button = connectButton.GetComponent<Button>();

        if (client.GetState() == ConnectionState.Idle)
        {
            button.GetComponentInChildren<Text>().text = "Connecting…";
            button.interactable = false;
            Connect();
        }
        else
        {
            button.GetComponentInChildren<Text>().text = "Disconnecting…";
            button.interactable = false;
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

    private void Connect()
    {
        string roomId = roomIDEdit.text;
        var task = Task.Run(() =>
        {
            lock (lockObject)
            {
                try
                {
                    bool isH264Supported = CodecUtil.IsH264Supported();
                    Logger.Debug("IsH264Supported = " + isH264Supported);
                    var videoCodec = isH264Supported ? SendingVideoOption.VideoCodecType.H264 : SendingVideoOption.VideoCodecType.Vp8;

                    var roomSpec = new RoomSpec(roomTypeDropdown.RoomType);

                    var accessToken = JwtAccessToken.CreateAccessToken(
                        Secrets.GetInstance().ClientSecret,
                        roomId,
                        roomSpec);

                    videoDeviceCapturer = new VideoDeviceCapturer(
                        capabilityDropdown.DeviceName,
                        capabilityDropdown.Width,
                        capabilityDropdown.Height,
                        capabilityDropdown.FrameRate);

                    var tags = new Dictionary<string, object>()
                    {
                        { "sample", "sampleValue" }
                    };

                    var audioMeta = new Dictionary<string, object>()
                    {
                        { "metasample", "win_audio" }
                    };

                    var videoMeta = new Dictionary<string, object>()
                    {
                        { "metasample", "win_video" }
                    };

                    var constraints = new MediaStreamConstraints()
                        .SetVideoCapturer(videoDeviceCapturer)
                        .SetAudio(audioInputDropdown.Exists | audioOutputDropdown.Exists);

                    var stream = client.GetUserMedia(constraints);

                    localLSTracks.Clear();
                    foreach (var track in stream.GetAudioTracks())
                    {
                        var trackOption = new LSTrackOption()
                            .SetMeta(audioMeta)
                            .SetMuteType(micMuteDropdown.MuteType);

                        localLSTracks.Add(new LSTrack(track, stream, trackOption));
                    }
                    foreach (var track in stream.GetVideoTracks())
                    {
                        var trackOption = new LSTrackOption()
                            .SetMeta(videoMeta)
                            .SetMuteType(videoMuteDropdown.MuteType);

                        localLSTracks.Add(new LSTrack(track, stream, trackOption));
                    }

                    var sendingVideoOption = new SendingVideoOption()
                        .SetCodec(videoCodec)
                        .SetMaxBitrateKbps(Secrets.GetInstance().VideoBitrate);

                    var option = new Option()
                        .SetLocalLSTracks(localLSTracks)
                        .SetMeta(tags)
                        .SetSendingOption(new SendingOption(sendingVideoOption));

                    client.Connect(Secrets.GetInstance().ClientId, accessToken, option);
                    userData.RoomID = roomId;
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to Connect.", e);
                    UnityUIContext.Post(__ =>
                    {
                        var button = connectButton.GetComponent<UnityEngine.UI.Button>();
                        button.GetComponentInChildren<Text>().text = "Connect";
                        button.interactable = true;
                    }, null);
                }
            }
        });
    }

    private void Disconnect()
    {
        var task = Task.Run(() =>
        {
            lock (lockObject)
            {
                try
                {
                    client.Disconnect();
                    RenderLocalVideoTrack = null;
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to Disconnect.", e);
                    UnityUIContext.Post(__ =>
                    {
                        var button = connectButton.GetComponent<UnityEngine.UI.Button>();
                        button.GetComponentInChildren<Text>().text = "Connect";
                        button.interactable = true;
                    }, null);
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

                var remoteView = new RemoteView(connectionId, videoTrack, content, stream.Id, metadata);
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
            lock (lockObject)
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

        var track = localLSTracks.Find(item => item.MediaStreamTrack.Type == MediaStreamTrack.TrackType.Video);
        if (track == null)
        {
            Logger.Info("Not found video track");
            return;
        }

        videoDeviceCapturer.Release();

        videoDeviceCapturer = new VideoDeviceCapturer(deviceName, width, height, frameRate);

        var constraints = new MediaStreamConstraints()
            .SetVideoCapturer(videoDeviceCapturer);

        try
        {
            var stream = client.GetUserMedia(constraints);
            var videoTrack = stream.GetVideoTracks()[0];
            client.ReplaceMediaStreamTrack(track, videoTrack);
            videoTrack.AddSink();
            RenderLocalVideoTrack = videoTrack;
        }
        catch (Exception e)
        {
            Logger.Error("Failed to replace media stream.", e);
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
