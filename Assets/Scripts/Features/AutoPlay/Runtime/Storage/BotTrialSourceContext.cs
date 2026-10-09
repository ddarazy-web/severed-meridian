using System;

namespace AutoPlay
{
    // 제작 문서 형식은 호출자가 소유한다. 실행기는 고정된 원본과 이동 횟수별 사본만 전달받는다.
    public sealed class BotTrialSourceContext
    {
        public string DocumentId { get; }
        public string Content { get; }
        private readonly Func<int, string> withMoves;
        public BotTrialSourceContext(string documentId, string content, Func<int, string> withMoves)
        {
            if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("시험 원본 ID와 내용이 필요합니다.");
            DocumentId = documentId; Content = content;
            this.withMoves = withMoves ?? throw new ArgumentNullException(nameof(withMoves));
        }
        public string ForMoves(int moves) => withMoves(moves);
    }
}
