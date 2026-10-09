using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 조사기록(수첩) 화면 - 재훈이 조사하면서 적어 나가는 수사 수첩
// =====================================================================================
// 퀵바의 Note 버튼을 누르면 열리는 화면이다.
//
// ===== 화면 구성 =====
//   왼쪽 바깥 : 탭 2개 (수사 메모 / 업무 수첩)
//   페이지 하나 = 장소 하나 (사무실, 해안도로, 경찰서...). 페이지 맨 위에 장소 이름, 그 아래로
//                 재훈이 적은 문장이 한 줄씩 이어진다. 한 페이지를 넘치면 다음 페이지로 이어진다.
//                 펼치면 두 페이지(보통 두 장소)가 나란히 보인다.
//   아래쪽 : [‹] [›] 와 쪽 번호. 키보드 ← → 로도 넘긴다.
//
// 목록/제목/번호 없이 문장만 적힌 "수첩"처럼 보이게 하는 것이 목적이다.
// 문장은 NoteEntries.csv의 Text, 장소는 Chapter, 순서는 Order에서 온다.
// Chapter 앞의 "#01 " 같은 번호는 장소 순서를 정하는 데만 쓰고 화면에는 보이지 않는다.
//
// ===== 수첩을 열면 어느 장이 보이나 =====
//   아직 안 읽은 새 메모가 있으면 그 메모가 있는 장, 없으면 가장 최근 장소가 있는 장.
//   펼쳐 본 장(양쪽 페이지)의 메모는 "읽음"으로 친다 (NewContentTracker - 퀵바 숫자/추리 중 [수첩 보기]와 연동).
//
// ===== 왜 UI를 캔버스에 직접 만드나 (중요) =====
// 씬의 NotePanel은 프로토타입 시절 크기와 위치가 제각각이라, 메모 화면을 캔버스 바로 아래에
// 따로 만든다. 씬의 NotePanel은 "열렸는지 닫혔는지"를 알려주는 스위치 역할만 한다
// (UIManager가 그 패널을 켜고 끄므로, 이 스크립트는 그때 맞춰 메모 화면을 보여준다).
public class NotePanelUI : MonoBehaviour
{
    // 캔버스 아래에 만드는 메모 화면의 이름.
    private const string OverlayName = "__NoteOverlay";

    // ----- 탭 -----
    private const string TabInvestigation = "수사 메모";
    private const string TabWork = "업무 수첩";
    private static readonly string[] Tabs = { TabInvestigation, TabWork };

    // ----- 색 (가죽 수사 수첩 v2 - 청회색 테마) -----
    private static readonly Color PaperColor = new Color(0.929f, 0.922f, 0.898f, 1f);   // EDEBE5 (그림이 없을 때만 쓰는 종이색)
    private static readonly Color CoverColor = new Color(0.157f, 0.196f, 0.239f, 1f);   // 28323D (그림이 없을 때만)
    private static readonly Color InkColor = new Color(0.114f, 0.141f, 0.169f);         // 1D242B 본문 잉크
    private static readonly Color FadedInkColor = new Color(0.33f, 0.38f, 0.43f);       // 54616E 흐린 잉크 (종이 위 대비 5:1 이상)
    private static readonly Color AccentColor = new Color(0.298f, 0.388f, 0.467f);      // 4C6377 청회색 강조
    private static readonly Color SpineColor = new Color(0.114f, 0.141f, 0.169f, 0.25f);
    private static readonly Color TabIdleColor = new Color(0.79f, 0.8f, 0.8f, 1f);      // 그림이 없을 때만
    private static readonly Color TabIdleInkColor = new Color(0.31f, 0.365f, 0.412f);   // 4F5D69
    private static readonly Color TabIdleIndexColor = new Color(0.43f, 0.48f, 0.52f);   // 6E7A85

    // ----- 글자 -----
    private const float BodyFontSize = 23f;
    private const float BodyLineSpacing = 8f;        // 줄 사이 (TMP em/100)
    private const float BodyParagraphSpacing = 20f;  // 메모와 메모 사이
    private const float PlaceTitleFontSize = 38f;

    // 노트 그림 경로 (Resources 기준, 확장자 없음).
    //   NoteBook_v2    : 1440x1080 투명 PNG. 책 + 기본 상태 탭이 그려져 있다.
    //   NoteTab_Active : 176x80. 고른 탭 자리에 책 위로 얹는 종이색 탭.
    private const string BookSpritePath = "Illusts/UI/NoteBook_v2";
    private const string ActiveTabSpritePath = "Illusts/UI/NoteTab_Active";
    private bool hasBookArt;
    private Sprite activeTabSprite;

    private GameObject overlay;

    // 탭 하나를 그리는 데 필요한 것들. UpdateTabVisuals가 고른 탭/안 고른 탭 모양을 바꾼다.
    private class TabView
    {
        public Image art;        // 고른 탭 그림 (그림이 없을 때는 색 도형)
        public RectTransform index;
        public RectTransform label;
        public TMP_Text indexText;
        public TMP_Text labelText;
    }
    private readonly List<TabView> tabViews = new List<TabView>();

    // 페이지 한 쪽 분량. 펼친 장 하나 = 페이지 두 쪽 (pages[2k] 왼쪽, pages[2k+1] 오른쪽).
    private class Page
    {
        public string place;
        public string text = "";
        public readonly List<string> entryIds = new List<string>();
    }
    private readonly List<Page> pages = new List<Page>();
    private int SpreadCount => (pages.Count + 1) / 2;
    private int currentSpread;
    private int currentTab;
    private bool pickSpreadOnRefresh;   // 수첩을 막 열었거나 탭을 바꿨을 때만 "보여줄 장"을 새로 고른다

    private TMP_Text leftTitleText;
    private TMP_Text rightTitleText;
    private GameObject rightHeadRules;
    private TMP_Text leftBodyText;
    private TMP_Text rightBodyText;
    private TMP_Text pageCounterText;
    private Button prevButton;
    private Button nextButton;

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

        // 새 메모가 있으면 수사 메모 탭으로 연다.
        if (HasUnseenIn(TabInvestigation)) currentTab = 0;
        pickSpreadOnRefresh = true;
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

    // ← → 로 장 넘기기. 자료 뷰어가 위에 떠 있으면 그쪽이 ← →를 쓰므로 건드리지 않는다.
    private void Update()
    {
        if (overlay == null || !overlay.activeInHierarchy) return;
        if (DocumentViewerController.Instance != null && DocumentViewerController.Instance.IsOpen) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) GoToSpread(currentSpread - 1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) GoToSpread(currentSpread + 1);
    }

    // ---------------------------------------------------------------------------------
    // 내용 그리기
    // ---------------------------------------------------------------------------------
    public void Refresh()
    {
        if (leftBodyText == null) return;

        UpdateTabVisuals();
        BuildNotePages(Tabs[currentTab]);

        if (pickSpreadOnRefresh)
        {
            pickSpreadOnRefresh = false;
            currentSpread = DefaultSpreadIndex();
        }
        ShowSpread(Mathf.Clamp(currentSpread, 0, Mathf.Max(0, SpreadCount - 1)));
    }

    private static bool IsWorkNote(NoteManager.NoteEntry entry)
        => NoteCatalog.CategoryOf(entry) == NoteCatalog.CategoryWorkNote;

    private static List<NoteManager.NoteEntry> EntriesForTab(string tab)
    {
        var result = new List<NoteManager.NoteEntry>();
        if (NoteManager.Instance == null) return result;
        foreach (var entry in NoteManager.Instance.GetRecordedEntriesSorted())
        {
            if (IsSupersededAutoNote(entry)) continue;
            if ((tab == TabWork) == IsWorkNote(entry)) result.Add(entry);
        }
        return result;
    }

    // 조사 문장을 그대로 옮겨 적은 자동 메모(auto_{조사화면}_{오브젝트}) 중, 지금은 NoteEntries.csv에
    // 같은 오브젝트(또는 그 오브젝트로 얻는 아이템)의 요약 메모가 따로 있는 것.
    // 요약 메모가 생기기 전의 세이브를 불러오면 이런 메모가 남아 있어서, 같은 내용이 긴 원문으로
    // 한 번 더 적혀 페이지를 넘치게 했다. 세이브는 그대로 두고 화면에서만 뺀다.
    private static bool IsSupersededAutoNote(NoteManager.NoteEntry entry)
    {
        if (entry == null || !string.Equals(entry.triggerType, "Auto", System.StringComparison.OrdinalIgnoreCase)) return false;
        var nm = NoteManager.Instance;
        if (nm == null) return false;

        if (!string.IsNullOrWhiteSpace(entry.itemId) && nm.HasEntryFor("Item", entry.itemId.Trim())) return true;

        // "auto_BG_02_CoastalRoad_Hotspot_Cliff" -> "BG_02_CoastalRoad|Hotspot_Cliff"
        string id = entry.entryId ?? "";
        int cut = id.IndexOf("_Hotspot_", System.StringComparison.Ordinal);
        if (!id.StartsWith("auto_") || cut < 0) return false;
        string screen = id.Substring(5, cut - 5);
        string hotspot = id.Substring(cut + 1);
        return nm.HasEntryFor("Hotspot", screen + "|" + hotspot);
    }

    private static bool HasUnseenIn(string tab)
    {
        foreach (var entry in EntriesForTab(tab))
        {
            if (NewContentTracker.IsNew(NewContentTracker.Kind.Note, entry.entryId)) return true;
        }
        return false;
    }

    // 처음 보여줄 장: 안 읽은 새 메모가 있는 첫 장, 없으면 가장 최근 장소(맨 끝 장소)의 첫 페이지가 있는 장.
    private int DefaultSpreadIndex()
    {
        if (pages.Count == 0) return 0;

        for (int i = 0; i < pages.Count; i++)
        {
            foreach (string id in pages[i].entryIds)
            {
                if (NewContentTracker.IsNew(NewContentTracker.Kind.Note, id)) return i / 2;
            }
        }

        int last = pages.Count - 1;
        string lastPlace = pages[last].place;
        while (last > 0 && pages[last - 1].place == lastPlace) last--;
        return last / 2;
    }

    // ----- 장소별로 묶고, 페이지 크기에 맞춰 나눈다 -----
    private void BuildNotePages(string tab)
    {
        pages.Clear();

        // 장소 묶기. Chapter의 첫 토막("#01")이 같으면 같은 장소다. 조사 문장을 자동으로 옮겨 적은
        // 메모는 Chapter가 "#01"처럼 번호만 있어서, 이름은 같은 번호의 다른 메모에서 빌려 온다.
        var groupOrder = new List<string>();
        var groups = new Dictionary<string, List<NoteManager.NoteEntry>>();
        var groupNames = new Dictionary<string, string>();
        foreach (var entry in EntriesForTab(tab))
        {
            string key = GroupKeyOf(entry.chapter);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<NoteManager.NoteEntry>();
                groups[key] = list;
                groupOrder.Add(key);
            }
            list.Add(entry);

            string name = PlaceNameOf(entry.chapter);
            if (!string.IsNullOrEmpty(name) && !groupNames.ContainsKey(key)) groupNames[key] = name;
        }

        foreach (string key in groupOrder)
        {
            var list = groups[key];
            list.Sort((a, b) => a.order.CompareTo(b.order));
            string place = groupNames.TryGetValue(key, out var n) ? n : FallbackPlaceName(key);
            PaginatePlace(place, list);
        }
    }

    // 한 장소의 메모를 새 페이지부터 채운다. 넘치면 다음 페이지로 이어 쓴다.
    // 메모는 되도록 쪼개지 않지만, 메모 하나가 빈 페이지 한 쪽보다도 길면 문장 단위로 잘라 이어 쓴다.
    // 왼쪽/오른쪽 페이지 폭이 달라서, 지금 채우는 페이지가 어느 쪽인지(짝수 = 왼쪽) 보고 잰다.
    private void PaginatePlace(string place, List<NoteManager.NoteEntry> entries)
    {
        var page = new Page { place = place };

        foreach (var entry in entries)
        {
            string remaining = NoteCatalog.BodyOf(entry);
            if (string.IsNullOrWhiteSpace(remaining)) continue;
            remaining = remaining.Trim();

            int guard = 0;
            while (remaining.Length > 0 && guard++ < 200)
            {
                bool leftSide = pages.Count % 2 == 0;
                string candidate = page.text.Length == 0 ? remaining : page.text + "\n" + remaining;
                if (FitsOnPage(leftSide, candidate))
                {
                    page.text = candidate;
                    if (!page.entryIds.Contains(entry.entryId)) page.entryIds.Add(entry.entryId);
                    remaining = "";
                    break;
                }

                if (page.text.Length > 0)
                {
                    // 이 페이지는 여기까지. 같은 메모를 다음 페이지에서 다시 시도한다.
                    pages.Add(page);
                    page = new Page { place = place };
                    continue;
                }

                // 빈 페이지에도 다 안 들어가는 긴 메모: 들어가는 만큼만 적고 나머지는 다음 페이지로.
                SplitToFit(remaining, leftSide, out string head, out string tail);
                page.text = head;
                page.entryIds.Add(entry.entryId);
                pages.Add(page);
                page = new Page { place = place };
                remaining = tail;
            }
        }

        if (page.text.Length > 0) pages.Add(page);
    }

    // 긴 글을 빈 페이지 한 쪽에 들어가는 앞부분(head)과 나머지(tail)로 나눈다.
    // 줄바꿈 -> 문장 끝(". ") 순으로 끊을 자리를 찾고, 그래도 안 되면 글자 단위로 자른다.
    private void SplitToFit(string text, bool leftSide, out string head, out string tail)
    {
        var pieces = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            bool lineEnd = text[i] == '\n';
            bool sentenceEnd = (text[i] == '.' || text[i] == '?' || text[i] == '!') && i + 1 < text.Length && text[i + 1] == ' ';
            if (lineEnd || sentenceEnd)
            {
                pieces.Add(text.Substring(start, i + 1 - start));
                start = i + 1;
            }
        }
        if (start < text.Length) pieces.Add(text.Substring(start));

        string current = "";
        int used = 0;
        foreach (string piece in pieces)
        {
            string candidate = current + piece;
            if (!FitsOnPage(leftSide, candidate.TrimEnd())) break;
            current = candidate;
            used += piece.Length;
        }

        if (used == 0)
        {
            // 첫 문장 하나도 안 들어간다: 글자 단위로 들어가는 만큼만 (이분 탐색).
            int lo = 1, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (FitsOnPage(leftSide, text.Substring(0, mid))) lo = mid;
                else hi = mid - 1;
            }
            used = lo;
            current = text.Substring(0, used);
        }

        head = current.TrimEnd();
        tail = text.Substring(used).TrimStart();
    }

    private bool FitsOnPage(bool leftSide, string text)
    {
        var component = leftSide ? leftBodyText : rightBodyText;
        float width = leftSide ? LeftPageWidth : RightPageWidth;
        return component.GetPreferredValues(text, width, 0f).y <= BodyHeight;
    }

    // "#02 해안도로" -> "#02",  "업무 수첩" -> "업무 수첩"
    private static string GroupKeyOf(string chapter)
    {
        chapter = (chapter ?? "").Trim();
        if (!chapter.StartsWith("#")) return chapter;
        int space = chapter.IndexOf(' ');
        return space > 0 ? chapter.Substring(0, space) : chapter;
    }

    // "#02 해안도로" -> "해안도로",  "#02" -> "" (이름 없음)
    private static string PlaceNameOf(string chapter)
    {
        chapter = (chapter ?? "").Trim();
        if (!chapter.StartsWith("#")) return chapter;
        int space = chapter.IndexOf(' ');
        return space > 0 ? chapter.Substring(space + 1).Trim() : "";
    }

    // 이름이 적힌 메모가 하나도 없는 장소 ("#회상02" 같은 자동 메모만 있는 경우).
    private static string FallbackPlaceName(string key)
    {
        if (key.StartsWith("#회상")) return "회상";
        return "메모";
    }

    private void GoToSpread(int index)
    {
        if (index < 0 || index >= SpreadCount || index == currentSpread) return;
        ShowSpread(index);
    }

    private void ShowSpread(int index)
    {
        currentSpread = index;

        if (pages.Count == 0)
        {
            SetPageView(leftTitleText, leftBodyText, null);
            leftTitleText.text = Tabs[currentTab];
            leftTitleText.color = InkColor;
            leftBodyText.text = "아직 적어둔 것이 없다.";
            leftBodyText.color = FadedInkColor;
            SetPageView(rightTitleText, rightBodyText, null);
            if (rightHeadRules != null) rightHeadRules.SetActive(false);
            UpdateNav();
            return;
        }

        var left = pages[index * 2];
        var right = index * 2 + 1 < pages.Count ? pages[index * 2 + 1] : null;
        SetPageView(leftTitleText, leftBodyText, left);
        SetPageView(rightTitleText, rightBodyText, right);
        if (rightHeadRules != null) rightHeadRules.SetActive(right != null);

        // 펼쳐 본 장의 메모는 읽은 것으로 친다 (퀵바 숫자 / 새 메모 위치 판단에 쓰인다).
        foreach (var page in new[] { left, right })
        {
            if (page == null) continue;
            foreach (string id in page.entryIds) NewContentTracker.MarkSeen(NewContentTracker.Kind.Note, id);
        }

        UpdateNav();
    }

    // 페이지 한 쪽을 채운다. 앞 페이지와 같은 장소가 이어지는 페이지면 장소 이름을 흐리게 다시 적는다.
    private void SetPageView(TMP_Text title, TMP_Text body, Page page)
    {
        body.color = InkColor;
        if (page == null)
        {
            title.text = "";
            body.text = "";
            return;
        }

        int i = pages.IndexOf(page);
        bool continued = i > 0 && pages[i - 1].place == page.place;
        title.text = page.place;
        title.color = continued ? FadedInkColor : InkColor;
        body.text = page.text;
        UIFontHelper.Apply(title);
        UIFontHelper.Apply(body);
    }

    private void UpdateNav()
    {
        int count = SpreadCount;
        pageCounterText.text = count <= 1 ? "" : (currentSpread + 1) + " / " + count;
        SetNavVisible(prevButton, currentSpread > 0);
        SetNavVisible(nextButton, currentSpread < count - 1);
    }

    private static void SetNavVisible(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    // 고른 탭: 종이색 탭 그림을 책 위로 얹고(더 튀어나온 모양) 글자를 굵게.
    // 안 고른 탭: 그림 속 기본 탭 위에 글자만 놓는다.
    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabViews.Count; i++)
        {
            var tab = tabViews[i];
            bool active = i == currentTab;

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

        // 이미 만들어져 있으면 지우고 새로 만든다 (예전 구조가 남아 있으면 쓸 수 없다).
        // Awake에서 한 번 도는 정리 코드라 즉시 지워도 안전하다 - Destroy()는 프레임 끝에 지워져
        // 한 프레임 동안 같은 이름이 둘 존재하게 된다.
        var existing = canvas.transform.Find(OverlayName);
        if (existing != null) DestroyImmediate(existing.gameObject);

        tabViews.Clear();
        pages.Clear();

        // ----- 화면 전체를 덮는 막 -----
        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.97f);   // 환경설정/가방과 같은 검은 막
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 펼친 책 -----
        // 노트 그림(NoteBook_v2.png)은 캔버스와 같은 1440x1080 한 장이라 책을 캔버스 전체 크기로 깔고,
        // 탭/페이지 위치는 그림 속 좌표(왼쪽 위 기준 px)로 잡는다. 그림이 없으면 색 도형 책으로 대신 그린다.
        var book = new GameObject("Book", typeof(RectTransform), typeof(Image));
        book.transform.SetParent(overlay.transform, false);
        var bookRt = book.GetComponent<RectTransform>();
        bookRt.anchorMin = bookRt.anchorMax = bookRt.pivot = new Vector2(0.5f, 0.5f);
        bookRt.sizeDelta = new Vector2(1440f, 1080f);
        bookRt.anchoredPosition = Vector2.zero;
        // 화면을 꽉 채우면 답답해 보여서 책 전체를 가운데 기준으로 살짝 줄인다.
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
        BuildPages(book.transform);
        // 닫기 버튼은 책이 아니라 어두운 바깥 화면의 오른쪽 위 모서리에 붙인다.
        BuildCloseButton(overlay.transform);

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨지므로 한글 글꼴을 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(overlay);

        overlay.SetActive(false);
    }

    // 책 안의 자리를 "그림 속 좌표"(왼쪽 위가 0,0 / 단위 px)로 잡는다.
    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    // ----- 노트 그림 속 좌표 (NoteBook_v2.png 기준) -----
    // 표지 x150~1290 y66~1014 / 왼쪽 페이지 x190~720 / 오른쪽 페이지 x720~1250 / 종이 y95~985.
    // 가운데 접힘선 그림자가 720 양옆으로 번지므로 글은 그보다 안쪽에서 시작하게 둔다.
    // 오른쪽 페이지의 x1186~1204에는 책갈피 끈이 지나가므로 본문은 1176에서 끝낸다.
    private const float LeftPageX = 228f;
    private const float LeftPageWidth = 456f;
    private const float RightPageX = 776f;
    private const float RightPageWidth = 400f;
    private const float PageTop = 128f;
    private const float BodyTop = 232f;          // 장소 이름 + 이중선 아래
    private const float BodyBottom = 900f;       // 그 아래는 장 넘기기 버튼 자리
    private const float BodyHeight = BodyBottom - BodyTop;
    private const float NavY = 912f;

    // 탭 (왼쪽, 위에서부터 y 150 + 96·i, 높이 80).
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

    // 왼쪽 바깥의 인덱스 탭. 번호(01, 02) + 탭 이름.
    private void BuildTabs(Transform bookTransform)
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            int tabIndex = i;
            var view = new TabView();

            // 클릭 영역. 고른 탭 그림/글자는 이 안에 자식으로 둔다.
            var go = new GameObject("Tab_" + Tabs[i], typeof(RectTransform), typeof(Image), typeof(Button));
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
            view.labelText.text = Tabs[i];
            view.labelText.textWrappingMode = TextWrappingModes.NoWrap;
            view.label = view.labelText.rectTransform;

            var button = go.GetComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() =>
            {
                if (currentTab == tabIndex) return;
                currentTab = tabIndex;
                pickSpreadOnRefresh = true;
                Refresh();
            });

            tabViews.Add(view);
        }
    }

    // 펼친 두 페이지: 각 페이지 맨 위 장소 이름 + 이중선, 본문, 아래쪽 장 넘기기.
    private void BuildPages(Transform bookTransform)
    {
        leftTitleText = AddPageTitle(bookTransform, "LeftTitle", LeftPageX, LeftPageWidth);
        AddHeadRules(bookTransform, "Left", LeftPageX, LeftPageWidth);
        rightTitleText = AddPageTitle(bookTransform, "RightTitle", RightPageX, RightPageWidth);
        rightHeadRules = AddHeadRules(bookTransform, "Right", RightPageX, RightPageWidth);

        leftBodyText = AddBodyText(bookTransform, "LeftBody", LeftPageX, BodyTop, LeftPageWidth, BodyHeight);
        rightBodyText = AddBodyText(bookTransform, "RightBody", RightPageX, BodyTop, RightPageWidth, BodyHeight);

        prevButton = AddNavButton(bookTransform, "Btn_PrevPage", "‹", LeftPageX, () => GoToSpread(currentSpread - 1));
        nextButton = AddNavButton(bookTransform, "Btn_NextPage", "›", RightPageX + RightPageWidth - 56f, () => GoToSpread(currentSpread + 1));

        pageCounterText = AddPlainText(bookTransform, "PageCounter", RightPageX, NavY, RightPageWidth, 44f, 18, FontStyles.Normal, FadedInkColor);
        pageCounterText.alignment = TextAlignmentOptions.Center;
        pageCounterText.characterSpacing = 6f;
    }

    private static TMP_Text AddPageTitle(Transform parent, string name, float x, float width)
    {
        var title = AddPlainText(parent, name, x + 2f, PageTop + 24f, width, 58f, PlaceTitleFontSize, FontStyles.Bold, InkColor);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        return title;
    }

    // 장소 이름 아래 이중선. 오른쪽 페이지가 비면 함께 숨기려고 묶어서 돌려준다.
    private static GameObject AddHeadRules(Transform parent, string side, float x, float width)
    {
        var group = new GameObject(side + "HeadRules", typeof(RectTransform));
        group.transform.SetParent(parent, false);
        Place((RectTransform)group.transform, 0f, 0f, 1440f, 1080f);
        AddPlainImage(group.transform, "HeadRule1", x, 214f, width, 2f, new Color(InkColor.r, InkColor.g, InkColor.b, 0.75f));
        AddPlainImage(group.transform, "HeadRule2", x, 220f, width, 1f, new Color(InkColor.r, InkColor.g, InkColor.b, 0.35f));
        return group;
    }

    private static TMP_Text AddBodyText(Transform parent, string name, float x, float y, float width, float height)
    {
        var text = AddPlainText(parent, name, x, y, width, height, BodyFontSize, FontStyles.Normal, InkColor);
        text.lineSpacing = BodyLineSpacing;
        text.paragraphSpacing = BodyParagraphSpacing;
        text.textWrappingMode = TextWrappingModes.Normal;
        // 페이지 나누기는 PaginatePlace가 미리 하지만, 만에 하나 넘쳐도 페이지 밖으로 글자가 삐져나오지 않게 자른다.
        text.overflowMode = TextOverflowModes.Truncate;
        text.richText = true;
        UIFontHelper.Apply(text);   // 페이지 나누기용 글자 크기 측정이 실제 글꼴로 되도록 먼저 물려준다
        return text;
    }

    // 장 넘기기 버튼 (‹ / ›). 글자만 보이고 누르는 영역은 넉넉하게.
    private Button AddNavButton(Transform parent, string name, string glyph, float x, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), x, NavY, 56f, 44f);
        var hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;

        var label = AddPlainText(go.transform, "Glyph", 0f, 0f, 56f, 44f, 34, FontStyles.Bold, AccentColor);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label.text = glyph;
        label.alignment = TextAlignmentOptions.Center;

        var button = go.GetComponent<Button>();
        button.targetGraphic = hit;
        button.onClick.AddListener(onClick);
        return button;
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
