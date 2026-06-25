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

        public LaserBeamState(Vector2Int position, LaserDirection direction, LaserColorKind color)
        {
            Position = position;
            Direction = direction;
            Color = color;
        }

        public bool Equals(LaserBeamState other)
        {
            return Position == other.Position && Direction == other.Direction && Color == other.Color;
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
                return hash;
            }
        }
    }
}
