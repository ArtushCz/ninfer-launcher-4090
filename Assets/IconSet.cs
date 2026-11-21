using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NinferLauncher_v_2;

/// <summary>
/// Ikony z PNG souboru /Assets/*.png.
/// Zakladni ikony jsou 48–50 px, verze _mini 16 px (tlacitka/klikaci akce),
/// verze _large 64 px (menu). Chybici ikony se nahradi error.png.
/// </summary>
public static class IconSet
{
    const string BaseUri = "avares://NinferLauncher-v-2/Assets/";

    static readonly Dictionary<string, IImage> _cache = new();
    static readonly object _lock = new();

    // menu (26 px) – verze _large (64 px), jinak zakladni 48–50 px
    public static IImage Chat    => Get("chat_large.png");
    public static IImage ChatNew => Get("new_chat.png");
    public static IImage History => Get("history_large.png");
    public static IImage Settings => Get("settings_large.png");
    public static IImage Server   => Get("server.png");
    public static IImage ServerProfile => Get("server_profile.png");

    // klikaci akce / tlacitka (16 px) – verze _mini, jinak zakladni
    public static IImage Start   => Get("check_mini.png");
    public static IImage Stop    => Get("cross.png");
    public static IImage Restart => Get("restart_mini.png");
    public static IImage Send    => Get("send.png");
    public static IImage Trash   => Get("trash_mini.png");
    public static IImage Save    => Get("save_mini.png");
    public static IImage Copy    => Get("copy_mini.png");

    // samostatne ikony (48–50 px)
    public static IImage Eye        => Get("eye.png");
    public static IImage EyeOff     => Get("eye_off.png");
    public static IImage Folder     => Get("folder_mini.png");
    public static IImage FolderPlus => Get("folder_plus.png");
    public static IImage Link       => Get("link.png");
    public static IImage Attachment => Get("attachment.png");
    public static IImage Chats      => Get("chats.png");
    public static IImage Info       => Get("info.png");
    public static IImage Warning    => Get("warning.png");

    // doposud bez PNG – vraci error.png
    public static IImage Token => Get("token.png");

    static IImage Get(string file)
    {
        string key = file;
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var hit))
                return hit;
        }

        var img = TryLoadFile(key) ?? GetErrorIcon();
        lock (_lock) _cache[key] = img;
        return img;
    }

    static readonly Lazy<IImage> _error = new(() =>
        TryLoadFile("error.png")
        ?? new WriteableBitmap(new PixelSize(24, 24), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul));

    /// <summary>Zalozni ikona pro chybějici PNG.</summary>
    static IImage GetErrorIcon() => _error.Value;

    /// <summary>Nalodi PNG z /Assets; vraci null, kdyz soubor neexistuje.</summary>
    static IImage? TryLoadFile(string file)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(BaseUri + file));
            return Autocrop(WriteableBitmap.Decode(stream));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Crops the transparent bitmap edges down to the content (alpha bounds)
    /// and adds a thin border band (~10 %) so the glyph fills the display box.
    /// </summary>
    static WriteableBitmap Autocrop(WriteableBitmap src)
    {
        int W = src.PixelSize.Width, H = src.PixelSize.Height;
        byte[] px = new byte[W * H * 4];
        var pin = GCHandle.Alloc(px, GCHandleType.Pinned);
        try
        {
            src.CopyPixels(new PixelRect(0, 0, W, H), pin.AddrOfPinnedObject(), px.Length, W * 4);
        }
        finally { pin.Free(); }

        // ohranici nepruzhledneho obsahu
        int minx = W, miny = H, maxx = -1, maxy = -1;
        for (int y = 0; y < H; y++)
        {
            int rowOff = y * W * 4;
            for (int x = 0; x < W; x++)
            {
                if (px[rowOff + x * 4 + 3] > 8)
                {
                    if (x < minx) minx = x;
                    if (x > maxx) maxx = x;
                    if (y < miny) miny = y;
                    if (y > maxy) maxy = y;
                }
            }
        }

        // zcela pruzhledna – nechte jak je
        if (maxx < 0)
        {
            return src;
        }

        int w = maxx - minx + 1, hgt = maxy - miny + 1;
        int padX = Math.Min((int)(w * 0.10), (W - w) / 2);
        int padY = Math.Min((int)(hgt * 0.10), (H - hgt) / 2);
        int nw = w + 2 * padX, nh = hgt + 2 * padY;
        int ox = minx - padX, oy = miny - padY;

        var outBmp = new WriteableBitmap(new PixelSize(nw, nh), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Unpremul);

        unsafe
        {
            using var dst = outBmp.Lock();
            byte* d = (byte*)dst.Address;
            int stride = dst.RowBytes;
            for (int y = 0; y < nh; y++)
            {
                int sy = oy + y;
                byte* row = d + y * stride;
                for (int x = 0; x < nw; x++)
                {
                    int di = x * 4;
                    int sx = ox + x;
                    if (sx < 0 || sx >= W)
                    {
                        row[di] = row[di + 1] = row[di + 2] = row[di + 3] = 0;
                        continue;
                    }
                    int si = (sy * W + sx) * 4;
                    row[di] = px[si];
                    row[di + 1] = px[si + 1];
                    row[di + 2] = px[si + 2];
                    row[di + 3] = px[si + 3];
                }
            }
        }
        return outBmp;
    }
}