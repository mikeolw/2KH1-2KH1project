using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 대화 로그(백로그) - 아래 화살표 키를 누르면 지금까지 나온 대사를 다시 볼 수 있는 창
// =====================================================================================
// ===== 언제 뜨나 =====
// 순수하게 대사가 진행 중일 때(암전 중이 아니고, 선택지/조사/미니게임/설정 등 다른 팝업이
// 안 떠 있을 때)만 아래 화살표 키로 열린다 - DialogueSystem.CanOpenOverlay 참고. 열려 있는
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
public class DialogueLogController : MonoBehaviour
{
    public static DialogueLogController Instance;

    private const string OverlayName = "__DialogueLogOverlay";

    private GameObject overlay;
    private TMP_Text logText;
    private ScrollRect scrollRect;

    // "화자 : 대사" 형태로 이미 완성된 문자열만 쌓아둔다. 화자가 없으면(지문) 대사만.
    private readonly List<string> entries = new List<string>();

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

        if (Input.GetKeyDown(KeyCode.DownArrow) &&
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

        entries.Add(string.IsNullOrEmpty(speaker) ? sentence : $"<b>{speaker}</b>\n{sentence}");

        // 열려 있는 동안에도 새 대사가 쌓일 수 있다(자동진행 중 아래 화살표 키를 누른 경우는
        // 이제 막혀 있지만, 만약을 대비해 열려 있으면 바로 반영해둔다).
        if (IsOpen) Refresh(scrollToBottom: true);
    }

    public void ClearLog()
    {
        entries.Clear();
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

    private void Refresh(bool scrollToBottom)
    {
        if (logText == null) return;

        logText.text = entries.Count == 0
            ? "아직 나온 대사가 없다."
            : string.Join("\n\n", entries);

        UIFontHelper.Apply(logText);

        if (scrollToBottom && scrollRect != null)
        {
            // Content 크기가 이번 프레임에 막 바뀐 상태라 한 프레임 늦게 반영되므로,
            // 강제로 레이아웃을 갱신한 뒤에 맨 아래로 내린다 (NotePanelUI.Refresh() 참고).
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
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

        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true; // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 화면 전체를 덮는 상자 -----
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overlay.transform, false);
        Stretch(box.GetComponent<RectTransform>());
        box.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

        // ----- 제목 -----
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(box.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(32f, 0f);
        titleRt.offsetMax = new Vector2(-32f, 0f);
        titleRt.sizeDelta = new Vector2(titleRt.sizeDelta.x, 56f);
        titleRt.anchoredPosition = new Vector2(0f, -16f);

        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "대화 기록";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = new Color(1f, 0.86f, 0.45f);
        titleText.raycastTarget = false;

        // ----- 글이 보이는 창(스크롤 영역) -----
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(box.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0f, 0f);
        viewportRt.anchorMax = new Vector2(1f, 1f);
        viewportRt.offsetMin = new Vector2(40f, 32f);
        viewportRt.offsetMax = new Vector2(-40f, -80f); // 위는 제목 자리
        var viewportImg = viewport.GetComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.01f); // 거의 투명하지만 마우스 휠 입력을 받는다

        // ----- 실제 글 -----
        var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 500f); // ContentSizeFitter가 글 길이에 맞춰 다시 계산한다
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        logText = content.AddComponent<TextMeshProUGUI>();
        logText.text = "아직 나온 대사가 없다.";
        logText.fontSize = 34; // 일반 대사창과 같은 크기 (DialogueSystem.SentenceFontSize 참고)
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.color = new Color(0.92f, 0.92f, 0.92f);
        logText.lineSpacing = 6f;
        logText.raycastTarget = false;
        logText.richText = true;
        logText.overflowMode = TextOverflowModes.Overflow;

        // ----- 스크롤 -----
        scrollRect = box.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        // ----- 글꼴 -----
        // 코드로 만든 글자는 기본 글꼴에 한글이 없어 깨지므로, 화면에서 한글이 잘 나오는
        // 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(overlay);

        overlay.SetActive(false);
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
