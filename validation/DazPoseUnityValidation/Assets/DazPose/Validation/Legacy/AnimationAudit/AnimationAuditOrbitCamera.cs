using UnityEngine;

namespace DazPose.AnimationAudit
{
    public sealed class AnimationAuditOrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 8f;
        public float minDistance = 3f;
        public float maxDistance = 18f;
        public float orbitSpeed = 5f;
        public float zoomSpeed = 1.5f;
        public float targetHeight = 0.9f;
        private float _yaw;
        private float _pitch = 14f;

        private void Start()
        {
            if (target == null) return;
            Vector3 offset = transform.position - (target.position + Vector3.up * targetHeight);
            distance = offset.magnitude;
            var angles = Quaternion.LookRotation(-offset.normalized, Vector3.up).eulerAngles;
            _yaw = angles.y;
            _pitch = Mathf.Clamp(angles.x > 180f ? angles.x - 360f : angles.x, -5f, 70f);
            ApplyView();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            bool overPreview = Input.mousePosition.x > AnimationAuditHarness.PanelWidth + 24f;
            if (overPreview && Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * orbitSpeed;
                _pitch -= Input.GetAxis("Mouse Y") * orbitSpeed;
                _pitch = Mathf.Clamp(_pitch, -5f, 70f);
            }
            if (overPreview) distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * zoomSpeed, minDistance, maxDistance);
            ApplyView();
        }

        private void ApplyView()
        {
            Vector3 focus = target.position + Vector3.up * targetHeight;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = focus + rotation * new Vector3(0f, 0f, -distance);
            transform.rotation = rotation;
        }
    }
}
