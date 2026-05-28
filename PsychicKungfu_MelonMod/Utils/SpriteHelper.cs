using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/*
namespace PsychicKungfu_MelonMod.Utils
{
    public static class SpriteHelper
    {
        private const string DumpSubfolder = "Sprite/Dump";
        private const string OverrideSubfolder = "Sprite/Override";

        private static string PluginDir =>
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static string DumpRoot => Path.Combine(PluginDir, DumpSubfolder);
        private static string OverrideRoot => Path.Combine(PluginDir, OverrideSubfolder);

        private static readonly Dictionary<string, Sprite> _overrideCache =
            new Dictionary<string, Sprite>();

        // ─────────────────────────────────────────────────────────────────
        //  Path helpers
        // ─────────────────────────────────────────────────────────────────

        // "Atlas/Skill" → "Atlas_Skill"
        private static string AtlasToFolder(string atlasPath) =>
            atlasPath.Replace('/', '_').Replace('\\', '_');

        /// <summary>
        /// With atlas:    root/Atlas_Skill/Skill_0.png
        /// Without atlas: root/Skill_0.png
        /// </summary>
        private static string ResolvePath(string root, string spriteName, string atlasPath)
        {
            return string.IsNullOrEmpty(atlasPath)
                ? Path.Combine(root, spriteName + ".png")
                : Path.Combine(root, AtlasToFolder(atlasPath), spriteName + ".png");
        }

        private static string CacheKey(string spriteName, string atlasPath) =>
            string.IsNullOrEmpty(atlasPath) ? spriteName : AtlasToFolder(atlasPath) + "/" + spriteName;

        // ─────────────────────────────────────────────────────────────────
        //  DUMP
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Saves a sprite as PNG.
        /// <para>With atlas:    Sprite/Dump/Atlas_Skill/Skill_0.png</para>
        /// <para>Without atlas: Sprite/Dump/Skill_0.png</para>
        /// </summary>
        public static void DumpSprite(Sprite sprite, string spriteName,
                               string atlasPath = null, bool overwriteExisting = false)
        {
            if (sprite == null) return;

            string filePath = ResolvePath(DumpRoot, spriteName, atlasPath);
            if (!overwriteExisting && File.Exists(filePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            Texture2D readable = ExtractSpriteTexture(sprite);
            if (readable == null)
            {
                MelonLogger.Warning($"[Sprite] Could not read texture for {spriteName}");
                return;
            }

            try
            {
                // ✓ ImageConversion static method
                File.WriteAllBytes(filePath, ImageConversion.EncodeToPNG(readable));
                MelonLogger.Msg($"[Sprite] Dumped → {filePath}");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[Sprite] Failed to write {filePath}: {e.Message}");
            }
            finally
            {
                if (readable != sprite.texture)
                    UnityEngine.Object.Destroy(readable);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  LOAD OVERRIDE
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tries to load an override PNG, returns fallback if none exists.
        /// <para>With atlas:    Sprite/Override/Atlas_Skill/Skill_0.png</para>
        /// <para>Without atlas: Sprite/Override/Skill_0.png</para>
        /// </summary>
        public static Sprite LoadOverrideSprite(string spriteName, Sprite fallback,
                                         string atlasPath = null)
        {
            string key = CacheKey(spriteName, atlasPath);

            if (_overrideCache.TryGetValue(key, out Sprite cached))
                return cached ?? fallback;

            string filePath = ResolvePath(OverrideRoot, spriteName, atlasPath);

            if (!File.Exists(filePath))
            {
                _overrideCache[key] = null;
                return fallback;
            }

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            if (fallback?.texture != null)
            {
                tex.filterMode = fallback.texture.filterMode;
                tex.wrapMode = fallback.texture.wrapMode;
            }

            // ✓ ImageConversion static method
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(filePath)))
            {
                MelonLogger.Warning($"[Sprite] Failed to decode PNG: {filePath}");
                UnityEngine.Object.Destroy(tex);
                _overrideCache[key] = null;
                return fallback;
            }

            tex.Apply();

            Sprite overrideSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                GetNormalizedPivot(fallback),
                fallback?.pixelsPerUnit ?? 100f);

            _overrideCache[key] = overrideSprite;
            MelonLogger.Msg($"[Sprite] Loaded override ← {filePath}");
            return overrideSprite;
        }

        /// <summary>
        /// Clears the override cache so files are re-read from disk on next call.
        /// </summary>
        public static void ClearOverrideCache() => _overrideCache.Clear();

        // ─────────────────────────────────────────────────────────────────
        //  PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────

        private static Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            Texture2D source = sprite.texture;
            Rect rect = sprite.textureRect;

            // ✓ 4-argument overload
            RenderTexture rt = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.Default);

            Graphics.Blit(source, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }

        private static Vector2 GetNormalizedPivot(Sprite sprite)
        {
            if (sprite == null) return new Vector2(0.5f, 0.5f);

            Rect r = sprite.textureRect;
            if (r.width == 0f || r.height == 0f) return new Vector2(0.5f, 0.5f);

            return new Vector2(sprite.pivot.x / r.width, sprite.pivot.y / r.height);
        }
    }
}
*/