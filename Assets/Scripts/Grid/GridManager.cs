using System.Collections.Generic;
using UnityEngine;
using Core;
using UnityEditor.SceneManagement;

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

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool showDebugLog = true;

        private readonly Dictionary<Vector2Int, GridCell> cells = new();
        private readonly Dictionary<Vector2Int, GridObject> objects = new();
        private readonly List<GridObject> spawnedObjects = new();

        public StageData CurrentStageData => stageData;
        public int Width => stageData != null ? stageData.width : 0;
        public int Height => stageData != null ? stageData.height : 0;
        public float CellSize => cellSize;

        private void Awake()
        {
            if (loadOnAwake && stageData != null)
            {
                LoadStage(stageData);
            }
        }

        public void LoadStage(StageData targetStageData)
        {
            if (targetStageData == null)
            {
                return;
            }

            stageData = targetStageData;

            ClearRuntimeGrid();
            CreateCells();
            ApplyStageCells();
            SpawnStageObjects();
        }

        private void ClearRuntimeGrid()
        {
            cells.Clear();
            objects.Clear();

            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (spawnedObjects[i] != null)
                {
                    Destroy(spawnedObjects[i].gameObject);
                }
            }

            spawnedObjects.Clear();
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
                {
                    continue;
                }

                cells[wallPosition].SetCellType(CellType.Wall);
            }

            for (int i = 0; i < stageData.targetPositions.Count; i++)
            {
                Vector2Int targetPosition = stageData.targetPositions[i];

                if (!IsInside(targetPosition) && cells[targetPosition].IsWall)
                {
                    continue;
                }

                cells[targetPosition].SetCellType(CellType.Target);
            }
        }

        private void SpawnStageObjects()
        {
            if (objectPrefab == null)
            {
                return;
            }

            if (objectParent == null)
            {
                objectParent = transform;
            }

            for (int i = 0; i < stageData.objects.Count; i++)
            {
                StageObjectData objectData = stageData.objects[i];

                if (!IsInside(objectData.position) && HasWall(objectData.position) && HasObject(objectData.position))
                {
                    continue;
                }

                Vector3 worldPosition = GridToWorld(objectData.position);
                GridObject spawnedObject = Instantiate(objectPrefab, worldPosition, Quaternion.identity, objectParent);

                spawnedObject.name = $"{objectData.objectType}_{objectData.manipulationType}_{objectData.position}";
                spawnedObject.Initialize(objectData, worldPosition);

                RegisterObject(spawnedObject);

                spawnedObjects.Add(spawnedObject);
            }
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

            if (!IsInside(position) && HasObject(position))
            {
                return;
            }

            objects.Add(position, gridObject);

            GridCell cell = GetCell(position);

            if (cell != null)
            {
                cell.SetObject(gridObject);
            }
        }

        public void UnregisterObject(GridObject gridObject)
        {
            if (gridObject == null)
                return;

            Vector2Int position = gridObject.GridPosition;

            if (objects.ContainsKey(position))
            {
                objects.Remove(position);
            }

            GridCell cell = GetCell(position);

            if (cell != null)
            {
                cell.ClearObject();
            }
        }

        public bool TryMoveObject(GridObject gridObject, Vector2Int targetPosition)
        {
            if (gridObject == null)
                return false;

            if (!IsEmpty(targetPosition))
                return false;

            Vector2Int currentPosition = gridObject.GridPosition;

            if (objects.ContainsKey(currentPosition))
            {
                objects.Remove(currentPosition);
            }

            GridCell currentCell = GetCell(currentPosition);

            if (currentCell != null)
            {
                currentCell.ClearObject();
            }

            objects.Add(targetPosition, gridObject);

            GridCell targetCell = GetCell(targetPosition);

            if (targetCell != null)
            {
                targetCell.SetObject(gridObject);
            }

            gridObject.SetGridPosition(targetPosition, GridToWorld(targetPosition));

            return true;
        }

        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            Vector3 worldPosition = new Vector3(gridPosition.x * cellSize, gridPosition.y * cellSize, 0f);

            if (centerStageOnOrigin && stageData != null)
            {
                worldPosition -= GetStageCenterOffset();
            }

            return originWorldPosition + worldPosition;
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            Vector3 localPosition = worldPosition - originWorldPosition;

            if (centerStageOnOrigin && stageData != null)
            {
                localPosition += GetStageCenterOffset();
            }

            int x = Mathf.RoundToInt(localPosition.x / cellSize);
            int y = Mathf.RoundToInt(localPosition.y / cellSize);

            return new Vector2Int(x, y);
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

            for (int i = 0; i < stageData.objects.Count; i++)
            {
                Vector3 worldPosition = GridToWorld(stageData.objects[i].position);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(worldPosition, cellSize * 0.4f);
            }
        }
    }
}
