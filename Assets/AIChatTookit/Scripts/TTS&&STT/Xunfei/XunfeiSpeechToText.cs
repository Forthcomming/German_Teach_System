using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
public class XunfeiSpeechToText : STT
{
    #region 配置参数
    /// <summary>
    /// 讯飞语音识别配置组件
    /// </summary>
    [SerializeField] private XunfeiSettings m_XunfeiSettings;
    /// <summary>
    /// 接口 host 地址
    /// </summary>
    [SerializeField] private string m_HostUrl = "iat-api.xfyun.cn";
    /// <summary>
    /// 识别语言
    /// </summary>
    [SerializeField] private string m_Language = "zh_cn";
    /// <summary>
    /// 业务领域参数
    /// </summary>
    [SerializeField] private string m_Domain = "iat";
    /// <summary>
    /// 方言参数，中文场景默认 mandarin，非中文语言通常留空
    /// </summary>
    [SerializeField] private string m_Accent = "mandarin";
    /// <summary>
    /// 音频格式参数
    /// </summary>
    [SerializeField] private string m_Format = "audio/L16;rate=16000";
    /// <summary>
    /// 音频编码参数
    /// </summary>
    [SerializeField] private string m_Encoding = "raw";
    #endregion
    /// <summary>
    /// websocket
    /// </summary>
    private ClientWebSocket m_WebSocket;
    private CancellationToken m_CancellationToken;

    private void Awake()
    {
        m_XunfeiSettings = this.GetComponent<XunfeiSettings>();
        m_SpeechRecognizeURL = "wss://iat-api.xfyun.cn/v2/iat";
    }


    /// <summary>
    /// 将 AudioClip 转为文字
    /// </summary>
    /// <param name="_clip"></param>
    /// <param name="_callback"></param>
    public override void SpeechToText(AudioClip _clip, Action<string> _callback)
    {
        byte[] _audioData = ConvertClipToBytes(_clip);
        StartCoroutine(SendAudioData(_audioData, _callback));
    }
    /// <summary>
    /// 将字节音频转为文字
    /// </summary>
    /// <param name="_audioData"></param>
    /// <param name="_callback"></param>
    public override void SpeechToText(byte[] _audioData, Action<string> _callback)
    {
        StartCoroutine(SendAudioData(_audioData, _callback));
    }


    #region 鉴权 URL 生成

    /// <summary>
    /// 生成带鉴权参数的 websocket url
    /// </summary>
    /// <returns></returns>
    private string GetUrl()
    {
        // 获取 RFC1123 格式时间
        string date = DateTime.Now.ToString("r");
        // 拼接待签名字符串
        string signature_origin = string.Format("host: " + m_HostUrl + "\ndate: " + date + "\nGET /v2/iat HTTP/1.1");
        // 使用 hmac-sha256 签名，并将结果做 base64
        string signature = Convert.ToBase64String(new HMACSHA256(Encoding.UTF8.GetBytes(m_XunfeiSettings.m_APISecret)).ComputeHash(Encoding.UTF8.GetBytes(signature_origin)));
        // 拼接 authorization 原文
        string authorization_origin = string.Format("api_key=\"{0}\",algorithm=\"hmac-sha256\",headers=\"host date request-line\",signature=\"{1}\"", m_XunfeiSettings.m_APIKey, signature);
        // 对 authorization 做 base64 编码
        string authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization_origin));
        // 组合最终请求 url
        string url = string.Format("{0}?authorization={1}&date={2}&host={3}", m_SpeechRecognizeURL, authorization, date, m_HostUrl);

        return url;
    }

    #endregion

    #region 发送与识别

    /// <summary>
    /// 协程入口，发送音频数据
    /// </summary>
    /// <param name="_audioData"></param>
    /// <param name="_callback"></param>
    /// <returns></returns>
    public IEnumerator SendAudioData(byte[] _audioData, Action<string> _callback)
    {
        yield return null;
        ConnetHostAndRecognize(_audioData, _callback);
    }

    /// <summary>
    /// 连接讯飞服务并持续接收识别结果
    /// </summary>
    /// <param name="_audioData"></param>
    /// <param name="_callback"></param>
    private async void ConnetHostAndRecognize(byte[] _audioData, Action<string> _callback)
    {
        try
        {
            stopwatch.Restart();
            // 创建并连接 websocket
            m_WebSocket = new ClientWebSocket();
            m_CancellationToken = new CancellationToken();
            Uri uri = new Uri(GetUrl());
            await m_WebSocket.ConnectAsync(uri, m_CancellationToken);
            // 发送音频数据
            SendVoiceData(_audioData, m_WebSocket);
            StringBuilder stringBuilder = new StringBuilder();
            while (m_WebSocket.State == WebSocketState.Open)
            {
                var result = new byte[4096];
                WebSocketReceiveResult receiveResult = await m_WebSocket.ReceiveAsync(new ArraySegment<byte>(result), m_CancellationToken);
                if (receiveResult.MessageType == WebSocketMessageType.Close || receiveResult.Count <= 0)
                {
                    break;
                }

                string str = Encoding.UTF8.GetString(result, 0, receiveResult.Count);
                // 反序列化返回 json
                ResponseData _responseData = JsonUtility.FromJson<ResponseData>(str);
                if (_responseData.code == 0)
                {
                    stringBuilder.Append(GetWords(_responseData));
                    if (_responseData.data != null && _responseData.data.status == 2)
                    {
                        break;
                    }
                }
                else
                {
                    PrintErrorLog(_responseData.code);
                    Debug.LogError($"讯飞识别失败 code={_responseData.code}, message={_responseData.message}");

                }
            }

            string _resultMsg = stringBuilder.ToString();
            // 回调最终识别文本
            _callback(_resultMsg);

            stopwatch.Stop();
            Debug.Log("讯飞语音识别耗时（秒）: " + stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            Debug.LogError("讯飞识别异常: " + ex.Message);
            m_WebSocket.Dispose();
        }

    }

    /// <summary>
    /// 从响应结构中提取识别词串
    /// </summary>
    /// <param name="_responseData"></param>
    /// <returns></returns>
    private string GetWords(ResponseData _responseData)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var item in _responseData.data.result.ws)
        {
            foreach (var _cw in item.cw)
            {
                stringBuilder.Append(_cw.w);
            }
        }

        return stringBuilder.ToString();
    }


    private void SendVoiceData(byte[] audio, ClientWebSocket socket)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        string language = (m_Language ?? string.Empty).Trim().Replace("-", "_").ToLowerInvariant();
        string accent = language.StartsWith("zh_") ? m_Accent : string.Empty;

        PostData _postData = new PostData()
        {
            common = new CommonTag(m_XunfeiSettings.m_AppID),
            business = new BusinessTag(language, m_Domain, accent),
            data = new DataTag(2, m_Format, m_Encoding, Convert.ToBase64String(audio))
        };

        string _jsonData = JsonUtility.ToJson(_postData);

        // 发送整包数据（当前实现为一次性发送）
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(_jsonData)), WebSocketMessageType.Text, true, new CancellationToken());
    }


    #endregion


    #region 音频与错误处理
    /// <summary>
    /// 将 AudioClip 转为 PCM byte[]
    /// </summary>
    /// <param name="audioClip"></param>
    /// <returns></returns>
    public byte[] ConvertClipToBytes(AudioClip audioClip)
    {
        float[] samples = new float[audioClip.samples];

        audioClip.GetData(samples, 0);

        short[] intData = new short[samples.Length];

        byte[] bytesData = new byte[samples.Length * 2];

        int rescaleFactor = 32767;

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = new byte[2];
            byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        return bytesData;
    }
    public AudioClip ConvertBytesToClip(byte[] rawData)
    {
        float[] samples = new float[rawData.Length / 2];
        float rescaleFactor = 32767;
        short st = 0;
        float ft = 0;

        for (int i = 0; i < rawData.Length; i += 2)
        {
            st = BitConverter.ToInt16(rawData, i);
            ft = st / rescaleFactor;
            samples[i / 2] = ft;
        }

        AudioClip audioClip = AudioClip.Create("mySound", samples.Length, 1, 16000, false);
        audioClip.SetData(samples, 0);

        return audioClip;
    }
    /// <summary>
    /// 根据错误码输出可读日志
    /// </summary>
    /// <param name="status"></param>
    private void PrintErrorLog(int status)
    {
        if (status == 10005)
        {
            Debug.LogError("appid 未注册");
            return;
        }
        if (status == 10006)
        {
            Debug.LogError("日流控超限，请稍后重试");
            return;
        }
        if (status == 10007)
        {
            Debug.LogError("应用未授权或权限不足");
            return;
        }
        if (status == 10010)
        {
            Debug.LogError("无效的请求参数");
            return;
        }
        if (status == 10019)
        {
            Debug.LogError("session 超时");
            return;
        }
        if (status == 10043)
        {
            Debug.LogError("音频解码失败");
            return;
        }
        if (status == 10101)
        {
            Debug.LogError("引擎内部错误");
            return;
        }
        if (status == 10313)
        {
            Debug.LogError("appid 没有调用权限");
            return;
        }
        if (status == 10317)
        {
            Debug.LogError("引擎繁忙");
            return;
        }
        if (status == 11200)
        {
            Debug.LogError("auth 失败");
            return;
        }
        if (status == 11201)
        {
            Debug.LogError("鉴权参数错误");
            return;
        }
        if (status == 10160)
        {
            Debug.LogError("请求数据格式错误");
            return;
        }
        if (status == 10161)
        {
            Debug.LogError("base64 解码失败");
            return;
        }
        if (status == 10163)
        {
            Debug.LogError("音频参数不符合要求，请检查采样率、声道和编码");
            return;
        }
        if (status == 10200)
        {
            Debug.LogError("会话已断开");
            return;
        }
        if (status == 10222)
        {
            Debug.LogError("文本为空");
            return;
        }
    }


    #endregion

    #region 数据结构定义

    /// <summary>
    /// 请求体根结构
    /// </summary>
    [Serializable]
    public class PostData
    {
        [SerializeField] public CommonTag common;
        [SerializeField] public BusinessTag business;
        [SerializeField] public DataTag data;
    }


    [Serializable]
    public class CommonTag
    {
        [SerializeField] public string app_id = string.Empty;
        public CommonTag(string app_id)
        {
            this.app_id = app_id;
        }
    }
    [Serializable]
    public class BusinessTag
    {
        [SerializeField] public string language = "zh_cn";
        [SerializeField] public string domain = "iat";
        [SerializeField] public string accent = "mandarin";
        public BusinessTag(string language, string domain, string accent)
        {
            this.language = language;
            this.domain = domain;
            this.accent = accent;
        }
    }

    [Serializable]
    public class DataTag
    {
        [SerializeField] public int status = 2;
        [SerializeField] public string format = "audio/L16;rate=16000";
        [SerializeField] public string encoding = "raw";
        [SerializeField] public string audio = string.Empty;
        public DataTag(int status, string format, string encoding, string audio)
        {
            this.status = status;
            this.format = format;
            this.encoding = encoding;
            this.audio = audio;
        }
    }


    [Serializable]
    public class ResponseData
    {
        [SerializeField] public int code = 0;
        [SerializeField] public string message = string.Empty;
        [SerializeField] public string sid = string.Empty;
        [SerializeField] public ResponcsedataTag data;
    }

    [Serializable]
    public class ResponcsedataTag
    {
        [SerializeField] public Results result;
        [SerializeField] public int status = 2;
    }

    [Serializable]
    public class Results
    {
        [SerializeField] public List<WsTag> ws;
    }

    [Serializable]
    public class WsTag
    {
        [SerializeField] public List<CwTag> cw;
    }

    [Serializable]
    public class CwTag
    {
        [SerializeField] public int sc = 0;
        [SerializeField] public string w = string.Empty;
    }

    #endregion

}
