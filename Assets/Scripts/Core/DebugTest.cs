using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Core;
using UnityEngine.InputSystem;

public class CoreTest : MonoBehaviour
{
    [Header("Test Data")]
    [SerializeField] private StageData stageData;

    [Header("Inspector Test Values")]
    [SerializeField] private GridDirection testDirection = GridDirection.Right;
    [SerializeField] private Vector2Int testPosition = new Vector2Int(3, 3);

    [Header("Debug Panel")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private Vector2 panelPosition = new Vector2(20f, 20f);
    [SerializeField] private Vector2 panelSize = new Vector2(560f, 460f);

    private readonly StringBuilder debugTextBuilder = new();

    private string debugText;
    private GUIStyle boxStyle;
    private GUIStyle textStyle;

    private void Start()
    {
        RunCoreTest();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RunCoreTest();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
        {
            RunCoreTest();
        }
    }

    private void RunCoreTest()
    {
        debugTextBuilder.Clear();

        AppendLine("---------------------------");
        AppendLine("");

        AppendLine($"테스트 방향: {testDirection}");
        AppendLine($"방향 -> 벡터: {testDirection.ToVector()}");
        AppendLine($"반대 방향: {testDirection.Opposite()}");
        AppendLine($"시계 방향 회전: {testDirection.RotateClockwise()}");
        AppendLine($"반시계 방향 회전: {testDirection.RotateCounterClockwise()}");
        AppendLine($"Z축 앵글: {testDirection.ToAngleZ()}");

        AppendLine("");

        if (stageData != null)
        {
            AppendLine("-------------------------");
            AppendLine($"스테이지 이름: {stageData.stageName}");
            AppendLine($"스테이지 번호: {stageData.stageNumber}");
            AppendLine($"맵 크기: {stageData.width} x {stageData.height}");
            AppendLine($"테스트 좌표: {testPosition}");
            AppendLine($"그리드 범위 안쪽에 있음: {stageData.IsInside(testPosition)}");
            AppendLine($"벽 위치임: {stageData.HasWall(testPosition)}");
            AppendLine($"목표 위치임: {stageData.HasTarget(testPosition)}");
            AppendLine($"기능 오브젝트 위치임: {stageData.HasObject(testPosition)}");
        }
        else
        {
            AppendLine("------------------------");
            AppendLine("StageData가 연결되지 않았음.");
        }

        AppendLine("");

        HashSet<LaserState> visited = new();

        LaserState stateA = new LaserState(testPosition, testDirection);
        LaserState stateB = new LaserState(testPosition, testDirection);

        visited.Add(stateA);

        AppendLine("-----------------------");
        AppendLine($"State A: {stateA}");
        AppendLine($"State B: {stateB}");
        AppendLine($"레이저 상태 중복 여부: {visited.Contains(stateB)}");

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

        GUI.Box(panelRect, "Core Debug Panel", boxStyle);

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