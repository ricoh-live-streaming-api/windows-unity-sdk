/*
 * Copyright 2022 RICOH Company, Ltd. All rights reserved.
 */
using System;

/// <summary>
/// Enum のメンバーを Dropdown に表示するための基本クラス
/// </summary>
public abstract class DropdownEnumBase<T> : DropdownInheritance where T : Enum
{
    /// <summary>
    /// 選択した Dropdown の Enum のメンバー
    /// </summary>
    public T Type { get; private set; } = default;

    private Action<T> onSelectAction;

    /// <summary>
    /// Dropdown を初期化する
    /// </summary>
    /// <param name="onSelectAction">Dropdown を選択した際に実行するアクション</param>
    /// <param name="defaultType">初期化と同時に選択状態にする Enum のメンバー</param>
    public void Initialize(Action<T> onSelectAction = null, T defaultType = default)
    {
        Initialize(true);
        value = (int)(object)defaultType;
        this.onSelectAction = onSelectAction;
    }

    internal override void OnValueChangedInternal()
    {
        Type = (T)Enum.ToObject(typeof(T), value);
        onSelectAction?.Invoke(Type);
    }

    /// <summary>
    /// Enum のメンバーがら Dropdown に表示するための文字を取得する
    /// </summary>
    /// <param name="item">Dropdown に表示する Enum</param>
    /// <returns>表示文字列</returns>
    protected virtual string GetItemName(T item)
    {
        return item.ToString();
    }

    internal override void Refresh()
    {
        ClearOptions();

        foreach (T type in Enum.GetValues(typeof(T)))
        {
            options.Add(new OptionData(GetItemName(type)));
        }

        captionText.text = options[value].text;
    }
}