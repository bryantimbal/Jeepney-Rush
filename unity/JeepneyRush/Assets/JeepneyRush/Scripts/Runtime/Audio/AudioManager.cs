using System;
using System.Collections;
using System.Collections.Generic;
using JeepneyRush.Logic.Audio;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Music (menu/run loops with crossfade), pooled SFX voices and the speed-following
    /// diesel engine. All clips are synthesised from code at load (original audio).
    /// Music and SFX have independent toggles persisted in the save.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private AudioClip _menuMusic, _engine, _rain;
        private readonly AudioClip[] _runMusicByRoute = new AudioClip[3];
        private AudioClip _runMusic => _runMusicByRoute[Mathf.Clamp(_route, 0, 2)];
        private int _route;
        private AudioSource _musicA, _musicB, _engineSrc, _rainSrc;
        private float _rainTarget;
        private Coroutine _fade;
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private int _nextVoice;
        private bool _musicOn = true, _sfxOn = true;
        private bool? _wantRunMusic;
        public bool Loaded { get; private set; }

        private void Awake()
        {
            _musicA = NewSource("MusicA", true);
            _musicB = NewSource("MusicB", true);
            _engineSrc = NewSource("Engine", true);
            _rainSrc = NewSource("Rain", true);
            for (int i = 0; i < 10; i++) _voices.Add(NewSource("Voice" + i, false));
            // coin sounds are played by the session with the combo pitch (PlayCoin)
            GameEvents.Landed += () => Play(Sfx.Land, 0.8f);
            GameEvents.LaneChanged += _ => Play(Sfx.Lane, 0.6f);
            GameEvents.Braked += () => Play(Sfx.Brake, 0.7f);
            GameEvents.Honked += () => Play(Sfx.Horn);
            GameEvents.Hit += (_, __) => Play(Sfx.Crash);
            GameEvents.PassengersServed += r =>
            {
                if (r.Alighted > 0) Play(Sfx.Deliver);
                if (r.Boarded > 0) Play(Sfx.Pickup);
                else if (r.Full) Play(Sfx.Full);
            };
            GameEvents.RunEnded += r => Play(r.NewBestScore ? Sfx.NewBest : Sfx.GameOver);
            GameEvents.PowerUpCollected += k => Play(k == JeepneyRush.Logic.PowerUpKind.Turbo ? Sfx.Turbo : Sfx.PowerUp);
            GameEvents.PassengersMissed += _ => Play(Sfx.Miss);
            GameEvents.ShieldBlocked += () => Play(Sfx.Shield);
            GameEvents.StateChanged += OnState;
        }

        private AudioSource NewSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.ignoreListenerPause = false;
            return s;
        }

        private static AudioClip Clip(string name, float[] data, bool loop = false)
        {
            var c = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Synthesises every clip, yielding between items (drives the splash progress bar).</summary>
        public IEnumerator LoadRoutine(Action<float> progress)
        {
            if (Loaded) { progress?.Invoke(1); yield break; }
            var all = new List<Sfx>(SoundBank.AllSfx);
            int total = all.Count + 5, done = 0;
            foreach (var s in all)
            {
                _clips[s] = Clip(s.ToString(), SoundBank.Make(s));
                progress?.Invoke(++done / (float)total);
                yield return null;
            }
            _engine = Clip("Engine", SoundBank.EngineLoop(), true);
            _rain = Clip("Rain", SoundBank.RainLoop(), true);
            progress?.Invoke(++done / (float)total);
            yield return null;
            _menuMusic = Clip("MenuMusic", MusicComposer.RenderMenu(), true);
            progress?.Invoke(++done / (float)total);
            yield return null;
            for (int e = 0; e < 3; e++)
            {
                _runMusicByRoute[e] = Clip("RunMusic" + e, MusicComposer.RenderRun((JeepneyRush.Logic.EnvironmentKind)e), true);
                progress?.Invoke(++done / (float)total);
                yield return null;
            }
            progress?.Invoke(1);
            Loaded = true;
            ApplySettings();
            if (_wantRunMusic.HasValue) PlayMusic(_wantRunMusic.Value);
        }

        /// <summary>Synchronous load used when the game starts directly in a gameplay scene (editor).</summary>
        public void EnsureLoaded()
        {
            if (Loaded) return;
            var it = LoadRoutine(null);
            while (it.MoveNext()) { }
        }

        public void ApplySettings()
        {
            var d = Services.Save != null ? Services.Save.Data : null;
            _musicOn = d == null || d.music;
            _sfxOn = d == null || d.sfx;
            _musicA.mute = _musicB.mute = !_musicOn;
            _engineSrc.mute = !_sfxOn;
            _rainSrc.mute = !_sfxOn;
            foreach (var v in _voices) v.mute = !_sfxOn;
        }

        private void OnState(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.MainMenu: PlayMusic(false); StopEngine(); SetRain(0); break;
                case GameState.Starting:
                case GameState.Playing:
                    PlayMusic(true);
                    StartEngine();
                    break;
                case GameState.Paused: _engineSrc.Pause(); _rainSrc.Pause(); break;
                case GameState.GameOver: StopEngine(); SetRain(0); break;
            }
            if (from == GameState.Paused && to == GameState.Playing) { _engineSrc.UnPause(); _rainSrc.UnPause(); }
        }

        // ------------------------------------------------------------------ music
        /// <summary>Selects the run music for a route (each route has its own arrangement).</summary>
        public void SetRoute(JeepneyRush.Logic.EnvironmentKind env) => _route = (int)env;

        public void PlayMusic(bool run)
        {
            _wantRunMusic = run; // remembered so music starts as soon as loading finishes
            var clip = run ? _runMusic : _menuMusic;
            if (clip == null) return;
            if (_musicA.clip == clip && _musicA.isPlaying) return;
            // crossfade: B becomes the new A
            (_musicA, _musicB) = (_musicB, _musicA);
            _musicA.clip = clip;
            _musicA.volume = 0f;
            _musicA.Play();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            float t = 0;
            float startB = _musicB.volume;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.8f;
                _musicA.volume = Mathf.Lerp(0, 0.55f, t);
                _musicB.volume = Mathf.Lerp(startB, 0, t);
                yield return null;
            }
            _musicB.Stop();
        }

        // ------------------------------------------------------------------ engine
        public void StartEngine()
        {
            if (_engine == null || _engineSrc.isPlaying) return;
            _engineSrc.clip = _engine;
            _engineSrc.volume = 0.32f;
            _engineSrc.pitch = 1f;
            _engineSrc.Play();
        }

        public void StopEngine() => _engineSrc.Stop();

        /// <summary>
        /// 0..1 speed factor → pitch/volume of the diesel loop. Holding the accelerator
        /// revs the engine harder and louder (the rev "vroom" itself is <see cref="Rev"/>).
        /// </summary>
        public void UpdateEngine(float speed01, bool braking, bool gas = false)
        {
            if (!_engineSrc.isPlaying) return;
            float targetPitch = 0.85f + speed01 * 0.9f - (braking ? 0.15f : 0f) + (gas ? 0.3f : 0f);
            _engineSrc.pitch = Mathf.MoveTowards(_engineSrc.pitch, targetPitch, Time.deltaTime * (gas ? 2.4f : 1.5f));
            float targetVol = 0.25f + speed01 * 0.12f + (gas ? 0.16f : 0f);
            _engineSrc.volume = Mathf.MoveTowards(_engineSrc.volume, targetVol, Time.deltaTime * 1.2f);
        }

        /// <summary>Stepping on the accelerator: the diesel revs up with a throaty roar.</summary>
        public void Rev() => Play(Sfx.Rev, 0.85f, UnityEngine.Random.Range(0.95f, 1.05f));

        // ------------------------------------------------------------------ weather
        /// <summary>Rain loop volume 0..1 (faded smoothly).</summary>
        public void SetRain(float volume)
        {
            _rainTarget = Mathf.Clamp01(volume);
            if (_rainTarget > 0 && _rain != null && !_rainSrc.isPlaying) { _rainSrc.clip = _rain; _rainSrc.volume = 0; _rainSrc.Play(); }
        }

        private void Update()
        {
            if (_rainSrc == null) return;
            _rainSrc.volume = Mathf.MoveTowards(_rainSrc.volume, _rainTarget * 0.55f, Time.unscaledDeltaTime * 0.4f);
            if (_rainTarget <= 0 && _rainSrc.isPlaying && _rainSrc.volume <= 0.001f) _rainSrc.Stop();
        }

        /// <summary>Thunder a moment after the lightning flash (sound is slower than light).</summary>
        public void Thunder(float delay) => StartCoroutine(PlayLater(Sfx.Thunder, delay, UnityEngine.Random.Range(0.8f, 1f)));

        private IEnumerator PlayLater(Sfx s, float delay, float volume)
        {
            yield return new WaitForSeconds(delay);
            Play(s, volume, UnityEngine.Random.Range(0.85f, 1.1f));
        }

        // ------------------------------------------------------------------ sfx
        public void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            if (!_sfxOn || !_clips.TryGetValue(s, out var clip)) return;
            var v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Count;
            v.pitch = pitch;
            v.PlayOneShot(clip, volume);
        }

        /// <summary>Coin blip whose pitch rises with the coin combo – satisfying on long trails.</summary>
        public void PlayCoin(float pitch) => Play(Sfx.Coin, 0.7f, Mathf.Clamp(pitch, 0.5f, 2.2f));

        public void Click() => Play(Sfx.Click, 0.6f);

        private void OnApplicationPause(bool paused) => AudioListener.pause = paused;
    }
}
