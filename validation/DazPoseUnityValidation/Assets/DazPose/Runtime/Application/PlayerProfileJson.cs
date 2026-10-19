using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
namespace DazPose.Game
{
    // Only persisted public fields are modeled. Unknown JSON survives edits in the service's raw document.
    public static class PlayerProfileJson
    {
        sealed class ProfileFields : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) =>
                type.GetFields(BindingFlags.Public|BindingFlags.Instance)
                    .Where(f=>!Attribute.IsDefined(f,typeof(NonSerializedAttribute)))
                    .Select(f=>CreateProperty(f,MemberSerialization.Fields)).ToList();
        }
        static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
        { ContractResolver=new ProfileFields(), TypeNameHandling=TypeNameHandling.None });
        public static PlayerProfile Read(JObject document) => document.ToObject<PlayerProfile>(Serializer());
        public static JObject Write(PlayerProfile profile) => JObject.FromObject(profile,Serializer());
        public static PlayerProfile Clone(PlayerProfile profile)
        {
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            return Read(Write(profile));
        }
        public static JObject Merge(JObject previous,PlayerProfile next) =>
            (JObject)MergeToken(previous,Write(next));
        static JToken MergeToken(JToken previous,JToken next)
        {
            if(next is JObject value)
            {
                var result=previous is JObject old ? (JObject)old.DeepClone() : new JObject();
                foreach(var property in value.Properties())
                    result[property.Name]=MergeToken(result[property.Name],property.Value);
                return result;
            }
            if(next is JArray array)
            {
                var old=previous as JArray;
                var result=new JArray();
                for(int i=0;i<array.Count;i++)
                {
                    string key=Identity(array[i]);
                    var prior=key!=null ? old?.FirstOrDefault(item=>Identity(item)==key) :
                        old!=null && i<old.Count ? old[i] : null;
                    result.Add(MergeToken(prior,array[i]));
                }
                return result;
            }
            return next.DeepClone();
        }
        static string Identity(JToken value)
        {
            if(!(value is JObject row))return null;
            foreach(string field in new[]{"id","key","instanceId","sessionId","name"})
                if(row[field]?.Type==JTokenType.String)return field+":"+row[field].Value<string>();
            return null;
        }
    }
}
