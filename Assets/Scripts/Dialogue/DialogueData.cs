using System.Collections.Generic;
using UnityEngine;

// 1. 선택지 클래스 (여기 선언되어 있어야 다른 스크립트에서 Choice를 인식할 수 있습니다!)
//
// 다음 대본을 가리키는 방법이 두 가지다:
//   - nextDialogue: DialogueData 에셋을 인스펙터에서 직접 손으로 연결하는 방식 (기존 방식)
//   - nextScenarioCsv: CSV 파일 이름(확장자 제외)만 적어두면 DialogueSystem이
//     Resources/Dialogues/에서 알아서 불러오는 방식 (CSV로 선택지를 만들 때 씀,
//     DialogueSystem.LoadDialogueFromCSV()가 채워줌)
// 하나의 선택지에는 둘 중 하나만 채우면 된다. 실제로 어느 쪽을 쓸지는
// DialogueSystem.ShowChoices()의 버튼 클릭 콜백에서 nextDialogue를 먼저 확인하고,
// 없으면 nextScenarioCsv를 확인하는 순서로 처리한다.
[System.Serializable]
public class Choice
{
    public string choiceText;            // 버튼 문구
    public DialogueData nextDialogue;    // 연결될 다음 대본 (에셋을 직접 손으로 연결할 때)
    public string nextScenarioCsv;       // 연결될 다음 대본 (CSV 파일명, 확장자 제외)

    [Header("엔딩 분기 설정")]
    public bool isEndingChoice;          // 체크하면 엔딩 직행
    public EndingType targetEnding;      // 연결할 엔딩 종류
}

// 2. 대본 데이터 클래스
[CreateAssetMenu(fileName = "NewDialogue", menuName = "VisualNovel/DialogueData")]
public class DialogueData : ScriptableObject
{
    [Header("대사 리스트 (클래스 구조)")]
    public List<DialogueLine> lines;     // 대사+연출 클래스 리스트

    [Header("대사 종료 후 선택지")]
    public List<Choice> choices;         // 선택지 리스트
}