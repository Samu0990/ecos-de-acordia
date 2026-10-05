// reimporta os sons do ElevenLabs com as regras do ArenAudioImport
int n = 0;
foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Aren/Resources/Audio/Eleven", "Assets/Aren/Resources/Audio/Samples" })) {
  var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
  if (p.Contains("/Eleven/") || p.Contains("/el_")) { UnityEditor.AssetDatabase.ImportAsset(p, UnityEditor.ImportAssetOptions.ForceUpdate); n++; }
}
var ai = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Aren/Resources/Audio/Eleven/impact_far.wav");
return n + " reimportados; impact_far: " + ai.defaultSampleSettings.loadType + " " + ai.defaultSampleSettings.compressionFormat;
