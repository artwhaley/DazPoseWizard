using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [Serializable] public sealed class WardrobeImportRecipe
    {
        public int schemaVersion=1;
        public string id,duf,fbx,destination;
        public string characterScene=LaraCandidateInstaller.ValidationScene;
        public string characterPoints=LaraCandidateBuilder.Folder+"/manifest.json";
        public string referenceFbx="Assets/TestCharacter/larafirstoutfit.fbx";
        public string[] contentRoots=Array.Empty<string>();
        public string[] include=Array.Empty<string>();
        public Mapping[] ownership=Array.Empty<Mapping>(),shapeNames=Array.Empty<Mapping>();
        public Policy[] policies=Array.Empty<Policy>();
        public Footwear shoes;
        [Serializable] public sealed class Mapping {public string from,to;}
        [Serializable] public sealed class Policy
        {
            public string node,role,coverage="none";
            public bool allowLocalBones=true,transferBodyMorphs=true,requiresBentFootPose;
        }
        [Serializable] public sealed class Footwear
        { public string node,articulation,capSurface; public float defaultFootFit=.05f; }
        [Serializable] public sealed class Manifest
        {public string asset,sha256,duf,dufSHA256;public Part[] parts;public Node[] sourceNodes;}
        [Serializable] public sealed class Node {public string id,parent;}
        [Serializable] public sealed class Part
        {
            public string node,sourceNode,kind,coveragePolicy;public bool shell;
            public LaraCandidateBuilder.Point[] points;public LaraCandidateBuilder.Frame[] frames;
            public LaraCandidateBuilder.Surface[] materials;
        }
        [Serializable] public sealed class Heel
        {public string sourceSHA256,referenceSHA256;public LaraCandidateBuilder.Entry[] entries;}
        public string Scene=>destination+"/Review.unity";
        public string Output=>"TestOutput/wardrobe-import/"+id;
        public static string Argument(string name)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);
            if(i<0 || i+1>=args.Length)throw new IOException("Required argument: "+name);
            return args[i+1];
        }
        public static WardrobeImportRecipe Read()=>Read(Argument("-wardrobeRecipe"));
        public static WardrobeImportRecipe Read(string path)
        {
            var recipe=JsonUtility.FromJson<WardrobeImportRecipe>(File.ReadAllText(path));
            if(recipe.schemaVersion!=1 || string.IsNullOrEmpty(recipe.id) || recipe.id.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'&&c!='_'))
                throw new IOException("Unsupported recipe version or unsafe outfit id.");
            if(string.IsNullOrEmpty(recipe.destination) || !recipe.destination.StartsWith("Assets/TestData/Wardrobe/",StringComparison.Ordinal)
                || recipe.destination.Split('/').Any(s=>s==".." || s=="."))throw new IOException("Destination must be an owned Assets/TestData/Wardrobe folder.");
            if(recipe.policies.GroupBy(p=>p.node).Any(g=>g.Count()!=1))throw new IOException("Duplicate piece policy.");
            return recipe;
        }
    }
}
