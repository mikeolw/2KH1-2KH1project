using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 타이틀 화면(Title.unity) 전용 컨트롤러.
//
// 이 스크립트가 하는 일은 딱 하나, 버튼 3개를 각각 다른 씬으로 보내는 것뿐이다.
// 세이브 슬롯 UI는 SaveData.unity + SaveDataSceneController, 환경설정 UI는
// Settings.unity + SettingsPanelController가 따로 담당한다 (원래는 이 셋을 전부
// Title.unity 안에 패널로 넣었었지만, "각 버튼이 완전히 별도의 페이지로 이동해야 한다"는
// 요청에 따라 씬 단위로 분리했다).
//
// Title.unity는 SaveManager/SettingsManager를 생성해서 DontDestroyOnLoad로 유지시키는
// "부트스트랩 씬" 역할도 겸한다. 그래서 게임을 처음 실행하면 반드시 Title 씬을 거쳐야
// 저 두 매니저가 만들어진다 (Build Settings에서도 Title이 0번 씬으로 등록되어 있음).
public class TitleManager : MonoBehaviour
{
    [Header("메인 메뉴 버튼")]
    public Button startButton;     // 시작하기
    public Button saveDataButton;  // 세이브데이터
    public Button settingsButton;  // 환경설정

    [Header("이동할 씬 이름 (Build Settings에 등록되어 있어야 함)")]
    public string gameplaySceneName = "SampleScene";
    public string saveDataSceneName = "SaveData";
    public string settingsSceneName = "Settings";

    private void Awake()
    {
        // Title 씬에는 카메라가 없어서 화면이 지워지지 않는다(Game 뷰에 "No cameras rendering").
        // 그러면 다른 씬으로 넘어간 뒤에도 타이틀 마지막 프레임이 가장자리에 남아 보인다.
        // 화면을 검게 지우는 카메라가 없으면 하나 만들어 둔다.
        if (Camera.allCamerasCount == 0)
        {
            var camGo = new GameObject("TitleClearCamera");
            var clearCam = camGo.AddComponent<Camera>();
            clearCam.clearFlags = CameraClearFlags.SolidColor;
            clearCam.backgroundColor = Color.black;
            clearCam.cullingMask = 0;
        }

        StyleMenu();

        // "시작하기"는 슬롯 선택 없이 곧바로 게임 씬으로 이동해 새 게임을 시작한다.
        // 이전에 이어하던 세이브가 남아 있으면 그 지점부터 시작해 버리므로 여기서 비워준다
        // (DialogueSystem.Start()가 SaveManager.ActiveSave를 보고 이어할지 판단한다).
        startButton.onClick.AddListener(() =>
        {
            if (SaveManager.Instance != null) SaveManager.Instance.SetActiveSave(null);
            SceneManager.LoadScene(gameplaySceneName);
        });

        // "세이브데이터"는 저장된 세이브를 골라 이어하는 화면(SaveData.unity)으로 이동.
        saveDataButton.onClick.AddListener(() =>
        {
            SaveDataSceneController.OpenMode = SaveDataSceneController.Mode.Load;
            SceneManager.LoadScene(saveDataSceneName);
        });

        // "환경설정"은 별도의 설정 화면(Settings.unity)으로 이동한다.
        // 그 씬은 SettingsPanelController가 통합 설정 화면(SettingsPanelUI)을 띄워준다.
        settingsButton.onClick.AddListener(() =>
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.ReturnSceneName = SceneManager.GetActiveScene().name;
            }
            SceneManager.LoadScene(settingsSceneName);
        });
    }

    // ===== 메뉴 모양 (청회색 테마, 씬은 그대로 두고 코드로 입힌다) =====
    // 타이틀 그림(TitleArt)은 4:3 그대로 화면 가운데에 놓이는데, 버튼 묶음은 화면 오른쪽 아래에
    // 붙어 있어서 와이드 화면에서는 그림 가장자리에 반쯤 걸쳐 보였다. 그래서 그림과 같은 4:3 틀을
    // 하나 만들어 버튼 묶음을 그 안 오른쪽 아래로 옮긴다. 화면 비율이 바뀌어도 그림 안에 머문다.
    //
    //   ┌──────────── 타이틀 그림 (4:3) ────────────┐
    //   │                                          │
    //   │                          ┌────────────┐  │
    //   │                          │  시작하기   │  │  ← 마우스 올림: 테두리/글자 밝게 + 왼쪽 막대
    //   │                          │ 세이브데이터 │  │
    //   │                          │  환경설정   │  │
    //   │                          └────────────┘  │
    //   └──────────────────────────────────────────┘
    private const float MenuButtonWidth = 280f;
    private const float MenuButtonHeight = 58f;
    private const float MenuSpacing = 12f;
    private const float MenuMarginRight = 72f;
    private const float MenuMarginBottom = 72f;

    private void StyleMenu()
    {
        if (startButton == null) return;
        var column = startButton.transform.parent as RectTransform;
        var canvasRt = column != null ? column.parent as RectTransform : null;
        if (column == null || canvasRt == null) return;

        // 그림과 같은 4:3 틀
        var frameGo = new GameObject("MenuFrame", typeof(RectTransform), typeof(AspectRatioFitter));
        var frame = (RectTransform)frameGo.transform;
        frame.SetParent(canvasRt, false);
        frame.SetSiblingIndex(column.GetSiblingIndex());
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        var fitter = frameGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 4f / 3f;

        column.SetParent(frame, false);
        column.anchorMin = column.anchorMax = new Vector2(1f, 0f);
        column.pivot = new Vector2(1f, 0f);
        column.anchoredPosition = new Vector2(-MenuMarginRight, MenuMarginBottom);
        column.sizeDelta = new Vector2(MenuButtonWidth, MenuButtonHeight * 3f + MenuSpacing * 2f);

        var layout = column.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = MenuSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        foreach (var button in new[] { startButton, saveDataButton, settingsButton })
        {
            if (button == null) continue;
            var le = button.GetComponent<LayoutElement>();
            if (le == null) le = button.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = MenuButtonHeight;
            MinigameTheme.StyleButton(button, button.GetComponentInChildren<TMPro.TMP_Text>(true), 24f);
        }
    }
}
