# Prepara os sons gerados no ElevenLabs (Sound Effects v2) para o jogo:
# corta o silêncio do começo, ajusta a sonoridade (LUFS), limita o pico em -1 dB e
# exporta WAV 44,1 kHz (mono para os sons posicionais).
# raw/<nome>_<variação>.ogg  ->  Assets/Aren/Resources/Audio/{Eleven,Samples}/<saída>.wav
import json, os, re, subprocess
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ELEVEN = os.path.join(ROOT, "Assets/Aren/Resources/Audio/Eleven")
SAMPLES = os.path.join(ROOT, "Assets/Aren/Resources/Audio/Samples")
# (origem, saída, pasta, mono, LUFS alvo, laço)
SEL = [
    ("impact_far_2", "impact_far", ELEVEN, False, -13, False),
    ("impact_far_3", "impact_far_b", ELEVEN, False, -13, False),
    ("shock_blast_2", "shock_blast", ELEVEN, False, -12, False),
    ("wind_debris_1", "wind_debris", ELEVEN, False, -17, False),
    ("body_fall_1", "body_fall", ELEVEN, True, -15, False),
    ("getup_breath_3", "getup_breath", ELEVEN, True, -18, False),
    ("meteor_pass_4", "meteor_pass", ELEVEN, False, -14, False),
    ("debris_rain_2", "debris_rain", ELEVEN, False, -20, False),
    ("bell_cracked_3", "bell_cracked", ELEVEN, True, -14, False),
    ("sky_tear_1", "sky_tear", ELEVEN, False, -13, False),
    ("sky_tear_3", "sky_tear_b", ELEVEN, False, -13, False),
    ("title_braam_3", "title_braam", ELEVEN, False, -12, False),
    ("riser_4", "riser", ELEVEN, False, -15, False),
    ("inhale_vacuum_3", "inhale_vacuum", ELEVEN, False, -15, False),
    ("seven_launch_4", "seven_launch", ELEVEN, False, -16, False),
    ("dark_drone_loop_2", "dark_drone_loop", ELEVEN, False, -20, True),
    ("village_fire_loop_2", "village_fire_loop", ELEVEN, False, -20, True),
    ("night_crickets_loop_1", "night_crickets_loop", ELEVEN, False, -22, True),
    ("scream_woman_3", "scream_woman_a", ELEVEN, True, -14, False),
    ("scream_woman_1", "scream_woman_b", ELEVEN, True, -14, False),
    ("scream_man_1", "scream_man_a", ELEVEN, True, -14, False),
    ("scream_man_3", "scream_man_b", ELEVEN, True, -14, False),
    ("crowd_panic_3", "crowd_panic", ELEVEN, False, -17, False),
    ("corrupt_transform_2", "corrupt_transform", ELEVEN, True, -15, False),
    ("disintegrate_4", "disintegrate_a", ELEVEN, True, -15, False),
    ("disintegrate_1", "disintegrate_b", ELEVEN, True, -15, False),
    ("scream_choir_1", "scream_choir_a", ELEVEN, True, -14, False),
    ("scream_choir_3", "scream_choir_b", ELEVEN, True, -14, False),
    ("possessed_growl_1", "growl_a", ELEVEN, True, -15, False),
    ("possessed_growl_2", "growl_b", ELEVEN, True, -15, False),
    ("possessed_growl_4", "growl_c", ELEVEN, True, -15, False),
    ("running_cobble_2", "running_cobble", ELEVEN, False, -19, False),
    ("shutters_slam_3", "shutters_slam", ELEVEN, True, -16, False),
    ("crowd_murmur_loop_1", "crowd_murmur_loop", ELEVEN, False, -21, True),
    ("heartbeat_3", "heartbeat", ELEVEN, False, -15, False),
    ("hit_light_4", "el_hit_light1", SAMPLES, True, -14, False),
    ("hit_light_3", "el_hit_light2", SAMPLES, True, -14, False),
    ("hit_heavy_1", "el_hit_heavy1", SAMPLES, True, -13, False),
    ("hit_heavy_4", "el_hit_heavy2", SAMPLES, True, -13, False),
    ("swing_whoosh_2", "el_swing1", SAMPLES, True, -17, False),
    ("swing_whoosh_4", "el_swing2", SAMPLES, True, -17, False),
    ("parry_clang_2", "el_parry1", SAMPLES, True, -14, False),
    ("parry_clang_4", "el_parry2", SAMPLES, True, -14, False),
    ("enemy_dissolve_3", "el_enemy_dissolve", SAMPLES, True, -15, False),
    ("sonic_pulse_2", "el_sonic_pulse", SAMPLES, True, -13, False),
    ("dodge_whoosh_1", "el_dodge1", SAMPLES, True, -17, False),
    ("dodge_whoosh_4", "el_dodge2", SAMPLES, True, -17, False),
    ("finisher_slowmo_4", "el_finisher", SAMPLES, True, -13, False),
    ("aren_hurt_2", "el_aren_hurt1", SAMPLES, True, -16, False),
    ("aren_hurt_3", "el_aren_hurt2", SAMPLES, True, -16, False),
    ("enemy_hit_1", "el_enemy_hit1", SAMPLES, True, -15, False),
    ("enemy_hit_2", "el_enemy_hit2", SAMPLES, True, -15, False),
    ("possessed_growl_1", "el_growl1", SAMPLES, True, -16, False),
    ("possessed_growl_2", "el_growl2", SAMPLES, True, -16, False),
    ("possessed_growl_4", "el_growl3", SAMPLES, True, -16, False),
]

def lufs(path, af=""):
    a = ["ffmpeg", "-hide_banner", "-i", path, "-af", (af + "," if af else "") + "ebur128", "-f", "null", "-"]
    o = subprocess.run(a, capture_output=True, text=True).stderr
    m = re.findall(r"I:\s+(-?[\d.]+) LUFS", o)
    return float(m[-1]) if m else -20.0

for src, out, folder, mono, target, loop in SEL:
    inp = os.path.join(HERE, "raw", src + ".ogg")
    pre = [] if loop else ["silenceremove=start_periods=1:start_threshold=-48dB:start_silence=0.01"]
    if mono: pre.append("pan=mono|c0=0.5*c0+0.5*c1")
    chain = ",".join(pre)
    cur = lufs(inp, chain)
    gain = max(-20.0, min(20.0, target - cur))
    post = [f"volume={gain:.2f}dB", "alimiter=limit=0.891:attack=2:release=60:level=disabled"]
    if not loop:
        dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", inp], capture_output=True, text=True).stdout)
        post.append(f"afade=t=out:st={max(0.0, dur - 0.18):.3f}:d=0.18")
    af = ",".join([c for c in [chain] + post if c])
    dst = os.path.join(folder, out + ".wav")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", inp, "-af", af, "-ar", "44100", "-c:a", "pcm_s16le", dst], check=True)
    print(f"{out:22s} {cur:6.1f} -> {target} LUFS ({gain:+.1f} dB)")
