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
//   2) 위쪽에서 배경 그림을 고른다 (예: BG_01_MyDesk). 아래 세 가지 방법이 있다.
//      - [배경 고르기]        : 배경 파일을 직접 고른다.
//      - [조사 화면 불러오기] : InvestigationData.csv를 읽어 그 화면의 오브젝트까지 한 번에 올린다.
//      - [시나리오 장면 불러오기] : scenario_*.csv를 읽어 "그 줄에서 화면이 어떻게 보이는지"
//                              (배경 + 그 줄에 세워진 캐릭터 스탠딩)를 그대로 재현한다.
//   3) 왼쪽 목록에서 올릴 그림을 더 체크하거나 뺀다.
//   4) 미리보기에서 그림을 마우스로 끌어 제자리에 놓는다.
//      - 클릭하면 선택되고, 방향키로 1픽셀씩(Shift=10픽셀) 움직인다.
//      - 오른쪽 패널에서 X, Y, 크기를 숫자로 직접 넣을 수도 있다.
//   5) [저장](또는 Ctrl+S)을 누르면 Assets/StreamingAssets/Dialogues/IllustLayout.csv 에 기록된다.
//      이 폴더는 git으로 공유되므로, 저장한 뒤 commit/push만 하면 팀원 모두에게 전달된다
//      (구글 드라이브에 따로 올릴 필요 없음 - 이슈 #10).
//
// ===== 저장 범위: [공통] 과 [이 화면 전용] =====
// 같은 오브젝트가 여러 배경에 나올 때 배경마다 다른 자리에 놓고 싶을 수 있다. 그래서
// 툴바에서 저장 범위를 고를 수 있게 했다.
//   [공통]          : 모든 화면에서 쓰는 기본 좌표를 고친다 (IllustLayout.csv의 Screen 칸이 빈 줄).
//   [이 화면 전용]  : 지금 고른 배경에서만 쓰는 좌표를 따로 만든다 (Screen 칸에 배경 이름이 들어감).
// 대부분은 [공통]으로 충분하고, 특정 배경에서만 어긋날 때 [이 화면 전용]으로 덮어쓰면 된다.
//
// ===== 표정은 하나만 잡으면 된다 =====
// STD_Past05_Hansung_Angry_OpenMouse 처럼 표정만 다른 그림은 배치표에 없으면
// STD_Past05_Hansung 줄을 자동으로 물려받는다. 그래서 캐릭터당 한 번만 잡으면 되고,
// 오른쪽 패널에 "(STD_Past05_Hansung 에서 물려받음)"이라고 표시된다.
// 특정 표정만 자세가 달라 따로 잡고 싶으면 그 표정을 움직여서 저장하면 그 줄이 우선된다.
// (자세한 규칙은 IllustLayout.cs의 [표정 상속] 주석 참고)
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
    // ===== 이 도구가 고치는 CSV는 전부 git으로 공유한다 (이슈 #10) =====
    // 예전에는 Assets/Resources/Dialogues/ 에 있었는데 그 폴더는 gitignore라, 좌표를 고칠 때마다
    // 구글 드라이브에 다시 올려야 했다. 지금은 Assets/StreamingAssets/Dialogues/ 에 두고
    // 저장 → git commit/push만 하면 팀원 모두에게 전달된다. (CSVReader.cs 상단 주석 참고)
    // 그림은 용량이 커서 계속 Resources(드라이브 공유)에 둔다 - 아래 Illusts 경로들.
    private const string LayoutCsvPath = "Assets/StreamingAssets/Dialogues/IllustLayout.csv";
    private const string InvestigationCsvPath = "Assets/StreamingAssets/Dialogues/InvestigationData.csv";
    private const string DialogueFolder = "Assets/StreamingAssets/Dialogues";
    private const string BackgroundFolder = "Assets/Resources/Illusts/Backgrounds";
    private const string ObjectFolder = "Assets/Resources/Illusts/Objects";
    private const string StandingFolder = "Assets/Resources/Illusts/Standings";

    private const float CanvasWidth = 1440f;
    private const float CanvasHeight = 1080f;

    // ---------------------------------------------------------------------------------
    // 편집 중인 데이터
    // ---------------------------------------------------------------------------------
    // 배치표의 한 줄에 해당한다.
    private class Row
    {
        public string fileName;
        public string screen;   // "" = 모든 화면 공통
        public float x, y;
        public float scale = 1f;
    }

    // 미리보기에 올려둔 그림 하나. 화면에 그려지는 값(x/y/scale)을 들고 있다가
    // 저장할 때 배치표 줄로 옮겨 적는다.
    private class Item
    {
        public string fileName;
        public Texture2D texture;
        public float x, y;
        public float scale = 1f;
        public bool visible = true;

        // 이 그림이 지금 어디서 좌표를 얻어왔는지 (오른쪽 패널에 표시하고, 저장 여부 판단에 쓴다)
        public bool hasScreenRow;      // 이 화면 전용 줄이 이미 있다
        public bool hasGlobalRow;      // 공통 줄이 이미 있다
        public string inheritedFrom;   // 표정 상속으로 물려받았다면 그 줄의 이름 (아니면 null)
        public bool edited;            // 이번에 사용자가 실제로 옮겼다
    }

    // 배치표 전체. 키는 "화면\0파일이름" (IllustLayout.cs와 같은 방식).
    private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();

    // 지금 미리보기에 올려둔 그림들
    private readonly List<Item> activeItems = new List<Item>();

    private Texture2D backgroundTexture;
    private string backgroundName = "";

    // 저장 범위. true면 지금 배경 전용 줄로 저장한다.
    private bool saveToCurrentScreen;

    private Item selected;
    private Vector2 dragOffset;
    private bool dragging;

    private Vector2 listScroll;
    private Vector2 pickerScroll;
    private string search = "";
    private bool showObjects = true;
    private bool showStandings = true;
    private bool dirty;

    // CSV를 읽다가 이상한 줄을 만나면 여기에 쌓아 창에 보여준다.
    // (예전에는 조용히 건너뛰어서, CSV가 망가져도 "도구가 그냥 안 되는" 것처럼 보였다)
    private readonly List<string> loadWarnings = new List<string>();

    // ---------------------------------------------------------------------------------
    // 조사 화면 소속 편집 (InvestigationData.csv)
    // ---------------------------------------------------------------------------------
    // ===== 왜 이게 따로 필요한가? (아주 중요) =====
    // 이 도구의 왼쪽 체크박스 목록은 "미리보기에 띄워서 좌표를 잡을 그림"을 고르는 것일 뿐이다.
    // 반면 **조사 화면에 실제로 어떤 오브젝트가 존재하는지는 InvestigationData.csv가 정한다.**
    //
    // 예전에는 이 둘이 완전히 따로 놀아서, 왼쪽에서 오브젝트를 체크 해제하고 저장해도
    // 화면에서는 그대로 남아 있고, 새 오브젝트를 체크해서 좌표를 잡아도 게임에는 나오지 않았다.
    // 다시 [조사 화면 불러오기]를 누르면 InvestigationData.csv를 다시 읽으므로 지웠다고 생각한
    // 오브젝트가 전부 되살아났다. "삭제가 적용이 안 된다"의 정체가 이것이다.
    //
    // 그래서 아래에 InvestigationData.csv를 직접 읽고 쓰는 기능을 넣어, 화면 소속을 이 창에서
    // 바로 넣고 뺄 수 있게 했다.
    //
    // ===== 안전장치 =====
    // 이 CSV에는 사람이 쓴 조사 대사(Text 칸)가 들어 있으므로 절대 함부로 덮어쓰면 안 된다.
    // 그래서 파일 전체를 표 그대로 읽어두었다가, **지금 고른 화면에 해당하는 줄만** 넣고 빼고,
    // 나머지 줄과 칸은 글자 하나 바꾸지 않고 그대로 다시 쓴다.
    private List<string[]> investigationTable;   // 헤더 포함 전체 표
    private string investigationScreenId;        // 지금 소속을 편집 중인 조사 화면 id
    private bool investigationDirty;             // 소속을 바꿨는데 아직 저장 안 함
    private Vector2 memberScroll;

    // 미리보기 배율 (1440x1080을 창에 맞춰 줄여서 보여준다)
    private float previewScale = 0.45f;

    [MenuItem("2KH1/일러스트 배치 도구")]
    public static void Open()
    {
        var window = GetWindow<IllustPlacementWindow>("일러스트 배치");
        window.minSize = new Vector2(1040f, 660f);
        window.LoadLayout();
    }

    // ===== 창을 닫을 때 =====
    // 저장하지 않은 변경이 있으면 물어보고, 저장을 고르면 그대로 저장한다.
    // 예전에는 아무 경고 없이 닫혀서 애써 잡아둔 배치가 통째로 날아갔다.
    // (OnDestroy는 창이 닫힐 때 유니티가 자동으로 불러주는 함수다)
    private void OnDestroy()
    {
        if (dirty && EditorUtility.DisplayDialog(
                "일러스트 배치 도구",
                "저장하지 않은 배치 좌표 변경이 있습니다. 저장할까요?",
                "저장", "버리고 닫기"))
        {
            SaveLayout();
        }

        // 좌표(IllustLayout.csv) · 조사 화면 소속(InvestigationData.csv) · 시나리오 장면
        // (scenario_*.csv)은 서로 다른 파일이라 각각 따로 묻는다.
        if (investigationDirty && EditorUtility.DisplayDialog(
                "일러스트 배치 도구",
                "저장하지 않은 조사 화면 소속 변경이 있습니다. 저장할까요?",
                "저장", "버리고 닫기"))
        {
            SaveInvestigationTable();
        }

        if (scenarioDirty && EditorUtility.DisplayDialog(
                "일러스트 배치 도구",
                "저장하지 않은 시나리오 장면 변경이 있습니다. 저장할까요?",
                "저장", "버리고 닫기"))
        {
            SaveScenarioTable();
        }
    }

    // ---------------------------------------------------------------------------------
    // 견고한 CSV 파서
    // ---------------------------------------------------------------------------------
    // ===== 왜 직접 만들었나? =====
    // 예전에는 File.ReadAllLines로 줄을 자르고 쉼표로 쪼갰는데, 엑셀로 CSV를 한 번만 저장해도
    // 다음 것들 때문에 줄이 통째로 어긋나거나 무시되어 도구가 "그냥 안 되는" 상태가 됐다.
    //   1) 셀 안의 줄바꿈  : 엑셀에서 Alt+Enter를 쓰면 따옴표 안에 진짜 줄바꿈이 들어간다.
    //                        줄 단위로 자르면 그 뒤 모든 행이 밀린다.
    //   2) 큰따옴표 이스케이프 : 셀 안의 "는 CSV에서 ""로 적힌다. 이걸 모르면 열이 밀린다.
    //   3) BOM              : 엑셀은 파일 맨 앞에 눈에 안 보이는 표식 3바이트를 붙인다.
    //                        그러면 첫 컬럼 이름이 "InvestigationId"가 아니게 되어 못 찾는다.
    //   4) 뒤쪽 빈 칸 생략  : 엑셀은 행 끝의 빈 셀을 쉼표째 빼고 저장하기도 한다.
    //                        마지막 열(Sprite)을 읽으려다 실패해 그 행을 버리게 된다.
    // 아래 파서는 이 네 가지를 전부 견딘다. 부족한 열은 빈 문자열로 채워서 돌려준다.
    private static List<string[]> ParseCsv(string text, int minimumColumns = 0)
    {
        var result = new List<string[]>();
        if (string.IsNullOrEmpty(text)) return result;

        // BOM 제거 (파일 맨 앞의 U+FEFF)
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // 따옴표 두 개("")는 "글자로서의 따옴표 한 개"라는 뜻이다.
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;   // 여기서 따옴표 구간이 끝난다
                }
                else sb.Append(c);           // 따옴표 안에서는 쉼표도 줄바꿈도 그냥 글자다
                continue;
            }

            if (c == '"') { inQuotes = true; continue; }
            if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); continue; }

            if (c == '\r' || c == '\n')
            {
                // \r\n은 두 글자지만 줄바꿈 하나다.
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;

                fields.Add(sb.ToString());
                sb.Clear();
                AddRow(result, fields, minimumColumns);
                fields.Clear();
                continue;
            }

            sb.Append(c);
        }

        // 파일이 줄바꿈 없이 끝나는 경우의 마지막 줄
        if (sb.Length > 0 || fields.Count > 0)
        {
            fields.Add(sb.ToString());
            AddRow(result, fields, minimumColumns);
        }

        return result;
    }

    // 완전히 빈 줄은 버리고, 열이 모자라면 빈 문자열로 채워서 넣는다.
    private static void AddRow(List<string[]> result, List<string> fields, int minimumColumns)
    {
        bool allEmpty = true;
        for (int i = 0; i < fields.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(fields[i])) { allEmpty = false; break; }
        }
        if (allEmpty) return;

        int width = Mathf.Max(fields.Count, minimumColumns);
        var row = new string[width];
        for (int i = 0; i < width; i++) row[i] = i < fields.Count ? fields[i] : "";
        result.Add(row);
    }

    // 헤더 배열에서 컬럼 위치를 찾는다. 앞뒤 공백과 대소문자는 무시한다.
    private static int ColumnIndex(string[] header, string name)
    {
        for (int i = 0; i < header.Length; i++)
        {
            if (string.Equals(header[i].Trim(), name, System.StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private static string Cell(string[] row, int index)
    {
        return (index >= 0 && index < row.Length) ? row[index].Trim() : "";
    }

    // ---------------------------------------------------------------------------------
    // 배치표 읽기 / 쓰기
    // ---------------------------------------------------------------------------------
    private static string MakeKey(string screen, string fileName)
    {
        return (screen ?? "") + "\0" + (fileName ?? "");
    }

    private void LoadLayout()
    {
        rows.Clear();
        loadWarnings.Clear();

        if (!File.Exists(LayoutCsvPath)) return;

        var table = ParseCsv(File.ReadAllText(LayoutCsvPath, Encoding.UTF8));
        if (table.Count == 0) return;

        string[] header = table[0];
        int nameCol = ColumnIndex(header, "FileName");
        int xCol = ColumnIndex(header, "X");
        int yCol = ColumnIndex(header, "Y");
        int scaleCol = ColumnIndex(header, "Scale");
        int screenCol = ColumnIndex(header, "Screen");   // 없으면 -1 = 전부 공통으로 읽힌다

        if (nameCol < 0 || xCol < 0 || yCol < 0)
        {
            loadWarnings.Add($"{Path.GetFileName(LayoutCsvPath)}: FileName/X/Y 컬럼을 찾지 못했습니다. 헤더를 확인하세요.");
            return;
        }

        for (int i = 1; i < table.Count; i++)
        {
            string name = Cell(table[i], nameCol);
            if (string.IsNullOrEmpty(name)) continue;

            float.TryParse(Cell(table[i], xCol), out float x);
            float.TryParse(Cell(table[i], yCol), out float y);
            if (!float.TryParse(Cell(table[i], scaleCol), out float scale) || scale <= 0f) scale = 1f;

            string screen = screenCol >= 0 ? Cell(table[i], screenCol) : "";

            rows[MakeKey(screen, name)] = new Row { fileName = name, screen = screen, x = x, y = y, scale = scale };
        }
    }

    private void SaveLayout()
    {
        // 미리보기에서 옮긴 값을 배치표에 반영한다.
        string targetScreen = saveToCurrentScreen ? backgroundName : "";

        foreach (var item in activeItems)
        {
            // ===== 캐릭터 스탠딩은 "캐릭터 기본 이름"으로 저장한다 =====
            // 예전에는 드래그한 그림의 파일 이름(예: STD_Past01_Hansung_Default)으로 저장해서,
            // 그 표정 하나에만 좌표가 붙고 Angry 같은 다른 표정은 옛 좌표에 그대로 서 있었다.
            // 스탠딩 위치는 캐릭터마다 하나여야 하므로(표정은 CSV가 줄마다 바꾸는 것일 뿐),
            // 어느 표정을 끌어서 옮기든 STD_장면_캐릭터 한 줄로 저장하고, 같은 캐릭터의
            // 표정별 줄은 지워서 모든 표정이 이 좌표를 따르게 한다.
            // (_Stand 계열은 자세가 다른 별개의 스탠딩이라 STD_장면_캐릭터_Stand 로 저장된다 -
            //  이름을 자르는 규칙은 IllustLayout.NameCandidates 와 같다)
            bool isStanding = item.fileName.StartsWith("STD_", System.StringComparison.OrdinalIgnoreCase);
            var nameCandidates = IllustLayout.NameCandidates(item.fileName);
            string saveName = isStanding && nameCandidates.Count > 0
                ? nameCandidates[nameCandidates.Count - 1]
                : item.fileName;

            if (isStanding)
            {
                // 스탠딩은 실제로 옮긴 경우에만 저장한다(물려받은 좌표를 다시 적을 필요 없음).
                if (!item.edited) continue;

                var stale = new List<string>();
                foreach (var pair in rows)
                {
                    if (pair.Value.screen != targetScreen) continue;
                    if (pair.Value.fileName == saveName) continue;
                    var c = IllustLayout.NameCandidates(pair.Value.fileName);
                    if (c.Count > 0 && c[c.Count - 1] == saveName) stale.Add(pair.Key);   // 같은 캐릭터의 표정별 줄
                }
                foreach (string k in stale) rows.Remove(k);
            }
            else
            {
                // 사용자가 손대지 않았고 이미 어딘가에서 좌표를 물려받고 있다면, 굳이 새 줄을
                // 만들지 않는다. 그래야 "그림 하나 보려고 올렸을 뿐인데 줄이 잔뜩 생기는" 일이 없다.
                bool alreadyStored = saveToCurrentScreen ? item.hasScreenRow : item.hasGlobalRow;
                if (!item.edited && !alreadyStored) continue;
            }

            string key = MakeKey(targetScreen, saveName);
            rows[key] = new Row
            {
                fileName = saveName,
                screen = targetScreen,
                x = item.x,
                y = item.y,
                scale = item.scale
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine("FileName,X,Y,Scale,Screen");

        // 화면 -> 이름 순으로 정렬해서 저장하면 CSV를 직접 열어볼 때 찾기 쉽고,
        // git diff에서도 변경된 줄만 깔끔하게 보인다.
        var list = new List<Row>(rows.Values);
        list.Sort((a, b) =>
        {
            int byScreen = string.CompareOrdinal(a.screen, b.screen);
            return byScreen != 0 ? byScreen : string.CompareOrdinal(a.fileName, b.fileName);
        });

        foreach (var row in list)
        {
            // 좌표는 정수로 저장한다. 소수 자리에 놓인 그림은 게임에서 픽셀 사이에 걸쳐 그려져
            // 선이 번지고 흐릿해 보인다 (IllustLayout.cs의 반올림 주석 참고).
            int x = Mathf.RoundToInt(row.x);
            int y = Mathf.RoundToInt(row.y);
            sb.AppendLine($"{row.fileName},{x},{y},{row.scale:0.###},{row.screen}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LayoutCsvPath));
        File.WriteAllText(LayoutCsvPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        // 방금 저장한 상태를 기준으로 다시 표시한다(어느 줄이 생겼는지 반영).
        // 저장이 끝났으니 "수정" 표시도 지운다 - 안 지우면 이미 저장된 것이 계속
        // 저장 안 된 것처럼 보인다.
        LoadLayout();
        foreach (var item in activeItems) item.edited = false;
        RefreshItemOrigins();

        dirty = false;
        Debug.Log($"[일러스트 배치 도구] {rows.Count}줄을 저장했습니다. -> {LayoutCsvPath}");
    }

    // ---------------------------------------------------------------------------------
    // 조사 화면 소속 읽기 / 쓰기 (InvestigationData.csv)
    // ---------------------------------------------------------------------------------

    // CSV 한 칸을 안전하게 쓰기 위한 처리.
    // 칸 안에 쉼표나 큰따옴표나 줄바꿈이 들어 있으면 통째로 따옴표로 감싸고,
    // 안에 있던 따옴표는 두 개("")로 바꿔 적는다. 이것이 CSV의 표준 규칙이며,
    // 이렇게 써야 사람이 쓴 대사에 쉼표가 있어도 열이 밀리지 않는다.
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        bool needsQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
                          || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
        if (!needsQuote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private void LoadInvestigationTable()
    {
        investigationTable = null;
        investigationDirty = false;

        if (!File.Exists(InvestigationCsvPath))
        {
            loadWarnings.Add("InvestigationData.csv가 없습니다.");
            return;
        }

        var table = ParseCsv(File.ReadAllText(InvestigationCsvPath, Encoding.UTF8));
        if (table.Count == 0)
        {
            loadWarnings.Add("InvestigationData.csv가 비어 있습니다.");
            return;
        }

        // 필요한 컬럼이 전부 있는지 먼저 확인한다. 하나라도 없으면 소속 편집을 막는다
        // (잘못 쓰면 사람이 쓴 대사가 엉뚱한 칸으로 밀려 들어가기 때문).
        string[] header = table[0];
        foreach (string need in new[] { "InvestigationId", "HotspotKey", "Type", "ObjectName", "Sprite" })
        {
            if (ColumnIndex(header, need) < 0)
            {
                loadWarnings.Add($"InvestigationData.csv에 '{need}' 컬럼이 없어 소속 편집을 할 수 없습니다.");
                return;
            }
        }

        investigationTable = table;
    }

    private void SaveInvestigationTable()
    {
        if (investigationTable == null || investigationTable.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var row in investigationTable)
        {
            for (int i = 0; i < row.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(row[i]));
            }
            sb.Append('\n');
        }

        File.WriteAllText(InvestigationCsvPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        investigationDirty = false;
        Debug.Log($"[일러스트 배치 도구] 조사 화면 소속을 저장했습니다. -> {InvestigationCsvPath}");
    }

    // 이 조사 화면에 속한 오브젝트 줄들의 인덱스를 찾는다.
    // Background/IntroText는 오브젝트가 아니라 화면 설정이므로 제외한다.
    private List<int> MemberRowIndices(string screenId)
    {
        var result = new List<int>();
        if (investigationTable == null || string.IsNullOrEmpty(screenId)) return result;

        string[] header = investigationTable[0];
        int idCol = ColumnIndex(header, "InvestigationId");
        int keyCol = ColumnIndex(header, "HotspotKey");

        for (int i = 1; i < investigationTable.Count; i++)
        {
            if (!string.Equals(Cell(investigationTable[i], idCol), screenId,
                               System.StringComparison.Ordinal)) continue;

            string key = Cell(investigationTable[i], keyCol);
            if (string.Equals(key, "Background", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(key, "IntroText", System.StringComparison.OrdinalIgnoreCase)) continue;
            // NextScreen/PrevScreen은 "옆 화면으로 가는 화살표"를 만드는 특수 줄이라
            // 조사 오브젝트가 아니다 (InvestigationController.cs 참고). Sprite 칸에 이동할
            // 화면 이름이 적혀 있어서 걸러내지 않으면 오브젝트인 척 목록에 섞여 들어가고,
            // 배치 도구에서 지우거나 옮기면 화살표가 통째로 사라진다.
            if (string.Equals(key, "NextScreen", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(key, "PrevScreen", System.StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(i);
        }
        return result;
    }

    private string MemberSprite(int rowIndex)
    {
        return Cell(investigationTable[rowIndex], ColumnIndex(investigationTable[0], "Sprite"));
    }

    private string MemberKey(int rowIndex)
    {
        return Cell(investigationTable[rowIndex], ColumnIndex(investigationTable[0], "HotspotKey"));
    }

    // 이 조사 화면의 배경을 바꾼다.
    // InvestigationData.csv에서 HotspotKey=Background 인 행의 Sprite 칸이 배경 이름이다.
    // 그런 행이 없으면 새로 만들어준다(조사 화면 이름이 곧 배경이던 화면도 있기 때문).
    private void SetInvestigationBackground(string newBg)
    {
        if (investigationTable == null || string.IsNullOrEmpty(investigationScreenId)) return;

        string[] header = investigationTable[0];
        int idCol = ColumnIndex(header, "InvestigationId");
        int keyCol = ColumnIndex(header, "HotspotKey");
        int sprCol = ColumnIndex(header, "Sprite");

        for (int i = 1; i < investigationTable.Count; i++)
        {
            if (!string.Equals(Cell(investigationTable[i], idCol), investigationScreenId,
                               System.StringComparison.Ordinal)) continue;
            if (!string.Equals(Cell(investigationTable[i], keyCol), "Background",
                               System.StringComparison.OrdinalIgnoreCase)) continue;

            // 엑셀이 뒤쪽 빈 칸을 잘라낸 줄이면 칸을 늘려준다.
            if (investigationTable[i].Length <= sprCol)
            {
                var grown = new string[sprCol + 1];
                System.Array.Copy(investigationTable[i], grown, investigationTable[i].Length);
                for (int j = investigationTable[i].Length; j <= sprCol; j++) grown[j] = "";
                investigationTable[i] = grown;
            }

            investigationTable[i][sprCol] = newBg;
            investigationDirty = true;
            return;
        }

        // Background 행이 없으면 이 화면 줄들 맨 앞에 하나 만들어 넣는다.
        var row = new string[header.Length];
        for (int j = 0; j < row.Length; j++) row[j] = "";
        row[idCol] = investigationScreenId;
        row[keyCol] = "Background";
        row[sprCol] = newBg;

        int last = LastRowIndexOfScreen(investigationScreenId);
        investigationTable.Insert(last >= 0 ? last + 1 : investigationTable.Count, row);
        investigationDirty = true;
    }

    // 이 화면에 속한 마지막 줄의 위치(Background/IntroText 포함). 없으면 -1.
    // 새 오브젝트를 파일 어디에 끼워 넣을지 정하는 데 쓴다.
    private int LastRowIndexOfScreen(string screenId)
    {
        if (investigationTable == null || string.IsNullOrEmpty(screenId)) return -1;

        int idCol = ColumnIndex(investigationTable[0], "InvestigationId");
        int last = -1;
        for (int i = 1; i < investigationTable.Count; i++)
        {
            if (string.Equals(Cell(investigationTable[i], idCol), screenId, System.StringComparison.Ordinal))
            {
                last = i;
            }
        }
        return last;
    }

    // 그림 이름으로 핫스팟 키를 만든다. 기존 규칙(OBJ_07_BookCase -> Hotspot_BookCase)을 따른다.
    private string MakeHotspotKey(string spriteName, string screenId)
    {
        string bare = spriteName;

        // 앞의 "OBJ_숫자_" 부분을 떼어낸다. 없으면 그대로 둔다.
        var parts = spriteName.Split('_');
        if (parts.Length >= 3 && parts[0].Equals("OBJ", System.StringComparison.OrdinalIgnoreCase))
        {
            bare = string.Join("_", parts, 2, parts.Length - 2);
        }

        string key = "Hotspot_" + bare;

        // 같은 화면 안에서 키가 겹치면 뒤에 숫자를 붙인다(핫스팟 키는 화면 안에서 유일해야 한다).
        var used = new HashSet<string>();
        foreach (int i in MemberRowIndices(screenId)) used.Add(MemberKey(i));

        string unique = key;
        int n = 2;
        while (used.Contains(unique)) unique = key + n++;
        return unique;
    }

    // 이 화면에 오브젝트를 새로 넣는다.
    //   hotspotType : "Description"(조사 가능) 또는 "Standing"/"Prop"(장식, 누를 수 없음)
    private void AddMember(string screenId, string spriteName, string hotspotType = "Description")
    {
        if (investigationTable == null || string.IsNullOrEmpty(screenId)) return;

        string[] header = investigationTable[0];
        var row = new string[header.Length];
        for (int i = 0; i < row.Length; i++) row[i] = "";

        row[ColumnIndex(header, "InvestigationId")] = screenId;
        row[ColumnIndex(header, "HotspotKey")] = MakeHotspotKey(spriteName, screenId);
        // 조사 오브젝트의 기본값은 Description(누르면 설명 문구만 뜨는 가장 안전한 종류).
        // 아이템으로 만들려면 나중에 엑셀에서 Type=Item, ItemId를 채우면 된다.
        // Standing/Prop은 장식이라 대사도 아이템도 필요 없다.
        row[ColumnIndex(header, "Type")] = hotspotType;
        row[ColumnIndex(header, "ObjectName")] = spriteName;
        row[ColumnIndex(header, "Sprite")] = spriteName;
        // Text 칸은 일부러 비워둔다 - 조사했을 때 나올 대사는 사람이 써야 하는 내용이라
        // 도구가 지어내면 안 된다. 비어 있으면 창에 "대사 없음" 경고로 표시된다.

        // 같은 화면 줄들 바로 뒤에 끼워 넣어 파일이 화면별로 뭉쳐 있게 유지한다.
        // 오브젝트를 전부 빼버려서 남은 줄이 Background/IntroText뿐인 경우에도, 그 뒤에
        // 붙여야 파일이 흩어지지 않는다(예전에는 파일 맨 끝으로 가버렸다).
        int insertAt = LastRowIndexOfScreen(screenId);
        insertAt = insertAt >= 0 ? insertAt + 1 : investigationTable.Count;
        investigationTable.Insert(insertAt, row);

        investigationDirty = true;
    }

    // 이 화면에서 오브젝트를 뺀다(그 줄을 지운다).
    private void RemoveMember(int rowIndex)
    {
        if (investigationTable == null) return;
        if (rowIndex <= 0 || rowIndex >= investigationTable.Count) return;

        investigationTable.RemoveAt(rowIndex);
        investigationDirty = true;
    }

    // ---------------------------------------------------------------------------------
    // 좌표 물려받기 (IllustLayout과 같은 규칙)
    // ---------------------------------------------------------------------------------
    // 이 그림이 지금 화면에서 실제로 어떤 좌표를 쓰게 되는지 계산한다.
    // 게임에서 쓰는 IllustLayout.NameCandidates()를 그대로 불러서 규칙이 어긋나지 않게 한다.
    private bool ResolvePlacement(string fileName, out Row found, out string inheritedFrom)
    {
        found = null;
        inheritedFrom = null;

        var candidates = IllustLayout.NameCandidates(fileName);
        if (candidates.Count == 0) return false;   // 이름이 비어 있으면 찾을 것도 없다

        string screen = backgroundName ?? "";

        // 1) 이 화면 + 정확한 이름  /  2) 공통 + 정확한 이름
        if (!string.IsNullOrEmpty(screen) && rows.TryGetValue(MakeKey(screen, candidates[0]), out found)) return true;
        if (rows.TryGetValue(MakeKey("", candidates[0]), out found)) return true;

        // 3) 이 화면 + 잘라낸 이름  /  4) 공통 + 잘라낸 이름  (= 표정 상속)
        for (int i = 1; i < candidates.Count; i++)
        {
            if (!string.IsNullOrEmpty(screen) && rows.TryGetValue(MakeKey(screen, candidates[i]), out found))
            {
                inheritedFrom = candidates[i];
                return true;
            }
            if (rows.TryGetValue(MakeKey("", candidates[i]), out found))
            {
                inheritedFrom = candidates[i];
                return true;
            }
        }

        return false;
    }

    // 미리보기에 올라와 있는 그림들의 좌표/출처 표시를 배치표 기준으로 다시 계산한다.
    private void RefreshItemOrigins()
    {
        foreach (var item in activeItems)
        {
            item.hasScreenRow = !string.IsNullOrEmpty(backgroundName)
                                && rows.ContainsKey(MakeKey(backgroundName, item.fileName));
            item.hasGlobalRow = rows.ContainsKey(MakeKey("", item.fileName));

            if (item.edited) continue;   // 사용자가 옮긴 값은 덮어쓰지 않는다

            if (ResolvePlacement(item.fileName, out Row row, out string from))
            {
                item.x = row.x;
                item.y = row.y;
                item.scale = row.scale;
                item.inheritedFrom = from;
            }
            else
            {
                item.inheritedFrom = null;
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 창 그리기
    // ---------------------------------------------------------------------------------
    private void OnGUI()
    {
        HandleShortcuts();

        DrawToolbar();
        DrawWarnings();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();     // 그림 고르기
        DrawPreview();       // 배경 + 그림 미리보기 (드래그)
        DrawRightPanel();    // 선택한 그림의 좌표 입력
        EditorGUILayout.EndHorizontal();
    }

    // Ctrl+S(맥은 Cmd+S)로 저장. 예전에는 저장 버튼이 회색으로 잠겨 있을 때가 많아
    // "언제 저장할 수 있는지 모르겠다"는 문제가 있었다.
    private void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S && (e.control || e.command))
        {
            SaveLayout();
            e.Use();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("배경 고르기", EditorStyles.toolbarDropDown, GUILayout.Width(80)))
        {
            ShowBackgroundMenu();
        }
        GUILayout.Label(string.IsNullOrEmpty(backgroundName) ? "(배경 없음)" : backgroundName,
                        EditorStyles.toolbarButton, GUILayout.Width(190));

        if (GUILayout.Button("조사 화면 불러오기", EditorStyles.toolbarDropDown, GUILayout.Width(120)))
        {
            ShowInvestigationMenu();
        }

        if (GUILayout.Button("시나리오 장면 불러오기", EditorStyles.toolbarDropDown, GUILayout.Width(140)))
        {
            ShowScenarioMenu();
        }

        GUILayout.Space(8);

        // ===== 저장 범위 =====
        GUILayout.Label("저장 범위", EditorStyles.miniLabel, GUILayout.Width(52));
        bool canUseScreen = !string.IsNullOrEmpty(backgroundName);
        GUI.enabled = canUseScreen;
        int mode = GUILayout.Toolbar(saveToCurrentScreen && canUseScreen ? 1 : 0,
                                     new[] { "공통", "이 화면 전용" },
                                     EditorStyles.toolbarButton, GUILayout.Width(150));
        if (canUseScreen && (mode == 1) != saveToCurrentScreen)
        {
            saveToCurrentScreen = mode == 1;
            RefreshItemOrigins();
        }
        GUI.enabled = true;

        GUILayout.Space(8);
        GUILayout.Label("미리보기 배율", EditorStyles.miniLabel, GUILayout.Width(70));
        previewScale = GUILayout.HorizontalSlider(previewScale, 0.2f, 0.8f, GUILayout.Width(90));

        GUILayout.FlexibleSpace();

        if (dirty) GUILayout.Label("● 저장 안 된 변경 있음", EditorStyles.miniLabel);

        // 저장 버튼은 항상 누를 수 있다. 바꾼 게 없어도 저장하면 그냥 같은 내용이 다시 쓰일 뿐이라
        // 위험하지 않고, "왜 회색이지?"로 헤매는 것보다 훨씬 낫다.
        if (GUILayout.Button("저장 (Ctrl+S)", EditorStyles.toolbarButton, GUILayout.Width(90))) SaveLayout();

        if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(65)))
        {
            if (!dirty || EditorUtility.DisplayDialog("일러스트 배치 도구",
                    "저장하지 않은 변경이 있습니다. 버리고 CSV를 다시 읽을까요?", "다시 읽기", "취소"))
            {
                LoadLayout();
                foreach (var item in activeItems) item.edited = false;
                RefreshItemOrigins();
                dirty = false;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // CSV를 읽다 생긴 문제를 창에 그대로 보여준다(조용히 넘어가지 않는다).
    private void DrawWarnings()
    {
        if (loadWarnings.Count == 0) return;

        EditorGUILayout.HelpBox("CSV를 읽는 중 문제가 있었습니다:\n· " +
                                string.Join("\n· ", loadWarnings), MessageType.Warning);
    }

    // 왼쪽: 어떤 그림을 올릴지 고르는 목록
    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(240));

        // ===== 화면 구성 편집 =====
        // 이 목록이 "게임에 실제로 나오는 그림"이다. 아래 체크박스 목록과 헷갈리지 않도록
        // 맨 위에 따로 두고 이름을 분명히 붙였다.
        // 조사 화면(InvestigationData.csv)과 대화 장면(scenario_*.csv)은 저장하는 파일이
        // 서로 달라서 패널도 따로 둔다. 둘 중 지금 연 쪽만 보인다.
        if (scenarioRow > 0) DrawScenarioPanel();
        else DrawMemberPanel();

        GUILayout.Space(8);
        EditorGUILayout.LabelField("좌표 잡을 그림 고르기", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("(미리보기 전용 · 화면 소속과 무관)", EditorStyles.miniLabel);

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

    // ===== 이 조사 화면에 실제로 존재하는 오브젝트 목록 =====
    // InvestigationData.csv를 그대로 보여주고, 여기서 넣고 뺄 수 있다.
    // 여기서 뺀 것만 게임에서 사라지고, 여기에 넣은 것만 게임에 나타난다.
    private void DrawMemberPanel()
    {
        EditorGUILayout.LabelField("이 조사 화면의 오브젝트", EditorStyles.boldLabel);

        if (string.IsNullOrEmpty(investigationScreenId))
        {
            EditorGUILayout.HelpBox(
                "[조사 화면 불러오기]로 화면을 고르면 여기서 오브젝트를 넣고 뺄 수 있습니다.\n" +
                "게임에 실제로 나오는 오브젝트는 이 목록이 정합니다.",
                MessageType.None);
            return;
        }

        if (investigationTable == null)
        {
            EditorGUILayout.HelpBox("InvestigationData.csv를 읽지 못해 소속을 편집할 수 없습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField(investigationScreenId, EditorStyles.miniLabel);

        var members = MemberRowIndices(investigationScreenId);
        int textCol = ColumnIndex(investigationTable[0], "Text");

        memberScroll = EditorGUILayout.BeginScrollView(memberScroll, GUILayout.Height(150));
        int removeIndex = -1;
        foreach (int rowIndex in members)
        {
            EditorGUILayout.BeginHorizontal();

            string sprite = MemberSprite(rowIndex);
            string label = string.IsNullOrEmpty(sprite) ? MemberKey(rowIndex) : sprite;

            // 장식(Standing/Prop)은 누를 수 없는 그림이라 대사가 없는 게 정상이다.
            // 반대로 조사 오브젝트인데 대사가 비었으면 눌러도 아무 말이 안 나오므로 알려준다.
            string type = Cell(investigationTable[rowIndex], ColumnIndex(investigationTable[0], "Type"));
            bool isDecoration = string.Equals(type, "Standing", System.StringComparison.OrdinalIgnoreCase)
                             || string.Equals(type, "Prop", System.StringComparison.OrdinalIgnoreCase);

            if (isDecoration)
            {
                label += "  (장식)";
            }
            else if (textCol >= 0 && string.IsNullOrWhiteSpace(Cell(investigationTable[rowIndex], textCol)))
            {
                label += "  (대사 없음)";
            }

            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("빼기", EditorStyles.miniButton, GUILayout.Width(38)))
            {
                removeIndex = rowIndex;
            }

            EditorGUILayout.EndHorizontal();
        }
        if (members.Count == 0)
        {
            EditorGUILayout.LabelField("(오브젝트 없음)", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();

        // 목록을 그리는 도중에 지우면 순회가 깨지므로, 다 그린 뒤에 지운다.
        if (removeIndex >= 0)
        {
            RemoveMember(removeIndex);
            ReloadPreviewFromMembers();
        }

        // ===== 오브젝트 추가 =====
        // 예전에는 "미리보기에서 클릭해 고른 그림"을 넣는 방식이었는데, 오브젝트를 전부 빼고 나면
        // 미리보기가 비어서 고를 것이 없어지고 버튼이 계속 회색으로 잠겨 있었다("추가가 안 된다").
        // 그래서 미리보기 상태와 상관없이 여기서 바로 그림을 고르는 목록으로 바꿨다.
        if (GUILayout.Button("+ 오브젝트 추가", EditorStyles.miniButton))
        {
            ShowAddMemberMenu();
        }

        // 이 조사 화면의 배경을 바꾼다 (InvestigationData.csv의 HotspotKey=Background 행).
        if (GUILayout.Button($"배경 바꾸기: {(string.IsNullOrEmpty(backgroundName) ? "(없음)" : backgroundName)}",
                             EditorStyles.miniButton))
        {
            ShowChangeBackgroundMenu(newBg =>
            {
                SetInvestigationBackground(newBg);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BackgroundFolder}/{newBg}.png");
                if (tex != null) SetBackground(newBg, tex);
            });
        }

        EditorGUILayout.BeginHorizontal();
        if (investigationDirty) GUILayout.Label("● 저장 안 됨", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("조사 화면 저장", EditorStyles.miniButton, GUILayout.Width(90)))
        {
            SaveInvestigationTable();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "추가한 오브젝트는 Type=Description, 대사는 비어 있습니다. " +
            "조사했을 때 나올 문구와 아이템 여부는 InvestigationData.csv에서 직접 채워주세요.",
            MessageType.None);
    }

    // 이 화면에 넣을 그림을 고르는 목록을 띄운다.
    // 조사 오브젝트(Objects)와 캐릭터 스탠딩(Standings)을 하위 메뉴로 나눠 보여준다.
    // 스탠딩은 Type=Standing으로 들어가서 "보이기만 하고 누를 수 없는" 장식이 된다.
    private void ShowAddMemberMenu()
    {
        var menu = new GenericMenu();

        // 이미 들어 있는 그림들을 모아둔다(중복 추가를 막기 위해).
        var already = new HashSet<string>();
        foreach (int i in MemberRowIndices(investigationScreenId))
        {
            string s = MemberSprite(i);
            if (!string.IsNullOrEmpty(s)) already.Add(s);
        }

        int count = 0;
        count += AddMenuSection(menu, "조사 오브젝트", ObjectFolder, "Description", already);
        count += AddMenuSection(menu, "캐릭터 스탠딩 (장식·누를 수 없음)", StandingFolder, "Standing", already);

        if (count == 0) menu.AddDisabledItem(new GUIContent("추가할 그림이 없습니다"));
        menu.ShowAsContext();
    }

    // 한 폴더의 그림들을 메뉴 한 묶음으로 넣는다. 넣은 개수를 돌려준다.
    private int AddMenuSection(GenericMenu menu, string sectionLabel, string folder,
                               string hotspotType, HashSet<string> already)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        var names = new List<string>();
        foreach (string guid in guids)
        {
            names.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
        }
        names.Sort(System.StringComparer.Ordinal);

        foreach (string name in names)
        {
            string captured = name;
            string capturedType = hotspotType;
            // GenericMenu는 '/'로 하위 메뉴를 만든다. "조사 오브젝트/OBJ_..." 처럼 묶어서 보여준다.
            var label = new GUIContent($"{sectionLabel}/{name}");

            if (already.Contains(name))
            {
                menu.AddDisabledItem(new GUIContent($"{sectionLabel}/{name}  (이미 있음)"), true);
                continue;
            }

            menu.AddItem(label, false, () =>
            {
                AddMember(investigationScreenId, captured, capturedType);
                ReloadPreviewFromMembers();   // 방금 넣은 것이 미리보기에도 바로 뜨게 한다
            });
        }

        return names.Count;
    }

    // 소속이 바뀌면 미리보기도 그 화면의 오브젝트로 다시 채운다.
    private void ReloadPreviewFromMembers()
    {
        if (investigationTable == null || string.IsNullOrEmpty(investigationScreenId)) return;

        activeItems.Clear();
        selected = null;

        int typeCol = ColumnIndex(investigationTable[0], "Type");

        foreach (int rowIndex in MemberRowIndices(investigationScreenId))
        {
            string sprite = MemberSprite(rowIndex);
            if (string.IsNullOrEmpty(sprite)) continue;

            // Type=Standing이면 그림이 Standings 폴더에 있다. 나머지는 Objects 폴더.
            string type = Cell(investigationTable[rowIndex], typeCol);
            string folder = string.Equals(type, "Standing", System.StringComparison.OrdinalIgnoreCase)
                ? StandingFolder : ObjectFolder;

            AddToPreview(sprite, $"{folder}/{sprite}.png");
        }
        Repaint();
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
            "그림을 클릭해 선택한 뒤 끌어서 위치를 맞추세요. 방향키로 1픽셀씩, Shift+방향키로 10픽셀씩 움직입니다. " +
            "저장은 Ctrl+S.",
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

            // ===== 왜 반올림하나? =====
            // 미리보기는 1440x1080을 0.45배 같은 배율로 줄여 보여주므로, 마우스 좌표를
            // 그 배율로 되돌리면 -448.89 처럼 소수가 나온다. 소수 자리에 놓인 그림은
            // 게임 화면에서 픽셀 사이에 걸쳐 그려져 선이 번지고 흐릿해 보인다(IllustLayout.cs 참고).
            // 그래서 항상 정수 픽셀로 맞춰 저장한다.
            selected.x = Mathf.Round((center.x - (area.x + area.width * 0.5f)) / previewScale);
            selected.y = Mathf.Round(((area.y + area.height * 0.5f) - center.y) / previewScale);
            MarkEdited(selected);
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
                MarkEdited(selected);
                e.Use();
                Repaint();
            }
        }
    }

    // 사용자가 이 그림을 실제로 옮겼다고 표시한다. 저장 대상이 되는 기준이다.
    private void MarkEdited(Item item)
    {
        item.edited = true;
        item.inheritedFrom = null;   // 이제 스스로 좌표를 갖게 되므로 "물려받음" 표시를 뗀다
        dirty = true;
    }

    // 오른쪽: 선택한 그림의 좌표를 숫자로 조정
    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        EditorGUILayout.LabelField("미리보기에 올린 그림", EditorStyles.boldLabel);

        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(200));
        foreach (var item in activeItems)
        {
            EditorGUILayout.BeginHorizontal();

            bool isSel = item == selected;
            if (GUILayout.Toggle(isSel, item.fileName, EditorStyles.miniButton) != isSel)
            {
                selected = item;
            }

            // 이 그림이 지금 어디서 좌표를 얻고 있는지 한눈에 보이게 한다.
            string tag = item.edited ? "수정" :
                         item.hasScreenRow ? "전용" :
                         item.inheritedFrom != null ? "상속" :
                         item.hasGlobalRow ? "공통" : "없음";
            GUILayout.Label(tag, EditorStyles.miniLabel, GUILayout.Width(28));

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

            // 표정 상속 안내: 이 그림이 다른 줄의 좌표를 물려받고 있으면 알려준다.
            if (!selected.edited && selected.inheritedFrom != null)
            {
                EditorGUILayout.HelpBox(
                    $"'{selected.inheritedFrom}' 의 좌표를 물려받고 있습니다.\n" +
                    "이 그림만 따로 놓고 싶을 때만 움직여서 저장하세요.",
                    MessageType.None);
            }

            // 좌표는 정수만 받는다(소수 자리에 놓으면 그림이 흐려진다 - IllustLayout.cs 참고).
            EditorGUI.BeginChangeCheck();
            selected.x = EditorGUILayout.IntField("X (오른쪽 +)", Mathf.RoundToInt(selected.x));
            selected.y = EditorGUILayout.IntField("Y (위쪽 +)", Mathf.RoundToInt(selected.y));
            selected.scale = EditorGUILayout.Slider("크기 배율", selected.scale, 0.2f, 3f);
            if (EditorGUI.EndChangeCheck()) MarkEdited(selected);

            GUILayout.Space(6);
            if (GUILayout.Button("가운데로 되돌리기"))
            {
                selected.x = 0f;
                selected.y = 0f;
                selected.scale = 1f;
                MarkEdited(selected);
            }

            // 이 화면 전용으로 만들어둔 줄을 지우고 공통값으로 되돌린다.
            if (selected.hasScreenRow && !string.IsNullOrEmpty(backgroundName))
            {
                if (GUILayout.Button($"'{backgroundName}' 전용 좌표 삭제"))
                {
                    rows.Remove(MakeKey(backgroundName, selected.fileName));
                    selected.edited = false;
                    RefreshItemOrigins();
                    dirty = true;
                }
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.HelpBox(
            saveToCurrentScreen
                ? $"[이 화면 전용] 으로 저장합니다. 움직인 그림만 '{backgroundName}' 전용 줄로 기록됩니다."
                : "[공통] 으로 저장합니다. 모든 화면에서 쓰는 기본 좌표가 됩니다.",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    // ---------------------------------------------------------------------------------
    // 목록 조작
    // ---------------------------------------------------------------------------------
    private void AddToPreview(string name, string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            loadWarnings.Add($"'{name}' 그림 파일을 찾지 못했습니다: {path}");
            return;
        }

        var item = new Item { fileName = name, texture = texture };

        // 배치표에서 지금 이 화면 기준으로 실제 좌표를 계산해 초기값으로 넣는다.
        if (ResolvePlacement(name, out Row row, out string from))
        {
            item.x = row.x;
            item.y = row.y;
            item.scale = row.scale;
            item.inheritedFrom = from;
        }
        item.hasScreenRow = !string.IsNullOrEmpty(backgroundName) && rows.ContainsKey(MakeKey(backgroundName, name));
        item.hasGlobalRow = rows.ContainsKey(MakeKey("", name));

        activeItems.Add(item);
        selected = item;
    }

    private void RemoveFromPreview(string name)
    {
        int index = activeItems.FindIndex(i => i.fileName == name);
        if (index < 0) return;

        if (selected == activeItems[index]) selected = null;
        activeItems.RemoveAt(index);
    }

    // 배경을 바꾸면 "이 화면 전용" 좌표가 달라지므로 전부 다시 계산한다.
    private void SetBackground(string name, Texture2D texture)
    {
        backgroundName = name;
        backgroundTexture = texture;
        RefreshItemOrigins();
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
                SetBackground(name, AssetDatabase.LoadAssetAtPath<Texture2D>(path));

                // 배경만 바꾼 것은 "조사 화면을 연 것"이 아니다. 소속 편집 패널을 켜둔 채로 두면
                // 엉뚱한 화면의 소속을 고치게 되므로, 저장이 끝난 상태일 때만 편집을 닫는다.
                if (!investigationDirty) investigationScreenId = null;

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
        loadWarnings.Clear();

        if (!File.Exists(InvestigationCsvPath))
        {
            menu.AddDisabledItem(new GUIContent("InvestigationData.csv가 없습니다"));
            menu.ShowAsContext();
            return;
        }

        var table = ParseCsv(File.ReadAllText(InvestigationCsvPath, Encoding.UTF8));
        if (table.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("CSV가 비어 있습니다"));
            menu.ShowAsContext();
            return;
        }

        string[] header = table[0];
        int idCol = ColumnIndex(header, "InvestigationId");
        int keyCol = ColumnIndex(header, "HotspotKey");
        int spriteCol = ColumnIndex(header, "Sprite");

        if (idCol < 0 || keyCol < 0 || spriteCol < 0)
        {
            var missing = new List<string>();
            if (idCol < 0) missing.Add("InvestigationId");
            if (keyCol < 0) missing.Add("HotspotKey");
            if (spriteCol < 0) missing.Add("Sprite");
            loadWarnings.Add($"InvestigationData.csv에 컬럼이 없습니다: {string.Join(", ", missing)}");

            menu.AddDisabledItem(new GUIContent("CSV에 필요한 컬럼이 없습니다 (창의 경고 참고)"));
            menu.ShowAsContext();
            return;
        }

        // 조사 화면 id -> (배경 이름, 오브젝트 그림 이름들)
        var screens = new Dictionary<string, (string bg, List<string> sprites)>();

        for (int i = 1; i < table.Count; i++)
        {
            string id = Cell(table[i], idCol);
            string key = Cell(table[i], keyCol);
            string sprite = Cell(table[i], spriteCol);
            if (string.IsNullOrEmpty(id)) continue;

            if (!screens.ContainsKey(id)) screens[id] = ("", new List<string>());

            if (string.Equals(key, "Background", System.StringComparison.OrdinalIgnoreCase))
            {
                screens[id] = (sprite, screens[id].sprites);
            }
            else if (string.Equals(key, "IntroText", System.StringComparison.OrdinalIgnoreCase)
                  || string.Equals(key, "NextScreen", System.StringComparison.OrdinalIgnoreCase)
                  || string.Equals(key, "PrevScreen", System.StringComparison.OrdinalIgnoreCase))
            {
                // 안내문/화살표는 그림이 아니다. Sprite 칸에 값이 들어 있어도 오브젝트 수에
                // 세면 안 된다 (메뉴에 "(N개)"로 표시되는 그 숫자).
            }
            else if (!string.IsNullOrEmpty(sprite))
            {
                screens[id].sprites.Add(sprite);
            }
        }

        // ===== 게임에서 실제로 들어가는 조사 화면만 목록에 올린다 =====
        // InvestigationData.csv에는 배경 34개가 전부 조사 화면으로 등록되어 있지만, 게임이 실제로
        // 조사 화면으로 여는 것은 시나리오 CSV에 LineType=Investigate 줄이 있는 화면뿐이다.
        // 나머지 배경은 대사 장면으로만 나오고, 그때는 시나리오 CSV의 Standing/Props 칸만 쓴다.
        //
        // 예전에는 34개가 전부 목록에 떠서, 한 번도 열리지 않는 조사 화면에 인물/오브젝트를
        // 올려놓고 "분명 추가했는데 게임엔 배경만 나온다"는 일이 생겼다. 같은 배경이 도구 안에
        // 두 벌 있었던 셈이다. 그래서 여기서는 실제로 열리는 조사 화면만 보여주고, 그 외 배경은
        // [시나리오 장면 불러오기]에서만 편집하게 한다. (InvestigationData.csv 자체는 그대로 둔다)
        var enteredScreens = new HashSet<string>();
        foreach (string scenarioFile in Directory.GetFiles(DialogueFolder, "scenario_*.csv"))
        {
            if (Path.GetFileName(scenarioFile).StartsWith("~$")) continue;
            var scenario = ParseCsv(File.ReadAllText(scenarioFile, Encoding.UTF8));
            if (scenario.Count == 0) continue;
            int typeCol = ColumnIndex(scenario[0], "LineType");
            int invCol = ColumnIndex(scenario[0], "InvestigationId");
            for (int i = 1; i < scenario.Count; i++)
            {
                if (!string.Equals(Cell(scenario[i], typeCol), "Investigate", System.StringComparison.OrdinalIgnoreCase)) continue;
                string invId = Cell(scenario[i], invCol);
                if (!string.IsNullOrEmpty(invId)) enteredScreens.Add(invId);
            }
        }

        // ===== 화살표로만 닿는 옆 화면도 목록에 남긴다 =====
        // NextScreen/PrevScreen 기능이 생기면서(InvestigationController.cs 참고), 시나리오 CSV에
        // Investigate 줄이 없어도 조사 도중 화살표로 건너갈 수 있는 화면이 생겼다.
        // 예) #07 회사 조사: 재훈의 책상(Site_01)으로만 들어가고, 회의실(Site_02)은 화살표로 간다.
        // 이런 화면을 목록에서 빼버리면 "게임엔 나오는데 도구에서는 배치할 수 없는 화면"이 되므로,
        // 들어가는 화면에서 연결을 따라가며 닿는 화면을 전부 더한다(연결이 여러 단계여도 되게 반복).
        int textCol = ColumnIndex(header, "Text");
        bool linkAdded = true;
        while (linkAdded)
        {
            linkAdded = false;
            for (int i = 1; i < table.Count; i++)
            {
                string linkKey = Cell(table[i], keyCol);
                if (!string.Equals(linkKey, "NextScreen", System.StringComparison.OrdinalIgnoreCase)
                 && !string.Equals(linkKey, "PrevScreen", System.StringComparison.OrdinalIgnoreCase)) continue;

                string from = Cell(table[i], idCol);
                // 이동할 화면 이름은 Sprite 칸에 적는다. 비어 있으면 Text 칸도 본다
                // (InvestigationController가 읽는 방식과 똑같이 맞춘 것).
                string to = Cell(table[i], spriteCol);
                if (string.IsNullOrEmpty(to)) to = Cell(table[i], textCol);
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;

                if (enteredScreens.Contains(from) && !enteredScreens.Contains(to))
                {
                    enteredScreens.Add(to);
                    linkAdded = true;
                }
            }
        }

        foreach (var pair in screens)
        {
            string id = pair.Key;
            var data = pair.Value;

            // 게임에서 한 번도 열리지 않는 조사 화면은 건너뛴다 (위 주석 참고).
            if (!enteredScreens.Contains(id)) continue;

            menu.AddItem(new GUIContent($"{id}  ({data.sprites.Count}개)"), false, () =>
            {
                // 소속을 바꾸다 만 것이 있으면 화면을 옮기기 전에 물어본다
                // (안 물어보면 다른 화면을 여는 순간 조용히 사라진다).
                if (investigationDirty && !EditorUtility.DisplayDialog("일러스트 배치 도구",
                        "저장하지 않은 조사 화면 소속 변경이 있습니다. 버리고 다른 화면을 열까요?",
                        "버리고 열기", "취소"))
                {
                    return;
                }
                if (!ConfirmDiscardScenarioEdits()) return;

                // 시나리오 편집과 조사 화면 편집은 동시에 열어두지 않는다.
                scenarioRow = -1;
                scenarioDirty = false;

                // 배경: Background 행이 있으면 그것을, 없으면 조사 화면 이름이 곧 배경 이름이다.
                string bgName = string.IsNullOrEmpty(data.bg) ? id : data.bg;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BackgroundFolder}/{bgName}.png");
                if (tex != null) SetBackground(bgName, tex);
                else loadWarnings.Add($"배경 그림을 찾지 못했습니다: {bgName}.png");

                // 소속 편집용으로 CSV 전체를 다시 읽어둔다. 미리보기도 이 목록에서 채우므로
                // "미리보기에 보이는 것 = 그 화면에 실제로 있는 것"이 항상 일치하게 된다.
                investigationScreenId = id;
                LoadInvestigationTable();
                ReloadPreviewFromMembers();
            });
        }

        if (screens.Count == 0) menu.AddDisabledItem(new GUIContent("조사 화면이 없습니다"));
        menu.ShowAsContext();
    }

    // ---------------------------------------------------------------------------------
    // 시나리오 장면 불러오기
    // ---------------------------------------------------------------------------------
    // ===== 이게 왜 필요한가? =====
    // 조사 화면은 InvestigationData.csv가 "배경 + 오브젝트"를 정해주지만, 대화 장면은
    // scenario_*.csv의 Background / Standing 칸이 "그 줄에서 어떤 배경에 누가 서 있는지"를
    // 정한다. 그래서 대화 장면의 스탠딩 위치를 잡으려면 그 줄의 상태를 알아야 하는데,
    // 예전에는 CSV를 사람이 직접 읽어서 스탠딩 이름을 하나하나 찾아 올려야 했다.
    //
    // 여기서는 시나리오 CSV를 위에서부터 훑으면서 "배경이 바뀌는 줄"을 목록으로 만들고,
    // 하나를 고르면 그 시점의 배경과 그때 서 있던 스탠딩을 그대로 미리보기에 재현한다.
    // (배경/스탠딩 칸은 비어 있으면 "이전 줄 상태 유지"라는 뜻이므로, 위에서부터 누적해야
    //  그 줄의 실제 화면을 알 수 있다 - DialogueLine.cs 주석 참고)
    private void ShowScenarioMenu()
    {
        var menu = new GenericMenu();
        loadWarnings.Clear();

        var files = new List<string>(Directory.GetFiles(DialogueFolder, "scenario_*.csv"));
        files.Sort(System.StringComparer.Ordinal);

        int added = 0;
        foreach (string file in files)
        {
            // 엑셀이 파일을 열어둘 때 만드는 임시 잠금 파일(~$로 시작)은 건너뛴다.
            string baseName = Path.GetFileNameWithoutExtension(file);
            if (baseName.StartsWith("~$")) continue;

            var table = ParseCsv(File.ReadAllText(file, Encoding.UTF8));
            if (table.Count < 2) continue;

            string[] header = table[0];
            int bgCol = ColumnIndex(header, "Background");
            int standCol = ColumnIndex(header, "Standing");
            int speakerCol = ColumnIndex(header, "Speaker");
            int sentenceCol = ColumnIndex(header, "Sentence");

            // ===== CSV마다 "장면 시작" 항목을 하나씩 무조건 넣는다 =====
            // 예전에는 Background 칸에 값이 적힌 줄만 목록에 올렸다. 그래서 배경을 한 번도
            // 지정하지 않는 CSV(엔딩 6개가 그렇다)는 메뉴에 아예 나오지 않았고, 게임에는
            // 나오는 장면인데 배치 도구로는 열 수가 없었다.
            // 첫 줄을 항상 넣어주면 어떤 CSV든 반드시 열 수 있고, 열고 나서 [배경 바꾸기]로
            // 그 줄에 배경을 지정하면 그때부터 정상적으로 관리된다.
            {
                string firstLabel = $"{baseName}/― 장면 시작 (2행부터) ―";
                string capturedFileFirst = file;
                menu.AddItem(new GUIContent(firstLabel), false,
                             () => LoadScenarioScene(capturedFileFirst, 1, ""));
                added++;
            }

            // Background 칸이 아예 없는 CSV라면 줄별 항목은 만들 수 없다.
            // 그래도 위의 "장면 시작" 항목으로 열 수는 있으므로 여기서만 건너뛴다.
            if (bgCol < 0) continue;

            // 위에서부터 누적하며 "그 줄의 화면 상태"를 만든다.
            string runningBg = "";
            string runningStanding = "";

            // ===== 게임이 화면 칸을 읽지 않는 줄은 목록에서 뺀다 =====
            // 선택지(Choice)/미니게임(Minigame)/조사(Investigate)/추리(Deduction) 줄은 대사를
            // 보여주는 줄이 아니라서, 거기에 Background/Standing/Props를 적어도 게임은 읽지 않는다
            // (DialogueSystem의 CSV 읽는 부분 참고). 그런데 예전에는 이런 줄도 목록에 올라와서
            // "도구에는 있는데 게임에는 절대 안 나오는 장면"이 생겼다. 그래서 여기서 제외한다.
            int typeCol = ColumnIndex(header, "LineType");

            for (int i = 1; i < table.Count; i++)
            {
                string lineType = Cell(table[i], typeCol).ToLowerInvariant();
                if (lineType == "choice" || lineType == "minigame" ||
                    lineType == "investigate" || lineType == "deduction") continue;

                string bg = Cell(table[i], bgCol);
                string standing = standCol >= 0 ? Cell(table[i], standCol) : "";

                if (!string.IsNullOrEmpty(bg)) runningBg = bg;
                if (!string.IsNullOrEmpty(standing)) runningStanding = standing;

                // 배경이 실제로 바뀐 줄만 목록에 올린다(전부 올리면 수백 개가 되어 못 고른다).
                if (string.IsNullOrEmpty(bg)) continue;
                if (string.Equals(bg, "none", System.StringComparison.OrdinalIgnoreCase)) continue;

                string speaker = speakerCol >= 0 ? Cell(table[i], speakerCol) : "";
                string sentence = sentenceCol >= 0 ? Cell(table[i], sentenceCol) : "";
                if (sentence.Length > 18) sentence = sentence.Substring(0, 18) + "…";

                // GenericMenu는 '/'를 하위 메뉴 구분자로 쓴다. 파일 이름 뒤의 '/' 하나만
                // 구분자로 남기고, 대사 안에 들어 있는 '/'는 전각 문자로 바꿔 메뉴가
                // 엉뚱하게 여러 단으로 갈라지지 않게 한다.
                string tail = $"{i + 1}행  {bg}  {speaker} {sentence}".Replace("/", "／");
                string label = $"{baseName}/{tail}";

                string capturedBg = runningBg;
                string capturedFile = file;
                int capturedRow = i;

                menu.AddItem(new GUIContent(label), false, () => LoadScenarioScene(capturedFile, capturedRow, capturedBg));
                added++;
            }
        }

        if (added == 0) menu.AddDisabledItem(new GUIContent("배경이 바뀌는 줄을 찾지 못했습니다"));
        menu.ShowAsContext();
    }

    // ---------------------------------------------------------------------------------
    // 시나리오 장면 편집 (scenario_*.csv 의 Standing / Props 칸)
    // ---------------------------------------------------------------------------------
    // ===== 조사 화면과 무엇이 다른가? =====
    // 조사 화면은 InvestigationData.csv의 "행 하나 = 오브젝트 하나"라서 행을 넣고 뺐다.
    // 반면 대화 장면은 scenario_*.csv의 **한 줄 안에** 세로줄(|)로 이어 붙인 목록이다.
    //   Standing = STD_A|STD_B     (그 줄에서 서 있을 사람들)
    //   Props    = OBJ_A|OBJ_B     (그 줄에서 배경 위에 얹을 소품들)
    // 그래서 여기서는 "그 줄의 그 칸 문자열"을 고쳐 쓴다.
    //
    // 둘 다 상호작용은 없다. 대화 장면에서는 조사를 하지 않으므로 스탠딩도 소품도
    // 그냥 그림일 뿐이고 클릭은 전부 통과한다 (StageController.ApplyProps 주석 참고).

    private List<string[]> scenarioTable;   // 지금 편집 중인 시나리오 CSV 전체
    private string scenarioPath;            // 그 CSV의 파일 경로
    private int scenarioRow = -1;           // 편집 중인 줄 번호 (표에서의 인덱스)
    private bool scenarioDirty;
    private Vector2 scenarioScroll;

    // 시나리오의 한 줄이 만들어내는 화면(배경 + 스탠딩 + 소품)을 미리보기에 재현한다.
    private void LoadScenarioScene(string filePath, int rowIndex, string bgName)
    {
        if (!ConfirmDiscardScenarioEdits()) return;

        scenarioTable = ParseCsv(File.ReadAllText(filePath, Encoding.UTF8));
        scenarioPath = filePath;
        scenarioRow = rowIndex;
        scenarioDirty = false;

        // 조사 화면 편집과 시나리오 편집은 동시에 열어두지 않는다(어느 쪽을 고치는지 헷갈린다).
        investigationScreenId = null;

        // 아직 배경이 정해지지 않은 줄("장면 시작" 항목 등)이면 배경 없이 연다.
        // 이 상태에서 [배경 바꾸기]로 배경을 지정하면 그때부터 정상적으로 관리된다.
        if (string.IsNullOrEmpty(bgName))
        {
            SetBackground("", null);
        }
        else
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BackgroundFolder}/{bgName}.png");
            if (tex != null) SetBackground(bgName, tex);
            else loadWarnings.Add($"배경 그림을 찾지 못했습니다: {bgName}.png");
        }

        ReloadPreviewFromScenario();
    }

    // 지금 편집 중인 줄의 Standing/Props 칸을 읽어 미리보기를 채운다.
    private void ReloadPreviewFromScenario()
    {
        activeItems.Clear();
        selected = null;

        foreach (string name in ScenarioList("Standing")) AddToPreview(name, $"{StandingFolder}/{name}.png");
        foreach (string name in ScenarioList("Props")) AddToPreview(name, $"{ObjectFolder}/{name}.png");

        Repaint();
    }

    // 편집 중인 줄의 한 칸("Standing" 또는 "Props")을 세로줄로 쪼개 목록으로 돌려준다.
    private List<string> ScenarioList(string column)
    {
        var result = new List<string>();
        if (scenarioTable == null || scenarioRow <= 0 || scenarioRow >= scenarioTable.Count) return result;

        int col = ColumnIndex(scenarioTable[0], column);
        if (col < 0) return result;

        string spec = Cell(scenarioTable[scenarioRow], col);
        if (string.IsNullOrWhiteSpace(spec)) return result;
        if (string.Equals(spec, "none", System.StringComparison.OrdinalIgnoreCase)) return result;

        foreach (string raw in spec.Split('|'))
        {
            string name = raw.Trim();
            if (!string.IsNullOrEmpty(name)) result.Add(name);
        }
        return result;
    }

    // 목록을 다시 세로줄로 이어 붙여 그 칸에 써 넣는다.
    // 칸이 없는 CSV(예전 파일)라면 헤더째 새 칸을 만들어준다.
    private void SetScenarioList(string column, List<string> names)
    {
        if (scenarioTable == null || scenarioRow <= 0) return;

        int col = ColumnIndex(scenarioTable[0], column);
        if (col < 0)
        {
            // 이 CSV에는 아직 그 칸이 없다. 모든 줄의 맨 끝에 칸을 하나씩 늘리고
            // 헤더에 이름을 적어준다(다른 칸은 건드리지 않으므로 기존 데이터는 그대로다).
            col = scenarioTable[0].Length;
            for (int i = 0; i < scenarioTable.Count; i++)
            {
                var grown = new string[col + 1];
                System.Array.Copy(scenarioTable[i], grown, scenarioTable[i].Length);
                for (int j = scenarioTable[i].Length; j <= col; j++) grown[j] = "";
                scenarioTable[i] = grown;
            }
            scenarioTable[0][col] = column;
        }

        // 편집 중인 줄이 헤더보다 짧으면 칸을 늘려준다(엑셀이 뒤쪽 빈 칸을 잘라낸 경우).
        if (scenarioTable[scenarioRow].Length <= col)
        {
            var grown = new string[col + 1];
            System.Array.Copy(scenarioTable[scenarioRow], grown, scenarioTable[scenarioRow].Length);
            for (int j = scenarioTable[scenarioRow].Length; j <= col; j++) grown[j] = "";
            scenarioTable[scenarioRow] = grown;
        }

        // 목록이 비면 "none"을 적는다. 빈칸은 "이전 줄 그대로 유지"라는 뜻이라
        // 전부 뺐다는 의도가 전달되지 않기 때문이다(DialogueLine.cs 주석 참고).
        scenarioTable[scenarioRow][col] = names.Count > 0 ? string.Join("|", names) : "none";
        scenarioDirty = true;
    }

    // ===== 장면 첫 줄을 저장할 때 그 장면의 나머지 줄을 맞춘다 =====
    // 도구의 장면 하나 = 배경 칸이 적힌 줄 하나다. 그 장면의 배경/인물 목록/소품은 이 줄이 정하고,
    // 다음 장면이 시작되기 전까지의 줄들은 "캐릭터 스탠딩 표정"만 바꿀 수 있어야 한다.
    //
    // 그런데 장면 안의 줄들은 표정을 바꾸려고 Standing 칸에 인물 이름을 적어두기 때문에,
    // 도구에서 장면의 인물을 빼거나 바꿔도 그 줄들이 옛 인물을 다시 불러와 버렸다.
    // (예: 95행 장면에서 아이를 뺐는데 105행/110행이 아이 표정을 바꾸면서 아이가 다시 나타남)
    //
    // 그래서 저장할 때 같은 장면 안의 줄들을 훑어서
    //   - Standing 칸: 장면 첫 줄의 인물 목록은 그대로 두고, 그 줄이 적은 "표정"만 반영한다.
    //                  장면에 없는 인물은 버린다. 장면에 인물이 없으면 칸을 비운다.
    //   - Props 칸   : 비운다 (소품은 장면 첫 줄이 정한다)
    // 로 맞춘다. 표정을 바꾸는 줄 자체는 그대로 살아 있으므로 대사 연출은 유지된다.
    // 반환값 = 고친 칸 수.
    private int NormalizeScenarioSegment(int entryRow)
    {
        if (scenarioTable == null || entryRow <= 0 || entryRow >= scenarioTable.Count) return 0;

        string[] header = scenarioTable[0];
        int typeCol = ColumnIndex(header, "LineType");
        int bgCol = ColumnIndex(header, "Background");
        int stCol = ColumnIndex(header, "Standing");
        int prCol = ColumnIndex(header, "Props");
        if (bgCol < 0 || stCol < 0) return 0;

        // 배경이 적힌 줄만 "장면 첫 줄"이다 (CSV 맨 앞의 "장면 시작" 항목처럼 배경이 비어 있으면 건너뜀)
        if (Cell(scenarioTable[entryRow], bgCol) == "") return 0;

        // 장면 첫 줄의 인물 목록(지금 표정 포함)과 자리
        var current = new List<string>();
        foreach (string raw in Cell(scenarioTable[entryRow], stCol).Split('|'))
        {
            string n = raw.Trim();
            if (n != "" && !n.Equals("none", System.StringComparison.OrdinalIgnoreCase)) current.Add(n);
        }

        int changed = 0;
        for (int i = entryRow + 1; i < scenarioTable.Count; i++)
        {
            string lineType = Cell(scenarioTable[i], typeCol).ToLowerInvariant();
            if (lineType == "investigate") break;                 // 조사 화면으로 넘어가면 이 장면은 끝
            if (lineType == "choice" || lineType == "minigame" || lineType == "deduction") continue;
            if (Cell(scenarioTable[i], bgCol) != "") break;        // 다음 장면이 시작됨

            if (prCol >= 0 && Cell(scenarioTable[i], prCol) != "")
            {
                SetScenarioCellAt(i, prCol, "");
                changed++;
            }

            string st = Cell(scenarioTable[i], stCol);
            if (st == "") continue;

            // 이 줄이 적은 이름을 캐릭터(기본 이름)별로 모은 뒤, 장면 인물의 표정만 바꾼다.
            var byBase = new Dictionary<string, string>();
            foreach (string raw in st.Split('|'))
            {
                string n = raw.Trim();
                if (n == "" || n.Equals("none", System.StringComparison.OrdinalIgnoreCase)) continue;
                string b = StandingBaseName(n);
                if (!byBase.ContainsKey(b)) byBase[b] = n;
            }
            for (int k = 0; k < current.Count; k++)
            {
                if (byBase.TryGetValue(StandingBaseName(current[k]), out string expression)) current[k] = expression;
            }

            string newSt = current.Count > 0 ? string.Join("|", current) : "";
            if (newSt != st)
            {
                SetScenarioCellAt(i, stCol, newSt);
                changed++;
            }
        }

        if (changed > 0) scenarioDirty = true;
        return changed;
    }

    // STD_Past01_Hansung_Angry -> STD_Past01_Hansung (표정 상속 규칙과 같다)
    private static string StandingBaseName(string fileName)
    {
        var candidates = IllustLayout.NameCandidates(fileName);
        return candidates.Count > 0 ? candidates[candidates.Count - 1] : fileName;
    }

    // 시나리오 표의 한 칸에 값을 넣는다. 줄이 짧으면(엑셀이 뒤쪽 빈 칸을 잘라낸 경우) 늘린다.
    private void SetScenarioCellAt(int rowIndex, int col, string value)
    {
        if (scenarioTable[rowIndex].Length <= col)
        {
            var grown = new string[col + 1];
            System.Array.Copy(scenarioTable[rowIndex], grown, scenarioTable[rowIndex].Length);
            for (int j = scenarioTable[rowIndex].Length; j <= col; j++) grown[j] = "";
            scenarioTable[rowIndex] = grown;
        }
        scenarioTable[rowIndex][col] = value;
    }

    private void SaveScenarioTable()
    {
        if (scenarioTable == null || string.IsNullOrEmpty(scenarioPath)) return;

        // 저장 전에 이 장면의 나머지 줄을 장면 첫 줄에 맞춘다 (위 NormalizeScenarioSegment 주석 참고).
        int synced = NormalizeScenarioSegment(scenarioRow);
        if (synced > 0)
        {
            Debug.Log($"[일러스트 배치 도구] 같은 장면 안의 줄 {synced}칸을 장면 첫 줄에 맞췄습니다 (표정만 남김).");
        }

        var sb = new StringBuilder();
        foreach (var row in scenarioTable)
        {
            for (int i = 0; i < row.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(row[i]));
            }
            sb.Append('\n');
        }

        File.WriteAllText(scenarioPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        scenarioDirty = false;
        Debug.Log($"[일러스트 배치 도구] 시나리오 장면을 저장했습니다. -> {scenarioPath} ({scenarioRow + 1}행)");
    }

    private bool ConfirmDiscardScenarioEdits()
    {
        if (!scenarioDirty) return true;
        return EditorUtility.DisplayDialog("일러스트 배치 도구",
            "저장하지 않은 시나리오 장면 변경이 있습니다. 버리고 진행할까요?", "버리고 진행", "취소");
    }

    // ===== 시나리오 장면 편집 패널 =====
    private void DrawScenarioPanel()
    {
        EditorGUILayout.LabelField("이 시나리오 장면의 그림", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{Path.GetFileNameWithoutExtension(scenarioPath)}  {scenarioRow + 1}행",
                                   EditorStyles.miniLabel);

        scenarioScroll = EditorGUILayout.BeginScrollView(scenarioScroll, GUILayout.Height(150));

        DrawScenarioSection("스탠딩", "Standing");
        DrawScenarioSection("소품", "Props");

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ 스탠딩 / 소품 추가", EditorStyles.miniButton))
        {
            ShowAddScenarioMenu();
        }

        // 이 줄의 배경을 다른 그림으로 바꾼다 (scenario_*.csv의 Background 칸).
        if (GUILayout.Button($"배경 바꾸기: {(string.IsNullOrEmpty(backgroundName) ? "(없음)" : backgroundName)}",
                             EditorStyles.miniButton))
        {
            ShowChangeBackgroundMenu(newBg =>
            {
                SetScenarioCell("Background", newBg);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BackgroundFolder}/{newBg}.png");
                if (tex != null) SetBackground(newBg, tex);
            });
        }

        EditorGUILayout.BeginHorizontal();
        if (scenarioDirty) GUILayout.Label("● 저장 안 됨", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("장면 저장", EditorStyles.miniButton, GUILayout.Width(90)))
        {
            SaveScenarioTable();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "이 줄의 Standing / Props 칸을 고칩니다. 둘 다 상호작용은 없습니다(그림만).",
            MessageType.None);
    }

    private void DrawScenarioSection(string label, string column)
    {
        var names = ScenarioList(column);

        EditorGUILayout.LabelField($"{label} ({names.Count})", EditorStyles.miniBoldLabel);
        if (names.Count == 0)
        {
            EditorGUILayout.LabelField("   (없음)", EditorStyles.miniLabel);
            return;
        }

        int removeAt = -1;
        for (int i = 0; i < names.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("   " + names[i], EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("빼기", EditorStyles.miniButton, GUILayout.Width(38))) removeAt = i;
            EditorGUILayout.EndHorizontal();
        }

        // 목록을 그리는 도중에 지우면 순회가 깨지므로 다 그린 뒤에 지운다.
        if (removeAt >= 0)
        {
            names.RemoveAt(removeAt);
            SetScenarioList(column, names);
            ReloadPreviewFromScenario();
        }
    }

    // 편집 중인 시나리오 줄의 한 칸에 값을 써 넣는다(Background 등 목록이 아닌 칸용).
    private void SetScenarioCell(string column, string value)
    {
        if (scenarioTable == null || scenarioRow <= 0) return;

        int col = ColumnIndex(scenarioTable[0], column);
        if (col < 0)
        {
            loadWarnings.Add($"이 CSV에는 '{column}' 칸이 없습니다.");
            return;
        }

        // 엑셀이 뒤쪽 빈 칸을 잘라낸 줄이면 칸을 늘려준다.
        if (scenarioTable[scenarioRow].Length <= col)
        {
            var grown = new string[col + 1];
            System.Array.Copy(scenarioTable[scenarioRow], grown, scenarioTable[scenarioRow].Length);
            for (int j = scenarioTable[scenarioRow].Length; j <= col; j++) grown[j] = "";
            scenarioTable[scenarioRow] = grown;
        }

        scenarioTable[scenarioRow][col] = value;
        scenarioDirty = true;
    }

    // ===== 배경 고르기 목록 =====
    // 배경(BG_*)은 전부 1440x1080 전체 화면 그림이라 위치나 크기를 잡을 것이 없다.
    // 그래서 배치 도구에서 배경에 대해 할 수 있는 일은 "어느 그림을 쓸지 바꾸는 것"뿐이며,
    // 이 메뉴가 그 역할을 한다. 고르면 CSV의 Background 칸이 바뀐다.
    private void ShowChangeBackgroundMenu(System.Action<string> onPick)
    {
        var menu = new GenericMenu();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { BackgroundFolder });

        var names = new List<string>();
        foreach (string guid in guids)
        {
            names.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
        }
        names.Sort(System.StringComparer.Ordinal);

        foreach (string name in names)
        {
            string captured = name;
            menu.AddItem(new GUIContent(name), backgroundName == name, () => onPick(captured));
        }

        if (names.Count == 0) menu.AddDisabledItem(new GUIContent("배경 그림이 없습니다"));
        menu.ShowAsContext();
    }

    // 이 시나리오 줄에 넣을 스탠딩/소품을 고르는 목록.
    private void ShowAddScenarioMenu()
    {
        var menu = new GenericMenu();

        var already = new HashSet<string>();
        foreach (string n in ScenarioList("Standing")) already.Add(n);
        foreach (string n in ScenarioList("Props")) already.Add(n);

        int count = 0;
        count += AddScenarioMenuSection(menu, "캐릭터 스탠딩", StandingFolder, "Standing", already);
        count += AddScenarioMenuSection(menu, "소품 (오브젝트)", ObjectFolder, "Props", already);

        if (count == 0) menu.AddDisabledItem(new GUIContent("추가할 그림이 없습니다"));
        menu.ShowAsContext();
    }

    private int AddScenarioMenuSection(GenericMenu menu, string sectionLabel, string folder,
                                       string column, HashSet<string> already)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        var names = new List<string>();
        foreach (string guid in guids)
        {
            names.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
        }
        names.Sort(System.StringComparer.Ordinal);

        foreach (string name in names)
        {
            if (already.Contains(name))
            {
                menu.AddDisabledItem(new GUIContent($"{sectionLabel}/{name}  (이미 있음)"), true);
                continue;
            }

            string captured = name;
            string capturedColumn = column;
            menu.AddItem(new GUIContent($"{sectionLabel}/{name}"), false, () =>
            {
                var list = ScenarioList(capturedColumn);
                list.Add(captured);
                SetScenarioList(capturedColumn, list);
                ReloadPreviewFromScenario();
            });
        }

        return names.Count;
    }
}
