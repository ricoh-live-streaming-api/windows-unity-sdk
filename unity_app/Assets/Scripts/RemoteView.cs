using com.ricoh.livestreaming.webrtc;
using log4net;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RemoteView
{
    // TODO プロパティ化
    private GameObject _content;
    private readonly VideoTrack _videoTrack;
    private readonly string _connectionId;
    private readonly string _streamId;
    public Dictionary<string, object> Metadata { get; private set; }
    private AudioTrack _audioTrack;
    private readonly Slider slider;
    private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    public RemoteView(string connectionId, VideoTrack videoTrack, GameObject content, string streamId, Dictionary<string, object> metadata)
    {
        _content = content;
        _videoTrack = videoTrack;
        _connectionId = connectionId;
        _streamId = streamId;
        Metadata = metadata;

        var volumeSlider = _content.transform.Find("VolumeSlider");
        if (volumeSlider != null)
        {
            slider = volumeSlider.GetComponent<UnityEngine.UI.Slider>();
            slider.onValueChanged.AddListener(delegate { VolumeSlderValueChanged(); });
        }
        else
        {
            slider = null;
        }
    }

    public GameObject GetContent()
    {
        return _content;
    }

    public VideoTrack GetVideoTrack()
    {
        return _videoTrack;
    }

    public string GetConnectionId()
    {
        return _connectionId;
    }

    public string GetStreamId()
    {
        return _streamId;
    }

    public Texture GetTexture()
    {
        var remoteRawImage = _content.transform.Find("RemoteRawImage");

        var image = remoteRawImage.GetComponent<UnityEngine.UI.RawImage>();
        return image.texture;
    }

    public Texture CreateTexture(int width, int height)
    {
        var remoteRawImage = _content.transform.Find("RemoteRawImage");
        var image = remoteRawImage.GetComponent<UnityEngine.UI.RawImage>();
        if (image.texture != null)
        {
            MonoBehaviour.Destroy(image.texture);
        }

        image.texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        return image.texture;
    }

    public RawImage GetRawImage()
    {
        var remoteRawImage = _content.transform.Find("RemoteRawImage");
        return remoteRawImage.GetComponent<UnityEngine.UI.RawImage>();
    }

    public void SetAudioTrack(AudioTrack track)
    {
        _audioTrack = track;
    }

    /// <summary>
    /// PodUIかどうか
    /// </summary>
    public bool IsPod
    {
        get
        {
            object isPod;
            if (Metadata.TryGetValue(MetadataKeys.IsPod, out isPod))
            {
                return (bool)isPod;
            }
            else
            {
                return false;
            }
        }
    }


    private void VolumeSlderValueChanged()
    {
        if (_audioTrack != null)
        {
            try
            {
                _audioTrack.SetVolume(slider.value);
            }
            catch (ObjectDisposedException e)
            {
                // SDK内部でAudioTrackがDisposeされている
                Logger.Warn("Failed to SetVolume().", e);
            }
        }

    }
}
