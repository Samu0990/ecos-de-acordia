# AGENTS.md — Ecos de Acordia (guia para agentes: Codex, Claude Code etc.)

Este arquivo é lido automaticamente por agentes de código. Ele resume **tudo** que é preciso
para continuar o jogo sem reler o histórico. Leia inteiro antes de mexer.

---

## 1. O que é o projeto

**Ecos de Acordia: A Ruptura do Contracanto** — ação/aventura em 3ª pessoa. O bardo
**Aren Vesper** luta com uma flauta (combate *freeflow* estilo Arkham, com "música" nos
golpes) e faz parkour. Este repositório contém uma **demo jogável completa**:

- Mundo: **Campanula, a Vila dos Doze Sinos**, ao pôr do sol, com a Fenda (rasgo negro com
  nebulosa) no céu. Estrada sul (tutorial de parkour) → muralha/portão → rua do mercado →
  Praça dos Doze Sinos com a Torre (escalada) → riacho/ponte a leste → Campo da Fenda (chefe).
- Roteiro (`GameFlow`): estrada → 13ª badalada → luta no mercado (2 Ecos) → praça (2 ondas)
  → escalar a torre → ponte → chefe **Cervo Corrompido** (+2 Ecos) → tela final com estatísticas.
- Combate: combo de 4 golpes com avanço magnético, esquiva com i-frames, contra-ataque
  (anel dourado fecha sobre o inimigo), 4 habilidades — **Pulso de Ressonância (Q)**,
  **Lâmina de Frequência (E)**, **Eco Fantasma (R)**, **Contracanto (segurar clique, 3 níveis)**.
- UI completa (menu sobre a vila ao vivo, HUD, pausa, configurações salvas, controles,
  créditos, morte, fim). Áudio: síntese procedural + amostras gravadas.

> **Lore atual (2026-10-05/06):** o jogo agora segue a *Bíblia Definitiva v2 — Ecos do Contracanto*
> (`~/Downloads/Ecos_do_Contracanto_Biblia_Definitiva_v2.docx`): mundo **Elyndra** (não "Acordia"),
> Aren é mudo, vilão **Elyan Vharos** (Regente do Contracanto), 7 Notas Corrompidas, 7 Relíquias de
> Vael, 13 reinos. Campânula fica em Valtéria; o chefe local é o **Cervo de Contratempo**. Os textos
> acima sobre "Doze Sinos / 13ª badalada" são da demo antiga. Regras do autor que valem ACIMA da
> Bíblia: **só seres vivos são hospedeiros da Corrupção** (lugares/objetos nunca) e, por enquanto,
> **os inimigos do mundo são só humanos corrompidos** (`EnemyCatalog.HumansOnly`). Veja a seção 9.

Outros projetos na Área de trabalho (`EcosDaDiscordia*` em Godot, `EcosBattleground` em Roblox)
**não são este jogo** — não mexa neles sem o usuário pedir.

Motor: **Unity 6000.3.22f1**, Built-in Render Pipeline, Input System 1.20, Cinemachine 2.10.7
(FreeLook), UGUI 2.0 (Text legado + fontes Noto). Base de parkour: **Dynamic Parkour System**
(DPS, MIT). Projeto: `~/Unity/ParkourLab`, branch **`main`** (o `remake-aren` foi juntado na `main` em 2026-10-04); repositório público https://github.com/Samu0990/ecos-de-acordia.

---

## 2. Regras do dono do projeto (obrigatórias)

1. **Fale em português (PT-BR)** com o usuário. Ele escreve informal, com erros de digitação.
2. **Git:** o jogo está no repositório **público** https://github.com/Samu0990/ecos-de-acordia (branch `main`). Trabalhe **direto na `main`: não crie branches novas** nem abra PR. Antes de começar, `git pull`; commit a cada etapa com mensagem em português e `git push` em seguida. Ele ficou público por decisão do dono (2026-10-04) para o ChatGPT conseguir ler: não mude a visibilidade, não adicione colaboradores, não suba builds (`Builds/` fica no .gitignore) e nunca coloque senhas/chaves no repositório.
   Mensagens de commit em português, descrevendo o que mudou e como foi testado.
3. **Nunca use animações do Game Animation Sample da Unreal** (licença só para Unreal).
   Use as do projeto, **Mixamo** ou **CC0** (Quaternius UAL2 já está no projeto).
   Mixamo exige login do usuário — peça para ele baixar; não crie contas.
4. **NÃO MEXA EM NADA RELACIONADO A CIRURGIA / VR MÉDICO.** É outro projeto do usuário.
   Não abrir, não editar, não mover, não apagar, não importar para este jogo, não usar como
   referência. Em `~/Downloads`, isso inclui:
   `cirugia coisas/`, `Meshy_models_20260912_005939*`, `Meshy_models_20260912_014004*`
   (luvas `mao1.glb`/`mao2.glb`), `documentacao_completa_do_jogo.md`,
   `PROMPT MASTER — VR SURGERY SIMULATOR - JOGO DE CIRURGIA EM VR.md`, `VR-Surgery-main*`,
   `Surgical_Training_IMSTK-Unity-main*`, `Medical-Unity-VR-main*`, `ic-vr-game-main*`,
   `Estado-do-Projeto-VR-Transplante.pdf`, `Vr Surgical Test.pdf`,
   `guia detalhado desenvolvimento vr.pdf`, `bisturi.glb`, `Heart_Vessels.glb`,
   `Meshy_AI_Heart_*`, `Meshy_AI_Body_Skin_*`, `Meshy_AI_clinical-mannequin-supine.png`,
   `hospital-bed.glb`, `Operating_Table_Final.glb`, `Ribcage_Fixed_Final.glb`,
   `human skeleton 3d model.glb`, `human-skeleton-obj/`, `anatomical+heart+3d+model.zip`,
   `anatomical muscle model 3d.zip`, `realistic+head+3d+model.zip`. Na dúvida se algo é da
   cirurgia: **não toque** e pergunte ao usuário.
5. **Desempenho**: alvo **≥ 45 FPS** no notebook dele — Dell Latitude 3490, **Intel UHD 620**,
   7,6 GB RAM, Linux Mint, 1920×1080, qualidade **Média** com escala 3D **80%**. Meça antes
   de afirmar (benchmark do executável, seção 5). Sombras em tempo real custam ~10 FPS fixos.
6. **Honestidade**: não diga "pronto" sem testar; relate limitações e falhas com números.
7. **Licenças**: todo asset novo precisa de licença compatível com uso comercial e entra nos
   **Créditos** (`Assets/Aren/Scripts/UI/GameMenus.cs`, `BuildCredits`). Sem assets do Batman.
8. O usuário costuma ficar ausente: trabalhe de forma autônoma e só pergunte o que realmente
   depende dele.

---

## 3. Mapa do código

```
Assets/
  Aren/
    Scripts/
      Combat/   ArenCombat (FSM única: Free/Attack/Dodge/Counter/Action/Hurt/Dead),
                ArenAbilities (recurso Ressonância + 4 habilidades), AttackData (ScriptableObject),
                ArenHealth, ArenFlute (bainha↔mão), FrequencyBlade, TargetResolver,
                CombatTypes (HitData, IDamageable, ICounterable, CombatRegistry),
                GameFeel (dono do timeScale: hitstop/slow-mo/pausa; ArenCameraFX shake/FOV),
                TorsoFacingLock (mantém o tronco virado para o alvo — ver §7)
      Enemies/  EnemyBase (IA: Spawning/Idle/Chase/Circle/Telegraph/Attack/Recover/Hurt/
                Stunned/Knockdown/Dead; ganchos virtuais), EnemyEco, EnemyDeer (chefe 300 HP:
                garra/pisão contra-atacáveis, investida só esquiva com pré-eco, fase 2 invoca Ecos),
                ThreatDirector (máx. 2 atacando), CounterIndicator,
                EnemySussurrante (inimigo da prancha do autor: cortes rápidos com counter, investida
                só-esquiva, golpe pesado e GRITO de Distorção; vulnerável depois dos pesados),
                SussurranteFX (+BladeTrail: olhos, fenda do peito, lascas, fiapos, rastro do corte,
                grito, morte em lascas, materialização), SussurranteDistortion (classe Distortion:
                medidor 0–100 no Aren, vinheta/aberração, RUPTURA em 100), EchoFragment (drop
                "Fragmento de Eco", contador em PlayerPrefs eda_echo_fragments)
      Movement/ ArenJump (pulo livre, pouso em 3 níveis), ArenPivot, ArenLean
      Input/    ArenInput (ações + buffer de 0.2 s, prioridade Counter>Dodge>Attack>Ability)
      VFX/      ArenVFX/VFXRunner (pool de efeitos procedurais + partículas), BlobShadow
      Audio/    ArenAudio/AudioRunner (synth em thread + amostras), ArenSynth, ArenFootsteps
      UI/       UIKit, ArenHUD, GameMenus, GameSettings (PlayerPrefs eda_*), UIWaveform,
                TitleScreen (tela inicial em pixel art, ver §6), DemoTitleShots (-eda-title)
      World/    GameFlow (roteiro, encontros, pausa, morte, checkpoints, DebugJump),
                CutsceneDirector (abertura da lore: sinos, 13ª badalada, Fenda, cervo),
                Encounter (ondas), BellRinger, RenderScaler (escala 3D + pós-processamento:
                cor/vinheta/bloom via Resources/Shaders/ArenPostUber), DemoBenchmark,
                DemoAutoTest, DemoDiag (áudio/visibilidade), DemoIntroShots (capturas da abertura)
      Movement/ ArenThirdPersonSetup.Tune — parâmetros da câmera do jogador (órbitas, FOV,
                colisor com distância mínima 1.1 m); Combat/ArenCombatCamera enquadra o alvo
      Debug/    ArenTestProbe (robô de input virtual: teclado/mouse falsos dentro do jogo)
    Editor/     RedAssassinSetup (modelo atual do jogador: Humanoid explícito, T-pose, materiais,
                troca no Player.prefab), ArenCombatSetup (estados de combate no Animator do DPS + AttackData),
                ArenEnemySetup (Eco.prefab, Deer.prefab, controllers, material corrompido),
                ArenAnimatorSetup, ArenModelPostprocessor (UAL2), ArenCharacterSetup,
                ArenUIImport, ArenAudioImport
    Resources/  Shaders/ (FX e inimigo), VFX/ (texturas geradas + Space/ nebulosas),
                Audio/Samples/ (sons gravados), UI/ (sprites e fontes)
    Enemies/    Eco.prefab, Deer.prefab, controllers, materiais
    Combat/     AttackData dos golpes (.asset)
    ThirdParty/Quaternius_UAL2/  animações CC0
  Campanula/
    Editor/     CampanulaBuilder (GERA A CENA INTEIRA), CampanulaImport (regras de import),
                FantasyPropsSetup (materiais do kit de props)
    Scripts/    RiftPulse, Spin, GroundHeight/StreamMath, CampanulaMarkers
    Shaders/    SunsetSky, Rift, Water, Trim (props do kit), Flame (tochas), Foliage (árvores:
                normais esféricas, oclusão por vértice, vento, contraluz)
    Models/     kit da vila exportado do Blender (FBX)
    ThirdParty/FantasyProps/  Quaternius Fantasy Props MegaKit (CC0)
    Scenes/Campanula.unity    ÚNICA cena do build
  Dynamic Parkour System/     DPS (MIT) com alterações mínimas comentadas; Player.prefab
ArtSource/
  Campanula/scripts/  kit_lib.py, kit_buildings.py, kit_props.py, kit_trees.py (árvores e arbustos
                      com volume — sobrescreve os do kit_props), preview.py (Blender headless)
  RedAssassin/scripts/ra_export.py  modelo atual do jogador (GLB do Tripo em ~/Downloads → FBX)
  Aren/scripts/       rig do Aren (aren_01..05)
  Deer/deer_rig.py    rig do cervo
Tools/
  cli/   ue, uej, rc, cons, play, cap, capui, lib.sh, bench.sh, cs/*.cs (scripts de editor)
  texgen/ gen_fx.py, gen_world.py (1024 px), gen_details.py (capim/trigo), gen_ui.py
Builds/EcosDeAcordia_Demo/   executável Linux (fora do git)
```

**A cena é gerada.** Não edite `Campanula.unity` à mão: altere
`Assets/Campanula/Editor/CampanulaBuilder.cs` e rode `CampanulaBuilder.Build()` (recria
terreno, luz, construções, props, parkour, gameplay, NavMesh, static batching, occlusion).
Os modelos da vila vêm de `ArtSource/Campanula/scripts` (Blender 5.2 headless → FBX em
`Assets/Campanula/Models`).

### Controles
WASD mover · Mouse câmera · Shift correr · Espaço pular/parkour (W+Espaço salta para a
pedra de cima na escalada) · C soltar/deslizar · Clique esq. atacar (segurar = Contracanto) ·
Clique dir. contra-atacar · Ctrl esquiva · Q/E/R habilidades · Esc pausa · F3 painel do DPS.

---

## 4. Trabalhar pela CLI do Unity (sem clicar no editor)

O editor precisa estar aberto com o pacote `com.unity.pipeline` (já instalado):

```bash
cd ~/Unity/ParkourLab
unity open .                 # abre o editor (espere `unity status` mostrar "ready")
unity status
Tools/cli/ue  arquivo.cs     # executa C# no editor (limite ~5 s por operação de main thread)
Tools/cli/uej arquivo.cs     # mesmo, como job destacado (importação, build de cena, etc.)
Tools/cli/rc                 # refresh do AssetDatabase + recompilar e esperar
Tools/cli/cons 15            # últimos erros/warnings do Console
```

Formato do `.cs` para `ue`/`uej`: corpo de método, com `return "texto";` no fim (veja
`Tools/cli/cs/*.cs`). Exemplos úteis já prontos:

| Script | Faz |
|---|---|
| `Tools/cli/cs/build_scene.cs` | `CampanulaBuilder.Build()` — regenera a cena (~2–4 min; use job destacado) |
| `Tools/cli/cs/build_player.sh` | compila o executável Linux e espera |
| `Tools/cli/cs/views.cs`, `views_props.cs`, `views_zoom.cs` | renderiza vistas da cena em `Tools/cli/shots/` |
| `Tools/cli/cs/topdown.cs` | vistas de cima com grade de 5 m (planejar posições) |
| `Tools/cli/cs/kit_sheet.cs` | prancha dos props do kit |
| `Tools/cli/cs/anim_facing_rt.cs` | mede para onde o tronco aponta em cada estado do Animator |
| `Tools/cli/cs/ledges_near.cs` | lista bordas (Ledge) perto de um ponto |

Job longo (ex.: regenerar a cena) sem o limite de 300 s do `uej`:
```bash
id=$(unity --json command eval_file --file Tools/cli/cs/build_scene.cs --timeout 1200 --detach | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["jobId"])')
unity --json job status $id
```

Logs: `~/.config/unity3d/Editor.log` (editor) e `-logFile` no executável.

**Play mode no editor fica a ~1 FPS nesta máquina** (problema não resolvido). Teste no
**executável** (seção 5). Os scripts `Tools/cli/play`, `cap`, `capui` e `lib.sh probe` são do
fluxo antigo em Play mode — funcionam, mas lentos.

---

## 5. Compilar, testar e medir

```bash
cd ~/Unity/ParkourLab
Tools/cli/rc                                   # compila scripts
# (se mexeu no builder) regenerar a cena: job com build_scene.cs (acima)
Tools/cli/cs/build_player.sh                   # Builds/EcosDeAcordia_Demo/EcosDeAcordia.x86_64
cd Builds/EcosDeAcordia_Demo
./EcosDeAcordia.x86_64 -eda-test -eda-quality 1 -eda-scale 0.8 -logFile ~/EcosBench/test_player.log
grep -a AUTOTEST ~/EcosBench/test_player.log   # resultado; detalhes em ~/EcosBench/tests.txt
cd ~/Unity/ParkourLab && Tools/cli/bench.sh 1 0.8 0   # benchmark (feche o editor antes, se possível)
```

Mais argumentos: `-eda-diag` (estrada→mercado→praça→chefe com o robô atacando; grava em
`~/EcosBench/diag.txt` o nível REAL da saída de áudio em dB, NaN na saída, vozes, e se o jogador
fica invisível na tela), `-eda-intro` (começa o jogo, deixa a abertura tocar e salva
`~/EcosBench/intro_NN.png`), `-eda-title` (fica na tela inicial, passa a seleção pelos três
botões, abre Configurações, salva `~/EcosBench/title_NN.png` + `title_seqNN.png` e o FPS em
`title.txt`), `-eda-title-video` (12 s da tela inicial a 30 quadros/s em `~/EcosBench/video/f_NNNN.jpg`; MP4: `ffmpeg -framerate 30 -i ~/EcosBench/video/f_%04d.jpg -c:v libx264 -crf 20 -pix_fmt yuv420p tela.mp4`), `-eda-intro-video` (grava 30 s da abertura a 30 q/s em `~/EcosBench/intro_video/f_NNNN.jpg`), `-eda-uncapped` (sem limite de 60 FPS, para medir folga). Abertura v2: `-eda-intro-noprologue`, `-eda-intro-every K` (salva 1 a cada K quadros), `-eda-intro-from S`, `-eda-intro-seconds S` (duração do `-eda-intro-audio`, que também registra `[FPS]`), `-eda-perf` (pula a abertura e mede o FPS ligando/desligando paisagem, céu, halos, pós, sombras), `-eda-day` (pôr do sol, para A/B), `-eda-look-debug` (log do olhar procedural). Rodada de 2026-10-05: `-eda-corruption-video` / `-eda-corruption-audio` (pula para o portão com a noite aberta, toca a cena da vila e grava quadros em `~/EcosBench/corruption_video/` ou o WAV + FPS), `-eda-combat-video` (luta no mercado com o robô, 20 s de quadros em `combat_video/`), `-eda-loading-shot` (captura da tela de carregamento). Vídeo com som: grave `-eda-intro-video` (quadros) e `-eda-intro-audio` (WAV), alinhe no primeiro quadro 3D (o prólogo dura 12,9 s de jogo) e junte com ffmpeg.
`Tools/cli/job arquivo.cs [timeout]` roda C# no editor como job longo (reenvia se ocupado).

Argumentos do executável: `-eda-test` (robô de input: vault, fardos, slide, escalada da
muralha e da torre, orientação no combate, encontros), `-eda-benchmark` (FPS médio, 1% baixo e
CPU por trecho → `~/EcosBench/bench.txt` + capturas), `-eda-quality 0|1|2`, `-eda-scale 0.5–1`,
`-eda-shadows 0|1` (sobrescrevem as configurações salvas sem gravá-las).

Critérios para considerar uma etapa pronta:
- `AUTOTEST`: muralha termina em cima/além do muro; torre termina em **y ≈ 19** (sineira);
  `ORIENTACAO com trava` com **costas(>90°) ≤ 3 %**; encontros: mercado 2, praça 4,
  campo 3 com chefe; **0 `Exception`** no log.
- Benchmark Média 80% sem sombras: **≥ 45 FPS** em estrada/mercado/praça/campo.

Resultado de 2026-10-06 (rochas e madeira do kit da natureza, `BuildRocks`): `-eda-test` passa (muralha, torre y=19,
orientação com trava 0 %/0 %, encontros 2/4/3 com chefe, 0 exceções). Benchmark A/B Média 80% 1080p contra a build
anterior SEM rochas, mesma hora: estrada 25,3 vs 25,7, mercado 29,6 vs 30,3, praça 27,7 vs 28,7, campo 32,8 vs 32,6 FPS
(as rochas custam ≲ 1 FPS). Os números absolutos estavam baixos porque a máquina estava com ~5,7 GB de swap em uso
(editor + várias sessões de IA abertas) — a build sem rochas também caiu para ~26–33 FPS. Meça de novo com tudo fechado.
Último resultado (2026-10-05, manhã, depois da abertura v3, aldeões-Ecos, cena da vila, sons do ElevenLabs):
`-eda-test` passa (muralha, torre y=19, orientação com trava 0 %/0 %, encontros 2/4/3 com chefe, 0 exceções).
Benchmark Média 80% 1080p com o editor FECHADO, mas a máquina quente (GPU a 1100 MHz e a CPU caindo para
1,9–2,0 GHz — as duas dividem o limite de energia): estrada 40, mercado 43, praça 45, campo 47 FPS. A/B na mesma
hora com `-eda-day` (sem nada da noite): 42/45/46/50 → a noite custa ~1,5–2 FPS; o resto é o estado da máquina.
Repita com a máquina fria (CPU ≥ 2,9 GHz) antes de concluir algo. Cena da vila: 48 FPS em 1280x720.
Resultado anterior (2026-10-05, depois da abertura v2, catedral, janelas e texturas Poly Haven; CPU a 2,9 GHz):
benchmark Média 80% 1080p ~**60 FPS** (limite) em estrada, mercado, praça e campo; a abertura roda a
55–60 FPS com quedas para ~40 nos segundos do impacto. `-eda-test` passa (a métrica de orientação com
trava oscila: deu 4% numa rodada e 0% na seguinte — rode de novo antes de concluir algo).
Resultado anterior (2026-10-02, depois dos props e efeitos novos): todos os testes passam,
0 exceções; benchmark ~**60 FPS** (limite) em estrada, mercado, praça e campo com o chefe,
menu ~45 FPS. Com a máquina quente após horas de compilação já deu 43–47 FPS.

---

## 6. Pipelines de assets

| Asset | Onde | Como entra |
|---|---|---|
| Animações UAL2 (Quaternius, CC0) | `Assets/Aren/ThirdParty/Quaternius_UAL2/UAL2_Standard.fbx` | `ArenModelPostprocessor`: Humanoid, raiz embutida na pose, **`rotationOffset = 180`** (§7) |
| Kit da vila (próprio) | `ArtSource/Campanula/scripts` → `Assets/Campanula/Models` | Blender 5.2 headless; `CampanulaImport` (bakeAxisConversion, materiais CMP_* por nome) |
| Props Fantasy Props MegaKit (Quaternius, CC0) | `Assets/Campanula/ThirdParty/FantasyProps/` | `CampanulaImport` + `FantasyPropsSetup.Setup()` (materiais MI_* no shader `Campanula/Trim`: trim sheet + ORM, cor de vértice nas variantes `_Vertex`, emblema pelo UV2 nos estandartes, pano dupla face). Colocação: `CampanulaBuilder.BuildKitDressing` com `Kit(...)`, `WallTorch(...)`, `WallBanner(...)` |
| Sons gravados (400 Sounds Pack, Chequered Ink — uso comercial livre, crédito opcional) | `Assets/Aren/Resources/Audio/Samples/` | preparados por script (silêncio inicial cortado, pico normalizado, mono nos posicionais); `ArenAudioImport` (curtos DecompressOnLoad, longos CompressedInMemory); mapeados em `AudioRunner.LoadSamples()` |
| Nebulosas/estrelas (Screaming Brain Studios, CC0) | `Assets/Aren/Resources/VFX/Space/` | usadas nos shaders do inimigo, fantasma, Fenda, céu, cortes, anéis e no portal |
| Texturas procedurais | `Tools/texgen/*.py` → `Assets/Aren/Resources/VFX`, `Assets/Campanula/Textures`, `UI` | numpy dentro do Python do Blender (`~/.local/opt/blender-5.2/5.2/python/bin/python3.13`; o python3 do sistema não tem numpy) |
| **Jogador atual: "red assassin"** (Tripo, já riggado, 41 ossos) | `~/Downloads/red+assassin+3d+model.glb` → `Assets/Aren/Character/RedAssassin/` | `blender -b --factory-startup --python ArtSource/RedAssassin/scripts/ra_export.py` (229k→40k tris, 1.78 m, texturas 2048/1024, MetallicGloss, ossos FluteSocket/FluteHolster + flauta) e depois `RedAssassinSetup.All()` (Humanoid explícito, T-pose, materiais, troca no Player). O Aren antigo (`Aren.fbx`) continua no projeto, fora do prefab |
| Árvores/arbustos | `ArtSource/Campanula/scripts/kit_trees.py` → `Assets/Campanula/Models/Tree_*.fbx`, `Bush.fbx` | importados com normais personalizadas (`CampanulaImport`); material `CMP_foliage` usa `Campanula/Foliage` |
| **Texturas fotográficas da vila** (Poly Haven, CC0) | `Assets/Campanula/Textures/PH` | `python3 Tools/texgen/fetch_polyhaven.py` baixa em 1k e prepara (albedo, normal OpenGL, `_mg` suavidade no alfa, `_ao`, `ph_tiles.json` com a repetição calibrada pelo tamanho real) → `CampanulaMaterials.Setup()` usa nos CMP_* (Standard) e `CampanulaMaterials.TerrainPH()` nas camadas do terreno (só albedo). Origem: `PH/PROVENANCE.md`; créditos no jogo |
| Capim e trigo do terreno | `Tools/texgen/gen_details.py` → `detail_grass.png`, `detail_wheat.png` | `CampanulaBuilder.BuildTerrainDetails`; distância/densidade por qualidade em `GameSettings.Apply` |
| **Tela inicial** (arte do autor, "exatamente assim") | `ArtSource/Menu/title_reference.webp` → `Assets/Aren/Resources/UI/Title/` | `~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Menu/scripts/build_title.py` (E/S pelo ImageMagick em `imgio.py`): fundo com o painel reconstruído atrás dos botões, placas apagada/vermelha e textos recortados da arte, máscara do título, brilho/névoa em pixel art e `title_layout.json` (posições em px da arte, chamas). `ArenUIImport` importa sem compressão. `TitleScreen` monta tudo em coordenadas da arte (1672x941, escala "cobrir"), shaders `Hidden/Aren/UITitle*` (pixel nítido, aditivo, névoa, reflexo). Fundo em movimento ("motion"): `ArtSource/Menu/scripts/motion_maps.py` gera `motion_a/b.png` (paralaxe por pixel com o painel em profundidade zero, máscaras da lanterna, lustre, cruz e estandarte, ar quente das velas, luz das velas no cenário) e o shader `Hidden/Aren/UITitleMotion` desloca a arte por pixel (câmera em Lissajous + zoom-base de 2,5% para a borda nunca ler fora da imagem, pêndulos, onda do pano); `UITitleRays` são os feixes de luz. `TitleMotion.Forward()` faz halos e brasas acompanharem o cenário. As animações usam `TitleClock` (tempo real que respeita `Time.captureDeltaTime`). Com a arte na tela as câmeras 3D só limpam a tela (cullingMask 0) e voltam ao sair do menu. Controles e Créditos ficam dentro de Configurações. Origem/licença: `ArtSource/Menu/PROVENANCE.md` |
| **Prólogo da abertura** (motion graphics) | código: `Assets/Aren/Scripts/World/PrologueFX.cs` | software renderer 480x270 (halftone por tamanho de ponto, glitch de fatias, queima, partículas aditivas) tocado por `CutsceneDirector` antes do voo sobre Campanula; técnicas inspiradas no BlumeApp do autor (`~/Área de trabalho/BlumeApp`) — não usar os áudios/coreografia de lá (vieram de vídeos de terceiros) |
| **Atenção: jogador = RedAssassin** | `Assets/Dynamic Parkour System/Prefabs/Player.prefab` | NÃO rode o menu `Aren/Setup/Player - Trocar Erika pelo Aren` (`ArenCharacterSetup.SwapPlayerModel`): ele recoloca o Aren antigo dentro do Player (aconteceu em 2026-10-04 e foi revertido). Para refazer o jogador use `RedAssassinSetup.All()` |
| **Abertura cinematográfica v2 (noite da Ruptura)** | `Assets/Aren/Scripts/World/Night/` + `CutsceneDirector.cs` | Storyboard do autor. Planos: S0 sino sob o telhado com a lanterna e a vila ao fundo → S1 grua de estabelecimento → S2 o Aren TOCA FLAUTA (IK de `ArenFlutePerformancePose.cinematic`) de perfil contra a lanterna → S3 uma nota da flauta entorta, ele para, o sino canta sozinho e desafinado (poeira vibrando em onda estacionária, chamas "batendo"), a torre responde errada → S4 a Fenda MUITO longe: ponto → rachadura → céu estilhaçado (sobre o ombro, rosto na luz violeta, plano aberto de escala) → S5 a Fenda inspira e solta os sete (contáveis, ritmo irregular) → S6 o dourado passa por cima da vila; grua revela o vale; grilos calam (silêncio relativo) → S7 impacto no planalto do leste (~2,5 km): núcleo, clarão que acende a vila, ondas de choque no chão e nas nuvens, cúpula com distorção, cortina de poeira, pilar de luz; 4,3 s depois a onda de pressão: câmera reage por mola (antecipação→golpe→recuperação), rajada de poeira, sino/lanterna, susto contido → S8 título e transição contínua para o jogo. Sistemas: `FarLands` (paisagem procedural de ~11 km, `FarLand.shader` com névoa/luz próprias), `NightSky.shader` (Fenda, nuvens, anel de choque), `SevenGlows`, `ImpactFX`, `CinematicRig` (luzes da abertura + pós HDR), `VillageFX`, `VillageLights` (janelas acesas, lanternas da muralha, sineira), `NightCommon.cginc`. Pós cinematográfico: `RenderScaler.Cinematic` (HDR R11G11B10, ombro de filme, bloom em escalas, rastro anamórfico, raios de luz, grão). Som: `RuptureSynth` + `RuptureSynthCinematic`, PRÉ-GERADOS em `Resources/Audio/Rupture` (rode `Tools/cli/job Tools/cli/cs/rupture_bake.cs` depois de mudar a síntese). REGRA DE LORE: nunca silêncio total, o som fica ERRADO (nada de glitch digital/bitcrush). **v3 (2026-10-05):** a frente de pressão varre o vale (`ImpactFX.UpdateFront`), corte seco para o perfil em câmera lenta (`GameFeel.SlowMo(dur, escala, saída)`), o Aren é arremessado (`Aren Death` = Hit_Knockback, pino recua 0,55 m), anel de refração na lente + pico de aberração (`RenderScaler.Ripple/AberrationPunch`), ouvido abafado (`OpeningSound.muffle` = passa-baixa no AudioListener) com zumbido e coração, plano do rosto no chão, levanta tossindo (`Aren Revive`), cinza e brasas caindo; `OpeningSound.SoftLimiter` no ouvinte (a soma do impacto passava de 0 dBFS). Prévia no editor sem Play mode: `NightPreview` (`Tools/cli/cs/night_*.cs`; `stage=1` monta o sino e o Aren, `day=1` sem a noite, `shards=T` simula os cacos T s depois do estilhaço, `purkinje=X`) |
| **Cacos da Fenda + abertura v4** (2026-10-05, tarde) | `ArtSource/Fenda/scripts/fenda_shards.py` → `Assets/Aren/Resources/Fenda/FendaShards.fbx`; `FendaShards.cs`, `Hidden/Aren/FendaShard` | 272 cacos de rocha (12 formas facetadas do Blender) num losango em volta do rasgo, como no storyboard, a ~12,3 km da câmera (atrás da paisagem; o grupo segue a câmera = sem paralaxe; `NightSetup.FarClip` = 14 km). Luz falsa no shader vinda do eixo do rasgo (aro aceso, face virada para a luz violeta, resto quase preto). `Shatter()`: nascem do rasgo como luz, arremessados girando, freiam (explosão congelada) e esfriam até pedra; a inspiração dos sete os puxa, cada lampejo empurra. Fenda aberta sem estilhaço (abertura pulada, testes) → `Settle()` automático no `RuptureSky`. Os cacos pintados no céu ficaram fracos (`_FragAmount` 0,15). S4 novo: (b) teleobjetiva do estilhaço com sucção de ar antes, (c) "dolly zoom" no rosto. Luz: `RenderScaler.Purkinje` (visão noturna: escuros cinza-azulados, luzes quentes com cor), lua 0,5 e ambiente menor, luz direcional da Fenda e brasa do impacto como luzes "não importantes" (entram nos harmônicos esféricos: sem passada extra na GPU). Som: `explosions.py` (camadas: estalo, corpo, grave saturado, vale), `births.py` (os sete com o sino gravado), coro em crescendo, rachadura no céu, tremor do chão no impacto, sucção antes da onda (`ImpactFX.PressureIn`) |
| **Sons gerados por IA (ElevenLabs Sound Effects v2)** | `ArtSource/Audio/ElevenLabs/` → `Assets/Aren/Resources/Audio/Eleven/*.wav` (abertura, vila, laços) e `Resources/Audio/Samples/el_*.wav` (combate) | Gerados na conta do autor (4 variações por pedido; escolha pelo espectrograma). `prepare.py` corta silêncio, ajusta LUFS e limita o pico; `detune.py` (Python do Blender, numpy) faz as versões DESAFINADAS a partir das gravações (nota da flauta que escorrega, sinos batendo, sino tremendo — nada eletrônico). `ArenAudioImport.Eleven()` (laços em PCM, longos Vorbis em memória). No jogo: `OpeningSound` carrega como `x_<nome>` e uma gravação `x_<id>` substitui a síntese de mesmo id (`flute_a`, `flute_wrong`, `tower_tuned`, `tower_detuned`, `bell_wobble`); o combate usa `el_*` como camadas no `AudioRunner.LoadSamples` (as 13 badaladas do `BellRinger` também: `el_bell_toll` = o sino gravado 7 semitons abaixo, `el_bell_toll_corrupt` = tremendo + trítono + pré-eco). **Licença: plano gratuito do ElevenLabs = sem uso comercial e com atribuição (nos créditos). Antes de vender: plano pago ou trocar os arquivos** (`PROVENANCE.md`). Automação do site: ver §7 |
| **Sussurrante** (prancha do autor "Eco Primordial · Rasgos") | `ArtSource/Sussurrante/` (README + PROVENANCE) → `Assets/Aren/Enemies/Sussurrante/` (FBX, lâmina, texturas, materiais, clipes, controller) → prefab `Assets/Aren/Resources/Sussurrante/Sussurrante.prefab` | Malha: Tripo H3.1 pelo Higgsfield a partir de uma pose A gerada da prancha (FLUX.1 Kontext) — 7 scripts do Blender (`suss_01..07`): solda costuras do glTF, 26 k/8,5 k tris, esqueleto UAL2 com dedos encaixado por medidas, pesos por região, bake de normal/AO no atlas do Tripo, gradação para a paleta da prancha, lâmina procedural, marcadores (weapon_r, weapon_tip_r, eye_l/r, mouth, chest_fx). `SussurranteSetup.All()` (Humanoid explícito + T-pose calculada, shaders `Aren/Sussurrante` e `Aren/SussurranteBlade`, clipes de músculo Grito/Morte/Ascensão gerados da pose da UAL2, LODGroup, lâmina no punho) e `SussurranteSetup.Preview()` (folha de poses sem Play mode). Sons: `suss_audio.py` (sussurros + canto da lâmina) e gravações do ElevenLabs que já estavam no projeto. No jogo: o 2º aldeão tomado na cena da Corrupção vira o Sussurrante (estoura em lascas, ele sobe e grita) e luta no mercado; mais um na 2ª onda da praça; o catálogo do mundo de Elyndra usa o mesmo prefab (id "sussurrante") |
| **Aldeões** (Quaternius Modular Outfits Fantasy + Universal Base Characters, CC0) | pacotes em `~/Downloads/QuaterniusChars` → `ArtSource/Villagers/scripts/villager_export.py` → `Assets/Aren/Character/Villagers/*.fbx` | Blender headless junta roupa de camponês + cabeça do personagem base + cabelo numa malha (4 variantes, ~10–11 mil vértices). `VillagerSetup.All()` (menu Aren/Setup/Aldeões - tudo): Humanoid com avatar próprio, materiais `Aren/Villager` (vivo → `_Corrupt` sobe dos pés → `_Dissolve` em brasa; também aceita `_Flash/_Telegraph` do EnemyBase), `Villager.controller` (UAL2 + corrida do DPS), prefabs `Resources/Villagers/Villager_*` e `Resources/Enemies/Eco_*` (**todos os Ecos da demo agora têm corpo de aldeão**, variante aleatória; o cervo continua). Mesmo esqueleto da UAL2, mas proporções diferentes do manequim (por isso cada um tem o próprio avatar) |
| **Campânula gótica (reforma do mapa, 2026-10-05 tarde)** | `ArtSource/Campanula/scripts/kit_gothic.py` → `Assets/Campanula/Models/G*.fbx`, `Aqueduct`, `Great_Aqueduct`, `Gorge_Wall`, `Cliff_Rock_*`, `Bell_Pavilion`, `Banner_Clef`; `CampanulaBuilder` (`BuildGorge`, `BuildSkyline`, `BuildGreatAqueduct`, `BuildUpperTown`, `BuildGroundMist`) | Igual ao painel "Referência de Campânula" do storyboard. Sobrados góticos de pedra no lugar das casas de enxaimel (MESMOS lotes: ruas, parkour e encontros não mudaram), torres com agulha/cúpula, Torre dos Sinos com agulha octogonal, estandartes com a clave de sol (`banner_albedo.png`, glifo da Noto Music/OFL), pórtico de pedra no sino da estrada (`BellShrine` usa o `Bell_Pavilion` da cena e pega o chão dele). **Desfiladeiro**: `StreamMath.Gorge` (16 m, cabeceira com cachoeira em z 62, sobe ao sul), ponte-aqueduto de 3 vãos em z 9,5, muros de arrimo com bueiros/cachoeiras (`Campanula/Waterfall`), penedos, névoa (`Campanula/Mist`), lanternas no fundo; quem cai é levado à borda (`GameFlow.GorgeRescue`). **Grande Aqueduto** (3 andares, z 78) com água no canal, cachoeiras e luz no pé dos pilares; cidade alta além dos muros (26 construções). Texturas Poly Haven novas: `ashlar`, `trim`, `rock` (`fetch_polyhaven.py ashlar,trim,rock` mescla). Janelas: marcadores `WIN_*` no plano do vidro → `VillageLights` cola a janela acesa recortada em arco (`WindowGlow._Shape`). **Luz da cidade** (`CityLight` + `Campanula/CityLit` nos materiais CMP_* + `Hidden/Aren/CityLightGround` no chão): cada janela/lanterna/tocha vira uma mancha de luz num mapa visto de cima — a pedra brilha quente sem luz em tempo real. Céu mais claro (azul-marinho com nuvens) e contraste 0,2 para as agulhas virarem silhueta. Câmera: a layer Ledge saiu da colisão da câmera (bordas invisíveis das sacadas puxavam a câmera). TERRENO: `BaseHeight` agora 18 (o cânion desce abaixo de −2). **Não use `StaticBatchingUtility.Combine` no builder**: as malhas iam para dentro do `.unity` e a cena passava de 100 MB (limite do GitHub); o static batching é feito no build. Voo de câmera: `-eda-map-video` / `-eda-map-audio` |
| **Campânula gótica — passe de qualidade (2026-10-05 tarde)** | `kit_gothic.py` (DETAIL, LODs, `bake_ao`/`build_atlas`), `Campanula/CityLit`, `CampanulaImport` | "Tirar do low poly": casas e torres com pedras de cunhal, chanfro do soco, cintas com pingadeira, cachorrada sob a cornija, pingadeira sobre os arcos, rendilhado (óculo), portas com arquivoltas e colunelos + lanterna de parede (marcador `LAMP_` = luz da rua), trapeiras com janela, rosáceas nas empenas largas (janela da noite redonda: `_Shape.w`), crista de ferro nas cumeeiras, crochês nas agulhas; penedos com ~900 triângulos. **LOD**: cada casa/torre tem `<nome>_LOD1` (a versão simples); o `CampanulaBuilder.Place` monta o `LODGroup` (troca a ~20% da altura da tela, ~70 m) e tira os marcadores do LOD1. **Oclusão de ambiente assada no Blender (Cycles)**: 2º UV por modelo em atlas (`ao_atlas.png` 4096 perto, `ao_atlas_far.png` 2048 para os LOD1; u+2 / u+4 marcam o atlas, os modelos sem isso têm u<1 e ficam sem), `generateSecondaryUV` desligado nesses modelos. `--no-ao` pula o cozimento (~15 min). **Relevo**: mapas de altura do Poly Haven (`_height`, paralaxe no CityLit) e albedo/normal em 2048 para pedra/telhado/rocha (`fetch_polyhaven.py <mats> --2k`). Bloom barato também na Média à noite |
| **Mundo em alta (2026-10-05 noite): terreno, capim/trigo 3D, árvores, água, vitrais, nuvens** | `Campanula/Terrain`, `Campanula.GrassField` + `Hidden/Campanula/GrassBlades`, `kit_trees.py` + `leaf_atlas.py` + `Campanula/LeafCards`, `Campanula/Water`, `WindowGlow.Stained`, `cloud_impostors.py` + `NightClouds` + `Hidden/Campanula/CloudImpostor` | **Terreno** (`Campanula/Terrain`, `materialTemplate`): sem basemap (antes, além de 70 m, uma textura de 256 px), splat misturado pela altura das fotos, capim com duas fotos (perto `gnd_near` 3 m, prado de drone `gnd_meadow` 30 m), manchas secas em grande escala (`macro_noise.png`), rocha triplanar nas encostas (`gnd_rock`), normal map em tudo e a luz da cidade no próprio pixel (a malha `CityLightGround` não é mais criada). As texturas dividem um amostrador (limite de 16 por passada). Heightmap 513, pixel error 4. **Relevo**: `Campanula.Relief.Hills` (cristas e ravinas; o builder e a `FarLands` usam a mesma função) com o **vale do riacho** (a faixa de água subia o morro e aparecia em pedaços: eram os "retângulos rosados"); campos com borda irregular e fora dos morros. **Capim/trigo**: o terreno não desenha mais os detalhes (`drawTreesAndFoliage = false`); `GrassField` lê as camadas de detalhe e desenha touceiras 3D geradas por código (28/14/6 folhas; trigo com espiguetas e arista) com GPU instancing por pedaços de 10 m, LOD pela distância, vento, jogador abrindo caminho, manchas secas iguais às do terreno, sumiço gradual; distância/densidade continuam em `terrain.detailObjectDistance/Density` (GameSettings/AdaptivePerformance). **Árvores** v2 (`kit_trees.py`): galhos recursivos + cartões com o atlas de folhas renderizado no Blender (`leaf_atlas.png`/`_normal`, carvalho e pinheiro), normais esféricas, `_LOD1` automático. **Água** v2: ondas em 2 camadas (`water_normal.png`), Fresnel, céu noturno/lua/Fenda refletidos (`_NightOn` global), luzes da cidade tremendo, espuma nas margens (UV da faixa). **Vitrais**: `_Shape.w` 2 (arco) / 3 (rosácea) — rosáceas e janelas das torres/campanário, sempre acesos, poças de luz colorida no chão (CityLight). **Nuvens volumétricas**: 4 nuvens renderizadas como volume no Cycles com luz de 6 direções (`clouds_a/b.png`), 26 cartões no céu à noite, iluminados por lua, Fenda, cidade (por baixo) e impacto. Colisão: auditoria (`Tools/cli/cs/collision_audit.cs`) — só os arbustos ficam sem colisor (de propósito); paredes invisíveis na borda do mapa (`Limites do mapa`, layer Ignore Raycast); pedras do riacho e entulhos com colisor |
| **Rochas e madeira esculpidas (kit da natureza, 2026-10-06)** | `ArtSource/Campanula/scripts/kit_nature.py` → `Assets/Campanula/Models/Nature_*.fbx` (+ `_LOD1`), atlas `Assets/Campanula/Textures/nature_{a,b,c}_{normal,mask}.png`; shader `Campanula/NatureRock`; `CampanulaMaterials.NatureRocks()` (menu Campanula/Setup/Rochas da natureza, `Tools/cli/cs/rocks_setup.cs`); `CampanulaBuilder.BuildRocks` + `Rock()` | 21 peças MODELADAS POR CÓDIGO como num pipeline de fotogrametria: escultura em alta (40–160 mil tri: elipsoide ∩ planos de fratura com mínimo suave, estratos com ressaltos, juntas de Voronoi com degrau por bloco, fissuras, ruído com cristas) → baixa = remesh por voxels suavizado + dizimação (0,5–3,2 mil tri, a parte enterrada cortada) → UV com poucas ilhas grandes → Cycles ASSA normal (tangente), oclusão com o chão e máscara (R oclusão, G arestas/pointiness, B fendas, A tom do estrato; na madeira A = cerne das pontas). Peças: `Outcrop_A/B/C` (afloramento em camadas, "tor" de lajes de granito, crista inclinada), `Boulder_A–D`, `Slab_A/B`, `Stones_A/B`, `Scree_A`, `Pebbles_A` (seixos do rio), `Rubble_A/B` (cantaria caída), `Cliff_A/B` (paredões do cânion: costas planas em y=0 que entram na parede de terra, frente = +Z do contêiner), `Log_A/B` e `Stump_A/B` (casca pelo 2º UV em metros, veio ao longo do tronco). Origem de cada modelo = linha do chão. Shader: relevo assado + foto do Poly Haven (`lichen_rock`, `mossy_rock`, `bark_willow_02`, CC0) em projeção TRIPLANAR no mundo (madeira: pelo UV2 com base tangente das derivadas), musgo onde olha para o céu, terra na base/fendas, variação por rocha, CityLight à noite; sem o relevo da foto além de 70 m. Importação: `Nature_*` com normais IMPORTADAS (o normal assado depende delas) e sem UV2 gerado. Colocação (`BuildRocks`, roda depois de `BuildNature`): afloramentos em faixas ao longo da curva de nível nos morros (mais nas encostas íngremes) com cascalho/penedos rolados abaixo, penedos soltos nos morros e prados, montes de pedra nas bordas dos campos, pedras/lajes na beira da estrada sul (fora dos obstáculos do tutorial e da abertura), entulho no pé das muralhas, penedos e seixos no fundo do cânion, troncos/tocos perto das árvores; `RockKeepOut` protege vila, estrada, arena do chefe, ponte, aqueduto e cidade alta; `RockFree` testa colisores já construídos. `Rock()` assenta pela encosta (plano inclinado pelo `follow` + o quanto o chão real fica abaixo dele no contorno) e registra a pegada: `ClearGrassUnderRocks` tira capim/trigo de baixo das pedras (senão o capim 3D atravessa a rocha). Cânion: `BuildGorge` usa `Cliff_A/B` em duas fileiras (a de baixo nasce do fundo). Camadas: pedras soltas/entulho/lajes/tocos em Detail (70 m), penedos e troncos em Vegetation (180 m), afloramentos e paredões sem corte (têm LOD1). Rodar o Blender: `blender -b --factory-startup --python ArtSource/Campanula/scripts/kit_nature.py [-- Nome1,Nome2] [--preview]` (~25 min tudo; o cache dos ladrilhos assados fica em `ArtSource/Campanula/nature/`, fora do git — num clone novo rode SEM nomes para remontar o atlas inteiro). Vistas: `Tools/cli/cs/rock_views.cs` (alturas relativas ao chão), `rock_top.cs` |
| **Tela de carregamento** | `Assets/Aren/Resources/UI/Loading/loading_fenda.jpg` (recorte do storyboard do autor, ampliado 2x) | `LoadingScreen` (faixa de cinema com zoom lento, brasas, frases da lore, barra dourada). `GameFlow.Start` monta tudo por etapas por trás dela (noite, sino, abertura, aquecimento de shaders, banco de sons). Origem em `ArtSource/Loading/PROVENANCE.md` |
| **A Corrupção na rua do mercado** | `Assets/Aren/Scripts/World/VillageCorruption.cs` + `GameFlow.CorruptionScene` | Seis aldeões na rua (dois com castiçal); ao passar o arco do portão (z > −36,5, só no caminho normal da história — `DebugJump` chama `Cancel()`), cena de ~11 s com a câmera de cinema: fios desafinados descem da Fenda (LineRenderer vibrando), dois são tomados e viram os Ecos da luta (troca no mesmo quadro, `EnemyBase.quietSpawn`, `Encounter.ForceStart`/`firstWaveYaw`), dois desintegram em cinza e brasa (o grito vira coro desafinado), dois fogem. `Warmup()` no carregamento evita a travada do primeiro quadro |
| Modelos antigos do Aren e do cervo | fornecidos pelo usuário (Meshy/Tripo), rigados por script em `ArtSource/` | — |

Pacotes originais em `~/Downloads`: `Fantasy Props MegaKit[Standard]`, `400 Sounds Pack`,
`SBS - Seamless Space Backgrounds - Small 512x512`, `Universal Animation Library 2[Standard]`.
Ainda **não usados** e disponíveis (confira a licença antes): `Dark VFX 01 - 02`, `StatusFXFree`,
`Sword Slashes.zip`, `DarkAgesUi_v1.0.zip`, `Soulslike_UI_Kit.zip`.

Documentos de design do usuário (`~/Downloads`): `ECOS_DE_ACORDIA_CLAUDE_MASTER_ORCHESTRATOR.md`,
`ECOS_DE_ACORDIA_FREEFLOW_COMBAT_ARKHAM_INSPIRED.md`, `ECOS_DE_ACORDIA_AREN_PROFESSIONAL_RIG_PROMPT.md`,
`PROMPT_REMAKE_ECOS_DE_ACORDIA_CLAUDE_CODE.md`, `Ecos_de_Acordia_Biblia_de_Lore.docx`,
`ataques_flauta_ecos_de_acordia.txt`, `HUD_v2_Revisao_de_Experiencia_e_System_Design.docx`,
`Guia Visual do HUD de Ecos e Ressonância.png`. (Não abrir `documentacao_completa_do_jogo.md` — é do projeto de cirurgia.)

---

## 7. Armadilhas conhecidas (aprendidas na prática)

- **Notebook com a CPU travada em 400 MHz** (visto em 2026-10-02: `grep MHz /proc/cpuinfo`
  mostra 400 em todos os núcleos, temperatura baixa, carregando pela USB-C). Dell faz isso quando
  o carregador não é reconhecido/fraco. Benchmarks nessas condições NÃO valem — confira a
  frequência antes de medir e compare só A/B na mesma condição. Não mexa em configuração do
  sistema para "destravar"; avise o usuário.
- **Áudio inteiro mudo por NaN**: um único NaN numa fonte envenena a mistura para sempre. Os
  tambores da música eram gerados como NaN (filtro `BandPassSweep` instável acima de ~6 kHz com
  q=2) e calavam o jogo quando o combate subia a intensidade. Hoje o filtro limita a frequência
  e `AudioRunner.Make` zera NaN/Inf. Para conferir: `Tools/cli/cs/synth_nan.cs` (gera o banco no
  editor e procura NaN) e `-eda-diag` (nível real da saída).
- **Build "Succeeded" sem compilar**: o pedido de build é recusado enquanto o editor importa/
  compila, e o status mostrava o build ANTERIOR. `build_player.sh` agora confere o `buildId`. Se
  desconfiar, compare a data de `Builds/.../Managed/Assembly-CSharp.dll`.
- **Desempenho adaptativo** (`AdaptivePerformance`): abaixo de ~42 FPS por 2 s reduz a escala 3D (até 0,6) e o capim; vale no jogo e nas cenas, desligado no benchmark e nas gravações (`-eda-intro*`, `-eda-corruption*`).
- **Câmera dentro do personagem** = "jogador sumindo": o colisor da Cinemachine podia puxar a
  câmera até 0.3 m do ombro. `ArenThirdPersonSetup.Tune` fixa distância mínima 1.1 m e raio 0.22.
- Pré-visualizar personagem fora do Play: `SkinnedMeshRenderer.forceMatrixRecalculationPerRender
  = true`, senão todas as poses renderizam iguais (`Tools/cli/cs/player_poses.cs`).
- Blender: `primitive_ico_sphere_add(subdivisions=1)` é o icosaedro puro (20 faces); 2 = 80 faces.

- **UAL2 vira 180°**: a orientação "Original" da raiz dos clipes aponta para trás nos avatares
  do projeto. Sem `rotationOffset = 180` (no `ArenModelPostprocessor`, que sobrescreve o
  importer a cada reimport) os Ecos e o cervo andavam/atacavam de costas e o golpe do Aren
  começava virado. Se reimportar a UAL2, confira com `Tools/cli/cs/anim_facing_rt.cs`
  (idle deve dar ~+17°, não ~−163°).
- **Golpes encadeados da UAL2 giram o tronco** (Sword_Regular_A termina "enrolado" de
  costas, B desenrola, C é um giro de 360°). `TorsoFacingLock` (ordem 150, depois da animação)
  gira o esqueleto em torno do eixo vertical para cortar a torção acima de 30°. No Aren só vale
  em estados com tag `Combat` (exceto a esquiva); nos inimigos sempre (desliga deitado).
- **Escalada do DPS**: borda detectada entre 1.57 e 2.47 m acima dos pés; saltos entre bordas
  precisam do grafo `HandlePointConnection` (o builder chama `ConnectLedges`, alcance ~1.5 m);
  o `forward` da borda aponta **para dentro** da parede; pedras salientes ~15 cm; subir no topo
  precisa de espaço livre acima (lábio largo, sem ameias no ponto de saída).
- **Vault**: Espaço muito cedo virava pulo — `anticipationTime 0.5` e `contextualBufferWindow 0.6`.
- **Sombras**: tempo real custa ~10 FPS fixos no UHD 620 (pré-passe de profundidade), mesmo
  com distância curta. Baixa/Média usam `BlobShadow` nos personagens; Alta liga as completas.
  Lightmap: o bake com denoiser Auto **crasha** (exit 11); o Gaussian ficou manchado → descartado.
- `RenderScaler`: a câmera 3D desenha num RT menor (80% na Média) e a UI fica nativa.
- Camadas de distância: Detail (11) some a 70 m, Vegetation (12) a 180 m. Props do kit vão em Detail.
- Static batching combina tudo em `Static/`; objetos que precisam da própria origem (chamas
  billboard) ficam em `Gameplay/` e o shader tem `DisableBatching`.
- `glob` do Python com `[Standard]` no caminho: use `glob.escape`.
- O Animator avaliado fora do Play (scripts de medição) dispara `NullReferenceException` do
  `MovementCharacterController.OnAnimatorIK` no Console — ruído da ferramenta, não do jogo.
- Erros "Token Exchange failed"/Unity Connect no log são inofensivos.
- As preferências salvas do usuário podem estar em Alta/100% (~23 FPS). O padrão é Média/80%.
- **Cursor travado no X11 com a sessão bloqueada** (madrugada, tela de bloqueio): `Cursor.lockState = Locked` demora ~5 s e congela a troca menu → jogo. Só acontece sem ninguém usando o PC; nas gravações de teste o cursor não é travado (`GameSettings.NoCursorLock`).
- **Shaders compilando no primeiro uso**: o menu mostra a arte (a câmera 3D não desenha), então a vila e os shaders da abertura compilavam no primeiro quadro do jogo. `CinematicRig.Warmup` desenha tudo uma vez no carregamento (~0,9 s).
- **Prólogo em halftone é CPU**: o tempo dele avança com `dt` limitado a 50 ms; abaixo de 20 FPS ele fica mais lento em tempo real. Não deixe síntese pesada rodando junto (os sons da noite são pré-gerados por isso).
- **ElevenLabs pelo Chrome (automação)**: o botão de download do site não salva nada; os áudios vêm embutidos (base64, Ogg Opus) na resposta de `/sound-generation` — dá para interceptar o `fetch` e salvar UM arquivo por aba (o Chrome bloqueia o 2º download automático da mesma aba; não aceite o pedido de permissão — abra outra aba). Abas em segundo plano congelam `setTimeout`: use laços com `await Promise.resolve()` e espere de fora (sleep no shell). O controle de duração só existe com o popover aberto (clique real).
- **FBX do Blender vem girado** (eixo "para cima" local não é Y): shaders que usam altura do corpo devem medir `wp - origem do objeto` no mundo (ver `ArenVillager.shader`). Importar com `materialImportMode = None` apaga os NOMES dos slots — os aldeões usam `ImportStandard` só para saber qual peça é qual.
- **Variantes de luz novas travam o primeiro quadro**: uma luz pontual em modo vértice (ou uma luz nova perto de um material nunca desenhado com luz) compila variantes na hora (~140 ms). Aqueça no carregamento (`CinematicRig.Warmup`, `VillageCorruption.Warmup`).
- **Gravação de áudio de teste (`AudioTap`) pega o sinal na ordem dos componentes do ouvinte**: o limitador precisa estar antes do gravador (por isso o `SoftLimiter` é posto na câmera de cinema no `Setup`).
- **Medir com a máquina quente**: em 2026-10-05 (madrugada, após horas compilando) o benchmark deu ~30 FPS tanto com a noite quanto com o pôr do sol (`-eda-day`): a máquina estava lenta, não o jogo. Compare A/B na mesma sessão.

---

- **glTF (Tripo/Meshy) vem com vértices duplicados nas costuras de UV**: solde (`remove_doubles` 1e-5) ANTES de reduzir,
  senão o decimate abre frestas nas costuras e o bone heat/inundações param em cada ilha (ver `suss_01_prep.py`).
- **Compilar C# sem o editor** (quando outra sessão está com ele): o Roslyn da Unity + o `.rsp` do Bee —
  `dotnet Data/DotNetSdkRoslyn/csc.dll /noconfig @Library/Bee/artifacts/<dag>/Assembly-CSharp.rsp` (troque `-out`, acrescente os
  `.cs` novos; para o Editor, aponte a referência do Assembly-CSharp para a saída anterior). Pega erro antes de entrar na vez.
- **Várias sessões do Claude no mesmo projeto**: a pasta de trabalho e o editor são compartilhados. Combine a vez do editor
  por mensagem, faça `git add` só dos seus caminhos e `git pull` antes do push.

## 8. Estado atual e próximos passos

Commits recentes (hoje na `main`): orientação dos ataques corrigida (UAL2 180° +
TorsoFacingLock + esquiva lateral), assets novos (props, sons, nebulosas), efeitos com o céu da
Fenda, Campanula decorada. Detalhes: `AREN_REMAKE_EXECUTION_LOG.md`. Pendências:
`AREN_REMAKE_BACKLOG.md`.

Feito em 2026-10-02 (noite): áudio mudo corrigido (NaN), jogador novo "red assassin",
câmera nova, abertura da lore, pós-processamento leve, árvores com volume, capim/trigo,
texturas 1024. Veja `AREN_REMAKE_EXECUTION_LOG.md`.

Sugestões em ordem de impacto:
1. **Animações de flauta/combate de verdade** (hoje são golpes de espada CC0 com a flauta na
   mão). Mixamo (o usuário baixa) ou animação própria; trocar os clipes em `ArenCombatSetup.States`
   e recalcular os tempos de contato (velocidade da mão direita).
2. **Câmera de combate**: no mercado estreito a câmera bate nas paredes/fica atrás de objetos
   (visto nas capturas do teste). Enquadrar alvo + Aren, afastar em luta, evitar colisão com props.
3. **Mais conteúdo**: 2º tipo de inimigo (à distância), rotas de telhado no mercado, um
   interior (taverna) usando os props de mobília do kit (cama, estante, velas, caldeirão).
4. **Rig do cervo** por artista (os chifres esticam em poses extremas) e animações próprias.
5. **Gamepad** testado em hardware real; remapeamento de teclas.
6. Usar os pacotes ainda livres (Dark VFX, StatusFX, UI kits) depois de checar licença.
7. Itens antigos do backlog (shimmy em borda larga, agarrar borda pelo pulo livre etc.).

Sempre: compilar → (regenerar cena) → build → `-eda-test` → benchmark → commit local.

---

## 9. Mundo de Elyndra (os 13 reinos)

Tudo em `Assets/World/` (namespace `Elyndra.World` em runtime, `Elyndra.WorldEditor` no editor). Campânula
(`Assets/Campanula`) **não é tocada**: ela ganha um portão leste em runtime (`CampanulaLink`) para Valtéria.

```
Assets/World/
  Systems/World/WorldCanon.cs     ← FONTE ÚNICA do cânone: 13 reinos (posição em km), 7 Notas (regra +
                                    Contramotivo), 7 Relíquias (Custódio, preço), 13 Vórtices (Motivo/Regra/
                                    Núcleo VIVO/Contramotivo), 13 masmorras, 21 rotas (com condição de abertura)
  Systems/WorldState/             ← estado salvo (elyndra_world.json): fase de Valtéria, flags, Notas, relíquias,
                                    Vórtices quebrados, Corrupção por reino. Condições: "nota:do", "flag:x",
                                    "!flag:x", "fase:VorticeAtivo", "fase<:X", "reliquias:2", "vortice:id", "nunca", "a&b"
  Systems/Regions/                ← RegionRoot (perfil visual + Corrupção + imagem/pós do reino), RegionTravel
                                    (carregamento assíncrono com fade), RegionGate (portões), RegionFlow (jogador,
                                    HUD, pausa, morte, mapa com M), StreamingCell
  Systems/Spawning/               ← EnemyCatalog (bestiário; humanos com corpo de aldeão provisório), EnemySpawnZone
  Systems/Vortex/, Bosses/, Dungeons/, POI/ (TownsfolkSpot = moradores), Map/, Checkpoints/
  Editor/                         ← construtor procedural: RegionRecipes (receita de cada reino), RegionBuilder,
                                    Settlements, Landmarks, Gameplay, DungeonBuilder, WorldMapBuilder, ElyndraBuild
  Shaders/                        ← WorldTriplanar, WorldSky, FarRange (serras em camadas), Crystal, Glow, GlassSea
  Scenes/Regions/*.unity, Scenes/Dungeons/D_*.unity, Scenes/WorldMap.unity   (geradas — não editar à mão)
```

- **Gerar cenas** (editor aberto): `Tools/cli/world_build.sh prepare Valteria Velaria … dungeons map settings`
  (ou `all`). `settings` grava as 28 cenas no Build Settings (Campanula primeiro).
- **Ver as cenas sem Play**: `Tools/cli/world_views.sh all` → `Tools/cli/shots/world/<Cena>_NN.png`.
- **Checar o C# sem o editor** (quando outra sessão está usando o Unity): `Tools/cli/typecheck.sh`.
- **Teste no executável**: `-eda-world-test` (Aren anda a rota de Valtéria a pé, portões trancados/abertos,
  Velária ida e volta, masmorra, mapa, Campânula ida e volta, e carrega os 13 reinos + 13 masmorras medindo FPS)
  → `~/EcosBench/world.txt` + `world_*.png`. `-eda-world-quick` pula a volta pelos 26. Outros: `-eda-region=Velaria`
  (começa direto num reino), `-eda-map-free`, `-eda-world-reset`, `-eda-world-phase=X`, `-eda-world-flag=x`,
  `-eda-world-note=do`.
- **Portões**: rota entre reinos = `rota_<Cena>` nos dois lados; masmorra = `masmorra` ↔ `entrada`/`saida`;
  Valtéria `portao_campanula` ↔ Campânula `valteria`. O portão leste de Campânula abre com `flag:campanula_cervo`
  (dada ao vencer o Cervo); as rotas de Valtéria abrem com `nota:do`.
- **Placeholders**: tudo provisório tem `PlaceholderTag` dizendo o que deve substituí-lo (menu
  *Elyndra/Relatório de placeholders*). Chefes das Notas e minibosses: arenas prontas (entrada, saída, câmeras,
  névoa de luta, ganchos `onArenaEnter/onBossDefeated`), corpos ainda não implementados.
- **Corrupção**: só seres vivos são hospedeiros; um Vórtice é um hospedeiro vivo em Ruptura cuja regra se
  espalha pelo lugar (o "Núcleo" é sempre alguém). Animais do cânone ficam em `RegionDef.animals` e no
  bestiário com `human = false` até ganharem corpo (desligar `EnemyCatalog.HumansOnly`).

