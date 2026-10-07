using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
// ===== 화면 (Figma "Screen / Document Viewer" - 청회색 테마) =====
//   ▬ EVIDENCE · DOCUMENT                          [🔍 100% | 원래대로] [X]
//   유류품 목록                                                          ESC
//   ──────────────────────────────────────────────────────────────────────
//   ┌ ·                                                                · ┐
//   │ [‹]                  [   자료 그림   ]                         [›] │
//   └ ·                                                                · ┘
//   ──────────────────────────────────────────────────────────────────────
//   경찰서에 보관된 한성의 유류품 목록. …                        01 / 04
//                                                                ▬ ▬ ▬ ▬
//   ← → 넘기기 · 휠 확대·축소 · 끌어서 이동 · Esc 닫기
//
// 모양이 전부 선/상자뿐이라 그림 파일 없이 코드로 그린다 (드라이브에서 받을 것 없음).
// 화면 좌표는 Figma 1440x1080 기준 그대로다.
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
    [Tooltip("'01' 처럼 몇 번째 장인지 보여주는 텍스트")]
    public TMP_Text pageLabel;
    [Tooltip("자료 제목(아이템 이름)")]
    public TMP_Text titleLabel;
    [Tooltip("자료 그림 아래에 보여줄 설명 텍스트(ItemData.csv의 Description)")]
    public TMP_Text descriptionLabel;
    [Tooltip("이전 장 버튼")]
    public Button prevButton;
    [Tooltip("다음 장 버튼")]
    public Button nextButton;
    [Tooltip("바깥 어두운 영역. 누르면 닫힌다.")]
    public Button backdropButton;

    [Tooltip("자료 그림을 담는 틀. 확대했을 때 이 틀 밖으로 삐져나온 부분이 잘린다.")]
    public RectTransform pageViewport;
    [Tooltip("오른쪽 위 돋보기 칸. 누르면 원래 크기로 되돌린다.")]
    public Button zoomButton;
    [Tooltip("지금 확대 배율('100%')을 보여주는 텍스트")]
    public TMP_Text zoomLabel;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    // 지금 펼쳐 보고 있는 그림들과 몇 번째를 보고 있는지.
    private readonly List<Sprite> pages = new List<Sprite>();
    private int pageIndex;

    // 자동 생성한 화면에만 있는 것들 (머리말, 쪽수 막대, 넘김 화살표 모양)
    private TMP_Text kickerLabel;
    private TMP_Text pageTotalLabel;
    private RectTransform pageSegmentRoot;
    private readonly List<Image> pageSegments = new List<Image>();
    private ArrowView prevArrow, nextArrow;

    // ===== 색 (Figma 청회색 테마 - 대사 기록/세이브 화면과 같은 값) =====
    private static readonly Color AccentColor   = Hex(0x8FA4B7);
    private static readonly Color KickerColor   = Hex(0x6F8496);
    private static readonly Color TitleColor    = Hex(0xE4EAEF);
    private static readonly Color BodyColor     = Hex(0xD2DAE1);
    private static readonly Color HintColor     = Hex(0x7B8A98);
    private static readonly Color EscColor      = Hex(0x6C7C8B);
    private static readonly Color IconColor     = Hex(0xA9BAC9);
    private static readonly Color DividerColor  = Hex(0x2A3540);
    private static readonly Color BoxLineColor  = Hex(0x34414D);
    private static readonly Color BoxLineHover  = Hex(0x5A6B7B);
    private static readonly Color ViewportColor = Hex(0x0B1015);
    private static readonly Color ArrowFill     = new Color(0.063f, 0.086f, 0.114f, 0.85f);  // 10161D 85%
    private static readonly Color ArrowLineOn   = Hex(0x3A4855);
    private static readonly Color ArrowLineOff  = Hex(0x26303A);
    private static readonly Color ChevronOn     = Hex(0xC3D0DC);
    private static readonly Color ChevronOff    = Hex(0x3E4B57);

    // ===== 배치 (Figma 1440x1080 기준, 왼쪽 위가 0,0) =====
    private const float ScreenW = 1440f, ScreenH = 1080f;
    private const float Margin = 120f;                         // 좌우 여백
    private const float ContentW = ScreenW - Margin * 2f;      // 1200
    private const float ViewportY = 160f, ViewportH = 700f;
    private const float PageInsetX = 120f, PageInsetY = 24f;  // 자료 칸 안에서 그림이 차지하는 영역의 여백 (좌우 화살표 자리)

    // ===== 확대 / 축소 =====
    // 서류 글씨가 작아서 그냥 크게 띄우는 것만으로는 안 읽히는 경우가 있다. 뷰어가 열려 있는
    // 동안에는 언제나 휠로 확대/축소하고 끌어서 움직일 수 있다(창은 그대로, 그림만 커진다).
    // 돋보기 칸은 켜고 끄는 스위치가 아니라 "원래 크기로 되돌리기"다.
    private const float ZoomMin = 1f;      // 1배 = 원래 크기
    private const float ZoomMax = 8f;      // 초근접
    private const float ZoomStep = 0.5f;   // 휠 한 칸
    private float zoomScale = 1f;

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

    // ← → 로 장 넘기기. (Esc로 닫기는 UIManager가 한다)
    private void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) PrevPage();
        else if (Input.GetKeyDown(KeyCode.RightArrow)) NextPage();
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

        string kicker = info.viewerType == ItemDatabase.ItemViewerType.Photo
            ? "EVIDENCE  ·  PHOTO" : "EVIDENCE  ·  DOCUMENT";
        Show(info.displayName, info.description, sprites, kicker);
        return true;
    }

    // 그림 목록을 직접 넘겨서 뷰어를 연다. (아이템이 아닌 자료를 보여줄 때도 쓸 수 있게 열어둠)
    public void Show(string title, string description, List<Sprite> sprites, string kicker = "EVIDENCE  ·  DOCUMENT")
    {
        if (panel == null) return;

        pages.Clear();
        pages.AddRange(sprites);
        pageIndex = 0;

        if (titleLabel != null) titleLabel.text = title;
        if (descriptionLabel != null) descriptionLabel.text = description;
        if (kickerLabel != null) kickerLabel.text = kicker;

        panel.SetActive(true);
        // 조사 상세는 퀵바/알림 등 다른 UI보다 항상 위에. 같은 Canvas 형제 순서만으로는
        // 나중에 만들어진 UI가 다시 위로 올라오므로 정렬 순서를 따로 준다 (UITopLayer.cs 참고).
        UITopLayer.MakeTopmost(panel, UITopLayer.InvestigationDetailOrder);
        BuildPageSegments();
        RefreshPage();
    }

    // 뷰어를 닫는다. 바깥 어두운 영역 클릭이나 닫기 버튼에 연결된다.
    public void Hide()
    {
        ResetZoom();   // 확대한 채로 닫으면 다음에 열 때도 확대되어 있다
        if (panel != null) panel.SetActive(false);
        pages.Clear();
    }

    // ---------------------------------------------------------------------------------
    // 돋보기 (확대 / 축소)
    // ---------------------------------------------------------------------------------
    // 뷰어가 열려 있는 동안에는 따로 켤 것 없이 언제나 쓸 수 있다.
    //   - 마우스 휠 : 확대 / 축소 (100% ~ 800%)
    //   - 끌기      : 확대한 그림을 움직여 원하는 곳을 본다
    //   - 돋보기 칸 : 원래 크기로 되돌리기
    // 그래서 자료 그림이 마우스 입력을 직접 받는다(raycastTarget). "아무 데나 누르면 닫힌다"는
    // 기존 동작은 ZoomInputRelay.OnPointerClick이 대신 처리한다.

    // 돋보기 칸: 원래 크기(100%)로 되돌린다.
    public void ResetZoom()
    {
        zoomScale = 1f;
        if (pageImage != null)
        {
            pageImage.rectTransform.localScale = Vector3.one;
            pageImage.rectTransform.anchoredPosition = Vector2.zero;
        }
        UpdateZoomLabel();
    }

    // 지금 확대해서 보고 있는 중인가. 확대 중일 때는 그림을 눌러도 뷰어가 닫히지 않는다
    // (글씨를 보려고 누른 것이지 닫으려는 게 아니기 때문).
    public bool IsZoomed => zoomScale > ZoomMin + 0.001f;

    // delta만큼 배율을 바꾼다(휠 한 칸).
    public void AddZoom(float delta)
    {
        if (pageImage == null) return;
        zoomScale = Mathf.Clamp(zoomScale + delta, ZoomMin, ZoomMax);
        pageImage.rectTransform.localScale = Vector3.one * zoomScale;
        ClampPan();
        UpdateZoomLabel();
    }

    // 확대된 그림을 끌어서 움직인다.
    public void PanBy(Vector2 delta)
    {
        if (pageImage == null) return;
        // 끌린 거리는 화면 픽셀 단위로 오는데, 캔버스가 확대/축소되어 있으면 UI 좌표와
        // 배율이 다르다. 나눠주지 않으면 손보다 그림이 빠르거나 느리게 따라온다.
        float canvasScale = targetCanvas != null ? targetCanvas.scaleFactor : 1f;
        if (canvasScale <= 0f) canvasScale = 1f;
        pageImage.rectTransform.anchoredPosition += delta / canvasScale;
        ClampPan();
    }

    // 그림을 너무 많이 끌어 화면 밖으로 사라지지 않게 가둔다.
    // (그림 영역을 키운 만큼만 움직일 수 있다 - 커진 그림의 가장자리가 자료 칸 가장자리까지 오면 멈춤)
    private void ClampPan()
    {
        if (pageImage == null || pageViewport == null) return;
        Vector2 view = pageViewport.rect.size;
        Vector2 page = pageImage.rectTransform.rect.size;
        Vector2 limit = (page * zoomScale - view) * 0.5f;
        limit = new Vector2(Mathf.Max(0f, limit.x), Mathf.Max(0f, limit.y));
        Vector2 pos = pageImage.rectTransform.anchoredPosition;
        pageImage.rectTransform.anchoredPosition =
            new Vector2(Mathf.Clamp(pos.x, -limit.x, limit.x), Mathf.Clamp(pos.y, -limit.y, limit.y));
    }

    private void UpdateZoomLabel()
    {
        if (zoomLabel == null) return;
        zoomLabel.text = $"{Mathf.RoundToInt(zoomScale * 100)}%";
        // 확대 중이면 배율 글자를 강조색으로 - "지금 커져 있다"는 걸 한눈에 알게
        zoomLabel.color = IsZoomed ? AccentColor : BodyColor;
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

        // 장을 넘기면 확대는 풀어준다. 확대된 채로 넘어가면 엉뚱한 구석이 보여 당황스럽다.
        ResetZoom();

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
            // 자동 생성 화면은 "01" + "/ 04" 두 글자로 나눠 그린다. 인스펙터로 연결한 예전 화면은 한 줄로.
            pageLabel.text = pageTotalLabel != null ? (pageIndex + 1).ToString("00") : $"{pageIndex + 1} / {pages.Count}";
        }
        if (pageTotalLabel != null)
        {
            pageTotalLabel.gameObject.SetActive(multiPage);
            pageTotalLabel.text = "/ " + pages.Count.ToString("00");
        }
        if (pageSegmentRoot != null)
        {
            pageSegmentRoot.gameObject.SetActive(multiPage);
            for (int i = 0; i < pageSegments.Count; i++)
                pageSegments[i].color = i == pageIndex ? AccentColor : BoxLineColor;
        }
        if (prevButton != null)
        {
            prevButton.gameObject.SetActive(multiPage);
            prevButton.interactable = pageIndex > 0;
            prevArrow?.Refresh();
        }
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(multiPage);
            nextButton.interactable = pageIndex < pages.Count - 1;
            nextArrow?.Refresh();
        }
    }

    // 쪽수 막대(▬ ▬ ▬ ▬)를 장 수만큼 만든다. 오른쪽 끝을 맞춰 왼쪽으로 늘어난다.
    private void BuildPageSegments()
    {
        if (pageSegmentRoot == null) return;
        foreach (var s in pageSegments) if (s != null) Destroy(s.gameObject);
        pageSegments.Clear();

        const float segW = 26f, segH = 3f, gap = 6f;
        int count = pages.Count;
        float total = count * segW + (count - 1) * gap;
        for (int i = 0; i < count; i++)
        {
            var img = AddRect(pageSegmentRoot, "Seg" + i, 0f, 0f, segW, segH, BoxLineColor);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-(total - segW - i * (segW + gap)), 0f);
            pageSegments.Add(img);
        }
    }

    // ---------------------------------------------------------------------------------
    // UI 자동 생성 (Figma "Screen / Document Viewer")
    // ---------------------------------------------------------------------------------
    private void EnsureUI()
    {
        if (panel != null) return; // 인스펙터에서 이미 연결해뒀으면 그대로 쓴다

        // FindAnyObjectByType: 씬에서 Canvas 아무거나 하나를 찾는다.
        // (예전 FindObjectOfType은 유니티 6에서 사용 중단되어 경고가 뜬다)
        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[DocumentViewerController] 씬에 Canvas가 없어 자료 뷰어를 만들 수 없습니다.");
            return;
        }

        // 루트 패널 (화면 전체를 덮음)
        panel = new GameObject("DocumentViewerPanel", typeof(RectTransform));
        panel.transform.SetParent(targetCanvas.transform, false);
        StretchFull(panel.GetComponent<RectTransform>());
        panel.transform.SetAsLastSibling();

        // 바깥 검은 막 (클릭하면 닫힘) - 퀵바 창/환경설정과 같은 97%
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(panel.transform, false);
        StretchFull(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.97f);
        backdropButton = backdrop.GetComponent<Button>();
        backdropButton.transition = Selectable.Transition.None; // 눌렀을 때 색이 변하면 어색하다
        backdropButton.onClick.AddListener(Hide);

        // Figma 1440x1080 화면을 그대로 옮겨 놓을 틀 (화면 정가운데)
        var boxGo = new GameObject("Box", typeof(RectTransform));
        boxGo.transform.SetParent(panel.transform, false);
        var box = (RectTransform)boxGo.transform;
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(ScreenW, ScreenH);

        // ----- 머리말 -----
        AddRect(box, "TopAccent", Margin, 36f, 120f, 3f, AccentColor);
        kickerLabel = CreateText(box, "Kicker", "EVIDENCE  ·  DOCUMENT", 14, FontStyles.Bold, KickerColor);
        kickerLabel.characterSpacing = 20f;
        PlaceTopLeft(kickerLabel.rectTransform, Margin, 52f, 600f, 20f);
        titleLabel = CreateText(box, "TitleLabel", "", 36, FontStyles.Bold, TitleColor);
        PlaceTopLeft(titleLabel.rectTransform, Margin, 68f, 900f, 54f);

        // 돋보기 칸 [🔍 100% | 원래대로] - 누르면 원래 크기로
        var zoomBox = CreateOutlinedButton(box, "ZoomButton", 1080f, 60f, 176f, 48f, ResetZoom, out var zoomBorder);
        zoomButton = zoomBox;
        var mag = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        mag.transform.SetParent(zoomBox.transform, false);
        PlaceTopLeft((RectTransform)mag.transform, 14f, 12f, 24f, 24f);
        var magImg = mag.GetComponent<Image>();
        magImg.sprite = CreateMagnifierSprite();
        magImg.color = IconColor;
        magImg.raycastTarget = false;
        zoomLabel = CreateText(zoomBox.transform, "ZoomLabel", "100%", 16, FontStyles.Bold, BodyColor);
        zoomLabel.alignment = TextAlignmentOptions.MidlineLeft;
        PlaceTopLeft(zoomLabel.rectTransform, 46f, 0f, 54f, 48f);
        AddRect(zoomBox.transform, "Sep", 100f, 14f, 1f, 20f, BoxLineColor);
        var reset = CreateText(zoomBox.transform, "ResetLabel", "원래대로", 14, FontStyles.Normal, HintColor);
        reset.alignment = TextAlignmentOptions.MidlineLeft;
        PlaceTopLeft(reset.rectTransform, 112f, 0f, 60f, 48f);
        zoomBox.gameObject.AddComponent<HoverLine>().Init(zoomBorder, new Graphic[] { reset }, HintColor, IconColor);

        // 닫기 X + ESC
        var close = CreateOutlinedButton(box, "CloseButton", 1272f, 60f, 48f, 48f, Hide, out var closeBorder);
        var crossA = AddRect(close.transform, "CrossA", 0f, 0f, 16f, 1.6f, IconColor);
        var crossB = AddRect(close.transform, "CrossB", 0f, 0f, 16f, 1.6f, IconColor);
        CenterRotated(crossA.rectTransform, Vector2.zero, 45f);
        CenterRotated(crossB.rectTransform, Vector2.zero, -45f);
        close.gameObject.AddComponent<HoverLine>().Init(closeBorder, new Graphic[] { crossA, crossB }, IconColor, TitleColor);
        var esc = CreateText(box, "EscHint", "ESC", 11, FontStyles.Bold, EscColor);
        esc.alignment = TextAlignmentOptions.Top;
        esc.characterSpacing = 10f;
        PlaceTopLeft(esc.rectTransform, 1272f, 114f, 48f, 16f);

        AddRect(box, "HeadDivider", Margin, 136f, ContentW, 1f, DividerColor);

        // ----- 자료 칸 -----
        // RectMask2D: 확대했을 때 이 칸 밖으로 나간 부분을 잘라서 제목이나 설명 위를 덮지 않게 한다.
        var viewGo = new GameObject("PageViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewGo.transform.SetParent(box, false);
        pageViewport = (RectTransform)viewGo.transform;
        PlaceTopLeft(pageViewport, Margin, ViewportY, ContentW, ViewportH);
        var viewImg = viewGo.GetComponent<Image>();
        viewImg.color = ViewportColor;
        viewImg.raycastTarget = false;   // 그림 밖 빈 곳을 누르면 뒤의 검은 막이 받아서 닫힌다
        AddBorder(pageViewport, DividerColor, 1f);

        // ===== 자료 그림 =====
        // 서류 그림은 A4 세로 비율(992x1403)이라 preserveAspect를 켜두면 세로 길이에 맞춰
        // 들어간다(약 650px 높이). 가로를 넉넉히 준 것은 사진처럼 가로로 긴 자료도 같은 칸을
        // 쓰기 때문이다 - 세로 자료는 preserveAspect가 알아서 가운데로 모아준다.
        // 좌우 여백은 넘김 화살표 자리다.
        var pageGo = new GameObject("PageImage", typeof(RectTransform), typeof(Image));
        pageGo.transform.SetParent(viewGo.transform, false);
        var pageRt = pageGo.GetComponent<RectTransform>();
        pageRt.anchorMin = Vector2.zero;
        pageRt.anchorMax = Vector2.one;
        pageRt.offsetMin = new Vector2(PageInsetX, PageInsetY);
        pageRt.offsetMax = new Vector2(-PageInsetX, -PageInsetY);
        // 확대는 가운데를 기준으로 커져야 자연스럽다.
        pageRt.pivot = new Vector2(0.5f, 0.5f);
        pageImage = pageGo.GetComponent<Image>();
        pageImage.preserveAspect = true;
        // 휠과 끌기를 받으려면 그림이 마우스 입력을 받아야 한다. 대신 "그냥 한 번 누르면
        // 닫힌다"는 기존 동작은 아래 ZoomInputRelay가 대신 처리한다(끌지 않았고 확대 중도
        // 아닐 때만 닫는다).
        pageImage.raycastTarget = true;
        pageGo.AddComponent<ZoomInputRelay>().owner = this;
        // 그림 그림자 (종이가 떠 있는 느낌). 따로 상자를 깔면 확대/이동 때 그림과 따로 놀기
        // 때문에, 그림 자체에 붙는 Shadow 효과를 쓴다.
        var shadow = pageGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -8f);

        // 네 모서리 괄호 (조사 화면의 마우스 올림 표시와 같은 모양)
        const float bl = 28f, bt = 2f, inset = 16f;
        var bracketColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.9f);
        for (int i = 0; i < 4; i++)
        {
            bool right = i % 2 == 1, bottom = i >= 2;
            float x = right ? ContentW - inset - bl : inset;
            float y = bottom ? ViewportH - inset - bt : inset;
            AddRect(pageViewport, "Bracket" + i + "H", x, y, bl, bt, bracketColor);
            float vx = right ? ContentW - inset - bt : inset;
            float vy = bottom ? ViewportH - inset - bl : inset;
            AddRect(pageViewport, "Bracket" + i + "V", vx, vy, bt, bl, bracketColor);
        }

        // 좌우 넘김 화살표 (자료 칸 안, 세로 가운데)
        prevButton = CreateArrow(pageViewport, "PrevButton", 40f, true, PrevPage, out prevArrow);
        nextButton = CreateArrow(pageViewport, "NextButton", ContentW - 40f - 56f, false, NextPage, out nextArrow);

        // ----- 아래쪽 -----
        AddRect(box, "FootDivider", Margin, 884f, ContentW, 1f, DividerColor);

        descriptionLabel = CreateText(box, "DescriptionLabel", "", 20, FontStyles.Normal, BodyColor);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;
        descriptionLabel.lineSpacing = 20f;
        PlaceTopLeft(descriptionLabel.rectTransform, Margin, 904f, 900f, 100f);

        // 쪽수 "01 / 04" - 오른쪽 끝 맞춤
        pageTotalLabel = CreateText(box, "PageTotal", "/ 01", 16, FontStyles.Bold, KickerColor);
        pageTotalLabel.alignment = TextAlignmentOptions.BottomRight;
        PlaceTopLeft(pageTotalLabel.rectTransform, ScreenW - Margin - 60f, 900f, 60f, 36f);
        pageLabel = CreateText(box, "PageLabel", "01", 28, FontStyles.Bold, TitleColor);
        pageLabel.alignment = TextAlignmentOptions.BottomRight;
        PlaceTopLeft(pageLabel.rectTransform, ScreenW - Margin - 60f - 8f - 80f, 898f, 80f, 40f);

        var segGo = new GameObject("PageSegments", typeof(RectTransform));
        segGo.transform.SetParent(box, false);
        pageSegmentRoot = (RectTransform)segGo.transform;
        PlaceTopLeft(pageSegmentRoot, Margin, 948f, ContentW, 3f);

        var hint = CreateText(box, "FootHint", "← → 넘기기   ·   휠 확대·축소   ·   끌어서 이동   ·   Esc 닫기",
                              15, FontStyles.Normal, HintColor);
        PlaceTopLeft(hint.rectTransform, Margin, 1020f, ContentW, 22f);

        UpdateZoomLabel();

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨져 보인다.
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
    }

    // ---------------------------------------------------------------------------------
    // 만들기 도우미
    // ---------------------------------------------------------------------------------
    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Figma 좌표(부모 왼쪽 위 기준, 아래로 +)로 놓는다.
    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모 가운데 기준으로 놓고 돌린다 (X / 꺾쇠 선 그리기용).
    private static void CenterRotated(RectTransform rt, Vector2 offset, float angle)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    private static Image AddRect(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        PlaceTopLeft((RectTransform)go.transform, x, y, w, h);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 상자 테두리를 얇은 선 4개로 그린다. 색을 바꿀 수 있게 4개를 돌려준다.
    private static Image[] AddBorder(RectTransform target, Color color, float t)
    {
        var lines = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Border" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(target, false);
            var rt = (RectTransform)go.transform;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, t); break; // 위
                case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(0, t); break; // 아래
                case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(t, 0); break; // 왼쪽
                default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(t, 0); break; // 오른쪽
            }
            rt.anchoredPosition = Vector2.zero;
            lines[i] = go.GetComponent<Image>();
            lines[i].color = color;
            lines[i].raycastTarget = false;
        }
        return lines;
    }

    // 테두리만 있는 버튼 (돋보기 칸, 닫기). 클릭은 투명한 바탕이 받는다.
    private static Button CreateOutlinedButton(Transform parent, string name, float x, float y, float w, float h,
                                               UnityEngine.Events.UnityAction onClick, out Image[] border)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        PlaceTopLeft(rt, x, y, w, h);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);   // 클릭만 받는 투명 바탕 (Linear 색 공간이라 0.01도 회색으로 보인다 → 0)
        border = AddBorder(rt, BoxLineColor, 1f);
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;   // 마우스 올림은 HoverLine이 테두리/글자 색으로 보여준다
        btn.onClick.AddListener(onClick);
        return btn;
    }

    // 넘김 화살표 56x128 (조사 화면 양옆 화살표와 같은 크기).
    private static Button CreateArrow(RectTransform viewport, string name, float x, bool left,
                                      UnityEngine.Events.UnityAction onClick, out ArrowView view)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(viewport, false);
        var rt = (RectTransform)go.transform;
        PlaceTopLeft(rt, x, (ViewportH - 128f) * 0.5f, 56f, 128f);
        var bg = go.GetComponent<Image>();
        bg.color = ArrowFill;
        var border = AddBorder(rt, ArrowLineOn, 1f);

        // 꺾쇠 ‹ › 를 짧은 선 두 개로
        float dir = left ? -1f : 1f;
        var a = AddRect(rt, "ChevronA", 0f, 0f, 12f, 2f, ChevronOn);
        var b = AddRect(rt, "ChevronB", 0f, 0f, 12f, 2f, ChevronOn);
        CenterRotated(a.rectTransform, new Vector2(0f, 4f), dir * -45f);
        CenterRotated(b.rectTransform, new Vector2(0f, -4f), dir * 45f);

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
        view = go.AddComponent<ArrowView>();
        view.Init(btn, border, new Graphic[] { a, b });
        return btn;
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
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false; // 글자가 클릭을 가로채지 않게
        return tmp;
    }

    // ===== 돋보기 아이콘을 코드로 그린다 =====
    // 아이콘 PNG를 따로 두면 그림 폴더(드라이브 공유)에 의존하게 되고, 파일이 없는 팀원
    // 화면에서는 버튼이 통째로 안 보인다. 동그라미와 손잡이뿐인 단순한 모양이라 코드로 그린다.
    private static Sprite magnifierSprite;
    private static Sprite CreateMagnifierSprite()
    {
        if (magnifierSprite != null) return magnifierSprite;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        var pixels = new Color[S * S];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        // 렌즈: 가운데 살짝 위쪽의 동그란 테두리
        Vector2 center = new Vector2(S * 0.42f, S * 0.58f);
        float radius = S * 0.26f;
        float thickness = S * 0.075f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                if (Mathf.Abs(d - radius) <= thickness * 0.5f) pixels[y * S + x] = Color.white;
            }
        }

        // 손잡이: 렌즈 오른쪽 아래로 뻗는 굵은 선
        Vector2 from = center + new Vector2(radius * 0.72f, -radius * 0.72f);
        Vector2 to = new Vector2(S * 0.86f, S * 0.14f);
        for (float t = 0f; t <= 1f; t += 0.002f)
        {
            Vector2 p = Vector2.Lerp(from, to, t);
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = Mathf.RoundToInt(p.x) + dx;
                    int y = Mathf.RoundToInt(p.y) + dy;
                    if (x < 0 || y < 0 || x >= S || y >= S) continue;
                    if (dx * dx + dy * dy > 6) continue;
                    pixels[y * S + x] = Color.white;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        magnifierSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        return magnifierSprite;
    }

    // ===== 마우스를 올리면 테두리와 글자/아이콘을 밝게 (돋보기 칸, 닫기) =====
    private class HoverLine : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image[] border;
        private Graphic[] marks;
        private Color markNormal, markHover;

        public void Init(Image[] border, Graphic[] marks, Color markNormal, Color markHover)
        {
            this.border = border; this.marks = marks; this.markNormal = markNormal; this.markHover = markHover;
        }

        public void OnPointerEnter(PointerEventData e) => Apply(true);
        public void OnPointerExit(PointerEventData e) => Apply(false);
        private void OnDisable() => Apply(false);   // 닫았다 열 때 밝은 색이 남지 않게

        private void Apply(bool hover)
        {
            if (border != null) foreach (var l in border) if (l != null) l.color = hover ? BoxLineHover : BoxLineColor;
            if (marks != null) foreach (var m in marks) if (m != null) m.color = hover ? markHover : markNormal;
        }
    }

    // ===== 넘김 화살표 모양 (켜짐 / 꺼짐 / 마우스 올림) =====
    // 첫 장에서는 ‹ 가, 마지막 장에서는 › 가 흐려진다.
    private class ArrowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Button button;
        private Image[] border;
        private Graphic[] chevron;
        private bool hover;

        public void Init(Button button, Image[] border, Graphic[] chevron)
        {
            this.button = button; this.border = border; this.chevron = chevron;
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hover = false; Refresh(); }
        private void OnDisable() { hover = false; Refresh(); }

        public void Refresh()
        {
            bool on = button != null && button.interactable;
            Color line = !on ? ArrowLineOff : hover ? BoxLineHover : ArrowLineOn;
            Color mark = !on ? ChevronOff : hover ? TitleColor : ChevronOn;
            if (border != null) foreach (var l in border) if (l != null) l.color = line;
            if (chevron != null) foreach (var c in chevron) if (c != null) c.color = mark;
        }
    }

    // ===== 자료 그림 위의 휠 / 끌기 / 클릭을 컨트롤러로 넘겨주는 부품 =====
    // 그림이 마우스 입력을 받게 되면서 뒤의 Backdrop이 가려지므로, "아무 데나 누르면 닫힌다"를
    // 여기서 대신 해준다. 단 두 경우에는 닫지 않는다:
    //   - 끌어서 그림을 움직인 경우 (놓는 순간 창이 닫히면 황당하다)
    //   - 확대해서 보고 있는 경우 (글씨를 보려고 누른 것이지 닫으려는 게 아니다)
    private class ZoomInputRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        public DocumentViewerController owner;

        // 이번에 누른 동안 얼마나 끌었는지. 손이 살짝 떨린 정도는 클릭으로 봐준다.
        private const float DragSlack = 8f;
        private float draggedDistance;

        // ===== 누를 때마다 처음부터 다시 센다 =====
        // 예전에는 OnPointerClick에서만 0으로 되돌렸는데, 그림 밖에서 손을 떼면
        // OnPointerClick이 오지 않아 끌린 거리가 그대로 남았다. 그러면 그 다음에 제대로
        // 한 번 눌러도 "아까 끌었잖아"로 오해해서 뷰어가 닫히지 않았다.
        public void OnPointerDown(PointerEventData e)
        {
            draggedDistance = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (owner == null) return;
            draggedDistance += e.delta.magnitude;
            owner.PanBy(e.delta);
        }

        public void OnScroll(PointerEventData e)
        {
            // 휠을 안 굴렸는데 이벤트만 온 경우가 있다. Mathf.Sign(0)은 1이라 그냥 두면
            // 가만히 있어도 확대된다.
            if (owner == null || Mathf.Approximately(e.scrollDelta.y, 0f)) return;
            owner.AddZoom(Mathf.Sign(e.scrollDelta.y) * ZoomStep);
        }

        public void OnPointerClick(PointerEventData e)
        {
            float moved = draggedDistance;
            draggedDistance = 0f;
            if (owner == null) return;
            if (moved > DragSlack) return;   // 끌었던 것이므로 닫지 않는다
            if (owner.IsZoomed) return;      // 확대해서 보는 중이므로 닫지 않는다
            owner.Hide();
        }
    }
}
