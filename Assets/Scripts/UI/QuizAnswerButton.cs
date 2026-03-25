using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace UI
{
    /// <summary>
    /// ���ⰴť�����������Ƿ�Ϊ��ȷ�𰸣���ʾ��Ӧ��ʾ UI������һ��ʱ����Զ����ء�
    /// �ɸ��ã����ű��ҵ����� Button �ϣ��� Inspector ���ü��ɡ�
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class QuizAnswerButton : MonoBehaviour
    {
        [Header("��������")]
        [Tooltip("�ð�ť�Ƿ������ȷ��")]
        [SerializeField] private bool isCorrectAnswer = false;
        [Header("��ʾ UI ����")]
        [Tooltip("���ʱҪ��ʾ����ʾ UI����������塢ͼƬ�����ֵȸ�����")]
        [SerializeField] private GameObject correctPrompt;
        [Tooltip("���ʱҪ��ʾ����ʾ UI")]
        [SerializeField] private GameObject wrongPrompt;
        [Header("��ʾʱ�����룩")]
        [Tooltip("��ʾ UI ��ʾ��ʱ�䣬��λ����")]
        [SerializeField] private float promptDuration = 1f;
        [Header("��ѡ������֪ͨ")]
        [Tooltip("����ҵ����ȷ��ʱ����")]
        [SerializeField] private UnityEvent onAnsweredCorrect;
        [Tooltip("����ҵ�������ʱ����")]
        [SerializeField] private UnityEvent onAnsweredWrong;
        private Button button;
        private Coroutine promptCoroutine;
        private void Awake()
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("[QuizAnswerButton] δ�ҵ� Button ������ű����޷�������", this);
                return;
            }
            // ȷ����ʾ��ʼΪ���أ�����ڳ�����û�أ������Զ���һ�Σ�
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
                ShowPrompt(correctPrompt, wrongPrompt);
                onAnsweredCorrect?.Invoke();
            }
            else
            {
                ShowPrompt(wrongPrompt, correctPrompt);
                onAnsweredWrong?.Invoke();
            }
        }
        private void ShowPrompt(GameObject toShow, GameObject toHide)
        {
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
                promptCoroutine = null;
            }
            // �����෴��ʾ
            SetPromptActive(toHide, false);
            // ��ʾ��ǰ��ʾ��������ʱЭ��
            promptCoroutine = StartCoroutine(ShowPromptTemporarily(toShow));
        }
        private IEnumerator ShowPromptTemporarily(GameObject prompt)
        {
            if (prompt == null)
            {
                yield break;
            }
            SetPromptActive(prompt, true);
            float duration = Mathf.Max(0f, promptDuration);
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
            SetPromptActive(prompt, false);
            promptCoroutine = null;
        }
        private static void SetPromptActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
        /// <summary>
        /// ����ʱ��̬�����Ƿ�Ϊ��ȷ�𰸣���ѡ����
        /// </summary>
        /// <param name="value">true ��ʾ�ð�ťΪ��ȷ�𰸡�</param>
        public void SetIsCorrectAnswer(bool value)
        {
            isCorrectAnswer = value;
        }
        /// <summary>
        /// ����ʱ��̬�޸���ʾʱ�����룩����ѡ����
        /// </summary>
        public void SetPromptDuration(float seconds)
        {
            promptDuration = Mathf.Max(0f, seconds);
        }
    }
}
