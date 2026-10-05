# Cacos da Fenda

`Assets/Aren/Resources/Fenda/FendaShards.fbx` — 12 cacos de rocha facetados, gerados por
`scripts/fenda_shards.py` (Blender, procedural, sem assets de terceiros): fecho convexo de uma nuvem de
pontos num elipsoide alongado, lascado por planos (faces de fratura), ruído leve. Cor de vértice:
R = face de fratura, G = altura relativa, B = semente. `preview.png` é só uma prévia da forma.

Usados por `FendaShards` (a realidade estilhaçada em volta da Fenda, como no storyboard do autor) com o
shader `Hidden/Aren/FendaShard` (luz falsa vinda do eixo do rasgo, técnica dos detritos do "vazio" no
céu de Moss 2, Polyarc).
