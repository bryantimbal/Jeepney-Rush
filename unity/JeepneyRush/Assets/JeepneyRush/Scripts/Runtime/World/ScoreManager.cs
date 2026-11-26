using System;
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
        /// <summary>Farthest point reached (reversing moves <see cref="Distance"/> back but never this).</summary>
        public float MaxDistance { get; private set; }
        public int Score => Mathf.FloorToInt(_scoreFloat);
        /// <summary>Everything earned this run: road coins + fares.</summary>
        public int Coins { get; private set; }
        /// <summary>Money paid by passengers (fares, tips, caught fare dodgers) – the boundary comes out of this.</summary>
        public int Fares { get; private set; }
        /// <summary>Coins picked up on the road (and other bonuses): never part of the boundary.</summary>
        public int RoadCoins => Math.Max(0, Coins + GasSpent - Fares);
        /// <summary>Paid at the gas station this run (comes out of the earnings).</summary>
        public int GasSpent { get; private set; }
        /// <summary>The driver's earnings so far: fares minus gas (minus the boundary in career, see Career).</summary>
        public int Kita => Fares - GasSpent;
        public int Passengers { get; private set; }

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            ResetRun();
        }

        public void ResetRun()
        {
            Distance = MaxDistance = 0;
            _scoreFloat = 0;
            Coins = 0;
            Fares = 0;
            GasSpent = 0;
            Passengers = 0;
        }

        public void AddDistance(float metres, float scoreMultiplier = 1f)
        {
            Distance += metres;
            if (Distance <= MaxDistance) return; // reversing / driving back over ground already scored
            float fresh = Distance - MaxDistance;
            MaxDistance = Distance;
            _scoreFloat += fresh * _cfg.scorePerMetre * scoreMultiplier;
        }

        public void AddCoins(int value)
        {
            Coins += value;
            _scoreFloat += value * _cfg.scorePerCoin;
        }

        /// <summary>MMDA ticket: deduct coins earned this run.</summary>
        public void SpendCoins(int value) { Coins = Mathf.Max(0, Coins - value); Fares = Mathf.Min(Fares, Coins); }

        /// <summary>Diesel at the station: paid out of this run's money, counted against the driver's earnings.</summary>
        public void SpendGas(int value)
        {
            value = Mathf.Clamp(value, 0, Coins);
            Coins -= value;
            GasSpent += value;
        }

        /// <summary>Passenger money outside a stop (a fare dodger who paid up after the horn).</summary>
        public void AddFare(int value)
        {
            AddCoins(value);
            Fares += value;
        }

        public void AddScore(int value) => _scoreFloat += value;

        public void AddPickup(PickupResult r)
        {
            Passengers += r.Boarded; // boarded passengers (deliveries are tracked by the route)
            Coins += r.Coins;
            Fares += r.Coins; // everything paid at a stop is passenger money
            _scoreFloat += r.Score;
        }

    }
}
