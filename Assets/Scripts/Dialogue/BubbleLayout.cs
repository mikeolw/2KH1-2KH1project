using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// =====================================================================================
// 말풍선 배치표 - 줄마다 "말풍선 / 대화창"과 말풍선 위치를 직접 정해 둔 기록
// =====================================================================================
// ===== 왜 필요한가? =====
// 말풍선은 기본 규칙(SpeechBubble.cs 상단)대로 자동으로 뜨지만, 장면에 따라 위치가 어색하거나
// 이 줄은 대화창이 낫겠다 싶은 경우가 있다. 그런 줄만 골라서 고쳐 둔 것이 이 배치표다.
// 여기 없는 줄은 전부 자동 규칙을 따른다 - 그래서 모든 줄을 하나하나 정할 필요는 없다.
//
// ===== 고치는 법 (에디터에서 Play 중) =====
// F2를 누르면 "말풍선 배치 모드"가 켜진다 (BubbleLayoutTool.cs).
//   - 말풍선을 마우스로 끌면 그 줄의 위치가 저장된다
//   - B : 이 줄을 말풍선 ↔ 대화창으로 바꾼다
//   - R : 이 줄을 자동 규칙으로 되돌린다 (배치표에서 지움)
// 저장할 때마다 바로 아래 파일에 기록된다.
//
// ===== 파일 =====
// Assets/StreamingAssets/Stage/BubbleLayout.csv  (git으로 공유 - 스토리가 없는 숫자만 담는다)
//   Scenario,Line,Mode,X,Y,Check
//   scenario_03,42,bubble,180,210,9f1c0a3e
//   - Scenario/Line : 시나리오 CSV 이름과 몇 번째 줄인지 (0부터, 대사 줄 기준 - "이미 읽음" 기록과 같은 번호)
//   - Mode          : bubble(말풍선) / box(대화창)
//   - X, Y          : 말풍선 가운데 위치 (화면 1440x1080, 가운데가 0,0 / 위로 +). 비면 자동 위치
//   - Check         : 그 줄 대사로 만든 확인용 숫자. 대사 원문은 저장하지 않는다(공개 저장소라서).
//                     나중에 시나리오가 고쳐져 줄 번호가 밀리면 이 숫자가 안 맞게 되므로,
//                     엉뚱한 줄에 배치가 붙지 않게 그 기록은 무시한다(콘솔에 한 번 알림).
public static class BubbleLayout
{
    public enum Mode { Auto, Bubble, Box }

    private const string CsvName = "Stage/BubbleLayout";
    private static readonly string Header = "Scenario,Line,Mode,X,Y,Check";

    private class Entry
    {
        public string scenario;
        public int line;
        public Mode mode;
        public bool hasPos;
        public Vector2 pos;
        public string check;
    }

    private static Dictionary<string, Entry> entries;
    private static readonly HashSet<string> warnedMismatch = new HashSet<string>();

    private static string Key(string scenario, int line) => (scenario ?? "") + "#" + line;

    // 대사로 만든 확인용 숫자 (FNV-1a 32비트). 대사가 같으면 항상 같은 값이 나온다.
    public static string CheckOf(string sentence)
    {
        uint h = 2166136261;
        foreach (char c in (sentence ?? "").Trim())
        {
            h ^= c;
            h *= 16777619;
        }
        return h.ToString("x8");
    }

    private static void EnsureLoaded()
    {
        if (entries != null) return;
        entries = new Dictionary<string, Entry>();
        if (!File.Exists(FilePath)) return;   // 아직 고친 줄이 없으면 파일이 없어도 된다

        foreach (var row in CSVReader.Read(CsvName))
        {
            var e = new Entry
            {
                scenario = Get(row, "Scenario"),
                line = int.TryParse(Get(row, "Line"), out int l) ? l : -1,
                check = Get(row, "Check"),
            };
            string mode = Get(row, "Mode").ToLowerInvariant();
            e.mode = mode == "box" ? Mode.Box : mode == "bubble" ? Mode.Bubble : Mode.Auto;
            if (float.TryParse(Get(row, "X"), out float x) && float.TryParse(Get(row, "Y"), out float y))
            {
                e.hasPos = true;
                e.pos = new Vector2(x, y);
            }
            if (e.line >= 0 && !string.IsNullOrEmpty(e.scenario)) entries[Key(e.scenario, e.line)] = e;
        }
    }

    private static string Get(Dictionary<string, object> row, string col) =>
        row.TryGetValue(col, out object v) && v != null ? v.ToString().Trim() : "";

    private static string FilePath => Path.Combine(Application.streamingAssetsPath, CsvName + ".csv");

    // 이 줄에 대해 정해 둔 것이 있으면 꺼낸다. 대사가 바뀌어 확인 숫자가 안 맞으면 없는 것으로 친다.
    private static Entry Find(string scenario, int line, string sentence)
    {
        EnsureLoaded();
        if (!entries.TryGetValue(Key(scenario, line), out var e)) return null;
        if (!string.IsNullOrEmpty(e.check) && e.check != CheckOf(sentence))
        {
            if (warnedMismatch.Add(Key(scenario, line)))
                Debug.LogWarning($"[BubbleLayout] {scenario} {line}번째 줄의 대사가 바뀌어 말풍선 배치를 무시합니다. " +
                                 "Play 중 F2로 다시 잡아 주세요.");
            return null;
        }
        return e;
    }

    public static Mode GetMode(string scenario, int line, string sentence) => Find(scenario, line, sentence)?.mode ?? Mode.Auto;

    public static bool TryGetPosition(string scenario, int line, string sentence, out Vector2 pos)
    {
        var e = Find(scenario, line, sentence);
        pos = e != null ? e.pos : Vector2.zero;
        return e != null && e.hasPos && e.mode != Mode.Box;
    }

#if UNITY_EDITOR
    // ---------------------------------------------------------------------------------
    // 고치기 (에디터에서만 - BubbleLayoutTool이 부른다)
    // ---------------------------------------------------------------------------------
    public static void SetMode(string scenario, int line, string sentence, Mode mode)
    {
        var e = GetOrCreate(scenario, line, sentence);
        e.mode = mode;
        if (mode == Mode.Box) e.hasPos = false;
        Save();
    }

    public static void SetPosition(string scenario, int line, string sentence, Vector2 pos)
    {
        var e = GetOrCreate(scenario, line, sentence);
        e.mode = Mode.Bubble;
        e.hasPos = true;
        e.pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
        Save();
    }

    public static void Remove(string scenario, int line)
    {
        EnsureLoaded();
        if (entries.Remove(Key(scenario, line))) Save();
    }

    private static Entry GetOrCreate(string scenario, int line, string sentence)
    {
        EnsureLoaded();
        string key = Key(scenario, line);
        if (!entries.TryGetValue(key, out var e))
        {
            e = new Entry { scenario = scenario, line = line, mode = Mode.Auto };
            entries[key] = e;
        }
        e.check = CheckOf(sentence);   // 지금 대사 기준으로 다시 맞춘다
        warnedMismatch.Remove(key);
        return e;
    }

    // 시나리오 이름 → 줄 번호 순으로 정렬해 저장한다 (git에서 바뀐 줄만 보이게).
    private static void Save()
    {
        var list = new List<Entry>(entries.Values);
        list.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.scenario, b.scenario);
            return c != 0 ? c : a.line.CompareTo(b.line);
        });

        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');
        foreach (var e in list)
        {
            if (e.mode == Mode.Auto && !e.hasPos) continue;   // 자동이면 적을 필요가 없다
            string mode = e.mode == Mode.Box ? "box" : "bubble";
            string x = e.hasPos ? e.pos.x.ToString("0") : "";
            string y = e.hasPos ? e.pos.y.ToString("0") : "";
            sb.Append(e.scenario).Append(',').Append(e.line).Append(',').Append(mode).Append(',')
              .Append(x).Append(',').Append(y).Append(',').Append(e.check).Append('\n');
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
    }
#endif
}
