#!/usr/bin/env bash
# Stampa le note di rilascio di una versione: la sua sezione di CHANGELOG.md
# seguita dalle istruzioni di download.
set -euo pipefail
tag="$1"
notes=$(awk -v tag="$tag" '
  /^## / { inside = ($2 == tag); next }
  inside' CHANGELOG.md | sed -e '/./,$!d')
if [ -z "$notes" ]; then
  echo "CHANGELOG.md non contiene la sezione «## $tag»" >&2
  exit 1
fi
printf '%s\n\n---\n\n' "$notes"
cat <<'MD'
**Download:** scarica **TimerSala.exe** qui sotto e avvialo (Windows 10/11, nessuna installazione).
Al primo avvio consenti l'accesso alle reti private, per vedere il timer dagli altri dispositivi.
Istruzioni complete nel [README](https://github.com/Lomme89/timersala#readme).
MD
