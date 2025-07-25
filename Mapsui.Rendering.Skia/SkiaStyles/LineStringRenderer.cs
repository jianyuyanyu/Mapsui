using Mapsui.Extensions;
using Mapsui.Rendering.Skia.Extensions;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using SkiaSharp;

namespace Mapsui.Rendering.Skia;

public static class LineStringRenderer
{
    public static void Draw(SKCanvas canvas, Viewport viewport, VectorStyle? vectorStyle,
        IFeature feature, LineString lineString, float opacity, RenderService renderService, int position)
    {
        if (vectorStyle == null)
            return;

        // lineString - relevant for GeometryCollection children
        SKPath ToPath((long featureId, int position, MRect extent, double rotation, float lineWidth) valueTuple)
        {
            var result = lineString.ToSkiaPath(viewport, viewport.ToSkiaRect(), valueTuple.lineWidth);
            _ = result.Bounds;
            _ = result.TightBounds;
            return result;
        }

        var extent = viewport.ToExtent();
        var rotation = viewport.Rotation;
        var lineWidth = (float)(vectorStyle.Line?.Width ?? 1f);

        if (vectorStyle.Line.IsVisible())
        {
            using var path = renderService.VectorCache.GetOrCreate((feature.Id, position, extent, rotation, lineWidth), ToPath);

            // If the Outline property is set and has a width greater than 0, draw the outline first.
            if (vectorStyle.Outline?.Width > 0)
            {
                // The width is calculated as the sum of the outline width and the line width, if both are defined.
                // For the caching callback to work, the calculated width must be passed to the CreateSkPaint method.
                var width = vectorStyle.Outline.Width + vectorStyle.Outline.Width + vectorStyle.Line?.Width ?? 1;
                using var paintOutline = renderService.VectorCache.GetOrCreate((vectorStyle.Outline, (float?)width, opacity), CreateSkPaint);
                canvas.DrawPath(path, paintOutline);
            }

            using var paintLine = renderService.VectorCache.GetOrCreate((vectorStyle.Line, (float?)null, opacity), CreateSkPaint);
            canvas.DrawPath(path, paintLine);
        }
    }

    private static SKPaint CreateSkPaint((Pen? pen, float? width, float opacity) valueTuple)
    {
        var pen = valueTuple.pen;
        var opacity = valueTuple.opacity;

        float lineWidth = valueTuple.width ?? 1;
        var lineColor = new Color();

        var strokeCap = PenStrokeCap.Butt;
        var strokeJoin = StrokeJoin.Miter;
        var strokeMiterLimit = 4f;
        var strokeStyle = PenStyle.Solid;
        float[]? dashArray = null;
        float dashOffset = 0;

        if (pen != null)
        {
            lineWidth = valueTuple.width ?? (float)pen.Width;
            lineColor = pen.Color;
            strokeCap = pen.PenStrokeCap;
            strokeJoin = pen.StrokeJoin;
            strokeMiterLimit = pen.StrokeMiterLimit;
            strokeStyle = pen.PenStyle;
            dashArray = pen.DashArray;
            dashOffset = pen.DashOffset;
        }

        var paint = new SKPaint { IsAntialias = true };
        paint.IsStroke = true;
        paint.StrokeWidth = lineWidth;
        paint.Color = lineColor.ToSkia(opacity);
        paint.StrokeCap = strokeCap.ToSkia();
        paint.StrokeJoin = strokeJoin.ToSkia();
        paint.StrokeMiter = strokeMiterLimit;
        paint.PathEffect = strokeStyle != PenStyle.Solid
            ? strokeStyle.ToSkia(lineWidth, dashArray, dashOffset)
            : null;
        return paint;
    }
}
