using com.ricoh.livestreaming.webrtc;
using System.Collections.Generic;

public class DropdownAudioOutput : DropdownAudioBase
{
    override internal List<DeviceInfo> GetDevices()
    {
        return DeviceUtil.GetAudioOutputDeviceList();
    }
}
