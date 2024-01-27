namespace YeahGame;

using Color = (byte R, byte G, byte B);
using TransparentColor = (byte R, byte G, byte B, byte A);

public class Png
{
    struct InformationChunk
    {
        public uint width;
        public uint height;
        public byte bitDepth;
        public byte colorType;
        public const byte compressionMethod = 0;
        public const byte filterMethod = 0;
        public byte interlaceMethod;

        /// <exception cref="NotImplementedException"/>
        public void Decode(ReadOnlySpan<byte> data, ref int i)
        {
            this.width = GetInt(data, ref i);
            this.height = GetInt(data, ref i);

            this.bitDepth = GetByte(data, ref i);
            this.colorType = GetByte(data, ref i);

            byte compressionMethod = GetByte(data, ref i);
            if (compressionMethod != 0)
            { throw new NotImplementedException($"Invalid compression method {compressionMethod}"); }

            byte filterMethod = GetByte(data, ref i);
            if (filterMethod != 0)
            { throw new NotImplementedException($"Invalid filter method {filterMethod}"); }

            this.interlaceMethod = GetByte(data, ref i);

            if (colorType != 6)
            { throw new NotImplementedException("We only support true-color with alpha"); }
            if (bitDepth != 8)
            { throw new NotImplementedException("We only support a bit depth of 8"); }
            if (interlaceMethod != 0)
            { throw new NotImplementedException("We only support no interlacing"); }
        }
    }

    InformationChunk Informations;
    readonly List<byte> Data = new();

    static uint SwapEndianness(uint x) =>
        ((x & 0x000000ff) << 24) +  // First byte
        ((x & 0x0000ff00) << 8) +   // Second byte
        ((x & 0x00ff0000) >> 8) +   // Third byte
        ((x & 0xff000000) >> 24);   // Fourth byte

    static ushort SwapEndianness(ushort x) => (ushort)(
        ((x & 0x00ff) << 8) +       // First byte
        ((x & 0xff00) >> 8));       // Second byte

    static bool DecodeHeader(byte[] data, ref int i)
    {
        i++;
        string signatureText = System.Text.Encoding.ASCII.GetString(data, i, 3);
        i += 3;
        i += 2;
        i++;
        i++;
        if (signatureText != "PNG") return false;

        return true;
    }

    static uint GetInt(byte[] data, ref int i)
    {
        uint v = BitConverter.ToUInt32(data, i);
        i += 4;
        if (BitConverter.IsLittleEndian)
        { v = SwapEndianness(v); }
        return v;
    }
    static uint GetInt(ReadOnlySpan<byte> data, ref int i)
    {
        uint v = BitConverter.ToUInt32(data.Slice(i, 4));
        i += 4;
        if (BitConverter.IsLittleEndian)
        { v = SwapEndianness(v); }
        return v;
    }
    static byte GetByte(ReadOnlySpan<byte> data, ref int i)
    {
        return data[i++];
    }

    bool DecodeChunk(byte[] data, ref int i)
    {
        uint length = GetInt(data, ref i);
        string type = System.Text.Encoding.ASCII.GetString(data, i, 4);
        i += 4;
        ReadOnlySpan<byte> chunkData = data.AsSpan(i, (int)length);
        i += (int)length;
        uint crc = BitConverter.ToUInt32(data, i);
        i += 4;

        return DecodeChunkData(chunkData, type);
    }

    static byte[] Decompress(byte[] data)
    {
        MemoryStream input = new(data);

        ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream zStream = new(input);

        MemoryStream output = new();
        zStream.CopyTo(output);

        return output.ToArray();
    }

    bool DecodeChunkData(ReadOnlySpan<byte> data, string type)
    {
        switch (type)
        {
            case "IHDR":
            {
                int i = 0;
                Informations = new InformationChunk();
                Informations.Decode(data, ref i);
                return true;
            }
            case "IDAT":
            {
                Data.AddRange(data.ToArray());
                return true;
            }
            default: return false;
        }
    }

    static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;

        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc)
        { return a; }
        else if (pb <= pc)
        { return b; }
        else
        { return c; }
    }

    const int bytesPerPixel = 4;

    TransparentImage LoadFileInternal(string file)
    {
        byte[] rawFileData = File.ReadAllBytes(file);
        byte[] imageData = LoadImageData(rawFileData);
        return GenerateImage(imageData);
    }

    Image LoadFileInternal(string file, Color background)
    {
        byte[] rawFileData = File.ReadAllBytes(file);
        byte[] imageData = LoadImageData(rawFileData);
        return GenerateImage(imageData, background);
    }

    byte[] LoadImageData(byte[] rawFileData)
    {
        int pixelIndex = 0;

        DecodeHeader(rawFileData, ref pixelIndex);
        while (pixelIndex < rawFileData.Length)
        {
            DecodeChunk(rawFileData, ref pixelIndex);
        }

        byte[] imageData = Data.ToArray();
        imageData = Decompress(imageData);

        int expectedLength = (int)(Informations.height * (1 + Informations.width * bytesPerPixel));

        int stride = (int)(Informations.width * bytesPerPixel);

        List<byte> reconstructed = new((int)Informations.height * stride);

        int Recon_a(int r, int c)
        {
            return c >= bytesPerPixel ? reconstructed[r * stride + c - bytesPerPixel] : 0;
        }

        int Recon_b(int r, int c)
        {
            return r > 0 ? reconstructed[(r - 1) * stride + c] : 0;
        }

        int Recon_c(int r, int c)
        {
            return (r > 0 && c >= bytesPerPixel) ? reconstructed[(r - 1) * stride + c - bytesPerPixel] : 0;
        }

        pixelIndex = 0;

        for (int r = 0; r < Informations.height; r++)
        {
            //  for each scanline
            byte filterType = imageData[pixelIndex]; //  first byte of scanline is filter type
            pixelIndex += 1;
            for (int c = 0; c < stride; c++)
            {//  for each byte in scanline
                byte p = imageData[pixelIndex];
                pixelIndex += 1;
                int reconstructedP;
                if (filterType == 0) //  None
                { reconstructedP = p; }
                else if (filterType == 1) //  Sub
                { reconstructedP = p + Recon_a(r, c); }
                else if (filterType == 2) //  Up
                { reconstructedP = p + Recon_b(r, c); }
                else if (filterType == 3) //  Average
                { reconstructedP = p + (Recon_a(r, c) + Recon_b(r, c)); }// 2
                else if (filterType == 4) //  Paeth
                { reconstructedP = p + PaethPredictor(Recon_a(r, c), Recon_b(r, c), Recon_c(r, c)); }
                else
                { throw new NotImplementedException($"Unknown filter type {filterType}"); }
                reconstructed.Add((byte)(reconstructedP & byte.MaxValue)); // truncation to byte
            }
        }

        return reconstructed.ToArray();
    }

    TransparentImage GenerateImage(byte[] reconstructed)
    {
        List<TransparentColor> pixels = new();

        for (int j = 0; j < reconstructed.Length; j += bytesPerPixel)
        {
            int r = reconstructed[j + 0];
            int g = reconstructed[j + 1];
            int b = reconstructed[j + 2];
            int a = reconstructed[j + 3];

            pixels.Add(new TransparentColor((byte)r, (byte)g, (byte)b, (byte)a));
            /*
            float alpha = (float)a / (float)byte.MaxValue;
            alpha = Math.Clamp(alpha, byte.MinValue, byte.MaxValue);

            Color color = Color.From24bitRGB(r, g, b) * alpha;
            Color backgroundColor_ = backgroundColor * (1f - alpha);
            pixels.Add(color + backgroundColor_);
            */
        }

        return new TransparentImage(pixels.ToArray(), (int)Informations.width, (int)Informations.height);
    }

    Image GenerateImage(byte[] reconstructed, Color background)
    {
        List<Color> pixels = new();

        for (int j = 0; j < reconstructed.Length; j += bytesPerPixel)
        {
            int r = reconstructed[j + 0];
            int g = reconstructed[j + 1];
            int b = reconstructed[j + 2];
            int a = reconstructed[j + 3];

            float alpha = (float)a / (float)byte.MaxValue;
            alpha = Math.Clamp(alpha, byte.MinValue, byte.MaxValue);

            Color color = new((byte)(r * alpha), (byte)(g * alpha), (byte)(b * alpha));
            Color backgroundColor_ = new((byte)(background.R * (1f - alpha)), (byte)(background.G * (1f - alpha)), (byte)(background.B * (1f - alpha)));
            pixels.Add(new Color((byte)(color.R + backgroundColor_.R), (byte)(color.G + backgroundColor_.G), (byte)(color.B + backgroundColor_.B)));
        }

        return new Image(pixels.ToArray(), (int)Informations.width, (int)Informations.height);
    }

    public static TransparentImage LoadFile(string file) => new Png().LoadFileInternal(file);
    public static Image LoadFile(string file, Color background) => new Png().LoadFileInternal(file, background);
}
