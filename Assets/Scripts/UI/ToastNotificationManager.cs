using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================================
// 아이템 획득 / 메모 추가 알림 (화면 우상단 토스트)
// =====================================================================================
// InventoryManager.OnItemAdded / NoteManager.OnNoteAdded를 구독해서 큐에 쌓고, 한 번에
// 하나씩 순서대로(오른쪽에서 나타나며 왼쪽으로 슬라이드인 -> 잠깐 유지 -> 오른쪽으로
// 슬라이드아웃하며 사라짐) 보여준다.
//
// GameBootstrap이 InventoryManager/NoteManager와 같은 "씬 단위 매니저"로 자동 생성한다
// (DontDestroyOnLoad가 아니다). 그래서 씬이 바뀌면 이 매니저와 큐에 남아있던 알림이 함께
// 사라진다 - 지나간 화면 얘기를 다음 씬에서 뒤늦게 보여주는 것보다 이 편이 자연스럽다.
public class ToastNotificationManager : MonoBehaviour
{
    public static ToastNotificationManager Instance;

    private const float SlideInDuration = 0.3f;
    private const float HoldDuration = 2.5f;
    private const float SlideOutDuration = 0.3f;

    private const float ToastWidth = 340f;
    private const float ToastHeight = 64f;

    // 알림 그림 (Figma "Toast" - 어두운 칸 + 왼쪽 청회색 막대). git 제외 - 드라이브로 공유.
    // 그림이 없으면 예전 검은 칸으로 대신 그린다.
    private const string ArtFolder = "Illusts/UI/Investigation/";
    private static readonly Color KickerColor = new Color(0.435f, 0.518f, 0.588f);    // 6F8496
    private static readonly Color MessageColor = new Color(0.851f, 0.89f, 0.922f);    // D9E3EB

    // 조사 화면의 "조사 그만하기" 버튼(우상단, -24/-24, 196x52) 바로 아래 자리.
    // 그 버튼이 없는 화면(#07 등)에서도 항상 같은 위치를 쓴다 - 화면마다 위치가
    // 들쭉날쭉해지는 것을 막기 위해서다.
    private const float RightInset = 24f;
    private const float TopInset = 88f;

    private Transform canvasTransform;

    private struct ToastRequest
    {
        public string kicker;    // 위쪽 작은 분류 글자 (ITEM / NOTE)
        public string message;
        public Sprite icon;      // 아이템 그림. 없으면 메모 아이콘을 쓴다.
    }

    private readonly Queue<ToastRequest> queue = new Queue<ToastRequest>();
    private bool isShowing;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[ToastNotificationManager] 씬에 Canvas가 없어 알림을 표시할 수 없습니다.");
            return;
        }
        canvasTransform = canvas.transform;

        if (InventoryManager.Instance != null) InventoryManager.Instance.OnItemAdded += OnItemAdded;
        if (NoteManager.Instance != null) NoteManager.Instance.OnNoteAdded += OnNoteAdded;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.OnItemAdded -= OnItemAdded;
        if (NoteManager.Instance != null) NoteManager.Instance.OnNoteAdded -= OnNoteAdded;
    }

    // ---------------------------------------------------------------------------------
    // 이벤트 -> 큐
    // ---------------------------------------------------------------------------------

    private void OnItemAdded(string itemId)
    {
        // mg_passed_화면ID, af_used_화면ID, resource_room_entered 같은 "내부 기록용 가짜
        // 아이템"은 ItemData.csv에 등록되어 있지 않다(InvestigationController.cs의
        // ScreenMinigamePassedFlag/AutoFinishUsedFlag 주석 참고 - 인벤토리 슬롯 UI에도
        // 대응하는 슬롯이 없어 원래 화면에 보이면 안 된다). 등록 안 된 아이템이면 진짜
        // 아이템이 아니므로 알림도 띄우지 않는다. 이렇게 하면 이름을 하나하나 몰라도
        // 앞으로 생길 비슷한 내부 플래그까지 자동으로 걸러진다.
        ItemDatabase.ItemInfo info = ItemDatabase.Get(itemId);
        if (info == null) return;

        Enqueue("ITEM", $"{info.displayName} 획득", info.GetIcon());
    }

    private void OnNoteAdded()
    {
        // 메모 내용은 스포일러가 될 수 있어 보여주지 않고, 추가되었다는 사실만 알린다.
        Enqueue("NOTE", "새 메모가 추가되었습니다", null);
    }

    private void Enqueue(string kicker, string message, Sprite icon)
    {
        if (canvasTransform == null) return;

        queue.Enqueue(new ToastRequest { kicker = kicker, message = message, icon = icon });
        if (!isShowing) StartCoroutine(ProcessQueue());
    }

    // ---------------------------------------------------------------------------------
    // 표시
    // ---------------------------------------------------------------------------------

    private IEnumerator ProcessQueue()
    {
        isShowing = true;

        while (queue.Count > 0)
        {
            yield return ShowToast(queue.Dequeue());
        }

        isShowing = false;
    }

    private IEnumerator ShowToast(ToastRequest request)
    {
        var go = new GameObject("Toast", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasTransform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(ToastWidth, ToastHeight);

        var bg = go.GetComponent<Image>();
        bool hasArt = UISpriteUtil.ApplySliced(bg, ArtFolder + "Toast", 8f);
        if (!hasArt) bg.color = new Color(0f, 0f, 0f, 0.82f);
        bg.raycastTarget = false;

        // 아이콘: 아이템이면 아이템 그림(40px), 메모면 메모 아이콘(24px)
        Sprite iconSprite = request.icon != null ? request.icon : UISpriteUtil.Load(ArtFolder + "Icon_Memo");
        float contentStartX = 18f;
        if (iconSprite != null)
        {
            bool isItem = request.icon != null;
            float size = isItem ? 40f : 24f;
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);

            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(isItem ? 14f : 20f, 0f);
            iconRt.sizeDelta = new Vector2(size, size);

            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            contentStartX = isItem ? 64f : 58f;
        }

        // 위: 작은 분류 글자 (ITEM / NOTE)
        var kickerGo = new GameObject("Kicker", typeof(RectTransform));
        kickerGo.transform.SetParent(go.transform, false);
        var kickerRt = kickerGo.GetComponent<RectTransform>();
        kickerRt.anchorMin = new Vector2(0f, 1f);
        kickerRt.anchorMax = new Vector2(1f, 1f);
        kickerRt.pivot = new Vector2(0f, 1f);
        kickerRt.offsetMin = new Vector2(contentStartX, -28f);
        kickerRt.offsetMax = new Vector2(-14f, -11f);
        var kicker = kickerGo.AddComponent<TextMeshProUGUI>();
        kicker.text = request.kicker;
        kicker.fontSize = 11;
        kicker.fontStyle = FontStyles.Bold;
        kicker.characterSpacing = 30f;
        kicker.alignment = TextAlignmentOptions.TopLeft;
        kicker.color = hasArt ? KickerColor : new Color(1f, 1f, 1f, 0.6f);
        kicker.raycastTarget = false;

        // 아래: 알림 문구
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(contentStartX, 8f);
        textRt.offsetMax = new Vector2(-14f, -27f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = request.message;
        tmp.fontSize = 17;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = hasArt ? MessageColor : Color.white;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;

        // 코드로 만든 글자는 기본 글꼴에 한글이 없어 깨져 보인다 (InvestigationController.CreateExitButton 참고).
        UIFontHelper.ApplyToChildren(go);

        // 오른쪽 화면 밖(대기) -> 제자리(왼쪽으로 슬라이드인) -> 오른쪽 화면 밖(오른쪽으로 슬라이드아웃)
        float onScreenX = -RightInset;
        float offScreenX = onScreenX + ToastWidth + 40f;

        rt.anchoredPosition = new Vector2(offScreenX, -TopInset);

        yield return SlideX(rt, offScreenX, onScreenX, SlideInDuration);

        yield return new WaitForSeconds(HoldDuration);

        yield return SlideX(rt, onScreenX, offScreenX, SlideOutDuration);

        Destroy(go);
    }

    private IEnumerator SlideX(RectTransform rt, float fromX, float toX, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(fromX, toX, t / duration);
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
            yield return null;
        }
        rt.anchoredPosition = new Vector2(toX, rt.anchoredPosition.y);
    }
}
