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

    // 지금 보여주고 있는 Talk 대사를 닫으면(DismissTalkLine) 이어서 띄울 선택지.
    // Inspect()가 Talk 오브젝트에 선택지가 달려 있을 때 채워둔다 (ShowTalkChoices 참고).
    private List<InvestigationTalkChoice> pendingTalkChoices;

    // pendingTalkChoices를 채운 조사 오브젝트 그 자체. 선택지를 골랐을 때(OnTalkChoiceSelected)
    // "이 선택지가 어느 오브젝트에서 나왔는지" 알아야 하는 경우에 쓴다 - 예) 자료실 문의
    // "알리지 않는다" 선택지는 곧바로 타이밍 클릭 미니게임을 걸어야 하는데, 성공했을 때
    // 이동할 화면은 이 오브젝트의 afterTargetScreenId(=BG_07_InvestigationSite_05, 문을
    // 다시 눌렀을 때 자동 이동하는 화면과 같은 값)를 그대로 재사용한다.
    private InvestigatableObject pendingTalkChoiceSource;

    // ===== 자료실 문: 선택지를 고르는 즉시 타이밍 클릭 미니게임으로 이어지는 유일한 지점 =====
    // 새 CSV 컬럼을 추가하지 않고(팀 규칙) 이 한 곳만을 위한 예외 분기이므로, 다른
    // ScreensWithoutExitButton처럼 화면/오브젝트 이름을 코드에 직접 적어 식별한다.
    // 나중에 다른 곳에도 같은 방식(선택지 -> 타이밍 미니게임)이 필요해지면, 이 두 상수를
    // 목록(배열/HashSet)으로 바꾸고 OnTalkChoiceSelected()의 판정도 그에 맞게 넓히면 된다.
    private const string ResourceRoomDoorScreenId = "BG_07_InvestigationSite_04";
    private const string ResourceRoomDoorHotspotKey = "Hotspot_ResourceRoomdoor";

    // ===== 조사 데이터는 두 파일로 나뉜다 =====
    //   1) Assets/StreamingAssets/Stage/InvestigationStage.csv  (git으로 공유 - 스토리 없음, 배치 도구가 고친다)
    //        컬럼: InvestigationId,HotspotKey,Type,Sprite
    //        - 화면에 어떤 오브젝트가 있는지와 그 종류, 그림. 이 파일에 있는 것만 화면에 나타난다.
    //        - HotspotKey : 오브젝트를 구분하는 이름. 조사기록(NoteEntries.csv)에서 이 이름으로 가리킨다.
    //        - Type       : Item(획득) / Description(설명만) / Talk(말 걸기) / Standing·Prop(장식)
    //        - Sprite     : 배경 위에 올릴 그림 파일 이름
    //        - 특수 키 Background  : 이 조사 화면의 배경 그림 (Sprite 칸에 배경 파일 이름)
    //        - 특수 키 NextScreen/PrevScreen : 옆 조사 화면으로 가는 화살표가 가리킬
    //          InvestigationId (Sprite 칸에 적는다). 예) 재훈의 책상 화면에 NextScreen=회의실 ID,
    //          회의실 화면에 PrevScreen=책상 ID를 적으면 두 화면을 화살표로 오갈 수 있다.
    //   2) Assets/Resources/Dialogues/InvestigationData.csv       (드라이브로 공유 - 스토리, 작가가 고친다)
    //        컬럼: InvestigationId,HotspotKey,ObjectName,Speaker,Text,ItemId,RequiredItemId,
    //             RequiredItemMissingText,AfterItemId,AfterTargetScreenId
    //        - ObjectName : 플레이어에게 보이는 이름 (예: "메모장")
    //        - Speaker    : Talk일 때 말하는 사람
    //        - Text       : 조사했을 때 나오는 문구 / 대사
    //        - ItemId     : Type=Item일 때 얻는 아이템
    //        - RequiredItemId          : 비워두면 항상 조사 가능. 적어두면 그 itemId를 가방에
    //          먼저 얻어야만 평소 반응(Text/대사)이 나온다. 아직 못 얻었으면 아래
    //          RequiredItemMissingText만 보여주고 아이템 획득/수첩 기록은 건너뛴다.
    //          예) 자료실 문(OBJ_07_ResourceRoomdoor)에 RequiredItemId=archive_key를 적어두면,
    //          자료실 열쇠를 얻기 전엔 문을 조사해도 열리지 않고 안내문만 나온다.
    //        - RequiredItemMissingText : RequiredItemId를 아직 못 얻었을 때 보여줄 문구.
    //        - AfterItemId/AfterTargetScreenId : 선택지를 통해 이 itemId를 이미 얻었으면,
    //          평소 반응(설명/대사/선택지) 대신 곧바로 AfterTargetScreenId 화면으로 넘어간다.
    //          "선택지 딸린 문을 한 번 통과하면 다음부턴 안 물어보고 바로 다음 방으로" 같은
    //          용도. AfterItemId는 InventoryTalkChoices.csv의 ItemId 칸으로 얻게 해두면 된다.
    //          예) 자료실 문에서 "알리지 않는다"를 고르면 resource_room_entered를 얻고,
    //          AfterItemId=resource_room_entered / AfterTargetScreenId=BG_07_InvestigationSite_05로
    //          적어두면 그 다음부터 문을 눌렀을 때 선택지 없이 바로 그 화면으로 이동한다.
    //        - 특수 키 IntroText : 조사를 시작할 때 대화창에 띄울 안내문 (Text 칸)
    // 두 파일은 InvestigationId + HotspotKey로 짝을 맞춘다.
    //
    // ===== 왜 나눴나? =====
    // 이 저장소는 공개라서 대사가 든 2번은 git에 올릴 수 없다. 예전에는 두 내용이 한 파일에 섞여
    // 있어서, 배치 도구로 오브젝트를 넣고 뺄 때마다 그 파일을 드라이브에 다시 올려야 했다.
    // 구성(1번)을 떼어낸 덕분에 배치 도구 작업은 git으로 전달되고, 드라이브는 대사를 고칠 때만 쓴다.
    //
    // (1번 파일이 없으면 드라이브의 예전 InvestigationData.csv 한 파일에 전부 적혀 있다고 보고 읽는다)
    private const string InvestigationStageCsv = "Stage/InvestigationStage";
    private const string InvestigationDataCsv = "Dialogues/InvestigationData";

    // ===== 조사 오브젝트의 선택지 =====
    // 대부분의 Talk/Description 오브젝트는 문장 한 줄 보여주고 끝이지만, 몇몇은 "누가
    // 시켰냐"(Talk, 예: OBJ_07_Manager) 또는 "알릴까 말까"(Description, 예: 자료실 문)
    // 처럼 선택지로 답해야 한다. 그 선택지 데이터만 따로 여기 담는다(InvestigationId +
    // HotspotKey로 InvestigationData.csv와 짝을 맞춘다). 컬럼:
    //   InvestigationId,HotspotKey,ChoiceText,ResponseSpeaker,ResponseText,ItemId,TargetEnding
    //   - ChoiceText     : 선택지 버튼 문구
    //   - ResponseSpeaker/ResponseText : 이 보기를 고르면 나올 대답 (비우면 대답 없이 바로 닫힘)
    //   - ItemId         : 이 보기를 고르면 얻는 아이템 (비우면 안 얻음)
    //   - TargetEnding   : 비어있지 않으면 이 보기를 고르는 즉시 그 엔딩으로 직행
    // 같은 InvestigationId+HotspotKey로 여러 줄을 적으면 그 줄 순서대로 선택지 버튼이 뜬다.
    private const string InvestigationTalkChoicesCsv = "Dialogues/InvestigationTalkChoices";

    private class HotspotData
    {
        public string key;
        public HotspotType type;
        public string objectName;
        public string speaker;
        public string text;
        public string itemId;
        public string spriteName;
        public List<InvestigationTalkChoice> talkChoices;
        public string requiredItemId;
        public string requiredItemMissingText;
        public string afterItemId;
        public string afterTargetScreenId;
    }

    // 조사 화면 하나에 대한 정보
    private class ScreenData
    {
        public string backgroundName;
        public string introText;
        // ===== 화면 이동(다음/이전) =====
        // 한 조사(예: #07 회사 조사)가 여러 화면(재훈의 책상 ↔ 회의실)으로 나뉘어 있을 때,
        // "조사 그만하기"로 완전히 나가지 않고도 화면끼리 오갈 수 있게 하는 연결 정보다.
        // CSV에 HotspotKey="NextScreen"/"PrevScreen" 특수 줄로 적으며, Sprite 칸에
        // 이동할 대상 InvestigationId를 적는다(Background 특수 키와 같은 방식).
        // 비어 있으면(연결이 없으면) 해당 방향 화살표를 만들지 않는다.
        public string nextScreenId;
        public string prevScreenId;

        // ===== 이 화면에 들어갈 때 거는 미니게임 =====
        // CSV에 HotspotKey="Minigame" 특수 줄로 적는다(IntroText와 같은 자리, 즉 대사 파일
        // InvestigationData.csv 쪽이다). Text 칸에 미니게임 안내 문구, ItemId 칸에 이
        // 화면에 들어가기 전 이미 가지고 있어야 할 아이템(비워두면 조건 없이 항상 뜬다,
        // 여러 개면 "|"로 구분)을 적는다. 예) 자료실 열쇠(archive_key)를 얻기 전에도
        // 화살표를 타고 "자료실 앞" 화면에 먼저 도착할 수 있으므로, 열쇠를 이미 가지고
        // 있을 때만 미니게임이 뜨게 하려면 ItemId 칸에 archive_key를 적어둔다.
        // 한 번 성공하면 그 화면을 나중에 다시 들어와도 다시 뜨지 않는다(InventoryManager에
        // 통과 표시를 남겨둔다 - EnterScreen() 참고). 실패 시 어떤 엔딩으로 보낼지는 아직
        // 정해지지 않았다 - 지금 MinigameController는 항상 성공하는 스텁이라 실패 경로
        // 자체가 없다. 나중에 진짜 실패 조건이 생기면 그때 컬럼을 추가하면 된다.
        public string minigameLabel;
        public List<string> minigameRequiredItemIds;

        // ===== 특정 아이템을 다 모으면 자동으로 다른 화면으로 돌아간다 =====
        // CSV에 HotspotKey="AutoExit" 특수 줄로 적는다. ItemId 칸에 필요한 아이템들을 "|"로
        // 구분해 적고, AfterTargetScreenId 칸에 돌아갈 화면을 적는다. 이 화면에서 아이템을
        // 얻을 때마다(Inspect() 참고) 검사해서, 전부 모인 순간 자동으로 이동한다.
        // 예) 자료실(_05)에서 단서 세 개를 전부 읽으면 자료실 앞(_04)으로 자동으로 돌아간다.
        public List<string> autoExitRequiredItemIds;
        public string autoExitTargetScreenId;

        // ===== NextScreen 화살표가 보이는 조건 =====
        // CSV에 HotspotKey="NextScreenRequires" 특수 줄로 적는다. ItemId 칸에 "|"로 구분해
        // 적은 아이템을 전부 가지고 있어야만 nextScreenId로 가는 화살표가 보인다(비어있으면
        // 기존과 같이 조건 없이 항상 보인다). 예) 자료실 단서 세 개를 다 모으기 전에는
        // 자료실 앞(_04)에 사장실 앞(_06)으로 가는 화살표가 보이지 않는다.
        public List<string> nextScreenRequiredItemIds;

        // ===== 이 화면에 "도착하는 것" 자체가 지금 조사를 끝내는 신호일 때 =====
        // CSV에 HotspotKey="AutoFinish" 특수 줄로 적는다. ItemId 칸에 "|"로 구분해 적은
        // 아이템을 이미 전부 가지고 있는 채로 이 화면에 도착하면(화살표/AfterTargetScreenId
        // 등 화면 안에서의 이동으로 도착한 경우만 - NavigateToLinkedScreen() 참고), 이
        // 화면을 보여주지 않고 곧장 조사를 끝낸다(Exit()). CSV가 Investigate로 이 화면을
        // 새로 열 때(Enter())는 검사하지 않는다 - 그래야 그 화면 자체의 미니게임 등이
        // 정상적으로 뜬다.
        // 예) 자료실 열쇠(archive_key)를 가진 채로 자료실 앞(_04)에 도착하면, #01/#02에서
        // 이어지던 조사가 거기서 끝나고 CSV가 새로 Investigate(_04)를 걸어 본격적인
        // "자료실에 들키지 않고 진입하라" 흐름을 시작한다.
        // hasAutoFinish : "AutoFinish" 특수 줄이 이 화면에 있는지. ItemId 칸을 비워두면
        //   (autoFinishRequiredItemIds가 null이면) 조건 없이 도착하는 즉시 끝난다 - 예)
        //   사장실 문을 통해서만 올 수 있는 사장실(_07)처럼, 도착 경로 자체가 이미 조건인 화면.
        public bool hasAutoFinish;
        public List<string> autoFinishRequiredItemIds;

        public readonly List<HotspotData> hotspots = new List<HotspotData>();
    }

    // InvestigationId -> 화면 정보
    private Dictionary<string, ScreenData> screenData;

    // ===== 지금 진행 중인 조사에서 오간 화면들 =====
    // NextScreen/PrevScreen 화살표로 여러 화면을 옮겨 다닐 수 있으므로, "조사 그만하기"를
    // 누른 시점에 마지막으로 있던 화면 하나만 수첩에 "조사 완료"로 남기면 나머지 화면은
    // 조사를 다 했어도 기록이 안 남는다. 그래서 Enter()부터 지금까지 거쳐 간 화면
    // InvestigationId를 전부 모아뒀다가 Exit()에서 한 번에 전부 기록한다.
    private readonly HashSet<string> visitedScreenIds = new HashSet<string>();

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

        var stageRows = CSVReader.Read(InvestigationStageCsv);
        var textRows = CSVReader.Read(InvestigationDataCsv);
        var talkChoicesByKey = LoadTalkChoices();

        // 구성 파일이 없으면 예전 형식(한 파일에 구성과 대사가 같이 있음)으로 읽는다.
        if (stageRows == null || stageRows.Count == 0)
        {
            if (textRows == null || textRows.Count == 0)
            {
                Debug.LogWarning($"[InvestigationController] {InvestigationStageCsv}.csv와 {InvestigationDataCsv}.csv를 모두 읽지 못했습니다.");
                return;
            }
            Debug.LogWarning($"[InvestigationController] {InvestigationStageCsv}.csv가 없어 예전 형식({InvestigationDataCsv}.csv 한 파일)으로 읽습니다.");
            BuildScreens(textRows, null, talkChoicesByKey);
            return;
        }

        // 대사/이름 표: "InvestigationId|HotspotKey" -> 그 줄. 같은 키가 두 번 있으면 뒤에 적힌 줄을 쓴다(예전과 같다).
        var texts = new Dictionary<string, Dictionary<string, object>>();
        if (textRows != null)
        {
            foreach (var row in textRows)
            {
                string id = GetField(row, "InvestigationId").Trim();
                string key = GetField(row, "HotspotKey").Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key)) continue;
                texts[TextKey(id, key)] = row;
            }
        }

        BuildScreens(stageRows, texts, talkChoicesByKey);

        // 안내문(IntroText)은 대사 파일에만 있다. 구성 파일에 없는 화면의 안내문은 쓸 곳이 없으므로 알린다.
        // 구성 파일에 없는데 대사만 적힌 오브젝트도 화면에 나타나지 않으므로 함께 알린다.
        var orphans = new List<string>();
        if (textRows != null)
        {
            foreach (var row in textRows)
            {
                string id = GetField(row, "InvestigationId").Trim();
                string key = GetField(row, "HotspotKey").Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key)) continue;

                screenData.TryGetValue(id, out ScreenData screen);

                if (key == "IntroText")
                {
                    if (screen != null) screen.introText = GetField(row, "Text");
                    else orphans.Add($"{id}/{key}");
                    continue;
                }

                // ===== 특수 줄 3종: Minigame / AutoExit / NextScreenRequires =====
                // ScreenData 필드 선언부의 설명 참고. 전부 IntroText와 같이 대사 파일에만
                // 있고, 배치 도구가 만지는 InvestigationStage.csv에는 없다.
                if (key == "Minigame")
                {
                    if (screen != null)
                    {
                        screen.minigameLabel = GetField(row, "Text");
                        screen.minigameRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                    }
                    else orphans.Add($"{id}/{key}");
                    continue;
                }
                if (key == "AutoExit")
                {
                    if (screen != null)
                    {
                        screen.autoExitRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                        screen.autoExitTargetScreenId = GetField(row, "AfterTargetScreenId").Trim();
                    }
                    else orphans.Add($"{id}/{key}");
                    continue;
                }
                if (key == "NextScreenRequires")
                {
                    if (screen != null) screen.nextScreenRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                    else orphans.Add($"{id}/{key}");
                    continue;
                }
                if (key == "AutoFinish")
                {
                    if (screen != null)
                    {
                        screen.hasAutoFinish = true;
                        screen.autoFinishRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                    }
                    else orphans.Add($"{id}/{key}");
                    continue;
                }

                // 예전 형식 파일이 드라이브에 남아 있으면 이런 구성 줄이 섞여 있을 수 있다. 구성 파일이 우선이므로 무시한다.
                if (key == "Background" || key == "NextScreen" || key == "PrevScreen") continue;

                if (screen == null || !screen.hotspots.Exists(h => h.key == key)) orphans.Add($"{id}/{key}");
            }
        }
        if (orphans.Count > 0)
        {
            Debug.LogWarning($"[InvestigationController] {InvestigationDataCsv}.csv에만 있고 {InvestigationStageCsv}.csv에는 없는 줄 {orphans.Count}개는 " +
                             "화면에 나타나지 않습니다: " + string.Join(", ", orphans.GetRange(0, Mathf.Min(8, orphans.Count))) +
                             (orphans.Count > 8 ? " ..." : ""));
        }
    }

    // 두 파일의 줄을 짝지을 때 쓰는 열쇠. InvestigationId와 HotspotKey에는 세로줄(|)이 들어가지 않는다.
    private static string TextKey(string id, string key)
    {
        return id + "|" + key;
    }

    // 구성 줄들로 조사 화면을 만든다.
    //   texts == null : 예전 형식. 한 줄에 구성과 대사/이름이 같이 적혀 있다.
    //   texts != null : 새 형식. 대사/이름은 InvestigationId + HotspotKey로 대사 파일에서 찾는다.
    //   talkChoicesByKey : InvestigationTalkChoices.csv를 TextKey(id,key)로 묶어둔 것. 없으면(null) 아무 Talk에도 선택지가 안 붙는다.
    private void BuildScreens(List<Dictionary<string, object>> rows, Dictionary<string, Dictionary<string, object>> texts,
        Dictionary<string, List<InvestigationTalkChoice>> talkChoicesByKey = null)
    {
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

            // 특수 키 1: 조사 시작 안내문 (예전 형식에서만 이 줄에 있다. 새 형식은 대사 파일에서 읽는다)
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

            // 특수 키 3-4: 옆 화면으로 이동하는 화살표가 가리킬 대상
            // (Background와 같은 방식: Sprite 칸에 적고, 비어있으면 Text 칸도 확인한다)
            if (key == "NextScreen")
            {
                string next = GetField(row, "Sprite").Trim();
                if (string.IsNullOrEmpty(next)) next = GetField(row, "Text").Trim();
                screen.nextScreenId = next;
                continue;
            }
            if (key == "PrevScreen")
            {
                string prev = GetField(row, "Sprite").Trim();
                if (string.IsNullOrEmpty(prev)) prev = GetField(row, "Text").Trim();
                screen.prevScreenId = prev;
                continue;
            }

            // 특수 키 5-7: Minigame / AutoExit / NextScreenRequires (예전 형식 - 이 줄 자신에서 읽는다)
            if (key == "Minigame")
            {
                screen.minigameLabel = GetField(row, "Text");
                screen.minigameRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                continue;
            }
            if (key == "AutoExit")
            {
                screen.autoExitRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                screen.autoExitTargetScreenId = GetField(row, "AfterTargetScreenId").Trim();
                continue;
            }
            if (key == "NextScreenRequires")
            {
                screen.nextScreenRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                continue;
            }
            if (key == "AutoFinish")
            {
                screen.hasAutoFinish = true;
                screen.autoFinishRequiredItemIds = ParseItemList(GetField(row, "ItemId"));
                continue;
            }

            // 일반 조사 오브젝트
            // Standing/Prop은 "보이기만 하고 조사는 안 되는" 장식이다 (HotspotType 주석 참고).
            if (!Enum.TryParse(GetField(row, "Type"), true, out HotspotType type))
            {
                Debug.LogWarning($"[InvestigationController] '{GetField(row, "Type")}'은 조사 타입이 아닙니다. " +
                                 $"Item/Description/Talk(조사 가능) 또는 Standing/Prop(장식) 중에서 적어주세요. " +
                                 $"(InvestigationId={id}, HotspotKey={key})");
                continue;
            }

            string spriteName = GetField(row, "Sprite").Trim();

            // 대사/이름을 어디서 가져올지: 새 형식은 대사 파일의 같은 키 줄, 예전 형식은 이 줄 자신.
            Dictionary<string, object> textRow = row;
            string objectName;
            if (texts != null)
            {
                texts.TryGetValue(TextKey(id, key), out textRow);   // 없으면 null -> GetField가 ""를 돌려준다
                objectName = GetField(textRow, "ObjectName");
                // 배치 도구로 막 추가해서 아직 대사 파일에 이름을 안 적은 오브젝트는 그림 이름으로 대신한다
                // (예전에는 배치 도구가 ObjectName 칸에 그림 이름을 넣어줬던 것과 같은 결과).
                if (string.IsNullOrWhiteSpace(objectName)) objectName = spriteName;
            }
            else
            {
                objectName = GetField(row, "ObjectName");
            }

            List<InvestigationTalkChoice> talkChoices = null;
            talkChoicesByKey?.TryGetValue(TextKey(id, key), out talkChoices);

            screen.hotspots.Add(new HotspotData
            {
                key = key,
                type = type,
                objectName = objectName,
                speaker = GetField(textRow, "Speaker"),
                text = GetField(textRow, "Text"),
                itemId = GetField(textRow, "ItemId"),
                spriteName = spriteName,
                talkChoices = talkChoices,
                requiredItemId = GetField(textRow, "RequiredItemId"),
                requiredItemMissingText = GetField(textRow, "RequiredItemMissingText"),
                afterItemId = GetField(textRow, "AfterItemId"),
                afterTargetScreenId = GetField(textRow, "AfterTargetScreenId")
            });
        }
    }

    // Resources/Dialogues/InvestigationTalkChoices.csv를 읽어 TextKey(InvestigationId,HotspotKey)
    // 별로 묶는다. 파일이 없거나 비어 있으면 빈 표를 돌려준다(선택지가 있는 Talk 오브젝트가
    // 없다는 뜻이므로 게임 진행에는 지장이 없다 - InvestigationController.Enter()와 같은 방침).
    private Dictionary<string, List<InvestigationTalkChoice>> LoadTalkChoices()
    {
        var result = new Dictionary<string, List<InvestigationTalkChoice>>();

        var rows = CSVReader.Read(InvestigationTalkChoicesCsv);
        if (rows == null) return result;

        foreach (var row in rows)
        {
            string id = GetField(row, "InvestigationId").Trim();
            string key = GetField(row, "HotspotKey").Trim();
            string choiceText = GetField(row, "ChoiceText").Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(choiceText)) continue;

            if (!Enum.TryParse(GetField(row, "TargetEnding").Trim(), true, out EndingType ending))
            {
                ending = EndingType.None;
            }

            string listKey = TextKey(id, key);
            if (!result.TryGetValue(listKey, out var list))
            {
                list = new List<InvestigationTalkChoice>();
                result[listKey] = list;
            }

            list.Add(new InvestigationTalkChoice
            {
                choiceText = choiceText,
                responseSpeaker = GetField(row, "ResponseSpeaker"),
                responseText = GetField(row, "ResponseText"),
                itemId = GetField(row, "ItemId"),
                targetEnding = ending
            });
        }

        return result;
    }

    private string GetField(Dictionary<string, object> row, string column)
    {
        return row != null && row.TryGetValue(column, out var value) ? value.ToString() : "";
    }

    // "resource_clue_1|resource_clue_2" 처럼 "|"로 구분해 적은 아이템 목록을 나눈다.
    // 빈 칸이면 null을 돌려준다(= 조건 없음, HasAllItems()가 이걸 "항상 통과"로 취급한다).
    private static List<string> ParseItemList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var result = new List<string>();
        foreach (var part in raw.Split('|'))
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed)) result.Add(trimmed);
        }
        return result.Count > 0 ? result : null;
    }

    // itemIds에 적힌 아이템을 전부 가지고 있는지. itemIds가 비어있으면(조건이 없으면) 항상 true.
    private static bool HasAllItems(List<string> itemIds)
    {
        if (itemIds == null || itemIds.Count == 0) return true;
        if (InventoryManager.Instance == null) return false;

        foreach (var itemId in itemIds)
        {
            if (!InventoryManager.Instance.HasItem(itemId)) return false;
        }
        return true;
    }

    // 화면 하나의 미니게임을 통과했다는 표시로 쓰는 가짜 아이템 id.
    // 인벤토리 슬롯 UI에는 대응하는 슬롯이 없어 화면에 보이지 않는다(resource_room_entered와
    // 같은 용도 - ScreenData.minigameLabel 주석 참고).
    private static string ScreenMinigamePassedFlag(string screenId) => "mg_passed_" + screenId;

    // AutoFinish가 이 화면에서 이미 한 번 조사를 끝냈다는 표시. 이게 없으면, 예를 들어
    // 열쇠를 든 채 자료실 앞(_04)에서 PrevScreen으로 되돌아갔다가 NextScreen으로 다시
    // 들어올 때마다("자료실 앞"을 여러 번 왔다갔다) 매번 조사가 끝나버린다 - 열쇠를 계속
    // 가지고 있으니 조건은 늘 참이기 때문이다. 한 번 쓰이면 다시는 발동하지 않게 막는다.
    private static string AutoFinishUsedFlag(string screenId) => "af_used_" + screenId;

    // ===== "조사 그만하기" 버튼을 만들지 않는 화면들 =====
    // #07(회사 잠입 조사)은 전부 화살표/자동이동/미니게임으로 쭉 이어지는 하나의 흐름이라,
    // 중간에 버튼으로 마음대로 빠져나갈 수 있으면 안 된다. 그래서 버튼을 아예 만들지 않고,
    // 대신 각 구간마다 정해둔 "도착/완료 조건"이 되면 자동으로 끝난다:
    //   - #01(내 책상)/#02(사무실)/#03(자료실 앞 복도): 자료실 열쇠(archive_key)를 든 채
    //     자료실 앞(_04)에 도착하는 순간 자동으로 끝난다 (ScreenData.autoFinishRequiredItemIds).
    //   - #04(자료실 앞)~#07(사장실): 사장실 트로피(AfterTargetScreenId=EXIT)를 눌러야
    //     끝난다 (Inspect() 참고).
    private static readonly HashSet<string> ScreensWithoutExitButton = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BG_07_InvestigationSite_01",
        "BG_07_InvestigationSite_02",
        "BG_07_InvestigationSite_03",
        "BG_07_InvestigationSite_04",
        "BG_07_InvestigationSite_05",
        "BG_07_InvestigationSite_06",
        "BG_07_InvestigationSite_07",
    };

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
                             "StreamingAssets/Stage/InvestigationStage.csv의 InvestigationId를 확인하세요.");
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

        // ===== #07부터는 수첩이 더 이상 갱신되지 않는다 =====
        // #07(회사 잠입 조사)은 시나리오의 마지막 이야기 챕터라 그 뒤로 수첩을 다시 볼
        // 장면이 없다. 조사 화면 id는 배경 파일 이름과 같아서 "BG_07_"로 시작하는 화면에
        // 들어오는 순간이 곧 "#07에 들어왔다"는 뜻이다 - DialogueSystem.LoadDialogueFromCSV()의
        // scenario_07 CSV 훅과 같은 목적이지만, scenario_07.csv가 아직 없어도(테스트용
        // DialogueData 에셋으로 곧장 들어오는 경우 등) 확실히 걸리도록 여기서도 한 번 더 끈다.
        // NoteManager.SetRealtimeUpdate(false)를 걸면 이후 조사/아이템 획득으로 쌓이는 메모는
        // 전부 보류함(deferred)에만 쌓이고 수첩에는 나타나지 않는다 - 이 챕터는 끝난 뒤에도
        // 일부러 FlushDeferredEntries()를 부르지 않으므로 계속 안 보인다.
        if (activeScreenId.StartsWith("BG_07", StringComparison.OrdinalIgnoreCase) && NoteManager.Instance != null)
        {
            NoteManager.Instance.SetRealtimeUpdate(false);
        }

        // 이번 조사에서 거쳐 간 화면을 새로 센다 (화면 이동 중 조사 완료 기록용).
        visitedScreenIds.Clear();
        visitedScreenIds.Add(activeScreenId);

        EnterScreen(activeScreenId, screen);
    }

    // ===== 화면 하나를 실제로 연다 (Enter()/NavigateToLinkedScreen() 공용) =====
    // 배경/오브젝트를 먼저 평소처럼 띄운 뒤(ShowScreenContent), 그 화면에 미니게임이
    // 걸려 있으면(screen.minigameLabel) 그 위에 미니게임 패널을 겹쳐 띄운다. 즉 "화살표를
    // 누르는 순간"이 아니라 "새 배경이 실제로 화면에 나타난 뒤"에 미니게임이 뜬다.
    // 조건 아이템(minigameRequiredItemIds)을 아직 안 가지고 있으면(예: 자료실 열쇠를 얻기
    // 전에 화살표로 자료실 앞에 먼저 온 경우) 미니게임 없이 화면만 연다 - 문을 눌러보면
    // RequiredItemId 안내문으로 자연스럽게 막힌다.
    private void EnterScreen(string screenId, ScreenData screen)
    {
        ShowScreenContent(screen);

        if (string.IsNullOrEmpty(screen.minigameLabel) || !HasAllItems(screen.minigameRequiredItemIds)) return;

        string passedFlag = ScreenMinigamePassedFlag(screenId);
        bool alreadyPassed = InventoryManager.Instance != null && InventoryManager.Instance.HasItem(passedFlag);
        if (alreadyPassed) return;

        if (MinigameController.Instance == null)
        {
            Debug.LogWarning($"[InvestigationController] MinigameController가 없어 '{screen.minigameLabel}' 미니게임을 건너뜁니다.");
            return;
        }

        MinigameController.Instance.StartMinigame(
            screen.minigameLabel,
            onSuccessCallback: () => InventoryManager.Instance?.AddItem(passedFlag),
            // 지금은 항상 성공하는 스텁이라 실패 경로가 없다 (MinigameController.cs 상단 주석 참고).
            onFailCallback: null);
    }

    // 배경을 깔고 조사 오브젝트를 올리고 안내문을 띄운다. (Enter()에 있던 원래 로직)
    private void ShowScreenContent(ScreenData screen)
    {
        // 1) 배경을 깐다. 대사 장면과 같은 배경 시스템을 그대로 쓰므로,
        //    조사 중에도 캐릭터 스탠딩이 필요하면 그대로 남길 수 있다.
        if (StageController.Instance != null)
        {
            // ===== 조사 화면과 대화 장면은 서로 구분된다 =====
            // 조사 화면에 놓일 그림은 InvestigationData.csv가 전부 정한다(오브젝트, 그리고
            // Type=Standing/Prop인 장식). 그래서 들어오기 전 대화 장면에서 올려둔 소품은
            // 여기까지 따라오면 안 된다 - 안 치우면 엉뚱한 소품이 조사 화면에 겹쳐 보인다.
            StageController.Instance.ClearProps();

            if (!string.IsNullOrEmpty(screen.backgroundName))
            {
                StageController.Instance.ApplyBackground(screen.backgroundName);
            }
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

        // 이 조사에서 거쳐 간 화면을 전부 마쳤다는 사실을 조사기록(수첩)에 남긴다.
        // (NextScreen/PrevScreen 화살표로 여러 화면을 오갔을 수 있으므로 activeScreenId
        // 하나만이 아니라 visitedScreenIds 전체를 기록한다.)
        if (NoteManager.Instance != null)
        {
            foreach (var id in visitedScreenIds)
            {
                NoteManager.Instance.OnInvestigationFinished(id);
            }
        }
        visitedScreenIds.Clear();
        activeScreenId = null;

        // 콜백을 지역 변수로 옮긴 뒤 비우고 호출한다. 콜백(ShowNextSentence) 안에서 다시
        // Enter()가 불릴 수 있으므로(다음 줄이 또 조사인 경우) 이전 콜백이 남아있으면 안 된다.
        Action callback = onExitCallback;
        onExitCallback = null;
        callback?.Invoke();
    }

    // ===== 엔딩이 조사 도중에 갑자기 끼어들 때 (예: 미니게임 2 타임어택 시간 초과) =====
    // 일반 Exit()과 거의 같지만 onExitCallback을 부르지 않는다는 점이 다르다. Exit()의
    // 콜백은 "조사를 마쳤으니 CSV의 다음 줄로 이어가라"는 뜻인데, 지금은 대사 흐름 자체를
    // 통째로 버리고 엔딩 CSV로 갈아타는 상황이라 그 콜백을 부르면 안 된다(엔딩 진행 중에
    // 원래 대사가 몰래 한 줄 더 나가버리는 사고가 생긴다). GameFlowManager.TriggerEnding()이
    // 부른다.
    public void ForceExit()
    {
        if (!inSession) return;

        ClearHotspots();

        inSession = false;
        IsShowingTalkLine = false;
        activeScreenId = null;
        visitedScreenIds.Clear();
        onExitCallback = null;
        pendingTalkChoices = null;
        pendingTalkChoiceSource = null;

        SetDialogueVisible(true);
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

        // 그리는 순서: 배경/스탠딩보다는 앞, 암전 판(FadeOverlay)·대화창보다는 뒤.
        PlaceAboveStage(hotspotRoot.transform);

        foreach (var data in screen.hotspots)
        {
            CreateHotspot(data);
        }

        // ===== "조사 그만하기" 버튼을 만들지 않는 화면 =====
        // #07의 자료실 앞(_03)부터 사장실(_07)까지는 화살표/자동이동/미니게임으로 쭉 이어지는
        // 하나의 흐름이라, 중간에 버튼으로 마음대로 빠져나갈 수 있으면 안 된다. 이 구간은
        // 사장실 트로피(EXIT 오브젝트)를 눌러야만 조사가 끝난다 (Inspect() 참고).
        // #01(내 책상)/#02(사무실)처럼 자유롭게 둘러보는 화면은 그대로 버튼을 만든다.
        if (!ScreensWithoutExitButton.Contains(activeScreenId ?? ""))
        {
            CreateExitButton();
        }

        // 옆 화면(재훈의 책상 ↔ 회의실 같은 연결)으로 이동하는 화살표.
        // CSV에 NextScreen/PrevScreen이 적혀 있는 화면에서만 만들어진다.
        // nextScreenRequiredItemIds가 적혀 있으면(NextScreenRequires 특수 줄), 그 아이템을
        // 전부 가지고 있을 때만 "다음" 화살표가 보인다 - 예) 자료실 단서를 다 모으기 전에는
        // 자료실 앞에 사장실 앞으로 가는 화살표가 보이지 않는다.
        if (!string.IsNullOrEmpty(screen.nextScreenId) && HasAllItems(screen.nextScreenRequiredItemIds))
        {
            CreateNavArrow(screen.nextScreenId, isNext: true);
        }
        if (!string.IsNullOrEmpty(screen.prevScreenId))
        {
            CreateNavArrow(screen.prevScreenId, isNext: false);
        }
    }

    // ---------------------------------------------------------------------------------
    // 화면 이동 (연결된 옆 조사 화면으로 - "조사 그만하기"가 아니다)
    // ---------------------------------------------------------------------------------
    // ===== Enter()와 다른 점 =====
    // Enter()는 CSV의 Investigate 줄에서 새 조사를 "시작"할 때 쓰고, onExitCallback을
    // 새로 받는다. 반면 이 함수는 이미 진행 중인 조사 안에서 옆 화면으로 "넘어가기만" 하는
    // 것이므로 inSession/onExitCallback은 그대로 두고 배경과 오브젝트만 바꿔치기한다.
    // 그래서 어느 화면에 있든 "조사 그만하기"를 누르면 처음 Enter()를 부른 CSV 줄의
    // 콜백(ShowNextSentence)이 그대로 이어진다.
    private void NavigateToLinkedScreen(string targetScreenId)
    {
        if (!inSession) return;
        if (string.IsNullOrWhiteSpace(targetScreenId)) return;

        string targetId = targetScreenId.Trim();
        if (!screenData.TryGetValue(targetId, out ScreenData targetScreen))
        {
            Debug.LogWarning($"[InvestigationController] 연결된 조사 화면 '{targetId}'을(를) 찾을 수 없습니다. " +
                             "StreamingAssets/Stage/InvestigationStage.csv의 NextScreen/PrevScreen 값을 확인하세요.");
            return;
        }

        // 이 화면에 도착하는 것 자체가 "지금까지의 조사는 끝났다"는 신호일 수 있다
        // (ScreenData.autoFinishRequiredItemIds 주석 참고). 화면을 보여주지도 않고 곧장
        // 조사를 끝낸다 - Enter()로 새로 시작하는 조사는 이 검사를 거치지 않는다.
        // 딱 한 번만 발동해야 한다 - 열쇠를 계속 들고 있으므로, 그냥 두면 이 화면을
        // 화살표로 다시 들어올 때마다(예: 자료실에 들어가려다 PrevScreen으로 되돌아간 뒤
        // 다시 NextScreen을 누르는 경우) 매번 조사가 끝나버린다.
        if (targetScreen.hasAutoFinish && HasAllItems(targetScreen.autoFinishRequiredItemIds))
        {
            string usedFlag = AutoFinishUsedFlag(targetId);
            bool alreadyUsed = InventoryManager.Instance != null && InventoryManager.Instance.HasItem(usedFlag);
            if (!alreadyUsed)
            {
                InventoryManager.Instance?.AddItem(usedFlag);
                Exit();
                return;
            }
        }

        activeScreenId = targetId;
        visitedScreenIds.Add(activeScreenId);

        EnterScreen(activeScreenId, targetScreen);
    }

    // 지금 화면에 AutoExit 조건(ScreenData.autoExitRequiredItemIds)이 걸려 있고, 그 아이템을
    // 전부 모았으면 autoExitTargetScreenId로 자동으로 돌아간다. Inspect()가 Item 타입 오브젝트를
    // 얻을 때마다 부른다 - 예) 자료실(_05)에서 단서 세 개를 전부 읽으면 자료실 앞(_04)으로.
    private void CheckAutoExit()
    {
        if (string.IsNullOrEmpty(activeScreenId)) return;
        if (!screenData.TryGetValue(activeScreenId, out ScreenData screen)) return;
        if (screen.autoExitRequiredItemIds == null || screen.autoExitRequiredItemIds.Count == 0) return;
        if (string.IsNullOrEmpty(screen.autoExitTargetScreenId)) return;
        if (!HasAllItems(screen.autoExitRequiredItemIds)) return;

        NavigateToLinkedScreen(screen.autoExitTargetScreenId);
    }

    // 옆 화면으로 이동하는 화살표 버튼을 만든다.
    //   isNext == true  : 오른쪽 중앙에 '>' (다음 화면)
    //   isNext == false : 왼쪽 중앙에 '<' (이전 화면)
    private void CreateNavArrow(string targetScreenId, bool isNext)
    {
        var go = new GameObject(isNext ? "Btn_NextScreen" : "Btn_PrevScreen",
            typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(hotspotRoot.transform, false);

        var rt = go.GetComponent<RectTransform>();
        float edgeX = isNext ? 1f : 0f;
        rt.anchorMin = new Vector2(edgeX, 0.5f);
        rt.anchorMax = new Vector2(edgeX, 0.5f);
        rt.pivot = new Vector2(edgeX, 0.5f);
        rt.anchoredPosition = new Vector2(isNext ? -24f : 24f, 0f);
        rt.sizeDelta = new Vector2(64f, 96f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = isNext ? ">" : "<";
        tmp.fontSize = 40;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        go.GetComponent<Button>().onClick.AddListener(() => NavigateToLinkedScreen(targetScreenId));

        // 코드로 만든 글자라 기본 글꼴에는 한글이 없다(여기선 '>' '<' 뿐이라 실제로는
        // 문제 없지만, 다른 버튼들과 같은 방식을 맞춰 둔다).
        UIFontHelper.ApplyToChildren(go);
    }

    // 조사 오브젝트 하나를 만든다.
    private void CreateHotspot(HotspotData data)
    {
        // ===== 장식(Standing/Prop)은 아예 다른 방식으로 만든다 =====
        // 누를 수 없어야 하므로 Button도 InvestigatableObject도 붙이지 않는다.
        if (data.type == HotspotType.Standing || data.type == HotspotType.Prop)
        {
            CreateDecoration(data);
            return;
        }

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
        io.talkChoices = data.talkChoices;
        io.requiredItemId = data.requiredItemId;
        io.requiredItemMissingText = data.requiredItemMissingText;
        io.afterItemId = data.afterItemId;
        io.afterTargetScreenId = data.afterTargetScreenId;

        // 이 오브젝트가 속한 조사 화면 이름. 배치표에서 "이 화면 전용 좌표"를 찾는 데 쓴다
        // (IllustLayout.cs의 [화면별 좌표] 주석 참고).
        io.screenId = activeScreenId;

        var image = go.GetComponent<Image>();

        if (string.IsNullOrEmpty(data.spriteName))
        {
            // 그림이 지정되지 않은 오브젝트(예: 창문처럼 배경에 이미 그려진 것).
            // 눈에 보이는 그림 없이 클릭 영역만 필요한 경우인데, 위치 정보도 없으면
            // 어디를 눌러야 할지 알 수 없으므로 만들지 않고 넘어간다.
            if (!IllustLayout.TryGet(data.key, activeScreenId, out var p))
            {
                Destroy(go);
                return;
            }

            // 배치표에 좌표가 있으면 투명한 클릭 영역으로 둔다.
            image.color = new Color(1f, 1f, 1f, 0f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
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

    // ---------------------------------------------------------------------------------
    // 장식 그림 (조사 화면에 올리는 캐릭터 스탠딩 / 소품)
    // ---------------------------------------------------------------------------------
    // ===== 왜 따로 만드나? =====
    // 조사 화면에도 인물이 서 있어야 하는 장면이 있는데(예: 자료실에 직원이 서 있는 화면),
    // 그 인물은 "조사 대상"이 아니라 그냥 배경의 일부다. 그런데 조사 오브젝트와 똑같이
    // 만들면 Button이 붙어서 눌리고, 무엇보다 인물 그림이 커서 뒤에 있는 진짜 조사
    // 오브젝트들을 덮어 가려버린다.
    //
    // 그래서 장식은:
    //   1) Button / InvestigatableObject를 아예 안 붙이고
    //   2) raycastTarget을 꺼서 클릭이 그대로 통과하게 하고
    //   3) 다른 조사 오브젝트보다 뒤(먼저 그려지는 자리)에 놓는다
    // 이렇게 하면 보이기만 하고 조사 진행을 전혀 방해하지 않는다.
    private void CreateDecoration(HotspotData data)
    {
        if (string.IsNullOrEmpty(data.spriteName))
        {
            Debug.LogWarning($"[InvestigationController] 장식('{data.key}')에 Sprite가 비어 있어 건너뜁니다. " +
                             $"(InvestigationId={activeScreenId})");
            return;
        }

        // 스탠딩은 Standings 폴더에서, 소품(Prop)은 Objects 폴더에서 찾는다.
        Sprite sprite = data.type == HotspotType.Standing
            ? IllustLoader.LoadStanding(data.spriteName)
            : IllustLoader.LoadObject(data.spriteName);

        if (sprite == null) return;   // 경고는 IllustLoader가 이미 남겼다

        var go = new GameObject(data.key, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(hotspotRoot.transform, false);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;

        // ===== 핵심: 클릭이 통과하게 한다 =====
        // raycastTarget을 끄면 이 그림은 마우스 입력을 아예 받지 않는다.
        // 그래서 인물 그림이 조사 오브젝트를 덮고 있어도 그 뒤가 정상적으로 눌린다.
        image.raycastTarget = false;

        // 위치는 조사 오브젝트와 똑같은 규칙으로 배치표에서 찾는다
        // (표정 상속/화면별 좌표 전부 그대로 적용된다 - IllustLayout.cs 참고).
        IllustLayout.Apply(image.rectTransform, sprite, data.spriteName, default, activeScreenId);

        // 장식은 조사 오브젝트보다 뒤에 그린다. hotspotRoot 안에서 맨 앞자리로 보내면
        // 나중에 만들어질 조사 오브젝트들이 그 위에 그려진다.
        go.transform.SetAsFirstSibling();
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
        // ===== 이미 한 번 통과했는지 확인 (선택지를 매번 다시 묻지 않게) =====
        // AfterItemId가 적혀 있고 그 아이템을 이미 얻었다면 - 즉 이 오브젝트의 선택지를
        // 이전에 이미 한 번 골랐다면 - 평소 반응(선택지 포함)을 다시 보여주지 않는다. 아래
        // RequiredItemId 확인보다 먼저 해야 한다: 이 상태에 도달했다는 것 자체가
        // RequiredItemId 조건도 이미 통과했다는 뜻이므로 다시 검사할 필요가 없고, 여기서
        // 먼저 걸러야 안내문이 다시 뜨는 일이 없다.
        //   AfterTargetScreenId까지 적혀 있으면 그 화면으로 곧장 넘어간다 (자료실 문처럼
        //   "통과하면 다음 방으로 이동"하는 오브젝트 - 기존과 동일하게 동작한다).
        //   AfterTargetScreenId가 비어 있으면 넘어갈 화면이 없다는 뜻이니, 대화창도 띄우지
        //   않고 그냥 조용히 무시한다 - "이미 한 번 이야기를 끝낸 사람"처럼, 더 볼 내용이
        //   없는 Talk/Description 오브젝트에 쓴다 (예: 회의실 사용 대장을 이미 넘겨준 서기).
        if (!string.IsNullOrEmpty(obj.afterItemId) &&
            InventoryManager.Instance != null && InventoryManager.Instance.HasItem(obj.afterItemId.Trim()))
        {
            if (!string.IsNullOrEmpty(obj.afterTargetScreenId))
            {
                NavigateToLinkedScreen(obj.afterTargetScreenId.Trim());
            }
            return;
        }

        // ===== 선행 아이템 확인 =====
        // RequiredItemId가 적혀 있는데 아직 그 아이템을 못 얻었으면, 평소 반응(설명/대사) 대신
        // 안내문 한 줄만 보여주고 끝낸다. 아이템 획득/수첩 기록/자료 뷰어까지 전부 건너뛰어야
        // "아직 조사하지 않은 것"과 동일하게 남아, 나중에 열쇠를 얻고 다시 눌렀을 때
        // 정상적으로 처음 조사한 것처럼 동작한다.
        if (!string.IsNullOrEmpty(obj.requiredItemId) &&
            (InventoryManager.Instance == null || !InventoryManager.Instance.HasItem(obj.requiredItemId.Trim())))
        {
            ShowLineInDialogue("", obj.requiredItemMissingText);
            return;
        }

        // ===== 조건 없이 곧장 다음 화면으로 넘어가는 오브젝트 =====
        // afterTargetScreenId만 적혀 있고 afterItemId가 비어 있으면(= 선택지로 얻는 "통과
        // 표시" 없이 바로 이동), 누를 때마다 조건 없이 곧장 그 화면으로 이동한다. 문을 열고
        // 닫는 선택지 없이 "문 = 다음 방으로 가는 통로"인 경우에 쓴다 (예: 사장실 문 -
        // RequiredItemId 게이트는 위에서 이미 통과했다).
        // AfterTargetScreenId 칸에 "EXIT"라고 적으면 다른 조사 화면이 아니라 조사 자체를
        // 끝낸다(Exit() - "조사 그만하기"를 누른 것과 똑같다). 예) 사장실 트로피를 눌러
        // 금고를 발견하면, 다른 조사 화면으로 넘어가는 게 아니라 조사를 마치고 금고를
        // 여는 대사 장면으로 이어간다.
        if (string.IsNullOrEmpty(obj.afterItemId) && !string.IsNullOrEmpty(obj.afterTargetScreenId))
        {
            string target = obj.afterTargetScreenId.Trim();
            if (string.Equals(target, "EXIT", StringComparison.OrdinalIgnoreCase))
            {
                Exit();
            }
            else
            {
                NavigateToLinkedScreen(target);
            }
            return;
        }

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

            // 선택지가 달린 오브젝트(예: OBJ_07_Manager, 자료실 문)는 질문 문장만으로는 아직
            // 확정된 사실이 아니다 - 플레이어가 무엇을 고르느냐에 따라 결과가 갈리므로,
            // 질문 자체를 수첩에 자동으로 옮겨 적지 않는다. (꼭 남겨야 하면 NoteEntries.csv에
            // 직접 써두면 위의 hasWrittenNote로 잡혀 그대로 적힌다.)
            // Talk(대사)뿐 아니라 Description(지문 - 화자 없는 혼잣말/선택 상황)에도 선택지가
            // 붙을 수 있으므로 타입은 보지 않고 talkChoices 존재 여부만 본다.
            bool hasChoices = obj.talkChoices != null && obj.talkChoices.Count > 0;

            if (!hasWrittenNote && !hasChoices)
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

            // 이 화면에 AutoExit 조건이 걸려 있다면(예: 자료실 단서 세 개), 지금 얻은
            // 아이템으로 조건이 다 채워졌는지 확인해서 다 채워졌으면 자동으로 돌아간다.
            CheckAutoExit();
        }

        // 서류/사진처럼 자료 자체를 읽어야 하는 것만 전체화면 뷰어로 펼친다.
        // 그 외에는 전부 대화창에 출력한다.
        if (TryOpenDocumentViewer(obj.itemId)) return;

        // 이 문장 끝에 선택지가 있으면 기억해뒀다가, 문장을 다 읽고 닫는 시점에
        // DismissTalkLine()에서 곧바로 이어서 보여준다 (ShowTalkChoices 참고).
        // Talk든 Description이든 상관없이 talkChoices만 있으면 선택지가 붙는다
        // (예: 자료실 문 - 화자 없는 지문인데도 "알릴까 말까" 선택지가 필요한 경우).
        pendingTalkChoices = (obj.talkChoices != null && obj.talkChoices.Count > 0) ? obj.talkChoices : null;
        pendingTalkChoiceSource = pendingTalkChoices != null ? obj : null;

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

        // 방금 닫은 대사에 선택지가 달려 있었다면, 대화창은 그대로 둔 채 선택지를 이어서 띄운다
        // (Inspect()에서 pendingTalkChoices에 미리 담아둔다).
        if (pendingTalkChoices != null)
        {
            var choices = pendingTalkChoices;
            pendingTalkChoices = null;
            ShowTalkChoices(choices);
            return;
        }

        // 대화창을 닫아서 조사 화면을 가리지 않게 한다.
        // (조사 오브젝트들은 계속 그 자리에 있으므로 바로 다음 것을 누를 수 있다)
        SetDialogueVisible(false);
    }

    // ===== Talk 오브젝트의 선택지 =====
    // DeductionController(추리 파트)와 똑같은 방식: 새 UI를 만들지 않고 DialogueSystem이
    // 이미 갖고 있는 선택지 UI(choicePanel/choiceContainer/choiceButtonPrefab)를 그대로
    // 빌려 쓰고, 여기서는 보기 목록과 "골랐을 때 할 일"만 채운다.
    private void ShowTalkChoices(List<InvestigationTalkChoice> choices)
    {
        var ds = DialogueSystem.Instance;
        if (ds == null || ds.choicePanel == null || ds.choiceContainer == null || ds.choiceButtonPrefab == null)
        {
            Debug.LogWarning("[InvestigationController] 선택지 UI(DialogueSystem.choicePanel 등)를 찾을 수 없어 " +
                             "선택지 없이 대화를 닫습니다.");
            SetDialogueVisible(false);
            return;
        }

        ds.choicePanel.SetActive(true);

        // 이전에 떠 있던 선택지 버튼을 지운다.
        foreach (Transform child in ds.choiceContainer) Destroy(child.gameObject);

        foreach (var choice in choices)
        {
            GameObject btnObj = Instantiate(ds.choiceButtonPrefab, ds.choiceContainer);
            var label = btnObj.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null) label.text = choice.choiceText;

            // 람다 안에서 반복 변수를 그대로 쓰면 마지막 값만 잡히므로 지역 변수로 복사해둔다
            // (DialogueSystem.ShowChoices, DeductionController.ShowCurrentStep과 같은 이유).
            InvestigationTalkChoice captured = choice;
            var button = btnObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnTalkChoiceSelected(captured));
            }
        }
    }

    // 선택지 하나를 골랐을 때.
    private void OnTalkChoiceSelected(InvestigationTalkChoice choice)
    {
        if (DialogueSystem.Instance != null && DialogueSystem.Instance.choicePanel != null)
        {
            DialogueSystem.Instance.choicePanel.SetActive(false);
        }

        // 이 선택지를 내놓은 오브젝트를 지역 변수로 옮겨두고 필드는 비운다 - 이 함수 밖에서
        // 또 참조할 일이 없고, 다음 조사에 낡은 값이 남아있지 않게 하기 위함이다.
        InvestigatableObject sourceObj = pendingTalkChoiceSource;
        pendingTalkChoiceSource = null;

        // 엔딩으로 직행하는 보기라면 대답/아이템은 볼 것도 없이 바로 엔딩으로 넘어간다
        // (Exit()의 자료실 열쇠 미획득 처리와 같은 방식 - InvestigationController.Exit() 참고).
        if (choice.targetEnding != EndingType.None)
        {
            ForceExit();
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.TriggerEnding(choice.targetEnding);
            }
            return;
        }

        // ===== 자료실 문: "알리지 않는다"를 고르는 즉시 타이밍 클릭 미니게임 =====
        // 평소처럼 아이템을 주고 대답 대사를 보여주는 대신, 그 자리에서 곧바로 미니게임을
        // 띄운다. 성공하면 그때 아이템(통과 표시)을 주고 문을 다시 누른 것처럼 자료실로
        // 곧장 이동하고, 실패하면 Bad_C 엔딩으로 보낸다 (ResourceRoomDoorScreenId/
        // ResourceRoomDoorHotspotKey 선언부 주석 참고).
        if (activeScreenId == ResourceRoomDoorScreenId &&
            sourceObj != null && sourceObj.gameObject.name == ResourceRoomDoorHotspotKey)
        {
            StartResourceRoomSneakMinigame(choice, sourceObj);
            return;
        }

        if (!string.IsNullOrWhiteSpace(choice.itemId) && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(choice.itemId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(choice.responseText))
        {
            // 대답도 대사 한 줄이므로 평소 Talk 흐름과 똑같이 보여주고 닫는다.
            ShowLineInDialogue(choice.responseSpeaker, choice.responseText);
        }
        else
        {
            SetDialogueVisible(false);
        }
    }

    // 자료실 문에서 "알리지 않는다"를 골랐을 때 실행하는 타이밍 클릭 미니게임.
    //   성공 -> choice.itemId(통과 표시)를 주고 sourceObj.afterTargetScreenId(=자료실)로 이동.
    //   실패 -> Bad_C 엔딩 (담당 직원에게 알린다를 골랐을 때와 같은 엔딩).
    private void StartResourceRoomSneakMinigame(InvestigationTalkChoice choice, InvestigatableObject sourceObj)
    {
        if (TimingClickMinigameController.Instance == null)
        {
            // 미니게임 컨트롤러가 없으면(예: 씬에 Canvas가 없는 테스트 환경) 게임을 막는
            // 대신 예전처럼 아이템만 주고 곧장 이동시킨다 - MinigameController.EnterScreen()
            // 쪽의 안전장치와 같은 방침.
            Debug.LogWarning("[InvestigationController] TimingClickMinigameController가 없어 자료실 잠입 미니게임을 건너뜁니다.");
            if (!string.IsNullOrWhiteSpace(choice.itemId) && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(choice.itemId.Trim());
            }
            if (!string.IsNullOrEmpty(sourceObj.afterTargetScreenId))
            {
                NavigateToLinkedScreen(sourceObj.afterTargetScreenId.Trim());
            }
            else
            {
                SetDialogueVisible(false);
            }
            return;
        }

        // 대사창을 닫아 미니게임 화면을 가리지 않게 한다 (선택지 창은 이미 위에서 닫았다).
        SetDialogueVisible(false);

        TimingClickMinigameController.Instance.StartGame(
            onSuccessCallback: () =>
            {
                if (!string.IsNullOrWhiteSpace(choice.itemId) && InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(choice.itemId.Trim());
                }
                if (!string.IsNullOrEmpty(sourceObj.afterTargetScreenId))
                {
                    NavigateToLinkedScreen(sourceObj.afterTargetScreenId.Trim());
                }
            },
            onFailCallback: () =>
            {
                ForceExit();
                GameFlowManager.Instance?.TriggerEnding(EndingType.Bad_C);
            });
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

    // ===== 조사 오브젝트를 배경 바로 위, 암전 판(FadeOverlay)보다는 아래에 둔다 =====
    // 예전에는 대화창 바로 앞자리(암전 판보다도 앞)에 두었다. 그런데 조사 직전 줄이
    // IsFadeOut=TRUE인 암전 연출인데, 화면이 아직 다 밝아지기 전에(예: 세이브 창을 닫으며
    // SaveSlotDialog.Close()가 곧장 다음 줄로 넘기는 경우) 조사가 시작되면, 암전 판을
    // 뚫고 오브젝트만 먼저 보이고 배경은 판 뒤에 가려진 채로 남아 "오브젝트는 바로
    // 보이는데 배경만 몇 초 늦게 나타나는" 것처럼 보였다.
    // 오브젝트를 배경·스탠딩과 똑같이 암전 판보다 뒤(=무대 바로 위)에 두면, 암전이 아직
    // 안 걷혔을 땐 오브젝트도 배경과 함께 가려지고, 암전이 걷히는 순간 항상 같이 나타난다.
    private void PlaceAboveStage(Transform target)
    {
        var stage = StageController.Instance;
        if (stage != null)
        {
            int stageTop = stage.GetTopStageSiblingIndex();
            if (stageTop >= 0)
            {
                target.SetSiblingIndex(stageTop + 1);
                return;
            }
        }

        // 무대(배경/스탠딩)를 못 찾으면 예전 방식(대화창 바로 앞자리)으로 대신한다.
        PlaceBehindDialogue(target);
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
