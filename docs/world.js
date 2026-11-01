import * as THREE from './vendor/three.module.min.js';
const host=document.getElementById('world'),status=document.getElementById('world-status');
const words=()=>document.documentElement.lang==='th'?['เดินผ่านประตูเพื่อเข้าสู่ศาลา','ภายในศาลา · พักใจได้ตามสบาย','อุปกรณ์นี้ไม่รองรับ 3D ลองเปิดด้วยเบราว์เซอร์รุ่นใหม่']:['Walk through the doorway to enter','Inside the pavilion · take a quiet moment','3D is unavailable. Try an updated browser.'];
let renderer;
try{renderer=new THREE.WebGLRenderer({antialias:true,alpha:false});}catch{status.textContent=words()[2];}
if(renderer){
 renderer.setPixelRatio(Math.min(devicePixelRatio,1.3));renderer.shadowMap.enabled=!matchMedia('(pointer: coarse)').matches;renderer.shadowMap.type=THREE.PCFSoftShadowMap;renderer.setClearColor(0xe4eadb);host.appendChild(renderer.domElement);renderer.domElement.addEventListener('webglcontextlost',e=>{e.preventDefault();document.getElementById('enter-temple').disabled=true;status.textContent=document.documentElement.lang==='th'?'กราฟิกหยุดทำงาน กรุณาโหลดหน้าใหม่':'Graphics stopped. Please reload the page.';});
 const scene=new THREE.Scene();scene.fog=new THREE.Fog(0xe4eadb,24,52);const camera=new THREE.PerspectiveCamera(46,1,.1,80);
 scene.add(new THREE.HemisphereLight(0xfff8e5,0x667c59,2.5));const sun=new THREE.DirectionalLight(0xffedca,3);sun.position.set(-8,14,8);sun.castShadow=true;sun.shadow.mapSize.set(1024,1024);sun.shadow.camera.left=-18;sun.shadow.camera.right=18;sun.shadow.camera.top=18;sun.shadow.camera.bottom=-18;sun.shadow.normalBias=.06;scene.add(sun);
 const mat=color=>new THREE.MeshStandardMaterial({color,roughness:.9});
 const cream=mat(0xf0dfb9),gold=mat(0xcdb078),roofmat=mat(0xa85f4c),wood=mat(0x775b40),grass=mat(0xb6c49f),green=mat(0x77936d),stone=mat(0xe5d6b7);
 function mesh(geometry,material,x,y,z,parent=scene){const m=new THREE.Mesh(geometry,material);m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;parent.add(m);return m;}
 function box(w,h,d,m,x,y,z,parent){return mesh(new THREE.BoxGeometry(w,h,d),m,x,y,z,parent);}
 function sphere(r,m,x,y,z,parent){return mesh(new THREE.SphereGeometry(r,16,12),m,x,y,z,parent);}
 box(70,.2,70,grass,0,-.2,0);box(2.8,.05,15,stone,0,-.06,7);
 const building=new THREE.Group();scene.add(building);box(10,.18,9,stone,0,-.04,-4.5,building);
 // A real doorway, side and rear walls; the avatar collides with their bounds.
 const facade=new THREE.Group();building.add(facade);box(3.6,3.4,.3,cream,-3.2,1.7,0,facade);box(3.6,3.4,.3,cream,3.2,1.7,0,facade);box(2.8,.7,.3,cream,0,3.05,0,facade);
 box(.3,3.4,9,cream,-5,1.7,-4.5,building);box(.3,3.4,9,cream,5,1.7,-4.5,building);box(10,3.4,.3,cream,0,1.7,-9,building);
 for(const x of [-4.5,-1.5,1.5,4.5]){box(.19,3.4,.3,gold,x,1.7,.21,facade);}
 const roofs=new THREE.Group();scene.add(roofs);
 function roof(w,d,y){const geo=new THREE.BufferGeometry();const v=[-w/2,0,-d/2,w/2,0,-d/2,0,1.7,-d/2,-w/2,0,d/2,w/2,0,d/2,0,1.7,d/2];geo.setAttribute('position',new THREE.Float32BufferAttribute(v,3));geo.setIndex([0,2,1,3,4,5,0,3,5,0,5,2,2,5,4,2,4,1,0,1,4,0,4,3]);geo.computeVertexNormals();const m=mesh(geo,roofmat,0,y,-4.5,roofs);for(const z of [-d/2-4.5,d/2-4.5]){const curve=new THREE.CatmullRomCurve3([new THREE.Vector3(-w/2,y,z),new THREE.Vector3(0,y+1.7,z),new THREE.Vector3(w/2,y,z)]);mesh(new THREE.TubeGeometry(curve,12,.07,6,false),gold,0,0,0,roofs);}return m;}
 roof(11.5,10,3.4);roof(8.8,8,4.6);
 // Simple interior: quiet cushions and flowers, with no simulated sacred figure.
 for(const x of [-2,0,2]){box(1.1,.12,1.1,mat(0xb79b77),x,.16,-6,building);}
 box(3,.35,1,wood,0,.2,-8,building);for(const x of [-1,1]){mesh(new THREE.CylinderGeometry(.12,.2,.45,12),gold,x,.65,-8,building);for(let j=0;j<4;j++)sphere(.12,mat(0xead7b3),x+Math.sin(j*2)*.15,1+j*.06,-8+Math.cos(j*2)*.15,building);}
 for(const [x,z]of[[-8,1],[8,2],[-8,-7],[8,-8],[-7,9],[8,10]]){box(.35,2,.35,wood,x,.9,z);sphere(1.5,green,x,2.8,z);sphere(1.1,mat(0x90a879),x+.6,3.4,z-.2);}
 for(const x of [-2.7,2.7])for(const z of [3,7,11]){box(.35,.45,.35,gold,x,.2,z);sphere(.18,mat(0xf5e7b7),x,.6,z);}
 const avatar=new THREE.Group();scene.add(avatar);avatar.position.set(0,0,8);const shirt=mat(0xbd795e),skin=mat(0xecc29c),hair=mat(0x433e32),pants=mat(0x5e7060);
 mesh(new THREE.CapsuleGeometry(.3,.4,5,12),shirt,0,.95,0,avatar);sphere(.32,skin,0,1.64,0,avatar);const hairMesh=sphere(.335,hair,0,1.73,.025,avatar);hairMesh.scale.y=.65;
 for(const x of [-.12,.12])sphere(.025,mat(0x343e32),x,1.64,-.3,avatar);
 const legs=[];for(const x of [-.15,.15]){const leg=new THREE.Group();leg.position.set(x,.62,0);avatar.add(leg);mesh(new THREE.CapsuleGeometry(.105,.32,4,8),pants,0,-.25,0,leg);legs.push(leg);mesh(new THREE.CapsuleGeometry(.09,.3,4,8),skin,x*2.65,1,0,avatar);}
 const pressed=new Set(),touch=new Set();let inside=false,previous=performance.now(),phase=0,autoEnter=false;
 const enter=document.getElementById('enter-temple');enter.disabled=false;
 enter.onclick=()=>{avatar.position.set(0,0,8);resetInput();autoEnter=true;host.scrollIntoView({block:'center',behavior:'smooth'});};
 const keys={ArrowUp:'up',w:'up',W:'up',ArrowDown:'down',s:'down',S:'down',ArrowLeft:'left',a:'left',A:'left',ArrowRight:'right',d:'right',D:'right'};
 const editing=()=>['INPUT','SELECT','TEXTAREA'].includes(document.activeElement.tagName)||document.getElementById('dialog').open;
 window.addEventListener('keydown',e=>{if(keys[e.key]&&!editing()){e.preventDefault();autoEnter=false;pressed.add(keys[e.key]);}});window.addEventListener('keyup',e=>pressed.delete(keys[e.key]));
 const resetInput=()=>{pressed.clear();touch.clear();autoEnter=false;};window.addEventListener('blur',resetInput);document.addEventListener('visibilitychange',resetInput);
 document.querySelectorAll('[data-move]').forEach(button=>{button.addEventListener('pointerdown',e=>{e.preventDefault();autoEnter=false;button.setPointerCapture(e.pointerId);touch.add(button.dataset.move);});for(const event of ['pointerup','pointercancel','lostpointercapture'])button.addEventListener(event,()=>touch.delete(button.dataset.move));});
 document.getElementById('home').onclick=()=>{avatar.position.set(0,0,8);resetInput();};
 function allowed(x,z){if(Math.abs(x)>12||z>13||z< -12)return false;const wall=.42;if(Math.abs(z)<wall&&Math.abs(x)>1.04&&Math.abs(x)<5.4)return false;if(Math.abs(Math.abs(x)-5)<wall&&z<.4&&z> -9.4)return false;if(Math.abs(z+9)<wall&&Math.abs(x)<5.4)return false;return true;}
 function resize(){const w=host.clientWidth,h=host.clientHeight;renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix();}new ResizeObserver(resize).observe(host);resize();camera.position.set(0,8,19);
 const target=new THREE.Vector3(),desired=new THREE.Vector3();
 function frame(now){requestAnimationFrame(frame);const dt=Math.min((now-previous)/1000,.05);previous=now;const held=d=>pressed.has(d)||touch.has(d);let dx=Number(held('right'))-Number(held('left')),dz=Number(held('down'))-Number(held('up'));if(autoEnter){dx=0;dz=-1;if(avatar.position.z<=-3){autoEnter=false;dz=0;}}if(editing()){dx=0;dz=0;}const length=Math.hypot(dx,dz);if(length){dx/=length;dz/=length;const speed=3;const nx=avatar.position.x+dx*dt*speed,nz=avatar.position.z+dz*dt*speed;if(allowed(nx,avatar.position.z))avatar.position.x=nx;if(allowed(avatar.position.x,nz))avatar.position.z=nz;avatar.rotation.y=Math.atan2(-dx,-dz);phase+=dt*10;legs[0].rotation.x=Math.sin(phase)*.5;legs[1].rotation.x=-Math.sin(phase)*.5;}else{legs.forEach(l=>l.rotation.x=0);}
 inside=Math.abs(avatar.position.x)<4.8&&avatar.position.z<-.4&&avatar.position.z> -8.7;roofs.visible=!inside;facade.visible=!inside;
 status.textContent=words()[inside?1:0];host.dataset.location=inside?'inside':'garden';
 const color=document.getElementById('avatar').style.getPropertyValue('--shirt');if(color)shirt.color.set(color);
 desired.set(avatar.position.x,inside?5:7,avatar.position.z+(inside?6:10));camera.position.lerp(desired,1-Math.exp(-dt*4));target.set(avatar.position.x,1,avatar.position.z-1.5);camera.lookAt(target);renderer.render(scene,camera);
 }requestAnimationFrame(frame);
}
