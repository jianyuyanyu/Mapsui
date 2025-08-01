﻿using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Rendering;
using Mapsui.Rendering.Skia;
using Mapsui.Rendering.Skia.SkiaStyles;
using Mapsui.Samples.Common.DataBuilders;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI;
using Mapsui.Widgets.InfoWidgets;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Mapsui.Samples.Common.Maps.Styles;

public class CustomStyle : BaseStyle
{
    public CustomStyle() => Opacity = 0.7f;
}

public class SkiaCustomStyleRenderer : ISkiaStyleRenderer
{
    public static Random Random = new(1);
    public bool Draw(SKCanvas canvas, Viewport viewport, ILayer layer, IFeature feature, IStyle style, RenderService renderService, long iteration)
    {
        if (feature is not PointFeature pointFeature) return false;
        var worldPoint = pointFeature.Point;

        var screenPoint = viewport.WorldToScreen(worldPoint);
        var color = new SKColor((byte)Random.Next(0, 256), (byte)Random.Next(0, 256), (byte)Random.Next(0, 256), (byte)(256.0 * layer.Opacity * style.Opacity));
        using var colored = new SKPaint { Color = color, IsAntialias = true };
        using var black = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        canvas.Translate((float)screenPoint.X, (float)screenPoint.Y);
        canvas.DrawCircle(0, 0, 15, colored);
        canvas.DrawCircle(-8, -12, 8, colored);
        canvas.DrawCircle(8, -12, 8, colored);
        canvas.DrawCircle(8, -8, 2, black);
        canvas.DrawCircle(-8, -8, 2, black);

        using var path = new SKPath();
        path.ArcTo(new SKRect(-8, 2, 8, 10), 25, 135, true);
        using var skPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Black, IsAntialias = true };
        canvas.DrawPath(path, skPaint);

        return true;
    }
}

public class CustomStyleSample : IMapControlSample
{
    public string Name => "Custom Style";
    public string Category => "Styles";

    private const string _mapInfoLayerName = "Custom Style Layer";

    public void Setup(IMapControl mapControl)
    {
        mapControl.Map = CreateMap();
    }

    public static Map CreateMap()
    {
        MapRenderer.RegisterStyleRenderer(typeof(CustomStyle), new SkiaCustomStyleRenderer());

        var map = new Map();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(CreateStylesLayer(map.Extent));

        map.Widgets.Add(new MapInfoWidget(map, l => l.Name == _mapInfoLayerName));

        return map;
    }

    private static ILayer CreateStylesLayer(MRect? envelope)
    {
        return new MemoryLayer
        {
            Name = _mapInfoLayerName,
            Features = CreateDiverseFeatures(RandomPointsBuilder.GenerateRandomPoints(envelope, 25)),
            Style = null,
        };
    }

    private static IEnumerable<IFeature> CreateDiverseFeatures(IEnumerable<MPoint> randomPoints)
    {
        var features = new List<IFeature>();
        var style = new CustomStyle();
        var counter = 1;
        foreach (var point in randomPoints)
        {
            var feature = new PointFeature(point);
            feature["Label"] = $"I'm no. {counter++} and, autsch, you hit me!";
            feature.Styles.Add(style); // Here the custom style is set!
            feature.Styles.Add(SmalleDot());
            features.Add(feature);
        }
        return features;
    }

    private static IStyle SmalleDot()
    {
        return new SymbolStyle { SymbolScale = 0.2, Fill = new Brush(new Color(40, 40, 40)) };
    }
}
