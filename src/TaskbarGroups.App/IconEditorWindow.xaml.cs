using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImage = System.Drawing.Image;

namespace TaskbarGroups.App;

/// <summary>
/// Lets the user pan and zoom an image inside a square frame and crop it to a
/// group icon. The visible square is what gets applied.
/// </summary>
public partial class IconEditorWindow : FluentWindow
{
    private const double Viewport = 320.0;
    private const double MaxZoom = 4.0;

    private readonly double _imgW;
    private readonly double _imgH;
    private double _baseScale; // scale at which the image just covers the frame
    private double _scale;
    private double _tx, _ty;   // image top-left inside the canvas

    private bool _dragging;
    private Point _lastPos;
    private bool _ready;
    private bool _suppressSlider;

    /// <summary>The cropped 1:1 icon, or null if the user cancelled.</summary>
    public DrawingImage? Result { get; private set; }

    public IconEditorWindow(BitmapSource source)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);

        EditImage.Source = source;
        _imgW = Math.Max(1, source.PixelWidth);
        _imgH = Math.Max(1, source.PixelHeight);

        Loaded += (_, _) => InitLayout();
    }

    private void InitLayout()
    {
        _baseScale = Math.Max(Viewport / _imgW, Viewport / _imgH);
        _scale = _baseScale;
        _tx = (Viewport - _imgW * _scale) / 2;
        _ty = (Viewport - _imgH * _scale) / 2;
        ApplyTransform();
        _ready = true;
    }

    private void ApplyTransform()
    {
        EditImage.Width = _imgW * _scale;
        EditImage.Height = _imgH * _scale;
        Canvas.SetLeft(EditImage, _tx);
        Canvas.SetTop(EditImage, _ty);
    }

    private void ClampAndApply()
    {
        double dispW = _imgW * _scale;
        double dispH = _imgH * _scale;
        // Keep the frame fully covered (no transparent edges).
        _tx = Clamp(_tx, Viewport - dispW, 0);
        _ty = Clamp(_ty, Viewport - dispH, 0);
        ApplyTransform();
    }

    private void ZoomTo(double newScale, double pivotX, double pivotY)
    {
        newScale = Clamp(newScale, _baseScale, _baseScale * MaxZoom);

        // Keep the image point under the pivot fixed while zooming.
        double fx = (pivotX - _tx) / (_imgW * _scale);
        double fy = (pivotY - _ty) / (_imgH * _scale);
        _scale = newScale;
        _tx = pivotX - fx * (_imgW * _scale);
        _ty = pivotY - fy * (_imgH * _scale);

        ClampAndApply();
        SyncSlider();
    }

    private void SyncSlider()
    {
        if (ZoomSlider == null) return;
        _suppressSlider = true;
        ZoomSlider.Value = Clamp(_scale / _baseScale, 1, MaxZoom);
        _suppressSlider = false;
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _lastPos = e.GetPosition(CropCanvas);
        CropCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Point pos = e.GetPosition(CropCanvas);
        _tx += pos.X - _lastPos.X;
        _ty += pos.Y - _lastPos.Y;
        _lastPos = pos;
        ClampAndApply();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        CropCanvas.ReleaseMouseCapture();
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_ready) return;
        Point p = e.GetPosition(CropCanvas);
        double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        ZoomTo(_scale * factor, p.X, p.Y);
        e.Handled = true;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _suppressSlider) return;
        ZoomTo(_baseScale * ZoomSlider.Value, Viewport / 2, Viewport / 2);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rtb = new RenderTargetBitmap((int)Viewport, (int)Viewport, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(CropCanvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;
            using var tmp = DrawingImage.FromStream(ms);
            Result = new DrawingBitmap(tmp); // independent copy, detached from the stream

            DialogResult = true;
            Close();
        }
        catch
        {
            DialogResult = false;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static double Clamp(double value, double min, double max)
        => value < min ? min : (value > max ? max : value);
}
