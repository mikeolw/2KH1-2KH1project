using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
// 미니게임 화면 공통 모양 (청회색 테마)
// =====================================================================================
// 미션 카드(MinigameController), 타임어택 타이머(TimeAttackController), 타이밍 클릭,
// 금고 키패드, 복도 감시가 같은 색/테두리/버튼을 쓰도록 모아 둔다.
// 색은 선택지 화면(ChoicePanelStyle)·자료 뷰어와 같은 값이다.
public static class MinigameTheme
{
    public static readonly Color Accent     = Hex(0x8FA4B7);
    public static readonly Color Text       = Hex(0xD2DAE1);
    public static readonly Color TextBright = Hex(0xE4EAEF);
    public static readonly Color TextDim    = Hex(0x8C9AA6);
    public static readonly Color Line       = Hex(0x34414D);
    // Linear 색 공간에서는 반투명 알파가 생각보다 훨씬 비쳐 보여서(0.96도 뒤 배경이 번진다) 카드/버튼은 불투명으로 둔다.
    public static readonly Color Fill       = Hex(0x10161D);
    public static readonly Color ButtonFill = Hex(0x18222D);   // 카드 위 버튼은 카드보다 한 단계 밝게
    public static readonly Color ButtonHover = Hex(0x22303D);
    public static readonly Color Well       = Hex(0x0A0D10);                              // 입력칸 등 움푹한 곳
    public static readonly Color Danger     = Hex(0xE25A4F);
    public static readonly Color Ok         = Hex(0x6FCF97);

    public static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ---------------------------------------------------------------------------------
    // 부품
    // ---------------------------------------------------------------------------------
    public static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 가운데 기준으로 크기/위치 잡기.
    public static RectTransform Center(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    public static TMP_Text AddText(Transform parent, string name, float size, Color color,
                                   TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UIFontHelper.Apply(text);
        return text;
    }

    // 얇은 테두리 4개. 마우스 올림 때 색을 바꾸려고 돌려준다.
    public static Image[] AddBorder(RectTransform rt, Color color, float width = 1f)
    {
        var lines = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var line = AddImage(rt, "Border" + i, color);
            var lrt = line.rectTransform;
            switch (i)
            {
                case 0: lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 1); lrt.sizeDelta = new Vector2(0, width); break;
                case 1: lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0); lrt.sizeDelta = new Vector2(0, width); break;
                case 2: lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0, 1); lrt.pivot = new Vector2(0, 0.5f); lrt.sizeDelta = new Vector2(width, 0); break;
                default: lrt.anchorMin = new Vector2(1, 0); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(1, 0.5f); lrt.sizeDelta = new Vector2(width, 0); break;
            }
            lrt.anchoredPosition = Vector2.zero;
            lines[i] = line;
        }
        return lines;
    }

    // 카드 바탕: 어두운 면 + 테두리 + 위쪽 강조선.
    public static void StyleCard(RectTransform card)
    {
        var bg = card.GetComponent<Image>();
        if (bg == null) bg = card.gameObject.AddComponent<Image>();
        bg.sprite = null;
        bg.type = Image.Type.Simple;
        bg.color = Fill;
        AddBorder(card, Line);
        var top = AddImage(card, "TopAccent", Accent);
        var trt = top.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(0, 3); trt.anchoredPosition = Vector2.zero;
    }

    // 선택지와 같은 모양의 버튼 (어두운 면 + 테두리, 마우스 올림 때 밝아지고 왼쪽 막대).
    public static Button MakeButton(Transform parent, string name, string label, Vector2 size, Vector2 pos,
                                    Action onClick, float fontSize = 22f)
    {
        var img = AddImage(parent, name, ButtonFill);
        img.raycastTarget = true;
        var rt = Center(img.rectTransform, size, pos);

        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None;
        var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        var text = AddText(rt, "Label", fontSize, Text);
        Stretch(text.rectTransform);
        text.text = label;

        var hover = img.gameObject.AddComponent<ThemedButtonHover>();
        hover.Init(img, AddBorder(rt, Line), text);
        return button;
    }

    // ---------------------------------------------------------------------------------
    // 코드로 그리는 원형 그림 (링/원판/가장자리 붉은 테두리)
    // ---------------------------------------------------------------------------------
    private static readonly Dictionary<int, Sprite> ringCache = new Dictionary<int, Sprite>();

    // 256x256 안에 바깥 반지름이 꽉 찬 링. 가장자리는 1픽셀 부드럽게.
    public static Sprite RingSprite(float thickness)
    {
        int key = Mathf.RoundToInt(thickness * 10f);
        if (ringCache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int size = 256;
        float outer = size / 2f - 2f;
        float inner = outer - thickness;
        var sprite = BuildRadial(size, d => Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f));
        ringCache[key] = sprite;
        return sprite;
    }

    private static Sprite discCache;
    public static Sprite DiscSprite()
    {
        if (discCache != null) return discCache;
        const int size = 64;
        float r = size / 2f - 1f;
        discCache = BuildRadial(size, d => Mathf.Clamp01(r - d + 0.5f));
        return discCache;
    }

    // 가운데는 투명, 가장자리로 갈수록 진해지는 테두리 (피격 연출용).
    private static Sprite vignetteCache;
    public static Sprite VignetteSprite()
    {
        if (vignetteCache != null) return vignetteCache;
        const int size = 256;
        float half = size / 2f;
        vignetteCache = BuildRadial(size, d =>
        {
            float t = Mathf.Clamp01((d / half - 0.55f) / 0.6f);
            return t * t * (3f - 2f * t);
        });
        return vignetteCache;
    }

    private static Sprite BuildRadial(int size, Func<float, float> alphaAtDistance)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var center = new Vector2(size / 2f, size / 2f);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAtDistance(d)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ---------------------------------------------------------------------------------
    // 피격 연출: 화면 흔들기 + 가장자리 붉은 번쩍임
    // ---------------------------------------------------------------------------------
    // 캔버스 바로 밑의 모든 화면(배경, 대화창, 조사 화면 …)을 같이 흔든다.
    // 흔드는 동안 다른 코드가 위치를 바꿔도 그 변화는 살리도록, 매 프레임 "지난번에 더한 만큼 빼고
    // 이번 만큼 더한다". 끝나면 더한 만큼만 빼서 원래 자리로 돌려놓는다.
    public static void PlayHit(Canvas canvas, float duration = 0.35f, float strength = 18f)
    {
        if (canvas == null) return;
        var runner = canvas.GetComponent<HitEffectRunner>();
        if (runner == null) runner = canvas.gameObject.AddComponent<HitEffectRunner>();
        runner.Play(duration, strength);
    }

    // =================================================================================
    private class ThemedButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image bg;
        private Image[] border;
        private TMP_Text label;

        public void Init(Image background, Image[] borderLines, TMP_Text text)
        {
            bg = background; border = borderLines; label = text;
            Set(false);
        }

        public void OnPointerEnter(PointerEventData e) => Set(true);
        public void OnPointerExit(PointerEventData e) => Set(false);
        private void OnDisable() => Set(false);

        private void Set(bool on)
        {
            if (bg != null) bg.color = on ? ButtonHover : ButtonFill;
            if (border != null) foreach (var b in border) if (b != null) b.color = on ? Accent : Line;
            if (label != null) label.color = on ? TextBright : Text;
        }
    }

    private class HitEffectRunner : MonoBehaviour
    {
        private Coroutine routine;
        private Image vignette;
        private readonly List<RectTransform> targets = new List<RectTransform>();
        private readonly List<Vector2> applied = new List<Vector2>();

        public void Play(float duration, float strength)
        {
            if (routine != null) { StopCoroutine(routine); Restore(); }
            routine = StartCoroutine(Run(duration, strength));
        }

        private IEnumerator Run(float duration, float strength)
        {
            EnsureVignette();
            targets.Clear();
            applied.Clear();
            foreach (Transform child in transform)
            {
                if (child == vignette.transform) continue;
                if (child is RectTransform rt) { targets.Add(rt); applied.Add(Vector2.zero); }
            }

            vignette.transform.SetAsLastSibling();
            vignette.gameObject.SetActive(true);

            const float flashDuration = 0.55f;
            float total = Mathf.Max(duration, flashDuration);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float shake = t < duration ? 1f - t / duration : 0f;
                Vector2 offset = shake > 0f ? UnityEngine.Random.insideUnitCircle * strength * shake * shake : Vector2.zero;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] == null) continue;
                    targets[i].anchoredPosition += offset - applied[i];
                    applied[i] = offset;
                }

                float flash = 1f - Mathf.Clamp01(t / flashDuration);
                vignette.color = WithAlpha(Danger, 0.6f * flash * flash);
                yield return null;
            }

            Restore();
            vignette.gameObject.SetActive(false);
            routine = null;
        }

        private void Restore()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) targets[i].anchoredPosition -= applied[i];
            }
            targets.Clear();
            applied.Clear();
        }

        private void EnsureVignette()
        {
            if (vignette != null) return;
            vignette = AddImage(transform, "HitVignette", WithAlpha(Danger, 0f));
            vignette.sprite = VignetteSprite();
            Stretch(vignette.rectTransform);
            vignette.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            Restore();
            if (vignette != null) vignette.gameObject.SetActive(false);
        }
    }
}
