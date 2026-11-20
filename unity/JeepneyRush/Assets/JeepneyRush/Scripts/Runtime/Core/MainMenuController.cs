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
        private float _t;

        private void Awake()
        {
            Services.Game.Register(this);
            Services.Audio.EnsureLoaded(); // when Play is pressed directly in this scene
            EnvironmentSetup.Apply();
            _camera = EnvironmentSetup.EnsureCamera().GetComponent<CameraController>();
            var roadGo = new GameObject("RoadManager");
            roadGo.transform.SetParent(transform, false);
            _road = roadGo.AddComponent<RoadManager>();
            _road.Build();
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
            var wp = JeepneyBuilder.WheelPositions;
            _wheels = new Transform[wp.Length];
            for (int i = 0; i < wp.Length; i++)
            {
                _wheels[i] = ModelLibrary.Instantiate(ModelLibrary.Wheel(), "Wheel" + i, _jeepRoot).transform;
                _wheels[i].localPosition = new Vector3(wp[i].X, wp[i].Y, wp[i].Z);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            const float cruise = 5f;
            _road.Scroll(cruise * dt);
            foreach (var w in _wheels) w.localRotation = Quaternion.Euler(_t * cruise / JeepneyBuilder.WheelRadius * Mathf.Rad2Deg, 0, 0);
            _body.transform.localPosition = new Vector3(0, Mathf.Sin(_t * 38f) * 0.01f, 0);
            _fx.Tick(dt, _jeepRoot.position, cruise, 0);
            _camera.Orbit(_jeepRoot.position, _t);
        }
    }
}
