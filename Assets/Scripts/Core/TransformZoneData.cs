using System;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class TransformZoneData
    {
        public string zoneId;
        public Vector2Int center;
        [Min(1)] public int width = 3;
        [Min(1)] public int height = 3;

        public TransformZoneType zoneType = TransformZoneType.Rotate90;

        [Header("Rotate90")]
        public bool clockwise = true;

        [Header("Mirror")]
        public MirrorAxis mirrorAxis = MirrorAxis.Vertical;
    }
}
