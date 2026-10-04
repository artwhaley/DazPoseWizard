using UnityEngine;
using System;

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
        private int transitionRevision;

        public Transform Handle { get => handle; set => handle = value; }
        public Transform GripPoint { get => gripPoint != null ? gripPoint : handle; set => gripPoint = value; }
        public float Value01
        {
            get => value01;
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Approximately(value01, next)) return;
                value01 = next;
                ApplyValue();
                RaiseValueChanged();
            }
        }

        public event Action<float> ValueChanged;

        private void Awake() => ApplyValue();
        private void OnValidate() { value01 = Mathf.Clamp01(value01); ApplyValue(); }

        public void ConfigureHandle(Transform target, Vector3 minimum, Vector3 maximum)
        {
            handle = target;
            minimumHandleLocalPosition = minimum;
            maximumHandleLocalPosition = maximum;
            ApplyValue();
        }

        public void MoveTo(float target, float seconds) => ObserveMove(MoveToAsync(target, seconds));

        /// <summary>Animates the visible control on scene time and reports each normalized value change.</summary>
        public async Awaitable MoveToAsync(float target, float seconds)
        {
            if (float.IsNaN(target) || float.IsInfinity(target))
                throw new ArgumentOutOfRangeException(nameof(target), "Control value must be finite.");
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Duration must be finite and nonnegative.");

            target = Mathf.Clamp01(target);
            int revision = ++transitionRevision;
            float start = value01;
            if (seconds <= 0f) { Value01 = target; return; }
            float elapsed = 0f;
            while (elapsed < seconds && revision == transitionRevision && this != null)
            {
                await Awaitable.NextFrameAsync();
                elapsed = Mathf.Min(seconds, elapsed + Mathf.Max(0f, Time.deltaTime));
                Value01 = Mathf.LerpUnclamped(start, target, Mathf.Clamp01(elapsed / seconds));
                if (Time.deltaTime <= 0f) await Awaitable.NextFrameAsync();
            }
            if (revision == transitionRevision && this != null) Value01 = target;
        }

        private async void ObserveMove(Awaitable move)
        {
            try { await move; }
            catch (Exception exception) { Debug.LogWarning("Control surface move failed: " + exception.Message, this); }
        }

        private void OnDisable() => transitionRevision++;

        private void ApplyValue()
        {
            if (handle != null)
                handle.localPosition = Vector3.LerpUnclamped(minimumHandleLocalPosition,
                    maximumHandleLocalPosition, Mathf.Clamp01(value01));
        }

        private void RaiseValueChanged()
        {
            Action<float> handlers = ValueChanged;
            if (handlers == null) return;
            foreach (Action<float> handler in handlers.GetInvocationList())
                try { handler(value01); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
