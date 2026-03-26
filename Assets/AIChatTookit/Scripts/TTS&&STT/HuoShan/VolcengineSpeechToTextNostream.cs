using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class VolcengineSpeechToTextNostream : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private VolcengineSettings settings;
    [SerializeField] private string microphoneDevice = "";
    [SerializeField] private int microphoneFrequency = 16000;
    [SerializeField] private int maxRecordSeconds = 20;
    [SerializeField] private int recognizeTimeoutSeconds = 40;
    [SerializeField] private string userId = "unity_uid";

    public event Action<string> PartialTextReceived;
    public event Action<string> FinalTextReceived;
    public event Action<string> ErrorOccurred;

    private AudioClip recordingClip;
    private bool isRecording;
    private bool isRecognizing;
    private VolcengineAsrWsClient wsClient;
    private CancellationTokenSource sessionCts;
    private string latestFinalText = string.Empty;

    public bool IsRecording => isRecording;
    public bool IsRecognizing => isRecognizing;
    public string LatestFinalText => latestFinalText;

    public void StartRecordAndRecognize()
    {
        if (isRecording)
        {
            Debug.LogWarning("[VolcengineSpeechToTextNostream] 当前已在录音中。", this);
            return;
        }

        if (settings == null)
        {
            EmitError("VolcengineSettings 未绑定。");
            return;
        }

        try
        {
            string selectedDevice = ResolveMicrophoneDevice();
            recordingClip = Microphone.Start(selectedDevice, false, maxRecordSeconds, microphoneFrequency);
            isRecording = true;
            latestFinalText = string.Empty;
            Debug.Log($"[VolcengineSpeechToTextNostream] 开始录音，设备：{selectedDevice}。", this);
        }
        catch (Exception ex)
        {
            isRecording = false;
            EmitError($"开始录音失败: {ex.Message}");
        }
    }

    public async void StopAndFlush()
    {
        if (!isRecording)
        {
            // 某些设备上可能出现状态不同步：isRecording=false 但麦克风仍在采集。
            string recoverDevice = ResolveMicrophoneDeviceSafe();
            if (!string.IsNullOrEmpty(recoverDevice) && Microphone.IsRecording(recoverDevice))
            {
                isRecording = true;
                Debug.LogWarning("[VolcengineSpeechToTextNostream] 检测到录音状态不同步，已自动恢复后继续停止流程。", this);
            }
            else
            {
                if (isRecognizing)
                {
                    Debug.LogWarning("[VolcengineSpeechToTextNostream] 当前正在识别中，请等待结果返回。", this);
                }
                else
                {
                    Debug.LogWarning("[VolcengineSpeechToTextNostream] 当前未在录音。请先点击开始录音。", this);
                }
                return;
            }
        }

        try
        {
            string selectedDevice = ResolveMicrophoneDevice();
            int position = Microphone.GetPosition(selectedDevice);
            Microphone.End(selectedDevice);
            isRecording = false;

            if (recordingClip == null || position <= 0)
            {
                EmitError("录音为空或时长过短。");
                return;
            }

            AudioClip trimmed = TrimClip(recordingClip, position);
            await RecognizeClipAsync(trimmed);
        }
        catch (Exception ex)
        {
            isRecording = false;
            EmitError($"停止录音失败: {ex.Message}");
        }
    }

    public void CancelCurrentSession()
    {
        sessionCts?.Cancel();
    }

    private async Task RecognizeClipAsync(AudioClip clip)
    {
        if (clip == null)
        {
            EmitError("待识别音频为空。");
            return;
        }

        byte[] wavBytes = WavUtility.FromAudioClip(clip);
        if (!TryExtractWavData(wavBytes, out byte[] pcmData, out int sampleRate, out int channels, out int sampleBytes))
        {
            EmitError("WAV 数据解析失败。");
            return;
        }

        if (channels != 1)
        {
            Debug.LogWarning($"[VolcengineSpeechToTextNostream] 当前为 {channels} 声道，建议录制为单声道。", this);
        }

        sessionCts?.Cancel();
        sessionCts?.Dispose();
        sessionCts = new CancellationTokenSource(TimeSpan.FromSeconds(Mathf.Max(5, recognizeTimeoutSeconds)));

        wsClient?.Dispose();
        wsClient = new VolcengineAsrWsClient(settings);
        string runId = $"asr_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        wsClient.DebugRunId = runId;
        wsClient.OnPartialText += HandlePartialText;
        wsClient.OnFinalText += HandleFinalText;
        wsClient.OnError += HandleError;

        // #region agent log
        VolcengineDebugLogger.Log(
            runId,
            "H1_H2_H3_H5",
            "VolcengineSpeechToTextNostream.RecognizeClipAsync",
            "recognize_start",
            new
            {
                platform = Application.platform.ToString(),
                internetReachability = Application.internetReachability.ToString(),
                asrWsUrl = settings != null ? settings.asrWsUrl : string.Empty,
                asrLanguage = settings != null ? settings.asrLanguage : string.Empty,
                asrModelName = settings != null ? settings.asrModelName : string.Empty,
                asrResourceId = settings != null ? settings.asrResourceId : string.Empty,
                appKeyConfigured = settings != null && !string.IsNullOrEmpty(settings.appKey),
                accessKeyConfigured = settings != null && !string.IsNullOrEmpty(settings.accessKey),
                segmentDurationMs = settings != null ? settings.asrSegmentDurationMs : -1,
                timeoutSeconds = recognizeTimeoutSeconds,
                sampleRate = sampleRate,
                channels = channels,
                sampleBytes = sampleBytes,
                wavLength = wavBytes != null ? wavBytes.Length : 0,
                pcmLength = pcmData != null ? pcmData.Length : 0
            });
        // #endregion

        isRecognizing = true;
        try
        {
            await wsClient.ConnectAsync(sessionCts.Token);
            // 与 Python 示例一致：audio.format=wav 时发送包含 WAV 头的完整字节流。
            await wsClient.RunSessionAsync(wavBytes, sampleRate, channels, sampleBytes, userId, sessionCts.Token);
        }
        catch (OperationCanceledException)
        {
            EmitError("识别任务被取消或超时。");
        }
        catch (Exception ex)
        {
            // #region agent log
            VolcengineDebugLogger.Log(
                runId,
                "H3_H4_H5",
                "VolcengineSpeechToTextNostream.RecognizeClipAsync",
                "recognize_exception",
                new
                {
                    exceptionType = ex.GetType().FullName,
                    message = ex.Message,
                    innerType = ex.InnerException?.GetType().FullName,
                    innerMessage = ex.InnerException?.Message,
                    timeoutCanceled = sessionCts != null && sessionCts.IsCancellationRequested
                });
            // #endregion
            EmitError($"识别失败: {ex.Message}");
        }
        finally
        {
            await wsClient.CloseAsync();
            wsClient.Dispose();
            wsClient = null;
            isRecognizing = false;
        }
    }

    private void HandlePartialText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Debug.Log($"[VolcengineSpeechToTextNostream] Partial: {text}", this);
        PartialTextReceived?.Invoke(text);
    }

    private void HandleFinalText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        latestFinalText = text;
        Debug.Log($"[VolcengineSpeechToTextNostream] Final: {text}", this);
        FinalTextReceived?.Invoke(text);
    }

    private void HandleError(string message)
    {
        EmitError(message);
    }

    private void EmitError(string message)
    {
        Debug.LogError($"[VolcengineSpeechToTextNostream] {message}", this);
        ErrorOccurred?.Invoke(message);
    }

    private AudioClip TrimClip(AudioClip source, int sampleFrames)
    {
        int channels = Mathf.Max(1, source.channels);
        int totalSamples = sampleFrames * channels;
        float[] data = new float[totalSamples];
        source.GetData(data, 0);

        AudioClip trimmed = AudioClip.Create("mic_trimmed", sampleFrames, channels, source.frequency, false);
        trimmed.SetData(data, 0);
        return trimmed;
    }

    private string ResolveMicrophoneDevice()
    {
        if (!string.IsNullOrEmpty(microphoneDevice))
        {
            return microphoneDevice;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            throw new InvalidOperationException("未检测到可用麦克风设备。");
        }

        return Microphone.devices[0];
    }

    private string ResolveMicrophoneDeviceSafe()
    {
        try
        {
            return ResolveMicrophoneDevice();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryExtractWavData(byte[] wav, out byte[] dataChunk, out int sampleRate, out int channels, out int sampleBytes)
    {
        dataChunk = null;
        sampleRate = 16000;
        channels = 1;
        sampleBytes = 2;

        try
        {
            using (MemoryStream ms = new MemoryStream(wav))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                string riff = new string(reader.ReadChars(4));
                reader.ReadInt32(); // chunk size
                string wave = new string(reader.ReadChars(4));
                if (riff != "RIFF" || wave != "WAVE")
                {
                    return false;
                }

                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();
                    if (chunkSize < 0 || reader.BaseStream.Position + chunkSize > reader.BaseStream.Length)
                    {
                        return false;
                    }

                    if (chunkId == "fmt ")
                    {
                        ushort audioFormat = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32(); // byte rate
                        reader.ReadUInt16(); // block align
                        ushort bits = reader.ReadUInt16();
                        sampleBytes = Mathf.Max(1, bits / 8);

                        int remain = chunkSize - 16;
                        if (remain > 0)
                        {
                            reader.BaseStream.Position += remain;
                        }

                        if (audioFormat != 1)
                        {
                            return false;
                        }
                    }
                    else if (chunkId == "data")
                    {
                        dataChunk = reader.ReadBytes(chunkSize);
                        return dataChunk != null && dataChunk.Length > 0;
                    }
                    else
                    {
                        reader.BaseStream.Position += chunkSize;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private void OnDisable()
    {
        if (isRecording)
        {
            try
            {
                Microphone.End(ResolveMicrophoneDevice());
            }
            catch
            {
                // ignore
            }

            isRecording = false;
        }

        sessionCts?.Cancel();
    }

    private async void OnDestroy()
    {
        sessionCts?.Cancel();
        sessionCts?.Dispose();
        sessionCts = null;

        if (wsClient != null)
        {
            await wsClient.CloseAsync();
            wsClient.Dispose();
            wsClient = null;
        }
    }
}
