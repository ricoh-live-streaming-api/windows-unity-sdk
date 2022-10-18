/*
 * Copyright 2022 RICOH Company, Ltd. All rights reserved.
 */
public class DropdownRoomType : DropdownEnumBase<RoomSpec.Type>
{
    override protected string GetItemName(RoomSpec.Type item)
    {
        return item.ToString().ToUpper();
    }
}
