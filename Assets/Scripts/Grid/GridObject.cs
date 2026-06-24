using System;
using System.Collections.Generic;
using UnityEngine;
using Core;

namespace Grid
{
    [Serializable]
    public class GridObjectVisualEntry
    {
        [Header("Target")]
        public GameObject visualObject;

        [Header("Object Condition")]
        public PuzzleObjectType objectType;

        [Header("Manipulation Filter")]
        public bool useManipulationFilter;
        public ManipulationType manipulationType;

        [Header("Mirror Shape Filter")]
        public bool useMirrorShapeFilter;
        public MirrorShape mirrorShape;

        public Vector3 localRotationOffset;
    }

    public class GridObject : MonoBehaviour
    {
        [Header("Object Info")]
        [SerializeField] private PuzzleObjectType objectType = PuzzleObjectType.Mirror;
        [SerializeField] private ManipulationType manipulationType = ManipulationType.None;

        [Header("Grid")]
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private GridDirection direction = GridDirection.Up;

        [Header("Mirror")]
        [SerializeField] private MirrorShape mirrorShape = MirrorShape.NormalL;

        [Header("Visual Entries")]
        [SerializeField] private List<GridObjectVisualEntry> visualEntries = new();

        public PuzzleObjectType ObjectType => objectType;
        public ManipulationType ManipulationType => manipulationType;
        public Vector2Int GridPosition => gridPosition;
        public GridDirection Direction => direction;
        public MirrorShape MirrorShape => mirrorShape;

        public bool CanPush => manipulationType.CanPush();
        public bool CanRotate => manipulationType.CanRotate();

        private void OnValidate()
        {
            RefreshVisual();
        }

        public void Initialize(StageObjectData data, Vector3 worldPosition)
        {
            objectType = data.objectType;
            manipulationType = data.manipulationType;
            gridPosition = data.position;
            direction = data.direction;
            mirrorShape = data.mirrorShape;

            transform.position = worldPosition;

            RefreshVisual();
        }

        public void SetGridPosition(Vector2Int newPosition, Vector3 worldPosition)
        {
            gridPosition = newPosition;
            transform.position = worldPosition;
        }

        public void SetDirection(GridDirection newDirection)
        {
            direction = newDirection;
            RefreshVisual();
        }

        public void RotateClockwise()
        {
            if (!CanRotate)
                return;

            direction = direction.RotateClockwise();
            RefreshVisual();
        }

        public void RotateCounterClockwise()
        {
            if (!CanRotate)
                return;

            direction = direction.RotateCounterClockwise();
            RefreshVisual();
        }

        public bool TryReflectLaser(GridDirection laserMoveDirection, out GridDirection reflectedDirection)
        {
            reflectedDirection = laserMoveDirection;

            if (objectType != PuzzleObjectType.Mirror)
                return false;

            GridDirection entrySide = laserMoveDirection.Opposite();

            GetMirrorOpenSides(mirrorShape, direction, out GridDirection sideA, out GridDirection sideB);

            if (entrySide == sideA)
            {
                reflectedDirection = sideB;
                return true;
            }

            if (entrySide == sideB)
            {
                reflectedDirection = sideA;
                return true;
            }

            return false;
        }

        private static void GetMirrorOpenSides(
            MirrorShape shape,
            GridDirection mirrorDirection,
            out GridDirection sideA,
            out GridDirection sideB)
        {
            if (shape == MirrorShape.NormalL)
            {
                sideA = GridDirection.Up;
                sideB = GridDirection.Right;
            }
            else
            {
                sideA = GridDirection.Up;
                sideB = GridDirection.Left;
            }

            sideA = RotateSideByMirrorDirection(sideA, mirrorDirection);
            sideB = RotateSideByMirrorDirection(sideB, mirrorDirection);
        }

        private static GridDirection RotateSideByMirrorDirection(
            GridDirection side,
            GridDirection mirrorDirection)
        {
            int rotateCount = mirrorDirection switch
            {
                GridDirection.Up => 0,
                GridDirection.Right => 1,
                GridDirection.Down => 2,
                GridDirection.Left => 3,
                _ => 0
            };

            GridDirection result = side;

            for (int i = 0; i < rotateCount; i++)
            {
                result = result.RotateClockwise();
            }

            return result;
        }

        private void RefreshVisual()
        {
            ApplyRootRotation();

            DisableAllVisuals();

            GridObjectVisualEntry entry = FindBestVisualEntry();

            if (entry == null || entry.visualObject == null)
                return;

            entry.visualObject.SetActive(true);

            Transform visualTransform = entry.visualObject.transform;

            Vector3 localEuler = entry.localRotationOffset;

            if (objectType == PuzzleObjectType.Mirror && mirrorShape == MirrorShape.ReverseL && !entry.useMirrorShapeFilter)
            {
                localEuler.y += 180f;
            }

            visualTransform.localRotation = Quaternion.Euler(localEuler);
        }

        private void ApplyRootRotation()
        {
            if (objectType == PuzzleObjectType.Mirror || objectType == PuzzleObjectType.Prism)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());
                return;
            }

            transform.rotation = Quaternion.identity;
        }

        private GridObjectVisualEntry FindBestVisualEntry()
        {
            GridObjectVisualEntry fallback = null;
            int bestScore = -1;

            for (int i = 0; i < visualEntries.Count; i++)
            {
                GridObjectVisualEntry entry = visualEntries[i];

                if (entry == null || entry.visualObject == null)
                    continue;

                if (entry.objectType != objectType)
                    continue;

                if (entry.useManipulationFilter &&
                    entry.manipulationType != manipulationType)
                    continue;

                if (entry.useMirrorShapeFilter &&
                    entry.mirrorShape != mirrorShape)
                    continue;

                int score = 0;

                if (entry.useManipulationFilter)
                    score += 10;

                if (entry.useMirrorShapeFilter)
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    fallback = entry;
                }
            }

            return fallback;
        }

        private void DisableAllVisuals()
        {
            for (int i = 0; i < visualEntries.Count; i++)
            {
                if (visualEntries[i] == null)
                    continue;

                if (visualEntries[i].visualObject != null)
                    visualEntries[i].visualObject.SetActive(false);
            }
        }
    }
}