using System;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class TransformZoneData
    {
        public string zoneId;
        public Vector2Int center;
        [Min(1)] public int width = 3;
        [Min(1)] public int height = 3;
        [Tooltip("짝수 가로 크기일 때 -1은 왼쪽, 1은 오른쪽 보정")] public int offsetX = -1;
        [Tooltip("짝수 세로 크기일 때 -1은 아래, 1은 위 보정")] public int offsetY = -1;

        public TransformZoneType zoneType = TransformZoneType.Rotate90;

        [Header("Rotate90")]
        public bool clockwise = true;

        [Header("Mirror")]
        public MirrorAxis mirrorAxis = MirrorAxis.Vertical;
    }
}
