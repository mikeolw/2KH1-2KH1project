using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 조사기록(수첩) 화면 - 재훈이 조사하면서 적어나가는 메모를 보여준다
// =====================================================================================
// 퀵바의 Note 버튼을 누르면 열리는 탭이다.
//
// ===== 내용은 어디서 오나 =====
// 메모는 NoteManager가 관리한다. 이 스크립트는 그것을 화면에 그리기만 한다.
//   - 게임을 시작하면 회사 관련 기본 메모가 이미 적혀 있다(NoteEntries.csv의 Initial 항목).
//   - 조사를 하거나 아이템을 얻을 때마다 실시간으로 한 줄씩 추가된다.
//     CSV에 따로 써둔 문장이 있으면 그것을, 없으면 조사할 때 나온 내용을 그대로 적는다.
//   - #07(회사 잠입) 구간에서는 실시간 추가가 멈추고 구간이 끝날 때 한꺼번에 반영된다.
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

    private GameObject overlay;      // 메모 화면 전체
    private TMP_Text titleText;
    private TMP_Text noteText;
    private ScrollRect scrollRect;

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
                sb.AppendLine($"<b>{entry.chapter}</b>");
                lastChapter = entry.chapter;
            }

            sb.AppendLine($"  · {entry.text}");
            sb.AppendLine();
        }

        noteText.text = sb.ToString();

        // 글꼴은 갱신할 때마다 다시 확인한다.
        // (수첩이 대사창보다 먼저 만들어지면 처음에는 글꼴을 못 찾을 수 있다)
        UIFontHelper.Apply(noteText);
        UIFontHelper.Apply(titleText);

        // 새 메모가 추가되면 맨 아래(최신)로 스크롤을 내려준다.
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // ---------------------------------------------------------------------------------
    // 씬에 있던 원래 패널은 안 보이게 한다
    // ---------------------------------------------------------------------------------
    // 메모 화면을 캔버스에 따로 만들기 때문에, 씬의 NotePanel 자체는 눈에 보이면 안 된다.
    // 다만 UIManager가 이 패널을 켜고 끄면서 여닫음을 관리하므로 오브젝트 자체는 남겨둔다.
    private void HideOriginalPanelVisuals()
    {
        // 패널 배경을 투명하게 (클릭은 계속 막아서 뒤쪽 대사가 진행되지 않게 한다)
        var img = GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
        }

        // 프로토타입 시절 남아 있던 자식들(아이템 자리 등)을 꺼둔다.
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

        // 이미 만들어져 있으면 그것을 쓴다(패널을 여러 번 여닫아도 하나만 만든다).
        var existing = canvas.transform.Find(OverlayName);
        if (existing != null)
        {
            overlay = existing.gameObject;
            titleText = overlay.transform.Find("Box/Title")?.GetComponent<TMP_Text>();
            noteText = overlay.transform.Find("Box/Viewport/Content")?.GetComponent<TMP_Text>();
            scrollRect = overlay.GetComponentInChildren<ScrollRect>(true);
            return;
        }

        // ----- 화면 전체를 덮는 막 -----
        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        // ----- 가운데 수첩 상자 -----
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overlay.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(920f, 800f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.95f, 0.92f, 0.84f, 1f);   // 누런 종이

        // ----- 제목 -----
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(box.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(40f, 0f);
        titleRt.offsetMax = new Vector2(-40f, 0f);
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
        line.transform.SetParent(box.transform, false);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 1f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.pivot = new Vector2(0.5f, 1f);
        lineRt.offsetMin = new Vector2(40f, 0f);
        lineRt.offsetMax = new Vector2(-40f, 0f);
        lineRt.sizeDelta = new Vector2(lineRt.sizeDelta.x, 2f);
        lineRt.anchoredPosition = new Vector2(0f, -88f);
        var lineImg = line.GetComponent<Image>();
        lineImg.color = new Color(0.55f, 0.45f, 0.32f, 0.7f);
        lineImg.raycastTarget = false;

        // ----- 글이 보이는 창(스크롤 영역) -----
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(box.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0f, 0f);
        viewportRt.anchorMax = new Vector2(1f, 1f);
        viewportRt.offsetMin = new Vector2(40f, 90f);    // 아래는 닫기 버튼 자리
        viewportRt.offsetMax = new Vector2(-40f, -100f); // 위는 제목 자리
        var viewportImg = viewport.GetComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.01f);   // 거의 투명하지만 스크롤 입력을 받는다

        // ----- 실제 글 -----
        var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        // 높이를 미리 잡아둔다. 0으로 두면 글이 들어가도 잘려서 안 보인다.
        // 실제 높이는 아래 ContentSizeFitter가 글 길이에 맞춰 다시 계산한다.
        contentRt.sizeDelta = new Vector2(0f, 500f);

        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        noteText = content.AddComponent<TextMeshProUGUI>();
        noteText.text = "아직 적어둔 것이 없다.";
        noteText.fontSize = 24;
        noteText.alignment = TextAlignmentOptions.TopLeft;
        noteText.color = new Color(0.16f, 0.13f, 0.09f);
        noteText.lineSpacing = 8f;
        noteText.raycastTarget = false;
        noteText.richText = true;
        noteText.overflowMode = TextOverflowModes.Overflow;   // 길어지면 아래로 계속 이어진다

        // ----- 스크롤 -----
        scrollRect = box.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        // ----- 닫기 버튼 -----
        var closeGo = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(box.transform, false);
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 20f);
        closeRt.sizeDelta = new Vector2(200f, 50f);

        var closeBg = closeGo.GetComponent<Image>();
        closeBg.color = new Color(0.35f, 0.28f, 0.18f, 0.85f);
        closeBg.raycastTarget = true;

        var closeTextGo = new GameObject("Text", typeof(RectTransform));
        closeTextGo.transform.SetParent(closeGo.transform, false);
        Stretch(closeTextGo.GetComponent<RectTransform>());
        var closeLabel = closeTextGo.AddComponent<TextMeshProUGUI>();
        closeLabel.text = "닫기";
        closeLabel.fontSize = 24;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.color = new Color(0.96f, 0.94f, 0.88f);
        closeLabel.raycastTarget = false;

        var closeBtn = closeGo.GetComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        // 수첩을 닫는다 = 씬의 NotePanel을 끄는 것(UIManager가 그 상태로 여닫음을 판단한다)
        closeBtn.onClick.AddListener(() => gameObject.SetActive(false));

        // ----- 글꼴 -----
        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨지므로,
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
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
