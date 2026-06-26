using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Core;

namespace Laser
{
    public enum LaserVisualMatchMode
    {
        Any,
        Direction,
        CornerSides
    }

    [Serializable]
    public class LaserEndMoveTarget
    {
        public Transform target;

        [Header("Move Option")]
        public Vector3 localMoveDirection = Vector3.up;
        public float moveMultiplier = 0.5f;
    }

    [Serializable]
    public class LaserVisualEntry
    {
        [Header("Target")]
        public GameObject visualObject;

        [Header("Condition")]
        public LaserPathNodeType nodeType;
        public LaserVisualMatchMode matchMode = LaserVisualMatchMode.Any;

        [Header("Direction Condition")]
        public List<LaserDirection> directions = new();

        [Header("Corner Condition")]
        public LaserDirection cornerSideA = LaserDirection.Up;
        public LaserDirection cornerSideB = LaserDirection.Right;

        [Header("Corner Angle Filter")]
        public bool useCornerAngleFilter = false;
        public LaserCornerAngle cornerAngle = LaserCornerAngle.None;

        [Header("Rotation")]
        public bool autoRotateVisual = false;
        public Vector3 rotationOffset;

        [Header("Color")]
        public bool applyLaserColorToMainVisual = true;
        public GameObject colorSymbolObject;
        public bool hideColorSymbolWhenDefault = false;

        [Header("End Scale Targets")]
        public List<Transform> endScaleTargets = new();
        public List<LaserEndMoveTarget> endMoveTargets = new();
        
    }

    public class LaserPathTile : MonoBehaviour
    {
        [Header("Visual Entries")]
        [SerializeField] private List<LaserVisualEntry> visualEntries = new();

        [Header("Color")]
        [SerializeField] private Color defaultColor = Color.white;

        [Header("Diagonal Straight Scale")]
        [SerializeField] private float diagonalStraightYScaleMultiplier = 1.5f;
        [SerializeField] private float endYScaleMultiplier = 1.6f;

        private readonly Dictionary<GameObject, Vector3> initialLocalScales = new();
        private readonly Dictionary<GameObject, Quaternion> initialLocalRotations = new();
        private readonly Dictionary<Transform, Vector3> initialTransformScales = new();
        private readonly Dictionary<Transform, Vector3> initialTransformPositions = new();

        private void Awake()
        {
            CacheInitialTransforms();
        }

        private void OnValidate()
        {
            CacheInitialTransforms();
        }

        public void SetNode(LaserPathNode node)
        {
            SetNode(node, defaultColor);
        }

        public void SetNode(LaserPathNode node, Color laserColor)
        {
            if (initialLocalRotations.Count <= 0 || initialLocalScales.Count <= 0)
                CacheInitialTransforms();

            DisableAllVisuals();

            LaserVisualEntry entry = FindBestVisual(node);

            if (entry == null || entry.visualObject == null)
                return;

            entry.visualObject.SetActive(true);

            if (entry.autoRotateVisual)
            {
                float angle = GetRotationAngle(node);
                Vector3 flipEuler = GetCornerFlipEuler(node);
                Quaternion baseRotation = GetInitialLocalRotation(entry.visualObject);
                entry.visualObject.transform.localRotation = baseRotation * Quaternion.Euler(entry.rotationOffset + flipEuler + new Vector3(0f, 0f, angle));
            }

            ApplyDiagonalStraightScale(entry, node);
            
            ApplyLaserColor(entry, node, laserColor);
        }

        public void ResetTile()
        {
            DisableAllVisuals();
        }

        private void ApplyLaserColor(LaserVisualEntry entry, LaserPathNode node, Color laserColor)
        {
            if (entry == null)
                return;

            if (entry.visualObject != null && entry.applyLaserColorToMainVisual)
                ApplyColor(entry.visualObject, laserColor);

            if (entry.colorSymbolObject == null)
                return;

            bool showSymbol = !(entry.hideColorSymbolWhenDefault && node.Color == LaserColorKind.Default);
            entry.colorSymbolObject.SetActive(showSymbol);

            if (showSymbol)
                ApplyColor(entry.colorSymbolObject, laserColor);
        }

        private void ApplyDiagonalStraightScale(LaserVisualEntry entry, LaserPathNode node)
        {
            if (entry == null || entry.visualObject == null)
                return;

            Vector3 baseScale = GetInitialLocalScale(entry.visualObject);
            entry.visualObject.transform.localScale = baseScale;

            if (node.NodeType == LaserPathNodeType.Straight)
            {
                LaserDirection direction = GetMainDirection(node);

                if (IsDiagonalDirection(direction))
                {
                    Vector3 scaled = baseScale;
                    scaled.y *= diagonalStraightYScaleMultiplier;
                    entry.visualObject.transform.localScale = scaled;
                }
            }

            if (node.NodeType == LaserPathNodeType.End)
            {
                ApplyEndScaleTargets(entry);
                ApplyEndMoveTargets(entry);
            }
        }


        private void ApplyEndMoveTargets(LaserVisualEntry entry)
        {
            float extraLength = GetEndExtraLength(entry);

            for (int i = 0; i < entry.endMoveTargets.Count; i++)
            {
                LaserEndMoveTarget moveTarget = entry.endMoveTargets[i];

                if (moveTarget == null || moveTarget.target == null)
                    continue;

                Vector3 basePosition = GetInitialTransformPosition(moveTarget.target);
                Vector3 moveDirection = moveTarget.localMoveDirection;

                if (moveDirection.sqrMagnitude <= 0.0001f)
                    moveDirection = Vector3.up;

                moveDirection.Normalize();

                moveTarget.target.localPosition = basePosition + moveDirection * extraLength * moveTarget.moveMultiplier;
            }
        }

        private float GetEndExtraLength(LaserVisualEntry entry)
        {
            float extraLength = 0f;

            for (int i = 0; i < entry.endScaleTargets.Count; i++)
            {
                Transform target = entry.endScaleTargets[i];

                if (target == null)
                    continue;

                Vector3 baseScale = GetInitialTransformScale(target);
                float before = Mathf.Abs(baseScale.y);
                float after = Mathf.Abs(baseScale.y * endYScaleMultiplier);
                float extra = Mathf.Abs(after - before);

                if (extra > extraLength)
                    extraLength = extra;
            }

            return extraLength;
        }

        private Vector3 GetInitialTransformScale(Transform target)
        {
            if (target != null && initialTransformScales.TryGetValue(target, out Vector3 scale))
                return scale;

            return target != null ? target.localScale : Vector3.one;
        }

        private Vector3 GetInitialTransformPosition(Transform target)
        {
            if (target != null && initialTransformPositions.TryGetValue(target, out Vector3 position))
                return position;

            return target != null ? target.localPosition : Vector3.zero;
        }

        private void ApplyEndScaleTargets(LaserVisualEntry entry)
        {
            for (int i = 0; i < entry.endScaleTargets.Count; i++)
            {
                Transform target = entry.endScaleTargets[i];

                if (target == null)
                    continue;

                Vector3 baseScale = GetInitialTransformScale(target);
                Vector3 scaled = baseScale;
                scaled.y *= endYScaleMultiplier;
                target.localScale = scaled;
            }
        }

        private Vector3 GetCornerFlipEuler(LaserPathNode node)
        {
            if (node.NodeType != LaserPathNodeType.Corner && node.NodeType != LaserPathNodeType.CornerEnd)
                return Vector3.zero;

            if (node.CornerAngle != LaserCornerAngle.Turn45)
                return Vector3.zero;

            if (!node.HasIncomingDirection || !node.HasOutgoingDirection)
                return Vector3.zero;

            if (!IsClockwise45Turn(node.IncomingDirection, node.OutgoingDirection))
                return Vector3.zero;

            return new Vector3(180f, 180f, 0f);
        }

        private bool IsClockwise45Turn(LaserDirection incomingDirection, LaserDirection outgoingDirection)
        {
            int incomingIndex = ToDirectionIndex(incomingDirection);
            int outgoingIndex = ToDirectionIndex(outgoingDirection);
            int diff = outgoingIndex - incomingIndex;

            if (diff < 0)
                diff += 8;

            return diff == 1;
        }

        private int ToDirectionIndex(LaserDirection direction)
        {
            return direction switch
            {
                LaserDirection.Up => 0,
                LaserDirection.UpRight => 1,
                LaserDirection.Right => 2,
                LaserDirection.DownRight => 3,
                LaserDirection.Down => 4,
                LaserDirection.DownLeft => 5,
                LaserDirection.Left => 6,
                LaserDirection.UpLeft => 7,
                _ => 0
            };
        }

        private void CacheInitialTransforms()
        {
            initialLocalRotations.Clear();
            initialLocalScales.Clear();
            initialTransformScales.Clear();
            initialTransformPositions.Clear();

            for (int i = 0; i < visualEntries.Count; i++)
            {
                LaserVisualEntry entry = visualEntries[i];

                if (entry == null)
                    continue;

                CacheInitialTransform(entry.visualObject);
                CacheInitialTransform(entry.colorSymbolObject);

                for (int j = 0; j < entry.endScaleTargets.Count; j++)
                    CacheInitialTransform(entry.endScaleTargets[j]);

                for (int j = 0; j < entry.endMoveTargets.Count; j++)
                {
                    if (entry.endMoveTargets[j] == null)
                        continue;

                    CacheInitialTransform(entry.endMoveTargets[j].target);
                }
            }
        }

        private void CacheInitialTransform(Transform target)
        {
            if (target == null)
                return;

            if (!initialTransformScales.ContainsKey(target))
                initialTransformScales.Add(target, target.localScale);

            if (!initialTransformPositions.ContainsKey(target))
                initialTransformPositions.Add(target, target.localPosition);
        }

        private void CacheInitialTransform(GameObject targetObject)
        {
            if (targetObject == null)
                return;

            if (!initialLocalRotations.ContainsKey(targetObject))
                initialLocalRotations.Add(targetObject, targetObject.transform.localRotation);

            if (!initialLocalScales.ContainsKey(targetObject))
                initialLocalScales.Add(targetObject, targetObject.transform.localScale);
        }

        private Quaternion GetInitialLocalRotation(GameObject visualObject)
        {
            if (visualObject != null && initialLocalRotations.TryGetValue(visualObject, out Quaternion rotation))
                return rotation;

            return Quaternion.identity;
        }

        private Vector3 GetInitialLocalScale(GameObject visualObject)
        {
            if (visualObject != null && initialLocalScales.TryGetValue(visualObject, out Vector3 scale))
                return scale;

            return Vector3.one;
        }

        private void ApplyColor(GameObject targetObject, Color color)
        {
            if (targetObject == null)
                return;

            SpriteRenderer[] spriteRenderers = targetObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
                spriteRenderers[i].color = color;

            TMP_Text[] texts = targetObject.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].color = color;
        }

        private LaserVisualEntry FindBestVisual(LaserPathNode node)
        {
            LaserVisualEntry fallback = null;

            for (int i = 0; i < visualEntries.Count; i++)
            {
                LaserVisualEntry entry = visualEntries[i];

                if (entry == null || entry.visualObject == null)
                    continue;

                if (entry.nodeType != node.NodeType)
                    continue;

                if (!IsMatched(entry, node))
                    continue;

                if (entry.useCornerAngleFilter)
                    return entry;

                if (entry.matchMode == LaserVisualMatchMode.Direction)
                    return entry;

                if (entry.matchMode == LaserVisualMatchMode.CornerSides)
                    return entry;

                if (fallback == null)
                    fallback = entry;
            }

            return fallback;
        }

        private bool IsMatched(LaserVisualEntry entry, LaserPathNode node)
        {
            if (!IsCornerAngleMatched(entry, node))
                return false;

            switch (entry.matchMode)
            {
                case LaserVisualMatchMode.Any:
                    return true;

                case LaserVisualMatchMode.Direction:
                    LaserDirection mainDirection = GetMainDirection(node);
                    return entry.directions.Contains(mainDirection);

                case LaserVisualMatchMode.CornerSides:
                    if (node.NodeType != LaserPathNodeType.Corner && node.NodeType != LaserPathNodeType.CornerEnd)
                        return false;

                    if (!node.HasIncomingDirection || !node.HasOutgoingDirection)
                        return false;

                    LaserDirection directionA = node.IncomingDirection;
                    LaserDirection directionB = node.OutgoingDirection;

                    return IsSamePair(directionA, directionB, entry.cornerSideA, entry.cornerSideB);

                default:
                    return true;
            }
        }

        private bool IsCornerAngleMatched(LaserVisualEntry entry, LaserPathNode node)
        {
            if (!entry.useCornerAngleFilter)
                return true;

            return node.CornerAngle == entry.cornerAngle;
        }

        private LaserDirection GetMainDirection(LaserPathNode node)
        {
            if (node.HasOutgoingDirection)
                return node.OutgoingDirection;

            if (node.HasIncomingDirection)
                return node.IncomingDirection;

            return LaserDirection.Up;
        }

        private void DisableAllVisuals()
        {
            for (int i = 0; i < visualEntries.Count; i++)
            {
                LaserVisualEntry entry = visualEntries[i];

                if (entry == null)
                    continue;

                for (int j = 0; j < entry.endScaleTargets.Count; j++)
                    ResetEndScaleTarget(entry.endScaleTargets[j]);

                ResetAndDisable(entry.colorSymbolObject);
                ResetAndDisable(entry.visualObject);
            }
        }

        private void ResetEndScaleTarget(Transform target)
        {
            if (target == null)
                return;

            target.localScale = GetInitialTransformScale(target);
        }

        private void ResetAndDisable(GameObject targetObject)
        {
            if (targetObject == null)
                return;

            targetObject.transform.localRotation = GetInitialLocalRotation(targetObject);
            targetObject.transform.localScale = GetInitialLocalScale(targetObject);
            targetObject.SetActive(false);
        }

        private float GetRotationAngle(LaserPathNode node)
        {
            if ((node.NodeType == LaserPathNodeType.Corner || node.NodeType == LaserPathNodeType.CornerEnd) && node.HasIncomingDirection && node.HasOutgoingDirection)
            {
                if (node.CornerAngle == LaserCornerAngle.Turn90)
                {
                    LaserDirection entrySide = node.IncomingDirection.Opposite();
                    LaserDirection exitSide = node.OutgoingDirection;
                    return GetCornerAngle(entrySide, exitSide, node.CornerAngle);
                }

                if (node.CornerAngle == LaserCornerAngle.Turn45)
                {
                    LaserDirection directionA = node.IncomingDirection;
                    LaserDirection directionB = node.OutgoingDirection;
                    return GetCornerAngle(directionA, directionB, node.CornerAngle);
                }
            }

            if (node.HasOutgoingDirection)
                return node.OutgoingDirection.ToAngleZ();

            if (node.HasIncomingDirection)
                return node.IncomingDirection.ToAngleZ();

            return 0f;
        }

        private float GetCornerAngle(LaserDirection directionA, LaserDirection directionB, LaserCornerAngle cornerAngle)
        {
            if (cornerAngle == LaserCornerAngle.Turn45)
            {
                if (IsSamePair(directionA, directionB, LaserDirection.Up, LaserDirection.UpRight)) return 0f;
                if (IsSamePair(directionA, directionB, LaserDirection.UpRight, LaserDirection.Right)) return -45f;
                if (IsSamePair(directionA, directionB, LaserDirection.Right, LaserDirection.DownRight)) return -90f;
                if (IsSamePair(directionA, directionB, LaserDirection.DownRight, LaserDirection.Down)) return -135f;
                if (IsSamePair(directionA, directionB, LaserDirection.Down, LaserDirection.DownLeft)) return 180f;
                if (IsSamePair(directionA, directionB, LaserDirection.DownLeft, LaserDirection.Left)) return 135f;
                if (IsSamePair(directionA, directionB, LaserDirection.Left, LaserDirection.UpLeft)) return 90f;
                if (IsSamePair(directionA, directionB, LaserDirection.UpLeft, LaserDirection.Up)) return 45f;
            }

            if (cornerAngle == LaserCornerAngle.Turn90)
            {
                if (IsSamePair(directionA, directionB, LaserDirection.Up, LaserDirection.Right)) return 0f;
                if (IsSamePair(directionA, directionB, LaserDirection.Right, LaserDirection.Down)) return -90f;
                if (IsSamePair(directionA, directionB, LaserDirection.Down, LaserDirection.Left)) return 180f;
                if (IsSamePair(directionA, directionB, LaserDirection.Left, LaserDirection.Up)) return 90f;

                if (IsSamePair(directionA, directionB, LaserDirection.UpRight, LaserDirection.DownRight)) return -45f;
                if (IsSamePair(directionA, directionB, LaserDirection.DownRight, LaserDirection.DownLeft)) return -135f;
                if (IsSamePair(directionA, directionB, LaserDirection.DownLeft, LaserDirection.UpLeft)) return 135f;
                if (IsSamePair(directionA, directionB, LaserDirection.UpLeft, LaserDirection.UpRight)) return 45f;
            }

            return 0f;
        }

        private bool IsSamePair(LaserDirection a, LaserDirection b, LaserDirection x, LaserDirection y)
        {
            return (a == x && b == y) || (a == y && b == x);
        }

        private bool IsDiagonalDirection(LaserDirection direction)
        {
            return direction == LaserDirection.UpRight ||
                direction == LaserDirection.DownRight ||
                direction == LaserDirection.DownLeft ||
                direction == LaserDirection.UpLeft;
        }
    }
}
