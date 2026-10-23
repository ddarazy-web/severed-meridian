#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool.Presentation
{
    public sealed partial class LevelToolPropertiesPanel
    {
        private static readonly string[] MissionKinds = { "Color", "Crate", "Web", "Scrap", "Dust", "Safe", "ColorLock", "Appliance", "Mold", "Recovery" };
        private static readonly string[] MissionTargets = { "일반 블록", "나무상자", "거미줄", "고철 뭉치", "먼지", "고물 회수 캡슐", "색 자물쇠", "금속기둥 상자", "곰팡이", "회수 부품" };
        private static readonly string[] ColorLabels = { "분홍", "노랑", "파랑", "초록", "보라" };

        private static string MissionSummary(string kind, string tint, int count)
        {
            int index = Array.IndexOf(MissionKinds, kind);
            string target = index < 0 ? "대상 확인 필요" : MissionTargets[index];
            if (kind == "Color") target = ColorLabels[Math.Max(0, Math.Min(4, Array.IndexOf(new[] { "Type1", "Type2", "Type3", "Type4", "Type5" }, tint)))] + " " + target;
            return kind == "Mold" ? target + " 전부 제거" : target + " " + count + "개 " + (kind == "Color" || kind == "Recovery" ? "수집" : "제거");
        }

        private void DrawMissions(JObject level)
        {
            VisualElement section = new VisualElement { name = "mission-section" };
            section.AddToClassList("mission-section"); inspector.Add(section);
            JArray missions = (JArray)level["missions"];
            section.Add(new Label("클리어 미션 · 최대 4개"));
            section.Add(new Label("모든 미션을 달성하면 레벨을 완료합니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            VisualElement draft = new VisualElement { name = "mission-draft" };
            for (int i = 0; i < missions.Count; i++)
            {
                int index = i;
                JObject value = (JObject)missions[i];
                VisualElement card = new VisualElement(); card.AddToClassList("mission-card"); section.Add(card);
                Image image = new Image { name = "mission-image-" + i, scaleMode = UnityEngine.ScaleMode.ScaleToFit };
                image.style.width = 40; image.style.height = 40; card.Add(image);
                actions.LoadMissionImage?.Invoke(image, (string)value["kind"], (string)value["color"]);
                card.Add(new Label(MissionSummary((string)value["kind"], (string)value["color"], (int)value["count"]))
                    { name = "mission-summary-" + i, style = { whiteSpace = WhiteSpace.Normal } });
                Button(card, "edit-mission-" + i, "수정", () => OpenMissionDraft(draft, level, index));
                Button(card, "remove-mission-" + i, "삭제", () => Change("미션 삭제", data => MissionDocumentEditing.Remove(data, index)));
            }
            Button add = Button(section, "add-mission", "+ 미션 만들기", () => OpenMissionDraft(draft, level, missions.Count));
            add.SetEnabled(missions.Count < 4);
            section.Add(draft);
        }

        private void OpenMissionDraft(VisualElement host, JObject level, int index)
        {
            host.Clear();
            object draftToken = new object(); host.userData = draftToken;
            JObject original = ((JArray)level["missions"]).ElementAtOrDefault(index) as JObject;
            string kind = (string)original?["kind"] ?? "Color";
            string tint = (string)original?["color"] ?? level["colors"].Values<string>().FirstOrDefault() ?? "Type1";
            int amount = (int?)original?["count"] ?? 10;
            int purposeIndex = kind == "Color" ? 0 : kind == "Recovery" ? 2 : 1;
            DropdownField purpose = new DropdownField("1. 무엇을 하나요?", new[] { "일반 블록 수집", "장애물·덮개 제거", "부품 회수" }.ToList(), purposeIndex) { name = "mission-purpose" };
            DropdownField target = new DropdownField("2. 대상", MissionTargets.Skip(1).Take(8).ToList(), Math.Max(0, Array.IndexOf(MissionKinds, kind) - 1)) { name = "mission-target" };
            DropdownField colors = new DropdownField("2. 블록 색", ColorLabels.ToList(), Math.Max(0, Array.IndexOf(new[] { "Type1", "Type2", "Type3", "Type4", "Type5" }, tint))) { name = "mission-color" };
            IntegerField count = new IntegerField("3. 목표 수량") { name = "mission-count", value = amount };
            Label help = new Label { name = "mission-help", style = { whiteSpace = WhiteSpace.Normal } };
            Image preview = new Image { name = "mission-preview", scaleMode = UnityEngine.ScaleMode.ScaleToFit };
            preview.style.width = 48; preview.style.height = 48;
            host.Add(purpose); host.Add(target); host.Add(colors); host.Add(preview); host.Add(count); host.Add(help);
            Func<bool> active = CaptureValidity();
            Button apply = Button(host, "apply-mission", "적용", () =>
            {
                if (host.userData != draftToken) return;
                Change(original == null ? "미션 추가" : "미션 수정", data => MissionDocumentEditing.Set(data, index, kind, tint, count.value));
                host.userData = null; host.Clear();
            });
            Button(host, "cancel-mission", "취소", () => { host.userData = null; host.Clear(); });
            void Validate()
            {
                if (!active() || host.userData != draftToken) return;
                purposeIndex = purpose.index;
                kind = purposeIndex == 0 ? "Color" : purposeIndex == 2 ? "Recovery" : MissionKinds[target.index + 1];
                tint = "Type" + (colors.index + 1);
                target.style.display = purposeIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;
                colors.style.display = purposeIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                count.SetEnabled(kind != "Mold");
                try
                {
                    // 초안만 검증하고 적용 전까지 세션과 Undo 이력을 변경하지 않는다.
                    JObject candidate = new JObject { ["missions"] = level["missions"].DeepClone(), ["colors"] = level["colors"].DeepClone() };
                    MissionDocumentEditing.Set(candidate, index, kind, tint, count.value);
                    help.text = MissionSummary(kind, tint, count.value) + (kind == "Mold" ? " · 남은 곰팡이가 0개가 되면 완료됩니다." : " · 목표 수량은 1 이상이어야 합니다.");
                    apply.SetEnabled(true);
                }
                catch (Exception error) { help.text = error.Message; apply.SetEnabled(false); }
                actions.LoadMissionImage?.Invoke(preview, kind, tint);
            }
            Bind(purpose, _ => Validate()); Bind(target, _ => Validate()); Bind(colors, _ => Validate()); Bind(count, _ => Validate());
            Validate();
        }
    }
}
#endif
