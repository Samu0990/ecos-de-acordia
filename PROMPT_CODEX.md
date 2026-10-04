# Prompt para colar no Codex

Copie tudo abaixo da linha e cole no Codex (aberto na pasta `~/Unity/ParkourLab`).

---

Você vai continuar o desenvolvimento do meu jogo **Ecos de Acordia: A Ruptura do Contracanto**.
Responda sempre em **português (PT-BR)**.

## Onde está o jogo
- **Projeto Unity:** `/home/samuel/Unity/ParkourLab` (Unity **6000.3.22f1**, Built-in Render Pipeline).
- **Branch:** `main` (o antigo `remake-aren` foi juntado nela em 2026-10-04) — repositório privado https://github.com/Samu0990/ecos-de-acordia.
- **Única cena do jogo:** `Assets/Campanula/Scenes/Campanula.unity`. Ela é **gerada por código**
  (`Assets/Campanula/Editor/CampanulaBuilder.cs`, método `Build()`). Não edite a cena à mão:
  altere o builder e regenere.
- **Código do jogo:** `Assets/Aren/Scripts/` (combate, inimigos, movimento, VFX, áudio, UI, roteiro)
  e `Assets/Campanula/` (mundo, shaders, props).
- **Executável já compilado:** `Builds/EcosDeAcordia_Demo/EcosDeAcordia.x86_64`
  (atalho na Área de trabalho: "Ecos de Acordia - Demo").
- **Atenção:** na Área de trabalho existem `EcosDaDiscordia` (projeto Godot), `EcosDaDiscordia_BACKUP_*`, `EcosDaDiscordia_Original` e `EcosBattleground` (projeto Roblox). **Não são este jogo** — não mexa neles sem eu pedir. O jogo que você vai continuar é só o `~/Unity/ParkourLab`.
- **Guia completo do projeto:** `AGENTS.md` na raiz. **Leia inteiro antes de qualquer coisa.**
  Histórico: `AREN_REMAKE_EXECUTION_LOG.md`. Pendências: `AREN_REMAKE_BACKLOG.md`.
- **Documentos de design** (só leitura, em `~/Downloads`):
  `ECOS_DE_ACORDIA_CLAUDE_MASTER_ORCHESTRATOR.md`, `ECOS_DE_ACORDIA_FREEFLOW_COMBAT_ARKHAM_INSPIRED.md`,
  `ECOS_DE_ACORDIA_AREN_PROFESSIONAL_RIG_PROMPT.md`, `PROMPT_REMAKE_ECOS_DE_ACORDIA_CLAUDE_CODE.md`,
  `Ecos_de_Acordia_Biblia_de_Lore.docx`, `ataques_flauta_ecos_de_acordia.txt`,
  `HUD_v2_Revisao_de_Experiencia_e_System_Design.docx`.

## O que o jogo já tem (demo jogável completa)
- Mundo medieval **Campanula, a Vila dos Doze Sinos**, ao pôr do sol, com a Fenda no céu:
  estrada (tutorial de parkour) → portão → rua do mercado → Praça dos Doze Sinos com a Torre
  (escalada pelas pedras) → ponte → Campo da Fenda.
- Roteiro: 13ª badalada → luta no mercado → 2 ondas na praça → escalar a torre → chefe
  **Cervo Corrompido** → tela final com estatísticas.
- Combate freeflow (estilo Arkham): combo de 4 golpes com a flauta e mira automática, esquiva,
  contra-ataque (anel dourado), 4 habilidades: **Pulso de Ressonância (Q)**, **Lâmina de
  Frequência (E)**, **Eco Fantasma (R)**, **Contracanto (segurar clique, 3 níveis)**.
- Parkour (Dynamic Parkour System): vault, deslizar, escalar, pular entre bordas.
- UI completa (menu, HUD, pausa, configurações, controles, créditos, morte, fim).
- Áudio: síntese procedural + sons gravados (400 Sounds Pack). Props do Fantasy Props MegaKit.
  Efeitos com nebulosas (Seamless Space Backgrounds).
- Jogador atual: modelo "red assassin" (Tripo). Abertura cinematográfica da lore (CutsceneDirector).
  Câmera nova, pós-processamento leve (cor/vinheta/bloom), árvores com volume, capim e trigo.
- ATENÇÃO desempenho: em 2026-10-02 a CPU do notebook estava travada em 400 MHz (carregador?);
  confira `grep MHz /proc/cpuinfo` antes de confiar em qualquer benchmark.
- Testes automáticos dentro do executável e benchmark de FPS.

## REGRAS OBRIGATÓRIAS
1. **NÃO MEXA EM NADA RELACIONADO A CIRURGIA.** É outro projeto meu (simulador de cirurgia em VR).
   Não abra, não edite, não mova, não apague, não importe e não use como referência. Em `~/Downloads`:
   `cirugia coisas/`, `Meshy_models_20260912_005939*`, `Meshy_models_20260912_014004*`
   (luvas `mao1.glb`/`mao2.glb`), `documentacao_completa_do_jogo.md`,
   `PROMPT MASTER — VR SURGERY SIMULATOR - JOGO DE CIRURGIA EM VR.md`, `VR-Surgery-main*`,
   `Surgical_Training_IMSTK-Unity-main*`, `Medical-Unity-VR-main*`, `ic-vr-game-main*`,
   `Estado-do-Projeto-VR-Transplante.pdf`, `Vr Surgical Test.pdf`, `guia detalhado desenvolvimento vr.pdf`,
   `bisturi.glb`, `Heart_Vessels.glb`, `Meshy_AI_Heart_*`, `Meshy_AI_Body_Skin_*`,
   `Meshy_AI_clinical-mannequin-supine.png`, `hospital-bed.glb`, `Operating_Table_Final.glb`,
   `Ribcage_Fixed_Final.glb`, `human skeleton 3d model.glb`, `human-skeleton-obj/`,
   `anatomical+heart+3d+model.zip`, `anatomical muscle model 3d.zip`, `realistic+head+3d+model.zip`.
   Na dúvida se algo é da cirurgia: não toque e me pergunte.
2. **Git:** o jogo está no repositório PRIVADO https://github.com/Samu0990/ecos-de-acordia (branch `main`).
   Trabalhe DIRETO NA `main`: não crie branches novas nem abra PR. Antes de começar dê
   `git pull`; faça commit a cada etapa, com mensagem em português, e dê `git push`. Não torne o
   repositório público e não adicione colaboradores.
3. **Animações:** NUNCA use as do Game Animation Sample da Unreal (licença só para Unreal).
   Use as do projeto, CC0 (Quaternius UAL2 já está no projeto) ou Mixamo (eu baixo — me peça).
4. **Desempenho:** meu notebook é fraco (Dell Latitude 3490, Intel UHD 620, 7,6 GB RAM, Linux Mint,
   1920×1080). O jogo tem que ficar **≥ 45 FPS** na qualidade Média com escala 3D 80%.
   Meça com o benchmark antes de dizer que está bom. Sombras em tempo real custam ~10 FPS.
5. **Licença:** todo asset novo precisa permitir uso comercial e entra nos Créditos
   (`Assets/Aren/Scripts/UI/GameMenus.cs`, `BuildCredits`). Nada do Batman.
6. **Não diga que está pronto sem testar.** Mostre os números do teste e do benchmark e conte
   o que ficou faltando.
7. Trabalhe sozinho o máximo possível; só me pergunte o que realmente depende de mim.

## Como compilar e testar (detalhes no AGENTS.md, seções 4 e 5)
O editor da Unity precisa estar aberto (`unity open .` e espere `unity status` mostrar "ready").
Play mode no editor fica a ~1 FPS nesta máquina — **teste no executável**.
```bash
cd ~/Unity/ParkourLab
Tools/cli/rc                         # recompila os scripts e mostra erros
Tools/cli/cons 15                    # erros/avisos do Console
# se mexeu no CampanulaBuilder: regenerar a cena (job longo, ~2–4 min)
id=$(unity --json command eval_file --file Tools/cli/cs/build_scene.cs --timeout 1200 --detach | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["jobId"])'); unity --json job status $id
Tools/cli/cs/build_player.sh         # gera o executável
cd Builds/EcosDeAcordia_Demo && ./EcosDeAcordia.x86_64 -eda-test -eda-quality 1 -eda-scale 0.8 -logFile ~/EcosBench/test_player.log
grep -a AUTOTEST ~/EcosBench/test_player.log; grep -ac Exception ~/EcosBench/test_player.log
cd ~/Unity/ParkourLab && Tools/cli/bench.sh 1 0.8 0     # FPS por área
```
Etapa só está pronta se: testes de parkour, torre (termina em y ≈ 19) e encontros passarem,
orientação com trava ≤ 3% de quadros de costas, **0 exceções**, e **≥ 45 FPS** no benchmark.

## Cuidados que já custaram tempo (leia a seção 7 do AGENTS.md)
- Os clipes da UAL2 precisam de `rotationOffset = 180` (está no `ArenModelPostprocessor`); sem
  isso os personagens atacam de costas. `TorsoFacingLock` mantém o tronco virado para o alvo.
- Regras de borda do parkour (altura 1.57–2.47 m, grafo de conexões, frente da borda para dentro
  da parede) estão no AGENTS.md.
- O Python do sistema não tem numpy: use `~/.local/opt/blender-5.2/5.2/python/bin/python3.13`.

## Próximas tarefas sugeridas (por ordem)
1. **Câmera de combate:** no mercado estreito ela bate nas paredes e fica atrás de objetos.
2. **Animações de flauta de verdade** (hoje são golpes de espada CC0 com a flauta na mão).
3. **Mais conteúdo:** 2º tipo de inimigo (à distância), rotas de telhado no mercado, interior da
   taverna com os móveis do kit de props.
4. Passos gravados para os Ecos e o cervo; menu principal mais leve (está a ~45 FPS, o resto a 60).
5. Itens do `AREN_REMAKE_BACKLOG.md`.

Comece lendo o `AGENTS.md`, rode `git log --oneline | head` para ver onde paramos e me diga em
poucas linhas o que vai fazer primeiro.
