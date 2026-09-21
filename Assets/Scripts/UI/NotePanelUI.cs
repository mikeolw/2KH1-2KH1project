using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 조사기록(수첩) 화면 - 재훈이 조사하면서 적어나가는 메모를 양면 다이어리로 보여준다
// =====================================================================================
// 퀵바의 Note 버튼을 누르면 열리는 탭이다.
//
// ===== 화면 구성 =====
//   왼쪽 바깥 : 성격별 세로 탭 4개 (사건경과 / 증거 / 증언 / 업무수첩)
//   왼쪽 페이지 : 그 탭에 속한 메모 목록. 챕터(예: #01)로 묶어서 보여준다.
//   오른쪽 페이지 : 목록에서 고른 메모의 제목과 본문.
//
// 어느 탭에 넣을지와 목록에 띄울 제목은 NoteCatalog가 정한다. 이 스크립트는 그리기만 한다.
//
// ===== 왜 UI를 캔버스에 직접 만드나 (중요) =====
// 처음에는 씬의 NotePanel 안에 메모 UI를 만들었다. 그런데 그 패널은 프로토타입 시절
// 크기와 위치가 제각각으로 잡혀 있고 안에 옛날 오브젝트도 남아 있어서, 코드에서 크기를
// 다시 잡아도 화면 구석에 작게 뜨거나 글자가 잘려 아무것도 안 보였다.
//
// 그래서 메모 화면을 씬의 패널 안이 아니라 캔버스 바로 아래에 따로 만든다. 씬이 어떻게
// 짜여 있든 영향을 받지 않으므로 항상 같은 자리에 같은 크기로 뜬다.
// 씬의 NotePanel은 "열렸는지 닫혔는지"를 알려주는 스위치 역할만 한다
// (UIManager가 그 패널을 켜고 끄므로, 이 스크립트는 그때 맞춰 메모 화면을 보여준다).
public class NotePanelUI : MonoBehaviour
{
    // 캔버스 아래에 만드는 메모 화면의 이름.
    private const string OverlayName = "__NoteOverlay";

    // ----- 색 (아트 에셋이 없어서 색 도형으로만 그린다) -----
    private static readonly Color PaperColor = new Color(0.95f, 0.92f, 0.84f, 1f);
    private static readonly Color InkColor = new Color(0.16f, 0.13f, 0.09f);
    private static readonly Color FadedInkColor = new Color(0.42f, 0.35f, 0.26f);
    private static readonly Color LineColor = new Color(0.55f, 0.45f, 0.32f, 0.7f);
    private static readonly Color SpineColor = new Color(0.28f, 0.21f, 0.13f, 1f);
    private static readonly Color TabIdleColor = new Color(0.72f, 0.66f, 0.55f, 1f);
    private static readonly Color TabActiveColor = PaperColor;
    private static readonly Color RowSelectedColor = new Color(0.82f, 0.74f, 0.58f, 1f);

    private GameObject overlay;

    private readonly List<Button> tabButtons = new List<Button>();
    private RectTransform listContent;      // 왼쪽 페이지에 줄을 쌓는 자리
    private ScrollRect listScroll;
    private ScrollRect detailScroll;
    private TMP_Text detailTitleText;
    private TMP_Text detailBodyText;

    // 지금 보고 있는 탭과 고른 메모.
    private string currentCategory = NoteCatalog.Tabs[0];
    private string selectedEntryId;

    // 목록에 만들어둔 줄 버튼들. 다시 그릴 때 지우려고 들고 있는다.
    private readonly List<GameObject> listRows = new List<GameObject>();

    private void Awake()
    {
        HideOriginalPanelVisuals();
        BuildOverlay();
    }

    private void OnEnable()
    {
        // 어떤 이유로든 NoteManager가 없으면 여기서 만들어 확보한다.
        if (NoteManager.Instance == null)
        {
            new GameObject("NoteManager").AddComponent<NoteManager>();
        }

        if (NoteManager.Instance != null)
        {
            NoteManager.Instance.OnNoteChanged -= Refresh;
            NoteManager.Instance.OnNoteChanged += Refresh;
        }

        if (overlay == null) BuildOverlay();

        if (overlay != null)
        {
            overlay.SetActive(true);
            // 다른 UI에 가리지 않도록 항상 맨 앞으로.
            overlay.transform.SetAsLastSibling();
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (NoteManager.Instance != null)
        {
            NoteManager.Instance.OnNoteChanged -= Refresh;
        }

        if (overlay != null) overlay.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 내용 그리기
    // ---------------------------------------------------------------------------------
    public void Refresh()
    {
        if (listContent == null) return;

        UpdateTabVisuals();
        RebuildList();
    }

    // 지금 탭에 속한 메모를 챕터별로 묶어 왼쪽 페이지에 쌓는다.
    private void RebuildList()
    {
        foreach (var row in listRows)
        {
            if (row == null) continue;

            // 부모에서 먼저 떼어낸 뒤에 지운다. Destroy()는 이번 프레임이 끝날 때 실제로
            // 지워지기 때문에, 그냥 지우면 아래에서 새로 만든 줄과 옛 줄이 한 프레임 동안
            // 같이 남아 VerticalLayoutGroup이 두 배 높이로 잡히며 목록이 덜컥거린다.
            row.transform.SetParent(null, false);
            Destroy(row);
        }
        listRows.Clear();

        var all = NoteManager.Instance != null
            ? NoteManager.Instance.GetRecordedEntriesSorted()
            : new List<NoteManager.NoteEntry>();
        var entries = NoteCatalog.EntriesIn(all, currentCategory);

        if (entries.Count == 0)
        {
            AddChapterHeading("아직 적어둔 것이 없다.");
            ShowDetail(null);
            return;
        }

        // 고른 메모가 이 탭에 없으면(탭을 막 바꿨을 때) 첫 줄을 대신 고른다.
        bool selectionInThisTab = false;
        foreach (var entry in entries)
        {
            if (entry.entryId == selectedEntryId) { selectionInThisTab = true; break; }
        }
        if (!selectionInThisTab) selectedEntryId = entries[0].entryId;

        string lastChapter = null;
        foreach (var entry in entries)
        {
            if (entry.chapter != lastChapter)
            {
                AddChapterHeading(entry.chapter);
                lastChapter = entry.chapter;
            }

            AddEntryRow(entry);
            if (entry.entryId == selectedEntryId) ShowDetail(entry);
        }

        // 줄을 새로 만들었으니 스크롤을 맨 위로 되돌린다.
        if (listScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            listScroll.verticalNormalizedPosition = 1f;
        }
    }

    // 목록 사이에 들어가는 챕터 소제목 (CSV의 Chapter 칸).
    private void AddChapterHeading(string chapter)
    {
        var go = new GameObject("Chapter", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 44f;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = chapter;
        text.fontSize = 26;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.color = FadedInkColor;
        text.raycastTarget = false;
        UIFontHelper.Apply(text);

        listRows.Add(go);
    }

    // 목록의 한 줄. 누르면 오른쪽 페이지가 그 메모로 바뀐다.
    private void AddEntryRow(NoteManager.NoteEntry entry)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(Image),
                                typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 46f;

        var bg = go.GetComponent<Image>();
        bool selected = entry.entryId == selectedEntryId;
        bg.color = selected ? RowSelectedColor : new Color(1f, 1f, 1f, 0.01f);
        bg.raycastTarget = true;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(24f, 0f);
        labelRt.offsetMax = new Vector2(-12f, 0f);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "· " + NoteCatalog.TitleOf(entry);
        label.fontSize = 23;
        label.alignment = TextAlignmentOptions.Left;
        label.color = InkColor;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        UIFontHelper.Apply(label);

        string id = entry.entryId;
        var button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() =>
        {
            selectedEntryId = id;
            RebuildList();
        });

        listRows.Add(go);
    }

    // 오른쪽 페이지에 메모 하나를 펼친다. null이면 비운다.
    private void ShowDetail(NoteManager.NoteEntry entry)
    {
        if (detailTitleText == null || detailBodyText == null) return;

        detailTitleText.text = entry != null ? NoteCatalog.TitleOf(entry) : "";
        detailBodyText.text = entry != null ? NoteCatalog.BodyOf(entry) : "";

        UIFontHelper.Apply(detailTitleText);
        UIFontHelper.Apply(detailBodyText);

        if (detailScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            detailScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabButtons.Count && i < NoteCatalog.Tabs.Length; i++)
        {
            var image = tabButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = NoteCatalog.Tabs[i] == currentCategory ? TabActiveColor : TabIdleColor;
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 씬에 있던 원래 패널은 안 보이게 한다
    // ---------------------------------------------------------------------------------
    // 메모 화면을 캔버스에 따로 만들기 때문에, 씬의 NotePanel 자체는 눈에 보이면 안 된다.
    // 다만 UIManager가 이 패널을 켜고 끄면서 여닫음을 관리하므로 오브젝트 자체는 남겨둔다.
    private void HideOriginalPanelVisuals()
    {
        var img = GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------------------------
    // 메모 화면 만들기 (캔버스 바로 아래)
    // ---------------------------------------------------------------------------------
    private void BuildOverlay()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[NotePanelUI] 씬에 Canvas가 없어 수첩 화면을 만들 수 없습니다.");
            return;
        }

        // 이미 만들어져 있으면 지우고 새로 만든다. 예전 구조(글 덩어리 하나)가 남아 있으면
        // 자리만 차지하고 쓸 수 없기 때문이다.
        //
        // Destroy()가 아니라 DestroyImmediate()를 쓰는 이유: Destroy()는 이번 프레임이
        // 끝날 때 지워지므로, 바로 아래에서 같은 이름으로 새로 만들면 한 프레임 동안
        // __NoteOverlay가 두 개 존재하게 되고 다음번 Find()가 옛것을 집을 수 있다.
        // 여기는 Awake에서 한 번 도는 정리 코드라 즉시 지워도 안전하다.
        var existing = canvas.transform.Find(OverlayName);
        if (existing != null) DestroyImmediate(existing.gameObject);

        tabButtons.Clear();
        listRows.Clear();

        // ----- 화면 전체를 덮는 막 -----
        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 펼친 책 -----
        var book = new GameObject("Book", typeof(RectTransform), typeof(Image));
        book.transform.SetParent(overlay.transform, false);
        var bookRt = book.GetComponent<RectTransform>();
        bookRt.anchorMin = new Vector2(0.5f, 0.5f);
        bookRt.anchorMax = new Vector2(0.5f, 0.5f);
        bookRt.pivot = new Vector2(0.5f, 0.5f);
        bookRt.sizeDelta = new Vector2(1520f, 880f);
        bookRt.anchoredPosition = Vector2.zero;
        book.GetComponent<Image>().color = PaperColor;

        BuildTabs(book.transform);
        BuildSpine(book.transform);
        BuildLeftPage(book.transform);
        BuildRightPage(book.transform);
        BuildCloseButton(book.transform);

        // ----- 글꼴 -----
        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨지므로,
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(overlay);

        overlay.SetActive(false);
    }

    // 책 왼쪽 바깥에 세로로 붙는 탭 4개.
    private void BuildTabs(Transform bookTransform)
    {
        const float tabWidth = 150f;
        const float tabHeight = 56f;
        const float gap = 8f;

        for (int i = 0; i < NoteCatalog.Tabs.Length; i++)
        {
            string category = NoteCatalog.Tabs[i];

            var go = new GameObject("Tab_" + category, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(bookTransform, false);
            // 책의 왼쪽 위 모서리를 기준으로 아래로 쌓는다.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 1f);   // 책 왼쪽 바깥으로 나가도록
            rt.sizeDelta = new Vector2(tabWidth, tabHeight);
            rt.anchoredPosition = new Vector2(0f, -60f - i * (tabHeight + gap));

            var bg = go.GetComponent<Image>();
            bg.color = TabIdleColor;
            bg.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Stretch(labelGo.GetComponent<RectTransform>());
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = category;
            label.fontSize = 22;
            label.alignment = TextAlignmentOptions.Center;
            label.color = InkColor;
            label.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() =>
            {
                currentCategory = category;
                // 탭을 바꾸면 고른 메모를 비운다. RebuildList()가 그 탭의 첫 줄을 골라준다.
                selectedEntryId = null;
                Refresh();
            });

            tabButtons.Add(button);
        }
    }

    // 가운데 접힘선.
    private void BuildSpine(Transform bookTransform)
    {
        var go = new GameObject("Spine", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(bookTransform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(20f, 0f);
        rt.offsetMin = new Vector2(rt.offsetMin.x, 40f);
        rt.offsetMax = new Vector2(rt.offsetMax.x, -40f);

        var img = go.GetComponent<Image>();
        img.color = SpineColor;
        img.raycastTarget = false;
    }

    // 왼쪽 페이지: 세로로 줄을 쌓는 스크롤 목록.
    private void BuildLeftPage(Transform bookTransform)
    {
        var viewport = new GameObject("ListViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(bookTransform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0f, 0f);
        viewportRt.anchorMax = new Vector2(0.5f, 1f);
        viewportRt.offsetMin = new Vector2(40f, 90f);    // 아래는 닫기 버튼 자리
        viewportRt.offsetMax = new Vector2(-20f, -40f);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);   // 스크롤 입력만 받는다

        var content = new GameObject("ListContent", typeof(RectTransform),
                                     typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        listContent = content.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = new Vector2(0f, 100f);

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.spacing = 2f;

        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        listScroll = viewport.AddComponent<ScrollRect>();
        listScroll.viewport = viewportRt;
        listScroll.content = listContent;
        listScroll.horizontal = false;
        listScroll.vertical = true;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = 40f;
    }

    // 오른쪽 페이지: 제목 + 구분선 + 본문(스크롤).
    private void BuildRightPage(Transform bookTransform)
    {
        var titleGo = new GameObject("DetailTitle", typeof(RectTransform));
        titleGo.transform.SetParent(bookTransform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(20f, 0f);
        titleRt.offsetMax = new Vector2(-40f, 0f);
        titleRt.sizeDelta = new Vector2(titleRt.sizeDelta.x, 56f);
        titleRt.anchoredPosition = new Vector2(0f, -40f);

        detailTitleText = titleGo.AddComponent<TextMeshProUGUI>();
        detailTitleText.text = "";
        detailTitleText.fontSize = 32;
        detailTitleText.fontStyle = FontStyles.Bold;
        detailTitleText.alignment = TextAlignmentOptions.Left;
        detailTitleText.color = InkColor;
        detailTitleText.raycastTarget = false;

        var line = new GameObject("DetailDivider", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(bookTransform, false);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.5f, 1f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.pivot = new Vector2(0.5f, 1f);
        lineRt.offsetMin = new Vector2(20f, 0f);
        lineRt.offsetMax = new Vector2(-40f, 0f);
        lineRt.sizeDelta = new Vector2(lineRt.sizeDelta.x, 2f);
        lineRt.anchoredPosition = new Vector2(0f, -100f);
        var lineImg = line.GetComponent<Image>();
        lineImg.color = LineColor;
        lineImg.raycastTarget = false;

        var viewport = new GameObject("DetailViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(bookTransform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0.5f, 0f);
        viewportRt.anchorMax = new Vector2(1f, 1f);
        viewportRt.offsetMin = new Vector2(20f, 90f);
        viewportRt.offsetMax = new Vector2(-40f, -114f);   // 위는 제목과 구분선 자리
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        var content = new GameObject("DetailContent", typeof(RectTransform), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        // 높이를 미리 잡아둔다. 0으로 두면 글이 들어가도 잘려서 안 보인다.
        // 실제 높이는 ContentSizeFitter가 글 길이에 맞춰 다시 계산한다.
        contentRt.sizeDelta = new Vector2(0f, 400f);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        detailBodyText = content.AddComponent<TextMeshProUGUI>();
        detailBodyText.text = "";
        detailBodyText.fontSize = 24;
        detailBodyText.alignment = TextAlignmentOptions.TopLeft;
        detailBodyText.color = InkColor;
        detailBodyText.lineSpacing = 8f;
        detailBodyText.raycastTarget = false;
        detailBodyText.richText = true;
        detailBodyText.overflowMode = TextOverflowModes.Overflow;   // 길어지면 아래로 이어진다

        detailScroll = viewport.AddComponent<ScrollRect>();
        detailScroll.viewport = viewportRt;
        detailScroll.content = contentRt;
        detailScroll.horizontal = false;
        detailScroll.vertical = true;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.scrollSensitivity = 40f;
    }

    private void BuildCloseButton(Transform bookTransform)
    {
        var go = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(bookTransform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta = new Vector2(200f, 50f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.35f, 0.28f, 0.18f, 0.85f);
        bg.raycastTarget = true;

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        Stretch(labelGo.GetComponent<RectTransform>());
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "닫기";
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.94f, 0.88f);
        label.raycastTarget = false;

        var button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        // 수첩을 닫는다 = 씬의 NotePanel을 끄는 것(UIManager가 그 상태로 여닫음을 판단한다)
        button.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
