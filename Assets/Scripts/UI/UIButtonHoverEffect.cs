using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 通用UI按钮悬停 / 点击动效组件（尽量零配置即可好看）。
    /// 建议挂在带有 Button / Image 的 GameObject 上。
    /// 仅负责视觉效果，不影响 Button.onClick 逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonHoverEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Header("目标引用（通常无需手动设置）")]
        [SerializeField] private RectTransform targetRectTransform;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Graphic textGraphic; // TMP_Text 或 Text 都继承自 Graphic

        [Header("缩放设置")]
        [SerializeField] private bool enableScale = true;
        [Tooltip("Hover 相对初始缩放的倍数，例如 1.05 = 放大 5%")]
        [SerializeField] private float hoverScaleMultiplier = 1.05f;
        [Tooltip("按下时相对初始缩放的倍数，例如 0.95 = 缩小 5%")]
        [SerializeField] private float pressedScaleMultiplier = 0.95f;
        [SerializeField] private float scaleDuration = 0.1f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("背景颜色变化（可选）")]
        [SerializeField] private bool adjustBackgroundColor = false;
        [Tooltip("Hover 时的亮度系数，>1 变亮，<1 变暗")]
        [SerializeField] private float backgroundHoverBrightness = 1.05f;
        [Tooltip("按下时的亮度系数，>1 变亮，<1 变暗")]
        [SerializeField] private float backgroundPressedBrightness = 0.9f;
        [SerializeField] private float backgroundColorDuration = 0.08f;

        [Header("文本/图标颜色变化（可选）")]
        [SerializeField] private bool adjustTextColor = false;
        [Tooltip("Hover 时的亮度系数")]
        [SerializeField] private float textHoverBrightness = 1.05f;
        [Tooltip("按下时的亮度系数")]
        [SerializeField] private float textPressedBrightness = 0.9f;
        [SerializeField] private float textColorDuration = 0.08f;

        [Header("Hover 音效（自动绑定）")]
        [SerializeField] private bool enableHoverSound = true;
        [Tooltip("从 Resources 中自动加载名为 UI_Hover 的 AudioClip，并在 PointerEnter 时播放")]
        [SerializeField] private string hoverClipResourceName = "UI_Hover";

        private Vector3 initialScale;
        private Color initialBackgroundColor;
        private Color initialTextColor;

        private Coroutine scaleCoroutine;
        private Coroutine backgroundColorCoroutine;
        private Coroutine textColorCoroutine;

        private bool isPointerInside;
        private bool isPressed;

        private AudioSource audioSource;
        private static AudioClip sharedHoverClip;

        private void Awake()
        {
            if (targetRectTransform == null)
            {
                targetRectTransform = transform as RectTransform;
            }

            if (enableScale && targetRectTransform != null)
            {
                initialScale = targetRectTransform.localScale;
            }

            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (backgroundImage != null)
            {
                initialBackgroundColor = backgroundImage.color;
            }

            if (textGraphic == null)
            {
                textGraphic = GetComponentInChildren<Graphic>();
            }

            if (textGraphic != null)
            {
                initialTextColor = textGraphic.color;
            }

            if (enableHoverSound)
            {
                EnsureHoverAudio();
            }
        }

        private void OnEnable()
        {
            ApplyImmediateNormal();
        }

        private void OnDisable()
        {
            StopAllTweens();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            if (!isPressed)
            {
                PlayToHover();
            }

            PlayHoverSound();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            if (!isPressed)
            {
                PlayToNormal();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            PlayToPressed();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            if (isPointerInside)
            {
                PlayToHover();
            }
            else
            {
                PlayToNormal();
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            // 键盘/手柄导航选中时，模拟 Hover 状态
            PlayToHover();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            PlayToNormal();
        }

        public void PlayToNormal()
        {
            if (enableScale && targetRectTransform != null)
            {
                StartScaleTween(targetRectTransform.localScale, initialScale);
            }

            if (adjustBackgroundColor && backgroundImage != null)
            {
                StartBackgroundColorTween(backgroundImage.color, initialBackgroundColor, backgroundColorDuration);
            }

            if (adjustTextColor && textGraphic != null)
            {
                StartTextColorTween(textGraphic.color, initialTextColor, textColorDuration);
            }
        }

        public void PlayToHover()
        {
            if (enableScale && targetRectTransform != null)
            {
                var targetScale = initialScale * hoverScaleMultiplier;
                StartScaleTween(targetRectTransform.localScale, targetScale);
            }

            if (adjustBackgroundColor && backgroundImage != null)
            {
                var targetColor = AdjustBrightness(initialBackgroundColor, backgroundHoverBrightness);
                StartBackgroundColorTween(backgroundImage.color, targetColor, backgroundColorDuration);
            }

            if (adjustTextColor && textGraphic != null)
            {
                var targetColor = AdjustBrightness(initialTextColor, textHoverBrightness);
                StartTextColorTween(textGraphic.color, targetColor, textColorDuration);
            }
        }

        public void PlayToPressed()
        {
            if (enableScale && targetRectTransform != null)
            {
                var targetScale = initialScale * pressedScaleMultiplier;
                StartScaleTween(targetRectTransform.localScale, targetScale);
            }

            if (adjustBackgroundColor && backgroundImage != null)
            {
                var targetColor = AdjustBrightness(initialBackgroundColor, backgroundPressedBrightness);
                StartBackgroundColorTween(backgroundImage.color, targetColor, backgroundColorDuration);
            }

            if (adjustTextColor && textGraphic != null)
            {
                var targetColor = AdjustBrightness(initialTextColor, textPressedBrightness);
                StartTextColorTween(textGraphic.color, targetColor, textColorDuration);
            }
        }

        private void ApplyImmediateNormal()
        {
            if (enableScale && targetRectTransform != null)
            {
                targetRectTransform.localScale = initialScale;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = initialBackgroundColor;
            }

            if (textGraphic != null)
            {
                textGraphic.color = initialTextColor;
            }
        }

        private void StartScaleTween(Vector3 from, Vector3 to)
        {
            if (!enableScale || targetRectTransform == null)
            {
                return;
            }

            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            scaleCoroutine = StartCoroutine(TweenScale(from, to, scaleDuration));
        }

        private void StartBackgroundColorTween(Color from, Color to, float duration)
        {
            if (!adjustBackgroundColor || backgroundImage == null)
            {
                return;
            }

            if (backgroundColorCoroutine != null)
            {
                StopCoroutine(backgroundColorCoroutine);
            }

            backgroundColorCoroutine = StartCoroutine(TweenBackgroundColor(from, to, duration));
        }

        private void StartTextColorTween(Color from, Color to, float duration)
        {
            if (!adjustTextColor || textGraphic == null)
            {
                return;
            }

            if (textColorCoroutine != null)
            {
                StopCoroutine(textColorCoroutine);
            }

            textColorCoroutine = StartCoroutine(TweenTextColor(from, to, duration));
        }

        private void StopAllTweens()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }

            if (backgroundColorCoroutine != null)
            {
                StopCoroutine(backgroundColorCoroutine);
                backgroundColorCoroutine = null;
            }

            if (textColorCoroutine != null)
            {
                StopCoroutine(textColorCoroutine);
                textColorCoroutine = null;
            }
        }

        private IEnumerator TweenScale(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                targetRectTransform.localScale = to;
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                float eased = scaleCurve != null ? scaleCurve.Evaluate(t) : t;
                targetRectTransform.localScale = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }

            targetRectTransform.localScale = to;
        }

        private IEnumerator TweenBackgroundColor(Color from, Color to, float duration)
        {
            if (duration <= 0f)
            {
                backgroundImage.color = to;
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                backgroundImage.color = Color.LerpUnclamped(from, to, t);
                yield return null;
            }

            backgroundImage.color = to;
        }

        private IEnumerator TweenTextColor(Color from, Color to, float duration)
        {
            if (duration <= 0f)
            {
                textGraphic.color = to;
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                textGraphic.color = Color.LerpUnclamped(from, to, t);
                yield return null;
            }

            textGraphic.color = to;
        }

        private static Color AdjustBrightness(Color color, float brightness)
        {
            return new Color(
                color.r * brightness,
                color.g * brightness,
                color.b * brightness,
                color.a);
        }

        private void EnsureHoverAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            if (sharedHoverClip == null && !string.IsNullOrEmpty(hoverClipResourceName))
            {
                sharedHoverClip = Resources.Load<AudioClip>(hoverClipResourceName);
            }

            if (sharedHoverClip != null)
            {
                audioSource.clip = sharedHoverClip;
            }
        }

        private void PlayHoverSound()
        {
            if (!enableHoverSound)
            {
                return;
            }

            if (audioSource == null || audioSource.clip == null)
            {
                EnsureHoverAudio();
            }

            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.PlayOneShot(audioSource.clip);
            }
        }
    }
}