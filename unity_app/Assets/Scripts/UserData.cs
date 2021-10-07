using UnityEngine;

[System.Serializable]
public class UserData
{
    [SerializeField]
    private string roomID;

    /// <summary>
    /// ルームID
    /// </summary>
    public string RoomID
    {
        get { return roomID; }
        set { roomID = value; }
    }
}