using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    internal struct MagicTargetSample
    {
        public Vector3 Center;
        public Vector3 BaseCenter;
        public float Radius;
        public float Height;
    }

    /// <summary>Renderer-derived target dimensions cached in target-local space.</summary>
    internal sealed class MagicTargetGeometry
    {
        private readonly Transform _target;
        private readonly Vector3 _localCenter;
        private readonly Vector3 _localExtents;

        public Transform Target => _target;

        public MagicTargetGeometry(Transform target)
        {
            _target = target != null ? target : throw new System.ArgumentNullException(nameof(target));
            if (TryBuildRendererBounds(target, out Vector3 center, out Vector3 extents))
            {
                _localCenter = center;
                _localExtents = extents;
            }
            else
            {
                _localCenter = Vector3.zero;
                _localExtents = new Vector3(0.5f, 0.9f, 0.5f);
            }
        }

        public MagicTargetSample Sample(float scale, float padding)
        {
            Vector3 center = _target.TransformPoint(_localCenter);
            Vector3 x = _target.TransformVector(Vector3.right * _localExtents.x);
            Vector3 y = _target.TransformVector(Vector3.up * _localExtents.y);
            Vector3 z = _target.TransformVector(Vector3.forward * _localExtents.z);
            Vector3 worldExtents = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));

            float safeScale = Mathf.Max(0.01f, scale);
            float safePadding = Mathf.Max(0f, padding);
            return new MagicTargetSample
            {
                Center = center,
                BaseCenter = center - Vector3.up * worldExtents.y,
                Radius = Mathf.Max(0.05f, Mathf.Max(worldExtents.x, worldExtents.z) * safeScale + safePadding),
                Height = Mathf.Max(0.1f, worldExtents.y * 2f * safeScale + safePadding * 2f)
            };
        }

        private static bool TryBuildRendererBounds(Transform target, out Vector3 center, out Vector3 extents)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Bounds localBounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (!IsUsefulRenderer(renderer)) continue;
                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 local = target.InverseTransformPoint(world);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else localBounds.Encapsulate(local);
                }
            }

            center = hasBounds ? localBounds.center : Vector3.zero;
            extents = hasBounds ? localBounds.extents : Vector3.zero;
            return hasBounds;
        }

        private static bool IsUsefulRenderer(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) return false;
            if (renderer.GetComponentInParent<VisualEffect>() != null) return false;
            if (renderer.GetComponentInParent<ParticleSystem>() != null) return false;
            return true;
        }
    }
}
