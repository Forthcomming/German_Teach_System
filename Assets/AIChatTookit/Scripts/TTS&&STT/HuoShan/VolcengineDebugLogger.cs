using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class VolcengineDebugLogger
{
    private const string SessionId = "9c4e6f";
    private const string LogFileName = "debug-9c4e6f.log";
    private static readonly object WriteLock = new object();

    public static void Log(string runId, string hypothesisId, string location, string message, object data = null)
    {
        try
        {
            JObject payload = new JObject
            {
                ["sessionId"] = SessionId,
                ["runId"] = string.IsNullOrEmpty(runId) ? "unknown_run" : runId,
                ["hypothesisId"] = hypothesisId ?? string.Empty,
                ["location"] = location ?? string.Empty,
                ["message"] = message ?? string.Empty,
                ["data"] = data == null ? new JObject() : JToken.FromObject(data),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            string logPath = Path.Combine(Directory.GetCurrentDirectory(), LogFileName);
            lock (WriteLock)
            {
                File.AppendAllText(logPath, payload.ToString(Formatting.None) + Environment.NewLine);
            }
        }
        catch
        {
            // 调试日志不影响主流程
        }
    }
}
