using System.Collections.Generic;
using UnityEngine;
using TMPro;

// =====================================================================================
// 글꼴 바꾸기 / 글씨 크기 조절 - 화면의 모든 텍스트에 한꺼번에 적용한다
// =====================================================================================
// 환경설정에서 글꼴과 글씨 크기를 바꾸면, 대사창부터 버튼 글씨까지 화면에 있는 모든
// TextMeshPro 텍스트에 즉시 반영된다.
//
// ===== 글꼴 목록 =====
//   0 = 기본     : 프로젝트에 원래 설정되어 있던 글꼴 (게임 시작 시 기억해둔다)
//   1 = 맑은 고딕 : 윈도우 기본 고딕체. 화면에서 읽기 가장 편하다.
//   2 = 바탕     : 명조체(세리프). 소설 같은 느낌.
//   3 = 굴림     : 예전 윈도우 기본 글꼴. 각지고 촘촘하다.
// 1~3번은 윈도우에 설치되어 있는 시스템 글꼴을 게임 실행 중에 읽어와서 쓴다
// (Font.CreateDynamicFontFromOSFont). 그래서 프로젝트에 글꼴 파일을 따로 넣지 않아도 된다.
//
// ===== 시스템 글꼴을 쓸 때 주의할 점 =====
//   - 윈도우에서만 확실히 동작한다. 다른 OS나 글꼴이 지워진 PC에서는 못 찾을 수 있는데,
//     그 경우 경고를 남기고 기본 글꼴을 그대로 쓴다(게임이 멈추지는 않는다).
//   - 시스템 글꼴에서 만든 TMP 폰트는 "필요한 글자를 그때그때 그려서 채우는" 방식이라
//     한글 11,172자를 미리 구워둘 필요가 없다. 대신 처음 나오는 글자는 아주 잠깐 느릴 수 있다.
//
// ===== 글씨 크기 =====
// 절대 크기(pt)가 아니라 배율로 조절한다. 텍스트마다 원래 크기가 다르기 때문에
// (대사 28pt, 버튼 20pt 등) 전부 같은 크기로 만들면 화면이 망가진다.
// 그래서 각 텍스트의 "원래 크기"를 처음 한 번 기억해두고, 거기에 배율을 곱한다.
//
// ===== 씬 배치 =====
// Title.unity에 빈 GameObject를 만들고 이 스크립트를 붙여두면 된다(DontDestroyOnLoad).
// 씬이 바뀌면 새 씬의 텍스트에도 자동으로 다시 적용된다.
public class FontManager : MonoBehaviour
{
    public static FontManager Instance;

    // 설정 화면 드롭다운에 표시할 이름들. GameSettings.fontIndex가 이 배열의 번호다.
    public static readonly string[] FontOptionNames = { "기본", "맑은 고딕", "바탕", "굴림" };

    // 위 이름에 대응하는 실제 윈도우 글꼴 이름. 0번(기본)은 시스템 글꼴이 아니므로 비워둔다.
    private static readonly string[] SystemFontNames = { "", "Malgun Gothic", "Batang", "Gulim" };

    // 게임 시작 시점의 원래 글꼴. 0번(기본)을 고르면 이걸로 되돌린다.
    private TMP_FontAsset defaultFont;

    // 한 번 만든 TMP 폰트를 기억해둔다. 설정을 바꿀 때마다 새로 만들면 메모리가 낭비된다.
    private readonly Dictionary<int, TMP_FontAsset> fontCache = new Dictionary<int, TMP_FontAsset>();

    // 각 텍스트의 "원래 글씨 크기". 배율을 곱할 기준값이다.
    // 키를 TMP_Text로 두면 파괴된 오브젝트가 남을 수 있으므로, 적용할 때마다 정리한다.
    private readonly Dictionary<TMP_Text, float> baseFontSizes = new Dictionary<TMP_Text, float>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 프로젝트에 원래 설정되어 있던 글꼴을 기억해둔다.
        // TMP_Settings.defaultFontAsset은 TextMeshPro가 새 텍스트를 만들 때 쓰는 기본 글꼴이다.
        defaultFont = TMP_Settings.defaultFontAsset;
    }

    private void Start()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= ApplyToAllTexts;
            SettingsManager.Instance.OnSettingsChanged += ApplyToAllTexts;
        }

        ApplyToAllTexts();
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= ApplyToAllTexts;
        }
    }

    private void OnEnable()
    {
        // 씬이 바뀔 때마다 새 씬의 텍스트에도 적용해야 한다.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 새 씬의 UI가 다 만들어진 뒤에 적용해야 하므로 한 프레임 뒤에 실행한다.
        StartCoroutine(ApplyNextFrame());
    }

    private System.Collections.IEnumerator ApplyNextFrame()
    {
        yield return null;
        ApplyToAllTexts();
    }

    // ---------------------------------------------------------------------------------
    // 적용
    // ---------------------------------------------------------------------------------

    // 화면에 있는 모든 TMP 텍스트에 현재 글꼴/크기 설정을 적용한다.
    public void ApplyToAllTexts()
    {
        if (SettingsManager.Instance == null) return;

        var settings = SettingsManager.Instance.Current;
        TMP_FontAsset font = GetFont(settings.fontIndex);
        float scale = Mathf.Clamp(settings.fontScale, 0.5f, 2f);

        // 꺼져 있는(비활성) 오브젝트의 텍스트까지 포함해서 찾는다.
        // FindObjectsInactive.Include를 빼면 지금 닫혀 있는 팝업 안의 글씨는 안 바뀐다.
        // (FindObjectsByType은 유니티 6에서 예전 FindObjectsOfType을 대체한 함수다.
        //  FindObjectsSortMode.None은 "정렬하지 않음" - 순서가 상관없어서 이게 더 빠르다.)
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var text in texts)
        {
            if (text == null) continue;

            // 원래 크기를 아직 모르는 텍스트라면 지금 값을 원래 크기로 기억해둔다.
            // (주의: 이미 배율이 적용된 뒤에 처음 발견하면 그 값이 원래 크기로 잘못 기억되므로,
            //  씬이 로드된 직후에 한 번 훑어주는 게 중요하다. 위 OnSceneLoaded가 그 역할을 한다.)
            if (!baseFontSizes.ContainsKey(text))
            {
                baseFontSizes[text] = text.fontSize;
            }

            if (font != null) text.font = font;
            text.fontSize = baseFontSizes[text] * scale;
        }

        CleanupDestroyedTexts();
    }

    // fontIndex에 해당하는 TMP 폰트를 얻는다. 실패하면 기본 글꼴(null 가능)을 돌려준다.
    private TMP_FontAsset GetFont(int fontIndex)
    {
        // 0번 = 기본 글꼴
        if (fontIndex <= 0) return defaultFont;

        // 이미 만들어둔 게 있으면 재사용
        if (fontCache.TryGetValue(fontIndex, out var cached) && cached != null) return cached;

        if (fontIndex >= SystemFontNames.Length)
        {
            Debug.LogWarning($"[FontManager] 글꼴 번호 {fontIndex}는 목록에 없습니다. 기본 글꼴을 씁니다.");
            return defaultFont;
        }

        string osFontName = SystemFontNames[fontIndex];

        // 윈도우에 설치된 글꼴을 읽어온다. 크기(16)는 여기서 의미가 없다
        // (TMP가 알아서 필요한 크기로 그린다). 글꼴이 없으면 null이 아니라
        // "이름만 다른 기본 글꼴"이 돌아올 수 있어서, 아래에서 실제 생성 결과를 확인한다.
        Font osFont = Font.CreateDynamicFontFromOSFont(osFontName, 16);
        if (osFont == null)
        {
            Debug.LogWarning($"[FontManager] 시스템 글꼴 '{osFontName}'을 찾지 못했습니다. 기본 글꼴을 씁니다.");
            return defaultFont;
        }

        // 시스템 글꼴(Font)을 TextMeshPro가 쓸 수 있는 형식(TMP_FontAsset)으로 바꾼다.
        TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(osFont);
        if (tmpFont == null)
        {
            Debug.LogWarning($"[FontManager] '{osFontName}'을 TMP 글꼴로 변환하지 못했습니다. 기본 글꼴을 씁니다.");
            return defaultFont;
        }

        // 게임이 도는 동안 계속 쓸 글꼴이므로 씬이 바뀌어도 지워지지 않게 한다.
        tmpFont.hideFlags = HideFlags.DontUnloadUnusedAsset;

        fontCache[fontIndex] = tmpFont;
        return tmpFont;
    }

    // 파괴된 텍스트를 기록에서 지운다(메모리 누수 방지).
    private void CleanupDestroyedTexts()
    {
        var toRemove = new List<TMP_Text>();
        foreach (var pair in baseFontSizes)
        {
            if (pair.Key == null) toRemove.Add(pair.Key);
        }
        foreach (var key in toRemove) baseFontSizes.Remove(key);
    }
}
