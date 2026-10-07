using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
// 세이브 화면 공용 UI (Figma "Screen / Save" - 청회색 테마)
// =====================================================================================
// 세이브포인트 저장 창(SaveSlotDialog)과 이어하기 화면(SaveDataSceneController)이 같이 쓴다.
//
//   SaveScreenUI.BuildFrame()  : 검은 막 + 패널 그림 + 머리말(SAVE / 기록 남기기 / 안내 문구)
//   SaveSlotRow.Create()       : 슬롯 한 줄 [01 │ #03 경찰서        2026.10.07 21:14]
//                                          [   │ 안내 문구          플레이 01:24:10 ]
//   SaveScreenUI.CreateButton(): 아래쪽 버튼 (환경설정 버튼 그림 재사용)
//
// 그림은 Assets/Resources/Illusts/UI/Save/ 에 있다 (git 제외 - 드라이브 공유).
// 그림이 없으면 같은 자리에 색 도형으로 대신 그린다.
public static class SaveScreenUI
{
    public const string ArtFolder = "Illusts/UI/Save/";
    private const string ButtonArtFolder = "Illusts/UI/Settings/";

    // 패널: 화면 1440x1080 기준 x240 y120 960x840 (= 화면 정가운데)
    public const float BoxWidth = 960f;
    public const float BoxHeight = 840f;
    public const float Pad = 56f;
    public const float SlotTop = 176f;
    public const float SlotStep = 130f;     // 슬롯 높이 116 + 간격 14
    public const float FooterY = BoxHeight - Pad - 54f;   // 아래 버튼 위쪽

    public static readonly Color KickerColor = new Color(0.435f, 0.518f, 0.588f);   // 6F8496
    public static readonly Color TitleColor = new Color(0.894f, 0.918f, 0.937f);    // E4EAEF
    public static readonly Color HintColor = new Color(0.482f, 0.541f, 0.596f);     // 7B8A98
    public static readonly Color ButtonTextColor = new Color(0.663f, 0.729f, 0.788f);// A9BAC9
    private static readonly Color AccentColor = new Color(0.561f, 0.643f, 0.718f);  // 8FA4B7
    private static readonly Color DividerColor = new Color(0.165f, 0.208f, 0.251f); // 2A3540
    private static readonly Color PanelColor = new Color(0.063f, 0.086f, 0.114f, 0.95f);

    // 화면 전체를 덮는 막 + 패널을 만들고, 글자/슬롯을 얹을 패널(box)을 돌려준다.
    public static GameObject BuildFrame(Transform canvas, string objectName, string kicker, string title,
                                        out RectTransform box, out TMP_Text subtitle)
    {
        var root = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas, false);
        Stretch((RectTransform)root.transform);
        var dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.97f);   // 환경설정/퀵바 창과 같은 검은 막
        dim.raycastTarget = true;                    // 뒤쪽 화면이 눌리지 않게

        var panelArt = UISpriteUtil.Load(ArtFolder + "SavePanel");
        if (panelArt != null)
        {
            var art = new GameObject("PanelArt", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(root.transform, false);
            var artRt = (RectTransform)art.transform;
            artRt.anchorMin = artRt.anchorMax = artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(1440f, 1080f);
            var artImg = art.GetComponent<Image>();
            artImg.sprite = panelArt;
            artImg.raycastTarget = false;
        }
        else
        {
            Debug.LogWarning("[SaveScreenUI] 세이브 화면 그림을 찾을 수 없어 색 도형으로 그립니다: Assets/Resources/" + ArtFolder + "SavePanel.png");
        }

        var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(root.transform, false);
        box = (RectTransform)boxGo.transform;
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        var boxImg = boxGo.GetComponent<Image>();
        boxImg.color = panelArt != null ? new Color(0f, 0f, 0f, 0f) : PanelColor;
        boxImg.raycastTarget = false;

        if (panelArt == null)
        {
            AddRect(box, "TopAccent", Pad, 0f, 120f, 3f, AccentColor);
            AddRect(box, "HeadDivider", Pad, 150f, BoxWidth - Pad * 2f, 1f, DividerColor);
            AddRect(box, "FootDivider", Pad, FooterY - 28f, BoxWidth - Pad * 2f, 1f, DividerColor);
        }

        var k = CreateText(box, "Kicker", kicker, 14, FontStyles.Bold, KickerColor);
        PlaceTopLeft(k.rectTransform, Pad, 40f, 400f, 20f);
        k.characterSpacing = 36f;

        var t = CreateText(box, "Title", title, 36, FontStyles.Bold, TitleColor);
        PlaceTopLeft(t.rectTransform, Pad, 56f, 600f, 54f);

        subtitle = CreateText(box, "Subtitle", "", 17, FontStyles.Normal, HintColor);
        PlaceTopLeft(subtitle.rectTransform, Pad, 110f, BoxWidth - Pad * 2f, 26f);
        return root;
    }

    // 아래쪽 버튼 (환경설정의 Button_Secondary 그림). x는 패널 왼쪽 기준.
    public static Button CreateButton(RectTransform box, string name, string label, float x, float width,
                                      UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(box, false);
        PlaceTopLeft((RectTransform)go.transform, x, FooterY, width, 54f);
        var bg = go.GetComponent<Image>();
        if (!UISpriteUtil.ApplySliced(bg, ButtonArtFolder + "Button_Secondary", 6f))
            bg.color = new Color(0.204f, 0.255f, 0.302f, 0.35f);

        var text = CreateText(go.transform, "Text", label, 19, FontStyles.Normal, ButtonTextColor);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.9f, 0.94f, 1f);
        colors.pressedColor = new Color(0.72f, 0.76f, 0.82f);
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    public static TMP_Text CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddRect(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        PlaceTopLeft((RectTransform)go.transform, x, y, w, h);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }
}

// 세이브 슬롯 한 줄. 상태(저장됨/빈 칸/잠김)와 마우스 올림에 따라 그림과 글자가 바뀐다.
public class SaveSlotRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public const float Width = SaveScreenUI.BoxWidth - SaveScreenUI.Pad * 2f;   // 848
    public const float Height = 116f;

    // 그림(848x116 @2x)과 화면 크기가 달라져도 왼쪽 번호 칸(구분선 포함 110px)과 테두리는 그대로 둔다.
    private static readonly Vector4 SlotBorder = new Vector4(220f, 12f, 12f, 12f);

    private static readonly Color IndexColor = new Color(0.561f, 0.643f, 0.718f);    // 8FA4B7
    private static readonly Color IndexEmptyColor = new Color(0.204f, 0.255f, 0.302f);// 34414D
    private static readonly Color ActiveColor = new Color(0.851f, 0.89f, 0.922f);    // D9E3EB
    private static readonly Color LabelColor = new Color(0.788f, 0.835f, 0.878f);    // C9D5E0
    private static readonly Color DimColor = new Color(0.424f, 0.486f, 0.545f);      // 6C7C8B
    private static readonly Color SlotColor = new Color(0.043f, 0.063f, 0.082f, 0.95f);
    private static readonly Color SlotHoverColor = new Color(0.094f, 0.133f, 0.176f, 0.95f);

    public enum State { Filled, Empty, Locked }

    private Button button;
    private Image background;
    private TMP_Text indexText, chapterText, subText, timeText, playText, emptyText, lockText, saveHereText;
    private Image lockIcon, plusIcon;

    private State state;
    private bool saveMode;      // 저장 화면이면 빈 칸에 "여기에 저장", 저장된 칸에 마우스를 올리면 "덮어써서 저장"
    private bool hovering;

    public Button Button => button;

    public static SaveSlotRow Create(RectTransform box, int index, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(box, false);
        SaveScreenUI.PlaceTopLeft((RectTransform)go.transform, SaveScreenUI.Pad,
                                  SaveScreenUI.SlotTop + index * SaveScreenUI.SlotStep, Width, Height);

        var row = go.AddComponent<SaveSlotRow>();
        row.background = go.GetComponent<Image>();
        row.background.raycastTarget = true;
        row.button = go.GetComponent<Button>();
        row.button.targetGraphic = row.background;
        row.button.transition = Selectable.Transition.None;   // 모양은 이 스크립트가 직접 바꾼다
        if (onClick != null) row.button.onClick.AddListener(onClick);

        var t = go.transform;
        row.indexText = SaveScreenUI.CreateText(t, "Index", (index + 1).ToString("00"), 34, FontStyles.Bold, IndexColor);
        SaveScreenUI.PlaceTopLeft(row.indexText.rectTransform, 32f, 32f, 64f, 48f);

        row.chapterText = SaveScreenUI.CreateText(t, "Chapter", "", 24, FontStyles.Bold, LabelColor);
        SaveScreenUI.PlaceTopLeft(row.chapterText.rectTransform, 132f, 26f, 380f, 36f);
        row.subText = SaveScreenUI.CreateText(t, "Sub", "", 15, FontStyles.Normal, SaveScreenUI.HintColor);
        SaveScreenUI.PlaceTopLeft(row.subText.rectTransform, 132f, 66f, 380f, 24f);

        row.timeText = SaveScreenUI.CreateText(t, "Timestamp", "", 17, FontStyles.Normal, LabelColor);
        SaveScreenUI.PlaceTopLeft(row.timeText.rectTransform, Width - 32f - 280f, 32f, 280f, 26f);
        row.timeText.alignment = TextAlignmentOptions.TopRight;
        row.playText = SaveScreenUI.CreateText(t, "PlayTime", "", 15, FontStyles.Normal, DimColor);
        SaveScreenUI.PlaceTopLeft(row.playText.rectTransform, Width - 32f - 280f, 64f, 280f, 22f);
        row.playText.alignment = TextAlignmentOptions.TopRight;

        row.emptyText = SaveScreenUI.CreateText(t, "Empty", "비어 있음", 22, FontStyles.Normal, DimColor);
        SaveScreenUI.PlaceTopLeft(row.emptyText.rectTransform, 132f, 40f, 300f, 36f);

        row.plusIcon = CreateIcon(t, "PlusIcon", "Icon_Plus", Width - 32f - 112f, 50f, 16f);
        row.saveHereText = SaveScreenUI.CreateText(t, "SaveHere", "여기에 저장", 16, FontStyles.Normal, DimColor);
        SaveScreenUI.PlaceTopLeft(row.saveHereText.rectTransform, Width - 32f - 90f, 45f, 90f, 26f);
        row.saveHereText.alignment = TextAlignmentOptions.TopRight;

        row.lockIcon = CreateIcon(t, "LockIcon", "Icon_Lock", 132f, 48f, 20f);
        row.lockText = SaveScreenUI.CreateText(t, "Locked", "아직 저장할 수 있는 지점을 지나지 않았습니다", 17, FontStyles.Normal, DimColor);
        SaveScreenUI.PlaceTopLeft(row.lockText.rectTransform, 162f, 45f, 600f, 28f);
        return row;
    }

    private static Image CreateIcon(Transform parent, string name, string sprite, float x, float y, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SaveScreenUI.PlaceTopLeft((RectTransform)go.transform, x, y, size, size);
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteUtil.Load(SaveScreenUI.ArtFolder + sprite);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = img.sprite != null;
        return img;
    }

    // data가 있으면 저장된 칸, 없으면 빈 칸. locked면 내용과 상관없이 잠긴 칸(누를 수 없음).
    public void Set(SaveData data, bool isSaveMode, bool locked, bool clickable)
    {
        saveMode = isSaveMode;
        state = locked ? State.Locked : data != null ? State.Filled : State.Empty;

        if (data != null)
        {
            chapterText.text = data.chapterId;
            timeText.text = data.timestamp;
            playText.text = "플레이  " + SavePointManager.FormatPlayTime(data.playTimeSeconds);
        }
        button.interactable = clickable && !locked;
        if (!button.interactable) hovering = false;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable) return;
        hovering = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        Apply();
    }

    private void OnDisable() => hovering = false;

    private void Apply()
    {
        bool filled = state == State.Filled;
        bool empty = state == State.Empty;
        bool locked = state == State.Locked;
        bool hover = hovering && !locked;

        string art = locked ? "SaveSlot_Locked"
            : filled ? (hover ? "SaveSlot_Hover" : "SaveSlot_Filled")
            : (hover ? "SaveSlot_EmptyHover" : "SaveSlot_Empty");
        if (!UISpriteUtil.ApplySliced(background, SaveScreenUI.ArtFolder + art, SlotBorder))
            background.color = hover ? SlotHoverColor : new Color(SlotColor.r, SlotColor.g, SlotColor.b, filled ? 0.95f : 0.55f);

        indexText.color = hover ? ActiveColor : filled ? IndexColor : IndexEmptyColor;

        chapterText.gameObject.SetActive(filled);
        subText.gameObject.SetActive(filled);
        timeText.gameObject.SetActive(filled);
        playText.gameObject.SetActive(filled);
        if (filled)
        {
            chapterText.color = hover ? SaveScreenUI.TitleColor : LabelColor;
            subText.text = saveMode && hover ? "덮어써서 저장합니다" : "마지막 저장 지점부터 이어집니다";
        }

        emptyText.gameObject.SetActive(empty);
        if (empty) emptyText.color = hover ? LabelColor : DimColor;

        bool showSaveHere = empty && saveMode;
        saveHereText.gameObject.SetActive(showSaveHere);
        if (plusIcon.sprite != null) plusIcon.gameObject.SetActive(showSaveHere);
        if (showSaveHere)
        {
            saveHereText.color = hover ? ActiveColor : DimColor;
            saveHereText.fontStyle = hover ? FontStyles.Bold : FontStyles.Normal;
            plusIcon.color = hover ? ActiveColor : DimColor;
        }

        lockText.gameObject.SetActive(locked);
        if (lockIcon.sprite != null) lockIcon.gameObject.SetActive(locked);
    }
}
