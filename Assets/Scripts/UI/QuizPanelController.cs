using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// 面板级测验 UI 控制器（已整合倒计时逻辑）：
    /// - 显示题干文本；
    /// - 显示题目进度（题目: 1 / N）；
    /// - 为选项图片 Image 赋值；
    /// - 每题独立倒计时；
    /// - 处理玩家点击选项，调用 QuizModule.OnUserAnswer；
    /// - 控制对/错提示 UI 的显示与隐藏。
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

        [Header("选项图片 UI")]
        [Tooltip("用于显示选项图片的 Image 数组，例如长度为 4 对应四个选项。顺序即为选项编号 0/1/2/3。")]
        [SerializeField] private Image[] optionImages;

        [Serializable]
        public class QuestionSprites
        {
            [Tooltip("该题目对应的选项图片数组，顺序需与“选项编号”一致（0/1/2/3）。")]
            public Sprite[] optionSprites;
        }

        [Header("每题对应的选项图片")]
        [Tooltip("长度需与 QuizModule.questions 一致，每个元素包含该题的所有选项图片。")]
        [SerializeField] private QuestionSprites[] questionSprites;

        [Header("对/错反馈 UI")]
        [Tooltip("答对时显示的提示 UI 根对象（如对号、文字提示等）。")]
        [SerializeField] private GameObject correctPrompt;
        [Tooltip("答错时显示的提示 UI 根对象。")]
        [SerializeField] private GameObject wrongPrompt;
        [Tooltip("对/错提示显示时长（秒）。")]
        [SerializeField] private float feedbackDuration = 1f;

        [Header("选项按钮交互")]
        [Tooltip("在反馈期间需要禁用/启用交互的按钮集合（通常是 4 个选项 Button）。")]
        [SerializeField] private Button[] optionButtons;

        [Header("每题倒计时设置")]
        [Tooltip("每道题的倒计时时长（秒），例如 10 / 15 秒。")]
        [SerializeField] private float perQuestionCountdownSeconds = 10f;
        [Tooltip("用于显示倒计时的 TextMeshProUGUI 文本组件。")]
        [SerializeField] private TextMeshProUGUI timeText;

        private bool awaitingAnswer;
        private Coroutine feedbackCoroutine;
        private Coroutine countdownRoutine;
        private int lastSelectedOptionIndex = -1; // -1 表示本题还没选择过

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

            if (!awaitingAnswer)
            {
                // 当前不在等待作答阶段（可能在播放反馈或超时/测验已结束），直接忽略点击。
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
            feedbackCoroutine = StartCoroutine(ShowFeedbackAndSubmitAnswer(correct, optionIndex));
        }

        private void OnQuestionShown(int questionIndex)
        {
            awaitingAnswer = true;
            lastSelectedOptionIndex = -1;

            UpdateQuestionText();
            UpdateOptionImages(questionIndex);
            UpdateProgressText(questionIndex);
            EnableOptionButtons(true);

            // 切题时确保反馈 UI 关闭
            SetActiveIfNotNull(correctPrompt, false);
            SetActiveIfNotNull(wrongPrompt, false);

            // 启动本题倒计时
            StartPerQuestionCountdown();
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

        private void UpdateOptionImages(int questionIndex)
        {
            if (optionImages == null || optionImages.Length == 0)
            {
                return;
            }

            if (questionSprites == null || questionIndex < 0 || questionIndex >= questionSprites.Length)
            {
                Debug.LogWarning($"[QuizPanelController] questionSprites 未正确配置或索引越界（index={questionIndex}）。", this);
                return;
            }

            QuestionSprites spritesForQuestion = questionSprites[questionIndex];
            if (spritesForQuestion == null || spritesForQuestion.optionSprites == null)
            {
                Debug.LogWarning($"[QuizPanelController] 第 {questionIndex} 题的 optionSprites 未配置。", this);
                return;
            }

            int count = Mathf.Min(optionImages.Length, spritesForQuestion.optionSprites.Length);
            for (int i = 0; i < count; i++)
            {
                if (optionImages[i] == null)
                {
                    continue;
                }

                optionImages[i].sprite = spritesForQuestion.optionSprites[i];
                optionImages[i].enabled = optionImages[i].sprite != null;
            }

            // 多余的 Image 隐藏（如果有的话）
            for (int i = count; i < optionImages.Length; i++)
            {
                if (optionImages[i] != null)
                {
                    optionImages[i].enabled = false;
                }
            }
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
            if (optionIndexToUse >= 0)
            {
                correct = IsCurrentOptionCorrect(optionIndexToUse);
            }

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }
            feedbackCoroutine = StartCoroutine(ShowFeedbackAndSubmitAnswer(correct, optionIndexToUse));
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
            string userAnswer = optionIndex.ToString();
            return string.Equals(userAnswer, expected, StringComparison.Ordinal);
        }

        private IEnumerator ShowFeedbackAndSubmitAnswer(bool correct, int optionIndex)
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

            // 将玩家答案提交给 QuizModule，由其负责计分、记录日志和出下一题。
            // 如果 optionIndex 为 -1（未选择且超时），会作为 "-1" 记录在日志中，一定判错。
            string answerString = optionIndex >= 0 ? optionIndex.ToString() : "-1";
            quizModule.OnUserAnswer(answerString);

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
            if (optionButtons == null)
            {
                return;
            }

            foreach (var btn in optionButtons)
            {
                if (btn != null)
                {
                    btn.interactable = enable;
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