var views = @"
d1_mercado 1 1.8 -38 -1 3 -10 55
d2_praca -10 2.2 2 8 6 30 55
d3_muralha -6 1.7 -60 4 6 -42 55
d4_casa -3.5 1.7 -25 -6 3 -22 50
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night");
return Aren.EditorTools.NightPreview.Render(views, "day=1;cine=0", dir, 960, 540);
