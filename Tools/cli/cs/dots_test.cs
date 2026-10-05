var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/hq2");
var a = Aren.EditorTools.NightPreview.Render("t_nobloom 0 1.8 -88 2 3 -60 60", "fenda=1;bloom=0", dir, 960, 540);
var gf = UnityEngine.Object.FindAnyObjectByType<Campanula.GrassField>();
gf.enabled = false;
var b = Aren.EditorTools.NightPreview.Render("t_nograss 0 1.8 -88 2 3 -60 60", "fenda=1", dir, 960, 540);
gf.enabled = true;
return a + b;
