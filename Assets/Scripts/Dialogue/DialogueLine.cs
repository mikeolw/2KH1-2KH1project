using System.Collections.Generic;
using UnityEngine;

// 대사 연출 유형 (일반 대사, 지문/나레이션, 연출 전용 등)
public enum LineType
{
    NormalDialogue,   // 캐릭터 대사
    Narration,        // 주인공 독백/지문 (화자 이름 숨김)
    EventTrigger      // 화면 연출만 발생하고 넘어가기
}

[System.Serializable]
public class DialogueLine
{
    [Header("기본 대사 정보")]
    public LineType lineType = LineType.NormalDialogue;
    public string speaker;             // 화자 이름 (예: "재훈", "한성")
    [TextArea(2, 5)]
    public string sentence;            // 대사 내용

    [Header("화면 연출 (선택)")]
    public Sprite backgroundImage;     // (구버전) 인스펙터로 직접 꽂던 배경. 지금은 아래 backgroundName을 쓴다.
    public bool isFadeOut;             // 체크 시 화면 암전(Fade) 연출
    // 시나리오 문서의 {세이브포인트}에 해당하는 줄인지. CSV의 IsSavePoint 칸이 TRUE면 켜진다.
    // 이 줄을 지나가야만 플레이어가 "저장하기"를 쓸 수 있다 (SavePointManager.cs 참고).
    public bool isSavePoint;

    // 세이브포인트를 사람이 알아볼 수 있게 붙이는 이름. CSV의 SavePointId 칸.
    // 세이브 슬롯 목록에 "#01 사무실"처럼 표시된다. 비워두면 CSV 파일 이름이 대신 쓰인다.
    public string savePointId;

    [Header("배경 / 캐릭터 스탠딩 (StageController.cs가 처리)")]
    // ===== 왜 Sprite가 아니라 "파일 이름 문자열"인가? =====
    // 그림은 Assets/Resources/Illusts/ 아래에 있는데 이 폴더는 git으로 공유되지 않는다
    // (구글 드라이브로 따로 주고받음). 인스펙터로 Sprite를 꽂아두면 팀원마다 .meta의 GUID가
    // 달라져서 연결이 깨지므로, BGM/SFX와 똑같이 "파일 이름"으로 불러온다.
    // 자세한 이유는 IllustLoader.cs 상단 주석 참고.
    //
    // 아래 4개는 CSV의 Background / Standing / StandingPos / Talker 컬럼에서 그대로 들어온다.
    // 값이 비어 있으면 "이전 줄의 상태를 그대로 유지"한다는 뜻이라, 장면이나 표정이 바뀌는
    // 줄에만 적어주면 된다 (매 줄마다 채울 필요 없음).

    // 배경 파일 이름 (예: BG_01_Office). 빈 값 = 유지, "none" = 배경 지움.
    public string backgroundName;

    // 캐릭터 스탠딩 파일 이름. 두 명 이상은 세로줄(|)로 구분한다.
    // 예: STD_Past01_Hansung_Default|STD_Past01_Jaehoon_Default
    // 빈 값 = 유지(표정 안 바뀜), "none" = 전원 퇴장.
    public string standingNames;

    // 각 스탠딩이 설 자리. L=왼쪽 C=가운데 R=오른쪽, 세로줄(|)로 구분 (예: L|R).
    // 빈 값이면 인원수에 맞춰 자동 배치한다.
    public string standingPositions;

    // 지금 말하는 캐릭터의 자리(L/C/R). 대사가 타이핑되는 동안 그 캐릭터만 입을 뻐끔거린다.
    // 빈 값이면 Speaker 이름으로 자동 인식한다 (Dialogues/Characters.csv의 매핑 사용).
    // "none"이면 아무도 입을 움직이지 않는다 (나레이션 등).
    public string talkerSlot;

    [Header("사운드 연출 (선택)")]
    public AudioClip bgmToPlay;        // 재생할 BGM (예: 빗소리, 천둥소리)[cite: 2]
    public AudioClip sfxToPlay;        // 재생할 효과음 (예: 문 여는 소리, 타격음)[cite: 2]

    [Header("아이템/단서 획득 (선택)")]
    public string acquireItemName;     // 이 대사 출력 시 획득할 아이템 (예: "디지털 카메라 SD카드")[cite: 2]

    [Header("미니게임 (선택)")]
    // CSV의 LineType이 "Minigame"인 행에서만 채워진다. isMinigame이 true면
    // DialogueSystem.ShowNextSentence()가 대사를 표시하는 대신 MinigameController를 띄운다.
    //
    // 지금은 실제 미니게임 기획(조작 방식, 난이도, 아트)이 하나도 정해지지 않은 상태라,
    // "버튼 하나 누르면 무조건 성공"하는 완전한 스텁으로만 구현되어 있다(MinigameController.cs
    // 참고). 그래서 여기 필드도 실제로 게임에 필요한 최소한(라벨 문구 + 실패 시 엔딩)만 남겨뒀다.
    //
    // ===== 나중에 진짜 미니게임을 만들 때 확장하는 방법 =====
    // 예를 들어 "제한시간 안에 정답 상자 클릭" 같은 실제 조작이 정해지면:
    //   1) 여기에 필요한 파라미터 필드를 추가한다 (예: minigameTimeLimit, minigameBoxCount,
    //      minigameCorrectIndex 등 - 조작 방식에 맞는 것으로).
    //   2) DialogueSystem.LoadDialogueFromCSV()의 "Minigame" 파싱 부분에서 CSV 컬럼을
    //      읽어와 그 필드를 채우는 코드를 추가한다 (MinigameLabel/TargetEnding을 읽는
    //      코드가 이미 있으니 그 옆에 나란히 추가하면 된다).
    //   3) MinigameController.StartMinigame()의 시그니처에 필요한 파라미터를 추가하고,
    //      내부 구현(지금의 "버튼 하나" 대신 실제 UI/로직)을 새로 짠다.
    //   4) DialogueSystem.ShowNextSentence()가 StartMinigame()을 호출하는 부분은 인자만
    //      늘어날 뿐 구조는 그대로 - onSuccessCallback/onFailCallback으로 성공/실패를
    //      알려주는 흐름(대사 계속 진행 vs GameFlowManager.TriggerEnding)은 이미 완성되어
    //      있으므로 손댈 필요 없다.
    // 즉, 이 필드들과 성공/실패 콜백 배관은 미니게임의 "겉모습"이 무엇으로 바뀌든 그대로 쓸 수
    // 있게 설계되어 있고, 바뀌는 건 MinigameController.cs의 내부 구현뿐이다.
    public bool isMinigame;
    public string minigameLabel;       // 미니게임 안내 문구 (CSV의 MinigameLabel 컬럼)
    public EndingType minigameFailEnding; // 실패 시 연결할 엔딩 (CSV의 TargetEnding 컬럼 재사용,
                                           // 지금 스텁은 항상 성공하므로 실제로 쓰이진 않지만
                                           // 나중에 진짜 실패 조건이 생기면 바로 쓸 수 있게 남겨둠)

    [Header("조사 모드 (선택)")]
    // CSV의 LineType이 "Investigate"인 행에서만 채워진다. isInvestigation이 true면
    // DialogueSystem.ShowNextSentence()가 대사를 표시하는 대신 InvestigationController를
    // 띄운다 - 위의 미니게임(isMinigame)과 완전히 동일한 배관 구조이고, 자리만
    // InvestigationController로 바뀐 것뿐이다.
    //
    // investigationId는 씬에 미리 배치해둔 조사 화면(InvestigationController.screens 목록의
    // InvestigationScreen.id)과 매칭된다. 지금은 조사 가능한 위치(책상/핸드폰/창문/회사 동료
    // 등) 자체를 CSV가 아니라 씬에 직접 배치한 Placeholder 오브젝트(InvestigatableObject.cs)로
    // 관리한다. 나중에 조사 위치 자체를 CSV로 데이터화해야 한다면, 미니게임 확장 방법과
    // 동일하게 여기에 필드를 추가하고 DialogueSystem.LoadDialogueFromCSV()의 "Investigate"
    // 파싱부에서 채워주면 된다.
    public bool isInvestigation;
    public string investigationId;

    [Header("추리 파트 (선택)")]
    // CSV의 LineType이 "Deduction"인 행에서만 채워진다. 미니게임/조사와 완전히 같은
    // 배관 구조이고, 자리만 DeductionController로 바뀐 것뿐이다.
    //
    // deductionId는 Resources/Dialogues/DeductionData.csv의 DeductionId 칸과 매칭된다.
    // 문제를 전부 맞히면 다음 대사로 이어지고, 하나라도 틀리면 그 보기에 지정된 엔딩
    // (기본값 Bad_D)으로 바로 넘어간다. 자세한 내용은 DeductionController.cs 참고.
    public bool isDeduction;
    public string deductionId;
}