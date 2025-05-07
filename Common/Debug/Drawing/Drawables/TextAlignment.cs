namespace Tyr.Common.Debug.Drawing.Drawables;

/// <summary>
/// Defines text alignment options that can be combined using bitwise operations.
/// </summary>
[Flags]
public enum TextAlignment
{
    /// <summary>Align text to the left.</summary>
    Left = 1 << 0, // 1

    /// <summary>Align text to the right.</summary>
    Right = 1 << 1, // 2

    /// <summary>Center text horizontally.</summary>
    HCenter = 1 << 2, // 4

    /// <summary>Align text to the top.</summary>
    Top = 1 << 3, // 8

    /// <summary>Align text to the bottom.</summary>
    Bottom = 1 << 4, // 16

    /// <summary>Center text vertically.</summary>
    VCenter = 1 << 5, // 32

    // Common combinations
    /// <summary>Align text to the top-left corner.</summary>
    TopLeft = Top | Left,

    /// <summary>Align text to the top-right corner.</summary>
    TopRight = Top | Right,

    /// <summary>Align text to the bottom-left corner.</summary>
    BottomLeft = Bottom | Left,

    /// <summary>Align text to the bottom-right corner.</summary>
    BottomRight = Bottom | Right,

    /// <summary>Center text both horizontally and vertically.</summary>
    Center = HCenter | VCenter,

    /// <summary>Align text to the top and center horizontally.</summary>
    TopCenter = Top | HCenter,

    /// <summary>Align text to the bottom and center horizontally.</summary>
    BottomCenter = Bottom | HCenter,

    /// <summary>Align text to the left and center vertically.</summary>
    MiddleLeft = Left | VCenter,

    /// <summary>Align text to the right and center vertically.</summary>
    MiddleRight = Right | VCenter,
}