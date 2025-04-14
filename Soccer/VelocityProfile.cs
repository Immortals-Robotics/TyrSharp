using Tyr.Common.Config;

namespace Tyr.Soccer;

[Configurable]
public struct VelocityProfile
{
    [ConfigEntry] public static VelocityProfile Sooski { get; set; }
    [ConfigEntry] public static VelocityProfile Aroom { get; set; }
    [ConfigEntry] public static VelocityProfile Mamooli { get; set; }
    [ConfigEntry] public static VelocityProfile Kharaki { get; set; }

    public float Speed { get; set; }
    public float Acceleration { get; set; }
}