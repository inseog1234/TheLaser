using System.Collections.Generic;
using UnityEngine;
using Grid;

namespace Laser
{
    public class LaserRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private LaserTilePool tilePool;
        [SerializeField] private Transform laserTileParent;

        [Header("Option")]
        [SerializeField] private bool renderBlockedNode = false;

        private readonly List<LaserPathTile> activeTiles = new();

        private void Awake()
        {
            GameObject parentObject = new GameObject("ActiveLaserTiles");
            parentObject.transform.SetParent(transform);
            parentObject.transform.localPosition = Vector3.zero;
            laserTileParent = parentObject.transform;
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
                tile.name = $"LaserTile_{node.NodeType}_{node.Position}";

                tile.SetNode(node);

                activeTiles.Add(tile);
            }
        }

        public void Clear()
        {
            for (int i = activeTiles.Count - 1; i >= 0; i--)
            {
                if (activeTiles[i] != null && tilePool != null)
                {
                    tilePool.Return(activeTiles[i]);
                }
            }

            activeTiles.Clear();
        }
    }
}