using UnityEngine;

namespace Core
{
    // 무슨 칸인지
    public enum CellType
    {
        Empty, Wall, Target
    }

    // 뭔 기능의 오브젝트인지
    public enum PuzzleObjectType
    {
        None, Mirror, Prism
    }

    // 무슨 상호작용이 가능한지
    public enum ManipulationType
    {
        None, PushOnly, RotateOnly, PushAndRotate
    }

    // 거울 방향
    public enum MirrorShape
    {
        None,
        Slash, // 정 방향
        BackSlash // 역 방향
    }
}
