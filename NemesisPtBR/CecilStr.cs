// Ferramenta de tradução dos textos embutidos no NemesisMod.dll (ldstr) usando o Mono.Cecil do Build Tools.
//   CecilStr dump  <dll> <saida.tsv>             lista cada ldstr com método, offset e o que consome o texto
//   CecilStr apply <dll> <mapa.tsv> <saida.dll>  troca os textos do mapa (original<TAB>tradução, escapes \t \n \\)
// No mapa, uma linha "@Tipo::Metodo<TAB>original<TAB>tradução" restringe a troca àquele método.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class CecilStr
{
    static string avisos;

    static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "dump") return Dump(args[1], args[2]);
        if (args.Length >= 4 && args[0] == "apply") { if (args.Length >= 6 && args[4] == "--avisos") avisos = args[5]; return Apply(args[1], args[2], args[3]); }
        if (args.Length >= 3 && args[0] == "il") return Il(args[1], args[2]);
        if (args.Length >= 3 && args[0] == "callers") return Callers(args[1], args[2]);
        if (args.Length >= 3 && args[0] == "field") return FieldUsers(args[1], args[2]);
        Console.Error.WriteLine("uso: CecilStr dump <dll> <saida.tsv> | apply <dll> <mapa.tsv> <saida.dll>");
        return 2;
    }

    static ModuleDefinition Open(string dll, bool rw)
    {
        var res = new DefaultAssemblyResolver();
        // A pasta do DLL e as de cima (com o scripts\ de cada uma): acha a API do SHRDN de dentro do _mod-lab.
        for (var d = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(dll))); d != null; d = d.Parent)
        {
            res.AddSearchDirectory(d.FullName);
            if (Directory.Exists(Path.Combine(d.FullName, "scripts"))) res.AddSearchDirectory(Path.Combine(d.FullName, "scripts"));
        }
        res.AddSearchDirectory(Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), @"Microsoft.NET\Framework64\v4.0.30319"));
        res.AddSearchDirectory(Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), @"Microsoft.NET\Framework64\v4.0.30319\WPF"));
        return ModuleDefinition.ReadModule(dll, new ReaderParameters { AssemblyResolver = res, ReadWrite = false, InMemory = true });
    }

    static IEnumerable<MethodDefinition> Methods(ModuleDefinition m)
    {
        foreach (var t in m.GetTypes())
            foreach (var md in t.Methods)
                if (md.HasBody) yield return md;
    }

    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
    static string Unesc(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char c = s[++i];
                sb.Append(c == 't' ? '\t' : c == 'n' ? '\n' : c == 'r' ? '\r' : c);
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // O que vem depois do ldstr até a primeira chamada: indica se o texto vai para a tela, vira chave, caminho etc.
    static string Consumer(Instruction ins)
    {
        var cur = ins.Next;
        for (int n = 0; cur != null && n < 12; n++, cur = cur.Next)
        {
            if (cur.OpCode.FlowControl == FlowControl.Call && cur.Operand is MethodReference mr)
                return mr.DeclaringType.Name + "." + mr.Name;
            if (cur.OpCode.Code == Code.Stfld || cur.OpCode.Code == Code.Stsfld)
                return "campo " + ((FieldReference)cur.Operand).Name;
            if (cur.OpCode.Code == Code.Ret) return "ret";
            if (cur.OpCode.Code == Code.Switch || cur.OpCode.FlowControl == FlowControl.Cond_Branch) return "desvio";
        }
        return "";
    }

    // Instruções vizinhas em forma curta (n < 0: anteriores; n > 0: seguintes).
    static string Ctx(Instruction ins, int n)
    {
        var parts = new List<string>();
        var cur = n < 0 ? ins.Previous : ins.Next;
        for (int i = 0; cur != null && i < Math.Abs(n); i++, cur = n < 0 ? cur.Previous : cur.Next)
        {
            string op = cur.OpCode.Name;
            if (cur.Operand is MethodReference mr) op += " " + mr.DeclaringType.Name + "." + mr.Name;
            else if (cur.Operand is FieldReference fr) op += " " + fr.Name;
            else if (cur.Operand is string s) op += " \"" + (s.Length > 30 ? s.Substring(0, 30) + "..." : s) + "\"";
            else if (cur.Operand is int || cur.Operand is sbyte) op += " " + cur.Operand;
            parts.Add(Esc(op));
        }
        if (n < 0) parts.Reverse();
        return string.Join(" | ", parts);
    }

    static int Dump(string dll, string outPath)
    {
        var m = Open(dll, false);
        int count = 0;
        using (var w = new StreamWriter(outPath, false, new UTF8Encoding(false)))
        {
            w.WriteLine("metodo\toffset\tconsumidor\tantes\tdepois\ttexto");
            foreach (var md in Methods(m))
                foreach (var ins in md.Body.Instructions)
                    if (ins.OpCode.Code == Code.Ldstr)
                    {
                        w.WriteLine(md.DeclaringType.FullName + "::" + md.Name + "\t" + ins.Offset + "\t" + Consumer(ins) + "\t" +
                            Ctx(ins, -2) + "\t" + Ctx(ins, 4) + "\t" + Esc((string)ins.Operand));
                        count++;
                    }
        }
        Console.WriteLine("ldstr: " + count);
        return 0;
    }

    // il <dll> <Tipo::Metodo>: IL de todos os métodos cujo "Tipo::Metodo" contém o trecho.
    static int Il(string dll, string pattern)
    {
        var m = Open(dll, false);
        foreach (var md in Methods(m))
        {
            string key = md.DeclaringType.FullName + "::" + md.Name;
            if (key.IndexOf(pattern, StringComparison.Ordinal) < 0) continue;
            Console.WriteLine("== " + md.FullName);
            foreach (var ins in md.Body.Instructions)
                Console.WriteLine("  " + ins.ToString().Replace("\n", "\\n"));
        }
        return 0;
    }

    // callers <dll> <Tipo::Metodo>: quem chama o método, com a contagem.
    static int Callers(string dll, string pattern)
    {
        var m = Open(dll, false);
        var cont = new Dictionary<string, int>();
        foreach (var md in Methods(m))
            foreach (var ins in md.Body.Instructions)
                if (ins.Operand is MethodReference mr && (mr.DeclaringType.FullName + "::" + mr.Name).IndexOf(pattern, StringComparison.Ordinal) >= 0)
                {
                    string k = md.DeclaringType.FullName + "::" + md.Name;
                    cont[k] = cont.TryGetValue(k, out int c) ? c + 1 : 1;
                }
        foreach (var kv in cont.OrderByDescending(x => x.Value)) Console.WriteLine(kv.Value + "\t" + kv.Key);
        Console.WriteLine("total: " + cont.Values.Sum());
        return 0;
    }

    // field <dll> <nome>: métodos que leem ou gravam o campo.
    static int FieldUsers(string dll, string name)
    {
        var m = Open(dll, false);
        foreach (var md in Methods(m))
            foreach (var ins in md.Body.Instructions)
                if (ins.Operand is FieldReference fr && fr.Name == name)
                    Console.WriteLine(ins.OpCode.Name + "\t" + md.DeclaringType.FullName + "::" + md.Name + "\t" + ins.Offset);
        return 0;
    }

    static int Apply(string dll, string mapPath, string outDll)
    {
        var global = new Dictionary<string, string>(StringComparer.Ordinal);
        var local = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        // Linha "Tipo::Metodo<TAB>offset<TAB>original<TAB>tradução": troca só aquela instrução, conferindo o original.
        var exato = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(mapPath, Encoding.UTF8))
        {
            if (raw.Length == 0 || raw.StartsWith("#")) continue;
            var p = raw.Split('\t');
            if (p.Length >= 4 && p[0].Contains("::") && int.TryParse(p[1], out _))
                exato[p[0] + "@" + p[1]] = new[] { Unesc(p[2]), Unesc(p[3]) };
            else if (p[0].StartsWith("@") && p.Length >= 3)
            {
                if (!local.TryGetValue(p[0].Substring(1), out var d)) local[p[0].Substring(1)] = d = new Dictionary<string, string>(StringComparer.Ordinal);
                d[Unesc(p[1])] = Unesc(p[2]);
            }
            else if (p.Length >= 2) global[Unesc(p[0])] = Unesc(p[1]);
        }

        var m = Open(dll, true);
        int trocas = 0, exatoUsados = 0;
        var divergentes = new List<string>();
        var usados = new HashSet<string>(StringComparer.Ordinal);
        foreach (var md in Methods(m))
        {
            string key = md.DeclaringType.FullName + "::" + md.Name;
            local.TryGetValue(key, out var loc);
            foreach (var ins in md.Body.Instructions)
            {
                if (ins.OpCode.Code != Code.Ldstr) continue;
                var s = (string)ins.Operand;
                string t;
                if (exato.TryGetValue(key + "@" + ins.Offset, out var par))
                {
                    if (par[0] == s) { if (par[1] != s) { ins.Operand = par[1]; trocas++; } exatoUsados++; }
                    else divergentes.Add(key + "@" + ins.Offset);
                }
                else if ((loc != null && loc.TryGetValue(s, out t)) || global.TryGetValue(s, out t))
                {
                    if (t != s) { ins.Operand = t; trocas++; }
                    usados.Add(s);
                }
            }
        }
        if (avisos != null) RedirecionarAvisos(m, avisos);
        m.Write(outDll);
        int naoUsados = global.Keys.Count(k => !usados.Contains(k));
        Console.WriteLine("trocas: " + trocas + " | exatas aplicadas: " + exatoUsados + "/" + exato.Count +
            " | divergentes: " + divergentes.Count + " | entradas globais sem uso: " + naoUsados);
        foreach (var d in divergentes.Take(20)) Console.WriteLine("  divergente: " + d);
        return divergentes.Count > 0 ? 3 : 0;
    }

    // Troca o Screen.PrintSubtitle de NemesisMod.ShowNotification por NemesisAvisos.Aviso.Mostrar(string).
    static void RedirecionarAvisos(ModuleDefinition m, string helperDll)
    {
        var helper = ModuleDefinition.ReadModule(helperDll);
        var alvo = helper.GetType("NemesisAvisos.Aviso").Methods.First(x => x.Name == "Mostrar");
        var imp = m.ImportReference(alvo);
        int n = 0;
        foreach (var md in Methods(m))
        {
            if (md.DeclaringType.FullName != "NemesisSystem.NemesisMod" || md.Name != "ShowNotification") continue;
            foreach (var ins in md.Body.Instructions)
                if (ins.Operand is MethodReference mr && mr.DeclaringType.FullName == "RDR2.UI.Screen" && mr.Name == "PrintSubtitle")
                { ins.Operand = imp; n++; }
        }
        Console.WriteLine("avisos redirecionados: " + n);
        if (n != 1) throw new Exception("esperava 1 PrintSubtitle em ShowNotification, achei " + n);
    }
}
