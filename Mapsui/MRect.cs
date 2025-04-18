﻿using System;
using System.Collections.Generic;

namespace Mapsui;

public class MRect : IEquatable<MRect>
{
    public MPoint Max { get; }
    public MPoint Min { get; }
    public MPoint Centroid { get; }

    public double MaxX => Max.X;
    public double MaxY => Max.Y;
    public double MinX => Min.X;
    public double MinY => Min.Y;

    public double Width => Max.X - Min.X;
    public double Height => Max.Y - Min.Y;

    public double Bottom => Min.Y;
    public double Left => Min.X;
    public double Top => Max.Y;
    public double Right => Max.X;

    // Creates an MPoint with the coordinates of the top left corner of the rectangle
    public MPoint GetTopLeft() => new(Left, Top);
    // Creates an MPoint with the coordinates of the top right corner of the rectangle
    public MPoint GetTopRight() => new(Right, Top);
    // Creates an MPoint with the coordinates of the bottom left corner of the rectangle
    public MPoint GetBottomLeft() => new(Left, Bottom);
    // Creates an MPoint with the coordinates of the bottom right corner of the rectangle
    public MPoint GetBottomRight() => new(Right, Bottom);

    public MRect(double minX, double minY, double maxX, double maxY)
    {
        Min = new MPoint(minX, minY);
        Max = new MPoint(maxX, maxY);
        SwapMinAndMaxIfNeeded();
        Centroid = new(Min.X + Width * 0.5, Min.Y + Height * 0.5);
    }

    public MRect(double value) : this(value, value, value, value) { }

    public MRect(double x, double y) : this(x, y, x, y) { }

    public MRect(MRect rect) : this(rect.Min.X, rect.Min.Y, rect.Max.X, rect.Max.Y) { }

    public MRect(IEnumerable<MRect> rects)
    {
        foreach (var rect in rects)
        {
            Min ??= rect.Min.Copy();
            Max ??= rect.Max.Copy();

            Min.X = Math.Min(Min.X, rect.Min.X);
            Min.Y = Math.Min(Min.Y, rect.Min.Y);
            Max.X = Math.Max(Max.X, rect.Max.X);
            Max.Y = Math.Max(Max.Y, rect.Max.Y);
        }

        if (Min == null) throw new ArgumentException("Empty Collection", nameof(rects));
        if (Max == null) throw new ArgumentException("Empty Collection", nameof(rects));

        Centroid = new(Min.X + Width * 0.5, Min.Y + Height * 0.5);
    }

    /// <summary>
    ///     Returns the vertices in clockwise order from bottom left around to bottom right
    /// </summary>
    public IEnumerable<MPoint> Vertices
    {
        get
        {
            yield return GetBottomLeft();
            yield return GetTopLeft();
            yield return GetTopRight();
            yield return GetBottomRight();
        }
    }

    public MRect Copy()
    {
        return new MRect(Min.X, Min.Y, Max.X, Max.Y);
    }

    public bool Contains(double x, double y)
    {
        if (x < Min.X) return false;
        if (y < Min.Y) return false;
        if (x > Max.X) return false;
        if (y > Max.Y) return false;

        return true;
    }

    public bool Contains(MPoint? point)
    {
        if (point is null) return false;

        if (point.X < Min.X) return false;
        if (point.Y < Min.Y) return false;
        if (point.X > Max.X) return false;
        if (point.Y > Max.Y) return false;

        return true;
    }

    public bool Contains(MRect r)
    {
        return Min.X <= r.Min.X && Min.Y <= r.Min.Y && Max.X >= r.Max.X && Max.Y >= r.Max.Y;
    }

    public bool Equals(MRect? other)
    {
        if (other == null)
            return false;

        return Max.Equals(other.Max) && Min.Equals(other.Min);
    }

    public double GetArea()
    {
        return Width * Height;
    }

    public MRect Grow(double amount)
    {
        return Grow(amount, amount);
    }

    public MRect Grow(double amountInX, double amountInY)
    {
        var grownBox = new MRect(Min.X - amountInX, Min.Y - amountInY, Max.X + amountInX, MaxY + amountInY);
        grownBox.SwapMinAndMaxIfNeeded();
        return grownBox;
    }

    public bool Intersects(MRect? rect)
    {
        if (rect is null) return false;

        if (rect.Max.X <= Min.X) return false;
        if (rect.Max.Y <= Min.Y) return false;
        if (rect.Min.X >= Max.X) return false;
        if (rect.Min.Y >= Max.Y) return false;

        return true;
    }

    public MRect Join(MRect? rect)
    {
        if (rect is null) return Copy();

        return new MRect(
            Math.Min(Min.X, rect.Min.X),
            Math.Min(Min.Y, rect.Min.Y),
            Math.Max(Max.X, rect.Max.X),
            Math.Max(Max.Y, rect.Max.Y));
    }

    /// <summary>
    /// Adjusts the size by increasing Width and Heigh with (Width * Height) / 2 * factor.
    /// </summary>
    /// <param name="factor"></param>
    /// <returns></returns>
    public MRect Multiply(double factor)
    {
        if (factor < 0)
        {
            throw new ArgumentException($"{nameof(factor)} can not be smaller than zero");
        }

        var size = (Width + Height) * 0.5;
        var change = (size * 0.5 * factor) - (size * 0.5);
        var box = Copy();
        box.Min.X -= change;
        box.Min.Y -= change;
        box.Max.X += change;
        box.Max.Y += change;
        return box;
    }

    /// <summary>
    ///     Calculates a new quad by rotating this rect about its center by the
    ///     specified angle clockwise
    /// </summary>
    /// <param name="degrees">Angle about which to rotate (degrees)</param>
    /// <returns>Returns the calculated quad</returns>
    public MQuad Rotate(double degrees)
    {
        var bottomLeft = new MPoint(MinX, MinY);
        var topLeft = new MPoint(MinX, MaxY);
        var topRight = new MPoint(MaxX, MaxY);
        var bottomRight = new MPoint(MaxX, MinY);
        var quad = new MQuad(bottomLeft, topLeft, topRight, bottomRight);
        var center = Centroid;

        return quad.Rotate(degrees, center.X, center.Y);
    }

    /// <summary>
    ///     Returns a string representation of the vertices from bottom-left and top-right
    /// </summary>
    /// <returns>Returns the string</returns>
    public override string ToString()
    {
        return $"Min: {Min}  Max: {Max}";
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((MRect)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Max.GetHashCode() * 397) ^ Min.GetHashCode();
        }
    }

    public static bool operator ==(MRect? left, MRect? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MRect? left, MRect? right)
    {
        return !Equals(left, right);
    }

    private void SwapMinAndMaxIfNeeded()
    {
        if (Min.X > Max.X)
        {
            (Min.X, Max.X) = (Max.X, Min.X);
        }
        if (Min.Y > Max.Y)
        {
            (Min.Y, Max.Y) = (Max.Y, Min.Y);
        }
    }
}
