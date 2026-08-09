using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueSystem : MonoBehaviour
{
    [Header("UI 연결")]
    public TMP_Text speakerText;
    public TMP_Text sentenceText;
    public GameObject choicePanel;
    public GameObject choiceButtonPrefab;
    public Transform choiceContainer;

    [Header("프로토타입 테스트용 대사 데이터")]
    public DialogueData currentDialogue;
    private int lineIndex = 0;

    void Start()
    {
        if (currentDialogue != null)
        {
            StartDialogue(currentDialogue);
        }
    }

    public void StartDialogue(DialogueData data)
    {
        currentDialogue = data;
        lineIndex = 0;
        choicePanel.SetActive(false);
        ShowNextSentence();
    }

    public void ShowNextSentence()
    {
        // 대사가 끝났을 때
        if (lineIndex >= currentDialogue.lines.Count)
        {
            ShowChoices();
            return;
        }

        var line = currentDialogue.lines[lineIndex];
        speakerText.text = line.speaker;
        sentenceText.text = line.sentence;
        lineIndex++;
    }

    private void ShowChoices()
    {
        if (currentDialogue.choices == null || currentDialogue.choices.Count == 0)
        {
            sentenceText.text = "[대사 세트 종료]";
            return;
        }

        choicePanel.SetActive(true);

        // 기존 선택지 버튼 제거
        foreach (Transform child in choiceContainer) Destroy(child.gameObject);

        // 선택지 동적 생성
        foreach (var choice in currentDialogue.choices)
        {
            GameObject btn = Instantiate(choiceButtonPrefab, choiceContainer);
            btn.GetComponentInChildren<TMP_Text>().text = choice.choiceText;

            DialogueData next = choice.nextDialogue;
            bool isEnding = choice.isEndingChoice;
            EndingType ending = choice.targetEnding;

            btn.GetComponent<Button>().onClick.AddListener(() => {
                choicePanel.SetActive(false);

                if (isEnding)
                {
                    // 엔딩 분기 처리
                    GameFlowManager.Instance.TriggerEnding(ending);
                }
                else if (next != null)
                {
                    // 다음 대사로 분기
                    StartDialogue(next);
                }
            });
        }
    }

    void Update()
    {
        // 스페이스바/마우스 클릭으로 대사 넘기기 (선택지나 팝업 UI가 안 떠있을 때만)
        if (!choicePanel.activeSelf && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            // 팝업 패널이 열려있지 않은 상태일 때만 넘어감
            ShowNextSentence();
        }
    }
}