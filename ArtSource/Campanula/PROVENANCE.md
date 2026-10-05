# Campânula gótica (reforma do mapa, 2026-10-05)

Referência: painel "Referência de Campânula" e plano principal do storyboard do autor
(`ArtSource/Loading/storyboard_fenda_sete_brilhos.png`) — cidade de pedra "baseada em som e sinos, com
pontes, torres e vida noturna": empenas e agulhas góticas, pontes de arcos sobre cachoeiras, névoa,
estandartes com a clave de sol, janelas acesas.

- `scripts/kit_gothic.py` (Blender, procedural, sem assets de terceiros): sobrados góticos (`GHouse_*`,
  `GTavern`), torres (`GTower_*`), ponte-aqueduto do desfiladeiro (`Aqueduct`), Grande Aqueduto
  (`Great_Aqueduct`), muro de arrimo com bueiros (`Gorge_Wall`), penedos (`Cliff_Rock_*`), pórtico do
  sino da estrada (`Bell_Pavilion`), estandarte (`Banner_Clef`). A agulha da Torre dos Sinos foi trocada
  em `kit_buildings.py`.
- `Assets/Campanula/Textures/banner_albedo.png`: estandarte gerado com ImageMagick; a clave de sol é o
  glifo U+1D11E da fonte **Noto Music** (SIL Open Font License 1.1 — permite usar o desenho renderizado
  em imagens, inclusive comercialmente).
- Fotos de pedra/rocha: Poly Haven (CC0), ver `Assets/Campanula/Textures/PH/PROVENANCE.md`
  (`ashlar` = stone_brick_wall_001, `trim` = rock_wall_16, `rock` = rock_wall_10).

Passe de qualidade: `ao/*.png` são as oclusões de ambiente assadas no Cycles por modelo (montadas em
`Assets/Campanula/Textures/ao_atlas*.png`); os modelos `*_LOD1` são as versões simples para longe.
