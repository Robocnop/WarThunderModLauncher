using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WTModLauncher.Core;

namespace WTModLauncher.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly HttpClient _http = Http.Create();
    private readonly LauncherState _state = StateStore.Load();
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        Loc.Instance.Language = _state.Language ?? Loc.DetectNative();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(GameStatusText));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(Language));
        };
    }

    public ObservableCollection<ModItemViewModel> Items { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];
    public ObservableCollection<SightsProfile> SightsProfiles { get; } = [];

    public string AppVersion => "v" + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0");

    // ---------- navigation ----------

    [ObservableProperty]
    private string _currentPage = "mods";

    [RelayCommand]
    private void Navigate(string page) => CurrentPage = page;

    // ---------- language ----------

    public string Language
    {
        get => Loc.Instance.Language;
        set
        {
            Loc.Instance.Language = value;
            _state.Language = value;
            StateStore.Save(_state);
        }
    }

    [RelayCommand]
    private void SetLanguage(string lang) => Language = lang;

    // ---------- game folder ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameFound), nameof(GameStatusText))]
    private string? _gamePath;

    public bool IsGameFound => GameLocator.IsValidGameDir(GamePath);

    public string GameStatusText => IsGameFound ? Loc.Format("Game.Found", GamePath!) : Loc.Get("Game.NotFound");

    [ObservableProperty]
    private SightsProfile? _selectedSightsProfile;

    partial void OnSelectedSightsProfileChanged(SightsProfile? value)
    {
        if (value is null) return;
        _state.SightsProfileId = value.UserId;
        StateStore.Save(_state);
    }

    // ---------- progress ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
    [NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _progressIndeterminate;

    [ObservableProperty]
    private string _progressMessage = "";

    [ObservableProperty]
    private string _progressDetail = "";

    public bool HasPendingWork => Items.Any(i => i.IsSelected && i.NeedsAction);

    public string PrimaryButtonText =>
        IsBusy ? Loc.Get("Action.Working") : HasPendingWork ? Loc.Get("Action.Install") : Loc.Get("Action.UpToDate");

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasPendingWork));
        OnPropertyChanged(nameof(PrimaryButtonText));
        InstallSelectedCommand.NotifyCanExecuteChanged();
    }

    // ---------- startup ----------

    public async Task InitializeAsync()
    {
        Log($"WT Mod Launcher {AppVersion}");
        GamePath = GameLocator.IsValidGameDir(_state.GamePath) ? _state.GamePath : GameLocator.Detect();
        if (IsGameFound)
        {
            _state.GamePath = GamePath;
            StateStore.Save(_state);
        }
        Log(GameStatusText);
        LoadSightsProfiles();
        await LoadCatalogAsync();
    }

    private void LoadSightsProfiles()
    {
        SightsProfiles.Clear();
        foreach (var p in GameLocator.FindSightsProfiles()) SightsProfiles.Add(p);
        SelectedSightsProfile = SightsProfiles.FirstOrDefault(p => p.UserId == _state.SightsProfileId) ?? SightsProfiles.FirstOrDefault();
    }

    private async Task LoadCatalogAsync()
    {
        IsBusy = true;
        ProgressIndeterminate = true;
        try
        {
            var (manifest, source) = await ManifestService.LoadAsync(_http, CancellationToken.None);
            Log(source == "embedded" ? Loc.Get("Msg.ManifestOffline") : Loc.Format("Msg.ManifestLoaded", source));

            Items.Clear();
            foreach (var item in manifest.Items)
            {
                try { Items.Add(new ModItemViewModel(ModInstaller.Create(item), this)); }
                catch (NotSupportedException ex) { Log(ex.Message); }
            }

            var ctx = CreateContext();
            await Task.WhenAll(Items.Select(async vm =>
                vm.AvailableVersion = await vm.Installer.ResolveAvailableVersionAsync(ctx, CancellationToken.None)));
        }
        catch (Exception ex)
        {
            Log(ex.Message);
        }
        finally
        {
            ProgressIndeterminate = false;
            IsBusy = false;
            RefreshStatuses(selectPending: true);
        }
    }

    private void RefreshStatuses(bool selectPending = false)
    {
        var ctx = CreateContext();
        foreach (var vm in Items)
        {
            // Tools live in the launcher's own folder; everything else needs a known game folder.
            vm.Status = IsGameFound || vm.IsTool ? SafeStatus(vm, ctx) : ModStatus.Unknown;
            if (selectPending) vm.IsSelected = vm.NeedsAction;
        }
        NotifySelectionChanged();
    }

    private ModStatus SafeStatus(ModItemViewModel vm, InstallContext ctx)
    {
        try { return vm.Installer.GetStatus(ctx, vm.AvailableVersion); }
        catch (Exception ex)
        {
            Log(ex.Message);
            return ModStatus.Unknown;
        }
    }

    private InstallContext CreateContext() => new(
        GamePath ?? "",
        _state,
        _http,
        new Progress<InstallProgress>(p =>
        {
            ProgressMessage = p.Message;
            ProgressDetail = p.Detail ?? "";
            ProgressIndeterminate = p.Fraction is null;
            if (p.Fraction is { } f) Progress = f;
        }),
        Log);

    // ---------- install / uninstall / repair ----------

    private bool CanInstall() => !IsBusy && HasPendingWork;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallSelected() => RunAsync(async (ctx, ct) =>
    {
        foreach (var vm in Items.Where(i => i.IsSelected && i.NeedsAction).ToList())
        {
            if (!EnsureCanTouchGame(vm)) continue;
            try
            {
                if (vm.Status == ModStatus.NeedsRepair)
                {
                    await vm.Installer.RepairAsync(ctx, vm.AvailableVersion, ct);
                    Log(Loc.Format("Msg.Repaired", vm.Title));
                }
                else
                {
                    await vm.Installer.InstallAsync(ctx, vm.AvailableVersion, ct);
                    Log(Loc.Format("Msg.Installed", vm.Title));
                }
            }
            catch (UnauthorizedAccessException)
            {
                OfferElevation();
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log(Loc.Format("Msg.Failed", vm.Title, ex.Message));
            }
        }
    });

    [RelayCommand]
    private Task Uninstall(ModItemViewModel vm)
    {
        if (MessageBox.Show(Loc.Format("Msg.ConfirmUninstall", vm.Title), "WT Mod Launcher",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return Task.CompletedTask;

        return RunAsync(async (ctx, ct) =>
        {
            if (!EnsureCanTouchGame(vm)) return;
            try
            {
                await vm.Installer.UninstallAsync(ctx, ct);
                Log(Loc.Format("Msg.Uninstalled", vm.Title));
            }
            catch (UnauthorizedAccessException) { OfferElevation(); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log(Loc.Format("Msg.Failed", vm.Title, ex.Message));
            }
        });
    }

    [RelayCommand]
    private Task Repair() => RunAsync(async (ctx, ct) =>
    {
        var broken = Items.Where(i => i.Status == ModStatus.NeedsRepair).ToList();
        if (broken.Count == 0) Log(Loc.Get("Msg.AllGood"));
        foreach (var vm in broken)
        {
            if (!EnsureCanTouchGame(vm)) continue;
            try
            {
                await vm.Installer.RepairAsync(ctx, vm.AvailableVersion, ct);
                Log(Loc.Format("Msg.Repaired", vm.Title));
            }
            catch (UnauthorizedAccessException) { OfferElevation(); return; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log(Loc.Format("Msg.Failed", vm.Title, ex.Message));
            }
        }
    });

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private async Task RunAsync(Func<InstallContext, CancellationToken, Task> work)
    {
        if (IsBusy) return;
        if (!IsGameFound)
        {
            Log(Loc.Get("Game.NotFound"));
            CurrentPage = "settings";
            return;
        }

        IsBusy = true;
        Progress = 0;
        _cts = new CancellationTokenSource();
        try
        {
            await work(CreateContext(), _cts.Token);
            Log(Loc.Get("Msg.Done"));
        }
        catch (OperationCanceledException)
        {
            Log(Loc.Get("Msg.Cancelled"));
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
            ProgressIndeterminate = false;
            ProgressMessage = "";
            ProgressDetail = "";
            Progress = 0;
            RefreshStatuses();
        }
    }

    private bool EnsureCanTouchGame(ModItemViewModel vm)
    {
        if (!vm.Installer.TouchesGameFiles || !GameProcess.IsRunning()) return true;
        Log(Loc.Get("Game.Running"));
        MessageBox.Show(Loc.Get("Game.Running"), "WT Mod Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void OfferElevation()
    {
        if (MessageBox.Show(Loc.Get("Msg.NeedAdmin"), "WT Mod Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (Exception ex) { Log(ex.Message); } // UAC prompt declined
    }

    // ---------- per-item actions ----------

    [RelayCommand]
    private void LaunchTool(ModItemViewModel vm)
    {
        if (vm.Installer is not GitHubToolInstaller tool) return;
        try { tool.Launch(); }
        catch (Exception ex) { Log(Loc.Format("Msg.Failed", vm.Title, ex.Message)); }
    }

    [RelayCommand]
    private void OpenUserSights()
    {
        if (SelectedSightsProfile is null) return;
        Directory.CreateDirectory(SelectedSightsProfile.UserSightsPath);
        OpenInExplorer(SelectedSightsProfile.UserSightsPath);
    }

    [RelayCommand]
    private void OpenPresetFolder(ModItemViewModel vm)
    {
        if (vm.Installer is ControlsPresetInstaller preset && IsGameFound)
            OpenInExplorer(preset.PresetPath(GamePath!), select: true);
    }

    [RelayCommand]
    private void CopyPresetPath(ModItemViewModel vm)
    {
        if (vm.Installer is ControlsPresetInstaller preset && IsGameFound)
            Clipboard.SetText(preset.PresetPath(GamePath!));
    }

    [RelayCommand]
    private void OpenHomepage(ModItemViewModel vm)
    {
        if (vm.HasHomepage) OpenUrl(vm.Item.Homepage!);
    }

    [RelayCommand]
    private void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    [RelayCommand]
    private void PlayGame() => OpenUrl($"steam://rungameid/{GameLocator.SteamAppId}");

    // ---------- settings ----------

    [RelayCommand]
    private void BrowseGame()
    {
        var dlg = new OpenFolderDialog { InitialDirectory = IsGameFound ? GamePath : null };
        if (dlg.ShowDialog() != true) return;
        if (!GameLocator.IsValidGameDir(dlg.FolderName))
        {
            MessageBox.Show(Loc.Get("Settings.InvalidGame"), "WT Mod Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SetGamePath(dlg.FolderName);
    }

    [RelayCommand]
    private void DetectGame()
    {
        var found = GameLocator.Detect();
        if (found is null) Log(Loc.Get("Game.NotFound"));
        else SetGamePath(found);
    }

    private void SetGamePath(string path)
    {
        GamePath = path;
        _state.GamePath = path;
        StateStore.Save(_state);
        Log(GameStatusText);
        RefreshStatuses(selectPending: true);
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(AppPaths.Root);
        OpenInExplorer(AppPaths.Root);
    }

    [RelayCommand]
    private void CopyLog() => Clipboard.SetText(string.Join(Environment.NewLine, LogLines));

    private static void OpenInExplorer(string path, bool select = false)
    {
        var args = select && File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
    }

    // ---------- log ----------

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (Application.Current?.Dispatcher.CheckAccess() == false)
            Application.Current.Dispatcher.Invoke(() => LogLines.Add(line));
        else
            LogLines.Add(line);

        try
        {
            Directory.CreateDirectory(AppPaths.Logs);
            File.AppendAllText(Path.Combine(AppPaths.Logs, "launcher.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch (Exception) { /* logging must never break the app */ }
    }
}
