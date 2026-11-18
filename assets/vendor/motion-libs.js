var OpenPortalMotion=(()=>{var so=Object.defineProperty;var xh=Object.getOwnPropertyDescriptor;var vh=Object.getOwnPropertyNames;var yh=Object.prototype.hasOwnProperty;var Mh=(c,e)=>{for(var t in e)so(c,t,{get:e[t],enumerable:!0})},Sh=(c,e,t,n)=>{if(e&&typeof e=="object"||typeof e=="function")for(let i of vh(e))!yh.call(c,i)&&i!==t&&so(c,i,{get:()=>e[i],enumerable:!(n=xh(e,i))||n.enumerable});return c};var bh=c=>Sh(so({},"__esModule",{value:!0}),c);var Sm={};Mh(Sm,{Group:()=>Wr,OrthographicCamera:()=>qr,ScrollTrigger:()=>Y,Vector3:()=>ee,gsap:()=>ea});function vn(c){if(c===void 0)throw new ReferenceError("this hasn't been initialised - super() hasn't been called");return c}function hl(c,e){c.prototype=Object.create(e.prototype),c.prototype.constructor=c,c.__proto__=e}var bt={autoSleep:120,force3D:"auto",nullTargetWarn:1,units:{lineHeight:""}},Ii={duration:.5,overwrite:!1,delay:0},wo,We,de,zt=1e8,le=1/zt,po=Math.PI*2,Th=po/4,wh=0,ul=Math.sqrt,Eh=Math.cos,Ah=Math.sin,Le=function(e){return typeof e=="string"},ye=function(e){return typeof e=="function"},Mn=function(e){return typeof e=="number"},ss=function(e){return typeof e>"u"},hn=function(e){return typeof e=="object"},St=function(e){return e!==!1},Eo=function(){return typeof window<"u"},$r=function(e){return ye(e)||Le(e)},fl=typeof ArrayBuffer=="function"&&ArrayBuffer.isView||function(){},Qe=Array.isArray,mo=/(?:-?\.?\d|\.)+/gi,Ao=/[-+=.]*\d+[.e\-+]*\d*[e\-+]*\d*/g,li=/[-+=.]*\d+[.e-]*\d*[a-z%]*/g,oo=/[-+=.]*\d+\.?\d*(?:e-|e\+)?\d*/gi,Co=/[+-]=-?[.\d]+/,dl=/[^,'"\[\]\s]+/gi,Ch=/^[+\-=e\s\d]*\d+[.\d]*([a-z]*|%)\s*$/i,me,ln,go,Ro,Rt={},es={},pl,ml=function(e){return(es=Di(e,Rt))&&je},os=function(e,t){return console.warn("Invalid property",e,"set to",t,"Missing plugin? gsap.registerPlugin()")},dr=function(e,t){return!t&&console.warn(e)},gl=function(e,t){return e&&(Rt[e]=t)&&es&&(es[e]=t)||Rt},pr=function(){return 0},Rh={suppressEvents:!0,isStart:!0,kill:!1},Kr={suppressEvents:!0,kill:!1},Ph={suppressEvents:!0},Po={},zn=[],_o={},_l,yt={},ao={},il=30,Qr=[],Io="",Do=function(e){var t=e[0],n,i;if(hn(t)||ye(t)||(e=[e]),!(n=(t._gsap||{}).harness)){for(i=Qr.length;i--&&!Qr[i].targetTest(t););n=Qr[i]}for(i=e.length;i--;)e[i]&&(e[i]._gsap||(e[i]._gsap=new Fo(e[i],n)))||e.splice(i,1);return e},kn=function(e){return e._gsap||Do(kt(e))[0]._gsap},Lo=function(e,t,n){return(n=e[t])&&ye(n)?e[t]():ss(n)&&e.getAttribute&&e.getAttribute(t)||n},ct=function(e,t){return(e=e.split(",")).forEach(t)||e},Me=function(e){return Math.round(e*1e5)/1e5||0},Re=function(e){return Math.round(e*1e7)/1e7||0},ci=function(e,t){var n=t.charAt(0),i=parseFloat(t.substr(2));return e=parseFloat(e),n==="+"?e+i:n==="-"?e-i:n==="*"?e*i:e/i},Ih=function(e,t){for(var n=t.length,i=0;e.indexOf(t[i])<0&&++i<n;);return i<n},ts=function(){var e=zn.length,t=zn.slice(0),n,i;for(_o={},zn.length=0,n=0;n<e;n++)i=t[n],i&&i._lazy&&(i.render(i._lazy[0],i._lazy[1],!0)._lazy=0)},Uo=function(e){return!!(e._initted||e._startAt||e.add)},xl=function(e,t,n,i){zn.length&&!We&&ts(),e.render(t,n,i||!!(We&&t<0&&Uo(e))),zn.length&&!We&&ts()},vl=function(e){var t=parseFloat(e);return(t||t===0)&&(e+"").match(dl).length<2?t:Le(e)?e.trim():e},yl=function(e){return e},Pt=function(e,t){for(var n in t)n in e||(e[n]=t[n]);return e},Dh=function(e){return function(t,n){for(var i in n)i in t||i==="duration"&&e||i==="ease"||(t[i]=n[i])}},Di=function(e,t){for(var n in t)e[n]=t[n];return e},rl=function c(e,t){for(var n in t)n!=="__proto__"&&n!=="constructor"&&n!=="prototype"&&(e[n]=hn(t[n])?c(e[n]||(e[n]={}),t[n]):t[n]);return e},ns=function(e,t){var n={},i;for(i in e)i in t||(n[i]=e[i]);return n},hr=function(e){var t=e.parent||me,n=e.keyframes?Dh(Qe(e.keyframes)):Pt;if(St(e.inherit))for(;t;)n(e,t.vars.defaults),t=t.parent||t._dp;return e},Lh=function(e,t){for(var n=e.length,i=n===t.length;i&&n--&&e[n]===t[n];);return n<0},Ml=function(e,t,n,i,r){n===void 0&&(n="_first"),i===void 0&&(i="_last");var s=e[i],o;if(r)for(o=t[r];s&&s[r]>o;)s=s._prev;return s?(t._next=s._next,s._next=t):(t._next=e[n],e[n]=t),t._next?t._next._prev=t:e[i]=t,t._prev=s,t.parent=t._dp=e,t},as=function(e,t,n,i){n===void 0&&(n="_first"),i===void 0&&(i="_last");var r=t._prev,s=t._next;r?r._next=s:e[n]===t&&(e[n]=s),s?s._prev=r:e[i]===t&&(e[i]=r),t._next=t._prev=t.parent=null},Vn=function(e,t){e.parent&&(!t||e.parent.autoRemoveChildren)&&e.parent.remove&&e.parent.remove(e),e._act=0},si=function(e,t){if(e&&(!t||t._end>e._dur||t._start<0))for(var n=e;n;)n._dirty=1,n=n.parent;return e},Uh=function(e){for(var t=e.parent;t&&t.parent;)t._dirty=1,t.totalDuration(),t=t.parent;return e},xo=function(e,t,n,i){return e._startAt&&(We?e._startAt.revert(Kr):e.vars.immediateRender&&!e.vars.autoRevert||e._startAt.render(t,!0,i))},Nh=function c(e){return!e||e._ts&&c(e.parent)},sl=function(e){return e._repeat?Li(e._tTime,e=e.duration()+e._rDelay)*e:0},Li=function(e,t){var n=Math.floor(e=Re(e/t));return e&&n===e?n-1:n},is=function(e,t){return(e-t._start)*t._ts+(t._ts>=0?0:t._dirty?t.totalDuration():t._tDur)},ls=function(e){return e._end=Re(e._start+(e._tDur/Math.abs(e._ts||e._rts||le)||0))},cs=function(e,t){var n=e._dp;return n&&n.smoothChildTiming&&e._ts&&(e._start=Re(n._time-(e._ts>0?t/e._ts:((e._dirty?e.totalDuration():e._tDur)-t)/-e._ts)),ls(e),n._dirty||si(n,e)),e},Sl=function(e,t){var n;if((t._time||!t._dur&&t._initted||t._start<e._time&&(t._dur||!t.add))&&(n=is(e.rawTime(),t),(!t._dur||_r(0,t.totalDuration(),n)-t._tTime>le)&&t.render(n,!0)),si(e,t)._dp&&e._initted&&e._time>=e._dur&&e._ts){if(e._dur<e.duration())for(n=e;n._dp;)n.rawTime()>=0&&n.totalTime(n._tTime),n=n._dp;e._zTime=-le}},cn=function(e,t,n,i){return t.parent&&Vn(t),t._start=Re((Mn(n)?n:n||e!==me?Bt(e,n,t):e._time)+t._delay),t._end=Re(t._start+(t.totalDuration()/Math.abs(t.timeScale())||0)),Ml(e,t,"_first","_last",e._sort?"_start":0),vo(t)||(e._recent=t),i||Sl(e,t),e._ts<0&&cs(e,e._tTime),e},bl=function(e,t){return(Rt.ScrollTrigger||os("scrollTrigger",t))&&Rt.ScrollTrigger.create(t,e)},Tl=function(e,t,n,i,r){if(zo(e,t,r),!e._initted)return 1;if(!n&&e._pt&&!We&&(e._dur&&e.vars.lazy!==!1||!e._dur&&e.vars.lazy)&&_l!==Mt.frame)return zn.push(e),e._lazy=[r,i],1},Fh=function c(e){var t=e.parent;return t&&t._ts&&t._initted&&!t._lock&&(t.rawTime()<0||c(t))},vo=function(e){var t=e.data;return t==="isFromStart"||t==="isStart"},Oh=function(e,t,n,i){var r=e.ratio,s=t<0||!t&&(!e._start&&Fh(e)&&!(!e._initted&&vo(e))||(e._ts<0||e._dp._ts<0)&&!vo(e))?0:1,o=e._rDelay,a=0,l,h,f;if(o&&e._repeat&&(a=_r(0,e._tDur,t),h=Li(a,o),e._yoyo&&h&1&&(s=1-s),h!==Li(e._tTime,o)&&(r=1-s,e.vars.repeatRefresh&&e._initted&&e.invalidate())),s!==r||We||i||e._zTime===le||!t&&e._zTime){if(!e._initted&&Tl(e,t,i,n,a))return;for(f=e._zTime,e._zTime=t||(n?le:0),n||(n=t&&!f),e.ratio=s,e._from&&(s=1-s),e._time=0,e._tTime=a,l=e._pt;l;)l.r(s,l.d),l=l._next;t<0&&xo(e,t,n,!0),e._onUpdate&&!n&&Ct(e,"onUpdate"),a&&e._repeat&&!n&&e.parent&&Ct(e,"onRepeat"),(t>=e._tDur||t<0)&&e.ratio===s&&(s&&Vn(e,1),!n&&!We&&(Ct(e,s?"onComplete":"onReverseComplete",!0),e._prom&&e._prom()))}else e._zTime||(e._zTime=t)},Bh=function(e,t,n){var i;if(n>t)for(i=e._first;i&&i._start<=n;){if(i.data==="isPause"&&i._start>t)return i;i=i._next}else for(i=e._last;i&&i._start>=n;){if(i.data==="isPause"&&i._start<t)return i;i=i._prev}},Ui=function(e,t,n,i){var r=e._repeat,s=Re(t)||0,o=e._tTime/e._tDur;return o&&!i&&(e._time*=s/e._dur),e._dur=s,e._tDur=r?r<0?1e10:Re(s*(r+1)+e._rDelay*r):s,o>0&&!i&&cs(e,e._tTime=e._tDur*o),e.parent&&ls(e),n||si(e.parent,e),e},ol=function(e){return e instanceof Ge?si(e):Ui(e,e._dur)},zh={_start:0,endTime:pr,totalDuration:pr},Bt=function c(e,t,n){var i=e.labels,r=e._recent||zh,s=e.duration()>=zt?r.endTime(!1):e._dur,o,a,l;return Le(t)&&(isNaN(t)||t in i)?(a=t.charAt(0),l=t.substr(-1)==="%",o=t.indexOf("="),a==="<"||a===">"?(o>=0&&(t=t.replace(/=/,"")),(a==="<"?r._start:r.endTime(r._repeat>=0))+(parseFloat(t.substr(1))||0)*(l?(o<0?r:n).totalDuration()/100:1)):o<0?(t in i||(i[t]=s),i[t]):(a=parseFloat(t.charAt(o-1)+t.substr(o+1)),l&&n&&(a=a/100*(Qe(n)?n[0]:n).totalDuration()),o>1?c(e,t.substr(0,o-1),n)+a:s+a)):t==null?s:+t},ur=function(e,t,n){var i=Mn(t[1]),r=(i?2:1)+(e<2?0:1),s=t[r],o,a;if(i&&(s.duration=t[1]),s.parent=n,e){for(o=s,a=n;a&&!("immediateRender"in o);)o=a.vars.defaults||{},a=St(a.vars.inherit)&&a.parent;s.immediateRender=St(o.immediateRender),e<2?s.runBackwards=1:s.startAt=t[r-1]}return new Ee(t[0],s,t[r+1])},Hn=function(e,t){return e||e===0?t(e):t},_r=function(e,t,n){return n<e?e:n>t?t:n},Xe=function(e,t){return!Le(e)||!(t=Ch.exec(e))?"":t[1]},kh=function(e,t,n){return Hn(n,function(i){return _r(e,t,i)})},yo=[].slice,wl=function(e,t){return e&&hn(e)&&"length"in e&&(!t&&!e.length||e.length-1 in e&&hn(e[0]))&&!e.nodeType&&e!==ln},Vh=function(e,t,n){return n===void 0&&(n=[]),e.forEach(function(i){var r;return Le(i)&&!t||wl(i,1)?(r=n).push.apply(r,kt(i)):n.push(i)})||n},kt=function(e,t,n){return de&&!t&&de.selector?de.selector(e):Le(e)&&!n&&(go||!Ni())?yo.call((t||Ro).querySelectorAll(e),0):Qe(e)?Vh(e,n):wl(e)?yo.call(e,0):e?[e]:[]},Mo=function(e){return e=kt(e)[0]||dr("Invalid scope")||{},function(t){var n=e.current||e.nativeElement||e;return kt(t,n.querySelectorAll?n:n===e?dr("Invalid scope")||Ro.createElement("div"):e)}},El=function(e){return e.sort(function(){return .5-Math.random()})},Al=function(e){if(ye(e))return e;var t=hn(e)?e:{each:e},n=oi(t.ease),i=t.from||0,r=parseFloat(t.base)||0,s={},o=i>0&&i<1,a=isNaN(i)||o,l=t.axis,h=i,f=i;return Le(i)?h=f={center:.5,edges:.5,end:1}[i]||0:!o&&a&&(h=i[0],f=i[1]),function(d,u,m){var p=(m||t).length,g=s[p],x,S,T,y,M,w,v,C,E;if(!g){if(E=t.grid==="auto"?0:(t.grid||[1,zt])[1],!E){for(v=-zt;v<(v=m[E++].getBoundingClientRect().left)&&E<p;);E<p&&E--}for(g=s[p]=[],x=a?Math.min(E,p)*h-.5:i%E,S=E===zt?0:a?p*f/E-.5:i/E|0,v=0,C=zt,w=0;w<p;w++)T=w%E-x,y=S-(w/E|0),g[w]=M=l?Math.abs(l==="y"?y:T):ul(T*T+y*y),M>v&&(v=M),M<C&&(C=M);i==="random"&&El(g),g.max=v-C,g.min=C,g.v=p=(parseFloat(t.amount)||parseFloat(t.each)*(E>p?p-1:l?l==="y"?p/E:E:Math.max(E,p/E))||0)*(i==="edges"?-1:1),g.b=p<0?r-p:r,g.u=Xe(t.amount||t.each)||0,n=n&&p<0?Fl(n):n}return p=(g[d]-g.min)/g.max||0,Re(g.b+(n?n(p):p)*g.v)+g.u}},So=function(e){var t=Math.pow(10,((e+"").split(".")[1]||"").length);return function(n){var i=Re(Math.round(parseFloat(n)/e)*e*t);return(i-i%1)/t+(Mn(n)?0:Xe(n))}},Cl=function(e,t){var n=Qe(e),i,r;return!n&&hn(e)&&(i=n=e.radius||zt,e.values?(e=kt(e.values),(r=!Mn(e[0]))&&(i*=i)):e=So(e.increment)),Hn(t,n?ye(e)?function(s){return r=e(s),Math.abs(r-s)<=i?r:s}:function(s){for(var o=parseFloat(r?s.x:s),a=parseFloat(r?s.y:0),l=zt,h=0,f=e.length,d,u;f--;)r?(d=e[f].x-o,u=e[f].y-a,d=d*d+u*u):d=Math.abs(e[f]-o),d<l&&(l=d,h=f);return h=!i||l<=i?e[h]:s,r||h===s||Mn(s)?h:h+Xe(s)}:So(e))},Rl=function(e,t,n,i){return Hn(Qe(e)?!t:n===!0?!!(n=0):!i,function(){return Qe(e)?e[~~(Math.random()*e.length)]:(n=n||1e-5)&&(i=n<1?Math.pow(10,(n+"").length-2):1)&&Math.floor(Math.round((e-n/2+Math.random()*(t-e+n*.99))/n)*n*i)/i})},Hh=function(){for(var e=arguments.length,t=new Array(e),n=0;n<e;n++)t[n]=arguments[n];return function(i){return t.reduce(function(r,s){return s(r)},i)}},Gh=function(e,t){return function(n){return e(parseFloat(n))+(t||Xe(n))}},Wh=function(e,t,n){return Il(e,t,0,1,n)},Pl=function(e,t,n){return Hn(n,function(i){return e[~~t(i)]})},Xh=function c(e,t,n){var i=t-e;return Qe(e)?Pl(e,c(0,e.length),t):Hn(n,function(r){return(i+(r-e)%i)%i+e})},qh=function c(e,t,n){var i=t-e,r=i*2;return Qe(e)?Pl(e,c(0,e.length-1),t):Hn(n,function(s){return s=(r+(s-e)%r)%r||0,e+(s>i?r-s:s)})},Fi=function(e){for(var t=0,n="",i,r,s,o;~(i=e.indexOf("random(",t));)s=e.indexOf(")",i),o=e.charAt(i+7)==="[",r=e.substr(i+7,s-i-7).match(o?dl:mo),n+=e.substr(t,i-t)+Rl(o?r:+r[0],o?0:+r[1],+r[2]||1e-5),t=s+1;return n+e.substr(t,e.length-t)},Il=function(e,t,n,i,r){var s=t-e,o=i-n;return Hn(r,function(a){return n+((a-e)/s*o||0)})},Yh=function c(e,t,n,i){var r=isNaN(e+t)?0:function(u){return(1-u)*e+u*t};if(!r){var s=Le(e),o={},a,l,h,f,d;if(n===!0&&(i=1)&&(n=null),s)e={p:e},t={p:t};else if(Qe(e)&&!Qe(t)){for(h=[],f=e.length,d=f-2,l=1;l<f;l++)h.push(c(e[l-1],e[l]));f--,r=function(m){m*=f;var p=Math.min(d,~~m);return h[p](m-p)},n=t}else i||(e=Di(Qe(e)?[]:{},e));if(!h){for(a in t)Oo.call(o,e,a,"get",t[a]);r=function(m){return Ho(m,o)||(s?e.p:e)}}}return Hn(n,r)},al=function(e,t,n){var i=e.labels,r=zt,s,o,a;for(s in i)o=i[s]-t,o<0==!!n&&o&&r>(o=Math.abs(o))&&(a=s,r=o);return a},Ct=function(e,t,n){var i=e.vars,r=i[t],s=de,o=e._ctx,a,l,h;if(r)return a=i[t+"Params"],l=i.callbackScope||e,n&&zn.length&&ts(),o&&(de=o),h=a?r.apply(l,a):r.call(l),de=s,h},lr=function(e){return Vn(e),e.scrollTrigger&&e.scrollTrigger.kill(!!We),e.progress()<1&&Ct(e,"onInterrupt"),e},Pi,Dl=[],Ll=function(e){if(e)if(e=!e.name&&e.default||e,Eo()||e.headless){var t=e.name,n=ye(e),i=t&&!n&&e.init?function(){this._props=[]}:e,r={init:pr,render:Ho,add:Oo,kill:cu,modifier:lu,rawVars:0},s={targetTest:0,get:0,getSetter:hs,aliases:{},register:0};if(Ni(),e!==i){if(yt[t])return;Pt(i,Pt(ns(e,r),s)),Di(i.prototype,Di(r,ns(e,s))),yt[i.prop=t]=i,e.targetTest&&(Qr.push(i),Po[t]=1),t=(t==="css"?"CSS":t.charAt(0).toUpperCase()+t.substr(1))+"Plugin"}gl(t,i),e.register&&e.register(je,i,ht)}else Dl.push(e)},ae=255,cr={aqua:[0,ae,ae],lime:[0,ae,0],silver:[192,192,192],black:[0,0,0],maroon:[128,0,0],teal:[0,128,128],blue:[0,0,ae],navy:[0,0,128],white:[ae,ae,ae],olive:[128,128,0],yellow:[ae,ae,0],orange:[ae,165,0],gray:[128,128,128],purple:[128,0,128],green:[0,128,0],red:[ae,0,0],pink:[ae,192,203],cyan:[0,ae,ae],transparent:[ae,ae,ae,0]},lo=function(e,t,n){return e+=e<0?1:e>1?-1:0,(e*6<1?t+(n-t)*e*6:e<.5?n:e*3<2?t+(n-t)*(2/3-e)*6:t)*ae+.5|0},Ul=function(e,t,n){var i=e?Mn(e)?[e>>16,e>>8&ae,e&ae]:0:cr.black,r,s,o,a,l,h,f,d,u,m;if(!i){if(e.substr(-1)===","&&(e=e.substr(0,e.length-1)),cr[e])i=cr[e];else if(e.charAt(0)==="#"){if(e.length<6&&(r=e.charAt(1),s=e.charAt(2),o=e.charAt(3),e="#"+r+r+s+s+o+o+(e.length===5?e.charAt(4)+e.charAt(4):"")),e.length===9)return i=parseInt(e.substr(1,6),16),[i>>16,i>>8&ae,i&ae,parseInt(e.substr(7),16)/255];e=parseInt(e.substr(1),16),i=[e>>16,e>>8&ae,e&ae]}else if(e.substr(0,3)==="hsl"){if(i=m=e.match(mo),!t)a=+i[0]%360/360,l=+i[1]/100,h=+i[2]/100,s=h<=.5?h*(l+1):h+l-h*l,r=h*2-s,i.length>3&&(i[3]*=1),i[0]=lo(a+1/3,r,s),i[1]=lo(a,r,s),i[2]=lo(a-1/3,r,s);else if(~e.indexOf("="))return i=e.match(Ao),n&&i.length<4&&(i[3]=1),i}else i=e.match(mo)||cr.transparent;i=i.map(Number)}return t&&!m&&(r=i[0]/ae,s=i[1]/ae,o=i[2]/ae,f=Math.max(r,s,o),d=Math.min(r,s,o),h=(f+d)/2,f===d?a=l=0:(u=f-d,l=h>.5?u/(2-f-d):u/(f+d),a=f===r?(s-o)/u+(s<o?6:0):f===s?(o-r)/u+2:(r-s)/u+4,a*=60),i[0]=~~(a+.5),i[1]=~~(l*100+.5),i[2]=~~(h*100+.5)),n&&i.length<4&&(i[3]=1),i},Nl=function(e){var t=[],n=[],i=-1;return e.split(yn).forEach(function(r){var s=r.match(li)||[];t.push.apply(t,s),n.push(i+=s.length+1)}),t.c=n,t},ll=function(e,t,n){var i="",r=(e+i).match(yn),s=t?"hsla(":"rgba(",o=0,a,l,h,f;if(!r)return e;if(r=r.map(function(d){return(d=Ul(d,t,1))&&s+(t?d[0]+","+d[1]+"%,"+d[2]+"%,"+d[3]:d.join(","))+")"}),n&&(h=Nl(e),a=n.c,a.join(i)!==h.c.join(i)))for(l=e.replace(yn,"1").split(li),f=l.length-1;o<f;o++)i+=l[o]+(~a.indexOf(o)?r.shift()||s+"0,0,0,0)":(h.length?h:r.length?r:n).shift());if(!l)for(l=e.split(yn),f=l.length-1;o<f;o++)i+=l[o]+r[o];return i+l[f]},yn=(function(){var c="(?:\\b(?:(?:rgb|rgba|hsl|hsla)\\(.+?\\))|\\B#(?:[0-9a-f]{3,4}){1,2}\\b",e;for(e in cr)c+="|"+e+"\\b";return new RegExp(c+")","gi")})(),Zh=/hsl[a]?\(/,No=function(e){var t=e.join(" "),n;if(yn.lastIndex=0,yn.test(t))return n=Zh.test(t),e[1]=ll(e[1],n),e[0]=ll(e[0],n,Nl(e[1])),!0},mr,Mt=(function(){var c=Date.now,e=500,t=33,n=c(),i=n,r=1e3/240,s=r,o=[],a,l,h,f,d,u,m=function p(g){var x=c()-i,S=g===!0,T,y,M,w;if((x>e||x<0)&&(n+=x-t),i+=x,M=i-n,T=M-s,(T>0||S)&&(w=++f.frame,d=M-f.time*1e3,f.time=M=M/1e3,s+=T+(T>=r?4:r-T),y=1),S||(a=l(p)),y)for(u=0;u<o.length;u++)o[u](M,d,w,g)};return f={time:0,frame:0,tick:function(){m(!0)},deltaRatio:function(g){return d/(1e3/(g||60))},wake:function(){pl&&(!go&&Eo()&&(ln=go=window,Ro=ln.document||{},Rt.gsap=je,(ln.gsapVersions||(ln.gsapVersions=[])).push(je.version),ml(es||ln.GreenSockGlobals||!ln.gsap&&ln||{}),Dl.forEach(Ll)),h=typeof requestAnimationFrame<"u"&&requestAnimationFrame,a&&f.sleep(),l=h||function(g){return setTimeout(g,s-f.time*1e3+1|0)},mr=1,m(2))},sleep:function(){(h?cancelAnimationFrame:clearTimeout)(a),mr=0,l=pr},lagSmoothing:function(g,x){e=g||1/0,t=Math.min(x||33,e)},fps:function(g){r=1e3/(g||240),s=f.time*1e3+r},add:function(g,x,S){var T=x?function(y,M,w,v){g(y,M,w,v),f.remove(T)}:g;return f.remove(g),o[S?"unshift":"push"](T),Ni(),T},remove:function(g,x){~(x=o.indexOf(g))&&o.splice(x,1)&&u>=x&&u--},_listeners:o},f})(),Ni=function(){return!mr&&Mt.wake()},j={},Jh=/^[\d.\-M][\d.\-,\s]/,$h=/["']/g,Kh=function(e){for(var t={},n=e.substr(1,e.length-3).split(":"),i=n[0],r=1,s=n.length,o,a,l;r<s;r++)a=n[r],o=r!==s-1?a.lastIndexOf(","):a.length,l=a.substr(0,o),t[i]=isNaN(l)?l.replace($h,"").trim():+l,i=a.substr(o+1).trim();return t},Qh=function(e){var t=e.indexOf("(")+1,n=e.indexOf(")"),i=e.indexOf("(",t);return e.substring(t,~i&&i<n?e.indexOf(")",n+1):n)},jh=function(e){var t=(e+"").split("("),n=j[t[0]];return n&&t.length>1&&n.config?n.config.apply(null,~e.indexOf("{")?[Kh(t[1])]:Qh(e).split(",").map(vl)):j._CE&&Jh.test(e)?j._CE("",e):n},Fl=function(e){return function(t){return 1-e(1-t)}},Ol=function c(e,t){for(var n=e._first,i;n;)n instanceof Ge?c(n,t):n.vars.yoyoEase&&(!n._yoyo||!n._repeat)&&n._yoyo!==t&&(n.timeline?c(n.timeline,t):(i=n._ease,n._ease=n._yEase,n._yEase=i,n._yoyo=t)),n=n._next},oi=function(e,t){return e&&(ye(e)?e:j[e]||jh(e))||t},hi=function(e,t,n,i){n===void 0&&(n=function(a){return 1-t(1-a)}),i===void 0&&(i=function(a){return a<.5?t(a*2)/2:1-t((1-a)*2)/2});var r={easeIn:t,easeOut:n,easeInOut:i},s;return ct(e,function(o){j[o]=Rt[o]=r,j[s=o.toLowerCase()]=n;for(var a in r)j[s+(a==="easeIn"?".in":a==="easeOut"?".out":".inOut")]=j[o+"."+a]=r[a]}),r},Bl=function(e){return function(t){return t<.5?(1-e(1-t*2))/2:.5+e((t-.5)*2)/2}},co=function c(e,t,n){var i=t>=1?t:1,r=(n||(e?.3:.45))/(t<1?t:1),s=r/po*(Math.asin(1/i)||0),o=function(h){return h===1?1:i*Math.pow(2,-10*h)*Ah((h-s)*r)+1},a=e==="out"?o:e==="in"?function(l){return 1-o(1-l)}:Bl(o);return r=po/r,a.config=function(l,h){return c(e,l,h)},a},ho=function c(e,t){t===void 0&&(t=1.70158);var n=function(s){return s?--s*s*((t+1)*s+t)+1:0},i=e==="out"?n:e==="in"?function(r){return 1-n(1-r)}:Bl(n);return i.config=function(r){return c(e,r)},i};ct("Linear,Quad,Cubic,Quart,Quint,Strong",function(c,e){var t=e<5?e+1:e;hi(c+",Power"+(t-1),e?function(n){return Math.pow(n,t)}:function(n){return n},function(n){return 1-Math.pow(1-n,t)},function(n){return n<.5?Math.pow(n*2,t)/2:1-Math.pow((1-n)*2,t)/2})});j.Linear.easeNone=j.none=j.Linear.easeIn;hi("Elastic",co("in"),co("out"),co());(function(c,e){var t=1/e,n=2*t,i=2.5*t,r=function(o){return o<t?c*o*o:o<n?c*Math.pow(o-1.5/e,2)+.75:o<i?c*(o-=2.25/e)*o+.9375:c*Math.pow(o-2.625/e,2)+.984375};hi("Bounce",function(s){return 1-r(1-s)},r)})(7.5625,2.75);hi("Expo",function(c){return Math.pow(2,10*(c-1))*c+c*c*c*c*c*c*(1-c)});hi("Circ",function(c){return-(ul(1-c*c)-1)});hi("Sine",function(c){return c===1?1:-Eh(c*Th)+1});hi("Back",ho("in"),ho("out"),ho());j.SteppedEase=j.steps=Rt.SteppedEase={config:function(e,t){e===void 0&&(e=1);var n=1/e,i=e+(t?0:1),r=t?1:0,s=1-le;return function(o){return((i*_r(0,s,o)|0)+r)*n}}};Ii.ease=j["quad.out"];ct("onComplete,onUpdate,onStart,onRepeat,onReverseComplete,onInterrupt",function(c){return Io+=c+","+c+"Params,"});var Fo=function(e,t){this.id=wh++,e._gsap=this,this.target=e,this.harness=t,this.get=t?t.get:Lo,this.set=t?t.getSetter:hs},gr=(function(){function c(t){this.vars=t,this._delay=+t.delay||0,(this._repeat=t.repeat===1/0?-2:t.repeat||0)&&(this._rDelay=t.repeatDelay||0,this._yoyo=!!t.yoyo||!!t.yoyoEase),this._ts=1,Ui(this,+t.duration,1,1),this.data=t.data,de&&(this._ctx=de,de.data.push(this)),mr||Mt.wake()}var e=c.prototype;return e.delay=function(n){return n||n===0?(this.parent&&this.parent.smoothChildTiming&&this.startTime(this._start+n-this._delay),this._delay=n,this):this._delay},e.duration=function(n){return arguments.length?this.totalDuration(this._repeat>0?n+(n+this._rDelay)*this._repeat:n):this.totalDuration()&&this._dur},e.totalDuration=function(n){return arguments.length?(this._dirty=0,Ui(this,this._repeat<0?n:(n-this._repeat*this._rDelay)/(this._repeat+1))):this._tDur},e.totalTime=function(n,i){if(Ni(),!arguments.length)return this._tTime;var r=this._dp;if(r&&r.smoothChildTiming&&this._ts){for(cs(this,n),!r._dp||r.parent||Sl(r,this);r&&r.parent;)r.parent._time!==r._start+(r._ts>=0?r._tTime/r._ts:(r.totalDuration()-r._tTime)/-r._ts)&&r.totalTime(r._tTime,!0),r=r.parent;!this.parent&&this._dp.autoRemoveChildren&&(this._ts>0&&n<this._tDur||this._ts<0&&n>0||!this._tDur&&!n)&&cn(this._dp,this,this._start-this._delay)}return(this._tTime!==n||!this._dur&&!i||this._initted&&Math.abs(this._zTime)===le||!n&&!this._initted&&(this.add||this._ptLookup))&&(this._ts||(this._pTime=n),xl(this,n,i)),this},e.time=function(n,i){return arguments.length?this.totalTime(Math.min(this.totalDuration(),n+sl(this))%(this._dur+this._rDelay)||(n?this._dur:0),i):this._time},e.totalProgress=function(n,i){return arguments.length?this.totalTime(this.totalDuration()*n,i):this.totalDuration()?Math.min(1,this._tTime/this._tDur):this.rawTime()>=0&&this._initted?1:0},e.progress=function(n,i){return arguments.length?this.totalTime(this.duration()*(this._yoyo&&!(this.iteration()&1)?1-n:n)+sl(this),i):this.duration()?Math.min(1,this._time/this._dur):this.rawTime()>0?1:0},e.iteration=function(n,i){var r=this.duration()+this._rDelay;return arguments.length?this.totalTime(this._time+(n-1)*r,i):this._repeat?Li(this._tTime,r)+1:1},e.timeScale=function(n,i){if(!arguments.length)return this._rts===-le?0:this._rts;if(this._rts===n)return this;var r=this.parent&&this._ts?is(this.parent._time,this):this._tTime;return this._rts=+n||0,this._ts=this._ps||n===-le?0:this._rts,this.totalTime(_r(-Math.abs(this._delay),this.totalDuration(),r),i!==!1),ls(this),Uh(this)},e.paused=function(n){return arguments.length?(this._ps!==n&&(this._ps=n,n?(this._pTime=this._tTime||Math.max(-this._delay,this.rawTime()),this._ts=this._act=0):(Ni(),this._ts=this._rts,this.totalTime(this.parent&&!this.parent.smoothChildTiming?this.rawTime():this._tTime||this._pTime,this.progress()===1&&Math.abs(this._zTime)!==le&&(this._tTime-=le)))),this):this._ps},e.startTime=function(n){if(arguments.length){this._start=n;var i=this.parent||this._dp;return i&&(i._sort||!this.parent)&&cn(i,this,n-this._delay),this}return this._start},e.endTime=function(n){return this._start+(St(n)?this.totalDuration():this.duration())/Math.abs(this._ts||1)},e.rawTime=function(n){var i=this.parent||this._dp;return i?n&&(!this._ts||this._repeat&&this._time&&this.totalProgress()<1)?this._tTime%(this._dur+this._rDelay):this._ts?is(i.rawTime(n),this):this._tTime:this._tTime},e.revert=function(n){n===void 0&&(n=Ph);var i=We;return We=n,Uo(this)&&(this.timeline&&this.timeline.revert(n),this.totalTime(-.01,n.suppressEvents)),this.data!=="nested"&&n.kill!==!1&&this.kill(),We=i,this},e.globalTime=function(n){for(var i=this,r=arguments.length?n:i.rawTime();i;)r=i._start+r/(Math.abs(i._ts)||1),i=i._dp;return!this.parent&&this._sat?this._sat.globalTime(n):r},e.repeat=function(n){return arguments.length?(this._repeat=n===1/0?-2:n,ol(this)):this._repeat===-2?1/0:this._repeat},e.repeatDelay=function(n){if(arguments.length){var i=this._time;return this._rDelay=n,ol(this),i?this.time(i):this}return this._rDelay},e.yoyo=function(n){return arguments.length?(this._yoyo=n,this):this._yoyo},e.seek=function(n,i){return this.totalTime(Bt(this,n),St(i))},e.restart=function(n,i){return this.play().totalTime(n?-this._delay:0,St(i)),this._dur||(this._zTime=-le),this},e.play=function(n,i){return n!=null&&this.seek(n,i),this.reversed(!1).paused(!1)},e.reverse=function(n,i){return n!=null&&this.seek(n||this.totalDuration(),i),this.reversed(!0).paused(!1)},e.pause=function(n,i){return n!=null&&this.seek(n,i),this.paused(!0)},e.resume=function(){return this.paused(!1)},e.reversed=function(n){return arguments.length?(!!n!==this.reversed()&&this.timeScale(-this._rts||(n?-le:0)),this):this._rts<0},e.invalidate=function(){return this._initted=this._act=0,this._zTime=-le,this},e.isActive=function(){var n=this.parent||this._dp,i=this._start,r;return!!(!n||this._ts&&this._initted&&n.isActive()&&(r=n.rawTime(!0))>=i&&r<this.endTime(!0)-le)},e.eventCallback=function(n,i,r){var s=this.vars;return arguments.length>1?(i?(s[n]=i,r&&(s[n+"Params"]=r),n==="onUpdate"&&(this._onUpdate=i)):delete s[n],this):s[n]},e.then=function(n){var i=this;return new Promise(function(r){var s=ye(n)?n:yl,o=function(){var l=i.then;i.then=null,ye(s)&&(s=s(i))&&(s.then||s===i)&&(i.then=l),r(s),i.then=l};i._initted&&i.totalProgress()===1&&i._ts>=0||!i._tTime&&i._ts<0?o():i._prom=o})},e.kill=function(){lr(this)},c})();Pt(gr.prototype,{_time:0,_start:0,_end:0,_tTime:0,_tDur:0,_dirty:0,_repeat:0,_yoyo:!1,parent:null,_initted:!1,_rDelay:0,_ts:1,_dp:0,ratio:0,_zTime:-le,_prom:0,_ps:!1,_rts:1});var Ge=(function(c){hl(e,c);function e(n,i){var r;return n===void 0&&(n={}),r=c.call(this,n)||this,r.labels={},r.smoothChildTiming=!!n.smoothChildTiming,r.autoRemoveChildren=!!n.autoRemoveChildren,r._sort=St(n.sortChildren),me&&cn(n.parent||me,vn(r),i),n.reversed&&r.reverse(),n.paused&&r.paused(!0),n.scrollTrigger&&bl(vn(r),n.scrollTrigger),r}var t=e.prototype;return t.to=function(i,r,s){return ur(0,arguments,this),this},t.from=function(i,r,s){return ur(1,arguments,this),this},t.fromTo=function(i,r,s,o){return ur(2,arguments,this),this},t.set=function(i,r,s){return r.duration=0,r.parent=this,hr(r).repeatDelay||(r.repeat=0),r.immediateRender=!!r.immediateRender,new Ee(i,r,Bt(this,s),1),this},t.call=function(i,r,s){return cn(this,Ee.delayedCall(0,i,r),s)},t.staggerTo=function(i,r,s,o,a,l,h){return s.duration=r,s.stagger=s.stagger||o,s.onComplete=l,s.onCompleteParams=h,s.parent=this,new Ee(i,s,Bt(this,a)),this},t.staggerFrom=function(i,r,s,o,a,l,h){return s.runBackwards=1,hr(s).immediateRender=St(s.immediateRender),this.staggerTo(i,r,s,o,a,l,h)},t.staggerFromTo=function(i,r,s,o,a,l,h,f){return o.startAt=s,hr(o).immediateRender=St(o.immediateRender),this.staggerTo(i,r,o,a,l,h,f)},t.render=function(i,r,s){var o=this._time,a=this._dirty?this.totalDuration():this._tDur,l=this._dur,h=i<=0?0:Re(i),f=this._zTime<0!=i<0&&(this._initted||!l),d,u,m,p,g,x,S,T,y,M,w,v;if(this!==me&&h>a&&i>=0&&(h=a),h!==this._tTime||s||f){if(o!==this._time&&l&&(h+=this._time-o,i+=this._time-o),d=h,y=this._start,T=this._ts,x=!T,f&&(l||(o=this._zTime),(i||!r)&&(this._zTime=i)),this._repeat){if(w=this._yoyo,g=l+this._rDelay,this._repeat<-1&&i<0)return this.totalTime(g*100+i,r,s);if(d=Re(h%g),h===a?(p=this._repeat,d=l):(M=Re(h/g),p=~~M,p&&p===M&&(d=l,p--),d>l&&(d=l)),M=Li(this._tTime,g),!o&&this._tTime&&M!==p&&this._tTime-M*g-this._dur<=0&&(M=p),w&&p&1&&(d=l-d,v=1),p!==M&&!this._lock){var C=w&&M&1,E=C===(w&&p&1);if(p<M&&(C=!C),o=C?0:h%l?l:h,this._lock=1,this.render(o||(v?0:Re(p*g)),r,!l)._lock=0,this._tTime=h,!r&&this.parent&&Ct(this,"onRepeat"),this.vars.repeatRefresh&&!v&&(this.invalidate()._lock=1),o&&o!==this._time||x!==!this._ts||this.vars.onRepeat&&!this.parent&&!this._act)return this;if(l=this._dur,a=this._tDur,E&&(this._lock=2,o=C?l:-1e-4,this.render(o,!0),this.vars.repeatRefresh&&!v&&this.invalidate()),this._lock=0,!this._ts&&!x)return this;Ol(this,v)}}if(this._hasPause&&!this._forcing&&this._lock<2&&(S=Bh(this,Re(o),Re(d)),S&&(h-=d-(d=S._start))),this._tTime=h,this._time=d,this._act=!T,this._initted||(this._onUpdate=this.vars.onUpdate,this._initted=1,this._zTime=i,o=0),!o&&h&&!r&&!M&&(Ct(this,"onStart"),this._tTime!==h))return this;if(d>=o&&i>=0)for(u=this._first;u;){if(m=u._next,(u._act||d>=u._start)&&u._ts&&S!==u){if(u.parent!==this)return this.render(i,r,s);if(u.render(u._ts>0?(d-u._start)*u._ts:(u._dirty?u.totalDuration():u._tDur)+(d-u._start)*u._ts,r,s),d!==this._time||!this._ts&&!x){S=0,m&&(h+=this._zTime=-le);break}}u=m}else{u=this._last;for(var A=i<0?i:d;u;){if(m=u._prev,(u._act||A<=u._end)&&u._ts&&S!==u){if(u.parent!==this)return this.render(i,r,s);if(u.render(u._ts>0?(A-u._start)*u._ts:(u._dirty?u.totalDuration():u._tDur)+(A-u._start)*u._ts,r,s||We&&Uo(u)),d!==this._time||!this._ts&&!x){S=0,m&&(h+=this._zTime=A?-le:le);break}}u=m}}if(S&&!r&&(this.pause(),S.render(d>=o?0:-le)._zTime=d>=o?1:-1,this._ts))return this._start=y,ls(this),this.render(i,r,s);this._onUpdate&&!r&&Ct(this,"onUpdate",!0),(h===a&&this._tTime>=this.totalDuration()||!h&&o)&&(y===this._start||Math.abs(T)!==Math.abs(this._ts))&&(this._lock||((i||!l)&&(h===a&&this._ts>0||!h&&this._ts<0)&&Vn(this,1),!r&&!(i<0&&!o)&&(h||o||!a)&&(Ct(this,h===a&&i>=0?"onComplete":"onReverseComplete",!0),this._prom&&!(h<a&&this.timeScale()>0)&&this._prom())))}return this},t.add=function(i,r){var s=this;if(Mn(r)||(r=Bt(this,r,i)),!(i instanceof gr)){if(Qe(i))return i.forEach(function(o){return s.add(o,r)}),this;if(Le(i))return this.addLabel(i,r);if(ye(i))i=Ee.delayedCall(0,i);else return this}return this!==i?cn(this,i,r):this},t.getChildren=function(i,r,s,o){i===void 0&&(i=!0),r===void 0&&(r=!0),s===void 0&&(s=!0),o===void 0&&(o=-zt);for(var a=[],l=this._first;l;)l._start>=o&&(l instanceof Ee?r&&a.push(l):(s&&a.push(l),i&&a.push.apply(a,l.getChildren(!0,r,s)))),l=l._next;return a},t.getById=function(i){for(var r=this.getChildren(1,1,1),s=r.length;s--;)if(r[s].vars.id===i)return r[s]},t.remove=function(i){return Le(i)?this.removeLabel(i):ye(i)?this.killTweensOf(i):(i.parent===this&&as(this,i),i===this._recent&&(this._recent=this._last),si(this))},t.totalTime=function(i,r){return arguments.length?(this._forcing=1,!this._dp&&this._ts&&(this._start=Re(Mt.time-(this._ts>0?i/this._ts:(this.totalDuration()-i)/-this._ts))),c.prototype.totalTime.call(this,i,r),this._forcing=0,this):this._tTime},t.addLabel=function(i,r){return this.labels[i]=Bt(this,r),this},t.removeLabel=function(i){return delete this.labels[i],this},t.addPause=function(i,r,s){var o=Ee.delayedCall(0,r||pr,s);return o.data="isPause",this._hasPause=1,cn(this,o,Bt(this,i))},t.removePause=function(i){var r=this._first;for(i=Bt(this,i);r;)r._start===i&&r.data==="isPause"&&Vn(r),r=r._next},t.killTweensOf=function(i,r,s){for(var o=this.getTweensOf(i,s),a=o.length;a--;)Bn!==o[a]&&o[a].kill(i,r);return this},t.getTweensOf=function(i,r){for(var s=[],o=kt(i),a=this._first,l=Mn(r),h;a;)a instanceof Ee?Ih(a._targets,o)&&(l?(!Bn||a._initted&&a._ts)&&a.globalTime(0)<=r&&a.globalTime(a.totalDuration())>r:!r||a.isActive())&&s.push(a):(h=a.getTweensOf(o,r)).length&&s.push.apply(s,h),a=a._next;return s},t.tweenTo=function(i,r){r=r||{};var s=this,o=Bt(s,i),a=r,l=a.startAt,h=a.onStart,f=a.onStartParams,d=a.immediateRender,u,m=Ee.to(s,Pt({ease:r.ease||"none",lazy:!1,immediateRender:!1,time:o,overwrite:"auto",duration:r.duration||Math.abs((o-(l&&"time"in l?l.time:s._time))/s.timeScale())||le,onStart:function(){if(s.pause(),!u){var g=r.duration||Math.abs((o-(l&&"time"in l?l.time:s._time))/s.timeScale());m._dur!==g&&Ui(m,g,0,1).render(m._time,!0,!0),u=1}h&&h.apply(m,f||[])}},r));return d?m.render(0):m},t.tweenFromTo=function(i,r,s){return this.tweenTo(r,Pt({startAt:{time:Bt(this,i)}},s))},t.recent=function(){return this._recent},t.nextLabel=function(i){return i===void 0&&(i=this._time),al(this,Bt(this,i))},t.previousLabel=function(i){return i===void 0&&(i=this._time),al(this,Bt(this,i),1)},t.currentLabel=function(i){return arguments.length?this.seek(i,!0):this.previousLabel(this._time+le)},t.shiftChildren=function(i,r,s){s===void 0&&(s=0);for(var o=this._first,a=this.labels,l;o;)o._start>=s&&(o._start+=i,o._end+=i),o=o._next;if(r)for(l in a)a[l]>=s&&(a[l]+=i);return si(this)},t.invalidate=function(i){var r=this._first;for(this._lock=0;r;)r.invalidate(i),r=r._next;return c.prototype.invalidate.call(this,i)},t.clear=function(i){i===void 0&&(i=!0);for(var r=this._first,s;r;)s=r._next,this.remove(r),r=s;return this._dp&&(this._time=this._tTime=this._pTime=0),i&&(this.labels={}),si(this)},t.totalDuration=function(i){var r=0,s=this,o=s._last,a=zt,l,h,f;if(arguments.length)return s.timeScale((s._repeat<0?s.duration():s.totalDuration())/(s.reversed()?-i:i));if(s._dirty){for(f=s.parent;o;)l=o._prev,o._dirty&&o.totalDuration(),h=o._start,h>a&&s._sort&&o._ts&&!s._lock?(s._lock=1,cn(s,o,h-o._delay,1)._lock=0):a=h,h<0&&o._ts&&(r-=h,(!f&&!s._dp||f&&f.smoothChildTiming)&&(s._start+=h/s._ts,s._time-=h,s._tTime-=h),s.shiftChildren(-h,!1,-1/0),a=0),o._end>r&&o._ts&&(r=o._end),o=l;Ui(s,s===me&&s._time>r?s._time:r,1,1),s._dirty=0}return s._tDur},e.updateRoot=function(i){if(me._ts&&(xl(me,is(i,me)),_l=Mt.frame),Mt.frame>=il){il+=bt.autoSleep||120;var r=me._first;if((!r||!r._ts)&&bt.autoSleep&&Mt._listeners.length<2){for(;r&&!r._ts;)r=r._next;r||Mt.sleep()}}},e})(gr);Pt(Ge.prototype,{_lock:0,_hasPause:0,_forcing:0});var eu=function(e,t,n,i,r,s,o){var a=new ht(this._pt,e,t,0,1,Vo,null,r),l=0,h=0,f,d,u,m,p,g,x,S;for(a.b=n,a.e=i,n+="",i+="",(x=~i.indexOf("random("))&&(i=Fi(i)),s&&(S=[n,i],s(S,e,t),n=S[0],i=S[1]),d=n.match(oo)||[];f=oo.exec(i);)m=f[0],p=i.substring(l,f.index),u?u=(u+1)%5:p.substr(-5)==="rgba("&&(u=1),m!==d[h++]&&(g=parseFloat(d[h-1])||0,a._pt={_next:a._pt,p:p||h===1?p:",",s:g,c:m.charAt(1)==="="?ci(g,m)-g:parseFloat(m)-g,m:u&&u<4?Math.round:0},l=oo.lastIndex);return a.c=l<i.length?i.substring(l,i.length):"",a.fp=o,(Co.test(i)||x)&&(a.e=0),this._pt=a,a},Oo=function(e,t,n,i,r,s,o,a,l,h){ye(i)&&(i=i(r||0,e,s));var f=e[t],d=n!=="get"?n:ye(f)?l?e[t.indexOf("set")||!ye(e["get"+t.substr(3)])?t:"get"+t.substr(3)](l):e[t]():f,u=ye(f)?l?su:Vl:ko,m;if(Le(i)&&(~i.indexOf("random(")&&(i=Fi(i)),i.charAt(1)==="="&&(m=ci(d,i)+(Xe(d)||0),(m||m===0)&&(i=m))),!h||d!==i||bo)return!isNaN(d*i)&&i!==""?(m=new ht(this._pt,e,t,+d||0,i-(d||0),typeof f=="boolean"?au:Hl,0,u),l&&(m.fp=l),o&&m.modifier(o,this,e),this._pt=m):(!f&&!(t in e)&&os(t,i),eu.call(this,e,t,d,i,u,a||bt.stringFilter,l))},tu=function(e,t,n,i,r){if(ye(e)&&(e=fr(e,r,t,n,i)),!hn(e)||e.style&&e.nodeType||Qe(e)||fl(e))return Le(e)?fr(e,r,t,n,i):e;var s={},o;for(o in e)s[o]=fr(e[o],r,t,n,i);return s},Bo=function(e,t,n,i,r,s){var o,a,l,h;if(yt[e]&&(o=new yt[e]).init(r,o.rawVars?t[e]:tu(t[e],i,r,s,n),n,i,s)!==!1&&(n._pt=a=new ht(n._pt,r,e,0,1,o.render,o,0,o.priority),n!==Pi))for(l=n._ptLookup[n._targets.indexOf(r)],h=o._props.length;h--;)l[o._props[h]]=a;return o},Bn,bo,zo=function c(e,t,n){var i=e.vars,r=i.ease,s=i.startAt,o=i.immediateRender,a=i.lazy,l=i.onUpdate,h=i.runBackwards,f=i.yoyoEase,d=i.keyframes,u=i.autoRevert,m=e._dur,p=e._startAt,g=e._targets,x=e.parent,S=x&&x.data==="nested"?x.vars.targets:g,T=e._overwrite==="auto"&&!wo,y=e.timeline,M,w,v,C,E,A,F,R,V,z,G,H,L;if(y&&(!d||!r)&&(r="none"),e._ease=oi(r,Ii.ease),e._yEase=f?Fl(oi(f===!0?r:f,Ii.ease)):0,f&&e._yoyo&&!e._repeat&&(f=e._yEase,e._yEase=e._ease,e._ease=f),e._from=!y&&!!i.runBackwards,!y||d&&!i.stagger){if(R=g[0]?kn(g[0]).harness:0,H=R&&i[R.prop],M=ns(i,Po),p&&(p._zTime<0&&p.progress(1),t<0&&h&&o&&!u?p.render(-1,!0):p.revert(h&&m?Kr:Rh),p._lazy=0),s){if(Vn(e._startAt=Ee.set(g,Pt({data:"isStart",overwrite:!1,parent:x,immediateRender:!0,lazy:!p&&St(a),startAt:null,delay:0,onUpdate:l&&function(){return Ct(e,"onUpdate")},stagger:0},s))),e._startAt._dp=0,e._startAt._sat=e,t<0&&(We||!o&&!u)&&e._startAt.revert(Kr),o&&m&&t<=0&&n<=0){t&&(e._zTime=t);return}}else if(h&&m&&!p){if(t&&(o=!1),v=Pt({overwrite:!1,data:"isFromStart",lazy:o&&!p&&St(a),immediateRender:o,stagger:0,parent:x},M),H&&(v[R.prop]=H),Vn(e._startAt=Ee.set(g,v)),e._startAt._dp=0,e._startAt._sat=e,t<0&&(We?e._startAt.revert(Kr):e._startAt.render(-1,!0)),e._zTime=t,!o)c(e._startAt,le,le);else if(!t)return}for(e._pt=e._ptCache=0,a=m&&St(a)||a&&!m,w=0;w<g.length;w++){if(E=g[w],F=E._gsap||Do(g)[w]._gsap,e._ptLookup[w]=z={},_o[F.id]&&zn.length&&ts(),G=S===g?w:S.indexOf(E),R&&(V=new R).init(E,H||M,e,G,S)!==!1&&(e._pt=C=new ht(e._pt,E,V.name,0,1,V.render,V,0,V.priority),V._props.forEach(function(J){z[J]=C}),V.priority&&(A=1)),!R||H)for(v in M)yt[v]&&(V=Bo(v,M,e,G,E,S))?V.priority&&(A=1):z[v]=C=Oo.call(e,E,v,"get",M[v],G,S,0,i.stringFilter);e._op&&e._op[w]&&e.kill(E,e._op[w]),T&&e._pt&&(Bn=e,me.killTweensOf(E,z,e.globalTime(t)),L=!e.parent,Bn=0),e._pt&&a&&(_o[F.id]=1)}A&&Go(e),e._onInit&&e._onInit(e)}e._onUpdate=l,e._initted=(!e._op||e._pt)&&!L,d&&t<=0&&y.render(zt,!0,!0)},nu=function(e,t,n,i,r,s,o,a){var l=(e._pt&&e._ptCache||(e._ptCache={}))[t],h,f,d,u;if(!l)for(l=e._ptCache[t]=[],d=e._ptLookup,u=e._targets.length;u--;){if(h=d[u][t],h&&h.d&&h.d._pt)for(h=h.d._pt;h&&h.p!==t&&h.fp!==t;)h=h._next;if(!h)return bo=1,e.vars[t]="+=0",zo(e,o),bo=0,a?dr(t+" not eligible for reset"):1;l.push(h)}for(u=l.length;u--;)f=l[u],h=f._pt||f,h.s=(i||i===0)&&!r?i:h.s+(i||0)+s*h.c,h.c=n-h.s,f.e&&(f.e=Me(n)+Xe(f.e)),f.b&&(f.b=h.s+Xe(f.b))},iu=function(e,t){var n=e[0]?kn(e[0]).harness:0,i=n&&n.aliases,r,s,o,a;if(!i)return t;r=Di({},t);for(s in i)if(s in r)for(a=i[s].split(","),o=a.length;o--;)r[a[o]]=r[s];return r},ru=function(e,t,n,i){var r=t.ease||i||"power1.inOut",s,o;if(Qe(t))o=n[e]||(n[e]=[]),t.forEach(function(a,l){return o.push({t:l/(t.length-1)*100,v:a,e:r})});else for(s in t)o=n[s]||(n[s]=[]),s==="ease"||o.push({t:parseFloat(e),v:t[s],e:r})},fr=function(e,t,n,i,r){return ye(e)?e.call(t,n,i,r):Le(e)&&~e.indexOf("random(")?Fi(e):e},zl=Io+"repeat,repeatDelay,yoyo,repeatRefresh,yoyoEase,autoRevert",kl={};ct(zl+",id,stagger,delay,duration,paused,scrollTrigger",function(c){return kl[c]=1});var Ee=(function(c){hl(e,c);function e(n,i,r,s){var o;typeof i=="number"&&(r.duration=i,i=r,r=null),o=c.call(this,s?i:hr(i))||this;var a=o.vars,l=a.duration,h=a.delay,f=a.immediateRender,d=a.stagger,u=a.overwrite,m=a.keyframes,p=a.defaults,g=a.scrollTrigger,x=a.yoyoEase,S=i.parent||me,T=(Qe(n)||fl(n)?Mn(n[0]):"length"in i)?[n]:kt(n),y,M,w,v,C,E,A,F;if(o._targets=T.length?Do(T):dr("GSAP target "+n+" not found. https://gsap.com",!bt.nullTargetWarn)||[],o._ptLookup=[],o._overwrite=u,m||d||$r(l)||$r(h)){if(i=o.vars,y=o.timeline=new Ge({data:"nested",defaults:p||{},targets:S&&S.data==="nested"?S.vars.targets:T}),y.kill(),y.parent=y._dp=vn(o),y._start=0,d||$r(l)||$r(h)){if(v=T.length,A=d&&Al(d),hn(d))for(C in d)~zl.indexOf(C)&&(F||(F={}),F[C]=d[C]);for(M=0;M<v;M++)w=ns(i,kl),w.stagger=0,x&&(w.yoyoEase=x),F&&Di(w,F),E=T[M],w.duration=+fr(l,vn(o),M,E,T),w.delay=(+fr(h,vn(o),M,E,T)||0)-o._delay,!d&&v===1&&w.delay&&(o._delay=h=w.delay,o._start+=h,w.delay=0),y.to(E,w,A?A(M,E,T):0),y._ease=j.none;y.duration()?l=h=0:o.timeline=0}else if(m){hr(Pt(y.vars.defaults,{ease:"none"})),y._ease=oi(m.ease||i.ease||"none");var R=0,V,z,G;if(Qe(m))m.forEach(function(H){return y.to(T,H,">")}),y.duration();else{w={};for(C in m)C==="ease"||C==="easeEach"||ru(C,m[C],w,m.easeEach);for(C in w)for(V=w[C].sort(function(H,L){return H.t-L.t}),R=0,M=0;M<V.length;M++)z=V[M],G={ease:z.e,duration:(z.t-(M?V[M-1].t:0))/100*l},G[C]=z.v,y.to(T,G,R),R+=G.duration;y.duration()<l&&y.to({},{duration:l-y.duration()})}}l||o.duration(l=y.duration())}else o.timeline=0;return u===!0&&!wo&&(Bn=vn(o),me.killTweensOf(T),Bn=0),cn(S,vn(o),r),i.reversed&&o.reverse(),i.paused&&o.paused(!0),(f||!l&&!m&&o._start===Re(S._time)&&St(f)&&Nh(vn(o))&&S.data!=="nested")&&(o._tTime=-le,o.render(Math.max(0,-h)||0)),g&&bl(vn(o),g),o}var t=e.prototype;return t.render=function(i,r,s){var o=this._time,a=this._tDur,l=this._dur,h=i<0,f=i>a-le&&!h?a:i<le?0:i,d,u,m,p,g,x,S,T,y;if(!l)Oh(this,i,r,s);else if(f!==this._tTime||!i||s||!this._initted&&this._tTime||this._startAt&&this._zTime<0!==h||this._lazy){if(d=f,T=this.timeline,this._repeat){if(p=l+this._rDelay,this._repeat<-1&&h)return this.totalTime(p*100+i,r,s);if(d=Re(f%p),f===a?(m=this._repeat,d=l):(g=Re(f/p),m=~~g,m&&m===g?(d=l,m--):d>l&&(d=l)),x=this._yoyo&&m&1,x&&(y=this._yEase,d=l-d),g=Li(this._tTime,p),d===o&&!s&&this._initted&&m===g)return this._tTime=f,this;m!==g&&(T&&this._yEase&&Ol(T,x),this.vars.repeatRefresh&&!x&&!this._lock&&d!==p&&this._initted&&(this._lock=s=1,this.render(Re(p*m),!0).invalidate()._lock=0))}if(!this._initted){if(Tl(this,h?i:d,s,r,f))return this._tTime=0,this;if(o!==this._time&&!(s&&this.vars.repeatRefresh&&m!==g))return this;if(l!==this._dur)return this.render(i,r,s)}if(this._tTime=f,this._time=d,!this._act&&this._ts&&(this._act=1,this._lazy=0),this.ratio=S=(y||this._ease)(d/l),this._from&&(this.ratio=S=1-S),!o&&f&&!r&&!g&&(Ct(this,"onStart"),this._tTime!==f))return this;for(u=this._pt;u;)u.r(S,u.d),u=u._next;T&&T.render(i<0?i:T._dur*T._ease(d/this._dur),r,s)||this._startAt&&(this._zTime=i),this._onUpdate&&!r&&(h&&xo(this,i,r,s),Ct(this,"onUpdate")),this._repeat&&m!==g&&this.vars.onRepeat&&!r&&this.parent&&Ct(this,"onRepeat"),(f===this._tDur||!f)&&this._tTime===f&&(h&&!this._onUpdate&&xo(this,i,!0,!0),(i||!l)&&(f===this._tDur&&this._ts>0||!f&&this._ts<0)&&Vn(this,1),!r&&!(h&&!o)&&(f||o||x)&&(Ct(this,f===a?"onComplete":"onReverseComplete",!0),this._prom&&!(f<a&&this.timeScale()>0)&&this._prom()))}return this},t.targets=function(){return this._targets},t.invalidate=function(i){return(!i||!this.vars.runBackwards)&&(this._startAt=0),this._pt=this._op=this._onUpdate=this._lazy=this.ratio=0,this._ptLookup=[],this.timeline&&this.timeline.invalidate(i),c.prototype.invalidate.call(this,i)},t.resetTo=function(i,r,s,o,a){mr||Mt.wake(),this._ts||this.play();var l=Math.min(this._dur,(this._dp._time-this._start)*this._ts),h;return this._initted||zo(this,l),h=this._ease(l/this._dur),nu(this,i,r,s,o,h,l,a)?this.resetTo(i,r,s,o,1):(cs(this,0),this.parent||Ml(this._dp,this,"_first","_last",this._dp._sort?"_start":0),this.render(0))},t.kill=function(i,r){if(r===void 0&&(r="all"),!i&&(!r||r==="all"))return this._lazy=this._pt=0,this.parent?lr(this):this.scrollTrigger&&this.scrollTrigger.kill(!!We),this;if(this.timeline){var s=this.timeline.totalDuration();return this.timeline.killTweensOf(i,r,Bn&&Bn.vars.overwrite!==!0)._first||lr(this),this.parent&&s!==this.timeline.totalDuration()&&Ui(this,this._dur*this.timeline._tDur/s,0,1),this}var o=this._targets,a=i?kt(i):o,l=this._ptLookup,h=this._pt,f,d,u,m,p,g,x;if((!r||r==="all")&&Lh(o,a))return r==="all"&&(this._pt=0),lr(this);for(f=this._op=this._op||[],r!=="all"&&(Le(r)&&(p={},ct(r,function(S){return p[S]=1}),r=p),r=iu(o,r)),x=o.length;x--;)if(~a.indexOf(o[x])){d=l[x],r==="all"?(f[x]=r,m=d,u={}):(u=f[x]=f[x]||{},m=r);for(p in m)g=d&&d[p],g&&((!("kill"in g.d)||g.d.kill(p)===!0)&&as(this,g,"_pt"),delete d[p]),u!=="all"&&(u[p]=1)}return this._initted&&!this._pt&&h&&lr(this),this},e.to=function(i,r){return new e(i,r,arguments[2])},e.from=function(i,r){return ur(1,arguments)},e.delayedCall=function(i,r,s,o){return new e(r,0,{immediateRender:!1,lazy:!1,overwrite:!1,delay:i,onComplete:r,onReverseComplete:r,onCompleteParams:s,onReverseCompleteParams:s,callbackScope:o})},e.fromTo=function(i,r,s){return ur(2,arguments)},e.set=function(i,r){return r.duration=0,r.repeatDelay||(r.repeat=0),new e(i,r)},e.killTweensOf=function(i,r,s){return me.killTweensOf(i,r,s)},e})(gr);Pt(Ee.prototype,{_targets:[],_lazy:0,_startAt:0,_op:0,_onInit:0});ct("staggerTo,staggerFrom,staggerFromTo",function(c){Ee[c]=function(){var e=new Ge,t=yo.call(arguments,0);return t.splice(c==="staggerFromTo"?5:4,0,0),e[c].apply(e,t)}});var ko=function(e,t,n){return e[t]=n},Vl=function(e,t,n){return e[t](n)},su=function(e,t,n,i){return e[t](i.fp,n)},ou=function(e,t,n){return e.setAttribute(t,n)},hs=function(e,t){return ye(e[t])?Vl:ss(e[t])&&e.setAttribute?ou:ko},Hl=function(e,t){return t.set(t.t,t.p,Math.round((t.s+t.c*e)*1e6)/1e6,t)},au=function(e,t){return t.set(t.t,t.p,!!(t.s+t.c*e),t)},Vo=function(e,t){var n=t._pt,i="";if(!e&&t.b)i=t.b;else if(e===1&&t.e)i=t.e;else{for(;n;)i=n.p+(n.m?n.m(n.s+n.c*e):Math.round((n.s+n.c*e)*1e4)/1e4)+i,n=n._next;i+=t.c}t.set(t.t,t.p,i,t)},Ho=function(e,t){for(var n=t._pt;n;)n.r(e,n.d),n=n._next},lu=function(e,t,n,i){for(var r=this._pt,s;r;)s=r._next,r.p===i&&r.modifier(e,t,n),r=s},cu=function(e){for(var t=this._pt,n,i;t;)i=t._next,t.p===e&&!t.op||t.op===e?as(this,t,"_pt"):t.dep||(n=1),t=i;return!n},hu=function(e,t,n,i){i.mSet(e,t,i.m.call(i.tween,n,i.mt),i)},Go=function(e){for(var t=e._pt,n,i,r,s;t;){for(n=t._next,i=r;i&&i.pr>t.pr;)i=i._next;(t._prev=i?i._prev:s)?t._prev._next=t:r=t,(t._next=i)?i._prev=t:s=t,t=n}e._pt=r},ht=(function(){function c(t,n,i,r,s,o,a,l,h){this.t=n,this.s=r,this.c=s,this.p=i,this.r=o||Hl,this.d=a||this,this.set=l||ko,this.pr=h||0,this._next=t,t&&(t._prev=this)}var e=c.prototype;return e.modifier=function(n,i,r){this.mSet=this.mSet||this.set,this.set=hu,this.m=n,this.mt=r,this.tween=i},c})();ct(Io+"parent,duration,ease,delay,overwrite,runBackwards,startAt,yoyo,immediateRender,repeat,repeatDelay,data,paused,reversed,lazy,callbackScope,stringFilter,id,yoyoEase,stagger,inherit,repeatRefresh,keyframes,autoRevert,scrollTrigger",function(c){return Po[c]=1});Rt.TweenMax=Rt.TweenLite=Ee;Rt.TimelineLite=Rt.TimelineMax=Ge;me=new Ge({sortChildren:!1,defaults:Ii,autoRemoveChildren:!0,id:"root",smoothChildTiming:!0});bt.stringFilter=No;var ai=[],jr={},uu=[],cl=0,fu=0,uo=function(e){return(jr[e]||uu).map(function(t){return t()})},To=function(){var e=Date.now(),t=[];e-cl>2&&(uo("matchMediaInit"),ai.forEach(function(n){var i=n.queries,r=n.conditions,s,o,a,l;for(o in i)s=ln.matchMedia(i[o]).matches,s&&(a=1),s!==r[o]&&(r[o]=s,l=1);l&&(n.revert(),a&&t.push(n))}),uo("matchMediaRevert"),t.forEach(function(n){return n.onMatch(n,function(i){return n.add(null,i)})}),cl=e,uo("matchMedia"))},Gl=(function(){function c(t,n){this.selector=n&&Mo(n),this.data=[],this._r=[],this.isReverted=!1,this.id=fu++,t&&this.add(t)}var e=c.prototype;return e.add=function(n,i,r){ye(n)&&(r=i,i=n,n=ye);var s=this,o=function(){var l=de,h=s.selector,f;return l&&l!==s&&l.data.push(s),r&&(s.selector=Mo(r)),de=s,f=i.apply(s,arguments),ye(f)&&s._r.push(f),de=l,s.selector=h,s.isReverted=!1,f};return s.last=o,n===ye?o(s,function(a){return s.add(null,a)}):n?s[n]=o:o},e.ignore=function(n){var i=de;de=null,n(this),de=i},e.getTweens=function(){var n=[];return this.data.forEach(function(i){return i instanceof c?n.push.apply(n,i.getTweens()):i instanceof Ee&&!(i.parent&&i.parent.data==="nested")&&n.push(i)}),n},e.clear=function(){this._r.length=this.data.length=0},e.kill=function(n,i){var r=this;if(n?(function(){for(var o=r.getTweens(),a=r.data.length,l;a--;)l=r.data[a],l.data==="isFlip"&&(l.revert(),l.getChildren(!0,!0,!1).forEach(function(h){return o.splice(o.indexOf(h),1)}));for(o.map(function(h){return{g:h._dur||h._delay||h._sat&&!h._sat.vars.immediateRender?h.globalTime(0):-1/0,t:h}}).sort(function(h,f){return f.g-h.g||-1/0}).forEach(function(h){return h.t.revert(n)}),a=r.data.length;a--;)l=r.data[a],l instanceof Ge?l.data!=="nested"&&(l.scrollTrigger&&l.scrollTrigger.revert(),l.kill()):!(l instanceof Ee)&&l.revert&&l.revert(n);r._r.forEach(function(h){return h(n,r)}),r.isReverted=!0})():this.data.forEach(function(o){return o.kill&&o.kill()}),this.clear(),i)for(var s=ai.length;s--;)ai[s].id===this.id&&ai.splice(s,1)},e.revert=function(n){this.kill(n||{})},c})(),du=(function(){function c(t){this.contexts=[],this.scope=t,de&&de.data.push(this)}var e=c.prototype;return e.add=function(n,i,r){hn(n)||(n={matches:n});var s=new Gl(0,r||this.scope),o=s.conditions={},a,l,h;de&&!s.selector&&(s.selector=de.selector),this.contexts.push(s),i=s.add("onMatch",i),s.queries=n;for(l in n)l==="all"?h=1:(a=ln.matchMedia(n[l]),a&&(ai.indexOf(s)<0&&ai.push(s),(o[l]=a.matches)&&(h=1),a.addListener?a.addListener(To):a.addEventListener("change",To)));return h&&i(s,function(f){return s.add(null,f)}),this},e.revert=function(n){this.kill(n||{})},e.kill=function(n){this.contexts.forEach(function(i){return i.kill(n,!0)})},c})(),rs={registerPlugin:function(){for(var e=arguments.length,t=new Array(e),n=0;n<e;n++)t[n]=arguments[n];t.forEach(function(i){return Ll(i)})},timeline:function(e){return new Ge(e)},getTweensOf:function(e,t){return me.getTweensOf(e,t)},getProperty:function(e,t,n,i){Le(e)&&(e=kt(e)[0]);var r=kn(e||{}).get,s=n?yl:vl;return n==="native"&&(n=""),e&&(t?s((yt[t]&&yt[t].get||r)(e,t,n,i)):function(o,a,l){return s((yt[o]&&yt[o].get||r)(e,o,a,l))})},quickSetter:function(e,t,n){if(e=kt(e),e.length>1){var i=e.map(function(h){return je.quickSetter(h,t,n)}),r=i.length;return function(h){for(var f=r;f--;)i[f](h)}}e=e[0]||{};var s=yt[t],o=kn(e),a=o.harness&&(o.harness.aliases||{})[t]||t,l=s?function(h){var f=new s;Pi._pt=0,f.init(e,n?h+n:h,Pi,0,[e]),f.render(1,f),Pi._pt&&Ho(1,Pi)}:o.set(e,a);return s?l:function(h){return l(e,a,n?h+n:h,o,1)}},quickTo:function(e,t,n){var i,r=je.to(e,Pt((i={},i[t]="+=0.1",i.paused=!0,i.stagger=0,i),n||{})),s=function(a,l,h){return r.resetTo(t,a,l,h)};return s.tween=r,s},isTweening:function(e){return me.getTweensOf(e,!0).length>0},defaults:function(e){return e&&e.ease&&(e.ease=oi(e.ease,Ii.ease)),rl(Ii,e||{})},config:function(e){return rl(bt,e||{})},registerEffect:function(e){var t=e.name,n=e.effect,i=e.plugins,r=e.defaults,s=e.extendTimeline;(i||"").split(",").forEach(function(o){return o&&!yt[o]&&!Rt[o]&&dr(t+" effect requires "+o+" plugin.")}),ao[t]=function(o,a,l){return n(kt(o),Pt(a||{},r),l)},s&&(Ge.prototype[t]=function(o,a,l){return this.add(ao[t](o,hn(a)?a:(l=a)&&{},this),l)})},registerEase:function(e,t){j[e]=oi(t)},parseEase:function(e,t){return arguments.length?oi(e,t):j},getById:function(e){return me.getById(e)},exportRoot:function(e,t){e===void 0&&(e={});var n=new Ge(e),i,r;for(n.smoothChildTiming=St(e.smoothChildTiming),me.remove(n),n._dp=0,n._time=n._tTime=me._time,i=me._first;i;)r=i._next,(t||!(!i._dur&&i instanceof Ee&&i.vars.onComplete===i._targets[0]))&&cn(n,i,i._start-i._delay),i=r;return cn(me,n,0),n},context:function(e,t){return e?new Gl(e,t):de},matchMedia:function(e){return new du(e)},matchMediaRefresh:function(){return ai.forEach(function(e){var t=e.conditions,n,i;for(i in t)t[i]&&(t[i]=!1,n=1);n&&e.revert()})||To()},addEventListener:function(e,t){var n=jr[e]||(jr[e]=[]);~n.indexOf(t)||n.push(t)},removeEventListener:function(e,t){var n=jr[e],i=n&&n.indexOf(t);i>=0&&n.splice(i,1)},utils:{wrap:Xh,wrapYoyo:qh,distribute:Al,random:Rl,snap:Cl,normalize:Wh,getUnit:Xe,clamp:kh,splitColor:Ul,toArray:kt,selector:Mo,mapRange:Il,pipe:Hh,unitize:Gh,interpolate:Yh,shuffle:El},install:ml,effects:ao,ticker:Mt,updateRoot:Ge.updateRoot,plugins:yt,globalTimeline:me,core:{PropTween:ht,globals:gl,Tween:Ee,Timeline:Ge,Animation:gr,getCache:kn,_removeLinkedListItem:as,reverting:function(){return We},context:function(e){return e&&de&&(de.data.push(e),e._ctx=de),de},suppressOverwrites:function(e){return wo=e}}};ct("to,from,fromTo,delayedCall,set,killTweensOf",function(c){return rs[c]=Ee[c]});Mt.add(Ge.updateRoot);Pi=rs.to({},{duration:0});var pu=function(e,t){for(var n=e._pt;n&&n.p!==t&&n.op!==t&&n.fp!==t;)n=n._next;return n},mu=function(e,t){var n=e._targets,i,r,s;for(i in t)for(r=n.length;r--;)s=e._ptLookup[r][i],s&&(s=s.d)&&(s._pt&&(s=pu(s,i)),s&&s.modifier&&s.modifier(t[i],e,n[r],i))},fo=function(e,t){return{name:e,headless:1,rawVars:1,init:function(i,r,s){s._onInit=function(o){var a,l;if(Le(r)&&(a={},ct(r,function(h){return a[h]=1}),r=a),t){a={};for(l in r)a[l]=t(r[l]);r=a}mu(o,r)}}}},je=rs.registerPlugin({name:"attr",init:function(e,t,n,i,r){var s,o,a;this.tween=n;for(s in t)a=e.getAttribute(s)||"",o=this.add(e,"setAttribute",(a||0)+"",t[s],i,r,0,0,s),o.op=s,o.b=a,this._props.push(s)},render:function(e,t){for(var n=t._pt;n;)We?n.set(n.t,n.p,n.b,n):n.r(e,n.d),n=n._next}},{name:"endArray",headless:1,init:function(e,t){for(var n=t.length;n--;)this.add(e,n,e[n]||0,t[n],0,0,0,0,0,1)}},fo("roundProps",So),fo("modifiers"),fo("snap",Cl))||rs;Ee.version=Ge.version=je.version="3.13.0";pl=1;Eo()&&Ni();var gu=j.Power0,_u=j.Power1,xu=j.Power2,vu=j.Power3,yu=j.Power4,Mu=j.Linear,Su=j.Quad,bu=j.Cubic,Tu=j.Quart,wu=j.Quint,Eu=j.Strong,Au=j.Elastic,Cu=j.Back,Ru=j.SteppedEase,Pu=j.Bounce,Iu=j.Sine,Du=j.Expo,Lu=j.Circ;var Wl,Gn,Bi,Jo,pi,Uu,Xl,$o,Nu=function(){return typeof window<"u"},bn={},di=180/Math.PI,zi=Math.PI/180,Oi=Math.atan2,ql=1e8,Ko=/([A-Z])/g,Fu=/(left|right|width|margin|padding|x)/i,Ou=/[\s,\(]\S/,un={autoAlpha:"opacity,visibility",scale:"scaleX,scaleY",alpha:"opacity"},Xo=function(e,t){return t.set(t.t,t.p,Math.round((t.s+t.c*e)*1e4)/1e4+t.u,t)},Bu=function(e,t){return t.set(t.t,t.p,e===1?t.e:Math.round((t.s+t.c*e)*1e4)/1e4+t.u,t)},zu=function(e,t){return t.set(t.t,t.p,e?Math.round((t.s+t.c*e)*1e4)/1e4+t.u:t.b,t)},ku=function(e,t){var n=t.s+t.c*e;t.set(t.t,t.p,~~(n+(n<0?-.5:.5))+t.u,t)},ec=function(e,t){return t.set(t.t,t.p,e?t.e:t.b,t)},tc=function(e,t){return t.set(t.t,t.p,e!==1?t.b:t.e,t)},Vu=function(e,t,n){return e.style[t]=n},Hu=function(e,t,n){return e.style.setProperty(t,n)},Gu=function(e,t,n){return e._gsap[t]=n},Wu=function(e,t,n){return e._gsap.scaleX=e._gsap.scaleY=n},Xu=function(e,t,n,i,r){var s=e._gsap;s.scaleX=s.scaleY=n,s.renderTransform(r,s)},qu=function(e,t,n,i,r){var s=e._gsap;s[t]=n,s.renderTransform(r,s)},ge="transform",Tt=ge+"Origin",Yu=function c(e,t){var n=this,i=this.target,r=i.style,s=i._gsap;if(e in bn&&r){if(this.tfm=this.tfm||{},e!=="transform")e=un[e]||e,~e.indexOf(",")?e.split(",").forEach(function(o){return n.tfm[o]=Sn(i,o)}):this.tfm[e]=s.x?s[e]:Sn(i,e),e===Tt&&(this.tfm.zOrigin=s.zOrigin);else return un.transform.split(",").forEach(function(o){return c.call(n,o,t)});if(this.props.indexOf(ge)>=0)return;s.svg&&(this.svgo=i.getAttribute("data-svg-origin"),this.props.push(Tt,t,"")),e=ge}(r||t)&&this.props.push(e,t,r[e])},nc=function(e){e.translate&&(e.removeProperty("translate"),e.removeProperty("scale"),e.removeProperty("rotate"))},Zu=function(){var e=this.props,t=this.target,n=t.style,i=t._gsap,r,s;for(r=0;r<e.length;r+=3)e[r+1]?e[r+1]===2?t[e[r]](e[r+2]):t[e[r]]=e[r+2]:e[r+2]?n[e[r]]=e[r+2]:n.removeProperty(e[r].substr(0,2)==="--"?e[r]:e[r].replace(Ko,"-$1").toLowerCase());if(this.tfm){for(s in this.tfm)i[s]=this.tfm[s];i.svg&&(i.renderTransform(),t.setAttribute("data-svg-origin",this.svgo||"")),r=$o(),(!r||!r.isStart)&&!n[ge]&&(nc(n),i.zOrigin&&n[Tt]&&(n[Tt]+=" "+i.zOrigin+"px",i.zOrigin=0,i.renderTransform()),i.uncache=1)}},ic=function(e,t){var n={target:e,props:[],revert:Zu,save:Yu};return e._gsap||je.core.getCache(e),t&&e.style&&e.nodeType&&t.split(",").forEach(function(i){return n.save(i)}),n},rc,qo=function(e,t){var n=Gn.createElementNS?Gn.createElementNS((t||"http://www.w3.org/1999/xhtml").replace(/^https/,"http"),e):Gn.createElement(e);return n&&n.style?n:Gn.createElement(e)},Vt=function c(e,t,n){var i=getComputedStyle(e);return i[t]||i.getPropertyValue(t.replace(Ko,"-$1").toLowerCase())||i.getPropertyValue(t)||!n&&c(e,ki(t)||t,1)||""},Yl="O,Moz,ms,Ms,Webkit".split(","),ki=function(e,t,n){var i=t||pi,r=i.style,s=5;if(e in r&&!n)return e;for(e=e.charAt(0).toUpperCase()+e.substr(1);s--&&!(Yl[s]+e in r););return s<0?null:(s===3?"ms":s>=0?Yl[s]:"")+e},Yo=function(){Nu()&&window.document&&(Wl=window,Gn=Wl.document,Bi=Gn.documentElement,pi=qo("div")||{style:{}},Uu=qo("div"),ge=ki(ge),Tt=ge+"Origin",pi.style.cssText="border-width:0;line-height:0;position:absolute;padding:0",rc=!!ki("perspective"),$o=je.core.reverting,Jo=1)},Zl=function(e){var t=e.ownerSVGElement,n=qo("svg",t&&t.getAttribute("xmlns")||"http://www.w3.org/2000/svg"),i=e.cloneNode(!0),r;i.style.display="block",n.appendChild(i),Bi.appendChild(n);try{r=i.getBBox()}catch{}return n.removeChild(i),Bi.removeChild(n),r},Jl=function(e,t){for(var n=t.length;n--;)if(e.hasAttribute(t[n]))return e.getAttribute(t[n])},sc=function(e){var t,n;try{t=e.getBBox()}catch{t=Zl(e),n=1}return t&&(t.width||t.height)||n||(t=Zl(e)),t&&!t.width&&!t.x&&!t.y?{x:+Jl(e,["x","cx","x1"])||0,y:+Jl(e,["y","cy","y1"])||0,width:0,height:0}:t},oc=function(e){return!!(e.getCTM&&(!e.parentNode||e.ownerSVGElement)&&sc(e))},mi=function(e,t){if(t){var n=e.style,i;t in bn&&t!==Tt&&(t=ge),n.removeProperty?(i=t.substr(0,2),(i==="ms"||t.substr(0,6)==="webkit")&&(t="-"+t),n.removeProperty(i==="--"?t:t.replace(Ko,"-$1").toLowerCase())):n.removeAttribute(t)}},Wn=function(e,t,n,i,r,s){var o=new ht(e._pt,t,n,0,1,s?tc:ec);return e._pt=o,o.b=i,o.e=r,e._props.push(n),o},$l={deg:1,rad:1,turn:1},Ju={grid:1,flex:1},Xn=function c(e,t,n,i){var r=parseFloat(n)||0,s=(n+"").trim().substr((r+"").length)||"px",o=pi.style,a=Fu.test(t),l=e.tagName.toLowerCase()==="svg",h=(l?"client":"offset")+(a?"Width":"Height"),f=100,d=i==="px",u=i==="%",m,p,g,x;if(i===s||!r||$l[i]||$l[s])return r;if(s!=="px"&&!d&&(r=c(e,t,n,"px")),x=e.getCTM&&oc(e),(u||s==="%")&&(bn[t]||~t.indexOf("adius")))return m=x?e.getBBox()[a?"width":"height"]:e[h],Me(u?r/m*f:r/100*m);if(o[a?"width":"height"]=f+(d?s:i),p=i!=="rem"&&~t.indexOf("adius")||i==="em"&&e.appendChild&&!l?e:e.parentNode,x&&(p=(e.ownerSVGElement||{}).parentNode),(!p||p===Gn||!p.appendChild)&&(p=Gn.body),g=p._gsap,g&&u&&g.width&&a&&g.time===Mt.time&&!g.uncache)return Me(r/g.width*f);if(u&&(t==="height"||t==="width")){var S=e.style[t];e.style[t]=f+i,m=e[h],S?e.style[t]=S:mi(e,t)}else(u||s==="%")&&!Ju[Vt(p,"display")]&&(o.position=Vt(e,"position")),p===e&&(o.position="static"),p.appendChild(pi),m=pi[h],p.removeChild(pi),o.position="absolute";return a&&u&&(g=kn(p),g.time=Mt.time,g.width=p[h]),Me(d?m*r/f:m&&r?f/m*r:0)},Sn=function(e,t,n,i){var r;return Jo||Yo(),t in un&&t!=="transform"&&(t=un[t],~t.indexOf(",")&&(t=t.split(",")[0])),bn[t]&&t!=="transform"?(r=yr(e,i),r=t!=="transformOrigin"?r[t]:r.svg?r.origin:fs(Vt(e,Tt))+" "+r.zOrigin+"px"):(r=e.style[t],(!r||r==="auto"||i||~(r+"").indexOf("calc("))&&(r=us[t]&&us[t](e,t,n)||Vt(e,t)||Lo(e,t)||(t==="opacity"?1:0))),n&&!~(r+"").trim().indexOf(" ")?Xn(e,t,r,n)+n:r},$u=function(e,t,n,i){if(!n||n==="none"){var r=ki(t,e,1),s=r&&Vt(e,r,1);s&&s!==n?(t=r,n=s):t==="borderColor"&&(n=Vt(e,"borderTopColor"))}var o=new ht(this._pt,e.style,t,0,1,Vo),a=0,l=0,h,f,d,u,m,p,g,x,S,T,y,M;if(o.b=n,o.e=i,n+="",i+="",i.substring(0,6)==="var(--"&&(i=Vt(e,i.substring(4,i.indexOf(")")))),i==="auto"&&(p=e.style[t],e.style[t]=i,i=Vt(e,t)||i,p?e.style[t]=p:mi(e,t)),h=[n,i],No(h),n=h[0],i=h[1],d=n.match(li)||[],M=i.match(li)||[],M.length){for(;f=li.exec(i);)g=f[0],S=i.substring(a,f.index),m?m=(m+1)%5:(S.substr(-5)==="rgba("||S.substr(-5)==="hsla(")&&(m=1),g!==(p=d[l++]||"")&&(u=parseFloat(p)||0,y=p.substr((u+"").length),g.charAt(1)==="="&&(g=ci(u,g)+y),x=parseFloat(g),T=g.substr((x+"").length),a=li.lastIndex-T.length,T||(T=T||bt.units[t]||y,a===i.length&&(i+=T,o.e+=T)),y!==T&&(u=Xn(e,t,p,T)||0),o._pt={_next:o._pt,p:S||l===1?S:",",s:u,c:x-u,m:m&&m<4||t==="zIndex"?Math.round:0});o.c=a<i.length?i.substring(a,i.length):""}else o.r=t==="display"&&i==="none"?tc:ec;return Co.test(i)&&(o.e=0),this._pt=o,o},Kl={top:"0%",bottom:"100%",left:"0%",right:"100%",center:"50%"},Ku=function(e){var t=e.split(" "),n=t[0],i=t[1]||"50%";return(n==="top"||n==="bottom"||i==="left"||i==="right")&&(e=n,n=i,i=e),t[0]=Kl[n]||n,t[1]=Kl[i]||i,t.join(" ")},Qu=function(e,t){if(t.tween&&t.tween._time===t.tween._dur){var n=t.t,i=n.style,r=t.u,s=n._gsap,o,a,l;if(r==="all"||r===!0)i.cssText="",a=1;else for(r=r.split(","),l=r.length;--l>-1;)o=r[l],bn[o]&&(a=1,o=o==="transformOrigin"?Tt:ge),mi(n,o);a&&(mi(n,ge),s&&(s.svg&&n.removeAttribute("transform"),i.scale=i.rotate=i.translate="none",yr(n,1),s.uncache=1,nc(i)))}},us={clearProps:function(e,t,n,i,r){if(r.data!=="isFromStart"){var s=e._pt=new ht(e._pt,t,n,0,0,Qu);return s.u=i,s.pr=-10,s.tween=r,e._props.push(n),1}}},vr=[1,0,0,1,0,0],ac={},lc=function(e){return e==="matrix(1, 0, 0, 1, 0, 0)"||e==="none"||!e},Ql=function(e){var t=Vt(e,ge);return lc(t)?vr:t.substr(7).match(Ao).map(Me)},Qo=function(e,t){var n=e._gsap||kn(e),i=e.style,r=Ql(e),s,o,a,l;return n.svg&&e.getAttribute("transform")?(a=e.transform.baseVal.consolidate().matrix,r=[a.a,a.b,a.c,a.d,a.e,a.f],r.join(",")==="1,0,0,1,0,0"?vr:r):(r===vr&&!e.offsetParent&&e!==Bi&&!n.svg&&(a=i.display,i.display="block",s=e.parentNode,(!s||!e.offsetParent&&!e.getBoundingClientRect().width)&&(l=1,o=e.nextElementSibling,Bi.appendChild(e)),r=Ql(e),a?i.display=a:mi(e,"display"),l&&(o?s.insertBefore(e,o):s?s.appendChild(e):Bi.removeChild(e))),t&&r.length>6?[r[0],r[1],r[4],r[5],r[12],r[13]]:r)},Zo=function(e,t,n,i,r,s){var o=e._gsap,a=r||Qo(e,!0),l=o.xOrigin||0,h=o.yOrigin||0,f=o.xOffset||0,d=o.yOffset||0,u=a[0],m=a[1],p=a[2],g=a[3],x=a[4],S=a[5],T=t.split(" "),y=parseFloat(T[0])||0,M=parseFloat(T[1])||0,w,v,C,E;n?a!==vr&&(v=u*g-m*p)&&(C=y*(g/v)+M*(-p/v)+(p*S-g*x)/v,E=y*(-m/v)+M*(u/v)-(u*S-m*x)/v,y=C,M=E):(w=sc(e),y=w.x+(~T[0].indexOf("%")?y/100*w.width:y),M=w.y+(~(T[1]||T[0]).indexOf("%")?M/100*w.height:M)),i||i!==!1&&o.smooth?(x=y-l,S=M-h,o.xOffset=f+(x*u+S*p)-x,o.yOffset=d+(x*m+S*g)-S):o.xOffset=o.yOffset=0,o.xOrigin=y,o.yOrigin=M,o.smooth=!!i,o.origin=t,o.originIsAbsolute=!!n,e.style[Tt]="0px 0px",s&&(Wn(s,o,"xOrigin",l,y),Wn(s,o,"yOrigin",h,M),Wn(s,o,"xOffset",f,o.xOffset),Wn(s,o,"yOffset",d,o.yOffset)),e.setAttribute("data-svg-origin",y+" "+M)},yr=function(e,t){var n=e._gsap||new Fo(e);if("x"in n&&!t&&!n.uncache)return n;var i=e.style,r=n.scaleX<0,s="px",o="deg",a=getComputedStyle(e),l=Vt(e,Tt)||"0",h,f,d,u,m,p,g,x,S,T,y,M,w,v,C,E,A,F,R,V,z,G,H,L,J,ie,_,re,ot,Zt,_e,ke;return h=f=d=p=g=x=S=T=y=0,u=m=1,n.svg=!!(e.getCTM&&oc(e)),a.translate&&((a.translate!=="none"||a.scale!=="none"||a.rotate!=="none")&&(i[ge]=(a.translate!=="none"?"translate3d("+(a.translate+" 0 0").split(" ").slice(0,3).join(", ")+") ":"")+(a.rotate!=="none"?"rotate("+a.rotate+") ":"")+(a.scale!=="none"?"scale("+a.scale.split(" ").join(",")+") ":"")+(a[ge]!=="none"?a[ge]:"")),i.scale=i.rotate=i.translate="none"),v=Qo(e,n.svg),n.svg&&(n.uncache?(J=e.getBBox(),l=n.xOrigin-J.x+"px "+(n.yOrigin-J.y)+"px",L=""):L=!t&&e.getAttribute("data-svg-origin"),Zo(e,L||l,!!L||n.originIsAbsolute,n.smooth!==!1,v)),M=n.xOrigin||0,w=n.yOrigin||0,v!==vr&&(F=v[0],R=v[1],V=v[2],z=v[3],h=G=v[4],f=H=v[5],v.length===6?(u=Math.sqrt(F*F+R*R),m=Math.sqrt(z*z+V*V),p=F||R?Oi(R,F)*di:0,S=V||z?Oi(V,z)*di+p:0,S&&(m*=Math.abs(Math.cos(S*zi))),n.svg&&(h-=M-(M*F+w*V),f-=w-(M*R+w*z))):(ke=v[6],Zt=v[7],_=v[8],re=v[9],ot=v[10],_e=v[11],h=v[12],f=v[13],d=v[14],C=Oi(ke,ot),g=C*di,C&&(E=Math.cos(-C),A=Math.sin(-C),L=G*E+_*A,J=H*E+re*A,ie=ke*E+ot*A,_=G*-A+_*E,re=H*-A+re*E,ot=ke*-A+ot*E,_e=Zt*-A+_e*E,G=L,H=J,ke=ie),C=Oi(-V,ot),x=C*di,C&&(E=Math.cos(-C),A=Math.sin(-C),L=F*E-_*A,J=R*E-re*A,ie=V*E-ot*A,_e=z*A+_e*E,F=L,R=J,V=ie),C=Oi(R,F),p=C*di,C&&(E=Math.cos(C),A=Math.sin(C),L=F*E+R*A,J=G*E+H*A,R=R*E-F*A,H=H*E-G*A,F=L,G=J),g&&Math.abs(g)+Math.abs(p)>359.9&&(g=p=0,x=180-x),u=Me(Math.sqrt(F*F+R*R+V*V)),m=Me(Math.sqrt(H*H+ke*ke)),C=Oi(G,H),S=Math.abs(C)>2e-4?C*di:0,y=_e?1/(_e<0?-_e:_e):0),n.svg&&(L=e.getAttribute("transform"),n.forceCSS=e.setAttribute("transform","")||!lc(Vt(e,ge)),L&&e.setAttribute("transform",L))),Math.abs(S)>90&&Math.abs(S)<270&&(r?(u*=-1,S+=p<=0?180:-180,p+=p<=0?180:-180):(m*=-1,S+=S<=0?180:-180)),t=t||n.uncache,n.x=h-((n.xPercent=h&&(!t&&n.xPercent||(Math.round(e.offsetWidth/2)===Math.round(-h)?-50:0)))?e.offsetWidth*n.xPercent/100:0)+s,n.y=f-((n.yPercent=f&&(!t&&n.yPercent||(Math.round(e.offsetHeight/2)===Math.round(-f)?-50:0)))?e.offsetHeight*n.yPercent/100:0)+s,n.z=d+s,n.scaleX=Me(u),n.scaleY=Me(m),n.rotation=Me(p)+o,n.rotationX=Me(g)+o,n.rotationY=Me(x)+o,n.skewX=S+o,n.skewY=T+o,n.transformPerspective=y+s,(n.zOrigin=parseFloat(l.split(" ")[2])||!t&&n.zOrigin||0)&&(i[Tt]=fs(l)),n.xOffset=n.yOffset=0,n.force3D=bt.force3D,n.renderTransform=n.svg?ef:rc?cc:ju,n.uncache=0,n},fs=function(e){return(e=e.split(" "))[0]+" "+e[1]},Wo=function(e,t,n){var i=Xe(t);return Me(parseFloat(t)+parseFloat(Xn(e,"x",n+"px",i)))+i},ju=function(e,t){t.z="0px",t.rotationY=t.rotationX="0deg",t.force3D=0,cc(e,t)},ui="0deg",xr="0px",fi=") ",cc=function(e,t){var n=t||this,i=n.xPercent,r=n.yPercent,s=n.x,o=n.y,a=n.z,l=n.rotation,h=n.rotationY,f=n.rotationX,d=n.skewX,u=n.skewY,m=n.scaleX,p=n.scaleY,g=n.transformPerspective,x=n.force3D,S=n.target,T=n.zOrigin,y="",M=x==="auto"&&e&&e!==1||x===!0;if(T&&(f!==ui||h!==ui)){var w=parseFloat(h)*zi,v=Math.sin(w),C=Math.cos(w),E;w=parseFloat(f)*zi,E=Math.cos(w),s=Wo(S,s,v*E*-T),o=Wo(S,o,-Math.sin(w)*-T),a=Wo(S,a,C*E*-T+T)}g!==xr&&(y+="perspective("+g+fi),(i||r)&&(y+="translate("+i+"%, "+r+"%) "),(M||s!==xr||o!==xr||a!==xr)&&(y+=a!==xr||M?"translate3d("+s+", "+o+", "+a+") ":"translate("+s+", "+o+fi),l!==ui&&(y+="rotate("+l+fi),h!==ui&&(y+="rotateY("+h+fi),f!==ui&&(y+="rotateX("+f+fi),(d!==ui||u!==ui)&&(y+="skew("+d+", "+u+fi),(m!==1||p!==1)&&(y+="scale("+m+", "+p+fi),S.style[ge]=y||"translate(0, 0)"},ef=function(e,t){var n=t||this,i=n.xPercent,r=n.yPercent,s=n.x,o=n.y,a=n.rotation,l=n.skewX,h=n.skewY,f=n.scaleX,d=n.scaleY,u=n.target,m=n.xOrigin,p=n.yOrigin,g=n.xOffset,x=n.yOffset,S=n.forceCSS,T=parseFloat(s),y=parseFloat(o),M,w,v,C,E;a=parseFloat(a),l=parseFloat(l),h=parseFloat(h),h&&(h=parseFloat(h),l+=h,a+=h),a||l?(a*=zi,l*=zi,M=Math.cos(a)*f,w=Math.sin(a)*f,v=Math.sin(a-l)*-d,C=Math.cos(a-l)*d,l&&(h*=zi,E=Math.tan(l-h),E=Math.sqrt(1+E*E),v*=E,C*=E,h&&(E=Math.tan(h),E=Math.sqrt(1+E*E),M*=E,w*=E)),M=Me(M),w=Me(w),v=Me(v),C=Me(C)):(M=f,C=d,w=v=0),(T&&!~(s+"").indexOf("px")||y&&!~(o+"").indexOf("px"))&&(T=Xn(u,"x",s,"px"),y=Xn(u,"y",o,"px")),(m||p||g||x)&&(T=Me(T+m-(m*M+p*v)+g),y=Me(y+p-(m*w+p*C)+x)),(i||r)&&(E=u.getBBox(),T=Me(T+i/100*E.width),y=Me(y+r/100*E.height)),E="matrix("+M+","+w+","+v+","+C+","+T+","+y+")",u.setAttribute("transform",E),S&&(u.style[ge]=E)},tf=function(e,t,n,i,r){var s=360,o=Le(r),a=parseFloat(r)*(o&&~r.indexOf("rad")?di:1),l=a-i,h=i+l+"deg",f,d;return o&&(f=r.split("_")[1],f==="short"&&(l%=s,l!==l%(s/2)&&(l+=l<0?s:-s)),f==="cw"&&l<0?l=(l+s*ql)%s-~~(l/s)*s:f==="ccw"&&l>0&&(l=(l-s*ql)%s-~~(l/s)*s)),e._pt=d=new ht(e._pt,t,n,i,l,Bu),d.e=h,d.u="deg",e._props.push(n),d},jl=function(e,t){for(var n in t)e[n]=t[n];return e},nf=function(e,t,n){var i=jl({},n._gsap),r="perspective,force3D,transformOrigin,svgOrigin",s=n.style,o,a,l,h,f,d,u,m;i.svg?(l=n.getAttribute("transform"),n.setAttribute("transform",""),s[ge]=t,o=yr(n,1),mi(n,ge),n.setAttribute("transform",l)):(l=getComputedStyle(n)[ge],s[ge]=t,o=yr(n,1),s[ge]=l);for(a in bn)l=i[a],h=o[a],l!==h&&r.indexOf(a)<0&&(u=Xe(l),m=Xe(h),f=u!==m?Xn(n,a,l,m):parseFloat(l),d=parseFloat(h),e._pt=new ht(e._pt,o,a,f,d-f,Xo),e._pt.u=m||0,e._props.push(a));jl(o,i)};ct("padding,margin,Width,Radius",function(c,e){var t="Top",n="Right",i="Bottom",r="Left",s=(e<3?[t,n,i,r]:[t+r,t+n,i+n,i+r]).map(function(o){return e<2?c+o:"border"+o+c});us[e>1?"border"+c:c]=function(o,a,l,h,f){var d,u;if(arguments.length<4)return d=s.map(function(m){return Sn(o,m,l)}),u=d.join(" "),u.split(d[0]).length===5?d[0]:u;d=(h+"").split(" "),u={},s.forEach(function(m,p){return u[m]=d[p]=d[p]||d[(p-1)/2|0]}),o.init(a,u,f)}});var jo={name:"css",register:Yo,targetTest:function(e){return e.style&&e.nodeType},init:function(e,t,n,i,r){var s=this._props,o=e.style,a=n.vars.startAt,l,h,f,d,u,m,p,g,x,S,T,y,M,w,v,C;Jo||Yo(),this.styles=this.styles||ic(e),C=this.styles.props,this.tween=n;for(p in t)if(p!=="autoRound"&&(h=t[p],!(yt[p]&&Bo(p,t,n,i,e,r)))){if(u=typeof h,m=us[p],u==="function"&&(h=h.call(n,i,e,r),u=typeof h),u==="string"&&~h.indexOf("random(")&&(h=Fi(h)),m)m(this,e,p,h,n)&&(v=1);else if(p.substr(0,2)==="--")l=(getComputedStyle(e).getPropertyValue(p)+"").trim(),h+="",yn.lastIndex=0,yn.test(l)||(g=Xe(l),x=Xe(h)),x?g!==x&&(l=Xn(e,p,l,x)+x):g&&(h+=g),this.add(o,"setProperty",l,h,i,r,0,0,p),s.push(p),C.push(p,0,o[p]);else if(u!=="undefined"){if(a&&p in a?(l=typeof a[p]=="function"?a[p].call(n,i,e,r):a[p],Le(l)&&~l.indexOf("random(")&&(l=Fi(l)),Xe(l+"")||l==="auto"||(l+=bt.units[p]||Xe(Sn(e,p))||""),(l+"").charAt(1)==="="&&(l=Sn(e,p))):l=Sn(e,p),d=parseFloat(l),S=u==="string"&&h.charAt(1)==="="&&h.substr(0,2),S&&(h=h.substr(2)),f=parseFloat(h),p in un&&(p==="autoAlpha"&&(d===1&&Sn(e,"visibility")==="hidden"&&f&&(d=0),C.push("visibility",0,o.visibility),Wn(this,o,"visibility",d?"inherit":"hidden",f?"inherit":"hidden",!f)),p!=="scale"&&p!=="transform"&&(p=un[p],~p.indexOf(",")&&(p=p.split(",")[0]))),T=p in bn,T){if(this.styles.save(p),u==="string"&&h.substring(0,6)==="var(--"&&(h=Vt(e,h.substring(4,h.indexOf(")"))),f=parseFloat(h)),y||(M=e._gsap,M.renderTransform&&!t.parseTransform||yr(e,t.parseTransform),w=t.smoothOrigin!==!1&&M.smooth,y=this._pt=new ht(this._pt,o,ge,0,1,M.renderTransform,M,0,-1),y.dep=1),p==="scale")this._pt=new ht(this._pt,M,"scaleY",M.scaleY,(S?ci(M.scaleY,S+f):f)-M.scaleY||0,Xo),this._pt.u=0,s.push("scaleY",p),p+="X";else if(p==="transformOrigin"){C.push(Tt,0,o[Tt]),h=Ku(h),M.svg?Zo(e,h,0,w,0,this):(x=parseFloat(h.split(" ")[2])||0,x!==M.zOrigin&&Wn(this,M,"zOrigin",M.zOrigin,x),Wn(this,o,p,fs(l),fs(h)));continue}else if(p==="svgOrigin"){Zo(e,h,1,w,0,this);continue}else if(p in ac){tf(this,M,p,d,S?ci(d,S+h):h);continue}else if(p==="smoothOrigin"){Wn(this,M,"smooth",M.smooth,h);continue}else if(p==="force3D"){M[p]=h;continue}else if(p==="transform"){nf(this,h,e);continue}}else p in o||(p=ki(p)||p);if(T||(f||f===0)&&(d||d===0)&&!Ou.test(h)&&p in o)g=(l+"").substr((d+"").length),f||(f=0),x=Xe(h)||(p in bt.units?bt.units[p]:g),g!==x&&(d=Xn(e,p,l,x)),this._pt=new ht(this._pt,T?M:o,p,d,(S?ci(d,S+f):f)-d,!T&&(x==="px"||p==="zIndex")&&t.autoRound!==!1?ku:Xo),this._pt.u=x||0,g!==x&&x!=="%"&&(this._pt.b=l,this._pt.r=zu);else if(p in o)$u.call(this,e,p,l,S?S+h:h);else if(p in e)this.add(e,p,l||e[p],S?S+h:h,i,r);else if(p!=="parseTransform"){os(p,h);continue}T||(p in o?C.push(p,0,o[p]):typeof e[p]=="function"?C.push(p,2,e[p]()):C.push(p,1,l||e[p])),s.push(p)}}v&&Go(this)},render:function(e,t){if(t.tween._time||!$o())for(var n=t._pt;n;)n.r(e,n.d),n=n._next;else t.styles.revert()},get:Sn,aliases:un,getSetter:function(e,t,n){var i=un[t];return i&&i.indexOf(",")<0&&(t=i),t in bn&&t!==Tt&&(e._gsap.x||Sn(e,"x"))?n&&Xl===n?t==="scale"?Wu:Gu:(Xl=n||{})&&(t==="scale"?Xu:qu):e.style&&!ss(e.style[t])?Vu:~t.indexOf("-")?Hu:hs(e,t)},core:{_removeProperty:mi,_getMatrix:Qo}};je.utils.checkPrefix=ki;je.core.getStyleSaver=ic;(function(c,e,t,n){var i=ct(c+","+e+","+t,function(r){bn[r]=1});ct(e,function(r){bt.units[r]="deg",ac[r]=1}),un[i[13]]=c+","+e,ct(n,function(r){var s=r.split(":");un[s[1]]=i[s[0]]})})("x,y,z,scale,scaleX,scaleY,xPercent,yPercent","rotation,rotationX,rotationY,skewX,skewY","transform,transformOrigin,svgOrigin,force3D,smoothOrigin,transformPerspective","0:translateX,1:translateY,2:translateZ,8:rotate,8:rotationZ,8:rotateZ,9:rotateX,10:rotateY");ct("x,y,z,top,right,bottom,left,width,height,fontSize,padding,margin,perspective",function(c){bt.units[c]="px"});je.registerPlugin(jo);var ea=je.registerPlugin(jo)||je,Im=ea.core.Tween;function hc(c,e){for(var t=0;t<e.length;t++){var n=e[t];n.enumerable=n.enumerable||!1,n.configurable=!0,"value"in n&&(n.writable=!0),Object.defineProperty(c,n.key,n)}}function rf(c,e,t){return e&&hc(c.prototype,e),t&&hc(c,t),c}var qe,ms,sf,It,qn,Yn,Hi,fc,gi,Sr,dc,Tn,en,pc,mc=function(){return qe||typeof window<"u"&&(qe=window.gsap)&&qe.registerPlugin&&qe},gc=1,Vi=[],W=[],tn=[],br=Date.now,ta=function(e,t){return t},of=function(){var e=Sr.core,t=e.bridge||{},n=e._scrollers,i=e._proxies;n.push.apply(n,W),i.push.apply(i,tn),W=n,tn=i,ta=function(s,o){return t[s](o)}},En=function(e,t){return~tn.indexOf(e)&&tn[tn.indexOf(e)+1][t]},Tr=function(e){return!!~dc.indexOf(e)},ft=function(e,t,n,i,r){return e.addEventListener(t,n,{passive:i!==!1,capture:!!r})},ut=function(e,t,n,i){return e.removeEventListener(t,n,!!i)},ds="scrollLeft",ps="scrollTop",na=function(){return Tn&&Tn.isPressed||W.cache++},gs=function(e,t){var n=function i(r){if(r||r===0){gc&&(It.history.scrollRestoration="manual");var s=Tn&&Tn.isPressed;r=i.v=Math.round(r)||(Tn&&Tn.iOS?1:0),e(r),i.cacheID=W.cache,s&&ta("ss",r)}else(t||W.cache!==i.cacheID||ta("ref"))&&(i.cacheID=W.cache,i.v=e());return i.v+i.offset};return n.offset=0,e&&n},et={s:ds,p:"left",p2:"Left",os:"right",os2:"Right",d:"width",d2:"Width",a:"x",sc:gs(function(c){return arguments.length?It.scrollTo(c,Pe.sc()):It.pageXOffset||qn[ds]||Yn[ds]||Hi[ds]||0})},Pe={s:ps,p:"top",p2:"Top",os:"bottom",os2:"Bottom",d:"height",d2:"Height",a:"y",op:et,sc:gs(function(c){return arguments.length?It.scrollTo(et.sc(),c):It.pageYOffset||qn[ps]||Yn[ps]||Hi[ps]||0})},dt=function(e,t){return(t&&t._ctx&&t._ctx.selector||qe.utils.toArray)(e)[0]||(typeof e=="string"&&qe.config().nullTargetWarn!==!1?console.warn("Element not found:",e):null)},af=function(e,t){for(var n=t.length;n--;)if(t[n]===e||t[n].contains(e))return!0;return!1},wn=function(e,t){var n=t.s,i=t.sc;Tr(e)&&(e=qn.scrollingElement||Yn);var r=W.indexOf(e),s=i===Pe.sc?1:2;!~r&&(r=W.push(e)-1),W[r+s]||ft(e,"scroll",na);var o=W[r+s],a=o||(W[r+s]=gs(En(e,n),!0)||(Tr(e)?i:gs(function(l){return arguments.length?e[n]=l:e[n]})));return a.target=e,o||(a.smooth=qe.getProperty(e,"scrollBehavior")==="smooth"),a},_s=function(e,t,n){var i=e,r=e,s=br(),o=s,a=t||50,l=Math.max(500,a*3),h=function(m,p){var g=br();p||g-s>a?(r=i,i=m,o=s,s=g):n?i+=m:i=r+(m-r)/(g-o)*(s-o)},f=function(){r=i=n?0:i,o=s=0},d=function(m){var p=o,g=r,x=br();return(m||m===0)&&m!==i&&h(m),s===o||x-o>l?0:(i+(n?g:-g))/((n?x:s)-p)*1e3};return{update:h,reset:f,getVelocity:d}},Mr=function(e,t){return t&&!e._gsapAllow&&e.preventDefault(),e.changedTouches?e.changedTouches[0]:e},uc=function(e){var t=Math.max.apply(Math,e),n=Math.min.apply(Math,e);return Math.abs(t)>=Math.abs(n)?t:n},_c=function(){Sr=qe.core.globals().ScrollTrigger,Sr&&Sr.core&&of()},xc=function(e){return qe=e||mc(),!ms&&qe&&typeof document<"u"&&document.body&&(It=window,qn=document,Yn=qn.documentElement,Hi=qn.body,dc=[It,qn,Yn,Hi],sf=qe.utils.clamp,pc=qe.core.context||function(){},gi="onpointerenter"in Hi?"pointer":"mouse",fc=Se.isTouch=It.matchMedia&&It.matchMedia("(hover: none), (pointer: coarse)").matches?1:"ontouchstart"in It||navigator.maxTouchPoints>0||navigator.msMaxTouchPoints>0?2:0,en=Se.eventTypes=("ontouchstart"in Yn?"touchstart,touchmove,touchcancel,touchend":"onpointerdown"in Yn?"pointerdown,pointermove,pointercancel,pointerup":"mousedown,mousemove,mouseup,mouseup").split(","),setTimeout(function(){return gc=0},500),_c(),ms=1),ms};et.op=Pe;W.cache=0;var Se=(function(){function c(t){this.init(t)}var e=c.prototype;return e.init=function(n){ms||xc(qe)||console.warn("Please gsap.registerPlugin(Observer)"),Sr||_c();var i=n.tolerance,r=n.dragMinimum,s=n.type,o=n.target,a=n.lineHeight,l=n.debounce,h=n.preventDefault,f=n.onStop,d=n.onStopDelay,u=n.ignore,m=n.wheelSpeed,p=n.event,g=n.onDragStart,x=n.onDragEnd,S=n.onDrag,T=n.onPress,y=n.onRelease,M=n.onRight,w=n.onLeft,v=n.onUp,C=n.onDown,E=n.onChangeX,A=n.onChangeY,F=n.onChange,R=n.onToggleX,V=n.onToggleY,z=n.onHover,G=n.onHoverEnd,H=n.onMove,L=n.ignoreCheck,J=n.isNormalizer,ie=n.onGestureStart,_=n.onGestureEnd,re=n.onWheel,ot=n.onEnable,Zt=n.onDisable,_e=n.onClick,ke=n.scrollSpeed,Ye=n.capture,Ae=n.allowClicks,at=n.lockAxis,Ze=n.onLockAxis;this.target=o=dt(o)||Yn,this.vars=n,u&&(u=qe.utils.toArray(u)),i=i||1e-9,r=r||0,m=m||1,ke=ke||1,s=s||"wheel,touch,pointer",l=l!==!1,a||(a=parseFloat(It.getComputedStyle(Hi).lineHeight)||22);var Dn,lt,xt,te,be,vt,wt,b=this,Et=0,pn=0,Ln=n.passive||!h&&n.passive!==!1,xe=wn(o,et),mn=wn(o,Pe),Un=xe(),ei=mn(),Ne=~s.indexOf("touch")&&!~s.indexOf("pointer")&&en[0]==="pointerdown",Nn=Tr(o),Te=o.ownerDocument||qn,Jt=[0,0,0],Ot=[0,0,0],gn=0,rr=function(){return gn=br()},Ce=function(N,ne){return(b.event=N)&&u&&af(N.target,u)||ne&&Ne&&N.pointerType!=="touch"||L&&L(N,ne)},Yr=function(){b._vx.reset(),b._vy.reset(),lt.pause(),f&&f(b)},_n=function(){var N=b.deltaX=uc(Jt),ne=b.deltaY=uc(Ot),P=Math.abs(N)>=i,O=Math.abs(ne)>=i;F&&(P||O)&&F(b,N,ne,Jt,Ot),P&&(M&&b.deltaX>0&&M(b),w&&b.deltaX<0&&w(b),E&&E(b),R&&b.deltaX<0!=Et<0&&R(b),Et=b.deltaX,Jt[0]=Jt[1]=Jt[2]=0),O&&(C&&b.deltaY>0&&C(b),v&&b.deltaY<0&&v(b),A&&A(b),V&&b.deltaY<0!=pn<0&&V(b),pn=b.deltaY,Ot[0]=Ot[1]=Ot[2]=0),(te||xt)&&(H&&H(b),xt&&(g&&xt===1&&g(b),S&&S(b),xt=0),te=!1),vt&&!(vt=!1)&&Ze&&Ze(b),be&&(re(b),be=!1),Dn=0},Ai=function(N,ne,P){Jt[P]+=N,Ot[P]+=ne,b._vx.update(N),b._vy.update(ne),l?Dn||(Dn=requestAnimationFrame(_n)):_n()},Ci=function(N,ne){at&&!wt&&(b.axis=wt=Math.abs(N)>Math.abs(ne)?"x":"y",vt=!0),wt!=="y"&&(Jt[2]+=N,b._vx.update(N,!0)),wt!=="x"&&(Ot[2]+=ne,b._vy.update(ne,!0)),l?Dn||(Dn=requestAnimationFrame(_n)):_n()},Fn=function(N){if(!Ce(N,1)){N=Mr(N,h);var ne=N.clientX,P=N.clientY,O=ne-b.x,U=P-b.y,B=b.isDragging;b.x=ne,b.y=P,(B||(O||U)&&(Math.abs(b.startX-ne)>=r||Math.abs(b.startY-P)>=r))&&(xt=B?2:1,B||(b.isDragging=!0),Ci(O,U))}},ti=b.onPress=function(k){Ce(k,1)||k&&k.button||(b.axis=wt=null,lt.pause(),b.isPressed=!0,k=Mr(k),Et=pn=0,b.startX=b.x=k.clientX,b.startY=b.y=k.clientY,b._vx.reset(),b._vy.reset(),ft(J?o:Te,en[1],Fn,Ln,!0),b.deltaX=b.deltaY=0,T&&T(b))},$=b.onRelease=function(k){if(!Ce(k,1)){ut(J?o:Te,en[1],Fn,!0);var N=!isNaN(b.y-b.startY),ne=b.isDragging,P=ne&&(Math.abs(b.x-b.startX)>3||Math.abs(b.y-b.startY)>3),O=Mr(k);!P&&N&&(b._vx.reset(),b._vy.reset(),h&&Ae&&qe.delayedCall(.08,function(){if(br()-gn>300&&!k.defaultPrevented){if(k.target.click)k.target.click();else if(Te.createEvent){var U=Te.createEvent("MouseEvents");U.initMouseEvent("click",!0,!0,It,1,O.screenX,O.screenY,O.clientX,O.clientY,!1,!1,!1,!1,0,null),k.target.dispatchEvent(U)}}})),b.isDragging=b.isGesturing=b.isPressed=!1,f&&ne&&!J&&lt.restart(!0),xt&&_n(),x&&ne&&x(b),y&&y(b,P)}},ni=function(N){return N.touches&&N.touches.length>1&&(b.isGesturing=!0)&&ie(N,b.isDragging)},$t=function(){return(b.isGesturing=!1)||_(b)},Kt=function(N){if(!Ce(N)){var ne=xe(),P=mn();Ai((ne-Un)*ke,(P-ei)*ke,1),Un=ne,ei=P,f&&lt.restart(!0)}},Qt=function(N){if(!Ce(N)){N=Mr(N,h),re&&(be=!0);var ne=(N.deltaMode===1?a:N.deltaMode===2?It.innerHeight:1)*m;Ai(N.deltaX*ne,N.deltaY*ne,0),f&&!J&&lt.restart(!0)}},ii=function(N){if(!Ce(N)){var ne=N.clientX,P=N.clientY,O=ne-b.x,U=P-b.y;b.x=ne,b.y=P,te=!0,f&&lt.restart(!0),(O||U)&&Ci(O,U)}},Ri=function(N){b.event=N,z(b)},xn=function(N){b.event=N,G(b)},sr=function(N){return Ce(N)||Mr(N,h)&&_e(b)};lt=b._dc=qe.delayedCall(d||.25,Yr).pause(),b.deltaX=b.deltaY=0,b._vx=_s(0,50,!0),b._vy=_s(0,50,!0),b.scrollX=xe,b.scrollY=mn,b.isDragging=b.isGesturing=b.isPressed=!1,pc(this),b.enable=function(k){return b.isEnabled||(ft(Nn?Te:o,"scroll",na),s.indexOf("scroll")>=0&&ft(Nn?Te:o,"scroll",Kt,Ln,Ye),s.indexOf("wheel")>=0&&ft(o,"wheel",Qt,Ln,Ye),(s.indexOf("touch")>=0&&fc||s.indexOf("pointer")>=0)&&(ft(o,en[0],ti,Ln,Ye),ft(Te,en[2],$),ft(Te,en[3],$),Ae&&ft(o,"click",rr,!0,!0),_e&&ft(o,"click",sr),ie&&ft(Te,"gesturestart",ni),_&&ft(Te,"gestureend",$t),z&&ft(o,gi+"enter",Ri),G&&ft(o,gi+"leave",xn),H&&ft(o,gi+"move",ii)),b.isEnabled=!0,b.isDragging=b.isGesturing=b.isPressed=te=xt=!1,b._vx.reset(),b._vy.reset(),Un=xe(),ei=mn(),k&&k.type&&ti(k),ot&&ot(b)),b},b.disable=function(){b.isEnabled&&(Vi.filter(function(k){return k!==b&&Tr(k.target)}).length||ut(Nn?Te:o,"scroll",na),b.isPressed&&(b._vx.reset(),b._vy.reset(),ut(J?o:Te,en[1],Fn,!0)),ut(Nn?Te:o,"scroll",Kt,Ye),ut(o,"wheel",Qt,Ye),ut(o,en[0],ti,Ye),ut(Te,en[2],$),ut(Te,en[3],$),ut(o,"click",rr,!0),ut(o,"click",sr),ut(Te,"gesturestart",ni),ut(Te,"gestureend",$t),ut(o,gi+"enter",Ri),ut(o,gi+"leave",xn),ut(o,gi+"move",ii),b.isEnabled=b.isPressed=b.isDragging=!1,Zt&&Zt(b))},b.kill=b.revert=function(){b.disable();var k=Vi.indexOf(b);k>=0&&Vi.splice(k,1),Tn===b&&(Tn=0)},Vi.push(b),J&&Tr(o)&&(Tn=b),b.enable(p)},rf(c,[{key:"velocityX",get:function(){return this._vx.getVelocity()}},{key:"velocityY",get:function(){return this._vy.getVelocity()}}]),c})();Se.version="3.13.0";Se.create=function(c){return new Se(c)};Se.register=xc;Se.getAll=function(){return Vi.slice()};Se.getById=function(c){return Vi.filter(function(e){return e.vars.id===c})[0]};mc()&&qe.registerPlugin(Se);var D,Xi,q,ce,Ut,se,_a,Ls,Or,Ir,Er,xs,tt,Os,ha,mt,vc,yc,qi,Fc,ia,Oc,pt,ua,Bc,zc,Zn,fa,xa,Yi,va,Us,da,ra,vs=1,nt=Date.now,sa=nt(),Wt=0,Ar=0,Mc=function(e,t,n){var i=Lt(e)&&(e.substr(0,6)==="clamp("||e.indexOf("max")>-1);return n["_"+t+"Clamp"]=i,i?e.substr(6,e.length-7):e},Sc=function(e,t){return t&&(!Lt(e)||e.substr(0,6)!=="clamp(")?"clamp("+e+")":e},lf=function c(){return Ar&&requestAnimationFrame(c)},bc=function(){return Os=1},Tc=function(){return Os=0},fn=function(e){return e},Cr=function(e){return Math.round(e*1e5)/1e5||0},kc=function(){return typeof window<"u"},Vc=function(){return D||kc()&&(D=window.gsap)&&D.registerPlugin&&D},Si=function(e){return!!~_a.indexOf(e)},Hc=function(e){return(e==="Height"?va:q["inner"+e])||Ut["client"+e]||se["client"+e]},Gc=function(e){return En(e,"getBoundingClientRect")||(Si(e)?function(){return Ds.width=q.innerWidth,Ds.height=va,Ds}:function(){return An(e)})},cf=function(e,t,n){var i=n.d,r=n.d2,s=n.a;return(s=En(e,"getBoundingClientRect"))?function(){return s()[i]}:function(){return(t?Hc(r):e["client"+r])||0}},hf=function(e,t){return!t||~tn.indexOf(e)?Gc(e):function(){return Ds}},dn=function(e,t){var n=t.s,i=t.d2,r=t.d,s=t.a;return Math.max(0,(n="scroll"+i)&&(s=En(e,n))?s()-Gc(e)()[r]:Si(e)?(Ut[n]||se[n])-Hc(i):e[n]-e["offset"+i])},ys=function(e,t){for(var n=0;n<qi.length;n+=3)(!t||~t.indexOf(qi[n+1]))&&e(qi[n],qi[n+1],qi[n+2])},Lt=function(e){return typeof e=="string"},it=function(e){return typeof e=="function"},Rr=function(e){return typeof e=="number"},_i=function(e){return typeof e=="object"},wr=function(e,t,n){return e&&e.progress(t?0:1)&&n&&e.pause()},oa=function(e,t){if(e.enabled){var n=e._ctx?e._ctx.add(function(){return t(e)}):t(e);n&&n.totalTime&&(e.callbackAnimation=n)}},Gi=Math.abs,Wc="left",Xc="top",ya="right",Ma="bottom",vi="width",yi="height",Dr="Right",Lr="Left",Ur="Top",Nr="Bottom",Ie="padding",Ht="margin",Ji="Width",Sa="Height",Ue="px",Gt=function(e){return q.getComputedStyle(e)},uf=function(e){var t=Gt(e).position;e.style.position=t==="absolute"||t==="fixed"?t:"relative"},wc=function(e,t){for(var n in t)n in e||(e[n]=t[n]);return e},An=function(e,t){var n=t&&Gt(e)[ha]!=="matrix(1, 0, 0, 1, 0, 0)"&&D.to(e,{x:0,y:0,xPercent:0,yPercent:0,rotation:0,rotationX:0,rotationY:0,scale:1,skewX:0,skewY:0}).progress(1),i=e.getBoundingClientRect();return n&&n.progress(0).kill(),i},Ns=function(e,t){var n=t.d2;return e["offset"+n]||e["client"+n]||0},qc=function(e){var t=[],n=e.labels,i=e.duration(),r;for(r in n)t.push(n[r]/i);return t},ff=function(e){return function(t){return D.utils.snap(qc(e),t)}},ba=function(e){var t=D.utils.snap(e),n=Array.isArray(e)&&e.slice(0).sort(function(i,r){return i-r});return n?function(i,r,s){s===void 0&&(s=.001);var o;if(!r)return t(i);if(r>0){for(i-=s,o=0;o<n.length;o++)if(n[o]>=i)return n[o];return n[o-1]}else for(o=n.length,i+=s;o--;)if(n[o]<=i)return n[o];return n[0]}:function(i,r,s){s===void 0&&(s=.001);var o=t(i);return!r||Math.abs(o-i)<s||o-i<0==r<0?o:t(r<0?i-e:i+e)}},df=function(e){return function(t,n){return ba(qc(e))(t,n.direction)}},Ms=function(e,t,n,i){return n.split(",").forEach(function(r){return e(t,r,i)})},Be=function(e,t,n,i,r){return e.addEventListener(t,n,{passive:!i,capture:!!r})},Oe=function(e,t,n,i){return e.removeEventListener(t,n,!!i)},Ss=function(e,t,n){n=n&&n.wheelHandler,n&&(e(t,"wheel",n),e(t,"touchmove",n))},Ec={startColor:"green",endColor:"red",indent:0,fontSize:"16px",fontWeight:"normal"},bs={toggleActions:"play",anticipatePin:0},Fs={top:0,left:0,center:.5,bottom:1,right:1},Cs=function(e,t){if(Lt(e)){var n=e.indexOf("="),i=~n?+(e.charAt(n-1)+1)*parseFloat(e.substr(n+1)):0;~n&&(e.indexOf("%")>n&&(i*=t/100),e=e.substr(0,n-1)),e=i+(e in Fs?Fs[e]*t:~e.indexOf("%")?parseFloat(e)*t/100:parseFloat(e)||0)}return e},Ts=function(e,t,n,i,r,s,o,a){var l=r.startColor,h=r.endColor,f=r.fontSize,d=r.indent,u=r.fontWeight,m=ce.createElement("div"),p=Si(n)||En(n,"pinType")==="fixed",g=e.indexOf("scroller")!==-1,x=p?se:n,S=e.indexOf("start")!==-1,T=S?l:h,y="border-color:"+T+";font-size:"+f+";color:"+T+";font-weight:"+u+";pointer-events:none;white-space:nowrap;font-family:sans-serif,Arial;z-index:1000;padding:4px 8px;border-width:0;border-style:solid;";return y+="position:"+((g||a)&&p?"fixed;":"absolute;"),(g||a||!p)&&(y+=(i===Pe?ya:Ma)+":"+(s+parseFloat(d))+"px;"),o&&(y+="box-sizing:border-box;text-align:left;width:"+o.offsetWidth+"px;"),m._isStart=S,m.setAttribute("class","gsap-marker-"+e+(t?" marker-"+t:"")),m.style.cssText=y,m.innerText=t||t===0?e+"-"+t:e,x.children[0]?x.insertBefore(m,x.children[0]):x.appendChild(m),m._offset=m["offset"+i.op.d2],Rs(m,0,i,S),m},Rs=function(e,t,n,i){var r={display:"block"},s=n[i?"os2":"p2"],o=n[i?"p2":"os2"];e._isFlipped=i,r[n.a+"Percent"]=i?-100:0,r[n.a]=i?"1px":0,r["border"+s+Ji]=1,r["border"+o+Ji]=0,r[n.p]=t+"px",D.set(e,r)},X=[],pa={},Br,Ac=function(){return nt()-Wt>34&&(Br||(Br=requestAnimationFrame(Cn)))},Wi=function(){(!pt||!pt.isPressed||pt.startX>se.clientWidth)&&(W.cache++,pt?Br||(Br=requestAnimationFrame(Cn)):Cn(),Wt||Ti("scrollStart"),Wt=nt())},aa=function(){zc=q.innerWidth,Bc=q.innerHeight},Pr=function(e){W.cache++,(e===!0||!tt&&!Oc&&!ce.fullscreenElement&&!ce.webkitFullscreenElement&&(!ua||zc!==q.innerWidth||Math.abs(q.innerHeight-Bc)>q.innerHeight*.25))&&Ls.restart(!0)},bi={},pf=[],Yc=function c(){return Oe(Y,"scrollEnd",c)||xi(!0)},Ti=function(e){return bi[e]&&bi[e].map(function(t){return t()})||pf},Dt=[],Zc=function(e){for(var t=0;t<Dt.length;t+=5)(!e||Dt[t+4]&&Dt[t+4].query===e)&&(Dt[t].style.cssText=Dt[t+1],Dt[t].getBBox&&Dt[t].setAttribute("transform",Dt[t+2]||""),Dt[t+3].uncache=1)},Ta=function(e,t){var n;for(mt=0;mt<X.length;mt++)n=X[mt],n&&(!t||n._ctx===t)&&(e?n.kill(1):n.revert(!0,!0));Us=!0,t&&Zc(t),t||Ti("revert")},Jc=function(e,t){W.cache++,(t||!gt)&&W.forEach(function(n){return it(n)&&n.cacheID++&&(n.rec=0)}),Lt(e)&&(q.history.scrollRestoration=xa=e)},gt,Mi=0,Cc,mf=function(){if(Cc!==Mi){var e=Cc=Mi;requestAnimationFrame(function(){return e===Mi&&xi(!0)})}},$c=function(){se.appendChild(Yi),va=!pt&&Yi.offsetHeight||q.innerHeight,se.removeChild(Yi)},Rc=function(e){return Or(".gsap-marker-start, .gsap-marker-end, .gsap-marker-scroller-start, .gsap-marker-scroller-end").forEach(function(t){return t.style.display=e?"none":"block"})},xi=function(e,t){if(Ut=ce.documentElement,se=ce.body,_a=[q,ce,Ut,se],Wt&&!e&&!Us){Be(Y,"scrollEnd",Yc);return}$c(),gt=Y.isRefreshing=!0,W.forEach(function(i){return it(i)&&++i.cacheID&&(i.rec=i())});var n=Ti("refreshInit");Fc&&Y.sort(),t||Ta(),W.forEach(function(i){it(i)&&(i.smooth&&(i.target.style.scrollBehavior="auto"),i(0))}),X.slice(0).forEach(function(i){return i.refresh()}),Us=!1,X.forEach(function(i){if(i._subPinOffset&&i.pin){var r=i.vars.horizontal?"offsetWidth":"offsetHeight",s=i.pin[r];i.revert(!0,1),i.adjustPinSpacing(i.pin[r]-s),i.refresh()}}),da=1,Rc(!0),X.forEach(function(i){var r=dn(i.scroller,i._dir),s=i.vars.end==="max"||i._endClamp&&i.end>r,o=i._startClamp&&i.start>=r;(s||o)&&i.setPositions(o?r-1:i.start,s?Math.max(o?r:i.start+1,r):i.end,!0)}),Rc(!1),da=0,n.forEach(function(i){return i&&i.render&&i.render(-1)}),W.forEach(function(i){it(i)&&(i.smooth&&requestAnimationFrame(function(){return i.target.style.scrollBehavior="smooth"}),i.rec&&i(i.rec))}),Jc(xa,1),Ls.pause(),Mi++,gt=2,Cn(2),X.forEach(function(i){return it(i.vars.onRefresh)&&i.vars.onRefresh(i)}),gt=Y.isRefreshing=!1,Ti("refresh")},ma=0,Ps=1,Fr,Cn=function(e){if(e===2||!gt&&!Us){Y.isUpdating=!0,Fr&&Fr.update(0);var t=X.length,n=nt(),i=n-sa>=50,r=t&&X[0].scroll();if(Ps=ma>r?-1:1,gt||(ma=r),i&&(Wt&&!Os&&n-Wt>200&&(Wt=0,Ti("scrollEnd")),Er=sa,sa=n),Ps<0){for(mt=t;mt-- >0;)X[mt]&&X[mt].update(0,i);Ps=1}else for(mt=0;mt<t;mt++)X[mt]&&X[mt].update(0,i);Y.isUpdating=!1}Br=0},ga=[Wc,Xc,Ma,ya,Ht+Nr,Ht+Dr,Ht+Ur,Ht+Lr,"display","flexShrink","float","zIndex","gridColumnStart","gridColumnEnd","gridRowStart","gridRowEnd","gridArea","justifySelf","alignSelf","placeSelf","order"],Is=ga.concat([vi,yi,"boxSizing","max"+Ji,"max"+Sa,"position",Ht,Ie,Ie+Ur,Ie+Dr,Ie+Nr,Ie+Lr]),gf=function(e,t,n){Zi(n);var i=e._gsap;if(i.spacerIsNative)Zi(i.spacerState);else if(e._gsap.swappedIn){var r=t.parentNode;r&&(r.insertBefore(e,t),r.removeChild(t))}e._gsap.swappedIn=!1},la=function(e,t,n,i){if(!e._gsap.swappedIn){for(var r=ga.length,s=t.style,o=e.style,a;r--;)a=ga[r],s[a]=n[a];s.position=n.position==="absolute"?"absolute":"relative",n.display==="inline"&&(s.display="inline-block"),o[Ma]=o[ya]="auto",s.flexBasis=n.flexBasis||"auto",s.overflow="visible",s.boxSizing="border-box",s[vi]=Ns(e,et)+Ue,s[yi]=Ns(e,Pe)+Ue,s[Ie]=o[Ht]=o[Xc]=o[Wc]="0",Zi(i),o[vi]=o["max"+Ji]=n[vi],o[yi]=o["max"+Sa]=n[yi],o[Ie]=n[Ie],e.parentNode!==t&&(e.parentNode.insertBefore(t,e),t.appendChild(e)),e._gsap.swappedIn=!0}},_f=/([A-Z])/g,Zi=function(e){if(e){var t=e.t.style,n=e.length,i=0,r,s;for((e.t._gsap||D.core.getCache(e.t)).uncache=1;i<n;i+=2)s=e[i+1],r=e[i],s?t[r]=s:t[r]&&t.removeProperty(r.replace(_f,"-$1").toLowerCase())}},ws=function(e){for(var t=Is.length,n=e.style,i=[],r=0;r<t;r++)i.push(Is[r],n[Is[r]]);return i.t=e,i},xf=function(e,t,n){for(var i=[],r=e.length,s=n?8:0,o;s<r;s+=2)o=e[s],i.push(o,o in t?t[o]:e[s+1]);return i.t=e.t,i},Ds={left:0,top:0},Pc=function(e,t,n,i,r,s,o,a,l,h,f,d,u,m){it(e)&&(e=e(a)),Lt(e)&&e.substr(0,3)==="max"&&(e=d+(e.charAt(4)==="="?Cs("0"+e.substr(3),n):0));var p=u?u.time():0,g,x,S;if(u&&u.seek(0),isNaN(e)||(e=+e),Rr(e))u&&(e=D.utils.mapRange(u.scrollTrigger.start,u.scrollTrigger.end,0,d,e)),o&&Rs(o,n,i,!0);else{it(t)&&(t=t(a));var T=(e||"0").split(" "),y,M,w,v;S=dt(t,a)||se,y=An(S)||{},(!y||!y.left&&!y.top)&&Gt(S).display==="none"&&(v=S.style.display,S.style.display="block",y=An(S),v?S.style.display=v:S.style.removeProperty("display")),M=Cs(T[0],y[i.d]),w=Cs(T[1]||"0",n),e=y[i.p]-l[i.p]-h+M+r-w,o&&Rs(o,w,i,n-w<20||o._isStart&&w>20),n-=n-w}if(m&&(a[m]=e||-.001,e<0&&(e=0)),s){var C=e+n,E=s._isStart;g="scroll"+i.d2,Rs(s,C,i,E&&C>20||!E&&(f?Math.max(se[g],Ut[g]):s.parentNode[g])<=C+1),f&&(l=An(o),f&&(s.style[i.op.p]=l[i.op.p]-i.op.m-s._offset+Ue))}return u&&S&&(g=An(S),u.seek(d),x=An(S),u._caScrollDist=g[i.p]-x[i.p],e=e/u._caScrollDist*d),u&&u.seek(p),u?e:Math.round(e)},vf=/(webkit|moz|length|cssText|inset)/i,Ic=function(e,t,n,i){if(e.parentNode!==t){var r=e.style,s,o;if(t===se){e._stOrig=r.cssText,o=Gt(e);for(s in o)!+s&&!vf.test(s)&&o[s]&&typeof r[s]=="string"&&s!=="0"&&(r[s]=o[s]);r.top=n,r.left=i}else r.cssText=e._stOrig;D.core.getCache(e).uncache=1,t.appendChild(e)}},Kc=function(e,t,n){var i=t,r=i;return function(s){var o=Math.round(e());return o!==i&&o!==r&&Math.abs(o-i)>3&&Math.abs(o-r)>3&&(s=o,n&&n()),r=i,i=Math.round(s),i}},Es=function(e,t,n){var i={};i[t.p]="+="+n,D.set(e,i)},Dc=function(e,t){var n=wn(e,t),i="_scroll"+t.p2,r=function s(o,a,l,h,f){var d=s.tween,u=a.onComplete,m={};l=l||n();var p=Kc(n,l,function(){d.kill(),s.tween=0});return f=h&&f||0,h=h||o-l,d&&d.kill(),a[i]=o,a.inherit=!1,a.modifiers=m,m[i]=function(){return p(l+h*d.ratio+f*d.ratio*d.ratio)},a.onUpdate=function(){W.cache++,s.tween&&Cn()},a.onComplete=function(){s.tween=0,u&&u.call(d)},d=s.tween=D.to(e,a),d};return e[i]=n,n.wheelHandler=function(){return r.tween&&r.tween.kill()&&(r.tween=0)},Be(e,"wheel",n.wheelHandler),Y.isTouch&&Be(e,"touchmove",n.wheelHandler),r},Y=(function(){function c(t,n){Xi||c.register(D)||console.warn("Please gsap.registerPlugin(ScrollTrigger)"),fa(this),this.init(t,n)}var e=c.prototype;return e.init=function(n,i){if(this.progress=this.start=0,this.vars&&this.kill(!0,!0),!Ar){this.update=this.refresh=this.kill=fn;return}n=wc(Lt(n)||Rr(n)||n.nodeType?{trigger:n}:n,bs);var r=n,s=r.onUpdate,o=r.toggleClass,a=r.id,l=r.onToggle,h=r.onRefresh,f=r.scrub,d=r.trigger,u=r.pin,m=r.pinSpacing,p=r.invalidateOnRefresh,g=r.anticipatePin,x=r.onScrubComplete,S=r.onSnapComplete,T=r.once,y=r.snap,M=r.pinReparent,w=r.pinSpacer,v=r.containerAnimation,C=r.fastScrollEnd,E=r.preventOverlaps,A=n.horizontal||n.containerAnimation&&n.horizontal!==!1?et:Pe,F=!f&&f!==0,R=dt(n.scroller||q),V=D.core.getCache(R),z=Si(R),G=("pinType"in n?n.pinType:En(R,"pinType")||z&&"fixed")==="fixed",H=[n.onEnter,n.onLeave,n.onEnterBack,n.onLeaveBack],L=F&&n.toggleActions.split(" "),J="markers"in n?n.markers:bs.markers,ie=z?0:parseFloat(Gt(R)["border"+A.p2+Ji])||0,_=this,re=n.onRefreshInit&&function(){return n.onRefreshInit(_)},ot=cf(R,z,A),Zt=hf(R,z),_e=0,ke=0,Ye=0,Ae=wn(R,A),at,Ze,Dn,lt,xt,te,be,vt,wt,b,Et,pn,Ln,xe,mn,Un,ei,Ne,Nn,Te,Jt,Ot,gn,rr,Ce,Yr,_n,Ai,Ci,Fn,ti,$,ni,$t,Kt,Qt,ii,Ri,xn;if(_._startClamp=_._endClamp=!1,_._dir=A,g*=45,_.scroller=R,_.scroll=v?v.time.bind(v):Ae,lt=Ae(),_.vars=n,i=i||n.animation,"refreshPriority"in n&&(Fc=1,n.refreshPriority===-9999&&(Fr=_)),V.tweenScroll=V.tweenScroll||{top:Dc(R,Pe),left:Dc(R,et)},_.tweenTo=at=V.tweenScroll[A.p],_.scrubDuration=function(P){ni=Rr(P)&&P,ni?$?$.duration(P):$=D.to(i,{ease:"expo",totalProgress:"+=0",inherit:!1,duration:ni,paused:!0,onComplete:function(){return x&&x(_)}}):($&&$.progress(1).kill(),$=0)},i&&(i.vars.lazy=!1,i._initted&&!_.isReverted||i.vars.immediateRender!==!1&&n.immediateRender!==!1&&i.duration()&&i.render(0,!0,!0),_.animation=i.pause(),i.scrollTrigger=_,_.scrubDuration(f),Fn=0,a||(a=i.vars.id)),y&&((!_i(y)||y.push)&&(y={snapTo:y}),"scrollBehavior"in se.style&&D.set(z?[se,Ut]:R,{scrollBehavior:"auto"}),W.forEach(function(P){return it(P)&&P.target===(z?ce.scrollingElement||Ut:R)&&(P.smooth=!1)}),Dn=it(y.snapTo)?y.snapTo:y.snapTo==="labels"?ff(i):y.snapTo==="labelsDirectional"?df(i):y.directional!==!1?function(P,O){return ba(y.snapTo)(P,nt()-ke<500?0:O.direction)}:D.utils.snap(y.snapTo),$t=y.duration||{min:.1,max:2},$t=_i($t)?Ir($t.min,$t.max):Ir($t,$t),Kt=D.delayedCall(y.delay||ni/2||.1,function(){var P=Ae(),O=nt()-ke<500,U=at.tween;if((O||Math.abs(_.getVelocity())<10)&&!U&&!Os&&_e!==P){var B=(P-te)/xe,Fe=i&&!F?i.totalProgress():B,Q=O?0:(Fe-ti)/(nt()-Er)*1e3||0,we=D.utils.clamp(-B,1-B,Gi(Q/2)*Q/.185),Je=B+(y.inertia===!1?0:we),ve,fe,oe=y,jt=oe.onStart,pe=oe.onInterrupt,At=oe.onComplete;if(ve=Dn(Je,_),Rr(ve)||(ve=Je),fe=Math.max(0,Math.round(te+ve*xe)),P<=be&&P>=te&&fe!==P){if(U&&!U._initted&&U.data<=Gi(fe-P))return;y.inertia===!1&&(we=ve-B),at(fe,{duration:$t(Gi(Math.max(Gi(Je-Fe),Gi(ve-Fe))*.185/Q/.05||0)),ease:y.ease||"power3",data:Gi(fe-P),onInterrupt:function(){return Kt.restart(!0)&&pe&&pe(_)},onComplete:function(){_.update(),_e=Ae(),i&&!F&&($?$.resetTo("totalProgress",ve,i._tTime/i._tDur):i.progress(ve)),Fn=ti=i&&!F?i.totalProgress():_.progress,S&&S(_),At&&At(_)}},P,we*xe,fe-P-we*xe),jt&&jt(_,at.tween)}}else _.isActive&&_e!==P&&Kt.restart(!0)}).pause()),a&&(pa[a]=_),d=_.trigger=dt(d||u!==!0&&u),xn=d&&d._gsap&&d._gsap.stRevert,xn&&(xn=xn(_)),u=u===!0?d:dt(u),Lt(o)&&(o={targets:d,className:o}),u&&(m===!1||m===Ht||(m=!m&&u.parentNode&&u.parentNode.style&&Gt(u.parentNode).display==="flex"?!1:Ie),_.pin=u,Ze=D.core.getCache(u),Ze.spacer?mn=Ze.pinState:(w&&(w=dt(w),w&&!w.nodeType&&(w=w.current||w.nativeElement),Ze.spacerIsNative=!!w,w&&(Ze.spacerState=ws(w))),Ze.spacer=Ne=w||ce.createElement("div"),Ne.classList.add("pin-spacer"),a&&Ne.classList.add("pin-spacer-"+a),Ze.pinState=mn=ws(u)),n.force3D!==!1&&D.set(u,{force3D:!0}),_.spacer=Ne=Ze.spacer,Ci=Gt(u),rr=Ci[m+A.os2],Te=D.getProperty(u),Jt=D.quickSetter(u,A.a,Ue),la(u,Ne,Ci),ei=ws(u)),J){pn=_i(J)?wc(J,Ec):Ec,b=Ts("scroller-start",a,R,A,pn,0),Et=Ts("scroller-end",a,R,A,pn,0,b),Nn=b["offset"+A.op.d2];var sr=dt(En(R,"content")||R);vt=this.markerStart=Ts("start",a,sr,A,pn,Nn,0,v),wt=this.markerEnd=Ts("end",a,sr,A,pn,Nn,0,v),v&&(Ri=D.quickSetter([vt,wt],A.a,Ue)),!G&&!(tn.length&&En(R,"fixedMarkers")===!0)&&(uf(z?se:R),D.set([b,Et],{force3D:!0}),Yr=D.quickSetter(b,A.a,Ue),Ai=D.quickSetter(Et,A.a,Ue))}if(v){var k=v.vars.onUpdate,N=v.vars.onUpdateParams;v.eventCallback("onUpdate",function(){_.update(0,0,1),k&&k.apply(v,N||[])})}if(_.previous=function(){return X[X.indexOf(_)-1]},_.next=function(){return X[X.indexOf(_)+1]},_.revert=function(P,O){if(!O)return _.kill(!0);var U=P!==!1||!_.enabled,B=tt;U!==_.isReverted&&(U&&(Qt=Math.max(Ae(),_.scroll.rec||0),Ye=_.progress,ii=i&&i.progress()),vt&&[vt,wt,b,Et].forEach(function(Fe){return Fe.style.display=U?"none":"block"}),U&&(tt=_,_.update(U)),u&&(!M||!_.isActive)&&(U?gf(u,Ne,mn):la(u,Ne,Gt(u),Ce)),U||_.update(U),tt=B,_.isReverted=U)},_.refresh=function(P,O,U,B){if(!((tt||!_.enabled)&&!O)){if(u&&P&&Wt){Be(c,"scrollEnd",Yc);return}!gt&&re&&re(_),tt=_,at.tween&&!U&&(at.tween.kill(),at.tween=0),$&&$.pause(),p&&i&&(i.revert({kill:!1}).invalidate(),i.getChildren&&i.getChildren(!0,!0,!1).forEach(function(On){return On.vars.immediateRender&&On.render(0,!0,!0)})),_.isReverted||_.revert(!0,!0),_._subPinOffset=!1;var Fe=ot(),Q=Zt(),we=v?v.duration():dn(R,A),Je=xe<=.01||!xe,ve=0,fe=B||0,oe=_i(U)?U.end:n.end,jt=n.endTrigger||d,pe=_i(U)?U.start:n.start||(n.start===0||!d?0:u?"0 0":"0 100%"),At=_.pinnedContainer=n.pinnedContainer&&dt(n.pinnedContainer,_),sn=d&&Math.max(0,X.indexOf(_))||0,Ve=sn,He,$e,ri,Zr,Ke,De,on,ro,nl,or,an,ar,Jr;for(J&&_i(U)&&(ar=D.getProperty(b,A.p),Jr=D.getProperty(Et,A.p));Ve-- >0;)De=X[Ve],De.end||De.refresh(0,1)||(tt=_),on=De.pin,on&&(on===d||on===u||on===At)&&!De.isReverted&&(or||(or=[]),or.unshift(De),De.revert(!0,!0)),De!==X[Ve]&&(sn--,Ve--);for(it(pe)&&(pe=pe(_)),pe=Mc(pe,"start",_),te=Pc(pe,d,Fe,A,Ae(),vt,b,_,Q,ie,G,we,v,_._startClamp&&"_startClamp")||(u?-.001:0),it(oe)&&(oe=oe(_)),Lt(oe)&&!oe.indexOf("+=")&&(~oe.indexOf(" ")?oe=(Lt(pe)?pe.split(" ")[0]:"")+oe:(ve=Cs(oe.substr(2),Fe),oe=Lt(pe)?pe:(v?D.utils.mapRange(0,v.duration(),v.scrollTrigger.start,v.scrollTrigger.end,te):te)+ve,jt=d)),oe=Mc(oe,"end",_),be=Math.max(te,Pc(oe||(jt?"100% 0":we),jt,Fe,A,Ae()+ve,wt,Et,_,Q,ie,G,we,v,_._endClamp&&"_endClamp"))||-.001,ve=0,Ve=sn;Ve--;)De=X[Ve],on=De.pin,on&&De.start-De._pinPush<=te&&!v&&De.end>0&&(He=De.end-(_._startClamp?Math.max(0,De.start):De.start),(on===d&&De.start-De._pinPush<te||on===At)&&isNaN(pe)&&(ve+=He*(1-De.progress)),on===u&&(fe+=He));if(te+=ve,be+=ve,_._startClamp&&(_._startClamp+=ve),_._endClamp&&!gt&&(_._endClamp=be||-.001,be=Math.min(be,dn(R,A))),xe=be-te||(te-=.01)&&.001,Je&&(Ye=D.utils.clamp(0,1,D.utils.normalize(te,be,Qt))),_._pinPush=fe,vt&&ve&&(He={},He[A.a]="+="+ve,At&&(He[A.p]="-="+Ae()),D.set([vt,wt],He)),u&&!(da&&_.end>=dn(R,A)))He=Gt(u),Zr=A===Pe,ri=Ae(),Ot=parseFloat(Te(A.a))+fe,!we&&be>1&&(an=(z?ce.scrollingElement||Ut:R).style,an={style:an,value:an["overflow"+A.a.toUpperCase()]},z&&Gt(se)["overflow"+A.a.toUpperCase()]!=="scroll"&&(an.style["overflow"+A.a.toUpperCase()]="scroll")),la(u,Ne,He),ei=ws(u),$e=An(u,!0),ro=G&&wn(R,Zr?et:Pe)(),m?(Ce=[m+A.os2,xe+fe+Ue],Ce.t=Ne,Ve=m===Ie?Ns(u,A)+xe+fe:0,Ve&&(Ce.push(A.d,Ve+Ue),Ne.style.flexBasis!=="auto"&&(Ne.style.flexBasis=Ve+Ue)),Zi(Ce),At&&X.forEach(function(On){On.pin===At&&On.vars.pinSpacing!==!1&&(On._subPinOffset=!0)}),G&&Ae(Qt)):(Ve=Ns(u,A),Ve&&Ne.style.flexBasis!=="auto"&&(Ne.style.flexBasis=Ve+Ue)),G&&(Ke={top:$e.top+(Zr?ri-te:ro)+Ue,left:$e.left+(Zr?ro:ri-te)+Ue,boxSizing:"border-box",position:"fixed"},Ke[vi]=Ke["max"+Ji]=Math.ceil($e.width)+Ue,Ke[yi]=Ke["max"+Sa]=Math.ceil($e.height)+Ue,Ke[Ht]=Ke[Ht+Ur]=Ke[Ht+Dr]=Ke[Ht+Nr]=Ke[Ht+Lr]="0",Ke[Ie]=He[Ie],Ke[Ie+Ur]=He[Ie+Ur],Ke[Ie+Dr]=He[Ie+Dr],Ke[Ie+Nr]=He[Ie+Nr],Ke[Ie+Lr]=He[Ie+Lr],Un=xf(mn,Ke,M),gt&&Ae(0)),i?(nl=i._initted,ia(1),i.render(i.duration(),!0,!0),gn=Te(A.a)-Ot+xe+fe,_n=Math.abs(xe-gn)>1,G&&_n&&Un.splice(Un.length-2,2),i.render(0,!0,!0),nl||i.invalidate(!0),i.parent||i.totalTime(i.totalTime()),ia(0)):gn=xe,an&&(an.value?an.style["overflow"+A.a.toUpperCase()]=an.value:an.style.removeProperty("overflow-"+A.a));else if(d&&Ae()&&!v)for($e=d.parentNode;$e&&$e!==se;)$e._pinOffset&&(te-=$e._pinOffset,be-=$e._pinOffset),$e=$e.parentNode;or&&or.forEach(function(On){return On.revert(!1,!0)}),_.start=te,_.end=be,lt=xt=gt?Qt:Ae(),!v&&!gt&&(lt<Qt&&Ae(Qt),_.scroll.rec=0),_.revert(!1,!0),ke=nt(),Kt&&(_e=-1,Kt.restart(!0)),tt=0,i&&F&&(i._initted||ii)&&i.progress()!==ii&&i.progress(ii||0,!0).render(i.time(),!0,!0),(Je||Ye!==_.progress||v||p||i&&!i._initted)&&(i&&!F&&(i._initted||Ye||i.vars.immediateRender!==!1)&&i.totalProgress(v&&te<-.001&&!Ye?D.utils.normalize(te,be,0):Ye,!0),_.progress=Je||(lt-te)/xe===Ye?0:Ye),u&&m&&(Ne._pinOffset=Math.round(_.progress*gn)),$&&$.invalidate(),isNaN(ar)||(ar-=D.getProperty(b,A.p),Jr-=D.getProperty(Et,A.p),Es(b,A,ar),Es(vt,A,ar-(B||0)),Es(Et,A,Jr),Es(wt,A,Jr-(B||0))),Je&&!gt&&_.update(),h&&!gt&&!Ln&&(Ln=!0,h(_),Ln=!1)}},_.getVelocity=function(){return(Ae()-xt)/(nt()-Er)*1e3||0},_.endAnimation=function(){wr(_.callbackAnimation),i&&($?$.progress(1):i.paused()?F||wr(i,_.direction<0,1):wr(i,i.reversed()))},_.labelToScroll=function(P){return i&&i.labels&&(te||_.refresh()||te)+i.labels[P]/i.duration()*xe||0},_.getTrailing=function(P){var O=X.indexOf(_),U=_.direction>0?X.slice(0,O).reverse():X.slice(O+1);return(Lt(P)?U.filter(function(B){return B.vars.preventOverlaps===P}):U).filter(function(B){return _.direction>0?B.end<=te:B.start>=be})},_.update=function(P,O,U){if(!(v&&!U&&!P)){var B=gt===!0?Qt:_.scroll(),Fe=P?0:(B-te)/xe,Q=Fe<0?0:Fe>1?1:Fe||0,we=_.progress,Je,ve,fe,oe,jt,pe,At,sn;if(O&&(xt=lt,lt=v?Ae():B,y&&(ti=Fn,Fn=i&&!F?i.totalProgress():Q)),g&&u&&!tt&&!vs&&Wt&&(!Q&&te<B+(B-xt)/(nt()-Er)*g?Q=1e-4:Q===1&&be>B+(B-xt)/(nt()-Er)*g&&(Q=.9999)),Q!==we&&_.enabled){if(Je=_.isActive=!!Q&&Q<1,ve=!!we&&we<1,pe=Je!==ve,jt=pe||!!Q!=!!we,_.direction=Q>we?1:-1,_.progress=Q,jt&&!tt&&(fe=Q&&!we?0:Q===1?1:we===1?2:3,F&&(oe=!pe&&L[fe+1]!=="none"&&L[fe+1]||L[fe],sn=i&&(oe==="complete"||oe==="reset"||oe in i))),E&&(pe||sn)&&(sn||f||!i)&&(it(E)?E(_):_.getTrailing(E).forEach(function(ri){return ri.endAnimation()})),F||($&&!tt&&!vs?($._dp._time-$._start!==$._time&&$.render($._dp._time-$._start),$.resetTo?$.resetTo("totalProgress",Q,i._tTime/i._tDur):($.vars.totalProgress=Q,$.invalidate().restart())):i&&i.totalProgress(Q,!!(tt&&(ke||P)))),u){if(P&&m&&(Ne.style[m+A.os2]=rr),!G)Jt(Cr(Ot+gn*Q));else if(jt){if(At=!P&&Q>we&&be+1>B&&B+1>=dn(R,A),M)if(!P&&(Je||At)){var Ve=An(u,!0),He=B-te;Ic(u,se,Ve.top+(A===Pe?He:0)+Ue,Ve.left+(A===Pe?0:He)+Ue)}else Ic(u,Ne);Zi(Je||At?Un:ei),_n&&Q<1&&Je||Jt(Ot+(Q===1&&!At?gn:0))}}y&&!at.tween&&!tt&&!vs&&Kt.restart(!0),o&&(pe||T&&Q&&(Q<1||!ra))&&Or(o.targets).forEach(function(ri){return ri.classList[Je||T?"add":"remove"](o.className)}),s&&!F&&!P&&s(_),jt&&!tt?(F&&(sn&&(oe==="complete"?i.pause().totalProgress(1):oe==="reset"?i.restart(!0).pause():oe==="restart"?i.restart(!0):i[oe]()),s&&s(_)),(pe||!ra)&&(l&&pe&&oa(_,l),H[fe]&&oa(_,H[fe]),T&&(Q===1?_.kill(!1,1):H[fe]=0),pe||(fe=Q===1?1:3,H[fe]&&oa(_,H[fe]))),C&&!Je&&Math.abs(_.getVelocity())>(Rr(C)?C:2500)&&(wr(_.callbackAnimation),$?$.progress(1):wr(i,oe==="reverse"?1:!Q,1))):F&&s&&!tt&&s(_)}if(Ai){var $e=v?B/v.duration()*(v._caScrollDist||0):B;Yr($e+(b._isFlipped?1:0)),Ai($e)}Ri&&Ri(-B/v.duration()*(v._caScrollDist||0))}},_.enable=function(P,O){_.enabled||(_.enabled=!0,Be(R,"resize",Pr),z||Be(R,"scroll",Wi),re&&Be(c,"refreshInit",re),P!==!1&&(_.progress=Ye=0,lt=xt=_e=Ae()),O!==!1&&_.refresh())},_.getTween=function(P){return P&&at?at.tween:$},_.setPositions=function(P,O,U,B){if(v){var Fe=v.scrollTrigger,Q=v.duration(),we=Fe.end-Fe.start;P=Fe.start+we*P/Q,O=Fe.start+we*O/Q}_.refresh(!1,!1,{start:Sc(P,U&&!!_._startClamp),end:Sc(O,U&&!!_._endClamp)},B),_.update()},_.adjustPinSpacing=function(P){if(Ce&&P){var O=Ce.indexOf(A.d)+1;Ce[O]=parseFloat(Ce[O])+P+Ue,Ce[1]=parseFloat(Ce[1])+P+Ue,Zi(Ce)}},_.disable=function(P,O){if(_.enabled&&(P!==!1&&_.revert(!0,!0),_.enabled=_.isActive=!1,O||$&&$.pause(),Qt=0,Ze&&(Ze.uncache=1),re&&Oe(c,"refreshInit",re),Kt&&(Kt.pause(),at.tween&&at.tween.kill()&&(at.tween=0)),!z)){for(var U=X.length;U--;)if(X[U].scroller===R&&X[U]!==_)return;Oe(R,"resize",Pr),z||Oe(R,"scroll",Wi)}},_.kill=function(P,O){_.disable(P,O),$&&!O&&$.kill(),a&&delete pa[a];var U=X.indexOf(_);U>=0&&X.splice(U,1),U===mt&&Ps>0&&mt--,U=0,X.forEach(function(B){return B.scroller===_.scroller&&(U=1)}),U||gt||(_.scroll.rec=0),i&&(i.scrollTrigger=null,P&&i.revert({kill:!1}),O||i.kill()),vt&&[vt,wt,b,Et].forEach(function(B){return B.parentNode&&B.parentNode.removeChild(B)}),Fr===_&&(Fr=0),u&&(Ze&&(Ze.uncache=1),U=0,X.forEach(function(B){return B.pin===u&&U++}),U||(Ze.spacer=0)),n.onKill&&n.onKill(_)},X.push(_),_.enable(!1,!1),xn&&xn(_),i&&i.add&&!xe){var ne=_.update;_.update=function(){_.update=ne,W.cache++,te||be||_.refresh()},D.delayedCall(.01,_.update),xe=.01,te=be=0}else _.refresh();u&&mf()},c.register=function(n){return Xi||(D=n||Vc(),kc()&&window.document&&c.enable(),Xi=Ar),Xi},c.defaults=function(n){if(n)for(var i in n)bs[i]=n[i];return bs},c.disable=function(n,i){Ar=0,X.forEach(function(s){return s[i?"kill":"disable"](n)}),Oe(q,"wheel",Wi),Oe(ce,"scroll",Wi),clearInterval(xs),Oe(ce,"touchcancel",fn),Oe(se,"touchstart",fn),Ms(Oe,ce,"pointerdown,touchstart,mousedown",bc),Ms(Oe,ce,"pointerup,touchend,mouseup",Tc),Ls.kill(),ys(Oe);for(var r=0;r<W.length;r+=3)Ss(Oe,W[r],W[r+1]),Ss(Oe,W[r],W[r+2])},c.enable=function(){if(q=window,ce=document,Ut=ce.documentElement,se=ce.body,D&&(Or=D.utils.toArray,Ir=D.utils.clamp,fa=D.core.context||fn,ia=D.core.suppressOverwrites||fn,xa=q.history.scrollRestoration||"auto",ma=q.pageYOffset||0,D.core.globals("ScrollTrigger",c),se)){Ar=1,Yi=document.createElement("div"),Yi.style.height="100vh",Yi.style.position="absolute",$c(),lf(),Se.register(D),c.isTouch=Se.isTouch,Zn=Se.isTouch&&/(iPad|iPhone|iPod|Mac)/g.test(navigator.userAgent),ua=Se.isTouch===1,Be(q,"wheel",Wi),_a=[q,ce,Ut,se],D.matchMedia?(c.matchMedia=function(l){var h=D.matchMedia(),f;for(f in l)h.add(f,l[f]);return h},D.addEventListener("matchMediaInit",function(){return Ta()}),D.addEventListener("matchMediaRevert",function(){return Zc()}),D.addEventListener("matchMedia",function(){xi(0,1),Ti("matchMedia")}),D.matchMedia().add("(orientation: portrait)",function(){return aa(),aa})):console.warn("Requires GSAP 3.11.0 or later"),aa(),Be(ce,"scroll",Wi);var n=se.hasAttribute("style"),i=se.style,r=i.borderTopStyle,s=D.core.Animation.prototype,o,a;for(s.revert||Object.defineProperty(s,"revert",{value:function(){return this.time(-.01,!0)}}),i.borderTopStyle="solid",o=An(se),Pe.m=Math.round(o.top+Pe.sc())||0,et.m=Math.round(o.left+et.sc())||0,r?i.borderTopStyle=r:i.removeProperty("border-top-style"),n||(se.setAttribute("style",""),se.removeAttribute("style")),xs=setInterval(Ac,250),D.delayedCall(.5,function(){return vs=0}),Be(ce,"touchcancel",fn),Be(se,"touchstart",fn),Ms(Be,ce,"pointerdown,touchstart,mousedown",bc),Ms(Be,ce,"pointerup,touchend,mouseup",Tc),ha=D.utils.checkPrefix("transform"),Is.push(ha),Xi=nt(),Ls=D.delayedCall(.2,xi).pause(),qi=[ce,"visibilitychange",function(){var l=q.innerWidth,h=q.innerHeight;ce.hidden?(vc=l,yc=h):(vc!==l||yc!==h)&&Pr()},ce,"DOMContentLoaded",xi,q,"load",xi,q,"resize",Pr],ys(Be),X.forEach(function(l){return l.enable(0,1)}),a=0;a<W.length;a+=3)Ss(Oe,W[a],W[a+1]),Ss(Oe,W[a],W[a+2])}},c.config=function(n){"limitCallbacks"in n&&(ra=!!n.limitCallbacks);var i=n.syncInterval;i&&clearInterval(xs)||(xs=i)&&setInterval(Ac,i),"ignoreMobileResize"in n&&(ua=c.isTouch===1&&n.ignoreMobileResize),"autoRefreshEvents"in n&&(ys(Oe)||ys(Be,n.autoRefreshEvents||"none"),Oc=(n.autoRefreshEvents+"").indexOf("resize")===-1)},c.scrollerProxy=function(n,i){var r=dt(n),s=W.indexOf(r),o=Si(r);~s&&W.splice(s,o?6:2),i&&(o?tn.unshift(q,i,se,i,Ut,i):tn.unshift(r,i))},c.clearMatchMedia=function(n){X.forEach(function(i){return i._ctx&&i._ctx.query===n&&i._ctx.kill(!0,!0)})},c.isInViewport=function(n,i,r){var s=(Lt(n)?dt(n):n).getBoundingClientRect(),o=s[r?vi:yi]*i||0;return r?s.right-o>0&&s.left+o<q.innerWidth:s.bottom-o>0&&s.top+o<q.innerHeight},c.positionInViewport=function(n,i,r){Lt(n)&&(n=dt(n));var s=n.getBoundingClientRect(),o=s[r?vi:yi],a=i==null?o/2:i in Fs?Fs[i]*o:~i.indexOf("%")?parseFloat(i)*o/100:parseFloat(i)||0;return r?(s.left+a)/q.innerWidth:(s.top+a)/q.innerHeight},c.killAll=function(n){if(X.slice(0).forEach(function(r){return r.vars.id!=="ScrollSmoother"&&r.kill()}),n!==!0){var i=bi.killAll||[];bi={},i.forEach(function(r){return r()})}},c})();Y.version="3.13.0";Y.saveStyles=function(c){return c?Or(c).forEach(function(e){if(e&&e.style){var t=Dt.indexOf(e);t>=0&&Dt.splice(t,5),Dt.push(e,e.style.cssText,e.getBBox&&e.getAttribute("transform"),D.core.getCache(e),fa())}}):Dt};Y.revert=function(c,e){return Ta(!c,e)};Y.create=function(c,e){return new Y(c,e)};Y.refresh=function(c){return c?Pr(!0):(Xi||Y.register())&&xi(!0)};Y.update=function(c){return++W.cache&&Cn(c===!0?2:0)};Y.clearScrollMemory=Jc;Y.maxScroll=function(c,e){return dn(c,e?et:Pe)};Y.getScrollFunc=function(c,e){return wn(dt(c),e?et:Pe)};Y.getById=function(c){return pa[c]};Y.getAll=function(){return X.filter(function(c){return c.vars.id!=="ScrollSmoother"})};Y.isScrolling=function(){return!!Wt};Y.snapDirectional=ba;Y.addEventListener=function(c,e){var t=bi[c]||(bi[c]=[]);~t.indexOf(e)||t.push(e)};Y.removeEventListener=function(c,e){var t=bi[c],n=t&&t.indexOf(e);n>=0&&t.splice(n,1)};Y.batch=function(c,e){var t=[],n={},i=e.interval||.016,r=e.batchMax||1e9,s=function(l,h){var f=[],d=[],u=D.delayedCall(i,function(){h(f,d),f=[],d=[]}).pause();return function(m){f.length||u.restart(!0),f.push(m.trigger),d.push(m),r<=f.length&&u.progress(1)}},o;for(o in e)n[o]=o.substr(0,2)==="on"&&it(e[o])&&o!=="onRefreshInit"?s(o,e[o]):e[o];return it(r)&&(r=r(),Be(Y,"refresh",function(){return r=e.batchMax()})),Or(c).forEach(function(a){var l={};for(o in n)l[o]=n[o];l.trigger=a,t.push(Y.create(l))}),t};var Lc=function(e,t,n,i){return t>i?e(i):t<0&&e(0),n>i?(i-t)/(n-t):n<0?t/(t-n):1},ca=function c(e,t){t===!0?e.style.removeProperty("touch-action"):e.style.touchAction=t===!0?"auto":t?"pan-"+t+(Se.isTouch?" pinch-zoom":""):"none",e===Ut&&c(se,t)},As={auto:1,scroll:1},yf=function(e){var t=e.event,n=e.target,i=e.axis,r=(t.changedTouches?t.changedTouches[0]:t).target,s=r._gsap||D.core.getCache(r),o=nt(),a;if(!s._isScrollT||o-s._isScrollT>2e3){for(;r&&r!==se&&(r.scrollHeight<=r.clientHeight&&r.scrollWidth<=r.clientWidth||!(As[(a=Gt(r)).overflowY]||As[a.overflowX]));)r=r.parentNode;s._isScroll=r&&r!==n&&!Si(r)&&(As[(a=Gt(r)).overflowY]||As[a.overflowX]),s._isScrollT=o}(s._isScroll||i==="x")&&(t.stopPropagation(),t._gsapAllow=!0)},Qc=function(e,t,n,i){return Se.create({target:e,capture:!0,debounce:!1,lockAxis:!0,type:t,onWheel:i=i&&yf,onPress:i,onDrag:i,onScroll:i,onEnable:function(){return n&&Be(ce,Se.eventTypes[0],Nc,!1,!0)},onDisable:function(){return Oe(ce,Se.eventTypes[0],Nc,!0)}})},Mf=/(input|label|select|textarea)/i,Uc,Nc=function(e){var t=Mf.test(e.target.tagName);(t||Uc)&&(e._gsapAllow=!0,Uc=t)},Sf=function(e){_i(e)||(e={}),e.preventDefault=e.isNormalizer=e.allowClicks=!0,e.type||(e.type="wheel,touch"),e.debounce=!!e.debounce,e.id=e.id||"normalizer";var t=e,n=t.normalizeScrollX,i=t.momentum,r=t.allowNestedScroll,s=t.onRelease,o,a,l=dt(e.target)||Ut,h=D.core.globals().ScrollSmoother,f=h&&h.get(),d=Zn&&(e.content&&dt(e.content)||f&&e.content!==!1&&!f.smooth()&&f.content()),u=wn(l,Pe),m=wn(l,et),p=1,g=(Se.isTouch&&q.visualViewport?q.visualViewport.scale*q.visualViewport.width:q.outerWidth)/q.innerWidth,x=0,S=it(i)?function(){return i(o)}:function(){return i||2.8},T,y,M=Qc(l,e.type,!0,r),w=function(){return y=!1},v=fn,C=fn,E=function(){a=dn(l,Pe),C=Ir(Zn?1:0,a),n&&(v=Ir(0,dn(l,et))),T=Mi},A=function(){d._gsap.y=Cr(parseFloat(d._gsap.y)+u.offset)+"px",d.style.transform="matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, "+parseFloat(d._gsap.y)+", 0, 1)",u.offset=u.cacheID=0},F=function(){if(y){requestAnimationFrame(w);var J=Cr(o.deltaY/2),ie=C(u.v-J);if(d&&ie!==u.v+u.offset){u.offset=ie-u.v;var _=Cr((parseFloat(d&&d._gsap.y)||0)-u.offset);d.style.transform="matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, "+_+", 0, 1)",d._gsap.y=_+"px",u.cacheID=W.cache,Cn()}return!0}u.offset&&A(),y=!0},R,V,z,G,H=function(){E(),R.isActive()&&R.vars.scrollY>a&&(u()>a?R.progress(1)&&u(a):R.resetTo("scrollY",a))};return d&&D.set(d,{y:"+=0"}),e.ignoreCheck=function(L){return Zn&&L.type==="touchmove"&&F(L)||p>1.05&&L.type!=="touchstart"||o.isGesturing||L.touches&&L.touches.length>1},e.onPress=function(){y=!1;var L=p;p=Cr((q.visualViewport&&q.visualViewport.scale||1)/g),R.pause(),L!==p&&ca(l,p>1.01?!0:n?!1:"x"),V=m(),z=u(),E(),T=Mi},e.onRelease=e.onGestureStart=function(L,J){if(u.offset&&A(),!J)G.restart(!0);else{W.cache++;var ie=S(),_,re;n&&(_=m(),re=_+ie*.05*-L.velocityX/.227,ie*=Lc(m,_,re,dn(l,et)),R.vars.scrollX=v(re)),_=u(),re=_+ie*.05*-L.velocityY/.227,ie*=Lc(u,_,re,dn(l,Pe)),R.vars.scrollY=C(re),R.invalidate().duration(ie).play(.01),(Zn&&R.vars.scrollY>=a||_>=a-1)&&D.to({},{onUpdate:H,duration:ie})}s&&s(L)},e.onWheel=function(){R._ts&&R.pause(),nt()-x>1e3&&(T=0,x=nt())},e.onChange=function(L,J,ie,_,re){if(Mi!==T&&E(),J&&n&&m(v(_[2]===J?V+(L.startX-L.x):m()+J-_[1])),ie){u.offset&&A();var ot=re[2]===ie,Zt=ot?z+L.startY-L.y:u()+ie-re[1],_e=C(Zt);ot&&Zt!==_e&&(z+=_e-Zt),u(_e)}(ie||J)&&Cn()},e.onEnable=function(){ca(l,n?!1:"x"),Y.addEventListener("refresh",H),Be(q,"resize",H),u.smooth&&(u.target.style.scrollBehavior="auto",u.smooth=m.smooth=!1),M.enable()},e.onDisable=function(){ca(l,!0),Oe(q,"resize",H),Y.removeEventListener("refresh",H),M.kill()},e.lockAxis=e.lockAxis!==!1,o=new Se(e),o.iOS=Zn,Zn&&!u()&&u(1),Zn&&D.ticker.add(fn),G=o._dc,R=D.to(o,{ease:"power4",paused:!0,inherit:!1,scrollX:n?"+=0.1":"+=0",scrollY:"+=0.1",modifiers:{scrollY:Kc(u,u(),function(){return R.pause()})},onUpdate:Cn,onComplete:G.vars.onComplete}),o};Y.sort=function(c){if(it(c))return X.sort(c);var e=q.pageYOffset||0;return Y.getAll().forEach(function(t){return t._sortY=t.trigger?e+t.trigger.getBoundingClientRect().top:t.start+q.innerHeight}),X.sort(c||function(t,n){return(t.vars.refreshPriority||0)*-1e6+(t.vars.containerAnimation?1e6:t._sortY)-((n.vars.containerAnimation?1e6:n._sortY)+(n.vars.refreshPriority||0)*-1e6)})};Y.observe=function(c){return new Se(c)};Y.normalizeScroll=function(c){if(typeof c>"u")return pt;if(c===!0&&pt)return pt.enable();if(c===!1){pt&&pt.kill(),pt=c;return}var e=c instanceof Se?c:Sf(c);return pt&&pt.target===e.target&&pt.kill(),Si(e.target)&&(pt=e),e};Y.core={_getVelocityProp:_s,_inputObserver:Qc,_scrollers:W,_proxies:tn,bridge:{ss:function(){Wt||Ti("scrollStart"),Wt=nt()},ref:function(){return tt}}};Vc()&&D.registerPlugin(Y);var Ga=0,Wa=1,Xa=2,qa=3,Ya=4,Za=5,Ja=6,$a=7;var Ka=300;var Da=1e3,kr=1001,La=1002;var hh=1006;var uh=1008;var fh=1009;var dh=1023;var Vr=2300,Ws=2301,Hs=2302,Ua=2400,Na=2401,Fa=2402;var Qa="",qt="srgb",Oa="srgb-linear",Ba="linear",Gs="srgb";var er=2e3,za=2001;var Hr=class{addEventListener(e,t){this._listeners===void 0&&(this._listeners={});let n=this._listeners;n[e]===void 0&&(n[e]=[]),n[e].indexOf(t)===-1&&n[e].push(t)}hasEventListener(e,t){let n=this._listeners;return n===void 0?!1:n[e]!==void 0&&n[e].indexOf(t)!==-1}removeEventListener(e,t){let n=this._listeners;if(n===void 0)return;let i=n[e];if(i!==void 0){let r=i.indexOf(t);r!==-1&&i.splice(r,1)}}dispatchEvent(e){let t=this._listeners;if(t===void 0)return;let n=t[e.type];if(n!==void 0){e.target=this;let i=n.slice(0);for(let r=0,s=i.length;r<s;r++)i[r].call(this,e);e.target=null}}},rt=["00","01","02","03","04","05","06","07","08","09","0a","0b","0c","0d","0e","0f","10","11","12","13","14","15","16","17","18","19","1a","1b","1c","1d","1e","1f","20","21","22","23","24","25","26","27","28","29","2a","2b","2c","2d","2e","2f","30","31","32","33","34","35","36","37","38","39","3a","3b","3c","3d","3e","3f","40","41","42","43","44","45","46","47","48","49","4a","4b","4c","4d","4e","4f","50","51","52","53","54","55","56","57","58","59","5a","5b","5c","5d","5e","5f","60","61","62","63","64","65","66","67","68","69","6a","6b","6c","6d","6e","6f","70","71","72","73","74","75","76","77","78","79","7a","7b","7c","7d","7e","7f","80","81","82","83","84","85","86","87","88","89","8a","8b","8c","8d","8e","8f","90","91","92","93","94","95","96","97","98","99","9a","9b","9c","9d","9e","9f","a0","a1","a2","a3","a4","a5","a6","a7","a8","a9","aa","ab","ac","ad","ae","af","b0","b1","b2","b3","b4","b5","b6","b7","b8","b9","ba","bb","bc","bd","be","bf","c0","c1","c2","c3","c4","c5","c6","c7","c8","c9","ca","cb","cc","cd","ce","cf","d0","d1","d2","d3","d4","d5","d6","d7","d8","d9","da","db","dc","dd","de","df","e0","e1","e2","e3","e4","e5","e6","e7","e8","e9","ea","eb","ec","ed","ee","ef","f0","f1","f2","f3","f4","f5","f6","f7","f8","f9","fa","fb","fc","fd","fe","ff"];var Fm=Math.PI/180,bf=180/Math.PI;function ja(){let c=Math.random()*4294967295|0,e=Math.random()*4294967295|0,t=Math.random()*4294967295|0,n=Math.random()*4294967295|0;return(rt[c&255]+rt[c>>8&255]+rt[c>>16&255]+rt[c>>24&255]+"-"+rt[e&255]+rt[e>>8&255]+"-"+rt[e>>16&15|64]+rt[e>>24&255]+"-"+rt[t&63|128]+rt[t>>8&255]+"-"+rt[t>>16&255]+rt[t>>24&255]+rt[n&255]+rt[n>>8&255]+rt[n>>16&255]+rt[n>>24&255]).toLowerCase()}function he(c,e,t){return Math.max(e,Math.min(t,c))}function Tf(c,e){return(c%e+e)%e}function wa(c,e,t){return(1-t)*c+t*e}var rn=class c{constructor(e=0,t=0){c.prototype.isVector2=!0,this.x=e,this.y=t}get width(){return this.x}set width(e){this.x=e}get height(){return this.y}set height(e){this.y=e}set(e,t){return this.x=e,this.y=t,this}setScalar(e){return this.x=e,this.y=e,this}setX(e){return this.x=e,this}setY(e){return this.y=e,this}setComponent(e,t){switch(e){case 0:this.x=t;break;case 1:this.y=t;break;default:throw new Error("index is out of range: "+e)}return this}getComponent(e){switch(e){case 0:return this.x;case 1:return this.y;default:throw new Error("index is out of range: "+e)}}clone(){return new this.constructor(this.x,this.y)}copy(e){return this.x=e.x,this.y=e.y,this}add(e){return this.x+=e.x,this.y+=e.y,this}addScalar(e){return this.x+=e,this.y+=e,this}addVectors(e,t){return this.x=e.x+t.x,this.y=e.y+t.y,this}addScaledVector(e,t){return this.x+=e.x*t,this.y+=e.y*t,this}sub(e){return this.x-=e.x,this.y-=e.y,this}subScalar(e){return this.x-=e,this.y-=e,this}subVectors(e,t){return this.x=e.x-t.x,this.y=e.y-t.y,this}multiply(e){return this.x*=e.x,this.y*=e.y,this}multiplyScalar(e){return this.x*=e,this.y*=e,this}divide(e){return this.x/=e.x,this.y/=e.y,this}divideScalar(e){return this.multiplyScalar(1/e)}applyMatrix3(e){let t=this.x,n=this.y,i=e.elements;return this.x=i[0]*t+i[3]*n+i[6],this.y=i[1]*t+i[4]*n+i[7],this}min(e){return this.x=Math.min(this.x,e.x),this.y=Math.min(this.y,e.y),this}max(e){return this.x=Math.max(this.x,e.x),this.y=Math.max(this.y,e.y),this}clamp(e,t){return this.x=he(this.x,e.x,t.x),this.y=he(this.y,e.y,t.y),this}clampScalar(e,t){return this.x=he(this.x,e,t),this.y=he(this.y,e,t),this}clampLength(e,t){let n=this.length();return this.divideScalar(n||1).multiplyScalar(he(n,e,t))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this}negate(){return this.x=-this.x,this.y=-this.y,this}dot(e){return this.x*e.x+this.y*e.y}cross(e){return this.x*e.y-this.y*e.x}lengthSq(){return this.x*this.x+this.y*this.y}length(){return Math.sqrt(this.x*this.x+this.y*this.y)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)}normalize(){return this.divideScalar(this.length()||1)}angle(){return Math.atan2(-this.y,-this.x)+Math.PI}angleTo(e){let t=Math.sqrt(this.lengthSq()*e.lengthSq());if(t===0)return Math.PI/2;let n=this.dot(e)/t;return Math.acos(he(n,-1,1))}distanceTo(e){return Math.sqrt(this.distanceToSquared(e))}distanceToSquared(e){let t=this.x-e.x,n=this.y-e.y;return t*t+n*n}manhattanDistanceTo(e){return Math.abs(this.x-e.x)+Math.abs(this.y-e.y)}setLength(e){return this.normalize().multiplyScalar(e)}lerp(e,t){return this.x+=(e.x-this.x)*t,this.y+=(e.y-this.y)*t,this}lerpVectors(e,t,n){return this.x=e.x+(t.x-e.x)*n,this.y=e.y+(t.y-e.y)*n,this}equals(e){return e.x===this.x&&e.y===this.y}fromArray(e,t=0){return this.x=e[t],this.y=e[t+1],this}toArray(e=[],t=0){return e[t]=this.x,e[t+1]=this.y,e}fromBufferAttribute(e,t){return this.x=e.getX(t),this.y=e.getY(t),this}rotateAround(e,t){let n=Math.cos(t),i=Math.sin(t),r=this.x-e.x,s=this.y-e.y;return this.x=r*n-s*i+e.x,this.y=r*i+s*n+e.y,this}random(){return this.x=Math.random(),this.y=Math.random(),this}*[Symbol.iterator](){yield this.x,yield this.y}},In=class{constructor(e=0,t=0,n=0,i=1){this.isQuaternion=!0,this._x=e,this._y=t,this._z=n,this._w=i}static slerpFlat(e,t,n,i,r,s,o){let a=n[i+0],l=n[i+1],h=n[i+2],f=n[i+3],d=r[s+0],u=r[s+1],m=r[s+2],p=r[s+3];if(o===0){e[t+0]=a,e[t+1]=l,e[t+2]=h,e[t+3]=f;return}if(o===1){e[t+0]=d,e[t+1]=u,e[t+2]=m,e[t+3]=p;return}if(f!==p||a!==d||l!==u||h!==m){let g=1-o,x=a*d+l*u+h*m+f*p,S=x>=0?1:-1,T=1-x*x;if(T>Number.EPSILON){let M=Math.sqrt(T),w=Math.atan2(M,x*S);g=Math.sin(g*w)/M,o=Math.sin(o*w)/M}let y=o*S;if(a=a*g+d*y,l=l*g+u*y,h=h*g+m*y,f=f*g+p*y,g===1-o){let M=1/Math.sqrt(a*a+l*l+h*h+f*f);a*=M,l*=M,h*=M,f*=M}}e[t]=a,e[t+1]=l,e[t+2]=h,e[t+3]=f}static multiplyQuaternionsFlat(e,t,n,i,r,s){let o=n[i],a=n[i+1],l=n[i+2],h=n[i+3],f=r[s],d=r[s+1],u=r[s+2],m=r[s+3];return e[t]=o*m+h*f+a*u-l*d,e[t+1]=a*m+h*d+l*f-o*u,e[t+2]=l*m+h*u+o*d-a*f,e[t+3]=h*m-o*f-a*d-l*u,e}get x(){return this._x}set x(e){this._x=e,this._onChangeCallback()}get y(){return this._y}set y(e){this._y=e,this._onChangeCallback()}get z(){return this._z}set z(e){this._z=e,this._onChangeCallback()}get w(){return this._w}set w(e){this._w=e,this._onChangeCallback()}set(e,t,n,i){return this._x=e,this._y=t,this._z=n,this._w=i,this._onChangeCallback(),this}clone(){return new this.constructor(this._x,this._y,this._z,this._w)}copy(e){return this._x=e.x,this._y=e.y,this._z=e.z,this._w=e.w,this._onChangeCallback(),this}setFromEuler(e,t=!0){let n=e._x,i=e._y,r=e._z,s=e._order,o=Math.cos,a=Math.sin,l=o(n/2),h=o(i/2),f=o(r/2),d=a(n/2),u=a(i/2),m=a(r/2);switch(s){case"XYZ":this._x=d*h*f+l*u*m,this._y=l*u*f-d*h*m,this._z=l*h*m+d*u*f,this._w=l*h*f-d*u*m;break;case"YXZ":this._x=d*h*f+l*u*m,this._y=l*u*f-d*h*m,this._z=l*h*m-d*u*f,this._w=l*h*f+d*u*m;break;case"ZXY":this._x=d*h*f-l*u*m,this._y=l*u*f+d*h*m,this._z=l*h*m+d*u*f,this._w=l*h*f-d*u*m;break;case"ZYX":this._x=d*h*f-l*u*m,this._y=l*u*f+d*h*m,this._z=l*h*m-d*u*f,this._w=l*h*f+d*u*m;break;case"YZX":this._x=d*h*f+l*u*m,this._y=l*u*f+d*h*m,this._z=l*h*m-d*u*f,this._w=l*h*f-d*u*m;break;case"XZY":this._x=d*h*f-l*u*m,this._y=l*u*f-d*h*m,this._z=l*h*m+d*u*f,this._w=l*h*f+d*u*m;break;default:console.warn("THREE.Quaternion: .setFromEuler() encountered an unknown order: "+s)}return t===!0&&this._onChangeCallback(),this}setFromAxisAngle(e,t){let n=t/2,i=Math.sin(n);return this._x=e.x*i,this._y=e.y*i,this._z=e.z*i,this._w=Math.cos(n),this._onChangeCallback(),this}setFromRotationMatrix(e){let t=e.elements,n=t[0],i=t[4],r=t[8],s=t[1],o=t[5],a=t[9],l=t[2],h=t[6],f=t[10],d=n+o+f;if(d>0){let u=.5/Math.sqrt(d+1);this._w=.25/u,this._x=(h-a)*u,this._y=(r-l)*u,this._z=(s-i)*u}else if(n>o&&n>f){let u=2*Math.sqrt(1+n-o-f);this._w=(h-a)/u,this._x=.25*u,this._y=(i+s)/u,this._z=(r+l)/u}else if(o>f){let u=2*Math.sqrt(1+o-n-f);this._w=(r-l)/u,this._x=(i+s)/u,this._y=.25*u,this._z=(a+h)/u}else{let u=2*Math.sqrt(1+f-n-o);this._w=(s-i)/u,this._x=(r+l)/u,this._y=(a+h)/u,this._z=.25*u}return this._onChangeCallback(),this}setFromUnitVectors(e,t){let n=e.dot(t)+1;return n<1e-8?(n=0,Math.abs(e.x)>Math.abs(e.z)?(this._x=-e.y,this._y=e.x,this._z=0,this._w=n):(this._x=0,this._y=-e.z,this._z=e.y,this._w=n)):(this._x=e.y*t.z-e.z*t.y,this._y=e.z*t.x-e.x*t.z,this._z=e.x*t.y-e.y*t.x,this._w=n),this.normalize()}angleTo(e){return 2*Math.acos(Math.abs(he(this.dot(e),-1,1)))}rotateTowards(e,t){let n=this.angleTo(e);if(n===0)return this;let i=Math.min(1,t/n);return this.slerp(e,i),this}identity(){return this.set(0,0,0,1)}invert(){return this.conjugate()}conjugate(){return this._x*=-1,this._y*=-1,this._z*=-1,this._onChangeCallback(),this}dot(e){return this._x*e._x+this._y*e._y+this._z*e._z+this._w*e._w}lengthSq(){return this._x*this._x+this._y*this._y+this._z*this._z+this._w*this._w}length(){return Math.sqrt(this._x*this._x+this._y*this._y+this._z*this._z+this._w*this._w)}normalize(){let e=this.length();return e===0?(this._x=0,this._y=0,this._z=0,this._w=1):(e=1/e,this._x=this._x*e,this._y=this._y*e,this._z=this._z*e,this._w=this._w*e),this._onChangeCallback(),this}multiply(e){return this.multiplyQuaternions(this,e)}premultiply(e){return this.multiplyQuaternions(e,this)}multiplyQuaternions(e,t){let n=e._x,i=e._y,r=e._z,s=e._w,o=t._x,a=t._y,l=t._z,h=t._w;return this._x=n*h+s*o+i*l-r*a,this._y=i*h+s*a+r*o-n*l,this._z=r*h+s*l+n*a-i*o,this._w=s*h-n*o-i*a-r*l,this._onChangeCallback(),this}slerp(e,t){if(t===0)return this;if(t===1)return this.copy(e);let n=this._x,i=this._y,r=this._z,s=this._w,o=s*e._w+n*e._x+i*e._y+r*e._z;if(o<0?(this._w=-e._w,this._x=-e._x,this._y=-e._y,this._z=-e._z,o=-o):this.copy(e),o>=1)return this._w=s,this._x=n,this._y=i,this._z=r,this;let a=1-o*o;if(a<=Number.EPSILON){let u=1-t;return this._w=u*s+t*this._w,this._x=u*n+t*this._x,this._y=u*i+t*this._y,this._z=u*r+t*this._z,this.normalize(),this}let l=Math.sqrt(a),h=Math.atan2(l,o),f=Math.sin((1-t)*h)/l,d=Math.sin(t*h)/l;return this._w=s*f+this._w*d,this._x=n*f+this._x*d,this._y=i*f+this._y*d,this._z=r*f+this._z*d,this._onChangeCallback(),this}slerpQuaternions(e,t,n){return this.copy(e).slerp(t,n)}random(){let e=2*Math.PI*Math.random(),t=2*Math.PI*Math.random(),n=Math.random(),i=Math.sqrt(1-n),r=Math.sqrt(n);return this.set(i*Math.sin(e),i*Math.cos(e),r*Math.sin(t),r*Math.cos(t))}equals(e){return e._x===this._x&&e._y===this._y&&e._z===this._z&&e._w===this._w}fromArray(e,t=0){return this._x=e[t],this._y=e[t+1],this._z=e[t+2],this._w=e[t+3],this._onChangeCallback(),this}toArray(e=[],t=0){return e[t]=this._x,e[t+1]=this._y,e[t+2]=this._z,e[t+3]=this._w,e}fromBufferAttribute(e,t){return this._x=e.getX(t),this._y=e.getY(t),this._z=e.getZ(t),this._w=e.getW(t),this._onChangeCallback(),this}toJSON(){return this.toArray()}_onChange(e){return this._onChangeCallback=e,this}_onChangeCallback(){}*[Symbol.iterator](){yield this._x,yield this._y,yield this._z,yield this._w}},ee=class c{constructor(e=0,t=0,n=0){c.prototype.isVector3=!0,this.x=e,this.y=t,this.z=n}set(e,t,n){return n===void 0&&(n=this.z),this.x=e,this.y=t,this.z=n,this}setScalar(e){return this.x=e,this.y=e,this.z=e,this}setX(e){return this.x=e,this}setY(e){return this.y=e,this}setZ(e){return this.z=e,this}setComponent(e,t){switch(e){case 0:this.x=t;break;case 1:this.y=t;break;case 2:this.z=t;break;default:throw new Error("index is out of range: "+e)}return this}getComponent(e){switch(e){case 0:return this.x;case 1:return this.y;case 2:return this.z;default:throw new Error("index is out of range: "+e)}}clone(){return new this.constructor(this.x,this.y,this.z)}copy(e){return this.x=e.x,this.y=e.y,this.z=e.z,this}add(e){return this.x+=e.x,this.y+=e.y,this.z+=e.z,this}addScalar(e){return this.x+=e,this.y+=e,this.z+=e,this}addVectors(e,t){return this.x=e.x+t.x,this.y=e.y+t.y,this.z=e.z+t.z,this}addScaledVector(e,t){return this.x+=e.x*t,this.y+=e.y*t,this.z+=e.z*t,this}sub(e){return this.x-=e.x,this.y-=e.y,this.z-=e.z,this}subScalar(e){return this.x-=e,this.y-=e,this.z-=e,this}subVectors(e,t){return this.x=e.x-t.x,this.y=e.y-t.y,this.z=e.z-t.z,this}multiply(e){return this.x*=e.x,this.y*=e.y,this.z*=e.z,this}multiplyScalar(e){return this.x*=e,this.y*=e,this.z*=e,this}multiplyVectors(e,t){return this.x=e.x*t.x,this.y=e.y*t.y,this.z=e.z*t.z,this}applyEuler(e){return this.applyQuaternion(jc.setFromEuler(e))}applyAxisAngle(e,t){return this.applyQuaternion(jc.setFromAxisAngle(e,t))}applyMatrix3(e){let t=this.x,n=this.y,i=this.z,r=e.elements;return this.x=r[0]*t+r[3]*n+r[6]*i,this.y=r[1]*t+r[4]*n+r[7]*i,this.z=r[2]*t+r[5]*n+r[8]*i,this}applyNormalMatrix(e){return this.applyMatrix3(e).normalize()}applyMatrix4(e){let t=this.x,n=this.y,i=this.z,r=e.elements,s=1/(r[3]*t+r[7]*n+r[11]*i+r[15]);return this.x=(r[0]*t+r[4]*n+r[8]*i+r[12])*s,this.y=(r[1]*t+r[5]*n+r[9]*i+r[13])*s,this.z=(r[2]*t+r[6]*n+r[10]*i+r[14])*s,this}applyQuaternion(e){let t=this.x,n=this.y,i=this.z,r=e.x,s=e.y,o=e.z,a=e.w,l=2*(s*i-o*n),h=2*(o*t-r*i),f=2*(r*n-s*t);return this.x=t+a*l+s*f-o*h,this.y=n+a*h+o*l-r*f,this.z=i+a*f+r*h-s*l,this}project(e){return this.applyMatrix4(e.matrixWorldInverse).applyMatrix4(e.projectionMatrix)}unproject(e){return this.applyMatrix4(e.projectionMatrixInverse).applyMatrix4(e.matrixWorld)}transformDirection(e){let t=this.x,n=this.y,i=this.z,r=e.elements;return this.x=r[0]*t+r[4]*n+r[8]*i,this.y=r[1]*t+r[5]*n+r[9]*i,this.z=r[2]*t+r[6]*n+r[10]*i,this.normalize()}divide(e){return this.x/=e.x,this.y/=e.y,this.z/=e.z,this}divideScalar(e){return this.multiplyScalar(1/e)}min(e){return this.x=Math.min(this.x,e.x),this.y=Math.min(this.y,e.y),this.z=Math.min(this.z,e.z),this}max(e){return this.x=Math.max(this.x,e.x),this.y=Math.max(this.y,e.y),this.z=Math.max(this.z,e.z),this}clamp(e,t){return this.x=he(this.x,e.x,t.x),this.y=he(this.y,e.y,t.y),this.z=he(this.z,e.z,t.z),this}clampScalar(e,t){return this.x=he(this.x,e,t),this.y=he(this.y,e,t),this.z=he(this.z,e,t),this}clampLength(e,t){let n=this.length();return this.divideScalar(n||1).multiplyScalar(he(n,e,t))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this.z=Math.floor(this.z),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this.z=Math.ceil(this.z),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this.z=Math.round(this.z),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this.z=Math.trunc(this.z),this}negate(){return this.x=-this.x,this.y=-this.y,this.z=-this.z,this}dot(e){return this.x*e.x+this.y*e.y+this.z*e.z}lengthSq(){return this.x*this.x+this.y*this.y+this.z*this.z}length(){return Math.sqrt(this.x*this.x+this.y*this.y+this.z*this.z)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)+Math.abs(this.z)}normalize(){return this.divideScalar(this.length()||1)}setLength(e){return this.normalize().multiplyScalar(e)}lerp(e,t){return this.x+=(e.x-this.x)*t,this.y+=(e.y-this.y)*t,this.z+=(e.z-this.z)*t,this}lerpVectors(e,t,n){return this.x=e.x+(t.x-e.x)*n,this.y=e.y+(t.y-e.y)*n,this.z=e.z+(t.z-e.z)*n,this}cross(e){return this.crossVectors(this,e)}crossVectors(e,t){let n=e.x,i=e.y,r=e.z,s=t.x,o=t.y,a=t.z;return this.x=i*a-r*o,this.y=r*s-n*a,this.z=n*o-i*s,this}projectOnVector(e){let t=e.lengthSq();if(t===0)return this.set(0,0,0);let n=e.dot(this)/t;return this.copy(e).multiplyScalar(n)}projectOnPlane(e){return Ea.copy(this).projectOnVector(e),this.sub(Ea)}reflect(e){return this.sub(Ea.copy(e).multiplyScalar(2*this.dot(e)))}angleTo(e){let t=Math.sqrt(this.lengthSq()*e.lengthSq());if(t===0)return Math.PI/2;let n=this.dot(e)/t;return Math.acos(he(n,-1,1))}distanceTo(e){return Math.sqrt(this.distanceToSquared(e))}distanceToSquared(e){let t=this.x-e.x,n=this.y-e.y,i=this.z-e.z;return t*t+n*n+i*i}manhattanDistanceTo(e){return Math.abs(this.x-e.x)+Math.abs(this.y-e.y)+Math.abs(this.z-e.z)}setFromSpherical(e){return this.setFromSphericalCoords(e.radius,e.phi,e.theta)}setFromSphericalCoords(e,t,n){let i=Math.sin(t)*e;return this.x=i*Math.sin(n),this.y=Math.cos(t)*e,this.z=i*Math.cos(n),this}setFromCylindrical(e){return this.setFromCylindricalCoords(e.radius,e.theta,e.y)}setFromCylindricalCoords(e,t,n){return this.x=e*Math.sin(t),this.y=n,this.z=e*Math.cos(t),this}setFromMatrixPosition(e){let t=e.elements;return this.x=t[12],this.y=t[13],this.z=t[14],this}setFromMatrixScale(e){let t=this.setFromMatrixColumn(e,0).length(),n=this.setFromMatrixColumn(e,1).length(),i=this.setFromMatrixColumn(e,2).length();return this.x=t,this.y=n,this.z=i,this}setFromMatrixColumn(e,t){return this.fromArray(e.elements,t*4)}setFromMatrix3Column(e,t){return this.fromArray(e.elements,t*3)}setFromEuler(e){return this.x=e._x,this.y=e._y,this.z=e._z,this}setFromColor(e){return this.x=e.r,this.y=e.g,this.z=e.b,this}equals(e){return e.x===this.x&&e.y===this.y&&e.z===this.z}fromArray(e,t=0){return this.x=e[t],this.y=e[t+1],this.z=e[t+2],this}toArray(e=[],t=0){return e[t]=this.x,e[t+1]=this.y,e[t+2]=this.z,e}fromBufferAttribute(e,t){return this.x=e.getX(t),this.y=e.getY(t),this.z=e.getZ(t),this}random(){return this.x=Math.random(),this.y=Math.random(),this.z=Math.random(),this}randomDirection(){let e=Math.random()*Math.PI*2,t=Math.random()*2-1,n=Math.sqrt(1-t*t);return this.x=n*Math.cos(e),this.y=t,this.z=n*Math.sin(e),this}*[Symbol.iterator](){yield this.x,yield this.y,yield this.z}},Ea=new ee,jc=new In,Z=class c{constructor(e,t,n,i,r,s,o,a,l){c.prototype.isMatrix3=!0,this.elements=[1,0,0,0,1,0,0,0,1],e!==void 0&&this.set(e,t,n,i,r,s,o,a,l)}set(e,t,n,i,r,s,o,a,l){let h=this.elements;return h[0]=e,h[1]=i,h[2]=o,h[3]=t,h[4]=r,h[5]=a,h[6]=n,h[7]=s,h[8]=l,this}identity(){return this.set(1,0,0,0,1,0,0,0,1),this}copy(e){let t=this.elements,n=e.elements;return t[0]=n[0],t[1]=n[1],t[2]=n[2],t[3]=n[3],t[4]=n[4],t[5]=n[5],t[6]=n[6],t[7]=n[7],t[8]=n[8],this}extractBasis(e,t,n){return e.setFromMatrix3Column(this,0),t.setFromMatrix3Column(this,1),n.setFromMatrix3Column(this,2),this}setFromMatrix4(e){let t=e.elements;return this.set(t[0],t[4],t[8],t[1],t[5],t[9],t[2],t[6],t[10]),this}multiply(e){return this.multiplyMatrices(this,e)}premultiply(e){return this.multiplyMatrices(e,this)}multiplyMatrices(e,t){let n=e.elements,i=t.elements,r=this.elements,s=n[0],o=n[3],a=n[6],l=n[1],h=n[4],f=n[7],d=n[2],u=n[5],m=n[8],p=i[0],g=i[3],x=i[6],S=i[1],T=i[4],y=i[7],M=i[2],w=i[5],v=i[8];return r[0]=s*p+o*S+a*M,r[3]=s*g+o*T+a*w,r[6]=s*x+o*y+a*v,r[1]=l*p+h*S+f*M,r[4]=l*g+h*T+f*w,r[7]=l*x+h*y+f*v,r[2]=d*p+u*S+m*M,r[5]=d*g+u*T+m*w,r[8]=d*x+u*y+m*v,this}multiplyScalar(e){let t=this.elements;return t[0]*=e,t[3]*=e,t[6]*=e,t[1]*=e,t[4]*=e,t[7]*=e,t[2]*=e,t[5]*=e,t[8]*=e,this}determinant(){let e=this.elements,t=e[0],n=e[1],i=e[2],r=e[3],s=e[4],o=e[5],a=e[6],l=e[7],h=e[8];return t*s*h-t*o*l-n*r*h+n*o*a+i*r*l-i*s*a}invert(){let e=this.elements,t=e[0],n=e[1],i=e[2],r=e[3],s=e[4],o=e[5],a=e[6],l=e[7],h=e[8],f=h*s-o*l,d=o*a-h*r,u=l*r-s*a,m=t*f+n*d+i*u;if(m===0)return this.set(0,0,0,0,0,0,0,0,0);let p=1/m;return e[0]=f*p,e[1]=(i*l-h*n)*p,e[2]=(o*n-i*s)*p,e[3]=d*p,e[4]=(h*t-i*a)*p,e[5]=(i*r-o*t)*p,e[6]=u*p,e[7]=(n*a-l*t)*p,e[8]=(s*t-n*r)*p,this}transpose(){let e,t=this.elements;return e=t[1],t[1]=t[3],t[3]=e,e=t[2],t[2]=t[6],t[6]=e,e=t[5],t[5]=t[7],t[7]=e,this}getNormalMatrix(e){return this.setFromMatrix4(e).invert().transpose()}transposeIntoArray(e){let t=this.elements;return e[0]=t[0],e[1]=t[3],e[2]=t[6],e[3]=t[1],e[4]=t[4],e[5]=t[7],e[6]=t[2],e[7]=t[5],e[8]=t[8],this}setUvTransform(e,t,n,i,r,s,o){let a=Math.cos(r),l=Math.sin(r);return this.set(n*a,n*l,-n*(a*s+l*o)+s+e,-i*l,i*a,-i*(-l*s+a*o)+o+t,0,0,1),this}scale(e,t){return this.premultiply(Aa.makeScale(e,t)),this}rotate(e){return this.premultiply(Aa.makeRotation(-e)),this}translate(e,t){return this.premultiply(Aa.makeTranslation(e,t)),this}makeTranslation(e,t){return e.isVector2?this.set(1,0,e.x,0,1,e.y,0,0,1):this.set(1,0,e,0,1,t,0,0,1),this}makeRotation(e){let t=Math.cos(e),n=Math.sin(e);return this.set(t,-n,0,n,t,0,0,0,1),this}makeScale(e,t){return this.set(e,0,0,0,t,0,0,0,1),this}equals(e){let t=this.elements,n=e.elements;for(let i=0;i<9;i++)if(t[i]!==n[i])return!1;return!0}fromArray(e,t=0){for(let n=0;n<9;n++)this.elements[n]=e[n+t];return this}toArray(e=[],t=0){let n=this.elements;return e[t]=n[0],e[t+1]=n[1],e[t+2]=n[2],e[t+3]=n[3],e[t+4]=n[4],e[t+5]=n[5],e[t+6]=n[6],e[t+7]=n[7],e[t+8]=n[8],e}clone(){return new this.constructor().fromArray(this.elements)}},Aa=new Z;function ka(c){return document.createElementNS("http://www.w3.org/1999/xhtml",c)}var eh={};function Va(c){c in eh||(eh[c]=!0,console.warn(c))}var th=new Z().set(.4123908,.3575843,.1804808,.212639,.7151687,.0721923,.0193308,.1191948,.9505322),nh=new Z().set(3.2409699,-1.5373832,-.4986108,-.9692436,1.8759675,.0415551,.0556301,-.203977,1.0569715);function wf(){let c={enabled:!0,workingColorSpace:Oa,spaces:{},convert:function(i,r,s){return this.enabled===!1||r===s||!r||!s||(this.spaces[r].transfer===Gs&&(i.r=Pn(i.r),i.g=Pn(i.g),i.b=Pn(i.b)),this.spaces[r].primaries!==this.spaces[s].primaries&&(i.applyMatrix3(this.spaces[r].toXYZ),i.applyMatrix3(this.spaces[s].fromXYZ)),this.spaces[s].transfer===Gs&&(i.r=tr(i.r),i.g=tr(i.g),i.b=tr(i.b))),i},workingToColorSpace:function(i,r){return this.convert(i,this.workingColorSpace,r)},colorSpaceToWorking:function(i,r){return this.convert(i,r,this.workingColorSpace)},getPrimaries:function(i){return this.spaces[i].primaries},getTransfer:function(i){return i===Qa?Ba:this.spaces[i].transfer},getToneMappingMode:function(i){return this.spaces[i].outputColorSpaceConfig.toneMappingMode||"standard"},getLuminanceCoefficients:function(i,r=this.workingColorSpace){return i.fromArray(this.spaces[r].luminanceCoefficients)},define:function(i){Object.assign(this.spaces,i)},_getMatrix:function(i,r,s){return i.copy(this.spaces[r].toXYZ).multiply(this.spaces[s].fromXYZ)},_getDrawingBufferColorSpace:function(i){return this.spaces[i].outputColorSpaceConfig.drawingBufferColorSpace},_getUnpackColorSpace:function(i=this.workingColorSpace){return this.spaces[i].workingColorSpaceConfig.unpackColorSpace},fromWorkingColorSpace:function(i,r){return Va("THREE.ColorManagement: .fromWorkingColorSpace() has been renamed to .workingToColorSpace()."),c.workingToColorSpace(i,r)},toWorkingColorSpace:function(i,r){return Va("THREE.ColorManagement: .toWorkingColorSpace() has been renamed to .colorSpaceToWorking()."),c.colorSpaceToWorking(i,r)}},e=[.64,.33,.3,.6,.15,.06],t=[.2126,.7152,.0722],n=[.3127,.329];return c.define({[Oa]:{primaries:e,whitePoint:n,transfer:Ba,toXYZ:th,fromXYZ:nh,luminanceCoefficients:t,workingColorSpaceConfig:{unpackColorSpace:qt},outputColorSpaceConfig:{drawingBufferColorSpace:qt}},[qt]:{primaries:e,whitePoint:n,transfer:Gs,toXYZ:th,fromXYZ:nh,luminanceCoefficients:t,outputColorSpaceConfig:{drawingBufferColorSpace:qt}}}),c}var Xt=wf();function Pn(c){return c<.04045?c*.0773993808:Math.pow(c*.9478672986+.0521327014,2.4)}function tr(c){return c<.0031308?c*12.92:1.055*Math.pow(c,.41666)-.055}var $i,Xs=class{static getDataURL(e,t="image/png"){if(/^data:/i.test(e.src)||typeof HTMLCanvasElement>"u")return e.src;let n;if(e instanceof HTMLCanvasElement)n=e;else{$i===void 0&&($i=ka("canvas")),$i.width=e.width,$i.height=e.height;let i=$i.getContext("2d");e instanceof ImageData?i.putImageData(e,0,0):i.drawImage(e,0,0,e.width,e.height),n=$i}return n.toDataURL(t)}static sRGBToLinear(e){if(typeof HTMLImageElement<"u"&&e instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&e instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&e instanceof ImageBitmap){let t=ka("canvas");t.width=e.width,t.height=e.height;let n=t.getContext("2d");n.drawImage(e,0,0,e.width,e.height);let i=n.getImageData(0,0,e.width,e.height),r=i.data;for(let s=0;s<r.length;s++)r[s]=Pn(r[s]/255)*255;return n.putImageData(i,0,0),t}else if(e.data){let t=e.data.slice(0);for(let n=0;n<t.length;n++)t instanceof Uint8Array||t instanceof Uint8ClampedArray?t[n]=Math.floor(Pn(t[n]/255)*255):t[n]=Pn(t[n]);return{data:t,width:e.width,height:e.height}}else return console.warn("THREE.ImageUtils.sRGBToLinear(): Unsupported image type. No color space conversion applied."),e}},Ef=0,qs=class{constructor(e=null){this.isSource=!0,Object.defineProperty(this,"id",{value:Ef++}),this.uuid=ja(),this.data=e,this.dataReady=!0,this.version=0}getSize(e){let t=this.data;return typeof HTMLVideoElement<"u"&&t instanceof HTMLVideoElement?e.set(t.videoWidth,t.videoHeight,0):t instanceof VideoFrame?e.set(t.displayHeight,t.displayWidth,0):t!==null?e.set(t.width,t.height,t.depth||0):e.set(0,0,0),e}set needsUpdate(e){e===!0&&this.version++}toJSON(e){let t=e===void 0||typeof e=="string";if(!t&&e.images[this.uuid]!==void 0)return e.images[this.uuid];let n={uuid:this.uuid,url:""},i=this.data;if(i!==null){let r;if(Array.isArray(i)){r=[];for(let s=0,o=i.length;s<o;s++)i[s].isDataTexture?r.push(Ca(i[s].image)):r.push(Ca(i[s]))}else r=Ca(i);n.url=r}return t||(e.images[this.uuid]=n),n}};function Ca(c){return typeof HTMLImageElement<"u"&&c instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&c instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&c instanceof ImageBitmap?Xs.getDataURL(c):c.data?{data:Array.from(c.data),width:c.width,height:c.height,type:c.data.constructor.name}:(console.warn("THREE.Texture: Unable to serialize Texture."),{})}var Af=0,Ra=new ee,nr=class c extends Hr{constructor(e=c.DEFAULT_IMAGE,t=c.DEFAULT_MAPPING,n=kr,i=kr,r=hh,s=uh,o=dh,a=fh,l=c.DEFAULT_ANISOTROPY,h=Qa){super(),this.isTexture=!0,Object.defineProperty(this,"id",{value:Af++}),this.uuid=ja(),this.name="",this.source=new qs(e),this.mipmaps=[],this.mapping=t,this.channel=0,this.wrapS=n,this.wrapT=i,this.magFilter=r,this.minFilter=s,this.anisotropy=l,this.format=o,this.internalFormat=null,this.type=a,this.offset=new rn(0,0),this.repeat=new rn(1,1),this.center=new rn(0,0),this.rotation=0,this.matrixAutoUpdate=!0,this.matrix=new Z,this.generateMipmaps=!0,this.premultiplyAlpha=!1,this.flipY=!0,this.unpackAlignment=4,this.colorSpace=h,this.userData={},this.updateRanges=[],this.version=0,this.onUpdate=null,this.renderTarget=null,this.isRenderTargetTexture=!1,this.isArrayTexture=!!(e&&e.depth&&e.depth>1),this.pmremVersion=0}get width(){return this.source.getSize(Ra).x}get height(){return this.source.getSize(Ra).y}get depth(){return this.source.getSize(Ra).z}get image(){return this.source.data}set image(e=null){this.source.data=e}updateMatrix(){this.matrix.setUvTransform(this.offset.x,this.offset.y,this.repeat.x,this.repeat.y,this.rotation,this.center.x,this.center.y)}addUpdateRange(e,t){this.updateRanges.push({start:e,count:t})}clearUpdateRanges(){this.updateRanges.length=0}clone(){return new this.constructor().copy(this)}copy(e){return this.name=e.name,this.source=e.source,this.mipmaps=e.mipmaps.slice(0),this.mapping=e.mapping,this.channel=e.channel,this.wrapS=e.wrapS,this.wrapT=e.wrapT,this.magFilter=e.magFilter,this.minFilter=e.minFilter,this.anisotropy=e.anisotropy,this.format=e.format,this.internalFormat=e.internalFormat,this.type=e.type,this.offset.copy(e.offset),this.repeat.copy(e.repeat),this.center.copy(e.center),this.rotation=e.rotation,this.matrixAutoUpdate=e.matrixAutoUpdate,this.matrix.copy(e.matrix),this.generateMipmaps=e.generateMipmaps,this.premultiplyAlpha=e.premultiplyAlpha,this.flipY=e.flipY,this.unpackAlignment=e.unpackAlignment,this.colorSpace=e.colorSpace,this.renderTarget=e.renderTarget,this.isRenderTargetTexture=e.isRenderTargetTexture,this.isArrayTexture=e.isArrayTexture,this.userData=JSON.parse(JSON.stringify(e.userData)),this.needsUpdate=!0,this}setValues(e){for(let t in e){let n=e[t];if(n===void 0){console.warn(`THREE.Texture.setValues(): parameter '${t}' has value of undefined.`);continue}let i=this[t];if(i===void 0){console.warn(`THREE.Texture.setValues(): property '${t}' does not exist.`);continue}i&&n&&i.isVector2&&n.isVector2||i&&n&&i.isVector3&&n.isVector3||i&&n&&i.isMatrix3&&n.isMatrix3?i.copy(n):this[t]=n}}toJSON(e){let t=e===void 0||typeof e=="string";if(!t&&e.textures[this.uuid]!==void 0)return e.textures[this.uuid];let n={metadata:{version:4.7,type:"Texture",generator:"Texture.toJSON"},uuid:this.uuid,name:this.name,image:this.source.toJSON(e).uuid,mapping:this.mapping,channel:this.channel,repeat:[this.repeat.x,this.repeat.y],offset:[this.offset.x,this.offset.y],center:[this.center.x,this.center.y],rotation:this.rotation,wrap:[this.wrapS,this.wrapT],format:this.format,internalFormat:this.internalFormat,type:this.type,colorSpace:this.colorSpace,minFilter:this.minFilter,magFilter:this.magFilter,anisotropy:this.anisotropy,flipY:this.flipY,generateMipmaps:this.generateMipmaps,premultiplyAlpha:this.premultiplyAlpha,unpackAlignment:this.unpackAlignment};return Object.keys(this.userData).length>0&&(n.userData=this.userData),t||(e.textures[this.uuid]=n),n}dispose(){this.dispatchEvent({type:"dispose"})}transformUv(e){if(this.mapping!==Ka)return e;if(e.applyMatrix3(this.matrix),e.x<0||e.x>1)switch(this.wrapS){case Da:e.x=e.x-Math.floor(e.x);break;case kr:e.x=e.x<0?0:1;break;case La:Math.abs(Math.floor(e.x)%2)===1?e.x=Math.ceil(e.x)-e.x:e.x=e.x-Math.floor(e.x);break}if(e.y<0||e.y>1)switch(this.wrapT){case Da:e.y=e.y-Math.floor(e.y);break;case kr:e.y=e.y<0?0:1;break;case La:Math.abs(Math.floor(e.y)%2)===1?e.y=Math.ceil(e.y)-e.y:e.y=e.y-Math.floor(e.y);break}return this.flipY&&(e.y=1-e.y),e}set needsUpdate(e){e===!0&&(this.version++,this.source.needsUpdate=!0)}set needsPMREMUpdate(e){e===!0&&this.pmremVersion++}};nr.DEFAULT_IMAGE=null;nr.DEFAULT_MAPPING=Ka;nr.DEFAULT_ANISOTROPY=1;var Yt=class c{constructor(e,t,n,i,r,s,o,a,l,h,f,d,u,m,p,g){c.prototype.isMatrix4=!0,this.elements=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],e!==void 0&&this.set(e,t,n,i,r,s,o,a,l,h,f,d,u,m,p,g)}set(e,t,n,i,r,s,o,a,l,h,f,d,u,m,p,g){let x=this.elements;return x[0]=e,x[4]=t,x[8]=n,x[12]=i,x[1]=r,x[5]=s,x[9]=o,x[13]=a,x[2]=l,x[6]=h,x[10]=f,x[14]=d,x[3]=u,x[7]=m,x[11]=p,x[15]=g,this}identity(){return this.set(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1),this}clone(){return new c().fromArray(this.elements)}copy(e){let t=this.elements,n=e.elements;return t[0]=n[0],t[1]=n[1],t[2]=n[2],t[3]=n[3],t[4]=n[4],t[5]=n[5],t[6]=n[6],t[7]=n[7],t[8]=n[8],t[9]=n[9],t[10]=n[10],t[11]=n[11],t[12]=n[12],t[13]=n[13],t[14]=n[14],t[15]=n[15],this}copyPosition(e){let t=this.elements,n=e.elements;return t[12]=n[12],t[13]=n[13],t[14]=n[14],this}setFromMatrix3(e){let t=e.elements;return this.set(t[0],t[3],t[6],0,t[1],t[4],t[7],0,t[2],t[5],t[8],0,0,0,0,1),this}extractBasis(e,t,n){return e.setFromMatrixColumn(this,0),t.setFromMatrixColumn(this,1),n.setFromMatrixColumn(this,2),this}makeBasis(e,t,n){return this.set(e.x,t.x,n.x,0,e.y,t.y,n.y,0,e.z,t.z,n.z,0,0,0,0,1),this}extractRotation(e){let t=this.elements,n=e.elements,i=1/Ki.setFromMatrixColumn(e,0).length(),r=1/Ki.setFromMatrixColumn(e,1).length(),s=1/Ki.setFromMatrixColumn(e,2).length();return t[0]=n[0]*i,t[1]=n[1]*i,t[2]=n[2]*i,t[3]=0,t[4]=n[4]*r,t[5]=n[5]*r,t[6]=n[6]*r,t[7]=0,t[8]=n[8]*s,t[9]=n[9]*s,t[10]=n[10]*s,t[11]=0,t[12]=0,t[13]=0,t[14]=0,t[15]=1,this}makeRotationFromEuler(e){let t=this.elements,n=e.x,i=e.y,r=e.z,s=Math.cos(n),o=Math.sin(n),a=Math.cos(i),l=Math.sin(i),h=Math.cos(r),f=Math.sin(r);if(e.order==="XYZ"){let d=s*h,u=s*f,m=o*h,p=o*f;t[0]=a*h,t[4]=-a*f,t[8]=l,t[1]=u+m*l,t[5]=d-p*l,t[9]=-o*a,t[2]=p-d*l,t[6]=m+u*l,t[10]=s*a}else if(e.order==="YXZ"){let d=a*h,u=a*f,m=l*h,p=l*f;t[0]=d+p*o,t[4]=m*o-u,t[8]=s*l,t[1]=s*f,t[5]=s*h,t[9]=-o,t[2]=u*o-m,t[6]=p+d*o,t[10]=s*a}else if(e.order==="ZXY"){let d=a*h,u=a*f,m=l*h,p=l*f;t[0]=d-p*o,t[4]=-s*f,t[8]=m+u*o,t[1]=u+m*o,t[5]=s*h,t[9]=p-d*o,t[2]=-s*l,t[6]=o,t[10]=s*a}else if(e.order==="ZYX"){let d=s*h,u=s*f,m=o*h,p=o*f;t[0]=a*h,t[4]=m*l-u,t[8]=d*l+p,t[1]=a*f,t[5]=p*l+d,t[9]=u*l-m,t[2]=-l,t[6]=o*a,t[10]=s*a}else if(e.order==="YZX"){let d=s*a,u=s*l,m=o*a,p=o*l;t[0]=a*h,t[4]=p-d*f,t[8]=m*f+u,t[1]=f,t[5]=s*h,t[9]=-o*h,t[2]=-l*h,t[6]=u*f+m,t[10]=d-p*f}else if(e.order==="XZY"){let d=s*a,u=s*l,m=o*a,p=o*l;t[0]=a*h,t[4]=-f,t[8]=l*h,t[1]=d*f+p,t[5]=s*h,t[9]=u*f-m,t[2]=m*f-u,t[6]=o*h,t[10]=p*f+d}return t[3]=0,t[7]=0,t[11]=0,t[12]=0,t[13]=0,t[14]=0,t[15]=1,this}makeRotationFromQuaternion(e){return this.compose(Cf,e,Rf)}lookAt(e,t,n){let i=this.elements;return Nt.subVectors(e,t),Nt.lengthSq()===0&&(Nt.z=1),Nt.normalize(),Jn.crossVectors(n,Nt),Jn.lengthSq()===0&&(Math.abs(n.z)===1?Nt.x+=1e-4:Nt.z+=1e-4,Nt.normalize(),Jn.crossVectors(n,Nt)),Jn.normalize(),Bs.crossVectors(Nt,Jn),i[0]=Jn.x,i[4]=Bs.x,i[8]=Nt.x,i[1]=Jn.y,i[5]=Bs.y,i[9]=Nt.y,i[2]=Jn.z,i[6]=Bs.z,i[10]=Nt.z,this}multiply(e){return this.multiplyMatrices(this,e)}premultiply(e){return this.multiplyMatrices(e,this)}multiplyMatrices(e,t){let n=e.elements,i=t.elements,r=this.elements,s=n[0],o=n[4],a=n[8],l=n[12],h=n[1],f=n[5],d=n[9],u=n[13],m=n[2],p=n[6],g=n[10],x=n[14],S=n[3],T=n[7],y=n[11],M=n[15],w=i[0],v=i[4],C=i[8],E=i[12],A=i[1],F=i[5],R=i[9],V=i[13],z=i[2],G=i[6],H=i[10],L=i[14],J=i[3],ie=i[7],_=i[11],re=i[15];return r[0]=s*w+o*A+a*z+l*J,r[4]=s*v+o*F+a*G+l*ie,r[8]=s*C+o*R+a*H+l*_,r[12]=s*E+o*V+a*L+l*re,r[1]=h*w+f*A+d*z+u*J,r[5]=h*v+f*F+d*G+u*ie,r[9]=h*C+f*R+d*H+u*_,r[13]=h*E+f*V+d*L+u*re,r[2]=m*w+p*A+g*z+x*J,r[6]=m*v+p*F+g*G+x*ie,r[10]=m*C+p*R+g*H+x*_,r[14]=m*E+p*V+g*L+x*re,r[3]=S*w+T*A+y*z+M*J,r[7]=S*v+T*F+y*G+M*ie,r[11]=S*C+T*R+y*H+M*_,r[15]=S*E+T*V+y*L+M*re,this}multiplyScalar(e){let t=this.elements;return t[0]*=e,t[4]*=e,t[8]*=e,t[12]*=e,t[1]*=e,t[5]*=e,t[9]*=e,t[13]*=e,t[2]*=e,t[6]*=e,t[10]*=e,t[14]*=e,t[3]*=e,t[7]*=e,t[11]*=e,t[15]*=e,this}determinant(){let e=this.elements,t=e[0],n=e[4],i=e[8],r=e[12],s=e[1],o=e[5],a=e[9],l=e[13],h=e[2],f=e[6],d=e[10],u=e[14],m=e[3],p=e[7],g=e[11],x=e[15];return m*(+r*a*f-i*l*f-r*o*d+n*l*d+i*o*u-n*a*u)+p*(+t*a*u-t*l*d+r*s*d-i*s*u+i*l*h-r*a*h)+g*(+t*l*f-t*o*u-r*s*f+n*s*u+r*o*h-n*l*h)+x*(-i*o*h-t*a*f+t*o*d+i*s*f-n*s*d+n*a*h)}transpose(){let e=this.elements,t;return t=e[1],e[1]=e[4],e[4]=t,t=e[2],e[2]=e[8],e[8]=t,t=e[6],e[6]=e[9],e[9]=t,t=e[3],e[3]=e[12],e[12]=t,t=e[7],e[7]=e[13],e[13]=t,t=e[11],e[11]=e[14],e[14]=t,this}setPosition(e,t,n){let i=this.elements;return e.isVector3?(i[12]=e.x,i[13]=e.y,i[14]=e.z):(i[12]=e,i[13]=t,i[14]=n),this}invert(){let e=this.elements,t=e[0],n=e[1],i=e[2],r=e[3],s=e[4],o=e[5],a=e[6],l=e[7],h=e[8],f=e[9],d=e[10],u=e[11],m=e[12],p=e[13],g=e[14],x=e[15],S=f*g*l-p*d*l+p*a*u-o*g*u-f*a*x+o*d*x,T=m*d*l-h*g*l-m*a*u+s*g*u+h*a*x-s*d*x,y=h*p*l-m*f*l+m*o*u-s*p*u-h*o*x+s*f*x,M=m*f*a-h*p*a-m*o*d+s*p*d+h*o*g-s*f*g,w=t*S+n*T+i*y+r*M;if(w===0)return this.set(0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0);let v=1/w;return e[0]=S*v,e[1]=(p*d*r-f*g*r-p*i*u+n*g*u+f*i*x-n*d*x)*v,e[2]=(o*g*r-p*a*r+p*i*l-n*g*l-o*i*x+n*a*x)*v,e[3]=(f*a*r-o*d*r-f*i*l+n*d*l+o*i*u-n*a*u)*v,e[4]=T*v,e[5]=(h*g*r-m*d*r+m*i*u-t*g*u-h*i*x+t*d*x)*v,e[6]=(m*a*r-s*g*r-m*i*l+t*g*l+s*i*x-t*a*x)*v,e[7]=(s*d*r-h*a*r+h*i*l-t*d*l-s*i*u+t*a*u)*v,e[8]=y*v,e[9]=(m*f*r-h*p*r-m*n*u+t*p*u+h*n*x-t*f*x)*v,e[10]=(s*p*r-m*o*r+m*n*l-t*p*l-s*n*x+t*o*x)*v,e[11]=(h*o*r-s*f*r-h*n*l+t*f*l+s*n*u-t*o*u)*v,e[12]=M*v,e[13]=(h*p*i-m*f*i+m*n*d-t*p*d-h*n*g+t*f*g)*v,e[14]=(m*o*i-s*p*i-m*n*a+t*p*a+s*n*g-t*o*g)*v,e[15]=(s*f*i-h*o*i+h*n*a-t*f*a-s*n*d+t*o*d)*v,this}scale(e){let t=this.elements,n=e.x,i=e.y,r=e.z;return t[0]*=n,t[4]*=i,t[8]*=r,t[1]*=n,t[5]*=i,t[9]*=r,t[2]*=n,t[6]*=i,t[10]*=r,t[3]*=n,t[7]*=i,t[11]*=r,this}getMaxScaleOnAxis(){let e=this.elements,t=e[0]*e[0]+e[1]*e[1]+e[2]*e[2],n=e[4]*e[4]+e[5]*e[5]+e[6]*e[6],i=e[8]*e[8]+e[9]*e[9]+e[10]*e[10];return Math.sqrt(Math.max(t,n,i))}makeTranslation(e,t,n){return e.isVector3?this.set(1,0,0,e.x,0,1,0,e.y,0,0,1,e.z,0,0,0,1):this.set(1,0,0,e,0,1,0,t,0,0,1,n,0,0,0,1),this}makeRotationX(e){let t=Math.cos(e),n=Math.sin(e);return this.set(1,0,0,0,0,t,-n,0,0,n,t,0,0,0,0,1),this}makeRotationY(e){let t=Math.cos(e),n=Math.sin(e);return this.set(t,0,n,0,0,1,0,0,-n,0,t,0,0,0,0,1),this}makeRotationZ(e){let t=Math.cos(e),n=Math.sin(e);return this.set(t,-n,0,0,n,t,0,0,0,0,1,0,0,0,0,1),this}makeRotationAxis(e,t){let n=Math.cos(t),i=Math.sin(t),r=1-n,s=e.x,o=e.y,a=e.z,l=r*s,h=r*o;return this.set(l*s+n,l*o-i*a,l*a+i*o,0,l*o+i*a,h*o+n,h*a-i*s,0,l*a-i*o,h*a+i*s,r*a*a+n,0,0,0,0,1),this}makeScale(e,t,n){return this.set(e,0,0,0,0,t,0,0,0,0,n,0,0,0,0,1),this}makeShear(e,t,n,i,r,s){return this.set(1,n,r,0,e,1,s,0,t,i,1,0,0,0,0,1),this}compose(e,t,n){let i=this.elements,r=t._x,s=t._y,o=t._z,a=t._w,l=r+r,h=s+s,f=o+o,d=r*l,u=r*h,m=r*f,p=s*h,g=s*f,x=o*f,S=a*l,T=a*h,y=a*f,M=n.x,w=n.y,v=n.z;return i[0]=(1-(p+x))*M,i[1]=(u+y)*M,i[2]=(m-T)*M,i[3]=0,i[4]=(u-y)*w,i[5]=(1-(d+x))*w,i[6]=(g+S)*w,i[7]=0,i[8]=(m+T)*v,i[9]=(g-S)*v,i[10]=(1-(d+p))*v,i[11]=0,i[12]=e.x,i[13]=e.y,i[14]=e.z,i[15]=1,this}decompose(e,t,n){let i=this.elements,r=Ki.set(i[0],i[1],i[2]).length(),s=Ki.set(i[4],i[5],i[6]).length(),o=Ki.set(i[8],i[9],i[10]).length();this.determinant()<0&&(r=-r),e.x=i[12],e.y=i[13],e.z=i[14],nn.copy(this);let l=1/r,h=1/s,f=1/o;return nn.elements[0]*=l,nn.elements[1]*=l,nn.elements[2]*=l,nn.elements[4]*=h,nn.elements[5]*=h,nn.elements[6]*=h,nn.elements[8]*=f,nn.elements[9]*=f,nn.elements[10]*=f,t.setFromRotationMatrix(nn),n.x=r,n.y=s,n.z=o,this}makePerspective(e,t,n,i,r,s,o=er,a=!1){let l=this.elements,h=2*r/(t-e),f=2*r/(n-i),d=(t+e)/(t-e),u=(n+i)/(n-i),m,p;if(a)m=r/(s-r),p=s*r/(s-r);else if(o===er)m=-(s+r)/(s-r),p=-2*s*r/(s-r);else if(o===za)m=-s/(s-r),p=-s*r/(s-r);else throw new Error("THREE.Matrix4.makePerspective(): Invalid coordinate system: "+o);return l[0]=h,l[4]=0,l[8]=d,l[12]=0,l[1]=0,l[5]=f,l[9]=u,l[13]=0,l[2]=0,l[6]=0,l[10]=m,l[14]=p,l[3]=0,l[7]=0,l[11]=-1,l[15]=0,this}makeOrthographic(e,t,n,i,r,s,o=er,a=!1){let l=this.elements,h=2/(t-e),f=2/(n-i),d=-(t+e)/(t-e),u=-(n+i)/(n-i),m,p;if(a)m=1/(s-r),p=s/(s-r);else if(o===er)m=-2/(s-r),p=-(s+r)/(s-r);else if(o===za)m=-1/(s-r),p=-r/(s-r);else throw new Error("THREE.Matrix4.makeOrthographic(): Invalid coordinate system: "+o);return l[0]=h,l[4]=0,l[8]=0,l[12]=d,l[1]=0,l[5]=f,l[9]=0,l[13]=u,l[2]=0,l[6]=0,l[10]=m,l[14]=p,l[3]=0,l[7]=0,l[11]=0,l[15]=1,this}equals(e){let t=this.elements,n=e.elements;for(let i=0;i<16;i++)if(t[i]!==n[i])return!1;return!0}fromArray(e,t=0){for(let n=0;n<16;n++)this.elements[n]=e[n+t];return this}toArray(e=[],t=0){let n=this.elements;return e[t]=n[0],e[t+1]=n[1],e[t+2]=n[2],e[t+3]=n[3],e[t+4]=n[4],e[t+5]=n[5],e[t+6]=n[6],e[t+7]=n[7],e[t+8]=n[8],e[t+9]=n[9],e[t+10]=n[10],e[t+11]=n[11],e[t+12]=n[12],e[t+13]=n[13],e[t+14]=n[14],e[t+15]=n[15],e}},Ki=new ee,nn=new Yt,Cf=new ee(0,0,0),Rf=new ee(1,1,1),Jn=new ee,Bs=new ee,Nt=new ee,ih=new Yt,rh=new In,Gr=class c{constructor(e=0,t=0,n=0,i=c.DEFAULT_ORDER){this.isEuler=!0,this._x=e,this._y=t,this._z=n,this._order=i}get x(){return this._x}set x(e){this._x=e,this._onChangeCallback()}get y(){return this._y}set y(e){this._y=e,this._onChangeCallback()}get z(){return this._z}set z(e){this._z=e,this._onChangeCallback()}get order(){return this._order}set order(e){this._order=e,this._onChangeCallback()}set(e,t,n,i=this._order){return this._x=e,this._y=t,this._z=n,this._order=i,this._onChangeCallback(),this}clone(){return new this.constructor(this._x,this._y,this._z,this._order)}copy(e){return this._x=e._x,this._y=e._y,this._z=e._z,this._order=e._order,this._onChangeCallback(),this}setFromRotationMatrix(e,t=this._order,n=!0){let i=e.elements,r=i[0],s=i[4],o=i[8],a=i[1],l=i[5],h=i[9],f=i[2],d=i[6],u=i[10];switch(t){case"XYZ":this._y=Math.asin(he(o,-1,1)),Math.abs(o)<.9999999?(this._x=Math.atan2(-h,u),this._z=Math.atan2(-s,r)):(this._x=Math.atan2(d,l),this._z=0);break;case"YXZ":this._x=Math.asin(-he(h,-1,1)),Math.abs(h)<.9999999?(this._y=Math.atan2(o,u),this._z=Math.atan2(a,l)):(this._y=Math.atan2(-f,r),this._z=0);break;case"ZXY":this._x=Math.asin(he(d,-1,1)),Math.abs(d)<.9999999?(this._y=Math.atan2(-f,u),this._z=Math.atan2(-s,l)):(this._y=0,this._z=Math.atan2(a,r));break;case"ZYX":this._y=Math.asin(-he(f,-1,1)),Math.abs(f)<.9999999?(this._x=Math.atan2(d,u),this._z=Math.atan2(a,r)):(this._x=0,this._z=Math.atan2(-s,l));break;case"YZX":this._z=Math.asin(he(a,-1,1)),Math.abs(a)<.9999999?(this._x=Math.atan2(-h,l),this._y=Math.atan2(-f,r)):(this._x=0,this._y=Math.atan2(o,u));break;case"XZY":this._z=Math.asin(-he(s,-1,1)),Math.abs(s)<.9999999?(this._x=Math.atan2(d,l),this._y=Math.atan2(o,r)):(this._x=Math.atan2(-h,u),this._y=0);break;default:console.warn("THREE.Euler: .setFromRotationMatrix() encountered an unknown order: "+t)}return this._order=t,n===!0&&this._onChangeCallback(),this}setFromQuaternion(e,t,n){return ih.makeRotationFromQuaternion(e),this.setFromRotationMatrix(ih,t,n)}setFromVector3(e,t=this._order){return this.set(e.x,e.y,e.z,t)}reorder(e){return rh.setFromEuler(this),this.setFromQuaternion(rh,e)}equals(e){return e._x===this._x&&e._y===this._y&&e._z===this._z&&e._order===this._order}fromArray(e){return this._x=e[0],this._y=e[1],this._z=e[2],e[3]!==void 0&&(this._order=e[3]),this._onChangeCallback(),this}toArray(e=[],t=0){return e[t]=this._x,e[t+1]=this._y,e[t+2]=this._z,e[t+3]=this._order,e}_onChange(e){return this._onChangeCallback=e,this}_onChangeCallback(){}*[Symbol.iterator](){yield this._x,yield this._y,yield this._z,yield this._order}};Gr.DEFAULT_ORDER="XYZ";var Ys=class{constructor(){this.mask=1}set(e){this.mask=(1<<e|0)>>>0}enable(e){this.mask|=1<<e|0}enableAll(){this.mask=-1}toggle(e){this.mask^=1<<e|0}disable(e){this.mask&=~(1<<e|0)}disableAll(){this.mask=0}test(e){return(this.mask&e.mask)!==0}isEnabled(e){return(this.mask&(1<<e|0))!==0}},Pf=0,sh=new ee,Qi=new In,Rn=new Yt,zs=new ee,zr=new ee,If=new ee,Df=new In,oh=new ee(1,0,0),ah=new ee(0,1,0),lh=new ee(0,0,1),ch={type:"added"},Lf={type:"removed"},ji={type:"childadded",child:null},Pa={type:"childremoved",child:null},Kn=class c extends Hr{constructor(){super(),this.isObject3D=!0,Object.defineProperty(this,"id",{value:Pf++}),this.uuid=ja(),this.name="",this.type="Object3D",this.parent=null,this.children=[],this.up=c.DEFAULT_UP.clone();let e=new ee,t=new Gr,n=new In,i=new ee(1,1,1);function r(){n.setFromEuler(t,!1)}function s(){t.setFromQuaternion(n,void 0,!1)}t._onChange(r),n._onChange(s),Object.defineProperties(this,{position:{configurable:!0,enumerable:!0,value:e},rotation:{configurable:!0,enumerable:!0,value:t},quaternion:{configurable:!0,enumerable:!0,value:n},scale:{configurable:!0,enumerable:!0,value:i},modelViewMatrix:{value:new Yt},normalMatrix:{value:new Z}}),this.matrix=new Yt,this.matrixWorld=new Yt,this.matrixAutoUpdate=c.DEFAULT_MATRIX_AUTO_UPDATE,this.matrixWorldAutoUpdate=c.DEFAULT_MATRIX_WORLD_AUTO_UPDATE,this.matrixWorldNeedsUpdate=!1,this.layers=new Ys,this.visible=!0,this.castShadow=!1,this.receiveShadow=!1,this.frustumCulled=!0,this.renderOrder=0,this.animations=[],this.customDepthMaterial=void 0,this.customDistanceMaterial=void 0,this.userData={}}onBeforeShadow(){}onAfterShadow(){}onBeforeRender(){}onAfterRender(){}applyMatrix4(e){this.matrixAutoUpdate&&this.updateMatrix(),this.matrix.premultiply(e),this.matrix.decompose(this.position,this.quaternion,this.scale)}applyQuaternion(e){return this.quaternion.premultiply(e),this}setRotationFromAxisAngle(e,t){this.quaternion.setFromAxisAngle(e,t)}setRotationFromEuler(e){this.quaternion.setFromEuler(e,!0)}setRotationFromMatrix(e){this.quaternion.setFromRotationMatrix(e)}setRotationFromQuaternion(e){this.quaternion.copy(e)}rotateOnAxis(e,t){return Qi.setFromAxisAngle(e,t),this.quaternion.multiply(Qi),this}rotateOnWorldAxis(e,t){return Qi.setFromAxisAngle(e,t),this.quaternion.premultiply(Qi),this}rotateX(e){return this.rotateOnAxis(oh,e)}rotateY(e){return this.rotateOnAxis(ah,e)}rotateZ(e){return this.rotateOnAxis(lh,e)}translateOnAxis(e,t){return sh.copy(e).applyQuaternion(this.quaternion),this.position.add(sh.multiplyScalar(t)),this}translateX(e){return this.translateOnAxis(oh,e)}translateY(e){return this.translateOnAxis(ah,e)}translateZ(e){return this.translateOnAxis(lh,e)}localToWorld(e){return this.updateWorldMatrix(!0,!1),e.applyMatrix4(this.matrixWorld)}worldToLocal(e){return this.updateWorldMatrix(!0,!1),e.applyMatrix4(Rn.copy(this.matrixWorld).invert())}lookAt(e,t,n){e.isVector3?zs.copy(e):zs.set(e,t,n);let i=this.parent;this.updateWorldMatrix(!0,!1),zr.setFromMatrixPosition(this.matrixWorld),this.isCamera||this.isLight?Rn.lookAt(zr,zs,this.up):Rn.lookAt(zs,zr,this.up),this.quaternion.setFromRotationMatrix(Rn),i&&(Rn.extractRotation(i.matrixWorld),Qi.setFromRotationMatrix(Rn),this.quaternion.premultiply(Qi.invert()))}add(e){if(arguments.length>1){for(let t=0;t<arguments.length;t++)this.add(arguments[t]);return this}return e===this?(console.error("THREE.Object3D.add: object can't be added as a child of itself.",e),this):(e&&e.isObject3D?(e.removeFromParent(),e.parent=this,this.children.push(e),e.dispatchEvent(ch),ji.child=e,this.dispatchEvent(ji),ji.child=null):console.error("THREE.Object3D.add: object not an instance of THREE.Object3D.",e),this)}remove(e){if(arguments.length>1){for(let n=0;n<arguments.length;n++)this.remove(arguments[n]);return this}let t=this.children.indexOf(e);return t!==-1&&(e.parent=null,this.children.splice(t,1),e.dispatchEvent(Lf),Pa.child=e,this.dispatchEvent(Pa),Pa.child=null),this}removeFromParent(){let e=this.parent;return e!==null&&e.remove(this),this}clear(){return this.remove(...this.children)}attach(e){return this.updateWorldMatrix(!0,!1),Rn.copy(this.matrixWorld).invert(),e.parent!==null&&(e.parent.updateWorldMatrix(!0,!1),Rn.multiply(e.parent.matrixWorld)),e.applyMatrix4(Rn),e.removeFromParent(),e.parent=this,this.children.push(e),e.updateWorldMatrix(!1,!0),e.dispatchEvent(ch),ji.child=e,this.dispatchEvent(ji),ji.child=null,this}getObjectById(e){return this.getObjectByProperty("id",e)}getObjectByName(e){return this.getObjectByProperty("name",e)}getObjectByProperty(e,t){if(this[e]===t)return this;for(let n=0,i=this.children.length;n<i;n++){let s=this.children[n].getObjectByProperty(e,t);if(s!==void 0)return s}}getObjectsByProperty(e,t,n=[]){this[e]===t&&n.push(this);let i=this.children;for(let r=0,s=i.length;r<s;r++)i[r].getObjectsByProperty(e,t,n);return n}getWorldPosition(e){return this.updateWorldMatrix(!0,!1),e.setFromMatrixPosition(this.matrixWorld)}getWorldQuaternion(e){return this.updateWorldMatrix(!0,!1),this.matrixWorld.decompose(zr,e,If),e}getWorldScale(e){return this.updateWorldMatrix(!0,!1),this.matrixWorld.decompose(zr,Df,e),e}getWorldDirection(e){this.updateWorldMatrix(!0,!1);let t=this.matrixWorld.elements;return e.set(t[8],t[9],t[10]).normalize()}raycast(){}traverse(e){e(this);let t=this.children;for(let n=0,i=t.length;n<i;n++)t[n].traverse(e)}traverseVisible(e){if(this.visible===!1)return;e(this);let t=this.children;for(let n=0,i=t.length;n<i;n++)t[n].traverseVisible(e)}traverseAncestors(e){let t=this.parent;t!==null&&(e(t),t.traverseAncestors(e))}updateMatrix(){this.matrix.compose(this.position,this.quaternion,this.scale),this.matrixWorldNeedsUpdate=!0}updateMatrixWorld(e){this.matrixAutoUpdate&&this.updateMatrix(),(this.matrixWorldNeedsUpdate||e)&&(this.matrixWorldAutoUpdate===!0&&(this.parent===null?this.matrixWorld.copy(this.matrix):this.matrixWorld.multiplyMatrices(this.parent.matrixWorld,this.matrix)),this.matrixWorldNeedsUpdate=!1,e=!0);let t=this.children;for(let n=0,i=t.length;n<i;n++)t[n].updateMatrixWorld(e)}updateWorldMatrix(e,t){let n=this.parent;if(e===!0&&n!==null&&n.updateWorldMatrix(!0,!1),this.matrixAutoUpdate&&this.updateMatrix(),this.matrixWorldAutoUpdate===!0&&(this.parent===null?this.matrixWorld.copy(this.matrix):this.matrixWorld.multiplyMatrices(this.parent.matrixWorld,this.matrix)),t===!0){let i=this.children;for(let r=0,s=i.length;r<s;r++)i[r].updateWorldMatrix(!1,!0)}}toJSON(e){let t=e===void 0||typeof e=="string",n={};t&&(e={geometries:{},materials:{},textures:{},images:{},shapes:{},skeletons:{},animations:{},nodes:{}},n.metadata={version:4.7,type:"Object",generator:"Object3D.toJSON"});let i={};i.uuid=this.uuid,i.type=this.type,this.name!==""&&(i.name=this.name),this.castShadow===!0&&(i.castShadow=!0),this.receiveShadow===!0&&(i.receiveShadow=!0),this.visible===!1&&(i.visible=!1),this.frustumCulled===!1&&(i.frustumCulled=!1),this.renderOrder!==0&&(i.renderOrder=this.renderOrder),Object.keys(this.userData).length>0&&(i.userData=this.userData),i.layers=this.layers.mask,i.matrix=this.matrix.toArray(),i.up=this.up.toArray(),this.matrixAutoUpdate===!1&&(i.matrixAutoUpdate=!1),this.isInstancedMesh&&(i.type="InstancedMesh",i.count=this.count,i.instanceMatrix=this.instanceMatrix.toJSON(),this.instanceColor!==null&&(i.instanceColor=this.instanceColor.toJSON())),this.isBatchedMesh&&(i.type="BatchedMesh",i.perObjectFrustumCulled=this.perObjectFrustumCulled,i.sortObjects=this.sortObjects,i.drawRanges=this._drawRanges,i.reservedRanges=this._reservedRanges,i.geometryInfo=this._geometryInfo.map(o=>({...o,boundingBox:o.boundingBox?o.boundingBox.toJSON():void 0,boundingSphere:o.boundingSphere?o.boundingSphere.toJSON():void 0})),i.instanceInfo=this._instanceInfo.map(o=>({...o})),i.availableInstanceIds=this._availableInstanceIds.slice(),i.availableGeometryIds=this._availableGeometryIds.slice(),i.nextIndexStart=this._nextIndexStart,i.nextVertexStart=this._nextVertexStart,i.geometryCount=this._geometryCount,i.maxInstanceCount=this._maxInstanceCount,i.maxVertexCount=this._maxVertexCount,i.maxIndexCount=this._maxIndexCount,i.geometryInitialized=this._geometryInitialized,i.matricesTexture=this._matricesTexture.toJSON(e),i.indirectTexture=this._indirectTexture.toJSON(e),this._colorsTexture!==null&&(i.colorsTexture=this._colorsTexture.toJSON(e)),this.boundingSphere!==null&&(i.boundingSphere=this.boundingSphere.toJSON()),this.boundingBox!==null&&(i.boundingBox=this.boundingBox.toJSON()));function r(o,a){return o[a.uuid]===void 0&&(o[a.uuid]=a.toJSON(e)),a.uuid}if(this.isScene)this.background&&(this.background.isColor?i.background=this.background.toJSON():this.background.isTexture&&(i.background=this.background.toJSON(e).uuid)),this.environment&&this.environment.isTexture&&this.environment.isRenderTargetTexture!==!0&&(i.environment=this.environment.toJSON(e).uuid);else if(this.isMesh||this.isLine||this.isPoints){i.geometry=r(e.geometries,this.geometry);let o=this.geometry.parameters;if(o!==void 0&&o.shapes!==void 0){let a=o.shapes;if(Array.isArray(a))for(let l=0,h=a.length;l<h;l++){let f=a[l];r(e.shapes,f)}else r(e.shapes,a)}}if(this.isSkinnedMesh&&(i.bindMode=this.bindMode,i.bindMatrix=this.bindMatrix.toArray(),this.skeleton!==void 0&&(r(e.skeletons,this.skeleton),i.skeleton=this.skeleton.uuid)),this.material!==void 0)if(Array.isArray(this.material)){let o=[];for(let a=0,l=this.material.length;a<l;a++)o.push(r(e.materials,this.material[a]));i.material=o}else i.material=r(e.materials,this.material);if(this.children.length>0){i.children=[];for(let o=0;o<this.children.length;o++)i.children.push(this.children[o].toJSON(e).object)}if(this.animations.length>0){i.animations=[];for(let o=0;o<this.animations.length;o++){let a=this.animations[o];i.animations.push(r(e.animations,a))}}if(t){let o=s(e.geometries),a=s(e.materials),l=s(e.textures),h=s(e.images),f=s(e.shapes),d=s(e.skeletons),u=s(e.animations),m=s(e.nodes);o.length>0&&(n.geometries=o),a.length>0&&(n.materials=a),l.length>0&&(n.textures=l),h.length>0&&(n.images=h),f.length>0&&(n.shapes=f),d.length>0&&(n.skeletons=d),u.length>0&&(n.animations=u),m.length>0&&(n.nodes=m)}return n.object=i,n;function s(o){let a=[];for(let l in o){let h=o[l];delete h.metadata,a.push(h)}return a}}clone(e){return new this.constructor().copy(this,e)}copy(e,t=!0){if(this.name=e.name,this.up.copy(e.up),this.position.copy(e.position),this.rotation.order=e.rotation.order,this.quaternion.copy(e.quaternion),this.scale.copy(e.scale),this.matrix.copy(e.matrix),this.matrixWorld.copy(e.matrixWorld),this.matrixAutoUpdate=e.matrixAutoUpdate,this.matrixWorldAutoUpdate=e.matrixWorldAutoUpdate,this.matrixWorldNeedsUpdate=e.matrixWorldNeedsUpdate,this.layers.mask=e.layers.mask,this.visible=e.visible,this.castShadow=e.castShadow,this.receiveShadow=e.receiveShadow,this.frustumCulled=e.frustumCulled,this.renderOrder=e.renderOrder,this.animations=e.animations.slice(),this.userData=JSON.parse(JSON.stringify(e.userData)),t===!0)for(let n=0;n<e.children.length;n++){let i=e.children[n];this.add(i.clone())}return this}};Kn.DEFAULT_UP=new ee(0,1,0);Kn.DEFAULT_MATRIX_AUTO_UPDATE=!0;Kn.DEFAULT_MATRIX_WORLD_AUTO_UPDATE=!0;var ph={aliceblue:15792383,antiquewhite:16444375,aqua:65535,aquamarine:8388564,azure:15794175,beige:16119260,bisque:16770244,black:0,blanchedalmond:16772045,blue:255,blueviolet:9055202,brown:10824234,burlywood:14596231,cadetblue:6266528,chartreuse:8388352,chocolate:13789470,coral:16744272,cornflowerblue:6591981,cornsilk:16775388,crimson:14423100,cyan:65535,darkblue:139,darkcyan:35723,darkgoldenrod:12092939,darkgray:11119017,darkgreen:25600,darkgrey:11119017,darkkhaki:12433259,darkmagenta:9109643,darkolivegreen:5597999,darkorange:16747520,darkorchid:10040012,darkred:9109504,darksalmon:15308410,darkseagreen:9419919,darkslateblue:4734347,darkslategray:3100495,darkslategrey:3100495,darkturquoise:52945,darkviolet:9699539,deeppink:16716947,deepskyblue:49151,dimgray:6908265,dimgrey:6908265,dodgerblue:2003199,firebrick:11674146,floralwhite:16775920,forestgreen:2263842,fuchsia:16711935,gainsboro:14474460,ghostwhite:16316671,gold:16766720,goldenrod:14329120,gray:8421504,green:32768,greenyellow:11403055,grey:8421504,honeydew:15794160,hotpink:16738740,indianred:13458524,indigo:4915330,ivory:16777200,khaki:15787660,lavender:15132410,lavenderblush:16773365,lawngreen:8190976,lemonchiffon:16775885,lightblue:11393254,lightcoral:15761536,lightcyan:14745599,lightgoldenrodyellow:16448210,lightgray:13882323,lightgreen:9498256,lightgrey:13882323,lightpink:16758465,lightsalmon:16752762,lightseagreen:2142890,lightskyblue:8900346,lightslategray:7833753,lightslategrey:7833753,lightsteelblue:11584734,lightyellow:16777184,lime:65280,limegreen:3329330,linen:16445670,magenta:16711935,maroon:8388608,mediumaquamarine:6737322,mediumblue:205,mediumorchid:12211667,mediumpurple:9662683,mediumseagreen:3978097,mediumslateblue:8087790,mediumspringgreen:64154,mediumturquoise:4772300,mediumvioletred:13047173,midnightblue:1644912,mintcream:16121850,mistyrose:16770273,moccasin:16770229,navajowhite:16768685,navy:128,oldlace:16643558,olive:8421376,olivedrab:7048739,orange:16753920,orangered:16729344,orchid:14315734,palegoldenrod:15657130,palegreen:10025880,paleturquoise:11529966,palevioletred:14381203,papayawhip:16773077,peachpuff:16767673,peru:13468991,pink:16761035,plum:14524637,powderblue:11591910,purple:8388736,rebeccapurple:6697881,red:16711680,rosybrown:12357519,royalblue:4286945,saddlebrown:9127187,salmon:16416882,sandybrown:16032864,seagreen:3050327,seashell:16774638,sienna:10506797,silver:12632256,skyblue:8900331,slateblue:6970061,slategray:7372944,slategrey:7372944,snow:16775930,springgreen:65407,steelblue:4620980,tan:13808780,teal:32896,thistle:14204888,tomato:16737095,turquoise:4251856,violet:15631086,wheat:16113331,white:16777215,whitesmoke:16119285,yellow:16776960,yellowgreen:10145074},$n={h:0,s:0,l:0},ks={h:0,s:0,l:0};function Ia(c,e,t){return t<0&&(t+=1),t>1&&(t-=1),t<1/6?c+(e-c)*6*t:t<1/2?e:t<2/3?c+(e-c)*6*(2/3-t):c}var ze=class{constructor(e,t,n){return this.isColor=!0,this.r=1,this.g=1,this.b=1,this.set(e,t,n)}set(e,t,n){if(t===void 0&&n===void 0){let i=e;i&&i.isColor?this.copy(i):typeof i=="number"?this.setHex(i):typeof i=="string"&&this.setStyle(i)}else this.setRGB(e,t,n);return this}setScalar(e){return this.r=e,this.g=e,this.b=e,this}setHex(e,t=qt){return e=Math.floor(e),this.r=(e>>16&255)/255,this.g=(e>>8&255)/255,this.b=(e&255)/255,Xt.colorSpaceToWorking(this,t),this}setRGB(e,t,n,i=Xt.workingColorSpace){return this.r=e,this.g=t,this.b=n,Xt.colorSpaceToWorking(this,i),this}setHSL(e,t,n,i=Xt.workingColorSpace){if(e=Tf(e,1),t=he(t,0,1),n=he(n,0,1),t===0)this.r=this.g=this.b=n;else{let r=n<=.5?n*(1+t):n+t-n*t,s=2*n-r;this.r=Ia(s,r,e+1/3),this.g=Ia(s,r,e),this.b=Ia(s,r,e-1/3)}return Xt.colorSpaceToWorking(this,i),this}setStyle(e,t=qt){function n(r){r!==void 0&&parseFloat(r)<1&&console.warn("THREE.Color: Alpha component of "+e+" will be ignored.")}let i;if(i=/^(\w+)\(([^\)]*)\)/.exec(e)){let r,s=i[1],o=i[2];switch(s){case"rgb":case"rgba":if(r=/^\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setRGB(Math.min(255,parseInt(r[1],10))/255,Math.min(255,parseInt(r[2],10))/255,Math.min(255,parseInt(r[3],10))/255,t);if(r=/^\s*(\d+)\%\s*,\s*(\d+)\%\s*,\s*(\d+)\%\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setRGB(Math.min(100,parseInt(r[1],10))/100,Math.min(100,parseInt(r[2],10))/100,Math.min(100,parseInt(r[3],10))/100,t);break;case"hsl":case"hsla":if(r=/^\s*(\d*\.?\d+)\s*,\s*(\d*\.?\d+)\%\s*,\s*(\d*\.?\d+)\%\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setHSL(parseFloat(r[1])/360,parseFloat(r[2])/100,parseFloat(r[3])/100,t);break;default:console.warn("THREE.Color: Unknown color model "+e)}}else if(i=/^\#([A-Fa-f\d]+)$/.exec(e)){let r=i[1],s=r.length;if(s===3)return this.setRGB(parseInt(r.charAt(0),16)/15,parseInt(r.charAt(1),16)/15,parseInt(r.charAt(2),16)/15,t);if(s===6)return this.setHex(parseInt(r,16),t);console.warn("THREE.Color: Invalid hex color "+e)}else if(e&&e.length>0)return this.setColorName(e,t);return this}setColorName(e,t=qt){let n=ph[e.toLowerCase()];return n!==void 0?this.setHex(n,t):console.warn("THREE.Color: Unknown color "+e),this}clone(){return new this.constructor(this.r,this.g,this.b)}copy(e){return this.r=e.r,this.g=e.g,this.b=e.b,this}copySRGBToLinear(e){return this.r=Pn(e.r),this.g=Pn(e.g),this.b=Pn(e.b),this}copyLinearToSRGB(e){return this.r=tr(e.r),this.g=tr(e.g),this.b=tr(e.b),this}convertSRGBToLinear(){return this.copySRGBToLinear(this),this}convertLinearToSRGB(){return this.copyLinearToSRGB(this),this}getHex(e=qt){return Xt.workingToColorSpace(st.copy(this),e),Math.round(he(st.r*255,0,255))*65536+Math.round(he(st.g*255,0,255))*256+Math.round(he(st.b*255,0,255))}getHexString(e=qt){return("000000"+this.getHex(e).toString(16)).slice(-6)}getHSL(e,t=Xt.workingColorSpace){Xt.workingToColorSpace(st.copy(this),t);let n=st.r,i=st.g,r=st.b,s=Math.max(n,i,r),o=Math.min(n,i,r),a,l,h=(o+s)/2;if(o===s)a=0,l=0;else{let f=s-o;switch(l=h<=.5?f/(s+o):f/(2-s-o),s){case n:a=(i-r)/f+(i<r?6:0);break;case i:a=(r-n)/f+2;break;case r:a=(n-i)/f+4;break}a/=6}return e.h=a,e.s=l,e.l=h,e}getRGB(e,t=Xt.workingColorSpace){return Xt.workingToColorSpace(st.copy(this),t),e.r=st.r,e.g=st.g,e.b=st.b,e}getStyle(e=qt){Xt.workingToColorSpace(st.copy(this),e);let t=st.r,n=st.g,i=st.b;return e!==qt?`color(${e} ${t.toFixed(3)} ${n.toFixed(3)} ${i.toFixed(3)})`:`rgb(${Math.round(t*255)},${Math.round(n*255)},${Math.round(i*255)})`}offsetHSL(e,t,n){return this.getHSL($n),this.setHSL($n.h+e,$n.s+t,$n.l+n)}add(e){return this.r+=e.r,this.g+=e.g,this.b+=e.b,this}addColors(e,t){return this.r=e.r+t.r,this.g=e.g+t.g,this.b=e.b+t.b,this}addScalar(e){return this.r+=e,this.g+=e,this.b+=e,this}sub(e){return this.r=Math.max(0,this.r-e.r),this.g=Math.max(0,this.g-e.g),this.b=Math.max(0,this.b-e.b),this}multiply(e){return this.r*=e.r,this.g*=e.g,this.b*=e.b,this}multiplyScalar(e){return this.r*=e,this.g*=e,this.b*=e,this}lerp(e,t){return this.r+=(e.r-this.r)*t,this.g+=(e.g-this.g)*t,this.b+=(e.b-this.b)*t,this}lerpColors(e,t,n){return this.r=e.r+(t.r-e.r)*n,this.g=e.g+(t.g-e.g)*n,this.b=e.b+(t.b-e.b)*n,this}lerpHSL(e,t){this.getHSL($n),e.getHSL(ks);let n=wa($n.h,ks.h,t),i=wa($n.s,ks.s,t),r=wa($n.l,ks.l,t);return this.setHSL(n,i,r),this}setFromVector3(e){return this.r=e.x,this.g=e.y,this.b=e.z,this}applyMatrix3(e){let t=this.r,n=this.g,i=this.b,r=e.elements;return this.r=r[0]*t+r[3]*n+r[6]*i,this.g=r[1]*t+r[4]*n+r[7]*i,this.b=r[2]*t+r[5]*n+r[8]*i,this}equals(e){return e.r===this.r&&e.g===this.g&&e.b===this.b}fromArray(e,t=0){return this.r=e[t],this.g=e[t+1],this.b=e[t+2],this}toArray(e=[],t=0){return e[t]=this.r,e[t+1]=this.g,e[t+2]=this.b,e}fromBufferAttribute(e,t){return this.r=e.getX(t),this.g=e.getY(t),this.b=e.getZ(t),this}toJSON(){return this.getHex()}*[Symbol.iterator](){yield this.r,yield this.g,yield this.b}},st=new ze;ze.NAMES=ph;function mh(c){let e={};for(let t in c){e[t]={};for(let n in c[t]){let i=c[t][n];i&&(i.isColor||i.isMatrix3||i.isMatrix4||i.isVector2||i.isVector3||i.isVector4||i.isTexture||i.isQuaternion)?i.isRenderTargetTexture?(console.warn("UniformsUtils: Textures of render targets cannot be cloned via cloneUniforms() or mergeUniforms()."),e[t][n]=null):e[t][n]=i.clone():Array.isArray(i)?e[t][n]=i.slice():e[t][n]=i}}return e}function _t(c){let e={};for(let t=0;t<c.length;t++){let n=mh(c[t]);for(let i in n)e[i]=n[i]}return e}var Zs=class extends Kn{constructor(){super(),this.isCamera=!0,this.type="Camera",this.matrixWorldInverse=new Yt,this.projectionMatrix=new Yt,this.projectionMatrixInverse=new Yt,this.coordinateSystem=er,this._reversedDepth=!1}get reversedDepth(){return this._reversedDepth}copy(e,t){return super.copy(e,t),this.matrixWorldInverse.copy(e.matrixWorldInverse),this.projectionMatrix.copy(e.projectionMatrix),this.projectionMatrixInverse.copy(e.projectionMatrixInverse),this.coordinateSystem=e.coordinateSystem,this}getWorldDirection(e){return super.getWorldDirection(e).negate()}updateMatrixWorld(e){super.updateMatrixWorld(e),this.matrixWorldInverse.copy(this.matrixWorld).invert()}updateWorldMatrix(e,t){super.updateWorldMatrix(e,t),this.matrixWorldInverse.copy(this.matrixWorld).invert()}clone(){return new this.constructor().copy(this)}};var Wr=class extends Kn{constructor(){super(),this.isGroup=!0,this.type="Group"}};function Vs(c,e){return!c||c.constructor===e?c:typeof e.BYTES_PER_ELEMENT=="number"?new e(c):Array.prototype.slice.call(c)}function Uf(c){return ArrayBuffer.isView(c)&&!(c instanceof DataView)}var wi=class{constructor(e,t,n,i){this.parameterPositions=e,this._cachedIndex=0,this.resultBuffer=i!==void 0?i:new t.constructor(n),this.sampleValues=t,this.valueSize=n,this.settings=null,this.DefaultSettings_={}}evaluate(e){let t=this.parameterPositions,n=this._cachedIndex,i=t[n],r=t[n-1];n:{e:{let s;t:{i:if(!(e<i)){for(let o=n+2;;){if(i===void 0){if(e<r)break i;return n=t.length,this._cachedIndex=n,this.copySampleValue_(n-1)}if(n===o)break;if(r=i,i=t[++n],e<i)break e}s=t.length;break t}if(!(e>=r)){let o=t[1];e<o&&(n=2,r=o);for(let a=n-2;;){if(r===void 0)return this._cachedIndex=0,this.copySampleValue_(0);if(n===a)break;if(i=r,r=t[--n-1],e>=r)break e}s=n,n=0;break t}break n}for(;n<s;){let o=n+s>>>1;e<t[o]?s=o:n=o+1}if(i=t[n],r=t[n-1],r===void 0)return this._cachedIndex=0,this.copySampleValue_(0);if(i===void 0)return n=t.length,this._cachedIndex=n,this.copySampleValue_(n-1)}this._cachedIndex=n,this.intervalChanged_(n,r,i)}return this.interpolate_(n,r,e,i)}getSettings_(){return this.settings||this.DefaultSettings_}copySampleValue_(e){let t=this.resultBuffer,n=this.sampleValues,i=this.valueSize,r=e*i;for(let s=0;s!==i;++s)t[s]=n[r+s];return t}interpolate_(){throw new Error("call to abstract method")}intervalChanged_(){}},Js=class extends wi{constructor(e,t,n,i){super(e,t,n,i),this._weightPrev=-0,this._offsetPrev=-0,this._weightNext=-0,this._offsetNext=-0,this.DefaultSettings_={endingStart:Ua,endingEnd:Ua}}intervalChanged_(e,t,n){let i=this.parameterPositions,r=e-2,s=e+1,o=i[r],a=i[s];if(o===void 0)switch(this.getSettings_().endingStart){case Na:r=e,o=2*t-n;break;case Fa:r=i.length-2,o=t+i[r]-i[r+1];break;default:r=e,o=n}if(a===void 0)switch(this.getSettings_().endingEnd){case Na:s=e,a=2*n-t;break;case Fa:s=1,a=n+i[1]-i[0];break;default:s=e-1,a=t}let l=(n-t)*.5,h=this.valueSize;this._weightPrev=l/(t-o),this._weightNext=l/(a-n),this._offsetPrev=r*h,this._offsetNext=s*h}interpolate_(e,t,n,i){let r=this.resultBuffer,s=this.sampleValues,o=this.valueSize,a=e*o,l=a-o,h=this._offsetPrev,f=this._offsetNext,d=this._weightPrev,u=this._weightNext,m=(n-t)/(i-t),p=m*m,g=p*m,x=-d*g+2*d*p-d*m,S=(1+d)*g+(-1.5-2*d)*p+(-.5+d)*m+1,T=(-1-u)*g+(1.5+u)*p+.5*m,y=u*g-u*p;for(let M=0;M!==o;++M)r[M]=x*s[h+M]+S*s[l+M]+T*s[a+M]+y*s[f+M];return r}},$s=class extends wi{constructor(e,t,n,i){super(e,t,n,i)}interpolate_(e,t,n,i){let r=this.resultBuffer,s=this.sampleValues,o=this.valueSize,a=e*o,l=a-o,h=(n-t)/(i-t),f=1-h;for(let d=0;d!==o;++d)r[d]=s[l+d]*f+s[a+d]*h;return r}},Ks=class extends wi{constructor(e,t,n,i){super(e,t,n,i)}interpolate_(e){return this.copySampleValue_(e-1)}},Ft=class{constructor(e,t,n,i){if(e===void 0)throw new Error("THREE.KeyframeTrack: track name is undefined");if(t===void 0||t.length===0)throw new Error("THREE.KeyframeTrack: no keyframes in track named "+e);this.name=e,this.times=Vs(t,this.TimeBufferType),this.values=Vs(n,this.ValueBufferType),this.setInterpolation(i||this.DefaultInterpolation)}static toJSON(e){let t=e.constructor,n;if(t.toJSON!==this.toJSON)n=t.toJSON(e);else{n={name:e.name,times:Vs(e.times,Array),values:Vs(e.values,Array)};let i=e.getInterpolation();i!==e.DefaultInterpolation&&(n.interpolation=i)}return n.type=e.ValueTypeName,n}InterpolantFactoryMethodDiscrete(e){return new Ks(this.times,this.values,this.getValueSize(),e)}InterpolantFactoryMethodLinear(e){return new $s(this.times,this.values,this.getValueSize(),e)}InterpolantFactoryMethodSmooth(e){return new Js(this.times,this.values,this.getValueSize(),e)}setInterpolation(e){let t;switch(e){case Vr:t=this.InterpolantFactoryMethodDiscrete;break;case Ws:t=this.InterpolantFactoryMethodLinear;break;case Hs:t=this.InterpolantFactoryMethodSmooth;break}if(t===void 0){let n="unsupported interpolation for "+this.ValueTypeName+" keyframe track named "+this.name;if(this.createInterpolant===void 0)if(e!==this.DefaultInterpolation)this.setInterpolation(this.DefaultInterpolation);else throw new Error(n);return console.warn("THREE.KeyframeTrack:",n),this}return this.createInterpolant=t,this}getInterpolation(){switch(this.createInterpolant){case this.InterpolantFactoryMethodDiscrete:return Vr;case this.InterpolantFactoryMethodLinear:return Ws;case this.InterpolantFactoryMethodSmooth:return Hs}}getValueSize(){return this.values.length/this.times.length}shift(e){if(e!==0){let t=this.times;for(let n=0,i=t.length;n!==i;++n)t[n]+=e}return this}scale(e){if(e!==1){let t=this.times;for(let n=0,i=t.length;n!==i;++n)t[n]*=e}return this}trim(e,t){let n=this.times,i=n.length,r=0,s=i-1;for(;r!==i&&n[r]<e;)++r;for(;s!==-1&&n[s]>t;)--s;if(++s,r!==0||s!==i){r>=s&&(s=Math.max(s,1),r=s-1);let o=this.getValueSize();this.times=n.slice(r,s),this.values=this.values.slice(r*o,s*o)}return this}validate(){let e=!0,t=this.getValueSize();t-Math.floor(t)!==0&&(console.error("THREE.KeyframeTrack: Invalid value size in track.",this),e=!1);let n=this.times,i=this.values,r=n.length;r===0&&(console.error("THREE.KeyframeTrack: Track is empty.",this),e=!1);let s=null;for(let o=0;o!==r;o++){let a=n[o];if(typeof a=="number"&&isNaN(a)){console.error("THREE.KeyframeTrack: Time is not a valid number.",this,o,a),e=!1;break}if(s!==null&&s>a){console.error("THREE.KeyframeTrack: Out of order keys.",this,o,a,s),e=!1;break}s=a}if(i!==void 0&&Uf(i))for(let o=0,a=i.length;o!==a;++o){let l=i[o];if(isNaN(l)){console.error("THREE.KeyframeTrack: Value is not a valid number.",this,o,l),e=!1;break}}return e}optimize(){let e=this.times.slice(),t=this.values.slice(),n=this.getValueSize(),i=this.getInterpolation()===Hs,r=e.length-1,s=1;for(let o=1;o<r;++o){let a=!1,l=e[o],h=e[o+1];if(l!==h&&(o!==1||l!==e[0]))if(i)a=!0;else{let f=o*n,d=f-n,u=f+n;for(let m=0;m!==n;++m){let p=t[f+m];if(p!==t[d+m]||p!==t[u+m]){a=!0;break}}}if(a){if(o!==s){e[s]=e[o];let f=o*n,d=s*n;for(let u=0;u!==n;++u)t[d+u]=t[f+u]}++s}}if(r>0){e[s]=e[r];for(let o=r*n,a=s*n,l=0;l!==n;++l)t[a+l]=t[o+l];++s}return s!==e.length?(this.times=e.slice(0,s),this.values=t.slice(0,s*n)):(this.times=e,this.values=t),this}clone(){let e=this.times.slice(),t=this.values.slice(),n=this.constructor,i=new n(this.name,e,t);return i.createInterpolant=this.createInterpolant,i}};Ft.prototype.ValueTypeName="";Ft.prototype.TimeBufferType=Float32Array;Ft.prototype.ValueBufferType=Float32Array;Ft.prototype.DefaultInterpolation=Ws;var Qn=class extends Ft{constructor(e,t,n){super(e,t,n)}};Qn.prototype.ValueTypeName="bool";Qn.prototype.ValueBufferType=Array;Qn.prototype.DefaultInterpolation=Vr;Qn.prototype.InterpolantFactoryMethodLinear=void 0;Qn.prototype.InterpolantFactoryMethodSmooth=void 0;var Qs=class extends Ft{constructor(e,t,n,i){super(e,t,n,i)}};Qs.prototype.ValueTypeName="color";var js=class extends Ft{constructor(e,t,n,i){super(e,t,n,i)}};js.prototype.ValueTypeName="number";var eo=class extends wi{constructor(e,t,n,i){super(e,t,n,i)}interpolate_(e,t,n,i){let r=this.resultBuffer,s=this.sampleValues,o=this.valueSize,a=(n-t)/(i-t),l=e*o;for(let h=l+o;l!==h;l+=4)In.slerpFlat(r,0,s,l-o,s,l,a);return r}},Xr=class extends Ft{constructor(e,t,n,i){super(e,t,n,i)}InterpolantFactoryMethodLinear(e){return new eo(this.times,this.values,this.getValueSize(),e)}};Xr.prototype.ValueTypeName="quaternion";Xr.prototype.InterpolantFactoryMethodSmooth=void 0;var jn=class extends Ft{constructor(e,t,n){super(e,t,n)}};jn.prototype.ValueTypeName="string";jn.prototype.ValueBufferType=Array;jn.prototype.DefaultInterpolation=Vr;jn.prototype.InterpolantFactoryMethodLinear=void 0;jn.prototype.InterpolantFactoryMethodSmooth=void 0;var to=class extends Ft{constructor(e,t,n,i){super(e,t,n,i)}};to.prototype.ValueTypeName="vector";var no=class{constructor(e,t,n){let i=this,r=!1,s=0,o=0,a,l=[];this.onStart=void 0,this.onLoad=e,this.onProgress=t,this.onError=n,this.abortController=new AbortController,this.itemStart=function(h){o++,r===!1&&i.onStart!==void 0&&i.onStart(h,s,o),r=!0},this.itemEnd=function(h){s++,i.onProgress!==void 0&&i.onProgress(h,s,o),s===o&&(r=!1,i.onLoad!==void 0&&i.onLoad())},this.itemError=function(h){i.onError!==void 0&&i.onError(h)},this.resolveURL=function(h){return a?a(h):h},this.setURLModifier=function(h){return a=h,this},this.addHandler=function(h,f){return l.push(h,f),this},this.removeHandler=function(h){let f=l.indexOf(h);return f!==-1&&l.splice(f,2),this},this.getHandler=function(h){for(let f=0,d=l.length;f<d;f+=2){let u=l[f],m=l[f+1];if(u.global&&(u.lastIndex=0),u.test(h))return m}return null},this.abort=function(){return this.abortController.abort(),this.abortController=new AbortController,this}}},gh=new no,io=class{constructor(e){this.manager=e!==void 0?e:gh,this.crossOrigin="anonymous",this.withCredentials=!1,this.path="",this.resourcePath="",this.requestHeader={}}load(){}loadAsync(e,t){let n=this;return new Promise(function(i,r){n.load(e,i,t,r)})}parse(){}setCrossOrigin(e){return this.crossOrigin=e,this}setWithCredentials(e){return this.withCredentials=e,this}setPath(e){return this.path=e,this}setResourcePath(e){return this.resourcePath=e,this}setRequestHeader(e){return this.requestHeader=e,this}abort(){return this}};io.DEFAULT_MATERIAL_NAME="__DEFAULT";var qr=class extends Zs{constructor(e=-1,t=1,n=1,i=-1,r=.1,s=2e3){super(),this.isOrthographicCamera=!0,this.type="OrthographicCamera",this.zoom=1,this.view=null,this.left=e,this.right=t,this.top=n,this.bottom=i,this.near=r,this.far=s,this.updateProjectionMatrix()}copy(e,t){return super.copy(e,t),this.left=e.left,this.right=e.right,this.top=e.top,this.bottom=e.bottom,this.near=e.near,this.far=e.far,this.zoom=e.zoom,this.view=e.view===null?null:Object.assign({},e.view),this}setViewOffset(e,t,n,i,r,s){this.view===null&&(this.view={enabled:!0,fullWidth:1,fullHeight:1,offsetX:0,offsetY:0,width:1,height:1}),this.view.enabled=!0,this.view.fullWidth=e,this.view.fullHeight=t,this.view.offsetX=n,this.view.offsetY=i,this.view.width=r,this.view.height=s,this.updateProjectionMatrix()}clearViewOffset(){this.view!==null&&(this.view.enabled=!1),this.updateProjectionMatrix()}updateProjectionMatrix(){let e=(this.right-this.left)/(2*this.zoom),t=(this.top-this.bottom)/(2*this.zoom),n=(this.right+this.left)/2,i=(this.top+this.bottom)/2,r=n-e,s=n+e,o=i+t,a=i-t;if(this.view!==null&&this.view.enabled){let l=(this.right-this.left)/this.view.fullWidth/this.zoom,h=(this.top-this.bottom)/this.view.fullHeight/this.zoom;r+=l*this.view.offsetX,s=r+l*this.view.width,o-=h*this.view.offsetY,a=o-h*this.view.height}this.projectionMatrix.makeOrthographic(r,s,o,a,this.near,this.far,this.coordinateSystem,this.reversedDepth),this.projectionMatrixInverse.copy(this.projectionMatrix).invert()}toJSON(e){let t=super.toJSON(e);return t.object.zoom=this.zoom,t.object.left=this.left,t.object.right=this.right,t.object.top=this.top,t.object.bottom=this.bottom,t.object.near=this.near,t.object.far=this.far,this.view!==null&&(t.object.view=Object.assign({},this.view)),t}};var el="\\[\\]\\.:\\/",Nf=new RegExp("["+el+"]","g"),tl="[^"+el+"]",Ff="[^"+el.replace("\\.","")+"]",Of=/((?:WC+[\/:])*)/.source.replace("WC",tl),Bf=/(WCOD+)?/.source.replace("WCOD",Ff),zf=/(?:\.(WC+)(?:\[(.+)\])?)?/.source.replace("WC",tl),kf=/\.(WC+)(?:\[(.+)\])?/.source.replace("WC",tl),Vf=new RegExp("^"+Of+Bf+zf+kf+"$"),Hf=["material","materials","bones","map"],Ha=class{constructor(e,t,n){let i=n||ue.parseTrackName(t);this._targetGroup=e,this._bindings=e.subscribe_(t,i)}getValue(e,t){this.bind();let n=this._targetGroup.nCachedObjects_,i=this._bindings[n];i!==void 0&&i.getValue(e,t)}setValue(e,t){let n=this._bindings;for(let i=this._targetGroup.nCachedObjects_,r=n.length;i!==r;++i)n[i].setValue(e,t)}bind(){let e=this._bindings;for(let t=this._targetGroup.nCachedObjects_,n=e.length;t!==n;++t)e[t].bind()}unbind(){let e=this._bindings;for(let t=this._targetGroup.nCachedObjects_,n=e.length;t!==n;++t)e[t].unbind()}},ue=class c{constructor(e,t,n){this.path=t,this.parsedPath=n||c.parseTrackName(t),this.node=c.findNode(e,this.parsedPath.nodeName),this.rootNode=e,this.getValue=this._getValue_unbound,this.setValue=this._setValue_unbound}static create(e,t,n){return e&&e.isAnimationObjectGroup?new c.Composite(e,t,n):new c(e,t,n)}static sanitizeNodeName(e){return e.replace(/\s/g,"_").replace(Nf,"")}static parseTrackName(e){let t=Vf.exec(e);if(t===null)throw new Error("PropertyBinding: Cannot parse trackName: "+e);let n={nodeName:t[2],objectName:t[3],objectIndex:t[4],propertyName:t[5],propertyIndex:t[6]},i=n.nodeName&&n.nodeName.lastIndexOf(".");if(i!==void 0&&i!==-1){let r=n.nodeName.substring(i+1);Hf.indexOf(r)!==-1&&(n.nodeName=n.nodeName.substring(0,i),n.objectName=r)}if(n.propertyName===null||n.propertyName.length===0)throw new Error("PropertyBinding: can not parse propertyName from trackName: "+e);return n}static findNode(e,t){if(t===void 0||t===""||t==="."||t===-1||t===e.name||t===e.uuid)return e;if(e.skeleton){let n=e.skeleton.getBoneByName(t);if(n!==void 0)return n}if(e.children){let n=function(r){for(let s=0;s<r.length;s++){let o=r[s];if(o.name===t||o.uuid===t)return o;let a=n(o.children);if(a)return a}return null},i=n(e.children);if(i)return i}return null}_getValue_unavailable(){}_setValue_unavailable(){}_getValue_direct(e,t){e[t]=this.targetObject[this.propertyName]}_getValue_array(e,t){let n=this.resolvedProperty;for(let i=0,r=n.length;i!==r;++i)e[t++]=n[i]}_getValue_arrayElement(e,t){e[t]=this.resolvedProperty[this.propertyIndex]}_getValue_toArray(e,t){this.resolvedProperty.toArray(e,t)}_setValue_direct(e,t){this.targetObject[this.propertyName]=e[t]}_setValue_direct_setNeedsUpdate(e,t){this.targetObject[this.propertyName]=e[t],this.targetObject.needsUpdate=!0}_setValue_direct_setMatrixWorldNeedsUpdate(e,t){this.targetObject[this.propertyName]=e[t],this.targetObject.matrixWorldNeedsUpdate=!0}_setValue_array(e,t){let n=this.resolvedProperty;for(let i=0,r=n.length;i!==r;++i)n[i]=e[t++]}_setValue_array_setNeedsUpdate(e,t){let n=this.resolvedProperty;for(let i=0,r=n.length;i!==r;++i)n[i]=e[t++];this.targetObject.needsUpdate=!0}_setValue_array_setMatrixWorldNeedsUpdate(e,t){let n=this.resolvedProperty;for(let i=0,r=n.length;i!==r;++i)n[i]=e[t++];this.targetObject.matrixWorldNeedsUpdate=!0}_setValue_arrayElement(e,t){this.resolvedProperty[this.propertyIndex]=e[t]}_setValue_arrayElement_setNeedsUpdate(e,t){this.resolvedProperty[this.propertyIndex]=e[t],this.targetObject.needsUpdate=!0}_setValue_arrayElement_setMatrixWorldNeedsUpdate(e,t){this.resolvedProperty[this.propertyIndex]=e[t],this.targetObject.matrixWorldNeedsUpdate=!0}_setValue_fromArray(e,t){this.resolvedProperty.fromArray(e,t)}_setValue_fromArray_setNeedsUpdate(e,t){this.resolvedProperty.fromArray(e,t),this.targetObject.needsUpdate=!0}_setValue_fromArray_setMatrixWorldNeedsUpdate(e,t){this.resolvedProperty.fromArray(e,t),this.targetObject.matrixWorldNeedsUpdate=!0}_getValue_unbound(e,t){this.bind(),this.getValue(e,t)}_setValue_unbound(e,t){this.bind(),this.setValue(e,t)}bind(){let e=this.node,t=this.parsedPath,n=t.objectName,i=t.propertyName,r=t.propertyIndex;if(e||(e=c.findNode(this.rootNode,t.nodeName),this.node=e),this.getValue=this._getValue_unavailable,this.setValue=this._setValue_unavailable,!e){console.warn("THREE.PropertyBinding: No target node found for track: "+this.path+".");return}if(n){let l=t.objectIndex;switch(n){case"materials":if(!e.material){console.error("THREE.PropertyBinding: Can not bind to material as node does not have a material.",this);return}if(!e.material.materials){console.error("THREE.PropertyBinding: Can not bind to material.materials as node.material does not have a materials array.",this);return}e=e.material.materials;break;case"bones":if(!e.skeleton){console.error("THREE.PropertyBinding: Can not bind to bones as node does not have a skeleton.",this);return}e=e.skeleton.bones;for(let h=0;h<e.length;h++)if(e[h].name===l){l=h;break}break;case"map":if("map"in e){e=e.map;break}if(!e.material){console.error("THREE.PropertyBinding: Can not bind to material as node does not have a material.",this);return}if(!e.material.map){console.error("THREE.PropertyBinding: Can not bind to material.map as node.material does not have a map.",this);return}e=e.material.map;break;default:if(e[n]===void 0){console.error("THREE.PropertyBinding: Can not bind to objectName of node undefined.",this);return}e=e[n]}if(l!==void 0){if(e[l]===void 0){console.error("THREE.PropertyBinding: Trying to bind to objectIndex of objectName, but is undefined.",this,e);return}e=e[l]}}let s=e[i];if(s===void 0){let l=t.nodeName;console.error("THREE.PropertyBinding: Trying to update property for track: "+l+"."+i+" but it wasn't found.",e);return}let o=this.Versioning.None;this.targetObject=e,e.isMaterial===!0?o=this.Versioning.NeedsUpdate:e.isObject3D===!0&&(o=this.Versioning.MatrixWorldNeedsUpdate);let a=this.BindingType.Direct;if(r!==void 0){if(i==="morphTargetInfluences"){if(!e.geometry){console.error("THREE.PropertyBinding: Can not bind to morphTargetInfluences because node does not have a geometry.",this);return}if(!e.geometry.morphAttributes){console.error("THREE.PropertyBinding: Can not bind to morphTargetInfluences because node does not have a geometry.morphAttributes.",this);return}e.morphTargetDictionary[r]!==void 0&&(r=e.morphTargetDictionary[r])}a=this.BindingType.ArrayElement,this.resolvedProperty=s,this.propertyIndex=r}else s.fromArray!==void 0&&s.toArray!==void 0?(a=this.BindingType.HasFromToArray,this.resolvedProperty=s):Array.isArray(s)?(a=this.BindingType.EntireArray,this.resolvedProperty=s):this.propertyName=i;this.getValue=this.GetterByBindingType[a],this.setValue=this.SetterByBindingTypeAndVersioning[a][o]}unbind(){this.node=null,this.getValue=this._getValue_unbound,this.setValue=this._setValue_unbound}};ue.Composite=Ha;ue.prototype.BindingType={Direct:0,EntireArray:1,ArrayElement:2,HasFromToArray:3};ue.prototype.Versioning={None:0,NeedsUpdate:1,MatrixWorldNeedsUpdate:2};ue.prototype.GetterByBindingType=[ue.prototype._getValue_direct,ue.prototype._getValue_array,ue.prototype._getValue_arrayElement,ue.prototype._getValue_toArray];ue.prototype.SetterByBindingTypeAndVersioning=[[ue.prototype._setValue_direct,ue.prototype._setValue_direct_setNeedsUpdate,ue.prototype._setValue_direct_setMatrixWorldNeedsUpdate],[ue.prototype._setValue_array,ue.prototype._setValue_array_setNeedsUpdate,ue.prototype._setValue_array_setMatrixWorldNeedsUpdate],[ue.prototype._setValue_arrayElement,ue.prototype._setValue_arrayElement_setNeedsUpdate,ue.prototype._setValue_arrayElement_setMatrixWorldNeedsUpdate],[ue.prototype._setValue_fromArray,ue.prototype._setValue_fromArray_setNeedsUpdate,ue.prototype._setValue_fromArray_setMatrixWorldNeedsUpdate]];var Om=new Float32Array(1);typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("register",{detail:{revision:"180"}}));typeof window<"u"&&(window.__THREE__?console.warn("WARNING: Multiple instances of Three.js being imported."):window.__THREE__="180");var Gf=`#ifdef USE_ALPHAHASH
	if ( diffuseColor.a < getAlphaHashThreshold( vPosition ) ) discard;
#endif`,Wf=`#ifdef USE_ALPHAHASH
	const float ALPHA_HASH_SCALE = 0.05;
	float hash2D( vec2 value ) {
		return fract( 1.0e4 * sin( 17.0 * value.x + 0.1 * value.y ) * ( 0.1 + abs( sin( 13.0 * value.y + value.x ) ) ) );
	}
	float hash3D( vec3 value ) {
		return hash2D( vec2( hash2D( value.xy ), value.z ) );
	}
	float getAlphaHashThreshold( vec3 position ) {
		float maxDeriv = max(
			length( dFdx( position.xyz ) ),
			length( dFdy( position.xyz ) )
		);
		float pixScale = 1.0 / ( ALPHA_HASH_SCALE * maxDeriv );
		vec2 pixScales = vec2(
			exp2( floor( log2( pixScale ) ) ),
			exp2( ceil( log2( pixScale ) ) )
		);
		vec2 alpha = vec2(
			hash3D( floor( pixScales.x * position.xyz ) ),
			hash3D( floor( pixScales.y * position.xyz ) )
		);
		float lerpFactor = fract( log2( pixScale ) );
		float x = ( 1.0 - lerpFactor ) * alpha.x + lerpFactor * alpha.y;
		float a = min( lerpFactor, 1.0 - lerpFactor );
		vec3 cases = vec3(
			x * x / ( 2.0 * a * ( 1.0 - a ) ),
			( x - 0.5 * a ) / ( 1.0 - a ),
			1.0 - ( ( 1.0 - x ) * ( 1.0 - x ) / ( 2.0 * a * ( 1.0 - a ) ) )
		);
		float threshold = ( x < ( 1.0 - a ) )
			? ( ( x < a ) ? cases.x : cases.y )
			: cases.z;
		return clamp( threshold , 1.0e-6, 1.0 );
	}
#endif`,Xf=`#ifdef USE_ALPHAMAP
	diffuseColor.a *= texture2D( alphaMap, vAlphaMapUv ).g;
#endif`,qf=`#ifdef USE_ALPHAMAP
	uniform sampler2D alphaMap;
#endif`,Yf=`#ifdef USE_ALPHATEST
	#ifdef ALPHA_TO_COVERAGE
	diffuseColor.a = smoothstep( alphaTest, alphaTest + fwidth( diffuseColor.a ), diffuseColor.a );
	if ( diffuseColor.a == 0.0 ) discard;
	#else
	if ( diffuseColor.a < alphaTest ) discard;
	#endif
#endif`,Zf=`#ifdef USE_ALPHATEST
	uniform float alphaTest;
#endif`,Jf=`#ifdef USE_AOMAP
	float ambientOcclusion = ( texture2D( aoMap, vAoMapUv ).r - 1.0 ) * aoMapIntensity + 1.0;
	reflectedLight.indirectDiffuse *= ambientOcclusion;
	#if defined( USE_CLEARCOAT ) 
		clearcoatSpecularIndirect *= ambientOcclusion;
	#endif
	#if defined( USE_SHEEN ) 
		sheenSpecularIndirect *= ambientOcclusion;
	#endif
	#if defined( USE_ENVMAP ) && defined( STANDARD )
		float dotNV = saturate( dot( geometryNormal, geometryViewDir ) );
		reflectedLight.indirectSpecular *= computeSpecularOcclusion( dotNV, ambientOcclusion, material.roughness );
	#endif
#endif`,$f=`#ifdef USE_AOMAP
	uniform sampler2D aoMap;
	uniform float aoMapIntensity;
#endif`,Kf=`#ifdef USE_BATCHING
	#if ! defined( GL_ANGLE_multi_draw )
	#define gl_DrawID _gl_DrawID
	uniform int _gl_DrawID;
	#endif
	uniform highp sampler2D batchingTexture;
	uniform highp usampler2D batchingIdTexture;
	mat4 getBatchingMatrix( const in float i ) {
		int size = textureSize( batchingTexture, 0 ).x;
		int j = int( i ) * 4;
		int x = j % size;
		int y = j / size;
		vec4 v1 = texelFetch( batchingTexture, ivec2( x, y ), 0 );
		vec4 v2 = texelFetch( batchingTexture, ivec2( x + 1, y ), 0 );
		vec4 v3 = texelFetch( batchingTexture, ivec2( x + 2, y ), 0 );
		vec4 v4 = texelFetch( batchingTexture, ivec2( x + 3, y ), 0 );
		return mat4( v1, v2, v3, v4 );
	}
	float getIndirectIndex( const in int i ) {
		int size = textureSize( batchingIdTexture, 0 ).x;
		int x = i % size;
		int y = i / size;
		return float( texelFetch( batchingIdTexture, ivec2( x, y ), 0 ).r );
	}
#endif
#ifdef USE_BATCHING_COLOR
	uniform sampler2D batchingColorTexture;
	vec3 getBatchingColor( const in float i ) {
		int size = textureSize( batchingColorTexture, 0 ).x;
		int j = int( i );
		int x = j % size;
		int y = j / size;
		return texelFetch( batchingColorTexture, ivec2( x, y ), 0 ).rgb;
	}
#endif`,Qf=`#ifdef USE_BATCHING
	mat4 batchingMatrix = getBatchingMatrix( getIndirectIndex( gl_DrawID ) );
#endif`,jf=`vec3 transformed = vec3( position );
#ifdef USE_ALPHAHASH
	vPosition = vec3( position );
#endif`,ed=`vec3 objectNormal = vec3( normal );
#ifdef USE_TANGENT
	vec3 objectTangent = vec3( tangent.xyz );
#endif`,td=`float G_BlinnPhong_Implicit( ) {
	return 0.25;
}
float D_BlinnPhong( const in float shininess, const in float dotNH ) {
	return RECIPROCAL_PI * ( shininess * 0.5 + 1.0 ) * pow( dotNH, shininess );
}
vec3 BRDF_BlinnPhong( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in vec3 specularColor, const in float shininess ) {
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNH = saturate( dot( normal, halfDir ) );
	float dotVH = saturate( dot( viewDir, halfDir ) );
	vec3 F = F_Schlick( specularColor, 1.0, dotVH );
	float G = G_BlinnPhong_Implicit( );
	float D = D_BlinnPhong( shininess, dotNH );
	return F * ( G * D );
} // validated`,nd=`#ifdef USE_IRIDESCENCE
	const mat3 XYZ_TO_REC709 = mat3(
		 3.2404542, -0.9692660,  0.0556434,
		-1.5371385,  1.8760108, -0.2040259,
		-0.4985314,  0.0415560,  1.0572252
	);
	vec3 Fresnel0ToIor( vec3 fresnel0 ) {
		vec3 sqrtF0 = sqrt( fresnel0 );
		return ( vec3( 1.0 ) + sqrtF0 ) / ( vec3( 1.0 ) - sqrtF0 );
	}
	vec3 IorToFresnel0( vec3 transmittedIor, float incidentIor ) {
		return pow2( ( transmittedIor - vec3( incidentIor ) ) / ( transmittedIor + vec3( incidentIor ) ) );
	}
	float IorToFresnel0( float transmittedIor, float incidentIor ) {
		return pow2( ( transmittedIor - incidentIor ) / ( transmittedIor + incidentIor ));
	}
	vec3 evalSensitivity( float OPD, vec3 shift ) {
		float phase = 2.0 * PI * OPD * 1.0e-9;
		vec3 val = vec3( 5.4856e-13, 4.4201e-13, 5.2481e-13 );
		vec3 pos = vec3( 1.6810e+06, 1.7953e+06, 2.2084e+06 );
		vec3 var = vec3( 4.3278e+09, 9.3046e+09, 6.6121e+09 );
		vec3 xyz = val * sqrt( 2.0 * PI * var ) * cos( pos * phase + shift ) * exp( - pow2( phase ) * var );
		xyz.x += 9.7470e-14 * sqrt( 2.0 * PI * 4.5282e+09 ) * cos( 2.2399e+06 * phase + shift[ 0 ] ) * exp( - 4.5282e+09 * pow2( phase ) );
		xyz /= 1.0685e-7;
		vec3 rgb = XYZ_TO_REC709 * xyz;
		return rgb;
	}
	vec3 evalIridescence( float outsideIOR, float eta2, float cosTheta1, float thinFilmThickness, vec3 baseF0 ) {
		vec3 I;
		float iridescenceIOR = mix( outsideIOR, eta2, smoothstep( 0.0, 0.03, thinFilmThickness ) );
		float sinTheta2Sq = pow2( outsideIOR / iridescenceIOR ) * ( 1.0 - pow2( cosTheta1 ) );
		float cosTheta2Sq = 1.0 - sinTheta2Sq;
		if ( cosTheta2Sq < 0.0 ) {
			return vec3( 1.0 );
		}
		float cosTheta2 = sqrt( cosTheta2Sq );
		float R0 = IorToFresnel0( iridescenceIOR, outsideIOR );
		float R12 = F_Schlick( R0, 1.0, cosTheta1 );
		float T121 = 1.0 - R12;
		float phi12 = 0.0;
		if ( iridescenceIOR < outsideIOR ) phi12 = PI;
		float phi21 = PI - phi12;
		vec3 baseIOR = Fresnel0ToIor( clamp( baseF0, 0.0, 0.9999 ) );		vec3 R1 = IorToFresnel0( baseIOR, iridescenceIOR );
		vec3 R23 = F_Schlick( R1, 1.0, cosTheta2 );
		vec3 phi23 = vec3( 0.0 );
		if ( baseIOR[ 0 ] < iridescenceIOR ) phi23[ 0 ] = PI;
		if ( baseIOR[ 1 ] < iridescenceIOR ) phi23[ 1 ] = PI;
		if ( baseIOR[ 2 ] < iridescenceIOR ) phi23[ 2 ] = PI;
		float OPD = 2.0 * iridescenceIOR * thinFilmThickness * cosTheta2;
		vec3 phi = vec3( phi21 ) + phi23;
		vec3 R123 = clamp( R12 * R23, 1e-5, 0.9999 );
		vec3 r123 = sqrt( R123 );
		vec3 Rs = pow2( T121 ) * R23 / ( vec3( 1.0 ) - R123 );
		vec3 C0 = R12 + Rs;
		I = C0;
		vec3 Cm = Rs - T121;
		for ( int m = 1; m <= 2; ++ m ) {
			Cm *= r123;
			vec3 Sm = 2.0 * evalSensitivity( float( m ) * OPD, float( m ) * phi );
			I += Cm * Sm;
		}
		return max( I, vec3( 0.0 ) );
	}
#endif`,id=`#ifdef USE_BUMPMAP
	uniform sampler2D bumpMap;
	uniform float bumpScale;
	vec2 dHdxy_fwd() {
		vec2 dSTdx = dFdx( vBumpMapUv );
		vec2 dSTdy = dFdy( vBumpMapUv );
		float Hll = bumpScale * texture2D( bumpMap, vBumpMapUv ).x;
		float dBx = bumpScale * texture2D( bumpMap, vBumpMapUv + dSTdx ).x - Hll;
		float dBy = bumpScale * texture2D( bumpMap, vBumpMapUv + dSTdy ).x - Hll;
		return vec2( dBx, dBy );
	}
	vec3 perturbNormalArb( vec3 surf_pos, vec3 surf_norm, vec2 dHdxy, float faceDirection ) {
		vec3 vSigmaX = normalize( dFdx( surf_pos.xyz ) );
		vec3 vSigmaY = normalize( dFdy( surf_pos.xyz ) );
		vec3 vN = surf_norm;
		vec3 R1 = cross( vSigmaY, vN );
		vec3 R2 = cross( vN, vSigmaX );
		float fDet = dot( vSigmaX, R1 ) * faceDirection;
		vec3 vGrad = sign( fDet ) * ( dHdxy.x * R1 + dHdxy.y * R2 );
		return normalize( abs( fDet ) * surf_norm - vGrad );
	}
#endif`,rd=`#if NUM_CLIPPING_PLANES > 0
	vec4 plane;
	#ifdef ALPHA_TO_COVERAGE
		float distanceToPlane, distanceGradient;
		float clipOpacity = 1.0;
		#pragma unroll_loop_start
		for ( int i = 0; i < UNION_CLIPPING_PLANES; i ++ ) {
			plane = clippingPlanes[ i ];
			distanceToPlane = - dot( vClipPosition, plane.xyz ) + plane.w;
			distanceGradient = fwidth( distanceToPlane ) / 2.0;
			clipOpacity *= smoothstep( - distanceGradient, distanceGradient, distanceToPlane );
			if ( clipOpacity == 0.0 ) discard;
		}
		#pragma unroll_loop_end
		#if UNION_CLIPPING_PLANES < NUM_CLIPPING_PLANES
			float unionClipOpacity = 1.0;
			#pragma unroll_loop_start
			for ( int i = UNION_CLIPPING_PLANES; i < NUM_CLIPPING_PLANES; i ++ ) {
				plane = clippingPlanes[ i ];
				distanceToPlane = - dot( vClipPosition, plane.xyz ) + plane.w;
				distanceGradient = fwidth( distanceToPlane ) / 2.0;
				unionClipOpacity *= 1.0 - smoothstep( - distanceGradient, distanceGradient, distanceToPlane );
			}
			#pragma unroll_loop_end
			clipOpacity *= 1.0 - unionClipOpacity;
		#endif
		diffuseColor.a *= clipOpacity;
		if ( diffuseColor.a == 0.0 ) discard;
	#else
		#pragma unroll_loop_start
		for ( int i = 0; i < UNION_CLIPPING_PLANES; i ++ ) {
			plane = clippingPlanes[ i ];
			if ( dot( vClipPosition, plane.xyz ) > plane.w ) discard;
		}
		#pragma unroll_loop_end
		#if UNION_CLIPPING_PLANES < NUM_CLIPPING_PLANES
			bool clipped = true;
			#pragma unroll_loop_start
			for ( int i = UNION_CLIPPING_PLANES; i < NUM_CLIPPING_PLANES; i ++ ) {
				plane = clippingPlanes[ i ];
				clipped = ( dot( vClipPosition, plane.xyz ) > plane.w ) && clipped;
			}
			#pragma unroll_loop_end
			if ( clipped ) discard;
		#endif
	#endif
#endif`,sd=`#if NUM_CLIPPING_PLANES > 0
	varying vec3 vClipPosition;
	uniform vec4 clippingPlanes[ NUM_CLIPPING_PLANES ];
#endif`,od=`#if NUM_CLIPPING_PLANES > 0
	varying vec3 vClipPosition;
#endif`,ad=`#if NUM_CLIPPING_PLANES > 0
	vClipPosition = - mvPosition.xyz;
#endif`,ld=`#if defined( USE_COLOR_ALPHA )
	diffuseColor *= vColor;
#elif defined( USE_COLOR )
	diffuseColor.rgb *= vColor;
#endif`,cd=`#if defined( USE_COLOR_ALPHA )
	varying vec4 vColor;
#elif defined( USE_COLOR )
	varying vec3 vColor;
#endif`,hd=`#if defined( USE_COLOR_ALPHA )
	varying vec4 vColor;
#elif defined( USE_COLOR ) || defined( USE_INSTANCING_COLOR ) || defined( USE_BATCHING_COLOR )
	varying vec3 vColor;
#endif`,ud=`#if defined( USE_COLOR_ALPHA )
	vColor = vec4( 1.0 );
#elif defined( USE_COLOR ) || defined( USE_INSTANCING_COLOR ) || defined( USE_BATCHING_COLOR )
	vColor = vec3( 1.0 );
#endif
#ifdef USE_COLOR
	vColor *= color;
#endif
#ifdef USE_INSTANCING_COLOR
	vColor.xyz *= instanceColor.xyz;
#endif
#ifdef USE_BATCHING_COLOR
	vec3 batchingColor = getBatchingColor( getIndirectIndex( gl_DrawID ) );
	vColor.xyz *= batchingColor.xyz;
#endif`,fd=`#define PI 3.141592653589793
#define PI2 6.283185307179586
#define PI_HALF 1.5707963267948966
#define RECIPROCAL_PI 0.3183098861837907
#define RECIPROCAL_PI2 0.15915494309189535
#define EPSILON 1e-6
#ifndef saturate
#define saturate( a ) clamp( a, 0.0, 1.0 )
#endif
#define whiteComplement( a ) ( 1.0 - saturate( a ) )
float pow2( const in float x ) { return x*x; }
vec3 pow2( const in vec3 x ) { return x*x; }
float pow3( const in float x ) { return x*x*x; }
float pow4( const in float x ) { float x2 = x*x; return x2*x2; }
float max3( const in vec3 v ) { return max( max( v.x, v.y ), v.z ); }
float average( const in vec3 v ) { return dot( v, vec3( 0.3333333 ) ); }
highp float rand( const in vec2 uv ) {
	const highp float a = 12.9898, b = 78.233, c = 43758.5453;
	highp float dt = dot( uv.xy, vec2( a,b ) ), sn = mod( dt, PI );
	return fract( sin( sn ) * c );
}
#ifdef HIGH_PRECISION
	float precisionSafeLength( vec3 v ) { return length( v ); }
#else
	float precisionSafeLength( vec3 v ) {
		float maxComponent = max3( abs( v ) );
		return length( v / maxComponent ) * maxComponent;
	}
#endif
struct IncidentLight {
	vec3 color;
	vec3 direction;
	bool visible;
};
struct ReflectedLight {
	vec3 directDiffuse;
	vec3 directSpecular;
	vec3 indirectDiffuse;
	vec3 indirectSpecular;
};
#ifdef USE_ALPHAHASH
	varying vec3 vPosition;
#endif
vec3 transformDirection( in vec3 dir, in mat4 matrix ) {
	return normalize( ( matrix * vec4( dir, 0.0 ) ).xyz );
}
vec3 inverseTransformDirection( in vec3 dir, in mat4 matrix ) {
	return normalize( ( vec4( dir, 0.0 ) * matrix ).xyz );
}
mat3 transposeMat3( const in mat3 m ) {
	mat3 tmp;
	tmp[ 0 ] = vec3( m[ 0 ].x, m[ 1 ].x, m[ 2 ].x );
	tmp[ 1 ] = vec3( m[ 0 ].y, m[ 1 ].y, m[ 2 ].y );
	tmp[ 2 ] = vec3( m[ 0 ].z, m[ 1 ].z, m[ 2 ].z );
	return tmp;
}
bool isPerspectiveMatrix( mat4 m ) {
	return m[ 2 ][ 3 ] == - 1.0;
}
vec2 equirectUv( in vec3 dir ) {
	float u = atan( dir.z, dir.x ) * RECIPROCAL_PI2 + 0.5;
	float v = asin( clamp( dir.y, - 1.0, 1.0 ) ) * RECIPROCAL_PI + 0.5;
	return vec2( u, v );
}
vec3 BRDF_Lambert( const in vec3 diffuseColor ) {
	return RECIPROCAL_PI * diffuseColor;
}
vec3 F_Schlick( const in vec3 f0, const in float f90, const in float dotVH ) {
	float fresnel = exp2( ( - 5.55473 * dotVH - 6.98316 ) * dotVH );
	return f0 * ( 1.0 - fresnel ) + ( f90 * fresnel );
}
float F_Schlick( const in float f0, const in float f90, const in float dotVH ) {
	float fresnel = exp2( ( - 5.55473 * dotVH - 6.98316 ) * dotVH );
	return f0 * ( 1.0 - fresnel ) + ( f90 * fresnel );
} // validated`,dd=`#ifdef ENVMAP_TYPE_CUBE_UV
	#define cubeUV_minMipLevel 4.0
	#define cubeUV_minTileSize 16.0
	float getFace( vec3 direction ) {
		vec3 absDirection = abs( direction );
		float face = - 1.0;
		if ( absDirection.x > absDirection.z ) {
			if ( absDirection.x > absDirection.y )
				face = direction.x > 0.0 ? 0.0 : 3.0;
			else
				face = direction.y > 0.0 ? 1.0 : 4.0;
		} else {
			if ( absDirection.z > absDirection.y )
				face = direction.z > 0.0 ? 2.0 : 5.0;
			else
				face = direction.y > 0.0 ? 1.0 : 4.0;
		}
		return face;
	}
	vec2 getUV( vec3 direction, float face ) {
		vec2 uv;
		if ( face == 0.0 ) {
			uv = vec2( direction.z, direction.y ) / abs( direction.x );
		} else if ( face == 1.0 ) {
			uv = vec2( - direction.x, - direction.z ) / abs( direction.y );
		} else if ( face == 2.0 ) {
			uv = vec2( - direction.x, direction.y ) / abs( direction.z );
		} else if ( face == 3.0 ) {
			uv = vec2( - direction.z, direction.y ) / abs( direction.x );
		} else if ( face == 4.0 ) {
			uv = vec2( - direction.x, direction.z ) / abs( direction.y );
		} else {
			uv = vec2( direction.x, direction.y ) / abs( direction.z );
		}
		return 0.5 * ( uv + 1.0 );
	}
	vec3 bilinearCubeUV( sampler2D envMap, vec3 direction, float mipInt ) {
		float face = getFace( direction );
		float filterInt = max( cubeUV_minMipLevel - mipInt, 0.0 );
		mipInt = max( mipInt, cubeUV_minMipLevel );
		float faceSize = exp2( mipInt );
		highp vec2 uv = getUV( direction, face ) * ( faceSize - 2.0 ) + 1.0;
		if ( face > 2.0 ) {
			uv.y += faceSize;
			face -= 3.0;
		}
		uv.x += face * faceSize;
		uv.x += filterInt * 3.0 * cubeUV_minTileSize;
		uv.y += 4.0 * ( exp2( CUBEUV_MAX_MIP ) - faceSize );
		uv.x *= CUBEUV_TEXEL_WIDTH;
		uv.y *= CUBEUV_TEXEL_HEIGHT;
		#ifdef texture2DGradEXT
			return texture2DGradEXT( envMap, uv, vec2( 0.0 ), vec2( 0.0 ) ).rgb;
		#else
			return texture2D( envMap, uv ).rgb;
		#endif
	}
	#define cubeUV_r0 1.0
	#define cubeUV_m0 - 2.0
	#define cubeUV_r1 0.8
	#define cubeUV_m1 - 1.0
	#define cubeUV_r4 0.4
	#define cubeUV_m4 2.0
	#define cubeUV_r5 0.305
	#define cubeUV_m5 3.0
	#define cubeUV_r6 0.21
	#define cubeUV_m6 4.0
	float roughnessToMip( float roughness ) {
		float mip = 0.0;
		if ( roughness >= cubeUV_r1 ) {
			mip = ( cubeUV_r0 - roughness ) * ( cubeUV_m1 - cubeUV_m0 ) / ( cubeUV_r0 - cubeUV_r1 ) + cubeUV_m0;
		} else if ( roughness >= cubeUV_r4 ) {
			mip = ( cubeUV_r1 - roughness ) * ( cubeUV_m4 - cubeUV_m1 ) / ( cubeUV_r1 - cubeUV_r4 ) + cubeUV_m1;
		} else if ( roughness >= cubeUV_r5 ) {
			mip = ( cubeUV_r4 - roughness ) * ( cubeUV_m5 - cubeUV_m4 ) / ( cubeUV_r4 - cubeUV_r5 ) + cubeUV_m4;
		} else if ( roughness >= cubeUV_r6 ) {
			mip = ( cubeUV_r5 - roughness ) * ( cubeUV_m6 - cubeUV_m5 ) / ( cubeUV_r5 - cubeUV_r6 ) + cubeUV_m5;
		} else {
			mip = - 2.0 * log2( 1.16 * roughness );		}
		return mip;
	}
	vec4 textureCubeUV( sampler2D envMap, vec3 sampleDir, float roughness ) {
		float mip = clamp( roughnessToMip( roughness ), cubeUV_m0, CUBEUV_MAX_MIP );
		float mipF = fract( mip );
		float mipInt = floor( mip );
		vec3 color0 = bilinearCubeUV( envMap, sampleDir, mipInt );
		if ( mipF == 0.0 ) {
			return vec4( color0, 1.0 );
		} else {
			vec3 color1 = bilinearCubeUV( envMap, sampleDir, mipInt + 1.0 );
			return vec4( mix( color0, color1, mipF ), 1.0 );
		}
	}
#endif`,pd=`vec3 transformedNormal = objectNormal;
#ifdef USE_TANGENT
	vec3 transformedTangent = objectTangent;
#endif
#ifdef USE_BATCHING
	mat3 bm = mat3( batchingMatrix );
	transformedNormal /= vec3( dot( bm[ 0 ], bm[ 0 ] ), dot( bm[ 1 ], bm[ 1 ] ), dot( bm[ 2 ], bm[ 2 ] ) );
	transformedNormal = bm * transformedNormal;
	#ifdef USE_TANGENT
		transformedTangent = bm * transformedTangent;
	#endif
#endif
#ifdef USE_INSTANCING
	mat3 im = mat3( instanceMatrix );
	transformedNormal /= vec3( dot( im[ 0 ], im[ 0 ] ), dot( im[ 1 ], im[ 1 ] ), dot( im[ 2 ], im[ 2 ] ) );
	transformedNormal = im * transformedNormal;
	#ifdef USE_TANGENT
		transformedTangent = im * transformedTangent;
	#endif
#endif
transformedNormal = normalMatrix * transformedNormal;
#ifdef FLIP_SIDED
	transformedNormal = - transformedNormal;
#endif
#ifdef USE_TANGENT
	transformedTangent = ( modelViewMatrix * vec4( transformedTangent, 0.0 ) ).xyz;
	#ifdef FLIP_SIDED
		transformedTangent = - transformedTangent;
	#endif
#endif`,md=`#ifdef USE_DISPLACEMENTMAP
	uniform sampler2D displacementMap;
	uniform float displacementScale;
	uniform float displacementBias;
#endif`,gd=`#ifdef USE_DISPLACEMENTMAP
	transformed += normalize( objectNormal ) * ( texture2D( displacementMap, vDisplacementMapUv ).x * displacementScale + displacementBias );
#endif`,_d=`#ifdef USE_EMISSIVEMAP
	vec4 emissiveColor = texture2D( emissiveMap, vEmissiveMapUv );
	#ifdef DECODE_VIDEO_TEXTURE_EMISSIVE
		emissiveColor = sRGBTransferEOTF( emissiveColor );
	#endif
	totalEmissiveRadiance *= emissiveColor.rgb;
#endif`,xd=`#ifdef USE_EMISSIVEMAP
	uniform sampler2D emissiveMap;
#endif`,vd="gl_FragColor = linearToOutputTexel( gl_FragColor );",yd=`vec4 LinearTransferOETF( in vec4 value ) {
	return value;
}
vec4 sRGBTransferEOTF( in vec4 value ) {
	return vec4( mix( pow( value.rgb * 0.9478672986 + vec3( 0.0521327014 ), vec3( 2.4 ) ), value.rgb * 0.0773993808, vec3( lessThanEqual( value.rgb, vec3( 0.04045 ) ) ) ), value.a );
}
vec4 sRGBTransferOETF( in vec4 value ) {
	return vec4( mix( pow( value.rgb, vec3( 0.41666 ) ) * 1.055 - vec3( 0.055 ), value.rgb * 12.92, vec3( lessThanEqual( value.rgb, vec3( 0.0031308 ) ) ) ), value.a );
}`,Md=`#ifdef USE_ENVMAP
	#ifdef ENV_WORLDPOS
		vec3 cameraToFrag;
		if ( isOrthographic ) {
			cameraToFrag = normalize( vec3( - viewMatrix[ 0 ][ 2 ], - viewMatrix[ 1 ][ 2 ], - viewMatrix[ 2 ][ 2 ] ) );
		} else {
			cameraToFrag = normalize( vWorldPosition - cameraPosition );
		}
		vec3 worldNormal = inverseTransformDirection( normal, viewMatrix );
		#ifdef ENVMAP_MODE_REFLECTION
			vec3 reflectVec = reflect( cameraToFrag, worldNormal );
		#else
			vec3 reflectVec = refract( cameraToFrag, worldNormal, refractionRatio );
		#endif
	#else
		vec3 reflectVec = vReflect;
	#endif
	#ifdef ENVMAP_TYPE_CUBE
		vec4 envColor = textureCube( envMap, envMapRotation * vec3( flipEnvMap * reflectVec.x, reflectVec.yz ) );
	#else
		vec4 envColor = vec4( 0.0 );
	#endif
	#ifdef ENVMAP_BLENDING_MULTIPLY
		outgoingLight = mix( outgoingLight, outgoingLight * envColor.xyz, specularStrength * reflectivity );
	#elif defined( ENVMAP_BLENDING_MIX )
		outgoingLight = mix( outgoingLight, envColor.xyz, specularStrength * reflectivity );
	#elif defined( ENVMAP_BLENDING_ADD )
		outgoingLight += envColor.xyz * specularStrength * reflectivity;
	#endif
#endif`,Sd=`#ifdef USE_ENVMAP
	uniform float envMapIntensity;
	uniform float flipEnvMap;
	uniform mat3 envMapRotation;
	#ifdef ENVMAP_TYPE_CUBE
		uniform samplerCube envMap;
	#else
		uniform sampler2D envMap;
	#endif
	
#endif`,bd=`#ifdef USE_ENVMAP
	uniform float reflectivity;
	#if defined( USE_BUMPMAP ) || defined( USE_NORMALMAP ) || defined( PHONG ) || defined( LAMBERT )
		#define ENV_WORLDPOS
	#endif
	#ifdef ENV_WORLDPOS
		varying vec3 vWorldPosition;
		uniform float refractionRatio;
	#else
		varying vec3 vReflect;
	#endif
#endif`,Td=`#ifdef USE_ENVMAP
	#if defined( USE_BUMPMAP ) || defined( USE_NORMALMAP ) || defined( PHONG ) || defined( LAMBERT )
		#define ENV_WORLDPOS
	#endif
	#ifdef ENV_WORLDPOS
		
		varying vec3 vWorldPosition;
	#else
		varying vec3 vReflect;
		uniform float refractionRatio;
	#endif
#endif`,wd=`#ifdef USE_ENVMAP
	#ifdef ENV_WORLDPOS
		vWorldPosition = worldPosition.xyz;
	#else
		vec3 cameraToVertex;
		if ( isOrthographic ) {
			cameraToVertex = normalize( vec3( - viewMatrix[ 0 ][ 2 ], - viewMatrix[ 1 ][ 2 ], - viewMatrix[ 2 ][ 2 ] ) );
		} else {
			cameraToVertex = normalize( worldPosition.xyz - cameraPosition );
		}
		vec3 worldNormal = inverseTransformDirection( transformedNormal, viewMatrix );
		#ifdef ENVMAP_MODE_REFLECTION
			vReflect = reflect( cameraToVertex, worldNormal );
		#else
			vReflect = refract( cameraToVertex, worldNormal, refractionRatio );
		#endif
	#endif
#endif`,Ed=`#ifdef USE_FOG
	vFogDepth = - mvPosition.z;
#endif`,Ad=`#ifdef USE_FOG
	varying float vFogDepth;
#endif`,Cd=`#ifdef USE_FOG
	#ifdef FOG_EXP2
		float fogFactor = 1.0 - exp( - fogDensity * fogDensity * vFogDepth * vFogDepth );
	#else
		float fogFactor = smoothstep( fogNear, fogFar, vFogDepth );
	#endif
	gl_FragColor.rgb = mix( gl_FragColor.rgb, fogColor, fogFactor );
#endif`,Rd=`#ifdef USE_FOG
	uniform vec3 fogColor;
	varying float vFogDepth;
	#ifdef FOG_EXP2
		uniform float fogDensity;
	#else
		uniform float fogNear;
		uniform float fogFar;
	#endif
#endif`,Pd=`#ifdef USE_GRADIENTMAP
	uniform sampler2D gradientMap;
#endif
vec3 getGradientIrradiance( vec3 normal, vec3 lightDirection ) {
	float dotNL = dot( normal, lightDirection );
	vec2 coord = vec2( dotNL * 0.5 + 0.5, 0.0 );
	#ifdef USE_GRADIENTMAP
		return vec3( texture2D( gradientMap, coord ).r );
	#else
		vec2 fw = fwidth( coord ) * 0.5;
		return mix( vec3( 0.7 ), vec3( 1.0 ), smoothstep( 0.7 - fw.x, 0.7 + fw.x, coord.x ) );
	#endif
}`,Id=`#ifdef USE_LIGHTMAP
	uniform sampler2D lightMap;
	uniform float lightMapIntensity;
#endif`,Dd=`LambertMaterial material;
material.diffuseColor = diffuseColor.rgb;
material.specularStrength = specularStrength;`,Ld=`varying vec3 vViewPosition;
struct LambertMaterial {
	vec3 diffuseColor;
	float specularStrength;
};
void RE_Direct_Lambert( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in LambertMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Lambert( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in LambertMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_Lambert
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Lambert`,Ud=`uniform bool receiveShadow;
uniform vec3 ambientLightColor;
#if defined( USE_LIGHT_PROBES )
	uniform vec3 lightProbe[ 9 ];
#endif
vec3 shGetIrradianceAt( in vec3 normal, in vec3 shCoefficients[ 9 ] ) {
	float x = normal.x, y = normal.y, z = normal.z;
	vec3 result = shCoefficients[ 0 ] * 0.886227;
	result += shCoefficients[ 1 ] * 2.0 * 0.511664 * y;
	result += shCoefficients[ 2 ] * 2.0 * 0.511664 * z;
	result += shCoefficients[ 3 ] * 2.0 * 0.511664 * x;
	result += shCoefficients[ 4 ] * 2.0 * 0.429043 * x * y;
	result += shCoefficients[ 5 ] * 2.0 * 0.429043 * y * z;
	result += shCoefficients[ 6 ] * ( 0.743125 * z * z - 0.247708 );
	result += shCoefficients[ 7 ] * 2.0 * 0.429043 * x * z;
	result += shCoefficients[ 8 ] * 0.429043 * ( x * x - y * y );
	return result;
}
vec3 getLightProbeIrradiance( const in vec3 lightProbe[ 9 ], const in vec3 normal ) {
	vec3 worldNormal = inverseTransformDirection( normal, viewMatrix );
	vec3 irradiance = shGetIrradianceAt( worldNormal, lightProbe );
	return irradiance;
}
vec3 getAmbientLightIrradiance( const in vec3 ambientLightColor ) {
	vec3 irradiance = ambientLightColor;
	return irradiance;
}
float getDistanceAttenuation( const in float lightDistance, const in float cutoffDistance, const in float decayExponent ) {
	float distanceFalloff = 1.0 / max( pow( lightDistance, decayExponent ), 0.01 );
	if ( cutoffDistance > 0.0 ) {
		distanceFalloff *= pow2( saturate( 1.0 - pow4( lightDistance / cutoffDistance ) ) );
	}
	return distanceFalloff;
}
float getSpotAttenuation( const in float coneCosine, const in float penumbraCosine, const in float angleCosine ) {
	return smoothstep( coneCosine, penumbraCosine, angleCosine );
}
#if NUM_DIR_LIGHTS > 0
	struct DirectionalLight {
		vec3 direction;
		vec3 color;
	};
	uniform DirectionalLight directionalLights[ NUM_DIR_LIGHTS ];
	void getDirectionalLightInfo( const in DirectionalLight directionalLight, out IncidentLight light ) {
		light.color = directionalLight.color;
		light.direction = directionalLight.direction;
		light.visible = true;
	}
#endif
#if NUM_POINT_LIGHTS > 0
	struct PointLight {
		vec3 position;
		vec3 color;
		float distance;
		float decay;
	};
	uniform PointLight pointLights[ NUM_POINT_LIGHTS ];
	void getPointLightInfo( const in PointLight pointLight, const in vec3 geometryPosition, out IncidentLight light ) {
		vec3 lVector = pointLight.position - geometryPosition;
		light.direction = normalize( lVector );
		float lightDistance = length( lVector );
		light.color = pointLight.color;
		light.color *= getDistanceAttenuation( lightDistance, pointLight.distance, pointLight.decay );
		light.visible = ( light.color != vec3( 0.0 ) );
	}
#endif
#if NUM_SPOT_LIGHTS > 0
	struct SpotLight {
		vec3 position;
		vec3 direction;
		vec3 color;
		float distance;
		float decay;
		float coneCos;
		float penumbraCos;
	};
	uniform SpotLight spotLights[ NUM_SPOT_LIGHTS ];
	void getSpotLightInfo( const in SpotLight spotLight, const in vec3 geometryPosition, out IncidentLight light ) {
		vec3 lVector = spotLight.position - geometryPosition;
		light.direction = normalize( lVector );
		float angleCos = dot( light.direction, spotLight.direction );
		float spotAttenuation = getSpotAttenuation( spotLight.coneCos, spotLight.penumbraCos, angleCos );
		if ( spotAttenuation > 0.0 ) {
			float lightDistance = length( lVector );
			light.color = spotLight.color * spotAttenuation;
			light.color *= getDistanceAttenuation( lightDistance, spotLight.distance, spotLight.decay );
			light.visible = ( light.color != vec3( 0.0 ) );
		} else {
			light.color = vec3( 0.0 );
			light.visible = false;
		}
	}
#endif
#if NUM_RECT_AREA_LIGHTS > 0
	struct RectAreaLight {
		vec3 color;
		vec3 position;
		vec3 halfWidth;
		vec3 halfHeight;
	};
	uniform sampler2D ltc_1;	uniform sampler2D ltc_2;
	uniform RectAreaLight rectAreaLights[ NUM_RECT_AREA_LIGHTS ];
#endif
#if NUM_HEMI_LIGHTS > 0
	struct HemisphereLight {
		vec3 direction;
		vec3 skyColor;
		vec3 groundColor;
	};
	uniform HemisphereLight hemisphereLights[ NUM_HEMI_LIGHTS ];
	vec3 getHemisphereLightIrradiance( const in HemisphereLight hemiLight, const in vec3 normal ) {
		float dotNL = dot( normal, hemiLight.direction );
		float hemiDiffuseWeight = 0.5 * dotNL + 0.5;
		vec3 irradiance = mix( hemiLight.groundColor, hemiLight.skyColor, hemiDiffuseWeight );
		return irradiance;
	}
#endif`,Nd=`#ifdef USE_ENVMAP
	vec3 getIBLIrradiance( const in vec3 normal ) {
		#ifdef ENVMAP_TYPE_CUBE_UV
			vec3 worldNormal = inverseTransformDirection( normal, viewMatrix );
			vec4 envMapColor = textureCubeUV( envMap, envMapRotation * worldNormal, 1.0 );
			return PI * envMapColor.rgb * envMapIntensity;
		#else
			return vec3( 0.0 );
		#endif
	}
	vec3 getIBLRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness ) {
		#ifdef ENVMAP_TYPE_CUBE_UV
			vec3 reflectVec = reflect( - viewDir, normal );
			reflectVec = normalize( mix( reflectVec, normal, roughness * roughness) );
			reflectVec = inverseTransformDirection( reflectVec, viewMatrix );
			vec4 envMapColor = textureCubeUV( envMap, envMapRotation * reflectVec, roughness );
			return envMapColor.rgb * envMapIntensity;
		#else
			return vec3( 0.0 );
		#endif
	}
	#ifdef USE_ANISOTROPY
		vec3 getIBLAnisotropyRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness, const in vec3 bitangent, const in float anisotropy ) {
			#ifdef ENVMAP_TYPE_CUBE_UV
				vec3 bentNormal = cross( bitangent, viewDir );
				bentNormal = normalize( cross( bentNormal, bitangent ) );
				bentNormal = normalize( mix( bentNormal, normal, pow2( pow2( 1.0 - anisotropy * ( 1.0 - roughness ) ) ) ) );
				return getIBLRadiance( viewDir, bentNormal, roughness );
			#else
				return vec3( 0.0 );
			#endif
		}
	#endif
#endif`,Fd=`ToonMaterial material;
material.diffuseColor = diffuseColor.rgb;`,Od=`varying vec3 vViewPosition;
struct ToonMaterial {
	vec3 diffuseColor;
};
void RE_Direct_Toon( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
	vec3 irradiance = getGradientIrradiance( geometryNormal, directLight.direction ) * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Toon( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_Toon
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Toon`,Bd=`BlinnPhongMaterial material;
material.diffuseColor = diffuseColor.rgb;
material.specularColor = specular;
material.specularShininess = shininess;
material.specularStrength = specularStrength;`,zd=`varying vec3 vViewPosition;
struct BlinnPhongMaterial {
	vec3 diffuseColor;
	vec3 specularColor;
	float specularShininess;
	float specularStrength;
};
void RE_Direct_BlinnPhong( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in BlinnPhongMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
	reflectedLight.directSpecular += irradiance * BRDF_BlinnPhong( directLight.direction, geometryViewDir, geometryNormal, material.specularColor, material.specularShininess ) * material.specularStrength;
}
void RE_IndirectDiffuse_BlinnPhong( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in BlinnPhongMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_BlinnPhong
#define RE_IndirectDiffuse		RE_IndirectDiffuse_BlinnPhong`,kd=`PhysicalMaterial material;
material.diffuseColor = diffuseColor.rgb * ( 1.0 - metalnessFactor );
vec3 dxy = max( abs( dFdx( nonPerturbedNormal ) ), abs( dFdy( nonPerturbedNormal ) ) );
float geometryRoughness = max( max( dxy.x, dxy.y ), dxy.z );
material.roughness = max( roughnessFactor, 0.0525 );material.roughness += geometryRoughness;
material.roughness = min( material.roughness, 1.0 );
#ifdef IOR
	material.ior = ior;
	#ifdef USE_SPECULAR
		float specularIntensityFactor = specularIntensity;
		vec3 specularColorFactor = specularColor;
		#ifdef USE_SPECULAR_COLORMAP
			specularColorFactor *= texture2D( specularColorMap, vSpecularColorMapUv ).rgb;
		#endif
		#ifdef USE_SPECULAR_INTENSITYMAP
			specularIntensityFactor *= texture2D( specularIntensityMap, vSpecularIntensityMapUv ).a;
		#endif
		material.specularF90 = mix( specularIntensityFactor, 1.0, metalnessFactor );
	#else
		float specularIntensityFactor = 1.0;
		vec3 specularColorFactor = vec3( 1.0 );
		material.specularF90 = 1.0;
	#endif
	material.specularColor = mix( min( pow2( ( material.ior - 1.0 ) / ( material.ior + 1.0 ) ) * specularColorFactor, vec3( 1.0 ) ) * specularIntensityFactor, diffuseColor.rgb, metalnessFactor );
#else
	material.specularColor = mix( vec3( 0.04 ), diffuseColor.rgb, metalnessFactor );
	material.specularF90 = 1.0;
#endif
#ifdef USE_CLEARCOAT
	material.clearcoat = clearcoat;
	material.clearcoatRoughness = clearcoatRoughness;
	material.clearcoatF0 = vec3( 0.04 );
	material.clearcoatF90 = 1.0;
	#ifdef USE_CLEARCOATMAP
		material.clearcoat *= texture2D( clearcoatMap, vClearcoatMapUv ).x;
	#endif
	#ifdef USE_CLEARCOAT_ROUGHNESSMAP
		material.clearcoatRoughness *= texture2D( clearcoatRoughnessMap, vClearcoatRoughnessMapUv ).y;
	#endif
	material.clearcoat = saturate( material.clearcoat );	material.clearcoatRoughness = max( material.clearcoatRoughness, 0.0525 );
	material.clearcoatRoughness += geometryRoughness;
	material.clearcoatRoughness = min( material.clearcoatRoughness, 1.0 );
#endif
#ifdef USE_DISPERSION
	material.dispersion = dispersion;
#endif
#ifdef USE_IRIDESCENCE
	material.iridescence = iridescence;
	material.iridescenceIOR = iridescenceIOR;
	#ifdef USE_IRIDESCENCEMAP
		material.iridescence *= texture2D( iridescenceMap, vIridescenceMapUv ).r;
	#endif
	#ifdef USE_IRIDESCENCE_THICKNESSMAP
		material.iridescenceThickness = (iridescenceThicknessMaximum - iridescenceThicknessMinimum) * texture2D( iridescenceThicknessMap, vIridescenceThicknessMapUv ).g + iridescenceThicknessMinimum;
	#else
		material.iridescenceThickness = iridescenceThicknessMaximum;
	#endif
#endif
#ifdef USE_SHEEN
	material.sheenColor = sheenColor;
	#ifdef USE_SHEEN_COLORMAP
		material.sheenColor *= texture2D( sheenColorMap, vSheenColorMapUv ).rgb;
	#endif
	material.sheenRoughness = clamp( sheenRoughness, 0.07, 1.0 );
	#ifdef USE_SHEEN_ROUGHNESSMAP
		material.sheenRoughness *= texture2D( sheenRoughnessMap, vSheenRoughnessMapUv ).a;
	#endif
#endif
#ifdef USE_ANISOTROPY
	#ifdef USE_ANISOTROPYMAP
		mat2 anisotropyMat = mat2( anisotropyVector.x, anisotropyVector.y, - anisotropyVector.y, anisotropyVector.x );
		vec3 anisotropyPolar = texture2D( anisotropyMap, vAnisotropyMapUv ).rgb;
		vec2 anisotropyV = anisotropyMat * normalize( 2.0 * anisotropyPolar.rg - vec2( 1.0 ) ) * anisotropyPolar.b;
	#else
		vec2 anisotropyV = anisotropyVector;
	#endif
	material.anisotropy = length( anisotropyV );
	if( material.anisotropy == 0.0 ) {
		anisotropyV = vec2( 1.0, 0.0 );
	} else {
		anisotropyV /= material.anisotropy;
		material.anisotropy = saturate( material.anisotropy );
	}
	material.alphaT = mix( pow2( material.roughness ), 1.0, pow2( material.anisotropy ) );
	material.anisotropyT = tbn[ 0 ] * anisotropyV.x + tbn[ 1 ] * anisotropyV.y;
	material.anisotropyB = tbn[ 1 ] * anisotropyV.x - tbn[ 0 ] * anisotropyV.y;
#endif`,Vd=`struct PhysicalMaterial {
	vec3 diffuseColor;
	float roughness;
	vec3 specularColor;
	float specularF90;
	float dispersion;
	#ifdef USE_CLEARCOAT
		float clearcoat;
		float clearcoatRoughness;
		vec3 clearcoatF0;
		float clearcoatF90;
	#endif
	#ifdef USE_IRIDESCENCE
		float iridescence;
		float iridescenceIOR;
		float iridescenceThickness;
		vec3 iridescenceFresnel;
		vec3 iridescenceF0;
	#endif
	#ifdef USE_SHEEN
		vec3 sheenColor;
		float sheenRoughness;
	#endif
	#ifdef IOR
		float ior;
	#endif
	#ifdef USE_TRANSMISSION
		float transmission;
		float transmissionAlpha;
		float thickness;
		float attenuationDistance;
		vec3 attenuationColor;
	#endif
	#ifdef USE_ANISOTROPY
		float anisotropy;
		float alphaT;
		vec3 anisotropyT;
		vec3 anisotropyB;
	#endif
};
vec3 clearcoatSpecularDirect = vec3( 0.0 );
vec3 clearcoatSpecularIndirect = vec3( 0.0 );
vec3 sheenSpecularDirect = vec3( 0.0 );
vec3 sheenSpecularIndirect = vec3(0.0 );
vec3 Schlick_to_F0( const in vec3 f, const in float f90, const in float dotVH ) {
    float x = clamp( 1.0 - dotVH, 0.0, 1.0 );
    float x2 = x * x;
    float x5 = clamp( x * x2 * x2, 0.0, 0.9999 );
    return ( f - vec3( f90 ) * x5 ) / ( 1.0 - x5 );
}
float V_GGX_SmithCorrelated( const in float alpha, const in float dotNL, const in float dotNV ) {
	float a2 = pow2( alpha );
	float gv = dotNL * sqrt( a2 + ( 1.0 - a2 ) * pow2( dotNV ) );
	float gl = dotNV * sqrt( a2 + ( 1.0 - a2 ) * pow2( dotNL ) );
	return 0.5 / max( gv + gl, EPSILON );
}
float D_GGX( const in float alpha, const in float dotNH ) {
	float a2 = pow2( alpha );
	float denom = pow2( dotNH ) * ( a2 - 1.0 ) + 1.0;
	return RECIPROCAL_PI * a2 / pow2( denom );
}
#ifdef USE_ANISOTROPY
	float V_GGX_SmithCorrelated_Anisotropic( const in float alphaT, const in float alphaB, const in float dotTV, const in float dotBV, const in float dotTL, const in float dotBL, const in float dotNV, const in float dotNL ) {
		float gv = dotNL * length( vec3( alphaT * dotTV, alphaB * dotBV, dotNV ) );
		float gl = dotNV * length( vec3( alphaT * dotTL, alphaB * dotBL, dotNL ) );
		float v = 0.5 / ( gv + gl );
		return saturate(v);
	}
	float D_GGX_Anisotropic( const in float alphaT, const in float alphaB, const in float dotNH, const in float dotTH, const in float dotBH ) {
		float a2 = alphaT * alphaB;
		highp vec3 v = vec3( alphaB * dotTH, alphaT * dotBH, a2 * dotNH );
		highp float v2 = dot( v, v );
		float w2 = a2 / v2;
		return RECIPROCAL_PI * a2 * pow2 ( w2 );
	}
#endif
#ifdef USE_CLEARCOAT
	vec3 BRDF_GGX_Clearcoat( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in PhysicalMaterial material) {
		vec3 f0 = material.clearcoatF0;
		float f90 = material.clearcoatF90;
		float roughness = material.clearcoatRoughness;
		float alpha = pow2( roughness );
		vec3 halfDir = normalize( lightDir + viewDir );
		float dotNL = saturate( dot( normal, lightDir ) );
		float dotNV = saturate( dot( normal, viewDir ) );
		float dotNH = saturate( dot( normal, halfDir ) );
		float dotVH = saturate( dot( viewDir, halfDir ) );
		vec3 F = F_Schlick( f0, f90, dotVH );
		float V = V_GGX_SmithCorrelated( alpha, dotNL, dotNV );
		float D = D_GGX( alpha, dotNH );
		return F * ( V * D );
	}
#endif
vec3 BRDF_GGX( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in PhysicalMaterial material ) {
	vec3 f0 = material.specularColor;
	float f90 = material.specularF90;
	float roughness = material.roughness;
	float alpha = pow2( roughness );
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNL = saturate( dot( normal, lightDir ) );
	float dotNV = saturate( dot( normal, viewDir ) );
	float dotNH = saturate( dot( normal, halfDir ) );
	float dotVH = saturate( dot( viewDir, halfDir ) );
	vec3 F = F_Schlick( f0, f90, dotVH );
	#ifdef USE_IRIDESCENCE
		F = mix( F, material.iridescenceFresnel, material.iridescence );
	#endif
	#ifdef USE_ANISOTROPY
		float dotTL = dot( material.anisotropyT, lightDir );
		float dotTV = dot( material.anisotropyT, viewDir );
		float dotTH = dot( material.anisotropyT, halfDir );
		float dotBL = dot( material.anisotropyB, lightDir );
		float dotBV = dot( material.anisotropyB, viewDir );
		float dotBH = dot( material.anisotropyB, halfDir );
		float V = V_GGX_SmithCorrelated_Anisotropic( material.alphaT, alpha, dotTV, dotBV, dotTL, dotBL, dotNV, dotNL );
		float D = D_GGX_Anisotropic( material.alphaT, alpha, dotNH, dotTH, dotBH );
	#else
		float V = V_GGX_SmithCorrelated( alpha, dotNL, dotNV );
		float D = D_GGX( alpha, dotNH );
	#endif
	return F * ( V * D );
}
vec2 LTC_Uv( const in vec3 N, const in vec3 V, const in float roughness ) {
	const float LUT_SIZE = 64.0;
	const float LUT_SCALE = ( LUT_SIZE - 1.0 ) / LUT_SIZE;
	const float LUT_BIAS = 0.5 / LUT_SIZE;
	float dotNV = saturate( dot( N, V ) );
	vec2 uv = vec2( roughness, sqrt( 1.0 - dotNV ) );
	uv = uv * LUT_SCALE + LUT_BIAS;
	return uv;
}
float LTC_ClippedSphereFormFactor( const in vec3 f ) {
	float l = length( f );
	return max( ( l * l + f.z ) / ( l + 1.0 ), 0.0 );
}
vec3 LTC_EdgeVectorFormFactor( const in vec3 v1, const in vec3 v2 ) {
	float x = dot( v1, v2 );
	float y = abs( x );
	float a = 0.8543985 + ( 0.4965155 + 0.0145206 * y ) * y;
	float b = 3.4175940 + ( 4.1616724 + y ) * y;
	float v = a / b;
	float theta_sintheta = ( x > 0.0 ) ? v : 0.5 * inversesqrt( max( 1.0 - x * x, 1e-7 ) ) - v;
	return cross( v1, v2 ) * theta_sintheta;
}
vec3 LTC_Evaluate( const in vec3 N, const in vec3 V, const in vec3 P, const in mat3 mInv, const in vec3 rectCoords[ 4 ] ) {
	vec3 v1 = rectCoords[ 1 ] - rectCoords[ 0 ];
	vec3 v2 = rectCoords[ 3 ] - rectCoords[ 0 ];
	vec3 lightNormal = cross( v1, v2 );
	if( dot( lightNormal, P - rectCoords[ 0 ] ) < 0.0 ) return vec3( 0.0 );
	vec3 T1, T2;
	T1 = normalize( V - N * dot( V, N ) );
	T2 = - cross( N, T1 );
	mat3 mat = mInv * transposeMat3( mat3( T1, T2, N ) );
	vec3 coords[ 4 ];
	coords[ 0 ] = mat * ( rectCoords[ 0 ] - P );
	coords[ 1 ] = mat * ( rectCoords[ 1 ] - P );
	coords[ 2 ] = mat * ( rectCoords[ 2 ] - P );
	coords[ 3 ] = mat * ( rectCoords[ 3 ] - P );
	coords[ 0 ] = normalize( coords[ 0 ] );
	coords[ 1 ] = normalize( coords[ 1 ] );
	coords[ 2 ] = normalize( coords[ 2 ] );
	coords[ 3 ] = normalize( coords[ 3 ] );
	vec3 vectorFormFactor = vec3( 0.0 );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 0 ], coords[ 1 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 1 ], coords[ 2 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 2 ], coords[ 3 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 3 ], coords[ 0 ] );
	float result = LTC_ClippedSphereFormFactor( vectorFormFactor );
	return vec3( result );
}
#if defined( USE_SHEEN )
float D_Charlie( float roughness, float dotNH ) {
	float alpha = pow2( roughness );
	float invAlpha = 1.0 / alpha;
	float cos2h = dotNH * dotNH;
	float sin2h = max( 1.0 - cos2h, 0.0078125 );
	return ( 2.0 + invAlpha ) * pow( sin2h, invAlpha * 0.5 ) / ( 2.0 * PI );
}
float V_Neubelt( float dotNV, float dotNL ) {
	return saturate( 1.0 / ( 4.0 * ( dotNL + dotNV - dotNL * dotNV ) ) );
}
vec3 BRDF_Sheen( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, vec3 sheenColor, const in float sheenRoughness ) {
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNL = saturate( dot( normal, lightDir ) );
	float dotNV = saturate( dot( normal, viewDir ) );
	float dotNH = saturate( dot( normal, halfDir ) );
	float D = D_Charlie( sheenRoughness, dotNH );
	float V = V_Neubelt( dotNV, dotNL );
	return sheenColor * ( D * V );
}
#endif
float IBLSheenBRDF( const in vec3 normal, const in vec3 viewDir, const in float roughness ) {
	float dotNV = saturate( dot( normal, viewDir ) );
	float r2 = roughness * roughness;
	float a = roughness < 0.25 ? -339.2 * r2 + 161.4 * roughness - 25.9 : -8.48 * r2 + 14.3 * roughness - 9.95;
	float b = roughness < 0.25 ? 44.0 * r2 - 23.7 * roughness + 3.26 : 1.97 * r2 - 3.27 * roughness + 0.72;
	float DG = exp( a * dotNV + b ) + ( roughness < 0.25 ? 0.0 : 0.1 * ( roughness - 0.25 ) );
	return saturate( DG * RECIPROCAL_PI );
}
vec2 DFGApprox( const in vec3 normal, const in vec3 viewDir, const in float roughness ) {
	float dotNV = saturate( dot( normal, viewDir ) );
	const vec4 c0 = vec4( - 1, - 0.0275, - 0.572, 0.022 );
	const vec4 c1 = vec4( 1, 0.0425, 1.04, - 0.04 );
	vec4 r = roughness * c0 + c1;
	float a004 = min( r.x * r.x, exp2( - 9.28 * dotNV ) ) * r.x + r.y;
	vec2 fab = vec2( - 1.04, 1.04 ) * a004 + r.zw;
	return fab;
}
vec3 EnvironmentBRDF( const in vec3 normal, const in vec3 viewDir, const in vec3 specularColor, const in float specularF90, const in float roughness ) {
	vec2 fab = DFGApprox( normal, viewDir, roughness );
	return specularColor * fab.x + specularF90 * fab.y;
}
#ifdef USE_IRIDESCENCE
void computeMultiscatteringIridescence( const in vec3 normal, const in vec3 viewDir, const in vec3 specularColor, const in float specularF90, const in float iridescence, const in vec3 iridescenceF0, const in float roughness, inout vec3 singleScatter, inout vec3 multiScatter ) {
#else
void computeMultiscattering( const in vec3 normal, const in vec3 viewDir, const in vec3 specularColor, const in float specularF90, const in float roughness, inout vec3 singleScatter, inout vec3 multiScatter ) {
#endif
	vec2 fab = DFGApprox( normal, viewDir, roughness );
	#ifdef USE_IRIDESCENCE
		vec3 Fr = mix( specularColor, iridescenceF0, iridescence );
	#else
		vec3 Fr = specularColor;
	#endif
	vec3 FssEss = Fr * fab.x + specularF90 * fab.y;
	float Ess = fab.x + fab.y;
	float Ems = 1.0 - Ess;
	vec3 Favg = Fr + ( 1.0 - Fr ) * 0.047619;	vec3 Fms = FssEss * Favg / ( 1.0 - Ems * Favg );
	singleScatter += FssEss;
	multiScatter += Fms * Ems;
}
#if NUM_RECT_AREA_LIGHTS > 0
	void RE_Direct_RectArea_Physical( const in RectAreaLight rectAreaLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
		vec3 normal = geometryNormal;
		vec3 viewDir = geometryViewDir;
		vec3 position = geometryPosition;
		vec3 lightPos = rectAreaLight.position;
		vec3 halfWidth = rectAreaLight.halfWidth;
		vec3 halfHeight = rectAreaLight.halfHeight;
		vec3 lightColor = rectAreaLight.color;
		float roughness = material.roughness;
		vec3 rectCoords[ 4 ];
		rectCoords[ 0 ] = lightPos + halfWidth - halfHeight;		rectCoords[ 1 ] = lightPos - halfWidth - halfHeight;
		rectCoords[ 2 ] = lightPos - halfWidth + halfHeight;
		rectCoords[ 3 ] = lightPos + halfWidth + halfHeight;
		vec2 uv = LTC_Uv( normal, viewDir, roughness );
		vec4 t1 = texture2D( ltc_1, uv );
		vec4 t2 = texture2D( ltc_2, uv );
		mat3 mInv = mat3(
			vec3( t1.x, 0, t1.y ),
			vec3(    0, 1,    0 ),
			vec3( t1.z, 0, t1.w )
		);
		vec3 fresnel = ( material.specularColor * t2.x + ( vec3( 1.0 ) - material.specularColor ) * t2.y );
		reflectedLight.directSpecular += lightColor * fresnel * LTC_Evaluate( normal, viewDir, position, mInv, rectCoords );
		reflectedLight.directDiffuse += lightColor * material.diffuseColor * LTC_Evaluate( normal, viewDir, position, mat3( 1.0 ), rectCoords );
	}
#endif
void RE_Direct_Physical( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	#ifdef USE_CLEARCOAT
		float dotNLcc = saturate( dot( geometryClearcoatNormal, directLight.direction ) );
		vec3 ccIrradiance = dotNLcc * directLight.color;
		clearcoatSpecularDirect += ccIrradiance * BRDF_GGX_Clearcoat( directLight.direction, geometryViewDir, geometryClearcoatNormal, material );
	#endif
	#ifdef USE_SHEEN
		sheenSpecularDirect += irradiance * BRDF_Sheen( directLight.direction, geometryViewDir, geometryNormal, material.sheenColor, material.sheenRoughness );
	#endif
	reflectedLight.directSpecular += irradiance * BRDF_GGX( directLight.direction, geometryViewDir, geometryNormal, material );
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Physical( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectSpecular_Physical( const in vec3 radiance, const in vec3 irradiance, const in vec3 clearcoatRadiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight) {
	#ifdef USE_CLEARCOAT
		clearcoatSpecularIndirect += clearcoatRadiance * EnvironmentBRDF( geometryClearcoatNormal, geometryViewDir, material.clearcoatF0, material.clearcoatF90, material.clearcoatRoughness );
	#endif
	#ifdef USE_SHEEN
		sheenSpecularIndirect += irradiance * material.sheenColor * IBLSheenBRDF( geometryNormal, geometryViewDir, material.sheenRoughness );
	#endif
	vec3 singleScattering = vec3( 0.0 );
	vec3 multiScattering = vec3( 0.0 );
	vec3 cosineWeightedIrradiance = irradiance * RECIPROCAL_PI;
	#ifdef USE_IRIDESCENCE
		computeMultiscatteringIridescence( geometryNormal, geometryViewDir, material.specularColor, material.specularF90, material.iridescence, material.iridescenceFresnel, material.roughness, singleScattering, multiScattering );
	#else
		computeMultiscattering( geometryNormal, geometryViewDir, material.specularColor, material.specularF90, material.roughness, singleScattering, multiScattering );
	#endif
	vec3 totalScattering = singleScattering + multiScattering;
	vec3 diffuse = material.diffuseColor * ( 1.0 - max( max( totalScattering.r, totalScattering.g ), totalScattering.b ) );
	reflectedLight.indirectSpecular += radiance * singleScattering;
	reflectedLight.indirectSpecular += multiScattering * cosineWeightedIrradiance;
	reflectedLight.indirectDiffuse += diffuse * cosineWeightedIrradiance;
}
#define RE_Direct				RE_Direct_Physical
#define RE_Direct_RectArea		RE_Direct_RectArea_Physical
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Physical
#define RE_IndirectSpecular		RE_IndirectSpecular_Physical
float computeSpecularOcclusion( const in float dotNV, const in float ambientOcclusion, const in float roughness ) {
	return saturate( pow( dotNV + ambientOcclusion, exp2( - 16.0 * roughness - 1.0 ) ) - 1.0 + ambientOcclusion );
}`,Hd=`
vec3 geometryPosition = - vViewPosition;
vec3 geometryNormal = normal;
vec3 geometryViewDir = ( isOrthographic ) ? vec3( 0, 0, 1 ) : normalize( vViewPosition );
vec3 geometryClearcoatNormal = vec3( 0.0 );
#ifdef USE_CLEARCOAT
	geometryClearcoatNormal = clearcoatNormal;
#endif
#ifdef USE_IRIDESCENCE
	float dotNVi = saturate( dot( normal, geometryViewDir ) );
	if ( material.iridescenceThickness == 0.0 ) {
		material.iridescence = 0.0;
	} else {
		material.iridescence = saturate( material.iridescence );
	}
	if ( material.iridescence > 0.0 ) {
		material.iridescenceFresnel = evalIridescence( 1.0, material.iridescenceIOR, dotNVi, material.iridescenceThickness, material.specularColor );
		material.iridescenceF0 = Schlick_to_F0( material.iridescenceFresnel, 1.0, dotNVi );
	}
#endif
IncidentLight directLight;
#if ( NUM_POINT_LIGHTS > 0 ) && defined( RE_Direct )
	PointLight pointLight;
	#if defined( USE_SHADOWMAP ) && NUM_POINT_LIGHT_SHADOWS > 0
	PointLightShadow pointLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_POINT_LIGHTS; i ++ ) {
		pointLight = pointLights[ i ];
		getPointLightInfo( pointLight, geometryPosition, directLight );
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_POINT_LIGHT_SHADOWS )
		pointLightShadow = pointLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getPointShadow( pointShadowMap[ i ], pointLightShadow.shadowMapSize, pointLightShadow.shadowIntensity, pointLightShadow.shadowBias, pointLightShadow.shadowRadius, vPointShadowCoord[ i ], pointLightShadow.shadowCameraNear, pointLightShadow.shadowCameraFar ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_SPOT_LIGHTS > 0 ) && defined( RE_Direct )
	SpotLight spotLight;
	vec4 spotColor;
	vec3 spotLightCoord;
	bool inSpotLightMap;
	#if defined( USE_SHADOWMAP ) && NUM_SPOT_LIGHT_SHADOWS > 0
	SpotLightShadow spotLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHTS; i ++ ) {
		spotLight = spotLights[ i ];
		getSpotLightInfo( spotLight, geometryPosition, directLight );
		#if ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS )
		#define SPOT_LIGHT_MAP_INDEX UNROLLED_LOOP_INDEX
		#elif ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
		#define SPOT_LIGHT_MAP_INDEX NUM_SPOT_LIGHT_MAPS
		#else
		#define SPOT_LIGHT_MAP_INDEX ( UNROLLED_LOOP_INDEX - NUM_SPOT_LIGHT_SHADOWS + NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS )
		#endif
		#if ( SPOT_LIGHT_MAP_INDEX < NUM_SPOT_LIGHT_MAPS )
			spotLightCoord = vSpotLightCoord[ i ].xyz / vSpotLightCoord[ i ].w;
			inSpotLightMap = all( lessThan( abs( spotLightCoord * 2. - 1. ), vec3( 1.0 ) ) );
			spotColor = texture2D( spotLightMap[ SPOT_LIGHT_MAP_INDEX ], spotLightCoord.xy );
			directLight.color = inSpotLightMap ? directLight.color * spotColor.rgb : directLight.color;
		#endif
		#undef SPOT_LIGHT_MAP_INDEX
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
		spotLightShadow = spotLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getShadow( spotShadowMap[ i ], spotLightShadow.shadowMapSize, spotLightShadow.shadowIntensity, spotLightShadow.shadowBias, spotLightShadow.shadowRadius, vSpotLightCoord[ i ] ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_DIR_LIGHTS > 0 ) && defined( RE_Direct )
	DirectionalLight directionalLight;
	#if defined( USE_SHADOWMAP ) && NUM_DIR_LIGHT_SHADOWS > 0
	DirectionalLightShadow directionalLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_DIR_LIGHTS; i ++ ) {
		directionalLight = directionalLights[ i ];
		getDirectionalLightInfo( directionalLight, directLight );
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_DIR_LIGHT_SHADOWS )
		directionalLightShadow = directionalLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getShadow( directionalShadowMap[ i ], directionalLightShadow.shadowMapSize, directionalLightShadow.shadowIntensity, directionalLightShadow.shadowBias, directionalLightShadow.shadowRadius, vDirectionalShadowCoord[ i ] ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_RECT_AREA_LIGHTS > 0 ) && defined( RE_Direct_RectArea )
	RectAreaLight rectAreaLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_RECT_AREA_LIGHTS; i ++ ) {
		rectAreaLight = rectAreaLights[ i ];
		RE_Direct_RectArea( rectAreaLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if defined( RE_IndirectDiffuse )
	vec3 iblIrradiance = vec3( 0.0 );
	vec3 irradiance = getAmbientLightIrradiance( ambientLightColor );
	#if defined( USE_LIGHT_PROBES )
		irradiance += getLightProbeIrradiance( lightProbe, geometryNormal );
	#endif
	#if ( NUM_HEMI_LIGHTS > 0 )
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_HEMI_LIGHTS; i ++ ) {
			irradiance += getHemisphereLightIrradiance( hemisphereLights[ i ], geometryNormal );
		}
		#pragma unroll_loop_end
	#endif
#endif
#if defined( RE_IndirectSpecular )
	vec3 radiance = vec3( 0.0 );
	vec3 clearcoatRadiance = vec3( 0.0 );
#endif`,Gd=`#if defined( RE_IndirectDiffuse )
	#ifdef USE_LIGHTMAP
		vec4 lightMapTexel = texture2D( lightMap, vLightMapUv );
		vec3 lightMapIrradiance = lightMapTexel.rgb * lightMapIntensity;
		irradiance += lightMapIrradiance;
	#endif
	#if defined( USE_ENVMAP ) && defined( STANDARD ) && defined( ENVMAP_TYPE_CUBE_UV )
		iblIrradiance += getIBLIrradiance( geometryNormal );
	#endif
#endif
#if defined( USE_ENVMAP ) && defined( RE_IndirectSpecular )
	#ifdef USE_ANISOTROPY
		radiance += getIBLAnisotropyRadiance( geometryViewDir, geometryNormal, material.roughness, material.anisotropyB, material.anisotropy );
	#else
		radiance += getIBLRadiance( geometryViewDir, geometryNormal, material.roughness );
	#endif
	#ifdef USE_CLEARCOAT
		clearcoatRadiance += getIBLRadiance( geometryViewDir, geometryClearcoatNormal, material.clearcoatRoughness );
	#endif
#endif`,Wd=`#if defined( RE_IndirectDiffuse )
	RE_IndirectDiffuse( irradiance, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
#endif
#if defined( RE_IndirectSpecular )
	RE_IndirectSpecular( radiance, iblIrradiance, clearcoatRadiance, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
#endif`,Xd=`#if defined( USE_LOGARITHMIC_DEPTH_BUFFER )
	gl_FragDepth = vIsPerspective == 0.0 ? gl_FragCoord.z : log2( vFragDepth ) * logDepthBufFC * 0.5;
#endif`,qd=`#if defined( USE_LOGARITHMIC_DEPTH_BUFFER )
	uniform float logDepthBufFC;
	varying float vFragDepth;
	varying float vIsPerspective;
#endif`,Yd=`#ifdef USE_LOGARITHMIC_DEPTH_BUFFER
	varying float vFragDepth;
	varying float vIsPerspective;
#endif`,Zd=`#ifdef USE_LOGARITHMIC_DEPTH_BUFFER
	vFragDepth = 1.0 + gl_Position.w;
	vIsPerspective = float( isPerspectiveMatrix( projectionMatrix ) );
#endif`,Jd=`#ifdef USE_MAP
	vec4 sampledDiffuseColor = texture2D( map, vMapUv );
	#ifdef DECODE_VIDEO_TEXTURE
		sampledDiffuseColor = sRGBTransferEOTF( sampledDiffuseColor );
	#endif
	diffuseColor *= sampledDiffuseColor;
#endif`,$d=`#ifdef USE_MAP
	uniform sampler2D map;
#endif`,Kd=`#if defined( USE_MAP ) || defined( USE_ALPHAMAP )
	#if defined( USE_POINTS_UV )
		vec2 uv = vUv;
	#else
		vec2 uv = ( uvTransform * vec3( gl_PointCoord.x, 1.0 - gl_PointCoord.y, 1 ) ).xy;
	#endif
#endif
#ifdef USE_MAP
	diffuseColor *= texture2D( map, uv );
#endif
#ifdef USE_ALPHAMAP
	diffuseColor.a *= texture2D( alphaMap, uv ).g;
#endif`,Qd=`#if defined( USE_POINTS_UV )
	varying vec2 vUv;
#else
	#if defined( USE_MAP ) || defined( USE_ALPHAMAP )
		uniform mat3 uvTransform;
	#endif
#endif
#ifdef USE_MAP
	uniform sampler2D map;
#endif
#ifdef USE_ALPHAMAP
	uniform sampler2D alphaMap;
#endif`,jd=`float metalnessFactor = metalness;
#ifdef USE_METALNESSMAP
	vec4 texelMetalness = texture2D( metalnessMap, vMetalnessMapUv );
	metalnessFactor *= texelMetalness.b;
#endif`,ep=`#ifdef USE_METALNESSMAP
	uniform sampler2D metalnessMap;
#endif`,tp=`#ifdef USE_INSTANCING_MORPH
	float morphTargetInfluences[ MORPHTARGETS_COUNT ];
	float morphTargetBaseInfluence = texelFetch( morphTexture, ivec2( 0, gl_InstanceID ), 0 ).r;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		morphTargetInfluences[i] =  texelFetch( morphTexture, ivec2( i + 1, gl_InstanceID ), 0 ).r;
	}
#endif`,np=`#if defined( USE_MORPHCOLORS )
	vColor *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		#if defined( USE_COLOR_ALPHA )
			if ( morphTargetInfluences[ i ] != 0.0 ) vColor += getMorph( gl_VertexID, i, 2 ) * morphTargetInfluences[ i ];
		#elif defined( USE_COLOR )
			if ( morphTargetInfluences[ i ] != 0.0 ) vColor += getMorph( gl_VertexID, i, 2 ).rgb * morphTargetInfluences[ i ];
		#endif
	}
#endif`,ip=`#ifdef USE_MORPHNORMALS
	objectNormal *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		if ( morphTargetInfluences[ i ] != 0.0 ) objectNormal += getMorph( gl_VertexID, i, 1 ).xyz * morphTargetInfluences[ i ];
	}
#endif`,rp=`#ifdef USE_MORPHTARGETS
	#ifndef USE_INSTANCING_MORPH
		uniform float morphTargetBaseInfluence;
		uniform float morphTargetInfluences[ MORPHTARGETS_COUNT ];
	#endif
	uniform sampler2DArray morphTargetsTexture;
	uniform ivec2 morphTargetsTextureSize;
	vec4 getMorph( const in int vertexIndex, const in int morphTargetIndex, const in int offset ) {
		int texelIndex = vertexIndex * MORPHTARGETS_TEXTURE_STRIDE + offset;
		int y = texelIndex / morphTargetsTextureSize.x;
		int x = texelIndex - y * morphTargetsTextureSize.x;
		ivec3 morphUV = ivec3( x, y, morphTargetIndex );
		return texelFetch( morphTargetsTexture, morphUV, 0 );
	}
#endif`,sp=`#ifdef USE_MORPHTARGETS
	transformed *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		if ( morphTargetInfluences[ i ] != 0.0 ) transformed += getMorph( gl_VertexID, i, 0 ).xyz * morphTargetInfluences[ i ];
	}
#endif`,op=`float faceDirection = gl_FrontFacing ? 1.0 : - 1.0;
#ifdef FLAT_SHADED
	vec3 fdx = dFdx( vViewPosition );
	vec3 fdy = dFdy( vViewPosition );
	vec3 normal = normalize( cross( fdx, fdy ) );
#else
	vec3 normal = normalize( vNormal );
	#ifdef DOUBLE_SIDED
		normal *= faceDirection;
	#endif
#endif
#if defined( USE_NORMALMAP_TANGENTSPACE ) || defined( USE_CLEARCOAT_NORMALMAP ) || defined( USE_ANISOTROPY )
	#ifdef USE_TANGENT
		mat3 tbn = mat3( normalize( vTangent ), normalize( vBitangent ), normal );
	#else
		mat3 tbn = getTangentFrame( - vViewPosition, normal,
		#if defined( USE_NORMALMAP )
			vNormalMapUv
		#elif defined( USE_CLEARCOAT_NORMALMAP )
			vClearcoatNormalMapUv
		#else
			vUv
		#endif
		);
	#endif
	#if defined( DOUBLE_SIDED ) && ! defined( FLAT_SHADED )
		tbn[0] *= faceDirection;
		tbn[1] *= faceDirection;
	#endif
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	#ifdef USE_TANGENT
		mat3 tbn2 = mat3( normalize( vTangent ), normalize( vBitangent ), normal );
	#else
		mat3 tbn2 = getTangentFrame( - vViewPosition, normal, vClearcoatNormalMapUv );
	#endif
	#if defined( DOUBLE_SIDED ) && ! defined( FLAT_SHADED )
		tbn2[0] *= faceDirection;
		tbn2[1] *= faceDirection;
	#endif
#endif
vec3 nonPerturbedNormal = normal;`,ap=`#ifdef USE_NORMALMAP_OBJECTSPACE
	normal = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;
	#ifdef FLIP_SIDED
		normal = - normal;
	#endif
	#ifdef DOUBLE_SIDED
		normal = normal * faceDirection;
	#endif
	normal = normalize( normalMatrix * normal );
#elif defined( USE_NORMALMAP_TANGENTSPACE )
	vec3 mapN = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;
	mapN.xy *= normalScale;
	normal = normalize( tbn * mapN );
#elif defined( USE_BUMPMAP )
	normal = perturbNormalArb( - vViewPosition, normal, dHdxy_fwd(), faceDirection );
#endif`,lp=`#ifndef FLAT_SHADED
	varying vec3 vNormal;
	#ifdef USE_TANGENT
		varying vec3 vTangent;
		varying vec3 vBitangent;
	#endif
#endif`,cp=`#ifndef FLAT_SHADED
	varying vec3 vNormal;
	#ifdef USE_TANGENT
		varying vec3 vTangent;
		varying vec3 vBitangent;
	#endif
#endif`,hp=`#ifndef FLAT_SHADED
	vNormal = normalize( transformedNormal );
	#ifdef USE_TANGENT
		vTangent = normalize( transformedTangent );
		vBitangent = normalize( cross( vNormal, vTangent ) * tangent.w );
	#endif
#endif`,up=`#ifdef USE_NORMALMAP
	uniform sampler2D normalMap;
	uniform vec2 normalScale;
#endif
#ifdef USE_NORMALMAP_OBJECTSPACE
	uniform mat3 normalMatrix;
#endif
#if ! defined ( USE_TANGENT ) && ( defined ( USE_NORMALMAP_TANGENTSPACE ) || defined ( USE_CLEARCOAT_NORMALMAP ) || defined( USE_ANISOTROPY ) )
	mat3 getTangentFrame( vec3 eye_pos, vec3 surf_norm, vec2 uv ) {
		vec3 q0 = dFdx( eye_pos.xyz );
		vec3 q1 = dFdy( eye_pos.xyz );
		vec2 st0 = dFdx( uv.st );
		vec2 st1 = dFdy( uv.st );
		vec3 N = surf_norm;
		vec3 q1perp = cross( q1, N );
		vec3 q0perp = cross( N, q0 );
		vec3 T = q1perp * st0.x + q0perp * st1.x;
		vec3 B = q1perp * st0.y + q0perp * st1.y;
		float det = max( dot( T, T ), dot( B, B ) );
		float scale = ( det == 0.0 ) ? 0.0 : inversesqrt( det );
		return mat3( T * scale, B * scale, N );
	}
#endif`,fp=`#ifdef USE_CLEARCOAT
	vec3 clearcoatNormal = nonPerturbedNormal;
#endif`,dp=`#ifdef USE_CLEARCOAT_NORMALMAP
	vec3 clearcoatMapN = texture2D( clearcoatNormalMap, vClearcoatNormalMapUv ).xyz * 2.0 - 1.0;
	clearcoatMapN.xy *= clearcoatNormalScale;
	clearcoatNormal = normalize( tbn2 * clearcoatMapN );
#endif`,pp=`#ifdef USE_CLEARCOATMAP
	uniform sampler2D clearcoatMap;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	uniform sampler2D clearcoatNormalMap;
	uniform vec2 clearcoatNormalScale;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	uniform sampler2D clearcoatRoughnessMap;
#endif`,mp=`#ifdef USE_IRIDESCENCEMAP
	uniform sampler2D iridescenceMap;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	uniform sampler2D iridescenceThicknessMap;
#endif`,gp=`#ifdef OPAQUE
diffuseColor.a = 1.0;
#endif
#ifdef USE_TRANSMISSION
diffuseColor.a *= material.transmissionAlpha;
#endif
gl_FragColor = vec4( outgoingLight, diffuseColor.a );`,_p=`vec3 packNormalToRGB( const in vec3 normal ) {
	return normalize( normal ) * 0.5 + 0.5;
}
vec3 unpackRGBToNormal( const in vec3 rgb ) {
	return 2.0 * rgb.xyz - 1.0;
}
const float PackUpscale = 256. / 255.;const float UnpackDownscale = 255. / 256.;const float ShiftRight8 = 1. / 256.;
const float Inv255 = 1. / 255.;
const vec4 PackFactors = vec4( 1.0, 256.0, 256.0 * 256.0, 256.0 * 256.0 * 256.0 );
const vec2 UnpackFactors2 = vec2( UnpackDownscale, 1.0 / PackFactors.g );
const vec3 UnpackFactors3 = vec3( UnpackDownscale / PackFactors.rg, 1.0 / PackFactors.b );
const vec4 UnpackFactors4 = vec4( UnpackDownscale / PackFactors.rgb, 1.0 / PackFactors.a );
vec4 packDepthToRGBA( const in float v ) {
	if( v <= 0.0 )
		return vec4( 0., 0., 0., 0. );
	if( v >= 1.0 )
		return vec4( 1., 1., 1., 1. );
	float vuf;
	float af = modf( v * PackFactors.a, vuf );
	float bf = modf( vuf * ShiftRight8, vuf );
	float gf = modf( vuf * ShiftRight8, vuf );
	return vec4( vuf * Inv255, gf * PackUpscale, bf * PackUpscale, af );
}
vec3 packDepthToRGB( const in float v ) {
	if( v <= 0.0 )
		return vec3( 0., 0., 0. );
	if( v >= 1.0 )
		return vec3( 1., 1., 1. );
	float vuf;
	float bf = modf( v * PackFactors.b, vuf );
	float gf = modf( vuf * ShiftRight8, vuf );
	return vec3( vuf * Inv255, gf * PackUpscale, bf );
}
vec2 packDepthToRG( const in float v ) {
	if( v <= 0.0 )
		return vec2( 0., 0. );
	if( v >= 1.0 )
		return vec2( 1., 1. );
	float vuf;
	float gf = modf( v * 256., vuf );
	return vec2( vuf * Inv255, gf );
}
float unpackRGBAToDepth( const in vec4 v ) {
	return dot( v, UnpackFactors4 );
}
float unpackRGBToDepth( const in vec3 v ) {
	return dot( v, UnpackFactors3 );
}
float unpackRGToDepth( const in vec2 v ) {
	return v.r * UnpackFactors2.r + v.g * UnpackFactors2.g;
}
vec4 pack2HalfToRGBA( const in vec2 v ) {
	vec4 r = vec4( v.x, fract( v.x * 255.0 ), v.y, fract( v.y * 255.0 ) );
	return vec4( r.x - r.y / 255.0, r.y, r.z - r.w / 255.0, r.w );
}
vec2 unpackRGBATo2Half( const in vec4 v ) {
	return vec2( v.x + ( v.y / 255.0 ), v.z + ( v.w / 255.0 ) );
}
float viewZToOrthographicDepth( const in float viewZ, const in float near, const in float far ) {
	return ( viewZ + near ) / ( near - far );
}
float orthographicDepthToViewZ( const in float depth, const in float near, const in float far ) {
	return depth * ( near - far ) - near;
}
float viewZToPerspectiveDepth( const in float viewZ, const in float near, const in float far ) {
	return ( ( near + viewZ ) * far ) / ( ( far - near ) * viewZ );
}
float perspectiveDepthToViewZ( const in float depth, const in float near, const in float far ) {
	return ( near * far ) / ( ( far - near ) * depth - far );
}`,xp=`#ifdef PREMULTIPLIED_ALPHA
	gl_FragColor.rgb *= gl_FragColor.a;
#endif`,vp=`vec4 mvPosition = vec4( transformed, 1.0 );
#ifdef USE_BATCHING
	mvPosition = batchingMatrix * mvPosition;
#endif
#ifdef USE_INSTANCING
	mvPosition = instanceMatrix * mvPosition;
#endif
mvPosition = modelViewMatrix * mvPosition;
gl_Position = projectionMatrix * mvPosition;`,yp=`#ifdef DITHERING
	gl_FragColor.rgb = dithering( gl_FragColor.rgb );
#endif`,Mp=`#ifdef DITHERING
	vec3 dithering( vec3 color ) {
		float grid_position = rand( gl_FragCoord.xy );
		vec3 dither_shift_RGB = vec3( 0.25 / 255.0, -0.25 / 255.0, 0.25 / 255.0 );
		dither_shift_RGB = mix( 2.0 * dither_shift_RGB, -2.0 * dither_shift_RGB, grid_position );
		return color + dither_shift_RGB;
	}
#endif`,Sp=`float roughnessFactor = roughness;
#ifdef USE_ROUGHNESSMAP
	vec4 texelRoughness = texture2D( roughnessMap, vRoughnessMapUv );
	roughnessFactor *= texelRoughness.g;
#endif`,bp=`#ifdef USE_ROUGHNESSMAP
	uniform sampler2D roughnessMap;
#endif`,Tp=`#if NUM_SPOT_LIGHT_COORDS > 0
	varying vec4 vSpotLightCoord[ NUM_SPOT_LIGHT_COORDS ];
#endif
#if NUM_SPOT_LIGHT_MAPS > 0
	uniform sampler2D spotLightMap[ NUM_SPOT_LIGHT_MAPS ];
#endif
#ifdef USE_SHADOWMAP
	#if NUM_DIR_LIGHT_SHADOWS > 0
		uniform sampler2D directionalShadowMap[ NUM_DIR_LIGHT_SHADOWS ];
		varying vec4 vDirectionalShadowCoord[ NUM_DIR_LIGHT_SHADOWS ];
		struct DirectionalLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform DirectionalLightShadow directionalLightShadows[ NUM_DIR_LIGHT_SHADOWS ];
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
		uniform sampler2D spotShadowMap[ NUM_SPOT_LIGHT_SHADOWS ];
		struct SpotLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform SpotLightShadow spotLightShadows[ NUM_SPOT_LIGHT_SHADOWS ];
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		uniform sampler2D pointShadowMap[ NUM_POINT_LIGHT_SHADOWS ];
		varying vec4 vPointShadowCoord[ NUM_POINT_LIGHT_SHADOWS ];
		struct PointLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
			float shadowCameraNear;
			float shadowCameraFar;
		};
		uniform PointLightShadow pointLightShadows[ NUM_POINT_LIGHT_SHADOWS ];
	#endif
	float texture2DCompare( sampler2D depths, vec2 uv, float compare ) {
		float depth = unpackRGBAToDepth( texture2D( depths, uv ) );
		#ifdef USE_REVERSED_DEPTH_BUFFER
			return step( depth, compare );
		#else
			return step( compare, depth );
		#endif
	}
	vec2 texture2DDistribution( sampler2D shadow, vec2 uv ) {
		return unpackRGBATo2Half( texture2D( shadow, uv ) );
	}
	float VSMShadow( sampler2D shadow, vec2 uv, float compare ) {
		float occlusion = 1.0;
		vec2 distribution = texture2DDistribution( shadow, uv );
		#ifdef USE_REVERSED_DEPTH_BUFFER
			float hard_shadow = step( distribution.x, compare );
		#else
			float hard_shadow = step( compare, distribution.x );
		#endif
		if ( hard_shadow != 1.0 ) {
			float distance = compare - distribution.x;
			float variance = max( 0.00000, distribution.y * distribution.y );
			float softness_probability = variance / (variance + distance * distance );			softness_probability = clamp( ( softness_probability - 0.3 ) / ( 0.95 - 0.3 ), 0.0, 1.0 );			occlusion = clamp( max( hard_shadow, softness_probability ), 0.0, 1.0 );
		}
		return occlusion;
	}
	float getShadow( sampler2D shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord ) {
		float shadow = 1.0;
		shadowCoord.xyz /= shadowCoord.w;
		shadowCoord.z += shadowBias;
		bool inFrustum = shadowCoord.x >= 0.0 && shadowCoord.x <= 1.0 && shadowCoord.y >= 0.0 && shadowCoord.y <= 1.0;
		bool frustumTest = inFrustum && shadowCoord.z <= 1.0;
		if ( frustumTest ) {
		#if defined( SHADOWMAP_TYPE_PCF )
			vec2 texelSize = vec2( 1.0 ) / shadowMapSize;
			float dx0 = - texelSize.x * shadowRadius;
			float dy0 = - texelSize.y * shadowRadius;
			float dx1 = + texelSize.x * shadowRadius;
			float dy1 = + texelSize.y * shadowRadius;
			float dx2 = dx0 / 2.0;
			float dy2 = dy0 / 2.0;
			float dx3 = dx1 / 2.0;
			float dy3 = dy1 / 2.0;
			shadow = (
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx0, dy0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx1, dy0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx2, dy2 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy2 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx3, dy2 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx0, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx2, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy, shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx3, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx1, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx2, dy3 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy3 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx3, dy3 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx0, dy1 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy1 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx1, dy1 ), shadowCoord.z )
			) * ( 1.0 / 17.0 );
		#elif defined( SHADOWMAP_TYPE_PCF_SOFT )
			vec2 texelSize = vec2( 1.0 ) / shadowMapSize;
			float dx = texelSize.x;
			float dy = texelSize.y;
			vec2 uv = shadowCoord.xy;
			vec2 f = fract( uv * shadowMapSize + 0.5 );
			uv -= f * texelSize;
			shadow = (
				texture2DCompare( shadowMap, uv, shadowCoord.z ) +
				texture2DCompare( shadowMap, uv + vec2( dx, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, uv + vec2( 0.0, dy ), shadowCoord.z ) +
				texture2DCompare( shadowMap, uv + texelSize, shadowCoord.z ) +
				mix( texture2DCompare( shadowMap, uv + vec2( -dx, 0.0 ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, 0.0 ), shadowCoord.z ),
					 f.x ) +
				mix( texture2DCompare( shadowMap, uv + vec2( -dx, dy ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, dy ), shadowCoord.z ),
					 f.x ) +
				mix( texture2DCompare( shadowMap, uv + vec2( 0.0, -dy ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( 0.0, 2.0 * dy ), shadowCoord.z ),
					 f.y ) +
				mix( texture2DCompare( shadowMap, uv + vec2( dx, -dy ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( dx, 2.0 * dy ), shadowCoord.z ),
					 f.y ) +
				mix( mix( texture2DCompare( shadowMap, uv + vec2( -dx, -dy ), shadowCoord.z ),
						  texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, -dy ), shadowCoord.z ),
						  f.x ),
					 mix( texture2DCompare( shadowMap, uv + vec2( -dx, 2.0 * dy ), shadowCoord.z ),
						  texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, 2.0 * dy ), shadowCoord.z ),
						  f.x ),
					 f.y )
			) * ( 1.0 / 9.0 );
		#elif defined( SHADOWMAP_TYPE_VSM )
			shadow = VSMShadow( shadowMap, shadowCoord.xy, shadowCoord.z );
		#else
			shadow = texture2DCompare( shadowMap, shadowCoord.xy, shadowCoord.z );
		#endif
		}
		return mix( 1.0, shadow, shadowIntensity );
	}
	vec2 cubeToUV( vec3 v, float texelSizeY ) {
		vec3 absV = abs( v );
		float scaleToCube = 1.0 / max( absV.x, max( absV.y, absV.z ) );
		absV *= scaleToCube;
		v *= scaleToCube * ( 1.0 - 2.0 * texelSizeY );
		vec2 planar = v.xy;
		float almostATexel = 1.5 * texelSizeY;
		float almostOne = 1.0 - almostATexel;
		if ( absV.z >= almostOne ) {
			if ( v.z > 0.0 )
				planar.x = 4.0 - v.x;
		} else if ( absV.x >= almostOne ) {
			float signX = sign( v.x );
			planar.x = v.z * signX + 2.0 * signX;
		} else if ( absV.y >= almostOne ) {
			float signY = sign( v.y );
			planar.x = v.x + 2.0 * signY + 2.0;
			planar.y = v.z * signY - 2.0;
		}
		return vec2( 0.125, 0.25 ) * planar + vec2( 0.375, 0.75 );
	}
	float getPointShadow( sampler2D shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord, float shadowCameraNear, float shadowCameraFar ) {
		float shadow = 1.0;
		vec3 lightToPosition = shadowCoord.xyz;
		
		float lightToPositionLength = length( lightToPosition );
		if ( lightToPositionLength - shadowCameraFar <= 0.0 && lightToPositionLength - shadowCameraNear >= 0.0 ) {
			float dp = ( lightToPositionLength - shadowCameraNear ) / ( shadowCameraFar - shadowCameraNear );			dp += shadowBias;
			vec3 bd3D = normalize( lightToPosition );
			vec2 texelSize = vec2( 1.0 ) / ( shadowMapSize * vec2( 4.0, 2.0 ) );
			#if defined( SHADOWMAP_TYPE_PCF ) || defined( SHADOWMAP_TYPE_PCF_SOFT ) || defined( SHADOWMAP_TYPE_VSM )
				vec2 offset = vec2( - 1, 1 ) * shadowRadius * texelSize.y;
				shadow = (
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xyy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yyy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xyx, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yyx, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xxy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yxy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xxx, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yxx, texelSize.y ), dp )
				) * ( 1.0 / 9.0 );
			#else
				shadow = texture2DCompare( shadowMap, cubeToUV( bd3D, texelSize.y ), dp );
			#endif
		}
		return mix( 1.0, shadow, shadowIntensity );
	}
#endif`,wp=`#if NUM_SPOT_LIGHT_COORDS > 0
	uniform mat4 spotLightMatrix[ NUM_SPOT_LIGHT_COORDS ];
	varying vec4 vSpotLightCoord[ NUM_SPOT_LIGHT_COORDS ];
#endif
#ifdef USE_SHADOWMAP
	#if NUM_DIR_LIGHT_SHADOWS > 0
		uniform mat4 directionalShadowMatrix[ NUM_DIR_LIGHT_SHADOWS ];
		varying vec4 vDirectionalShadowCoord[ NUM_DIR_LIGHT_SHADOWS ];
		struct DirectionalLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform DirectionalLightShadow directionalLightShadows[ NUM_DIR_LIGHT_SHADOWS ];
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
		struct SpotLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform SpotLightShadow spotLightShadows[ NUM_SPOT_LIGHT_SHADOWS ];
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		uniform mat4 pointShadowMatrix[ NUM_POINT_LIGHT_SHADOWS ];
		varying vec4 vPointShadowCoord[ NUM_POINT_LIGHT_SHADOWS ];
		struct PointLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
			float shadowCameraNear;
			float shadowCameraFar;
		};
		uniform PointLightShadow pointLightShadows[ NUM_POINT_LIGHT_SHADOWS ];
	#endif
#endif`,Ep=`#if ( defined( USE_SHADOWMAP ) && ( NUM_DIR_LIGHT_SHADOWS > 0 || NUM_POINT_LIGHT_SHADOWS > 0 ) ) || ( NUM_SPOT_LIGHT_COORDS > 0 )
	vec3 shadowWorldNormal = inverseTransformDirection( transformedNormal, viewMatrix );
	vec4 shadowWorldPosition;
#endif
#if defined( USE_SHADOWMAP )
	#if NUM_DIR_LIGHT_SHADOWS > 0
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_DIR_LIGHT_SHADOWS; i ++ ) {
			shadowWorldPosition = worldPosition + vec4( shadowWorldNormal * directionalLightShadows[ i ].shadowNormalBias, 0 );
			vDirectionalShadowCoord[ i ] = directionalShadowMatrix[ i ] * shadowWorldPosition;
		}
		#pragma unroll_loop_end
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_POINT_LIGHT_SHADOWS; i ++ ) {
			shadowWorldPosition = worldPosition + vec4( shadowWorldNormal * pointLightShadows[ i ].shadowNormalBias, 0 );
			vPointShadowCoord[ i ] = pointShadowMatrix[ i ] * shadowWorldPosition;
		}
		#pragma unroll_loop_end
	#endif
#endif
#if NUM_SPOT_LIGHT_COORDS > 0
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHT_COORDS; i ++ ) {
		shadowWorldPosition = worldPosition;
		#if ( defined( USE_SHADOWMAP ) && UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
			shadowWorldPosition.xyz += shadowWorldNormal * spotLightShadows[ i ].shadowNormalBias;
		#endif
		vSpotLightCoord[ i ] = spotLightMatrix[ i ] * shadowWorldPosition;
	}
	#pragma unroll_loop_end
#endif`,Ap=`float getShadowMask() {
	float shadow = 1.0;
	#ifdef USE_SHADOWMAP
	#if NUM_DIR_LIGHT_SHADOWS > 0
	DirectionalLightShadow directionalLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_DIR_LIGHT_SHADOWS; i ++ ) {
		directionalLight = directionalLightShadows[ i ];
		shadow *= receiveShadow ? getShadow( directionalShadowMap[ i ], directionalLight.shadowMapSize, directionalLight.shadowIntensity, directionalLight.shadowBias, directionalLight.shadowRadius, vDirectionalShadowCoord[ i ] ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
	SpotLightShadow spotLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHT_SHADOWS; i ++ ) {
		spotLight = spotLightShadows[ i ];
		shadow *= receiveShadow ? getShadow( spotShadowMap[ i ], spotLight.shadowMapSize, spotLight.shadowIntensity, spotLight.shadowBias, spotLight.shadowRadius, vSpotLightCoord[ i ] ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
	PointLightShadow pointLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_POINT_LIGHT_SHADOWS; i ++ ) {
		pointLight = pointLightShadows[ i ];
		shadow *= receiveShadow ? getPointShadow( pointShadowMap[ i ], pointLight.shadowMapSize, pointLight.shadowIntensity, pointLight.shadowBias, pointLight.shadowRadius, vPointShadowCoord[ i ], pointLight.shadowCameraNear, pointLight.shadowCameraFar ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#endif
	return shadow;
}`,Cp=`#ifdef USE_SKINNING
	mat4 boneMatX = getBoneMatrix( skinIndex.x );
	mat4 boneMatY = getBoneMatrix( skinIndex.y );
	mat4 boneMatZ = getBoneMatrix( skinIndex.z );
	mat4 boneMatW = getBoneMatrix( skinIndex.w );
#endif`,Rp=`#ifdef USE_SKINNING
	uniform mat4 bindMatrix;
	uniform mat4 bindMatrixInverse;
	uniform highp sampler2D boneTexture;
	mat4 getBoneMatrix( const in float i ) {
		int size = textureSize( boneTexture, 0 ).x;
		int j = int( i ) * 4;
		int x = j % size;
		int y = j / size;
		vec4 v1 = texelFetch( boneTexture, ivec2( x, y ), 0 );
		vec4 v2 = texelFetch( boneTexture, ivec2( x + 1, y ), 0 );
		vec4 v3 = texelFetch( boneTexture, ivec2( x + 2, y ), 0 );
		vec4 v4 = texelFetch( boneTexture, ivec2( x + 3, y ), 0 );
		return mat4( v1, v2, v3, v4 );
	}
#endif`,Pp=`#ifdef USE_SKINNING
	vec4 skinVertex = bindMatrix * vec4( transformed, 1.0 );
	vec4 skinned = vec4( 0.0 );
	skinned += boneMatX * skinVertex * skinWeight.x;
	skinned += boneMatY * skinVertex * skinWeight.y;
	skinned += boneMatZ * skinVertex * skinWeight.z;
	skinned += boneMatW * skinVertex * skinWeight.w;
	transformed = ( bindMatrixInverse * skinned ).xyz;
#endif`,Ip=`#ifdef USE_SKINNING
	mat4 skinMatrix = mat4( 0.0 );
	skinMatrix += skinWeight.x * boneMatX;
	skinMatrix += skinWeight.y * boneMatY;
	skinMatrix += skinWeight.z * boneMatZ;
	skinMatrix += skinWeight.w * boneMatW;
	skinMatrix = bindMatrixInverse * skinMatrix * bindMatrix;
	objectNormal = vec4( skinMatrix * vec4( objectNormal, 0.0 ) ).xyz;
	#ifdef USE_TANGENT
		objectTangent = vec4( skinMatrix * vec4( objectTangent, 0.0 ) ).xyz;
	#endif
#endif`,Dp=`float specularStrength;
#ifdef USE_SPECULARMAP
	vec4 texelSpecular = texture2D( specularMap, vSpecularMapUv );
	specularStrength = texelSpecular.r;
#else
	specularStrength = 1.0;
#endif`,Lp=`#ifdef USE_SPECULARMAP
	uniform sampler2D specularMap;
#endif`,Up=`#if defined( TONE_MAPPING )
	gl_FragColor.rgb = toneMapping( gl_FragColor.rgb );
#endif`,Np=`#ifndef saturate
#define saturate( a ) clamp( a, 0.0, 1.0 )
#endif
uniform float toneMappingExposure;
vec3 LinearToneMapping( vec3 color ) {
	return saturate( toneMappingExposure * color );
}
vec3 ReinhardToneMapping( vec3 color ) {
	color *= toneMappingExposure;
	return saturate( color / ( vec3( 1.0 ) + color ) );
}
vec3 CineonToneMapping( vec3 color ) {
	color *= toneMappingExposure;
	color = max( vec3( 0.0 ), color - 0.004 );
	return pow( ( color * ( 6.2 * color + 0.5 ) ) / ( color * ( 6.2 * color + 1.7 ) + 0.06 ), vec3( 2.2 ) );
}
vec3 RRTAndODTFit( vec3 v ) {
	vec3 a = v * ( v + 0.0245786 ) - 0.000090537;
	vec3 b = v * ( 0.983729 * v + 0.4329510 ) + 0.238081;
	return a / b;
}
vec3 ACESFilmicToneMapping( vec3 color ) {
	const mat3 ACESInputMat = mat3(
		vec3( 0.59719, 0.07600, 0.02840 ),		vec3( 0.35458, 0.90834, 0.13383 ),
		vec3( 0.04823, 0.01566, 0.83777 )
	);
	const mat3 ACESOutputMat = mat3(
		vec3(  1.60475, -0.10208, -0.00327 ),		vec3( -0.53108,  1.10813, -0.07276 ),
		vec3( -0.07367, -0.00605,  1.07602 )
	);
	color *= toneMappingExposure / 0.6;
	color = ACESInputMat * color;
	color = RRTAndODTFit( color );
	color = ACESOutputMat * color;
	return saturate( color );
}
const mat3 LINEAR_REC2020_TO_LINEAR_SRGB = mat3(
	vec3( 1.6605, - 0.1246, - 0.0182 ),
	vec3( - 0.5876, 1.1329, - 0.1006 ),
	vec3( - 0.0728, - 0.0083, 1.1187 )
);
const mat3 LINEAR_SRGB_TO_LINEAR_REC2020 = mat3(
	vec3( 0.6274, 0.0691, 0.0164 ),
	vec3( 0.3293, 0.9195, 0.0880 ),
	vec3( 0.0433, 0.0113, 0.8956 )
);
vec3 agxDefaultContrastApprox( vec3 x ) {
	vec3 x2 = x * x;
	vec3 x4 = x2 * x2;
	return + 15.5 * x4 * x2
		- 40.14 * x4 * x
		+ 31.96 * x4
		- 6.868 * x2 * x
		+ 0.4298 * x2
		+ 0.1191 * x
		- 0.00232;
}
vec3 AgXToneMapping( vec3 color ) {
	const mat3 AgXInsetMatrix = mat3(
		vec3( 0.856627153315983, 0.137318972929847, 0.11189821299995 ),
		vec3( 0.0951212405381588, 0.761241990602591, 0.0767994186031903 ),
		vec3( 0.0482516061458583, 0.101439036467562, 0.811302368396859 )
	);
	const mat3 AgXOutsetMatrix = mat3(
		vec3( 1.1271005818144368, - 0.1413297634984383, - 0.14132976349843826 ),
		vec3( - 0.11060664309660323, 1.157823702216272, - 0.11060664309660294 ),
		vec3( - 0.016493938717834573, - 0.016493938717834257, 1.2519364065950405 )
	);
	const float AgxMinEv = - 12.47393;	const float AgxMaxEv = 4.026069;
	color *= toneMappingExposure;
	color = LINEAR_SRGB_TO_LINEAR_REC2020 * color;
	color = AgXInsetMatrix * color;
	color = max( color, 1e-10 );	color = log2( color );
	color = ( color - AgxMinEv ) / ( AgxMaxEv - AgxMinEv );
	color = clamp( color, 0.0, 1.0 );
	color = agxDefaultContrastApprox( color );
	color = AgXOutsetMatrix * color;
	color = pow( max( vec3( 0.0 ), color ), vec3( 2.2 ) );
	color = LINEAR_REC2020_TO_LINEAR_SRGB * color;
	color = clamp( color, 0.0, 1.0 );
	return color;
}
vec3 NeutralToneMapping( vec3 color ) {
	const float StartCompression = 0.8 - 0.04;
	const float Desaturation = 0.15;
	color *= toneMappingExposure;
	float x = min( color.r, min( color.g, color.b ) );
	float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
	color -= offset;
	float peak = max( color.r, max( color.g, color.b ) );
	if ( peak < StartCompression ) return color;
	float d = 1. - StartCompression;
	float newPeak = 1. - d * d / ( peak + d - StartCompression );
	color *= newPeak / peak;
	float g = 1. - 1. / ( Desaturation * ( peak - newPeak ) + 1. );
	return mix( color, vec3( newPeak ), g );
}
vec3 CustomToneMapping( vec3 color ) { return color; }`,Fp=`#ifdef USE_TRANSMISSION
	material.transmission = transmission;
	material.transmissionAlpha = 1.0;
	material.thickness = thickness;
	material.attenuationDistance = attenuationDistance;
	material.attenuationColor = attenuationColor;
	#ifdef USE_TRANSMISSIONMAP
		material.transmission *= texture2D( transmissionMap, vTransmissionMapUv ).r;
	#endif
	#ifdef USE_THICKNESSMAP
		material.thickness *= texture2D( thicknessMap, vThicknessMapUv ).g;
	#endif
	vec3 pos = vWorldPosition;
	vec3 v = normalize( cameraPosition - pos );
	vec3 n = inverseTransformDirection( normal, viewMatrix );
	vec4 transmitted = getIBLVolumeRefraction(
		n, v, material.roughness, material.diffuseColor, material.specularColor, material.specularF90,
		pos, modelMatrix, viewMatrix, projectionMatrix, material.dispersion, material.ior, material.thickness,
		material.attenuationColor, material.attenuationDistance );
	material.transmissionAlpha = mix( material.transmissionAlpha, transmitted.a, material.transmission );
	totalDiffuse = mix( totalDiffuse, transmitted.rgb, material.transmission );
#endif`,Op=`#ifdef USE_TRANSMISSION
	uniform float transmission;
	uniform float thickness;
	uniform float attenuationDistance;
	uniform vec3 attenuationColor;
	#ifdef USE_TRANSMISSIONMAP
		uniform sampler2D transmissionMap;
	#endif
	#ifdef USE_THICKNESSMAP
		uniform sampler2D thicknessMap;
	#endif
	uniform vec2 transmissionSamplerSize;
	uniform sampler2D transmissionSamplerMap;
	uniform mat4 modelMatrix;
	uniform mat4 projectionMatrix;
	varying vec3 vWorldPosition;
	float w0( float a ) {
		return ( 1.0 / 6.0 ) * ( a * ( a * ( - a + 3.0 ) - 3.0 ) + 1.0 );
	}
	float w1( float a ) {
		return ( 1.0 / 6.0 ) * ( a *  a * ( 3.0 * a - 6.0 ) + 4.0 );
	}
	float w2( float a ){
		return ( 1.0 / 6.0 ) * ( a * ( a * ( - 3.0 * a + 3.0 ) + 3.0 ) + 1.0 );
	}
	float w3( float a ) {
		return ( 1.0 / 6.0 ) * ( a * a * a );
	}
	float g0( float a ) {
		return w0( a ) + w1( a );
	}
	float g1( float a ) {
		return w2( a ) + w3( a );
	}
	float h0( float a ) {
		return - 1.0 + w1( a ) / ( w0( a ) + w1( a ) );
	}
	float h1( float a ) {
		return 1.0 + w3( a ) / ( w2( a ) + w3( a ) );
	}
	vec4 bicubic( sampler2D tex, vec2 uv, vec4 texelSize, float lod ) {
		uv = uv * texelSize.zw + 0.5;
		vec2 iuv = floor( uv );
		vec2 fuv = fract( uv );
		float g0x = g0( fuv.x );
		float g1x = g1( fuv.x );
		float h0x = h0( fuv.x );
		float h1x = h1( fuv.x );
		float h0y = h0( fuv.y );
		float h1y = h1( fuv.y );
		vec2 p0 = ( vec2( iuv.x + h0x, iuv.y + h0y ) - 0.5 ) * texelSize.xy;
		vec2 p1 = ( vec2( iuv.x + h1x, iuv.y + h0y ) - 0.5 ) * texelSize.xy;
		vec2 p2 = ( vec2( iuv.x + h0x, iuv.y + h1y ) - 0.5 ) * texelSize.xy;
		vec2 p3 = ( vec2( iuv.x + h1x, iuv.y + h1y ) - 0.5 ) * texelSize.xy;
		return g0( fuv.y ) * ( g0x * textureLod( tex, p0, lod ) + g1x * textureLod( tex, p1, lod ) ) +
			g1( fuv.y ) * ( g0x * textureLod( tex, p2, lod ) + g1x * textureLod( tex, p3, lod ) );
	}
	vec4 textureBicubic( sampler2D sampler, vec2 uv, float lod ) {
		vec2 fLodSize = vec2( textureSize( sampler, int( lod ) ) );
		vec2 cLodSize = vec2( textureSize( sampler, int( lod + 1.0 ) ) );
		vec2 fLodSizeInv = 1.0 / fLodSize;
		vec2 cLodSizeInv = 1.0 / cLodSize;
		vec4 fSample = bicubic( sampler, uv, vec4( fLodSizeInv, fLodSize ), floor( lod ) );
		vec4 cSample = bicubic( sampler, uv, vec4( cLodSizeInv, cLodSize ), ceil( lod ) );
		return mix( fSample, cSample, fract( lod ) );
	}
	vec3 getVolumeTransmissionRay( const in vec3 n, const in vec3 v, const in float thickness, const in float ior, const in mat4 modelMatrix ) {
		vec3 refractionVector = refract( - v, normalize( n ), 1.0 / ior );
		vec3 modelScale;
		modelScale.x = length( vec3( modelMatrix[ 0 ].xyz ) );
		modelScale.y = length( vec3( modelMatrix[ 1 ].xyz ) );
		modelScale.z = length( vec3( modelMatrix[ 2 ].xyz ) );
		return normalize( refractionVector ) * thickness * modelScale;
	}
	float applyIorToRoughness( const in float roughness, const in float ior ) {
		return roughness * clamp( ior * 2.0 - 2.0, 0.0, 1.0 );
	}
	vec4 getTransmissionSample( const in vec2 fragCoord, const in float roughness, const in float ior ) {
		float lod = log2( transmissionSamplerSize.x ) * applyIorToRoughness( roughness, ior );
		return textureBicubic( transmissionSamplerMap, fragCoord.xy, lod );
	}
	vec3 volumeAttenuation( const in float transmissionDistance, const in vec3 attenuationColor, const in float attenuationDistance ) {
		if ( isinf( attenuationDistance ) ) {
			return vec3( 1.0 );
		} else {
			vec3 attenuationCoefficient = -log( attenuationColor ) / attenuationDistance;
			vec3 transmittance = exp( - attenuationCoefficient * transmissionDistance );			return transmittance;
		}
	}
	vec4 getIBLVolumeRefraction( const in vec3 n, const in vec3 v, const in float roughness, const in vec3 diffuseColor,
		const in vec3 specularColor, const in float specularF90, const in vec3 position, const in mat4 modelMatrix,
		const in mat4 viewMatrix, const in mat4 projMatrix, const in float dispersion, const in float ior, const in float thickness,
		const in vec3 attenuationColor, const in float attenuationDistance ) {
		vec4 transmittedLight;
		vec3 transmittance;
		#ifdef USE_DISPERSION
			float halfSpread = ( ior - 1.0 ) * 0.025 * dispersion;
			vec3 iors = vec3( ior - halfSpread, ior, ior + halfSpread );
			for ( int i = 0; i < 3; i ++ ) {
				vec3 transmissionRay = getVolumeTransmissionRay( n, v, thickness, iors[ i ], modelMatrix );
				vec3 refractedRayExit = position + transmissionRay;
				vec4 ndcPos = projMatrix * viewMatrix * vec4( refractedRayExit, 1.0 );
				vec2 refractionCoords = ndcPos.xy / ndcPos.w;
				refractionCoords += 1.0;
				refractionCoords /= 2.0;
				vec4 transmissionSample = getTransmissionSample( refractionCoords, roughness, iors[ i ] );
				transmittedLight[ i ] = transmissionSample[ i ];
				transmittedLight.a += transmissionSample.a;
				transmittance[ i ] = diffuseColor[ i ] * volumeAttenuation( length( transmissionRay ), attenuationColor, attenuationDistance )[ i ];
			}
			transmittedLight.a /= 3.0;
		#else
			vec3 transmissionRay = getVolumeTransmissionRay( n, v, thickness, ior, modelMatrix );
			vec3 refractedRayExit = position + transmissionRay;
			vec4 ndcPos = projMatrix * viewMatrix * vec4( refractedRayExit, 1.0 );
			vec2 refractionCoords = ndcPos.xy / ndcPos.w;
			refractionCoords += 1.0;
			refractionCoords /= 2.0;
			transmittedLight = getTransmissionSample( refractionCoords, roughness, ior );
			transmittance = diffuseColor * volumeAttenuation( length( transmissionRay ), attenuationColor, attenuationDistance );
		#endif
		vec3 attenuatedColor = transmittance * transmittedLight.rgb;
		vec3 F = EnvironmentBRDF( n, v, specularColor, specularF90, roughness );
		float transmittanceFactor = ( transmittance.r + transmittance.g + transmittance.b ) / 3.0;
		return vec4( ( 1.0 - F ) * attenuatedColor, 1.0 - ( 1.0 - transmittedLight.a ) * transmittanceFactor );
	}
#endif`,Bp=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	varying vec2 vUv;
#endif
#ifdef USE_MAP
	varying vec2 vMapUv;
#endif
#ifdef USE_ALPHAMAP
	varying vec2 vAlphaMapUv;
#endif
#ifdef USE_LIGHTMAP
	varying vec2 vLightMapUv;
#endif
#ifdef USE_AOMAP
	varying vec2 vAoMapUv;
#endif
#ifdef USE_BUMPMAP
	varying vec2 vBumpMapUv;
#endif
#ifdef USE_NORMALMAP
	varying vec2 vNormalMapUv;
#endif
#ifdef USE_EMISSIVEMAP
	varying vec2 vEmissiveMapUv;
#endif
#ifdef USE_METALNESSMAP
	varying vec2 vMetalnessMapUv;
#endif
#ifdef USE_ROUGHNESSMAP
	varying vec2 vRoughnessMapUv;
#endif
#ifdef USE_ANISOTROPYMAP
	varying vec2 vAnisotropyMapUv;
#endif
#ifdef USE_CLEARCOATMAP
	varying vec2 vClearcoatMapUv;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	varying vec2 vClearcoatNormalMapUv;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	varying vec2 vClearcoatRoughnessMapUv;
#endif
#ifdef USE_IRIDESCENCEMAP
	varying vec2 vIridescenceMapUv;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	varying vec2 vIridescenceThicknessMapUv;
#endif
#ifdef USE_SHEEN_COLORMAP
	varying vec2 vSheenColorMapUv;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	varying vec2 vSheenRoughnessMapUv;
#endif
#ifdef USE_SPECULARMAP
	varying vec2 vSpecularMapUv;
#endif
#ifdef USE_SPECULAR_COLORMAP
	varying vec2 vSpecularColorMapUv;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	varying vec2 vSpecularIntensityMapUv;
#endif
#ifdef USE_TRANSMISSIONMAP
	uniform mat3 transmissionMapTransform;
	varying vec2 vTransmissionMapUv;
#endif
#ifdef USE_THICKNESSMAP
	uniform mat3 thicknessMapTransform;
	varying vec2 vThicknessMapUv;
#endif`,zp=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	varying vec2 vUv;
#endif
#ifdef USE_MAP
	uniform mat3 mapTransform;
	varying vec2 vMapUv;
#endif
#ifdef USE_ALPHAMAP
	uniform mat3 alphaMapTransform;
	varying vec2 vAlphaMapUv;
#endif
#ifdef USE_LIGHTMAP
	uniform mat3 lightMapTransform;
	varying vec2 vLightMapUv;
#endif
#ifdef USE_AOMAP
	uniform mat3 aoMapTransform;
	varying vec2 vAoMapUv;
#endif
#ifdef USE_BUMPMAP
	uniform mat3 bumpMapTransform;
	varying vec2 vBumpMapUv;
#endif
#ifdef USE_NORMALMAP
	uniform mat3 normalMapTransform;
	varying vec2 vNormalMapUv;
#endif
#ifdef USE_DISPLACEMENTMAP
	uniform mat3 displacementMapTransform;
	varying vec2 vDisplacementMapUv;
#endif
#ifdef USE_EMISSIVEMAP
	uniform mat3 emissiveMapTransform;
	varying vec2 vEmissiveMapUv;
#endif
#ifdef USE_METALNESSMAP
	uniform mat3 metalnessMapTransform;
	varying vec2 vMetalnessMapUv;
#endif
#ifdef USE_ROUGHNESSMAP
	uniform mat3 roughnessMapTransform;
	varying vec2 vRoughnessMapUv;
#endif
#ifdef USE_ANISOTROPYMAP
	uniform mat3 anisotropyMapTransform;
	varying vec2 vAnisotropyMapUv;
#endif
#ifdef USE_CLEARCOATMAP
	uniform mat3 clearcoatMapTransform;
	varying vec2 vClearcoatMapUv;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	uniform mat3 clearcoatNormalMapTransform;
	varying vec2 vClearcoatNormalMapUv;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	uniform mat3 clearcoatRoughnessMapTransform;
	varying vec2 vClearcoatRoughnessMapUv;
#endif
#ifdef USE_SHEEN_COLORMAP
	uniform mat3 sheenColorMapTransform;
	varying vec2 vSheenColorMapUv;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	uniform mat3 sheenRoughnessMapTransform;
	varying vec2 vSheenRoughnessMapUv;
#endif
#ifdef USE_IRIDESCENCEMAP
	uniform mat3 iridescenceMapTransform;
	varying vec2 vIridescenceMapUv;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	uniform mat3 iridescenceThicknessMapTransform;
	varying vec2 vIridescenceThicknessMapUv;
#endif
#ifdef USE_SPECULARMAP
	uniform mat3 specularMapTransform;
	varying vec2 vSpecularMapUv;
#endif
#ifdef USE_SPECULAR_COLORMAP
	uniform mat3 specularColorMapTransform;
	varying vec2 vSpecularColorMapUv;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	uniform mat3 specularIntensityMapTransform;
	varying vec2 vSpecularIntensityMapUv;
#endif
#ifdef USE_TRANSMISSIONMAP
	uniform mat3 transmissionMapTransform;
	varying vec2 vTransmissionMapUv;
#endif
#ifdef USE_THICKNESSMAP
	uniform mat3 thicknessMapTransform;
	varying vec2 vThicknessMapUv;
#endif`,kp=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	vUv = vec3( uv, 1 ).xy;
#endif
#ifdef USE_MAP
	vMapUv = ( mapTransform * vec3( MAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ALPHAMAP
	vAlphaMapUv = ( alphaMapTransform * vec3( ALPHAMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_LIGHTMAP
	vLightMapUv = ( lightMapTransform * vec3( LIGHTMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_AOMAP
	vAoMapUv = ( aoMapTransform * vec3( AOMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_BUMPMAP
	vBumpMapUv = ( bumpMapTransform * vec3( BUMPMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_NORMALMAP
	vNormalMapUv = ( normalMapTransform * vec3( NORMALMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_DISPLACEMENTMAP
	vDisplacementMapUv = ( displacementMapTransform * vec3( DISPLACEMENTMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_EMISSIVEMAP
	vEmissiveMapUv = ( emissiveMapTransform * vec3( EMISSIVEMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_METALNESSMAP
	vMetalnessMapUv = ( metalnessMapTransform * vec3( METALNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ROUGHNESSMAP
	vRoughnessMapUv = ( roughnessMapTransform * vec3( ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ANISOTROPYMAP
	vAnisotropyMapUv = ( anisotropyMapTransform * vec3( ANISOTROPYMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOATMAP
	vClearcoatMapUv = ( clearcoatMapTransform * vec3( CLEARCOATMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	vClearcoatNormalMapUv = ( clearcoatNormalMapTransform * vec3( CLEARCOAT_NORMALMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	vClearcoatRoughnessMapUv = ( clearcoatRoughnessMapTransform * vec3( CLEARCOAT_ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_IRIDESCENCEMAP
	vIridescenceMapUv = ( iridescenceMapTransform * vec3( IRIDESCENCEMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	vIridescenceThicknessMapUv = ( iridescenceThicknessMapTransform * vec3( IRIDESCENCE_THICKNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SHEEN_COLORMAP
	vSheenColorMapUv = ( sheenColorMapTransform * vec3( SHEEN_COLORMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	vSheenRoughnessMapUv = ( sheenRoughnessMapTransform * vec3( SHEEN_ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULARMAP
	vSpecularMapUv = ( specularMapTransform * vec3( SPECULARMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULAR_COLORMAP
	vSpecularColorMapUv = ( specularColorMapTransform * vec3( SPECULAR_COLORMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	vSpecularIntensityMapUv = ( specularIntensityMapTransform * vec3( SPECULAR_INTENSITYMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_TRANSMISSIONMAP
	vTransmissionMapUv = ( transmissionMapTransform * vec3( TRANSMISSIONMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_THICKNESSMAP
	vThicknessMapUv = ( thicknessMapTransform * vec3( THICKNESSMAP_UV, 1 ) ).xy;
#endif`,Vp=`#if defined( USE_ENVMAP ) || defined( DISTANCE ) || defined ( USE_SHADOWMAP ) || defined ( USE_TRANSMISSION ) || NUM_SPOT_LIGHT_COORDS > 0
	vec4 worldPosition = vec4( transformed, 1.0 );
	#ifdef USE_BATCHING
		worldPosition = batchingMatrix * worldPosition;
	#endif
	#ifdef USE_INSTANCING
		worldPosition = instanceMatrix * worldPosition;
	#endif
	worldPosition = modelMatrix * worldPosition;
#endif`,Hp=`varying vec2 vUv;
uniform mat3 uvTransform;
void main() {
	vUv = ( uvTransform * vec3( uv, 1 ) ).xy;
	gl_Position = vec4( position.xy, 1.0, 1.0 );
}`,Gp=`uniform sampler2D t2D;
uniform float backgroundIntensity;
varying vec2 vUv;
void main() {
	vec4 texColor = texture2D( t2D, vUv );
	#ifdef DECODE_VIDEO_TEXTURE
		texColor = vec4( mix( pow( texColor.rgb * 0.9478672986 + vec3( 0.0521327014 ), vec3( 2.4 ) ), texColor.rgb * 0.0773993808, vec3( lessThanEqual( texColor.rgb, vec3( 0.04045 ) ) ) ), texColor.w );
	#endif
	texColor.rgb *= backgroundIntensity;
	gl_FragColor = texColor;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,Wp=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
	gl_Position.z = gl_Position.w;
}`,Xp=`#ifdef ENVMAP_TYPE_CUBE
	uniform samplerCube envMap;
#elif defined( ENVMAP_TYPE_CUBE_UV )
	uniform sampler2D envMap;
#endif
uniform float flipEnvMap;
uniform float backgroundBlurriness;
uniform float backgroundIntensity;
uniform mat3 backgroundRotation;
varying vec3 vWorldDirection;
#include <cube_uv_reflection_fragment>
void main() {
	#ifdef ENVMAP_TYPE_CUBE
		vec4 texColor = textureCube( envMap, backgroundRotation * vec3( flipEnvMap * vWorldDirection.x, vWorldDirection.yz ) );
	#elif defined( ENVMAP_TYPE_CUBE_UV )
		vec4 texColor = textureCubeUV( envMap, backgroundRotation * vWorldDirection, backgroundBlurriness );
	#else
		vec4 texColor = vec4( 0.0, 0.0, 0.0, 1.0 );
	#endif
	texColor.rgb *= backgroundIntensity;
	gl_FragColor = texColor;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,qp=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
	gl_Position.z = gl_Position.w;
}`,Yp=`uniform samplerCube tCube;
uniform float tFlip;
uniform float opacity;
varying vec3 vWorldDirection;
void main() {
	vec4 texColor = textureCube( tCube, vec3( tFlip * vWorldDirection.x, vWorldDirection.yz ) );
	gl_FragColor = texColor;
	gl_FragColor.a *= opacity;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,Zp=`#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
varying vec2 vHighPrecisionZW;
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <skinbase_vertex>
	#include <morphinstance_vertex>
	#ifdef USE_DISPLACEMENTMAP
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vHighPrecisionZW = gl_Position.zw;
}`,Jp=`#if DEPTH_PACKING == 3200
	uniform float opacity;
#endif
#include <common>
#include <packing>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
varying vec2 vHighPrecisionZW;
void main() {
	vec4 diffuseColor = vec4( 1.0 );
	#include <clipping_planes_fragment>
	#if DEPTH_PACKING == 3200
		diffuseColor.a = opacity;
	#endif
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <logdepthbuf_fragment>
	#ifdef USE_REVERSED_DEPTH_BUFFER
		float fragCoordZ = vHighPrecisionZW[ 0 ] / vHighPrecisionZW[ 1 ];
	#else
		float fragCoordZ = 0.5 * vHighPrecisionZW[ 0 ] / vHighPrecisionZW[ 1 ] + 0.5;
	#endif
	#if DEPTH_PACKING == 3200
		gl_FragColor = vec4( vec3( 1.0 - fragCoordZ ), opacity );
	#elif DEPTH_PACKING == 3201
		gl_FragColor = packDepthToRGBA( fragCoordZ );
	#elif DEPTH_PACKING == 3202
		gl_FragColor = vec4( packDepthToRGB( fragCoordZ ), 1.0 );
	#elif DEPTH_PACKING == 3203
		gl_FragColor = vec4( packDepthToRG( fragCoordZ ), 0.0, 1.0 );
	#endif
}`,$p=`#define DISTANCE
varying vec3 vWorldPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <skinbase_vertex>
	#include <morphinstance_vertex>
	#ifdef USE_DISPLACEMENTMAP
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <worldpos_vertex>
	#include <clipping_planes_vertex>
	vWorldPosition = worldPosition.xyz;
}`,Kp=`#define DISTANCE
uniform vec3 referencePosition;
uniform float nearDistance;
uniform float farDistance;
varying vec3 vWorldPosition;
#include <common>
#include <packing>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <clipping_planes_pars_fragment>
void main () {
	vec4 diffuseColor = vec4( 1.0 );
	#include <clipping_planes_fragment>
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	float dist = length( vWorldPosition - referencePosition );
	dist = ( dist - nearDistance ) / ( farDistance - nearDistance );
	dist = saturate( dist );
	gl_FragColor = packDepthToRGBA( dist );
}`,Qp=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
}`,jp=`uniform sampler2D tEquirect;
varying vec3 vWorldDirection;
#include <common>
void main() {
	vec3 direction = normalize( vWorldDirection );
	vec2 sampleUV = equirectUv( direction );
	gl_FragColor = texture2D( tEquirect, sampleUV );
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,em=`uniform float scale;
attribute float lineDistance;
varying float vLineDistance;
#include <common>
#include <uv_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	vLineDistance = scale * lineDistance;
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
}`,tm=`uniform vec3 diffuse;
uniform float opacity;
uniform float dashSize;
uniform float totalSize;
varying float vLineDistance;
#include <common>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	if ( mod( vLineDistance, totalSize ) > dashSize ) {
		discard;
	}
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
}`,nm=`#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#if defined ( USE_ENVMAP ) || defined ( USE_SKINNING )
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinbase_vertex>
		#include <skinnormal_vertex>
		#include <defaultnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <fog_vertex>
}`,im=`uniform vec3 diffuse;
uniform float opacity;
#ifndef FLAT_SHADED
	varying vec3 vNormal;
#endif
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <fog_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	#ifdef USE_LIGHTMAP
		vec4 lightMapTexel = texture2D( lightMap, vLightMapUv );
		reflectedLight.indirectDiffuse += lightMapTexel.rgb * lightMapIntensity * RECIPROCAL_PI;
	#else
		reflectedLight.indirectDiffuse += vec3( 1.0 );
	#endif
	#include <aomap_fragment>
	reflectedLight.indirectDiffuse *= diffuseColor.rgb;
	vec3 outgoingLight = reflectedLight.indirectDiffuse;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,rm=`#define LAMBERT
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,sm=`#define LAMBERT
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float opacity;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_lambert_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_lambert_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + totalEmissiveRadiance;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,om=`#define MATCAP
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <color_pars_vertex>
#include <displacementmap_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
	vViewPosition = - mvPosition.xyz;
}`,am=`#define MATCAP
uniform vec3 diffuse;
uniform float opacity;
uniform sampler2D matcap;
varying vec3 vViewPosition;
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <normal_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	vec3 viewDir = normalize( vViewPosition );
	vec3 x = normalize( vec3( viewDir.z, 0.0, - viewDir.x ) );
	vec3 y = cross( viewDir, x );
	vec2 uv = vec2( dot( x, normal ), dot( y, normal ) ) * 0.495 + 0.5;
	#ifdef USE_MATCAP
		vec4 matcapColor = texture2D( matcap, uv );
	#else
		vec4 matcapColor = vec4( vec3( mix( 0.2, 0.8, uv.y ) ), 1.0 );
	#endif
	vec3 outgoingLight = diffuseColor.rgb * matcapColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,lm=`#define NORMAL
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	varying vec3 vViewPosition;
#endif
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	vViewPosition = - mvPosition.xyz;
#endif
}`,cm=`#define NORMAL
uniform float opacity;
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	varying vec3 vViewPosition;
#endif
#include <packing>
#include <uv_pars_fragment>
#include <normal_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( 0.0, 0.0, 0.0, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	gl_FragColor = vec4( packNormalToRGB( normal ), diffuseColor.a );
	#ifdef OPAQUE
		gl_FragColor.a = 1.0;
	#endif
}`,hm=`#define PHONG
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,um=`#define PHONG
uniform vec3 diffuse;
uniform vec3 emissive;
uniform vec3 specular;
uniform float shininess;
uniform float opacity;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_phong_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_phong_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + reflectedLight.directSpecular + reflectedLight.indirectSpecular + totalEmissiveRadiance;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,fm=`#define STANDARD
varying vec3 vViewPosition;
#ifdef USE_TRANSMISSION
	varying vec3 vWorldPosition;
#endif
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
#ifdef USE_TRANSMISSION
	vWorldPosition = worldPosition.xyz;
#endif
}`,dm=`#define STANDARD
#ifdef PHYSICAL
	#define IOR
	#define USE_SPECULAR
#endif
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float roughness;
uniform float metalness;
uniform float opacity;
#ifdef IOR
	uniform float ior;
#endif
#ifdef USE_SPECULAR
	uniform float specularIntensity;
	uniform vec3 specularColor;
	#ifdef USE_SPECULAR_COLORMAP
		uniform sampler2D specularColorMap;
	#endif
	#ifdef USE_SPECULAR_INTENSITYMAP
		uniform sampler2D specularIntensityMap;
	#endif
#endif
#ifdef USE_CLEARCOAT
	uniform float clearcoat;
	uniform float clearcoatRoughness;
#endif
#ifdef USE_DISPERSION
	uniform float dispersion;
#endif
#ifdef USE_IRIDESCENCE
	uniform float iridescence;
	uniform float iridescenceIOR;
	uniform float iridescenceThicknessMinimum;
	uniform float iridescenceThicknessMaximum;
#endif
#ifdef USE_SHEEN
	uniform vec3 sheenColor;
	uniform float sheenRoughness;
	#ifdef USE_SHEEN_COLORMAP
		uniform sampler2D sheenColorMap;
	#endif
	#ifdef USE_SHEEN_ROUGHNESSMAP
		uniform sampler2D sheenRoughnessMap;
	#endif
#endif
#ifdef USE_ANISOTROPY
	uniform vec2 anisotropyVector;
	#ifdef USE_ANISOTROPYMAP
		uniform sampler2D anisotropyMap;
	#endif
#endif
varying vec3 vViewPosition;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <iridescence_fragment>
#include <cube_uv_reflection_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_physical_pars_fragment>
#include <fog_pars_fragment>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_physical_pars_fragment>
#include <transmission_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <clearcoat_pars_fragment>
#include <iridescence_pars_fragment>
#include <roughnessmap_pars_fragment>
#include <metalnessmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <roughnessmap_fragment>
	#include <metalnessmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <clearcoat_normal_fragment_begin>
	#include <clearcoat_normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_physical_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 totalDiffuse = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse;
	vec3 totalSpecular = reflectedLight.directSpecular + reflectedLight.indirectSpecular;
	#include <transmission_fragment>
	vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;
	#ifdef USE_SHEEN
		float sheenEnergyComp = 1.0 - 0.157 * max3( material.sheenColor );
		outgoingLight = outgoingLight * sheenEnergyComp + sheenSpecularDirect + sheenSpecularIndirect;
	#endif
	#ifdef USE_CLEARCOAT
		float dotNVcc = saturate( dot( geometryClearcoatNormal, geometryViewDir ) );
		vec3 Fcc = F_Schlick( material.clearcoatF0, material.clearcoatF90, dotNVcc );
		outgoingLight = outgoingLight * ( 1.0 - material.clearcoat * Fcc ) + ( clearcoatSpecularDirect + clearcoatSpecularIndirect ) * material.clearcoat;
	#endif
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,pm=`#define TOON
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,mm=`#define TOON
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float opacity;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <gradientmap_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_toon_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_toon_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + totalEmissiveRadiance;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,gm=`uniform float size;
uniform float scale;
#include <common>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
#ifdef USE_POINTS_UV
	varying vec2 vUv;
	uniform mat3 uvTransform;
#endif
void main() {
	#ifdef USE_POINTS_UV
		vUv = ( uvTransform * vec3( uv, 1 ) ).xy;
	#endif
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <project_vertex>
	gl_PointSize = size;
	#ifdef USE_SIZEATTENUATION
		bool isPerspective = isPerspectiveMatrix( projectionMatrix );
		if ( isPerspective ) gl_PointSize *= ( scale / - mvPosition.z );
	#endif
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <worldpos_vertex>
	#include <fog_vertex>
}`,_m=`uniform vec3 diffuse;
uniform float opacity;
#include <common>
#include <color_pars_fragment>
#include <map_particle_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_particle_fragment>
	#include <color_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
}`,xm=`#include <common>
#include <batching_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <shadowmap_pars_vertex>
void main() {
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,vm=`uniform vec3 color;
uniform float opacity;
#include <common>
#include <packing>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <logdepthbuf_pars_fragment>
#include <shadowmap_pars_fragment>
#include <shadowmask_pars_fragment>
void main() {
	#include <logdepthbuf_fragment>
	gl_FragColor = vec4( color, opacity * ( 1.0 - getShadowMask() ) );
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
}`,ym=`uniform float rotation;
uniform vec2 center;
#include <common>
#include <uv_pars_vertex>
#include <fog_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	vec4 mvPosition = modelViewMatrix[ 3 ];
	vec2 scale = vec2( length( modelMatrix[ 0 ].xyz ), length( modelMatrix[ 1 ].xyz ) );
	#ifndef USE_SIZEATTENUATION
		bool isPerspective = isPerspectiveMatrix( projectionMatrix );
		if ( isPerspective ) scale *= - mvPosition.z;
	#endif
	vec2 alignedPosition = ( position.xy - ( center - vec2( 0.5 ) ) ) * scale;
	vec2 rotatedPosition;
	rotatedPosition.x = cos( rotation ) * alignedPosition.x - sin( rotation ) * alignedPosition.y;
	rotatedPosition.y = sin( rotation ) * alignedPosition.x + cos( rotation ) * alignedPosition.y;
	mvPosition.xy += rotatedPosition;
	gl_Position = projectionMatrix * mvPosition;
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
}`,Mm=`uniform vec3 diffuse;
uniform float opacity;
#include <common>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
}`,K={alphahash_fragment:Gf,alphahash_pars_fragment:Wf,alphamap_fragment:Xf,alphamap_pars_fragment:qf,alphatest_fragment:Yf,alphatest_pars_fragment:Zf,aomap_fragment:Jf,aomap_pars_fragment:$f,batching_pars_vertex:Kf,batching_vertex:Qf,begin_vertex:jf,beginnormal_vertex:ed,bsdfs:td,iridescence_fragment:nd,bumpmap_pars_fragment:id,clipping_planes_fragment:rd,clipping_planes_pars_fragment:sd,clipping_planes_pars_vertex:od,clipping_planes_vertex:ad,color_fragment:ld,color_pars_fragment:cd,color_pars_vertex:hd,color_vertex:ud,common:fd,cube_uv_reflection_fragment:dd,defaultnormal_vertex:pd,displacementmap_pars_vertex:md,displacementmap_vertex:gd,emissivemap_fragment:_d,emissivemap_pars_fragment:xd,colorspace_fragment:vd,colorspace_pars_fragment:yd,envmap_fragment:Md,envmap_common_pars_fragment:Sd,envmap_pars_fragment:bd,envmap_pars_vertex:Td,envmap_physical_pars_fragment:Nd,envmap_vertex:wd,fog_vertex:Ed,fog_pars_vertex:Ad,fog_fragment:Cd,fog_pars_fragment:Rd,gradientmap_pars_fragment:Pd,lightmap_pars_fragment:Id,lights_lambert_fragment:Dd,lights_lambert_pars_fragment:Ld,lights_pars_begin:Ud,lights_toon_fragment:Fd,lights_toon_pars_fragment:Od,lights_phong_fragment:Bd,lights_phong_pars_fragment:zd,lights_physical_fragment:kd,lights_physical_pars_fragment:Vd,lights_fragment_begin:Hd,lights_fragment_maps:Gd,lights_fragment_end:Wd,logdepthbuf_fragment:Xd,logdepthbuf_pars_fragment:qd,logdepthbuf_pars_vertex:Yd,logdepthbuf_vertex:Zd,map_fragment:Jd,map_pars_fragment:$d,map_particle_fragment:Kd,map_particle_pars_fragment:Qd,metalnessmap_fragment:jd,metalnessmap_pars_fragment:ep,morphinstance_vertex:tp,morphcolor_vertex:np,morphnormal_vertex:ip,morphtarget_pars_vertex:rp,morphtarget_vertex:sp,normal_fragment_begin:op,normal_fragment_maps:ap,normal_pars_fragment:lp,normal_pars_vertex:cp,normal_vertex:hp,normalmap_pars_fragment:up,clearcoat_normal_fragment_begin:fp,clearcoat_normal_fragment_maps:dp,clearcoat_pars_fragment:pp,iridescence_pars_fragment:mp,opaque_fragment:gp,packing:_p,premultiplied_alpha_fragment:xp,project_vertex:vp,dithering_fragment:yp,dithering_pars_fragment:Mp,roughnessmap_fragment:Sp,roughnessmap_pars_fragment:bp,shadowmap_pars_fragment:Tp,shadowmap_pars_vertex:wp,shadowmap_vertex:Ep,shadowmask_pars_fragment:Ap,skinbase_vertex:Cp,skinning_pars_vertex:Rp,skinning_vertex:Pp,skinnormal_vertex:Ip,specularmap_fragment:Dp,specularmap_pars_fragment:Lp,tonemapping_fragment:Up,tonemapping_pars_fragment:Np,transmission_fragment:Fp,transmission_pars_fragment:Op,uv_pars_fragment:Bp,uv_pars_vertex:zp,uv_vertex:kp,worldpos_vertex:Vp,background_vert:Hp,background_frag:Gp,backgroundCube_vert:Wp,backgroundCube_frag:Xp,cube_vert:qp,cube_frag:Yp,depth_vert:Zp,depth_frag:Jp,distanceRGBA_vert:$p,distanceRGBA_frag:Kp,equirect_vert:Qp,equirect_frag:jp,linedashed_vert:em,linedashed_frag:tm,meshbasic_vert:nm,meshbasic_frag:im,meshlambert_vert:rm,meshlambert_frag:sm,meshmatcap_vert:om,meshmatcap_frag:am,meshnormal_vert:lm,meshnormal_frag:cm,meshphong_vert:hm,meshphong_frag:um,meshphysical_vert:fm,meshphysical_frag:dm,meshtoon_vert:pm,meshtoon_frag:mm,points_vert:gm,points_frag:_m,shadow_vert:xm,shadow_frag:vm,sprite_vert:ym,sprite_frag:Mm},I={common:{diffuse:{value:new ze(16777215)},opacity:{value:1},map:{value:null},mapTransform:{value:new Z},alphaMap:{value:null},alphaMapTransform:{value:new Z},alphaTest:{value:0}},specularmap:{specularMap:{value:null},specularMapTransform:{value:new Z}},envmap:{envMap:{value:null},envMapRotation:{value:new Z},flipEnvMap:{value:-1},reflectivity:{value:1},ior:{value:1.5},refractionRatio:{value:.98}},aomap:{aoMap:{value:null},aoMapIntensity:{value:1},aoMapTransform:{value:new Z}},lightmap:{lightMap:{value:null},lightMapIntensity:{value:1},lightMapTransform:{value:new Z}},bumpmap:{bumpMap:{value:null},bumpMapTransform:{value:new Z},bumpScale:{value:1}},normalmap:{normalMap:{value:null},normalMapTransform:{value:new Z},normalScale:{value:new rn(1,1)}},displacementmap:{displacementMap:{value:null},displacementMapTransform:{value:new Z},displacementScale:{value:1},displacementBias:{value:0}},emissivemap:{emissiveMap:{value:null},emissiveMapTransform:{value:new Z}},metalnessmap:{metalnessMap:{value:null},metalnessMapTransform:{value:new Z}},roughnessmap:{roughnessMap:{value:null},roughnessMapTransform:{value:new Z}},gradientmap:{gradientMap:{value:null}},fog:{fogDensity:{value:25e-5},fogNear:{value:1},fogFar:{value:2e3},fogColor:{value:new ze(16777215)}},lights:{ambientLightColor:{value:[]},lightProbe:{value:[]},directionalLights:{value:[],properties:{direction:{},color:{}}},directionalLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{}}},directionalShadowMap:{value:[]},directionalShadowMatrix:{value:[]},spotLights:{value:[],properties:{color:{},position:{},direction:{},distance:{},coneCos:{},penumbraCos:{},decay:{}}},spotLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{}}},spotLightMap:{value:[]},spotShadowMap:{value:[]},spotLightMatrix:{value:[]},pointLights:{value:[],properties:{color:{},position:{},decay:{},distance:{}}},pointLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{},shadowCameraNear:{},shadowCameraFar:{}}},pointShadowMap:{value:[]},pointShadowMatrix:{value:[]},hemisphereLights:{value:[],properties:{direction:{},skyColor:{},groundColor:{}}},rectAreaLights:{value:[],properties:{color:{},position:{},width:{},height:{}}},ltc_1:{value:null},ltc_2:{value:null}},points:{diffuse:{value:new ze(16777215)},opacity:{value:1},size:{value:1},scale:{value:1},map:{value:null},alphaMap:{value:null},alphaMapTransform:{value:new Z},alphaTest:{value:0},uvTransform:{value:new Z}},sprite:{diffuse:{value:new ze(16777215)},opacity:{value:1},center:{value:new rn(.5,.5)},rotation:{value:0},map:{value:null},mapTransform:{value:new Z},alphaMap:{value:null},alphaMapTransform:{value:new Z},alphaTest:{value:0}}},_h={basic:{uniforms:_t([I.common,I.specularmap,I.envmap,I.aomap,I.lightmap,I.fog]),vertexShader:K.meshbasic_vert,fragmentShader:K.meshbasic_frag},lambert:{uniforms:_t([I.common,I.specularmap,I.envmap,I.aomap,I.lightmap,I.emissivemap,I.bumpmap,I.normalmap,I.displacementmap,I.fog,I.lights,{emissive:{value:new ze(0)}}]),vertexShader:K.meshlambert_vert,fragmentShader:K.meshlambert_frag},phong:{uniforms:_t([I.common,I.specularmap,I.envmap,I.aomap,I.lightmap,I.emissivemap,I.bumpmap,I.normalmap,I.displacementmap,I.fog,I.lights,{emissive:{value:new ze(0)},specular:{value:new ze(1118481)},shininess:{value:30}}]),vertexShader:K.meshphong_vert,fragmentShader:K.meshphong_frag},standard:{uniforms:_t([I.common,I.envmap,I.aomap,I.lightmap,I.emissivemap,I.bumpmap,I.normalmap,I.displacementmap,I.roughnessmap,I.metalnessmap,I.fog,I.lights,{emissive:{value:new ze(0)},roughness:{value:1},metalness:{value:0},envMapIntensity:{value:1}}]),vertexShader:K.meshphysical_vert,fragmentShader:K.meshphysical_frag},toon:{uniforms:_t([I.common,I.aomap,I.lightmap,I.emissivemap,I.bumpmap,I.normalmap,I.displacementmap,I.gradientmap,I.fog,I.lights,{emissive:{value:new ze(0)}}]),vertexShader:K.meshtoon_vert,fragmentShader:K.meshtoon_frag},matcap:{uniforms:_t([I.common,I.bumpmap,I.normalmap,I.displacementmap,I.fog,{matcap:{value:null}}]),vertexShader:K.meshmatcap_vert,fragmentShader:K.meshmatcap_frag},points:{uniforms:_t([I.points,I.fog]),vertexShader:K.points_vert,fragmentShader:K.points_frag},dashed:{uniforms:_t([I.common,I.fog,{scale:{value:1},dashSize:{value:1},totalSize:{value:2}}]),vertexShader:K.linedashed_vert,fragmentShader:K.linedashed_frag},depth:{uniforms:_t([I.common,I.displacementmap]),vertexShader:K.depth_vert,fragmentShader:K.depth_frag},normal:{uniforms:_t([I.common,I.bumpmap,I.normalmap,I.displacementmap,{opacity:{value:1}}]),vertexShader:K.meshnormal_vert,fragmentShader:K.meshnormal_frag},sprite:{uniforms:_t([I.sprite,I.fog]),vertexShader:K.sprite_vert,fragmentShader:K.sprite_frag},background:{uniforms:{uvTransform:{value:new Z},t2D:{value:null},backgroundIntensity:{value:1}},vertexShader:K.background_vert,fragmentShader:K.background_frag},backgroundCube:{uniforms:{envMap:{value:null},flipEnvMap:{value:-1},backgroundBlurriness:{value:0},backgroundIntensity:{value:1},backgroundRotation:{value:new Z}},vertexShader:K.backgroundCube_vert,fragmentShader:K.backgroundCube_frag},cube:{uniforms:{tCube:{value:null},tFlip:{value:-1},opacity:{value:1}},vertexShader:K.cube_vert,fragmentShader:K.cube_frag},equirect:{uniforms:{tEquirect:{value:null}},vertexShader:K.equirect_vert,fragmentShader:K.equirect_frag},distanceRGBA:{uniforms:_t([I.common,I.displacementmap,{referencePosition:{value:new ee},nearDistance:{value:1},farDistance:{value:1e3}}]),vertexShader:K.distanceRGBA_vert,fragmentShader:K.distanceRGBA_frag},shadow:{uniforms:_t([I.lights,I.fog,{color:{value:new ze(0)},opacity:{value:1}}]),vertexShader:K.shadow_vert,fragmentShader:K.shadow_frag}};_h.physical={uniforms:_t([_h.standard.uniforms,{clearcoat:{value:0},clearcoatMap:{value:null},clearcoatMapTransform:{value:new Z},clearcoatNormalMap:{value:null},clearcoatNormalMapTransform:{value:new Z},clearcoatNormalScale:{value:new rn(1,1)},clearcoatRoughness:{value:0},clearcoatRoughnessMap:{value:null},clearcoatRoughnessMapTransform:{value:new Z},dispersion:{value:0},iridescence:{value:0},iridescenceMap:{value:null},iridescenceMapTransform:{value:new Z},iridescenceIOR:{value:1.3},iridescenceThicknessMinimum:{value:100},iridescenceThicknessMaximum:{value:400},iridescenceThicknessMap:{value:null},iridescenceThicknessMapTransform:{value:new Z},sheen:{value:0},sheenColor:{value:new ze(0)},sheenColorMap:{value:null},sheenColorMapTransform:{value:new Z},sheenRoughness:{value:1},sheenRoughnessMap:{value:null},sheenRoughnessMapTransform:{value:new Z},transmission:{value:0},transmissionMap:{value:null},transmissionMapTransform:{value:new Z},transmissionSamplerSize:{value:new rn},transmissionSamplerMap:{value:null},thickness:{value:0},thicknessMap:{value:null},thicknessMapTransform:{value:new Z},attenuationDistance:{value:0},attenuationColor:{value:new ze(0)},specularColor:{value:new ze(1,1,1)},specularColorMap:{value:null},specularColorMapTransform:{value:new Z},specularIntensity:{value:1},specularIntensityMap:{value:null},specularIntensityMapTransform:{value:new Z},anisotropyVector:{value:new rn},anisotropyMap:{value:null},anisotropyMapTransform:{value:new Z}}]),vertexShader:K.meshphysical_vert,fragmentShader:K.meshphysical_frag};var Ei=(1+Math.sqrt(5))/2,ir=1/Ei,yM=[new ee(-Ei,ir,0),new ee(Ei,ir,0),new ee(-ir,0,Ei),new ee(ir,0,Ei),new ee(0,Ei,-ir),new ee(0,Ei,ir),new ee(-1,1,-1),new ee(1,1,-1),new ee(-1,1,1),new ee(1,1,1)];var MM=new Float32Array(16),SM=new Float32Array(9),bM=new Float32Array(4);var TM={[Ga]:Wa,[Xa]:Ja,[Ya]:$a,[qa]:Za,[Wa]:Ga,[Ja]:Xa,[$a]:Ya,[Za]:qa};return bh(Sm);})();
/*! Bundled license information:

gsap/gsap-core.js:
  (*!
   * GSAP 3.13.0
   * https://gsap.com
   *
   * @license Copyright 2008-2025, GreenSock. All rights reserved.
   * Subject to the terms at https://gsap.com/standard-license
   * @author: Jack Doyle, jack@greensock.com
  *)

gsap/CSSPlugin.js:
  (*!
   * CSSPlugin 3.13.0
   * https://gsap.com
   *
   * Copyright 2008-2025, GreenSock. All rights reserved.
   * Subject to the terms at https://gsap.com/standard-license
   * @author: Jack Doyle, jack@greensock.com
  *)

gsap/Observer.js:
  (*!
   * Observer 3.13.0
   * https://gsap.com
   *
   * @license Copyright 2008-2025, GreenSock. All rights reserved.
   * Subject to the terms at https://gsap.com/standard-license
   * @author: Jack Doyle, jack@greensock.com
  *)

gsap/ScrollTrigger.js:
  (*!
   * ScrollTrigger 3.13.0
   * https://gsap.com
   *
   * @license Copyright 2008-2025, GreenSock. All rights reserved.
   * Subject to the terms at https://gsap.com/standard-license
   * @author: Jack Doyle, jack@greensock.com
  *)

three/build/three.core.js:
three/build/three.module.js:
  (**
   * @license
   * Copyright 2010-2025 Three.js Authors
   * SPDX-License-Identifier: MIT
   *)
*/
