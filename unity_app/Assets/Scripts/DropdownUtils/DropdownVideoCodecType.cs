using com.ricoh.livestreaming;
using com.ricoh.livestreaming.webrtc;
using System;

public class DropdownVideoCodecType : DropdownInheritance
{
    public SendingVideoOption.VideoCodecType Type { get; private set; }
    private bool isH264Supported;

    public void Initialize()
    {
        Initialize(false);

        isH264Supported = CodecUtil.IsH264Supported();

        Refresh();

        Type = isH264Supported 
            ? SendingVideoOption.VideoCodecType.H264 
            : SendingVideoOption.VideoCodecType.Vp8;

        value = (int)(object)Type;
    }

    override internal void OnValueChangedInternal()
    {
        Type = (SendingVideoOption.VideoCodecType)Enum.ToObject(typeof(SendingVideoOption.VideoCodecType), value);
    }

    override internal void Refresh()
    {
        ClearOptions();

        foreach (SendingVideoOption.VideoCodecType type in Enum.GetValues(typeof(SendingVideoOption.VideoCodecType)))
        {
            if (type == SendingVideoOption.VideoCodecType.H264)
            {
                if (isH264Supported)
                {
                    options.Add(new OptionData(type.ToString().ToUpper()));
                }
            }
            else
            {
                options.Add(new OptionData(type.ToString().ToUpper()));
            }
        }
    }
}
