using System.Collections;
using System.Text;
using Core;
using Grid;
using Laser;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.InGame
{
    public class RuntimeDebugOverlay : MonoBehaviour
    {
        [Header("Hotkey")]
        [SerializeField] private Key toggleKey = Key.F1;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(620f, 760f);
        [SerializeField] private Vector2 panelPosition = new Vector2(24f, -24f);
        [SerializeField] private float refreshInterval = 0.15f;

        private GridManager gridManager;
        private PlayerGridController playerController;
        private LaserShooter laserShooter;
        private StageResetController resetController;
        private InGameStageFlowController stageFlowController;

        private Canvas canvas;
        private RectTransform panel;
        private TMP_Text infoText;
        private TMP_Text gridText;
        private TMP_Text simulationSpeedButtonText;
        private TMP_Text gridToggleButtonText;
        private RectTransform gridVisualRoot;
        private TMP_FontAsset font;
        private Sprite whiteSprite;
        private Coroutine refreshRoutine;
        private bool showGridData;

        private void Awake()
        {
            EnsureReferences();
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            var keyControl = Keyboard.current[toggleKey];
            if (keyControl != null && keyControl.wasPressedThisFrame)
                Toggle();
        }

        private void Toggle()
        {
            bool next = panel == null || !panel.gameObject.activeSelf;
            SetVisible(next);
        }

        private void SetVisible(bool visible)
        {
            if (panel == null)
                return;

            panel.gameObject.SetActive(visible);

            if (visible)
            {
                RefreshNow();
                if (refreshRoutine == null)
                    refreshRoutine = StartCoroutine(RefreshRoutine());
            }
            else if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }
        }

        private IEnumerator RefreshRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, refreshInterval));

            while (panel != null && panel.gameObject.activeSelf)
            {
                RefreshNow();
                yield return wait;
            }

            refreshRoutine = null;
        }

        private void EnsureReferences()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();

            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerGridController>();

            if (laserShooter == null)
                laserShooter = FindFirstObjectByType<LaserShooter>();

            if (resetController == null)
                resetController = FindFirstObjectByType<StageResetController>();

            if (stageFlowController == null)
                stageFlowController = FindFirstObjectByType<InGameStageFlowController>();
        }

        private void BuildUI()
        {
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            font = Resources.Load<TMP_FontAsset>("Font/TMP/PF스타더스트 3");
            if (font == null)
                font = TMP_Settings.defaultFontAsset;

            GameObject canvasObject = new GameObject("RuntimeDebugOverlayCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform root = global::UI.LetterboxSafeFrame.Install(canvas, false, "DebugSafeFrame_16_9");
            panel = CreatePanel("DebugPanel", root, panelPosition, panelSize, new Color(0f, 0f, 0f, 0.82f));
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);

            TMP_Text title = CreateText("Title", panel, "[DEBUG OVERLAY]  F1", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(title.rectTransform, new Vector2(18f, -14f), new Vector2(panelSize.x - 36f, 42f));

            infoText = CreateText("InfoText", panel, "", 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetTopLeft(infoText.rectTransform, new Vector2(18f, -62f), new Vector2(panelSize.x - 36f, 360f));
            infoText.enableWordWrapping = false;

            RectTransform buttonRow = CreateRect("ButtonRow", panel);
            SetTopLeft(buttonRow, new Vector2(18f, -430f), new Vector2(panelSize.x - 36f, 46f));

            AddButton(buttonRow, "다음 스테이지", DebugNextStage, 0f, 138f);
            AddButton(buttonRow, "재시작", DebugRestart, 148f, 120f);
            AddButton(buttonRow, "레이저 발사", DebugFireLaser, 278f, 126f);

            RectTransform simulationRow = CreateRect("SimulationRow", panel);
            SetTopLeft(simulationRow, new Vector2(18f, -478f), new Vector2(panelSize.x - 36f, 46f));

            AddButton(simulationRow, "클리어 시뮬", DebugSimulateSolution, 0f, 190f);
            simulationSpeedButtonText = AddButton(simulationRow, "배속 x1", DebugCycleSimulationSpeed, 200f, 130f);
            gridToggleButtonText = AddButton(simulationRow, "그리드 표시", ToggleGridData, 340f, 150f);

            gridText = CreateText("GridLegendText", panel, "", 15f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetTopLeft(gridText.rectTransform, new Vector2(18f, -536f), new Vector2(panelSize.x - 36f, 34f));
            gridText.font = font;
            gridText.enableWordWrapping = true;

            gridVisualRoot = CreatePanel("GridVisualRoot", panel, new Vector2(18f, -574f), new Vector2(panelSize.x - 36f, 164f), new Color(0.03f, 0.04f, 0.06f, 0.92f));
            SetTopLeft(gridVisualRoot, new Vector2(18f, -574f), new Vector2(panelSize.x - 36f, 164f));
            gridVisualRoot.gameObject.SetActive(false);
        }

        private void RefreshNow()
        {
            EnsureReferences();

            if (infoText == null)
                return;

            StageData stage = gridManager != null ? gridManager.CurrentStageData : null;
            LaserResult result = laserShooter != null ? laserShooter.LastResult : null;

            int usedDistance = result != null ? result.MaxBeamStepCount : 0;
            bool hasDistanceLimit = stage != null && stage.useLaserDistanceLimit && stage.laserMaxDistance > 0;
            int remainingDistance = hasDistanceLimit ? Mathf.Max(0, stage.laserMaxDistance - usedDistance) : -1;
            int targetTotal = gridManager != null ? gridManager.GetTotalTargetCount() : 0;
            int targetActivated = gridManager != null ? gridManager.GetActivatedTargetCount() : 0;
            int beamCount = ResolveBeamCount(result);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Stage: {FormatStageName(stage)}");
            builder.AppendLine($"Grid Size: {FormatGridSize(stage)}");
            builder.AppendLine($"Player: {FormatPlayer()}");
            builder.AppendLine($"Laser Distance: {(hasDistanceLimit ? stage.laserMaxDistance.ToString() : "Unlimited")}");
            builder.AppendLine($"Used / Remaining: {usedDistance} / {(hasDistanceLimit ? remainingDistance.ToString() : "∞")}");
            builder.AppendLine($"Beam Count: {beamCount}");
            builder.AppendLine($"Segment Count: {(result != null ? result.Segments.Count : 0)}");
            builder.AppendLine($"Path Node Count: {(result != null ? result.PathNodes.Count : 0)}");
            builder.AppendLine($"Target Hits: {(result != null ? result.TargetHits.Count : 0)}");
            builder.AppendLine($"Targets: {targetActivated} / {targetTotal}");
            builder.AppendLine($"Loop Detected: {(result != null && result.LoopDetected)}");
            builder.AppendLine($"Distance Ended: {(result != null && result.DistanceEnded)}");
            builder.AppendLine($"Stage Solved: {(gridManager != null && gridManager.IsStageSolvedLocked)}");
            builder.AppendLine($"Clear Hole: {(gridManager != null && gridManager.IsClearHoleActive ? gridManager.ClearHolePosition.ToString() : "Inactive")}");

            infoText.text = builder.ToString();

            RefreshGridVisual(stage);

            RefreshDebugButtonLabels();
        }

        private void RefreshDebugButtonLabels()
        {
            if (simulationSpeedButtonText == null)
                return;

            int speed = stageFlowController != null ? stageFlowController.DebugSimulationSpeedMultiplier : 1;
            simulationSpeedButtonText.text = $"배속 x{Mathf.Clamp(speed, 1, 5)}";

            if (gridToggleButtonText != null)
                gridToggleButtonText.text = showGridData ? "그리드 숨김" : "그리드 표시";
        }

        private void RefreshGridVisual(StageData stage)
        {
            if (gridText != null)
                gridText.text = showGridData ? "그리드 데이터  @ 플레이어 / # 벽 / M 거울 / P 프리즘 / A 증폭기 / T 타깃 / O 구멍" : "그리드 데이터: 꺼짐";

            if (gridVisualRoot == null)
                return;

            ClearChildren(gridVisualRoot);
            gridVisualRoot.gameObject.SetActive(showGridData && stage != null && gridManager != null);

            if (!gridVisualRoot.gameObject.activeSelf)
                return;

            int width = Mathf.Max(1, stage.width);
            int height = Mathf.Max(1, stage.height);
            float gap = 3f;
            float rootWidth = gridVisualRoot.rect.width > 1f ? gridVisualRoot.rect.width : gridVisualRoot.sizeDelta.x;
            float rootHeight = gridVisualRoot.rect.height > 1f ? gridVisualRoot.rect.height : gridVisualRoot.sizeDelta.y;
            float availableWidth = Mathf.Max(1f, rootWidth - 12f);
            float availableHeight = Mathf.Max(1f, rootHeight - 12f);
            float cellSize = Mathf.Floor(Mathf.Min((availableWidth - gap * (width - 1)) / width, (availableHeight - gap * (height - 1)) / height));
            cellSize = Mathf.Clamp(cellSize, 12f, 34f);
            float gridWidth = width * cellSize + (width - 1) * gap;
            float gridHeight = height * cellSize + (height - 1) * gap;
            Vector2 origin = new Vector2((availableWidth - gridWidth) * 0.5f + 6f, -((availableHeight - gridHeight) * 0.5f + 6f));

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    int visualRow = height - 1 - y;
                    Vector2 anchoredPosition = origin + new Vector2(x * (cellSize + gap), -visualRow * (cellSize + gap));
                    CreateGridCellVisual(position, anchoredPosition, cellSize);
                }
            }
        }

        private void CreateGridCellVisual(Vector2Int position, Vector2 anchoredPosition, float cellSize)
        {
            char symbol = ResolveGridCellSymbol(position);
            Color color = ResolveGridCellColor(symbol);
            RectTransform cell = CreatePanel($"GridCell_{position.x}_{position.y}", gridVisualRoot, anchoredPosition, new Vector2(cellSize, cellSize), color);
            SetTopLeft(cell, anchoredPosition, new Vector2(cellSize, cellSize));

            TMP_Text label = CreateText("Label", cell, symbol == '.' ? "" : symbol.ToString(), Mathf.Clamp(cellSize * 0.52f, 10f, 18f), FontStyles.Bold, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = ResolveGridCellTextColor(symbol);
        }

        private char ResolveGridCellSymbol(Vector2Int position)
        {
            if (playerController != null && playerController.GridPosition == position)
                return '@';

            if (gridManager != null && gridManager.IsClearHoleActive && gridManager.ClearHolePosition == position)
                return 'O';

            GridObject gridObject = gridManager != null ? gridManager.GetObjectAt(position) : null;
            if (gridObject != null)
            {
                return gridObject.ObjectType switch
                {
                    PuzzleObjectType.Mirror => 'M',
                    PuzzleObjectType.Prism => 'P',
                    PuzzleObjectType.Lens => 'A',
                    PuzzleObjectType.Wall => '#',
                    _ => 'o'
                };
            }

            GridTarget target = gridManager != null ? gridManager.GetTargetAt(position) : null;
            if (target != null)
                return target.IsActivated ? 't' : 'T';

            if (gridManager != null && gridManager.HasWall(position))
                return '#';

            return '.';
        }

        private Color ResolveGridCellColor(char symbol)
        {
            return symbol switch
            {
                '@' => new Color(0.2f, 0.58f, 1f, 0.95f),
                '#' => new Color(0.28f, 0.3f, 0.36f, 0.98f),
                'M' => new Color(0.68f, 0.82f, 1f, 0.95f),
                'P' => new Color(0.64f, 0.45f, 1f, 0.95f),
                'A' => new Color(1f, 0.78f, 0.26f, 0.95f),
                'T' => new Color(0.28f, 0.9f, 0.5f, 0.92f),
                't' => new Color(0.1f, 1f, 0.25f, 0.98f),
                'O' => new Color(0.02f, 0.02f, 0.03f, 0.98f),
                'o' => new Color(0.75f, 0.78f, 0.85f, 0.95f),
                _ => new Color(0.1f, 0.12f, 0.16f, 0.95f)
            };
        }

        private Color ResolveGridCellTextColor(char symbol)
        {
            return symbol switch
            {
                '#' => Color.white,
                'O' => Color.white,
                '.' => new Color(0.45f, 0.48f, 0.55f, 1f),
                _ => Color.black
            };
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private string FormatStageName(StageData stage)
        {
            if (stage == null)
                return "None";

            if (!string.IsNullOrWhiteSpace(stage.stageName))
                return $"{stage.chapterName} / {stage.stageName}";

            return $"Chapter {stage.chapterIndex} - Stage {stage.stageIndexInChapter}";
        }

        private string FormatGridSize(StageData stage)
        {
            return stage != null ? $"{stage.width} x {stage.height}" : "None";
        }

        private string FormatPlayer()
        {
            if (playerController == null)
                return "None";

            return $"{playerController.GridPosition}, Facing {playerController.FacingDirection}";
        }

        private int ResolveBeamCount(LaserResult result)
        {
            if (result == null || result.Segments.Count <= 0)
                return 0;

            int maxBeamId = 0;
            for (int i = 0; i < result.Segments.Count; i++)
            {
                if (result.Segments[i].BeamId > maxBeamId)
                    maxBeamId = result.Segments[i].BeamId;
            }

            return maxBeamId + 1;
        }

        private void DebugNextStage()
        {
            if (stageFlowController == null)
                stageFlowController = FindFirstObjectByType<InGameStageFlowController>();

            if (stageFlowController != null)
                stageFlowController.DebugSkipToNextStage();
        }

        private void DebugRestart()
        {
            if (resetController == null)
                resetController = FindFirstObjectByType<StageResetController>();

            if (resetController != null)
                resetController.ResetStage();
        }

        private void DebugFireLaser()
        {
            if (laserShooter == null)
                laserShooter = FindFirstObjectByType<LaserShooter>();

            if (laserShooter != null)
                laserShooter.ShootFromPlayer();
        }

        private void DebugSimulateSolution()
        {
            if (stageFlowController == null)
                stageFlowController = FindFirstObjectByType<InGameStageFlowController>();

            if (stageFlowController != null)
                stageFlowController.DebugToggleSolutionSimulation();

            RefreshNow();
        }

        private void DebugCycleSimulationSpeed()
        {
            if (stageFlowController == null)
                stageFlowController = FindFirstObjectByType<InGameStageFlowController>();

            if (stageFlowController != null)
                stageFlowController.DebugCycleSimulationSpeed();

            RefreshNow();
        }

        private void ToggleGridData()
        {
            showGridData = !showGridData;
            RefreshNow();
        }

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;
            return rect;
        }

        private TMP_Text CreateText(string name, Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private TMP_Text AddButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action, float x, float width)
        {
            RectTransform rect = CreatePanel(label, parent, new Vector2(x, 0f), new Vector2(width, 42f), new Color(0.12f, 0.16f, 0.24f, 0.94f));
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(action);

            TMP_Text text = CreateText("Text", rect, label, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private void SetTopLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
