using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using static UIKit;   // CreateLabel, PlaceTopLeft, AddRect ... (UIKit.cs)

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
//   ← → 넘기기 · 휠/↑↓ 위아래 · Ctrl+휠 확대·축소 · 끌어서 이동 · Esc 닫기
//
// 모양이 전부 선/상자뿐이라 그림 파일 없이 코드로 그린다 (드라이브에서 받을 것 없음).
// 색은 UITheme, 머리말·닫기 X·테두리 버튼 같은 공용 모양은 UIKit을 쓴다.
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

    // Figma 1440x1080 틀. 말풍선처럼 "화면에 붙는" 것을 여기 자식으로 넣는다.
    private RectTransform screenBox;
    // 형광펜 + 차재훈 독백 (DocumentHighlightPlayer.cs). 표가 비어 있으면 아무것도 하지 않는다.
    private DocumentHighlightPlayer highlights;

    // ===== 색 =====
    // 공용 색은 UITheme.cs. 여기에는 이 화면의 넘김 화살표에만 쓰는 색만 둔다.
    private static readonly Color ArrowFill    = UITheme.Hex(0x10161D, 0.85f);
    private static readonly Color ArrowLineOff = UITheme.Hex(0x26303A);
    private static readonly Color ChevronOn    = UITheme.Hex(0xC3D0DC);
    private static readonly Color ChevronOff   = UITheme.Hex(0x3E4B57);

    // ===== 배치 (Figma 1440x1080 기준, 왼쪽 위가 0,0) =====
    private const float ScreenW = 1440f, ScreenH = 1080f;
    private const float Margin = 120f;                         // 좌우 여백
    private const float ContentW = ScreenW - Margin * 2f;      // 1200
    private const float ViewportY = 160f, ViewportH = 700f;
    // 자료 칸 안에서 그림이 차지하는 영역의 여백.
    // 예전에는 좌우 120씩 비워 넘김 화살표를 그 자리에 넣었다. 그런데 그러면 서류가 쓸 수
    // 있는 가로가 960px뿐이라 글씨가 작았다. 화살표를 화면 바깥 여백(x 32, 1352)으로 옮기고
    // 여기는 48까지 줄여서, 서류가 1104px을 쓴다. (네 모서리 괄호가 inset 16~44에 있으므로
    // 48보다 더 줄이면 괄호가 종이 위에 올라탄다)
    private const float PageInsetX = 48f, PageInsetY = 24f;
    private const float ArrowW = 56f, ArrowH = 128f;

    // ===== 확대 / 축소 / 위아래 굴리기 =====
    // ===== 1배 = "폭 맞춤" =====
    // 세로로 긴 서류(A4)를 쪽 전체가 들어가게 넣으면 가로 폭의 절반도 못 쓴다. 그러면 본문
    // 11pt가 화면에서 8.5px밖에 안 돼서 읽을 수가 없다. 그래서 서류는 PDF를 보듯이
    // "가로 폭을 칸에 꽉 맞추고" 위아래로 굴려 읽는다. A4 11pt가 17.7px이 된다.
    // 쪽 전체를 보고 싶으면 휠을 내려 축소하면 된다(zoomMin까지).
    // 사진처럼 가로로 넓거나 네모난 자료는 예전처럼 한 번에 다 보인다(ApplyPageSize 참고).
    //
    //   휠        : 위아래로 굴리기
    //   Ctrl+휠   : 확대 / 축소
    //   끌기      : 그림을 움직인다
    //   ↑ ↓       : 위아래로 굴리기
    //   돋보기 칸 : 폭 맞춤(1배) + 맨 위로 되돌리기
    private const float ZoomMax = 8f;      // 초근접
    private const float ZoomStep = 0.5f;   // Ctrl+휠 한 칸
    private const float ZoomFloor = 0.2f;  // 아무리 줄여도 이보다 작게는 안 만든다
    private const float WheelScroll = 90f; // 휠 한 칸에 위아래로 움직이는 거리
    // ↑ ↓ 를 누르고 있을 때 1초에 움직이는 거리. 프레임마다 고정 거리로 움직이면
    // 컴퓨터가 빠를수록(fps가 높을수록) 더 빨리 굴러가므로 시간에 맞춘다.
    private const float KeyScrollPerSecond = 1200f;
    private float zoomScale = 1f;
    // 이 자료에서 더 줄일 수 있는 한계. "쪽 전체가 딱 보이는 배율"로 ApplyPageSize가 정한다.
    // (가로로 넓은 자료는 1배가 이미 전체라서 1)
    private float zoomMin = 1f;

    // 다른 스크립트(DialogueSystem 등)가 "지금 뷰어가 열려 있나?"를 확인할 때 쓴다.
    // 뷰어가 열려 있는 동안에는 뒤에 깔린 대사가 클릭으로 넘어가면 안 된다.
    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        EnsureUI();
        if (panel != null) panel.SetActive(false);

        // 조사 상세는 퀵바·알림·가방보다 항상 위 층 (UILayers.cs 참고). Esc = 닫기.
        // (자동으로 만든 화면은 CreateWindow가 이미 했고, 인스펙터로 연결한 화면을 위해 한 번 더 건다)
        UILayers.Place(panel, UILayer.Detail);
        ModalStack.Register(panel, Hide);
    }

    // ← → 로 장 넘기기, ↑ ↓ 로 위아래 굴리기.
    // 위에 다른 창(세이브 창 등)이 떠 있으면 그쪽 차례이므로 무시한다.
    private void Update()
    {
        if (!IsOpen || ModalStack.IsCoveredByOther(panel)) return;
        // 스페이스/엔터 = 다음 줄 읽기. 읽을 것이 없으면 아무 일도 일어나지 않는다.
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            AdvanceReading();
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) PrevPage();
        else if (Input.GetKeyDown(KeyCode.RightArrow)) NextPage();
        // 위아래는 꾹 누르고 있으면 계속 굴러가야 한다(GetKeyDown이 아니라 GetKey).
        else if (Input.GetKey(KeyCode.UpArrow)) ScrollBy(-KeyScrollPerSecond * Time.unscaledDeltaTime);
        else if (Input.GetKey(KeyCode.DownArrow)) ScrollBy(KeyScrollPerSecond * Time.unscaledDeltaTime);
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

        bool isPhoto = info.viewerType == ItemDatabase.ItemViewerType.Photo;
        string kicker = isPhoto ? "EVIDENCE  ·  PHOTO" : "EVIDENCE  ·  DOCUMENT";
        Show(info.displayName, info.description, sprites, kicker, isPhoto);
        return true;
    }

    // 그림 목록을 직접 넘겨서 뷰어를 연다. (아이템이 아닌 자료를 보여줄 때도 쓸 수 있게 열어둠)
    // photo = true 면 글을 읽는 자료가 아니라 한눈에 보는 사진이라는 뜻이다(ApplyPageSize 참고).
    public void Show(string title, string description, List<Sprite> sprites,
                     string kicker = "EVIDENCE  ·  DOCUMENT", bool photo = false)
    {
        if (panel == null) return;

        fitWholePage = photo;
        pages.Clear();
        pages.AddRange(sprites);
        pageIndex = 0;

        if (titleLabel != null) titleLabel.text = title;
        if (descriptionLabel != null) descriptionLabel.text = description;
        if (kickerLabel != null) kickerLabel.text = kicker;

        panel.SetActive(true);
        BuildPageSegments();
        RefreshPage();
    }

    // 뷰어를 닫는다. 바깥 어두운 영역 클릭이나 닫기 버튼에 연결된다.
    public void Hide()
    {
        bool wasOpen = IsOpen;
        if (highlights != null) highlights.Close();
        ResetZoom();   // 확대한 채로 닫으면 다음에 열 때도 확대되어 있다
        if (panel != null) panel.SetActive(false);
        pages.Clear();
        if (wasOpen) OnHidden?.Invoke();
    }

    // 뷰어가 닫힐 때 발생. 조사 화면이 "자료를 다 본 뒤" 대사를 이어 붙일 때 쓴다
    // (InvestigationController.ScheduleCombineHint 참고).
    public event System.Action OnHidden;

    // ---------------------------------------------------------------------------------
    // 읽어 내려가기 / 돋보기 (확대 · 축소)
    // ---------------------------------------------------------------------------------
    // 뷰어가 열려 있는 동안에는 따로 켤 것 없이 언제나 쓸 수 있다.
    //   - 마우스 휠, ↑ ↓ : 위아래로 굴려 읽는다
    //   - Ctrl + 휠       : 확대 / 축소 (쪽 전체가 보이는 배율 ~ 800%)
    //   - 끌기            : 그림을 움직여 원하는 곳을 본다
    //   - 돋보기 칸       : 폭 맞춤(100%) + 맨 위로 되돌리기
    // 그래서 자료 그림이 마우스 입력을 직접 받는다(raycastTarget). "아무 데나 누르면 닫힌다"는
    // 기존 동작은 ZoomInputRelay.OnPointerClick이 대신 처리한다.

    // ===== 자료 그림의 크기를 "폭 맞춤"으로 다시 잡는다 =====
    // 장을 넘길 때마다(RefreshPage) 부른다. 그림 틀(RectTransform)을 자료가 화면에 실제로
    // 차지할 크기로 만들어 두면, 확대/끌기 한계를 재는 ClampPan이 letterbox(위아래 빈 칸)에
    // 속지 않고 정확해진다.
    //
    //   폭 맞춤   : 가로를 칸에 꽉 맞춘다. 세로는 칸을 넘어가고, 굴려서 읽는다.
    //   전체 보기 : 쪽 전체가 칸 안에 들어가게 맞춘다. 한눈에 봐야 하는 자료용.
    //
    // ===== 어느 쪽으로 보낼지 =====
    // 두 가지를 본다.
    //
    //  (1) 원본이 칸(1104px)을 채울 만큼 큰가?  - WideEnoughRatio
    //      작은 그림(핸드폰 244px, 서류 뭉치 431px 같은 물건 그림)을 1104까지 늘리면
    //      2~4배로 뻥튀기돼서 뭉개진다. 이런 자료는 무조건 전체 보기로 보낸다.
    //
    //      0.75인 이유: 지금 자료 중 "물건 그림"은 가장 큰 것이 491px(액자 사진)이고,
    //      "스캔한 서류"는 가장 작은 것이 992px(경찰 사본)이다. 그 사이를 넉넉히 가르는 값이
    //      828px(=1104x0.75)이다. 예전에 0.9로 뒀더니 경계가 994px이 되어, 992px짜리
    //      경찰 사본이 1.6px 차이로 탈락했다. 그래서 그 서류만 461x652로 쪼그라들고
    //      굴릴 거리가 0이 되는 바람에, 다 읽고 누르면 뷰어가 닫혀 버렸다.
    //
    //  (2) 폭을 맞췄을 때 칸보다 길어지는가?
    //      길어지면 어차피 한눈에 안 들어오니, 작게 줄여 보여주느니 크게 키우고
    //      굴려서 읽게 하는 편이 낫다. 세로로 긴 A4(1.41)는 물론이고,
    //      오른쪽 여백을 잘라낸 가로 장부(0.85)도 여기에 걸린다.
    //
    // 예전에는 "세로/가로 >= 1.2"만 봤다. 그래서 가로로 넓은 장부가 전체 보기로 가서
    // 본문 11pt가 12px까지 쪼그라들었다. (1)을 함께 보기 때문에 사진·물건 그림이
    // 폭 맞춤으로 끌려가는 일은 없다.
    //
    // 그리고 ItemData.csv의 ViewerType이 Photo인 자료는 위 두 가지와 상관없이 무조건
    // 전체 보기다. 사진은 "읽는" 자료가 아니라 한눈에 보는 자료이기 때문이다.
    // (지금 쓰는 사진들은 전부 960px보다 작아서 (1)에 이미 걸리지만, 나중에 큰 사진이
    //  들어와도 굴려 읽는 자료로 바뀌지 않게 못 박아 둔다)
    private const float WideEnoughRatio = 0.75f;
    private bool fitWholePage;
    private void ApplyPageSize()
    {
        if (pageImage == null || pageViewport == null) return;
        Sprite s = pageImage.sprite;
        if (s == null) return;

        Vector2 src = s.rect.size;
        if (src.x <= 0f || src.y <= 0f) return;

        // 자료 칸에서 좌우 화살표 자리를 뺀 실제 그림 영역
        float availW = pageViewport.rect.width - PageInsetX * 2f;
        float availH = pageViewport.rect.height - PageInsetY * 2f;
        if (availW <= 1f || availH <= 1f) { availW = ContentW - PageInsetX * 2f; availH = ViewportH - PageInsetY * 2f; }

        float aspect = src.y / src.x;                       // 세로/가로
        bool bigEnough = src.x >= availW * WideEnoughRatio;  // 늘려도 뭉개지지 않을 만큼 큰가
        bool tooLong = availW * aspect > availH;             // 폭을 맞추면 칸보다 길어지는가

        Vector2 size;
        if (!fitWholePage && bigEnough && tooLong)
            size = new Vector2(availW, availW * aspect);                 // 폭 맞춤
        else
        {
            float fit = Mathf.Min(availW / src.x, availH / src.y);        // 전체 보기
            size = src * fit;
        }
        pageImage.rectTransform.sizeDelta = size;

        // 쪽 전체가 딱 들어가는 배율까지는 축소할 수 있게 열어준다(폭 맞춤이 1배이므로 1보다 작다).
        zoomMin = Mathf.Clamp(Mathf.Min(1f, availH / Mathf.Max(1f, size.y)), ZoomFloor, 1f);
    }

    // 돋보기 칸: 폭 맞춤(100%)으로 되돌리고 맨 위로 올린다.
    public void ResetZoom()
    {
        zoomScale = 1f;
        if (pageImage != null)
        {
            pageImage.rectTransform.localScale = Vector3.one;
            ScrollToTop();
        }
        UpdateZoomLabel();
    }

    // ===== 맨 위로 =====
    // 그림 틀은 가운데를 기준으로 놓여 있어서 그냥 두면 서류의 "한가운데"가 보인다.
    // 읽으려면 맨 윗줄부터 보여야 하므로, 자료가 칸보다 길면 그만큼 아래로 내려 윗변을 맞춘다.
    private void ScrollToTop()
    {
        if (pageImage == null) return;
        pageImage.rectTransform.anchoredPosition = new Vector2(0f, -PanLimit().y);
    }

    // 지금 기본 상태(폭 맞춤 100%)가 아닌가. 확대/축소 중일 때는 그림을 눌러도 뷰어가 닫히지
    // 않는다 (글씨를 보려고 누른 것이지 닫으려는 게 아니기 때문).
    public bool IsZoomed => Mathf.Abs(zoomScale - 1f) > 0.001f;

    // 위아래로 굴릴 것이 남아 있는 자료인가(= 폭 맞춤으로 들어간 세로 서류).
    // 이런 자료는 그림이 칸을 꽉 채워서 읽다가 누르는 일이 잦으므로, 눌러도 닫지 않는다.
    public bool IsScrollable => PanLimit().y > 1f;

    // delta만큼 배율을 바꾼다(Ctrl+휠 한 칸).
    public void AddZoom(float delta)
    {
        if (pageImage == null) return;
        zoomScale = Mathf.Clamp(zoomScale + delta, zoomMin, ZoomMax);
        pageImage.rectTransform.localScale = Vector3.one * zoomScale;
        ClampPan();
        UpdateZoomLabel();
    }

    // 휠/방향키로 위아래로 굴린다. 양수 = 아래쪽 내용으로 내려간다.
    public void ScrollBy(float amount)
    {
        if (pageImage == null) return;
        pageImage.rectTransform.anchoredPosition += new Vector2(0f, amount);
        ClampPan();
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

    // ===== 얼마나 움직일 수 있는가 =====
    // 그림이 자료 칸보다 큰 만큼만 움직일 수 있다(가장자리가 칸 가장자리에 닿으면 멈춤).
    // 칸보다 작은 방향은 0 - 가운데에 붙어서 꼼짝하지 않는다.
    // 폭 맞춤으로 들어간 세로 서류는 y 쪽이 크게 남으므로, 이 값이 곧 "굴릴 수 있는 거리"다.
    private Vector2 PanLimit()
    {
        if (pageImage == null || pageViewport == null) return Vector2.zero;
        Vector2 view = pageViewport.rect.size;
        Vector2 page = pageImage.rectTransform.rect.size * zoomScale;
        return new Vector2(Mathf.Max(0f, (page.x - view.x) * 0.5f),
                           Mathf.Max(0f, (page.y - view.y) * 0.5f));
    }

    // 그림을 너무 많이 끌어 화면 밖으로 사라지지 않게 가둔다.
    private void ClampPan()
    {
        if (pageImage == null) return;
        Vector2 limit = PanLimit();
        Vector2 pos = pageImage.rectTransform.anchoredPosition;
        pageImage.rectTransform.anchoredPosition =
            new Vector2(Mathf.Clamp(pos.x, -limit.x, limit.x), Mathf.Clamp(pos.y, -limit.y, limit.y));

        // 종이가 움직였으면 말풍선도 따라가야 한다 (형광펜 친 자리 옆에 붙어 있어야 하므로)
        if (highlights != null) highlights.Reposition();
    }

    // ---------------------------------------------------------------------------------
    // 형광펜 재생기가 쓰는 자리 계산
    // ---------------------------------------------------------------------------------
    // 자료 칸의 위/아래 끝 (1440x1080 틀 기준, 가운데가 0,0 / 위로 +)
    public float ViewportTop => ScreenH * 0.5f - ViewportY;
    public float ViewportBottom => ScreenH * 0.5f - (ViewportY + ViewportH);

    // ===== 그 줄이 보이도록 자료를 굴린다 =====
    // t: 서류 위에서부터의 비율(0=맨 위, 1=맨 아래). 그 지점이 자료 칸 세로 42%쯤에 오게 한다.
    // 42%인 이유: 말풍선이 그 위에 설 자리를 남겨두려고. 가운데(50%)에 두면 위가 빠듯하다.
    private const float ReadFocusY = 0.42f;
    private float TargetScrollY(float t)
    {
        if (pageImage == null) return 0f;
        float pageH = pageImage.rectTransform.rect.height * zoomScale;
        float want = (0.5f - ReadFocusY) * ViewportH;      // 칸 안에서의 목표 높이
        float y = want - (0.5f - t) * pageH;
        Vector2 limit = PanLimit();
        return Mathf.Clamp(y, -limit.y, limit.y);
    }

    public IEnumerator ScrollToPageFractionRoutine(float t, float seconds)
    {
        if (pageImage == null) yield break;
        var rt = pageImage.rectTransform;
        float from = rt.anchoredPosition.y;
        float to = TargetScrollY(t);
        // 이미 거의 그 자리면 굳이 움직이지 않는다 (화면이 괜히 들썩이면 어지럽다)
        if (Mathf.Abs(to - from) < 2f || seconds <= 0f)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, to);
            ClampPan();
            yield break;
        }

        float e = 0f;
        while (e < seconds)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / seconds);
            k = 1f - Mathf.Pow(1f - k, 3f);                // 처음 빠르고 끝에 부드럽게
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(from, to, k));
            ClampPan();
            yield return null;
        }
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, to);
        ClampPan();
    }

    // 형광펜 칸이 1440x1080 틀 어디에 있는지 (확대·스크롤까지 반영된 실제 자리)
    public bool PenRectOnScreenBox(RectTransform pen, out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        if (pen == null || screenBox == null) return false;

        var corners = new Vector3[4];
        pen.GetWorldCorners(corners);                       // 0=왼아래, 2=오른위
        Vector2 a = screenBox.InverseTransformPoint(corners[0]);
        Vector2 b = screenBox.InverseTransformPoint(corners[2]);
        center = (a + b) * 0.5f;
        size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        return true;
    }

    // 그 자리가 아직 자료 칸 안에 보이는가 (굴려서 밖으로 나갔으면 말풍선도 숨겨야 한다)
    public bool IsInsideViewport(Vector2 centerOnBox) =>
        centerOnBox.y <= ViewportTop - 4f && centerOnBox.y >= ViewportBottom + 4f;

    // 말풍선이 화면 밖으로 삐져나가지 않게 (pos = 말풍선 왼쪽 위 모서리)
    public Vector2 ClampBubble(Vector2 pos, Vector2 size)
    {
        float halfW = ScreenW * 0.5f, halfH = ScreenH * 0.5f;
        const float edge = 28f;
        float x = Mathf.Clamp(pos.x, -halfW + edge, halfW - edge - size.x);
        float y = Mathf.Clamp(pos.y, -halfH + edge + size.y, halfH - edge);
        return new Vector2(x, y);
    }

    private void UpdateZoomLabel()
    {
        if (zoomLabel == null) return;
        zoomLabel.text = $"{Mathf.RoundToInt(zoomScale * 100)}%";
        // 확대 중이면 배율 글자를 강조색으로 - "지금 커져 있다"는 걸 한눈에 알게
        zoomLabel.color = IsZoomed ? UITheme.Accent : UITheme.Body;
    }

    // ---------------------------------------------------------------------------------
    // 장 넘기기
    // ---------------------------------------------------------------------------------

    public bool HasNextPage => pageIndex < pages.Count - 1;

    public void NextPage()
    {
        if (HasNextPage)
        {
            pageIndex++;
            RefreshPage();
        }
    }

    // ===== 자료를 읽는 중 "다음"을 눌렀다 (클릭 / 스페이스 / 엔터) =====
    // 처리했으면 true. false면 부르는 쪽이 원래 하던 일(닫기 등)을 한다.
    //
    //   1) 이 장에 아직 읽을 줄이 남았다      -> 다음 줄
    //   2) 이 장은 다 읽었고 다음 장이 있다   -> 다음 장 (여기서 끊기면 안 된다)
    //   3) 마지막 장까지 다 읽었다            -> false. 그제서야 닫힐 수 있다
    //
    // 2번이 없으면 여러 장짜리 서류에서 첫 장을 다 읽자마자 뷰어가 닫혀 버린다.
    //
    // 단, 확대해서 들여다보는 중에는 장을 넘기지 않는다. 글씨를 보려고 누른 것이지
    // 넘기려는 게 아니기 때문이다. (그때는 ← → 나 넘김 화살표를 쓴다)
    public bool AdvanceReading()
    {
        if (highlights == null) return false;
        if (highlights.Advance()) return true;
        if (highlights.HasSpots && HasNextPage && !IsZoomed) { NextPage(); return true; }
        return false;
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
            // 새 자료의 비율에 맞게 그림 틀 크기를 다시 잡는다. ResetZoom이 "맨 위"를
            // 계산하려면 크기가 먼저 정해져 있어야 하므로 순서가 중요하다.
            ApplyPageSize();
        }

        // 장을 넘기면 확대는 풀고 맨 위로 올린다. 확대되거나 중간쯤 내려간 채로 넘어가면
        // 엉뚱한 구석이 보여 당황스럽다.
        ResetZoom();

        // 이 장에 그을 형광펜을 준비한다. 표에 없는 그림이면 조용히 아무것도 안 한다.
        if (highlights != null && pageImage != null)
            highlights.SetPage(pages[pageIndex], pageImage.rectTransform);

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
                pageSegments[i].color = i == pageIndex ? UITheme.Accent : UITheme.Line;
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
            var img = AddRect(pageSegmentRoot, "Seg" + i, 0f, 0f, segW, segH, UITheme.Line);
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
        if (targetCanvas == null) targetCanvas = UILayers.RootCanvas();
        if (targetCanvas == null)
        {
            Debug.LogError("[DocumentViewerController] 씬에 Canvas가 없어 자료 뷰어를 만들 수 없습니다.");
            return;
        }

        // 창 (화면 전체를 덮는 검은 막 + 조사 상세 층 + Esc = 닫기). 막을 누르면 닫힌다.
        panel = CreateWindow(targetCanvas.transform, "DocumentViewerPanel", UILayer.Detail, Hide);
        backdropButton = panel.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None; // 눌렀을 때 색이 변하면 어색하다
        backdropButton.onClick.AddListener(Hide);

        // Figma 1440x1080 화면을 그대로 옮겨 놓을 틀 (화면 정가운데)
        var boxGo = new GameObject("Box", typeof(RectTransform));
        boxGo.transform.SetParent(panel.transform, false);
        var box = (RectTransform)boxGo.transform;
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(ScreenW, ScreenH);
        screenBox = box;

        // ----- 머리말 (▬ / EVIDENCE · DOCUMENT / 제목) -----
        CreateHeader(box, Margin, 36f, "EVIDENCE  ·  DOCUMENT", "", out kickerLabel, out titleLabel);

        // 돋보기 칸 [🔍 100% | 원래대로] - 누르면 원래 크기로
        var zoomBox = CreateOutlinedButton(box, "ZoomButton", 1080f, 60f, 176f, 48f, ResetZoom, out var zoomBorder);
        zoomButton = zoomBox;
        var mag = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        mag.transform.SetParent(zoomBox.transform, false);
        PlaceTopLeft((RectTransform)mag.transform, 14f, 12f, 24f, 24f);
        var magImg = mag.GetComponent<Image>();
        magImg.sprite = CreateMagnifierSprite();
        magImg.color = UITheme.ButtonText;
        magImg.raycastTarget = false;
        zoomLabel = CreateLabel(zoomBox.transform, "ZoomLabel", "100%", 16, FontStyles.Bold, UITheme.Body);
        zoomLabel.alignment = TextAlignmentOptions.MidlineLeft;
        PlaceTopLeft(zoomLabel.rectTransform, 46f, 0f, 54f, 48f);
        AddRect(zoomBox.transform, "Sep", 100f, 14f, 1f, 20f, UITheme.Line);
        var reset = CreateLabel(zoomBox.transform, "ResetLabel", "원래대로", 14, FontStyles.Normal, UITheme.Hint);
        reset.alignment = TextAlignmentOptions.MidlineLeft;
        PlaceTopLeft(reset.rectTransform, 112f, 0f, 60f, 48f);
        AddHoverLine(zoomBox.gameObject, zoomBorder, new Graphic[] { reset }, UITheme.Hint, UITheme.ButtonText);

        // 닫기 X + ESC
        CreateCloseButton(box, 1272f, 60f, Hide);

        AddRect(box, "HeadDivider", Margin, 136f, ContentW, 1f, UITheme.Divider);

        // ----- 자료 칸 -----
        // RectMask2D: 확대했을 때 이 칸 밖으로 나간 부분을 잘라서 제목이나 설명 위를 덮지 않게 한다.
        var viewGo = new GameObject("PageViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewGo.transform.SetParent(box, false);
        pageViewport = (RectTransform)viewGo.transform;
        PlaceTopLeft(pageViewport, Margin, ViewportY, ContentW, ViewportH);
        var viewImg = viewGo.GetComponent<Image>();
        viewImg.color = UITheme.Surface;
        viewImg.raycastTarget = false;   // 그림 밖 빈 곳을 누르면 뒤의 검은 막이 받아서 닫힌다
        AddBorder(pageViewport, UITheme.Divider, 1f);

        // ===== 자료 그림 =====
        // 칸 가운데에 놓고, 크기는 자료를 띄울 때 ApplyPageSize가 비율에 맞춰 정한다.
        // 서류(세로로 긴 자료)는 가로 폭을 꽉 채우느라 칸보다 길어지고, 넘친 부분은 위의
        // RectMask2D가 잘라낸다. 좌우에 PageInsetX만큼 비워둔 것은 넘김 화살표 자리다.
        //
        // 예전에는 이 틀을 칸 전체로 늘려놓고 preserveAspect에 맡겼는데, 그러면 A4가 세로
        // 길이에 맞춰 들어가 폭의 48%만 쓰고 글씨가 8.5px이 됐다. 게다가 틀 크기(960x652)와
        // 실제 그림 크기(461x652)가 달라서 끌기 한계 계산도 어긋났다.
        var pageGo = new GameObject("PageImage", typeof(RectTransform), typeof(Image));
        pageGo.transform.SetParent(viewGo.transform, false);
        var pageRt = pageGo.GetComponent<RectTransform>();
        // 확대도 끌기도 가운데를 기준으로 해야 자연스럽다.
        pageRt.anchorMin = pageRt.anchorMax = pageRt.pivot = new Vector2(0.5f, 0.5f);
        pageRt.sizeDelta = new Vector2(ContentW - PageInsetX * 2f, ViewportH - PageInsetY * 2f);
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
        var bracketColor = new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.9f);
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

        // 좌우 넘김 화살표 — 자료 칸 "바깥", 화면 좌우 여백 한가운데에 둔다.
        // 칸 안에 두면 그만큼 서류가 좁아진다. 여백 120 가운데에 56짜리를 놓으면 x = 32.
        float arrowY = ViewportY + (ViewportH - ArrowH) * 0.5f;
        prevButton = CreateArrow(box, "PrevButton", (Margin - ArrowW) * 0.5f, arrowY, true, PrevPage, out prevArrow);
        nextButton = CreateArrow(box, "NextButton", ScreenW - (Margin + ArrowW) * 0.5f, arrowY, false, NextPage, out nextArrow);

        // ----- 아래쪽 -----
        AddRect(box, "FootDivider", Margin, 884f, ContentW, 1f, UITheme.Divider);

        descriptionLabel = CreateLabel(box, "DescriptionLabel", "", 20, FontStyles.Normal, UITheme.Body);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;
        descriptionLabel.lineSpacing = 20f;
        PlaceTopLeft(descriptionLabel.rectTransform, Margin, 904f, 900f, 100f);

        // 쪽수 "01 / 04" - 오른쪽 끝 맞춤
        pageTotalLabel = CreateLabel(box, "PageTotal", "/ 01", 16, FontStyles.Bold, UITheme.Kicker);
        pageTotalLabel.alignment = TextAlignmentOptions.BottomRight;
        PlaceTopLeft(pageTotalLabel.rectTransform, ScreenW - Margin - 60f, 900f, 60f, 36f);
        pageLabel = CreateLabel(box, "PageLabel", "01", 28, FontStyles.Bold, UITheme.Title);
        pageLabel.alignment = TextAlignmentOptions.BottomRight;
        PlaceTopLeft(pageLabel.rectTransform, ScreenW - Margin - 60f - 8f - 80f, 898f, 80f, 40f);

        var segGo = new GameObject("PageSegments", typeof(RectTransform));
        segGo.transform.SetParent(box, false);
        pageSegmentRoot = (RectTransform)segGo.transform;
        PlaceTopLeft(pageSegmentRoot, Margin, 948f, ContentW, 3f);

        CreateFootHint(box, Margin, 1020f, ContentW,
            "스페이스 / 자료 누르기 = 다음 줄   ·   ← → 넘기기   ·   휠 / ↑ ↓ 위아래   ·   Ctrl+휠 확대·축소   ·   Esc 닫기");

        // 형광펜 + 독백. 자료를 펼칠 때 RefreshPage가 이 친구에게 장을 넘겨준다.
        if (highlights == null) highlights = DocumentHighlightPlayer.Attach(this, box);

        UpdateZoomLabel();

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨져 보인다.
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
    }

    // ---------------------------------------------------------------------------------
    // 만들기 도우미 (공용은 UIKit.cs - 여기는 이 화면 전용)
    // ---------------------------------------------------------------------------------
    // 넘김 화살표 56x128 (조사 화면 양옆 화살표와 같은 크기).
    private static Button CreateArrow(RectTransform parent, string name, float x, float y, bool left,
                                      UnityEngine.Events.UnityAction onClick, out ArrowView view)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        PlaceTopLeft(rt, x, y, ArrowW, ArrowH);
        var bg = go.GetComponent<Image>();
        bg.color = ArrowFill;
        var border = AddBorder(rt, UITheme.LineStrong, 1f);

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
            Color line = !on ? ArrowLineOff : hover ? UITheme.LineHover : UITheme.LineStrong;
            Color mark = !on ? ChevronOff : hover ? UITheme.Title : ChevronOn;
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

        // ===== 휠 = 위아래로 굴리기, Ctrl+휠 = 확대·축소 =====
        // 서류가 폭 맞춤으로 들어가면서 한 화면에 다 안 들어오게 됐다. 문서를 보는 프로그램이
        // 다 그렇듯 휠의 기본은 "읽어 내려가기"여야 한다. 확대는 Ctrl을 같이 누른다.
        public void OnScroll(PointerEventData e)
        {
            // 휠을 안 굴렸는데 이벤트만 온 경우가 있다. Mathf.Sign(0)은 1이라 그냥 두면
            // 가만히 있어도 움직인다.
            if (owner == null || Mathf.Approximately(e.scrollDelta.y, 0f)) return;
            float dir = Mathf.Sign(e.scrollDelta.y);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl) owner.AddZoom(dir * ZoomStep);
            else owner.ScrollBy(-dir * WheelScroll);   // 휠을 내리면 아래쪽 내용으로
        }

        public void OnPointerClick(PointerEventData e)
        {
            float moved = draggedDistance;
            draggedDistance = 0f;
            if (owner == null) return;
            if (moved > DragSlack) return;     // 끌었던 것이므로 닫지 않는다
            // 읽는 중이면 누름은 "다음 줄" 또는 "다음 장"이다 (AdvanceReading 참고).
            if (owner.AdvanceReading()) return;
            if (owner.IsZoomed) return;        // 확대/축소해서 보는 중이므로 닫지 않는다
            // 폭 맞춤으로 들어간 세로 서류는 그림이 칸을 꽉 채운다. 읽다가 누르는 일이 잦은데
            // 그때마다 닫히면 못 쓴다. 이런 자료는 X / Esc / 바깥쪽으로만 닫는다.
            if (owner.IsScrollable) return;
            owner.Hide();
        }
    }
}
