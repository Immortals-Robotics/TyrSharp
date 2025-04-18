using System.Numerics;
using Tyr.Common.Math;

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

    public Vector2 ScreenOffset
    {
        get => _screenOffset;
        set
        {
            _screenOffset = value;
            _dirty = true;
        }
    }

    private bool _dirty = false;

    private Vector2 _position = Vector2.Zero;
    private float _zoom = 1f;
    private Angle _rotation = Angle.FromDeg(0f);
    private Vector2 _screenOffset = Vector2.Zero;

    private Matrix3x2 _matrix;
    private Matrix3x2 _inverse;

    private void UpdateMatrix()
    {
        // World → Screen
        var translateToOrigin = Matrix3x2.CreateTranslation(-Position);
        var scale = Matrix3x2.CreateScale(Zoom);
        var rotate = Matrix3x2.CreateRotation(Rotation.Rad);
        var translateToScreen = Matrix3x2.CreateTranslation(ScreenOffset);

        _matrix = translateToOrigin * rotate * scale * translateToScreen;

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
}