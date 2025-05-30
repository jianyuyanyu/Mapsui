﻿using Mapsui.Utilities;
using System;

namespace Mapsui;

/// <summary>
/// Class for a point in Mapsui
/// </summary>
public class MPoint : IEquatable<MPoint>
{
    public double X { get; set; }
    public double Y { get; set; }

    public MPoint() : this(0, 0) { }

    public MPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public MPoint((double x, double y) position)
    {
        X = position.x;
        Y = position.y;
    }

    public MPoint(MPoint point)
    {
        X = point.X;
        Y = point.Y;
    }

    public MPoint Copy()
    {
        return new MPoint(X, Y);
    }

    /// <summary>
    /// Calculate distance to a given point
    /// </summary>
    /// <param name="point">Point for calculating distance</param>
    /// <returns>Distance between this and given point</returns>
    public double Distance(MPoint point)
    {
        return Algorithms.Distance(X, Y, point.X, point.Y);
    }

    public bool Equals(MPoint? point)
    {
        if (point == null)
            return false;

        return X.Equals(point.X) && Y.Equals(point.Y);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (X.GetHashCode() * 397) ^ Y.GetHashCode();
        }
    }

    /// <summary>
    /// Create a new MPoint with an offset (x, y) to the original one
    /// </summary>
    /// <param name="offsetX">Offset in X direction</param>
    /// <param name="offsetY">Offset in Y direction</param>
    /// <returns></returns>
    public MPoint Offset(double offsetX, double offsetY)
    {
        return new MPoint(X + offsetX, Y + offsetY);
    }

    /// <summary>
    /// Calculates a new point by rotating this point clockwise about the specified center point
    /// </summary>
    /// <param name="degrees">Angle to rotate clockwise (degrees)</param>
    /// <param name="centerX">X coordinate of point about which to rotate</param>
    /// <param name="centerY">Y coordinate of point about which to rotate</param>
    /// <returns>Returns the rotated point</returns>
    public MPoint Rotate(double degrees, double centerX, double centerY)
    {
        // translate this point back to the center
        var newX = X - centerX;
        var newY = Y - centerY;

        // rotate the values
        var p = Algorithms.RotateClockwiseDegrees(newX, newY, degrees);

        // translate back to original reference frame
        newX = p.X + centerX;
        newY = p.Y + centerY;

        return new MPoint(newX, newY);
    }

    /// <summary>
    ///     Calculates a new point by rotating this point clockwise about the specified center point
    /// </summary>
    /// <param name="degrees">Angle to rotate clockwise (degrees)</param>
    /// <param name="center">MPoint about which to rotate</param>
    /// <returns>Returns the rotated point</returns>
    public MPoint Rotate(double degrees, MPoint center)
    {
        return Rotate(degrees, center.X, center.Y);
    }

    /// <summary>
    ///     Calculates a new point by rotating this point clockwise about the origin (0,0)
    /// </summary>
    /// <param name="degrees">Angle to rotate clockwise (degrees)</param>
    /// <returns>Returns the rotated point</returns>
    public MPoint Rotate(double degrees)
    {
        // rotate the values
        return Algorithms.RotateClockwiseDegrees(X, Y, degrees);
    }

    public override string ToString() => $"(X={X},Y={Y})";

    public static MPoint operator +(MPoint point1, MPoint point2)
    {
        return new MPoint(point1.X + point2.X, point1.Y + point2.Y);
    }

    public static MPoint operator -(MPoint point1, MPoint point2)
    {
        return new MPoint(point1.X - point2.X, point1.Y - point2.Y);
    }

    public static MPoint operator *(MPoint point1, double multiplier)
    {
        return new MPoint(point1.X * multiplier, point1.Y * multiplier);
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

        return Equals((MPoint)obj);
    }

    public static bool operator ==(MPoint? left, MPoint? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MPoint? left, MPoint? right)
    {
        return !Equals(left, right);
    }
}
