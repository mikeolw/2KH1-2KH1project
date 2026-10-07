using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
// 선택지 화면 모양 (청회색 테마)
// =====================================================================================
// 선택지는 세 곳에서 띄운다 - 모두 DialogueSystem의 choicePanel/choiceContainer/choiceButtonPrefab을
// 같이 쓴다.
//   - DialogueSystem.ShowChoices()             : 대본 끝의 선택지
//   - InvestigationController.ShowTalkChoices() : 조사 중 대화 오브젝트의 선택지
//   - DeductionController.ShowCurrentStep()     : 추리 파트 보기
// 세 곳을 다 고치는 대신, 선택지 패널에 이 부품을 붙여 "버튼이 새로 생기면 모양을 입힌다".
// 씬 파일과 ChoiceButton 프리팹은 건드리지 않는다(팀원끼리 씬 충돌 방지).
//
//   ┌───────────────────────────────────────────┐
//   │      한성이 마지막으로 연락한 곳을 묻는다      │   ← 마우스 올림: 테두리/왼쪽 막대 강조
//   └───────────────────────────────────────────┘
//   ┌───────────────────────────────────────────┐
//   │               그냥 돌아간다                 │
//   └───────────────────────────────────────────┘
//   (화면 정가운데, 글자는 가운데 정렬)
//
// 화면 전체를 어둡게 하되, 그 막을 대화창 "뒤"에 깔아서 대화창의 질문은 그대로 읽히게 한다.
// 숫자키 1~9로도 위에서부터 차례로 고를 수 있다.
public class ChoicePanelStyle : MonoBehaviour
{
    // ===== 색 (DocumentViewerController / 대사 기록과 같은 값) =====
    private static readonly Color AccentColor  = Hex(0x8FA4B7);
    private static readonly Color TextColor    = Hex(0xD2DAE1);
    private static readonly Color TextHover    = Hex(0xE4EAEF);
    private static readonly Color LineColor    = Hex(0x34414D);
    private static readonly Color FillColor    = new Color(0.063f, 0.086f, 0.114f, 0.92f);  // 10161D
    private static readonly Color FillHover    = new Color(0.094f, 0.133f, 0.176f, 0.96f);  // 18222D

    // ===== 배치 (화면 1440x1080 기준) =====
    private const float Width = 760f;
    private const float RowHeight = 64f;
    private const float Spacing = 12f;
    private const float TextPadding = 32f;

    private GameObject dim;
    private CanvasGroup group;
    private Coroutine fadeRoutine;

    // DialogueSystem이 시작할 때 한 번 부른다. 이미 붙어 있으면 그대로 둔다.
    public static void Attach(GameObject panel, Transform container, GameObject dialoguePanel)
    {
        if (panel == null || container == null) return;
        if (container.GetComponent<ChoicePanelStyle>() != null) return;
        var style = container.gameObject.AddComponent<ChoicePanelStyle>();
        style.Setup(panel, dialoguePanel);
    }

    private void Setup(GameObject panel, GameObject dialoguePanel)
    {
        // ----- 패널 자리: 화면 정가운데 -----
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);            // 가운데 기준 - 선택지가 많아지면 위아래로 같이 늘어난다
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(Width, rt.sizeDelta.y);

        // 예전 흰 반투명 바탕은 없앤다 (Linear 색 공간이라 알파가 조금만 있어도 회색 상자로 보인다 → 0)
        var bg = panel.GetComponent<Image>();
        if (bg != null) { bg.sprite = null; bg.color = new Color(0f, 0f, 0f, 0f); }

        var layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = Spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();

        // ----- 화면을 어둡게 하는 막 -----
        // 패널의 자식으로 두면 선택지를 다시 띄울 때마다 "자식 전부 지우기"에 같이 지워지므로 형제로 둔다.
        // 예전에는 대화창 윗변 위쪽만 덮었는데, 대화창 그림의 실제 윗변과 딱 맞지 않아 그 사이에
        // 밝은 띠가 생겼다. 그래서 화면 전체를 덮고, 대화창과 선택지 패널 중 더 앞쪽 형제 순서에
        // 끼워 넣어 둘 다 막 위에 그려지게 한다.
        Transform parent = panel.transform.parent;
        if (parent != null)
        {
            dim = new GameObject("ChoiceDim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(parent, false);
            int index = panel.transform.GetSiblingIndex();
            Transform dialogueRoot = FindChildOf(parent, dialoguePanel != null ? dialoguePanel.transform : null);
            if (dialogueRoot != null) index = Mathf.Min(index, dialogueRoot.GetSiblingIndex());
            dim.transform.SetSiblingIndex(index);
            var drt = (RectTransform)dim.transform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;
            var dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;   // 선택지가 떠 있는 동안 뒤의 조사 오브젝트가 눌리지 않게
            dim.SetActive(panel.activeInHierarchy);
        }

        StyleChildren();
    }

    // 패널이 켜질 때: 어두운 막을 켜고, 선택지를 살짝 떠오르듯 보여준다.
    private void OnEnable()
    {
        if (dim != null) dim.SetActive(true);
        if (group != null)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeIn());
        }
    }

    private void OnDisable()
    {
        if (dim != null) dim.SetActive(false);
        if (group != null) group.alpha = 1f;
    }

    private void OnDestroy()
    {
        if (dim != null) Destroy(dim);
    }

    private IEnumerator FadeIn()
    {
        const float duration = 0.2f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / duration;
            yield return null;
        }
        group.alpha = 1f;
        fadeRoutine = null;
    }

    // 숫자키 1~9로 고르기 (위에서부터 1, 2, 3 …)
    private void Update()
    {
        for (int i = 0; i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
            int n = 0;
            foreach (Transform child in transform)
            {
                var row = child.GetComponent<ChoiceRow>();
                if (row == null) continue;
                if (n++ == i) { row.Click(); return; }
            }
        }
    }

    // 버튼이 새로 생길 때마다 불린다.
    private void OnTransformChildrenChanged() => StyleChildren();

    private void StyleChildren()
    {
        foreach (Transform child in transform)
        {
            if (child.GetComponent<ChoiceRow>() == null) child.gameObject.AddComponent<ChoiceRow>().Build();
        }
    }

    // t의 조상 중 parent의 바로 아래 자식을 찾는다 (없으면 null).
    private static Transform FindChildOf(Transform parent, Transform t)
    {
        while (t != null && t.parent != parent) t = t.parent;
        return t;
    }

    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

    // =================================================================================
    // 선택지 한 줄 (ChoiceButton 프리팹에 모양을 입힌다)
    // =================================================================================
    private class ChoiceRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image bg;
        private Image[] border;
        private Image accentBar;
        private TMP_Text label;
        private Button button;

        public ChoiceRow Build()
        {
            var rt = (RectTransform)transform;

            // 프리팹에 붙은 세로 정렬/크기 맞춤을 끈다. 켜 두면 아래에서 만드는 테두리·막대까지
            // 위에서부터 차례로 쌓아 버려서 테두리가 회색 덩어리가 된다.
            // 높이는 아래 LayoutElement로, 자식 위치는 직접 정한다.
            var innerLayout = GetComponent<VerticalLayoutGroup>();
            if (innerLayout != null) innerLayout.enabled = false;
            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;

            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.minHeight = RowHeight;
            le.preferredHeight = RowHeight;

            bg = GetComponent<Image>();
            if (bg != null) { bg.sprite = null; bg.type = Image.Type.Simple; bg.color = FillColor; }

            button = GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;   // 마우스 올림 모양은 아래 SetHover가 맡는다
                var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            }

            // 테두리 (얇은 선 4개)
            border = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var line = NewImage("Border" + i, rt, LineColor);
                var lrt = line.rectTransform;
                switch (i)
                {
                    case 0: lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 1); lrt.sizeDelta = new Vector2(0, 1); break;
                    case 1: lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0); lrt.sizeDelta = new Vector2(0, 1); break;
                    case 2: lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0, 1); lrt.pivot = new Vector2(0, 0.5f); lrt.sizeDelta = new Vector2(1, 0); break;
                    default: lrt.anchorMin = new Vector2(1, 0); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(1, 0.5f); lrt.sizeDelta = new Vector2(1, 0); break;
                }
                lrt.anchoredPosition = Vector2.zero;
                border[i] = line;
            }

            // 마우스 올림 때만 보이는 왼쪽 강조 막대
            accentBar = NewImage("AccentBar", rt, AccentColor);
            PlaceLeftColumn(accentBar.rectTransform, 0f, 3f, 0f);
            accentBar.enabled = false;

            // 선택지 글자 (프리팹에 들어 있는 TMP 글자를 옮겨 쓴다 - 부르는 쪽이 이미 글을 넣어 둠)
            label = GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                var lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(TextPadding, 6f);
                lrt.offsetMax = new Vector2(-TextPadding, -6f);
                label.fontSize = 22;
                label.enableAutoSizing = true;          // 긴 선택지는 줄이 넘치지 않게 글자를 줄인다
                label.fontSizeMin = 16;
                label.fontSizeMax = 22;
                label.fontStyle = FontStyles.Normal;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.color = TextColor;
                label.raycastTarget = false;
            }

            SetHover(false);
            return this;
        }

        public void Click()
        {
            if (button != null && button.interactable) button.onClick.Invoke();
        }

        public void OnPointerEnter(PointerEventData e) => SetHover(true);
        public void OnPointerExit(PointerEventData e) => SetHover(false);
        private void OnDisable() => SetHover(false);

        private void SetHover(bool on)
        {
            if (bg != null) bg.color = on ? FillHover : FillColor;
            if (border != null) foreach (var b in border) if (b != null) b.color = on ? AccentColor : LineColor;
            if (accentBar != null) accentBar.enabled = on;
            if (label != null) label.color = on ? TextHover : TextColor;
        }

        private static Image NewImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // 왼쪽에서 x만큼 떨어진 세로 선 (위아래 inset만큼 띄움)
        private static void PlaceLeftColumn(RectTransform rt, float x, float width, float inset)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(x, inset);
            rt.offsetMax = new Vector2(x + width, -inset);
        }
    }
}
