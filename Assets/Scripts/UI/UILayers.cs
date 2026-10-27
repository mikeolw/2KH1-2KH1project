using UnityEngine;

// =====================================================================================
// UI 층 (앞뒤 순서)
// =====================================================================================
// ===== 왜 필요한가? =====
// 화면 UI는 대부분 같은 Canvas 밑 형제라서, 목록에서 나중에 있는 것이 위에 그려진다.
// 그래서 창마다 열 때 SetAsLastSibling으로 맨 뒤로 보냈는데, 그 뒤에 새로 만들어진
// UI(퀵바, 알림 등)가 다시 위로 올라오거나, 타이머처럼 매 프레임 맨 뒤로 가는 것과 순서를
// 다투는 일이 반복됐다(예: 자료 뷰어가 퀵바·알림에 가려짐).
//
// ===== 이제는 =====
// 창 루트에 층을 정해 준다. 층은 형제 순서와 상관없이 숫자가 큰 쪽이 항상 위에 그려진다.
//
//   (층 없음)  배경 · 인물 · 암전 · 대화창 · 퀵바 · 조사 화면 · 미니게임      ← 예전처럼 형제 순서
//   Panel   20  수첩 · 핸드폰 · 가방 · 환경설정 · 대사 기록 · 이어하기 화면
//   Toast   25  아이템/메모 알림 (가방을 연 채로 조합해도 알림이 보이게)
//   Detail  30  조사 상세: 자료 뷰어 · 아이템 팝업 (가방의 [자세히 보기]도 가방 위에)
//   Dialog  40  세이브포인트 저장 창 (무조건 골라야 하는 창)
//   Timer   45  타임어택 남은 시간 (어떤 창을 열어도 보여야 한다)
//   (따로)  5000 엔딩 카드 - 자기 Canvas를 따로 가진다 (EndingCardUI.cs)
//
// ===== 새 창을 만들 때 =====
//   UILayers.Place(창 루트, UILayer.Panel);
// 그리고 Canvas를 찾을 때는 FindAnyObjectByType<Canvas>() 대신 UILayers.RootCanvas()를 쓴다.
// 층을 주면 그 오브젝트에 Canvas가 하나 더 붙는데, FindAnyObjectByType은 그걸 집어 올 수
// 있어서(그러면 새 UI가 그 창 안에 들어가 창과 같이 꺼진다) 항상 맨 바깥 Canvas를 찾아야 한다.
public enum UILayer
{
    Panel = 20,
    Toast = 25,
    Detail = 30,
    Dialog = 40,
    Timer = 45,
}

public static class UILayers
{
    public static void Place(GameObject go, UILayer layer)
    {
        if (go == null) return;
        var member = go.GetComponent<UILayerMember>();
        if (member == null) member = go.AddComponent<UILayerMember>();
        member.layer = layer;
        member.Apply();
    }

    // 게임 화면의 맨 바깥 Canvas.
    // 엔딩 카드처럼 따로 맨 위에 띄우는 Canvas(정렬 순서가 큼)가 있을 수 있으므로,
    // 맨 바깥 Canvas 중 정렬 순서가 가장 작은 것(= 바탕 화면)을 고른다.
    private static Canvas cachedRoot;

    public static Canvas RootCanvas()
    {
        if (cachedRoot != null && cachedRoot.isActiveAndEnabled) return cachedRoot;

        Canvas best = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas) continue;
            if (best == null || c.sortingOrder < best.sortingOrder) best = c;
        }
        cachedRoot = best;
        return best;
    }
}
