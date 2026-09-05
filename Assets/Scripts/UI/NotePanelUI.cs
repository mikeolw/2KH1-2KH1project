using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 조사기록(수첩) 화면 - 지금까지 재훈이 적어둔 메모를 챕터별로 보여준다
// =====================================================================================
// NoteManager가 "어떤 메모가 쌓였는지"를 관리하고, 이 스크립트는 그것을 화면에 그리기만 한다.
// 메모가 새로 추가되면 NoteManager.OnNoteChanged 이벤트로 알려주므로, 수첩을 열어둔 채로
// 내용이 늘어나도 즉시 반영된다.
//
// ===== 표시 방식 =====
// 메모를 챕터(#01 사무실, #02 해안도로 ...)별로 묶어서 하나의 긴 글로 만든다.
// 항목이 스무 개 남짓이라 스크롤 하나로 충분해서, 항목마다 UI 오브젝트를 만드는 대신
// 텍스트 한 덩어리로 이어 붙였다. (오브젝트를 수십 개 만들면 여는 순간 살짝 버벅인다.)
//
// ===== 씬 배치 =====
// UIManager.notePanel에 이 스크립트가 붙은 GameObject를 연결하면 퀵바의 수첩 버튼으로
// 여닫을 수 있다. 인스펙터 필드를 비워두면 스스로 UI를 만든다.
public class NotePanelUI : MonoBehaviour
{
    [Header("UI 연결 (비워두면 자동 생성)")]
    [Tooltip("메모 전체가 들어갈 텍스트")]
    public TMP_Text noteText;
    [Tooltip("스크롤을 쓸 경우 연결. 없어도 동작한다.")]
    public ScrollRect scrollRect;
    [Tooltip("수첩 제목")]
    public TMP_Text titleText;

    private void Awake()
    {
        EnsureUI();
    }

    private void OnEnable()
    {
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

    // 지금까지 쌓인 메모를 챕터별로 묶어서 다시 그린다.
    public void Refresh()
    {
        if (noteText == null) return;

        if (NoteManager.Instance == null)
        {
            noteText.text = "(조사기록을 불러올 수 없습니다. 씬에 NoteManager가 있는지 확인하세요.)";
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
                if (lastChapter != null) sb.AppendLine();   // 챕터 사이 한 줄 띄우기
                // <b>...</b>는 TextMeshPro가 알아보는 굵게 표시 태그다.
                sb.AppendLine($"<b>{entry.chapter}</b>");
                lastChapter = entry.chapter;
            }

            sb.AppendLine($"· {entry.text}");
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
    // UI 자동 생성
    // ---------------------------------------------------------------------------------
    private void EnsureUI()
    {
        if (noteText != null) return;

        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.96f, 0.94f, 0.86f, 0.98f); // 누런 종이 느낌

        // 제목
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.90f);
        titleRt.anchorMax = new Vector2(0.95f, 0.97f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "조사기록";
        titleText.fontSize = 30;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = new Color(0.15f, 0.13f, 0.10f);
        titleText.raycastTarget = false;

        // 스크롤 영역
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(transform, false);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0.05f, 0.05f);
        viewportRt.anchorMax = new Vector2(0.95f, 0.88f);
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        // Mask는 Image가 있어야 동작한다(그 Image 모양대로 잘라낸다).
        viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        // 실제 글이 들어가는 영역. 내용이 길어지면 세로로 늘어난다.
        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.GetComponent<RectTransform>();
        // 위쪽 가장자리에 붙여두고 아래로 늘어나게 한다.
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        noteText = contentGo.AddComponent<TextMeshProUGUI>();
        noteText.fontSize = 20;
        noteText.alignment = TextAlignmentOptions.TopLeft;
        noteText.color = new Color(0.15f, 0.13f, 0.10f);
        noteText.raycastTarget = false;

        // 스크롤 기능 연결
        scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
    }
}
