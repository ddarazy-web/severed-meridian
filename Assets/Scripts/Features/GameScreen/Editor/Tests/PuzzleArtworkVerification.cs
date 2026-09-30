using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static class PuzzleArtworkVerification
    {
        public static void Run()
        {
            Directory.CreateDirectory("Logs/WorldBoardVerification");
            try
            {
                Type mapping = Type.GetType("GameScreen.PuzzleArtworkPaths, Assembly-CSharp");
                if (mapping == null) throw new Exception("Missing runtime artwork mapping");
                MethodInfo power = mapping.GetMethod("Power", BindingFlags.Public | BindingFlags.Static);
                foreach (Levels.InitialBlockKind kind in new[] { Levels.InitialBlockKind.Rocket, Levels.InitialBlockKind.Bomb, Levels.InitialBlockKind.Drone, Levels.InitialBlockKind.Magnet })
                    foreach (Levels.RocketDirection direction in new[] { Levels.RocketDirection.Horizontal, Levels.RocketDirection.Vertical })
                    {
                        string path = (string)power.Invoke(null, new object[] { kind, direction });
                        if (!File.Exists("Assets/Textures/" + path + ".png")) throw new Exception("Missing power artwork: " + path);
                    }
                File.WriteAllText("Logs/WorldBoardVerification/mapping.txt", "PASS: power mappings resolve to existing textures");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText("Logs/WorldBoardVerification/mapping.txt", "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
