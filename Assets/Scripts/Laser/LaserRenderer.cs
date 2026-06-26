using System;
using System.Collections.Generic;
using UnityEngine;
using Grid;
using Core;

namespace Laser
{
    [Serializable]
    public class LaserColorVisual
    {
        public LaserColorKind colorKind = LaserColorKind.Default;
        public Color color = Color.white;
    }

    public class LaserRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private LaserTilePool tilePool;
        [SerializeField] private Transform laserTileParent;

        [Header("Color")]
        [SerializeField] private List<LaserColorVisual> colorPalette = new();

        [Header("Option")]
        [SerializeField] private bool renderBlockedNode = false;

        private readonly List<LaserPathTile> activeTiles = new();

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();

            if (tilePool == null)
                tilePool = FindFirstObjectByType<LaserTilePool>();

            if (laserTileParent == null)
            {
                GameObject parentObject = new GameObject("ActiveLaserTiles");
                parentObject.transform.SetParent(transform);
                parentObject.transform.localPosition = Vector3.zero;
                laserTileParent = parentObject.transform;
            }
        }

        public void Render(LaserResult result)
        {
            Clear();

            if (result == null || tilePool == null || gridManager == null)
                return;

            for (int i = 0; i < result.PathNodes.Count; i++)
            {
                LaserPathNode node = result.PathNodes[i];

                if (!renderBlockedNode && node.NodeType == LaserPathNodeType.Blocked)
                    continue;

                LaserPathTile tile = tilePool.Get(laserTileParent);
                tile.transform.position = gridManager.GridToWorld(node.Position);
                tile.transform.rotation = Quaternion.identity;
                tile.name = $"LaserTile_{node.NodeType}_{node.Position}_Beam{node.BeamId}";

                Color color = ResolveColor(node.Color);
                tile.SetNode(node, color);

                activeTiles.Add(tile);
            }
        }

        public void Clear()
        {
            for (int i = activeTiles.Count - 1; i >= 0; i--)
            {
                if (activeTiles[i] != null && tilePool != null)
                    tilePool.Return(activeTiles[i]);
            }

            activeTiles.Clear();
        }

        private Color ResolveColor(LaserColorKind colorKind)
        {
            for (int i = 0; i < colorPalette.Count; i++)
            {
                if (colorPalette[i] != null && colorPalette[i].colorKind == colorKind)
                    return colorPalette[i].color;
            }

            return colorKind switch
            {
                LaserColorKind.Red => new Color(1f, 0.2f, 0.2f, 1f),
                LaserColorKind.Blue => new Color(0.2f, 0.45f, 1f, 1f),
                LaserColorKind.Green => new Color(0.25f, 1f, 0.35f, 1f),
                LaserColorKind.Yellow => new Color(1f, 0.9f, 0.2f, 1f),
                LaserColorKind.Purple => new Color(0.75f, 0.25f, 1f, 1f),
                LaserColorKind.White => Color.white,
                _ => new Color(0.2f, 0.85f, 1f, 1f)
            };
        }
    }
}
