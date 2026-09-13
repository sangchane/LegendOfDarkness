---
파일: "palh.tbl"
아카이브: "khan.dat"
줄수: 59
바이트: 482
인코딩: cp949
---

# palh.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/khan|khan.dat]]

## 내용 (앞 40줄 / 전체 59줄)

```
239 1
256 3
259 2
260 2
267 4
269 4
270 5
271 5
272 5
273 5
274 5
275 5
276 5
277 5
278 5
279 5
280 5
281 5
282 5
283 5
284 5
295 2
306 6 -2
307 6 -2
308 6 -2
309 6 -2
310 6 -2
312 6 -2
316 7 
317 7 
318 7 
319 7
334 2 
340 9
352 10 -1
353 10 -1
354 10 -1
355 10 -1
352 11 -2
353 11 -2
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/khan/khan.dat <폴더> palh
iconv -f CP949 -t UTF-8 <폴더>/…/palh.tbl
```
