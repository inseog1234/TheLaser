namespace Core
{
    // 무슨 칸인지
    public enum CellType
    {
        Empty = 0,
        Wall = 1,
        Target = 2
    }

    // 뭔 기능의 오브젝트인지
    // 기존 저장값 보호를 위해 순서 유지: None, Wall, Mirror, Prism
    public enum PuzzleObjectType
    {
        None = 0,
        Wall = 1,
        Mirror = 2,
        Prism = 3,
        Lens = 4
    }

    // 무슨 상호작용이 가능한지
    public enum ManipulationType
    {
        None = 0,
        PushOnly = 1,
        RotateOnly = 2,
        PushAndRotate = 3
    }

    // 거울 모양
    public enum MirrorShape
    {
        None = 0,
        NormalL = 1,   // ㄴ
        ReverseL = 2   // 역ㄴ
    }

    // 레이저 색상 상태
    public enum LaserColorKind
    {
        Default = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,
        Purple = 5,
        White = 6
    }

    // 프리즘 기능
    public enum PrismType
    {
        Splitter = 0,    // 분기 프리즘
        Color = 1,       // 색상 프리즘
        Refraction = 2   // 45도 굴절 프리즘
    }

    public enum PrismSplitterMode
    {
        ForwardAndLeft = 0,
        ForwardAndRight = 1,
        ForwardLeftRight = 2,
        LeftAndRight = 3
    }

    public enum RefractionMode
    {
        Clockwise45 = 0,
        CounterClockwise45 = 1
    }

    public enum LensType
    {
        DistanceAmplifier = 0
    }

    public enum TargetType
    {
        Normal = 0,
        ColorLocked = 1,
        SequenceLocked = 2,
        Intersection = 3
    }

    public enum TransformZoneType
    {
        Rotate90 = 0,
        Mirror = 1
    }

    public enum MirrorAxis
    {
        Vertical = 0,    // 좌우 대칭, x 반전
        Horizontal = 1   // 상하 대칭, y 반전
    }
}
