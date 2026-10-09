const {chromium}=require('C:/Users/ddara/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {pathToFileURL}=require('url');
const path=require('path');
const assert=require('assert');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'chrome'});
 try{
 const page=await browser.newPage({viewport:{width:1600,height:1000},colorScheme:'dark'});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(pathToFileURL(path.join(__dirname,'level-tool-windows.html')).href);
 await page.waitForFunction(()=>Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0));
 await page.screenshot({path:path.join(__dirname,'level-tool-windows-board.png')});
 assert.equal(await page.locator('[data-cell]').count(),81);
 await page.locator('#hp').fill('3');await page.locator('#hp').press('Tab');
 assert.equal(await page.locator('#hp').inputValue(),'3');
 await page.locator('#undo').click();assert.equal(await page.locator('#hp').inputValue(),'4');
 await page.selectOption('#layer','블록');await page.locator('[data-brush="pink"]').click();
 await page.locator('[data-cell="0"]').click();assert.equal(await page.locator('[data-cell="0"] img').getAttribute('alt'),'분홍 토끼');
 await page.locator('#undo').click();
 await page.locator('#selectTool').click();await page.locator('[data-cell="30"]').click();
 await page.locator('[data-mode="tutorial"]').click();await page.locator('#previewToggle').click();
 assert.equal(await page.locator('.cell.dimmed').count(),79);
 await page.locator('#toast').evaluate(e=>e.classList.remove('show'));
 await page.screenshot({path:path.join(__dirname,'level-tool-windows-tutorial.png')});
 await page.locator('#pick').click();await page.locator('[data-cell="11"]').click();await page.locator('[data-cell="12"]').click();
 await page.locator('#condition').click();
 await page.locator('[data-cond="내구도 감소"]').click();assert.equal(await page.locator('.removeCondition').count(),2);
 await page.locator('#addStep').click();await page.locator('[data-sample="로켓 생성"]').click();assert.equal(await page.locator('[data-step]').count(),4);
 await page.locator('#validate').click();await page.locator('#issue').click();
 await page.locator('#play').click();await page.selectOption('#modalSource',{index:1});assert(await page.locator('#packWarning').isVisible());await page.locator('#returnEditor').click();
 await page.locator('[data-mode="board"]').click();await page.selectOption('#layer','장애물');await page.locator('[data-cell="30"]').click();
 for(const size of [{width:1366,height:768},{width:1920,height:1080}]){
   await page.setViewportSize(size);
   const geometry=await page.evaluate(()=>{let g=document.querySelector('#grid').getBoundingClientRect(),s=document.querySelector('.stage').getBoundingClientRect();return {noPageOverflow:document.documentElement.scrollWidth<=innerWidth,boardFits:g.width<=s.width&&g.height<=s.height,panelsSeparate:document.querySelector('.right').getBoundingClientRect().left>=s.right-1};});
   assert(Object.values(geometry).every(Boolean),JSON.stringify({size,geometry}));
 }
 await page.setViewportSize({width:1366,height:768});await page.locator('#dockToggle').click();await page.locator('#toast').evaluate(e=>e.classList.remove('show'));await page.screenshot({path:path.join(__dirname,'level-tool-windows-1366.png')});
 await page.locator('#theme').click();await page.screenshot({path:path.join(__dirname,'level-tool-windows-light.png')});
 assert.equal(errors.length,0,errors.join('\n'));
 console.log('PASS: images, selection, placement, undo, tutorial picking/conditions/samples, validation navigation, trial modal, 1366/1920 geometry and themes.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
