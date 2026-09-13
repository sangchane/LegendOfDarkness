---
파일: "effpal.tbl"
아카이브: "roh.dat"
줄수: 76
바이트: 611
인코딩: cp949
---

# effpal.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/roh|roh.dat]]

## 내용 (앞 40줄 / 전체 76줄)

```
142 1
160 2
161 2
162 2
163 2
164 2
165 2
166 2
167 2
168 2
169 2
170 2
171 2
172 2
173 2
174 2
175 2
176 2
177 2
178 2
179 2
180 2
181 2
182 2
183 2
184 2
192 2
194 2
200 5
202 5
203 5
204 5
205 5
206 5
207 5
209 1004
210 1001
213 6
69 1000
215 1007
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/roh/roh.dat <폴더> effpal
iconv -f CP949 -t UTF-8 <폴더>/…/effpal.tbl
```
