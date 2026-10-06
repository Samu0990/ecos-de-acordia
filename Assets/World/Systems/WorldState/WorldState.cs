using System.Collections.Generic;
using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Fases de Valtéria/Campânula na abertura e no Ato I (a ordem importa: só avança).
    /// Campânula viva → a Fenda abre → o brilho cai → a Corrupção cresce → o Vórtice surge →
    /// Dó Partido é enfrentado → a região muda depois da vitória.
    /// </summary>
    public enum ValteriaPhase
    {
        CampanulaViva = 0,
        FendaAberta = 1,
        BrilhoCaiu = 2,
        CorrupcaoCrescente = 3,
        VorticeAtivo = 4,
        DoEnfrentado = 5,
        DoReafinado = 6,
    }

    /// <summary>
    /// Estado do mundo que sobrevive à troca de cenas e é salvo em disco (JSON em persistentDataPath):
    /// fase do Ato I, flags, Notas reafinadas, Relíquias (com quem estão), Vórtices quebrados, nível de
    /// Corrupção por reino, reinos descobertos e o último ponto de retorno. Tudo que muda o mundo passa
    /// por aqui — as cenas só LEEM (WorldStateSwitch, VortexZone, RegionGate) e reagem a OnChanged.
    /// </summary>
    public static class WorldState
    {
        [System.Serializable]
        class Data
        {
            public int version = 1;
            public int valteriaPhase;
            public List<string> flags = new List<string>();
            public List<string> notesReafinadas = new List<string>();
            public List<string> relicsWithAren = new List<string>();
            public List<string> vorticesBroken = new List<string>();
            public List<string> discovered = new List<string>();
            public List<string> corruptionKeys = new List<string>();
            public List<float> corruptionValues = new List<float>();
            public string lastScene = "";
            public string lastCheckpoint = "";
        }

        static Data d;
        static bool dirty;
        public static event System.Action OnChanged;

        static Data D { get { if (d == null) Load(); return d; } }
        static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "elyndra_world.json");

        // ------------------------------------------------------------ leitura

        public static ValteriaPhase Phase => (ValteriaPhase)D.valteriaPhase;
        public static bool Has(string flag) => D.flags.Contains(flag);
        public static bool NoteReafinada(string noteId) => D.notesReafinadas.Contains(noteId);
        public static int NotesReafinadas => D.notesReafinadas.Count;
        public static bool RelicWithAren(string relicId) => D.relicsWithAren.Contains(relicId);
        /// <summary>Quantas facetas do Acorde-Matriz estão com o Aren (a Partilha de Vael em risco).</summary>
        public static int RelicsWithAren => D.relicsWithAren.Count;
        public static bool VortexBroken(string id) => D.vorticesBroken.Contains(id);
        public static bool Discovered(RegionId r) => D.discovered.Contains(r.ToString());
        public static string LastScene => D.lastScene;
        public static string LastCheckpoint => D.lastCheckpoint;

        /// <summary>Corrupção do reino (0 = intacto, 1 = tomado). Valtéria segue a fase do Ato I.</summary>
        public static float Corruption(RegionId r)
        {
            int i = D.corruptionKeys.IndexOf(r.ToString());
            float stored = i >= 0 ? D.corruptionValues[i] : DefaultCorruption(r);
            if (r == RegionId.Valteria)
            {
                switch (Phase)
                {
                    case ValteriaPhase.CampanulaViva: case ValteriaPhase.FendaAberta: return Mathf.Max(stored, 0f);
                    case ValteriaPhase.BrilhoCaiu: return Mathf.Max(stored, 0.2f);
                    case ValteriaPhase.CorrupcaoCrescente: return Mathf.Max(stored, 0.45f);
                    case ValteriaPhase.VorticeAtivo: case ValteriaPhase.DoEnfrentado: return Mathf.Max(stored, 0.7f);
                    case ValteriaPhase.DoReafinado: return Mathf.Min(stored, 0.25f);
                }
            }
            return stored;
        }

        /// <summary>Os reinos começam com a Corrupção que as outras seis luzes trouxeram.</summary>
        static float DefaultCorruption(RegionId r)
        {
            var def = WorldCanon.Region(r);
            if (def == null) return 0f;
            if (r == RegionId.FronteiraMuda) return 0.9f;
            if (!string.IsNullOrEmpty(def.noteId) && !NoteReafinada(def.noteId)) return 0.35f + 0.08f * def.dangerTier;
            return 0.15f + 0.05f * def.dangerTier;
        }

        /// <summary>
        /// Avalia uma condição de texto (usada por portões, interruptores e rotas):
        /// "" (sempre) · "flag:x" · "!flag:x" · "nota:do" · "fase:VorticeAtivo" (fase ≥) · "fase<:DoReafinado" ·
        /// "reliquias:3" (≥ 3 com o Aren) · "vortice:id" (quebrado) · várias com "&amp;".
        /// </summary>
        public static bool Check(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            foreach (var raw in condition.Split('&'))
            {
                var c = raw.Trim();
                if (c.Length == 0) continue;
                bool neg = c.StartsWith("!");
                if (neg) c = c.Substring(1);
                bool ok;
                int k = c.IndexOf(':');
                string key = k >= 0 ? c.Substring(0, k) : c, val = k >= 0 ? c.Substring(k + 1) : "";
                switch (key)
                {
                    case "flag": ok = Has(val); break;
                    case "nota": ok = NoteReafinada(val); break;
                    case "fase": ok = System.Enum.TryParse(val, out ValteriaPhase p) && Phase >= p; break;
                    case "fase<": ok = System.Enum.TryParse(val, out ValteriaPhase q) && Phase < q; break;
                    case "reliquias": ok = int.TryParse(val, out int n) && RelicsWithAren >= n; break;
                    case "vortice": ok = VortexBroken(val); break;
                    case "nunca": ok = false; break;   // áreas futuras ainda sem conteúdo
                    default: ok = Has(c); break;
                }
                if (neg) ok = !ok;
                if (!ok) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ escrita

        public static void AdvancePhase(ValteriaPhase p)
        {
            if ((int)p <= D.valteriaPhase) return;
            D.valteriaPhase = (int)p;
            Changed();
        }

        public static void SetFlag(string flag, bool on = true)
        {
            bool has = D.flags.Contains(flag);
            if (on == has) return;
            if (on) D.flags.Add(flag); else D.flags.Remove(flag);
            Changed();
        }

        public static void ReafinarNote(string noteId)
        {
            if (D.notesReafinadas.Contains(noteId)) return;
            D.notesReafinadas.Add(noteId);
            if (noteId == "do") AdvancePhase(ValteriaPhase.DoReafinado);
            Changed();
        }

        public static void SetRelicWithAren(string relicId, bool with)
        {
            bool has = D.relicsWithAren.Contains(relicId);
            if (with == has) return;
            if (with) D.relicsWithAren.Add(relicId); else D.relicsWithAren.Remove(relicId);
            Changed();
        }

        public static void BreakVortex(string id)
        {
            if (D.vorticesBroken.Contains(id)) return;
            D.vorticesBroken.Add(id);
            Changed();
        }

        public static void Discover(RegionId r)
        {
            if (D.discovered.Contains(r.ToString())) return;
            D.discovered.Add(r.ToString());
            Changed();
        }

        public static void SetCorruption(RegionId r, float v)
        {
            int i = D.corruptionKeys.IndexOf(r.ToString());
            if (i < 0) { D.corruptionKeys.Add(r.ToString()); D.corruptionValues.Add(Mathf.Clamp01(v)); }
            else D.corruptionValues[i] = Mathf.Clamp01(v);
            Changed();
        }

        public static void SetCheckpoint(string scene, string checkpointId)
        {
            D.lastScene = scene; D.lastCheckpoint = checkpointId;
            dirty = true; Save();
        }

        static void Changed() { dirty = true; Save(); OnChanged?.Invoke(); }

        // ------------------------------------------------------------ disco

        public static void Load()
        {
            d = null;
            try { if (System.IO.File.Exists(FilePath)) d = JsonUtility.FromJson<Data>(System.IO.File.ReadAllText(FilePath)); }
            catch (System.Exception e) { Debug.LogWarning("[WorldState] não consegui ler o save: " + e.Message); }
            if (d == null) d = new Data();
            // testes do executável podem pedir um mundo limpo ou numa fase
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-eda-world-reset") d = new Data();
                if (a.StartsWith("-eda-world-phase=") && System.Enum.TryParse(a.Substring(17), out ValteriaPhase p)) d.valteriaPhase = (int)p;
                if (a.StartsWith("-eda-world-flag=")) { var f = a.Substring(16); if (!d.flags.Contains(f)) d.flags.Add(f); }
                if (a.StartsWith("-eda-world-note=")) { var n = a.Substring(16); if (!d.notesReafinadas.Contains(n)) d.notesReafinadas.Add(n); }
            }
        }

        public static void Save()
        {
            if (!dirty || d == null) return;
            dirty = false;
            try { System.IO.File.WriteAllText(FilePath, JsonUtility.ToJson(d, true)); }
            catch (System.Exception e) { Debug.LogWarning("[WorldState] não consegui salvar: " + e.Message); }
        }

        /// <summary>Novo jogo: apaga o estado do mundo.</summary>
        public static void ResetAll() { d = new Data(); dirty = true; Save(); OnChanged?.Invoke(); }

        public static string Describe()
        {
            return $"fase={Phase} notas=[{string.Join(",", D.notesReafinadas)}] relíquias=[{string.Join(",", D.relicsWithAren)}] flags=[{string.Join(",", D.flags)}] vórtices=[{string.Join(",", D.vorticesBroken)}]";
        }
    }
}
