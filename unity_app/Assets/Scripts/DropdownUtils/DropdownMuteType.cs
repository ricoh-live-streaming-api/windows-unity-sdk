using com.ricoh.livestreaming;
using System;

public class DropdownMuteType : DropdownInheritance
{
    internal MuteType MuteType { get; private set; } = MuteType.Unmute;
    private Action<MuteType> onValueChangedAction;

    internal void Initialize(Action<MuteType> onValueChangedAction = null)
    {
        Initialize(true);
        this.onValueChangedAction = onValueChangedAction;
    }

    override internal void OnValueChangedInternal()
    {
        MuteType = (MuteType)Enum.ToObject(typeof(MuteType), value);
        onValueChangedAction?.Invoke(MuteType);
    }

    override internal void Refresh()
    {
        ClearOptions();
        foreach (var type in Enum.GetValues(typeof(MuteType)))
        {
            options.Add(new OptionData(type.ToString()));
        }
    }
}
