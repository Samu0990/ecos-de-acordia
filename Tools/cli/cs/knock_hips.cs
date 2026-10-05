// mede o quadril (relativo à raiz) em Aren Death e Aren Revive: há salto entre o fim de um e o começo do outro?
var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Dynamic Parkour System/Prefabs/Player.prefab"));
go.transform.SetPositionAndRotation(new UnityEngine.Vector3(800, 0, 800), UnityEngine.Quaternion.identity);
var an = go.GetComponentInChildren<UnityEngine.Animator>(); an.applyRootMotion = false; an.cullingMode = UnityEngine.AnimatorCullingMode.AlwaysAnimate;
an.Rebind(); an.Update(0f);
var hips = an.GetBoneTransform(UnityEngine.HumanBodyBones.Hips); var head = an.GetBoneTransform(UnityEngine.HumanBodyBones.Head);
var sb = new System.Text.StringBuilder();
foreach (var (st, ts) in new[] { ("Aren Death", new[] { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.83f, 1.2f }), ("Aren Revive", new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0f, 1.2f, 1.4f, 1.53f }) }) {
  foreach (var t in ts) {
    an.Play(st, 0, 0f); an.Update(0f); an.Update(t);
    var h = hips.position - go.transform.position; var hd = head.position - go.transform.position;
    sb.AppendLine($"{st} t={t:0.00} hips=({h.x:0.00},{h.y:0.00},{h.z:0.00}) head=({hd.x:0.00},{hd.y:0.00},{hd.z:0.00})");
  }
}
UnityEngine.Object.DestroyImmediate(go);
return sb.ToString();
