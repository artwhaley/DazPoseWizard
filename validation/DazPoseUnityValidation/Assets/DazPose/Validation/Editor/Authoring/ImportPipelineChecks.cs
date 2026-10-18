using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor
{
    /// <summary>Explicit end-to-end import regression check; no automatic hook or menu.</summary>
    public static class ImportPipelineChecks
    {
        [Serializable] private sealed class Check { public string name; public bool passed; public string detail; }
        [Serializable] private sealed class Report { public string startedUtc, completedUtc, sourceAssetPath, fixtureAssetPath, statusPath, importerReportPath; public bool passed; public Check[] checks; }
        private sealed class Sample { public string assetPath, diskPath; }

        public static void Run()
        {
            Guard();
            var project = Directory.GetParent(Application.dataPath).FullName;
            var queue = FindType("DazPose.Editor.Importing.DazPoseBrowserImportQueue");
            var bridgeArgs = new object[] { null, false };
            if (!(bool)Call(queue, "TryLoadBridge", bridgeArgs)) throw new InvalidOperationException("Configured project bridge unavailable.");
            var roots = new object[] { bridgeArgs[0], null, null, null, null };
            Call(queue, "ValidateBridge", roots);
            var importRoot = (string)roots[1]; var outputRoot = (string)roots[2];
            var sample = FindSample(queue, importRoot);
            if (sample == null) throw new FileNotFoundException("No current Converted G8F canonical pose with matching clip and PerformerPose under configured roots.");

            var id = Guid.NewGuid().ToString("N");
            var folder = "__CleanupImportProbe_" + id;
            var inputFolder = importRoot.TrimEnd('/') + "/" + folder;
            var outputFolder = outputRoot.TrimEnd('/') + "/" + folder;
            var fixture = inputFolder + "/" + Path.GetFileName(sample.diskPath);
            var fixtureDisk = AssetToDisk(fixture);
            var statusPath = (string)Call(queue, "GetStatusPath", fixture);
            var importerReportPath = Path.GetFullPath(Path.Combine(project, ".dazposewizard", "reports", (string)Call(queue, "StatusFileName", fixture) + ".report.json"));
            var runReportPath = Path.GetFullPath(Path.Combine(project, ".dazposewizard", "reports", "cleanup-import-pipeline-" + id + ".json"));
            var checks = new List<Check>();
            var report = new Report { startedUtc=DateTime.UtcNow.ToString("O"), sourceAssetPath=sample.assetPath, fixtureAssetPath=fixture, statusPath=statusPath, importerReportPath=importerReportPath };
            bool inputMade=false, outputMade=false, statusMade=false, importReportMade=false;
            try
            {
                if (AssetDatabase.IsValidFolder(inputFolder) || AssetDatabase.IsValidFolder(outputFolder) || File.Exists(statusPath) || File.Exists(importerReportPath))
                    throw new IOException("Unique fixture path already exists; refusing overwrite.");
                Directory.CreateDirectory(Path.GetDirectoryName(fixtureDisk));
                File.Copy(sample.diskPath, fixtureDisk, false); inputMade=true;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Call(queue, "ProcessCanonicalImport", fixture, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                outputMade=AssetDatabase.IsValidFolder(outputFolder); statusMade=File.Exists(statusPath); importReportMade=File.Exists(importerReportPath);
                var first=Call(queue,"ReadStatus",fixture);
                var time=Field(first,"timestamp") as string; var contentHash=Field(first,"canonicalContentHash") as string;
                var anim=Field(first,"expectedAnimPath") as string; var posePath=Field(first,"expectedWrapperAssetPath") as string;
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(anim); var pose=AssetDatabase.LoadAssetAtPath<PerformerPose>(posePath);
                var converted=Is(first,"state","Converted") && clip!=null && pose!=null && pose.Clip==clip;
                Add(checks,"Converted typed output",converted,"Real ProcessCanonicalImport produced a PerformerPose bound to its AnimationClip.");
                if (!converted) throw new InvalidOperationException("Fixture did not produce usable Converted output.");
                var clipGuid=AssetDatabase.AssetPathToGUID(anim); var poseGuid=AssetDatabase.AssetPathToGUID(posePath);
                var statusHash=Hash(File.ReadAllBytes(statusPath)); var statusTime=File.GetLastWriteTimeUtc(statusPath);
                var importReportHash=importReportMade?Hash(File.ReadAllBytes(importerReportPath)):null;

                Call(queue,"ProcessCanonicalImport",fixture,false); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var again=Call(queue,"ReadStatus",fixture);
                var unchanged=Is(again,"timestamp",time) && Is(again,"canonicalContentHash",contentHash)
                    && Hash(File.ReadAllBytes(statusPath))==statusHash && File.GetLastWriteTimeUtc(statusPath)==statusTime
                    && AssetDatabase.AssetPathToGUID(anim)==clipGuid && AssetDatabase.AssetPathToGUID(posePath)==poseGuid
                    && (importReportHash==null || (File.Exists(importerReportPath) && Hash(File.ReadAllBytes(importerReportPath))==importReportHash));
                Add(checks,"unchanged import is no-op",unchanged,"Second call preserved status, report, timestamp, and output GUIDs.");

                File.AppendAllText(fixtureDisk," \n",new UTF8Encoding(false));
                AssetDatabase.ImportAsset(fixture,ImportAssetOptions.ForceSynchronousImport);
                Call(queue,"ProcessCanonicalImport",fixture,false); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var changed=Call(queue,"ReadStatus",fixture);
                var clip2=AssetDatabase.LoadAssetAtPath<AnimationClip>(anim); var pose2=AssetDatabase.LoadAssetAtPath<PerformerPose>(posePath);
                var reconverted=Is(changed,"state","Converted") && Is(changed,"canonicalContentHash",Hash(File.ReadAllBytes(fixtureDisk)))
                    && AssetDatabase.AssetPathToGUID(anim)==clipGuid && AssetDatabase.AssetPathToGUID(posePath)==poseGuid
                    && clip2!=null && pose2!=null && pose2.Clip==clip2;
                Add(checks,"changed copied input reconverts",reconverted,"Whitespace changed the hash; conversion retained both asset GUIDs.");
            }
            catch(Exception e) { Add(checks,"execution",false,e.GetBaseException().Message); }
            finally
            {
                outputMade = outputMade || AssetDatabase.IsValidFolder(outputFolder);
                statusMade = statusMade || File.Exists(statusPath);
                importReportMade = importReportMade || File.Exists(importerReportPath);
                if(inputMade) CleanFolder(inputFolder,checks,"input cleanup");
                if(outputMade) CleanFolder(outputFolder,checks,"output cleanup");
                DeleteFixture(statusPath,project,statusMade,checks,"status cleanup");
                DeleteFixture(importerReportPath,project,importReportMade,checks,"import report cleanup");
            }
            report.completedUtc=DateTime.UtcNow.ToString("O"); report.checks=checks.ToArray(); report.passed=checks.Count>0 && checks.All(c=>c.passed);
            Directory.CreateDirectory(Path.GetDirectoryName(runReportPath));
            File.WriteAllText(runReportPath,JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            if(!report.passed) throw new InvalidOperationException("Import pipeline checks failed; report: "+runReportPath);
            Debug.Log("Import pipeline checks passed. Source "+sample.assetPath+"; report "+runReportPath);
        }

        private static Sample FindSample(Type queue,string root)
        {
            var directory=AssetToDisk(root+"/probe"); directory=Directory.GetParent(directory).FullName;
            if(!Directory.Exists(directory)) return null;
            foreach(var disk in Directory.GetFiles(directory,"*.dazpose.json",SearchOption.AllDirectories))
            {
                var asset="Assets/"+Path.GetRelativePath(Application.dataPath,disk).Replace('\\','/');
                var status=Call(queue,"ReadStatus",asset);
                if(!Is(status,"state","Converted") || !Is(status,"canonicalContentHash",(string)Call(queue,"ContentHash",disk))) continue;
                if(File.ReadAllText(disk).IndexOf("Genesis8Female.dsf",StringComparison.OrdinalIgnoreCase)<0) continue;
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Field(status,"expectedAnimPath") as string);
                var pose=AssetDatabase.LoadAssetAtPath<PerformerPose>(Field(status,"expectedWrapperAssetPath") as string);
                if(clip!=null && pose!=null && pose.Clip==clip) return new Sample{assetPath=asset,diskPath=disk};
            }
            return null;
        }
        private static void Guard()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Run only in idle Edit Mode.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("A loaded scene has unsaved changes.");
            foreach(var o in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
                if(o!=null && EditorUtility.IsPersistent(o) && AssetDatabase.IsNativeAsset(o) && EditorUtility.IsDirty(o) &&
                    AssetDatabase.GetAssetPath(o).StartsWith("Assets/", StringComparison.Ordinal))
                    throw new InvalidOperationException("A persistent asset has unsaved changes: "+AssetDatabase.GetAssetPath(o)+" ["+o.GetType().FullName+" / "+o.name+"]. No fixture import was started.");
        }
        private static void CleanFolder(string path,List<Check> checks,string name)
        { try { Add(checks,name,AssetDatabase.DeleteAsset(path)&&!AssetDatabase.IsValidFolder(path),"Deleted unique fixture folder via AssetDatabase."); } catch(Exception e){Add(checks,name,false,e.GetBaseException().Message);} }
        private static void DeleteFixture(string path,string project,bool created,List<Check> checks,string name)
        {
            if(!created)return;
            try
            {
                var root=Path.GetFullPath(Path.Combine(project,".dazposewizard")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                var full=Path.GetFullPath(path);
                if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Refusing deletion outside project .dazposewizard.");
                if(File.Exists(full))File.Delete(full);
                Add(checks,name,!File.Exists(full),"Removed exact fixture file "+full);
            } catch(Exception e){Add(checks,name,false,e.GetBaseException().Message);}
        }
        private static string AssetToDisk(string asset)
        {
            var root=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            var full=Path.GetFullPath(Path.Combine(root,asset.Substring("Assets/".Length).Replace('/',Path.DirectorySeparatorChar)));
            if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Fixture escaped Assets.");
            return full;
        }
        private static Type FindType(string name){foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){var t=a.GetType(name,false);if(t!=null)return t;}throw new TypeLoadException(name);}
        private static object Call(Type t,string n,params object[] args){var m=t.GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);if(m==null)throw new MissingMethodException(t.FullName,n);return m.Invoke(null,args);}
        private static object Field(object o,string n)=>o==null?null:o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(o);
        private static bool Is(object o,string n,string v)=>string.Equals(Field(o,n) as string,v,StringComparison.OrdinalIgnoreCase);
        private static string Hash(byte[] b){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        private static void Add(List<Check> c,string n,bool p,string d)=>c.Add(new Check{name=n,passed=p,detail=d});
    }
}