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
        [SerializeField] private FmodRuntimeAudio audioController;

        [Header("Stage Clear Presentation")]
        [SerializeField] private float stageClearLaserPathViewDuration = 1f;
        [SerializeField] private float clearHoleFocusHoldDuration = 2f;
        [SerializeField] private bool lockPlayerDuringStageClearLaserPath = true;
        [SerializeField] private bool shakeCameraOnStageSolved = true;
        [SerializeField] private float stageSolvedShakeDuration = 0.18f;
        [SerializeField] private float stageSolvedShakeStrength = 0.24f;
        [SerializeField] private float stageSolvedShakeFrequency = 55f;

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

            if (audioController != null)
                audioController.ApplySavedVolumes();

            if (laserShooter != null)
                laserShooter.RefreshLaserRemainingText(null);

            if (gridManager != null)
                gridManager.StageSolved += HandleStageSolved;

            if (inputReader != null)
            {
                inputReader.InteractPressed += HandleInteractPressed;
                inputReader.PausePressed += HandlePausePressed;
            }

            if (audioController != null && currentStage != null)
                audioController.PlayBgm(currentStage.bgmEventPath);

            RefreshIntroText();
            yield return PlayPlayerSpawnIntro();

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
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                HandlePausePressed();

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                HandleInteractPressed();

            UpdateHoleInteractIcon();
        }

        private void EnsureReferences()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (playerController == null) playerController = FindFirstObjectByType<PlayerGridController>();
            if (inputReader == null) inputReader = FindFirstObjectByType<PlayerInputReader>();
            if (laserShooter == null) laserShooter = FindFirstObjectByType<LaserShooter>();
            if (smoothCameraFollow == null) smoothCameraFollow = FindFirstObjectByType<SmoothCameraFollow>();
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

            if (GameSceneRequest.IsEditorTestPlay && GameSceneRequest.HasEditorTestStageData)
            {
                gridManager.LoadStage(GameSceneRequest.EditorTestStageData.Clone());
                if (playerController != null)
                    playerController.ResetToStageStartImmediate();
                return;
            }

            if (string.IsNullOrWhiteSpace(GameSceneRequest.StageFilePath))
                return;

            gridManager.LoadStageFromFile(GameSceneRequest.StageFilePath);
            if (playerController != null)
                playerController.ResetToStageStartImmediate();
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

            RectTransform intro = CreatePanel("StageIntro", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-260f, -110f), new Vector2(260f, -22f), new Color(0f, 0f, 0f, 0.55f));
            AddVertical(intro, 8, 8, 8, 8, 2);
            introText = AddText(intro, "", 28, TextAlignmentOptions.Center, Color.white, true);
            introText.enableWordWrapping = true;

            holeVisual = null;
            holeInteractText = AddText(root, "SPACE", 22, TextAlignmentOptions.Center, Color.white, false);
            holeInteractText.rectTransform.sizeDelta = new Vector2(120f, 36f);
            holeInteractText.gameObject.SetActive(false);

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
            RefreshIntroText();
            HideAllPopups();

            if (smoothCameraFollow != null)
                smoothCameraFollow.CancelClearHoleFocus();

            if (playerController != null)
                playerController.SetControlsEnabled(true);
        }

        private void HandleStageSolved()
        {
            if (stageSolved)
                return;

            stageSolved = true;
            clearHoleActivated = false;

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
            if (!GameSceneRequest.IsEditorTestPlay)
                StageProgressManager.MarkCleared(currentStage);
            LoadNextStageOrReturnTitle();
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
