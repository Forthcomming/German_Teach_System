using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 为“点击播放音频”的按钮提供点击时 icon + 文字高亮，高亮在音频播放结束时自动恢复。
    /// 不接管播放逻辑，仅监听 Button.onClick 并根据绑定的 AudioSource 判断播完后再取消高亮。
    /// 可与 UIButtonHoverEffect 挂在同一按钮上共存。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonAudioHighlight : MonoBehaviour
    {
        [Header("播放该按钮点击音的 AudioSource（留空则使用固定时长高亮）")]
        [SerializeField] private AudioSource audioSource;

        [Header("高亮目标（可选，至少填一个）")]
        [Tooltip("用于高亮的图标，通常为 Image")]
        [SerializeField] private Graphic iconGraphic;
        [Tooltip("用于高亮的文字，Text 或 TMP_Text；留空时尝试从子节点取一个 Graphic")]
        [SerializeField] private Graphic textGraphic;

        [Header("高亮样式")]
        [Tooltip("高亮时颜色亮度系数，>1 变亮")]
        [SerializeField] private float highlightBrightness = 2f;
        [Tooltip("高亮时相对初始缩放的倍数，1 表示不缩放")]
        [SerializeField] private float highlightScaleMultiplier = 1f;
        [Tooltip("等待音频结束的最大秒数，超时强制取消高亮")]
        [SerializeField] private float maxWaitSeconds = 15f;
        [Tooltip("未绑定 AudioSource 时高亮持续秒数")]
        [SerializeField] private float fallbackHighlightDuration = 2f;

        private Button button;
        private readonly List<Graphic> graphics = new List<Graphic>();
        private readonly List<Color> initialColors = new List<Color>();
        private readonly List<Vector3> initialScales = new List<Vector3>();
        private Coroutine waitCoroutine;
        private bool isHighlighted;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("[UIButtonAudioHighlight] 未找到 Button，组件将无效。", this);
                return;
            }

            CollectGraphics();
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClick);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClick);
            }

            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
                waitCoroutine = null;
            }

            if (isHighlighted)
            {
                ApplyNormal();
            }
        }

        private void CollectGraphics()
        {
            graphics.Clear();
            initialColors.Clear();
            initialScales.Clear();

            if (iconGraphic != null)
            {
                graphics.Add(iconGraphic);
            }

            if (textGraphic != null)
            {
                graphics.Add(textGraphic);
            }

            if (graphics.Count == 0 && textGraphic == null)
            {
                var fallback = GetComponentInChildren<Graphic>();
                if (fallback != null)
                {
                    graphics.Add(fallback);
                }
            }

            CacheInitialState();
        }

        private void CacheInitialState()
        {
            initialColors.Clear();
            initialScales.Clear();
            foreach (var g in graphics)
            {
                if (g != null)
                {
                    initialColors.Add(g.color);
                    initialScales.Add(g.rectTransform != null ? g.rectTransform.localScale : g.transform.localScale);
                }
            }
        }

        private void OnButtonClick()
        {
            if (graphics.Count == 0)
            {
                return;
            }

            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
                waitCoroutine = null;
            }

            if (!isHighlighted)
            {
                CacheInitialState();
            }

            ApplyHighlight();
            isHighlighted = true;
            waitCoroutine = StartCoroutine(WaitThenRemoveHighlight());
        }

        private void ApplyHighlight()
        {
            for (int i = 0; i < graphics.Count; i++)
            {
                var g = graphics[i];
                if (g == null) continue;

                Color c = i < initialColors.Count ? initialColors[i] : g.color;
                g.color = new Color(
                    Mathf.Clamp01(c.r * highlightBrightness),
                    Mathf.Clamp01(c.g * highlightBrightness),
                    Mathf.Clamp01(c.b * highlightBrightness),
                    c.a);

                if (highlightScaleMultiplier != 1f && g.rectTransform != null)
                {
                    Vector3 baseScale = i < initialScales.Count ? initialScales[i] : g.rectTransform.localScale;
                    g.rectTransform.localScale = baseScale * highlightScaleMultiplier;
                }
            }
        }

        private void ApplyNormal()
        {
            for (int i = 0; i < graphics.Count; i++)
            {
                var g = graphics[i];
                if (g == null) continue;

                if (i < initialColors.Count)
                {
                    g.color = initialColors[i];
                }

                if (i < initialScales.Count && g.rectTransform != null)
                {
                    g.rectTransform.localScale = initialScales[i];
                }
            }

            isHighlighted = false;
        }

        private IEnumerator WaitThenRemoveHighlight()
        {
            yield return null;

            if (audioSource != null)
            {
                float elapsed = 0f;
                while (audioSource.isPlaying && elapsed < maxWaitSeconds)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < fallbackHighlightDuration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            ApplyNormal();
            waitCoroutine = null;
        }
    }
}
