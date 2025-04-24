using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

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

    private bool _dirty;

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

    public Vector2 ScreenToWorldDirection(Vector2 screenDir)
    {
        if (_dirty) UpdateMatrix();

        return Vector2.TransformNormal(screenDir, _inverse);
    }

    public Vector2 WorldToScreenDirection(Vector2 worldDir)
    {
        if (_dirty) UpdateMatrix();

        return Vector2.TransformNormal(worldDir, _matrix);
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

    public Rectangle GetVisibleWorldBounds()
    {
        if (_dirty) UpdateMatrix();

        var corner1 = ScreenToWorld(Vector2.Zero);
        var corner2 = ScreenToWorld(_viewport.Size with { Y = 0 });
        var corner3 = ScreenToWorld(_viewport.Size with { X = 0 });
        var corner4 = ScreenToWorld(_viewport.Size);

        var min = Vector2.Min(
            Vector2.Min(corner1, corner2),
            Vector2.Min(corner3, corner4));

        var max = Vector2.Max(
            Vector2.Max(corner1, corner2),
            Vector2.Max(corner3, corner4));

        return new Rectangle(min, max);
    }
}