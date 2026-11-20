using System;
using JeepneyRush.Logic;

namespace JeepneyRush
{
    /// <summary>
    /// Static event hub: gameplay raises events, audio / VFX / UI / haptics listen.
    /// Keeps systems decoupled (no manager references another manager's internals).
    /// </summary>
    public static class GameEvents
    {
        public static event Action<GameState, GameState> StateChanged;
        public static event Action<int> CoinCollected;          // value
        public static event Action<PickupResult> PassengersServed;
        public static event Action<int, HazardKind> Hit;        // remaining health, what we hit
        public static event Action Jumped;
        public static event Action Landed;
        public static event Action<int> LaneChanged;            // direction
        public static event Action Braked;
        public static event Action Honked;
        public static event Action<RunResult> RunEnded;

        /// <summary>
        /// Clears every subscriber at the start of each Play session. Required when the
        /// Editor's "Enter Play Mode Options" skip domain reload (Unity 6 default in many
        /// templates): otherwise handlers from destroyed managers of the previous session
        /// stay subscribed and throw MissingReferenceException.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            StateChanged = null; CoinCollected = null; PassengersServed = null; Hit = null; Jumped = null;
            Landed = null; LaneChanged = null; Braked = null; Honked = null; RunEnded = null;
            Services.Reset();
        }

        public static void RaiseStateChanged(GameState from, GameState to) => StateChanged?.Invoke(from, to);
        public static void RaiseCoin(int value) => CoinCollected?.Invoke(value);
        public static void RaisePassengers(PickupResult r) => PassengersServed?.Invoke(r);
        public static void RaiseHit(int health, HazardKind kind) => Hit?.Invoke(health, kind);
        public static void RaiseJump() => Jumped?.Invoke();
        public static void RaiseLand() => Landed?.Invoke();
        public static void RaiseLane(int dir) => LaneChanged?.Invoke(dir);
        public static void RaiseBrake() => Braked?.Invoke();
        public static void RaiseHonk() => Honked?.Invoke();
        public static void RaiseRunEnded(RunResult r) => RunEnded?.Invoke(r);
    }

    public struct RunResult
    {
        public int Score;
        public float Distance;
        public int Coins;
        public int Passengers;
        public bool NewBestScore;
        public bool NewBestDistance;
        public int BestScore;
        public float BestDistance;
    }
}
