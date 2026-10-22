using System;
using System.IO;
using System.Reflection;
using Levels;
using Lobby;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LobbyRouteVerification
    {
        public static void Run()
        {
            string root = "Logs/LobbyStage02/route-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(root);
            int exit = 1;
            try
            {
                Type routeType = typeof(LobbyScreen).Assembly.GetType("GameScreen.Navigation.LobbyGameRoute");
                Check(routeType != null, "Lobby request routing is missing");
                var index = new LevelLobbyIndex { Entries = new[]
                {
                    new LevelLobbyEntry { LevelId = "one", LevelNumber = 1, DisplayName = "첫 레벨" },
                    new LevelLobbyEntry { LevelId = "two", LevelNumber = 2, DisplayName = "둘째" },
                    new LevelLobbyEntry { LevelId = "fifteen", LevelNumber = 15, DisplayName = "마지막" }
                } };
                var snapshot = (LobbyIndexSnapshot)Activator.CreateInstance(typeof(LobbyIndexSnapshot), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { "generation-a", index }, null);
                object route = Activator.CreateInstance(routeType, snapshot);
                MethodInfo create = routeType.GetMethod("TryCreateRequest");
                foreach (var level in snapshot.Levels)
                {
                    object[] args = { level.Id, level.Number, "generation-a", null };
                    Check((bool)create.Invoke(route, args), "Registered selection rejected");
                    object request = args[3];
                    Check((string)request.GetType().GetProperty("LevelId").GetValue(request) == level.Id &&
                        (int)request.GetType().GetProperty("LevelNumber").GetValue(request) == level.Number &&
                        (string)request.GetType().GetProperty("Generation").GetValue(request) == "generation-a", "Request metadata lost");
                }
                foreach (object[] args in new[] { new object[] { "one", 2, "generation-a", null }, new object[] { "one", 1, "old", null }, new object[] { "missing", 3, "generation-a", null } })
                    Check(!(bool)create.Invoke(route, args) && args[3] == null, "Invalid selection accepted");
                MethodInfo next = routeType.GetMethod("TryGetNext");
                object[] nextArgs = { 2, null };
                Check((bool)next.Invoke(route, nextArgs) && ((LobbyLevel)nextArgs[1]).Number == 15, "Next level assumed consecutive numbering");
                foreach (int number in new[] { 15, 3 })
                {
                    object[] args = { number, null };
                    Check(!(bool)next.Invoke(route, args) && args[1] == null, "Last/missing level produced next entry");
                }
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS ID/number/generation selection and nonconsecutive/last/missing progression");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
