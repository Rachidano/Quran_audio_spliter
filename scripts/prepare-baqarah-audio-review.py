"""Create waveform evidence only; peaks are not ayah or repetition boundaries."""
import array, hashlib, json, subprocess
from pathlib import Path
root=Path(__file__).resolve().parent.parent
source=root/'data/audio/sources/002/original.mp3'
probe=json.loads(subprocess.check_output(['ffprobe','-v','quiet','-show_entries','format=duration,size','-of','json',str(source)]))['format']
raw=subprocess.check_output(['ffmpeg','-v','quiet','-i',str(source),'-t','360','-ac','1','-ar','4000','-f','s16le','-'])
samples=array.array('h',raw)
peaks=[round(max(abs(x) for x in samples[i:i+800])/32768,4) for i in range(0,len(samples),800)]
checksum=hashlib.sha256()
with source.open('rb') as f:
    for chunk in iter(lambda:f.read(1024*1024),b''): checksum.update(chunk)
report=dict(sourceSha256=checksum.hexdigest(),durationSeconds=float(probe['duration']),sizeBytes=int(probe['size']),
            waveformSeconds=len(samples)/4000,stepSeconds=.2,peaks=peaks,
            status='Listening aid only. No verified ayah timestamps or wajh labels.')
target=root/'src/Quran.Web/wwwroot/data/baqarah-audio-review.json'
target.parent.mkdir(parents=True,exist_ok=True)
target.write_text(json.dumps(report)+'\n',encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k!='peaks'}))
