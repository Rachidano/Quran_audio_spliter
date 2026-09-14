(() => {
    'use strict';
    const root = document.getElementById('warsh-pilot');
    const audio = document.getElementById('passage-player');
    const filter = document.getElementById('playback-wajh');
    const list = document.getElementById('playback-passages');
    const message = document.getElementById('playback-message');
    if (!root || !audio || !filter || !list || !message) return;

    let passages = [];
    let active = null;
    let generation = 0;

    function render() {
        list.replaceChildren();
        const filtered = passages.filter(p => !filter.value || (p.wajhCatalogCode || 'unqualified') === filter.value);
        if (filtered.length === 0 && passages.length > 0) {
            const noMatch = document.createElement('p');
            noMatch.className = 'text-muted small my-2';
            noMatch.textContent = 'لا توجد تسجيلات مطابقة لهذا الوجه.';
            list.append(noMatch);
            return;
        }

        for (const p of filtered) {
            const row = document.createElement('div');
            row.className = 'd-flex gap-2 align-items-center my-1';

            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-outline-success btn-sm flex-grow-1 text-end';
            const durationSec = p.durationMs ? (p.durationMs / 1000).toFixed(1) : ((p.endMs - p.startMs) / 1000).toFixed(1);
            const wajhLabel = p.wajhName || 'وجه غير محدد';
            const typeLabel = p.segmentType === 3 ? 'إعادة' : 'مقطع';
            button.textContent = `▶ ${wajhLabel} · ${typeLabel} · ${durationSec} ث`;

            button.onclick = async () => {
                active = p;
                audio.src = p.audioUrl;
                const isCut = p.isCutAsset || (p.audioUrl && p.audioUrl.includes('/published/'));
                audio.currentTime = isCut ? 0 : (p.startMs / 1000);
                try {
                    await audio.play();
                    message.textContent = p.status === 'Published' ? `تشغيل: ${wajhLabel}` : `معاينة: ${wajhLabel}`;
                } catch {
                    message.textContent = 'تعذر تشغيل الصوت تلقائياً.';
                }
            };
            row.append(button);

            if (p.audioUrl && (p.isCutAsset || p.status === 'Published')) {
                const downloadLink = document.createElement('a');
                downloadLink.href = p.audioUrl;
                downloadLink.className = 'btn btn-sm btn-outline-secondary';
                downloadLink.title = 'تحميل المقطع';
                downloadLink.setAttribute('download', `quran_${(p.reference || '').replace(':', '_')}.mp3`);
                downloadLink.textContent = '⬇';
                row.append(downloadLink);
            }

            list.append(row);
        }
    }

    async function load(reference) {
        const version = ++generation;
        audio.pause();
        active = null;
        passages = [];
        list.replaceChildren();
        filter.replaceChildren(new Option('جميع الأوجه المتاحة', ''));
        if (!reference) return;

        try {
            const endpoint = root.dataset.previewPassage ? '/api/annotations/preview' : '/api/annotations/playback';
            const res = await fetch(`${endpoint}?reference=${encodeURIComponent(reference)}`);
            if (!res.ok) throw new Error();
            const data = await res.json();
            if (version !== generation) return;

            passages = data;
            const faces = new Map(passages.map(p => [p.wajhCatalogCode || 'unqualified', p.wajhName || 'وجه غير محدد']));
            faces.forEach((name, code) => filter.add(new Option(name, code)));
            render();
            message.textContent = passages.length ? 'اختر الوجه أو المقطع للاستماع.' : 'لا توجد مقاطع منشورة لهذه الآية بعد.';
        } catch {
            message.textContent = 'تعذر تحميل المقاطع الصوتية.';
        }
    }

    function stopAtEnd() {
        if (active && !audio.paused) {
            const isCut = active.isCutAsset || (active.audioUrl && active.audioUrl.includes('/published/'));
            if (!isCut && audio.currentTime * 1000 >= active.endMs) {
                audio.pause();
            }
        }
    }

    audio.addEventListener('timeupdate', stopAtEnd);
    const timer = setInterval(stopAtEnd, 40);
    window.addEventListener('pagehide', () => clearInterval(timer));

    filter.onchange = render;
    root.addEventListener('ayah-selected', e => load(e.detail));
    load(root.dataset.initialReference);
})();
