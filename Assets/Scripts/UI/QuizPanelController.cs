using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    public enum QuestionPresentationMode
    {
        Choice,
        Voice
    }

    /// <summary>
    /// 面板级测验 UI 控制器（已整合倒计时逻辑）：
    /// - 选择题：图/文混合选项，提交选项编号字符串；
    /// - 语音题：隐藏选项区，由外部语音识别完成后调用 SubmitVoiceRecognizedText，提交识别文本与 QuizModule.answers 比对（忽略大小写、Trim）；
    /// - 题目进度、每题倒计时、对/错反馈。
    /// </summary>
    [DisallowMultipleComponent]
    public class QuizPanelController : MonoBehaviour
    {
        [Header("逻辑模块引用")]
        [Tooltip("测验逻辑模块 QuizModule。")]
        [SerializeField] private QuizModule quizModule;

        [Header("题干 & 进度 UI")]
        [Tooltip("用于显示题干文本的 TextMeshProUGUI 组件。")]
        [SerializeField] private TextMeshProUGUI questionText;

        [Tooltip("用于显示题目进度的 TextMeshProUGUI，例如 \"题目: 1 / 10\"。")]
        [SerializeField] private TextMeshProUGUI progressText;

        [Serializable]
        public class OptionView
        {
            [Tooltip("该选项对应的按钮（必填）。按钮的 OnClick 需要调用 QuizPanelController.OnOptionClicked(index)。")]
            public Button button;

            [Tooltip("该选项对应的图片（可选）。若为空或本题该选项未配置图片，则会隐藏。")]
            public Image image;

            [Tooltip("该选项对应的文字（可选）。若为空或本题该选项未配置文字，则会隐藏。")]
            public TextMeshProUGUI label;
        }

        [Header("选项视图（最多 7 个）")]
        [Tooltip("场景中预先放置的选项槽位（建议固定 7 个）。顺序即为选项编号 0/1/2/3...")]
        [SerializeField] private OptionView[] optionViews;

        [Serializable]
        public class OptionData
        {
            [Tooltip("该选项的图片（可为空）。")]
            public Sprite sprite;

            [Tooltip("该选项的文字（可为空）。")]
            public string text;
        }

        [Serializable]
        public class QuestionOptions
        {
            [Tooltip("该题的选项列表，长度可变（4~7）。每个选项可仅图、仅文或图文混合。")]
            public OptionData[] options;
        }

        [Header("每题对应的选项数据（图/文混合）")]
        [Tooltip("长度需与 QuizModule.questions 一致。每题可配置 4~7 个选项。")]
        [SerializeField] private QuestionOptions[] questionOptions;

        [Header("题型（与 questions 一一对应）")]
        [Tooltip("选择题走选项 UI；语音题走语音面板，answers 中填期望识别文本（非纯数字）。")]
        [SerializeField] private QuestionPresentationMode[] questionModes;

        [Header("选择题 / 语音题 面板（可选）")]
        [Tooltip("选择题相关 UI 根节点（含选项槽位）。不填则仅通过隐藏选项表现语音题。")]
        [SerializeField] private GameObject choicePanelRoot;
        [Tooltip("语音题 UI 根节点（录音按钮、识别结果展示等）。")]
        [SerializeField] private GameObject voicePanelRoot;
        [Tooltip("可选：实时显示识别文本的 TMP，仅展示用。")]
        [SerializeField] private TextMeshProUGUI voiceTranscriptText;

        [Header("对/错反馈 UI")]
        [Tooltip("答对时显示的提示 UI 根对象（如对号、文字提示等）。")]
        [SerializeField] private GameObject correctPrompt;
        [Tooltip("答错时显示的提示 UI 根对象。")]
        [SerializeField] private GameObject wrongPrompt;
        [Tooltip("对/错提示显示时长（秒）。")]
        [SerializeField] private float feedbackDuration = 1f;

        [Header("每题倒计时设置")]
        [Tooltip("每道题的倒计时时长（秒），例如 10 / 15 秒。")]
        [SerializeField] private float perQuestionCountdownSeconds = 10f;
        [Tooltip("用于显示倒计时的 TextMeshProUGUI 文本组件。")]
        [SerializeField] private TextMeshProUGUI timeText;

        private bool awaitingAnswer;
        private Coroutine feedbackCoroutine;
        private Coroutine countdownRoutine;
        private int lastSelectedOptionIndex = -1; // -1 表示本题还没选择过
        private int currentOptionCount;

        private bool IsVoiceQuestion(int questionIndex)
        {
            if (questionModes == null || questionIndex < 0 || questionIndex >= questionModes.Length)
            {
                return false;
            }

            return questionModes[questionIndex] == QuestionPresentationMode.Voice;
        }

        private void Awake()
        {
            // 初始隐藏反馈 UI
            SetActiveIfNotNull(correctPrompt, false);
            SetActiveIfNotNull(wrongPrompt, false);

            // 初始清空倒计时显示
            UpdateTimeText(0f);
        }

        private void OnEnable()
        {
            if (quizModule != null)
            {
                quizModule.QuestionShown += OnQuestionShown;
            }
        }

        private void OnDisable()
        {
            if (quizModule != null)
            {
                quizModule.QuestionShown -= OnQuestionShown;
            }

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
                feedbackCoroutine = null;
            }

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }
        }

        /// <summary>
        /// 从 UI（例如“开始测验”按钮）调用，启动测验流程。
        /// </summary>
        public void BeginQuiz()
        {
            if (quizModule == null)
            {
                Debug.LogError("[QuizPanelController] quizModule 未绑定，无法开始测验。", this);
                return;
            }

            if (quizModule.questions != null &&
                questionModes != null &&
                questionModes.Length != quizModule.questions.Length)
            {
                Debug.LogWarning(
                    $"[QuizPanelController] questionModes 长度({questionModes.Length}) 与题目数({quizModule.questions.Length}) 不一致，未配置的题将视为选择题。",
                    this);
            }

            quizModule.StartQuiz(OnQuizFinished);
        }

        /// <summary>
        /// 选项按钮点击时调用。
        /// 在 Button 的 OnClick() 里传入对应索引（0/1/2/3）。
        /// </summary>
        /// <param name="optionIndex">本按钮对应的选项编号。</param>
        public void OnOptionClicked(int optionIndex)
        {
            if (quizModule == null)
            {
                Debug.LogWarning("[QuizPanelController] quizModule 为 null，无法处理选项点击。", this);
                return;
            }

            if (IsVoiceQuestion(quizModule.CurrentIndex))
            {
                return;
            }

            if (!awaitingAnswer)
            {
                // 当前不在等待作答阶段（可能在播放反馈或超时/测验已结束），直接忽略点击。
                return;
            }

            if (optionIndex < 0 || optionIndex >= currentOptionCount)
            {
                // 点击索引越界（例如某题只有 4 个选项但点了第 5 个槽位），忽略。
                return;
            }

            lastSelectedOptionIndex = optionIndex;

            // 玩家主动作答 → 停止倒计时
            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }

            bool correct = IsCurrentOptionCorrect(optionIndex);

            awaitingAnswer = false;

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }
            feedbackCoroutine = StartCoroutine(ShowFeedbackAndSubmitAnswerCoroutine(correct, optionIndex >= 0 ? optionIndex.ToString() : "-1"));
        }

        /// <summary>
        /// 由语音识别组件在得到最终文本后调用；与当前题的 <see cref="QuizModule.answers"/> 按 QuizModule.CompareAnswer 规则比对。
        /// </summary>
        public void SubmitVoiceRecognizedText(string recognizedText)
        {
            if (quizModule == null)
            {
                Debug.LogWarning("[QuizPanelController] quizModule 为 null。", this);
                return;
            }

            if (!awaitingAnswer)
            {
                return;
            }

            if (!IsVoiceQuestion(quizModule.CurrentIndex))
            {
                return;
            }

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }

            int idx = quizModule.CurrentIndex;
            string expected = quizModule.answers != null && idx >= 0 && idx < quizModule.answers.Length
                ? quizModule.answers[idx]
                : string.Empty;
            bool correct = QuizModule.CompareAnswer(recognizedText, expected);

            awaitingAnswer = false;

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }

            string toSubmit = recognizedText ?? string.Empty;
            feedbackCoroutine = StartCoroutine(ShowFeedbackAndSubmitAnswerCoroutine(correct, toSubmit));
        }

        private void OnQuestionShown(int questionIndex)
        {
            awaitingAnswer = true;
            lastSelectedOptionIndex = -1;

            UpdateQuestionText();
            UpdateProgressText(questionIndex);

            bool voice = IsVoiceQuestion(questionIndex);
            SetChoiceVoicePanels(!voice);

            if (voice)
            {
                HideAllOptionViews();
                currentOptionCount = 0;
                ClearVoiceTranscript();
            }
            else
            {
                UpdateOptions(questionIndex);
                EnableOptionButtons(true);
            }

            // 切题时确保反馈 UI 关闭
            SetActiveIfNotNull(correctPrompt, false);
            SetActiveIfNotNull(wrongPrompt, false);

            // 启动本题倒计时
            StartPerQuestionCountdown();
        }

        private void SetChoiceVoicePanels(bool showChoice)
        {
            if (choicePanelRoot != null)
            {
                choicePanelRoot.SetActive(showChoice);
            }

            if (voicePanelRoot != null)
            {
                voicePanelRoot.SetActive(!showChoice);
            }
        }

        private void ClearVoiceTranscript()
        {
            if (voiceTranscriptText != null)
            {
                voiceTranscriptText.text = string.Empty;
            }
        }

        /// <summary>
        /// 可选：识别过程中更新 UI 文案。
        /// </summary>
        public void SetVoiceTranscriptPreview(string text)
        {
            if (voiceTranscriptText != null)
            {
                voiceTranscriptText.text = text ?? string.Empty;
            }
        }

        private void UpdateQuestionText()
        {
            if (questionText == null)
            {
                return;
            }

            string question = quizModule != null ? quizModule.CurrentQuestion : null;
            questionText.text = string.IsNullOrEmpty(question) ? string.Empty : question;
        }

        /// <summary>
        /// 更新左上角的题目进度显示，例如“题目: 1 / 10”。
        /// </summary>
        private void UpdateProgressText(int questionIndex)
        {
            if (progressText == null || quizModule == null || quizModule.questions == null)
            {
                return;
            }

            int current = questionIndex + 1;                 // 从 1 开始计数
            int total = quizModule.questions.Length;         // 总题数（例如 10）

            progressText.text = $"题目: {current} / {total}";
        }

        private void UpdateOptions(int questionIndex)
        {
            currentOptionCount = 0;

            if (optionViews == null || optionViews.Length == 0)
            {
                return;
            }

            if (questionOptions == null || questionIndex < 0 || questionIndex >= questionOptions.Length)
            {
                Debug.LogWarning($"[QuizPanelController] questionOptions 未正确配置或索引越界（index={questionIndex}）。", this);
                HideAllOptionViews();
                return;
            }

            QuestionOptions dataForQuestion = questionOptions[questionIndex];
            if (dataForQuestion == null || dataForQuestion.options == null)
            {
                Debug.LogWarning($"[QuizPanelController] 第 {questionIndex} 题的 options 未配置。", this);
                HideAllOptionViews();
                return;
            }

            int configuredCount = Mathf.Min(optionViews.Length, dataForQuestion.options.Length);

            // 先按配置渲染 0..configuredCount-1；若中间出现“既无图也无字”的断档，则视为选项结束，避免索引错乱。
            for (int i = 0; i < configuredCount; i++)
            {
                OptionView view = optionViews[i];
                if (view == null || view.button == null)
                {
                    configuredCount = i;
                    break;
                }

                OptionData option = dataForQuestion.options[i];
                Sprite sprite = option != null ? option.sprite : null;
                string text = option != null ? option.text : null;

                bool hasSprite = sprite != null;
                bool hasText = !string.IsNullOrWhiteSpace(text);

                if (!hasSprite && !hasText)
                {
                    configuredCount = i;
                    break;
                }

                view.button.gameObject.SetActive(true);

                if (view.image != null)
                {
                    view.image.sprite = sprite;
                    view.image.enabled = hasSprite;
                }

                if (view.label != null)
                {
                    view.label.text = hasText ? text : string.Empty;
                    view.label.enabled = hasText;
                }
            }

            // 隐藏剩余槽位
            for (int i = configuredCount; i < optionViews.Length; i++)
            {
                OptionView view = optionViews[i];
                if (view?.button != null)
                {
                    view.button.gameObject.SetActive(false);
                }
            }

            currentOptionCount = configuredCount;
        }

        #region 倒计时相关

        private void StartPerQuestionCountdown()
        {
            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
            }
            countdownRoutine = StartCoroutine(PerQuestionCountdownCoroutine());
        }

        private IEnumerator PerQuestionCountdownCoroutine()
        {
            float timeLeft = Mathf.Max(0f, perQuestionCountdownSeconds);

            while (timeLeft > 0f && awaitingAnswer)
            {
                UpdateTimeText(timeLeft);
                yield return new WaitForSeconds(1f);
                timeLeft -= 1f;
            }

            // 显示 0
            UpdateTimeText(0f);

            // 仍然在等答案 → 说明是超时
            if (awaitingAnswer)
            {
                HandleTimeoutSubmit();
            }

            countdownRoutine = null;
        }

        private void UpdateTimeText(float timeLeft)
        {
            if (timeText == null)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(timeLeft);
            if (seconds < 0)
            {
                seconds = 0;
            }

            string display = seconds + "s"; // 如需 mm:ss 可在此调整
            timeText.text = display;
        }

        /// <summary>
        /// 时间到时的处理：自动提交当前已选选项；若未选择则按错误处理。
        /// </summary>
        private void HandleTimeoutSubmit()
        {
            awaitingAnswer = false;

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }

            int optionIndexToUse = lastSelectedOptionIndex;

            // 玩家完全没选 → 记成一个不可能正确的编号（-1），一定判错
            bool correct = false;
            if (optionIndexToUse >= 0 && optionIndexToUse < currentOptionCount)
            {
                correct = IsCurrentOptionCorrect(optionIndexToUse);
            }
            else
            {
                optionIndexToUse = -1;
            }

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }
            feedbackCoroutine = StartCoroutine(ShowFeedbackAndSubmitAnswerCoroutine(correct, optionIndexToUse >= 0 ? optionIndexToUse.ToString() : "-1"));
        }

        #endregion

        private bool IsCurrentOptionCorrect(int optionIndex)
        {
            if (quizModule == null)
            {
                return false;
            }

            int currentIndex = quizModule.CurrentIndex;

            if (quizModule.answers == null ||
                currentIndex < 0 ||
                currentIndex >= quizModule.answers.Length)
            {
                return false;
            }

            string expected = quizModule.answers[currentIndex];
            return QuizModule.CompareAnswer(optionIndex.ToString(), expected);
        }

        private IEnumerator ShowFeedbackAndSubmitAnswerCoroutine(bool correct, string answerForQuizModule)
        {
            // 播放反馈：显示对应 UI，并在期间禁用按钮。
            EnableOptionButtons(false);

            if (correct)
            {
                SetActiveIfNotNull(wrongPrompt, false);
                SetActiveIfNotNull(correctPrompt, true);
            }
            else
            {
                SetActiveIfNotNull(correctPrompt, false);
                SetActiveIfNotNull(wrongPrompt, true);
            }

            float duration = Mathf.Max(0f, feedbackDuration);
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            SetActiveIfNotNull(correctPrompt, false);
            SetActiveIfNotNull(wrongPrompt, false);

            quizModule.OnUserAnswer(answerForQuizModule ?? string.Empty);

            feedbackCoroutine = null;
        }

        private void OnQuizFinished(int finalScore)
        {
            awaitingAnswer = false;

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }

            EnableOptionButtons(false);
            HideAllOptionViews();
            SetChoiceVoicePanels(true);
            if (voicePanelRoot != null)
            {
                voicePanelRoot.SetActive(false);
            }

            SetActiveIfNotNull(correctPrompt, false);
            SetActiveIfNotNull(wrongPrompt, false);
            UpdateTimeText(0f);

            // 结束时进度可以设为“题目: N / N”
            UpdateProgressAtEnd();

            Debug.Log("[QuizPanelController] 测验结束，最终得分：" + finalScore, this);
            // 如有需要，可在这里切换到结果面板、显示分数等。
        }

        private void UpdateProgressAtEnd()
        {
            if (progressText == null || quizModule == null || quizModule.questions == null)
            {
                return;
            }

            int total = quizModule.questions.Length;
            progressText.text = $"题目: {total} / {total}";
        }

        private void EnableOptionButtons(bool enable)
        {
            if (optionViews == null)
            {
                return;
            }

            for (int i = 0; i < optionViews.Length; i++)
            {
                OptionView view = optionViews[i];
                if (view == null || view.button == null)
                {
                    continue;
                }

                bool activeAndValid = view.button.gameObject.activeInHierarchy && i < currentOptionCount;
                view.button.interactable = enable && activeAndValid;
            }
        }

        private void HideAllOptionViews()
        {
            currentOptionCount = 0;
            if (optionViews == null)
            {
                return;
            }

            foreach (var view in optionViews)
            {
                if (view?.button != null)
                {
                    view.button.gameObject.SetActive(false);
                }
            }
        }

        private static void SetActiveIfNotNull(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}