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
        public bool HasLevelChanges(string id)
        {
            if (!current.Documents.TryGetValue(id, out ContentDocument level) || level.Kind != "level")
                throw new ContentFormatException("없는 레벨 ID: " + id);
            return !saved.TryGetValue(id, out ContentDocument baseline) ||
                !ReferenceEquals(level, baseline) && !JToken.DeepEquals(level.Data, baseline.Data);
        }

        // 현재 레벨의 저장 전 변경만 제거한다. 다른 문서의 이력은 같은 순서로 남긴다.
        public void DiscardLevelChanges(string id)
        {
            if (!HasLevelChanges(id)) return;
            saved.TryGetValue(id, out ContentDocument baseline);
            string fallback = current.Documents.ContainsKey(savedLevelId) && savedLevelId != id ? savedLevelId :
                current.Documents.Values.FirstOrDefault(doc => doc.Kind == "level" && doc.Id != id)?.Id;
            if (baseline == null && fallback == null)
                throw new ContentFormatException("마지막 레벨을 제거할 수 없습니다.");

            // 모든 사전을 먼저 준비해 예외가 발생해도 기존 세션과 이력은 그대로 둔다.
            State next = ProjectLevelState(current, id, baseline, fallback);
            if (baseline != null) ContentDocumentValidator.ValidateDocumentReferences(baseline, next.Documents);
            State[] previous = ProjectLevelHistory(undo, next, id, baseline, fallback);
            State[] following = ProjectLevelHistory(redo, next, id, baseline, fallback);
            current = next;
            undo.Clear();
            foreach (State state in previous.Reverse()) undo.Push(state);
            redo.Clear();
            foreach (State state in following.Reverse()) redo.Push(state);
        }

        private static State ProjectLevelState(State source, string id, ContentDocument baseline, string fallback)
        {
            var documents = new Dictionary<string, ContentDocument>(source.Documents, StringComparer.Ordinal);
            if (baseline == null) documents.Remove(id);
            else documents[id] = baseline;
            string selected = source.LevelId;
            if (!documents.ContainsKey(selected))
                selected = fallback != null && documents.ContainsKey(fallback) ? fallback :
                    documents.Values.First(doc => doc.Kind == "level").Id;
            return new State
            {
                Documents = documents, LevelId = selected,
                Cells = selected == source.LevelId ? (int[])source.Cells.Clone() : Array.Empty<int>()
            };
        }

        private static State[] ProjectLevelHistory(Stack<State> history, State origin, string id, ContentDocument baseline, string fallback)
        {
            var result = new List<State>();
            State adjacent = origin;
            foreach (State source in history)
            {
                // 저장 전 원자적 이력을 투영하면 공유 문서 생성과 레벨 연결이 분리될 수 있다.
                State projected = source.BeforeSave ? source : ProjectLevelState(source, id, baseline, fallback);
                if (!source.BeforeSave && baseline != null)
                    ContentDocumentValidator.ValidateDocumentReferences(baseline, projected.Documents);
                // 버린 레벨만 바꾼 단계는 제거하고 다른 문서의 원자적 변경은 한 단계로 유지한다.
                if (Equal(projected.Documents, adjacent.Documents)) continue;
                result.Add(projected);
                adjacent = projected;
            }
            return result.ToArray();
        }
    }
}
#endif
