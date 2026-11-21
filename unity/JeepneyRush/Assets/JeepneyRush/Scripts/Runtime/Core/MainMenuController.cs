using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Main menu scene: the selected jeepney idles on a Manila street (wheels turning,
    /// diesel puffs) while the camera orbits it. The garage swaps the livery live.
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
        private float _t, _swapT = 1f, _yaw;
        private bool _garage;
        private Camera _cam;

        /// <summary>
        /// Garage showroom: the jeepney spins on a turntable in the upper part of the screen
        /// (above the garage card) and pops in whenever another model is selected.
        /// </summary>
        public void SetGarageMode(bool on)
        {
            _garage = on;
            _yaw = 0;
            _jeepRoot.localRotation = Quaternion.identity;
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
            foreach (var w in _wheels) w.localRotation = Quaternion.Euler(_t * cruise / JeepneyBuilder.WheelRadius * Mathf.Rad2Deg, 0, 0);
            _body.transform.localPosition = new Vector3(0, Mathf.Sin(_t * 9f) * 0.008f, 0);
            _fx.Tick(dt, _jeepRoot.position, cruise, 0);
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
                _yaw += dt * 28f;
                _jeepRoot.localRotation = Quaternion.Euler(0, _yaw + 150f, 0);
                if (_cam == null) _cam = _camera.GetComponent<Camera>();
                var focus = _jeepRoot.position;
                _camera.transform.position = Vector3.Lerp(_camera.transform.position, focus + new Vector3(0, 3.4f, -12.5f), 1f - Mathf.Exp(-dt * 4f));
                // aim below the jeepney so it sits in the upper part of the screen, above the garage card
                _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, Quaternion.LookRotation(focus + new Vector3(0, -2.9f, 0) - _camera.transform.position), 1f - Mathf.Exp(-dt * 4f));
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 62f, 1f - Mathf.Exp(-dt * 4f));
            }
            else _camera.Orbit(_jeepRoot.position, _t);
        }
    }
}
