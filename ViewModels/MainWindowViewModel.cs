using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nellie.Models;
using Nellie.Services;

namespace Nellie.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private const string DefaultPattern = "{artist} - {song} [{label}]";
        private const string FileNameField = "File name";
        private const string DurationField = "Duration";

        private readonly AudioPlayerService _player = new();
        private readonly FilePickerService? _filePicker;
        private readonly DispatcherTimer _timer;
        private readonly Random _random = new();

        private FilenamePattern _pattern = new(DefaultPattern);

        // Guards the seek slider: when the timer pushes the current position into
        // PositionSeconds we must not treat it as a user-initiated seek.
        private bool _updatingPositionFromTimer;

        public MainWindowViewModel() : this(null) { }

        public MainWindowViewModel(FilePickerService? filePicker)
        {
            _filePicker = filePicker;
            _player.Volume = (float)Volume;
            _player.TrackEnded += OnTrackEnded;
            _player.StateChanged += OnPlayerStateChanged;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += OnTimerTick;
            _timer.Start();

            RebuildPattern();
        }

        public SortableObservableCollection<Track> Playlist { get; } = new();

        [ObservableProperty] private Track? _currentTrack;
        [ObservableProperty] private Track? _selectedTrack;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private double _positionSeconds;
        [ObservableProperty] private double _durationSeconds;
        [ObservableProperty] private double _volume = 0.5;
        [ObservableProperty] private bool _shuffle;
        [ObservableProperty] private RepeatMode _repeatMode = RepeatMode.Off;
        [ObservableProperty] private Bitmap? _currentArtwork;

        // --- Filename-pattern sorting ---
        [ObservableProperty] private string _patternText = DefaultPattern;
        [ObservableProperty] private string? _selectedSortField;
        [ObservableProperty] private bool _sortDescending;
        [ObservableProperty] private bool _isSettingsOpen;

        /// <summary>Playlist column tokens, derived from the current pattern.</summary>
        public ObservableCollection<string> Columns { get; } = new();

        /// <summary>Raised after <see cref="Columns"/> is rebuilt so the view can regenerate DataGrid columns.</summary>
        public event Action? ColumnsChanged;

        public string PositionText => FormatTime(PositionSeconds);
        public string DurationText => FormatTime(DurationSeconds);
        public bool HasCurrentTrack => CurrentTrack is not null;
        public bool IsRepeatActive => RepeatMode != RepeatMode.Off;
        public bool IsRepeatOne => RepeatMode == RepeatMode.One;
        public bool IsPatternValid => _pattern.IsValid;
        public string PatternPreview => BuildPatternPreview();

        // --- Property change hooks -------------------------------------------------

        partial void OnPositionSecondsChanged(double value)
        {
            OnPropertyChanged(nameof(PositionText));
            if (!_updatingPositionFromTimer && _player.HasTrack)
                _player.Position = TimeSpan.FromSeconds(value);
        }

        partial void OnDurationSecondsChanged(double value) => OnPropertyChanged(nameof(DurationText));

        partial void OnVolumeChanged(double value) => _player.Volume = (float)value;

        partial void OnRepeatModeChanged(RepeatMode value)
        {
            OnPropertyChanged(nameof(IsRepeatActive));
            OnPropertyChanged(nameof(IsRepeatOne));
        }

        partial void OnCurrentTrackChanged(Track? oldValue, Track? newValue)
        {
            if (oldValue is not null)
                oldValue.IsCurrent = false;
            if (newValue is not null)
                newValue.IsCurrent = true;
            OnPropertyChanged(nameof(HasCurrentTrack));
        }

        partial void OnSelectedTrackChanged(Track? value) => OnPropertyChanged(nameof(PatternPreview));

        partial void OnPatternTextChanged(string value) => RebuildPattern();

        partial void OnSelectedSortFieldChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                ApplySort();
        }

        partial void OnSortDescendingChanged(bool value) => ApplySort();

        // --- Transport commands ----------------------------------------------------

        [RelayCommand]
        private void PlayPause()
        {
            if (_player.State == PlayerState.Playing)
            {
                _player.Pause();
                return;
            }

            if (!_player.HasTrack)
            {
                var track = SelectedTrack ?? CurrentTrack ?? Playlist.FirstOrDefault();
                if (track is not null)
                    LoadAndPlay(track);
                return;
            }

            _player.Play();
        }

        [RelayCommand]
        private void PlayTrack(Track? track)
        {
            track ??= SelectedTrack;
            if (track is not null)
                LoadAndPlay(track);
        }

        [RelayCommand]
        private void Stop()
        {
            _player.Stop();
            PositionSeconds = 0;
        }

        [RelayCommand]
        private void Next() => PlayAdjacent(forward: true);

        [RelayCommand]
        private void Previous()
        {
            // Restart the current track if we're more than 3s in, otherwise step back.
            if (_player.HasTrack && _player.Position.TotalSeconds > 3)
            {
                _player.Position = TimeSpan.Zero;
                PositionSeconds = 0;
                return;
            }

            PlayAdjacent(forward: false);
        }

        [RelayCommand]
        private void ToggleShuffle() => Shuffle = !Shuffle;

        [RelayCommand]
        private void CycleRepeat() => RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off,
        };

        // --- Playlist commands -----------------------------------------------------

        [RelayCommand]
        private async Task AddFilesAsync()
        {
            if (_filePicker is null)
                return;
            await AddTracksAsync(await _filePicker.PickFilesAsync());
        }

        [RelayCommand]
        private async Task AddFolderAsync()
        {
            if (_filePicker is null)
                return;
            await AddTracksAsync(await _filePicker.PickFolderAsync());
        }

        [RelayCommand]
        private void RemoveTrack(Track? track)
        {
            track ??= SelectedTrack;
            if (track is null)
                return;
            if (track == CurrentTrack)
            {
                _player.Stop();
                CurrentTrack = null;
                CurrentArtwork = null;
                PositionSeconds = 0;
                DurationSeconds = 0;
            }

            Playlist.Remove(track);
        }

        [RelayCommand]
        private void ClearPlaylist()
        {
            _player.Stop();
            Playlist.Clear();
            CurrentTrack = null;
            CurrentArtwork = null;
            PositionSeconds = 0;
            DurationSeconds = 0;
        }

        // --- Settings & sorting commands -------------------------------------------

        [RelayCommand]
        private void OpenSettings() => IsSettingsOpen = true;

        [RelayCommand]
        private void CloseSettings() => IsSettingsOpen = false;

        /// <summary>
        /// Sorts by a column token (clicking a header). Re-clicking the active
        /// column flips the direction; a different column starts ascending.
        /// </summary>
        public void SortByColumn(string token)
        {
            if (SelectedSortField == token)
            {
                SortDescending = !SortDescending; // hook applies the sort
            }
            else
            {
                SortDescending = false;
                SelectedSortField = token;        // hook applies the sort
            }
        }

        // --- Internals -------------------------------------------------------------

        private void LoadAndPlay(Track track)
        {
            try
            {
                _player.Load(track.FilePath);
                CurrentTrack = track;
                DurationSeconds = _player.Duration.TotalSeconds;
                PositionSeconds = 0;
                _player.Play();
                LoadArtworkAsync(track);
            }
            catch (Exception)
            {
                // Unsupported/corrupt file — leave state untouched so the UI stays sane.
            }
        }

        private void PlayAdjacent(bool forward)
        {
            if (Playlist.Count == 0)
                return;

            if (Shuffle)
            {
                PlayRandom();
                return;
            }

            int index = CurrentTrack is null ? -1 : Playlist.IndexOf(CurrentTrack);
            int next = index + (forward ? 1 : -1);

            if (next < 0)
                next = RepeatMode == RepeatMode.All ? Playlist.Count - 1 : 0;
            else if (next >= Playlist.Count)
            {
                if (RepeatMode != RepeatMode.All)
                    return;
                next = 0;
            }

            LoadAndPlay(Playlist[next]);
        }

        private void PlayRandom()
        {
            if (Playlist.Count == 0)
                return;
            if (Playlist.Count == 1)
            {
                LoadAndPlay(Playlist[0]);
                return;
            }

            int current = CurrentTrack is null ? -1 : Playlist.IndexOf(CurrentTrack);
            int index;
            do
            { index = _random.Next(Playlist.Count); } while (index == current);
            LoadAndPlay(Playlist[index]);
        }

        private async Task AddTracksAsync(IReadOnlyList<string> files)
        {
            if (files.Count == 0)
                return;

            var added = new List<Track>(files.Count);
            foreach (var path in files)
            {
                var track = new Track(path);
                ParseInto(track);
                Playlist.Add(track);
                added.Add(track);
            }

            OnPropertyChanged(nameof(PatternPreview));

            // Read tags off the UI thread; apply each result back on it.
            await Task.Run(() =>
            {
                foreach (var track in added)
                {
                    var meta = MetadataService.Read(track.FilePath);
                    Dispatcher.UIThread.Post(() => track.ApplyMetadata(meta));
                }
            });
        }

        // --- Filename-pattern parsing & sorting ------------------------------------

        private void RebuildPattern()
        {
            _pattern = new FilenamePattern(PatternText ?? string.Empty);

            // Columns mirror the pattern tokens.
            Columns.Clear();
            foreach (string token in _pattern.Tokens)
                Columns.Add(token);

            // Re-parse every existing track under the new pattern.
            foreach (var track in Playlist)
                ParseInto(track);

            // Drop the active sort if its token no longer exists.
            if (SelectedSortField is not null
                && SelectedSortField != DurationField
                && !_pattern.Tokens.Contains(SelectedSortField, StringComparer.OrdinalIgnoreCase))
            {
                SelectedSortField = null;
            }

            OnPropertyChanged(nameof(IsPatternValid));
            OnPropertyChanged(nameof(PatternPreview));
            ColumnsChanged?.Invoke();
        }

        /// <summary>
        /// Parses a track's filename with the active pattern. When it doesn't match,
        /// the raw filename is placed in the first column and the track is flagged
        /// unmatched so it sinks to the bottom when sorting.
        /// </summary>
        private void ParseInto(Track track)
        {
            string name = Path.GetFileNameWithoutExtension(track.FilePath);
            var fields = _pattern.Parse(name);

            if (fields.Count > 0)
            {
                track.IsMatched = true;
                track.FilenameFields = fields;
            }
            else
            {
                track.IsMatched = false;
                track.FilenameFields = _pattern.Tokens.Count > 0
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [_pattern.Tokens[0]] = name }
                    : new Dictionary<string, string>();
            }
        }

        private void ApplySort()
        {
            if (string.IsNullOrEmpty(SelectedSortField) || Playlist.Count < 2)
                return;

            string field = SelectedSortField;
            bool descending = SortDescending;

            // In-place sort raising a Reset so the DataGrid re-renders the new order.
            var selected = SelectedTrack;
            Playlist.Sort((a, b) => CompareTracks(a, b, field, descending));
            SelectedTrack = selected; // restore the highlighted row after the reset
        }

        private int CompareTracks(Track a, Track b, string field, bool descending)
        {
            if (field == DurationField)
            {
                int durationCmp = a.Duration.CompareTo(b.Duration);
                return descending ? -durationCmp : durationCmp;
            }

            // Tracks that don't match the pattern always sink to the bottom.
            if (!a.IsMatched && !b.IsMatched)
                return NaturalComparer.CompareNatural(FileNameOf(a), FileNameOf(b));
            if (!a.IsMatched)
                return 1;
            if (!b.IsMatched)
                return -1;

            string keyA = SortKey(a, field);
            string keyB = SortKey(b, field);
            int cmp = NaturalComparer.CompareNatural(keyA, keyB);
            if (cmp == 0)
                cmp = NaturalComparer.CompareNatural(FileNameOf(a), FileNameOf(b));
            return descending ? -cmp : cmp;
        }

        private static string SortKey(Track track, string field)
        {
            if (field == FileNameField)
                return FileNameOf(track);
            return track.FilenameFields.TryGetValue(field, out var value) ? value : string.Empty;
        }

        private static string FileNameOf(Track track) =>
            Path.GetFileNameWithoutExtension(track.FilePath);

        private string BuildPatternPreview()
        {
            if (!_pattern.IsValid)
                return "Invalid pattern";

            var track = SelectedTrack ?? Playlist.FirstOrDefault();
            if (track is null)
                return "Add tracks to preview parsing";

            var fields = _pattern.Parse(FileNameOf(track));
            if (fields.Count == 0)
                return "This file doesn't match the pattern";

            return string.Join("    ", _pattern.Tokens
                .Where(fields.ContainsKey)
                .Select(token => $"{token} = {fields[token]}"));
        }

        private void LoadArtworkAsync(Track track)
        {
            _ = Task.Run(() =>
            {
                var art = MetadataService.ReadArtwork(track.FilePath);
                Dispatcher.UIThread.Post(() =>
                {
                    // Ignore late results if the user has already moved on.
                    if (ReferenceEquals(CurrentTrack, track))
                        CurrentArtwork = art;
                });
            });
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!_player.HasTrack)
                return;
            _updatingPositionFromTimer = true;
            PositionSeconds = _player.Position.TotalSeconds;
            _updatingPositionFromTimer = false;
        }

        private void OnPlayerStateChanged(object? sender, EventArgs e) =>
            Dispatcher.UIThread.Post(() => IsPlaying = _player.State == PlayerState.Playing);

        private void OnTrackEnded(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
        {
            if (RepeatMode == RepeatMode.One && CurrentTrack is not null)
            {
                LoadAndPlay(CurrentTrack);
                return;
            }

            if (Shuffle)
            {
                PlayRandom();
                return;
            }

            int index = CurrentTrack is null ? -1 : Playlist.IndexOf(CurrentTrack);
            int next = index + 1;

            if (next >= Playlist.Count)
            {
                if (RepeatMode == RepeatMode.All && Playlist.Count > 0)
                {
                    next = 0;
                }
                else
                {
                    _player.Stop();
                    PositionSeconds = 0;
                    return;
                }
            }

            LoadAndPlay(Playlist[next]);
        });

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0)
                seconds = 0;
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _player.TrackEnded -= OnTrackEnded;
            _player.StateChanged -= OnPlayerStateChanged;
            _player.Dispose();
        }
    }
}
