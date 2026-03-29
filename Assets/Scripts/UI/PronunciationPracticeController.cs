using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 独立发音练习控制器：
    /// 点击按钮开始/结束录音 -> STT识别 -> 与目标文本计算相似度 -> 达标切题。
    /// </summary>
    [DisallowMultipleComponent]
    public class PronunciationPracticeController : MonoBehaviour
    {
        [SerializeField] private VoiceInputs voiceInputs;
        [SerializeField] private STT speechToText;
        [SerializeField] private Button recordButton;

        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private TextMeshProUGUI recognizedText;
        [SerializeField] private TextMeshProUGUI similarityText;

        [Range(0f, 1f)]
        [SerializeField] private float passThreshold = 0.8f;

        [SerializeField] private GameObject currentQuestionRoot;
        [SerializeField] private GameObject nextQuestionRoot;
        [SerializeField] private GameObject correctPrompt;
        [SerializeField] private GameObject wrongPrompt;

        private const string IdleLabel = "点击开始录音";
        private const string RecordingLabel = "点击结束录音";
        private const string RecognizingLabel = "识别中...";
        private const string SimilarityFormat = "相似度: {0:P0}";
        private const float FeedbackDurationSeconds = 1f;

        private bool isRecording;
        private bool isRecognizing;
        private Coroutine switchQuestionCoroutine;

        private void Awake()
        {
            SetPrompt(correctPrompt, false);
            SetPrompt(wrongPrompt, false);
            SetRecordButtonText(IdleLabel);
            SetTextIfNotNull(recognizedText, string.Empty);
            SetTextIfNotNull(similarityText, string.Empty);
        }

        private void OnEnable()
        {
            if (recordButton != null)
            {
                recordButton.onClick.AddListener(OnRecordButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(OnRecordButtonClicked);
            }

            if (switchQuestionCoroutine != null)
            {
                StopCoroutine(switchQuestionCoroutine);
                switchQuestionCoroutine = null;
            }
        }

        private void Update()
        {
            if (!isRecording || voiceInputs == null)
            {
                return;
            }

            if (voiceInputs.isAutoStopRecording)
            {
                voiceInputs.isAutoStopRecording = false;
                StopRecordAndRecognize();
            }
        }

        public void OnRecordButtonClicked()
        {
            if (isRecognizing)
            {
                return;
            }

            if (!isRecording)
            {
                StartRecord();
            }
            else
            {
                StopRecordAndRecognize();
            }
        }

        public void StartRecord()
        {
            if (voiceInputs == null)
            {
                Debug.LogWarning("[PronunciationPracticeController] 未绑定 VoiceInputs。", this);
                return;
            }

            if (speechToText == null)
            {
                Debug.LogWarning("[PronunciationPracticeController] 未绑定 STT。", this);
                return;
            }

            SetTextIfNotNull(recognizedText, string.Empty);
            SetTextIfNotNull(similarityText, string.Empty);
            SetPrompt(correctPrompt, false);
            SetPrompt(wrongPrompt, false);

            voiceInputs.StartRecordAudio();
            isRecording = true;
            SetRecordButtonText(RecordingLabel);
        }

        public void StopRecordAndRecognize()
        {
            if (!isRecording || isRecognizing)
            {
                return;
            }

            if (voiceInputs == null || speechToText == null)
            {
                isRecording = false;
                SetRecordButtonText(IdleLabel);
                return;
            }

            isRecording = false;
            isRecognizing = true;
            SetRecordButtonInteractable(false);
            SetRecordButtonText(RecognizingLabel);
            voiceInputs.StopRecordAudio(OnRecordClipReady);
        }

        private void OnRecordClipReady(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[PronunciationPracticeController] 录音为空，无法识别。", this);
                FinishRecognize();
                return;
            }

            speechToText.SpeechToText(clip, OnRecognizedTextReceived);
        }

        private void OnRecognizedTextReceived(string recognized)
        {
            string recognizedValue = recognized ?? string.Empty;
            string targetValue = targetText != null ? targetText.text : string.Empty;

            SetTextIfNotNull(recognizedText, recognizedValue);

            float similarity = ComputeSimilarity(recognizedValue, targetValue);
            SetTextIfNotNull(similarityText, string.Format(SimilarityFormat, similarity));

            bool passed = similarity >= passThreshold;
            if (passed)
            {
                HandlePass();
            }
            else
            {
                HandleFail();
            }

            FinishRecognize();
        }

        private void HandlePass()
        {
            SetPrompt(wrongPrompt, false);
            SetPrompt(correctPrompt, true);
            if (switchQuestionCoroutine != null)
            {
                StopCoroutine(switchQuestionCoroutine);
            }
            switchQuestionCoroutine = StartCoroutine(SwitchQuestionAfterDelay());
        }

        private void HandleFail()
        {
            SetPrompt(correctPrompt, false);
            SetPrompt(wrongPrompt, true);
            StartCoroutine(HideWrongPromptAfterDelay());
            // 失败时不切题，保留当前题继续练习。
        }

        private IEnumerator SwitchQuestionAfterDelay()
        {
            yield return new WaitForSeconds(FeedbackDurationSeconds);
            SetPrompt(correctPrompt, false);

            if (currentQuestionRoot != null)
            {
                currentQuestionRoot.SetActive(false);
            }

            if (nextQuestionRoot != null)
            {
                nextQuestionRoot.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[PronunciationPracticeController] 未配置 nextQuestionRoot，已关闭当前题。", this);
            }

            switchQuestionCoroutine = null;
        }

        private IEnumerator HideWrongPromptAfterDelay()
        {
            yield return new WaitForSeconds(FeedbackDurationSeconds);
            SetPrompt(wrongPrompt, false);
        }

        private void FinishRecognize()
        {
            isRecognizing = false;
            SetRecordButtonInteractable(true);
            SetRecordButtonText(IdleLabel);
        }

        private static void SetTextIfNotNull(TextMeshProUGUI textComp, string value)
        {
            if (textComp != null)
            {
                textComp.text = value ?? string.Empty;
            }
        }

        private void SetRecordButtonText(string text)
        {
            if (recordButton == null)
            {
                return;
            }

            TextMeshProUGUI tmp = recordButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text ?? string.Empty;
            }
        }

        private void SetRecordButtonInteractable(bool value)
        {
            if (recordButton != null)
            {
                recordButton.interactable = value;
            }
        }

        private static void SetPrompt(GameObject prompt, bool active)
        {
            if (prompt != null && prompt.activeSelf != active)
            {
                prompt.SetActive(active);
            }
        }

        private static float ComputeSimilarity(string inputA, string inputB)
        {
            string a = NormalizeText(inputA);
            string b = NormalizeText(inputB);

            if (a.Length == 0 && b.Length == 0)
            {
                return 1f;
            }

            int maxLen = Mathf.Max(a.Length, b.Length);
            if (maxLen == 0)
            {
                return 0f;
            }

            int distance = LevenshteinDistance(a, b);
            float similarity = 1f - (float)distance / maxLen;
            return Mathf.Clamp01(similarity);
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string lower = text.Trim().ToLowerInvariant();
            StringBuilder builder = new StringBuilder(lower.Length);
            bool lastWasSpace = false;

            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                bool keep = char.IsLetterOrDigit(c) || IsCjk(c);

                if (keep)
                {
                    builder.Append(c);
                    lastWasSpace = false;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        private static bool IsCjk(char c)
        {
            return c >= 0x4E00 && c <= 0x9FFF;
        }

        private static int LevenshteinDistance(string a, string b)
        {
            int n = a.Length;
            int m = b.Length;

            if (n == 0) return m;
            if (m == 0) return n;

            int[] prev = new int[m + 1];
            int[] curr = new int[m + 1];

            for (int j = 0; j <= m; j++)
            {
                prev[j] = j;
            }

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                char aChar = a[i - 1];
                for (int j = 1; j <= m; j++)
                {
                    int cost = aChar == b[j - 1] ? 0 : 1;
                    int deletion = prev[j] + 1;
                    int insertion = curr[j - 1] + 1;
                    int substitution = prev[j - 1] + cost;
                    curr[j] = Mathf.Min(Mathf.Min(deletion, insertion), substitution);
                }

                int[] tmp = prev;
                prev = curr;
                curr = tmp;
            }

            return prev[m];
        }
    }
}
