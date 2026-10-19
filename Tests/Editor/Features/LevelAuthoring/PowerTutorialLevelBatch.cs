using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using Levels;
using Tutorial;
using Simulation;
using Board;
using UnityEditor;
namespace LevelAuthoring.Editor {
public static class PowerTutorialLevelBatch {
public static void Refine() {
try {
var store=new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main"));var original=store.Read();
var backup=new ContentSnapshotStore(Path.GetFullPath("ContentData/Backups/power-tutorial-before-refine-20261016"));
backup.Publish(original.Snapshot,null);
var docs=original.Snapshot.Documents.Select(d=>d.Kind=="level"?new ContentDocument(d.Kind,d.Id,LevelTool.LevelToolDocuments.UpgradePreview(d.Data)):d).ToList();
foreach(var doc in docs.Where(d=>d.Kind=="level" && (int)d.Data["levelNumber"]==15)) {
var source=((JArray)doc.Data["tutorial"]["supply"]["sources"]).Single(s=>(int)s["coordinate"]["column"]==4);
source["items"][0]["color"]="Type4";
}
var candidate=new ContentSnapshot(docs);
foreach(var doc in docs.Where(d=>d.Kind=="level")) {
var level=JsonPuzzlePlayAdapter.CreateRequest(candidate,doc.Id,12345).CreateDefinition();
try {var issues=LevelTutorialReplayValidator.Validate(level);if(issues.Count>0)throw new Exception("Level "+level.LevelNumber+": "+string.Join(" | ",issues));}
finally{UnityEngine.Object.DestroyImmediate(level);}
}
store.Publish(candidate,original.Hash);
Inspect();
}catch(Exception e){File.WriteAllText("Logs/PowerTutorialLevels/refine-error.txt",e.ToString());EditorApplication.Exit(1);}
}
public static void Inspect() {
try {
var snapshot=new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main")).Read().Snapshot;
var lines=new List<string>();
foreach(var doc in snapshot.Documents.Where(d=>d.Kind=="level").OrderBy(d=>(int)d.Data["levelNumber"])) {
var level=JsonPuzzlePlayAdapter.CreateRequest(snapshot,doc.Id,12345).CreateDefinition();
try {
using(var adapter=TutorialBoardAdapter.Prepare(level,StartingBoardBuilder.Build(level,level.Tutorial.seed).State)) {
var executor=adapter.Executor;
for(int i=0;i<30 && adapter.Progress.State!=TutorialProgressState.Completed;i++) {
while(executor.HasPendingCascade)adapter.ObserveCascade(executor.AdvanceCascade());
adapter.Tick(true,false);
var s=adapter.Progress.Snapshot;
if(s.State==TutorialProgressState.Completed)break;
var input=s.State==TutorialProgressState.AwaitDescription?TutorialInput.Next():TutorialInput.Swap(s.First.Value,s.Second.Value);
if(!adapter.TryBegin(input))throw new Exception(adapter.Progress.Message);
if(input.Kind!=TutorialInputKind.Next)adapter.ReportAction(executor.Swap(input.First.Value,input.Second.Value).IsApplied);
while(executor.HasPendingCascade)adapter.ObserveCascade(executor.AdvanceCascade());
adapter.Tick(true,false);
}
lines.Add($"Level {level.LevelNumber}: tutorial={adapter.Progress.State}, phase={executor.Phase}, outcome={executor.Outcome?.Kind}: {executor.Outcome?.Message}, actions={ActionQuery.Find(executor.State).Count}");
}}
finally{UnityEngine.Object.DestroyImmediate(level);}
}
File.WriteAllLines("Logs/PowerTutorialLevels/inspection.txt",lines);EditorApplication.Exit(0);
}catch(Exception e){File.WriteAllText("Logs/PowerTutorialLevels/inspection-error.txt",e.ToString());EditorApplication.Exit(1);}
}
public static void Run() {
const string output="Logs/PowerTutorialLevels"; Directory.CreateDirectory(output);
try {
var store=new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main")); var original=store.Read();
var backup=new ContentSnapshotStore(Path.GetFullPath("ContentData/Backups/power-tutorial-levels-20261016")).Read();
if(backup.Hash!=original.Hash)throw new Exception("Verified backup no longer matches current source");
var docs=original.Snapshot.Documents.ToList(); var template=docs.Single(d=>d.Kind=="level"&&(int)d.Data["levelNumber"]==1);
string[] pairs={"Rocket","Bomb","Drone","Magnet","Rocket,Rocket","Rocket,Bomb","Rocket,Drone","Rocket,Magnet","Bomb,Bomb","Bomb,Drone","Bomb,Magnet","Drone,Drone","Drone,Magnet","Magnet,Magnet"};
string[] names={"로켓","폭탄","드론","자석","로켓 + 로켓","로켓 + 폭탄","로켓 + 드론","로켓 + 자석","폭탄 + 폭탄","폭탄 + 드론","폭탄 + 자석","드론 + 드론","드론 + 자석","자석 + 자석"};
string[] causes={"","","","","RocketRocket","RocketBomb","RocketDrone","MagnetRocket","BombBomb","BombDrone","MagnetBomb","DroneDrone","MagnetDrone","MagnetMagnet"};
var ids=new List<string>();
for(int n=1;n<=15;n++) {
var old=docs.SingleOrDefault(d=>d.Kind=="level"&&(int)d.Data["levelNumber"]==n); string id=old?.Id??"level-"+Guid.NewGuid().ToString("N"); ids.Add(id);
var data=(JObject)template.Data.DeepClone(); data["levelNumber"]=n;
data["missions"]=new JArray(Enumerable.Range(1,4).Select(c=>new JObject{["kind"]="Color",["color"]="Type"+c,["count"]=30}));
if(n>1) { data["displayName"]=$"Level_{n:D2} · {names[n-2]}"; var p=pairs[n-2].Split(',');
foreach(var b in (JArray)data["initialBlocks"])if((int)b["coordinate"]["column"]==4) {
if((int)b["coordinate"]["row"]==3)b["kind"]=p[0];
if((int)b["coordinate"]["row"]==4)b["kind"]=p.Length==2?p[1]:"FixedNormal"; }}
if(old!=null)docs.Remove(old); docs.Add(new ContentDocument("level",id,data)); }
using(var graph=new AuthoringObjectGraph(docs)) {
for(int n=2;n<=15;n++) {
var level=(LevelDefinition)graph.Resolve(ids[n-1]); var t=level.Tutorial; t.completionId=$"power-tutorial-level-{n:D2}-v1";t.steps.Clear();t.previousLevelNumbers.Clear();t.bindings.Clear();
var a=new BoardCoordinate(3,4);var b=new BoardCoordinate(4,4); string power="power."+pairs[n-2].Split(',')[0].ToLowerInvariant();
t.steps.Add(new TutorialStepDefinition{authoringId="intro",instructions=n<=5?names[n-2]+"을 옆 블록과 바꿔 사용해 보세요.":"두 파워블록을 서로 바꾸면 더 강한 효과가 나와요.",highlights=new List<BoardCoordinate>{a,b}});
var c=new TutorialConditionDefinition{authoringId="effect",kind=n<=5?TutorialConditionKind.Activated:TutorialConditionKind.Combined,requiredCount=1,target=new TutorialTargetDefinition{kind=TutorialTargetKind.Board,layer=TutorialTargetLayer.Content},powerDefinitionId=n<=5?power:""};
if(n>5)c.allowedOrigins.Add((EffectOrigin)Enum.Parse(typeof(EffectOrigin),causes[n-2]));
t.steps.Add(new TutorialStepDefinition{authoringId="use",kind=TutorialStepKind.PowerSwap,actionDefinitionId=power,instructions=names[n-2]+" · 빛나는 두 칸을 서로 바꿔 보세요.",hasFirst=true,first=a,hasSecond=true,second=b,automaticHighlights=true,conditions=new List<TutorialConditionDefinition>{c}});
t.steps.Add(new TutorialStepDefinition{authoringId="finish",instructions="잘했어요! 이제 네 가지 색 블록을 각각 30개씩 모아 보세요."});
foreach(var s in t.supply.sources) {s.items.Clear();for(int i=0;i<100;i++)s.items.Add(new ElementSupplyItemDefinition{definitionId="supply.normal.fixed",color=(RabbitColor)((i*2+s.coordinate.Column)%4),count=1,durability=1});}
var issues=LevelTutorialReplayValidator.Validate(level);if(issues.Count>0)throw new Exception("Level "+n+": "+string.Join(" | ",issues));
docs.RemoveAll(d=>d.Id==ids[n-1]);docs.Add(graph.Encode(ids[n-1])); }}
var project=docs.Single(d=>d.Kind=="project");project.Data["contentVersion"]=(int)project.Data["contentVersion"]+1;
project.Data["documents"]=new JArray(docs.Where(d=>d.Kind!="project").Select(d=>new JObject{["id"]=d.Id,["path"]="pending/"+d.Id+".json"}));
var candidate=new ContentSnapshot(docs);
foreach(var id in ids) {var level=JsonPuzzlePlayAdapter.CreateRequest(candidate,id,12345).CreateDefinition();try {var issues=LevelTutorialReplayValidator.Validate(level);if(issues.Count>0)throw new Exception("Roundtrip "+id+": "+string.Join(" | ",issues));}finally {UnityEngine.Object.DestroyImmediate(level);}}
var saved=store.Publish(candidate,original.Hash);
File.WriteAllText(output+"/result.txt","PASS 15 levels, same board geometry, 4 color missions x30\nPASS tutorial logical replays and JSON roundtrip\nPublished hash: "+saved.Hash);EditorApplication.Exit(0);
} catch(Exception e) {File.WriteAllText(output+"/error.txt",e.ToString());UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}
}}}

