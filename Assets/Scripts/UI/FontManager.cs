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
// 1~3번은 컴퓨터에 설치되어 있는 시스템 글꼴을 게임 실행 중에 읽어와서 쓴다
// (Font.CreateDynamicFontFromOSFont). 그래서 프로젝트에 글꼴 파일을 따로 넣지 않아도 된다.
// 글꼴 이름은 OS마다 다르므로 번호마다 후보를 여러 개 두고 되는 것을 쓴다
// (아래 SystemFontCandidates 참고).
//
// ===== 시스템 글꼴을 쓸 때 주의할 점 =====
//   - 글꼴이 없는 컴퓨터에서는 못 찾을 수 있다(특히 한글 글꼴이 안 깔린 리눅스).
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

    // 위 이름에 대응하는 실제 시스템 글꼴 이름 후보들. 0번(기본)은 시스템 글꼴이 아니라 비워둔다.
    //
    // ===== 왜 후보를 여러 개 두는가 =====
    // 예전에는 윈도우 글꼴 이름 하나씩만 적어 두었다("Malgun Gothic"/"Batang"/"Gulim").
    // 이 이름들은 윈도우에만 있어서, macOS와 리눅스에서는 글꼴 선택 1~3번이 모두 아무 반응
    // 없이 기본 글꼴로만 보였다(게임이 멈추지는 않지만 설정 기능이 죽은 것과 같다).
    // OS마다 한글 글꼴 이름이 전부 다르기 때문에, 앞에서부터 하나씩 시도해서 먼저 되는 것을 쓴다.
    // 윈도우 이름을 맨 앞에 두었으니 윈도우에서는 예전과 똑같이 동작한다.
    //
    // 리눅스는 한글 글꼴이 아예 안 깔려 있는 경우도 흔하다. 그때는 후보를 전부 실패하고
    // 프로젝트에 들어 있는 기본 글꼴을 그대로 쓴다(한글이 깨지지는 않는다).
    private static readonly string[][] SystemFontCandidates =
    {
        // 0 = 기본 (시스템 글꼴을 쓰지 않는다)
        new string[0],

        // 1 = 고딕 (산세리프)
        new[] { "Malgun Gothic",          // 윈도우
                "Apple SD Gothic Neo",    // macOS 10.11 이상
                "AppleGothic",            // 예전 macOS
                "Noto Sans CJK KR",       // 리눅스 (요즘 배포판 기본)
                "Noto Sans KR",
                "NanumGothic" },

        // 2 = 명조 (세리프)
        new[] { "Batang",                 // 윈도우
                "AppleMyungjo",           // macOS
                "Noto Serif CJK KR",      // 리눅스
                "Noto Serif KR",
                "NanumMyeongjo",
                "UnBatang" },

        // 3 = 굴림 (각지고 촘촘한 고딕)
        new[] { "Gulim",                  // 윈도우
                "AppleGothic",            // macOS - 굴림과 느낌이 가장 가깝다
                "NanumGothicCoding",      // 리눅스
                "Noto Sans CJK KR" },
    };

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

        // ===== "기본" 글꼴로 되돌릴 때 쓸 글꼴 =====
        // TMP_Settings.defaultFontAsset을 그대로 쓰면 안 된다. 이 프로젝트의 TextMeshPro
        // 기본 글꼴에는 한글 글자 모양이 다 들어있지 않아서, 그걸 화면 전체에 적용하면
        // 멀쩡하던 한글 대사까지 시커먼 네모로 깨져버린다.
        //
        // 그래서 "지금 화면에서 한글이 잘 나오고 있는 글꼴"을 찾아 그것을 기본값으로 삼는다.
        // (UIFontHelper.cs 참고 - 대사창 글꼴을 우선으로 찾는다)
        defaultFont = UIFontHelper.GameFont;
        if (defaultFont == null) defaultFont = TMP_Settings.defaultFontAsset;
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

        // 새 씬에는 아직 글꼴이 안 맞춰진 글자들이 있으므로, 설정값이 그대로여도
        // 반드시 한 번 훑어야 한다. (ApplyToAllTexts의 "값이 같으면 건너뛰기"를 무시)
        ForceApplyToAllTexts();
    }

    // 설정값이 바뀌지 않았어도 무조건 다시 적용한다.
    // 씬이 바뀌어 새 글자가 생겼을 때 쓴다.
    public void ForceApplyToAllTexts()
    {
        lastAppliedFontIndex = -1;
        lastAppliedScale = -1f;

        // 씬이 바뀌면 이전 씬 글자들의 원래 크기 기록은 의미가 없다.
        // 그대로 두면 파괴된 오브젝트가 계속 쌓인다.
        CleanupDestroyedTexts();

        ApplyToAllTexts();
    }

    // ---------------------------------------------------------------------------------
    // 적용
    // ---------------------------------------------------------------------------------

    // 마지막으로 적용한 값. 값이 그대로면 다시 훑을 필요가 없다.
    //
    // ===== 왜 이 검사가 필요한가 =====
    // 설정은 슬라이더를 드래그하는 동안 값이 바뀔 때마다 저장되고, 그때마다 이 함수가
    // 불린다. 그런데 이 함수는 씬의 모든 글자를 찾아 훑기 때문에 꽤 무겁다.
    // 볼륨 슬라이더처럼 글꼴과 무관한 설정을 움직일 때도 매번 전체를 훑고 있었으므로,
    // 글꼴이나 크기가 실제로 바뀐 경우에만 일하도록 걸러낸다.
    private int lastAppliedFontIndex = -1;
    private float lastAppliedScale = -1f;

    // 화면에 있는 모든 TMP 텍스트에 현재 글꼴/크기 설정을 적용한다.
    public void ApplyToAllTexts()
    {
        if (SettingsManager.Instance == null) return;

        var settings = SettingsManager.Instance.Current;
        TMP_FontAsset font = GetFont(settings.fontIndex);
        float scale = Mathf.Clamp(settings.fontScale, 0.5f, 2f);

        // 글꼴/크기가 그대로면 아무것도 하지 않는다.
        if (settings.fontIndex == lastAppliedFontIndex && Mathf.Approximately(scale, lastAppliedScale))
        {
            return;
        }
        lastAppliedFontIndex = settings.fontIndex;
        lastAppliedScale = scale;

        // 꺼져 있는(비활성) 오브젝트의 텍스트까지 포함해서 찾는다.
        // FindObjectsInactive.Include를 빼면 지금 닫혀 있는 팝업 안의 글씨는 안 바뀐다.
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);

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

    // 만들기에 실패한 글꼴 번호. 실패도 기억해둬야 매번 다시 시도하지 않는다.
    //
    // ===== 왜 이게 중요한가 =====
    // 예전에는 실패했을 때 아무것도 기억하지 않아서, 글자 크기를 조절하거나 씬이 바뀔 때마다
    // 매번 글꼴 만들기를 다시 시도하고 그때마다 경고를 찍었다. 그 결과 콘솔에 똑같은 경고가
    // 400개 넘게 쌓였다. 한 번 실패한 글꼴은 다시 시도하지 않고 조용히 기본 글꼴을 쓴다.
    private readonly HashSet<int> failedFonts = new HashSet<int>();

    // fontIndex에 해당하는 TMP 폰트를 얻는다. 실패하면 기본 글꼴(null 가능)을 돌려준다.
    private TMP_FontAsset GetFont(int fontIndex)
    {
        // 0번 = 기본 글꼴
        if (fontIndex <= 0) return defaultFont;

        // 이미 만들어둔 게 있으면 재사용
        if (fontCache.TryGetValue(fontIndex, out var cached) && cached != null) return cached;

        // 이미 실패한 적 있는 글꼴이면 조용히 기본 글꼴을 쓴다.
        if (failedFonts.Contains(fontIndex)) return defaultFont;

        if (fontIndex >= SystemFontCandidates.Length)
        {
            Debug.LogWarning($"[FontManager] 글꼴 번호 {fontIndex}는 목록에 없습니다. 기본 글꼴을 씁니다.");
            failedFonts.Add(fontIndex);
            return defaultFont;
        }

        // OS별 후보를 앞에서부터 시도해서 먼저 되는 글꼴을 쓴다.
        string[] candidates = SystemFontCandidates[fontIndex];
        TMP_FontAsset tmpFont = null;
        foreach (string name in candidates)
        {
            tmpFont = TryCreateSystemFont(name);
            if (tmpFont != null) break;
        }

        if (tmpFont == null)
        {
            // 경고는 글꼴 하나당 딱 한 번만 남긴다.
            Debug.LogWarning($"[FontManager] '{FontOptionNames[fontIndex]}'에 쓸 시스템 글꼴을 이 컴퓨터에서 " +
                             "찾지 못해 기본 글꼴로 표시합니다. " +
                             $"(찾아본 이름: {string.Join(", ", candidates)})");
            failedFonts.Add(fontIndex);
            return defaultFont;
        }

        // 게임이 도는 동안 계속 쓸 글꼴이므로 씬이 바뀌어도 지워지지 않게 한다.
        tmpFont.hideFlags = HideFlags.DontUnloadUnusedAsset;

        fontCache[fontIndex] = tmpFont;
        return tmpFont;
    }

    // 윈도우에 설치된 글꼴을 TextMeshPro가 쓸 수 있는 형식으로 바꾼다.
    //
    // 만드는 방법이 유니티 버전에 따라 두 가지라서 순서대로 시도한다:
    //   1) 글꼴 이름으로 바로 만들기 - 유니티 6 계열에서 권장하는 방법
    //   2) 시스템 글꼴 객체를 거쳐 만들기 - 예전 방법
    // 둘 다 실패하면 null을 돌려주고, 부르는 쪽이 기본 글꼴을 쓴다.
    private TMP_FontAsset TryCreateSystemFont(string osFontName)
    {
        // 방법 1
        try
        {
            var direct = TMP_FontAsset.CreateFontAsset(osFontName, "Regular", 90);
            if (direct != null) return direct;
        }
        catch (System.Exception)
        {
            // 이 유니티 버전에 이 방식이 없거나 실패한 경우 - 다음 방법으로 넘어간다.
        }

        // 방법 2
        try
        {
            Font osFont = Font.CreateDynamicFontFromOSFont(osFontName, 90);
            if (osFont == null) return null;

            return TMP_FontAsset.CreateFontAsset(osFont);
        }
        catch (System.Exception)
        {
            return null;
        }
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
