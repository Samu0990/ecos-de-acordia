var tpc = UnityEngine.Object.FindAnyObjectByType<Climbing.ThirdPersonController>();
var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Dynamic Parkour System/Model/Animator Controller.controller");
var names = new System.Collections.Generic.List<string>();
System.Action<UnityEditor.Animations.AnimatorStateMachine> walk = null;
walk = sm => { foreach (var s in sm.states) names.Add(s.state.name); foreach (var c in sm.stateMachines) walk(c.stateMachine); };
foreach (var layer in ctrl.layers) walk(layer.stateMachine);
var rb = tpc.GetComponent<UnityEngine.Rigidbody>();
//SETUP
var probe = Aren.DebugTools.ArenTestProbe.Run(tpc.gameObject, "TIMELINE", DURATION, names);
var jump = tpc.GetComponent<Aren.ArenJump>();
var lean = tpc.GetComponent<Aren.ArenLean>(); var piv = tpc.GetComponent<Aren.ArenPivot>();
probe.extra = () => {
  string e = "yaw=" + tpc.transform.eulerAngles.y.ToString("F0");
  if (lean != null) e += " lean=" + lean.CurrentLean.ToString("F1");
  if (piv != null) e += " skid=" + (piv.IsSkidding ? 1 : 0);
  if (jump != null) e += " fj=" + (jump.IsFreeJumping ? 1 : 0) + " h=" + jump.MaxHeightThisJump.ToString("F2") + " land=" + jump.LastLandTier + "/" + jump.LastLandSpeed.ToString("F1");
  //EXTRA
  return e;
};
return "started " + names.Count;
