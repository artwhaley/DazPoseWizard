using System;
using System.Collections.Generic;
using System.IO;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Refreshes the five project-owned Magic graphs, art presets, catalog, and scene selector binding.</summary>
    internal static class PerformerMagicAcceptanceSetup
    {
        private const string EffectRoot = "Assets/DazPose/Effects/Magic";
        private const string GeneratedRoot = "Assets/DazPose/Generated/Magic";

        private sealed class StarterStyle
        {
            public string Key;
            public string Label;
            public Color Primary;
            public Color Secondary;
            public Color Accent;
            public Color Smoke;
            public float Duration;
            public int Burst;
            public float SpellLifetime;
            public float AuraFadeIn;
            public float AuraFadeOut;
            public float AuraRate;
            public float AuraLifetime;
            public float Intensity;
            public float Size;
            public float Rise;
            public float Swirl;
            public float Turbulence;
            public float Pulse;
        }

        [MenuItem("Tools/DAZ Pose/Magic/Generate Starter Magic Assets")]
        public static void GenerateStarterMagicAssets()
        {
            try
            {
                EnsureFolder(EffectRoot);
                EnsureFolder(EffectRoot + "/Shared");
                EnsureFolder(GeneratedRoot);
                EnsureFolder(GeneratedRoot + "/Styles");
                EnsureFolder(GeneratedRoot + "/Spells");
                EnsureFolder(GeneratedRoot + "/Auras");

                string hlslPath = EffectRoot + "/Shared/PerformerMagic.hlsl";
                if (!File.Exists(hlslPath))
                    throw new InvalidOperationException("Magic shader source is missing at " + hlslPath + ".");
                AssetDatabase.ImportAsset(hlslPath, ImportAssetOptions.ForceSynchronousImport);
                PerformerMagicTextureBuilder.Generate();

                var spells = new List<PerformerSpell>();
                var auras = new List<PerformerAura>();
                int familyId = 0;
                foreach (StarterStyle definition in CreateStarterStyles())
                {
                    string graphFolder = EffectRoot + "/" + definition.Key;
                    EnsureFolder(graphFolder);
                    string graphPath = graphFolder + "/" + definition.Key + "_Magic.vfx";
                    VisualEffectAsset graph = PerformerMagicVfxGraphBuilder.GetOrCreate(graphPath, familyId);
                    PerformerMagicStyle style = GetOrCreateStyle(definition, graph, familyId++);
                    PerformerSpell spell = GetOrCreateSpell(definition, style);
                    PerformerAura aura = GetOrCreateAura(definition, style);
                    spells.Add(spell);
                    auras.Add(aura);
                }

                PerformerMagicCatalog catalog = GetOrCreateCatalog(spells, auras);
                if (!catalog.IsReady(out string catalogReason))
                    throw new InvalidOperationException("Generated Magic Catalog is not ready: " + catalogReason);
                int controlsWired = AssignCatalogToLoadedControls(catalog);
                if (controlsWired > 0)
                {
                    Debug.Log("Magic art presets refreshed with three-second casts, fine motes and turbulent vapor. Updated the five starter pairs in DefaultMagicCatalog and assigned it to "
                        + controlsWired + " loaded FirstPerformanceVoid control component(s). Existing scene objects and camera were left untouched.", catalog);
                }
                else
                {
                    Debug.Log("Magic art presets refreshed with three-second casts at " + GeneratedRoot
                        + ". No FirstPerformanceVoid controls are loaded; open the existing scene and assign DefaultMagicCatalog "
                        + "to its FirstPerformanceVoidControls component.", catalog);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not generate the starter Magic assets: " + exception.Message + "\n" + exception.StackTrace);
            }
        }

        private static PerformerMagicStyle GetOrCreateStyle(StarterStyle definition, VisualEffectAsset graph, int familyId)
        {
            string path = GeneratedRoot + "/Styles/Style_" + definition.Key + ".asset";
            PerformerMagicStyle style = AssetDatabase.LoadAssetAtPath<PerformerMagicStyle>(path);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<PerformerMagicStyle>();
                style.name = "Style " + definition.Label;
                Configure(style, definition, graph, familyId);
                AssetDatabase.CreateAsset(style, path);
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssetIfDirty(style);
            }
            else
            {
                Configure(style, definition, graph, familyId);
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssetIfDirty(style);
            }
            return style;
        }

        private static void Configure(PerformerMagicStyle style, StarterStyle definition, VisualEffectAsset graph, int familyId)
        {
            style.ConfigureInEditor(definition.Label, graph, familyId,
                definition.Primary, definition.Secondary, definition.Accent, definition.Smoke,
                1f, 0.12f, definition.Duration, definition.Burst, definition.SpellLifetime,
                definition.AuraFadeIn, definition.AuraFadeOut, definition.AuraRate, definition.AuraLifetime,
                definition.Intensity, definition.Size, definition.Rise, definition.Swirl,
                definition.Turbulence, definition.Pulse);
        }

        private static PerformerSpell GetOrCreateSpell(StarterStyle definition, PerformerMagicStyle style)
        {
            string path = GeneratedRoot + "/Spells/Spell_" + definition.Key + ".asset";
            PerformerSpell spell = AssetDatabase.LoadAssetAtPath<PerformerSpell>(path);
            if (spell == null)
            {
                spell = ScriptableObject.CreateInstance<PerformerSpell>();
                spell.name = "Spell " + definition.Label;
                spell.ConfigureInEditor(style);
                AssetDatabase.CreateAsset(spell, path);
                AssetDatabase.SaveAssetIfDirty(spell);
            }
            else if (!ReferenceEquals(spell.Style, style) || !spell.IsReady(out _))
            {
                spell.ConfigureInEditor(style);
                EditorUtility.SetDirty(spell);
                AssetDatabase.SaveAssetIfDirty(spell);
            }
            return spell;
        }

        private static PerformerAura GetOrCreateAura(StarterStyle definition, PerformerMagicStyle style)
        {
            string path = GeneratedRoot + "/Auras/Aura_" + definition.Key + ".asset";
            PerformerAura aura = AssetDatabase.LoadAssetAtPath<PerformerAura>(path);
            if (aura == null)
            {
                aura = ScriptableObject.CreateInstance<PerformerAura>();
                aura.name = "Aura " + definition.Label;
                aura.ConfigureInEditor(style);
                AssetDatabase.CreateAsset(aura, path);
                AssetDatabase.SaveAssetIfDirty(aura);
            }
            else if (!ReferenceEquals(aura.Style, style) || !aura.IsReady(out _))
            {
                aura.ConfigureInEditor(style);
                EditorUtility.SetDirty(aura);
                AssetDatabase.SaveAssetIfDirty(aura);
            }
            return aura;
        }

        private static PerformerMagicCatalog GetOrCreateCatalog(IList<PerformerSpell> spells, IList<PerformerAura> auras)
        {
            string path = GeneratedRoot + "/DefaultMagicCatalog.asset";
            PerformerMagicCatalog catalog = AssetDatabase.LoadAssetAtPath<PerformerMagicCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PerformerMagicCatalog>();
                catalog.name = "Default Magic Catalog";
                AssetDatabase.CreateAsset(catalog, path);
            }
            catalog.ConfigureInEditor(spells, auras);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return catalog;
        }

        private static int AssignCatalogToLoadedControls(PerformerMagicCatalog catalog)
        {
            int assigned = 0;
            FirstPerformanceVoidControls[] controls = Resources.FindObjectsOfTypeAll<FirstPerformanceVoidControls>();
            foreach (FirstPerformanceVoidControls item in controls)
            {
                if (item == null || !item.gameObject.scene.IsValid() || !item.gameObject.scene.isLoaded) continue;
                if (!string.Equals(Path.GetFileNameWithoutExtension(item.gameObject.scene.path),
                    "FirstPerformanceVoid", StringComparison.OrdinalIgnoreCase)) continue;
                Undo.RecordObject(item, "Assign Magic Catalog");
                item.ConfigureMagicCatalog(catalog);
                EditorUtility.SetDirty(item);
                EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
                assigned++;
            }
            return assigned;
        }

        private static StarterStyle[] CreateStarterStyles()
        {
            return new[]
            {
                new StarterStyle
                {
                    Key = "Emberfire", Label = "Emberfire",
                    Primary = new Color(3.2f, 2.7f, 1.8f, 1f), Secondary = new Color(1.8f, 0.65f, 0.12f, 1f),
                    Accent = new Color(0.8f, 0.16f, 0.025f, 1f), Smoke = new Color(0.12f, 0.095f, 0.08f, 0.38f),
                    Duration = 3f, Burst = 768, SpellLifetime = 3.15f, AuraFadeIn = 0.6f, AuraFadeOut = 0.8f,
                    AuraRate = 170f, AuraLifetime = 2.8f, Intensity = 1.15f, Size = 0.026f, Rise = 1.0f,
                    Swirl = 0.40f, Turbulence = 0.55f, Pulse = 0.9f
                },
                new StarterStyle
                {
                    Key = "RiftBloom", Label = "Rift Bloom",
                    Primary = new Color(2.8f, 2.3f, 2.7f, 1f), Secondary = new Color(1.45f, 0.35f, 0.95f, 1f),
                    Accent = new Color(0.42f, 0.18f, 0.85f, 1f), Smoke = new Color(0.11f, 0.07f, 0.14f, 0.30f),
                    Duration = 3f, Burst = 768, SpellLifetime = 3.15f, AuraFadeIn = 0.7f, AuraFadeOut = 0.9f,
                    AuraRate = 175f, AuraLifetime = 2.8f, Intensity = 1.1f, Size = 0.024f, Rise = 0.50f,
                    Swirl = 0.85f, Turbulence = 0.60f, Pulse = 0.8f
                },
                new StarterStyle
                {
                    Key = "ArcCyan", Label = "Arc Cyan",
                    Primary = new Color(2.6f, 3.0f, 3.3f, 1f), Secondary = new Color(0.20f, 1.25f, 1.8f, 1f),
                    Accent = new Color(0.12f, 0.35f, 0.80f, 1f), Smoke = new Color(0.06f, 0.10f, 0.14f, 0.25f),
                    Duration = 3f, Burst = 768, SpellLifetime = 3.15f, AuraFadeIn = 0.5f, AuraFadeOut = 0.8f,
                    AuraRate = 185f, AuraLifetime = 2.3f, Intensity = 1.1f, Size = 0.022f, Rise = 0.45f,
                    Swirl = 0.55f, Turbulence = 0.65f, Pulse = 1.1f
                },
                new StarterStyle
                {
                    Key = "VerdantPulse", Label = "Verdant Pulse",
                    Primary = new Color(2.1f, 2.7f, 2.15f, 1f), Secondary = new Color(0.30f, 1.05f, 0.45f, 1f),
                    Accent = new Color(0.65f, 1.20f, 0.40f, 1f), Smoke = new Color(0.065f, 0.11f, 0.08f, 0.27f),
                    Duration = 3f, Burst = 640, SpellLifetime = 3.15f, AuraFadeIn = 0.8f, AuraFadeOut = 1f,
                    AuraRate = 145f, AuraLifetime = 3.0f, Intensity = 1.05f, Size = 0.023f, Rise = 0.60f,
                    Swirl = 0.40f, Turbulence = 0.48f, Pulse = 0.65f
                },
                new StarterStyle
                {
                    Key = "VioletSerenity", Label = "Violet Serenity",
                    Primary = new Color(2.35f, 2.2f, 2.8f, 1f), Secondary = new Color(0.60f, 0.45f, 1.05f, 1f),
                    Accent = new Color(0.90f, 0.65f, 1.15f, 1f), Smoke = new Color(0.09f, 0.08f, 0.13f, 0.30f),
                    Duration = 3f, Burst = 512, SpellLifetime = 3.15f, AuraFadeIn = 1f, AuraFadeOut = 1f,
                    AuraRate = 115f, AuraLifetime = 3.6f, Intensity = 1f, Size = 0.025f, Rise = 0.18f,
                    Swirl = 0.25f, Turbulence = 0.38f, Pulse = 0.45f
                }
            };
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new InvalidOperationException("Invalid project asset folder path: " + path);
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
