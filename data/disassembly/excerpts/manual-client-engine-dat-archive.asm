; 원작 2005(=5.99) Legend.exe — .dat 아카이브 목차와 파일 찾기
; 목차 = u32 개수 N, 그 뒤 17바이트 칸 N개(u32 시작 위치 + 13바이트 이름). 마지막 칸은 빈 이름 + 파일 크기(끝 표시).
; 이름은 대소문자 무시(_stricmp 0x6706a0) 이진 탐색(bsearch 0x6708d6, 칸 크기 17). 크기 = 다음 칸 시작 − 내 시작.
; 열기 순서: Legend.dat(없으면 DarkAges.dat) → seo → khan → setoa → national → ia → hades → roh → cious → khan2. 오프셋은 10진수.

; 열기 순서 (0x510663~)
510670  call 0x4bddb0
510675  push 0
510677  push 20
510679  push 6947572   ;; "Legend.dat"
51067e  mov ecx, eax
510680  call 0x4be000
510685  test eax, eax
510687  jne 0x51071b
51071b  call 0x4bde50
510720  test eax, eax
510722  je 0x510e2c
510728  call 0x4bde50
51072d  mov edx, dword ptr [8789660]
510733  push edx
510734  push 20
510736  push 6947548   ;; "seo.dat"
51073b  mov ecx, eax
51073d  call 0x4be000
510742  test eax, eax
5107bb  call 0x4bdc20
5107c0  test eax, eax
5107c2  je 0x510e0e
5107c8  call 0x4bdc20
5107cd  mov edx, dword ptr [8789664]
5107d3  push edx
5107d4  push 20
5107d6  push 6947536   ;; "khan.dat"
5107db  mov ecx, eax
5107dd  call 0x4be000
5107e2  test eax, eax
5107e4  jne 0x51085b

; 목차 읽기 — 개수가 -1 이 아니면 평문 목차 (0x4be000 안)
4be165  mov ebx, dword ptr [edx]
4be167  cmp ebx, -1
4be16a  je 0x4be1ab
4be16c  mov edi, ecx
4be16e  mov dword ptr [edi + 52], ebx
4be171  lea ecx, [edx + 4]
4be174  mov dword ptr [edi + 12], ecx
4be177  lea esi, [ebx + ebx]
4be17a  add esi, esi
4be17c  add esi, esi
4be17e  add esi, esi
4be180  add esi, ebx
4be182  mov edx, dword ptr [esi + edx - 13]
4be186  mov dword ptr [edi + 64], edx
4be189  mov byte ptr [edi + 68], 0
4be18d  mov eax, 1

; 이름으로 찾기 — bsearch(이름, 목차+4, 개수, 17, 비교 0x4bd260)
4bd637  lea esi, [esp + 28]
4bd63b  mov dl, byte ptr [edi]
4bd63d  add edi, 1
4bd640  mov byte ptr [esi], dl
4bd642  add esi, 1
4bd645  test dl, dl
4bd647  jne 0x4bd63b
4bd649  lea eax, [esp + 24]
4bd64d  mov dword ptr [esp], eax
4bd650  mov edx, dword ptr [ebp + 12]
4bd653  mov dword ptr [esp + 4], edx
4bd657  mov esi, dword ptr [ebp + 52]
4bd65a  mov dword ptr [esp + 8], esi
4bd65e  mov dword ptr [esp + 12], 17
4bd666  mov dword ptr [esp + 16], 4969056
4bd66e  call 0x6708d6
4bd673  test eax, eax

; 찾은 칸 → 열린 핸들 {1, 시작, 크기=다음 시작−시작, 칸}
4bd786  mov esi, ebx
4bd788  mov ebp, dword ptr [esp + 52]
4bd78c  mov ebx, dword ptr [esp + 56]
4bd790  mov dword ptr [ecx + 4*esi], 1
4bd797  mov ecx, dword ptr [eax]
4bd799  mov edi, dword ptr [ebp + 56]
4bd79c  mov dword ptr [edi + 4*esi + 4], ecx
4bd7a0  mov ecx, dword ptr [eax + 17]
4bd7a3  sub ecx, dword ptr [eax]
4bd7a5  mov edi, dword ptr [ebp + 56]
4bd7a8  mov dword ptr [edi + 4*esi + 8], ecx
4bd7ac  mov edi, dword ptr [ebp + 56]
4bd7af  mov dword ptr [edi + 4*esi + 12], eax
4bd7b3  mov eax, dword ptr [ebp + 56]
4bd7b6  add eax, edx
4bd7b8  add esp, 64
4bd7bb  pop ebp
4bd7bc  pop esi
4bd7bd  pop edi
4bd7be  ret 4

; 비교 함수: 칸+4(이름) 끼리 _stricmp
4bd260  sub esp, 12
4bd263  mov ecx, dword ptr [esp + 20]
4bd267  mov eax, dword ptr [esp + 16]
4bd26b  lea edx, [eax + 4]
4bd26e  mov dword ptr [esp], edx
4bd271  lea eax, [ecx + 4]
4bd274  mov dword ptr [esp + 4], eax
4bd278  call 0x6706a0
4bd27d  add esp, 12
4bd280  ret
