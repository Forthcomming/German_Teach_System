using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace UI
{
    public class CountdownUIController : MonoBehaviour
    {
        [Header("Countdown Settings")]
        [Tooltip("倒计时时长（秒），默认 60 秒。")]
        public float countdownSeconds = 60f;

        [Header("UI References")]
        [Tooltip("用于显示倒计时的 TextMeshProUGUI 文本组件（必填）。")]
        public TextMeshProUGUI timeText;

        [Tooltip("当前倒计时 UI 的根对象。倒计时结束后会被隐藏。")]
        public GameObject currentPanel;

        [Tooltip("倒计时结束后要显示的新 UI 根对象。")]
        public GameObject nextPanel;

        [Header("Events")]
        [Tooltip("倒计时结束时触发的回调（在切换 UI 之后调用）。")]
        public UnityEvent onCountdownFinished;

        private Coroutine countdownRoutine;

        private void OnEnable()
        {
            // UI 被激活时自动开始倒计时
            StartCountdown();
        }

        private void OnDisable()
        {
            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }
        }

        /// <summary>
        /// 手动开始或重置倒计时。
        /// </summary>
        public void StartCountdown()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
            }

            countdownRoutine = StartCoroutine(CountdownCoroutine());
        }

        private IEnumerator CountdownCoroutine()
        {
            float timeLeft = Mathf.Max(0f, countdownSeconds);

            while (timeLeft > 0f)
            {
                UpdateTimeText(timeLeft);
                yield return new WaitForSeconds(1f);
                timeLeft -= 1f;
            }

            UpdateTimeText(0f);
            HandleCountdownFinished();
        }

        private void UpdateTimeText(float timeLeft)
        {
            int seconds = Mathf.CeilToInt(timeLeft);
            if (seconds < 0)
            {
                seconds = 0;
            }

            // 这里使用简单的“Xs”格式，如需 mm:ss 可在此修改
            string display = seconds + "s";

            if (timeText != null)
            {
                timeText.text = display;
            }
        }

        private void HandleCountdownFinished()
        {
            if (currentPanel != null)
            {
                currentPanel.SetActive(false);
            }

            if (nextPanel != null)
            {
                nextPanel.SetActive(true);
            }

            onCountdownFinished?.Invoke();
        }
    }
}

