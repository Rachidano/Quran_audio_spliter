const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.PILOT_URL || 'http://127.0.0.1:5098';
(async () => {
    fs.mkdirSync('artifacts/marw-region-review', { recursive: true });
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage({ viewport: { width: 1280, height: 1100 } });
        await page.goto(`${base}/mushaf/page/604`);
        await page.locator('#pilot-image').evaluate(img => img.decode());
        // Independent image landmarks: blank tail, title/basmala, then verse text.
        // Coordinates are fractions of the original PNG, not derived from the map.
        for (const [ref, textY, headerY, blankY] of [
            ['112:1', .29, .205, null],
            ['113:1', .46, .375, .33],
            ['114:1', .68, .59, .545]]) {
            await page.locator(`button[data-reference="${ref}"]`).click();
            const hits = await page.evaluate(({ ref, textY, headerY, blankY }) => {
                const svg = document.querySelector('.pilot-regions');
                const path = svg.querySelector(`path[data-reference="${ref}"]`);
                const box = svg.viewBox.baseVal;
                const contains = (x, y) => path.isPointInFill(new DOMPoint(x * box.width, y * box.height));
                return { text: contains(.75, textY), header: contains(.75, headerY),
                    blank: blankY !== null && contains(.2, blankY) };
            }, { ref, textY, headerY, blankY });
            assert.equal(hits.text, true, `${ref}: verse text selectable`);
            assert.equal(hits.header, false, `${ref}: heading excluded`);
            assert.equal(hits.blank, false, `${ref}: previous line excluded`);
            await page.locator('#pilot-sheet').screenshot({ path: `artifacts/marw-region-review/604-${ref.replace(':', '-')}.png` });
        }
        for (const [n, ref, minY] of [[1,'1:1',.38], [2,'2:1',.375], [3,'2:5'], [50,'3:1',.26], [106,'5:1',.475], [300,null], [603,'109:1',.26], [603,'110:1',.475], [603,'111:1',.685]]) {
            await page.goto(`${base}/mushaf/page/${n}`);
            await page.locator('#pilot-image').evaluate(img => img.decode());
            const button = ref ? page.locator(`button[data-reference="${ref}"]`) : page.locator('button[data-reference]').first();
            await button.click();
            if (minY) {
                const top = await page.locator('.pilot-regions path.selected').evaluate(p => p.getBBox().y / p.ownerSVGElement.viewBox.baseVal.height);
                assert.ok(top > minY, `${ref}: heading/basmala excluded (${top})`);
            }
            if (n === 2) assert.ok((await page.locator('.pilot-regions path.selected').getAttribute('d')).split('M ').length > 2, 'Multiline 2:1 preserved');
            await page.locator('#pilot-sheet').screenshot({ path: `artifacts/marw-region-review/page-${n}${n === 603 ? '-' + ref.replace(':', '-') : ''}.png` });
        }
        console.log('OK: page 604 verse landmarks included; headings and blank tails excluded; multiline 2:1 preserved. Review screenshots saved.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
