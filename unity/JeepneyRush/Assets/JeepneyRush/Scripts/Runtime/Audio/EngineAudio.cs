using JeepneyRush.Logic.Audio;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// The jeepney's engine sound, generated live on the audio thread by
    /// <see cref="DieselEngineSynth"/> and driven by a 5-speed <see cref="JeepneyDrivetrain"/>
    /// (revs climb, drop at each gear change, roar on GAS, idle when stopped).
    ///
    /// Your own recording wins: put a seamless idle loop of a real jeepney at
    /// Resources/Audio/jeepney_engine (wav/ogg; recorded at ~<see cref="RecordedIdleRpm"/> rpm) and
    /// it is pitched with the rpm instead of the synthesiser. Resources/Audio/jeepney_rev
    /// (optional) is played when GAS is pressed.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineAudio : MonoBehaviour
    {
        public const float RecordedIdleRpm = 800f;
        private AudioSource _src;
        private DieselEngineSynth _synth;
        private readonly JeepneyDrivetrain _drive = new JeepneyDrivetrain();
        private float[] _mono = new float[16384];
        private AudioClip _recorded;
        public AudioClip RecordedRev { get; private set; }

        // written by the main thread, read by the audio thread
        private volatile float _rpm = JeepneyDrivetrain.IdleRpm, _throttle, _volume;
        private volatile bool _running;
        private float _gain;
        private float _lastSpeed;

        public float Volume = 0.9f;
        public bool UsingRecording => _recorded != null;
        public JeepneyDrivetrain Drivetrain => _drive;

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = true;
            _src.spatialBlend = 0f;
            _recorded = Resources.Load<AudioClip>("Audio/jeepney_engine");
            RecordedRev = Resources.Load<AudioClip>("Audio/jeepney_rev");
            if (_recorded != null)
            {
                _src.clip = _recorded;
                Debug.Log("[Audio] using recorded jeepney engine: Resources/Audio/jeepney_engine");
            }
            else
            {
                int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
                _synth = new DieselEngineSynth(rate);
                // a silent looping clip keeps the source (and OnAudioFilterRead) running
                _src.clip = AudioClip.Create("EngineCarrier", rate, 1, rate, false);
                _src.volume = 1f;
            }
        }

        public bool Running => _running;

        public void Run(bool on)
        {
            _running = on;
            if (on)
            {
                _drive.Reset();
                if (!_src.isPlaying) _src.Play();
            }
            else if (_recorded != null) _src.Stop();
        }

        public void Pause(bool paused) { if (paused) _src.Pause(); else _src.UnPause(); }
        public void Mute(bool mute) => _src.mute = mute;

        /// <summary>Called every frame with the jeepney's state.</summary>
        public void Drive(float dt, float speed, bool gas, bool brake)
        {
            bool accelerating = Mathf.Abs(speed) > Mathf.Abs(_lastSpeed) + 0.02f;
            _lastSpeed = speed;
            _drive.Tick(dt, speed, gas, brake, accelerating);
            _rpm = _drive.Rpm;
            _throttle = _drive.Throttle;
            _volume = Volume * (0.7f + 0.3f * _drive.Throttle);
            if (_recorded != null)
            {
                _src.pitch = Mathf.Clamp(_drive.Rpm / RecordedIdleRpm, 0.5f, 3f);
                _src.volume = _running ? _volume : 0f;
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_synth == null) return;
            int frames = data.Length / channels;
            if (frames > _mono.Length) return; // never allocate on the audio thread
            _synth.TargetRpm = _rpm;
            _synth.TargetThrottle = _throttle;
            _synth.Render(_mono, 0, frames);
            float target = _running ? _volume : 0f;
            for (int i = 0; i < frames; i++)
            {
                _gain += (target - _gain) * 0.0008f; // ~25 ms fade in/out
                float v = _mono[i] * _gain;
                for (int c = 0; c < channels; c++) data[i * channels + c] = v;
            }
        }
    }
}
