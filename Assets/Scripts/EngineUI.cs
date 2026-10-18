using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class EngineUI : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public EngineSpin engineSpin;
    public EnginePartSeparator separator;

    [Header("Button Labels")]
    public string startLabel = "Start Engine";
    public string stopLabel = "Stop Engine";
    public string separateLabel = "Disassemble Parts";
    public string assembleLabel = "Assemble Parts";

    [Header("Button Colors")]
    public Color startColor = new Color(0.15f, 0.6f, 0.25f);   // green (engine off)
    public Color stopColor = new Color(0.8f, 0.2f, 0.2f);     // red (engine running)
    public Color separateColor = new Color(0.2f, 0.4f, 0.8f);

    [Header("Text")]
    public float buttonFontSize = 34f;

    private bool engineRunning;
    private bool partsSeparated;
    private TextMeshProUGUI startText;
    private TextMeshProUGUI separateText;
    private Image startImage;

    void Start()
    {
        if (engineSpin == null) engineSpin = FindObjectOfType<EngineSpin>();
        if (separator == null) separator = FindObjectOfType<EnginePartSeparator>();

        EnsureEventSystem();
        BuildUI();
    }

    // ---------------- ENGINE ----------------

    void StartEngine()
    {
        if (engineSpin != null) engineSpin.StartEngine();
        else Debug.LogWarning("EngineUI: no EngineSpin found.");
    }

    void StopEngine()
    {
        if (engineSpin != null) engineSpin.StopEngine();
    }

    // ---------------- PARTS ----------------

    void SeparateParts()
    {
        if (separator != null) separator.Separate();
        else Debug.LogWarning("EngineUI: no EnginePartSeparator found.");
    }

    void AssembleParts()
    {
        if (separator != null) separator.Assemble();
    }

    // ---------------- BUTTON HANDLERS ----------------

    void OnStartClicked()
    {
        engineRunning = !engineRunning;
        if (engineRunning) StartEngine(); else StopEngine();

        startText.text = engineRunning ? stopLabel : startLabel;
        startImage.color = engineRunning ? stopColor : startColor;
    }

    void OnSeparateClicked()
    {
        partsSeparated = !partsSeparated;
        if (partsSeparated) SeparateParts(); else AssembleParts();
        separateText.text = partsSeparated ? assembleLabel : separateLabel;
    }

    // ---------------- UI BUILDING ----------------

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("EngineUI_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 0f);
        prt.anchoredPosition = new Vector2(40, 40);
        prt.sizeDelta = new Vector2(380, 170);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 15;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        startText = CreateButton(panel.transform, startLabel, OnStartClicked, startColor);
        startImage = startText.GetComponentInParent<Image>();

        separateText = CreateButton(panel.transform, separateLabel, OnSeparateClicked, separateColor);
    }

    TextMeshProUGUI CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, Color color)
    {
        var go = new GameObject(label + " Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = color;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = buttonFontSize;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.raycastTarget = false;   // clicks go to the button
        return txt;
    }
}