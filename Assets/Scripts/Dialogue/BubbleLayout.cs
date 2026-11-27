using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// =====================================================================================
// 말풍선 배치표 - 스탠딩마다 말풍선 자리를 하나씩 정해 둔 기록
// =====================================================================================
// ===== 규칙은 하나다: "한 종류의 스탠딩 = 중심점 하나" =====
// 같은 스탠딩이 화면에 있는 동안은 누가 말하든, 배경이 무엇이든, 몇 장이든 말풍선의
// 가운데가 늘 같은 점에 온다. 그 점은 스탠딩 한 종류당 딱 하나만 존재한다.
//
// 표정과 입모양(_OpenMouse)은 열쇠에서 떼어낸다. 그래서 같은 인물의 표정이 바뀌어도
// 말풍선은 꼼짝하지 않는다 (StageController.StandingKeyOf 참고).
//
// ===== 어떻게 여기까지 왔나 (같은 실수를 되풀이하지 않게) =====
//   1차: 줄마다 좌표  -> 화자가 바뀔 때마다 말풍선이 화면을 가로질러 건너뛰었다.
//   2차: 장면마다 좌표 -> 한 장면 안에서는 가만히 있었지만, 같은 인물인데도 장면이
//                        넘어갈 때마다 자리가 바뀌어 여전히 어수선했다.
//   3차(지금): 스탠딩마다 좌표 -> 인물이 같으면 어디서든 같은 자리.
//
// ===== 고치는 법 (에디터에서 Play 중) =====
// F2를 누르면 "말풍선 배치 모드"가 켜진다 (BubbleLayoutTool.cs).
//   - 말풍선을 마우스로 끌면 "지금 서 있는 스탠딩"의 자리가 저장된다
//     (그 스탠딩이 나오는 모든 장면·모든 대사에 적용된다)
//   - B : 이 줄만 말풍선 ↔ 대화창으로 바꾼다 (이것만 줄 단위다 - 아래 설명)
//   - R : 이 스탠딩의 자리를 지운다 (자동 배치로 되돌아간다)
// 저장할 때마다 바로 아래 파일에 기록된다.
//
// ===== 왜 모양(말풍선/대화창)만 줄 단위인가 =====
// 자리는 "이 인물의 얼굴을 어디서 피하나"의 문제라 스탠딩 단위가 맞다.
// 반면 "이 줄은 대화창이 낫겠다"는 그 대사 하나의 성격(긴 설명, 분위기) 문제라 줄 단위다.
// 그래서 한 파일 안에 두 종류의 줄이 섞여 있다.
//
// ===== 파일 =====
// Assets/StreamingAssets/Stage/BubbleLayout.csv  (git으로 공유 - 스토리가 없는 숫자만 담는다)
//   Standing,Scenario,Line,Mode,X,Y,Check
//
//   [자리 줄]  Standing을 적고 Scenario/Line은 비운다
//     STD_04_Child,,,,204,264,
//       - Standing : 표정을 뗀 스탠딩 열쇠. 두 사람이면 "A+B"(이름 순서대로)
//                    아무도 없는 장면(내레이션 등)은 none
//       - X, Y     : 말풍선 가운데 (화면 1440x1080, 가운데가 0,0 / 위로 +)
//       - 시나리오를 적지 않는다. 중심점은 스탠딩당 하나뿐이어서 장·파일과 무관하다.
//
//   [모양 줄]  Scenario와 Line을 적고 Standing은 비운다
//     ,scenario_02,14,box,,,81f6b54d
//       - Line  : 몇 번째 대사 줄인지 (0부터 - "이미 읽음" 기록과 같은 번호)
//       - Mode  : bubble(말풍선) / box(대화창)
//       - Check : 그 줄 대사로 만든 확인용 숫자. 대사 원문은 저장하지 않는다(공개 저장소라서).
//                 시나리오가 고쳐져 줄 번호가 밀리면 이 숫자가 안 맞게 되므로, 엉뚱한 줄에
//                 모양이 붙지 않게 그 기록은 무시한다(콘솔에 한 번 알림).
//
// 여기 없는 스탠딩은 자동이다 - 모든 스탠딩을 하나하나 정할 필요는 없다. 자동이어도
// 한 번 고른 자리를 그 스탠딩의 것으로 기억해 두므로 중심점은 역시 하나다
// (SpeechBubble.ShowAnchored).
public static class BubbleLayout
{
    public enum Mode { Auto, Bubble, Box }

    private const string CsvName = "Stage/BubbleLayout";
    private const string Header = "Standing,Scenario,Line,Mode,X,Y,Check";

    // 스탠딩 자리: "표정을 뗀 스탠딩 열쇠" -> 말풍선 가운데. 열쇠 하나에 점 하나다.
    private class AnchorPos
    {
        public string standing;
        public Vector2 pos;
    }

    // 줄 모양: "시나리오#줄번호" -> 말풍선/대화창
    private class LineMode
    {
        public string scenario;
        public int line;
        public Mode mode;
        public string check;
    }

    private static Dictionary<string, AnchorPos> anchors;
    private static Dictionary<string, LineMode> modes;
    private static readonly HashSet<string> warnedMismatch = new HashSet<string>();

    private static string LineKey(string scenario, int line) => (scenario ?? "") + "#" + line;

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
        if (anchors != null) return;
        anchors = new Dictionary<string, AnchorPos>();
        modes = new Dictionary<string, LineMode>();
        if (!File.Exists(FilePath)) return;   // 아직 고친 것이 없으면 파일이 없어도 된다

        int oldFormatRows = 0;

        foreach (var row in CSVReader.Read(CsvName, keepBlankFirstColumn: true))
        {
            // 예전 형식(Scene 칸으로 장면마다 적어 둔 것)은 더 쓸 수 없다. 세어 두고 알린다.
            if (!string.IsNullOrEmpty(Get(row, "Scene")))
            {
                oldFormatRows++;
                continue;
            }

            string standing = Get(row, "Standing");
            string scenario = Get(row, "Scenario");
            string lineText = Get(row, "Line");
            // &&(앞이 거짓이면 뒤를 건너뜀) 대신 &를 쓰면 y가 반드시 채워져 컴파일러가 안심한다
            bool hasPos = float.TryParse(Get(row, "X"), out float x)
                        & float.TryParse(Get(row, "Y"), out float y);

            // ----- 자리 줄 (Standing이 적혀 있다) -----
            if (!string.IsNullOrEmpty(standing))
            {
                if (!hasPos) continue;
                anchors[standing] = new AnchorPos
                {
                    standing = standing,
                    pos = new Vector2(x, y),
                };
                continue;
            }

            // ----- 모양 줄 (Scenario와 Line이 적혀 있다) -----
            if (string.IsNullOrEmpty(scenario)) continue;
            if (!int.TryParse(lineText, out int line) || line < 0) continue;

            string modeText = Get(row, "Mode").ToLowerInvariant();
            modes[LineKey(scenario, line)] = new LineMode
            {
                scenario = scenario,
                line = line,
                mode = modeText == "box" ? Mode.Box : modeText == "bubble" ? Mode.Bubble : Mode.Auto,
                check = Get(row, "Check"),
            };
        }

        if (oldFormatRows > 0)
        {
            Debug.LogWarning(
                $"[BubbleLayout] 장면마다 좌표를 적어 둔 예전 형식의 줄 {oldFormatRows}개를 건너뜁니다. " +
                "말풍선 자리는 이제 스탠딩 단위입니다(한 종류당 하나).\n" +
                "  Play 중 F2로 그 인물이 서 있을 때 말풍선을 한 번만 끌어 주면 됩니다.");
        }
    }

    private static string Get(Dictionary<string, object> row, string col) =>
        row.TryGetValue(col, out object v) && v != null ? v.ToString().Trim() : "";

    private static string FilePath => Path.Combine(Application.streamingAssetsPath, CsvName + ".csv");

    // ---------------------------------------------------------------------------------
    // 읽기
    // ---------------------------------------------------------------------------------

    // 이 스탠딩에 정해 둔 말풍선 자리가 있으면 꺼낸다.
    public static bool TryGetPosition(string standingKey, out Vector2 pos)
    {
        pos = Vector2.zero;
        if (string.IsNullOrEmpty(standingKey)) return false;

        EnsureLoaded();
        if (!anchors.TryGetValue(standingKey, out var e)) return false;
        pos = e.pos;
        return true;
    }

    // 이 줄의 모양(말풍선/대화창). 대사가 바뀌어 확인 숫자가 안 맞으면 자동으로 친다.
    public static Mode GetMode(string scenario, int line, string sentence)
    {
        EnsureLoaded();
        string key = LineKey(scenario, line);
        if (!modes.TryGetValue(key, out var e)) return Mode.Auto;

        if (!string.IsNullOrEmpty(e.check) && e.check != CheckOf(sentence))
        {
            if (warnedMismatch.Add(key))
                Debug.LogWarning($"[BubbleLayout] {scenario} {line}번째 줄의 대사가 바뀌어 말풍선 모양 설정을 무시합니다. " +
                                 "Play 중 F2 > B 로 다시 정해 주세요.");
            return Mode.Auto;
        }
        return e.mode;
    }

#if UNITY_EDITOR
    // ---------------------------------------------------------------------------------
    // 고치기 (에디터에서만 - BubbleLayoutTool이 부른다)
    // ---------------------------------------------------------------------------------

    // 이 스탠딩의 말풍선 자리를 정한다. 그 스탠딩이 나오는 모든 대사에 적용된다.
    // 열쇠 하나에 점 하나이므로, 같은 열쇠로 다시 저장하면 앞의 값을 덮어쓴다(둘로 늘지 않는다).
    public static void SetPosition(string standingKey, Vector2 pos)
    {
        if (string.IsNullOrEmpty(standingKey)) return;
        EnsureLoaded();
        anchors[standingKey] = new AnchorPos
        {
            standing = standingKey,
            pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)),
        };
        Save();
    }

    // 이 스탠딩을 자동 배치로 되돌린다.
    public static void Clear(string standingKey)
    {
        if (string.IsNullOrEmpty(standingKey)) return;
        EnsureLoaded();
        if (anchors.Remove(standingKey)) Save();
    }

    public static void SetMode(string scenario, int line, string sentence, Mode mode)
    {
        EnsureLoaded();
        string key = LineKey(scenario, line);
        if (mode == Mode.Auto)
        {
            if (modes.Remove(key)) Save();
            return;
        }
        modes[key] = new LineMode
        {
            scenario = scenario,
            line = line,
            mode = mode,
            check = CheckOf(sentence),   // 지금 대사 기준으로 다시 맞춘다
        };
        warnedMismatch.Remove(key);
        Save();
    }

    // 자리 줄은 스탠딩 이름 순으로, 모양 줄은 시나리오 -> 줄 번호 순으로 적는다
    // (git에서 바뀐 줄만 보이게). 자리 줄을 먼저 몰아 적어서 사람이 읽기 쉽게 한다.
    private static void Save()
    {
        var anchorList = new List<AnchorPos>(anchors.Values);
        anchorList.Sort((a, b) => string.CompareOrdinal(a.standing, b.standing));

        var modeList = new List<LineMode>(modes.Values);
        modeList.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.scenario, b.scenario);
            return c != 0 ? c : a.line.CompareTo(b.line);
        });

        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');

        foreach (var e in anchorList)
        {
            sb.Append(e.standing).Append(",,,,")
              .Append(e.pos.x.ToString("0")).Append(',').Append(e.pos.y.ToString("0")).Append(",\n");
        }

        foreach (var e in modeList)
        {
            if (e.mode == Mode.Auto) continue;   // 자동이면 적을 필요가 없다
            sb.Append(',').Append(e.scenario).Append(',').Append(e.line).Append(',')
              .Append(e.mode == Mode.Box ? "box" : "bubble").Append(",,,")
              .Append(e.check).Append('\n');
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
    }
#endif
}
