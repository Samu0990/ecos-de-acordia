var views = @"
g_campo2 25 1.6 -80 40 0.8 -95 60
g_horizonte 0 20 -20 0 15 -200 60
h_close 10 12 -95 18 22 -135 60
g_oeste -40 4 -60 -120 12 -40 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/ground3");
var a = Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 800, 450);
var dviews = @"
d_hill 10 12 -95 18 22 -135 60
d_oeste -40 4 -60 -120 12 -40 60
";
var b = Aren.EditorTools.NightPreview.Render(dviews, "day=1", dir, 800, 450);
return a + b;
