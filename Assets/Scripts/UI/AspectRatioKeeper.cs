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

        // 카메라가 아무것도 그리지 않는 영역(검은 띠)에 보일 색.
        // clearFlags를 SolidColor로 해야 그 색으로 칠해진다.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = letterboxColor;
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

        cam.rect = rect;
    }

    // 씬의 Canvas들이 카메라의 표시 영역을 따르도록 설정한다.
    //
    // Screen Space - Overlay 모드의 Canvas는 카메라와 무관하게 "화면 전체"에 그려진다.
    // 그러면 애써 만든 검은 띠 위에 UI가 겹쳐 나와서 비율 고정이 무의미해진다.
    // Screen Space - Camera 모드로 바꾸고 이 카메라를 지정하면, UI도 카메라의 4:3 영역
    // 안에만 그려진다.
    private void SetupCanvases()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
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
        }
    }
}
