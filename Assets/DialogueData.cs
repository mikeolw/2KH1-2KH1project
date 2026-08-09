using System.Collections.Generic;
using UnityEngine;

// 1. 선택지 구조체
[System.Serializable]
public class Choice
{
    public string choiceText;            // 버튼 문구 (예: "과장에게 복수한다")
    public DialogueData nextDialogue;    // 다음으로 연결될 대본 (일반 진행)

    [Header("엔딩 분기 설정")]
    public bool isEndingChoice;          // 체크하면 엔딩으로 직행
    public EndingType targetEnding;      // 연결할 엔딩 종류 (Bad_A, Bad_B 등)
}

// 2. 대본 파일 생성 스크립트
[CreateAssetMenu(fileName = "NewDialogue", menuName = "VisualNovel/DialogueData")]
public class DialogueData : ScriptableObject
{
    [System.Serializable]
    public struct Line
    {
        public string speaker;           // 말하는 사람 (예: "유한성")
        [TextArea(2, 5)]
        public string sentence;          // 대사 내용
    }

    public List<Line> lines;             // 대사 리스트
    public List<Choice> choices;         // 대사 종료 후 나올 선택지 리스트
}