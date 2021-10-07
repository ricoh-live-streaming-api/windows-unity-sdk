using com.ricoh.livestreaming.webrtc;
using System.Collections.Generic;

public class DropdownAudioInput : DropdownAudioBase
{
    override internal List<DeviceInfo> GetDevices()
    {
        return DeviceUtil.GetAudioInputDeviceList();
    }
}
