﻿using Mapsui.Extensions;
using Mapsui.Logging;
using Mapsui.Manipulations;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Diagnostics;

namespace Mapsui.UI.WindowsForms;

public partial class MapControl : UserControl, IMapControl, IDisposable
{
    public static bool UseGPU = false;

    private readonly SKGLControl? _glView;
    private readonly SKControl? _canvasView;
    private readonly ManipulationTracker _manipulationTracker = new();
    private bool _disposed;

#if NET9_OR_GREATER
    [Register("MapControl"), DesignTimeVisible(true)]
#endif
    public MapControl()
    {
        Control view;

        Dock = DockStyle.Fill;
        AutoSize = true;
        BackColor = Color.White;
        Resize += MapControlResize;

        if (UseGPU)
        {
            // Use GPU backend
            _glView = new SKGLControl();
            // Events
            _glView.PaintSurface += OnGLPaintSurface;
            view = _glView;
        }
        else
        {
            // Use CPU backend
            _canvasView = new SKControl();
            // Events
            _canvasView.PaintSurface += OnPaintSurface;
            view = _canvasView;
        }

        // Common events
        view.MouseDown += MapControlMouseDown;
        view.MouseMove += MapControlMouseMove;
        view.MouseUp += MapControlMouseUp;
        view.MouseWheel += MapControlMouseWheel;

        view.Dock = DockStyle.Fill;

        Controls.Add(view);

        SharedConstructor();
    }

    public void InvalidateCanvas()
    {
        if (_glView is SKGLControl glView)
        {
            if (!_glView.IsHandleCreated)
                return;
            Invoke(glView.Invalidate);
        }
        else if (_canvasView is SKControl canvasView)
        {
            if (!canvasView.IsHandleCreated)
                return;
            Invoke(_canvasView.Invalidate);
        }
        else
            throw new InvalidOperationException("Neither the SKGLControl nor the SKControl is initialized.");
    }

    private void MapControlResize(object? sender, EventArgs e)
    {
        SharedOnSizeChanged(Width, Height);
    }

    private void OnGLPaintSurface(object? sender, SKPaintGLSurfaceEventArgs args)
    {
        if (_glView?.GRContext is null)
        {
            // Could this be null before Home is called? If so we should change the logic.
            Logger.Log(LogLevel.Warning, "Refresh can not be called because GRContext is null");
            return;
        }

        // Called on UI thread
        PaintSurface(args.Surface.Canvas);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        // Called on UI thread
        PaintSurface(args.Surface.Canvas);
    }

    private void PaintSurface(SKCanvas canvas)
    {
        if (GetPixelDensity() is not float pixelDensity)
            return;

        canvas.Scale(pixelDensity, pixelDensity);

        _renderController?.Render(canvas);
    }

    private void MapControlMouseDown(object? sender, MouseEventArgs e)
    {
        var screenPosition = GetScreenPosition(e.Location);
        _manipulationTracker.Restart([screenPosition]);

        OnPointerPressed([screenPosition]);
    }

    private void MapControlMouseMove(object? sender, MouseEventArgs e)
    {
        var isHovering = IsHovering(e);
        var position = GetScreenPosition(e.Location);

        if (OnPointerMoved([position], isHovering))
            return;

        if (!isHovering)
            _manipulationTracker.Manipulate([position], Map.Navigator.Manipulate);
    }

    private void MapControlMouseUp(object? sender, MouseEventArgs e)
    {
        var screenPosition = GetScreenPosition(e.Location);
        OnPointerReleased([screenPosition]);
    }

    private void MapControlMouseWheel(object? sender, MouseEventArgs e)
    {
        var mouseWheelDelta = e.Delta;
        var mousePosition = GetScreenPosition(e.Location);
        Map.Navigator.MouseWheelZoom(mouseWheelDelta, mousePosition);
    }

    private static bool IsHovering(MouseEventArgs e)
    {
        return e.Button != MouseButtons.Left;
    }

    private static bool GetShiftPressed()
    {
        return (ModifierKeys & Keys.Shift) == Keys.Shift;
    }

    public void OpenInBrowser(string url)
    {
        Catch.TaskRun(() =>
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = url,
                // The default for this has changed in .net core, you have to explicitly set if to true for it to work.
                UseShellExecute = true
            });
        });
    }

    public ScreenPosition GetScreenPosition(Point position)
    {
        return new ScreenPosition(position.X, position.Y);
    }

    public float? GetPixelDensity()
    {
        if (Width <= 0)
            return null;
        return (float)(UseGPU ? _glView!.CanvasSize.Width : _canvasView!.CanvasSize.Width) / Width;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            SharedDispose(disposing);

            _glView?.Dispose();
            _canvasView?.Dispose();
        }

        base.Dispose(disposing);
    }
}
