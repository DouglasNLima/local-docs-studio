import test from 'node:test';
import assert from 'node:assert/strict';
import {qualifyInterruption,checkPresentation} from '../../scripts/native/completion-audit.mjs';

function pending() {
  return {staleReady:0,deliveries:[{operation:1,reader:'primary',tick:20,pendingTask:true,sameCancellation:true,cancellationRequested:false,action:'close',inputTarget:'CloseComparison'}],
    operations:[{id:1,reader:'primary',start:10,end:30,cancelled:25,outcome:'rejected'}]};
}
const native=[{boundary:'native-input',offered:1,tick:19,message:0x202}];
test('qualification needs native delivery inside the actual unfinished operation',()=> {
  assert.equal(qualifyInterruption(pending(),native),'PASS');
  for(const patch of [{pendingTask:false},{sameCancellation:false},{cancellationRequested:true},{tick:31},{inputTarget:'Other'}]) {
    const data=pending();Object.assign(data.deliveries[0],patch);
    assert.equal(qualifyInterruption(data,native),'INCONCLUSIVE');
  }
  assert.equal(qualifyInterruption(pending(),[]),'INCONCLUSIVE');
});
test('accepted interruption must cancel its own task and cannot publish stale Ready',()=> {
  for(const patch of [{outcome:'restored'},{cancelled:null},{cancelled:19}]) {
    const data=pending();Object.assign(data.operations[0],patch);
    assert.equal(qualifyInterruption(data,native),'FAIL');
  }
  assert.equal(qualifyInterruption({...pending(),staleReady:1},native),'FAIL');
});
test('keyboard qualification needs the native key-down before WPF consumes it',()=> {
  const data=pending();data.deliveries[0].action='find';
  assert.equal(qualifyInterruption(data,[{...native[0],message:0x101,key:27}]),'INCONCLUSIVE');
  assert.equal(qualifyInterruption(data,[{...native[0],message:0x100,key:27,source:'thread-filter'}]),'PASS');
});
function state() {
  const reader={isVisible:true,currentOwner:true,attachedMatches:true,itemStatus:'Ready',isMeasureValid:true,isArrangeValid:true,source:40,
    textPattern:[{meaningful:true,rectangles:[1,2,3,4]}],mode:'Page',pageNumber:4,pageCount:5,nativePage:4,nativeCount:5,pageText:'Page 4 of 5',pageUia:'Page 4 of 5'};
  return {previewStatus:'Preview updated',statusText:'Preview updated',statusUia:'Preview updated',primary:{...reader},secondary:{...reader}};
}
test('semantic Ready does not conceal stale page counters, UIA, ownership or missing text',()=> {
  assert.deepEqual(checkPresentation(state()),[]);
  for(const patch of [{nativePage:5},{pageText:'Page 1 of 5'},{pageUia:'Page 1 of 5'},{currentOwner:false},{textPattern:[]}]) {
    const data=state();Object.assign(data.secondary,patch);assert.ok(checkPresentation(data).length);
  }
  const data=state();data.statusText='Preview layout changed';assert.ok(checkPresentation(data).length);
});
test('an unfinished independent pane is not completion evidence and Ready still requires full checks',()=> {
  const data=state();Object.assign(data.secondary,{itemStatus:'Busy; comparison preview is being updated',currentOwner:false,textPattern:[]});
  assert.ok(checkPresentation(data).length);
  assert.deepEqual(checkPresentation(data,['primary']),[]);
  data.secondary.itemStatus='Ready';assert.ok(checkPresentation(data,['primary']).length);
  data.primary.currentOwner=false;assert.ok(checkPresentation(data,['primary']).length);
});
