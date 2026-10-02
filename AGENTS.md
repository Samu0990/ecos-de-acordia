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
(DPS, MIT). Projeto: `~/Unity/ParkourLab`, branch **`remake-aren`** (`main` = backup antigo).

---

## 2. Regras do dono do projeto (obrigatórias)

1. **Fale em português (PT-BR)** com o usuário. Ele escreve informal, com erros de digitação.
2. **Git: commit local a cada etapa, NUNCA `git push`** nem PR ("n quero salvar no git hub").
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
      UI/       UIKit, ArenHUD, GameMenus, GameSettings (PlayerPrefs eda_*), UIWaveform
      World/    GameFlow (roteiro, encontros, pausa, morte, checkpoints, DebugJump),
                Encounter (ondas), BellRinger, RenderScaler, DemoBenchmark, DemoAutoTest
      Debug/    ArenTestProbe (robô de input virtual: teclado/mouse falsos dentro do jogo)
    Editor/     ArenCombatSetup (estados de combate no Animator do DPS + AttackData),
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
    Shaders/    SunsetSky, Rift, Water, Trim (props do kit), Flame (tochas)
    Models/     kit da vila exportado do Blender (FBX)
    ThirdParty/FantasyProps/  Quaternius Fantasy Props MegaKit (CC0)
    Scenes/Campanula.unity    ÚNICA cena do build
  Dynamic Parkour System/     DPS (MIT) com alterações mínimas comentadas; Player.prefab
ArtSource/
  Campanula/scripts/  kit_lib.py, kit_buildings.py, kit_props.py, preview.py (Blender headless)
  Aren/scripts/       rig do Aren (aren_01..05)
  Deer/deer_rig.py    rig do cervo
Tools/
  cli/   ue, uej, rc, cons, play, cap, capui, lib.sh, bench.sh, cs/*.cs (scripts de editor)
  texgen/ gen_fx.py, gen_world.py, gen_ui.py (texturas procedurais)
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
| Modelos do Aren e do cervo | fornecidos pelo usuário (Meshy etc.), rigados por script em `ArtSource/` | — |

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

Commits recentes (branch `remake-aren`): orientação dos ataques corrigida (UAL2 180° +
TorsoFacingLock + esquiva lateral), assets novos (props, sons, nebulosas), efeitos com o céu da
Fenda, Campanula decorada. Detalhes: `AREN_REMAKE_EXECUTION_LOG.md`. Pendências:
`AREN_REMAKE_BACKLOG.md`.

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
