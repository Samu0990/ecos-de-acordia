// abertura com o mapa gótico: sino (S0), bardo (S2), grua (S1), estabelecimento, Fenda
var views = @"
v_s0 1.6 1.3 -93.6 2.6 1.9 -91.4 38
v_s2 -2.05 1.34 -91.45 0.2 1.39 -92 32
v_grua -3.6 2.15 -98.2 0.7 1.55 -88 40
v_estab -17 23 -128 4 8 -12 46
v_fenda -24 30 -144 30 150 900 50
v_ponte 20 2 2 44 -3 10 60
v_canion 44 -9 -30 43 -8 30 60
v_portao 0 1.8 -64 0 6 -40 55
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/map");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12;stage=1", dir, 960, 540);
