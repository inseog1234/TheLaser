using System.Collections.Generic;
using Audio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LevelEditor
{
    public partial class LevelEditorController
    {
        private void PushManualUndoSnapshotForPlayerRoute()
        {
            if (editingStageData == null)
                return;

            if (lastCommittedSnapshot != null)
                undoStack.Add(lastCommittedSnapshot.Clone());
            else
                undoStack.Add(editingStageData.Clone());

            TrimUndoStack();
            redoStack.Clear();
            skipAutoHistoryThisFrame = true;
        }

        private void BeginPlayerRouteDraw(Vector2Int position)
        {
            if (editingStageData == null || !editingStageData.IsInside(position))
                return;

            PushManualUndoSnapshotForPlayerRoute();
            if (editingStageData.playerRoutePositions == null)
                editingStageData.playerRoutePositions = new List<Vector2Int>();

            editingStageData.playerRoutePositions.Clear();
            AddPlayerRoutePathTo(position);
            playerRouteLastGridPosition = position;
            isDrawingPlayerRoute = true;
            selectedTool = null;
            PlayEditorSfx(FmodRuntimeAudio.SfxEditorObjPlaced);
            RebuildPalette();
            RebuildStageVisuals();
            SetStatus("플레이어 이동경로 기록 중... 마우스를 드래그하세요.");
        }

        private void UpdatePlayerRouteDraw()
        {
            if (editingStageData == null || !hasHoverPosition || IsPointerOverUI())
                return;

            if (!Mouse.current.leftButton.isPressed)
                return;

            if (hoverGridPosition == playerRouteLastGridPosition)
                return;

            AddPlayerRoutePathTo(hoverGridPosition);
            playerRouteLastGridPosition = hoverGridPosition;
            RebuildStageVisuals();
        }

        private void AddPlayerRoutePathTo(Vector2Int target)
        {
            if (editingStageData == null || !editingStageData.IsInside(target))
                return;

            if (editingStageData.playerRoutePositions == null)
                editingStageData.playerRoutePositions = new List<Vector2Int>();

            if (editingStageData.playerRoutePositions.Count <= 0)
            {
                editingStageData.playerRoutePositions.Add(target);
                return;
            }

            Vector2Int current = editingStageData.playerRoutePositions[editingStageData.playerRoutePositions.Count - 1];
            AddStraightRouteSteps(current, new Vector2Int(target.x, current.y));
            Vector2Int afterHorizontal = editingStageData.playerRoutePositions[editingStageData.playerRoutePositions.Count - 1];
            AddStraightRouteSteps(afterHorizontal, target);
        }

        private void AddStraightRouteSteps(Vector2Int from, Vector2Int to)
        {
            Vector2Int delta = to - from;
            int stepX = delta.x == 0 ? 0 : delta.x > 0 ? 1 : -1;
            int stepY = delta.y == 0 ? 0 : delta.y > 0 ? 1 : -1;
            Vector2Int cursor = from;

            while (cursor != to)
            {
                cursor += new Vector2Int(stepX, stepY);
                AddUniquePlayerRoutePoint(cursor);
            }
        }

        private void AddUniquePlayerRoutePoint(Vector2Int position)
        {
            if (editingStageData == null || !editingStageData.IsInside(position))
                return;

            List<Vector2Int> route = editingStageData.playerRoutePositions;
            if (route == null)
                return;

            if (route.Count > 0 && route[route.Count - 1] == position)
                return;

            route.Add(position);
        }

        private int GetPlayerRouteCount()
        {
            return editingStageData != null && editingStageData.playerRoutePositions != null ? editingStageData.playerRoutePositions.Count : 0;
        }

        private void BuildPlayerRouteVisual()
        {
            if (editingStageData == null || editingStageData.playerRoutePositions == null || editingStageData.playerRoutePositions.Count <= 0)
                return;

            GameObject root = new GameObject("PlayerRoute");
            root.transform.SetParent(stageRoot.transform);
            root.transform.position = Vector3.zero;

            List<Vector3> points = BuildManhattanRouteWorldPoints(editingStageData.playerRoutePositions);
            if (points.Count >= 2)
            {
                GameObject lineObj = new GameObject("PlayerRouteLine");
                lineObj.transform.SetParent(root.transform);
                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.positionCount = points.Count;
                for (int i = 0; i < points.Count; i++)
                    line.SetPosition(i, points[i]);

                line.startWidth = 0.08f;
                line.endWidth = 0.08f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = new Color(0.2f, 1f, 0.68f, 0.95f);
                line.endColor = new Color(0.2f, 1f, 0.68f, 0.95f);
                line.sortingOrder = 48;
                line.numCornerVertices = 0;
                line.numCapVertices = 0;
            }

            for (int i = 0; i < editingStageData.playerRoutePositions.Count; i++)
            {
                Vector2Int position = editingStageData.playerRoutePositions[i];
                if (!editingStageData.IsInside(position))
                    continue;

                GameObject marker = CreateSpriteObject($"RoutePoint_{i + 1}", root.transform, GridToWorld(position), new Vector2(0.34f, 0.34f), new Color(0.2f, 1f, 0.68f, 0.82f), 49);
                AddWorldLabel(marker.transform, (i + 1).ToString(), Vector3.zero, 0.22f, Color.black, 50);
            }

            stageVisuals.Add(root);
        }

        private List<Vector3> BuildManhattanRouteWorldPoints(List<Vector2Int> route)
        {
            List<Vector3> points = new List<Vector3>();
            if (route == null || route.Count <= 0)
                return points;

            points.Add(GridToWorld(route[0]) + new Vector3(0f, 0f, -0.02f));

            for (int i = 1; i < route.Count; i++)
            {
                Vector2Int previous = route[i - 1];
                Vector2Int current = route[i];
                Vector2Int corner = new Vector2Int(current.x, previous.y);

                if (corner != previous && corner != current)
                    AddRouteWorldPoint(points, corner);

                AddRouteWorldPoint(points, current);
            }

            return points;
        }

        private void AddRouteWorldPoint(List<Vector3> points, Vector2Int gridPosition)
        {
            Vector3 world = GridToWorld(gridPosition) + new Vector3(0f, 0f, -0.02f);
            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], world) < 0.001f)
                return;

            points.Add(world);
        }
    }
}
