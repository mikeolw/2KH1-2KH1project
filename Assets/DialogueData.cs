using System.Collections.Generic;
using UnityEngine;

// 1. 선택지 클래스 (여기 선언되어 있어야 다른 스크립트에서 Choice를 인식할 수 있습니다!)
[System.Serializable]
public class Choice
{
    public string choiceText;            // 버튼 문구
    public DialogueData nextDialogue;    // 연결될 다음 대본

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