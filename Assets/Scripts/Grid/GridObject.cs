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
        [SerializeField] private MirrorShape mirrorShape = MirrorShape.Slash;

        public PuzzleObjectType ObjectType => objectType;
        public ManipulationType ManipulationType => manipulationType;
        public Vector2Int GridPosition => gridPosition;
        public GridDirection Direction => direction;
        public MirrorShape MirrorShape => mirrorShape;

        public bool CanPush => manipulationType.CanPush();
        public bool CanRotate => manipulationType.CanRotate();

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

        public void SetMirrorShape(MirrorShape newShape)
        {
            mirrorShape = newShape;
            RefreshVisual();
        }

        public void RotateClockwise()
        {
            direction = direction.RotateClockwise();

            if (objectType == PuzzleObjectType.Mirror)
            {
                mirrorShape = mirrorShape.Rotate90();
            }

            RefreshVisual();
        }

        public void RotateCounterClockwise()
        {
            direction = direction.RotateCounterClockwise();

            if (objectType == PuzzleObjectType.Mirror)
            {
                mirrorShape = mirrorShape.Rotate90();
            }

            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (objectType == PuzzleObjectType.Mirror)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, mirrorShape.VisualZAngle());
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());
            }
        }
    }
}