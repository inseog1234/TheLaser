using System.Collections;
using UnityEngine;
using Core;
using Grid;

namespace Player
{
    public class PlayerGridController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerDirectionView directionView;
        [SerializeField] private PlayerObjectInteractor objectInteractor;

        [Header("Move")]
        [SerializeField] private bool smoothMove = true;
        [SerializeField] private float moveDuration = 0.08f;

        [Header("Runtime")]
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private GridDirection facingDirection = GridDirection.Right;

        private bool isMoving;
        private Coroutine moveCoroutine;

        public Vector2Int GridPosition => gridPosition;
        public GridDirection FacingDirection => facingDirection;
        public bool IsMoving => isMoving;

        private void Start()
        {
            InitializeFromStageData();
            SubscribeInput();
        }

        private void OnDestroy()
        {
            UnsubscribeInput();
        }

        private void InitializeFromStageData()
        {
            if (gridManager == null || gridManager.CurrentStageData == null)
            {
                return;
            }

            StageData stageData = gridManager.CurrentStageData;

            gridPosition = stageData.playerStartPosition;
            facingDirection = stageData.playerStartDirection;

            transform.position = gridManager.GridToWorld(gridPosition);
            RefreshDirectionView();
        }

        private void SubscribeInput()
        {
            if (inputReader == null)
                return;

            inputReader.MovePressed += HandleMovePressed;
            inputReader.LaserPressed += HandleLaserPressed;
            inputReader.RotateClockwisePressed += HandleRotateClockwisePressed;
            inputReader.RotateCounterClockwisePressed += HandleRotateCounterClockwisePressed;
            inputReader.ResetPressed += HandleResetPressed;
        }

        private void UnsubscribeInput()
        {
            if (inputReader == null)
                return;

            inputReader.MovePressed -= HandleMovePressed;
            inputReader.LaserPressed -= HandleLaserPressed;
            inputReader.RotateClockwisePressed -= HandleRotateClockwisePressed;
            inputReader.RotateCounterClockwisePressed -= HandleRotateCounterClockwisePressed;
            inputReader.ResetPressed -= HandleResetPressed;
        }

        private void HandleMovePressed(GridDirection direction)
        {
            TryMove(direction);
        }

        public bool TryMove(GridDirection direction)
        {
            if (gridManager == null)
                return false;

            if (isMoving)
                return false;

            SetFacingDirection(direction);

            Vector2Int targetPosition = gridPosition + direction.ToVector();

            if (gridManager.IsWalkable(targetPosition))
            {
                SetGridPosition(targetPosition);
                return true;
            }

            if (gridManager.HasObject(targetPosition))
            {
                return TryPushForward(direction);
            }

            return false;
        }

        private bool TryPushForward(GridDirection direction)
        {
            if (objectInteractor == null)
            {
                return false;
            }

            bool pushed = objectInteractor.TryPushObject(
                gridPosition,
                direction,
                out Vector2Int newPlayerPosition
            );

            if (!pushed)
                return false;

            SetGridPosition(newPlayerPosition);
            return true;
        }

        public void SetFacingDirection(GridDirection direction)
        {
            facingDirection = direction;
            RefreshDirectionView();
        }

        public Vector2Int GetForwardPosition()
        {
            return gridPosition + facingDirection.ToVector();
        }

        public void SetGridPosition(Vector2Int newGridPosition)
        {
            gridPosition = newGridPosition;

            Vector3 targetWorldPosition = gridManager.GridToWorld(gridPosition);

            if (smoothMove)
            {
                if (moveCoroutine != null)
                {
                    StopCoroutine(moveCoroutine);
                }

                moveCoroutine = StartCoroutine(MoveRoutine(targetWorldPosition));
            }
            else
            {
                transform.position = targetWorldPosition;
            }
        }

        private IEnumerator MoveRoutine(Vector3 targetWorldPosition)
        {
            isMoving = true;

            Vector3 startWorldPosition = transform.position;
            float elapsedTime = 0f;

            while (elapsedTime < moveDuration)
            {
                elapsedTime += Time.deltaTime;

                float t = moveDuration <= 0f ? 1f : elapsedTime / moveDuration;
                transform.position = Vector3.Lerp(startWorldPosition, targetWorldPosition, t);

                yield return null;
            }

            transform.position = targetWorldPosition;
            isMoving = false;
            moveCoroutine = null;
        }

        private void RefreshDirectionView()
        {
            if (directionView != null)
            {
                directionView.SetDirection(facingDirection);
            }
        }

        private void HandleLaserPressed()
        {
        }

        private void HandleRotateClockwisePressed()
        {
            if (isMoving)
                return;

            if (objectInteractor == null)
            {
                return;
            }

            objectInteractor.TryRotateObject(gridPosition, facingDirection, true);
        }

        private void HandleRotateCounterClockwisePressed()
        {
            if (isMoving)
                return;

            if (objectInteractor == null)
            {
                return;
            }

            objectInteractor.TryRotateObject(gridPosition, facingDirection, false);
        }

        private void HandleResetPressed()
        {
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.35f);

            Vector2Int direction = facingDirection.ToVector();
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + direction);
        }
    }
}