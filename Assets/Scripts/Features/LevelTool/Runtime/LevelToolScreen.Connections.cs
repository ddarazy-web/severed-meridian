#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private string connectionGenerator, connectionTarget;
        private int selectedConnection;
        private bool wireDrawing;
        private readonly List<int> wireRoute = new List<int>();
        private void DrawConnections(JObject level)
        {
            JObject[] bodies = level["elements"].OfType<JObject>().Where(item => (string)item["layer"] == "Obstacle").ToArray();
            JObject[] generators = bodies.Where(item => (string)definitions[(string)item["definitionId"]]["reaction"] == "GeneratorCharge").ToArray();
            JObject[] targets = bodies.Where(item =>
            {
                JObject definition = definitions[(string)item["definitionId"]];
                return (string)definition["reaction"] == "Durability" && !((bool?)definition["removal"]?["enabled"] == true && (string)definition["removal"]["kind"] == "Scrap");
            }).ToArray();
            string Name(JObject item) => CellName(Cell(item["coordinate"])) + " · " + definitions[(string)item["definitionId"]]["displayName"];
            if (!generators.Any(item => (string)item["instanceId"] == connectionGenerator)) connectionGenerator = (string)generators.FirstOrDefault()?["instanceId"];
            if (!targets.Any(item => (string)item["instanceId"] == connectionTarget)) connectionTarget = (string)targets.FirstOrDefault()?["instanceId"];
            inspector.Add(new Label("발전기와 내구도 장애물을 선택해 연결합니다. 발전기 하나에 최대 3개 대상입니다."));
            Choice(inspector, "connection-generator", "발전기", generators.Select(item => (string)item["instanceId"]).ToArray(), generators.Select(Name).ToArray(), connectionGenerator,
                value => { connectionGenerator = value; Refresh(); });
            Choice(inspector, "connection-target", "대상 장애물", targets.Select(item => (string)item["instanceId"]).ToArray(), targets.Select(Name).ToArray(), connectionTarget,
                value => connectionTarget = value);
            Button(inspector, "connection-add", "선택한 발전기와 대상 연결", () => Change("발전기 연결", data =>
            { ConnectionDocumentEditing.Add(data, definitions, connectionGenerator, connectionTarget); selectedConnection = data["connections"].Count() - 1; }));
            JObject generator = generators.FirstOrDefault(item => (string)item["instanceId"] == connectionGenerator);
            if (generator != null)
            {
                Button(inspector, "generator-select", "발전기 위치 선택", () => SelectFlowCells(Cell(generator["coordinate"])));
                Number(inspector, "generator-charge", "필요 충전량", (int)generator["requiredCharge"], value => Change("발전기 충전량", data =>
                    LevelDocumentEditing.Place(data, definitions, (string)generator["definitionId"], (int)generator["coordinate"]["row"], (int)generator["coordinate"]["column"], true,
                        (int)generator["durability"], value, (string)generator["color"], (string)generator["rocketDirection"])));
            }
            JArray connections = (JArray)level["connections"];
            if (connections.Count == 0) { inspector.Add(new Label("연결을 만든 뒤 전선을 그릴 수 있습니다.")); return; }
            selectedConnection = Math.Max(0, Math.Min(selectedConnection, connections.Count - 1));
            for (int i = 0; i < connections.Count; i++)
            {
                int index = i;
                JObject start = bodies.FirstOrDefault(item => (string)item["instanceId"] == (string)connections[index]["generatorId"]);
                JObject end = bodies.FirstOrDefault(item => (string)item["instanceId"] == (string)connections[index]["targetId"]);
                Button button = Button(inspector, "connection-" + i, (start == null ? "없는 발전기" : Name(start)) + " → " + (end == null ? "없는 대상" : Name(end)), () =>
                {
                    selectedConnection = index; wireDrawing = false; wireRoute.Clear();
                    Session.SelectCells(new[] { start, end }.Where(item => item != null).Select(item => Cell(item["coordinate"]))); Refresh();
                });
                button.EnableInClassList("selected", selectedConnection == i);
            }
            Button(inspector, "connection-remove", "선택한 연결 삭제", () => Change("연결 삭제", data =>
            { ConnectionDocumentEditing.Remove(data, selectedConnection); wireDrawing = false; wireRoute.Clear(); }));
            Button(inspector, "wire-start", "선택 연결의 전선 그리기 / 이어 그리기", () =>
            {
                CancelFlowTool(); brush = null; wireDrawing = true; wireRoute.Clear();
                wireRoute.AddRange(connections[selectedConnection]["vertices"].Select(v => (int)v["row"] * 10 + (int)v["column"])); Refresh();
            });
            if (!wireDrawing) return;
            inspector.Add(new Label("칸 사이 점을 순서대로 찍으세요. 발전기 테두리에서 시작해 대상 테두리에서 끝냅니다."));
            inspector.Add(new Label("찍은 꼭짓점 " + wireRoute.Count + "개"));
            Button(inspector, "wire-save-draft", "현재까지 경로를 초안으로 저장", () => Change("전선 중간 저장", data => ConnectionDocumentEditing.SetWire(data, definitions, selectedConnection, wireRoute, true)));
            Button(inspector, "wire-finish", "대상까지 연결 완료", () => Change("전선 완료", data =>
            { ConnectionDocumentEditing.SetWire(data, definitions, selectedConnection, wireRoute); wireDrawing = false; }));
            Button(inspector, "wire-clear-input", "찍은 점 비우고 다시 그리기", () => { wireRoute.Clear(); Refresh(); });
            Button(inspector, "wire-cancel", "그리기 취소 (저장한 경로 유지)", () => { wireDrawing = false; wireRoute.Clear(); Refresh(); });
        }
    }
}
#endif
