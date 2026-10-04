; 운영자 명령 — 채팅 첫 글자 '#' 이면 0x464d54 로 간다. 0x464d54 는 char+0x1C(아이디등급)가 0 이면 아무것도 안 한다. 채팅 '@노바찡짱야' 는 누구든 char+0x1C 를 0x88 로 만든다(뒷문)
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x4668f3-0x46696b ----
4668f3  cmp	ecx, 64
4668f6  jne	0x466938 <.text+0x65938>
4668f8  push	4787440   ;; "노바찡짱야"
4668fd  lea	edx, [ebp - 5447]
466903  push	edx
466904  call	0x477480 <.text+0x76480>
466909  add	esp, 8
46690c  test	eax, eax
46690e  jne	0x46691d <.text+0x6591d>
466910  mov	eax, dword ptr [ebp - 2888]
466916  mov	ecx, dword ptr [eax + 16]
466919  mov	byte ptr [ecx + 28], -120
46691d  mov	edx, dword ptr [ebp - 2888]
466923  mov	eax, dword ptr [edx + 16]
466926  movsx	ecx, byte ptr [eax + 28]
46692a  test	ecx, ecx
46692c  jne	0x466933 <.text+0x65933>
46692e  jmp	0x466a25 <.text+0x65a25>
466933  jmp	0x466a25 <.text+0x65a25>
466938  movsx	edx, byte ptr [ebp - 5448]
46693f  cmp	edx, 42
466942  jne	0x466949 <.text+0x65949>
466944  jmp	0x466a25 <.text+0x65a25>
466949  movsx	eax, byte ptr [ebp - 5448]
466950  cmp	eax, 35
466953  jne	0x466970 <.text+0x65970>
466955  lea	ecx, [ebp - 5447]
46695b  push	ecx
46695c  mov	edx, dword ptr [ebp - 2888]
466962  push	edx
466963  call	0x464d54 <.text+0x63d54>
466968  add	esp, 8
46696b  jmp	0x466a25 <.text+0x65a25>

; ---- 0x464da7-0x464de8 ----
464da7  lea	eax, [ebp - 520]
464dad  push	eax
464dae  lea	ecx, [ebp - 1032]
464db4  push	ecx
464db5  push	4785728   ;; "%[^ ] %[^"
464dba  mov	edx, dword ptr [ebp + 12]
464dbd  push	edx
464dbe  call	0x477441 <.text+0x76441>
464dc3  add	esp, 16
464dc6  cmp	eax, 2
464dc9  je	0x464dd0 <.text+0x63dd0>
464dcb  jmp	0x4664e9 <.text+0x654e9>
464dd0  mov	eax, dword ptr [ebp + 8]
464dd3  mov	ecx, dword ptr [eax + 16]
464dd6  movsx	edx, byte ptr [ecx + 28]
464dda  test	edx, edx
464ddc  jne	0x464de3 <.text+0x63de3>
464dde  jmp	0x4664e9 <.text+0x654e9>
464de3  push	4785744   ;; "스킬"
464de8  lea	eax, [ebp - 1032]
