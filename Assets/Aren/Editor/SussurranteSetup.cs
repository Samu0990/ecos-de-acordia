using System.Collections.Generic;
using System.Linq;
using Aren.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Aren.EditorTools
{
    /// <summary>
    /// SUSSURRANTE (prancha do autor, "Eco Primordial · Rasgos"). Modelo/texturas/lâmina vêm de
    /// ArtSource/Sussurrante/scripts (Tripo H3.1 a partir da prancha → Blender: escala 2,85 m,
    /// atlas do Tripo, bake de normal/AO, gradação para a paleta da prancha, esqueleto UAL2).
    /// Gera, de forma idempotente:
    ///   - importação: Humanoid com mapeamento explícito (inclui dedos) e T-pose calculada
    ///     (o modelo vem em pose A), tangentes MikkTSpace, lâmina sem rig;
    ///   - materiais Aren/Sussurrante e Aren/SussurranteBlade;
    ///   - clipes de músculo próprios (Grito, Morte, Ascensão) a partir da pose da UAL2;
    ///   - Sussurrante.controller (UAL2 CC0 + corrida do DPS + clipes próprios);
    ///   - prefab Resources/Sussurrante/Sussurrante.prefab (EnemySussurrante, LOD0/LOD1, lâmina
    ///     no osso weapon_r, pontos de efeito).
    /// Rodar: SussurranteSetup.All() (menu Aren/Setup/Inimigos - Sussurrante).
    /// Prévia sem Play mode: SussurranteSetup.Preview() → Tools/cli/shots/suss_*.png.
    /// </summary>
    public static class SussurranteSetup
    {
        public const string Dir = "Assets/Aren/Enemies/Sussurrante/";
        public const string ModelPath = Dir + "Sussurrante.fbx";
        public const string BladePath = Dir + "SussurranteBlade.fbx";
        const string TexDir = Dir + "Textures/";
        const string AnimDir = Dir + "Anim/";
        public const string PrefabPath = "Assets/Aren/Resources/Sussurrante/Sussurrante.prefab";
        const float Height = 2.85f;

        static readonly (string human, string bone)[] Body =
        {
            ("Hips", "pelvis"), ("Spine", "spine_01"), ("Chest", "spine_02"), ("UpperChest", "spine_03"), ("Neck", "neck_01"), ("Head", "Head"),
            ("LeftShoulder", "clavicle_l"), ("LeftUpperArm", "upperarm_l"), ("LeftLowerArm", "lowerarm_l"), ("LeftHand", "hand_l"),
            ("RightShoulder", "clavicle_r"), ("RightUpperArm", "upperarm_r"), ("RightLowerArm", "lowerarm_r"), ("RightHand", "hand_r"),
            ("LeftUpperLeg", "thigh_l"), ("LeftLowerLeg", "calf_l"), ("LeftFoot", "foot_l"), ("LeftToes", "ball_l"),
            ("RightUpperLeg", "thigh_r"), ("RightLowerLeg", "calf_r"), ("RightFoot", "foot_r"), ("RightToes", "ball_r"),
        };
        static readonly string[] FingerHuman = { "Thumb", "Index", "Middle", "Ring", "Little" };
        static readonly string[] FingerBone = { "thumb", "index", "middle", "ring", "pinky" };
        static readonly string[] Phal = { "Proximal", "Intermediate", "Distal" };

        static IEnumerable<(string human, string bone)> Map()
        {
            foreach (var b in Body) yield return b;
            foreach (var (side, s) in new[] { ("Left", "l"), ("Right", "r") })
                for (int f = 0; f < 5; f++)
                    for (int k = 0; k < 3; k++)
                        yield return (side + " " + FingerHuman[f] + " " + Phal[k], FingerBone[f] + "_0" + (k + 1) + "_" + s);
        }

        [MenuItem("Aren/Setup/Inimigos - Sussurrante")]
        public static string All()
        {
            var log = new System.Text.StringBuilder();
            log.Append(Textures());
            log.Append(Avatar());
            log.Append(Blade());
            log.Append(Materials());
            log.Append(Clips());
            log.Append(Controller());
            log.Append(Prefab());
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ------------------------------------------------------------ importação

        public static string Textures()
        {
            void T(string file, bool normal, bool linear, int size)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(TexDir + file);
                if (ti == null) return;
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal && !linear;
                ti.maxTextureSize = size;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 2;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.SaveAndReimport();
            }
            T("Sussurrante_Albedo.png", false, false, 2048);
            T("Sussurrante_Normal.png", true, true, 2048);
            T("Sussurrante_Mask.png", false, true, 1024);
            return "texturas ok\n";
        }

        public static string Avatar()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (mi == null) return "ERRO: falta " + ModelPath + "\n";
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.optimizeGameObjects = false;          // ossos-marcadores (weapon_r, eye_l...) acessíveis
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.SaveAndReimport();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = Object.Instantiate(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var all = new Dictionary<string, Transform>();
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (!all.ContainsKey(t.name)) all[t.name] = t;
                var map = Map().ToList();
                var missing = map.Where(m => !all.ContainsKey(m.bone)).Select(m => m.bone).ToList();
                if (missing.Count > 0) return "ERRO: ossos não encontrados: " + string.Join(",", missing) + "\n";
                Transform B(string bone) => all.TryGetValue(bone, out var t) ? t : null;

                // T-pose de referência (o modelo vem em pose A, mãos penduradas com a palma para dentro)
                Vector3 up = Vector3.up, down = Vector3.down;
                Vector3 fwd = Flat(B("ball_l").position - B("foot_l").position).normalized;
                var feet = new Dictionary<Transform, Quaternion>();
                foreach (var f in new[] { "foot_l", "foot_r", "ball_l", "ball_r" }) feet[B(f)] = B(f).rotation;
                foreach (var s in new[] { "l", "r" })
                {
                    Vector3 outDir = Flat(B("upperarm_" + s).position - B("spine_03").position).normalized;
                    Aim(B("upperarm_" + s), B("lowerarm_" + s), outDir);
                    Aim(B("lowerarm_" + s), B("hand_" + s), outDir);
                    Aim(B("hand_" + s), B("middle_01_" + s), outDir);
                    foreach (var f in new[] { "index", "middle", "ring", "pinky" })
                    {
                        Aim(B(f + "_01_" + s), B(f + "_02_" + s), outDir);
                        Aim(B(f + "_02_" + s), B(f + "_03_" + s), outDir);
                        Aim(B(f + "_03_" + s), B(f + "_04_leaf_" + s), outDir);
                    }
                    Vector3 thumb = (outDir * 0.7f + fwd * 0.7f - up * 0.15f).normalized;
                    Aim(B("thumb_01_" + s), B("thumb_02_" + s), thumb);
                    Aim(B("thumb_02_" + s), B("thumb_03_" + s), thumb);
                    Aim(B("thumb_03_" + s), B("thumb_04_leaf_" + s), thumb);
                    Aim(B("thigh_" + s), B("calf_" + s), down);
                    Aim(B("calf_" + s), B("foot_" + s), down);
                }
                foreach (var kv in feet) kv.Key.rotation = kv.Value;

                var human = map.Select(m => new HumanBone { humanName = m.human, boneName = m.bone, limit = new HumanLimit { useDefaultValues = true } }).ToArray();
                var skel = new List<SkeletonBone>();
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    skel.Add(new SkeletonBone { name = t == go.transform ? prefab.name : t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
                var hd = mi.humanDescription;
                hd.human = human;
                hd.skeleton = skel.ToArray();
                hd.upperArmTwist = 0.5f; hd.lowerArmTwist = 0.5f; hd.upperLegTwist = 0.5f; hd.lowerLegTwist = 0.5f;
                hd.armStretch = 0.05f; hd.legStretch = 0.05f; hd.feetSpacing = 0f;
                hd.hasTranslationDoF = false;
                mi.humanDescription = hd;
                mi.SaveAndReimport();
            }
            finally { Object.DestroyImmediate(go); }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<UnityEngine.Avatar>().FirstOrDefault();
            return "avatar válido=" + (avatar != null && avatar.isValid) + " humano=" + (avatar != null && avatar.isHuman) + "\n";
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (bone == null || child == null) return;
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(cur, dir) * bone.rotation;
        }

        public static string Blade()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(BladePath);
            if (mi == null) return "ERRO: falta " + BladePath + "\n";
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();
            return "lâmina ok\n";
        }

        static Material Mat(string path, string shader)
        {
            var sh = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.enableInstancing = false;
            return m;
        }

        public static string Materials()
        {
            var noise = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Aren/Resources/VFX/noise_perlin.png");
            var body = Mat(Dir + "Sussurrante_Body.mat", "Aren/Sussurrante");
            body.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(TexDir + "Sussurrante_Albedo.png"));
            body.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>(TexDir + "Sussurrante_Normal.png"));
            body.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture>(TexDir + "Sussurrante_Mask.png"));
            body.SetTexture("_Noise", noise);
            body.SetTexture("_Void", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Aren/Resources/VFX/Space/space_purple_veins.png"));
            body.SetFloat("_Height", Height);
            body.SetColor("_RimColor", new Color(0.30f, 0.28f, 0.52f));
            body.SetFloat("_RimPower", 2.6f);
            body.SetFloat("_NightLift", 0.13f);
            EditorUtility.SetDirty(body);
            var blade = Mat(Dir + "Sussurrante_Blade.mat", "Aren/SussurranteBlade");
            blade.SetTexture("_Noise", noise);
            EditorUtility.SetDirty(blade);
            return "materiais ok (shader corpo=" + (body.shader != null && body.shader.isSupported) + ", lâmina=" + (blade.shader != null && blade.shader.isSupported) + ")\n";
        }

        // ------------------------------------------------------------ clipes de músculo

        /// <summary>Desvios por músculo em cada chave (somados à pose do Zombie_Idle da UAL2).
        /// "L&R x" vale para os dois lados (o espaço de músculo da Unity é espelhado).</summary>
        class PoseClip
        {
            public string name; public float[] times;
            public readonly Dictionary<string, float[]> d = new Dictionary<string, float[]>();
            public float[] rootDy, rootPitch, rootDz;
            public float trembleFrom = -1, trembleTo = -1, trembleAmp;
            public string[] tremble = new string[0];
            public void M(string muscle, params float[] v)
            {
                if (muscle.StartsWith("L&R "))
                {
                    string m = muscle.Substring(4);
                    d["Left " + m] = v; d["Right " + m] = v;
                }
                else d[muscle] = v;
            }
        }

        public static string Clips()
        {
            if (!AssetDatabase.IsValidFolder(AnimDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Anim");
            var src = ArenAnimatorSetup.Clip("Zombie_Idle_Loop");
            if (src == null) return "ERRO: Zombie_Idle_Loop\n";
            var log = new System.Text.StringBuilder();

            // GRITO (2,8 s): inspira encolhido (0–1,0), estoura arqueado para trás com os braços abertos
            // e a cabeça jogada (1,05–2,25, tremendo), volta.
            // convenção do espaço de músculos da Unity: "A-B" vai de -1 (A) a +1 (B) — Front-Back: -1 frente;
            // Down-Up: -1 baixo. A pose-base (UAL2) olha para -Z antes do giro de 180°: inclinação da raiz
            // para a FRENTE é negativa e o avanço em z também.
            var scream = new PoseClip { name = "Sussurrante_Scream", times = new[] { 0f, 0.35f, 0.95f, 1.1f, 1.6f, 2.25f, 2.8f } };
            scream.M("Spine Front-Back", 0, -0.35f, -0.45f, 0.35f, 0.4f, 0.3f, 0);
            scream.M("Chest Front-Back", 0, -0.3f, -0.4f, 0.5f, 0.55f, 0.45f, 0);
            scream.M("UpperChest Front-Back", 0, -0.2f, -0.3f, 0.4f, 0.45f, 0.35f, 0);
            scream.M("Neck Nod Down-Up", 0, -0.3f, -0.4f, 0.6f, 0.7f, 0.5f, 0);
            scream.M("Head Nod Down-Up", 0, -0.35f, -0.45f, 0.7f, 0.8f, 0.6f, 0);
            scream.M("L&R Shoulder Down-Up", 0, -0.2f, -0.25f, 0.35f, 0.4f, 0.3f, 0);
            scream.M("L&R Arm Down-Up", 0, -0.25f, -0.3f, 0.3f, 0.35f, 0.25f, 0);
            scream.M("L&R Arm Front-Back", 0, -0.45f, -0.5f, 0.6f, 0.65f, 0.5f, 0);
            scream.M("L&R Forearm Stretch", 0, -0.35f, -0.45f, 0.6f, 0.65f, 0.45f, 0);
            scream.M("L&R Hand Down-Up", 0, -0.25f, -0.3f, 0.6f, 0.65f, 0.45f, 0);
            scream.M("L&R Upper Leg Front-Back", 0, -0.25f, -0.3f, -0.05f, -0.05f, -0.05f, 0);
            scream.M("L&R Lower Leg Stretch", 0, -0.35f, -0.4f, -0.12f, -0.12f, -0.1f, 0);
            scream.M("Jaw Close", 0, 0, 0, -1, -1, -0.8f, 0);
            scream.rootDy = new[] { 0f, -0.06f, -0.07f, 0f, 0f, 0f, 0f };
            scream.trembleFrom = 1.1f; scream.trembleTo = 2.25f; scream.trembleAmp = 0.06f;
            scream.tremble = new[] { "Head Nod Down-Up", "Head Tilt Left-Right", "Left Arm Down-Up", "Right Arm Down-Up", "Left Hand In-Out", "Right Hand In-Out", "Chest Left-Right" };
            log.Append(BuildClip(src, scream, 2.8f));

            // MORTE (2,2 s): o golpe joga para trás, os joelhos cedem, ajoelha e tomba para a frente
            // (o corpo vira lasca enquanto isso).
            var death = new PoseClip { name = "Sussurrante_Death", times = new[] { 0f, 0.25f, 0.6f, 1.0f, 1.5f, 2.2f } };
            death.M("Spine Front-Back", 0, 0.4f, -0.2f, -0.5f, -0.7f, -0.8f);
            death.M("Chest Front-Back", 0, 0.4f, -0.2f, -0.4f, -0.6f, -0.7f);
            death.M("UpperChest Front-Back", 0, 0.3f, -0.15f, -0.3f, -0.5f, -0.55f);
            death.M("Neck Nod Down-Up", 0, 0.5f, -0.3f, -0.5f, -0.6f, -0.7f);
            death.M("Head Nod Down-Up", 0, 0.6f, -0.3f, -0.6f, -0.8f, -0.9f);
            death.M("L&R Arm Down-Up", 0, 0.2f, -0.6f, -0.8f, -0.8f, -0.8f);
            death.M("L&R Arm Front-Back", 0, 0.3f, -0.2f, -0.3f, -0.3f, -0.3f);
            death.M("L&R Forearm Stretch", 0, 0.3f, 0.2f, 0.4f, 0.5f, 0.5f);
            death.M("L&R Upper Leg Front-Back", 0, 0.1f, -0.5f, -0.75f, -0.75f, -0.75f);
            death.M("L&R Lower Leg Stretch", 0, 0f, -0.8f, -1f, -1f, -1f);
            death.M("L&R Foot Up-Down", 0, 0, 0.3f, 0.5f, 0.5f, 0.5f);
            death.M("Jaw Close", 0, -1, -0.6f, -0.4f, -0.4f, -0.4f);
            death.rootDy = new[] { 0f, 0.02f, -0.22f, -0.42f, -0.48f, -0.52f };
            death.rootPitch = new[] { 0f, 10f, -8f, -18f, -28f, -35f };
            death.rootDz = new[] { 0f, 0.05f, 0f, -0.05f, -0.08f, -0.1f };
            log.Append(BuildClip(src, death, 2.2f));

            // ASCENSÃO (1,6 s): nasce encolhido (das lascas) e se ergue até a postura de espreita,
            // olhando para cima no fim.
            var rise = new PoseClip { name = "Sussurrante_Rise", times = new[] { 0f, 0.6f, 1.2f, 1.6f } };
            rise.M("Spine Front-Back", -0.8f, -0.6f, -0.15f, 0);
            rise.M("Chest Front-Back", -0.6f, -0.45f, -0.1f, 0);
            rise.M("UpperChest Front-Back", -0.5f, -0.35f, -0.05f, 0);
            rise.M("Neck Nod Down-Up", -0.6f, -0.4f, 0.3f, 0);
            rise.M("Head Nod Down-Up", -0.7f, -0.5f, 0.35f, 0);
            rise.M("L&R Arm Down-Up", -0.5f, -0.4f, -0.1f, 0);
            rise.M("L&R Arm Front-Back", -0.4f, -0.3f, -0.1f, 0);
            rise.M("L&R Forearm Stretch", -0.2f, 0f, 0.2f, 0);
            rise.M("L&R Upper Leg Front-Back", -0.8f, -0.6f, -0.2f, 0);
            rise.M("L&R Lower Leg Stretch", -1f, -0.7f, -0.2f, 0);
            rise.rootDy = new[] { -0.45f, -0.33f, -0.07f, 0f };
            rise.rootPitch = new[] { -18f, -12f, -3f, 0f };
            log.Append(BuildClip(src, rise, 1.6f));
            return log.ToString();
        }

        static string BuildClip(AnimationClip src, PoseClip p, float length)
        {
            string path = AnimDir + p.name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            clip.frameRate = 30f;
            var bindings = AnimationUtility.GetCurveBindings(src);
            int used = 0;
            var rootT = new Dictionary<string, float>(); var rootQ = new Dictionary<string, float>();
            foreach (var b in bindings)
            {
                var c = AnimationUtility.GetEditorCurve(src, b);
                if (c == null) continue;
                float baseV = c.Evaluate(0f);
                string prop = b.propertyName;
                if (prop.StartsWith("RootT.")) { rootT[prop] = baseV; continue; }
                if (prop.StartsWith("RootQ.")) { rootQ[prop] = baseV; continue; }
                if (prop.StartsWith("Motion")) continue;
                float[] dv = null;
                foreach (var kv in p.d)
                    if (Same(prop, kv.Key)) { dv = kv.Value; break; }
                bool trem = p.tremble.Any(t => Same(prop, t));
                var curve = new AnimationCurve();
                if (dv == null && !trem)
                {
                    curve.AddKey(0f, baseV); curve.AddKey(length, baseV);
                }
                else
                {
                    if (dv != null) used++;
                    var times = new List<float>(p.times);
                    if (trem) for (float t = p.trembleFrom; t <= p.trembleTo; t += 0.06f) times.Add(t);
                    times = times.Distinct().OrderBy(t => t).ToList();
                    foreach (float t in times)
                    {
                        float v = baseV + (dv != null ? Sample(p.times, dv, t) : 0f);
                        if (trem && t > p.trembleFrom && t < p.trembleTo)
                            v += p.trembleAmp * Mathf.Sin(t * 47f + prop.Length) * Mathf.Sin(t * 13f);
                        curve.AddKey(t, Mathf.Clamp(v, -1f, 1f));
                    }
                }
                for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            // corpo (centro de massa): altura e inclinação para a frente
            var q0 = new Quaternion(Get(rootQ, "RootQ.x"), Get(rootQ, "RootQ.y"), Get(rootQ, "RootQ.z"), Get(rootQ, "RootQ.w", 1f));

            var curves = new Dictionary<string, AnimationCurve>();
            foreach (var n in new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }) curves[n] = new AnimationCurve();
            for (int i = 0; i < p.times.Length; i++)
            {
                float t = p.times[i];
                float dy = p.rootDy != null ? p.rootDy[i] : 0f;
                float dz = p.rootDz != null ? p.rootDz[i] : 0f;
                float pitch = p.rootPitch != null ? p.rootPitch[i] : 0f;
                var q = Quaternion.Euler(pitch, 0f, 0f) * q0;
                curves["RootT.x"].AddKey(t, Get(rootT, "RootT.x"));
                curves["RootT.y"].AddKey(t, Get(rootT, "RootT.y", 1f) + dy);
                curves["RootT.z"].AddKey(t, Get(rootT, "RootT.z") + dz);
                curves["RootQ.x"].AddKey(t, q.x); curves["RootQ.y"].AddKey(t, q.y); curves["RootQ.z"].AddKey(t, q.z); curves["RootQ.w"].AddKey(t, q.w);
            }
            foreach (var kv in curves)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), kv.Value);
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = false;
            // igual ao importador da UAL2 (ArenModelPostprocessor): raiz "Original" assada na pose e girada
            // 180° — a pose-base vem da UAL2, cuja raiz aponta para trás nos avatares do projeto
            st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true;
            st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
            st.orientationOffsetY = 180f;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            EditorUtility.SetDirty(clip);
            return p.name + ": " + used + " músculos animados, humano=" + clip.humanMotion + "\n";
        }

        static float Get(Dictionary<string, float> d, string k, float def = 0f) => d.TryGetValue(k, out var v) ? v : def;

        /// <summary>Compara nomes de músculo ignorando a forma (a curva usa "LeftHand.Index.1 Stretched"
        /// para dedos e o nome do HumanTrait para o resto).</summary>
        static bool Same(string a, string b) => Norm(a) == Norm(b);
        static string Norm(string s) => s.Replace(".", " ").Replace("-", "").Replace(" ", "").ToLowerInvariant();

        static float Sample(float[] times, float[] v, float t)
        {
            if (t <= times[0]) return v[0];
            for (int i = 0; i < times.Length - 1; i++)
                if (t <= times[i + 1])
                {
                    float k = (t - times[i]) / Mathf.Max(1e-5f, times[i + 1] - times[i]);
                    return Mathf.Lerp(v[i], v[i + 1], Mathf.SmoothStep(0f, 1f, k));
                }
            return v[v.Length - 1];
        }

        // ------------------------------------------------------------ Animator

        public static string Controller()
        {
            string p = Dir + "Sussurrante.controller";
            // reconstrói NO MESMO asset (GUID estável: apagar e recriar deixava o prefab carregado
            // apontando para o controller antigo)
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(p);
            foreach (var par in ctrl.parameters.ToArray()) ctrl.RemoveParameter(par);
            var sm0 = ctrl.layers[0].stateMachine;
            foreach (var cs in sm0.states.ToArray()) sm0.RemoveState(cs.state);
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(p).OfType<BlendTree>().ToArray()) Object.DestroyImmediate(sub, true);
            ctrl.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "StateSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "LocoSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = ctrl.layers[0].stateMachine;

            var run = AssetDatabase.LoadAllAssetsAtPath("Assets/Dynamic Parkour System/Model/Animations/Run.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            var bt = new BlendTree { name = "Locomotion", blendParameter = "MoveSpeed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(bt, ctrl);
            bt.AddChild(ArenAnimatorSetup.Clip("Zombie_Idle_Loop"), 0f);
            bt.AddChild(ArenAnimatorSetup.Clip("Zombie_Walk_Fwd_Loop"), 2.2f);
            if (run != null) bt.AddChild(run, 5.5f);
            var loco = sm.AddState("Locomotion");
            loco.motion = bt; loco.speedParameter = "LocoSpeed"; loco.speedParameterActive = true;
            sm.defaultState = loco;

            void S(string n, Motion clip)
            {
                var st = sm.AddState(n);
                st.motion = clip;
                st.speedParameter = "StateSpeed"; st.speedParameterActive = true;
                st.writeDefaultValues = true;
            }
            S("SlashA", ArenAnimatorSetup.Clip("Sword_Regular_A"));
            S("SlashB", ArenAnimatorSetup.Clip("Sword_Regular_B"));
            S("SlashC", ArenAnimatorSetup.Clip("Sword_Regular_C"));
            S("Heavy", ArenAnimatorSetup.Clip("Sword_Heavy_Combo"));
            S("Dash", ArenAnimatorSetup.Clip("Sword_Dash"));
            S("Hurt", ArenAnimatorSetup.Clip("Idle_Shield_Break"));
            S("Knock", ArenAnimatorSetup.Clip("Hit_Knockback"));
            S("GetUp", ArenAnimatorSetup.Clip("LayToIdle"));
            S("Scream", AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "Sussurrante_Scream.anim"));
            S("Death", AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "Sussurrante_Death.anim"));
            S("Rise", AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "Sussurrante_Rise.anim"));
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return "controller ok (corrida=" + (run != null) + ")\n";
        }

        // ------------------------------------------------------------ prefab

        public static string Prefab()
        {
            var log = new System.Text.StringBuilder();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var bladeFbx = AssetDatabase.LoadAssetAtPath<GameObject>(BladePath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<UnityEngine.Avatar>().FirstOrDefault();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Dir + "Sussurrante.controller");
            var bodyMat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Sussurrante_Body.mat");
            var bladeMat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Sussurrante_Blade.mat");
            if (fbx == null || bladeFbx == null || avatar == null || ctrl == null) return "ERRO: falta fbx/lâmina/avatar/controller\n";
            string resDir = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(resDir)) AssetDatabase.CreateFolder("Assets/Aren/Resources", "Sussurrante");

            var root = new GameObject("Sussurrante");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            var an = model.GetComponent<Animator>(); if (an == null) an = model.AddComponent<Animator>();
            an.avatar = avatar; an.runtimeAnimatorController = ctrl;
            an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var all = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (!all.ContainsKey(t.name)) all[t.name] = t;
            // o modelo tem que olhar para +Z (a IA gira a raiz): mede pelos dedos dos pés e corrige o giro
            {
                Vector3 toes = Flat(all["ball_l"].position - all["foot_l"].position) + Flat(all["ball_r"].position - all["foot_r"].position);
                float yaw = Vector3.SignedAngle(toes, Vector3.forward, Vector3.up);
                if (Mathf.Abs(yaw) > 20f)
                {
                    model.transform.localRotation = Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f);
                    log.Append("modelo girado " + Mathf.Round(yaw / 90f) * 90f + "° para olhar +Z\n");
                }
            }

            // malhas: LOD0 / LOD1 com o material do corpo; limites folgados (golpes largos)
            SkinnedMeshRenderer lod0 = null, lod1 = null;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.sharedMaterial = bodyMat;
                smr.updateWhenOffscreen = false;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.skinnedMotionVectors = false;
                var b = smr.localBounds; b.Expand(b.size.magnitude * 0.45f); smr.localBounds = b;
                if (smr.name.EndsWith("LOD1")) lod1 = smr; else lod0 = smr;
            }
            if (lod0 != null)
            {
                lod0.quality = SkinQuality.Bone4;
                // a importação já cria o LODGroup quando as malhas terminam em _LOD0/_LOD1
                var lg = model.GetComponent<LODGroup>(); if (lg == null) lg = model.AddComponent<LODGroup>();
                var lods = new List<LOD> { new LOD(0.2f, new Renderer[] { lod0 }) };
                if (lod1 != null) { lod1.quality = SkinQuality.Bone2; lods.Add(new LOD(0.012f, new Renderer[] { lod1 })); }
                lg.SetLODs(lods.ToArray());
                lg.RecalculateBounds();
            }
            log.Append("LOD0=" + (lod0 != null ? lod0.sharedMesh.vertexCount : 0) + " LOD1=" + (lod1 != null ? lod1.sharedMesh.vertexCount : 0) + " vértices\n");

            // lâmina no punho direito: eixo da lâmina = weapon_r -> weapon_tip_r (medido no Blender),
            // largura (fio) no sentido dos nós dos dedos; o punho fica no centro da mão
            var bladeGo = (GameObject)PrefabUtility.InstantiatePrefab(bladeFbx);
            PrefabUtility.UnpackPrefabInstance(bladeGo, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            bladeGo.name = "SussurranteBlade";
            foreach (var r in bladeGo.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = bladeMat; r.name = "SussurranteBlade" + (r.gameObject == bladeGo ? "" : "_mesh");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Transform wr = all.TryGetValue("weapon_r", out var w) ? w : all["hand_r"];
            Transform wt = all.TryGetValue("weapon_tip_r", out var w2) ? w2 : null;
            Vector3 bladeDir = wt != null ? (wt.position - wr.position).normalized : model.transform.forward;
            Vector3 knuckles = (all["middle_01_r"].position - all["hand_r"].position).normalized;
            Vector3 widthDir = Vector3.ProjectOnPlane(knuckles, bladeDir).normalized;
            // eixos do modelo da lâmina (medidos na malha: o maior é o comprimento, ponta = lado mais longe da origem)
            var mf = bladeGo.GetComponentInChildren<MeshFilter>();
            Vector3 lenAxis = Vector3.up, wAxis = Vector3.right;
            if (mf != null)
            {
                var mesh = mf.sharedMesh; var bb = mesh.bounds;
                Vector3 e = bb.extents; var vs = mesh.vertices;
                int li = e.x >= e.y && e.x >= e.z ? 0 : (e.y >= e.z ? 1 : 2);
                float far = 0f; Vector3 tipV = Vector3.zero;
                foreach (var v in vs) if (v[li] * v[li] > far) { far = v[li] * v[li]; tipV = v; }
                lenAxis = Vector3.zero; lenAxis[li] = Mathf.Sign(tipV[li]);
                // largura = o maior dos outros dois eixos
                int wi = li == 0 ? (e.y >= e.z ? 1 : 2) : (li == 1 ? (e.x >= e.z ? 0 : 2) : (e.x >= e.y ? 0 : 1));
                wAxis = Vector3.zero; wAxis[wi] = 1f;
                lenAxis = mf.transform.localRotation * lenAxis; wAxis = mf.transform.localRotation * wAxis;
            }
            bladeGo.transform.SetParent(wr, false);
            Quaternion fromModel = Quaternion.LookRotation(lenAxis, wAxis);
            Quaternion toWorld = Quaternion.LookRotation(bladeDir, widthDir);
            bladeGo.transform.rotation = toWorld * Quaternion.Inverse(fromModel);
            bladeGo.transform.position = wr.position + bladeDir * 0.15f;    // centro do cabo (0,30 m) na mão
            bladeGo.transform.localScale = Vector3.one;
            var bBase = new GameObject("Blade_Base").transform; bBase.SetParent(bladeGo.transform, false);
            var bTip = new GameObject("Blade_Tip").transform; bTip.SetParent(bladeGo.transform, false);
            bBase.position = bladeGo.transform.position + bladeDir * 0.12f;
            bTip.position = bladeGo.transform.position + bladeDir * 1.17f;
            log.Append("lâmina: dir=" + bladeDir.ToString("F2") + " largura=" + widthDir.ToString("F2") + "\n");

            // pontos de efeito: topo do crânio (fiapos)
            if (all.TryGetValue("Head", out var head))
            {
                var top = new GameObject("FX_HeadTop").transform; top.SetParent(head, false);
                top.position = new Vector3(head.position.x, model.transform.position.y + Height - 0.02f, head.position.z + 0.02f);
            }

            // raiz: colisor, navegação, IA
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.4f, 0); col.height = 2.8f; col.radius = 0.45f;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f; agent.height = 2.8f; agent.speed = 4.4f; agent.acceleration = 14f; agent.angularSpeed = 0f;
            agent.stoppingDistance = 0.2f; agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            root.AddComponent<SussurranteFX>();
            var e2 = root.AddComponent<EnemySussurrante>();
            e2.displayName = "Sussurrante";
            e2.maxHealth = 75f; e2.stability = 42f; e2.staggerRecover = 12f;
            e2.bodyRadius = 0.5f; e2.aimHeight = 1.75f; e2.headHeight = 2.85f;
            e2.walkSpeed = 1.9f; e2.chaseSpeed = 4.4f; e2.turnSpeed = 420f; e2.circleRadius = 5.5f; e2.aggroRange = 24f;
            e2.attackRange = 2.7f; e2.telegraphTime = 0.56f; e2.attackActive = 0.2f; e2.attackRecover = 0.55f;
            e2.attackDamage = 12f; e2.attackKnockback = 4.5f; e2.attackCooldown = 1.5f; e2.attackLungeSpeed = 4f; e2.attackArc = 80f;
            e2.knockbackScale = 0.65f;
            e2.spawnTime = 1.7f; e2.hurtTime = 0.38f; e2.stunTime = 1.8f; e2.knockdownTime = 2.4f; e2.deathTime = 2.7f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            return log.Append("prefab ok: " + PrefabPath + "\n").ToString();
        }

        // ------------------------------------------------------------ prévia (sem Play mode)

        /// <summary>Folha de poses + retrato com efeitos em Tools/cli/shots/suss_*.png.</summary>
        public static string Preview()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return "ERRO: prefab\n";
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(new Vector3(900f, 0f, 900f), Quaternion.identity);
            var an = go.GetComponentInChildren<Animator>();
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.Rebind(); an.Update(0f);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            var lg = go.GetComponentInChildren<LODGroup>(); if (lg != null) lg.ForceLOD(0);
            var fx = go.GetComponent<SussurranteFX>(); fx.Init();
            string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots");
            System.IO.Directory.CreateDirectory(dir);

            var camGo = new GameObject("susscam"); var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.055f, 0.08f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f; cam.allowHDR = true;
            var moon = new GameObject("susslua").AddComponent<Light>(); moon.type = LightType.Directional; moon.intensity = 0.9f; moon.color = new Color(0.75f, 0.82f, 1f);
            moon.transform.rotation = Quaternion.Euler(38f, 150f, 0f); moon.shadows = LightShadows.Soft;
            var rim = new GameObject("sussrim").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = 0.8f; rim.color = new Color(0.55f, 0.35f, 1f);
            rim.transform.rotation = Quaternion.Euler(20f, -30f, 0f);
            var amb = RenderSettings.ambientLight; RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.18f);

            int W = 360, H = 540;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            var poses = new[] { ("Locomotion", 0.4f), ("SlashA", 0.27f), ("SlashC", 0.62f), ("Heavy", 0.42f), ("Dash", 0.35f), ("Scream", 0.5f), ("Scream", 1.5f), ("Rise", 0.3f), ("Death", 0.6f), ("Death", 1.6f) };
            var sheet = new Texture2D(W * poses.Length, H * 2, TextureFormat.RGB24, false);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            for (int k = 0; k < poses.Length; k++)
            {
                var (st, time) = poses[k];
                an.SetFloat("StateSpeed", 1f); an.SetFloat("LocoSpeed", 1f); an.SetFloat("MoveSpeed", 0f);
                an.Play(st, 0, 0f); an.Update(0f); an.Update(time);
                for (int v = 0; v < 2; v++)
                {
                    var center = go.transform.position + Vector3.up * 1.45f;
                    cam.transform.position = center + (v == 0 ? new Vector3(1.6f, 0.4f, 7.4f) : new Vector3(7.4f, 0.4f, 0.8f));
                    cam.transform.LookAt(center);
                    cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                    sheet.SetPixels(k * W, (1 - v) * H, W, H, tex.GetPixels());
                }
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "suss_poses.png"), sheet.EncodeToPNG());

            // retrato com efeitos (lascas simuladas, olhos e fenda acesos)
            int PW = 900, PH = 1200;
            var prt = new RenderTexture(PW, PH, 24, RenderTextureFormat.ARGB32); cam.targetTexture = prt;
            an.Play("Locomotion", 0, 0f); an.Update(0f); an.Update(0.5f);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(2.5f, true, true);
            var c2 = go.transform.position + Vector3.up * 1.5f;
            cam.transform.position = c2 + new Vector3(1.9f, 0.35f, 6.2f); cam.transform.LookAt(c2);
            cam.Render(); RenderTexture.active = prt;
            var ptex = new Texture2D(PW, PH, TextureFormat.RGB24, false); ptex.ReadPixels(new Rect(0, 0, PW, PH), 0, 0); ptex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "suss_portrait.png"), ptex.EncodeToPNG());

            RenderTexture.active = null; cam.targetTexture = null; rt.Release(); prt.Release();
            RenderSettings.ambientLight = amb;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(moon.gameObject); Object.DestroyImmediate(rim.gameObject); Object.DestroyImmediate(go);
            return "prévia ok: " + dir + "/suss_poses.png, suss_portrait.png\n";
        }
    }
}
