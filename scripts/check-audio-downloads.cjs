const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const base = process.env.QURAN_BASE_URL || 'http://127.0.0.1:5098';
(async () => {
    const browser = await chromium.launch({channel:'msedge',headless:true});
    try {
        const page = await browser.newPage();
        await page.goto(base + '/mushaf/warsh/page/2?Reference=2:1');
        await page.waitForFunction(() => document.getElementById('passage-download').getAttribute('aria-disabled') === 'false');
        const ayahUrl = await page.locator('#passage-download').getAttribute('href');
        assert.ok(ayahUrl.startsWith('/audio/downloads/ayah/'));
        const ayah = await page.request.get(base + ayahUrl, {headers:{Range:'bytes=0-31'}});
        assert.equal(ayah.status(),206);
        assert.match(ayah.headers()['content-disposition'],/attachment;.*warsh-002-001-/);
        assert.equal((await ayah.body()).length,32);
        const downloadPromise = page.waitForEvent('download');
        await page.click('#passage-download');
        const download = await downloadPromise;
        assert.ok(download.suggestedFilename().startsWith('warsh-002-001-'));
        await download.cancel();
        await page.selectOption('#playback-mode','continuous');
        await page.waitForFunction(() => document.getElementById('passage-download').getAttribute('href') === '/audio/downloads/surah/2');
        const surah = await page.request.get(base + '/audio/downloads/surah/2',{headers:{Range:'bytes=0-31'}});
        assert.equal(surah.status(),206);
        assert.match(surah.headers()['content-disposition'],/attachment;.*warsh-surah-002-original.mp3/);
        assert.equal((await surah.body()).length,32);
        assert.equal((await page.request.get(base + '/audio/sources/002/original.mp3',{maxRedirects:0})).status(),302);
        await page.selectOption('#playback-mode','single');
        assert.equal(await page.locator('#passage-download').getAttribute('href'),ayahUrl);
        await page.locator('button[data-reference="2:4"]').click();
        await page.waitForFunction(() => document.getElementById('passage-download').getAttribute('aria-disabled') === 'true');
        assert.equal(await page.locator('#passage-download').getAttribute('href'),null);
        await page.selectOption('#playback-mode','continuous');
        await page.waitForFunction(() => document.getElementById('passage-download').getAttribute('href') === '/audio/downloads/surah/2');
        console.log('PASS ayah attachment/click, full surah range download, mode switching, no stale ayah link, original private stream unchanged');
    } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exitCode=1;});
