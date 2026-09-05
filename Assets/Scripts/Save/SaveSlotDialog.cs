using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 세이브포인트에 도착했을 때 뜨는 "어느 슬롯에 저장할까요?" 창
// =====================================================================================
// 시나리오 문서의 {세이브포인트}를 지날 때마다 이 창이 떠서, 플레이어가 슬롯을 골라
// 저장하거나 그냥 넘어갈 수 있게 한다.
//
// ===== 왜 슬롯을 고르게 하나 =====
// 세이브포인트가 9곳이라 슬롯도 9개다. 자동으로 한 곳에 덮어써 버리면 앞 지점으로
// 되돌아갈 수가 없다. 어느 칸에 남길지 플레이어가 정하면, 나중에 다른 선택지를 보려고
// 특정 지점부터 다시 시작하기 쉬워진다.
//
// ===== 씬 배치 =====
// 씬에 미리 만들어둘 것이 없다. GameBootstrap이 게임 씬에 자동으로 만들고,
// SavePointManager가 세이브포인트를 지날 때 알아서 띄운다.
public class SaveSlotDialog : MonoBehaviour
{
    public static SaveSlotDialog Instance;

    [Header("자동 생성 시 사용할 캔버스 (비워두면 씬에서 찾는다)")]
    public Canvas targetCanvas;

    // 창 전체. 이걸 켜고 끄는 것으로 여닫는다.
    private GameObject panel;
    private TMP_Text titleLabel;
    private TMP_Text[] slotLabels;

    // 다른 스크립트가 "지금 저장 창이 열려 있나?"를 확인할 때 쓴다.
    // 열려 있는 동안에는 대사가 클릭으로 넘어가면 안 된다.
    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        BuildUI();
        if (panel != null) panel.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 열기 / 닫기
    // ---------------------------------------------------------------------------------

    // SavePointManager가 세이브포인트에 도착했을 때 호출한다.
    public void Open(string savePointName)
    {
        if (panel == null) return;

        if (titleLabel != null)
        {
            titleLabel.text = string.IsNullOrEmpty(savePointName)
                ? "저장할 슬롯을 고르세요"
                : $"[{savePointName}] 저장할 슬롯을 고르세요";
        }

        RefreshSlots();
        panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    // 슬롯마다 "무엇이 저장되어 있는지"를 다시 읽어 표시한다.
    private void RefreshSlots()
    {
        if (SaveManager.Instance == null) return;

        for (int i = 0; i < slotLabels.Length; i++)
        {
            SaveData data = SaveManager.Instance.Load(i);

            if (data != null)
            {
                string playTime = SavePointManager.FormatPlayTime(data.playTimeSeconds);
                slotLabels[i].text = $"{i + 1}. {data.chapterId}\n<size=70%>{data.timestamp} · {playTime}</size>";
            }
            else
            {
                slotLabels[i].text = $"{i + 1}. <color=#888888>비어 있음</color>";
            }
        }
    }

    // 슬롯을 골랐을 때: 그 칸에 저장하고 창을 닫는다.
    private void OnClickSlot(int slotIndex)
    {
        if (SavePointManager.Instance == null) return;

        bool ok = SavePointManager.Instance.SaveToSlot(slotIndex);
        if (!ok)
        {
            if (titleLabel != null) titleLabel.text = "저장할 수 없습니다.";
            return;
        }

        // 방금 저장된 내용을 잠깐 보여주고 닫는다.
        RefreshSlots();
        Close();
    }

    // ---------------------------------------------------------------------------------
    // UI 만들기
    // ---------------------------------------------------------------------------------
    private void BuildUI()
    {
        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[SaveSlotDialog] 씬에 Canvas가 없어 저장 창을 만들 수 없습니다.");
            return;
        }

        int slotCount = SaveManager.SlotCount;
        slotLabels = new TMP_Text[slotCount];

        // ----- 창 전체 (화면을 덮는 어두운 배경) -----
        panel = new GameObject("SaveSlotDialog", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(targetCanvas.transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        // 다른 UI보다 항상 위에 뜨도록 계층 맨 끝으로.
        panel.transform.SetAsLastSibling();

        // ----- 가운데 상자 -----
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(760f, 720f);
        box.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.97f);

        // ----- 제목 -----
        titleLabel = CreateText(box.transform, "Title",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(20f, -70f), new Vector2(-20f, -16f), 30, TextAlignmentOptions.Center);
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.color = new Color(1f, 0.86f, 0.45f);

        // ----- 슬롯 목록 -----
        // 세이브포인트 개수만큼 세로로 쌓는다.
        const float slotHeight = 60f;
        const float slotGap = 8f;
        float listTop = -86f;

        for (int i = 0; i < slotCount; i++)
        {
            int index = i;   // 람다가 반복 변수를 그대로 잡지 않도록 복사

            var slot = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(box.transform, false);

            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(24f, 0f);
            rt.offsetMax = new Vector2(-24f, 0f);
            rt.anchoredPosition = new Vector2(0f, listTop - i * (slotHeight + slotGap));
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, slotHeight);

            var bg = slot.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.10f);
            bg.raycastTarget = true;

            var btn = slot.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.7f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.35f);
            btn.colors = colors;
            btn.onClick.AddListener(() => OnClickSlot(index));

            slotLabels[i] = CreateText(slot.transform, "Label",
                Vector2.zero, Vector2.one,
                new Vector2(16f, 0f), new Vector2(-16f, 0f), 20, TextAlignmentOptions.Left);

        }

        // ----- 저장 안 함 버튼 -----
        var skip = new GameObject("Btn_Skip", typeof(RectTransform), typeof(Image), typeof(Button));
        skip.transform.SetParent(box.transform, false);
        var skipRt = skip.GetComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(0.5f, 0f);
        skipRt.anchorMax = new Vector2(0.5f, 0f);
        skipRt.pivot = new Vector2(0.5f, 0f);
        skipRt.anchoredPosition = new Vector2(0f, 18f);
        skipRt.sizeDelta = new Vector2(220f, 48f);

        var skipBg = skip.GetComponent<Image>();
        skipBg.color = new Color(1f, 1f, 1f, 0.16f);
        skipBg.raycastTarget = true;

        var skipBtn = skip.GetComponent<Button>();
        skipBtn.targetGraphic = skipBg;
        skipBtn.onClick.AddListener(Close);

        var skipLabel = CreateText(skip.transform, "Label",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22, TextAlignmentOptions.Center);
        skipLabel.text = "저장하지 않고 계속";

        // 코드로 만든 글자는 기본 글꼴에 한글이 없어 깨지므로, 화면에서 한글이 잘 나오는
        // 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(panel);
    }

    private TMP_Text CreateText(Transform parent, string name,
                                Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 offsetMin, Vector2 offsetMax,
                                float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
