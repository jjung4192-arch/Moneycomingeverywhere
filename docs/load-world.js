const label=document.getElementById('world-status');
let ready=false;
const timeout=setTimeout(()=>{if(!ready)label.textContent='3D loading slowly · Please open in Safari or Chrome / 3D 로딩 중 · Safari 또는 Chrome에서 열어주세요.';},12000);
try{await import('./world.js?v=2');ready=true;clearTimeout(timeout);}catch(error){clearTimeout(timeout);label.textContent='3D를 불러오지 못했어요. Safari/Chrome에서 다시 열어주세요. · 3D could not load.';label.style.padding='25px';console.error('3D initialization failed',error);}
