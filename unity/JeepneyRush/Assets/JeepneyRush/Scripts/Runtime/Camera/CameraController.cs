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
        /// <summary>Driver's eye in the anchor's local space (depends on the jeepney: the modern PUV sits further forward).</summary>
        public Vector3 DriverEye = new Vector3(-0.45f, 1.93f, 0.72f);
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
            // the camera's own viewport (it is letterboxed to 9:16 while taking store screenshots)
            _lastW = _cam.pixelWidth;
            _lastH = _cam.pixelHeight;
            float aspect = Mathf.Max(0.3f, (float)_cam.pixelWidth / Mathf.Max(1, _cam.pixelHeight));
            float v = 2f * Mathf.Atan(Mathf.Tan(horizontalFov * 0.5f * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
            _baseFov = Mathf.Clamp(v, 50f, 78f);
        }

        public void AddShake(float amount) => _shake = Mathf.Max(_shake, amount);

        // Effects never change the view in a single frame (that read as a flicker): an impulse
        // raises a TARGET, the actual value eases toward it (critically damped) and the target
        // fades out, so the zoom swells in over ~0.1 s and settles back over ~0.6 s.
        private float _kick, _kickVel, _kickTarget, _tilt, _tiltVel, _tiltTarget;
        public const float MaxKick = 5f, MaxTilt = 2f;
        /// <summary>Gentle field-of-view swell (degrees) for near misses, combos and Turbo.</summary>
        public void Kick(float fov) { if (Effects) _kickTarget = Mathf.Min(MaxKick, Mathf.Max(_kickTarget, fov) + Mathf.Min(_kickTarget, fov) * 0.25f); }
        /// <summary>Soft roll (degrees, + = right) for overtakes and swerves.</summary>
        public void Tilt(float deg) { if (Effects) _tiltTarget = Mathf.Clamp(_tiltTarget + deg, -MaxTilt, MaxTilt); }

        private void UpdateEffects(float dt)
        {
            if (dt <= 0) return;
            _kick = Mathf.SmoothDamp(_kick, _kickTarget, ref _kickVel, 0.08f, Mathf.Infinity, dt);
            _tilt = Mathf.SmoothDamp(_tilt, _tiltTarget, ref _tiltVel, 0.12f, Mathf.Infinity, dt);
            _kickTarget *= Mathf.Exp(-dt * 4f);
            _tiltTarget *= Mathf.Exp(-dt * 3f);
            if (_kickTarget < 0.01f) _kickTarget = 0f;
        }
        private static bool Effects => Services.Save == null || Services.Save.Data.cameraEffects;

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
            if (_cam.pixelWidth != _lastW || _cam.pixelHeight != _lastH) UpdateFov();
            UpdateEffects(dt);
            if (DriverView && DriverAnchor != null)
            {
                _cam.nearClipPlane = 0.04f;
                _shakeT += dt;
                var sh = Vector3.zero;
                if (_shake > 0.001f) { sh = new Vector3(Mathf.Sin(_shakeT * 61f), Mathf.Cos(_shakeT * 47f) * 0.7f, 0) * _shake * 0.25f; _shake *= Mathf.Pow(0.02f, dt); }
                transform.position = DriverAnchor.TransformPoint(DriverEye) + sh;
                // look a little down the road and slightly toward where we're steering
                float yaw = Mathf.Clamp(lateralVelocity * 0.8f, -6f, 6f);
                transform.rotation = DriverAnchor.rotation * Quaternion.Euler(Logic.Geometry.CockpitBuilder.PitchDeg, yaw + 2f, 0);
                float aspect = Mathf.Max(0.3f, (float)_cam.pixelWidth / Mathf.Max(1, _cam.pixelHeight));
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
            transform.rotation = Quaternion.LookRotation(look - transform.position) * Quaternion.Euler(0, 0, _roll + _tilt);
            _fovBoost = Mathf.Lerp(_fovBoost, speed01 * 8f, 1f - Mathf.Exp(-dt * 3f));
            _cam.fieldOfView = _baseFov + _fovBoost + _kick;
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
