// A/B da gradação noturna (Purkinje) nos planos da abertura: bardo de perfil, fim da grua, estabelecimento
var views = @"
g_bardo -2.05 1.34 -91.45 0.2 1.39 -92 32
g_grua -3.6 2.15 -98.2 0.7 1.55 -88 40
g_aberto -17 23 -128 4 8 -12 46
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night");
var r = Aren.EditorTools.NightPreview.Render(views.Replace("g_", "p0_"), "fenda=0;stage=1;purkinje=0", dir, 960, 540);
r += Aren.EditorTools.NightPreview.Render(views.Replace("g_", "p1_"), "fenda=0;stage=1;purkinje=0.62", dir, 960, 540);
return r;
