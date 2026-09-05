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

        // 아래 둘은 Canvas 위에 UI를 만들어야 하므로, 씬에 Canvas가 있을 때만 만든다.
        // FindAnyObjectByType: "아무거나 하나만 찾으면 된다"는 뜻. 예전 FindObjectOfType은
        // 유니티 6에서 사용 중단(deprecated)되어 경고가 뜬다.
        if (Object.FindAnyObjectByType<Canvas>() != null)
        {
            if (StageController.Instance == null) Create<StageController>("StageController");
            if (DocumentViewerController.Instance == null) Create<DocumentViewerController>("DocumentViewer");
        }
        else
        {
            Debug.LogWarning("[GameBootstrap] 씬에 Canvas가 없어 배경/스탠딩과 자료 뷰어를 준비하지 못했습니다.");
        }

        // 카메라에 화면 비율 고정(검은 여백 처리)을 붙인다.
        EnsureAspectRatioKeeper();
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
