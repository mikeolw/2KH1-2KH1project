using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 타이밍 클릭 미니게임 (자료실 문 잠입에서 사용)
// =====================================================================================
// ===== 어떤 게임인가 =====
// 화면 가운데에 외곽선만 있는 원이 뜨고, 그 원 위에 "성공 구간"인 짧은 원호가 표시된다.
// 원 반지름 길이의 바늘이 9시 방향에서 출발해 시계 방향으로 돈다. 바늘이 원호 위에
// 있을 때(=성공 구간에 들어와 있을 때) 원 중앙의 "CLICK" 글자가 빨간색으로 바뀐다.
//   - 바늘이 원호 안에 있을 때 클릭 -> 성공 (onSuccess)
//   - 원호에 닿기 전에 미리 클릭 -> 실패 (onFail)
//   - 클릭하지 않고 바늘이 원호를 완전히 지나쳐버림 -> 실패 (onFail)
//
// ===== 왜 그림 파일 없이 코드로 원을 그리나 =====
// 이 원/바늘/원호는 서사가 담긴 CG가 아니라 미니게임 조작을 표시하는 UI 부품이다.
// IllustLoader로 불러오는 배경/오브젝트 그림과 달리 매번 똑같은 모양이면 되므로,
// Resources 폴더에 그림 파일을 새로 그려 넣을 필요 없이 텍스처를 코드로 직접 그려서
// 쓴다(BuildRingSprite 참고). 팀원이 그림을 준비하지 않아도 바로 동작한다.
//
// ===== 씬 배치 =====
// TimeAttackController와 같은 방식이다. 인스펙터에서 아무것도 연결하지 않아도 되고,
// GameBootstrap이 씬에 없으면 자동으로 만들어준다. 스스로 Canvas를 찾아 UI를 만든다.
//
// ===== 난이도를 조절하려면 =====
// 아래 [Header("난이도 조절")] 밑의 세 값을 인스펙터에서 바꾸면 된다(씬에 이 컴포넌트를
// 직접 하나 만들어 배치하면 그 값이 자동 생성보다 우선한다 - 다른 매니저들과 같은 방식).
//   rotationDurationSeconds : 바늘이 한 바퀴(360도) 도는 데 걸리는 시간. 짧을수록 어렵다.
//   arcWidthDegrees         : 성공 구간(원호)의 각도 폭. 클수록 쉽다.
//   arcStartClockDegrees    : 성공 구간이 시작하는 위치. 12시 방향을 0도로 놓고
//                             시계 방향으로 잰 각도(예: 90 = 3시 방향).
//
// ===== 다른 곳에서도 이 미니게임을 쓰려면 =====
// StartGame(onSuccess, onFail)만 호출하면 된다 (MinigameController.StartMinigame과
// 같은 콜백 방식). 지금은 자료실 문(InvestigationController의 자료실 문 처리 부분)
// 한 곳에서만 쓰지만, 똑같은 "타이밍에 맞춰 클릭" 방식이 필요한 곳이 또 생기면
// 그대로 재사용하면 된다.
public class TimingClickMinigameController : MonoBehaviour
{
    public static TimingClickMinigameController Instance;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    [Header("난이도 조절")]
    [Tooltip("바늘이 원을 한 바퀴 도는 데 걸리는 시간(초). 짧을수록 어렵다.")]
    public float rotationDurationSeconds = 2.2f;
    [Tooltip("성공 구간(원호)의 각도 폭. 클수록 쉽다.")]
    public float arcWidthDegrees = 40f;
    [Tooltip("성공 구간이 시작하는 위치. 12시 방향을 0도로 놓고 시계 방향으로 잰 각도.")]
    public float arcStartClockDegrees = 30f;

    [Header("색상")]
    public Color ringColor = Color.white;
    public Color arcColor = new Color(1f, 0.35f, 0.25f, 0.95f);
    public Color needleColor = Color.white;
    public Color clickTextNormalColor = Color.white;
    public Color clickTextHitColor = new Color(1f, 0.15f, 0.15f);

    // 지금 미니게임 패널이 떠 있는지. GameFlowManager.TriggerEnding()이 엔딩 도중 갑자기
    // 끼어들 때(ForceExit) 이 값을 보고 정리할 게 있는지 판단한다.
    public bool IsActive => root != null && root.activeSelf;

    private GameObject root;
    private RectTransform needle;
    private Image arcImage;
    private TMP_Text clickText;

    private bool running;
    private bool resolved;
    private float elapsed;

    // ===== 연속 각도 공간 =====
    // 바늘은 9시 방향(시계 기준 270도)에서 출발해 630도(=270+360, 한 바퀴 돌아 다시 9시)
    // 까지 쭉 증가하는 값으로 다룬다. 원호의 시작/끝 각도도 "270도 이후" 공간으로 맞춰
    // 두면 되므로, 270도보다 작은 시계각은 360도를 더해 옮겨둔다(StartGame 참고).
    // 이렇게 하면 "몇 도를 지났다"를 매 프레임 간단한 대소비교 하나로 판정할 수 있다.
    private float arcStartContinuous;
    private float arcEndContinuous;

    private Action onSuccess;
    private Action onFail;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // InvestigationController 등에서 미니게임을 시작할 때 부른다.
    public void StartGame(Action onSuccessCallback, Action onFailCallback)
    {
        if (!EnsureUI())
        {
            // UI를 못 만들면(씬에 Canvas가 없는 등) 미니게임으로 막아버리는 것보다,
            // 그냥 통과시키는 쪽이 안전하다 - TimeAttackController.EnsureTimerUI()와 같은 방침.
            Debug.LogError("[TimingClickMinigameController] UI를 만들지 못해 미니게임을 시작할 수 없습니다. 곧바로 성공 처리합니다.");
            onSuccessCallback?.Invoke();
            return;
        }

        onSuccess = onSuccessCallback;
        onFail = onFailCallback;
        resolved = false;
        running = true;
        elapsed = 0f;

        // 원호 위치를 "270도 이후" 연속 공간으로 옮긴다.
        float arcStartClock = ((arcStartClockDegrees % 360f) + 360f) % 360f;
        arcStartContinuous = arcStartClock < 270f ? arcStartClock + 360f : arcStartClock;
        float width = Mathf.Max(1f, arcWidthDegrees);
        arcEndContinuous = arcStartContinuous + width;

        // Radial360으로 잘라낸 원호를 실제 화면 위치(시계각)에 맞춰 돌려놓는다.
        // z 회전은 반시계가 양수이므로, 시계 기준 각도만큼 시계 방향(음수)으로 돌린다.
        arcImage.transform.localEulerAngles = new Vector3(0f, 0f, -arcStartClockDegrees);
        arcImage.fillAmount = width / 360f;
        arcImage.color = arcColor;

        clickText.text = "CLICK";
        clickText.color = clickTextNormalColor;

        // 바늘을 9시 방향(z=90)에 놓고 시작한다 (EnsureUI 상단 주석의 좌표 설명 참고).
        needle.localEulerAngles = new Vector3(0f, 0f, 90f);
        needle.gameObject.GetComponent<Image>().color = needleColor;

        root.SetActive(true);
    }

    private void Update()
    {
        if (!running || resolved) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, rotationDurationSeconds));

        // 지금 바늘의 각도 (270도에서 시작해 630도까지 증가).
        float currentContinuous = 270f + 360f * t;

        // 화면에 그릴 회전값. 9시(90도)에서 시작해 시계 방향(z 감소)으로 360도 돈다.
        needle.localEulerAngles = new Vector3(0f, 0f, 90f - 360f * t);

        bool inArc = currentContinuous >= arcStartContinuous && currentContinuous <= arcEndContinuous;
        clickText.color = inArc ? clickTextHitColor : clickTextNormalColor;

        // 클릭 없이 원호를 완전히 지나쳐버리거나(가장 흔한 실패), 한 바퀴를 다 돌 때까지도
        // 정리가 안 됐으면(원호가 아주 끝부분에 있는 경우의 안전장치) 실패 처리한다.
        if (currentContinuous > arcEndContinuous || t >= 1f)
        {
            Fail();
        }
    }

    // 화면 아무 곳이나 누르면 호출된다 (root 전체를 덮는 버튼의 OnClick).
    private void OnScreenClicked()
    {
        if (!running || resolved) return;

        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, rotationDurationSeconds));
        float currentContinuous = 270f + 360f * t;

        bool inArc = currentContinuous >= arcStartContinuous && currentContinuous <= arcEndContinuous;
        if (inArc) Success();
        else Fail();
    }

    private void Success()
    {
        if (resolved) return;
        resolved = true;
        running = false;
        root.SetActive(false);
        onSuccess?.Invoke();
    }

    private void Fail()
    {
        if (resolved) return;
        resolved = true;
        running = false;
        root.SetActive(false);
        onFail?.Invoke();
    }

    // ===== 엔딩이 미니게임 도중에 갑자기 끼어들 때 =====
    // MinigameController.ForceExit()과 같은 이유/방식. onSuccess/onFail을 부르지 않고
    // 패널만 치운다 - 이미 다른 경로로 엔딩이 확정된 상황이라 콜백을 또 부르면 안 된다.
    public void ForceExit()
    {
        if (resolved) return;
        resolved = true;
        running = false;
        if (root != null) root.SetActive(false);
        onSuccess = null;
        onFail = null;
    }

    // ---------------------------------------------------------------------------------
    // UI 생성 (코드로 직접 - TimeAttackController.EnsureTimerUI()와 같은 방식)
    // ---------------------------------------------------------------------------------
    private bool EnsureUI()
    {
        if (root != null) return true;

        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[TimingClickMinigameController] 씬에 Canvas가 없어 UI를 만들 수 없습니다.");
            return false;
        }

        // 화면 전체를 덮는 반투명 배경이자, 클릭을 감지하는 버튼.
        root = new GameObject("TimingClickMinigame", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(targetCanvas.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        root.GetComponent<Button>().onClick.AddListener(OnScreenClicked);

        // 원 (외곽선만 - 링 모양 텍스처를 그대로 보여준다)
        var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(root.transform, false);
        var ringRect = ringGo.GetComponent<RectTransform>();
        ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = new Vector2(320f, 320f);
        ringRect.anchoredPosition = Vector2.zero;
        var ringImage = ringGo.GetComponent<Image>();
        ringImage.sprite = BuildRingSprite();
        ringImage.color = ringColor;
        ringImage.raycastTarget = false;

        // 원호(성공 구간) - 같은 링 텍스처를 Radial360로 잘라 일부만 보여준다.
        // ===== 왜 이렇게 하면 "원 위의 임의 구간"이 되나 =====
        // Image.Type.Filled + FillMethod.Radial360은 이미지를 부채꼴로 잘라 보여준다.
        // 원본이 이미 "링(도넛)" 모양이므로, 부채꼴로 자른 결과는 자연히 "원호(링의 일부)"가
        // 된다. fillOrigin=Top으로 12시 방향에서 시작하게 해두고, 오브젝트 자체를
        // z축으로 돌리면 그 회전만큼 시작 위치가 옮겨간다 - 그래서 시작 각도(arcStartClockDegrees)를
        // 자유롭게 바꿀 수 있다. fillAmount는 "360도 중 몇 도를 보여줄지"의 비율이라
        // 원호의 폭(arcWidthDegrees)이 된다. 실제 회전/fillAmount 값은 StartGame()에서
        // 매번 최신 설정으로 다시 계산해 넣는다.
        var arcGo = new GameObject("Arc", typeof(RectTransform), typeof(Image));
        arcGo.transform.SetParent(root.transform, false);
        var arcRect = arcGo.GetComponent<RectTransform>();
        arcRect.anchorMin = arcRect.anchorMax = new Vector2(0.5f, 0.5f);
        arcRect.sizeDelta = new Vector2(320f, 320f);
        arcRect.anchoredPosition = Vector2.zero;
        arcImage = arcGo.GetComponent<Image>();
        arcImage.sprite = BuildRingSprite();
        arcImage.raycastTarget = false;
        arcImage.type = Image.Type.Filled;
        arcImage.fillMethod = Image.FillMethod.Radial360;
        arcImage.fillOrigin = (int)Image.Origin360.Top;
        arcImage.fillClockwise = true;

        // 바늘 (원 반지름 길이의 얇은 막대).
        // ===== 회전 좌표 설명 =====
        // pivot을 막대의 아래쪽 끝(0.5, 0)에 두면, 이 막대는 원 중심을 축으로 삼아
        // 회전한다. 회전값(z)이 0이면 위쪽(12시)을 가리키고, 유니티는 z가 커질수록
        // 반시계 방향으로 도므로, z=90은 12시에서 반시계로 90도 = 9시 방향이 된다.
        // "시계 방향으로 돈다"는 곧 z 값이 점점 작아진다는 뜻이다(Update() 참고).
        var needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
        needleGo.transform.SetParent(root.transform, false);
        needle = needleGo.GetComponent<RectTransform>();
        needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
        needle.pivot = new Vector2(0.5f, 0f);
        needle.sizeDelta = new Vector2(6f, 158f);
        needle.anchoredPosition = Vector2.zero;
        needleGo.GetComponent<Image>().raycastTarget = false;

        // 원 중앙의 "CLICK" 글자.
        var textGo = new GameObject("ClickText", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(220f, 80f);
        textRect.anchoredPosition = Vector2.zero;
        clickText = textGo.AddComponent<TextMeshProUGUI>();
        clickText.text = "CLICK";
        clickText.fontSize = 36;
        clickText.fontStyle = TMPro.FontStyles.Bold;
        clickText.alignment = TMPro.TextAlignmentOptions.Center;
        clickText.color = clickTextNormalColor;
        clickText.raycastTarget = false;

        // 코드로 만든 글자에는 한글 글꼴이 없다 - "CLICK"은 영문이라 당장은 문제없지만,
        // 다른 코드 생성 UI와 같은 방식으로 통일해서 물려둔다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(root);

        root.SetActive(false);
        return true;
    }

    // 256x256 텍스처 안에 두께 14짜리 링(도넛) 모양을 그려 Sprite로 만든다. 한 번만
    // 만들어서 재사용한다(ringSpriteCache) - 이 미니게임 안에서 원과 원호 그림 둘 다
    // 이 텍스처를 그대로 쓴다(원호 쪽은 Radial360으로 일부만 잘라 보여줄 뿐이다).
    private static Sprite ringSpriteCache;
    private static Sprite BuildRingSprite()
    {
        if (ringSpriteCache != null) return ringSpriteCache;

        const int size = 256;
        const float outerRadius = size / 2f - 4f;
        const float thickness = 14f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                bool onRing = dist <= outerRadius && dist >= outerRadius - thickness;
                pixels[y * size + x] = onRing ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        ringSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return ringSpriteCache;
    }
}
