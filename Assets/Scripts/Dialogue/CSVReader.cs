using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// =====================================================================================
// CSV 읽기
// =====================================================================================
// ===== CSV를 어디서 읽는가? (두 군데, 서로 섞이지 않는다) =====
//   1) Assets/Resources/<이름>.csv            - 스토리가 담긴 CSV (구글 드라이브로 공유, gitignore)
//        scenario_*.csv, InvestigationData.csv, ItemData, NoteEntries, Characters,
//        DeductionData, ItemCombinations
//   2) Assets/StreamingAssets/Stage/<이름>.csv - 스토리가 없는 공통 데이터 (git으로 공유)
//        IllustLayout.csv (일러스트 배치 좌표)
//
// 부르는 쪽은 이름만 넘긴다. "Stage/"로 시작하면 2번, 그 외는 전부 1번에서 읽는다.
//   CSVReader.Read("Dialogues/scenario_01")  -> Assets/Resources/Dialogues/scenario_01.csv
//   CSVReader.Read("Stage/IllustLayout")     -> Assets/StreamingAssets/Stage/IllustLayout.csv
//
// ===== 왜 스토리 CSV는 반드시 Resources에서만 읽는가? =====
//   - 이 저장소는 공개(public)다. git에 올라가면 누구나 스토리를 미리 볼 수 있으므로
//     스토리 CSV는 통째로 gitignore된 Resources 폴더에 둔다.
//   - StreamingAssets 폴더는 빌드할 때 안의 파일이 "원본 그대로" 게임 폴더에 복사된다.
//     거기에 스토리 CSV가 있으면 설치된 게임 폴더에서 메모장으로 대사를 전부 열어볼 수 있다.
//     Resources는 빌드 시 데이터 파일 하나로 묶여 들어가서 그렇게 바로 열리지는 않는다.
//   그래서 스토리 CSV는 StreamingAssets 쪽을 아예 찾아보지 않는다. 누가 실수로 그쪽에
//   복사해 넣어도 게임이 그 파일에 기대지 않게, 읽는 위치를 이름으로 딱 나눠두었다.
//
// ===== 왜 좌표(IllustLayout)만 StreamingAssets/Stage인가? =====
//   Resources.Load는 "Resources" 폴더 안만 찾는데, 그 폴더는 gitignore다. 좌표를 Resources에
//   두면 배치 도구로 고칠 때마다 드라이브에 다시 올려야 해서 팀원마다 배치가 달라졌다(이슈 #10).
//   좌표에는 스토리가 없으므로 git으로 공유하고, 빌드에 원본으로 들어가도 문제가 없다.
//   (주의: Stage 폴더에는 대사·설명문이 들어간 파일을 절대 넣지 말 것)
public class CSVReader
{
    static string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";
    static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
    static char[] TRIM_CHARS = { '\"' };

    // 스토리가 없는 공통 데이터만 이 접두어로 부른다 (위 주석 참고).
    const string SharedStagePrefix = "Stage/";

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
                           "(스토리 CSV는 Assets/Resources/ 아래, " +
                           "좌표 같은 공통 데이터는 Assets/StreamingAssets/Stage/ 아래에 있어야 합니다)");
            return list;
        }

        // ===== 엑셀이 붙이는 BOM 제거 (아주 중요) =====
        // 엑셀에서 CSV를 저장하면 파일 맨 앞에 눈에 보이지 않는 표식 문자(U+FEFF, 이른바 BOM)가
        // 붙는다. 이걸 안 떼면 첫 번째 컬럼 이름이 "LineType"이 아니라 "(보이지 않는 문자)LineType"이
        // 되어버려서, 그 열을 찾는 코드가 전부 실패한다. 결과적으로 "엑셀로 CSV를 열었다 저장했더니
        // 갑자기 대사가 안 나온다" 같은 원인 모를 고장이 생긴다.
        // 글자 하나 떼는 것으로 이 문제가 통째로 사라지므로 여기서 처리한다.
        if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);

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
                value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\n", "\n");
                entry[header[j]] = value;
            }
            list.Add(entry);
        }
        return list;
    }

    // CSV 파일의 내용을 글자로 가져온다. 없으면 null.
    private static string LoadText(string file)
    {
        // 스토리가 없는 공통 데이터: git으로 공유하는 StreamingAssets/Stage 에서 읽는다.
        if (file.StartsWith(SharedStagePrefix))
        {
            string path = Path.Combine(Application.streamingAssetsPath, file + ".csv");
            return File.Exists(path) ? ReadSharedFile(path) : null;
        }

        // 그 외(스토리 CSV 전부): Resources에서만 읽는다. StreamingAssets는 보지 않는다.
        TextAsset data = Resources.Load<TextAsset>(file);
        return data != null ? data.text : null;
    }

    // 다른 프로그램이 열고 있어도 읽을 수 있게 파일을 연다.
    // 배치 도구가 IllustLayout.csv를 저장하거나 엑셀이 파일을 열어둔 동안 기본 방식
    // (File.ReadAllText)으로 읽으면 "다른 프로세스가 사용 중" 오류가 난다.
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
