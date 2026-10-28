// Sonda de natives: chama natives candidatos no modo história e grava em scripts\NativeProbe.log
// o que retornou, o que lançou exceção e onde o jogo parou (a linha "->" é gravada antes de cada chamada).
// Roda sozinha quando o jogador fica controlável (AutoRun) ou no F9, tira os próprios prints em
// scripts\NativeProbe-shots, guarda o andamento em scripts\NativeProbe.state e escreve o veredito de cada
// seção em scripts\NativeProbe-resumo.txt. Seção que estava rodando quando o jogo caiu vira TRAVOU e é pulada.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NativeProbe
{
    public unsafe class NativeProbe : Script
    {
        // Hashes tirados do IL do ScriptHookRDRNetAPI 2.2.0 (wrappers MISC/UIFEED).
        const ulong VarStringHash = 0xFA925AC00EB830B9UL;

        // Ordem por risco: o que já se mostrou seguro primeiro, PERSCHAR (travou na rodada 2) por último.
        static readonly string[] Order = { "UIFEED", "ANIMSCENE", "COMPANION", "GUARDZONE", "PERSCHAR" };

        readonly string logPath, statePath, summaryPath, shotsRoot, shotTool;
        readonly Keys runKey;
        readonly Dictionary<string, bool> enabled = new Dictionary<string, bool>();
        readonly bool autoRun, shotsOn;
        readonly int autoDelay, persCharMax;
        readonly string pedModel, persCharCreate;
        readonly string[] animScenes, scenePlaylists, sceneRoles;
        readonly string playDict;
        readonly string build;

        bool pending, running, autoDone, waitLogged;
        DateTime readySince = DateTime.MinValue;
        readonly List<int> spawned = new List<int>();
        string iniPath;
        DateTime runEnded = DateTime.MinValue;
        readonly List<int> volumes = new List<int>();
        readonly List<int> zoneVolumes = new List<int>();
        readonly List<string> zones = new List<string>();

        // Estado: seção -> (status, detalhe). Status: RODANDO, OK, FALHOU, TRAVOU.
        readonly Dictionary<string, string[]> state = new Dictionary<string, string[]>();
        string verdict;
        readonly List<string> notes = new List<string>();
        string shotDir;
        int shotNo;
        string lastShot;  // nome do último print, para servir de referência
        readonly Dictionary<string, float[]> thumbs = new Dictionary<string, float[]>();

        public NativeProbe()
        {
            logPath = Path.Combine(BaseDirectory, "NativeProbe.log");
            statePath = Path.Combine(BaseDirectory, "NativeProbe.state");
            summaryPath = Path.Combine(BaseDirectory, "NativeProbe-resumo.txt");
            shotsRoot = Path.Combine(BaseDirectory, "NativeProbe-shots");
            build = typeof(NativeProbe).Assembly.ManifestModule.ModuleVersionId.ToString("N").Substring(0, 8);

            // O ScriptSettings do SHRDN 2.2.0 não leu este ini (rodadas 6 e 7 ficaram com o modelo padrão):
            // tudo é lido direto do arquivo.
            iniPath = Path.Combine(BaseDirectory, "NativeProbe.ini");
            runKey = Enum.TryParse(Ini("Probe", "RunKey", "F9"), true, out Keys k) ? k : Keys.F9;
            autoRun = IniBool("Probe", "AutoRun", true);
            autoDelay = int.TryParse(Ini("Probe", "AutoRunDelay", "15"), out int d) ? d : 15;
            shotsOn = IniBool("Probe", "Shots", true);
            // GDI (CopyFromScreen) devolve preto com o RDR2 em Vulkan: o print sai pelo ddshot (Desktop Duplication).
            shotTool = Path.GetFullPath(Path.Combine(BaseDirectory, Ini("Probe", "ShotTool", @"..\_mod-lab\ddshot\ddshot.exe")));
            enabled["UIFEED"] = IniBool("Probe", "UiFeed", true);
            enabled["ANIMSCENE"] = IniBool("Probe", "AnimScene", true);
            enabled["COMPANION"] = IniBool("Probe", "Companion", true);
            enabled["GUARDZONE"] = IniBool("Probe", "GuardZone", true);
            enabled["PERSCHAR"] = IniBool("Probe", "PersChar", true);
            persCharMax = int.TryParse(Ini("PersChar", "CheckMax", "5"), out int m) ? m : 5;
            persCharCreate = Ini("PersChar", "CreateName", "");
            pedModel = Ini("GuardZone", "PedModel", "a_m_m_valdeputyresident_01");
            animScenes = Ini("AnimScene", "Candidates", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            scenePlaylists = Ini("AnimScene", "Playlists", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            playDict = Ini("AnimScene", "PlayDict", "");
            // RoleFile: um candidato por linha (lista grande tirada da memória com _mod-lab\memstr\memctx).
            var roleFile = Path.Combine(BaseDirectory, Ini("AnimScene", "RoleFile", ""));
            sceneRoles = Ini("AnimScene", "Roles", "player").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Concat(File.Exists(roleFile) ? File.ReadAllLines(roleFile) : new string[0])
                .Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            Log("=== NativeProbe build " + build + " carregado (AutoRun=" + autoRun + ", tecla=" + runKey +
                ", guarda=" + pedModel + ", animscenes=" + animScenes.Length + ", papeis=" + sceneRoles.Length + ")");
            // A repetição da tecla segurada chega depois do fim da rodada e disparava uma segunda; 2 s de folga.
            KeyDown += (s, e) => { if (e.KeyCode == runKey && !running && (DateTime.Now - runEnded).TotalSeconds > 2) pending = true; };
            Tick += OnTick;
            Aborted += (s, e) => Cleanup();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (!pending && autoRun && !autoDone && !running) CheckAutoRun();
            if (!pending || running) return;
            pending = false;
            running = true;
            try { RunAll(); }
            catch (Exception ex) { Log("FATAL " + ex); }
            finally { Cleanup(); running = false; runEnded = DateTime.Now; }
        }

        void CheckAutoRun()
        {
            var me = Game.Player.Character;
            // Na rodada 3 estas condições já valiam com a tela ainda preta de carregamento e o mundo vazio,
            // por isso o critério final é o print ter imagem.
            bool ready = !Game.IsLoading && Game.IsScreenFadedIn && !CutsceneOn() &&
                         !DLC.GET_IS_LOADING_SCREEN_ACTIVE() && !STREAMING.IS_LOAD_SCENE_ACTIVE() &&
                         me.Exists() && !ENTITY.IS_ENTITY_DEAD(me.Handle) &&
                         ENTITY.HAS_COLLISION_LOADED_AROUND_ENTITY(me.Handle) &&
                         PLAYER.IS_PLAYER_CONTROL_ON(PLAYER.PLAYER_ID());
            if (!ready) { readySince = DateTime.MinValue; return; }
            if (readySince == DateTime.MinValue)
            {
                readySince = DateTime.Now;
                if (!waitLogged) { Log("jogador controlavel; AutoRun em " + autoDelay + "s"); waitLogged = true; }
            }
            if ((DateTime.Now - readySince).TotalSeconds < autoDelay) return;
            if (shotsOn && File.Exists(shotTool))
            {
                Directory.CreateDirectory(shotsRoot);
                var lum = Capture(Path.Combine(shotsRoot, "pronto.jpg"));
                float mean = lum == null ? -1 : lum.Average();
                if (mean < 8)
                {
                    Log("AutoRun: tela ainda sem imagem (brilho=" + mean.ToString("0") + "), tentando de novo em 5s");
                    readySince = DateTime.Now.AddSeconds(5 - autoDelay);
                    return;
                }
                Log("AutoRun: tela com imagem (brilho=" + mean.ToString("0") + ")");
            }
            autoDone = true;
            LoadState();
            if (Order.Any(n => enabled[n] && !state.ContainsKey(n))) pending = true;
            else Log("AutoRun: nada pendente nesta build (F9 recomeca do zero)");
        }

        void RunAll()
        {
            LoadState();
            var todo = Order.Where(n => enabled[n] && !state.ContainsKey(n)).ToList();
            if (todo.Count == 0)
            {
                // F9 com tudo concluído: rodada nova do zero.
                state.Clear();
                todo = Order.Where(n => enabled[n]).ToList();
            }

            var me = Game.Player.Character;
            shotDir = Path.Combine(shotsRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            shotNo = 0;
            thumbs.Clear();
            Log("=== rodada " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " build=" + build +
                " secoes=" + string.Join(",", todo));
            Log("player=" + me.Handle + " pos=" + Fmt(me.Position) + " janela=" + WindowInfo());
            RDR2.UI.Screen.DisplaySubtitle("NativeProbe: rodando, nao feche o jogo");
            Shot("inicio", null);

            foreach (var name in todo) Section(name);

            Log("=== fim");
            WriteSummary();
            RDR2.UI.Screen.DisplaySubtitle("NativeProbe: terminou, veja scripts\\NativeProbe-resumo.txt");
        }

        void Section(string name)
        {
            Log("--- " + name);
            verdict = "OK";
            notes.Clear();
            SetState(name, "RODANDO", "");
            try
            {
                string run = name;
                if (StoryBusy()) { Fail("missao ou cutscene ativa; secao pulada"); run = ""; }
                switch (run)
                {
                    case "UIFEED": ProbeUiFeed(); break;
                    case "ANIMSCENE": ProbeAnimScene(); break;
                    case "COMPANION": ProbeCompanion(); break;
                    case "GUARDZONE": ProbeGuardZone(); break;
                    case "PERSCHAR": ProbePersChar(); break;
                }
            }
            catch (Exception ex)
            {
                Log("EXC na secao " + name + ": " + ex.Message);
                verdict = "FALHOU";
                notes.Add("excecao: " + ex.Message);
            }
            Cleanup();
            SetState(name, verdict, string.Join(" | ", notes));
            WriteSummary();
        }

        // ---------- UIFEED ----------

        void ProbeUiFeed()
        {
            const string txd = "generic_textures";
            TXD.REQUEST_STREAMED_TEXTURE_DICT(txd, false);
            WaitFor(() => TXD.HAS_STREAMED_TEXTURE_DICT_LOADED(txd), 2000);
            Log("txd " + txd + " loaded=" + TXD.HAS_STREAMED_TEXTURE_DICT_LOADED(txd));

            // Rodadas 6 e 7: com o jogador parado o jogo esconde o HUD (radar some, IS_RADAR_HIDDEN=True) e
            // os avisos ficam na fila até alguém mexer. DISPLAY_HUD/DISPLAY_RADAR não trazem de volta.
            notes.Add("hud " + WakeHud());

            // O texto precisa vir de VAR_STRING (string do jogo). Ponteiro UTF-8 nosso travou o jogo
            // no HELP_TEXT em 25/10/2026 (AppHang). Texto nulo dentro da struct pode derrubar o jogo.
            if (Str("teste") == 0) { Fail("VAR_STRING nao devolveu ponteiro, secao pulada"); return; }
            PostAndWatch("HELP_TEXT", (d, x) => UIFEED._UI_FEED_POST_HELP_TEXT(d, x, true),
                0, Str("[sonda] help text"));
            PostAndWatch("OBJECTIVE", (d, x) => UIFEED._UI_FEED_POST_OBJECTIVE(d, x, true),
                0, Str("[sonda] objetivo"));
            PostAndWatch("SAMPLE_TOAST", (d, x) => UIFEED._UI_FEED_POST_SAMPLE_TOAST(d, x, true, true),
                0, Str("[sonda] toast"), Str("subtitulo"), 0,
                MISC.GET_HASH_KEY(txd), MISC.GET_HASH_KEY("tick"), MISC.GET_HASH_KEY("COLOR_WHITE"), 0);
            PostAndWatch("LOCATION_SHARD", (d, x) => UIFEED._UI_FEED_POST_LOCATION_SHARD(d, x, true, true),
                0, Str("[sonda] local"), Str("Valentine"));
            TXD.SET_STREAMED_TEXTURE_DICT_AS_NO_LONGER_NEEDED(txd);
        }

        delegate int FeedPost(ulong* duration, ulong* data);

        void PostAndWatch(string what, FeedPost post, params ulong[] slots)
        {
            // Cada campo das structs do feed ocupa 8 bytes alinhados.
            ulong* dur = stackalloc ulong[4];
            ulong* data = stackalloc ulong[16];
            for (int i = 0; i < 4; i++) dur[i] = 0;
            for (int i = 0; i < 16; i++) data[i] = i < slots.Length ? slots[i] : 0;
            dur[0] = 4000;

            WakeHud();
            lastShot = null;
            Shot(what + "-antes", null);
            string before = lastShot;
            var id = Step("UIFEED", what, () => post(dur, data));
            Wait(700);
            lastShot = null;
            string diff = Shot(what + "-depois", before);
            string after1 = lastShot;
            Wait(500);
            lastShot = null;
            string diff2 = Shot(what + "-depois2", before);
            string after2 = lastShot;
            object st = null;
            if (id is int msg && msg != 0)
                st = Step("UIFEED", "_UI_FEED_GET_MESSAGE_STATE(" + msg + ")", () => UIFEED._UI_FEED_GET_MESSAGE_STATE(msg));
            else Fail(what + " nao devolveu id");
            // Câmera parada muda até ~5 por quadrante; o toast da rodada 5 deu 17,7 e a faixa de local 13,3.
            float change = Math.Max(MaxChange(before, after1), MaxChange(before, after2));
            string seen = change < 0 ? "sem print" : change >= 6 ? "SIM" : "NAO";
            notes.Add(what + " na tela: " + seen + " (max " + change.ToString("0.0") + ") id=" + (id ?? "EXC") +
                      " estado=" + (st ?? "-") + " tela " + diff + " / " + diff2);
            Wait(3000);
        }

        // Acorda o HUD escondido por inatividade simulando entrada por alguns quadros: primeiro o
        // "revelar HUD", depois um giro mínimo de câmera. Devolve o que funcionou, para o resumo.
        string WakeHud()
        {
            if (!SafeBool(() => HUD.IS_RADAR_HIDDEN())) return "visivel";
            foreach (var (name, input, value) in new[] { ("INPUT_REVEAL_HUD", 0xCF8A4ECAu, 1f), ("INPUT_LOOK_LR", 0xA987235Fu, 0.05f) })
            {
                for (int i = 0; i < 20; i++)
                {
                    Try(() => PAD.SET_CONTROL_VALUE_NEXT_FRAME(0, input, value));
                    Wait(0);
                }
                Wait(300);
                bool shown = !SafeBool(() => HUD.IS_RADAR_HIDDEN());
                Log("hud: " + name + " -> radar visivel=" + shown);
                if (shown) return "escondido, acordado por " + name;
            }
            return "escondido, nao acordou";
        }

        ulong Str(string text)
        {
            var r = Step("UIFEED", "VAR_STRING(10, LITERAL_STRING)",
                () => Function.Call<ulong>(VarStringHash, 10, "LITERAL_STRING", text));
            return r is ulong p ? p : 0;
        }

        // ---------- ANIMSCENE ----------

        void ProbeAnimScene()
        {
            // Os nomes vêm da memória do jogo (_mod-lab\memstr): dicionários script@... e playlists PBL_...
            // A playlist vai nula na criação, como os scripts do jogo fazem; depois cada playlist é testada na cena.
            if (animScenes.Length == 0) { notes.Add("sem candidatos no ini"); return; }
            int created = 0, loaded = 0;
            foreach (var entry in animScenes)
            {
                var parts = entry.Split('|');
                var dict = parts[0].Trim();
                string list = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : null;
                int scene = CreateScene(dict, list);
                if (scene <= 0) { notes.Add(dict + ": nao criou (id=" + scene + ")"); continue; }
                created++;
                bool ok = LoadScene(scene);
                if (ok) loaded++;
                var dur = Safe(() => ANIMSCENE._GET_ANIM_SCENE_DURATION(scene));
                var lists = scenePlaylists.Where(pl => SafeBool(() => ANIMSCENE._DOES_ANIM_SCENE_PLAY_LIST_EXIST(scene, pl))).ToList();
                var roles = RolesIn(scene);
                Log("carregou=" + ok + " duracao=" + dur + " playlists=" + string.Join(",", lists) + " papeis=" + string.Join(",", roles));
                notes.Add(dict + ": criou carregou=" + ok + " duracao=" + dur + (lists.Count > 0 ? " playlists=" + string.Join(",", lists) : "") +
                          (roles.Count > 0 ? " papeis=" + string.Join(",", roles) : ""));
                Call("ANIMSCENE", "_DELETE_ANIM_SCENE", () => ANIMSCENE._DELETE_ANIM_SCENE(scene));
            }
            notes.Insert(0, "criadas " + created + "/" + animScenes.Length + ", carregadas " + loaded);
            if (created == 0) { verdict = "FALHOU"; return; }
            // PlayDict aceita vários, separados por ';': toca no jogador o primeiro que funcionar.
            foreach (var d in playDict.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (PlayOnPlayer(d.Trim())) break;
        }

        // Papéis (ids de entidade) que a cena declara, entre os candidatos do ini; não precisa tocar a cena.
        List<string> RolesIn(int scene) =>
            sceneRoles.Where(r => SafeBool(() => ANIMSCENE._DOES_ENTITY_WITH_ID_EXIST_IN_ANIM_SCENE(scene, r))).ToList();

        int CreateScene(string dict, string list)
        {
            var r = Step("ANIMSCENE", "_CREATE_ANIM_SCENE(" + dict + ", " + (list ?? "null") + ")",
                () => ANIMSCENE._CREATE_ANIM_SCENE(dict, 0, list, false, true));
            return r is int s ? s : -2;
        }

        bool LoadScene(int scene)
        {
            Call("ANIMSCENE", "LOAD_ANIM_SCENE", () => ANIMSCENE.LOAD_ANIM_SCENE(scene));
            return WaitFor(() => ANIMSCENE.IS_ANIM_SCENE_LOADED(scene, true, false), 5000);
        }

        // Toca a cena no próprio jogador, um nome de papel por vez, até um deles colocar o jogador na cena.
        bool PlayOnPlayer(string dict)
        {
            var me = Game.Player.Character;
            int ph = me.Handle;
            // Só os papéis que a própria cena confirma são tocados; sem nenhum, a cena é pulada.
            int probe = CreateScene(dict, null);
            if (probe <= 0) { notes.Add("tocar " + dict + ": nao criou"); return false; }
            LoadScene(probe);
            var tryRoles = RolesIn(probe);
            Call("ANIMSCENE", "_DELETE_ANIM_SCENE", () => ANIMSCENE._DELETE_ANIM_SCENE(probe));
            notes.Add("tocar " + dict + ": papeis na cena=" + (tryRoles.Count > 0 ? string.Join(",", tryRoles) : "nenhum dos " + sceneRoles.Length + " candidatos"));
            if (tryRoles.Count == 0) return false;
            foreach (var role in tryRoles)
            {
                if (StoryBusy()) { notes.Add("tocar: missao ou cutscene ativa, parou"); return true; }
                int scene = CreateScene(dict, null);
                if (scene <= 0) { notes.Add("tocar: nao criou"); return false; }
                var pos = me.Position;
                float heading = ENTITY.GET_ENTITY_HEADING(ph);
                Call("ANIMSCENE", "SET_ANIM_SCENE_ORIGIN", () => ANIMSCENE.SET_ANIM_SCENE_ORIGIN(scene, pos.X, pos.Y, pos.Z - 1f, 0f, 0f, heading, 2));
                Call("ANIMSCENE", "SET_ANIM_SCENE_ENTITY(" + role + ")", () => ANIMSCENE.SET_ANIM_SCENE_ENTITY(scene, role, ph, 0));
                bool ok = LoadScene(scene);
                lastShot = null;
                Shot("animscene-" + role + "-antes", null);
                string before = lastShot;
                Call("ANIMSCENE", "START_ANIM_SCENE", () => ANIMSCENE.START_ANIM_SCENE(scene));
                Wait(1200);
                bool playing = SafeBool(() => ANIMSCENE.IS_ENTITY_PLAYING_ANIM_SCENE(ph, scene));
                bool running = SafeBool(() => ANIMSCENE.IS_ANIM_SCENE_RUNNING(scene, false));
                Log("papel " + role + ": carregou=" + ok + " rodando=" + running + " jogador na cena=" + playing);
                if (playing)
                {
                    string diff = Shot("animscene-" + role + "-tocando", before);
                    var until = DateTime.Now.AddSeconds(10);
                    while (DateTime.Now < until && SafeBool(() => ANIMSCENE.IS_ANIM_SCENE_RUNNING(scene, false))) Wait(250);
                    Shot("animscene-" + role + "-fim", before);
                    notes.Add("tocou no jogador com papel '" + role + "' (" + dict + ") tela " + diff);
                }
                Call("ANIMSCENE", "ABORT_ANIM_SCENE", () => ANIMSCENE.ABORT_ANIM_SCENE(scene, false));
                Call("ANIMSCENE", "_DELETE_ANIM_SCENE", () => ANIMSCENE._DELETE_ANIM_SCENE(scene));
                if (playing) { Try(() => TASK.CLEAR_PED_TASKS(ph, true, false)); return true; }
            }
            notes.Add("tocar: nenhum papel colocou o jogador na cena (" + string.Join(",", tryRoles) + ")");
            return false;
        }

        // ---------- COMPANION ----------

        void ProbeCompanion()
        {
            // Só os dois natives com nome; os 23 só-hash ficam de fora até termos assinatura confiável.
            int ped = SpawnPed(4f);
            if (ped == 0) { Fail("ped nao nasceu"); return; }
            Wait(1500);
            Shot("companion-antes", null);
            string before = lastShot;
            Call("COMPANION", "_ACTIVATE_COMPANION_ANALYSIS(ped)", () => COMPANION._ACTIVATE_COMPANION_ANALYSIS(ped));
            Wait(3000);
            notes.Add("ped: tela " + Shot("companion-ped", before));
            Call("COMPANION", "_DEACTIVATE_COMPANION_ANALYSIS(ped)", () => COMPANION._DEACTIVATE_COMPANION_ANALYSIS(ped));
            int me = Game.Player.Character.Handle;
            Call("COMPANION", "_ACTIVATE_COMPANION_ANALYSIS(player)", () => COMPANION._ACTIVATE_COMPANION_ANALYSIS(me));
            Wait(3000);
            notes.Add("player: tela " + Shot("companion-player", before));
            Call("COMPANION", "_DEACTIVATE_COMPANION_ANALYSIS(player)", () => COMPANION._DEACTIVATE_COMPANION_ANALYSIS(me));
            notes.Add("efeito em jogo nao observavel por estado; ver prints");
        }

        // ---------- GUARD ZONE ----------

        void ProbeGuardZone()
        {
            var me = Game.Player.Character;
            int guard = SpawnPed(34f);
            if (guard == 0) { Fail("guarda nao nasceu"); return; }
            var gpos = ENTITY.GET_ENTITY_COORDS(guard, true, true);
            var start = me.Position;
            var fwd = me.ForwardVector;
            int wanted0 = LAW.GET_WANTED_SCORE(Game.Player.Handle);

            // Variante A: zona ligada a uma entidade.
            const string zoneA = "PROBE_GZ_ENTITY";
            var a = Step("GUARDZONE", "_CREATE_GUARD_ZONE_FOR_ENTITY", () => LAW._CREATE_GUARD_ZONE_FOR_ENTITY(zoneA, guard, gpos));
            if (a is bool okA && okA) zones.Add(zoneA);
            notes.Add("zona por entidade=" + (a ?? "EXC"));

            // Variante B: zona nomeada com volumes de aviso, restrito e ameaça.
            const string zoneB = "PROBE_GZ_VOLUMES";
            Call("GUARDZONE", "_CREATE_GUARD_ZONE", () => LAW._CREATE_GUARD_ZONE(zoneB));
            zones.Add(zoneB);
            var seen = new HashSet<string>();
            PlaceZone(zoneB, gpos);
            Shot("guarda-inicio", null);

            // O jogador fica parado: andar com ele já disparou gatilho de missão (rodada 5, Beecher's Hope).
            // Quem se aproxima é o guarda, levado com a zona inteira até cada faixa (fora, aviso, restrito, ameaça).
            foreach (var stop in new[] { 30f, 21f, 12f, 4f })
            {
                if (StoryBusy()) { Fail("missao ou cutscene comecou; secao interrompida"); break; }
                float x = start.X + fwd.X * stop, y = start.Y + fwd.Y * stop, z = start.Z, ground;
                if (MISC.GET_GROUND_Z_FOR_3D_COORD(x, y, z + 50f, &ground, false) && Math.Abs(ground - z) < 40f) z = ground;
                gpos = new Vector3(x, y, z);
                var at = gpos;
                Call("ENTITY", "SET_ENTITY_COORDS(guarda) a " + stop + "m",
                    () => ENTITY.SET_ENTITY_COORDS(guard, at.X, at.Y, at.Z, false, false, false, false));
                Try(() => ENTITY.SET_ENTITY_HEADING(guard, ENTITY.GET_ENTITY_HEADING(me.Handle) + 180f));
                PlaceZone(zoneB, gpos);
                for (int s = 0; s < 6; s++) { Watch(guard, gpos, seen); Wait(1000); }
                Shot("guarda-" + stop + "m", null);
            }

            int wanted1 = LAW.GET_WANTED_SCORE(Game.Player.Handle);
            notes.Add("reacoes: " + (seen.Count == 0 ? "nenhuma" : string.Join(", ", seen)));
            notes.Add("procurado " + wanted0 + " -> " + wanted1);
            if (seen.Count == 0) verdict = "SEM REACAO";
        }

        // Recria os volumes de aviso (25 m), restrito (15 m) e ameaça (7 m) em volta do guarda.
        void PlaceZone(string zone, Vector3 at)
        {
            foreach (var v in zoneVolumes) Try(() => { if (VOLUME.DOES_VOLUME_EXIST(v)) VOLUME.DELETE_VOLUME(v); volumes.Remove(v); });
            zoneVolumes.Clear();
            Call("GUARDZONE", "_SET_GUARD_ZONE_POSITION", () => LAW._SET_GUARD_ZONE_POSITION(zone, at));
            int warn = Sphere(at, 25f), restricted = Sphere(at, 15f), threat = Sphere(at, 7f);
            zoneVolumes.AddRange(new[] { warn, restricted, threat });
            Call("GUARDZONE", "_SET_GUARD_ZONE_VOLUME_WARNING", () => LAW._SET_GUARD_ZONE_VOLUME_WARNING(zone, warn));
            Call("GUARDZONE", "_SET_GUARD_ZONE_VOLUME_RESTRICTED", () => LAW._SET_GUARD_ZONE_VOLUME_RESTRICTED(zone, restricted));
            Call("GUARDZONE", "_SET_GUARD_ZONE_VOLUME_THREAT", () => LAW._SET_GUARD_ZONE_VOLUME_THREAT(zone, threat));
        }

        // Missão ou cutscene em curso: a sonda não deve mexer no jogador nem seguir criando coisas.
        // Game.IsCutsceneActive do SHRDN 2.2.0 lança OverflowException neste exe; erro conta como sem cutscene.
        bool CutsceneOn() => SafeBool(() => Game.IsCutsceneActive);

        bool StoryBusy() => CutsceneOn() || SafeBool(() => MISC.GET_MISSION_FLAG());

        void Watch(int guard, Vector3 gpos, HashSet<string> seen)
        {
            var me = Game.Player.Character;
            float dist = me.Position.DistanceTo(gpos);
            bool inv = SafeBool(() => LAW._IS_GUARD_PED_INVESTIGATING(guard));
            bool combat = SafeBool(() => PED.IS_PED_IN_COMBAT(guard, me.Handle));
            bool armed = SafeBool(() => WEAPON.IS_PED_ARMED(guard, 6));
            bool flee = SafeBool(() => PED.IS_PED_FLEEING(guard));
            bool alive = SafeBool(() => !ENTITY.IS_ENTITY_DEAD(guard));
            int wanted = LAW.GET_WANTED_SCORE(Game.Player.Handle);
            Log("dist=" + dist.ToString("0.0") + " investigando=" + inv + " combate=" + combat + " armado=" + armed +
                " fugindo=" + flee + " vivo=" + alive + " procurado=" + wanted);
            string band = dist > 25 ? "fora" : dist > 15 ? "aviso" : dist > 7 ? "restrito" : "ameaca";
            if (inv) seen.Add("investigou(" + band + ")");
            if (combat) seen.Add("combate(" + band + ")");
            if (armed) seen.Add("armado(" + band + ")");
            if (flee) seen.Add("fugiu(" + band + ")");
            if (!alive) seen.Add("morreu(" + band + ")");
        }

        // ---------- PERSCHAR ----------

        void ProbePersChar()
        {
            // Só leitura. O valor de _GET_PERSCHAR_INDEX_FROM_PED_INDEX anda de 256 em 256 (é handle, não índice):
            // varrer 0..512 com _IS_PERSISTENT_CHARACTER_VALID travou o jogo na rodada 2. Só checa o que achou.
            var peds = World.GetAllPeds().Select(p => p.Handle).ToList();
            Log("peds no mundo (World.GetAllPeds): " + peds.Count);
            if (peds.Count == 0)
            {
                // Nas rodadas 3 e 4 o GetAllPeds devolveu 0 com gente em volta: segunda fonte pelo native.
                ulong* arr = stackalloc ulong[65];
                for (int i = 0; i < 65; i++) arr[i] = 0;
                arr[0] = 64;
                int me = Game.Player.Character.Handle;
                var n = Step("PED", "GET_PED_NEARBY_PEDS(player, 64)", () => PED.GET_PED_NEARBY_PEDS(me, arr, -1, 0));
                int count = n is int k ? Math.Min(k, 64) : 0;
                for (int i = 1; i <= count; i++) if (arr[i] != 0) peds.Add((int)arr[i]);
                Log("peds perto (GET_PED_NEARBY_PEDS): " + peds.Count);
            }
            var found = new List<int[]>();
            foreach (var ph in peds)
            {
                var p = new Ped(ph);
                int idx;
                try { idx = PERSCHAR._GET_PERSCHAR_INDEX_FROM_PED_INDEX(p.Handle); }
                catch (Exception ex) { Log("EXC _GET_PERSCHAR_INDEX_FROM_PED_INDEX: " + ex.Message); break; }
                if (idx <= 0) continue;
                found.Add(new[] { p.Handle, idx });
                Log("ped " + p.Handle + " model=0x" + ENTITY.GET_ENTITY_MODEL(p.Handle).ToString("X8") +
                    " perschar=" + idx + " dist=" + p.Position.DistanceTo(Game.Player.Character.Position).ToString("0"));
            }
            notes.Add("peds persistentes: " + found.Count + " de " + peds.Count);

            int match = 0, checkedN = 0;
            foreach (var f in found.Take(persCharMax))
            {
                int ped = f[0], h = f[1];
                checkedN++;
                var valid = Step("PERSCHAR", "_IS_PERSISTENT_CHARACTER_VALID(" + h + ")", () => PERSCHAR._IS_PERSISTENT_CHARACTER_VALID(h));
                var back = Step("PERSCHAR", "_GET_PERSCHAR_PED_INDEX(" + h + ")", () => PERSCHAR._GET_PERSCHAR_PED_INDEX(h));
                Step("PERSCHAR", "_IS_PERSISTENT_CHARACTER_DEAD(" + h + ")", () => PERSCHAR._IS_PERSISTENT_CHARACTER_DEAD(h));
                if (back is int b && b == ped) match++;
                Log("ped " + ped + " handle " + h + " valido=" + (valid ?? "EXC") + " volta=" + (back ?? "EXC"));
            }
            notes.Add("checados " + checkedN + ", ida e volta ped<->handle bateu em " + match);

            if (persCharCreate.Length > 0)
            {
                var hash = MISC.GET_HASH_KEY(persCharCreate);
                var pc = Step("PERSCHAR", "_CREATE_PERSISTENT_CHARACTER(" + persCharCreate + ")",
                    () => PERSCHAR._CREATE_PERSISTENT_CHARACTER(hash));
                if (pc is int handle && handle > 0)
                {
                    Step("PERSCHAR", "_IS_PERSISTENT_CHARACTER_VALID", () => PERSCHAR._IS_PERSISTENT_CHARACTER_VALID(handle));
                    Step("PERSCHAR", "_FORCE_SPAWN_PERSCHAR", () => PERSCHAR._FORCE_SPAWN_PERSCHAR(handle, true));
                    Wait(2000);
                    Shot("perschar-criado", null);
                    Step("PERSCHAR", "_GET_PERSCHAR_PED_INDEX", () => PERSCHAR._GET_PERSCHAR_PED_INDEX(handle));
                    Call("PERSCHAR", "_FORCE_DESPAWN_PERSCHAR", () => PERSCHAR._FORCE_DESPAWN_PERSCHAR(handle));
                    Call("PERSCHAR", "_DELETE_PERSCHAR", () => PERSCHAR._DELETE_PERSCHAR(handle));
                }
            }
            if (found.Count > 0 && match == 0) verdict = "FALHOU";
        }

        // ---------- prints ----------

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        static IntPtr GameWindow() { return Process.GetCurrentProcess().MainWindowHandle; }

        static Rectangle GameRect()
        {
            var h = GameWindow();
            RECT r;
            if (h != IntPtr.Zero && GetClientRect(h, out r) && r.R > 0)
            {
                var o = new POINT();
                ClientToScreen(h, ref o);
                return new Rectangle(o.X, o.Y, r.R, r.B);
            }
            return System.Windows.Forms.Screen.PrimaryScreen.Bounds;
        }

        static string WindowInfo()
        {
            var h = GameWindow();
            return GameRect() + (h == GetForegroundWindow() ? " em primeiro plano" : " FORA de primeiro plano");
        }

        // Salva o print e devolve um texto com brilho e, se houver referência, a mudança por quadrante
        // (0-255, média do |delta| de luminância numa miniatura 160x90).
        string Shot(string tag, string refTag)
        {
            if (!shotsOn) return "(prints desligados)";
            try
            {
                Directory.CreateDirectory(shotDir);
                string name = (++shotNo).ToString("00") + "-" + tag;
                var lum = Capture(Path.Combine(shotDir, name + ".jpg"));
                if (lum == null) return "print falhou";
                thumbs[name] = lum;
                lastShot = name;
                float mean = lum.Average();
                string res = "brilho=" + mean.ToString("0");
                if (mean < 4) res += " PRETO(captura falhou?)";
                if (refTag != null && thumbs.ContainsKey(refTag)) res += " mudanca " + Quadrants(thumbs[refTag], lum);
                Log("print " + name + " " + res);
                return res;
            }
            catch (Exception ex)
            {
                Log("print " + tag + " EXC " + ex.Message);
                return "print falhou";
            }
        }

        // Print pelo ddshot (processo à parte; a sonda cede o quadro com Wait enquanto ele roda) ou, sem ele, GDI.
        float[] Capture(string file)
        {
            if (!File.Exists(shotTool))
            {
                var rect = GameRect();
                using (var full = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(full)) g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
                    int w = Math.Min(1600, rect.Width), hh = rect.Height * w / rect.Width;
                    using (var small = new Bitmap(full, w, hh)) small.Save(file, ImageFormat.Jpeg);
                    return Thumb(full);
                }
            }
            // A duplicação às vezes falha em série (INVALID_CALL) por alguns segundos; tenta de novo antes de desistir.
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (RunShotTool(file)) using (var ms = new MemoryStream(File.ReadAllBytes(file)))
                    using (var bmp = new Bitmap(ms)) return Thumb(bmp);
                Wait(1000);
            }
            return null;
        }

        bool RunShotTool(string file)
        {
            var psi = new ProcessStartInfo(shotTool, "\"" + file + "\" 0 0 1600")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
            };
            using (var p = Process.Start(psi))
            {
                var until = DateTime.Now.AddSeconds(8);
                while (!p.HasExited && DateTime.Now < until) Wait(50);
                if (!p.HasExited) { Try(() => p.Kill()); Log("ddshot nao respondeu"); return false; }
                if (p.ExitCode != 0) { Log("ddshot: " + p.StandardError.ReadToEnd().Trim()); return false; }
            }
            return true;
        }

        string Ini(string section, string key, string fallback)
        {
            var v = IniRaw(iniPath, section, key);
            return v.Length > 0 ? v : fallback;
        }

        bool IniBool(string section, string key, bool fallback) =>
            bool.TryParse(Ini(section, key, fallback.ToString()), out bool b) ? b : fallback;

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
                    return l.Substring(eq + 1).Trim();
            }
            return "";
        }

        static float[] Thumb(Bitmap src)
        {
            const int W = 160, H = 90;
            var lum = new float[W * H];
            using (var t = new Bitmap(W, H, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(t))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(src, 0, 0, W, H);
                }
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        var c = t.GetPixel(x, y);
                        lum[y * W + x] = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
                    }
            }
            return lum;
        }

        static string Quadrants(float[] a, float[] b)
        {
            var q = QuadValues(a, b);
            return "SE=" + q[0].ToString("0.0") + " SD=" + q[1].ToString("0.0") +
                   " IE=" + q[2].ToString("0.0") + " ID=" + q[3].ToString("0.0");
        }

        static float[] QuadValues(float[] a, float[] b)
        {
            const int W = 160, H = 90;
            var sum = new float[4];
            var cnt = new int[4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int q = (y < H / 2 ? 0 : 2) + (x < W / 2 ? 0 : 1);
                    sum[q] += Math.Abs(a[y * W + x] - b[y * W + x]);
                    cnt[q]++;
                }
            for (int q = 0; q < 4; q++) sum[q] /= cnt[q];
            return sum;
        }

        // Maior mudança por quadrante entre dois prints; -1 se algum faltar.
        float MaxChange(string from, string to)
        {
            if (from == null || to == null || !thumbs.ContainsKey(from) || !thumbs.ContainsKey(to)) return -1;
            return QuadValues(thumbs[from], thumbs[to]).Max();
        }

        // ---------- estado e resumo ----------

        void LoadState()
        {
            state.Clear();
            if (!File.Exists(statePath)) return;
            var lines = File.ReadAllLines(statePath);
            if (lines.Length == 0 || lines[0] != "build=" + build) { Log("estado de outra build descartado"); return; }
            foreach (var l in lines.Skip(1))
            {
                var p = l.Split('\t');
                if (p.Length < 2) continue;
                var st = p[1] == "RODANDO" ? "TRAVOU" : p[1];
                var det = p[1] == "RODANDO" ? "jogo travou ou fechou nesta secao; a ultima linha '->' do log e o native" : (p.Length > 2 ? p[2] : "");
                state[p[0]] = new[] { st, det };
            }
            if (lines.Skip(1).Any(l => l.Contains("\tRODANDO"))) { SaveState(); WriteSummary(); }
        }

        void SetState(string name, string status, string detail)
        {
            state[name] = new[] { status, detail };
            SaveState();
        }

        void SaveState()
        {
            var sb = new StringBuilder("build=" + build + Environment.NewLine);
            foreach (var kv in state) sb.AppendLine(kv.Key + "\t" + kv.Value[0] + "\t" + kv.Value[1]);
            File.WriteAllText(statePath, sb.ToString());
        }

        void WriteSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("NativeProbe build " + build + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("prints: " + (shotDir ?? "-"));
            foreach (var n in Order)
            {
                string[] v;
                if (!enabled[n]) sb.AppendLine(n + ": DESLIGADO no ini");
                else if (state.TryGetValue(n, out v)) sb.AppendLine(n + ": " + v[0] + (v[1].Length > 0 ? " - " + v[1] : ""));
                else sb.AppendLine(n + ": NAO RODOU");
            }
            File.WriteAllText(summaryPath, sb.ToString());
        }

        void Fail(string why)
        {
            verdict = "FALHOU";
            notes.Add(why);
            Log(why);
        }

        // ---------- utilitários ----------

        int SpawnPed(float ahead)
        {
            var me = Game.Player.Character;
            var fwd = me.ForwardVector;
            var at = me.Position;
            float x = at.X + fwd.X * ahead, y = at.Y + fwd.Y * ahead, z = at.Z;
            float ground;
            if (MISC.GET_GROUND_Z_FOR_3D_COORD(x, y, z + 50f, &ground, false) && Math.Abs(ground - z) < 40f) z = ground;
            uint model = MISC.GET_HASH_KEY(pedModel);
            STREAMING.REQUEST_MODEL(model, false);
            if (!WaitFor(() => STREAMING.HAS_MODEL_LOADED(model), 3000)) { Log("modelo nao carregou: " + pedModel); return 0; }
            var r = Step("PED", "CREATE_PED(" + pedModel + ") a " + ahead + "m",
                () => PED.CREATE_PED(model, x, y, z, 0f, false, false, false, false));
            if (!(r is int ped) || ped == 0) return 0;
            PED._SET_RANDOM_OUTFIT_VARIATION(ped, true);
            ENTITY.SET_ENTITY_AS_MISSION_ENTITY(ped, true, true);
            spawned.Add(ped);
            return ped;
        }

        int Sphere(Vector3 at, float radius)
        {
            var r = Step("VOLUME", "CREATE_VOLUME_SPHERE r=" + radius,
                () => VOLUME.CREATE_VOLUME_SPHERE(at, new Vector3(0, 0, 0), new Vector3(radius, radius, radius)));
            int v = r is int h ? h : 0;
            if (v != 0) volumes.Add(v);
            return v;
        }

        void Cleanup()
        {
            foreach (var z in zones) Try(() => LAW._REMOVE_GUARD_ZONE(z));
            zones.Clear();
            foreach (var v in volumes) Try(() => { if (VOLUME.DOES_VOLUME_EXIST(v)) VOLUME.DELETE_VOLUME(v); });
            volumes.Clear();
            zoneVolumes.Clear();
            foreach (var p in spawned)
            {
                int h = p;
                try { if (ENTITY.DOES_ENTITY_EXIST(h)) PED.DELETE_PED(&h); } catch { }
            }
            spawned.Clear();
        }

        object Step(string cat, string call, Func<object> f)
        {
            Log("-> " + cat + " " + call);
            try
            {
                var r = f();
                Log("   ok = " + (r ?? "void"));
                return r;
            }
            catch (Exception ex)
            {
                Log("   EXC " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        void Call(string cat, string call, Action a)
        {
            Step(cat, call, () => { a(); return null; });
        }

        static string Safe(Func<object> f)
        {
            try { return Convert.ToString(f()); }
            catch (Exception ex) { return "EXC(" + ex.Message + ")"; }
        }

        static bool SafeBool(Func<bool> f)
        {
            try { return f(); } catch { return false; }
        }

        static void Try(Action a)
        {
            try { a(); } catch { }
        }

        bool WaitFor(Func<bool> cond, int timeoutMs)
        {
            var until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < until)
            {
                if (cond()) return true;
                Wait(100);
            }
            return cond();
        }

        static string Fmt(Vector3 v)
        {
            return v.X.ToString("0.0") + "," + v.Y.ToString("0.0") + "," + v.Z.ToString("0.0");
        }

        void Log(string line)
        {
            // Flush imediato: se o jogo cair, a última linha "->" mostra o native culpado. O log acumula as rodadas.
            File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine);
        }
    }
}
