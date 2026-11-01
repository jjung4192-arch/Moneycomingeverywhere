# 셋 / SET — stock shortlist UI prototype
Korean, mobile-first static interface: up to three buy candidates and three sell-review candidates, click through to a candlestick/volume chart and reasons. All six names and all data are fictional fixtures, not actual security recommendations. No AI or market analysis executes. Home and details explicitly identify the mock data. Sell-review fixtures are not filtered by holdings.

Run: `python3 -m http.server 3000 --directory docs`. GitHub Pages uses main:/docs. No dependencies, keys, login, tracking or remote assets.

Chart: 20/60 synthetic trading sessions, selectable candle OHLC values, synthetic volume. Prices and changes are computed from the same fixture data. Reasons cover chart, earnings, market/sector and flow/disclosures, with opposing evidence and invalidation considerations. Actual sources and timestamps are unavailable and the UI says so. Production requires licensed price/fundamental/news data and a validated selection pipeline; do not portray this prototype as working investment analysis.
