using UnityEngine;
using Core;

namespace Laser
{
    public readonly struct LaserSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly LaserColorKind Color;
        public readonly int BeamId;

        public LaserSegment(Vector2 start, Vector2 end, LaserColorKind color, int beamId)
        {
            Start = start;
            End = end;
            Color = color;
            BeamId = beamId;
        }
    }
}
