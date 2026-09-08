using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 미니게임 2: 시나리오 진행형 타임어택 - "제한시간 안에 어디까지 도달했는지"를 재는 배경 타이머
// =====================================================================================
// ===== 다른 미니게임들과 뭐가 다른가? =====
// MinigameController(미니게임 3~6 등)는 "그 자리에서 성공/실패가 즉시 갈리는" 방식이다.
// 반면 시나리오 문서의 [미니게임 2]는 "#07 회사 조사 구간을 5분 안에 사장실 세이브포인트까지
// 도달해야 한다"는 식으로, 대사/조사 화면/다른 미니게임을 넘나드는 동안 계속 흐르는
// 배경 타이머다. 그래서 MinigameController 패널 하나로는 표현할 수 없어 이 컨트롤러를
// 따로 둔다. (MinigameController는 그대로 두고, 이 컨트롤러가 그 위에 얹혀서 병행 동작한다.)
//
// ===== 동작 흐름 =====
//   1) CSV(LineType=Minigame)의 MinigameTimeLimit 칸에 초 단위 숫자가 적혀 있으면,
//      DialogueSystem.ShowNextSentence()가 그 줄을 표시하기 직전에 StartTimer()를 부른다.
//   2) 이때부터 화면 위쪽에 mm:ss 카운트다운이 뜨고, 대사/조사/미니게임 어떤 화면이 떠 있든
//      상관없이 실시간으로 줄어든다 (Update()에서 매 프레임 감산).
//   3) 남은 시간이 0이 되면, 지금 무엇을 하고 있었든 즉시 끼어들어 실패 엔딩(보통 Bad_D)으로
//      넘어간다. 조사 화면이나 미니게임 패널이 열려 있었다면 그것부터 강제로 정리한다
//      (GameFlowManager.TriggerEnding() 참고 - 여기서 InvestigationController/MinigameController를
//      ForceExit() 해준다).
//   4) 시간이 끝나기 전에 MinigameTimerStopId 칸에 적힌 SavePointId에 도달하면(=플레이어가
//      제시간에 목표 지점까지 왔다는 뜻), 타이머는 조용히 사라지고 아무 일도 일어나지 않는다.
//      (SavePointManager.OnSavePointReached 이벤트를 구독해서 판정한다.)
//
// ===== CSV 사용법 (Minigame 행에서만) =====
//   MinigameTimeLimit   : 제한시간(초). 예) 300 = 5분. 비워두면 이 행은 타이머를 켜지 않고
//                         기존 미니게임 스텁("버튼 하나 누르면 성공")으로만 동작한다.
//   MinigameTimerStopId : 도달하면 타이머가 꺼지는 세이브포인트의 SavePointId. 어떤 Narration/
//                         대사 행이든 IsSavePoint=TRUE, SavePointId=이 값 으로 적어둔 행과
//                         철자가 정확히 같아야 한다.
//   TargetEnding        : (기존 컬럼 재사용) 시간 초과 시 갈 엔딩. Minigame 행이 이미
//                         "실패 시 엔딩"으로 쓰고 있는 칸을 그대로 쓴다.
//
// ===== 씬 배치 =====
// 인스펙터에서 아무것도 연결하지 않아도 된다. GameBootstrap이 씬에 없으면 자동으로 만들고,
// 이 스크립트가 스스로 Canvas를 찾아 카운트다운 글자를 화면 위쪽에 만들어낸다
// (StageController.EnsureStageObjects()/DialogueSystem.CreateContinueIndicator()와 같은 방식).
public class TimeAttackController : MonoBehaviour
{
    public static TimeAttackController Instance;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    [Header("남은 시간이 이 초 이하로 떨어지면 빨간색 경고로 바뀐다")]
    public float warningThresholdSeconds = 60f;

    [Header("평소 색 / 경고 색")]
    public Color normalColor = new Color(1f, 0.86f, 0.45f); // 대화창 화자 이름과 같은 옅은 금색
    public Color warningColor = new Color(1f, 0.3f, 0.3f);

    // 지금 타이머가 돌아가는 중인지. DialogueSystem 등 다른 곳에서 참고할 수 있게 열어둔다.
    public bool IsRunning { get; private set; }

    private float remainingSeconds;
    private EndingType failEnding;
    private string stopAtSavePointId;

    // 코드로 만든 카운트다운 UI. 평소엔 꺼져 있다가 StartTimer()가 불릴 때만 켜진다.
    private GameObject timerRoot;
    private TMP_Text timerText;

    // ===== Awake란? =====
    // 유니티가 이 오브젝트를 만든 직후 게임 시작 전에 딱 한 번 불러주는 함수다.
    // 여기서 하는 일: "나 하나만 있으면 된다"는 규칙(싱글톤 패턴)을 지킨다 - 이미
    // TimeAttackController가 하나 있는데 또 하나가 생기면(예: 실수로 씬에 두 번 배치),
    // 새로 생긴 쪽은 곧바로 스스로를 지워서 항상 Instance 하나만 살아있게 만든다.
    // 다른 매니저(DeductionController, StageController 등)도 전부 이 방식을 쓴다.
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ===== "구독"이란? =====
    // SavePointManager는 세이브포인트를 지날 때마다 "나 방금 여기 지났어!"라고 방송(이벤트)한다.
    // 이 방송을 듣고 싶은 쪽은 OnSavePointReached += 내가만든함수 처럼 "이 방송 들을래요"라고
    // 등록(구독)해두면 된다. 그러면 방송이 나갈 때마다 그 함수가 자동으로 실행된다.
    // OnEnable은 이 오브젝트가 켜질 때, OnDisable은 꺼지거나 사라질 때 유니티가 자동으로
    // 불러주는 함수다. 켜질 때 구독을 걸고, 꺼질 때 반드시 구독을 해제해야 한다 - 안 그러면
    // 이미 사라진 이 오브젝트를 향해 방송이 계속 날아와서 오류가 나거나, 다음 씬에서 같은
    // 방송을 두 번 세 번 중복해서 듣게 된다.
    private void OnEnable()
    {
        // 목표 세이브포인트에 도달했는지는 이렇게 SavePointManager의 방송을 구독해서 판단한다.
        if (SavePointManager.Instance != null)
        {
            SavePointManager.Instance.OnSavePointReached += HandleSavePointReached;
        }
    }

    private void OnDisable()
    {
        if (SavePointManager.Instance != null)
        {
            SavePointManager.Instance.OnSavePointReached -= HandleSavePointReached;
        }
    }

    // ---------------------------------------------------------------------------------
    // 타이머 시작 / 정지
    // ---------------------------------------------------------------------------------

    // DialogueSystem.ShowNextSentence()가 MinigameTimeLimit이 적힌 Minigame 행을 만났을 때 부른다.
    //   seconds    : 제한시간(초)
    //   stopSavePointId : 이 세이브포인트에 도달하면 성공으로 보고 조용히 꺼진다.
    //   onFailEnding    : 시간 초과 시 이동할 엔딩.
    public void StartTimer(float seconds, string stopSavePointId, EndingType onFailEnding)
    {
        if (seconds <= 0f)
        {
            Debug.LogWarning("[TimeAttackController] 제한시간이 0 이하라 타이머를 켜지 않습니다.");
            return;
        }

        remainingSeconds = seconds;
        failEnding = onFailEnding;
        stopAtSavePointId = string.IsNullOrWhiteSpace(stopSavePointId) ? null : stopSavePointId.Trim();
        IsRunning = true;

        if (stopAtSavePointId == null)
        {
            // 목표 지점이 안 적혀 있으면 시간이 다 될 때까지 절대 안 꺼진다 - CSV 실수를
            // 바로 알아챌 수 있게 경고만 남기고 그대로 진행한다(게임을 막지는 않는다).
            Debug.LogWarning("[TimeAttackController] MinigameTimerStopId가 비어 있어 이 타이머는 " +
                              "세이브포인트로는 멈추지 않고 시간 초과로만 끝납니다. CSV를 확인하세요.");
        }

        EnsureTimerUI();
        // Canvas를 못 찾아 UI를 못 만들었더라도(EnsureTimerUI 안의 에러 로그 참고) 타이머
        // 자체(IsRunning/remainingSeconds)는 계속 흘러가게 둔다 - 화면에 안 보일 뿐, 시간
        // 초과로 배드엔딩에 가는 로직까지 같이 멈추면 안 되기 때문이다.
        if (timerRoot != null)
        {
            timerRoot.SetActive(true);
            UpdateTimerText();
        }

        Debug.Log($"[TimeAttackController] 타임어택 시작: {seconds}초, 목표 세이브포인트='{stopAtSavePointId}'");
    }

    // 시간 초과가 아닌 이유로(목표 지점 도달, 강제 종료 등) 타이머를 멈춘다.
    private void StopTimer()
    {
        IsRunning = false;
        if (timerRoot != null) timerRoot.SetActive(false);
    }

    // 목표 세이브포인트에 도달했다는 이벤트가 오면 판정한다.
    private void HandleSavePointReached(string savePointId)
    {
        if (!IsRunning || stopAtSavePointId == null) return;
        if (savePointId != stopAtSavePointId) return;

        Debug.Log($"[TimeAttackController] 목표 세이브포인트('{stopAtSavePointId}') 도달 - 타임어택 성공, 타이머를 끕니다.");
        StopTimer();
    }

    // ===== Update란? =====
    // 유니티가 "화면을 한 프레임 그릴 때마다" 자동으로 불러주는 함수다(1초에 보통 30~60번).
    // 그래서 시간이 계속 줄어드는 것처럼 보이는 카운트다운을 여기서 만든다.
    private void Update()
    {
        if (!IsRunning) return;

        // Time.deltaTime = "지난 프레임부터 지금까지 걸린 실제 시간(초)". 컴퓨터 성능에 따라
        // 프레임이 빠르거나 느릴 수 있으므로, 그냥 "1씩 깎기"가 아니라 이 값만큼 깎아야
        // 실제 시계와 똑같은 속도로 줄어든다.
        remainingSeconds -= Time.deltaTime;

        if (remainingSeconds <= 0f)
        {
            remainingSeconds = 0f;
            UpdateTimerText();

            // ===== 시간 초과 =====
            // 지금 대사가 어디까지 진행됐든, 조사 화면이나 다른 미니게임 패널이 열려 있든
            // 상관없이 즉시 엔딩으로 끼어든다. 남아있는 화면 정리는 GameFlowManager.TriggerEnding()
            // 쪽에서 InvestigationController/MinigameController를 ForceExit() 해서 처리한다.
            IsRunning = false;
            if (timerRoot != null) timerRoot.SetActive(false);

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.TriggerEnding(failEnding);
            }
            else
            {
                Debug.LogError("[TimeAttackController] GameFlowManager가 없어 시간 초과 엔딩을 발동하지 못했습니다.");
            }
            return;
        }

        UpdateTimerText();
    }

    // ---------------------------------------------------------------------------------
    // 카운트다운 UI (코드로 생성)
    // ---------------------------------------------------------------------------------

    // 화면 위쪽 가운데에 반투명 검은 상자 + mm:ss 글자를 만든다.
    // DialogueSystem.CreateContinueIndicator()/InvestigationController.CreateExitButton()와
    // 같은 이유로 씬 파일을 직접 건드리지 않고 코드로 만든다.
    private void EnsureTimerUI()
    {
        if (timerRoot != null) return;

        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[TimeAttackController] 씬에 Canvas가 없어 타이머 UI를 만들 수 없습니다.");
            return;
        }

        timerRoot = new GameObject("TimeAttackTimer", typeof(RectTransform), typeof(Image));
        timerRoot.transform.SetParent(targetCanvas.transform, false);

        // ===== 화면 위치 잡기 (RectTransform) =====
        // 유니티 UI는 "화면의 어느 모서리/가운데를 기준으로 삼을지(anchor)"와 "그 기준점에서
        // 얼마나 떨어질지(anchoredPosition)"로 위치를 정한다. 아래 설정은 "화면 위쪽 가운데를
        // 기준점으로 삼고, 거기서 아래로 20픽셀 내려온 자리에 가로 160 x 세로 56 크기의
        // 상자를 놓는다"는 뜻이다 - 즉 화면 맨 위 중앙에 뜨는 작은 박스.
        var rt = timerRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);  // 기준 사각형의 왼쪽 아래 = 화면 위쪽 가운데
        rt.anchorMax = new Vector2(0.5f, 1f);  // 기준 사각형의 오른쪽 위 = 화면 위쪽 가운데 (min과 같으면 "점 하나"가 기준이 된다)
        rt.pivot = new Vector2(0.5f, 1f);      // 이 오브젝트 자신의 중심점도 "위쪽 가운데"로 맞춘다
        rt.anchoredPosition = new Vector2(0f, -20f); // 기준점에서 아래로 20픽셀
        rt.sizeDelta = new Vector2(160f, 56f);       // 가로 160, 세로 56 크기

        var bg = timerRoot.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(timerRoot.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        timerText = textGo.AddComponent<TextMeshProUGUI>();
        timerText.fontSize = 32;
        timerText.fontStyle = TMPro.FontStyles.Bold;
        timerText.alignment = TMPro.TextAlignmentOptions.Center;
        timerText.color = normalColor;
        timerText.raycastTarget = false;

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨져 보인다 - 다만 mm:ss는
        // 숫자/콜론뿐이라 당장은 문제없지만, 나중에 라벨을 덧붙일 수도 있으니 통일해서 물려둔다.
        UIFontHelper.ApplyToChildren(timerRoot);

        // 타이머가 아직 시작되지 않았으니 일단 꺼둔다. StartTimer()가 불릴 때 다시 켠다.
        // (화면에 항상 맨 위로 뜨게 하는 처리는 LateUpdate() 참고.)
        timerRoot.SetActive(false);
    }

    // ===== 왜 Update가 아니라 LateUpdate인가? =====
    // 유니티는 한 프레임 안에서 모든 오브젝트의 Update()를 먼저 다 실행한 뒤에, 그다음
    // LateUpdate()를 전부 실행한다. 조사 화면이나 미니게임 패널이 "이번 프레임의 Update()
    // 안에서" 새로 만들어질 수 있으므로, 순서를 확실히 뒤로 미루기 위해 LateUpdate에서
    // "제일 위로 올리기"를 한다 - 그래야 이번 프레임에 새로 생긴 패널보다도 확실하게 위에 뜬다.
    //
    // ===== "맨 위로 올린다"는 게 무슨 뜻인가? (SetAsLastSibling) =====
    // 유니티 UI는 같은 부모(Canvas) 밑에 여러 형제 오브젝트가 나란히 있을 때, 목록의
    // 아래쪽(나중)에 있는 것이 화면에서는 앞쪽(위)에 그려진다. 대화창/선택지/조사 화면/
    // 미니게임 패널이 전부 이 타이머와 같은 Canvas 밑 형제이므로(StageController/
    // InvestigationController 참고), 형제 목록의 맨 끝으로 보내주면 그 어떤 패널보다도
    // 항상 화면에서 가장 위에(가려지지 않고) 보이게 된다.
    private void LateUpdate()
    {
        if (timerRoot != null && timerRoot.activeSelf)
        {
            timerRoot.transform.SetAsLastSibling();
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.CeilToInt(remainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
        timerText.color = remainingSeconds <= warningThresholdSeconds ? warningColor : normalColor;
    }
}
