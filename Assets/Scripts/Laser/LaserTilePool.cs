using System.Collections.Generic;
using UnityEngine;

namespace Laser
{
    public class LaserTilePool : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField] private LaserPathTile prefab;
        [SerializeField] private Transform poolRoot;
        [SerializeField] private int preloadCount = 32;

        private readonly Queue<LaserPathTile> pool = new();

        private void Awake()
        {
            if (poolRoot == null)
            {
                GameObject rootObject = new GameObject("laserPool");
                rootObject.transform.SetParent(transform);
                rootObject.transform.localPosition = Vector3.zero;
                poolRoot = rootObject.transform;
            }

            Preload();
        }

        private void Preload()
        {
            if (prefab == null)
                return;

            for (int i = 0; i < preloadCount; i++)
            {
                LaserPathTile tile = CreateNewTile();
                Return(tile);
            }
        }

        public LaserPathTile Get(Transform activeParent)
        {
            LaserPathTile tile;

            if (pool.Count > 0)
            {
                tile = pool.Dequeue();
            }
            else
            {
                tile = CreateNewTile();
            }

            tile.transform.SetParent(activeParent);
            tile.transform.localScale = Vector3.one;
            tile.gameObject.SetActive(true);

            return tile;
        }

        public void Return(LaserPathTile tile)
        {
            if (tile == null)
                return;

            tile.ResetTile();
            tile.gameObject.SetActive(false);
            tile.transform.SetParent(poolRoot);
            tile.transform.localPosition = Vector3.zero;

            pool.Enqueue(tile);
        }

        private LaserPathTile CreateNewTile()
        {
            LaserPathTile tile = Instantiate(prefab, poolRoot);
            tile.gameObject.SetActive(false);
            return tile;
        }
    }
}