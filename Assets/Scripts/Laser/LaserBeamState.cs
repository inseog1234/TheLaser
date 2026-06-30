using System;
using UnityEngine;
using Core;

namespace Laser
{
    public readonly struct LaserBeamState : IEquatable<LaserBeamState>
    {
        public readonly Vector2Int Position;
        public readonly LaserDirection Direction;
        public readonly LaserColorKind Color;
        public readonly int RemainingDistance;

        public LaserBeamState(Vector2Int position, LaserDirection direction, LaserColorKind color, int remainingDistance = -1)
        {
            Position = position;
            Direction = direction;
            Color = color;
            RemainingDistance = remainingDistance;
        }

        public bool Equals(LaserBeamState other)
        {
            return Position == other.Position &&
                   Direction == other.Direction &&
                   Color == other.Color &&
                   RemainingDistance == other.RemainingDistance;
        }

        public override bool Equals(object obj)
        {
            return obj is LaserBeamState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Position.GetHashCode();
                hash = (hash * 397) ^ (int)Direction;
                hash = (hash * 397) ^ (int)Color;
                hash = (hash * 397) ^ RemainingDistance;
                return hash;
            }
        }
    }
}
