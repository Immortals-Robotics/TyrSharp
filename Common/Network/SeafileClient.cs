using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tyr.Common.Config;

namespace Tyr.Common.Network;

[Configurable]
public sealed partial class SeafileClient : IDisposable
{
    private readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("TyrSharp", "1.0") } }
    };

    [ConfigEntry("The Seafile server base URL")]
    private static string BaseUrl { get; set; } = "https://seafile.tigers-mannheim.de";

    [ConfigEntry("The public share token for the log directory")]
    private static string ShareToken { get; set; } = "e85851d9bc9944bf95bb";

    public async Task<List<SeafileDirent>> ListDirentsAsync(string path, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/v2.1/share-links/{ShareToken}/dirents/?path={Uri.EscapeDataString(path)}";
        var json = "";
        try
        {
            json = await _http.GetStringAsync(url, ct);
            var response = JsonSerializer.Deserialize<SeafileResponse>(json);
            return response?.DirentList ?? [];
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to parse Seafile response from {url}. Raw JSON: {json}");
            return [];
        }
    }

    public async Task DownloadFileAsync(string remotePath, string localPath, IProgress<float>? progress = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/d/{ShareToken}/files/?p={Uri.EscapeDataString(remotePath)}&dl=1";
        
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var src = await response.Content.ReadAsStreamAsync(ct);
        
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await using var dst = File.Create(localPath);

        var buf = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, read), ct);
            downloaded += read;
            if (total > 0) progress?.Report((float)downloaded / total);
        }
    }

    public void Dispose() => _http.Dispose();
}

public sealed class SeafileDirent
{
    [JsonPropertyName("is_dir")] public bool IsDir { get; set; }
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    
    [JsonPropertyName("folder_name")] public string? FolderName { get; set; }
    [JsonPropertyName("folder_path")] public string? FolderPath { get; set; }
    
    [JsonPropertyName("file_name")] public string? FileName { get; set; }
    [JsonPropertyName("file_path")] public string? FilePath { get; set; }

    [JsonPropertyName("obj_name")] public string? ObjName { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    
    [JsonPropertyName("last_modified")] public string? LastModified { get; set; }
    [JsonPropertyName("mtime")] public long? MTime { get; set; }
    
    public string EffectiveName => Name ?? (IsDir ? FolderName : FileName) ?? ObjName ?? "";
    public string UniqueId => !string.IsNullOrEmpty(Id) ? Id : (IsDir ? FolderPath : FilePath) ?? EffectiveName;
}

internal sealed class SeafileResponse
{
    [JsonPropertyName("dirent_list")] public List<SeafileDirent> DirentList { get; set; } = [];
}
