; 공지 — 3초 타이머 0x46ceaa: rand%10==0 이면 server_broadcast.txt 목록에서 한 줄을 rand%100 으로 골라 0x45d087(타입, 글)로 전체에 보낸다
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x46ceaa-0x46cf7c ----
46ceaa  push	ebp
46ceab  mov	ebp, esp
46cead  sub	esp, 28
46ceb0  mov	eax, 3435973836
46ceb5  mov	dword ptr [ebp - 28], eax
46ceb8  mov	dword ptr [ebp - 24], eax
46cebb  mov	dword ptr [ebp - 20], eax
46cebe  mov	dword ptr [ebp - 16], eax
46cec1  mov	dword ptr [ebp - 12], eax
46cec4  mov	dword ptr [ebp - 8], eax
46cec7  mov	dword ptr [ebp - 4], eax
46ceca  call	0x476d82 <.text+0x75d82>
46cecf  cdq
46ced0  mov	ecx, 10
46ced5  idiv	ecx
46ced7  test	edx, edx
46ced9  jne	0x46cf7c <.text+0x6bf7c>
46cedf  call	0x476d82 <.text+0x75d82>
46cee4  and	eax, 2147483649
46cee9  jns	0x46cef0 <.text+0x6bef0>
46ceeb  dec	eax
46ceec  or	eax, -2
46ceef  inc	eax
46cef0  test	eax, eax
46cef2  je	0x46cf39 <.text+0x6bf39>
46cef4  mov	edx, dword ptr [5173140]
46cefa  mov	dword ptr [ebp - 8], edx
46cefd  cmp	dword ptr [ebp - 8], 0
46cf01  je	0x46cf37 <.text+0x6bf37>
46cf03  call	0x476d82 <.text+0x75d82>
46cf08  cdq
46cf09  mov	ecx, 100
46cf0e  idiv	ecx
46cf10  test	edx, edx
46cf12  jne	0x46cf2c <.text+0x6bf2c>
46cf14  mov	edx, dword ptr [ebp - 8]
46cf17  mov	eax, dword ptr [edx + 4]
46cf1a  push	eax
46cf1b  mov	ecx, dword ptr [ebp - 8]
46cf1e  movsx	edx, byte ptr [ecx]
46cf21  push	edx
46cf22  call	0x45d087 <.text+0x5c087>
46cf27  add	esp, 8
46cf2a  jmp	0x46cf37 <.text+0x6bf37>
46cf2c  mov	eax, dword ptr [ebp - 8]
46cf2f  mov	ecx, dword ptr [eax + 12]
46cf32  mov	dword ptr [ebp - 8], ecx
46cf35  jmp	0x46cefd <.text+0x6befd>
46cf37  jmp	0x46cf7c <.text+0x6bf7c>
46cf39  mov	edx, dword ptr [5165604]
46cf3f  mov	dword ptr [ebp - 8], edx
46cf42  cmp	dword ptr [ebp - 8], 0
46cf46  je	0x46cf7c <.text+0x6bf7c>
46cf48  call	0x476d82 <.text+0x75d82>
46cf4d  cdq
46cf4e  mov	ecx, 100
46cf53  idiv	ecx
46cf55  test	edx, edx
46cf57  jne	0x46cf71 <.text+0x6bf71>
46cf59  mov	edx, dword ptr [ebp - 8]
46cf5c  mov	eax, dword ptr [edx + 4]
46cf5f  push	eax
46cf60  mov	ecx, dword ptr [ebp - 8]
46cf63  movsx	edx, byte ptr [ecx]
46cf66  push	edx
46cf67  call	0x45d087 <.text+0x5c087>
46cf6c  add	esp, 8
46cf6f  jmp	0x46cf7c <.text+0x6bf7c>
46cf71  mov	eax, dword ptr [ebp - 8]
46cf74  mov	ecx, dword ptr [eax + 8]
46cf77  mov	dword ptr [ebp - 8], ecx
46cf7a  jmp	0x46cf42 <.text+0x6bf42>
46cf7c  mov	edx, dword ptr [5173028]
