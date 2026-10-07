using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
// UI 그림 불러오기 도우미 (Resources)
// =====================================================================================
// Figma에서 내보낸 UI 그림은 2배 크기(@2x)로 Assets/Resources/Illusts/UI/ 아래에 둔다.
// 버튼/이름표처럼 크기가 그림과 달라지는 것은 모서리가 찌그러지지 않게 "가장자리를 유지한 채"
// 늘려 써야(9-slice) 하는데, 그 가장자리 정보를 임포트 설정에 일일이 넣지 않아도 되도록
// 읽어온 그림에 가장자리 정보를 붙인 사본을 만들어 준다.
// 그림이 없으면(드라이브에서 아직 안 받은 사람) null을 돌려주므로 부르는 쪽에서 색 도형으로 대신한다.
public static class UISpriteUtil
{
    private static readonly Dictionary<string, Sprite> plainCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> slicedCache = new Dictionary<string, Sprite>();

    public static Sprite Load(string path)
    {
        if (plainCache.TryGetValue(path, out var cached) && cached != null) return cached;
        var sprite = Resources.Load<Sprite>(path);
        plainCache[path] = sprite;
        return sprite;
    }

    // border: 그림 픽셀 기준 가장자리 두께 (@2x 그림이면 화면 두께의 2배).
    public static Sprite LoadSliced(string path, float border)
    {
        string key = path + "#" + border;
        if (slicedCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var src = Load(path);
        Sprite sliced = src == null ? null : Sprite.Create(src.texture, src.rect, new Vector2(0.5f, 0.5f), src.pixelsPerUnit,
                                                           0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        slicedCache[key] = sliced;
        return sliced;
    }

    // 가장자리 두께가 변마다 다를 때 (x=왼쪽, y=아래, z=오른쪽, w=위 - 그림 픽셀 기준).
    // 예: 세이브 슬롯은 왼쪽 번호 칸(구분선 포함)을 통째로 유지해야 해서 왼쪽만 두껍다.
    public static Sprite LoadSliced(string path, Vector4 border)
    {
        string key = path + "#" + border;
        if (slicedCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var src = Load(path);
        Sprite sliced = src == null ? null : Sprite.Create(src.texture, src.rect, new Vector2(0.5f, 0.5f), src.pixelsPerUnit,
                                                           0, SpriteMeshType.FullRect, border);
        slicedCache[key] = sliced;
        return sliced;
    }

    public static bool ApplySliced(Image img, string path, Vector4 border)
    {
        var sprite = LoadSliced(path, border);
        if (sprite == null) return false;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2f;
        img.color = Color.white;
        return true;
    }

    // @2x 그림을 가장자리 유지(Sliced)로 Image에 넣는다. 그림이 없으면 false.
    public static bool ApplySliced(Image img, string path, float border)
    {
        var sprite = LoadSliced(path, border);
        if (sprite == null) return false;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2f;
        img.color = Color.white;
        return true;
    }
}
