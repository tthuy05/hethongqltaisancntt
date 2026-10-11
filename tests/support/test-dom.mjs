// Small deterministic DOM for page behavior tests, not a browser layout substitute.
export class TestNode {
  constructor(tag = '#text', text = '') { this.tagName=tag.toUpperCase();this.data=text;this.childNodes=[];this.attributes=new Map();this.dataset={};this.handlers=new Map();this.value='';this.disabled=false;this.style={setProperty(){}};this.className='';
    this.classList={contains:n=>this.className.split(/\s+/).includes(n),add:n=>{this.className+=' '+n;},remove:n=>{this.className=this.className.split(/\s+/).filter(x=>x!==n).join(' ');}}; }
  get textContent(){return this.tagName==='#TEXT'?this.data:this.childNodes.map(n=>n.textContent).join('');}
  set textContent(v){this.replaceChildren(new TestNode('#text',String(v)));}
  get isConnected(){let n=this;while(n.parentNode)n=n.parentNode;return n.tagName==='BODY';}
  get children(){return this.childNodes.filter(n=>n.tagName!=='#TEXT');}
  append(...nodes){for(const n of nodes){n.remove();n.parentNode=this;this.childNodes.push(n);}}
  replaceChildren(...nodes){this.childNodes.forEach(n=>n.parentNode=null);this.childNodes=[];this.append(...nodes);}
  remove(){if(this.parentNode){this.parentNode.childNodes=this.parentNode.childNodes.filter(n=>n!==this);this.parentNode=null;}}
  setAttribute(n,v){this.attributes.set(n,String(v));if(n==='class')this.className=String(v);}
  getAttribute(n){return this.attributes.get(n)??null;}
  removeAttribute(n){this.attributes.delete(n);}
  addEventListener(n,f){const a=this.handlers.get(n)??[];a.push(f);this.handlers.set(n,a);}
  fire(n){for(const f of this.handlers.get(n)??[])f({target:this,currentTarget:this,preventDefault(){},stopImmediatePropagation(){}});}
  querySelectorAll(selector){const [tag,cls]=selector.split('.');return descendants(this).filter(n=>(!tag||n.tagName.toLowerCase()===tag)&&(!cls||n.classList.contains(cls)));}
  querySelector(s){return this.querySelectorAll(s)[0]??null;}
  focus(){} showModal(){} close(){this.fire('close');}
}
export const descendants=n=>n.childNodes.flatMap(c=>[c,...descendants(c)]);
export async function until(test){const end=Date.now()+2000;while(!test()){if(Date.now()>end)throw Error('DOM state did not settle');await new Promise(r=>setTimeout(r,5));}}
export function installDom(hash='/permissions'){
  const keys=['Node','document','window','location'];const old=new Map(keys.map(k=>[k,Object.getOwnPropertyDescriptor(globalThis,k)]));
  const body=new TestNode('body');const doc={body,createElement:t=>new TestNode(t),createElementNS:(_n,t)=>new TestNode(t),createTextNode:t=>new TestNode('#text',t),querySelector:s=>body.querySelector(s),querySelectorAll:s=>body.querySelectorAll(s),getElementById:id=>descendants(body).find(n=>n.getAttribute('id')===id),addEventListener(){}};
  const location={hash:'#'+hash,pathname:'/',search:''};const history=[];
  const win={location,history:{replaceState(_s,_t,url){history.push(url);location.hash=url.slice(url.indexOf('#'));}},addEventListener(){},dispatchEvent(){},setTimeout(){return 0;}};
  Object.assign(globalThis,{Node:TestNode,document:doc,window:win,location});
  return{body,history,restore(){for(const[k,d]of old){if(d)Object.defineProperty(globalThis,k,d);else delete globalThis[k];}}};
}
