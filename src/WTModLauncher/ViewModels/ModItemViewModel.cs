using CommunityToolkit.Mvvm.ComponentModel;
using WTModLauncher.Core;

namespace WTModLauncher.ViewModels;

public sealed partial class ModItemViewModel : ObservableObject
{
    public ModItemViewModel(ModInstaller installer, MainViewModel owner)
    {
        Installer = installer;
        Owner = owner;
        Loc.Instance.PropertyChanged += (_, _) => RefreshTexts();
    }

    public ModInstaller Installer { get; }
    public MainViewModel Owner { get; }
    public ModItem Item => Installer.Item;

    public string Title => Loc.Instance.Pick(Item.Name);
    public string Description => Loc.Instance.Pick(Item.Description);
    public string Author => Item.Author is null ? "" : Loc.Format("Card.By", Item.Author);
    public string Icon => Item.Icon ?? "";
    public bool HasHomepage => !string.IsNullOrEmpty(Item.Homepage);

    public bool IsTool => Installer is GitHubToolInstaller;
    public bool IsControls => Installer is ControlsPresetInstaller;
    public string? HowTo => IsTool ? Loc.Get("Fcs.HowTo") : IsControls ? Loc.Get("Preset.HowTo") : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(NeedsAction), nameof(IsPresent), nameof(CanLaunch), nameof(StatusKey))]
    private ModStatus _status = ModStatus.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText))]
    private string _availableVersion = "";

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Has something to do: what the big INSTALL button processes when the item is selected.</summary>
    public bool NeedsAction => Status is ModStatus.NotInstalled or ModStatus.UpdateAvailable or ModStatus.NeedsRepair;
    public bool IsPresent => Status is ModStatus.Installed or ModStatus.UpdateAvailable or ModStatus.NeedsRepair;
    public bool CanLaunch => IsTool && IsPresent;

    /// <summary>Used by the view to colour the status chip.</summary>
    public string StatusKey => Status.ToString();

    public string StatusText => Status switch
    {
        ModStatus.NotInstalled => Loc.Get("Status.NotInstalled"),
        ModStatus.Installed => Loc.Get("Status.Installed"),
        ModStatus.UpdateAvailable => Loc.Get("Status.Update"),
        ModStatus.NeedsRepair => Loc.Get("Status.Repair"),
        _ => Loc.Get("Status.Unknown"),
    };

    public string VersionText => string.IsNullOrEmpty(AvailableVersion) ? "" : Loc.Format("Card.Version", AvailableVersion);

    partial void OnIsSelectedChanged(bool value) => Owner.NotifySelectionChanged();

    private void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Author));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(HowTo));
    }
}
