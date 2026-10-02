// avalia o Animator de verdade (Animator.Update manual fora do Play) e mede a frente do tronco
var sb = new System.Text.StringBuilder();
var models = new (string label, string path, string[] states)[] {
  ("Eco", "Assets/Aren/Enemies/Eco.prefab", new[]{"Locomotion","Attack","Hurt","Knock","GetUp"}),
  ("Deer", "Assets/Aren/Enemies/Deer.prefab", new[]{"Locomotion","Attack","Slam","ChargeWind","Charge","Roar","Stagger"}),
  ("Aren", "Assets/Dynamic Parkour System/Prefabs/Player.prefab", new[]{"Base Layer.Idle","Base Layer.Walk","Aren Atk1","Aren Atk2","Aren Atk3","Aren Atk4","Aren Counter","Aren Dodge","Aren Jump","Aren Land Heavy","Aren Cast Blade","Aren Release","Aren Hurt"}) };
foreach (var m in models) {
  var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(m.path));
  go.transform.SetPositionAndRotation(new UnityEngine.Vector3(500, 0, 500), UnityEngine.Quaternion.identity);
  var an = go.GetComponentInChildren<UnityEngine.Animator>();
  an.applyRootMotion = false; an.cullingMode = UnityEngine.AnimatorCullingMode.AlwaysAnimate;
  an.Rebind(); an.Update(0f);
  sb.Append("== " + m.label + " anim on " + an.name + " root=" + go.name + "\n");
  foreach (var s in m.states) {
    an.Play(s, 0, 0f); an.Update(0f);
    sb.Append(string.Format("{0,-16}", s));
    for (int i = 0; i < 16; i++) {
      var L = an.GetBoneTransform(UnityEngine.HumanBodyBones.LeftUpperArm).position;
      var R = an.GetBoneTransform(UnityEngine.HumanBodyBones.RightUpperArm).position;
      var LL = an.GetBoneTransform(UnityEngine.HumanBodyBones.LeftUpperLeg).position;
      var RL = an.GetBoneTransform(UnityEngine.HumanBodyBones.RightUpperLeg).position;
      float y1 = UnityEngine.Vector3.SignedAngle(go.transform.forward, UnityEngine.Vector3.Cross(R - L, UnityEngine.Vector3.up), UnityEngine.Vector3.up);
      float y2 = UnityEngine.Vector3.SignedAngle(go.transform.forward, UnityEngine.Vector3.Cross(RL - LL, UnityEngine.Vector3.up), UnityEngine.Vector3.up);
      var head = an.GetBoneTransform(UnityEngine.HumanBodyBones.Head).position - an.GetBoneTransform(UnityEngine.HumanBodyBones.Hips).position;
      sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, " {0,4:F0}/{1,-4:F0}", y1, y2));
      an.Update(0.08f);
    }
    var bf = an.bodyRotation * UnityEngine.Vector3.forward;
    sb.Append(" bodyRot=" + UnityEngine.Vector3.SignedAngle(go.transform.forward, bf, UnityEngine.Vector3.up).ToString("F0") + "\n");
  }
  UnityEngine.Object.DestroyImmediate(go);
}
return sb.ToString();
