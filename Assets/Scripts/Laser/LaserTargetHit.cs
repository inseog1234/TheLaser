using UnityEngine;
using Core;

namespace Laser
{
    public readonly struct LaserTargetHit
    {
        public readonly Vector2Int Position;
        public readonly LaserColorKind Color;
        public readonly int BeamId;
        public readonly int OrderIndex;

        public LaserTargetHit(Vector2Int position, LaserColorKind color, int beamId, int orderIndex)
        {
            Position = position;
            Color = color;
            BeamId = beamId;
            OrderIndex = orderIndex;
        }
    }
}
