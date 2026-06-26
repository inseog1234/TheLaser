using UnityEngine;
using Core;

namespace Laser
{
    public enum LaserPathNodeType
    {
        Start, Straight, Corner, Target, Blocked, Loop,
        End, CornerEnd, Splitter, StartEnd
    }

    public enum LaserCornerAngle
    {
        None, Turn45, Turn90
    }

    public readonly struct LaserPathNode
    {
        public readonly Vector2Int Position;

        public readonly bool HasIncomingDirection;
        public readonly LaserDirection IncomingDirection;

        public readonly bool HasOutgoingDirection;
        public readonly LaserDirection OutgoingDirection;

        public readonly LaserPathNodeType NodeType;
        public readonly LaserColorKind Color;
        public readonly LaserCornerAngle CornerAngle;
        public readonly int BeamId;

        public LaserPathNode(Vector2Int position, bool hasIncomingDirection, LaserDirection incomingDirection, bool hasOutgoingDirection, LaserDirection outgoingDirection, LaserPathNodeType nodeType, LaserColorKind color, int beamId, LaserCornerAngle cornerAngle)
        {
            Position = position;
            HasIncomingDirection = hasIncomingDirection;
            IncomingDirection = incomingDirection;
            HasOutgoingDirection = hasOutgoingDirection;
            OutgoingDirection = outgoingDirection;
            NodeType = nodeType;
            Color = color;
            BeamId = beamId;
            CornerAngle = cornerAngle;
        }

        public static LaserPathNode Start(Vector2Int position, LaserDirection outgoingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, false, LaserDirection.Up, true, outgoingDirection, LaserPathNodeType.Start, color, beamId, LaserCornerAngle.None);
        }

        public static LaserPathNode StartEnd(Vector2Int position, LaserDirection outgoingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, false, LaserDirection.Up, true, outgoingDirection, LaserPathNodeType.StartEnd, color, beamId, LaserCornerAngle.None);
        }

        public static LaserPathNode Straight(Vector2Int position, LaserDirection direction, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, direction, true, direction, LaserPathNodeType.Straight, color, beamId, LaserCornerAngle.None);
        }

        public static LaserPathNode Corner(Vector2Int position, LaserDirection incomingDirection, LaserDirection outgoingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, incomingDirection, true, outgoingDirection, LaserPathNodeType.Corner, color, beamId, GetCornerAngle(incomingDirection, outgoingDirection));
        }

        public static LaserPathNode CornerEnd(Vector2Int position, LaserDirection incomingDirection, LaserDirection outgoingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, incomingDirection, true, outgoingDirection, LaserPathNodeType.CornerEnd, color, beamId, GetCornerAngle(incomingDirection, outgoingDirection));
        }

        public static LaserPathNode Splitter(Vector2Int position, LaserDirection incomingDirection, LaserDirection outgoingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, incomingDirection, true, outgoingDirection, LaserPathNodeType.Splitter, color, beamId, GetCornerAngle(incomingDirection, outgoingDirection));
        }

        public static LaserPathNode Target(Vector2Int position, LaserDirection incomingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, incomingDirection, false, LaserDirection.Up, LaserPathNodeType.Target, color, beamId, LaserCornerAngle.None);
        }

        public static LaserPathNode Blocked(Vector2Int position, LaserDirection incomingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, incomingDirection, false, LaserDirection.Up, LaserPathNodeType.Blocked, color, beamId, LaserCornerAngle.None);
        }

        public static LaserPathNode Loop(Vector2Int position, LaserDirection incomingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, incomingDirection, false, LaserDirection.Up, LaserPathNodeType.Loop, color, beamId, LaserCornerAngle.None);
        }

        public static LaserPathNode End(Vector2Int position, LaserDirection incomingDirection, LaserColorKind color, int beamId)
        {
            return new LaserPathNode(position, true, incomingDirection, false, LaserDirection.Up, LaserPathNodeType.End, color, beamId, LaserCornerAngle.None);
        }

        private static LaserCornerAngle GetCornerAngle(LaserDirection incomingDirection, LaserDirection outgoingDirection)
        {
            int incomingIndex = ToDirectionIndex(incomingDirection);
            int outgoingIndex = ToDirectionIndex(outgoingDirection);

            int diff = Mathf.Abs(outgoingIndex - incomingIndex);
            diff = Mathf.Min(diff, 8 - diff);

            if (diff == 1)
                return LaserCornerAngle.Turn45;

            if (diff == 2)
                return LaserCornerAngle.Turn90;

            return LaserCornerAngle.None;
        }

        private static int ToDirectionIndex(LaserDirection direction)
        {
            return direction switch
            {
                LaserDirection.Up => 0,
                LaserDirection.UpRight => 1,
                LaserDirection.Right => 2,
                LaserDirection.DownRight => 3,
                LaserDirection.Down => 4,
                LaserDirection.DownLeft => 5,
                LaserDirection.Left => 6,
                LaserDirection.UpLeft => 7,
                _ => 0
            };
        }
    }
}
