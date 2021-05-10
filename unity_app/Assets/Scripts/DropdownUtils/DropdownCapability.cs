using com.ricoh.livestreaming.webrtc;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UI;

public class DropdownCapability : DropdownInheritance
{
    protected List<VideoCapturerDeviceCapability> capabilities = new List<VideoCapturerDeviceCapability>();
    DropdownVideoCapturer dropdownVideoCapturer;
    private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private Action<string, int, int, int> onValueChangedAction;

    /// <summary>
    /// Selected DeviceName.
    /// </summary>
    public string DeviceName { get; private set; } = "";

    /// <summary>
    /// Selected Width.
    /// </summary>
    public int Width { get; private set; } = 0;

    /// <summary>
    /// Selected Height.
    /// </summary>
    public int Height { get; private set; } = 0;

    /// <summary>
    /// Selected FrameRate.
    /// </summary>
    public int FrameRate { get; private set; } = 0;

    /// <summary>
    /// Initialize dropdown list for video devices.
    /// </summary>
    /// <param name="dropdownVideoCapturer"><see cref="DropdownVideoCapturer"/> used in the dropdown list.</param>
    /// <param name="onValueChangedAction">Action to be executed if the selected devise is changed in the dropdown list.</param>
    internal void Initialize(DropdownVideoCapturer dropdownVideoCapturer, Action<string, int, int, int> onValueChangedAction = null)
    {
        Initialize(false);
        this.dropdownVideoCapturer = dropdownVideoCapturer;
        this.onValueChangedAction = onValueChangedAction;
    }

    override internal void OnValueChangedInternal()
    {
        if (capabilities.Count > value)
        {
            SetDevice(dropdownVideoCapturer.captionText.text, value);
        }
    }

    override internal async void Refresh()
    {
        string selectedVideoCapturer = dropdownVideoCapturer.captionText.text;
        capabilities = await Task.Run(() => DeviceUtil.GetVideoCapturerDeviceCapabilities(selectedVideoCapturer));

        string selectedText = captionText.text;

        ClearOptions();
        int selectedItem = -1;
        for (int i = 0; i < capabilities.Count; i++)
        {
            VideoCapturerDeviceCapability capability = capabilities[i];
            string capabilityText = GetCapabilityText(capability);
            options.Add(new Dropdown.OptionData(capabilityText));
            if (capabilityText == selectedText)
            {
                selectedItem = i;
            }
        }

        int select = 0;
        if (selectedItem != -1)
        {
            select = selectedItem;
        } else
        {
            select = 0;
        }
        SetDevice(selectedVideoCapturer, select);
        captionText.text = options[select].text;
        value = select;
    }

    private string GetCapabilityText(VideoCapturerDeviceCapability capability)
    {
        return capability.Width + " x " + capability.Height + " ( " + capability.FrameRate + "fps )";
    }

    private void SetDevice(string deviceName, int select)
    {
        if (DeviceName == deviceName &&
            Width == capabilities[select].Width &&
            Height == capabilities[select].Height &&
            FrameRate == capabilities[select].FrameRate)
        {
            // already selected;
            return;
        }

        Logger.Info(string.Format("SetDevice(deviceName={0}, width={1}, height={2}, frameRate={3}",
            deviceName, capabilities[select].Width, capabilities[select].Height, capabilities[select].FrameRate));

        DeviceName = deviceName;
        Width = capabilities[select].Width;
        Height = capabilities[select].Height;
        FrameRate = capabilities[select].FrameRate;

        onValueChangedAction?.Invoke(DeviceName, Width, Height, FrameRate);
    }
}
