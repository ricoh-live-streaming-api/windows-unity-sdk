using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Runtime.Serialization;

public class RoomSpec
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Type
    {
        [EnumMember(Value = "sfu")]
        Sfu,
        [EnumMember(Value = "sfu_large")]
        SfuLarge,
        [EnumMember(Value = "p2p")]
        P2p,
        [EnumMember(Value = "p2p_turn")]
        P2pTurn
    }

    private readonly Type type;

    public RoomSpec(Type type)
    {
        this.type = type;
    }

    public Dictionary<string, object> GetSpec()
    {
        var dic = new Dictionary<string, object>
        {
            ["type"] = type,
            ["media_control"] = new Dictionary<string, object>() { ["bitrate_reservation_mbps"] = 25 }
        };

        return dic;
    }
}
