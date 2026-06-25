using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    // 스테이지 에디터 만들거 고려
    [CreateAssetMenu(menuName = "The Laser/Stage Data", fileName = "StageData_")]
    public class StageData : ScriptableObject
    {
        [Header("Stage Info")]
        public int stageNumber = 1;
        public string stageName = "Stage 1";

        [Header("Grid Size")]
        [Min(1)] public int width = 8;
        [Min(1)] public int height = 8;

        [Header("Player Start")]
        public Vector2Int playerStartPosition;
        public GridDirection playerStartDirection = GridDirection.Right;

        [Header("Cells")]
        public List<Vector2Int> wallPositions = new();
        public List<Vector2Int> targetPositions = new(); // 기존 일반 도착지 호환용

        [Header("Advanced Targets")]
        public List<StageTargetData> advancedTargets = new();
        public List<int> sequenceLockPattern = new();

        [Header("Distance Sensors")]
        public List<DistanceSensorData> distanceSensors = new();

        [Header("Transform Zones")]
        public List<TransformZoneData> transformZones = new();

        [Header("Puzzle Objects")]
        public List<StageObjectData> objects = new();

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
    }
}
