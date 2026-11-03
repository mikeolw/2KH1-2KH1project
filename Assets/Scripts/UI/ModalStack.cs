using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 열린 창 목록 (창 관리) + Esc 처리
// =====================================================================================
// ===== 왜 필요한가? =====
// 예전에는 창마다 스스로 Esc를 읽었다(UIManager, 환경설정, 대사 기록, 이어하기 화면...).
// 그래서 창이 겹쳐 있으면 같은 Esc 한 번에 둘이 같이 닫히거나, 그걸 막으려고
// "이번 프레임에 Esc를 썼다" 같은 표시를 주고받아야 했다. 또 대사를 넘겨도 되는지 판단할 때
// (DialogueSystem.IsBlockedByOtherUI) 창 이름을 하나하나 적어 두어서, 새 창을 만들면
// 그 목록에 추가하는 걸 잊기 쉬웠다.
//
// ===== 이제는 =====
//   - 창은 열릴 때 이 목록에 올라가고 닫힐 때 빠진다 (ModalWindow 부품이 알아서 한다).
//   - Esc는 여기 한 곳에서만 읽고, "가장 나중에 열린 창" 하나에만 전달한다.
//   - "창이 하나라도 떠 있나?"는 IsAnyOpen / BlocksDialogue 하나로 물어보면 된다.
//
// ===== 새 창을 만들 때 =====
//   var root = UIKit.CreateWindow(캔버스, "MyPanel", UILayer.Panel, onEscape: 닫는함수);
// 이 한 줄이 검은 막 + 층(UILayers) + 창 등록(여기)을 같이 한다. Esc로 닫히면 안 되는 창
// (세이브포인트 저장 창, 선택지)은 onEscape를 null로 둔다 - 그러면 Esc가 그 창에서 멈추고
// 아래 창까지 내려가지 않는다. 씬에 미리 만들어 둔 창이면 ModalStack.Register(창, 닫는함수)만 한다.
//
// ===== 등록을 빠뜨리면? (개발 중 경고) =====
// 에디터/개발용 빌드에서는 0.5초마다 캔버스 바로 아래를 훑어서, "화면 전체를 덮고 클릭을 막는
// 어두운 막"인데 창으로 등록되지 않은 것이 켜져 있으면 콘솔에 경고를 한 번 띄운다.
// (다른 브랜치를 합친 뒤 Play 한 번이면 빠뜨린 창이 바로 보인다. 출시 빌드에서는 돌지 않는다.)
// 미니게임 막이나 암전처럼 일부러 창이 아닌 것은 ModalStack.MarkNotWindow(오브젝트, 이유)로 뺀다.
public class ModalStack : MonoBehaviour
{
    private static readonly List<ModalWindow> open = new List<ModalWindow>();
    private static ModalStack driver;

    // 창 하나를 등록한다. 이미 등록돼 있으면 설정만 바꾼다.
    // root가 켜져 있는 동안 "열린 창"으로 친다.
    public static ModalWindow Register(GameObject root, System.Action onEscape, bool blocksDialogue = true)
    {
        if (root == null) return null;
        var window = root.GetComponent<ModalWindow>();
        if (window == null) window = root.AddComponent<ModalWindow>();   // 켜져 있으면 이때 바로 목록에 올라간다
        window.onEscape = onEscape;
        window.blocksDialogue = blocksDialogue;
        EnsureDriver();
        return window;
    }

    // "화면을 덮지만 창이 아니다" (미니게임 막, 암전, 다른 오브젝트가 대신 등록한 창의 그림 부분).
    // 위 개발 중 경고에서 뺀다.
    public static void MarkNotWindow(GameObject go, string reason)
    {
        if (go == null) return;
        var mark = go.GetComponent<NotAWindow>();
        if (mark == null) mark = go.AddComponent<NotAWindow>();
        mark.reason = reason;
    }

    // 창이 하나라도 떠 있나
    public static bool IsAnyOpen
    {
        get { Prune(); return open.Count > 0; }
    }

    // 대사 넘기기/자동 진행을 멈춰야 하는 창이 떠 있나
    public static bool BlocksDialogue
    {
        get
        {
            Prune();
            foreach (var w in open) if (w.blocksDialogue) return true;
            return false;
        }
    }

    // 가장 나중에 열린 창
    public static ModalWindow Top
    {
        get { Prune(); return open.Count > 0 ? open[open.Count - 1] : null; }
    }

    // root 위에 다른 창이 떠 있나. 창 안에서 키보드(← → 등)를 쓸 때, 그 위에 다른 창이
    // 떠 있으면 그쪽 차례이므로 무시하도록 확인하는 데 쓴다.
    // (root가 등록되지 않은 창이어도, 다른 창이 없으면 false라서 키가 막히지 않는다)
    public static bool IsCoveredByOther(GameObject root)
    {
        var top = Top;
        return top != null && top.gameObject != root;
    }

    // ModalWindow가 켜지고 꺼질 때 부른다.
    internal static void Push(ModalWindow w)
    {
        open.Remove(w);
        open.Add(w);   // 다시 열린 창은 맨 위로
        EnsureDriver();
    }

    internal static void Remove(ModalWindow w) => open.Remove(w);

    // 씬이 바뀌며 지워진 창이 목록에 남아 있지 않게 정리한다.
    private static void Prune() => open.RemoveAll(w => w == null || !w.isActiveAndEnabled);

    // 에디터에서 "도메인 다시 불러오기"를 끈 채로 Play를 다시 눌러도 예전 목록이 남지 않게.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        open.Clear();
        driver = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        warnedObjects.Clear();
#endif
    }

    // Esc를 읽는 오브젝트는 게임 전체에 하나만 둔다 (씬이 바뀌어도 유지).
    // 창이 하나도 등록되지 않은 씬에서도 아래 경고 검사가 돌도록 시작할 때 만든다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureDriver()
    {
        if (driver != null) return;
        var go = new GameObject("[ModalStack]");
        DontDestroyOnLoad(go);
        driver = go.AddComponent<ModalStack>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        var top = Top;
        if (top == null) return;
        top.onEscape?.Invoke();   // null이면 Esc가 이 창에서 멈춘다 (아래 창도 닫지 않음)
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ---------------------------------------------------------------------------------
    // 개발 중 경고: 등록되지 않은 창 찾기
    // ---------------------------------------------------------------------------------
    private const float AuditInterval = 0.5f;
    private float nextAuditTime;
    private static readonly HashSet<GameObject> warnedObjects = new HashSet<GameObject>();

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextAuditTime) return;
        nextAuditTime = Time.unscaledTime + AuditInterval;
        AuditUnregisteredWindows();
    }

    private static void AuditUnregisteredWindows()
    {
        var canvas = UILayers.RootCanvas();
        if (canvas == null) return;
        Vector2 screen = ((RectTransform)canvas.transform).rect.size;

        foreach (Transform child in canvas.transform)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.GetComponent<ModalWindow>() != null || child.GetComponent<NotAWindow>() != null) continue;
            if (!CoversScreenWithDim(child, screen)) continue;
            if (!warnedObjects.Add(child.gameObject)) continue;   // 같은 것은 한 번만

            Debug.LogWarning(
                $"[ModalStack] '{child.name}'이(가) 화면을 덮고 있는데 창으로 등록되지 않았습니다. " +
                "이대로면 Esc로 닫히지 않고, 뒤에서 대사가 넘어갈 수 있으며, 다른 UI와 앞뒤 순서가 꼬일 수 있습니다.\n" +
                "→ 창이면: UIKit.CreateWindow(...)로 만들거나 ModalStack.Register(창, 닫는함수) + UILayers.Place(창, 층)\n" +
                "→ 창이 아니면(미니게임 막, 암전 등): ModalStack.MarkNotWindow(오브젝트, \"이유\")",
                child.gameObject);
        }
    }

    // 자기 자신이나 바로 아래 자식 중에 "화면 전체 크기 + 클릭을 막는 + 어두운" 막이 있나.
    // (흰 선화 배경이나 인물 그림처럼 밝은 것은 창이 아니므로 어두운 것만 본다)
    private static bool CoversScreenWithDim(Transform t, Vector2 screen)
    {
        if (IsDim(t.GetComponent<UnityEngine.UI.Graphic>(), screen)) return true;
        foreach (Transform c in t)
            if (c.gameObject.activeInHierarchy && IsDim(c.GetComponent<UnityEngine.UI.Graphic>(), screen)) return true;
        return false;
    }

    private static bool IsDim(UnityEngine.UI.Graphic g, Vector2 screen)
    {
        if (g == null || !g.isActiveAndEnabled || !g.raycastTarget) return false;

        Vector2 size = g.rectTransform.rect.size;
        if (size.x < screen.x * 0.9f || size.y < screen.y * 0.9f) return false;

        Color c = g.color;
        if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) > 0.15f) return false;

        // CanvasGroup으로 투명하게 해 두었거나 클릭을 끈 것(예: 평소의 암전 판)은 막이 아니다.
        float alpha = c.a;
        foreach (var group in g.GetComponentsInParent<CanvasGroup>())
        {
            if (!group.blocksRaycasts) return false;
            alpha *= group.alpha;
            if (group.ignoreParentGroups) break;
        }
        return alpha >= 0.5f;
    }
#endif
}
