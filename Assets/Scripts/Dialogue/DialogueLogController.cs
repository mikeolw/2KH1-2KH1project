using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 대화 로그(백로그) - 위 화살표 키를 누르면 지금까지 나온 대사를 다시 볼 수 있는 창
// =====================================================================================
// ===== 언제 뜨나 =====
// 순수하게 대사가 진행 중일 때(암전 중이 아니고, 선택지/조사/미니게임/설정 등 다른 팝업이
// 안 떠 있을 때)만 위 화살표 키로 열린다 - DialogueSystem.CanOpenOverlay 참고. 열려 있는
// 동안은 Esc로 닫는다. 자동진행/스킵도 이 창이 열려 있는 동안은 멈춘다(DialogueSystem이
// IsBlockedByOtherUI()에서 이 창의 IsOpen을 같이 확인하기 때문).
//
// ===== 내용은 어디서 오나 =====
// 이 스크립트는 기록을 저장만 할 뿐, 언제 무엇을 남길지는 DialogueSystem이 정한다
// (DisplayLine()/ShowChoices() 참고). NormalDialogue/Narration 대사와, 플레이어가 고른
// 선택지만 남는다 - 조사/추리/미니게임 화면은 이미 따로 보여주고 있어서 로그에 같이
// 섞으면 문맥이 어색해지므로 뺐다.
//
// ===== 로그 범위 =====
// 세이브 파일 포맷을 안 건드리려고 로그는 그날 세션 한정이다. 새 시나리오 CSV를 불러올
// 때마다(챕터가 바뀌거나 세이브를 불러올 때) 통째로 비워지고 그 파일 안에서 다시 쌓인다
// (DialogueSystem.LoadDialogueFromCSV() 참고). 개수 제한은 없다 - 파일 하나 분량이라
// 애초에 몇백 줄을 넘기기 어렵다.
//
// ===== 왜 UI를 캔버스에 직접 만드나 =====
// NotePanelUI.cs/PhonePanelUI.cs와 같은 이유 - 씬에 미리 배치해둘 필요 없이 캔버스
// 바로 아래에 코드로 만들면 씬 구조와 무관하게 항상 같은 자리/크기로 뜬다.
//
// ===== 화면 구성 (Figma "Screen / Dialogue Log" - 청회색 테마) =====
//   [검은 막 + 패널 그림(LogPanel.png, 패널 x160 y100 1120x880)]
//     DIALOGUE LOG / 대화 기록                                 [X] ESC
//     ─────────────────────────────────────────────
//     화자 이름 │ 대사                               (줄마다 옅은 구분선)
//               │ 지문은 이름 칸을 비우고 흐린 색
//     ▌재훈     │ 가장 최근 대사는 바탕 + 왼쪽 막대로 강조
//     ─────────────────────────────────────────────
//     ↑ 키 또는 LOG 버튼으로 열기 · 마우스 휠로 넘겨 보기 · Esc로 닫기
// 그림은 Assets/Resources/Illusts/UI/Log/ 에 있다 (git 제외 - 드라이브 공유).
// 그림이 없으면 같은 자리에 색 도형으로 대신 그린다.
public class DialogueLogController : MonoBehaviour
{
    public static DialogueLogController Instance;

    private const string OverlayName = "__DialogueLogOverlay";
    private const string ArtFolder = "Illusts/UI/Log/";

    private GameObject overlay;
    private RectTransform listContent;
    private ScrollRect scrollRect;

    // 기록 한 줄. 화자가 없으면(지문) speaker가 빈 문자열.
    private struct Entry
    {
        public string speaker;
        public string sentence;
    }
    private readonly List<Entry> entries = new List<Entry>();

    // 화면에 만들어 둔 줄들 (다시 그릴 때 지운다)
    private readonly List<GameObject> rows = new List<GameObject>();
    private bool rowsDirty = true;

    // ===== 화면 크기 기준값 (패널 안 왼쪽 위 기준 px) =====
    private const float BoxWidth = 1120f;
    private const float BoxHeight = 880f;
    private const float Pad = 56f;
    private const float ListTop = 125f;          // 머리말 아래 구분선 바로 밑
    private const float ListBottom = 90f;        // 아래 안내 줄 위 구분선 (패널 아래에서부터)
    private const float ScrollBarSpace = 24f;    // 목록 오른쪽에 스크롤바 자리
    private const float NameWidth = 180f;
    private const float NameGap = 32f;

    // ===== 색 =====
    private static readonly Color KickerColor = new Color(0.435f, 0.518f, 0.588f);   // 6F8496
    private static readonly Color TitleColor = new Color(0.894f, 0.918f, 0.937f);    // E4EAEF
    private static readonly Color SpeakerColor = new Color(0.561f, 0.643f, 0.718f);  // 8FA4B7
    private static readonly Color SentenceColor = new Color(0.824f, 0.855f, 0.882f); // D2DAE1
    private static readonly Color NarrationColor = new Color(0.576f, 0.631f, 0.682f);// 93A1AE
    private static readonly Color LatestNameColor = new Color(0.851f, 0.89f, 0.922f);// D9E3EB
    private static readonly Color DividerColor = new Color(0.165f, 0.208f, 0.251f);  // 2A3540
    private static readonly Color HintColor = new Color(0.482f, 0.541f, 0.596f);     // 7B8A98
    private static readonly Color DimTextColor = new Color(0.424f, 0.486f, 0.545f);  // 6C7C8B
    private static readonly Color ButtonTextColor = new Color(0.663f, 0.729f, 0.788f);
    // 그림이 없을 때만 쓰는 색
    private static readonly Color PanelColor = new Color(0.063f, 0.086f, 0.114f, 0.95f);
    private static readonly Color LatestBgColor = new Color(0.094f, 0.133f, 0.176f);  // 18222D

    public bool IsOpen => overlay != null && overlay.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        BuildOverlay();
    }

    private void Update()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) &&
            DialogueSystem.Instance != null && DialogueSystem.Instance.CanOpenOverlay)
        {
            Open();
        }
    }

    // ---------------------------------------------------------------------------------
    // 기록 쌓기 / 비우기 (DialogueSystem이 부른다)
    // ---------------------------------------------------------------------------------

    public void AddEntry(string speaker, string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return;

        entries.Add(new Entry { speaker = speaker ?? "", sentence = sentence });
        rowsDirty = true;

        // 열려 있는 동안에도 새 대사가 쌓일 수 있다(자동진행 중 위 화살표 키를 누른 경우는
        // 이제 막혀 있지만, 만약을 대비해 열려 있으면 바로 반영해둔다).
        if (IsOpen) Refresh(scrollToBottom: true);
    }

    public void ClearLog()
    {
        entries.Clear();
        rowsDirty = true;
        if (IsOpen) Refresh(scrollToBottom: true);
    }

    // ---------------------------------------------------------------------------------
    // 열기 / 닫기
    // ---------------------------------------------------------------------------------

    public void Open()
    {
        if (overlay == null) return;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        Refresh(scrollToBottom: true);
    }

    public void Close()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    // 줄을 다시 만든다. 기록이 바뀌지 않았으면(그냥 다시 연 경우) 만들어 둔 것을 그대로 쓴다.
    private void Refresh(bool scrollToBottom)
    {
        if (listContent == null) return;

        if (rowsDirty)
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                row.transform.SetParent(null, false);   // 한 프레임 동안 옛 줄이 레이아웃에 남지 않게
                Destroy(row);
            }
            rows.Clear();

            if (entries.Count == 0)
            {
                rows.Add(CreateRow("", "아직 나온 대사가 없다.", latest: false));
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    rows.Add(CreateRow(entries[i].speaker, entries[i].sentence, latest: i == entries.Count - 1));
                }
            }
            rowsDirty = false;
        }

        if (scrollToBottom && scrollRect != null)
        {
            // Content 크기가 이번 프레임에 막 바뀐 상태라 한 프레임 늦게 반영되므로,
            // 강제로 레이아웃을 갱신한 뒤에 맨 아래로 내린다 (NotePanelUI.Refresh() 참고).
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // 기록 한 줄: [화자 이름 180px] [대사]. 줄 아래 옅은 구분선. 최근 줄은 바탕 + 왼쪽 막대.
    private GameObject CreateRow(string speaker, string sentence, bool latest)
    {
        bool narration = string.IsNullOrEmpty(speaker);

        var row = new GameObject(latest ? "Row (Latest)" : "Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(listContent, false);

        var bg = row.GetComponent<Image>();
        bg.raycastTarget = false;
        if (latest)
        {
            if (!UISpriteUtil.ApplySliced(bg, ArtFolder + "Log_LatestBg", 8f)) bg.color = LatestBgColor;
        }
        else
        {
            bg.color = new Color(0f, 0f, 0f, 0f);
        }

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(latest ? 16 : 0, 0, 18, 18);
        layout.spacing = NameGap;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var name = CreateText(row.transform, "Speaker", narration ? "" : speaker, 18, FontStyles.Bold,
                              latest ? LatestNameColor : SpeakerColor);
        var nameLe = name.gameObject.AddComponent<LayoutElement>();
        nameLe.minWidth = nameLe.preferredWidth = NameWidth - (latest ? 16f : 0f);
        name.textWrappingMode = TextWrappingModes.Normal;

        var body = CreateText(row.transform, "Sentence", sentence, 22, FontStyles.Normal,
                              narration ? NarrationColor : latest ? TitleColor : SentenceColor);
        body.lineSpacing = 18f;
        body.richText = true;
        var bodyLe = body.gameObject.AddComponent<LayoutElement>();
        bodyLe.flexibleWidth = 1f;

        // 구분선 (줄 아래쪽, 레이아웃에 끼지 않게)
        var line = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        line.transform.SetParent(row.transform, false);
        line.GetComponent<LayoutElement>().ignoreLayout = true;
        var lineRt = (RectTransform)line.transform;
        lineRt.anchorMin = new Vector2(0f, 0f);
        lineRt.anchorMax = new Vector2(1f, 0f);
        lineRt.pivot = new Vector2(0.5f, 0f);
        lineRt.offsetMin = Vector2.zero;
        lineRt.offsetMax = new Vector2(0f, 1f);
        var lineImg = line.GetComponent<Image>();
        lineImg.color = DividerColor;
        lineImg.raycastTarget = false;

        UIFontHelper.ApplyToChildren(row);
        return row;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color)
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
        return tmp;
    }

    // ---------------------------------------------------------------------------------
    // UI 만들기 (캔버스 바로 아래)
    // ---------------------------------------------------------------------------------
    private void BuildOverlay()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DialogueLogController] 씬에 Canvas가 없어 대화 로그 창을 만들 수 없습니다.");
            return;
        }

        // ----- 화면 전체를 덮는 검은 막 (환경설정/가방과 같은 톤) -----
        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.97f);
        dim.raycastTarget = true; // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 패널 그림 (1440x1080 한 장: 패널 + 위쪽 강조선 + 구분선 두 개) -----
        var panelArt = UISpriteUtil.Load(ArtFolder + "LogPanel");
        if (panelArt != null)
        {
            var art = new GameObject("PanelArt", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(overlay.transform, false);
            var artRt = (RectTransform)art.transform;
            artRt.anchorMin = artRt.anchorMax = artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(1440f, 1080f);
            var artImg = art.GetComponent<Image>();
            artImg.sprite = panelArt;
            artImg.raycastTarget = false;
        }
        else
        {
            Debug.LogWarning("[DialogueLogController] 대화 기록 그림을 찾을 수 없어 색 도형으로 그립니다: Assets/Resources/" + ArtFolder + "LogPanel.png");
        }

        // ----- 가운데 패널 (그림 속 패널 자리 = 화면 정가운데 1120x880) -----
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overlay.transform, false);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        var boxImg = box.GetComponent<Image>();
        boxImg.color = panelArt != null ? new Color(0f, 0f, 0f, 0f) : PanelColor;
        boxImg.raycastTarget = false;

        if (panelArt == null)
        {
            AddRect(box.transform, "TopAccent", Pad, 0f, 120f, 3f, SpeakerColor);
            AddRect(box.transform, "HeadDivider", Pad, ListTop - 1f, BoxWidth - Pad * 2f, 1f, DividerColor);
            AddRect(box.transform, "FootDivider", Pad, BoxHeight - ListBottom, BoxWidth - Pad * 2f, 1f, DividerColor);
        }

        // ----- 머리말 -----
        var kicker = CreateText(box.transform, "Kicker", "DIALOGUE LOG", 14, FontStyles.Bold, KickerColor);
        PlaceTopLeft(kicker.rectTransform, Pad, 40f, 400f, 20f);
        kicker.characterSpacing = 36f;

        var title = CreateText(box.transform, "Title", "대화 기록", 36, FontStyles.Bold, TitleColor);
        PlaceTopLeft(title.rectTransform, Pad, 56f, 400f, 54f);

        // ----- 닫기 (오른쪽 위) + ESC 표기 -----
        var close = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(box.transform, false);
        PlaceTopLeft((RectTransform)close.transform, BoxWidth - Pad - 48f, 44f, 48f, 48f);
        var closeImg = close.GetComponent<Image>();
        bool hasCloseArt = UISpriteUtil.ApplySliced(closeImg, ArtFolder + "Log_CloseBox", 6f);
        if (!hasCloseArt)
        {
            closeImg.color = new Color(0.204f, 0.255f, 0.302f, 0.6f);
            var x = CreateText(close.transform, "X", "X", 22, FontStyles.Normal, ButtonTextColor);
            Stretch(x.rectTransform);
            x.alignment = TextAlignmentOptions.Center;
        }
        var closeBtn = close.GetComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var colors = closeBtn.colors;
        colors.highlightedColor = new Color(0.9f, 0.94f, 1f);
        colors.pressedColor = new Color(0.72f, 0.76f, 0.82f);
        closeBtn.colors = colors;
        closeBtn.onClick.AddListener(Close);

        var esc = CreateText(box.transform, "EscHint", "ESC", 11, FontStyles.Bold, DimTextColor);
        PlaceTopLeft(esc.rectTransform, BoxWidth - Pad - 48f, 98f, 48f, 16f);
        esc.alignment = TextAlignmentOptions.Top;
        esc.characterSpacing = 15f;

        // ----- 목록 (스크롤) -----
        float listWidth = BoxWidth - Pad * 2f - ScrollBarSpace;
        float listHeight = BoxHeight - ListTop - ListBottom;
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(box.transform, false);
        var viewportRt = (RectTransform)viewport.transform;
        PlaceTopLeft(viewportRt, Pad, ListTop, listWidth, listHeight);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 투명하지만 마우스 휠 입력을 받는다

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        listContent = (RectTransform)content.transform;
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = new Vector2(0f, 100f);
        var vlayout = content.GetComponent<VerticalLayoutGroup>();
        vlayout.childControlWidth = true;
        vlayout.childControlHeight = true;
        vlayout.childForceExpandWidth = true;
        vlayout.childForceExpandHeight = false;
        vlayout.spacing = 0f;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 목록 위쪽을 패널 색으로 덮는 페이드 (더 오래된 기록이 위에 있다는 느낌)
        var fade = new GameObject("TopFade", typeof(RectTransform), typeof(Image));
        fade.transform.SetParent(viewport.transform, false);
        var fadeRt = (RectTransform)fade.transform;
        fadeRt.anchorMin = new Vector2(0f, 1f);
        fadeRt.anchorMax = new Vector2(1f, 1f);
        fadeRt.pivot = new Vector2(0.5f, 1f);
        fadeRt.sizeDelta = new Vector2(0f, 40f);
        fadeRt.anchoredPosition = Vector2.zero;
        var fadeImg = fade.GetComponent<Image>();
        fadeImg.sprite = UISpriteUtil.Load(ArtFolder + "Log_TopFade");
        fadeImg.enabled = fadeImg.sprite != null;
        fadeImg.raycastTarget = false;

        scrollRect = viewport.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = listContent;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        // ----- 스크롤바 (목록 오른쪽 가는 막대, 넘칠 때만 보인다) -----
        scrollRect.verticalScrollbar = CreateScrollbar(box.transform, BoxWidth - Pad - 4f, ListTop + 20f, listHeight - 40f);
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // ----- 아래 안내 -----
        var hint = CreateText(box.transform, "FootHint", "↑ 키 또는 LOG 버튼으로 열기  ·  마우스 휠로 넘겨 보기  ·  Esc로 닫기",
                              15, FontStyles.Normal, HintColor);
        PlaceTopLeft(hint.rectTransform, Pad, BoxHeight - 62f, BoxWidth - Pad * 2f, 24f);

        // ----- 글꼴 -----
        // 코드로 만든 글자는 기본 글꼴에 한글이 없어 깨지므로, 화면에서 한글이 잘 나오는
        // 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(overlay);

        overlay.SetActive(false);
    }

    private Scrollbar CreateScrollbar(Transform parent, float x, float y, float height)
    {
        var go = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        go.transform.SetParent(parent, false);
        PlaceTopLeft((RectTransform)go.transform, x, y, 4f, height);
        var track = go.GetComponent<Image>();
        track.color = new Color(0.204f, 0.255f, 0.302f, 0.6f);   // 34414D
        track.raycastTarget = true;

        var area = new GameObject("Sliding Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        Stretch((RectTransform)area.transform);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(area.transform, false);
        var handleRt = (RectTransform)handle.transform;
        handleRt.offsetMin = handleRt.offsetMax = Vector2.zero;
        var handleImg = handle.GetComponent<Image>();
        if (!UISpriteUtil.ApplySliced(handleImg, ArtFolder + "Log_ScrollThumb", 5f)) handleImg.color = SpeakerColor;

        var bar = go.GetComponent<Scrollbar>();
        bar.handleRect = handleRt;
        bar.targetGraphic = handleImg;
        bar.direction = Scrollbar.Direction.BottomToTop;
        return bar;
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
