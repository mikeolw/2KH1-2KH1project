using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 미니게임 완전 스텁 (실제 기획 확정 전 placeholder)
// =====================================================================================
// 시나리오 대본에는 6개의 미니게임(추리/타임어택형 5개)이 언급되어 있지만, 조작 방식/난이도/
// 아트가 하나도 정해지지 않았다. 그 상태에서 "제한시간 안에 정답 상자 클릭" 같은 구체적인
// 조작을 미리 만들어두면, 실제 기획이 정해졌을 때 그 가정 자체를 갈아엎어야 할 수 있다.
//
// 그래서 지금은 "버튼 하나 누르면 무조건 성공"하는 완전한 스텁만 구현한다. 대신 아래
// 두 가지는 실제 미니게임이 뭐가 되든 그대로 재사용할 수 있게 설계했다:
//   1) CSV(LineType=Minigame) -> DialogueSystem -> MinigameController -> 성공/실패 콜백
//      -> (성공)다음 대사로 진행 / (실패)GameFlowManager.TriggerEnding() 으로 이어지는 배관.
//   2) 아래 StartMinigame()의 콜백 기반 API 형태(성공/실패를 Action으로 알려주는 방식).
// 즉, 나중에 바뀌는 건 이 스크립트의 내부 구현(패널 안에 뭐가 있고 어떻게 조작하는지)뿐이고,
// DialogueSystem이나 CSV 스키마 쪽은 크게 안 건드려도 되도록 만들어졌다.
//
// ===== CSV 사용법 =====
// LineType을 "Minigame"으로 적고 다음 컬럼을 채운다:
//   MinigameLabel : 상단에 보여줄 안내 문구 (예: "자료실을 조사하라")
//   TargetEnding  : 실패 시 연결할 EndingType (Choice 행과 같은 컬럼을 재사용함).
//                   지금 스텁은 항상 성공하므로 실제로는 호출되지 않지만, 나중에 진짜
//                   실패 조건이 생기면 바로 쓸 수 있도록 미리 CSV에 적어둬도 된다.
//
// ===== 나중에 진짜 미니게임으로 확장하는 방법 =====
//   1) DialogueLine.cs에 필요한 파라미터 필드를 추가한다 (조작 방식에 맞게).
//   2) DialogueSystem.LoadDialogueFromCSV()의 "Minigame" 파싱부에서 해당 CSV 컬럼을
//      읽어와 그 필드를 채운다.
//   3) 아래 StartMinigame()의 시그니처에 필요한 파라미터를 추가하고, panel 안의 UI를
//      실제 조작에 맞게 새로 구성한다 (지금의 "버튼 하나 누르면 성공" 구조를 대체).
//   4) Success()/Fail()을 실제 성공/실패 조건이 발생하는 지점에서 호출해주면 된다 -
//      이 두 함수가 onSuccess/onFail 콜백을 실행해 DialogueSystem으로 결과를 돌려주는
//      구조는 그대로 유지하면 된다.
public class MinigameController : MonoBehaviour
{
    public static MinigameController Instance;

    [Header("UI 연결")]
    public GameObject panel;
    public TMP_Text labelText;

    // 버튼은 씬에 고정해두지 않고 DialogueSystem.ShowChoices()와 동일한 방식(ChoiceButton.prefab을
    // 런타임에 Instantiate)으로 만든다 - 새 Button GameObject를 씬 YAML에 직접 손으로 추가하는
    // 것보다 기존에 검증된 프리팹을 재사용하는 쪽이 훨씬 안전하다.
    public GameObject confirmButtonPrefab; // ChoiceButton.prefab
    public Transform buttonContainer;      // panel의 VerticalLayoutGroup 밑 (보통 panel 자신)

    public bool IsActive => panel != null && panel.activeSelf;

    private bool resolved;
    private Action onSuccess;
    private Action onFail;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (panel != null) panel.SetActive(false);

        GameObject buttonObj = Instantiate(confirmButtonPrefab, buttonContainer);
        var text = buttonObj.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = "진행하기";
        buttonObj.GetComponent<Button>().onClick.AddListener(Success);
    }

    // label만 받는다 - 실제 미니게임 파라미터(제한시간, 난이도 등)가 정해지면 여기에
    // 인자를 추가하면 된다. 호출부(DialogueSystem.ShowNextSentence())도 같이 늘어난다.
    public void StartMinigame(string label, Action onSuccessCallback, Action onFailCallback)
    {
        labelText.text = label;
        resolved = false;
        onSuccess = onSuccessCallback;
        onFail = onFailCallback;

        panel.SetActive(true);
    }

    private void Success()
    {
        if (resolved) return;
        resolved = true;
        panel.SetActive(false);
        onSuccess?.Invoke();
    }

    // 지금은 아무 데서도 호출되지 않는다(스텁이 항상 성공하므로). 실제 실패 조건이
    // 생기면 그 지점에서 이 함수를 호출하면 된다 - onFail 콜백 실행 로직은 이미 완성돼있다.
    private void Fail()
    {
        if (resolved) return;
        resolved = true;
        panel.SetActive(false);
        onFail?.Invoke();
    }
}
