using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class StageData
    {
        [Header("Stage Info")]
        public int stageNumber = 1;
        public string stageName = "Stage 1";
        public int chapterIndex = 1;
        public string chapterName = "Chapter 1";
        public int stageIndexInChapter = 1;
        public string chapterFeatureName = "";

        [Header("Audio / Tutorial")]
        public string bgmEventPath = "";
        public bool hasTutorial;
        public List<string> tutorialPages = new();

        [Header("Clear Hole")]
        public Vector2Int clearHolePosition;

        [Header("Grid Size")]
        [Min(1)] public int width = 8;
        [Min(1)] public int height = 8;

        [Header("Rule Limit")]
        public bool useLaserDistanceLimit;
        [Min(0)] public int laserMaxDistance = 20;
        [Tooltip("0이면 이동 제한 없음")]
        [Min(0)] public int moveLimit = 0;

        [Header("Player Start")]
        public Vector2Int playerStartPosition;
        public GridDirection playerStartDirection = GridDirection.Right;

        [Header("Cells")]
        public List<Vector2Int> wallPositions = new();
        public List<Vector2Int> targetPositions = new();

        [Header("Advanced Targets")]
        public List<StageTargetData> advancedTargets = new();
        public List<int> sequenceLockPattern = new();

        [Header("Distance Sensors")]
        public List<DistanceSensorData> distanceSensors = new();

        [Header("Transform Zones")]
        public List<TransformZoneData> transformZones = new();

        [Header("Puzzle Objects")]
        public List<StageObjectData> objects = new();

        [Header("Solution Recording")]
        public List<StageSolutionActionData> solutionActions = new();

        public bool HasSolution => solutionActions != null && solutionActions.Count > 0;

        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
        }

        public bool HasWall(Vector2Int position)
        {
            return wallPositions.Contains(position);
        }

        public bool HasTarget(Vector2Int position)
        {
            if (targetPositions.Contains(position))
                return true;

            for (int i = 0; i < advancedTargets.Count; i++)
            {
                if (advancedTargets[i] != null && advancedTargets[i].position == position)
                    return true;
            }

            return false;
        }

        public bool HasObject(Vector2Int position)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].position == position)
                    return true;
            }

            return false;
        }

        public StageObjectData GetObjectDataAt(Vector2Int position)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].position == position)
                    return objects[i];
            }

            return null;
        }

        public StageData Clone()
        {
            StageData clone = new StageData
            {
                stageNumber = stageNumber,
                stageName = stageName,
                chapterIndex = chapterIndex,
                chapterName = chapterName,
                stageIndexInChapter = stageIndexInChapter,
                chapterFeatureName = chapterFeatureName,
                bgmEventPath = bgmEventPath,
                hasTutorial = hasTutorial,
                tutorialPages = tutorialPages != null ? new List<string>(tutorialPages) : new List<string>(),
                clearHolePosition = clearHolePosition,
                width = width,
                height = height,
                useLaserDistanceLimit = useLaserDistanceLimit,
                laserMaxDistance = laserMaxDistance,
                moveLimit = moveLimit,
                playerStartPosition = playerStartPosition,
                playerStartDirection = playerStartDirection,
                wallPositions = new List<Vector2Int>(wallPositions),
                targetPositions = new List<Vector2Int>(targetPositions),
                sequenceLockPattern = new List<int>(sequenceLockPattern)
            };

            for (int i = 0; i < advancedTargets.Count; i++)
                clone.advancedTargets.Add(CloneTarget(advancedTargets[i]));

            for (int i = 0; i < distanceSensors.Count; i++)
                clone.distanceSensors.Add(CloneDistanceSensor(distanceSensors[i]));

            for (int i = 0; i < transformZones.Count; i++)
                clone.transformZones.Add(CloneTransformZone(transformZones[i]));

            for (int i = 0; i < objects.Count; i++)
                clone.objects.Add(CloneObject(objects[i]));

            if (solutionActions != null)
            {
                for (int i = 0; i < solutionActions.Count; i++)
                    clone.solutionActions.Add(CloneSolutionAction(solutionActions[i]));
            }

            return clone;
        }

        private static StageSolutionActionData CloneSolutionAction(StageSolutionActionData source)
        {
            return source != null ? source.Clone() : null;
        }

        private static StageObjectData CloneObject(StageObjectData source)
        {
            if (source == null)
                return null;

            return new StageObjectData
            {
                objectType = source.objectType,
                manipulationType = source.manipulationType,
                position = source.position,
                direction = source.direction,
                mirrorShape = source.mirrorShape,
                prismType = source.prismType,
                splitterMode = source.splitterMode,
                prismColor = source.prismColor,
                refractionMode = source.refractionMode,
                lensType = source.lensType,
                distanceBoost = source.distanceBoost
            };
        }

        private static StageTargetData CloneTarget(StageTargetData source)
        {
            if (source == null)
                return null;

            return new StageTargetData
            {
                targetId = source.targetId,
                targetType = source.targetType,
                position = source.position,
                requiredColor = source.requiredColor,
                sequenceValue = source.sequenceValue,
                detectionRadius = source.detectionRadius,
                requiredIntersectionCount = source.requiredIntersectionCount,
                intersectionColors = source.intersectionColors != null ? new List<LaserColorKind>(source.intersectionColors) : new List<LaserColorKind>(),
                requireDifferentColors = source.requireDifferentColors,
                stopLaserOnHit = source.stopLaserOnHit
            };
        }

        private static DistanceSensorData CloneDistanceSensor(DistanceSensorData source)
        {
            if (source == null)
                return null;

            DistanceSensorData clone = new DistanceSensorData
            {
                sensorId = source.sensorId,
                position = source.position,
                detectionRadius = source.detectionRadius,
                activateTransformZone = source.activateTransformZone,
                transformZoneId = source.transformZoneId,
                triggers = new List<DistanceSensorTriggerData>()
            };

            if (source.triggers != null)
            {
                for (int i = 0; i < source.triggers.Count; i++)
                    clone.triggers.Add(CloneDistanceSensorTrigger(source.triggers[i]));
            }

            return clone;
        }

        private static DistanceSensorTriggerData CloneDistanceSensorTrigger(DistanceSensorTriggerData source)
        {
            if (source == null)
                return null;

            return new DistanceSensorTriggerData
            {
                triggerId = source.triggerId,
                triggerKind = source.triggerKind,
                wallPosition = source.wallPosition,
                wallMoveTargetPosition = source.wallMoveTargetPosition,
                prismPosition = source.prismPosition,
                prismDirection = source.prismDirection,
                mirrorPosition = source.mirrorPosition,
                mirrorDirection = source.mirrorDirection,
                mirrorShape = source.mirrorShape,
                transformZoneId = source.transformZoneId
            };
        }

        private static TransformZoneData CloneTransformZone(TransformZoneData source)
        {
            if (source == null)
                return null;

            return new TransformZoneData
            {
                zoneId = source.zoneId,
                center = source.center,
                width = source.width,
                height = source.height,
                offsetX = source.offsetX,
                offsetY = source.offsetY,
                zoneType = source.zoneType,
                clockwise = source.clockwise,
                mirrorAxis = source.mirrorAxis
            };
        }
    }
}
