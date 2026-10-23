#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Validation;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public sealed partial class AuthoringEditSession
    {
        public sealed class LevelWorkCheckpoint
        {
            internal AuthoringEditSession Owner;
            internal ContentDocument Baseline;
            internal HashSet<object> ExistingStates;
            internal int UndoFloor;
            internal string Revision;
            internal bool Active = true;
            public string LevelId => Baseline.Id;
        }

        public LevelWorkCheckpoint BeginLevelWork()
        {
            if (SelectedLevelId == null) throw new InvalidOperationException("먼저 작업할 레벨을 선택하세요.");
            return new LevelWorkCheckpoint
            {
                Owner = this, Baseline = Get(SelectedLevelId), UndoFloor = undo.Count, Revision = Revision,
                ExistingStates = new HashSet<object>(undo.Concat(redo).Select(state => (object)state.Documents).Append(current.Documents))
            };
        }

        public bool CanUndoLevelWork(LevelWorkCheckpoint checkpoint)
        {
            ValidateLevelWork(checkpoint);
            return undo.Count > checkpoint.UndoFloor;
        }

        public void CompleteLevelWork(LevelWorkCheckpoint checkpoint)
        {
            ValidateLevelWork(checkpoint);
            checkpoint.Active = false;
        }

        public void CancelLevelWork(LevelWorkCheckpoint checkpoint)
        {
            ValidateLevelWork(checkpoint);
            // 작업 이후 이력에서 해당 레벨만 투영한다. 이전 이력과 다른 문서는 그대로 공유한다.
            var projectedDocuments = new Dictionary<ContentDocument, ContentDocument>();
            State next = ProjectWorkState(current, checkpoint, projectedDocuments);
            State[] previous = ProjectWorkHistory(undo, next, checkpoint, projectedDocuments);
            State[] following = ProjectWorkHistory(redo, next, checkpoint, projectedDocuments);
            current = next;
            undo.Clear(); foreach (State state in previous.Reverse()) undo.Push(state);
            redo.Clear(); foreach (State state in following.Reverse()) redo.Push(state);
            checkpoint.Active = false;
        }

        private State ProjectWorkState(State source, LevelWorkCheckpoint checkpoint, Dictionary<ContentDocument, ContentDocument> cache)
        {
            ContentDocument document = source.Documents[checkpoint.LevelId];
            if (!cache.TryGetValue(document, out ContentDocument restored))
            {
                JObject data = (JObject)document.Data.DeepClone();
                // 튜토리얼과 함께 편집한 배치·흐름·공급만 되돌린다. 이름·미션·이동 횟수 등은 보존한다.
                foreach (string field in new[] { "tutorial", "board", "colors", "initialBlocks", "obstacles", "covers", "dust", "flow", "connections", "supply", "recoveryParts", "elements", "elementSupply" })
                {
                    if (checkpoint.Baseline.Data.TryGetValue(field, out JToken value)) data[field] = value.DeepClone();
                    else data.Remove(field);
                }
                restored = ContentDocument.FromParsedBody(document.Kind, document.Id, data);
                cache.Add(document, restored);
            }
            State result = ProjectLevelState(source, checkpoint.LevelId, restored, checkpoint.LevelId);
            ContentDocumentValidator.ValidateDocumentReferences(restored, result.Documents);
            return result;
        }

        private State[] ProjectWorkHistory(Stack<State> history, State origin, LevelWorkCheckpoint checkpoint, Dictionary<ContentDocument, ContentDocument> cache)
        {
            List<State> result = new List<State>();
            State adjacent = origin;
            foreach (State state in history)
            {
                State projected = checkpoint.ExistingStates.Contains(state.Documents) ? state :
                    ProjectWorkState(state, checkpoint, cache);
                if (Equal(projected.Documents, adjacent.Documents)) continue;
                result.Add(projected); adjacent = projected;
            }
            return result.ToArray();
        }

        private void ValidateLevelWork(LevelWorkCheckpoint checkpoint)
        {
            if (checkpoint == null || checkpoint.Owner != this || !checkpoint.Active || checkpoint.LevelId != SelectedLevelId)
                throw new InvalidOperationException("현재 레벨의 작성 작업이 아닙니다.");
            if (Revision != checkpoint.Revision || undo.Count < checkpoint.UndoFloor)
                throw new InvalidOperationException("작성 시작 이전 상태로 변경되었습니다. 다른 작업을 덮어쓰지 않습니다.");
        }
    }
}
#endif
