import {parseProfile} from './profile.mjs';
const $=id=>document.getElementById(id);
$('form').addEventListener('submit',event=>{
 event.preventDefault();$('error').textContent='';$('url').removeAttribute('aria-invalid');
 try {
  const account=parseProfile($('url').value);
  $('username').textContent=`@${account.username}`;
  $('profile').href=account.url;
  $('result').hidden=false;
  $('result').scrollIntoView({behavior:'smooth',block:'start'});
 }catch(error){$('result').hidden=true;$('error').textContent=error.message;$('url').setAttribute('aria-invalid','true');$('url').focus();}
});
$('reset').addEventListener('click',()=>{$('result').hidden=true;$('url').value='';$('url').focus();});
