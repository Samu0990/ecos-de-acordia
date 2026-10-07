using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Identidade visual de um reino (ajustável no Inspector; o construtor gera um por reino em
    /// Assets/World/Data/Regions). A cena aplica isto ao carregar: sol, ambiente em 3 cores, neblina,
    /// céu, partículas do ar e a cor da Corrupção daquele reino.
    /// </summary>
    [CreateAssetMenu(menuName = "Elyndra/Perfil de Reino")]
    public class RegionProfile : ScriptableObject
    {
        public RegionId region;
        [Header("Luz")]
        public Color sunColor = new Color(1f, 0.8f, 0.6f);
        public float sunIntensity = 1f;
        public Vector2 sunAngles = new Vector2(25f, 60f);   // elevação, azimute
        public Color ambientSky = new Color(0.4f, 0.4f, 0.5f), ambientEquator = new Color(0.35f, 0.3f, 0.3f), ambientGround = new Color(0.12f, 0.1f, 0.1f);
        [Header("Atmosfera")]
        public Color fogColor = new Color(0.5f, 0.45f, 0.5f);
        public float fogDensity = 0.004f;
        public Color skyTop = new Color(0.1f, 0.12f, 0.2f), skyHorizon = new Color(0.6f, 0.45f, 0.4f);
        public float starAmount = 0.2f;
        [Range(0, 1)] public float overcast = 0.35f;     // nuvens pesadas (0 = céu limpo, 1 = coberto)
        [Range(0, 1)] public float sunDisk = 1f;          // disco do sol visível (0 = escondido atrás da névoa)
        public Color farRange = new Color(0.22f, 0.2f, 0.24f);   // cor das serras do horizonte (antes da névoa)
        [Range(0.002f, 0.05f)] public float sunSize = 0.005f;      // tamanho do disco (luas grandes em Sefra)
        public Color riftTint = new Color(0.55f, 0.25f, 0.75f);    // cor da Fenda no céu deste reino
        [ColorUsage(true, true)] public Color eclipse = new Color(0, 0, 0, 0);   // anel de eclipse no céu (alfa 0 = sem)
        public Vector3 eclipseDir = new Vector3(0.35f, 0.45f, 0.82f);
        [ColorUsage(false, true)] public Color windowColor = new Color(1.6f, 0.9f, 0.4f);   // janelas acesas
        [Header("Imagem (pós-processamento do reino)")]
        public float gradeContrast = 0.3f;
        public float gradeSaturation = 0.95f;
        public float gradeVignette = 0.34f;
        public float gradeExposure = 1.05f;
        [Range(0, 1)] public float purkinje;              // visão noturna nos escuros (reinos de noite)
        public Color shadowTint = new Color(0.92f, 0.95f, 1.06f), highTint = new Color(1.05f, 1f, 0.92f);
        public bool bloom = true;
        [Header("Ar (partículas)")]
        public Color motesColor = new Color(1f, 0.9f, 0.7f, 0.5f);
        public float motesRate = 40f;
        public bool motesFall;           // cinza / neve caem; pólen / brasas sobem
        [Header("Corrupção do reino")]
        public Color corruptionTint = new Color(0.45f, 0.3f, 0.55f);   // cor que a Ressonância distorcida puxa
        public Color corruptionFog = new Color(0.25f, 0.2f, 0.3f);
        [Header("Chão")]
        public Color groundTint = Color.white;
        public Color rockTint = Color.white;
        public Color grassTint = Color.white;
    }
}
