using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 一次性在 UITestScene 中生成教学 UI 布局（UGUI）。
/// 使用方式：打开 UITestScene，在菜单点击 Tools/Build UITestScene UI。
/// 生成后你可以自由在场景中编辑 UI 细节。
/// </summary>
public static class UITestSceneUIBuilder
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
    private static readonly Color BackgroundColor = new Color32(244, 244, 246, 255);
    private static readonly Color CardColor = Color.white;
    private static readonly Color PrimaryColor = new Color32(59, 130, 246, 255);
    private static readonly Color PrimaryLight = new Color32(219, 234, 254, 255);
    private static readonly Color TextDark = new Color32(17, 24, 39, 255);
    private static readonly Color TextMuted = new Color32(107, 114, 128, 255);

    [MenuItem("Tools/Build UITestScene UI")]
    private static void BuildUITestSceneUI()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("当前没有打开的场景。");
            return;
        }

        if (scene.name != "UITestScene")
        {
            if (!EditorUtility.DisplayDialog("提示",
                    $"当前场景名为 \"{scene.name}\"，而非 \"UITestScene\"。\n仍然在当前场景生成 UI 吗？",
                    "是，在当前场景生成", "取消"))
            {
                return;
            }
        }

        if (GameObject.Find("LessonCanvas") != null)
        {
            EditorUtility.DisplayDialog("提示", "场景中已存在 LessonCanvas，不再重复生成。", "好");
            return;
        }

        CreateUI();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("UITestScene UI 已生成。可以直接在场景中编辑细节。");
    }

    private static void CreateUI()
    {
        // EventSystem
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // Canvas
        GameObject canvasGO = new GameObject("LessonCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create LessonCanvas");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.anchorMin = Vector2.zero;
        canvasRT.anchorMax = Vector2.one;
        canvasRT.offsetMin = Vector2.zero;
        canvasRT.offsetMax = Vector2.zero;

        // Root 背景
        GameObject rootBG = CreateUIObject("RootBackground", canvasRT);
        Image rootImg = rootBG.AddComponent<Image>();
        rootImg.color = BackgroundColor;
        RectTransform rootRT = rootBG.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // TopBar
        RectTransform topBarRT = CreatePanel("TopBar", rootRT, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        topBarRT.sizeDelta = new Vector2(0, 80);
        topBarRT.offsetMin = new Vector2(0, -80);
        topBarRT.offsetMax = Vector2.zero;
        Image topBarImg = topBarRT.gameObject.AddComponent<Image>();
        topBarImg.color = Color.white;

        HorizontalLayoutGroup topBarHLG = topBarRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        topBarHLG.padding = new RectOffset(32, 32, 0, 0);
        topBarHLG.spacing = 24;
        topBarHLG.childAlignment = TextAnchor.MiddleCenter;
        topBarHLG.childForceExpandWidth = true;
        topBarHLG.childForceExpandHeight = false;

        // 顶部左标题
        GameObject titleGO = CreateUIObject("TopBar_Title", topBarRT);
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1;
        CreateTMP(titleGO, "DE 01·校园 咖啡馆·Bestellszene", 24, TextAlignmentOptions.MidlineLeft, TextDark);

        // 顶部右 Stepper
        GameObject stepperGO = CreateUIObject("TopBar_Stepper", topBarRT);
        HorizontalLayoutGroup stepperHLG = stepperGO.AddComponent<HorizontalLayoutGroup>();
        stepperHLG.spacing = 8;
        stepperHLG.childAlignment = TextAnchor.MiddleRight;
        stepperHLG.childForceExpandWidth = false;
        stepperHLG.childForceExpandHeight = false;
        LayoutElement stepperLE = stepperGO.AddComponent<LayoutElement>();
        stepperLE.minWidth = 400;

        string[] steps = { "einfhrung", "Erklärung", "Übung", "Transfer", "Test" };
        int currentStepIndex = 2;
        for (int i = 0; i < steps.Length; i++)
        {
            GameObject stepButtonGO = CreateUIObject("Step_" + (i + 1) + "_" + steps[i], stepperGO.transform as RectTransform);
            RectTransform stepRT = stepButtonGO.GetComponent<RectTransform>();
            stepRT.sizeDelta = new Vector2(120, 32);
            Image stepImg = stepButtonGO.AddComponent<Image>();
            stepButtonGO.AddComponent<Button>();

            bool isCurrent = (i == currentStepIndex);
            stepImg.color = isCurrent ? PrimaryColor : new Color32(224, 226, 231, 255);

            GameObject labelGO = CreateUIObject("Label", stepRT);
            CreateTMP(labelGO, steps[i], 16,
                TextAlignmentOptions.Midline, isCurrent ? Color.white : new Color32(75, 85, 99, 255));
        }

        // MainArea
        RectTransform mainAreaRT = CreatePanel("MainArea", rootRT, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        mainAreaRT.offsetMin = new Vector2(0, 120);
        mainAreaRT.offsetMax = new Vector2(0, -80);

        HorizontalLayoutGroup mainHLG = mainAreaRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        mainHLG.padding = new RectOffset(32, 32, 16, 16);
        mainHLG.spacing = 24;
        mainHLG.childAlignment = TextAnchor.UpperLeft;
        mainHLG.childForceExpandWidth = true;
        mainHLG.childForceExpandHeight = true;

        // 左列：Viewport
        RectTransform leftColRT = CreatePanel("LeftColumn_ViewportArea", mainAreaRT, Vector2.zero, Vector2.one, new Vector2(0, 1));
        LayoutElement leftColLE = leftColRT.gameObject.AddComponent<LayoutElement>();
        leftColLE.flexibleWidth = 3;

        VerticalLayoutGroup leftVLG = leftColRT.gameObject.AddComponent<VerticalLayoutGroup>();
        leftVLG.padding = new RectOffset(0, 0, 0, 8);
        leftVLG.spacing = 8;
        leftVLG.childAlignment = TextAnchor.UpperLeft;
        leftVLG.childForceExpandWidth = true;
        leftVLG.childForceExpandHeight = false;

        RectTransform viewportRT = CreatePanel("ViewportContainer", leftColRT, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Image viewportBG = viewportRT.gameObject.AddComponent<Image>();
        viewportBG.color = new Color32(229, 231, 235, 255);
        LayoutElement vpLE = viewportRT.gameObject.AddComponent<LayoutElement>();
        vpLE.flexibleHeight = 1;

        GameObject rawImageGO = CreateUIObject("ViewportRawImage", viewportRT);
        RawImage rawImage = rawImageGO.AddComponent<RawImage>();
        RectTransform rawRT = rawImageGO.GetComponent<RectTransform>();
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.offsetMin = Vector2.zero;
        rawRT.offsetMax = Vector2.zero;

        // Viewport 顶部右侧按钮
        RectTransform topRightBtnsRT = CreatePanel("Viewport_TopRightButtons", viewportRT, Vector2.one, Vector2.one, Vector2.one);
        topRightBtnsRT.sizeDelta = new Vector2(100, 32);
        topRightBtnsRT.anchoredPosition = new Vector2(-12, -12);
        HorizontalLayoutGroup trHLG = topRightBtnsRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        trHLG.spacing = 8;
        trHLG.childAlignment = TextAnchor.UpperRight;
        trHLG.childForceExpandWidth = false;
        trHLG.childForceExpandHeight = false;

        CreateIconButton("Btn_MuteToggle", topRightBtnsRT);
        CreateIconButton("Btn_PlayPause", topRightBtnsRT);

        // PhaseChip
        RectTransform phaseChipRT = CreatePanel("PhaseChip", viewportRT, Vector2.zero, Vector2.zero, Vector2.zero);
        phaseChipRT.sizeDelta = new Vector2(160, 32);
        phaseChipRT.anchoredPosition = new Vector2(12, 12);
        Image phaseImg = phaseChipRT.gameObject.AddComponent<Image>();
        phaseImg.color = PrimaryLight;

        GameObject phaseTextGO = CreateUIObject("PhaseText", phaseChipRT);
        TextMeshProUGUI phaseTMP = CreateTMP(phaseTextGO, "阶段: Übung", 16, TextAlignmentOptions.MidlineLeft, new Color32(29, 78, 216, 255));
        RectTransform phaseTextRT = phaseTextGO.GetComponent<RectTransform>();
        phaseTextRT.anchorMin = Vector2.zero;
        phaseTextRT.anchorMax = Vector2.one;
        phaseTextRT.offsetMin = new Vector2(8, 4);
        phaseTextRT.offsetMax = new Vector2(-8, -4);

        // 右列：信息卡片
        RectTransform rightColRT = CreatePanel("RightColumn_Info", mainAreaRT, Vector2.zero, Vector2.one, new Vector2(1, 1));
        LayoutElement rightColLE = rightColRT.gameObject.AddComponent<LayoutElement>();
        rightColLE.flexibleWidth = 2;

        VerticalLayoutGroup rightVLG = rightColRT.gameObject.AddComponent<VerticalLayoutGroup>();
        rightVLG.spacing = 16;
        rightVLG.childAlignment = TextAnchor.UpperLeft;
        rightVLG.childForceExpandWidth = true;
        rightVLG.childForceExpandHeight = false;

        // Card 1
        RectTransform cardTaskRT = CreateCard(rightColRT, "Card_CurrentTask", 160);
        CreateCardTitleAndBody(cardTaskRT,
            "Aktuelle Aufgabe",
            "请观察示例对话，并尝试在虚拟咖啡馆中用德语完成点单。注意句型结构和礼貌用语。");

        // Card 2
        RectTransform cardTargetRT = CreateCard(rightColRT, "Card_TargetSentence", 180);
        RectTransform targetContentRT = cardTargetRT.GetComponent<VerticalLayoutGroup>().transform as RectTransform;
        GameObject targetTitleGO = CreateUIObject("Title_TargetSentence", targetContentRT);
        CreateTMP(targetTitleGO, "Zielsatz", 20, TextAlignmentOptions.TopLeft, TextDark);

        GameObject targetMainGO = CreateUIObject("Text_TargetSentence_Main", targetContentRT);
        CreateTMP(targetMainGO, "Ich möchte großen Kaffee.", 30, TextAlignmentOptions.TopLeft, TextDark);

        GameObject targetExplainGO = CreateUIObject("Text_TargetSentence_Explain", targetContentRT);
        CreateTMP(targetExplainGO, "这是在咖啡馆中点一杯大杯咖啡的基本句型。", 18, TextAlignmentOptions.TopLeft, TextMuted);

        // Card 3
        RectTransform cardInstrRT = CreateCard(rightColRT, "Card_Instructions", 140);
        CreateCardTitleAndBody(cardInstrRT,
            "指令",
            "使用手柄指向并点击咖啡杯进行选择，或者抓取对象进行摆放。当需要说话时，按下录制按钮并用德语表达你的点单。");

        // BottomBar
        RectTransform bottomBarRT = CreatePanel("BottomBar", rootRT, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        bottomBarRT.sizeDelta = new Vector2(0, 120);
        Image bottomImg = bottomBarRT.gameObject.AddComponent<Image>();
        bottomImg.color = Color.white;

        HorizontalLayoutGroup bottomHLG = bottomBarRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        bottomHLG.padding = new RectOffset(32, 32, 12, 12);
        bottomHLG.spacing = 24;
        bottomHLG.childAlignment = TextAnchor.MiddleCenter;
        bottomHLG.childForceExpandWidth = true;
        bottomHLG.childForceExpandHeight = true;

        // Bottom 左：字幕
        RectTransform subRT = CreatePanel("Bottom_Subtitles", bottomBarRT, Vector2.zero, Vector2.one, new Vector2(0, 0.5f));
        VerticalLayoutGroup subVLG = subRT.gameObject.AddComponent<VerticalLayoutGroup>();
        subVLG.spacing = 4;
        subVLG.childAlignment = TextAnchor.MiddleLeft;
        LayoutElement subLE = subRT.gameObject.AddComponent<LayoutElement>();
        subLE.flexibleWidth = 3;

        GameObject deSubGO = CreateUIObject("Subtitle_DE", subRT);
        CreateTMP(deSubGO, "Ich möchte großen Kaffee, bitte.", 26, TextAlignmentOptions.MidlineLeft, TextDark);

        GameObject cnSubGO = CreateUIObject("Subtitle_CN", subRT);
        CreateTMP(cnSubGO, "我想要一杯大杯咖啡，谢谢。", 18, TextAlignmentOptions.MidlineLeft, TextMuted);

        // Bottom 右：按钮
        RectTransform actionsRT = CreatePanel("Bottom_Actions", bottomBarRT, Vector2.zero, Vector2.one, new Vector2(1, 0.5f));
        HorizontalLayoutGroup actHLG = actionsRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        actHLG.spacing = 16;
        actHLG.childAlignment = TextAnchor.MiddleRight;
        actHLG.childForceExpandWidth = false;
        actHLG.childForceExpandHeight = false;
        LayoutElement actLE = actionsRT.gameObject.AddComponent<LayoutElement>();
        actLE.flexibleWidth = 2;

        CreateTextButton("Btn_Nochmal", actionsRT, "Nochmal", CardColor, TextDark);
        CreateTextButton("Btn_Weiter", actionsRT, "Weiter", PrimaryColor, Color.white);
        CreateTextButton("Btn_Record", actionsRT, "录制", new Color32(239, 68, 68, 255), Color.white);

        Selection.activeGameObject = canvasGO;
    }

    // 辅助方法
    private static GameObject CreateUIObject(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static TextMeshProUGUI CreateTMP(GameObject go, string text, float fontSize, TextAlignmentOptions align, Color color)
    {
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return tmp;
    }

    private static RectTransform CreateCard(RectTransform parent, string name, float preferredHeight)
    {
        RectTransform cardRT = CreatePanel(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 1));
        Image img = cardRT.gameObject.AddComponent<Image>();
        img.color = CardColor;
        VerticalLayoutGroup vlg = cardRT.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperLeft;

        LayoutElement le = cardRT.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.flexibleWidth = 1;

        return cardRT;
    }

    private static void CreateCardTitleAndBody(RectTransform cardRT, string title, string body)
    {
        RectTransform contentRT = cardRT.GetComponent<VerticalLayoutGroup>().transform as RectTransform;

        GameObject titleGO = CreateUIObject("Title", contentRT);
        CreateTMP(titleGO, title, 20, TextAlignmentOptions.TopLeft, TextDark);

        GameObject bodyGO = CreateUIObject("Body", contentRT);
        CreateTMP(bodyGO, body, 18, TextAlignmentOptions.TopLeft, TextDark);
    }

    private static void CreateIconButton(string name, RectTransform parent)
    {
        GameObject btnGO = CreateUIObject(name, parent);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(32, 32);
        Image img = btnGO.AddComponent<Image>();
        img.color = new Color32(229, 231, 235, 255);
        btnGO.AddComponent<Button>();
    }

    private static void CreateTextButton(string name, RectTransform parent, string label, Color bgColor, Color textColor)
    {
        GameObject btnGO = CreateUIObject(name, parent);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(140, 48);
        Image img = btnGO.AddComponent<Image>();
        img.color = bgColor;
        btnGO.AddComponent<Button>();

        GameObject txtGO = CreateUIObject("Label", rt);
        CreateTMP(txtGO, label, 18, TextAlignmentOptions.Midline, textColor);
    }
}


