using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class OrbitAroundModel : MonoBehaviour
{
    [Header("Targets")]
    public Transform target;                 // the whole jet engine (root object)
    public EnginePartSeparator separator;    // auto-found if empty

    [Header("Zoom")]
    public float distance = 10f;
    public float minDistance = 0.2f;
    public float maxDistance = 100f;
    public float zoomSpeed = 0.1f;
    public float fitPadding = 1.3f;
    [Range(0.2f, 1.5f)]
    public float startCloseUp = 0.7f;

    [Header("Mouse Rotation (hold right button)")]
    public float rotateSpeed = 0.25f;
    public float minPitch = -89f;
    public float maxPitch = 89f;

    [Header("Auto Rotate")]
    public bool autoRotate = true;
    public float autoRotateSpeed = 8f;
    public float resumeDelay = 2f;

    [Header("Focus")]
    public bool clickToFocus = true;
    public bool makeClickable = true;
    public float focusSmoothness = 6f;

    [Header("On-screen UI")]
    public bool showPartName = true;
    public bool showControls = true;
    public bool showZoomSlider = true;
    public string wholeEngineName = "Jet Engine";
    public float nameFontSize = 48f;
    public float controlsFontSize = 44f;
    public float sliderLabelFontSize = 48f;

    [Header("Info Button / Details Box")]
    public bool showInfoButton = true;
    [TextArea(3, 12)]
    public string wholeEngineDetails = "";   // details shown for the whole engine
    public float detailsFontSize = 26f;
    public float boxWidth = 650f;            // always used
    public bool autoBoxHeight = true;        // true = height fits the text
    public float boxHeight = 350f;           // used when Auto Box Height is off
    public float maxBoxHeight = 750f;        // cap when auto (text shrinks to fit)
    public bool openOnStart = false;

    [Header("Smoothing")]
    public float smoothness = 12f;

    private float yaw, pitch = 15f;
    private float targetYaw, targetPitch, targetDistance;
    private float startDistance;
    private float lastInputTime = -999f;
    private Vector3 focusPoint;
    private int focusIndex = -1;            // -1 = whole engine

    private Camera cam;
    private Renderer[] rootRenderers;
    private Renderer[][] partRenderers;
    private Dictionary<Transform, int> partLookup = new Dictionary<Transform, int>();

    private TextMeshProUGUI nameText;
    private Slider zoomSlider;
    private string shownName = null;

    private RectTransform detailsPanel;
    private TextMeshProUGUI detailsText;
    private string shownDetails = null;
    private float shownWidth, shownHeightSetting;
    private bool detailsOpen;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (separator == null) separator = FindObjectOfType<EnginePartSeparator>();

        BuildCaches();

        focusPoint = GetBounds(-1).center;

        Vector3 dir = transform.position - focusPoint;
        if (dir.sqrMagnitude > 0.001f)
        {
            distance = dir.magnitude;
            yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(dir.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
        }

        distance = Mathf.Clamp(distance * startCloseUp, minDistance, maxDistance);

        startDistance = distance;
        targetYaw = yaw;
        targetPitch = pitch;
        targetDistance = distance;

        EnsureEventSystem();
        BuildUI();

        detailsOpen = openOnStart;
        if (detailsPanel != null) detailsPanel.gameObject.SetActive(detailsOpen);
    }

    // ---------------- SETUP ----------------

    void BuildCaches()
    {
        int n = (separator != null && separator.parts != null) ? separator.parts.Length : 0;
        partRenderers = new Renderer[n][];
        var all = new List<Renderer>();

        for (int i = 0; i < n; i++)
        {
            Transform t = separator.parts[i] != null ? separator.parts[i].target : null;
            if (t == null) continue;

            partRenderers[i] = t.GetComponentsInChildren<Renderer>();
            partLookup[t] = i;
            all.AddRange(partRenderers[i]);

            if (makeClickable) AddColliders(t);
        }

        rootRenderers = target != null ? target.GetComponentsInChildren<Renderer>() : all.ToArray();
    }

    void AddColliders(Transform t)
    {
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }

        if (t.GetComponentInParent<Rigidbody>() == null)
        {
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // ---------------- BOUNDS / FOCUS ----------------

    Bounds GetBounds(int index)
    {
        Renderer[] rs = index < 0 ? rootRenderers : partRenderers[index];
        bool has = false;
        Bounds b = new Bounds();

        if (rs != null)
        {
            foreach (var r in rs)
            {
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        if (!has)
        {
            Transform t = index < 0 ? target : separator.parts[index].target;
            b = new Bounds(t != null ? t.position : Vector3.zero, Vector3.one);
        }
        return b;
    }

    float FitDistance(Bounds b)
    {
        float fov = cam != null ? cam.fieldOfView : 60f;
        float radius = b.extents.magnitude;
        float d = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * fitPadding;
        return Mathf.Clamp(d, minDistance, maxDistance);
    }

    void FocusOn(int index)
    {
        focusIndex = index;
        if (index < 0) targetDistance = startDistance;
        else targetDistance = FitDistance(GetBounds(index));
    }

    bool PartValid(int i)
    {
        return separator != null && separator.parts != null &&
               i >= 0 && i < separator.parts.Length &&
               separator.parts[i] != null && separator.parts[i].target != null;
    }

    // Label from the Inspector, or the Hierarchy name if Label is empty / still "Part"
    string GetPartName(int i)
    {
        var p = separator.parts[i];
        if (!string.IsNullOrWhiteSpace(p.label) && p.label != "Part")
            return p.label;
        return p.target.name;
    }

    // Details from the Inspector
    string GetDetails()
    {
        string d = (focusIndex >= 0 && PartValid(focusIndex))
            ? separator.parts[focusIndex].details
            : wholeEngineDetails;

        if (string.IsNullOrWhiteSpace(d)) return "No details added yet.";
        return d;
    }

    void Cycle(int dir)
    {
        int n = (separator != null && separator.parts != null) ? separator.parts.Length : 0;
        if (n == 0) return;

        int idx = focusIndex;
        for (int s = 0; s < n; s++)
        {
            if (idx < 0) idx = dir > 0 ? 0 : n - 1;
            else idx = (idx + dir + n) % n;

            if (PartValid(idx)) { FocusOn(idx); return; }
        }
    }

    void TryPick(Vector2 screenPos)
    {
        if (cam == null) return;
        // Ignore clicks on buttons / slider / details box
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Physics.SyncTransforms();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            Transform t = hit.collider.transform;
            while (t != null)
            {
                if (partLookup.TryGetValue(t, out int idx)) { FocusOn(idx); return; }
                t = t.parent;
            }
        }
    }

    // ---------------- MAIN LOOP ----------------

    void LateUpdate()
    {
        bool rightHeld = false, leftClicked = false, nextKey = false, prevKey = false, spaceKey = false;
        Vector2 delta = Vector2.zero, mousePos = Vector2.zero;
        float scrollNotches = 0f;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse != null)
        {
            rightHeld = mouse.rightButton.isPressed;
            leftClicked = mouse.leftButton.wasPressedThisFrame;
            delta = mouse.delta.ReadValue();
            mousePos = mouse.position.ReadValue();
            scrollNotches = mouse.scroll.ReadValue().y / 120f;
        }
        if (kb != null)
        {
            nextKey = kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            prevKey = kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
            spaceKey = kb.spaceKey.wasPressedThisFrame;
        }
#else
        rightHeld = Input.GetMouseButton(1);
        leftClicked = Input.GetMouseButtonDown(0);
        delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        mousePos = Input.mousePosition;
        scrollNotches = Input.GetAxis("Mouse ScrollWheel") * 10f;
        nextKey = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        prevKey = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        spaceKey = Input.GetKeyDown(KeyCode.Space);
#endif

        if (nextKey) Cycle(+1);
        if (prevKey) Cycle(-1);
        if (spaceKey) FocusOn(-1);
        if (clickToFocus && leftClicked) TryPick(mousePos);

        if (rightHeld)
        {
            targetYaw += delta.x * rotateSpeed;
            targetPitch -= delta.y * rotateSpeed;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            lastInputTime = Time.time;
        }

        if (Mathf.Abs(scrollNotches) > 0.001f)
        {
            targetDistance *= 1f - scrollNotches * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            lastInputTime = Time.time;
        }

        if (autoRotate && Time.time - lastInputTime > resumeDelay)
            targetYaw += autoRotateSpeed * Time.deltaTime;

        float t = 1f - Mathf.Exp(-smoothness * Time.deltaTime);
        yaw = Mathf.Lerp(yaw, targetYaw, t);
        pitch = Mathf.Lerp(pitch, targetPitch, t);
        distance = Mathf.Lerp(distance, targetDistance, t);

        Vector3 desired = GetBounds(focusIndex).center;
        float f = 1f - Mathf.Exp(-focusSmoothness * Time.deltaTime);
        focusPoint = Vector3.Lerp(focusPoint, desired, f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = focusPoint + rotation * new Vector3(0f, 0f, -distance);
        transform.rotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);

        UpdateUI();
    }

    // ---------------- ON-SCREEN UI (TextMeshPro) ----------------

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
        var canvasGO = new GameObject("CameraUI_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;   // snaps UI to whole pixels for sharper text
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Part name (top left)
        nameText = CreateText(canvasGO.transform, "Part Name", wholeEngineName, nameFontSize,
                              TextAlignmentOptions.TopLeft, FontStyles.Bold);
        var nrt = nameText.rectTransform;
        nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0f, 1f);
        nrt.anchoredPosition = new Vector2(30f, -20f);
        nrt.sizeDelta = new Vector2(1200f, 70f);
        nameText.gameObject.SetActive(showPartName);

        // Info button + details box (just below the name)
        if (showInfoButton) BuildInfo(canvasGO.transform);

        // Controls (bottom right)
        var controls = CreateText(canvasGO.transform, "Controls",
            "Right mouse + drag: rotate    Scroll: zoom\n" +
            "A / D or Arrows: previous / next part\n" +
            "Click: pick part    Space: whole engine",
            controlsFontSize, TextAlignmentOptions.BottomRight, FontStyles.Normal);
        controls.color = new Color(1f, 1f, 1f, 0.9f);
        var crt = controls.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(-30f, 110f);   // raised so it clears the slider
        crt.sizeDelta = new Vector2(1300f, 220f);
        controls.gameObject.SetActive(showControls);

        // Zoom slider (bottom center)
        if (showZoomSlider) BuildSlider(canvasGO.transform);
    }

    void BuildInfo(Transform parent)
    {
        // "i" button
        var btnGO = new GameObject("Info Button", typeof(RectTransform));
        btnGO.transform.SetParent(parent, false);
        var brt = btnGO.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(30f, -95f);
        brt.sizeDelta = new Vector2(56f, 56f);

        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.2f, 0.45f, 0.85f, 0.95f);
#if UNITY_EDITOR
        // Round button in the editor (the built-in knob sprite is not available in builds)
        var knob = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        if (knob != null) img.sprite = knob;
#endif

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ToggleDetails);

        var label = CreateText(btnGO.transform, "i", "i", 44f,
                               TextAlignmentOptions.Center, FontStyles.Normal);
        label.outlineWidth = 0f;

#if !UNITY_WEBGL
        // Use Times New Roman (or a similar serif font) for the "i".
        // Skipped in WebGL, where OS fonts are not available.
        try
        {
            Font serif = Font.CreateDynamicFontFromOSFont(
                new string[] { "Times New Roman", "Times", "Liberation Serif", "Georgia", "Serif" }, 90);
            TMP_FontAsset serifAsset = TMP_FontAsset.CreateFontAsset(serif);
            if (serifAsset != null) label.font = serifAsset;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not load serif font for the info button: " + e.Message);
        }
#endif

        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        // Details box
        var panelGO = new GameObject("Details Box", typeof(RectTransform));
        panelGO.transform.SetParent(parent, false);
        detailsPanel = panelGO.GetComponent<RectTransform>();
        detailsPanel.anchorMin = detailsPanel.anchorMax = detailsPanel.pivot = new Vector2(0f, 1f);
        detailsPanel.anchoredPosition = new Vector2(30f, -165f);
        detailsPanel.sizeDelta = new Vector2(boxWidth, boxHeight);

        var bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.1f, 0.88f);   // blocks clicks so you don't pick parts through it

        detailsText = CreateText(panelGO.transform, "Details Text", "", detailsFontSize,
                                 TextAlignmentOptions.TopLeft, FontStyles.Normal);
        detailsText.outlineWidth = 0f;
        detailsText.enableWordWrapping = true;
        detailsText.overflowMode = TextOverflowModes.Truncate;
        var drt = detailsText.rectTransform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = new Vector2(20f, 20f);
        drt.offsetMax = new Vector2(-20f, -20f);
    }

    void ToggleDetails()
    {
        detailsOpen = !detailsOpen;
        if (detailsPanel != null) detailsPanel.gameObject.SetActive(detailsOpen);
        shownDetails = null;   // force a refresh
    }

    void RefreshDetails()
    {
        if (detailsPanel == null || detailsText == null || !detailsOpen) return;

        string d = GetDetails();
        float heightSetting = autoBoxHeight ? -1f : boxHeight;

        if (d == shownDetails && boxWidth == shownWidth && heightSetting == shownHeightSetting) return;
        shownDetails = d;
        shownWidth = boxWidth;
        shownHeightSetting = heightSetting;

        detailsText.enableAutoSizing = false;
        detailsText.fontSize = detailsFontSize;
        detailsText.text = d;

        float h = boxHeight;
        if (autoBoxHeight)
        {
            float pref = detailsText.GetPreferredValues(d, boxWidth - 40f, 0f).y;
            h = Mathf.Clamp(pref + 40f, 90f, maxBoxHeight);
        }
        detailsPanel.sizeDelta = new Vector2(boxWidth, h);

        // If the text is still too long for the box, shrink the font to fit
        detailsText.enableAutoSizing = true;
        detailsText.fontSizeMax = detailsFontSize;
        detailsText.fontSizeMin = 12f;
    }

    void BuildSlider(Transform parent)
    {
        var root = new GameObject("Zoom Slider", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(420f, 30f);

        var bg = NewChild(root.transform, "Background");
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.3f);
        bg.anchorMin = new Vector2(0f, 0.5f);
        bg.anchorMax = new Vector2(1f, 0.5f);
        bg.sizeDelta = new Vector2(0f, 8f);
        bg.anchoredPosition = Vector2.zero;

        var fillArea = NewChild(root.transform, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-20f, 8f);
        fillArea.anchoredPosition = Vector2.zero;

        var fill = NewChild(fillArea, "Fill");
        var fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 1f, 0.9f);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.sizeDelta = new Vector2(10f, 0f);

        var slideArea = NewChild(root.transform, "Handle Slide Area");
        slideArea.anchorMin = new Vector2(0f, 0.5f);
        slideArea.anchorMax = new Vector2(1f, 0.5f);
        slideArea.sizeDelta = new Vector2(-20f, 24f);
        slideArea.anchoredPosition = Vector2.zero;

        var handle = NewChild(slideArea, "Handle");
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = Color.white;
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(20f, 0f);

        zoomSlider = root.AddComponent<Slider>();
        zoomSlider.fillRect = fill;
        zoomSlider.handleRect = handle;
        zoomSlider.targetGraphic = handleImg;
        zoomSlider.direction = Slider.Direction.LeftToRight;
        zoomSlider.minValue = 0f;
        zoomSlider.maxValue = 1f;
        zoomSlider.onValueChanged.AddListener(OnZoomSlider);

        var far = CreateText(root.transform, "Far Label", "Far", sliderLabelFontSize,
                             TextAlignmentOptions.MidlineRight, FontStyles.Normal);
        var frt = far.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
        frt.pivot = new Vector2(1f, 0.5f);
        frt.anchoredPosition = new Vector2(-20f, 0f);
        frt.sizeDelta = new Vector2(180f, 70f);

        var close = CreateText(root.transform, "Close Label", "Close", sliderLabelFontSize,
                               TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        var clrt = close.rectTransform;
        clrt.anchorMin = clrt.anchorMax = new Vector2(1f, 0.5f);
        clrt.pivot = new Vector2(0f, 0.5f);
        clrt.anchoredPosition = new Vector2(20f, 0f);
        clrt.sizeDelta = new Vector2(180f, 70f);
    }

    RectTransform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
                               TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = style;
        t.color = Color.white;
        t.raycastTarget = false;

        // thinner outline = sharper text (set outlineWidth to 0 to remove it completely)
        t.outlineWidth = 0.08f;
        t.outlineColor = new Color32(0, 0, 0, 180);
        return t;
    }

    void OnZoomSlider(float v)
    {
        float logMin = Mathf.Log(minDistance), logMax = Mathf.Log(maxDistance);
        targetDistance = Mathf.Clamp(Mathf.Exp(Mathf.Lerp(logMax, logMin, v)), minDistance, maxDistance);
        lastInputTime = Time.time;
    }

    void UpdateUI()
    {
        if (nameText != null)
        {
            string n = wholeEngineName;
            if (focusIndex >= 0 && PartValid(focusIndex))
                n = GetPartName(focusIndex);

            if (n != shownName)
            {
                nameText.text = n;
                shownName = n;
            }
        }

        RefreshDetails();

        if (zoomSlider != null)
        {
            float logMin = Mathf.Log(minDistance), logMax = Mathf.Log(maxDistance);
            float current = 1f - Mathf.InverseLerp(logMin, logMax, Mathf.Log(Mathf.Max(targetDistance, 0.0001f)));
            zoomSlider.SetValueWithoutNotify(current);
        }
    }
}