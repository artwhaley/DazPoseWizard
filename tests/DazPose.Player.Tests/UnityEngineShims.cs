using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath =>
            throw new InvalidOperationException("Player state must never access the real Unity persistentDataPath in tests.");
    }

    public static class Debug
    {
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    public static class JsonUtility
    {
        sealed class PublicFieldResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) =>
                type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(field => CreateProperty(field, MemberSerialization.Fields)).ToList();
        }

        public static T FromJson<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { ContractResolver = new PublicFieldResolver() });
    }

    public class ScriptableObject
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class TextAsset
    {
        public string text;
        public TextAsset(string value) { text = value; }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : class =>
            throw new InvalidOperationException("Tests must supply explicit catalogs; Resources.Load must not access the Unity project.");
    }
}
