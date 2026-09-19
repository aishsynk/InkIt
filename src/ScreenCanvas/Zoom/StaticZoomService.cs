using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenCanvas.Presentation;
using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace ScreenCanvas.Zoom;

public sealed class StaticZoomService : IDisposable
{
    private StaticZoomWindow? _window;
    public bool IsVisible => _window?.IsVisible == true;
    public event EventHandler? Closed;

    public void Show(double factor = 2)
    {
        Hide();
        _window = new StaticZoomWindow(Capture(), factor);
        _window.Closed += (_, _) =>
        {
            _window = null;
            Closed?.Invoke(this, EventArgs.Empty);
        };
        _window.Show();
        _window.Activate();
    }
    public void SetFactor(double factor) => _window?.SetFactor(factor);
    public void Hide() { _window?.Close(); _window=null; }
    public void Dispose() => Hide();

    private static BitmapSource Capture()
    {
        var bounds=new Rectangle((int)SystemParameters.VirtualScreenLeft,(int)SystemParameters.VirtualScreenTop,(int)SystemParameters.VirtualScreenWidth,(int)SystemParameters.VirtualScreenHeight);
        using var bitmap=new Bitmap(bounds.Width,bounds.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Left,bounds.Top,0,0,bounds.Size);
        var handle=bitmap.GetHbitmap(); try { var source=Imaging.CreateBitmapSourceFromHBitmap(handle,nint.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source; } finally { DeleteObject(handle); }
    }
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
}

internal sealed class StaticZoomWindow : EmergencySafeWindow
{
    private readonly Image _image; private readonly ScaleTransform _scale=new(); private readonly TranslateTransform _translate=new(); private Point _anchor; private bool _panning;
    internal StaticZoomWindow(BitmapSource source,double factor)
    {
        Left=SystemParameters.VirtualScreenLeft;Top=SystemParameters.VirtualScreenTop;Width=SystemParameters.VirtualScreenWidth;Height=SystemParameters.VirtualScreenHeight;Background=Brushes.Black;Cursor=Cursors.SizeAll;
        var transforms=new TransformGroup();transforms.Children.Add(_scale);transforms.Children.Add(_translate);
        _image=new Image{Source=source,Stretch=Stretch.Fill,RenderTransform=transforms,RenderTransformOrigin=new Point(0,0)};Content=_image;
        Loaded+=(_,_)=>{var p=Mouse.GetPosition(this);SetFactor(factor,p);};MouseWheel+=(_,e)=>SetFactor(_scale.ScaleX+(e.Delta>0?.25:-.25),e.GetPosition(this));
        MouseLeftButtonDown+=(_,e)=>{_panning=true;_anchor=e.GetPosition(this);CaptureMouse();};MouseMove+=(_,e)=>{if(!_panning)return;var p=e.GetPosition(this);_translate.X+=p.X-_anchor.X;_translate.Y+=p.Y-_anchor.Y;_anchor=p;};MouseLeftButtonUp+=(_,_)=>EndPan();
        LostMouseCapture+=(_,_)=>_panning=false;
        Closed+=(_,_)=>EndPan();
        KeyDown+=(_,e)=>{if(e.Key is Key.D1 or Key.D2 or Key.D3 or Key.D4)SetFactor(e.Key==Key.D1?1.5:e.Key==Key.D2?2:e.Key==Key.D3?3:4);};
    }
    internal void SetFactor(double factor,Point? focus=null)
    {
        factor=Math.Clamp(factor,1.25,4);var p=focus??new Point(ActualWidth/2,ActualHeight/2);var old=_scale.ScaleX<=0?1:_scale.ScaleX;_translate.X=p.X-(p.X-_translate.X)*factor/old;_translate.Y=p.Y-(p.Y-_translate.Y)*factor/old;_scale.ScaleX=_scale.ScaleY=factor;
    }

    private void EndPan()
    {
        _panning=false;
        if(IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnClosed(EventArgs e){EndPan();_image.Source=null;_image.RenderTransform=Transform.Identity;base.OnClosed(e);}
}
