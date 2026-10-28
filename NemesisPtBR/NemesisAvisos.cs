// Avisos do enRAGE no feed nativo do jogo. O NemesisMod.dll traduzido chama Aviso.Mostrar no lugar do
// Screen.PrintSubtitle que tinha em NemesisMod.ShowNotification (a legenda pequena embaixo).
// Configuração em scripts\NemesisAvisos.ini:
//   Estilo = toast (caixa com ícone no canto superior esquerdo) | ajuda (caixa de dica) | legenda (o original)
//   Duracao = milissegundos na tela; Titulo = linha de cima do toast; TambemLegenda = 1 repete embaixo.
// O texto vai pelo VAR_STRING (string do jogo): ponteiro próprio dentro da struct do feed travou o jogo.
using System;
using System.IO;
using RDR2;
using RDR2.Native;

namespace NemesisAvisos
{
    public static unsafe class Aviso
    {
        const ulong VarStringHash = 0xFA925AC00EB830B9UL;
        const string Txd = "generic_textures";

        static bool carregado, txdPedido;
        static string estilo = "toast", titulo = "enRAGE", icone = "tick", cor = "COLOR_WHITE";
        static int duracao = 6000, maxToast = 90;
        static bool tambemLegenda;
        static string logPath;

        public static void Mostrar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;
            try
            {
                Carregar();
                if (tambemLegenda || estilo == "legenda") RDR2.UI.Screen.PrintSubtitle(texto);
                if (estilo == "legenda") return;

                // Texto comprido não cabe no toast: vai para a caixa de ajuda, que quebra linha.
                bool toast = estilo == "toast" && texto.Length <= maxToast;
                int id = toast ? Toast(texto) : Ajuda(texto);
                if (id == 0) RDR2.UI.Screen.PrintSubtitle(texto);
            }
            catch (Exception ex)
            {
                Log("erro: " + ex.GetType().Name + ": " + ex.Message);
                try { RDR2.UI.Screen.PrintSubtitle(texto); } catch { }
            }
        }

        static int Toast(string texto)
        {
            if (!txdPedido) { TXD.REQUEST_STREAMED_TEXTURE_DICT(Txd, false); txdPedido = true; }
            ulong t = Str(titulo), s = Str(texto);
            if (t == 0 || s == 0) return 0;
            ulong* dur = stackalloc ulong[4];
            ulong* data = stackalloc ulong[16];
            for (int i = 0; i < 4; i++) dur[i] = 0;
            for (int i = 0; i < 16; i++) data[i] = 0;
            dur[0] = (ulong)duracao;
            data[1] = t;
            data[2] = s;
            data[4] = MISC.GET_HASH_KEY(Txd);
            data[5] = MISC.GET_HASH_KEY(icone);
            data[6] = MISC.GET_HASH_KEY(cor);
            return UIFEED._UI_FEED_POST_SAMPLE_TOAST(dur, data, true, true);
        }

        static int Ajuda(string texto)
        {
            ulong s = Str(texto);
            if (s == 0) return 0;
            ulong* dur = stackalloc ulong[4];
            ulong* data = stackalloc ulong[16];
            for (int i = 0; i < 4; i++) dur[i] = 0;
            for (int i = 0; i < 16; i++) data[i] = 0;
            dur[0] = (ulong)duracao;
            data[1] = s;
            return UIFEED._UI_FEED_POST_HELP_TEXT(dur, data, true);
        }

        static ulong Str(string text) => Function.Call<ulong>(VarStringHash, 10, "LITERAL_STRING", text);

        static void Carregar()
        {
            if (carregado) return;
            carregado = true;
            string dir = Path.GetDirectoryName(typeof(Aviso).Assembly.Location);
            logPath = Path.Combine(dir, "NemesisAvisos.log");
            string ini = Path.Combine(dir, "NemesisAvisos.ini");
            if (!File.Exists(ini)) return;
            foreach (var raw in File.ReadAllLines(ini))
            {
                var l = raw.Trim();
                if (l.Length == 0 || l.StartsWith(";") || l.StartsWith("[")) continue;
                int eq = l.IndexOf('=');
                if (eq <= 0) continue;
                string k = l.Substring(0, eq).Trim().ToLowerInvariant(), v = l.Substring(eq + 1).Trim();
                int n;
                switch (k)
                {
                    case "estilo": estilo = v.ToLowerInvariant(); break;
                    case "titulo": titulo = v; break;
                    case "icone": icone = v; break;
                    case "cor": cor = v; break;
                    case "duracao": if (int.TryParse(v, out n) && n > 0) duracao = n; break;
                    case "maxtoast": if (int.TryParse(v, out n) && n > 0) maxToast = n; break;
                    case "tambemlegenda": tambemLegenda = v == "1" || v.ToLowerInvariant() == "true"; break;
                }
            }
        }

        static void Log(string line)
        {
            try { File.AppendAllText(logPath ?? "NemesisAvisos.log", DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine); }
            catch { }
        }
    }
}
