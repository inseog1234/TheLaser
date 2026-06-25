using UnityEngine;
using Core;

namespace Grid
{
    public class GridTarget : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private GameObject inactiveVisual;
        [SerializeField] private GameObject activatedVisual;
        [SerializeField] private GameObject failedVisual;

        [Header("Runtime")]
        [SerializeField] private string targetId;
        [SerializeField] private TargetType targetType = TargetType.Normal;
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private LaserColorKind requiredColor = LaserColorKind.Default;
        [SerializeField] private int sequenceValue = 1;
        [SerializeField] private float detectionRadius = 0.25f;
        [SerializeField] private bool requireDifferentColors;
        [SerializeField] private bool stopLaserOnHit = true;
        [SerializeField] private bool isActivated;

        public string TargetId => targetId;
        public TargetType TargetType => targetType;
        public Vector2Int GridPosition => gridPosition;
        public LaserColorKind RequiredColor => requiredColor;
        public int SequenceValue => sequenceValue;
        public float DetectionRadius => detectionRadius;
        public bool RequireDifferentColors => requireDifferentColors;
        public bool StopLaserOnHit => stopLaserOnHit;
        public bool IsActivated => isActivated;

        public void Initialize(Vector2Int position)
        {
            targetId = $"Target_{position.x}_{position.y}";
            targetType = TargetType.Normal;
            gridPosition = position;
            requiredColor = LaserColorKind.Default;
            sequenceValue = 1;
            detectionRadius = 0.25f;
            requireDifferentColors = false;
            stopLaserOnHit = true;
            SetActivated(false);
        }

        public void Initialize(StageTargetData data)
        {
            targetId = string.IsNullOrWhiteSpace(data.targetId)
                ? $"Target_{data.position.x}_{data.position.y}"
                : data.targetId;

            targetType = data.targetType;
            gridPosition = data.position;
            requiredColor = data.requiredColor;
            sequenceValue = data.sequenceValue;
            detectionRadius = data.detectionRadius;
            requireDifferentColors = data.requireDifferentColors;
            stopLaserOnHit = data.stopLaserOnHit;
            SetActivated(false);
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
        }
    }
}
