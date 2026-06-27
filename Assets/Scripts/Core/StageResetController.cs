using UnityEngine;
using Grid;
using Laser;
using Player;
using UI.InGame;

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
        [SerializeField] private InGameStageFlowController stageFlowController;

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();

            if (playerGridController == null)
                playerGridController = FindFirstObjectByType<PlayerGridController>();

            if (inputReader == null)
                inputReader = FindFirstObjectByType<PlayerInputReader>();

            if (laserRenderer == null)
                laserRenderer = FindFirstObjectByType<LaserRenderer>();

            if (turnHistoryController == null)
                turnHistoryController = FindFirstObjectByType<StageTurnHistoryController>();

            if (stageFlowController == null)
                stageFlowController = FindFirstObjectByType<InGameStageFlowController>();
        }

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

            if (stageFlowController != null)
                stageFlowController.ResetRuntimeStateAfterStageReset();
        }
    }
}