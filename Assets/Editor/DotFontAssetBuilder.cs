using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// =====================================================================================
// 도트 글꼴(Neo둥근모) TMP 에셋 만들기 - 메뉴: 2KH1 / 글꼴 / 도트 글꼴 에셋 만들기
// =====================================================================================
// ===== 왜 손으로 만들지 않고 도구를 만드나 =====
// 도트 글꼴은 TextMeshPro의 기본 설정(SDF)으로 구우면 반드시 흐려진다. SDF는 글자
// 윤곽선을 "거리값"으로 저장해 매끄럽게 펴주는 방식이라, 각이 살아야 하는 도트 글꼴의
// 계단 모양을 뭉개 버린다. 선명하게 나오려면 아래 설정이 전부 맞아야 한다:
//
//   1) Render Mode = RASTER_HINTED  (안티에일리어싱 없이 점 단위로 굽는다)
//   2) Sampling Point Size = 글꼴 고유 크기(16)  - 다른 값이면 글자가 이미 구울 때 뭉개진다
//   3) Atlas Padding = 0            - 도트 글꼴은 여백이 필요 없다
//   4) 아틀라스 텍스처 Filter Mode = Point  - Bilinear이면 화면에서 다시 흐려진다
//   5) 머티리얼 셰이더 = TextMeshPro/Bitmap - SDF 셰이더는 경계를 부드럽게 만든다
//
// Font Asset Creator 창에서 이 다섯 개를 매번 손으로 맞추면 하나씩 빠뜨리게 되고,
// 팀원이 다시 구울 때 설정이 달라져 글자 모양이 바뀐다. 그래서 코드로 고정한다.
//
// ===== 글자를 "쓰는 것만" 골라 굽는다 (Static 아틀라스) =====
// 한글 완성형은 11,172자다. 전부 구우면 16px이라도 아틀라스가 커지고, 쓰지도 않는
// 글자가 빌드에 들어간다. 이 도구는 게임에 실제로 나오는 글자만 모아서 굽는다:
//
//   - CSV (시나리오/조사/아이템/수첩...)  → 대사와 항목 이름
//   - 스크립트의 문자열 ("..." 안쪽)      → 코드로 만든 UI 글자
//   - 씬 파일의 m_text                    → 씬에 손으로 배치해 둔 글자
//   - 기본 묶음                            → ASCII, 자모, 화살표 같은 기호
//
// 빠진 글자가 있으면 콘솔에 전부 찍어 준다. 그 목록이 비어 있으면 이 글꼴로 게임
// 전체를 표시할 수 있다는 뜻이다(두부 □ 가 안 뜬다).
//
// ===== 쓰는 순서 =====
//   1) Neo둥근모 TTF를 Assets/Fonts/ 에 넣는다 (파일 이름에 neodunggeunmo 또는 neodgm 포함)
//   2) 메뉴 2KH1 / 글꼴 / 도트 글꼴 에셋 만들기
//   3) 콘솔에서 "빠진 글자: 없음" 확인
// 대사 CSV를 고친 뒤에는 다시 한 번 돌려야 새 글자가 들어간다.
public static class DotFontAssetBuilder
{
    // ---------------------------------------------------------------------------------
    // 설정값
    // ---------------------------------------------------------------------------------

    // 글꼴 고유 크기. 둥근모꼴 계열은 16x16 비트맵에서 온 글꼴이라 16이다.
    // 이 값으로 구워야 글자가 원래 도트 모양 그대로 나온다.
    // (다른 도트 글꼴로 바꾼다면 그 글꼴의 고유 px로 고쳐야 한다. FontManager.PixelGrid도 같이.)
    public const int SamplingPointSize = 16;

    // ===== 글자 사이 여백 (0으로 두면 글자가 깨진다) =====
    // 처음에 "도트 글꼴은 여백이 필요 없다"고 0으로 두었다가 글자가 깨져 나왔다.
    // 아틀라스에 글자를 빈틈 없이 붙여 놓으면, 글자를 그릴 때 사각형 경계에서 바로 옆
    // 글자의 점을 집어온다. 그래서 글자 주변에 다른 글자 조각이 붙어 보인다.
    // 1픽셀만 띄워 두면 경계에서 집어오는 것이 "투명"이 되어 깨끗해진다.
    //
    // 1보다 크게 둘 이유는 없다. SDF처럼 번지는 효과를 쓰지 않기 때문에 여백이 넓어도
    // 아틀라스만 커진다. (1038자 기준 18x18 = 약 34만 px, 1024x1024에 넉넉히 들어간다)
    public const int AtlasPadding = 1;

    // 굽는 방식. RASTER_HINTED는 안티에일리어싱 없이 점 단위로 굽고 픽셀 격자에 맞춰 준다.
    private const GlyphRenderMode RenderMode = GlyphRenderMode.RASTER_HINTED;

    // 아틀라스 크기. 16px 글자는 한 장(1024x1024)에 약 4,000자가 들어간다.
    // 쓰는 글자가 그보다 많으면 멀티 아틀라스로 자동 분할된다.
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 1024;

    // 만들어질 에셋 자리.
    // Assets/Resources/ 가 아니라 "Assets/TextMesh Pro/Resources/" 아래에 두는 이유:
    //   - Assets/Resources/ 는 드라이브로 공유하는 폴더라 git에 올라가지 않는다(.gitignore).
    //     글꼴은 스토리가 아니라 코드와 같이 가야 하므로 git에 올라가는 자리에 둔다.
    //   - 그래도 Resources 폴더 안이라서 Resources.Load 로 런타임에 불러올 수 있고
    //     빌드에도 반드시 포함된다. (UIFontHelper.DotFont 가 이 경로로 읽는다)
    private const string OutputDir = "Assets/TextMesh Pro/Resources/Fonts & Materials";
    private const string OutputName = "NeoDunggeunmo";   // 한글 파일명은 경로 문제가 생길 수 있어 영문으로

    // TTF를 찾을 폴더와 파일 이름 조각
    private static readonly string[] SearchDirs =
    {
        "Assets/Fonts",
        "Assets/TextMesh Pro/Resources/Fonts & Materials",
        "Assets/TextMesh Pro/Fonts",
    };
    private static readonly string[] NameHints = { "neodunggeunmo", "neodgm", "둥근모" };

    // 글자를 긁어올 곳
    private static readonly string[] CsvDirs =
    {
        "Assets/StreamingAssets/Stage",
        "Assets/Resources/Dialogues",      // 드라이브 공유분. 없으면 건너뛴다.
        "Assets/StreamingAssets",
    };

    // ===== 통째로 화면에 뜨는 텍스트 파일 =====
    // 크레딧 화면이 글꼴 라이선스 전문을 TextAsset으로 읽어 그대로 보여준다
    // (CreditsPanelUI.cs). 이 파일은 CSV도 스크립트도 아니어서 위 경로들에 안 걸리는데,
    // 안 넣으면 전문 안의 예약 글꼴 이름 "Neo둥근모"의 '둥·근·모'가 빈칸으로 나온다.
    // 라이선스는 글자 하나라도 빠지면 안 되는 글이므로 글자 목록에 꼭 넣는다.
    private static readonly string[] TextAssetFiles =
    {
        "Assets/TextMesh Pro/Resources/Fonts & Materials/NeoDunggeunmo-OFL.txt",
    };

    // ===== 기본 묶음 =====
    // CSV나 코드에 아직 없더라도 반드시 들어가야 하는 글자들.
    // 나중에 대사를 추가했을 때 글꼴을 다시 굽지 않아도 숫자/기호가 깨지지 않게 한다.
    private const string BaseAscii =
        " !\"#$%&'()*+,-./0123456789:;<=>?@" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
        "abcdefghijklmnopqrstuvwxyz{|}~";

    // ===== 반드시 들어가야 하는 한글 =====
    // UIFontHelper.SupportsKorean()이 '가'가 있는지로 "이 글꼴이 한글을 그릴 수 있나"를
    // 판정한다. 이 도구는 "게임에 쓰는 글자만" 굽기 때문에, 하필 '가'가 대사에 없으면
    // 다 구워 놓고도 도트 글꼴이 조용히 거부된다. 그래서 '가'를 못 박아 둔다.
    // (UIFontHelper.KoreanSample 과 짝이다. 한쪽만 고치면 안 된다)
    private const string RequiredHangul = "가";

    // 화면에 쓰는 기호: 계속 표시(▼), 화살표, 말줄임, 따옴표, 괄호 등
    private const string BaseSymbols =
        "▼▲▶◀△▽○●◎◇◆□■★☆※·…—–‘’“”「」『』〈〉《》【】" +
        "←→↑↓↔♪♩♬✓✕×÷±≤≥∞°㎝㎞㎏㎖ⓒ™" +
        "ㅁㅂㅈㄷㄱㅅㅛㅕㅑㅐㅔㅍㅎㅗㅓㅏㅣㅋㅌㅊㅍㅠㅜㅡ" +
        "①②③④⑤⑥⑦⑧⑨⑩";

    // ---------------------------------------------------------------------------------
    // 메뉴
    // ---------------------------------------------------------------------------------

    [MenuItem("2KH1/글꼴/도트 글꼴 에셋 만들기", false, 10)]
    public static void Build() => Run(false);

    // ===== 에셋이 없거나 설정이 바뀌면 알아서 다시 굽는다 =====
    // TTF는 git에 올라가지만, 팀원이 받은 뒤 메뉴를 눌러야 한다는 걸 모르면
    // "도트가 안 먹는다"가 된다. 그래서 TTF가 있는데 에셋이 없으면 알아서 굽는다.
    //
    // 위쪽 설정값(크기/여백/굽는 방식)을 고쳤을 때도 다시 굽는다. 이게 없으면 값을 바꿔도
    // 예전에 구운 에셋이 그대로 쓰여서 "고쳤는데 왜 그대로지?"가 된다.
    // (실제로 여백을 0 -> 1로 고칠 때 이 함정에 걸렸다)
    //
    // delayCall로 미루는 이유: 에디터가 켜지는 중(에셋 임포트 도중)에 글꼴을 구우면
    // AssetDatabase가 아직 준비되지 않아 실패할 수 있다.
    [InitializeOnLoadMethod]
    private static void AutoBuildIfStale()
    {
        EditorApplication.delayCall += () =>
        {
            if (FindTtf() == null) return;      // 구울 원본이 없으면 아무것도 하지 않는다

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{OutputDir}/{OutputName}.asset");
            if (existing == null)
            {
                Debug.Log("[도트 글꼴] 글꼴 에셋이 없어 자동으로 굽습니다.");
                Run(true);
                return;
            }

            // 설정이 그대로면 다시 구울 이유가 없다 (에디터를 켤 때마다 구우면 느리다).
            string diff = SettingsDiff(existing);
            if (diff == null) diff = CharacterDiff(existing);
            if (diff == null) return;

            Debug.Log($"[도트 글꼴] 다시 굽습니다: {diff}");
            Run(true);
        };
    }

    // ===== 쓰는 글자가 늘었는지 =====
    // 이 도구는 "게임에 실제로 쓰는 글자만" 굽는다. 그래서 대사나 UI 글자를 새로 쓰면
    // 그 글자가 아틀라스에 없어서 화면에서 빈칸으로 나온다 (크레딧에 "흑성게"를 넣었을 때
    // 실제로 '흑'이 빠졌다). 사람이 메뉴를 다시 눌러주길 기대할 수는 없으니 자동으로 본다.
    //
    // ===== 왜 EditorPrefs에 지문을 남기나 =====
    // "없는 글자가 있으면 굽는다"만으로 하면 무한 반복에 빠진다. TTF 자체에 없는 글자
    // (예: 한자, 희귀 기호)가 CSV에 하나라도 섞여 있으면 구운 뒤에도 계속 "없는 글자"로
    // 남아서, 에디터를 켤 때마다 2분씩 다시 굽게 된다.
    // 그래서 "이 글자 묶음으로는 이미 한 번 구워 봤다"는 지문을 남기고, 글자 묶음이
    // 실제로 바뀌었을 때만 다시 굽는다.
    private const string CharsSignatureKey = "2KH1.DotFont.CharsSignature";

    // 글자를 모으는 일은 CSV·스크립트·씬을 전부 읽는 것이라 가볍지 않다.
    // 글꼴 에셋보다 나중에 고쳐진 원본이 하나도 없으면 읽지 않고 넘어간다.
    private static string CharacterDiff(TMP_FontAsset asset)
    {
        string assetPath = $"{OutputDir}/{OutputName}.asset";
        if (!File.Exists(assetPath)) return null;

        if (!AnySourceNewerThan(File.GetLastWriteTimeUtc(assetPath))) return null;

        string characters = CollectCharacters(out _);
        string signature = Signature(characters);

        // 같은 글자 묶음으로 이미 구워 봤으면 더 할 수 있는 게 없다.
        if (EditorPrefs.GetString(CharsSignatureKey, "") == signature) return null;

        // 지금 에셋에 없는 글자를 센다.
        int missing = 0;
        char firstMissing = '\0';
        foreach (char c in characters)
        {
            if (asset.HasCharacter(c)) continue;
            if (missing == 0) firstMissing = c;
            missing++;
        }

        // 글자 묶음이 바뀌긴 했지만 빠진 글자가 없으면(글자가 지워지기만 한 경우 등)
        // 굽지 않고 지문만 갱신해 둔다.
        if (missing == 0)
        {
            EditorPrefs.SetString(CharsSignatureKey, signature);
            return null;
        }

        return $"아틀라스에 없는 글자 {missing}자 (예: '{firstMissing}')";
    }

    // 글자를 모으는 원본(CSV·스크립트·씬·라이선스 전문) 중에 기준 시각보다 나중에
    // 고쳐진 것이 하나라도 있는지 본다. 파일 목록과 수정 시각만 보므로 내용은 읽지 않는다.
    private static bool AnySourceNewerThan(System.DateTime utc)
    {
        foreach (string file in TextAssetFiles)
            if (File.Exists(file) && File.GetLastWriteTimeUtc(file) > utc) return true;

        foreach (string dir in CsvDirs)
            if (AnyFileNewerThan(dir, "*.csv", utc)) return true;

        return AnyFileNewerThan("Assets/Scripts", "*.cs", utc)
            || AnyFileNewerThan("Assets/Scenes", "*.unity", utc);
    }

    private static bool AnyFileNewerThan(string dir, string pattern, System.DateTime utc)
    {
        if (!Directory.Exists(dir)) return false;
        foreach (string file in Directory.GetFiles(dir, pattern, SearchOption.AllDirectories))
            if (File.GetLastWriteTimeUtc(file) > utc) return true;
        return false;
    }

    // 글자 묶음의 지문. string.GetHashCode()는 실행할 때마다 값이 달라질 수 있어
    // (런타임에 따라 해시에 난수를 섞는다) 저장해 두고 비교하는 용도로는 쓸 수 없다.
    // 그래서 값이 언제나 같은 FNV-1a로 직접 센다.
    private static string Signature(string characters)
    {
        uint hash = 2166136261u;
        foreach (char c in characters)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return $"{characters.Length}:{hash:x8}";
    }

    // 이미 구워진 에셋이 지금 설정과 다른 점을 알려준다. 같으면 null.
    private static string SettingsDiff(TMP_FontAsset asset)
    {
        if (asset.atlasPadding != AtlasPadding)
            return $"글자 여백 {asset.atlasPadding} -> {AtlasPadding}";

        if (asset.atlasRenderMode != RenderMode)
            return $"굽는 방식 {asset.atlasRenderMode} -> {RenderMode}";

        if (asset.faceInfo.pointSize != SamplingPointSize)
            return $"고유 크기 {asset.faceInfo.pointSize} -> {SamplingPointSize}";

        return null;
    }

    // silent: 자동 실행일 때는 확인 창을 띄우지 않는다 (에디터가 켜지는 중에 모달이 뜨면 막힌다).
    public static void Run(bool silent)
    {
        string ttfPath = FindTtf();
        if (ttfPath == null)
        {
            if (silent) return;
            EditorUtility.DisplayDialog(
                "도트 글꼴 TTF를 찾지 못했습니다",
                "Neo둥근모 TTF 파일을 아래 폴더 중 한 곳에 넣어 주세요.\n\n" +
                "  Assets/Fonts/\n\n" +
                "받는 곳: https://github.com/neodgm/neodgm/releases\n" +
                "(파일 이름에 neodunggeunmo 또는 neodgm 가 들어 있어야 찾습니다)",
                "확인");
            return;
        }

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (ttf == null)
        {
            Debug.LogError($"[도트 글꼴] '{ttfPath}' 를 Font 로 읽지 못했습니다. 임포트가 끝났는지 확인하세요.");
            return;
        }

        Debug.Log($"[도트 글꼴] TTF: {ttfPath}");

        // ----- 1. 쓸 글자 모으기 -----
        string characters = CollectCharacters(out string report);
        Debug.Log(report);

        if (characters.Length == 0)
        {
            Debug.LogError("[도트 글꼴] 구울 글자를 하나도 찾지 못했습니다. CSV와 씬 경로를 확인하세요.");
            return;
        }

        // ----- 2. 글꼴 에셋 만들기 -----
        // padding 0 + RASTER_HINTED: 안티에일리어싱 없이 점 그대로 굽는다.
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            ttf,
            SamplingPointSize,
            AtlasPadding,                       // 0이면 옆 글자가 묻어 들어온다 (위 주석 참고)
            RenderMode,
            AtlasWidth,
            AtlasHeight,
            AtlasPopulationMode.Dynamic,        // 글자를 채워 넣는 동안은 Dynamic이어야 한다
            true);                              // 한 장을 넘치면 아틀라스를 더 만든다

        if (fontAsset == null)
        {
            Debug.LogError("[도트 글꼴] 글꼴 에셋을 만들지 못했습니다.");
            return;
        }
        fontAsset.name = OutputName;

        // ----- 3. 글자 채우기 -----
        bool allAdded = fontAsset.TryAddCharacters(characters, out string missing);

        // 이 글자 묶음으로는 구워 봤다고 남겨 둔다. 이게 없으면, TTF에 아예 없는 글자가
        // CSV에 섞여 있을 때 "없는 글자가 있다"가 영원히 참이 되어 에디터를 켤 때마다
        // 다시 굽는다 (CharacterDiff 주석 참고).
        EditorPrefs.SetString(CharsSignatureKey, Signature(characters));

        // ----- 4. Static 으로 굳히기 -----
        // Static이면 게임 중에 글자를 더 굽지 않는다. 빌드에서 TTF 원본이 필요 없고,
        // 처음 나오는 글자에서 한 프레임 멈추는 일도 없어진다.
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        // ----- 5. 선명하게 보이도록 마무리 -----
        ApplyPixelPerfectSettings(fontAsset);

        // ----- 6. 저장 -----
        SaveAsset(fontAsset);

        // ----- 7. 결과 보고 -----
        var sb = new StringBuilder();
        sb.AppendLine($"[도트 글꼴] 완료: {OutputDir}/{OutputName}.asset");
        sb.AppendLine($"  구운 글자 수   : {characters.Length}자");
        sb.AppendLine($"  아틀라스       : {fontAsset.atlasTextures.Length}장 ({AtlasWidth}x{AtlasHeight})");
        sb.AppendLine($"  고유 크기      : {SamplingPointSize}px (화면에서는 {SamplingPointSize}, {SamplingPointSize * 2}, {SamplingPointSize * 3}px 로 써야 선명하다)");
        sb.AppendLine($"  빠진 글자      : {(allAdded || string.IsNullOrEmpty(missing) ? "없음" : $"{missing.Length}자")}");
        if (!string.IsNullOrEmpty(missing))
        {
            sb.AppendLine();
            sb.AppendLine("  ↓ 이 글자들은 이 글꼴에 모양이 없어서 □ 로 보입니다.");
            sb.AppendLine("    대사에 쓰이는 글자라면 다른 글꼴을 쓰거나 대사를 고쳐야 합니다.");
            sb.AppendLine($"    {missing}");
        }

        if (string.IsNullOrEmpty(missing)) Debug.Log(sb.ToString());
        else Debug.LogWarning(sb.ToString());

        if (silent) return;
        EditorUtility.DisplayDialog("도트 글꼴 에셋 만들기",
            $"{characters.Length}자를 구웠습니다.\n" +
            $"빠진 글자: {(string.IsNullOrEmpty(missing) ? "없음" : missing.Length + "자 (콘솔 확인)")}\n\n" +
            $"{OutputDir}/{OutputName}.asset",
            "확인");
    }

    [MenuItem("2KH1/글꼴/쓰는 글자 목록만 뽑기 (굽지 않음)", false, 11)]
    public static void DumpCharacterList()
    {
        string characters = CollectCharacters(out string report);
        Debug.Log(report);

        // Font Asset Creator 창의 Custom Character List 에 붙여 넣을 수 있는 파일.
        // 다른 도트 글꼴을 검토할 때 이 파일 하나로 커버리지를 비교할 수 있다.
        //
        // 저장 위치를 직접 고르게 하는 이유: 저장소 안에 두면 git에 잡히는 임시 파일이 된다.
        // (바탕화면 같은 저장소 밖에 저장하는 것을 권한다)
        string path = EditorUtility.SaveFilePanel(
            "쓰는 글자 목록 저장", "", "사용글자목록.txt", "txt");
        if (string.IsNullOrEmpty(path)) return;      // 취소

        // BOM을 넣는 이유: 메모장에서 열었을 때 한글이 깨지지 않게.
        File.WriteAllText(path, characters, new UTF8Encoding(true));
        Debug.Log($"[도트 글꼴] 글자 목록을 저장했습니다 ({characters.Length}자): {path}");
    }

    // ---------------------------------------------------------------------------------
    // 선명하게 보이게 하는 설정
    // ---------------------------------------------------------------------------------
    // 이 네 가지 중 하나라도 빠지면 도트 글꼴이 흐려진다.
    private static void ApplyPixelPerfectSettings(TMP_FontAsset fontAsset)
    {
        // (1) 아틀라스 텍스처를 점 단위로 확대한다.
        //     Bilinear(기본값)이면 2배로 키울 때 픽셀 사이를 섞어서 흐려진다.
        foreach (var tex in fontAsset.atlasTextures)
        {
            if (tex == null) continue;
            tex.filterMode = FilterMode.Point;
            tex.anisoLevel = 0;
            tex.Apply(false, false);
        }

        // (2) 셰이더를 Bitmap 으로. SDF 셰이더는 글자 경계를 부드럽게 만든다.
        var mat = fontAsset.material;
        if (mat != null)
        {
            var bitmap = Shader.Find("TextMeshPro/Bitmap");
            if (bitmap != null) mat.shader = bitmap;
            else Debug.LogWarning("[도트 글꼴] 'TextMeshPro/Bitmap' 셰이더를 찾지 못했습니다. " +
                                  "글자가 약간 흐려 보일 수 있습니다.");
        }

        // (3) 가짜 굵게(faux bold) 보정을 끈다.
        //     TMP는 Bold가 걸린 글자를 "윤곽을 부풀려서" 굵게 만든다. 도트 글꼴에서는
        //     그 부풀림이 점을 한 칸씩 번지게 해서 글자가 두 겹으로 보인다.
        //     0으로 두면 Bold 글자도 보통 글자와 같은 모양으로, 선명하게 나온다.
        //     (대신 굵게가 눈에 띄지 않으므로, 강조는 색으로 구분한다 - UITheme 참고)
        fontAsset.boldStyle = 0;
        fontAsset.boldSpacing = 0f;
        fontAsset.normalStyle = 0;

        // (4) 대체 글꼴 목록을 비운다.
        //     여기에 다른(매끄러운) 글꼴이 섞이면 일부 글자만 흐리게 나와서 더 어색하다.
        //     빠진 글자는 위 보고에서 확인하고 대사를 고치는 쪽이 낫다.
        if (fontAsset.fallbackFontAssetTable != null) fontAsset.fallbackFontAssetTable.Clear();

        fontAsset.ReadFontAssetDefinition();
    }

    // ---------------------------------------------------------------------------------
    // 저장
    // ---------------------------------------------------------------------------------
    private static void SaveAsset(TMP_FontAsset fontAsset)
    {
        if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);
        string path = $"{OutputDir}/{OutputName}.asset";

        // 이미 있으면 지우고 새로 만든다.
        // 덮어쓰기를 하면 예전 아틀라스 텍스처가 하위 에셋으로 남아 파일만 커진다.
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.CreateAsset(fontAsset, path);

        // 머티리얼과 아틀라스 텍스처는 글꼴 에셋 안에 하위 에셋으로 넣는다.
        // 따로 두면 팀원이 글꼴만 받고 텍스처를 빠뜨려 글자가 안 보이는 일이 생긴다.
        if (fontAsset.material != null)
        {
            fontAsset.material.name = OutputName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        foreach (var tex in fontAsset.atlasTextures)
        {
            if (tex == null) continue;
            AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.SetDirty(fontAsset);
    }

    // ---------------------------------------------------------------------------------
    // TTF 찾기
    // ---------------------------------------------------------------------------------
    private static string FindTtf()
    {
        foreach (string dir in SearchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".ttf" && ext != ".otf") continue;

                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
                foreach (string hint in NameHints)
                {
                    if (name.Contains(hint)) return file.Replace('\\', '/');
                }
            }
        }
        return null;
    }

    // ---------------------------------------------------------------------------------
    // 쓰는 글자 모으기
    // ---------------------------------------------------------------------------------
    private static string CollectCharacters(out string report)
    {
        var chars = new HashSet<char>();
        var sb = new StringBuilder();
        sb.AppendLine("[도트 글꼴] 쓰는 글자 모으기");

        // ----- 기본 묶음 -----
        AddAll(chars, BaseAscii);
        AddAll(chars, BaseSymbols);
        AddAll(chars, RequiredHangul);
        sb.AppendLine($"  기본 묶음        : {chars.Count}자 (ASCII + 기호 + 한글 판정용 '가')");

        // ----- CSV: 대사와 항목 이름이 전부 여기 있다 -----
        int before = chars.Count;
        int csvFiles = 0;
        foreach (string dir in CsvDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.csv", SearchOption.AllDirectories))
            {
                AddAll(chars, SafeRead(file));
                csvFiles++;
            }
        }
        sb.AppendLine($"  CSV {csvFiles,3}개      : +{chars.Count - before}자");
        if (csvFiles == 0)
        {
            sb.AppendLine("    ! CSV를 하나도 찾지 못했습니다. 드라이브 공유분을 받았는지 확인하세요.");
        }

        // ----- 화면에 그대로 뜨는 텍스트 파일 (글꼴 라이선스 전문 등) -----
        before = chars.Count;
        foreach (string file in TextAssetFiles)
        {
            if (!File.Exists(file))
            {
                sb.AppendLine($"    ! 텍스트 파일이 없습니다: {file}");
                continue;
            }
            AddAll(chars, SafeRead(file));
        }
        sb.AppendLine($"  텍스트 파일 {TextAssetFiles.Length,2}개 : +{chars.Count - before}자");

        // ----- 스크립트의 문자열: 코드로 만든 UI 글자 -----
        // 주석의 한글은 화면에 안 나오므로 넣지 않는다 ("..." 안쪽만 가져온다).
        before = chars.Count;
        int csFiles = 0;
        if (Directory.Exists("Assets/Scripts"))
        {
            foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in StringLiteral.Matches(SafeRead(file)))
                {
                    AddAll(chars, m.Groups[1].Value);
                }
                csFiles++;
            }
        }
        sb.AppendLine($"  스크립트 {csFiles,3}개  : +{chars.Count - before}자 (문자열만)");

        // ----- 씬에 손으로 배치해 둔 글자 -----
        before = chars.Count;
        int sceneFiles = 0;
        if (Directory.Exists("Assets/Scenes"))
        {
            foreach (string file in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
            {
                foreach (Match m in SceneText.Matches(SafeRead(file)))
                {
                    AddAll(chars, m.Groups[1].Value);
                }
                sceneFiles++;
            }
        }
        sb.AppendLine($"  씬 {sceneFiles,3}개       : +{chars.Count - before}자 (m_text)");

        // ----- 정리 -----
        // 줄바꿈/탭 같은 제어문자는 글자가 아니므로 뺀다.
        var final = chars.Where(c => c >= ' ' && c != '\u007F').OrderBy(c => c).ToArray();

        int hangul = final.Count(c => c >= 0xAC00 && c <= 0xD7A3);
        sb.AppendLine($"  ---------------------------------------");
        sb.AppendLine($"  합계             : {final.Length}자 (한글 완성형 {hangul}자)");

        report = sb.ToString();
        return new string(final);
    }

    // "..." 안쪽. \" 같은 이스케이프를 건너뛰도록 했다.
    private static readonly Regex StringLiteral = new Regex("\"((?:\\\\.|[^\"\\\\\\n])*)\"", RegexOptions.Compiled);

    // 씬 파일(YAML)의 m_text 한 줄.
    private static readonly Regex SceneText = new Regex(@"m_text:\s*(.*)", RegexOptions.Compiled);

    private static void AddAll(HashSet<char> set, string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        foreach (char c in s) set.Add(c);
    }

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[도트 글꼴] '{path}' 를 읽지 못해 건너뜁니다: {e.Message}");
            return "";
        }
    }
}
