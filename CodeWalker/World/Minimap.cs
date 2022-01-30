using System;
using System.Threading;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using SharpDX.Direct2D1.Effects;
using CodeWalker.Rendering;
using CodeWalker.World;

public class Minimap
{
    private Camera camera;

    private DXManager dxMan;
    private Bitmap d2dBitmapMain;
    private Bitmap d2dBitmapOffscreen;
    private DeviceContext d2dContext;

    private TextFormat dwFormatMain;
    private TextFormat dwFormatMapCharacter;
    private SolidColorBrush d2dMainBrush;
    private SolidColorBrush d2dSolidWhiteBrush;
    private SolidColorBrush d2dSolidBlackBrush;
    private Crop d2dCropEffect;
    private Scale d2dScaleEffect;

    private BitmapProperties1 bitmapProperties1;

    public RawVector2 Position { get; set; }
    public float WorldOffsetX { get; private set; }
    public float WorldOffsetY { get; private set; }
    public float Radius { get; private set; }
    public float Opacity { get; private set; }
    public float ZoomScale { get; private set; }
    public float BorderThickness { get; private set; }
    public float WorldToMapFactor { get; private set; }
    
    public bool IsHovering { get; private set; }

    private Vector2 North;
    private float NorthSize;
    public float Rotation { get; private set; }

    public Minimap()
    {
        Position = new RawVector2();
        North = new Vector2();
        NorthSize = 10.0f;
        Radius = 93.0f * 1.2f;
        Opacity = 1.0f;
        ZoomScale = 1.84f;
        BorderThickness = 7.0f;
        WorldOffsetX = 1870f;
        WorldOffsetY = 3319f;
        WorldToMapFactor = 0.329f;
        IsHovering = false;
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
                d2dBitmapMain = LoadFromFile(dxMan, d2dContext, "res\\GTAV_ATLUS_4096.png");
            }
            catch (Exception e)
            { System.Windows.Forms.MessageBox.Show("Faild to load \\res\\GTAV_ATLUS_4096.png: " + e.Message); }
        }).Start();

        bitmapProperties1 = new BitmapProperties1(new PixelFormat(SharpDX.DXGI.Format.R8G8B8A8_UNorm, AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target);
        d2dBitmapOffscreen = new Bitmap1(d2dContext, new Size2((int) (Radius * 2), (int) (Radius * 2)), bitmapProperties1);

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

        d2dScaleEffect = new Scale(d2dContext);
        d2dCropEffect = new Crop(d2dContext);
    }

    public void DisposeResources()
    {
        if (dwFormatMain != null) { dwFormatMain.Dispose(); }
        if (dwFormatMapCharacter != null) { dwFormatMapCharacter.Dispose(); }
        if (d2dMainBrush != null) { d2dMainBrush.Dispose(); }
        if (d2dSolidWhiteBrush != null) { d2dSolidWhiteBrush.Dispose(); }
        if (d2dSolidBlackBrush != null) { d2dSolidBlackBrush.Dispose(); }
        if (d2dScaleEffect != null) { d2dScaleEffect.Dispose(); }
        if (d2dCropEffect != null) { d2dCropEffect.Dispose(); }
    }

    public void Update(DXManager pDXMan, Camera pCamera, Vector2 pMousePos)
    {
        dxMan = pDXMan;
        d2dContext = pDXMan.d2dContext;
        camera = pCamera;

        Position = new RawVector2(4f + Radius, camera.Height - (29f + Radius));

        Rotation = (camera.CurrentRotation.X / (float)(2 * Math.PI));
        Rotation -= (int)Rotation;
        if (Rotation < 0) Rotation += 1;
        Rotation *= 360.0f;

        WorldOffsetX = 1262.0f + ((camera.FollowEntity.Position.X + 1874.07f) * WorldToMapFactor);
        WorldOffsetY = 3162.0f - ((camera.FollowEntity.Position.Y + 1213.51f) * WorldToMapFactor);

        // Mouse Hovering
        if ((Position-pMousePos).Length() <= Radius) { IsHovering = true; } else { IsHovering = false; }
    }

    public void Render()
    {
        if (d2dContext == null || d2dBitmapMain == null || d2dBitmapOffscreen == null) return;

        d2dContext.BeginDraw();

        RenderOffscreen(Radius * ZoomScale);
        
        // Main
        d2dContext.Transform = Matrix3x2.Rotation(Deg2Rad(360.0f - Rotation), new Vector2(Position.X, Position.Y));
        d2dContext.DrawBitmap(d2dBitmapOffscreen, new RawRectangleF(Position.X - Radius, Position.Y - Radius, Position.X + Radius, Position.Y + Radius), Opacity, BitmapInterpolationMode.Linear, new RawRectangleF(0, 0, Radius * 2, Radius * 2));
        d2dContext.Transform = Matrix3x2.Identity;

        // Player Center
        Ellipse backgroundEllipse = new Ellipse(new RawVector2(Position.X, Position.Y), 3f, 3f);
        d2dSolidWhiteBrush.Color = Color.Red;
        d2dContext.FillEllipse(backgroundEllipse, d2dSolidWhiteBrush);
        d2dSolidWhiteBrush.Color = Color.White;

        // Map Border
        backgroundEllipse.Point = new RawVector2(Position.X, Position.Y);
        backgroundEllipse.RadiusX = Radius;
        backgroundEllipse.RadiusY = Radius;
        d2dContext.DrawEllipse(backgroundEllipse, d2dSolidBlackBrush, BorderThickness);

        // North
        North = new Vector2(Radius * (float)Math.Cos(Deg2Rad(Rotation - 90.0f)), Radius * (float)Math.Sin(Deg2Rad(Rotation - 90.0f)));

        backgroundEllipse.Point = new RawVector2(Position.X - North.X, Position.Y + North.Y);
        backgroundEllipse.RadiusX = NorthSize;
        backgroundEllipse.RadiusY = NorthSize;
        
        d2dContext.FillEllipse(backgroundEllipse, d2dSolidBlackBrush);
        d2dContext.DrawText("N", dwFormatMapCharacter, new RectangleF(Position.X - North.X - BorderThickness,  Position.Y - (BorderThickness*2) + North.Y, 2 * NorthSize, 2 * NorthSize), d2dSolidWhiteBrush);

        // Disclaimer
        d2dContext.DrawText("Disclaimer: This version of CodeWalker is modified. Use on your one risk.", dwFormatMain, new RectangleF(13, 50, 500, 40), d2dMainBrush);

        d2dContext.EndDraw();
    }

    private void RenderOffscreen(float offsetmap) 
    {
        // Crop
        d2dCropEffect.SetInput(0, d2dBitmapMain, true);
        d2dCropEffect.Rectangle = new RawVector4(WorldOffsetX - offsetmap, WorldOffsetY - offsetmap, WorldOffsetX + offsetmap, WorldOffsetY + offsetmap);

        // Rescale
        d2dScaleEffect.SetInput(0, d2dCropEffect.Output, true);
        d2dScaleEffect.CenterPoint = new RawVector2(WorldOffsetX, WorldOffsetY);
        d2dScaleEffect.ScaleAmount = new RawVector2(Radius / offsetmap, Radius / offsetmap);

        var tempBitmap = new Bitmap1(d2dContext, new Size2((int) (Radius * 2), (int)(Radius * 2)), bitmapProperties1);

        d2dContext.Target = tempBitmap;

        var tempColorBursh = new SolidColorBrush(d2dContext, Color.Black);
        d2dContext.FillEllipse(new Ellipse(new RawVector2(Radius, Radius), Radius, Radius), tempColorBursh);

        var tempBrush = new BitmapBrush(d2dContext, tempBitmap);

        d2dContext.Target = d2dBitmapOffscreen;
        d2dContext.Clear(Color.Transparent);

        d2dSolidWhiteBrush.Color = new Color(1f, 1f, 1f, 0.5f);
        d2dContext.FillEllipse(new Ellipse(new RawVector2(Radius, Radius), Radius, Radius), d2dSolidWhiteBrush);
        d2dSolidWhiteBrush.Color = new Color(1f, 1f, 1f, Opacity);

        d2dContext.PushLayer(new LayerParameters1()
        {
            ContentBounds = new RawRectangleF(0, 0, Radius * 2, Radius * 2),
            GeometricMask = null,
            MaskAntialiasMode = AntialiasMode.Aliased,
            MaskTransform = new Matrix3x2(),
            Opacity = 1.0f,
            OpacityBrush = tempBrush,
            LayerOptions = LayerOptions1.None
        }, null);

        d2dContext.DrawImage(d2dScaleEffect, new RawVector2(-WorldOffsetX + Radius, (-WorldOffsetY + Radius)), InterpolationMode.NearestNeighbor);
        d2dContext.PopLayer();

        d2dContext.Target = dxMan.d2dRenderTargetBitmap;
    }

    public void Resize(DXManager pDXMan)
    {
        dxMan = pDXMan;
        d2dContext = pDXMan.d2dContext;

        DisposeResources();
        CreateResources();
    }

    public void Cleanup()
    {
        DisposeResources();

        if (d2dBitmapMain != null) { d2dBitmapMain.Dispose(); }
        if (d2dBitmapOffscreen != null) { d2dBitmapOffscreen.Dispose(); }
    }

    public void Zoom(float delta)
    {
        float v = (delta < 0) ? 1.1f : (delta > 0) ? 1.0f / 1.1f : 1.0f;
        ZoomScale = Math.Max(0.25f, Math.Min(ZoomScale *= v, 20f));
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
