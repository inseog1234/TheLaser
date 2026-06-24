using UnityEngine;
using Core;

namespace Grid
{
    public class GridObject : MonoBehaviour
    {
        [Header("Object Info")]
        [SerializeField] private PuzzleObjectType objectType = PuzzleObjectType.Mirror;
        [SerializeField] private ManipulationType manipulationType = ManipulationType.None;

        [Header("Grid")]
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private GridDirection direction = GridDirection.Up;

        [Header("Mirror")]
        [SerializeField] private MirrorShape mirrorShape = MirrorShape.NormalL;

        [Header("Visual")]
        [SerializeField] private Transform visualRoot;

        public PuzzleObjectType ObjectType => objectType;
        public ManipulationType ManipulationType => manipulationType;
        public Vector2Int GridPosition => gridPosition;
        public GridDirection Direction => direction;
        public MirrorShape MirrorShape => mirrorShape;

        public bool CanPush => manipulationType.CanPush();
        public bool CanRotate => manipulationType.CanRotate();

        private void OnValidate()
        {
            RefreshVisual();
        }

        public void Initialize(StageObjectData data, Vector3 worldPosition)
        {
            objectType = data.objectType;
            manipulationType = data.manipulationType;
            gridPosition = data.position;
            direction = data.direction;
            mirrorShape = data.mirrorShape;

            transform.position = worldPosition;

            RefreshVisual();
        }

        public void SetGridPosition(Vector2Int newPosition, Vector3 worldPosition)
        {
            gridPosition = newPosition;
            transform.position = worldPosition;
        }

        public void SetDirection(GridDirection newDirection)
        {
            direction = newDirection;
            RefreshVisual();
        }

        public void RotateClockwise()
        {
            direction = direction.RotateClockwise();
            RefreshVisual();
        }

        public void RotateCounterClockwise()
        {
            direction = direction.RotateCounterClockwise();
            RefreshVisual();
        }

        public bool TryReflectLaser(GridDirection laserMoveDirection, out GridDirection reflectedDirection)
        {
            reflectedDirection = laserMoveDirection;

            if (objectType != PuzzleObjectType.Mirror)
                return false;

            GridDirection entrySide = laserMoveDirection.Opposite();

            GetMirrorOpenSides(
                mirrorShape,
                direction,
                out GridDirection sideA,
                out GridDirection sideB
            );

            if (entrySide == sideA)
            {
                reflectedDirection = sideB;
                return true;
            }

            if (entrySide == sideB)
            {
                reflectedDirection = sideA;
                return true;
            }

            return false;
        }

        private static void GetMirrorOpenSides(
            MirrorShape shape,
            GridDirection mirrorDirection,
            out GridDirection sideA,
            out GridDirection sideB)
        {
            if (shape == MirrorShape.NormalL)
            {
                sideA = GridDirection.Up;
                sideB = GridDirection.Right;
            }
            else
            {
                sideA = GridDirection.Up;
                sideB = GridDirection.Left;
            }

            sideA = RotateSideByMirrorDirection(sideA, mirrorDirection);
            sideB = RotateSideByMirrorDirection(sideB, mirrorDirection);
        }

        private static GridDirection RotateSideByMirrorDirection(
            GridDirection side,
            GridDirection mirrorDirection)
        {
            int rotateCount = mirrorDirection switch
            {
                GridDirection.Up => 0,
                GridDirection.Right => 1,
                GridDirection.Down => 2,
                GridDirection.Left => 3,
                _ => 0
            };

            GridDirection result = side;

            for (int i = 0; i < rotateCount; i++)
            {
                result = result.RotateClockwise();
            }

            return result;
        }

        private void RefreshVisual()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());

            if (visualRoot == null)
                return;

            if (mirrorShape == MirrorShape.NormalL)
            {
                visualRoot.localRotation = Quaternion.identity;
            }
            else
            {
                visualRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }
    }
}