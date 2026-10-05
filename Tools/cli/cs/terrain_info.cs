var t = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Terrain>();
var m = t.materialTemplate;
var sh = m ? m.shader : null;
string s = "mat=" + (m? m.name : "null") + " shader=" + (sh? sh.name : "null") + " supported=" + (sh? sh.isSupported.ToString() : "-") + " passes=" + (sh? sh.passCount : 0);
if (sh) { foreach (var msg in UnityEditor.ShaderUtil.GetShaderMessages(sh)) s += "\n" + msg.severity + " " + msg.message + " L" + msg.line; }
s += "\ninstanced=" + t.drawInstanced + " basemap=" + t.basemapDistance + " pe=" + t.heightmapPixelError;
var gl = UnityEngine.GameObject.Find("Luz da cidade (chão)");
s += "\nground=" + (gl != null);
return s;
