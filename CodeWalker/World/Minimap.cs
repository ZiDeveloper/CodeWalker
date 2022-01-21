using System;
using System.Threading;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using CodeWalker.Rendering;
using CodeWalker.World;

public class Minimap
{
    public Camera camera { get; private set; }

    public DXManager dxMan { get; private set; }
    public Bitmap d2dBitmap { get; private set; }
    public DeviceContext d2dContext { get; private set; }

    public TextFormat dwFormatMain { get; private set; }
    public TextFormat dwFormatMapCharacter { get; private set; }
    public SolidColorBrush d2dMainBrush { get; private set; }
    public SolidColorBrush d2dSolidWhiteBrush { get; private set; }
    public SolidColorBrush d2dSolidBlackBrush { get; private set; }

    public BitmapProperties1 d2dMaskBitmapProperties { get; private set; }

    public RawVector2 Position { get; private set; }
    public float WorldOffsetX { get; private set; }
    public float WorldOffsetY { get; private set; }
    public float Radius { get; private set; }
    public float Opacity { get; private set; }
    public float Scale { get; private set; }
    public float BorderThickness { get; private set; }
    public float WorldToMapFactor { get; private set; }

    public Vector2 North { get; private set; }
    public float NorthSize { get; private set; }
    public float Rotation { get; private set; }

    public Minimap()
    {
        Position = new RawVector2(100f, 120f);
        North = new Vector2();
        NorthSize = 10.0f;
        Radius = 93.0f;
        Opacity = 1.0f;
        Scale = 1.3f;
        BorderThickness = 7.0f;
        WorldOffsetX = 1870f;
        WorldOffsetY = 3319f;
        WorldToMapFactor = 0.329f;
    }

    public void Init(DeviceContext pD2DContext, DXManager pDXMan, Camera pCamera)
    {
        dxMan = pDXMan;
        d2dContext = pD2DContext;
        camera = pCamera;

        if (dxMan == null || pD2DContext == null) throw new ArgumentNullException("Minimap Init Failed: DXManager nor 2D DeviceContext can't be null");

        // Load bitmap from disk
        new Thread(() =>
        {
            try
            {
                d2dBitmap = LoadFromFile(dxMan, d2dContext, "res\\GTAV_ATLUS_4096.png");
            }
            catch (Exception e)
            { System.Windows.Forms.MessageBox.Show("Faild to load \\res\\GTAV_ATLUS_4096.png: " + e.Message); }
        }).Start();

        d2dMaskBitmapProperties = new BitmapProperties1(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target);

        CreateResources();
    }

    public void CreateResources()
    {
        SharpDX.DirectWrite.Factory dwFactory = new SharpDX.DirectWrite.Factory();
        dwFormatMain = new TextFormat(dwFactory, "Calibri", 12)
        {
            TextAlignment = TextAlignment.Leading,
            ParagraphAlignment = ParagraphAlignment.Near
        };
        dwFormatMapCharacter = new TextFormat(dwFactory, "Calibri", 22)
        {
            TextAlignment = TextAlignment.Leading,
            ParagraphAlignment = ParagraphAlignment.Near
        };
        dwFactory.Dispose();

        d2dMainBrush = new SolidColorBrush(d2dContext, new RawColor4(1f, 1f, 1f, 0.5f));
        d2dSolidWhiteBrush = new SolidColorBrush(d2dContext, new RawColor4(1f, 1f, 1f, Opacity));
        d2dSolidBlackBrush = new SolidColorBrush(d2dContext, new RawColor4(0f, 0f, 0f, Opacity));
    }

    public void DisposeResources()
    {
        if (dwFormatMain != null) { dwFormatMain.Dispose(); }
        if (dwFormatMapCharacter != null) { dwFormatMapCharacter.Dispose(); }
        if (d2dMainBrush != null) { d2dMainBrush.Dispose(); }
        if (d2dSolidWhiteBrush != null) { d2dSolidWhiteBrush.Dispose(); }
        if (d2dSolidBlackBrush != null) { d2dSolidBlackBrush.Dispose(); }
    }

    public void Update(Camera pCamera)
    {
        camera = pCamera;

        Rotation = (camera.CurrentRotation.X / (float)(2 * Math.PI));
        Rotation -= (int)Rotation;
        if (Rotation < 0) Rotation += 1;
        Rotation *= 360.0f;

        WorldOffsetX = 1262.0f + ((camera.FollowEntity.Position.X + 1874.07f) * WorldToMapFactor);
        WorldOffsetY = 3162.0f - ((camera.FollowEntity.Position.Y + 1213.51f) * WorldToMapFactor);
    }

    public void Render(Camera camera)
    {
        if (d2dContext == null || d2dBitmap == null) return;

        d2dContext.BeginDraw();

        // Disclaimer
        d2dContext.DrawText(
            "Disclaimer: This version of CodeWalker is modified. Use on your one risk.",
            dwFormatMain,
            new RectangleF(13, 50, 500, 40),
            d2dMainBrush);

        // Draw background shape
        Ellipse backgroundEllipse = new Ellipse(Position, Radius, Radius);
        backgroundEllipse.Point.Y = (-backgroundEllipse.Point.Y + camera.Height);
        d2dContext.FillEllipse(backgroundEllipse, d2dSolidWhiteBrush);

        // Draw atlas
        var MaskBitmap = new Bitmap1(d2dContext, new Size2((int)(Position.X + Radius), (int)camera.Height), d2dMaskBitmapProperties); ;

        d2dContext.Target = MaskBitmap;
        d2dContext.FillEllipse(new Ellipse(new RawVector2(Position.X, camera.Height - Position.Y), Radius, Radius), d2dSolidWhiteBrush);

        var MaskBrush = new BitmapBrush(d2dContext, MaskBitmap);
        d2dContext.Target = dxMan.d2dRenderTargetBitmap;

        d2dContext.PushLayer(new LayerParameters1()
        {
            ContentBounds = new RawRectangleF(0, 0, (Position.X + Radius), camera.Height),
            GeometricMask = null,
            MaskAntialiasMode = AntialiasMode.Aliased,
            MaskTransform = new Matrix3x2(),
            Opacity = 1.0f,
            OpacityBrush = MaskBrush,
            LayerOptions = LayerOptions1.None
        }, null);

        d2dContext.Transform = Matrix3x2.Rotation(Deg2Rad(360.0f - Rotation), new Vector2(100.0f, camera.Height - 120.0f));
        d2dContext.DrawBitmap(
                d2dBitmap,
                new RawRectangleF(
                    Position.X - 96,
                    camera.Height - Position.Y - 96,
                    Position.X + 96,
                    camera.Height - Position.Y + 96),
                Opacity,
                BitmapInterpolationMode.Linear,
                new RawRectangleF(
                    (-96 * Scale) + WorldOffsetX,
                    (-96 * Scale) + WorldOffsetY,
                    (96 * Scale) + WorldOffsetX,
                    (96 * Scale) + WorldOffsetY));
        d2dContext.Transform = Matrix3x2.Identity;
        d2dContext.PopLayer();

        MaskBitmap.Dispose();
        MaskBrush.Dispose();

        // Draw border shape
        d2dContext.DrawEllipse(backgroundEllipse, d2dSolidBlackBrush, BorderThickness);

        // Draw north shape
        North = new Vector2(
            Radius * (float)Math.Cos(Deg2Rad(Rotation - 90.0f)),
            Radius * (float)Math.Sin(Deg2Rad(Rotation - 90.0f)));

        backgroundEllipse.Point = new RawVector2(Position.X - North.X, -Position.Y + camera.Height + North.Y);
        backgroundEllipse.RadiusX = NorthSize;
        backgroundEllipse.RadiusY = NorthSize;
        d2dContext.FillEllipse(backgroundEllipse, d2dSolidBlackBrush);

        // Draw north character
        d2dContext.DrawText(
                "N",
                dwFormatMapCharacter,
                new RectangleF(Position.X - North.X - 7, -Position.Y + camera.Height - 14 + North.Y, 2 * NorthSize, 2 * NorthSize),
                d2dSolidWhiteBrush);

        // Draw player marker
        backgroundEllipse.Point = new RawVector2(Position.X, camera.Height - Position.Y);
        backgroundEllipse.RadiusX = 3.0f;
        backgroundEllipse.RadiusY = 3.0f;
        d2dSolidWhiteBrush.Color = new RawColor4(0f, 0.65f, 1f, Opacity);
        d2dContext.FillEllipse(backgroundEllipse, d2dSolidWhiteBrush);
        d2dSolidWhiteBrush.Color = Color.White;

        // Draw crosshair ( remove later )
        backgroundEllipse.Point = new RawVector2(camera.Width / 2, camera.Height / 2);
        backgroundEllipse.RadiusX = 2.0f;
        backgroundEllipse.RadiusY = 2.0f;
        d2dSolidWhiteBrush.Opacity = 0.4f;
        d2dContext.FillEllipse(backgroundEllipse, d2dSolidWhiteBrush);
        d2dSolidWhiteBrush.Opacity = Opacity;

        d2dContext.EndDraw();
    }

    public void Resize(DXManager pDXMan)
    {
        d2dContext = pDXMan.d2dContext;
        DisposeResources();
        CreateResources();
    }

    public void Cleanup()
    {
        DisposeResources();

        if (d2dBitmap != null) { d2dBitmap.Dispose(); }
    }


    /// Source: https://stackoverflow.com/a/5200086
    /// <summary>
    /// Loads a Direct2D Bitmap from a file using System.Drawing.Image.FromFile(...)
    /// </summary>
    /// <param name="renderTarget">The render target.</param>
    /// <param name="file">The file.</param>
    /// <returns>A D2D1 Bitmap</returns>
    public static Bitmap LoadFromFile(DXManager dxm, RenderTarget renderTarget, string file)
    {
        Bitmap result;
        // Loads from file using System.Drawing.Image
        using (var bitmap = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(file))
        {
            var sourceArea = new System.Drawing.Rectangle(
                0,
                0,
                bitmap.Width,
                bitmap.Height);
            var bitmapProperties = new SharpDX.Direct2D1.BitmapProperties(
                new SharpDX.Direct2D1.PixelFormat(
                    SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                    SharpDX.Direct2D1.AlphaMode.Premultiplied));
            var size = new SharpDX.Size2(
                bitmap.Width,
                bitmap.Height);

            // Transform pixels from BGRA to RGBA
            int stride = bitmap.Width * sizeof(int);

            using (var tempStream = new SharpDX.DataStream(bitmap.Height * stride, true, true))
            {
                // Lock System.Drawing.Bitmap
                var bitmapData = bitmap.LockBits(sourceArea, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // Convert all pixels 
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int offset = bitmapData.Stride * y;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        // Not optimized 
                        byte B = System.Runtime.InteropServices.Marshal.ReadByte(bitmapData.Scan0, offset++);
                        byte G = System.Runtime.InteropServices.Marshal.ReadByte(bitmapData.Scan0, offset++);
                        byte R = System.Runtime.InteropServices.Marshal.ReadByte(bitmapData.Scan0, offset++);
                        byte A = System.Runtime.InteropServices.Marshal.ReadByte(bitmapData.Scan0, offset++);
                        int rgba = R | (G << 8) | (B << 16) | (A << 24);
                        tempStream.Write(rgba);
                    }
                }

                bitmap.UnlockBits(bitmapData);
                tempStream.Position = 0;

                if (renderTarget.IsDisposed) { renderTarget = dxm.d2dContext; }
                result = new Bitmap(renderTarget, size, tempStream, stride, bitmapProperties);

                bitmap.Dispose();
                tempStream.Dispose();
                bitmapData = null;

                return result;
            }
        }
    }

    /// <summary>
    /// Converts angels from degrees to radians
    /// </summary>
    /// <param name="degree">Angel in degrees.</param>
    /// <returns>Float angel in radians.</returns>
    private float Deg2Rad(float degree)
    {
        return degree * ((float)Math.PI / 180.0f);
    }

}
