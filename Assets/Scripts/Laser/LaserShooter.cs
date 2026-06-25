using UnityEngine;
using Core;
using Player;
using Grid;

namespace Laser
{
    public class LaserShooter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerGridController playerGridController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private LaserSimulator laserSimulator;
        [SerializeField] private LaserRenderer laserRenderer;
        [SerializeField] private GridManager gridManager;

        private void Awake()
        {
            if (playerGridController == null)
                playerGridController = FindFirstObjectByType<PlayerGridController>();

            if (inputReader == null)
                inputReader = FindFirstObjectByType<PlayerInputReader>();

            if (laserSimulator == null)
                laserSimulator = FindFirstObjectByType<LaserSimulator>();

            if (laserRenderer == null)
                laserRenderer = FindFirstObjectByType<LaserRenderer>();

            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
        }

        private void OnEnable()
        {
            if (inputReader == null)
                return;

            inputReader.LaserPressed += ShootFromPlayer;
            inputReader.MovePressed += ClearLaserOnMove;
            inputReader.RotateClockwisePressed += ClearLaser;
            inputReader.RotateCounterClockwisePressed += ClearLaser;
            inputReader.ResetPressed += ClearLaser;
        }

        private void OnDisable()
        {
            if (inputReader == null)
                return;

            inputReader.LaserPressed -= ShootFromPlayer;
            inputReader.MovePressed -= ClearLaserOnMove;
            inputReader.RotateClockwisePressed -= ClearLaser;
            inputReader.RotateCounterClockwisePressed -= ClearLaser;
            inputReader.ResetPressed -= ClearLaser;
        }

        public void ShootFromPlayer()
        {
            if (playerGridController == null || laserSimulator == null)
                return;

            Shoot(playerGridController.GridPosition, playerGridController.FacingDirection);
        }

        public void Shoot(Vector2Int startPosition, GridDirection startDirection)
        {
            if (laserRenderer != null)
                laserRenderer.Clear();

            LaserResult result = laserSimulator.Simulate(startPosition, startDirection);

            if (gridManager != null)
                gridManager.EvaluateLaserResult(result);

            if (laserRenderer != null)
                laserRenderer.Render(result);

            if (gridManager != null && gridManager.AreAllTargetsActivated())
                Debug.Log("스테이지 클리어");
            else if (result.ReachedTarget)
                Debug.Log("목표 일부 도달");
            else if (result.LoopDetected)
                Debug.Log("레이저 루프 감지");
        }

        private void ClearLaserOnMove(GridDirection direction)
        {
            ClearLaser();
        }

        private void ClearLaser()
        {
            if (laserRenderer != null)
                laserRenderer.Clear();

            if (gridManager != null)
            {
                gridManager.ResetAllTargets();
                gridManager.ResetAllDistanceSensors();
            }
        }
    }
}
