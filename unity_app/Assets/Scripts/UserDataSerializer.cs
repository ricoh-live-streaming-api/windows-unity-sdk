using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// <see cref="UserData"/>のシリアライザ
/// </summary>
public static class UserDataSerializer
{
    /// <summary>
    /// Json形式のファイルを読み込み、<see cref="UserData"/>に変換する
    /// </summary>
    /// <param name="filePath">JSONファイルパス</param>
    /// <returns>変換結果</returns>
    public static UserData Load(string filePath)
    {
        if (File.Exists(filePath))
        {
            using (var streamReader = new StreamReader(filePath))
            {
                string data = streamReader.ReadToEnd();

                return JsonUtility.FromJson<UserData>(data);
            }
        }
        else
        {
            return new UserData();
        }
    }

    /// <summary>
    /// 指定されたファイルに<see cref="UserData"/>をJSON形式で保存する。
    /// </summary>
    /// <param name="userData">保存するデータ</param>
    /// <param name="filePath">保存先</param>
    public static void Save(UserData userData, string filePath)
    {
        string json = JsonUtility.ToJson(userData);
        using (var streamWriter = new StreamWriter(filePath))
        {
            streamWriter.Write(json);
        }
    }
}