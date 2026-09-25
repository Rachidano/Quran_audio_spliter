const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const base = process.env.QURAN_BASE_URL || 'http://127.0.0.1:5098';
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const page = await browser.newPage();
        const errors = []; page.on('pageerror', e => errors.push(e.message));
        const published = await (await page.request.get(base + '/api/annotations/playback?reference=2:1')).json();
        assert.ok(published[0]?.audioUrl, 'A published audio fixture is needed; no database writes are made');
        const next = await (await page.request.get(base + '/api/mushaf/marw-pilot/next?reference=2:4')).json();
        assert.deepEqual(next, {reference:'2:5',pageNumber:3});
        assert.equal((await (await page.request.get(base + '/api/mushaf/marw-pilot/next?reference=2:285')).json()).reference, '3:1');
        assert.equal((await page.request.get(base + '/api/mushaf/marw-pilot/next?reference=114:6')).status(),204);
        assert.equal((await page.request.get(base + '/api/mushaf/marw-pilot/next?reference=2:286')).status(),404);
        await page.goto(base + '/mushaf/warsh/page/2?Reference=2:1');
        await page.waitForFunction(() => !document.getElementById('passage-toggle').disabled);
        await page.selectOption('#playback-mode','single');
        await page.click('#passage-toggle');
        await page.waitForFunction(() => !document.getElementById('passage-player').paused);
        await page.evaluate(() => { const a=document.getElementById('passage-player'); a.currentTime=Number(document.getElementById('passage-seek').max)-0.1; });
        await page.waitForFunction(() => document.getElementById('passage-player').paused);
        assert.equal(await page.locator('#warsh-pilot').getAttribute('data-selected-reference'),'2:1');
        // Short browser-only fixtures reuse a real MP3; no fake audio or DB changes.
        await page.route('**/api/annotations/playback?*', async route => {
            const reference = new URL(route.request().url()).searchParams.get('reference');
            const data = ['2:4','2:5','2:6'].includes(reference) ? [{...published[0], id:reference,
                reference, segmentType:1, status:'Published', startMs:0, endMs:reference==='2:5'?2000:700,
                durationMs:reference==='2:5'?2000:700, isCutAsset:true,
                wajhCatalogCode:reference==='2:6'?'different-face':'same-face'}] : [];
            await route.fulfill({json:data});
        });
        await page.goto(base + '/mushaf/warsh/page/2?Reference=2:4');
        await page.waitForFunction(() => !document.getElementById('passage-toggle').disabled);
        await page.selectOption('#playback-mode','continuous');
        await page.evaluate(() => { window.originalAudio=document.getElementById('passage-player'); });
        await page.click('#passage-toggle');
        await page.waitForFunction(() => location.pathname.endsWith('/3') && document.getElementById('warsh-pilot').dataset.selectedReference==='2:5' && !document.getElementById('passage-player').paused);
        assert.ok(await page.evaluate(() => window.originalAudio === document.getElementById('passage-player')));
        assert.equal(await page.locator('#playback-wajh').inputValue(),'same-face');
        assert.equal(await page.locator('#page-jump').inputValue(),'3');
        assert.ok((await page.locator('#pilot-image').getAttribute('src')).includes('/pages/3/'));
        await page.waitForFunction(() => document.getElementById('warsh-pilot').dataset.selectedReference==='2:6' && document.getElementById('passage-toggle').disabled);
        assert.ok(await page.locator('#passage-player').evaluate(a=>a.paused));
        assert.equal(await page.locator('#playback-wajh').inputValue(),'same-face');
        assert.deepEqual(errors,[]);
        console.log('PASS single stop; canonical next/page/surah/end; continuous autoplay across page; same audio element; stop on missing matching face');
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode=1; });
