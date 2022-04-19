using System;

public class DropdownSendReceive : DropdownInheritance
{
    public bool IsSendingEnabled => (type == SendReceiveType.SendReceive) | (type == SendReceiveType.SendOnly);
    public bool IsReceivingEnabled => (type == SendReceiveType.SendReceive) | (type == SendReceiveType.ReceiveOnly);

    private enum SendReceiveType
    {
        SendReceive,
        SendOnly,
        ReceiveOnly
    }

    private SendReceiveType type = SendReceiveType.SendReceive;

    internal void Initialize()
    {
        Initialize(true);
        value = (int)(object)type;
    }

    override internal void OnValueChangedInternal()
    {
        type = (SendReceiveType)Enum.ToObject(typeof(SendReceiveType), value);
    }

    override internal void Refresh()
    {
        ClearOptions();

        foreach (SendReceiveType type in Enum.GetValues(typeof(SendReceiveType)))
        {
            options.Add(new OptionData(type.ToString()));
        }
    }
}
