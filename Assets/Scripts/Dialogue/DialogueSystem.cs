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

    // UIManager.cs/MinigameController.cs와 동일한 싱글톤 패턴. InvestigationController가
    // "Talk" 타입 조사 오브젝트를 처리할 때 기존 대사창(speakerText/sentenceText)을
    // 빌려 쓰기 위해 이 Instance를 통해 접근한다 (ShowInvestigationLine() 참고).
    public static DialogueSystem Instance;

    // UIManager.cs와 동일한 방식: 씬에 미리 연결해둘 필요 없이 자기 GameObject에서
    // AudioSource를 직접 확보한다 (없으면 새로 붙인다). SFX/BGM을 동시에 겹쳐 들려줘야 하니
    // (예: 발소리 SFX 위에 빗소리 BGM) 두 개로 분리했다. 같은 종류(SFX끼리, BGM끼리)는
    // 한 번에 하나만 재생된다 - 아래 규칙 참고.
    //
    // ===== CSV의 SFX/BGM 칸 사용법 (ApplyLineAudio 참고) =====
    // 파일 위치: SFX는 Assets/Resources/Sounds/SFX/, BGM은 Assets/Resources/Sounds/ 바로 밑.
    // 둘 다 확장자 없이 파일명만 적는다. 기준은 "다음 문장(대사 한 줄)"으로 넘어갈 때마다
    // 매번 적용된다 (CSV 파일이 바뀌는 것과는 무관함):
    //   1) 칸이 비어있는 줄로 넘어가면 그 시점에 재생 중이던 걸 끊는다(정지).
    //   2) 칸에 적힌 이름이 지금 재생 중인 것과 같으면 그대로 이어간다(재시작 안 함).
    //      -> N번 줄부터 M번 줄까지 계속 이어지게 하려면, N~M 줄 전부에 같은 파일명을
    //         적어두면 된다. M+1번 줄에서 칸을 비우면 그때 끊긴다.
    //   3) 칸에 지금과 다른 이름이 적혀있으면, 이전 것을 멈추고 새 걸로 전환한다.
    // CSV 파일(씬)이 바뀌어도 이 규칙은 그대로 적용된다 - 새 CSV 첫 줄이 비어있으면 끊기고,
    // 같은 이름이 적혀있으면 이어진다.
    // 주의: SFX는 더 이상 PlayOneShot이 아니라서(끊을 수 있어야 하므로) 짧은 효과음 두 개를
    // 동시에 겹쳐 재생할 수는 없다 - 새 SFX가 시작되면 이전 SFX는 그 즉시 끊긴다.
    private AudioSource sfxSource;
    private AudioSource bgmSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;

        // 게임 시작 시 scenario_01.csv(프롤로그)부터 자동 로드
        LoadDialogueFromCSV("scenario_01");
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
        // BGM 재생/정지는 줄 단위로 ShowNextSentence()에서 처리한다 (필드 선언부의
        // "CSV의 BGM 칸 사용법" 주석 참고). 여기서 따로 끊지 않아도 새 CSV의 첫 줄이
        // 비어있으면 알아서 끊기고, 같은 곡 이름이면 알아서 이어진다.
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
        lineIndex++;

        // LineType이 "Minigame"인 줄은 대사 대신 미니게임 패널을 띄운다 (MinigameController.cs
        // 상단 주석 참고). 성공하면 다음 줄로 계속 진행하고, 실패하면 바로 엔딩으로 분기한다.
        // 지금은 MinigameController가 "버튼 하나 누르면 무조건 성공"하는 스텁이라 onFailCallback이
        // 실제로 호출되진 않지만, 나중에 진짜 실패 조건이 생겨도 이 호출부는 그대로 두면 된다.
        if (line.isMinigame)
        {
            MinigameController.Instance.StartMinigame(
                line.minigameLabel,
                onSuccessCallback: () => ShowNextSentence(),
                onFailCallback: () => GameFlowManager.Instance.TriggerEnding(line.minigameFailEnding)
            );
            return;
        }

        // LineType이 "Investigate"인 줄은 대사 대신 조사 화면을 띄운다 (InvestigationController.cs
        // 상단 주석 참고). 조사를 마치고 "조사 그만하기"를 누르면 onExit 콜백으로 넘겨준
        // ShowNextSentence()가 다시 호출되어 CSV의 다음 줄부터 이어간다.
        if (line.isInvestigation)
        {
            InvestigationController.Instance.Enter(
                line.investigationId,
                onExit: () => ShowNextSentence()
            );
            return;
        }

        // 지문/나레이션(LineType.Narration)은 화자 이름을 숨긴다 (DialogueLine.cs의 lineType 주석 참고).
        speakerText.text = line.lineType == LineType.Narration ? "" : line.speaker;
        sentenceText.text = line.sentence;

        // 다음 문장으로 넘어갈 때마다 SFX/BGM 칸을 확인한다 (규칙은 필드 선언부 주석 참고):
        // 칸이 비어있으면 끊고, 같은 클립이면 이어가고, 다른 클립이면 전환한다.
        // (SFX도 예전엔 PlayOneShot으로 쏘기만 하고 끊는 방법이 없었다 - PlayOneShot은
        // sfxSource.clip에 기록되지 않아서 Stop()으로만 멈출 수 있다. BGM과 같은 방식으로
        // 통일해서 다음 줄로 넘어가면 SFX도 확실히 끊기도록 고쳤다.)
        ApplyLineAudio(sfxSource, line.sfxToPlay);
        ApplyLineAudio(bgmSource, line.bgmToPlay);
    }

    // Talk 타입 조사 오브젝트(InvestigatableObject.cs 참고, 예: 회사 동료)가
    // InvestigationController.Inspect()를 거쳐서 호출한다. 일반 ShowNextSentence()와 달리
    // lineIndex/currentDialogue를 전혀 건드리지 않고 speakerText/sentenceText만 그 자리에서
    // 바꿔 보여준다 - 즉 "지금 CSV 대사가 어디까지 진행됐는지"는 그대로 유지한 채, 대사창
    // UI만 잠깐 빌려 쓰는 것이다. 다시 조사 화면으로 돌아가는 처리는
    // InvestigationController.DismissTalkLine()이 담당하고, 그 트리거(스페이스/클릭)는
    // 아래 Update()가 IsShowingTalkLine을 보고 분기한다.
    public void ShowInvestigationLine(string speaker, string sentence)
    {
        speakerText.text = speaker;
        sentenceText.text = sentence;
    }

    private void ApplyLineAudio(AudioSource source, AudioClip desiredClip)
    {
        if (desiredClip == null)
        {
            source.Stop();
            source.clip = null;
        }
        else if (source.clip != desiredClip)
        {
            source.Stop();
            source.clip = desiredClip;
            source.Play();
        }
        // else: 같은 클립이 이미 재생 중 -> 그대로 둔다 (재시작하지 않음)
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
        if (MinigameController.Instance != null && MinigameController.Instance.IsActive) return;

        // 조사 모드 처리: 평소엔 조사 화면의 버튼들(InvestigatableObject)이 클릭을 직접
        // 받으므로 여기서 따로 막을 필요가 없다. 다만 "Talk" 타입 오브젝트(예: 회사 동료)를
        // 조사해서 기존 대사창을 임시로 보여주고 있는 동안(IsShowingTalkLine)은, 그 대사창이
        // 실제 CSV lineIndex와 무관한 "오버레이"이므로 스페이스/클릭이 ShowNextSentence()로
        // 새어나가면 안 된다 - 조사 화면이 떠 있는 채로 몰래 CSV가 진행돼버리는 버그가 생긴다.
        // 그래서 이 경우엔 DismissTalkLine()으로 돌려서 "조사 화면으로 복귀"만 시킨다.
        if (InvestigationController.Instance != null && InvestigationController.Instance.IsActive)
        {
            if (InvestigationController.Instance.IsShowingTalkLine &&
                (Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && !IsPointerOverButton())))
            {
                InvestigationController.Instance.DismissTalkLine();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ShowNextSentence();
            return;
        }

        // 마우스 클릭은 "지금 클릭한 지점 아래에 실제 버튼(Button)이 있을 때만" 대사 넘기기를
        // 막는다. 대사창 자체(배경/텍스트)도 UI라서 EventSystem.IsPointerOverGameObject()로
        // "UI 위인지"만 검사하면 화면 어디를 눌러도 항상 UI 위로 판정되어 클릭이 아예 안 먹히는
        // 문제가 있었다. QuickBar 버튼(Note 등)을 눌러서 패널을 여는 클릭이 동시에 "대사창
        // 클릭"으로도 처리되는 것만 막으면 되므로, Button 컴포넌트가 있는지로 좁혀서 검사한다.
        if (Input.GetMouseButtonDown(0) && !IsPointerOverButton())
        {
            ShowNextSentence();
        }
    }

    private bool IsPointerOverButton()
    {
        if (EventSystem.current == null) return false;

        var pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.GetComponentInParent<Button>() != null)
            {
                return true;
            }
        }
        return false;
    }

    //CSV 데이터를 DialogueData로 변환해 불러오기
    //
    // CSV 컬럼: LineType,Speaker,Sentence,BGM,SFX,IsFadeOut,Item,ChoiceText,NextScenario,
    //           IsEndingChoice,TargetEnding,MinigameLabel,InvestigationId
    // (MinigameLabel은 LineType이 "Minigame"인 행에서만, InvestigationId는 LineType이
    // "Investigate"인 행에서만 쓰인다. 실제 미니게임 조작 방식이 정해지면 여기에 컬럼이 더
    // 추가될 수 있다 - DialogueLine.cs 상단 주석 참고.)
    //
    // LineType이 "Choice"인 행은 일반 대사(DialogueLine)가 아니라 선택지(Choice)로 취급되어
    // currentDialogue.choices에 쌓인다. 그 외("Normal"/"Narration"/"Minigame")는 lines에
    // 순서대로 쌓인다 (Minigame도 대사 흐름 중간에 끼워야 하므로 Choice와 다르게 lines 쪽으로
    // 감). 지금 엔진 구조상 선택지는 항상 대사가 다 끝난 뒤 한 번에 보여주므로(ShowChoices()
    // 참고), Choice 행은 CSV 파일의 맨 끝에 몰아서 적어야 한다 (중간에 끼워넣으면 무시되지
    // 않고 그냥 choices 리스트에 똑같이 쌓이긴 하지만, 화면에는 대사가 전부 끝난 뒤에야
    // 나타난다).
    //
    // "Minigame" 행은 MinigameController.cs가 실제로 처리한다. TargetEnding 칸은 Choice
    // 행과 같은 컬럼이지만 여기서는 "실패 시 연결할 엔딩"이라는 뜻으로 재사용된다.
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

            if (string.Equals(lineTypeStr, "Minigame", StringComparison.OrdinalIgnoreCase))
            {
                // 지금은 실제 미니게임 기획이 없어서 "라벨 문구 + 실패 시 엔딩"만 읽어온다.
                // 나중에 실제 조작 방식이 정해지면 여기서 필요한 CSV 컬럼을 추가로 읽어와서
                // DialogueLine의 새 필드에 채워주면 된다 (DialogueLine.cs 상단의 확장 방법 주석 참고).
                var minigameLine = new DialogueLine();
                minigameLine.isMinigame = true;
                minigameLine.minigameLabel = GetField(data[i], "MinigameLabel");

                // TargetEnding 칸을 실패 엔딩으로 재사용한다 (Choice 행의 TargetEnding과 같은 컬럼).
                string failEndingStr = GetField(data[i], "TargetEnding");
                if (Enum.TryParse(failEndingStr, out EndingType parsedFailEnding))
                {
                    minigameLine.minigameFailEnding = parsedFailEnding;
                }
                else
                {
                    Debug.LogWarning($"[DialogueSystem] Minigame 행의 TargetEnding '{failEndingStr}'이 EndingType에 없습니다. (CSV: {csvFileName}, 행: {i + 2})");
                }

                currentDialogue.lines.Add(minigameLine);
                continue;
            }

            if (string.Equals(lineTypeStr, "Investigate", StringComparison.OrdinalIgnoreCase))
            {
                // 조사 화면은 CSV가 아니라 씬에 직접 배치해두므로, 여기서는 어떤 조사 화면을
                // 띄울지 가리키는 식별자(InvestigationId)만 읽어오면 된다.
                var investigateLine = new DialogueLine();
                investigateLine.isInvestigation = true;
                investigateLine.investigationId = GetField(data[i], "InvestigationId");

                currentDialogue.lines.Add(investigateLine);
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

            // BGM도 SFX와 동일한 방식으로 불러온다. 실제 재생/전환 로직은 ShowNextSentence()에 있다.
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
