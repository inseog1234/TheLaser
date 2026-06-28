using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Core;

namespace Grid
{
    [Serializable]
    public class TargetColorVisual
    {
        public LaserColorKind colorKind = LaserColorKind.Default;
        public Color color = Color.white;
        public string symbol = "";
    }

    public class GridTarget : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private GameObject inactiveVisual;
        [SerializeField] private GameObject activatedVisual;
        [SerializeField] private GameObject failedVisual;

        [Header("Info Visual")]
        [SerializeField] private SpriteRenderer colorIndicatorRenderer;
        [SerializeField] private TMP_Text symbolText;
        [SerializeField] private bool showNormalTargetSymbol = false;
        [SerializeField] private string normalTargetSymbol = "";
        [SerializeField] private string intersectionTargetSymbol = "×";
        [SerializeField] private Color normalTargetColor = Color.white;
        [SerializeField] private Color sequenceTargetColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color intersectionTargetColor = new Color(1f, 0.45f, 1f, 1f);
        [SerializeField] private List<TargetColorVisual> colorPalette = new();

        [Header("Runtime")]
        [SerializeField] private string targetId;
        [SerializeField] private TargetType targetType = TargetType.Normal;
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private LaserColorKind requiredColor = LaserColorKind.Default;
        [SerializeField] private int sequenceValue = 1;
        [SerializeField] private float detectionRadius = 0.25f;
        [SerializeField] private int requiredIntersectionCount = 2;
        [SerializeField] private List<LaserColorKind> intersectionColors = new();
        [SerializeField] private bool requireDifferentColors;
        [SerializeField] private bool stopLaserOnHit = true;
        [SerializeField] private bool isActivated;

        public string TargetId => targetId;
        public TargetType TargetType => targetType;
        public Vector2Int GridPosition => gridPosition;
        public LaserColorKind RequiredColor => requiredColor;
        public int SequenceValue => sequenceValue;
        public float DetectionRadius => detectionRadius;
        public int RequiredIntersectionCount => requiredIntersectionCount;
        public IReadOnlyList<LaserColorKind> IntersectionColors => intersectionColors;
        public bool RequireDifferentColors => requireDifferentColors;
        public bool StopLaserOnHit => stopLaserOnHit;
        public bool IsActivated => isActivated;

        private void OnValidate()
        {
            RefreshInfoVisual();
        }

        public void Initialize(Vector2Int position)
        {
            targetId = $"Target_{position.x}_{position.y}";
            targetType = TargetType.Normal;
            gridPosition = position;
            requiredColor = LaserColorKind.Default;
            sequenceValue = 1;
            detectionRadius = 0.25f;
            requiredIntersectionCount = 2;
            intersectionColors = new List<LaserColorKind>();
            requireDifferentColors = false;
            stopLaserOnHit = true;
            SetActivated(false);
            RefreshInfoVisual();
        }

        public void Initialize(StageTargetData data)
        {
            targetId = string.IsNullOrWhiteSpace(data.targetId) ? $"Target_{data.position.x}_{data.position.y}" : data.targetId;
            targetType = data.targetType;
            gridPosition = data.position;
            requiredColor = data.requiredColor;
            sequenceValue = data.sequenceValue;
            detectionRadius = data.detectionRadius;
            requiredIntersectionCount = Mathf.Clamp(data.requiredIntersectionCount, 2, 3);
            intersectionColors = data.intersectionColors != null ? new List<LaserColorKind>(data.intersectionColors) : new List<LaserColorKind>();
            requireDifferentColors = data.requireDifferentColors;
            stopLaserOnHit = data.stopLaserOnHit;
            SetActivated(false);
            RefreshInfoVisual();
        }

        public void SetActivated(bool activated)
        {
            isActivated = activated;

            if (inactiveVisual != null)
                inactiveVisual.SetActive(!activated);

            if (activatedVisual != null)
                activatedVisual.SetActive(activated);

            if (failedVisual != null)
                failedVisual.SetActive(false);

            RefreshInfoVisual();
        }

        public void SetFailed(bool failed)
        {
            if (failedVisual != null)
                failedVisual.SetActive(failed);

            if (failed)
            {
                if (inactiveVisual != null)
                    inactiveVisual.SetActive(false);

                if (activatedVisual != null)
                    activatedVisual.SetActive(false);
            }

            RefreshInfoVisual();
        }

        private void RefreshInfoVisual()
        {
            Color targetColor = ResolveTargetColor();
            string symbol = ResolveTargetSymbol();

            if (colorIndicatorRenderer != null)
            {
                colorIndicatorRenderer.color = targetColor;
                colorIndicatorRenderer.gameObject.SetActive(ShouldShowColorIndicator() && string.IsNullOrEmpty(symbol));
            }

            if (symbolText != null)
            {
                symbolText.richText = true;
                symbolText.text = symbol;
                symbolText.color = targetType == TargetType.Intersection ? Color.white : targetColor;
                symbolText.gameObject.SetActive(!string.IsNullOrEmpty(symbol));
            }
        }

        private bool ShouldShowColorIndicator()
        {
            if (targetType == TargetType.ColorLocked)
                return true;

            if (targetType == TargetType.SequenceColorLocked)
                return true;

            if (targetType == TargetType.SequenceLocked)
                return true;

            if (targetType == TargetType.Intersection)
                return true;

            return showNormalTargetSymbol;
        }

        private Color ResolveTargetColor()
        {
            if (targetType == TargetType.ColorLocked)
                return ResolveLaserColor(requiredColor);

            if (targetType == TargetType.SequenceLocked)
                return ResolveLaserColor(requiredColor);

            if (targetType == TargetType.SequenceColorLocked)
                return ResolveLaserColor(requiredColor);

            if (targetType == TargetType.Intersection)
                return intersectionTargetColor;

            return normalTargetColor;
        }

        private string ResolveTargetSymbol()
        {
            if (targetType == TargetType.SequenceLocked || targetType == TargetType.SequenceColorLocked)
                return sequenceValue.ToString();

            if (targetType == TargetType.Intersection)
                return BuildIntersectionSymbol();

            return showNormalTargetSymbol ? normalTargetSymbol : "";
        }

        private string BuildIntersectionSymbol()
        {
            int count = Mathf.Clamp(requiredIntersectionCount, 2, 3);

            if (intersectionColors == null)
                intersectionColors = new List<LaserColorKind>();

            List<string> parts = new();

            for (int i = 0; i < count; i++)
            {
                LaserColorKind colorKind = i < intersectionColors.Count ? intersectionColors[i] : LaserColorKind.Default;
                Color color = ResolveLaserColor(colorKind);
                parts.Add($"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>|</color>");
            }

            return string.Join(" ", parts);
        }

        private Color ResolveLaserColor(LaserColorKind colorKind)
        {
            for (int i = 0; i < colorPalette.Count; i++)
            {
                if (colorPalette[i] != null && colorPalette[i].colorKind == colorKind)
                    return colorPalette[i].color;
            }

            return colorKind switch
            {
                LaserColorKind.Red => new Color(1f, 0.2f, 0.2f, 1f),
                LaserColorKind.Blue => new Color(0.2f, 0.45f, 1f, 1f),
                LaserColorKind.Green => new Color(0.25f, 1f, 0.35f, 1f),
                LaserColorKind.Yellow => new Color(1f, 0.9f, 0.2f, 1f),
                LaserColorKind.Purple => new Color(0.75f, 0.25f, 1f, 1f),
                LaserColorKind.White => Color.white,
                _ => normalTargetColor
            };
        }
    }
}