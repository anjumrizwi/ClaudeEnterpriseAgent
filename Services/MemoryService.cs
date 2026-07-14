using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class MemoryService
{
    private readonly string _filePath;

    public MemoryService(string filePath = null)
    {
        _filePath = filePath ?? Path.Combine(Directory.GetCurrentDirectory(), "workspace", "memory.json");
    }

    public void Append(Message message)
    {
        var history = Load();
        history.Add(message);
        Save(history);
    }

    public List<Message> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new List<Message>();
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<Message>>(json) ?? new List<Message>();
    }

    public void Save(List<Message> history)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
