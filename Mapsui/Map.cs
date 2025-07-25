// Copyright (c) The Mapsui authors.
// The Mapsui authors licensed this file under the MIT license.
// See the LICENSE file in the project root for full license information.

// This file was originally created by Morten Nielsen (www.iter.dk) as part of SharpMap

using Mapsui.Extensions;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Rendering;
using Mapsui.Styles;
using Mapsui.Utilities;
using Mapsui.Widgets;
using Mapsui.Widgets.InfoWidgets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Mapsui;

/// <summary>
/// Map class
/// </summary>
/// <remarks>
/// Map holds all map related info like the target CRS, layers, widgets and so on.
/// </remarks>
public class Map : INotifyPropertyChanged, IDisposable
{
    private LayerCollection _layers = [];
    private Color _backColor = Color.White;
    private IWidget[] _oldWidgets = [];
    private readonly LayerFetcher _layerFetcher;

    /// <summary>
    /// Initializes a new map
    /// </summary>
    public Map()
    {
        BackColor = Color.White;
        Layers = [];
        _layerFetcher = new LayerFetcher(Layers);
        Widgets.Add(CreateLoggingWidget(RefreshGraphics));
        Widgets.Add(CreatePerformanceWidget(this));
        Navigator.FetchRequested += Navigator_FetchRequested;
        Navigator.ViewportChanged += Navigator_ViewportChanged;
    }

    public FetchMachine FetchMachine { get; } = new(16); // This is still needed because we support the IAsyncDataFetcher interface.
    public RenderService RenderService { get; set; } = new();

    /// <summary>
    /// Event that is triggered when the map is tapped. Can be a single tap, double tap or long press.
    /// </summary>
    public event EventHandler<MapEventArgs>? Tapped;
    /// <summary>
    /// Event that is triggered when on pointer down.
    /// </summary>
    public event EventHandler<MapEventArgs>? PointerPressed;
    /// <summary>
    /// Event that is triggered when on pointer move. Can be a drag or hover.
    /// </summary>
    public event EventHandler<MapEventArgs>? PointerMoved;
    /// <summary>
    /// Event that is triggered when on pointer up.
    /// </summary>
    public event EventHandler<MapEventArgs>? PointerReleased;

    private void Navigator_ViewportChanged(object? sender, ViewportChangedEventArgs e)
    {
        RefreshGraphics();
    }

    /// <summary>
    /// List of Widgets belonging to map
    /// </summary>
    public ConcurrentQueue<IWidget> Widgets { get; } = [];

    /// <summary>
    /// Coordinate reference system (projection type of map).
    /// Default: "EPSG:3857" (SphericalMercator).
    /// </summary>
    public string? CRS { get; set; } = "EPSG:3857";

    /// <summary>
    /// A collection of layers. The first layer in the list is drawn first, the last one on top.
    /// </summary>
    public LayerCollection Layers
    {
        get
        {
            AssureWidgetsConnected();
            return _layers;
        }
        private set
        {
            var tempLayers = _layers;
            if (tempLayers != null)
                _layers.Changed -= LayersCollectionChanged;

            _layers = value;
            _layers.Changed += LayersCollectionChanged;
        }
    }

    private void AssureWidgetsConnected()
    {
        // it would be better if Widgets would be an observable collection then I wouldn't need this workaround
        if (_oldWidgets.Length != Widgets.Count)
        {
            foreach (var widget in _oldWidgets)
            {
                if (widget is INotifyPropertyChanged propertyChanged)
                {
                    propertyChanged.PropertyChanged -= WidgetPropertyChanged;
                }
            }

            _oldWidgets = Widgets.ToArray();

            foreach (var widget in Widgets)
            {
                if (widget is INotifyPropertyChanged propertyChanged)
                {
                    propertyChanged.PropertyChanged += WidgetPropertyChanged;
                }
            }
        }
    }

    private void WidgetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshGraphics();
    }

    /// <summary>
    /// Map background color (defaults to transparent)
    ///  </summary>
    public Color BackColor
    {
        get => _backColor;
        set
        {
            if (_backColor == value) return;
            _backColor = value;
            OnPropertyChanged(nameof(BackColor));
        }
    }

    /// <summary>
    /// Gets the extent of the map based on the extent of all the layers in the layers collection
    /// </summary>
    /// <returns>Full map extent</returns>
    public MRect? Extent
    {
        get
        {
            if (_layers.Count == 0) return null;

            MRect? extent = null;
            foreach (var layer in _layers)
            {
                extent = extent == null ? layer.Extent : extent.Join(layer.Extent);
            }
            return extent;
        }
    }

    /// <summary>
    /// Handles all manipulations of the map viewport
    /// </summary>
    public Navigator Navigator { get; private set; } = new Navigator();

    public Performance Performance { get; } = new Performance();

    /// <summary>
    /// Called whenever a property changed
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// DataChanged should be triggered by any data changes of any of the child layers
    /// </summary>
    public event DataChangedEventHandler? DataChanged;

    public event EventHandler? RefreshGraphicsRequest;

    /// <summary>
    /// Called whenever the map is clicked. The MapInfoEventArgs contain the features that were hit in
    /// the layers that have IsMapInfoLayer set to true. 
    /// </summary>
    /// <remarks>
    /// The Tapped event is preferred over the Info event. This event is kept for backwards compatibility.
    /// </remarks>
    public event EventHandler<MapInfoEventArgs>? Info;

    private void Navigator_FetchRequested(object? sender, FetchRequestedEventArgs e)
    {
        RefreshData(e.ChangeType);
    }

    /// <summary>
    /// Refresh data of the map and than repaint it
    /// </summary>
    public void Refresh(ChangeType changeType = ChangeType.Discrete)
    {
        RefreshData(changeType);
        RefreshGraphics();
    }

    public void RefreshData(Viewport viewport)
    {
        RefreshData(ChangeType.Discrete, viewport);
    }

    /// <summary>
    /// Refresh data of Map, but don't paint it
    /// </summary>
    public void RefreshData(ChangeType changeType = ChangeType.Discrete, Viewport? viewport = null)
    {
        var localViewport = viewport ?? Navigator.Viewport;

        if (localViewport.ToExtent() is null)
            return;
        if (localViewport.ToExtent().GetArea() <= 0)
            return;

        var fetchInfo = new FetchInfo(localViewport.ToSection(), CRS, changeType);

        foreach (var layer in _layers.ToList())
        {
            if (layer is IAsyncDataFetcher asyncDataFetcher)
                asyncDataFetcher.RefreshData(fetchInfo, FetchMachine.Enqueue);
        }

        if (changeType == ChangeType.Discrete)
            _layerFetcher.ViewportChanged(fetchInfo);
    }

    public void RefreshGraphics()
    {
        RefreshGraphicsRequest?.Invoke(this, EventArgs.Empty);
    }

    public void OnViewportSizeInitialized()
    {
        ViewportInitialized?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>
    /// Called when the viewport is initialized
    /// </summary>
    public event EventHandler? ViewportInitialized; //todo: Consider to use the Viewport PropertyChanged


    /// <summary>
    /// Abort fetching of all layers
    /// </summary>
    public void AbortFetch()
    {
        foreach (var layer in _layers.ToList())
        {
            if (layer is IAsyncDataFetcher asyncLayer) asyncLayer.AbortFetch();
        }
    }

    /// <summary>
    /// Clear cache of all layers
    /// </summary>
    public void ClearCache()
    {
        foreach (var layer in _layers)
        {
            if (layer is IAsyncDataFetcher asyncLayer)
                asyncLayer.ClearCache();
            if (layer is IFetchableSource fetchableSource)
                fetchableSource.ClearCache();
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void OnTapped(MapEventArgs e)
    {
        Tapped?.Invoke(this, e);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void OnPointerPressed(MapEventArgs e)
    {
        PointerPressed?.Invoke(this, e);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void OnPointerMoved(MapEventArgs e)
    {
        PointerMoved?.Invoke(this, e);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void OnPointerReleased(MapEventArgs e)
    {
        PointerReleased?.Invoke(this, e);
    }

    private void LayersCollectionChanged(object sender, LayerCollectionChangedEventArgs args)
    {
        foreach (var layer in args.RemovedLayers ?? [])
            LayerRemoved(layer);

        foreach (var layer in args.AddedLayers ?? [])
            LayerAdded(layer);

        LayersChanged();
    }

    private void LayerAdded(ILayer layer)
    {
        layer.DataChanged += LayerDataChanged;
        layer.PropertyChanged += LayerPropertyChanged;
        if (layer is IFetchableSource fetchableSource)
            fetchableSource.FetchRequested += FetchableSource_FetchRequested;
    }

    private void FetchableSource_FetchRequested(object? sender, FetchRequestedEventArgs e)
    {
        RefreshData(e.ChangeType);
    }

    private void LayerRemoved(ILayer layer)
    {
        if (layer is IAsyncDataFetcher asyncLayer)
            asyncLayer.AbortFetch();
        if (layer is IFetchableSource fetchableSource)
            fetchableSource.FetchRequested -= FetchableSource_FetchRequested;

        layer.DataChanged -= LayerDataChanged;
        layer.PropertyChanged -= LayerPropertyChanged;
    }

    private void LayersChanged()
    {
        Navigator.DefaultResolutions = DetermineResolutions(Layers);
        Navigator.DefaultZoomBounds = GetMinMaxResolution(Navigator.Resolutions);
        Navigator.DefaultPanBounds = Extent?.Copy();
        OnPropertyChanged(nameof(Layers));
    }

    private static MMinMax? GetMinMaxResolution(IEnumerable<double>? resolutions)
    {
        if (resolutions == null || !resolutions.Any()) return null;
        resolutions = resolutions.OrderByDescending(r => r).ToArray();
        var mostZoomedOut = resolutions.First();
        var mostZoomedIn = resolutions.Last() * 0.5; // Divide by two to allow one extra level to zoom-in
        return new MMinMax(mostZoomedOut, mostZoomedIn);
    }

    private static double[] DetermineResolutions(IEnumerable<ILayer> layers)
    {
        var items = new Dictionary<double, double>();
        const float normalizedDistanceThreshold = 0.75f;
        foreach (var layer in layers)
        {
            if (!layer.Enabled || layer.Resolutions == null) continue;

            foreach (var resolution in layer.Resolutions)
            {
                // About normalization:
                // Resolutions don't have equal distances because they 
                // are multiplied by two at every step. Distances on the 
                // lower zoom levels have very different meaning than on the
                // higher zoom levels. So we work with a normalized resolution
                // to determine if another resolution adds value. If a resolution
                // is a factor of 2 of another resolution. The normalized distance
                // is one.
                var normalized = Math.Log(resolution, 2);
                if (items.Count == 0)
                {
                    items[normalized] = resolution;
                }
                else
                {
                    var normalizedDistance = items.Keys.Min(k => Math.Abs(k - normalized));
                    if (normalizedDistance > normalizedDistanceThreshold) items[normalized] = resolution;
                }
            }
        }

        return items.Select(i => i.Value).OrderByDescending(i => i).ToArray();
    }

    private void LayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(sender, e.PropertyName);
    }

    private void OnPropertyChanged(object? sender, string? propertyName)
    {
        PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
    }

    private void OnPropertyChanged(string name)
    {
        OnPropertyChanged(this, name);
    }

    private void LayerDataChanged(object sender, DataChangedEventArgs e)
    {
        OnDataChanged(sender, e);
    }

    private void OnDataChanged(object sender, DataChangedEventArgs e)
    {
        DataChanged?.Invoke(sender, e);
    }

    public IEnumerable<IWidget> GetWidgetsOfMapAndLayers()
    {
        return Widgets.Concat(Layers.Where(l => l.Enabled).Select(l => l.Attribution))
            .Where(a => a != null && a.Enabled).ToArray();
    }

    /// <summary>
    /// This method is to invoke the Info event from the Map. This method is called
    /// by the MapControl/MapView and should usually not be called from user code.
    /// </summary>
    public void OnMapInfo(MapInfoEventArgs e)
    {
        Info?.Invoke(this, e);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var layer in Layers)
                LayerRemoved(layer); // Remove Event so that no memory leaks occur
            Layers.ClearAllGroups();
            RenderService.Dispose();
        }
    }

    public bool UpdateAnimations()
    {
        var areAnimationsRunning = false;

        foreach (var layer in Layers)
        {
            if (layer.UpdateAnimations())
                areAnimationsRunning = true;
        }

        return areAnimationsRunning;
    }

    private static LoggingWidget CreateLoggingWidget(Action refreshGraphics) => new(refreshGraphics)
    {
        Margin = new MRect(10),
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        BackColor = Color.Transparent,
        Opacity = 0.0f,
    };

    private static PerformanceWidget CreatePerformanceWidget(Map map) => new(map.Performance)
    {
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new MRect(10, 60),
        TextSize = 12,
        TextColor = Color.Black,
        BackColor = Color.White,
        WithTappedEvent = (s, e) =>
        {
            map.Performance.Clear();
            map.RefreshGraphics();
            e.Handled = true;
        }
    };
}
