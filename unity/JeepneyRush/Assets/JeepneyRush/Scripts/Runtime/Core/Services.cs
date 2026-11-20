namespace JeepneyRush
{
    /// <summary>
    /// Access point for the persistent managers created by <see cref="Bootstrap"/>.
    /// Gameplay code asks Services instead of searching the scene.
    /// </summary>
    public static class Services
    {
        public static GameManager Game { get; internal set; }
        public static SaveManager Save { get; internal set; }
        public static AudioManager Audio { get; internal set; }
        public static PoolManager Pools { get; internal set; }
        public static SceneLoader Scenes { get; internal set; }
        public static UIManager UI { get; internal set; }

        public static bool Ready => Game != null && Save != null && Audio != null && Pools != null && UI != null;
    }
}
