using UnityEngine;

namespace Grid
{
    public class GridTarget : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private GameObject inactiveVisual;
        [SerializeField] private GameObject activatedVisual;

        [Header("Runtime")]
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private bool isActivated;

        public Vector2Int GridPosition => gridPosition;
        public bool IsActivated => isActivated;

        public void Initialize(Vector2Int position)
        {
            gridPosition = position;
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