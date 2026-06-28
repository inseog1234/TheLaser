using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LevelEditor
{
    public class LevelEditorController : MonoBehaviour
    {
        private enum ToolCategory { All, Position, Target, Wall, Mirror, Prism, Zone, Tile }
        private enum ToolType { None, PlayerStart, ClearHole, TargetNormal, TargetSequence, TargetIntersection, Wall, Mirror, PrismSplitter, PrismColor, PrismRefraction, ZoneRotate, ZoneMirror, DistanceSensor, LensAmplifier, Eraser }
        private enum SelectedElementKind { None, PlayerStart, ClearHole, Wall, Target, Object, DistanceSensor, TransformZone }
        private enum TriggerEditMode { None, WaitingTarget, WaitingWallDestination, WaitingPrismDirection }
        private enum PlayerStartPreset { LeftTop, RightTop, CenterTop, LeftMiddle, RightMiddle, Center, LeftBottom, RightBottom, CenterBottom }

        private sealed class ToolDefinition
        {
            public ToolType ToolType;
            public ToolCategory Category;
            public string Name;
            public string Description;
            public bool HasDirection;

            public ToolDefinition(ToolType toolType, ToolCategory category, string name, string description, bool hasDirection = false)
            {
                ToolType = toolType;
                Category = category;
                Name = name;
                Description = description;
                HasDirection = hasDirection;
            }
        }

        private sealed class ToolSettings
        {
            public GridDirection Direction = GridDirection.Right;
            public LaserColorKind Color = LaserColorKind.Default;
            public int SequenceIndex = 1;
            public bool TargetPassThrough = true;
            public int IntersectionCount = 2;
            public List<LaserColorKind> IntersectionColors = new() { LaserColorKind.Default, LaserColorKind.Default, LaserColorKind.Default };
            public bool Pushable;
            public bool Rotatable;
            public bool Reverse;
            public int BranchCount = 2;
            public PrismSplitterMode SplitterMode = PrismSplitterMode.ForwardAndLeft;
            public int ZoneWidth = 1;
            public int ZoneHeight = 1;
            public int ZoneOffsetX = -1;
            public int ZoneOffsetY = -1;
            public bool Clockwise = true;
            public MirrorAxis MirrorAxis = MirrorAxis.Horizontal;
            public float DetectionRadius = 0.5f;
            public int DistanceBoost = 1;
        }

        [Header("Runtime")]
        [SerializeField] private StageData editingStageData = new StageData();
        [SerializeField] private int defaultWidth = 8;
        [SerializeField] private int defaultHeight = 8;
        [SerializeField] private int defaultLaserMaxDistance = 8;
        [SerializeField] private int defaultMoveLimit = 0;
        [SerializeField] private bool allowBuiltInLevelLoad;

        [Header("Camera")]
        [SerializeField] private Camera editorCamera;
        [SerializeField] private float cameraPanSpeed = 0.015f;
        [SerializeField] private float cameraZoomSpeed = 5f;
        [SerializeField] private float minCameraSize = 3f;
        [SerializeField] private float maxCameraSize = 30f;

        private readonly List<ToolDefinition> tools = new();
        private readonly Dictionary<Vector2Int, GameObject> cellVisuals = new();
        private readonly List<GameObject> stageVisuals = new();
        private readonly List<GameObject> triggerLineVisuals = new();
        private readonly List<Selectable> runtimeSettingSelectables = new();

        private TMP_FontAsset uiFont;
        private Sprite whiteSprite;
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform leftPanel;
        private RectTransform paletteContent;
        private RectTransform settingsPanel;
        private RectTransform topStatusPanel;
        private RectTransform bottomActionPanel;
        private RectTransform descriptionPanel;
        private RectTransform runtimeSettingsPanel;
        private RectTransform mainPopup;
        private RectTransform newLevelPopup;
        private RectTransform loadPopup;
        private RectTransform savePopup;
        private RectTransform uploadPopup;
        private RectTransform solutionOverwritePopup;
        private RectTransform helpPanel;
        private Button uploadButton;
        private Button uploadPublishButton;
        private TMP_InputField uploadNicknameInput;
        private TMP_InputField uploadTitleInput;
        private TMP_InputField uploadDescriptionInput;
        private TMP_Text uploadValidationText;
        private TMP_Text editorOnlineNoticeText;
        private Coroutine editorNoticeRoutine;
        private TMP_Text descriptionTitleText;
        private TMP_Text descriptionBodyText;
        private TMP_Text statusText;
        private TMP_Text gridInfoText;
        private TMP_Text currentFileText;
        private TMP_Text solutionOverwriteInfoText;
        private TMP_InputField runtimeStageNameInput;
        private TMP_InputField runtimeWidthInput;
        private TMP_InputField runtimeHeightInput;
        private TMP_InputField runtimeLaserInput;
        private TMP_InputField runtimeMoveInput;
        private TMP_Dropdown runtimeBgmDropdown;
        private TMP_InputField newStageNameInput;
        private TMP_InputField newWidthInput;
        private TMP_InputField newHeightInput;
        private TMP_InputField newLaserInput;
        private TMP_InputField newMoveInput;
        private TMP_Dropdown newPlayerStartDropdown;
        private TMP_Dropdown newHolePositionDropdown;
        private TMP_Dropdown newBgmDropdown;
        private TMP_Dropdown editorBgmDropdown;
        private PlayerStartPreset newPlayerStartPreset = PlayerStartPreset.LeftMiddle;
        private PlayerStartPreset newHolePositionPreset = PlayerStartPreset.Center;
        private int newBgmIndex;
        private TMP_InputField loadPathInput;
        private TMP_InputField saveDirectoryInput;
        private TMP_InputField saveFileNameInput;

        private GameObject stageRoot;
        private GameObject ghostRoot;
        private GameObject rangePreviewRoot;
        private GameObject pendingArrowRoot;
        private Vector2Int hoverGridPosition;
        private bool hasHoverPosition;
        private bool isPanning;
        private Vector2 previousMousePosition;

        private ToolCategory selectedCategory = ToolCategory.All;
        private ToolDefinition selectedTool;
        private readonly ToolSettings placementSettings = new();
        private SelectedElementKind selectedElementKind = SelectedElementKind.None;
        private Vector2Int selectedPosition;
        private StageTargetData selectedTarget;
        private StageObjectData selectedObject;
        private DistanceSensorData selectedSensor;
        private TransformZoneData selectedZone;
        private TriggerEditMode triggerEditMode = TriggerEditMode.None;
        private Vector2Int pendingWallPosition;
        private Vector2Int pendingPrismPosition;
        private GridDirection pendingPrismDirection;
        private string currentFilePath;
        private string selectedLoadFilePath;
        private string selectedSaveDirectory;
        private string selectedSaveFileName = "CustomLevel";
        private List<StageSolutionActionData> pendingReturnedSolutionActions;
        private FmodRuntimeAudio audioController;
        private int editorBgmIndex = 10;
        private bool suppressRuntimeSettingEvent;
        private bool isDraggingElement;
        private bool isNativeFileDialogOpen;
        private float lastNativeFileDialogClosedTime = -10f;
        private bool dragUndoCaptured;
        private bool isSettingsPanelExpanded = true;
        private Vector2Int dragLastGridPosition;

        private const int MaxUndoCount = 150;
        private readonly List<StageData> undoStack = new();
        private readonly List<StageData> redoStack = new();
        private string lastCommittedStageKey;
        private bool isApplyingHistory;
        private bool skipAutoHistoryThisFrame;
        private MirrorShape pendingMirrorShape = MirrorShape.NormalL;

        public StageData EditingStageData => editingStageData;
        public string ExportDirectory => string.IsNullOrWhiteSpace(selectedSaveDirectory) ? StageFilePaths.MyCustomLevelsDirectory : selectedSaveDirectory;

        private void Awake()
        {
            EnsureBasics();
            EnsureEditorAudio();
            PlayEditorBgmByIndex(editorBgmIndex, false);
            CreateToolDefinitions();
            BuildUI();

            bool restoredFromTest = TryRestoreEditorStageAfterTest();
            if (!restoredFromTest)
            {
                CreateNewLevel(defaultWidth, defaultHeight, defaultLaserMaxDistance, defaultMoveLimit, false);
                ShowMainPopup();
            }

            StartCoroutine(EditorTutorialRoutine());
        }

        private void Update()
        {
            HandleCameraInput();
            UpdateHover();
            HandleShortcutInput();
            UpdateGhostAndRange();
            HandleMouseClick();
            HandleElementDrag();
            AutoTrackStageHistory();
        }

        public void SelectPalette(int index)
        {
            if (index < 0 || index >= tools.Count)
                return;

            SelectTool(tools[index]);
        }

        public void CreateNewLevel()
        {
            CreateNewLevel(defaultWidth, defaultHeight, defaultLaserMaxDistance, defaultMoveLimit, false);
        }

        public void CreateNewLevel(int width, int height, int laserMaxDistance, int moveLimit, bool useLaserDistanceLimit)
        {
            CreateNewLevel(width, height, laserMaxDistance, moveLimit, useLaserDistanceLimit, newPlayerStartPreset, "Custom Level", GetBgmEventPathByIndex(newBgmIndex));
        }

        private void CreateNewLevel(int width, int height, int laserMaxDistance, int moveLimit, bool useLaserDistanceLimit, PlayerStartPreset playerStartPreset, string stageName)
        {
            CreateNewLevel(width, height, laserMaxDistance, moveLimit, useLaserDistanceLimit, playerStartPreset, stageName, GetBgmEventPathByIndex(newBgmIndex));
        }

        private void CreateNewLevel(int width, int height, int laserMaxDistance, int moveLimit, bool useLaserDistanceLimit, PlayerStartPreset playerStartPreset, string stageName, string bgmEventPath)
        {
            int finalLaserMaxDistance = Mathf.Max(0, laserMaxDistance);

            editingStageData = new StageData
            {
                stageNumber = 1,
                stageName = string.IsNullOrWhiteSpace(stageName) ? "Custom Level" : stageName.Trim(),
                bgmEventPath = string.IsNullOrWhiteSpace(bgmEventPath) ? GetBgmEventPathByIndex(0) : bgmEventPath,
                width = Mathf.Max(1, width),
                height = Mathf.Max(1, height),
                useLaserDistanceLimit = finalLaserMaxDistance > 0,
                laserMaxDistance = finalLaserMaxDistance,
                moveLimit = Mathf.Max(0, moveLimit),
                playerStartPosition = ResolvePlayerStartPosition(Mathf.Max(1, width), Mathf.Max(1, height), playerStartPreset),
                playerStartDirection = GridDirection.Right,
                clearHolePosition = ResolvePlayerStartPosition(Mathf.Max(1, width), Mathf.Max(1, height), newHolePositionPreset)
            };

            defaultWidth = editingStageData.width;
            defaultHeight = editingStageData.height;
            defaultLaserMaxDistance = editingStageData.laserMaxDistance;
            defaultMoveLimit = editingStageData.moveLimit;

            currentFilePath = string.Empty;
            selectedSaveFileName = StageFilePaths.NormalizeStageFileName(editingStageData.stageName).Replace(".tls", string.Empty);
            ClearSelection();
            RebuildSequencePattern();
            RebuildStageVisuals();
            RefreshRuntimeSettingsPanel();
            ResetStageHistory();
            HideAllPopups();
            PlayEditorSfx(FmodRuntimeAudio.SfxUiConfirmation);
            SetStatus($"새 레벨 생성 완료: {editingStageData.stageName}");
        }

        private bool TryRestoreEditorStageAfterTest()
        {
            if (!GameSceneRequest.TryConsumeEditorReturnStage(out StageData restoredStageData, out string restoredFilePath, out string restoredSaveDirectory, out string restoredSaveFileName))
                return false;

            editingStageData = restoredStageData;
            currentFilePath = restoredFilePath;
            selectedSaveDirectory = string.IsNullOrWhiteSpace(restoredSaveDirectory) ? StageFilePaths.MyCustomLevelsDirectory : restoredSaveDirectory;
            selectedSaveFileName = string.IsNullOrWhiteSpace(restoredSaveFileName) ? StageFilePaths.NormalizeStageFileName(editingStageData.stageName).Replace(".tls", string.Empty) : restoredSaveFileName;

            defaultWidth = editingStageData.width;
            defaultHeight = editingStageData.height;
            defaultLaserMaxDistance = editingStageData.laserMaxDistance;
            defaultMoveLimit = editingStageData.moveLimit;

            ClearSelection();
            RebuildSequencePattern();
            RebuildStageVisuals();
            RefreshRuntimeSettingsPanel();
            ResetStageHistory();
            HideAllPopups(false);
            SetStatus("테스트 종료: 작업 중인 맵을 복구함");
            HandleReturnedSolutionAfterTest();
            return true;
        }

        private void HandleReturnedSolutionAfterTest()
        {
            if (!GameSceneRequest.TryConsumePendingEditorSolution(out List<StageSolutionActionData> actions) || actions == null || actions.Count <= 0)
                return;

            pendingReturnedSolutionActions = CloneSolutionActions(actions);

            if (editingStageData != null && editingStageData.HasSolution)
            {
                ShowSolutionOverwritePopup();
                return;
            }

            ApplyPendingReturnedSolution();
        }

        private void ShowSolutionOverwritePopup()
        {
            HideAllPopups(false);
            if (solutionOverwriteInfoText != null)
            {
                int oldCount = editingStageData != null && editingStageData.solutionActions != null ? editingStageData.solutionActions.Count : 0;
                int newCount = pendingReturnedSolutionActions != null ? pendingReturnedSolutionActions.Count : 0;
                solutionOverwriteInfoText.text = $"기존 답안을 덮어쓸까요?\n기존 답안: {oldCount} 행동 / 새 답안: {newCount} 행동";
            }

            if (solutionOverwritePopup != null)
                solutionOverwritePopup.gameObject.SetActive(true);

            PlayEditorSfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void ConfirmOverwriteSolution()
        {
            ApplyPendingReturnedSolution();
            HideAllPopups();
        }

        private void KeepExistingSolution()
        {
            pendingReturnedSolutionActions = null;
            HideAllPopups();
            SetStatus("이전 답안을 유지함");
        }

        private void ApplyPendingReturnedSolution()
        {
            if (editingStageData == null || pendingReturnedSolutionActions == null || pendingReturnedSolutionActions.Count <= 0)
                return;

            undoStack.Add(editingStageData.Clone());
            editingStageData.solutionActions = CloneSolutionActions(pendingReturnedSolutionActions);
            pendingReturnedSolutionActions = null;
            RefreshRuntimeSettingsPanel();
            ResetStageHistory();
            SetStatus($"답안 기록 완료: {editingStageData.solutionActions.Count} 행동");
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

        public void LoadLevelFromInputPath()
        {
            LoadLevel(selectedLoadFilePath);
        }

        public void LoadLevel(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                SetStatus("불러오기 실패: 파일 경로 없음");
                return;
            }

            string fullPath = Path.GetFullPath(filePath);
            string builtInRoot = Path.GetFullPath(StageFilePaths.BuiltInLevelsDirectory);

            if (!allowBuiltInLevelLoad && fullPath.StartsWith(builtInRoot, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("기본 내장 레벨 불러오기는 현재 비활성화 상태임");
                return;
            }

            if (!StageBinarySerializer.TryLoad(fullPath, out StageData loadedStageData))
            {
                SetStatus("불러오기 실패: .tls 파일 확인 필요");
                return;
            }

            editingStageData = loadedStageData;
            currentFilePath = fullPath;
            selectedLoadFilePath = fullPath;
            ApplyLoadedFileAsSaveTarget(fullPath);
            ClearSelection();
            RebuildSequencePattern();
            RebuildStageVisuals();
            RefreshRuntimeSettingsPanel();
            ResetStageHistory();
            HideAllPopups();
            PlayEditorSfx(FmodRuntimeAudio.SfxUiConfirmation);
            SetStatus($"불러오기 완료: {Path.GetFileName(fullPath)}");
        }

        public void ExportLevel()
        {
            string directory = ExportDirectory;
            string fileName = StageFilePaths.NormalizeStageFileName(selectedSaveFileName);
            SaveCurrentStageToPath(Path.Combine(directory, fileName), true);
        }

        public void SetExportDirectory(string directory)
        {
            selectedSaveDirectory = directory;
        }

        public void SetExportFileName(string fileName)
        {
            selectedSaveFileName = fileName;
        }

        public void SetLoadFilePath(string filePath)
        {
            selectedLoadFilePath = filePath;
        }

        private void ApplyLoadedFileAsSaveTarget(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string directory = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                selectedSaveDirectory = directory;
                if (saveDirectoryInput != null)
                    saveDirectoryInput.SetTextWithoutNotify(selectedSaveDirectory);
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                selectedSaveFileName = fileName;
                if (saveFileNameInput != null)
                    saveFileNameInput.SetTextWithoutNotify(selectedSaveFileName);
            }
        }

        public void OpenExportDirectory()
        {
            Directory.CreateDirectory(ExportDirectory);
            Application.OpenURL(ExportDirectory);
        }

        private System.Collections.IEnumerator EditorTutorialRoutine()
        {
            yield return null;

            if (PlayerPrefs.GetInt("TheLaser_LevelEditor_TutorialSeen", 0) == 1)
                yield break;

            PlayerPrefs.SetInt("TheLaser_LevelEditor_TutorialSeen", 1);
            PlayerPrefs.Save();

            RectTransform panel = CreatePanel("EditorTutorialOverlay", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-430f, -210f), new Vector2(430f, 210f), new Color(0.02f, 0.025f, 0.035f, 0.96f));
            AddVerticalLayout(panel, 20, 20, 20, 20, 12).childForceExpandHeight = false;
            TMP_Text title = AddText(panel, "레벨 에디터 튜토리얼", 32, TextAlignmentOptions.Center, Color.white);
            title.enableWordWrapping = true;
            TMP_Text body = AddText(panel, "왼쪽 기능 패널에서 기능을 고르고 맵에 좌클릭으로 배치합니다.\n기능 설정 패널에서 세부값을 바꾸고, 설명 패널에서 용도를 확인합니다.\n상단 상태창은 현재 작업 상태를 보여주며, 우측 도움말은 단축키를 보여줍니다.\n하단에는 테스트 / 저장하기 / 불러오기 / 메뉴 / 타이틀 버튼이 있습니다.", 22, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 0.98f, 1f));
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Overflow;
            body.GetComponent<LayoutElement>().preferredHeight = 230f;
            AddButton(panel, "확인", () => Destroy(panel.gameObject), 720f, 54f);
        }

        private void EnsureBasics()
        {
            StageFilePaths.EnsureDefaultDirectories();
            uiFont = Resources.Load<TMP_FontAsset>("Font/TMP/PF스타더스트 3");
            whiteSprite = CreateWhiteSprite();

            if (editorCamera == null)
                editorCamera = Camera.main;

            if (editorCamera == null)
            {
                GameObject cameraObject = new GameObject("EditorCamera");
                editorCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.tag = "MainCamera";
            }

            editorCamera.orthographic = true;
            editorCamera.orthographicSize = 8f;
            editorCamera.transform.position = new Vector3(0f, 0f, -10f);
            editorCamera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
            EnsureFmodStudioListener(editorCamera.gameObject);

            EnsureEventSystem();

            stageRoot = new GameObject("LevelEditor_StageRoot");
            ghostRoot = new GameObject("LevelEditor_Ghost");
            rangePreviewRoot = new GameObject("LevelEditor_RangePreview");
            pendingArrowRoot = new GameObject("LevelEditor_PendingArrow");
            ghostRoot.SetActive(false);
            rangePreviewRoot.SetActive(false);
            pendingArrowRoot.SetActive(false);
        }

        private void EnsureFmodStudioListener(GameObject targetObject)
        {
            if (targetObject == null)
                return;

            Type listenerType = Type.GetType("FMODUnity.StudioListener, FMODUnity");
            if (listenerType == null)
                return;

            if (targetObject.GetComponent(listenerType) == null)
                targetObject.AddComponent(listenerType);
        }

        private void EnsureEditorAudio()
        {
            audioController = FmodRuntimeAudio.EnsureInstance();
            if (audioController != null)
                audioController.ApplySavedVolumes();
        }

        private FmodRuntimeAudio GetAudioController()
        {
            if (audioController == null)
                EnsureEditorAudio();

            return audioController;
        }

        private void PlayEditorBgmByIndex(int index, bool playUiSound)
        {
            List<string> paths = BgmEventPaths();
            if (paths.Count <= 0)
                return;

            editorBgmIndex = Mathf.Clamp(index, 0, paths.Count - 1);

            if (editorBgmDropdown != null)
            {
                editorBgmDropdown.SetValueWithoutNotify(editorBgmIndex);
                editorBgmDropdown.RefreshShownValue();
            }

            if (playUiSound)
                PlayEditorSfx(FmodRuntimeAudio.SfxEditorCheckBox);

            GetAudioController()?.PlayBgm(paths[editorBgmIndex]);
        }

        private void SetEditorBgmFromDropdown(int index)
        {
            PlayEditorBgmByIndex(index, true);
            SetStatus($"에디터 BGM 변경: {BgmDisplayNames()[editorBgmIndex]}");
        }

        private void PlayEditorSfx(string eventPath)
        {
            GetAudioController()?.PlaySfx(eventPath);
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                if (Application.isPlaying)
                    Destroy(oldModule);
                else
                    DestroyImmediate(oldModule);
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            if (inputModule.actionsAsset == null)
                inputModule.AssignDefaultActions();
        }

        private Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private void CreateToolDefinitions()
        {
            tools.Clear();
            tools.Add(new ToolDefinition(ToolType.PlayerStart, ToolCategory.Position, "플레이어 위치 수정", "플레이어 시작 위치와 바라보는 방향을 수정한다.", true));
            tools.Add(new ToolDefinition(ToolType.ClearHole, ToolCategory.Position, "구멍 위치 수정", "스테이지 클리어 후 생성되는 구멍 위치를 수정한다."));
            tools.Add(new ToolDefinition(ToolType.TargetNormal, ToolCategory.Target, "도착지 / 색상 도착지", "Default면 일반 도착지, 색상을 고르면 해당 색상 레이저만 인정한다."));
            tools.Add(new ToolDefinition(ToolType.TargetSequence, ToolCategory.Target, "시퀸스 도착지", "정해진 순서대로 맞아야 하는 도착지다. 색상을 고르면 시퀸스+컬러 도착지가 된다."));
            tools.Add(new ToolDefinition(ToolType.TargetIntersection, ToolCategory.Target, "선분 교차 도착지", "여러 레이저 선분이 이 위치에서 교차하면 활성화된다."));
            tools.Add(new ToolDefinition(ToolType.Wall, ToolCategory.Wall, "벽", "막는 타일이다. 체크하면 플레이어가 밀 수 있는 오브젝트 벽이 된다."));
            tools.Add(new ToolDefinition(ToolType.Mirror, ToolCategory.Mirror, "거울", "ㄴ 또는 역ㄴ 반사 거울이다. 밀기/회전 가능 여부를 설정할 수 있다.", true));
            tools.Add(new ToolDefinition(ToolType.PrismSplitter, ToolCategory.Prism, "분기 프리즘", "레이저를 2개 또는 3개 줄기로 분기한다."));
            tools.Add(new ToolDefinition(ToolType.PrismColor, ToolCategory.Prism, "색상 프리즘", "통과한 레이저 색상을 지정 색상으로 바꾼다."));
            tools.Add(new ToolDefinition(ToolType.PrismRefraction, ToolCategory.Prism, "굴절 프리즘", "레이저 방향을 45도 굴절시킨다."));
            tools.Add(new ToolDefinition(ToolType.ZoneRotate, ToolCategory.Zone, "회전 구역", "구역 안 오브젝트를 90도 회전 변환한다."));
            tools.Add(new ToolDefinition(ToolType.ZoneMirror, ToolCategory.Zone, "대칭 구역", "구역 안 오브젝트를 가로/세로 방향으로 대칭 변환한다."));
            tools.Add(new ToolDefinition(ToolType.DistanceSensor, ToolCategory.Tile, "거리 감응 타일", "레이저 선분이 반경 안을 지나가면 연결된 트리거를 실행한다."));
            tools.Add(new ToolDefinition(ToolType.LensAmplifier, ToolCategory.Tile, "증폭기", "레이저 최대 길이를 지정 칸 수만큼 늘린다."));
            tools.Add(new ToolDefinition(ToolType.Eraser, ToolCategory.All, "지우개", "클릭한 칸의 기능을 지운다."));
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("LevelEditorCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasRect = canvasObject.GetComponent<RectTransform>();

            BuildRuntimeSettingsPanel();
            BuildLeftPanel();
            BuildSettingsPanel();
            BuildDescriptionPanel();
            BuildStatusPanel();
            BuildHelpPanel();
            BuildMainPopup();
            BuildNewLevelPopup();
            BuildLoadPopup();
            BuildSavePopup();
            BuildUploadPopup();
            BuildSolutionOverwritePopup();
            RebuildPalette();
            HideAllPopups();
        }

        private void BuildRuntimeSettingsPanel()
        {
            runtimeSettingsPanel = CreatePanel("RuntimeSettingsPanel", canvasRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -240f), new Vector2(420f, -12f), new Color(0.09f, 0.11f, 0.15f, 0.94f));
            VerticalLayoutGroup layout = AddVerticalLayout(runtimeSettingsPanel, 8, 8, 8, 8, 6);
            layout.childForceExpandHeight = false;
            AddText(runtimeSettingsPanel, "런타임 설정", 24, TextAlignmentOptions.Left, Color.white);
            runtimeStageNameInput = AddInputRow(runtimeSettingsPanel, "스테이지 이름", "Custom Level", value => ApplyRuntimeSettingInputs());
            runtimeWidthInput = AddInputRow(runtimeSettingsPanel, "가로 칸", "8", value => ApplyRuntimeSettingInputs());
            runtimeHeightInput = AddInputRow(runtimeSettingsPanel, "세로 칸", "8", value => ApplyRuntimeSettingInputs());
            runtimeLaserInput = AddInputRow(runtimeSettingsPanel, "레이저 최대 길이", "8", value => ApplyRuntimeSettingInputs());
            runtimeMoveInput = AddInputRow(runtimeSettingsPanel, "이동 제한 행동 수", "0", value => ApplyRuntimeSettingInputs());
            runtimeBgmDropdown = AddDropdownRow(runtimeSettingsPanel, "레벨 BGM", BgmDisplayNames(), FindBgmIndex(editingStageData != null ? editingStageData.bgmEventPath : string.Empty), ApplyRuntimeBgmDropdown);
            gridInfoText = AddText(runtimeSettingsPanel, "", 18, TextAlignmentOptions.Left, new Color(0.85f, 0.9f, 1f, 1f));
        }

        private void BuildLeftPanel()
        {
            leftPanel = CreatePanel("FunctionPanel", canvasRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(12f, 12f), new Vector2(420f, -258f), new Color(0.075f, 0.08f, 0.105f, 0.95f));
            VerticalLayoutGroup rootLayout = AddVerticalLayout(leftPanel, 8, 8, 8, 8, 8);
            rootLayout.childForceExpandHeight = false;
            AddText(leftPanel, "기능 패널", 28, TextAlignmentOptions.Left, Color.white);

            RectTransform tabRoot = CreateUIObject("Tabs", leftPanel);
            GridLayoutGroup tabLayout = tabRoot.gameObject.AddComponent<GridLayoutGroup>();
            tabLayout.cellSize = new Vector2(92f, 36f);
            tabLayout.spacing = new Vector2(5f, 5f);
            tabLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tabLayout.constraintCount = 4;
            tabRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = 84f;

            AddTabButton(tabRoot, "전부", ToolCategory.All);
            AddTabButton(tabRoot, "위치", ToolCategory.Position);
            AddTabButton(tabRoot, "도착지", ToolCategory.Target);
            AddTabButton(tabRoot, "벽", ToolCategory.Wall);
            AddTabButton(tabRoot, "거울", ToolCategory.Mirror);
            AddTabButton(tabRoot, "프리즘", ToolCategory.Prism);
            AddTabButton(tabRoot, "구역", ToolCategory.Zone);
            AddTabButton(tabRoot, "타일", ToolCategory.Tile);

            ScrollRect scrollRect = CreateScrollRect(leftPanel, "PaletteScroll", out paletteContent);
            scrollRect.GetComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private void BuildSettingsPanel()
        {
            settingsPanel = CreatePanel("FeatureSettingsPanel", canvasRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(436f, 12f), new Vector2(790f, -12f), new Color(0.08f, 0.095f, 0.13f, 0.95f));
            AddVerticalLayout(settingsPanel, 10, 10, 10, 10, 8).childForceExpandHeight = false;
            ApplySettingsPanelFoldState();
            RebuildSettingsPanel();
        }

        private void BuildDescriptionPanel()
        {
            descriptionPanel = CreatePanel("FeatureDescriptionPanel", canvasRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-470f, 12f), new Vector2(-12f, 178f), new Color(0.08f, 0.09f, 0.12f, 0.95f));
            AddVerticalLayout(descriptionPanel, 10, 10, 10, 10, 6).childForceExpandHeight = false;
            descriptionTitleText = AddText(descriptionPanel, "선택 없음", 24, TextAlignmentOptions.Left, Color.white);
            descriptionBodyText = AddText(descriptionPanel, "왼쪽 기능 버튼을 눌러 배치할 기능을 선택하세요.", 18, TextAlignmentOptions.Left, new Color(0.82f, 0.86f, 0.92f, 1f));
            descriptionBodyText.enableWordWrapping = true;
            descriptionBodyText.overflowMode = TextOverflowModes.Overflow;
            descriptionBodyText.GetComponent<LayoutElement>().preferredHeight = 100f;
        }

        private void BuildStatusPanel()
        {
            topStatusPanel = CreatePanel("TopStatusPanel", canvasRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-430f, -92f), new Vector2(430f, -18f), new Color(0.04f, 0.045f, 0.06f, 0.82f));
            AddVerticalLayout(topStatusPanel, 12, 12, 8, 8, 2);
            statusText = AddText(topStatusPanel, "준비됨", 18, TextAlignmentOptions.Center, Color.white);
            statusText.enableWordWrapping = true;
            statusText.overflowMode = TextOverflowModes.Overflow;
            statusText.GetComponent<LayoutElement>().preferredHeight = 58f;

            bottomActionPanel = CreatePanel("BottomActionPanel", canvasRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-430f, 16f), new Vector2(430f, 70f), new Color(0.05f, 0.055f, 0.07f, 0.85f));
            AddHorizontalLayout(bottomActionPanel, 8, 8, 8, 8, 10);
            Button testButton = AddButton(bottomActionPanel, "테스트", StartTestPlay, 90f, 38f);
            Button saveButton = AddButton(bottomActionPanel, "저장하기", ShowSavePopup, 120f, 38f);
            Button loadButton = AddButton(bottomActionPanel, "불러오기", ShowLoadPopup, 120f, 38f);
            Button mainButton = AddButton(bottomActionPanel, "메뉴", ShowMainPopup, 90f, 38f);
            uploadButton = AddButton(bottomActionPanel, "업로드", ShowUploadPopup, 100f, 38f);
            Button titleButton = AddButton(bottomActionPanel, "타이틀", ReturnToTitle, 90f, 38f);
            testButton.name = "TestButton";
            saveButton.name = "SaveButton";
            loadButton.name = "LoadButton";
            mainButton.name = "MenuButton";
            if (uploadButton != null) uploadButton.name = "UploadButton";
            titleButton.name = "TitleButton";
            RefreshUploadButtonVisibility();
            BuildEditorOnlineNotice();

            ApplyFloatingPanelsLayout();
        }

        private void BuildHelpPanel()
        {
            helpPanel = CreatePanel("HelpPanel", canvasRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-500f, -150f), new Vector2(-12f, -12f), new Color(0.04f, 0.045f, 0.06f, 0.82f));
            AddVerticalLayout(helpPanel, 10, 10, 10, 10, 3);
            TMP_Text text = AddText(helpPanel, "조작: 좌클릭 배치/선택/드래그 이동 | 우클릭 드래그 맵 이동 | 휠 줌 | C 시계/V 반시계 회전 | X 거리 감응 트리거 | Ctrl+Z 되돌리기 | Ctrl+Y/Ctrl+Shift+Z 다시실행 | ESC 배치취소", 17, TextAlignmentOptions.Left, new Color(0.82f, 0.86f, 0.92f, 1f));
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.GetComponent<LayoutElement>().preferredHeight = 112f;
        }

        private void BuildMainPopup()
        {
            mainPopup = CreateModalPanel("MainPopup", 560f, 450f, "레벨 에디터");
            AddButton(mainPopup, "레벨 생성", ShowNewLevelPopup, 460f, 54f);
            AddButton(mainPopup, "레벨 불러오기", ShowLoadPopup, 460f, 54f);
            editorBgmDropdown = AddDropdownRow(mainPopup, "에디터 BGM", BgmDisplayNames(), editorBgmIndex, SetEditorBgmFromDropdown);
            TMP_Text info = AddText(mainPopup, "ESC로 이 메뉴를 열고 닫을 수 있습니다.", 17, TextAlignmentOptions.Center, new Color(0.8f, 0.86f, 0.95f, 1f));
            info.enableWordWrapping = true;
        }

        private void BuildNewLevelPopup()
        {
            newLevelPopup = CreateModalPanel("NewLevelPopup", 560f, 610f, "새 레벨 기본 설정");
            newStageNameInput = AddInputRow(newLevelPopup, "스테이지 이름", "New Level", null);
            newWidthInput = AddInputRow(newLevelPopup, "가로 칸 길이", defaultWidth.ToString(), null);
            newHeightInput = AddInputRow(newLevelPopup, "세로 칸 길이", defaultHeight.ToString(), null);
            newLaserInput = AddInputRow(newLevelPopup, "레이저 최대 길이", defaultLaserMaxDistance.ToString(), null);
            newMoveInput = AddInputRow(newLevelPopup, "이동 제한 행동 수", defaultMoveLimit.ToString(), null);
            newPlayerStartDropdown = AddDropdownRow(newLevelPopup, "플레이어 위치", PlayerStartPresetNames(), (int)newPlayerStartPreset, index => newPlayerStartPreset = (PlayerStartPreset)index);
            newHolePositionDropdown = AddDropdownRow(newLevelPopup, "구멍 위치", PlayerStartPresetNames(), (int)newHolePositionPreset, index => newHolePositionPreset = (PlayerStartPreset)index);
            newBgmDropdown = AddDropdownRow(newLevelPopup, "BGM", BgmDisplayNames(), newBgmIndex, index => newBgmIndex = Mathf.Clamp(index, 0, BgmDisplayNames().Count - 1));
            TMP_Text info = AddText(newLevelPopup, "레이저 최대 길이와 이동 제한은 0이면 제한 없음", 17, TextAlignmentOptions.Left, new Color(0.8f, 0.86f, 0.95f, 1f));
            info.enableWordWrapping = false;
            AddButton(newLevelPopup, "완료", CompleteNewLevelPopup, 460f, 52f);
            AddButton(newLevelPopup, "취소", ShowMainPopup, 460f, 44f);
        }

        private void BuildLoadPopup()
        {
            loadPopup = CreateModalPanel("LoadPopup", 700f, 360f, "레벨 불러오기");
            loadPathInput = AddInputRow(loadPopup, "파일 경로", selectedLoadFilePath, value => selectedLoadFilePath = value);
            AddButton(loadPopup, "파일 경로 선택", PickLoadFile, 600f, 46f);
            AddButton(loadPopup, "불러오기", () => LoadLevel(selectedLoadFilePath), 600f, 52f);
            AddButton(loadPopup, "취소", () => HideAllPopups(), 600f, 42f);
        }

        private void BuildSavePopup()
        {
            if (string.IsNullOrWhiteSpace(selectedSaveDirectory))
                selectedSaveDirectory = StageFilePaths.MyCustomLevelsDirectory;

            savePopup = CreateModalPanel("SavePopup", 700f, 430f, "저장하기");
            saveDirectoryInput = AddInputRow(savePopup, "폴더 경로", selectedSaveDirectory, value => selectedSaveDirectory = value);
            saveFileNameInput = AddInputRow(savePopup, "파일 이름", selectedSaveFileName, value => selectedSaveFileName = value);
            AddButton(savePopup, "폴더 경로 선택", PickSaveFolder, 600f, 46f);
            AddButton(savePopup, "저장", ExportLevel, 600f, 52f);
            AddButton(savePopup, "취소", () => HideAllPopups(), 600f, 42f);
            currentFileText = AddText(savePopup, "", 16, TextAlignmentOptions.Left, new Color(0.75f, 0.8f, 0.9f, 1f));
            currentFileText.enableWordWrapping = false;
        }

        private void BuildUploadPopup()
        {
            uploadPopup = CreateModalPanel("UploadPopup", 700f, 560f, "커스텀 맵 업로드");
            uploadNicknameInput = AddInputRow(uploadPopup, "닉네임", "", value => RefreshUploadPublishButton());
            uploadTitleInput = AddInputRow(uploadPopup, "게시물 이름", editingStageData != null ? editingStageData.stageName : "", value => RefreshUploadPublishButton());
            uploadDescriptionInput = AddInputRow(uploadPopup, "게시물 설명", "", value => RefreshUploadPublishButton());
            uploadValidationText = AddText(uploadPopup, "답안이 있는 맵만 업로드할 수 있습니다.", 18, TextAlignmentOptions.Left, new Color(0.85f, 0.9f, 1f, 1f));
            uploadValidationText.enableWordWrapping = true;
            uploadValidationText.overflowMode = TextOverflowModes.Overflow;
            uploadValidationText.GetComponent<LayoutElement>().preferredHeight = 72f;
            uploadPublishButton = AddButton(uploadPopup, "게시하기", PublishCurrentLevel, 600f, 52f);
            AddButton(uploadPopup, "취소", () => HideAllPopups(), 600f, 42f);
            RefreshUploadPublishButton();
        }

        private void BuildSolutionOverwritePopup()
        {
            solutionOverwritePopup = CreateModalPanel("SolutionOverwritePopup", 640f, 330f, "기존 답안 덮어쓰기");
            solutionOverwriteInfoText = AddText(solutionOverwritePopup, "기존 답안을 덮어쓸까요?", 22, TextAlignmentOptions.Center, new Color(0.9f, 0.94f, 1f, 1f));
            solutionOverwriteInfoText.enableWordWrapping = true;
            solutionOverwriteInfoText.GetComponent<LayoutElement>().preferredHeight = 90f;
            AddButton(solutionOverwritePopup, "네", ConfirmOverwriteSolution, 560f, 52f);
            AddButton(solutionOverwritePopup, "아니오", KeepExistingSolution, 560f, 46f);
        }

        private RectTransform CreateModalPanel(string name, float width, float height, string title)
        {
            RectTransform panel = CreatePanel(name, canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width * 0.5f, -height * 0.5f), new Vector2(width * 0.5f, height * 0.5f), new Color(0.075f, 0.085f, 0.115f, 0.98f));
            AddVerticalLayout(panel, 18, 18, 18, 18, 10).childForceExpandHeight = false;
            AddText(panel, title, 32, TextAlignmentOptions.Center, Color.white);
            return panel;
        }

        private void RebuildPalette()
        {
            ClearChildren(paletteContent);

            for (int i = 0; i < tools.Count; i++)
            {
                ToolDefinition tool = tools[i];
                if (selectedCategory != ToolCategory.All && tool.Category != selectedCategory && tool.ToolType != ToolType.Eraser)
                    continue;

                Button button = AddButton(paletteContent, tool.Name, () => SelectTool(tool), 360f, 48f);
                ColorBlock colors = button.colors;
                colors.normalColor = selectedTool == tool ? new Color(0.3f, 0.46f, 0.72f, 1f) : new Color(0.15f, 0.17f, 0.22f, 1f);
                colors.highlightedColor = new Color(0.24f, 0.30f, 0.42f, 1f);
                colors.pressedColor = new Color(0.1f, 0.12f, 0.16f, 1f);
                button.colors = colors;
            }
        }

        private void AddTabButton(RectTransform parent, string label, ToolCategory category)
        {
            AddButton(parent, label, () => { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); selectedCategory = category; RebuildPalette(); }, 92f, 36f);
        }

        private void SelectTool(ToolDefinition tool)
        {
            selectedTool = tool;
            PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect1);
            selectedElementKind = SelectedElementKind.None;
            selectedTarget = null;
            selectedObject = null;
            selectedSensor = null;
            selectedZone = null;
            triggerEditMode = TriggerEditMode.None;
            placementSettings.Direction = tool.HasDirection ? placementSettings.Direction : GridDirection.Right;

            if (tool.ToolType == ToolType.TargetSequence)
                placementSettings.SequenceIndex = GetNextSequenceIndex();

            RebuildPalette();
            RebuildSettingsPanel();
            UpdateDescription(tool.Name, tool.Description);
            SetStatus($"기능 선택: {tool.Name}");
        }

        private void RebuildSettingsPanel()
        {
            ClearChildren(settingsPanel);
            BuildSettingsPanelHeader();

            if (!isSettingsPanelExpanded)
                return;

            if (selectedElementKind != SelectedElementKind.None)
            {
                BuildSelectedElementSettings();
                return;
            }

            if (selectedTool == null)
            {
                TMP_Text info = AddText(settingsPanel, "선택된 기능 없음", 20, TextAlignmentOptions.Left, new Color(0.8f, 0.84f, 0.9f, 1f));
                info.enableWordWrapping = false;
                return;
            }

            BuildToolSettings(selectedTool.ToolType, placementSettings, true);
        }

        private void BuildSettingsPanelHeader()
        {
            if (!isSettingsPanelExpanded)
            {
                Button expandButton = AddButton(settingsPanel, ">", ToggleSettingsPanelExpanded, 34f, 42f);
                expandButton.name = "FeatureSettingsExpandButton";
                return;
            }

            RectTransform header = CreateUIObject("FeatureSettingsHeader", settingsPanel);
            HorizontalLayoutGroup layout = AddHorizontalLayout(header, 0, 0, 0, 0, 8);
            layout.childForceExpandWidth = false;

            TMP_Text title = AddText(header, "기능 설정", 26, TextAlignmentOptions.Left, Color.white);
            LayoutElement titleLayout = title.GetComponent<LayoutElement>();
            titleLayout.preferredWidth = 235f;
            titleLayout.flexibleWidth = 1f;

            Button collapseButton = AddButton(header, "접기", ToggleSettingsPanelExpanded, 76f, 38f);
            collapseButton.name = "FeatureSettingsCollapseButton";
        }

        private void ToggleSettingsPanelExpanded()
        {
            isSettingsPanelExpanded = !isSettingsPanelExpanded;
            ApplySettingsPanelFoldState();
            RebuildSettingsPanel();
        }

        private void ApplySettingsPanelFoldState()
        {
            if (settingsPanel == null)
                return;

            settingsPanel.offsetMin = new Vector2(436f, 12f);
            settingsPanel.offsetMax = isSettingsPanelExpanded ? new Vector2(790f, -12f) : new Vector2(490f, -12f);
            ApplyFloatingPanelsLayout();
        }

        private void ApplyFloatingPanelsLayout()
        {
            if (topStatusPanel != null)
            {
                if (isSettingsPanelExpanded)
                {
                    topStatusPanel.offsetMin = new Vector2(-154f, -92f);
                    topStatusPanel.offsetMax = new Vector2(446f, -18f);
                }
                else
                {
                    topStatusPanel.offsetMin = new Vector2(-385f, -92f);
                    topStatusPanel.offsetMax = new Vector2(385f, -18f);
                }
            }

            if (bottomActionPanel != null)
            {
                bool hasUploadButton = uploadButton != null && uploadButton.gameObject.activeSelf;

                if (isSettingsPanelExpanded)
                {
                    bottomActionPanel.offsetMin = hasUploadButton
                        ? new Vector2(-104f, 16f)
                        : new Vector2(-54f, 16f);

                    bottomActionPanel.offsetMax = hasUploadButton
                        ? new Vector2(376f, 70f)
                        : new Vector2(326f, 70f);
                }
                else
                {
                    bottomActionPanel.offsetMin = hasUploadButton
                        ? new Vector2(-245f, 16f)
                        : new Vector2(-190f, 16f);

                    bottomActionPanel.offsetMax = hasUploadButton
                        ? new Vector2(245f, 70f)
                        : new Vector2(190f, 70f);
                }
            }
        }

        private void BuildSelectedElementSettings()
        {
            AddText(settingsPanel, $"선택 위치: {selectedPosition.x}, {selectedPosition.y}", 20, TextAlignmentOptions.Left, new Color(0.85f, 0.9f, 1f, 1f));

            if (selectedElementKind == SelectedElementKind.PlayerStart)
            {
                AddStepperRow(settingsPanel, "방향", DirectionName(editingStageData.playerStartDirection), () => { editingStageData.playerStartDirection = editingStageData.playerStartDirection.RotateClockwise(); RebuildStageVisuals(); RebuildSettingsPanel(); }, () => { editingStageData.playerStartDirection = editingStageData.playerStartDirection.RotateCounterClockwise(); RebuildStageVisuals(); RebuildSettingsPanel(); });
                AddButton(settingsPanel, "선택 해제", ClearSelection, 300f, 42f);
                return;
            }

            if (selectedElementKind == SelectedElementKind.ClearHole)
            {
                AddText(settingsPanel, "구멍 위치는 드래그해서 이동할 수 있습니다.", 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f)).enableWordWrapping = true;
                AddButton(settingsPanel, "선택 해제", ClearSelection, 300f, 42f);
                return;
            }

            if (selectedElementKind == SelectedElementKind.Wall)
            {
                ToolSettings temp = new ToolSettings { Pushable = FindObjectAt(selectedPosition) != null };
                AddToggleRow(settingsPanel, "밀 수 있음", temp.Pushable, value => ConvertSelectedWallPushable(value));
                AddButton(settingsPanel, "삭제", () => { ClearAt(selectedPosition); ClearSelection(); RebuildStageVisuals(); }, 300f, 42f);
                return;
            }

            if (selectedElementKind == SelectedElementKind.Target && selectedTarget != null)
            {
                BuildTargetSettings(selectedTarget);
                AddButton(settingsPanel, "삭제", () => { RemoveTarget(selectedTarget); ClearSelection(); RebuildStageVisuals(); }, 300f, 42f);
                return;
            }

            if (selectedElementKind == SelectedElementKind.Object && selectedObject != null)
            {
                BuildObjectSettings(selectedObject);
                AddButton(settingsPanel, "삭제", () => { editingStageData.objects.Remove(selectedObject); ClearSelection(); RebuildStageVisuals(); }, 300f, 42f);
                return;
            }

            if (selectedElementKind == SelectedElementKind.DistanceSensor && selectedSensor != null)
            {
                BuildDistanceSensorSettings(selectedSensor);
                AddButton(settingsPanel, "삭제", () => { editingStageData.distanceSensors.Remove(selectedSensor); ClearSelection(); RebuildStageVisuals(); }, 300f, 42f);
                return;
            }

            if (selectedElementKind == SelectedElementKind.TransformZone && selectedZone != null)
            {
                BuildTransformZoneSettings(selectedZone);
                AddButton(settingsPanel, "삭제", () => { editingStageData.transformZones.Remove(selectedZone); ClearSelection(); RebuildStageVisuals(); }, 300f, 42f);
            }
        }

        private void BuildToolSettings(ToolType toolType, ToolSettings settings, bool placement)
        {
            if (selectedTool != null && selectedTool.HasDirection)
                AddStepperRow(settingsPanel, "방향", DirectionName(settings.Direction), () => { settings.Direction = settings.Direction.RotateClockwise(); RebuildSettingsPanel(); }, () => { settings.Direction = settings.Direction.RotateCounterClockwise(); RebuildSettingsPanel(); });

            switch (toolType)
            {
                case ToolType.PlayerStart:
                    AddText(settingsPanel, "추가 설정 없음", 19, TextAlignmentOptions.Left, new Color(0.8f, 0.84f, 0.9f, 1f));
                    break;

                case ToolType.TargetNormal:
                    AddLaserColorDropdown(settingsPanel, "색상", settings.Color, value => settings.Color = value);
                    break;

                case ToolType.TargetSequence:
                    AddIntStepper(settingsPanel, "시퀸스 Index", settings.SequenceIndex, 1, 999, value => settings.SequenceIndex = GetAvailableSequenceIndex(value, null));
                    AddLaserColorDropdown(settingsPanel, "색상", settings.Color, value => settings.Color = value);
                    AddToggleRow(settingsPanel, "레이저 관통", settings.TargetPassThrough, value => settings.TargetPassThrough = value);
                    break;

                case ToolType.TargetIntersection:
                    AddIntStepper(settingsPanel, "교차 갯수", settings.IntersectionCount, 2, 3, value => { settings.IntersectionCount = Mathf.Clamp(value, 2, 3); RebuildSettingsPanel(); });
                    EnsureIntersectionColorCount(settings.IntersectionColors, settings.IntersectionCount);
                    for (int i = 0; i < settings.IntersectionCount; i++)
                    {
                        int index = i;
                        AddLaserColorDropdown(settingsPanel, $"색상 {i + 1}", settings.IntersectionColors[index], value => settings.IntersectionColors[index] = value);
                    }
                    break;

                case ToolType.Wall:
                    AddToggleRow(settingsPanel, "밀 수 있음", settings.Pushable, value => settings.Pushable = value);
                    break;

                case ToolType.Mirror:
                    AddToggleRow(settingsPanel, "역 방향", settings.Reverse, value => settings.Reverse = value);
                    AddToggleRow(settingsPanel, "밀 수 있음", settings.Pushable, value => settings.Pushable = value);
                    AddToggleRow(settingsPanel, "회전 가능", settings.Rotatable, value => settings.Rotatable = value);
                    break;

                case ToolType.PrismSplitter:
                    AddToggleRow(settingsPanel, "밀 수 있음", settings.Pushable, value => settings.Pushable = value);
                    AddToggleRow(settingsPanel, "회전 가능", settings.Rotatable, value => settings.Rotatable = value);
                    AddIntStepper(settingsPanel, "줄기 갯수", settings.BranchCount, 2, 3, value => { settings.BranchCount = Mathf.Clamp(value, 2, 3); if (settings.BranchCount >= 3) settings.SplitterMode = PrismSplitterMode.ForwardLeftRight; else if (settings.SplitterMode == PrismSplitterMode.ForwardLeftRight) settings.SplitterMode = PrismSplitterMode.ForwardAndLeft; RebuildSettingsPanel(); }, "개");
                    if (settings.BranchCount <= 2)
                        AddSplitterModeDropdown(settingsPanel, "분기 방향", settings.SplitterMode, value => { settings.SplitterMode = value == PrismSplitterMode.ForwardLeftRight ? PrismSplitterMode.ForwardAndLeft : value; RebuildSettingsPanel(); });
                    break;

                case ToolType.PrismColor:
                    AddToggleRow(settingsPanel, "밀 수 있음", settings.Pushable, value => settings.Pushable = value);
                    AddToggleRow(settingsPanel, "회전 가능", settings.Rotatable, value => settings.Rotatable = value);
                    AddLaserColorDropdown(settingsPanel, "색상", settings.Color == LaserColorKind.Default ? LaserColorKind.Red : settings.Color, value => settings.Color = value == LaserColorKind.Default ? LaserColorKind.Red : value);
                    break;

                case ToolType.PrismRefraction:
                    AddToggleRow(settingsPanel, "역 방향", settings.Reverse, value => settings.Reverse = value);
                    AddToggleRow(settingsPanel, "밀 수 있음", settings.Pushable, value => settings.Pushable = value);
                    AddToggleRow(settingsPanel, "회전 가능", settings.Rotatable, value => settings.Rotatable = value);
                    break;

                case ToolType.ZoneRotate:
                    AddIntStepper(settingsPanel, "가로 크기", settings.ZoneWidth, 1, editingStageData.width, value => { settings.ZoneWidth = Mathf.Clamp(value, 1, editingStageData.width); RebuildSettingsPanel(); }, "칸");
                    AddIntStepper(settingsPanel, "세로 크기", settings.ZoneHeight, 1, editingStageData.height, value => { settings.ZoneHeight = Mathf.Clamp(value, 1, editingStageData.height); RebuildSettingsPanel(); }, "칸");
                    AddZoneOffsetSettings(settings.ZoneWidth, settings.ZoneHeight, value => { settings.ZoneOffsetX = value; RebuildSettingsPanel(); }, value => { settings.ZoneOffsetY = value; RebuildSettingsPanel(); }, settings.ZoneOffsetX, settings.ZoneOffsetY);
                    AddToggleRow(settingsPanel, "시계 방향 회전", settings.Clockwise, value => settings.Clockwise = value);
                    break;

                case ToolType.ZoneMirror:
                    AddIntStepper(settingsPanel, "가로 크기", settings.ZoneWidth, 1, editingStageData.width, value => { settings.ZoneWidth = Mathf.Clamp(value, 1, editingStageData.width); RebuildSettingsPanel(); }, "칸");
                    AddIntStepper(settingsPanel, "세로 크기", settings.ZoneHeight, 1, editingStageData.height, value => { settings.ZoneHeight = Mathf.Clamp(value, 1, editingStageData.height); RebuildSettingsPanel(); }, "칸");
                    AddZoneOffsetSettings(settings.ZoneWidth, settings.ZoneHeight, value => { settings.ZoneOffsetX = value; RebuildSettingsPanel(); }, value => { settings.ZoneOffsetY = value; RebuildSettingsPanel(); }, settings.ZoneOffsetX, settings.ZoneOffsetY);
                    AddStepperRow(settingsPanel, "축", settings.MirrorAxis == MirrorAxis.Horizontal ? "가로" : "세로", () => { settings.MirrorAxis = ToggleMirrorAxis(settings.MirrorAxis); RebuildSettingsPanel(); }, () => { settings.MirrorAxis = ToggleMirrorAxis(settings.MirrorAxis); RebuildSettingsPanel(); });
                    break;

                case ToolType.DistanceSensor:
                    AddFloatStepper(settingsPanel, "감지 반경", settings.DetectionRadius, 0.5f, 99f, 0.5f, value => settings.DetectionRadius = value);
                    break;

                case ToolType.LensAmplifier:
                    AddIntStepper(settingsPanel, "증폭 칸 수", settings.DistanceBoost, 1, 999, value => { settings.DistanceBoost = Mathf.Max(1, value); RebuildSettingsPanel(); }, "칸");
                    break;

                case ToolType.Eraser:
                    AddText(settingsPanel, "좌클릭한 칸의 배치물을 지운다.", 18, TextAlignmentOptions.Left, new Color(0.8f, 0.84f, 0.9f, 1f));
                    break;
            }
        }

        private void BuildTargetSettings(StageTargetData target)
        {
            if (target.targetType == TargetType.Normal || target.targetType == TargetType.ColorLocked)
            {
                AddLaserColorDropdown(settingsPanel, "색상", target.requiredColor, value => { target.requiredColor = value; target.targetType = value == LaserColorKind.Default ? TargetType.Normal : TargetType.ColorLocked; AddTargetData(target); selectedTarget = FindTargetAt(target.position); RebuildStageVisuals(); RebuildSettingsPanel(); });
                return;
            }

            if (target.targetType == TargetType.SequenceLocked || target.targetType == TargetType.SequenceColorLocked)
            {
                AddIntStepper(settingsPanel, "시퀸스 Index", target.sequenceValue, 1, 999, value => { target.sequenceValue = GetAvailableSequenceIndex(value, target); RebuildSequencePattern(); RebuildStageVisuals(); RebuildSettingsPanel(); });
                AddLaserColorDropdown(settingsPanel, "색상", target.requiredColor, value => { target.requiredColor = value; target.targetType = value == LaserColorKind.Default ? TargetType.SequenceLocked : TargetType.SequenceColorLocked; RebuildSequencePattern(); RebuildStageVisuals(); RebuildSettingsPanel(); });
                AddToggleRow(settingsPanel, "레이저 관통", !target.stopLaserOnHit, value => { target.stopLaserOnHit = !value; RebuildStageVisuals(); RebuildSettingsPanel(); });
                return;
            }

            if (target.targetType == TargetType.Intersection)
            {
                AddIntStepper(settingsPanel, "교차 갯수", target.requiredIntersectionCount, 2, 3, value => { target.requiredIntersectionCount = Mathf.Clamp(value, 2, 3); EnsureIntersectionColorCount(target.intersectionColors, target.requiredIntersectionCount); RebuildStageVisuals(); RebuildSettingsPanel(); });
                EnsureIntersectionColorCount(target.intersectionColors, target.requiredIntersectionCount);
                for (int i = 0; i < target.requiredIntersectionCount; i++)
                {
                    int index = i;
                    AddLaserColorDropdown(settingsPanel, $"색상 {i + 1}", target.intersectionColors[index], value => { target.intersectionColors[index] = value; RebuildStageVisuals(); });
                }
            }
        }

        private void BuildObjectSettings(StageObjectData obj)
        {
            if (ShouldShowDirectionSetting(obj))
                AddStepperRow(settingsPanel, "방향", DirectionName(obj.direction), () => { obj.direction = obj.direction.RotateClockwise(); RebuildStageVisuals(); RebuildSettingsPanel(); }, () => { obj.direction = obj.direction.RotateCounterClockwise(); RebuildStageVisuals(); RebuildSettingsPanel(); });

            if (obj.objectType == PuzzleObjectType.Wall)
            {
                AddToggleRow(settingsPanel, "밀 수 있음", obj.manipulationType.CanPush(), value => ConvertObjectWallPushable(obj, value));
                return;
            }

            if (obj.objectType == PuzzleObjectType.Mirror)
            {
                AddToggleRow(settingsPanel, "역 방향", obj.mirrorShape == MirrorShape.ReverseL, value => { obj.mirrorShape = value ? MirrorShape.ReverseL : MirrorShape.NormalL; RebuildStageVisuals(); });
                AddManipulationToggles(obj);
                return;
            }

            if (obj.objectType == PuzzleObjectType.Prism)
            {
                if (obj.prismType == PrismType.Splitter)
                {
                    AddManipulationToggles(obj);
                    int count = obj.splitterMode == PrismSplitterMode.ForwardLeftRight ? 3 : 2;
                    AddIntStepper(settingsPanel, "줄기 갯수", count, 2, 3, value => { obj.splitterMode = value >= 3 ? PrismSplitterMode.ForwardLeftRight : (obj.splitterMode == PrismSplitterMode.ForwardLeftRight ? PrismSplitterMode.ForwardAndLeft : obj.splitterMode); RebuildStageVisuals(); RebuildSettingsPanel(); }, "개");
                    if (obj.splitterMode != PrismSplitterMode.ForwardLeftRight)
                        AddSplitterModeDropdown(settingsPanel, "분기 방향", obj.splitterMode, value => { obj.splitterMode = value == PrismSplitterMode.ForwardLeftRight ? PrismSplitterMode.ForwardAndLeft : value; RebuildStageVisuals(); RebuildSettingsPanel(); });
                    return;
                }

                if (obj.prismType == PrismType.Color)
                {
                    AddManipulationToggles(obj);
                    AddLaserColorDropdown(settingsPanel, "색상", obj.prismColor, value => { obj.prismColor = value == LaserColorKind.Default ? LaserColorKind.Red : value; RebuildStageVisuals(); });
                    return;
                }

                if (obj.prismType == PrismType.Refraction)
                {
                    AddToggleRow(settingsPanel, "역 방향", obj.refractionMode == RefractionMode.CounterClockwise45, value => { obj.refractionMode = value ? RefractionMode.CounterClockwise45 : RefractionMode.Clockwise45; RebuildStageVisuals(); });
                    AddManipulationToggles(obj);
                    return;
                }
            }

            if (obj.objectType == PuzzleObjectType.Lens)
            {
                AddIntStepper(settingsPanel, "증폭 칸 수", obj.distanceBoost, 1, 999, value => { obj.distanceBoost = Mathf.Max(1, value); RebuildStageVisuals(); RebuildSettingsPanel(); }, "칸");
            }
        }

        private void AddManipulationToggles(StageObjectData obj)
        {
            AddToggleRow(settingsPanel, "밀 수 있음", obj.manipulationType.CanPush(), value => { obj.manipulationType = ResolveManipulation(value, obj.manipulationType.CanRotate()); RebuildStageVisuals(); RebuildSettingsPanel(); });
            AddToggleRow(settingsPanel, "회전 가능", obj.manipulationType.CanRotate(), value => { obj.manipulationType = ResolveManipulation(obj.manipulationType.CanPush(), value); RebuildStageVisuals(); RebuildSettingsPanel(); });
        }

        private void BuildTransformZoneSettings(TransformZoneData zone)
        {
            AddIntStepper(settingsPanel, "가로 크기", zone.width, 1, editingStageData.width, value => { zone.width = Mathf.Clamp(value, 1, editingStageData.width); NormalizeZoneOffset(zone); RebuildStageVisuals(); RebuildSettingsPanel(); }, "칸");
            AddIntStepper(settingsPanel, "세로 크기", zone.height, 1, editingStageData.height, value => { zone.height = Mathf.Clamp(value, 1, editingStageData.height); NormalizeZoneOffset(zone); RebuildStageVisuals(); RebuildSettingsPanel(); }, "칸");
            AddZoneOffsetSettings(zone.width, zone.height, value => { zone.offsetX = value; RebuildStageVisuals(); RebuildSettingsPanel(); }, value => { zone.offsetY = value; RebuildStageVisuals(); RebuildSettingsPanel(); }, zone.offsetX, zone.offsetY);

            if (zone.zoneType == TransformZoneType.Rotate90)
                AddToggleRow(settingsPanel, "시계 방향 회전", zone.clockwise, value => { zone.clockwise = value; RebuildStageVisuals(); });
            else
                AddStepperRow(settingsPanel, "축", zone.mirrorAxis == MirrorAxis.Horizontal ? "가로" : "세로", () => { zone.mirrorAxis = ToggleMirrorAxis(zone.mirrorAxis); RebuildStageVisuals(); RebuildSettingsPanel(); }, () => { zone.mirrorAxis = ToggleMirrorAxis(zone.mirrorAxis); RebuildStageVisuals(); RebuildSettingsPanel(); });
        }

        private void BuildDistanceSensorSettings(DistanceSensorData sensor)
        {
            AddFloatStepper(settingsPanel, "감지 반경", sensor.detectionRadius, 0.5f, 99f, 0.5f, value => { sensor.detectionRadius = value; RebuildStageVisuals(); RebuildSettingsPanel(); });
            AddButton(settingsPanel, "트리거 추가 (X 또는 C)", StartSensorTriggerAdd, 300f, 42f);
            AddText(settingsPanel, "트리거 목록", 20, TextAlignmentOptions.Left, Color.white);

            if (sensor.triggers.Count <= 0)
                AddText(settingsPanel, "아직 연결된 트리거 없음", 17, TextAlignmentOptions.Left, new Color(0.8f, 0.84f, 0.9f, 1f));

            for (int i = 0; i < sensor.triggers.Count; i++)
            {
                int index = i;
                DistanceSensorTriggerData trigger = sensor.triggers[i];
                RectTransform row = CreateUIObject($"Trigger_{i}", settingsPanel);
                AddHorizontalLayout(row, 0, 0, 0, 0, 4);
                TMP_Text label = AddText(row, DescribeTrigger(trigger, i + 1), 16, TextAlignmentOptions.Left, new Color(0.82f, 0.88f, 0.95f, 1f));
                label.GetComponent<LayoutElement>().flexibleWidth = 1f;
                AddButton(row, "삭제", () => { sensor.triggers.RemoveAt(index); RebuildStageVisuals(); RebuildSettingsPanel(); }, 58f, 32f);
            }
        }

        private string DescribeTrigger(DistanceSensorTriggerData trigger, int displayIndex)
        {
            if (trigger == null)
                return $"트리거 {displayIndex}";

            return trigger.triggerKind switch
            {
                DistanceSensorTriggerKind.MoveWall => $"벽 이동 트리거 {displayIndex}: {trigger.wallPosition} → {trigger.wallMoveTargetPosition}",
                DistanceSensorTriggerKind.ChangeMirrorState => $"거울 상태 변경 트리거 {displayIndex}: {trigger.mirrorPosition} / {DirectionName(trigger.mirrorDirection)} / {(trigger.mirrorShape == MirrorShape.NormalL ? "정방향" : "역방향")}",
                DistanceSensorTriggerKind.ActivateTransformZone => $"변환 구역 트리거 {displayIndex}: {trigger.transformZoneId}",
                _ => $"트리거 {displayIndex}"
            };
        }

        private void CompleteNewLevelPopup()
        {
            string stageName = newStageNameInput != null ? newStageNameInput.text : "Custom Level";
            int width = ParseInt(newWidthInput, defaultWidth);
            int height = ParseInt(newHeightInput, defaultHeight);
            int laser = ParseInt(newLaserInput, defaultLaserMaxDistance);
            int move = ParseInt(newMoveInput, defaultMoveLimit);
            int bgmIndex = newBgmDropdown != null ? newBgmDropdown.value : newBgmIndex;
            newBgmIndex = Mathf.Clamp(bgmIndex, 0, BgmDisplayNames().Count - 1);
            CreateNewLevel(width, height, laser, move, laser > 0, newPlayerStartPreset, stageName, GetBgmEventPathByIndex(newBgmIndex));
            editingStageData.clearHolePosition = ResolvePlayerStartPosition(width, height, newHolePositionPreset);
            RebuildStageVisuals();
        }

        private void ApplyRuntimeBgmDropdown(int index)
        {
            if (suppressRuntimeSettingEvent || editingStageData == null)
                return;

            int finalIndex = Mathf.Clamp(index, 0, BgmDisplayNames().Count - 1);
            editingStageData.bgmEventPath = GetBgmEventPathByIndex(finalIndex);

            if (runtimeBgmDropdown != null)
            {
                runtimeBgmDropdown.SetValueWithoutNotify(finalIndex);
                runtimeBgmDropdown.RefreshShownValue();
            }

            RefreshRuntimeSettingsPanel();
            SetStatus($"레벨 BGM 변경: {BgmDisplayNames()[finalIndex]}");
        }

        private void ApplyRuntimeSettingInputs()
        {
            if (suppressRuntimeSettingEvent || editingStageData == null)
                return;

            string stageName = runtimeStageNameInput != null ? runtimeStageNameInput.text : editingStageData.stageName;
            stageName = string.IsNullOrWhiteSpace(stageName) ? "Custom Level" : stageName.Trim();
            int width = Mathf.Max(1, ParseInt(runtimeWidthInput, editingStageData.width));
            int height = Mathf.Max(1, ParseInt(runtimeHeightInput, editingStageData.height));
            int laser = Mathf.Max(0, ParseInt(runtimeLaserInput, editingStageData.laserMaxDistance));
            int move = Mathf.Max(0, ParseInt(runtimeMoveInput, editingStageData.moveLimit));

            editingStageData.stageName = stageName;
            if (string.IsNullOrWhiteSpace(selectedSaveFileName) || selectedSaveFileName == "CustomLevel")
                selectedSaveFileName = StageFilePaths.NormalizeStageFileName(stageName).Replace(".tls", string.Empty);

            editingStageData.width = width;
            editingStageData.height = height;
            editingStageData.laserMaxDistance = laser;
            editingStageData.useLaserDistanceLimit = laser > 0;
            editingStageData.moveLimit = move;
            ClampStageDataToSize();
            RebuildStageVisuals();
            RefreshRuntimeSettingsPanel();
            SetStatus("런타임 설정 반영 완료");
        }

        private void RefreshRuntimeSettingsPanel()
        {
            suppressRuntimeSettingEvent = true;
            if (runtimeStageNameInput != null) runtimeStageNameInput.SetTextWithoutNotify(string.IsNullOrWhiteSpace(editingStageData.stageName) ? "Custom Level" : editingStageData.stageName);
            if (runtimeWidthInput != null) runtimeWidthInput.SetTextWithoutNotify(editingStageData.width.ToString());
            if (runtimeHeightInput != null) runtimeHeightInput.SetTextWithoutNotify(editingStageData.height.ToString());
            if (runtimeLaserInput != null) runtimeLaserInput.SetTextWithoutNotify(editingStageData.laserMaxDistance.ToString());
            if (runtimeMoveInput != null) runtimeMoveInput.SetTextWithoutNotify(editingStageData.moveLimit.ToString());
            if (runtimeBgmDropdown != null)
            {
                runtimeBgmDropdown.SetValueWithoutNotify(FindBgmIndex(editingStageData.bgmEventPath));
                runtimeBgmDropdown.RefreshShownValue();
            }
            suppressRuntimeSettingEvent = false;

            if (gridInfoText != null)
            {
                int solutionCount = editingStageData.solutionActions != null ? editingStageData.solutionActions.Count : 0;
                string solutionText = solutionCount > 0 ? $"답안 {solutionCount} 행동" : "답안 없음";
                gridInfoText.text = $"스테이지: {(string.IsNullOrWhiteSpace(editingStageData.stageName) ? "Custom Level" : editingStageData.stageName)}\n맵: {editingStageData.width} x {editingStageData.height} / 레이저 {(editingStageData.laserMaxDistance <= 0 ? "무제한" : editingStageData.laserMaxDistance + "칸")} / 이동 {(editingStageData.moveLimit <= 0 ? "무제한" : editingStageData.moveLimit + "회")} / BGM {BgmDisplayNames()[FindBgmIndex(editingStageData.bgmEventPath)]} / {solutionText}";
            }

            if (currentFileText != null)
                currentFileText.text = string.IsNullOrWhiteSpace(currentFilePath) ? "현재 파일 없음" : currentFilePath;

            RefreshUploadButtonVisibility();
            RefreshUploadPublishButton();
        }

        private int NormalizeZoneOffsetValue(int size, int value)
        {
            if (size % 2 != 0)
                return 0;

            return value >= 0 ? 1 : -1;
        }

        private void NormalizeZoneOffset(TransformZoneData zone)
        {
            if (zone == null)
                return;

            zone.offsetX = NormalizeZoneOffsetValue(zone.width, zone.offsetX);
            zone.offsetY = NormalizeZoneOffsetValue(zone.height, zone.offsetY);
        }

        private Vector3 GetZoneVisualWorldPosition(Vector3 baseWorld, int width, int height, int offsetX, int offsetY)
        {
            return baseWorld + GetZoneVisualOffset(width, height, offsetX, offsetY);
        }

        private Vector3 GetZoneVisualOffset(int width, int height, int offsetX, int offsetY)
        {
            float x = width % 2 == 0 ? (NormalizeZoneOffsetValue(width, offsetX) > 0 ? 0.5f : -0.5f) : 0f;
            float y = height % 2 == 0 ? (NormalizeZoneOffsetValue(height, offsetY) > 0 ? 0.5f : -0.5f) : 0f;
            return new Vector3(x, y, 0f);
        }

        private Vector2Int GetZoneMinCell(TransformZoneData zone)
        {
            int minX = zone.center.x - zone.width / 2;
            int minY = zone.center.y - zone.height / 2;

            if (zone.width % 2 == 0 && NormalizeZoneOffsetValue(zone.width, zone.offsetX) > 0)
                minX += 1;

            if (zone.height % 2 == 0 && NormalizeZoneOffsetValue(zone.height, zone.offsetY) > 0)
                minY += 1;

            return new Vector2Int(minX, minY);
        }

        private void ClampStageDataToSize()
        {
            editingStageData.playerStartPosition = ClampToStage(editingStageData.playerStartPosition);
            editingStageData.wallPositions.RemoveAll(pos => !editingStageData.IsInside(pos));
            editingStageData.targetPositions.RemoveAll(pos => !editingStageData.IsInside(pos));
            editingStageData.objects.RemoveAll(obj => obj == null || !editingStageData.IsInside(obj.position));
            editingStageData.advancedTargets.RemoveAll(target => target == null || !editingStageData.IsInside(target.position));
            editingStageData.distanceSensors.RemoveAll(sensor => sensor == null || !editingStageData.IsInside(sensor.position));
            editingStageData.transformZones.RemoveAll(zone => zone == null || !editingStageData.IsInside(zone.center));
            for (int i = 0; i < editingStageData.transformZones.Count; i++)
                NormalizeZoneOffset(editingStageData.transformZones[i]);
        }

        private Vector2Int ClampToStage(Vector2Int position)
        {
            return new Vector2Int(Mathf.Clamp(position.x, 0, Mathf.Max(0, editingStageData.width - 1)), Mathf.Clamp(position.y, 0, Mathf.Max(0, editingStageData.height - 1)));
        }

        private void HandleCameraInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || editorCamera == null)
                return;

            Vector2 mousePosition = mouse.position.ReadValue();

            if (mouse.rightButton.wasPressedThisFrame)
            {
                isPanning = true;
                previousMousePosition = mousePosition;
            }

            if (mouse.rightButton.wasReleasedThisFrame)
                isPanning = false;

            if (isPanning)
            {
                Vector2 delta = mousePosition - previousMousePosition;
                previousMousePosition = mousePosition;
                editorCamera.transform.position += new Vector3(-delta.x * cameraPanSpeed, -delta.y * cameraPanSpeed, 0f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUI())
                editorCamera.orthographicSize = Mathf.Clamp(editorCamera.orthographicSize - scroll * cameraZoomSpeed * 0.03f, minCameraSize, maxCameraSize);
        }

        private void UpdateHover()
        {
            hasHoverPosition = false;
            Mouse mouse = Mouse.current;
            if (mouse == null || editorCamera == null || IsPointerOverUI())
                return;

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector3 world = editorCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -editorCamera.transform.position.z));
            hoverGridPosition = WorldToGrid(world);
            hasHoverPosition = editingStageData != null && editingStageData.IsInside(hoverGridPosition);
        }

        private void HandleShortcutInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            bool ctrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool leftShiftPressed = keyboard.leftShiftKey.isPressed;

            if (ctrlPressed && keyboard.zKey.wasPressedThisFrame)
            {
                if (leftShiftPressed)
                    RedoEditorAction();
                else
                    UndoEditorAction();
                return;
            }

            if (ctrlPressed && keyboard.yKey.wasPressedThisFrame)
            {
                RedoEditorAction();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (triggerEditMode != TriggerEditMode.None)
                {
                    triggerEditMode = TriggerEditMode.None;
                    pendingArrowRoot.SetActive(false);
                    PlayEditorSfx(FmodRuntimeAudio.SfxUiCancel);
                    SetStatus("트리거 추가 취소");
                    RebuildStageVisuals();
                    RebuildSettingsPanel();
                    return;
                }

                if (selectedTool != null)
                {
                    selectedTool = null;
                    ghostRoot.SetActive(false);
                    rangePreviewRoot.SetActive(false);
                    PlayEditorSfx(FmodRuntimeAudio.SfxUiCancel);
                    RebuildPalette();
                    RebuildSettingsPanel();
                    SetStatus("배치 취소");
                    return;
                }

                if (IsAnyPopupOpen())
                    HideAllPopups(true);
                else
                    ShowMainPopup();

                return;
            }

            if (keyboard.xKey.wasPressedThisFrame)
            {
                HandleXPressed();
                return;
            }

            if (keyboard.bKey.wasPressedThisFrame && triggerEditMode == TriggerEditMode.WaitingPrismDirection)
            {
                pendingMirrorShape = pendingMirrorShape == MirrorShape.NormalL ? MirrorShape.ReverseL : MirrorShape.NormalL;
                SetStatus($"거울 트리거: {(pendingMirrorShape == MirrorShape.NormalL ? "정방향" : "역방향")} / C,V 회전 / B 정역 변경 / X 확정");
                RebuildStageVisuals();
                return;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                if (triggerEditMode == TriggerEditMode.WaitingPrismDirection)
                {
                    pendingPrismDirection = pendingPrismDirection.RotateCounterClockwise();
                    SetStatus($"거울 트리거: {DirectionName(pendingPrismDirection)} / {(pendingMirrorShape == MirrorShape.NormalL ? "정방향" : "역방향")} / B 정역 변경 / X 확정");
                    RebuildStageVisuals();
                    return;
                }

                if (selectedElementKind == SelectedElementKind.DistanceSensor && selectedSensor != null && triggerEditMode == TriggerEditMode.None)
                {
                    StartSensorTriggerAdd();
                    return;
                }

                RotateCurrentDirection(true);
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                if (triggerEditMode == TriggerEditMode.WaitingPrismDirection)
                {
                    pendingPrismDirection = pendingPrismDirection.RotateClockwise();
                    SetStatus($"거울 트리거: {DirectionName(pendingPrismDirection)} / {(pendingMirrorShape == MirrorShape.NormalL ? "정방향" : "역방향")} / B 정역 변경 / X 확정");
                    RebuildStageVisuals();
                    return;
                }

                RotateCurrentDirection(false);
            }
        }

        private void HandleXPressed()
        {
            if (triggerEditMode == TriggerEditMode.WaitingPrismDirection)
            {
                AddMirrorStateTrigger();
                return;
            }

            if (triggerEditMode != TriggerEditMode.None)
            {
                triggerEditMode = TriggerEditMode.None;
                pendingArrowRoot.SetActive(false);
                SetStatus("트리거 추가 취소");
                RebuildStageVisuals();
                return;
            }

            if (selectedElementKind == SelectedElementKind.DistanceSensor && selectedSensor != null)
                StartSensorTriggerAdd();
        }

        private void RotateCurrentDirection(bool clockwise)
        {
            if (selectedElementKind == SelectedElementKind.Object && selectedObject != null)
            {
                if (!ShouldShowDirectionSetting(selectedObject))
                    return;

                selectedObject.direction = clockwise ? selectedObject.direction.RotateClockwise() : selectedObject.direction.RotateCounterClockwise();
                RebuildStageVisuals();
                RebuildSettingsPanel();
                return;
            }

            if (selectedElementKind == SelectedElementKind.PlayerStart)
            {
                editingStageData.playerStartDirection = clockwise ? editingStageData.playerStartDirection.RotateClockwise() : editingStageData.playerStartDirection.RotateCounterClockwise();
                RebuildStageVisuals();
                RebuildSettingsPanel();
                return;
            }

            if (selectedTool != null && selectedTool.HasDirection)
            {
                placementSettings.Direction = clockwise ? placementSettings.Direction.RotateClockwise() : placementSettings.Direction.RotateCounterClockwise();
                RebuildSettingsPanel();
            }
        }

        private void UpdateGhostAndRange()
        {
            if (!hasHoverPosition || selectedTool == null || selectedElementKind != SelectedElementKind.None)
            {
                ghostRoot.SetActive(false);
                rangePreviewRoot.SetActive(false);
                ClearChildren(rangePreviewRoot.transform);
                return;
            }

            Vector3 world = GridToWorld(hoverGridPosition);
            ghostRoot.SetActive(true);
            DrawToolVisual(ghostRoot.transform, selectedTool.ToolType, world, placementSettings.Direction, placementSettings, true, "Ghost");
            ClearChildren(rangePreviewRoot.transform);
            UpdateRangePreview(world);
        }

        private void UpdateRangePreview(Vector3 world)
        {
            rangePreviewRoot.SetActive(false);

            if (selectedTool == null)
                return;

            if (selectedTool.ToolType == ToolType.ZoneRotate || selectedTool.ToolType == ToolType.ZoneMirror)
            {
                rangePreviewRoot.SetActive(true);
                DrawRange(rangePreviewRoot.transform, GetZoneVisualWorldPosition(world, placementSettings.ZoneWidth, placementSettings.ZoneHeight, placementSettings.ZoneOffsetX, placementSettings.ZoneOffsetY), placementSettings.ZoneWidth, placementSettings.ZoneHeight, selectedTool.ToolType == ToolType.ZoneRotate ? new Color(0.15f, 0.7f, 1f, 0.22f) : new Color(0.85f, 0.25f, 1f, 0.22f));
                return;
            }

            if (selectedTool.ToolType == ToolType.DistanceSensor)
            {
                int size = Mathf.Max(1, Mathf.CeilToInt(placementSettings.DetectionRadius * 2f) + 1);
                rangePreviewRoot.SetActive(true);
                DrawRange(rangePreviewRoot.transform, world, size, size, new Color(0.2f, 1f, 0.7f, 0.18f));
                return;
            }

            if (selectedTool.ToolType == ToolType.TargetIntersection)
            {
                rangePreviewRoot.SetActive(true);
                DrawRange(rangePreviewRoot.transform, world, 1, 1, new Color(1f, 0.2f, 1f, 0.18f));
            }
        }

        private void HandleMouseClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || IsPointerOverUI() || !hasHoverPosition)
                return;

            if (!mouse.leftButton.wasPressedThisFrame)
                return;

            if (triggerEditMode != TriggerEditMode.None)
            {
                HandleTriggerPlacementClick(hoverGridPosition);
                return;
            }

            if (selectedTool != null && selectedElementKind == SelectedElementKind.None)
            {
                PlaceSelectedTool(hoverGridPosition);
                return;
            }

            SelectElementAt(hoverGridPosition);

            if (selectedElementKind != SelectedElementKind.None)
            {
                isDraggingElement = true;
                dragLastGridPosition = hoverGridPosition;
            }
        }

        private void HandleElementDrag()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                isDraggingElement = false;
                return;
            }

            if (!isDraggingElement || !mouse.leftButton.isPressed || !hasHoverPosition || IsPointerOverUI())
                return;

            if (hoverGridPosition == dragLastGridPosition)
                return;

            MoveSelectedElementTo(hoverGridPosition);
            dragLastGridPosition = hoverGridPosition;
        }

        private void MoveSelectedElementTo(Vector2Int position)
        {
            if (editingStageData == null || !editingStageData.IsInside(position))
                return;

            if (position == selectedPosition)
                return;

            switch (selectedElementKind)
            {
                case SelectedElementKind.PlayerStart:
                    editingStageData.playerStartPosition = position;
                    selectedPosition = position;
                    break;

                case SelectedElementKind.ClearHole:
                    editingStageData.clearHolePosition = position;
                    selectedPosition = position;
                    break;

                case SelectedElementKind.Wall:
                    if (editingStageData.wallPositions.Contains(selectedPosition))
                    {
                        ClearBlockingAt(position, true);
                        editingStageData.wallPositions.RemoveAll(pos => pos == selectedPosition);
                        AddUnique(editingStageData.wallPositions, position);
                        selectedPosition = position;
                    }
                    break;

                case SelectedElementKind.Target:
                    if (selectedTarget != null)
                    {
                        StageTargetData movingTarget = selectedTarget;
                        bool wasNormalTarget = movingTarget.targetType == TargetType.Normal && movingTarget.requiredColor == LaserColorKind.Default;
                        editingStageData.targetPositions.RemoveAll(pos => pos == selectedPosition || pos == position);
                        editingStageData.advancedTargets.RemoveAll(target => target == movingTarget || (target != null && target.position == selectedPosition) || (target != null && target.position == position));
                        ClearBlockingAt(position, false);
                        movingTarget.position = position;

                        if (wasNormalTarget)
                            AddUnique(editingStageData.targetPositions, position);
                        else
                            editingStageData.advancedTargets.Add(movingTarget);

                        selectedTarget = FindTargetAt(position);
                        selectedPosition = position;
                    }
                    break;

                case SelectedElementKind.Object:
                    if (selectedObject != null)
                    {
                        ClearBlockingAt(position, true, selectedObject);
                        selectedObject.position = position;
                        selectedPosition = position;
                    }
                    break;

                case SelectedElementKind.DistanceSensor:
                    if (selectedSensor != null)
                    {
                        editingStageData.distanceSensors.RemoveAll(sensor => sensor != null && sensor != selectedSensor && sensor.position == position);
                        selectedSensor.position = position;
                        selectedPosition = position;
                    }
                    break;

                case SelectedElementKind.TransformZone:
                    if (selectedZone != null)
                    {
                        selectedZone.center = position;
                        selectedPosition = position;
                    }
                    break;
            }

            RebuildSequencePattern();
            RebuildStageVisuals();
            RebuildSettingsPanel();
        }

        private void PlaceSelectedTool(Vector2Int position)
        {
            if (selectedTool == null || !editingStageData.IsInside(position))
                return;

            switch (selectedTool.ToolType)
            {
                case ToolType.PlayerStart:
                    editingStageData.playerStartPosition = position;
                    editingStageData.playerStartDirection = placementSettings.Direction;
                    break;

                case ToolType.ClearHole:
                    editingStageData.clearHolePosition = position;
                    break;

                case ToolType.TargetNormal:
                    ClearBlockingAt(position, false);
                    StageTargetData normalTarget = new StageTargetData
                    {
                        targetId = CreateId("Target", position),
                        position = position,
                        targetType = placementSettings.Color == LaserColorKind.Default ? TargetType.Normal : TargetType.ColorLocked,
                        requiredColor = placementSettings.Color,
                        stopLaserOnHit = true
                    };
                    AddTargetData(normalTarget);
                    break;

                case ToolType.TargetSequence:
                    ClearBlockingAt(position, false);
                    StageTargetData sequenceTarget = new StageTargetData
                    {
                        targetId = CreateId("SequenceTarget", position),
                        position = position,
                        targetType = placementSettings.Color == LaserColorKind.Default ? TargetType.SequenceLocked : TargetType.SequenceColorLocked,
                        requiredColor = placementSettings.Color,
                        sequenceValue = GetAvailableSequenceIndex(placementSettings.SequenceIndex, null),
                        stopLaserOnHit = !placementSettings.TargetPassThrough
                    };
                    AddTargetData(sequenceTarget);
                    RebuildSequencePattern();
                    break;

                case ToolType.TargetIntersection:
                    ClearBlockingAt(position, false);
                    StageTargetData intersectionTarget = new StageTargetData
                    {
                        targetId = CreateId("IntersectionTarget", position),
                        position = position,
                        targetType = TargetType.Intersection,
                        detectionRadius = 0.25f,
                        requiredIntersectionCount = placementSettings.IntersectionCount,
                        intersectionColors = new List<LaserColorKind>(placementSettings.IntersectionColors),
                        stopLaserOnHit = false
                    };
                    AddTargetData(intersectionTarget);
                    break;

                case ToolType.Wall:
                    ClearBlockingAt(position, true);
                    if (placementSettings.Pushable)
                        editingStageData.objects.Add(CreateWallObject(position, placementSettings.Direction, ManipulationType.PushOnly));
                    else
                        AddUnique(editingStageData.wallPositions, position);
                    break;

                case ToolType.Mirror:
                    ClearBlockingAt(position, true);
                    editingStageData.objects.Add(CreateMirrorObject(position));
                    break;

                case ToolType.PrismSplitter:
                    ClearBlockingAt(position, true);
                    editingStageData.objects.Add(CreatePrismObject(position, PrismType.Splitter));
                    break;

                case ToolType.PrismColor:
                    ClearBlockingAt(position, true);
                    editingStageData.objects.Add(CreatePrismObject(position, PrismType.Color));
                    break;

                case ToolType.PrismRefraction:
                    ClearBlockingAt(position, true);
                    editingStageData.objects.Add(CreatePrismObject(position, PrismType.Refraction));
                    break;

                case ToolType.ZoneRotate:
                    editingStageData.transformZones.RemoveAll(zone => zone != null && IsPositionInsideZone(position, zone));
                    editingStageData.transformZones.Add(CreateTransformZone(position, TransformZoneType.Rotate90));
                    break;

                case ToolType.ZoneMirror:
                    editingStageData.transformZones.RemoveAll(zone => zone != null && IsPositionInsideZone(position, zone));
                    editingStageData.transformZones.Add(CreateTransformZone(position, TransformZoneType.Mirror));
                    break;

                case ToolType.DistanceSensor:
                    editingStageData.distanceSensors.RemoveAll(sensor => sensor != null && sensor.position == position);
                    editingStageData.distanceSensors.Add(new DistanceSensorData { sensorId = CreateId("Sensor", position), position = position, detectionRadius = Mathf.Max(0.5f, placementSettings.DetectionRadius) });
                    break;

                case ToolType.LensAmplifier:
                    ClearBlockingAt(position, true);
                    editingStageData.objects.Add(CreateLensObject(position));
                    break;

                case ToolType.Eraser:
                    ClearAt(position);
                    break;
            }

            PlayEditorSfx(selectedTool.ToolType == ToolType.Eraser ? FmodRuntimeAudio.SfxEditorObjRemove : FmodRuntimeAudio.SfxEditorObjPlaced);
            selectedTool = null;
            RebuildSequencePattern();
            RebuildStageVisuals();
            SelectElementAt(position);
            RebuildPalette();
        }

        private StageObjectData CreateWallObject(Vector2Int position, GridDirection direction, ManipulationType manipulation)
        {
            return new StageObjectData { objectType = PuzzleObjectType.Wall, manipulationType = manipulation, position = position, direction = direction };
        }

        private StageObjectData CreateMirrorObject(Vector2Int position)
        {
            return new StageObjectData { objectType = PuzzleObjectType.Mirror, manipulationType = ResolveManipulation(placementSettings.Pushable, placementSettings.Rotatable), position = position, direction = placementSettings.Direction, mirrorShape = placementSettings.Reverse ? MirrorShape.ReverseL : MirrorShape.NormalL };
        }

        private StageObjectData CreatePrismObject(Vector2Int position, PrismType prismType)
        {
            StageObjectData data = new StageObjectData { objectType = PuzzleObjectType.Prism, manipulationType = ResolveManipulation(placementSettings.Pushable, placementSettings.Rotatable), position = position, direction = placementSettings.Direction, prismType = prismType };
            data.splitterMode = placementSettings.BranchCount >= 3 ? PrismSplitterMode.ForwardLeftRight : placementSettings.SplitterMode;
            data.prismColor = placementSettings.Color == LaserColorKind.Default ? LaserColorKind.Red : placementSettings.Color;
            data.refractionMode = placementSettings.Reverse ? RefractionMode.CounterClockwise45 : RefractionMode.Clockwise45;
            return data;
        }

        private StageObjectData CreateLensObject(Vector2Int position)
        {
            return new StageObjectData { objectType = PuzzleObjectType.Lens, manipulationType = ManipulationType.None, position = position, direction = GridDirection.Up, lensType = LensType.DistanceAmplifier, distanceBoost = Mathf.Max(1, placementSettings.DistanceBoost) };
        }

        private TransformZoneData CreateTransformZone(Vector2Int position, TransformZoneType zoneType)
        {
            TransformZoneData zone = new TransformZoneData
            {
                zoneId = CreateId(zoneType == TransformZoneType.Rotate90 ? "RotateZone" : "MirrorZone", position),
                center = position,
                width = Mathf.Clamp(placementSettings.ZoneWidth, 1, editingStageData.width),
                height = Mathf.Clamp(placementSettings.ZoneHeight, 1, editingStageData.height),
                zoneType = zoneType,
                clockwise = placementSettings.Clockwise,
                mirrorAxis = placementSettings.MirrorAxis,
                offsetX = NormalizeZoneOffsetValue(placementSettings.ZoneWidth, placementSettings.ZoneOffsetX),
                offsetY = NormalizeZoneOffsetValue(placementSettings.ZoneHeight, placementSettings.ZoneOffsetY)
            };

            NormalizeZoneOffset(zone);
            return zone;
        }

        private void SelectElementAt(Vector2Int position)
        {
            ClearSelectionReferencesOnly();
            selectedPosition = position;
            bool playedSelectionSound = false;

            DistanceSensorData sensor = FindSensorAt(position);
            if (sensor != null)
            {
                selectedElementKind = SelectedElementKind.DistanceSensor;
                selectedSensor = sensor;
                if (!playedSelectionSound) { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); playedSelectionSound = true; }
                UpdateDescription("거리 감응 타일", "레이저 선분이 감지 반경 안을 지나면 연결된 트리거를 실행한다. X 또는 C로 트리거 추가를 시작한다.");
                RebuildSettingsPanel();
                RebuildStageVisuals();
                return;
            }

            StageTargetData target = FindTargetAt(position);
            if (target != null)
            {
                selectedElementKind = SelectedElementKind.Target;
                selectedTarget = target;
                if (!playedSelectionSound) { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); playedSelectionSound = true; }
                UpdateDescription("도착지", "도착지 타입과 색상/순서/교차 조건을 수정할 수 있다.");
                RebuildSettingsPanel();
                RebuildStageVisuals();
                return;
            }

            StageObjectData obj = FindObjectAt(position);
            if (obj != null)
            {
                selectedElementKind = SelectedElementKind.Object;
                selectedObject = obj;
                if (!playedSelectionSound) { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); playedSelectionSound = true; }
                UpdateDescription(GetObjectDisplayName(obj), "오브젝트의 방향과 상호작용 조건을 수정할 수 있다.");
                RebuildSettingsPanel();
                RebuildStageVisuals();
                return;
            }

            if (editingStageData.wallPositions.Contains(position))
            {
                selectedElementKind = SelectedElementKind.Wall;
                if (!playedSelectionSound) { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); playedSelectionSound = true; }
                UpdateDescription("벽", "고정 벽이다. 밀 수 있음으로 바꾸면 오브젝트 벽이 된다.");
                RebuildSettingsPanel();
                RebuildStageVisuals();
                return;
            }

            if (editingStageData.playerStartPosition == position)
            {
                selectedElementKind = SelectedElementKind.PlayerStart;
                if (!playedSelectionSound) { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); playedSelectionSound = true; }
                UpdateDescription("플레이어 시작점", "플레이어 시작 방향을 수정할 수 있다.");
                RebuildSettingsPanel();
                RebuildStageVisuals();
                return;
            }

            if (editingStageData.clearHolePosition == position)
            {
                selectedElementKind = SelectedElementKind.ClearHole;
                if (!playedSelectionSound) { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); playedSelectionSound = true; }
                UpdateDescription("구멍 위치", "목적지를 모두 활성화하면 생성되는 클리어 구멍 위치다. 드래그해서 위치를 바꿀 수 있다.");
                RebuildSettingsPanel();
                RebuildStageVisuals();
                return;
            }

            TransformZoneData zone = FindZoneAt(position);
            if (zone != null)
            {
                selectedElementKind = SelectedElementKind.TransformZone;
                selectedZone = zone;
                if (!playedSelectionSound) { PlayEditorSfx(FmodRuntimeAudio.SfxEditorSelect2); playedSelectionSound = true; }
                UpdateDescription(zone.zoneType == TransformZoneType.Rotate90 ? "회전 구역" : "대칭 구역", "구역 크기와 변환 방식을 수정할 수 있다.");
                RebuildSettingsPanel();
                RebuildStageVisuals();
                return;
            }

            ClearSelection();
        }

        private void ClearSelection()
        {
            selectedTool = null;
            ClearSelectionReferencesOnly();
            RebuildSettingsPanel();
            RebuildPalette();
            RebuildStageVisuals();
            UpdateDescription("선택 없음", "왼쪽 기능 패널에서 배치할 기능을 선택하거나 맵 위 오브젝트를 클릭하세요.");
        }

        private void ClearSelectionReferencesOnly()
        {
            isDraggingElement = false;
            selectedElementKind = SelectedElementKind.None;
            selectedTarget = null;
            selectedObject = null;
            selectedSensor = null;
            selectedZone = null;
            triggerEditMode = TriggerEditMode.None;
            pendingArrowRoot.SetActive(false);
        }

        private void ClearAt(Vector2Int position)
        {
            editingStageData.wallPositions.RemoveAll(p => p == position);
            editingStageData.targetPositions.RemoveAll(p => p == position);
            editingStageData.advancedTargets.RemoveAll(target => target != null && target.position == position);
            editingStageData.objects.RemoveAll(obj => obj != null && obj.position == position);
            editingStageData.distanceSensors.RemoveAll(sensor => sensor != null && sensor.position == position);
            editingStageData.transformZones.RemoveAll(zone => zone != null && IsPositionInsideZone(position, zone));
            RemoveTriggersPointingTo(position);
        }

        private void ClearBlockingAt(Vector2Int position, bool clearTargets)
        {
            ClearBlockingAt(position, clearTargets, null);
        }

        private void ClearBlockingAt(Vector2Int position, bool clearTargets, StageObjectData ignoreObject)
        {
            editingStageData.wallPositions.RemoveAll(p => p == position);
            editingStageData.objects.RemoveAll(obj => obj != null && obj != ignoreObject && obj.position == position);

            if (clearTargets)
            {
                editingStageData.targetPositions.RemoveAll(p => p == position);
                editingStageData.advancedTargets.RemoveAll(target => target != null && target.position == position);
            }
        }

        private void ConvertSelectedWallPushable(bool pushable)
        {
            if (!pushable)
                return;

            editingStageData.wallPositions.RemoveAll(pos => pos == selectedPosition);
            editingStageData.objects.Add(CreateWallObject(selectedPosition, GridDirection.Up, ManipulationType.PushOnly));
            RebuildStageVisuals();
            SelectElementAt(selectedPosition);
        }



        private void ConvertObjectWallPushable(StageObjectData wallObject, bool pushable)
        {
            if (wallObject == null || wallObject.objectType != PuzzleObjectType.Wall)
                return;

            if (pushable)
            {
                wallObject.manipulationType = ManipulationType.PushOnly;
                RebuildStageVisuals();
                RebuildSettingsPanel();
                return;
            }

            Vector2Int position = wallObject.position;
            editingStageData.objects.Remove(wallObject);
            AddUnique(editingStageData.wallPositions, position);
            selectedElementKind = SelectedElementKind.Wall;
            selectedObject = null;
            selectedPosition = position;
            RebuildStageVisuals();
            RebuildSettingsPanel();
        }

        private void AddTargetData(StageTargetData target)
        {
            editingStageData.targetPositions.RemoveAll(pos => pos == target.position);
            editingStageData.advancedTargets.RemoveAll(existing => existing != null && existing.position == target.position);

            if (target.targetType == TargetType.Normal && target.requiredColor == LaserColorKind.Default)
                editingStageData.targetPositions.Add(target.position);
            else
                editingStageData.advancedTargets.Add(target);
        }

        private void RemoveTarget(StageTargetData target)
        {
            if (target == null)
                return;

            editingStageData.targetPositions.RemoveAll(pos => pos == target.position);
            editingStageData.advancedTargets.Remove(target);
            RebuildSequencePattern();
        }

        private void RemoveTargetAt(Vector2Int position, StageTargetData ignoreTarget)
        {
            editingStageData.targetPositions.RemoveAll(pos => pos == position);
            editingStageData.advancedTargets.RemoveAll(target => target != null && target != ignoreTarget && target.position == position);
        }

        private void RemoveTriggersPointingTo(Vector2Int position)
        {
            for (int i = 0; i < editingStageData.distanceSensors.Count; i++)
            {
                DistanceSensorData sensor = editingStageData.distanceSensors[i];
                if (sensor == null || sensor.triggers == null)
                    continue;

                sensor.triggers.RemoveAll(trigger => trigger != null && (trigger.wallPosition == position || trigger.wallMoveTargetPosition == position || trigger.prismPosition == position || trigger.mirrorPosition == position));
            }
        }

        private void StartSensorTriggerAdd()
        {
            if (selectedSensor == null)
                return;

            triggerEditMode = TriggerEditMode.WaitingTarget;
            PlayEditorSfx(FmodRuntimeAudio.SfxEditorTrigger);
            SetStatus("트리거 추가: 벽/거울/변환 구역을 클릭하세요. X를 다시 누르면 취소됩니다.");
            RebuildStageVisuals();
        }

        private void HandleTriggerPlacementClick(Vector2Int position)
        {
            if (selectedSensor == null)
                return;

            if (triggerEditMode == TriggerEditMode.WaitingTarget)
            {
                if (editingStageData.wallPositions.Contains(position))
                {
                    pendingWallPosition = position;
                    triggerEditMode = TriggerEditMode.WaitingWallDestination;
                    SetStatus("벽 이동 트리거: 벽이 이동할 위치를 클릭하세요.");
                    return;
                }

                StageObjectData obj = FindObjectAt(position);
                if (obj != null && obj.objectType == PuzzleObjectType.Mirror)
                {
                    pendingPrismPosition = position;
                    pendingPrismDirection = obj.direction;
                    pendingMirrorShape = obj.mirrorShape;
                    triggerEditMode = TriggerEditMode.WaitingPrismDirection;
                    SetStatus($"거울 상태 변경 트리거: C/V 회전, B 정방향/역방향 변경, X 확정. 현재 {(pendingMirrorShape == MirrorShape.NormalL ? "정방향" : "역방향")}");
                    RebuildStageVisuals();
                    return;
                }

                TransformZoneData zone = FindZoneAt(position);
                if (zone != null)
                {
                    selectedSensor.triggers.Add(new DistanceSensorTriggerData { triggerId = CreateId("ZoneTrigger", selectedSensor.position), triggerKind = DistanceSensorTriggerKind.ActivateTransformZone, transformZoneId = zone.zoneId });
                    PlayEditorSfx(FmodRuntimeAudio.SfxEditorTrigger);
                    triggerEditMode = TriggerEditMode.None;
                    SetStatus("변환 구역 트리거 추가 완료");
                    RebuildStageVisuals();
                    RebuildSettingsPanel();
                    return;
                }

                SetStatus("트리거 대상이 아님: 벽, 거울, 변환 구역을 클릭하세요.");
                return;
            }

            if (triggerEditMode == TriggerEditMode.WaitingWallDestination)
            {
                selectedSensor.triggers.Add(new DistanceSensorTriggerData { triggerId = CreateId("WallMoveTrigger", selectedSensor.position), triggerKind = DistanceSensorTriggerKind.MoveWall, wallPosition = pendingWallPosition, wallMoveTargetPosition = position });
                PlayEditorSfx(FmodRuntimeAudio.SfxEditorTrigger);
                triggerEditMode = TriggerEditMode.None;
                SetStatus("벽 이동 트리거 추가 완료");
                RebuildStageVisuals();
                RebuildSettingsPanel();
            }
        }

        private void AddMirrorStateTrigger()
        {
            if (selectedSensor == null)
                return;

            selectedSensor.triggers.Add(new DistanceSensorTriggerData { triggerId = CreateId("MirrorStateTrigger", selectedSensor.position), triggerKind = DistanceSensorTriggerKind.ChangeMirrorState, mirrorPosition = pendingPrismPosition, mirrorDirection = pendingPrismDirection, mirrorShape = pendingMirrorShape });
            PlayEditorSfx(FmodRuntimeAudio.SfxEditorTrigger);
            triggerEditMode = TriggerEditMode.None;
            pendingArrowRoot.SetActive(false);
            SetStatus("거울 상태 변경 트리거 추가 완료");
            RebuildStageVisuals();
            RebuildSettingsPanel();
        }

        private void RebuildStageVisuals()
        {
            ClearStageVisuals();

            if (editingStageData == null)
                return;

            BuildGridVisuals();
            BuildZoneVisuals();
            BuildTargetVisuals();
            BuildWallVisuals();
            BuildObjectVisuals();
            BuildSensorVisuals();
            BuildPlayerStartVisual();
            BuildClearHoleVisual();
            BuildTriggerLines();
            BuildPendingArrow();
            RefreshRuntimeSettingsPanel();
        }

        private void ClearStageVisuals()
        {
            for (int i = 0; i < stageVisuals.Count; i++)
            {
                if (stageVisuals[i] != null)
                    Destroy(stageVisuals[i]);
            }

            stageVisuals.Clear();
            cellVisuals.Clear();
            ClearChildren(ghostRoot.transform);
            ClearChildren(rangePreviewRoot.transform);
            ClearChildren(pendingArrowRoot.transform);
        }

        private void BuildGridVisuals()
        {
            for (int y = 0; y < editingStageData.height; y++)
            {
                for (int x = 0; x < editingStageData.width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    GameObject cell = CreateSpriteObject($"Cell_{x}_{y}", stageRoot.transform, GridToWorld(position), new Vector2(0.96f, 0.96f), new Color(0.12f, 0.14f, 0.18f, 1f), -20);
                    cellVisuals.Add(position, cell);
                    stageVisuals.Add(cell);
                }
            }
        }

        private void BuildZoneVisuals()
        {
            for (int i = 0; i < editingStageData.transformZones.Count; i++)
            {
                TransformZoneData zone = editingStageData.transformZones[i];
                if (zone == null)
                    continue;

                GameObject root = new GameObject($"Zone_{zone.zoneId}");
                root.transform.SetParent(stageRoot.transform);
                root.transform.position = GridToWorld(zone.center);
                DrawRange(root.transform, GetZoneVisualOffset(zone.width, zone.height, zone.offsetX, zone.offsetY), zone.width, zone.height, zone.zoneType == TransformZoneType.Rotate90 ? new Color(0.15f, 0.55f, 1f, 0.22f) : new Color(0.85f, 0.25f, 1f, 0.22f));
                AddWorldLabel(root.transform, zone.zoneType == TransformZoneType.Rotate90 ? "회전" : "대칭", Vector3.zero, 0.22f, Color.white, 10);
                stageVisuals.Add(root);
            }
        }

        private void BuildTargetVisuals()
        {
            for (int i = 0; i < editingStageData.targetPositions.Count; i++)
                DrawTarget(editingStageData.targetPositions[i], TargetType.Normal, LaserColorKind.Default, 0, 2, null);

            for (int i = 0; i < editingStageData.advancedTargets.Count; i++)
            {
                StageTargetData target = editingStageData.advancedTargets[i];
                if (target == null)
                    continue;

                DrawTarget(target.position, target.targetType, target.requiredColor, target.sequenceValue, target.requiredIntersectionCount, target.intersectionColors);
            }
        }

        private void DrawTarget(Vector2Int position, TargetType targetType, LaserColorKind color, int sequenceValue, int intersectionCount, List<LaserColorKind> intersectionColors)
        {
            GameObject root = new GameObject($"Target_{position}");
            root.transform.SetParent(stageRoot.transform);
            root.transform.position = GridToWorld(position);
            Color visualColor = ResolveLaserColor(color, targetType == TargetType.Intersection ? new Color(1f, 0.35f, 1f, 1f) : Color.white);
            CreateSpriteObject("TargetCircle", root.transform, Vector3.zero, new Vector2(0.72f, 0.72f), visualColor, -4);
            string label = "T";
            Color labelColor = Color.black;
            if (targetType == TargetType.SequenceLocked || targetType == TargetType.SequenceColorLocked) label = sequenceValue.ToString();
            if (targetType == TargetType.Intersection)
            {
                label = BuildIntersectionLabel(intersectionCount, intersectionColors);
                labelColor = Color.white;
            }
            AddWorldLabel(root.transform, label, Vector3.zero, 0.34f, labelColor, 15);
            stageVisuals.Add(root);
        }

        private void BuildWallVisuals()
        {
            for (int i = 0; i < editingStageData.wallPositions.Count; i++)
            {
                Vector2Int position = editingStageData.wallPositions[i];
                GameObject wall = CreateSpriteObject($"Wall_{position}", stageRoot.transform, GridToWorld(position), new Vector2(0.88f, 0.88f), new Color(0.36f, 0.38f, 0.43f, 1f), -3);
                AddWorldLabel(wall.transform, "벽", Vector3.zero, 0.24f, Color.white, 15);
                stageVisuals.Add(wall);
            }
        }

        private void BuildObjectVisuals()
        {
            for (int i = 0; i < editingStageData.objects.Count; i++)
            {
                StageObjectData obj = editingStageData.objects[i];
                if (obj == null)
                    continue;

                GameObject root = new GameObject($"Object_{obj.objectType}_{obj.position}");
                root.transform.SetParent(stageRoot.transform);
                root.transform.position = GridToWorld(obj.position);
                DrawObjectVisual(root.transform, obj, false);
                stageVisuals.Add(root);
            }
        }

        private void BuildSensorVisuals()
        {
            for (int i = 0; i < editingStageData.distanceSensors.Count; i++)
            {
                DistanceSensorData sensor = editingStageData.distanceSensors[i];
                if (sensor == null)
                    continue;

                GameObject root = new GameObject($"Sensor_{sensor.position}");
                root.transform.SetParent(stageRoot.transform);
                root.transform.position = GridToWorld(sensor.position);
                CreateSpriteObject("Sensor", root.transform, Vector3.zero, new Vector2(0.58f, 0.58f), new Color(0.25f, 1f, 0.65f, 1f), 2);
                AddWorldLabel(root.transform, "감응", Vector3.zero, 0.18f, Color.black, 16);
                if (selectedSensor == sensor)
                {
                    int size = Mathf.Max(1, Mathf.CeilToInt(sensor.detectionRadius * 2f) + 1);
                    DrawRange(root.transform, Vector3.zero, size, size, new Color(0.25f, 1f, 0.65f, 0.12f));
                }
                stageVisuals.Add(root);
            }
        }

        private void BuildPlayerStartVisual()
        {
            GameObject root = new GameObject("PlayerStart");
            root.transform.SetParent(stageRoot.transform);
            root.transform.position = GridToWorld(editingStageData.playerStartPosition);
            CreateSpriteObject("Player", root.transform, Vector3.zero, new Vector2(0.62f, 0.62f), new Color(0.2f, 0.65f, 1f, 1f), 4);
            AddWorldLabel(root.transform, "P", Vector3.zero, 0.34f, Color.white, 16);
            DrawDirectionArrow(root.transform, Vector3.zero, editingStageData.playerStartDirection, new Color(0.2f, 0.85f, 1f, 1f), 20);
            stageVisuals.Add(root);
        }

        private void BuildClearHoleVisual()
        {
            if (editingStageData == null || !editingStageData.IsInside(editingStageData.clearHolePosition))
                return;

            GameObject hole = CreateSpriteObject($"ClearHole_{editingStageData.clearHolePosition}", stageRoot.transform, GridToWorld(editingStageData.clearHolePosition), new Vector2(0.68f, 0.68f), new Color(0f, 0f, 0f, 0.82f), -2);
            AddWorldLabel(hole.transform, "구멍", Vector3.zero, 0.18f, Color.white, 16);
            if (selectedElementKind == SelectedElementKind.ClearHole)
                CreateSpriteObject("SelectedClearHoleOutline", hole.transform, Vector3.zero, new Vector2(0.84f, 0.84f), new Color(1f, 0.85f, 0.2f, 0.35f), -1);
            stageVisuals.Add(hole);
        }

        private void BuildTriggerLines()
        {
            for (int i = 0; i < triggerLineVisuals.Count; i++)
            {
                if (triggerLineVisuals[i] != null)
                    Destroy(triggerLineVisuals[i]);
            }
            triggerLineVisuals.Clear();

            if (selectedSensor == null)
                return;

            Vector3 from = GridToWorld(selectedSensor.position);
            Color lineColor = new Color(1f, 0.18f, 0.18f, 0.95f);

            for (int i = 0; i < selectedSensor.triggers.Count; i++)
            {
                DistanceSensorTriggerData trigger = selectedSensor.triggers[i];
                if (trigger == null)
                    continue;

                if (trigger.triggerKind == DistanceSensorTriggerKind.MoveWall)
                {
                    Vector3 wall = GridToWorld(trigger.wallPosition);
                    Vector3 destination = GridToWorld(trigger.wallMoveTargetPosition);
                    triggerLineVisuals.Add(CreateLine($"TriggerLine_{i}_SensorToWall", from, wall, lineColor, 0.045f, 35));
                    triggerLineVisuals.Add(CreateLine($"TriggerLine_{i}_WallToDestination", wall, destination, lineColor, 0.045f, 35));
                    GameObject marker = CreateSpriteObject($"WallMoveDestination_{i}", stageRoot.transform, destination, new Vector2(0.36f, 0.36f), new Color(1f, 0.18f, 0.18f, 0.85f), 36);
                    triggerLineVisuals.Add(marker);
                    continue;
                }

                if (trigger.triggerKind == DistanceSensorTriggerKind.ChangeMirrorState)
                {
                    Vector3 mirror = GridToWorld(trigger.mirrorPosition);
                    triggerLineVisuals.Add(CreateLine($"TriggerLine_{i}_Mirror", from, mirror, lineColor, 0.045f, 35));
                    GameObject mirrorRoot = new GameObject($"MirrorStateTrigger_{i}");
                    mirrorRoot.transform.SetParent(stageRoot.transform);
                    mirrorRoot.transform.position = mirror;
                    DrawMirrorOpenSides(mirrorRoot.transform, trigger.mirrorDirection, trigger.mirrorShape, Color.yellow, 36);
                    AddWorldLabel(mirrorRoot.transform, trigger.mirrorShape == MirrorShape.NormalL ? "정" : "역", new Vector3(0f, 0.7f, 0f), 0.16f, Color.yellow, 37);
                    triggerLineVisuals.Add(mirrorRoot);
                    continue;
                }

                if (trigger.triggerKind == DistanceSensorTriggerKind.ActivateTransformZone)
                {
                    TransformZoneData zone = FindZoneById(trigger.transformZoneId);
                    if (zone != null)
                    {
                        Vector3 zoneCenter = GridToWorld(zone.center);
                        triggerLineVisuals.Add(CreateLine($"TriggerLine_{i}_Zone", from, zoneCenter, lineColor, 0.045f, 35));
                    }
                }
            }
        }

        private void BuildPendingArrow()
        {
            pendingArrowRoot.SetActive(false);
            ClearChildren(pendingArrowRoot.transform);

            if (triggerEditMode != TriggerEditMode.WaitingPrismDirection)
                return;

            pendingArrowRoot.SetActive(true);
            pendingArrowRoot.transform.position = GridToWorld(pendingPrismPosition);
            DrawMirrorOpenSides(pendingArrowRoot.transform, pendingPrismDirection, pendingMirrorShape, Color.yellow, 40);
            AddWorldLabel(pendingArrowRoot.transform, pendingMirrorShape == MirrorShape.NormalL ? "정방향  B변경  X확정" : "역방향  B변경  X확정", new Vector3(0f, 0.78f, 0f), 0.14f, Color.yellow, 41);
        }

        private void DrawObjectVisual(Transform parent, StageObjectData obj, bool ghost)
        {
            Color color = obj.objectType switch
            {
                PuzzleObjectType.Wall => new Color(0.5f, 0.52f, 0.6f, ghost ? 0.55f : 1f),
                PuzzleObjectType.Mirror => new Color(0.75f, 0.9f, 1f, ghost ? 0.55f : 1f),
                PuzzleObjectType.Prism => obj.prismType == PrismType.Color ? ResolveLaserColor(obj.prismColor, Color.red) : new Color(1f, 0.72f, 0.22f, ghost ? 0.55f : 1f),
                PuzzleObjectType.Lens => new Color(0.45f, 1f, 0.95f, ghost ? 0.55f : 1f),
                _ => Color.white
            };

            int bodyOrder = ghost ? 30 : 3;
            int labelOrder = ghost ? 31 : 15;
            int guideOrder = ghost ? 32 : 18;
            CreateSpriteObject("Body", parent, Vector3.zero, GetObjectBodySize(obj), color, bodyOrder);
            string label = GetObjectShortLabel(obj);
            AddWorldLabel(parent, label, Vector3.zero, 0.22f, Color.black, labelOrder);

            if (obj.objectType == PuzzleObjectType.Mirror)
                DrawMirrorOpenSides(parent, obj.direction, obj.mirrorShape, new Color(1f, 1f, 1f, ghost ? 0.85f : 1f), guideOrder);
            else if (obj.objectType == PuzzleObjectType.Prism && obj.prismType == PrismType.Splitter)
                DrawSplitterOutputGuide(parent, obj.splitterMode, guideOrder);
            else if (obj.objectType == PuzzleObjectType.Prism && obj.prismType == PrismType.Refraction)
                DrawRefractionGuide(parent, obj.refractionMode, guideOrder);
        }

        private Vector2 GetObjectBodySize(StageObjectData obj)
        {
            if (obj != null && obj.objectType == PuzzleObjectType.Wall)
                return obj.manipulationType.CanPush() ? new Vector2(0.72f, 0.72f) : new Vector2(0.88f, 0.88f);

            return new Vector2(0.72f, 0.72f);
        }

        private bool ShouldShowDirectionSetting(StageObjectData obj)
        {
            return obj != null && obj.objectType == PuzzleObjectType.Mirror;
        }

        private void DrawMirrorOpenSides(Transform parent, GridDirection direction, MirrorShape shape, Color color, int sortingOrder)
        {
            GridDirection sideA = direction;
            GridDirection sideB = shape == MirrorShape.NormalL ? direction.RotateClockwise() : direction.RotateCounterClockwise();
            DrawShortDirection(parent, sideA, color, sortingOrder);
            DrawShortDirection(parent, sideB, color, sortingOrder);
        }

        private void DrawShortDirection(Transform parent, GridDirection direction, Color color, int sortingOrder)
        {
            Vector3 dir = new Vector3(direction.ToVector().x, direction.ToVector().y, 0f);
            CreateLine("GuideLine", parent.TransformPoint(Vector3.zero), parent.TransformPoint(dir * 0.48f), color, 0.055f, sortingOrder).transform.SetParent(parent);
            GameObject head = CreateSpriteObject("GuideHead", parent, dir * 0.52f, new Vector2(0.14f, 0.14f), color, sortingOrder);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());
        }

        private void DrawSplitterOutputGuide(Transform parent, PrismSplitterMode mode, int sortingOrder)
        {
            Color color = new Color(1f, 1f, 1f, 0.95f);
            AddWorldLabel(parent, "입력→", new Vector3(0f, -0.34f, 0f), 0.12f, Color.black, sortingOrder + 1);
            if (mode == PrismSplitterMode.ForwardAndLeft || mode == PrismSplitterMode.ForwardLeftRight)
                DrawShortDirection(parent, GridDirection.Up, color, sortingOrder);
            if (mode == PrismSplitterMode.ForwardAndRight || mode == PrismSplitterMode.ForwardLeftRight)
                DrawShortDirection(parent, GridDirection.Down, color, sortingOrder);
            if (mode == PrismSplitterMode.ForwardAndLeft || mode == PrismSplitterMode.ForwardAndRight || mode == PrismSplitterMode.ForwardLeftRight)
                DrawShortDirection(parent, GridDirection.Right, color, sortingOrder);
            if (mode == PrismSplitterMode.LeftAndRight)
            {
                DrawShortDirection(parent, GridDirection.Up, color, sortingOrder);
                DrawShortDirection(parent, GridDirection.Down, color, sortingOrder);
            }
        }

        private void DrawRefractionGuide(Transform parent, RefractionMode mode, int sortingOrder)
        {
            Color inputColor = new Color(1f, 1f, 1f, 0.85f);
            Color outputColor = new Color(0.35f, 0.9f, 1f, 0.95f);
            Vector3 left = new Vector3(-0.46f, 0f, 0f);
            Vector3 center = Vector3.zero;
            Vector3 output = mode == RefractionMode.Clockwise45 ? new Vector3(0.36f, -0.36f, 0f) : new Vector3(0.36f, 0.36f, 0f);
            CreateLine("RefractionInput", parent.TransformPoint(left), parent.TransformPoint(center), inputColor, 0.05f, sortingOrder).transform.SetParent(parent);
            CreateLine("RefractionOutput", parent.TransformPoint(center), parent.TransformPoint(output), outputColor, 0.05f, sortingOrder).transform.SetParent(parent);
            GameObject head = CreateSpriteObject("RefractionHead", parent, output, new Vector2(0.14f, 0.14f), outputColor, sortingOrder);
            float angle = Mathf.Atan2(output.y, output.x) * Mathf.Rad2Deg - 90f;
            head.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void DrawToolVisual(Transform parent, ToolType toolType, Vector3 world, GridDirection direction, ToolSettings settings, bool ghost, string namePrefix)
        {
            ClearChildren(parent);
            parent.position = world;

            switch (toolType)
            {
                case ToolType.PlayerStart:
                    CreateSpriteObject("PlayerGhost", parent, Vector3.zero, new Vector2(0.62f, 0.62f), new Color(0.2f, 0.65f, 1f, 0.65f), 30);
                    AddWorldLabel(parent, "P", Vector3.zero, 0.34f, Color.white, 32);
                    DrawDirectionArrow(parent, Vector3.zero, direction, Color.white, 33);
                    break;

                case ToolType.ClearHole:
                    CreateSpriteObject("ClearHoleGhost", parent, Vector3.zero, new Vector2(0.68f, 0.68f), new Color(0f, 0f, 0f, 0.65f), 30);
                    AddWorldLabel(parent, "구멍", Vector3.zero, 0.18f, Color.white, 32);
                    break;

                case ToolType.TargetNormal:
                case ToolType.TargetSequence:
                case ToolType.TargetIntersection:
                    TargetType targetType = toolType == ToolType.TargetSequence ? (settings.Color == LaserColorKind.Default ? TargetType.SequenceLocked : TargetType.SequenceColorLocked) : toolType == ToolType.TargetIntersection ? TargetType.Intersection : (settings.Color == LaserColorKind.Default ? TargetType.Normal : TargetType.ColorLocked);
                    DrawTargetGhost(parent, targetType, settings);
                    break;

                case ToolType.Wall:
                    StageObjectData wall = CreateWallObject(Vector2Int.zero, direction, settings.Pushable ? ManipulationType.PushOnly : ManipulationType.None);
                    DrawObjectVisual(parent, wall, true);
                    break;

                case ToolType.Mirror:
                    StageObjectData mirror = CreateMirrorObject(Vector2Int.zero);
                    mirror.direction = direction;
                    DrawObjectVisual(parent, mirror, true);
                    break;

                case ToolType.PrismSplitter:
                    StageObjectData splitter = CreatePrismObject(Vector2Int.zero, PrismType.Splitter);
                    splitter.direction = direction;
                    DrawObjectVisual(parent, splitter, true);
                    break;

                case ToolType.PrismColor:
                    StageObjectData colorPrism = CreatePrismObject(Vector2Int.zero, PrismType.Color);
                    colorPrism.direction = direction;
                    DrawObjectVisual(parent, colorPrism, true);
                    break;

                case ToolType.PrismRefraction:
                    StageObjectData refraction = CreatePrismObject(Vector2Int.zero, PrismType.Refraction);
                    refraction.direction = direction;
                    DrawObjectVisual(parent, refraction, true);
                    break;

                case ToolType.ZoneRotate:
                    DrawRange(parent, GetZoneVisualOffset(settings.ZoneWidth, settings.ZoneHeight, settings.ZoneOffsetX, settings.ZoneOffsetY), settings.ZoneWidth, settings.ZoneHeight, new Color(0.15f, 0.55f, 1f, 0.25f));
                    AddWorldLabel(parent, "회전", Vector3.zero, 0.22f, Color.white, 32);
                    break;

                case ToolType.ZoneMirror:
                    DrawRange(parent, GetZoneVisualOffset(settings.ZoneWidth, settings.ZoneHeight, settings.ZoneOffsetX, settings.ZoneOffsetY), settings.ZoneWidth, settings.ZoneHeight, new Color(0.85f, 0.25f, 1f, 0.25f));
                    AddWorldLabel(parent, "대칭", Vector3.zero, 0.22f, Color.white, 32);
                    break;

                case ToolType.DistanceSensor:
                    CreateSpriteObject("SensorGhost", parent, Vector3.zero, new Vector2(0.58f, 0.58f), new Color(0.25f, 1f, 0.65f, 0.65f), 32);
                    AddWorldLabel(parent, "감응", Vector3.zero, 0.18f, Color.black, 33);
                    break;

                case ToolType.LensAmplifier:
                    StageObjectData lens = CreateLensObject(Vector2Int.zero);
                    lens.direction = direction;
                    DrawObjectVisual(parent, lens, true);
                    break;

                case ToolType.Eraser:
                    CreateSpriteObject("EraserGhost", parent, Vector3.zero, new Vector2(0.78f, 0.78f), new Color(1f, 0.25f, 0.25f, 0.45f), 32);
                    AddWorldLabel(parent, "X", Vector3.zero, 0.36f, Color.white, 33);
                    break;
            }
        }

        private void DrawTargetGhost(Transform parent, TargetType targetType, ToolSettings settings)
        {
            Color color = targetType == TargetType.Intersection ? new Color(1f, 0.35f, 1f, 0.65f) : ResolveLaserColor(settings.Color, new Color(1f, 1f, 1f, 0.65f));
            CreateSpriteObject("TargetGhost", parent, Vector3.zero, new Vector2(0.72f, 0.72f), color, 32);
            string label = "T";
            Color labelColor = Color.black;
            if (targetType == TargetType.SequenceLocked || targetType == TargetType.SequenceColorLocked) label = settings.SequenceIndex.ToString();
            if (targetType == TargetType.Intersection)
            {
                label = BuildIntersectionLabel(settings.IntersectionCount, settings.IntersectionColors);
                labelColor = Color.white;
            }
            AddWorldLabel(parent, label, Vector3.zero, 0.34f, labelColor, 33);
        }

        private void DrawRange(Transform parent, Vector3 localOrWorld, int width, int height, Color color)
        {
            GameObject range = CreateSpriteObject("Range", parent, localOrWorld, new Vector2(width, height), color, 1);
            range.transform.localPosition = localOrWorld;
        }

        private GameObject CreateSpriteObject(string name, Transform parent, Vector3 position, Vector2 scale, Color color, int sortingOrder)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.localPosition = position;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return obj;
        }

        private string BuildIntersectionLabel(int intersectionCount, List<LaserColorKind> intersectionColors)
        {
            int count = Mathf.Clamp(intersectionCount, 2, 3);
            List<string> parts = new();

            for (int i = 0; i < count; i++)
            {
                LaserColorKind colorKind = intersectionColors != null && i < intersectionColors.Count ? intersectionColors[i] : LaserColorKind.Default;
                Color color = ResolveLaserColor(colorKind, Color.white);
                parts.Add($"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>|</color>");
            }

            return string.Join(" ", parts);
        }

        private void AddWorldLabel(Transform parent, string text, Vector3 localPosition, float fontSize, Color color, int sortingOrder)
        {
            GameObject obj = new GameObject("Label");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = localPosition + new Vector3(0f, 0f, -0.01f);
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            tmp.font = uiFont;
            tmp.richText = true;
            tmp.text = text;
            tmp.fontSize = fontSize * 10f;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 0.8f);
            tmp.renderer.sortingOrder = sortingOrder;
        }

        private void DrawDirectionArrow(Transform parent, Vector3 localPosition, GridDirection direction, Color color, int sortingOrder)
        {
            Vector3 dir = new Vector3(direction.ToVector().x, direction.ToVector().y, 0f);
            Vector3 start = localPosition + dir * 0.1f;
            Vector3 end = localPosition + dir * 0.62f;
            CreateLine("ArrowLine", parent.TransformPoint(start), parent.TransformPoint(end), color, 0.055f, sortingOrder).transform.SetParent(parent);
            GameObject head = CreateSpriteObject("ArrowHead", parent, end, new Vector2(0.18f, 0.18f), color, sortingOrder);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());
        }

        private GameObject CreateLine(string name, Vector3 from, Vector3 to, Color color, float width, int sortingOrder = 25)
        {
            GameObject lineObj = new GameObject(name);
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = width;
            line.endWidth = width;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            return lineObj;
        }

        private void AddUnique(List<Vector2Int> list, Vector2Int value)
        {
            if (!list.Contains(value))
                list.Add(value);
        }

        private void RebuildSequencePattern()
        {
            HashSet<int> values = new HashSet<int>();

            for (int i = 0; i < editingStageData.advancedTargets.Count; i++)
            {
                StageTargetData target = editingStageData.advancedTargets[i];
                if (target == null)
                    continue;

                if (target.targetType == TargetType.SequenceLocked || target.targetType == TargetType.SequenceColorLocked)
                {
                    target.sequenceValue = GetAvailableSequenceIndex(target.sequenceValue, target, values);
                    values.Add(target.sequenceValue);
                }
            }

            List<int> sorted = new List<int>(values);
            sorted.Sort((a, b) => a.CompareTo(b));
            editingStageData.sequenceLockPattern = sorted;

        }

        private int GetNextSequenceIndex()
        {
            int max = 0;
            for (int i = 0; i < editingStageData.advancedTargets.Count; i++)
            {
                StageTargetData target = editingStageData.advancedTargets[i];
                if (target != null && (target.targetType == TargetType.SequenceLocked || target.targetType == TargetType.SequenceColorLocked))
                    max = Mathf.Max(max, target.sequenceValue);
            }

            return max + 1;
        }

        private int GetAvailableSequenceIndex(int requested, StageTargetData ignoreTarget)
        {
            return GetAvailableSequenceIndex(requested, ignoreTarget, null);
        }

        private int GetAvailableSequenceIndex(int requested, StageTargetData ignoreTarget, HashSet<int> reserved)
        {
            int value = Mathf.Max(1, requested);
            while (IsSequenceIndexUsed(value, ignoreTarget) || (reserved != null && reserved.Contains(value)))
                value++;
            return value;
        }

        private bool IsSequenceIndexUsed(int value, StageTargetData ignoreTarget)
        {
            for (int i = 0; i < editingStageData.advancedTargets.Count; i++)
            {
                StageTargetData target = editingStageData.advancedTargets[i];
                if (target == null || target == ignoreTarget)
                    continue;

                if ((target.targetType == TargetType.SequenceLocked || target.targetType == TargetType.SequenceColorLocked) && target.sequenceValue == value)
                    return true;
            }

            return false;
        }

        private StageTargetData FindTargetAt(Vector2Int position)
        {
            for (int i = 0; i < editingStageData.advancedTargets.Count; i++)
            {
                StageTargetData target = editingStageData.advancedTargets[i];
                if (target != null && target.position == position)
                    return target;
            }

            if (editingStageData.targetPositions.Contains(position))
                return new StageTargetData { targetId = CreateId("Target", position), position = position, targetType = TargetType.Normal, requiredColor = LaserColorKind.Default, stopLaserOnHit = true };

            return null;
        }

        private StageObjectData FindObjectAt(Vector2Int position)
        {
            for (int i = 0; i < editingStageData.objects.Count; i++)
            {
                if (editingStageData.objects[i] != null && editingStageData.objects[i].position == position)
                    return editingStageData.objects[i];
            }

            return null;
        }

        private DistanceSensorData FindSensorAt(Vector2Int position)
        {
            for (int i = 0; i < editingStageData.distanceSensors.Count; i++)
            {
                if (editingStageData.distanceSensors[i] != null && editingStageData.distanceSensors[i].position == position)
                    return editingStageData.distanceSensors[i];
            }

            return null;
        }

        private TransformZoneData FindZoneAt(Vector2Int position)
        {
            for (int i = 0; i < editingStageData.transformZones.Count; i++)
            {
                TransformZoneData zone = editingStageData.transformZones[i];
                if (zone != null && IsPositionInsideZone(position, zone))
                    return zone;
            }

            return null;
        }

        private TransformZoneData FindZoneById(string zoneId)
        {
            for (int i = 0; i < editingStageData.transformZones.Count; i++)
            {
                if (editingStageData.transformZones[i] != null && editingStageData.transformZones[i].zoneId == zoneId)
                    return editingStageData.transformZones[i];
            }

            return null;
        }

        private bool IsPositionInsideZone(Vector2Int position, TransformZoneData zone)
        {
            Vector2Int min = GetZoneMinCell(zone);
            int maxX = min.x + zone.width - 1;
            int maxY = min.y + zone.height - 1;
            return position.x >= min.x && position.x <= maxX && position.y >= min.y && position.y <= maxY;
        }

        private ManipulationType ResolveManipulation(bool push, bool rotate)
        {
            if (push && rotate) return ManipulationType.PushAndRotate;
            if (push) return ManipulationType.PushOnly;
            if (rotate) return ManipulationType.RotateOnly;
            return ManipulationType.None;
        }

        private MirrorAxis ToggleMirrorAxis(MirrorAxis axis)
        {
            return axis == MirrorAxis.Horizontal ? MirrorAxis.Vertical : MirrorAxis.Horizontal;
        }

        private void EnsureIntersectionColorCount(List<LaserColorKind> colors, int count)
        {
            while (colors.Count < count)
                colors.Add(LaserColorKind.Default);
        }

        private string CreateId(string prefix, Vector2Int position)
        {
            return $"{prefix}_{position.x}_{position.y}_{DateTime.Now.Ticks % 100000}";
        }

        private string DirectionName(GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => "위",
                GridDirection.Right => "오른쪽",
                GridDirection.Down => "아래",
                GridDirection.Left => "왼쪽",
                _ => direction.ToString()
            };
        }

        private string GetObjectDisplayName(StageObjectData obj)
        {
            if (obj.objectType == PuzzleObjectType.Wall) return "벽";
            if (obj.objectType == PuzzleObjectType.Mirror) return "거울";
            if (obj.objectType == PuzzleObjectType.Lens) return "증폭기";
            if (obj.objectType == PuzzleObjectType.Prism)
            {
                if (obj.prismType == PrismType.Splitter) return "분기 프리즘";
                if (obj.prismType == PrismType.Color) return "색상 프리즘";
                if (obj.prismType == PrismType.Refraction) return "굴절 프리즘";
            }
            return obj.objectType.ToString();
        }

        private string GetObjectShortLabel(StageObjectData obj)
        {
            if (obj.objectType == PuzzleObjectType.Wall) return obj.manipulationType.CanPush() ? "밀벽" : "벽";
            if (obj.objectType == PuzzleObjectType.Mirror) return "거울";
            if (obj.objectType == PuzzleObjectType.Lens) return $"+{obj.distanceBoost}";
            if (obj.objectType == PuzzleObjectType.Prism)
            {
                if (obj.prismType == PrismType.Splitter) return obj.splitterMode == PrismSplitterMode.ForwardLeftRight ? "분3" : "분2";
                if (obj.prismType == PrismType.Color) return "색";
                if (obj.prismType == PrismType.Refraction) return "굴절";
            }
            return "?";
        }

        private Color ResolveLaserColor(LaserColorKind color, Color defaultColor)
        {
            return color switch
            {
                LaserColorKind.Red => new Color(1f, 0.22f, 0.22f, 1f),
                LaserColorKind.Blue => new Color(0.25f, 0.52f, 1f, 1f),
                LaserColorKind.Green => new Color(0.25f, 1f, 0.42f, 1f),
                LaserColorKind.Yellow => new Color(1f, 0.9f, 0.18f, 1f),
                LaserColorKind.Purple => new Color(0.78f, 0.35f, 1f, 1f),
                LaserColorKind.White => Color.white,
                _ => defaultColor
            };
        }



        private void AutoTrackStageHistory()
        {
            if (editingStageData == null || isApplyingHistory || skipAutoHistoryThisFrame || isDraggingElement)
            {
                skipAutoHistoryThisFrame = false;
                return;
            }

            string key = CreateStageHistoryKey();

            if (string.IsNullOrEmpty(lastCommittedStageKey))
            {
                lastCommittedStageKey = key;
                return;
            }

            if (key == lastCommittedStageKey)
                return;

            if (lastCommittedSnapshot != null)
                undoStack.Add(lastCommittedSnapshot.Clone());

            TrimUndoStack();
            redoStack.Clear();
            lastCommittedSnapshot = editingStageData.Clone();
            lastCommittedStageKey = key;
        }

        private void ResetStageHistory()
        {
            undoStack.Clear();
            redoStack.Clear();
            RefreshHistoryBaseline();
            skipAutoHistoryThisFrame = true;
        }

        private string CreateStageHistoryKey()
        {
            return CreateStageHistoryKey(editingStageData);
        }

        private string CreateStageHistoryKey(StageData data)
        {
            return data == null ? string.Empty : JsonUtility.ToJson(data);
        }

        private StageData lastCommittedSnapshot;

        private StageData StageDataSnapshotFromKeySource(string ignoredKey)
        {
            return lastCommittedSnapshot != null ? lastCommittedSnapshot.Clone() : editingStageData.Clone();
        }

        private void RefreshHistoryBaseline()
        {
            lastCommittedSnapshot = editingStageData != null ? editingStageData.Clone() : null;
            lastCommittedStageKey = CreateStageHistoryKey();
        }

        private void TrimUndoStack()
        {
            while (undoStack.Count > MaxUndoCount)
                undoStack.RemoveAt(0);
        }

        private void UndoEditorAction()
        {
            if (undoStack.Count <= 0 || editingStageData == null)
                return;

            isApplyingHistory = true;
            redoStack.Add(editingStageData.Clone());
            editingStageData = undoStack[undoStack.Count - 1].Clone();
            undoStack.RemoveAt(undoStack.Count - 1);
            ClearSelection();
            RebuildSequencePattern();
            RebuildStageVisuals();
            RefreshRuntimeSettingsPanel();
            RefreshHistoryBaseline();
            isApplyingHistory = false;
            skipAutoHistoryThisFrame = true;
            SetStatus("되돌리기 완료");
        }

        private void RedoEditorAction()
        {
            if (redoStack.Count <= 0 || editingStageData == null)
                return;

            isApplyingHistory = true;
            undoStack.Add(editingStageData.Clone());
            TrimUndoStack();
            editingStageData = redoStack[redoStack.Count - 1].Clone();
            redoStack.RemoveAt(redoStack.Count - 1);
            ClearSelection();
            RebuildSequencePattern();
            RebuildStageVisuals();
            RefreshRuntimeSettingsPanel();
            RefreshHistoryBaseline();
            isApplyingHistory = false;
            skipAutoHistoryThisFrame = true;
            SetStatus("다시 실행 완료");
        }

        private Vector3 GridToWorld(Vector2Int position)
        {
            float offsetX = (editingStageData.width - 1) * 0.5f;
            float offsetY = (editingStageData.height - 1) * 0.5f;
            return new Vector3(position.x - offsetX, position.y - offsetY, 0f);
        }

        private Vector2Int WorldToGrid(Vector3 world)
        {
            float offsetX = (editingStageData.width - 1) * 0.5f;
            float offsetY = (editingStageData.height - 1) * 0.5f;
            return new Vector2Int(Mathf.RoundToInt(world.x + offsetX), Mathf.RoundToInt(world.y + offsetY));
        }

        private void SaveCurrentStageToPath(string path, bool showStatus)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("저장 실패: 경로 없음");
                return;
            }

            RebuildSequencePattern();
            StageBinarySerializer.Save(editingStageData, path);
            currentFilePath = path;
            selectedSaveDirectory = Path.GetDirectoryName(path);
            selectedSaveFileName = Path.GetFileNameWithoutExtension(path);
            RefreshRuntimeSettingsPanel();
            if (showStatus)
            {
                HideAllPopups();
                PlayEditorSfx(FmodRuntimeAudio.SfxUiConfirmation);
                SetStatus($"저장 완료: {path}");
            }
        }

        private void StartTestPlay()
        {
            if (editingStageData == null)
                return;

            RebuildSequencePattern();
            GameSceneRequest.RequestEditorTestStage(editingStageData, SceneManager.GetActiveScene().name, currentFilePath, selectedSaveDirectory, selectedSaveFileName);
            SceneFadeController.Instance.LoadScene("Game");
        }

        private void ReturnToTitle()
        {
            SceneFadeController.Instance.LoadScene("Title");
        }

        private void PickLoadFile()
        {
            if (!CanOpenNativeFileDialog())
                return;

            BeginNativeFileDialog();

#if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("불러올 .tls 선택", StageFilePaths.MyCustomLevelsDirectory, "tls");
            EndNativeFileDialog();

            if (!string.IsNullOrWhiteSpace(path))
            {
                selectedLoadFilePath = path;
                if (loadPathInput != null)
                    loadPathInput.SetTextWithoutNotify(path);

                ClearCurrentUiSelection();
            }
#else
            EndNativeFileDialog();
            SetStatus("빌드 환경에서는 경로 입력칸에 .tls 전체 경로를 직접 입력하세요.");
#endif
        }

        private void PickSaveFolder()
        {
            if (!CanOpenNativeFileDialog())
                return;

            BeginNativeFileDialog();

#if UNITY_EDITOR
            string path = EditorUtility.OpenFolderPanel("저장할 폴더 선택", StageFilePaths.MyCustomLevelsDirectory, "");
            EndNativeFileDialog();

            if (!string.IsNullOrWhiteSpace(path))
            {
                selectedSaveDirectory = path;
                if (saveDirectoryInput != null)
                    saveDirectoryInput.SetTextWithoutNotify(path);

                ClearCurrentUiSelection();
            }
#else
            EndNativeFileDialog();
            SetStatus("빌드 환경에서는 폴더 경로 입력칸에 직접 입력하세요.");
#endif
        }

        private bool CanOpenNativeFileDialog()
        {
            if (isNativeFileDialogOpen)
                return false;

            return Time.unscaledTime - lastNativeFileDialogClosedTime > 0.25f;
        }

        private void BeginNativeFileDialog()
        {
            isNativeFileDialogOpen = true;
            ClearCurrentUiSelection();
        }

        private void EndNativeFileDialog()
        {
            isNativeFileDialogOpen = false;
            lastNativeFileDialogClosedTime = Time.unscaledTime;
            ClearCurrentUiSelection();
        }

        private void ClearCurrentUiSelection()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void ShowMainPopup()
        {
            HideAllPopups();
            if (editorBgmDropdown != null)
            {
                editorBgmDropdown.SetValueWithoutNotify(editorBgmIndex);
                editorBgmDropdown.RefreshShownValue();
            }
            mainPopup.gameObject.SetActive(true);
            PlayEditorSfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void ShowNewLevelPopup()
        {
            HideAllPopups();
            if (newStageNameInput != null) newStageNameInput.text = string.IsNullOrWhiteSpace(editingStageData.stageName) ? "New Level" : editingStageData.stageName;
            if (newWidthInput != null) newWidthInput.text = defaultWidth.ToString();
            if (newHeightInput != null) newHeightInput.text = defaultHeight.ToString();
            if (newLaserInput != null) newLaserInput.text = defaultLaserMaxDistance.ToString();
            if (newMoveInput != null) newMoveInput.text = defaultMoveLimit.ToString();
            if (newPlayerStartDropdown != null) newPlayerStartDropdown.value = (int)newPlayerStartPreset;
            newBgmIndex = FindBgmIndex(editingStageData != null ? editingStageData.bgmEventPath : string.Empty);
            if (newBgmDropdown != null)
            {
                newBgmDropdown.value = newBgmIndex;
                newBgmDropdown.RefreshShownValue();
            }
            if (newHolePositionDropdown != null) newHolePositionDropdown.value = (int)newHolePositionPreset;
            newLevelPopup.gameObject.SetActive(true);
            PlayEditorSfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void ShowLoadPopup()
        {
            HideAllPopups();
            if (loadPathInput != null) loadPathInput.text = selectedLoadFilePath;
            loadPopup.gameObject.SetActive(true);
            PlayEditorSfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void ShowSavePopup()
        {
            HideAllPopups();
            if (saveDirectoryInput != null) saveDirectoryInput.text = selectedSaveDirectory;
            if (saveFileNameInput != null) saveFileNameInput.text = selectedSaveFileName;
            if (currentFileText != null) currentFileText.text = string.IsNullOrWhiteSpace(currentFilePath) ? "현재 파일 없음" : currentFilePath;
            savePopup.gameObject.SetActive(true);
            PlayEditorSfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void ShowUploadPopup()
        {
            if (editingStageData == null || !editingStageData.HasSolution)
            {
                ShowEditorNotice("답안이 있는 맵만 업로드할 수 있습니다.", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(currentFilePath) || !File.Exists(currentFilePath))
                SaveCurrentStageToPath(Path.Combine(ExportDirectory, StageFilePaths.NormalizeStageFileName(selectedSaveFileName)), false);

            HideAllPopups(false);
            if (uploadPopup != null)
                uploadPopup.gameObject.SetActive(true);

            if (uploadTitleInput != null && string.IsNullOrWhiteSpace(uploadTitleInput.text))
                uploadTitleInput.SetTextWithoutNotify(string.IsNullOrWhiteSpace(editingStageData.stageName) ? selectedSaveFileName : editingStageData.stageName);

            RefreshUploadPublishButton();
            PlayEditorSfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void RefreshUploadButtonVisibility()
        {
            if (uploadButton == null)
                return;

            bool canShow = editingStageData != null && editingStageData.HasSolution;
            uploadButton.gameObject.SetActive(canShow);

            ApplyFloatingPanelsLayout();
        }

        private void RefreshUploadPublishButton()
        {
            bool hasInputs = uploadNicknameInput != null && uploadTitleInput != null && uploadDescriptionInput != null &&
                !string.IsNullOrWhiteSpace(uploadNicknameInput.text) &&
                !string.IsNullOrWhiteSpace(uploadTitleInput.text) &&
                !string.IsNullOrWhiteSpace(uploadDescriptionInput.text);
            bool hasSolution = editingStageData != null && editingStageData.HasSolution;
            bool hasSavedFile = !string.IsNullOrWhiteSpace(currentFilePath) && File.Exists(currentFilePath);

            if (uploadPublishButton != null)
                uploadPublishButton.interactable = hasInputs && hasSolution && hasSavedFile;

            if (uploadValidationText != null)
            {
                if (!hasSolution)
                    uploadValidationText.text = "답안이 없습니다. 테스트 플레이로 클리어해서 답안을 먼저 기록하세요.";
                else if (!hasSavedFile)
                    uploadValidationText.text = "업로드 전에 .tls 저장이 필요합니다. 업로드 버튼을 열 때 자동 저장을 시도합니다.";
                else if (!hasInputs)
                    uploadValidationText.text = "닉네임, 게시물 이름, 게시물 설명을 모두 입력하면 게시하기가 활성화됩니다.";
                else
                    uploadValidationText.text = "게시 준비 완료";
            }
        }

        private void PublishCurrentLevel()
        {
            RefreshUploadPublishButton();
            if (uploadPublishButton == null || !uploadPublishButton.interactable)
                return;

            ShowEditorNotice("게시 중...", false);
            uploadPublishButton.interactable = false;
            StartCoroutine(SupabaseCustomLevelService.Instance.UploadCustomLevel(currentFilePath, editingStageData, uploadNicknameInput.text.Trim(), uploadTitleInput.text.Trim(), uploadDescriptionInput.text.Trim(), (success, message) =>
            {
                ShowEditorNotice(message, !success);
                if (success)
                {
                    HideAllPopups(false);
                    PlayEditorSfx(FmodRuntimeAudio.SfxUiConfirmation);
                }
                RefreshUploadPublishButton();
            }));
        }

        private void BuildEditorOnlineNotice()
        {
            RectTransform panel = CreatePanel("EditorOnlineNotice", canvasRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-470f, -72f), new Vector2(-18f, -18f), new Color(0.03f, 0.035f, 0.045f, 0.92f));
            AddVerticalLayout(panel, 8, 8, 8, 8, 0);
            editorOnlineNoticeText = AddText(panel, "", 18, TextAlignmentOptions.Right, Color.white);
            editorOnlineNoticeText.enableWordWrapping = true;
            panel.gameObject.SetActive(false);
        }

        private void ShowEditorNotice(string message, bool error)
        {
            if (editorOnlineNoticeText == null)
                return;

            editorOnlineNoticeText.text = message;
            editorOnlineNoticeText.color = error ? new Color(1f, 0.35f, 0.35f, 1f) : new Color(0.7f, 0.95f, 1f, 1f);
            editorOnlineNoticeText.transform.parent.gameObject.SetActive(true);

            if (editorNoticeRoutine != null)
                StopCoroutine(editorNoticeRoutine);
            editorNoticeRoutine = StartCoroutine(HideEditorNoticeRoutine());
        }

        private System.Collections.IEnumerator HideEditorNoticeRoutine()
        {
            yield return new WaitForSecondsRealtime(2.4f);
            if (editorOnlineNoticeText != null)
                editorOnlineNoticeText.transform.parent.gameObject.SetActive(false);
            editorNoticeRoutine = null;
        }

        private bool IsAnyPopupOpen()
        {
            return (mainPopup != null && mainPopup.gameObject.activeSelf) ||
                (newLevelPopup != null && newLevelPopup.gameObject.activeSelf) ||
                (loadPopup != null && loadPopup.gameObject.activeSelf) ||
                (savePopup != null && savePopup.gameObject.activeSelf) ||
                (uploadPopup != null && uploadPopup.gameObject.activeSelf) ||
                (solutionOverwritePopup != null && solutionOverwritePopup.gameObject.activeSelf);
        }

        private void HideAllPopups(bool playSound = false)
        {
            bool hadOpenPopup = IsAnyPopupOpen();
            if (mainPopup != null) mainPopup.gameObject.SetActive(false);
            if (newLevelPopup != null) newLevelPopup.gameObject.SetActive(false);
            if (loadPopup != null) loadPopup.gameObject.SetActive(false);
            if (savePopup != null) savePopup.gameObject.SetActive(false);
            if (uploadPopup != null) uploadPopup.gameObject.SetActive(false);
            if (solutionOverwritePopup != null) solutionOverwritePopup.gameObject.SetActive(false);

            if (playSound && hadOpenPopup)
                PlayEditorSfx(FmodRuntimeAudio.SfxUiClose);
        }

        private void UpdateDescription(string title, string description)
        {
            if (descriptionTitleText != null) descriptionTitleText.text = title;
            if (descriptionBodyText != null) descriptionBodyText.text = description;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;

            Debug.Log($"[LevelEditor] {message}");
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private int ParseInt(TMP_InputField input, int fallback)
        {
            return input != null && int.TryParse(input.text, out int value) ? value : fallback;
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rect = CreateUIObject(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private VerticalLayoutGroup AddVerticalLayout(RectTransform rect, int left, int right, int top, int bottom, int spacing)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private HorizontalLayoutGroup AddHorizontalLayout(RectTransform rect, int left, int right, int top, int bottom, int spacing)
        {
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private TMP_Text AddText(Transform parent, string text, int size, TextAlignmentOptions alignment, Color color)
        {
            RectTransform rect = CreateUIObject("Text", parent);
            TMP_Text tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = uiFont;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(28f, size + 12f);
            return tmp;
        }

        private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float width, float height)
        {
            RectTransform rect = CreateUIObject(label + "Button", parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.17f, 0.23f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                PlayEditorSfx(FmodRuntimeAudio.SfxUiClick);
                onClick?.Invoke();
            });
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            TMP_Text text = AddText(rect, label, 18, TextAlignmentOptions.Center, Color.white);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private TMP_InputField AddInputRow(Transform parent, string label, string value, Action<string> onEndEdit)
        {
            RectTransform row = CreateUIObject(label + "Row", parent);
            AddHorizontalLayout(row, 0, 0, 0, 0, 8);
            TMP_Text labelText = AddText(row, label, 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f));
            labelText.GetComponent<LayoutElement>().preferredWidth = 170f;
            TMP_InputField input = CreateInputField(row, value);
            input.onEndEdit.AddListener(text => { PlayEditorSfx(FmodRuntimeAudio.SfxEditorCheckBox); onEndEdit?.Invoke(text); });
            runtimeSettingSelectables.Add(input);
            return input;
        }

        private TMP_InputField CreateInputField(Transform parent, string value)
        {
            RectTransform rect = CreateUIObject("InputField", parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.095f, 1f);
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 36f;
            layout.flexibleWidth = 1f;
            TMP_Text text = AddText(rect, value, 17, TextAlignmentOptions.Left, Color.white);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            input.textComponent = text;
            input.text = value;
            return input;
        }

        private void AddToggleRow(Transform parent, string label, bool value, Action<bool> onChanged)
        {
            RectTransform row = CreateUIObject(label + "ToggleRow", parent);
            AddHorizontalLayout(row, 0, 0, 0, 0, 8);
            TMP_Text labelText = AddText(row, label, 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f));
            labelText.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Toggle toggle = CreateToggle(row, value);
            toggle.onValueChanged.AddListener(isOn => { PlayEditorSfx(FmodRuntimeAudio.SfxEditorCheckBox); onChanged?.Invoke(isOn); });
        }

        private Toggle CreateToggle(Transform parent, bool value)
        {
            RectTransform rect = CreateUIObject("Toggle", parent);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 48f;
            layout.preferredHeight = 32f;
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.22f, 0.25f, 0.32f, 1f);
            Toggle toggle = rect.gameObject.AddComponent<Toggle>();
            RectTransform checkRect = CreateUIObject("Check", rect);
            checkRect.anchorMin = new Vector2(0.18f, 0.18f);
            checkRect.anchorMax = new Vector2(0.82f, 0.82f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image checkImage = checkRect.gameObject.AddComponent<Image>();
            checkImage.color = new Color(0.7f, 0.95f, 1f, 1f);
            toggle.targetGraphic = background;
            toggle.graphic = checkImage;
            toggle.isOn = value;
            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            toggle.colors = colors;
            return toggle;
        }

        private List<string> BgmDisplayNames()
        {
            return new List<string>
            {
                "Chapter01.mp3",
                "Chapter02.mp3",
                "Chapter03.mp3",
                "Chapter04.mp3",
                "Chapter05.mp3",
                "Chapter06.mp3",
                "Chapter07.mp3",
                "Chapter08.mp3",
                "Chapter09.mp3",
                "Chapter10.mp3",
                "EditorBGM.mp3"
            };
        }

        private List<string> BgmEventPaths()
        {
            return new List<string>
            {
                FmodRuntimeAudio.BgmChapter01,
                FmodRuntimeAudio.BgmChapter02,
                FmodRuntimeAudio.BgmChapter03,
                FmodRuntimeAudio.BgmChapter04,
                FmodRuntimeAudio.BgmChapter05,
                FmodRuntimeAudio.BgmChapter06,
                FmodRuntimeAudio.BgmChapter07,
                FmodRuntimeAudio.BgmChapter08,
                FmodRuntimeAudio.BgmChapter09,
                FmodRuntimeAudio.BgmChapter10,
                FmodRuntimeAudio.BgmEditor
            };
        }

        private string GetBgmEventPathByIndex(int index)
        {
            List<string> paths = BgmEventPaths();
            if (paths.Count <= 0)
                return string.Empty;

            return paths[Mathf.Clamp(index, 0, paths.Count - 1)];
        }

        private int FindBgmIndex(string eventPath)
        {
            string normalized = FmodRuntimeAudio.NormalizeEventPath(eventPath);
            List<string> paths = BgmEventPaths();

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], normalized, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private List<string> PlayerStartPresetNames()
        {
            return new List<string> { "좌 상단", "우 상단", "중앙 상단", "좌 중단", "우 중단", "센터", "좌 하단", "우 하단", "중앙 하단" };
        }

        private Vector2Int ResolvePlayerStartPosition(int width, int height, PlayerStartPreset preset)
        {
            int left = 0;
            int centerX = Mathf.Clamp(width / 2, 0, Mathf.Max(0, width - 1));
            int right = Mathf.Max(0, width - 1);
            int bottom = 0;
            int centerY = Mathf.Clamp(height / 2, 0, Mathf.Max(0, height - 1));
            int top = Mathf.Max(0, height - 1);

            return preset switch
            {
                PlayerStartPreset.LeftTop => new Vector2Int(left, top),
                PlayerStartPreset.CenterTop => new Vector2Int(centerX, top),
                PlayerStartPreset.RightTop => new Vector2Int(right, top),
                PlayerStartPreset.LeftMiddle => new Vector2Int(left, centerY),
                PlayerStartPreset.Center => new Vector2Int(centerX, centerY),
                PlayerStartPreset.RightMiddle => new Vector2Int(right, centerY),
                PlayerStartPreset.LeftBottom => new Vector2Int(left, bottom),
                PlayerStartPreset.CenterBottom => new Vector2Int(centerX, bottom),
                PlayerStartPreset.RightBottom => new Vector2Int(right, bottom),
                _ => new Vector2Int(left, centerY)
            };
        }

        private TMP_Dropdown AddDropdownRow(Transform parent, string label, List<string> options, int value, Action<int> onChanged)
        {
            RectTransform row = CreateUIObject(label + "DropdownRow", parent);
            AddHorizontalLayout(row, 0, 0, 0, 0, 8);
            TMP_Text labelText = AddText(row, label, 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f));
            labelText.GetComponent<LayoutElement>().preferredWidth = 150f;
            TMP_Dropdown dropdown = CreateDropdown(row, options);
            dropdown.value = Mathf.Clamp(value, 0, dropdown.options.Count - 1);
            dropdown.onValueChanged.AddListener(index => { PlayEditorSfx(FmodRuntimeAudio.SfxEditorCheckBox); onChanged?.Invoke(index); });
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private void AddSplitterModeDropdown(Transform parent, string label, PrismSplitterMode value, Action<PrismSplitterMode> onChanged)
        {
            List<string> options = new List<string> { "앞+왼쪽", "앞+오른쪽", "왼쪽+오른쪽" };
            int selectedIndex = value switch
            {
                PrismSplitterMode.ForwardAndRight => 1,
                PrismSplitterMode.LeftAndRight => 2,
                _ => 0
            };

            AddDropdownRow(parent, label, options, selectedIndex, index =>
            {
                PrismSplitterMode mode = index switch
                {
                    1 => PrismSplitterMode.ForwardAndRight,
                    2 => PrismSplitterMode.LeftAndRight,
                    _ => PrismSplitterMode.ForwardAndLeft
                };
                onChanged?.Invoke(mode);
            });
        }

        private void AddZoneOffsetSettings(int width, int height, Action<int> onXChanged, Action<int> onYChanged, int currentX, int currentY)
        {
            if (width % 2 == 0)
            {
                int value = NormalizeZoneOffsetValue(width, currentX);
                AddStepperRow(settingsPanel, "가로 보정", value < 0 ? "왼쪽" : "오른쪽", () => onXChanged?.Invoke(-1), () => onXChanged?.Invoke(1));
            }

            if (height % 2 == 0)
            {
                int value = NormalizeZoneOffsetValue(height, currentY);
                AddStepperRow(settingsPanel, "세로 보정", value < 0 ? "아래" : "위", () => onYChanged?.Invoke(-1), () => onYChanged?.Invoke(1));
            }
        }

        private void AddLaserColorDropdown(Transform parent, string label, LaserColorKind value, Action<LaserColorKind> onChanged)
        {
            RectTransform row = CreateUIObject(label + "DropdownRow", parent);
            AddHorizontalLayout(row, 0, 0, 0, 0, 8);
            TMP_Text labelText = AddText(row, label, 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f));
            labelText.GetComponent<LayoutElement>().preferredWidth = 120f;
            TMP_Dropdown dropdown = CreateDropdown(row, EnumNames<LaserColorKind>());
            dropdown.value = Mathf.Clamp((int)value, 0, dropdown.options.Count - 1);
            dropdown.onValueChanged.AddListener(index => { PlayEditorSfx(FmodRuntimeAudio.SfxEditorCheckBox); onChanged?.Invoke((LaserColorKind)index); });
        }

        private TMP_Dropdown CreateDropdown(Transform parent, List<string> options)
        {
            RectTransform rect = CreateUIObject("Dropdown", parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.095f, 1f);
            TMP_Dropdown dropdown = rect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 36f;
            layout.flexibleWidth = 1f;

            TMP_Text label = AddText(rect, "", 17, TextAlignmentOptions.Left, Color.white);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-30f, 0f);
            dropdown.captionText = label;

            RectTransform arrow = CreateUIObject("Arrow", rect);
            arrow.anchorMin = new Vector2(1f, 0.5f);
            arrow.anchorMax = new Vector2(1f, 0.5f);
            arrow.sizeDelta = new Vector2(18f, 18f);
            arrow.anchoredPosition = new Vector2(-14f, 0f);
            TMP_Text arrowText = arrow.gameObject.AddComponent<TextMeshProUGUI>();
            arrowText.font = uiFont;
            arrowText.text = "▼";
            arrowText.fontSize = 14;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.enableWordWrapping = false;
            arrowText.color = Color.white;

            RectTransform template = CreateDropdownTemplate(rect, out TMP_Text itemText);
            dropdown.template = template;
            dropdown.itemText = itemText;
            dropdown.options = new List<TMP_Dropdown.OptionData>();
            for (int i = 0; i < options.Count; i++)
                dropdown.options.Add(new TMP_Dropdown.OptionData(options[i]));
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private RectTransform CreateDropdownTemplate(RectTransform dropdownRoot, out TMP_Text itemText)
        {
            RectTransform template = CreateUIObject("Template", dropdownRoot);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, 240f);
            Image templateImage = template.gameObject.AddComponent<Image>();
            templateImage.color = new Color(0.035f, 0.04f, 0.055f, 0.99f);
            ScrollRect scrollRect = template.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = CreateUIObject("Viewport", template);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-4f, -4f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
            Mask viewportMask = viewport.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            RectTransform content = CreateUIObject("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            contentLayout.spacing = 2;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform item = CreateUIObject("Item", content);
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(1f, 0.5f);
            item.sizeDelta = new Vector2(0f, 36f);
            LayoutElement itemLayout = item.gameObject.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = 36f;
            Image itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = new Color(0.12f, 0.14f, 0.19f, 1f);
            Toggle toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;

            RectTransform checkmark = CreateUIObject("Item Checkmark", item);
            checkmark.anchorMin = new Vector2(0f, 0.5f);
            checkmark.anchorMax = new Vector2(0f, 0.5f);
            checkmark.sizeDelta = new Vector2(18f, 18f);
            checkmark.anchoredPosition = new Vector2(16f, 0f);
            Image checkImage = checkmark.gameObject.AddComponent<Image>();
            checkImage.color = new Color(0.7f, 0.95f, 1f, 1f);
            toggle.graphic = checkImage;

            RectTransform labelRect = CreateUIObject("Item Label", item);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(40f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            itemText = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            itemText.font = uiFont;
            itemText.fontSize = 17;
            itemText.color = Color.white;
            itemText.alignment = TextAlignmentOptions.MidlineLeft;
            itemText.enableWordWrapping = false;
            itemText.overflowMode = TextOverflowModes.Ellipsis;
            itemText.raycastTarget = false;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            template.gameObject.SetActive(false);
            return template;
        }

        private List<string> EnumNames<T>() where T : Enum
        {
            Array values = Enum.GetValues(typeof(T));
            List<string> names = new List<string>();
            for (int i = 0; i < values.Length; i++)
                names.Add(values.GetValue(i).ToString());
            return names;
        }

        private void AddIntStepper(Transform parent, string label, int value, int min, int max, Action<int> onChanged, string suffix = "")
        {
            AddStepperRow(parent, label, $"{value}{suffix}", () => onChanged?.Invoke(Mathf.Clamp(value - 1, min, max)), () => onChanged?.Invoke(Mathf.Clamp(value + 1, min, max)));
        }

        private void AddFloatStepper(Transform parent, string label, float value, float min, float max, float step, Action<float> onChanged)
        {
            AddStepperRow(parent, label, value.ToString("0.0"), () => onChanged?.Invoke(Mathf.Clamp(value - step, min, max)), () => onChanged?.Invoke(Mathf.Clamp(value + step, min, max)));
        }

        private void AddStepperRow(Transform parent, string label, string valueLabel, Action onMinus, Action onPlus)
        {
            RectTransform row = CreateUIObject(label + "StepperRow", parent);
            AddHorizontalLayout(row, 0, 0, 0, 0, 6);
            TMP_Text labelText = AddText(row, label, 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f));
            labelText.GetComponent<LayoutElement>().preferredWidth = 130f;
            AddButton(row, "<", () => onMinus?.Invoke(), 44f, 34f);
            TMP_Text valueText = AddText(row, valueLabel, 18, TextAlignmentOptions.Center, Color.white);
            valueText.GetComponent<LayoutElement>().preferredWidth = 84f;
            AddButton(row, ">", () => onPlus?.Invoke(), 44f, 34f);
        }

        private ScrollRect CreateScrollRect(Transform parent, string name, out RectTransform content)
        {
            RectTransform root = CreateUIObject(name, parent);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.04f, 0.045f, 0.06f, 0.5f);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = CreateUIObject("Viewport", root);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.03f);
            content = CreateUIObject("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup layout = AddVerticalLayout(content, 8, 8, 8, 8, 6);
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            LayoutElement rootLayout = root.gameObject.AddComponent<LayoutElement>();
            rootLayout.flexibleHeight = 1f;
            return scroll;
        }
    }
}
