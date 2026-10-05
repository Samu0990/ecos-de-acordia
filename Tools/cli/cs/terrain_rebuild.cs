// reconstrói só o terreno na cena aberta (alturas, splat, detalhes, material) para iterar rápido
var B = typeof(Campanula.EditorTools.CampanulaBuilder);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
var rootGo = UnityEngine.GameObject.Find("Campanula");
B.GetField("root", flags).SetValue(null, rootGo.transform);
var old = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Terrain>();
if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
UnityEditor.AssetDatabase.DeleteAsset("Assets/Campanula/Materials/Terrain.mat");
var log = new System.Text.StringBuilder();
var t0 = System.DateTime.Now;
var t = (UnityEngine.Terrain)B.GetMethod("BuildTerrain", flags).Invoke(null, new object[] { log });
UnityEngine.Physics.SyncTransforms();
var oldW = UnityEngine.GameObject.Find("Riacho"); if (oldW) UnityEngine.Object.DestroyImmediate(oldW);
B.GetMethod("BuildWater", flags).Invoke(null, new object[] { log });
return log.ToString() + " " + (System.DateTime.Now - t0).TotalSeconds.ToString("F1") + " s";
