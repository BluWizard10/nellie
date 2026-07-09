using System.IO;
using NAudio.Flac;
using NAudio.Vorbis;
using NAudio.Wave;

namespace Nellie.Services
{
    /// <summary>
    /// Picks the right NAudio reader for a file based on its extension, keeping the
    /// player itself format-agnostic. Every branch returns a <see cref="WaveStream"/>,
    /// which the player converts to a sample provider for volume control and output.
    /// </summary>
    public static class AudioReaderFactory
    {
        public static WaveStream Create(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                // AudioFileReader natively handles these and exposes a sample provider.
                ".wav" or ".aif" or ".aiff" or ".mp3" => new AudioFileReader(filePath),

                // Dedicated managed decoders (no reliance on OS codecs).
                ".ogg" or ".oga" => new VorbisWaveReader(filePath),
                ".flac" => new FlacReader(filePath),
                ".opus" => new OpusWaveStream(filePath),

                // AAC/M4A/MP4/WMA and anything else via Windows Media Foundation.
                _ => new MediaFoundationReader(filePath),
            };
        }
    }
}
