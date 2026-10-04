"""E/S de imagem sem PIL: o python do Blender tem numpy, e o ImageMagick (convert) faz a
decodificação/codificação por pipe em RGBA cru."""
import subprocess
import numpy as np


def load(path):
    w, h = map(int, subprocess.run(['identify', '-format', '%w %h\n', path], capture_output=True, text=True, check=True).stdout.split()[:2])
    raw = subprocess.run(['convert', path + '[0]', '-depth', '8', 'rgba:-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.uint8).reshape(h, w, 4).astype(np.float32) / 255.0


def save(path, a):
    a = np.asarray(a, np.float32)
    if a.ndim == 2:
        a = np.dstack([a, a, a])
    if a.shape[2] == 3:
        a = np.dstack([a, np.ones(a.shape[:2], np.float32)])
    a8 = (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
    h, w = a8.shape[:2]
    # -strip: sem data/hora dentro do PNG, então gerar de novo dá o mesmo arquivo (git limpo)
    subprocess.run(['convert', '-size', f'{w}x{h}', '-depth', '8', 'rgba:-', '-strip', '-define', 'png:color-type=6', path], input=a8.tobytes(), check=True)
