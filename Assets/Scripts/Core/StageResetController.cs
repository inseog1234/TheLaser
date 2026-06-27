using UnityEngine;
using Grid;
using Laser;
using Player;

namespace Core
{
    public class StageResetController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PlayerGridController playerGridController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private LaserRenderer laserRenderer;
        [SerializeField] private StageTurnHistoryController turnHistoryController;

        private void OnEnable()
        {
            if (inputReader != null)
                inputReader.ResetPressed += ResetStage;
        }

        private void OnDisable()
        {
            if (inputReader != null)
                inputReader.ResetPressed -= ResetStage;
        }

        public void ResetStage()
        {
            if (laserRenderer != null)
                laserRenderer.Clear();

            if (turnHistoryController != null)
                turnHistoryController.ClearHistory();

            if (gridManager != null && gridManager.CurrentStageData != null)
                gridManager.LoadStage(gridManager.CurrentStageData);

            if (playerGridController != null)
                playerGridController.ResetToStageStartImmediate();
        }
    }
}