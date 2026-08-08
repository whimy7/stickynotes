using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StickyNotes.Models;

[JsonConverter(typeof(JsonStringEnumConverter<NoteBlockType>))]
public enum NoteBlockType
{
    Text,
    Image
}

public sealed class NoteBlock : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private double _imageWidth = 280;
    private NoteImage? _resolvedImage;

    public Guid Id { get; set; } = Guid.NewGuid();

    public NoteBlockType Type { get; set; }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value ?? string.Empty);
    }

    public Guid? ImageId { get; set; }

    public double ImageWidth
    {
        get => _imageWidth;
        set => SetField(ref _imageWidth, Math.Clamp(value, 200, 760));
    }

    [JsonIgnore]
    public bool IsText => Type == NoteBlockType.Text;

    [JsonIgnore]
    public bool IsImage => Type == NoteBlockType.Image;

    [JsonIgnore]
    public NoteImage? ResolvedImage
    {
        get => _resolvedImage;
        private set
        {
            if (SetField(ref _resolvedImage, value))
            {
                OnPropertyChanged(nameof(ImagePath));
                OnPropertyChanged(nameof(ImageName));
            }
        }
    }

    [JsonIgnore]
    public string ImagePath => ResolvedImage?.LocalPath ?? string.Empty;

    [JsonIgnore]
    public string ImageName => ResolvedImage?.OriginalFileName ?? "图片不可用";

    public event PropertyChangedEventHandler? PropertyChanged;

    public static NoteBlock CreateText(string text = "")
    {
        return new NoteBlock { Type = NoteBlockType.Text, Text = text };
    }

    public static NoteBlock CreateImage(NoteImage image)
    {
        var block = new NoteBlock
        {
            Type = NoteBlockType.Image,
            ImageId = image.Id,
            ImageWidth = Math.Clamp(image.PixelWidth, 180, 520)
        };
        block.ResolveImage(image);
        return block;
    }

    public NoteBlock Clone()
    {
        return new NoteBlock
        {
            Id = Id,
            Type = Type,
            Text = Text,
            ImageId = ImageId,
            ImageWidth = ImageWidth
        };
    }

    public void ResolveImage(NoteImage? image)
    {
        ResolvedImage = image;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
