using UnityEngine;

// =====================================================================================
// 화면 비율 고정 - 전체화면에서도 그림이 찌그러지지 않게 하고 남는 공간은 검게 채운다
// =====================================================================================
// ===== 왜 필요한가? =====
// 이 게임의 그림(배경/스탠딩/조사 화면)은 전부 1440x1080, 즉 4:3 비율로 그려져 있다.
// 그런데 요즘 모니터는 대부분 16:9(1920x1080)라서, 전체화면으로 켜면 유니티가 화면을
// 가로로 늘려 채우면서 그림이 옆으로 퍼진 것처럼 보인다(캐릭터 얼굴이 넓어진다).
//
// ===== 어떻게 해결하나 (레터박스 / 필러박스) =====
// 카메라가 실제로 그리는 영역(Camera.rect)을 화면 한가운데의 4:3 사각형으로 제한한다.
// 그러면 그 바깥은 카메라가 아무것도 그리지 않아 배경색(검은색)만 남는다.
//   - 모니터가 가로로 길면(16:9) : 좌우에 검은 띠 (필러박스)
//   - 모니터가 세로로 길면        : 위아래에 검은 띠 (레터박스)
// 영화 DVD를 와이드 TV에서 볼 때 좌우에 검은 띠가 생기는 것과 같은 원리다.
//
// ===== 씬 배치 =====
// 게임의 메인 카메라(Main Camera)에 이 스크립트를 붙이면 된다.
// 붙일 카메라를 지정하지 않으면 Camera.main을 자동으로 찾는다.
//
// ===== 주의: UI(Canvas)도 함께 맞춰야 한다 =====
// Canvas의 Render Mode가 "Screen Space - Overlay"이면 카메라 설정을 무시하고 화면 전체에
// 그려지기 때문에, 검은 띠 위에 UI가 삐져나온다. 그래서 이 스크립트는 씬의 Canvas를 찾아
// "Screen Space - Camera" 모드로 바꾸고 이 카메라에 연결해준다(아래 SetupCanvases 참고).
[RequireComponent(typeof(Camera))]
public class AspectRatioKeeper : MonoBehaviour
{
    [Header("고정할 화면 비율 (가로 / 세로)")]
    [Tooltip("이 게임의 그림은 1440x1080 = 4:3 으로 그려져 있다.")]
    public float targetWidth = 1440f;
    public float targetHeight = 1080f;

    [Header("검은 띠 영역에 칠할 색")]
    public Color letterboxColor = Color.black;

    private Camera cam;

    // 마지막으로 계산한 화면 크기. 창 크기가 바뀔 때만 다시 계산하기 위해 기억해둔다.
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        // 카메라가 아무것도 그리지 않는 영역(검은 띠)과 배경이 비었을 때 보일 색.
        // clearFlags를 SolidColor로 해야 이 색으로 칠해진다.
        // (URP에서는 이 설정이 카메라 인스펙터의 Background Type = Solid Color 에 해당한다)
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = letterboxColor;   // 기본값 Color.black
    }

    private void Start()
    {
        SetupCanvases();
        ApplyAspect();
    }

    private void Update()
    {
        // 창 크기가 바뀌었을 때만 다시 계산한다(매 프레임 계산할 필요가 없다).
        // 창 모드 <-> 전체화면 전환, 창 크기 드래그 등에 모두 반응한다.
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyAspect();
        }
    }

    // 카메라가 그릴 영역을 화면 한가운데의 4:3 사각형으로 제한한다.
    public void ApplyAspect()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (cam == null) return;

        float targetAspect = targetWidth / targetHeight;              // 1.333... (4:3)
        float windowAspect = (float)Screen.width / Screen.height;     // 실제 창 비율
        float scaleHeight = windowAspect / targetAspect;

        // Camera.rect는 화면을 0~1 비율로 나타낸 사각형이다.
        // x=0,y=0,w=1,h=1 이면 화면 전체를 쓴다는 뜻.
        Rect rect = cam.rect;

        if (scaleHeight < 1f)
        {
            // 창이 목표 비율보다 "세로로 길다" -> 위아래에 검은 띠 (레터박스)
            rect.width = 1f;
            rect.height = scaleHeight;
            rect.x = 0f;
            rect.y = (1f - scaleHeight) / 2f;
        }
        else
        {
            // 창이 목표 비율보다 "가로로 길다" -> 좌우에 검은 띠 (필러박스)
            float scaleWidth = 1f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1f;
            rect.x = (1f - scaleWidth) / 2f;
            rect.y = 0f;
        }

        // ===== 뷰포트를 정수 픽셀로 맞춘다 (그림이 흐려지는 것을 막는다) =====
        // Camera.rect는 0~1 비율이라, 그대로 두면 실제 렌더 크기가 1439.6픽셀처럼 소수가 될 수 있다.
        // 그러면 UI 전체가 어중간한 배율로 다시 그려지면서 모든 그림이 한 겹 뭉개진다.
        // 비율을 "화면 픽셀 수로 환산했을 때 정수가 되는 값"으로 다듬어 이 문제를 없앤다.
        int pixelW = Mathf.RoundToInt(rect.width * Screen.width);
        int pixelH = Mathf.RoundToInt(rect.height * Screen.height);
        rect.width = (float)pixelW / Screen.width;
        rect.height = (float)pixelH / Screen.height;
        rect.x = Mathf.Round(rect.x * Screen.width) / Screen.width;
        rect.y = Mathf.Round(rect.y * Screen.height) / Screen.height;

        cam.rect = rect;

        WarnIfNotPixelPerfect(pixelW, pixelH);
    }

    // ===== 화면이 원본 크기와 다르면 한 번만 알려준다 =====
    // 그림이 흐린 원인의 대부분은 "게임 화면이 1440x1080이 아니라서 UI 전체가 축소/확대되는 것"이다.
    // 텍스처나 좌표 문제가 아니라 화면 크기 문제라는 걸 바로 알 수 있게 실제 배율을 찍어준다.
    //
    // 유니티 에디터의 Game 탭은 기본이 "Free Aspect"라 창 크기에 따라 아무 해상도나 되는데,
    // 이때는 배율이 0.6배 같은 값이 되어 원화가 뭉개져 보인다. Game 탭 해상도를 1440x1080
    // (또는 1920x1080)으로 고정하면 배율 1.00이 되어 원화 그대로 선명해진다.
    private static bool pixelPerfectWarned;

    private void WarnIfNotPixelPerfect(int pixelW, int pixelH)
    {
        if (pixelPerfectWarned) return;
        pixelPerfectWarned = true;

        float scale = pixelH / targetHeight;
        if (Mathf.Abs(scale - 1f) < 0.001f)
        {
            Debug.Log($"[AspectRatioKeeper] 렌더 크기 {pixelW}x{pixelH} = 원본 크기. 그림이 원화 그대로 선명하게 나옵니다.");
            return;
        }

        Debug.LogWarning(
            $"[AspectRatioKeeper] 지금 게임 화면이 {pixelW}x{pixelH}라서 그림이 {scale:0.00}배로 " +
            $"다시 그려지고 있습니다. 이러면 선이 뭉개져 원화보다 흐려 보입니다.\n" +
            $"→ 에디터에서는 Game 탭 위쪽 해상도 목록을 '{targetWidth:0}x{targetHeight:0}'(또는 1920x1080)으로 " +
            $"바꾸면 배율이 1.00이 되어 선명해집니다. 빌드에서는 전체화면이면 자동으로 맞습니다.");
    }

    // 씬의 Canvas들이 카메라의 표시 영역을 따르도록 설정한다.
    //
    // Screen Space - Overlay 모드의 Canvas는 카메라와 무관하게 "화면 전체"에 그려진다.
    // 그러면 애써 만든 검은 띠 위에 UI가 겹쳐 나와서 비율 고정이 무의미해진다.
    // Screen Space - Camera 모드로 바꾸고 이 카메라를 지정하면, UI도 카메라의 4:3 영역
    // 안에만 그려진다.
    private void SetupCanvases()
    {
        // FindObjectsByType: 여러 개를 한꺼번에 찾는다(유니티 6에서 FindObjectsOfType을 대체).
        // FindObjectsInactive.Include는 꺼져 있는 오브젝트도 포함해서 찾으라는 뜻이다.
        // 지금 닫혀 있는 팝업의 Canvas도 맞춰줘야 하므로 반드시 포함해야 한다.
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var canvas in canvases)
        {
            // 다른 Canvas의 자식으로 딸려 있는 것(중첩 Canvas)은 부모를 따라가므로 건드리지 않는다.
            if (canvas.transform.parent != null &&
                canvas.transform.parent.GetComponentInParent<Canvas>() != null) continue;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;

                // planeDistance: UI를 카메라에서 얼마나 떨어뜨려 놓을지.
                // 카메라의 near/far 사이에 있어야 보인다. 기본값 100이면 대부분 문제없다.
                canvas.planeDistance = 100f;
            }

            // ===== 그림이 흐려지는 것을 막는다 =====
            // pixelPerfect를 켜면 유니티가 UI 요소를 화면의 정수 픽셀 자리에 딱 맞춰 그린다.
            // 꺼져 있으면 그림이 픽셀과 픽셀 사이(예: x=340.5)에 걸쳐 그려지면서 인접 픽셀이
            // 섞여(바이리니어 보간) 선이 한 픽셀 번지고, 원화보다 흐릿해 보인다.
            // 이 게임은 움직이는 UI가 거의 없고 선화가 많아서 켜두는 쪽이 훨씬 선명하다.
            canvas.pixelPerfect = true;
        }
    }
}
