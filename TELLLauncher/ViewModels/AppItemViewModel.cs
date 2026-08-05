using System.IO;
using System.Windows.Media;
using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.ViewModels;

public sealed class AppItemViewModel
{
    public AppItemViewModel(AppEntry model)
    {
        Model = model;
        Icon = IconService.LoadIcon(model.IconPath ?? model.TargetPath);
    }

    public AppEntry Model { get; }

    public string Name => Model.Name;

    public string? TargetPath => Model.TargetPath;

    public string Initial => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name[..1].ToUpperInvariant();

    public ImageSource? Icon { get; }

    public bool HasIcon => Icon is not null;

    public bool HasNoIcon => Icon is null;

    public bool IsMissing => string.IsNullOrWhiteSpace(TargetPath) || !File.Exists(TargetPath);

    public bool IsResolved => !IsMissing;
}
