using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Nellie.Models;

namespace Nellie.Services
{
    /// <summary>
    /// Wraps NAudio into a small, format-agnostic playback engine: load a file,
    /// play/pause/stop, seek, and set volume. Raises <see cref="TrackEnded"/> when
    /// a track finishes on its own so the view model can advance the playlist.
    /// </summary>
    public sealed class AudioPlayerService : IDisposable
    {
        private IWavePlayer? _output;
        private WaveStream? _reader;
        private VolumeSampleProvider? _volumeProvider;
        private float _volume = 0.5f;

        // Set true when *we* stop (explicit Stop / switching tracks) so the
        // resulting PlaybackStopped event is not mistaken for a natural end.
        private bool _stopRequested;

        public PlayerState State { get; private set; } = PlayerState.Stopped;

        /// <summary>Raised when the current track reaches its natural end.</summary>
        public event EventHandler? TrackEnded;

        /// <summary>Raised whenever <see cref="State"/> changes.</summary>
        public event EventHandler? StateChanged;

        public bool HasTrack => _reader is not null;

        public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

        public TimeSpan Position
        {
            get => _reader?.CurrentTime ?? TimeSpan.Zero;
            set
            {
                if (_reader is null)
                    return;
                var clamped = value < TimeSpan.Zero ? TimeSpan.Zero
                            : value > _reader.TotalTime ? _reader.TotalTime
                            : value;
                _reader.CurrentTime = clamped;
            }
        }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                if (_volumeProvider is not null)
                    _volumeProvider.Volume = _volume;
            }
        }

        /// <summary>Loads a file and prepares it for playback (does not start playing).</summary>
        public void Load(string filePath)
        {
            DisposePlayback();

            _reader = AudioReaderFactory.Create(filePath);
            _volumeProvider = new VolumeSampleProvider(_reader.ToSampleProvider()) { Volume = _volume };
            _output = new WaveOutEvent();
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_volumeProvider);

            _stopRequested = false;
            SetState(PlayerState.Stopped);
        }

        public void Play()
        {
            if (_output is null || _reader is null)
                return;
            if (_reader.CurrentTime >= _reader.TotalTime)
                _reader.CurrentTime = TimeSpan.Zero;
            _output.Play();
            SetState(PlayerState.Playing);
        }

        public void Pause()
        {
            if (_output is null)
                return;
            _output.Pause();
            SetState(PlayerState.Paused);
        }

        public void Stop()
        {
            if (_output is null)
                return;
            _stopRequested = true;
            _output.Stop();
            if (_reader is not null)
                _reader.CurrentTime = TimeSpan.Zero;
            SetState(PlayerState.Stopped);
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_stopRequested)
            {
                // Consume the event that our own Stop() triggered.
                _stopRequested = false;
                return;
            }

            SetState(PlayerState.Stopped);

            // Only advance on a clean end-of-stream, not on a decode error.
            if (e.Exception is null)
                TrackEnded?.Invoke(this, EventArgs.Empty);
        }

        private void SetState(PlayerState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DisposePlayback()
        {
            if (_output is not null)
            {
                // Unsubscribe first so Dispose()'s internal stop never reaches us.
                _output.PlaybackStopped -= OnPlaybackStopped;
                _output.Dispose();
                _output = null;
            }

            _reader?.Dispose();
            _reader = null;
            _volumeProvider = null;
        }

        public void Dispose() => DisposePlayback();
    }
}
