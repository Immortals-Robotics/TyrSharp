using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Shape;

namespace Tyr.Gui;

public class Camera2D
{
    public Vector2 Position
    {
        get => _position;
        set
        {
            _position = value;
            _dirty = true;
        }
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = value;
            _dirty = true;
        }
    }

    public Angle Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            _dirty = true;
        }
    }

    public Viewport Viewport
    {
        get => _viewport;
        set
        {
            _viewport = value;
            _dirty = true;
        }
    }

    private bool _dirty = false;

    private Vector2 _position = Vector2.Zero;
    private float _zoom = 1f;
    private Angle _rotation = Angle.FromDeg(0f);
    private Viewport _viewport = new(Vector2.Zero, Vector2.Zero);

    private Matrix3x2 _matrix;
    private Matrix3x2 _inverse;

    private void UpdateMatrix()
    {
        // World → Screen
        var translateToOrigin = Matrix3x2.CreateTranslation(-_position);
        var rotate = Matrix3x2.CreateRotation(_rotation.Rad);
        var scale = Matrix3x2.CreateScale(_zoom, -_zoom); // Flip Y axis

        // Offset to screen center of the viewport
        var translateToViewportCenter = Matrix3x2.CreateTranslation(
            _viewport.Offset + _viewport.Size * 0.5f
        );

        _matrix = translateToOrigin * rotate * scale * translateToViewportCenter;

        // Precompute inverse for picking
        Matrix3x2.Invert(_matrix, out _inverse);

        _dirty = false;
    }

    // for drawing
    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        if (_dirty) UpdateMatrix();

        return Vector2.Transform(worldPos, _matrix);
    }

    // for picking
    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        if (_dirty) UpdateMatrix();

        return Vector2.Transform(screenPos, _inverse);
    }

    // Scale a size, thickness, or radius from world to screen
    public float WorldToScreenLength(float worldLength)
    {
        if (_dirty) UpdateMatrix();

        return Vector2.TransformNormal(new Vector2(worldLength, 0), _matrix).Length();
    }

    public float ScreenToWorldLength(float screenLength)
    {
        if (_dirty) UpdateMatrix();

        return Vector2.TransformNormal(new Vector2(screenLength, 0), _inverse).Length();
    }

    public Rect GetVisibleWorldBounds()
    {
        if (_dirty) UpdateMatrix();

        var corners = new[]
        {
            new Vector2(0, 0),
            new Vector2(_viewport.Size.X, 0),
            new Vector2(0, _viewport.Size.Y),
            new Vector2(_viewport.Size.X, _viewport.Size.Y)
        };

        var worldCorners = corners.Select(ScreenToWorld).ToArray();
        var min = Vector2.Min(Vector2.Min(worldCorners[0], worldCorners[1]),
            Vector2.Min(worldCorners[2], worldCorners[3]));
        var max = Vector2.Max(Vector2.Max(worldCorners[0], worldCorners[1]),
            Vector2.Max(worldCorners[2], worldCorners[3]));

        return new Rect(min, max);
    }
}