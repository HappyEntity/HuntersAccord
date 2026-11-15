using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace HuntersAccord.Launcher.Services;

/// <summary>File/folder pickers, abstracted so view models do not depend on a window.</summary>
public interface IStorageService
{
    Task<string?> PickFolderAsync(string title);

    Task<string?> PickFileAsync(string title, string fileTypeName, params string[] patterns);
}

public sealed class AvaloniaStorageService(TopLevel topLevel) : IStorageService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileAsync(string title, string fileTypeName, params string[] patterns)
    {
        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(fileTypeName) { Patterns = patterns }],
        });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }
}
