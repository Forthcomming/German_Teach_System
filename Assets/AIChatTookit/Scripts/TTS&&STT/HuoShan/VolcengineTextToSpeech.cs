using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;


public class VolcengineTextToSpeech : MonoBehaviour
{
    public VolcengineSettings settings;
    public Lipsync3 lipsync3Instance; // 在 Inspector 里拖你的 Lipsync3 脚本挂着的对象

    private string apiUrl = "https://openspeech.bytedance.com/api/v1/tts";

    
    public IEnumerator RequestTextToSpeech(string text, Action<AudioClip, string> callback)
    {
        if (settings == null)
        {
            Debug.LogError("VolcengineSettings 未设置！");
            yield break;
        }

        string reqId = Guid.NewGuid().ToString();

        var payload = new
        {
            app = new
            {
                appid = settings.appId,
                token = settings.accessToken,
                cluster = settings.cluster
            },
            user = new
            {
                uid = SystemInfo.deviceUniqueIdentifier
            },
            audio = new
            {
                voice_type = settings.voiceType,
                encoding = "pcm",
                speed_ratio = settings.speedRatio,
                volume_ratio = settings.volumeRatio,
                pitch_ratio = settings.pitchRatio
            },
            request = new
            {
                reqid = reqId,
                text = text,
                text_type = "plain",
                operation = "query",
                with_frontend = 1,
                frontend_type = "unitTson",
                with_timestamp = 0 // 不需要时间戳
            }
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer;{settings.accessToken}");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                
                try
                {
                    JObject jsonResponse = JObject.Parse(webRequest.downloadHandler.text);
                    string base64Audio = jsonResponse["data"]?.ToString();

                    if (!string.IsNullOrEmpty(base64Audio))
                    {
                        AudioClip clip = AudioBase64ToAudioClip(base64Audio, settings.channels, settings.sampleRate);
                        callback?.Invoke(clip, text);
                    }
                    else
                    {
                        Debug.LogError("TTS 响应中未找到音频数据");
                    }
                    // ====== 3. 解析 addition.frontend（注意它是字符串） ======
                    // 音素数据解析
                    string frontendStr = jsonResponse["addition"]?["frontend"]?.ToString();
                    if (!string.IsNullOrEmpty(frontendStr))
                      {
                      JObject frontendJson = JObject.Parse(frontendStr);
                      var phonemes = frontendJson["phonemes"];
                        // 找到本句的最小 start_time 作为基准
                        float baseOffset = float.MaxValue;
                        foreach (var p in phonemes)
                        {
                            float st = p["start_time"]?.ToObject<float>() ?? 0f;
                            if (st < baseOffset) baseOffset = st;
                        }
                        if (baseOffset == float.MaxValue) baseOffset = 0f;

                        // 构造归零后的 list
                        var list = new List<Lipsync3.PhonemeData>();
                        foreach (var p in phonemes)
                        {
                            string phone = p["phone"]?.ToString();
                            float st = p["start_time"]?.ToObject<float>() ?? 0f;
                            float et = p["end_time"]?.ToObject<float>() ?? 0f;

                            list.Add(new Lipsync3.PhonemeData
                            {
                                phoneme = phone,
                                startTime = st - baseOffset, // 时间归零
                                endTime = et - baseOffset
                            });
                        }

                        // 一次性加载新句子
                        lipsync3Instance.LoadNewSentencePhonemes(list);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"解析火山 TTS 响应失败: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"火山 TTS 请求失败: {webRequest.error}");
            }
        }
    }
    public void VoiceTTS1(string text, Action<AudioClip, string> _callback)
    {
        StartCoroutine(RequestTextToSpeech(text, _callback));
    }
    private AudioClip AudioBase64ToAudioClip(string base64Audio, int channels, int sampleRate)
    {
        try
        {
            byte[] audioBytes = Convert.FromBase64String(base64Audio);
            int sampleCount = audioBytes.Length / (channels * 2);

            short[] shortSamples = new short[sampleCount * channels];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    int index = (i * channels + c) * 2;
                    shortSamples[i * channels + c] = (short)((audioBytes[index + 1] << 8) | audioBytes[index]);
                }
            }

            float[] floatSamples = new float[sampleCount * channels];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    floatSamples[i * channels + c] = shortSamples[i * channels + c] / 32768.0f;
                }
            }

            AudioClip audioClip = AudioClip.Create("VolcengineTTS", sampleCount, channels, sampleRate, false);
            audioClip.SetData(floatSamples, 0);
            return audioClip;
        }
        catch (Exception e)
        {
            Debug.LogError($"Base64 音频解码失败: {e.Message}");
            return null;
        }
    }
    

}
