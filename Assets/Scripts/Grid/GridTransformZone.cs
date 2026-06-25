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
        [SerializeField] private TransformZoneType zoneType = TransformZoneType.Rotate90;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private MirrorAxis mirrorAxis = MirrorAxis.Vertical;

        public string ZoneId => zoneId;
        public Vector2Int Center => center;
        public int Width => width;
        public int Height => height;
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
            int minX = center.x - width / 2;
            int maxX = minX + width - 1;
            int minY = center.y - height / 2;
            int maxY = minY + height - 1;

            return position.x >= minX && position.x <= maxX && position.y >= minY && position.y <= maxY;
        }
    }
}
