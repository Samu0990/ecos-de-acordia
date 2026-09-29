# AREN REMAKE — EXECUTION PLAN

Tarefas convertidas para o código real (Unity 6 / DPS). Cada fase termina com:
Play mode + estações testadas + Console + FPS + commit local + `AREN_REMAKE_EXECUTION_LOG.md`.
Branch de trabalho: `remake-aren` (o `main` fica intacto como backup).

## Mapa de controles (novo — documentado conforme proibição 65 do prompt)

| Ação | Teclado/Mouse | Gamepad | Status |
|---|---|---|---|
| Mover | WASD | Stick esq. | existente |
| Correr | Shift (segurar) | RT | existente |
| Pular / escalar / vault | Espaço | A (South) | existente → ganha pulo livre |
| Soltar / deslizar | C | B (East) | existente |
| **Atacar (flauta)** | Botão esq. do mouse | X (West) | novo |
| **Contracanto (carregado)** | Segurar botão esq. | Segurar X | novo |
| **Counter / Parry** | Botão dir. do mouse | Y (North) | novo |
| **Dodge** | Ctrl esquerdo | RB | novo |
| **Pulso de Ressonância** | E | LB | novo |
| HUD debug / gizmos | F1 / F2 | — | existente |
| **Debug combate / VFX on-off / câmera lenta** | F3 / F4 / F5 | — | novo |

## Fase 0 — Proteção
- [ ] branch `remake-aren`; docs de controle (audit, plan, log, backlog) commitados.

## Fase 1 — Input
- Novo `Scripts/Aren/Input/ArenInput.cs`: `InputAction`s criadas em código (não edita
  o `PlayerControls.cs` gerado): Attack, Counter, Dodge, Ability, com teclado+gamepad.
- `ActionBuffer`: timestamp por ação + direção no momento do press (world-space,
  relativa à câmera), `Consume()`, janela configurável; **máx. 1 ação na fila**
  (Freeflow §16). Histórico curto (últimos 8 eventos) só para o overlay de debug.
- Direção de intenção: último vetor de movimento não-nulo com deadzone 0.2 (teclado e
  stick) — usado pelo targeting.

## Fase 2 — Core movement (refatora `ThirdPersonController`/`MovementCharacterController`)
- **Pulo livre** novo (`Scripts/Aren/Movement/ArenJump.cs`, integrado ao DPS):
  - só dispara se nenhuma `VaultAction`/`ClimbController` consumiu o Espaço naquele
    frame (as ações contextuais têm prioridade — preserva o parkour);
  - impulso vertical calculado da altura alvo (h = v²/2g), altura variável ao soltar
    cedo (multiplica v.y), apex assist (gravidade ×0.6 perto de v.y≈0), gravidade de
    queda maior (já existe `fallForce`);
  - coyote 0.1 s + buffer 0.15 s (reaproveita `CoyoteAvailable`/`JumpBuffered`);
  - animações: Jump start/air/land do DPS + UAL2 NinjaJump como alternativa.
- **Air control**: usar direção do input relativa à câmera em vez de `transform.forward`.
- **Pouso por intensidade**: medir v.y no impacto → leve (<6 m/s) / médio / pesado
  (>11 m/s) com recuperação curta proporcional, feet IK religado suavemente.
- **Pivot/skid**: ângulo entre velocidade e input > 135° correndo → micro-estado de
  ~0.12 s: desacelera forte, lean para trás, gira, reacelera. Sem clipe de pivot no
  material → procedural (lean + rotação rápida), clipe Mixamo "Running Turn 180" fica
  como melhoria opcional.
- **Lean procedural** (pendência B.2): `ArenProceduralLean` no `OnAnimatorIK`/LateUpdate
  inclinando spine/chest pela aceleração lateral/longitudinal, amortecido.
- Métricas antes/depois registradas (tempo até vmax, distância de parada, altura e
  duração do pulo).

## Fase 3 — Forgiveness
- Coyote/buffer já existem para borda → generalizar para pulo livre e vault.
- Ledge forgiveness: já há buffer; revisar raio de detecção se testes mostrarem falhas.
- Corner/head-bump correction: avaliar no teste (em 3D é menos crítico) → backlog se
  não aparecer problema.

## Fase 4 — Parkour
- Revalidar todas as estações (vault, slide, climb, hang, ledge-to-ledge, poles) depois
  das fases 1–3 (regressão). Wall slide/jump → backlog (sem level design para isso).

## Fase 5 — Rig do Aren (Blender headless, scripts versionados em `ArtSource/Aren/`)
1. Preparar malha: escala 1.8 m, origem nos pés, −Y frente, merge by distance
   conservador (não fundir peças soltas), normais.
2. Esqueleto **deform** Humanoid-compatível com nomes Mixamo (Hips, Spine, Spine1,
   Spine2, Neck, Head, Shoulder, Arm, ForeArm, Hand, dedos 3 falanges, UpLeg, Leg, Foot,
   ToeBase), juntas posicionadas por fatias da geometria (não chute).
3. Extras não-humanoides: `ForeArmTwist`/`ArmTwist` (L/R), `Hood_01..03`,
   `TabardFront_01..03`, `TabardBack_01..03`, `Chain_01..02`, `FluteSocket` (mão dir.),
   `CameraAnchor` (filho de Hips, não da cabeça), anchors de VFX (peito, mãos, pés).
4. Pesos: regras por peça (braço só em ossos de braço, bota em perna/pé, capuz em
   Head/Hood, abas em Tabard*), gradiente ao longo das cadeias, suavização Laplaciana,
   máx. 4 influências, normalizado; acessórios herdam pesos da superfície mais próxima.
5. Stress test por render: T-pose, agachamento profundo, braço acima da cabeça, braço
   cruzando o peito, torção de tronco, ledge grab, flauta na boca, pose de Contracanto.
6. Flauta: modelada em bpy (corpo, anéis, orifícios, bocal) como prop separado.
7. Export FBX Humanoid + script de import no Unity (T-pose forçada no avatar).
- Docs: `AREN_RIG_DOCUMENTATION.md`, `AREN_RIG_CHANGELOG.md`, `AREN_RIG_FINAL_REPORT.md`.

## Fase 6 — Animation foundation
- Trocar Erika→Aren no `Player.prefab` mantendo Animator Controller, scripts e IK.
- Revalidar todas as estações com o Aren (IK de mãos na borda, feet IK, vault).
- Secundário: `ArenSpringBone` próprio (verlet, sub-steps fixos → independente de FPS,
  colliders esfera/cápsula: cabeça, ombros, coxas para o tabardo), com peso global e
  por cadeia para reduzir/desligar (rig §39).
- Twist: `ArenTwistDriver` distribui a torção da mão para os ossos de twist.
- Dedos fechando ao agarrar borda/poste (Avatar muscle de dedos via HumanPose ou
  rotação direta dos ossos de dedo).

## Fase 7 — Dodge
- `ArenDodge`: curva de deslocamento (AnimationCurve) — startup curto, burst, recovery;
  direção = input (não busca alvo — Freeflow §112); sweep de cápsula contra paredes;
  i-frames configuráveis; cancel windows. Animação: UAL2 `Sword_Dash`/`Shield_Dash`
  (passo evasivo de duelista); roll de Mixamo como melhoria opcional.

## Fase 8 — Combo de flauta (M1-1..4)
- `AttackData` (ScriptableObject) com todos os campos do Freeflow §70.
- `ArenCombat` (fachada) + `ComboController` + `AttackExecutor` — sem monolito.
- Hitbox por janela ativa (OverlapCapsule no anchor da flauta, sem physics triggers
  soltos), eventos `attack_started … finished`.
- Clipes: UAL2 Sword_Regular_A/B/C + Sword_Heavy_Combo recortados em 4 golpes com
  personalidade (rápido / cruzado / ressonância / finalizador).

## Fases 9–13 — Freeflow
- `TargetResolver` (score determinístico: direção, facing, distância, alvo anterior,
  linha de visão; max_angle/max_distance), `TargetingConfig` SO, debug de score.
- `AttackMovement` (curva por golpe, sweep de cápsula, nunca teleporta, tracking por
  fase startup/travel/active/recovery), assist off/low/normal para comparação.
- Redirecionamento entre golpes sem resetar combo; rotação interpolada (sem snap).
- `ThreatResolver` + `CounterController` (janela e perfect window, direção da ameaça),
  dummies agressores/emissores.
- Dodge dentro do combo (combo_grace_time, re-entry com ataque bufferizado).
- `FREEFLOW_CANCEL_MATRIX.md`.
- Lab: cena `FreeflowCombatLab.unity` gerada por script (3 dummies, 5 em círculo,
  distante, parede entre alvos, dummy móvel, emissores).

## Fase 14 — Musical flow
- `MusicalClock` (BPM), avaliação free/good/perfect **sem nunca atrasar input**,
  frase do combo (nota curta → resposta → tensão → resolução), ressonância com decay.

## Fase 15 — VFX (linguagem única, botão F4 liga/desliga)
- Slash de ressonância (malha de arco gerada + shader de gradiente/ruído), anéis de
  frequência, trails da flauta, impacto, parry, dodge afterimage, poeira de pouso,
  Contracanto. Pooling. Orçamento de partículas para GPU integrada.

## Fase 16 — Áudio
- `ArenSynth`: nota de flauta procedural por golpe (escala da frase), whoosh, impacto,
  parry cristalino, footsteps por animation event/feet contact.

## Fase 17 — Câmera / feedback
- Hitstop (timeScale curto com proteção), Cinemachine Impulse por preset
  (LightHit, HeavyHit, Parry, PerfectParry, Dodge, Finisher, Landing), leve zoom em
  combate. Opção de reduzir shake/flash.

## Fase 18 — Contracanto (teste máximo)
## Fase 19 — Stress test (30/60/120 FPS via `Application.targetFrameRate`, teclado+gamepad)
## Fase 20 — Quality pass → `FREEFLOW_COMBAT_FINAL.md`, `REMAKE_CHANGELOG.md`

## Pontos onde vou precisar de você [PARE]
- Clipes do Mixamo se o material CC0 não bastar (ex.: roll, running turn 180) — só
  com seu login. Vou juntar a lista exata antes de pedir, uma vez só.
- Playtest de sensação nos checkpoints A (movimento), B (rig), D (freeflow).
