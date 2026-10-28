// AutoSaque v2.0: Saque automático de corpos humanos à distância (sem animação) ou com animação vanilla.
// Substitui o AutoLooting.asi (que causava crash com o PDO Reloaded por hook interno).
//
// Melhorias da v2.0:
// 1. Rastreamento multi-fonte (GET_PED_NEARBY_PEDS + mira livre/travada + World.GetAllPeds):
//    Como World.GetAllPeds() retorna 0 no ScriptHookRDR2DotNet V2 (build 1491.50) e GET_PED_NEARBY_PEDS
//    lista apenas NPCs vivos, o mod memoriza os handles de todos os NPCs vivos ao redor e na mira do
//    jogador. No instante em que morrem (mesmo a 35+ metros), o corpo já está rastreado e é saqueado.
// 2. Saque à distância sem animação (Modo=rapido, padrão):
//    Funciona a pé, a cavalo (PermitirMontado=1) e durante tiroteios (PermitirEmCombate=1).
//    Entrega dinheiro (_MONEY_INCREMENT_CASH_BALANCE) e munição (_ADD_AMMO_TO_PED) com ADD_REASON_LOOTED,
//    acionando o feed nativo de saque no canto direito da tela e marcando o corpo como saqueado
//    (_SET_ENTITY_FULLY_LOOTED) para atualizar o ícone X no minimapa.
// 3. Compatibilidade com PDO Reloaded:
//    Ignora NPCs agonizando (ainda vivos para a engine) e saqueia automaticamente assim que morrem.
// 4. Debounce de teclas (400 ms) para evitar alternância repetida ao segurar F7/F10.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace AutoSaque
{
    public unsafe class AutoSaque : Script
    {
        readonly string iniPath, logPath;
        bool ligado, rapido;
        readonly float distancia, raioHostil, raioRapido;
        readonly bool soMeus, aceitarSemAutor, avisar, permitirMontado, permitirEmCombate, modoTeste, logDetalhado;
        readonly int timeoutMs, intervaloMs, dinheiroMin, dinheiroMax, municaoMin, municaoMax;
        readonly Keys teclaLigar, teclaModo, teclaTeste;
        readonly string modeloTeste;

        // Corpos já processados e NPCs rastreados enquanto vivos ou mirados.
        readonly HashSet<int> tratados = new HashSet<int>();
        readonly HashSet<int> rastreados = new HashSet<int>();
        readonly HashSet<int> vistos = new HashSet<int>();
        readonly Dictionary<int, int> ultimoAtacante = new Dictionary<int, int>();

        int alvo, caixaAntes;
        DateTime alvoAte, proximaVarredura, ultimoAtalho;
        bool pedirTeste;

        const uint AddReasonLooted = 0xCA806A55;   // ADD_REASON_LOOTED
        const uint WeaponUnarmed = 0xA2719263;     // WEAPON_UNARMED
        readonly Random rng = new Random();

        public AutoSaque()
        {
            iniPath = Path.Combine(BaseDirectory, "AutoSaque.ini");
            logPath = Path.Combine(BaseDirectory, "AutoSaque.log");

            ligado = IniBool("Geral", "Ligado", true);
            rapido = !Ini("Geral", "Modo", "rapido").StartsWith("anima", StringComparison.OrdinalIgnoreCase);
            distancia = IniFloat("Geral", "Distancia", 2.5f);
            raioHostil = IniFloat("Geral", "RaioHostil", 35f);
            soMeus = IniBool("Geral", "SoQuemEuMatei", false);
            aceitarSemAutor = IniBool("Geral", "AceitarSemAutor", true);
            avisar = IniBool("Geral", "Avisar", false);
            timeoutMs = (int)IniFloat("Geral", "TimeoutMs", 9000);
            intervaloMs = (int)IniFloat("Geral", "IntervaloMs", 250);
            teclaLigar = Enum.TryParse(Ini("Geral", "TeclaLigar", "F7"), true, out Keys k1) ? k1 : Keys.F7;
            teclaModo = Enum.TryParse(Ini("Geral", "TeclaModo", "F10"), true, out Keys km) ? km : Keys.F10;

            raioRapido = IniFloat("Rapido", "Raio", 35f);
            permitirMontado = IniBool("Rapido", "PermitirMontado", true);
            permitirEmCombate = IniBool("Rapido", "PermitirEmCombate", true);
            Faixa(Ini("Rapido", "DinheiroSeVazio", "45-250"), out dinheiroMin, out dinheiroMax);
            Faixa(Ini("Rapido", "Municao", "4-10"), out municaoMin, out municaoMax);

            modoTeste = IniBool("Teste", "ModoTeste", false);
            teclaTeste = Enum.TryParse(Ini("Teste", "TeclaTeste", "F12"), true, out Keys k2) ? k2 : Keys.F12;
            modeloTeste = Ini("Teste", "Modelo", "a_m_m_valtownfolk_01");
            logDetalhado = IniBool("Teste", "LogDetalhado", false) || modoTeste;

            if (File.Exists(logPath) && new FileInfo(logPath).Length > 512 * 1024) File.Delete(logPath);
            Log("iniciou v2.0: ligado=" + ligado + " modo=" + (rapido ? "rapido raio=" + raioRapido : "animacao distancia=" + distancia) +
                " dinheiroSeVazio=" + dinheiroMin + "-" + dinheiroMax + " municao=" + municaoMin + "-" + municaoMax +
                " soQuemEuMatei=" + soMeus + " montado=" + permitirMontado + " combate=" + permitirEmCombate);

            Interval = 0;
            KeyDown += OnKeyDown;
            Tick += OnTick;
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if ((DateTime.Now - ultimoAtalho).TotalMilliseconds < 400) return;

            if (e.KeyCode == teclaLigar)
            {
                ultimoAtalho = DateTime.Now;
                ligado = !ligado;
                Log("tecla: ligado=" + ligado);
                RDR2.UI.Screen.DisplaySubtitle(ligado ? "AutoSaque: LIGADO (" + (rapido ? "Distância " + raioRapido + "m" : "Animação") + ")" : "AutoSaque: DESLIGADO");
            }
            else if (e.KeyCode == teclaModo)
            {
                ultimoAtalho = DateTime.Now;
                rapido = !rapido;
                alvo = 0;
                Log("tecla: modo=" + (rapido ? "rapido" : "animacao"));
                RDR2.UI.Screen.DisplaySubtitle("AutoSaque: modo " + (rapido ? "RÁPIDO (sem animação, " + raioRapido + "m)" : "ANIMAÇÃO (" + distancia + "m)"));
            }
            else if (modoTeste && e.KeyCode == teclaTeste)
            {
                ultimoAtalho = DateTime.Now;
                pedirTeste = true;
            }
        }

        void OnTick(object sender, EventArgs e)
        {
            if (pedirTeste) { pedirTeste = false; Try("teste", CriarCorpoDeTeste); }
            if (!ligado) { alvo = 0; return; }

            // Captura contínua de alvo na mira do jogador (garante rastreio de tiros de sniper/rifle a longa distância).
            Try("mira", RastrearMira);

            if (!rapido && alvo != 0) { Try("acompanhar", Acompanhar); return; }

            if (DateTime.Now < proximaVarredura) return;
            proximaVarredura = DateTime.Now.AddMilliseconds(intervaloMs);
            Try("varrer", Varrer);
        }

        void RastrearMira()
        {
            int pid = PLAYER.PLAYER_ID();
            int ent = 0;
            if (PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(pid, &ent) && ent != 0 && ENTITY.DOES_ENTITY_EXIST(ent) && ENTITY.IS_ENTITY_A_PED(ent))
            {
                rastreados.Add(ent);
                ultimoAtacante[ent] = PLAYER.PLAYER_PED_ID();
            }
            int tgt = 0;
            if (PLAYER.GET_PLAYER_TARGET_ENTITY(pid, &tgt) && tgt != 0 && ENTITY.DOES_ENTITY_EXIST(tgt) && ENTITY.IS_ENTITY_A_PED(tgt))
            {
                rastreados.Add(tgt);
                ultimoAtacante[tgt] = PLAYER.PLAYER_PED_ID();
            }
        }

        void Acompanhar()
        {
            int ped = alvo;
            bool sumiu = !ENTITY.DOES_ENTITY_EXIST(ped);
            bool saqueado = !sumiu && ENTITY._IS_ENTITY_FULLY_LOOTED(ped);
            bool estourou = DateTime.Now > alvoAte;
            if (!sumiu && !saqueado && !estourou) return;

            tratados.Add(ped);
            alvo = 0;
            Log("corpo " + ped + ": " + (saqueado ? "saqueado" : sumiu ? "sumiu" : "desisti (tempo)") +
                ", caixa +" + (MONEY._MONEY_GET_CASH_BALANCE() - caixaAntes) + " centavos");
            if (saqueado && avisar) RDR2.UI.Screen.DisplaySubtitle("Corpo saqueado");
        }

        void Varrer()
        {
            int me = PLAYER.PLAYER_PED_ID();
            if (me == 0 || !ENTITY.DOES_ENTITY_EXIST(me) || ENTITY.IS_ENTITY_DEAD(me)) { Motivo("jogador morto ou carregando"); return; }
            if (!PLAYER.IS_PLAYER_CONTROL_ON(PLAYER.PLAYER_ID())) { Motivo("sem controle"); return; }
            if (StoryBusy()) { Motivo("missao ou cutscene"); return; }
            if (PED.IS_PED_SWIMMING(me)) { Motivo("nadando"); return; }

            // No modo com animação, exige estar a pé e fora de combate. No modo rápido (sem animação), funciona a cavalo e em combate.
            if (!rapido || !permitirMontado)
            {
                if (!PED.IS_PED_ON_FOOT(me)) { Motivo("montado"); return; }
            }
            if (!rapido || !permitirEmCombate)
            {
                if (PED.IS_PED_IN_COMBAT(me, 0)) { Motivo("em combate"); return; }
            }

            Vector3 eu = ENTITY.GET_ENTITY_COORDS(me, true, true);
            var todos = PedsPertoERastreados(me);

            if (!rapido || !permitirEmCombate)
            {
                int inimigo = raioHostil > 0 ? InimigoVivo(me, eu, todos) : 0;
                if (inimigo != 0) { Motivo("inimigo vivo perto (" + inimigo + ")"); return; }
            }
            Motivo("procurando");

            int montaria = 0;
            try { montaria = PED._GET_LAST_MOUNT(me); } catch { }

            float alcance = rapido ? raioRapido : distancia;
            int melhor = 0;
            float melhorDist = float.MaxValue;
            var corpos = new List<int>();

            foreach (int ped in todos)
            {
                if (tratados.Contains(ped)) continue;
                if (!ENTITY.DOES_ENTITY_EXIST(ped)) continue;

                // Se ainda está vivo (ou agonizando no PDO), apenas verifica se está em combate conosco para registrar autoria.
                if (!ENTITY.IS_ENTITY_DEAD(ped))
                {
                    if (PED.IS_PED_IN_COMBAT(ped, me) || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped, me, true, true))
                        ultimoAtacante[ped] = me;
                    continue;
                }

                if (!PED.IS_PED_HUMAN(ped)) { tratados.Add(ped); continue; }
                if (ENTITY._IS_ENTITY_FULLY_LOOTED(ped)) { tratados.Add(ped); continue; }

                float d = Distancia(eu, ENTITY.GET_ENTITY_COORDS(ped, true, true));
                if (logDetalhado && d <= alcance && vistos.Add(ped))
                    Log("corpo " + ped + ": detectado a " + d.ToString("0.0") + " m, dinheiro=" + PED.GET_PED_MONEY(ped));
                if (d > alcance) continue;

                if (soMeus && !modoTeste)
                {
                    int autor = PED.GET_PED_SOURCE_OF_DEATH(ped);
                    bool danificadoPorMim = SafeBool(() => ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped, me, true, true));
                    bool rastreadoComoMeu = ultimoAtacante.TryGetValue(ped, out int atk) && atk == me;
                    bool meu = autor == me || (montaria != 0 && autor == montaria) || danificadoPorMim || rastreadoComoMeu || (autor == 0 && aceitarSemAutor);
                    if (logDetalhado) Log("corpo " + ped + ": autor=" + autor + " danificado=" + danificadoPorMim + " meu=" + meu);
                    if (!meu) { tratados.Add(ped); continue; }
                }

                corpos.Add(ped);
                if (d < melhorDist) { melhor = ped; melhorDist = d; }
            }

            if (melhor == 0) return;

            if (rapido)
            {
                foreach (int ped in corpos) SaqueRapido(me, ped);
                return;
            }

            caixaAntes = MONEY._MONEY_GET_CASH_BALANCE();
            TASK.TASK_LOOT_ENTITY(me, melhor);
            alvo = melhor;
            alvoAte = DateTime.Now.AddMilliseconds(timeoutMs);
            Log("corpo " + melhor + ": saqueando com animacao a " + melhorDist.ToString("0.0") + " m");
        }

        // Saque à distância sem animação: transfere dinheiro + munição via natives com ADD_REASON_LOOTED
        // (aciona o toast nativo da Rockstar à direita da tela) e marca o corpo como totalmente saqueado.
        void SaqueRapido(int me, int ped)
        {
            int dinheiro = Math.Max(0, PED.GET_PED_MONEY(ped));
            bool sorteado = false;
            if (dinheiro == 0 && dinheiroMax > 0)
            {
                dinheiro = rng.Next(dinheiroMin, dinheiroMax + 1);
                sorteado = true;
            }
            if (dinheiro > 0)
            {
                MONEY._MONEY_INCREMENT_CASH_BALANCE(dinheiro, AddReasonLooted);
                PED.SET_PED_MONEY(ped, 0);
            }

            int balas = 0;
            uint arma = 0;
            if (municaoMax > 0)
            {
                arma = WEAPON.GET_BEST_PED_WEAPON(ped, false, false);
                if (arma == 0 || arma == WeaponUnarmed)
                {
                    uint armaJogador = 0;
                    if (WEAPON.GET_CURRENT_PED_WEAPON(me, &armaJogador, true, 0, false) && armaJogador != 0 && armaJogador != WeaponUnarmed)
                        arma = armaJogador;
                }
                if (arma != 0 && arma != WeaponUnarmed)
                {
                    balas = rng.Next(municaoMin, municaoMax + 1);
                    WEAPON._ADD_AMMO_TO_PED(me, arma, balas, AddReasonLooted);
                }
            }

            ENTITY._SET_ENTITY_FULLY_LOOTED(ped, true);
            tratados.Add(ped);
            ultimoAtacante.Remove(ped);

            Log("corpo " + ped + ": saque rapido concluido, +" + dinheiro + " centavos" + (sorteado ? " (tabela)" : "") +
                ", +" + balas + " balas (arma 0x" + arma.ToString("X8") + ")");

            if (avisar)
                RDR2.UI.Screen.DisplaySubtitle("Saque rápido: $" + (dinheiro / 100.0).ToString("0.00") + (balas > 0 ? " | +" + balas + " munições" : ""));
        }

        int InimigoVivo(int me, Vector3 eu, List<int> todos)
        {
            foreach (int ped in todos)
            {
                if (!ENTITY.DOES_ENTITY_EXIST(ped) || ENTITY.IS_ENTITY_DEAD(ped)) continue;
                if (Distancia(eu, ENTITY.GET_ENTITY_COORDS(ped, true, true)) > raioHostil) continue;
                if (PED.IS_PED_IN_COMBAT(ped, me)) return ped;
            }
            return 0;
        }

        // Combina GET_PED_NEARBY_PEDS (vivos que entram no cache rastreados), mira do jogador e World.GetAllPeds().
        List<int> PedsPertoERastreados(int me)
        {
            // 1. Alimenta rastreados com GET_PED_NEARBY_PEDS (funciona sempre na build 1491.50).
            ulong* arr = stackalloc ulong[66];
            for (int i = 0; i < 66; i++) arr[i] = 0;
            arr[0] = 64;
            try
            {
                int n = PED.GET_PED_NEARBY_PEDS(me, arr, -1, 0);
                int count = Math.Min(Math.Max(n, 0), 64);
                for (int i = 1; i <= count; i++)
                {
                    int h = (int)(arr[i] & 0xFFFFFFFFUL);
                    if (h != 0 && h != me && ENTITY.DOES_ENTITY_EXIST(h))
                        rastreados.Add(h);
                }
            }
            catch { }

            // 2. Soma World.GetAllPeds() caso retorne algo.
            try
            {
                foreach (var p in World.GetAllPeds())
                    if (p != null && p.Handle != 0 && p.Handle != me)
                        rastreados.Add(p.Handle);
            }
            catch { }

            // 3. Limpa entidades que já deixaram de existir no mundo e monta a lista ativa.
            var ativos = new List<int>(rastreados.Count);
            var remover = new List<int>();
            foreach (int h in rastreados)
            {
                if (h == 0 || h == me || !ENTITY.DOES_ENTITY_EXIST(h))
                    remover.Add(h);
                else
                    ativos.Add(h);
            }
            foreach (int r in remover)
            {
                rastreados.Remove(r);
                tratados.Remove(r);
                ultimoAtacante.Remove(r);
            }
            return ativos;
        }

        string ultimoMotivo = "";
        void Motivo(string m)
        {
            if (m == ultimoMotivo) return;
            ultimoMotivo = m;
            if (logDetalhado) Log("estado: " + m);
        }

        void CriarCorpoDeTeste()
        {
            int me = PLAYER.PLAYER_PED_ID();
            Vector3 at = ENTITY.GET_ENTITY_COORDS(me, true, true);
            Vector3 fwd = ENTITY.GET_ENTITY_FORWARD_VECTOR(me);
            float x = at.X + fwd.X * 6f, y = at.Y + fwd.Y * 6f, z = at.Z, ground;
            if (MISC.GET_GROUND_Z_FOR_3D_COORD(x, y, z + 50f, &ground, false) && Math.Abs(ground - z) < 40f) z = ground;
            uint model = MISC.GET_HASH_KEY(modeloTeste);
            STREAMING.REQUEST_MODEL(model, false);
            var ate = DateTime.Now.AddSeconds(3);
            while (!STREAMING.HAS_MODEL_LOADED(model) && DateTime.Now < ate) Wait(50);
            if (!STREAMING.HAS_MODEL_LOADED(model)) { Log("teste: modelo nao carregou " + modeloTeste); return; }
            int ped = PED.CREATE_PED(model, x, y, z, 0f, false, false, false, false);
            STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED(model);
            if (ped == 0) { Log("teste: CREATE_PED falhou"); return; }
            PED._SET_RANDOM_OUTFIT_VARIATION(ped, true);
            rastreados.Add(ped);
            ultimoAtacante[ped] = me;
            Wait(300);
            ENTITY.SET_ENTITY_HEALTH(ped, 0, 0);
            Log("teste: corpo " + ped + " criado a 6 m");
        }

        // ---------- utilitários ----------

        static float Distancia(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static void Faixa(string s, out int min, out int max)
        {
            var p = s.Split('-');
            int.TryParse(p[0].Trim(), out min);
            if (p.Length < 2 || !int.TryParse(p[1].Trim(), out max)) max = min;
            if (min < 0) min = 0;
            if (max < min) max = min;
        }

        bool StoryBusy() => SafeBool(() => Game.IsCutsceneActive);

        static bool SafeBool(Func<bool> f) { try { return f(); } catch { return false; } }

        void Try(string onde, Action a)
        {
            try { a(); }
            catch (Exception ex) { alvo = 0; Log("erro em " + onde + ": " + ex.GetType().Name + ": " + ex.Message); }
        }

        string Ini(string section, string key, string def)
        {
            string v = IniRaw(iniPath, section, key);
            return v.Length > 0 ? v : def;
        }

        bool IniBool(string section, string key, bool def)
        {
            string v = Ini(section, key, def ? "1" : "0").ToLowerInvariant();
            return v == "1" || v == "true" || v == "sim";
        }

        float IniFloat(string section, string key, float def)
        {
            return float.TryParse(Ini(section, key, "").Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : def;
        }

        static string IniRaw(string path, string section, string key)
        {
            if (!File.Exists(path)) return "";
            bool inSec = false;
            foreach (var raw in File.ReadAllLines(path))
            {
                var l = raw.Trim();
                if (l.StartsWith("[")) { inSec = string.Equals(l, "[" + section + "]", StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSec || l.StartsWith(";")) continue;
                int eq = l.IndexOf('=');
                if (eq > 0 && string.Equals(l.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    var v = l.Substring(eq + 1);
                    int c = v.IndexOf(';');
                    return (c >= 0 ? v.Substring(0, c) : v).Trim();
                }
            }
            return "";
        }

        void Log(string line)
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine); }
            catch { }
        }
    }
}
