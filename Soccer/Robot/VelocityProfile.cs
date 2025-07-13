using Tyr.Common.Config;

namespace Tyr.Soccer.Robot;

[Configurable]
public readonly record struct VelocityProfile()
{
    [ConfigEntry] public static VelocityProfile Sooski { get; set; } = new VelocityProfile 
    { 
        Speed = 450.0f,
        Acceleration = 1620.0f 
    };
    
    [ConfigEntry] public static VelocityProfile Aroom { get; set; } = new VelocityProfile 
    { 
        Speed = 900.0f,
        Acceleration = 2160.0f 
    };
    
    [ConfigEntry] public static VelocityProfile Mamooli { get; set; } = new VelocityProfile 
    { 
        Speed = 3000.0f,
        Acceleration = 3500.0f 
    };
    
    [ConfigEntry] public static VelocityProfile Kharaki { get; set; } = new VelocityProfile 
    { 
        Speed = 3500.0f,
        Acceleration = 4010.0f 
    };


    public float Speed { get; init; }
    public float Acceleration { get; init; }
}