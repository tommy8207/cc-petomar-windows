namespace CCPetomar;

// Skript vložený do stránky CC: sleduje prichádzajúci hovor a nové správy
// (rovnaká logika ako plocha – pickCall(), S.spravy) a hlási ich programu.
static class Skript
{
    public const string JS = """
(function(){
 if(!window.chrome||!chrome.webview)return;
 var post=function(o){try{chrome.webview.postMessage(o)}catch(e){}};
 var zvonil={},videne={},init=false,posl='';
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
   (S.spravy||[]).forEach(function(s){
    if(s.komu!==ME||s.precitane||videne[s.id])return;
    videne[s.id]=1;
    if(init)post({typ:'sprava',id:s.id,od:String(MENA[s.od]||s.od),text:String(s.text||'')});
   });
   init=true;
   var karta=!!(p||(typeof myCB==='function'&&myCB())||window.IC);
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
