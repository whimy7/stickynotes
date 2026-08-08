using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using StickyNotes.Models;
using StickyNotes.Services;

namespace StickyNotes.Views;

public partial class NoteWindow : Window
{
    private const string ImageBlockDragFormat = "StickyNotes.ImageBlock";
    private const int MaximumHistoryEntries = 100;

    private readonly AppController _controller;
    private readonly Note _note;
    private readonly DispatcherTimer _historyTimer;
    private readonly List<IReadOnlyList<BlockState>> _history = [];
    private NoteBlock? _activeTextBlock;
    private int _activeCaretIndex;
    private NoteBlock? _dragCandidate;
    private Point _dragStart;
    private NoteBlock? _resizeBlock;
    private Point _resizeStart;
    private double _resizeInitialWidth;
    private int _historyIndex;
    private bool _isReady;
    private bool _allowClose;
    private bool _isApplyingHistory;
    private bool _isHistorySuspended;

    public NoteWindow(AppController controller, Note note)
    {
        InitializeComponent();
        _controller = controller;
        _note = note;
        DataContext = note;

        _historyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _historyTimer.Tick += (_, _) => CommitHistoryPoint();
        _history.Add(CaptureSnapshot());
        UpdateHistoryButtons();

        _note.PropertyChanged += Note_PropertyChanged;
        Loaded += NoteWindow_Loaded;
        LocationChanged += NoteWindow_LocationChanged;
        SizeChanged += NoteWindow_SizeChanged;
    }

    public void PrepareForShutdown()
    {
        CommitHistoryPoint();
        _historyTimer.Stop();
        _note.PropertyChanged -= Note_PropertyChanged;
        _controller.FinalizeDocumentSession(_note);
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(() => _controller.HideNote(_note));
        }

        base.OnClosing(e);
    }

    private void NoteWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Width = Math.Clamp(_note.WindowWidth, MinWidth, MaxWidth);
        Height = Math.Clamp(_note.WindowHeight, MinHeight, MaxHeight);

        var requestedLeft = _note.WindowLeft;
        var requestedTop = _note.WindowTop;
        if (IsPlacementVisible(requestedLeft, requestedTop, Width, Height))
        {
            Left = requestedLeft;
            Top = requestedTop;
        }
        else
        {
            Left = SystemParameters.WorkArea.Left + 80;
            Top = SystemParameters.WorkArea.Top + 80;
        }

        _isReady = true;
        Dispatcher.BeginInvoke(FocusLastTextBlock, DispatcherPriority.ApplicationIdle);
    }

    private static bool IsPlacementVisible(double left, double top, double width, double height)
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        return left + Math.Min(width, 80) >= virtualLeft
            && top + Math.Min(height, 80) >= virtualTop
            && left <= virtualRight - 80
            && top <= virtualBottom - 80;
    }

    private void NoteWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (!_isReady || WindowState != WindowState.Normal)
        {
            return;
        }

        _note.WindowLeft = Left;
        _note.WindowTop = Top;
    }

    private void NoteWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isReady || WindowState != WindowState.Normal)
        {
            return;
        }

        _note.WindowWidth = ActualWidth;
        _note.WindowHeight = ActualHeight;
    }

    private void Note_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Note.DocumentRevision) || _isApplyingHistory || _isHistorySuspended)
        {
            return;
        }

        _historyTimer.Stop();
        _historyTimer.Start();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.ContextMenu = NotePalette.CreateMenu(color => _note.BackgroundColor = color);
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图片",
            Filter = "支持的图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif|PNG 图片|*.png|JPEG 图片|*.jpg;*.jpeg|BMP 图片|*.bmp|GIF 图片|*.gif",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            ImportFiles(dialog.FileNames, _activeTextBlock, _activeCaretIndex);
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        Redo();
    }

    private void TextBlock_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is NoteBlock block)
        {
            _activeTextBlock = block;
            _activeCaretIndex = textBox.CaretIndex;
        }
    }

    private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is NoteBlock block && textBox.IsKeyboardFocusWithin)
        {
            _activeTextBlock = block;
            _activeCaretIndex = textBox.CaretIndex;
        }
    }

    private void TextBlock_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        try
        {
            if (!Clipboard.ContainsImage())
            {
                return;
            }

            var bitmap = Clipboard.GetImage();
            if (bitmap is null)
            {
                return;
            }

            var textBox = (TextBox)sender;
            var image = _controller.ImportImageFromClipboard(_note, bitmap);
            CommitHistoryPoint();
            _controller.InsertImages(_note, [image], (NoteBlock)textBox.DataContext, textBox.CaretIndex);
            CommitHistoryPoint();
            e.Handled = true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or System.Runtime.InteropServices.ExternalException)
        {
            ShowImageError(exception.Message);
            e.Handled = true;
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ImageBlockDragFormat)
            ? DragDropEffects.Move
            : e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var targetIndex = GetDropIndex(e);
        if (e.Data.GetDataPresent(ImageBlockDragFormat))
        {
            if (e.Data.GetData(ImageBlockDragFormat) is string idText
                && Guid.TryParse(idText, out var blockId)
                && _note.DocumentBlocks.FirstOrDefault(block => block.Id == blockId) is { IsImage: true } imageBlock)
            {
                CommitHistoryPoint();
                _controller.MoveBlock(_note, imageBlock, targetIndex);
                CommitHistoryPoint();
            }

            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            ImportFiles(files, null, 0, targetIndex);
            e.Handled = true;
        }
    }

    private void ImageDragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is NoteBlock block)
        {
            _dragCandidate = block;
            _dragStart = e.GetPosition(this);
        }
    }

    private void ImageDragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var block = _dragCandidate;
        _dragCandidate = null;
        var data = new DataObject(ImageBlockDragFormat, block.Id.ToString());
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
    }

    private void ImageResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not NoteBlock block)
        {
            return;
        }

        CommitHistoryPoint();
        _isHistorySuspended = true;
        _resizeBlock = block;
        _resizeStart = e.GetPosition(this);
        _resizeInitialWidth = block.ImageWidth;
        Mouse.Capture((IInputElement)sender);
        e.Handled = true;
    }

    private void ImageResize_MouseMove(object sender, MouseEventArgs e)
    {
        if (_resizeBlock is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        _resizeBlock.ImageWidth = _resizeInitialWidth + position.X - _resizeStart.X;
        e.Handled = true;
    }

    private void ImageResize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CompleteImageResize();
        Mouse.Capture(null);
        e.Handled = true;
    }

    private void ImageResize_LostMouseCapture(object sender, MouseEventArgs e)
    {
        CompleteImageResize();
    }

    private void ResizeImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: NoteBlock block, CommandParameter: string deltaText }
            || !double.TryParse(deltaText, out var delta))
        {
            return;
        }

        CommitHistoryPoint();
        block.ImageWidth += delta;
        _controller.SaveDocumentNow(_note);
        CommitHistoryPoint();
    }

    private void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not NoteBlock block)
        {
            return;
        }

        try
        {
            _controller.OpenImage(_note, block);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or Win32Exception)
        {
            ShowImageError(exception.Message);
        }
    }

    private void DeleteImage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not NoteBlock block)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"确定要从正文删除“{block.ImageName}”吗？",
            "删除图片",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        CommitHistoryPoint();
        _controller.DeleteImageBlock(_note, block);
        CommitHistoryPoint();
    }

    private void DocumentItems_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<TextBox>(e.OriginalSource as DependencyObject) is not null
            || FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        Dispatcher.BeginInvoke(FocusLastTextBlock, DispatcherPriority.Input);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _controller.HideNote(_note);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ImportFiles(
        IEnumerable<string> files,
        NoteBlock? anchor,
        int caretIndex,
        int? requestedInsertIndex = null)
    {
        var importedImages = new List<NoteImage>();
        var errors = new List<string>();
        foreach (var file in files)
        {
            try
            {
                importedImages.Add(_controller.ImportImageFromFile(_note, file));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                errors.Add($"{Path.GetFileName(file)}：{exception.Message}");
            }
        }

        if (importedImages.Count > 0)
        {
            try
            {
                CommitHistoryPoint();
                _controller.InsertImages(_note, importedImages, anchor, caretIndex, requestedInsertIndex);
                CommitHistoryPoint();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add(exception.Message);
            }
        }

        if (errors.Count > 0)
        {
            ShowImageError(string.Join(Environment.NewLine, errors));
        }
    }

    private int GetDropIndex(DragEventArgs e)
    {
        var targetElement = FindAncestor<FrameworkElement>(e.OriginalSource as DependencyObject,
            element => element.Tag is NoteBlock);
        if (targetElement?.Tag is not NoteBlock targetBlock)
        {
            return _note.DocumentBlocks.Count;
        }

        var index = _note.DocumentBlocks.IndexOf(targetBlock);
        var position = e.GetPosition(targetElement);
        return position.Y > targetElement.ActualHeight / 2 ? index + 1 : index;
    }

    private void Undo()
    {
        CommitHistoryPoint();
        if (_historyIndex <= 0)
        {
            return;
        }

        _historyIndex--;
        ApplySnapshot(_history[_historyIndex]);
    }

    private void Redo()
    {
        CommitHistoryPoint();
        if (_historyIndex >= _history.Count - 1)
        {
            return;
        }

        _historyIndex++;
        ApplySnapshot(_history[_historyIndex]);
    }

    private void ApplySnapshot(IReadOnlyList<BlockState> snapshot)
    {
        _historyTimer.Stop();
        _isApplyingHistory = true;
        try
        {
            _controller.ApplyDocumentBlocks(_note, snapshot.Select(state => state.ToBlock()));
            _activeTextBlock = null;
            _activeCaretIndex = 0;
        }
        finally
        {
            _isApplyingHistory = false;
        }

        UpdateHistoryButtons();
        Dispatcher.BeginInvoke(FocusLastTextBlock, DispatcherPriority.Input);
    }

    private void CommitHistoryPoint()
    {
        _historyTimer.Stop();
        if (_isApplyingHistory || _isHistorySuspended)
        {
            return;
        }

        var snapshot = CaptureSnapshot();
        if (_history[_historyIndex].SequenceEqual(snapshot))
        {
            UpdateHistoryButtons();
            return;
        }

        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        _history.Add(snapshot);
        if (_history.Count > MaximumHistoryEntries)
        {
            _history.RemoveAt(0);
        }

        _historyIndex = _history.Count - 1;
        UpdateHistoryButtons();
    }

    private IReadOnlyList<BlockState> CaptureSnapshot()
    {
        return _note.DocumentBlocks
            .Select(block => new BlockState(block.Id, block.Type, block.Text, block.ImageId, block.ImageWidth))
            .ToList();
    }

    private void UpdateHistoryButtons()
    {
        UndoButton.IsEnabled = _historyIndex > 0;
        RedoButton.IsEnabled = _historyIndex < _history.Count - 1;
    }

    private void CompleteImageResize()
    {
        if (_resizeBlock is null)
        {
            return;
        }

        _resizeBlock = null;
        _isHistorySuspended = false;
        _controller.SaveDocumentNow(_note);
        CommitHistoryPoint();
    }

    private void FocusLastTextBlock()
    {
        var textBlock = _note.DocumentBlocks.LastOrDefault(block => block.IsText);
        if (textBlock is null)
        {
            return;
        }

        var container = DocumentItems.ItemContainerGenerator.ContainerFromItem(textBlock) as DependencyObject;
        var textBox = FindVisualChild<TextBox>(container);
        if (textBox is null)
        {
            return;
        }

        textBox.Focus();
        textBox.CaretIndex = textBox.Text.Length;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                return result;
            }

            if (FindVisualChild<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? element, Func<T, bool>? predicate = null)
        where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T result && (predicate is null || predicate(result)))
            {
                return result;
            }

            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return null;
    }

    private void ShowImageError(string message)
    {
        MessageBox.Show(this, message, "图片处理失败", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private sealed record BlockState(
        Guid Id,
        NoteBlockType Type,
        string Text,
        Guid? ImageId,
        double ImageWidth)
    {
        public NoteBlock ToBlock()
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
    }
}
