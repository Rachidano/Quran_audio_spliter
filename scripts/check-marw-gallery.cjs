const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
 const browser = await chromium.launch({ headless: true });
 try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  const base = process.env.PILOT_URL || 'http://127.0.0.1:5098';
  assert.equal((await page.goto(base + '/mushaf/review')).status(), 200);
  assert.equal(await page.locator('.review-card').count(), 114);
  assert.equal(await page.locator('.review-card path').count(), 114);
  const reviewResponse = await page.request.get(base + '/mushaf/review?handler=Report');
  assert.equal(reviewResponse.status(), 200);
  const review = await reviewResponse.json();
  assert.equal(review.entries.length, 114);
  assert.equal(review.reviewVersion, 'openings-v1');
  assert.equal(await page.locator('.review-card[data-surah="114"]').getAttribute('data-page'), '604');
  await page.locator('#status-113').selectOption('issue');
  await page.locator('#note-113').fill('Observation de test');
  await page.reload();
  assert.equal(await page.locator('#status-113').inputValue(), 'issue');
  assert.equal(await page.locator('#note-113').inputValue(), 'Observation de test');
  await page.locator('#review-filter').selectOption('issue');
  assert.equal(await page.locator('.review-card:visible').count(), 1);
  await page.locator('#review-overlay').uncheck();
  assert.equal(await page.locator('.review-card[data-surah="113"] svg').evaluate(el => getComputedStyle(el).visibility), 'hidden');
  const downloadPromise = page.waitForEvent('download');
  await page.locator('#review-export').click();
  const download = await downloadPromise;
  const report = JSON.parse(fs.readFileSync(await download.path(), 'utf8'));
  assert.equal(Object.keys(report.openings).length, 114);
  assert.equal(report.openings['113'].note, 'Observation de test');
  // A changed geometry must not silently retain a previous validation.
  await page.evaluate(() => {
   const key = 'marw-opening-review:' + document.getElementById('marw-review').dataset.revision;
   const data = JSON.parse(localStorage.getItem(key)); data['113'].fingerprint = 'outdated'; localStorage.setItem(key, JSON.stringify(data));
  });
  await page.reload();
  assert.equal(await page.locator('#status-113').inputValue(), 'pending');
  await page.locator('#review-search').fill('الفلق');
  assert.equal(await page.locator('.review-card:visible').count(), 1);
  await page.locator('.review-card:visible img').evaluate(img => img.decode());
  await page.screenshot({ path: 'artifacts/marw-gallery.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  console.log('OK: 114 cards, filters, overlay toggle, note persistence, export, stale review invalidation, mobile.');
 } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
