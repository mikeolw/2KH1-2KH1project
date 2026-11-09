using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Spot = DocumentHighlights.Spot;

// =====================================================================================
// 서류 형광펜 배치 도구  (메뉴: 2KH1 > 서류 형광펜 배치 도구)
// =====================================================================================
// 자료 뷰어에서 서류를 읽을 때 "어디에 밑줄을 긋고 무슨 생각을 하는지"를 눈으로 보며 잡는다.
// 일러스트 배치 도구와 쓰는 법이 같다 - 끌어서 옮기고, 저장을 누르면 CSV에 적힌다.
//
// ===== 쓰는 법 =====
//   왼쪽  : 서류 그림 고르기 (형광펜이 있는 그림에는 개수가 붙는다)
//   가운데: 서류를 보며 노란 칸을 끌어 옮긴다. 오른쪽 아래 모서리를 끌면 크기가 바뀐다
//   오른쪽: 순서 바꾸기 / 지우기 / 독백 고치기
//
// ===== 저장되는 곳 (두 군데로 나뉜다) =====
//   좌표 -> Assets/StreamingAssets/Stage/DocumentHighlights.csv   (git으로 공유)
//   독백 -> Assets/Resources/Dialogues/DocumentThoughts.csv       (드라이브로 공유)
// 이 저장소는 공개라서 대사가 git에 올라가면 안 된다. 그래서 숫자와 글을 갈라 두었다.
// (자세한 이유는 DocumentHighlights.cs 윗부분)
public class DocumentHighlightWindow : EditorWindow
{
    private const string ObjectFolder = "Assets/Resources/Illusts/Objects";
    private const float SideW = 230f;      // 왼쪽 목록 너비
    private const float PanelW = 330f;     // 오른쪽 편집판 너비
    private const float HandleSize = 12f;  // 크기 조절 손잡이

    private static readonly Color PenFill = new Color(0.949f, 0.871f, 0.369f, 0.40f);
    private static readonly Color PenEdge = new Color(0.72f, 0.62f, 0.10f, 0.9f);
    private static readonly Color PenSel = new Color(0.25f, 0.62f, 1f, 1f);

    private Dictionary<string, List<Spot>> data;
    private List<string> allImages = new List<string>();
    private string search = "Windoc";
    private string current;
    private Texture2D tex;
    private int selected = -1;
    private bool dirty;

    private Vector2 listScroll, panelScroll;

    // 끌기 상태
    private enum Drag { None, Move, Resize }
    private Drag drag = Drag.None;
    private Vector2 dragStartMouse;
    private Rect dragStartRect;

    [MenuItem("2KH1/서류 형광펜 배치 도구")]
    public static void Open()
    {
        var w = GetWindow<DocumentHighlightWindow>("서류 형광펜");
        w.minSize = new Vector2(1000f, 640f);
    }

    private void OnEnable()
    {
        DocumentHighlights.Reload();
        data = DocumentHighlights.EditableCopy();
        RefreshImageList();
        if (current == null && allImages.Count > 0) Select(allImages[0]);
    }

    // 저장하지 않고 닫으면 애써 잡은 자리가 날아간다. 일러스트 배치 도구와 같게 한 번 묻는다.
    private void OnDestroy()
    {
        if (dirty && EditorUtility.DisplayDialog("서류 형광펜 배치 도구",
                "저장하지 않은 형광펜 변경이 있습니다. 저장할까요?", "저장", "버리고 닫기"))
        {
            Save();
        }
    }

    private void RefreshImageList()
    {
        allImages.Clear();
        if (!Directory.Exists(ObjectFolder)) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ObjectFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            allImages.Add(Path.GetFileNameWithoutExtension(path));
        }
        allImages.Sort(string.CompareOrdinal);
    }

    private void Select(string image)
    {
        current = image;
        selected = -1;
        drag = Drag.None;
        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(ObjectFolder, image + ".png").Replace('\\', '/'));
    }

    private List<Spot> Spots()
    {
        if (current == null) return null;
        if (!data.TryGetValue(current, out var list)) data[current] = list = new List<Spot>();
        return list;
    }

    // ---------------------------------------------------------------------------------
    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.BeginHorizontal();
        DrawImageList();
        DrawPreview();
        DrawPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUI.enabled = dirty;
        if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60f))) Save();
        GUI.enabled = true;
        if (GUILayout.Button("되돌리기", EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            if (!dirty || EditorUtility.DisplayDialog("서류 형광펜 배치 도구",
                    "저장하지 않은 변경을 버리고 파일에서 다시 읽을까요?", "다시 읽기", "그만두기"))
            {
                OnEnable();
                dirty = false;
            }
        }
        GUILayout.Space(12f);
        GUILayout.Label(dirty ? "● 저장하지 않은 변경이 있습니다" : "저장됨", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label("좌표 → Stage/DocumentHighlights.csv (git)   ·   독백 → Dialogues/DocumentThoughts.csv (드라이브)",
            EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawImageList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(SideW));
        search = EditorGUILayout.TextField("찾기", search);
        listScroll = EditorGUILayout.BeginScrollView(listScroll);
        foreach (string name in allImages)
        {
            if (!string.IsNullOrEmpty(search) &&
                name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            int count = data.TryGetValue(name, out var l) ? l.Count : 0;
            string label = count > 0 ? $"{name}   ({count})" : name;
            bool on = name == current;
            if (GUILayout.Toggle(on, label, "Button") != on) Select(name);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ---------------------------------------------------------------------------------
    // 가운데: 서류를 보며 칸을 끈다
    // ---------------------------------------------------------------------------------
    private void DrawPreview()
    {
        EditorGUILayout.BeginVertical();
        Rect area = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(area, new Color(0.13f, 0.14f, 0.16f));

        if (tex == null)
        {
            GUI.Label(area, "서류 그림을 고르세요", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        // 그림을 영역에 비율 그대로 맞춰 넣는다
        float pad = 14f;
        float fit = Mathf.Min((area.width - pad * 2f) / tex.width, (area.height - pad * 2f) / tex.height);
        var img = new Rect(area.x + (area.width - tex.width * fit) * 0.5f,
                           area.y + (area.height - tex.height * fit) * 0.5f,
                           tex.width * fit, tex.height * fit);
        GUI.DrawTexture(img, tex, ScaleMode.StretchToFill);

        var spots = Spots();
        HandleMouse(img, spots);

        for (int i = 0; i < spots.Count; i++)
        {
            Rect r = ToScreen(img, spots[i].rect);
            EditorGUI.DrawRect(r, PenFill);
            DrawOutline(r, i == selected ? PenSel : PenEdge, i == selected ? 2f : 1f);

            // 순서 번호
            var tag = new Rect(r.x - 1f, r.y - 17f, 22f, 16f);
            EditorGUI.DrawRect(tag, i == selected ? PenSel : new Color(0.1f, 0.12f, 0.14f, 0.9f));
            GUI.Label(tag, (i + 1).ToString(), MiniWhite);

            // 크기 조절 손잡이 (오른쪽 아래)
            if (i == selected)
                EditorGUI.DrawRect(new Rect(r.xMax - HandleSize, r.yMax - HandleSize, HandleSize, HandleSize), PenSel);
        }

        GUI.Label(new Rect(area.x + 8f, area.yMax - 20f, area.width - 16f, 18f),
            "칸을 끌면 이동 · 오른쪽 아래 모서리를 끌면 크기 · 빈 곳을 끌면 새 칸", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private static GUIStyle miniWhite;
    private static GUIStyle MiniWhite
    {
        get
        {
            if (miniWhite == null)
                miniWhite = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                };
            return miniWhite;
        }
    }

    private static void DrawOutline(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // 비율 좌표 -> 화면 사각형
    private static Rect ToScreen(Rect img, Rect n) =>
        new Rect(img.x + n.x * img.width, img.y + n.y * img.height, n.width * img.width, n.height * img.height);

    // 화면 좌표 -> 비율
    private static Vector2 ToNorm(Rect img, Vector2 p) =>
        new Vector2((p.x - img.x) / img.width, (p.y - img.y) / img.height);

    private void HandleMouse(Rect img, List<Spot> spots)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && img.Contains(e.mousePosition))
        {
            // 선택된 칸의 손잡이를 먼저 본다 (칸 안쪽과 겹치므로)
            if (selected >= 0 && selected < spots.Count)
            {
                Rect sr = ToScreen(img, spots[selected].rect);
                var h = new Rect(sr.xMax - HandleSize, sr.yMax - HandleSize, HandleSize, HandleSize);
                if (h.Contains(e.mousePosition))
                {
                    drag = Drag.Resize;
                    dragStartMouse = e.mousePosition;
                    dragStartRect = spots[selected].rect;
                    e.Use();
                    return;
                }
            }

            // 위에 있는 칸부터 찾는다
            for (int i = spots.Count - 1; i >= 0; i--)
            {
                if (ToScreen(img, spots[i].rect).Contains(e.mousePosition))
                {
                    selected = i;
                    drag = Drag.Move;
                    dragStartMouse = e.mousePosition;
                    dragStartRect = spots[i].rect;
                    Repaint();
                    e.Use();
                    return;
                }
            }

            // 빈 곳을 눌렀다 -> 새 칸을 만들며 크기 끌기로 들어간다
            Vector2 n = ToNorm(img, e.mousePosition);
            spots.Add(new Spot
            {
                image = current,
                order = spots.Count + 1,
                rect = new Rect(n.x, n.y, 0.001f, 0.018f),
                speaker = DocumentHighlights.DefaultSpeaker,
                text = "",
            });
            selected = spots.Count - 1;
            drag = Drag.Resize;
            dragStartMouse = e.mousePosition;
            dragStartRect = spots[selected].rect;
            dirty = true;
            Repaint();
            e.Use();
            return;
        }

        if (drag != Drag.None && selected >= 0 && selected < spots.Count)
        {
            if (e.type == EventType.MouseDrag)
            {
                Vector2 d = e.mousePosition - dragStartMouse;
                Vector2 dn = new Vector2(d.x / img.width, d.y / img.height);
                Rect r = dragStartRect;

                if (drag == Drag.Move)
                {
                    r.x = Mathf.Clamp(r.x + dn.x, 0f, 1f - r.width);
                    r.y = Mathf.Clamp(r.y + dn.y, 0f, 1f - r.height);
                }
                else
                {
                    r.width = Mathf.Clamp(r.width + dn.x, 0.004f, 1f - r.x);
                    r.height = Mathf.Clamp(r.height + dn.y, 0.004f, 1f - r.y);
                }
                spots[selected].rect = r;
                dirty = true;
                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                drag = Drag.None;
                e.Use();
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 오른쪽: 순서와 독백
    // ---------------------------------------------------------------------------------
    private void DrawPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PanelW));
        var spots = Spots();
        if (spots == null)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField(current ?? "", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"형광펜 {spots.Count}군데 · 읽는 순서대로", EditorStyles.miniLabel);
        EditorGUILayout.Space(4f);

        panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
        for (int i = 0; i < spots.Count; i++)
        {
            var s = spots[i];
            bool isSel = i == selected;
            EditorGUILayout.BeginVertical(isSel ? EditorStyles.helpBox : GUIStyle.none);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"{i + 1}", GUILayout.Width(26f))) { selected = i; Repaint(); }
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(26f))) { Swap(spots, i, i - 1); break; }
            GUI.enabled = i < spots.Count - 1;
            if (GUILayout.Button("▼", GUILayout.Width(26f))) { Swap(spots, i, i + 1); break; }
            GUI.enabled = true;
            GUILayout.Label($"x {s.rect.x:0.000}  y {s.rect.y:0.000}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("지우기", GUILayout.Width(52f)))
            {
                spots.RemoveAt(i);
                selected = -1;
                dirty = true;
                break;
            }
            EditorGUILayout.EndHorizontal();

            // 독백 (드라이브 CSV로 저장된다). 비워두면 밑줄만 긋고 말풍선은 안 뜬다.
            EditorGUI.BeginChangeCheck();
            string t = EditorGUILayout.TextArea(s.text ?? "", GUILayout.Height(46f));
            if (EditorGUI.EndChangeCheck()) { s.text = t; dirty = true; }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("말하는 사람", GUILayout.Width(70f));
            EditorGUI.BeginChangeCheck();
            string sp = EditorGUILayout.TextField(string.IsNullOrEmpty(s.speaker) ? DocumentHighlights.DefaultSpeaker : s.speaker);
            if (EditorGUI.EndChangeCheck()) { s.speaker = sp; dirty = true; }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6f);
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("칸 하나 더 (가운데에 생깁니다)"))
        {
            spots.Add(new Spot
            {
                image = current,
                order = spots.Count + 1,
                rect = new Rect(0.15f, 0.45f, 0.5f, 0.022f),
                speaker = DocumentHighlights.DefaultSpeaker,
                text = "",
            });
            selected = spots.Count - 1;
            dirty = true;
        }
        EditorGUILayout.EndVertical();
    }

    private void Swap(List<Spot> list, int a, int b)
    {
        (list[a], list[b]) = (list[b], list[a]);
        selected = b;
        dirty = true;
        Repaint();
    }

    private void Save()
    {
        // 칸이 하나도 없는 그림은 표에서 빼 둔다 (빈 줄만 늘어난다)
        var keys = new List<string>(data.Keys);
        foreach (string k in keys) if (data[k].Count == 0) data.Remove(k);

        DocumentHighlights.SaveAll(data);
        AssetDatabase.Refresh();
        dirty = false;
        data = DocumentHighlights.EditableCopy();
        Debug.Log("[서류 형광펜] 저장했습니다. " +
                  "좌표 → StreamingAssets/Stage/DocumentHighlights.csv (git에 올립니다) / " +
                  "독백 → Resources/Dialogues/DocumentThoughts.csv (드라이브에 올려 주세요)");
    }
}
