using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// InvestigationController - 장면별 예외 처리
// =====================================================================================
// 특정 조사 화면에서만 쓰는 규칙을 모아 둔 곳이다 (partial class - 본체는 InvestigationController.cs).
//   자료실 앞 문과 복도 / 자료실 안 책 / 사장실 앞·안 / 사무실 / 경찰서 아이 / 내 책상 서랍·서류
//   / "조사 그만하기" 버튼이 없는 화면 / 조합 힌트
// 장면 하나를 고칠 때는 보통 이 파일만 보면 된다. 공통 흐름에서 이 규칙을 부르는 자리는
// 본체(Inspect, EnterScreen, CreateHotspot 등)에 있다.
public partial class InvestigationController
{
    // ===== 자료실 문: 선택지를 고르는 즉시 타이밍 클릭 미니게임으로 이어지는 유일한 지점 =====
    // 새 CSV 컬럼을 추가하지 않고(팀 규칙) 이 한 곳만을 위한 예외 분기이므로, 다른
    // ScreensWithoutExitButton처럼 화면/오브젝트 이름을 코드에 직접 적어 식별한다.
    // 나중에 다른 곳에도 같은 방식(선택지 -> 타이밍 미니게임)이 필요해지면, 이 두 상수를
    // 목록(배열/HashSet)으로 바꾸고 OnTalkChoiceSelected()의 판정도 그에 맞게 넓히면 된다.
    private const string ResourceRoomDoorScreenId = "BG_07_InvestigationSite_04";
    private const string ResourceRoomDoorHotspotKey = "Hotspot_ResourceRoomdoor";

    // 자료실 안(책장이 늘어선 화면). 책이 24권이라 이름을 일일이 적는 대신
    // 접두사/접미사 규칙으로 가르는 곳이 있어서 상수로 빼 두었다. (IsWrongHotspot 참고)
    private const string ArchiveRoomScreenId = "BG_07_InvestigationSite_05";
    private const string ArchiveBookPrefix = "Hotspot_Documents_";
    private const string ArchiveClueSuffix = "_Clue";

    // 사장실 앞(_06)에 처음 도착하면 "#09 사장실 진입" 저장 창을 띄운다.
    // 화살표로 자료실 앞과 왔다갔다해도 한 번만 뜨도록 표시 아이템을 남긴다.
    private const string CeoRoomFrontScreenId = "BG_07_InvestigationSite_06";
    private const string CeoRoomSaveShownFlag = "ceo_room_save_shown";

    // ===== 자료실 / 사장실 문 여는 소리 =====
    //   자료실: 문 앞 타이밍 클릭 미니게임에 성공했을 때 (StartResourceRoomSneakMinigame).
    //   사장실: 사장실 문(Hotspot_CEORoomDoor)을 눌렀을 때 (Inspect의 EXIT 분기).
    private const string DoorOpenSfx = "Sounds/SFX/OpenDoorSFX";
    private const string CeoRoomDoorHotspotKey = "Hotspot_CEORoomDoor";

    // ===== 사장실 트로피: 비밀번호 힌트를 찾은 뒤 누르면 쇠 떨어지는 소리 =====
    // 힌트(safe_hint_note)가 없으면 안내문만 뜨고 이 분기까지 오지 않으므로 소리도 나지 않는다.
    private const string TrophySfx = "Sounds/SFX/DropTheMetal_SFX";
    private const string CeoRoomInsideScreenId = "BG_07_InvestigationSite_07";
    private const string TrophyHotspotKey = "Hotspot_Trophy";

    // ===== 조사할 때 서류 뷰어를 열지 않고 대사+토스트로만 안내하는 오브젝트 =====
    // (화면ID, HotspotKey) 쌍을 적어둔다. ItemData.csv의 ViewerType은 그대로
    // Document/Photo로 둬야 한다 - 가방에서 "펼쳐보기"로 나중에 다시 읽을 수 있어야
    // 하기 때문이다(InventoryPanelUI.cs가 이 값을 그대로 쓴다). 여기 적힌 오브젝트만
    // "주울 때는" 뷰어를 건너뛰고 Inspect()의 대사(Text 칸) 표시로 대신한다.
    // 자료실 단서 세 개가 전부 이 방식이라, 세 개를 다 읽고 닫아야 자동 이동
    // 조건(CheckAutoExit)이 검사된다(대사 없이 뷰어만 열리면 그 순간 조용히 채워져버려
    // 부자연스럽다).
    //
    // ===== #01 내 책상의 서류도 여기에 넣는다 =====
    // 같은 서류를 #07에서 제대로 조사(뷰어 + 형광펜)할 수 있어서, #01에서는 뷰어를 열지 않고
    // 대사(InvestigationData.csv의 Text 칸)만 보여준다. Description 타입이라 아이템은 얻지 않는다.
    private static readonly HashSet<string> ItemHotspotsSkipAutoViewer = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        TextKey("BG_01_MyDesk", "Hotspot_Documents"),
        TextKey(ArchiveRoomScreenId, "Hotspot_Documents_F01L_01_Clue"),   // 계약서
        TextKey(ArchiveRoomScreenId, "Hotspot_Documents_F01R_07_Clue"),   // 업무기록 2001.07
        TextKey(ArchiveRoomScreenId, "Hotspot_Documents_F01R_08_Clue"),   // 업무기록 2001.08
    };

    // ===== 이미 할 일을 끝낸 오브젝트를 다시 누르면 보여줄 "평소 대사"의 출처 =====
    // 키: 다시 누르는 오브젝트(화면+핫스팟), 값: 대사를 빌려올 줄(화면+핫스팟).
    // 예) #07 서랍은 열쇠를 얻은 뒤에는 #01 내 책상 서랍의 대사를 다시 보여준다.
    private static readonly Dictionary<string, (string screenId, string hotspotKey)> RepeatLineSources =
        new Dictionary<string, (string screenId, string hotspotKey)>(StringComparer.OrdinalIgnoreCase)
    {
        { TextKey("BG_07_InvestigationSite_01", "Hotspot_Drawer"), ("BG_01_MyDesk", "Hotspot_Drawer") },
    };

    // ===== "엉뚱한 곳"을 조사하면 타임어택 시간이 깎이는 오브젝트들 =====
    // 자료실(_05)의 단서가 아닌 책들, 사장실(_07)의 커튼/책장/소파처럼 "뒤져볼 법하지만
    // 정답은 아닌" 자리를 조사하면 시간을 낭비했다는 뜻으로 페널티
    // (TimeAttackController.ApplyPenalty)를 준다. 새 CSV 컬럼을 추가하지 않고
    // (팀 규칙, CSV_가이드.md 참고) 자료실 문의 예외 분기(ResourceRoomDoorScreenId 등)와 같은
    // 방식으로 화면/오브젝트 이름을 코드에서 식별한다. 타임어택이 돌아가는 중이
    // 아니면(TimeAttackController.IsRunning == false) ApplyPenalty()가 알아서 조용히
    // 무시하므로 여기서 따로 검사할 필요는 없다.
    //
    // 아래 목록은 "이름을 하나씩 적는" 쪽이고, 자료실 책처럼 수가 많은 곳은
    // IsWrongHotspot()에서 규칙으로 가른다. 실제 판정은 항상 IsWrongHotspot()을 거친다.
    private const float WrongHotspotPenaltySeconds = 5f;
    private static readonly HashSet<string> WrongHotspots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        TextKey("BG_07_InvestigationSite_07", "Hotspot_Curtain"),
        TextKey("BG_07_InvestigationSite_07", "Hotspot_BookCase"),
        // 사장 책상(Hotspot_CEODesk)은 뺐다. 아이템을 주는 핵심 조사 자리라 "엉뚱한 곳"이
        // 아니다. 시간을 깎으면 꼭 해야 할 일에 벌을 주는 셈이 된다.
        TextKey("BG_07_InvestigationSite_07", "Hotspot_Sofa"),
    };

    // 자료실 책 24권은 위 목록에 이름을 적지 않고 규칙으로 가른다.
    // 책이 24권이나 되는데 그중 단서는 셋뿐이라, 21개를 손으로 적어두면 책 이름을 하나
    // 고칠 때마다 목록도 같이 고쳐야 하고 빠뜨리면 조용히 페널티가 사라진다.
    // 단서 책만 핫스팟 이름이 "_Clue"로 끝나므로(InvestigationStage.csv), 자료실 화면에서
    // "_Clue로 끝나지 않는 책"이면 전부 엉뚱한 곳이다.
    private static bool IsWrongHotspot(string screenId, string hotspotKey)
    {
        if (string.IsNullOrEmpty(screenId) || string.IsNullOrEmpty(hotspotKey)) return false;

        if (string.Equals(screenId, ArchiveRoomScreenId, StringComparison.OrdinalIgnoreCase))
            return IsArchiveJunkBook(screenId, hotspotKey);

        return WrongHotspots.Contains(TextKey(screenId, hotspotKey));
    }

    // 자료실 책 24권 중 단서가 아닌 21권인가. (단서 책만 핫스팟 이름이 "_Clue"로 끝난다)
    private static bool IsArchiveJunkBook(string screenId, string hotspotKey)
    {
        return string.Equals(screenId, ArchiveRoomScreenId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(hotspotKey)
            && hotspotKey.StartsWith(ArchiveBookPrefix, StringComparison.OrdinalIgnoreCase)
            && !hotspotKey.EndsWith(ArchiveClueSuffix, StringComparison.OrdinalIgnoreCase);
    }

    // ===== 자료실 단서를 다시 누르면 "이미 확인한 단서다." =====
    // 단서 책(핫스팟 이름이 "_Clue"로 끝나는 책)은 처음 누르면 CSV의 Text 칸 대사와 함께
    // 아이템을 주고, 그 아이템을 전부 얻은 뒤 다시 누르면 같은 대사를 또 보여주던 것을 바꾼다.
    // 새 CSV 컬럼 없이(팀 규칙) 코드에서 가른다: 이 단서가 주는 아이템을 전부 이미 갖고 있으면 "다시 누른 것"이다.
    private const string AlreadyCheckedClueText = "이미 확인한 단서다.";
    private static bool IsArchiveClue(string screenId, string hotspotKey)
    {
        return string.Equals(screenId, ArchiveRoomScreenId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(hotspotKey)
            && hotspotKey.StartsWith(ArchiveBookPrefix, StringComparison.OrdinalIgnoreCase)
            && hotspotKey.EndsWith(ArchiveClueSuffix, StringComparison.OrdinalIgnoreCase);
    }

    // 이 오브젝트가 주는 아이템을 전부 이미 갖고 있는가 (아이템이 없는 오브젝트는 false).
    private static bool HasAllItemsOf(string itemIdRaw)
    {
        List<string> ids = ParseItemList(itemIdRaw);
        if (ids == null || ids.Count == 0 || InventoryManager.Instance == null) return false;
        foreach (string id in ids)
            if (!InventoryManager.Instance.HasItem(id)) return false;
        return true;
    }

    // ===== 경찰서(#03)의 아이: 다른 오브젝트를 2개 조사해야 나타났다가, 대사 뒤 뛰어서 사라진다 =====
    // 새 CSV 컬럼 없이(팀 규칙) 화면/오브젝트 이름을 코드에 적어 식별한다 (자료실 문 예외와 같은 방식).
    //   - 아이를 뺀 이 화면의 다른 오브젝트(사람 포함)를 PoliceChildRevealCount개 누르면 아이가 나타난다.
    //   - 아이를 누르면 CSV에 적힌 대사가 나오고, 그 대사를 닫는 순간 발소리와 함께 사라진다.
    //   - 한 번 사라지면 다시 나타나지 않는다 (표시 아이템 PoliceChildGoneFlag, 세이브에도 남는다).
    private const string PoliceScreenId = "BG_03_Police";
    private const string PoliceChildKey = "Hotspot_Child";
    private const int PoliceChildRevealCount = 2;
    private const string PoliceChildGoneFlag = "police_child_gone";
    private const string PoliceChildLeaveSfx = "Sounds/SFX/RunningFootsteps_SFX";

    // ===== 이벤트 07 사무실: 직원을 누르면 타자 소리 =====
    // 새 CSV 컬럼 없이(팀 규칙) 화면/오브젝트 이름을 코드에 적어 식별한다.
    //   Hotspot_Officer2(대리) = 빠른 타자, Hotspot_Officer3(신입) = 느린 타자. 누를 때마다 난다.
    private const string OfficeScreenId = "BG_07_InvestigationSite_02";
    private static readonly Dictionary<string, string> OfficeTypingSfx = new Dictionary<string, string>
    {
        { "Hotspot_Officer2", "Sounds/SFX/KeyboardFast_SFX" },
        { "Hotspot_Officer3", "Sounds/SFX/Keyboard_SFX" },
    };

    // 아이의 대사를 닫으면 사라져야 하는 상태인가 (DismissTalkLine / FinishPoliceChildIfPending 참고).
    private bool policeChildVanishPending;

    // ===== 자료실 증거를 전부 모았는가 =====
    // 자료실 앞(_04)의 NextScreenRequires 줄에 적힌 목록(자료실 증거 전부)을 그대로 쓴다.
    // 이 목록은 원래 사장실 앞으로 가는 화살표를 열어줄 때 쓰는 것인데, 새 CSV 컬럼을
    // 추가하지 않기 위해(팀 규칙, CSV_가이드.md 참고) 같은 목록을 재사용한다.
    //
    // 두 곳에서 이 판단이 필요하다 - 자료실 재입장을 막는 처리(Inspect)와 복도 감시를
    // 끄는 처리(EnterScreen). 기준이 갈리면 "문은 안내문만 띄우는데 복도에서는 들키는"
    // 어긋난 상태가 생기므로 한 함수로 묶어둔다.
    //
    // 목록이 비어 있으면 false다. HasAllItems()는 조건이 없으면 true를 돌려주는데,
    // 그 값을 그대로 쓰면 CSV에서 NextScreenRequires 줄이 빠졌을 때 "이미 다 모았다"로
    // 오해해서 복도 감시가 통째로 꺼져버린다.
    private bool HasAllResourceRoomClues()
    {
        if (!screenData.TryGetValue(ResourceRoomDoorScreenId, out ScreenData doorScreen)) return false;
        var need = doorScreen.nextScreenRequiredItemIds;
        if (need == null || need.Count == 0) return false;
        return HasAllItems(need);
    }

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

    // ---------------------------------------------------------------------------------
    // 경찰서 아이 (등장 조건 / 등장 / 퇴장)
    // ---------------------------------------------------------------------------------
    private static bool IsPoliceChild(string screenId, string hotspotKey)
    {
        return string.Equals(screenId, PoliceScreenId, StringComparison.OrdinalIgnoreCase) && hotspotKey == PoliceChildKey;
    }

    // 지금 아이가 화면에 있어야 하는가: 아직 사라진 적 없고, 다른 오브젝트를 충분히 조사했다.
    private static bool ShouldShowPoliceChild()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem(PoliceChildGoneFlag)) return false;

        int count = 0;
        string prefix = PoliceScreenId + "|";
        string childKey = TextKey(PoliceScreenId, PoliceChildKey);
        foreach (string k in inspectedHotspots)
        {
            if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && k != childKey) count++;
        }
        return count >= PoliceChildRevealCount;
    }

    // 오브젝트를 누를 때마다 부른다. 조건이 채워진 순간 아이를 만든다.
    private void RevealPoliceChildIfNeeded()
    {
        if (!string.Equals(activeScreenId, PoliceScreenId, StringComparison.OrdinalIgnoreCase) || hotspotRoot == null) return;
        if (hotspotRoot.transform.Find(PoliceChildKey) != null || !ShouldShowPoliceChild()) return;
        if (!screenData.TryGetValue(activeScreenId, out ScreenData screen)) return;

        var data = screen.hotspots.Find(h => h.key == PoliceChildKey);
        if (data != null) CreateHotspot(data);
    }

    // 아이의 대사를 닫는 순간 발소리와 함께 아이를 지운다. 대기 중이 아니면 아무 일도 하지 않는다.
    private void FinishPoliceChildIfPending()
    {
        if (!policeChildVanishPending) return;
        policeChildVanishPending = false;

        InventoryManager.Instance?.AddItem(PoliceChildGoneFlag);
        AudioManager.PlaySfx(PoliceChildLeaveSfx);

        if (hotspotRoot != null)
        {
            var child = hotspotRoot.transform.Find(PoliceChildKey);
            if (child != null) Destroy(child.gameObject);
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
                AudioManager.PlaySfxIfIdle(DoorOpenSfx);   // 자료실 문이 열리는 소리
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

    // ===== 조합 힌트 대사 =====
    // 가방에서 두 물건을 "조합"해야 나오는 단서(예: SD카드 + 카메라 -> 현장 사진)는, 조합을
    // 떠올리지 못하면 추리에 쓰이는 단서를 통째로 놓친다. 그래서 조합 재료를 주웠을 때 짝이 되는
    // 물건을 이미 갖고 있으면, 자료 뷰어를 닫은 직후 주인공이 한마디 해서 조합을 떠올리게 한다.
    //   주운 아이템 -> (짝 아이템, 화자, 대사)
    private static readonly Dictionary<string, (string partner, string speaker, string line)> CombineHintLines =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "sd_card", ("camera", "차재훈", "카메라에 꽂아 보자.") },
        };

    private void ScheduleCombineHint(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || !CombineHintLines.TryGetValue(itemId.Trim(), out var hint)) return;
        if (InventoryManager.Instance == null || !InventoryManager.Instance.HasItem(hint.partner)) return;
        if (DocumentViewerController.Instance == null) return;

        System.Action handler = null;
        handler = () =>
        {
            DocumentViewerController.Instance.OnHidden -= handler;
            // 그 사이 조사가 끝났거나 다른 대사가 떠 있으면 끼어들지 않는다.
            if (!inSession || IsShowingTalkLine) return;
            ShowLineInDialogue(hint.speaker, hint.line);
        };
        DocumentViewerController.Instance.OnHidden += handler;
    }
}
