using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Distance-based difficulty from the configurable curve in GameBalance:
    /// 0–500 m easy, 500–1000 moderate, 1000–2000 faster, 2000–3000 high, 3000+ expert.
    /// </summary>
    public sealed class DifficultyManager : MonoBehaviour
    {
        private BalanceConfig _cfg;
        public float Difficulty { get; private set; }
        public float BaseSpeed { get; private set; }
        public string BandName { get; private set; } = "Easy";

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            Tick(0);
        }

        public void Tick(float distance)
        {
            Difficulty = _cfg.DifficultyAt(distance);
            BaseSpeed = _cfg.SpeedAt(distance);
            BandName = distance < 500 ? "Easy" : distance < 1000 ? "Moderate" : distance < 2000 ? "Fast" : distance < 3000 ? "Hard" : "Expert";
        }

        /// <summary>0..1 position between start and max speed (camera FOV, engine pitch, speed lines).</summary>
        public float Speed01(float speed) => Mathf.InverseLerp(_cfg.startSpeed, _cfg.maxSpeed, speed);
    }
}
