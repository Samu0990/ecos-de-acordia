var views = @"
top 5 90 -130 5 0 -129.9 40
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/ground3");
var r = Aren.EditorTools.NightPreview.Render(views, "day=1;bloom=0;cine=0", dir, 800, 800);
var t = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Terrain>();
var td = t.terrainData;
var sb = new System.Text.StringBuilder();
for (float z = -112; z >= -158; z -= 4) {
  float x = 0f;
  for (float xx = -6; xx <= 6; xx += 3) {
    int ax = (int)((xx + 160f) / 320f * td.alphamapWidth), az = (int)((z + 160f) / 320f * td.alphamapHeight);
    var a = td.GetAlphamaps(ax, az, 1, 1);
    sb.Append(string.Format("({0},{1}) g{2:F2} d{3:F2} c{4:F2} f{5:F2}  ", xx, z, a[0,0,0], a[0,0,1], a[0,0,2], a[0,0,3]));
  }
  sb.Append("\n");
}
return r + "\n" + sb;
