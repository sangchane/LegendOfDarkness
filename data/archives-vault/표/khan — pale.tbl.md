---
파일: "pale.tbl"
아카이브: "khan.dat"
줄수: 46
바이트: 344
인코딩: cp949
---

# pale.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/khan|khan.dat]]

## 내용 (앞 40줄 / 전체 46줄)

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
352 4 -2
353 4 -2
354 4 -2
355 4 -2
352 6 -1
353 5 -1
354 5 -1
355 5 -1
360 7
361 7
362 7
363 7
364 7
365 7
366 7
367 7
368 7
369 7
370 7
371 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/khan/khan.dat <폴더> pale
iconv -f CP949 -t UTF-8 <폴더>/…/pale.tbl
```
