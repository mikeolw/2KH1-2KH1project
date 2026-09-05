using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 일러스트 배치 도구 - 배경 위에서 오브젝트/스탠딩을 마우스로 끌어 위치를 잡는 에디터 창
// =====================================================================================
// ===== 왜 필요한가? =====
// 조사 오브젝트(OBJ_*)와 캐릭터 스탠딩(STD_*) 그림은 여백이 잘린 채로 저장되어 있어서,
// "1440x1080 화면 어디에 놓여야 하는지"를 코드가 알 방법이 없다. 그 위치를 사람이 눈으로
// 보면서 잡아주는 도구다. (자세한 배경 설명은 IllustLayout.cs 상단 주석 참고)
//
// ===== 쓰는 법 =====
//   1) 유니티 상단 메뉴 [2KH1] > [일러스트 배치 도구]를 연다.
//   2) 위쪽에서 배경 그림을 고른다 (예: BG_01_MyDesk).
//   3) 그 아래 목록에서 이 배경 위에 올릴 그림들을 체크한다.
//      - "조사 화면으로 자동 선택"을 누르면 InvestigationData.csv를 읽어서
//        그 화면에 속한 오브젝트를 알아서 골라준다.
//   4) 미리보기 화면에서 그림을 마우스로 끌어 제자리에 놓는다.
//      - 그림을 클릭하면 선택되고, 선택된 그림은 방향키로도 1픽셀씩 움직일 수 있다.
//      - 오른쪽 패널에서 X, Y, 크기 배율을 숫자로 직접 입력할 수도 있다.
//   5) [저장] 버튼을 누르면 Assets/Resources/Dialogues/IllustLayout.csv 에 기록된다.
//
// ===== 아트 담당자에게 부탁하면 더 쉬워지는 방법 =====
// 그림을 내보낼 때 "여백을 자르지 말고 캔버스 크기(1440x1080) 그대로" 내보내달라고 하면
// 이 도구를 쓸 필요가 아예 없어진다. 게임이 1440x1080 그림을 발견하면 화면에 그대로 깔아서
// 원래 그려진 위치에 정확히 나타나기 때문이다.
public class IllustPlacementWindow : EditorWindow
{
    // ---------------------------------------------------------------------------------
    // 상수 / 경로
    // ---------------------------------------------------------------------------------
    private const string LayoutCsvPath = "Assets/Resources/Dialogues/IllustLayout.csv";
    private const string InvestigationCsvPath = "Assets/Resources/Dialogues/InvestigationData.csv";
    private const string BackgroundFolder = "Assets/Resources/Illusts/Backgrounds";
    private const string ObjectFolder = "Assets/Resources/Illusts/Objects";
    private const string StandingFolder = "Assets/Resources/Illusts/Standings";

    private const float CanvasWidth = 1440f;
    private const float CanvasHeight = 1080f;

    // ---------------------------------------------------------------------------------
    // 편집 중인 데이터
    // ---------------------------------------------------------------------------------
    private class Item
    {
        public string fileName;
        public Texture2D texture;
        public float x, y;
        public float scale = 1f;
        public bool visible = true;
    }

    // 파일 이름 -> 배치 정보 (CSV에서 읽어와 여기서 편집하고 다시 저장한다)
    private readonly Dictionary<string, Item> allItems = new Dictionary<string, Item>();

    // 지금 미리보기에 올려둔 그림들
    private readonly List<Item> activeItems = new List<Item>();

    private Texture2D backgroundTexture;
    private string backgroundName = "";

    private Item selected;
    private Vector2 dragOffset;
    private bool dragging;

    private Vector2 listScroll;
    private Vector2 pickerScroll;
    private string search = "";
    private bool showObjects = true;
    private bool showStandings = true;
    private bool dirty;

    // 미리보기 배율 (1440x1080을 창에 맞춰 줄여서 보여준다)
    private float previewScale = 0.45f;

    [MenuItem("2KH1/일러스트 배치 도구")]
    public static void Open()
    {
        var window = GetWindow<IllustPlacementWindow>("일러스트 배치");
        window.minSize = new Vector2(1000f, 640f);
        window.LoadLayout();
    }

    // ---------------------------------------------------------------------------------
    // CSV 읽기 / 쓰기
    // ---------------------------------------------------------------------------------
    private void LoadLayout()
    {
        allItems.Clear();

        if (!File.Exists(LayoutCsvPath)) return;

        string[] lines = File.ReadAllLines(LayoutCsvPath, Encoding.UTF8);
        for (int i = 1; i < lines.Length; i++)   // 0번 줄은 컬럼 이름
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] parts = line.Split(',');
            if (parts.Length < 3) continue;

            string name = parts[0].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            float.TryParse(parts[1].Trim(), out float x);
            float.TryParse(parts[2].Trim(), out float y);
            float scale = 1f;
            if (parts.Length > 3) float.TryParse(parts[3].Trim(), out scale);
            if (scale <= 0f) scale = 1f;

            allItems[name] = new Item { fileName = name, x = x, y = y, scale = scale };
        }
    }

    private void SaveLayout()
    {
        // 미리보기에서 편집한 값을 전체 표에 반영한다.
        foreach (var item in activeItems) allItems[item.fileName] = item;

        var sb = new StringBuilder();
        sb.AppendLine("FileName,X,Y,Scale");

        // 이름 순으로 정렬해서 저장하면 나중에 CSV를 직접 열어볼 때 찾기 쉽고,
        // git diff에서도 변경된 줄만 깔끔하게 보인다.
        var names = new List<string>(allItems.Keys);
        names.Sort(System.StringComparer.Ordinal);

        foreach (string name in names)
        {
            var item = allItems[name];
            sb.AppendLine($"{name},{item.x:0.##},{item.y:0.##},{item.scale:0.###}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LayoutCsvPath));
        File.WriteAllText(LayoutCsvPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        dirty = false;
        Debug.Log($"[일러스트 배치 도구] {allItems.Count}개 배치 정보를 저장했습니다. -> {LayoutCsvPath}");
    }

    // ---------------------------------------------------------------------------------
    // 창 그리기
    // ---------------------------------------------------------------------------------
    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();     // 그림 고르기
        DrawPreview();       // 배경 + 그림 미리보기 (드래그)
        DrawRightPanel();    // 선택한 그림의 좌표 입력
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("배경 고르기", EditorStyles.toolbarDropDown, GUILayout.Width(90)))
        {
            ShowBackgroundMenu();
        }
        GUILayout.Label(string.IsNullOrEmpty(backgroundName) ? "(배경 없음)" : backgroundName,
                        EditorStyles.toolbarButton, GUILayout.Width(220));

        if (GUILayout.Button("조사 화면으로 자동 선택", EditorStyles.toolbarDropDown, GUILayout.Width(150)))
        {
            ShowInvestigationMenu();
        }

        GUILayout.Space(10);
        GUILayout.Label("미리보기 배율", EditorStyles.miniLabel, GUILayout.Width(70));
        previewScale = GUILayout.HorizontalSlider(previewScale, 0.2f, 0.8f, GUILayout.Width(100));

        GUILayout.FlexibleSpace();

        if (dirty) GUILayout.Label("● 저장 안 된 변경 있음", EditorStyles.miniLabel);

        GUI.enabled = dirty;
        if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60))) SaveLayout();
        GUI.enabled = true;

        if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            LoadLayout();
            SyncActiveFromLayout();
            dirty = false;
        }

        EditorGUILayout.EndHorizontal();
    }

    // 왼쪽: 어떤 그림을 올릴지 고르는 목록
    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        EditorGUILayout.LabelField("올릴 그림 고르기", EditorStyles.boldLabel);

        search = EditorGUILayout.TextField("검색", search);
        EditorGUILayout.BeginHorizontal();
        showObjects = GUILayout.Toggle(showObjects, "조사 오브젝트", EditorStyles.miniButtonLeft);
        showStandings = GUILayout.Toggle(showStandings, "캐릭터 스탠딩", EditorStyles.miniButtonRight);
        EditorGUILayout.EndHorizontal();

        pickerScroll = EditorGUILayout.BeginScrollView(pickerScroll);

        if (showObjects) DrawPickerFolder(ObjectFolder);
        if (showStandings) DrawPickerFolder(StandingFolder);

        EditorGUILayout.EndScrollView();

        GUILayout.Space(6);
        if (GUILayout.Button("미리보기 비우기"))
        {
            activeItems.Clear();
            selected = null;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPickerFolder(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrEmpty(search) &&
                name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            bool isActive = activeItems.Exists(i => i.fileName == name);
            bool nowActive = GUILayout.Toggle(isActive, name, EditorStyles.miniButton);

            if (nowActive && !isActive) AddToPreview(name, path);
            else if (!nowActive && isActive) RemoveFromPreview(name);
        }
    }

    // 가운데: 배경 위에 그림을 겹쳐 보여주고 마우스로 끌 수 있게 한다
    private void DrawPreview()
    {
        EditorGUILayout.BeginVertical();

        float w = CanvasWidth * previewScale;
        float h = CanvasHeight * previewScale;

        Rect area = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        // 배경
        EditorGUI.DrawRect(area, Color.black);
        if (backgroundTexture != null) GUI.DrawTexture(area, backgroundTexture, ScaleMode.StretchToFill);

        // 그림들
        foreach (var item in activeItems)
        {
            if (!item.visible || item.texture == null) continue;
            Rect r = ItemRect(item, area);
            GUI.DrawTexture(r, item.texture, ScaleMode.StretchToFill, true);

            if (item == selected)
            {
                // 선택 표시 테두리
                Handles.BeginGUI();
                Handles.color = new Color(1f, 0.8f, 0.2f);
                Handles.DrawSolidRectangleWithOutline(r, Color.clear, new Color(1f, 0.8f, 0.2f));
                Handles.EndGUI();
            }
        }

        HandlePreviewInput(area);

        EditorGUILayout.HelpBox(
            "그림을 클릭해 선택한 뒤 끌어서 위치를 맞추세요. 방향키로 1픽셀씩, Shift+방향키로 10픽셀씩 움직입니다.",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    // 화면 좌표계(가운데가 0,0, 위가 +Y)를 에디터 창의 픽셀 좌표로 바꾼다.
    private Rect ItemRect(Item item, Rect area)
    {
        float w = item.texture.width * item.scale * previewScale;
        float h = item.texture.height * item.scale * previewScale;

        // 화면 가운데 기준 좌표 -> 미리보기 영역 안의 좌표
        float cx = area.x + area.width * 0.5f + item.x * previewScale;
        float cy = area.y + area.height * 0.5f - item.y * previewScale;  // Y는 위가 +라서 부호를 뒤집는다

        return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
    }

    private void HandlePreviewInput(Rect area)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
        {
            // 위에 그려진 것부터 검사해야 겹쳤을 때 앞엣것이 잡힌다.
            for (int i = activeItems.Count - 1; i >= 0; i--)
            {
                var item = activeItems[i];
                if (!item.visible || item.texture == null) continue;

                if (ItemRect(item, area).Contains(e.mousePosition))
                {
                    selected = item;
                    dragging = true;

                    Rect r = ItemRect(item, area);
                    dragOffset = e.mousePosition - new Vector2(r.center.x, r.center.y);

                    e.Use();
                    Repaint();
                    return;
                }
            }

            selected = null;
            Repaint();
        }

        if (dragging && e.type == EventType.MouseDrag && selected != null)
        {
            Vector2 center = e.mousePosition - dragOffset;
            selected.x = (center.x - (area.x + area.width * 0.5f)) / previewScale;
            selected.y = ((area.y + area.height * 0.5f) - center.y) / previewScale;
            dirty = true;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.MouseUp && dragging)
        {
            dragging = false;
            e.Use();
        }

        // 방향키 미세 조정
        if (e.type == EventType.KeyDown && selected != null)
        {
            float step = e.shift ? 10f : 1f;
            bool moved = true;

            switch (e.keyCode)
            {
                case KeyCode.LeftArrow: selected.x -= step; break;
                case KeyCode.RightArrow: selected.x += step; break;
                case KeyCode.UpArrow: selected.y += step; break;
                case KeyCode.DownArrow: selected.y -= step; break;
                default: moved = false; break;
            }

            if (moved)
            {
                dirty = true;
                e.Use();
                Repaint();
            }
        }
    }

    // 오른쪽: 선택한 그림의 좌표를 숫자로 조정
    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(230));
        EditorGUILayout.LabelField("미리보기에 올린 그림", EditorStyles.boldLabel);

        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(220));
        foreach (var item in activeItems)
        {
            EditorGUILayout.BeginHorizontal();

            bool isSel = item == selected;
            if (GUILayout.Toggle(isSel, item.fileName, EditorStyles.miniButton) != isSel)
            {
                selected = item;
            }
            item.visible = GUILayout.Toggle(item.visible, "표시", GUILayout.Width(40));

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(8);

        if (selected == null)
        {
            EditorGUILayout.HelpBox("그림을 선택하면 좌표를 조정할 수 있습니다.", MessageType.None);
        }
        else
        {
            EditorGUILayout.LabelField(selected.fileName, EditorStyles.boldLabel);
            if (selected.texture != null)
            {
                EditorGUILayout.LabelField($"원본 크기: {selected.texture.width} x {selected.texture.height}");
            }

            EditorGUI.BeginChangeCheck();
            selected.x = EditorGUILayout.FloatField("X (오른쪽 +)", selected.x);
            selected.y = EditorGUILayout.FloatField("Y (위쪽 +)", selected.y);
            selected.scale = EditorGUILayout.Slider("크기 배율", selected.scale, 0.2f, 3f);
            if (EditorGUI.EndChangeCheck()) dirty = true;

            GUILayout.Space(6);
            if (GUILayout.Button("가운데로 되돌리기"))
            {
                selected.x = 0f;
                selected.y = 0f;
                selected.scale = 1f;
                dirty = true;
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.HelpBox(
            "아트 담당자가 그림을 1440x1080 캔버스 크기 그대로 내보내주면 " +
            "이 도구로 위치를 잡을 필요가 없습니다.",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    // ---------------------------------------------------------------------------------
    // 목록 조작
    // ---------------------------------------------------------------------------------
    private void AddToPreview(string name, string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) return;

        if (!allItems.TryGetValue(name, out Item item))
        {
            item = new Item { fileName = name, x = 0f, y = 0f, scale = 1f };
            allItems[name] = item;
        }
        item.texture = texture;

        if (!activeItems.Contains(item)) activeItems.Add(item);
        selected = item;
    }

    private void RemoveFromPreview(string name)
    {
        int index = activeItems.FindIndex(i => i.fileName == name);
        if (index < 0) return;

        if (selected == activeItems[index]) selected = null;
        activeItems.RemoveAt(index);
    }

    // CSV를 다시 읽었을 때 미리보기에 올려둔 그림들의 좌표도 갱신한다.
    private void SyncActiveFromLayout()
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            if (allItems.TryGetValue(activeItems[i].fileName, out Item fromCsv))
            {
                fromCsv.texture = activeItems[i].texture;
                activeItems[i] = fromCsv;
            }
        }
        selected = null;
    }

    // ---------------------------------------------------------------------------------
    // 메뉴
    // ---------------------------------------------------------------------------------
    private void ShowBackgroundMenu()
    {
        var menu = new GenericMenu();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { BackgroundFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            menu.AddItem(new GUIContent(name), backgroundName == name, () =>
            {
                backgroundName = name;
                backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Repaint();
            });
        }

        if (guids.Length == 0) menu.AddDisabledItem(new GUIContent("배경 그림이 없습니다"));
        menu.ShowAsContext();
    }

    // InvestigationData.csv를 읽어서 "이 조사 화면에 속한 오브젝트"를 한 번에 올려준다.
    private void ShowInvestigationMenu()
    {
        var menu = new GenericMenu();

        if (!File.Exists(InvestigationCsvPath))
        {
            menu.AddDisabledItem(new GUIContent("InvestigationData.csv가 없습니다"));
            menu.ShowAsContext();
            return;
        }

        // 조사 화면 id -> (배경 이름, 오브젝트 그림 이름들)
        var screens = new Dictionary<string, (string bg, List<string> sprites)>();

        string[] lines = File.ReadAllLines(InvestigationCsvPath, Encoding.UTF8);
        // 헤더에서 각 컬럼이 몇 번째인지 찾아둔다(컬럼 순서가 바뀌어도 동작하도록).
        string[] header = lines.Length > 0 ? SplitCsvLine(lines[0]) : new string[0];
        int idCol = System.Array.IndexOf(header, "InvestigationId");
        int keyCol = System.Array.IndexOf(header, "HotspotKey");
        int spriteCol = System.Array.IndexOf(header, "Sprite");
        if (idCol < 0 || keyCol < 0 || spriteCol < 0)
        {
            menu.AddDisabledItem(new GUIContent("CSV에 필요한 컬럼이 없습니다"));
            menu.ShowAsContext();
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string[] parts = SplitCsvLine(lines[i]);
            if (parts.Length <= spriteCol) continue;

            string id = parts[idCol].Trim();
            string key = parts[keyCol].Trim();
            string sprite = parts[spriteCol].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            if (!screens.ContainsKey(id)) screens[id] = ("", new List<string>());

            if (key == "Background")
            {
                screens[id] = (sprite, screens[id].sprites);
            }
            else if (!string.IsNullOrEmpty(sprite))
            {
                screens[id].sprites.Add(sprite);
            }
        }

        foreach (var pair in screens)
        {
            string id = pair.Key;
            var data = pair.Value;

            menu.AddItem(new GUIContent($"{id}  ({data.sprites.Count}개)"), false, () =>
            {
                // 배경 세팅
                if (!string.IsNullOrEmpty(data.bg))
                {
                    string bgPath = $"{BackgroundFolder}/{data.bg}.png";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(bgPath);
                    if (tex != null)
                    {
                        backgroundName = data.bg;
                        backgroundTexture = tex;
                    }
                }

                // 오브젝트 올리기
                activeItems.Clear();
                selected = null;
                foreach (string sprite in data.sprites)
                {
                    AddToPreview(sprite, $"{ObjectFolder}/{sprite}.png");
                }
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    // 따옴표 안의 쉼표를 무시하고 CSV 한 줄을 자른다.
    // (CSVReader.cs가 게임에서 쓰는 것과 같은 규칙. 에디터 코드에서 게임 코드를 직접
    //  가져다 쓰면 어셈블리 참조가 얽히므로 간단한 버전을 여기 따로 두었다.)
    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(c);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }
}
