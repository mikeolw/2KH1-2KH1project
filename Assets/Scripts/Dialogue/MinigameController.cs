using System;
using System.Collections;
using System.Text.RegularExpressions;
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
//   MinigameLabel : 미션 카드에 크게 보여줄 목표 문장 (예: "자료실을 조사하라")
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
//
// ===== 미션 카드 모양 (코드로 입힌다 - 씬의 MinigamePanel은 지우지 않고 모양만 바꾼다) =====
//   ┌───────────────────────────────────────────┐  ← 위쪽 강조선
//   │                  MISSION                   │
//   │                   ────                     │
//   │     자료실 열쇠를 찾아 몰래 조사를 시작하라     │
//   │              제한 시간   05:00              │  ← 타임어택일 때만
//   │               [    시작    ]                │
//   └───────────────────────────────────────────┘
// 카드가 떠 있는 동안에는 타임어택 시계를 멈춘다 (지시를 읽는 시간까지 깎이면 억울하다).
public class MinigameController : MonoBehaviour
{
    public static MinigameController Instance;

    [Header("UI 연결")]
    public GameObject panel;
    public TMP_Text labelText;

    // 예전에는 ChoiceButton.prefab을 복제해 [진행하기] 버튼을 만들었다. 지금은 버튼을
    // 미니게임 공통 모양(MinigameTheme.MakeButton)으로 만들어서 이 두 칸은 쓰지 않는다.
    // 씬에 연결이 남아 있어 지우지 않고 둔다.
    public GameObject confirmButtonPrefab; // ChoiceButton.prefab
    public Transform buttonContainer;      // panel의 VerticalLayoutGroup 밑 (보통 panel 자신)

    public bool IsActive => panel != null && panel.activeSelf;

    private const float CardWidth = 760f;
    private const float CardHeight = 330f;
    private const float CardHeightNoTimer = 290f;

    private bool resolved;
    private Action onSuccess;
    private Action onFail;

    private TMP_Text timeLimitText;
    private GameObject dim;
    private CanvasGroup cardGroup;
    private Coroutine fadeRoutine;
    private bool pausedTimer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (panel == null) return;
        BuildCard();
        panel.SetActive(false);
    }

    private void BuildCard()
    {
        // 씬에 붙은 세로 정렬/크기 맞춤을 끄고 자리를 직접 잡는다.
        var layout = panel.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        var fitter = panel.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        var rt = (RectTransform)panel.transform;
        MinigameTheme.Center(rt, new Vector2(CardWidth, CardHeight), Vector2.zero);
        MinigameTheme.StyleCard(rt);
        cardGroup = panel.GetComponent<CanvasGroup>();
        if (cardGroup == null) cardGroup = panel.AddComponent<CanvasGroup>();

        var header = MinigameTheme.AddText(rt, "Header", 16, MinigameTheme.Accent);
        PlaceFromTop(header.rectTransform, 30f, 24f);
        header.fontStyle = FontStyles.Bold;
        header.characterSpacing = 24f;
        header.text = "MISSION";

        var rule = MinigameTheme.AddImage(rt, "HeaderRule", MinigameTheme.Accent);
        var rrt = rule.rectTransform;
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 1f);
        rrt.sizeDelta = new Vector2(40f, 2f);
        rrt.anchoredPosition = new Vector2(0f, -64f);

        if (labelText != null)
        {
            labelText.transform.SetParent(rt, false);
            var lle = labelText.GetComponent<LayoutElement>();
            if (lle != null) lle.ignoreLayout = true;
            PlaceFromTop(labelText.rectTransform, 86f, 110f, 48f);
            labelText.enableAutoSizing = true;      // 긴 목표 문장은 줄을 바꾸고 글자를 줄여 카드 안에 넣는다
            labelText.fontSizeMin = 22;
            labelText.fontSizeMax = 30;
            labelText.fontSize = 30;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.textWrappingMode = TextWrappingModes.Normal;
            labelText.overflowMode = TextOverflowModes.Truncate;
            labelText.color = MinigameTheme.TextBright;
            labelText.raycastTarget = false;
            UIFontHelper.Apply(labelText);
        }

        timeLimitText = MinigameTheme.AddText(rt, "TimeLimit", 20, MinigameTheme.TextDim);
        PlaceFromTop(timeLimitText.rectTransform, 202f, 30f);
        timeLimitText.richText = true;

        var button = MinigameTheme.MakeButton(rt, "Btn_Start", "시작", new Vector2(220f, 56f), Vector2.zero, Success);
        var brt = (RectTransform)button.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 30f);

        // 화면 전체를 어둡게 하는 막. 카드의 형제로 두고, 띄울 때 카드 바로 뒤에 놓는다.
        if (panel.transform.parent != null)
        {
            var dimImg = MinigameTheme.AddImage(panel.transform.parent, "MinigameDim", new Color(0f, 0f, 0f, 0.7f));
            dimImg.raycastTarget = true;   // 카드가 떠 있는 동안 뒤의 화면이 눌리지 않게
            MinigameTheme.Stretch(dimImg.rectTransform);
            dim = dimImg.gameObject;
            dim.SetActive(false);
            ModalStack.MarkNotWindow(dim, "미니게임 카드 뒤의 막. 게임 단계라 창이 아니다 (대사 막기는 IsActive).");
        }
    }

    // 카드 위쪽에서 top만큼 내려온 자리에, 좌우 inset만 남기고 가로로 꽉 차게 놓는다.
    private static void PlaceFromTop(RectTransform rt, float top, float height, float inset = 0f)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-inset * 2f, height);
        rt.anchoredPosition = new Vector2(0f, -top);
    }

    // label만 받는다 - 실제 미니게임 파라미터(제한시간, 난이도 등)가 정해지면 여기에
    // 인자를 추가하면 된다. 호출부(DialogueSystem.ShowNextSentence())도 같이 늘어난다.
    public void StartMinigame(string label, Action onSuccessCallback, Action onFailCallback)
    {
        // 대본에 "(타임어택)"처럼 붙여 둔 꼬리표는 카드의 "제한 시간" 줄이 대신 알려주므로 뗀다.
        labelText.text = Regex.Replace(label ?? "", @"\s*\(타임\s*어택\)\s*$", "");
        resolved = false;
        onSuccess = onSuccessCallback;
        onFail = onFailCallback;

        // 타임어택 시계는 DialogueSystem이 이 카드를 띄우기 직전에 켠다.
        var timer = TimeAttackController.Instance;
        bool timed = timer != null && timer.IsRunning;
        if (timeLimitText != null)
        {
            timeLimitText.gameObject.SetActive(timed);
            if (timed)
            {
                int total = Mathf.CeilToInt(timer.RemainingSeconds);
                timeLimitText.text = $"제한 시간   <color=#E4EAEF><b>{total / 60:00}:{total % 60:00}</b></color>";
            }
        }
        ((RectTransform)panel.transform).sizeDelta = new Vector2(CardWidth, timed ? CardHeight : CardHeightNoTimer);

        pausedTimer = timed;
        if (timed) timer.Paused = true;

        // 막을 먼저 맨 앞으로 보낸 뒤 카드를 그 앞에 둔다 (막 < 카드).
        if (dim != null)
        {
            dim.SetActive(true);
            dim.transform.SetAsLastSibling();
        }
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn());
    }

    // 살짝 아래에서 떠오르며 나타난다.
    private IEnumerator FadeIn()
    {
        const float duration = 0.25f;
        var rt = (RectTransform)panel.transform;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / duration;
            if (cardGroup != null) cardGroup.alpha = 1f - k;
            rt.anchoredPosition = new Vector2(0f, -16f * k * k);
            yield return null;
        }
        if (cardGroup != null) cardGroup.alpha = 1f;
        rt.anchoredPosition = Vector2.zero;
        fadeRoutine = null;
    }

    // Enter로도 시작할 수 있다.
    private void Update()
    {
        if (!IsActive || resolved) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Success();
    }

    // 카드를 치우고, 멈춰 둔 타임어택 시계를 다시 돌린다.
    private void HideCard()
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        panel.SetActive(false);
        if (dim != null) dim.SetActive(false);
        if (pausedTimer && TimeAttackController.Instance != null) TimeAttackController.Instance.Paused = false;
        pausedTimer = false;
    }

    private void Success()
    {
        if (resolved) return;
        resolved = true;
        HideCard();
        onSuccess?.Invoke();
    }

    // 지금은 아무 데서도 호출되지 않는다(스텁이 항상 성공하므로). 실제 실패 조건이
    // 생기면 그 지점에서 이 함수를 호출하면 된다 - onFail 콜백 실행 로직은 이미 완성돼있다.
    private void Fail()
    {
        if (resolved) return;
        resolved = true;
        HideCard();
        onFail?.Invoke();
    }

    // ===== 엔딩이 미니게임 도중에 갑자기 끼어들 때 (예: 미니게임 2 타임어택 시간 초과) =====
    // Success()/Fail()과 달리 onSuccess/onFail 콜백을 부르지 않는다. 이미 다른 경로
    // (TimeAttackController -> GameFlowManager.TriggerEnding)로 엔딩이 확정된 상황이라,
    // 여기서 콜백을 또 부르면 "미니게임도 끝났다"며 대사가 한 번 더 진행되어 버린다.
    // 그냥 패널만 치우고 이 라운드를 무효화한다. GameFlowManager.TriggerEnding()이 부른다.
    public void ForceExit()
    {
        if (resolved) return;
        resolved = true;
        if (panel != null) HideCard();
        onSuccess = null;
        onFail = null;
    }
}
