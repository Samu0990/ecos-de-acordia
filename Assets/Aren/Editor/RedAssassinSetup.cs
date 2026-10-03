using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>
    /// Novo modelo do jogador ("red assassin", Tripo, riggado com 41 ossos) gerado por
    /// ArtSource/RedAssassin/scripts/ra_export.py. Mapeamento Humanoid explícito (o
    /// automático da Unity confunde os nomes do Tripo), T-pose de referência (o modelo vem
    /// em A-pose), materiais Standard com normal/metal e troca do modelo no Player.prefab.
    /// Rodar: RedAssassinSetup.All() (ou o menu Aren/Setup/Jogador - Red Assassin).
    /// </summary>
    public static class RedAssassinSetup
    {
        public const string Dir = "Assets/Aren/Character/RedAssassin/";
        public const string ModelPath = Dir + "RedAssassin.fbx";
        const string PlayerPrefab = "Assets/Dynamic Parkour System/Prefabs/Player.prefab";

        static readonly (string human, string bone)[] Map =
        {
            ("Hips", "Hip"), ("Spine", "Waist"), ("Chest", "Spine01"), ("UpperChest", "Spine02"),
            ("Neck", "NeckTwist01"), ("Head", "Head"),
            ("LeftShoulder", "L_Clavicle"), ("LeftUpperArm", "L_Upperarm"), ("LeftLowerArm", "L_Forearm"), ("LeftHand", "L_Hand"),
            ("RightShoulder", "R_Clavicle"), ("RightUpperArm", "R_Upperarm"), ("RightLowerArm", "R_Forearm"), ("RightHand", "R_Hand"),
            ("LeftUpperLeg", "L_Thigh"), ("LeftLowerLeg", "L_Calf"), ("LeftFoot", "L_Foot"), ("LeftToes", "L_ToeBase"),
            ("RightUpperLeg", "R_Thigh"), ("RightLowerLeg", "R_Calf"), ("RightFoot", "R_Foot"), ("RightToes", "R_ToeBase"),
        };

        [MenuItem("Aren/Setup/Jogador - Red Assassin (tudo)")]
        public static string All()
        {
            var log = new System.Text.StringBuilder();
            log.Append(Textures());
            log.Append(Avatar());
            log.Append(Materials());
            log.Append(SwapPlayer());
            return log.ToString();
        }

        public static string Textures()
        {
            void T(string file, bool normal, bool linear, int size)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(Dir + "Textures/" + file);
                if (ti == null) return;
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal && !linear;
                ti.maxTextureSize = size;
                ti.mipmapEnabled = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.SaveAndReimport();
            }
            // 2048 na cor (o jogador fica perto da câmera o tempo todo), 1024 no resto
            T("RedAssassin_BaseColor.png", false, false, 2048);
            T("RedAssassin_Normal.png", true, true, 1024);
            T("RedAssassin_MetallicGloss.png", false, true, 1024);
            return "texturas ok\n";
        }

        public static string Avatar()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
            mi.optimizeGameObjects = false;   // FluteSocket/FluteHolster e a flauta acessíveis
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();

            // esqueleto de referência e mapeamento explícito
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = Object.Instantiate(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var all = new Dictionary<string, Transform>();
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (!all.ContainsKey(t.name)) all[t.name] = t;
                var missing = Map.Where(m => !all.ContainsKey(m.bone)).Select(m => m.bone).ToList();
                if (missing.Count > 0) return "ERRO: ossos não encontrados: " + string.Join(",", missing) + "\n";
                Transform B(string human) { var m = Map.FirstOrDefault(x => x.human == human); return m.bone != null && all.TryGetValue(m.bone, out var t) ? t : null; }

                // T-pose: braços na horizontal para fora, pernas retas, pés como estão
                var feet = new Dictionary<Transform, Quaternion>();
                foreach (var f in new[] { "LeftFoot", "RightFoot", "LeftToes", "RightToes" }) if (B(f)) feet[B(f)] = B(f).rotation;
                foreach (var side in new[] { "Left", "Right" })
                {
                    Vector3 outDir = side == "Left" ? Vector3.left : Vector3.right;
                    Aim(B(side + "UpperArm"), B(side + "LowerArm"), outDir);
                    Aim(B(side + "LowerArm"), B(side + "Hand"), outDir);
                    AimLeaf(B(side + "Hand"), outDir);
                    Aim(B(side + "UpperLeg"), B(side + "LowerLeg"), Vector3.down);
                    Aim(B(side + "LowerLeg"), B(side + "Foot"), Vector3.down);
                }
                foreach (var kv in feet) kv.Key.rotation = kv.Value;

                var human = Map.Select(m => new HumanBone { humanName = m.human, boneName = m.bone, limit = new HumanLimit { useDefaultValues = true } }).ToArray();
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
            return "avatar valid=" + (avatar != null && avatar.isValid) + " human=" + (avatar != null && avatar.isHuman) + "\n";
        }

        static void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (bone == null || child == null) return;
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(cur, dir) * bone.rotation;
        }

        static void AimLeaf(Transform bone, Vector3 dir)
        {
            if (bone == null) return;
            for (int i = 0; i < bone.childCount; i++)
            {
                var c = bone.GetChild(i);
                if (c.name.StartsWith("Flute")) continue;   // o soquete da flauta não é "dedo"
                Aim(bone, c, dir);
                return;
            }
        }

        public static string Materials()
        {
            string p = Dir + "RedAssassin_Body.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
            m.shader = Shader.Find("Standard");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/RedAssassin_BaseColor.png"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/RedAssassin_Normal.png"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/RedAssassin_MetallicGloss.png"));
            m.EnableKeyword("_METALLICGLOSSMAP");
            m.SetFloat("_GlossMapScale", 0.85f);   // couro/metal do Tripo um pouco brilhante demais no pôr do sol
            m.SetColor("_Color", Color.white);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();

            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "RedAssassin_MAT"), m);
            foreach (var f in new[] { "Flute_Wood", "Flute_Gold", "Flute_Hole", "Flute_Wrap" })
            {
                var fm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Aren/Character/Materials/" + f + ".mat");
                if (fm != null) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), f), fm);
            }
            mi.SaveAndReimport();
            return "materiais ok\n";
        }

        /// <summary>Troca o modelo dentro do PlayerModel (o Animator e os scripts do DPS ficam onde estão).</summary>
        public static string SwapPlayer()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var model = root.transform.Find("PlayerModel");
                var anim = model.GetComponent<Animator>();
                foreach (var oldName in new[] { "Aren", "RedAssassin", "Erika" })
                {
                    var old = model.Find(oldName);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, model);
                inst.name = "RedAssassin";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                var innerAnim = inst.GetComponent<Animator>();
                if (innerAnim != null) Object.DestroyImmediate(innerAnim);
                anim.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<UnityEngine.Avatar>().First();

                var bones = new Dictionary<string, Transform>();
                foreach (var t in inst.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
                var climb = model.GetComponent<Climbing.ClimbController>();
                if (climb != null)
                {
                    var so = new SerializedObject(climb);
                    so.FindProperty("LHand").objectReferenceValue = bones["L_Hand"].gameObject;
                    so.FindProperty("RHand").objectReferenceValue = bones["R_Hand"].gameObject;
                    so.FindProperty("LFoot").objectReferenceValue = bones["L_Foot"].gameObject;
                    so.FindProperty("RFoot").objectReferenceValue = bones["R_Foot"].gameObject;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = false;
                    // o jogador nunca é escondido pelo occlusion culling (dados assados grossos
                    // podiam sumir com ele perto de paredes)
                    smr.allowOcclusionWhenDynamic = false;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true)) mr.allowOcclusionWhenDynamic = false;

                var missing = new List<string>();
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    var so = new SerializedObject(c);
                    var it = so.GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                            missing.Add(c.GetType().Name + "." + it.propertyPath);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                return "Player usa o RedAssassin (avatar " + anim.avatar.name + "). Referências perdidas: " + (missing.Count == 0 ? "nenhuma" : string.Join(", ", missing)) + "\n";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
