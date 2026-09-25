(() => {
    'use strict';
    const root = document.getElementById('warsh-pilot');
    if (!root) return;
    let polygons = [...root.querySelectorAll('path[data-reference]')];
    let buttons = [...root.querySelectorAll('button[data-reference]')];
    const selection = document.getElementById('pilot-selection');
    function select(reference, updateUrl = true, notify = true) {
        const valid = polygons.some(p => p.dataset.reference === reference);
        polygons.forEach(p => p.classList.toggle('selected', valid && p.dataset.reference === reference));
        buttons.forEach(b => b.setAttribute('aria-pressed', String(valid && b.dataset.reference === reference)));
        if (selection) {
            selection.textContent = valid ? `سورة ${reference.split(':')[0]} — آية ${reference.split(':')[1]}` : 'لم تُحدَّد آية بعد';
        }
        root.dataset.selectedReference = valid ? reference : '';
        if (notify) root.dispatchEvent(new CustomEvent('ayah-selected',{detail:valid?reference:null}));
        if (updateUrl) {
            const url = new URL(location.href);
            url.searchParams.delete('HighlightAyahId');
            if (valid) url.searchParams.set('Reference', reference);
            else url.searchParams.delete('Reference');
            history.replaceState(null, '', url);
        }
    }
    function bindSelection() { [...polygons, ...buttons].forEach(el => el.addEventListener('click', () => select(el.dataset.reference))); }
    bindSelection();
    document.getElementById('pilot-zoom').addEventListener('input', event => {
        document.getElementById('pilot-sheet').style.width = `${event.target.value}%`;
    });
    const image = document.getElementById('pilot-image');
    function imageFailed() {
        document.getElementById('pilot-image-error').hidden = false;
        root.querySelector('.pilot-regions').style.display = 'none';
        buttons.forEach(b => b.disabled = true);
    }
    image.addEventListener('error', imageFailed);
    if (image.complete && image.naturalWidth === 0) imageFailed();
    select(root.dataset.initialReference, false);

    // Keep the same audio element and document when continuous reading crosses a page.
    window.mushafViewer = {
        async showReference(target, isCurrent) {
            if (Number(root.dataset.pageNumber) !== target.pageNumber) {
                const response = await fetch(`/api/mushaf/marw-pilot/pages/${target.pageNumber}`);
                if (!response.ok) throw new Error('page');
                const data = await response.json();
                const sheet = data.page;
                const probe = new Image();
                await new Promise((resolve, reject) => { probe.onload = resolve; probe.onerror = reject; probe.src = data.imageUrl; });
                if (!isCurrent()) return false;
                image.src = data.imageUrl;
                image.width = sheet.width; image.height = sheet.height;
                image.alt = `مصحف ورش — الصفحة ${target.pageNumber}`;
                const overlay = root.querySelector('.pilot-regions');
                overlay.setAttribute('viewBox', sheet.viewBox);
                overlay.replaceChildren();
                const ayahs = root.querySelector('.pilot-ayahs');
                ayahs.replaceChildren();
                for (const region of sheet.regions) {
                    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                    path.setAttribute('d', region.pathData); path.dataset.reference = region.reference;
                    overlay.append(path);
                    const button = document.createElement('button');
                    button.type = 'button'; button.dataset.reference = region.reference;
                    button.textContent = region.reference; button.setAttribute('aria-pressed', 'false');
                    button.setAttribute('aria-label', `سورة ${region.surahNumber} آية ${region.ayahNumber}`);
                    ayahs.append(button);
                }
                polygons = [...overlay.querySelectorAll('path')]; buttons = [...ayahs.querySelectorAll('button')];
                bindSelection();
                root.dataset.pageNumber = target.pageNumber;
                document.getElementById('page-jump').value = target.pageNumber;
                document.getElementById('surah-jump').value = target.reference.split(':')[0];
                document.getElementById('pilot-zoom').value = 100;
                document.getElementById('pilot-sheet').style.width = '100%';
                overlay.style.display = '';
                document.getElementById('pilot-image-error').hidden = true;
                const nav = root.querySelector('.pilot-pages'); nav.replaceChildren();
                const numbers = [...new Set([1,604,...[target.pageNumber-2,target.pageNumber-1,target.pageNumber,target.pageNumber+1,target.pageNumber+2].filter(n=>n>=1&&n<=604)])].sort((a,b)=>a-b);
                for (const n of numbers) {
                    const link = document.createElement('a'); link.href = `/mushaf/warsh/page/${n}`;
                    link.textContent = `صفحة ${n}`; if(n===target.pageNumber) link.setAttribute('aria-current','page'); nav.append(link);
                }
                const url = new URL(location.href); url.pathname = `/mushaf/warsh/page/${target.pageNumber}`;
                url.searchParams.delete('PageNumber'); url.searchParams.delete('SurahNumber');
                history.replaceState(null, '', url);
                document.title = `مصحف ورش — صفحة ${target.pageNumber}`;
            }
            if (!isCurrent()) return false;
            select(target.reference, true, false);
            return true;
        }
    };

    // Keyboard navigation
    document.addEventListener('keydown', e => {
        if (e.repeat || document.activeElement?.closest('input, select, textarea, button, a, [contenteditable=true]')) return;
        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
            e.preventDefault();
            const uniqueRefs = [...new Set(buttons.map(b => b.dataset.reference))];
            if (uniqueRefs.length === 0) return;
            const currentRef = root.dataset.selectedReference || (buttons.find(b => b.getAttribute('aria-pressed') === 'true')?.dataset.reference);
            let idx = uniqueRefs.indexOf(currentRef);
            if (e.key === 'ArrowLeft') {
                idx = (idx + 1) % uniqueRefs.length;
            } else {
                idx = (idx - 1 + uniqueRefs.length) % uniqueRefs.length;
            }
            select(uniqueRefs[idx]);
        } else if (e.code === 'Space') {
            e.preventDefault();
            root.dispatchEvent(new Event('playback-toggle'));
        }
    });
})();
