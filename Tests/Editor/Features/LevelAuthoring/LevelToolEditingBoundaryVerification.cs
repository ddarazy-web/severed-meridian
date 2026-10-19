using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Levels;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolEditingBoundaryVerification
    {
        private static readonly StringBuilder Evidence = new StringBuilder();
        private static AuthoringEditSession session;
        private static MethodInfo edit;
        private static string levelId;

        public static void Run()
        {
            string folder = Path.GetFullPath("Logs/LevelToolRefactorStage02/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            int exit = 0;
            try
            {
                edit = typeof(AuthoringEditSession).GetMethod("EditLevel");
                Check(edit != null, "common selected-level edit transaction exists");
                AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
                workspace.Create(Path.Combine(folder, "Project"), LevelToolDocuments.NewProject());
                session = workspace.Session; levelId = session.SelectedLevelId;
                Dictionary<string, JObject> definitions = LevelToolDocuments.Definitions(session);
                JObject baseline = Read();
                Change(level =>
                {
                    Property("SetName", level, "경계 검사");
                    Property("SetNumber", level, 7);
                    Property("SetMoveCount", level, 29);
                    Property("SetColorEnabled", level, "Type5", true);
                });
                JObject changed = Read();
                Check((string)changed["displayName"] == "경계 검사" && (int)changed["levelNumber"] == 7 &&
                    (int)changed["moveCount"] == 29 && changed["colors"].Values<string>().Contains("Type5"), "level property writes use common rules");
                session.Undo(); Check(Equal(Read(), baseline) && !session.CanUndo, "combined property edit has one Undo");
                session.Redo(); Check(Equal(Read(), changed), "Redo restores exact property document");
                Change(level => Property("SetMoveCount", level, 29));
                session.Undo(); Check(Equal(Read(), baseline) && !session.CanUndo, "unchanged property creates no extra Undo");
                session.Discard();
                Change(level => Property("SetMoveCount", level, -1));
                Check((int)Read()["moveCount"] == -1, "unfinished invalid move count remains editable");
                Reject(() => workspace.Save(), "invalid draft is rejected at save");
                session.Undo(); Check(Equal(Read(), baseline), "invalid draft can be undone"); session.Discard();

                string normal = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Block" &&
                    (string)pair.Value["supply"]?["behavior"] == "FixedNormal").Key;
                string large = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 2).Key;
                string small = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 1).Key;
                Change(level => LevelDocumentEditing.Place(level, definitions, large, 3, 3, false));
                session.Undo(); session.Discard();
                Change(level => LevelDocumentEditing.Place(level, definitions, large, 3, 3, false));
                Change(level => Property("PlaceSelection", level, definitions, small, new[] { 30, 31, 39, 40 }, true, "Type1", "Horizontal", "Obstacle"));
                JArray items = (JArray)Read()["elements"];
                Check(items.Count == 1 && (string)items[0]["definitionId"] == small && (int)items[0]["coordinate"]["row"] == 3 &&
                    (int)items[0]["coordinate"]["column"] == 3, "2x2 selection replaces original body once at original anchor");
                session.Undo(); Check(Read()["elements"].Count() == 1 && (string)Read()["elements"][0]["definitionId"] == large, "replacement Undo restores large body");
                session.Redo(); Check(Equal(Read()["elements"], items), "replacement Redo preserves generated instance identity"); session.Discard();

                Change(level => LevelDocumentEditing.Place(level, definitions, normal, 4, 4, false));
                JObject occupied = Read(); string state = session.ExportState();
                Reject(() => Change(level => Property("PlaceSelection", level, definitions, normal, new[] { 39, 40 }, false, "Type1", "Horizontal", "Block")),
                    "last-cell collision rejects entire area placement");
                Check(session.ExportState() == state, "failed area keeps document selection Undo and Redo unchanged");
                Reject(() => Change(level => Property("PlaceSelection", level, definitions, small, new[] { 40 }, true, "Type1", "Horizontal", "Obstacle")),
                    "layer-preserving replacement rejects deleting another layer");
                Check(Equal(Read(), occupied), "other-layer failure preserves original block");
                string instanceId = (string)occupied["elements"][0]["instanceId"];
                Change(level => { Property("SetElementDurability", level, instanceId, -2); Property("SetElementAppearance", level, instanceId, "Type2", "Vertical"); });
                Check((int)Read()["elements"][0]["durability"] == -2 && (string)Read()["elements"][0]["color"] == "Type2" &&
                    (string)Read()["elements"][0]["rocketDirection"] == "Vertical", "element draft properties preserve values without new clamping");
                session.Undo(); Check(Equal(Read(), occupied), "element property Undo restores all fields");
                Change(level => Property("EraseSelection", level, definitions, "Block", new[] { 40, 40 }));
                Check(!Read()["elements"].Any(), "selected layer erase reuses existing removal rule");
                session.Undo(); Check(Equal(Read(), occupied), "erase Undo restores original instance"); session.Discard();

                string otherId = DocumentEditing.DuplicateLevel(session, levelId, 2, "두 번째");
                JObject original = session.Get(levelId).Data;
                Change(level => Property("SetName", level, "현재 선택만"));
                Check((string)session.Get(otherId).Data["displayName"] == "현재 선택만" && Equal(original, session.Get(levelId).Data),
                    "common edit resolves current selected level without stale previous reference"); session.Discard();
                session.Apply("구형 초안", docs => docs[levelId].Data["schemaVersion"] = 4);
                state = session.ExportState();
                Reject(() => Change(level => Property("SetName", level, "거절")), "old schema requires explicit migration");
                Check(state == session.ExportState(), "schema refusal has no partial edit or Undo entry"); session.Discard();

                Change(level => SupplyDocumentEditing.SetSourceProperty(level, definitions, new[] { 0 }, "mode", "Fixed"));
                Change(level => Supply("AppendItem", level, definitions, new[] { 0 }, normal, 2, "Type1", "Horizontal", 1));
                Change(level => Supply("AppendItem", level, definitions, new[] { 0 }, normal, 3, "Type2", "Horizontal", 1));
                JObject supplyBefore = Read();
                Change(level => Supply("SetItemProperty", level, definitions, 0, 0, "count", new JValue(4)));
                Check((int)Read()["elementSupply"]["sources"][0]["items"][0]["count"] == 4, "supply item property uses validated common replacement");
                session.Undo(); Check(Equal(Read(), supplyBefore), "supply item property Undo preserves list");
                Change(level => Supply("ReorderItem", level, definitions, 0, 1, false));
                Check((int)Read()["elementSupply"]["sources"][0]["items"][0]["count"] == 3, "supply reorder preserves item payload");
                session.Undo();
                Change(level => Supply("ReorderItem", level, definitions, 0, 0, true));
                Check(Read()["elementSupply"]["sources"][0]["items"].Count() == 1, "supply remove affects one item"); session.Undo();
                state = session.ExportState();
                Reject(() => Change(level => Supply("SetItemProperty", level, definitions, 0, 0, "count", new JValue(-1))), "invalid supply item still rejected by existing validator");
                Check(state == session.ExportState(), "supply validation failure keeps transaction intact");
                JObject normalSupply = (JObject)Read()["elementSupply"].DeepClone();
                Change(level => SupplyDocumentEditing.EditTutorial(level, definitions, candidate =>
                    Supply("PlaceTutorialSources", candidate, new[] { 40 })));
                Check(Equal(Read()["elementSupply"], normalSupply) && (string)Read()["tutorial"]["supply"]["sources"].Last()["mode"] == "Fixed",
                    "tutorial source edit changes only tutorial supply and creates Fixed mode");
                Change(level => Property("SetName", level, "저장 왕복"));
                JObject beforeSave = Read(); workspace.Save(); workspace.Reload("Discard"); session = workspace.Session;
                Check(Equal(Read(), beforeSave) && !session.IsDirty, "saved JSON roundtrip preserves all edited document values");

                LevelDefinition legacy = ScriptableObject.CreateInstance<LevelDefinition>();
                ContentDocument old;
                try
                {
                    legacy.name = "구형 전환 검사";
                    JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[{\"coordinate\":{\"row\":4,\"column\":4},\"kind\":1,\"fixedColor\":0,\"rocketDirection\":0}]}", legacy);
                    old = UnityAuthoringCodec.WriteDraft(legacy, "level", "legacy", _ => null, _ => null);
                }
                finally { UnityEngine.Object.DestroyImmediate(legacy); }
                AuthoringToolWorkspace legacyWorkspace = new AuthoringToolWorkspace();
                ContentSnapshot legacyProject = LevelToolDocuments.NewProject();
                string legacyId = legacyProject.Documents.First(doc => doc.Kind == "level").Id;
                old = new ContentDocument("level", legacyId, old.Data);
                legacyWorkspace.Create(Path.Combine(folder, "Legacy"), new ContentSnapshot(legacyProject.Documents.Select(doc => doc.Id == legacyId ? old : doc)));
                AuthoringEditSession migration = legacyWorkspace.Session;
                LevelToolDocuments.UpgradeSelectedLevel(migration);
                JObject upgraded = migration.Get(old.Id).Data;
                Check((int)upgraded["schemaVersion"] == 5, "explicit migration uses current selected level");
                foreach (JProperty property in old.Data.Properties())
                    if (!new[] { "schemaVersion", "elements", "elementSupply" }.Contains(property.Name))
                        Check(Equal(property.Value, upgraded[property.Name]), "migration preserves field " + property.Name);
                migration.Undo(); Check(Equal(migration.Get(old.Id).Data, old.Data) && !migration.CanUndo, "migration has one Undo restoring legacy JSON");
                migration.Redo(); Check(Equal(migration.Get(old.Id).Data, upgraded), "migration Redo preserves generated IDs");
                LevelToolDocuments.UpgradeSelectedLevel(migration);
                migration.Undo(); Check(Equal(migration.Get(old.Id).Data, old.Data), "repeated migration creates no extra Undo");
            }
            catch (Exception error) { exit = 1; Evidence.AppendLine("FAIL " + error); Debug.LogException(error); }
            File.WriteAllText(Path.Combine(folder, "result.txt"), Evidence.ToString());
            Debug.Log("LevelToolEditingBoundaryVerification: " + folder);
            EditorApplication.Exit(exit);
        }

        private static JObject Read() => session.Get(levelId).Data;
        private static bool Equal(JToken left, JToken right) => JToken.DeepEquals(left, right);
        private static void Change(Action<JObject> action) => Invoke(edit, session, "경계 편집 검사", action);
        private static void Property(string name, params object[] args) => Invoke(typeof(LevelDocumentEditing).GetMethod(name), null, args);
        private static void Supply(string name, params object[] args) => Invoke(typeof(SupplyDocumentEditing).GetMethod(name), null, args);
        private static void Invoke(MethodInfo method, object target, params object[] args)
        {
            if (method == null) throw new Exception("Expected editing API missing");
            try { method.Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (ContentFormatException) { rejected = true; }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, message);
        }
        private static void Check(bool valid, string message)
        {
            if (!valid) throw new Exception(message);
            Evidence.AppendLine("PASS " + message);
        }
    }
}
