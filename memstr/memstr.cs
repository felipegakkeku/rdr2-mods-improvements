// memstr: lê (só leitura) a memória de um processo e lista as strings ASCII terminadas em zero que contêm
// um trecho (padrão '@', o formato dos dicionários de animação e de animscene do RDR2, ex.: script@beat@...).
// O trecho é comparado sem diferenciar maiúsculas (ex.: "pbl_" acha as playlists PBL_...).
// Uso: memstr <processo> <saida.txt> [minimo=8] [trecho=@]
// Saída: uma string por linha, "contagem<TAB>string", sem repetição, em ordem alfabética.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

static class MemStr
{
    [StructLayout(LayoutKind.Sequential)]
    struct MBI { public IntPtr Base, AllocBase; public uint AllocProtect; public IntPtr Size; public uint State, Protect, Type; }

    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, IntPtr len);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);

    const uint PROCESS_QUERY_INFORMATION = 0x400, PROCESS_VM_READ = 0x10, MEM_COMMIT = 0x1000;
    const uint PAGE_GUARD = 0x100, PAGE_NOACCESS = 0x01;

    static bool Ok(byte c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                              c == '_' || c == '@' || c == '/' || c == '-';

    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("uso: memstr <processo> <saida.txt> [minimo=8] [trecho=@]"); return 2; }
        int min = args.Length > 2 ? int.Parse(args[2]) : 8;
        string part = args.Length > 3 ? args[3] : "@";
        var p = Process.GetProcessesByName(args[0]).FirstOrDefault();
        if (p == null) { Console.Error.WriteLine("processo nao encontrado: " + args[0]); return 1; }
        var h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, p.Id);
        if (h == IntPtr.Zero) { Console.Error.WriteLine("OpenProcess falhou: " + Marshal.GetLastWin32Error()); return 1; }

        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        var sw = Stopwatch.StartNew();
        long scanned = 0;
        var buf = new byte[16 << 20];
        var sb = new StringBuilder();
        IntPtr addr = IntPtr.Zero;
        try
        {
            while (VirtualQueryEx(h, addr, out MBI m, (IntPtr)Marshal.SizeOf(typeof(MBI))) != IntPtr.Zero)
            {
                long baseL = m.Base.ToInt64(), size = m.Size.ToInt64();
                bool readable = m.State == MEM_COMMIT && (m.Protect & PAGE_GUARD) == 0 && (m.Protect & PAGE_NOACCESS) == 0 && m.Protect != 0;
                if (readable)
                {
                    for (long off = 0; off < size; off += buf.Length)
                    {
                        long n = Math.Min(buf.Length, size - off);
                        if (!ReadProcessMemory(h, (IntPtr)(baseL + off), buf, (IntPtr)n, out IntPtr got)) continue;
                        int len = (int)got.ToInt64();
                        scanned += len;
                        // Strings cortadas na borda do bloco se perdem; aceitável para um inventário.
                        sb.Clear();
                        for (int i = 0; i <= len; i++)
                        {
                            byte c = i < len ? buf[i] : (byte)0;
                            if (i < len && Ok(c)) { sb.Append((char)c); continue; }
                            if (sb.Length >= min && c == 0)
                            {
                                var s = sb.ToString();
                                if (s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                                    found[s] = found.TryGetValue(s, out int k) ? k + 1 : 1;
                            }
                            sb.Clear();
                        }
                    }
                }
                long next = baseL + size;
                if (next <= addr.ToInt64()) break;
                addr = (IntPtr)next;
            }
        }
        finally { CloseHandle(h); }

        File.WriteAllLines(args[1], found.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value + "\t" + kv.Key));
        Console.WriteLine(args[1] + " strings=" + found.Count + " lidos=" + (scanned >> 20) + "MB em " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s");
        return 0;
    }
}
