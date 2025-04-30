using System.Globalization;
using System.Numerics;

namespace Tyr.Common.Debug.Drawing;

public readonly record struct Color
{
    // ReSharper disable once InconsistentNaming
    public Vector4 RGBA { get; }

    // ReSharper disable once InconsistentNaming
    public uint ABGR32 { get; }

    public float R => RGBA.X;
    public float G => RGBA.Y;
    public float B => RGBA.Z;
    public float A => RGBA.W;

    private Color(float r, float g, float b, float a = 1f)
    {
        RGBA = new Vector4(r, g, b, a);

        var ua = (uint)(a * 255.0f);
        var ub = (uint)(b * 255.0f);
        var ug = (uint)(g * 255.0f);
        var ur = (uint)(r * 255.0f);

        ABGR32 = (ua << 24) | (ub << 16) | (ug << 8) | ur;
    }

    private static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Hex color string can't be empty", nameof(hex));

        var span = hex.StartsWith('#') ? hex.AsSpan(1) : hex.AsSpan(0);

        if (span.Length is not (6 or 8))
            throw new ArgumentException("Hex color string must be 6 or 8 characters long after the # character",
                nameof(hex));

        var r = byte.Parse(span[0..2], NumberStyles.HexNumber) / 255f;
        var g = byte.Parse(span[2..4], NumberStyles.HexNumber) / 255f;
        var b = byte.Parse(span[4..6], NumberStyles.HexNumber) / 255f;
        var a = span.Length == 8 ? byte.Parse(span[6..8], NumberStyles.HexNumber) / 255f : 1f;

        return new Color(r, g, b, a);
    }

    public Color WithAlpha(float alpha) => new Color(R, G, B, alpha);

    public override int GetHashCode() => (int)ABGR32;
    public bool Equals(Color other) => ABGR32 == other.ABGR32;

    public static implicit operator Vector4(Color color) => color.RGBA;

    public static readonly Color Invisible = Grey.WithAlpha(0f);

    public static readonly Color White = FromHex("#ffffff");
    public static readonly Color Black = FromHex("#000000");

    // red
    public static Color Red => Red500;
    public static readonly Color Red50 = FromHex("#ffebee");
    public static readonly Color Red100 = FromHex("#ffcdd2");
    public static readonly Color Red200 = FromHex("#ef9a9a");
    public static readonly Color Red300 = FromHex("#e57373");
    public static readonly Color Red400 = FromHex("#ef5350");
    public static readonly Color Red500 = FromHex("#f44336");
    public static readonly Color Red600 = FromHex("#e53935");
    public static readonly Color Red700 = FromHex("#d32f2f");
    public static readonly Color Red800 = FromHex("#c62828");
    public static readonly Color Red900 = FromHex("#b71c1c");
    public static readonly Color RedA100 = FromHex("#ff8a80");
    public static readonly Color RedA200 = FromHex("#ff5252");
    public static readonly Color RedA400 = FromHex("#ff1744");
    public static readonly Color RedA700 = FromHex("#d50000");

    // pink
    public static Color Pink => Pink500;
    public static readonly Color Pink50 = FromHex("#fce4ec");
    public static readonly Color Pink100 = FromHex("#f8bbd0");
    public static readonly Color Pink200 = FromHex("#f48fb1");
    public static readonly Color Pink300 = FromHex("#f06292");
    public static readonly Color Pink400 = FromHex("#ec407a");
    public static readonly Color Pink500 = FromHex("#e91e63");
    public static readonly Color Pink600 = FromHex("#d81b60");
    public static readonly Color Pink700 = FromHex("#c2185b");
    public static readonly Color Pink800 = FromHex("#ad1457");
    public static readonly Color Pink900 = FromHex("#880e4f");
    public static readonly Color PinkA100 = FromHex("#ff80ab");
    public static readonly Color PinkA200 = FromHex("#ff4081");
    public static readonly Color PinkA400 = FromHex("#f50057");
    public static readonly Color PinkA700 = FromHex("#c51162");

    // purple
    public static Color Purple => Purple500;
    public static readonly Color Purple50 = FromHex("#f3e5f5");
    public static readonly Color Purple100 = FromHex("#e1bee7");
    public static readonly Color Purple200 = FromHex("#ce93d8");
    public static readonly Color Purple300 = FromHex("#ba68c8");
    public static readonly Color Purple400 = FromHex("#ab47bc");
    public static readonly Color Purple500 = FromHex("#9c27b0");
    public static readonly Color Purple600 = FromHex("#8e24aa");
    public static readonly Color Purple700 = FromHex("#7b1fa2");
    public static readonly Color Purple800 = FromHex("#6a1b9a");
    public static readonly Color Purple900 = FromHex("#4a148c");
    public static readonly Color PurpleA100 = FromHex("#ea80fc");
    public static readonly Color PurpleA200 = FromHex("#e040fb");
    public static readonly Color PurpleA400 = FromHex("#d500f9");
    public static readonly Color PurpleA700 = FromHex("#aa00ff");

    // deep purple
    public static Color DeepPurple => DeepPurple500;
    public static readonly Color DeepPurple50 = FromHex("#ede7f6");
    public static readonly Color DeepPurple100 = FromHex("#d1c4e9");
    public static readonly Color DeepPurple200 = FromHex("#b39ddb");
    public static readonly Color DeepPurple300 = FromHex("#9575cd");
    public static readonly Color DeepPurple400 = FromHex("#7e57c2");
    public static readonly Color DeepPurple500 = FromHex("#673ab7");
    public static readonly Color DeepPurple600 = FromHex("#5e35b1");
    public static readonly Color DeepPurple700 = FromHex("#512da8");
    public static readonly Color DeepPurple800 = FromHex("#4527a0");
    public static readonly Color DeepPurple900 = FromHex("#311b92");
    public static readonly Color DeepPurpleA100 = FromHex("#b388ff");
    public static readonly Color DeepPurpleA200 = FromHex("#7c4dff");
    public static readonly Color DeepPurpleA400 = FromHex("#651fff");
    public static readonly Color DeepPurpleA700 = FromHex("#6200ea");

    // indigo
    public static Color Indigo => Indigo500;
    public static readonly Color Indigo50 = FromHex("#e8eaf6");
    public static readonly Color Indigo100 = FromHex("#c5cae9");
    public static readonly Color Indigo200 = FromHex("#9fa8da");
    public static readonly Color Indigo300 = FromHex("#7986cb");
    public static readonly Color Indigo400 = FromHex("#5c6bc0");
    public static readonly Color Indigo500 = FromHex("#3f51b5");
    public static readonly Color Indigo600 = FromHex("#3949ab");
    public static readonly Color Indigo700 = FromHex("#303f9f");
    public static readonly Color Indigo800 = FromHex("#283593");
    public static readonly Color Indigo900 = FromHex("#1a237e");
    public static readonly Color IndigoA100 = FromHex("#8c9eff");
    public static readonly Color IndigoA200 = FromHex("#536dfe");
    public static readonly Color IndigoA400 = FromHex("#3d5afe");
    public static readonly Color IndigoA700 = FromHex("#304ffe");

    // blue
    public static Color Blue => Blue500;
    public static readonly Color Blue50 = FromHex("#e3f2fd");
    public static readonly Color Blue100 = FromHex("#bbdefb");
    public static readonly Color Blue200 = FromHex("#90caf9");
    public static readonly Color Blue300 = FromHex("#64b5f6");
    public static readonly Color Blue400 = FromHex("#42a5f5");
    public static readonly Color Blue500 = FromHex("#2196f3");
    public static readonly Color Blue600 = FromHex("#1e88e5");
    public static readonly Color Blue700 = FromHex("#1976d2");
    public static readonly Color Blue800 = FromHex("#1565c0");
    public static readonly Color Blue900 = FromHex("#0d47a1");
    public static readonly Color BlueA100 = FromHex("#82b1ff");
    public static readonly Color BlueA200 = FromHex("#448aff");
    public static readonly Color BlueA400 = FromHex("#2979ff");
    public static readonly Color BlueA700 = FromHex("#2962ff");

    // light blue
    public static Color LightBlue => LightBlue500;
    public static readonly Color LightBlue50 = FromHex("#e1f5fe");
    public static readonly Color LightBlue100 = FromHex("#b3e5fc");
    public static readonly Color LightBlue200 = FromHex("#81d4fa");
    public static readonly Color LightBlue300 = FromHex("#4fc3f7");
    public static readonly Color LightBlue400 = FromHex("#29b6f6");
    public static readonly Color LightBlue500 = FromHex("#03a9f4");
    public static readonly Color LightBlue600 = FromHex("#039be5");
    public static readonly Color LightBlue700 = FromHex("#0288d1");
    public static readonly Color LightBlue800 = FromHex("#0277bd");
    public static readonly Color LightBlue900 = FromHex("#01579b");
    public static readonly Color LightBlueA100 = FromHex("#80d8ff");
    public static readonly Color LightBlueA200 = FromHex("#40c4ff");
    public static readonly Color LightBlueA400 = FromHex("#00b0ff");
    public static readonly Color LightBlueA700 = FromHex("#0091ea");

    // cyan
    public static Color Cyan => Cyan500;
    public static readonly Color Cyan50 = FromHex("#e0f7fa");
    public static readonly Color Cyan100 = FromHex("#b2ebf2");
    public static readonly Color Cyan200 = FromHex("#80deea");
    public static readonly Color Cyan300 = FromHex("#4dd0e1");
    public static readonly Color Cyan400 = FromHex("#26c6da");
    public static readonly Color Cyan500 = FromHex("#00bcd4");
    public static readonly Color Cyan600 = FromHex("#00acc1");
    public static readonly Color Cyan700 = FromHex("#0097a7");
    public static readonly Color Cyan800 = FromHex("#00838f");
    public static readonly Color Cyan900 = FromHex("#006064");
    public static readonly Color CyanA100 = FromHex("#84ffff");
    public static readonly Color CyanA200 = FromHex("#18ffff");
    public static readonly Color CyanA400 = FromHex("#00e5ff");
    public static readonly Color CyanA700 = FromHex("#00b8d4");

    // teal
    public static Color Teal => Teal500;
    public static readonly Color Teal50 = FromHex("#e0f2f1");
    public static readonly Color Teal100 = FromHex("#b2dfdb");
    public static readonly Color Teal200 = FromHex("#80cbc4");
    public static readonly Color Teal300 = FromHex("#4db6ac");
    public static readonly Color Teal400 = FromHex("#26a69a");
    public static readonly Color Teal500 = FromHex("#009688");
    public static readonly Color Teal600 = FromHex("#00897b");
    public static readonly Color Teal700 = FromHex("#00796b");
    public static readonly Color Teal800 = FromHex("#00695c");
    public static readonly Color Teal900 = FromHex("#004d40");
    public static readonly Color TealA100 = FromHex("#a7ffeb");
    public static readonly Color TealA200 = FromHex("#64ffda");
    public static readonly Color TealA400 = FromHex("#1de9b6");
    public static readonly Color TealA700 = FromHex("#00bfa5");

    // green
    public static Color Green => Green500;
    public static readonly Color Green50 = FromHex("#e8f5e9");
    public static readonly Color Green100 = FromHex("#c8e6c9");
    public static readonly Color Green200 = FromHex("#a5d6a7");
    public static readonly Color Green300 = FromHex("#81c784");
    public static readonly Color Green400 = FromHex("#66bb6a");
    public static readonly Color Green500 = FromHex("#4caf50");
    public static readonly Color Green600 = FromHex("#43a047");
    public static readonly Color Green700 = FromHex("#388e3c");
    public static readonly Color Green800 = FromHex("#2e7d32");
    public static readonly Color Green900 = FromHex("#1b5e20");
    public static readonly Color GreenA100 = FromHex("#b9f6ca");
    public static readonly Color GreenA200 = FromHex("#69f0ae");
    public static readonly Color GreenA400 = FromHex("#00e676");
    public static readonly Color GreenA700 = FromHex("#00c853");

    // light green
    public static Color LightGreen => LightGreen500;
    public static readonly Color LightGreen50 = FromHex("#f1f8e9");
    public static readonly Color LightGreen100 = FromHex("#dcedc8");
    public static readonly Color LightGreen200 = FromHex("#c5e1a5");
    public static readonly Color LightGreen300 = FromHex("#aed581");
    public static readonly Color LightGreen400 = FromHex("#9ccc65");
    public static readonly Color LightGreen500 = FromHex("#8bc34a");
    public static readonly Color LightGreen600 = FromHex("#7cb342");
    public static readonly Color LightGreen700 = FromHex("#689f38");
    public static readonly Color LightGreen800 = FromHex("#558b2f");
    public static readonly Color LightGreen900 = FromHex("#33691e");
    public static readonly Color LightGreenA100 = FromHex("#ccff90");
    public static readonly Color LightGreenA200 = FromHex("#b2ff59");
    public static readonly Color LightGreenA400 = FromHex("#76ff03");
    public static readonly Color LightGreenA700 = FromHex("#64dd17");

    // lime
    public static Color Lime => Lime500;
    public static readonly Color Lime50 = FromHex("#f9fbe7");
    public static readonly Color Lime100 = FromHex("#f0f4c3");
    public static readonly Color Lime200 = FromHex("#e6ee9c");
    public static readonly Color Lime300 = FromHex("#dce775");
    public static readonly Color Lime400 = FromHex("#d4e157");
    public static readonly Color Lime500 = FromHex("#cddc39");
    public static readonly Color Lime600 = FromHex("#c0ca33");
    public static readonly Color Lime700 = FromHex("#afb42b");
    public static readonly Color Lime800 = FromHex("#9e9d24");
    public static readonly Color Lime900 = FromHex("#827717");
    public static readonly Color LimeA100 = FromHex("#f4ff81");
    public static readonly Color LimeA200 = FromHex("#eeff41");
    public static readonly Color LimeA400 = FromHex("#c6ff00");
    public static readonly Color LimeA700 = FromHex("#aeea00");

    // yellow
    public static Color Yellow => Yellow500;
    public static readonly Color Yellow50 = FromHex("#fffde7");
    public static readonly Color Yellow100 = FromHex("#fff9c4");
    public static readonly Color Yellow200 = FromHex("#fff59d");
    public static readonly Color Yellow300 = FromHex("#fff176");
    public static readonly Color Yellow400 = FromHex("#ffee58");
    public static readonly Color Yellow500 = FromHex("#ffeb3b");
    public static readonly Color Yellow600 = FromHex("#fdd835");
    public static readonly Color Yellow700 = FromHex("#fbc02d");
    public static readonly Color Yellow800 = FromHex("#f9a825");
    public static readonly Color Yellow900 = FromHex("#f57f17");
    public static readonly Color YellowA100 = FromHex("#ffff8d");
    public static readonly Color YellowA200 = FromHex("#ffff00");
    public static readonly Color YellowA400 = FromHex("#ffea00");
    public static readonly Color YellowA700 = FromHex("#ffd600");

    // amber
    public static Color Amber => Amber500;
    public static readonly Color Amber50 = FromHex("#fff8e1");
    public static readonly Color Amber100 = FromHex("#ffecb3");
    public static readonly Color Amber200 = FromHex("#ffe082");
    public static readonly Color Amber300 = FromHex("#ffd54f");
    public static readonly Color Amber400 = FromHex("#ffca28");
    public static readonly Color Amber500 = FromHex("#ffc107");
    public static readonly Color Amber600 = FromHex("#ffb300");
    public static readonly Color Amber700 = FromHex("#ffa000");
    public static readonly Color Amber800 = FromHex("#ff8f00");
    public static readonly Color Amber900 = FromHex("#ff6f00");
    public static readonly Color AmberA100 = FromHex("#ffe57f");
    public static readonly Color AmberA200 = FromHex("#ffd740");
    public static readonly Color AmberA400 = FromHex("#ffc400");
    public static readonly Color AmberA700 = FromHex("#ffab00");

    // orange
    public static Color Orange => Orange500;
    public static readonly Color Orange50 = FromHex("#fff3e0");
    public static readonly Color Orange100 = FromHex("#ffe0b2");
    public static readonly Color Orange200 = FromHex("#ffcc80");
    public static readonly Color Orange300 = FromHex("#ffb74d");
    public static readonly Color Orange400 = FromHex("#ffa726");
    public static readonly Color Orange500 = FromHex("#ff9800");
    public static readonly Color Orange600 = FromHex("#fb8c00");
    public static readonly Color Orange700 = FromHex("#f57c00");
    public static readonly Color Orange800 = FromHex("#ef6c00");
    public static readonly Color Orange900 = FromHex("#e65100");
    public static readonly Color OrangeA100 = FromHex("#ffd180");
    public static readonly Color OrangeA200 = FromHex("#ffab40");
    public static readonly Color OrangeA400 = FromHex("#ff9100");
    public static readonly Color OrangeA700 = FromHex("#ff6d00");

    // deep orange
    public static Color DeepOrange => DeepOrange500;
    public static readonly Color DeepOrange50 = FromHex("#fbe9e7");
    public static readonly Color DeepOrange100 = FromHex("#ffccbc");
    public static readonly Color DeepOrange200 = FromHex("#ffab91");
    public static readonly Color DeepOrange300 = FromHex("#ff8a65");
    public static readonly Color DeepOrange400 = FromHex("#ff7043");
    public static readonly Color DeepOrange500 = FromHex("#ff5722");
    public static readonly Color DeepOrange600 = FromHex("#f4511e");
    public static readonly Color DeepOrange700 = FromHex("#e64a19");
    public static readonly Color DeepOrange800 = FromHex("#d84315");
    public static readonly Color DeepOrange900 = FromHex("#bf360c");
    public static readonly Color DeepOrangeA100 = FromHex("#ff9e80");
    public static readonly Color DeepOrangeA200 = FromHex("#ff6e40");
    public static readonly Color DeepOrangeA400 = FromHex("#ff3d00");
    public static readonly Color DeepOrangeA700 = FromHex("#dd2c00");

    // brown
    public static Color Brown => Brown500;
    public static readonly Color Brown50 = FromHex("#efebe9");
    public static readonly Color Brown100 = FromHex("#d7ccc8");
    public static readonly Color Brown200 = FromHex("#bcaaa4");
    public static readonly Color Brown300 = FromHex("#a1887f");
    public static readonly Color Brown400 = FromHex("#8d6e63");
    public static readonly Color Brown500 = FromHex("#795548");
    public static readonly Color Brown600 = FromHex("#6d4c41");
    public static readonly Color Brown700 = FromHex("#5d4037");
    public static readonly Color Brown800 = FromHex("#4e342e");
    public static readonly Color Brown900 = FromHex("#3e2723");

    // grey
    public static Color Grey => Grey500;
    public static readonly Color Grey50 = FromHex("#fafafa");
    public static readonly Color Grey100 = FromHex("#f5f5f5");
    public static readonly Color Grey200 = FromHex("#eeeeee");
    public static readonly Color Grey300 = FromHex("#e0e0e0");
    public static readonly Color Grey400 = FromHex("#bdbdbd");
    public static readonly Color Grey500 = FromHex("#9e9e9e");
    public static readonly Color Grey600 = FromHex("#757575");
    public static readonly Color Grey700 = FromHex("#616161");
    public static readonly Color Grey800 = FromHex("#424242");
    public static readonly Color Grey900 = FromHex("#212121");

    // blue grey
    public static Color BlueGrey => BlueGrey500;
    public static readonly Color BlueGrey50 = FromHex("#eceff1");
    public static readonly Color BlueGrey100 = FromHex("#cfd8dc");
    public static readonly Color BlueGrey200 = FromHex("#b0bec5");
    public static readonly Color BlueGrey300 = FromHex("#90a4ae");
    public static readonly Color BlueGrey400 = FromHex("#78909c");
    public static readonly Color BlueGrey500 = FromHex("#607d8b");
    public static readonly Color BlueGrey600 = FromHex("#546e7a");
    public static readonly Color BlueGrey700 = FromHex("#455a64");
    public static readonly Color BlueGrey800 = FromHex("#37474f");
    public static readonly Color BlueGrey900 = FromHex("#263238");

    /*public static Color Random()
    {
        ReadOnlySpan<Color> colors = [White, Black, Red, Green, Blue, Yellow, Orange, Magenta];
        return Rand.Get(colors);
    }*/
}