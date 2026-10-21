using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class LaraSecondOutfitReviewWindow : EditorWindow
    {
        private LaraWardrobe outfit;
        private Vector2 scroll;
        private float dissolve;
        private readonly System.Collections.Generic.Dictionary<string,bool> opacitySections=new System.Collections.Generic.Dictionary<string,bool>();
        public static void ShowFor(LaraWardrobe wardrobe)
        {var window=GetWindow<LaraSecondOutfitReviewWindow>("Lara Outfit Review");window.outfit=wardrobe;window.Show();}
        [MenuItem("Tools/DAZ Pose/Development/Lara Outfit Review Controls")]
        public static void Open()=>ShowFor(UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>());
        private void OnGUI()
        {
            outfit=(LaraWardrobe)EditorGUILayout.ObjectField("Lara wardrobe",outfit,typeof(LaraWardrobe),true);
            if(outfit==null)return;
            scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUI.BeginChangeCheck();
            outfit.ShowClothes=EditorGUILayout.Toggle("Show outfit",outfit.ShowClothes);
            outfit.ShowGlossShells=EditorGUILayout.Toggle("Gloss layers",outfit.ShowGlossShells);
            var inspection=outfit.transform.Find("Outfit Inspection Light");
            if(inspection!=null)inspection.gameObject.SetActive(EditorGUILayout.Toggle("Inspection light",inspection.gameObject.activeSelf));
            foreach(var piece in outfit.Pieces)piece.visible=EditorGUILayout.ToggleLeft(piece.name,piece.visible);
            EditorGUILayout.Space();
            foreach(var piece in outfit.Pieces)
            {
                var materials=System.Array.FindAll(piece.renderer.sharedMaterials,m=>m!=null && m.HasProperty("_Alpha") &&
                    (m.HasProperty("_SurfaceType") && m.GetFloat("_SurfaceType")>.5f || m.HasProperty("_AlphaMap") && m.GetTexture("_AlphaMap")!=null));
                if(materials.Length==0)continue;
                opacitySections.TryGetValue(piece.name,out bool expanded);
                opacitySections[piece.name]=EditorGUILayout.Foldout(expanded,piece.name+" opacity",true);
                if(!opacitySections[piece.name])continue;
                foreach(var material in materials)
                {
                    EditorGUI.BeginChangeCheck();float value=EditorGUILayout.Slider(material.name,material.GetFloat("_Alpha"),0,1);
                    if(EditorGUI.EndChangeCheck()){Undo.RecordObject(material,"Wardrobe opacity");material.SetFloat("_Alpha",value);EditorUtility.SetDirty(material);}
                }
            }
            EditorGUILayout.Space();var anatomy=outfit.GetComponent<LaraAnatomyControls>();
            if(anatomy!=null)
            {
                anatomy.LaraBreastsMeshPreview=EditorGUILayout.Slider("Breasts",anatomy.LaraBreastsMeshPreview,0,1);
                anatomy.Nipples=EditorGUILayout.Slider("Nipples",anatomy.Nipples,0,1);
                anatomy.CapturedOpening=EditorGUILayout.Slider("Opening",anatomy.CapturedOpening,0,1);
            }
            var body=outfit.Body;
            ShapeSlider(body,"Breathing","Genesis8Female__EX_Breathe",0,300);
            ShapeSlider(body,"Belly breathing","Genesis8Female__EX_BreatheBelly",0,300);
            ShapeSlider(body,"Left blink","Genesis8Female__eCTRLEyesClosedL",0,100);
            ShapeSlider(body,"Speech AA","Genesis8Female__eCTRLvAA",0,100);
            var heel=outfit.GetComponent<LaraHeelReview>();
            if(heel!=null)
            {
                EditorGUILayout.Space();var data=new SerializedObject(heel);data.Update();
                EditorGUILayout.PropertyField(data.FindProperty("additionalFootPitch"),new GUIContent("Foot pitch"));
                EditorGUILayout.PropertyField(data.FindProperty("additionalToePitch"),new GUIContent("Toe pitch"));
                EditorGUILayout.PropertyField(data.FindProperty("floorSupport"),new GUIContent("Floor support"));
                data.ApplyModifiedProperties();
                EditorGUI.BeginChangeCheck();
                float height=EditorGUILayout.Slider("Standing height (m)",heel.RigHeight,0,.3f);
                float shrink=EditorGUILayout.Slider("Foot surface shrink (%)",heel.FootShrink*100,0,10);
                if(EditorGUI.EndChangeCheck())
                {
                    if(heel.Footwear!=null)Undo.RecordObject(heel.Footwear,"Footwear fit");
                    heel.RigHeight=height;heel.FootShrink=shrink/100;
                    if(heel.Footwear!=null)EditorUtility.SetDirty(heel.Footwear);
                }
                EditorGUILayout.HelpBox("Fit shrinks Lara's foot surface only. The shoe profile stores height and heel/toe floor contacts. Floor support blends out during swing and seating; seated contacts and heel-specific animation still need review.",MessageType.Info);
            }
            using(new EditorGUI.DisabledScope(Application.isPlaying))dissolve=EditorGUILayout.Slider("Dissolve",dissolve,0,1);
            bool changed=EditorGUI.EndChangeCheck();EditorGUILayout.EndScrollView();
            if(!changed)return;
            anatomy?.Apply();heel?.Apply();
            if(!Application.isPlaying)LaraCandidateRenderValidation.SetDissolve(body,dissolve);
            outfit.Apply();EditorUtility.SetDirty(outfit);if(anatomy!=null)EditorUtility.SetDirty(anatomy);
            if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(outfit.gameObject.scene);
            SceneView.RepaintAll();
        }
        private static void ShapeSlider(SkinnedMeshRenderer body,string label,string name,float min,float max)
        {int index=body.sharedMesh.GetBlendShapeIndex(name);if(index>=0)body.SetBlendShapeWeight(index,EditorGUILayout.Slider(label,body.GetBlendShapeWeight(index),min,max));}
        private void OpacitySlider(string label,string pieceName,string surface)
        {
            foreach(var piece in outfit.Pieces)if(piece.name==pieceName)
            {
                var material=System.Array.Find(piece.renderer.sharedMaterials,m=>m.name.EndsWith("_"+surface,System.StringComparison.Ordinal));
                if(material==null || !material.HasProperty("_Alpha"))return;
                EditorGUI.BeginChangeCheck();float value=EditorGUILayout.Slider(label,material.GetFloat("_Alpha"),0,1);
                if(!EditorGUI.EndChangeCheck())return;
                Undo.RecordObject(material,label);material.SetFloat("_Alpha",value);EditorUtility.SetDirty(material);return;
            }
        }
    }
}
