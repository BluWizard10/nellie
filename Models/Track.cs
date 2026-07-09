using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nellie.Models
{
    /// <summary>
    /// A single item in a playlist. Created immediately from a file path (so the
    /// row appears at once) and enriched with tag data asynchronously.
    /// </summary>
    public partial class Track : ObservableObject
    {
        public Track(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileNameWithoutExtension(filePath);
        }

        public string FilePath { get; }

        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private string _artist = string.Empty;
        [ObservableProperty] private string _album = string.Empty;
        [ObservableProperty] private uint _trackNumber;
        [ObservableProperty] private uint _year;
        [ObservableProperty] private TimeSpan _duration;

        /// <summary>True while this track is the one loaded in the player.</summary>
        [ObservableProperty] private bool _isCurrent;

        public string DurationText => FormatDuration(Duration);

        partial void OnDurationChanged(TimeSpan value) => OnPropertyChanged(nameof(DurationText));

        /// <summary>Applies tag data, keeping the filename title only if the tag has none.</summary>
        public void ApplyMetadata(TrackMetadata meta)
        {
            if (!string.IsNullOrWhiteSpace(meta.Title))
                Title = meta.Title;
            Artist = meta.Artist;
            Album = meta.Album;
            TrackNumber = meta.TrackNumber;
            Year = meta.Year;
            if (meta.Duration > TimeSpan.Zero)
                Duration = meta.Duration;
        }

        private static string FormatDuration(TimeSpan t) =>
            t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
}
