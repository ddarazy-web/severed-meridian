namespace GameScreen
{
    public readonly struct PuzzleFeedbackCue
    {
        public PuzzleFeedbackCueKind Kind { get; }
        public float Time { get; }
        public PuzzleFeedbackCue(PuzzleFeedbackCueKind kind, float time) { Kind = kind; Time = time; }
    }
    public enum PuzzleFeedbackCueKind
    {
        Swap, InvalidSwap, Match, Rocket, Bomb, DroneDive, DroneHit, Magnet,
        Landing, MissionArrival, MissionComplete, Start, Win, Lose
    }
}
