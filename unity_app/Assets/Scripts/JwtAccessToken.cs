using JWT.Builder;
using JWT.Algorithms;
using System;

public static class JwtAccessToken
{
    /*
     * サンプルのためクライアントサイドに生成関数を追加していますが、
     * AccessTokenはアプリバックエンドを用意して生成してください。
     */
    public static string CreateAccessToken(
        string clientSecret,
        string roomId,
        RoomSpec roomSpec)
    {
        byte[] guid = Guid.NewGuid().ToByteArray();
        string connectionId = Convert.ToBase64String(guid, 0, guid.Length)
            .Replace("=", "")
            .Replace("+", "")
            .Replace("/", "");

        var nbf = DateTimeOffset.UtcNow.AddMinutes(-30);
        var exp = nbf.AddHours(1);

        return new JwtBuilder()
            .WithAlgorithm(new HMACSHA256Algorithm())
            .WithSecret(clientSecret)
            .AddClaim("nbf", nbf.ToUnixTimeSeconds())
            .AddClaim("exp", exp.ToUnixTimeSeconds())
            .AddClaim("connection_id", "WIN" + connectionId)
            .AddClaim("room_id", roomId)
            .AddClaim("room_spec", roomSpec.GetSpec())
            .Encode();
    }

}
