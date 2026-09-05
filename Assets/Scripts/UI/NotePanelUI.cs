using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 조사기록(수첩) 화면 - 재훈이 적어나가는 메모를 챕터별로 보여준다
// =====================================================================================
// 퀵바의 Note 버튼을 누르면 열리는 탭이다.
//
// ===== 어떻게 채워지나 =====
// 메모 내용은 전부 Resources/Dialogues/NoteEntries.csv 에 적혀 있고, NoteManager가
// "지금까지 어떤 메모가 열렸는지"를 관리한다. 이 스크립트는 그것을 화면에 그리기만 한다.
//   - 게임을 시작하면 TriggerType=Initial 인 메모(회사 관련 기본 정보)가 이미 적혀 있다.
//   - 조사를 하거나 아이템을 얻을 때마다 관련 메모가 실시간으로 추가된다.
//   - #07(회사 잠입) 구간에서는 실시간 추가가 멈추고, 구간이 끝날 때 한꺼번에 반영된다.
// 메모가 추가되면 NoteManager.OnNoteChanged 이벤트가 오므로, 수첩을 열어둔 채로도
// 내용이 바로 갱신된다.
//
// ===== 씬에 이미 있던 것들 처리 =====
// 씬의 NotePanel에는 예전 프로토타입에서 쓰던 아이템 자리(ItemSlots 등)가 남아 있다.
// 그대로 두면 메모 위에 겹쳐 보이므로, 이 스크립트가 만드는 내용은 전용 자식
// (__NoteContent) 안에 넣고 나머지 자식은 꺼둔다. 지우지 않고 꺼두기만 하므로
// 나중에 되돌리고 싶으면 이 스크립트만 떼면 된다.
public class NotePanelUI : MonoBehaviour
{
    // 이 스크립트가 만든 UI를 담는 자식의 이름.
    private const string ContentRootName = "__NoteContent";

    [Header("UI 연결 (비워두면 자동 생성)")]
    [Tooltip("메모 전체가 들어갈 텍스트")]
    public TMP_Text noteText;
    [Tooltip("스크롤 담당")]
    public ScrollRect scrollRect;
    [Tooltip("수첩 제목")]
    public TMP_Text titleText;

    private void Awake()
    {
        EnsureUI();
    }

    private void OnEnable()
    {
        // 어떤 이유로든 NoteManager가 없으면 여기서 만들어준다.
        // (원래는 GameBootstrap이 만들어두지만, 이 패널만 단독으로 테스트하는 경우 등
        //  빠져 있을 수 있다. 없으면 수첩이 영영 비어 보이므로 확실히 확보한다.)
        if (NoteManager.Instance == null)
        {
            new GameObject("NoteManager").AddComponent<NoteManager>();
        }

        if (NoteManager.Instance != null)
        {
            NoteManager.Instance.OnNoteChanged -= Refresh;
            NoteManager.Instance.OnNoteChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (NoteManager.Instance != null)
        {
            NoteManager.Instance.OnNoteChanged -= Refresh;
        }
    }

    // ---------------------------------------------------------------------------------
    // 내용 그리기
    // ---------------------------------------------------------------------------------
    public void Refresh()
    {
        if (noteText == null) return;

        if (NoteManager.Instance == null)
        {
            noteText.text = "(조사기록을 불러올 수 없습니다.)";
            return;
        }

        var entries = NoteManager.Instance.GetRecordedEntriesSorted();

        if (entries.Count == 0)
        {
            noteText.text = "아직 적어둔 것이 없다.";
            return;
        }

        // StringBuilder를 쓰는 이유: 문자열을 += 로 수십 번 이어붙이면 그때마다 새 문자열이
        // 통째로 만들어져 낭비가 크다. StringBuilder는 하나의 버퍼에 계속 덧붙인다.
        var sb = new StringBuilder();
        string lastChapter = null;

        foreach (var entry in entries)
        {
            // 챕터가 바뀔 때마다 소제목을 넣는다.
            if (entry.chapter != lastChapter)
            {
                if (lastChapter != null) sb.AppendLine();
                // <b>...</b>와 <color>는 TextMeshPro가 알아보는 서식 태그다.
                sb.AppendLine($"<b><color=#4A3520>{entry.chapter}</color></b>");
                lastChapter = entry.chapter;
            }

            sb.AppendLine($"  · {entry.text}");
            sb.AppendLine();
        }

        noteText.text = sb.ToString();

        // 새 메모가 추가되면 맨 아래(최신)로 스크롤을 내려준다.
        if (scrollRect != null)
        {
            // 레이아웃이 다시 계산된 뒤에 스크롤해야 정확한 위치로 간다.
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // ---------------------------------------------------------------------------------
    // UI 만들기
    // ---------------------------------------------------------------------------------
    private void EnsureUI()
    {
        // 이미 만들어둔 게 있으면 다시 만들지 않는다(패널을 여닫아도 한 번만 만들어진다).
        var existing = transform.Find(ContentRootName);
        if (existing != null)
        {
            CacheReferences(existing);
            return;
        }

        // ----- 씬에 남아 있던 예전 오브젝트를 꺼둔다 -----
        // NotePanel 안에는 프로토타입 시절 아이템 자리 등이 들어 있어서, 그대로 두면
        // 메모 위에 겹쳐 보인다. 지우지 않고 비활성화만 한다.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == ContentRootName) continue;
            child.gameObject.SetActive(false);
        }

        // ----- 패널 배경: 누런 종이 느낌 -----
        var panelRect = GetComponent<RectTransform>();
        if (panelRect == null) panelRect = gameObject.AddComponent<RectTransform>();

        // 화면 가운데에 적당한 크기로 띄운다. 씬에 설정된 크기가 제각각이라 여기서 고정한다.
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 820f);
        panelRect.anchoredPosition = Vector2.zero;

        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.95f, 0.92f, 0.84f, 1f);
        bg.raycastTarget = true;   // 뒤쪽 클릭이 새어나가지 않게 막는다

        // ----- 내용 담을 전용 자식 -----
        var root = new GameObject(ContentRootName, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());

        // ----- 제목 -----
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(root.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(36f, 0f);
        titleRt.offsetMax = new Vector2(-36f, 0f);
        titleRt.sizeDelta = new Vector2(titleRt.sizeDelta.x, 64f);
        titleRt.anchoredPosition = new Vector2(0f, -20f);

        titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "조사기록";
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = new Color(0.18f, 0.13f, 0.08f);
        titleText.raycastTarget = false;

        // 제목 아래 구분선
        var line = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(root.transform, false);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 1f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.pivot = new Vector2(0.5f, 1f);
        lineRt.offsetMin = new Vector2(36f, 0f);
        lineRt.offsetMax = new Vector2(-36f, 0f);
        lineRt.sizeDelta = new Vector2(lineRt.sizeDelta.x, 2f);
        lineRt.anchoredPosition = new Vector2(0f, -86f);
        var lineImg = line.GetComponent<Image>();
        lineImg.color = new Color(0.55f, 0.45f, 0.32f, 0.7f);
        lineImg.raycastTarget = false;

        // ----- 스크롤 영역 -----
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0f, 0f);
        viewportRt.anchorMax = new Vector2(1f, 1f);
        viewportRt.offsetMin = new Vector2(36f, 30f);
        viewportRt.offsetMax = new Vector2(-36f, -96f);
        // RectMask2D는 이 영역 밖으로 나가는 글자를 잘라준다(스크롤에 필요).
        // Image는 클릭(스크롤 휠)을 받기 위해 필요하고, 거의 투명하게 둔다.
        var viewportImg = viewport.GetComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.01f);

        // ----- 실제 글이 들어가는 영역 -----
        // 내용이 길어지면 세로로 늘어난다.
        var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = new Vector2(0f, 0f);
        contentRt.offsetMax = new Vector2(0f, 0f);
        contentRt.anchoredPosition = Vector2.zero;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        noteText = content.AddComponent<TextMeshProUGUI>();
        noteText.fontSize = 24;
        noteText.alignment = TextAlignmentOptions.TopLeft;
        noteText.color = new Color(0.16f, 0.13f, 0.09f);
        noteText.lineSpacing = 8f;
        noteText.raycastTarget = false;
        noteText.richText = true;
        // 글이 길어지면 아래로 계속 이어지게 한다(잘라내지 않는다).
        noteText.overflowMode = TextOverflowModes.Overflow;

        // ----- 스크롤 기능 -----
        scrollRect = gameObject.GetComponent<ScrollRect>();
        if (scrollRect == null) scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;
    }

    // 이미 만들어져 있던 UI에서 참조를 다시 찾아온다.
    private void CacheReferences(Transform root)
    {
        if (titleText == null)
        {
            var t = root.Find("Title");
            if (t != null) titleText = t.GetComponent<TMP_Text>();
        }
        if (noteText == null)
        {
            var c = root.Find("Viewport/Content");
            if (c != null) noteText = c.GetComponent<TMP_Text>();
        }
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
