(() => {
    'use strict';
    const root = document.getElementById('warsh-pilot');
    const audio = document.getElementById('passage-player');
    const mode = document.getElementById('playback-mode');
    const filter = document.getElementById('playback-wajh');
    const container = document.getElementById('playback-passages');
    const selector = document.getElementById('playback-segment');
    const toggle = document.getElementById('passage-toggle');
    const seek = document.getElementById('passage-seek');
    const time = document.getElementById('passage-time');
    const description = document.getElementById('playback-description');
    const message = document.getElementById('playback-message');
    if (!root || !audio || !toggle) return;
    let passages = [], active = null, generation = 0, playAttempt = 0;
    let flow = 0, finished = false, autoStart = false, currentReference = root.dataset.initialReference;
    let wantedFace = new URLSearchParams(location.search).get('wajh') || '';
    try { mode.value = sessionStorage.getItem('mushaf-reading-mode') === 'continuous' ? 'continuous' : 'single'; } catch {}
    if (root.dataset.previewPassage) { mode.value = 'single'; mode.disabled = true; }

    const isCut = p => p.isCutAsset || p.audioUrl?.includes('/published/');
    const start = () => active && !isCut(active) ? active.startMs / 1000 : 0;
    const duration = () => active ? Math.max(0, (active.endMs - active.startMs) / 1000) : 0;
    const end = () => start() + duration();
    const format = seconds => `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${Math.floor(seconds % 60).toString().padStart(2, '0')}`;
    const label = p => `${p.wajhName || 'وجه غير محدد'} · ${{1:'آية كاملة',2:'جزء من آية',3:'إعادة',4:'تكملة'}[p.segmentType] || 'مقطع'} · ${((p.endMs-p.startMs)/1000).toFixed(1)} ث${p.status !== 'Published' ? ' — معاينة' : ''}`;

    function update() {
        const elapsed = active ? Math.min(duration(), Math.max(0, audio.currentTime - start())) : 0;
        seek.value = elapsed;
        time.textContent = `${format(elapsed)} / ${format(duration())}`;
        toggle.textContent = audio.paused ? '▶ تشغيل' : '⏸ إيقاف مؤقت';
        toggle.setAttribute('aria-pressed', String(!audio.paused));
    }
    function reset() {
        ++playAttempt;
        audio.pause();
        active = null;
        autoStart = false;
        finished = false;
        audio.removeAttribute('src');
        audio.load();
        toggle.disabled = true;
        seek.disabled = true;
        seek.max = 0;
        description.textContent = '';
        update();
    }
    function choose(p, autoplay = false) {
        reset();
        if (!p?.audioUrl || !(p.endMs > p.startMs)) return;
        active = p;
        autoStart = autoplay;
        description.textContent = label(p);
        seek.max = duration();
        audio.src = p.audioUrl;
        audio.load();
        message.textContent = 'جارٍ تحميل المقطع…';
    }
    function render(autoplay = false) {
        const filtered = passages.filter(p => !filter.value || (p.wajhCatalogCode || 'unqualified') === filter.value);
        selector.replaceChildren(...filtered.map(p => new Option(label(p), p.id)));
        container.hidden = filtered.length < 2;
        const preferred = mode.value === 'continuous' ? filtered.find(p => p.segmentType === 1 && p.status === 'Published') : filtered[0];
        selector.value = preferred?.id || '';
        choose(preferred, autoplay);
        if (filtered.length && !preferred) message.textContent = 'لا توجد آية كاملة منشورة لهذا الوجه؛ توقفت القراءة المتواصلة.';
        if (!filtered.length) message.textContent = (autoplay ? 'توقفت القراءة. ' : '') + (passages.length ? 'لا يوجد تسجيل لهذا الوجه.' : 'لا يوجد تسجيل متاح لهذه الآية بعد.');
    }
    async function load(reference, autoplay = false) {
        ++flow;
        const flowAtLoad = flow;
        currentReference = reference;
        const version = ++generation;
        reset();
        passages = [];
        container.hidden = true;
        selector.replaceChildren();
        filter.replaceChildren(new Option('جميع الأوجه المتاحة', ''));
        filter.disabled = true;
        message.textContent = reference ? 'جارٍ تحميل التسجيلات…' : 'اختر آية من المصحف للاستماع.';
        if (!reference) return;
        try {
            // Drafts are available only through an explicit review link.
            const endpoint = root.dataset.previewPassage ? 'preview' : 'playback';
            const response = await fetch(`/api/annotations/${endpoint}?reference=${encodeURIComponent(reference)}`);
            if (!response.ok) throw new Error();
            const data = await response.json();
            if (version !== generation) return;
            passages = root.dataset.previewPassage ? data.filter(p => p.id === root.dataset.previewPassage) : data;
            const faces = new Map(passages.map(p => [p.wajhCatalogCode || 'unqualified', p.wajhName || 'وجه غير محدد']));
            faces.forEach((name, code) => filter.add(new Option(name, code)));
            const requested = wantedFace;
            if (requested) {
                if (!faces.has(requested)) filter.add(new Option('الوجه المطلوب غير متاح', requested));
                filter.value = requested;
            }
            filter.disabled = !passages.length;
            render(autoplay && flowAtLoad === flow && mode.value === 'continuous');
        } catch {
            if (version === generation) message.textContent = 'تعذر تحميل التسجيلات. أعد اختيار الآية للمحاولة.';
        }
    }
    async function playPause(automatic = false) {
        if (!automatic) { ++flow; autoStart = false; }
        if (!active || toggle.disabled) return;
        if (!audio.paused) { ++playAttempt; audio.pause(); return; }
        finished = false;
        const attempt = ++playAttempt;
        try {
            if (audio.currentTime < start() || audio.currentTime >= end() - 0.05) audio.currentTime = start();
            await audio.play();
            if (attempt === playAttempt) message.textContent = active?.status === 'Published' ? 'جارٍ الاستماع.' : 'معاينة المقطع.';
        } catch {
            if (attempt === playAttempt) message.textContent = 'تعذر تشغيل الصوت. اضغط تشغيل للمحاولة مجدداً.';
        }
    }
    audio.addEventListener('loadedmetadata', () => {
        if (!active) return;
        audio.currentTime = start();
        toggle.disabled = false;
        seek.disabled = false;
        message.textContent = 'المقطع جاهز للاستماع.';
        update();
        if (autoStart) { autoStart = false; void playPause(true); }
    });
    audio.addEventListener('error', () => {
        if (!active) return;
        toggle.disabled = true;
        seek.disabled = true;
        message.textContent = 'تعذر تحميل الملف الصوتي. أعد اختيار الآية للمحاولة.';
    });
    async function finish() {
        if (!active || finished) return;
        finished = true;
        audio.pause();
        if (mode.value !== 'continuous' || root.dataset.previewPassage) {
            message.textContent = 'انتهى المقطع. اضغط تشغيل لإعادته أو اختر آية أخرى.';
            return;
        }
        if (active.segmentType !== 1 || active.status !== 'Published') {
            message.textContent = 'توقفت القراءة: المقطع ليس آية كاملة منشورة.';
            return;
        }
        const token = flow;
        const face = filter.value || active.wajhCatalogCode || 'unqualified';
        message.textContent = 'جارٍ الانتقال إلى الآية التالية…';
        try {
            const response = await fetch(`/api/mushaf/marw-pilot/next?reference=${encodeURIComponent(currentReference)}`);
            if (token !== flow || mode.value !== 'continuous') return;
            if (response.status === 204) { message.textContent = 'انتهت القراءة عند آخر آية من المصحف.'; return; }
            if (!response.ok) throw new Error();
            const next = await response.json();
            if (token !== flow || mode.value !== 'continuous') return;
            const moved = await window.mushafViewer.showReference(next, () => token === flow && mode.value === 'continuous');
            if (!moved || token !== flow) return;
            wantedFace = face;
            await load(next.reference, true);
        } catch {
            if (token === flow) message.textContent = 'تعذر الانتقال إلى الآية التالية. توقفت القراءة.';
        }
    }
    function enforceBounds() {
        if (active && !audio.paused && audio.currentTime >= end()) void finish();
        update();
    }
    seek.addEventListener('input', () => {
        if (active) audio.currentTime = start() + Math.min(duration(), Math.max(0, Number(seek.value)));
        finished = false;
        enforceBounds();
    });
    audio.addEventListener('timeupdate', enforceBounds);
    audio.addEventListener('play', update);
    audio.addEventListener('pause', () => {
        update();
        if (active && !finished) message.textContent = audio.currentTime >= end() - 0.05 ? 'انتهى المقطع. اضغط تشغيل لإعادته.' : 'التشغيل متوقف مؤقتاً.';
    });
    audio.addEventListener('ended', () => { void finish(); update(); });
    toggle.addEventListener('click', () => playPause());
    root.addEventListener('playback-toggle', () => playPause());
    selector.addEventListener('change', () => { ++flow; ++generation; choose(passages.find(p => p.id === selector.value)); });
    filter.addEventListener('change', () => {
        ++flow; ++generation; wantedFace = filter.value;
        const url = new URL(location.href);
        if (wantedFace) url.searchParams.set('wajh', wantedFace); else url.searchParams.delete('wajh');
        history.replaceState(null, '', url);
        render();
    });
    mode.addEventListener('change', () => {
        ++flow; autoStart = false;
        try { sessionStorage.setItem('mushaf-reading-mode', mode.value); } catch {}
        message.textContent = mode.value === 'continuous' ? 'تُقرأ الآيات الكاملة تباعاً بالوجه نفسه. يتوقف التشغيل عند غياب تسجيل مطابق.' : 'يتوقف التشغيل بعد الآية الحالية.';
    });
    root.addEventListener('ayah-selected', e => load(e.detail));
    const timer = setInterval(enforceBounds, 40);
    window.addEventListener('pagehide', () => { clearInterval(timer); audio.pause(); });
    load(root.dataset.initialReference);
})();
