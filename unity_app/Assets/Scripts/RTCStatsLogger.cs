using System;
using com.ricoh.livestreaming.webrtc;
using System.Text;
using System.IO;

public class RTCStatsLogger : IDisposable
{
    private StreamWriter streamWriter;

    public RTCStatsLogger(string path)
    {
        streamWriter = new StreamWriter(path);
    }

    public void Log(string connectionID, RTCStatsReport report)
    {
        foreach (var stats in report.Stats.Values)
        {
            if (stats.Type == "candidate-pair" ||
                stats.Type == "stream" ||
                stats.Type == "track" ||
                stats.Type == "media-source" ||
                stats.Type == "inbound-rtp" ||
                stats.Type == "outbound-rtp" ||
                stats.Type == "remote-inbound-rtp")
            {
                streamWriter.Write(ToLTSV(connectionID, stats));
            }
        }
    }

    private string ToLTSV(string connectionID, RTCStats stats)
    {
        var sb = new StringBuilder();
        var originalTime = DateTimeOffset.FromUnixTimeMilliseconds(stats.TimestampUs / 1000);
        var utcDateTime = originalTime.UtcDateTime;
        var dateTime = utcDateTime.ToLocalTime();

        sb.Append(dateTime.ToString("MM-dd HH:mm:ss.fff")).Append("\t");
        sb.Append("connectionId:").Append(connectionID).Append("\t");
        sb.Append("type:");
        sb.Append(stats.Type);
        foreach (var member in stats.Members)
        {
            sb.Append("\t");
            sb.Append(member.Key).Append(":").Append(member.Value);

        }
        sb.Append("\n");
        return sb.ToString();
    }

    public void Dispose()
    {
        streamWriter.Flush();
        streamWriter.Close();
    }
}
