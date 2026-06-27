using System.Collections.Generic;
using UnityEngine;
using Core;

namespace Grid
{
    public class GridDistanceSensor : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private GameObject inactiveVisual;
        [SerializeField] private GameObject activatedVisual;

        [Header("Runtime")]
        [SerializeField] private string sensorId;
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private float detectionRadius = 0.5f;
        [SerializeField] private bool activateTransformZone;
        [SerializeField] private string transformZoneId;
        [SerializeField] private List<DistanceSensorTriggerData> triggers = new();
        [SerializeField] private bool isActivated;

        public string SensorId => sensorId;
        public Vector2Int GridPosition => gridPosition;
        public float DetectionRadius => detectionRadius;
        public bool ActivateTransformZone => activateTransformZone;
        public string TransformZoneId => transformZoneId;
        public IReadOnlyList<DistanceSensorTriggerData> Triggers => triggers;
        public bool IsActivated => isActivated;

        public void Initialize(DistanceSensorData data)
        {
            sensorId = string.IsNullOrWhiteSpace(data.sensorId)
                ? $"Sensor_{data.position.x}_{data.position.y}"
                : data.sensorId;

            gridPosition = data.position;
            detectionRadius = data.detectionRadius;
            activateTransformZone = data.activateTransformZone;
            transformZoneId = data.transformZoneId;
            triggers = data.triggers != null ? new List<DistanceSensorTriggerData>(data.triggers) : new List<DistanceSensorTriggerData>();
            SetActivated(false);
        }

        public void SetActivated(bool activated)
        {
            isActivated = activated;

            if (inactiveVisual != null)
                inactiveVisual.SetActive(!activated);

            if (activatedVisual != null)
                activatedVisual.SetActive(activated);
        }
    }
}
