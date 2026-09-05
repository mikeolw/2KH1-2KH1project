using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 추리 파트 - 지금까지 모은 단서로 사실을 맞히는 선택지 문제 (시나리오의 [이벤트 5])
// =====================================================================================
// ===== 원래 있던 것과 뭐가 다른가? =====
// 원래도 CSV의 Choice 행으로 "선택지 하나 고르면 엔딩으로 직행" 정도는 가능했다.
// 하지만 추리 파트는 다음이 더 필요했다:
//   1) 문제를 여러 개 연속으로 내야 한다 (사인은? 범행 장소는? 배후는? ...)
//   2) 정답을 고르면 "맞았다"는 반응을 보여주고 다음 문제로 넘어가야 한다
//   3) 하나라도 틀리면 그 자리에서 바로 배드엔딩D로 떨어져야 한다
//   4) 문제/보기/정답을 기획자가 CSV로 고칠 수 있어야 한다
// 그래서 선택지 UI는 기존 것을 그대로 빌려 쓰되, 문제 진행과 정답 판정만 이 클래스가 맡는다.
//
// ===== 배관 구조 =====
// MinigameController / InvestigationController와 완전히 똑같은 콜백 방식이다.
//   CSV(LineType=Deduction) -> DialogueSystem -> Enter() -> (전부 맞히면) onSuccess 콜백
//                                                        -> (틀리면) 바로 배드엔딩D
// 그래서 DialogueSystem 쪽 코드 모양이 미니게임/조사와 판박이라 헷갈릴 일이 없다.
//
// ===== 문제 데이터: Resources/Dialogues/DeductionData.csv =====
//   DeductionId : 추리 묶음의 이름 (CSV의 DeductionId 칸과 매칭). 예: deduction_01
//   Step        : 몇 번째 문제인지 (1, 2, 3...). 같은 Step의 행들이 한 문제의 보기가 된다.
//   Question    : 문제 문장. 같은 Step의 첫 줄에만 적으면 되고, 나머지는 비워도 된다.
//   ChoiceText  : 보기 하나의 문구.
//   IsCorrect   : 이 보기가 정답이면 TRUE.
//   ResultText  : 이 보기를 골랐을 때 보여줄 반응 문장.
//                 정답이면 "맞다, ~였다" 같은 납득, 오답이면 엔딩 직전의 마지막 독백.
//   WrongEnding : 이 보기를 골라서 틀렸을 때 갈 엔딩 (비워두면 Bad_D).
//
// 예)
//   deduction_01,1,한성은 어떻게 죽었나?,스스로 바위로 뛰어내렸다,FALSE,...,Bad_D
//   deduction_01,1,,물로 뛰어들었고 그 전에 이미 다쳐 있었다,TRUE,맞다. 학생은 '물'이라고 했다.,
public class DeductionController : MonoBehaviour
{
    public static DeductionController Instance;

    // 보기 하나.
    private class DeductionChoice
    {
        public string text;
        public bool isCorrect;
        public string resultText;
        public EndingType wrongEnding;
    }

    // 문제 하나 (보기 여러 개를 가진다).
    private class DeductionStep
    {
        public int step;
        public string question;
        public readonly List<DeductionChoice> choices = new List<DeductionChoice>();
    }

    [Header("UI 연결 (DialogueSystem의 선택지 UI를 그대로 빌려 쓴다)")]
    [Tooltip("문제 문장을 표시할 텍스트. 비워두면 DialogueSystem의 대사창을 대신 쓴다.")]
    public TMP_Text questionText;
    [Tooltip("보기 버튼들이 들어갈 부모. 비워두면 DialogueSystem.choiceContainer를 쓴다.")]
    public Transform choiceContainer;
    [Tooltip("보기 버튼 프리팹. 비워두면 DialogueSystem.choiceButtonPrefab을 쓴다.")]
    public GameObject choiceButtonPrefab;
    [Tooltip("보기들을 감싸는 패널. 비워두면 DialogueSystem.choicePanel을 쓴다.")]
    public GameObject choicePanel;

    private const string DeductionCsv = "Dialogues/DeductionData";

    // DeductionId -> 그 묶음의 문제들 (Step 순으로 정렬됨)
    private Dictionary<string, List<DeductionStep>> deductions;

    // 지금 진행 중인 추리
    private List<DeductionStep> currentSteps;
    private int currentStepIndex;
    private System.Action onSuccessCallback;

    // 추리 파트가 진행 중인지. DialogueSystem이 이 값을 보고 대사 넘기기를 막는다.
    public bool IsActive { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        LoadDeductions();
    }

    // ---------------------------------------------------------------------------------
    // CSV 로딩
    // ---------------------------------------------------------------------------------
    private void LoadDeductions()
    {
        deductions = new Dictionary<string, List<DeductionStep>>();

        var rows = CSVReader.Read(DeductionCsv);
        if (rows == null || rows.Count == 0)
        {
            Debug.LogWarning($"[DeductionController] {DeductionCsv}.csv를 읽지 못했습니다. 추리 파트가 동작하지 않습니다.");
            return;
        }

        foreach (var row in rows)
        {
            string id = GetField(row, "DeductionId").Trim();
            if (string.IsNullOrEmpty(id)) continue;

            if (!int.TryParse(GetField(row, "Step").Trim(), out int stepNo)) stepNo = 1;

            if (!deductions.TryGetValue(id, out var steps))
            {
                steps = new List<DeductionStep>();
                deductions[id] = steps;
            }

            // 같은 Step 번호의 문제를 찾거나 새로 만든다.
            DeductionStep step = steps.Find(s => s.step == stepNo);
            if (step == null)
            {
                step = new DeductionStep { step = stepNo };
                steps.Add(step);
            }

            // Question은 같은 Step의 첫 줄에만 적으면 되므로, 비어 있지 않을 때만 덮어쓴다.
            string q = GetField(row, "Question").Trim();
            if (!string.IsNullOrEmpty(q)) step.question = q;

            string choiceText = GetField(row, "ChoiceText").Trim();
            if (string.IsNullOrEmpty(choiceText)) continue; // 문제 문장만 적은 줄

            // 오답일 때 갈 엔딩. 비워두면 Bad_D(포기/일상 복귀 엔딩)로 간다.
            string endingStr = GetField(row, "WrongEnding").Trim();
            if (!System.Enum.TryParse(endingStr, out EndingType wrongEnding) || wrongEnding == EndingType.None)
            {
                wrongEnding = EndingType.Bad_D;
            }

            step.choices.Add(new DeductionChoice
            {
                text = choiceText,
                isCorrect = GetField(row, "IsCorrect").Trim().ToLower() == "true",
                resultText = GetField(row, "ResultText"),
                wrongEnding = wrongEnding
            });
        }

        // 문제를 Step 번호 순으로 정렬해둔다 (CSV에 순서가 뒤섞여 있어도 안전하게).
        foreach (var pair in deductions)
        {
            pair.Value.Sort((a, b) => a.step.CompareTo(b.step));
        }
    }

    private string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var v) ? v.ToString() : "";
    }

    // ---------------------------------------------------------------------------------
    // 진행
    // ---------------------------------------------------------------------------------

    // DialogueSystem이 LineType=Deduction 행을 만났을 때 호출한다.
    public void Enter(string deductionId, System.Action onSuccess)
    {
        if (deductions == null || !deductions.TryGetValue(deductionId, out var steps) || steps.Count == 0)
        {
            // 데이터가 없으면 추리를 건너뛰고 그냥 다음 대사로 넘어간다.
            // (게임이 멈춰버리는 것보다는 낫다 - InvestigationController.Enter와 같은 방침)
            Debug.LogWarning($"[DeductionController] '{deductionId}' 추리 데이터를 찾을 수 없어 건너뜁니다. " +
                             "DeductionData.csv의 DeductionId를 확인하세요.");
            onSuccess?.Invoke();
            return;
        }

        currentSteps = steps;
        currentStepIndex = 0;
        onSuccessCallback = onSuccess;
        IsActive = true;

        ShowCurrentStep();
    }

    // 지금 차례의 문제와 보기를 화면에 띄운다.
    private void ShowCurrentStep()
    {
        if (currentStepIndex >= currentSteps.Count)
        {
            // 문제를 전부 맞혔다 -> 추리 성공
            Finish(true);
            return;
        }

        DeductionStep step = currentSteps[currentStepIndex];

        // 문제 문장 표시. 전용 텍스트가 없으면 대사창을 빌려 쓴다.
        if (questionText != null)
        {
            questionText.text = step.question;
        }
        else if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.ShowInvestigationLine("", step.question);
        }

        GameObject panel = choicePanel != null ? choicePanel : GetDialoguePanel();
        Transform container = choiceContainer != null ? choiceContainer : GetDialogueContainer();
        GameObject prefab = choiceButtonPrefab != null ? choiceButtonPrefab : GetDialoguePrefab();

        if (panel == null || container == null || prefab == null)
        {
            Debug.LogError("[DeductionController] 선택지 UI를 찾을 수 없습니다. " +
                           "인스펙터에서 choicePanel/choiceContainer/choiceButtonPrefab을 연결하거나, " +
                           "DialogueSystem 쪽 선택지 UI가 제대로 연결되어 있는지 확인하세요.");
            Finish(true); // 진행을 막지 않기 위해 성공 처리하고 빠져나간다
            return;
        }

        panel.SetActive(true);

        // 이전 문제의 버튼을 지운다.
        foreach (Transform child in container) Destroy(child.gameObject);

        // 보기 버튼 생성
        foreach (var choice in step.choices)
        {
            GameObject btnObj = Instantiate(prefab, container);
            var label = btnObj.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choice.text;

            // 람다 안에서 반복 변수를 그대로 쓰면 마지막 값만 잡히는 문제가 있으므로
            // 지역 변수로 복사해둔다 (DialogueSystem.ShowChoices와 같은 이유).
            DeductionChoice captured = choice;
            var button = btnObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnChoiceSelected(captured));
            }
        }
    }

    // 보기 하나를 골랐을 때.
    private void OnChoiceSelected(DeductionChoice choice)
    {
        GameObject panel = choicePanel != null ? choicePanel : GetDialoguePanel();
        if (panel != null) panel.SetActive(false);

        // 고른 보기에 대한 반응 문장을 대사창에 보여준다.
        if (!string.IsNullOrWhiteSpace(choice.resultText) && DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.ShowInvestigationLine("재훈", choice.resultText);
        }

        if (!choice.isCorrect)
        {
            // ===== 틀렸다 -> 바로 배드엔딩 =====
            // 시나리오 기획상 추리 실패는 곧바로 엔딩으로 직행한다(다시 풀 기회 없음).
            Debug.Log($"[DeductionController] 추리 실패 -> {choice.wrongEnding} 엔딩으로 진입합니다.");

            IsActive = false;
            currentSteps = null;
            onSuccessCallback = null;

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.TriggerEnding(choice.wrongEnding);
            }
            return;
        }

        // 맞았다 -> 다음 문제로
        currentStepIndex++;

        // 반응 문장을 잠깐 읽을 시간을 준 뒤 다음 문제를 띄운다.
        // (바로 다음 문제가 뜨면 방금 맞았다는 반응을 읽을 새가 없다)
        StartCoroutine(ShowNextStepAfterDelay());
    }

    private System.Collections.IEnumerator ShowNextStepAfterDelay()
    {
        yield return new WaitForSeconds(1.2f);
        ShowCurrentStep();
    }

    // 추리 묶음이 끝났을 때.
    private void Finish(bool success)
    {
        IsActive = false;
        currentSteps = null;

        GameObject panel = choicePanel != null ? choicePanel : GetDialoguePanel();
        if (panel != null) panel.SetActive(false);

        // 콜백을 지역 변수로 옮긴 뒤 비우고 호출한다. 콜백 안에서 또 Enter()가 불릴 수
        // 있으므로(다음 줄이 또 추리인 경우) 이전 콜백이 남아있으면 안 된다.
        var callback = onSuccessCallback;
        onSuccessCallback = null;

        if (success) callback?.Invoke();
    }

    // ---------------------------------------------------------------------------------
    // DialogueSystem의 선택지 UI 빌려 쓰기
    // ---------------------------------------------------------------------------------
    // 인스펙터에서 따로 연결하지 않으면 대사 선택지와 같은 UI를 그대로 쓴다.
    // 추리도 결국 "버튼 몇 개 중 하나 고르기"라 UI를 새로 만들 이유가 없다.
    private GameObject GetDialoguePanel() => DialogueSystem.Instance != null ? DialogueSystem.Instance.choicePanel : null;
    private Transform GetDialogueContainer() => DialogueSystem.Instance != null ? DialogueSystem.Instance.choiceContainer : null;
    private GameObject GetDialoguePrefab() => DialogueSystem.Instance != null ? DialogueSystem.Instance.choiceButtonPrefab : null;
}
