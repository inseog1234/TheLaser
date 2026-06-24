using UnityEngine;
using Core;

namespace Laser
{
    public enum LaserPathNodeType
    {
        Start,
        Straight,
        Corner,
        Target,
        Blocked,
        Loop,
        End,
        CornerEnd
    }

    public readonly struct LaserPathNode
    {
        public readonly Vector2Int Position;

        public readonly bool HasIncomingDirection;
        public readonly GridDirection IncomingDirection;

        public readonly bool HasOutgoingDirection;
        public readonly GridDirection OutgoingDirection;

        public readonly LaserPathNodeType NodeType;

        public LaserPathNode(
            Vector2Int position,
            bool hasIncomingDirection,
            GridDirection incomingDirection,
            bool hasOutgoingDirection,
            GridDirection outgoingDirection,
            LaserPathNodeType nodeType)
        {
            Position = position;
            HasIncomingDirection = hasIncomingDirection;
            IncomingDirection = incomingDirection;
            HasOutgoingDirection = hasOutgoingDirection;
            OutgoingDirection = outgoingDirection;
            NodeType = nodeType;
        }

        public static LaserPathNode Start(Vector2Int position, GridDirection outgoingDirection)
        {
            return new LaserPathNode(position, false, GridDirection.Up, true, outgoingDirection, LaserPathNodeType.Start);
        }

        public static LaserPathNode Straight(Vector2Int position, GridDirection direction)
        {
            return new LaserPathNode(position, true, direction, true, direction, LaserPathNodeType.Straight);
        }

        public static LaserPathNode Corner(Vector2Int position, GridDirection incomingDirection, GridDirection outgoingDirection)
        {
            return new LaserPathNode(position, true, incomingDirection, true, outgoingDirection, LaserPathNodeType.Corner);
        }

        public static LaserPathNode CornerEnd(Vector2Int position, GridDirection incomingDirection, GridDirection outgoingDirection)
        {
            return new LaserPathNode(position, true, incomingDirection, true, outgoingDirection, LaserPathNodeType.CornerEnd);
        }

        public static LaserPathNode Target(Vector2Int position, GridDirection incomingDirection)
        {
            return new LaserPathNode(position, true, incomingDirection, false, GridDirection.Up, LaserPathNodeType.Target);
        }

        public static LaserPathNode Blocked(Vector2Int position, GridDirection incomingDirection)
        {
            return new LaserPathNode( position, true, incomingDirection, false, GridDirection.Up, LaserPathNodeType.Blocked);
        }

        public static LaserPathNode Loop(Vector2Int position, GridDirection incomingDirection)
        {
            return new LaserPathNode(position, true, incomingDirection, false, GridDirection.Up, LaserPathNodeType.Loop);
        }

        public static LaserPathNode End(Vector2Int position, GridDirection incomingDirection)
        {
            return new LaserPathNode(position, true, incomingDirection, false, GridDirection.Up, LaserPathNodeType.End);
        }
    }
}