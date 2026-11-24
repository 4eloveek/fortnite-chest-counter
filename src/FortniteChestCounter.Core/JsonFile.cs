using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FortniteChestCounter.Core;

public static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), Options)
                ?? throw new InvalidDataException($"Пустые данные в файле {path}.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Не удалось прочитать {path}. Файл сохранён для восстановления.", ex);
        }
    }

    public static void Write<T>(string path, T value)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporaryPath = fullPath + ".tmp";
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(value, Options);

        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(data);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
