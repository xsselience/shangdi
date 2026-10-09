using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteFactory : MonoBehaviour
{
    static Sprite _disc, _square;

    public static Sprite WhiteDisc
    {
        get { if (_disc == null) _disc = Build(64, true); return _disc; }
    }

    public static Sprite WhiteSquare
    {
        get { if (_square == null) _square = Build(64, false); return _square; }
    }

    // 生成纯白图，靠 SpriteRenderer.color 染色。PPU = size，所以刚好 1 个世界单位
    static Sprite Build(int size, bool disc)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float r = size * 0.5f - 1f;
        var px = new Color32[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool inside;
                if (disc)
                {
                    float dx = x - size * 0.5f + 0.5f;
                    float dy = y - size * 0.5f + 0.5f;
                    inside = dx * dx + dy * dy <= r * r;
                }
                else inside = true;

                px[y * size + x] = inside ? new Color32(255, 255, 255, 255)
                                          : new Color32(255, 255, 255, 0);
            }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
