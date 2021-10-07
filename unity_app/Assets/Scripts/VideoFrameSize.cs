using System;
using com.ricoh.livestreaming.webrtc;
using System.Text;
using System.IO;

public class VideoFrameSize
{
    public VideoFrameSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
}
