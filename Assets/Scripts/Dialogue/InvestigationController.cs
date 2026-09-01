using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 조사 모드 컨트롤러 (MinigameController.cs와 완전히 동일한 배관 패턴)
// =====================================================================================
// CSV(LineType=Investigate) -> DialogueSystem -> 여기 Enter() -> (Exit 시) onExit 콜백
// -> DialogueSystem.ShowNextSentence()로 이어지는 구조가 미니게임과 똑같다. 다른 점은
// "성공/실패"가 아니라 "조사 화면 안에서 여러 오브젝트를 자유롭게 눌러보고 스스로 나간다"는
// 점 뿐이다.
//
// ===== 조사 화면(Screen) 등록 방법 =====
// 이벤트마다 씬에 미리 만들어둔 조사 화면 Panel을 investigationId 문자열과 함께
// screens 리스트에 등록해두면 된다 (Inspector에서 직접 드래그). CSV의 InvestigationId
// 컬럼 값과 여기 id가 정확히 일치해야 한다.
//
// InvestigationScreen_Office 같은 Panel 자체나 그 안의 핫스팟 배치(격자로 대충 흩어놓은
// 위치/크기)는 정식 배경 아트가 나오기 전까지의 placeholder일 뿐이다 - 나중에 그림이
// 정해지면 좌표만 옮기면 되고, 여기 screens 리스트의 등록 방식(id <-> panel 매칭)이나
// InvestigatableObject.cs 쪽 배관은 그대로 재사용된다 (InvestigatableObject.cs 상단
// 주석에 같은 내용 더 자세히 적어둠). 즉 "위치가 아트에 따라 크게 바뀔 텐데 지금 미리
// 짜두는 게 낭비 아니냐"는 질문에는: 바뀌는 건 좌표뿐이고 이 등록 구조는 그대로 남는다는
// 게 답이다.
//
// ===== Talk 타입(회사 동료 등) 오버레이 처리 =====
// InvestigatableObject.type이 Talk이면 모달 대신 기존 대사창(DialogueSystem의
// speakerText/sentenceText)을 그대로 빌려서 대사 한 줄을 보여준다. 이때 조사 화면 Panel은
// 잠깐 꺼두고(IsShowingTalkLine = true), 실제 CSV 진행(lineIndex)은 전혀 건드리지 않는다.
// DialogueSystem.Update()가 IsShowingTalkLine을 보고 스페이스/클릭을 "다음 대사로 진행"이
// 아니라 DismissTalkLine() 호출로 돌린다 (DialogueSystem.cs의 Update() 주석 참고).
public class InvestigationController : MonoBehaviour
{
    public static InvestigationController Instance;

    [Serializable]
    public class InvestigationScreen
    {
        public string id;          // CSV의 InvestigationId 컬럼과 매칭되는 식별자
        public GameObject panel;   // 씬에 미리 배치해둔 조사 화면 Panel
    }

    [Header("조사 화면 목록 (이벤트별로 하나씩 등록)")]
    public List<InvestigationScreen> screens = new List<InvestigationScreen>();

    // 조사 화면은 대사창(DialoguePanel) 뒤에 깔린 배경이 아니라, 대사창과 자리를 공유하는
    // 별도 화면이다. 그래서 조사 모드에 들어가는 동안은 대사창을 꺼서 조사 화면만 보이게 하고,
    // Talk 타입 오버레이(Inspect()의 HotspotType.Talk 분기 참고)일 때만 잠깐 다시 켠다.
    [Header("대사창 (조사 중엔 꺼둔다)")]
    public GameObject dialoguePanel;

    // 지금 열려 있는 조사 화면의 Panel. Talk 오버레이 중에는 잠깐 꺼두기 위해 따로 들고 있는다.
    private GameObject activeScreenPanel;

    // Exit()에서 호출할 콜백. DialogueSystem.ShowNextSentence()가 Enter()를 호출할 때
    // "() => ShowNextSentence()"를 넘겨준다 - 즉 조사가 끝나면 CSV의 다음 줄로 이어간다.
    private Action onExitCallback;

    // "지금 조사 세션 안에 있는가"를 나타낸다. Talk 오버레이 때문에 activeScreenPanel이
    // 잠깐 꺼져 있어도(SetActive(false)) inSession은 true로 유지된다 - DialogueSystem이
    // "조사 모드가 통째로 끝났는지"를 판단하는 기준은 화면이 보이는지가 아니라 이 값이기 때문.
    private bool inSession;

    public bool IsActive => inSession;
    public bool IsShowingTalkLine { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // DialogueSystem.ShowNextSentence()가 LineType=Investigate 행을 만났을 때 호출한다.
    public void Enter(string investigationId, Action onExit)
    {
        InvestigationScreen screen = screens.Find(s => s.id == investigationId);
        if (screen == null || screen.panel == null)
        {
            // 씬에 screens 등록을 깜빡했거나 CSV에 오타가 있는 경우. 조사 화면을 못 띄우고
            // 대사가 멈춰버리는 것보다는, 경고를 남기고 바로 다음 대사로 넘어가는 편이 안전하다.
            Debug.LogWarning($"[InvestigationController] '{investigationId}'에 해당하는 조사 화면을 찾을 수 없습니다. screens 목록에 등록되어 있는지 확인하세요.");
            onExit?.Invoke();
            return;
        }

        inSession = true;
        IsShowingTalkLine = false;
        onExitCallback = onExit;
        activeScreenPanel = screen.panel;
        activeScreenPanel.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    // 조사 화면 안의 "조사 그만하기" 버튼 OnClick에 연결한다.
    public void Exit()
    {
        if (!inSession) return;

        if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
        activeScreenPanel = null;
        inSession = false;
        IsShowingTalkLine = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        // 콜백을 지역변수로 빼서 먼저 null로 지운 뒤 호출한다 - 콜백(ShowNextSentence) 안에서
        // 다시 Enter()가 호출되는 경우(다음 대사가 또 조사 행인 경우)에도 이전 콜백이 남아있지
        // 않도록 하기 위함.
        Action callback = onExitCallback;
        onExitCallback = null;
        callback?.Invoke();
    }

    // InvestigatableObject.OnClickInspect()가 호출한다. 타입별로 반응이 갈린다.
    public void Inspect(InvestigatableObject obj)
    {
        switch (obj.type)
        {
            case HotspotType.Item:
                ItemModalController.Instance.Show(obj.modalImage, obj.objectName, obj.description);
                InventoryManager.Instance.AddItem(obj.itemId);
                break;

            case HotspotType.Description:
                ItemModalController.Instance.Show(obj.modalImage, obj.objectName, obj.description);
                break;

            case HotspotType.Talk:
                if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
                IsShowingTalkLine = true;
                if (dialoguePanel != null) dialoguePanel.SetActive(true);
                DialogueSystem.Instance.ShowInvestigationLine(obj.talkSpeaker, obj.talkSentence);
                break;
        }
    }

    // DialogueSystem.Update()가 Talk 오버레이 중 스페이스/클릭을 감지했을 때 호출한다.
    public void DismissTalkLine()
    {
        if (!IsShowingTalkLine) return;

        IsShowingTalkLine = false;
        if (activeScreenPanel != null) activeScreenPanel.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }
}
