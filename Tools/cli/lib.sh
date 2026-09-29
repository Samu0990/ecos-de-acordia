T=~/Unity/ParkourLab/Tools/cli
tp(){ echo "Aren.DebugTools.ArenTestProbe.ResetPlayer(tpc.gameObject, new UnityEngine.Vector3($1f, $2f, $3f), ${4:-0}f);"; }
summarize(){ awk -F'|' 'NR>2 && $1 !~ /input/ {st=$5; gsub(/ [0-9.,]+ *$/,"",st); if (st!=last) {print $1 "|" $2 "|" $4 "|" st; last=st}}'; }
probe_bg(){ python3 - "$1" "$2" "$3" "${4:-}" <<'PY'
import sys, os
T=os.path.expanduser("~/Unity/ParkourLab/Tools/cli/")
s=open(T+"cs/probe_start.cs").read().replace("TIMELINE",sys.argv[1]).replace("DURATION",sys.argv[2]+"f").replace("//SETUP",sys.argv[3]).replace("//EXTRA",sys.argv[4])
open(T+"tmp/probe_run.cs","w").write(s)
PY
$T/ue $T/tmp/probe_run.cs; }
probe(){ probe_bg "$1" "$2" "$3" "${5:-}" >/dev/null; sleep $(python3 -c "print($2+${4:-1.5})"); $T/ue $T/cs/probe_read.cs; }
fpsfromprobe(){ $T/ue $T/cs/probe_read.cs | LC_ALL=C awk -F'|' 'NR>2 && $1 ~ /^[0-9]/ {t=$1+0; n++; if (n==1) t0=t; if (n>1 && t-prev>maxdt) maxdt=t-prev; prev=t; tl=t} END {printf "frames=%d dur=%.2fs avg_fps=%.1f worst_frame=%.0fms\n", n, tl-t0, (n-1)/(tl-t0), maxdt*1000}'; }
