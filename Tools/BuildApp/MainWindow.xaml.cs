using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace MoonRabbit.BuildApp;

public partial class MainWindow : Window
{
    private readonly string preferencesPath;
    private readonly WorkspacePreferences preferences;
    private bool clearingPasswords;
    private string? displayedGuid;
    public BuildWorkspaceViewModel ViewModel { get; }
    public MainWindow() : this(new BuildToolClient(), WorkspacePreferences.DefaultPath, true) { }
    public MainWindow(IBuildToolClient client, string preferencesPath, bool autoLoad)
    {
        InitializeComponent();
        this.preferencesPath = preferencesPath; preferences = WorkspacePreferences.Load(preferencesPath);
        ViewModel = new(client) { Confirm = message => MessageBox.Show(this, message, "빌드 도구", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK };
        DataContext = ViewModel;
        string project = Directory.Exists(preferences.ProjectPath) ? preferences.ProjectPath : WorkspacePreferences.DetectProject(AppContext.BaseDirectory);
        if (project.Length == 0) project = WorkspacePreferences.DetectProject(Environment.CurrentDirectory);
        ViewModel.ChangeProject(project);
        if (preferences.UnityPath.Length > 0 && project == preferences.ProjectPath) ViewModel.UnityPath = preferences.UnityPath;
        RestorePlacement();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.Selected) && displayedGuid != ViewModel.Selected?.Guid)
            { displayedGuid = ViewModel.Selected?.Guid; ClearPasswords(); }
        };
        Loaded += async (_, _) => { if (autoLoad && ViewModel.ProjectPath.Length > 0) await ViewModel.ReloadAsync(preferences.ProfileGuid); };
        Closing += ClosingWindow;
    }
    private void RestorePlacement()
    {
        Rect area = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, area.Width); MinHeight = Math.Min(MinHeight, area.Height);
        Width = Math.Clamp(double.IsFinite(preferences.Width) ? preferences.Width : 1240, MinWidth, area.Width);
        Height = Math.Clamp(double.IsFinite(preferences.Height) ? preferences.Height : 840, MinHeight, area.Height);
        Left = Math.Clamp(double.IsFinite(preferences.Left) ? preferences.Left : area.Left, area.Left, area.Right - Width);
        Top = Math.Clamp(double.IsFinite(preferences.Top) ? preferences.Top : area.Top, area.Top, area.Bottom - Height);
        if (preferences.Maximized) WindowState = WindowState.Maximized;
    }
    private void ClosingWindow(object? sender, CancelEventArgs args)
    {
        if (ViewModel.Busy) { MessageBox.Show(this, "진행 중인 작업은 강제 종료하지 않습니다. 필요하면 취소를 요청하고 작업이 끝난 뒤 닫으세요.", "작업 진행 중"); args.Cancel = true; return; }
        if ((ViewModel.Dirty || ViewModel.SigningDirty) && !ViewModel.Confirm("저장하지 않은 변경을 버리고 창을 닫을까요?")) { args.Cancel = true; return; }
        try
        {
            Rect rectangle = WindowState == WindowState.Normal ? new(Left, Top, Width, Height) : RestoreBounds;
            preferences.Left = rectangle.Left; preferences.Top = rectangle.Top; preferences.Width = rectangle.Width; preferences.Height = rectangle.Height; preferences.Maximized = WindowState == WindowState.Maximized;
            preferences.ProjectPath = ViewModel.ProjectPath; preferences.UnityPath = ViewModel.UnityPath; preferences.ProfileGuid = ViewModel.Selected?.Guid ?? "";
            preferences.Save(preferencesPath); ClearPasswords();
        }
        catch (Exception error) { ViewModel.Error(error); args.Cancel = true; }
    }
    private async void ProjectClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Unity 프로젝트 폴더 선택", InitialDirectory = Directory.Exists(ViewModel.ProjectPath) ? ViewModel.ProjectPath : "" };
        if (dialog.ShowDialog(this) == true && ViewModel.ChangeProject(dialog.FolderName)) await ViewModel.ReloadAsync();
    }
    private void UnityClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Unity.exe 선택", Filter = "Unity 실행 파일|Unity.exe" };
        if (dialog.ShowDialog(this) == true) ViewModel.UnityPath = dialog.FileName;
    }
    private void KeystoreClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Keystore 선택", Filter = "키 저장소|*.keystore;*.jks|모든 파일|*.*" };
        if (dialog.ShowDialog(this) == true) ViewModel.KeystorePath = dialog.FileName;
    }
    private void PasswordChanged(object sender, RoutedEventArgs e) { if (!clearingPasswords) ViewModel?.MarkSigningInputChanged(); }
    private async void AliasesClick(object sender, RoutedEventArgs e) => await ViewModel.SigningAsync(false, StorePassword.Password, KeyPassword.Password);
    private async void SigningSaveClick(object sender, RoutedEventArgs e)
    { if (await ViewModel.SigningAsync(true, StorePassword.Password, KeyPassword.Password)) ClearPasswords(); }
    private void ClearPasswords() { clearingPasswords = true; StorePassword.Clear(); KeyPassword.Clear(); clearingPasswords = false; }
    private void HelpClick(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(AppContext.BaseDirectory, "README.md"), false);
    private void LogClick(object sender, RoutedEventArgs e) => OpenPath(ViewModel.LogPath.Length == 0 ? Path.Combine(ViewModel.ProjectPath, "Logs") : ViewModel.LogPath, true);
    private void OutputClick(object sender, RoutedEventArgs e) => OpenPath(ViewModel.OutputPath, true);
    private void OpenPath(string path, bool folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) throw new IOException("아직 경로가 없습니다.");
            path = Path.GetFullPath(path, Path.GetFullPath(ViewModel.ProjectPath.Length > 0 ? ViewModel.ProjectPath : Environment.CurrentDirectory));
            if (!File.Exists(path) && !Directory.Exists(path)) throw new IOException("아직 생성되지 않은 경로입니다: " + path);
            if (folder && File.Exists(path)) path = Path.GetDirectoryName(path)!;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception error) { ViewModel.Error(error); }
    }
}
