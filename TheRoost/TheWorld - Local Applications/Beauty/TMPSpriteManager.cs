using System;
using System.Reflection;
using SecretHistories.Constants.Modding;
using SecretHistories.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roost.World.Beauty
{
    public static class TMPSpriteManager
    {
        private static readonly FieldInfo DefaultSpriteAsset =
            typeof(TMP_Settings).GetFieldInvariant("m_defaultSpriteAsset");

        private static Sprite NamedSprite(Texture2D t, string name, float x, float y, float width, float height)
        {
            var s = Sprite.Create(
                t,
                new Rect(x, y, width, height),
                Vector2.zero
            );
            s.name = name;
            return s;
        }


        private struct PaintedSprite
        {
            public Sprite Sprite;
            public int X;
            public int Y;
            public int W;
            public int H;
        }

        private static (Texture2D spritesheet, List<PaintedSprite> sprites) PaintTexture(List<Sprite> sprites)
        {
            var paints = new List<PaintedSprite>();
            int x = 0, y = 0, nextY = 0, lastAdvance = 0, yOffset = 0;
            var totalW = 128 * Mathf.CeilToInt(Mathf.Sqrt(sprites.Count));
            foreach (var s in sprites.OrderByDescending(s => s.texture.height))
            {
                if (s.texture.width <= lastAdvance && y + yOffset + s.texture.height <= nextY)
                {
                    paints.Add(new PaintedSprite
                    {
                        X = x - lastAdvance,
                        Y = y + yOffset,
                        W = s.texture.width,
                        H = s.texture.height,
                        Sprite = s
                    });
                    yOffset += s.texture.height;
                }
                else
                {
                    if (x + s.texture.width > totalW)
                    {
                        x = 0;
                        y = nextY;
                        nextY = y;
                    }

                    paints.Add(new PaintedSprite
                    {
                        X = x,
                        Y = y,
                        W = s.texture.width,
                        H = s.texture.height,
                        Sprite = s
                    });
                    nextY = Mathf.Max(nextY, y + s.texture.height);
                    x += s.texture.width;
                    lastAdvance = s.texture.width;
                    yOffset = s.texture.height;
                }
            }

            var t = new Texture2D(
                totalW,
                nextY,
                TextureFormat.ARGB32,
                false
            );
            foreach (var s in paints)
            {
                t.SetPixels(
                    s.X, s.Y, s.W, s.H,
                    s.Sprite.texture.GetPixels(0, 0, s.W, s.H)
                );
            }

            t.Apply();
            return (t, paints);
        }

        private static readonly FieldInfo LoadedImages = typeof(ModManager).GetFieldInvariant("_images");

        private static List<Sprite> GetAllInFolder(string folder)
        {
            return ((Dictionary<string, Sprite>)LoadedImages.GetValue(Watchman.Get<ModManager>()))
                .Where(kv => kv.Key.StartsWith(folder)).Select(kv =>
                {
                    kv.Value.name = kv.Key.Replace(folder, "");
                    return kv.Value;
                }).ToList();
        }

        private static void UpdateSprites()
        {
            var spritesList = GetAllInFolder("images\\textsprites\\");

            var (t, sprites) = PaintTexture(spritesList);
            var sa = CreateSpriteAssetFromSelectedObject(t,
                sprites.Select(s => NamedSprite(t, s.Sprite.name, s.X, s.Y, s.W, s.H)));
            DefaultSpriteAsset.SetValue(TMP_Settings.instance, sa);
        }

        internal static void Enact()
        {
            AtTimeOfPower.CompendiumLoad.Schedule(UpdateSprites, PatchType.Postfix);
        }

        private static readonly FieldInfo
            TMPSpriteAssetVersion = typeof(TMP_SpriteAsset).GetFieldInvariant("m_Version"),
            TMPSpriteAssetSpriteCharacterTable = typeof(TMP_SpriteAsset).GetFieldInvariant("m_SpriteCharacterTable"),
            TMPSpriteAssetSpriteGlyphTable = typeof(TMP_SpriteAsset).GetFieldInvariant("m_SpriteGlyphTable");

        private static TMP_SpriteAsset CreateSpriteAssetFromSelectedObject(Texture sourceTex,
            IEnumerable<Sprite> sourceSprites)
        {
            var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();

            TMPSpriteAssetVersion.SetValue(spriteAsset, "1.1.0");
            spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(spriteAsset.name);
            spriteAsset.spriteSheet = sourceTex;
            spriteAsset.material = GetDefaultMaterial(spriteAsset);

            TMPSpriteAssetSpriteCharacterTable.SetValue(spriteAsset, new List<TMP_SpriteCharacter>());
            TMPSpriteAssetSpriteGlyphTable.SetValue(spriteAsset, new List<TMP_SpriteGlyph>());
            PopulateSpriteTables(sourceSprites, spriteAsset.spriteCharacterTable, spriteAsset.spriteGlyphTable);

            spriteAsset.UpdateLookupTables();
            return spriteAsset;
        }

        private static Material GetDefaultMaterial(TMP_SpriteAsset spriteAsset)
        {
            var material = new Material(Shader.Find("TextMeshPro/Sprite"));
            material.SetTexture(ShaderUtilities.ID_MainTex, spriteAsset.spriteSheet);
            material.name = spriteAsset.name + " Material";
            return material;
        }


        private static void PopulateSpriteTables(
            IEnumerable<Sprite> sourceSprites,
            List<TMP_SpriteCharacter> spriteCharacterTable,
            List<TMP_SpriteGlyph> spriteGlyphTable
        )
        {
            var sprites = sourceSprites
                .Where(x => x != null)
                .OrderByDescending(x => x.rect.y)
                .ThenBy(x => x.rect.x)
                .ToArray();
            for (var i = 0; i < sprites.Length; i++)
            {
                var sprite = sprites[i];

                var spriteGlyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    metrics = new GlyphMetrics(sprite.rect.width, sprite.rect.height, -sprite.pivot.x,
                        sprite.rect.height - sprite.pivot.y, sprite.rect.width),
                    glyphRect = new GlyphRect(sprite.rect),
                    scale = 1.5f,
                    sprite = sprite
                };

                spriteGlyphTable.Add(spriteGlyph);
                spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, spriteGlyph)
                {
                    name = sprite.name,
                    scale = 1f
                });
            }
        }
    }
}