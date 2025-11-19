using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI; // 如果需要UI元素，如Text组件来显示响应

public class VoiceCloneTTS : MonoBehaviour
{
    // 填写你的API凭据和参数
    public string code = "1";
    public string appId = "7925191101";
    public string accessToken = "jC8xF0tlz9_OESYM5mVsxf3wTtzj0tDe";
    public string cluster = "volcano_tts";

    private string apiUrl = "https://openspeech.bytedance.com/api/v1/tts";

    // 用于存储响应的音频数据
    private byte[] audioData;

    void Start()
    {
        //StartCoroutine(RequestTextToSpeech("speaker_id","字节跳动语音合成"));
    }

    // 协程用于处理异步HTTP请求
    public IEnumerator RequestTextToSpeech(string text, Action<AudioClip, string> _callback = null)
    {
        string ReqId = Guid.NewGuid().ToString();

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            webRequest.SetRequestHeader("Authorization", $"Bearer;{accessToken}");
            webRequest.SetRequestHeader("Content-Type", "application/json");

            string jsonPayload = JsonUtility.ToJson(new requestPayload
            {
                app = new appPayload { appid = appId, token = accessToken, cluster = cluster },
                user = new userPayload { uid = code },
                audio = new audioPayload { voice_type = "zh_female_yingyujiaoyu_mars_bigtts", encoding = "pcm", speed_ratio = 1.0f, volume_ratio = 1.0f, pitch_ratio = 0.0f },
                request = new requestDetails { reqid = ReqId, text = text, text_type = "plain", operation = "query", with_frontend = 1, frontend_type = "unitTson" }
            });

            Debug.Log("RequestTextToSpeech jsonPayload to: " + jsonPayload);

            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseText = webRequest.downloadHandler.text;
                Debug.Log("RequestTextToSpeech response: " + responseText);

                try
                {
                    JObject responseJson = JObject.Parse(responseText);

                    if (responseJson.TryGetValue("data", out JToken dataToken) && dataToken.Type == JTokenType.String)
                    {
                        string base64Audio = dataToken.ToString();

                        AudioClip audioClip = AudioBase64ToAudioClip(base64Audio, 1, 24000);

                        if (_callback != null)
                        {
                            _callback(audioClip, text);
                        }
                    }
                    else
                    {
                        Debug.LogError("Response JSON does not contain valid 'data' field or it is not a string.");
                        Debug.LogError("Full response: " + responseText);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Exception while parsing JSON response: " + ex);
                    Debug.LogError("Response text: " + responseText);
                }
            }
            else
            {
                Debug.LogError("Status request failed: " + webRequest.error + " Response: " + webRequest.downloadHandler.text);
            }
        }
    }

    /// <summary>
    /// 语音合成，返回合成文本
    /// </summary>
    public void VoiceTTS(string text, Action<AudioClip, string> _callback)
    {
        StartCoroutine(RequestTextToSpeech(text, _callback));
    }
    public AudioClip AudioBase64ToAudioClip(string base64AudioData, int channels, int sampleRate)
    {
        try
        {
            // 解码Base64字符串
            byte[] audioBytes = Convert.FromBase64String(base64AudioData);

            // 计算样本数（每个样本的字节数取决于位数和通道数）
            // 这里假设每个样本是16位（2个字节）
            int sampleCount = audioBytes.Length / (channels * 2);

            // 将字节数组转换为short数组
            short[] shortSamples = new short[sampleCount * channels];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    int index = (i * channels + c) * 2;
                    shortSamples[i * channels + c] = (short)((audioBytes[index + 1] << 8) | audioBytes[index]);
                }
            }

            // 将short数组转换为float数组（范围通常是-1到1）
            float[] floatSamples = new float[sampleCount * channels];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    // 归一化到-1到1之间
                    floatSamples[i * channels + c] = shortSamples[i * channels + c] / 32768.0f;
                }
            }

            // 创建AudioClip对象并设置数据
            string clipName = "audioClip_" + Guid.NewGuid().ToString("N"); // 生成唯一名称
            AudioClip audioClip = AudioClip.Create(clipName, sampleCount, channels, sampleRate, false);
            audioClip.SetData(floatSamples, 0);
            return audioClip;
        }
        catch (FormatException)
        {
            Debug.LogError("Base64解码失败：提供的字符串不是有效的Base64编码。");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError("处理Base64音频数据时发生异常：" + e.Message);
            return null;
        }
    }

    // 用于序列化和反序列化的类结构（需要根据实际的API响应和请求结构进行调整）
    [System.Serializable]
    public class appPayload
    {
        public string appid;
        public string token;
        public string cluster;
    }

    [System.Serializable]
    public class userPayload
    {
        public string uid;
    }

    [System.Serializable]
    public class audioPayload
    {
        public string voice_type;
        public string encoding;
        public float speed_ratio;
        public float volume_ratio;
        public float pitch_ratio;
    }

    [System.Serializable]
    public class requestDetails
    {
        public string reqid;
        public string text;
        public string text_type;
        public string operation;
        public int with_frontend;
        public string frontend_type;
    }

    [System.Serializable]
    public class requestPayload
    {
        public appPayload app;
        public userPayload user;
        public audioPayload audio;
        public requestDetails request;
    }
}