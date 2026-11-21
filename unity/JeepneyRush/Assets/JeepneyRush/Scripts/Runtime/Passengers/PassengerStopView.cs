using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// A roadside pickup point: waiting shed, stop sign, yellow PARA zone (trigger),
    /// floating arrow and the commuters waving the jeepney down.
    /// </summary>
    public sealed class PassengerStopView : MonoBehaviour
    {
        public PassengerStop Stop;
        public bool Done;
        public readonly List<Transform> Waiting = new List<Transform>();
        public readonly List<int> WaitingVariants = new List<int>();
        public Transform Shed, Arrow, DropArrow, Zone;
        public GameObject Sign;
        public BoxCollider Trigger;

        public static GameObject Create(float zoneWidth, float zoneLength)
        {
            var root = new GameObject("PassengerStop");
            var v = root.AddComponent<PassengerStopView>();
            v.Shed = ModelLibrary.Instantiate(ModelLibrary.Shed(), "Shed", root.transform).transform;
            v.Zone = ModelLibrary.Instantiate(ModelLibrary.Zone(zoneWidth, zoneLength), "Zone", root.transform, castShadows: false).transform;
            v.Arrow = ModelLibrary.Instantiate(ModelLibrary.StopArrow(false), "Arrow", root.transform, castShadows: false).transform;
            v.DropArrow = ModelLibrary.Instantiate(ModelLibrary.StopArrow(true), "DropArrow", root.transform, castShadows: false).transform;
            var trig = new GameObject("Trigger");
            trig.transform.SetParent(root.transform, false);
            v.Trigger = trig.AddComponent<BoxCollider>();
            v.Trigger.isTrigger = true;
            v.Trigger.size = new Vector3(zoneWidth, 2.5f, zoneLength);
            v.Trigger.center = new Vector3(0, 1.25f, 0);
            var rb = trig.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            trig.AddComponent<StopZoneTrigger>().View = v;
            return root;
        }
    }

    /// <summary>Marker on the zone collider so CollisionHandler can find the stop.</summary>
    public sealed class StopZoneTrigger : MonoBehaviour
    {
        public PassengerStopView View;
    }
}
