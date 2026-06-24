using UnityEngine;

namespace Core
{
    // 그리드 방향 

    
    public enum GridDirection
    {
        Up, Right, Down, Left
    }

    public static class GridDirectionExtensions
    {
        public static Vector2Int ToVector(this GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => Vector2Int.up,
                GridDirection.Right => Vector2Int.right,
                GridDirection.Down => Vector2Int.down,
                GridDirection.Left => Vector2Int.left,
                _ => Vector2Int.zero
            };
        }

        public static GridDirection Opposite(this GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => GridDirection.Down,
                GridDirection.Right => GridDirection.Left,
                GridDirection.Down => GridDirection.Up,
                GridDirection.Left => GridDirection.Right,
                _ => GridDirection.Down
            };
        }

        public static GridDirection RotateClockwise(this GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => GridDirection.Right,
                GridDirection.Right => GridDirection.Down,
                GridDirection.Down => GridDirection.Left,
                GridDirection.Left => GridDirection.Up,
                _ => GridDirection.Up
            };
        }

        public static GridDirection RotateCounterClockwise(this GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => GridDirection.Left,
                GridDirection.Left => GridDirection.Down,
                GridDirection.Down => GridDirection.Right,
                GridDirection.Right => GridDirection.Up,
                _ => GridDirection.Up
            };
        }

        public static float ToAngleZ(this GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => 0f,
                GridDirection.Right => -90f,
                GridDirection.Down => 180f,
                GridDirection.Left => 90f,
                _ => 0f
            };
        }
    }
}