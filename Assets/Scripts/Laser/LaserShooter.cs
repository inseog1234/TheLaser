using UnityEngine;
using Core;
using Player;

namespace Laser
{
    public class LaserShooter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerGridController playerGridController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private LaserSimulator laserSimulator;
        [SerializeField] private LaserRenderer laserRenderer;

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

            Shoot(
                playerGridController.GridPosition,
                playerGridController.FacingDirection
            );
        }

        public void Shoot(Vector2Int startPosition, GridDirection startDirection)
        {
            LaserResult result = laserSimulator.Simulate(startPosition, startDirection);

            if (laserRenderer != null)
            {
                laserRenderer.Render(result);
            }

            if (result.ReachedTarget)
            {
                Debug.Log("스테이지 클리어");
            }
            else if (result.LoopDetected)
            {
                // 루프 감지 됨
            }
            else
            {
                // 도달 실패
            }
        }

        private void ClearLaserOnMove(GridDirection direction)
        {
            ClearLaser();
        }

        private void ClearLaser()
        {
            if (laserRenderer != null)
            {
                laserRenderer.Clear();
            }
        }
    }
}