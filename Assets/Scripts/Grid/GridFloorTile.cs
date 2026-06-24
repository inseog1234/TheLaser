using UnityEngine;

namespace Grid
{
    public class GridFloorTile : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Style")]
        [SerializeField] private Color normalColor = new Color(1f, 1, 1f, 1f);
        [SerializeField] private Color alternateColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        [SerializeField] private bool useCheckerPattern = true;

        [Header("Size")]
        [SerializeField] private float sizeRatio = 0.95f;

        [Header("Sorting")]
        [SerializeField] private int sortingOrder = -10;

        private void Awake()
        {
            CacheReferences();
        }

        public void Initialize(float cellSize, Vector2Int gridPosition)
        {
            CacheReferences();

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * cellSize * sizeRatio;
            }

            if (spriteRenderer != null)
            {
                bool isAlternate = (gridPosition.x + gridPosition.y) % 2 == 0;

                spriteRenderer.color = useCheckerPattern && isAlternate ? alternateColor : normalColor;

                spriteRenderer.sortingOrder = sortingOrder;
            }
        }

        private void CacheReferences()
        {
            if (visualRoot == null)
                visualRoot = transform;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }
}