using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 在指定 Canvas 下，运行时生成 Apple Vision Pro 风格的封面页：
/// - 标题「智慧德语课堂 / Intelligent German Learning」
/// - 左右双卡片按钮：「校园咖啡馆 / Campus Café」「校园水果店 / Campus Fruit Shop」
/// - 玻璃拟态卡片 + 半透明背景（具体材质由美术在 Inspector 中配置）
/// 说明：脚本只负责结构与基础动画，不破坏现有 UI；需要你在场景中挂到任意对象上即可。
/// </summary>
public class CoverPageUI : MonoBehaviour
{
    [Header("挂载目标")]
    [Tooltip("一般为场景里的 LessonCanvas（Canvas 的 RectTransform）。留空则自动向上查找 Canvas。")]
    public RectTransform rootCanvas;

    [Header("视觉资源（需在 Inspector 中配置）")]
    [Tooltip("全屏背景使用的材质，建议为 URP 模糊渐变 Shader 的材质，可选。")]
    public Material backgroundMaterial;

    [Tooltip("玻璃卡片/按钮底板的材质，建议为 URP 玻璃拟态 Shader 的材质，可选。")]
    public Material cardMaterial;

    [Tooltip("带圆角（约 24~36 像素）的 9-Slice 精灵，用于卡片与按钮。")]
    public Sprite roundedSprite;

    [Tooltip("左侧插图（比如校园咖啡馆插画），可选。")]
    public Sprite leftIllustration;

    [Tooltip("右侧插图（比如水果店插画），可选。")]
    public Sprite rightIllustration;

    [Header("布局")]
    public Vector2 cardsSize = new Vector2(520, 520);
    public float cardsSpacing = 48f;

    [Header("背景渐变")]
    public Color gradientColorA = new Color(0.10f, 0.16f, 0.28f, 1f);
    public Color gradientColorB = new Color(0.24f, 0.36f, 0.62f, 1f);
    public float gradientSpeed = 0.2f;
    public float backgroundAlpha = 0.72f;

    private void Start()
    {
        BuildIfNeeded();
    }

    private void BuildIfNeeded()
    {
        var canvasRect = rootCanvas != null
            ? rootCanvas
            : GetComponentInParent<Canvas>() != null
                ? GetComponentInParent<Canvas>().GetComponent<RectTransform>()
                : null;

        if (canvasRect == null) return;

        // 避免重复生成
        if (canvasRect.Find("CoverRoot") != null) return;

        // 全屏 Cover 根节点
        var coverRoot = CreateRect("CoverRoot", canvasRect);
        StretchFull(coverRoot);

        // 背景面板（半透明 + 渐变）
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(CoverBackgroundGradient));
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.SetParent(coverRoot, false);
        StretchFull(bgRect);

        var bgImage = bgGO.GetComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, backgroundAlpha);
        if (backgroundMaterial != null) bgImage.material = backgroundMaterial;

        var bgGradient = bgGO.GetComponent<CoverBackgroundGradient>();
        bgGradient.colorA = gradientColorA;
        bgGradient.colorB = gradientColorB;
        bgGradient.speed = gradientSpeed;

        // 中心容器：水平左右双卡片
        var container = CreateRect("CardsContainer", coverRoot);
        container.anchorMin = new Vector2(0.5f, 0.5f);
        container.anchorMax = new Vector2(0.5f, 0.5f);
        container.pivot = new Vector2(0.5f, 0.5f);
        container.sizeDelta = new Vector2(cardsSize.x * 2f + cardsSpacing, cardsSize.y);
        container.anchoredPosition = Vector2.zero;

        var hLayout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = cardsSpacing;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.padding = new RectOffset(16, 16, 16, 16);

        // 左侧卡片：校园咖啡馆
        var leftCard = CreateCard(container, "CampusCafeCard");
        BuildCardContent(
            leftCard,
            "智慧德语课堂",
            "Intelligent German Learning",
            "校园咖啡馆",
            "Campus Café",
            leftIllustration
        );

        // 右侧卡片：校园水果店
        var rightCard = CreateCard(container, "CampusFruitCard");
        BuildCardContent(
            rightCard,
            "智慧德语课堂",
            "Intelligent German Learning",
            "校园水果店",
            "Campus Fruit Shop",
            rightIllustration
        );
    }

    private static RectTransform CreateRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = Vector3.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private RectTransform CreateCard(RectTransform parent, string name)
    {
        var card = CreateRect(name, parent);
        card.sizeDelta = cardsSize;

        var image = card.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.16f);
        image.raycastTarget = true;
        if (cardMaterial != null) image.material = cardMaterial;
        if (roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }

        var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var elem = card.gameObject.AddComponent<LayoutElement>();
        elem.preferredWidth = cardsSize.x;
        elem.preferredHeight = cardsSize.y;

        return card;
    }

    private void BuildCardContent(
        RectTransform card,
        string titleZh,
        string titleEn,
        string buttonZh,
        string buttonEn,
        Sprite illustration
    )
    {
        // 上方标题（中 + 英）
        var titleZhText = CreateText("TitleZh", card, titleZh, 30, FontStyle.Normal, TextAnchor.MiddleLeft);
        var titleEnText = CreateText("TitleEn", card, titleEn, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
        titleEnText.color = new Color(1f, 1f, 1f, 0.75f);

        // 中间留白占位（柔和排版）
        var spacer = CreateRect("Spacer", card);
        var spacerElem = spacer.gameObject.AddComponent<LayoutElement>();
        spacerElem.flexibleHeight = 1f;

        // 中下方按钮
        var buttonRoot = CreateRect("PrimaryButton", card);
        buttonRoot.sizeDelta = new Vector2(260, 64);

        var buttonImage = buttonRoot.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.27f, 0.52f, 0.98f, 0.90f);
        if (roundedSprite != null)
        {
            buttonImage.sprite = roundedSprite;
            buttonImage.type = Image.Type.Sliced;
        }

        var button = buttonRoot.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        // 悬停光晕
        var glowRect = CreateRect("Glow", buttonRoot);
        StretchFull(glowRect);
        var glowImage = glowRect.gameObject.AddComponent<Image>();
        glowImage.color = new Color(0.6f, 0.8f, 1f, 0.0f);
        glowImage.raycastTarget = false;
        if (roundedSprite != null)
        {
            glowImage.sprite = roundedSprite;
            glowImage.type = Image.Type.Sliced;
        }

        var effects = buttonRoot.gameObject.AddComponent<CoverButtonEffects>();
        effects.glowImage = glowImage;

        var btnLabel = CreateText("Label", buttonRoot, $"{buttonZh}  ({buttonEn})", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        btnLabel.color = Color.white;

        // 插图（右下角）
        if (illustration != null)
        {
            var illuRect = CreateRect("Illustration", card);
            illuRect.anchorMin = new Vector2(1f, 0f);
            illuRect.anchorMax = new Vector2(1f, 0f);
            illuRect.pivot = new Vector2(1f, 0f);
            illuRect.anchoredPosition = new Vector2(0f, 0f);
            illuRect.sizeDelta = new Vector2(220, 180);

            var illuImage = illuRect.gameObject.AddComponent<Image>();
            illuImage.sprite = illustration;
            illuImage.preserveAspect = true;
            illuImage.color = new Color(1f, 1f, 1f, 0.96f);

            var illuElem = illuRect.gameObject.AddComponent<LayoutElement>();
            illuElem.preferredHeight = 180;
            illuElem.preferredWidth = 220;
        }
    }

    private static Text CreateText(
        string name,
        RectTransform parent,
        string content,
        int fontSize,
        FontStyle style,
        TextAnchor align
    )
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0, 0);

        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = align;
        text.supportRichText = true;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        // 使用默认字体，避免依赖工程中特定字体资产
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var elem = go.gameObject.AddComponent<LayoutElement>();
        elem.preferredHeight = fontSize + 8;

        return text;
    }
}

/// <summary>
/// 简单的背景渐变动画：在两种颜色之间缓慢插值，营造柔和的动态背景。
/// 建议将本组件挂在带 Image 的全屏背景上。
/// </summary>
public class CoverBackgroundGradient : MonoBehaviour
{
    public Color colorA = Color.black;
    public Color colorB = Color.white;
    public float speed = 0.2f;

    private Image targetImage;
    private float t;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
    }

    private void Update()
    {
        if (targetImage == null) return;
        t += Time.deltaTime * speed;
        float lerp = (Mathf.Sin(t) + 1f) * 0.5f;
        var c = Color.Lerp(colorA, colorB, lerp);
        c.a = targetImage.color.a; // 只替换颜色，不动透明度
        targetImage.color = c;
    }
}

/// <summary>
/// 封面按钮的交互效果：悬停放大、点击轻微回弹，并带柔和光晕。
/// 需要配合 Button 组件使用。
/// </summary>
public class CoverButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("缩放参数")]
    public float hoverScale = 1.05f;
    public float pressedScale = 0.97f;
    public float lerpSpeed = 10f;

    [Header("光晕")]
    public Image glowImage;
    public float glowAlpha = 0.45f;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private void Awake()
    {
        originalScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        targetScale = originalScale;

        if (glowImage != null)
        {
            var c = glowImage.color;
            c.a = 0f;
            glowImage.color = c;
        }
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;
        SetGlow(glowAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
        SetGlow(0f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;
    }

    private void SetGlow(float alpha)
    {
        if (glowImage == null) return;
        var c = glowImage.color;
        c.a = alpha;
        glowImage.color = c;
    }
}







