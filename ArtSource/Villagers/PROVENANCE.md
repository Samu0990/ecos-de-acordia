# Aldeões de Campanula

Quaternius (quaternius.com / quaternius.itch.io), licença **CC0** (uso comercial livre, sem atribuição
obrigatória — mesmo assim estão nos créditos):
- *Modular Character Outfits - Fantasy* [Standard] v2.1 — roupas de camponês e camponesa;
- *Universal Base Characters* [Standard] — cabeças, olhos, sobrancelhas e cabelos.

Baixados em 2026-10-05 para `~/Downloads/QuaterniusChars` (fora do repositório). Mesmo esqueleto da
Universal Animation Library 2 (as animações da UAL2 servem direto). `scripts/villager_export.py`
(Blender 5.2, headless) monta 4 variantes (roupa + cabeça do personagem base + cabelo, uma malha só)
em `Assets/Aren/Character/Villagers/*.fbx` e copia as texturas em 1024 px. `VillagerSetup.All()`
(menu Aren/Setup/Aldeões - tudo) gera materiais `Aren/Villager`, `Villager.controller` e os prefabs
`Resources/Villagers/Villager_*` (vivos) e `Resources/Enemies/Eco_*` (os mesmos, corrompidos).
