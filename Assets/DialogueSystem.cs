using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        // 지문/나레이션(LineType.Narration)은 화자 이름을 숨긴다 (DialogueLine.cs의 lineType 주석 참고).
        speakerText.text = line.lineType == LineType.Narration ? "" : line.speaker;
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
            string nextCsv = choice.nextScenarioCsv;
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
                    // 손으로 연결해둔 DialogueData 에셋으로 분기
                    StartDialogue(next);
                }
                else if (!string.IsNullOrEmpty(nextCsv))
                {
                    // CSV로 만든 선택지: 다음 CSV 파일을 이어서 불러온다
                    LoadDialogueFromCSV(nextCsv);
                }
            });
        }
    }

    void Update()
    {
        // 선택지 패널이나 UIManager 팝업(조사기록/인벤토리/사진첩/핸드폰/설정)이 열려있을 땐
        // 스페이스바로도 대사가 넘어가면 안 된다.
        if (choicePanel.activeSelf) return;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyPanelOpen) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ShowNextSentence();
            return;
        }

        // 마우스 클릭은 EventSystem 기준으로 "지금 클릭한 지점 아래에 UI 요소가 없을 때만"
        // 대사를 넘긴다. 이 체크가 없으면 QuickBar 버튼(Note 등)을 눌러서 패널을 여는
        // 그 클릭이 동시에 "대사창 클릭"으로도 처리되어, 버튼 누를 때마다 대사도 같이
        // 넘어가는 버그가 생긴다.
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            ShowNextSentence();
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    //CSV 데이터를 DialogueData로 변환해 불러오기
    //
    // CSV 컬럼: LineType,Speaker,Sentence,BGM,SFX,IsFadeOut,Item,ChoiceText,NextScenario,IsEndingChoice,TargetEnding
    //
    // LineType이 "Choice"인 행은 일반 대사(DialogueLine)가 아니라 선택지(Choice)로 취급되어
    // currentDialogue.choices에 쌓인다. 그 외("Normal"/"Narration")는 기존처럼 lines에 쌓인다.
    // 지금 엔진 구조상 선택지는 항상 대사가 다 끝난 뒤 한 번에 보여주므로(ShowChoices() 참고),
    // Choice 행은 CSV 파일의 맨 끝에 몰아서 적어야 한다 (중간에 끼워넣으면 무시되지 않고 그냥
    // choices 리스트에 똑같이 쌓이긴 하지만, 화면에는 대사가 전부 끝난 뒤에야 나타난다).
    public void LoadDialogueFromCSV(string csvFileName)
    {
        // Resources/Dialogues/ 폴더 내의 CSV 파일 읽기
        List<Dictionary<string, object>> data = CSVReader.Read("Dialogues/" + csvFileName);

        currentDialogue = ScriptableObject.CreateInstance<DialogueData>();
        currentDialogue.lines = new List<DialogueLine>();
        currentDialogue.choices = new List<Choice>();

        for (var i = 0; i < data.Count; i++)
        {
            string lineTypeStr = GetField(data[i], "LineType");

            if (string.Equals(lineTypeStr, "Choice", StringComparison.OrdinalIgnoreCase))
            {
                var choice = new Choice();
                choice.choiceText = GetField(data[i], "ChoiceText");
                choice.isEndingChoice = GetField(data[i], "IsEndingChoice").ToLower() == "true";

                if (choice.isEndingChoice)
                {
                    // TargetEnding 칸에 EndingType 이름(예: Bad_D)을 그대로 적으면 된다.
                    string endingStr = GetField(data[i], "TargetEnding");
                    if (Enum.TryParse(endingStr, out EndingType parsedEnding))
                    {
                        choice.targetEnding = parsedEnding;
                    }
                    else
                    {
                        Debug.LogWarning($"[DialogueSystem] '{endingStr}'은 EndingType에 없는 값입니다. (CSV: {csvFileName}, 행: {i + 2})");
                    }
                }
                else
                {
                    choice.nextScenarioCsv = GetField(data[i], "NextScenario");
                }

                currentDialogue.choices.Add(choice);
                continue;
            }

            DialogueLine line = new DialogueLine();

            // 엑셀 칼럼 값 매핑
            line.lineType = string.Equals(lineTypeStr, "Narration", StringComparison.OrdinalIgnoreCase)
                ? LineType.Narration
                : LineType.NormalDialogue;
            line.speaker = GetField(data[i], "Speaker");
            line.sentence = GetField(data[i], "Sentence");
            line.isFadeOut = GetField(data[i], "IsFadeOut").ToLower() == "true";
            line.acquireItemName = GetField(data[i], "Item");

            // 사운드 파일명이 적혀있다면 Resources 폴더에서 오디오 불러오기
            // 효과음(SFX)은 배경음악(BGM)과 구분하기 쉽도록 Sounds/SFX/ 하위 폴더에 모아둔다.
            string sfxName = GetField(data[i], "SFX");
            if (!string.IsNullOrEmpty(sfxName))
            {
                line.sfxToPlay = Resources.Load<AudioClip>("Sounds/SFX/" + sfxName);
            }

            // BGM도 SFX와 동일한 방식으로 불러온다. 단, 실제로 이 클립을 재생하는 코드는 아직
            // 없다 (TODO: 대사 진행에 맞춰 BGM을 재생/전환하는 오디오 매니저가 필요함 - 지금은
            // Title 화면 전용 BgmPlayer.cs만 있고, 게임 씬 쪽 BGM 재생기는 없다).
            string bgmName = GetField(data[i], "BGM");
            if (!string.IsNullOrEmpty(bgmName))
            {
                line.bgmToPlay = Resources.Load<AudioClip>("Sounds/" + bgmName);
            }

            currentDialogue.lines.Add(line);
        }

        // 대사 시작
        StartDialogue(currentDialogue);
    }

    // CSVReader가 만든 행(Dictionary)에서 값을 안전하게 꺼낸다. 컬럼 자체가 없거나(예전 CSV처럼
    // ChoiceText 칼럼이 없는 파일) 비어있으면 빈 문자열을 반환해서 NullReferenceException을 막는다.
    private string GetField(Dictionary<string, object> row, string column)
    {
        return row.TryGetValue(column, out var value) ? value.ToString() : "";
    }
}
