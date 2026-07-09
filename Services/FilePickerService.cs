using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Nellie.Services
{
    /// <summary>
    /// Bridges the view model to Avalonia's storage picker without the view model
    /// needing a reference to any view. Also expands a chosen folder to the audio
    /// files it contains.
    /// </summary>
    public sealed class FilePickerService
    {
        public static readonly string[] SupportedExtensions =
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma", ".mp4", ".ogg", ".oga", ".opus", ".aif", ".aiff",
        };

        private readonly Window _window;

        public FilePickerService(Window window) => _window = window;

        public async Task<IReadOnlyList<string>> PickFilesAsync()
        {
            var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add audio files",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Audio files")
                    {
                        Patterns = SupportedExtensions.Select(e => "*" + e).ToArray(),
                    },
                    FilePickerFileTypes.All,
                },
            });

            return files
                .Select(f => f.TryGetLocalPath())
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> PickFolderAsync()
        {
            var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Add folder",
                AllowMultiple = false,
            });

            if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } path)
                return new List<string>();

            return EnumerateAudioFiles(path);
        }

        private static List<string> EnumerateAudioFiles(string directory) =>
            Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
