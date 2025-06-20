using Eto.Forms;
using Eto.SkiaDraw;
using Mapsui.Extensions;
using Mapsui.Manipulations;
using Mapsui.UI.Eto.Extensions;
using System;
using System.Diagnostics;

namespace Mapsui.UI.Eto;

public partial class MapControl : SkiaDrawable, IMapControl
{
    private Cursor _defaultCursor = Cursors.Default;
    private readonly ManipulationTracker _manipulationTracker = new();
    private bool _shiftPressed;

    public MapControl()
    {
        SizeChanged += MapControl_SizeChanged; ;
        SharedConstructor();
    }

    public void InvalidateCanvas()
    {
        RunOnUIThread(Invalidate);
    }

    private void MapControl_SizeChanged(object? sender, EventArgs e)
    {
        SharedOnSizeChanged(Width, Height);
    }

    public Cursor MoveCursor { get; set; } = Cursors.Move;
    public MouseButtons MoveButton { get; set; } = MouseButtons.Primary;
    public Keys MoveModifier { get; set; } = Keys.None;
    public MouseButtons ZoomButton { get; set; } = MouseButtons.Primary;
    public Keys ZoomModifier { get; set; } = Keys.Control;

    public void OpenInBrowser(string url)
    {
        Catch.TaskRun(() =>
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        });
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        SetCursorInMoveMode();
        var position = e.Location.ToScreenPosition();

        _manipulationTracker.Restart([position]);

        if (OnPointerPressed([position]))
            return;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var isHovering = IsHovering(e);
        var position = e.Location.ToScreenPosition();

        if (OnPointerMoved([position], isHovering))
            return;

        if (!isHovering)
            _manipulationTracker.Manipulate([position], Map.Navigator.Manipulate);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        SetCursorInDefaultMode();
        var position = e.Location.ToScreenPosition();
        OnPointerReleased([position]);
    }

    protected override void OnLoadComplete(EventArgs e)
    {
        base.OnLoadComplete(e);

        SharedOnSizeChanged(Width, Height);
        CanFocus = true;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        var mouseWheelDelta = (int)e.Delta.Height;
        var mousePosition = e.Location.ToScreenPosition();
        Map.Navigator.MouseWheelZoom(mouseWheelDelta, mousePosition);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        SharedOnSizeChanged(Width, Height);
    }

    protected override void OnPaint(SKPaintEventArgs e)
    {
        if (GetPixelDensity() is not float pixelDensity)
            return;

        var canvas = e.Surface.Canvas;
        canvas.Scale(pixelDensity, pixelDensity);
        _renderController?.Render(canvas);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _map?.Dispose();
        }

#pragma warning disable IDISP023 // Don't use reference types in finalizer context
        SharedDispose(disposing);
#pragma warning restore IDISP023 // Don't use reference types in finalizer context

        base.Dispose(disposing);
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _shiftPressed = e.Shift;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _shiftPressed = e.Shift;
    }

    public float? GetPixelDensity()
    {
        var center = PointToScreen(Location + Size / 2);
        return Screen.FromPoint(center).LogicalPixelSize;
    }

    private static void RunOnUIThread(Action action) => Application.Instance.AsyncInvoke(action);

    private bool IsHovering(MouseEventArgs e)
        => !(e.Buttons == MoveButton && (MoveModifier == Keys.None || e.Modifiers == MoveModifier));

    private void SetCursorInMoveMode()
    {
        _defaultCursor = Cursor; // And store previous cursor to restore it later
        Cursor = MoveCursor;
    }

    private void SetCursorInDefaultMode() => Cursor = _defaultCursor;

    private bool GetShiftPressed()
    {
        return _shiftPressed;
    }
}
