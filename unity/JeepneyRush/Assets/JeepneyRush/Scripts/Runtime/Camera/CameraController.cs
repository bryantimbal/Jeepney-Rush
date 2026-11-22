using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Third-person chase camera for portrait play: behind and above the jeepney,
    /// smoothly following lanes and jumps, tilting into lane changes, widening FOV with
    /// speed, and shaking on collisions. Also provides the main-menu orbit.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0, 6.8f, -10.5f);
        public float lookAhead = 16f;
        public float lookHeight = 0.8f;
        [Tooltip("Horizontal field of view kept for any aspect ratio so all three lanes stay visible.")]
        public float horizontalFov = 62f;

        private Camera _cam;
        private Vector3 _pos;
        private float _roll, _shake, _shakeT, _fovBoost;
        private float _baseFov = 60f;
        private int _lastW, _lastH;
        /// <summary>Driver's-seat view: the camera rides in the jeepney body at the driver's eye.</summary>
        public bool DriverView;
        public Transform DriverAnchor;
        [Tooltip("Horizontal field of view from the driver's seat.")]
        public float driverHorizontalFov = 84f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 260f;
            _pos = transform.position;
            UpdateFov();
        }

        private void UpdateFov()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            float aspect = Mathf.Max(0.3f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float v = 2f * Mathf.Atan(Mathf.Tan(horizontalFov * 0.5f * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
            _baseFov = Mathf.Clamp(v, 50f, 78f);
        }

        public void AddShake(float amount) => _shake = Mathf.Max(_shake, amount);

        public void Snap(Vector3 playerPos)
        {
            _pos = new Vector3(playerPos.x * 0.55f, offset.y, offset.z);
            transform.position = _pos;
            transform.LookAt(new Vector3(playerPos.x * 0.8f, lookHeight, lookAhead));
        }

        /// <param name="speed01">0..1 speed factor (FOV boost).</param>
        /// <param name="lateralVelocity">player X velocity for the lane-change tilt.</param>
        public void Follow(Vector3 player, float lateralVelocity, float speed01, float dt)
        {
            if (Screen.width != _lastW || Screen.height != _lastH) UpdateFov();
            if (DriverView && DriverAnchor != null)
            {
                _cam.nearClipPlane = 0.04f;
                _shakeT += dt;
                var sh = Vector3.zero;
                if (_shake > 0.001f) { sh = new Vector3(Mathf.Sin(_shakeT * 61f), Mathf.Cos(_shakeT * 47f) * 0.7f, 0) * _shake * 0.25f; _shake *= Mathf.Pow(0.02f, dt); }
                var e = Logic.Geometry.CockpitBuilder.Eye;
                transform.position = DriverAnchor.TransformPoint(new Vector3(e.X, e.Y, e.Z)) + sh;
                // look a little down the road and slightly toward where we're steering
                float yaw = Mathf.Clamp(lateralVelocity * 0.8f, -6f, 6f);
                transform.rotation = DriverAnchor.rotation * Quaternion.Euler(Logic.Geometry.CockpitBuilder.PitchDeg, yaw + 2f, 0);
                float aspect = Mathf.Max(0.3f, (float)Screen.width / Mathf.Max(1, Screen.height));
                float v = 2f * Mathf.Atan(Mathf.Tan(driverHorizontalFov * 0.5f * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
                _fovBoost = Mathf.Lerp(_fovBoost, speed01 * 5f, 1f - Mathf.Exp(-dt * 3f));
                _cam.fieldOfView = Mathf.Clamp(v, 60f, Logic.Geometry.CockpitBuilder.MaxVerticalFov) + _fovBoost; // tall phones: don't stare at the ceiling
                _pos = new Vector3(player.x * 0.55f, offset.y, offset.z); // ready for switching back
                return;
            }
            _cam.nearClipPlane = 0.3f;
            var target = new Vector3(player.x * 0.55f, offset.y + player.y * 0.35f, offset.z);
            _pos = Vector3.Lerp(_pos, target, 1f - Mathf.Exp(-dt * 5f));
            _roll = Mathf.Lerp(_roll, Mathf.Clamp(-lateralVelocity * 0.25f, -3f, 3f), 1f - Mathf.Exp(-dt * 6f));
            _shakeT += dt;
            var shake = Vector3.zero;
            if (_shake > 0.001f)
            {
                shake = new Vector3(Mathf.Sin(_shakeT * 61f), Mathf.Cos(_shakeT * 47f) * 0.7f, 0) * _shake;
                _shake *= Mathf.Pow(0.02f, dt);
            }
            transform.position = _pos + shake;
            var look = new Vector3(player.x * 0.8f, lookHeight + player.y * 0.25f, lookAhead);
            transform.rotation = Quaternion.LookRotation(look - transform.position) * Quaternion.Euler(0, 0, _roll);
            _fovBoost = Mathf.Lerp(_fovBoost, speed01 * 8f, 1f - Mathf.Exp(-dt * 3f));
            _cam.fieldOfView = _baseFov + _fovBoost;
        }

        /// <summary>Slow showcase orbit around the jeepney (main menu / garage).</summary>
        public void Orbit(Vector3 focus, float time, float radius = 8.6f)
        {
            float a = Mathf.Sin(time * 0.25f) * 0.75f + 0.55f;
            transform.position = focus + new Vector3(Mathf.Sin(a) * radius - 1.2f, 2.6f + Mathf.Sin(time * 0.4f) * 0.3f, Mathf.Cos(a) * radius + 0.3f);
            transform.LookAt(focus + new Vector3(0, 1.2f, 0.4f));
            _cam.fieldOfView = Mathf.Clamp(_baseFov * 0.85f, 45f, 70f);
        }
    }
}
