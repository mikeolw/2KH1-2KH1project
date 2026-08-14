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
        // 게임 시작 시 scenario_sample.csv 파일 자동 로드 테스트
        LoadDialogueFromCSV("scenario_sample");
    }

    //void Start()
    //{
    //    if (currentDialogue != null)
    //    {
    //        StartDialogue(currentDialogue);
    //    }
    //}

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
    //CSV 데이터를 DialogueData로 변환해 불러오기
    public void LoadDialogueFromCSV(string csvFileName)
    {
        // Resources/Dialogues/ 폴더 내의 CSV 파일 읽기
        List<Dictionary<string, object>> data = CSVReader.Read("Dialogues/" + csvFileName);

        currentDialogue = ScriptableObject.CreateInstance<DialogueData>();
        currentDialogue.lines = new List<DialogueLine>();

        for (var i = 0; i < data.Count; i++)
        {
            DialogueLine line = new DialogueLine();

            // 엑셀 칼럼 값 매핑
            line.speaker = data[i]["Speaker"].ToString();
            line.sentence = data[i]["Sentence"].ToString();
            line.isFadeOut = data[i]["IsFadeOut"].ToString().ToLower() == "true";
            line.acquireItemName = data[i]["Item"].ToString();

            // 사운드 파일명이 적혀있다면 Resources 폴더에서 오디오 불러오기
            string sfxName = data[i]["SFX"].ToString();
            if (!string.IsNullOrEmpty(sfxName))
            {
                line.sfxToPlay = Resources.Load<AudioClip>("Sounds/" + sfxName);
            }

            currentDialogue.lines.Add(line);
        }

        // 대사 시작
        StartDialogue(currentDialogue);
    }
}

