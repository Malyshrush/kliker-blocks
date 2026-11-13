const fs = require('fs');
const vm = require('vm');
const path = require('path');
const site = path.resolve(__dirname, '../../site/docs');
const html = fs.readFileSync(path.join(site, 'index.html'), 'utf8');
const code = fs.readFileSync(path.join(site, 'app.js'), 'utf8');
const keys = [...html.matchAll(/data-i18n="([^"]+)"/g)].map(match => match[1]);
const ariaKeys = [...html.matchAll(/data-i18n-aria="([^"]+)"/g)].map(match => match[1]);
function check(language) {
  const nodes = keys.map(key => ({dataset:{i18n:key},textContent:'',setAttribute(){}}));
  const aria = ariaKeys.map(key => ({dataset:{i18nAria:key},setAttribute(name,value){this[name]=value}}));
  const elements = new Map();
  const element = id => {
    if (!elements.has(id)) elements.set(id, {textContent:'',innerHTML:'',hidden:false,placeholder:'',value:'',
      handlers:{},addEventListener(name,fn){this.handlers[name]=fn},setAttribute(){},querySelectorAll(){return []},close(){},showModal(){}});
    return elements.get(id);
  };
  const context={document:{documentElement:{},title:'',getElementById:element,
      querySelectorAll(selector){return selector==='[data-i18n]'?nodes:selector==='[data-i18n-aria]'?aria:[]}},
    localStorage:{getItem(){return language},setItem(){}},navigator:{clipboard:{writeText:async()=>{}}},
    fetch:async()=>({ok:true,json:async()=>[]}),console};
  vm.createContext(context);
  vm.runInContext(code,context);
  const missing=nodes.filter(n=>!n.textContent.trim()).map(n=>n.dataset.i18n);
  const missingAria=aria.filter(n=>!n['aria-label']).map(n=>n.dataset.i18nAria);
  if(missing.length||missingAria.length)throw Error(language+' missing: '+[...missing,...missingAria].join(', '));
  const text=key=>nodes.find(n=>n.dataset.i18n===key)?.textContent;
  if(language==='ru' && (!/распознавание/i.test(text('f5title'))||!/текстовые/i.test(text('update128Title'))))
    throw Error('Russian first-load translations did not apply');
  if(language==='en' && (!/recognition/i.test(text('f5title'))||!/text files/i.test(text('update128Title'))))
    throw Error('English first-load translations did not apply');
  element('locale-switch').handlers.click();
  if(language==='ru' && !/recognition/i.test(text('f5title')))throw Error('Switch from Russian to English failed');
  if(language==='en' && !/распознавание/i.test(text('f5title')))throw Error('Switch from English to Russian failed');
  element('locale-switch').handlers.click();
  if(language==='ru' && !/распознавание/i.test(text('f5title')))throw Error('Switch back to Russian failed');
  console.log(language+': '+nodes.length+' translations and '+aria.length+' accessible labels present at startup and after switching.');

}
check('ru');check('en');
