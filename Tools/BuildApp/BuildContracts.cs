using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows.Input;

namespace MoonRabbit.BuildApp;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Changed(name); return true; }
}

public sealed class ActionCommand(Action action, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) { if (CanExecute(parameter)) action(); }
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class OptionItem : Observable
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public string On { get; init; } = "ON";
    public string Off { get; init; } = "OFF";
    private bool enabled;
    public bool Enabled { get => enabled; set { if (Set(ref enabled, value)) Changed(nameof(StateLabel)); } }
    public string StateLabel => Enabled ? On : Off;
}

public sealed record ProfileItem(string Guid, string Name, string Target, JsonObject Data);
public sealed record BuildProgress(string Status, string? LogPath = null, string? Output = null, string? LogText = null);
public sealed record SecretInput(string StorePassword, string KeyPassword);
public interface IBuildToolClient
{
    Task<JsonObject> SendAsync(string project, string unity, JsonObject request, IProgress<BuildProgress>? progress = null, SecretInput? secrets = null);
    void RequestCancel();
}
public sealed class BuildToolException(string code, string message) : Exception(message) { public string Code { get; } = code; }

public static class JsonFields
{
    public static string Text(this JsonNode? node, string name, string fallback = "") => node?[name]?.GetValue<string>() ?? fallback;
    public static bool Flag(this JsonNode? node, string name) => node?[name]?.GetValue<bool>() ?? false;
}
