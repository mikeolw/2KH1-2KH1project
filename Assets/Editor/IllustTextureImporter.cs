using UnityEditor;
using UnityEngine;

// =====================================================================================
// Illusts 폴더에 들어오는 PNG의 임포트 설정을 자동으로 맞춰주는 에디터 전용 스크립트
// =====================================================================================
// ===== 이게 왜 필요한가? =====
// 유니티는 그림 파일을 프로젝트에 넣을 때 "이걸 어떻게 쓸 건지"(Sprite인지 일반 텍스처인지,
// 픽셀을 코드에서 읽을 수 있게 할 건지 등) 설정을 함께 저장하는데, 그 설정은 그림 옆에 생기는
// .meta 파일에 들어간다.
//
// 그런데 우리 프로젝트는 Assets/Resources/ 폴더 전체가 .gitignore 대상이라 .meta도 git으로
// 공유되지 않는다. 즉 팀원마다 드라이브에서 PNG를 받아 넣을 때마다 유니티가 "기본 설정"으로
// 새로 임포트해버린다. 기본 설정에는 아래 두 가지가 빠져 있어서 문제가 생긴다:
//
//   1) Read/Write Enabled 가 꺼져 있음
//      -> 투명한 부분은 클릭이 통과하고 그림이 그려진 부분만 클릭되게 하는 기능
//         (Image.alphaHitTestMinimumThreshold)이 동작하지 않는다. 조사 오브젝트가
//         네모난 판때기처럼 클릭되어 버린다.
//   2) Mesh Type 이 Tight 로 되어 있음
//      -> 위 기능이 알파값을 제대로 읽지 못해 판정이 어긋난다. Full Rect 여야 한다.
//
// 이 스크립트는 유니티가 그림을 임포트하기 "직전"에 자동으로 끼어들어서 위 설정을 강제로
// 맞춰준다. 스크립트 자체는 Assets/Scripts 바깥의 Assets/Editor/ 에 있고 git으로 공유되므로,
// 누가 어떤 PC에서 그림을 넣든 항상 같은 설정이 적용된다. (팀원이 인스펙터에서 체크박스를
// 일일이 켜줄 필요가 전혀 없다.)
//
// ===== 동작 범위 =====
// Assets/Resources/Illusts/ 아래에 있는 그림에만 적용된다. TextMesh Pro 예제 이미지 등
// 다른 그림은 건드리지 않는다.
//
// ===== 이미 넣어둔 그림에도 적용하려면 =====
// 이 스크립트는 "새로 임포트될 때" 동작하므로, 스크립트를 추가하기 전에 이미 들어와 있던
// 그림에는 아직 적용되지 않았다. 유니티 상단 메뉴의
//   [2KH1] -> [Illusts 폴더 그림 임포트 설정 다시 적용]
// 을 한 번 눌러주면 폴더 안의 모든 그림을 다시 임포트하면서 설정을 맞춘다.
// (그림 개수가 140장 정도라 몇 초면 끝난다.)
public class IllustTextureImporter : AssetPostprocessor
{
    // 이 경로로 시작하는 그림만 손댄다.
    private const string TargetFolder = "Assets/Resources/Illusts/";

    // 유니티가 텍스처를 임포트하기 직전에 자동으로 불러주는 함수 (이름이 정해져 있음).
    // assetPath에는 지금 임포트 중인 파일의 경로가 들어온다.
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(TargetFolder)) return;

        var importer = (TextureImporter)assetImporter;

        // 이미 임포트된 적 있는 그림은 사용자가 인스펙터에서 손수 바꾼 설정을 존중해야 하므로
        // 최초 임포트 때만 전체 설정을 잡아준다... 라고 하고 싶지만, 우리 프로젝트는 팀원들이
        // 드라이브에서 그림을 계속 덮어쓰는 구조라 매번 강제로 맞춰주는 게 맞다.
        // (그래서 importer.importSettingsMissingSince 같은 조건 없이 항상 적용한다.)

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;

        // 투명 픽셀 클릭 무시 기능(alphaHitTestMinimumThreshold)이 알파값을 읽으려면
        // 이 두 가지가 반드시 필요하다.
        importer.isReadable = true;                              // Read/Write Enabled
        importer.spriteMeshType = SpriteMeshType.FullRect;       // Mesh Type = Full Rect

        // 알파 채널을 그대로 보존한다. (반투명/투명 배경 PNG가 많으므로)
        importer.alphaIsTransparency = true;

        // 1440x1080 캔버스에 1:1 픽셀로 얹을 것이므로 압축을 끄고 원본 화질을 유지한다.
        // 프로토타입 단계라 용량보다 화질/정확도가 중요하다. (나중에 최적화가 필요해지면
        // 이 줄을 TextureImporterCompression.Compressed 로 바꾸면 된다.)
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        // 스프라이트의 기준점을 가운데로. 배경/스탠딩 모두 코드에서 위치를 잡으므로
        // 기준점이 제각각이면 배치가 어긋난다.
        importer.spritePivot = new Vector2(0.5f, 0.5f);

        // pixelsPerUnit: UI(Canvas) 위에 올리는 그림이라 실제로는 영향이 적지만,
        // 100(유니티 기본값)으로 통일해두면 나중에 월드 스페이스로 옮겨도 크기가 일정하다.
        importer.spritePixelsPerUnit = 100f;
    }

    // ===== 이미 들어와 있는 그림에 일괄 적용하는 메뉴 =====
    // 유니티 상단 메뉴바에 [2KH1] 메뉴를 만들어준다.
    [MenuItem("2KH1/Illusts 폴더 그림 임포트 설정 다시 적용")]
    private static void ReimportAllIllusts()
    {
        // Illusts 폴더 안의 모든 텍스처(t:Texture)를 찾는다.
        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { "Assets/Resources/Illusts" });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "2KH1",
                "Assets/Resources/Illusts/ 폴더에서 그림을 찾지 못했습니다.\n" +
                "구글 드라이브에서 받은 그림을 이 폴더에 넣었는지 확인해 주세요.",
                "확인");
            return;
        }

        try
        {
            // 여러 파일을 한꺼번에 다시 임포트할 때는 Start/StopAssetEditing으로 감싸주면
            // 파일 하나 처리할 때마다 에디터가 갱신되지 않아서 훨씬 빠르다.
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                // 진행 상황 표시줄 (그림이 많을 때 멈춘 것처럼 보이지 않게)
                EditorUtility.DisplayProgressBar(
                    "Illusts 임포트 설정 적용 중",
                    path,
                    (float)i / guids.Length);

                // ImportAsset을 부르면 위의 OnPreprocessTexture가 다시 실행되어 설정이 적용된다.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
        finally
        {
            // 중간에 오류가 나도 반드시 원상복구되도록 finally에서 정리한다.
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[IllustTextureImporter] 그림 {guids.Length}장의 임포트 설정을 다시 적용했습니다.");
    }
}
