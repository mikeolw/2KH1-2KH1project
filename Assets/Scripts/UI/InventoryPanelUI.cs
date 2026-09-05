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
    [Tooltip("조합 결과나 안내 문구를 띄우는 텍스트")]
    public TMP_Text messageText;

    // 지금 고른 아이템의 ItemId.
    private string selectedItemId;

    // 조합 모드인지. true인 상태에서 다른 아이템을 누르면 조합을 시도한다.
    private bool combineMode;

    // 조합 모드로 들어갈 때 "첫 번째 재료"로 잡아둔 아이템.
    private string combineSourceItemId;

    // 지금 화면에 만들어둔 아이템 버튼들. 목록을 새로 그릴 때 지우기 위해 들고 있는다.
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

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

        // 가방을 닫았다 열면 조합 모드는 초기화한다(헷갈림 방지).
        SetCombineMode(false, null);

        Refresh();
        ShowMessage("아이템을 눌러 설명을 보세요. 두 개를 합치려면 하나를 고른 뒤 [조합하기]를 누르세요.");
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
        }
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
            if (go != null) Destroy(go);
        }
        spawnedButtons.Clear();

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

        // 아무것도 없을 때 안내
        if (count == 0)
        {
            if (selectedNameText != null) selectedNameText.text = "";
            if (selectedDescriptionText != null) selectedDescriptionText.text = "아직 가진 것이 없다.";
            if (selectedIconImage != null) selectedIconImage.enabled = false;
            SetButtonVisible(viewDetailButton, false);
            SetButtonVisible(combineButton, false);
            return;
        }

        // 고른 아이템이 조합으로 사라졌을 수도 있으므로 확인한다.
        if (!string.IsNullOrEmpty(selectedItemId) && !InventoryManager.Instance.HasItem(selectedItemId))
        {
            selectedItemId = null;
            ClearSelection();
        }
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
            slotBg.color = new Color(1f, 1f, 1f, 0.14f);
            slotBg.raycastTarget = true;

            // targetGraphic을 지정해야 클릭 판정과 색 변화가 동작한다.
            // (이게 빠져 있어서 아이템을 눌러도 아무 반응이 없었다)
            var slotBtn = go.GetComponent<Button>();
            slotBtn.targetGraphic = slotBg;
            slotBtn.transition = Selectable.Transition.ColorTint;

            var c = slotBtn.colors;
            c.highlightedColor = new Color(1f, 0.95f, 0.7f);
            c.pressedColor = new Color(0.8f, 0.7f, 0.35f);
            slotBtn.colors = c;
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
            // 코드로 만든 버튼이면 아이콘용 Image를 하나 더 붙인다.
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.25f);
            iconRt.anchorMax = new Vector2(0.9f, 0.95f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = info.GetIcon();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = iconImg.sprite != null;
        }

        // 이름 라벨
        var label = go.GetComponentInChildren<TMP_Text>();
        if (label == null && itemButtonPrefab == null)
        {
            var textGo = new GameObject("Name", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0.25f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 16;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }
        if (label != null) label.text = info.displayName;

        // 클릭 처리
        var button = go.GetComponent<Button>();
        if (button != null)
        {
            string capturedId = info.itemId; // 람다가 반복 변수를 잡지 않도록 복사
            button.onClick.AddListener(() => OnItemClicked(capturedId));
        }

        spawnedButtons.Add(go);
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
    }

    private void ClearSelection()
    {
        selectedItemId = null;
        if (selectedNameText != null) selectedNameText.text = "";
        if (selectedDescriptionText != null) selectedDescriptionText.text = "";
        if (selectedIconImage != null) selectedIconImage.enabled = false;
        SetButtonVisible(viewDetailButton, false);
        SetButtonVisible(combineButton, false);
    }

    // "자세히 보기" 버튼. 서류/사진을 전체 화면 뷰어로 펼친다.
    public void OnViewDetailClicked()
    {
        if (string.IsNullOrEmpty(selectedItemId)) return;
        if (DocumentViewerController.Instance == null) return;

        DocumentViewerController.Instance.ShowItem(selectedItemId);
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
        ShowMessage($"'{ItemDatabase.GetDisplayName(selectedItemId)}'와(과) 함께 쓸 물건을 고르세요.");
    }

    // 조합 모드를 켜고 끄면서 화면 표시도 함께 바꾼다.
    // 버튼 글씨와 색이 바뀌지 않으면 지금 조합 모드인지 알 수 없어서 혼란스럽다.
    private void SetCombineMode(bool on, string sourceItemId)
    {
        combineMode = on;
        combineSourceItemId = on ? sourceItemId : null;

        if (combineButton == null) return;

        var label = combineButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = on ? "조합 취소" : "조합하기";

        var bg = combineButton.GetComponent<Image>();
        if (bg != null)
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

    private void ShowMessage(string message)
    {
        if (messageText != null) messageText.text = message;
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    // ---------------------------------------------------------------------------------
    // UI 자동 생성 (씬을 아직 안 꾸민 상태에서도 동작하게 하는 편의 기능)
    // ---------------------------------------------------------------------------------
    // 이 스크립트가 만든 UI를 담는 자식의 이름.
    private const string ContentRootName = "__InventoryContent";

    // 만들어진 UI를 담는 부모. 아래 Create* 함수들이 여기에 붙인다.
    private Transform contentRoot;

    private void EnsureUI()
    {
        // 아이템 목록 자리가 이미 인스펙터에 연결되어 있으면 손대지 않는다.
        if (itemListContainer != null) return;

        // 이미 만들어둔 게 있으면 다시 만들지 않는다.
        var existing = transform.Find(ContentRootName);
        if (existing != null)
        {
            contentRoot = existing;
            return;
        }

        // ----- 씬에 남아 있던 예전 오브젝트를 꺼둔다 -----
        // InventoryPanel 안에는 프로토타입 시절 아이템 자리(ItemSlots, Slot_* 등)가 남아
        // 있어서 그대로 두면 새 목록 위에 겹쳐 보인다. 지우지 않고 비활성화만 한다.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == ContentRootName) continue;
            child.gameObject.SetActive(false);
        }

        // ----- 패널 크기/배경 -----
        var rootRt = GetComponent<RectTransform>();
        if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();

        // 씬에 설정된 크기가 제각각이라 여기서 고정한다.
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(1200f, 820f);
        rootRt.anchoredPosition = Vector2.zero;

        var bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.95f);
        bg.raycastTarget = true;   // 뒤쪽 클릭이 새어나가지 않게 막는다

        // ----- 내용 담을 전용 자식 -----
        var root = new GameObject(ContentRootName, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        var rr = root.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;
        contentRoot = root.transform;

        // ----- 제목 -----
        var title = CreateText("Title", new Vector2(0.03f, 0.93f), new Vector2(0.55f, 0.99f), 30, TextAlignmentOptions.Left);
        title.text = "가방";
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(1f, 0.86f, 0.45f);

        // ----- 왼쪽: 아이템 격자 -----
        var listGo = new GameObject("ItemList", typeof(RectTransform), typeof(GridLayoutGroup));
        listGo.transform.SetParent(contentRoot, false);
        var listRt = listGo.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0.03f, 0.05f);
        listRt.anchorMax = new Vector2(0.55f, 0.92f);
        listRt.offsetMin = Vector2.zero;
        listRt.offsetMax = Vector2.zero;

        var grid = listGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150f, 150f);
        grid.spacing = new Vector2(12f, 12f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        itemListContainer = listGo.transform;

        // ----- 오른쪽: 고른 아이템 정보 -----
        selectedIconImage = CreateImage("SelectedIcon", new Vector2(0.58f, 0.58f), new Vector2(0.97f, 0.92f));
        selectedNameText = CreateText("SelectedName", new Vector2(0.58f, 0.49f), new Vector2(0.97f, 0.57f), 30, TextAlignmentOptions.Left);
        selectedNameText.fontStyle = FontStyles.Bold;
        selectedDescriptionText = CreateText("SelectedDescription", new Vector2(0.58f, 0.24f), new Vector2(0.97f, 0.48f), 22, TextAlignmentOptions.TopLeft);
        messageText = CreateText("Message", new Vector2(0.58f, 0.05f), new Vector2(0.97f, 0.14f), 19, TextAlignmentOptions.TopLeft);
        messageText.color = new Color(1f, 0.85f, 0.4f);

        viewDetailButton = CreateButton("ViewDetailButton", "자세히 보기",
            new Vector2(0.58f, 0.155f), new Vector2(0.765f, 0.225f), OnViewDetailClicked);
        combineButton = CreateButton("CombineButton", "조합하기",
            new Vector2(0.785f, 0.155f), new Vector2(0.97f, 0.225f), OnCombineClicked);

        SetButtonVisible(viewDetailButton, false);
        SetButtonVisible(combineButton, false);
    }

    private Image CreateImage(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(contentRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    private TMP_Text CreateText(string name, Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(contentRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        // 줄바꿈은 TextMeshPro 기본값이 켜짐이라 따로 지정하지 않는다.
        // (TMP 버전에 따라 속성 이름이 enableWordWrapping / textWrappingMode 로 달라서,
        //  버전을 타지 않도록 건드리지 않는 편이 안전하다.)
        return tmp;
    }

    // ===== 실제로 누를 수 있는 버튼 만들기 =====
    // 예전에는 글씨만 있고 눌리지 않는 버튼이 만들어졌다. 원인은 두 가지였다:
    //   1) Button에 targetGraphic이 연결되지 않아 클릭 판정이 잡히지 않았다.
    //   2) 배경 Image의 raycastTarget이 꺼져 있으면 클릭이 아예 통과해 버린다.
    // 아래에서 둘 다 확실히 지정한다. 눌렀을 때 색이 변하는 것도 함께 넣어서
    // "지금 눌리는 버튼이다"라는 게 눈에 보이게 했다.
    private Button CreateButton(string name, string label, Vector2 anchorMin, Vector2 anchorMax,
                                UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(contentRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = go.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.20f);
        bg.raycastTarget = true;   // 이게 꺼져 있으면 클릭이 통과해 버튼이 안 눌린다

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;   // 글씨가 클릭을 가로채지 않게

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;      // 클릭 판정과 색 변화의 기준이 되는 그래픽
        btn.transition = Selectable.Transition.ColorTint;

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.7f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.35f);
        colors.selectedColor = Color.white;
        btn.colors = colors;

        btn.onClick.AddListener(onClick);
        return btn;
    }
}
