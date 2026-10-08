# WorldKit — modelos de terceiros do mundo de Elyndra

## Poly Haven (CC0 1.0 — domínio público, uso comercial livre, sem atribuição obrigatória)
Fonte: https://polyhaven.com (glTF 1k/2k baixado por `Tools/texgen/fetch_ph_models.py` para
`~/EcosAssets/polyhaven/`, fora do repositório). Reduzidos para jogo por `scripts/wk_ph_reduce.py`
(Blender 5.2: solda as costuras do glTF, colapsa até o alvo, LOD1 a 30%, ARM → metálico/suavidade + AO).
Saída: `Assets/World/Models/PolyHaven/<id>/`; prefabs por `Elyndra.WorldEditor.WorldKitSetup.All()`.

| id | uso no jogo |
|---|---|
| boulder_01, rock_07, moon_rock_01, mountainside, coast_rocks_05 | pedras, rochedos e encostas |
| coastal_cliff_02, coastal_cliff_04 | falésias (a face de ~11 m fica no +Z do modelo, com o topo atrás) |
| modular_fort_01 | kit de forte separado em 20 peças (`modular_fort_01_pNN`: torres, muralhas, cantos) — postos de fronteira e fortalezas |
| large_castle_door, large_iron_gate | portões dos reinos e das cidades |
| cannon_01, wooden_bowl_01, ceramic_pot, jug_01 | objetos das vilas (os barris e o lampião do Poly Haven são modernos: ficaram de fora) |
| dead_tree_trunk, dead_tree_trunk_02 | troncos mortos |

Créditos (cortesia, CC0 não exige): Poly Haven e seus autores (lista em cada `<id>.json`).
