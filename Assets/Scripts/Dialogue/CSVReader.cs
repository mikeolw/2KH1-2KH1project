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

    // ===== 첫 칸이 비어 있는 줄은 기본적으로 버린다 =====
    // 시나리오 CSV는 첫 칸(LineType)이 비면 대사 줄이 아니다. 엑셀에서 아래쪽에 남는
    // 빈 줄이나 메모 줄이 그런 모양이라, 그걸 걸러 주는 규칙이 꼭 필요하다.
    //
    // 그런데 표에 따라 첫 칸을 비워 두는 것이 '뜻이 있는' 경우가 있다. 핸드폰 표가
    // 그렇다 - 첫 칸이 Chapter이고, 비워 두면 '처음부터 끝까지 계속 보인다'는 뜻이다.
    // 그런 표는 keepBlankFirstColumn: true 로 부르면 된다. 이때는 '모든 칸이 빈 줄'만
    // 버린다(파일 끝의 빈 줄은 여전히 걸러진다).
    public static List<Dictionary<string, object>> Read(string file) => Read(file, false);

    public static List<Dictionary<string, object>> Read(string file, bool keepBlankFirstColumn)
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
            if (values.Length == 0) continue;
            if (keepBlankFirstColumn ? IsAllBlank(values) : values[0] == "") continue;

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

    // 모든 칸이 비어 있는 줄인가. 파일 끝에 남는 빈 줄(",,,," 같은 것도 포함)을 걸러낸다.
    private static bool IsAllBlank(string[] values)
    {
        for (int i = 0; i < values.Length; i++)
            if (values[i].Trim(TRIM_CHARS).Trim().Length > 0) return false;
        return true;
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
        if (data == null) return null;
        return Decode(file + ".csv", data.bytes, data.text);
    }

    // ===== 엑셀에서 잘못 저장한 CSV를 알아챈다 (아주 중요) =====
    // 엑셀의 '다른 이름으로 저장'에는 비슷한 항목이 두 개 있다.
    //    CSV UTF-8(쉼표로 분리)  <- 이것을 골라야 한다
    //    CSV(쉼표로 분리)        <- 이것을 고르면 ANSI(cp949)로 저장된다
    // 아래쪽으로 저장하면 한글이 전부 다른 바이트로 적힌다. 유니티는 CSV를 UTF-8로만
    // 읽기 때문에 그 파일의 한글은 통째로 깨진 글자가 되어 버린다. 그런데 화면에는
    // 그냥 이상한 글자가 보일 뿐 아무 오류도 안 나서, 원인이 '저장 옵션'이라는 것을
    // 알아채기가 거의 불가능하다 (실제로 scenario_03.csv가 이 상태로 있었다).
    // 그래서 읽을 때마다 UTF-8이 맞는지 직접 확인하고, 아니면 어느 파일인지 알린다.
    //
    // 검사에 걸려도 게임을 멈추지는 않는다. 깨진 채로나마 진행되게 두고(fallback),
    // 콘솔에 무엇을 어떻게 고치면 되는지 적어 둔다.
    private static string Decode(string label, byte[] bytes, string fallback)
    {
        if (bytes == null || bytes.Length == 0) return fallback ?? "";

        // 두 번째 인수 true = 잘못된 바이트를 만나면 조용히 넘기지 말고 예외를 던져라
        var strict = new UTF8Encoding(false, true);
        try
        {
            // 맨 앞 BOM은 Read()가 떼어내므로 여기서는 그대로 둔다.
            return strict.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Debug.LogError(
                $"[CSVReader] {label} 이 UTF-8이 아닙니다. 한글이 깨져서 나옵니다." + "\n" +
                "  엑셀에서 그 파일을 열고 [다른 이름으로 저장] > " +
                "'CSV UTF-8(쉼표로 분리)(*.csv)' 로 다시 저장해 주세요." + "\n" +
                "  ('CSV(쉼표로 분리)'를 고르면 ANSI로 저장되어 이 문제가 다시 생깁니다)");
            // 깨진 글자로라도 돌아가게 둔다 (UTF-8이 아닌 바이트는 ?로 바뀐다)
            return fallback ?? Encoding.UTF8.GetString(bytes);
        }
    }

    // 다른 프로그램이 열고 있어도 읽을 수 있게 파일을 연다.
    // 배치 도구가 IllustLayout.csv를 저장하거나 엑셀이 파일을 열어둔 동안 기본 방식
    // (File.ReadAllText)으로 읽으면 "다른 프로세스가 사용 중" 오류가 난다.
    // FileShare.ReadWrite = "다른 프로그램이 읽거나 쓰는 중이어도 나는 읽기만 하겠다"는 뜻.
    private static string ReadSharedFile(string path)
    {
        byte[] bytes;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }
        return Decode(path, bytes, null);
    }
}
