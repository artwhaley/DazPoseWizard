using System;
using System.Threading.Tasks;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>First-slice reach, grasp, move, and release interaction for a normalized control surface.</summary>
    [DefaultExecutionOrder(5000)]
    public sealed class LaraControlAnimator : MonoBehaviour
    {
        [SerializeField] private Transform hand;
        [SerializeField, Min(0.01f)] private float shortestMoveSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float longestMoveSeconds = 1.2f;

        private PerformerControlSurface surface;
        private TaskCompletionSource<bool> completion;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 graspPosition;
        private Quaternion graspRotation;
        private float elapsed;
        private float reachSeconds;
        private float transitionSeconds;
        private float returnSeconds;
        private float startValue;
        private float targetValue;
        private bool moving;

        public Transform Hand { get => hand; set => hand = value; }
        public bool IsMoving => moving;

        public Task SetControlAsync(PerformerControlSurface target, float normalizedValue)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (hand == null) throw new InvalidOperationException("Assign Lara's hand transform before operating a control.");
            if (float.IsNaN(normalizedValue) || float.IsInfinity(normalizedValue))
                throw new ArgumentOutOfRangeException(nameof(normalizedValue));
            if (moving) throw new InvalidOperationException("Lara is already operating a control.");

            surface = target;
            startValue = target.Value01;
            targetValue = Mathf.Clamp01(normalizedValue);
            startPosition = hand.position;
            startRotation = hand.rotation;
            Transform grip = target.GripPoint;
            graspPosition = grip != null ? grip.position : target.transform.position;
            graspRotation = grip != null ? grip.rotation : target.transform.rotation;
            float delta = Mathf.Abs(targetValue - startValue);
            transitionSeconds = Mathf.Lerp(shortestMoveSeconds, longestMoveSeconds, delta);
            reachSeconds = Mathf.Min(0.28f, transitionSeconds * 0.4f);
            returnSeconds = Mathf.Min(0.32f, transitionSeconds * 0.4f);
            elapsed = 0f;
            moving = true;
            completion = new TaskCompletionSource<bool>();
            return completion.Task;
        }

        private void LateUpdate() => AdvanceControl(Time.deltaTime);

        internal void AdvanceControl(float deltaSeconds)
        {
            if (!moving || hand == null || surface == null) return;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            elapsed += deltaSeconds;
            float movingHandleSeconds = Mathf.Max(0.01f, transitionSeconds - reachSeconds - returnSeconds);
            if (elapsed < reachSeconds)
            {
                float t = Ease(elapsed / reachSeconds);
                hand.SetPositionAndRotation(Vector3.Lerp(startPosition, graspPosition, t),
                    Quaternion.Slerp(startRotation, graspRotation, t));
                return;
            }

            if (elapsed < reachSeconds + movingHandleSeconds)
            {
                float t = Ease((elapsed - reachSeconds) / movingHandleSeconds);
                surface.Value01 = Mathf.Lerp(startValue, targetValue, t);
                Transform grip = surface.GripPoint;
                if (grip != null) graspPosition = grip.position;
                hand.SetPositionAndRotation(graspPosition, graspRotation);
                return;
            }

            surface.Value01 = targetValue;
            float release = Ease((elapsed - reachSeconds - movingHandleSeconds) / returnSeconds);
            hand.SetPositionAndRotation(Vector3.Lerp(graspPosition, startPosition, release),
                Quaternion.Slerp(graspRotation, startRotation, release));
            if (release < 1f) return;

            moving = false;
            hand.SetPositionAndRotation(startPosition, startRotation);
            completion.TrySetResult(true);
            completion = null;
            surface = null;
        }

        private static float Ease(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
