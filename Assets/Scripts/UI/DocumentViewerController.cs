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
    [Tooltip("자료 오른쪽 위 구석의 돋보기 버튼. 누르면 확대 모드가 켜진다.")]
    public Button zoomButton;
    [Tooltip("확대 모드일 때 배율과 조작법을 알려주는 텍스트")]
    public TMP_Text zoomLabel;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    // 지금 펼쳐 보고 있는 그림들과 몇 번째를 보고 있는지.
    private readonly List<Sprite> pages = new List<Sprite>();
    private int pageIndex;

    // ===== 확대 / 축소 =====
    // 서류 글씨가 작아서 그냥 크게 띄우는 것만으로는 안 읽히는 경우가 있다. 뷰어가 열려 있는
    // 동안에는 언제나 휠로 확대/축소하고 끌어서 움직일 수 있다(창은 그대로, 그림만 커진다).
    // 돋보기 버튼은 켜고 끄는 스위치가 아니라 "원래 크기로 되돌리기"다.
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

        Show(info.displayName, info.description, sprites);
        return true;
    }

    // 그림 목록을 직접 넘겨서 뷰어를 연다. (아이템이 아닌 자료를 보여줄 때도 쓸 수 있게 열어둠)
    public void Show(string title, string description, List<Sprite> sprites)
    {
        if (panel == null) return;

        pages.Clear();
        pages.AddRange(sprites);
        pageIndex = 0;

        if (titleLabel != null) titleLabel.text = title;
        if (descriptionLabel != null) descriptionLabel.text = description;

        panel.SetActive(true);
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
    //   - 돋보기 버튼 : 원래 크기로 되돌리기
    // 그래서 자료 그림이 마우스 입력을 직접 받는다(raycastTarget). "아무 데나 누르면 닫힌다"는
    // 기존 동작은 ZoomInputRelay.OnPointerClick이 대신 처리한다.

    // 돋보기 버튼: 원래 크기(100%)로 되돌린다.
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
    private void ClampPan()
    {
        if (pageImage == null || pageViewport == null) return;
        Vector2 view = pageViewport.rect.size;
        Vector2 limit = (view * zoomScale - view) * 0.5f;
        limit = new Vector2(Mathf.Max(0f, limit.x), Mathf.Max(0f, limit.y));
        Vector2 pos = pageImage.rectTransform.anchoredPosition;
        pageImage.rectTransform.anchoredPosition =
            new Vector2(Mathf.Clamp(pos.x, -limit.x, limit.x), Mathf.Clamp(pos.y, -limit.y, limit.y));
    }

    private void UpdateZoomLabel()
    {
        if (zoomLabel == null) return;
        zoomLabel.gameObject.SetActive(true);
        zoomLabel.text = IsZoomed
            ? $"{Mathf.RoundToInt(zoomScale * 100)}%   휠: 확대·축소   끌기: 이동"
            : "휠: 확대·축소";
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

        // ===== 자료 그림은 화면을 거의 꽉 채운다 =====
        // 서류 그림은 A4 세로 비율(992x1403)이라 preserveAspect를 켜두면 세로 길이에 맞춰
        // 들어간다. 예전에는 이 칸이 화면의 가로 36% x 세로 30%밖에 안 돼서, 1920x1080
        // 기준으로 서류가 229x324로 줄어들어 글씨를 읽을 수 없었다. 세로를 81%까지 넓혀
        // 875px 높이로 띄운다(약 2.7배). 가로를 넉넉히 준 것은 사진처럼 가로로 긴 자료도
        // 같은 칸을 쓰기 때문이다 - 세로 자료는 preserveAspect가 알아서 가운데로 모아준다.
        // 자료를 담는 틀. RectMask2D를 붙여두면 확대했을 때 이 틀 밖으로 나간 부분이 잘려서,
        // 확대한 그림이 제목이나 설명 위를 덮지 않는다.
        var viewGo = new GameObject("PageViewport", typeof(RectTransform), typeof(RectMask2D));
        viewGo.transform.SetParent(panel.transform, false);
        pageViewport = viewGo.GetComponent<RectTransform>();
        pageViewport.anchorMin = new Vector2(0.06f, 0.13f);
        pageViewport.anchorMax = new Vector2(0.94f, 0.94f);
        pageViewport.offsetMin = Vector2.zero;
        pageViewport.offsetMax = Vector2.zero;

        var pageGo = new GameObject("PageImage", typeof(RectTransform), typeof(Image));
        pageGo.transform.SetParent(viewGo.transform, false);
        var pageRt = pageGo.GetComponent<RectTransform>();
        StretchFull(pageRt);
        // 확대는 가운데를 기준으로 커져야 자연스럽다.
        pageRt.pivot = new Vector2(0.5f, 0.5f);
        pageImage = pageGo.GetComponent<Image>();
        pageImage.preserveAspect = true;
        // 휠과 끌기를 받으려면 그림이 마우스 입력을 받아야 한다. 대신 "그냥 한 번 누르면
        // 닫힌다"는 기존 동작은 아래 ZoomInputRelay가 대신 처리한다(끌지 않았고 확대 중도
        // 아닐 때만 닫는다).
        pageImage.raycastTarget = true;
        pageGo.AddComponent<ZoomInputRelay>().owner = this;

        // 제목 - 그림 위 맨 윗줄. 그림이 커진 만큼 위로 올렸다.
        titleLabel = CreateLabel("TitleLabel", panel.transform,
            new Vector2(0.05f, 0.945f), new Vector2(0.95f, 0.995f), 30, TextAlignmentOptions.Center);

        // 설명(ItemData.csv의 Description) - 그림 아래 한 줄. 자리를 그림에 내줬으므로
        // 글자를 조금 줄이고 높이를 얇게 잡는다.
        descriptionLabel = CreateLabel("DescriptionLabel", panel.transform,
            new Vector2(0.06f, 0.065f), new Vector2(0.94f, 0.128f), 20, TextAlignmentOptions.Center);

        // 쪽수
        pageLabel = CreateLabel("PageLabel", panel.transform,
            new Vector2(0.42f, 0.012f), new Vector2(0.58f, 0.062f), 24, TextAlignmentOptions.Center);

        // 이전/다음 버튼
        prevButton = CreateNavButton("PrevButton", panel.transform, "◀",
            new Vector2(0.10f, 0.012f), new Vector2(0.20f, 0.062f), PrevPage);
        nextButton = CreateNavButton("NextButton", panel.transform, "▶",
            new Vector2(0.80f, 0.012f), new Vector2(0.90f, 0.062f), NextPage);

        // ===== 돋보기 버튼 - 자료 오른쪽 위 구석 =====
        var zoomGo = new GameObject("ZoomButton", typeof(RectTransform), typeof(Image), typeof(Button));
        zoomGo.transform.SetParent(panel.transform, false);
        var zoomRt = zoomGo.GetComponent<RectTransform>();
        zoomRt.anchorMin = new Vector2(0.865f, 0.865f);
        zoomRt.anchorMax = new Vector2(0.930f, 0.930f);
        zoomRt.offsetMin = Vector2.zero;
        zoomRt.offsetMax = Vector2.zero;
        var zoomImg = zoomGo.GetComponent<Image>();
        zoomImg.sprite = CreateMagnifierSprite();
        zoomImg.color = new Color(1f, 1f, 1f, 0.85f);
        zoomImg.preserveAspect = true;
        zoomButton = zoomGo.GetComponent<Button>();
        zoomButton.onClick.AddListener(ResetZoom);   // 스위치가 아니라 "원래 크기로"

        // 확대 중일 때만 뜨는 안내 (배율 + 조작법).
        // 자료 칸 위쪽에 겹쳐 띄운다 - 아래쪽 설명 글과 부딪히지 않고, 돋보기 버튼(0.865~)
        // 왼쪽에서 끝나도록 가로 범위를 잡았다.
        zoomLabel = CreateLabel("ZoomLabel", panel.transform,
            new Vector2(0.07f, 0.885f), new Vector2(0.85f, 0.935f), 20, TextAlignmentOptions.Left);
        zoomLabel.gameObject.SetActive(false);

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨져 보인다.
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
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

    // ===== 자료 그림 위의 휠 / 끌기 / 클릭을 컨트롤러로 넘겨주는 부품 =====
    // 그림이 마우스 입력을 받게 되면서 뒤의 Backdrop이 가려지므로, "아무 데나 누르면 닫힌다"를
    // 여기서 대신 해준다. 단 두 경우에는 닫지 않는다:
    //   - 끌어서 그림을 움직인 경우 (놓는 순간 창이 닫히면 황당하다)
    //   - 확대해서 보고 있는 경우 (글씨를 보려고 누른 것이지 닫으려는 게 아니다)
    private class ZoomInputRelay : MonoBehaviour, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        public DocumentViewerController owner;

        // 이번에 누른 동안 얼마나 끌었는지. 손이 살짝 떨린 정도는 클릭으로 봐준다.
        private const float DragSlack = 8f;
        private float draggedDistance;

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
