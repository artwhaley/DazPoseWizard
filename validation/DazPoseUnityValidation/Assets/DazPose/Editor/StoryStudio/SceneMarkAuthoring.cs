using System;
using System.Linq;
using DazPose.Story;
using DazPose.Performer;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor.Studio
{
    public static class SceneMarkAuthoring
    {
        static Scene ValidateHost(StorySceneBindings host) {
            if(host==null)throw new InvalidOperationException("Choose a scene host.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Authoring marks is available only in Edit Mode.");
            var scene=host.gameObject.scene;
            if(!scene.IsValid()||!scene.isLoaded||string.IsNullOrEmpty(scene.path)||EditorSceneManager.IsPreviewScene(scene))
                throw new InvalidOperationException("Choose a host in an open, saved scene.");
            void SameScene(GameObject go,string label) {if(go!=null&&go.scene!=scene)throw new InvalidOperationException(label+" belongs to another scene.");}
            SameScene(host.lara==null?null:host.lara.gameObject,"Lara");
            SameScene(host.playerBody==null?null:host.playerBody.gameObject,"PlayerBody");
            SameScene(host.prompts==null?null:host.prompts.gameObject,"Prompt service");
            SameScene(host.debugPresenter==null?null:host.debugPresenter.gameObject,"Debug presenter");
            SameScene(host.laraFocus==null?null:host.laraFocus.gameObject,"Lara focus");
            foreach(var target in host.targets??Array.Empty<StoryTransformBinding>())SameScene(target==null||target.value==null?null:target.value.gameObject,"Legacy target "+target?.id);
            foreach(var asset in host.assets??Array.Empty<StoryObjectBinding>()) {
                var component=asset?.value as Component;var gameObject=asset?.value as GameObject;
                SameScene(component!=null?component.gameObject:gameObject,"Scene asset "+asset?.id);
            }
            var performance=host.GetComponent<PerformanceSceneBindings>();
            if(performance!=null) {var contact=performance.ContactTarget as Component;SameScene(contact==null?null:contact.gameObject,"Measured contact target");}
            foreach(var row in host.locations??Array.Empty<StoryLocationBinding>()) {
                if(row==null)continue;
                if(string.IsNullOrWhiteSpace(row.id))throw new InvalidOperationException("Every authored location requires a nonempty ID.");
                SameScene(row.placement==null?null:row.placement.gameObject,"Location "+row.id+" placement");
                SameScene(row.focus==null?null:row.focus.gameObject,"Location "+row.id+" focus");
                SameScene(row.seat==null?null:row.seat.gameObject,"Location "+row.id+" seat");
                if(row.seat!=null){SameScene(row.seat.ApproachAnchor==null?null:row.seat.ApproachAnchor.gameObject,"Location "+row.id+" approach anchor");SameScene(row.seat.SeatAnchor==null?null:row.seat.SeatAnchor.gameObject,"Location "+row.id+" seat anchor");}
            }
            var duplicates=(host.locations??Array.Empty<StoryLocationBinding>()).Where(l=>l!=null).GroupBy(l=>l.id,StringComparer.Ordinal).FirstOrDefault(g=>g.Count()>1);
            if(duplicates!=null)throw new InvalidOperationException("Resolve duplicate location ID '"+duplicates.Key+"' before authoring.");
            return scene;
        }
        static GameObject MarksRoot(Scene scene,bool create) {
            var matches=scene.GetRootGameObjects().Where(g=>g.name=="Authoring Marks").ToArray();
            if(matches.Length>1)throw new InvalidOperationException("Scene has more than one root named Authoring Marks; resolve the duplicate before authoring.");
            if(matches.Length==1)return matches[0];
            if(!create)return null;
            var parent=new GameObject("Authoring Marks");SceneManager.MoveGameObjectToScene(parent,scene);Undo.RegisterCreatedObjectUndo(parent,"Create Authoring Marks");return parent;
        }
        public static void AttachRequirements(StorySceneBindings host,StorySourceAnalysis analysis) {
            var scene=ValidateHost(host);if(analysis==null)throw new ArgumentNullException(nameof(analysis));
            var locations=analysis.Requirements.Where(r=>r.Kind==StoryBindingKind.Location).Select(r=>r.Alias).Distinct(StringComparer.Ordinal).ToArray();
            var missing=locations.Where(id=>!(host.locations??Array.Empty<StoryLocationBinding>()).Any(l=>l!=null&&l.id==id)).ToArray();
            if(missing.Length==0)return;
            Undo.RecordObject(host,"Attach source location requirements");
            host.locations=(host.locations??Array.Empty<StoryLocationBinding>()).Concat(missing.Select(id=>new StoryLocationBinding{id=id})).ToArray();
            EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(scene);
        }
        public static Transform Create(StorySceneBindings host,string id,Vector3 position,Quaternion rotation) {
            var scene=ValidateHost(host);if(string.IsNullOrWhiteSpace(id))throw new InvalidOperationException("Select a source location.");
            var rows=(host.locations??Array.Empty<StoryLocationBinding>()).Where(l=>l!=null&&l.id==id).ToArray();
            if(rows.Length>1)throw new InvalidOperationException("Duplicate location: "+id);
            if(rows.Length==1&&rows[0].placement!=null)throw new InvalidOperationException("This location already exists. Explicitly edit the existing mark.");
            var parent=MarksRoot(scene,true);
            if(parent.GetComponentsInChildren<Transform>(true).Any(t=>t!=parent.transform&&t.name==id))throw new InvalidOperationException("Authoring Marks already contains an object named '"+id+"'. Resolve the duplicate before creating a mark.");
            int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Create authored location");
            var mark=new GameObject(id);Undo.RegisterCreatedObjectUndo(mark,"Create "+id);mark.transform.SetParent(parent.transform,false);mark.transform.SetPositionAndRotation(position,rotation);
            Undo.RecordObject(host,"Bind "+id);
            if(rows.Length==1)rows[0].placement=mark.transform;
            else host.locations=(host.locations??Array.Empty<StoryLocationBinding>()).Concat(new[]{new StoryLocationBinding{id=id,placement=mark.transform}}).ToArray();
            EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);return mark.transform;
        }
        public static void Commit(StorySceneBindings host,Transform mark,Vector3 position,Quaternion rotation) {
            var scene=ValidateHost(host);
            if(mark==null||mark.gameObject.scene!=scene)throw new InvalidOperationException("A mark in this saved scene is required.");
            var parent=MarksRoot(scene,false);
            if(parent==null||!mark.IsChildOf(parent.transform))throw new InvalidOperationException("Only marks under Authoring Marks can be committed.");
            int bindings=(host.locations??Array.Empty<StoryLocationBinding>()).Count(l=>l!=null&&l.placement==mark);
            if(bindings!=1)throw new InvalidOperationException("The mark must be bound to exactly one scene location before it can be committed.");
            Undo.RecordObject(mark,"Commit authored location");mark.SetPositionAndRotation(position,rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mark);EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
