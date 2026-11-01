import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
test('serves the onboarding app and rejects unknown routes',async()=>{const child=spawn(process.execPath,['server.mjs'],{env:{...process.env,PORT:'39127'},stdio:'pipe'});try{let response;for(let i=0;i<40;i++){try{response=await fetch('http://127.0.0.1:39127');break;}catch{await new Promise(r=>setTimeout(r,50));}}assert.ok(response);assert.equal(response.status,200);assert.match(await response.text(),/프로필 링크/);for(const path of ['app.js','style.css','profile.mjs']){const r=await fetch('http://127.0.0.1:39127/'+path);assert.equal(r.status,200);assert.ok((await r.text()).length>100);}assert.equal((await fetch('http://127.0.0.1:39127/missing')).status,404);}finally{if(child.exitCode===null){const exited=once(child,'exit');child.kill();await exited;}}});
