namespace Nellie.Models
{
    /// <summary>
    /// High-level playback state exposed by the audio engine.
    /// Named to avoid a clash with NAudio's own PlaybackState enum.
    /// </summary>
    public enum PlayerState
    {
        Stopped,
        Playing,
        Paused,
    }
}
