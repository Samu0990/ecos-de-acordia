var tpc = UnityEngine.Object.FindAnyObjectByType<Climbing.ThirdPersonController>();
var p = tpc.GetComponent<Aren.DebugTools.ArenTestProbe>();
if (p == null) return "no probe";
return (p.Done ? "DONE\n" : "RUNNING\n") + p.log.ToString();
