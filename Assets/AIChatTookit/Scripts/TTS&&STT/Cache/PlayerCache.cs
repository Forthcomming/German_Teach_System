using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 玩家缓存类
public class PlayerCache
{
    public string myCode = "";

    // 用于追踪的请求ID
    public string request_id = string.Empty;

    // 返回时间戳
    public string date = string.Empty;

    // 增量数据
    public string answer = string.Empty;

    // 对话ID
    public string conversation_id = string.Empty;

    // 消息ID
    public string message_id = string.Empty;

    // 错误码
    public string code = string.Empty;

    // 错误信息
    public string message = string.Empty;

    // 用于存储事件相关信息的嵌套对象数组
    public List<EventData> content;

    public class EventData
    {
        public string result_type = string.Empty; // 结果类型
        public int event_code = 0; // 事件代码
        public string event_message = string.Empty; // 事件消息
        public string event_type = string.Empty; // 事件类型
        public string event_id = string.Empty; // 事件ID
        public string event_status = string.Empty; // 事件状态
        public string content_type = string.Empty; // 内容类型
        public string visible_scope = string.Empty; // 可见范围

        public Outputs outputs; // 嵌套 Outputs 类
        public Usage usage; // 嵌套 Usage 类

    
        public class Outputs
        {
            public Text text;

            // 嵌套类 Text
            [Serializable]
            public class Text
            {
                public object arguments; // 参数对象
                public string component_code = string.Empty; // 组件代码
                public string component_name = string.Empty; // 组件名称
            }
        }

        public class Usage
        {
            public int prompt_tokens = 0; // 提示词令牌数
            public int completion_tokens = 0; // 完成词令牌数
            public int total_tokens = 0; // 总令牌数
            public string name = string.Empty; // 名称
            public string type = string.Empty; // 类型
        }
    }

    // 初始化玩家信息
    public void SetData(Dictionary<string, object> info)
    {
        request_id = JSONUtil.readString(info, "request_id");
        date = JSONUtil.readString(info, "date");
        answer = JSONUtil.readString(info, "answer");
        conversation_id = JSONUtil.readString(info, "conversation_id");
        message_id = JSONUtil.readString(info, "message_id");

        content = new List<EventData>();
        List<object> contentList = new List<object>();

        // 初始化 content 列表
        contentList = JSONUtil.readList(info, "content");

        if (contentList != null && contentList.Count > 0)
        {
            foreach (Dictionary<string, object> eventInfo in contentList)
            {
                EventData eventData = new EventData();
                eventData.result_type = JSONUtil.readString(eventInfo, "result_type");
                eventData.event_code = JSONUtil.readInt(eventInfo, "event_code");
                eventData.event_message = JSONUtil.readString(eventInfo, "event_message");
                eventData.event_type = JSONUtil.readString(eventInfo, "event_type");
                eventData.event_id = JSONUtil.readString(eventInfo, "event_id");
                eventData.event_status = JSONUtil.readString(eventInfo, "event_status");
                eventData.content_type = JSONUtil.readString(eventInfo, "content_type");
                eventData.visible_scope = JSONUtil.readString(eventInfo, "visible_scope");

        

                //// 初始化 usage
                //Dictionary<string, object> usageData = JSONUtil.readDictionary(eventInfo, "usage");
                //if (usageData != null)
                //{
                //    eventData.usage = new EventData.Usage
                //    {
                //        prompt_tokens = JSONUtil.readInt(usageData, "prompt_tokens"),
                //        completion_tokens = JSONUtil.readInt(usageData, "completion_tokens"),
                //        total_tokens = JSONUtil.readInt(usageData, "total_tokens"),
                //        name = JSONUtil.readString(usageData, "name"),
                //        type = JSONUtil.readString(usageData, "type")
                //    };
                //}

                content.Add(eventData);
            }
        }
    }
}

