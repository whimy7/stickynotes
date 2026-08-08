using System.Text.Json.Serialization;

namespace StickyNotes.Models;

public sealed class NoteImage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public int PixelWidth { get; set; }

    public int PixelHeight { get; set; }

    public long FileSize { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public string LocalPath { get; set; } = string.Empty;

    [JsonIgnore]
    public string Dimensions => PixelWidth > 0 && PixelHeight > 0
        ? $"{PixelWidth} x {PixelHeight}"
        : string.Empty;
}

