---
파일: "palf.tbl"
아카이브: "khan.dat"
줄수: 42
바이트: 304
인코딩: cp949
---

# palf.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/khan|khan.dat]]

## 내용 (앞 40줄 / 전체 42줄)

```
256 1
270 2
271 2
272 2
273 2
274 2
275 2
276 2
277 2
278 2
279 2
280 2
281 2
282 2
283 2
284 2
316 3
317 3
318 3
319 3
352 4 -1
353 4 -1
354 4 -1
355 4 -1
360 5
361 5
362 5
363 5
364 5
365 5
366 5
367 5
368 5
369 5
370 5
371 5
372 5
373 5
374 5
391 6
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/khan/khan.dat <폴더> palf
iconv -f CP949 -t UTF-8 <폴더>/…/palf.tbl
```
