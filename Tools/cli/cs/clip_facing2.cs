// mede, para cada clipe usado no combate, para onde o tronco aponta em relação ao "frente" do objeto
var sb = new System.Text.StringBuilder();
var ual = "Assets/Aren/ThirdParty/Quaternius_UAL2/UAL2_Standard.fbx";
var clips = new System.Collections.Generic.Dictionary<string, UnityEngine.AnimationClip>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(ual)) if (o is UnityEngine.AnimationClip c && !c.name.StartsWith("__")) clips[c.name] = c;
string[] names = { "Sword_Regular_A_Rec", "Sword_Regular_B_Rec", "Melee_Hook_Rec", "Idle_No_Loop", "Zombie_Walk_Fwd_Loop", "Zombie_Idle_Loop", "Zombie_Scratch", "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_Combo", "Sword_Regular_C", "Melee_Hook", "Shield_Dash", "Sword_Block", "Idle_Shield_Break", "Hit_Knockback", "LayToIdle", "OverhandThrow", "Shield_OneShot", "Idle_Shield_Loop", "Sword_Heavy_Combo", "NinjaJump_Start", "Idle_Rail_Call" };
var models = new (string label, string path)[] {
  
  ("Eco", "Assets/Aren/Enemies/Eco.prefab") };
UnityEditor.AnimationMode.StartAnimationMode();
try {
foreach (var m in models) {
  var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(m.path));
  go.transform.SetPositionAndRotation(UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
  var an = go.GetComponentInChildren<UnityEngine.Animator>();
  sb.Append("== " + m.label + " animator=" + an.name + " human=" + an.isHuman + " modelLocalRot=" + an.transform.localEulerAngles + "\n");
  foreach (var n in names) {
    if (!clips.TryGetValue(n, out var clip)) { sb.Append(n + " ?\n"); continue; }
    sb.Append(string.Format("{0,-22}", n));
    for (int i = 0; i <= 24; i++) {
      float t = clip.length * i / 24f;
      UnityEditor.AnimationMode.BeginSampling();
      UnityEditor.AnimationMode.SampleAnimationClip(an.gameObject, clip, t);
      UnityEditor.AnimationMode.EndSampling();
      var L = an.GetBoneTransform(UnityEngine.HumanBodyBones.LeftUpperArm).position;
      var R = an.GetBoneTransform(UnityEngine.HumanBodyBones.RightUpperArm).position;
      var f = UnityEngine.Vector3.Cross(R - L, UnityEngine.Vector3.up);
      var LL = an.GetBoneTransform(UnityEngine.HumanBodyBones.LeftUpperLeg).position; var RL = an.GetBoneTransform(UnityEngine.HumanBodyBones.RightUpperLeg).position;
      float pyaw = UnityEngine.Vector3.SignedAngle(go.transform.forward, UnityEngine.Vector3.Cross(RL - LL, UnityEngine.Vector3.up), UnityEngine.Vector3.up);
      float yaw = UnityEngine.Vector3.SignedAngle(go.transform.forward, f, UnityEngine.Vector3.up);
      var hips = an.GetBoneTransform(UnityEngine.HumanBodyBones.Hips).position - go.transform.position;
      sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, " {0,4:F0}/{1,-4:F0}", yaw, pyaw));
      if (i == 24) sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "  hipsEnd=({0:F2},{1:F2},{2:F2}) len={3:F2}", hips.x, hips.y, hips.z, clip.length));
    }
    sb.Append("\n");
  }
  UnityEngine.Object.DestroyImmediate(go);
}
} finally { UnityEditor.AnimationMode.StopAnimationMode(); }
return sb.ToString();
