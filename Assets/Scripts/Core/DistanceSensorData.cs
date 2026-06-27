using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public enum DistanceSensorTriggerKind
    {
        MoveWall = 0,
        ChangePrismDirection = 1,
        ActivateTransformZone = 2,
        ChangeMirrorState = 3
    }

    [Serializable]
    public class DistanceSensorTriggerData
    {
        public string triggerId;
        public DistanceSensorTriggerKind triggerKind = DistanceSensorTriggerKind.ActivateTransformZone;

        [Header("Move Wall")]
        public Vector2Int wallPosition;
        public Vector2Int wallMoveTargetPosition;

        [Header("Change Prism Direction / Legacy")]
        public Vector2Int prismPosition;
        public GridDirection prismDirection = GridDirection.Up;

        [Header("Change Mirror State")]
        public Vector2Int mirrorPosition;
        public GridDirection mirrorDirection = GridDirection.Up;
        public MirrorShape mirrorShape = MirrorShape.NormalL;

        [Header("Transform Zone")]
        public string transformZoneId;
    }

    [Serializable]
    public class DistanceSensorData
    {
        public string sensorId;
        public Vector2Int position;
        [Min(0.01f)] public float detectionRadius = 0.5f;

        [Header("Legacy Optional Trigger")]
        public bool activateTransformZone;
        public string transformZoneId;

        [Header("Triggers")]
        public List<DistanceSensorTriggerData> triggers = new();
    }
}
