// Synthetic fixtures only. Names do not refer to listed securities.
const buyReasons=[['차트','거래량을 동반한 추세 개선','예시에서는 가격이 상승하며 거래량이 늘어난 상황을 가정했습니다. 실제 서비스에서는 이동평균선, 지지·저항, 변동성을 함께 확인합니다.'],['실적','이익 성장 여부 확인','매출과 영업이익, 현금흐름이 함께 개선된 상황을 가정했습니다. 실제 재무제표나 실적 전망을 조회한 결과가 아닙니다.'],['시장·업종','시장과 업종의 방향 확인','소속 업종이 시장보다 강한 상황을 가정했습니다. 실제 판단에는 지수, 금리, 환율과 업종별 흐름을 반영해야 합니다.'],['수급·공시','수급의 지속성과 주요 사건 확인','기관·외국인 수급과 공시를 함께 확인하는 영역입니다. 현재는 수급·공시 데이터가 연결되지 않아 판단을 제공하지 않습니다.']];
const sellReasons=[['차트','추세 약화 여부 확인','예시에서는 하락 흐름이 이어진 상황을 가정했습니다. 실제 판단에는 거래량과 주요 가격대의 이탈 여부를 함께 확인해야 합니다.'],['실적','수익성 변화 점검','원가 상승으로 이익률이 낮아진 상황을 가정했습니다. 실제 기업의 재무 상태나 실적을 분석한 결과가 아닙니다.'],['시장·업종','시장 대비 약세 여부 확인','예시에서는 업종의 흐름이 시장보다 약한 상황을 가정했습니다. 실제 시장 데이터와 연결된 판단이 아닙니다.'],['수급·공시','위험 요인의 재확인','증자, 부채, 소송 등은 확인해야 할 항목의 예시입니다. 해당 가상 종목에서 실제 사건이 발생했다는 의미가 아닙니다.']];
export const stocks=[
{id:'b1',name:'가상반도체',sector:'반도체',type:'buy',base:42000,trend:105,seed:1,summary:'추세 개선과 이익 성장 점검'},
{id:'b2',name:'가상모빌리티',sector:'자동차',type:'buy',base:68000,trend:150,seed:2,summary:'업종 강세와 현금흐름 점검'},
{id:'b3',name:'가상헬스케어',sector:'헬스케어',type:'buy',base:27000,trend:60,seed:3,summary:'가격 회복과 실적 개선 점검'},
{id:'s1',name:'가상리테일',sector:'유통',type:'sell',base:37000,trend:-80,seed:4,summary:'추세 약화와 수익성 점검'},
{id:'s2',name:'가상케미컬',sector:'화학',type:'sell',base:54000,trend:-110,seed:5,summary:'업종 약세와 비용 부담 점검'},
{id:'s3',name:'가상네트웍스',sector:'통신장비',type:'sell',base:23000,trend:-45,seed:6,summary:'반등 강도와 실적 불확실성 점검'}
].map(s=>({...s,reasons:s.type==='buy'?buyReasons:sellReasons,risk:s.type==='buy'?'상승 흐름이 계속된다는 보장은 없습니다. 시장 급락, 실적 미달, 고평가 가능성은 긍정적인 근거와 함께 확인해야 합니다.':'약세가 일시적이라면 매도 후 반등을 놓칠 수 있습니다. 보유 가격·투자 기간·세금·거래 비용에 따라 판단이 달라집니다.',invalidate:s.type==='buy'?'추세가 무너지거나 실적 전망이 악화되면 선정 근거를 재검토합니다. 구체적인 조건은 실제 데이터 연결 후 산출해야 합니다.':'가격과 거래량이 회복되거나 실적 전망이 개선되면 매도 검토 근거를 재평가합니다.'}));
export function candles(stock){let previous=stock.base;return Array.from({length:60},(_,i)=>{const open=previous;const close=Math.round((stock.base+stock.trend*(i+1)+Math.sin(i*.7+stock.seed)*stock.base*.012)/10)*10;previous=close;return{day:i+1,open,close,high:Math.max(open,close)+Math.round(stock.base*.006),low:Math.min(open,close)-Math.round(stock.base*.006),volume:Math.round(100000+(Math.sin(i*.9+stock.seed)+1)*120000+i*500)};});}
