#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Runtime
{
    public sealed class SharedTutorialDraft
    {
        public AuthoringEditSession Session { get; private set; }
        public string FlowId { get; private set; }
        public string LevelId { get; private set; }
        private JObject baseline;
        public bool IsChanged => !JToken.DeepEquals(baseline, Session.Get(FlowId).Data);

        public SharedTutorialDraft(AuthoringEditSession parent)
        {
            LevelId = parent.SelectedLevelId; FlowId = (string)parent.Get(LevelId).Data["tutorial"]["flowId"];
            if (string.IsNullOrEmpty(FlowId)) throw new InvalidOperationException("공유 구성을 연결한 뒤 원본 편집을 시작하세요.");
            baseline = parent.Get(FlowId).Data;
            Session = parent.Fork();
        }
        private SharedTutorialDraft() { }
        public void Apply(AuthoringEditSession parent)
        {
            ValidateParent(parent);
            ContentDocument changed = Session.Get(FlowId);
            parent.Apply("공유 튜토리얼 원본 적용", docs => docs[FlowId] = changed);
        }
        public ContentSnapshot Preview(AuthoringEditSession parent)
        {
            ValidateParent(parent);
            return new ContentSnapshot(parent.Documents.Select(doc => doc.Id == FlowId ? Session.Get(FlowId) : doc));
        }
        public void ValidateParent(AuthoringEditSession parent)
        {
            if (parent.SelectedLevelId != LevelId || (string)parent.Get(LevelId).Data["tutorial"]["flowId"] != FlowId)
                throw new InvalidOperationException("공유 편집을 시작한 레벨과 연결이 달라졌습니다. 사본을 취소하고 다시 여세요.");
            if (!JToken.DeepEquals(parent.Get(FlowId).Data, baseline))
                throw new InvalidOperationException("편집 중 공유 원본이 바뀌었습니다. 다른 변경을 덮어쓰지 않습니다. 사본을 취소하고 다시 여세요.");
        }
        public string ExportState() => new JObject
        {
            ["version"] = 1, ["flowId"] = FlowId, ["levelId"] = LevelId,
            ["baseline"] = baseline.DeepClone(), ["session"] = JObject.Parse(Session.ExportState())
        }.ToString(Formatting.None);
        public static SharedTutorialDraft Restore(string text)
        {
            JObject data = JObject.Parse(text);
            if ((int?)data["version"] != 1 || !(data["baseline"] is JObject original)) throw new ContentFormatException("공유 편집 사본의 복구 형식이 잘못됐습니다.");
            var draft = new SharedTutorialDraft
            {
                FlowId = (string)data["flowId"], LevelId = (string)data["levelId"], baseline = (JObject)original.DeepClone(),
                Session = AuthoringEditSession.Restore(data["session"].ToString(Formatting.None))
            };
            if (draft.Session.SelectedLevelId != draft.LevelId || draft.Session.Get(draft.FlowId).Kind != "tutorialFlow")
                throw new ContentFormatException("공유 편집 사본의 대상이 잘못됐습니다.");
            return draft;
        }
    }
}
#endif
