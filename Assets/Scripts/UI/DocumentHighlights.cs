using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// =====================================================================================
// 서류 형광펜 표 - 자료를 읽을 때 어디에 밑줄을 긋고 무슨 생각을 할지 적어 둔 기록
// =====================================================================================
// ===== 무엇을 하는 기능인가? =====
// 자료 뷰어에서 서류를 펼치면, 추리에 쓰이는 줄에만 형광펜이 차례로 그어지고
// 그 옆에 차재훈의 독백 말풍선이 뜬다. 어디에 긋고 무슨 말을 할지는 전부 아래 표에 있다.
// 코드는 손대지 않고 표만 고치면 된다.
//
// ===== 파일이 왜 두 개인가? =====
// 이 저장소는 공개(public)다. 대사가 git에 올라가면 누구나 스토리를 미리 볼 수 있다.
// 그래서 "스토리가 없는 숫자"와 "스토리인 글"을 갈라 두었다. (CSVReader.cs 윗부분 참고)
//
//   1) Assets/StreamingAssets/Stage/DocumentHighlights.csv   - git으로 공유 (좌표만)
//        Image,Order,X,Y,W,H
//        OBJ_01_Windoc1,1,0.118,0.150,0.520,0.030
//        - Image : 서류 그림 파일 이름 (Resources/Illusts/Objects/ 안, 확장자 없이)
//        - Order : 읽는 순서 (1부터). 같은 그림 안에서 이 순서대로 그어진다
//        - X,Y   : 형광펜 칸의 왼쪽 위 모서리. 그림 전체를 1로 본 비율 (왼쪽 위가 0,0)
//        - W,H   : 칸의 가로·세로. 역시 비율
//        비율로 적는 이유: 나중에 그림을 더 큰 해상도로 다시 뽑아도 좌표가 그대로 맞는다.
//
//   2) Assets/Resources/Dialogues/DocumentThoughts.csv       - 드라이브로 공유 (대사)
//        Image,Order,Speaker,Text
//        OBJ_01_Windoc1,1,차재훈,계약 금액이 오십억.\n안전관리비가 2.4억이면 4.8%다.
//        - Image,Order : 위 표와 똑같이 맞춘다. 이걸로 짝을 짓는다
//        - Speaker     : 말풍선 이름표. 비우면 "차재훈"
//        - Text        : 독백. \n 을 넣으면 줄이 바뀐다
//
// 2번 파일이 없는 팀원(드라이브를 안 받은 사람)도 게임은 그대로 돌아간다.
// 형광펜만 그어지고 말풍선이 안 뜰 뿐이다.
//
// ===== 좌표를 고치는 법 =====
// 유니티 메뉴 2KH1 > 서류 형광펜 배치 도구 (DocumentHighlightWindow.cs).
// 서류 그림을 보면서 칸을 끌어 옮기고, 순서를 바꾸고, 독백도 거기서 고칠 수 있다.
public static class DocumentHighlights
{
    private const string CoordCsv = "Stage/DocumentHighlights";   // git (좌표)
    private const string TextCsv = "Dialogues/DocumentThoughts";  // 드라이브 (대사)
    public const string CoordHeader = "Image,Order,X,Y,W,H";
    public const string TextHeader = "Image,Order,Speaker,Text";
    public const string DefaultSpeaker = "차재훈";

    // 형광펜 한 군데
    public class Spot
    {
        public string image;
        public int order;
        public Rect rect;        // 그림 전체를 1로 본 비율. 왼쪽 위가 (0,0)
        public string speaker;
        public string text;      // 비어 있으면 말풍선을 띄우지 않는다

        public bool HasText => !string.IsNullOrEmpty(text);
        public string Key => image + "#" + order;
    }

    // 그림 이름 -> 그 그림의 형광펜 목록 (Order 순으로 정렬됨)
    private static Dictionary<string, List<Spot>> byImage;

    // ===== 이미 읽은 곳 =====
    // 한 번 그어진 형광펜은 자료를 닫았다 다시 펼쳐도 남아 있어야 한다.
    // (두 번째부터는 말풍선 없이 밑줄만 보인다)
    private static readonly HashSet<string> revealed = new HashSet<string>();

    // ---------------------------------------------------------------------------------
    // 읽기
    // ---------------------------------------------------------------------------------
    private static void EnsureLoaded()
    {
        if (byImage != null) return;
        byImage = new Dictionary<string, List<Spot>>();

        // 1) 좌표표 (없으면 기능이 꺼진 것과 같다 - 조용히 넘어간다)
        foreach (var row in CSVReader.Read(CoordCsv))
        {
            string image = Get(row, "Image");
            if (string.IsNullOrEmpty(image)) continue;
            if (!int.TryParse(Get(row, "Order"), out int order)) continue;

            var spot = new Spot
            {
                image = image,
                order = order,
                rect = new Rect(F(row, "X"), F(row, "Y"), F(row, "W"), F(row, "H")),
                speaker = DefaultSpeaker,
            };
            // 크기가 0이면 화면에 아무것도 안 보여서 버그처럼 느껴진다. 미리 걸러낸다.
            if (spot.rect.width <= 0.0005f || spot.rect.height <= 0.0005f)
            {
                Debug.LogWarning($"[DocumentHighlights] {image} {order}번 칸의 크기가 0이라 건너뜁니다. " +
                                 "2KH1 > 서류 형광펜 배치 도구에서 다시 잡아 주세요.");
                continue;
            }

            if (!byImage.TryGetValue(image, out var list)) byImage[image] = list = new List<Spot>();
            list.Add(spot);
        }

        // 2) 대사표를 같은 (Image, Order)에 붙인다. 파일이 없으면 말풍선만 안 뜬다.
        foreach (var row in CSVReader.Read(TextCsv))
        {
            string image = Get(row, "Image");
            if (string.IsNullOrEmpty(image)) continue;
            if (!int.TryParse(Get(row, "Order"), out int order)) continue;
            if (!byImage.TryGetValue(image, out var list)) continue;

            var spot = list.Find(s => s.order == order);
            if (spot == null) continue;

            string speaker = Get(row, "Speaker");
            spot.speaker = string.IsNullOrEmpty(speaker) ? DefaultSpeaker : speaker;
            spot.text = Get(row, "Text");
        }

        foreach (var list in byImage.Values) list.Sort((a, b) => a.order.CompareTo(b.order));
    }

    // 이 그림에 그을 형광펜 목록. 없으면 빈 목록(널이 아니다).
    public static IReadOnlyList<Spot> For(string imageName)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(imageName)) return System.Array.Empty<Spot>();
        return byImage.TryGetValue(imageName, out var list) ? list : (IReadOnlyList<Spot>)System.Array.Empty<Spot>();
    }

    public static bool Any(string imageName) => For(imageName).Count > 0;

    // ---------------------------------------------------------------------------------
    // 이미 읽은 곳 기억하기
    // ---------------------------------------------------------------------------------
    public static bool IsRevealed(Spot s) => s != null && revealed.Contains(s.Key);
    public static void MarkRevealed(Spot s) { if (s != null) revealed.Add(s.Key); }

    // 이 그림을 끝까지 읽었는가 (다시 펼쳤을 때 말풍선 없이 밑줄만 보일지 판단)
    public static bool AllRevealed(string imageName)
    {
        var list = For(imageName);
        if (list.Count == 0) return false;
        for (int i = 0; i < list.Count; i++) if (!revealed.Contains(list[i].Key)) return false;
        return true;
    }

    // 아직 안 읽은 첫 번째 칸의 번호. 전부 읽었으면 list.Count.
    public static int FirstUnread(string imageName)
    {
        var list = For(imageName);
        for (int i = 0; i < list.Count; i++) if (!revealed.Contains(list[i].Key)) return i;
        return list.Count;
    }

    // 새 게임을 시작할 때 (GameFlowManager 등에서 부르면 된다)
    public static void ResetProgress() => revealed.Clear();

    // 세이브에 넣고 뺄 때 쓴다. 지금은 뷰어를 닫았다 열어도 남는 정도까지만 쓰고 있다.
    public static List<string> ExportProgress() => new List<string>(revealed);
    public static void ImportProgress(IEnumerable<string> keys)
    {
        revealed.Clear();
        if (keys == null) return;
        foreach (string k in keys) if (!string.IsNullOrEmpty(k)) revealed.Add(k);
    }

    // ---------------------------------------------------------------------------------
    private static string Get(Dictionary<string, object> row, string col) =>
        row.TryGetValue(col, out object v) && v != null ? v.ToString().Trim() : "";

    private static float F(Dictionary<string, object> row, string col) =>
        float.TryParse(Get(row, col), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;

#if UNITY_EDITOR
    // ---------------------------------------------------------------------------------
    // 고치기 (에디터 전용 - DocumentHighlightWindow가 부른다)
    // ---------------------------------------------------------------------------------
    public static string CoordPath => Path.Combine(Application.streamingAssetsPath, CoordCsv + ".csv");

    public static void Reload() { byImage = null; EnsureLoaded(); }

    // 편집용으로 통째로 꺼낸다 (도구가 고친 뒤 SaveAll로 돌려준다)
    public static Dictionary<string, List<Spot>> EditableCopy()
    {
        EnsureLoaded();
        var copy = new Dictionary<string, List<Spot>>();
        foreach (var kv in byImage)
        {
            var list = new List<Spot>();
            foreach (var s in kv.Value)
                list.Add(new Spot { image = s.image, order = s.order, rect = s.rect, speaker = s.speaker, text = s.text });
            copy[kv.Key] = list;
        }
        return copy;
    }

    // 좌표표(git)와 대사표(드라이브)를 각각 저장한다.
    // Order는 목록에 담긴 차례대로 1,2,3...으로 다시 매긴다 - 도구에서 순서를 바꾸면 그대로 반영된다.
    public static void SaveAll(Dictionary<string, List<Spot>> data)
    {
        var images = new List<string>(data.Keys);
        images.Sort(string.CompareOrdinal);

        var coord = new StringBuilder().Append(CoordHeader).Append('\n');
        var texts = new StringBuilder().Append(TextHeader).Append('\n');

        foreach (string image in images)
        {
            var list = data[image];
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                s.order = i + 1;
                coord.Append(image).Append(',').Append(s.order).Append(',')
                     .Append(N(s.rect.x)).Append(',').Append(N(s.rect.y)).Append(',')
                     .Append(N(s.rect.width)).Append(',').Append(N(s.rect.height)).Append('\n');

                if (!string.IsNullOrEmpty(s.text))
                {
                    texts.Append(image).Append(',').Append(s.order).Append(',')
                         .Append(Csv(string.IsNullOrEmpty(s.speaker) ? DefaultSpeaker : s.speaker)).Append(',')
                         .Append(Csv(s.text.Replace("\n", "\\n"))).Append('\n');
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(CoordPath));
        File.WriteAllText(CoordPath, coord.ToString(), new UTF8Encoding(false));

        // 대사표는 Resources 안이라 Resources.Load로 읽지만, 쓸 때는 실제 경로로 쓴다.
        string textPath = Path.Combine(Application.dataPath, "Resources", TextCsv + ".csv");
        Directory.CreateDirectory(Path.GetDirectoryName(textPath));
        File.WriteAllText(textPath, texts.ToString(), new UTF8Encoding(false));

        byImage = null;   // 다음에 읽을 때 새로 불러오게
    }

    private static string N(float v) =>
        v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    // 쉼표나 따옴표, 줄바꿈이 있으면 큰따옴표로 감싼다 (엑셀이 읽을 수 있게)
    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        bool needQuote = s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0;
        return needQuote ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
#endif
}
