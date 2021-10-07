using com.ricoh.livestreaming.webrtc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UI;

abstract public class DropdownInheritance : Dropdown
{
    internal void Initialize(bool isRefresh = true)
    {
        onValueChanged.AddListener(delegate
        {
            OnValueChangedInternal();
        });
        if (isRefresh)
        {
            Refresh();
        }
    }

    abstract internal void Refresh();
    abstract internal void OnValueChangedInternal();
}

abstract public class DropdownAudioBase : DropdownInheritance
{
    protected List<DeviceInfo> devices = new List<DeviceInfo>();
    protected Action<string> onValueChangedAction;

    abstract internal List<DeviceInfo> GetDevices();

    /// <summary>
    /// Whether audio device exists in dropdown list.
    /// </summary>
    internal bool Exists { get { return devices.Count != 0; } }

    /// <summary>
    /// Selected audio device name in dropdown list.
    /// </summary>
    internal string DeviceName { get; private set; } = "";

    /// <summary>
    /// Initialize dropdown list for audio devices.
    /// </summary>
    /// <param name="onValueChangedAction">Action to be executed if the selected devise is changed in the dropdown list.</param>
    internal void Initialize(Action<string> onValueChangedAction = null)
    {
        Initialize(true);
        this.onValueChangedAction = onValueChangedAction;
    }

    private void SetDevice(int select)
    {
        if (!Exists)
        {
            // No device.
            return;
        }

        if (DeviceName == devices[select].DeviceName)
        {
            // already selected.
            return;
        }

        DeviceName = devices[select].DeviceName;
        onValueChangedAction?.Invoke(DeviceName);
    }

    override internal void OnValueChangedInternal()
    {
        if (devices.Count > value)
        {
            SetDevice(value);
        }
    }

    override internal async void Refresh()
    {
        devices = await Task.Run(() => GetDevices());

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
        int select = 0;
        if (selectedItem != -1)
        {
            select = selectedItem;
        } else
        {
            select = 0;
        }
        SetDevice(select);
        value = select;

        if (options.Count > 0)
        {
            captionText.text = options[select].text;
        }
    }
}
