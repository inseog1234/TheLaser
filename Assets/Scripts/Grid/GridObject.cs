using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
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

        [Header("Prism Filter")]
        public bool usePrismTypeFilter;
        public PrismType prismType;

        [Header("Lens Filter")]
        public bool useLensTypeFilter;
        public LensType lensType;

        [Header("Function Symbol")]
        public SpriteRenderer functionSymbolRenderer;
        public bool useFunctionSymbolColor;
        public bool hideFunctionSymbolWhenUnused = true;

        [Header("Lens Boost Text")]
        public TMP_Text lensBoostText;
        public bool hideLensBoostTextWhenUnused = true;

        [Header("Transform")]
        public bool overrideLocalScale;
        public Vector3 localScale = Vector3.one;
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

        [Header("Prism")]
        [SerializeField] private PrismType prismType = PrismType.Splitter;
        [SerializeField] private PrismSplitterMode splitterMode = PrismSplitterMode.ForwardLeftRight;
        [SerializeField] private LaserColorKind prismColor = LaserColorKind.Red;
        [SerializeField] private RefractionMode refractionMode = RefractionMode.Clockwise45;

        [Header("Lens")]
        [SerializeField] private LensType lensType = LensType.DistanceAmplifier;
        [SerializeField] private int distanceBoost = 5;

        [Header("Visual Entries")]
        [SerializeField] private List<GridObjectVisualEntry> visualEntries = new();

        public PuzzleObjectType ObjectType => objectType;
        public ManipulationType ManipulationType => manipulationType;
        public Vector2Int GridPosition => gridPosition;
        public GridDirection Direction => direction;
        public MirrorShape MirrorShape => mirrorShape;
        public PrismType PrismType => prismType;
        public PrismSplitterMode SplitterMode => splitterMode;
        public LaserColorKind PrismColor => prismColor;
        public RefractionMode RefractionMode => refractionMode;
        public LensType LensType => lensType;
        public int DistanceBoost => distanceBoost;

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

            prismType = data.prismType;
            splitterMode = data.splitterMode;
            prismColor = data.prismColor;
            refractionMode = data.refractionMode;

            lensType = data.lensType;
            distanceBoost = data.distanceBoost;

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

        public void SetMirrorShape(MirrorShape newShape)
        {
            mirrorShape = newShape;
            RefreshVisual();
        }

        public void ApplyTransformedState(Vector2Int newPosition, GridDirection newDirection, MirrorShape newMirrorShape, Vector3 worldPosition)
        {
            gridPosition = newPosition;
            direction = newDirection;
            mirrorShape = newMirrorShape;
            transform.position = worldPosition;
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

        public bool TryReflectLaser(LaserDirection laserMoveDirection, out LaserDirection reflectedDirection)
        {
            reflectedDirection = laserMoveDirection;

            if (objectType != PuzzleObjectType.Mirror)
                return false;

            if (!laserMoveDirection.TryToGridDirection(out GridDirection gridMoveDirection))
                return false;

            GridDirection entrySide = gridMoveDirection.Opposite();

            GetMirrorOpenSides(mirrorShape, direction, out GridDirection sideA, out GridDirection sideB);

            if (entrySide == sideA)
            {
                reflectedDirection = LaserDirectionExtensions.FromGridDirection(sideB);
                return true;
            }

            if (entrySide == sideB)
            {
                reflectedDirection = LaserDirectionExtensions.FromGridDirection(sideA);
                return true;
            }

            return false;
        }

        public void GetSplitterOutputDirections(LaserDirection inputDirection, List<LaserDirection> outputDirections)
        {
            outputDirections.Clear();

            if (objectType != PuzzleObjectType.Prism || prismType != PrismType.Splitter)
                return;

            LaserDirection left = inputDirection.RotateCounterClockwise90();
            LaserDirection right = inputDirection.RotateClockwise90();

            switch (splitterMode)
            {
                case PrismSplitterMode.ForwardAndLeft:
                    outputDirections.Add(inputDirection);
                    outputDirections.Add(left);
                    break;

                case PrismSplitterMode.ForwardAndRight:
                    outputDirections.Add(inputDirection);
                    outputDirections.Add(right);
                    break;

                case PrismSplitterMode.ForwardLeftRight:
                    outputDirections.Add(inputDirection);
                    outputDirections.Add(left);
                    outputDirections.Add(right);
                    break;

                case PrismSplitterMode.LeftAndRight:
                    outputDirections.Add(left);
                    outputDirections.Add(right);
                    break;
            }
        }

        public LaserColorKind ApplyColorPrism(LaserColorKind currentColor)
        {
            if (objectType != PuzzleObjectType.Prism || prismType != PrismType.Color)
                return currentColor;

            return prismColor;
        }

        public LaserDirection ApplyRefractionPrism(LaserDirection inputDirection)
        {
            if (objectType != PuzzleObjectType.Prism || prismType != PrismType.Refraction)
                return inputDirection;

            return refractionMode == RefractionMode.Clockwise45
                ? inputDirection.RotateClockwise45()
                : inputDirection.RotateCounterClockwise45();
        }

        private static void GetMirrorOpenSides(MirrorShape shape, GridDirection mirrorDirection, out GridDirection sideA, out GridDirection sideB)
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

        private static GridDirection RotateSideByMirrorDirection(GridDirection side, GridDirection mirrorDirection)
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
                result = result.RotateClockwise();

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
                localEuler.y += 180f;

            visualTransform.localRotation = Quaternion.Euler(localEuler);

            if (entry.overrideLocalScale)
                visualTransform.localScale = entry.localScale;

            RefreshFunctionSymbol(entry);
        }

        private void RefreshFunctionSymbol(GridObjectVisualEntry entry)
        {
            if (entry == null)
                return;

            RefreshColorPrismSymbol(entry);
            RefreshLensBoostText(entry);
        }

        private void RefreshColorPrismSymbol(GridObjectVisualEntry entry)
        {
            if (entry.functionSymbolRenderer == null)
                return;

            bool shouldShowSymbol = objectType == PuzzleObjectType.Prism &&
                                    prismType == PrismType.Color &&
                                    entry.useFunctionSymbolColor;

            if (entry.hideFunctionSymbolWhenUnused)
                entry.functionSymbolRenderer.gameObject.SetActive(shouldShowSymbol);
            else
                entry.functionSymbolRenderer.gameObject.SetActive(true);

            if (!shouldShowSymbol)
                return;

            entry.functionSymbolRenderer.color = GetLaserColor(prismColor);
        }

        private void RefreshLensBoostText(GridObjectVisualEntry entry)
        {
            if (entry.lensBoostText == null)
                return;

            bool shouldShowText = objectType == PuzzleObjectType.Lens &&
                                  lensType == LensType.DistanceAmplifier;

            if (entry.hideLensBoostTextWhenUnused)
                entry.lensBoostText.gameObject.SetActive(shouldShowText);
            else
                entry.lensBoostText.gameObject.SetActive(true);

            if (!shouldShowText)
                return;

            entry.lensBoostText.richText = false;
            entry.lensBoostText.enableWordWrapping = false;
            entry.lensBoostText.text = $"+{Mathf.Max(0, distanceBoost)}칸";
        }

        private Color GetLaserColor(LaserColorKind colorKind)
        {
            return colorKind switch
            {
                LaserColorKind.Default => Color.white,
                LaserColorKind.Red => new Color(1f, 0.15f, 0.15f, 1f),
                LaserColorKind.Blue => new Color(0.2f, 0.45f, 1f, 1f),
                LaserColorKind.Green => new Color(0.2f, 1f, 0.35f, 1f),
                LaserColorKind.Yellow => new Color(1f, 0.9f, 0.15f, 1f),
                LaserColorKind.Purple => new Color(0.75f, 0.25f, 1f, 1f),
                LaserColorKind.White => Color.white,
                _ => Color.white
            };
        }

        private void ApplyRootRotation()
        {
            if (objectType == PuzzleObjectType.Mirror ||
                objectType == PuzzleObjectType.Prism ||
                objectType == PuzzleObjectType.Lens)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());
                return;
            }

            transform.rotation = Quaternion.identity;
        }

        private GridObjectVisualEntry FindBestVisualEntry()
        {
            GridObjectVisualEntry best = null;
            int bestScore = -1;

            for (int i = 0; i < visualEntries.Count; i++)
            {
                GridObjectVisualEntry entry = visualEntries[i];

                if (entry == null || entry.visualObject == null)
                    continue;

                if (entry.objectType != objectType)
                    continue;

                if (entry.useManipulationFilter && entry.manipulationType != manipulationType)
                    continue;

                if (entry.useMirrorShapeFilter && entry.mirrorShape != mirrorShape)
                    continue;

                if (entry.usePrismTypeFilter && entry.prismType != prismType)
                    continue;

                if (entry.useLensTypeFilter && entry.lensType != lensType)
                    continue;

                int score = 0;

                if (entry.useManipulationFilter)
                    score += 10;

                if (entry.useMirrorShapeFilter)
                    score += 10;

                if (entry.usePrismTypeFilter)
                    score += 10;

                if (entry.useLensTypeFilter)
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }

            return best;
        }

        private void DisableAllVisuals()
        {
            for (int i = 0; i < visualEntries.Count; i++)
            {
                GridObjectVisualEntry entry = visualEntries[i];

                if (entry == null)
                    continue;

                if (entry.functionSymbolRenderer != null && entry.hideFunctionSymbolWhenUnused)
                    entry.functionSymbolRenderer.gameObject.SetActive(false);

                if (entry.lensBoostText != null && entry.hideLensBoostTextWhenUnused)
                    entry.lensBoostText.gameObject.SetActive(false);

                if (entry.visualObject != null)
                    entry.visualObject.SetActive(false);
            }
        }
    }
}