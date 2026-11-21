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
        private RoadBender _bender;
        private float _speed, _runTime, _dyingTime, _cleanDistance, _bestClean, _turboFx, _wetDistance;
        private int _smoothHumps, _hits;
        private bool _gasWas;
        private float _revCooldown, _dwell, _loadT, _stillT, _beepT;
        private PassengerStopView _inZone;
        // ---- fuel & gas stations
        private StationManager _stations;
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
            _passengers.FarePaid = pos =>
            {
                if (_dying) return;
                Services.UI.CoinFly(pos);
                if (_rng.Chance(0.4)) Services.UI.Say(_rng.Chance(0.5) ? "SALAMAT!" : "BAYAD NATANGGAP!", () => _player.Body.TransformPoint(PassengerManager.DriverSeat + new Vector3(0, 1.1f, 0)));
            };
            _powerUps = GetOrAdd<PowerUpManager>(Child("PowerUpManager"));
            _powerUps.Init(_cfg, _player.transform);
            _enforcer = GetOrAdd<EnforcerManager>(Child("EnforcerManager"));
            _enforcer.Init();
            _enforcer.Passed = OnEnforcerPassed;
            _spawner = new LevelSpawner(_traffic, _obstacles, _coins, _passengers, _powerUps);
            _stations = GetOrAdd<StationManager>(Child("StationManager"));
            _spawner.Stations = _stations;
            _traffic.CollectAll = _spawner.CollectBlockers;
            _traffic.PlayerLanes = () => _spawner.PlayerLanes;

            // hills & bends are applied for rendering only (gameplay stays in straight lanes)
            _bender = GetOrAdd<RoadBender>(Child("RoadBender"));
            _bender.Road = _road;
            foreach (var root in new[] { _traffic.transform, _obstacles.transform, _coins.transform, _passengers.transform, _powerUps.transform, _enforcer.transform, _stations.transform, _road.SegmentRoot })
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
            int seed = debugSeed != 0 ? debugSeed : System.Environment.TickCount & 0x7fffffff;
            _rng = new GameRandom(seed);
            _traffic.Clear();
            _obstacles.Clear();
            _stations.Clear();
            _stations.Init(_cfg);
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
            RoadCurve.Shape = new RoadShape(seed, _env);
            RoadCurve.Distance = 0;
            _weather.ResetRun(seed, _env);
            _director = new SpawnDirector(_cfg, _rng, _spawner, route, _env, _weather.Schedule.WetnessAt);
            _enforcer.ResetRun();
            _combo.Reset();
            _input.Sensitivity = save.swipeSensitivity;
            _player.ResetRun();
            _road.Layout();
            _speed = _runTime = _cleanDistance = _bestClean = 0;
            _smoothHumps = _hits = 0;
            _wetDistance = 0;
            _gasWas = false;
            _dwell = _loadT = _stillT = _beepT = 0;
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
                case PlayerInputHandler.Action.Down:
                    if (_player.Down()) GameEvents.RaiseBrake();
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
            _revCooldown -= dt;
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
                _stillT = 0; // after loading, BRAKE must be held again before reversing
            }
            else if (_fuel.Empty)
            {
                // out of diesel: no power – the jeepney coasts (brakes still work, no reversing)
                _speed = Mathf.MoveTowards(_speed, 0f, (brakeHeld ? _cfg.brakeDecel : 2.2f) * dt);
            }
            else if (brakeHeld)
            {
                // real brakes: decelerate all the way to a complete stop ...
                if (_speed > 0.05f || _stillT < _cfg.reverseDelay)
                {
                    _speed = Mathf.MoveTowards(_speed, 0f, _cfg.brakeDecel * dt);
                    _stillT = _speed <= 0.05f ? _stillT + dt : 0f;
                }
                else
                {
                    // ... and keep holding to back up (e.g. to a PARA stop you overshot)
                    // stop reversing at the reverse limit, when back at a PARA zone, or right before
                    // backing into something (parking sensor)
                    bool room = _score.Distance > _score.MaxDistance - _cfg.reverseLimit && _inZone == null && _stationZone == null && ClearanceBehind() > 0.8f;
                    _speed = Mathf.MoveTowards(_speed, room ? -_cfg.reverseSpeed : 0f, (room ? 3f : 12f) * dt);
                    if (!room && _speed > -0.05f) _speed = 0;
                }
            }
            else
            {
                _stillT = 0;
                if (_speed < 0) _speed = Mathf.MoveTowards(_speed, 0f, _cfg.brakeDecel * dt); // stop reversing first
                float target = _difficulty.BaseSpeed * _player.SpeedMultiplier * pu.SpeedMultiplier * (0.35f + 0.65f * intro);
                float accel = _player.Braking ? 5f : gasHeld || pu.IsActive(PowerUpKind.Turbo) ? 3f : 2f;
                _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-dt * accel));
            }

            // ---- weather + road shape
            _weather.Tick(_score.Distance, dt);
            float wet = _weather.Wetness;
            _player.SetWetness(wet);
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
            _enforcer.Tick(dt, _speed);
            if (!_dying) UpdateStops(dt);
            _stations.Tick(dt, _speed, _player.transform.localPosition);
            if (!_dying) UpdateStations(dt);
            float hump = NextHumpZ();
            var next = _passengers.NextStop();
            float nextZ = next != null ? next.transform.localPosition.z - _cfg.stopZoneLength / 2f : -1f;
            if (_enforcer.Active && _enforcer.Z > 0)
                Services.UI.SetEnforcerBanner($"MMDA CHECKPOINT · BRAKE! {Mathf.CeilToInt(_enforcer.Z)} m");
            else if (_refuelling)
                Services.UI.SetEnforcerBanner(_refuelWalk > 0 ? "GAS STATION · PA-FULL TANK PO!" : $"NAGPAPAGASOLINA... {Mathf.RoundToInt(_fuel.Fraction * 100)}%");
            else if (_fuel.Empty)
                Services.UI.SetEnforcerBanner("UBOS ANG GAS! · STOP AT A GAS STATION");
            else if (_stationZone != null && _fuel.Fraction < 0.97f)
                Services.UI.SetEnforcerBanner("GAS STATION · HOLD BRAKE · FULL STOP");
            else if (NextStationAhead(out float stz) && stz < 90f && _fuel.Fraction < 0.8f && _inZone == null)
                Services.UI.SetEnforcerBanner($"GAS STATION {Mathf.CeilToInt(stz)} m · STOP TO REFUEL");
            else if (_loadT > 0)
                Services.UI.SetEnforcerBanner("SAKAY / BABA... HINTAY LANG");
            else if (_inZone != null)
                Services.UI.SetEnforcerBanner("PARA! HOLD BRAKE · FULL STOP");
            else if (_speed < -0.3f)
                Services.UI.SetEnforcerBanner("ATRAS! (REVERSING)");
            else if (next != null && nextZ < 0 && _route_HasBusiness(next) && _speed < 3f)
                Services.UI.SetEnforcerBanner("LAGPAS SA PARA! HOLD BRAKE TO REVERSE");
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
            if (!_engineDead) Services.Audio.UpdateEngine(s01, _player.Braking, _player.GasHeld);
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
            if (c.Dead) BeginDeath();
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
                Services.UI.Float("MAHINA NA ANG GAS!", UIManager.FloatKind.Bad);
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
                Services.UI.Say(Lines.Pick(Lines.Board, _rng), _passengers.RandomRiderHead);
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
