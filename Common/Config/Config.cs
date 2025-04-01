using Tomlyn;

namespace Tyr.Common.Config;

public class Config
{
    public Common Common { get; set; } = new();
    public Network Network { get; set; } = new();
    public Vision Vision { get; set; } = new();
    public Soccer Soccer { get; set; } = new();
    
    public static Config Load(string path)
    {
        var text = File.ReadAllText(path);
        return Toml.ToModel<Config>(text);
    }

    public void Save(string path)
    {
        var text = Toml.FromModel(this);
        File.WriteAllText(path, text);
    }
}