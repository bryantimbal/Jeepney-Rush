using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// The patrol car that chases the jeepney when the driver rating hits ★1.0. It follows in
    /// the jeepney's lane, closing in from behind (<see cref="PoliceChase"/>), lights flashing
    /// and siren wailing. Purely visual: the chase outcome is decided by the logic class.
    /// </summary>
    public sealed class PoliceChaseManager : MonoBehaviour
    {
        private GameObject _car, _red, _blue;
        private float _t, _sirenT, _x;
        public readonly PoliceChase Chase = new PoliceChase();
        public bool Active => Chase.Active || _arrived;
        private bool _arrived;   // pulled up behind the jeepney after a pull-over / bust
        private float _arrivedZ;

        public Vector3 HeadPosition => _car != null ? _car.transform.position + new Vector3(-0.9f, 2.2f, 1.2f) : Vector3.zero;

        private void Build()
        {
            if (_car != null) return;
            _car = ModelLibrary.Instantiate(ModelLibrary.Get("police_car", VehicleBuilder.PoliceCar), "PoliceCar", transform);
            _red = ModelLibrary.Instantiate(ModelLibrary.Get("police_lamp_red", () => VehicleBuilder.PoliceLamp(true)), "LampRed", _car.transform, false);
            _blue = ModelLibrary.Instantiate(ModelLibrary.Get("police_lamp_blue", () => VehicleBuilder.PoliceLamp(false)), "LampBlue", _car.transform, false);
            _car.SetActive(false);
        }

        public void ResetRun()
        {
            Chase.End();
            _arrived = false;
            if (_car != null) _car.SetActive(false);
        }

        public void Begin(float playerX)
        {
            Build();
            Chase.Start();
            _arrived = false;
            _x = playerX;
            _sirenT = 0;
            _car.SetActive(true);
        }

        /// <summary>After a pull-over or bust: the car parks right behind the jeepney.</summary>
        public void PullUp(float seconds = 3f)
        {
            _arrived = true;
            _arrivedZ = -6.5f;
            Invoke(nameof(Leave), seconds);
        }

        private void Leave()
        {
            _arrived = false;
            if (!Chase.Active && _car != null) _car.SetActive(false);
        }

        /// <param name="playerX">Jeepney lane position (the car follows it).</param>
        public void Tick(float dt, float playerX)
        {
            if (_car == null || !_car.activeSelf) return;
            _t += dt;
            _x = Mathf.Lerp(_x, playerX, 1f - Mathf.Exp(-dt * 2.5f));
            // gap 75 m → 0 m maps to 75 m → 6.5 m behind the jeepney's centre (the chase cam sees the last ~20 m)
            float z = _arrived ? _arrivedZ : -(6.5f + Mathf.Max(0, Chase.Gap));
            _car.transform.localPosition = new Vector3(_x, 0, z);
            bool phase = (_t * 4f) % 1f < 0.5f;
            if (_red.activeSelf != phase) _red.SetActive(phase);
            if (_blue.activeSelf == phase) _blue.SetActive(!phase);
            if (Chase.Active)
            {
                _sirenT -= dt;
                if (_sirenT <= 0) { _sirenT = 1.25f; Services.Audio.Play(Logic.Audio.Sfx.Siren, Mathf.Lerp(0.35f, 0.9f, Chase.Progress)); }
            }
        }
    }
}
