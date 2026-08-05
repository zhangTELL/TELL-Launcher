using TELLLauncher.Services;

namespace TELLLauncher.ViewModels;

public sealed class SteamGameItemViewModel
{
    public SteamGameItemViewModel(SteamGameInfo model)
    {
        Model = model;
    }

    public SteamGameInfo Model { get; }

    public string Name => Model.Name;

    public string AppId => Model.AppId;

    public string Initial => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name[..1].ToUpperInvariant();
}
