using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GS_graph
{
    class Program
    {
        public class Palette
        {
            public Color[] colors;
            private int trans_id;

            public Palette(string fn)
            {
                using (FileStream stream = File.OpenRead(fn))
                {
                    colors = new Color[256];
                    stream.ReadByte();
                    byte[] buffer = new byte[2];
                    for (int i = 0; i < colors.Length; ++i)
                    {
                        stream.Read(buffer, 0, 2);
                        colors[i] = FromBGRA5551(BitConverter.ToInt16(buffer, 0));
                        if (colors[i].A == 0)
                        {
                            trans_id = i;
                        }
                    }
                }
            }

            public void WriteToFile(string fn)
            {
                using (FileStream stream = File.Open(fn, FileMode.CreateNew))
                {
                    stream.Write(new byte[] { 0x10 }, 0, 1);
                    for (int i = 0; i < colors.Length; ++i)
                    {
                        stream.Write(BitConverter.GetBytes(ToBGRA5551(colors[i])), 0, 2);
                    }
                }
            }

            public Palette(Color[] colors, int trans_id)
            {
                this.colors = colors;
                this.trans_id = trans_id;
            }

            private static Color FromBGRA5551(Int16 val)
            {
                int b = (val & ((1 << 5) - 1)) >> 0 << 3;
                int g = (val & ((1 << 10) - 1)) >> 5 << 3;
                int r = (val & ((1 << 15) - 1)) >> 10 << 3;
                int a = ((val & (1 << 15)) != 0) ? 255 : 0;
                return Color.FromArgb(a, r, g, b);
            }

            private static Int16 ToBGRA5551(Color c)
            {
                int b = (c.B & 0xF8) >> 3;
                int g = (c.G & 0xF8) >> 3;
                int r = (c.R & 0xF8) >> 3;
                int a = c.A == 0 ? 0 : 1;
                return (Int16) (b | g << 5 | r << 10 | a << 15);
            }

            public int TryFindColor(Color c)
            {
                for (int i = 0; i < colors.Length; ++i)
                {
                    if (colors[i] == c)
                    {
                        return i;
                    }
                }
                return trans_id;
            }
        }

        static void PrintHelpMessage()
        {
            Console.Out.WriteLine("---------------------------------");
            Console.Out.WriteLine("Help");
            Console.Out.WriteLine("Convert bmp files according to a given palette into cv2 files.");
            Console.Out.WriteLine("Can also be used to convert palette to and from bmp image.");
            Console.Out.WriteLine("When converting bmp to cv2, colors that can not be found in palette will " +
                                  "be converted into transparent color (if there is one in the palette).");

            Console.Out.WriteLine("             ");
            Console.Out.WriteLine("            Tasks:");
            Console.Out.WriteLine("              Default: Convert images(bmp/png) into cv2 files.");
            Console.Out.WriteLine("  -a          Convert palette to or from bitmap. Must have -p or -m");
            Console.Out.WriteLine("  -r          Convert cv2 files into images(png).");
            Console.Out.WriteLine("  -h          Print this message.");
            Console.Out.WriteLine("             ");
            Console.Out.WriteLine("            Options:");
            Console.Out.WriteLine("  -p <path>   Specify a palette file used in conversion.");
            Console.Out.WriteLine("  -m <path>   Specify a bitmap as input to create a palette with -a.");
            Console.Out.WriteLine("  -n          Don't use palette in this conversion.");
            Console.Out.WriteLine("  -f <path>   Convert a single file.");
            Console.Out.WriteLine("  -d <path>   Convert all files in a directory.");
            Console.Out.WriteLine("---------------------------------");
        }

        static void Error(string message)
        {
            Console.Out.WriteLine("[Error] " + message);
            PrintHelpMessage();
            Environment.Exit(1);
        }

        static void Warning(string message)
        {
            Console.Out.WriteLine("[Warn]  " + message);
        }

        static void Success(string message)
        {
            Console.Out.WriteLine("[OK]    " + message);
        }

        static void DoAllColor(string pfn)
        {
            if (pfn.Equals(""))
            {
                Error("You must provide a palette to use -a option.");
            }
            Palette p = new Palette(pfn);
            string fout = Path.ChangeExtension(pfn, "bmp");
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (FileStream file_out = File.Open(fout, FileMode.CreateNew))
            {
                for (int j = 0; j < 16; ++j)
                for (int i = 0; i < 16; ++i)
                {
                    Color c = p.colors[i + j * 16];
                    bitmap.SetPixel(2 * i + 0, 2 * j + 0, c);
                    bitmap.SetPixel(2 * i + 1, 2 * j + 0, c);
                    bitmap.SetPixel(2 * i + 0, 2 * j + 1, c);
                    bitmap.SetPixel(2 * i + 1, 2 * j + 1, c);
                }
                bitmap.Save(file_out, ImageFormat.Bmp);
            }
            Success("Write palette colors to " + fout);
        }

        static void DoAllColor_Reverse(string pbfn)
        {
            string fout = Path.ChangeExtension(pbfn, "pal");
            if (File.Exists(fout))
            {
                Error("Output file already exists: " + fout);
            }
            Color[] colors = new Color[256];
            using (Bitmap bitmap = new Bitmap(File.OpenRead(pbfn)))
            {
                if (bitmap.Width != bitmap.Height ||
                    (bitmap.Width != 16 && bitmap.Width != 32) ||
                    (bitmap.Height != 16 && bitmap.Height != 32))
                {
                    Error("Invalid bitmap size");
                }
                int scale = bitmap.Width / 16;
                for (int j = 0; j < 16; ++j)
                    for (int i = 0; i < 16; ++i)
                    {
                        Color c = bitmap.GetPixel(i * scale, j * scale);
                        colors[i + j * 16] = c;
                    }
            }
            colors[0] = Color.Transparent;
            Palette p = new Palette(colors, 0);
            p.WriteToFile(fout);
            Success("Write palette file to " + fout);
        }

        static void DoConvert(string p, List<string> dirs, List<string> files, bool has_transp, Color tr)
        {
            if (dirs.Count == 0 && files.Count == 0)
            {
                Error("No input.");
            }
            Palette pal = p.Equals("") ? null : new Palette(p);
            foreach (string dir in dirs)
            {
                int count = 0;
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext.Equals(".bmp") || ext.Equals(".png"))
                    {
                        if (ConvertBmpToCv2(pal, file)) ++count;
                    }
                }

                Success("Convert " + count + " bmp file(s) into cv2 in " + dir);
            }
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext.Equals(".bmp") || ext.Equals(".png"))
                {
                    ConvertBmpToCv2(pal, file);
                }
                else
                {
                    Warning("Unsupported image format: " + file);
                }
            }

            Success("Convert finished.");
        }

        static byte[] GetIntColor(Color c)
        {
            return new byte[] { c.B, c.G, c.R, c.A };
        }

        static bool ConvertBmpToCv2(string file)
        {
            string file_out = Path.ChangeExtension(file, "cv2");
            if (File.Exists(file_out))
            {
                Warning("File exist: " + file_out);
                return false;
            }
            using (FileStream f_out = File.Open(file_out, FileMode.CreateNew))
            using (FileStream f_in = File.OpenRead(file))
            using (Bitmap bitmap = new Bitmap(f_in))
            {
                f_out.Write(BitConverter.GetBytes((byte)32), 0, 1);
                f_out.Write(BitConverter.GetBytes((Int32)bitmap.Width), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)bitmap.Height), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)bitmap.Width), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)0), 0, 4);
                for (int j = 0; j < bitmap.Height; ++j)
                    for (int i = 0; i < bitmap.Width; ++i)
                    {
                        f_out.Write(GetIntColor(bitmap.GetPixel(i, j)), 0, 4);
                    }
            }
            Success("Convert image into cv2: " + file);
            return true;
        }

        static bool ConvertBmpToCv2(Palette p, string file)
        {
            if (p == null)
            {
                return ConvertBmpToCv2(file);
            }
            string file_out = Path.ChangeExtension(file, "cv2");
            if (File.Exists(file_out))
            {
                Warning("File exist: " + file_out);
                return false;
            }
            using (FileStream f_out = File.Open(file_out, FileMode.CreateNew))
            using (FileStream f_in = File.OpenRead(file))
            using (Bitmap bitmap = new Bitmap(f_in))
            {
                f_out.Write(BitConverter.GetBytes((byte)8), 0, 1);
                f_out.Write(BitConverter.GetBytes((Int32)bitmap.Width), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)bitmap.Height), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)bitmap.Width), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)0), 0, 4);
                for (int j = 0; j < bitmap.Height; ++j)
                    for (int i = 0; i < bitmap.Width; ++i)
                    {
                        f_out.Write(BitConverter.GetBytes((byte)p.TryFindColor(bitmap.GetPixel(i, j))), 0, 1);
                    }
            }
            Success("Convert image into cv2: " + file);
            return true;
        }

        static void DoConvert_Reverse(string p, List<string> dirs, List<string> files)
        {
            if (dirs.Count == 0 && files.Count == 0)
            {
                Error("No input.");
            }
            Palette pal = p.Equals("") ? null : new Palette(p);
            foreach (string dir in dirs)
            {
                int count = 0;
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext.Equals(".cv2"))
                    {
                        if (ConvertBmpToCv2_Reverse(pal, file)) ++count;
                    }
                }

                Success("Convert " + count + " cv2 file(s) into png in " + dir);
            }
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext.Equals(".cv2"))
                {
                    ConvertBmpToCv2_Reverse(pal, file);
                }
                else
                {
                    Warning("Not a cv2 file: " + file);
                }
            }

            Success("Convert finished.");
        }

        static bool ConvertBmpToCv2_Reverse(Palette p, string f)
        {
            string file_out = Path.ChangeExtension(f, "png");
            if (File.Exists(file_out))
            {
                Warning("File exist: " + file_out);
                return false;
            }
            using (FileStream f_in = File.OpenRead(f))
            {
                byte[] header = new byte[1 + 4 + 4 + 4 + 4];
                f_in.Read(header, 0, header.Length);
                int width = BitConverter.ToInt32(header, 1);
                int height = BitConverter.ToInt32(header, 5);
                int stride = BitConverter.ToInt32(header, 9);
                byte bit_depth = header[0];
                bool use_pal = (bit_depth == 8);
                if (use_pal && p == null)
                {
                    Warning("cv2 need a palette file: " + f);
                    return false;
                }
                if (bit_depth != 8 && bit_depth != 16 && bit_depth != 24 && bit_depth != 32)
                {
                    Warning("Unsupported bit depth: " + f);
                    return false;
                }
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    byte[] buf = new byte[4];
                    for (int j = 0; j < height; ++j) for (int i = 0; i < stride; ++i)
                    {
                        Color c = Color.White;
                        if (use_pal)
                        {
                            f_in.Read(buf, 0, 1);
                            c = p.colors[buf[0]];
                        }
                        else if (bit_depth == 32 || bit_depth == 24)
                        {
                            f_in.Read(buf, 0, 4);
                            c = Color.FromArgb(buf[3], buf[2], buf[1], buf[0]); //bgra8888
                        }
                        else if (bit_depth == 16)
                        {
                            f_in.Read(buf, 0, 2);
                            UInt16 u16 = BitConverter.ToUInt16(buf, 0);
                            int a = (u16 & ( 1 << 15)) >> 15;
                            int r = (u16 & (31 << 10)) >> 10;
                            int g = (u16 & (31 <<  5)) >>  5;
                            int b = (u16 & (31 <<  0)) >>  0;
                            c = Color.FromArgb(a * 255, r * 8, g * 8, b * 8); //bgra5551
                        }
                        if (i < width)
                            bitmap.SetPixel(i, j, c);
                    }
                    using (FileStream f_out = File.Open(file_out, FileMode.CreateNew))
                    {
                        bitmap.Save(f_out, ImageFormat.Png);
                    }
                }
            }
            Success("Convert cv2 into png: " + f);
            return true;
        }

        static string TryGetNext(string[] args, ref int i)
        {
            if (++i >= args.Length)
            {
                Error("Invalid arguments.");
                Environment.Exit(1);
                return null; //never get here
            }
            return args[i];
        }
        static void Main3()
        {
            string dir = @"E:\Games\[game]GRIEFSYNDROME\griefsyndrome\gs00";
            byte[] buffer = new byte[1];
            foreach (var file in Directory.EnumerateFiles(dir, "*.cv2", SearchOption.AllDirectories))
            {
                using (var s = File.OpenRead(file))
                {
                    s.Read(buffer, 0, 1);
                }
                if (buffer[0] != 8 && buffer[0] != 32 && buffer[0] != 24)
                {
                    int i = 0;
                    i = i / 1;
                }
            }
        }
        static void Main(string[] args)
        {
            Console.Out.WriteLine("GS Graph: cv2 generator. By acaly.");
            if (args.Length <= 0)
            {
                PrintHelpMessage();
                return;
            }
            //process args
            string the_palette_filename = null;
            string the_palette_bitmap_filename = null;
            bool is_all_color = false;
            bool is_reverse = false;
            Color transp = new Color();
            bool has_transp = false;
            List<string> directories = new List<string>();
            List<string> files = new List<string>();
            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i].Equals("-p"))
                {
                    //select a palette
                    if (the_palette_filename != null)
                    {
                        Error("No more palettes.");
                    }
                    the_palette_filename = args[++i];
                }
                else if (args[i].Equals("-h"))
                {
                    PrintHelpMessage();
                    return;
                }
                else if (args[i].Equals("-a"))
                {
                    if (is_all_color || is_reverse)
                    {
                        Error("Only one task a time.");
                    }
                    is_all_color = true;
                }
                else if (args[i].Equals("-r"))
                {
                    if (is_all_color || is_reverse)
                    {
                        Error("Only one task a time.");
                    }
                    is_reverse = true;
                }
                else if (args[i].Equals("-d"))
                {
                    directories.Add(args[++i]);
                }
                else if (args[i].Equals("-f"))
                {
                    files.Add(args[++i]);
                }
                else if (args[i].Equals("-t"))
                {
                    if (has_transp)
                    {
                        Error("No more transparent colors.");
                    }
                    int r, g, b;
                    r = byte.Parse(args[++i]);
                    g = byte.Parse(args[++i]);
                    b = byte.Parse(args[++i]);
                    transp = Color.FromArgb(255, r, g, b);
                    has_transp = true;
                }
                else if (args[i].Equals("-n"))
                {
                    if (the_palette_filename != null)
                    {
                        Error("Don't use option -n while providing a palette.");
                    }
                    the_palette_filename = "";
                }
                else if (args[i].Equals("-m"))
                {
                    if (the_palette_bitmap_filename != null)
                    {
                        Error("No more input bitmap.");
                    }
                    the_palette_bitmap_filename = args[++i];
                }
                else
                {
                    Error("Unknown command.");
                }
            }
            try
            {
                if (is_all_color)
                {
                    if (the_palette_filename == null && the_palette_bitmap_filename == null)
                    {
                        Error("You must specify an palette input.");
                    }
                    if (the_palette_filename != null && the_palette_bitmap_filename != null)
                    {
                        Error("You must specify only one palette input.");
                    }
                    if (directories.Count > 0 || files.Count > 0)
                    {
                        Error("When using -a, no input file or directory should be given.");
                    }
                    if (the_palette_filename != null)
                    {
                        DoAllColor(the_palette_filename);
                    }
                    else
                    {
                        DoAllColor_Reverse(the_palette_bitmap_filename);
                    }
                }
                else if (is_reverse)
                {
                    if (the_palette_filename == null)
                    {
                        Error("You must specify a palette.");
                    }
                    DoConvert_Reverse(the_palette_filename, directories, files);
                }
                else
                {
                    if (the_palette_filename == null)
                    {
                        Error("You must specify a palette.");
                    }
                    DoConvert(the_palette_filename, directories, files, has_transp, transp);
                }
            }
            catch (Exception e)
            {
                Error("Exception: " + e.ToString());
            }
            return;
            /*
            string fnin = "E:\\Games\\[game]GRIEFSYNDROME\\griefsyndrome\\gs00\\data\\actor\\madoka - 副本\\Madoka_run0009.cv2";
            string fnout = "E:\\Games\\[game]GRIEFSYNDROME\\griefsyndrome\\gs00\\data\\actor\\madoka - 副本\\Madoka_run0009a.cv2";
            FileStream f = File.OpenRead(fnin);
            byte[] data = new byte[100000];//100k
            f.Read(data, 0, data.Length);
            FileStream fout = File.Open(fnout, FileMode.Create);
            fout.Write(data, 0, 1 + 8 * 4 + 128 * 128);
            f.Dispose();
            fout.Dispose();
            */
            Palette pal;
            pal = new Palette("E:\\Games\\[game]GRIEFSYNDROME\\griefsyndrome\\gs00\\data\\actor\\madoka - 副本\\palette000.pal");
            Color c = pal.colors[0x63];
            string bmp_in = "E:\\Temp\\madoka_ani\\dds\\madoka0.bmp";
            using (FileStream f_out = File.Open("E:\\Temp\\madoka_ani\\dds\\madoka0.cv2", FileMode.CreateNew))
            using (FileStream f_in = File.OpenRead(bmp_in))
            using (Bitmap bitmap = new Bitmap(f_in))
            {
                f_out.Write(BitConverter.GetBytes((byte)8), 0, 1);
                f_out.Write(BitConverter.GetBytes((Int32)128), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)128), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)128), 0, 4);
                f_out.Write(BitConverter.GetBytes((Int32)0), 0, 4);
                for (int j = 0; j < bitmap.Height; ++j)
                for (int i = 0; i < bitmap.Width; ++i)
                {
                    f_out.Write(BitConverter.GetBytes((byte)pal.TryFindColor(bitmap.GetPixel(i, j))), 0, 1);
                }
            }
        }
    }
}
