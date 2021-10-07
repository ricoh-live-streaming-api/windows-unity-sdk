using com.ricoh.livestreaming.webrtc;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UI;

public class DropdownVideoCapturer : DropdownInheritance
{
    protected List<DeviceInfo> devices = new List<DeviceInfo>();
    DropdownCapability dropdownCapability;

    internal void Initialize(DropdownCapability dropdownCapability)
    {
        Initialize(true);
        this.dropdownCapability = dropdownCapability;
    }

    override internal void OnValueChangedInternal()
    {
        if (devices.Count > value)
        {
            dropdownCapability.Refresh();
        }
    }

    override internal async void Refresh()
    {
        devices = await Task.Run(() => DeviceUtil.GetVideoCapturerDeviceList());
        string selectedText = captionText.text;

        ClearOptions();
        int selectedItem = -1;
        for (int i = 0; i < devices.Count; i++)
        {
            DeviceInfo device = devices[i];
            options.Add(new Dropdown.OptionData(device.DeviceName));
            if (device.DeviceName == selectedText)
            {
                selectedItem = i;
            }
        }
        if (selectedItem != -1)
        {
            value = selectedItem;
            captionText.text = options[selectedItem].text;
        }
        else if (devices.Count > 0)
        {
            value = 0;
            captionText.text = options[0].text;
        }
        dropdownCapability.Refresh();
    }
}
