using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Lives in the Gameplay scene and runs one frame of the game in a fixed order:
    /// input → speed → player → spawning → world → camera/audio → HUD.
    /// Physics triggers (CollisionHandler) report hits, coins and PARA zones back here.
    /// </summary>
    public sealed class GameplaySession : MonoBehaviour
    {
        [Tooltip("Fixed seed for reproducible runs (0 = random each run).")]
        public int debugSeed;

        private BalanceConfig _cfg;
        private PlayerController _player;
        private PlayerInputHandler _input;
        private CameraController _camera;
        private RoadManager _road;
        private TrafficManager _traffic;
        private ObstacleManager _obstacles;
        private CoinManager _coins;
        private PassengerManager _passengers;
        private EffectsManager _fx;
        private DifficultyManager _difficulty;
        private ScoreManager _score;
        private LevelSpawner _spawner;
        private SpawnDirector _director;
        private GameRandom _rng;

        private float _speed, _runTime, _dyingTime;
        private bool _dying;

        public ScoreManager Score => _score;
        public PlayerController Player => _player;
        public PassengerManager Passengers => _passengers;
        public DifficultyManager Difficulty => _difficulty;
        public float Speed => _speed;
        public float Speed01 => _difficulty != null ? _difficulty.Speed01(_speed) : 0;

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private GameObject Child(string name)
        {
            var t = transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }

        private void Awake()
        {
            Services.Game.Register(this);
            Services.Audio.EnsureLoaded(); // when Play is pressed directly in this scene
            _cfg = GameBalance.Active;
            EnvironmentSetup.Apply();
            _camera = EnvironmentSetup.EnsureCamera().GetComponent<CameraController>();

            _difficulty = GetOrAdd<DifficultyManager>(Child("DifficultyManager"));
            _difficulty.Init(_cfg);
            _score = GetOrAdd<ScoreManager>(Child("ScoreManager"));
            _score.Init(_cfg);
            _road = GetOrAdd<RoadManager>(Child("RoadManager"));
            _road.Build();
            _rng = new GameRandom(1);
            _traffic = GetOrAdd<TrafficManager>(Child("TrafficManager"));
            _traffic.Init(_cfg, _rng);
            _obstacles = GetOrAdd<ObstacleManager>(Child("ObstacleManager"));
            _obstacles.Init(_cfg, _rng);
            _coins = GetOrAdd<CoinManager>(Child("CoinManager"));
            _coins.Init(_cfg);
            _fx = GetOrAdd<EffectsManager>(Child("Effects"));
            _fx.Build();

            var playerGo = Child("PlayerJeepney");
            GetOrAdd<Rigidbody>(playerGo);
            GetOrAdd<BoxCollider>(playerGo);
            GetOrAdd<CollisionHandler>(playerGo);
            _player = GetOrAdd<PlayerController>(playerGo);
            _player.Init(_cfg, Services.Save.Data.selectedJeepney);
            _input = GetOrAdd<PlayerInputHandler>(playerGo);
            _input.OnAction += OnAction;

            _passengers = GetOrAdd<PassengerManager>(Child("PassengerManager"));
            _passengers.Init(_cfg, _player.Body);
            _spawner = new LevelSpawner(_traffic, _obstacles, _coins, _passengers);
            _traffic.CollectAll = _spawner.CollectBlockers;
            _traffic.PlayerLanes = () => _spawner.PlayerLanes;

            _player.Collisions.HazardHit = OnHazardHit;
            _player.Collisions.CoinTouched = OnCoinTouched;
            _player.Collisions.ZoneEntered = OnZone;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void Start() => StartRun();

        private void OnDestroy()
        {
            GameEvents.StateChanged -= OnStateChanged;
            if (Services.Game != null) Services.Game.Unregister(this);
        }

        private void OnStateChanged(GameState from, GameState to)
        {
            _input.InputEnabled = to == GameState.Playing || to == GameState.Starting;
        }

        // ---------------------------------------------------------------- run lifecycle
        public void StartRun()
        {
            Time.timeScale = 1f;
            int seed = debugSeed != 0 ? debugSeed : System.Environment.TickCount & 0x7fffffff;
            _rng = new GameRandom(seed);
            _traffic.Clear();
            _obstacles.Clear();
            _coins.Clear();
            _traffic.SetRandom(_rng);
            _obstacles.SetRandom(_rng);
            _fx.Clear();
            var route = new PassengerRoute(_cfg, _rng);
            _passengers.ResetRun(route);
            _director = new SpawnDirector(_cfg, _rng, _spawner, route);
            _score.ResetRun();
            _difficulty.Tick(0);
            _player.SetLivery(Services.Save.Data.selectedJeepney);
            _passengers.AttachTo(_player.Body);
            _player.ResetRun();
            _road.Layout();
            _speed = 0;
            _runTime = 0;
            _dying = false;
            _camera.Snap(_player.transform.position);
            Services.Game.SetState(GameState.Starting);
            Services.UI.ShowHud(!Services.Save.Data.tutorialDone);
            Services.UI.Float("ARANGKADA!", UIManager.FloatKind.Big);
            GameEvents.RaiseHonk();
        }

        private void OnAction(PlayerInputHandler.Action a)
        {
            var state = Services.Game.State;
            if (a == PlayerInputHandler.Action.Pause) { Services.Game.Pause(); return; }
            if (_dying || (state != GameState.Playing && state != GameState.Starting)) return;
            switch (a)
            {
                case PlayerInputHandler.Action.Left:
                case PlayerInputHandler.Action.Right:
                    int dir = a == PlayerInputHandler.Action.Left ? -1 : 1;
                    if (_player.MoveLane(dir)) GameEvents.RaiseLane(dir);
                    break;
                case PlayerInputHandler.Action.Jump:
                    if (_player.Jump()) GameEvents.RaiseJump();
                    break;
                case PlayerInputHandler.Action.Down:
                    if (_player.Down() && _player.Jumper.Grounded) GameEvents.RaiseBrake();
                    break;
                case PlayerInputHandler.Action.Horn:
                    Honk();
                    break;
            }
        }

        public void Honk() => GameEvents.RaiseHonk();

        private void Update()
        {
            var state = Services.Game.State;
            if (state != GameState.Playing && state != GameState.Starting && !_dying) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            dt = Mathf.Min(dt, 0.05f);

            if (_dying)
            {
                _dyingTime += Time.unscaledDeltaTime;
                if (_dyingTime > 1.1f) { FinishRun(); return; }
            }

            _runTime += dt;
            if (state == GameState.Starting && _runTime > 1.2f) Services.Game.SetState(GameState.Playing);

            // ---- speed: difficulty curve × brake/crash modifiers, eased in at the start
            _difficulty.Tick(_score.Distance);
            float intro = Mathf.Clamp01(_runTime / 1.2f);
            float target = _dying ? 0 : _difficulty.BaseSpeed * _player.SpeedMultiplier * (0.35f + 0.65f * intro);
            float accel = _player.Braking ? 5f : 2f;
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-dt * accel));

            // ---- player
            if (_player.Tick(dt, _speed))
            {
                GameEvents.RaiseLand();
                _fx.Dust(_player.transform.position + new Vector3(0, 0.1f, -1.5f));
                _camera.AddShake(0.06f);
            }

            // ---- distance, score, spawning
            float dz = _speed * dt;
            _score.AddDistance(dz);
            if (!_dying)
            {
                var lanes = _player.Lanes;
                _spawner.SetPlayerLanes(lanes.Lane, lanes.Changing ? lanes.FromLane : -1);
                _director.Update(_score.Distance, Mathf.Max(_speed, _difficulty.BaseSpeed * 0.6f));
            }

            // ---- world
            _road.Scroll(dz);
            _traffic.Tick(dt, _speed);
            _obstacles.Tick(dt, _speed);
            _coins.Tick(dt, _speed);
            _passengers.Tick(dt, _speed);
            float s01 = Speed01;
            _fx.Tick(dt, _player.transform.position, _speed, s01);

            // ---- camera, audio, HUD
            _camera.Follow(_player.transform.position, _player.Lanes.VelocityX, s01, dt);
            Services.Audio.UpdateEngine(s01, _player.Braking);
            if (!_dying) Services.UI.UpdateHud(this);
        }

        // ---------------------------------------------------------------- physics callbacks
        private void OnHazardHit(Hazard h)
        {
            if (_dying || h.Hit) return;
            var c = _player.Collisions;
            if (c.Invulnerable) return; // pass through while blinking
            float z = h.transform.localPosition.z;
            bool sideSwipe = _player.Lanes.Changing && Mathf.Abs(z) < h.Def.HalfLength + _cfg.playerHalfLength - 1.4f;
            if (!c.TryDamage()) return;
            _player.OnCrash(sideSwipe);
            (HazardCatalog.IsMoving(h.Kind) ? (HazardManagerBase)_traffic : _obstacles).Knock(h, _player.transform.position.x);
            var hitPos = (_player.transform.position + h.transform.position) * 0.5f + Vector3.up;
            _fx.Crash(hitPos);
            _camera.AddShake(h.Def.Jumpable ? 0.25f : 0.55f);
            GameEvents.RaiseHit(c.Health, h.Kind);
            Services.UI.FlashDamage();
            if (c.Dead) BeginDeath();
            else Services.UI.Float(h.Def.Jumpable ? "ARAY!" : "BANGGA!", UIManager.FloatKind.Bad);
        }

        private void OnCoinTouched(Coin coin)
        {
            if (_dying) return;
            int v = _coins.Collect(coin);
            if (v <= 0) return;
            _score.AddCoins(v);
            _fx.CoinSparkle(coin.transform.position);
            GameEvents.RaiseCoin(v);
        }

        private void OnZone(PassengerStopView stop)
        {
            if (_dying || stop.Done) return;
            var r = _passengers.Serve(stop);
            _score.AddPickup(r);
            if (r.Boarded > 0)
            {
                Services.UI.Float($"+{r.Boarded} SAKAY!", UIManager.FloatKind.Good);
                _fx.Confetti(_player.transform.position + new Vector3(0, 2.5f, -2f), 12 + r.Boarded * 6);
            }
            if (r.Alighted > 0) Services.UI.Float($"{r.Alighted} BABA", UIManager.FloatKind.Info);
            if (r.Full) Services.UI.Float("PUNO NA!", UIManager.FloatKind.Info);
            GameEvents.RaisePassengers(r);
        }

        private void BeginDeath()
        {
            _dying = true;
            _dyingTime = 0;
            _input.InputEnabled = false;
            Time.timeScale = 0.35f; // dramatic slow motion before the results
            Services.Audio.StopEngine();
        }

        private void FinishRun()
        {
            _dying = false;
            Time.timeScale = 1f;
            var (bestScore, bestDistance) = Services.Save.RecordRun(_score.Summary(true));
            var d = Services.Save.Data;
            var result = new RunResult
            {
                Score = _score.Score, Distance = _score.Distance, Coins = _score.Coins, Passengers = _score.Passengers,
                NewBestScore = bestScore, NewBestDistance = bestDistance, BestScore = d.bestScore, BestDistance = d.bestDistance,
            };
            Services.Game.SetState(GameState.GameOver);
            GameEvents.RaiseRunEnded(result);
            Services.UI.ShowGameOver(result);
        }

        /// <summary>Debug snapshot (shown with the FPS overlay).</summary>
        public string DebugText() =>
            $"{_difficulty.BandName} · {_speed * 3.6f:0} km/h · pools {Services.Pools.ActiveCount} · rows {_director?.RowsPlaced} (rej {_director?.Rejected})";
    }
}
