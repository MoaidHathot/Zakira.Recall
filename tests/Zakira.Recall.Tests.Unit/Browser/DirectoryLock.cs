namespace Zakira.Recall.Tests.Unit.Browser;

/// <summary>
/// Makes a directory undeletable for the duration of the lock, the way a browser that still holds files does.
/// Windows: an open handle with <see cref="FileShare.None"/> on a file inside blocks deletion of the file and therefore
/// of the directory. Unix: open handles do not block unlink, so the directory's write permission is removed instead,
/// which makes deleting its entries fail with EACCES; the original mode is restored on dispose.
/// </summary>
internal sealed class DirectoryLock : IDisposable
{
    private readonly FileStream? _handle;
    private readonly string? _directory;
    private readonly UnixFileMode _originalMode;

    public DirectoryLock(string directory, string fileName)
    {
        if (OperatingSystem.IsWindows())
        {
            _handle = new FileStream(Path.Combine(directory, fileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        else
        {
            _directory = directory;
            _originalMode = File.GetUnixFileMode(directory);
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }

    public void Dispose()
    {
        _handle?.Dispose();
        if (!OperatingSystem.IsWindows() && _directory is not null && Directory.Exists(_directory))
        {
            File.SetUnixFileMode(_directory, _originalMode);
        }
    }
}
