import http from 'node:http';
import {readFile} from 'node:fs/promises';
const files={'/':['index.html','text/html'],'/app.js':['app.js','text/javascript'],'/style.css':['style.css','text/css']};
http.createServer(async(req,res)=>{const route=files[new URL(req.url,'http://localhost').pathname]; if(!route){res.writeHead(404);return res.end('Not found');}try {const data=await readFile(new URL('./public/'+route[0],import.meta.url));res.writeHead(200,{'Content-Type':route[1]+'; charset=utf-8','X-Content-Type-Options':'nosniff','Content-Security-Policy':"default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; object-src 'none'; base-uri 'none'"});res.end(data);}catch{res.writeHead(500);res.end('Server error');}}).listen(Number(process.env.PORT||3000),'0.0.0.0');
