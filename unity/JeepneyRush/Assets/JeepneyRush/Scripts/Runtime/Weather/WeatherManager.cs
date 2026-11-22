using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Philippine weather during a run: sunny ("maaraw"), rain ("umuulan") and typhoon
    /// ("bagyo") from a deterministic <see cref="WeatherSchedule"/>. Drives rain particles
    /// (attached to the camera), darker sky / thicker fog, lightning + thunder in typhoons,
    /// a wet glossy road, the rain loop volume and the HUD label. Gameplay effects
    /// (slippery steering, floods) read <see cref="Wetness"/>.
    /// </summary>
    public sealed class WeatherManager : MonoBehaviour
    {
        private WeatherSchedule _schedule;
        private ParticleSystem _rain, _splash;
        private Camera _cam;
        private RoadManager _road;
        private Light _sun;
        private Material _sky;
        private float _baseSun, _baseShadow, _baseFogStart, _baseFogEnd, _baseExposure = 1f;
        private Color _baseFog, _baseAmbSky, _baseAmbEq, _baseAmbGround, _baseSkyTint;
        private float _lightningTimer, _flashT;
        private float _night, _dusk, _sunHeight = 1f;
        private Color _baseSunColor = Color.white;
        private Quaternion _baseSunRot = Quaternion.Euler(52f, -35f, 0f);

        public float Night => _night;

        private static readonly Color NightFog = new Color(0.07f, 0.09f, 0.16f);
        private static readonly Color DuskFog = new Color(0.98f, 0.64f, 0.45f);
        private static readonly Color NightSky = new Color(0.05f, 0.07f, 0.16f);
        private static readonly Color DuskSky = new Color(1f, 0.55f, 0.35f);
        private static readonly Color MoonLight = new Color(0.62f, 0.72f, 1f);
        private static readonly Color DuskLight = new Color(1f, 0.62f, 0.4f);

        /// <summary>Time of day for the lighting (hour 0..24, see <see cref="DayNight"/>).</summary>
        public void SetTimeOfDay(float hour)
        {
            _night = DayNight.Night(hour);
            _dusk = DayNight.Dusk(hour);
            _sunHeight = DayNight.SunHeight(hour);
        }
        private WeatherKind _shownKind = (WeatherKind)(-1);

        public float Wetness { get; private set; }
        public WeatherKind Kind { get; private set; }
        public WeatherSchedule Schedule => _schedule;

        private static readonly Color StormFog = new Color(0.46f, 0.5f, 0.56f);
        private static readonly Color StormSky = new Color(0.32f, 0.35f, 0.4f);

        public void Init(Camera cam, RoadManager road)
        {
            _cam = cam;
            _road = road;
            if (_rain == null) BuildRain();
        }

        /// <summary>Call after EnvironmentSetup.Apply so the dry look is captured as the base.</summary>
        public void ResetRun(int seed, EnvironmentKind env)
        {
            if (_schedule != null) Apply(0, 0); // back to the dry look before capturing it
            _schedule = new WeatherSchedule(seed, env);
            _sun = RenderSettings.sun;
            _baseSun = _sun != null ? _sun.intensity : 1.6f;
            _baseShadow = _sun != null ? _sun.shadowStrength : 0.75f;
            if (_sun != null) { _baseSunColor = _sun.color; _baseSunRot = _sun.transform.rotation; }
            _night = _dusk = 0; _sunHeight = 1f;
            _baseFog = RenderSettings.fogColor;
            _baseFogStart = RenderSettings.fogStartDistance;
            _baseFogEnd = RenderSettings.fogEndDistance;
            _baseAmbSky = RenderSettings.ambientSkyColor;
            _baseAmbEq = RenderSettings.ambientEquatorColor;
            _baseAmbGround = RenderSettings.ambientGroundColor;
            // own copy of the skybox so the stormy tint never touches the shared asset
            if (RenderSettings.skybox != null)
            {
                if (_sky == null || RenderSettings.skybox != _sky)
                    _sky = new Material(RenderSettings.skybox) { name = "JR_Skybox_Weather" };
                RenderSettings.skybox = _sky;
                _baseSkyTint = _sky.HasProperty("_SkyTint") ? _sky.GetColor("_SkyTint") : Color.white;
                _baseExposure = _sky.HasProperty("_Exposure") ? _sky.GetFloat("_Exposure") : 1f;
            }
            Wetness = 0;
            Kind = WeatherKind.Clear;
            _shownKind = (WeatherKind)(-1);
            _lightningTimer = Random.Range(3f, 6f);
            _flashT = 0;
            if (_rain != null) _rain.Clear();
            Apply(0, 0);
        }

        /// <summary>Restores the dry look (back to the menu).</summary>
        public void Stop()
        {
            if (_schedule == null) return;
            _night = _dusk = 0; _sunHeight = 1f;
            Apply(0, 0);
            if (_sun != null) { _sun.color = _baseSunColor; _sun.transform.rotation = _baseSunRot; }
            MaterialLibrary.SetNight(0);
            _schedule = null;
            if (_rain != null) { var em = _rain.emission; em.rateOverTime = 0; _rain.Clear(); }
            if (_splash != null) { var se = _splash.emission; se.rateOverTime = 0; _splash.Clear(); }
            Services.Audio?.SetRain(0);
            _road?.SetWetness(0);
        }

        private void OnDestroy() => Stop();

        private void BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(_cam != null ? _cam.transform : transform, false);
            go.transform.localPosition = new Vector3(0, 7f, 18f);
            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 2500;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            // thin, varied streaks (some brighter, some barely visible) read much more like real rain
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.85f, 0.95f, 0.18f), new Color(0.9f, 0.94f, 1f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = _rain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 1f, 40f);
            var vel = _rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(-26f, -22f);
            vel.z = new ParticleSystem.MinMaxCurve(-6f);
            var em = _rain.emission;
            em.enabled = true;
            em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1.4f;
            r.sharedMaterial = MaterialLibrary.Particle();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _rain.Play();
            BuildSplashes();
        }

        /// <summary>Little rings splashing up on the road and sidewalk around the jeepney while it rains.</summary>
        private void BuildSplashes()
        {
            var go = new GameObject("RainSplashes");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, 0.06f, 14f);
            _splash = go.AddComponent<ParticleSystem>();
            _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _splash.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 900;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = _splash.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 0.01f, 44f);
            var size = _splash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.2f, 1, 1f)); // ring grows
            var col = _splash.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0f, 1) });
            col.color = grad;
            var em = _splash.emission;
            em.enabled = true;
            em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // lying flat on the road
            r.sharedMaterial = MaterialLibrary.Particle();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _splash.Play();
        }

        public void Tick(float distance, float dt)
        {
            if (_schedule == null) return;
            Wetness = _schedule.WetnessAt(distance);
            Kind = _schedule.KindAt(distance);
            // lightning in typhoons (and the odd strike in heavy rain)
            if (Wetness > 0.7f)
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0)
                {
                    _lightningTimer = Random.Range(4f, 10f) / Mathf.Max(0.5f, Wetness);
                    _flashT = 0.35f;
                    Services.UI?.LightningFlash();
                    Services.Audio?.Thunder(Random.Range(0.25f, 1.2f));
                    Haptics.Pulse(20);
                }
            }
            if (_flashT > 0) _flashT -= dt;
            Apply(Wetness, _flashT > 0 ? Mathf.Clamp01(_flashT / 0.35f) : 0);

            if (Kind != _shownKind && (Wetness > 0.3f || Kind == WeatherKind.Clear))
            {
                bool first = _shownKind == (WeatherKind)(-1);
                _shownKind = Kind;
                var ui = Services.UI;
                if (ui != null)
                {
                    switch (Kind)
                    {
                        case WeatherKind.Rain:
                            ui.SetWeather("UMUULAN · MADULAS", new Color(0.6f, 0.82f, 1f));
                            ui.Float("UMUULAN! MADULAS ANG DAAN", UIManager.FloatKind.Info);
                            break;
                        case WeatherKind.Typhoon:
                            ui.SetWeather("BAGYO! · BAHA", new Color(1f, 0.55f, 0.45f));
                            ui.Float("SIGNAL NO. 2! MAG-INGAT!", UIManager.FloatKind.Bad);
                            break;
                        default:
                            ui.SetWeather("MAARAW", new Color(1f, 0.85f, 0.35f));
                            if (!first) ui.Float("TUMILA NA ANG ULAN!", UIManager.FloatKind.Good);
                            break;
                    }
                }
            }
        }

        private void Apply(float wet, float flash)
        {
            if (_rain != null)
            {
                var em = _rain.emission;
                em.rateOverTime = wet < 0.05f ? 0f : 300f + 2000f * wet;
                if (_splash != null) { var se = _splash.emission; se.rateOverTime = wet < 0.05f ? 0f : 120f + 700f * wet; }
                var vel = _rain.velocityOverLifetime;
                float wind = Mathf.Clamp01((wet - 0.6f) / 0.4f);
                vel.x = new ParticleSystem.MinMaxCurve(-9f * wind - 1f, -4f * wind);
            }
            float k = wet; // 0 dry .. 1 typhoon
            float n = _night, du = _dusk * (1f - k * 0.8f); // no golden hour under storm clouds
            var fog = Color.Lerp(_baseFog, StormFog, Mathf.Clamp01(k * 1.1f));
            fog = Color.Lerp(fog, DuskFog, du * 0.5f);
            RenderSettings.fogColor = Color.Lerp(fog, NightFog, n);
            RenderSettings.fogStartDistance = Mathf.Lerp(Mathf.Lerp(_baseFogStart, 35f, k), 30f, n * 0.6f);
            RenderSettings.fogEndDistance = Mathf.Lerp(Mathf.Lerp(_baseFogEnd, 150f, k), 150f, n);
            float dark = Mathf.Lerp(1f, 0.55f, k) * Mathf.Lerp(1f, 0.3f, n);
            var nightTint = Color.Lerp(Color.white, new Color(0.62f, 0.72f, 1f), n);
            RenderSettings.ambientSkyColor = Color.Lerp(_baseAmbSky * dark * nightTint, Color.white, flash);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(_baseAmbEq, DuskLight, du * 0.35f) * dark * nightTint, Color.white, flash);
            RenderSettings.ambientGroundColor = _baseAmbGround * dark;
            if (_sun != null)
            {
                // sun sinks for long evening shadows, then becomes soft blue moonlight
                _sun.intensity = Mathf.Lerp(_baseSun, _baseSun * 0.35f, k) * Mathf.Lerp(1f, 0.16f, n) + flash * 2.5f;
                _sun.shadowStrength = Mathf.Lerp(Mathf.Lerp(_baseShadow, 0.2f, k), 0.25f, n);
                _sun.color = Color.Lerp(Color.Lerp(_baseSunColor, DuskLight, du), MoonLight, n);
                var e = _baseSunRot.eulerAngles;
                var day = Quaternion.Euler(Mathf.Lerp(14f, e.x, Mathf.Clamp01(_sunHeight * 1.6f)), e.y, 0);
                _sun.transform.rotation = Quaternion.Slerp(day, Quaternion.Euler(48f, e.y + 140f, 0), n);
            }
            if (_sky != null)
            {
                var tint = Color.Lerp(Color.Lerp(_baseSkyTint, StormSky, k), DuskSky, du * 0.8f);
                if (_sky.HasProperty("_SkyTint")) _sky.SetColor("_SkyTint", Color.Lerp(tint, NightSky, n));
                if (_sky.HasProperty("_Exposure")) _sky.SetFloat("_Exposure", Mathf.Lerp(_baseExposure, _baseExposure * 0.45f, k) * Mathf.Lerp(1f, 0.14f, n) + flash * 1.5f);
            }
            MaterialLibrary.SetNight(n);
            if (_cam != null && _cam.clearFlags == CameraClearFlags.SolidColor)
                _cam.backgroundColor = RenderSettings.fogColor;
            _road?.SetWetness(Mathf.Clamp01(wet * 1.4f));
            Services.Audio?.SetRain(wet < 0.05f ? 0 : 0.35f + 0.65f * wet);
        }
    }
}
