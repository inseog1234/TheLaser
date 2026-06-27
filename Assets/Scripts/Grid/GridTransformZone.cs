using UnityEngine;
using Core;

namespace Grid
{
    public class GridTransformZone : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;

        [Header("Visual")]
        [SerializeField] private GameObject visualRoot;

        [Header("Runtime")]
        [SerializeField] private string zoneId;
        [SerializeField] private Vector2Int center;
        [SerializeField] private int width = 3;
        [SerializeField] private int height = 3;
        [SerializeField] private int offsetX = -1;
        [SerializeField] private int offsetY = -1;
        [SerializeField] private TransformZoneType zoneType = TransformZoneType.Rotate90;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private MirrorAxis mirrorAxis = MirrorAxis.Vertical;

        public string ZoneId => zoneId;
        public Vector2Int Center => center;
        public int Width => width;
        public int Height => height;
        public int OffsetX => offsetX;
        public int OffsetY => offsetY;
        public TransformZoneType ZoneType => zoneType;
        public bool Clockwise => clockwise;
        public MirrorAxis MirrorAxis => mirrorAxis;

        public void Initialize(GridManager owner, TransformZoneData data)
        {
            gridManager = owner;
            zoneId = string.IsNullOrWhiteSpace(data.zoneId)
                ? $"Zone_{data.center.x}_{data.center.y}"
                : data.zoneId;

            center = data.center;
            width = data.width;
            height = data.height;
            offsetX = NormalizeOffset(width, data.offsetX);
            offsetY = NormalizeOffset(height, data.offsetY);
            zoneType = data.zoneType;
            clockwise = data.clockwise;
            mirrorAxis = data.mirrorAxis;
        }

        [ContextMenu("Activate Zone")]
        public void Activate()
        {
            if (gridManager != null)
                gridManager.ApplyTransformZone(zoneId);
        }

        public bool Contains(Vector2Int position)
        {
            Vector2Int min = GetMinCell();
            int maxX = min.x + width - 1;
            int maxY = min.y + height - 1;

            return position.x >= min.x && position.x <= maxX && position.y >= min.y && position.y <= maxY;
        }

        public Vector2Int GetMinCell()
        {
            int minX = center.x - width / 2;
            int minY = center.y - height / 2;

            if (width % 2 == 0 && offsetX > 0)
                minX += 1;

            if (height % 2 == 0 && offsetY > 0)
                minY += 1;

            return new Vector2Int(minX, minY);
        }

        private int NormalizeOffset(int size, int value)
        {
            if (size % 2 != 0)
                return 0;

            return value >= 0 ? 1 : -1;
        }
    }
}
