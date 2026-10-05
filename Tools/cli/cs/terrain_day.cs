var views = @"
d_hill 10 12 -95 18 22 -135 60
d_ground 20 1.7 -70 26 0 -78 60
d_gorge 47 2 -20 40 -12 -5 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/ground2");
return Aren.EditorTools.NightPreview.Render(views, "day=1", dir, 800, 450);
