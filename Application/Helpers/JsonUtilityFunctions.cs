using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Application.Helpers;

public class JsonUtilityFunctions
{
    public static string GetJsonString(object o)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true
        };

        return JsonSerializer.Serialize(o, options);
    }

    public static async Task WriteToFile(object o, string path)
    {
        await File.WriteAllTextAsync(path, GetJsonString(o));
    }
}