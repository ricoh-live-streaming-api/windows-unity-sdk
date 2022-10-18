/*
 * Copyright 2022 RICOH Company, Ltd. All rights reserved.
 */
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

public class Watch : BehaviorBase
{
    public GameObject remoteRenderTarget;
    private Vector2 remoteViewOriginalSize;

    public GameObject connectButton;
    public InputField roomIDEdit;

    public DropdownAudioOutput audioOutputDropdown;
    public DropdownRoomType roomTypeDropdown;
    public Button deviceDropdownRefreshButton;

    private UnityRenderer cappellaRenderer;
    private VideoTrack renderRemoteVideoTrack = null;
    private Dictionary<string, object> remoteVideoTrackMetadata;
    private VideoFrameSize updateFrameSize = null;

    private IntPtr hWnd;    // own window handle

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
        // Get own window handle.
        hWnd = FindWindow(null, Application.productName);
        windowProcedureHook = new WindowProcedureHook(hWnd);
        windowProcedureHook.AddListener(new WindowProcedureHookListener(this));

        UnityUIContext = SynchronizationContext.Current;

        cappellaRenderer = new UnityRenderer();

        remoteViewOriginalSize = Utils.GetRectSize(remoteRenderTarget);

        var image = remoteRenderTarget.GetComponent<UnityEngine.UI.RawImage>();
        image.texture = new Texture2D((int)remoteViewOriginalSize.x, (int)remoteViewOriginalSize.y);

        // set previous value of RoomID
        // (get from Secrets when not saved)
        roomIDEdit.text = (string.IsNullOrEmpty(userData.RoomID) ? Secrets.GetInstance().RoomId : userData.RoomID);

        HasLocalVideoTrack = false;
        InitializeClient(new ClientListener(this));
        InitializeDevice();
        SetConfigWebrtcLog();
    }

    // Update is called once per frame
    void Update()
    {
        lock (frameLockObject)
        {
            if (updateFrameSize != null)
            {
                // Processed when the frame size is updated.
                // Resize View while keeping aspect ratio.
                var image = remoteRenderTarget.GetComponent<UnityEngine.UI.RawImage>();
                image.texture = Utils.CreateTexture(updateFrameSize.Width, updateFrameSize.Height);

                Utils.AdjustAspect(image, remoteViewOriginalSize, updateFrameSize.Width, updateFrameSize.Height);
                updateFrameSize = null;
            }

            // Draw Video frame to Texture.
            if (renderRemoteVideoTrack != null)
            {
                var image = remoteRenderTarget.GetComponent<UnityEngine.UI.RawImage>();
                cappellaRenderer.RenderToTexture(image.texture, renderRemoteVideoTrack);
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
        audioOutputDropdown.Initialize(OnAudioOutputDropdownValueChanged);
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

    protected override void SetDevice(Client client)
    {
        client.SetAudioOutputDevice(audioOutputDropdown.DeviceName);
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
                    var videoCodec = isH264Supported ? SendingVideoOption.VideoCodecType.H264 : SendingVideoOption.VideoCodecType.Vp8;

                    var roomSpec = new RoomSpec(roomTypeDropdown.Type);

                    var accessToken = JwtAccessToken.CreateAccessToken(
                        Secrets.GetInstance().ClientSecret,
                        roomId,
                        roomSpec);

                    var tags = new Dictionary<string, object>()
                    {
                        { "sample", "sampleValue" }
                    };

                    var sendingVideoOption = new SendingVideoOption()
                        .SetCodec(videoCodec)
                        .SetMaxBitrateKbps(Secrets.GetInstance().VideoBitrate);

                    var option = new Option()
                        .SetMeta(tags)
                        .SetSendingOption(new SendingOption(sendingVideoOption, false));

                    client.Connect(Secrets.GetInstance().ClientId, accessToken, option);
                    userData.RoomID = roomId;
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to Connect.", e);
                    SetConnectButtonText("Connect", true);
                }
            }
        });
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
                    renderRemoteVideoTrack = null;
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
        public ClientListener(Watch unityCamera) : base(unityCamera) { }

        protected override void ClearRemoteTracks()
        {
            (app as Watch).renderRemoteVideoTrack = null;
        }

        protected override void AddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, Dictionary<string, object> metadata)
        {
            if (mediaStreamTrack is VideoTrack videoTrack)
            {
                videoTrack.AddSink();
                videoTrack.SetEventListener(new VideoTrackListener(app));

                (app as Watch).renderRemoteVideoTrack = videoTrack;
                (app as Watch).remoteVideoTrackMetadata = metadata;
            }
        }

        protected override void RemoveRemoteTrackByConnectionId(string connectionId)
        {
            (app as Watch).renderRemoteVideoTrack.RemoveSink(); ;
            ClearRemoteTracks();
        }

        protected override VideoTrack.IListener CreateVideoTrackListener(BehaviorBase app)
        {
            return new VideoTrackListener(app);
        }
    }

    private class VideoTrackListener : VideoTrack.IListener
    {
        private readonly BehaviorBase app;

        public VideoTrackListener(BehaviorBase app)
        {
            this.app = app;
        }

        public void OnFrameSizeChanged(string id, int width, int height)
        {
            lock (frameLockObject)
            {
                Logger.Info(String.Format("OnFrameSizeChanged(id={0} width={1} height={2})", id, width, height));
                (app as Watch).updateFrameSize = new VideoFrameSize(width, height);
            }
        }
    }

    public void OnDeviceDropdownRefreshButtonClick()
    {
        audioOutputDropdown.Refresh();
    }

    private class WindowProcedureHookListener : WindowProcedureHook.IListener
    {
        private readonly BehaviorBase app;

        public WindowProcedureHookListener(BehaviorBase app)
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
                        (app as Watch).audioOutputDropdown.Refresh();
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
