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

            AddButton(buttonRow, "Next Stage", DebugNextStage, 0f, 138f);
            AddButton(buttonRow, "Restart", DebugRestart, 148f, 120f);
            AddButton(buttonRow, "Fire Laser", DebugFireLaser, 278f, 126f);
            AddButton(buttonRow, "Show Grid", ToggleGridData, 414f, 150f);

            gridText = CreateText("GridText", panel, "", 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetTopLeft(gridText.rectTransform, new Vector2(18f, -488f), new Vector2(panelSize.x - 36f, 250f));
            gridText.font = font;
            gridText.enableWordWrapping = false;
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

            if (gridText != null)
                gridText.text = showGridData && gridManager != null ? BuildGridText() : "Grid Data: OFF";
        }

        private string BuildGridText()
        {
            Vector2Int? playerPosition = null;
            if (playerController != null)
                playerPosition = playerController.GridPosition;

            string ascii = gridManager.BuildDebugGridAscii(playerPosition);
            return "Grid Data  (# wall, @ player, M mirror, P prism, A amplifier, T target, t active)\n" + ascii;
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

        private void AddButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action, float x, float width)
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
