using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Specification of a playable jeepney. Phase 1 ships two free liveries; the Phase 2
    /// garage adds price, unlock requirements and upgrade stats to this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Jeepney Rush/Jeepney Spec", fileName = "JeepneySpec")]
    public sealed class JeepneySpec : ScriptableObject
    {
        [Tooltip("Must match an id in JeepneyCatalog (livery colours & art).")]
        public string liveryId = JeepneyCatalog.DefaultId;
        public string displayName = "Pilipinas Classic";
        [TextArea] public string description = "The classic stainless-steel jeepney, painted proud.";
        [Range(1, 15)] public int passengerCapacity = 5;
        [Range(1, 6)] public int health = 3;
        [Tooltip("Visual speed/handling rating shown in the garage (1-5).")]
        [Range(1, 5)] public int handling = 3;

        public JeepneyLivery Livery => JeepneyCatalog.Get(liveryId);

        public static JeepneySpec[] LoadAll()
        {
            var specs = Resources.LoadAll<JeepneySpec>("Jeepneys");
            if (specs != null && specs.Length > 0) return specs;
            // defaults when the setup step has not created assets yet
            var list = new JeepneySpec[JeepneyCatalog.All.Length];
            for (int i = 0; i < list.Length; i++)
            {
                var s = CreateInstance<JeepneySpec>();
                s.liveryId = JeepneyCatalog.All[i].Id;
                s.displayName = JeepneyCatalog.All[i].DisplayName;
                s.description = i == 0 ? "Chrome fenders, sun-and-stars art and a PILIPINAS roof sign." : "Purple pride of the San Juan route, with beaded antennas.";
                list[i] = s;
            }
            return list;
        }
    }
}
