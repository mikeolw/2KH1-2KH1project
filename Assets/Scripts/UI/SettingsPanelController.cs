using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

// =====================================================================================
// Settings.unity(독립 설정 씬) 전용 어댑터
// =====================================================================================
// 타이틀 화면에서 "환경설정"을 누르면 이 씬으로 넘어온다. 이 스크립트가 하는 일은
// 딱 하나 — 씬에 원래 있던 설정 UI를 감추고, 코드로 만드는 통합 설정 화면
// (SettingsPanelUI)을 대신 띄우는 것이다.
//
// ===== 왜 씬의 UI를 안 쓰고 새로 만드나 =====
// 씬에 짜여 있던 설정 화면은 항목이 늘어나면서 버튼이 잘리고 서로 겹쳐 보였다.
// 코드에서 그 위에 항목을 끼워 넣으려 해봤지만, 씬이 어떤 좌표계로 배치돼 있는지
// 알 수 없어 손댈 때마다 더 어긋났다.
// 그래서 설정 화면을 한 벌만 코드로 제대로 만들고(SettingsPanelUI), 인게임에서도
// 타이틀에서도 똑같은 그 화면을 쓰기로 했다. 항목을 추가해도 배치가 깨지지 않고,
// 두 곳의 모양이 항상 같다.
//
// 씬에 있던 원래 UI는 지우지 않고 꺼두기만 하므로, 나중에 되돌리고 싶으면
// 이 스크립트만 떼면 된다.
//
// ===== 인게임 설정은 어디에 있나 =====
// 게임 도중 옵션은 이제 이 씬을 거치지 않는다. UIManager.OpenSettingsScene()이
// 게임 씬 안에서 SettingsPanelUI를 바로 띄운다. 씬을 오가지 않으므로
// "돌아가기를 눌렀는데 옵션 창이 게임 화면 뒤에 다시 보이는" 문제가 생기지 않는다.
public class SettingsPanelController : MonoBehaviour
{
    [Header("돌아갈 씬 이름 (비워두면 SettingsManager의 값을 쓴다)")]
    public string fallbackReturnScene = "Title";

    // 인게임 팝업으로 쓸 때 닫기 동작을 바깥에서 지정하고 싶을 때 사용.
    // (지금은 인게임에서 이 씬을 쓰지 않지만, 구조는 남겨둔다)
    public System.Action onBack;

    // 다른 스크립트가 "설정 화면이 열려 있나?"를 확인할 때 쓴다.
    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        RemoveDuplicateEventSystem();
        HideSceneUI();
    }

    private void Start()
    {
        // SettingsPanelUI는 Canvas가 준비된 뒤에 만들어야 하므로 Start에서 처리한다.
        ShowUnifiedSettings();
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    // ---------------------------------------------------------------------------------
    // 통합 설정 화면 띄우기
    // ---------------------------------------------------------------------------------
    private void ShowUnifiedSettings()
    {
        if (SettingsPanelUI.Instance == null)
        {
            new GameObject("SettingsPanel").AddComponent<SettingsPanelUI>();
        }

        var ui = SettingsPanelUI.Instance;
        if (ui == null) return;

        // 이 씬은 타이틀에서 들어온 독립 설정 화면이므로,
        // 닫으면 패널만 감추는 게 아니라 원래 화면으로 돌아가야 한다.
        ui.onClose = ReturnToPreviousScene;

        // 이미 타이틀 계열 화면이므로 "메인 화면으로" 버튼은 필요 없다.
        ui.SetMainMenuButtonVisible(false);

        ui.Open();
        IsOpen = true;
    }

    // 설정 화면을 닫고 원래 있던 화면으로 돌아간다.
    private void ReturnToPreviousScene()
    {
        IsOpen = false;

        // 다음에 인게임에서 이 패널을 쓸 때를 위해 닫기 동작을 원상복구한다.
        // (안 지우면 인게임에서 닫기를 눌렀을 때도 씬을 갈아치워 버린다)
        if (SettingsPanelUI.Instance != null)
        {
            SettingsPanelUI.Instance.onClose = null;
            SettingsPanelUI.Instance.SetMainMenuButtonVisible(true);
            SettingsPanelUI.Instance.HidePanel();
        }

        if (onBack != null)
        {
            onBack.Invoke();
            return;
        }

        string target = SettingsManager.Instance != null && !string.IsNullOrEmpty(SettingsManager.Instance.ReturnSceneName)
            ? SettingsManager.Instance.ReturnSceneName
            : fallbackReturnScene;

        SceneManager.LoadScene(target);
    }

    // ---------------------------------------------------------------------------------
    // 씬에 원래 있던 UI 감추기
    // ---------------------------------------------------------------------------------
    // 새 설정 화면과 겹쳐 보이지 않도록, 이 씬의 캔버스 아래 있는 것들을 꺼둔다.
    // 지우지 않고 비활성화만 하므로 되돌릴 수 있다.
    private void HideSceneUI()
    {
        var scene = gameObject.scene;
        if (!scene.IsValid()) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                // 캔버스 자체는 켜두고(새 설정 화면이 이 캔버스에 붙는다) 그 자식만 끈다.
                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    canvas.transform.GetChild(i).gameObject.SetActive(false);
                }
            }
        }
    }

    // additive로 겹쳐 뜬 경우 EventSystem이 중복돼 입력이 충돌한다. 이 씬 쪽만 제거한다.
    private void RemoveDuplicateEventSystem()
    {
        var eventSystems = FindObjectsByType<EventSystem>();
        if (eventSystems.Length <= 1) return;

        foreach (var es in eventSystems)
        {
            if (es.gameObject.scene == gameObject.scene) Destroy(es.gameObject);
        }
    }
}
