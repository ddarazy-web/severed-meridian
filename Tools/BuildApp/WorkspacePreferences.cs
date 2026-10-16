using System.IO;
using System.Text.Json;

namespace MoonRabbit.BuildApp;

public sealed class WorkspacePreferences
{
    public string ProjectPath { get; set; } = "";
    public string UnityPath { get; set; } = "";
    public string ProfileGuid { get; set; } = "";
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 1240;
    public double Height { get; set; } = 840;
    public bool Maximized { get; set; }
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoonRabbit", "BuildApp", "workspace.json");
    public static WorkspacePreferences Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(path)) ?? new() : new(); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, true);
    }
    public static string DetectProject(string start)
    {
        for (DirectoryInfo? directory = new(start); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ProjectSettings", "ProjectVersion.txt"))) return directory.FullName;
        return "";
    }
    public static string DetectUnity(string project)
    {
        string path = Path.Combine(project, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(path)) return "";
        string? version = File.ReadLines(path).FirstOrDefault(line => line.StartsWith("m_EditorVersion: "))?.Split(':', 2)[1].Trim();
        return version == null ? "" : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", version, "Editor", "Unity.exe");
    }
}
