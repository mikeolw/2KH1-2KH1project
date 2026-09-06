using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// 조사 모드 컨트롤러 - 배경 그림 위에 올려둔 오브젝트를 직접 눌러 조사한다
// =====================================================================================
// ===== 이 게임의 조사 방식 =====
// 별도의 조사 전용 창을 띄우는 게 아니라, 지금 보고 있는 배경 그림 위에 조사할 수 있는
// 오브젝트들이 그대로 놓여 있고 그걸 바로 클릭하는 방식이다. 조사 결과는 (서류나 사진처럼
// 자료 자체를 읽어야 하는 경우가 아니면) 평소 쓰던 대화창에 그대로 출력된다.
//
// ===== 예전 방식과 무엇이 달라졌나 =====
// 예전에는 씬에 조사 화면 Panel(검은 배경 + 회색 네모 placeholder)을 미리 만들어두고
// 그걸 켜고 끄는 방식이었다. 그래서
//   - 조사할 때마다 검은 창이 배경을 가렸고,
//   - 조사 화면을 하나 늘릴 때마다 유니티에서 손으로 Panel을 만들어야 했으며,
//   - 배경 그림이 34장인데 조사 화면은 6개뿐이라 오브젝트를 배경별로 나눌 수가 없었다.
//
// 지금은 CSV만 보고 조사 화면을 그때그때 만들어낸다. 씬에 미리 만들어둘 것이 전혀 없고,
// 배경 하나당 조사 화면 하나를 얼마든지 늘릴 수 있다. InvestigationData.csv에 줄만 추가하면 된다.
//
// ===== 화면이 만들어지는 순서 =====
//   1) CSV에서 이 조사 화면(InvestigationId)의 배경과 오브젝트 목록을 읽는다
//   2) StageController에게 배경을 깔게 한다 (대사 장면과 같은 배경 시스템을 그대로 쓴다)
//   3) 오브젝트마다 Image + Button을 만들어 배경 위에 올린다
//      - 위치/크기는 IllustLayout.csv를 따른다 (IllustLayout.cs 참고)
//      - 투명한 부분은 클릭이 통과하므로 그림이 그려진 곳만 눌린다
//   4) "조사 그만하기" 버튼을 화면 구석에 만든다
//   5) 안내문(IntroText)을 대화창에 띄운다
//
// 조사가 끝나면 만들어둔 오브젝트를 전부 지우고, CSV의 다음 대사로 이어간다.
public class InvestigationController : MonoBehaviour
{
    public static InvestigationController Instance;

    [Header("대사창 (조사 결과를 여기에 출력한다)")]
    [Tooltip("비워두면 씬에서 DialoguePanel이라는 이름으로 찾는다.")]
    public GameObject dialoguePanel;

    [Header("조사 오브젝트를 올릴 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    [Header("'조사 그만하기' 버튼 문구")]
    public string exitButtonLabel = "조사 그만하기";

    // 이번 조사에서 만들어낸 오브젝트들을 담아두는 부모. 조사가 끝나면 통째로 지운다.
    private GameObject hotspotRoot;

    // Exit()에서 호출할 콜백. DialogueSystem이 "조사가 끝나면 다음 대사로" 라고 넘겨준다.
    private Action onExitCallback;

    private bool inSession;
    private string activeScreenId;

    public bool IsActive => inSession;

    // 조사 결과 대사를 대화창에 띄우고 있는 중인지.
    // DialogueSystem.Update()가 이 값을 보고, 스페이스/클릭을 "CSV 다음 줄"이 아니라
    // "조사 대사 닫기"로 돌린다.
    public bool IsShowingTalkLine { get; private set; }

    // ===== 조사 데이터 (Resources/Dialogues/InvestigationData.csv) =====
    // 컬럼: InvestigationId,HotspotKey,Type,ObjectName,Speaker,Text,ItemId,Sprite
    //   - HotspotKey : 오브젝트를 구분하는 이름. 조사기록(NoteEntries.csv)에서 이 이름으로 가리킨다.
    //   - Type       : Item(획득) / Description(설명만) / Talk(말 걸기)
    //   - Sprite     : 배경 위에 올릴 그림 (Resources/Illusts/Objects/ 기준 파일 이름)
    //   - 특수 키 IntroText   : 조사 시작할 때 대화창에 띄울 안내문
    //   - 특수 키 Background  : 이 조사 화면의 배경 그림 (Sprite 칸에 배경 파일 이름)
    private const string InvestigationDataCsv = "InvestigationData";

    private class HotspotData
    {
        public string key;
        public HotspotType type;
        public string objectName;
        public string speaker;
        public string text;
        public string itemId;
        public string spriteName;
    }

    // 조사 화면 하나에 대한 정보
    private class ScreenData
    {
        public string backgroundName;
        public string introText;
        public readonly List<HotspotData> hotspots = new List<HotspotData>();
    }

    // InvestigationId -> 화면 정보
    private Dictionary<string, ScreenData> screenData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ---------------------------------------------------------------------------------
    // CSV 로딩
    // ---------------------------------------------------------------------------------
    private void LoadDataIfNeeded()
    {
        if (screenData != null) return;
        screenData = new Dictionary<string, ScreenData>();

        var rows = CSVReader.Read("Dialogues/" + InvestigationDataCsv);
        if (rows == null || rows.Count == 0)
        {
            Debug.LogWarning($"[InvestigationController] Dialogues/{InvestigationDataCsv}.csv를 읽지 못했습니다.");
            return;
        }

        foreach (var row in rows)
        {
            string id = GetField(row, "InvestigationId").Trim();
            string key = GetField(row, "HotspotKey").Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key)) continue;

            if (!screenData.TryGetValue(id, out ScreenData screen))
            {
                screen = new ScreenData();
                screenData[id] = screen;
            }

            // 특수 키 1: 조사 시작 안내문
            if (key == "IntroText")
            {
                screen.introText = GetField(row, "Text");
                continue;
            }

            // 특수 키 2: 이 조사 화면의 배경
            if (key == "Background")
            {
                string bg = GetField(row, "Sprite").Trim();
                if (string.IsNullOrEmpty(bg)) bg = GetField(row, "Text").Trim();
                screen.backgroundName = bg;
                continue;
            }

            // 일반 조사 오브젝트
            if (!Enum.TryParse(GetField(row, "Type"), true, out HotspotType type))
            {
                Debug.LogWarning($"[InvestigationController] '{GetField(row, "Type")}'은 조사 타입(Item/Description/Talk)이 아닙니다. " +
                                 $"(InvestigationId={id}, HotspotKey={key})");
                continue;
            }

            screen.hotspots.Add(new HotspotData
            {
                key = key,
                type = type,
                objectName = GetField(row, "ObjectName"),
                speaker = GetField(row, "Speaker"),
                text = GetField(row, "Text"),
                itemId = GetField(row, "ItemId"),
                spriteName = GetField(row, "Sprite").Trim()
            });
        }
    }

    private string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var value) ? value.ToString() : "";
    }

    // ---------------------------------------------------------------------------------
    // 조사 시작 / 종료
    // ---------------------------------------------------------------------------------

    // DialogueSystem이 LineType=Investigate 행을 만났을 때 호출한다.
    public void Enter(string investigationId, Action onExit)
    {
        LoadDataIfNeeded();

        if (string.IsNullOrWhiteSpace(investigationId) ||
            !screenData.TryGetValue(investigationId.Trim(), out ScreenData screen))
        {
            // 데이터가 없으면 조사를 건너뛰고 다음 대사로 넘어간다.
            // (조사 하나 때문에 게임이 멈추는 것보다 낫다)
            Debug.LogWarning($"[InvestigationController] '{investigationId}' 조사 데이터를 찾을 수 없습니다. " +
                             "InvestigationData.csv의 InvestigationId를 확인하세요.");
            onExit?.Invoke();
            return;
        }

        if (!EnsureCanvas())
        {
            onExit?.Invoke();
            return;
        }

        inSession = true;
        IsShowingTalkLine = false;
        onExitCallback = onExit;
        activeScreenId = investigationId.Trim();

        // 1) 배경을 깐다. 대사 장면과 같은 배경 시스템을 그대로 쓰므로,
        //    조사 중에도 캐릭터 스탠딩이 필요하면 그대로 남길 수 있다.
        if (StageController.Instance != null && !string.IsNullOrEmpty(screen.backgroundName))
        {
            StageController.Instance.ApplyBackground(screen.backgroundName);
        }

        // 2) 배경 위에 조사 오브젝트를 올린다.
        BuildHotspots(screen);

        // 3) 안내문을 대화창에 띄운다.
        if (!string.IsNullOrWhiteSpace(screen.introText))
        {
            ShowLineInDialogue("", screen.introText);
        }
        else
        {
            // 안내문이 없으면 대화창은 비워둔다(조사 화면을 가리지 않게).
            SetDialogueVisible(false);
        }
    }

    // "조사 그만하기" 버튼이 호출한다.
    public void Exit()
    {
        if (!inSession) return;

        ClearHotspots();

        inSession = false;
        IsShowingTalkLine = false;

        // 조사가 끝나면 대화창을 다시 켜서 다음 대사가 보이게 한다.
        SetDialogueVisible(true);

        // 이 조사 화면을 마쳤다는 사실을 조사기록(수첩)에 남긴다.
        if (NoteManager.Instance != null && !string.IsNullOrEmpty(activeScreenId))
        {
            NoteManager.Instance.OnInvestigationFinished(activeScreenId);
        }
        activeScreenId = null;

        // 콜백을 지역 변수로 옮긴 뒤 비우고 호출한다. 콜백(ShowNextSentence) 안에서 다시
        // Enter()가 불릴 수 있으므로(다음 줄이 또 조사인 경우) 이전 콜백이 남아있으면 안 된다.
        Action callback = onExitCallback;
        onExitCallback = null;
        callback?.Invoke();
    }

    // ---------------------------------------------------------------------------------
    // 조사 오브젝트 만들기 (핵심)
    // ---------------------------------------------------------------------------------
    private void BuildHotspots(ScreenData screen)
    {
        ClearHotspots();

        // 오브젝트들을 담을 부모를 만든다. 화면 전체 크기이고, 자기 자신은 클릭을 받지 않는다.
        hotspotRoot = new GameObject("InvestigationHotspots", typeof(RectTransform));
        hotspotRoot.transform.SetParent(targetCanvas.transform, false);
        StretchFull(hotspotRoot.GetComponent<RectTransform>());

        // 그리는 순서: 배경/스탠딩보다는 앞, 대화창보다는 뒤.
        // 대화창 바로 앞자리에 끼워 넣으면 이 조건이 자동으로 맞는다.
        PlaceBehindDialogue(hotspotRoot.transform);

        foreach (var data in screen.hotspots)
        {
            CreateHotspot(data);
        }

        CreateExitButton();
    }

    // 조사 오브젝트 하나를 만든다.
    private void CreateHotspot(HotspotData data)
    {
        var go = new GameObject(data.key, typeof(RectTransform), typeof(Image), typeof(Button), typeof(InvestigatableObject));
        go.transform.SetParent(hotspotRoot.transform, false);

        // 데이터 채우기
        var io = go.GetComponent<InvestigatableObject>();
        io.type = data.type;
        io.objectName = data.objectName;
        io.description = data.text;
        io.itemId = data.itemId;
        io.spriteName = data.spriteName;
        io.talkSpeaker = string.IsNullOrEmpty(data.speaker) ? data.objectName : data.speaker;
        io.talkSentence = data.text;

        var image = go.GetComponent<Image>();

        if (string.IsNullOrEmpty(data.spriteName))
        {
            // 그림이 지정되지 않은 오브젝트(예: 창문처럼 배경에 이미 그려진 것).
            // 눈에 보이는 그림 없이 클릭 영역만 필요한 경우인데, 위치 정보도 없으면
            // 어디를 눌러야 할지 알 수 없으므로 만들지 않고 넘어간다.
            if (!IllustLayout.TryGet(data.key, out _))
            {
                Destroy(go);
                return;
            }

            // 배치표에 좌표가 있으면 투명한 클릭 영역으로 둔다.
            image.color = new Color(1f, 1f, 1f, 0f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            IllustLayout.TryGet(data.key, out var p);
            rt.anchoredPosition = p.Position;
            rt.sizeDelta = new Vector2(120f, 120f) * p.scale;
        }
        else
        {
            // 그림을 올리고, 위치/크기를 배치표대로 잡고, 투명 픽셀은 클릭이 통과하게 한다.
            // (전부 InvestigatableObject.ApplyIllust가 처리한다)
            io.ApplyIllust();
        }

        go.GetComponent<Button>().onClick.AddListener(io.OnClickInspect);
    }

    // 화면 구석에 "조사 그만하기" 버튼을 만든다.
    private void CreateExitButton()
    {
        var go = new GameObject("Btn_ExitInvestigation", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(hotspotRoot.transform, false);

        var rt = go.GetComponent<RectTransform>();
        // 오른쪽 위 구석. 대화창(아래쪽)이나 퀵바와 겹치지 않는 자리다.
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(180f, 52f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.82f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = exitButtonLabel;
        tmp.fontSize = 22;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        go.GetComponent<Button>().onClick.AddListener(Exit);

        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨져 보인다.
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(go);
    }

    private void ClearHotspots()
    {
        if (hotspotRoot != null)
        {
            Destroy(hotspotRoot);
            hotspotRoot = null;
        }
    }

    // ---------------------------------------------------------------------------------
    // 조사 결과 출력
    // ---------------------------------------------------------------------------------

    // InvestigatableObject.OnClickInspect()가 호출한다.
    public void Inspect(InvestigatableObject obj)
    {
        // ===== 무엇을 살펴봤는지 조사기록(수첩)에 남긴다 =====
        // 이 게임의 수첩은 주인공이 조사하면서 실시간으로 적어나가는 것이므로,
        // 조사한 것은 무엇이든 기록에 남아야 한다.
        //   1) NoteEntries.csv에 이 오브젝트용으로 따로 써둔 문장이 있으면 그것을 쓴다.
        //   2) 없으면 방금 조사해서 화면에 나온 내용을 그대로 수첩에 옮겨 적는다.
        // 예전에는 1번만 있어서, CSV에 안 적어둔 오브젝트를 조사하면 수첩이 그대로였다.
        // (#01 내 책상만 해도 조사할 것이 일곱 개인데 CSV에는 두 개뿐이었다)
        if (NoteManager.Instance != null && !string.IsNullOrEmpty(activeScreenId))
        {
            bool hasWrittenNote = NoteManager.Instance.OnHotspotInspected(activeScreenId, obj.gameObject.name);

            if (!hasWrittenNote)
            {
                // Talk 타입은 대사이므로 "누가 이렇게 말했다" 형태로, 나머지는 조사 설명 그대로 적는다.
                string noteBody = obj.type == HotspotType.Talk ? obj.talkSentence : obj.description;
                string noteName = obj.type == HotspotType.Talk
                    ? (string.IsNullOrEmpty(obj.talkSpeaker) ? obj.objectName : obj.talkSpeaker)
                    : obj.objectName;

                NoteManager.Instance.AddAutoEntry(activeScreenId, obj.gameObject.name, noteName, noteBody);
            }
        }

        if (obj.type == HotspotType.Item && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(obj.itemId);
        }

        // 서류/사진처럼 자료 자체를 읽어야 하는 것만 전체화면 뷰어로 펼친다.
        // 그 외에는 전부 대화창에 출력한다.
        if (TryOpenDocumentViewer(obj.itemId)) return;

        if (obj.type == HotspotType.Talk)
        {
            string speaker = string.IsNullOrEmpty(obj.talkSpeaker) ? obj.objectName : obj.talkSpeaker;
            ShowLineInDialogue(speaker, obj.talkSentence);
        }
        else
        {
            // 조사 설명은 지문처럼 화자 없이 보여준다.
            ShowLineInDialogue("", obj.description);
        }
    }

    // 대화창에 한 줄 띄운다. 조사 화면(오브젝트들)은 계속 보이는 채로 대화창만 위에 겹친다.
    private void ShowLineInDialogue(string speaker, string sentence)
    {
        IsShowingTalkLine = true;
        SetDialogueVisible(true);

        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.ShowInvestigationLine(speaker, sentence);
        }
    }

    // DialogueSystem.Update()가 조사 대사 표시 중 스페이스/클릭을 감지했을 때 호출한다.
    public void DismissTalkLine()
    {
        if (!IsShowingTalkLine) return;

        IsShowingTalkLine = false;

        // 대화창을 닫아서 조사 화면을 가리지 않게 한다.
        // (조사 오브젝트들은 계속 그 자리에 있으므로 바로 다음 것을 누를 수 있다)
        SetDialogueVisible(false);
    }

    private bool TryOpenDocumentViewer(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (DocumentViewerController.Instance == null) return false;

        return DocumentViewerController.Instance.ShowItem(itemId);
    }

    // ---------------------------------------------------------------------------------
    // 화면 도우미
    // ---------------------------------------------------------------------------------
    private bool EnsureCanvas()
    {
        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();

        if (targetCanvas == null)
        {
            Debug.LogError("[InvestigationController] 씬에 Canvas가 없어 조사 화면을 만들 수 없습니다.");
            return false;
        }
        return true;
    }

    private GameObject GetDialoguePanel()
    {
        if (dialoguePanel != null) return dialoguePanel;

        // 인스펙터에서 연결하지 않았으면 이름으로 찾아본다.
        if (targetCanvas != null)
        {
            var found = targetCanvas.transform.Find("DialoguePanel");
            if (found != null) dialoguePanel = found.gameObject;
        }
        return dialoguePanel;
    }

    private void SetDialogueVisible(bool visible)
    {
        var panel = GetDialoguePanel();
        if (panel != null) panel.SetActive(visible);
    }

    // 조사 오브젝트들이 대화창보다 뒤에 그려지도록 계층 순서를 잡는다.
    // (유니티 UI는 계층에서 아래에 있을수록 앞에 그려진다)
    private void PlaceBehindDialogue(Transform target)
    {
        var panel = GetDialoguePanel();
        if (panel != null && panel.transform.parent == target.parent)
        {
            target.SetSiblingIndex(panel.transform.GetSiblingIndex());
        }
        else
        {
            // 대화창을 못 찾으면 맨 앞으로 보낸다(최소한 배경에 가려지지는 않게).
            target.SetAsLastSibling();
        }
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
