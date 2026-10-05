var views = @"
h_close 10 12 -95 18 22 -135 60
h_top 15 120 -120 15 0 -121 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/ground");
var s = Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5", dir, 800, 450);
var t = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Terrain>();
var cl = UnityEngine.GameObject.Find("Luz da cidade (chão)");
return s + " terrain basemapDist=" + t.basemapDistance + " mat=" + (t.materialTemplate? t.materialTemplate.shader.name : "null") + " cl=" + (cl!=null);
