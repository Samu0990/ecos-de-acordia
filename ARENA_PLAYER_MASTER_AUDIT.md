# ARENA PLAYER MASTER AUDIT — Ecos de Acordia / Aren

> Auditoria consolidada pedida pelo `ECOS_DE_ACORDIA_CLAUDE_MASTER_ORCHESTRATOR.md`.
> Substitui três diagnósticos separados (PLAYER_RIG_AUDIT / PLAYER_FREEFLOW_AUDIT /
> PROJECT_AUDIT): as seções "Rig" e "Combat" abaixo SÃO esses audits.
> Data: 2026-09-29. Base: commit `c4c085a` (branch `main`).

## Documentos usados e conflitos de documentação

| Documento | Situação |
|---|---|
| `ECOS_DE_ACORDIA_CLAUDE_MASTER_ORCHESTRATOR.md` | lido — define ordem e precedência |
| `ECOS_DE_ACORDIA_PLAYER_REMAKE_MASTER_PROMPT.md` | **NÃO EXISTE** em `~/Downloads`. Uso `PROMPT_REMAKE_ECOS_DE_ACORDIA_CLAUDE_CODE.md` no lugar dele (cobre exatamente os mesmos tópicos: locomoção, input, jump, parkour, dodge, VFX, câmera, debug, performance). `(1).md` é cópia idêntica. |
| `ECOS_DE_ACORDIA_AREN_PROFESSIONAL_RIG_PROMPT.md` | lido — autoridade de rig |
| `ECOS_DE_ACORDIA_FREEFLOW_COMBAT_ARKHAM_INSPIRED.md` | lido — autoridade de combate |
| `Ecos_de_Acordia_Biblia_de_Lore.docx` | lido (contexto de identidade) |

Conflitos encontrados:
1. **Instrumento**: a Bíblia de Lore diz que o instrumento principal da campanha é o
   *Alaúde Quebrado*; os prompts de remake dizem "o instrumento principal **para esta
   fase** será FLAUTA". Sigo os prompts (são a spec desta fase). A flauta é
   implementada como prop trocável (socket + anchors), então trocar por alaúde depois
   não exige refazer o combate.
2. **Exemplos em Godot/2D** (`flip_x`, `queue_free`, `.gd`, `CharacterBody2D`,
   "Verifique compatibilidade com Godot"): o projeto real é **Unity 3D**. Adaptei tudo.
3. **"Capa/manto"**: o modelo real do Aren **não tem capa**. Tem **capuz** + **tabardo**
   (aba frontal longa entre as pernas, aba traseira) + corrente pendurada no quadril.
   Os requisitos de "cape" do rig viram requisitos para as abas do tabardo.
4. **Lore diz** "Aren começa visualmente modesto". O modelo atual (vermelho/preto,
   máscara branca) é o visual que você escolheu — preservo (regra 61 do Orchestrator).

Arquivos em `~/Downloads` que **não** são deste projeto e foram ignorados de propósito:
`Meshy_models_*/mao1.glb` e `mao2.glb` (luvas cirúrgicas azuis com manga de avental — do
projeto de cirurgia VR) e `documentacao_completa_do_jogo.md` (GDD do VR Surgery). Nada
relacionado a cirurgia é tocado.

# PROJECT

- **Engine:** Unity 6000.3.22f1
- **Version control:** git local (`~/Unity/ParkourLab`), sem remoto — commits só locais.
- **Dimension:** 3D, câmera em terceira pessoa (Cinemachine FreeLook 2.10.7).
- **Render pipeline:** Built-in (sem URP/HDRP) → **sem VFX Graph / Shader Graph**. VFX
  serão Shuriken (Particle System) + shaders HLSL próprios. Color space: Gamma.
- **Física:** Rigidbody + CapsuleCollider (não CharacterController).
- **Input:** Input System 1.20 (classe gerada `PlayerControls.cs`, teclado + gamepad).
- **Player Scene:** `Assets/Dynamic Parkour System/Scenes/Testing.unity`,
  prefab `Prefabs/Player.prefab`.
- **Hardware alvo:** Dell Latitude 3490 (GPU integrada Intel) — meta ≥45 FPS; hoje ~60.

# CURRENT PLAYER

## Architecture
Dynamic Parkour System (MIT), componentes no mesmo GameObject:
`ThirdPersonController` (orquestra flags: isGrounded, isJumping, isVaulting, dummy,
allowMovement) → `InputCharacterController`, `MovementCharacterController` (Rigidbody,
feet IK, auto-step, boundary check), `AnimationCharacterController` (parâmetros do
Animator, root motion por tag), `DetectionCharacterController` (raycasts),
`VaultingController` (lista de `VaultAction`s mutuamente exclusivas via `isVaulting`),
`ClimbController` (795 linhas, máquina de estados de escalada),
`JumpPredictionController` (parábola para pontos pré-definidos), `CameraController`.
Não é monolito, mas estado é compartilhado por flags booleanas públicas — frágil.

## Movement
| Item | Estado | Classificação |
|---|---|---|
| Walk 2.4 m/s / Run 4.5 m/s | calibrado com `averageSpeed` (Etapa 2) | KEEP |
| Aceleração (lerp fator 8, 90% em ~0.29 s) | medido (Etapa B.1) | KEEP |
| Desaceleração (~0.1 s) | medido | KEEP |
| Rotação (SmoothDampAngle 0.1 s) | ok | KEEP |
| **Pivot/skid** (inversão brusca) | não existe — só gira suavemente no lugar correndo | REBUILD (novo) |
| **Pulo livre** | **NÃO EXISTE.** Espaço só dispara ações contextuais (vault, reach, borda, ponto previsto). Sem pulo em chão plano, sem altura variável, sem apex | REBUILD (novo) |
| Queda / pouso | 1 animação de pouso, sem intensidade por altura | FIX (pouso por intensidade) |
| Fast fall | não existe | novo (baixa prioridade) |
| Coyote / buffer | existem só para agarrar borda (Etapa C) | REFACTOR (generalizar p/ pulo) |
| Lean procedural | não existe (pendência B.2) | novo |
| Air control | `rb.position += forward*walkSpeed` no ar (ignora direção do input) | FIX |
| Slopes / auto-step | funcionam | KEEP |
| Roll / dodge | **não existe** | REBUILD (novo) |

## Parkour
Vault (W+Shift+Espaço), slide (W+Shift+C), escalar/pendurar, pulo borda-a-borda,
shimmy, drop, pulo previsto em postes: **funcionam e foram testados** nas Etapas A–C.
**KEEP.** Wall slide / wall jump não existem → backlog (o level design atual não tem
paredes para isso; o prompt diz "caso seja compatível").

## Animation
- Animator Controller do DPS, 35 clipes Mixamo (Erika) + blend trees calibradas.
- Transições de 0 s já corrigidas (Etapas 2, B.2).
- **Sem** estados: pivot, jump start/rise/apex, land leve/médio/pesado, roll/dodge,
  qualquer ataque, combat idle, hit reaction.
- Fontes de animação disponíveis (licença verificada):
  - DPS/Mixamo (já no projeto).
  - **Universal Animation Library 2 Standard (Quaternius, CC0)** — tem: Sword_Regular_A/B/C
    (+ recoveries), Sword_Regular_Combo, Sword_Heavy_Combo, Sword_Dash, Sword_Block,
    Melee_Hook, Hit_Knockback, Shield_Dash, NinjaJump Start/Loop/Land, Slide, ClimbUp.
    Esqueleto UE-style de 65 ossos com dedos → retarget Humanoid.
  - Mixamo (só com login seu — ponto de parada se eu precisar de clipes extras).
  - **Proibido:** Game Animation Sample da Unreal. `RunJumpComponent/*.uasset` também é
    Unreal (formato inutilizável no Unity) → ignorado.
- **Honestidade:** não existe no material disponível nenhuma animação de *flauta*.
  Os ataques vão reaproveitar golpes de espada CC0 (a flauta empunhada como bastão de
  ressonância) + camadas procedurais (torso, flauta à boca por IK). Isso é a limitação
  mais séria do projeto e está documentada no plano.

## Rig  (= PLAYER_RIG_AUDIT)
- **Modelo do Aren:** `~/Unity/Personagem-para-Mixamo.zip → personagem.fbx` (Tripo),
  confere com a arte conceitual (capuz vermelho, máscara branca, tabardo, cintos,
  botas com fivelas). 25.888 vértices / 48.862 triângulos, 1 material, textura 4K.
- **Não tem rig nenhum.** Sem ossos, sem pesos, sem shape keys.
- Altura **1.0 m** (precisa escalar para ~1.8 m). Já olha para −Y (convenção Blender). Braços para baixo (A-pose fechada).
- **169 peças soltas** (não é uma casca única): cabeça+capuz+ombros, antebraço+luva
  esquerda/direita, botas, tronco, tabardo e dezenas de fivelas/alças separadas.
  Bom para o skin (dá pra isolar braço do tronco por peça), mas 3.326 arestas
  não-manifold → *bone heat* do Blender provavelmente falha em várias peças; terei que
  escrever pesos por regra + transferência para acessórios.
- Sem flauta no modelo → prop da flauta tem que ser modelado.
- Sem capa → secundário = capuz + aba frontal + aba traseira + corrente.
- Mãos: luvas com dedos modelados (confirmar separação no rig).
- Scripts que dependem de ossos: `MovementCharacterController` (feet IK via
  `HumanBodyBones`), `ClimbController` (IK de mãos/pés), `VaultAction`s (IK de mão).
  Todos usam **Humanoid** → o esqueleto novo precisa ser Humanoid-compatível.
- **Personagem em cena hoje: Erika (placeholder Mixamo), não o Aren.**

## Combat  (= PLAYER_FREEFLOW_AUDIT)
**Não existe nenhum código de combate.** Nada de: PlayerCombat, targeting, lock-on,
hitbox/hurtbox, combo, buffer de ataque, dodge, parry, dummy, hit reaction, hitstop.
Tudo é **REBUILD** (construção nova). Vantagem: sem legado para brigar.
Risco: integração com as flags do DPS (`isVaulting`, `dummy`, `allowMovement`) —
combate precisa bloquear/ser bloqueado pelo parkour sem quebrar os estados de escalada.

## VFX
Nenhum VFX de gameplay. Só gizmos de debug do DPS. Assets recebidos:
- `StatusFXFree` (Binbun, CC0) — é **Godot** (.tscn/.gdshader). Não importa direto;
  útil só como referência de técnica (overlay/shatter). Não uso diretamente.
- `Dark VFX 1/2` — sprites **pixel-art 40×32 e 48×64**, sem arquivo de licença. Em 3D
  estilizado ficariam borrados/destoantes e a licença não está clara → não uso.
- Conclusão: VFX serão feitos por mim (Particle System + shaders HLSL + malhas de
  slash geradas por código), linguagem musical (anéis, ondas, frequência).

## Camera
Cinemachine FreeLook com damping ajustado (B.3), colisão (CinemachineCollider), FOV
dinâmico na corrida, offset na escalada. **KEEP.** Falta: impulsos (Cinemachine
Impulse existe no pacote), enquadramento de combate, hitstop.

## Audio
**Nenhum áudio no projeto** (zero .wav/.ogg). Sem footsteps, sem nota de flauta.
Plano: síntese procedural em runtime (flauta = senoides + ruído de sopro + envelope +
vibrato) — licença limpa, sem assets externos, sincronizada por eventos.

# KEEP
- Toda a base de parkour do DPS (vault, slide, climb, ledge, poles, jump prediction).
- Calibração de velocidade/aceleração/rotação/blend trees (Etapas 2, B.1).
- Correções de IK/HUD/câmera das Etapas A, B.2, B.3, C.
- Cinemachine FreeLook + collider + FOV dinâmico.
- Input System (teclado + gamepad já mapeados para locomoção).

# FIX
- Air control ignora direção do input (usa `transform.forward`).
- Pouso único sem intensidade.
- Coyote/buffer só existem para o agarrão — generalizar.
- `IsGrounded(stepHeight)` ignora o parâmetro (AUDITORIA.md item 4).

# REFACTOR
- Input: criar camada de ações de combate com buffer/histórico (sem mexer na classe
  gerada `PlayerControls.cs`).
- Estados do jogador: adicionar um "action lock" central que combate, dodge e parkour
  consultam, em vez de mais flags soltas.

# REBUILD
- Pulo livre (variável, apex, coyote, buffer), pivot/skid, pouso por intensidade, lean.
- Rig completo do Aren (Blender, headless bpy).
- Prop da flauta + sockets/anchors.
- Secundário: capuz, abas do tabardo, corrente (spring bones próprios, sem dependência).
- Dodge.
- Todo o combate Freeflow Musical.
- VFX, áudio procedural, hitstop/impulsos.
- Labs: `FreeflowCombatLab` (dummies, emissores de ameaça).

# DEPENDENCIES
```
Input actions ──► Pulo/Dodge/Combate
Rig do Aren (Blender) ──► troca Erika→Aren ──► flauta/sockets ──► animações de ataque
Humanoid retarget (UAL2 + DPS) ──► todo ataque/dodge
Dummies + emissores ──► targeting, counter, stress test
Eventos de combate (attack_started, hit_connected...) ──► VFX, áudio, câmera
```

# RISKS
1. **Animações de flauta não existem** → golpes de espada CC0 reinterpretados. Pode
   parecer "espada disfarçada", que o prompt proíbe. Mitigação: camadas procedurais
   (flauta à boca antes do golpe, torso/respiração), VFX nascendo da flauta, e deixar
   claro no relatório. Solução real exige animação feita à mão/mocap.
2. **Rig por script sem artista** — pesos por regra num mesh Tripo com 169 peças e
   não-manifold. Vou fazer stress test por render, mas qualidade "technical animator"
   de verdade num mesh gerado por IA é um teto difícil. Serei honesto no relatório.
3. **Retarget Humanoid de A-pose** — precisa de T-pose forçada no avatar; erro aqui
   deixa todos os braços tortos.
4. **Performance** na GPU integrada: GrabPass/distorção é caro; VFX com orçamento.
5. **Testes sem jogar de verdade**: testo por input simulado + screenshots + métricas.
   "Sensação" final depende do seu playtest.
6. Flags booleanas compartilhadas do DPS: combate pode destravar/estragar escalada.

# EXECUTION ORDER
Ver `AREN_REMAKE_EXECUTION_PLAN.md`. Resumo:
Fase 0 proteção → 1 input → 2 core movement → 3 forgiveness → 4 parkour (validar) →
5 rig → 6 animation foundation (Aren em cena) → 7 dodge → 8 combo de flauta →
9 targeting → 10 magnetism → 11 redirection → 12 counter/parry → 13 dodge+freeflow →
14 musical flow → 15 VFX → 16 áudio → 17 câmera/feedback → 18 Contracanto →
19 stress test → 20 quality pass.
