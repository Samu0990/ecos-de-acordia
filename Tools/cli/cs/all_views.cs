var views = @"
v_estrada 0 1.8 -88 2 3 -60 60
v_arvores -60 2 -40 -90 5 -70 60
v_morro 20 3 -95 40 12 -140 60
v_praca -6 2.2 6 6 10 14 60
v_torre 4 2 -2 9.6 20 8.3 60
v_campo2 25 1.6 -80 40 0.8 -95 60
v_ponte 33 2.5 11 44 1 9.5 55
v_alto 0 45 -150 0 10 0 55
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/hq2");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
