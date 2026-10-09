using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
internal static partial class Program
{
    private static void EditingChecks()
    {
        var stored = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read();
        string levelId = stored.Snapshot.Documents.First(doc => doc.Kind == "level").Id;
        var session = new AuthoringEditSession(stored, levelId);
        int moves = (int)session.Get(levelId).Data["moveCount"];
        Check(!session.IsDirty && !session.CanUndo, "edit session starts clean");
        session.SelectCells(new[] { 10, 11 });
        session.Apply("이동 수", docs => docs[levelId].Data["moveCount"] = moves + 1);
        Check(session.IsDirty && session.CanUndo, "one edit creates history");
        session.Undo();
        Check(!session.IsDirty && (int)session.Get(levelId).Data["moveCount"] == moves && session.SelectedCells.SequenceEqual(new[] { 10, 11 }), "undo restores content selection and clean state");
        session.Redo();
        Check((int)session.Get(levelId).Data["moveCount"] == moves + 1, "redo restores edit");
        var isolated = session.Get(levelId); isolated.Data["moveCount"] = 777;
        Check((int)session.Get(levelId).Data["moveCount"] == moves + 1, "edit reads are isolated");
        try { session.Apply("실패", docs => { docs[levelId].Data["moveCount"] = 999; throw new InvalidOperationException("injected"); }); }
        catch (InvalidOperationException) { }
        Check((int)session.Get(levelId).Data["moveCount"] == moves + 1, "failed edit is atomic");
        session.Apply("미완성", docs => docs[levelId].Data["moveCount"] = -1);
        Check(session.ValidationError != null, "incomplete edit remains editable");
        Reject(() => session.CreateSnapshot(), "incomplete edit cannot publish or play");
        var recovered = AuthoringEditSession.Restore(session.ExportState());
        Check(recovered.ValidationError != null && recovered.IsDirty && recovered.CanUndo, "reload preserves incomplete draft and history");
        recovered.Undo(); recovered.Redo();
        Check(recovered.ValidationError != null && recovered.SelectedCells.SequenceEqual(new[] { 10, 11 }), "reload history keeps selection");
        session.Undo();
        Check(session.ValidationError == null, "undo repairs incomplete edit");
        session.Apply("변경 없음", docs => { });
        Check(session.CanRedo, "no-op retains redo");
        string originalCompletion = (string)session.Get(levelId).Data["tutorial"]["completionId"];
        string duplicateId = DocumentEditing.DuplicateLevel(session, levelId, 881991, "복사 레벨");
        Check(duplicateId != levelId && (int)session.Get(duplicateId).Data["levelNumber"] == 881991, "duplicate has new document identity and number");
        Check((string)session.Get(duplicateId).Data["tutorial"]["completionId"] == originalCompletion, "duplicate preserves explicit tutorial completion meaning");
        Reject(() => DocumentEditing.DuplicateLevel(session, levelId, 881991, "충돌"), "duplicate level number rejected before mutation");
        session.Undo();
        Check(!session.Documents.Any(doc => doc.Id == duplicateId), "duplicate is one undo step");
        session.Redo();
        Check(session.SelectedLevelId == duplicateId, "duplicate redo restores selection");
        DocumentEditing.SetField(session, duplicateId, "levelNumber", new JValue(881992));
        Check(session.Get(duplicateId).Id == duplicateId && (string)session.Get(duplicateId).Data["tutorial"]["completionId"] == originalCompletion, "renumber preserves document and explicit completion IDs");
                var add = typeof(DocumentEditing).GetMethod("AddLevel");
        Check(add != null, "new document operation exists");
        if (add != null)
        {
            string created = (string)add.Invoke(null, new object[] { session, session.Get(levelId).Data, 881994, "새 레벨" });
            Check(created != levelId && session.SelectedLevelId == created, "new document selected without writing disk");
            session.Undo(); Check(!session.Documents.Any(doc => doc.Id == created), "new document one undo step");
        }
        var discard = typeof(AuthoringEditSession).GetMethod("Discard");
        Check(discard != null, "discard operation exists");
        if (discard != null)
        {
            discard.Invoke(session, null);
            Check(!session.IsDirty && !session.CanUndo && !session.CanRedo && session.SelectedLevelId == levelId, "discard restores saved baseline without disk overwrite");
        }
        string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/session-" + Guid.NewGuid().ToString("N"));
        var repository = new ContentSnapshotStore(root);
        var initial = repository.Publish(stored.Snapshot, null);
        var writer = new AuthoringEditSession(initial, levelId);
        writer.Apply("변경", docs => docs[levelId].Data["moveCount"] = moves + 2);
        writer.Save(repository);
        Check(!writer.IsDirty && writer.CanUndo, "save retains history and marks clean");
        writer.Undo(); Check(writer.IsDirty, "undo after save is dirty");
        writer.Redo(); Check(!writer.IsDirty, "redo to saved version is clean");
        var external = repository.Read(); repository.Publish(external.Snapshot, external.Hash);
        writer.Apply("충돌", docs => docs[levelId].Data["moveCount"] = moves + 3);
        Reject(() => writer.Save(repository), "session save detects external conflict");
        Check(writer.IsDirty && (int)writer.Get(levelId).Data["moveCount"] == moves + 3, "save failure retains draft");
        var shapes = typeof(DocumentEditing).Assembly.GetType("LevelAuthoring.Editing.ShapeDocumentEditing");
        Check(shapes != null, "shape document operations exist");
        if (shapes != null)
        {
            writer.Apply("모양 준비", docs => { int index = 0; foreach (var cell in docs[levelId].Data["board"]["cells"]) cell["isActive"] = index++ == 40; });
            string shapeId = (string)shapes.GetMethod("Register").Invoke(null, new object[] { writer, levelId, "가운데", new[] { "기록" } });
            Check(writer.Get(shapeId).Kind == "shape" && (bool)writer.Get(shapeId).Data["cells"][40], "shape captures current mask");
            string[] uses = (string[])shapes.GetMethod("Usage").Invoke(null, new object[] { writer, shapeId });
            Check(uses.Contains(levelId), "shape usage follows current JSON documents");
            bool refused = false;
            try { shapes.GetMethod("DeleteUnused").Invoke(null, new object[] { writer, shapeId }); }
            catch (System.Reflection.TargetInvocationException) { refused = true; }
            Check(refused && writer.Documents.Any(doc => doc.Id == shapeId), "used shape deletion rejected");
            writer.Undo(); Check(!writer.Documents.Any(doc => doc.Id == shapeId), "shape registration one undo step");
            writer.Undo();
        }
        var saveAs = typeof(AuthoringEditSession).GetMethod("SaveAs");
        Check(saveAs != null, "save copy operation exists");
        if (saveAs != null)
        {
            var copyRepository = new ContentSnapshotStore(root + "-copy");
            saveAs.Invoke(writer, new object[] { copyRepository });
            Check(!writer.IsDirty && writer.CanUndo && (int)copyRepository.Read().Snapshot.Get(levelId).Data["moveCount"] == moves + 3, "save copy retains history and IDs");
            Check((int)repository.Read().Snapshot.Get(levelId).Data["moveCount"] == moves + 2, "save copy leaves conflicted original intact");
        }
    }
}