// Run with PLAYWRIGHT_MODULE pointing to an installed playwright/core module if needed.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.PILOT_URL || 'http://127.0.0.1:5098';
const output = 'artifacts/warsh-pilot-checks';
fs.mkdirSync(output, { recursive: true });

(async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        // Exercise both Razor's native route and its Warsh alias, including generated links.
        for (const route of ['/mushaf/page', '/Mushaf/Page', '/mushaf/warsh/page']) {
            for (const suffix of ['', '/1', '/2', '/3']) {
                const response = await page.goto(base + route + suffix);
                assert.equal(response.status(), 200, `Route failed: ${route}${suffix}`);
                await page.locator('#pilot-image').evaluate(img => img.decode());
            }
        }
        for (const number of [1, 2, 3]) {
            const navigation = page.locator('.pilot-pages a').filter({ hasText: `صفحة ${number}` });
            const href = await navigation.getAttribute('href');
            const response = await page.goto(new URL(href, base).href);
            assert.equal(response.status(), 200, `Generated link failed: ${href}`);
            assert.equal(await page.locator('.pilot-pages a[aria-current="page"]').textContent(), `صفحة ${number}`);
        }
        let clicks = 0;
        for (const [number, expected] of [[1, 7], [2, 4], [3, 11]]) {
            const response = await page.goto(`${base}/mushaf/warsh/page/${number}`);
            assert.equal(response.status(), 200);
            await page.locator('#pilot-image').evaluate(img => img.decode());
            assert.equal(await page.locator('.pilot-regions path').count(), expected);
            const buttons = page.locator('.pilot-ayahs button');
            for (let i = 0; i < expected; i++) {
                await buttons.nth(i).click();
                const reference = await buttons.nth(i).getAttribute('data-reference');
                assert.equal(await buttons.nth(i).getAttribute('aria-pressed'), 'true');
                assert.equal(await page.locator('.pilot-regions path.selected').getAttribute('data-reference'), reference);
                assert.equal(new URL(page.url()).searchParams.get('Reference'), reference);
                clicks++;
            }
            await page.locator('#pilot-clear').click();
            assert.equal(await page.locator('.pilot-regions path.selected').count(), 0);
            // Find an actual filled point, not the bounding-box centre of a concave shape.
            const target = page.locator('.pilot-regions path').first();
            await target.scrollIntoViewIfNeeded();
            const position = await target.evaluate(path => {
                const box = path.getBBox();
                for (let y = box.y + 2; y < box.y + box.height; y += 2)
                    for (let x = box.x + 2; x < box.x + box.width; x += 2) {
                        const point = new DOMPoint(x, y);
                        if (path.isPointInFill(point)) {
                            const screen = point.matrixTransform(path.getScreenCTM());
                            if (document.elementFromPoint(screen.x, screen.y) === path)
                                return { x: screen.x, y: screen.y };
                        }
                    }
                throw new Error('No clickable point in polygon');
            });
            await page.mouse.click(position.x, position.y);
            assert.equal(await target.getAttribute('class'), 'selected');
            await page.screenshot({ path: `${output}/page-${number}.png`, fullPage: true });
        }
        await page.goto(`${base}/mushaf/warsh/page/2?Reference=2%3A3`);
        assert.equal(await page.locator('button[data-reference="2:3"]').getAttribute('aria-pressed'), 'true');
        assert.equal((await page.request.get(`${base}/mushaf/warsh/page/605`)).status(), 404);
        assert.equal((await page.request.get(`${base}/mushaf/warsh/page/2?Reference=2:5`)).status(), 404);
        const metadata = await (await page.request.get(`${base}/api/mushaf/marw-pilot/pages/2`)).json();
        assert.match(metadata.edition, /^MARW_newMushaf_/);
        assert.equal(metadata.audioMappingStatus, 'PendingReview');
        const image = await page.request.get(base + metadata.imageUrl);
        assert.equal(image.status(), 200);
        assert.match(image.headers()['content-type'], /image\/png/);
        const crypto = require('node:crypto');
        const receivedHash = crypto.createHash('sha256').update(await image.body()).digest('hex');
        const originalHash = crypto.createHash('sha256').update(fs.readFileSync('docs/research/marw/page-002.png')).digest('hex');
        assert.equal(receivedHash, originalHash, 'The served image must be the original MARW PNG');
        assert.match(await page.locator('#pilot-image').getAttribute('src'), /marw-pilot/);
        assert.match(image.headers()['cache-control'], /immutable/);
        assert.equal((await page.request.get(base + metadata.imageUrl, { headers: { 'If-None-Match': image.headers().etag } })).status(), 304);
        assert.equal((await page.request.get(base + metadata.imageUrl.replace(metadata.revision, 'wrong'))).status(), 404);
        const audioUrl = await page.locator('audio').getAttribute('src');
        if (audioUrl) {
            const audio = await page.request.get(base + audioUrl, { headers: { Range: 'bytes=0-31' } });
            assert.equal(audio.status(), 206);
            assert.equal((await audio.body()).length, 32);
        }
        await page.setViewportSize({ width: 390, height: 844 });
        await page.goto(`${base}/mushaf/warsh/page/3`);
        await page.locator('#pilot-image').evaluate(img => img.decode());
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.locator('#pilot-zoom').fill('175');
        assert.equal(await page.locator('#pilot-sheet').evaluate(e => e.style.width), '175%');
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.locator('button[data-reference="2:12"]').focus();
        await page.keyboard.press('Enter');
        assert.equal(await page.locator('button[data-reference="2:12"]').getAttribute('aria-pressed'), 'true');
        await page.screenshot({ path: `${output}/mobile.png`, fullPage: true });
        assert.deepEqual(errors, []);
        console.log(`OK: ${clicks} selections, 3 geometric clicks, mobile/zoom/keyboard, original MARW PNG hash/cache/404, source audio Range.`);
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
