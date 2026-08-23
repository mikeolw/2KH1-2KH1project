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
    public Sprite backgroundImage;     // 변경할 배경 이미지 (없으면 유지)
    public bool isFadeOut;             // 체크 시 화면 암전(Fade) 연출
    public bool isSavePoint;           // {세이브포인트} 여부[cite: 2]

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
}