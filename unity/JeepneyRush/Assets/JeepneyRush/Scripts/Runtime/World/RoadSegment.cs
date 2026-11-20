using UnityEngine;

namespace JeepneyRush
{
    /// <summary>One recyclable 40 m slice of street scenery.</summary>
    public sealed class RoadSegment : MonoBehaviour
    {
        public float Length = Logic.Geometry.SceneryBuilder.ChunkLength;
        public int Variant;

        /// <summary>Far (front) edge of the segment in world Z.</summary>
        public float FrontZ => transform.localPosition.z + Length / 2f;
        public float BackZ => transform.localPosition.z - Length / 2f;

        public void MoveBy(float dz)
        {
            var p = transform.localPosition;
            p.z -= dz;
            transform.localPosition = p;
        }
    }
}
