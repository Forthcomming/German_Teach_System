using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 统一对话控制器（UI代理层）：
    /// 仅负责按钮与文本同步，核心流程全部转发到 ChatSample。
    /// </summary>
    [DisallowMultipleComponent]
    public class VoiceLlmStreamingController : MonoBehaviour
    {
        [SerializeField] private ChatSample chatSample;

        [SerializeField] private Button recordButton;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button stopAudioButton;
        [SerializeField] private InputField inputField;
        [SerializeField] private TMP_InputField tmpInputField;
        [SerializeField] private TextMeshProUGUI recognizedText;
        [SerializeField] private TextMeshProUGUI streamText;
        [SerializeField] private TextMeshProUGUI statusText;

        private TextMeshProUGUI recordButtonText;

        private const string IdleLabel = "点击开始录音";
        private const string RecordingLabel = "点击结束录音";
        private const string RecognizingLabel = "识别中...";
        private const string ThinkingLabel = "思考中...";
        private const string StreamingLabel = "回答中...";
        private const string EmptyRecognizedLabel = "未识别到有效文本";
        private const string ThinkingPrefix = "正在思考中...";
        private const string DefaultGreetingText = "Guten Tag, was möchten Sie bitte?\n你好，请问要点些什么?";
        private string cachedResponseText = string.Empty;
        private string cachedTipText = string.Empty;

        private void Awake()
        {
            recordButtonText = recordButton != null ? recordButton.GetComponentInChildren<TextMeshProUGUI>() : null;
            SetTextIfNotNull(recognizedText, string.Empty);
            SetTextIfNotNull(streamText, DefaultGreetingText);
            SetStatus(IdleLabel);
            UpdateRecordButtonState(interactable: true, label: IdleLabel);
        }

        private void OnEnable()
        {
            if (recordButton != null)
            {
                recordButton.onClick.AddListener(OnRecordButtonClicked);
            }

            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendButtonClicked);
            }

            if (stopAudioButton != null)
            {
                stopAudioButton.onClick.AddListener(OnStopAudioClicked);
            }
        }

        private void OnDisable()
        {
            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(OnRecordButtonClicked);
            }

            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(OnSendButtonClicked);
            }

            if (stopAudioButton != null)
            {
                stopAudioButton.onClick.RemoveListener(OnStopAudioClicked);
            }
        }

        private void Update()
        {
            if (chatSample == null)
            {
                return;
            }

            // 同步录音按钮状态
            bool isRecording = chatSample.IsRecording;
            UpdateRecordButtonState(true, isRecording ? RecordingLabel : IdleLabel);

            // 同步识别文本（来自 ChatSample.m_RecordTips）
            string latestTip = chatSample.CurrentRecordTipText;
            if (!string.Equals(cachedTipText, latestTip, StringComparison.Ordinal))
            {
                cachedTipText = latestTip;
                SetTextIfNotNull(recognizedText, latestTip);
            }

            // 同步流式文本（来自 ChatSample 的完整增量缓存）
            string latestResponse = chatSample.CurrentResponseText;
            if (!string.Equals(cachedResponseText, latestResponse, StringComparison.Ordinal))
            {
                cachedResponseText = latestResponse;
                string displayResponse = RemoveThinkingPrefix(latestResponse);
                SetTextIfNotNull(streamText, string.IsNullOrEmpty(displayResponse) ? DefaultGreetingText : displayResponse);
            }

            // 同步状态文案
            if (chatSample.isLoadingAnswer == 1)
            {
                SetStatus(ThinkingLabel);
            }
            else if (chatSample.isLoadingAnswer == 2)
            {
                SetStatus(StreamingLabel);
            }
            else if (isRecording)
            {
                SetStatus(RecordingLabel);
            }
            else
            {
                SetStatus(IdleLabel);
            }
        }

        public void OnRecordButtonClicked()
        {
            if (chatSample == null)
            {
                Debug.LogError("[VoiceLlmStreamingController] 未绑定 ChatSample。", this);
                return;
            }
            chatSample.ToggleRecordExternal();
            UpdateRecordButtonState(true, chatSample.IsRecording ? RecordingLabel : RecognizingLabel);
        }

        public void OnSendButtonClicked()
        {
            if (chatSample == null)
            {
                Debug.LogError("[VoiceLlmStreamingController] 未绑定 ChatSample。", this);
                return;
            }

            string text = string.Empty;

            if (tmpInputField != null)
            {
                text = tmpInputField.text;
                tmpInputField.text = string.Empty;
            }
            else if (inputField != null)
            {
                text = inputField.text;
                inputField.text = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus(EmptyRecognizedLabel);
                return;
            }

            SetTextIfNotNull(streamText, string.Empty);
            chatSample.SendData(text);
            SetStatus(ThinkingLabel);
        }

        public void OnStopAudioClicked()
        {
            if (chatSample != null)
            {
                chatSample.StopPlayAudio();
            }
        }

        private void UpdateRecordButtonState(bool interactable, string label)
        {
            if (recordButton != null)
            {
                recordButton.interactable = interactable;
            }

            if (recordButtonText != null)
            {
                recordButtonText.text = label ?? string.Empty;
            }
        }

        private void SetStatus(string status)
        {
            SetTextIfNotNull(statusText, status);
        }

        private static void SetTextIfNotNull(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static string RemoveThinkingPrefix(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            if (!raw.StartsWith(ThinkingPrefix, StringComparison.Ordinal))
            {
                return raw;
            }

            string stripped = raw.Substring(ThinkingPrefix.Length);
            return stripped.TrimStart('\r', '\n', ' ');
        }

    }
}
