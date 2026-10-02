var path = "Assets/Dynamic Parkour System/Prefabs/Player.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var j = root.GetComponentInChildren<Aren.ArenJump>(true); j.anticipationTime = 0.5f;
var ic = root.GetComponentInChildren<Climbing.InputCharacterController>(true); ic.contextualBufferWindow = 0.6f;
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
return "ok " + j.anticipationTime + " " + ic.contextualBufferWindow;
