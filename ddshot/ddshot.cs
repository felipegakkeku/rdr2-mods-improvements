// ddshot: print de um monitor por DXGI Desktop Duplication (pega jogo Vulkan/DX em janela sem borda,
// onde a captura GDI devolve preto). Uso: ddshot <saida.png> [x y] [largura]
// x,y escolhe o monitor que contém o ponto (padrão 0,0); largura redimensiona a saída (padrão: original).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

static unsafe class DdShot
{
    [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr f);
    [DllImport("d3d11.dll")]
    static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr soft, uint flags, IntPtr levels,
        uint nLevels, uint sdk, out IntPtr device, out int level, out IntPtr context);

    static Guid IID_Factory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
    static Guid IID_Output1 = new Guid("00cd7c03-1ea6-4c43-a7b6-0b7d6bdd5ce3");
    static Guid IID_Tex2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    static IntPtr Fn(IntPtr obj, int slot) { return (*(IntPtr**)obj)[slot]; }
    static void Release(IntPtr obj) { if (obj != IntPtr.Zero) ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Fn(obj, 2))(obj); }
    static int QI(IntPtr obj, ref Guid iid, out IntPtr res)
    {
        IntPtr r;
        fixed (Guid* g = &iid)
        {
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)Fn(obj, 0))(obj, g, &r);
            res = r;
            return hr;
        }
    }
    static void Check(int hr, string what) { if (hr < 0) throw new Exception(what + " hr=0x" + hr.ToString("X8")); }

    [StructLayout(LayoutKind.Sequential)]
    struct OutputDesc
    {
        public fixed char Name[32];
        public int L, T, R, B;
        public int Attached, Rotation;
        public IntPtr Monitor;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TexDesc { public uint W, H, Mips, Array, Format, SCount, SQuality, Usage, Bind, Cpu, Misc; }

    [StructLayout(LayoutKind.Sequential)]
    struct FrameInfo { public long LastPresent, LastMouse; public uint Accum; public int Coalesced, Masked, PX, PY, PVis; public uint Meta, Shape; }

    [StructLayout(LayoutKind.Sequential)]
    struct Mapped { public IntPtr Data; public uint Row, Depth; }

    sealed class Veil : System.Windows.Forms.Form
    {
        public Veil(Rectangle bounds)
        {
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            Bounds = bounds;
            BackColor = Color.Black;
            Opacity = 0.01;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override System.Windows.Forms.CreateParams CreateParams
        {
            get
            {
                // NOACTIVATE | TRANSPARENT (clique passa) | LAYERED | TOPMOST | TOOLWINDOW (fora do Alt+Tab)
                var p = base.CreateParams;
                p.ExStyle |= 0x08000000 | 0x20 | 0x80000 | 0x8 | 0x80;
                return p;
            }
        }
    }

    // Amostra o quadro numa cópia em memória de CPU: preto puro = quadro sem o jogo.
    static bool IsBlack(IntPtr device, IntPtr context, IntPtr resource)
    {
        IntPtr tex;
        if (QI(resource, ref IID_Tex2D, out tex) < 0) return false;
        TexDesc td;
        ((delegate* unmanaged[Stdcall]<IntPtr, TexDesc*, void>)Fn(tex, 10))(tex, &td);
        var sd = td;
        sd.Mips = 1; sd.Array = 1; sd.SCount = 1; sd.SQuality = 0; sd.Usage = 3; sd.Bind = 0; sd.Cpu = 0x20000; sd.Misc = 0;
        IntPtr staging;
        if (((delegate* unmanaged[Stdcall]<IntPtr, TexDesc*, IntPtr, IntPtr*, int>)Fn(device, 5))(device, &sd, IntPtr.Zero, &staging) < 0)
        { Release(tex); return false; }
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>)Fn(context, 47))(context, staging, tex);
        Mapped m;
        long sum = 0, n = 0;
        if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, uint, Mapped*, int>)Fn(context, 14))(context, staging, 0, 1, 0, &m) >= 0)
        {
            for (uint y = 0; y < td.H; y += 32)
                for (uint x = 0; x < td.W; x += 32)
                {
                    byte* px = (byte*)m.Data + y * m.Row + x * 4;
                    sum += px[0] + px[1] + px[2];
                    n++;
                }
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)Fn(context, 15))(context, staging, 0);
        }
        Release(staging);
        Release(tex);
        return n > 0 && sum / (3.0 * n) < 1.0;
    }

    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("uso: ddshot <saida.png> [x y] [largura]"); return 2; }
        int px = args.Length >= 3 ? int.Parse(args[1]) : 0, py = args.Length >= 3 ? int.Parse(args[2]) : 0;
        int width = args.Length >= 4 ? int.Parse(args[3]) : 0;
        try { Capture(args[0], px, py, width); return 0; }
        catch (Exception ex) { Console.Error.WriteLine("erro: " + ex.Message); return 1; }
    }

    static void Capture(string path, int px, int py, int width)
    {
        IntPtr factory;
        Check(CreateDXGIFactory1(ref IID_Factory1, out factory), "CreateDXGIFactory1");
        IntPtr adapter = IntPtr.Zero, output = IntPtr.Zero;
        OutputDesc od = default(OutputDesc);
        for (uint a = 0; output == IntPtr.Zero; a++)
        {
            IntPtr ad;
            if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Fn(factory, 12))(factory, a, &ad) < 0) break;
            for (uint o = 0; ; o++)
            {
                IntPtr op;
                if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Fn(ad, 7))(ad, o, &op) < 0) break;
                OutputDesc d;
                ((delegate* unmanaged[Stdcall]<IntPtr, OutputDesc*, int>)Fn(op, 7))(op, &d);
                if (px >= d.L && px < d.R && py >= d.T && py < d.B) { adapter = ad; output = op; od = d; break; }
                Release(op);
            }
            if (output == IntPtr.Zero) Release(ad);
        }
        if (output == IntPtr.Zero) throw new Exception("nenhum monitor contem " + px + "," + py);

        IntPtr device, context;
        int level;
        Check(D3D11CreateDevice(adapter, 0, IntPtr.Zero, 0, IntPtr.Zero, 0, 7, out device, out level, out context), "D3D11CreateDevice");

        IntPtr out1;
        if (QI(output, ref IID_Output1, out out1) < 0) out1 = output;
        IntPtr dupl = IntPtr.Zero;

        // Jogo em independent flip (RDR2 Vulkan sem borda) não passa pela composição do Windows: a duplicação
        // devolve preto. Uma película quase transparente, sem foco e que deixa o clique passar, alternada a cada
        // tentativa, obriga o DWM a compor um quadro com o jogo.
        var veil = new Veil(new Rectangle(od.L, od.T, od.R - od.L, od.B - od.T));
        veil.Show();
        System.Windows.Forms.Application.DoEvents();

        // O primeiro quadro pode vir sem imagem (LastPresent=0): tenta alguns. ACCESS_LOST (troca de modo,
        // jogo entrando em tela cheia) invalida a duplicação: recria.
        IntPtr resource = IntPtr.Zero;
        int lastHr = 0;
        for (int i = 0; i < 30 && resource == IntPtr.Zero; i++)
        {
            veil.Opacity = i % 2 == 0 ? 0.01 : 0.02;
            System.Windows.Forms.Application.DoEvents();
            if (dupl == IntPtr.Zero)
            {
                IntPtr d;
                lastHr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int>)Fn(out1, 22))(out1, device, &d);
                if (lastHr < 0) { Console.Error.WriteLine("DuplicateOutput hr=0x" + lastHr.ToString("X8")); System.Threading.Thread.Sleep(200); continue; }
                dupl = d;
            }
            FrameInfo fi;
            IntPtr res;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint, FrameInfo*, IntPtr*, int>)Fn(dupl, 8))(dupl, 500, &fi, &res);
            if (hr < 0)
            {
                lastHr = hr;
                if (Environment.GetEnvironmentVariable("DDSHOT_DEBUG") != null) Console.Error.WriteLine(i + " AcquireNextFrame hr=0x" + hr.ToString("X8"));
                // Fora o timeout (WAIT_TIMEOUT), a duplicação ficou inválida (ACCESS_LOST, INVALID_CALL): recria.
                if (hr != unchecked((int)0x887A0027)) { Release(dupl); dupl = IntPtr.Zero; System.Threading.Thread.Sleep(50); }
                continue;
            }
            bool black = fi.LastPresent != 0 && IsBlack(device, context, res);
            if (Environment.GetEnvironmentVariable("DDSHOT_DEBUG") != null)
                Console.Error.WriteLine(i + " quadro present=" + fi.LastPresent + " acum=" + fi.Accum + " preto=" + black);
            if ((fi.LastPresent != 0 && !black) || i >= 25) { resource = res; break; }
            Release(res);
            ((delegate* unmanaged[Stdcall]<IntPtr, int>)Fn(dupl, 14))(dupl);
        }
        veil.Close();
        if (resource == IntPtr.Zero) throw new Exception("AcquireNextFrame nao devolveu quadro, hr=0x" + lastHr.ToString("X8"));

        IntPtr tex;
        Check(QI(resource, ref IID_Tex2D, out tex), "QI Texture2D");
        TexDesc td;
        ((delegate* unmanaged[Stdcall]<IntPtr, TexDesc*, void>)Fn(tex, 10))(tex, &td);
        var sd = td;
        sd.Mips = 1; sd.Array = 1; sd.SCount = 1; sd.SQuality = 0; sd.Usage = 3; sd.Bind = 0; sd.Cpu = 0x20000; sd.Misc = 0;
        IntPtr staging;
        Check(((delegate* unmanaged[Stdcall]<IntPtr, TexDesc*, IntPtr, IntPtr*, int>)Fn(device, 5))(device, &sd, IntPtr.Zero, &staging), "CreateTexture2D");
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>)Fn(context, 47))(context, staging, tex);
        Mapped m;
        Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, uint, Mapped*, int>)Fn(context, 14))(context, staging, 0, 1, 0, &m), "Map");

        int w = (int)td.W, h = (int)td.H;
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb))
        {
            var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            for (int y = 0; y < h; y++)
                Buffer.MemoryCopy((byte*)m.Data + y * m.Row, (byte*)bd.Scan0 + y * bd.Stride, bd.Stride, w * 4);
            bmp.UnlockBits(bd);
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)Fn(context, 15))(context, staging, 0);

            double sum = 0;
            int n = 0;
            for (int y = 0; y < h; y += 16)
                for (int x = 0; x < w; x += 16)
                {
                    var c = bmp.GetPixel(x, y);
                    sum += 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                    n++;
                }

            int ow = width > 0 && width < w ? width : w, oh = h * ow / w;
            using (var outBmp = new Bitmap(ow, oh, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(outBmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, 0, 0, ow, oh);
                }
                outBmp.Save(path, path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png);
            }
            Console.WriteLine(path + " monitor=" + od.L + "," + od.T + " " + w + "x" + h + " formato=" + td.Format +
                " brilho=" + (sum / n).ToString("0.0"));
        }

        Release(staging); Release(tex); Release(resource);
        ((delegate* unmanaged[Stdcall]<IntPtr, int>)Fn(dupl, 14))(dupl);
        Release(dupl);
        if (out1 != output) Release(out1);
        Release(output); Release(context); Release(device); Release(adapter); Release(factory);
    }
}
