#!/usr/bin/env bash
# Rigenera il video «Avvio con la voce» (docs/video/avvio-con-la-voce.*).
# Richiede: node + playwright (Chromium), python3 con numpy e scipy, ffmpeg, espeak-ng con la voce mbrola-it3.
set -euo pipefail
cd "$(dirname "$0")"
[ -d node_modules/@fontsource ] || npm i --no-save playwright @fontsource/big-shoulders-display @fontsource/instrument-serif @fontsource/instrument-sans
OUT=../../docs/video
rm -rf frames && mkdir frames
W=1920 node frames.js full.html frames 30              # fotogrammi: ogni immagine dipende solo dal tempo
node events.js full.html events.json                   # eventi sonori ricavati dalla stessa animazione
python3 sfx.py events.json raw.wav                      # sonoro sintetizzato
ffmpeg -loglevel error -y -i raw.wav -af "volume=4dB,alimiter=limit=0.84:attack=3:release=60:level=disabled" audio.wav
ffmpeg -loglevel error -y -framerate 30 -i frames/f%04d.png -i audio.wav -c:v libx264 -preset slow -crf 22 -pix_fmt yuv420p -c:a aac -b:a 160k -shortest -movflags +faststart $OUT/avvio-con-la-voce.mp4
ffmpeg -loglevel error -y -framerate 30 -i frames/f%04d.png -i audio.wav -c:v libvpx-vp9 -b:v 0 -crf 36 -row-mt 1 -pix_fmt yuv420p -c:a libopus -b:a 128k -shortest $OUT/avvio-con-la-voce.webm
ffmpeg -loglevel error -y -i frames/f0810.png -q:v 3 $OUT/avvio-con-la-voce.jpg
rm -rf frames raw.wav audio.wav events.json
echo "Fatto: $OUT"
