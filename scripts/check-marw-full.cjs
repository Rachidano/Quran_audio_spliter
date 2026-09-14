const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const crypto = require('node:crypto');
const base = process.env.PILOT_URL || 'http://127.0.0.1:5098';
const manifest = JSON.parse(fs.readFileSync('data/mushaf/editions/marw-newMushaf/20260913-full/manifest.json', 'utf8'));
const hashes = new Map(manifest.files.map(f => [f.file, f.sha256]));
(async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
        const refs = new Set();
        for (let n = 1; n <= 604; n++) {
            const response = await page.request.get(`${base}/api/mushaf/marw-pilot/pages/${n}`);
            assert.equal(response.status(), 200, `Metadata page ${n}`);
            const data = await response.json();
            assert.equal(data.page.number, n);
            for (const region of data.page.regions) refs.add(region.reference);
            const image = await page.request.get(base + data.imageUrl);
            assert.equal(image.status(), 200, `Image page ${n}`);
            const hash = crypto.createHash('sha256').update(await image.body()).digest('hex');
            assert.equal(hash, hashes.get(`page-${String(n).padStart(3, '0')}.png`));
            await response.dispose();
            await image.dispose();
            if (n % 100 === 0) console.log(`${n}/604 image/map pairs verified`);
        }
        assert.equal(refs.size, 6214);
        assert.ok(refs.has('114:6'));
        await page.goto(`${base}/mushaf/page/1`);
        assert.equal(await page.locator('#surah-jump option').count(), 114);
        await page.locator('#page-jump').fill('300');
        await page.locator('#page-jump').press('Enter');
        await page.waitForURL(/PageNumber=300/);
        assert.match(await page.locator('#pilot-image').getAttribute('src'), /pages\/300\//);
        await page.locator('#surah-jump').selectOption('114');
        await page.locator('#surah-jump').locator('..').locator('button').click();
        await page.waitForURL(/604.*Reference/);
        await page.locator('#pilot-image').evaluate(img => img.decode());
        assert.equal(await page.locator('button[data-reference="114:1"]').getAttribute('aria-pressed'), 'true');
        await page.locator('button[data-reference="113:1"]').click();
        assert.equal(await page.locator('.pilot-regions path.selected').getAttribute('data-reference'), '113:1');
        assert.ok(await page.locator('button[data-reference="112:1"]').count());
        await page.screenshot({ path: 'artifacts/marw-full-last-page.png', fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 });
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        assert.equal((await page.request.get(`${base}/mushaf/page/605`)).status(), 404);
        assert.equal((await page.request.get(`${base}/mushaf/page?SurahNumber=115`)).status(), 404);
        console.log('OK: all 604 PNG hashes/maps, 6214 references, 114 surahs, page jump, surah jump, multi-surah page, mobile bounds.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
