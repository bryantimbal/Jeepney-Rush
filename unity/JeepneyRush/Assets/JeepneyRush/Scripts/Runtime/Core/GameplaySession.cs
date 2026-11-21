using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Lives in the Gameplay scene and runs one frame of the game in a fixed order:
    /// input → speed → player → spawning → world → power-ups → camera/audio → HUD.
    /// Physics triggers (CollisionHandler) report hits, coins, power-ups and PARA zones here.
    /// </summary>
    public sealed class GameplaySession : MonoBehaviour
    {
        [Tooltip("Fixed seed for reproducible runs (0 = random each run).")]
        public int debugSeed;

        private BalanceConfig _baseCfg, _cfg;
        private EnvironmentKind _env;
        private PlayerController _player;
        private PlayerInputHandler _input;
        private CameraController _camera;
        private RoadManager _road;
        private TrafficManager _traffic;
        private ObstacleManager _obstacles;
        private CoinManager _coins;
        private PassengerManager _passengers;
        private PowerUpManager _powerUps;
        private EffectsManager _fx;
        private DifficultyManager _difficulty;
        private ScoreManager _score;
        private LevelSpawner _spawner;
        private SpawnDirector _director;
        private GameRandom _rng;

        private float _speed, _runTime, _dyingTime, _cleanDistance, _bestClean, _turboFx;
        private int _jumps, _hits;
        private bool _dying;

        public ScoreManager Score => _score;
        public PlayerController Player => _player;
        public PassengerManager Passengers => _passengers;
        public PowerUpManager PowerUps => _powerUps;
        public DifficultyManager Difficulty => _difficulty;
        public PlayerInputHandler Input => _input;
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

        /// <summary>Effective balance for this run: base asset + selected jeepney + garage upgrades.</summary>
        private BalanceConfig BuildRunConfig()
        {
            var d = Services.Save.Data;
            return Upgrades.Apply(_baseCfg, JeepneyCatalog.Get(d.selectedJeepney), d.UpgradeLevel);
        }

        private void Awake()
        {
            Services.Game.Register(this);
            Services.Audio.EnsureLoaded(); // when Play is pressed directly in this scene
            _baseCfg = GameBalance.Active;
            _cfg = BuildRunConfig();
            _env = (EnvironmentKind)Services.Save.Data.selectedEnvironment;
            Services.Audio.SetRoute(_env);
            EnvironmentSetup.Apply(_env);
            _camera = EnvironmentSetup.EnsureCamera().GetComponent<CameraController>();

            _difficulty = GetOrAdd<DifficultyManager>(Child("DifficultyManager"));
            _score = GetOrAdd<ScoreManager>(Child("ScoreManager"));
            _road = GetOrAdd<RoadManager>(Child("RoadManager"));
            _road.Build(_env);
            _rng = new GameRandom(1);
            _traffic = GetOrAdd<TrafficManager>(Child("TrafficManager"));
            _obstacles = GetOrAdd<ObstacleManager>(Child("ObstacleManager"));
            _coins = GetOrAdd<CoinManager>(Child("CoinManager"));
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
            _passengers.Missed = OnMissed;
            _powerUps = GetOrAdd<PowerUpManager>(Child("PowerUpManager"));
            _powerUps.Init(_cfg, _player.transform);
            _spawner = new LevelSpawner(_traffic, _obstacles, _coins, _passengers, _powerUps);
            _traffic.CollectAll = _spawner.CollectBlockers;
            _traffic.PlayerLanes = () => _spawner.PlayerLanes;

            _player.Collisions.HazardHit = OnHazardHit;
            _player.Collisions.CoinTouched = OnCoinTouched;
            _player.Collisions.ZoneEntered = OnZone;
            _player.Collisions.PowerUpTouched = OnPowerUpTouched;
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
            var save = Services.Save.Data;
            _cfg = BuildRunConfig();
            int seed = debugSeed != 0 ? debugSeed : System.Environment.TickCount & 0x7fffffff;
            _rng = new GameRandom(seed);
            _traffic.Clear();
            _obstacles.Clear();
            _coins.Clear();
            _traffic.Init(_cfg, _rng);
            _obstacles.Init(_cfg, _rng);
            _coins.Init(_cfg);
            _difficulty.Init(_cfg);
            _score.Init(_cfg);
            _fx.Clear();
            _player.SetLivery(save.selectedJeepney);
            _player.Configure(_cfg);
            _passengers.Init(_cfg, _player.Body);
            var route = new PassengerRoute(_cfg, _rng, StopNames.ForEnvironment(_env));
            _passengers.ResetRun(route);
            _powerUps.ResetRun(_cfg, Upgrades.PowerUpDurationScale(save.UpgradeLevel(UpgradeKind.PowerUpDuration)));
            _director = new SpawnDirector(_cfg, _rng, _spawner, route, _env);
            _input.Sensitivity = save.swipeSensitivity;
            _player.ResetRun();
            _road.Layout();
            _speed = _runTime = _cleanDistance = _bestClean = 0;
            _jumps = _hits = 0;
            _dying = false;
            _camera.Snap(_player.transform.position);
            Services.Game.SetState(GameState.Starting);
            Services.UI.ShowHud(!save.tutorialDone, !save.controlsTutorialDone);
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
                    if (_player.Jump()) { _jumps++; GameEvents.RaiseJump(); }
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

            // ---- controls → speed: difficulty curve × gas/brake × crash slow-down × turbo
            var pu = _powerUps.State;
            bool gasHeld = !_dying && _input.GasHeld;
            bool brakeHeld = !_dying && _input.BrakeHeld;
            if (brakeHeld && !_player.BrakeHeld) GameEvents.RaiseBrake();
            _player.GasHeld = gasHeld && !brakeHeld;
            _player.BrakeHeld = brakeHeld;
            _difficulty.Tick(_score.Distance);
            float intro = Mathf.Clamp01(_runTime / 1.2f);
            float target = _dying ? 0 : _difficulty.BaseSpeed * _player.SpeedMultiplier * pu.SpeedMultiplier * (0.35f + 0.65f * intro);
            float accel = _player.Braking ? 5f : gasHeld || pu.IsActive(PowerUpKind.Turbo) ? 3f : 2f;
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-dt * accel));

            // ---- player
            if (_player.Tick(dt, _speed))
            {
                GameEvents.RaiseLand();
                _fx.Dust(_player.transform.position + new Vector3(0, 0.1f, -1.5f));
                _camera.AddShake(0.06f);
            }

            // ---- distance, score (gas = bonus score), spawning
            float dz = _speed * dt;
            _score.AddDistance(dz, _player.GasHeld ? _cfg.gasScoreMultiplier : 1f);
            _cleanDistance += dz;
            _bestClean = Mathf.Max(_bestClean, _cleanDistance);
            if (!_dying)
            {
                var lanes = _player.Lanes;
                _spawner.SetPlayerLanes(lanes.Lane, lanes.Changing ? lanes.FromLane : -1);
                _director.Update(_score.Distance, Mathf.Max(_speed, _difficulty.BaseSpeed * 0.6f));
            }

            // ---- world + power-up effects
            _passengers.Route.FareMultiplier = pu.FareMultiplier;
            _coins.MagnetRadius = pu.IsActive(PowerUpKind.Magnet) ? _cfg.magnetRadius : 0f;
            _coins.MagnetTarget = _player.transform.position;
            _road.Scroll(dz);
            _traffic.Tick(dt, _speed);
            _obstacles.Tick(dt, _speed);
            _coins.Tick(dt, _speed);
            _passengers.Tick(dt, _speed);
            _powerUps.Tick(dt, _speed);
            float s01 = Mathf.Clamp01(Speed01 + (pu.IsActive(PowerUpKind.Turbo) ? 0.6f : _player.GasHeld ? 0.25f : 0f));
            _fx.Tick(dt, _player.transform.position, _speed, s01);
            if (pu.IsActive(PowerUpKind.Turbo))
            {
                _turboFx -= dt;
                if (_turboFx <= 0) { _turboFx = 0.03f; _fx.Turbo(_player.transform.position); }
            }

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
            var pu = _powerUps.State;
            var manager = HazardCatalog.IsMoving(h.Kind) ? (HazardManagerBase)_traffic : _obstacles;
            var hitPos = (_player.transform.position + h.transform.position) * 0.5f + Vector3.up;
            if (pu.Invulnerable)
            {
                // Turbo: smash through
                manager.Knock(h, _player.transform.position.x);
                _fx.Crash(hitPos);
                _camera.AddShake(0.2f);
                return;
            }
            if (c.Invulnerable) return; // pass through while blinking
            if (pu.ConsumeShield())
            {
                manager.Knock(h, _player.transform.position.x);
                _fx.Crash(hitPos);
                _camera.AddShake(0.3f);
                GameEvents.RaiseShieldBlocked();
                Services.UI.Float("SHIELD!", UIManager.FloatKind.Info);
                return;
            }
            float z = h.transform.localPosition.z;
            bool sideSwipe = _player.Lanes.Changing && Mathf.Abs(z) < h.Def.HalfLength + _cfg.playerHalfLength - 1.4f;
            if (!c.TryDamage()) return;
            _hits++;
            _cleanDistance = 0;
            _player.OnCrash(sideSwipe);
            manager.Knock(h, _player.transform.position.x);
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
            int v = _coins.Collect(coin) * _powerUps.State.CoinMultiplier;
            if (v <= 0) return;
            _score.AddCoins(v);
            _fx.CoinSparkle(coin.transform.position);
            GameEvents.RaiseCoin(v);
        }

        private void OnPowerUpTouched(PowerUpPickup p)
        {
            if (_dying || !_powerUps.Collect(p)) return;
            _fx.Confetti(p.transform.position + Vector3.up * 1.3f, 16);
            if (p.Kind == PowerUpKind.RepairKit)
            {
                if (_player.Collisions.Heal()) Services.UI.Float("+1 BUHAY!", UIManager.FloatKind.Good);
                else
                {
                    _score.AddCoins(_cfg.repairKitFallbackCoins);
                    Services.UI.Float($"+{_cfg.repairKitFallbackCoins} COINS", UIManager.FloatKind.Good);
                }
            }
            else Services.UI.Float(PowerUpState.DisplayName(p.Kind).ToUpperInvariant() + "!", UIManager.FloatKind.Info);
            GameEvents.RaisePowerUp(p.Kind);
        }

        private void OnZone(PassengerStopView stop)
        {
            if (_dying || stop.Done) return;
            var r = _passengers.Serve(stop);
            _score.AddPickup(r);
            if (r.Alighted > 0)
            {
                Services.UI.Float($"{r.Alighted} BABA · +{r.Coins}", UIManager.FloatKind.Good);
                if (r.Streak > 1) Services.UI.Float($"SUNOD-SUNOD x{_passengers.Route.StreakMultiplier:0.##}!", UIManager.FloatKind.Info);
                _fx.Confetti(_player.transform.position + new Vector3(0, 2.5f, -2f), 20 + r.Alighted * 6);
            }
            if (r.Boarded > 0)
            {
                if (r.Alighted == 0) Services.UI.Float($"+{r.Boarded} SAKAY!", UIManager.FloatKind.Good);
                _fx.Confetti(_player.transform.position + new Vector3(0, 2.5f, -2f), 10 + r.Boarded * 4);
            }
            if (r.Full) Services.UI.Float("PUNO NA!", UIManager.FloatKind.Info);
            GameEvents.RaisePassengers(r);
        }

        private void OnMissed(int lost, PassengerStop stop)
        {
            if (_dying) return;
            Services.UI.Float($"LAGPAS! -{lost}", UIManager.FloatKind.Bad);
            GameEvents.RaiseMissed(lost);
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
            var route = _passengers.Route;
            var summary = new RunSummary
            {
                Score = _score.Score, Distance = _score.Distance, Coins = _score.Coins, Passengers = route.PickedUp,
                Deliveries = route.Delivered, PowerUps = _powerUps.State.UsedThisRun, Jumps = _jumps, Hits = _hits,
                CleanDistance = _bestClean, BestStreak = route.BestStreak, Crashed = true, Environment = _env,
            };
            var rewards = Services.Save.RecordRun(summary);
            var d = Services.Save.Data;
            var result = new RunResult
            {
                Score = _score.Score, Distance = _score.Distance, Coins = _score.Coins, Passengers = route.PickedUp, Deliveries = route.Delivered,
                NewBestScore = rewards.NewBestScore, NewBestDistance = rewards.NewBestDistance, BestScore = d.bestScore, BestDistance = d.bestDistance,
                Rewards = rewards,
            };
            Services.Game.SetState(GameState.GameOver);
            GameEvents.RaiseRunEnded(result);
            Services.UI.ShowGameOver(result);
        }

        /// <summary>Debug snapshot (shown with the FPS overlay).</summary>
        public string DebugText() =>
            $"{_env} · {_difficulty.BandName} · {_speed * 3.6f:0} km/h · pools {Services.Pools.ActiveCount} · rows {_director?.RowsPlaced} (rej {_director?.Rejected})";
    }
}
