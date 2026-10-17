using Dealcraft.Core;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Dealcraft.Phone;

/// <summary>
/// The home screen icon, rasterised into a texture at load time from
/// <see cref="IconGlyph"/>. No sprite, texture or font file ships with the mod;
/// the only thing taken from the scene is the colour, so the mark sits in the
/// game's own palette.
/// </summary>
internal static class AppIconArt
{
    private const int Size = 128;

    /// <summary>
    /// The monogram in <paramref name="tint"/>. Edges are antialiased against
    /// transparency, so the mark reads cleanly whether or not the phone's icon
    /// prefab draws a plate behind it.
    /// </summary>
    public static Sprite CreateMonogram(Color tint)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "Dealcraft_AppIcon",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Il2CppStructArray<Color>(Size * Size);
        const float feather = 2f / Size;

        for (int y = 0; y < Size; y++)
        {
            float v = (y + 0.5f) / Size;
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size;
                float coverage = IconGlyph.Coverage(u, v, feather);
                pixels[(y * Size) + x] = new Color(tint.r, tint.g, tint.b, coverage * tint.a);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, Size, Size),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = "Dealcraft_AppIcon";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
