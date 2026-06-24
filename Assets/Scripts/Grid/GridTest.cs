using System.Text;
using UnityEngine;
using Core;

using UnityEngine.InputSystem;

namespace Grid
{
    public class GridTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;

        [Header("Inspector Test Values")]
        [SerializeField] private Vector2Int testPosition = new Vector2Int(3, 3);
        [SerializeField] private Vector2Int worldTestPosition = new Vector2Int(1, 1);

        [Header("Debug Panel")]
        [SerializeField] private bool showDebugPanel = true;
        [SerializeField] private Vector2 panelPosition = new Vector2(20f, 20f);
        [SerializeField] private Vector2 panelSize = new Vector2(620f, 480f);

        private readonly StringBuilder debugTextBuilder = new();

        private string debugText;
        private GUIStyle boxStyle;
        private GUIStyle textStyle;

        private void Start()
        {
            RunGridTest();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                RunGridTest();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            {
                RunGridTest();
            }
        }

        [ContextMenu("Run Grid Test")]
        private void RunGridTest()
        {
            debugTextBuilder.Clear();

            AppendLine("---------------------");

            if (gridManager == null)
            {
                return;
            }

            StageData stageData = gridManager.CurrentStageData;

            if (stageData == null)
            {
                return;
            }

            AppendLine("-----------------------");
            AppendLine($"스테이지 이름: {stageData.stageName}");
            AppendLine($"스테이지 번호: {stageData.stageNumber}");
            AppendLine($"맵 크기: {stageData.width} x {stageData.height}");
            AppendLine($"셀 크기: {gridManager.CellSize}");

            AppendLine("-----------------------");
            AppendLine($"테스트 좌표: {testPosition}");
            AppendLine($"맵 안쪽인가: {gridManager.IsInside(testPosition)}");
            AppendLine($"벽인가: {gridManager.HasWall(testPosition)}");
            AppendLine($"목표인가: {gridManager.HasTarget(testPosition)}");
            AppendLine($"오브젝트가 있는가: {gridManager.HasObject(testPosition)}");
            AppendLine($"걸어갈 수 있는가: {gridManager.IsWalkable(testPosition)}");
            AppendLine($"비어 있는가: {gridManager.IsEmpty(testPosition)}");

            Vector3 worldPos = gridManager.GridToWorld(worldTestPosition);
            Vector2Int gridPos = gridManager.WorldToGrid(worldPos);

            AppendLine("----------------------");
            AppendLine($"Grid 좌표: {worldTestPosition}");
            AppendLine($"World 좌표: {worldPos}");
            AppendLine($"World -> Grid 결과: {gridPos}");

            AppendLine("----------------------");
            AppendLine($"등록된 오브젝트 수: {gridManager.GetObjects().Count}");
            AppendLine($"전체 셀 수: {gridManager.GetCells().Count}");

            debugText = debugTextBuilder.ToString();
        }

        private void AppendLine(string text)
        {
            debugTextBuilder.AppendLine(text);
        }

        private void OnGUI()
        {
            if (!showDebugPanel)
                return;

            InitializeGUIStyles();

            Rect panelRect = new Rect(panelPosition.x, panelPosition.y, panelSize.x, panelSize.y);

            GUI.Box(panelRect, "Grid Debug Panel", boxStyle);

            Rect textRect = new Rect(panelRect.x + 16f, panelRect.y + 42f, panelRect.width - 32f, panelRect.height - 58f);

            GUI.Label(textRect, debugText, textStyle);
        }

        private void InitializeGUIStyles()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.fontSize = 20;
                boxStyle.alignment = TextAnchor.UpperCenter;
                boxStyle.padding = new RectOffset(12, 12, 12, 12);
            }

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label);
                textStyle.fontSize = 18;
                textStyle.alignment = TextAnchor.UpperLeft;
                textStyle.normal.textColor = Color.white;
                textStyle.wordWrap = true;
            }
        }
    }
}