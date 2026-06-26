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
        None, Wall, Mirror, Prism, Lens
    }

    // 무슨 상호작용이 가능한지
    public enum ManipulationType
    {
        None, PushOnly, RotateOnly, PushAndRotate
    }

    // 거울 모양
    public enum MirrorShape
    {
        None,
        NormalL,   // ㄴ
        ReverseL   // 역ㄴ
    }

    // 레이저 색상 상태
    public enum LaserColorKind
    {
        Default, Red, Blue, Green, Yellow, Purple, White
    }

    // 프리즘 기능
    public enum PrismType
    {
        Splitter, // 분기 프리즘
        Color, // 색상 프리즘
        Refraction // 45도 굴절 프리즘
    }

    public enum PrismSplitterMode
    {
        ForwardAndLeft, ForwardAndRight, ForwardLeftRight, LeftAndRight
    }

    public enum RefractionMode
    {
        Clockwise45, CounterClockwise45
    }

    public enum LensType
    {
        DistanceAmplifier
    }

    public enum TargetType
    {
        Normal, ColorLocked, SequenceLocked, Intersection, SequenceColorLocked
    }

    public enum TransformZoneType
    {
        Rotate90, Mirror
    }

    public enum MirrorAxis
    {
        Vertical, // 좌우 대칭, x 반전
        Horizontal // 상하 대칭, y 반전
    }
}
