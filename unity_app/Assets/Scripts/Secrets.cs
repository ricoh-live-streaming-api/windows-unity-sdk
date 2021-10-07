using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using UnityEngine;

public class Secrets
{
    public string ClientId { get { return properties.client_id; } }
    public string ClientSecret { get { return properties.client_secret; } }
    public string RoomId { get { return properties.room_id; } }
    public int VideoBitrate { get { return properties.video_bitrate; } }
    public int AudioBitrate { get { return properties.audio_bitrate; } }

    public static Secrets GetInstance()
    {
        return Instance;
    }

    private static readonly Secrets Instance = new Secrets();

    private Properties properties;

    [DataContract]
    private class Properties
    {
        [DataMember]
        public string client_id = null;

        [DataMember]
        public string client_secret = null;

        [DataMember]
        public string room_id = null;

        [DataMember]
        public int video_bitrate = 0;

        [DataMember]
        public int audio_bitrate = 0;
    }

    private Secrets()
    {
        using (FileStream fs = new FileStream(Application.streamingAssetsPath + "/Secrets.json", FileMode.Open, FileAccess.Read))
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Properties));
            properties = (Properties)serializer.ReadObject(fs);
            if (string.IsNullOrEmpty(properties.client_id) || string.IsNullOrEmpty(properties.client_secret) || string.IsNullOrEmpty(properties.room_id) || properties.video_bitrate <= 0)
            {
                throw new Exception("Required parameters are not declared in Secrets.json.");
            }
        }
    }
}