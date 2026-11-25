namespace CCPetomar;

// Skript vložený do stránky CC: sleduje prichádzajúci hovor a nové správy
// (rovnaká logika ako plocha – pickCall(), S.spravy) a hlási ich programu.
static class Skript
{
    public const string JS = """
(function(){
 if(!window.chrome||!chrome.webview)return;
 var post=function(o){try{chrome.webview.postMessage(o)}catch(e){}};
 var zvonil={},videne={},init=false,posl='',hkluc='';
 // v11: šípka na pravom okraji okna (1/3 zdola), 📱 = mobilný náhľad = schovať CC do lišty; pod ňou monitory 1, 2… = kam dať lištu
 var SVG_MON=function(n){return '<svg width="18" height="15" viewBox="0 0 24 20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="2" y="2" width="20" height="13" rx="2"></rect><path d="M8 19h8M12 15v4"></path><text x="12" y="11.5" text-anchor="middle" font-size="8.5" font-weight="700" fill="currentColor" stroke="none" font-family="Segoe UI,sans-serif">'+n+'</text></svg>'};
 function ltab(){
  var t=document.getElementById('ccTab');
  if(!window.ME||!document.body){if(t)t.style.display='none';return}
  if(!t){
   t=document.createElement('div');t.id='ccTab';
   t.style.cssText='position:fixed;right:0;bottom:33vh;z-index:45;display:flex;flex-direction:column;align-items:center;gap:4px;padding:6px 3px;background:var(--surface);border:1px solid var(--line);border-right:none;border-radius:12px 0 0 12px;box-shadow:var(--shadow)';
   t.addEventListener('click',function(e){var b=e.target.closest('button');if(!b)return;if(b.dataset.m)post({typ:'monitor',i:Number(b.dataset.m)});else if(b.dataset.v)post({typ:'mobil'});else post({typ:'lista'})});
   document.body.appendChild(t);
  }
  t.style.display='flex';
  var M=window.__ccMon||{n:1,akt:1,mob:false},k=M.n+'|'+M.akt+'|'+M.mob;if(t._k===k)return;t._k=k;
  var bs='display:flex;align-items:center;justify-content:center;border:1px solid var(--line);background:var(--surface2);cursor:pointer;padding:0;';
  var h='<button type="button" title="Schovať CC do bočnej lišty" aria-label="Schovať do lišty" style="'+bs+'width:24px;height:38px;border-radius:9px;color:var(--accent);border-color:var(--accent)"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h13M13 6l6 6-6 6"></path></svg></button>';
  if(M.n>1)for(var i=1;i<=M.n;i++){var on=i===M.akt;h+='<button type="button" data-m="'+i+'" aria-pressed="'+on+'" title="Lišta na monitore '+i+(on?' (teraz)':'')+'" style="'+bs+'width:24px;height:24px;border-radius:7px;'+(on?'background:var(--accent);color:var(--accentInk);border-color:var(--accent)':'color:var(--muted)')+'">'+SVG_MON(i)+'</button>'}
  h+='<button type="button" data-v="1" aria-pressed="'+!!M.mob+'" title="'+(M.mob?'Späť na zobrazenie pre počítač':'Mobilný náhľad – CC v šírke telefónu')+'" style="'+bs+'width:24px;height:26px;border-radius:7px;margin-top:2px;'+(M.mob?'background:var(--accent);color:var(--accentInk);border-color:var(--accent)':'color:var(--muted)')+'">'+(M.mob?'<svg width="15" height="14" viewBox="0 0 24 20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="2" y="2" width="20" height="13" rx="2"></rect><path d="M8 19h8M12 15v4"></path></svg>':'<svg width="12" height="16" viewBox="0 0 14 22" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="1.5" y="1.5" width="11" height="19" rx="2.5"></rect><path d="M5.5 17.5h3"></path></svg>')+'</button>';
  t.innerHTML=h;
 }
 setInterval(ltab,500);
 function kontrola(){
  try{
   if(typeof pickCall!=='function'||!window.S||!window.ME)return;
   var p=pickCall(),f=p?p.f:'',h=p?p.h:null;
   if(h){
    var id=String(h.id||h.ch||'');
    var zv=(f==='zvoni'||f==='hlaska'||(f==='int'&&!h.up));
    if(zv&&id&&!zvonil[id]){
     zvonil[id]=1;
     var kto=f==='int'?(MENA[h.druhy]||h.druhy):(typeof nmBlk==='function'?nmBlk(h.cislo,h.meno).big:(h.meno||h.cislo||''));
     post({typ:'hovor',id:id,f:f,kto:String(kto||'')});
    }
   }
   // pri hovore (zvoní / prijatý / spätné volanie / interný) obraz vždy úplne hore
   var cb=typeof myCB==='function'?myCB():null;
   var hk=h&&f!=='koniec'?(String(h.id||h.ch||'')+'|'+f):(cb&&!cb.koniec?('cb'+cb.id+'|'+cb.faza):'');
   if(hk&&hk!==hkluc){try{window.scrollTo({top:0,behavior:'smooth'})}catch(e){}}
   hkluc=hk;
   (S.spravy||[]).forEach(function(s){
    if(s.komu!==ME||s.precitane||videne[s.id])return;
    videne[s.id]=1;
    if(init)post({typ:'sprava',id:s.id,od:String(MENA[s.od]||s.od),text:String(s.text||'')});
   });
   init=true;
   var karta=!!(p||cb||window.IC);
   var st=JSON.stringify({typ:'stav',ja:ME,meno:MENA[ME]||'',hovor:!!(h&&(f==='zvoni'||f==='hovor'||f==='hlaska'||f==='int')),karta:karta,st:(typeof opStav==='function'?opStav(ME):''),ms:(typeof msOpenCount==='function'?msOpenCount():0)});
   if(st!==posl){posl=st;post(JSON.parse(st))}
  }catch(e){}
 }
 // po každej zmene zo servera (SSE → renderAll) a aj každú sekundu
 var hook=setInterval(function(){
  if(typeof window.renderAll==='function'&&!window.renderAll._cc){
   var o=window.renderAll;window.renderAll=function(){var r=o.apply(this,arguments);kontrola();return r};window.renderAll._cc=1;
  }
 },300);
 setInterval(kontrola,1000);
 // zvuky bez kliknutia (program povoľuje prehrávanie)
 var zv=setInterval(function(){
  if(typeof zvukUnlock==='function'){try{zvukUnlock(0)}catch(e){}}
  if(typeof zvOn==='function'&&zvOn())clearInterval(zv);
 },1500);
})();
""";

    public static string Chyba(string server) => """
<!doctype html><html lang="sk"><head><meta charset="utf-8"><style>
body{margin:0;height:100vh;display:flex;align-items:center;justify-content:center;background:#0A1116;color:#E6EDF1;font:16px 'Segoe UI',sans-serif}
.k{max-width:460px;padding:28px 32px;border:1px solid #24343E;border-radius:16px;background:#111B22}
h1{margin:0 0 10px;font-size:22px;color:#F2B04C}.m{color:#9AAAB4;font-size:14px;line-height:1.5}
</style></head><body><div class="k"><h1>Server CC nie je dostupný</h1>
<div class="m">Program sa nevie spojiť so serverom <b>
""" + System.Net.WebUtility.HtmlEncode(server) + """
</b>.<br>Skúšam to znova každých 10 sekúnd.<br><br>Skontrolujte, či je počítač pripojený do siete firmy.</div></div></body></html>
""";
}
