namespace DeskCanvas.App.Services;

internal sealed class MediaStore
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif"
    };

    private readonly string mediaDirectory;

    internal MediaStore(string root)
    {
        mediaDirectory = Path.Combine(root, "Media");
        Directory.CreateDirectory(mediaDirectory);
    }

    internal static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    internal string Import(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("素材ファイルが見つかりません。", sourcePath);
        }
        if (!IsSupported(sourcePath))
        {
            throw new NotSupportedException(
                "対応形式は PNG、JPEG、BMP、WebP、GIF です。");
        }

        var destinationName = $"{Guid.NewGuid():N}{Path.GetExtension(sourcePath).ToLowerInvariant()}";
        var destination = Path.Combine(mediaDirectory, destinationName);
        File.Copy(sourcePath, destination, overwrite: false);
        try { File.SetAttributes(destination, FileAttributes.Normal); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return destinationName;
    }

    internal string GetPath(string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        return Path.Combine(mediaDirectory, safeName);
    }

    internal void Delete(string storedFileName)
    {
        var path = GetPath(storedFileName);
        if (!File.Exists(path)) return;
        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal void Reclaim(IEnumerable<string> referencedFileNames)
    {
        var keep = new HashSet<string>(
            referencedFileNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);
        string[] files;
        try { files = Directory.GetFiles(mediaDirectory); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return; }
        foreach (var file in files)
        {
            if (!keep.Contains(Path.GetFileName(file))) Delete(Path.GetFileName(file));
        }
    }
}
