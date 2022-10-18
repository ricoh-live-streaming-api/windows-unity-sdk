/*
 * Copyright 2022 RICOH Company, Ltd. All rights reserved.
 */
using com.ricoh.livestreaming;

public class DropdownIceServersProtocol : DropdownEnumBase<IceServersProtocol>
{
    override protected string GetItemName(IceServersProtocol item)
    {
        return item.ToString().ToUpper();
    }
}
