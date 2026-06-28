using System;
using System.Collections.Generic;
using UnityEngine;
using Grid;
using Laser;
using Player;

namespace Core
{
    public class StageTurnHistoryController : MonoBehaviour
    {
        private class GridObjectTurnState
        {
            public GridObject GridObject;
            public Vector2Int Position;
            public GridDirection Direction;
            public MirrorShape MirrorShape;
        }

        private class StageTurnSnapshot
        {
            public Vector2Int PlayerPosition;
            public GridDirection PlayerDirection;
            public readonly List<GridObjectTurnState> ObjectStates = new();
        }

        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PlayerGridController playerGridController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private LaserRenderer laserRenderer;

        [Header("Option")]
        [SerializeField] private int maxTurnCount = 20;

        private readonly List<StageTurnSnapshot> undoStack = new();
        private readonly List<StageTurnSnapshot> redoStack = new();

        private StageTurnSnapshot pendingSnapshot;
        private bool hasPendingSnapshot;
        private bool isApplyingHistory;
        private bool turnCountingEnabled = true;

        public event Action TurnCountChanged;

        public bool IsApplyingHistory => isApplyingHistory;
        public bool IsTurnCountingEnabled => turnCountingEnabled;
        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;
        public int TurnCount => undoStack.Count;
        public int CurrentMoveLimit => gridManager != null && gridManager.CurrentStageData != null ? Mathf.Max(0, gridManager.CurrentStageData.moveLimit) : 0;
        public int RemainingTurnCount => CurrentMoveLimit <= 0 ? 0 : Mathf.Max(0, CurrentMoveLimit - TurnCount);
        public bool HasMoveLimit => turnCountingEnabled && CurrentMoveLimit > 0;
        public bool IsMoveLimitReached => HasMoveLimit && RemainingTurnCount <= 0;

        private void OnEnable()
        {
            if (inputReader == null)
                return;

            inputReader.UndoPressed += UndoTurn;
            inputReader.RedoPressed += RedoTurn;
        }

        private void OnDisable()
        {
            if (inputReader == null)
                return;

            inputReader.UndoPressed -= UndoTurn;
            inputReader.RedoPressed -= RedoTurn;
        }

        public void SetTurnCountingEnabled(bool enabled)
        {
            turnCountingEnabled = enabled;

            if (!turnCountingEnabled)
                CancelTurn();

            TurnCountChanged?.Invoke();
        }

        public void BeginTurn()
        {
            if (!turnCountingEnabled)
                return;

            if (isApplyingHistory)
                return;

            if (hasPendingSnapshot)
                return;

            pendingSnapshot = CaptureSnapshot();
            hasPendingSnapshot = true;
        }

        public void CommitTurn()
        {
            if (!turnCountingEnabled)
            {
                CancelTurn();
                return;
            }

            if (isApplyingHistory)
                return;

            if (!hasPendingSnapshot)
                return;

            StageTurnSnapshot currentSnapshot = CaptureSnapshot();
            bool turnChanged = false;

            if (!AreSameSnapshot(pendingSnapshot, currentSnapshot))
            {
                undoStack.Add(pendingSnapshot);
                TrimUndoStack();
                redoStack.Clear();
                turnChanged = true;
            }

            pendingSnapshot = null;
            hasPendingSnapshot = false;

            if (turnChanged)
                TurnCountChanged?.Invoke();
        }

        public void CancelTurn()
        {
            pendingSnapshot = null;
            hasPendingSnapshot = false;
        }

        public void UndoTurn()
        {
            if (isApplyingHistory)
                return;

            if (playerGridController != null && playerGridController.IsMoving)
                return;

            if (undoStack.Count <= 0)
                return;

            StageTurnSnapshot currentSnapshot = CaptureSnapshot();
            StageTurnSnapshot targetSnapshot = undoStack[undoStack.Count - 1];

            undoStack.RemoveAt(undoStack.Count - 1);
            redoStack.Add(currentSnapshot);

            ApplySnapshot(targetSnapshot);
            TurnCountChanged?.Invoke();
        }

        public void RedoTurn()
        {
            if (isApplyingHistory)
                return;

            if (playerGridController != null && playerGridController.IsMoving)
                return;

            if (redoStack.Count <= 0)
                return;

            StageTurnSnapshot currentSnapshot = CaptureSnapshot();
            StageTurnSnapshot targetSnapshot = redoStack[redoStack.Count - 1];

            redoStack.RemoveAt(redoStack.Count - 1);
            undoStack.Add(currentSnapshot);
            TrimUndoStack();

            ApplySnapshot(targetSnapshot);
            TurnCountChanged?.Invoke();
        }

        public void ClearHistory()
        {
            undoStack.Clear();
            redoStack.Clear();
            pendingSnapshot = null;
            hasPendingSnapshot = false;
            TurnCountChanged?.Invoke();
        }

        private StageTurnSnapshot CaptureSnapshot()
        {
            StageTurnSnapshot snapshot = new StageTurnSnapshot();

            if (playerGridController != null)
            {
                snapshot.PlayerPosition = playerGridController.GridPosition;
                snapshot.PlayerDirection = playerGridController.FacingDirection;
            }

            if (gridManager != null)
            {
                foreach (KeyValuePair<Vector2Int, GridObject> pair in gridManager.GetObjects())
                {
                    GridObject gridObject = pair.Value;

                    if (gridObject == null)
                        continue;

                    GridObjectTurnState state = new GridObjectTurnState
                    {
                        GridObject = gridObject,
                        Position = gridObject.GridPosition,
                        Direction = gridObject.Direction,
                        MirrorShape = gridObject.MirrorShape
                    };

                    snapshot.ObjectStates.Add(state);
                }
            }

            return snapshot;
        }

        private void ApplySnapshot(StageTurnSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            isApplyingHistory = true;

            if (laserRenderer != null)
                laserRenderer.Clear();

            if (gridManager != null)
            {
                gridManager.ResetAllTargets();
                gridManager.ResetAllDistanceSensors();
                ApplyObjectStates(snapshot);
            }

            if (playerGridController != null)
                playerGridController.ApplyRuntimeStateImmediate(snapshot.PlayerPosition, snapshot.PlayerDirection);

            isApplyingHistory = false;
        }

        private void ApplyObjectStates(StageTurnSnapshot snapshot)
        {
            if (gridManager == null || snapshot == null)
                return;

            List<GridObject> currentObjects = new();

            foreach (KeyValuePair<Vector2Int, GridObject> pair in gridManager.GetObjects())
            {
                if (pair.Value != null)
                    currentObjects.Add(pair.Value);
            }

            for (int i = 0; i < currentObjects.Count; i++)
                gridManager.UnregisterObject(currentObjects[i]);

            for (int i = 0; i < snapshot.ObjectStates.Count; i++)
            {
                GridObjectTurnState state = snapshot.ObjectStates[i];

                if (state == null || state.GridObject == null)
                    continue;

                Vector3 worldPosition = gridManager.GridToWorld(state.Position);
                state.GridObject.ApplyTransformedState(state.Position, state.Direction, state.MirrorShape, worldPosition);
                gridManager.RegisterObject(state.GridObject);
            }
        }

        private bool AreSameSnapshot(StageTurnSnapshot a, StageTurnSnapshot b)
        {
            if (a == null || b == null)
                return false;

            if (a.PlayerPosition != b.PlayerPosition)
                return false;

            if (a.PlayerDirection != b.PlayerDirection)
                return false;

            if (a.ObjectStates.Count != b.ObjectStates.Count)
                return false;

            for (int i = 0; i < a.ObjectStates.Count; i++)
            {
                GridObjectTurnState stateA = a.ObjectStates[i];
                GridObjectTurnState stateB = FindObjectState(b, stateA.GridObject);

                if (stateB == null)
                    return false;

                if (stateA.Position != stateB.Position)
                    return false;

                if (stateA.Direction != stateB.Direction)
                    return false;

                if (stateA.MirrorShape != stateB.MirrorShape)
                    return false;
            }

            return true;
        }

        private GridObjectTurnState FindObjectState(StageTurnSnapshot snapshot, GridObject gridObject)
        {
            if (snapshot == null || gridObject == null)
                return null;

            for (int i = 0; i < snapshot.ObjectStates.Count; i++)
            {
                if (snapshot.ObjectStates[i] != null && snapshot.ObjectStates[i].GridObject == gridObject)
                    return snapshot.ObjectStates[i];
            }

            return null;
        }

        private void TrimUndoStack()
        {
            int finalMaxTurnCount = Mathf.Max(maxTurnCount, CurrentMoveLimit);

            while (undoStack.Count > finalMaxTurnCount)
                undoStack.RemoveAt(0);
        }
    }
}