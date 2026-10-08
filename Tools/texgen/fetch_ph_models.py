# Modelos escaneados do Poly Haven (CC0 — https://polyhaven.com, uso comercial livre, sem atribuição
# obrigatória) para o mundo de Elyndra: pedras e falésias, portões, forte e objetos de vila.
# Baixa o glTF (com as texturas) em ~/EcosAssets/polyhaven/<id>/ — FORA do repositório: os originais
# são pesados (até ~3 milhões de faces). O ArtSource/WorldKit/scripts/wk_ph_reduce.py reduz para versões de
# jogo e só elas entram em Assets/.
# Uso: python3 Tools/texgen/fetch_ph_models.py [id1,id2,...]
import json, os, sys, urllib.request

UA = {"User-Agent": "EcosDeAcordia-assets/1.0"}
# id → resolução das texturas (2k nas peças grandes vistas de perto; 1k nos objetos pequenos)
MODELS = {
    # pedras e falésias (falésias do Mar de Vidro/Valtéria, Granith, pináculos, beiras de estrada)
    'boulder_01': '2k', 'rock_07': '2k', 'moon_rock_01': '1k', 'mountainside': '2k',
    'coastal_cliff_02': '2k', 'coastal_cliff_04': '2k', 'coast_rocks_05': '2k',
    # construções: portões dos reinos e postos de fronteira
    'modular_fort_01': '2k', 'large_castle_door': '2k', 'large_iron_gate': '1k',
    # vila
    'cannon_01': '1k', 'Lantern_01': '1k', 'Barrel_01': '1k', 'barrel_03': '1k',
    'wooden_bowl_01': '1k', 'ceramic_pot': '1k', 'jug_01': '1k',
    # troncos mortos
    'dead_tree_trunk': '1k', 'dead_tree_trunk_02': '1k',
}
OUT = os.path.expanduser('~/EcosAssets/polyhaven')


def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60)


def main():
    only = sys.argv[1].split(',') if len(sys.argv) > 1 else None
    total = 0
    for mid, res in MODELS.items():
        if only and mid not in only:
            continue
        files = json.load(get('https://api.polyhaven.com/files/' + mid))
        g = files['gltf'][res]['gltf']
        d = os.path.join(OUT, mid)
        os.makedirs(d, exist_ok=True)
        todo = [(g['url'], os.path.basename(g['url']), g.get('size', 0))]
        for rel, inc in g.get('include', {}).items():
            todo.append((inc['url'], rel, inc.get('size', 0)))
        for url, rel, size in todo:
            p = os.path.join(d, rel)
            if os.path.exists(p) and os.path.getsize(p) == size:
                continue
            os.makedirs(os.path.dirname(p), exist_ok=True)
            with get(url) as r, open(p + '.part', 'wb') as f:
                while True:
                    b = r.read(1 << 20)
                    if not b:
                        break
                    f.write(b)
            os.replace(p + '.part', p)
            total += size
        info = json.load(get('https://api.polyhaven.com/info/' + mid))
        with open(os.path.join(d, 'info.json'), 'w') as f:
            json.dump({'id': mid, 'name': info.get('name'), 'license': 'CC0', 'url': 'https://polyhaven.com/a/' + mid,
                       'authors': info.get('authors'), 'polycount': info.get('polycount'), 'res': res}, f, indent=1)
        print(f"{mid}: ok ({info.get('polycount')} faces)", flush=True)
    print(f"baixados {total / 1e6:.0f} MB em {OUT}")


if __name__ == '__main__':
    main()
