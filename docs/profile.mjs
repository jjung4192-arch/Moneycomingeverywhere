export function parseProfile(value) {
 const raw=value.trim();
 if(!raw || /\s/.test(raw)) throw new Error('인스타그램 프로필 링크를 넣어주세요.');
 let url;
 try{url=new URL(/^[a-z][a-z\d+.-]*:/i.test(raw)?raw:`https://${raw}`);}catch{throw new Error('올바른 프로필 링크를 넣어주세요.');}
 if(url.protocol!=='https:' || !['instagram.com','www.instagram.com'].includes(url.hostname.toLowerCase()) || url.username || url.password || url.port) throw new Error('instagram.com으로 시작하는 프로필 링크를 넣어주세요.');
 const parts=url.pathname.split('/').filter(Boolean);
 const name=parts[0];
 if(parts.length!==1 || !/^[A-Za-z0-9_](?:[A-Za-z0-9_.]{0,28}[A-Za-z0-9_])?$/.test(name||'') || name.includes('..') || ['p','reel','reels','stories','explore','accounts','direct','about','developer','legal','privacy','terms'].includes(name.toLowerCase())) throw new Error('게시물 링크 대신 본인 프로필 링크를 넣어주세요.');
 return {username:name,url:`https://www.instagram.com/${name}/`};
}
