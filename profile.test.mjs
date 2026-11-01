import {test} from 'node:test';
import assert from 'node:assert/strict';
import {parseProfile} from './public/profile.mjs';
test('profile links are normalized and tracking removed',()=>{
 assert.deepEqual(parseProfile('instagram.com/my.stock_1/?igsh=hello'),{username:'my.stock_1',url:'https://www.instagram.com/my.stock_1/'});
 assert.equal(parseProfile('https://www.instagram.com/test/').username,'test');
});
test('rejects unsafe links, non-profiles and malformed usernames',()=>{
 for(const input of ['https://evil.com/user','https://instagram.com.evil.com/user','javascript:alert(1)','https://user:pass@instagram.com/test','http://instagram.com/test','https://instagram.com:444/test','instagram.com/p/abc','instagram.com/reel/123','instagram.com/accounts','instagram.com/test/extra','instagram.com/a..b','instagram.com/.abc','instagram.com/'])assert.throws(()=>parseProfile(input),input);
});
