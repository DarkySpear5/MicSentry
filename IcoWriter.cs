using System.Drawing.Imaging;

namespace MicSentry;

// Minimal multi-resolution .ico writer using PNG-compressed frames, which every
// Windows version since Vista accepts. Lets us ship one real icon file built from
// several sizes of the same in-code star drawing, instead of a single fixed-size one.
internal static class IcoWriter
{
    public static void Save(string path, IReadOnlyList<Bitmap> images)
    {
        var pngBlobs = new List<byte[]>();
        foreach (var bmp in images)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            pngBlobs.Add(ms.ToArray());
        }

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // type: icon
        writer.Write((ushort)images.Count);

        int offset = 6 + images.Count * 16;
        for (int i = 0; i < images.Count; i++)
        {
            var bmp = images[i];
            writer.Write((byte)(bmp.Width >= 256 ? 0 : bmp.Width));
            writer.Write((byte)(bmp.Height >= 256 ? 0 : bmp.Height));
            writer.Write((byte)0);   // color count (0 = truecolor)
            writer.Write((byte)0);   // reserved
            writer.Write((ushort)1); // color planes
            writer.Write((ushort)32); // bits per pixel
            writer.Write(pngBlobs[i].Length);
            writer.Write(offset);

            offset += pngBlobs[i].Length;
        }

        foreach (var blob in pngBlobs)
            writer.Write(blob);
    }
}
