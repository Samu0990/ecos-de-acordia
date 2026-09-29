using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>Importação do Aren.fbx (gerado por ArtSource/Aren/scripts/aren_05_flute_export.py).</summary>
    public class ArenCharacterPostprocessor : AssetPostprocessor
    {
        public const string Folder = "Assets/Aren/Character/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            // Só na primeira importação: depois a T-pose forçada fica gravada no humanDescription.
            if (!mi.importSettingsMissing) return;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.skinWeights = ModelImporterSkinWeights.Standard;   // 4 influências (igual ao export)
            mi.optimizeGameObjects = false;                       // sockets, flauta e ossos secundários acessíveis
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    public static class ArenCharacterSetup
    {
        public const string ModelPath = "Assets/Aren/Character/Aren.fbx";
        public const string PlayerPrefab = "Assets/Dynamic Parkour System/Prefabs/Player.prefab";

        /// <summary>
        /// O modelo está em A-pose (braços para baixo) com dedos curvados. O Humanoid precisa de
        /// T-pose como referência de "zero muscle"; sem isso toda animação retargetada sai com
        /// braços/dedos tortos. Equivale ao "Enforce T-Pose" do Avatar Configurator, via script.
        /// </summary>
        [MenuItem("Aren/Setup/Avatar - Forçar T-Pose")]
        public static string EnforceTPose()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            var hd = mi.humanDescription;
            if (hd.human == null || hd.human.Length == 0) return "humanDescription vazio (importe como Humanoid primeiro)";
            // O auto-mapeamento do Unity colocou Hood_01 como "RightEye". O Aren não tem ossos de
            // olho/mandíbula (usa máscara) — tira esses mapeamentos.
            hd.human = hd.human.Where(h => !h.humanName.Contains("Eye") && h.humanName != "Jaw").ToArray();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = Object.Instantiate(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var all = go.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
                var map = hd.human.ToDictionary(h => h.humanName, h => all.TryGetValue(h.boneName, out var t) ? t : null);
                Transform B(string human) => map.TryGetValue(human, out var t) ? t : null;

                // personagem olha para +Z; esquerda dele = -X
                var feetRot = new Dictionary<Transform, Quaternion>();
                foreach (var f in new[] { "LeftFoot", "RightFoot", "LeftToes", "RightToes" })
                    if (B(f)) feetRot[B(f)] = B(f).rotation;

                foreach (var side in new[] { "Left", "Right" })
                {
                    Vector3 outDir = side == "Left" ? Vector3.left : Vector3.right;
                    Aim(B(side + "UpperArm"), B(side + "LowerArm"), outDir);
                    Aim(B(side + "LowerArm"), B(side + "Hand"), outDir);
                    Aim(B(side + "Hand"), B(side + "Middle Proximal"), outDir);
                    foreach (var f in new[] { "Index", "Middle", "Ring", "Little" })
                    {
                        Aim(B($"{side} {f} Proximal"), B($"{side} {f} Intermediate"), outDir);
                        Aim(B($"{side} {f} Intermediate"), B($"{side} {f} Distal"), outDir);
                        AimLeaf(B($"{side} {f} Distal"), outDir);
                    }
                    // polegar: 45° para frente, como no T-pose padrão
                    Vector3 thumbDir = (outDir + Vector3.forward).normalized;
                    Aim(B($"{side} Thumb Proximal"), B($"{side} Thumb Intermediate"), thumbDir);
                    Aim(B($"{side} Thumb Intermediate"), B($"{side} Thumb Distal"), thumbDir);
                    AimLeaf(B($"{side} Thumb Distal"), thumbDir);
                    // pernas retas para baixo (modelo tem as pernas abertas ~9°)
                    Aim(B(side + "UpperLeg"), B(side + "LowerLeg"), Vector3.down);
                    Aim(B(side + "LowerLeg"), B(side + "Foot"), Vector3.down);
                }
                // pés voltam à orientação original (planos no chão)
                foreach (var kv in feetRot) kv.Key.rotation = kv.Value;

                var sk = hd.skeleton;
                for (int i = 0; i < sk.Length; i++)
                {
                    if (!all.TryGetValue(sk[i].name, out var t)) continue;
                    if (t == go.transform) continue;
                    sk[i].rotation = t.localRotation;
                    sk[i].position = t.localPosition;
                }
                hd.skeleton = sk;
                mi.humanDescription = hd;
                mi.SaveAndReimport();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            return Report();
        }

        static void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (bone == null || child == null) return;
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8) return;
            bone.rotation = Quaternion.FromToRotation(cur, dir) * bone.rotation;
        }

        static void AimLeaf(Transform bone, Vector3 dir)
        {
            if (bone == null || bone.childCount == 0) return;
            Aim(bone, bone.GetChild(0), dir);
        }

        public static string Report()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            var hd = mi.humanDescription;
            var mapped = new HashSet<string>(hd.human.Select(h => h.humanName));
            var missing = HumanTrait.BoneName.Where((n, i) => HumanTrait.RequiredBone(i) && !mapped.Contains(n)).ToList();
            var fingers = hd.human.Count(h => h.humanName.Contains("Proximal") || h.humanName.Contains("Intermediate") || h.humanName.Contains("Distal"));
            string lines = string.Join(", ", hd.human.Where(h => !h.humanName.Contains("Proximal") && !h.humanName.Contains("Intermediate") && !h.humanName.Contains("Distal")).Select(h => h.humanName + "=" + h.boneName));
            return $"avatar valid={avatar?.isValid} human={avatar?.isHuman} mapeados={hd.human.Length} dedos={fingers} faltando_obrigatorios=[{string.Join(",", missing)}]\n{lines}";
        }

        /// <summary>
        /// Materiais do Aren no projeto (Standard, pipeline Built-in) e remap no importador do FBX.
        /// Textura 4K do Tripo importada em 2048 (GPU integrada do notebook: menos VRAM/banda).
        /// </summary>
        [MenuItem("Aren/Setup/Materiais")]
        public static string SetupMaterials()
        {
            const string texPath = "Assets/Aren/Character/Textures/Aren_BaseColor.jpg";
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.maxTextureSize = 2048;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            Material Make(string name, Color color, float metallic, float smooth, Texture2D map = null)
            {
                string p = "Assets/Aren/Character/Materials/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
                m.SetColor("_Color", color);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Glossiness", smooth);
                m.SetTexture("_MainTex", map);
                EditorUtility.SetDirty(m);
                return m;
            }
            var mats = new Dictionary<string, Material>
            {
                { "Aren_MAT", Make("Aren_Body", Color.white, 0f, 0.28f, tex) },
                { "Flute_Wood", Make("Flute_Wood", new Color(0.52f, 0.33f, 0.2f), 0f, 0.45f) },
                { "Flute_Gold", Make("Flute_Gold", new Color(0.93f, 0.78f, 0.45f), 1f, 0.62f) },
                { "Flute_Hole", Make("Flute_Hole", new Color(0.02f, 0.01f, 0.01f), 0f, 0.1f) },
                { "Flute_Wrap", Make("Flute_Wrap", new Color(0.55f, 0.06f, 0.05f), 0f, 0.3f) },
            };
            AssetDatabase.SaveAssets();
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            foreach (var kv in mats)
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
            return "materiais: " + string.Join(", ", mats.Keys);
        }

        /// <summary>Troca a Erika pelo Aren no Player.prefab (Erika continua no projeto, só sai do prefab).</summary>
        [MenuItem("Aren/Setup/Player - Trocar Erika pelo Aren")]
        public static string SwapPlayerModel()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var model = root.transform.Find("PlayerModel");
                var anim = model.GetComponent<Animator>();
                var old = model.Find("Erika");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var existing = model.Find("Aren");
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, model);
                inst.name = "Aren";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                // o Animator fica no PlayerModel (scripts do DPS dependem disso)
                var innerAnim = inst.GetComponent<Animator>();
                if (innerAnim != null) Object.DestroyImmediate(innerAnim);

                anim.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().First();

                // ClimbController guardava referências para os ossos da Erika (destruídos acima)
                var bonesByName = inst.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
                var climb = model.GetComponent<Climbing.ClimbController>();
                if (climb != null)
                {
                    var so = new SerializedObject(climb);
                    so.FindProperty("LHand").objectReferenceValue = bonesByName["LeftHand"].gameObject;
                    so.FindProperty("RHand").objectReferenceValue = bonesByName["RightHand"].gameObject;
                    so.FindProperty("LFoot").objectReferenceValue = bonesByName["LeftFoot"].gameObject;
                    so.FindProperty("RFoot").objectReferenceValue = bonesByName["RightFoot"].gameObject;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
                    smr.updateWhenOffscreen = false;

                // procura qualquer outra referência que apontava para algo da Erika
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
                return "Player agora usa o Aren (avatar " + anim.avatar.name + "). Referências perdidas: " + (missing.Count == 0 ? "nenhuma" : string.Join(", ", missing));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
