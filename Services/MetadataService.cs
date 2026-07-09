using System;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Nellie.Models;

namespace Nellie.Services
{
    /// <summary>
    /// Reads tag data and embedded album art via TagLib#. All methods are
    /// exception-safe: unreadable or unsupported files yield empty/null results.
    /// </summary>
    public static class MetadataService
    {
        public static TrackMetadata Read(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                var tag = file.Tag;

                var artist = !string.IsNullOrWhiteSpace(tag.FirstPerformer)
                    ? tag.FirstPerformer
                    : tag.FirstAlbumArtist ?? string.Empty;

                return new TrackMetadata(
                    Title: tag.Title ?? string.Empty,
                    Artist: artist,
                    Album: tag.Album ?? string.Empty,
                    TrackNumber: tag.Track,
                    Year: tag.Year,
                    Duration: file.Properties?.Duration ?? TimeSpan.Zero);
            }
            catch
            {
                return new TrackMetadata(string.Empty, string.Empty, string.Empty, 0, 0, TimeSpan.Zero);
            }
        }

        public static Bitmap? ReadArtwork(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                var picture = file.Tag.Pictures.FirstOrDefault();
                if (picture?.Data?.Data is not { Length: > 0 } bytes)
                    return null;

                using var stream = new MemoryStream(bytes);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
    }
}
