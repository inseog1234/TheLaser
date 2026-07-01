using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UI.InGame
{
    public partial class InGameStageFlowController
    {
        private readonly List<Vector2Int> dragNumberPositions = new();
        private readonly List<TextMeshPro> dragNumberTexts = new();
        private Transform dragNumberRoot;
        private bool isTileDragNumberActive;

        private void HandleTileDragNumbers()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || gridManager == null || Camera.main == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (IsPointerOverUI())
                    return;

                if (TryGetMouseGridPosition(out Vector2Int gridPosition))
                {
                    isTileDragNumberActive = true;
                    ClearTileDragNumbers();
                    AddTileDragNumber(gridPosition);
                }
            }

            if (isTileDragNumberActive && mouse.leftButton.isPressed)
            {
                if (TryGetMouseGridPosition(out Vector2Int gridPosition))
                    AddTileDragNumber(gridPosition);
            }

            if (isTileDragNumberActive && mouse.leftButton.wasReleasedThisFrame)
            {
                isTileDragNumberActive = false;
                StartCoroutine(ClearTileDragNumbersAfterDelay(0.45f));
            }
        }

        private bool TryGetMouseGridPosition(out Vector2Int gridPosition)
        {
            gridPosition = Vector2Int.zero;
            if (Mouse.current == null || gridManager == null || Camera.main == null)
                return false;

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(Camera.main.transform.position.z)));
            gridPosition = gridManager.WorldToGrid(worldPosition);
            return gridManager.IsInside(gridPosition);
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void AddTileDragNumber(Vector2Int gridPosition)
        {
            if (dragNumberPositions.Contains(gridPosition))
                return;

            dragNumberPositions.Add(gridPosition);

            if (dragNumberRoot == null)
                dragNumberRoot = new GameObject("TileDragNumbers").transform;

            GameObject obj = new GameObject($"TileDragNumber_{dragNumberPositions.Count}");
            obj.transform.SetParent(dragNumberRoot, false);
            obj.transform.position = gridManager.GridToWorld(gridPosition) + Vector3.back * 0.1f;

            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.text = dragNumberPositions.Count.ToString();
            tmp.fontSize = 5.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.25f, 1f);
            tmp.enableWordWrapping = false;
            MeshRenderer textRenderer = tmp.GetComponent<MeshRenderer>();
            if (textRenderer != null)
                textRenderer.sortingOrder = 500;
            dragNumberTexts.Add(tmp);
        }

        private IEnumerator ClearTileDragNumbersAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!isTileDragNumberActive)
                ClearTileDragNumbers();
        }

        private void ClearTileDragNumbers()
        {
            dragNumberPositions.Clear();

            for (int i = 0; i < dragNumberTexts.Count; i++)
            {
                if (dragNumberTexts[i] != null)
                    Destroy(dragNumberTexts[i].gameObject);
            }

            dragNumberTexts.Clear();
        }
    }
}
