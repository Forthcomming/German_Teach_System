using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class VolcengineAsrWsClient : IDisposable
{
    private sealed class HandshakeProbeResult
    {
        public bool Success;
        public string StatusLine;
        public string BodySnippet;
    }

    private readonly VolcengineSettings settings;
    private readonly SynchronizationContext unityContext;
    private ClientWebSocket socket;
    private CancellationTokenSource internalCts;
    private int seq = 1;
    private bool disposed;
    public string DebugRunId { get; set; } = "unknown_run";

    public event Action<VolcengineAsrProtocol.AsrResponse> OnResponse;
    public event Action<string> OnPartialText;
    public event Action<string> OnFinalText;
    public event Action<string> OnError;

    public VolcengineAsrWsClient(VolcengineSettings settings)
    {
        this.settings = settings;
        unityContext = SynchronizationContext.Current;
    }

    public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

    public async Task ConnectAsync(CancellationToken externalToken)
    {
        ValidateSettings();
        EnsureTlsConfiguration();

        internalCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        socket = new ClientWebSocket();
        // Unity/Windows 下系统代理可能导致 wss 握手失败，这里强制直连。
        socket.Options.Proxy = null;
        Dictionary<string, string> headers = VolcengineAsrProtocol.BuildAuthHeaders(settings);

        Uri wsUri = new Uri(settings.asrWsUrl);
        bool hasAccessKeyHeader = headers.ContainsKey("X-Api-Access-Key");
        // #region agent log
        VolcengineDebugLogger.Log(
            DebugRunId,
            "H1_H2_H5",
            "VolcengineAsrWsClient.ConnectAsync",
            "connect_start",
            new
            {
                wsScheme = wsUri.Scheme,
                wsHost = wsUri.Host,
                wsPort = wsUri.Port,
                wsPath = wsUri.AbsolutePath,
                appKeyConfigured = !string.IsNullOrEmpty(settings.appKey),
                accessKeyHeaderPresent = hasAccessKeyHeader,
                timeoutCanceled = externalToken.IsCancellationRequested,
                linkedCanceled = internalCts.Token.IsCancellationRequested
            });
        // #endregion
        // #region agent log
        VolcengineDebugLogger.Log(
            DebugRunId,
            "H4_H6",
            "VolcengineAsrWsClient.ConnectAsync",
            "tls_proxy_state",
            new
            {
                securityProtocol = ServicePointManager.SecurityProtocol.ToString(),
                proxyDisabled = socket.Options.Proxy == null
            });
        // #endregion

        // #region agent log
        try
        {
            IPAddress[] addrs = await Dns.GetHostAddressesAsync(wsUri.Host);
            string[] ipPreview = new string[Math.Min(3, addrs.Length)];
            for (int i = 0; i < ipPreview.Length; i++)
            {
                ipPreview[i] = addrs[i].ToString();
            }

            VolcengineDebugLogger.Log(
                DebugRunId,
                "H3_H1",
                "VolcengineAsrWsClient.ConnectAsync",
                "dns_resolve_result",
                new
                {
                    host = wsUri.Host,
                    addressCount = addrs.Length,
                    addressPreview = ipPreview
                });
        }
        catch (Exception ex)
        {
            VolcengineDebugLogger.Log(
                DebugRunId,
                "H3_H1",
                "VolcengineAsrWsClient.ConnectAsync",
                "dns_resolve_failed",
                new
                {
                    host = wsUri.Host,
                    exceptionType = ex.GetType().FullName,
                    message = ex.Message
                });
        }
        // #endregion

        // #region agent log
        using (TcpClient tcpProbe = new TcpClient())
        {
            try
            {
                Task connectTask = tcpProbe.ConnectAsync(wsUri.Host, wsUri.Port);
                Task timeoutTask = Task.Delay(3000, internalCts.Token);
                Task completed = await Task.WhenAny(connectTask, timeoutTask);
                if (completed == connectTask && tcpProbe.Connected)
                {
                    VolcengineDebugLogger.Log(
                        DebugRunId,
                        "H3_H1",
                        "VolcengineAsrWsClient.ConnectAsync",
                        "tcp_probe_success",
                        new
                        {
                            host = wsUri.Host,
                            port = wsUri.Port
                        });
                }
                else
                {
                    VolcengineDebugLogger.Log(
                        DebugRunId,
                        "H3_H1",
                        "VolcengineAsrWsClient.ConnectAsync",
                        "tcp_probe_timeout_or_failed",
                        new
                        {
                            host = wsUri.Host,
                            port = wsUri.Port,
                            completedTask = completed == connectTask ? "connectTask" : "timeoutTask",
                            connected = tcpProbe.Connected
                        });
                }
            }
            catch (Exception ex)
            {
                VolcengineDebugLogger.Log(
                    DebugRunId,
                    "H3_H1",
                    "VolcengineAsrWsClient.ConnectAsync",
                    "tcp_probe_exception",
                    new
                    {
                        host = wsUri.Host,
                        port = wsUri.Port,
                        exceptionType = ex.GetType().FullName,
                        message = ex.Message
                    });
            }
        }
        // #endregion

        // #region agent log
        HandshakeProbeResult probeResult = await ProbeHandshakeAsync(wsUri, headers);
        if (probeResult != null && !probeResult.Success)
        {
            string statusLine = string.IsNullOrEmpty(probeResult.StatusLine) ? "unknown_status" : probeResult.StatusLine;
            string bodySnippet = string.IsNullOrEmpty(probeResult.BodySnippet) ? string.Empty : $" body={probeResult.BodySnippet}";
            throw new InvalidOperationException($"WebSocket 握手失败：{statusLine}.{bodySnippet} 请检查 appKey/accessKey/resourceId 是否正确。");
        }
        // #endregion

        foreach (KeyValuePair<string, string> pair in headers)
        {
            socket.Options.SetRequestHeader(pair.Key, pair.Value);
        }
        socket.Options.SetRequestHeader("User-Agent", "Unity-Volcengine-ASR/1.0");

        try
        {
            await socket.ConnectAsync(wsUri, internalCts.Token);
            // #region agent log
            VolcengineDebugLogger.Log(
                DebugRunId,
                "H1_H3",
                "VolcengineAsrWsClient.ConnectAsync",
                "connect_success",
                new
                {
                    state = socket.State.ToString()
                });
            // #endregion
        }
        catch (Exception ex)
        {
            // #region agent log
            VolcengineDebugLogger.Log(
                DebugRunId,
                "H1_H3_H4_H5",
                "VolcengineAsrWsClient.ConnectAsync",
                "connect_failed",
                new
                {
                    exceptionType = ex.GetType().FullName,
                    message = ex.Message,
                    innerType = ex.InnerException?.GetType().FullName,
                    innerMessage = ex.InnerException?.Message,
                    hResult = ex.HResult,
                    socketState = socket?.State.ToString()
                });
            // #endregion
            throw;
        }
    }

    private static void EnsureTlsConfiguration()
    {
        // 确保至少启用 TLS1.2，避免在部分 Unity/Mono 环境下 wss 握手失败。
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
    }

    private async Task<HandshakeProbeResult> ProbeHandshakeAsync(Uri wsUri, Dictionary<string, string> headers)
    {
        HandshakeProbeResult result = new HandshakeProbeResult();
        try
        {
            using (TcpClient tcp = new TcpClient())
            {
                Task connectTask = tcp.ConnectAsync(wsUri.Host, wsUri.Port);
                Task timeoutTask = Task.Delay(3000, internalCts.Token);
                Task completed = await Task.WhenAny(connectTask, timeoutTask);
                if (completed != connectTask || !tcp.Connected)
                {
                    result.Success = false;
                    result.StatusLine = "tcp_connect_failed";
                    VolcengineDebugLogger.Log(
                        DebugRunId,
                        "H7",
                        "VolcengineAsrWsClient.ProbeHandshakeAsync",
                        "probe_connect_failed",
                        new { host = wsUri.Host, port = wsUri.Port });
                    return result;
                }

                SslPolicyErrors sslErrors = SslPolicyErrors.None;
                using (SslStream ssl = new SslStream(
                    tcp.GetStream(),
                    false,
                    (_, _, _, errors) =>
                    {
                        sslErrors = errors;
                        return errors == SslPolicyErrors.None;
                    }))
                {
                    await ssl.AuthenticateAsClientAsync(wsUri.Host);
                    byte[] wsKeyBytes = new byte[16];
                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(wsKeyBytes);
                    }
                    string secKey = Convert.ToBase64String(wsKeyBytes);
                    StringBuilder req = new StringBuilder();
                    req.Append($"GET {wsUri.PathAndQuery} HTTP/1.1\r\n");
                    req.Append($"Host: {wsUri.Host}\r\n");
                    req.Append("Upgrade: websocket\r\n");
                    req.Append("Connection: Upgrade\r\n");
                    req.Append("Sec-WebSocket-Version: 13\r\n");
                    req.Append($"Sec-WebSocket-Key: {secKey}\r\n");
                    req.Append("User-Agent: Unity-Volcengine-ASR/1.0\r\n");
                    foreach (KeyValuePair<string, string> pair in headers)
                    {
                        req.Append($"{pair.Key}: {pair.Value}\r\n");
                    }

                    req.Append("\r\n");
                    byte[] reqBytes = Encoding.ASCII.GetBytes(req.ToString());
                    await ssl.WriteAsync(reqBytes, 0, reqBytes.Length, internalCts.Token);
                    await ssl.FlushAsync(internalCts.Token);

                    using (StreamReader reader = new StreamReader(ssl, Encoding.ASCII, false, 1024, true))
                    {
                        string statusLine = await reader.ReadLineAsync();
                        string header1 = await reader.ReadLineAsync();
                        string header2 = await reader.ReadLineAsync();
                        result.StatusLine = statusLine;
                        result.Success = !string.IsNullOrEmpty(statusLine) &&
                                         statusLine.IndexOf("101", StringComparison.OrdinalIgnoreCase) >= 0;

                        string line;
                        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                        {
                            // consume header lines until CRLF
                        }

                        if (!result.Success && ssl.CanRead)
                        {
                            char[] bodyBuf = new char[256];
                            int read = await reader.ReadAsync(bodyBuf, 0, bodyBuf.Length);
                            if (read > 0)
                            {
                                result.BodySnippet = new string(bodyBuf, 0, read).Replace("\r", " ").Replace("\n", " ");
                            }
                        }

                        VolcengineDebugLogger.Log(
                            DebugRunId,
                            "H7_H4_H2",
                            "VolcengineAsrWsClient.ProbeHandshakeAsync",
                            "probe_handshake_response",
                            new
                            {
                                statusLine = statusLine,
                                header1 = header1,
                                header2 = header2,
                                sslPolicyErrors = sslErrors.ToString(),
                                bodySnippet = result.BodySnippet
                            });
                    }
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.StatusLine = "probe_exception";
            VolcengineDebugLogger.Log(
                DebugRunId,
                "H7_H4",
                "VolcengineAsrWsClient.ProbeHandshakeAsync",
                "probe_exception",
                new
                {
                    exceptionType = ex.GetType().FullName,
                    message = ex.Message,
                    innerType = ex.InnerException?.GetType().FullName,
                    innerMessage = ex.InnerException?.Message
                });
            return result;
        }
    }

    public async Task SendFullRequestAsync(string uid)
    {
        EnsureConnected();
        byte[] payload = VolcengineAsrProtocol.BuildFullClientRequest(seq, settings, uid);
        seq++;
        await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, internalCts.Token);
    }

    public async Task StreamAudioAsync(byte[] rawPcmAudio, int sampleRate, int channels, int sampleBytes)
    {
        EnsureConnected();
        if (rawPcmAudio == null || rawPcmAudio.Length == 0)
        {
            throw new ArgumentException("音频数据不能为空。", nameof(rawPcmAudio));
        }

        int bytesPerSecond = sampleRate * channels * sampleBytes;
        int segmentSize = Mathf.Max(1, bytesPerSecond * Mathf.Max(40, settings.asrSegmentDurationMs) / 1000);
        List<ArraySegment<byte>> segments = Split(rawPcmAudio, segmentSize);
        for (int i = 0; i < segments.Count; i++)
        {
            bool isLast = i == segments.Count - 1;
            byte[] segmentBytes = new byte[segments[i].Count];
            Buffer.BlockCopy(rawPcmAudio, segments[i].Offset, segmentBytes, 0, segments[i].Count);

            byte[] packet = VolcengineAsrProtocol.BuildAudioOnlyRequest(seq, segmentBytes, isLast);
            await socket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, internalCts.Token);

            if (!isLast)
            {
                seq++;
                await Task.Delay(settings.asrSegmentDurationMs, internalCts.Token);
            }
        }
    }

    public async Task ReceiveLoopAsync(CancellationToken externalToken)
    {
        EnsureConnected();
        CancellationToken token = CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, externalToken).Token;
        byte[] buffer = new byte[16 * 1024];

        try
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        if (result.Count > 0)
                        {
                            ms.Write(buffer, 0, result.Count);
                        }
                    } while (!result.EndOfMessage);

                    if (ms.Length == 0)
                    {
                        continue;
                    }

                    VolcengineAsrProtocol.AsrResponse response = VolcengineAsrProtocol.ParseResponse(ms.ToArray());
                    // #region agent log
                    VolcengineDebugLogger.Log(
                        DebugRunId,
                        "H8",
                        "VolcengineAsrWsClient.ReceiveLoopAsync",
                        "response_received",
                        new
                        {
                            code = response.Code,
                            isLast = response.IsLastPackage,
                            payloadSeq = response.PayloadSequence,
                            hasPayload = response.PayloadMessage != null
                        });
                    // #endregion
                    DispatchResponse(response);
                    if (response.Code != 0)
                    {
                        string serverError = ExtractServerError(response);
                        DispatchError($"ASR 返回错误 code={response.Code}, detail={serverError}");
                        return;
                    }

                    if (response.IsLastPackage)
                    {
                        return;
                    }
                }
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            DispatchError($"接收消息失败: {ex.Message}");
            throw;
        }
    }

    public async Task RunSessionAsync(byte[] rawPcmAudio, int sampleRate, int channels, int sampleBytes, string uid, CancellationToken token)
    {
        try
        {
            await SendFullRequestAsync(uid);
            // #region agent log
            VolcengineDebugLogger.Log(
                DebugRunId,
                "H8",
                "VolcengineAsrWsClient.RunSessionAsync",
                "full_request_sent",
                new { seqAfterFullRequest = seq });
            // #endregion
            Task receiveTask = ReceiveLoopAsync(token);
            Task sendTask = StreamAudioAsync(rawPcmAudio, sampleRate, channels, sampleBytes);

            Task firstCompleted = await Task.WhenAny(receiveTask, sendTask);
            if (firstCompleted == receiveTask)
            {
                await receiveTask;
                if (!sendTask.IsCompleted)
                {
                    internalCts?.Cancel();
                    try
                    {
                        await sendTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // 接收端已先结束（通常是服务端返回错误），发送任务取消属于预期。
                    }
                }

                return;
            }

            await sendTask;
            // #region agent log
            VolcengineDebugLogger.Log(
                DebugRunId,
                "H8",
                "VolcengineAsrWsClient.RunSessionAsync",
                "audio_stream_sent_done",
                null);
            // #endregion

            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(8), token);
            Task completed = await Task.WhenAny(receiveTask, timeoutTask);
            if (completed == timeoutTask)
            {
                DispatchError("已发送音频，但等待识别结果超时。请检查 resourceId/modelName 或音频格式。");
                // #region agent log
                VolcengineDebugLogger.Log(
                    DebugRunId,
                    "H8",
                    "VolcengineAsrWsClient.RunSessionAsync",
                    "receive_timeout_after_send",
                    new { waitSeconds = 8 });
                // #endregion
                return;
            }

            await receiveTask;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            DispatchError($"ASR 会话失败: {ex.Message}");
            throw;
        }
    }

    private static string ExtractServerError(VolcengineAsrProtocol.AsrResponse response)
    {
        if (response == null || response.PayloadMessage == null)
        {
            return "no_payload";
        }

        JToken root = response.PayloadMessage;
        string message = root["message"]?.ToString();
        if (!string.IsNullOrEmpty(message))
        {
            return message;
        }

        string statusMessage = root["status_message"]?.ToString();
        if (!string.IsNullOrEmpty(statusMessage))
        {
            return statusMessage;
        }

        string error = root["error"]?.ToString();
        if (!string.IsNullOrEmpty(error))
        {
            return error;
        }

        return root.ToString(Newtonsoft.Json.Formatting.None);
    }

    public async Task CloseAsync()
    {
        if (socket == null)
        {
            return;
        }

        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client_close", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VolcengineAsrWsClient] Close 过程中发生异常: {ex.Message}");
        }
        finally
        {
            socket.Dispose();
            socket = null;
            internalCts?.Cancel();
            internalCts?.Dispose();
            internalCts = null;
            seq = 1;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (socket != null)
        {
            try
            {
                socket.Dispose();
            }
            catch
            {
                // ignore
            }

            socket = null;
        }

        internalCts?.Cancel();
        internalCts?.Dispose();
        internalCts = null;
    }

    private void DispatchResponse(VolcengineAsrProtocol.AsrResponse response)
    {
        PostToMainThread(() =>
        {
            OnResponse?.Invoke(response);
            string text = ExtractText(response.PayloadMessage);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (response.IsLastPackage)
            {
                OnFinalText?.Invoke(text);
            }
            else
            {
                OnPartialText?.Invoke(text);
            }
        });
    }

    private void DispatchError(string message)
    {
        PostToMainThread(() => OnError?.Invoke(message));
    }

    private string ExtractText(JObject payload)
    {
        if (payload == null)
        {
            return string.Empty;
        }

        // 兼容常见结构：{result:{text:""}}、{text:""}、{utterances:[{text:""}]}
        JToken resultToken = payload["result"];
        string direct = resultToken?["text"]?.ToString();
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        string rootText = payload["text"]?.ToString();
        if (!string.IsNullOrEmpty(rootText))
        {
            return rootText;
        }

        JArray utterances = payload["utterances"] as JArray;
        if (utterances != null)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < utterances.Count; i++)
            {
                string s = utterances[i]?["text"]?.ToString();
                if (string.IsNullOrEmpty(s))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(" ");
                }

                sb.Append(s);
            }

            return sb.ToString();
        }

        return string.Empty;
    }

    private void PostToMainThread(Action action)
    {
        if (action == null)
        {
            return;
        }

        if (unityContext == null)
        {
            action.Invoke();
            return;
        }

        unityContext.Post(_ => action.Invoke(), null);
    }

    private void ValidateSettings()
    {
        if (settings == null)
        {
            throw new InvalidOperationException("VolcengineSettings 未配置。");
        }

        if (string.IsNullOrEmpty(settings.asrWsUrl))
        {
            throw new InvalidOperationException("asrWsUrl 不能为空。");
        }

        if (string.IsNullOrEmpty(settings.appKey))
        {
            throw new InvalidOperationException("appKey 不能为空。");
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("WebSocket 尚未连接。");
        }
    }

    private static List<ArraySegment<byte>> Split(byte[] data, int segmentSize)
    {
        List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
        if (data == null || data.Length == 0 || segmentSize <= 0)
        {
            return result;
        }

        for (int i = 0; i < data.Length; i += segmentSize)
        {
            int count = Math.Min(segmentSize, data.Length - i);
            result.Add(new ArraySegment<byte>(data, i, count));
        }

        return result;
    }
}
