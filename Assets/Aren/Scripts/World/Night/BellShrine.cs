using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Sino de beira de estrada ao lado de onde o Aren começa (Campanula, a Vila dos Doze Sinos,
    /// tem sinos até nas estradas). Montado em tempo de jogo com peças do próprio mundo: o sino é
    /// uma cópia de um dos sinos da torre, a madeira/ferro/vidro usam os materiais da vila, a
    /// lanterna tem halo e uma luz pequena. Badala (balanço amortecido), vibra sozinho com a
    /// onda do impacto (ressonância por simpatia) e a lanterna balança.
    /// </summary>
    public class BellShrine : MonoBehaviour
    {
        public static BellShrine Instance { get; private set; }
        public Transform bell, lantern;
        public Vector3 BellCenter => bell != null ? bell.position + Vector3.down * 0.45f : transform.position + Vector3.up * 2.2f;
        float swingVel, swingAngle, vibrate, lanternVel, lanternAngle;
        Light lanternLight;
        /// <summary>Zumbido por simpatia (o sino "cantando" junto com a flauta / desafinando): tremor fino, sem balanço.</summary>
        public float hum;
        public Vector3 LanternPos => lanternLight != null ? lanternLight.transform.position : BellCenter;
        Quaternion bellRest, lanternRest;

        static Material FindMat(string name)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name == name) return m;
            return null;
        }

        public static BellShrine Build(Vector3 groundPos, float yaw)
        {
            // com o pórtico gótico na cena, o chão é o dele (o raio de cima para baixo bateria no telhado)
            var pav = GameObject.Find("Bell_Pavilion");
            if (pav != null) groundPos = pav.transform.position;
            var root = new GameObject("Sino da estrada");
            root.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0, yaw, 0));
            var s = root.AddComponent<BellShrine>();
            Instance = s;
            var wood = FindMat("CMP_timber") ?? FindMat("CMP_planks");
            var iron = FindMat("CMP_iron") ?? FindMat("CMP_dark");
            var glass = FindMat("CMP_glass");
            var roof = FindMat("CMP_roof_slate") ?? wood;

            GameObject Box(string n, Vector3 lp, Vector3 size, Material m, Transform parent = null, float rotZ = 0f)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = n;
                g.transform.SetParent(parent != null ? parent : root.transform, false);
                g.transform.localPosition = lp; g.transform.localScale = size;
                g.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
                return g;
            }
            // Campânula gótica: o pórtico de pedra com os estandartes da clave vem da cena (kit_gothic.py,
            // posto pelo CampanulaBuilder neste mesmo lugar); sem ele, os postes de madeira de antes
            bool gothic = GameObject.Find("Bell_Pavilion") != null;
            if (!gothic) {
            // dois postes, travessa, mãos-francesas e um telhadinho de duas águas
            Box("Poste_E", new Vector3(-0.85f, 1.35f, 0), new Vector3(0.16f, 2.7f, 0.16f), wood);
            Box("Poste_D", new Vector3(0.85f, 1.35f, 0), new Vector3(0.16f, 2.7f, 0.16f), wood);
            Box("Travessa", new Vector3(0, 2.62f, 0), new Vector3(2.1f, 0.16f, 0.2f), wood);
            Box("Mao_E", new Vector3(-0.66f, 2.42f, 0), new Vector3(0.42f, 0.09f, 0.1f), wood, null, -42f);
            Box("Mao_D", new Vector3(0.66f, 2.42f, 0), new Vector3(0.42f, 0.09f, 0.1f), wood, null, 42f);
            Box("Telhado_E", new Vector3(-0.55f, 2.95f, 0), new Vector3(1.3f, 0.06f, 0.75f), roof, null, 28f);
            Box("Telhado_D", new Vector3(0.55f, 2.95f, 0), new Vector3(1.3f, 0.06f, 0.75f), roof, null, -28f);
            Box("Base", new Vector3(0, 0.06f, 0), new Vector3(2.0f, 0.12f, 0.5f), FindMat("CMP_stone_dark") ?? wood);
            }

            // o sino: pivô na travessa, cópia de um sino da torre
            var pivot = new GameObject("PivoSino").transform;
            pivot.SetParent(root.transform, false); pivot.localPosition = new Vector3(0, 2.55f, 0);
            // (os sinos da torre estão num static batch — copiar não desenha nada; o sino é torneado aqui)
            var bellGo = new GameObject("Sino");
            bellGo.transform.SetParent(pivot, false);
            bellGo.transform.localPosition = new Vector3(0, -0.06f, 0);
            bellGo.AddComponent<MeshFilter>().sharedMesh = LatheBell(0.82f, 0.37f);
            var bmr = bellGo.AddComponent<MeshRenderer>();
            var bsh = Resources.Load<Shader>("Shaders/Bronze");
            if (bsh != null && bsh.isSupported)
            {
                var bm = new Material(bsh) { name = "Bronze do sino", hideFlags = HideFlags.DontSave };
                bm.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_night"));
                bmr.sharedMaterial = bm;
            }
            else bmr.sharedMaterial = FindMat("CMP_bronze") ?? iron;
            var clapper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clapper.name = "Badalo";
            Object.Destroy(clapper.GetComponent<Collider>());
            clapper.transform.SetParent(pivot, false);
            clapper.transform.localPosition = new Vector3(0, -0.8f, 0); clapper.transform.localScale = Vector3.one * 0.11f;
            clapper.GetComponent<Renderer>().sharedMaterial = iron;
            s.bell = pivot; s.bellRest = pivot.localRotation;

            // lanterna pendurada no poste direito, com vidro aceso, halo e luz pequena
            var lp = new GameObject("PivoLanterna").transform;
            lp.SetParent(root.transform, false); lp.localPosition = gothic ? new Vector3(1.0f, 2.2f, 0.36f) : new Vector3(1.05f, 2.2f, 0.18f);   // no gótico, por fora do pilar
            Box("Gancho", new Vector3(0, -0.08f, 0), new Vector3(0.025f, 0.16f, 0.025f), iron, lp);
            Box("Lanterna", new Vector3(0, -0.31f, 0), new Vector3(0.12f, 0.2f, 0.12f), glass ?? iron, lp);
            Box("Tampa", new Vector3(0, -0.19f, 0), new Vector3(0.19f, 0.04f, 0.19f), iron, lp);
            Box("Fundo", new Vector3(0, -0.43f, 0), new Vector3(0.17f, 0.03f, 0.17f), iron, lp);
            for (int k = 0; k < 4; k++)   // armação de ferro na frente do vidro
                Box("Haste", new Vector3((k % 2 == 0 ? -1 : 1) * 0.07f, -0.31f, (k < 2 ? -1 : 1) * 0.07f), new Vector3(0.018f, 0.22f, 0.018f), iron, lp);
            var halo = new GameObject("Halo");
            halo.transform.SetParent(lp, false); halo.transform.localPosition = new Vector3(0, -0.31f, 0); halo.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            halo.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var hr = halo.AddComponent<MeshRenderer>(); hr.sharedMaterial = NightSetup.GlowMat;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.AddComponent<Flicker>().baseColor = new Color(1f, 0.6f, 0.26f, 0.45f);
            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(lp, false); lightGo.transform.localPosition = new Vector3(0, -0.32f, 0);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.3f); l.range = 5.5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            s.lantern = lp; s.lanternRest = lp.localRotation; s.lanternLight = l;
            foreach (var c in root.GetComponentsInChildren<Collider>()) if (c.gameObject.name != "Poste_E" && c.gameObject.name != "Poste_D" && c.gameObject.name != "Base") Object.Destroy(c);
            return s;
        }

        // perfil de sino de igreja (altura relativa → raio relativo), coroa arredondada e lábio aberto
        static readonly float[] PV = { 0f, .05f, .12f, .30f, .55f, .80f, .93f, 1f };
        static readonly float[] PR = { 0f, 22f / 68f, 30f / 68f, 34f / 68f, 38f / 68f, 52f / 68f, 64f / 68f, 1f };

        static float ProfileR(float v)
        {
            if (v <= 0f) return 0f;
            for (int i = 1; i < PV.Length; i++)
                if (v <= PV[i])
                {
                    float u = (v - PV[i - 1]) / (PV[i] - PV[i - 1]);
                    if (i == 1) return PR[1] * Mathf.Sqrt(u);
                    return Mathf.Lerp(PR[i - 1], PR[i], u * u * (3 - 2 * u));
                }
            return 1f;
        }

        /// <summary>Sino torneado: superfície externa, interna (espessura) e o lábio ligando as duas.</summary>
        static Mesh LatheBell(float height, float radius)
        {
            const int seg = 28, rings = 22;
            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Surface(float scale, float yOff, bool inside)
            {
                int start = verts.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float v = r / (float)rings;
                    float rad = ProfileR(v) * radius * scale;
                    float dv = 0.01f;
                    float slope = (ProfileR(Mathf.Min(1f, v + dv)) - ProfileR(Mathf.Max(0f, v - dv))) * radius / (2 * dv * height);
                    for (int k = 0; k <= seg; k++)
                    {
                        float a = k / (float)seg * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        verts.Add(dir * rad + Vector3.down * (v * height + yOff));
                        var n = (dir + Vector3.up * slope).normalized;
                        norms.Add(inside ? -n : n);
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int k = 0; k < seg; k++)
                    {
                        int i0 = start + r * (seg + 1) + k, i1 = i0 + 1, i2 = i0 + seg + 1, i3 = i2 + 1;
                        if (!inside) { tris.Add(i0); tris.Add(i2); tris.Add(i1); tris.Add(i1); tris.Add(i2); tris.Add(i3); }
                        else { tris.Add(i0); tris.Add(i1); tris.Add(i2); tris.Add(i1); tris.Add(i3); tris.Add(i2); }
                    }
            }
            Surface(1f, 0f, false);
            Surface(0.9f, 0.05f, true);
            // lábio: anel entre a borda externa e a interna
            int lo = verts.Count;
            for (int k = 0; k <= seg; k++)
            {
                float a = k / (float)seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts.Add(dir * radius + Vector3.down * height); norms.Add(Vector3.down);
                verts.Add(dir * radius * 0.9f + Vector3.down * (height + 0.05f)); norms.Add(Vector3.down);
            }
            for (int k = 0; k < seg; k++)
            {
                int a0 = lo + k * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tris.Add(a0); tris.Add(b0); tris.Add(a1); tris.Add(a1); tris.Add(b0); tris.Add(b1);
            }
            var m = new Mesh { name = "SinoTorneado" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Badalada: impulso no balanço do sino (a lanterna sente um pouco).</summary>
        public void Strike(float strength = 1f) { swingVel += 1.6f * strength; lanternVel += 0.4f * strength; }

        /// <summary>Vibração por simpatia (a onda do impacto chegando): treme sem balançar.</summary>
        public void Resonate(float amount = 1f) { vibrate = Mathf.Max(vibrate, amount); lanternVel += 0.9f * amount; }

        void Update()
        {
            float dt = Time.deltaTime;
            // pêndulo amortecido (graus)
            swingVel += (-swingAngle * 9f - swingVel * 0.9f) * dt;
            swingAngle += swingVel * dt * 20f;
            lanternVel += (-lanternAngle * 7f - lanternVel * 0.7f) * dt;
            lanternAngle += lanternVel * dt * 20f;
            vibrate = Mathf.MoveTowards(vibrate, 0f, dt * 0.25f);
            float jitter = vibrate * (Mathf.PerlinNoise(Time.time * 38f, 0.3f) - 0.5f) * 3.2f
                         + hum * Mathf.Sin(Time.time * 6.2832f * 11f) * 0.25f;
            if (lanternLight != null)
            {
                // a chama da lanterna: tremula; com o Contracanto, "bate" como duas notas desafinadas
                float corr = OpeningSound.Instance != null ? OpeningSound.Instance.CurrentCorruption : 0f;
                float tt = Time.time;
                float fl = 0.9f + 0.1f * Mathf.PerlinNoise(tt * 8f, 1.7f);
                float beat = 0.5f + 0.5f * Mathf.Sin(tt * 6.2832f * 1.37f) * Mathf.Sin(tt * 6.2832f * 0.29f);
                lanternLight.intensity = 1.6f * fl * (1f - corr * 0.4f * beat);
            }
            if (bell != null) bell.localRotation = bellRest * Quaternion.Euler(swingAngle + jitter, 0, jitter * 0.5f);
            if (lantern != null) lantern.localRotation = lanternRest * Quaternion.Euler(lanternAngle, 0, lanternAngle * 0.4f);
        }
    }
}
