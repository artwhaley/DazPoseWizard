using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Independent normalized scene value for a physical looking, animatable control.</summary>
    [DisallowMultipleComponent]
    public sealed class PerformerControlSurface : MonoBehaviour
    {
        [SerializeField] private Transform handle;
        [SerializeField] private Transform gripPoint;
        [SerializeField, Range(0f, 1f)] private float value01 = 0.3f;
        [SerializeField] private Vector3 minimumHandleLocalPosition = new Vector3(0f, -0.045f, 0f);
        [SerializeField] private Vector3 maximumHandleLocalPosition = new Vector3(0f, 0.045f, 0f);

        public Transform Handle { get => handle; set => handle = value; }
        public Transform GripPoint { get => gripPoint != null ? gripPoint : handle; set => gripPoint = value; }
        public float Value01
        {
            get => value01;
            set
            {
                value01 = Mathf.Clamp01(value);
                ApplyValue();
            }
        }

        private void Awake() => ApplyValue();
        private void OnValidate() { value01 = Mathf.Clamp01(value01); ApplyValue(); }

        public void ConfigureHandle(Transform target, Vector3 minimum, Vector3 maximum)
        {
            handle = target;
            minimumHandleLocalPosition = minimum;
            maximumHandleLocalPosition = maximum;
            ApplyValue();
        }

        private void ApplyValue()
        {
            if (handle != null)
                handle.localPosition = Vector3.LerpUnclamped(minimumHandleLocalPosition,
                    maximumHandleLocalPosition, Mathf.Clamp01(value01));
        }
    }
}
