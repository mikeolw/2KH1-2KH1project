using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// InvestigationController - 조사 진행 기록
// =====================================================================================
// 한 번이라도 누른 오브젝트 기록(체크 표시, 세이브 연결)과 "아직 살펴볼 곳" 판정
// (partial class - 본체는 InvestigationController.cs).
public partial class InvestigationController
{
    // 이번 조사에서 거쳐 간 화면들(지금 화면 포함)에서 아직 안 살펴본 "핵심" 조사 오브젝트 수.
    private int CountUninspectedHotspots()
    {
        int count = 0;
        var screens = new HashSet<string>(visitedScreenIds);
        if (!string.IsNullOrEmpty(activeScreenId)) screens.Add(activeScreenId);

        foreach (string id in screens)
        {
            if (!screenData.TryGetValue(id, out ScreenData screen) || screen.hotspots == null) continue;
            foreach (var data in screen.hotspots)
            {
                if (IsStillWorthInspecting(id, data)) count++;
            }
        }
        return count;
    }

    // ===== 이 오브젝트를 "아직 살펴볼 곳"으로 칠 것인가 =====
    // 예전에는 "한 번도 안 누른 오브젝트"를 전부 셌다. 그래서 핵심 단서를 다 모았는데도
    // 아래 같은 것들 때문에 "아직 살펴볼 곳이 있는 것 같다"가 떴다.
    //   - 문(다른 화면으로 가는 통로 / 조사를 끝내는 EXIT) : 조사 대상이 아니라 길이다
    //   - 아직 열쇠가 없어 지금은 눌러도 안내문만 뜨는 것 : 지금 할 수 있는 게 없다
    //   - 이미 끝낸 것 (아이템을 이미 가졌거나, 선택지를 이미 골랐거나)
    //     : 세이브를 불러오면 "누른 기록"은 비어도 가방은 그대로라 이걸로 판단해야 한다
    private bool IsStillWorthInspecting(string screenId, HotspotData data)
    {
        // 아직 나타나지 않았거나 이미 사라진 경찰서 아이 (CreateHotspot과 같은 기준)
        if (IsPoliceChild(screenId, data.key) && !ShouldShowPoliceChild()) return false;

        // 장식(스탠딩/소품)과, 그림도 배치 좌표도 없어 화면에 만들어지지 않는 오브젝트
        // (CreateHotspot과 같은 기준)
        if (data.type == HotspotType.Standing || data.type == HotspotType.Prop) return false;
        if (string.IsNullOrEmpty(data.spriteName) && !IllustLayout.TryGet(data.key, screenId, out _)) return false;

        // 이미 눌러 본 것
        if (inspectedHotspots.Contains(TextKey(screenId, data.key))) return false;

        // 선택지로 얻는 "통과 표시"를 이미 가졌다 = 이 오브젝트로 할 일은 끝났다
        if (!string.IsNullOrWhiteSpace(data.afterItemId) && HasAllItems(ParseItemList(data.afterItemId)))
            return false;

        // 문 / EXIT : 누르면 조건 없이 다른 화면으로 가거나 조사를 끝낸다 (Inspect의 같은 분기 참고)
        if (string.IsNullOrWhiteSpace(data.afterItemId) && !string.IsNullOrWhiteSpace(data.afterTargetScreenId))
            return false;

        // 열쇠(RequiredItemId)가 없어 지금은 안내문만 뜨는 것
        if (!string.IsNullOrWhiteSpace(data.requiredItemId) && !HasAllItems(ParseItemList(data.requiredItemId)))
            return false;

        // 가방에 넣는 오브젝트인데 그 아이템을 이미 다 가졌다
        if (data.type == HotspotType.Item && !string.IsNullOrWhiteSpace(data.itemId) &&
            HasAllItems(ParseItemList(data.itemId)))
            return false;

        // 여기까지 왔으면 아직 안 눌러 본 것. 그중 "핵심"만 센다.
        return IsCoreHotspot(data);
    }

    // ===== 핵심 조사 오브젝트인가 =====
    // 안 누르면 추리에 쓸 단서를 놓치는 것만 핵심으로 친다.
    //   1) 가방에 아이템이 들어오는 것 (Item 타입)
    //   2) 누르면 자료 뷰어로 서류/사진이 펼쳐지는 것 (ItemData.csv의 ViewerType이 Document/Photo)
    //   3) 선택지를 골라 아이템을 받는 대화 (InvestigationTalkChoices.csv의 ItemId)
    // 커튼·소파처럼 설명 한 줄만 나오는 것, 아이템 없는 인물 대화는 분위기용이라 세지 않는다.
    // (안 눌러도 "아직 살펴볼 곳이 있는 것 같다"가 뜨지 않는다)
    private static bool IsCoreHotspot(HotspotData data)
    {
        if (data.type == HotspotType.Item) return true;

        var ids = ParseItemList(data.itemId);
        if (ids != null && ids.Count > 0)
        {
            // Inspect와 같은 기준: 첫 번째 아이템으로 자료 뷰어를 연다
            var info = ItemDatabase.Get(ids[0]);
            if (info != null && info.viewerType != ItemDatabase.ItemViewerType.None &&
                info.viewerImages != null && info.viewerImages.Length > 0)
                return true;
        }

        if (data.talkChoices != null)
        {
            foreach (var choice in data.talkChoices)
            {
                if (!string.IsNullOrWhiteSpace(choice.itemId)) return true;
            }
        }

        return false;
    }

    // ===== 조사한 오브젝트 표시 =====
    // 이번 플레이 동안 한 번이라도 누른 오브젝트를 기억해 두고, 오른쪽 위에 작은 체크를 붙인다.
    // "조사 그만하기" 경고(IsStillWorthInspecting)도 이 기록을 보므로 세이브에 넣고,
    // 새 게임을 시작하면 비운다 (SavePointManager.SaveToSlot / RestoreFrom / ResetForNewGame).
    // 예전에는 새 게임에서도 지난 플레이의 기록이 남아, 아무것도 안 눌렀는데 체크가 붙어 있고
    // 경고도 뜨지 않았다.
    private static readonly HashSet<string> inspectedHotspots = new HashSet<string>();

    public static List<string> ExportInspected() => new List<string>(inspectedHotspots);
    public static void ImportInspected(IEnumerable<string> keys)
    {
        inspectedHotspots.Clear();
        if (keys == null) return;
        foreach (string k in keys) if (!string.IsNullOrEmpty(k)) inspectedHotspots.Add(k);
    }
    public static void ResetInspected() => inspectedHotspots.Clear();

    private void MarkInspected(InvestigatableObject obj)
    {
        if (obj == null || string.IsNullOrEmpty(activeScreenId)) return;
        if (inspectedHotspots.Add(TextKey(activeScreenId, obj.gameObject.name))) AddCheckedBadge(obj.gameObject);
    }

    private void AddCheckedBadge(GameObject hotspot)
    {
        if (hotspot.transform.Find("CheckedBadge") != null) return;
        var sprite = UISpriteUtil.Load(UiArtFolder + "Hotspot_Checked");
        if (sprite == null) return;

        var go = new GameObject("CheckedBadge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(hotspot.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(26f, 26f);
        // 오브젝트 그림이 커지거나 작아져도(배치표의 scale) 체크 크기는 그대로 보이게 되돌린다.
        var s = hotspot.transform.lossyScale;
        if (s.x > 0.0001f && s.y > 0.0001f)
        {
            var canvasScale = hotspot.GetComponentInParent<Canvas>() != null
                ? hotspot.GetComponentInParent<Canvas>().transform.lossyScale : Vector3.one;
            go.transform.localScale = new Vector3(canvasScale.x / s.x, canvasScale.y / s.y, 1f);
        }
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
    }
}
