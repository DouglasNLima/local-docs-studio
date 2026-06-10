using System.Text;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LensDocsStudio.Windows.Services;

public sealed class NativeFileService
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown",
        ".mmd",
        ".mermaid",
        ".txt",
    };

    private readonly nint ownerWindowHandle;
    private readonly Dictionary<string, string> nativeHandles = new(StringComparer.Ordinal);
    private readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public NativeFileService(Window ownerWindow)
    {
        ownerWindowHandle = WindowNative.GetWindowHandle(ownerWindow);
    }

    public static bool IsSupportedExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);
    }

    public async Task<object> OpenFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".mmd");
        picker.FileTypeFilter.Add(".mermaid");
        picker.FileTypeFilter.Add(".txt");
        InitializeWithWindow.Initialize(picker, ownerWindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return new { cancelled = true };
        }

        return await OpenFilePathAsync(file.Path);
    }

    public async Task<object> OpenFilePathAsync(string path)
    {
        ValidateOpenPath(path);
        var info = new FileInfo(path);
        if (info.Length > MaxFileBytes)
        {
            throw new NativeFileException("Choose a UTF-8 text file up to 5 MB.");
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(path, strictUtf8);
        }
        catch (DecoderFallbackException)
        {
            throw new NativeFileException("Choose a UTF-8 encoded text file.");
        }

        var handleId = CreateHandle(path);
        var name = Path.GetFileName(path);
        return new
        {
            cancelled = false,
            name,
            displayName = name,
            extension = Path.GetExtension(name).ToLowerInvariant(),
            encoding = "utf-8",
            content,
            nativeHandleId = handleId,
        };
    }

    public async Task<object> SaveFileAsync(string? nativeHandleId, string? content)
    {
        if (string.IsNullOrWhiteSpace(nativeHandleId) || !nativeHandles.TryGetValue(nativeHandleId, out var path))
        {
            throw new NativeFileException("The Windows file handle is no longer available. Use Save as.");
        }

        ValidatePath(path);
        ValidateContent(content);
        await File.WriteAllTextAsync(path, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var name = Path.GetFileName(path);
        return new
        {
            saved = true,
            name,
            displayName = name,
            encoding = "utf-8",
        };
    }

    public async Task<object> SaveFileAsAsync(string? suggestedName, string? content)
    {
        ValidateContent(content);
        var safeSuggestedName = GetSafeSuggestedName(suggestedName);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(safeSuggestedName),
            DefaultFileExtension = Path.GetExtension(safeSuggestedName),
        };
        picker.FileTypeChoices.Add("Markdown files", new List<string> { ".md", ".markdown" });
        picker.FileTypeChoices.Add("Mermaid files", new List<string> { ".mmd", ".mermaid" });
        picker.FileTypeChoices.Add("Text files", new List<string> { ".txt" });
        InitializeWithWindow.Initialize(picker, ownerWindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return new { cancelled = true };
        }

        return await SaveFileAsPathAsync(file.Path, content);
    }

    public async Task<object> SaveFileAsPathAsync(string path, string? content)
    {
        ValidatePath(path);
        ValidateContent(content);
        await File.WriteAllTextAsync(path, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var handleId = CreateHandle(path);
        var name = Path.GetFileName(path);
        return new
        {
            cancelled = false,
            saved = true,
            name,
            displayName = name,
            encoding = "utf-8",
            nativeHandleId = handleId,
        };
    }

    private static void ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new NativeFileException("The selected file is unavailable.");
        }

        var extension = Path.GetExtension(path);
        if (!IsSupportedExtension(extension))
        {
            throw new NativeFileException("Choose a .md, .markdown, .mmd, .mermaid, or .txt file.");
        }
    }

    private static void ValidateOpenPath(string? path)
    {
        ValidatePath(path);

        var fullPath = Path.GetFullPath(path!);
        if (!File.Exists(fullPath))
        {
            throw new NativeFileException("The selected file is unavailable.");
        }

        if (Directory.Exists(fullPath))
        {
            throw new NativeFileException("Choose a file, not a folder.");
        }
    }

    private static void ValidateContent(string? content)
    {
        var byteCount = Encoding.UTF8.GetByteCount(content ?? string.Empty);
        if (byteCount > MaxFileBytes)
        {
            throw new NativeFileException("Save a UTF-8 text file up to 5 MB.");
        }
    }

    private static string GetSafeSuggestedName(string? suggestedName)
    {
        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(suggestedName) ? "document.md" : suggestedName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "document.md";
        }

        return IsSupportedExtension(Path.GetExtension(fileName))
            ? fileName
            : $"{Path.GetFileNameWithoutExtension(fileName)}.md";
    }

    private string CreateHandle(string path)
    {
        var handleId = Guid.NewGuid().ToString("N");
        nativeHandles[handleId] = path;
        return handleId;
    }
}

public sealed class NativeFileException : Exception
{
    public NativeFileException(string message)
        : base(message)
    {
    }
}
