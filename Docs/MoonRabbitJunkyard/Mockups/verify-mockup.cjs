// 독립 HTML 목업 검수. 실제 Unity 게임이나 레벨 데이터는 실행하거나 수정하지 않는다.
const { chromium } = require('C:/Users/ddara/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { pathToFileURL } = require('url');
const path = require('path');
const assert = require('assert');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 1600, height: 1080 } });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(pathToFileURL(path.join(__dirname, 'puzzle-screen.html')).href);
    await page.evaluate(() => document.fonts.ready);
    assert(await page.evaluate(() => document.fonts.check('16px Noto')));
    const portrait = page.locator('.screen.portrait');
    await portrait.locator('[data-goal="1"]').click();
    assert(await portrait.locator('.bubble').isVisible());
    await page.keyboard.press('Escape');
    assert.equal(await portrait.locator('.bubble').count(), 0);
    for (const ratio of ['800', '975', '600']) {
      await page.selectOption('#portrait-ratio', ratio);
      await portrait.locator('[data-item="hammer"]').click();
      assert(await portrait.locator('.pause').isDisabled());
      assert(await portrait.locator('[data-item="shuffle"]').isDisabled());
      const geometry = await portrait.evaluate(s => {
        const r = sel => s.querySelector(sel).getBoundingClientRect();
        const screen = s.getBoundingClientRect(), board = r('.board'), hud = r('.hud'), items = r('.items'), prompt = r('.item-prompt');
        return { boardInside: board.left >= screen.left && board.right <= screen.right + 1,
          hudClear: hud.bottom <= board.top, promptClear: board.bottom <= prompt.top,
          itemsClear: prompt.bottom <= items.top, itemsInside: items.bottom < screen.bottom };
      });
      assert(Object.values(geometry).every(Boolean), JSON.stringify({ ratio, geometry }));
      await portrait.locator('.item-prompt button').click();
      await page.locator('[data-state="fail"]').click();
      await portrait.locator('[data-action="continue"]').click();
      await portrait.locator('.panel-actions button').first().click();
      assert(await portrait.locator('[data-action="continue"]').isVisible());
      await portrait.locator('[data-action="continue"]').click();
      await portrait.locator('.panel-actions button').last().click();
      assert.equal(await portrait.locator('.shade').count(), 0);
      assert.equal(await portrait.locator('.moves-value .number').getAttribute('aria-label'), '5');
      await page.locator('[data-state="fail"]').click();
      assert.equal(await portrait.locator('[data-action="continue"]').count(), 0);
      await portrait.locator('[data-action="ad"]').click();
      await page.locator('[data-state="fail"]').click();
      assert.equal(await portrait.locator('[data-action="ad"]').count(), 0);
      assert.equal(await portrait.locator('[data-action="continue"]').count(), 0);
      await page.locator('[data-state="play"]').click();
    }
    await page.selectOption('#portrait-ratio', '800');
    await page.locator('[data-state="clear"]').click();
    await page.keyboard.press('Escape');
    assert(await portrait.locator('[data-action="next"]').isVisible());
    await page.screenshot({ path: path.join(__dirname, 'preview-clear.png'), fullPage: true });
    await page.locator('[data-state="play"]').click();
    await page.screenshot({ path: path.join(__dirname, 'preview.png'), fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await page.locator('[data-view="portrait"]').click();
    await page.screenshot({ path: path.join(__dirname, 'preview-mobile.png'), fullPage: true });
    assert.deepEqual(errors, []);
    console.log('PASS: embedded font, 3 portrait ratios, non-overlapping board/HUD/items, goal help, item cancellation, continuation confirm/cancel/limits, result Esc, mobile page width, no JS errors.');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
