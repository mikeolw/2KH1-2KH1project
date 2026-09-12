using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// =====================================================================================
// CSV 읽기
// =====================================================================================
// ===== CSV를 어디서 읽는가? (두 군데) =====
//   1) Assets/StreamingAssets/<이름>.csv  - git으로 공유하는 CSV
//        일러스트 배치 도구가 저장하는 파일들이 여기 있다.
//        IllustLayout.csv, InvestigationData.csv, scenario_*.csv
//   2) Assets/Resources/<이름>.csv        - 구글 드라이브로 공유하는 CSV
//        그 외 CSV(ItemData, NoteEntries, Characters, DeductionData, ItemCombinations)
//
// 부르는 쪽은 예전과 똑같이 CSVReader.Read("Dialogues/IllustLayout")처럼 이름만 넘기면 된다.
// 1번에 파일이 있으면 그것을, 없으면 2번을 읽는다.
//
// ===== 왜 나눴나? (이슈 #10) =====
// 예전에는 CSV가 전부 Assets/Resources/ 안에 있었고 이 폴더는 통째로 gitignore다.
// 그래서 배치 도구로 좌표를 고칠 때마다 드라이브에 다시 올려야 했고, 누가 안 올리면
// 팀원마다 좌표가 달라져 "멀쩡하던 배치가 틀어졌다"는 문제가 반복됐다.
// 배치 도구가 고치는 CSV만 git으로 옮기면 저장 → commit/push만으로 모두에게 전달된다.
//
// ===== 왜 하필 StreamingAssets인가? =====
// Resources.Load는 이름이 "Resources"인 폴더에서만 파일을 찾는다. 그래서 Resources 밖으로
// 옮기려면 다른 방법이 필요한데, StreamingAssets는 유니티가 빌드할 때 안의 파일을
// 가공하지 않고 그대로 게임 폴더에 복사해주는 특별한 폴더다. 그래서 에디터와 빌드 모두
// 평범한 파일처럼 읽을 수 있다.
public class CSVReader
{
    static string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";
    static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
    static char[] TRIM_CHARS = { '\"' };

    // 같은 경고를 매번 찍지 않도록, 한 번 확인한 파일 이름을 기억해둔다.
    static readonly HashSet<string> checkedDuplicates = new HashSet<string>();

    public static List<Dictionary<string, object>> Read(string file)
    {
        var list = new List<Dictionary<string, object>>();

        string text = LoadText(file);

        // 파일이 없으면 여기서 멈춘다. 예전에는 곧바로 data.text를 써서 이 경우
        // NullReferenceException으로 게임이 멈췄고, 원인이 "CSV 파일 이름 오타"라는 걸
        // 알아채기 어려웠다.
        if (text == null)
        {
            Debug.LogError($"[CSVReader] {file}.csv 를 찾지 못했습니다. " +
                           "파일 이름과 위치를 확인하세요. " +
                           "(배치 도구가 쓰는 CSV는 Assets/StreamingAssets/ 아래, " +
                           "그 외 CSV는 Assets/Resources/ 아래에 있어야 합니다)");
            return list;
        }

        // ===== 엑셀이 붙이는 BOM 제거 (아주 중요) =====
        // 엑셀에서 CSV를 저장하면 파일 맨 앞에 눈에 보이지 않는 표식 문자(U+FEFF, 이른바 BOM)가
        // 붙는다. 이걸 안 떼면 첫 번째 컬럼 이름이 "LineType"이 아니라 "(보이지 않는 문자)LineType"이
        // 되어버려서, 그 열을 찾는 코드가 전부 실패한다. 결과적으로 "엑셀로 CSV를 열었다 저장했더니
        // 갑자기 대사가 안 나온다" 같은 원인 모를 고장이 생긴다.
        // 글자 하나 떼는 것으로 이 문제가 통째로 사라지므로 여기서 처리한다.
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        var lines = Regex.Split(text, LINE_SPLIT_RE);

        if (lines.Length <= 1) return list;

        var header = Regex.Split(lines[0], SPLIT_RE);

        // 컬럼 이름 앞뒤 공백도 없애준다. 엑셀에서 편집하다 보면 " Speaker"처럼
        // 공백이 섞여 들어가는 일이 있는데, 그러면 그 열을 못 찾는다.
        for (int j = 0; j < header.Length; j++)
        {
            header[j] = header[j].TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Trim();
        }

        for (var i = 1; i < lines.Length; i++)
        {
            var values = Regex.Split(lines[i], SPLIT_RE);
            if (values.Length == 0 || values[0] == "") continue;

            var entry = new Dictionary<string, object>();
            for (var j = 0; j < header.Length && j < values.Length; j++)
            {
                string value = values[j];
                value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\n", "\n");
                entry[header[j]] = value;
            }
            list.Add(entry);
        }
        return list;
    }

    // CSV 파일의 내용을 글자로 가져온다. 어디에도 없으면 null.
    private static string LoadText(string file)
    {
        // 1) git으로 공유하는 쪽(StreamingAssets)을 먼저 본다.
        string streamingPath = Path.Combine(Application.streamingAssetsPath, file + ".csv");
        if (File.Exists(streamingPath))
        {
            WarnIfStaleResourcesCopy(file);
            return ReadSharedFile(streamingPath);
        }

        // 2) 없으면 드라이브로 공유하는 쪽(Resources)을 읽는다.
        TextAsset data = Resources.Load<TextAsset>(file);
        return data != null ? data.text : null;
    }

    // ===== 옛날 복사본이 남아 있으면 알려준다 =====
    // 이 CSV들은 원래 Assets/Resources/Dialogues/ 에 있었다. 드라이브에서 옛날 Resources 폴더를
    // 통째로 받아 덮어쓴 팀원은 두 곳에 같은 이름의 파일이 생긴다. 게임은 항상 StreamingAssets
    // 쪽을 쓰므로 동작은 문제없지만, Resources 쪽을 고쳐놓고 "왜 반영이 안 되지?" 하고 헤맬 수
    // 있어서 한 번만 경고를 남긴다. (파일당 한 번만 확인하므로 매번 느려지지 않는다)
    private static void WarnIfStaleResourcesCopy(string file)
    {
        if (!checkedDuplicates.Add(file)) return;
        if (Resources.Load<TextAsset>(file) == null) return;

        Debug.LogWarning(
            $"[CSVReader] {file}.csv 가 Assets/StreamingAssets/ 와 Assets/Resources/ 양쪽에 있습니다. " +
            "게임은 StreamingAssets 쪽(git으로 공유되는 최신본)을 사용합니다. " +
            $"Assets/Resources/{file}.csv 는 예전 복사본이므로 지워주세요.");
    }

    // 다른 프로그램이 열고 있어도 읽을 수 있게 파일을 연다.
    // 엑셀은 CSV를 열어두는 동안 파일을 붙잡고 있어서, 기본 방식(File.ReadAllText)으로 읽으면
    // "다른 프로세스가 사용 중" 오류가 난다. 예전 Resources 방식은 유니티가 복사해둔 사본을
    // 읽었기 때문에 이 문제가 없었는데, 이제 원본 파일을 직접 읽으므로 따로 처리한다.
    // FileShare.ReadWrite = "다른 프로그램이 읽거나 쓰는 중이어도 나는 읽기만 하겠다"는 뜻.
    private static string ReadSharedFile(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream, Encoding.UTF8, true))
        {
            return reader.ReadToEnd();
        }
    }
}
