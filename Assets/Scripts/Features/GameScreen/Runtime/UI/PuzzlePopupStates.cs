using PopupUI;

namespace GameScreen
{
    public sealed class PuzzlePauseState : PopupState
    {
        public bool Busy;
        public override PopupState Copy() => new PuzzlePauseState { Busy = Busy };
    }
    public sealed class PuzzleResultState : PopupState
    {
        public string Title, Body;
        public bool NextVisible, NextEnabled, Busy;
        public override PopupState Copy() => new PuzzleResultState {
            Title = Title, Body = Body, NextVisible = NextVisible, NextEnabled = NextEnabled, Busy = Busy
        };
    }
    public sealed class PuzzleDescriptionState : PopupState
    {
        public string Text;
        public override PopupState Copy() => new PuzzleDescriptionState { Text = Text };
    }
}
