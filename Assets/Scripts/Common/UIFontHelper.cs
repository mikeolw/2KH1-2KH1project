using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// =====================================================================================
// 코드로 만든 글자에 "한글이 실제로 나오는 글꼴"을 물려주는 도우미
// =====================================================================================
// ===== 왜 필요한가 =====
// 코드에서 AddComponent<TextMeshProUGUI>()로 글자를 만들면 TextMeshPro의 "기본 글꼴"이
// 붙는다. 그런데 이 프로젝트의 기본 글꼴은 LiberationSans인데 여기엔 한글 글자 모양이
// 아예 없다. 그래서 코드로 만든 UI(수첩·가방·설정·저장창 등)만 한글이 시커먼 네모로
// 나오거나 아예 보이지 않았다.
// 팀이 실제로 쓰는 한글 글꼴은 NanumBarunGothic SDF이고, 씬에 손으로 배치한 글자들은
// 인스펙터에서 그걸 직접 꽂아뒀기 때문에 멀쩡했다.
//
// ===== 어떻게 고르나 =====
// 글꼴 이름을 코드에 박아두면 나중에 글꼴을 바꿀 때 또 깨진다. 그래서 이름이 아니라
// "한글이 실제로 들어있는지"를 직접 확인해서 고른다.
//   TMP_FontAsset.HasCharacter('가') 로 물어보면 그 글꼴이 한글을 그릴 수 있는지 알 수 있다.
// 화면에 있는 글꼴들을 훑어서 이 검사를 통과하는 첫 번째 것을 쓴다.
// 덕분에 팀이 글꼴을 다른 한글 글꼴로 교체해도 코드를 고칠 필요가 없다.
public static class UIFontHelper
{
    // 한글이 들어있는지 확인할 때 쓰는 표본 글자.
    // '가'는 한글 음절의 첫 글자라 한글 글꼴이면 거의 반드시 들어있다.
    private const char KoreanSample = '가';

    // 한 번 찾은 글꼴은 기억해둔다. 매번 씬을 뒤지면 느리다.
    private static TMP_FontAsset cachedFont;

    // 씬이 바뀌면 이전 씬의 글꼴 오브젝트가 사라질 수 있으므로 캐시를 비워야 한다.
    // 이 구독을 한 번만 걸기 위한 플래그.
    private static bool hookedSceneChange;

    // 이 프로젝트에서 한글이 제대로 나오는 글꼴.
    public static TMP_FontAsset GameFont
    {
        get
        {
            HookSceneChangeOnce();

            // 캐시된 글꼴이 아직 살아있고 한글도 되면 그대로 쓴다.
            if (cachedFont != null && SupportsKorean(cachedFont)) return cachedFont;

            cachedFont = FindKoreanFont();
            return cachedFont;
        }
    }

    // 한글이 실제로 나오는 글꼴을 찾는다.
    private static TMP_FontAsset FindKoreanFont()
    {
        // 1) 대사창 글꼴을 먼저 본다. 게임에서 한글이 가장 확실히 나오는 곳이다.
        if (DialogueSystem.Instance != null && DialogueSystem.Instance.sentenceText != null)
        {
            var f = DialogueSystem.Instance.sentenceText.font;
            if (SupportsKorean(f)) return f;
        }

        // 2) 씬에 있는 모든 글자를 훑어서 한글이 되는 글꼴을 찾는다.
        //    꺼져 있는 오브젝트까지 뒤지는 이유: 게임 시작 직후에는 대부분의 팝업이 닫혀
        //    있어서, 켜진 것만 찾으면 못 찾는 경우가 있다.
        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        foreach (var t in texts)
        {
            if (t == null) continue;
            if (SupportsKorean(t.font)) return t.font;
        }

        // 3) 마지막 수단: TextMeshPro 기본 글꼴.
        //    한글이 안 될 가능성이 높지만, 아무것도 없는 것보다는 낫다.
        var fallback = TMP_Settings.defaultFontAsset;
        if (fallback != null && !SupportsKorean(fallback))
        {
            Debug.LogWarning(
                "[UIFontHelper] 한글이 들어있는 글꼴을 찾지 못했습니다. " +
                "코드로 만든 UI의 한글이 깨져 보일 수 있습니다.\n" +
                "  Assets/TextMesh Pro/Fonts/ 아래에 한글 글꼴(예: NanumBarunGothic SDF)이 있는지, " +
                "씬의 글자에 그 글꼴이 연결되어 있는지 확인하세요.");
        }
        return fallback;
    }

    // 이 글꼴로 한글을 그릴 수 있는지 확인한다.
    // 두 번째 인자 true는 "이 글꼴에 연결된 대체 글꼴(fallback)까지 찾아봐 달라"는 뜻이다.
    private static bool SupportsKorean(TMP_FontAsset font)
    {
        if (font == null) return false;

        try
        {
            return font.HasCharacter(KoreanSample, true);
        }
        catch (System.Exception)
        {
            // 어떤 이유로든 확인에 실패하면 "안 된다"로 본다(다음 후보를 찾도록).
            return false;
        }
    }

    // 씬이 바뀔 때 캐시를 비우도록 한 번만 구독해둔다.
    private static void HookSceneChangeOnce()
    {
        if (hookedSceneChange) return;
        hookedSceneChange = true;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 이전 씬에서 찾아둔 글꼴 오브젝트가 사라졌을 수 있으므로 다시 찾게 한다.
        cachedFont = null;
    }

    // 코드로 만든 글자에 게임 글꼴을 물려준다.
    public static void Apply(TMP_Text text)
    {
        if (text == null) return;

        var font = GameFont;
        if (font != null) text.font = font;
    }

    // 어떤 오브젝트 아래에 있는 모든 글자에 한꺼번에 적용한다.
    public static void ApplyToChildren(GameObject root)
    {
        if (root == null) return;

        var font = GameFont;
        if (font == null) return;

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null) text.font = font;
        }
    }

    // 글꼴을 바꾼 뒤 다시 찾게 하고 싶을 때 호출한다.
    public static void ClearCache()
    {
        cachedFont = null;
    }
}
