; 원작 2005 Legend.exe — 맵 화면 창(보이는 칸)과 스크롤
; MapPane 을 (32,32,24,24) 로 만든다 → 보이는 칸 창 24×24. 내 위치가 바뀌면 창 원점 = 내 칸 − 창/2 (가운데 맞춤).
; 그리기 쪽은 창 원점 기준 −12..+12 칸을 훑어 바닥을 다시 깐다. 맵 화면 사각형 전역 0x6a10c0..cc = (2,2)-(618,310)(바뀌는 곳 없음).

; MapPane 생성 (가로 32·세로 32 + 창 24·24)
52268a  push 0
52268c  push 8468
522691  call 0x51cdb0
522696  mov dword ptr [ebp - 28], eax
522699  add esp, 8
52269c  test eax, eax
52269e  je 0x52284a
5226a4  mov dword ptr [ebp - 4], 1
5226ab  mov edx, 32
5226b0  mov eax, 24
5226b5  push eax
5226b6  push eax
5226b7  push edx
5226b8  push edx
5226b9  mov ecx, dword ptr [ebp - 28]
5226bc  call 0x53cfe0

; 생성자: 창 크기 저장
53d327  movsx ebx, word ptr [ebp + 20]
53d32b  movsx ecx, word ptr [ebp + 16]
53d32f  mov esi, dword ptr [ebp - 148]
53d335  xor eax, eax
53d337  mov byte ptr [esi + 8460], al
53d33d  mov byte ptr [esi + 8461], al
53d343  mov byte ptr [esi + 652], al
53d349  mov byte ptr [esi + 477], al
53d34f  mov byte ptr [esi + 478], al
53d355  mov byte ptr [esi + 479], al
53d35b  mov byte ptr [esi + 480], al
53d361  mov dl, 1
53d363  mov byte ptr [esi + 481], dl
53d369  mov dword ptr [esi + 560], eax
53d36f  mov dword ptr [esi + 556], eax
53d375  mov dword ptr [esi + 564], ecx
53d37b  mov dword ptr [esi + 568], ebx
53d381  mov dword ptr [esi + 572], eax
53d387  mov dword ptr [esi + 576], eax

; 내 칸을 가운데로 — 원점 = 내 칸 − 창/2
5493e5  mov edx, dword ptr [6951108]
5493eb  lea edi, [edx - 56]
5493ee  mov dword ptr [esi + 324], edi
5493f4  mov eax, dword ptr [6951104]
5493f9  lea edi, [eax - 28]
5493fc  mov dword ptr [esi + 320], edi
549402  mov edi, dword ptr [6951112]
549408  mov ecx, dword ptr [6951116]
54940e  add eax, 26
549411  mov dword ptr [esp + 8], eax
549415  add edx, 54
549418  mov dword ptr [esp + 12], edx
54941c  add edi, 26
54941f  mov dword ptr [esp + 16], edi
549423  add ecx, 54
549426  mov dword ptr [esp + 20], ecx
54942a  lea ecx, [esi + 12]
54942d  lea eax, [esp + 8]
549431  push eax
549432  call 0x4d4610
549437  lea eax, [esi + 88]
54943a  push eax
54943b  mov ecx, esi
54943d  call 0x5e0850
549442  mov eax, dword ptr [esi + 564]
549448  add eax, 2147483648
54944d  adc eax, 2147483648
549452  sar eax
549454  neg eax
549456  add eax, ebx
549458  mov dword ptr [esi + 556], eax
54945e  mov edx, dword ptr [esi + 568]
549464  add edx, 2147483648
54946a  adc edx, 2147483648
549470  sar edx
549472  neg edx
549474  add edx, ebp
549476  mov dword ptr [esi + 560], edx
54947c  lea edi, [esp + 24]
549480  push edi
549481  mov ecx, esi
549483  call 0x544284
549488  mov ecx, dword ptr [esi + 624]

; 그리기: 창 −12..+12 칸마다 바닥 그리기
542f87  mov esi, dword ptr [ebp - 288]
542f8d  add esi, -12
542f90  mov edx, dword ptr [ebp - 280]
542f96  add edx, 12
542f99  cmp esi, edx
542f9b  jge 0x5437db
542fa1  mov eax, dword ptr [ebp - 276]
542fa7  add eax, 12
542faa  mov ebx, dword ptr [ebp - 284]
542fb0  add ebx, -12
542fb3  cmp ebx, eax
542fb5  jge 0x5437d0
542fbb  push esi
542fbc  push ebx
542fbd  mov ecx, dword ptr [ebp - 28]
542fc0  call 0x5456e4
542fc5  add ebx, 1
542fc8  mov eax, dword ptr [ebp - 276]
542fce  add eax, 12
542fd1  cmp ebx, eax
542fd3  jl 0x542fbb
