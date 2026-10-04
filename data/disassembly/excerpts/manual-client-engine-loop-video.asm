; 원작 2005(=5.99) Legend.exe — 메인 루프·타이머·화면(DirectDraw)
; 증명: (1) WinMain 이 640x480·전체화면으로 시작하고 16비트 색만 받는다 (2) 타이머 해상도 5ms, 메시지 루프는 PeekMessage + 타이머 큐
; (3) ScreenPane 이 50ms 마다(=초당 20장) 다시 그리고, 바뀐 것이 있을 때만 Flip/Blt 한다. 오프셋은 10진수(맥 objdump), 주소는 16진수.

; --- WinMain: 640x480, 전체화면 깃발 1, 창 클래스 Darkages
618d86  mov dword ptr [ebp - 148], 640
618d90  mov dword ptr [ebp - 144], 480
618d9a  mov dword ptr [ebp - 140], 1
618da4  mov dword ptr [ebp - 136], 9058944   ;; "Darkages"
618dae  mov dword ptr [ebp - 4], 0
618db5  mov edx, dword ptr [ebp + 20]
618db8  mov ebx, dword ptr [ebp + 12]
618dbb  mov esi, dword ptr [ebp + 8]
618dbe  mov edi, 7687520
618dc3  lea eax, [ebp - 148]
618dc9  push eax
618dca  push edx
618dcb  mov ecx, dword ptr [ebp + 16]

; --- 0x5110a6: [+8]!=0 이면 '창 모드 아님' → 0x634de0(Video System) 에 넘김
5110a6  mov ecx, dword ptr [6860224]
5110ac  mov eax, dword ptr [ebp + 24]
5110af  mov edx, dword ptr [eax + 8]
5110b2  test edx, edx
5110b4  je 0x5110ba <.text+0xea0ba>
5110b6  xor edx, edx
5110b8  jmp 0x5110bf <.text+0xea0bf>
5110ba  mov edx, 1
5110bf  mov eax, dword ptr [ebp + 24]
5110c2  mov esi, dword ptr [eax]
5110c4  mov ebx, dword ptr [eax + 4]

; --- 0x634e62: 색 깊이 16 고정으로 DirectDraw 초기화 0x489ad0 호출
634e62  mov ebx, dword ptr [ebp + 20]
634e65  mov esi, dword ptr [ebp + 16]
634e68  mov ecx, dword ptr [6870208]
634e6e  xor eax, eax
634e70  test ebx, ebx
634e72  sete al
634e75  mov edi, dword ptr [ebp - 28]
634e78  lea edi, [edi + 40]
634e7b  xor edx, edx
634e7d  cmp ebx, 1
634e80  setne dl
634e83  push edx
634e84  push edi
634e85  push eax
634e86  push 16
634e88  push esi

; --- 0x489ad0: 전체화면이면 SetCooperativeLevel 0x13(EXCLUSIVE|FULLSCREEN|ALLOWREBOOT), 아니면 8(NORMAL)
489bc6  test esi, esi
489bc8  jne 0x489bd1 <.text+0x62bd1>
489bca  mov eax, 8
489bcf  jmp 0x489bd6 <.text+0x62bd6>
489bd1  mov eax, 19
489bd6  mov ebx, dword ptr [ebp + 8]
489bd9  mov edx, dword ptr [edi + 40]
489bdc  push eax
489bdd  push ebx
489bde  push edx
489bdf  mov eax, dword ptr [edx]

; --- 기본(1차) 표면: 전체화면은 뒷버퍼 1장 + Flip(caps 0x218), 창 모드는 기본 표면만(caps 0x200) + 클리퍼
489cf1  test esi, esi
489cf3  jne 0x489d0b <.text+0x62d0b>
489cf5  mov dword ptr [ebp - 1004], 1
489cff  mov dword ptr [ebp - 904], 512
489d09  jmp 0x489d49 <.text+0x62d49>
489d0b  mov eax, dword ptr [edi + 40]
489d0e  mov edx, dword ptr [ebp + 20]
489d11  push edx
489d12  mov edx, dword ptr [ebp + 16]
489d15  push edx
489d16  mov edx, dword ptr [ebp + 12]
489d2b  mov dword ptr [ebp - 1004], 33
489d35  mov dword ptr [ebp - 904], 536
489d3f  mov dword ptr [ebp - 988], 1
489d49  lea eax, [ebp - 888]

; --- 픽셀 형식: 32→4, 24→3, 16비트 G마스크 0x7E0 이면 1(565) 아니면 2(555)
489dbf  mov dword ptr [edi + 56], 0
489dc6  mov eax, dword ptr [ebp - 804]
489dcc  cmp eax, 32
489dcf  jne 0x489dde <.text+0x62dde>
489dd1  mov dword ptr [edi + 56], 4
489dd8  mov eax, dword ptr [ebp - 804]
489dde  cmp eax, 24
489de1  jne 0x489df0 <.text+0x62df0>
489de3  mov dword ptr [edi + 56], 3
489dea  mov eax, dword ptr [ebp - 804]
489df0  cmp eax, 16
489df3  jne 0x489e1b <.text+0x62e1b>
489df5  mov eax, dword ptr [ebp - 796]
489dfb  cmp eax, 2016
489e00  jne 0x489e0b <.text+0x62e0b>
489e02  mov dword ptr [edi + 56], 1
489e09  jmp 0x489e1b <.text+0x62e1b>
489e0b  mov dword ptr [edi + 56], 2
489e12  jmp 0x489e1b <.text+0x62e1b>
489e14  mov dword ptr [edi + 56], 0
489e1b  mov eax, 364
489e20  mov dword ptr [ebp - 780], eax
489e26  mov dword ptr [ebp - 416], eax

; --- 그림 표면은 시스템 메모리 offscreen (caps 0x840)
4896a3  mov dword ptr [esp], 108
4896aa  mov dword ptr [esp + 4], 7
4896b2  mov dword ptr [esp + 104], 2112
4896ba  mov dword ptr [esp + 12], eax

; --- 화면 내보내기: 전체화면이면 Flip(DDFLIP_WAIT), 창 모드면 Blt
633fc0  push esi
633fc1  mov eax, ecx
633fc3  mov ecx, dword ptr [6870208]
633fc9  test ecx, ecx
633fcb  je 0x633ff9 <.text+0x20cff9>
633fcd  mov edx, dword ptr [eax + 24]
633fd0  cmp edx, 2
633fd3  je 0x633fd9 <.text+0x20cfd9>
633fd5  test edx, edx
633fd7  jne 0x633ff9 <.text+0x20cff9>
633fd9  mov edx, dword ptr [eax + 20]
633fdc  test edx, edx
633fde  je 0x633ff9 <.text+0x20cff9>
633fe0  mov edx, dword ptr [eax + 44]
633fe3  test edx, edx
633fe5  jne 0x633ff4 <.text+0x20cff4>
633fe7  mov eax, dword ptr [eax + 36]
489730  mov edx, dword ptr [ecx + 44]
489733  mov eax, dword ptr [ecx + 48]
489736  push 1
489738  push eax
489739  push edx
48973a  mov eax, dword ptr [edx]
48973c  call dword ptr [eax + 44]
48973f  ret

; --- 타이머 해상도: timeBeginPeriod(max(5, 장치 최소) ≤ 장치 최대)
4aad87  lea edx, [ebp - 64]
4aad8a  push 8
4aad8c  push edx
4aad8d  call dword ptr [8665988]   ;; timeGetDevCaps
4aad93  mov ecx, dword ptr [ebp - 64]
4aad96  cmp ecx, 5
4aad99  jbe 0x4aad9f <.text+0x83d9f>
4aad9b  mov eax, ecx
4aad9d  jmp 0x4aada4 <.text+0x83da4>
4aad9f  mov eax, 5
4aada4  mov edx, dword ptr [ebp - 60]
4aada7  cmp eax, edx
4aada9  jbe 0x4aadaf <.text+0x83daf>
4aadab  mov ecx, edx
4aadad  jmp 0x4aadb9 <.text+0x83db9>
4aadaf  cmp ecx, 5

; --- 메인 루프: 메시지를 다 비운 뒤 타이머 큐(EventDispatcher vfunc+8) 한 번
50f360  lea eax, [esp + 8]
50f364  xor edx, edx
50f366  push 1
50f368  push edx
50f369  push edx
50f36a  push edx
50f36b  push eax
50f36c  call dword ptr [8665904]   ;; PeekMessageA
50f372  test eax, eax
50f374  je 0x50f3b9 <.text+0xe83b9>
50f376  mov edx, dword ptr [ebx + 65864]
50f37c  test edx, edx
50f37e  je 0x50f38d <.text+0xe838d>
50f380  lea eax, [esp + 8]
50f384  mov dword ptr [esp], eax
50f387  call edx
50f389  test eax, eax
50f38b  je 0x50f3a3 <.text+0xe83a3>
50f38d  lea eax, [esp + 8]
50f391  push eax
50f392  call dword ptr [8665908]   ;; TranslateMessage
50f398  lea eax, [esp + 8]
50f39c  push eax
50f39d  call dword ptr [8665744]   ;; DispatchMessageA
50f3a3  lea edx, [esp + 8]
50f3a7  xor eax, eax
50f3a9  push 1
50f3ab  push eax
50f3ac  push eax
50f3ad  push eax
50f3ae  push edx

; --- 타이머 큐: 다음 일까지 20ms 넘게 남으면 Sleep(5), 한 번에 최대 40개
4acd6b  call dword ptr [8666000]   ;; timeGetTime
4acd71  mov edx, dword ptr [ebp - 24]
4acd74  mov dword ptr [edx + 48], eax
4acd77  lea ecx, [eax + 20]
4acd7a  cmp ecx, dword ptr [edx + 52]
4acd7d  jae 0x4acd8d <.text+0x85d8d>
4acd7f  push 5
4acd81  call dword ptr [8665208]   ;; Sleep
4acd87  mov eax, dword ptr [ebp - 24]
4acd8a  mov eax, dword ptr [eax + 48]
4acd8d  mov edx, dword ptr [ebp - 24]
4acd90  mov edx, dword ptr [edx + 84]
4acd93  test edx, edx
4acdc9  mov ebx, 40
4acdce  mov edx, dword ptr [ebp - 24]

; --- ScreenPane 타이머 콜백(vtable 0x89b044 +8): id 0 → 다시 그리기 → 50ms 뒤 다시 등록 (초당 20장)
5f0d00  push edi
5f0d01  push ebx
5f0d02  push esi
5f0d03  mov ebx, ecx
5f0d05  lea ecx, [ebx - 308]
5f0d0b  mov eax, dword ptr [7838408]
5f0d10  test eax, eax
5f0d12  jle 0x5f0d1c <.text+0x1c9d1c>
5f0d14  add eax, -1
5f0d17  mov dword ptr [7838408], eax
5f0d1c  mov edi, dword ptr [esp + 16]
5f0de4  push edi
5f0de5  mov edi, ecx
5f0de7  mov ecx, dword ptr [6860224]
5f0ded  call 0x634260 <.text+0x20d260>
5f0df2  test eax, eax
5f0df4  je 0x5f0dfd <.text+0x1c9dfd>
5f0df6  mov ecx, edi
5f0df8  call 0x5f20b0 <.text+0x1cb0b0>
5f0dfd  lea ecx, [edi + 308]
5f0e03  call 0x51cea0 <.text+0xf5ea0>
5f0e08  test eax, eax
5f0e0a  je 0x5f0e0e <.text+0x1c9e0e>
5f0e0c  pop edi
5f0e0d  ret
5f0e0e  mov ecx, dword ptr [7556692]
5f0e14  xor eax, eax
5f0e16  push eax
5f0e17  push eax
5f0e18  push 50
5f0e1a  push eax
5f0e1b  push edi
5f0e1c  call 0x4ac5b0 <.text+0x855b0>
5f0e21  mov ecx, dword ptr [7556692]
5f0e27  call 0x51cea0 <.text+0xf5ea0>

; --- 다시 그리기 0x5f20b0: 바뀐 깃발 [+0x2A8] 또는 남은 횟수 [0x779ac8] 가 있을 때만 640x480 을 내보낸다
5f2253  mov esi, dword ptr [ebp - 24]
5f2256  mov al, byte ptr [esi + 680]
5f225c  cmp al, 1
5f225e  je 0x5f226d <.text+0x1cb26d>
5f2260  mov eax, dword ptr [7838408]
5f2265  test eax, eax
5f2267  je 0x5f2332 <.text+0x1cb332>
5f226d  mov eax, esi
5f226f  lea ebx, [eax + 628]
5f2275  push ebx
5f2276  call dword ptr [8665476]   ;; EnterCriticalSection
5f227c  mov al, byte ptr [esi + 681]
5f2282  cmp al, 1
5f2301  mov ecx, dword ptr [6860224]
5f2307  call 0x634260 <.text+0x20d260>
5f230c  test eax, eax
5f230e  je 0x5f231b <.text+0x1cb31b>
5f2310  mov ecx, dword ptr [6860224]
5f2316  call 0x633fc0 <.text+0x20cfc0>
5f231b  mov ecx, esi
5f231d  call 0x5f1b74 <.text+0x1cab74>
5f2322  mov eax, esi
5f2324  mov byte ptr [eax + 680], 0

; --- 반투명 50%: 두 색을 반씩 더하는 마스크 565=0xF7DE, 555=0x7BDE
633f40  push ebp
633f41  mov ebp, dword ptr [esp + 20]
633f45  mov ecx, dword ptr [esp + 16]
633f49  mov eax, dword ptr [esp + 12]
633f4d  movzx edx, word ptr [esp + 8]
633f52  test ebp, ebp
633f54  je 0x633f6e <.text+0x20cf6e>
633f56  mov ebp, edx
633f58  and ebp, 63454
633f5e  sar ebp
633f60  mov word ptr [eax], bp
633f63  and edx, 2081
633f69  mov word ptr [ecx], dx
633f6c  pop ebp
633f6d  ret
633f6e  mov ebp, edx
633f70  and ebp, 31710
633f76  sar ebp
633f78  mov word ptr [eax], bp
633f7b  and edx, 1057
633f81  mov word ptr [ecx], dx

; --- 화면 흔들기 0x5f1fd0: n 번 다시 그리며 x=rand%10, y=rand%40 만큼 밀고, 끝나면 0
5f1ff8  call 0x6705ab <.text+0x2495ab>
5f1ffd  movsx ebx, ax
5f2000  mov eax, 1717986919
5f2005  mov edx, ebx
5f2007  imul edx
5f2009  sar edx, 2
5f200c  mov edi, ebx
5f200e  sar edi, 31
5f2011  sub edx, edi
5f2013  lea edi, [edx + edx]
5f2016  add edi, edi
5f2018  add edi, edx
5f201a  add edi, edi
5f201c  sub ebx, edi
5f201e  mov word ptr [esi + 684], bx
5f2025  call 0x6705ab <.text+0x2495ab>
5f202a  movsx ebx, ax
5f202d  mov eax, 1717986919
5f2032  mov edx, ebx
5f2034  imul edx
5f2036  sar edx, 4
5f2039  mov edi, ebx
5f203b  sar edi, 31
5f203e  sub edx, edi
5f2040  lea edi, [edx + 4*edx]
5f2043  add edi, edi
5f2045  add edi, edi
5f2047  add edi, edi
5f2049  sub ebx, edi
5f204b  mov word ptr [esi + 686], bx
5f2052  mov byte ptr [esi + 680], 1
5f2081  xor edx, edx
5f2083  mov word ptr [eax + 684], dx
5f208a  mov word ptr [eax + 686], dx
5f2091  mov byte ptr [eax + 680], 1
5f2098  mov ecx, eax
5f209a  call 0x5f20b0 <.text+0x1cb0b0>
5f209f  add esp, 28
