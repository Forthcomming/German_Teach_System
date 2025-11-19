using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AppBuilderBaidu : LLM
{
    #region Params

    [SerializeField] private string app_id = string.Empty; // App ID
    [SerializeField] private string api_key = string.Empty; // API Key
    private string m_ConversationUrl = string.Empty; // 新建会话API地址
    [SerializeField] private string m_ConversationID = string.Empty; // 对话ID
    public string singleRoundFullResponse = string.Empty;
    // 新增成员变量，用于存储ChatSample脚本实例
    private ChatSample chatSample;
    #endregion

    #region Public Method




    /// <summary>
    /// 发送消息
    /// </summary>
    public override void PostMsg(string _msg, Action<string> _callback)
    {
        // 缓存发送的信息列表
        m_DataList.Add(new SendData("user", _msg));
        StartCoroutine(Request(_msg, _callback));
    }

    /// <summary>
    /// 发送数据（流式请求）
    /// </summary>
    public override IEnumerator Request(string _postWord, Action<string> _callback)
    {
        stopwatch.Restart();

        string jsonPayload = JsonConvert.SerializeObject(new RequestData
        {
            app_id = app_id,
            query = _postWord,
            conversation_id = m_ConversationID,
            stream = true // 启用流式模式
        });

        //Debug.Log($"发送的请求负载: {jsonPayload}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Appbuilder-Authorization", $"Bearer {api_key}");

            var operation = request.SendWebRequest();

            string previousData = string.Empty;

            while (!operation.isDone)
            {
                string responseText = request.downloadHandler.text;
                //Debug.Log($"服务器返回内容: {responseText}");

                if (!string.IsNullOrEmpty(responseText) && responseText != previousData)
                {
                    string newData = responseText.Substring(previousData.Length);
                    previousData = responseText;

                    //Debug.Log($"新增数据: {newData}");

                    string[] lines = newData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        //Debug.Log($"当前处理的行数据: {line}");

                        // 去掉 data: 前缀
                        string jsonLine = line.TrimStart().StartsWith("data:") ? line.Substring(5).Trim() : line;

                        if (!string.IsNullOrEmpty(jsonLine) && jsonLine.StartsWith("{"))
                        {
                            try
                            {
                                ResponseData response = JsonConvert.DeserializeObject<ResponseData>(jsonLine);
                                //Debug.Log($"解析成功的JSON: {jsonLine}");
                                if (!string.IsNullOrEmpty(response.answer))
                                {
                                    // 将增量回复追加到完整回复变量中
                                    singleRoundFullResponse += response.answer;
                                    //Debug.Log($"=== 流式增量数据 ===: {response.answer}");
                                    // 继续原有的回调逻辑，这里未做改变
                                    _callback(response.answer);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"JSON解析错误: {ex.Message}");
                                Debug.LogError($"无法解析的JSON行: {jsonLine}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"无效的行数据或非JSON: {line}");
                        }
                    }
                }

                yield return null;
            }


            if (request.responseCode == 200)
            {
                //Debug.Log("Streaming complete.");
                // 输出单轮完整回复内容
                Debug.Log("单轮完整回复：" + singleRoundFullResponse);
                // 检查是否成功获取到ChatSample脚本实例
                if (chatSample != null)
                {
                    // 调用ChatSample脚本的方法并传递参数
                    StartCoroutine(chatSample.ReceiveFullResponse(singleRoundFullResponse));

                }
                else
                {
                    Debug.LogError("未能获取到ChatSample脚本实例！");
                }
                // 每轮结束后清空单轮完整回复内容
                singleRoundFullResponse = string.Empty;
            }
            else
            {
                Debug.LogError($"请求失败: {request.error}");
            }
        }

        stopwatch.Stop();
        Debug.Log($"流式请求耗时: {stopwatch.Elapsed.TotalSeconds} 秒");
    }
    #endregion

    #region Private Method

    void Awake()
    {
        // 获取ChatSample脚本所在的游戏对象实例
        chatSample = GameObject.FindObjectOfType<ChatSample>();
        OnInitial();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    private void OnInitial()
    {
        m_ConversationUrl = "https://qianfan.baidubce.com/v2/app/conversation"; // 新建会话地址
        url = "https://qianfan.baidubce.com/v2/app/conversation/runs"; // 聊天API地址
        StartCoroutine(OnStartConversation());
    }

    /// <summary>
    /// 新建会话
    /// </summary>
    private IEnumerator OnStartConversation()
    {
        string jsonPayload = JsonUtility.ToJson(new CreateConversationData { app_id = app_id });
        using (UnityWebRequest request = new UnityWebRequest(m_ConversationUrl, "POST"))
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Appbuilder-Authorization", $"Bearer {api_key}");

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                string _msg = request.downloadHandler.text;
                ConversationCreateReponse response = JsonConvert.DeserializeObject<ConversationCreateReponse>(_msg);

                if (response.code == string.Empty)
                {
                    m_ConversationID = response.conversation_id; // 获取会话ID
                }
                else
                {
                    Debug.LogError(response.message);
                }
            }
            else
            {
                Debug.LogError(request.error);
            }
        }
    }

    #endregion

    #region Data Define

    [Serializable]
    public class CreateConversationData
    {
        public string app_id = string.Empty;
    }

    [Serializable]
    public class ConversationCreateReponse
    {
        public string request_id = string.Empty;
        public string conversation_id = string.Empty;
        public string code = string.Empty;
        public string message = string.Empty;
    }

    [Serializable]
    public class RequestData
    {
        public string app_id = string.Empty; // App ID
        public string query = string.Empty; // 提问内容
        public bool stream = true; // 启用流式模式
        public string conversation_id = string.Empty; // 对话ID
        public List<string> file_ids = new List<string>(); // 文件ID列表
    }

    [Serializable]
    public class ResponseData
    {
        public string code = string.Empty; // 错误码
        public string message = string.Empty; // 错误信息
        public string request_id = string.Empty; // 用于追踪的请求ID
        public string date = string.Empty; // 返回时间戳
        public string answer = string.Empty; // 增量数据
        public string conversation_id = string.Empty; // 对话ID
        public string message_id = string.Empty; // 消息ID
    }

    #endregion
}