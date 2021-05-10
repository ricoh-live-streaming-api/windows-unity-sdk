using System;
using System.Collections.Generic;

public class RoomSpec
{
    public enum Type
    {
        Sfu,
        P2p,
        P2pTurn
    }

    private readonly Type type;

    private readonly string[] typeStrings = { "sfu", "p2p", "p2p_turn" };

    public RoomSpec(Type type)
    {
        if (!Enum.IsDefined(typeof(Type), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        this.type = type;
    }

    public Dictionary<string, object> GetSpec()
    {
        var dic = new Dictionary<string, object>
        {
            ["type"] = typeStrings[(int)type],
//            ["media_control"] = new Dictionary<string, object>() { ["bitrate_reservation_mbps"] = 10 }
        };

        return dic;
    }
}
