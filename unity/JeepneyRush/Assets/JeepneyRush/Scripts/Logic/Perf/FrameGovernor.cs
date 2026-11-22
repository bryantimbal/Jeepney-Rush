using System;

namespace JeepneyRush.Logic.Perf
{
    /// <summary>
    /// "Auto" graphics: picks a starting level from the device and then watches the frame
    /// time during play. A sustained slowdown steps the level down (shadows off, lower render
    /// scale); a long smooth stretch may step it back up once. Engine-free so it is unit-tested.
    /// </summary>
    public sealed class FrameGovernor
    {
        public const int Low = 0, Medium = 1, High = 2;
        /// <summary>Settings value meaning "Auto" (0..2 are fixed levels).</summary>
        public const int AutoSetting = 3;

        /// <summary>Frame budget the game aims for (60 fps).</summary>
        public const float TargetFrame = 1f / 60f;
        /// <summary>Averages slower than this (≈ 48 fps) count as struggling.</summary>
        public const float SlowFrame = 1f / 48f;
        /// <summary>Averages faster than this (≈ 58 fps) count as comfortable.</summary>
        public const float FastFrame = 1f / 58f;
        public const float DropAfter = 3f;      // seconds of struggling before stepping down
        public const float RaiseAfter = 25f;    // seconds of comfort before stepping up
        public const float SettleTime = 2.5f;   // ignore frames right after a change / a loading hitch
        public const float HitchFrame = 0.25f;  // single frames this long are loading hitches, not load

        private float _avg = TargetFrame, _slowT, _fastT, _settle = SettleTime;
        private bool _raisedOnce;

        public int Level { get; private set; }
        /// <summary>Highest level this device is allowed to climb back to.</summary>
        public int Ceiling { get; }
        public int Changes { get; private set; }

        public FrameGovernor(int startLevel, int ceiling)
        {
            Ceiling = Clamp(ceiling);
            Level = Math.Min(Clamp(startLevel), Ceiling);
        }

        /// <summary>Starting level from device memory (MB) and CPU cores.</summary>
        public static int StartLevel(int memoryMb, int cores)
        {
            if (memoryMb > 0 && memoryMb < 3000 || cores > 0 && cores < 4) return Low;
            if (memoryMb < 6000 || cores < 8) return Medium;
            return High;
        }

        /// <summary>Feeds one frame; returns true when <see cref="Level"/> changed.</summary>
        public bool Tick(float dt)
        {
            if (dt <= 0f) return false;
            if (dt >= HitchFrame) { _settle = SettleTime; return false; } // spawn / GC / app switch
            if (_settle > 0f) { _settle -= dt; _avg = TargetFrame; return false; }
            _avg += (dt - _avg) * 0.05f; // ~20-frame moving average
            if (_avg > SlowFrame) { _slowT += dt; _fastT = 0f; }
            else if (_avg < FastFrame) { _fastT += dt; _slowT = 0f; }
            else { _slowT = Math.Max(0f, _slowT - dt); _fastT = Math.Max(0f, _fastT - dt); }

            if (_slowT >= DropAfter && Level > Low) return Change(Level - 1);
            // climb back at most once per session so it never ping-pongs between levels
            if (_fastT >= RaiseAfter && Level < Ceiling && !_raisedOnce) { _raisedOnce = true; return Change(Level + 1); }
            return false;
        }

        private bool Change(int level)
        {
            Level = level;
            Changes++;
            _slowT = _fastT = 0f;
            _settle = SettleTime;
            _avg = TargetFrame;
            return true;
        }

        private static int Clamp(int l) => Math.Max(Low, Math.Min(High, l));
    }
}
