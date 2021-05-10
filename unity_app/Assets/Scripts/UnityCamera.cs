using com.ricoh.livestreaming;
using com.ricoh.livestreaming.unity;
using com.ricoh.livestreaming.webrtc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UnityCamera : BehaviorBase
{
    private readonly int VIDEO_WIDTH = 320;
    private readonly int VIDEO_HEIGHT = 180;

    public GameObject scrollViewContent;
    public GameObject baseContent;

    public GameObject localRenderTarget;

    public DropdownAudioInput audioInputDropdown;
    public DropdownAudioOutput audioOutputDropdown;
    public DropdownMuteType micMuteDropdown;
    public DropdownMuteType videoMuteDropdown;
    public DropdownRoomType roomTypeDropdown;
    public Button deviceDropdownRefreshButton;
    public Button connectButton;

    public InputField roomIDEdit;

    public Camera captureCamera;

    private Vector2 remoteViewOriginalSize;
    private Vector2 localViewOriginalSize;

    private ConnectionState connectionState;

    private UnityCameraCapturer unityCameraCapturer;
    protected override VideoCapturer VideoCapturer
    {
        get { return unityCameraCapturer; }
    }

    private UnityRenderer cappellaRenderer;

    private Dictionary<string, RemoteView> remoteTracks = new Dictionary<string, RemoteView>();
    private Dictionary<string, AudioTrack> remoteAudioTracks = new Dictionary<string, AudioTrack>();

    private Dictionary<string, VideoFrameSize> updateFrameSizeMap = new Dictionary<string, VideoFrameSize>();

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpszClass, string lpszTitle);


    private void Awake()
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
        Logger.Info("Start UnityCamera Scene.");

        IntPtr hWnd = FindWindow(null, Application.productName);
        windowProcedureHook = new WindowProcedureHook(hWnd);
        windowProcedureHook.AddListener(new WindowProcedureHookListener(this));

        UnityUIContext = SynchronizationContext.Current;

        cappellaRenderer = new UnityRenderer();

        remoteViewOriginalSize = Utils.GetRectSize(baseContent);
        localViewOriginalSize = Utils.GetRectSize(localRenderTarget);

        var image = localRenderTarget.GetComponent<RawImage>();
        image.texture = new Texture2D((int)localViewOriginalSize.x, (int)localViewOriginalSize.y);

        roomIDEdit.text = (string.IsNullOrEmpty(userData.RoomID) ? Secrets.GetInstance().RoomId : userData.RoomID);

        HasLocalVideoTrack = true;
        InitializeClient(new ClientListener(this));
        InitializeDevice();
        SetConfigWebrtcLog();

        StartCoroutine(Render());

        // create texture for unitycamera
        captureCamera.targetTexture = new RenderTexture(VIDEO_WIDTH, VIDEO_HEIGHT, 0, RenderTextureFormat.BGRA32);
        captureCamera.enabled = true;

        // Set MuteType dropdown.
        micMuteDropdown.Initialize(OnMicMuteValueChanged);
        videoMuteDropdown.Initialize(OnVideoMuteValueChanged);
    }

    IEnumerator Render()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();

            // UnityCamera Caputer Process
            if (ConnectionState.Open == client.GetState())
            {
                unityCameraCapturer?.OnRender();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (updateFrameSizeMap.Count != 0)
        {
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
                    var image = localRenderTarget.GetComponent<RawImage>();
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

        foreach (var view in remoteTracks.Values)
        {
            var texture = view.GetTexture();

            if (texture == null)
            {
                texture = view.CreateTexture((int)remoteViewOriginalSize.x, (int)remoteViewOriginalSize.y);
            }

            cappellaRenderer.RenderToTexture(texture, view.GetVideoTrack());
        }

        if (RenderLocalVideoTrack != null)
        {
            var image = localRenderTarget.GetComponent<RawImage>();
            cappellaRenderer.RenderToTexture(image.texture, RenderLocalVideoTrack);
        }

        ConnectionState state = client.GetState();

        if (state != connectionState)
        {
            switch (state)
            {
                case ConnectionState.Idle:
                    connectButton.GetComponentInChildren<Text>().text = "Connect";
                    connectButton.interactable = true;
                    break;
                case ConnectionState.Open:
                    connectButton.GetComponentInChildren<Text>().text = "Disconnect";
                    connectButton.interactable = true;
                    break;
                case ConnectionState.Closing:
                    break;
            }

            connectionState = state;
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

    public void OnClickConnectButton()
    {
        Logger.Debug("OnConnectButtonClick()");
        UnityEngine.Debug.Log("OnConnectButtonClick()");

        connectButton.interactable = false;

        if (client.GetState() == ConnectionState.Idle)
        {
            connectButton.GetComponentInChildren<Text>().text = "Connecting…";
            Connect();
        }
        else
        {
            connectButton.GetComponentInChildren<Text>().text = "Disconnecting…";
            Disconnect();
        }
    }

    protected override void InitializeDevice()
    {
        // Input/Output device selector setting.
        audioInputDropdown.Initialize(OnAudioInputDropdownValueChanged);
        audioOutputDropdown.Initialize(OnAudioOutputDropdownValueChanged);
    }

    protected override void SetDevice(Client client)
    {
        client.SetAudioInputDevice(audioInputDropdown.DeviceName);
        client.SetAudioOutputDevice(audioOutputDropdown.DeviceName);
    }

    private void Connect()
    {
        string roomId = roomIDEdit.text;

        IntPtr captureCameraTexture = captureCamera.targetTexture.GetNativeTexturePtr();
        unityCameraCapturer = new UnityCameraCapturer(captureCameraTexture, VIDEO_WIDTH, VIDEO_HEIGHT);

        Task.Run(() =>
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

                // mode unitycamera
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
                    { "metasample", "unity_camera" }
                };

                var constraints = new MediaStreamConstraints()
                    .SetVideoCapturer(unityCameraCapturer)
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
                    connectButton.GetComponentInChildren<Text>().text = "Connect";
                    connectButton.interactable = true;
                }
                , null);
            }
        }
        );
    }

    private void Disconnect()
    {
        Task.Run(() =>
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
                    connectButton.GetComponentInChildren<Text>().text = "Connect";
                    connectButton.interactable = true;
                }
                , null);
            }

        }
       );
    }

    public void OnDeviceDropdownRefreshButtonClick()
    {
        audioInputDropdown?.Refresh();
        audioOutputDropdown?.Refresh();
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

    private class ClientListener : ClientListenerBase
    {
        public ClientListener(UnityCamera unityCamera) : base(unityCamera) { }

        protected override void ClearRemoteTracks()
        {
            UnityCamera unityCamera = app as UnityCamera;
            foreach (var view in unityCamera.remoteTracks.Values)
            {
                MonoBehaviour.Destroy(view.GetTexture());
                Destroy(view.GetContent());
            }

            unityCamera.remoteTracks.Clear();
            unityCamera.remoteAudioTracks.Clear();
            unityCamera.RenderLocalVideoTrack = null;
        }

        protected override void AddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, Dictionary<string, object> metadata)
        {
            UnityCamera unityCamera = app as UnityCamera;
            if (mediaStreamTrack is VideoTrack videoTrack)
            {
                videoTrack.AddSink();
                videoTrack.SetEventListener(new VideoTrackListener(unityCamera));

                var content = Instantiate(unityCamera.baseContent, Vector3.zero, Quaternion.identity);
                content.name = string.Format("track_{0}", videoTrack.Id);
                content.transform.SetParent(unityCamera.scrollViewContent.transform, false);
                content.SetActive(true);

                // すでにconnectionIdと紐づいたViewがある場合は破棄する
                RemoveRemoteTrackByConnectionId(connectionId);

                var remoteView = new RemoteView(connectionId, videoTrack, content, stream.Id, metadata);
                unityCamera.remoteTracks.Add(videoTrack.Id, remoteView);
                // 同じStreamIDのAudioTrackを探す。
                // 見つかったらAudioTrackをRemoteViewに設定する
                if (unityCamera.remoteAudioTracks.ContainsKey(stream.Id))
                {
                    remoteView.SetAudioTrack(unityCamera.remoteAudioTracks[stream.Id]);
                    unityCamera.remoteAudioTracks.Remove(stream.Id);
                }
            }
            else if (mediaStreamTrack is AudioTrack audioTrack)
            {
                // 既に作成済みのRemoteViewから同じStreamIDのVideoTrackを探す。
                // 見つかったらAudioTrackをRemoteViewに設定する
                bool foundView = false;
                foreach (var view in unityCamera.remoteTracks.Values)
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
                    unityCamera.remoteAudioTracks.Add(stream.Id, audioTrack);
                }
            }
        }

        protected override void RemoveRemoteTrackByConnectionId(string connectionId)
        {
            UnityCamera unityCamera = app as UnityCamera;
            foreach (var trackId in unityCamera.remoteTracks.Keys)
            {
                var remoteView = unityCamera.remoteTracks[trackId];
                if (remoteView.GetConnectionId() == connectionId)
                {
                    MonoBehaviour.Destroy(remoteView.GetTexture());
                    unityCamera.RemoveRemoteView(trackId);
                    break;
                }
            }
        }

        protected override VideoTrack.IListener CreateVideoTrackListener(BehaviorBase app)
        {
            return new VideoTrackListener(app as UnityCamera);
        }
    }

    private class VideoTrackListener : VideoTrack.IListener
    {
        private readonly UnityCamera app;

        public VideoTrackListener(UnityCamera unityCamera)
        {
            app = unityCamera;
        }

        public void OnFrameSizeChanged(string id, int width, int height)
        {
            Logger.Info(String.Format("OnFrameSizeChanged(id={0} width={1} height={2})", id, width, height));
            app.updateFrameSizeMap.Add(id, new VideoFrameSize(width, height));
        }
    }

    private class WindowProcedureHookListener : WindowProcedureHook.IListener
    {
        private readonly UnityCamera app;

        public WindowProcedureHookListener(UnityCamera unityCamera)
        {
            app = unityCamera;
        }

        public void OnDevicesChanged(WindowProcedureHook.DeviceType type)
        {
            Logger.Debug(string.Format("OnDevicesChanged(type ={0})", type));

            app.UnityUIContext.Post(__ =>
            {

                switch (type)
                {
                    case WindowProcedureHook.DeviceType.Audio:
                        app.audioInputDropdown?.Refresh();
                        app.audioOutputDropdown?.Refresh();
                        break;

                    case WindowProcedureHook.DeviceType.VideoCapture:
                    default:
                        // nothing to do.
                        break;
                }
            }
            , null);
        }
    }

}
