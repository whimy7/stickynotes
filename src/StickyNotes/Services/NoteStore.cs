using System.IO;
using System.IO.Compression;
using System.Text.Json;
using StickyNotes.Models;

namespace StickyNotes.Services;

public sealed class NoteStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _storageDirectory;
    private readonly string _storagePath;
    private readonly string _backupDirectory;
    private DateTimeOffset _lastBackupUtc = DateTimeOffset.MinValue;

    public NoteStore(string? storageDirectory = null)
    {
        _storageDirectory = string.IsNullOrWhiteSpace(storageDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StickyNotes")
            : Path.GetFullPath(storageDirectory);
        _storagePath = Path.Combine(_storageDirectory, "notes.json");
        _backupDirectory = Path.Combine(_storageDirectory, "Backups");
    }

    public string AssetsDirectory => Path.Combine(_storageDirectory, "Assets");

    public IReadOnlyList<Note> Load()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_storagePath);
            var notes = JsonSerializer.Deserialize<StorageDocument>(json, SerializerOptions)?.Notes ?? [];
            TryCreateBackup(force: true);
            return notes;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            var recoveryPath = TryPreserveUnreadableData();
            var recoveryMessage = recoveryPath is null
                ? "原数据文件未被修改。"
                : $"原数据文件未被修改，并已复制到：{recoveryPath}";
            throw new InvalidDataException($"无法读取便签数据。{recoveryMessage}", exception);
        }
    }

    public void Save(IEnumerable<Note> notes)
    {
        Directory.CreateDirectory(_storageDirectory);
        TryCreateBackup(force: false);
        var document = new StorageDocument { Notes = notes.ToList() };
        var json = JsonSerializer.Serialize(document, SerializerOptions);
        var temporaryPath = _storagePath + ".tmp";

        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _storagePath, true);
    }

    public void CreateBackupNow()
    {
        TryCreateBackup(force: true);
    }

    private string? TryPreserveUnreadableData()
    {
        try
        {
            Directory.CreateDirectory(_storageDirectory);
            var recoveryPath = Path.Combine(
                _storageDirectory,
                $"notes-unreadable-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.Copy(_storagePath, recoveryPath, false);
            return recoveryPath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void TryCreateBackup(bool force)
    {
        if (!File.Exists(_storagePath))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastBackupUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(_backupDirectory);
            var backupPath = Path.Combine(
                _backupDirectory,
                $"notes-auto-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip");
            temporaryPath = backupPath + ".tmp";

            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(_storagePath, "notes.json", CompressionLevel.Optimal);
                if (Directory.Exists(AssetsDirectory))
                {
                    foreach (var assetPath in Directory.EnumerateFiles(AssetsDirectory, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(_storageDirectory, assetPath).Replace('\\', '/');
                        archive.CreateEntryFromFile(assetPath, relativePath, CompressionLevel.Optimal);
                    }
                }
            }

            File.Move(temporaryPath, backupPath, false);
            _lastBackupUtc = now;

            foreach (var oldBackup in Directory
                .EnumerateFiles(_backupDirectory, "notes-auto-*.zip")
                .OrderByDescending(File.GetCreationTimeUtc)
                .Skip(30))
            {
                File.Delete(oldBackup);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
