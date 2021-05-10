using System;

public class DropdownRoomType : DropdownInheritance
{
    internal RoomSpec.Type RoomType { get; private set; }

    internal void Initialize(RoomSpec.Type defaultRoomType = RoomSpec.Type.Sfu)
    {
        Initialize(true);
        value = (int)defaultRoomType;
    }

    override internal void OnValueChangedInternal()
    {
        RoomType = (RoomSpec.Type)Enum.ToObject(typeof(RoomSpec.Type), value);
    }

    override internal void Refresh()
    {
        ClearOptions();
        foreach (var type in Enum.GetValues(typeof(RoomSpec.Type)))
        {
            options.Add(new OptionData(type.ToString().ToUpper()));
        }
    }
}
