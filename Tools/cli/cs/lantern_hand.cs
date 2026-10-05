var go = UnityEngine.Object.Instantiate(UnityEngine.Resources.Load<UnityEngine.GameObject>("Villagers/Villager_M1"), new UnityEngine.Vector3(900, 0, 900), UnityEngine.Quaternion.identity);
var an = go.GetComponent<UnityEngine.Animator>(); an.Rebind(); an.Update(0f);
var sb = new System.Text.StringBuilder();
foreach (var st in new[] { "Idle_Lantern", "Idle_FoldArms" }) {
  an.Play(st, 0, 0f); an.Update(0f); an.Update(0.7f);
  var l = an.GetBoneTransform(UnityEngine.HumanBodyBones.LeftHand).position - go.transform.position;
  var r = an.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand).position - go.transform.position;
  sb.AppendLine(st + " L=" + l.ToString("F2") + " R=" + r.ToString("F2"));
}
UnityEngine.Object.DestroyImmediate(go);
return sb.ToString();
