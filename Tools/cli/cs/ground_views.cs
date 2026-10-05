// vistas do chão, grama, montanhas (avaliação de qualidade)
var views = @"
g_campo 0 1.7 -60 -20 0.5 -85 60
g_campo2 25 1.6 -80 40 0.8 -95 60
g_montanha -20 6 -40 -140 20 -160 55
g_horizonte 0 20 -20 0 15 -200 60
g_leste 60 2 10 110 3 40 60
g_encosta -60 3 30 -110 10 60 60
g_grama 10 1.2 -70 14 0.2 -74 55
g_canion 30 -6 -30 30 -10 0 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/ground");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
