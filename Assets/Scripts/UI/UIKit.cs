using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// 코드로 UI를 그릴 때 쓰는 공용 부품
// =====================================================================================
// 화면 UI를 그림 파일 없이 코드로 그리면서(자료 뷰어, 선택지, 세이브 화면, 대사 기록...)
// "글자 만들기", "Figma 좌표로 놓기", "얇은 선 상자", "닫기 X" 같은 같은 코드가 파일마다
// 복사되어 있었다. 여기 한 곳에 모아 두고 다들 이걸 쓴다.
//
// ===== 쓰는 법 =====
// 파일 맨 위에 `using static UIKit;` 를 적으면 UIKit. 없이 바로 부를 수 있다:
//   var t = CreateLabel(box, "Title", "대화 기록", 36, FontStyles.Bold, UITheme.Title);
//   PlaceTopLeft(t.rectTransform, 56f, 58f, 600f, 54f);
//
// ===== 좌표 =====
// PlaceTopLeft / AddRect는 Figma와 같은 좌표계다: 부모의 왼쪽 위가 (0,0), 아래로 갈수록 y가 커진다.
// (유니티는 원래 위로 갈수록 y가 커지므로 여기서 뒤집어 준다.) Figma에서 읽은 x, y, w, h를
// 그대로 넣으면 된다.
public static class UIKit
{
    // ---------------------------------------------------------------------------------
    // 배치
    // ---------------------------------------------------------------------------------

    // 부모를 꽉 채운다. inset만큼 안쪽으로 들인다.
    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // Figma 좌표(부모 왼쪽 위 기준, 아래로 +)로 놓는다.
    public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모 가운데 기준으로 놓고 돌린다 (X 표시 / 꺾쇠를 짧은 선으로 그릴 때).
    public static void CenterRotated(RectTransform rt, Vector2 offset, float angle)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    // ---------------------------------------------------------------------------------
    // 도형
    // ---------------------------------------------------------------------------------

    // 색 사각형 (선, 막대, 구분선). 클릭은 받지 않는다.
    public static Image AddRect(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        PlaceTopLeft((RectTransform)go.transform, x, y, w, h);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 상자 테두리를 얇은 선 4개로 그린다 (위, 아래, 왼쪽, 오른쪽). 마우스 올림 때 색을 바꿀 수 있게
    // 4개를 돌려준다. 9-slice 그림 없이도 크기에 상관없이 1px 선이 유지된다.
    public static Image[] AddBorder(RectTransform target, Color color, float thickness = 1f)
    {
        var lines = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Border" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(target, false);
            var rt = (RectTransform)go.transform;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, thickness); break;
                case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(0, thickness); break;
                case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(thickness, 0); break;
                default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(thickness, 0); break;
            }
            rt.anchoredPosition = Vector2.zero;
            lines[i] = go.GetComponent<Image>();
            lines[i].color = color;
            lines[i].raycastTarget = false;
        }
        return lines;
    }

    // ===== 새 창 만들기 =====
    // 화면을 덮는 창(검은 막 97%)을 만들고, 층(UILayers)과 창 목록(ModalStack) 등록까지 한 번에 한다.
    // 이걸로 만들면 Esc로 닫기 / 떠 있는 동안 대사 멈춤 / 다른 UI와의 앞뒤 순서가 자동으로 맞는다.
    //   parent         : 보통 UILayers.RootCanvas().transform
    //   layer          : Panel(수첩·설정 같은 일반 창) / Detail(조사 상세) / Dialog(반드시 골라야 하는 창) ...
    //   onEscape       : Esc를 눌렀을 때 할 일 (보통 닫기). null이면 Esc로 닫히지 않는다.
    //   blocksDialogue : 떠 있는 동안 대사 넘기기/자동 진행을 멈출지
    // 돌려받은 오브젝트 아래에 창 내용을 만들고, 켜고 끄는 것으로 창을 여닫는다.
    public static GameObject CreateWindow(Transform parent, string name, UILayer layer,
                                          System.Action onEscape, bool blocksDialogue = true)
    {
        var root = CreateScreenDim(parent, name);
        UILayers.Place(root, layer);
        ModalStack.Register(root, onEscape, blocksDialogue);
        return root;
    }

    // 화면 전체를 덮는 검은 막(97%)만 만든다. 창이면 위 CreateWindow를 쓴다.
    // 클릭을 받아서 뒤의 게임 화면이 눌리지 않게 막는다.
    public static GameObject CreateScreenDim(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var img = go.GetComponent<Image>();
        img.color = UITheme.ScreenDim;
        img.raycastTarget = true;
        return go;
    }

    // ---------------------------------------------------------------------------------
    // 글자
    // ---------------------------------------------------------------------------------

    // 여러 줄 글자 (본문, 대사). 줄바꿈은 TextMeshPro 기본값(자동 줄바꿈)을 그대로 둔다.
    public static TMP_Text CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;   // 글자가 클릭을 가로채지 않게
        return tmp;
    }

    // 한 줄 글자 (제목, 머리말, 버튼 글자). 칸보다 길면 줄을 바꾸지 않고 "…"로 자른다.
    public static TMP_Text CreateLabel(Transform parent, string name, string text, float size, FontStyles style, Color color)
    {
        var tmp = CreateText(parent, name, text, size, style, color);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    // ---------------------------------------------------------------------------------
    // 버튼
    // ---------------------------------------------------------------------------------

    // 테두리만 있는 버튼. 클릭은 투명한 바탕이 받는다.
    // 마우스 올림 표시는 AddHoverLine으로 따로 붙인다.
    public static Button CreateOutlinedButton(Transform parent, string name, float x, float y, float w, float h,
                                              UnityEngine.Events.UnityAction onClick, out Image[] border)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        PlaceTopLeft(rt, x, y, w, h);
        // 클릭만 받는 투명 바탕. Linear 색 공간이라 알파 0.01만 줘도 회색 상자로 보이므로 0.
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        border = AddBorder(rt, UITheme.Line);
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    // 마우스를 올리면 테두리를 밝게, marks(글자·아이콘)를 markHover 색으로.
    public static UIHoverLine AddHoverLine(GameObject target, Image[] border, Graphic[] marks, Color markNormal, Color markHover)
    {
        var hover = target.AddComponent<UIHoverLine>();
        hover.Init(border, marks, markNormal, markHover);
        return hover;
    }

    // 닫기 X (48x48) + 아래 "ESC" 표기. x, y는 X 상자의 왼쪽 위.
    public static Button CreateCloseButton(Transform parent, float x, float y, UnityEngine.Events.UnityAction onClick)
    {
        var close = CreateOutlinedButton(parent, "CloseButton", x, y, 48f, 48f, onClick, out var border);
        var a = AddRect(close.transform, "CrossA", 0f, 0f, 16f, 1.6f, UITheme.ButtonText);
        var b = AddRect(close.transform, "CrossB", 0f, 0f, 16f, 1.6f, UITheme.ButtonText);
        CenterRotated(a.rectTransform, Vector2.zero, 45f);
        CenterRotated(b.rectTransform, Vector2.zero, -45f);
        AddHoverLine(close.gameObject, border, new Graphic[] { a, b }, UITheme.ButtonText, UITheme.Title);

        var esc = CreateLabel(parent, "EscHint", "ESC", 11, FontStyles.Bold, UITheme.Muted);
        esc.alignment = TextAlignmentOptions.Top;
        esc.characterSpacing = 10f;
        PlaceTopLeft(esc.rectTransform, x, y + 54f, 48f, 16f);
        return close;
    }

    // ---------------------------------------------------------------------------------
    // 화면 머리말 / 꼬리말
    // ---------------------------------------------------------------------------------

    // ▬ (강조 막대)
    // KICKER  (머리말, 작은 영문)
    // 제목    (큰 글자)
    // x, y는 강조 막대의 왼쪽 위. 머리말은 y+16, 제목은 y+32에 놓인다.
    public static void CreateHeader(Transform parent, float x, float y, string kicker, string title,
                                    out TMP_Text kickerText, out TMP_Text titleText, float width = 900f)
    {
        AddRect(parent, "TopAccent", x, y, 120f, 3f, UITheme.Accent);
        kickerText = CreateLabel(parent, "Kicker", kicker, 14, FontStyles.Bold, UITheme.Kicker);
        kickerText.characterSpacing = 20f;
        PlaceTopLeft(kickerText.rectTransform, x, y + 16f, width, 20f);
        titleText = CreateLabel(parent, "Title", title, 36, FontStyles.Bold, UITheme.Title);
        PlaceTopLeft(titleText.rectTransform, x, y + 32f, width, 54f);
    }

    // 맨 아래 조작 안내 한 줄 ("← → 넘기기 · Esc 닫기")
    public static TMP_Text CreateFootHint(Transform parent, float x, float y, float width, string text)
    {
        var hint = CreateLabel(parent, "FootHint", text, 15, FontStyles.Normal, UITheme.Hint);
        PlaceTopLeft(hint.rectTransform, x, y, width, 22f);
        return hint;
    }
}
