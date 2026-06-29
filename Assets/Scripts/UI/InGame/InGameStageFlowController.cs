using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Audio;
using Core;
using Grid;
using Laser;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.InGame
{
    public class InGameStageFlowController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private string gameSceneName = "Game";

        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PlayerGridController playerController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private LaserShooter laserShooter;
        [SerializeField] private SmoothCameraFollow smoothCameraFollow;
        [SerializeField] private StageTurnHistoryController turnHistoryController;
        [SerializeField] private FmodRuntimeAudio audioController;

        [Header("InGame UI")]
        [SerializeField] private TMP_Text moveLimitText;
        [SerializeField] private GameObject moveLimitRoot;

        [Header("Stage Clear Presentation")]
        [SerializeField] private float stageClearLaserPathViewDuration = 1f;
        [SerializeField] private float clearHoleFocusHoldDuration = 2f;
        [SerializeField] private bool lockPlayerDuringStageClearLaserPath = true;
        [SerializeField] private bool shakeCameraOnStageSolved = true;
        [SerializeField] private float stageSolvedShakeDuration = 0.18f;
        [SerializeField] private float stageSolvedShakeStrength = 0.24f;
        [SerializeField] private float stageSolvedShakeFrequency = 55f;

        [Header("Editor Auto Solver")]
        [SerializeField] private bool enableEditorAutoSolver = true;
        [SerializeField] private float autoSolverActionDelay = 0.3f;
        [SerializeField] private int autoSolverMaxNodeCount = 120000;
        [SerializeField] private int autoSolverMaxActionCount = 80;
        [SerializeField] private bool autoSolverFastApproximateSearch = true;
        [SerializeField] private StageAutoSolverSearchMode autoSolverSearchMode = StageAutoSolverSearchMode.WeightedAStar;
        [SerializeField, Range(1f, 6f)] private float autoSolverHeuristicWeight = 3f;
        [SerializeField] private int autoSolverBeamWidth = 1024;

        [Header("Solution Playback Cheat")]
        [SerializeField] private bool enableSolutionPlaybackCheat = true;
        [SerializeField] private float solutionPlaybackReturnDuration = 0.18f;

        private TMP_FontAsset font;
        private Sprite whiteSprite;
        private Canvas canvas;
        private RectTransform root;
        private RectTransform pausePopup;
        private RectTransform testPausePopup;
        private RectTransform settingsPopup;
        private RectTransform tutorialPopup;
        private TMP_Text introText;
        private TMP_Text holeInteractText;
        private RectTransform holeVisual;
        private StageData currentStage;
        private readonly List<StageSolutionActionData> editorRecordedSolutionActions = new();
        private readonly List<List<StageSolutionActionData>> editorRecordedSolutionUndoSnapshots = new();
        private readonly List<List<StageSolutionActionData>> editorRecordedSolutionRedoSnapshots = new();
        private StageData autoSolverInitialStageData;
        private TMP_Text autoSolverStatusText;
        private Coroutine autoSolverRoutine;
        private int autoSolverSpeedMultiplier = 1;
        private bool isAutoSolverRunning;
        private bool previousInputEnabledBeforeAutoSolver = true;
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
        private bool isRecordingEditorSolution;
        private bool stageSolved;
        private bool pauseOpen;
        private bool tutorialOpen;
        private bool isJumpingIntoHole;
        private bool clearHoleActivated;
        private Coroutine stageSolvedPresentationRoutine;
        private int tutorialPageIndex;
        private int lastPauseInputFrame = -1;
        private int lastInteractInputFrame = -1;

        private void Awake()
        {
            EnsureReferences();
            EnsureEventSystem();
            font = Resources.Load<TMP_FontAsset>("Font/TMP/PF스타더스트 3");
            whiteSprite = CreateWhiteSprite();
            BuildUI();
        }

        private IEnumerator Start()
        {
            yield return null;
            EnsureReferences();
            LoadRequestedStageIfNeeded();
            currentStage = gridManager != null ? gridManager.CurrentStageData : null;
            autoSolverInitialStageData = currentStage != null ? currentStage.Clone() : null;

            if (audioController != null)
                audioController.ApplySavedVolumes();

            if (laserShooter != null)
                laserShooter.RefreshLaserRemainingText(null);

            if (gridManager != null)
                gridManager.StageSolved += HandleStageSolved;

            if (turnHistoryController != null)
            {
                turnHistoryController.SetTurnCountingEnabled(true);
                turnHistoryController.ClearHistory();
                turnHistoryController.TurnCountChanged += UpdateMoveLimitText;
                turnHistoryController.UndoApplied += HandleRecordedSolutionUndoApplied;
                turnHistoryController.RedoApplied += HandleRecordedSolutionRedoApplied;
                turnHistoryController.HistoryCleared += HandleRecordedSolutionHistoryCleared;
            }

            UpdateMoveLimitText();

            if (inputReader != null)
            {
                inputReader.InteractPressed += HandleInteractPressed;
                inputReader.PausePressed += HandlePausePressed;
            }

            if (playerController != null)
                playerController.SolutionActionPerformed += HandleSolutionActionPerformed;

            if (laserShooter != null)
                laserShooter.LaserFiredFromPlayer += HandleLaserFiredFromPlayer;

            ClearRecordedSolutionTimeline();
            isRecordingEditorSolution = GameSceneRequest.IsEditorTestPlay || GameSceneRequest.IsEditorBatchSolutionProcessing;

            if (audioController != null && currentStage != null)
                audioController.PlayBgm(currentStage.bgmEventPath);

            RefreshIntroText();
            yield return PlayPlayerSpawnIntro();

            if (GameSceneRequest.IsEditorBatchSolutionProcessing)
            {
                StartBatchAutoSolver();
                yield break;
            }

            if (currentStage != null && currentStage.hasTutorial && currentStage.tutorialPages != null && currentStage.tutorialPages.Count > 0)
                ShowTutorialPopup();
        }

        private void OnDestroy()
        {
            if (gridManager != null)
                gridManager.StageSolved -= HandleStageSolved;

            if (inputReader != null)
            {
                inputReader.InteractPressed -= HandleInteractPressed;
                inputReader.PausePressed -= HandlePausePressed;
            }

            if (turnHistoryController != null)
            {
                turnHistoryController.TurnCountChanged -= UpdateMoveLimitText;
                turnHistoryController.UndoApplied -= HandleRecordedSolutionUndoApplied;
                turnHistoryController.RedoApplied -= HandleRecordedSolutionRedoApplied;
                turnHistoryController.HistoryCleared -= HandleRecordedSolutionHistoryCleared;
            }

            if (playerController != null)
                playerController.SolutionActionPerformed -= HandleSolutionActionPerformed;

            if (laserShooter != null)
                laserShooter.LaserFiredFromPlayer -= HandleLaserFiredFromPlayer;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                HandlePausePressed();

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                HandleInteractPressed();

            HandleEditorAutoSolverHotkeys();

            UpdateHoleInteractIcon();
            UpdateMoveLimitText();
        }

        private void EnsureReferences()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (playerController == null) playerController = FindFirstObjectByType<PlayerGridController>();
            if (inputReader == null) inputReader = FindFirstObjectByType<PlayerInputReader>();
            if (laserShooter == null) laserShooter = FindFirstObjectByType<LaserShooter>();
            if (smoothCameraFollow == null) smoothCameraFollow = FindFirstObjectByType<SmoothCameraFollow>();
            if (turnHistoryController == null) turnHistoryController = FindFirstObjectByType<StageTurnHistoryController>();
            if (audioController == null)
            {
                audioController = FmodRuntimeAudio.Instance;

                if (audioController == null)
                    audioController = FindFirstObjectByType<FmodRuntimeAudio>();

                if (audioController == null)
                    audioController = gameObject.AddComponent<FmodRuntimeAudio>();
            }
        }

        private void LoadRequestedStageIfNeeded()
        {
            if (!GameSceneRequest.HasRequest || gridManager == null)
                return;

            if (GameSceneRequest.IsEditorBatchSolutionProcessing)
            {
                string batchPath = GameSceneRequest.CurrentBatchSolutionFilePath;
                if (!string.IsNullOrWhiteSpace(batchPath))
                    gridManager.LoadStageFromFile(batchPath);

                if (playerController != null)
                    playerController.ResetToStageStartImmediate();
                return;
            }

            if (GameSceneRequest.IsEditorTestPlay && GameSceneRequest.HasEditorTestStageData)
            {
                gridManager.LoadStage(GameSceneRequest.EditorTestStageData.Clone());
                if (playerController != null)
                    playerController.ResetToStageStartImmediate();
                return;
            }

            if (string.IsNullOrWhiteSpace(GameSceneRequest.StageFilePath))
                return;

            if (StageFilePaths.IsBuiltInResourcePath(GameSceneRequest.StageFilePath))
            {
                if (BuiltInStageLoader.TryLoad(GameSceneRequest.StageFilePath, out StageData builtInStageData))
                    gridManager.LoadStage(builtInStageData);
            }
            else
            {
                gridManager.LoadStageFromFile(GameSceneRequest.StageFilePath);
            }

            if (playerController != null)
                playerController.ResetToStageStartImmediate();
        }

        private void HandleEditorAutoSolverHotkeys()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                SetAutoSolverSpeed(1);

            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                SetAutoSolverSpeed(2);

            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                SetAutoSolverSpeed(3);

            if (Keyboard.current.pKey.wasPressedThisFrame)
                HandlePHotkey();
        }

        private void HandlePHotkey()
        {
            if (GameSceneRequest.IsEditorBatchSolutionProcessing)
                return;

            if (GameSceneRequest.IsEditorTestPlay)
            {
                ToggleEditorTestAutoSolver();
                return;
            }

            ToggleSolutionPlaybackCheat();
        }

        private void SetAutoSolverSpeed(int multiplier)
        {
            autoSolverSpeedMultiplier = Mathf.Clamp(multiplier, 1, 3);

            if (isSolutionPlaybackRunning)
                ShowAutoSolverStatus($"답안 시뮬레이션 {autoSolverSpeedMultiplier}배속  {solutionPlaybackNextActionIndex}/{solutionPlaybackActions.Count}", new Color(0.55f, 0.85f, 1f, 1f));
            else if (solutionPlaybackPaused)
                ShowAutoSolverStatus($"답안 시뮬레이션 정지  {autoSolverSpeedMultiplier}배속  {solutionPlaybackNextActionIndex}/{solutionPlaybackActions.Count}  P: 재개", new Color(1f, 0.85f, 0.35f, 1f));
            else if (isAutoSolverRunning)
                ShowAutoSolverStatus($"AI 자동 풀이 {autoSolverSpeedMultiplier}배속", new Color(0.55f, 0.85f, 1f, 1f));
            else
                ShowAutoSolverStatus($"시뮬레이션 배속 {autoSolverSpeedMultiplier}배속", new Color(0.55f, 0.85f, 1f, 1f));
        }

        private void ToggleSolutionPlaybackCheat()
        {
            if (!enableSolutionPlaybackCheat)
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

        private void ToggleEditorTestAutoSolver()
        {
            if (!enableEditorAutoSolver)
                return;

            if (pauseOpen || tutorialOpen || isJumpingIntoHole)
                return;

            if (isAutoSolverRunning)
            {
                StopAutoSolverRoutine(true);
                ShowAutoSolverStatus($"AI 자동 탐색 정지  {autoSolverSpeedMultiplier}배속  P: 다시 시작", new Color(1f, 0.85f, 0.35f, 1f));
                return;
            }

            if (isSolutionPlaybackRunning || solutionPlaybackPaused)
            {
                StopSolutionPlaybackRoutine(true);
                solutionPlaybackPaused = false;
            }

            StartEditorAutoSolver();
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
            ShowAutoSolverStatus($"답안 시뮬레이션 정지  {solutionPlaybackNextActionIndex}/{solutionPlaybackActions.Count}  P: 재개", new Color(1f, 0.85f, 0.35f, 1f));
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

        private void StartEditorAutoSolver()
        {
            if (isAutoSolverRunning)
                return;

            if (!GameSceneRequest.IsEditorTestPlay)
                return;

            if (autoSolverRoutine != null)
                StopCoroutine(autoSolverRoutine);

            autoSolverRoutine = StartCoroutine(EditorAutoSolverRoutine(false));
        }

        private void StartBatchAutoSolver()
        {
            if (isAutoSolverRunning)
                return;

            if (!GameSceneRequest.IsEditorBatchSolutionProcessing)
                return;

            if (autoSolverRoutine != null)
                StopCoroutine(autoSolverRoutine);

            autoSolverRoutine = StartCoroutine(EditorAutoSolverRoutine(true));
        }

        private IEnumerator EditorAutoSolverRoutine()
        {
            yield return EditorAutoSolverRoutine(false);
        }

        private string GetAutoSolverSearchLabel()
        {
            if (!autoSolverFastApproximateSearch)
                return "완전 탐색 중";

            switch (autoSolverSearchMode)
            {
                case StageAutoSolverSearchMode.BalancedAStar:
                    return "균형 A* 탐색 중";
                case StageAutoSolverSearchMode.BeamSearch:
                    return "Beam Search 탐색 중";
                case StageAutoSolverSearchMode.BreadthFirst:
                    return "완전 탐색 중";
                case StageAutoSolverSearchMode.WeightedAStar:
                default:
                    return "Weighted A* 탐색 중";
            }
        }

        private IEnumerator EditorAutoSolverRoutine(bool batchMode)
        {
            isAutoSolverRunning = true;
            previousInputEnabledBeforeAutoSolver = inputReader == null || inputReader.InputEnabled;

            if (inputReader != null)
                inputReader.InputEnabled = false;

            if (playerController != null)
                playerController.SetControlsEnabled(false);

            string searchLabel = GetAutoSolverSearchLabel();
            ShowAutoSolverStatus($"{searchLabel}... {autoSolverSpeedMultiplier}배속  노드 0/{autoSolverMaxNodeCount}", new Color(0.55f, 0.85f, 1f, 1f));
            yield return null;

            StageData solveTarget = autoSolverInitialStageData != null ? autoSolverInitialStageData.Clone() : currentStage != null ? currentStage.Clone() : null;
            bool solved = false;
            List<StageSolutionActionData> solutionActions = new List<StageSolutionActionData>();
            string solveMessage = string.Empty;
            int finalNodeCount = 0;

            yield return StageAutoSolver.SolveCoroutine(
                solveTarget,
                (nodeCount, nodeLimit) =>
                {
                    finalNodeCount = nodeCount;
                    ShowAutoSolverStatus($"{searchLabel}... {autoSolverSpeedMultiplier}배속  노드 {nodeCount}/{nodeLimit}", new Color(0.55f, 0.85f, 1f, 1f));
                },
                (result, actions, message, nodeCount) =>
                {
                    solved = result;
                    solutionActions = actions ?? new List<StageSolutionActionData>();
                    solveMessage = message;
                    finalNodeCount = nodeCount;
                },
                autoSolverMaxNodeCount,
                autoSolverMaxActionCount,
                256,
                autoSolverFastApproximateSearch,
                autoSolverBeamWidth,
                autoSolverSearchMode,
                autoSolverHeuristicWeight);

            if (!solved)
            {
                ShowAutoSolverStatus(solveMessage, new Color(1f, 0.35f, 0.35f, 1f));
                RestoreInputAfterAutoSolver();
                isAutoSolverRunning = false;
                autoSolverRoutine = null;

                if (batchMode)
                {
                    GameSceneRequest.ReportCurrentBatchSolutionResult(false, solveMessage);
                    yield return new WaitForSeconds(0.8f);
                    LoadNextBatchStageOrReturnEditor(false);
                }

                yield break;
            }

            ShowAutoSolverStatus($"AI 풀이 발견  노드 {finalNodeCount}/{autoSolverMaxNodeCount}  /  {solutionActions.Count} 행동", new Color(0.55f, 0.85f, 1f, 1f));
            yield return new WaitForSeconds(0.2f);
            ShowAutoSolverStatus($"AI 풀이 실행 중... {autoSolverSpeedMultiplier}배속 / {solutionActions.Count} 행동", new Color(0.55f, 0.85f, 1f, 1f));
            ResetStageForAutoSolverPlayback(solveTarget);

            if (playerController != null)
                playerController.SetControlsEnabled(true);

            for (int i = 0; i < solutionActions.Count; i++)
            {
                if (stageSolved)
                    break;

                StageSolutionActionData action = solutionActions[i];
                ApplyAutoSolverAction(action);

                while (playerController != null && playerController.IsMoving)
                    yield return null;

                ShowAutoSolverStatus($"AI 자동 풀이 {autoSolverSpeedMultiplier}배속  {i + 1}/{solutionActions.Count}", new Color(0.55f, 0.85f, 1f, 1f));
                float waitTime = autoSolverSpeedMultiplier <= 0 ? autoSolverActionDelay : autoSolverActionDelay / autoSolverSpeedMultiplier;
                yield return new WaitForSeconds(waitTime);
            }

            if (!stageSolved && laserShooter != null)
            {
                ShowAutoSolverStatus($"AI 자동 풀이 {autoSolverSpeedMultiplier}배속  레이저 발사", new Color(0.55f, 0.85f, 1f, 1f));
                laserShooter.ShootFromPlayer();
            }

            if (batchMode)
            {
                yield return BatchAutoSolverFinishRoutine();
                yield break;
            }

            RestoreInputAfterAutoSolver();
            isAutoSolverRunning = false;
            autoSolverRoutine = null;
        }

        private IEnumerator BatchAutoSolverFinishRoutine()
        {
            float waitSolved = 0f;
            while (!stageSolved && waitSolved < 3f)
            {
                waitSolved += Time.deltaTime;
                yield return null;
            }

            if (!stageSolved)
            {
                string message = "AI가 레이저를 발사했지만 도착지에 닿지 못했습니다.";
                ShowAutoSolverStatus(message, new Color(1f, 0.35f, 0.35f, 1f));
                GameSceneRequest.ReportCurrentBatchSolutionResult(false, message);
                RestoreInputAfterAutoSolver();
                isAutoSolverRunning = false;
                autoSolverRoutine = null;
                yield return new WaitForSeconds(0.8f);
                LoadNextBatchStageOrReturnEditor(false);
                yield break;
            }

            float waitHole = 0f;
            while (!clearHoleActivated && waitHole < 5f)
            {
                waitHole += Time.deltaTime;
                yield return null;
            }

            if (!clearHoleActivated)
            {
                string message = "클리어 후 구멍이 생성되지 않았습니다.";
                ShowAutoSolverStatus(message, new Color(1f, 0.35f, 0.35f, 1f));
                GameSceneRequest.ReportCurrentBatchSolutionResult(false, message);
                RestoreInputAfterAutoSolver();
                isAutoSolverRunning = false;
                autoSolverRoutine = null;
                yield return new WaitForSeconds(0.8f);
                LoadNextBatchStageOrReturnEditor(false);
                yield break;
            }

            yield return MovePlayerToHoleAndEnterRoutine();
        }

        private IEnumerator MovePlayerToHoleAndEnterRoutine()
        {
            if (playerController == null || gridManager == null)
                yield break;

            playerController.SetControlsEnabled(true);

            if (laserShooter != null)
                laserShooter.ClearLaser();

            if (!IsPlayerAdjacentToClearHole())
            {
                if (!TryFindPathToClearHoleAdjacent(out List<GridDirection> path))
                {
                    string message = "구멍까지 이동할 경로를 찾지 못했습니다.";
                    ShowAutoSolverStatus(message, new Color(1f, 0.35f, 0.35f, 1f));
                    GameSceneRequest.ReportCurrentBatchSolutionResult(false, message);
                    RestoreInputAfterAutoSolver();
                    isAutoSolverRunning = false;
                    autoSolverRoutine = null;
                    yield return new WaitForSeconds(0.8f);
                    LoadNextBatchStageOrReturnEditor(false);
                    yield break;
                }

                for (int i = 0; i < path.Count; i++)
                {
                    if (laserShooter != null)
                        laserShooter.ClearLaser();

                    playerController.TryMove(path[i]);
                    while (playerController != null && playerController.IsMoving)
                        yield return null;

                    ShowAutoSolverStatus($"구멍으로 이동 중... {i + 1}/{path.Count}", new Color(0.55f, 0.85f, 1f, 1f));
                    float waitTime = autoSolverSpeedMultiplier <= 0 ? autoSolverActionDelay : autoSolverActionDelay / autoSolverSpeedMultiplier;
                    yield return new WaitForSeconds(waitTime);
                }
            }

            RestoreInputAfterAutoSolver();
            isAutoSolverRunning = false;
            autoSolverRoutine = null;

            if (IsPlayerAdjacentToClearHole())
                StartCoroutine(JumpIntoHoleRoutine());
            else
                LoadNextBatchStageOrReturnEditor(false);
        }

        private bool IsPlayerAdjacentToClearHole()
        {
            if (playerController == null || gridManager == null)
                return false;

            Vector2Int hole = gridManager.ClearHolePosition;
            return Mathf.Abs(playerController.GridPosition.x - hole.x) + Mathf.Abs(playerController.GridPosition.y - hole.y) == 1;
        }

        private bool TryFindPathToClearHoleAdjacent(out List<GridDirection> path)
        {
            path = new List<GridDirection>();

            if (playerController == null || gridManager == null)
                return false;

            Vector2Int start = playerController.GridPosition;
            Vector2Int hole = gridManager.ClearHolePosition;
            GridDirection[] directions = { GridDirection.Up, GridDirection.Right, GridDirection.Down, GridDirection.Left };
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, GridDirection> parentDirection = new Dictionary<Vector2Int, GridDirection>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue(start);
            visited.Add(start);

            Vector2Int found = start;
            bool hasFound = IsAdjacent(start, hole);

            while (queue.Count > 0 && !hasFound)
            {
                Vector2Int current = queue.Dequeue();

                for (int i = 0; i < directions.Length; i++)
                {
                    GridDirection direction = directions[i];
                    Vector2Int next = current + direction.ToVector();

                    if (visited.Contains(next))
                        continue;

                    if (!gridManager.IsInside(next))
                        continue;

                    if (!gridManager.IsWalkable(next))
                        continue;

                    visited.Add(next);
                    parent[next] = current;
                    parentDirection[next] = direction;

                    if (IsAdjacent(next, hole))
                    {
                        found = next;
                        hasFound = true;
                        break;
                    }

                    queue.Enqueue(next);
                }
            }

            if (!hasFound)
                return false;

            List<GridDirection> reversed = new List<GridDirection>();
            Vector2Int cursor = found;
            while (cursor != start)
            {
                GridDirection direction = parentDirection[cursor];
                reversed.Add(direction);
                cursor = parent[cursor];
            }

            reversed.Reverse();
            path = reversed;
            return true;
        }

        private bool IsAdjacent(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        private void ResetStageForAutoSolverPlayback(StageData stageData)
        {
            if (stageData == null || gridManager == null)
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

            gridManager.LoadStage(stageData.Clone());
            currentStage = gridManager.CurrentStageData;
            ClearRecordedSolutionTimeline();
            isRecordingEditorSolution = GameSceneRequest.IsEditorTestPlay || GameSceneRequest.IsEditorBatchSolutionProcessing;

            if (playerController != null)
                playerController.ResetToStageStartImmediate();

            if (turnHistoryController != null)
            {
                turnHistoryController.ClearHistory();
                turnHistoryController.SetTurnCountingEnabled(true);
            }

            UpdateMoveLimitText();
        }

        private void ApplyAutoSolverAction(StageSolutionActionData action)
        {
            if (action == null || playerController == null)
                return;

            if (laserShooter != null)
                laserShooter.ClearLaser();

            switch (action.actionType)
            {
                case StageSolutionActionType.FireLaser:
                    laserShooter?.ShootFromPlayer();
                    break;

                case StageSolutionActionType.RotateClockwise:
                    playerController.TryRotateForwardObject(true);
                    break;

                case StageSolutionActionType.RotateCounterClockwise:
                    playerController.TryRotateForwardObject(false);
                    break;

                default:
                    playerController.TryMove(action.direction);
                    break;
            }
        }

        private void RestoreInputAfterAutoSolver()
        {
            if (inputReader != null)
                inputReader.InputEnabled = previousInputEnabledBeforeAutoSolver;

            if (playerController != null && !stageSolved && !pauseOpen && !tutorialOpen && !isJumpingIntoHole)
                playerController.SetControlsEnabled(true);
        }

        private void StopAutoSolverRoutine(bool restoreInput)
        {
            if (autoSolverRoutine != null)
            {
                StopCoroutine(autoSolverRoutine);
                autoSolverRoutine = null;
            }

            isAutoSolverRunning = false;

            if (restoreInput)
                RestoreInputAfterAutoSolver();
        }

        private void ShowAutoSolverStatus(string text, Color color)
        {
            if (autoSolverStatusText == null)
                return;

            autoSolverStatusText.text = text;
            autoSolverStatusText.color = color;
            autoSolverStatusText.gameObject.SetActive(true);
        }

        private void HandleLaserFiredFromPlayer()
        {
            HandleSolutionActionPerformed(new StageSolutionActionData
            {
                actionType = StageSolutionActionType.FireLaser,
                direction = playerController != null ? playerController.FacingDirection : GridDirection.Right
            });
        }

        private void HandleSolutionActionPerformed(StageSolutionActionData action)
        {
            if (!isRecordingEditorSolution || stageSolved || action == null)
                return;

            StageSolutionActionData clonedAction = action.Clone();

            if (ShouldCreateUndoSnapshotForRecordedAction(clonedAction))
            {
                editorRecordedSolutionUndoSnapshots.Add(CloneSolutionActions(editorRecordedSolutionActions));
                editorRecordedSolutionRedoSnapshots.Clear();
            }
            else
            {
                editorRecordedSolutionRedoSnapshots.Clear();
            }

            editorRecordedSolutionActions.Add(clonedAction);
        }

        private bool ShouldCreateUndoSnapshotForRecordedAction(StageSolutionActionData action)
        {
            if (action == null)
                return false;

            return action.actionType == StageSolutionActionType.Move
                || action.actionType == StageSolutionActionType.RotateClockwise
                || action.actionType == StageSolutionActionType.RotateCounterClockwise;
        }

        private void HandleRecordedSolutionUndoApplied()
        {
            if (!isRecordingEditorSolution)
                return;

            if (editorRecordedSolutionUndoSnapshots.Count <= 0)
                return;

            editorRecordedSolutionRedoSnapshots.Add(CloneSolutionActions(editorRecordedSolutionActions));

            int lastIndex = editorRecordedSolutionUndoSnapshots.Count - 1;
            ReplaceRecordedSolutionActions(editorRecordedSolutionUndoSnapshots[lastIndex]);
            editorRecordedSolutionUndoSnapshots.RemoveAt(lastIndex);
        }

        private void HandleRecordedSolutionRedoApplied()
        {
            if (!isRecordingEditorSolution)
                return;

            if (editorRecordedSolutionRedoSnapshots.Count <= 0)
                return;

            editorRecordedSolutionUndoSnapshots.Add(CloneSolutionActions(editorRecordedSolutionActions));

            int lastIndex = editorRecordedSolutionRedoSnapshots.Count - 1;
            ReplaceRecordedSolutionActions(editorRecordedSolutionRedoSnapshots[lastIndex]);
            editorRecordedSolutionRedoSnapshots.RemoveAt(lastIndex);
        }

        private void HandleRecordedSolutionHistoryCleared()
        {
            if (!isRecordingEditorSolution)
                return;

            ClearRecordedSolutionTimeline();
        }

        private void ReplaceRecordedSolutionActions(List<StageSolutionActionData> snapshot)
        {
            editorRecordedSolutionActions.Clear();

            if (snapshot == null)
                return;

            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i] != null)
                    editorRecordedSolutionActions.Add(snapshot[i].Clone());
            }
        }

        private void ClearRecordedSolutionTimeline()
        {
            editorRecordedSolutionActions.Clear();
            editorRecordedSolutionUndoSnapshots.Clear();
            editorRecordedSolutionRedoSnapshots.Clear();
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject obj = new GameObject("EventSystem");
                eventSystem = obj.AddComponent<EventSystem>();
            }

            StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null) Destroy(oldModule);
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null) inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (inputModule.actionsAsset == null) inputModule.AssignDefaultActions();
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("InGameFlowCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
            root = canvasObject.GetComponent<RectTransform>();

            RectTransform intro = CreatePanel("StageIntro", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-260f, -149f), new Vector2(260f, -61f), new Color(0f, 0f, 0f, 0.55f));
            AddVertical(intro, 8, 8, 8, 8, 2);
            introText = AddText(intro, "", 28, TextAlignmentOptions.Center, Color.white, true);
            introText.enableWordWrapping = true;


            holeVisual = null;
            holeInteractText = AddText(root, "SPACE", 22, TextAlignmentOptions.Center, Color.white, false);
            holeInteractText.rectTransform.sizeDelta = new Vector2(120f, 36f);
            holeInteractText.gameObject.SetActive(false);

            autoSolverStatusText = AddText(root, "", 24, TextAlignmentOptions.Center, new Color(0.55f, 0.85f, 1f, 1f), false);
            RectTransform solverStatusRect = autoSolverStatusText.rectTransform;
            solverStatusRect.anchorMin = new Vector2(0.5f, 1f);
            solverStatusRect.anchorMax = new Vector2(0.5f, 1f);
            solverStatusRect.pivot = new Vector2(0.5f, 1f);
            solverStatusRect.anchoredPosition = new Vector2(0f, -18f);
            solverStatusRect.sizeDelta = new Vector2(760f, 42f);
            autoSolverStatusText.gameObject.SetActive(false);

            pausePopup = BuildPausePopup("PausePopup", false);
            testPausePopup = BuildPausePopup("TestPausePopup", true);
            settingsPopup = BuildSettingsPopup();
            tutorialPopup = BuildTutorialPopup();
            HideAllPopups();
        }

        private RectTransform BuildPausePopup(string name, bool testMode)
        {
            RectTransform panel = CreateModal(name, 520f, testMode ? 250f : 430f, testMode ? "테스트 플레이" : "일시정지");
            if (testMode)
            {
                AddButton(panel, "테스트 중지", StopEditorTestPlay, 420f, 56f);
                AddButton(panel, "계속 하기", ClosePause, 420f, 48f);
            }
            else
            {
                AddButton(panel, "계속 하기", ClosePause, 420f, 52f);
                AddButton(panel, "설정", ShowSettingsPopup, 420f, 52f);
                AddButton(panel, "타이틀로 나가기", ReturnToTitle, 420f, 52f);
                AddButton(panel, "게임 종료", QuitGame, 420f, 52f);
            }
            return panel;
        }

        private RectTransform BuildSettingsPopup()
        {
            RectTransform panel = CreateModal("SettingsPopup", 560f, 420f, "설정");
            AddSlider(panel, "마스터 볼륨", "TheLaser_MasterVolume");
            AddSlider(panel, "배경음", "TheLaser_BgmVolume");
            AddSlider(panel, "효과음", "TheLaser_SfxVolume");
            AddButton(panel, "닫기", () => { settingsPopup.gameObject.SetActive(false); ShowPausePopup(); }, 460f, 48f);
            return panel;
        }

        private RectTransform BuildTutorialPopup()
        {
            RectTransform panel = CreateModal("TutorialPopup", 720f, 460f, "튜토리얼");
            return panel;
        }

        private void RefreshTutorialPopup()
        {
            ClearChildren(tutorialPopup);
            AddText(tutorialPopup, "튜토리얼", 32, TextAlignmentOptions.Center, Color.white, false);
            string page = currentStage != null && currentStage.tutorialPages != null && tutorialPageIndex >= 0 && tutorialPageIndex < currentStage.tutorialPages.Count ? currentStage.tutorialPages[tutorialPageIndex] : "";
            TMP_Text body = AddText(tutorialPopup, page, 22, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 0.98f, 1f), true);
            body.GetComponent<LayoutElement>().preferredHeight = 250f;
            string buttonText = currentStage != null && currentStage.tutorialPages != null && tutorialPageIndex >= currentStage.tutorialPages.Count - 1 ? "확인" : "다음";
            AddButton(tutorialPopup, buttonText, NextTutorialPage, 560f, 52f);
        }

        private IEnumerator PlayPlayerSpawnIntro()
        {
            if (playerController == null)
                yield break;

            playerController.SetControlsEnabled(false);
            Transform player = playerController.transform;
            Vector3 finalPosition = player.position;

            if (smoothCameraFollow != null)
                smoothCameraFollow.PrepareStageStartZoom(finalPosition);

            Vector3 startPosition = finalPosition + Vector3.up * 1.4f;
            Vector3 finalScale = player.localScale;
            player.position = startPosition;
            player.localScale = finalScale * 1.8f;
            SetRenderersEnabled(player, false);

            GameObject shadow = new GameObject("PlayerSpawnShadow");
            shadow.transform.position = finalPosition + Vector3.forward * 0.1f;
            SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = whiteSprite;
            shadowRenderer.color = new Color(0f, 0f, 0f, 0f);
            shadow.transform.localScale = new Vector3(0.75f, 0.35f, 1f);

            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.45f);
                shadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.75f, t));
                yield return null;
            }

            SetRenderersEnabled(player, true);
            elapsed = 0f;
            while (elapsed < 0.22f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.22f);
                player.position = Vector3.Lerp(startPosition, finalPosition, t);
                player.localScale = Vector3.Lerp(finalScale * 1.8f, finalScale, t);
                yield return null;
            }

            player.position = finalPosition;
            player.localScale = finalScale;
            Destroy(shadow);

            if (smoothCameraFollow != null)
                yield return smoothCameraFollow.PlayStageStartRevealRoutine();

            playerController.SetControlsEnabled(true);
        }

        private void SetRenderersEnabled(Transform rootTransform, bool enabled)
        {
            Renderer[] renderers = rootTransform.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = enabled;
        }

        public void ResetRuntimeStateAfterStageReset()
        {
            StopSolutionPlaybackRoutine(true);
            solutionPlaybackPaused = false;
            solutionPlaybackActions.Clear();
            StopAutoSolverRoutine(true);
            stageSolved = false;
            isJumpingIntoHole = false;
            clearHoleActivated = false;
            if (stageSolvedPresentationRoutine != null)
            {
                StopCoroutine(stageSolvedPresentationRoutine);
                stageSolvedPresentationRoutine = null;
            }
            pauseOpen = false;
            tutorialOpen = false;
            tutorialPageIndex = 0;
            Time.timeScale = 1f;

            if (holeInteractText != null)
                holeInteractText.gameObject.SetActive(false);

            currentStage = gridManager != null ? gridManager.CurrentStageData : currentStage;

            if (turnHistoryController != null)
                turnHistoryController.SetTurnCountingEnabled(true);

            RefreshIntroText();
            HideAllPopups();

            if (smoothCameraFollow != null)
                smoothCameraFollow.CancelClearHoleFocus();

            if (playerController != null)
                playerController.SetControlsEnabled(true);

            RestartEditorSolutionRecordingAfterStageReset();
        }

        private void RestartEditorSolutionRecordingAfterStageReset()
        {
            if (!GameSceneRequest.IsEditorTestPlay && !GameSceneRequest.IsEditorBatchSolutionProcessing)
                return;

            ClearRecordedSolutionTimeline();
            isRecordingEditorSolution = true;
        }

        private void HandleStageSolved()
        {
            if (stageSolved)
                return;

            stageSolved = true;
            clearHoleActivated = false;
            if (!isAutoSolverRunning)
                StopAutoSolverRoutine(true);
            if (!isSolutionPlaybackRunning)
                StopSolutionPlaybackRoutine(true);
            isRecordingEditorSolution = false;

            if (turnHistoryController != null)
                turnHistoryController.SetTurnCountingEnabled(false);

            if (stageSolvedPresentationRoutine != null)
                StopCoroutine(stageSolvedPresentationRoutine);

            stageSolvedPresentationRoutine = StartCoroutine(StageSolvedPresentationRoutine());
        }

        private IEnumerator StageSolvedPresentationRoutine()
        {
            if (lockPlayerDuringStageClearLaserPath && playerController != null)
                playerController.SetControlsEnabled(false);

            if (shakeCameraOnStageSolved && smoothCameraFollow != null)
                smoothCameraFollow.PlayShake(stageSolvedShakeDuration, stageSolvedShakeStrength, stageSolvedShakeFrequency);

            float viewDuration = Mathf.Max(0f, stageClearLaserPathViewDuration);
            float elapsed = 0f;
            while (elapsed < viewDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            ActivateClearHoleAndFocus();

            if (lockPlayerDuringStageClearLaserPath && playerController != null && !pauseOpen && !tutorialOpen && !isJumpingIntoHole)
                playerController.SetControlsEnabled(true);

            stageSolvedPresentationRoutine = null;
        }

        private void ActivateClearHoleAndFocus()
        {
            if (clearHoleActivated || gridManager == null)
                return;

            Vector2Int holePosition = currentStage != null ? currentStage.clearHolePosition : Vector2Int.zero;
            if (!gridManager.IsInside(holePosition))
                holePosition = new Vector2Int(gridManager.Width / 2, gridManager.Height / 2);

            clearHoleActivated = true;
            gridManager.SetClearHoleActive(true, holePosition);

            if (smoothCameraFollow != null)
                smoothCameraFollow.PlayClearHoleFocus(gridManager.GridToWorld(holePosition), clearHoleFocusHoldDuration);
        }

        private void UpdateHoleWorldPosition(Vector2Int gridPosition)
        {
            if (gridManager == null || holeVisual == null || canvas == null || Camera.main == null)
                return;

            Vector3 world = gridManager.GridToWorld(gridPosition);
            Vector2 screen = Camera.main.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            holeVisual.anchoredPosition = local;
            holeVisual.gameObject.SetActive(true);
        }

        private void UpdateMoveLimitText()
        {
            if (moveLimitText == null)
                return;

            GameObject displayRoot = moveLimitRoot != null ? moveLimitRoot : moveLimitText.gameObject;
            StageData stageData = currentStage;
            if (stageData == null && gridManager != null)
                stageData = gridManager.CurrentStageData;

            int moveLimit = stageData != null ? Mathf.Max(0, stageData.moveLimit) : 0;
            if (moveLimit <= 0)
            {
                displayRoot.SetActive(false);
                return;
            }

            int usedTurnCount = turnHistoryController != null ? turnHistoryController.TurnCount : 0;
            int remainingTurnCount = Mathf.Max(0, moveLimit - usedTurnCount);
            displayRoot.SetActive(true);
            moveLimitText.text = $"남은 턴 : {remainingTurnCount}";
            moveLimitText.color = remainingTurnCount <= 0 ? new Color(1f, 0.35f, 0.35f, 1f) : Color.white;
        }

        private void UpdateHoleInteractIcon()
        {
            if (!stageSolved || !clearHoleActivated || gridManager == null || playerController == null || holeInteractText == null)
            {
                if (holeInteractText != null) holeInteractText.gameObject.SetActive(false);
                return;
            }

            Vector2Int hole = gridManager.ClearHolePosition;
            bool adjacent = Mathf.Abs(playerController.GridPosition.x - hole.x) + Mathf.Abs(playerController.GridPosition.y - hole.y) == 1;
            holeInteractText.gameObject.SetActive(adjacent && !isJumpingIntoHole);
            if (adjacent && Camera.main != null)
            {
                Vector3 world = gridManager.GridToWorld(hole) + Vector3.up * 0.65f;
                Vector2 screen = Camera.main.WorldToScreenPoint(world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
                holeInteractText.rectTransform.anchoredPosition = local;
            }
        }

        private void HandleInteractPressed()
        {
            if (lastInteractInputFrame == Time.frameCount)
                return;

            lastInteractInputFrame = Time.frameCount;

            if (!stageSolved || !clearHoleActivated || playerController == null || gridManager == null || isJumpingIntoHole)
                return;

            Vector2Int hole = gridManager.ClearHolePosition;
            bool adjacent = Mathf.Abs(playerController.GridPosition.x - hole.x) + Mathf.Abs(playerController.GridPosition.y - hole.y) == 1;
            if (!adjacent)
                return;

            StartCoroutine(JumpIntoHoleRoutine());
        }

        private IEnumerator JumpIntoHoleRoutine()
        {
            isJumpingIntoHole = true;

            if (laserShooter != null)
                laserShooter.ClearLaser();

            playerController.SetControlsEnabled(false);
            Transform player = playerController.transform;
            Vector3 start = player.position;
            Vector3 target = gridManager.GridToWorld(gridManager.ClearHolePosition);
            Vector3 startScale = player.localScale;
            float elapsed = 0f;
            while (elapsed < 0.35f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.35f);
                player.position = Vector3.Lerp(start, target, t);
                player.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }
            if (audioController != null)
                audioController.PlaySfx(FmodRuntimeAudio.SfxStageClear);

            yield return SceneFadeController.Instance.Fade(1f, 0.35f);
            if (GameSceneRequest.IsEditorBatchSolutionProcessing)
            {
                SaveBatchSolutionToCurrentFile();
                LoadNextBatchStageOrReturnEditor(true);
                yield break;
            }

            if (GameSceneRequest.IsEditorTestPlay)
                GameSceneRequest.SetEditorTestRecordedSolution(editorRecordedSolutionActions);
            else
                StageProgressManager.MarkCleared(currentStage);
            LoadNextStageOrReturnTitle();
        }

        private void SaveBatchSolutionToCurrentFile()
        {
            string filePath = GameSceneRequest.CurrentBatchSolutionFilePath;
            if (currentStage == null || string.IsNullOrWhiteSpace(filePath))
            {
                GameSceneRequest.ReportCurrentBatchSolutionResult(false, "저장할 스테이지 파일 경로가 없습니다.");
                return;
            }

            currentStage.solutionActions = CloneSolutionActions(editorRecordedSolutionActions);

            try
            {
                StageBinarySerializer.Save(currentStage, filePath);
                GameSceneRequest.ReportCurrentBatchSolutionResult(true, $"답안 저장 완료: {Path.GetFileName(filePath)} / {currentStage.solutionActions.Count} 행동");
            }
            catch (Exception exception)
            {
                GameSceneRequest.ReportCurrentBatchSolutionResult(false, $"답안 저장 실패: {exception.Message}");
            }
        }

        private static List<StageSolutionActionData> CloneSolutionActions(List<StageSolutionActionData> source)
        {
            List<StageSolutionActionData> result = new List<StageSolutionActionData>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    result.Add(source[i].Clone());
            }

            return result;
        }

        private void LoadNextBatchStageOrReturnEditor(bool fromCurrentFade)
        {
            string returnScene = string.IsNullOrWhiteSpace(GameSceneRequest.ReturnSceneName) ? "LevelEditor" : GameSceneRequest.ReturnSceneName;

            if (GameSceneRequest.MoveToNextBatchSolutionStage())
            {
                string activeSceneName = SceneManager.GetActiveScene().name;
                if (fromCurrentFade)
                    SceneFadeController.Instance.LoadSceneFromCurrentFade(string.IsNullOrWhiteSpace(activeSceneName) ? gameSceneName : activeSceneName, 0.2f);
                else
                    SceneFadeController.Instance.LoadScene(string.IsNullOrWhiteSpace(activeSceneName) ? gameSceneName : activeSceneName, 0.2f);
                return;
            }

            if (fromCurrentFade)
                SceneFadeController.Instance.LoadSceneFromCurrentFade(returnScene, 0.35f);
            else
                SceneFadeController.Instance.LoadScene(returnScene, 0.35f);
        }

        private void LoadNextStageOrReturnTitle()
        {
            if (GameSceneRequest.IsEditorTestPlay)
            {
                string returnScene = string.IsNullOrWhiteSpace(GameSceneRequest.ReturnSceneName) ? "LevelEditor" : GameSceneRequest.ReturnSceneName;
                GameSceneRequest.ClearGameplayRequest();
                SceneFadeController.Instance.LoadSceneFromCurrentFade(returnScene, 0.35f);
                return;
            }

            string nextStage = FindNextBuiltInStagePath();
            if (!string.IsNullOrWhiteSpace(nextStage))
            {
                GameSceneRequest.RequestBuiltInStage(nextStage);
                string activeSceneName = SceneManager.GetActiveScene().name;
                SceneFadeController.Instance.LoadSceneFromCurrentFade(string.IsNullOrWhiteSpace(activeSceneName) ? gameSceneName : activeSceneName, 0.35f);
                return;
            }

            GameSceneRequest.Clear();
            SceneFadeController.Instance.LoadSceneFromCurrentFade(titleSceneName, 0.35f);
        }

        private string FindNextBuiltInStagePath()
        {
            if (currentStage == null || GameSceneRequest.IsCustomLevel)
                return string.Empty;

            if (StageFilePaths.IsBuiltInResourcePath(GameSceneRequest.StageFilePath))
                return BuiltInStageLoader.FindNextBuiltInResourcePath(currentStage);

            int nextStageIndex = currentStage.stageIndexInChapter + 1;
            List<string> searchDirectories = new List<string>();

            if (!string.IsNullOrWhiteSpace(GameSceneRequest.StageFilePath))
            {
                string currentDirectory = Path.GetDirectoryName(GameSceneRequest.StageFilePath);
                if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
                    searchDirectories.Add(currentDirectory);
            }

            if (Directory.Exists(StageFilePaths.BuiltInLevelsDirectory) && !searchDirectories.Contains(StageFilePaths.BuiltInLevelsDirectory))
                searchDirectories.Add(StageFilePaths.BuiltInLevelsDirectory);

            for (int d = 0; d < searchDirectories.Count; d++)
            {
                string directPath = Path.Combine(searchDirectories[d], $"Chapter{currentStage.chapterIndex:00}_Stage{nextStageIndex:00}.tls");
                if (File.Exists(directPath))
                    return directPath;

                string[] files = Directory.GetFiles(searchDirectories[d], "*.tls", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < files.Length; i++)
                {
                    if (!StageBinarySerializer.TryLoad(files[i], out StageData data))
                        continue;

                    if (data.chapterIndex == currentStage.chapterIndex && data.stageIndexInChapter == nextStageIndex)
                        return files[i];
                }
            }

            return string.Empty;
        }

        private void RefreshIntroText()
        {
            if (introText == null || currentStage == null)
                return;

            introText.text = $"<size=22>{currentStage.chapterName}</size>\n스테이지 {currentStage.stageIndexInChapter}";
        }

        private void ShowTutorialPopup()
        {
            tutorialPageIndex = 0;
            tutorialOpen = true;
            if (playerController != null) playerController.SetControlsEnabled(false);
            HideAllPopups();
            tutorialPopup.gameObject.SetActive(true);
            RefreshTutorialPopup();
        }

        private void NextTutorialPage()
        {
            tutorialPageIndex++;
            if (currentStage == null || currentStage.tutorialPages == null || tutorialPageIndex >= currentStage.tutorialPages.Count)
            {
                tutorialOpen = false;
                tutorialPopup.gameObject.SetActive(false);
                if (playerController != null) playerController.SetControlsEnabled(true);
                return;
            }
            RefreshTutorialPopup();
        }

        private void HandlePausePressed()
        {
            if (lastPauseInputFrame == Time.frameCount)
                return;

            lastPauseInputFrame = Time.frameCount;

            if (isJumpingIntoHole)
                return;

            if (tutorialOpen)
            {
                tutorialOpen = false;
                if (tutorialPopup != null)
                    tutorialPopup.gameObject.SetActive(false);
            }

            if (pauseOpen) ClosePause(); else ShowPausePopup();
        }

        private void ShowPausePopup()
        {
            pauseOpen = true;
            Time.timeScale = 0f;
            if (playerController != null) playerController.SetControlsEnabled(false);
            HideAllPopups();

            RectTransform targetPopup = GameSceneRequest.IsEditorTestPlay ? testPausePopup : pausePopup;
            if (targetPopup == null)
                targetPopup = pausePopup;

            if (targetPopup != null)
                targetPopup.gameObject.SetActive(true);

            if (audioController != null)
                audioController.PlaySfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void ClosePause()
        {
            pauseOpen = false;
            Time.timeScale = 1f;
            HideAllPopups();
            if (playerController != null && !tutorialOpen) playerController.SetControlsEnabled(true);

            if (audioController != null)
                audioController.PlaySfx(FmodRuntimeAudio.SfxUiClose);
        }

        private void ShowSettingsPopup()
        {
            pausePopup.gameObject.SetActive(false);
            testPausePopup.gameObject.SetActive(false);
            settingsPopup.gameObject.SetActive(true);
        }

        private void StopEditorTestPlay()
        {
            Time.timeScale = 1f;
            string returnScene = string.IsNullOrWhiteSpace(GameSceneRequest.ReturnSceneName) ? "LevelEditor" : GameSceneRequest.ReturnSceneName;
            GameSceneRequest.ClearGameplayRequest();
            SceneFadeController.Instance.LoadScene(returnScene);
        }

        private void ReturnToTitle()
        {
            Time.timeScale = 1f;
            GameSceneRequest.Clear();
            SceneFadeController.Instance.LoadScene(titleSceneName);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HideAllPopups()
        {
            if (pausePopup != null) pausePopup.gameObject.SetActive(false);
            if (testPausePopup != null) testPausePopup.gameObject.SetActive(false);
            if (settingsPopup != null) settingsPopup.gameObject.SetActive(false);
            if (tutorialPopup != null) tutorialPopup.gameObject.SetActive(false);
        }

        private void AddSlider(RectTransform parent, string label, string key)
        {
            AddText(parent, label, 20, TextAlignmentOptions.Left, Color.white, false);
            Slider slider = CreateSlider(parent, PlayerPrefs.GetFloat(key, 1f));
            slider.onValueChanged.AddListener(value =>
            {
                PlayerPrefs.SetFloat(key, value);
                PlayerPrefs.Save();
                if (audioController != null)
                    audioController.ApplySavedVolumes();
            });
        }

        private RectTransform CreateModal(string name, float width, float height, string title)
        {
            RectTransform panel = CreatePanel(name, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width * 0.5f, -height * 0.5f), new Vector2(width * 0.5f, height * 0.5f), new Color(0.075f, 0.085f, 0.115f, 0.98f));
            AddVertical(panel, 20, 20, 20, 20, 12).childForceExpandHeight = false;
            AddText(panel, title, 32, TextAlignmentOptions.Center, Color.white, false);
            return panel;
        }

        private RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rect = CreateUIObject(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;
            return rect;
        }

        private RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private VerticalLayoutGroup AddVertical(RectTransform parent, int left, int right, int top, int bottom, int spacing)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            return layout;
        }

        private TMP_Text AddText(RectTransform parent, string text, int size, TextAlignmentOptions alignment, Color color, bool wrap)
        {
            RectTransform rect = CreateUIObject("Text", parent);
            TMP_Text tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = wrap ? Mathf.Max(56f, size * 4f) : size * 1.6f;
            return tmp;
        }

        private Button AddButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick, float width, float height)
        {
            RectTransform rect = CreatePanel("Button", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.16f, 0.18f, 0.25f, 1f));
            LayoutElement le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            TMP_Text labelText = AddText(rect, label, 22, TextAlignmentOptions.Center, Color.white, false);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private Slider CreateSlider(RectTransform parent, float value)
        {
            RectTransform rootObject = CreateUIObject("Slider", parent);
            rootObject.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            Slider slider = rootObject.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            RectTransform fill = CreatePanel("Fill", rootObject, Vector2.zero, new Vector2(value, 1f), new Vector2(0f, 10f), new Vector2(0f, -10f), new Color(0.25f, 0.65f, 1f, 1f));
            RectTransform handle = CreatePanel("Handle", rootObject, new Vector2(value, 0.5f), new Vector2(value, 0.5f), new Vector2(-9f, -15f), new Vector2(9f, 15f), Color.white);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        private void ClearChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
