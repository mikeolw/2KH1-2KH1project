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
}