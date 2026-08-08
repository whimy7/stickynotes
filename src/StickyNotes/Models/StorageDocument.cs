namespace StickyNotes.Models;

public sealed class StorageDocument
{
    public int SchemaVersion { get; set; } = 3;

    public List<Note> Notes { get; set; } = [];
}
