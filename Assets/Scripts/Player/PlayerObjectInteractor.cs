using UnityEngine;
using Core;
using Grid;

namespace Player
{
    public class PlayerObjectInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;

        public bool TryPushObject(Vector2Int playerPosition, GridDirection pushDirection, out Vector2Int newPlayerPosition)
        {
            newPlayerPosition = playerPosition;

            if (gridManager == null)
                return false;

            Vector2Int objectPosition = playerPosition + pushDirection.ToVector();
            GridObject targetObject = gridManager.GetObjectAt(objectPosition);

            if (targetObject == null)
            {
                return false;
            }

            if (!targetObject.CanPush)
            {
                return false;
            }

            Vector2Int objectNextPosition = objectPosition + pushDirection.ToVector();

            bool pushed = gridManager.TryMoveObject(targetObject, objectNextPosition);

            if (!pushed)
            {
                return false;
            }

            newPlayerPosition = objectPosition;

            return true;
        }

        public bool TryRotateObject(Vector2Int playerPosition, GridDirection facingDirection, bool clockwise)
        {
            if (gridManager == null)
                return false;

            Vector2Int objectPosition = playerPosition + facingDirection.ToVector();
            GridObject targetObject = gridManager.GetObjectAt(objectPosition);

            if (targetObject == null)
            {
                return false;
            }

            if (!targetObject.CanRotate)
            {
                return false;
            }

            if (clockwise)
            {
                targetObject.RotateClockwise();
            }
            else
            {
                targetObject.RotateCounterClockwise();;
            }

            return true;
        }
    }
}