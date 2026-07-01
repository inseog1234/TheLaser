using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace UI.InGame
{
    public partial class InGameStageFlowController
    {
        private Coroutine solutionPlaybackRoutine;
        private readonly List<StageSolutionActionData> solutionPlaybackActions = new();
        private bool isSolutionPlaybackRunning;
        private bool solutionPlaybackPaused;
        private bool solutionPlaybackStopRequested;
        private int solutionPlaybackNextActionIndex;
        private Vector2Int solutionPlaybackPausedGridPosition;
        private GridDirection solutionPlaybackPausedFacingDirection = GridDirection.Right;
        private bool previousInputEnabledBeforeSolutionPlayback = true;
        private bool previousRecordingEnabledBeforeSolutionPlayback;

        public void DebugToggleSolutionSimulation()
        {
            ToggleSolutionPlaybackSimulation(false);
        }

        private void ToggleSolutionPlaybackCheat()
        {
            ToggleSolutionPlaybackSimulation(true);
        }

        private void ToggleSolutionPlaybackSimulation(bool requireCheatFlag)
        {
            if (requireCheatFlag && !enableSolutionPlaybackCheat)
                return;

            if (GameSceneRequest.IsEditorTestPlay || GameSceneRequest.IsEditorBatchSolutionProcessing)
                return;

            if (pauseOpen || tutorialOpen || isJumpingIntoHole)
                return;

            if (isSolutionPlaybackRunning)
            {
                RequestStopSolutionPlayback();
                return;
            }

            if (solutionPlaybackPaused)
            {
                ResumeSolutionPlaybackFromPausedPoint();
                return;
            }

            StartSolutionPlaybackFromBeginning();
        }

        private void StartSolutionPlaybackFromBeginning()
        {
            if (!TryPrepareSolutionPlaybackActions(out string message))
            {
                ShowAutoSolverStatus(message, new Color(1f, 0.35f, 0.35f, 1f));
                return;
            }

            StopAutoSolverRoutine(true);
            StopSolutionPlaybackRoutine(false);

            solutionPlaybackPaused = false;
            solutionPlaybackStopRequested = false;
            solutionPlaybackNextActionIndex = 0;
            solutionPlaybackPausedGridPosition = autoSolverInitialStageData != null ? autoSolverInitialStageData.playerStartPosition : currentStage != null ? currentStage.playerStartPosition : Vector2Int.zero;
            solutionPlaybackPausedFacingDirection = autoSolverInitialStageData != null ? autoSolverInitialStageData.playerStartDirection : currentStage != null ? currentStage.playerStartDirection : GridDirection.Right;

            solutionPlaybackRoutine = StartCoroutine(SolutionPlaybackRoutine(true));
        }

        private void ResumeSolutionPlaybackFromPausedPoint()
        {
            if (solutionPlaybackActions.Count == 0 && !TryPrepareSolutionPlaybackActions(out string message))
            {
                ShowAutoSolverStatus(message, new Color(1f, 0.35f, 0.35f, 1f));
                solutionPlaybackPaused = false;
                return;
            }

            StopAutoSolverRoutine(true);
            StopSolutionPlaybackRoutine(false);
            solutionPlaybackStopRequested = false;
            solutionPlaybackRoutine = StartCoroutine(SolutionPlaybackRoutine(false));
        }

        private bool TryPrepareSolutionPlaybackActions(out string message)
        {
            message = string.Empty;
            StageData source = autoSolverInitialStageData != null ? autoSolverInitialStageData : currentStage;

            if (source == null)
            {
                message = "현재 스테이지 데이터가 없습니다.";
                return false;
            }

            if (source.solutionActions == null || source.solutionActions.Count == 0)
            {
                message = "저장된 답안지가 없습니다.";
                return false;
            }

            solutionPlaybackActions.Clear();
            for (int i = 0; i < source.solutionActions.Count; i++)
            {
                if (source.solutionActions[i] != null)
                    solutionPlaybackActions.Add(source.solutionActions[i].Clone());
            }

            if (solutionPlaybackActions.Count == 0)
            {
                message = "저장된 답안지가 비어 있습니다.";
                return false;
            }

            return true;
        }

        private void RequestStopSolutionPlayback()
        {
            solutionPlaybackStopRequested = true;
            ShowAutoSolverStatus($"답안 시뮬레이션 정지 중... {autoSolverSpeedMultiplier}배속", new Color(1f, 0.85f, 0.35f, 1f));
        }

        private IEnumerator SolutionPlaybackRoutine(bool resetStage)
        {
            isSolutionPlaybackRunning = true;
            solutionPlaybackStopRequested = false;
            previousInputEnabledBeforeSolutionPlayback = inputReader == null || inputReader.InputEnabled;
            previousRecordingEnabledBeforeSolutionPlayback = isRecordingEditorSolution;

            if (inputReader != null)
                inputReader.InputEnabled = false;

            if (playerController != null)
                playerController.SetControlsEnabled(true);

            isRecordingEditorSolution = false;

            if (resetStage)
            {
                ResetStageForSolutionPlayback();
                solutionPlaybackNextActionIndex = 0;
            }
            else
            {
                ShowAutoSolverStatus($"멈춘 위치로 복귀 중... {autoSolverSpeedMultiplier}배속", new Color(0.55f, 0.85f, 1f, 1f));
                yield return MovePlayerToSolutionPlaybackPausedPoint();
            }

            int totalCount = solutionPlaybackActions.Count;
            if (solutionPlaybackNextActionIndex < 0)
                solutionPlaybackNextActionIndex = 0;

            if (solutionPlaybackNextActionIndex > totalCount)
                solutionPlaybackNextActionIndex = totalCount;

            ShowAutoSolverStatus($"답안 시뮬레이션 시작 {autoSolverSpeedMultiplier}배속  {solutionPlaybackNextActionIndex}/{totalCount}", new Color(0.55f, 0.85f, 1f, 1f));
            yield return WaitSolutionPlaybackDelayOrStop();

            for (int i = solutionPlaybackNextActionIndex; i < totalCount; i++)
            {
                if (stageSolved)
                    break;

                if (solutionPlaybackStopRequested)
                {
                    PauseSolutionPlayback(i);
                    yield break;
                }

                StageSolutionActionData action = solutionPlaybackActions[i];
                ApplySolutionPlaybackAction(action);

                while (playerController != null && playerController.IsMoving)
                    yield return null;

                int completedCount = i + 1;
                SaveSolutionPlaybackPausePoint(completedCount);
                ShowAutoSolverStatus($"답안 시뮬레이션 {autoSolverSpeedMultiplier}배속  {completedCount}/{totalCount}", new Color(0.55f, 0.85f, 1f, 1f));

                if (solutionPlaybackStopRequested)
                {
                    PauseSolutionPlayback(completedCount);
                    yield break;
                }

                yield return WaitSolutionPlaybackDelayOrStop();

                if (solutionPlaybackStopRequested)
                {
                    PauseSolutionPlayback(completedCount);
                    yield break;
                }
            }

            if (!stageSolved && laserShooter != null)
            {
                ShowAutoSolverStatus($"답안 시뮬레이션 {autoSolverSpeedMultiplier}배속  마지막 레이저 발사", new Color(0.55f, 0.85f, 1f, 1f));
                laserShooter.ShootFromPlayer();
                SaveSolutionPlaybackPausePoint(totalCount);
            }

            FinishSolutionPlayback(stageSolved ? "답안 시뮬레이션 완료" : "답안 시뮬레이션 종료", false);
        }

        private void ResetStageForSolutionPlayback()
        {
            StageData resetSource = autoSolverInitialStageData != null ? autoSolverInitialStageData : currentStage;
            if (resetSource == null || gridManager == null)
                return;

            if (laserShooter != null)
                laserShooter.ClearLaser();

            stageSolved = false;
            clearHoleActivated = false;
            isJumpingIntoHole = false;

            if (stageSolvedPresentationRoutine != null)
            {
                StopCoroutine(stageSolvedPresentationRoutine);
                stageSolvedPresentationRoutine = null;
            }

            gridManager.LoadStage(resetSource.Clone());
            currentStage = gridManager.CurrentStageData;

            if (playerController != null)
                playerController.ResetToStageStartImmediate();

            if (turnHistoryController != null)
            {
                turnHistoryController.ClearHistory();
                turnHistoryController.SetTurnCountingEnabled(true);
            }

            UpdateMoveLimitText();
        }

        private IEnumerator MovePlayerToSolutionPlaybackPausedPoint()
        {
            if (playerController == null)
                yield break;

            if (laserShooter != null)
                laserShooter.ClearLaser();

            float duration = Mathf.Max(0f, solutionPlaybackReturnDuration / Mathf.Max(1, autoSolverSpeedMultiplier));
            yield return playerController.MoveToRuntimeStateRoutine(solutionPlaybackPausedGridPosition, solutionPlaybackPausedFacingDirection, duration);
        }

        private void ApplySolutionPlaybackAction(StageSolutionActionData action)
        {
            ApplyAutoSolverAction(action);
        }

        private IEnumerator WaitSolutionPlaybackDelayOrStop()
        {
            float waitTime = autoSolverSpeedMultiplier <= 0 ? autoSolverActionDelay : autoSolverActionDelay / autoSolverSpeedMultiplier;
            float elapsed = 0f;
            while (elapsed < waitTime)
            {
                if (solutionPlaybackStopRequested)
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void SaveSolutionPlaybackPausePoint(int nextActionIndex)
        {
            solutionPlaybackNextActionIndex = Mathf.Clamp(nextActionIndex, 0, solutionPlaybackActions.Count);

            if (playerController == null)
                return;

            solutionPlaybackPausedGridPosition = playerController.GridPosition;
            solutionPlaybackPausedFacingDirection = playerController.FacingDirection;
        }

        private void PauseSolutionPlayback(int nextActionIndex)
        {
            SaveSolutionPlaybackPausePoint(nextActionIndex);
            solutionPlaybackPaused = true;
            isSolutionPlaybackRunning = false;
            solutionPlaybackStopRequested = false;
            solutionPlaybackRoutine = null;
            RestoreInputAfterSolutionPlayback();
            ShowAutoSolverStatus($"답안 시뮬레이션 정지  {solutionPlaybackNextActionIndex}/{solutionPlaybackActions.Count}  버튼: 재개", new Color(1f, 0.85f, 0.35f, 1f));
        }

        private void FinishSolutionPlayback(string message, bool clearPauseState)
        {
            isSolutionPlaybackRunning = false;
            solutionPlaybackStopRequested = false;
            solutionPlaybackRoutine = null;

            if (clearPauseState)
                solutionPlaybackPaused = false;

            if (!stageSolved)
                RestoreInputAfterSolutionPlayback();
            else if (inputReader != null)
                inputReader.InputEnabled = previousInputEnabledBeforeSolutionPlayback;

            isRecordingEditorSolution = previousRecordingEnabledBeforeSolutionPlayback;
            ShowAutoSolverStatus($"{message}  {autoSolverSpeedMultiplier}배속", stageSolved ? new Color(0.55f, 1f, 0.65f, 1f) : new Color(0.55f, 0.85f, 1f, 1f));
        }

        private void RestoreInputAfterSolutionPlayback()
        {
            if (inputReader != null)
                inputReader.InputEnabled = previousInputEnabledBeforeSolutionPlayback;

            if (playerController != null && !stageSolved && !pauseOpen && !tutorialOpen && !isJumpingIntoHole)
                playerController.SetControlsEnabled(true);

            isRecordingEditorSolution = previousRecordingEnabledBeforeSolutionPlayback;
        }

        private void StopSolutionPlaybackRoutine(bool restoreInput)
        {
            if (solutionPlaybackRoutine != null)
            {
                StopCoroutine(solutionPlaybackRoutine);
                solutionPlaybackRoutine = null;
            }

            isSolutionPlaybackRunning = false;
            solutionPlaybackStopRequested = false;

            if (restoreInput)
                RestoreInputAfterSolutionPlayback();
        }
    }
}
