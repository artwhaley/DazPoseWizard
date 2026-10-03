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
    /// <summary>Creates the five starter Magic styles, paired presets, catalog, and scene selector binding.</summary>
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

                var spells = new List<PerformerSpell>();
                var auras = new List<PerformerAura>();
                int familyId = 0;
                foreach (StarterStyle definition in CreateStarterStyles())
                {
                    string graphFolder = EffectRoot + "/" + definition.Key;
                    EnsureFolder(graphFolder);
                    string graphPath = graphFolder + "/" + definition.Key + "_Magic.vfx";
                    VisualEffectAsset graph = PerformerMagicVfxGraphBuilder.GetOrCreate(graphPath);
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
                    Debug.Log("Magic assets generated. Added the five starter pairs to DefaultMagicCatalog and assigned it to "
                        + controlsWired + " loaded FirstPerformanceVoid control component(s). Existing scene objects and camera were left untouched.", catalog);
                }
                else
                {
                    Debug.Log("Magic assets generated at " + GeneratedRoot
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
            else if (!style.IsReady(out _))
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
                    Primary = new Color(4.0f, 3.2f, 0.75f, 1f), Secondary = new Color(3.0f, 0.7f, 0.08f, 1f),
                    Accent = new Color(2.3f, 0.12f, 0.015f, 1f), Smoke = new Color(0.11f, 0.075f, 0.09f, 0.34f),
                    Duration = 2.05f, Burst = 520, SpellLifetime = 2.05f, AuraFadeIn = 0.3f, AuraFadeOut = 0.32f,
                    AuraRate = 150f, AuraLifetime = 2.25f, Intensity = 1.6f, Size = 0.04f, Rise = 0.85f,
                    Swirl = 0.9f, Turbulence = 0.28f, Pulse = 1.25f
                },
                new StarterStyle
                {
                    Key = "RiftBloom", Label = "Rift Bloom",
                    Primary = new Color(4.0f, 3.2f, 4.2f, 1f), Secondary = new Color(3.0f, 0.035f, 1.8f, 1f),
                    Accent = new Color(0.65f, 0.22f, 3.0f, 1f), Smoke = new Color(0.13f, 0.045f, 0.18f, 0.22f),
                    Duration = 1.7f, Burst = 460, SpellLifetime = 1.7f, AuraFadeIn = 0.28f, AuraFadeOut = 0.3f,
                    AuraRate = 175f, AuraLifetime = 2.1f, Intensity = 1.65f, Size = 0.022f, Rise = 0.48f,
                    Swirl = 1.45f, Turbulence = 0.24f, Pulse = 1.1f
                },
                new StarterStyle
                {
                    Key = "ArcCyan", Label = "Arc Cyan",
                    Primary = new Color(4.2f, 5.0f, 5.2f, 1f), Secondary = new Color(0.12f, 2.2f, 4.4f, 1f),
                    Accent = new Color(0.08f, 0.52f, 2.8f, 1f), Smoke = new Color(0.025f, 0.12f, 0.21f, 0.15f),
                    Duration = 1.4f, Burst = 340, SpellLifetime = 1.4f, AuraFadeIn = 0.2f, AuraFadeOut = 0.24f,
                    AuraRate = 205f, AuraLifetime = 1.25f, Intensity = 1.55f, Size = 0.018f, Rise = 0.2f,
                    Swirl = 1.2f, Turbulence = 0.38f, Pulse = 2.4f
                },
                new StarterStyle
                {
                    Key = "VerdantPulse", Label = "Verdant Pulse",
                    Primary = new Color(1.15f, 3.7f, 1.2f, 1f), Secondary = new Color(0.08f, 1.8f, 0.5f, 1f),
                    Accent = new Color(2.0f, 4.2f, 0.38f, 1f), Smoke = new Color(0.04f, 0.14f, 0.065f, 0.2f),
                    Duration = 1.85f, Burst = 440, SpellLifetime = 1.85f, AuraFadeIn = 0.34f, AuraFadeOut = 0.36f,
                    AuraRate = 160f, AuraLifetime = 2.15f, Intensity = 1.35f, Size = 0.03f, Rise = 0.54f,
                    Swirl = 0.85f, Turbulence = 0.22f, Pulse = 1.2f
                },
                new StarterStyle
                {
                    Key = "VioletSerenity", Label = "Violet Serenity",
                    Primary = new Color(2.7f, 2.2f, 4.0f, 1f), Secondary = new Color(1.15f, 0.48f, 2.8f, 1f),
                    Accent = new Color(3.2f, 2.75f, 4.2f, 1f), Smoke = new Color(0.12f, 0.09f, 0.2f, 0.18f),
                    Duration = 2.2f, Burst = 360, SpellLifetime = 2.2f, AuraFadeIn = 0.42f, AuraFadeOut = 0.45f,
                    AuraRate = 125f, AuraLifetime = 2.8f, Intensity = 1.2f, Size = 0.03f, Rise = 0.28f,
                    Swirl = 0.65f, Turbulence = 0.12f, Pulse = 0.65f
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
