using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// InvestigationController - 조사 데이터(CSV) 읽기
// =====================================================================================
// InvestigationStage.csv / InvestigationData.csv / InvestigationTalkChoices.csv를 읽어
// 화면(ScreenData)과 오브젝트(HotspotData)로 만드는 부분이다 (partial class - 본체는 InvestigationController.cs).
public partial class InvestigationController
{
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

    // 같은 키로 적은 둘째 줄부터의 대사 목록을 합친 줄(row)에 담아두는 임시 칸 이름 (LoadDataIfNeeded 참고).
    private const string FollowUpsKey = "__FollowUps";

    private class HotspotData
    {
        public string key;
        public HotspotType type;
        public string objectName;
        public string speaker;
        public string text;
        public bool longMonologue;   // CSV의 LongMonologue 칸 (큰 글자 화면으로 보여줄지)
        public List<InvestigationFollowUpLine> followUps;   // 같은 키의 둘째 줄부터의 대사들
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
        public bool introLongMonologue;   // 안내문을 큰 글자 화면으로 보여줄지 (CSV의 LongMonologue 칸)
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

        // 조건을 다 채운 순간 곧바로 화면을 넘기지 않고, 먼저 보여줄 마무리 대사(Text 칸,
        // AutoExit 특수 줄에서는 지금까지 쓰지 않던 칸이라 새 컬럼 없이 재사용한다). 비워두면
        // 예전처럼 조건이 채워지자마자 곧장 이동한다. 예) 자료실 단서를 다 모으면
        // "중요한 건 전부 찾은 것 같다. 얼른 빠져나가자." 대사를 먼저 보여준 뒤 이동한다.
        public string autoExitText;

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
        // ===== 같은 키가 여러 줄이면 "첫 줄 + 이어지는 줄들"이다 =====
        // 한 오브젝트를 여러 대사로 말하게 하려면 같은 InvestigationId+HotspotKey로 줄을 더 적는다.
        //   - 첫 줄이 기본 줄이다. ObjectName/ItemId/RequiredItemId/AfterItemId 같은 나머지 칸은
        //     첫 줄에서 읽고, 첫 줄에서 비어 있으면 뒤 줄에서 처음 나오는 값을 쓴다.
        //   - 둘째 줄부터는 Speaker/Text/LongMonologue만 쓰이며, 플레이어가 클릭할 때마다 한 줄씩
        //     이어서 보인다 (InvestigationFollowUpLine). Speaker를 비우면 첫 줄과 같은 화자다.
        //   - 아이템 획득, 수첩 기록은 오브젝트를 처음 누를 때, 선택지는 마지막 줄을 닫은 뒤에 평소처럼 처리된다.
        // (원래는 같은 키가 두 번이면 뒤 줄만 남고 앞 줄은 조용히 버려졌다)
        var texts = new Dictionary<string, Dictionary<string, object>>();
        if (textRows != null)
        {
            foreach (var row in textRows)
            {
                string id = GetField(row, "InvestigationId").Trim();
                string key = GetField(row, "HotspotKey").Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key)) continue;

                string textKey = TextKey(id, key);
                if (!texts.TryGetValue(textKey, out var first))
                {
                    // 원본 표(CSVReader가 돌려준 것)를 건드리지 않도록 복사해서 쓴다.
                    texts[textKey] = new Dictionary<string, object>(row);
                    continue;
                }

                // 둘째 줄부터: 첫 줄에서 비어 있던 칸을 채우고, 대사는 이어서 보여줄 목록에 쌓는다.
                foreach (var kv in row)
                {
                    if (kv.Key == "Speaker" || kv.Key == "Text" || kv.Key == "LongMonologue" || kv.Key == "LongMonologueNewPage") continue;
                    if (string.IsNullOrWhiteSpace(GetField(first, kv.Key)) && !string.IsNullOrWhiteSpace(kv.Value?.ToString()))
                    {
                        first[kv.Key] = kv.Value;
                    }
                }

                if (!first.TryGetValue(FollowUpsKey, out var listObj))
                {
                    listObj = new List<InvestigationFollowUpLine>();
                    first[FollowUpsKey] = listObj;
                }
                ((List<InvestigationFollowUpLine>)listObj).Add(new InvestigationFollowUpLine
                {
                    speaker = GetField(row, "Speaker"),
                    text = GetField(row, "Text"),
                    longMonologue = IsTrue(GetField(row, "LongMonologue")),
                    longMonologueNewPage = IsTrue(GetField(row, "LongMonologueNewPage"))
                });
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
                    if (screen != null)
                    {
                        screen.introText = GetField(row, "Text");
                        screen.introLongMonologue = IsTrue(GetField(row, "LongMonologue"));
                    }
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
                        screen.autoExitText = GetField(row, "Text");
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
                screen.introLongMonologue = IsTrue(GetField(row, "LongMonologue"));
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
                screen.autoExitText = GetField(row, "Text");
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
                longMonologue = IsTrue(GetField(textRow, "LongMonologue")),
                followUps = textRow != null && textRow.TryGetValue(FollowUpsKey, out var followUpObj)
                    ? followUpObj as List<InvestigationFollowUpLine> : null,
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

    // CSV 칸이 TRUE인지 (대소문자, 앞뒤 공백 무시). 칸이 없으면 false.
    private static bool IsTrue(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Trim().ToLowerInvariant() == "true";
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
}
