/*
 * Copyright 2022 RICOH Company, Ltd. All rights reserved.
 */
public enum SendReceiveType
{
    SendReceive,
    SendOnly,
    ReceiveOnly
}

public class DropdownSendReceive : DropdownEnumBase<SendReceiveType>
{
    public bool IsSendingEnabled => (Type == SendReceiveType.SendReceive) | (Type == SendReceiveType.SendOnly);
    public bool IsReceivingEnabled => (Type == SendReceiveType.SendReceive) | (Type == SendReceiveType.ReceiveOnly);
}
