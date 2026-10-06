# Sussurrante — pipeline do modelo, texturas, rig e lâmina

Inimigo "Sussurrante · Eco Primordial · Rasgos" da prancha do autor (`source/concept_sheet.webp`).
Tudo é regenerável por script, nesta ordem (Blender 5.2 headless, ~6 min no notebook):

```bash
cd ~/Unity/ParkourLab
B="blender -b --factory-startup --python"
$B ArtSource/Sussurrante/scripts/suss_01_prep.py      # orienta (+Y), escala 2,85 m, solda costuras, reduz (400 k / 26 k)
$B ArtSource/Sussurrante/scripts/suss_02_rig.py       # esqueleto UAL2/Quaternius encaixado + bone heat
$B ArtSource/Sussurrante/scripts/suss_03_weights.py   # pesos finais por região (braço/perna/saia/cachecol/crânio)
$B ArtSource/Sussurrante/scripts/suss_04_bake.py      # normal (malha densa → leve) e AO no atlas do Tripo
$B ArtSource/Sussurrante/scripts/suss_05_textures.py  # gradação para a paleta da prancha + máscaras (--rebake refaz as máscaras 3D)
$B ArtSource/Sussurrante/scripts/suss_06_blade.py     # lâmina dentada procedural → SussurranteBlade.fbx
$B ArtSource/Sussurrante/scripts/suss_07_export.py    # marcadores (arma, olhos, boca, peito), LOD1, Sussurrante.fbx
~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Sussurrante/scripts/suss_audio.py   # sussurros + canto da lâmina
```

Depois, no editor: `Aren.EditorTools.SussurranteSetup.All()` (menu *Aren/Setup/Inimigos - Sussurrante*)
e, para conferir sem Play mode, `SussurranteSetup.Preview()` → `Tools/cli/shots/suss_poses.png` e `suss_portrait.png`.

## Decisões

- **Malha**: Tripo H3.1 (imagem → 3D) a partir de uma pose A gerada da prancha (FLUX.1 Kontext dev),
  1,48 M faces → 26 k (LOD0) e 8,5 k (LOD1). O glTF duplica vértices nas costuras de UV: a etapa 1
  SOLDA antes de reduzir (sem isso o decimate abria frestas e o bone heat/regiões paravam em cada ilha).
- **UV**: fica o atlas do Tripo (415 ilhas) — o "Smart UV" do Blender criou centenas de ilhas minúsculas.
- **Esqueleto**: o mesmo da UAL2 (nomes Quaternius), com dedos; articulações medidas por fatias da malha
  (`suss_common.py`). As animações CC0 da UAL2 servem direto pelo Humanoid.
- **Pesos**: bone heat só como 1ª passada. Braços/pernas = inundação pela malha a partir das pontas dos
  dedos/solas dentro de cápsulas; saia = pelve no cós misturando com a coxa do lado (tiras do meio só pelve);
  cachecol/capuz = spine_03 → pescoço → cabeça; crânio rígido. Teste: poses extremas sem "espinhos".
- **Textura**: cor do Tripo regradada para a paleta da prancha (pele bege-acinzentada com manchas de ardósia
  e rachaduras orgânicas, cachecol índigo, saia azul-carvão, corda arroxeada); classificação pele/tecido
  medida por histograma (matiz × luminância) por região. Máscara: R AO, G suavidade, B fendas acesas
  (peito + relâmpagos do antebraço esquerdo), A veias (acendem no aviso/grito).
- **Lâmina**: 1,15 m + cabo 0,30 m, obsidiana com fio serrilhado; cores de vértice controlam o shader.

Proveniência e licenças: `PROVENANCE.md`.
