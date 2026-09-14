(() => {
    'use strict';
    const root = document.getElementById('warsh-pilot');
    if (!root) return;
    const polygons = [...root.querySelectorAll('path[data-reference]')];
    const buttons = [...root.querySelectorAll('button[data-reference]')];
    const selection = document.getElementById('pilot-selection');
    function select(reference, updateUrl = true) {
        const valid = polygons.some(p => p.dataset.reference === reference);
        polygons.forEach(p => p.classList.toggle('selected', valid && p.dataset.reference === reference));
        buttons.forEach(b => b.setAttribute('aria-pressed', String(valid && b.dataset.reference === reference)));
        selection.textContent = valid ? `سورة ${reference.split(':')[0]} — آية ${reference.split(':')[1]}` : 'لم تُحدَّد آية بعد';
        root.dispatchEvent(new CustomEvent('ayah-selected',{detail:valid?reference:null}));
        if (updateUrl) {
            const url = new URL(location.href);
            url.searchParams.delete('HighlightAyahId');
            if (valid) url.searchParams.set('Reference', reference);
            else url.searchParams.delete('Reference');
            history.replaceState(null, '', url);
        }
    }
    [...polygons, ...buttons].forEach(el => el.addEventListener('click', () => select(el.dataset.reference)));
    document.getElementById('pilot-clear').addEventListener('click', () => select(null));
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
})();
