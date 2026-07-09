using System;

namespace Nellie.Models
{
    /// <summary>
    /// Immutable snapshot of tag data read from a file on a background thread,
    /// then applied to a <see cref="Track"/> on the UI thread.
    /// </summary>
    public readonly record struct TrackMetadata(
        string Title,
        string Artist,
        string Album,
        uint TrackNumber,
        uint Year,
        TimeSpan Duration);
}
