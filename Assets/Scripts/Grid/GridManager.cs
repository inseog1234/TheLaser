using System.Collections.Generic;
using UnityEngine;
using Core;
using Laser;

namespace Grid
{
    public class GridManager : MonoBehaviour
    {
        [Header("Stage")]
        [SerializeField] private StageData stageData;
        [SerializeField] private bool loadOnAwake = true;

        [Header("Grid World Setting")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 originWorldPosition = Vector3.zero;
        [SerializeField] private bool centerStageOnOrigin = true;

        [Header("Runtime Object Spawn")]
        [SerializeField] private GridObject objectPrefab;
        [SerializeField] private Transform objectParent;

        [Header("Runtime Floor Spawn")]
        [SerializeField] private GridFloorTile floorTilePrefab;
        [SerializeField] private Transform floorTileParent;

        [Header("Runtime Target Spawn")]
        [SerializeField] private GridTarget targetPrefab;
        [SerializeField] private Transform targetParent;

        [Header("Runtime Distance Sensor Spawn")]
        [SerializeField] private GridDistanceSensor distanceSensorPrefab;
        [SerializeField] private Transform distanceSensorParent;

        [Header("Runtime Transform Zone Spawn")]
        [SerializeField] private GridTransformZone transformZonePrefab;
        [SerializeField] private Transform transformZoneParent;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private readonly Dictionary<Vector2Int, GridCell> cells = new();
        private readonly Dictionary<Vector2Int, GridObject> objects = new();
        private readonly Dictionary<Vector2Int, GridTarget> targets = new();
        private readonly Dictionary<string, GridTransformZone> transformZones = new();

        private readonly List<GridObject> spawnedObjects = new();
        private readonly List<GridObject> spawnedFixedWalls = new();
        private readonly List<GridFloorTile> spawnedFloorTiles = new();
        private readonly List<GridTarget> spawnedTargets = new();
        private readonly List<GridDistanceSensor> spawnedDistanceSensors = new();
        private readonly List<GridTransformZone> spawnedTransformZones = new();

        public StageData CurrentStageData => stageData;
        public int Width => stageData != null ? stageData.width : 0;
        public int Height => stageData != null ? stageData.height : 0;
        public float CellSize => cellSize;

        private void Awake()
        {
            if (loadOnAwake && stageData != null)
                LoadStage(stageData);
        }

        public void LoadStage(StageData targetStageData)
        {
            if (targetStageData == null)
                return;

            stageData = targetStageData;

            ClearRuntimeGrid();
            CreateCells();
            ApplyStageCells();
            SpawnFloorTiles();
            SpawnTargets();
            SpawnDistanceSensors();
            SpawnTransformZones();
            SpawnFixedWalls();
            SpawnStageObjects();
        }

        private void ClearRuntimeGrid()
        {
            cells.Clear();
            objects.Clear();
            targets.Clear();
            transformZones.Clear();

            DestroyList(spawnedObjects);
            DestroyList(spawnedFixedWalls);
            DestroyList(spawnedFloorTiles);
            DestroyList(spawnedTargets);
            DestroyList(spawnedDistanceSensors);
            DestroyList(spawnedTransformZones);
        }

        private void DestroyList<T>(List<T> list) where T : MonoBehaviour
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null)
                    Destroy(list[i].gameObject);
            }

            list.Clear();
        }

        private void CreateCells()
        {
            for (int y = 0; y < stageData.height; y++)
            {
                for (int x = 0; x < stageData.width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    GridCell cell = new GridCell(position, CellType.Empty);
                    cells.Add(position, cell);
                }
            }
        }

        private void ApplyStageCells()
        {
            for (int i = 0; i < stageData.wallPositions.Count; i++)
            {
                Vector2Int wallPosition = stageData.wallPositions[i];

                if (!IsInside(wallPosition))
                    continue;

                cells[wallPosition].SetCellType(CellType.Wall);
            }

            for (int i = 0; i < stageData.targetPositions.Count; i++)
            {
                Vector2Int targetPosition = stageData.targetPositions[i];

                if (!IsInside(targetPosition) || cells[targetPosition].IsWall)
                    continue;

                cells[targetPosition].SetCellType(CellType.Target);
            }

            if (stageData.advancedTargets != null)
            {
                for (int i = 0; i < stageData.advancedTargets.Count; i++)
                {
                    StageTargetData data = stageData.advancedTargets[i];

                    if (data == null)
                        continue;

                    if (!IsInside(data.position) || cells[data.position].IsWall)
                        continue;

                    cells[data.position].SetCellType(CellType.Target);
                }
            }
        }

        private void SpawnFloorTiles()
        {
            if (floorTilePrefab == null)
                return;

            if (floorTileParent == null)
                floorTileParent = transform;

            for (int y = 0; y < stageData.height; y++)
            {
                for (int x = 0; x < stageData.width; x++)
                {
                    Vector2Int gridPosition = new Vector2Int(x, y);
                    Vector3 worldPosition = GridToWorld(gridPosition);

                    GridFloorTile floorTile = Instantiate(floorTilePrefab, worldPosition, Quaternion.identity, floorTileParent);
                    floorTile.name = $"FloorTile_{gridPosition}";
                    floorTile.Initialize(cellSize, gridPosition);
                    spawnedFloorTiles.Add(floorTile);
                }
            }
        }

        private void SpawnTargets()
        {
            if (targetPrefab == null)
                return;

            if (targetParent == null)
                targetParent = transform;

            // 기존 targetPositions는 일반 도착지로 생성
            for (int i = 0; i < stageData.targetPositions.Count; i++)
            {
                Vector2Int targetPosition = stageData.targetPositions[i];

                if (HasAdvancedTargetAt(targetPosition))
                    continue;

                SpawnNormalTarget(targetPosition);
            }

            if (stageData.advancedTargets == null)
                return;

            for (int i = 0; i < stageData.advancedTargets.Count; i++)
            {
                StageTargetData data = stageData.advancedTargets[i];

                if (data == null)
                    continue;

                if (!IsInside(data.position) || HasWall(data.position) || targets.ContainsKey(data.position))
                    continue;

                Vector3 worldPosition = GridToWorld(data.position);
                GridTarget target = Instantiate(targetPrefab, worldPosition, Quaternion.identity, targetParent);
                target.name = $"Target_{data.targetType}_{data.position}";
                target.Initialize(data);

                spawnedTargets.Add(target);
                targets.Add(data.position, target);
            }
        }

        private bool HasAdvancedTargetAt(Vector2Int position)
        {
            if (stageData.advancedTargets == null)
                return false;

            for (int i = 0; i < stageData.advancedTargets.Count; i++)
            {
                if (stageData.advancedTargets[i] != null && stageData.advancedTargets[i].position == position)
                    return true;
            }

            return false;
        }

        private void SpawnNormalTarget(Vector2Int targetPosition)
        {
            if (!IsInside(targetPosition) || HasWall(targetPosition) || targets.ContainsKey(targetPosition))
                return;

            Vector3 worldPosition = GridToWorld(targetPosition);
            GridTarget target = Instantiate(targetPrefab, worldPosition, Quaternion.identity, targetParent);
            target.name = $"Target_{targetPosition}";
            target.Initialize(targetPosition);

            spawnedTargets.Add(target);
            targets.Add(targetPosition, target);
        }

        private void SpawnDistanceSensors()
        {
            if (distanceSensorPrefab == null || stageData.distanceSensors == null)
                return;

            if (distanceSensorParent == null)
                distanceSensorParent = transform;

            for (int i = 0; i < stageData.distanceSensors.Count; i++)
            {
                DistanceSensorData data = stageData.distanceSensors[i];

                if (data == null || !IsInside(data.position))
                    continue;

                Vector3 worldPosition = GridToWorld(data.position);
                GridDistanceSensor sensor = Instantiate(distanceSensorPrefab, worldPosition, Quaternion.identity, distanceSensorParent);
                sensor.name = string.IsNullOrWhiteSpace(data.sensorId) ? $"DistanceSensor_{data.position}" : data.sensorId;
                sensor.Initialize(data);

                spawnedDistanceSensors.Add(sensor);
            }
        }

        private void SpawnTransformZones()
        {
            if (transformZonePrefab == null || stageData.transformZones == null)
                return;

            if (transformZoneParent == null)
                transformZoneParent = transform;

            for (int i = 0; i < stageData.transformZones.Count; i++)
            {
                TransformZoneData data = stageData.transformZones[i];

                if (data == null || !IsInside(data.center))
                    continue;

                Vector3 worldPosition = GridToWorld(data.center);
                GridTransformZone zone = Instantiate(transformZonePrefab, worldPosition, Quaternion.identity, transformZoneParent);
                zone.name = string.IsNullOrWhiteSpace(data.zoneId) ? $"TransformZone_{data.center}" : data.zoneId;
                zone.Initialize(this, data);

                spawnedTransformZones.Add(zone);

                if (!transformZones.ContainsKey(zone.ZoneId))
                    transformZones.Add(zone.ZoneId, zone);
            }
        }

        private void SpawnFixedWalls()
        {
            if (objectPrefab == null)
                return;

            if (objectParent == null)
                objectParent = transform;

            for (int i = 0; i < stageData.wallPositions.Count; i++)
            {
                Vector2Int wallPosition = stageData.wallPositions[i];

                if (!IsInside(wallPosition))
                    continue;

                StageObjectData wallData = new StageObjectData
                {
                    objectType = PuzzleObjectType.Wall,
                    manipulationType = ManipulationType.None,
                    position = wallPosition,
                    direction = GridDirection.Up,
                    mirrorShape = MirrorShape.NormalL
                };

                Vector3 worldPosition = GridToWorld(wallPosition);
                GridObject wallObject = Instantiate(objectPrefab, worldPosition, Quaternion.identity, objectParent);
                wallObject.name = $"FixedWall_{wallPosition}";
                wallObject.Initialize(wallData, worldPosition);
                spawnedFixedWalls.Add(wallObject);
            }
        }

        private void SpawnStageObjects()
        {
            if (objectPrefab == null)
                return;

            if (objectParent == null)
                objectParent = transform;

            for (int i = 0; i < stageData.objects.Count; i++)
            {
                StageObjectData objectData = stageData.objects[i];

                if (objectData == null)
                    continue;

                if (!IsInside(objectData.position) || HasWall(objectData.position) || HasObject(objectData.position))
                    continue;

                Vector3 worldPosition = GridToWorld(objectData.position);
                GridObject spawnedObject = Instantiate(objectPrefab, worldPosition, Quaternion.identity, objectParent);
                spawnedObject.name = $"{objectData.objectType}_{objectData.manipulationType}_{objectData.position}";
                spawnedObject.Initialize(objectData, worldPosition);

                RegisterObject(spawnedObject);
                spawnedObjects.Add(spawnedObject);
            }
        }

        public void EvaluateLaserResult(LaserResult result)
        {
            ResetAllTargets();
            ResetAllDistanceSensors();

            if (result == null)
                return;

            EvaluateNormalAndColorTargets(result);
            EvaluateSequenceTargets(result);
            EvaluateDistanceSensors(result);
            EvaluateIntersectionTargets(result);
        }

        private void EvaluateNormalAndColorTargets(LaserResult result)
        {
            for (int i = 0; i < result.TargetHits.Count; i++)
            {
                LaserTargetHit hit = result.TargetHits[i];
                GridTarget target = GetTargetAt(hit.Position);

                if (target == null)
                    continue;

                if (target.TargetType == TargetType.Normal)
                {
                    target.SetActivated(true);
                }
                else if (target.TargetType == TargetType.ColorLocked)
                {
                    bool matched = target.RequiredColor == hit.Color;
                    target.SetActivated(matched);
                    target.SetFailed(!matched);
                }
            }
        }

        private void EvaluateSequenceTargets(LaserResult result)
        {
            if (stageData.sequenceLockPattern == null || stageData.sequenceLockPattern.Count <= 0)
                return;

            List<GridTarget> hitSequenceTargets = new();
            List<LaserTargetHit> hitSequenceInfos = new();

            for (int i = 0; i < result.TargetHits.Count; i++)
            {
                LaserTargetHit hit = result.TargetHits[i];
                GridTarget target = GetTargetAt(hit.Position);

                if (target == null)
                    continue;

                if (target.TargetType == TargetType.SequenceLocked ||
                    target.TargetType == TargetType.SequenceColorLocked)
                {
                    hitSequenceTargets.Add(target);
                    hitSequenceInfos.Add(hit);
                }
            }

            if (hitSequenceTargets.Count < stageData.sequenceLockPattern.Count)
                return;

            bool matched = true;

            for (int i = 0; i < stageData.sequenceLockPattern.Count; i++)
            {
                GridTarget target = hitSequenceTargets[i];
                LaserTargetHit hit = hitSequenceInfos[i];

                if (target.SequenceValue != stageData.sequenceLockPattern[i])
                {
                    matched = false;
                    break;
                }

                if (target.TargetType == TargetType.SequenceColorLocked &&
                    target.RequiredColor != hit.Color)
                {
                    matched = false;
                    break;
                }
            }

            for (int i = 0; i < hitSequenceTargets.Count; i++)
            {
                hitSequenceTargets[i].SetActivated(matched);
                hitSequenceTargets[i].SetFailed(!matched);
            }
        }

        private void EvaluateDistanceSensors(LaserResult result)
        {
            for (int i = 0; i < spawnedDistanceSensors.Count; i++)
            {
                GridDistanceSensor sensor = spawnedDistanceSensors[i];

                if (sensor == null)
                    continue;

                Vector2 point = new Vector2(sensor.GridPosition.x, sensor.GridPosition.y);
                bool activated = false;

                for (int j = 0; j < result.Segments.Count; j++)
                {
                    LaserSegment segment = result.Segments[j];
                    float distance = LaserGeometryUtility.DistancePointToSegment(point, segment.Start, segment.End);

                    if (distance <= sensor.DetectionRadius)
                    {
                        activated = true;
                        break;
                    }
                }

                sensor.SetActivated(activated);

                if (activated && sensor.ActivateTransformZone && !string.IsNullOrWhiteSpace(sensor.TransformZoneId))
                    ApplyTransformZone(sensor.TransformZoneId);
            }
        }

        private void EvaluateIntersectionTargets(LaserResult result)
        {
            for (int i = 0; i < spawnedTargets.Count; i++)
            {
                GridTarget target = spawnedTargets[i];

                if (target == null || target.TargetType != TargetType.Intersection)
                    continue;

                Vector2 targetPoint = new Vector2(target.GridPosition.x, target.GridPosition.y);
                bool activated = false;

                for (int a = 0; a < result.Segments.Count; a++)
                {
                    for (int b = a + 1; b < result.Segments.Count; b++)
                    {
                        LaserSegment segmentA = result.Segments[a];
                        LaserSegment segmentB = result.Segments[b];

                        if (segmentA.BeamId == segmentB.BeamId)
                            continue;

                        if (target.RequireDifferentColors && segmentA.Color == segmentB.Color)
                            continue;

                        if (!LaserGeometryUtility.TryGetSegmentIntersection(segmentA.Start, segmentA.End, segmentB.Start, segmentB.End, out Vector2 intersection))
                            continue;

                        if (Vector2.Distance(targetPoint, intersection) <= target.DetectionRadius)
                        {
                            activated = true;
                            break;
                        }
                    }

                    if (activated)
                        break;
                }

                target.SetActivated(activated);
            }
        }

        public void ApplyTransformZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return;

            if (!transformZones.TryGetValue(zoneId, out GridTransformZone zone))
                return;

            ApplyTransformZone(zone);
        }

        public void ApplyTransformZone(GridTransformZone zone)
        {
            if (zone == null)
                return;

            List<GridObject> affectedObjects = new();

            foreach (KeyValuePair<Vector2Int, GridObject> pair in objects)
            {
                if (pair.Value != null && zone.Contains(pair.Key))
                    affectedObjects.Add(pair.Value);
            }

            if (affectedObjects.Count <= 0)
                return;

            Dictionary<GridObject, Vector2Int> nextPositions = new();
            Dictionary<GridObject, GridDirection> nextDirections = new();
            Dictionary<GridObject, MirrorShape> nextShapes = new();
            HashSet<Vector2Int> occupiedByAffected = new();

            for (int i = 0; i < affectedObjects.Count; i++)
                occupiedByAffected.Add(affectedObjects[i].GridPosition);

            HashSet<Vector2Int> reserved = new();

            for (int i = 0; i < affectedObjects.Count; i++)
            {
                GridObject obj = affectedObjects[i];
                Vector2Int nextPosition = TransformPosition(obj.GridPosition, zone);
                GridDirection nextDirection = TransformDirection(obj.Direction, zone);
                MirrorShape nextShape = TransformMirrorShape(obj.MirrorShape, zone);

                if (!IsInside(nextPosition) || HasWall(nextPosition))
                    return;

                if (reserved.Contains(nextPosition))
                    return;

                GridObject other = GetObjectAt(nextPosition);
                if (other != null && !occupiedByAffected.Contains(nextPosition))
                    return;

                reserved.Add(nextPosition);
                nextPositions.Add(obj, nextPosition);
                nextDirections.Add(obj, nextDirection);
                nextShapes.Add(obj, nextShape);
            }

            for (int i = 0; i < affectedObjects.Count; i++)
                UnregisterObject(affectedObjects[i]);

            for (int i = 0; i < affectedObjects.Count; i++)
            {
                GridObject obj = affectedObjects[i];
                Vector2Int nextPosition = nextPositions[obj];
                obj.ApplyTransformedState(nextPosition, nextDirections[obj], nextShapes[obj], GridToWorld(nextPosition));
                RegisterObject(obj);
            }
        }

        private Vector2Int TransformPosition(Vector2Int position, GridTransformZone zone)
        {
            Vector2Int relative = position - zone.Center;

            if (zone.ZoneType == TransformZoneType.Rotate90)
            {
                Vector2Int rotated = zone.Clockwise
                    ? new Vector2Int(relative.y, -relative.x)
                    : new Vector2Int(-relative.y, relative.x);

                return zone.Center + rotated;
            }

            if (zone.ZoneType == TransformZoneType.Mirror)
            {
                Vector2Int mirrored = zone.MirrorAxis == MirrorAxis.Vertical
                    ? new Vector2Int(-relative.x, relative.y)
                    : new Vector2Int(relative.x, -relative.y);

                return zone.Center + mirrored;
            }

            return position;
        }

        private GridDirection TransformDirection(GridDirection direction, GridTransformZone zone)
        {
            if (zone.ZoneType == TransformZoneType.Rotate90)
                return zone.Clockwise ? direction.RotateClockwise() : direction.RotateCounterClockwise();

            if (zone.ZoneType == TransformZoneType.Mirror)
            {
                if (zone.MirrorAxis == MirrorAxis.Vertical)
                {
                    return direction switch
                    {
                        GridDirection.Left => GridDirection.Right,
                        GridDirection.Right => GridDirection.Left,
                        _ => direction
                    };
                }

                return direction switch
                {
                    GridDirection.Up => GridDirection.Down,
                    GridDirection.Down => GridDirection.Up,
                    _ => direction
                };
            }

            return direction;
        }

        private MirrorShape TransformMirrorShape(MirrorShape shape, GridTransformZone zone)
        {
            if (shape == MirrorShape.None)
                return shape;

            if (zone.ZoneType != TransformZoneType.Mirror)
                return shape;

            if (shape == MirrorShape.NormalL)
                return MirrorShape.ReverseL;

            if (shape == MirrorShape.ReverseL)
                return MirrorShape.NormalL;

            return shape;
        }

        private Vector3 GetStageCenterOffset()
        {
            if (stageData == null)
                return Vector3.zero;

            float offsetX = (stageData.width - 1) * cellSize * 0.5f;
            float offsetY = (stageData.height - 1) * cellSize * 0.5f;

            return new Vector3(offsetX, offsetY, 0f);
        }

        public bool IsInside(Vector2Int position)
        {
            if (stageData == null)
                return false;

            return position.x >= 0 && position.y >= 0 && position.x < stageData.width && position.y < stageData.height;
        }

        public bool HasCell(Vector2Int position)
        {
            return cells.ContainsKey(position);
        }

        public GridCell GetCell(Vector2Int position)
        {
            if (!cells.TryGetValue(position, out GridCell cell))
                return null;

            return cell;
        }

        public bool HasWall(Vector2Int position)
        {
            GridCell cell = GetCell(position);
            return cell != null && cell.IsWall;
        }

        public bool HasTarget(Vector2Int position)
        {
            GridCell cell = GetCell(position);
            return cell != null && cell.IsTarget;
        }

        public bool HasObject(Vector2Int position)
        {
            return objects.ContainsKey(position);
        }

        public GridObject GetObjectAt(Vector2Int position)
        {
            if (!objects.TryGetValue(position, out GridObject gridObject))
                return null;

            return gridObject;
        }

        public bool IsWalkable(Vector2Int position)
        {
            GridCell cell = GetCell(position);

            if (cell == null)
                return false;

            return cell.IsWalkable;
        }

        public bool IsEmpty(Vector2Int position)
        {
            if (!IsInside(position))
                return false;

            if (HasWall(position))
                return false;

            if (HasObject(position))
                return false;

            return true;
        }

        public void RegisterObject(GridObject gridObject)
        {
            if (gridObject == null)
                return;

            Vector2Int position = gridObject.GridPosition;

            if (!IsInside(position) || HasObject(position))
                return;

            objects.Add(position, gridObject);

            GridCell cell = GetCell(position);
            if (cell != null)
                cell.SetObject(gridObject);
        }

        public void UnregisterObject(GridObject gridObject)
        {
            if (gridObject == null)
                return;

            Vector2Int position = gridObject.GridPosition;

            if (objects.ContainsKey(position))
                objects.Remove(position);

            GridCell cell = GetCell(position);
            if (cell != null)
                cell.ClearObject();
        }

        public bool TryMoveObject(GridObject gridObject, Vector2Int targetPosition)
        {
            if (gridObject == null || !IsEmpty(targetPosition))
                return false;

            Vector2Int currentPosition = gridObject.GridPosition;

            if (objects.ContainsKey(currentPosition))
                objects.Remove(currentPosition);

            GridCell currentCell = GetCell(currentPosition);
            if (currentCell != null)
                currentCell.ClearObject();

            objects.Add(targetPosition, gridObject);

            GridCell targetCell = GetCell(targetPosition);
            if (targetCell != null)
                targetCell.SetObject(gridObject);

            gridObject.SetGridPosition(targetPosition, GridToWorld(targetPosition));
            return true;
        }

        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            Vector3 worldPosition = new Vector3(gridPosition.x * cellSize, gridPosition.y * cellSize, 0f);

            if (centerStageOnOrigin && stageData != null)
                worldPosition -= GetStageCenterOffset();

            return originWorldPosition + worldPosition;
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            Vector3 localPosition = worldPosition - originWorldPosition;

            if (centerStageOnOrigin && stageData != null)
                localPosition += GetStageCenterOffset();

            int x = Mathf.RoundToInt(localPosition.x / cellSize);
            int y = Mathf.RoundToInt(localPosition.y / cellSize);

            return new Vector2Int(x, y);
        }

        public GridTarget GetTargetAt(Vector2Int position)
        {
            if (!targets.TryGetValue(position, out GridTarget target))
                return null;

            return target;
        }

        public void SetTargetActivated(Vector2Int position, bool activated)
        {
            GridTarget target = GetTargetAt(position);
            if (target != null)
                target.SetActivated(activated);
        }

        public void ResetAllTargets()
        {
            for (int i = 0; i < spawnedTargets.Count; i++)
            {
                if (spawnedTargets[i] != null)
                {
                    spawnedTargets[i].SetActivated(false);
                    spawnedTargets[i].SetFailed(false);
                }
            }
        }

        public void ResetAllDistanceSensors()
        {
            for (int i = 0; i < spawnedDistanceSensors.Count; i++)
            {
                if (spawnedDistanceSensors[i] != null)
                    spawnedDistanceSensors[i].SetActivated(false);
            }
        }

        public bool AreAllTargetsActivated()
        {
            if (spawnedTargets.Count <= 0)
                return false;

            for (int i = 0; i < spawnedTargets.Count; i++)
            {
                if (spawnedTargets[i] == null)
                    continue;

                if (!spawnedTargets[i].IsActivated)
                    return false;
            }

            return true;
        }

        public IReadOnlyDictionary<Vector2Int, GridCell> GetCells()
        {
            return cells;
        }

        public IReadOnlyDictionary<Vector2Int, GridObject> GetObjects()
        {
            return objects;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || stageData == null)
                return;

            for (int y = 0; y < stageData.height; y++)
            {
                for (int x = 0; x < stageData.width; x++)
                {
                    Vector2Int gridPosition = new Vector2Int(x, y);
                    Vector3 worldPosition = GridToWorld(gridPosition);

                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(worldPosition, Vector3.one * cellSize);
                }
            }

            for (int i = 0; i < stageData.wallPositions.Count; i++)
            {
                Vector3 worldPosition = GridToWorld(stageData.wallPositions[i]);
                Gizmos.color = Color.red;
                Gizmos.DrawCube(worldPosition, Vector3.one * cellSize * 0.85f);
            }

            for (int i = 0; i < stageData.targetPositions.Count; i++)
            {
                Vector3 worldPosition = GridToWorld(stageData.targetPositions[i]);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(worldPosition, cellSize * 0.35f);
            }

            if (stageData.advancedTargets != null)
            {
                for (int i = 0; i < stageData.advancedTargets.Count; i++)
                {
                    if (stageData.advancedTargets[i] == null)
                        continue;

                    Vector3 worldPosition = GridToWorld(stageData.advancedTargets[i].position);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(worldPosition, cellSize * 0.35f);
                }
            }

            if (stageData.distanceSensors != null)
            {
                for (int i = 0; i < stageData.distanceSensors.Count; i++)
                {
                    if (stageData.distanceSensors[i] == null)
                        continue;

                    Vector3 worldPosition = GridToWorld(stageData.distanceSensors[i].position);
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(worldPosition, stageData.distanceSensors[i].detectionRadius * cellSize);
                }
            }

            for (int i = 0; i < stageData.objects.Count; i++)
            {
                if (stageData.objects[i] == null)
                    continue;

                Vector3 worldPosition = GridToWorld(stageData.objects[i].position);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(worldPosition, cellSize * 0.4f);
            }
        }
    }
}
