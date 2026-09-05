using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 서류/사진 자료를 화면 가득 펼쳐서 보여주는 뷰어 (ItemModalController.cs보다 한 단계 위)
// =====================================================================================
// ===== ItemModalController와 뭐가 다른가? =====
//   ItemModalController : 조사할 때 뜨는 작은 팝업. "그림 한 장 + 이름 + 설명글".
//   DocumentViewerController(이 파일) : 서류나 사진을 "자료 자체를 읽는" 큰 화면으로 펼친다.
//                                       여러 장을 넘겨볼 수 있고, 바깥을 누르면 닫힌다.
//
// 시나리오상 서류(사건 자료, 회의 기록, 계약서)나 사진(SD카드 속 현장 사진)은 글씨를 읽거나
// 그림을 자세히 봐야 하는 자료라서, 작은 팝업이 아니라 전체 화면으로 봐야 한다.
//
// ===== 지금은 Placeholder다 =====
// 정식 아트(서류 종이 질감, 사진 앨범 프레임 등)가 아직 없으므로, 지금은
//   - 화면 전체를 덮는 어두운 배경
//   - 그 위에 자료 그림 한 장을 크게
//   - 여러 장이면 좌우에 "이전/다음" 버튼과 "2 / 4" 같은 쪽수 표시
// 만 보여준다. 바깥(어두운 배경)을 클릭하면 닫힌다.
// 나중에 아트가 나오면 이 스크립트는 그대로 두고 씬의 패널 모양만 꾸미면 된다.
//
// ===== 씬 배치 (유니티를 잘 모르는 팀원을 위한 설명) =====
// 아래 필드들을 인스펙터에서 연결해도 되고, 비워두면 게임 시작 시 Canvas 아래에
// 자동으로 만들어준다. 빈 GameObject에 이 스크립트만 붙여두면 일단 동작한다.
public class DocumentViewerController : MonoBehaviour
{
    public static DocumentViewerController Instance;

    [Header("UI 연결 (비워두면 자동 생성)")]
    [Tooltip("화면 전체를 덮는 루트. 이걸 켜고 끄는 것으로 뷰어를 여닫는다.")]
    public GameObject panel;
    [Tooltip("자료 그림이 표시될 Image")]
    public Image pageImage;
    [Tooltip("'2 / 4' 처럼 몇 번째 장인지 보여주는 텍스트")]
    public TMP_Text pageLabel;
    [Tooltip("자료 제목(아이템 이름)")]
    public TMP_Text titleLabel;
    [Tooltip("이전 장 버튼")]
    public Button prevButton;
    [Tooltip("다음 장 버튼")]
    public Button nextButton;
    [Tooltip("바깥 어두운 영역. 누르면 닫힌다.")]
    public Button backdropButton;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    // 지금 펼쳐 보고 있는 그림들과 몇 번째를 보고 있는지.
    private readonly List<Sprite> pages = new List<Sprite>();
    private int pageIndex;

    // 다른 스크립트(DialogueSystem 등)가 "지금 뷰어가 열려 있나?"를 확인할 때 쓴다.
    // 뷰어가 열려 있는 동안에는 뒤에 깔린 대사가 클릭으로 넘어가면 안 된다.
    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        EnsureUI();
        if (panel != null) panel.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 여닫기
    // ---------------------------------------------------------------------------------

    // 아이템 하나를 펼쳐 본다. ItemData.csv의 ViewerType이 None이면 아무것도 하지 않는다.
    // 반환값: 실제로 뷰어를 열었으면 true. (부르는 쪽에서 "뷰어를 안 열었으니 대신 설명 팝업을
    //         띄우자" 같은 판단을 할 수 있게 하기 위함 - InvestigationController 참고)
    public bool ShowItem(string itemId)
    {
        var info = ItemDatabase.Get(itemId);
        if (info == null) return false;
        if (info.viewerType == ItemDatabase.ItemViewerType.None) return false;

        // 펼쳐 볼 그림 목록을 만든다. ViewerImages가 비어 있으면 아이콘 한 장을 크게 보여준다.
        var sprites = new List<Sprite>();
        if (info.viewerImages != null && info.viewerImages.Length > 0)
        {
            foreach (string imageName in info.viewerImages)
            {
                Sprite s = IllustLoader.LoadObject(imageName);
                if (s != null) sprites.Add(s);
            }
        }
        if (sprites.Count == 0)
        {
            Sprite icon = info.GetIcon();
            if (icon != null) sprites.Add(icon);
        }

        if (sprites.Count == 0)
        {
            Debug.LogWarning($"[DocumentViewerController] '{itemId}'의 자료 그림을 하나도 찾지 못해 뷰어를 열지 않습니다.");
            return false;
        }

        Show(info.displayName, sprites);
        return true;
    }

    // 그림 목록을 직접 넘겨서 뷰어를 연다. (아이템이 아닌 자료를 보여줄 때도 쓸 수 있게 열어둠)
    public void Show(string title, List<Sprite> sprites)
    {
        if (panel == null) return;

        pages.Clear();
        pages.AddRange(sprites);
        pageIndex = 0;

        if (titleLabel != null) titleLabel.text = title;

        panel.SetActive(true);
        RefreshPage();
    }

    // 뷰어를 닫는다. 바깥 어두운 영역 클릭이나 닫기 버튼에 연결된다.
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        pages.Clear();
    }

    // ---------------------------------------------------------------------------------
    // 장 넘기기
    // ---------------------------------------------------------------------------------

    public void NextPage()
    {
        if (pageIndex < pages.Count - 1)
        {
            pageIndex++;
            RefreshPage();
        }
    }

    public void PrevPage()
    {
        if (pageIndex > 0)
        {
            pageIndex--;
            RefreshPage();
        }
    }

    // 현재 장을 화면에 반영하고, 버튼/쪽수 표시를 갱신한다.
    private void RefreshPage()
    {
        if (pages.Count == 0) return;

        pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);

        if (pageImage != null)
        {
            pageImage.sprite = pages[pageIndex];
            pageImage.enabled = true;
            // 자료는 원본 비율을 유지해야 글씨가 안 찌그러진다.
            pageImage.preserveAspect = true;
        }

        // 한 장짜리면 쪽수 표시와 넘김 버튼을 숨긴다(있어봐야 누를 게 없다).
        bool multiPage = pages.Count > 1;
        if (pageLabel != null)
        {
            pageLabel.gameObject.SetActive(multiPage);
            pageLabel.text = $"{pageIndex + 1} / {pages.Count}";
        }
        if (prevButton != null)
        {
            prevButton.gameObject.SetActive(multiPage);
            prevButton.interactable = pageIndex > 0;
        }
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(multiPage);
            nextButton.interactable = pageIndex < pages.Count - 1;
        }
    }

    // ---------------------------------------------------------------------------------
    // UI 자동 생성 (씬을 아직 안 꾸민 상태에서도 동작하게 하는 편의 기능)
    // ---------------------------------------------------------------------------------
    private void EnsureUI()
    {
        if (panel != null) return; // 인스펙터에서 이미 연결해뒀으면 그대로 쓴다

        if (targetCanvas == null) targetCanvas = FindObjectOfType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[DocumentViewerController] 씬에 Canvas가 없어 자료 뷰어를 만들 수 없습니다.");
            return;
        }

        // 루트 패널 (화면 전체를 덮음)
        panel = new GameObject("DocumentViewerPanel", typeof(RectTransform));
        panel.transform.SetParent(targetCanvas.transform, false);
        StretchFull(panel.GetComponent<RectTransform>());
        // 다른 UI보다 항상 위에 뜨도록 계층의 맨 마지막으로 보낸다.
        panel.transform.SetAsLastSibling();

        // 바깥 어두운 배경 (클릭하면 닫힘)
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(panel.transform, false);
        StretchFull(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
        backdropButton = backdrop.GetComponent<Button>();
        backdropButton.transition = Selectable.Transition.None; // 눌렀을 때 색이 변하면 어색하다
        backdropButton.onClick.AddListener(Hide);

        // 자료 그림
        var pageGo = new GameObject("PageImage", typeof(RectTransform), typeof(Image));
        pageGo.transform.SetParent(panel.transform, false);
        var pageRt = pageGo.GetComponent<RectTransform>();
        // 화면 가장자리에서 조금 띄워서 배치한다(바깥을 클릭해 닫을 여지를 남기기 위함).
        pageRt.anchorMin = new Vector2(0.08f, 0.10f);
        pageRt.anchorMax = new Vector2(0.92f, 0.90f);
        pageRt.offsetMin = Vector2.zero;
        pageRt.offsetMax = Vector2.zero;
        pageImage = pageGo.GetComponent<Image>();
        pageImage.preserveAspect = true;
        // 그림 자체는 클릭을 받지 않는다 -> 그림 위를 눌러도 뒤의 Backdrop이 눌려서 닫힌다.
        // (자료를 다 읽고 아무 데나 누르면 닫히는 게 자연스럽다)
        pageImage.raycastTarget = false;

        // 제목
        titleLabel = CreateLabel("TitleLabel", panel.transform,
            new Vector2(0.08f, 0.90f), new Vector2(0.92f, 0.97f), 32, TextAlignmentOptions.Center);

        // 쪽수
        pageLabel = CreateLabel("PageLabel", panel.transform,
            new Vector2(0.40f, 0.02f), new Vector2(0.60f, 0.09f), 26, TextAlignmentOptions.Center);

        // 이전/다음 버튼
        prevButton = CreateNavButton("PrevButton", panel.transform, "◀",
            new Vector2(0.10f, 0.02f), new Vector2(0.22f, 0.09f), PrevPage);
        nextButton = CreateNavButton("NextButton", panel.transform, "▶",
            new Vector2(0.78f, 0.02f), new Vector2(0.90f, 0.09f), NextPage);
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private TMP_Text CreateLabel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                 float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false; // 글자가 클릭을 가로채지 않게
        return tmp;
    }

    private Button CreateNavButton(string name, Transform parent, string label,
                                   Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        return btn;
    }
}
