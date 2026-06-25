using System;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class DistanceSensorData
    {
        public string sensorId;
        public Vector2Int position;
        [Min(0.01f)] public float detectionRadius = 0.5f;

        [Header("Optional Trigger")]
        public bool activateTransformZone;
        public string transformZoneId;
    }
}
