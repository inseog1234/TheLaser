using UnityEngine;

namespace Core
{
    public static class ManipulationTypeExtensions
    {
        public static bool CanPush(this ManipulationType manipulationType)
        {
            return manipulationType == ManipulationType.PushOnly || manipulationType == ManipulationType.PushAndRotate;
        }

        public static bool CanRotate(this ManipulationType manipulationType)
        {
            return manipulationType == ManipulationType.RotateOnly || manipulationType == ManipulationType.PushAndRotate;
        }
    }
}
