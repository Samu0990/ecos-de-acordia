#!/bin/bash
# Checa a compilação do C# do projeto SEM o editor (útil quando outra sessão está usando o Unity):
# compila Assembly-CSharp (Assets/**/*.cs fora de Editor/) e Assembly-CSharp-Editor (pastas Editor/) com o
# Roslyn que vem com o Unity, usando as referências do .csproj gerado pelo editor. Só checa; não gera nada no projeto.
cd ~/Unity/ParkourLab
U=~/Unity/Hub/Editor/6000.3.22f1/Editor/Data
DOTNET=$U/NetCoreRuntime/dotnet; CSC=$U/DotNetSdkRoslyn/csc.dll
out=$(mktemp -d)
python3 - "$out" <<'PY'
import re, sys, os, glob
out = sys.argv[1]
def refs(proj):
    s = open(proj, encoding='utf-8').read()
    r = re.findall(r'<HintPath>([^<]*)</HintPath>', s)
    d = re.search(r'<DefineConstants>([^<]*)', s).group(1)
    return [x for x in r if 'ScriptAssemblies/Assembly-CSharp' not in x], d
skip = ('Assets/_Recovery', 'cirugia')
files = [f for f in glob.glob('Assets/**/*.cs', recursive=True) if not any(k in f for k in skip)]
ed = [f for f in files if '/Editor/' in f]
rt = [f for f in files if '/Editor/' not in f]
for name, proj, src, extra in (('rt', 'Assembly-CSharp.csproj', rt, []), ('ed', 'Assembly-CSharp-Editor.csproj', ed, [os.path.join(out, 'rt.dll')])):
    r, d = refs(proj)
    with open(os.path.join(out, name + '.rsp'), 'w') as f:
        f.write('-nologo -noconfig -nostdlib+ -target:library -langversion:9.0 -unsafe+ -nowarn:0169,0649,0414,0618,0219,0168,0162,0067,0108,0114 -warnaserror- -warn:0\n')
        f.write('-define:' + d + '\n')
        f.write('-out:' + os.path.join(out, name + '.dll') + '\n')
        for x in r + extra: f.write('-r:"' + x + '"\n')
        for x in src: f.write('"' + x + '"\n')
PY
ok=0
for a in rt ed; do
  $DOTNET $CSC @$out/$a.rsp 2>&1 | grep -E "error" | head -40
  [ ${PIPESTATUS[0]} -ne 0 ] && ok=1
  [ -f $out/$a.dll ] || { echo "($a não compilou)"; ok=1; break; }
done
rm -rf $out
[ $ok -eq 0 ] && echo "typecheck OK" || echo "typecheck FALHOU"
