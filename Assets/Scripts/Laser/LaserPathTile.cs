using System;
using System.Collections.Generic;
using UnityEngine;
using Core;

namespace Laser
{
    public enum LaserVisualMatchMode
    {
        Any, Direction, CornerSides
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
        public List<GridDirection> directions = new();

        [Header("Corner Condition")]
        public GridDirection cornerSideA = GridDirection.Up;
        public GridDirection cornerSideB = GridDirection.Right;

        [Header("Rotation")]
        public bool autoRotateVisual = false;
        public Vector3 rotationOffset;
    }

    public class LaserPathTile : MonoBehaviour
    {
        [Header("Visual Entries")]
        [SerializeField] private List<LaserVisualEntry> visualEntries = new();

        public void SetNode(LaserPathNode node)
        {
            DisableAllVisuals();

            LaserVisualEntry entry = FindBestVisual(node);

            if (entry == null || entry.visualObject == null)
                return;

            entry.visualObject.SetActive(true);

            if (entry.autoRotateVisual)
            {
                float angle = GetRotationAngle(node);
                entry.visualObject.transform.localRotation =
                    Quaternion.Euler(entry.rotationOffset + new Vector3(0f, 0f, angle));
            }
        }

        public void ResetTile()
        {
            DisableAllVisuals();
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

                if (entry.matchMode == LaserVisualMatchMode.Any)
                {
                    if (fallback == null)
                        fallback = entry;

                    continue;
                }

                if (IsMatched(entry, node))
                    return entry;
            }

            return fallback;
        }

        private bool IsMatched(LaserVisualEntry entry, LaserPathNode node)
        {
            switch (entry.matchMode)
            {
                case LaserVisualMatchMode.Direction:
                    GridDirection mainDirection = GetMainDirection(node);
                    return entry.directions.Contains(mainDirection);

                case LaserVisualMatchMode.CornerSides:
                    if (node.NodeType != LaserPathNodeType.Corner)
                        return false;

                    if (!node.HasIncomingDirection || !node.HasOutgoingDirection)
                        return false;

                    GridDirection entrySide = node.IncomingDirection.Opposite();
                    GridDirection exitSide = node.OutgoingDirection;

                    return IsSamePair(entrySide, exitSide, entry.cornerSideA, entry.cornerSideB);

                default:
                    return true;
            }
        }

        private GridDirection GetMainDirection(LaserPathNode node)
        {
            if (node.HasOutgoingDirection)
                return node.OutgoingDirection;

            if (node.HasIncomingDirection)
                return node.IncomingDirection;

            return GridDirection.Up;
        }

        private void DisableAllVisuals()
        {
            for (int i = 0; i < visualEntries.Count; i++)
            {
                if (visualEntries[i] == null)
                    continue;

                if (visualEntries[i].visualObject != null)
                {
                    visualEntries[i].visualObject.SetActive(false);
                }
            }
        }

        private float GetRotationAngle(LaserPathNode node)
        {
            if (node.NodeType == LaserPathNodeType.Corner &&
                node.HasIncomingDirection &&
                node.HasOutgoingDirection)
            {
                GridDirection entrySide = node.IncomingDirection.Opposite();
                GridDirection exitSide = node.OutgoingDirection;

                return GetCornerAngle(entrySide, exitSide);
            }

            if (node.HasOutgoingDirection)
                return node.OutgoingDirection.ToAngleZ();

            if (node.HasIncomingDirection)
                return node.IncomingDirection.ToAngleZ();

            return 0f;
        }

        private float GetCornerAngle(GridDirection a, GridDirection b)
        {
            if (IsSamePair(a, b, GridDirection.Up, GridDirection.Right))
                return 0f;

            if (IsSamePair(a, b, GridDirection.Right, GridDirection.Down))
                return -90f;

            if (IsSamePair(a, b, GridDirection.Down, GridDirection.Left))
                return 180f;

            if (IsSamePair(a, b, GridDirection.Left, GridDirection.Up))
                return 90f;

            return 0f;
        }

        private bool IsSamePair(GridDirection a, GridDirection b, GridDirection x, GridDirection y)
        {
            return (a == x && b == y) || (a == y && b == x);
        }
    }
}