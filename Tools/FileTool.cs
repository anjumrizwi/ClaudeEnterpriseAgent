using System;
using System.IO;
using System.Linq;

public class FileTool
{
    private readonly string _rootDirectory;

    public FileTool(string rootDirectory = null)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "workspace"));
        Directory.CreateDirectory(_rootDirectory);
    }

    public string Name => "file";

    public string Description =>
        "Reads, writes, lists, or deletes files within a sandboxed workspace directory.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            operation = new
            {
                type = "string",
                @enum = new[] { "read", "write", "list", "delete" },
                description = "The file operation to perform",
            },
            path = new
            {
                type = "string",
                description = "Path to the file or directory, relative to the sandbox root",
            },
            content = new
            {
                type = "string",
                description = "Content to write (required for the write operation)",
            },
        },
        required = new[] { "operation", "path" },
    };

    public string Execute(string operation, string path, string content = null)
    {
        var fullPath = ResolvePath(path);

        return operation switch
        {
            "read" => Read(fullPath),
            "write" => Write(fullPath, content),
            "list" => List(fullPath),
            "delete" => Delete(fullPath),
            _ => throw new ArgumentException($"Unknown operation: {operation}", nameof(operation)),
        };
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        var combined = Path.GetFullPath(Path.Combine(_rootDirectory, path));

        if (!IsWithinRoot(combined))
        {
            throw new UnauthorizedAccessException($"Path '{path}' escapes the sandbox root.");
        }

        return combined;
    }

    private bool IsWithinRoot(string candidate)
    {
        var rootWithSeparator = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;

        return candidate.Equals(_rootDirectory, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fullPath}");
        }

        return File.ReadAllText(fullPath);
    }

    private static string Write(string fullPath, string content)
    {
        if (content is null)
        {
            throw new ArgumentException("Content must be provided for the write operation.", nameof(content));
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
        return $"Wrote {content.Length} characters to {fullPath}";
    }

    private static string List(string fullPath)
    {
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        return string.Join(Environment.NewLine, Directory.GetFileSystemEntries(fullPath).Select(Path.GetFileName));
    }

    private static string Delete(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return $"Deleted file {fullPath}";
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
            return $"Deleted directory {fullPath}";
        }

        throw new FileNotFoundException($"Path not found: {fullPath}");
    }
}
