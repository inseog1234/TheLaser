using UnityEngine;
using Core;

namespace LevelEditor
{
    public class LevelEditorRangePreview : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color validColor = new Color(0.2f, 0.8f, 1f, 0.25f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.25f);

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            Hide();
        }

        public void Show(Vector3 worldPosition, float cellSize, int width, int height, bool valid)
        {
            gameObject.SetActive(true);
            transform.position = worldPosition;

            if (visualRoot != null)
                visualRoot.localScale = new Vector3(width * cellSize, height * cellSize, 1f);

            if (spriteRenderer != null)
                spriteRenderer.color = valid ? validColor : invalidColor;
        }

        public void ShowDistanceSensor(Vector3 worldPosition, float cellSize, float detectionRadius, bool valid)
        {
            int size = Mathf.Max(1, Mathf.CeilToInt(detectionRadius * 2f) + 1);
            Show(worldPosition, cellSize, size, size, valid);
        }

        public void ShowTransformZone(Vector3 worldPosition, float cellSize, TransformZoneData data, bool valid)
        {
            if (data == null)
            {
                Hide();
                return;
            }

            Show(worldPosition, cellSize, Mathf.Max(1, data.width), Mathf.Max(1, data.height), valid);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
