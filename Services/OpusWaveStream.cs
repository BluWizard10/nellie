using System;
using System.IO;
using Concentus;
using Concentus.Oggfile;
using NAudio.Wave;

namespace Nellie.Services
{
    /// <summary>
    /// Adapts an Ogg-encapsulated Opus file (.opus) to a NAudio <see cref="WaveStream"/>.
    /// Concentus hands back raw decoded PCM packets, so this class buffers them and
    /// serves them through the usual Read() contract.
    /// <para>
    /// Opus always decodes at 48 kHz. The channel count must be known before the
    /// decoder is created, but <see cref="OpusOggReadStream"/> does not expose the
    /// container's OpusHead — so we read it from the file's tags via TagLib#.
    /// </para>
    /// </summary>
    public sealed class OpusWaveStream : WaveStream
    {
        private const int OpusSampleRate = 48000;
        private const int BitsPerSample = 16;

        private readonly string _filePath;
        private readonly int _channels;
        private readonly WaveFormat _waveFormat;
        private readonly TimeSpan _totalTime;
        private readonly long _length;
        private readonly object _sync = new();

        private Stream _fileStream = null!;
        private OpusOggReadStream _oggStream = null!;

        // Decoded PCM that didn't fit in the previous Read() call.
        private byte[] _residual = Array.Empty<byte>();
        private int _residualOffset;
        private long _position;

        public OpusWaveStream(string filePath)
        {
            _filePath = filePath;
            (int channels, TimeSpan duration) = ReadProperties(filePath);
            _channels = channels;
            _waveFormat = new WaveFormat(OpusSampleRate, BitsPerSample, _channels);

            OpenStream();

            if (duration <= TimeSpan.Zero && _oggStream.CanSeek)
                duration = _oggStream.TotalTime;

            _totalTime = duration;
            _length = (long)(duration.TotalSeconds * _waveFormat.AverageBytesPerSecond);
        }

        public override WaveFormat WaveFormat => _waveFormat;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                lock (_sync)
                {
                    double seconds = Math.Max(0, (double)value / _waveFormat.AverageBytesPerSecond);
                    var target = TimeSpan.FromSeconds(seconds);

                    // Rebuild the stream from scratch. The underlying library's SeekTo
                    // does not re-prime its packet queue (and never clears end-of-stream),
                    // which leaves playback silent after a seek — a fresh reader avoids that.
                    ReopenStream();

                    if (target > TimeSpan.Zero && _oggStream.CanSeek)
                    {
                        if (target > _totalTime && _totalTime > TimeSpan.Zero)
                            target = _totalTime;

                        _oggStream.SeekTo(target);
                        _oggStream.DecodeNextPacket(); // discard the stale pre-seek packet
                        _position = (long)(target.TotalSeconds * _waveFormat.AverageBytesPerSecond);
                    }
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_sync)
            {
                int written = 0;

                while (written < count)
                {
                    // Serve leftovers from the last decoded packet first.
                    if (_residualOffset < _residual.Length)
                    {
                        int available = _residual.Length - _residualOffset;
                        int toCopy = Math.Min(available, count - written);
                        Buffer.BlockCopy(_residual, _residualOffset, buffer, offset + written, toCopy);
                        _residualOffset += toCopy;
                        written += toCopy;
                        continue;
                    }

                    if (!_oggStream.HasNextPacket)
                        break;

                    short[]? decoded = _oggStream.DecodeNextPacket();
                    if (decoded is null || decoded.Length == 0)
                        continue; // header/comment packet or transient — keep going

                    _residual = new byte[decoded.Length * sizeof(short)];
                    Buffer.BlockCopy(decoded, 0, _residual, 0, _residual.Length);
                    _residualOffset = 0;
                }

                _position += written;
                return written;
            }
        }

        private void OpenStream()
        {
            IOpusDecoder decoder = OpusCodecFactory.CreateDecoder(OpusSampleRate, _channels);
            _fileStream = File.OpenRead(_filePath);
            _oggStream = new OpusOggReadStream(decoder, _fileStream);
        }

        private void ReopenStream()
        {
            _oggStream.Close();
            _fileStream.Dispose();

            _residual = Array.Empty<byte>();
            _residualOffset = 0;
            _position = 0;

            OpenStream();
        }

        private static (int channels, TimeSpan duration) ReadProperties(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                int channels = file.Properties?.AudioChannels ?? 2;
                if (channels is not (1 or 2))
                    channels = 2;
                return (channels, file.Properties?.Duration ?? TimeSpan.Zero);
            }
            catch
            {
                return (2, TimeSpan.Zero);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _oggStream.Close();
                _fileStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
