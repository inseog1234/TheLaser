using System.Collections;
using UnityEngine;
using TMPro;
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

        [Header("Laser Remaining UI")]
        [SerializeField] private TMP_Text laserRemainingText;
        [SerializeField] private string limitedFormat = "레이저 잔여 칸 : {0}칸";
        [SerializeField] private string unlimitedText = "레이저 잔여 칸 : 제한 없음";

        private void Awake()
        {
            EnsureReferences();
        }

        private IEnumerator Start()
        {
            yield return null;
            RefreshLaserRemainingText(null);
        }

        private void OnEnable()
        {
            EnsureReferences();

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

            if (playerGridController.IsMoving || !playerGridController.ControlsEnabled)
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

            RefreshLaserRemainingText(result);

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

        public void ClearLaser()
        {
            if (laserRenderer != null)
                laserRenderer.Clear();

            if (gridManager != null)
            {
                gridManager.ResetAllTargets();
                gridManager.ResetAllDistanceSensors();
            }

            RefreshLaserRemainingText(null);
        }

        private void EnsureReferences()
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

        public void RefreshLaserRemainingText(LaserResult result)
        {
            if (laserRemainingText == null)
                return;

            StageData stageData = gridManager != null ? gridManager.CurrentStageData : null;

            if (stageData == null || !stageData.useLaserDistanceLimit || stageData.laserMaxDistance <= 0)
            {
                laserRemainingText.text = unlimitedText;
                return;
            }

            int usedCount = result != null ? result.MaxBeamStepCount : 0;
            int remainingCount = Mathf.Max(0, stageData.laserMaxDistance - usedCount);
            laserRemainingText.text = string.Format(limitedFormat, remainingCount);
        }
    }
}
