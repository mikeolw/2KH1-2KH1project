using UnityEngine;
using TMPro;

// =====================================================================================
// 코드로 만든 글자에 "한글이 나오는 글꼴"을 물려주는 도우미
// =====================================================================================
// ===== 왜 필요한가 =====
// 코드에서 AddComponent<TextMeshProUGUI>()로 글자를 만들면, 그 글자는 TextMeshPro의
// "기본 글꼴"을 쓴다. 그런데 이 프로젝트의 기본 글꼴에는 한글 글자 모양이 다 들어있지
// 않아서, 코드로 만든 글자만 한글이 시커먼 네모로 나오거나 아예 보이지 않았다.
// (씬에 손으로 배치한 글자들은 인스펙터에서 한글 글꼴을 직접 꽂아뒀기 때문에 멀쩡했다)
//
// 그래서 "이미 화면에서 한글이 잘 나오고 있는 글자"의 글꼴을 찾아내, 코드로 만드는
// 글자에도 똑같이 물려준다. 어느 글꼴을 쓸지는 게임마다 다를 수 있으므로 이름을 박아두지
// 않고, 실제로 쓰이고 있는 것을 찾아 쓰는 방식이다.
//
// ===== 찾는 순서 =====
//   1) 대사창(DialogueSystem.sentenceText)의 글꼴 - 한글이 확실히 나오는 곳
//   2) 씬에 있는 아무 TMP 글자의 글꼴
//   3) TextMeshPro 기본 글꼴 (마지막 수단)
public static class UIFontHelper
{
    // 한 번 찾은 글꼴은 기억해둔다. 매번 씬을 뒤지면 느리다.
    private static TMP_FontAsset cachedFont;

    // 이 프로젝트에서 한글이 제대로 나오는 글꼴.
    public static TMP_FontAsset GameFont
    {
        get
        {
            if (cachedFont != null) return cachedFont;

            // 1) 대사창 글꼴 - 게임에서 한글이 가장 확실히 나오는 곳이다.
            if (DialogueSystem.Instance != null && DialogueSystem.Instance.sentenceText != null)
            {
                cachedFont = DialogueSystem.Instance.sentenceText.font;
                if (cachedFont != null) return cachedFont;
            }

            // 2) 씬에 있는 아무 글자의 글꼴.
            //    꺼져 있는 오브젝트까지 뒤지는 이유: 게임 시작 직후에는 대부분의 팝업이
            //    닫혀 있어서, 켜져 있는 것만 찾으면 아무것도 못 찾는 경우가 있다.
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            foreach (var t in texts)
            {
                if (t != null && t.font != null)
                {
                    cachedFont = t.font;
                    return cachedFont;
                }
            }

            // 3) 마지막 수단
            cachedFont = TMP_Settings.defaultFontAsset;
            return cachedFont;
        }
    }

    // 코드로 만든 글자에 게임 글꼴을 물려준다.
    // 글자를 만든 직후에 한 번 불러주면 된다.
    public static void Apply(TMP_Text text)
    {
        if (text == null) return;

        var font = GameFont;
        if (font != null) text.font = font;
    }

    // 어떤 오브젝트 아래에 있는 모든 글자에 한꺼번에 적용한다.
    // 여러 개를 만든 뒤 마지막에 한 번만 불러도 되도록 준비해둔 것.
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

    // 씬이 바뀌어 글꼴을 다시 찾아야 할 때 호출한다.
    public static void ClearCache()
    {
        cachedFont = null;
    }
}
