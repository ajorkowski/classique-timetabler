using System.IO.Compression;
using System.Text.Json;

namespace ClassiqueTimetabler.Maui.Data;

public static class FileService
{
    public static void Save(string filePath)
    {
        var json = JsonSerializer.Serialize(AppData.Current, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        using var fileStream = new FileStream(filePath, FileMode.Create);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("data.json");

        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream);
        writer.Write(json);
    }

    public static bool TryLoad(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            using var fileStream = new FileStream(filePath, FileMode.Open);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry("data.json");

            if (entry == null)
                return false;

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            var json = reader.ReadToEnd();

            var data = JsonSerializer.Deserialize<TimetableData>(json);
            if (data != null)
            {
                AppData.Current = data;
                return true;
            }
        }
        catch
        {
            // Load failed
        }

        return false;
    }
}
