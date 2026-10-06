using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 퀵바(수첩/휴대폰/가방/설정) 버튼 모양 - Figma "QuickButton" 디자인을 씬 버튼에 입힌다
// =====================================================================================
// 씬의 QuickBarPanel 버튼들(Btn_Note 등)은 흰 기본 버튼에 영어 글자만 있는 상태다.
// 씬 파일을 고치면 팀원끼리 충돌이 잦으므로(GameBootstrap 주석 참고) 게임이 시작될 때
// 코드로 모양만 바꾼다. 버튼의 onClick(UIManager.ToggleNote 등)은 그대로 둔다.
//
// ===== 모양 (Figma QuickButton 컴포넌트) =====
//   Default : 84x84 어두운 칸 + 얇은 청회색 테두리 + 선 아이콘. 이름은 숨김.
//   Hover   : 테두리/아이콘이 밝아지고 칸 아래에 이름(수첩/휴대폰/...)이 나타남.
//   Active  : 그 버튼이 여는 패널이 열려 있는 동안 아이콘 아래에 짧은 밝은 막대.
//
// 그림: Assets/Resources/Illusts/UI/QuickBar/ (QuickButton_Box, Icon_Note, Icon_Phone,
// Icon_Inven, Icon_Settings). 전부 흰색 128px이라 Image.color로 색을 입힌다.
// 테두리 그림이 없으면 모양을 바꾸지 않고 예전 버튼을 그대로 둔다.
public class QuickBarStyler : MonoBehaviour
{
    private const string SpriteFolder = "Illusts/UI/QuickBar/";
    private const float ButtonSize = 84f;
    private const float IconSize = 40f;
    private const float Spacing = 12f;

    // ----- 색 (Figma 디자인 값) -----
    // 칸 불투명도는 Figma 값(0.78 / 0.92)을 Linear 색 공간에 맞게 보정한 값이다.
    // 이 프로젝트는 Linear라서 반투명이 Figma보다 훨씬 옅게 섞인다(흰 배경 위에서 회색으로 뜸).
    // 보정식: a' = 1 - srgbToLinear(1 - a)
    internal static readonly Color BoxColor = new Color(0x12 / 255f, 0x19 / 255f, 0x20 / 255f, 0.96f);
    internal static readonly Color BoxHoverColor = new Color(0x1A / 255f, 0x23 / 255f, 0x2D / 255f, 0.99f);
    internal static readonly Color StrokeColor = new Color32(0x39, 0x47, 0x54, 0xFF);
    internal static readonly Color StrokeHoverColor = new Color32(0x8F, 0xA4, 0xB7, 0xFF);
    internal static readonly Color IconColor = new Color32(0xA9, 0xBA, 0xC9, 0xFF);
    internal static readonly Color IconHoverColor = new Color32(0xEE, 0xF3, 0xF7, 0xFF);
    internal static readonly Color LabelColor = new Color32(0xD9, 0xE3, 0xEB, 0xFF);

    // 씬 버튼 이름 -> (아이콘 파일, 이름표)
    private static readonly (string button, string icon, string label)[] Buttons =
    {
        ("Btn_Note", "Icon_Note", "수첩"),
        ("Btn_Phone", "Icon_Phone", "휴대폰"),
        ("Btn_Inventory", "Icon_Inven", "가방"),
        ("Btn_Settings", "Icon_Settings", "설정"),
    };

    private void Start()
    {
        Apply();
    }

    private void Apply()
    {
        var box = Resources.Load<Sprite>(SpriteFolder + "QuickButton_Box");
        if (box == null)
        {
            Debug.LogWarning("[QuickBarStyler] 퀵바 그림을 찾을 수 없어 예전 버튼 모양을 그대로 씁니다: Assets/Resources/" + SpriteFolder);
            return;
        }

        var layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = Spacing;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
        }

        // 패널 배경이 있으면 지운다 (버튼만 떠 있게).
        var panelImage = GetComponent<Image>();
        if (panelImage != null) panelImage.color = new Color(0f, 0f, 0f, 0f);

        foreach (var (buttonName, iconName, label) in Buttons)
        {
            var t = transform.Find(buttonName);
            if (t == null) continue;
            var icon = Resources.Load<Sprite>(SpriteFolder + iconName);
            if (icon == null)
            {
                Debug.LogWarning("[QuickBarStyler] 아이콘 그림이 없어 이 버튼은 예전 모양 그대로 둡니다: " + iconName);
                continue;
            }
            StyleButton(t.gameObject, box, icon, label, PanelFor(buttonName));
        }
    }

    // 버튼이 여는 패널. 열려 있는 동안 Active 막대를 보여준다. 설정은 씬을 바꿔서 열리므로 없음.
    private static GameObject PanelFor(string buttonName)
    {
        var ui = UIManager.Instance;
        if (ui == null) return null;
        switch (buttonName)
        {
            case "Btn_Note": return ui.notePanel;
            case "Btn_Phone": return ui.phonePanel;
            case "Btn_Inventory": return ui.inventoryPanel;
            default: return null;
        }
    }

    private static void StyleButton(GameObject go, Sprite box, Sprite icon, string label, GameObject panel)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = ButtonSize;
        le.minHeight = le.preferredHeight = ButtonSize;
        le.flexibleWidth = le.flexibleHeight = 0f;

        // 버튼 자체 Image = 어두운 칸 (클릭 영역도 이것)
        var bg = go.GetComponent<Image>();
        if (bg != null)
        {
            bg.sprite = null;
            bg.type = Image.Type.Simple;
            bg.color = BoxColor;
            bg.raycastTarget = true;
        }
        var button = go.GetComponent<Button>();
        if (button != null) button.transition = Selectable.Transition.None;   // 색은 아래 QuickButtonView가 직접 바꾼다

        // 테두리
        var frame = NewImage("Frame", go.transform, box, StrokeColor);
        Stretch(frame.rectTransform);

        // 아이콘
        var ico = NewImage("Icon", go.transform, icon, IconColor);
        var irt = ico.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(IconSize, IconSize);
        irt.anchoredPosition = Vector2.zero;

        // 열려 있음 표시 막대
        var bar = NewImage("ActiveBar", go.transform, null, LabelColor);
        var brt = bar.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(34f, 3f);
        brt.anchoredPosition = new Vector2(0f, 8f);
        bar.gameObject.SetActive(false);

        // 이름표: 원래 있던 영어 글자(Text (TMP))를 재사용해 칸 아래로 옮긴다.
        var text = go.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            text.fontSize = 18;
            text.fontStyle = FontStyles.Bold;
            text.color = LabelColor;
            text.alignment = TextAlignmentOptions.Top;
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(120f, 28f);
            trt.anchoredPosition = new Vector2(0f, -8f);
            UIFontHelper.Apply(text);
            text.gameObject.SetActive(false);
        }

        var view = go.GetComponent<QuickButtonView>();
        if (view == null) view = go.AddComponent<QuickButtonView>();
        view.Init(bg, frame, ico, bar.gameObject, text != null ? text.gameObject : null, panel);
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = sprite != null;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
