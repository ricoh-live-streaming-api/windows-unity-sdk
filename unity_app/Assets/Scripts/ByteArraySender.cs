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
using static com.ricoh.livestreaming.unity.ByteArrayFunction;

public class ByteArraySender : BehaviorBase
{
    public GameObject scrollViewContent;
    public GameObject baseContent;
    private Vector2 remoteViewOriginalSize;

    public GameObject localRenderTarget;

    public GameObject connectButton;
    public InputField roomIDEdit;

    public DropdownAudioInput audioInputDropdown;
    public DropdownAudioOutput audioOutputDropdown;
    public DropdownMuteType micMuteDropdown;
    public DropdownMuteType videoMuteDropdown;
    public DropdownRoomType roomTypeDropdown;
    public Button deviceDropdownRefreshButton;

    private UnityRenderer cappellaRenderer;

    private Dictionary<string, RemoteView> remoteTracks = new Dictionary<string, RemoteView>();
    private Dictionary<string, AudioTrack> remoteAudioTracks = new Dictionary<string, AudioTrack>();
    private Dictionary<string, VideoFrameSize> updateFrameSizeMap = new Dictionary<string, VideoFrameSize>();
    private ConnectionState connectionState;

    private IntPtr hWnd;    // own window handle

    private ByteArrayFunction byteArrayFunction;
    private ByteArrayCapturer byteArrayCapturer;
    private ByteArrayReceiver byteArrayReceiver;
    protected override VideoCapturer VideoCapturer
    {
        get { return byteArrayCapturer; }
    }

    private const int CAMERA_NUM = 3;
    private const int COLOR_BUFFER_W = 320;
    private const int COLOR_BUFFER_H = 180;
    private const int DEPTH_BUFFER_W = 8;
    private const int DEPTH_BUFFER_H = 8;
    private byte[] color0 = new byte[COLOR_BUFFER_W * COLOR_BUFFER_H * 4];
    private byte[] color1 = new byte[COLOR_BUFFER_W * COLOR_BUFFER_H * 4];
    private byte[] color2 = new byte[COLOR_BUFFER_W * COLOR_BUFFER_H * 4];
    private byte[] depth0 = new byte[DEPTH_BUFFER_W * DEPTH_BUFFER_H * 2];
    private byte[] depth1 = new byte[DEPTH_BUFFER_W * DEPTH_BUFFER_H * 2];
    private byte[] depth2 = new byte[DEPTH_BUFFER_W * DEPTH_BUFFER_H * 2];
    private byte[] receiveColor = new byte[COLOR_BUFFER_W * COLOR_BUFFER_H * 4];
    private byte[] receiveDepth = new byte[DEPTH_BUFFER_W * DEPTH_BUFFER_H * 3];

    private Texture2D localStreamTexture;
    private bool dumpDepthData = false;
    private ImageLayoutSet imageLayoutSet;

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

        int index = 0;
        // Colorデータ
        for (int h = 0; h < COLOR_BUFFER_H; h++)
        {
            for (int w = 0; w < COLOR_BUFFER_W; w++)
            {
                // Set R data
                color0[index + 0] = 0x00;
                color0[index + 1] = 0x00;
                color0[index + 2] = 0xFF;
                color0[index + 3] = 0xFF;

                // Set G data
                color1[index + 0] = 0x00;
                color1[index + 1] = 0xFF;
                color1[index + 2] = 0x00;
                color1[index + 3] = 0xFF;

                // Set B data
                color2[index + 0] = 0xFF;
                color2[index + 1] = 0x00;
                color2[index + 2] = 0x00;
                color2[index + 3] = 0xFF;

                index += 4;
            }
        }

        index = 0;
        int depth0_value = 0x0000;
        int depth1_value = 0x1000;
        int depth2_value = 0xFEF0;

        // Depthデータ
        for (int h = 0; h < DEPTH_BUFFER_H; h++)
        {
            for (int w = 0; w < DEPTH_BUFFER_W; w++)
            {
                depth0[index + 0] = (byte)(depth0_value & 0xff);
                depth0[index + 1] = (byte)((depth0_value >> 8) & 0xff);

                depth1[index + 0] = (byte)(depth1_value & 0xff);
                depth1[index + 1] = (byte)((depth1_value >> 8) & 0xff);

                depth2[index + 0] = (byte)(depth2_value & 0xff);
                depth2[index + 1] = (byte)((depth2_value >> 8) & 0xff);

                depth0_value++;
                depth1_value++;
                depth2_value++;

                index += 2;
            }
        }

        imageLayoutSet = new ImageLayoutSet
        {
            src_num = CAMERA_NUM,                       // カメラ数
            color_area_w = COLOR_BUFFER_W,              // カラー書き込みサイズ
            color_area_h = COLOR_BUFFER_H,
            depth_area_w = DEPTH_BUFFER_W * CAMERA_NUM, // 深度書き込みサイズ
            depth_area_h = DEPTH_BUFFER_H,
            depth_scale = 2,                            // 深度圧縮スケール
            depth_min = 0x0010,
            depth_max = 0xFF00,
            color = new ImageLayout[CAMERA_NUM],        // カメラ毎のカラー書き込み設定
            depth = new ImageLayout[CAMERA_NUM],        // カメラ毎の深度書き込み設定
        };

        for (int i = 0; i < CAMERA_NUM; i++)
        {
            // 書き込み元位置
            imageLayoutSet.color[i].src_x = 0;
            imageLayoutSet.color[i].src_y = 0;
            // 書き込み元のバッファサイズ
            imageLayoutSet.color[i].tex_w = COLOR_BUFFER_W;
            imageLayoutSet.color[i].tex_h = COLOR_BUFFER_H;
            // 書き込み先位置
            imageLayoutSet.color[i].render_x = i * 110;
            imageLayoutSet.color[i].render_y = 0;
            // 書き込みサイズ
            imageLayoutSet.color[i].w = 100;
            imageLayoutSet.color[i].h = COLOR_BUFFER_H;

            // 書き込み元位置
            imageLayoutSet.depth[i].src_x = 0;
            imageLayoutSet.depth[i].src_y = 0;
            // 書き込み元のバッファサイズ
            imageLayoutSet.depth[i].tex_w = 0;
            imageLayoutSet.depth[i].tex_h = 0;
            // 書き込み先位置
            imageLayoutSet.depth[i].render_x = i * DEPTH_BUFFER_W;
            imageLayoutSet.depth[i].render_y = 0;
            // 書き込みサイズ
            imageLayoutSet.depth[i].w = DEPTH_BUFFER_W;
            imageLayoutSet.depth[i].h = DEPTH_BUFFER_H;
        }

        // Set RoomType dropdown.
        roomTypeDropdown.Initialize();
    }

    // Start is called before the first frame update
    void Start()
    {
        Logger.Info("Start ByteArraySender Scene.");

        // Get own window handle.
        hWnd = FindWindow(null, Application.productName);
        windowProcedureHook = new WindowProcedureHook(hWnd);
        windowProcedureHook.AddListener(new WindowProcedureHookListener(this));

        UnityUIContext = SynchronizationContext.Current;

        cappellaRenderer = new UnityRenderer();

        remoteViewOriginalSize = Utils.GetRectSize(baseContent);

        var image = localRenderTarget.GetComponent<UnityEngine.UI.RawImage>();
        localStreamTexture = new Texture2D(
            COLOR_BUFFER_W,
            COLOR_BUFFER_H,
            TextureFormat.RGBA32,
            false);
        image.texture = localStreamTexture;

        // set previous value of RoomID
        // (get from Secrets when not saved)
        roomIDEdit.text = (string.IsNullOrEmpty(userData.RoomID) ? Secrets.GetInstance().RoomId : userData.RoomID);

        HasLocalVideoTrack = true;
        InitializeClient(new ClientListener(this));
        InitializeDevice();
        SetConfigWebrtcLog();

        StartCoroutine(Render());

        // Set MuteType dropdown.
        micMuteDropdown.Initialize(OnMicMuteValueChanged);
        videoMuteDropdown.Initialize(OnVideoMuteValueChanged);
    }

    IEnumerator Render()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();

            ConnectionState state = client.GetState();
            if (state == ConnectionState.Open)
            {
                // byte配列データを合成し送信する
                byteArrayCapturer.RenderByteArray(
                    color0, color1, color2,
                    depth0, depth1, depth2);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        lock (lockObject)
        {
            if (updateFrameSizeMap.Count != 0)
            {
                // ビデオフレームのサイズが更新された
                foreach (var id in updateFrameSizeMap.Keys)
                {
                    var frameSize = updateFrameSizeMap[id];
                    if (remoteTracks.ContainsKey(id))
                    {
                        var remoteView = remoteTracks[id];
                        if (!remoteView.IsPod)
                        {
                            // Textureのサイズを更新する
                            remoteView.CreateTexture(frameSize.Width, frameSize.Height);
                            // 表示領域をアスペクト維持して変更する
                            Utils.AdjustAspect(remoteView.GetRawImage(), remoteViewOriginalSize, frameSize.Width, frameSize.Height);
                        }

                    }
                }
                updateFrameSizeMap.Clear();
            }


            // Remote View
            foreach (var view in remoteTracks.Values)
            {
                int width = 0;
                int height = 0;

                if (view.GetVideoTrack().GetFrameResolution(ref width, ref height))
                {
                    var texture = view.GetTexture();
                    if (texture == null)
                    {
                        if (view.IsPod)
                        {
                            texture = view.CreateTexture(COLOR_BUFFER_W, COLOR_BUFFER_H);
                        }
                        else
                        {
                            texture = view.CreateTexture((int)remoteViewOriginalSize.x, (int)remoteViewOriginalSize.y);
                        }
                    }

                    if (view.IsPod)
                    {
                        // 合成映像フレームの場合は、Colorデータ、DepthデータをSDKから取得する
                        ReceiveByteArrayData((Texture2D)texture, view.GetVideoTrack());
                    }
                    else
                    {
                        // それ以外のフレームの場合は、SDKでTextureにフレームデータをレンダリングする
                        cappellaRenderer.RenderToTexture(texture, view.GetVideoTrack());
                    }
                }
            }

            // Local View
            if (RenderLocalVideoTrack != null)
            {
                // 合成データの受信を行う
                ReceiveByteArrayData(localStreamTexture, RenderLocalVideoTrack);
            }
        }

        ConnectionState state = client.GetState();
        if (state != connectionState)
        {
            var button = connectButton.GetComponent<UnityEngine.UI.Button>();
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

            connectionState = state;
        }
    }

    /// <summary>
    /// Colorデータ、Depthデータの受信処理
    /// </summary>
    unsafe
    public void ReceiveByteArrayData(Texture2D texture, VideoTrack videoTrack)
    {
        fixed (byte* pColor = receiveColor)
        fixed (byte* pDepth = receiveDepth)
        {
            if (byteArrayReceiver.Receive((IntPtr)pColor, (IntPtr)pDepth, videoTrack) == false)
            {
                return;
            }
        }

        texture.LoadRawTextureData(receiveColor);
        texture.Apply();

        if (dumpDepthData)
        {
            DumpDepthData();
        }
    }

    private void DumpDepthData()
    {
        // Camera0 Depth
        Logger.Debug("### Receive DepthBuffer Camera0 ###");
        for (int h = 0; h < DEPTH_BUFFER_H; h++)
        {
            for (int w = 0; w < DEPTH_BUFFER_W; w++)
            {
                Logger.Debug(string.Format("  [{0}]", receiveDepth[w + h * DEPTH_BUFFER_H * 3]));
            }
        }

        // Camera1 Depth
        Logger.Debug("### Receive DepthBuffer Camera1 ###");
        for (int h = 0; h < DEPTH_BUFFER_H; h++)
        {
            for (int w = DEPTH_BUFFER_W; w < DEPTH_BUFFER_W * 2; w++)
            {
                Logger.Debug(string.Format("  [{0}]", receiveDepth[w + h * DEPTH_BUFFER_H * 3]));
            }
        }

        // Camera2 Depth
        Logger.Debug("### Receive DepthBuffer Camera2 ###");
        for (int h = 0; h < DEPTH_BUFFER_H; h++)
        {
            for (int w = DEPTH_BUFFER_W * 2; w < DEPTH_BUFFER_W * 3; w++)
            {
                Logger.Debug(string.Format("  [{0}]", receiveDepth[w + h * DEPTH_BUFFER_H * 3]));
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

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        byteArrayFunction?.Dispose();
    }

    public void OnConnectButtonClick()
    {
        Logger.Debug("OnConnectButtonClick()");

        var button = connectButton.GetComponent<UnityEngine.UI.Button>();

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
    }

    protected override void SetDevice(Client client)
    {
        client.SetAudioInputDevice(audioInputDropdown.DeviceName);
        client.SetAudioOutputDevice(audioOutputDropdown.DeviceName);
    }

    private void Connect()
    {
        string roomId = roomIDEdit.text;

        byteArrayFunction = new ByteArrayFunction(imageLayoutSet);

        // Capturerの生成
        byteArrayCapturer = byteArrayFunction.CreateCapturer(
            imageLayoutSet.TotalWidth(), COLOR_BUFFER_H + DEPTH_BUFFER_H);

        // Receiverの生成
        byteArrayReceiver = byteArrayFunction.CreateReceiver();

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
                        { "metasample", "byte_array_sender" },
                        { MetadataKeys.IsPod, true }    // isPodをtrueにすることで他クライアントがこちらの映像をPodと判断し表示する
                    };

                    var constraints = new MediaStreamConstraints()
                        .SetVideoCapturer(byteArrayCapturer)
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
                    byteArrayFunction?.Dispose();
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
        public ClientListener(ByteArraySender app) : base(app) { }

        protected override void ClearRemoteTracks()
        {
            ByteArraySender byteArraySender = app as ByteArraySender;
            foreach (var view in byteArraySender.remoteTracks.Values)
            {
                MonoBehaviour.Destroy(view.GetTexture());
                Destroy(view.GetContent());
            }

            byteArraySender.remoteTracks.Clear();
            byteArraySender.remoteAudioTracks.Clear();
            byteArraySender.RenderLocalVideoTrack = null;

        }

        protected override void AddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, Dictionary<string, object> metadata)
        {
            ByteArraySender byteArraySender = app as ByteArraySender;
            if (mediaStreamTrack is VideoTrack videoTrack)
            {
                videoTrack.AddSink();
                videoTrack.SetEventListener(new VideoTrackListener(byteArraySender));

                var content = GameObject.Instantiate(byteArraySender.baseContent, Vector3.zero, Quaternion.identity);
                content.name = string.Format("track_{0}", videoTrack.Id);
                content.transform.SetParent(byteArraySender.scrollViewContent.transform, false);
                content.SetActive(true);

                // すでにconnectionIdと紐づいたViewがある場合は破棄する
                RemoveRemoteTrackByConnectionId(connectionId);

                var remoteView = new RemoteView(connectionId, videoTrack, content, stream.Id, metadata);
                byteArraySender.remoteTracks.Add(videoTrack.Id, remoteView);
                // 同じStreamIDのAudioTrackを探す。
                // 見つかったらAudioTrackをRemoteViewに設定する
                if (byteArraySender.remoteAudioTracks.ContainsKey(stream.Id))
                {
                    remoteView.SetAudioTrack(byteArraySender.remoteAudioTracks[stream.Id]);
                    byteArraySender.remoteAudioTracks.Remove(stream.Id);
                }
            }
            else if (mediaStreamTrack is AudioTrack audioTrack)
            {
                // 既に作成済みのRemoteViewから同じStreamIDのVideoTrackを探す。
                // 見つかったらAudioTrackをRemoteViewに設定する
                bool foundView = false;
                foreach (var view in byteArraySender.remoteTracks.Values)
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
                    byteArraySender.remoteAudioTracks.Add(stream.Id, audioTrack);
                }
            }
        }

        protected override void RemoveRemoteTrackByConnectionId(string connectionId)
        {
            ByteArraySender byteArraySender = app as ByteArraySender;
            foreach (var trackId in byteArraySender.remoteTracks.Keys)
            {
                var remoteView = byteArraySender.remoteTracks[trackId];
                if (remoteView.GetConnectionId() == connectionId)
                {
                    MonoBehaviour.Destroy(remoteView.GetTexture());
                    byteArraySender.RemoveRemoteView(trackId);
                    break;
                }
            }
        }

        protected override VideoTrack.IListener CreateVideoTrackListener(BehaviorBase app)
        {
            return new VideoTrackListener(app as ByteArraySender);
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
        private readonly ByteArraySender app;

        public VideoTrackListener(ByteArraySender app)
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

    public void OnDeviceDropdownRefreshButtonClick()
    {
        audioInputDropdown.Refresh();
        audioOutputDropdown.Refresh();
    }

    private class WindowProcedureHookListener : WindowProcedureHook.IListener
    {
        private readonly ByteArraySender app;

        public WindowProcedureHookListener(ByteArraySender app)
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
                        // Audioデバイスが接続、切断された
                        app.audioInputDropdown.Refresh();
                        app.audioOutputDropdown.Refresh();
                        break;
                    case WindowProcedureHook.DeviceType.VideoCapture:
                    default:
                        // nothing to do.
                        break;
                }
            }, null);
        }
    }

}
