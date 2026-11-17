"""Create three portable, offline HTML transition studies; production code is untouched."""
from pathlib import Path
import base64
import json

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
LOGO = 'data:image/png;base64,' + base64.b64encode((ROOT/'src/LANPUT.Windows/Assets/logo.png').read_bytes()).decode()

def hall():
    seats = []
    for row in range(5):
        width = 58 + row * 12
        gap = 12
        n = int(680 / (width + gap))
        for side in (-1, 1):
            for col in range(n):
                x = 700 + side * (42 + (col+.5)*(width+gap)) - width/2
                y = 487 + row*68
                seats.append(f'<g transform="translate({x:.1f},{y})"><rect width="{width}" height="68" rx="15" fill="url(#seat)" stroke="#3b3740"/><path d="M10 12H{width-10}" stroke="#57505b" opacity=".24"/><rect x="5" y="46" width="{width-10}" height="23" rx="7" fill="#292a33"/><rect x="-4" y="31" width="8" height="39" rx="3" fill="#12171b"/><rect x="{width-4}" y="31" width="8" height="39" rx="3" fill="#12171b"/></g>')
    return '''<svg class="hall-art" viewBox="0 0 1400 800" preserveAspectRatio="xMidYMid slice" aria-hidden="true"><defs><linearGradient id="seat" x2="0" y2="1"><stop stop-color="#37323c"/><stop offset="1" stop-color="#171b23"/></linearGradient><linearGradient id="floor" x2="0" y2="1"><stop stop-color="#15191b"/><stop offset="1" stop-color="#080d11"/></linearGradient></defs><path fill="url(#floor)" d="M0 0H1400V800H0Z"/><path d="M0 0L220 90V520L0 700ZM1400 0L1180 90V520L1400 700Z" fill="#12161a"/><path d="M0 0L330 85H1070L1400 0M120 0L420 85M1280 0L980 85M700 0V85" fill="none" stroke="#272b2d"/><path d="M38 0V700M72 0V670M110 0V635M1362 0V700M1328 0V670M1290 0V635" stroke="#070b0f" stroke-width="14"/><path d="M168 238V357M218 272V380M1232 238V357M1182 272V380" stroke="#b59b61" stroke-width="3" stroke-linecap="round"/><path d="M687 480L614 800H786L713 480Z" fill="#29251d"/><path d="M694 540H706M680 610H720M662 697H738" stroke="#6a5b38" stroke-width="2"/>''' + ''.join(seats) + '</svg>'

POSTER = '''<svg class="poster-art" viewBox="0 0 800 450" preserveAspectRatio="xMidYMid slice" aria-hidden="true"><defs><linearGradient id="sky" x2="0" y2="1"><stop stop-color="#0a1d2a"/><stop offset="1" stop-color="#233039"/></linearGradient></defs><path fill="url(#sky)" d="M0 0H800V450H0Z"/><g fill="#81928d" opacity=".32"><circle cx="102" cy="45" r="1"/><circle cx="276" cy="80" r="1"/><circle cx="367" cy="38" r="1"/><circle cx="628" cy="63" r="1"/><circle cx="710" cy="117" r="1"/></g><path d="M640 90a23 23 0 1 0 31 30 24 24 0 0 1-31-30" fill="#b9b08b"/><path d="M0 345L112 188L208 295L309 235L399 322L520 224L628 300L727 213L800 290V450H0Z" fill="#263740"/><path d="M0 368L125 299L239 358L351 282L472 367L590 312L717 378L800 334V450H0Z" fill="#142832"/><path d="M0 417L125 350L254 415L393 344L529 414L669 358L800 410V450H0Z" fill="#081820"/></svg>'''

CSS = r'''
:root{color-scheme:dark;--bg:#0e1114;--surface:#171d23;--border:#2c333b;--gold:#e8c87a;--text:#eee9df;--secondary:#a9afb8;--muted:#8f98a3;font-family:"Segoe UI","Microsoft YaHei UI",sans-serif;font-size:14px}*{box-sizing:border-box}body{margin:0;background:#090d10;color:var(--text)}button,input{font:inherit}button{min-height:38px;border:1px solid var(--border);border-radius:7px;padding:8px 14px;background:#1a2026;color:var(--text);cursor:pointer;transition:background .16s}button:hover{background:#252d35}button:focus-visible,input:focus-visible,a:focus-visible{outline:2px solid var(--gold);outline-offset:4px}button:disabled{opacity:.4;cursor:default}.primary{background:var(--gold);color:#2a2010;border-color:transparent}.primary:hover{background:#f0d796}a{color:var(--secondary);text-decoration:none}a:hover{color:var(--gold)}.study{height:100dvh;min-height:650px;display:grid;grid-template-rows:auto minmax(0,1fr) auto;max-width:1800px;margin:auto;padding:24px 32px 20px;gap:18px}.study-head{display:flex;justify-content:space-between;align-items:center;gap:20px}.study-title{display:flex;align-items:baseline;gap:14px}.study-title span{color:var(--gold);font-size:13px}.study-title h1{margin:0;font-size:23px;font-weight:500}.description{margin:7px 0 0;color:var(--secondary);font-size:13px;line-height:1.6}.links{display:flex;gap:20px;font-size:13px;flex-shrink:0}.shell{min-height:0;display:grid;grid-template-rows:46px minmax(0,1fr) 66px;border:1px solid var(--border);border-radius:10px;overflow:hidden;background:var(--bg)}.app-head{padding:0 22px;display:flex;justify-content:space-between;align-items:center;font-size:12px;border-bottom:1px solid #242c33}.app-brand{display:flex;gap:12px;align-items:center}.app-brand img{width:49px;height:30px;object-fit:contain}.app-brand strong{font-weight:500;letter-spacing:.6px}.app-state{color:var(--secondary);display:flex;gap:9px;align-items:center}.app-state i{width:5px;height:5px;border-radius:50%;background:var(--gold)}.scene{min-height:0;position:relative;isolation:isolate;overflow:hidden;container-type:size;background:#10171d}.cinema,.music,.curtains,.bridge,.interlude,.dimmer{position:absolute;inset:0;pointer-events:none}.cinema{overflow:hidden;transform-origin:50% 30%}.hall-art{width:100%;height:100%;position:absolute;inset:0}.screen{position:absolute;left:24%;top:8%;width:52%;height:46%;border:1px solid #827965;border-radius:3px;overflow:hidden;box-shadow:0 12px 30px #0003;display:grid;place-items:center;background:#0a1d28}.poster-art{position:absolute;inset:0;width:100%;height:100%}.screen-copy{position:relative;text-align:center;padding:12px}.screen-copy img{width:clamp(58px,7cqw,108px);height:clamp(44px,5cqw,78px);object-fit:contain}.screen-copy h2{font-family:"SimSun",serif;font-weight:400;font-size:clamp(16px,2.3cqw,32px);margin:10px 0}.screen-copy p{font-size:clamp(10px,1.1cqw,15px);color:#b9c4c8;margin:8px 0}.lobby-hint{position:absolute;bottom:7%;width:100%;text-align:center;color:#c9bc9e;font-size:13px}.music{background:#11171c;opacity:0;overflow:hidden}.music::before{content:"";position:absolute;left:6%;top:4%;width:46%;height:82%;border:1px solid #bbaa7910;border-radius:50%;pointer-events:none}.disc-position{position:absolute;left:24%;top:40%;width:min(35cqw,60cqh);aspect-ratio:1;transform:translate(-50%,-50%)}.record{position:absolute;inset:6%;border-radius:50%;background:repeating-radial-gradient(circle at center,#171f25 0px,#171f25 1px,#263038 1.5px,#10171c 2.5px,#10171c 4px);border:1px solid #384047;box-shadow:0 12px 30px #0004;animation:spin 26s linear infinite;animation-play-state:paused}.record::before{content:"";position:absolute;inset:0;border-radius:50%;background:conic-gradient(from 20deg,transparent 0 35deg,#9aabbb19 36deg 65deg,transparent 66deg 205deg,#a5ada815 206deg 240deg,transparent 241deg)}.record-label{position:absolute;inset:35%;border-radius:50%;background:#ddbd77;color:#17212a;display:flex;align-items:center;justify-content:center;flex-direction:column;gap:10%;box-shadow:0 0 0 8px #111a20;font-size:clamp(9px,1.35cqw,18px);font-weight:600;letter-spacing:3px}.record-label small{font-size:clamp(6px,.62cqw,9px);font-weight:400;letter-spacing:.4px}.record-label::after{content:"";position:absolute;width:7%;height:7%;border-radius:50%;background:#0c1720}.record-label span{transform:translateY(-120%)}.record-label small{transform:translateY(160%)}.disc-ring{position:absolute;inset:0;border:1px solid #bba4723b;border-radius:50%}.tonearm{position:absolute;right:7%;top:2%;height:66%;width:3.4%;background:linear-gradient(90deg,#687368,#c0c0a0,#555e58);transform:rotate(19deg);transform-origin:50% 6%;border-radius:5px;box-shadow:7px 4px #0004}.tonearm::before{content:"";position:absolute;width:260%;aspect-ratio:1;border-radius:50%;background:radial-gradient(circle at 35% 30%,#89968b,#27333a);left:-80%;top:-3%;border:1px solid #67746b}.tonearm::after{content:"";position:absolute;bottom:-11%;left:-28%;width:170%;height:22%;border-radius:4px;background:linear-gradient(90deg,#1a2926,#697767,#24302a);border:1px solid #80907a}.music-meta{position:absolute;left:8%;top:76%;right:56%;white-space:nowrap}.music-meta h2{font-size:clamp(22px,2.7cqw,36px);font-weight:500;margin:0 0 8px}.music-meta p{font-size:13px;color:var(--secondary);margin:5px 0}.music-meta small{font-size:12px;color:var(--muted)}.lyrics{position:absolute;left:51%;right:6%;top:21%;bottom:14%;display:flex;flex-direction:column;justify-content:center;gap:clamp(22px,5cqh,46px);padding-left:5%;border-left:1px solid #bba47226}.lyrics p{font-size:clamp(16px,2.1cqw,28px);font-weight:400;margin:0;color:#929da6;line-height:1.4}.lyrics .active{font-size:clamp(24px,3.4cqw,46px);color:#ead4a5;font-weight:500}.lyrics .far{color:#65727c;font-size:clamp(12px,1.3cqw,19px)}.music-tools{position:absolute;right:26px;top:20px;color:var(--secondary);font-size:12px}.music-tools::after{content:"⌄";margin-left:8px}.transport{padding:10px 22px 12px;display:grid;grid-template-columns:1fr auto 1fr;gap:16px;align-items:center;background:#10151a;border-top:1px solid #252d35}.track{font-size:12px;color:var(--secondary);line-height:1.6}.track strong{color:var(--text);font-weight:500;display:block}.play{width:38px;height:38px;padding:0;border-radius:50%;background:var(--gold);color:#262013;border:0;display:grid;place-items:center}.play svg{width:17px;height:17px;fill:currentColor}.transport-right{display:flex;gap:12px;align-items:center;justify-content:flex-end;color:var(--secondary);font-size:12px}.track-line{width:90px;height:2px;background:#3b444f;position:relative}.track-line::before{content:"";position:absolute;width:64%;height:100%;background:var(--gold)}.transport-right svg{width:17px;height:17px;stroke:currentColor;stroke-width:1.5;fill:none}.curtains{z-index:4;display:flex;visibility:hidden}.curtain{width:50%;height:100%;background:repeating-linear-gradient(90deg,#0e1822 0,#17232f 24px,#121d29 42px,#0e1822 64px)}.interlude{display:flex;align-items:center;justify-content:center;flex-direction:column;gap:20px;z-index:5;opacity:0;background:transparent}.interlude img{width:130px;height:90px;object-fit:contain}.interlude p{margin:0;font-family:"SimSun",serif;font-size:clamp(22px,3cqw,38px);font-weight:400;color:#e7d7af}.interlude small{font-size:12px;color:var(--secondary)}.dimmer{background:#0a1016;opacity:0;z-index:2}.bridge{z-index:3;visibility:hidden}.bridge-object{position:absolute;left:24%;top:8%;width:52%;height:46%;transform-origin:50% 50%}.bridge-poster{position:absolute;inset:0;border:1px solid #b39a63;border-radius:3px;overflow:hidden;background:#0d1b26}.bridge-record{position:absolute;inset:0;border-radius:50%;background:repeating-radial-gradient(circle,#121b22 0 1px,#354049 2px,#121b22 3px 5px);border:1px solid #ad955a;opacity:0;display:grid;place-items:center}.bridge-record span{width:30%;height:30%;border-radius:50%;background:#dfbf78;display:grid;place-items:center;font-size:clamp(9px,1.2cqw,16px);letter-spacing:2px;color:#17212a}.review{display:grid;grid-template-columns:auto minmax(120px,1fr) auto;align-items:center;gap:20px}.actions{display:flex;gap:8px}.review-time{display:flex;align-items:center;gap:12px;color:var(--secondary);font-size:12px}.review-time input{width:100%;accent-color:var(--gold);min-height:30px;cursor:pointer}.timing{font-variant-numeric:tabular-nums;min-width:86px;text-align:right}.preferences{display:flex;gap:14px;align-items:center;white-space:nowrap;font-size:12px;color:var(--secondary)}.preferences label{display:flex;align-items:center;gap:6px}.preferences input{accent-color:var(--gold)}.demo-note{grid-column:1/-1;margin:0;color:var(--muted);font-size:12px;line-height:1.6;display:flex;justify-content:space-between;gap:12px}.running .record{animation-play-state:running}@keyframes spin{to{transform:rotate(360deg)}}@media(prefers-reduced-motion:reduce){.record{animation:none!important}button{transition:none}}@media(max-width:1000px){.study{padding:16px;gap:12px}.study-head{align-items:flex-start}.links{gap:10px}.review{grid-template-columns:1fr auto;gap:10px}.review-time{grid-row:2;grid-column:1/-1}.preferences{justify-content:flex-end}.demo-note{grid-row:3}.study-title h1{font-size:20px}}@media(max-width:650px){.study{min-height:820px;padding:12px;grid-template-rows:auto minmax(0,1fr) auto}.study-head{display:block}.links{margin-top:10px}.description{font-size:12px}.shell{grid-template-rows:42px minmax(0,1fr) 60px}.app-head{padding:0 12px}.app-state span{display:none}.screen{left:12%;width:76%;height:34%;top:12%}.screen-copy h2{font-size:19px}.screen-copy p{font-size:10px}.music .disc-position{left:28%;top:30%;width:min(54cqw,39cqh)}.music-meta{left:11%;top:54%;right:5%}.music-meta h2{font-size:24px}.lyrics{left:11%;right:9%;top:70%;bottom:7%;padding:0;border:0;gap:14px}.lyrics p{display:none}.lyrics .active{display:block;font-size:23px}.music-tools{right:16px;top:16px}.transport{padding:10px 12px}.track-line,.transport-right span{display:none}.actions{flex-wrap:wrap}.actions button{font-size:12px;padding:8px 10px}.preferences{font-size:11px}.demo-note{display:block}.demo-note span{display:block}.interlude img{width:110px;height:76px}.interlude p{font-size:25px}.lobby-hint{font-size:12px}}
'''

CSS += r'''
.music::before{display:none}.record-label{font-size:clamp(9px,1.1cqw,14px);letter-spacing:2px}.record-label span{position:absolute;top:24%;left:0;width:100%;text-align:center;transform:none}.record-label small{position:absolute;bottom:20%;left:0;width:100%;text-align:center;transform:none}.bridge-object{inset:0;width:auto;height:auto;transform:none!important}.bridge-poster{inset:auto;left:24%;top:8%;width:52%;height:46%;transform-origin:center}.bridge-record{inset:auto;left:24%;top:40%;width:min(30.8cqw,52.8cqh);height:auto;aspect-ratio:1;transform-origin:center}@media(max-width:650px){.bridge-poster{left:12%;top:12%;width:76%;height:34%}.bridge-record{left:28%;top:30%;width:min(47.52cqw,34.32cqh)}}
'''

JS = r'''
const $=s=>document.querySelector(s), clamp=x=>Math.max(0,Math.min(1,x));
const smooth=(a,b,p)=>{let t=clamp((p-a)/(b-a));return t*t*(3-2*t)};
const ease=t=>t<.5?4*t*t*t:1-Math.pow(-2*t+2,3)/2;
const el={cinema:$('.cinema'),music:$('.music'),curtains:$('.curtains'),left:$('.curtain.left'),right:$('.curtain.right'),copy:$('.interlude'),dim:$('.dimmer'),bridge:$('.bridge'),object:$('.bridge-object'),poster:$('.bridge-poster'),disc:$('.bridge-record')};
let glyph='',p=0,target=0,raf=0,auto=0,playing=true,rect={w:1000,h:600},lastFocus=null;
const preference=matchMedia('(prefers-reduced-motion: reduce)');
$('#reduce').checked=preference.matches;
function paint(q){
 p=clamp(q);let c=1-p,m=p,ct=1,dim=0,copy=0;
 el.cinema.style.transform='none';el.music.style.transform='none';
 el.bridge.style.visibility='hidden';el.curtains.style.visibility='hidden';
 if(VARIANT===1){
  const close=smooth(0,.27,p),open=smooth(.6,1,p);ct=1-close+open;
  el.curtains.style.visibility=p>0&&p<1?'visible':'hidden';
  el.left.style.transform=`translateX(${-101*ct}%)`;el.right.style.transform=`translateX(${101*ct}%)`;
  c=p<.44?1:0;m=1-c;copy=smooth(.28,.38,p)*(1-smooth(.54,.63,p));
 }else if(VARIANT===2){
  c=1-smooth(.02,.32,p);m=smooth(.58,.92,p);dim=0;
  el.bridge.style.visibility=p>0&&p<1?'visible':'hidden';
  const t=ease(clamp((p-.13)/.64));
  let sourceW=rect.w*(rect.w<650?.76:.52),sourceH=rect.h*(rect.w<650?.34:.46);
  let d=Math.min(rect.w*(rect.w<650?.54:.35),rect.h*(rect.w<650?.39:.60))*.88;
  let startX=rect.w*.5,startY=rect.h*(rect.w<650?.29:.31);
  let endX=rect.w*(rect.w<650?.28:.24),endY=rect.h*(rect.w<650?.30:.40);
  el.poster.style.transform=`translate(${(endX-startX)*t}px,${(endY-startY)*t}px) scale(${1+(d/sourceW-1)*t})`;
  el.disc.style.transform=`translate(-50%,-50%) translate(${(startX-endX)*(1-t)}px,${(startY-endY)*(1-t)}px) scale(${1.15-.15*t})`;
  el.bridge.style.opacity=String(smooth(0,.1,p)*(1-smooth(.74,.92,p)));
  el.poster.style.opacity=String(1-smooth(.3,.58,p));el.disc.style.opacity=String(smooth(.3,.58,p));
  el.music.style.transform=`translateY(${(1-smooth(.62,1,p))*14}px)`;
 }else{
  c=1-smooth(0,.40,p);m=smooth(.51,1,p);dim=smooth(.08,.34,p)*(1-smooth(.55,.9,p));
  copy=smooth(.23,.38,p)*(1-smooth(.51,.65,p));
  el.music.style.transform=`translateY(${(1-smooth(.51,1,p))*18}px)`;
 }
 el.cinema.style.opacity=c;el.music.style.opacity=m;el.dim.style.opacity=dim;el.copy.style.opacity=copy;
 el.cinema.inert=p>.01;el.music.inert=p<.99;
 $('#scrub').value=Math.round(p*1000);$('.timing').textContent=`${(p*DURATION).toFixed(1)} / ${DURATION.toFixed(1)} 秒`;
 let moving=p>0&&p<1;
 $('.state-label').textContent=moving?(target?'正在进入音乐厅':'正在返回影院'):p===1?'音乐厅':'私人放映室';
 $('.track strong').textContent=p===1?'夜航':'手机投屏影片';$('.track small').textContent=p===1?(playing?'正在播放 · 模拟':'已暂停 · 模拟'):'等待投屏';
 let nextGlyph=p===1&&playing?'pause':'play';if(glyph!==nextGlyph){glyph=nextGlyph;$('.play svg').innerHTML=glyph==='pause'?'<path d="M6 4h4v16H6zM14 4h4v16h-4z"/>':'<path d="M7 4l14 8-14 8z"/>';$('.play').setAttribute('aria-label',glyph==='pause'?'暂停模拟播放':'继续模拟播放')}
 $('.play').disabled=p!==1;$('.scene').classList.toggle('running',p===1&&playing&&!$('#reduce').checked);
 $('#toMusic').textContent=p===1?'重播此方案':'模拟收到音乐';
 document.body.dataset.state=moving?'transition':p===1?'music':'cinema';
 window.demoProgress=p;
}
function stop(){cancelAnimationFrame(raf);clearTimeout(auto);raf=0;auto=0}
function copyDirection(){ $('.interlude p').textContent=target?'这一场，听音乐':'下一场，回到故事里';$('.interlude small').textContent=target?'私人音乐厅':'私人放映室'}
function settle(){paint(target);$('#announcement').textContent=target?'已进入音乐厅':'已返回影院'}
function go(next){
 stop();target=next;copyDirection();
 if($('#reduce').checked||document.hidden){settle();return}
 let from=p,distance=Math.abs(next-from);if(distance<.001){settle();return}
 let start=performance.now(),time=Math.max(400,DURATION*1000*distance);
 function tick(now){let t=clamp((now-start)/time);paint(from+(next-from)*t);if(t<1)raf=requestAnimationFrame(tick);else{raf=0;settle()}}
 raf=requestAnimationFrame(tick);
}
function replay(){stop();target=1;copyDirection();paint(0);if($('#reduce').checked)go(1);else auto=setTimeout(()=>go(1),350)}
$('#toMusic').onclick=()=>p===1?replay():go(1);$('#toCinema').onclick=()=>go(0);$('#replay').onclick=replay;
$('#scrub').oninput=e=>{stop();copyDirection();paint(Number(e.target.value)/1000)};
$('#reduce').onchange=()=>{stop();settle()};preference.addEventListener('change',e=>{$('#reduce').checked=e.matches;stop();settle()});
document.addEventListener('visibilitychange',()=>{if(document.hidden){stop();settle()}});
document.addEventListener('keydown',e=>{if(e.key==='Escape'){stop();settle()}else if(e.key===' '&&e.target===document.body){e.preventDefault();go(p<.5?1:0)}});
$('.play').onclick=()=>{playing=!playing;$('.play').setAttribute('aria-label',playing?'暂停模拟播放':'继续模拟播放');$('.play svg').innerHTML=playing?'<path d="M6 4h4v16H6zM14 4h4v16h-4z"/>':'<path d="M7 4l14 8-14 8z"/>';paint(p)};
new ResizeObserver(entries=>{const r=entries[0].contentRect;rect={w:r.width,h:r.height};paint(p)}).observe($('.scene'));
window.demoSeek=q=>{stop();target=1;copyDirection();paint(q)};window.demoGo=go;window.demoReplay=replay;
paint(0);if(!preference.matches)auto=setTimeout(()=>go(1),2200);
'''

VARIANTS = [
    (1,'01-curtain.html','幕间换场','幕布落下，把影院留在幕外；再开幕时，旋律已经入座。',3.6,'仪式感最强 · 推荐先看'),
    (2,'02-screen-to-vinyl.html','银幕入盘','让银幕上的画面收拢为一张黑胶，场景转换有一条看得见的来路。',3.0,'视觉联系最强 · 转场更有记忆点'),
    (3,'03-lights-down.html','灯暗声起','灯光先暗下来，留一句过场，再让黑胶与歌词轻轻浮现。',2.8,'日常最轻盈 · 原生实现最稳妥'),
]

for number,filename,title,description,duration,note in VARIANTS:
    links=''.join(f'<a href="{f}"'+(' aria-current="page"' if n==number else '')+f'>{n:02d} {t}</a>' for n,f,t,*_ in VARIANTS)
    record='''<div class="disc-ring"></div><div class="record"><div class="record-label"><span>LANPUT</span><small>SIDE A · 33⅓</small></div></div><div class="tonearm"></div>'''
    page=f'''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="dark"><title>LANPUT · {title} · 转场演示</title><style>{CSS}</style></head><body>
<main class="study"><header class="study-head"><div><div class="study-title"><span>方案 {number:02d}</span><h1>{title}</h1></div><p class="description">{description}</p></div><nav class="links" aria-label="切换方案">{links}</nav></header>
<section class="shell" aria-label="LANPUT 场景预览"><header class="app-head"><div class="app-brand"><img src="{LOGO}" alt=""><strong>LANPUT</strong><span style="color:var(--muted)">私人放映室</span></div><div class="app-state"><i aria-hidden="true"></i><span>场景预览 · </span><b class="state-label" style="font-weight:400">私人放映室</b></div></header>
<div class="scene"><section class="cinema" aria-label="影院场景">{hall()}<div class="screen">{POSTER}<div class="screen-copy"><img src="{LOGO}" alt="LANPUT"><h2>今晚，坐进故事里</h2><p>手机选好影片，投屏，剩下的交给LANPUT。</p></div></div><div class="lobby-hint">好座位，已经为你留好</div></section>
<section class="music" aria-label="黑胶音乐厅" inert><div class="music-tools">歌词</div><div class="disc-position">{record}</div><div class="music-meta"><h2>夜航</h2><p>LANPUT STUDIO</p><small>午夜留声</small></div><div class="lyrics"><p class="far">把城市的喧嚣留在身后</p><p>让夜色慢慢流淌</p><p class="active">沿着夜色慢慢远航</p><p>让每一颗星为我们亮起</p></div></section>
<div class="dimmer" aria-hidden="true"></div><div class="bridge" aria-hidden="true"><div class="bridge-object"><div class="bridge-poster">{POSTER}</div><div class="bridge-record"><span>LANPUT</span></div></div></div>
<div class="curtains" aria-hidden="true"><div class="curtain left"></div><div class="curtain right"></div></div><div class="interlude" aria-hidden="true"><img src="{LOGO}" alt=""><p>这一场，听音乐</p><small>私人音乐厅</small></div></div>
<footer class="transport"><div class="track"><strong>手机投屏影片</strong><small>等待投屏</small></div><button class="play" disabled aria-label="暂停模拟播放"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 4h4v16H6zM14 4h4v16h-4z"/></svg></button><div class="transport-right"><span>播放信息</span><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 9h5l5-4v14l-5-4H3zM17 8a6 6 0 0 1 0 8"/></svg><div class="track-line"></div></div></footer></section>
<footer class="review"><div class="actions"><button id="toMusic" class="primary">模拟收到音乐</button><button id="toCinema">返回影院</button><button id="replay">重播</button></div><div class="review-time"><label for="scrub" style="white-space:nowrap">转场进度</label><input id="scrub" type="range" min="0" max="1000" value="0" aria-label="逐帧查看转场进度"><span class="timing"></span></div><div class="preferences"><label><input id="reduce" type="checkbox">减弱动态效果</label></div><p class="demo-note"><span>{note} · {duration:.1f} 秒 · 打开后自动演示一次，可随时打断或返回</span><span>独立离线 HTML · 原创示例歌词 · 模拟界面，无真实投屏或声音</span></p></footer><p id="announcement" role="status" aria-live="polite" style="position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)"></p></main>
<script>const VARIANT={number},DURATION={duration};{JS}</script></body></html>'''
    (OUT/filename).write_text(page,encoding='utf-8')

cards=''.join(f'<a class="option" href="{file}"><span>{n:02d}</span><div><h2>{title}</h2><p>{desc}</p><small>{note} · {duration:.1f} 秒</small></div><b aria-hidden="true">→</b></a>' for n,file,title,desc,duration,note in VARIANTS)
(OUT/'index.html').write_text(f'''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>LANPUT · 从影院到音乐厅</title><style>:root{{color-scheme:dark;font-family:"Segoe UI","Microsoft YaHei UI",sans-serif}}*{{box-sizing:border-box}}body{{margin:0;background:#0e1318;color:#eee9df}}main{{max-width:940px;margin:8vh auto;padding:24px}}header{{margin-bottom:44px}}header img{{width:144px;height:104px;object-fit:contain}}h1{{font-size:36px;font-weight:500;margin:28px 0 16px}}header p{{color:#a9b1ba;line-height:1.8;max-width:680px}}.option{{display:flex;align-items:center;gap:28px;padding:30px 0;border-top:1px solid #2b333c;color:inherit;text-decoration:none}}.option>span{{color:#e8c87a;font-size:18px}}.option div{{flex:1}}h2{{font-size:24px;font-weight:500;margin:0 0 12px}}.option p{{color:#a9b1ba;line-height:1.7;margin:0 0 12px}}small{{color:#8f99a5}}b{{font-size:24px;color:#e8c87a;font-weight:400}}.option:hover h2{{color:#e8c87a}}a:focus-visible{{outline:2px solid #e8c87a;outline-offset:6px}}footer{{margin-top:34px;color:#8f99a5;font-size:13px;line-height:1.8}}@media(max-width:600px){{h1{{font-size:29px}}.option{{gap:16px}}main{{margin:24px auto}}}}</style></head><body><main><header><img src="{LOGO}" alt="LANPUT"><h1>从电影院，到私人音乐厅</h1><p>三个独立转场研究，保留深蓝与暖金、影院观众席和黑胶音乐厅。比较的是进入音乐的方式，而不是三套不同的播放器。</p></header>{cards}<footer>每个方案可单独复制使用，双击 HTML 即可打开，不依赖网络。<br>自动演示一次；支持反向返回、重播、进度拖动、暂停唱片和减少动态效果。此轮仅做演示，未修改软件。</footer></main></body></html>''',encoding='utf-8')
print('Built three standalone HTML files and index in',OUT)
