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
    #region ????
    /// <summary>
    /// ???????????
    /// </summary>
    [SerializeField] private XunfeiSettings m_XunfeiSettings;
    /// <summary>
    /// host???
    /// </summary>
    [SerializeField] private string m_HostUrl = "iat-api.xfyun.cn";
    /// <summary>
    /// ????
    /// </summary>
    [SerializeField] private string m_Language = "zh_cn";
    /// <summary>
    /// ???????
    /// </summary>
    [SerializeField] private string m_Domain = "iat";
    /// <summary>
    /// ????mandarin?????????????????????
    /// </summary>
    [SerializeField] private string m_Accent = "mandarin";
    /// <summary>
    /// ??????????
    /// </summary>
    [SerializeField] private string m_Format = "audio/L16;rate=16000";
    /// <summary>
    /// ?????????
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
    /// ???????
    /// </summary>
    /// <param name="_clip"></param>
    /// <param name="_callback"></param>
    public override void SpeechToText(AudioClip _clip, Action<string> _callback)
    {
        byte[] _audioData = ConvertClipToBytes(_clip);
        StartCoroutine(SendAudioData(_audioData, _callback));
    }
    /// <summary>
    /// ???????
    /// </summary>
    /// <param name="_audioData"></param>
    /// <param name="_callback"></param>
    public override void SpeechToText(byte[] _audioData, Action<string> _callback)
    {
        StartCoroutine(SendAudioData(_audioData, _callback));
    }


    #region ??????Url

    /// <summary>
    /// ??????url
    /// </summary>
    /// <returns></returns>
    private string GetUrl()
    {
        //???????
        string date = DateTime.Now.ToString("r");
        //???????signature
        string signature_origin = string.Format("host: " + m_HostUrl + "\ndate: " + date + "\nGET /v2/iat HTTP/1.1");
        //hmac-sha256??-???????????base64????
        string signature = Convert.ToBase64String(new HMACSHA256(Encoding.UTF8.GetBytes(m_XunfeiSettings.m_APISecret)).ComputeHash(Encoding.UTF8.GetBytes(signature_origin)));
        //???????authorization
        string authorization_origin = string.Format("api_key=\"{0}\",algorithm=\"hmac-sha256\",headers=\"host date request-line\",signature=\"{1}\"", m_XunfeiSettings.m_APIKey, signature);
        //????base64????
        string authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization_origin));
        //???????url
        string url = string.Format("{0}?authorization={1}&date={2}&host={3}", m_SpeechRecognizeURL, authorization, date, m_HostUrl);

        return url;
    }

    #endregion

    #region ???????

    /// <summary>
    /// ???????
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
    /// ????????????
    /// </summary>
    /// <param name="_audioData"></param>
    /// <param name="_callback"></param>
    private async void ConnetHostAndRecognize(byte[] _audioData, Action<string> _callback)
    {
        try
        {
            stopwatch.Restart();
            //????socket????
            m_WebSocket = new ClientWebSocket();
            m_CancellationToken = new CancellationToken();
            Uri uri = new Uri(GetUrl());
            await m_WebSocket.ConnectAsync(uri, m_CancellationToken);
            //??????
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
                //????????json
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
                    Debug.LogError($"Ñ¶·ÉÊ¶±ðÊ§°Ü code={_responseData.code}, message={_responseData.message}");

                }
            }

            string _resultMsg = stringBuilder.ToString();
            //??????????
            _callback(_resultMsg);

            stopwatch.Stop();
            Debug.Log("??????????????" + stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            Debug.LogError("???????: " + ex.Message);
            m_WebSocket.Dispose();
        }

    }

    /// <summary>
    /// ??????????
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

        //????????
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(_jsonData)), WebSocketMessageType.Text, true, new CancellationToken());
    }


    #endregion


    #region ???????
    /// <summary>
    /// audioclip??byte[]
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
    /// ??????????
    /// </summary>
    /// <param name="status"></param>
    private void PrintErrorLog(int status)
    {
        if (status == 10005)
        {
            Debug.LogError("appid??????");
            return;
        }
        if (status == 10006)
        {
            Debug.LogError("?????????????");
            return;
        }
        if (status == 10007)
        {
            Debug.LogError("???????????§¹");
            return;
        }
        if (status == 10010)
        {
            Debug.LogError("???????????");
            return;
        }
        if (status == 10019)
        {
            Debug.LogError("session???");
            return;
        }
        if (status == 10043)
        {
            Debug.LogError("??????????");
            return;
        }
        if (status == 10101)
        {
            Debug.LogError("??????????");
            return;
        }
        if (status == 10313)
        {
            Debug.LogError("appid???????");
            return;
        }
        if (status == 10317)
        {
            Debug.LogError("?·Ú???");
            return;
        }
        if (status == 11200)
        {
            Debug.LogError("??????");
            return;
        }
        if (status == 11201)
        {
            Debug.LogError("?????????");
            return;
        }
        if (status == 10160)
        {
            Debug.LogError("?????????????");
            return;
        }
        if (status == 10161)
        {
            Debug.LogError("base64???????");
            return;
        }
        if (status == 10163)
        {
            Debug.LogError("??????????????????????????????????????????");
            return;
        }
        if (status == 10200)
        {
            Debug.LogError("?????????");
            return;
        }
        if (status == 10222)
        {
            Debug.LogError("??????");
            return;
        }
    }


    #endregion

    #region ???????

    /// <summary>
    /// ?????????
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
