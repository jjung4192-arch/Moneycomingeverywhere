const $=id=>document.getElementById(id);
const KEY='jai-demo-v1';let state={name:'Mali',color:'#bd795e',moments:0,lang:'th'};
try{const saved=JSON.parse(localStorage.getItem(KEY));if(saved&&typeof saved.name==='string'&&['#bd795e','#648d80','#9290b8'].includes(saved.color)&&Number.isSafeInteger(saved.moments)&&saved.moments>=0&&['th','en'].includes(saved.lang))state=saved;}catch{}
let end=null,interval=null,status='';
function save(){try{localStorage.setItem(KEY,JSON.stringify(state));}catch{status='storage';}}
function render(){$('companion-name').textContent=state.name;$('avatar').style.setProperty('--shirt',state.color);$('count').textContent=state.moments;document.documentElement.lang=state.lang;document.querySelectorAll('[data-th]').forEach(el=>{el.textContent=el.dataset[state.lang];});$('language').textContent=state.lang==='th'?'EN':'ไทย';const messages={done:['ขอบคุณที่ให้เวลาตัวเอง','Thank you for taking a moment for yourself.'],stopped:['พักได้ทุกเมื่อ กลับมาเมื่อพร้อมนะ','You can always come back when you feel ready.'],storage:['บันทึกข้อมูลไม่ได้ ข้อมูลอาจหายเมื่อปิดหน้านี้','Storage is unavailable. Your progress may not be saved.']};$('status').textContent=messages[status]?.[state.lang==='th'?0:1]||'';}
function stop(){clearInterval(interval);interval=null;end=null;$('breath').classList.remove('running');$('start').hidden=false;$('stop').hidden=true;}
function tick(){if(end===null)return;const seconds=Math.max(0,Math.ceil((end-Date.now())/1000));$('timer').textContent=`${Math.floor(seconds/60)}:${String(seconds%60).padStart(2,'0')}`;if(seconds===0){stop();state.moments++;status='done';save();render();}}
$('visit').onclick=()=>{$('practice').hidden=false;$('practice').scrollIntoView({behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth',block:'start'});};
$('start').onclick=()=>{if(end!==null)return;status='';render();end=Date.now()+60000;$('start').hidden=true;$('stop').hidden=false;$('breath').classList.add('running');tick();interval=setInterval(tick,250);};
$('stop').onclick=()=>{stop();$('timer').textContent='1:00';status='stopped';render();};
document.addEventListener('visibilitychange',()=>{if(!document.hidden)tick();});
$('customize').onclick=()=>{$('name').value=state.name;$('color').value=state.color;$('dialog').showModal();};
$('cancel').onclick=()=>$('dialog').close();
$('avatar-form').onsubmit=event=>{event.preventDefault();const name=$('name').value.trim();if(!name){$('name').focus();return;}state.name=name;state.color=$('color').value;save();render();$('dialog').close();};
$('language').onclick=()=>{state.lang=state.lang==='th'?'en':'th';save();render();};
$('clear').onclick=()=>{if(!confirm(state.lang==='th'?'ล้างชื่อและบันทึกทั้งหมดบนอุปกรณ์นี้?':'Clear your name and all moments on this device?'))return;stop();state={name:'Mali',color:'#bd795e',moments:0,lang:state.lang};status='';try{localStorage.removeItem(KEY);}catch{status='storage';}$('timer').textContent='1:00';render();};
render();
