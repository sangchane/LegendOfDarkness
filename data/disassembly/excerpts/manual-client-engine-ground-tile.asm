; 원작 2005 Legend.exe — 바닥 타일
; 바닥 그림은 seo.dat 의 tilea.bmp(겨울 tileas.bmp) 한 덩어리: 타일 하나 56×27=1512바이트(8비트 색번호)가 이어진다.
; 줄 너비 표 0x6a2fa6 = 0,4,8,…,56,…,4 (27줄 마름모) — 이 표의 누적으로 줄 시작을 만든다.
; 칸의 바닥 번호 N 은 (N−1) 번째 타일(0 은 그리지 않음). 바닥은 배경 버퍼에 칸 단위로 미리 그린다(0x5456e4).

; 마름모 줄 시작 누적 + tilea.bmp 열기
552870  mov dword ptr [ebx], 8899844
552876  xor eax, eax
552878  mov word ptr [7768040], ax
55287e  mov ecx, 1
552883  movzx edx, word ptr [2*ecx + 7767982]
55288b  movzx eax, word ptr [2*ecx + 6959014]
552893  add edx, eax
552895  mov word ptr [2*ecx + 7767984], dx
55289d  add ecx, 1
5528a0  cmp ecx, 27
5528a3  jl 0x552883
5528a5  call 0x4bde40
5528aa  push 8899904   ;; "tilea.bmp"
5528af  mov ecx, eax
5528b1  call 0x4bd600
5528b6  mov dword ptr [ebx + 4], eax
5528b9  test eax, eax
5528bb  je 0x5528d0
5528bd  call 0x4bde40
5528c2  mov edx, dword ptr [ebx + 4]
5528c5  push edx
5528c6  mov ecx, eax
5528c8  call 0x4bdb60
5528cd  mov dword ptr [ebx + 8], eax

; 칸 하나 그리기: 바닥 번호 읽고 −1 → 타일
5456e4  push edi
5456e5  push esi
5456e6  push ebp
5456e7  push ebx
5456e8  sub esp, 1684
5456ee  mov ebp, ecx
5456f0  mov esi, dword ptr [esp + 1708]
5456f7  mov ebx, dword ptr [esp + 1704]
5456fe  test ebx, ebx
545700  jl 0x545722
545702  lea ecx, [ebp + 484]
545708  movzx eax, word ptr [ebp + 486]
54570f  cmp ebx, eax
545711  jge 0x545722
545713  test esi, esi
545715  jl 0x545722
545717  movzx eax, word ptr [ebp + 488]
54571e  cmp esi, eax
545720  jl 0x545730
545722  mov eax, 4294967295
545727  mov dword ptr [esp + 1676], eax
54572e  jmp 0x545741
545730  push esi
545731  push ebx
545732  call 0x52bb00
545737  movzx edx, ax
54573a  mov dword ptr [esp + 1676], edx
545741  lea eax, [esp + 1608]
545886  mov edx, dword ptr [esp + 1676]
54588d  test edx, edx
54588f  jle 0x545970
545895  mov ecx, dword ptr [6947680]
54589b  mov eax, edx
54589d  add eax, -1
5458a0  movzx edx, ax
5458a3  lea edi, [esp + 16]
5458a7  mov ebp, dword ptr [ebp + 628]
5458ad  push ebp
5458ae  push edi
5458af  push edx
5458b0  call 0x552d10

; 칸 → 화면 픽셀 (가로 28, 세로 14 반칸)
53ec90  mov eax, dword ptr [esp + 4]
53ec94  push edi
53ec95  push esi
53ec96  mov edx, eax
53ec98  mov eax, dword ptr [edx + 4]
53ec9b  mov edi, dword ptr [edx]
53ec9d  mov esi, eax
53ec9f  sub esi, edi
53eca1  lea ecx, [esi + esi]
53eca4  add ecx, ecx
53eca6  add ecx, ecx
53eca8  sub ecx, esi
53ecaa  add ecx, ecx
53ecac  add ecx, ecx
53ecae  add ecx, ecx
53ecb0  add ecx, 2147483648
53ecb6  adc ecx, 2147483648
53ecbc  sar ecx
53ecbe  add eax, edi
53ecc0  add eax, eax
53ecc2  add eax, eax
53ecc4  lea esi, [eax + eax]
53ecc7  add esi, esi
53ecc9  add esi, esi
53eccb  sub esi, eax
53eccd  add esi, 2147483648
53ecd3  adc esi, 2147483648
53ecd9  sar esi
53ecdb  mov dword ptr [edx], esi
53ecdd  mov dword ptr [edx + 4], ecx
53ece0  pop esi
53ece1  pop edi
53ece2  ret
