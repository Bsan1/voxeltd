using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BuildModeHud : MonoBehaviour
{
    private const int MaxBuildingNameLength = 28;

    [Serializable]
    private sealed class SlotView
    {
        public string id;
        public Image frame;
        public Image accent;
        public Text titleText;
        public Text subtitleText;

        public void Set(string title, string subtitle, bool selected, Color selectedColor, Color idleColor, Color titleColor)
        {
            if (frame != null)
            {
                frame.color = selected ? selectedColor : idleColor;
            }

            if (accent != null)
            {
                accent.enabled = selected;
                accent.color = selectedColor;
            }

            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = titleColor;
            }

            if (subtitleText != null)
            {
                subtitleText.text = subtitle;
                subtitleText.color = selected ? Color.white : new Color(0.72f, 0.78f, 0.8f, 1f);
            }
        }
    }

    [Serializable]
    private sealed class DetailRow
    {
        public string label;
        public Text labelText;
        public Text valueText;

        public void Set(string labelValue, string value, Color valueColor)
        {
            label = labelValue;

            if (labelText != null)
            {
                labelText.text = labelValue;
            }

            if (valueText != null)
            {
                valueText.text = value;
                valueText.color = valueColor;
            }
        }
    }

    [Header("References")]
    [SerializeField] private BuildPlacementController placementController;
    [SerializeField] private bool autoFindController = true;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private Text modeText;
    [SerializeField] private Image modePill;
    [SerializeField] private Text selectedBuildingText;
    [SerializeField] private SlotView[] slots;
    [SerializeField] private DetailRow[] details;

    [Header("Refresh")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

    [Header("Colors")]
    [SerializeField] private Color barColor = new Color(0.045f, 0.052f, 0.057f, 0.92f);
    [SerializeField] private Color panelColor = new Color(0.075f, 0.088f, 0.096f, 0.96f);
    [SerializeField] private Color slotIdleColor = new Color(0.105f, 0.12f, 0.13f, 0.96f);
    [SerializeField] private Color buildColor = new Color(0.42f, 0.95f, 0.68f, 1f);
    [SerializeField] private Color removeColor = new Color(1f, 0.18f, 0.12f, 1f);
    [SerializeField] private Color labelColor = new Color(0.67f, 0.74f, 0.78f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.62f, 0.68f, 0.72f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.64f, 0.34f, 1f);

    private float nextRefreshTime;
    private string lastSummary;

    public BuildPlacementController PlacementController => placementController;

    private void Awake()
    {
        EnsureController();

        if (barRoot == null || slots == null || slots.Length == 0 || details == null || details.Length == 0)
        {
            CreateDefaultLayout();
        }

        RefreshNow();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshNow();
    }

    public void SetPlacementController(BuildPlacementController controller)
    {
        SetPlacementController(controller, true);
    }

    public void SetPlacementController(BuildPlacementController controller, bool refresh)
    {
        placementController = controller;
        if (refresh)
        {
            RefreshNow();
        }
    }

    public void SetAutoFindController(bool autoFind, bool refresh)
    {
        autoFindController = autoFind;
        if (refresh)
        {
            RefreshNow();
        }
    }

    public void RefreshNow()
    {
        EnsureController();

        if (placementController == null)
        {
            SetMode(false);
            SetSelectedBuilding("No Controller");
            SetSlot(0, "Build", "-", false, buildColor);
            SetSlot(1, "Remove", "-", false, removeColor);
            SetDetail(0, "Tool", "-", inactiveColor);
            SetDetail(1, "Direction", "-", inactiveColor);
            SetDetail(2, "Preview", "-", inactiveColor);
            lastSummary = "BuildModeHud layout=BottomBar controller=<none>";
            return;
        }

        bool buildMode = placementController.BuildModeActive;
        bool removeTool = placementController.RemoveToolActive;
        BuildingData selected = placementController.SelectedBuilding;
        string buildingName = FormatBuildingName(selected);
        string selectedSlot = removeTool ? "Remove" : "Build";
        string preview = ResolvePreviewText(buildMode, removeTool, placementController.CanPlaceCurrentPreview);

        SetMode(buildMode);
        SetSelectedBuilding(removeTool ? "Remove Tool" : buildingName);
        SetSlot(0, "Build", buildingName, !removeTool, buildColor);
        SetSlot(1, "Remove", "Hologram", removeTool, removeColor);
        SetDetail(0, "Tool", selectedSlot, removeTool ? removeColor : textColor);
        SetDetail(1, "Direction", removeTool ? "-" : placementController.SelectedDirection.ToString(), textColor);
        SetDetail(2, "Preview", preview, ResolvePreviewColor(buildMode, placementController.CanPlaceCurrentPreview, removeTool));

        lastSummary = $"{nameof(BuildModeHud)} layout=BottomBar mode={(buildMode ? "ON" : "OFF")} tool={selectedSlot} building={(removeTool ? "Remove Tool" : buildingName)} preview={preview}";
    }

    public string GetDebugSummary()
    {
        RefreshNow();
        return lastSummary;
    }

    public void CreateDefaultLayout()
    {
        EnsureCanvas();
        ClearChildren(transform);

        barRoot = CreateRect("BottomBuildBar", transform);
        barRoot.anchorMin = new Vector2(0f, 0f);
        barRoot.anchorMax = new Vector2(1f, 0f);
        barRoot.pivot = new Vector2(0.5f, 0f);
        barRoot.anchoredPosition = new Vector2(0f, 18f);
        barRoot.sizeDelta = new Vector2(-48f, 128f);

        Image barImage = barRoot.gameObject.AddComponent<Image>();
        barImage.color = barColor;

        HorizontalLayoutGroup barLayout = barRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        barLayout.padding = new RectOffset(16, 16, 14, 14);
        barLayout.spacing = 14f;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = false;
        barLayout.childForceExpandHeight = true;

        RectTransform statusRoot = CreatePanel("Status", barRoot, 230f);
        VerticalLayoutGroup statusLayout = statusRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        statusLayout.padding = new RectOffset(14, 14, 10, 10);
        statusLayout.spacing = 8f;
        statusLayout.childControlWidth = true;
        statusLayout.childControlHeight = true;
        statusLayout.childForceExpandWidth = true;
        statusLayout.childForceExpandHeight = false;

        Text title = CreateText("Title", statusRoot, "BUILD", 16, buildColor, FontStyle.Bold, TextAnchor.MiddleLeft);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

        modePill = CreatePill("ModePill", statusRoot, buildColor, out modeText);
        modePill.GetComponent<LayoutElement>().preferredHeight = 30f;

        selectedBuildingText = CreateText("SelectedBuilding", statusRoot, "-", 15, textColor, FontStyle.Bold, TextAnchor.MiddleLeft);
        selectedBuildingText.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

        RectTransform slotsRoot = CreatePanel("Slots", barRoot, 0f);
        LayoutElement slotsLayoutElement = slotsRoot.GetComponent<LayoutElement>();
        slotsLayoutElement.flexibleWidth = 1f;
        HorizontalLayoutGroup slotsLayout = slotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        slotsLayout.padding = new RectOffset(12, 12, 10, 10);
        slotsLayout.spacing = 10f;
        slotsLayout.childControlWidth = true;
        slotsLayout.childControlHeight = true;
        slotsLayout.childForceExpandWidth = false;
        slotsLayout.childForceExpandHeight = true;

        slots = new[]
        {
            CreateSlot("BuildSlot", "Build", "Selected", buildColor, slotsRoot, 250f),
            CreateSlot("RemoveSlot", "Remove", "Hologram", removeColor, slotsRoot, 170f),
        };

        RectTransform detailRoot = CreatePanel("Details", barRoot, 270f);
        VerticalLayoutGroup detailLayout = detailRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        detailLayout.padding = new RectOffset(14, 14, 10, 10);
        detailLayout.spacing = 8f;
        detailLayout.childControlWidth = true;
        detailLayout.childControlHeight = true;
        detailLayout.childForceExpandWidth = true;
        detailLayout.childForceExpandHeight = false;

        details = new[]
        {
            CreateDetailRow("Tool", detailRoot),
            CreateDetailRow("Direction", detailRoot),
            CreateDetailRow("Preview", detailRoot),
        };

        RefreshNow();
    }

    private void EnsureCanvas()
    {
        transform.localScale = Vector3.one;

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;
        }

        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private RectTransform CreatePanel(string name, Transform parent, float preferredWidth)
    {
        RectTransform panel = CreateRect(name, parent);
        Image image = panel.gameObject.AddComponent<Image>();
        image.color = panelColor;

        LayoutElement layout = panel.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;
        layout.minHeight = 100f;
        return panel;
    }

    private Image CreatePill(string name, Transform parent, Color color, out Text text)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 30f;

        text = CreateText("Text", rect, "ON", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return image;
    }

    private SlotView CreateSlot(string id, string title, string subtitle, Color accentColor, Transform parent, float width)
    {
        RectTransform root = CreateRect(id, parent);
        Image frame = root.gameObject.AddComponent<Image>();
        frame.color = slotIdleColor;

        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = 140f;

        VerticalLayoutGroup stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.padding = new RectOffset(12, 12, 10, 10);
        stack.spacing = 6f;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;

        Image accent = CreateAccent(root, accentColor);
        Text titleText = CreateText("Title", root, title, 14, labelColor, FontStyle.Bold, TextAnchor.MiddleLeft);
        titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
        Text subtitleText = CreateText("Subtitle", root, subtitle, 16, textColor, FontStyle.Bold, TextAnchor.MiddleLeft);
        subtitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        return new SlotView
        {
            id = id,
            frame = frame,
            accent = accent,
            titleText = titleText,
            subtitleText = subtitleText,
        };
    }

    private Image CreateAccent(Transform parent, Color color)
    {
        RectTransform accent = CreateRect("Accent", parent);
        Image image = accent.gameObject.AddComponent<Image>();
        image.color = color;
        LayoutElement layout = accent.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 4f;
        return image;
    }

    private DetailRow CreateDetailRow(string label, Transform parent)
    {
        RectTransform row = CreateRect($"{label}Row", parent);
        HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

        Text labelText = CreateText("Label", row, label, 13, labelColor, FontStyle.Normal, TextAnchor.MiddleLeft);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 86f;
        labelLayout.minWidth = 86f;

        Text valueText = CreateText("Value", row, "-", 14, textColor, FontStyle.Bold, TextAnchor.MiddleRight);
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.flexibleWidth = 1f;
        valueLayout.preferredWidth = 140f;

        return new DetailRow
        {
            label = label,
            labelText = labelText,
            valueText = valueText,
        };
    }

    private void SetMode(bool active)
    {
        if (modeText != null)
        {
            modeText.text = active ? "ON" : "OFF";
        }

        if (modePill != null)
        {
            modePill.color = active ? buildColor : inactiveColor;
        }
    }

    private void SetSelectedBuilding(string value)
    {
        if (selectedBuildingText != null)
        {
            selectedBuildingText.text = Shorten(value, MaxBuildingNameLength);
        }
    }

    private void SetSlot(int index, string title, string subtitle, bool selected, Color selectedColor)
    {
        if (slots == null || index < 0 || index >= slots.Length || slots[index] == null)
        {
            return;
        }

        slots[index].Set(title, Shorten(subtitle, MaxBuildingNameLength), selected, selectedColor, slotIdleColor, labelColor);
    }

    private void SetDetail(int index, string label, string value, Color color)
    {
        if (details == null || index < 0 || index >= details.Length || details[index] == null)
        {
            return;
        }

        details[index].Set(label, value, color);
    }

    private void EnsureController()
    {
        if (placementController == null && autoFindController)
        {
            placementController = FindFirstObjectByType<BuildPlacementController>();
        }
    }

    private static string ResolvePreviewText(bool buildMode, bool removeTool, bool canPlace)
    {
        if (!buildMode)
        {
            return "Inactive";
        }

        if (removeTool)
        {
            return canPlace ? "Target" : "No Target";
        }

        return canPlace ? "Valid" : "No Cell";
    }

    private Color ResolvePreviewColor(bool buildMode, bool canPlace, bool removeTool)
    {
        if (!buildMode)
        {
            return inactiveColor;
        }

        if (removeTool && canPlace)
        {
            return removeColor;
        }

        return canPlace ? buildColor : warningColor;
    }

    private static string FormatBuildingName(BuildingData building)
    {
        if (building == null)
        {
            return "None";
        }

        string value = !string.IsNullOrWhiteSpace(building.DisplayName)
            ? building.DisplayName
            : building.BuildingId;

        if (string.IsNullOrWhiteSpace(value))
        {
            value = building.name;
        }

        return Shorten(value, MaxBuildingNameLength);
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        Color color,
        FontStyle style,
        TextAnchor alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = ResolveDefaultFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Font ResolveDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }
}
