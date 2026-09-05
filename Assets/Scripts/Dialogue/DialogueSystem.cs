using System;
using System.Collections;
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

    // 지금 진행 중인 CSV 파일 이름(확장자 제외). 세이브/로드에서 "어디까지 봤는지"를
    // 기록하고 복원하는 데 쓴다 (SavePointManager.cs 참고).
    private string currentScenarioCsv = "";
    public string CurrentScenarioCsv => currentScenarioCsv;

    // 지금 몇 번째 줄까지 진행했는지. lineIndex는 "다음에 보여줄 줄"을 가리키므로,
    // 이어하기를 할 때 이 값을 그대로 lineIndex에 넣으면 저장 시점의 다음 줄부터 이어진다.
    public int CurrentLineIndex => lineIndex;

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

    [Header("화면 연출")]
    // DialogueLine.isFadeOut 줄을 보여줄 때 쓰는 화면 전체 검은 오버레이.
    // 시간 경과/장소 전환처럼 급격한 배경·대사 전환을 암전으로 부드럽게 가리는 용도.
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;   // 암전/복귀 각각에 걸리는 시간(초)
    public float blackHoldDuration = 1f; // 완전히 검게 된 채로 유지되는 시간(초)

    // 암전 코루틴이 도는 동안 스페이스/클릭으로 대사를 건너뛰지 못하게 막는 플래그.
    private bool isFading;

    // =================================================================================
    // 텍스트 타이핑 연출 / 자동 진행 (환경설정의 "텍스트 속도", "자동 진행"과 연결)
    // =================================================================================
    // ===== 동작 흐름 =====
    //   1) 대사 한 줄이 표시되면 TypeSentence 코루틴이 글자를 하나씩 늘려가며 찍는다.
    //      찍는 동안 말하는 캐릭터의 스탠딩은 입을 뻐끔거린다(StageController.SetTalking).
    //   2) 타이핑 도중에 플레이어가 클릭/스페이스를 누르면 "다음 줄로 넘어가는" 게 아니라
    //      "지금 줄을 즉시 전부 표시"한다. (미연시의 표준 동작)
    //   3) 다 찍힌 뒤 다시 누르면 그때 다음 줄로 넘어간다.
    //   4) 환경설정에서 자동 진행이 켜져 있으면, 다 찍힌 뒤 잠시 기다렸다가 알아서 넘어간다.
    //
    // 속도와 자동 진행 여부는 SettingsManager.Current에서 매번 읽어오므로, 설정 화면에서
    // 값을 바꾸면 다음 대사부터 바로 반영된다.

    // 지금 글자를 찍고 있는 중인지. Update()가 "즉시 완성"과 "다음 줄" 중 뭘 할지 판단하는 기준.
    private bool isTyping;

    // 돌아가고 있는 타이핑 코루틴. 새 줄을 표시할 때 이전 것을 확실히 멈추기 위해 들고 있는다.
    private Coroutine typingRoutine;

    // 돌아가고 있는 자동 진행 대기 코루틴. 플레이어가 수동으로 넘기면 취소해야 한다.
    private Coroutine autoAdvanceRoutine;

    // 지금 화면에 찍고 있는 대사의 "완성된 전체 문장". 타이핑을 건너뛸 때 이걸 통째로 넣는다.
    private string currentFullSentence = "";

    // 지금 표시 중인 줄. 자동 진행/립싱크 처리에 화자 정보가 필요해서 들고 있는다.
    private DialogueLine currentLine;

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

        // 환경설정의 효과음/배경음악 볼륨 슬라이더가 실제로 이 소리에 반영되도록 등록한다.
        // (예전에는 등록하는 곳이 없어서 슬라이더를 움직여도 아무 변화가 없었다 - AudioManager.cs 참고)
        AudioManager.RegisterSafe(sfxSource, AudioManager.Channel.Sfx);
        AudioManager.RegisterSafe(bgmSource, AudioManager.Channel.Bgm);

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

        // isFadeOut 줄은 화면을 암전시킨 뒤에 대사/사운드를 바꾸고 다시 밝게 복귀한다.
        if (line.isFadeOut)
        {
            StartCoroutine(ShowLineWithFade(line));
        }
        else
        {
            DisplayLine(line);
        }
    }

    // 화면을 검게 암전(alpha 0->1) -> 그 상태에서 대사/사운드 교체 -> blackHoldDuration만큼 검은
    // 화면 유지 -> 다시 밝게 복귀(alpha 1->0). 시간 경과/장소 전환처럼 급격한 전환을 부드럽게
    // 가리는 용도 (DialogueLine.isFadeOut 참고).
    private IEnumerator ShowLineWithFade(DialogueLine line)
    {
        isFading = true;
        if (fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = true;

        yield return StartCoroutine(Fade(1f));
        DisplayLine(line);
        yield return new WaitForSeconds(blackHoldDuration);
        yield return StartCoroutine(Fade(0f));

        if (fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = false;
        isFading = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeCanvasGroup == null) yield break;

        float startAlpha = fadeCanvasGroup.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = targetAlpha;
    }

    // 지문/나레이션(LineType.Narration)은 화자 이름을 숨긴다 (DialogueLine.cs의 lineType 주석 참고).
    // 다음 문장으로 넘어갈 때마다 SFX/BGM 칸을 확인한다 (규칙은 필드 선언부 주석 참고):
    // 칸이 비어있으면 끊고, 같은 클립이면 이어가고, 다른 클립이면 전환한다.
    // (SFX도 예전엔 PlayOneShot으로 쏘기만 하고 끊는 방법이 없었다 - PlayOneShot은
    // sfxSource.clip에 기록되지 않아서 Stop()으로만 멈출 수 있다. BGM과 같은 방식으로
    // 통일해서 다음 줄로 넘어가면 SFX도 확실히 끊기도록 고쳤다.)
    private void DisplayLine(DialogueLine line)
    {
        currentLine = line;

        speakerText.text = line.lineType == LineType.Narration ? "" : line.speaker;

        // ===== 1) 배경 / 캐릭터 스탠딩 갱신 =====
        // 값이 비어 있는 칸은 StageController가 "이전 상태 유지"로 처리하므로,
        // 여기서 굳이 빈 값인지 검사할 필요가 없다.
        if (StageController.Instance != null)
        {
            StageController.Instance.ApplyBackground(line.backgroundName);
            StageController.Instance.ApplyStandings(line.standingNames, line.standingPositions);
        }

        // ===== 2) 사운드 =====
        ApplyLineAudio(sfxSource, line.sfxToPlay);
        ApplyLineAudio(bgmSource, line.bgmToPlay);

        // ===== 3) 아이템 획득 =====
        // CSV의 Item 칸에 아이템 id가 적혀 있으면 이 줄이 표시되는 순간 가방에 들어간다.
        // (조사 화면에서 오브젝트를 클릭해 얻는 것과 별개로, 대사 흐름 중에 자동으로
        //  얻어야 하는 아이템을 위한 통로다. 예: 이야기상 그냥 건네받는 물건)
        if (!string.IsNullOrWhiteSpace(line.acquireItemName) && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(line.acquireItemName.Trim());
        }

        // ===== 4) 세이브포인트 =====
        // 시나리오 문서의 {세이브포인트}에 해당하는 줄. 여기서만 저장이 허용된다.
        if (line.isSavePoint && SavePointManager.Instance != null)
        {
            SavePointManager.Instance.ReachSavePoint(line.savePointId, currentScenarioCsv, lineIndex);
        }

        // ===== 5) 대사 텍스트 타이핑 시작 =====
        StartTyping(line.sentence);
    }

    // ---------------------------------------------------------------------------------
    // 텍스트 타이핑 연출
    // ---------------------------------------------------------------------------------

    // 한 줄을 화면에 찍기 시작한다. 설정이 "즉시"면 타이핑 없이 통째로 표시한다.
    private void StartTyping(string sentence)
    {
        // 이전 줄의 타이핑/자동진행이 남아있으면 확실히 정리한다.
        StopTypingRoutine();
        StopAutoAdvanceRoutine();

        currentFullSentence = sentence ?? "";

        bool instant = SettingsManager.Instance != null && SettingsManager.Instance.IsInstantText;

        if (instant || string.IsNullOrEmpty(currentFullSentence))
        {
            // 즉시 표시: 코루틴을 돌릴 필요가 없다.
            sentenceText.text = currentFullSentence;
            isTyping = false;
            OnLineFullyShown();
            return;
        }

        typingRoutine = StartCoroutine(TypeSentence(currentFullSentence));
    }

    // 글자를 하나씩 늘려가며 찍는 코루틴.
    //
    // ===== maxVisibleCharacters를 쓰는 이유 =====
    // sentenceText.text에 문자열을 조금씩 잘라 넣는 방식(text = s.Substring(0, i))은
    // 글자를 넣을 때마다 TextMeshPro가 줄바꿈을 다시 계산해서, 문장 끝 단어가 다음 줄로
    // 내려가는 순간 이미 찍힌 글자들이 출렁이며 움직인다. 대신 전체 문장을 한 번에 넣어두고
    // "몇 글자까지 보여줄지"(maxVisibleCharacters)만 늘리면 레이아웃이 처음부터 확정되어
    // 글자가 제자리에서 하나씩 나타난다. 미연시에서 흔히 쓰는 방법이다.
    private IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;

        sentenceText.text = sentence;
        sentenceText.maxVisibleCharacters = 0;

        // 말하는 캐릭터의 입을 움직이기 시작한다.
        SetTalkingAnimation(true);

        // TMP가 글자 수를 세려면 한 번 갱신이 필요하다. 이걸 안 하면 첫 프레임에
        // textInfo.characterCount가 0이라 문장이 통째로 건너뛰어질 수 있다.
        sentenceText.ForceMeshUpdate();
        int totalChars = sentenceText.textInfo.characterCount;

        float charsPerSecond = SettingsManager.Instance != null
            ? SettingsManager.Instance.TextSpeedCharsPerSecond
            : 40f;
        float secondsPerChar = 1f / Mathf.Max(1f, charsPerSecond);

        float timer = 0f;
        int visible = 0;

        while (visible < totalChars)
        {
            timer += Time.deltaTime;

            // 한 프레임에 여러 글자가 찍혀야 할 만큼 빠른 설정일 수도 있으므로 while로 처리한다.
            // (예: 80자/초인데 프레임이 30fps면 한 프레임에 약 2~3글자씩 찍어야 한다.)
            while (timer >= secondsPerChar && visible < totalChars)
            {
                timer -= secondsPerChar;
                visible++;
            }

            sentenceText.maxVisibleCharacters = visible;
            yield return null;
        }

        isTyping = false;
        typingRoutine = null;
        OnLineFullyShown();
    }

    // 타이핑 도중 클릭/스페이스를 눌렀을 때: 다음 줄로 넘어가지 않고 지금 줄을 즉시 완성한다.
    private void CompleteTypingImmediately()
    {
        StopTypingRoutine();

        sentenceText.text = currentFullSentence;
        sentenceText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        OnLineFullyShown();
    }

    // 한 줄이 화면에 완전히 표시되었을 때 공통으로 할 일.
    private void OnLineFullyShown()
    {
        // 말이 끝났으므로 입을 다문다.
        SetTalkingAnimation(false);

        // 자동 진행이 켜져 있으면 잠시 뒤 다음 줄로 넘어가도록 예약한다.
        if (SettingsManager.Instance != null && SettingsManager.Instance.Current.autoAdvance)
        {
            StopAutoAdvanceRoutine();
            autoAdvanceRoutine = StartCoroutine(AutoAdvanceAfterLine());
        }
    }

    // 자동 진행: 대사를 다 읽을 만한 시간을 기다렸다가 스스로 다음 줄로 넘어간다.
    private IEnumerator AutoAdvanceAfterLine()
    {
        // 기본 대기시간 + 글자 수에 비례한 읽기 시간.
        // 짧은 대사("응!")와 긴 대사가 똑같은 시간만 머무르면 짧은 건 답답하고 긴 건 놓치게 되므로,
        // 글자 수에 비례한 시간을 더해준다. (한글 기준 초당 약 12자를 읽는다고 가정)
        float baseDelay = SettingsManager.Instance != null
            ? SettingsManager.Instance.Current.autoAdvanceDelay
            : 1.2f;
        float readingTime = currentFullSentence.Length / 12f;

        yield return new WaitForSeconds(baseDelay + readingTime);

        autoAdvanceRoutine = null;

        // 기다리는 사이에 선택지가 뜨거나 팝업이 열렸을 수 있으므로 다시 확인한다.
        if (IsBlockedByOtherUI()) yield break;

        ShowNextSentence();
    }

    // 말하는 캐릭터의 입 뻐끔 연출을 켜고 끈다.
    // 나레이션(화자 없음)일 때는 아무도 입을 움직이지 않는다.
    private void SetTalkingAnimation(bool talking)
    {
        if (StageController.Instance == null || currentLine == null) return;

        bool isNarration = currentLine.lineType == LineType.Narration;
        string speaker = isNarration ? "" : currentLine.speaker;

        StageController.Instance.SetTalking(speaker, currentLine.talkerSlot, talking && !isNarration);
    }

    private void StopTypingRoutine()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
        isTyping = false;
    }

    private void StopAutoAdvanceRoutine()
    {
        if (autoAdvanceRoutine != null)
        {
            StopCoroutine(autoAdvanceRoutine);
            autoAdvanceRoutine = null;
        }
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
        // 진행 중인 타이핑/자동진행은 확실히 멈춘다. 안 그러면 조사 대사를 보여주는 도중에
        // 원래 대사의 타이핑 코루틴이 글자 수를 계속 덮어써서 글자가 뒤섞인다.
        StopTypingRoutine();
        StopAutoAdvanceRoutine();

        speakerText.text = speaker;
        sentenceText.text = sentence;

        // 직전 타이핑에서 maxVisibleCharacters가 작은 값으로 남아 있으면 글자가 잘려 보인다.
        // 조사 오버레이 대사는 타이핑 없이 통째로 보여주므로 제한을 풀어준다.
        sentenceText.maxVisibleCharacters = int.MaxValue;
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
            // 선택지가 없는 장면: 대사 행에 NextScenario가 적혀있었다면(autoNextScenarioCsv)
            // 선택지 UI 없이 곧바로 다음 CSV로 이어간다.
            if (!string.IsNullOrEmpty(currentDialogue.autoNextScenarioCsv))
            {
                LoadDialogueFromCSV(currentDialogue.autoNextScenarioCsv);
                return;
            }

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

    // 지금 대사 진행을 막아야 하는 UI(선택지/팝업/미니게임/자료 뷰어)가 떠 있는지 확인한다.
    // Update()와 자동 진행 코루틴이 똑같은 조건을 봐야 해서 함수로 빼두었다.
    private bool IsBlockedByOtherUI()
    {
        if (choicePanel != null && choicePanel.activeSelf) return true;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyPanelOpen) return true;
        if (MinigameController.Instance != null && MinigameController.Instance.IsActive) return true;
        if (DocumentViewerController.Instance != null && DocumentViewerController.Instance.IsOpen) return true;
        return false;
    }

    void Update()
    {
        // 암전 연출(ShowLineWithFade) 진행 중엔 스페이스/클릭으로 건너뛰지 못하게 막는다.
        if (isFading) return;

        // 선택지 패널이나 UIManager 팝업(조사기록/인벤토리/사진첩/핸드폰/설정), 자료 뷰어가
        // 열려있을 땐 스페이스바로도 대사가 넘어가면 안 된다.
        if (IsBlockedByOtherUI()) return;

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

        // ===== 타이핑 중이면 "다음 줄"이 아니라 "지금 줄 즉시 완성" =====
        // 미연시의 표준 동작이다. 글자가 찍히는 도중에 누르면 문장이 통째로 나타나고,
        // 다 나타난 뒤에 한 번 더 눌러야 다음 줄로 넘어간다.
        if (isTyping)
        {
            if (Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && !IsPointerOverButton()))
            {
                CompleteTypingImmediately();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StopAutoAdvanceRoutine(); // 손으로 넘겼으면 예약된 자동 진행은 취소
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
            StopAutoAdvanceRoutine(); // 손으로 넘겼으면 예약된 자동 진행은 취소
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
    //
    // 일반 대사 행(Normal/Narration)의 NextScenario 칸은 선택지(Choice)가 하나도 없는 CSV의
    // 맨 마지막 행에만 채우면 된다 - 대사가 다 끝났을 때 선택지 UI 없이 바로 그 CSV로 이어간다
    // (DialogueData.autoNextScenarioCsv, ShowChoices() 참고). Choice 행이 하나라도 있으면
    // 이 칸은 무시되고 선택지 UI가 대신 뜬다.
    public void LoadDialogueFromCSV(string csvFileName)
    {
        // 지금 어떤 CSV를 진행 중인지 기억해둔다. 세이브할 때 "어느 파일 몇 번째 줄에서
        // 저장했는지"를 남겨야 나중에 정확히 그 지점부터 이어할 수 있기 때문이다
        // (SavePointManager.cs 참고).
        currentScenarioCsv = csvFileName;

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

            // ===== 배경 / 캐릭터 스탠딩 (StageController.cs가 처리) =====
            // 네 칸 모두 비워두면 "이전 줄 상태 그대로 유지"라는 뜻이라, 장면이나 표정이
            // 바뀌는 줄에만 적으면 된다. 컬럼 자체가 없는 예전 CSV도 GetField가 ""를 돌려주므로
            // 아무 문제 없이 동작한다.
            line.backgroundName = GetField(data[i], "Background");
            line.standingNames = GetField(data[i], "Standing");
            line.standingPositions = GetField(data[i], "StandingPos");
            line.talkerSlot = GetField(data[i], "Talker");

            // ===== 세이브포인트 =====
            // 시나리오 문서의 {세이브포인트}에 해당하는 줄에 IsSavePoint=TRUE를 적어둔다.
            // 플레이어는 이 줄을 지나간 뒤부터 다음 세이브포인트까지 "저장하기"를 쓸 수 있다
            // (SavePointManager.cs 참고).
            line.isSavePoint = GetField(data[i], "IsSavePoint").ToLower() == "true";
            line.savePointId = GetField(data[i], "SavePointId");

            // 선택지 없이 바로 다음 CSV로 넘어가야 하는 장면을 위한 칸 (DialogueData.autoNextScenarioCsv
            // 참고). 보통 CSV 맨 마지막 대사 행에만 채워두면 된다.
            string autoNextScenario = GetField(data[i], "NextScenario");
            if (!string.IsNullOrEmpty(autoNextScenario))
            {
                currentDialogue.autoNextScenarioCsv = autoNextScenario;
            }

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
