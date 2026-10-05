using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Instance adapter for reviewed heel support; mutable calibration is cloned per actor.</summary>
    [DisallowMultipleComponent]
    public sealed class FootwearPoseDriver : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer body;
        [SerializeField] private LaraHeelReview reviewedSupport;
        private PerformerFootwearProfile _profileInstance;
        private bool _bindingsConfigured;
        private bool _shoeSupportActive;

        public bool IsFootwearActive => _profileInstance != null && _shoeSupportActive;
        public float StandingLift => _profileInstance != null ? _profileInstance.standingHeight : 0f;
        public float FootShrink => _profileInstance != null ? _profileInstance.footShrink : 0f;

        public bool Configure(SkinnedMeshRenderer renderer, PerformerFootwearProfile source,
            WardrobeFootwearState fit, out string error)
        {
            if (renderer == null || renderer.sharedMesh == null)
            { error = "Canonical body renderer/mesh is unavailable."; return false; }
            body = renderer;
            _shoeSupportActive = fit != null && fit.FootwearActive && source != null;
            if (reviewedSupport == null) reviewedSupport = GetComponent<LaraHeelReview>();
            if (reviewedSupport == null) reviewedSupport = gameObject.AddComponent<LaraHeelReview>();
            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var bone in renderer.bones)
                if (bone != null && !bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);
            if (!bones.TryGetValue("lFoot", out var leftFoot) || !bones.TryGetValue("rFoot", out var rightFoot) ||
                !bones.TryGetValue("lToe", out var leftToe) || !bones.TryGetValue("rToe", out var rightToe))
            { error = "Reviewed footwear support needs lFoot/rFoot and lToe/rToe canonical bones."; return false; }
            Transform root = renderer.rootBone != null ? renderer.rootBone.parent : null;
            if (root == null) { error = "Canonical rig root above the body root bone is missing."; return false; }
            if (!_bindingsConfigured)
            {
                reviewedSupport.Configure(renderer, root, leftFoot, rightFoot, leftToe, rightToe, 0f);
                _bindingsConfigured = true;
            }
            DestroyProfileInstance();
            if (fit != null && fit.BentFootPoseActive)
            {
                if (source != null)
                {
                    _profileInstance = Instantiate(source);
                    _profileInstance.name = source.name + " (Performer Wardrobe Instance)";
                    _profileInstance.standingHeight = fit.StandingLift;
                    _profileInstance.footShrink = fit.FootShrink;
                }
                reviewedSupport.SetReferenceFootShape(1f);
                reviewedSupport.SetFootwear(_profileInstance, fit.FootwearActive, fit.BentFootPoseActive);
            }
            else
            {
                reviewedSupport.SetFootwear(null, false, false);
                reviewedSupport.SetReferenceFootShape(0f);
                reviewedSupport.RigHeight = 0f;
            }
            error = null;
            return true;
        }

        private void DestroyProfileInstance()
        {
            if (_profileInstance == null) return;
            if (Application.isPlaying) Destroy(_profileInstance); else DestroyImmediate(_profileInstance);
            _profileInstance = null;
        }

        private void OnDestroy()
        {
            DestroyProfileInstance();
            if (reviewedSupport != null) reviewedSupport.SetFootwear(null);
        }
    }
}
