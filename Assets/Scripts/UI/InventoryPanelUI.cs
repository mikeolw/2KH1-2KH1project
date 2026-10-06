using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 가방(인벤토리) 화면 - 획득한 아이템 목록 + 설명 보기 + 아이템 조합
// =====================================================================================
// ===== 기존 InventorySlotUI와 뭐가 다른가? =====
//   InventorySlotUI : 수첩 안에 아이템 개수만큼 자리를 "미리 손으로 배치"해두고, 얻은
//                     아이템의 자리만 켜는 방식. 아이템이 늘어날 때마다 씬을 열어
//                     자리를 하나씩 더 만들어야 한다.
//   이 스크립트      : 얻은 아이템 목록을 보고 버튼을 "그때그때 만들어서" 채운다.
//                     아이템이 늘어나도 ItemData.csv에 한 줄 추가하면 끝이고, 씬은
//                     손댈 필요가 없다. (기존 InventorySlotUI도 그대로 쓸 수 있다 -
//                     둘은 서로 간섭하지 않는다.)
//
// ===== 조작 방법 =====
//   - 아이템을 한 번 누르면 오른쪽에 이름과 설명이 나온다.
//   - 서류/사진처럼 펼쳐 볼 수 있는 아이템이면 "자세히 보기" 버튼이 함께 뜬다.
//   - "조합하기"를 누른 뒤 다른 아이템을 누르면 두 아이템을 합쳐본다.
//     (예: SD카드를 고르고 조합하기 -> 카메라를 누르면 사진을 확인할 수 있다)
//     합칠 수 없는 조합이면 "이 둘은 같이 쓸 수 없다"고 알려준다.
//   - 카메라를 고르면 "작동하기" 버튼이 뜬다. 누르면 가방이 닫히고 카메라 화면이 열린다.
//     (플레이 화면 퀵바의 카메라 버튼은 없앴다 - 카메라는 가방 안에서만 쓸 수 있다.
//      OnOperateClicked / UIManager.OpenCamera / GameBootstrap.EnsurePanelUI 참고)
//
// ===== 화면 구성 (Figma "Screen / Inventory" - 청회색 테마) =====
//   [검은 막 + 패널 그림(InventoryPanel.png, 패널 x160 y130 1120x820)]
//     INVENTORY / 가방  N개                                   [X]
//     ┌ 아이템 칸 4열 (132x132) ┐ │ [고른 아이템 그림]
//     │  고른 칸 = 모서리 표시    │ │ 분류
//     │  조합 재료 = 점선 + 재료1 │ │ 아이템 이름 / 설명
//     └─────────────────────┘ │
//     [안내 문구 줄]              │ [작동하기] [자세히 보기] [조합하기] (보이는 것만 아래부터 쌓기)
// 그림은 Assets/Resources/Illusts/UI/Inventory/ 와 Settings/ (버튼) 에 있다 (git 제외 - 드라이브 공유).
// 그림이 없으면 같은 자리에 색 도형으로 대신 그린다.
//
// ===== 씬 배치 =====
// 인스펙터 필드를 비워두면 게임 시작 시 스스로 UI를 만든다. UIManager.inventoryPanel에
// 이 스크립트가 붙은 GameObject를 연결해두면 퀵바의 가방 버튼으로 여닫을 수 있다.
public class InventoryPanelUI : MonoBehaviour
{
    [Header("UI 연결 (비워두면 자동 생성)")]
    [Tooltip("아이템 버튼들이 채워질 부모. GridLayoutGroup이 붙어 있으면 격자로 정렬된다.")]
    public Transform itemListContainer;
    [Tooltip("아이템 버튼으로 복제해서 쓸 프리팹. 비워두면 코드로 간단한 버튼을 만든다.")]
    public GameObject itemButtonPrefab;
    [Tooltip("고른 아이템의 이름")]
    public TMP_Text selectedNameText;
    [Tooltip("고른 아이템의 설명")]
    public TMP_Text selectedDescriptionText;
    [Tooltip("고른 아이템의 큰 그림")]
    public Image selectedIconImage;
    [Tooltip("서류/사진을 펼쳐 보는 버튼")]
    public Button viewDetailButton;
    [Tooltip("조합 모드로 들어가는 버튼")]
    public Button combineButton;
    [Tooltip("카메라처럼 '작동'시킬 수 있는 아이템을 쓰는 버튼")]
    public Button operateButton;
    [Tooltip("조합 결과나 안내 문구를 띄우는 텍스트")]
    public TMP_Text messageText;

    // 지금 고른 아이템의 ItemId.
    private string selectedItemId;

    // 조합 모드인지. true인 상태에서 다른 아이템을 누르면 조합을 시도한다.
    private bool combineMode;

    // 조합 모드로 들어갈 때 "첫 번째 재료"로 잡아둔 아이템.
    private string combineSourceItemId;

    // 지금 화면에 만들어둔 아이템 버튼들(빈 칸 포함). 목록을 새로 그릴 때 지우기 위해 들고 있는다.
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    // ===== 코드로 만든 화면에서만 쓰는 것들 =====
    // 아이템 칸 하나의 모양을 바꾸려고 들고 있는다 (고른 칸 / 조합 재료 칸 표시).
    private class SlotView
    {
        public Image background;
        public TMP_Text name;
        public GameObject badge;   // "재료 1"
    }
    private readonly Dictionary<string, SlotView> slotViews = new Dictionary<string, SlotView>();
    private bool codeBuiltUI;
    private TMP_Text countText;
    private TMP_Text categoryText;
    private Image messageBar;
    private RectTransform itemListViewport;

    // ===== 화면 크기 기준값 (Figma "Screen / Inventory", 패널 안 왼쪽 위 기준 px) =====
    private const float BoxWidth = 1120f;
    private const float BoxHeight = 820f;
    private const float Pad = 56f;
    private const float GridTop = 160f;
    private const float SlotSize = 132f;
    private const float SlotGap = 16f;
    private const int GridColumns = 4;
    private const int MinSlots = 12;                 // 아이템이 적어도 빈 칸을 채워 4x3 격자를 보여준다
    private const float GridWidth = SlotSize * GridColumns + SlotGap * (GridColumns - 1);   // 576
    private const float GridHeight = SlotSize * 3f + SlotGap * 2f;                          // 428 (넘치면 스크롤)
    private const float DetailX = Pad + GridWidth + 48f;                                    // 680
    private const float DetailWidth = BoxWidth - Pad - DetailX;                             // 384
    private const float ButtonHeight = 54f;
    private const float ButtonGap = 12f;

    // ===== 색 =====
    private static readonly Color Accent = new Color(0.561f, 0.643f, 0.718f);       // 8FA4B7
    private static readonly Color TitleColor = new Color(0.894f, 0.918f, 0.937f);   // E4EAEF
    private static readonly Color KickerColor = new Color(0.435f, 0.518f, 0.588f);  // 6F8496
    private static readonly Color LabelColor = new Color(0.788f, 0.835f, 0.878f);   // C9D5E0
    private static readonly Color ActiveColor = new Color(0.851f, 0.89f, 0.922f);   // D9E3EB
    private static readonly Color DimColor = new Color(0.424f, 0.486f, 0.545f);     // 6C7C8B
    private static readonly Color HintColor = new Color(0.482f, 0.541f, 0.596f);    // 7B8A98
    private static readonly Color ButtonTextColor = new Color(0.663f, 0.729f, 0.788f);   // A9BAC9
    private static readonly Color PrimaryTextColor = new Color(0.059f, 0.082f, 0.106f);  // 0F151B
    private static readonly Color PrimaryBadgeText = PrimaryTextColor;
    // 그림이 없을 때만 쓰는 색
    private static readonly Color PanelColor = new Color(0.063f, 0.086f, 0.114f, 0.95f);   // 10161D
    private static readonly Color SlotColor = new Color(0.043f, 0.063f, 0.082f, 0.9f);     // 0B1015
    private static readonly Color SlotSelectedColor = new Color(0.094f, 0.133f, 0.176f);   // 18222D
    private static readonly Color LineColor = new Color(0.165f, 0.208f, 0.251f);           // 2A3540
    private static readonly Color OnFillColor = new Color(0.141f, 0.192f, 0.251f);         // 243140
    private static readonly Color MessageBarColor = new Color(0.043f, 0.063f, 0.082f, 0.6f);

    // 그림 경로 (Resources 기준)
    private const string ArtFolder = "Illusts/UI/Inventory/";
    private const string ButtonArtFolder = "Illusts/UI/Settings/";   // 버튼 그림은 환경설정 것을 같이 쓴다
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private const string DefaultHint = "아이템을 눌러 설명을 보세요. 두 개를 합치려면 하나를 고른 뒤 [조합하기]를 누르세요.";

    private void Awake()
    {
        EnsureUI();
    }

    private void OnEnable()
    {
        // 가방을 열 때마다 목록을 새로 그린다(그 사이에 아이템을 얻었을 수 있으므로).
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
            InventoryManager.Instance.OnInventoryChanged += Refresh;
        }

        if (overlay == null) EnsureUI();

        // 실제 화면은 캔버스 아래에 따로 만들어져 있으므로, 이 패널이 켜질 때 함께 켠다.
        if (overlay != null)
        {
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();   // 다른 UI에 가리지 않게
        }

        // 가방을 닫았다 열면 조합 모드는 초기화한다(헷갈림 방지).
        SetCombineMode(false, null);

        Refresh();
        ShowMessage(DefaultHint);
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
        }

        if (overlay != null) overlay.SetActive(false);
    }

    // ---------------------------------------------------------------------------------
    // 목록 그리기
    // ---------------------------------------------------------------------------------

    // 획득한 아이템으로 목록을 다시 채운다.
    public void Refresh()
    {
        if (itemListContainer == null) return;

        // 이전 버튼 정리
        foreach (var go in spawnedButtons)
        {
            if (go == null) continue;
            // 부모에서 먼저 떼어낸다. Destroy()는 프레임 끝에 지워져서, 그냥 지우면 한 프레임 동안
            // 새 칸과 옛 칸이 함께 격자에 잡혀 칸이 밀려 보인다.
            go.transform.SetParent(null, false);
            Destroy(go);
        }
        spawnedButtons.Clear();
        slotViews.Clear();

        if (InventoryManager.Instance == null) return;

        // ItemData.csv에 정의된 순서대로 훑으면서, 실제로 가지고 있는 것만 버튼으로 만든다.
        // (획득 순서가 아니라 CSV 순서를 따르므로 목록이 매번 뒤바뀌지 않아 찾기 쉽다.)
        int count = 0;
        foreach (var info in ItemDatabase.All)
        {
            if (!InventoryManager.Instance.HasItem(info.itemId)) continue;

            CreateItemButton(info);
            count++;
        }

        if (codeBuiltUI)
        {
            // 빈 칸으로 4열 격자를 채운다 (최소 4x3, 아이템이 많으면 마지막 줄까지).
            int total = Mathf.Max(MinSlots, Mathf.CeilToInt(count / (float)GridColumns) * GridColumns);
            for (int i = count; i < total; i++) CreateEmptySlot();
            if (countText != null) countText.text = count + "개";
        }

        // 아무것도 없을 때 안내
        if (count == 0)
        {
            if (selectedNameText != null) selectedNameText.text = "";
            if (selectedDescriptionText != null) selectedDescriptionText.text = "아직 가진 것이 없다.";
            if (selectedIconImage != null) selectedIconImage.enabled = false;
            if (categoryText != null) categoryText.text = "";
            SetButtonVisible(viewDetailButton, false);
            SetButtonVisible(combineButton, false);
            SetButtonVisible(operateButton, false);
            return;
        }

        // 고른 아이템이 조합으로 사라졌을 수도 있으므로 확인한다.
        if (!string.IsNullOrEmpty(selectedItemId) && !InventoryManager.Instance.HasItem(selectedItemId))
        {
            selectedItemId = null;
            ClearSelection();
        }

        UpdateSlotVisuals();
    }

    // 아이템 버튼 하나를 만든다.
    private void CreateItemButton(ItemDatabase.ItemInfo info)
    {
        GameObject go;

        if (itemButtonPrefab != null)
        {
            go = Instantiate(itemButtonPrefab, itemListContainer);
        }
        else
        {
            // 프리팹이 없으면 버튼을 코드로 만든다.
            go = new GameObject($"Item_{info.itemId}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(itemListContainer, false);

            var slotBg = go.GetComponent<Image>();
            SetImage(slotBg, ArtFolder + "Slot_Default", SlotColor);
            slotBg.raycastTarget = true;

            // targetGraphic을 지정해야 클릭 판정과 색 변화가 동작한다.
            // (이게 빠져 있어서 아이템을 눌러도 아무 반응이 없었다)
            var slotBtn = go.GetComponent<Button>();
            slotBtn.targetGraphic = slotBg;
            ApplyButtonColors(slotBtn);
        }

        // 아이콘: 버튼 안에 Image가 두 개 이상이면 두 번째를 아이콘으로 본다.
        // (첫 번째는 버튼 배경)
        var images = go.GetComponentsInChildren<Image>();
        if (images.Length > 1 && images[1] != null)
        {
            Sprite icon = info.GetIcon();
            if (icon != null)
            {
                images[1].sprite = icon;
                images[1].preserveAspect = true;
                images[1].enabled = true;
            }
        }
        else if (itemButtonPrefab == null)
        {
            // 코드로 만든 버튼이면 아이콘용 Image를 하나 더 붙인다. 칸 위쪽 (16, 12) ~ (116, 92).
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(16f / SlotSize, 40f / SlotSize);
            iconRt.anchorMax = new Vector2(116f / SlotSize, 120f / SlotSize);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = info.GetIcon();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = iconImg.sprite != null;
        }

        // 이름 라벨 (칸 아래쪽)
        var label = go.GetComponentInChildren<TMP_Text>();
        if (label == null && itemButtonPrefab == null)
        {
            var textGo = new GameObject("Name", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0f);
            textRt.pivot = new Vector2(0.5f, 0f);
            textRt.offsetMin = new Vector2(6f, 12f);
            textRt.offsetMax = new Vector2(-6f, 36f);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.Center;
            label.color = LabelColor;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (label != null) label.text = info.displayName;

        // 조합 재료 표시 ("재료 1", 칸 왼쪽 위에 걸친 작은 딱지)
        if (itemButtonPrefab == null)
        {
            var view = new SlotView { background = go.GetComponent<Image>(), name = label };
            view.badge = CreateBadge(go.transform);
            slotViews[info.itemId] = view;
        }

        // 클릭 처리
        var button = go.GetComponent<Button>();
        if (button != null)
        {
            string capturedId = info.itemId; // 람다가 반복 변수를 잡지 않도록 복사
            button.onClick.AddListener(() => OnItemClicked(capturedId));
        }

        // 아이템 버튼은 가방을 열 때마다 새로 만들어지므로 여기서도 글꼴을 물려준다.
        UIFontHelper.ApplyToChildren(go);

        spawnedButtons.Add(go);
    }

    // 아무 아이템도 없는 빈 칸 (누를 수 없음).
    private void CreateEmptySlot()
    {
        var go = new GameObject("EmptySlot", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(itemListContainer, false);
        var img = go.GetComponent<Image>();
        SetImage(img, ArtFolder + "Slot_Empty", new Color(SlotColor.r, SlotColor.g, SlotColor.b, 0.45f));
        img.raycastTarget = false;
        spawnedButtons.Add(go);
    }

    private GameObject CreateBadge(Transform slot)
    {
        var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        badge.transform.SetParent(slot, false);
        var rt = badge.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(8f, 0f);
        rt.sizeDelta = new Vector2(52f, 20f);
        var img = badge.GetComponent<Image>();
        img.color = Accent;
        img.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(badge.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var t = textGo.AddComponent<TextMeshProUGUI>();
        t.text = "재료 1";
        t.fontSize = 12;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = PrimaryBadgeText;
        t.raycastTarget = false;

        badge.SetActive(false);
        return badge;
    }

    // 칸 모양: 조합 재료 > 고른 칸 > 기본.
    private void UpdateSlotVisuals()
    {
        foreach (var pair in slotViews)
        {
            var view = pair.Value;
            bool isSource = combineMode && pair.Key == combineSourceItemId;
            bool isSelected = !isSource && pair.Key == selectedItemId;

            string art = isSource ? "Slot_Combine" : isSelected ? "Slot_Selected" : "Slot_Default";
            SetImage(view.background, ArtFolder + art, isSource || isSelected ? SlotSelectedColor : SlotColor);

            if (view.name != null)
            {
                view.name.color = isSource || isSelected ? ActiveColor : LabelColor;
                view.name.fontStyle = isSource || isSelected ? FontStyles.Bold : FontStyles.Normal;
            }
            if (view.badge != null) view.badge.SetActive(isSource);
        }
    }

    // ---------------------------------------------------------------------------------
    // 아이템 선택 / 조합
    // ---------------------------------------------------------------------------------

    private void OnItemClicked(string itemId)
    {
        // 조합 모드라면: 지금 누른 아이템을 두 번째 재료로 보고 합쳐본다.
        if (combineMode && !string.IsNullOrEmpty(combineSourceItemId))
        {
            TryCombineWith(itemId);
            return;
        }

        // 평소에는 그냥 아이템을 고른 것으로 처리한다.
        selectedItemId = itemId;
        ShowSelectedItem(itemId);
        ShowMessage("");
    }

    // 고른 아이템의 이름/설명/그림을 오른쪽에 표시한다.
    private void ShowSelectedItem(string itemId)
    {
        var info = ItemDatabase.Get(itemId);

        if (selectedNameText != null)
            selectedNameText.text = info != null ? info.displayName : itemId;

        if (selectedDescriptionText != null)
            selectedDescriptionText.text = info != null ? info.description : "";

        if (selectedIconImage != null)
        {
            Sprite icon = info != null ? info.GetIcon() : null;
            selectedIconImage.sprite = icon;
            selectedIconImage.preserveAspect = true;
            selectedIconImage.enabled = icon != null;
        }

        // 서류/사진처럼 펼쳐 볼 수 있는 아이템일 때만 "자세히 보기" 버튼을 보여준다.
        bool canView = info != null && info.viewerType != ItemDatabase.ItemViewerType.None;
        SetButtonVisible(viewDetailButton, canView);

        // 조합 버튼은 아이템을 고른 상태면 항상 보여준다
        // (조합 가능 여부는 눌러봐야 알 수 있고, 미리 알려주면 정답을 알려주는 셈이 된다).
        SetButtonVisible(combineButton, true);

        // 카메라처럼 작동시킬 수 있는 아이템일 때만 "작동하기" 버튼을 보여준다.
        SetButtonVisible(operateButton, IsOperable(itemId));

        UpdateCategoryText();
        UpdateSlotVisuals();
    }

    // 이름 위의 작은 분류 표기: 도구(작동 가능) / 자료(펼쳐 볼 수 있음) / 증거물 (+ 조합 재료).
    private void UpdateCategoryText()
    {
        if (categoryText == null) return;
        if (string.IsNullOrEmpty(selectedItemId)) { categoryText.text = ""; return; }

        var info = ItemDatabase.Get(selectedItemId);
        string kind = IsOperable(selectedItemId) ? "도구"
            : info != null && info.viewerType != ItemDatabase.ItemViewerType.None ? "자료"
            : "증거물";
        bool isSource = combineMode && selectedItemId == combineSourceItemId;
        categoryText.text = isSource ? kind + "  ·  조합 재료" : kind;
    }

    private void ClearSelection()
    {
        selectedItemId = null;
        if (selectedNameText != null) selectedNameText.text = "";
        if (selectedDescriptionText != null) selectedDescriptionText.text = "";
        if (selectedIconImage != null) selectedIconImage.enabled = false;
        if (categoryText != null) categoryText.text = "";
        SetButtonVisible(viewDetailButton, false);
        SetButtonVisible(combineButton, false);
        SetButtonVisible(operateButton, false);
        UpdateSlotVisuals();
    }

    // "자세히 보기" 버튼. 서류/사진을 전체 화면 뷰어로 펼친다.
    public void OnViewDetailClicked()
    {
        if (string.IsNullOrEmpty(selectedItemId)) return;
        if (DocumentViewerController.Instance == null) return;

        DocumentViewerController.Instance.ShowItem(selectedItemId);
    }

    // ---------------------------------------------------------------------------------
    // 아이템 작동 (카메라)
    // ---------------------------------------------------------------------------------
    // ===== 왜 가방 안에서만 쓰나? =====
    // 예전에는 플레이 화면 하단 퀵바에 카메라 버튼이 있어서, 카메라를 줍기도 전에 언제든
    // 카메라 화면을 열 수 있었다. 이제는 "가방에서 카메라를 고른 뒤 [작동하기]"를 눌러야만
    // 쓸 수 있다. 가방에 카메라가 있어야 버튼이 보이므로, 자연스럽게 카메라를 얻은 뒤에만 쓸 수 있다.
    // (퀵바의 카메라 버튼은 GameBootstrap이 숨긴다.)
    //
    // ===== 작동시킬 수 있는 아이템 =====
    // 지금은 카메라 두 종류(SD카드 꽂기 전/후)뿐이다. 나중에 작동시킬 아이템이 늘어나면
    // 아래 목록에 ItemId를 추가하고, OnOperateClicked()에 그 아이템이 할 일을 적으면 된다.
    private static readonly HashSet<string> CameraItemIds = new HashSet<string>
    {
        "camera",               // 카메라
        "camera_with_photos",   // SD카드를 꽂은 카메라 (조합 결과)
    };

    private static bool IsOperable(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && CameraItemIds.Contains(itemId);
    }

    // "작동하기" 버튼. 가방을 닫고 카메라 화면을 연다.
    public void OnOperateClicked()
    {
        if (!IsOperable(selectedItemId)) return;

        if (UIManager.Instance == null)
        {
            ShowMessage("지금은 카메라를 켤 수 없다.");
            return;
        }

        // UIManager.OpenCamera()가 가방 패널을 닫으면 이 스크립트의 OnDisable이 불려
        // 가방 화면(overlay)도 함께 사라진다.
        UIManager.Instance.OpenCamera();
    }

    // "조합하기" 버튼. 지금 고른 아이템을 첫 번째 재료로 잡고 조합 모드로 들어간다.
    //
    // ===== 조합하는 방법 =====
    //   1) 아이템 하나를 누른다 (예: SD카드)
    //   2) [조합하기] 버튼을 누른다 -> 조합 모드로 들어간다
    //   3) 함께 쓸 다른 아이템을 누른다 (예: 카메라) -> 조합 시도
    // 조합이 되면 결과 아이템이 가방에 들어오고 안내 문구가 뜬다.
    // 안 되는 조합이면 "같이 쓸 수 없다"고 알려주고 조합 모드가 풀린다.
    public void OnCombineClicked()
    {
        if (string.IsNullOrEmpty(selectedItemId))
        {
            ShowMessage("먼저 아이템을 하나 고르세요.");
            return;
        }

        // 이미 조합 모드면 취소로 동작한다(같은 버튼으로 켜고 끄기).
        if (combineMode)
        {
            SetCombineMode(false, null);
            ShowMessage("조합을 취소했다.");
            return;
        }

        SetCombineMode(true, selectedItemId);
        ShowMessage($"'{ItemDatabase.GetDisplayName(selectedItemId)}'와(과) 함께 쓸 물건을 고르세요.", highlight: true);
    }

    // 조합 모드를 켜고 끄면서 화면 표시도 함께 바꾼다.
    // 버튼 글씨와 색이 바뀌지 않으면 지금 조합 모드인지 알 수 없어서 혼란스럽다.
    private void SetCombineMode(bool on, string sourceItemId)
    {
        combineMode = on;
        combineSourceItemId = on ? sourceItemId : null;

        UpdateSlotVisuals();
        UpdateCategoryText();

        if (combineButton == null) return;

        var label = combineButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = on ? "조합 취소" : "조합하기";

        var bg = combineButton.GetComponent<Image>();
        if (bg == null) return;

        if (codeBuiltUI)
        {
            // 조합 모드일 땐 채워진 칸(Toggle_On) + 밝은 굵은 글자로 눈에 띄게
            SetSlicedImage(bg, ButtonArtFolder + (on ? "Toggle_On" : "Button_Secondary"), on ? OnFillColor : new Color(0f, 0f, 0f, 0f));
            if (label != null)
            {
                label.color = on ? ActiveColor : ButtonTextColor;
                label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        }
        else
        {
            bg.color = on
                ? new Color(1f, 0.75f, 0.25f, 0.55f)   // 조합 모드일 땐 눈에 띄는 주황빛
                : new Color(1f, 1f, 1f, 0.20f);
        }
    }

    // 조합 모드에서 두 번째 아이템을 눌렀을 때.
    private void TryCombineWith(string targetItemId)
    {
        string sourceId = combineSourceItemId;

        // 조합 모드는 시도 즉시 해제한다(성공/실패 무관).
        SetCombineMode(false, null);

        if (sourceId == targetItemId)
        {
            ShowMessage("같은 물건끼리는 합칠 수 없다.");
            return;
        }

        if (InventoryManager.Instance == null) return;

        bool success = InventoryManager.Instance.TryCombine(sourceId, targetItemId);

        if (success)
        {
            ShowMessage(InventoryManager.Instance.LastCombinationMessage);

            // 조합 결과 아이템을 자동으로 골라준다(바로 설명을 볼 수 있게).
            var rule = ItemDatabase.FindCombination(sourceId, targetItemId);
            if (rule != null)
            {
                selectedItemId = rule.resultItem;
                ShowSelectedItem(rule.resultItem);
            }
        }
        else
        {
            string a = ItemDatabase.GetDisplayName(sourceId);
            string b = ItemDatabase.GetDisplayName(targetItemId);
            ShowMessage($"{a}와(과) {b}는 같이 쓸 수 없다.");
        }
    }

    // 안내 문구 줄. highlight면 조합 모드처럼 밝은 테두리 줄로, 비어 있으면 줄을 숨긴다.
    private void ShowMessage(string message, bool highlight = false)
    {
        if (messageText != null)
        {
            messageText.text = message;
            if (codeBuiltUI) messageText.color = highlight ? ActiveColor : HintColor;
        }

        if (messageBar != null)
        {
            messageBar.gameObject.SetActive(!string.IsNullOrEmpty(message));
            if (highlight) SetSlicedImage(messageBar, ArtFolder + "MessageBar_Combine", OnFillColor);
            else { messageBar.sprite = null; messageBar.color = MessageBarColor; }
        }
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
        LayoutActionButtons();
    }

    // ===== 오른쪽 아래 버튼 정렬 =====
    // [작동하기] [자세히 보기] [조합하기] 세 버튼은 아이템에 따라 보였다 숨었다 한다
    // (카메라는 작동하기+조합하기, 서류는 자세히 보기+조합하기 ...).
    // 자리를 고정해두면 숨은 버튼 자리가 빈칸으로 남아 어색하므로, 보이는 버튼만
    // 오른쪽 페이지 아래에서부터 위로 빈틈없이 쌓는다 (순서는 위에서부터 작동/자세히/조합).
    // 코드로 만든 버튼일 때만 정렬하고, 인스펙터에서 직접 배치한 버튼이면 그 배치를 존중한다.
    private bool autoLayoutButtons;

    private void LayoutActionButtons()
    {
        if (!autoLayoutButtons) return;

        var visible = new List<Button>();
        foreach (var button in new[] { operateButton, viewDetailButton, combineButton })
        {
            if (button != null && button.gameObject.activeSelf) visible.Add(button);
        }

        float y = BoxHeight - Pad - (visible.Count * ButtonHeight + (visible.Count - 1) * ButtonGap);
        foreach (var button in visible)
        {
            PlaceTopLeft((RectTransform)button.transform, DetailX, y, DetailWidth, ButtonHeight);
            y += ButtonHeight + ButtonGap;
        }
    }

    // ---------------------------------------------------------------------------------
    // UI 자동 생성 (씬을 아직 안 꾸민 상태에서도 동작하게 하는 편의 기능)
    // ---------------------------------------------------------------------------------
    // 이 스크립트가 만든 UI를 담는 자식의 이름.
    private const string ContentRootName = "__InventoryContent";

    // 만들어진 UI를 담는 부모. 아래 Create* 함수들이 여기에 붙인다.
    private Transform contentRoot;

    // 캔버스 아래에 만드는 가방 화면. 아래 Create* 함수들이 여기에 붙는다.
    private GameObject overlay;

    // ===== 왜 UI를 캔버스에 직접 만드나 (중요) =====
    // 처음에는 씬의 InventoryPanel 안에 목록을 만들었다. 그런데 그 패널은 프로토타입 시절
    // 크기와 위치가 제각각으로 잡혀 있고 안에 옛날 오브젝트도 남아 있어서, 코드에서 크기를
    // 다시 잡아도 화면 구석에 작게 뜨거나 잘려 보였다.
    // 그래서 가방 화면을 씬의 패널 안이 아니라 캔버스 바로 아래에 따로 만든다.
    // 씬이 어떻게 짜여 있든 영향받지 않으므로 항상 같은 자리에 같은 크기로 뜬다.
    // 씬의 InventoryPanel은 "열렸는지 닫혔는지"를 알려주는 스위치 역할만 한다.
    private void EnsureUI()
    {
        // 아이템 목록 자리가 이미 인스펙터에 연결되어 있으면 손대지 않는다.
        if (itemListContainer != null) return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[InventoryPanelUI] 씬에 Canvas가 없어 가방 화면을 만들 수 없습니다.");
            return;
        }

        // 예전에 만들어둔 게 남아 있으면 지우고 새로 만든다 (모양이 바뀌었을 수 있으므로).
        var existing = canvas.transform.Find(ContentRootName);
        if (existing != null) DestroyImmediate(existing.gameObject);

        codeBuiltUI = true;

        // ----- 씬의 원래 패널은 안 보이게 한다 -----
        // (UIManager가 이 패널을 켜고 끄면서 여닫음을 관리하므로 오브젝트 자체는 남겨둔다)
        var panelImg = GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.color = new Color(0f, 0f, 0f, 0f);
            panelImg.raycastTarget = false;
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }

        // ----- 화면 전체를 덮는 검은 막 (환경설정과 같은 톤) -----
        overlay = new GameObject(ContentRootName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.97f);
        dim.raycastTarget = true;

        // ----- 패널 그림 (1440x1080 한 장: 패널 + 그림자 + 위쪽 강조선 + 구분선) -----
        var panelArt = LoadSprite(ArtFolder + "InventoryPanel");
        if (panelArt != null)
        {
            var art = new GameObject("PanelArt", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(overlay.transform, false);
            var artRt = art.GetComponent<RectTransform>();
            artRt.anchorMin = artRt.anchorMax = artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(1440f, 1080f);
            artRt.anchoredPosition = Vector2.zero;
            var artImg = art.GetComponent<Image>();
            artImg.sprite = panelArt;
            artImg.raycastTarget = false;
        }
        else
        {
            Debug.LogWarning("[InventoryPanelUI] 가방 화면 그림을 찾을 수 없어 색 도형으로 그립니다: Assets/Resources/" + ArtFolder + "InventoryPanel.png");
        }

        // ----- 가운데 패널 (그림 속 패널 자리 x160 y130 1120x820 = 화면 정가운데) -----
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overlay.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        boxRt.anchoredPosition = Vector2.zero;
        var boxImg = box.GetComponent<Image>();
        boxImg.color = panelArt != null ? new Color(0f, 0f, 0f, 0f) : PanelColor;
        boxImg.raycastTarget = false;

        contentRoot = box.transform;

        if (panelArt == null)
        {
            // 그림에 들어 있는 선들을 대신 그린다.
            AddRect("TopAccent", Pad, 0f, 120f, 3f, Accent);
            AddRect("HeadDivider", Pad, 128f, BoxWidth - Pad * 2f, 1f, LineColor);
            AddRect("DetailDivider", DetailX - 24f, GridTop, 1f, 560f, LineColor);
        }

        // ----- 머리말 -----
        var kicker = CreateText("Kicker", Pad, 40f, 300f, 20f, 14, TextAlignmentOptions.TopLeft);
        kicker.text = "INVENTORY";
        kicker.fontStyle = FontStyles.Bold;
        kicker.characterSpacing = 36f;
        kicker.color = KickerColor;

        var title = CreateText("Title", Pad, 58f, 120f, 54f, 36, TextAlignmentOptions.TopLeft);
        title.text = "가방";
        title.fontStyle = FontStyles.Bold;
        title.color = TitleColor;

        countText = CreateText("Count", Pad + 84f, 76f, 120f, 24f, 16, TextAlignmentOptions.TopLeft);
        countText.color = DimColor;

        // 닫기 버튼 (오른쪽 위). 씬의 InventoryPanel을 끄면 OnDisable에서 이 화면도 함께 닫힌다.
        var close = CreateButton("Btn_CloseInventory", "", ArtFolder + "CloseBox", new Color(0f, 0f, 0f, 0f),
                                 () => gameObject.SetActive(false));
        PlaceTopLeft((RectTransform)close.transform, BoxWidth - Pad - 48f, 48f, 48f, 48f);
        if (LoadSprite(ArtFolder + "CloseBox") == null)
        {
            var x = close.GetComponentInChildren<TMP_Text>();
            x.text = "X";
            x.fontSize = 22;
        }

        // ----- 왼쪽: 아이템 격자 (3줄이 넘으면 스크롤) -----
        var viewport = new GameObject("ItemListViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(contentRoot, false);
        itemListViewport = viewport.GetComponent<RectTransform>();
        // 위로 10px 더 열어둔다 - 조합 재료 딱지("재료 1")가 칸 위로 걸쳐 나오므로 잘리지 않게.
        PlaceTopLeft(itemListViewport, Pad, GridTop - 10f, GridWidth, GridHeight + 10f);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 스크롤 입력만 받는다

        var listGo = new GameObject("ItemList", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(viewport.transform, false);
        var listRt = listGo.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0f, 1f);
        listRt.anchorMax = new Vector2(1f, 1f);
        listRt.pivot = new Vector2(0.5f, 1f);
        listRt.anchoredPosition = Vector2.zero;
        listRt.sizeDelta = new Vector2(0f, GridHeight + 10f);

        var grid = listGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(SlotSize, SlotSize);
        grid.spacing = new Vector2(SlotGap, SlotGap);
        grid.padding = new RectOffset(0, 0, 10, 0);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = GridColumns;
        listGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        itemListContainer = listGo.transform;

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = itemListViewport;
        scroll.content = listRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // ----- 격자 아래: 안내 문구 줄 -----
        var barGo = new GameObject("MessageBar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(contentRoot, false);
        messageBar = barGo.GetComponent<Image>();
        messageBar.color = MessageBarColor;
        messageBar.raycastTarget = false;
        PlaceTopLeft((RectTransform)barGo.transform, Pad, GridTop + GridHeight + 22f, GridWidth, 46f);

        var msgGo = new GameObject("Message", typeof(RectTransform));
        msgGo.transform.SetParent(barGo.transform, false);
        var msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.anchorMin = Vector2.zero;
        msgRt.anchorMax = Vector2.one;
        msgRt.offsetMin = new Vector2(16f, 4f);
        msgRt.offsetMax = new Vector2(-16f, -4f);
        messageText = msgGo.AddComponent<TextMeshProUGUI>();
        messageText.fontSize = 15;
        messageText.alignment = TextAlignmentOptions.Left;
        messageText.color = HintColor;
        messageText.raycastTarget = false;
        messageText.overflowMode = TextOverflowModes.Ellipsis;

        // ----- 오른쪽: 고른 아이템 정보 -----
        var frameGo = new GameObject("ItemImageFrame", typeof(RectTransform), typeof(Image));
        frameGo.transform.SetParent(contentRoot, false);
        PlaceTopLeft((RectTransform)frameGo.transform, DetailX, GridTop, DetailWidth, 230f);
        var frameImg = frameGo.GetComponent<Image>();
        SetSlicedImage(frameImg, ArtFolder + "ItemImageFrame", SlotColor);
        frameImg.raycastTarget = false;

        selectedIconImage = CreateImage("SelectedIcon", DetailX + 20f, GridTop + 16f, DetailWidth - 40f, 198f);

        categoryText = CreateText("Category", DetailX, 412f, DetailWidth, 20f, 14, TextAlignmentOptions.TopLeft);
        categoryText.fontStyle = FontStyles.Bold;
        categoryText.characterSpacing = 20f;
        categoryText.color = KickerColor;

        selectedNameText = CreateText("SelectedName", DetailX, 434f, DetailWidth, 44f, 30, TextAlignmentOptions.TopLeft);
        selectedNameText.fontStyle = FontStyles.Bold;
        selectedNameText.color = TitleColor;

        // 설명은 이름 아래부터 버튼 3개가 다 보일 때의 맨 위 버튼 직전까지.
        float descTop = 486f;
        float buttonsTop = BoxHeight - Pad - (3f * ButtonHeight + 2f * ButtonGap);
        selectedDescriptionText = CreateText("SelectedDescription", DetailX, descTop, DetailWidth, buttonsTop - descTop - 12f, 18, TextAlignmentOptions.TopLeft);
        selectedDescriptionText.color = LabelColor;
        selectedDescriptionText.lineSpacing = 18f;
        selectedDescriptionText.overflowMode = TextOverflowModes.Ellipsis;

        // 버튼 셋의 실제 위치는 LayoutActionButtons()가 "보이는 것만 아래부터" 다시 잡는다.
        operateButton = CreateActionButton("OperateButton", "작동하기", primary: true, OnOperateClicked);
        viewDetailButton = CreateActionButton("ViewDetailButton", "자세히 보기", primary: false, OnViewDetailClicked);
        combineButton = CreateActionButton("CombineButton", "조합하기", primary: false, OnCombineClicked);

        // 코드로 만든 버튼이므로 자동 정렬을 켠다(인스펙터에서 배치한 버튼이면 이 줄에 오지 않는다).
        autoLayoutButtons = true;

        SetButtonVisible(operateButton, false);
        SetButtonVisible(viewDetailButton, false);
        SetButtonVisible(combineButton, false);

        // ===== 글꼴 물려주기 =====
        // 코드로 만든 글자는 기본 글꼴에 한글 글자 모양이 없어 깨져 보인다.
        // 화면에서 한글이 잘 나오는 글꼴을 찾아 물려준다 (UIFontHelper.cs 참고).
        UIFontHelper.ApplyToChildren(overlay);
    }

    private Button CreateActionButton(string name, string label, bool primary, UnityEngine.Events.UnityAction onClick)
    {
        var btn = CreateButton(name, label, ButtonArtFolder + (primary ? "Button_Primary" : "Button_Secondary"),
                               primary ? Accent : new Color(0f, 0f, 0f, 0f), onClick);
        var text = btn.GetComponentInChildren<TMP_Text>();
        text.fontSize = 19;
        text.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
        text.color = primary ? PrimaryTextColor : ButtonTextColor;
        return btn;
    }

    // ---------------------------------------------------------------------------------
    // 기본 부품 만들기
    // ---------------------------------------------------------------------------------
    // 패널 왼쪽 위 기준 (x, y)에 w x h 크기로 놓는다.
    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void AddRect(string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(contentRoot, false);
        PlaceTopLeft(go.GetComponent<RectTransform>(), x, y, w, h);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private Sprite LoadSprite(string path)
    {
        if (spriteCache.TryGetValue(path, out var cached)) return cached;
        var sprite = Resources.Load<Sprite>(path);
        spriteCache[path] = sprite;
        return sprite;
    }

    // 그림이 있으면 그림을, 없으면 fallbackColor 색 도형을 쓴다.
    // (그림이 없을 때 투명색이면 테두리가 안 보이므로 옅은 선 색으로 바꿔 준다)
    private void SetImage(Image img, string path, Color fallbackColor)
    {
        var sprite = LoadSprite(path);
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = sprite != null ? Color.white
            : fallbackColor.a > 0f ? fallbackColor : new Color(LineColor.r, LineColor.g, LineColor.b, 0.6f);
    }

    // 가장자리(둥근 모서리/테두리)를 유지한 채 늘려 쓰는 그림.
    // 내보낸 그림은 2배 크기(@2x)라 가장자리 6px = 화면에서 3px. 크기가 그림과 달라도
    // 모서리가 찌그러지지 않도록, 읽어온 그림에 가장자리 정보를 붙인 사본을 만들어 쓴다.
    private const float SliceBorder = 6f;
    private readonly Dictionary<string, Sprite> slicedCache = new Dictionary<string, Sprite>();

    private void SetSlicedImage(Image img, string path, Color fallbackColor)
    {
        if (!slicedCache.TryGetValue(path, out var sliced))
        {
            var src = LoadSprite(path);
            sliced = src == null ? null : Sprite.Create(src.texture, src.rect, new Vector2(0.5f, 0.5f), src.pixelsPerUnit,
                                                        0, SpriteMeshType.FullRect,
                                                        new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder));
            slicedCache[path] = sliced;
        }

        if (sliced == null)
        {
            SetImage(img, path, fallbackColor);
            return;
        }
        img.sprite = sliced;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2f;
        img.color = Color.white;
    }

    private Image CreateImage(string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(contentRoot, false);
        PlaceTopLeft(go.GetComponent<RectTransform>(), x, y, w, h);
        var img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    private TMP_Text CreateText(string name, float x, float y, float w, float h, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(contentRoot, false);
        PlaceTopLeft(go.GetComponent<RectTransform>(), x, y, w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = LabelColor;
        tmp.raycastTarget = false;
        return tmp;
    }

    // ===== 실제로 누를 수 있는 버튼 만들기 =====
    // 예전에는 글씨만 있고 눌리지 않는 버튼이 만들어졌다. 원인은 두 가지였다:
    //   1) Button에 targetGraphic이 연결되지 않아 클릭 판정이 잡히지 않았다.
    //   2) 배경 Image의 raycastTarget이 꺼져 있으면 클릭이 아예 통과해 버린다.
    // 아래에서 둘 다 확실히 지정한다. 마우스를 올리거나 누르면 색이 살짝 변해서
    // "지금 눌리는 버튼이다"라는 게 눈에 보인다.
    private Button CreateButton(string name, string label, string spritePath, Color fallbackColor,
                                UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(contentRoot, false);

        var bg = go.GetComponent<Image>();
        SetSlicedImage(bg, spritePath, fallbackColor);
        bg.raycastTarget = true;   // 이게 꺼져 있으면 클릭이 통과해 버튼이 안 눌린다

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 19;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = ButtonTextColor;
        tmp.raycastTarget = false;   // 글씨가 클릭을 가로채지 않게

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;      // 클릭 판정과 색 변화의 기준이 되는 그래픽
        ApplyButtonColors(btn);

        btn.onClick.AddListener(onClick);
        return btn;
    }

    // 마우스를 올리면 살짝 푸르게, 누르면 살짝 어둡게 (환경설정 버튼과 같은 값).
    private static void ApplyButtonColors(Button btn)
    {
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.94f, 1f);
        colors.pressedColor = new Color(0.72f, 0.76f, 0.82f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1f;
        btn.colors = colors;
    }
}
