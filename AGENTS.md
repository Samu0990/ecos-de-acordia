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

Outros projetos na Área de trabalho (`EcosDaDiscordia*` em Godot, `EcosBattleground` em Roblox)
**não são este jogo** — não mexa neles sem o usuário pedir.

Motor: **Unity 6000.3.22f1**, Built-in Render Pipeline, Input System 1.20, Cinemachine 2.10.7
(FreeLook), UGUI 2.0 (Text legado + fontes Noto). Base de parkour: **Dynamic Parkour System**
(DPS, MIT). Projeto: `~/Unity/ParkourLab`, branch **`main`** (o `remake-aren` foi juntado na `main` em 2026-10-04); repositório privado https://github.com/Samu0990/ecos-de-acordia.

---

## 2. Regras do dono do projeto (obrigatórias)

1. **Fale em português (PT-BR)** com o usuário. Ele escreve informal, com erros de digitação.
2. **Git:** o jogo está no repositório **privado** https://github.com/Samu0990/ecos-de-acordia (branch `main`). Commit a cada etapa com mensagem em português e `git push` em seguida. Não torne o repositório público, não adicione colaboradores e não suba builds (`Builds/` fica no .gitignore).
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
                ThreatDirector (máx. 2 atacando), CounterIndicator
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
`title.txt`), `-eda-title-video` (12 s da tela inicial a 30 quadros/s em `~/EcosBench/video/f_NNNN.jpg`; MP4: `ffmpeg -framerate 30 -i ~/EcosBench/video/f_%04d.jpg -c:v libx264 -crf 20 -pix_fmt yuv420p tela.mp4`), `-eda-intro-video` (grava 30 s da abertura a 30 q/s em `~/EcosBench/intro_video/f_NNNN.jpg`), `-eda-uncapped` (sem limite de 60 FPS, para medir folga).
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

Último resultado (2026-10-02, depois dos props e efeitos novos): todos os testes passam,
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
| Capim e trigo do terreno | `Tools/texgen/gen_details.py` → `detail_grass.png`, `detail_wheat.png` | `CampanulaBuilder.BuildTerrainDetails`; distância/densidade por qualidade em `GameSettings.Apply` |
| **Tela inicial** (arte do autor, "exatamente assim") | `ArtSource/Menu/title_reference.webp` → `Assets/Aren/Resources/UI/Title/` | `~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Menu/scripts/build_title.py` (E/S pelo ImageMagick em `imgio.py`): fundo com o painel reconstruído atrás dos botões, placas apagada/vermelha e textos recortados da arte, máscara do título, brilho/névoa em pixel art e `title_layout.json` (posições em px da arte, chamas). `ArenUIImport` importa sem compressão. `TitleScreen` monta tudo em coordenadas da arte (1672x941, escala "cobrir"), shaders `Hidden/Aren/UITitle*` (pixel nítido, aditivo, névoa, reflexo). Fundo em movimento ("motion"): `ArtSource/Menu/scripts/motion_maps.py` gera `motion_a/b.png` (paralaxe por pixel com o painel em profundidade zero, máscaras da lanterna, lustre, cruz e estandarte, ar quente das velas, luz das velas no cenário) e o shader `Hidden/Aren/UITitleMotion` desloca a arte por pixel (câmera em Lissajous + zoom-base de 2,5% para a borda nunca ler fora da imagem, pêndulos, onda do pano); `UITitleRays` são os feixes de luz. `TitleMotion.Forward()` faz halos e brasas acompanharem o cenário. As animações usam `TitleClock` (tempo real que respeita `Time.captureDeltaTime`). Com a arte na tela as câmeras 3D só limpam a tela (cullingMask 0) e voltam ao sair do menu. Controles e Créditos ficam dentro de Configurações. Origem/licença: `ArtSource/Menu/PROVENANCE.md` |
| **Prólogo da abertura** (motion graphics) | código: `Assets/Aren/Scripts/World/PrologueFX.cs` | software renderer 480x270 (halftone por tamanho de ponto, glitch de fatias, queima, partículas aditivas) tocado por `CutsceneDirector` antes do voo sobre Campanula; técnicas inspiradas no BlumeApp do autor (`~/Área de trabalho/BlumeApp`) — não usar os áudios/coreografia de lá (vieram de vídeos de terceiros) |
| **Atenção: jogador = RedAssassin** | `Assets/Dynamic Parkour System/Prefabs/Player.prefab` | NÃO rode o menu `Aren/Setup/Player - Trocar Erika pelo Aren` (`ArenCharacterSetup.SwapPlayerModel`): ele recoloca o Aren antigo dentro do Player (aconteceu em 2026-10-04 e foi revertido). Para refazer o jogador use `RedAssassinSetup.All()` |
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

---

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
