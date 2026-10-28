// GoreVisceral v1.0: Amplificador de gore arterial, poças de sangue dinâmicas, desmembramento craniano
// e impulso físico localizado por osso (Euphoria) para Red Dead Redemption 2 (1.0.1491.50).
//
// Projetado para 100% de compatibilidade com enRAGE (BulletImpact X-Ray) e PDO Reloaded:
// - Nunca chama CLEAR_PED_LAST_DAMAGE_BONE (detecta acertos por delta de vida GET_ENTITY_HEALTH),
//   preservando a leitura de ossos para o raio-X do BulletImpact e para o PDO Reloaded.
// - Usa GET_PED_NEARBY_PEDS + mira do jogador + World.GetAllPeds para rastrear alvos em qualquer distância.
//
// Fontes e referências técnicas:
// - ScriptHookRDR2DotNet V2 (Halen84): https://github.com/Halen84/ScriptHookRDR2DotNet-V2
// - RDR3 Native DB (alloc8or): https://github.com/alloc8or/rdr3-nativedb-data
// - RDR3 Discoveries (femga): https://github.com/femga/rdr3_discoveries
// - RDR3 Native Flags & Enums (Halen84): https://github.com/Halen84/RDR3-Native-Flags-And-Enums
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace GoreVisceral
{
    public unsafe class GoreVisceral : Script
    {
        readonly string iniPath, logPath;
        bool ligado;
        readonly float escalaImpacto, escalaLodImpacto, tamanhoPoca, forcaImpulso, distExplodirCabeca, pressaoArterial;
        readonly bool explodirCabeca, jatoArterial, pocasSangue, impulsoOsso, logDetalhado;
        readonly Keys teclaToggle;

        readonly HashSet<int> rastreados = new HashSet<int>();
        readonly Dictionary<int, int> ultimaVida = new Dictionary<int, int>();
        readonly HashSet<int> cabecaExplodida = new HashSet<int>();
        readonly Dictionary<int, DateTime> sangrandoAte = new Dictionary<int, DateTime>();

        DateTime proximaVarredura, proximoSangramento, ultimoAtalho;

        // BoneIDs mapeados em _mod-lab\api-enums.tsv
        const int BoneHead = 0x5226;
        const int BoneJaw = 0xE4BF;
        const int BoneNeck0 = 0x37CB;
        const int BoneNeck1 = 0x37CC;
        const int BoneNeck2 = 0x37CD;
        const int BoneNeck3 = 0x37CE;
        const int BoneNeck4 = 0x37CF;
        const int BoneNeck5 = 0x37D0;
        const int BoneSpine2 = 0x384C;
        const int BoneSpine3 = 0x384D;
        const int BoneSpine4 = 0x384E;
        const int BonePelvis = 0xDB88;

        public GoreVisceral()
        {
            iniPath = Path.Combine(BaseDirectory, "GoreVisceral.ini");
            logPath = Path.Combine(BaseDirectory, "GoreVisceral.log");

            ligado = IniBool("Geral", "Ligado", true);
            teclaToggle = Enum.TryParse(Ini("Geral", "TeclaLigar", "None"), true, out Keys k) ? k : Keys.None;
            logDetalhado = IniBool("Geral", "LogDetalhado", false);

            escalaImpacto = IniFloat("Particulas", "EscalaImpacto", 1.85f);
            escalaLodImpacto = IniFloat("Particulas", "EscalaLodImpacto", 2.0f);

            jatoArterial = IniBool("Sangue", "JatoArterial", true);
            pressaoArterial = IniFloat("Sangue", "PressaoArterial", 1.65f);
            pocasSangue = IniBool("Sangue", "PocasDinamicas", true);
            tamanhoPoca = IniFloat("Sangue", "TamanhoPoca", 1.45f);

            explodirCabeca = IniBool("Desmembramento", "ExplodirCabeca", true);
            distExplodirCabeca = IniFloat("Desmembramento", "DistanciaMaxCabeca", 28.0f);

            impulsoOsso = IniBool("Fisica", "ImpulsoPorOsso", true);
            forcaImpulso = IniFloat("Fisica", "ForcaImpulso", 2.8f);

            if (File.Exists(logPath) && new FileInfo(logPath).Length > 512 * 1024) File.Delete(logPath);
            Log("GoreVisceral v1.0 iniciado: ligado=" + ligado + " escalaImpacto=" + escalaImpacto +
                " cabeca=" + explodirCabeca + "(" + distExplodirCabeca + "m) arterial=" + jatoArterial + " impulso=" + impulsoOsso);

            Interval = 0;
            KeyDown += OnKeyDown;
            Tick += OnTick;
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (teclaToggle == Keys.None || e.KeyCode != teclaToggle) return;
            if ((DateTime.Now - ultimoAtalho).TotalMilliseconds < 400) return;
            ultimoAtalho = DateTime.Now;
            ligado = !ligado;
            Log("tecla: ligado=" + ligado);
            RDR2.UI.Screen.DisplaySubtitle(ligado ? "GoreVisceral: LIGADO" : "GoreVisceral: DESLIGADO");
        }

        void OnTick(object sender, EventArgs e)
        {
            if (!ligado) return;
            int me = PLAYER.PLAYER_PED_ID();
            if (me == 0 || !ENTITY.DOES_ENTITY_EXIST(me) || ENTITY.IS_ENTITY_DEAD(me)) return;

            try
            {
                GRAPHICS.SET_PARTICLE_FX_BULLET_IMPACT_SCALE(escalaImpacto);
                GRAPHICS.SET_PARTICLE_FX_BULLET_IMPACT_LODRANGE_SCALE(escalaLodImpacto);
            }
            catch { }

            RastrearMira(me);

            if (DateTime.Now >= proximaVarredura)
            {
                proximaVarredura = DateTime.Now.AddMilliseconds(180);
                AtualizarListaPeds(me);
            }

            ProcessarImpactos(me);

            if (DateTime.Now >= proximoSangramento)
            {
                proximoSangramento = DateTime.Now.AddMilliseconds(450);
                ProcessarSangramentoContinuo();
            }
        }

        void RastrearMira(int me)
        {
            int pid = PLAYER.PLAYER_ID();
            int ent = 0;
            if (PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(pid, &ent) && ent != 0 && ent != me && ENTITY.DOES_ENTITY_EXIST(ent) && ENTITY.IS_ENTITY_A_PED(ent))
            {
                if (rastreados.Add(ent)) ultimaVida[ent] = ENTITY.GET_ENTITY_HEALTH(ent);
            }
            int tgt = 0;
            if (PLAYER.GET_PLAYER_TARGET_ENTITY(pid, &tgt) && tgt != 0 && tgt != me && ENTITY.DOES_ENTITY_EXIST(tgt) && ENTITY.IS_ENTITY_A_PED(tgt))
            {
                if (rastreados.Add(tgt)) ultimaVida[tgt] = ENTITY.GET_ENTITY_HEALTH(tgt);
            }
        }

        void AtualizarListaPeds(int me)
        {
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
                    {
                        if (rastreados.Add(h)) ultimaVida[h] = ENTITY.GET_ENTITY_HEALTH(h);
                    }
                }
            }
            catch { }

            try
            {
                foreach (var p in World.GetAllPeds())
                {
                    if (p != null && p.Handle != 0 && p.Handle != me && ENTITY.DOES_ENTITY_EXIST(p.Handle))
                    {
                        if (rastreados.Add(p.Handle)) ultimaVida[p.Handle] = ENTITY.GET_ENTITY_HEALTH(p.Handle);
                    }
                }
            }
            catch { }
        }

        void ProcessarImpactos(int me)
        {
            var remover = new List<int>();
            Vector3 posJogador = ENTITY.GET_ENTITY_COORDS(me, true, true);

            foreach (int ped in rastreados)
            {
                if (ped == 0 || ped == me || !ENTITY.DOES_ENTITY_EXIST(ped))
                {
                    remover.Add(ped);
                    continue;
                }

                int vidaAtual = ENTITY.GET_ENTITY_HEALTH(ped);
                if (!ultimaVida.TryGetValue(ped, out int vidaAntiga))
                {
                    ultimaVida[ped] = vidaAtual;
                    continue;
                }

                if (vidaAtual >= vidaAntiga)
                {
                    ultimaVida[ped] = vidaAtual;
                    continue;
                }

                int dano = vidaAntiga - vidaAtual;
                ultimaVida[ped] = vidaAtual;

                if (!PED.IS_PED_HUMAN(ped)) continue;

                int osso = 0;
                bool temOsso = false;
                try { temOsso = PED.GET_PED_LAST_DAMAGE_BONE(ped, &osso); } catch { }

                Vector3 posPed = ENTITY.GET_ENTITY_COORDS(ped, true, true);
                float dist = Distancia(posJogador, posPed);

                bool tiroCabeca = temOsso && (osso == BoneHead || osso == BoneJaw);
                bool tiroPescoco = temOsso && (osso >= BoneNeck0 && osso <= BoneNeck5);
                bool tiroTorax = temOsso && (osso == BoneSpine2 || osso == BoneSpine3 || osso == BoneSpine4);

                // 1. Respingo imediato de sangue no solo e decal corporal
                try
                {
                    GRAPHICS._ADD_BLOOD_TRAIL_SPLAT(posPed);
                    GRAPHICS._ADD_BLOOD_TRAIL_POINT(posPed);
                    PED.APPLY_PED_DAMAGE_PACK(ped, tiroCabeca || tiroPescoco ? "Shotgun_Close" : "HOSPITAL_3", dano, 1.0f);
                }
                catch { }

                // 2. Jato arterial em acertos de pescoço, cabeça ou tórax alto
                if (jatoArterial && (tiroPescoco || tiroCabeca || tiroTorax))
                {
                    try
                    {
                        int ossoAlvo = temOsso ? osso : BoneNeck1;
                        PED._SET_PED_ACTIVATE_WOUND_EFFECT(ped, 1, ossoAlvo, 0.0f, pressaoArterial, 0.0f, 1.0f, 0.85f, 1.0f, 1.0f);
                        PED._UPDATE_PED_WOUND_EFFECT(ped, pressaoArterial);
                        sangrandoAte[ped] = DateTime.Now.AddSeconds(tiroPescoco ? 9 : 5);
                    }
                    catch { }
                }
                else if (pocasSangue && (vidaAtual <= 0 || ENTITY.IS_ENTITY_DEAD(ped)))
                {
                    sangrandoAte[ped] = DateTime.Now.AddSeconds(4);
                }

                // 3. Desmembramento craniano em tiros letais na cabeça dentro do alcance
                if (explodirCabeca && tiroCabeca && (vidaAtual <= 0 || ENTITY.IS_ENTITY_DEAD(ped)) && dist <= distExplodirCabeca && cabecaExplodida.Add(ped))
                {
                    try
                    {
                        uint arma = 0;
                        WEAPON.GET_CURRENT_PED_WEAPON(me, &arma, true, 0, false);
                        if (arma == 0) arma = PED.GET_PED_CAUSE_OF_DEATH(ped);
                        PED.EXPLODE_PED_HEAD(ped, arma);
                        if (logDetalhado) Log("ped " + ped + ": EXPLODE_PED_HEAD a " + dist.ToString("0.0") + "m");
                    }
                    catch { }
                }

                // 4. Impulso cinético localizado no osso atingido (Euphoria)
                if (impulsoOsso && temOsso && dist < 80f)
                {
                    try
                    {
                        int boneIdx = PED.GET_PED_BONE_INDEX(ped, osso);
                        Vector3 dir = Normalizar(posPed - posJogador);
                        float mult = tiroCabeca ? forcaImpulso * 1.25f : forcaImpulso;
                        ENTITY.APPLY_FORCE_TO_ENTITY(ped, 1, dir.X * mult, dir.Y * mult, dir.Z * mult + 0.35f,
                            0f, 0f, 0f, boneIdx, false, true, true, false, true);
                    }
                    catch { }
                }

                if (logDetalhado)
                    Log("impacto ped=" + ped + " dano=" + dano + " osso=0x" + osso.ToString("X") + " dist=" + dist.ToString("0.0") + "m");
            }

            foreach (int r in remover)
            {
                rastreados.Remove(r);
                ultimaVida.Remove(r);
                cabecaExplodida.Remove(r);
                sangrandoAte.Remove(r);
            }
        }

        void ProcessarSangramentoContinuo()
        {
            if (sangrandoAte.Count == 0) return;
            var fim = new List<int>();
            foreach (var kv in sangrandoAte)
            {
                int ped = kv.Key;
                if (DateTime.Now > kv.Value || !ENTITY.DOES_ENTITY_EXIST(ped))
                {
                    fim.Add(ped);
                    continue;
                }
                try
                {
                    Vector3 pos = ENTITY.GET_ENTITY_COORDS(ped, true, true);
                    GRAPHICS._ADD_BLOOD_TRAIL_SPLAT(pos);
                    if (pocasSangue)
                        GRAPHICS._ADD_BLOOD_POOLS_FOR_PED_WITH_PARAMS(ped, 1.0f, tamanhoPoca, 1.0f);
                    if (jatoArterial)
                        PED._UPDATE_PED_WOUND_EFFECT(ped, pressaoArterial);
                }
                catch { }
            }
            foreach (int p in fim) sangrandoAte.Remove(p);
        }

        static float Distancia(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static Vector3 Normalizar(Vector3 v)
        {
            float len = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return len > 0.001f ? new Vector3(v.X / len, v.Y / len, v.Z / len) : new Vector3(0f, 1f, 0f);
        }

        string Ini(string section, string key, string def)
        {
            if (!File.Exists(iniPath)) return def;
            bool inSec = false;
            foreach (var raw in File.ReadAllLines(iniPath))
            {
                var l = raw.Trim();
                if (l.StartsWith("[")) { inSec = string.Equals(l, "[" + section + "]", StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSec || l.StartsWith(";")) continue;
                int eq = l.IndexOf('=');
                if (eq > 0 && string.Equals(l.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    var v = l.Substring(eq + 1);
                    int c = v.IndexOf(';');
                    var res = (c >= 0 ? v.Substring(0, c) : v).Trim();
                    return res.Length > 0 ? res : def;
                }
            }
            return def;
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

        void Log(string line)
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine); }
            catch { }
        }
    }
}
