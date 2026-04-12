using System.Text.Json.Serialization;

namespace AssemblyEngine.Core;

/// <summary>
/// Color represented as RGBA bytes.
/// </summary>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Red = new(255, 0, 0);
    public static readonly Color Green = new(0, 255, 0);
    public static readonly Color Blue = new(0, 0, 255);
    public static readonly Color Yellow = new(255, 255, 0);
    public static readonly Color Cyan = new(0, 255, 255);
    public static readonly Color Magenta = new(255, 0, 255);
    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color CornflowerBlue = new(100, 149, 237);

    public static Color FromHex(string hex)
    {
        var span = hex.AsSpan();
        if (span.Length > 0 && span[0] == '#')
            span = span[1..];

        return span.Length switch
        {
            6 => new Color(
                Convert.ToByte(span[..2].ToString(), 16),
                Convert.ToByte(span[2..4].ToString(), 16),
                Convert.ToByte(span[4..6].ToString(), 16)),
            8 => new Color(
                Convert.ToByte(span[..2].ToString(), 16),
                Convert.ToByte(span[2..4].ToString(), 16),
                Convert.ToByte(span[4..6].ToString(), 16),
                Convert.ToByte(span[6..8].ToString(), 16)),
            _ => Black
        };
    }
}

/// <summary>
/// 2D integer vector / point.
/// </summary>
public record struct Vector2(float X, float Y)
{
    public static readonly Vector2 Zero = new(0, 0);
    public static readonly Vector2 One = new(1, 1);
    public static readonly Vector2 Up = new(0, -1);
    public static readonly Vector2 Down = new(0, 1);
    public static readonly Vector2 Left = new(-1, 0);
    public static readonly Vector2 Right = new(1, 0);

    [JsonIgnore] public readonly float LengthSquared => X * X + Y * Y;
    [JsonIgnore] public readonly float Length => MathF.Sqrt(LengthSquared);

    [JsonIgnore]
    public readonly Vector2 Normalized
    {
        get
        {
            var len = Length;
            return len > 0 ? new Vector2(X / len, Y / len) : Zero;
        }
    }

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, float s) => new(a.X * s, a.Y * s);
    public static Vector2 operator *(float s, Vector2 a) => new(a.X * s, a.Y * s);

    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    public static float Distance(Vector2 a, Vector2 b) => (a - b).Length;

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}

/// <summary>
/// Axis-aligned bounding box for 2D collision.
/// </summary>
public record struct Rectangle(float X, float Y, float Width, float Height)
{
    [JsonIgnore] public readonly float Right => X + Width;
    [JsonIgnore] public readonly float Bottom => Y + Height;
    [JsonIgnore] public readonly Vector2 Center => new(X + Width / 2, Y + Height / 2);

    public readonly bool Contains(Vector2 point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    public readonly bool Intersects(Rectangle other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}
