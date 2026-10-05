# Texturas fotográficas PBR da vila (Poly Haven, CC0 — https://polyhaven.com, uso comercial livre,
# sem atribuição obrigatória). Baixa em 1k, prepara para o shader Standard do Unity e grava em
# Assets/Campanula/Textures/PH: <mat>_albedo.png, <mat>_normal.png (OpenGL), <mat>_mg.png
# (metálico 0 + suavidade = 1 - rugosidade no alfa), <mat>_ao.png e ph_tiles.json (repetição
# calibrada: o kit repete cada material a cada TILE[mat] m; a foto tem tamanho real em mm).
# Uso: python3 Tools/texgen/fetch_polyhaven.py   (precisa do ImageMagick 'convert')
import json, os, subprocess, sys, tempfile, urllib.request

ASSETS = {
    'stone_wall': 'castle_wall_slates',      # muralha e torres: pedra cinza irregular
    'stone_dark': 'castle_wall_varriation',  # bases e fundações: pedra escura
    'cobble': 'cobblestone_floor_08',        # ruas e praça
    'plaster': 'plastered_wall_02',          # reboco das casas em enxaimel
    'timber': 'dark_wooden_planks',          # vigas escuras
    'planks': 'weathered_planks',            # tábuas gastas (portas, barracas)
    'roof_tiles': 'roof_tiles_14',           # telhas escuras
    'roof_slate': 'roof_slates_02',          # ardósia
}
# camadas do terreno (só albedo: normal em 4 camadas pesa no Intel UHD)
TERRAIN = {'grass': 'leafy_grass', 'dirt': 'stony_dirt_path', 'cobble': 'cobblestone_floor_08'}
TILE = {'stone_wall': 2.4, 'stone_dark': 2.0, 'cobble': 3.0, 'plaster': 2.5, 'timber': 1.2,
        'planks': 1.6, 'roof_tiles': 2.2, 'roof_slate': 2.0}
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Campanula', 'Textures', 'PH')

def get(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'EcosDeAcordia-texgen/1.0'})
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.read()

def main():
    os.makedirs(OUT, exist_ok=True)
    meta = {}
    info_all = json.loads(get('https://api.polyhaven.com/assets?t=textures'))
    tmp = tempfile.mkdtemp()
    for key, pid in ASSETS.items():
        files = json.loads(get('https://api.polyhaven.com/files/' + pid))
        def url(kind):
            try: return files[kind]['1k']['jpg']['url']
            except KeyError: return None
        paths = {}
        for kind in ('Diffuse', 'nor_gl', 'Rough', 'AO'):
            u = url(kind)
            if not u: continue
            p = os.path.join(tmp, f'{key}_{kind}.jpg')
            open(p, 'wb').write(get(u))
            paths[kind] = p
        o = lambda s: os.path.join(OUT, f'{key}_{s}.png')
        subprocess.run(['convert', paths['Diffuse'], '-resize', '1024x1024!', '-strip', o('albedo')], check=True)
        subprocess.run(['convert', paths['nor_gl'], '-resize', '1024x1024!', '-strip', o('normal')], check=True)
        if 'Rough' in paths:
            subprocess.run(['convert', '-size', '1024x1024', 'xc:black', '(', paths['Rough'], '-resize', '1024x1024!', '-colorspace', 'gray', '-negate', ')',
                            '-alpha', 'off', '-compose', 'CopyOpacity', '-composite', '-strip', o('mg')], check=True)
        if 'AO' in paths:
            subprocess.run(['convert', paths['AO'], '-resize', '1024x1024!', '-colorspace', 'gray', '-strip', o('ao')], check=True)
        size_m = info_all[pid]['dimensions'][0] / 1000.0
        meta[key] = {'id': pid, 'size_m': round(size_m, 3), 'tiling': round(TILE[key] / size_m, 4)}
        print(key, pid, meta[key], sorted(paths))
    for key, pid in TERRAIN.items():
        files = json.loads(get('https://api.polyhaven.com/files/' + pid))
        p = os.path.join(tmp, f't_{key}.jpg')
        open(p, 'wb').write(get(files['Diffuse']['1k']['jpg']['url']))
        subprocess.run(['convert', p, '-resize', '1024x1024!', '-strip', os.path.join(OUT, f'terrain_{key}_albedo.png')], check=True)
        meta['terrain_' + key] = {'id': pid, 'size_m': round(info_all[pid]['dimensions'][0] / 1000.0, 3)}
        print('terreno', key, pid)
    json.dump(meta, open(os.path.join(OUT, 'ph_tiles.json'), 'w'), indent=1)
    with open(os.path.join(OUT, 'PROVENANCE.md'), 'w') as f:
        f.write('# Texturas Poly Haven (CC0)\n\nFonte: https://polyhaven.com — licença CC0 (domínio público), uso comercial livre.\n'
                'Baixadas em 1k por Tools/texgen/fetch_polyhaven.py.\n\n| material | asset | tamanho real |\n|---|---|---|\n')
        for k, v in meta.items(): f.write(f"| {k} | [{v['id']}](https://polyhaven.com/a/{v['id']}) | {v['size_m']} m |\n")

main()
