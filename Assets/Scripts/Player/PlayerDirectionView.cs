using UnityEngine;
using Core;

namespace Player
{
    public class PlayerDirectionView : MonoBehaviour
    {
        [Header("Direction View")]
        [SerializeField] private Transform directionArrow;

        [Header("Option")]
        [SerializeField] private bool hideWhenArrowIsNull = true;

        public void SetDirection(GridDirection direction)
        {
            if (directionArrow == null)
            {
                if (hideWhenArrowIsNull)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            directionArrow.localRotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());
        }
    }
}