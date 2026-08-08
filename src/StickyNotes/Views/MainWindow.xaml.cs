using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StickyNotes.Models;
using StickyNotes.Services;

namespace StickyNotes.Views;

public partial class MainWindow : Window
{
    private readonly AppController _controller;

    public MainWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
        DataContext = controller;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_controller.IsShuttingDown)
        {
            NormalizeEmptyTitles();
            e.Cancel = true;
            _controller.ExitApplication();
        }

        base.OnClosing(e);
    }

    private void CreateNote_Click(object sender, RoutedEventArgs e)
    {
        var note = _controller.CreateNote();
        NotesGrid.SelectedItem = note;
        NotesGrid.ScrollIntoView(note);
    }

    private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Note note)
        {
            return;
        }

        if (note.IsVisible)
        {
            _controller.HideNote(note);
        }
        else
        {
            _controller.ShowNote(note);
        }
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Note note)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"确定要永久删除“{note.Title}”吗？",
            "删除便签",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
        {
            _controller.DeleteNote(note);
        }
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Note note)
        {
            return;
        }

        button.ContextMenu = NotePalette.CreateMenu(color => note.BackgroundColor = color);
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void NotesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NotesGrid.SelectedItem is Note note && e.OriginalSource is not TextBox)
        {
            _controller.ShowNote(note);
        }
    }

    private void NoteTitle_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Note note && string.IsNullOrWhiteSpace(note.Title))
        {
            note.Title = "未命名便签";
        }
    }

    private void NormalizeEmptyTitles()
    {
        foreach (var note in _controller.Notes.Where(note => string.IsNullOrWhiteSpace(note.Title)))
        {
            note.Title = "未命名便签";
        }
    }
}
