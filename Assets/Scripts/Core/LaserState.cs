using System;
using UnityEngine;

namespace Core
{
    [Serializable]
    public readonly struct LaserState : IEquatable<LaserState>
    {
        public readonly Vector2Int Position;
        public readonly GridDirection Direction;

        public LaserState(Vector2Int position, GridDirection direction)
        {
            Position = position;
            Direction = direction;
        }

        public bool Equals(LaserState other)
        {
            return Position == other.Position && Direction == other.Direction;
        }

        public override bool Equals(object obj)
        {
            return obj is LaserState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ (int)Direction;
            }
        }

        public override string ToString()
        {
            return $"Position: {Position}, Direction: {Direction}";
        }
    }
}