using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;

namespace UI.InGame
{
    public partial class InGameStageFlowController
    {
        private Transform editorTestRoutePreviewRoot;

        private void RefreshEditorTestPlayerRoutePreview()
        {
            ClearEditorTestPlayerRoutePreview();

            if (!GameSceneRequest.IsEditorTestPlay || gridManager == null || currentStage == null)
                return;

            List<Vector2Int> route = currentStage.playerRoutePositions;
            if (route == null || route.Count <= 0)
                return;

            editorTestRoutePreviewRoot = new GameObject("EditorTestPlayerRoutePreview").transform;

            List<Vector3> points = BuildEditorTestRouteWorldPoints(route);
            if (points.Count >= 2)
            {
                GameObject lineObject = new GameObject("PlayerRouteLine");
                lineObject.transform.SetParent(editorTestRoutePreviewRoot, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.positionCount = points.Count;
                for (int i = 0; i < points.Count; i++)
                    line.SetPosition(i, points[i]);

                line.startWidth = 0.08f;
                line.endWidth = 0.08f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = new Color(0.2f, 1f, 0.68f, 0.95f);
                line.endColor = new Color(0.2f, 1f, 0.68f, 0.95f);
                line.sortingOrder = 460;
                line.numCornerVertices = 0;
                line.numCapVertices = 0;
            }

            for (int i = 0; i < route.Count; i++)
            {
                if (!currentStage.IsInside(route[i]))
                    continue;

                CreateEditorTestRouteMarker(route[i], i + 1);
            }
        }

        private List<Vector3> BuildEditorTestRouteWorldPoints(List<Vector2Int> route)
        {
            List<Vector3> points = new List<Vector3>();
            if (route == null || route.Count <= 0 || gridManager == null)
                return points;

            AddEditorTestRouteWorldPoint(points, route[0]);
            for (int i = 1; i < route.Count; i++)
            {
                Vector2Int previous = route[i - 1];
                Vector2Int current = route[i];
                Vector2Int corner = new Vector2Int(current.x, previous.y);

                if (corner != previous && corner != current)
                    AddEditorTestRouteWorldPoint(points, corner);

                AddEditorTestRouteWorldPoint(points, current);
            }

            return points;
        }

        private void AddEditorTestRouteWorldPoint(List<Vector3> points, Vector2Int gridPosition)
        {
            Vector3 worldPosition = gridManager.GridToWorld(gridPosition) + new Vector3(0f, 0f, -0.2f);
            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], worldPosition) < 0.001f)
                return;

            points.Add(worldPosition);
        }

        private void CreateEditorTestRouteMarker(Vector2Int gridPosition, int index)
        {
            if (editorTestRoutePreviewRoot == null || gridManager == null)
                return;

            GameObject marker = new GameObject($"RoutePoint_{index}");
            marker.transform.SetParent(editorTestRoutePreviewRoot, false);
            marker.transform.position = gridManager.GridToWorld(gridPosition) + new Vector3(0f, 0f, -0.24f);

            SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = whiteSprite;
            markerRenderer.color = new Color(0.2f, 1f, 0.68f, 0.78f);
            markerRenderer.sortingOrder = 461;
            marker.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

            GameObject label = new GameObject("RouteNumber");
            label.transform.SetParent(marker.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.03f);

            TextMeshPro text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = index.ToString();
            text.fontSize = 2.4f;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.rectTransform.sizeDelta = new Vector2(1.2f, 0.6f);

            MeshRenderer textRenderer = text.GetComponent<MeshRenderer>();
            if (textRenderer != null)
                textRenderer.sortingOrder = 462;
        }

        private void ClearEditorTestPlayerRoutePreview()
        {
            if (editorTestRoutePreviewRoot == null)
                return;

            Destroy(editorTestRoutePreviewRoot.gameObject);
            editorTestRoutePreviewRoot = null;
        }
    }
}
