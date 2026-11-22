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

        /// <summary>Set by the menu to play today's Daily Challenge (fixed seed and route).</summary>
        public static DailyChallenge? Challenge;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Challenge = null; TrialJeepney = null; }

        /// <summary>Phase 4: a locked jeepney unlocked for ONE run by a rewarded ad (garage "TRY").</summary>
        public static string TrialJeepney;
        /// <summary>Jeepney used this run (the trial one, or the player's selection).</summary>
        private string RunJeepney => !string.IsNullOrEmpty(TrialJeepney) && JeepneyCatalog.Exists(TrialJeepney) ? TrialJeepney : Services.Save.Data.selectedJeepney;
        // last finished run (Game Over: double coins, interstitial pacing)
        public string LastRunId { get; private set; }
        public int LastRunCoins { get; private set; }
        public float LastRunSeconds { get; private set; }
        public bool CoinsDoubled { get; set; }
        private bool _revived;
        private int _seed;
        public bool IsDaily => Challenge.HasValue;

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
        private EnforcerManager _enforcer;
        private readonly CoinCombo _combo = new CoinCombo();
        private float _enforcerBase;
        private EffectsManager _fx;
        private DifficultyManager _difficulty;
        private ScoreManager _score;
        private LevelSpawner _spawner;
        private SpawnDirector _director;
        private GameRandom _rng;

        private WeatherManager _weather;
        // EDSA rush hour + day/night
        private JamSchedule _jams;
        private float _jamCap = 1f, _jam, _stuckT, _complainT, _hour;
        private int _jamComplaints, _dayPhase = -1;
        private bool _inJam;
        // patok pull-cord bell, brake sounds
        private bool _patok, _stopArmed;
        private int _bellStopId = -1;
        private float _prevSpeed;
        private RoadBender _bender;
        private float _speed, _runTime, _dyingTime, _cleanDistance, _bestClean, _turboFx, _wetDistance;
        private int _smoothHumps, _hits;
        private bool _gasWas;
        private float _revCooldown, _dwell, _loadT, _beepT, _roughT;
        /// <summary>Gear shift: false = D (drive), true = R (reverse).</summary>
        private bool _reverseGear;
        public bool ReverseGear => _reverseGear;
        private PassengerStopView _inZone;
        // ---- fuel & gas stations
        private StationManager _stations;
        private IntersectionManager _crossings;
        private float _hornCooldown;
        private FuelTank _fuel;
        private StationView _stationZone;
        private RefuelQuote _quote;
        private float _refuelLeft, _refuelWalk, _stationDwell, _emptyT, _sputterT, _pumpSfxT;
        private bool _refuelling, _lowWarned, _engineDead;
        private int _refuels;
        public FuelTank Fuel => _fuel;
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
            return Upgrades.Apply(_baseCfg, JeepneyCatalog.Get(RunJeepney), d.UpgradeLevel);
        }

        private void Awake()
        {
            Services.Game.Register(this);
            Services.Audio.EnsureLoaded(); // when Play is pressed directly in this scene
            _baseCfg = GameBalance.Active;
            _cfg = BuildRunConfig();
            _env = Challenge.HasValue ? Challenge.Value.Environment : (EnvironmentKind)Services.Save.Data.selectedEnvironment;
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
            _player.Init(_cfg, RunJeepney);
            _input = GetOrAdd<PlayerInputHandler>(playerGo);
            _input.OnAction += OnAction;

            _passengers = GetOrAdd<PassengerManager>(Child("PassengerManager"));
            _passengers.Init(_cfg, _player.Body);
            _passengers.Missed = OnMissed;
            _passengers.FarePaid = pos =>
            {
                if (_dying) return;
                Services.UI.CoinFly(pos);
                if (_rng.Chance(0.4)) Services.UI.Say(_rng.Chance(0.5) ? "SALAMAT!" : "BAYAD NATANGGAP!", () => _player.Body.TransformPoint(PassengerManager.DriverSeat + new Vector3(0, 1.1f, 0)));
            };
            _powerUps = GetOrAdd<PowerUpManager>(Child("PowerUpManager"));
            _powerUps.Init(_cfg, _player.transform);
            _enforcer = GetOrAdd<EnforcerManager>(Child("EnforcerManager"));
            _enforcer.Init(_env);
            _enforcer.Passed = OnEnforcerPassed;
            _spawner = new LevelSpawner(_traffic, _obstacles, _coins, _passengers, _powerUps);
            _stations = GetOrAdd<StationManager>(Child("StationManager"));
            _spawner.Stations = _stations;
            _crossings = GetOrAdd<IntersectionManager>(Child("IntersectionManager"));
            _spawner.Intersections = _crossings;
            _crossings.RanRed = OnRanRed;
            _traffic.CollectAll = _spawner.CollectBlockers;
            _traffic.PlayerLanes = () => _spawner.PlayerLanes;

            // hills & bends are applied for rendering only (gameplay stays in straight lanes)
            _bender = GetOrAdd<RoadBender>(Child("RoadBender"));
            _bender.Road = _road;
            foreach (var root in new[] { _traffic.transform, _obstacles.transform, _coins.transform, _passengers.transform, _powerUps.transform, _enforcer.transform, _stations.transform, _crossings.transform, _road.SegmentRoot })
                _bender.AddRoot(root);
            _weather = GetOrAdd<WeatherManager>(Child("Weather"));
            _weather.Init(_camera.GetComponent<Camera>(), _road);
            // moving traffic never swerves into a PARA zone it would occupy before the jeepney passes
            _traffic.ZoneBlocked = (lane, unityZ, speed, halfLength, tMeet) =>
                _director != null && _director.CrossesReservation(lane, _score.Distance + unityZ, speed, halfLength, tMeet);

            _player.Collisions.HazardHit = OnHazardHit;
            _player.Collisions.CoinTouched = OnCoinTouched;
            _player.Collisions.ZoneEntered = OnZoneEntered;
            _player.Collisions.PowerUpTouched = OnPowerUpTouched;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void Start() => StartRun();

        private void OnDestroy()
        {
            GameEvents.StateChanged -= OnStateChanged;
            RoadCurve.Shape = null;
            if (_weather != null) _weather.Stop();
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
            int seed = Challenge.HasValue ? Challenge.Value.Seed : debugSeed != 0 ? debugSeed : System.Environment.TickCount & 0x7fffffff;
            _seed = seed;
            _rng = new GameRandom(seed);
            _traffic.Clear();
            _obstacles.Clear();
            _stations.Clear();
            _stations.Init(_cfg);
            _crossings.Clear();
            _crossings.Init(_cfg);
            _coins.Clear();
            _traffic.Init(_cfg, _rng);
            _obstacles.Init(_cfg, _rng);
            _coins.Init(_cfg);
            _difficulty.Init(_cfg);
            _score.Init(_cfg);
            _fx.Clear();
            _player.SetLivery(RunJeepney);
            _patok = JeepneyCatalog.Get(RunJeepney).SoundSystem;
            _bellStopId = -1; _prevSpeed = 0; _stopArmed = false;
            Services.Audio.SetSoundTrip(JeepneyCatalog.Get(RunJeepney).SoundSystem && Services.Save.Data.soundTrip);
            _revived = false;
            if (_patok) Services.UI.Float("PATOK! · HILA KUNG BABABA", UIManager.FloatKind.Info);
            if (!string.IsNullOrEmpty(TrialJeepney)) Services.UI.Float("TRIAL RUN: " + JeepneyCatalog.Get(TrialJeepney).DisplayName.ToUpperInvariant(), UIManager.FloatKind.Info);
            _player.Configure(_cfg);
            _passengers.Init(_cfg, _player.Body);
            var route = new PassengerRoute(_cfg, _rng, StopNames.ForEnvironment(_env));
            _passengers.ResetRun(route);
            _powerUps.ResetRun(_cfg, Upgrades.PowerUpDurationScale(save.UpgradeLevel(UpgradeKind.PowerUpDuration)));
            RoadCurve.Shape = new RoadShape(seed, _env);
            RoadCurve.Distance = 0;
            _weather.ResetRun(seed, _env);
            _jams = new JamSchedule(seed, _env);
            _director = new SpawnDirector(_cfg, _rng, _spawner, route, _env, _weather.Schedule.WetnessAt, _jams.At);
            _jamCap = 1f; _jam = _stuckT = _complainT = 0; _jamComplaints = 0; _inJam = false; _dayPhase = -1;
            Services.UI.SetJam("");
            _enforcer.ResetRun();
            _combo.Reset();
            _input.Sensitivity = save.swipeSensitivity;
            _player.ResetRun();
            _road.Layout();
            _speed = _runTime = _cleanDistance = _bestClean = 0;
            _smoothHumps = _hits = 0;
            _wetDistance = 0;
            _gasWas = false;
            _dwell = _loadT = _beepT = 0;
            _reverseGear = false;
            Services.UI.SetGear(false);
            _fuel = new FuelTank(_cfg);
            _stationZone = null;
            _refuelling = _lowWarned = _engineDead = false;
            _refuelLeft = _refuelWalk = _stationDwell = _emptyT = _sputterT = _pumpSfxT = 0;
            _refuels = 0;
            _inZone = null;
            _dying = false;
            _camera.Snap(_player.transform.position);
            Services.Game.SetState(GameState.Starting);
            Services.UI.ShowHud(!save.tutorialDone, !save.controlsTutorialDone);
            Services.UI.Float(IsDaily ? "DAILY CHALLENGE!" : "ARANGKADA!", UIManager.FloatKind.Big);
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
                case PlayerInputHandler.Action.Down:
                    if (_player.Down()) GameEvents.RaiseBrake();
                    break;
                case PlayerInputHandler.Action.Horn:
                    Honk();
                    break;
                case PlayerInputHandler.Action.Gear:
                    ToggleGear();
                    break;
            }
        }

        /// <summary>
        /// Gear shift D ↔ R. Like a real gearbox you can only shift when (almost) stopped –
        /// hold BRAKE first.
        /// </summary>
        public void ToggleGear()
        {
            var st = Services.Game.State;
            if (_dying || (st != GameState.Playing && st != GameState.Starting)) return;
            if (Mathf.Abs(_speed) > 1.0f)
            {
                Services.UI.Float("PRENO MUNA BAGO MAG-SHIFT!", UIManager.FloatKind.Bad);
                Services.Audio.Play(Logic.Audio.Sfx.Full, 0.7f);
                return;
            }
            _reverseGear = !_reverseGear;
            Services.UI.SetGear(_reverseGear);
            Services.Audio.Play(Logic.Audio.Sfx.Click, 1f, 0.6f); // gearbox "clunk"
            Services.Audio.Play(Logic.Audio.Sfx.Land, 0.5f, 1.4f);
            _player.Bump(0.04f);
            Services.UI.Float(_reverseGear ? "R · ATRAS" : "D · ARANGKADA", UIManager.FloatKind.Info);
        }

        /// <summary>BUSINA: the jeepney horn. The car ahead in your lane moves over (if safe) or hurries up.</summary>
        public void Honk()
        {
            if (_hornCooldown > 0) return;
            _hornCooldown = _cfg.hornCooldown;
            GameEvents.RaiseHonk();
            Haptics.Pulse(10);
            if (_dying || _traffic == null || Services.Game.State != GameState.Playing) return;
            var car = _traffic.Honk(_player.Lanes.X, _speed);
            if (car != null && _rng.Chance(0.5))
            {
                var t = car.transform;
                Services.UI.Say(_rng.Chance(0.5) ? "OO NA, OO NA!" : "SANDALI LANG!", () => t.position + Vector3.up * 2.2f);
            }
        }

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
            _revCooldown -= dt;
            _hornCooldown -= dt;
            if (_player.GasHeld && !_gasWas && _revCooldown <= 0 && !_fuel.Empty)
            {
                _revCooldown = 0.7f;
                // stepping on the accelerator: diesel rev + a black cough of smoke
                Services.Audio.Rev();
                _fx.GasPuff(_player.transform.position, _speed);
            }
            _gasWas = _player.GasHeld;
            _difficulty.Tick(_score.Distance);
            float intro = Mathf.Clamp01(_runTime / 1.2f);
            if (_dying || _loadT > 0 || _refuelling)
            {
                // crashing, or passengers getting on/off: the jeepney stays put
                _speed = Mathf.MoveTowards(_speed, 0f, _cfg.brakeDecel * 2f * dt);
            }
            else if (_fuel.Empty)
            {
                // out of diesel: no power – the jeepney coasts (brakes still work, no reversing)
                _speed = Mathf.MoveTowards(_speed, 0f, (brakeHeld ? _cfg.brakeDecel : 2.2f) * dt);
            }
            else if (brakeHeld)
            {
                // real brakes: decelerate to a complete stop and hold it (never reverses by itself)
                _speed = Mathf.MoveTowards(_speed, 0f, _cfg.brakeDecel * dt);
            }
            else if (_reverseGear)
            {
                // gear R + GAS backs up; stops at the reverse limit, when back at a PARA zone or
                // gas station pad, or right before backing into something (parking sensor)
                bool room = _score.Distance > _score.MaxDistance - _cfg.reverseLimit && _inZone == null && _stationZone == null && ClearanceBehind() > 0.8f;
                float target = gasHeld && room ? -_cfg.reverseSpeed : 0f;
                _speed = Mathf.MoveTowards(_speed, target, (room ? 3f : 12f) * dt);
            }
            else
            {
                if (_speed < 0) _speed = Mathf.MoveTowards(_speed, 0f, _cfg.brakeDecel * dt); // stop reversing first
                float target = _difficulty.BaseSpeed * _jamCap * _player.SpeedMultiplier * pu.SpeedMultiplier * (0.35f + 0.65f * intro);
                float accel = _player.Braking ? 5f : gasHeld || pu.IsActive(PowerUpKind.Turbo) ? 3f : 2f;
                _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-dt * accel));
            }

            BrakeSounds(brakeHeld);

            // ---- traffic jam (EDSA rush hour): the road ahead crawls, the top speed eases down
            _jam = _jams.At(_score.Distance);
            float cap = JamRules.SpeedCap(_jam), baseV = Mathf.Max(1f, _difficulty.BaseSpeed);
            _jamCap = Mathf.MoveTowards(_jamCap, cap, (cap < _jamCap ? JamRules.CapDecel : JamRules.CapAccel) / baseV * dt);
            UpdateJam(dt);

            // ---- time of day: the run starts in the late afternoon and drives into the night
            _hour = DayNight.HourAt(_runTime);
            _weather.SetTimeOfDay(_hour);
            UpdateDayPhase();

            // ---- weather + road shape
            _weather.Tick(_score.Distance, dt);
            float wet = _weather.Wetness;
            // unpaved mountain road: loose, slippery ground and constant small jolts
            float rough = Environments.Roughness(_env);
            _player.SetWetness(Mathf.Max(wet, 0.35f * rough));
            if (rough > 0 && Mathf.Abs(_speed) > 2f && !_dying)
            {
                _roughT -= dt;
                if (_roughT <= 0)
                {
                    float k = Mathf.Clamp01(Mathf.Abs(_speed) / 18f);
                    _roughT = Random.Range(0.12f, 0.4f) / Mathf.Max(0.4f, k);
                    _player.Bump(Random.Range(0.015f, 0.05f) * rough * (0.4f + k));
                    _camera.AddShake(0.025f * k);
                    if (Random.value < 0.25f * k) _fx.Dust(_player.transform.position + new Vector3(Random.Range(-1f, 1f), 0.1f, -2.6f));
                }
            }
            RoadCurve.Distance = _score.Distance;
            _player.SlopePitch = RoadCurve.PlayerPitch;

            // ---- player
            _player.Tick(dt, _speed);
            _combo.Tick(dt);
            Services.UI.SetCombo(_combo.Count);

            // ---- distance, score (gas = bonus score), spawning
            float dz = _speed * dt;
            if (wet > 0.3f && dz > 0) _wetDistance += dz;
            _score.AddDistance(dz, _player.GasHeld ? _cfg.gasScoreMultiplier : 1f);
            if (!_dying && !_refuelling) BurnFuel(dz, dt);
            if (dz > 0) _cleanDistance += dz;
            if (_speed < -0.3f)
            {
                _beepT -= dt;
                if (_beepT <= 0)
                {
                    float gap = ClearanceBehind();
                    _beepT = gap < 3f ? 0.18f : gap < 7f ? 0.35f : 0.7f; // beeps faster near an obstacle
                    Services.Audio.Play(Logic.Audio.Sfx.ReverseBeep, 0.6f);
                }
            }
            else _beepT = 0;
            _bestClean = Mathf.Max(_bestClean, _cleanDistance);
            if (!_dying)
            {
                var lanes = _player.Lanes;
                _spawner.SetPlayerLanes(lanes.Lane, lanes.Changing ? lanes.FromLane : -1);
                _director.Update(_score.Distance, Mathf.Max(_speed, _difficulty.BaseSpeed * 0.6f * _jamCap));
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
            _enforcer.Tick(dt, _speed);
            if (!_dying) UpdateStops(dt);
            _stations.Tick(dt, _speed, _player.transform.localPosition);
            _crossings.Tick(dt, _speed);
            if (!_dying) UpdateStations(dt);
            float hump = NextHumpZ();
            var next = _passengers.NextStop();
            float nextZ = next != null ? next.transform.localPosition.z - _cfg.stopZoneLength / 2f : -1f;
            if (!_dying) { PullCordBell(next, nextZ); TurboSafety(pu, next, nextZ); }
            if (_enforcer.Active && _enforcer.Z > 0)
                Services.UI.SetEnforcerBanner($"{_enforcer.CheckpointName} · BRAKE! {Mathf.CeilToInt(_enforcer.Z)} m");
            else if (LightBanner(out string light))
                Services.UI.SetEnforcerBanner(light);
            else if (_refuelling)
                Services.UI.SetEnforcerBanner(_refuelWalk > 0 ? "GAS STATION · PA-FULL TANK PO!" : $"NAGPAPAGASOLINA... {Mathf.RoundToInt(_fuel.Fraction * 100)}%");
            else if (_fuel.Empty)
                Services.UI.SetEnforcerBanner("UBOS ANG GAS! · STOP AT A GAS STATION");
            else if (_stationZone != null && _fuel.Fraction < 0.97f)
                Services.UI.SetEnforcerBanner("GAS STATION · HOLD BRAKE · FULL STOP");
            else if (NextStationAhead(out float stz) && stz < 90f && _fuel.Fraction < 0.8f && _inZone == null)
                Services.UI.SetEnforcerBanner($"GASOLINAHAN {Mathf.CeilToInt(stz)} m · STOP TO REFUEL");
            else if (stz < 140f && stz > 0f && _inZone == null)
                Services.UI.SetEnforcerBanner($"GASOLINAHAN AHEAD · {Mathf.CeilToInt(stz)} m");
            else if (_loadT > 0)
                Services.UI.SetEnforcerBanner("SAKAY / BABA... HINTAY LANG");
            else if (_inZone != null)
                Services.UI.SetEnforcerBanner("PARA! HOLD BRAKE · FULL STOP");
            else if (_speed < -0.3f)
                Services.UI.SetEnforcerBanner("ATRAS! (GEAR R)");
            else if (_reverseGear && !_dying)
                Services.UI.SetEnforcerBanner("GEAR R · GAS = ATRAS · TAP D TO DRIVE");
            else if (next != null && nextZ < 0 && _route_HasBusiness(next) && _speed < 3f)
                Services.UI.SetEnforcerBanner("LAGPAS SA PARA! SHIFT TO R + GAS");
            else if (next != null && nextZ > 0 && nextZ < 70f && _route_HasBusiness(next))
                Services.UI.SetEnforcerBanner($"PARA ZONE {Mathf.CeilToInt(nextZ)} m · BRAKE TO STOP");
            else if (hump > 0 && hump < 75f)
                Services.UI.SetEnforcerBanner($"HUMP AHEAD · BRAKE! {Mathf.CeilToInt(hump)} m");
            else Services.UI.SetEnforcerBanner(null);
            float s01 = Mathf.Clamp01(Speed01 + (pu.IsActive(PowerUpKind.Turbo) ? 0.6f : _player.GasHeld ? 0.25f : 0f));
            _fx.Tick(dt, _player.transform.position, _speed, s01, _player.GasHeld, wet);
            if (pu.IsActive(PowerUpKind.Turbo))
            {
                _turboFx -= dt;
                if (_turboFx <= 0) { _turboFx = 0.03f; _fx.Turbo(_player.transform.position); }
            }

            // ---- camera, audio, HUD
            _camera.Follow(_player.transform.position, _player.Lanes.VelocityX, s01, dt);
            if (!_engineDead) Services.Audio.UpdateEngine(_speed, _player.GasHeld, _player.Braking);
            if (!_dying)
            {
                Services.UI.UpdateHud(this);
                float stationM = NextStationAhead(out float sz) ? sz : (_director != null ? _director.NextStationAt - _score.Distance : -1f);
                Services.UI.SetFuel(_fuel.Fraction, stationM, _fuel.Low);
            }
        }

        /// <summary>Free space (m) behind the jeepney's rear bumper before a solid obstacle in its path.</summary>
        private float ClearanceBehind()
        {
            float best = 999f, x = _player.Lanes.X;
            foreach (var list in new[] { _obstacles.Active, _traffic.Active })
                foreach (var h in list)
                {
                    if (h.Hit || h.Def.Soft) continue;
                    var p = h.transform.localPosition;
                    if (p.z > 0 || Mathf.Abs(p.x - x) > h.Def.HalfWidth + _cfg.playerHalfWidth) continue;
                    best = Mathf.Min(best, -p.z - h.Def.HalfLength - _cfg.playerHalfLength);
                }
            return best;
        }

        /// <summary>Distance (m) to the nearest speed hump ahead, or -1.</summary>
        private float NextHumpZ()
        {
            float best = -1;
            foreach (var h in _obstacles.Active)
            {
                if (h.Kind != HazardKind.SpeedHump || h.Hit) continue;
                float z = h.transform.localPosition.z;
                if (z > 0 && (best < 0 || z < best)) best = z;
            }
            return best;
        }

        // ---------------------------------------------------------------- physics callbacks
        /// <summary>Humps, potholes and floods: driven over, never a crash.</summary>
        private void OnSoftHazard(Hazard h)
        {
            h.Hit = true;
            var pos = _player.transform.position;
            if (HazardCatalog.IsMud(h.Kind))
            {
                // thick mud: the wheels dig in and slow the jeepney, mud flies everywhere
                _player.Flooded();
                _player.Bump(0.12f);
                _fx.MudSplash(pos, _speed);
                _camera.AddShake(0.12f);
                Services.Audio.Play(Logic.Audio.Sfx.Splash, 0.7f, 0.7f);
                Haptics.Pulse(12);
                Services.UI.Float("PUTIK!", UIManager.FloatKind.Info);
                if (_passengers.Route.Count > 0 && _rng.Chance(0.4)) Services.UI.Say(_rng.Chance(0.5) ? "NAPUTIKAN AKO!" : "DAHAN-DAHAN, MAPUTIK!", _passengers.RandomRiderHead);
                return;
            }
            if (HazardCatalog.IsFlood(h.Kind))
            {
                bool big = h.Kind == HazardKind.FloodWide;
                _player.Flooded();
                _fx.Splash(pos, _speed, big);
                _camera.AddShake(big ? 0.18f : 0.1f);
                Services.Audio.Play(Logic.Audio.Sfx.Splash, big ? 1f : 0.8f);
                Haptics.Pulse(15);
                Services.UI.Float(big ? "BAHA! LUSONG!" : "BAHA!", UIManager.FloatKind.Info);
                if (_passengers.Route.Count > 0 && _rng.Chance(0.6)) Services.UI.Say(Lines.Pick(Lines.Flood, _rng), _passengers.RandomRiderHead);
                return;
            }
            bool isHump = h.Kind == HazardKind.SpeedHump;
            if (HumpRules.Smooth(_speed, _difficulty.BaseSpeed, _cfg, _player.Braking))
            {
                // slowed down properly: gentle roll over, passengers are happy
                _player.Bump(0.08f);
                Services.Audio.Play(Logic.Audio.Sfx.Bump, 0.35f);
                if (isHump)
                {
                    _smoothHumps++;
                    _score.AddScore(HumpRules.SmoothBonusScore);
                    Services.UI.Float($"DAHAN-DAHAN! +{HumpRules.SmoothBonusScore}", UIManager.FloatKind.Good);
                    if (_passengers.Route.Count > 0 && _rng.Chance(0.5)) Services.UI.Say(Lines.Pick(Lines.Smooth, _rng), _passengers.RandomRiderHead);
                }
                return;
            }
            // too fast: everyone bounces off their seat
            float hard = isHump ? 0.4f : 0.3f;
            _player.Bump(hard);
            _camera.AddShake(isHump ? 0.4f : 0.3f);
            Services.Audio.Play(Logic.Audio.Sfx.Bump, 1f);
            Haptics.Pulse(30);
            _fx.Dust(pos + new Vector3(0, 0.1f, -1.5f));
            Services.UI.Float(isHump ? "HUMP! ARAY!" : "LUBAK! ARAY!", UIManager.FloatKind.Bad);
            var route = _passengers.Route;
            if (route.Count > 0)
            {
                Services.UI.Say(Lines.Pick(isHump ? Lines.Hump : Lines.Bump, _rng), _passengers.RandomRiderHead, true);
                Services.Audio.Play(Logic.Audio.Sfx.Complain);
                if (_rng.Chance(0.3) && route.EjectAngry() > 0)
                {
                    _passengers.AngryHopOut();
                    Services.UI.Say(Lines.Pick(Lines.Leave, _rng), () => _player.transform.position + new Vector3(0, 2.4f, -3.4f), true, 2.2f);
                    Services.UI.Float("-1 PASAHERO", UIManager.FloatKind.Bad);
                }
            }
        }

        /// <summary>Brake squeal while slowing down hard, and the air-brake "pssh" when the jeepney comes to a full stop.</summary>
        private void BrakeSounds(bool brakeHeld)
        {
            bool braking = brakeHeld || _player.Braking || _loadT > 0 || _refuelling || _dying;
            float v = Mathf.Abs(_speed);
            if (v > 3.5f) _stopArmed = true;
            if (braking && _prevSpeed > 5f && v <= 5f && !_dying) Services.Audio.Play(Logic.Audio.Sfx.BrakeSqueal, 0.75f, Random.Range(0.95f, 1.08f));
            if (_stopArmed && braking && v <= _cfg.stopSpeed && !_dying)
            {
                _stopArmed = false;
                Services.Audio.Play(Logic.Audio.Sfx.AirBrake, 0.6f);
            }
            _prevSpeed = v;
        }

        /// <summary>
        /// Patok jeepney: the sound trip is too loud to hear "PARA!", so a rider pulls the cord and
        /// the bell rings as you approach a stop where someone wants to get off.
        /// </summary>
        private void PullCordBell(PassengerStopView next, float nextZ)
        {
            if (!_patok || next == null || next.Stop.Id == _bellStopId) return;
            if (nextZ < 0 || nextZ > 75f || _passengers.Route.AlightingAt(next.Stop.Id) <= 0) return;
            _bellStopId = next.Stop.Id;
            Services.Audio.Play(Logic.Audio.Sfx.Bell, 0.95f);
            Services.UI.Say(Lines.Pick(Lines.PullCord, _rng), _passengers.RandomRiderHead);
            Services.UI.Float("DING-DING! MAY BABABA", UIManager.FloatKind.Info);
        }

        /// <summary>Turbo switches off before a PARA stop with business, a gas station or an intersection.</summary>
        private void TurboSafety(PowerUpState pu, PassengerStopView next, float nextZ)
        {
            if (!pu.IsActive(PowerUpKind.Turbo)) return;
            bool stopAhead = next != null && nextZ >= 0 && nextZ < 90f && _route_HasBusiness(next);
            bool stationAhead = NextStationAhead(out float stz) && stz < 90f;
            bool crossingAhead = false;
            foreach (var x in _crossings.Items) { float z = x.transform.localPosition.z; if (z > 0 && z < 90f) { crossingAhead = true; break; } }
            if (!stopAhead && !stationAhead && !crossingAhead) return;
            pu.End(PowerUpKind.Turbo);
            Services.UI.Float("TURBO OFF · MAY HINTUAN", UIManager.FloatKind.Info);
        }

        private bool RushHour => (_hour >= 16f && _hour < 20.5f) || (_hour >= 6f && _hour < 9.5f);

        /// <summary>
        /// Jam entry/exit messages, the "traffic ahead" warning, and riders complaining while
        /// stuck: late for work in the morning, tired and hungry going home at night. If you
        /// stay stuck too long, a fed-up rider gets off and walks.
        /// </summary>
        private void UpdateJam(float dt)
        {
            var ui = Services.UI;
            bool jammed = _jam > 0.3f;
            if (jammed != _inJam)
            {
                _inJam = jammed;
                if (jammed)
                {
                    ui.Float(_env == EnvironmentKind.Edsa ? (RushHour ? "RUSH HOUR SA EDSA!" : "TRAFFIC SA EDSA!") : "TRAFFIC JAM!", UIManager.FloatKind.Bad);
                    _jamComplaints = 0;
                    _complainT = 2.5f;
                }
                else
                {
                    ui.Float("LUMUWAG NA ANG DAAN!", UIManager.FloatKind.Good);
                    if (_passengers.Route.Count > 0 && !_dying) ui.Say(Lines.Pick(Lines.JamClear, _rng), _passengers.RandomRiderHead);
                }
            }
            if (jammed) ui.SetJam(RushHour ? "RUSH HOUR · MABAGAL" : "TRAFFIC · MABAGAL");
            else
            {
                float next = _jams.NextJamFrom(_score.Distance) - _score.Distance;
                ui.SetJam(next < 150f ? $"TRAFFIC AHEAD · {Mathf.CeilToInt(next)} m" : "");
            }
            bool stuck = jammed && !_dying && _passengers.Route.Count > 0 && Mathf.Abs(_speed) < JamRules.StuckSpeed
                && _loadT <= 0 && _inZone == null && !_refuelling;
            if (!stuck) { _stuckT = Mathf.Max(0, _stuckT - dt * 0.5f); return; }
            _stuckT += dt;
            _complainT -= dt;
            if (_complainT > 0) return;
            _complainT = JamRules.ComplaintInterval(_stuckT) * Random.Range(0.85f, 1.2f);
            _jamComplaints++;
            ui.Say(Lines.Stuck(_hour, _env, _rng), _passengers.RandomRiderHead, _jamComplaints > 2);
            Services.Audio.Play(Logic.Audio.Sfx.Complain, 0.8f);
            if (_jamComplaints > JamRules.PatienceComplaints && _rng.Chance(JamRules.WalkOffChance) && _passengers.Route.EjectAngry() > 0)
            {
                _passengers.AngryHopOut();
                ui.Say(_rng.Chance(0.5) ? "MAGLALAKAD NA LANG AKO!" : "MAS MABILIS PA MAGLAKAD!", () => _player.transform.position + new Vector3(0, 2.4f, -3.4f), true, 2.2f);
                ui.Float("-1 PASAHERO · NAINIP", UIManager.FloatKind.Bad);
            }
        }

        /// <summary>HUD clock and sunset / night / sunrise announcements.</summary>
        private void UpdateDayPhase()
        {
            float n = DayNight.Night(_hour);
            Services.UI.SetClock(DayNight.Clock(_hour), n > 0.5f);
            int phase = n >= 0.99f ? 2 : n > 0.01f || DayNight.Dusk(_hour) > 0.5f ? 1 : 0;
            if (phase == _dayPhase) return;
            bool first = _dayPhase < 0;
            bool evening = _hour > 12f;
            _dayPhase = phase;
            if (first) return;
            if (phase == 1) Services.UI.Float(evening ? "PALUBOG NA ANG ARAW..." : "MAGUUMAGA NA...", UIManager.FloatKind.Info);
            else if (phase == 2) Services.UI.Float("GABI NA! · BUKAS ANG ILAW", UIManager.FloatKind.Info);
            else Services.UI.Float("UMAGA NA!", UIManager.FloatKind.Good);
        }

        private void OnHazardHit(Hazard h)
        {
            if (_dying || h.Hit) return;
            if (h.Def.Soft) { OnSoftHazard(h); return; }
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
            _camera.AddShake(0.55f);
            GameEvents.RaiseHit(c.Health, h.Kind);
            Services.UI.FlashDamage();
            ReactToBump();
            if (c.Dead) OfferReviveOrDie();
            else Services.UI.Float("BANGGA!", UIManager.FloatKind.Bad);
        }

        /// <summary>Passengers complain about a bump; one may storm off; MMDA sets up a checkpoint.</summary>
        private void ReactToBump()
        {
            var route = _passengers.Route;
            if (route.Count > 0)
            {
                Services.UI.Say(Lines.Pick(Lines.Bump, _rng), _passengers.RandomRiderHead, true);
                Services.Audio.Play(Logic.Audio.Sfx.Complain);
                if (_rng.Chance(0.6) && route.EjectAngry() > 0)
                {
                    _passengers.AngryHopOut();
                    Services.UI.Say(Lines.Pick(Lines.Leave, _rng), () => _player.transform.position + new Vector3(0, 2.4f, -3.4f), true, 2.2f);
                    Services.UI.Float("-1 PASAHERO", UIManager.FloatKind.Bad);
                }
            }
            if (_enforcer.CanSpawn)
            {
                int side = _player.Lanes.Lane <= (_cfg.laneCount - 1) / 2 ? -1 : 1;
                _enforcer.Spawn(side, EnforcerRules.SpawnAhead);
                Services.Audio.Play(Logic.Audio.Sfx.Whistle);
                Services.UI.Say("PRRRT! TABI, KUYA!", () => _enforcer.HeadPosition, true, 2.5f);
            }
        }

        private void OnEnforcerPassed()
        {
            if (_dying) return;
            if (EnforcerRules.PassedSafely(_speed, _difficulty.BaseSpeed))
            {
                _score.AddScore(EnforcerRules.PoliteBonusScore);
                Services.UI.Say(Lines.Pick(Lines.Polite, _rng), () => _enforcer.HeadPosition);
                Services.UI.Float($"MABAGAL! +{EnforcerRules.PoliteBonusScore}", UIManager.FloatKind.Good);
            }
            else
            {
                int fine = EnforcerRules.Fine(_score.Coins);
                _score.SpendCoins(fine);
                Services.Audio.Play(Logic.Audio.Sfx.Whistle);
                Services.UI.Say(Lines.Pick(Lines.Ticket, _rng), () => _enforcer.HeadPosition, true, 2.2f);
                Services.UI.Float(fine > 0 ? $"TICKET! -{fine} COINS" : "WARNING!", UIManager.FloatKind.Bad);
                if (_passengers.Route.Count > 0) Services.UI.Say("KUYA NAMAN!", _passengers.RandomRiderHead, true);
            }
        }

        private void OnCoinTouched(Coin coin)
        {
            if (_dying) return;
            int v = _coins.Collect(coin) * _powerUps.State.CoinMultiplier;
            if (v <= 0) return;
            _score.AddCoins(v);
            var pos = coin.transform.position;
            _fx.CoinSparkle(pos);
            int rush = _combo.Add();
            Services.Audio.PlayCoin(_combo.Pitch);
            Services.UI.CoinFly(pos);
            if (_combo.Count % 5 == 0) Haptics.Pulse(8);
            if (rush > 0)
            {
                _score.AddCoins(rush);
                Services.UI.Float($"COIN RUSH! +{rush}", UIManager.FloatKind.Big);
                Services.Audio.Play(Logic.Audio.Sfx.CoinRush);
                _fx.CoinRush(_player.transform.position + Vector3.up * 2f);
            }
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

        // ---------------------------------------------------------------- traffic lights
        /// <summary>Banner for the next traffic light: countdown on yellow/red, nothing on green.</summary>
        private bool LightBanner(out string text)
        {
            text = null;
            var v = _crossings.Next();
            if (v == null) return false;
            float d = v.StopLineZ - _cfg.playerHalfLength;
            if (d < -0.5f || d > 90f) return false;
            int secs = Mathf.CeilToInt(v.Light.Remaining);
            switch (v.Light.Color)
            {
                case LightColor.Red:
                    text = Mathf.Abs(_speed) < 0.5f && d < 15f ? $"PULA · HINTAY LANG... {secs}" : $"PULA! STOP SA LINYA · {Mathf.CeilToInt(Mathf.Max(0, d))} m · {secs}";
                    return true;
                case LightColor.Yellow:
                    text = $"DILAW! · {secs}";
                    return true;
                default:
                    if (d < 25f && v.Light.Remaining < 3f) { text = $"BERDE · {secs}"; return true; }
                    return false;
            }
        }

        /// <summary>Ran a red light: whistle, MMDA ticket, shaken passengers.</summary>
        private void OnRanRed(IntersectionView v)
        {
            if (_dying) return;
            int fine = EnforcerRules.Fine(_score.Coins);
            _score.SpendCoins(fine);
            _cleanDistance = 0;
            Services.Audio.Play(Logic.Audio.Sfx.Whistle);
            Haptics.Pulse(40);
            _camera.AddShake(0.15f);
            Services.UI.Float(fine > 0 ? $"BEATING THE RED LIGHT! -{fine} COINS" : "BEATING THE RED LIGHT!", UIManager.FloatKind.Bad);
            var head = v.transform;
            Services.UI.Say("PRRRT! PULA NA, BOSS!", () => head.position + new Vector3(SceneryBuilderRoadEdge(), 2.4f, -6.5f), true, 2.2f);
            if (_passengers.Route.Count > 0) Services.UI.Say("KUYA, PULA NA!", _passengers.RandomRiderHead, true);
        }

        private static float SceneryBuilderRoadEdge() => Logic.Geometry.SceneryBuilder.RoadHalfWidth + 0.5f;

        // ---------------------------------------------------------------- fuel
        private bool NextStationAhead(out float metres)
        {
            metres = -1;
            var s = _stations.Next();
            if (s == null) return false;
            metres = s.transform.localPosition.z - _cfg.stationZoneLength / 2f;
            return metres > 0;
        }

        private void BurnFuel(float dz, float dt)
        {
            bool wasEmpty = _fuel.Empty;
            _fuel.Burn(dz, _player.GasHeld, dt);
            if (_fuel.Low && !_lowWarned)
            {
                _lowWarned = true;
                Services.UI.Float("PAUBOS NA ANG GAS!", UIManager.FloatKind.Bad);
                Services.Audio.Play(Logic.Audio.Sfx.Full, 0.9f);
                if (_passengers.Route.Count > 0) Services.UI.Say("KUYA, MAGPA-GAS KA NA!", _passengers.RandomRiderHead);
            }
            // last drops: the engine coughs
            if (!_fuel.Empty && _fuel.Fraction < 0.06f)
            {
                _sputterT -= dt;
                if (_sputterT <= 0) { _sputterT = Random.Range(0.8f, 1.8f); Services.Audio.Play(Logic.Audio.Sfx.Sputter, 0.8f); _player.Bump(0.06f); }
            }
            if (_fuel.Empty && !wasEmpty)
            {
                _engineDead = true;
                Services.Audio.Play(Logic.Audio.Sfx.Sputter, 1f);
                Services.Audio.StopEngine();
                Services.UI.Float("UBOS ANG GAS!", UIManager.FloatKind.Big);
                if (_passengers.Route.Count > 0) Services.UI.Say("NAKU, NAUBUSAN!", _passengers.RandomRiderHead, true);
            }
            if (_fuel.Empty && Mathf.Abs(_speed) < 0.1f && _stationZone == null)
            {
                _emptyT += dt;
                if (_emptyT > 1.8f) OutOfFuel();
            }
            else _emptyT = 0;
        }

        private void OutOfFuel()
        {
            if (_dying) return;
            Services.UI.Float("TIRIK! UBOS ANG GAS", UIManager.FloatKind.Big);
            BeginDeath();
        }

        /// <summary>Gas stations: stop on the pad (touching it is enough) and the attendant fills you up.</summary>
        private void UpdateStations(float dt)
        {
            if (_refuelling)
            {
                if (_refuelWalk > 0) { _refuelWalk -= dt; return; } // attendant walks over
                float add = Mathf.Min(_cfg.refuelLitresPerSecond * dt, _refuelLeft);
                _fuel.Add(add);
                _refuelLeft -= add;
                _pumpSfxT -= dt;
                if (_pumpSfxT <= 0) { _pumpSfxT = 1.1f; Services.Audio.Play(Logic.Audio.Sfx.Refuel, 0.8f); }
                if (_refuelLeft <= 0.01f) FinishRefuel();
                return;
            }
            var x = _player.Lanes.X;
            _stationZone = null;
            foreach (var s in _stations.Stations)
                if (!s.Done && StopRules.InZone(s.transform.localPosition.z, x, _cfg.LaneX(s.Station.Lane), _cfg, _cfg.stationZoneLength)) { _stationZone = s; break; }
            if (_stationZone != null && Mathf.Abs(_speed) <= _cfg.stopSpeed && _loadT <= 0)
            {
                _stationDwell += dt;
                if (_stationDwell >= _cfg.stopDwell) StartRefuel(_stationZone);
            }
            else _stationDwell = 0;
        }

        private void StartRefuel(StationView s)
        {
            _stationDwell = 0;
            _quote = FuelRules.Quote(_fuel.Fuel, _fuel.Capacity, _score.Coins, _cfg);
            if (_quote.Total < 0.5f)
            {
                s.Done = true;
                Services.UI.Float("PUNO PA ANG TANGKE!", UIManager.FloatKind.Info);
                return;
            }
            _refuelling = true;
            _refuelLeft = _quote.Total;
            _refuelWalk = 0.7f;
            _pumpSfxT = 0.7f;
            _stations.Serving = s;
            Services.UI.Say(_quote.Credit > 0 && _quote.Paid < 1 ? "KUYA, UTANG MUNA PO..." : "PA-FULL TANK PO!", () => _player.Body.TransformPoint(PassengerManager.DriverSeat + new Vector3(0, 1.1f, 0)));
            if (_engineDead) { _engineDead = false; }
        }

        private void FinishRefuel()
        {
            _refuelling = false;
            var s = _stations.Serving;
            if (s != null) s.Done = true;
            _stations.Serving = null;
            _score.SpendCoins(_quote.Cost);
            _refuels++;
            _lowWarned = _fuel.Low;
            Services.Audio.Play(Logic.Audio.Sfx.Purchase);
            Services.Audio.StartEngine();
            _fx.GasPuff(_player.transform.position, 0);
            int litres = Mathf.RoundToInt(_quote.Total);
            if (_quote.Credit > 0.5f) Services.UI.Float($"UTANG MUNA! +{litres} L", UIManager.FloatKind.Info);
            else Services.UI.Float($"FULL TANK! +{litres} L · -{_quote.Cost} COINS", UIManager.FloatKind.Good);
            // the driver grumbles about the price of diesel
            Services.UI.Say(Lines.Pick(Lines.GasPrice, _rng), () => _player.Body.TransformPoint(PassengerManager.DriverSeat + new Vector3(0, 1.1f, 0)), true, 2.4f);
            Services.UI.Float("ARANGKADA! (GAS)", UIManager.FloatKind.Info);
        }

        /// <summary>Does anyone want to get on or off at this stop?</summary>
        private bool _route_HasBusiness(PassengerStopView s) =>
            s.Stop.Waiting > 0 && _passengers.Route.Count < _cfg.passengerCapacity || _passengers.Route.AlightingAt(s.Stop.Id) > 0;

        /// <summary>
        /// PARA stops: passengers only board / get off once the jeepney has come to a
        /// complete stop inside the zone (in its lane); then it waits while they move.
        /// </summary>
        private void UpdateStops(float dt)
        {
            if (_loadT > 0)
            {
                _loadT -= dt;
                if (_loadT <= 0) Services.UI.Float("ARANGKADA! (GAS)", UIManager.FloatKind.Info);
                return;
            }
            var lanes = _player.Lanes;
            _inZone = null;
            foreach (var s in _passengers.Stops)
                if (!s.Done && StopRules.InZone(s.transform.localPosition.z, lanes.X, _cfg.LaneX(s.Stop.Lane), _cfg)) { _inZone = s; break; }
            if (_inZone != null && Mathf.Abs(_speed) <= _cfg.stopSpeed)
            {
                _dwell += dt;
                if (_dwell >= _cfg.stopDwell)
                {
                    _dwell = 0;
                    var r = ServeStop(_inZone);
                    _loadT = StopRules.LoadTime(r, _cfg);
                    _inZone = null;
                }
            }
            else _dwell = 0;
        }

        /// <summary>Driving into the zone only gives a hint; stopping is what counts.</summary>
        private void OnZoneEntered(PassengerStopView stop)
        {
            if (_dying || stop.Done || _speed < 2f) return;
            Services.UI.Float("PARA! PRENO!", UIManager.FloatKind.Info);
        }

        private PickupResult ServeStop(PassengerStopView stop)
        {
            if (_dying || stop.Done) return default;
            var r = _passengers.Serve(stop);
            _score.AddPickup(r);
            if (r.Alighted > 0)
            {
                Services.UI.Say(Lines.Pick(Lines.Alight, _rng), () => _player.transform.position + new Vector3(0, 2.4f, -3.4f));
                Services.UI.Float($"{r.Alighted} BABA · +{r.Coins}", UIManager.FloatKind.Good);
                if (r.Streak > 1) Services.UI.Float($"SUNOD-SUNOD x{_passengers.Route.StreakMultiplier:0.##}!", UIManager.FloatKind.Info);
                _fx.Confetti(_player.transform.position + new Vector3(0, 2.5f, -2f), 20 + r.Alighted * 6);
            }
            if (r.Boarded > 0)
            {
                if (r.Alighted == 0) Services.UI.Float($"+{r.Boarded} SAKAY!", UIManager.FloatKind.Good);
                Services.UI.Say(Lines.BoardFor(_env, _rng), _passengers.RandomRiderHead);
                _fx.Confetti(_player.transform.position + new Vector3(0, 2.5f, -2f), 10 + r.Boarded * 4);
            }
            if (r.Full) Services.UI.Float("PUNO NA!", UIManager.FloatKind.Info);
            GameEvents.RaisePassengers(r);
            return r;
        }

        private void OnMissed(int lost, PassengerStop stop)
        {
            if (_dying) return;
            Services.UI.Float($"LAGPAS! -{lost}", UIManager.FloatKind.Bad);
            GameEvents.RaiseMissed(lost);
        }

        /// <summary>
        /// Last heart lost: offer ONE optional rewarded-ad revive per run (the game is frozen while
        /// the offer is shown – no ad ever appears while driving). Otherwise the run ends.
        /// </summary>
        private void OfferReviveOrDie()
        {
            var ads = Services.Ads;
            if (ads == null || !ads.RewardedReady || !AdPolicy.CanRevive(_revived, _score.MaxDistance)) { BeginDeath(); return; }
            _revived = true;
            _input.InputEnabled = false;
            Time.timeScale = 0f;
            Services.Audio.StopEngine();
            Services.UI.ShowRevive(5f, watch =>
            {
                if (!watch) { Time.timeScale = 1f; BeginDeath(); return; }
                ads.ShowRewarded(RewardedKind.Revive, LastRunId, (earned, nonce) =>
                {
                    Time.timeScale = 1f;
                    if (earned) Revive(); else BeginDeath();
                });
            });
        }

        private void Revive()
        {
            _player.Collisions.Heal();
            _player.Collisions.GrantInvulnerability(3f);
            // clear the road just ahead so the jeepney isn't revived into the same crash
            foreach (var list in new[] { _traffic.Active, _obstacles.Active })
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var h = list[i];
                    float z = h.transform.localPosition.z;
                    if (!h.Hit && !h.Def.Soft && z > -4f && z < 35f) (HazardCatalog.IsMoving(h.Kind) ? (HazardManagerBase)_traffic : _obstacles).Knock(h, _player.transform.position.x);
                }
            _input.InputEnabled = true;
            if (!_engineDead) Services.Audio.StartEngine();
            Services.UI.Float("BUHAY PA! ARANGKADA!", UIManager.FloatKind.Big);
            Services.Audio.Play(Logic.Audio.Sfx.PowerUp);
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
                Score = _score.Score, Distance = _score.MaxDistance, Coins = _score.Coins, Passengers = route.PickedUp,
                Deliveries = route.Delivered, PowerUps = _powerUps.State.UsedThisRun, SmoothHumps = _smoothHumps, WetDistance = _wetDistance, Refuels = _refuels, Hits = _hits,
                CleanDistance = _bestClean, BestStreak = route.BestStreak, Crashed = true, Environment = _env,
            };
            var rewards = Services.Save.RecordRun(summary);
            var report = RunReport.From(summary, _runTime, _seed, IsDaily, Challenge.HasValue ? Challenge.Value.DayKey : 0, RunJeepney);
            LastRunId = report.run_id;
            LastRunCoins = summary.Coins;
            LastRunSeconds = _runTime;
            CoinsDoubled = false;
            TrialJeepney = null; // trials last one run
            Services.Ads?.RunEnded();
            // Phase 3: upload the run (queued on disk if offline; the server re-checks and credits it)
            if (Services.Online != null && Services.Online.SignedIn)
                Services.Online.SubmitRun(report);
            var d = Services.Save.Data;
            var result = new RunResult
            {
                Score = _score.Score, Distance = _score.MaxDistance, Coins = _score.Coins, Passengers = route.PickedUp, Deliveries = route.Delivered,
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
