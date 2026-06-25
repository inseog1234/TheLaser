using System;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class StageTargetData
    {
        [Header("Target Info")]
        public string targetId;
        public TargetType targetType = TargetType.Normal;
        public Vector2Int position;

        [Header("Color Target")]
        public LaserColorKind requiredColor = LaserColorKind.Default;

        [Header("Sequence Target")]
        public int sequenceValue = 1;

        [Header("Intersection Target")]
        [Min(0.01f)] public float detectionRadius = 0.25f;
        public bool requireDifferentColors;

        [Header("Laser Flow")]
        public bool stopLaserOnHit = true;
    }
}
