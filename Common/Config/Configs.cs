using System.Runtime.Serialization;
using Tomlyn;

namespace Tyr.Common.Config;

public class Configs
{
    [DataMember(Name = "common")] public Common CommonInternal { get; set; } = new();
    [DataMember(Name = "network")] public Network NetworkInternal { get; set; } = new();
    [DataMember(Name = "vision")] public Vision VisionInternal { get; set; } = new();
    [DataMember(Name = "soccer")] public Soccer SoccerInternal { get; set; } = new();

    private static Configs Instance { get; set; } = null!;

    public static Common Common => Instance.CommonInternal;
    public static Network Network => Instance.NetworkInternal;
    public static Vision Vision => Instance.VisionInternal;
    public static Soccer Soccer => Instance.SoccerInternal;

    public static void Load(string path)
    {
        var text = File.ReadAllText(path);
        Instance = Toml.ToModel<Configs>(text);
    }

    public static void Save(string path)
    {
        var text = Toml.FromModel(Instance);
        File.WriteAllText(path, text);
    }
}