// Read-only UI checks against the local site; temporary Identity account only.
const { chromium } = require('playwright');
const { spawnSync } = require('node:child_process');
const { randomUUID } = require('node:crypto');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const email = `studio-audit-${randomUUID()}@local.invalid`;
const password = randomUUID() + 'Aa!9';
const python = process.env.QURAN_PYTHON || 'C:/Users/r_kaci/.pyenv/pyenv-win/versions/3.10.0/python.exe';
let browser;
(async () => {
  try {
    const created = spawnSync('dotnet', [path.join(root, 'src/Quran.Web/bin/Debug/net9.0/Quran.Web.dll'), '--create-admin', email, password],
      { cwd: path.join(root, 'src/Quran.Web'), encoding: 'utf8', windowsHide: true });
    assert.equal(created.status, 0, 'Account setup command failed');
    assert.match(created.stdout, /created successfully/);
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage({ viewport: { width: 1440, height: 1080 } });
    await page.goto('http://127.0.0.1:5098/Account/Login');
    await page.locator('#Input_Email').fill(email);
    await page.locator('#Input_Password').fill(password);
    await Promise.all([page.waitForURL(url => !url.pathname.includes('/Login')), page.locator('form').filter({ has: page.locator('#Input_Email') }).locator('button[type=submit]').click()]);
    await page.goto('http://127.0.0.1:5098/Admin/AudioEditor/2');
    await page.waitForFunction(() => typeof editorContext !== 'undefined' && editorContext?.ayahs?.length === 285, { timeout: 60000 });
    assert.match(await page.locator('#surahSelect option:checked').textContent(), /285/);
    const text = await page.evaluate(() => editorContext.ayahs[1].textWarsh);
    assert.ok(text.includes('يُومِنُونَ'), 'Warsh comparison text expected');
    assert.equal(await page.evaluate(() => editorContext.ayahs[0].id), 0, 'No legacy AyahId binding');
    await page.waitForFunction(() => wavesurfer?.getDuration() > 10000, { timeout: 60000 });
    await page.evaluate(() => { wavesurfer.pause(); wavesurfer.setTime(14.321); });
    await page.locator('#btnMarkStart').click();
    assert.equal(await page.locator('#quickStartMs').inputValue(), '14321');
    await page.locator('#quickStartMs').fill('15000');
    await page.locator('#quickStartMs').press('Tab');
    assert.equal(await page.evaluate(() => currentStartMs), 15000);
    await page.evaluate(() => { document.activeElement.blur(); wavesurfer.setTime(16.123); });
    await page.keyboard.press('F7');
    assert.equal(await page.evaluate(() => currentStartMs), 16123);
    await page.locator('#quickStartMs').focus();
    await page.keyboard.press('F7');
    assert.equal(await page.evaluate(() => currentStartMs), 16123);
    await page.screenshot({ path: path.join(root, 'artifacts/studio-corrected.png'), fullPage: false });
    const api = await page.request.get('http://127.0.0.1:5098/api/annotations/playback?reference=2:1');
    assert.equal(api.status(), 200);
    const anon = await browser.newContext();
    assert.equal((await anon.request.get('http://127.0.0.1:5098/audio/published/unpublished-test.mp3')).status(), 404);
    console.log('PASS: 285 MARW references, Warsh candidate text, no legacy FK, button/manual/F7, input shortcut isolation, public media guard. No annotations created.');
  } finally {
    if (browser) await browser.close();
    const cleanup = spawnSync(python, ['scripts/cleanup-studio-audit-user.py', email],
      { cwd: root, encoding: 'utf8', windowsHide: true });
    if (cleanup.status !== 0) console.error(cleanup.stderr);
    assert.equal(cleanup.status, 0, 'Temporary account cleanup failed');
  }
})().catch(err => { console.error(err.message); process.exitCode = 1; });
