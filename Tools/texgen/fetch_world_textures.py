"""Texturas de chão dos reinos de Elyndra (Poly Haven, CC0) — 1K JPG (cor + normal OpenGL).
Saída: Assets/World/Textures/PH/<nome>_albedo.jpg / <nome>_normal.jpg e PROVENANCE.md.
Rodar: python3 Tools/texgen/fetch_world_textures.py
"""
import json, os, urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'World', 'Textures', 'PH')
# nome no projeto → slug do Poly Haven
TEX = {
    'snow': 'snow_02', 'sand': 'coast_sand_01', 'ash': 'burned_ground_01', 'darkrock': 'rock_face',
    'dryground': 'dry_ground_01', 'forestfloor': 'forest_leaves_02', 'mudleaves': 'brown_mud_leaves_01',
    'cracked': 'mud_cracked_dry_03', 'whitecliff': 'marble_cliff_01', 'monastery': 'monastery_stone_floor',
    'mossyrock': 'mossy_rock', 'withered': 'withered_grass',
}
os.makedirs(OUT, exist_ok=True)
UA = {'User-Agent': 'EcosDoContracanto-texfetch/1.0'}


def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA)).read()


prov = ['# Texturas dos reinos (Poly Haven, CC0 1.0)', '', 'Baixadas por Tools/texgen/fetch_world_textures.py, resolução 1K.', '']
for name, slug in TEX.items():
    files = json.loads(get(f'https://api.polyhaven.com/files/{slug}'))
    for kind, key, suffix in (('albedo', 'Diffuse', 'albedo'), ('normal', 'nor_gl', 'normal')):
        url = files[key]['1k']['jpg']['url']
        dst = os.path.join(OUT, f'{name}_{suffix}.jpg')
        if not os.path.exists(dst):
            open(dst, 'wb').write(get(url))
        print(name, kind, os.path.getsize(dst))
    prov.append(f'- `{name}_*` ← https://polyhaven.com/a/{slug} (CC0)')
open(os.path.join(OUT, 'PROVENANCE.md'), 'w').write('\n'.join(prov) + '\n')
