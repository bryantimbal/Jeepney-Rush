using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Main menu scene: the selected jeepney idles on the selected route (wheels turning,
    /// diesel puffs) while the camera orbits it. The garage is a Filipino talyer where the
    /// chosen jeepney stands parked (not spinning) and swaps livery live.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private CameraController _camera;
        private RoadManager _road;
        private EffectsManager _fx;
        private Transform _jeepRoot;
        private GameObject _body;
        private Transform[] _wheels;
        private string _livery;
        private float _t, _swapT = 1f, _spin;
        /// <summary>Turntable speed in the garage (degrees per second): slow enough to admire every side.</summary>
        public const float GarageSpinSpeed = 24f;
        private bool _garage;
        private Camera _cam;

        private Transform _garageRoot;
        private static readonly Vector3 StreetSpot = new Vector3(-1.6f, 0, 0);
        /// <summary>The talyer stands well away from the menu street (out of sight of the road).</summary>
        private static readonly Vector3 GarageSpot = new Vector3(0, 0, -600f);
        /// <summary>Parked at a three-quarter angle toward the garage door (and the camera).</summary>
        public const float GarageYaw = 152f;

        /// <summary>
        /// Garage: the jeepney turns slowly on a turntable inside a Filipino talyer (hollow-block walls,
        /// GI sheets, tyres, tools, Santo Niño altar), in the upper part of the screen above the
        /// garage card, and pops in whenever another model is selected.
        /// </summary>
        public void SetGarageMode(bool on)
        {
            _garage = on;
            if (_garageRoot == null) BuildGarage();
            _garageRoot.gameObject.SetActive(on);
            _spin = 0;
            _jeepRoot.localPosition = on ? GarageSpot : StreetSpot;
            _jeepRoot.localRotation = on ? Quaternion.Euler(0, GarageYaw, 0) : Quaternion.identity;
            if (on && _camera != null)
            {
                // cut straight into the garage (no long glide across the map)
                var focus = _jeepRoot.position;
                _camera.transform.position = focus + GarageCam;
                _camera.transform.rotation = Quaternion.LookRotation(focus + GarageLook - _camera.transform.position);
            }
        }

        private static readonly Vector3 GarageCam = new Vector3(0, 3.4f, -12.5f), GarageLook = new Vector3(0, -2.9f, 0);

        private void BuildGarage()
        {
            _garageRoot = ModelLibrary.Instantiate(ModelLibrary.Get("garage", GarageBuilder.Build), "Garage", transform, castShadows: false).transform;
            _garageRoot.localPosition = GarageSpot;
            // warm fluorescent / bulb light inside the workshop
            foreach (var p in new[] { new Vector3(-2.6f, 4.3f, -1.5f), new Vector3(2.6f, 4.3f, 3.5f), new Vector3(0, 4.6f, 6.5f) })
            {
                var lg = new GameObject("GarageLight").AddComponent<Light>();
                lg.transform.SetParent(_garageRoot, false);
                lg.transform.localPosition = p;
                lg.type = LightType.Point;
                lg.range = 9f;
                lg.intensity = 1.6f;
                lg.color = p.z > 5f ? new Color(1f, 0.85f, 0.6f) : new Color(0.92f, 0.97f, 1f);
                lg.shadows = LightShadows.None;
            }
        }

        private void Awake()
        {
            Services.Game.Register(this);
            Services.Audio.EnsureLoaded(); // when Play is pressed directly in this scene
            var env = (EnvironmentKind)Services.Save.Data.selectedEnvironment;
            EnvironmentSetup.Apply(env);
            _camera = EnvironmentSetup.EnsureCamera().GetComponent<CameraController>();
            var roadGo = new GameObject("RoadManager");
            roadGo.transform.SetParent(transform, false);
            RoadCurve.Shape = null; // the menu street is straight and dry
            _road = roadGo.AddComponent<RoadManager>();
            _road.Build(env);
            var fxGo = new GameObject("Effects");
            fxGo.transform.SetParent(transform, false);
            _fx = fxGo.AddComponent<EffectsManager>();
            _fx.Build();
            _jeepRoot = new GameObject("MenuJeepney").transform;
            _jeepRoot.SetParent(transform, false);
            _jeepRoot.localPosition = new Vector3(-1.6f, 0, 0);
            ShowLivery(Services.Save.Data.selectedJeepney);
        }

        private void Start()
        {
            GraphicsQuality.SetMenuMode(true);
            if (Services.Game.State == GameState.Loading || Services.Game.State == GameState.GameOver) Services.Game.SetState(GameState.MainMenu);
            Services.UI.ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (Services.Game != null) Services.Game.Unregister(this);
        }

        public void ShowLivery(string id)
        {
            if (_livery == id) return;
            _livery = id;
            if (_body != null) Destroy(_body);
            if (_wheels != null) foreach (var w in _wheels) Destroy(w.gameObject);
            _body = ModelLibrary.Instantiate(ModelLibrary.Jeepney(id), "Body", _jeepRoot);
            if (_wheels != null) { _swapT = 0f; _fx?.Confetti(_jeepRoot.position + Vector3.up * 2.5f, 30); }
            var wp = JeepneyBuilder.WheelPositions;
            _wheels = new Transform[wp.Length];
            for (int i = 0; i < wp.Length; i++)
            {
                _wheels[i] = ModelLibrary.Instantiate(ModelLibrary.WheelFor(id), "Wheel" + i, _jeepRoot).transform;
                _wheels[i].localPosition = new Vector3(wp[i].X, wp[i].Y, wp[i].Z);
            }
        }

        /// <summary>Routes screen: show the newly selected route behind the menu.</summary>
        public void ShowRoute(EnvironmentKind env)
        {
            if (_road.Environment == env) return;
            EnvironmentSetup.Apply(env);
            _road.Rebuild(env);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            const float cruise = 5f;
            _road.Scroll(cruise * dt);
            // in the garage: wheels still, only the idling engine shakes the body a little
            if (!_garage) foreach (var w in _wheels) w.localRotation = Quaternion.Euler(_t * cruise / JeepneyBuilder.WheelRadius * Mathf.Rad2Deg, 0, 0);
            _body.transform.localPosition = new Vector3(0, Mathf.Sin(_t * (_garage ? 14f : 9f)) * (_garage ? 0.005f : 0.008f), 0);
            _fx.Tick(dt, _jeepRoot.position, _garage ? 0f : cruise, 0);
            // pop-in when a new model is shown (ease-out-back)
            if (_swapT < 1f)
            {
                _swapT = Mathf.Min(1f, _swapT + dt * 2.5f);
                float k = _swapT - 1f;
                float s = 1f + 2.2f * k * k * k + 1.2f * k * k;
                _jeepRoot.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, s);
            }
            if (_garage)
            {
                // the jeepney turns slowly on the talyer's turntable so every side of the design shows
                _spin += dt * GarageSpinSpeed;
                _jeepRoot.localRotation = Quaternion.Euler(0, GarageYaw + _spin, 0);
                if (_cam == null) _cam = _camera.GetComponent<Camera>();
                var focus = _jeepRoot.position;
                // a fixed shot from the garage door
                _camera.transform.position = Vector3.Lerp(_camera.transform.position, focus + GarageCam, 1f - Mathf.Exp(-dt * 4f));
                // aim below the jeepney so it sits in the upper part of the screen, above the garage card
                _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, Quaternion.LookRotation(focus + GarageLook - _camera.transform.position), 1f - Mathf.Exp(-dt * 4f));
                float garageFov = _cam.aspect > 1f ? 40f : 62f; // landscape: same framing without a fish-eye
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, garageFov, 1f - Mathf.Exp(-dt * 4f));
            }
            else _camera.Orbit(_jeepRoot.position, _t);
        }
    }
}
