using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace UI
{
    /// <summary>
    /// 答题按钮控制器：根据是否为正确答案，显示对应提示 UI，并在一段时间后自动隐藏。
    /// 可复用，把脚本挂到任意 Button 上并在 Inspector 中配置即可。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class QuizAnswerButton : MonoBehaviour
    {
        [Header("答案设置")]
        [Tooltip("该按钮是否代表正确答案")]
        [SerializeField] private bool isCorrectAnswer = false;
        [Header("提示 UI 引用")]
        [Tooltip("答对时要显示的提示 UI（可以是面板、图片、文字等根对象）")]
        [SerializeField] private GameObject correctPrompt;
        [Tooltip("答错时要显示的提示 UI")]
        [SerializeField] private GameObject wrongPrompt;
        [Header("题目切换（可选）")]
        [Tooltip("当前题目的 UI 根对象。答对后会将其关闭。")]
        [SerializeField] private GameObject currentQuestionRoot;
        [Tooltip("下一题的 UI 根对象。答对后会将其打开。")]
        [SerializeField] private GameObject nextQuestionRoot;
        [Header("显示时长（秒）")]
        [Tooltip("提示 UI 显示的持续时间，单位：秒")]
        [SerializeField] private float promptDuration = 1f;
        [Header("答题结果事件通知")]
        [Tooltip("玩家答对时触发")]
        [SerializeField] private UnityEvent onAnsweredCorrect;
        [Tooltip("玩家答错时触发")]
        [SerializeField] private UnityEvent onAnsweredWrong;
        private Button button;
        private Coroutine promptCoroutine;
        private void Awake()
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("[QuizAnswerButton] 未找到 Button 组件，脚本将无法正常工作。", this);
                return;
            }
            // 确保提示初始为隐藏；即使场景里忘记关，这里也会统一重置一次。
            SetPromptActive(correctPrompt, false);
            SetPromptActive(wrongPrompt, false);
        }
        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }
        }
        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
                promptCoroutine = null;
            }
        }
        private void OnButtonClicked()
        {
            if (isCorrectAnswer)
            {
                ShowPrompt(correctPrompt, wrongPrompt, switchToNextQuestionAfterPrompt: true);
                onAnsweredCorrect?.Invoke();
            }
            else
            {
                ShowPrompt(wrongPrompt, correctPrompt, switchToNextQuestionAfterPrompt: false);
                onAnsweredWrong?.Invoke();
            }
        }
        private void ShowPrompt(GameObject toShow, GameObject toHide, bool switchToNextQuestionAfterPrompt)
        {
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
                promptCoroutine = null;
            }
            // 先隐藏相反提示
            SetPromptActive(toHide, false);
            // 显示当前提示并启动定时隐藏协程
            promptCoroutine = StartCoroutine(ShowPromptTemporarily(toShow, switchToNextQuestionAfterPrompt));
        }
        private IEnumerator ShowPromptTemporarily(GameObject prompt, bool switchToNextQuestionAfterPrompt)
        {
            if (prompt == null)
            {
                if (switchToNextQuestionAfterPrompt)
                {
                    SwitchToNextQuestionUI();
                }
                promptCoroutine = null;
                yield break;
            }
            SetPromptActive(prompt, true);
            float duration = Mathf.Max(0f, promptDuration);
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
            SetPromptActive(prompt, false);
            if (switchToNextQuestionAfterPrompt)
            {
                SwitchToNextQuestionUI();
            }
            promptCoroutine = null;
        }
        private void SwitchToNextQuestionUI()
        {
            SetPromptActive(currentQuestionRoot, false);
            if (nextQuestionRoot != null)
            {
                SetPromptActive(nextQuestionRoot, true);
                return;
            }
            Debug.LogWarning("[QuizAnswerButton] 未配置 nextQuestionRoot，已关闭当前题但无法自动显示下一题。", this);
        }
        private static void SetPromptActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
        /// <summary>
        /// 运行时动态设置是否为正确答案（可选）。
        /// </summary>
        /// <param name="value">true 表示该按钮为正确答案。</param>
        public void SetIsCorrectAnswer(bool value)
        {
            isCorrectAnswer = value;
        }
        /// <summary>
        /// 运行时动态修改提示时长（秒）（可选）。
        /// </summary>
        public void SetPromptDuration(float seconds)
        {
            promptDuration = Mathf.Max(0f, seconds);
        }
    }
}
