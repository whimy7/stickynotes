using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using StickyNotes.Models;
using StickyNotes.Views;

namespace StickyNotes.Services;

public sealed class AppController
{
    private readonly NoteStore _store;
    private readonly NoteImageService _imageService;
    private readonly Dictionary<Guid, NoteWindow> _noteWindows = [];
    private readonly DispatcherTimer _saveTimer;
    private MainWindow? _mainWindow;
    private bool _isShuttingDown;

    public AppController(NoteStore store)
    {
        _store = store;
        _imageService = new NoteImageService(store.AssetsDirectory);
        Notes = new ObservableCollection<Note>(_store.Load().OrderByDescending(note => note.UpdatedAtUtc));
        foreach (var note in Notes)
        {
            if (string.IsNullOrWhiteSpace(note.Title))
            {
                note.Title = "未命名便签";
            }

            _imageService.ResolvePaths(note);
            note.EnsureDocumentBlocks();
            note.ResolveDocumentImages();
            note.PropertyChanged += OnNotePropertyChanged;
        }

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    public ObservableCollection<Note> Notes { get; }

    public bool IsShuttingDown => _isShuttingDown;

    public void Start()
    {
        _mainWindow = new MainWindow(this);
        Application.Current.MainWindow = _mainWindow;
        _mainWindow.Show();

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (var note in Notes.Where(note => note.IsVisible).ToList())
            {
                ShowNote(note, activate: false);
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    public Note CreateNote()
    {
        var noteNumber = Notes.Count + 1;
        var note = new Note
        {
            Title = noteNumber == 1 ? "新便签" : $"新便签 {noteNumber}",
            IsVisible = true,
            WindowLeft = 120 + Notes.Count % 10 * 24,
            WindowTop = 120 + Notes.Count % 10 * 24
        };

        note.EnsureDocumentBlocks();
        note.PropertyChanged += OnNotePropertyChanged;
        Notes.Insert(0, note);
        ScheduleSave();
        ShowNote(note);
        return note;
    }

    public void ShowNote(Note note, bool activate = true)
    {
        if (!_noteWindows.TryGetValue(note.Id, out var window))
        {
            window = new NoteWindow(this, note);
            _noteWindows[note.Id] = window;
        }

        note.IsVisible = true;
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (activate)
        {
            window.Activate();
        }
    }

    public void HideNote(Note note)
    {
        if (_noteWindows.Remove(note.Id, out var window))
        {
            window.PrepareForShutdown();
            window.Close();
        }

        note.IsVisible = false;
    }

    public void DeleteNote(Note note)
    {
        _store.CreateBackupNow();
        note.PropertyChanged -= OnNotePropertyChanged;
        if (_noteWindows.Remove(note.Id, out var window))
        {
            window.PrepareForShutdown();
            window.Close();
        }

        Notes.Remove(note);
        SaveNow();
        _imageService.DeleteNoteAssets(note);
    }

    public NoteImage ImportImageFromFile(Note note, string sourcePath)
    {
        return _imageService.ImportFile(note, sourcePath);
    }

    public NoteImage ImportImageFromClipboard(Note note, System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        return _imageService.ImportClipboard(note, bitmap);
    }

    public void InsertImages(
        Note note,
        IReadOnlyList<NoteImage> images,
        NoteBlock? anchor,
        int caretIndex,
        int? requestedInsertIndex = null)
    {
        if (images.Count == 0)
        {
            return;
        }

        var originalBlocks = note.DocumentBlocks.Select(block => block.Clone()).ToList();
        try
        {
            foreach (var image in images)
            {
                note.Images.Add(image);
            }

            var anchorIndex = anchor is not null ? note.DocumentBlocks.IndexOf(anchor) : -1;
            var insertIndex = note.DocumentBlocks.Count;
            if (anchorIndex >= 0 && anchor!.IsText)
            {
                var splitIndex = Math.Clamp(caretIndex, 0, anchor.Text.Length);
                var trailingText = anchor.Text[splitIndex..];
                anchor.Text = anchor.Text[..splitIndex];
                insertIndex = anchorIndex + 1;

                foreach (var image in images)
                {
                    note.DocumentBlocks.Insert(insertIndex++, NoteBlock.CreateImage(image));
                }

                note.DocumentBlocks.Insert(insertIndex, NoteBlock.CreateText(trailingText));
            }
            else
            {
                insertIndex = Math.Clamp(requestedInsertIndex ?? note.DocumentBlocks.Count, 0, note.DocumentBlocks.Count);
                foreach (var image in images)
                {
                    note.DocumentBlocks.Insert(insertIndex++, NoteBlock.CreateImage(image));
                }

                if (note.DocumentBlocks[^1].IsImage)
                {
                    note.DocumentBlocks.Add(NoteBlock.CreateText());
                }
            }

            note.ResolveDocumentImages();
            note.UpdatedAtUtc = DateTimeOffset.UtcNow;
            SaveNow();
        }
        catch
        {
            note.DocumentBlocks = new ObservableCollection<NoteBlock>(originalBlocks);
            foreach (var image in images)
            {
                note.Images.Remove(image);
                _imageService.Delete(note, image);
            }

            note.ResolveDocumentImages();
            throw;
        }
    }

    public void OpenImage(Note note, NoteBlock block)
    {
        var image = FindImage(note, block);
        if (image is null)
        {
            throw new FileNotFoundException("图片记录不存在。");
        }

        _imageService.Open(note, image);
    }

    public void DeleteImageBlock(Note note, NoteBlock block)
    {
        var index = note.DocumentBlocks.IndexOf(block);
        if (index < 0 || !block.IsImage)
        {
            return;
        }

        _store.CreateBackupNow();
        note.DocumentBlocks.RemoveAt(index);
        MergeAdjacentTextBlocks(note, Math.Max(0, index - 1));
        EnsureEditableDocument(note);
        note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveNow();
    }

    public void MoveBlock(Note note, NoteBlock block, int targetIndex)
    {
        var currentIndex = note.DocumentBlocks.IndexOf(block);
        if (currentIndex < 0)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, note.DocumentBlocks.Count);
        note.DocumentBlocks.RemoveAt(currentIndex);
        if (targetIndex > currentIndex)
        {
            targetIndex--;
        }

        note.DocumentBlocks.Insert(targetIndex, block);
        EnsureEditableDocument(note);
        note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveNow();
    }

    public void ApplyDocumentBlocks(Note note, IEnumerable<NoteBlock> blocks)
    {
        note.DocumentBlocks = new ObservableCollection<NoteBlock>(blocks.Select(block => block.Clone()));
        EnsureEditableDocument(note);
        note.ResolveDocumentImages();
        note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveNow();
    }

    public void SaveDocumentNow(Note note)
    {
        note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveNow();
    }

    public void FinalizeDocumentSession(Note note)
    {
        var referencedImageIds = note.DocumentBlocks
            .Where(block => block.IsImage && block.ImageId.HasValue)
            .Select(block => block.ImageId!.Value)
            .ToHashSet();
        var unusedImages = note.Images
            .Where(image => !referencedImageIds.Contains(image.Id))
            .ToList();
        if (unusedImages.Count == 0)
        {
            return;
        }

        _store.CreateBackupNow();
        foreach (var image in unusedImages)
        {
            try
            {
                _imageService.Delete(note, image);
                note.Images.Remove(image);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        SaveNow();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void ExitApplication()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _saveTimer.Stop();
        SaveNow();

        foreach (var window in _noteWindows.Values.ToList())
        {
            window.PrepareForShutdown();
            window.Close();
        }

        Application.Current.Shutdown();
    }

    private void OnNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is Note note && e.PropertyName is nameof(Note.Title) or nameof(Note.Content) or nameof(Note.BackgroundColor) or nameof(Note.ImageCount))
        {
            note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer.Stop();
        _store.Save(Notes);
    }

    private static NoteImage? FindImage(Note note, NoteBlock block)
    {
        return block.ImageId is Guid imageId
            ? note.Images.FirstOrDefault(image => image.Id == imageId)
            : null;
    }

    private static void EnsureEditableDocument(Note note)
    {
        if (note.DocumentBlocks.Count == 0)
        {
            note.DocumentBlocks.Add(NoteBlock.CreateText());
        }

        if (note.DocumentBlocks[^1].IsImage)
        {
            note.DocumentBlocks.Add(NoteBlock.CreateText());
        }
    }

    private static void MergeAdjacentTextBlocks(Note note, int startIndex)
    {
        for (var index = Math.Clamp(startIndex, 0, Math.Max(0, note.DocumentBlocks.Count - 1));
             index < note.DocumentBlocks.Count - 1;)
        {
            var current = note.DocumentBlocks[index];
            var next = note.DocumentBlocks[index + 1];
            if (current.IsText && next.IsText)
            {
                current.Text += next.Text;
                note.DocumentBlocks.RemoveAt(index + 1);
                continue;
            }

            index++;
        }
    }
}
