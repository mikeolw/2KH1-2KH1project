using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 엔딩 분기 담당 - 어떤 엔딩이 발동하면 그에 맞는 엔딩 CSV로 넘어간다
// =====================================================================================
// ===== 예전에는 어땠나 =====
// TriggerEnding()이 Debug.Log로 "엔딩 발생: Bad_A"라고 찍기만 하고 실제로는 아무 일도
// 일어나지 않았다. 즉 선택지에서 엔딩을 골라도 게임이 그대로 멈춰 있었다.
//
// ===== 지금은 =====
// EndingType(엔딩 종류)마다 대응하는 엔딩 CSV 파일 이름을 표로 들고 있다가,
// 엔딩이 발동하면 DialogueSystem에게 그 CSV를 재생하라고 넘긴다.
//
// ===== 엔딩 루트 (시나리오 문서 기준) =====
//   #07 마지막 선택지
//     1. 되갚아준다
//        1-1. 차장에게 복수한다  -> Bad_B (조폭을 써서 차장 처리, 새 사장이 됨)
//        1-2. 사장에게 복수한다  -> Bad_A (사장을 칼로 찌름, 살인범이 됨)
//     2. 아무것도 하지 않는다    -> Bad_D (일상으로 복귀, 훗날 붕괴 사고)
//     3. 내가 해야만 하는 일이... -> True (내부 고발, 아버지를 경찰에 넘김)
//   그 밖의 분기
//     추리(단서 조합) 실패        -> Bad_D
//     조사 중 진범/파벌에게 들킴  -> Bad_C (도망치다 바다에 빠짐)
//     분노 폭발 루트              -> Bad_E (사무실 방화)
//
// 위 연결은 아래 endingCsvMap 하나만 고치면 바꿀 수 있다. 선택지 CSV에서는
// IsEndingChoice=TRUE, TargetEnding=Bad_A 처럼 "엔딩 종류"만 적으면 되고,
// 어떤 파일이 재생될지는 여기서 정한다.
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance;

    [Header("엔딩 진입 시 잠깐 보여줄 암전 시간(초)")]
    [Tooltip("엔딩 CSV로 넘어가기 전에 화면을 어둡게 해서 장면 전환을 부드럽게 만든다.")]
    public float endingFadeDelay = 0.5f;

    // 엔딩 종류 -> 재생할 CSV 파일 이름(확장자 제외).
    // 파일은 Assets/Resources/Dialogues/ 안에 있어야 한다.
    private readonly Dictionary<EndingType, string> endingCsvMap = new Dictionary<EndingType, string>
    {
        { EndingType.True,   "scenario_ending_true" },
        { EndingType.Bad_A,  "scenario_ending_bad_a" },
        { EndingType.Bad_B,  "scenario_ending_bad_b" },
        { EndingType.Bad_C,  "scenario_ending_bad_c" },
        { EndingType.Bad_D,  "scenario_ending_bad_d" },
        { EndingType.Bad_E,  "scenario_ending_bad_e" },
        // Normal 엔딩은 아직 전용 CSV가 없어서 트루엔딩 파일을 임시로 쓴다.
        // 노말엔딩 대본이 나오면 여기 파일 이름만 바꾸면 된다.
        { EndingType.Normal, "scenario_ending_true" },
    };

    // 이미 엔딩에 들어갔는지. 엔딩 도중에 또 엔딩이 발동해서 겹치는 것을 막는다.
    private bool endingStarted;

    // 지금 어떤 엔딩으로 진행 중인지. 엔딩 화면 UI 등에서 참고할 수 있게 열어둔다.
    public EndingType CurrentEnding { get; private set; } = EndingType.None;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 엔딩을 발동시킨다.
    //   - 선택지에서 IsEndingChoice=TRUE인 항목을 골랐을 때 (DialogueSystem.ShowChoices)
    //   - 미니게임/추리에 실패했을 때 (DialogueSystem, DeductionController)
    //   - 조사 중 진범에게 들켰을 때
    // 어디서 불러도 동작은 같다: 해당 엔딩 CSV를 재생한다.
    public void TriggerEnding(EndingType ending)
    {
        if (ending == EndingType.None)
        {
            Debug.LogWarning("[GameFlowManager] EndingType.None으로 엔딩이 요청되었습니다. " +
                             "CSV의 TargetEnding 칸에 엔딩 이름(True, Bad_A 등)을 제대로 적었는지 확인하세요.");
            return;
        }

        // 엔딩 도중에 또 엔딩이 발동하면 대사가 뒤섞이므로 첫 번째만 인정한다.
        if (endingStarted)
        {
            Debug.LogWarning($"[GameFlowManager] 이미 '{CurrentEnding}' 엔딩이 진행 중이라 '{ending}' 요청을 무시합니다.");
            return;
        }

        if (!endingCsvMap.TryGetValue(ending, out string csvName))
        {
            Debug.LogError($"[GameFlowManager] '{ending}' 엔딩에 연결된 CSV가 없습니다. endingCsvMap을 확인하세요.");
            return;
        }

        endingStarted = true;
        CurrentEnding = ending;

        Debug.Log($"[GameFlowManager] 엔딩 진입: {ending} -> {csvName}.csv");

        // 엔딩으로 넘어가기 전에 열려 있는 팝업들을 전부 닫는다.
        // (가방이 열린 채로 엔딩이 시작되면 어색하다)
        if (UIManager.Instance != null) UIManager.Instance.CloseAllPanels();

        if (DialogueSystem.Instance == null)
        {
            Debug.LogError("[GameFlowManager] DialogueSystem이 없어 엔딩 CSV를 재생할 수 없습니다.");
            return;
        }

        DialogueSystem.Instance.LoadDialogueFromCSV(csvName);
    }

    // 새 게임을 시작할 때 엔딩 상태를 초기화한다.
    public void ResetForNewGame()
    {
        endingStarted = false;
        CurrentEnding = EndingType.None;
    }
}
