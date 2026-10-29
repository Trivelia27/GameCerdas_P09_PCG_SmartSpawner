using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Helper pembuat elemen uGUI lewat kode (sprite rounded/circle/vignette dibuat prosedural).</summary>
public static class UIFactory
{
    public static readonly Color PanelColor = new Color(0.035f, 0.05f, 0.095f, 0.8f);
    public static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    public static readonly Color Red = new Color(1f, 0.32f, 0.38f);
    public static readonly Color Green = new Color(0.3f, 1f, 0.5f);
    public static readonly Color Dim = new Color(0.62f, 0.72f, 0.82f);

    private static Sprite rounded;
    private static Sprite circle;
    private static Sprite vignette;

    public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRoundedSprite(64, 22, true);
    public static Sprite Circle => circle != null ? circle : circle = MakeRoundedSprite(64, 32, false);
    public static Sprite Vignette => vignette != null ? vignette : vignette = MakeVignetteSprite(128);

    // ------------------------------------------------------------------ sprite

    private static Sprite MakeRoundedSprite(int size, int radius, bool sliced)
    {
        Texture2D texture = NewTexture(size);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance)));
            }
        }

        texture.Apply();

        Vector4 border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite MakeVignetteSprite(int size)
    {
        Texture2D texture = NewTexture(size);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt(Mathf.Pow((x + 0.5f - half) / half, 2f) + Mathf.Pow((y + 0.5f - half) / half, 2f));
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d));

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
    }

    // ------------------------------------------------------------------ rect helper

    public static RectTransform Node(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static void Stretch(RectTransform rt, float margin = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    public static LayoutElement Size(GameObject go, float width = -1f, float height = -1f, float flexibleWidth = -1f)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = go.AddComponent<LayoutElement>();
        }

        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleWidth = flexibleWidth;
        return element;
    }

    // ------------------------------------------------------------------ elemen

    public static Image MakeImage(Transform parent, string name, Color color, Sprite sprite = null, bool sliced = false)
    {
        RectTransform rt = Node(parent, name);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI MakeText(Transform parent, string content, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal, bool wrap = false)
    {
        RectTransform rt = Node(parent, "Text");
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    public static Button MakeButton(Transform parent, string label, float fontSize, UnityAction onClick, Color background)
    {
        RectTransform rt = Node(parent, "Button");
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, Color.white, 0.28f);
        colors.pressedColor = Color.Lerp(background, Color.black, 0.3f);
        colors.selectedColor = background;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI text = MakeText(rt, label, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(text.rectTransform);

        button.onClick.AddListener(onClick);
        button.onClick.AddListener(() => AudioManager.Play(Sfx.Click));

        return button;
    }

    public static Slider MakeSlider(Transform parent, float min, float max, bool whole, float value, UnityAction<float> onChange, Color fillColor)
    {
        RectTransform root = Node(parent, "Slider");
        Slider slider = root.gameObject.AddComponent<Slider>();

        RectTransform background = Node(root, "Background");
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.sprite = Rounded;
        backgroundImage.type = Image.Type.Sliced;
        backgroundImage.color = new Color(1f, 1f, 1f, 0.14f);
        Place(background, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
        background.anchorMax = new Vector2(1f, 0.5f);

        RectTransform fillArea = Node(root, "Fill Area");
        Place(fillArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
        fillArea.anchorMax = new Vector2(1f, 0.5f);

        RectTransform fill = Node(fillArea, "Fill");
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = Rounded;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = fillColor;
        fill.sizeDelta = new Vector2(8f, 0f);

        RectTransform handleArea = Node(root, "Handle Slide Area");
        Place(handleArea, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-18f, 18f));
        handleArea.anchorMax = new Vector2(1f, 0.5f);

        RectTransform handle = Node(handleArea, "Handle");
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = Circle;
        handleImage.color = Color.white;
        handle.sizeDelta = new Vector2(18f, 18f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChange);

        return slider;
    }

    public static VerticalLayoutGroup Vertical(GameObject go, float spacing, int padding)
    {
        VerticalLayoutGroup group = go.AddComponent<VerticalLayoutGroup>();
        group.spacing = spacing;
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.childAlignment = TextAnchor.UpperLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        return group;
    }

    public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing)
    {
        HorizontalLayoutGroup group = go.AddComponent<HorizontalLayoutGroup>();
        group.spacing = spacing;
        group.childAlignment = TextAnchor.MiddleLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = true;
        return group;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
