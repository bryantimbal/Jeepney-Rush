using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// MMDA checkpoint that appears ahead after reckless driving (a crash). The enforcer
    /// stands on the sidewalk next to his patrol bike (flashing red/blue sirens) — he never
    /// blocks a lane. When the jeepney passes, the session checks its speed
    /// (<see cref="EnforcerRules"/>): slow = thanked, fast = ticket.
    /// </summary>
    public sealed class EnforcerManager : MonoBehaviour
    {
        private GameObject _root, _red, _blue;
        private float _t, _cooldown;
        public bool Active { get; private set; }
        public float Z => Active ? _root.transform.localPosition.z : float.MaxValue;
        public Vector3 HeadPosition => _root.transform.position + new Vector3(0, 2.6f, 0);
        /// <summary>Raised once when the checkpoint passes the jeepney.</summary>
        public System.Action Passed;
        private bool _passed;

        public EnforcerKind Kind { get; private set; }
        public string CheckpointName => Enforcers.Checkpoint(Kind);

        /// <summary>MMDA only in Metro Manila (Manila streets, EDSA); police or city traffic enforcers elsewhere.</summary>
        public void Init(EnvironmentKind env = EnvironmentKind.Manila)
        {
            var kind = Enforcers.For(env);
            if (_root != null && kind == Kind) return;
            if (_root != null) Destroy(_root);
            Kind = kind;
            _root = new GameObject(CheckpointName.Replace(' ', '_'));
            _root.transform.SetParent(transform, false);
            ModelLibrary.Instantiate(ModelLibrary.Get("enforcer_" + kind, () => PropBuilder.Enforcer(kind)), "Enforcer", _root.transform);
            _red = ModelLibrary.Instantiate(ModelLibrary.Get("siren_red", () => PropBuilder.Siren(true)), "SirenRed", _root.transform, false);
            _blue = ModelLibrary.Instantiate(ModelLibrary.Get("siren_blue", () => PropBuilder.Siren(false)), "SirenBlue", _root.transform, false);
            _root.SetActive(false);
        }

        public void ResetRun()
        {
            Active = false;
            _cooldown = 0;
            if (_root != null) _root.SetActive(false);
        }

        public bool CanSpawn => !Active && _cooldown <= 0;

        /// <param name="side">-1 = left sidewalk, +1 = right sidewalk.</param>
        public void Spawn(int side, float z)
        {
            _passed = false;
            Active = true;
            _cooldown = EnforcerRules.Cooldown;
            float x = side * (SceneryBuilder.RoadHalfWidth + 0.8f);
            _root.transform.localPosition = new Vector3(x, SceneryBuilder.SidewalkHeight, z);
            _root.transform.localRotation = Quaternion.Euler(0, 180f, 0); // face the oncoming jeepney
            _root.SetActive(true);
        }

        public void Tick(float dt, float speed)
        {
            _t += dt;
            if (_cooldown > 0) _cooldown -= dt;
            if (!Active) return;
            var p = _root.transform.localPosition;
            float before = p.z;
            p.z -= speed * dt;
            _root.transform.localPosition = p;
            bool phase = Mathf.FloorToInt(_t * 6f) % 2 == 0;
            _red.SetActive(phase);
            _blue.SetActive(!phase);
            if (before > 0 && p.z <= 0 && !_passed) { _passed = true; Passed?.Invoke(); } // once, even if the jeepney reverses
            if (p.z < -25f)
            {
                Active = false;
                _root.SetActive(false);
            }
        }
    }
}
