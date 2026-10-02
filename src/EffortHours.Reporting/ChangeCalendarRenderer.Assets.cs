namespace EffortHours.Reporting;

public static partial class ChangeCalendarRenderer
{
    private const string Styles = """
        :root{color-scheme:dark;font-family:system-ui,sans-serif;background:#0c1217;color:#dce7ed}
        *{box-sizing:border-box}body{margin:0;padding:28px}main{max-width:1450px;margin:auto;border:1px solid #2b3c43;border-radius:14px;padding:28px;background:#131e22}
        h1{font-size:28px;margin:6px 0}h2{font-size:15px}p{color:#a8c1cc;line-height:1.5}.eyebrow{letter-spacing:.12em;font-size:12px;color:#b7dc8f}
        .summary{display:flex;justify-content:space-between;align-items:center;gap:20px;margin:25px 0}.summary strong{font-size:40px;color:#c1e49c}.summary p{margin:4px 0}
        button{font:inherit;color:#dce7ed;background:#1d3038;border:1px solid #3a535f;border-radius:5px;padding:8px 12px;cursor:pointer}
        button:hover,button:focus-visible{outline:2px solid #bfdd94}button[aria-pressed=true]{background:#355244}.units{display:flex;gap:8px}
        .layout{display:grid;grid-template-columns:minmax(0,1fr) 240px;gap:25px;border-top:1px solid #2b3c43;padding-top:20px}.layout>section{min-width:0}aside{border-left:1px solid #2b3c43;padding-left:20px}
        aside label{display:flex;gap:10px;align-items:center;border:1px solid #3a535f;border-radius:6px;padding:12px;margin:10px 0;overflow-wrap:anywhere}input{accent-color:#b7dc8f;width:18px;height:18px}
        .scroll{overflow-x:auto;padding:3px}#calendar{display:flex;gap:7px;min-height:265px}.week{display:grid;grid-template-rows:25px repeat(7,30px);gap:5px;flex:none;width:66px}
        .month{font-size:11px;color:#b7cbd5}.day{padding:2px;font-size:11px;display:flex;align-items:center;justify-content:space-between;flex-direction:column;color:#13241c;border:0}
        .day small{font-size:8px}.day.zero{background:#23343c;color:#a8c1cc}.day[aria-pressed=true]{outline:2px solid #fff}.legend{text-align:right;font-size:12px}.legend span{color:#a4d88c;font-size:20px}
        #detail{min-height:110px;padding:15px;border:1px solid #2b3c43;border-radius:7px;margin-top:12px}#detail h2{margin:0 0 8px}#detail p{margin:6px 0}.assumption,footer{font-size:12px}footer{border-top:1px solid #2b3c43;margin-top:20px}
        details{overflow-x:auto}footer{overflow-wrap:anywhere}table{border-collapse:collapse;width:100%;font-size:13px}th,td{text-align:right;padding:8px;border-bottom:1px solid #2b3c43}th:first-child{text-align:left}summary{cursor:pointer;padding:12px 0}
        @media(max-width:700px){body{padding:8px}main{padding:16px}.layout{grid-template-columns:minmax(0,1fr)}aside{border:0;padding:0}.summary strong{font-size:30px}.summary{align-items:flex-start;flex-direction:column}}
        """;

    private const string Script = """
        'use strict';
        const selected=new Set(data.projects.map(p=>p.id));let unit='ratio',active=null;
        const $=id=>document.getElementById(id),num=n=>n.toLocaleString('en-US',{maximumFractionDigits:2,minimumFractionDigits:2});
        const node=(tag,text)=>{const n=document.createElement(tag);if(text!==undefined)n.textContent=text;return n;};
        const enabled=()=>data.projects.filter(p=>selected.has(p.id));
        const sum=(i,k)=>enabled().reduce((n,p)=>n+p.points[i][k],0);
        const metric=(hours,i)=>unit==='hours'?hours:hours/data.capacity[i];
        const suffix=()=>unit==='hours'?'h':'×';
        function detail(i){active=i;const box=$('detail');box.replaceChildren(node('h2',data.dates[i]));
          box.append(node('p',`${num(sum(i,'expected'))} expected EHE hours (${num(sum(i,'low'))}–${num(sum(i,'high'))}); ${num(sum(i,'expected')/data.capacity[i])}× reference ratio.`));
          box.append(node('p',`${sum(i,'changes')} selected changes. Reference denominator: ${num(data.capacity[i])} hours.`));
          for(const p of enabled())box.append(node('p',`${p.id}: ${num(p.points[i].expected)}h (${num(p.points[i].low)}–${num(p.points[i].high)})`));
          document.querySelectorAll('.day').forEach(b=>b.setAttribute('aria-pressed',String(Number(b.dataset.index)===i)));
        }
        function draw(){const values=data.dates.map((_,i)=>sum(i,'expected')),total=values.reduce((a,b)=>a+b,0),capacity=data.capacity.reduce((a,b)=>a+b,0);
          $('total').textContent=unit==='hours'?`${num(total)} hours`:`${num(total/capacity)}× EHE`;
          $('range').textContent=`Planning range ${num(data.dates.reduce((s,_,i)=>s+sum(i,'low'),0))}–${num(data.dates.reduce((s,_,i)=>s+sum(i,'high'),0))} hours · ${selected.size} projects`;
          const cal=$('calendar');cal.replaceChildren();const labels=node('div');labels.className='week';labels.append(node('span','Day'));for(const day of ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'])labels.append(node('span',day));cal.append(labels);let week=null,month='';const max=Math.max(...values,0);
          data.dates.forEach((date,i)=>{const d=new Date(date+'T12:00:00Z'),weekday=d.getUTCDay();if(i===0||weekday===0){week=node('div');week.className='week';cal.append(week);const m=date.slice(0,7);week.append(node('span',m!==month?d.toLocaleDateString('en-US',{month:'short',year:'numeric',timeZone:'UTC'}):''));week.firstChild.className='month';month=m;}
            const b=node('button');b.className='day'+(values[i]===0?' zero':'');b.style.gridRow=String(weekday+2);b.dataset.index=i;b.setAttribute('aria-pressed',String(active===i));
            const label=`${date}: ${num(values[i])} expected EHE hours, ${num(values[i]/data.capacity[i])}× reference ratio`;b.title=label;b.setAttribute('aria-label',label);
            if(values[i]>0)b.style.backgroundColor=['#54765b','#7ca36d','#a4cb83','#c5e4a1'][Math.min(3,Math.floor(values[i]/max*4))];
            b.append(node('small',date.slice(-2)),node('span',`${num(metric(values[i],i))}${suffix()}`));b.addEventListener('click',()=>detail(i));week.append(b);
          });
          const table=$('values');table.replaceChildren();data.dates.forEach((date,i)=>{const row=node('tr');row.append(node('th',date));for(const v of [values[i],sum(i,'low'),sum(i,'high'),values[i]/data.capacity[i]])row.append(node('td',num(v)));table.append(row);});
          if(active!==null)detail(active);
        }
        for(const p of data.projects){const label=node('label'),input=node('input');input.type='checkbox';input.checked=true;input.dataset.project=p.id;input.addEventListener('change',()=>{input.checked?selected.add(p.id):selected.delete(p.id);draw();});label.append(input,node('span',p.id));$('projects').append(label);}
        for(const u of ['hours','ratio'])$(u).addEventListener('click',()=>{unit=u;$('hours').setAttribute('aria-pressed',String(u==='hours'));$('ratio').setAttribute('aria-pressed',String(u==='ratio'));draw();});
        for(const action of ['all','none'])$(action).addEventListener('click',()=>{selected.clear();if(action==='all')data.projects.forEach(p=>selected.add(p.id));document.querySelectorAll('input[data-project]').forEach(i=>i.checked=selected.has(i.dataset.project));draw();});
        $('hours').setAttribute('aria-pressed','false');$('ratio').setAttribute('aria-pressed','true');draw();
        """;
}
