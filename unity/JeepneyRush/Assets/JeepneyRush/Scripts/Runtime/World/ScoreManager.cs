using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Tracks distance, score, coins and passengers for the current run.</summary>
    public sealed class ScoreManager : MonoBehaviour
    {
        private BalanceConfig _cfg;
        private float _scoreFloat;

        public float Distance { get; private set; }
        public int Score => Mathf.FloorToInt(_scoreFloat);
        public int Coins { get; private set; }
        public int Passengers { get; private set; }

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            ResetRun();
        }

        public void ResetRun()
        {
            Distance = 0;
            _scoreFloat = 0;
            Coins = 0;
            Passengers = 0;
        }

        public void AddDistance(float metres, float scoreMultiplier = 1f)
        {
            Distance += metres;
            _scoreFloat += metres * _cfg.scorePerMetre * scoreMultiplier;
        }

        public void AddCoins(int value)
        {
            Coins += value;
            _scoreFloat += value * _cfg.scorePerCoin;
        }

        public void AddPickup(PickupResult r)
        {
            Passengers += r.Boarded; // boarded passengers (deliveries are tracked by the route)
            Coins += r.Coins;
            _scoreFloat += r.Score;
        }

    }
}
