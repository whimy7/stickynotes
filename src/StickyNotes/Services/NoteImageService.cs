using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using StickyNotes.Models;

namespace StickyNotes.Services;

public sealed class NoteImageService
{
    public const long MaximumFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif"
    };

    private readonly string _assetsDirectory;

    public NoteImageService(string assetsDirectory)
    {
        _assetsDirectory = assetsDirectory;
    }

    public void ResolvePaths(Note note)
    {
        foreach (var image in note.Images)
        {
            image.LocalPath = GetImagePath(note.Id, image);
        }
    }

    public NoteImage ImportFile(Note note, string sourcePath)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var extension = Path.GetExtension(fullSourcePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidDataException("仅支持 PNG、JPG、JPEG、BMP 和静态 GIF 图片。");
        }

        var sourceInfo = new FileInfo(fullSourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("图片文件不存在。", fullSourcePath);
        }

        if (sourceInfo.Length <= 0 || sourceInfo.Length > MaximumFileSize)
        {
            throw new InvalidDataException("单张图片必须小于 10 MB。 ");
        }

        var dimensions = ReadDimensions(fullSourcePath);
        var image = new NoteImage
        {
            FileName = $"{Guid.NewGuid():N}{extension}",
            OriginalFileName = Path.GetFileName(fullSourcePath),
            PixelWidth = dimensions.Width,
            PixelHeight = dimensions.Height,
            FileSize = sourceInfo.Length
        };

        var noteDirectory = GetNoteDirectory(note.Id);
        Directory.CreateDirectory(noteDirectory);
        var destinationPath = GetImagePath(note.Id, image);
        var temporaryPath = destinationPath + ".tmp";
        try
        {
            File.Copy(fullSourcePath, temporaryPath, false);
            File.Move(temporaryPath, destinationPath, false);
            image.LocalPath = destinationPath;
            return image;
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public NoteImage ImportClipboard(Note note, BitmapSource bitmap)
    {
        var image = new NoteImage
        {
            FileName = $"{Guid.NewGuid():N}.png",
            OriginalFileName = $"剪贴板图片 {DateTime.Now:yyyy-MM-dd HHmmss}.png",
            PixelWidth = bitmap.PixelWidth,
            PixelHeight = bitmap.PixelHeight
        };

        var noteDirectory = GetNoteDirectory(note.Id);
        Directory.CreateDirectory(noteDirectory);
        var destinationPath = GetImagePath(note.Id, image);
        var temporaryPath = destinationPath + ".tmp";

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(temporaryPath))
            {
                encoder.Save(stream);
            }

            var fileInfo = new FileInfo(temporaryPath);
            if (fileInfo.Length <= 0 || fileInfo.Length > MaximumFileSize)
            {
                throw new InvalidDataException("剪贴板图片保存后超过 10 MB。 ");
            }

            File.Move(temporaryPath, destinationPath, false);
            image.FileSize = fileInfo.Length;
            image.LocalPath = destinationPath;
            return image;
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public void Open(Note note, NoteImage image)
    {
        var path = GetImagePath(note.Id, image);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("图片文件不存在，可能已被移动或删除。", path);
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void Delete(Note note, NoteImage image)
    {
        var path = GetImagePath(note.Id, image);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteNoteAssets(Note note)
    {
        var noteDirectory = GetNoteDirectory(note.Id);
        if (Directory.Exists(noteDirectory))
        {
            Directory.Delete(noteDirectory, true);
        }
    }

    private string GetImagePath(Guid noteId, NoteImage image)
    {
        var safeFileName = Path.GetFileName(image.FileName);
        return Path.Combine(GetNoteDirectory(noteId), safeFileName);
    }

    private string GetNoteDirectory(Guid noteId)
    {
        return Path.Combine(_assetsDirectory, noteId.ToString("N"));
    }

    private static (int Width, int Height) ReadDimensions(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.First();
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException)
        {
            throw new InvalidDataException("无法读取该图片，文件可能已损坏。", exception);
        }
    }
}
