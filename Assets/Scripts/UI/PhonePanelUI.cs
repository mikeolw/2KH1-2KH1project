using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 핸드폰 UI - 퀵바의 폰 버튼을 누르면 열리는 화면 (NotePanelUI.cs와 같은 방식으로 작성)
// =====================================================================================
// ===== 지금은 모양만 잡아둔 단계다 =====
// 기획자가 그려준 목업(실제 핸드폰처럼 생긴 몸체 + 위쪽 "화면"에 아이콘 3x3 그리드 +
// 아래쪽 빈 "키패드" 영역)을 그대로 따라 만든다. 아이콘을 눌러도 아직은 아무 정보도
// 나오지 않는다 - 모양이 확정된 다음에 각 아이콘의 실제 기능(전화기록/문자기록/채팅창
// 출력 등)을 CSV와 함께 다시 붙일 예정이다. 그래서 Button 컴포넌트는 붙어 있어
// (눌렀을 때 색이 살짝 변하는 기본 반응은 있음) 나중에 onClick만 연결하면 되지만,
// 지금은 어떤 리스너도 달려 있지 않다.
//
// ===== 왜 NotePanelUI와 같은 방식으로 만드나 =====
// 씬의 PhonePanel은 내용 없는 빈 껍데기다. 그래서 여기서도 캔버스 바로 아래에 화면을
// 직접 만든다 - 씬이 어떻게 짜여 있든 항상 같은 자리/크기로 뜬다. GameBootstrap이 이
// 컴포넌트를 PhonePanel에 자동으로 붙여준다.
//
// ===== 몸체를 둥글게 그리는 방법 =====
// 유니티 기본 Image는 모서리를 둥글게 그려주지 않고, 이 프로젝트에는 둥근 사각형
// 그림(에셋)도 아직 없다. 그렇다고 각진 사각형으로 두면 목업과 너무 달라 보이므로,
// 코드에서 픽셀을 직접 계산해 둥근 사각형 텍스처를 만들어 쓴다(BuildRoundedRectSprite).
public class PhonePanelUI : MonoBehaviour
{
    private const string OverlayName = "__PhoneOverlay";

    // 핸드폰 몸체 크기
    private const float BodyWidth = 480f;
    private const float BodyHeight = 880f;
    private const float BodyCorner = 48f;

    // 위쪽 "화면" 영역 크기
    private const float ScreenWidth = 400f;
    private const float ScreenHeight = 380f;
    private const float ScreenCorner = 16f;
    private const float ScreenTopMargin = 40f;

    // 아이콘 3x3 그리드에 들어갈 이름. 맨 앞 null은 목업처럼 왼쪽 위 한 칸을 비워둔다.
    private static readonly string[] IconLabels =
    {
        null, "라디오", "설정",
        "날씨", "인터넷", "카메라",
        "전화", "이메일", "문자",
    };

    private GameObject overlay;

    private void Awake()
    {
        HideOriginalPanelVisuals();
        BuildOverlay();
    }

    private void OnEnable()
    {
        if (overlay == null) BuildOverlay();

        if (overlay != null)
        {
            overlay.SetActive(true);
            // 다른 UI에 가리지 않도록 항상 맨 앞으로.
            overlay.transform.SetAsLastSibling();
        }
    }

    private void OnDisable()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 씬에 있던 원래 패널은 안 보이게 한다 (NotePanelUI.cs와 동일한 이유)
    // ---------------------------------------------------------------------------------
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
    // 화면 만들기 (캔버스 바로 아래)
    // ---------------------------------------------------------------------------------
    private void BuildOverlay()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PhonePanelUI] 씬에 Canvas가 없어 핸드폰 화면을 만들 수 없습니다.");
            return;
        }

        var existing = canvas.transform.Find(OverlayName);
        if (existing != null)
        {
            overlay = existing.gameObject;
            return;
        }

        // ----- 화면 전체를 덮는 막(바깥을 누르면 닫힘) -----
        overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;   // 뒤쪽 게임 화면이 눌리지 않게 막는다

        var dimButton = overlay.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(() => gameObject.SetActive(false));

        // ----- 핸드폰 몸체 -----
        var body = new GameObject("PhoneBody", typeof(RectTransform), typeof(Image));
        body.transform.SetParent(overlay.transform, false);
        var bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = bodyRt.anchorMax = bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.sizeDelta = new Vector2(BodyWidth, BodyHeight);
        bodyRt.anchoredPosition = Vector2.zero;

        var bodyImage = body.GetComponent<Image>();
        bodyImage.sprite = BuildRoundedRectSprite((int)BodyWidth, (int)BodyHeight, BodyCorner);
        bodyImage.color = Color.white;
        // Image의 raycastTarget이 기본으로 켜져 있어, 몸체를 눌러도 뒤의 dim 버튼(바깥
        // 클릭 시 닫기)까지 클릭이 새지 않는다. 따로 막을 컴포넌트를 붙일 필요가 없다.

        // ----- 위쪽 "화면" 영역 -----
        var screen = new GameObject("Screen", typeof(RectTransform), typeof(Image));
        screen.transform.SetParent(body.transform, false);
        var screenRt = screen.GetComponent<RectTransform>();
        screenRt.anchorMin = new Vector2(0.5f, 1f);
        screenRt.anchorMax = new Vector2(0.5f, 1f);
        screenRt.pivot = new Vector2(0.5f, 1f);
        screenRt.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        screenRt.anchoredPosition = new Vector2(0f, -ScreenTopMargin);

        var screenImage = screen.GetComponent<Image>();
        screenImage.sprite = BuildRoundedRectSprite((int)ScreenWidth, (int)ScreenHeight, ScreenCorner);
        screenImage.color = new Color(0.96f, 0.96f, 0.96f);

        BuildIconGrid(screen.transform);

        // 아래쪽 빈 "키패드" 영역은 목업처럼 아무 것도 그리지 않고 몸체 배경만 남겨둔다.

        UIFontHelper.ApplyToChildren(overlay);

        overlay.SetActive(false);
    }

    // ----- 화면 안 아이콘 3x3 그리드 -----
    private void BuildIconGrid(Transform parent)
    {
        var gridGo = new GameObject("IconGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(parent, false);
        var gridRt = gridGo.GetComponent<RectTransform>();
        gridRt.anchorMin = Vector2.zero;
        gridRt.anchorMax = Vector2.one;
        gridRt.offsetMin = new Vector2(16f, 16f);
        gridRt.offsetMax = new Vector2(-16f, -16f);

        var grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(110f, 110f);
        grid.spacing = new Vector2(12f, 12f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        foreach (string label in IconLabels)
        {
            CreateIconSlot(gridGo.transform, label);
        }
    }

    // label이 null이면 목업처럼 빈 칸(버튼 없음)으로 둔다.
    private void CreateIconSlot(Transform parent, string label)
    {
        var go = new GameObject(string.IsNullOrEmpty(label) ? "Icon_Empty" : $"Icon_{label}",
            typeof(RectTransform));
        go.transform.SetParent(parent, false);

        if (string.IsNullOrEmpty(label)) return;   // 빈 칸: 자리만 차지하고 아무것도 없음

        // 알파를 완전히 0으로 두면 눌렀을 때의 기본 색 반응(Button의 ColorTint)도
        // 곱해져서 안 보이므로, 거의 안 보이는 수준으로만 살짝 남겨둔다.
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
        var button = go.AddComponent<Button>();

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        tmp.raycastTarget = false;

        button.targetGraphic = go.GetComponent<Image>();
        // 지금은 모양만 잡는 단계라 onClick에 아무 것도 연결하지 않는다.
        // (눌렀을 때 정보를 보여주는 기능은 모양이 확정된 뒤 다시 붙인다.)
    }

    // ---------------------------------------------------------------------------------
    // 둥근 사각형 텍스처 생성 (에셋 없이 모서리를 둥글게 그리기 위한 유틸)
    // ---------------------------------------------------------------------------------
    private Sprite BuildRoundedRectSprite(int width, int height, float corner)
    {
        var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[width * height];
        Color32 opaque = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(255, 255, 255, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = IsInsideRoundedRect(x, y, width, height, corner) ? opaque : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    // 픽셀 (x, y)가 모서리 반지름 corner인 둥근 사각형 안에 있는지 판정한다.
    private bool IsInsideRoundedRect(int x, int y, int width, int height, float corner)
    {
        // 네 모서리 중 가장 가까운 모서리의 중심까지 거리로 판정하고,
        // 모서리 영역이 아니면(사각형 안쪽 십자 영역) 항상 안쪽으로 취급한다.
        float nearestX = x < corner ? corner : (x > width - corner ? width - corner : x);
        float nearestY = y < corner ? corner : (y > height - corner ? height - corner : y);

        bool inCornerZoneX = x < corner || x > width - corner;
        bool inCornerZoneY = y < corner || y > height - corner;
        if (!inCornerZoneX || !inCornerZoneY) return true;

        float dx = x - nearestX;
        float dy = y - nearestY;
        return dx * dx + dy * dy <= corner * corner;
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
