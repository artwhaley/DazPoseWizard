using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Builds project-owned GPU particle graphs with the shared PerformerMagic input contract.</summary>
    internal static class PerformerMagicVfxGraphBuilder
    {
        private const string HlslPath = "Assets/DazPose/Effects/Magic/Shared/PerformerMagic.hlsl";

        private sealed class ParameterNode
        {
            public object Model;
            public int NodeId;
        }

        private static readonly BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static VisualEffectAsset GetOrCreate(string path)
        {
            VisualEffectAsset existing = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (existing != null) return existing;
            if (File.Exists(path))
                throw new InvalidOperationException("A VFX asset exists but Unity could not load it: " + path
                    + ". Remove or repair that Magic graph before running the generator again.");
            if (!File.Exists(HlslPath))
                throw new InvalidOperationException("The project-owned Magic HLSL source is missing at " + HlslPath + ".");

            Type utility = ResolveType("UnityEditor.VisualEffectAssetEditorUtility");
            MethodInfo create = utility.GetMethod("CreateNewAsset", AllStatic, null, new[] { typeof(string) }, null);
            if (create == null)
                throw new InvalidOperationException("Installed Visual Effect Graph API is missing VisualEffectAssetEditorUtility.CreateNewAsset(string).");
            VisualEffectAsset asset = create.Invoke(null, new object[] { path }) as VisualEffectAsset;
            if (asset == null) throw new InvalidOperationException("Unity did not create a VFX Graph at " + path + ".");

            try
            {
                BuildGraph(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                if (asset == null)
                    throw new InvalidOperationException("Unity did not reload the generated Magic graph at " + path
                        + ". Check the first VFX Graph import error in the Console.");
                return asset;
            }
            catch
            {
                AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        private static void BuildGraph(VisualEffectAsset asset)
        {
            object resource = InvokeExtension("UnityEditor.VFX.VisualEffectObjectExtensions", "GetOrCreateResource", asset);
            object graph = InvokeExtension("UnityEditor.VFX.VisualEffectResourceExtensions", "GetOrCreateGraph", resource);
            object spawner = CreateModel("UnityEditor.VFX.VFXBasicSpawner");
            object initialize = CreateModel("UnityEditor.VFX.VFXBasicInitialize");
            object update = CreateModel("UnityEditor.VFX.VFXBasicUpdate");
            object coreOutput = CreatePlanarOutput();
            object glowOutput = CreatePlanarOutput();
            object lineOutput = CreateModel("UnityEditor.VFX.VFXLineOutput");
            SetField(lineOutput, "useTargetOffset", false);
            SetField(lineOutput, "useNativeLines", false);

            SetPosition(spawner, new Vector2(330f, 80f));
            SetPosition(initialize, new Vector2(650f, 80f));
            SetPosition(update, new Vector2(960f, 80f));
            SetPosition(coreOutput, new Vector2(1280f, 0f));
            SetPosition(glowOutput, new Vector2(1280f, 360f));
            SetPosition(lineOutput, new Vector2(1280f, 720f));
            AddChild(graph, spawner);
            AddChild(graph, initialize);
            AddChild(graph, update);
            AddChild(graph, coreOutput);
            AddChild(graph, glowOutput);
            AddChild(graph, lineOutput);
            Call(spawner, "LinkTo", initialize, 0, 0);
            Call(initialize, "LinkTo", update, 0, 0);
            Call(update, "LinkTo", coreOutput, 0, 0);
            Call(update, "LinkTo", glowOutput, 0, 0);
            Call(update, "LinkTo", lineOutput, 0, 0);

            object data = Call(initialize, "GetData");
            if (data == null) throw new InvalidOperationException("VFX Graph did not create Magic particle data.");
            Call(data, "SetSettingValue", "capacity", (uint)2048);
            Type boundsModeType = FindField(data.GetType(), "boundsMode").FieldType;
            Call(data, "SetSettingValue", "boundsMode", Enum.Parse(boundsModeType, "Manual"));
            PropertyInfo dataSpace = FindProperty(data.GetType(), "space");
            dataSpace.SetValue(data, Enum.Parse(dataSpace.PropertyType, "World"), null);
            Call(update, "SetSettingValue", "integration", Enum.Parse(FindField(update.GetType(), "integration").FieldType, "None"));
            Call(update, "SetSettingValue", "angularIntegration", Enum.Parse(FindField(update.GetType(), "angularIntegration").FieldType, "None"));
            Call(update, "SetSettingValue", "ageParticles", true);
            Call(update, "SetSettingValue", "reapParticles", true);
            SetManualBounds(initialize);

            ParameterNode styleId = AddParameter(graph, "StyleId", typeof(int), 0, 20f, 20f);
            ParameterNode effectMode = AddParameter(graph, "EffectMode", typeof(int), 0, 20f, 95f);
            ParameterNode effectTime = AddParameter(graph, "EffectTime", typeof(float), 0f, 20f, 170f);
            ParameterNode effectProgress = AddParameter(graph, "EffectProgress", typeof(float), 0f, 20f, 245f);
            ParameterNode targetCenter = AddParameter(graph, "TargetCenter", typeof(Vector3), Vector3.zero, 20f, 320f);
            ParameterNode targetRadius = AddParameter(graph, "TargetRadius", typeof(float), 0.5f, 20f, 395f);
            ParameterNode targetHeight = AddParameter(graph, "TargetHeight", typeof(float), 1.8f, 20f, 470f);
            ParameterNode primaryColor = AddParameter(graph, "PrimaryColor", typeof(Vector4), Vector4.one, 20f, 545f);
            ParameterNode secondaryColor = AddParameter(graph, "SecondaryColor", typeof(Vector4), Vector4.one, 20f, 620f);
            ParameterNode accentColor = AddParameter(graph, "AccentColor", typeof(Vector4), Vector4.one, 20f, 695f);
            ParameterNode smokeColor = AddParameter(graph, "SmokeColor", typeof(Vector4), new Vector4(0.08f, 0.06f, 0.09f, 0.4f), 20f, 770f);
            ParameterNode intensity = AddParameter(graph, "Intensity", typeof(float), 1f, 20f, 845f);
            ParameterNode particleSize = AddParameter(graph, "ParticleSize", typeof(float), 0.035f, 20f, 920f);
            ParameterNode riseSpeed = AddParameter(graph, "RiseSpeed", typeof(float), 0.4f, 20f, 995f);
            ParameterNode swirlStrength = AddParameter(graph, "SwirlStrength", typeof(float), 1f, 20f, 1070f);
            ParameterNode turbulence = AddParameter(graph, "Turbulence", typeof(float), 0.2f, 20f, 1145f);
            ParameterNode pulseFrequency = AddParameter(graph, "PulseFrequency", typeof(float), 1f, 20f, 1220f);
            ParameterNode seed = AddParameter(graph, "Seed", typeof(int), 1, 20f, 1295f);
            ParameterNode spawnRate = AddParameter(graph, "SpawnRate", typeof(float), 100f, 20f, 1370f);
            ParameterNode spawnBurstCount = AddParameter(graph, "SpawnBurstCount", typeof(float), 0f, 20f, 1445f);
            ParameterNode particleLifetime = AddParameter(graph, "ParticleLifetime", typeof(float), 2f, 20f, 1520f);

            object burst = CreateModel("UnityEditor.VFX.VFXSpawnerBurst");
            AddChild(spawner, burst);
            SetBySettingName(burst, "repeat", "Single");
            SetBySettingName(burst, "spawnMode", "Constant");
            SetBySettingName(burst, "delayMode", "Constant");
            SetSlotValue(FindSlot(burst, "Delay"), 0f);
            LinkParameter(spawnBurstCount, FindSlot(burst, "Count"));

            object constantRate = CreateModel("UnityEditor.VFX.VFXSpawnerConstantRate");
            AddChild(spawner, constantRate);
            LinkParameter(spawnRate, FindSlot(constantRate, "Rate"));

            AddHlslBlock(initialize, "PerformerMagicInitialize");
            object initHlsl = LastChildOfType(initialize, "CustomHLSL");
            LinkParameter(styleId, FindHlslInputSlot(initHlsl, "StyleId"));
            LinkParameter(effectMode, FindHlslInputSlot(initHlsl, "EffectMode"));
            LinkParameter(targetCenter, FindHlslInputSlot(initHlsl, "TargetCenter"));
            LinkParameter(targetRadius, FindHlslInputSlot(initHlsl, "TargetRadius"));
            LinkParameter(targetHeight, FindHlslInputSlot(initHlsl, "TargetHeight"));
            LinkParameter(particleSize, FindHlslInputSlot(initHlsl, "ParticleSize"));
            LinkParameter(particleLifetime, FindHlslInputSlot(initHlsl, "ParticleLifetime"));
            LinkParameter(seed, FindHlslInputSlot(initHlsl, "Seed"));

            AddHlslBlock(update, "PerformerMagicUpdate");
            object updateHlsl = LastChildOfType(update, "CustomHLSL");
            LinkParameter(styleId, FindHlslInputSlot(updateHlsl, "StyleId"));
            LinkParameter(effectMode, FindHlslInputSlot(updateHlsl, "EffectMode"));
            LinkParameter(effectTime, FindHlslInputSlot(updateHlsl, "EffectTime"));
            LinkParameter(effectProgress, FindHlslInputSlot(updateHlsl, "EffectProgress"));
            LinkParameter(targetCenter, FindHlslInputSlot(updateHlsl, "TargetCenter"));
            LinkParameter(targetRadius, FindHlslInputSlot(updateHlsl, "TargetRadius"));
            LinkParameter(targetHeight, FindHlslInputSlot(updateHlsl, "TargetHeight"));
            LinkParameter(riseSpeed, FindHlslInputSlot(updateHlsl, "RiseSpeed"));
            LinkParameter(swirlStrength, FindHlslInputSlot(updateHlsl, "SwirlStrength"));
            LinkParameter(turbulence, FindHlslInputSlot(updateHlsl, "Turbulence"));
            LinkParameter(pulseFrequency, FindHlslInputSlot(updateHlsl, "PulseFrequency"));
            LinkParameter(seed, FindHlslInputSlot(updateHlsl, "Seed"));

            AddHlslBlock(coreOutput, "PerformerMagicCoreOutput");
            LinkOutputProperties(LastChildOfType(coreOutput, "CustomHLSL"), styleId, effectMode,
                effectTime, effectProgress, primaryColor, secondaryColor, accentColor, smokeColor,
                intensity, pulseFrequency, seed);
            AddHlslBlock(glowOutput, "PerformerMagicGlowOutput");
            LinkOutputProperties(LastChildOfType(glowOutput, "CustomHLSL"), styleId, effectMode,
                effectTime, effectProgress, primaryColor, secondaryColor, accentColor, smokeColor,
                intensity, pulseFrequency, seed);
            AddHlslBlock(lineOutput, "PerformerMagicLineOutput");
            object lineHlsl = LastChildOfType(lineOutput, "CustomHLSL");
            LinkParameter(styleId, FindHlslInputSlot(lineHlsl, "StyleId"));
            LinkParameter(effectMode, FindHlslInputSlot(lineHlsl, "EffectMode"));
            LinkParameter(effectTime, FindHlslInputSlot(lineHlsl, "EffectTime"));
            LinkParameter(primaryColor, FindHlslInputSlot(lineHlsl, "PrimaryColor"));
            LinkParameter(secondaryColor, FindHlslInputSlot(lineHlsl, "SecondaryColor"));
            LinkParameter(intensity, FindHlslInputSlot(lineHlsl, "Intensity"));
            LinkParameter(pulseFrequency, FindHlslInputSlot(lineHlsl, "PulseFrequency"));
            LinkParameter(seed, FindHlslInputSlot(lineHlsl, "Seed"));

            InvokeExtension("UnityEditor.VFX.VisualEffectResourceExtensions", "WriteAssetWithSubAssets", resource);
        }

        private static void LinkOutputProperties(object block, ParameterNode styleId, ParameterNode effectMode,
            ParameterNode effectTime, ParameterNode effectProgress, ParameterNode primary, ParameterNode secondary,
            ParameterNode accent, ParameterNode smoke, ParameterNode intensity, ParameterNode pulse, ParameterNode seed)
        {
            LinkParameter(styleId, FindHlslInputSlot(block, "StyleId"));
            LinkParameter(effectMode, FindHlslInputSlot(block, "EffectMode"));
            LinkParameter(effectTime, FindHlslInputSlot(block, "EffectTime"));
            LinkParameter(effectProgress, FindHlslInputSlot(block, "EffectProgress"));
            LinkParameter(primary, FindHlslInputSlot(block, "PrimaryColor"));
            LinkParameter(secondary, FindHlslInputSlot(block, "SecondaryColor"));
            LinkParameter(accent, FindHlslInputSlot(block, "AccentColor"));
            LinkParameter(smoke, FindHlslInputSlot(block, "SmokeColor"));
            LinkParameter(intensity, FindHlslInputSlot(block, "Intensity"));
            LinkParameter(pulse, FindHlslInputSlot(block, "PulseFrequency"));
            LinkParameter(seed, FindHlslInputSlot(block, "Seed"));
        }

        private static object CreatePlanarOutput()
        {
            object output = CreateModel("UnityEditor.VFX.VFXPlanarPrimitiveOutput");
            SetEnumField(output, "primitiveType", "Quad");
            SetEnumField(output, "useBaseColorMap", "ColorAndAlpha");
            SetEnumField(output, "uvMode", "Default");
            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DazPose/Effects/ParticleBody/FireflyGlow.png");
            if (glow == null) throw new InvalidOperationException("Magic Graphs reuse the existing ParticleBody FireflyGlow.png sprite.");
            SetSlotValue(FindSlot(output, "mainTexture"), glow);
            return output;
        }

        private static void SetManualBounds(object initialize)
        {
            object boundsSlot = FindSlot(initialize, "bounds");
            object property = GetProperty(boundsSlot, "property");
            Type boxType = (Type)GetProperty(property, "type");
            object bounds = Activator.CreateInstance(boxType);
            SetMember(bounds, "center", Vector3.zero);
            SetMember(bounds, "size", Vector3.one * 2000f);
            SetSlotValue(boundsSlot, bounds);
        }

        private static ParameterNode AddParameter(object graph, string name, Type type, object value, float x, float y)
        {
            object parameter = ScriptableObject.CreateInstance(ResolveType("UnityEditor.VFX.VFXParameter"));
            SetField(parameter, "m_ExposedName", name);
            SetField(parameter, "m_Exposed", true);
            SetField(parameter, "m_Category", "Performer Magic");
            Call(parameter, "Init", type);
            SetProperty(parameter, "value", value);
            AddChild(graph, parameter);
            int nodeId = (int)Call(parameter, "AddNode", new Vector2(x, y));
            return new ParameterNode { Model = parameter, NodeId = nodeId };
        }

        private static void LinkParameter(ParameterNode parameter, object input)
        {
            LinkInputToOutput(input, FirstSlot(parameter.Model, "outputSlots"));
            object node = Call(parameter.Model, "GetNode", parameter.NodeId);
            FieldInfo field = FindField(node.GetType(), "linkedSlots");
            IList links = field.GetValue(node) as IList;
            if (links == null)
            {
                Type elementType = field.FieldType.GetGenericArguments()[0];
                links = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                field.SetValue(node, links);
            }
            Type linkType = field.FieldType.GetGenericArguments()[0];
            object link = Activator.CreateInstance(linkType);
            SetField(link, "outputSlot", FirstSlot(parameter.Model, "outputSlots"));
            SetField(link, "inputSlot", input);
            links.Add(link);
        }

        private static void LinkInputToOutput(object input, object output)
        {
            object linked = Call(input, "Link", output, true);
            if (linked is bool && !(bool)linked)
                throw new InvalidOperationException("VFX Graph refused to link input '" + GetProperty(input, "name") + "'.");
        }

        private static void AddHlslBlock(object context, string function)
        {
            object block = CreateModel("UnityEditor.VFX.Block.CustomHLSL");
            SetField(block, "m_BlockName", function);
            SetField(block, "m_HLSLCode", File.ReadAllText(HlslPath));
            AddChild(context, block);
            Call(block, "ResyncSlots", true);
            FieldInfo selection = FindField(block.GetType(), "m_AvailableFunction");
            Type choiceType = selection.FieldType;
            object choice = Activator.CreateInstance(choiceType);
            choiceType.GetProperty("values", AllInstance).SetValue(choice, new List<string>
            {
                "PerformerMagicInitialize", "PerformerMagicUpdate",
                "PerformerMagicCoreOutput", "PerformerMagicGlowOutput", "PerformerMagicLineOutput"
            }, null);
            Call(block, "SetSelection", function);
            selection.SetValue(block, choice);
            Call(block, "ResyncSlots", true);
        }

        private static object LastChildOfType(object model, string name)
        {
            FieldInfo field = FindField(model.GetType(), "m_Children", false);
            IList children = field != null ? field.GetValue(model) as IList : null;
            if (children != null)
                for (int i = children.Count - 1; i >= 0; i--)
                    if (children[i] != null && children[i].GetType().Name == name) return children[i];
            throw new InvalidOperationException(model.GetType().Name + " has no child of type " + name + ".");
        }

        private static void SetBySettingName(object model, string name, string value)
        {
            FieldInfo field = FindField(model.GetType(), name);
            Call(model, "SetSettingValue", name, Enum.Parse(field.FieldType, value));
        }

        private static void SetEnumField(object model, string name, string value)
        {
            FieldInfo field = FindField(model.GetType(), name);
            field.SetValue(model, Enum.Parse(field.FieldType, value));
        }

        private static object CreateModel(string type) => ScriptableObject.CreateInstance(ResolveType(type));

        private static Type ResolveType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            throw new InvalidOperationException("Installed Visual Effect Graph API is missing type '" + fullName + "'.");
        }

        private static object InvokeExtension(string typeName, string methodName, object argument)
        {
            Type type = ResolveType(typeName);
            MethodInfo method = type.GetMethods(AllStatic).FirstOrDefault(candidate => candidate.Name == methodName
                && candidate.GetParameters().Length == 1
                && candidate.GetParameters()[0].ParameterType.IsInstanceOfType(argument));
            if (method == null) throw new InvalidOperationException("Installed VFX Graph API is missing " + typeName + "." + methodName + ".");
            return method.Invoke(null, new[] { argument });
        }

        private static object Call(object target, string name, params object[] args)
        {
            if (target == null) throw new InvalidOperationException("Cannot call " + name + " on a null VFX Graph model.");
            MethodInfo method = target.GetType().GetMethods(AllInstance)
                .Where(candidate => candidate.Name == name && candidate.GetParameters().Length == args.Length)
                .FirstOrDefault(candidate => ParametersAccept(candidate.GetParameters(), args));
            if (method == null)
                throw new InvalidOperationException("Installed VFX Graph model " + target.GetType().Name + " is missing compatible API '" + name + "'.");
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                throw new InvalidOperationException("VFX Graph operation '" + target.GetType().Name + "." + name + "' failed: " + cause.Message, cause);
            }
        }

        private static bool ParametersAccept(ParameterInfo[] parameters, object[] args)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                {
                    if (parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null) return false;
                }
                else if (!parameters[i].ParameterType.IsInstanceOfType(args[i])) return false;
            }
            return true;
        }

        private static void AddChild(object parent, object child) => Call(parent, "AddChild", child, -1, true);
        private static void SetPosition(object model, Vector2 position) => SetProperty(model, "position", position);

        private static object FirstSlot(object model, string propertyName)
        {
            IEnumerable slots = GetProperty(model, propertyName) as IEnumerable;
            if (slots != null) foreach (object slot in slots) return slot;
            throw new InvalidOperationException(model.GetType().Name + " has no " + propertyName + " slot.");
        }

        private static object FindSlot(object model, string name)
        {
            foreach (object slot in GetSlots(model, "inputSlots"))
            {
                object match = FindSlotRecursive(slot, name);
                if (match != null) return match;
            }
            foreach (object slot in GetSlots(model, "outputSlots"))
            {
                object match = FindSlotRecursive(slot, name);
                if (match != null) return match;
            }
            throw new InvalidOperationException(model.GetType().Name + " has no VFX slot named '" + name + "'.");
        }

        private static object FindHlslInputSlot(object model, string name) => FindSlot(model, "_" + name);

        private static IEnumerable<object> GetSlots(object model, string name)
        {
            IEnumerable slots = GetProperty(model, name) as IEnumerable;
            if (slots == null) yield break;
            foreach (object slot in slots) yield return slot;
        }

        private static object FindSlotRecursive(object slot, string name)
        {
            if (string.Equals(Convert.ToString(GetProperty(slot, "name")), name, StringComparison.OrdinalIgnoreCase)) return slot;
            FieldInfo field = FindField(slot.GetType(), "m_Children", false);
            IList children = field != null ? field.GetValue(slot) as IList : null;
            if (children == null) return null;
            foreach (object child in children)
            {
                object match = FindSlotRecursive(child, name);
                if (match != null) return match;
            }
            return null;
        }

        private static void SetSlotValue(object slot, object value) => SetProperty(slot, "value", value);
        private static object GetProperty(object target, string name) => FindProperty(target.GetType(), name).GetValue(target, null);
        private static void SetProperty(object target, string name, object value) => FindProperty(target.GetType(), name).SetValue(target, value, null);

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, AllInstance | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }
            throw new InvalidOperationException("Installed VFX Graph API is missing property '" + type.FullName + "." + name + "'.");
        }

        private static FieldInfo FindField(Type type, string name, bool required = true)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, AllInstance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            if (!required) return null;
            throw new InvalidOperationException("Installed VFX Graph API is missing field '" + type.FullName + "." + name + "'.");
        }

        private static void SetMember(object target, string name, object value)
        {
            FieldInfo field = FindField(target.GetType(), name, false);
            if (field != null) field.SetValue(target, value);
            else SetProperty(target, name, value);
        }

        private static void SetField(object target, string name, object value) => FindField(target.GetType(), name).SetValue(target, value);
    }
}
