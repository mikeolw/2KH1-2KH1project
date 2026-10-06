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

    // ----- 색 (가죽 수사 수첩 v2 - 청회색 테마, Figma "Screen / Note v2") -----
    private static readonly Color PaperColor = new Color(0.929f, 0.922f, 0.898f, 1f);   // EDEBE5 (그림이 없을 때만 쓰는 종이색)
    private static readonly Color CoverColor = new Color(0.157f, 0.196f, 0.239f, 1f);   // 28323D (그림이 없을 때만)
    private static readonly Color InkColor = new Color(0.114f, 0.141f, 0.169f);         // 1D242B 본문 잉크
    private static readonly Color FadedInkColor = new Color(0.42f, 0.471f, 0.518f);     // 6B7884 흐린 잉크
    private static readonly Color AccentColor = new Color(0.298f, 0.388f, 0.467f);      // 4C6377 청회색 강조
    private static readonly Color RuleColor = new Color(0.114f, 0.141f, 0.169f, 0.4f);  // 구분선
    private static readonly Color SpineColor = new Color(0.114f, 0.141f, 0.169f, 0.25f);
    private static readonly Color TabIdleColor = new Color(0.79f, 0.8f, 0.8f, 1f);      // 그림이 없을 때만
    private static readonly Color TabIdleInkColor = new Color(0.31f, 0.365f, 0.412f);   // 4F5D69
    private static readonly Color TabIdleIndexColor = new Color(0.494f, 0.541f, 0.58f); // 7E8A94
    private static readonly Color RowNumberColor = new Color(0.604f, 0.643f, 0.678f);   // 9AA4AD
    private static readonly Color RowSelectedColor = new Color(0.561f, 0.643f, 0.718f, 0.32f);   // 8FA4B7 형광펜 느낌
    private static readonly Color RowIdleColor = new Color(1f, 1f, 1f, 0.01f);          // 클릭만 받는 거의 투명

    // 노트 그림 경로 (Resources 기준, 확장자 없음).
    //   NoteBook_v2    : 1440x1080 투명 PNG. 책 + 기본 상태 탭 4개가 그려져 있다.
    //   NoteTab_Active : 176x80. 고른 탭 자리에 책 위로 얹는 종이색 탭.
    private const string BookSpritePath = "Illusts/UI/NoteBook_v2";
    private const string ActiveTabSpritePath = "Illusts/UI/NoteTab_Active";
    private bool hasBookArt;
    private Sprite activeTabSprite;

    private GameObject overlay;

    // 탭 하나를 그리는 데 필요한 것들. UpdateTabVisuals가 고른 탭/안 고른 탭 모양을 바꾼다.
    private class TabView
    {
        public Button button;
        public Image art;        // 고른 탭 그림 (그림이 없을 때는 색 도형)
        public RectTransform index;
        public RectTransform label;
        public TMP_Text indexText;
        public TMP_Text labelText;
    }
    private readonly List<TabView> tabViews = new List<TabView>();

    // 목록 한 줄. 고를 때 배경색/굵기/화살표만 바꾸려고 들고 있는다.
    private class RowView
    {
        public Image background;
        public TMP_Text number;
        public TMP_Text label;
        public GameObject arrow;
    }

    // 왼쪽 페이지 머리말 (EVIDENCE / 증거) 과 오른쪽 페이지 분류 표기.
    private TMP_Text pageKickerText;
    private TMP_Text pageTitleText;
    private TMP_Text detailMetaText;
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

    // 각 줄의 배경 Image를 entryId로 찾기 위한 사전. 선택만 바뀔 때(SelectEntry) 목록
    // 전체를 다시 만들지 않고 배경색만 다시 칠하는 데 쓴다. RebuildList가 새로 채운다.
    private readonly Dictionary<string, RowView> rowViews = new Dictionary<string, RowView>();

    // 지금 탭에 속한 메모 목록. SelectEntry가 선택된 entryId로 NoteEntry를 다시 찾을 때 쓴다.
    private List<NoteManager.NoteEntry> currentEntries = new List<NoteManager.NoteEntry>();

    // ShowDetail()이 스크롤을 되돌려야 하는지 판단하려고 마지막으로 보여준 메모의 id를
    // 기억해둔다. 실제 존재할 수 없는 값으로 시작해서 "아직 한 번도 안 보여줌"을 구분한다.
    private const string UnsetDetailId = "__unset_detail_id__";
    private string lastShownDetailEntryId = UnsetDetailId;

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
        UpdatePageHeader();
        RebuildList();
    }

    // 탭마다 왼쪽 페이지 머리말 위에 작게 붙는 영문 표기.
    private static string KickerOf(string category)
    {
        switch (category)
        {
            case NoteCatalog.CategoryProgress: return "CASE LOG";
            case NoteCatalog.CategoryEvidence: return "EVIDENCE";
            case NoteCatalog.CategoryTestimony: return "TESTIMONY";
            case NoteCatalog.CategoryWorkNote: return "WORK NOTES";
            default: return "";
        }
    }

    private void UpdatePageHeader()
    {
        if (pageKickerText != null) pageKickerText.text = KickerOf(currentCategory);
        if (pageTitleText != null)
        {
            pageTitleText.text = currentCategory;
            UIFontHelper.Apply(pageTitleText);
        }
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
        rowViews.Clear();

        var all = NoteManager.Instance != null
            ? NoteManager.Instance.GetRecordedEntriesSorted()
            : new List<NoteManager.NoteEntry>();
        var entries = NoteCatalog.EntriesIn(all, currentCategory);
        currentEntries = entries;

        if (entries.Count == 0)
        {
            AddEmptyNotice("아직 적어둔 것이 없다.");
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
        int number = 0;
        foreach (var entry in entries)
        {
            if (entry.chapter != lastChapter)
            {
                AddChapterHeading(entry.chapter);
                lastChapter = entry.chapter;
            }

            number++;
            AddEntryRow(entry, number);
            if (entry.entryId == selectedEntryId) ShowDetail(entry);
        }

        // 줄을 새로 만들었으니 스크롤을 맨 위로 되돌린다.
        if (listScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            listScroll.verticalNormalizedPosition = 1f;
        }
    }

    // 목록 사이에 들어가는 챕터 소제목 (CSV의 Chapter 칸). "#03 경찰서 ────" 처럼
    // 청회색 작은 글씨 뒤로 남은 폭만큼 옅은 선을 긋는다.
    private void AddChapterHeading(string chapter)
    {
        var go = new GameObject("Chapter", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 44f;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = chapter;
        text.fontSize = 18;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 10f;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.color = AccentColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UIFontHelper.Apply(text);
        float textWidth = text.GetPreferredValues(chapter).x;

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(0f, 1f);
        textRt.pivot = new Vector2(0f, 0f);
        textRt.anchoredPosition = new Vector2(0f, 6f);
        textRt.sizeDelta = new Vector2(textWidth + 4f, -6f);

        var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(go.transform, false);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0f);
        lineRt.anchorMax = new Vector2(1f, 0f);
        lineRt.pivot = new Vector2(0f, 0f);
        lineRt.offsetMin = new Vector2(textWidth + 14f, 16f);
        lineRt.offsetMax = new Vector2(0f, 17f);
        var lineImg = line.GetComponent<Image>();
        lineImg.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
        lineImg.raycastTarget = false;

        listRows.Add(go);
    }

    // 항목이 하나도 없는 탭에 띄우는 안내문.
    private void AddEmptyNotice(string message)
    {
        var go = new GameObject("EmptyNotice", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 44f;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = 23;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = FadedInkColor;
        text.raycastTarget = false;
        UIFontHelper.Apply(text);

        listRows.Add(go);
    }

    // 목록의 한 줄: [번호] 제목 ........ [▸(고른 줄만)]. 누르면 오른쪽 페이지가 그 메모로 바뀐다.
    private void AddEntryRow(NoteManager.NoteEntry entry, int number)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(Image),
                                typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 50f;

        var view = new RowView();
        view.background = go.GetComponent<Image>();
        view.background.raycastTarget = true;

        // 번호 칸: 왼쪽 10px에서 36px 폭
        view.number = AddRowText(go.transform, "Number", number.ToString("00"), 16, 10f, 36f, 0f);
        view.number.fontStyle = FontStyles.Bold;

        // 제목 칸: 56px부터 오른쪽 화살표 자리(36px)를 남기고 끝까지
        view.label = AddRowText(go.transform, "Label", NoteCatalog.TitleOf(entry), 24, 56f, 0f, -36f);
        view.label.overflowMode = TextOverflowModes.Ellipsis;
        view.label.textWrappingMode = TextWrappingModes.NoWrap;
        view.label.color = InkColor;

        // 고른 줄 오른쪽 끝의 ▸
        var arrow = AddRowText(go.transform, "Arrow", "▸", 20, 0f, 0f, 0f);
        var arrowRt = arrow.rectTransform;
        arrowRt.anchorMin = new Vector2(1f, 0f);
        arrowRt.anchorMax = new Vector2(1f, 1f);
        arrowRt.pivot = new Vector2(1f, 0.5f);
        arrowRt.offsetMin = new Vector2(-30f, 0f);
        arrowRt.offsetMax = new Vector2(-10f, 0f);
        arrow.fontStyle = FontStyles.Bold;
        arrow.color = AccentColor;
        arrow.alignment = TextAlignmentOptions.Right;
        view.arrow = arrow.gameObject;

        string id = entry.entryId;
        var button = go.GetComponent<Button>();
        button.targetGraphic = view.background;
        button.onClick.AddListener(() => SelectEntry(id));

        rowViews[id] = view;
        ApplyRowVisual(view, id == selectedEntryId);
        listRows.Add(go);
    }

    // 줄 안의 글자 하나. width가 0보다 크면 left부터 그 폭만큼, 0이면 left부터 오른쪽 끝(rightOffset)까지.
    private TMP_Text AddRowText(Transform parent, string name, string value, float size, float left, float width, float rightOffset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (width > 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(left, 0f);
            rt.sizeDelta = new Vector2(width, 0f);
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(rightOffset, 0f);
        }

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        UIFontHelper.Apply(text);
        return text;
    }

    // 고른 줄: 형광펜 같은 옅은 청회색 바탕 + 굵게 + 번호 강조색 + ▸ 표시.
    private void ApplyRowVisual(RowView view, bool selected)
    {
        view.background.color = selected ? RowSelectedColor : RowIdleColor;
        view.label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        view.number.color = selected ? AccentColor : RowNumberColor;
        view.arrow.SetActive(selected);
    }

    // 행을 클릭했을 때 부른다. RebuildList()를 부르지 않고 선택 표시와 오른쪽
    // 페이지만 갱신한다. RebuildList()를 부르면 목록 스크롤이 맨 위로 되감겨서, 아래로
    // 스크롤한 뒤 고른 항목이 화면 밖으로 사라지는 문제가 있었다.
    private void SelectEntry(string id)
    {
        selectedEntryId = id;

        foreach (var pair in rowViews)
        {
            ApplyRowVisual(pair.Value, pair.Key == id);
        }

        foreach (var entry in currentEntries)
        {
            if (entry.entryId == id)
            {
                ShowDetail(entry);
                break;
            }
        }
    }

    // 오른쪽 페이지에 메모 하나를 펼친다. null이면 비운다.
    private void ShowDetail(NoteManager.NoteEntry entry)
    {
        if (detailTitleText == null || detailBodyText == null) return;

        detailTitleText.text = entry != null ? NoteCatalog.TitleOf(entry) : "";
        detailBodyText.text = entry != null ? NoteCatalog.BodyOf(entry) : "";
        if (detailMetaText != null)
        {
            // "증거  ·  #03 경찰서" (챕터가 비어 있으면 탭 이름만)
            detailMetaText.text = entry == null ? ""
                : string.IsNullOrEmpty(entry.chapter) ? currentCategory
                : currentCategory + "  ·  " + entry.chapter;
            UIFontHelper.Apply(detailMetaText);
        }

        UIFontHelper.Apply(detailTitleText);
        UIFontHelper.Apply(detailBodyText);

        // 고른 메모가 실제로 바뀌었을 때만 스크롤을 맨 위로 되돌린다. 그러지 않으면
        // 수첩을 열어둔 채 새 메모가 추가돼 OnNoteChanged -> Refresh -> RebuildList ->
        // ShowDetail(같은 항목)이 돌 때마다 읽던 위치를 잃는다.
        string id = entry != null ? entry.entryId : null;
        bool changed = id != lastShownDetailEntryId;
        lastShownDetailEntryId = id;

        if (changed && detailScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            detailScroll.verticalNormalizedPosition = 1f;
        }
    }

    // 고른 탭: 종이색 탭 그림을 책 위로 얹고(더 튀어나온 모양) 글자를 굵게.
    // 안 고른 탭: 그림 속 기본 탭 위에 글자만 놓는다.
    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabViews.Count && i < NoteCatalog.Tabs.Length; i++)
        {
            var tab = tabViews[i];
            bool active = NoteCatalog.Tabs[i] == currentCategory;

            // 글자는 탭 왼쪽 끝에서 20px 안쪽. 고른 탭은 왼쪽으로 20px 더 튀어나와 있다.
            float tabLeft = active ? 0f : TabIdleX - TabActiveX;
            tab.index.anchoredPosition = new Vector2(tabLeft + TabTextInset, -12f);
            tab.label.anchoredPosition = new Vector2(tabLeft + TabTextInset, -32f);

            tab.indexText.color = active ? AccentColor : TabIdleIndexColor;
            tab.labelText.color = active ? InkColor : TabIdleInkColor;
            tab.labelText.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;

            if (hasBookArt)
            {
                tab.art.enabled = active && activeTabSprite != null;
            }
            else
            {
                // 그림이 없을 때: 색 도형으로 탭을 그린다.
                tab.art.enabled = true;
                tab.art.color = active ? PaperColor : TabIdleColor;
                var artRt = tab.art.rectTransform;
                artRt.anchoredPosition = new Vector2(tabLeft, 0f);
                artRt.sizeDelta = new Vector2(active ? TabActiveWidth : TabIdleWidth, TabHeight);
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

        tabViews.Clear();
        listRows.Clear();
        rowViews.Clear();
        lastShownDetailEntryId = UnsetDetailId;

        // ----- 화면 전체를 덮는 막 -----
        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.97f);   // 환경설정/가방과 같은 검은 막
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 펼친 책 -----
        var book = new GameObject("Book", typeof(RectTransform), typeof(Image));
        book.transform.SetParent(overlay.transform, false);
        var bookRt = book.GetComponent<RectTransform>();
        bookRt.anchorMin = new Vector2(0.5f, 0.5f);
        bookRt.anchorMax = new Vector2(0.5f, 0.5f);
        bookRt.pivot = new Vector2(0.5f, 0.5f);
        // ===== 크기는 캔버스 기준 해상도(1440x1080, 4:3)에 맞춰 잡는다 =====
        // 이 게임의 그림이 전부 4:3으로 그려져 있어 CanvasScaler 기준 해상도가 1440x1080이다
        // (AspectRatioKeeper.cs 참고). 노트 그림(NoteBook_v2.png)도 캔버스와 같은 1440x1080
        // 한 장이라 책을 캔버스 전체 크기로 깔고, 탭/페이지 위치는 그림 속 좌표(왼쪽 위 기준 px)로 잡는다.
        // 그림이 없으면(구글 드라이브에서 아직 안 받은 사람) 색 도형 책으로 대신 그린다.
        bookRt.sizeDelta = new Vector2(1440f, 1080f);
        bookRt.anchoredPosition = Vector2.zero;
        // 화면을 꽉 채우면 답답해 보여서 책 전체(탭/글자 포함)를 가운데 기준으로 살짝 줄인다.
        // 좌표는 계속 1440x1080 그림 기준 그대로 쓰고, 크기만 BookScale로 줄어든다.
        bookRt.localScale = new Vector3(BookScale, BookScale, 1f);
        var bookSprite = Resources.Load<Sprite>(BookSpritePath);
        activeTabSprite = Resources.Load<Sprite>(ActiveTabSpritePath);
        hasBookArt = bookSprite != null;
        var bookImage = book.GetComponent<Image>();
        if (hasBookArt)
        {
            bookImage.sprite = bookSprite;
            bookImage.color = Color.white;
            if (activeTabSprite == null)
            {
                Debug.LogWarning("[NotePanelUI] 고른 탭 그림을 찾을 수 없습니다: Assets/Resources/" + ActiveTabSpritePath + ".png");
            }
        }
        else
        {
            bookImage.color = new Color(0f, 0f, 0f, 0f);
            Debug.LogWarning("[NotePanelUI] 노트 그림을 찾을 수 없어 임시 도형으로 그립니다: Assets/Resources/" + BookSpritePath + ".png");
            BuildFallbackBook(book.transform);
        }
        bookImage.raycastTarget = false;

        BuildTabs(book.transform);
        BuildLeftPage(book.transform);
        BuildRightPage(book.transform);
        // 닫기 버튼은 책이 아니라 어두운 바깥 화면의 오른쪽 위 모서리에 붙인다.
        BuildCloseButton(overlay.transform);

        // ----- 글꼴 -----
        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨지므로,
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(overlay);

        overlay.SetActive(false);
    }

    // 책 안의 자리를 "그림 속 좌표"(왼쪽 위가 0,0 / 단위 px)로 잡는다.
    // 책이 1440x1080 그림 한 장이라 그림에서 본 숫자를 그대로 쓰면 된다.
    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    // ----- 노트 그림 속 좌표 (NoteBook_v2.png, Figma "Screen / Note v2" 기준) -----
    // 표지 x150~1290 y66~1014 / 왼쪽 페이지 x190~720 / 오른쪽 페이지 x720~1250 / 종이 y95~985.
    // 가운데 접힘선 그림자가 720 양옆으로 번지므로 글은 그보다 안쪽에서 시작하게 둔다.
    // 오른쪽 페이지의 x1186~1204에는 책갈피 끈이 지나가므로 본문은 1176에서 끝낸다.
    private const float LeftPageX = 228f;
    private const float LeftPageWidth = 456f;
    private const float RightPageX = 776f;
    private const float RightPageWidth = 400f;
    private const float PageTop = 128f;
    private const float PageBottom = 940f;
    private const float ListTop = 232f;          // 왼쪽 페이지 머리말(영문 + 탭 이름 + 이중선) 아래

    // 탭 4개 (왼쪽, 위에서부터 y 150 + 96·i, 높이 80).
    //   기본 탭: 그림에 그려져 있다 (x40~190, 표지 밖으로 보이는 건 x40~150).
    //   고른 탭: NoteTab_Active(176x80)를 x20에 책 위로 얹는다 (페이지 가장자리 x196까지 이어짐).
    private const float TabActiveX = 20f;
    private const float TabIdleX = 40f;
    private const float TabActiveWidth = 176f;
    private const float TabIdleWidth = 150f;
    private const float TabClickWidth = 130f;    // 클릭 영역: x20~150 (페이지는 가리지 않는다)
    private const float TabHeight = 80f;
    private const float TabFirstY = 150f;
    private const float TabPitch = 96f;
    private const float TabTextInset = 20f;

    // 책 전체 크기 배율 (1 = 그림 원래 크기). 더 줄이거나 키우려면 이 값만 바꾼다.
    private const float BookScale = 0.9f;

    // 노트 그림이 없을 때 쓰는 임시 책 (표지 + 종이 + 가운데 접힘선).
    private void BuildFallbackBook(Transform bookTransform)
    {
        AddPlainImage(bookTransform, "FallbackCover", 150f, 66f, 1140f, 948f, CoverColor);
        AddPlainImage(bookTransform, "FallbackPaper", 190f, 95f, 1060f, 890f, PaperColor);
        AddPlainImage(bookTransform, "FallbackSpine", 716f, 95f, 8f, 890f, SpineColor);
    }

    private static Image AddPlainImage(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), x, y, width, height);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text AddPlainText(Transform parent, string name, float x, float y, float width, float height,
                                         float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), x, y, width, height);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    // 왼쪽 바깥의 인덱스 탭 4개. 번호(01~04) + 탭 이름.
    private void BuildTabs(Transform bookTransform)
    {
        for (int i = 0; i < NoteCatalog.Tabs.Length; i++)
        {
            string category = NoteCatalog.Tabs[i];
            var view = new TabView();

            // 클릭 영역 (거의 투명). 고른 탭 그림/글자는 이 안에 자식으로 둔다.
            var go = new GameObject("Tab_" + category, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(bookTransform, false);
            Place(go.GetComponent<RectTransform>(), TabActiveX, TabFirstY + i * TabPitch, TabClickWidth, TabHeight);
            var hit = go.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // 클릭만 받는 완전 투명 (흰색 1%는 Linear 색 공간에서 회색 네모로 보인다)
            hit.raycastTarget = true;

            // 고른 탭 그림 (176x80, 클릭 영역보다 넓어서 페이지 가장자리까지 덮는다)
            var artGo = new GameObject("ActiveArt", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(go.transform, false);
            Place(artGo.GetComponent<RectTransform>(), 0f, 0f, TabActiveWidth, TabHeight);
            view.art = artGo.GetComponent<Image>();
            view.art.sprite = hasBookArt ? activeTabSprite : null;
            view.art.raycastTarget = false;
            view.art.enabled = false;

            view.indexText = AddPlainText(go.transform, "Index", 0f, 0f, 120f, 20f, 14, FontStyles.Bold, TabIdleIndexColor);
            view.indexText.text = (i + 1).ToString("00");
            view.indexText.characterSpacing = 15f;
            view.index = view.indexText.rectTransform;

            view.labelText = AddPlainText(go.transform, "Label", 0f, 0f, 130f, 32f, 21, FontStyles.Normal, TabIdleInkColor);
            view.labelText.text = category;
            view.labelText.textWrappingMode = TextWrappingModes.NoWrap;
            view.label = view.labelText.rectTransform;

            view.button = go.GetComponent<Button>();
            view.button.targetGraphic = hit;
            view.button.onClick.AddListener(() =>
            {
                currentCategory = category;
                // 탭을 바꾸면 고른 메모를 비운다. RebuildList()가 그 탭의 첫 줄을 골라준다.
                selectedEntryId = null;
                Refresh();
            });

            tabViews.Add(view);
        }
    }

    // 왼쪽 페이지: 머리말(영문 + 탭 이름 + 이중선) + 세로로 줄을 쌓는 스크롤 목록.
    private void BuildLeftPage(Transform bookTransform)
    {
        pageKickerText = AddPlainText(bookTransform, "PageKicker", LeftPageX + 4f, PageTop - 4f, LeftPageWidth, 22f, 14, FontStyles.Bold, FadedInkColor);
        pageKickerText.characterSpacing = 38f;
        pageTitleText = AddPlainText(bookTransform, "PageTitle", LeftPageX + 2f, PageTop + 16f, LeftPageWidth, 66f, 46, FontStyles.Bold, InkColor);
        AddPlainImage(bookTransform, "HeadRule1", LeftPageX, 214f, LeftPageWidth, 2f, new Color(InkColor.r, InkColor.g, InkColor.b, 0.75f));
        AddPlainImage(bookTransform, "HeadRule2", LeftPageX, 220f, LeftPageWidth, 1f, new Color(InkColor.r, InkColor.g, InkColor.b, 0.35f));

        var viewport = new GameObject("ListViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(bookTransform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        Place(viewportRt, LeftPageX, ListTop, LeftPageWidth, PageBottom - ListTop);
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
        layout.spacing = 0f;

        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        listScroll = viewport.AddComponent<ScrollRect>();
        listScroll.viewport = viewportRt;
        listScroll.content = listContent;
        listScroll.horizontal = false;
        listScroll.vertical = true;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = 40f;
    }

    // 오른쪽 페이지: 제목 + 분류 표기 + 구분선 + 본문(스크롤).
    private void BuildRightPage(Transform bookTransform)
    {
        detailTitleText = AddPlainText(bookTransform, "DetailTitle", RightPageX, PageTop - 4f, RightPageWidth, 58f, 35, FontStyles.Bold, InkColor);
        detailTitleText.alignment = TextAlignmentOptions.BottomLeft;
        detailTitleText.overflowMode = TextOverflowModes.Ellipsis;
        detailTitleText.textWrappingMode = TextWrappingModes.NoWrap;

        detailMetaText = AddPlainText(bookTransform, "DetailMeta", RightPageX + 2f, PageTop + 60f, RightPageWidth, 26f, 17, FontStyles.Normal, FadedInkColor);
        detailMetaText.characterSpacing = 4f;

        AddPlainImage(bookTransform, "DetailDivider", RightPageX, PageTop + 96f, RightPageWidth, 1f, RuleColor);

        var viewport = new GameObject("DetailViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(bookTransform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        float bodyTop = PageTop + 114f;   // 제목/분류/구분선 아래
        Place(viewportRt, RightPageX, bodyTop, RightPageWidth, PageBottom - bodyTop);
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
        detailBodyText.fontSize = 25;
        detailBodyText.alignment = TextAlignmentOptions.TopLeft;
        detailBodyText.color = InkColor;
        detailBodyText.lineSpacing = 18f;
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

    // 오른쪽 위 닫기 버튼 (퀵바 버튼과 같은 어두운 청회색 톤).
    private void BuildCloseButton(Transform parent)
    {
        var go = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(60f, 60f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.071f, 0.098f, 0.125f, 0.75f);
        bg.raycastTarget = true;

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        Stretch(labelGo.GetComponent<RectTransform>());
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "X";
        label.fontSize = 28;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.788f, 0.835f, 0.878f);
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
