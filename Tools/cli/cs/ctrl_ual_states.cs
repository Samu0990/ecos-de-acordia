var sb = new System.Text.StringBuilder();
var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Dynamic Parkour System/Model/Animator Controller.controller");
void Walk(UnityEditor.Animations.AnimatorStateMachine sm, string pre) {
  foreach (var c in sm.states) {
    var m = c.state.motion;
    string mn = m == null ? "-" : m.name;
    string p = m == null ? "" : UnityEditor.AssetDatabase.GetAssetPath(m);
    if (m is UnityEditor.Animations.BlendTree bt) { mn = "BT["; foreach (var ch in bt.children) mn += (ch.motion ? ch.motion.name + "@" + System.IO.Path.GetFileName(UnityEditor.AssetDatabase.GetAssetPath(ch.motion)) : "-") + ","; mn += "]"; }
    sb.Append(pre + c.state.name + " -> " + mn + " (" + System.IO.Path.GetFileName(p) + ")\n");
  }
  foreach (var s in sm.stateMachines) Walk(s.stateMachine, pre + s.stateMachine.name + "/");
}
Walk(ctrl.layers[0].stateMachine, "");
return sb.ToString();
