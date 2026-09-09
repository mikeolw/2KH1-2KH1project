using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================================
// 게임 시작 시 필요한 매니저들을 자동으로 만들어주는 부트스트랩
// =====================================================================================
// ===== 왜 필요한가? (유니티를 잘 모르는 팀원을 위한 설명) =====
// 유니티에서 "매니저" 스크립트는 보통 씬에 빈 GameObject를 만들고 거기에 붙여둬야 동작한다.
// 그런데 이번에 추가된 매니저가 여럿이라(조사기록, 세이브포인트, 오디오, 글꼴, 추리,
// 자료 뷰어, 무대 연출 ...) 씬마다 손으로 다 만들어 붙이려면 번거롭고, 하나라도 빠뜨리면
// 그 기능만 조용히 동작하지 않아서 원인을 찾기 어렵다.
//
// 그래서 이 스크립트가 게임이 시작될 때 "필요한데 씬에 없는" 매니저를 알아서 만들어준다.
// 씬에 이미 만들어둔 것이 있으면 그대로 두고 건드리지 않으므로, 나중에 인스펙터에서
// 세밀하게 설정하고 싶어지면 그때 씬에 직접 만들어 붙이면 된다.
//
// ===== 언제 실행되나? =====
// [RuntimeInitializeOnLoadMethod]는 유니티가 게임을 시작할 때 자동으로 불러주는 표시다.
// RuntimeInitializeLoadType.AfterSceneLoad를 지정했으므로 "첫 씬이 다 로드된 직후"에 실행된다.
// 씬이 바뀔 때마다도 확인해야 하므로 SceneManager.sceneLoaded도 함께 구독한다.
//
// ===== 두 종류의 매니저 =====
//   1) 게임 전체에서 하나만 있으면 되는 것 (DontDestroyOnLoad)
//      - SettingsManager, SaveManager, SavePointManager, AudioManager, FontManager
//      - 한 번 만들어두면 씬이 바뀌어도 계속 살아 있다.
//   2) 씬마다 하나씩 필요한 것
//      - StageController, DocumentViewerController, NoteManager, DeductionController,
//        InventoryManager, ItemModalController
//      - 화면(Canvas)에 붙어 동작하거나 씬 진행 상태를 다루므로 씬이 바뀌면 새로 필요하다.
//        (단, 게임 진행 상태를 들고 있는 InventoryManager/NoteManager는 세이브를 통해
//         복원되므로 씬이 바뀌어도 데이터가 사라지지 않는다.)
public static class GameBootstrap
{
    // 게임 시작 직후 한 번 실행된다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureGlobalManagers();
        EnsureSceneManagers();

        // 씬을 옮길 때마다 씬 단위 매니저를 다시 확인한다.
        // -= 를 먼저 해두는 이유: 도메인 리로드를 끈 설정에서는 플레이를 여러 번 눌러도
        // 이 static 이벤트 구독이 남아 있을 수 있어서, 그대로 += 하면 중복 등록된다.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Settings 씬처럼 겹쳐 뜨는(additive) 씬에서는 게임용 매니저를 또 만들 필요가 없다.
        if (mode == LoadSceneMode.Additive) return;

        EnsureGlobalManagers();
        EnsureSceneManagers();
    }

    // ---------------------------------------------------------------------------------
    // 게임 전체에서 하나만 있으면 되는 매니저들
    // ---------------------------------------------------------------------------------
    private static void EnsureGlobalManagers()
    {
        // 각 매니저는 자기 Awake()에서 DontDestroyOnLoad를 호출하므로,
        // 여기서는 "없으면 만든다"만 해주면 된다.
        if (SettingsManager.Instance == null) Create<SettingsManager>("SettingsManager");
        if (SaveManager.Instance == null) Create<SaveManager>("SaveManager");
        if (SavePointManager.Instance == null) Create<SavePointManager>("SavePointManager");
        if (AudioManager.Instance == null) Create<AudioManager>("AudioManager");
        if (FontManager.Instance == null) Create<FontManager>("FontManager");

        // ===== 스킵(이미 읽은 대사) 기록 =====
        // "이 줄을 예전에 본 적이 있는가"를 기기 단위로 기억하는 매니저다(ReadProgressManager.cs 참고).
        // 대사창의 스킵 버튼 중 "이미 읽은 곳까지만 넘기기"가 이 기록을 보고 어디서 멈출지 정한다.
        //
        // 원래는 씬에 직접 붙여야 하는 것으로 만들어졌는데 어느 씬에도 붙어 있지 않았고,
        // 그래서 Instance가 계속 null이었다. 호출부가 전부 ?. 로 되어 있어 오류는 안 났지만,
        // 읽은 줄이 하나도 기록되지 않아 스킵을 누르면 곧바로 멈춰버려 기능이 죽어 있었다.
        // 다른 전역 매니저와 똑같이 여기서 만들어준다.
        if (ReadProgressManager.Instance == null) Create<ReadProgressManager>("ReadProgressManager");
    }

    // ---------------------------------------------------------------------------------
    // 씬마다 필요한 매니저들
    // ---------------------------------------------------------------------------------
    private static void EnsureSceneManagers()
    {
        // 대사 시스템이 없는 씬(타이틀, 세이브 화면, 설정 화면)에서는 아래 매니저들이
        // 필요 없다. 괜히 만들면 빈 Canvas에 배경 오브젝트가 생기는 등 부작용만 있으므로
        // "대사 시스템이 있는 씬"에서만 준비한다.
        if (DialogueSystem.Instance == null) return;

        if (InventoryManager.Instance == null) Create<InventoryManager>("InventoryManager");
        if (NoteManager.Instance == null) Create<NoteManager>("NoteManager");
        if (DeductionController.Instance == null) Create<DeductionController>("DeductionController");

        // 아래 둘은 보통 씬에 이미 있지만, 없으면 대사 진행이 통째로 막히는 핵심 부품이라
        // 확실히 확보해둔다. (엔딩 분기와 조사 화면 담당)
        if (GameFlowManager.Instance == null) Create<GameFlowManager>("GameFlowManager");
        if (InvestigationController.Instance == null) Create<InvestigationController>("InvestigationController");

        // 아래 둘은 Canvas 위에 UI를 만들어야 하므로, 씬에 Canvas가 있을 때만 만든다.
        // FindAnyObjectByType: "아무거나 하나만 찾으면 된다"는 뜻. 예전 FindObjectOfType은
        // 유니티 6에서 사용 중단(deprecated)되어 경고가 뜬다.
        if (Object.FindAnyObjectByType<Canvas>() != null)
        {
            if (StageController.Instance == null) Create<StageController>("StageController");
            if (DocumentViewerController.Instance == null) Create<DocumentViewerController>("DocumentViewer");
            if (SaveSlotDialog.Instance == null) Create<SaveSlotDialog>("SaveSlotDialog");
            if (SettingsPanelUI.Instance == null) Create<SettingsPanelUI>("SettingsPanel");

            // 미니게임 2(진행형 타임어택)의 mm:ss 카운트다운 UI. StageController와 마찬가지로
            // 인스펙터 연결 없이 스스로 Canvas를 찾아 UI를 만들어내므로 자동 생성해도 안전하다.
            if (TimeAttackController.Instance == null) Create<TimeAttackController>("TimeAttackController");
        }
        else
        {
            Debug.LogWarning("[GameBootstrap] 씬에 Canvas가 없어 배경/스탠딩과 자료 뷰어를 준비하지 못했습니다.");
        }

        // 예전 방식으로 씬에 미리 만들어둔 껍데기 UI를 정리한다.
        HideLegacyPlaceholders();

        // 카메라에 화면 비율 고정(검은 여백 처리)을 붙인다.
        EnsureAspectRatioKeeper();

        // 퀵바의 가방/수첩 버튼이 여는 패널에 실제 기능을 붙인다.
        EnsurePanelUI();
    }

    // ===== 퀵바 패널에 기능 붙이기 =====
    // 씬의 InventoryPanel / NotePanel은 원래 빈 껍데기(또는 아이템 자리만 미리 놓아둔 것)라서
    // 열어봐도 아무 기능이 없었다. 여기서 실제 기능 스크립트를 붙여준다.
    //
    // 인스펙터에서 직접 붙여도 되지만, 씬 파일을 고치면 팀원끼리 충돌이 잦고 하나 빠뜨리면
    // 그 탭만 조용히 죽어버리므로 코드에서 확실히 보장한다.
    private static void EnsurePanelUI()
    {
        if (UIManager.Instance == null) return;

        // ----- 가방(Inven) 탭 -----
        var inventoryPanel = UIManager.Instance.inventoryPanel;
        if (inventoryPanel != null && inventoryPanel.GetComponent<InventoryPanelUI>() == null)
        {
            // 예전 방식으로 미리 놓아둔 아이템 자리(ItemSlots)는 새 목록과 겹치므로 꺼둔다.
            // 지우지 않고 꺼두기만 하는 이유: 나중에 예전 방식으로 되돌리고 싶을 때를 위해서.
            var oldSlots = inventoryPanel.transform.Find("ItemSlots");
            if (oldSlots != null) oldSlots.gameObject.SetActive(false);

            inventoryPanel.AddComponent<InventoryPanelUI>();
            Debug.Log("[GameBootstrap] 가방 패널에 아이템 목록/설명/조합 기능을 붙였습니다.");
        }

        // ----- 수첩(Note) 탭 -----
        var notePanel = UIManager.Instance.notePanel;
        if (notePanel != null && notePanel.GetComponent<NotePanelUI>() == null)
        {
            notePanel.AddComponent<NotePanelUI>();
            Debug.Log("[GameBootstrap] 수첩 패널에 조사기록 표시 기능을 붙였습니다.");
        }
    }

    // ---------------------------------------------------------------------------------
    // 예전 껍데기 UI 정리
    // ---------------------------------------------------------------------------------
    // ===== 왜 필요한가? =====
    // 이 씬은 원래 "미리 만들어둔 UI 판때기"로 게임을 흉내 내던 시절에 꾸며진 것이다.
    // 지금은 배경/스탠딩/조사 오브젝트를 전부 코드가 런타임에 만들어 쓰는데, 그 시절의
    // 껍데기 오브젝트들이 씬에 그대로 남아 켜져 있어서 화면을 덮고 있었다.
    //
    // 실제로 확인된 증상:
    //   background     : 화면 전체를 덮는 흰색 반투명(알파 0.392) 판.
    //                    새로 만든 Stage_Background 위에 얹혀서 배경/스탠딩/소품이
    //                    전부 뿌옇게 보였다("흐린 색으로 나온다").
    //   ItemModalPanel : 화면 전체를 덮는 검은 반투명(알파 0.6) 판. 위와 같은 이유로
    //                    화면을 어둡게 덮고 있었다.
    //   MinigamePanel  : 화면 한가운데 900x220 크기의 거의 불투명한 검은 상자.
    //                    조사 화면 한복판을 가려서 배경+스탠딩+오브젝트 조합이
    //                    제대로 안 보였다.
    //
    // 이 판들은 원래 각자의 컨트롤러가 Awake()에서 꺼주게 되어 있는데, 그 컨트롤러
    // (MinigameController / ItemModalController)가 씬에 없으면 끄는 코드 자체가 실행되지
    // 않아 계속 켜진 채로 남는다. 그래서 여기서 확실히 정리한다.
    //
    // ===== 지우지 않고 "끄기"만 하는 이유 =====
    // 씬 파일을 고치면 팀원끼리 충돌이 잦고, 나중에 예전 방식이 필요해질 수도 있다.
    // 끄기만 하면 씬 파일은 그대로 두고 화면만 깨끗해진다.
    private static void HideLegacyPlaceholders()
    {
        // 배경 껍데기: 코드가 만드는 Stage_Background가 이 역할을 대신하므로 항상 끈다.
        HideIfActive("background", "Stage_Background가 대신하므로");

        // 아래 둘은 "그 판을 관리하는 컨트롤러가 씬에 있으면" 그쪽이 알아서 켜고 끄므로
        // 건드리지 않는다. 컨트롤러가 없을 때만 우리가 꺼준다.
        if (Object.FindAnyObjectByType<MinigameController>(FindObjectsInactive.Include) == null)
        {
            HideIfActive("MinigamePanel", "MinigameController가 씬에 없어 아무도 끄지 않으므로");
        }
        if (Object.FindAnyObjectByType<ItemModalController>(FindObjectsInactive.Include) == null)
        {
            HideIfActive("ItemModalPanel", "ItemModalController가 씬에 없어 아무도 끄지 않으므로");
        }
    }

    // 이름으로 찾아서 켜져 있으면 끈다.
    // GameObject.Find는 "켜져 있는 것"만 찾아주는데, 우리는 켜져 있는 것만 끄면 되므로 딱 맞다.
    private static void HideIfActive(string objectName, string reason)
    {
        var go = GameObject.Find(objectName);
        if (go == null) return;

        go.SetActive(false);
        Debug.Log($"[GameBootstrap] 예전 껍데기 '{objectName}'을(를) 껐습니다 ({reason}).");
    }

    // 메인 카메라에 AspectRatioKeeper가 없으면 붙여준다.
    // 전체화면에서 4:3 그림이 옆으로 늘어나는 것을 막는 역할이다 (AspectRatioKeeper.cs 참고).
    private static void EnsureAspectRatioKeeper()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (cam.GetComponent<AspectRatioKeeper>() == null)
        {
            cam.gameObject.AddComponent<AspectRatioKeeper>();
        }
    }

    // 빈 GameObject를 만들고 지정한 컴포넌트를 붙인다.
    private static T Create<T>(string objectName) where T : Component
    {
        var go = new GameObject(objectName);
        var component = go.AddComponent<T>();
        Debug.Log($"[GameBootstrap] 씬에 없어서 '{objectName}'을(를) 자동으로 만들었습니다.");
        return component;
    }
}
