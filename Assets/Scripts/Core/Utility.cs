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

    public static class MirrorShapeExtensions
    {
        public static MirrorShape Rotate90(this MirrorShape shape)
        {
            return shape == MirrorShape.Slash ? MirrorShape.BackSlash : MirrorShape.Slash;
        }

        public static float VisualZAngle(this MirrorShape shape)
        {
            return shape == MirrorShape.Slash ? 45f : -45f;
        }
    }
}
