(() => {
    'use strict';
    const root = document.getElementById('marw-review');
    if (!root) return;
    const cards = [...root.querySelectorAll('.review-card')];
    const key = `marw-opening-review:${root.dataset.revision}`;
    let saved = {};
    function storageError() { document.getElementById('review-storage').textContent = 'Sauvegarde locale indisponible. Exporter le rapport avant de quitter cette page.'; }
    try { saved = JSON.parse(localStorage.getItem(key) || '{}') || {}; } catch { storageError(); }
    for (const card of cards) {
        const old = saved[card.dataset.surah];
        if (old?.fingerprint === card.dataset.fingerprint) {
            if (['pending', 'issue', 'checked'].includes(old.status)) card.querySelector('select').value = old.status;
            if (typeof old.note === 'string') card.querySelector('textarea').value = old.note.slice(0, 4000);
        }
    }
    function records() {
        return Object.fromEntries(cards.map(card => [card.dataset.surah, {
            page: Number(card.dataset.page), reference: `${card.dataset.surah}:1`,
            fingerprint: card.dataset.fingerprint, status: card.querySelector('select').value,
            note: card.querySelector('textarea').value
        }]));
    }
    function update() {
        const counts = { pending: 0, issue: 0, checked: 0 };
        const query = document.getElementById('review-search').value.trim().toLocaleLowerCase();
        const filter = document.getElementById('review-filter').value;
        let visible = 0;
        for (const card of cards) {
            const status = card.querySelector('select').value;
            counts[status]++;
            card.hidden = !(filter === 'all' || status === filter) || !card.dataset.search.toLocaleLowerCase().includes(query);
            if (!card.hidden) visible++;
        }
        document.getElementById('review-progress').textContent = `${visible} / 114 fiches affichées · ${counts.pending} à examiner · ${counts.issue} à corriger · ${counts.checked} conformes`;
    }
    root.addEventListener('input', event => {
        if (event.target.closest('.review-card')) {
            try { localStorage.setItem(key, JSON.stringify(records())); } catch { storageError(); }
        }
        update();
    });
    document.getElementById('review-overlay').addEventListener('change', event => root.classList.toggle('hide-review-overlay', !event.target.checked));
    document.getElementById('review-export').addEventListener('click', () => {
        const report = { edition: 'MARW newMushaf', revision: root.dataset.revision, scope: 'First ayah of each surah only', exportedAt: new Date().toISOString(), openings: records() };
        const url = URL.createObjectURL(new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' }));
        const link = document.createElement('a'); link.href = url; link.download = `marw-opening-review-${root.dataset.revision}.json`; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
    update();
})();
