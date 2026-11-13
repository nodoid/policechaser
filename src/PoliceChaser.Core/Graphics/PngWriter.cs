using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;

namespace PoliceChaser.Graphics
{
    /// <summary>
    /// Minimal PNG encoder used only by the self-test harness to save a frame for inspection.
    /// Avoids any dependency on System.Drawing, which is not available on all targets.
    /// </summary>
    public static class PngWriter
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        public static void Write(string path, int width, int height, Color[] pixels)
        {
            if (pixels == null || pixels.Length < width * height)
            {
                throw new ArgumentException("Pixel buffer is too small", nameof(pixels));
            }

            using var stream = File.Create(path);

            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            stream.Write(signature, 0, signature.Length);

            byte[] header = new byte[13];
            WriteBigEndian(header, 0, (uint)width);
            WriteBigEndian(header, 4, (uint)height);
            header[8] = 8;   // bit depth
            header[9] = 6;   // truecolour with alpha
            header[10] = 0;
            header[11] = 0;
            header[12] = 0;
            WriteChunk(stream, "IHDR", header);

            var raw = new byte[height * (width * 4 + 1)];
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                raw[index++] = 0; // no filter
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    Color c = pixels[rowStart + x];
                    raw[index++] = c.R;
                    raw[index++] = c.G;
                    raw[index++] = c.B;
                    raw[index++] = c.A;
                }
            }

            byte[] compressed;
            using (var memory = new MemoryStream())
            {
                using (var zlib = new ZLibStream(memory, CompressionLevel.Optimal, true))
                {
                    zlib.Write(raw, 0, raw.Length);
                }

                compressed = memory.ToArray();
            }

            WriteChunk(stream, "IDAT", compressed);
            WriteChunk(stream, "IEND", Array.Empty<byte>());
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            byte[] length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            stream.Write(length, 0, 4);

            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes, 0, 4);
            stream.Write(data, 0, data.Length);

            uint crc = Crc32(typeBytes, data);
            byte[] crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc);
            stream.Write(crcBytes, 0, 4);
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < type.Length; i++)
            {
                crc = CrcTable[(crc ^ type[i]) & 0xFF] ^ (crc >> 8);
            }

            for (int i = 0; i < data.Length; i++)
            {
                crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                table[n] = c;
            }

            return table;
        }

        private static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}
