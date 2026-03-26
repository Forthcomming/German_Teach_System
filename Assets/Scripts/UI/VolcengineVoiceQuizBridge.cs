using TMPro;
using UnityEngine;

namespace UI
{
    [DisallowMultipleComponent]
    public class VolcengineVoiceQuizBridge : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private VolcengineSpeechToTextNostream speechToText;
        [SerializeField] private QuizModule quizModule;

        [Header("UI（可选）")]
        [SerializeField] private TextMeshProUGUI partialTextLabel;
        [SerializeField] private TextMeshProUGUI finalTextLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("行为")]
        [SerializeField] private bool autoSubmitToQuiz = true;
        [SerializeField] private bool normalizeNumberAnswer = true;

        private void OnEnable()
        {
            if (speechToText == null)
            {
                return;
            }

            speechToText.PartialTextReceived += HandlePartialText;
            speechToText.FinalTextReceived += HandleFinalText;
            speechToText.ErrorOccurred += HandleError;
        }

        private void OnDisable()
        {
            if (speechToText == null)
            {
                return;
            }

            speechToText.PartialTextReceived -= HandlePartialText;
            speechToText.FinalTextReceived -= HandleFinalText;
            speechToText.ErrorOccurred -= HandleError;
        }

        public void StartVoiceAnswer()
        {
            if (speechToText == null)
            {
                Debug.LogError("[VolcengineVoiceQuizBridge] 未绑定 VolcengineSpeechToTextNostream。", this);
                SetStatus("未绑定语音识别组件");
                return;
            }

            SetStatus("录音中...");
            if (partialTextLabel != null)
            {
                partialTextLabel.text = string.Empty;
            }
            speechToText.StartRecordAndRecognize();
        }

        public void StopVoiceAnswer()
        {
            if (speechToText == null)
            {
                Debug.LogError("[VolcengineVoiceQuizBridge] 未绑定 VolcengineSpeechToTextNostream。", this);
                SetStatus("未绑定语音识别组件");
                return;
            }

            SetStatus("识别中...");
            speechToText.StopAndFlush();
        }

        private void HandlePartialText(string text)
        {
            if (partialTextLabel != null)
            {
                partialTextLabel.text = text;
            }

            SetStatus("识别中...");
        }

        private void HandleFinalText(string text)
        {
            if (finalTextLabel != null)
            {
                finalTextLabel.text = text;
            }
            SetStatus("识别完成");

            Debug.Log($"[VolcengineVoiceQuizBridge] 最终识别文本: {text}", this);
            if (!autoSubmitToQuiz || quizModule == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            string answer = normalizeNumberAnswer ? TryNormalizeNumberAnswer(text) : text;
            quizModule.OnUserAnswer(answer);
        }

        private void HandleError(string message)
        {
            Debug.LogError($"[VolcengineVoiceQuizBridge] 语音识别错误: {message}", this);
            SetStatus("识别失败");
        }

        private void SetStatus(string status)
        {
            if (statusLabel != null)
            {
                statusLabel.text = status;
            }
        }

        private static string TryNormalizeNumberAnswer(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    return text[i].ToString();
                }
            }

            return text.Trim();
        }
    }
}
