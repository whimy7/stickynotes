using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StickyNotes.Models;

public sealed class Note : INotifyPropertyChanged
{
    private string _title = "新便签";
    private string _content = string.Empty;
    private string _backgroundColor = "#FFF2A8";
    private bool _isVisible;
    private bool _isTopmost;
    private double _windowLeft = 120;
    private double _windowTop = 120;
    private double _windowWidth = 340;
    private double _windowHeight = 360;
    private DateTimeOffset _updatedAtUtc = DateTimeOffset.UtcNow;
    private ObservableCollection<NoteImage> _images = [];
    private ObservableCollection<NoteBlock> _documentBlocks = [];
    private int _documentRevision;

    public Note()
    {
        _images.CollectionChanged += OnImagesChanged;
        _documentBlocks.CollectionChanged += OnDocumentBlocksChanged;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set
        {
            if (SetField(ref _title, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetField(ref _content, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Preview));
            }
        }
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set => SetField(ref _backgroundColor, value);
    }

    public ObservableCollection<NoteImage> Images
    {
        get => _images;
        set
        {
            if (ReferenceEquals(_images, value))
            {
                return;
            }

            _images.CollectionChanged -= OnImagesChanged;
            _images = value ?? [];
            _images.CollectionChanged += OnImagesChanged;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<NoteBlock> DocumentBlocks
    {
        get => _documentBlocks;
        set
        {
            if (ReferenceEquals(_documentBlocks, value))
            {
                return;
            }

            UnsubscribeBlocks(_documentBlocks);
            _documentBlocks.CollectionChanged -= OnDocumentBlocksChanged;
            _documentBlocks = value ?? [];
            _documentBlocks.CollectionChanged += OnDocumentBlocksChanged;
            SubscribeBlocks(_documentBlocks);
            OnPropertyChanged();
            NotifyDocumentChanged();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    public bool IsTopmost
    {
        get => _isTopmost;
        set => SetField(ref _isTopmost, value);
    }

    public double WindowLeft
    {
        get => _windowLeft;
        set => SetField(ref _windowLeft, value);
    }

    public double WindowTop
    {
        get => _windowTop;
        set => SetField(ref _windowTop, value);
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set => SetField(ref _windowWidth, value);
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set => SetField(ref _windowHeight, value);
    }

    public DateTimeOffset UpdatedAtUtc
    {
        get => _updatedAtUtc;
        set
        {
            if (SetField(ref _updatedAtUtc, value))
            {
                OnPropertyChanged(nameof(DisplayUpdatedAt));
            }
        }
    }

    [JsonIgnore]
    public string Preview
    {
        get
        {
            var normalized = Content.Replace("\r", " ").Replace("\n", " ").Trim();
            if (ImageCount == 0)
            {
                return string.IsNullOrEmpty(normalized) ? "空白便签" : normalized;
            }

            var imageSummary = $"{ImageCount} 张图片";
            return string.IsNullOrEmpty(normalized) ? imageSummary : $"{normalized} · {imageSummary}";
        }
    }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "未命名便签" : Title;

    [JsonIgnore]
    public string DisplayUpdatedAt => UpdatedAtUtc.ToLocalTime().ToString("MM-dd HH:mm");

    [JsonIgnore]
    public bool HasImages => ImageCount > 0;

    [JsonIgnore]
    public int ImageCount => DocumentBlocks.Count(block => block.IsImage);

    [JsonIgnore]
    public int DocumentRevision => _documentRevision;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void EnsureDocumentBlocks()
    {
        if (DocumentBlocks.Count > 0)
        {
            return;
        }

        DocumentBlocks.Add(NoteBlock.CreateText(Content));
        foreach (var image in Images)
        {
            DocumentBlocks.Add(NoteBlock.CreateImage(image));
        }

        if (DocumentBlocks[^1].IsImage)
        {
            DocumentBlocks.Add(NoteBlock.CreateText());
        }
    }

    public void ResolveDocumentImages()
    {
        var imagesById = Images.ToDictionary(image => image.Id);
        foreach (var block in DocumentBlocks.Where(block => block.IsImage))
        {
            block.ResolveImage(block.ImageId is Guid imageId && imagesById.TryGetValue(imageId, out var image)
                ? image
                : null);
        }
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

    private void OnImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ResolveDocumentImages();
    }

    private void OnDocumentBlocksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (NoteBlock block in e.OldItems)
            {
                block.PropertyChanged -= OnBlockPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (NoteBlock block in e.NewItems)
            {
                block.PropertyChanged += OnBlockPropertyChanged;
            }
        }

        NotifyDocumentChanged();
    }

    private void OnBlockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NoteBlock.Text) or nameof(NoteBlock.ImageWidth))
        {
            NotifyDocumentChanged();
        }
    }

    private void NotifyDocumentChanged()
    {
        var content = string.Concat(DocumentBlocks.Where(block => block.IsText).Select(block => block.Text));
        if (_content != content)
        {
            _content = content;
            OnPropertyChanged(nameof(Content));
            OnPropertyChanged(nameof(Preview));
        }

        _documentRevision++;
        OnPropertyChanged(nameof(DocumentRevision));
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(ImageCount));
        OnPropertyChanged(nameof(Preview));
    }

    private void SubscribeBlocks(IEnumerable<NoteBlock> blocks)
    {
        foreach (var block in blocks)
        {
            block.PropertyChanged += OnBlockPropertyChanged;
        }
    }

    private void UnsubscribeBlocks(IEnumerable<NoteBlock> blocks)
    {
        foreach (var block in blocks)
        {
            block.PropertyChanged -= OnBlockPropertyChanged;
        }
    }
}
