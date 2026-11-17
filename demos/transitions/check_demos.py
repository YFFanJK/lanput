from pathlib import Path
from playwright.sync_api import sync_playwright
import json

root = Path(__file__).resolve().parent
out = root.parents[1] / 'tests-output/transition-demos'
out.mkdir(parents=True, exist_ok=True)
results=[]
with sync_playwright() as pw:
    browser=pw.chromium.launch(channel='chrome', headless=True)
    page=browser.new_page(viewport={'width':1440,'height':960},device_scale_factor=1)
    errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    for file in ['01-curtain.html','02-screen-to-vinyl.html','03-lights-down.html']:
        page.goto((root/file).as_uri())
        page.wait_for_timeout(6100)
        assert page.locator('body').get_attribute('data-state')=='music', file+' automatic transition'
        page.screenshot(path=str(out/(file[:-5]+'-music.png')))
        page.locator('.play').click()
        assert not page.locator('.scene').evaluate('(el)=>el.classList.contains("running")')
        page.locator('.play').click()
        page.locator('#toCinema').click()
        page.wait_for_timeout(150)
        page.locator('#toMusic').click()
        page.wait_for_timeout(4000)
        assert page.locator('body').get_attribute('data-state')=='music', file+' rapid reversal'
        page.locator('#toCinema').click()
        page.keyboard.press('Escape')
        assert page.locator('body').get_attribute('data-state')=='cinema', file+' escape cleanup'
        for progress in [.18,.5,.76]:
            page.evaluate('(p)=>window.demoSeek(p)',progress)
            page.screenshot(path=str(out/(file[:-5]+f'-{int(progress*100):02d}.png')))
        page.locator('#reduce').check()
        page.locator('#toCinema').click()
        page.locator('#toMusic').click()
        assert page.locator('body').get_attribute('data-state')=='music', file+' reduced motion'
        for width,height in [(900,640),(390,844)]:
            page.set_viewport_size({'width':width,'height':height})
            page.wait_for_timeout(120)
            assert page.evaluate('document.documentElement.scrollWidth<=innerWidth'), file+' horizontal overflow'
            box=page.locator('.scene').bounding_box()
            assert box['height']>200, file+' collapsed scene'
        page.screenshot(path=str(out/(file[:-5]+'-mobile.png')))
        page.set_viewport_size({'width':1440,'height':960})
        assert not errors, errors
        results.append({'file':file,'result':'PASS','checks':['offline load','automatic entry','pause record','interrupt and reverse','Escape','scrub','reduced motion','900px and 390px layout']})
    reduced=browser.new_page(reduced_motion='reduce',viewport={'width':1280,'height':800})
    reduced.goto((root/'01-curtain.html').as_uri())
    assert reduced.locator('#reduce').is_checked()
    reduced.wait_for_timeout(2400)
    assert reduced.locator('body').get_attribute('data-state')=='cinema'
    reduced.locator('#toMusic').click()
    assert reduced.locator('body').get_attribute('data-state')=='music'
    browser.close()
(out/'checks.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: all three demos; offline, interruption, reverse, reduced motion, responsive layout; no JavaScript errors')
