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


        public bool HasActiveLaser => activeTiles.Count > 0;

        public bool TryGetRenderedLaserBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < activeTiles.Count; i++)
            {
                LaserPathTile tile = activeTiles[i];

                if (tile == null)
                    continue;

                Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
                bool tileHasRenderer = false;

                for (int j = 0; j < renderers.Length; j++)
                {
                    Renderer renderer = renderers[j];

                    if (renderer == null || !renderer.gameObject.activeInHierarchy)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    tileHasRenderer = true;
                }

                if (tileHasRenderer)
                    continue;

                if (!hasBounds)
                {
                    bounds = new Bounds(tile.transform.position, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(tile.transform.position);
                }
            }

            return hasBounds;
        }

        public bool TryGetMostOffscreenLaserPoint(Camera camera, float viewportMargin, out Vector3 laserWorldPosition)
        {
            laserWorldPosition = Vector3.zero;

            if (camera == null || activeTiles.Count <= 0)
                return false;

            viewportMargin = Mathf.Clamp(viewportMargin, 0f, 0.45f);
            float min = viewportMargin;
            float max = 1f - viewportMargin;
            float bestScore = 0f;
            bool found = false;

            for (int i = 0; i < activeTiles.Count; i++)
            {
                LaserPathTile tile = activeTiles[i];

                if (tile == null)
                    continue;

                Vector3 position = tile.transform.position;
                Vector3 viewportPosition = camera.WorldToViewportPoint(position);
                float score = 0f;

                if (viewportPosition.x < min)
                    score += min - viewportPosition.x;
                else if (viewportPosition.x > max)
                    score += viewportPosition.x - max;

                if (viewportPosition.y < min)
                    score += min - viewportPosition.y;
                else if (viewportPosition.y > max)
                    score += viewportPosition.y - max;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                laserWorldPosition = position;
                found = true;
            }

            return found;
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
