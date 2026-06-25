using UnityEngine;

namespace Core
{
    // 플레이어 이동은 GridDirection 4방향을 유지하고,
    // 레이저 계산만 LaserDirection 8방향을 사용한다.
    public enum LaserDirection
    {
        Up = 0,
        UpRight = 1,
        Right = 2,
        DownRight = 3,
        Down = 4,
        DownLeft = 5,
        Left = 6,
        UpLeft = 7
    }

    public static class LaserDirectionExtensions
    {
        public static Vector2Int ToVector(this LaserDirection direction)
        {
            return direction switch
            {
                LaserDirection.Up => Vector2Int.up,
                LaserDirection.UpRight => new Vector2Int(1, 1),
                LaserDirection.Right => Vector2Int.right,
                LaserDirection.DownRight => new Vector2Int(1, -1),
                LaserDirection.Down => Vector2Int.down,
                LaserDirection.DownLeft => new Vector2Int(-1, -1),
                LaserDirection.Left => Vector2Int.left,
                LaserDirection.UpLeft => new Vector2Int(-1, 1),
                _ => Vector2Int.zero
            };
        }

        public static Vector2 ToVector2(this LaserDirection direction)
        {
            Vector2Int v = direction.ToVector();
            return new Vector2(v.x, v.y).normalized;
        }

        public static LaserDirection FromGridDirection(GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => LaserDirection.Up,
                GridDirection.Right => LaserDirection.Right,
                GridDirection.Down => LaserDirection.Down,
                GridDirection.Left => LaserDirection.Left,
                _ => LaserDirection.Up
            };
        }

        public static bool IsCardinal(this LaserDirection direction)
        {
            return direction == LaserDirection.Up ||
                   direction == LaserDirection.Right ||
                   direction == LaserDirection.Down ||
                   direction == LaserDirection.Left;
        }

        public static bool TryToGridDirection(this LaserDirection direction, out GridDirection gridDirection)
        {
            switch (direction)
            {
                case LaserDirection.Up:
                    gridDirection = GridDirection.Up;
                    return true;
                case LaserDirection.Right:
                    gridDirection = GridDirection.Right;
                    return true;
                case LaserDirection.Down:
                    gridDirection = GridDirection.Down;
                    return true;
                case LaserDirection.Left:
                    gridDirection = GridDirection.Left;
                    return true;
                default:
                    gridDirection = GridDirection.Up;
                    return false;
            }
        }

        public static LaserDirection Opposite(this LaserDirection direction)
        {
            return RotateByStep(direction, 4);
        }

        public static LaserDirection RotateClockwise45(this LaserDirection direction)
        {
            return RotateByStep(direction, 1);
        }

        public static LaserDirection RotateCounterClockwise45(this LaserDirection direction)
        {
            return RotateByStep(direction, -1);
        }

        public static LaserDirection RotateClockwise90(this LaserDirection direction)
        {
            return RotateByStep(direction, 2);
        }

        public static LaserDirection RotateCounterClockwise90(this LaserDirection direction)
        {
            return RotateByStep(direction, -2);
        }

        public static LaserDirection RotateByStep(this LaserDirection direction, int step)
        {
            int value = ((int)direction + step) % 8;
            if (value < 0)
                value += 8;

            return (LaserDirection)value;
        }

        public static float ToAngleZ(this LaserDirection direction)
        {
            return direction switch
            {
                LaserDirection.Up => 0f,
                LaserDirection.UpRight => -45f,
                LaserDirection.Right => -90f,
                LaserDirection.DownRight => -135f,
                LaserDirection.Down => 180f,
                LaserDirection.DownLeft => 135f,
                LaserDirection.Left => 90f,
                LaserDirection.UpLeft => 45f,
                _ => 0f
            };
        }
    }
}
