// memctx: lê (só leitura) a memória de um processo, acha cada ocorrência exata de um texto e lista as strings
// ASCII imprimíveis em volta (a tabela de strings de um script costuma guardar juntos o dicionário da animscene
// e os nomes de papel passados a SET_ANIM_SCENE_ENTITY).
// Uso: memctx <processo> <saida.txt> <texto> [janela=1024] [maximo=40]
// Saída: um bloco por ocorrência, com o endereço e as strings da janela em ordem, a ocorrência marcada com ">>".
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

static class MemCtx
{
    [StructLayout(LayoutKind.Sequential)]
    struct MBI { public IntPtr Base, AllocBase; public uint AllocProtect; public IntPtr Size; public uint State, Protect, Type; }

    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, IntPtr len);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);

    const uint PROCESS_QUERY_INFORMATION = 0x400, PROCESS_VM_READ = 0x10, MEM_COMMIT = 0x1000;
    const uint PAGE_GUARD = 0x100, PAGE_NOACCESS = 0x01;

    static bool Printable(byte c) => c >= 0x20 && c < 0x7F;

    static int Main(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("uso: memctx <processo> <saida.txt> <texto> [janela=1024] [maximo=40]"); return 2; }
        byte[] needle = Encoding.ASCII.GetBytes(args[2]);
        int win = args.Length > 3 ? int.Parse(args[3]) : 1024;
        int max = args.Length > 4 ? int.Parse(args[4]) : 40;
        var p = Process.GetProcessesByName(args[0]).FirstOrDefault();
        if (p == null) { Console.Error.WriteLine("processo nao encontrado: " + args[0]); return 1; }
        var h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, p.Id);
        if (h == IntPtr.Zero) { Console.Error.WriteLine("OpenProcess falhou: " + Marshal.GetLastWin32Error()); return 1; }

        var sw = Stopwatch.StartNew();
        var outp = new StringBuilder();
        int hits = 0;
        var buf = new byte[16 << 20];
        IntPtr addr = IntPtr.Zero;
        try
        {
            while (hits < max && VirtualQueryEx(h, addr, out MBI m, (IntPtr)Marshal.SizeOf(typeof(MBI))) != IntPtr.Zero)
            {
                long baseL = m.Base.ToInt64(), size = m.Size.ToInt64();
                bool readable = m.State == MEM_COMMIT && (m.Protect & PAGE_GUARD) == 0 && (m.Protect & PAGE_NOACCESS) == 0 && m.Protect != 0;
                if (readable)
                {
                    for (long off = 0; off < size && hits < max; off += buf.Length)
                    {
                        long n = Math.Min(buf.Length, size - off);
                        if (!ReadProcessMemory(h, (IntPtr)(baseL + off), buf, (IntPtr)n, out IntPtr got)) continue;
                        int len = (int)got.ToInt64();
                        for (int i = 0; i + needle.Length <= len && hits < max; i++)
                        {
                            if (buf[i] != needle[0] || !Match(buf, i, needle)) continue;
                            // Só a string inteira: zero antes e depois.
                            if ((i > 0 && buf[i - 1] != 0) || (i + needle.Length < len && buf[i + needle.Length] != 0)) continue;
                            hits++;
                            int a = Math.Max(0, i - win), b = Math.Min(len, i + needle.Length + win);
                            outp.AppendLine("== 0x" + (baseL + off + i).ToString("X") + " protect=0x" + m.Protect.ToString("X") + " tipo=0x" + m.Type.ToString("X"));
                            int s = a;
                            for (int j = a; j <= b; j++)
                            {
                                bool end = j == b || !Printable(buf[j]);
                                if (!end) continue;
                                if (j - s >= 3) outp.AppendLine((s == i ? ">> " : "   ") + Encoding.ASCII.GetString(buf, s, j - s));
                                s = j + 1;
                            }
                            i += needle.Length;
                        }
                    }
                }
                long next = baseL + size;
                if (next <= addr.ToInt64()) break;
                addr = (IntPtr)next;
            }
        }
        finally { CloseHandle(h); }

        File.WriteAllText(args[1], outp.ToString());
        Console.WriteLine(args[1] + " ocorrencias=" + hits + " em " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s");
        return 0;
    }

    static bool Match(byte[] buf, int i, byte[] needle)
    {
        for (int k = 1; k < needle.Length; k++) if (buf[i + k] != needle[k]) return false;
        return true;
    }
}
