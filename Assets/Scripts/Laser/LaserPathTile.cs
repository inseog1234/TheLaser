using System;
using System.Collections.Generic;
using UnityEngine;
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
    }

    public class LaserPathTile : MonoBehaviour
    {
        [Header("Visual Entries")]
        [SerializeField] private List<LaserVisualEntry> visualEntries = new();

        [Header("Color")]
        [SerializeField] private Color defaultColor = Color.white;

        [Header("Diagonal Straight Scale")]
        [SerializeField] private float diagonalStraightYScaleMultiplier = 1.5f;

        private readonly Dictionary<GameObject, Vector3> initialLocalScales = new();
        private readonly Dictionary<GameObject, Quaternion> initialLocalRotations = new();

        private void Awake()
        {
            CacheInitialRotations();
        }

        private void OnValidate()
        {
            CacheInitialRotations();
        }

        public void SetNode(LaserPathNode node)
        {
            SetNode(node, defaultColor);
        }

        public void SetNode(LaserPathNode node, Color laserColor)
        {
            if (initialLocalRotations.Count <= 0)
                CacheInitialRotations();

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

            ApplyDiagonalStraightScale(entry.visualObject, node);
            ApplyColor(entry.visualObject, laserColor);
        }

        private void ApplyDiagonalStraightScale(GameObject visualObject, LaserPathNode node)
        {
            if (visualObject == null)
                return;

            Vector3 baseScale = GetInitialLocalScale(visualObject);
            visualObject.transform.localScale = baseScale;

            if (node.NodeType != LaserPathNodeType.Straight)
                return;

            LaserDirection direction = GetMainDirection(node);

            if (!IsDiagonalDirection(direction))
                return;

            Vector3 scaled = baseScale;
            scaled.y *= diagonalStraightYScaleMultiplier;
            visualObject.transform.localScale = scaled;
        }

        private Vector3 GetInitialLocalScale(GameObject visualObject)
        {
            if (visualObject != null && initialLocalScales.TryGetValue(visualObject, out Vector3 scale))
                return scale;

            return Vector3.one;
        }

        private bool IsDiagonalDirection(LaserDirection direction)
        {
            return direction == LaserDirection.UpRight ||
                direction == LaserDirection.DownRight ||
                direction == LaserDirection.DownLeft ||
                direction == LaserDirection.UpLeft;
        }

        private Vector3 GetCornerFlipEuler(LaserPathNode node)
        {
            if (node.NodeType != LaserPathNodeType.Corner &&
                node.NodeType != LaserPathNodeType.CornerEnd)
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

        public void ResetTile()
        {
            DisableAllVisuals();
        }

        private void CacheInitialRotations()
        {
            initialLocalRotations.Clear();
            initialLocalScales.Clear();

            for (int i = 0; i < visualEntries.Count; i++)
            {
                LaserVisualEntry entry = visualEntries[i];

                if (entry == null || entry.visualObject == null)
                    continue;

                if (!initialLocalRotations.ContainsKey(entry.visualObject))
                    initialLocalRotations.Add(entry.visualObject, entry.visualObject.transform.localRotation);

                if (!initialLocalScales.ContainsKey(entry.visualObject))
                    initialLocalScales.Add(entry.visualObject, entry.visualObject.transform.localScale);
            }
        }

        private Quaternion GetInitialLocalRotation(GameObject visualObject)
        {
            if (visualObject != null && initialLocalRotations.TryGetValue(visualObject, out Quaternion rotation))
                return rotation;

            return Quaternion.identity;
        }

        private void ApplyColor(GameObject targetObject, Color color)
        {
            if (targetObject == null)
                return;

            SpriteRenderer[] renderers = targetObject.GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
                renderers[i].color = color;
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

                if (entry == null || entry.visualObject == null)
                    continue;

                entry.visualObject.transform.localRotation = GetInitialLocalRotation(entry.visualObject);
                entry.visualObject.transform.localScale = GetInitialLocalScale(entry.visualObject);
                entry.visualObject.SetActive(false);
            }
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
    }
}