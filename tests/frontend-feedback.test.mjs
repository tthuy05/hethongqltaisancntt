import test from 'node:test';
import assert from 'node:assert/strict';
import { permissionPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/permissions.js';
import { activationPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/activate.js';
import { userPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/users.js';
import { auditLogPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/audit-logs.js';
import { installDom, descendants, until } from './support/test-dom.mjs';
const version='AAAAAAAAAAAAAAAAAAAAAA==';
const page=items=>({items,totalPages:1,page:1,pageSize:100,totalItems:items.length});
const session={id:1,roles:['ADMIN_IT'],permissions:['roles.assign','roles.permissions.read','roles.read','users.read','users.create','departments.read','audit-logs.read']};
const base=()=>({mode:'api',auth:{session},departments:{list:async()=>page([{id:91,name:'Kỹ thuật',isActive:true}])}});

test('real matrix renders groups, saves dependencies, reloads and disables unavailable/protected rights',async()=>{
  const dom=installDom();try{
    const catalog=[{id:1,code:'assets.read',module:'assets',action:'Xem',available:true},{id:2,code:'assets.update',module:'assets',action:'Sửa',available:true,requires:'assets.read'},{id:3,code:'assignments.read',module:'assignments',action:'Xem',available:false}];
    const groups=[{id:11,name:'Admin IT',code:'ADMIN_IT',isSystem:true,isActive:true,permissions:catalog.map(p=>({id:p.id})),memberCount:1,rowVersion:version},{id:12,name:'Technical Support Lead',code:'CUSTOM_LEAD',isActive:true,isSystem:false,permissions:[],memberCount:2,rowVersion:version}];let writes=[];
    const services={...base(),roles:{list:async()=>page(groups),matrix:async()=>catalog,get:async id=>groups.find(g=>g.id===id),replacePermissions:async(id,dto)=>{writes.push({id,dto});return{...groups[1],permissions:dto.permissionIds.map(id=>({id})),rowVersion:version};}}};
    const root=await permissionPage(services);dom.body.append(root);await until(()=>root.querySelector('table'));
    assert.ok(root.querySelectorAll('input').every(n=>n.disabled));
    root.querySelectorAll('button').find(n=>n.textContent.includes('Technical Support Lead')).fire('click');
    await until(()=>root.querySelectorAll('h2').some(n=>n.textContent==='Technical Support Lead'));
    const update=root.querySelectorAll('input').find(n=>n.getAttribute('aria-label')==='Quản lý tài sản Sửa');update.checked=true;update.fire('change');
    assert.equal(root.querySelectorAll('input').find(n=>n.getAttribute('aria-label')==='Quản lý tài sản Xem').checked,true);
    assert.equal(root.querySelectorAll('input').find(n=>n.getAttribute('aria-label')==='Cấp phát tài sản Xem').disabled,true);
    assert.ok(root.textContent.includes('chưa lưu'));
    root.querySelectorAll('button').find(n=>n.textContent==='Lưu phân quyền').fire('click');
    descendants(dom.body).find(n=>n.tagName==='BUTTON'&&n.textContent==='Xác nhận').fire('click');
    await until(()=>writes.length===1);assert.deepEqual(writes[0].dto.permissionIds.sort(),[1,2]);assert.equal(writes[0].dto.rowVersion,version);
    await until(()=>!root.textContent.includes('Bạn có thay đổi chưa lưu.'));
    await until(()=>root.querySelectorAll('button').find(n=>n.textContent==='Lưu phân quyền')?.disabled === true);
  }finally{dom.restore();}
});
test('non-Admin is denied matrix before fetching data',async()=>{
  await assert.rejects(permissionPage({mode:'api',auth:{session:{roles:['SYSTEM_MANAGER'],permissions:session.permissions}}}),e=>e.status===403);
});
test('public activation removes token from URL, validates confirmation, sends exact contract and clears passwords',async()=>{
  const token='A'.repeat(43);const dom=installDom('/activate?token='+token);try{
    let sent;const root=activationPage({mode:'api',auth:{activate:async dto=>{sent=dto;}}});dom.body.append(root);
    assert.equal(dom.history.at(-1),'/#/activate');assert.equal(root.textContent.includes(token),false);
    const inputs=root.querySelectorAll('input');inputs[0].value='Synthetic-password-2026!';inputs[1].value='mismatch';root.querySelector('form').fire('submit');assert.equal(sent,undefined);
    inputs[1].value=inputs[0].value;root.querySelector('form').fire('submit');await until(()=>root.textContent.includes('Kích hoạt thành công'));
    assert.deepEqual(sent,{token,password:'Synthetic-password-2026!',confirmPassword:'Synthetic-password-2026!'});assert.equal(inputs[0].value,'');assert.equal(inputs[1].value,'');
  }finally{dom.restore();}
});
test('create user form selects groups, contains no admin password and list/detail display department names',async()=>{
  const dom=installDom('/users');try{
    const user={id:3,username:'staff',email:'staff@fixture.test',displayName:'Nhân viên',departmentId:91,departmentName:'Kỹ thuật',isEmailConfirmed:false,invitationEmailStatus:'NotConfigured',rowVersion:version};
    const services={...base(),users:{list:async()=>page([user]),get:async()=>user,account:async()=>({roleIds:[7],rowVersion:version,effectivePermissions:['assets.read']})},roles:{list:async()=>page([{id:7,name:'Technical Support',isActive:true}])}};
    const root=await userPage(services);dom.body.append(root);assert.ok(root.textContent.includes('Kỹ thuật'));assert.equal(root.textContent.includes('#91'),false);
    root.querySelectorAll('button').find(n=>n.textContent==='Thêm người dùng').fire('click');await until(()=>dom.body.querySelector('dialog'));
    const dialog=dom.body.querySelector('dialog');assert.equal(dialog.querySelectorAll('input').some(n=>n.getAttribute('name')==='password'),false);assert.ok(dialog.textContent.includes('Technical Support'));assert.ok(dialog.textContent.includes('30 phút'));
  }finally{dom.restore();}
});
test('audit removes entity column/detail/filters and strips old entity query while preserving backend audit identities',async()=>{
  const dom=installDom('/audit-logs?entityType=Asset&entityId=9');try{
    let query;const services={...base(),auditLogs:{list:async q=>{query=q;return page([]);}}};const root=await auditLogPage(services);dom.body.append(root);
    assert.equal(Object.hasOwn(query,'entityType'),false);assert.equal(Object.hasOwn(query,'entityId'),false);assert.equal(root.textContent.includes('Đối tượng'),false);
    assert.equal(root.querySelectorAll('input').some(n=>['entityType','entityId'].includes(n.getAttribute('name'))),false);
  }finally{dom.restore();}
});
