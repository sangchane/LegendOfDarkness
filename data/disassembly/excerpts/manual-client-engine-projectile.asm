; manual-client-engine-projectile — 원작 2005(=5.99) Legend.exe, 0x29 이펙트와 날아가는 것
; 0x29: 대상 id(4)가 0 이 아니면 [대상 id][시전자 id][대상 이펙트 u16][시전자 이펙트 u16][속도 u16], 0 이면 [이펙트 u16][속도 u16][x u16][y u16].
; 이펙트 번호로 갈린다: 255 = 특수(사람·괴물에 0x51b730), 10000~11999 = 시전자→대상 날아가는 것(MovingEffectObjectPane, Mefc%03d.epf, 번호-10000),
; 12000~12999 = 대상 몸에 0x51b840(번호-12000, 200, 12), 그 밖 = 대상 자리 이펙트 + (시전자≠대상이고 시전자 이펙트≠0 이면) 시전자 자리 이펙트.
; 날아가는 것: meffect.tbl(roh.dat) "distance <step> <delay>" — 칸 수 = 거리(px)/step, 칸 k 위치 = 시작 + (끝-시작)×(k+1)/(칸수+1), 칸 간격 = delay ms.
; --- 0x29 풀기
63f660  push edi
63f661  push esi
63f662  push esi
63f663  mov edi, ecx
63f665  mov esi, dword ptr [esp + 16]
63f669  mov ecx, esi
63f66b  call 0x63b640 <.text+0x214640>
63f670  mov dword ptr [edi + 16], eax
63f673  test eax, eax
63f675  je 0x63f681 <.text+0x218681>
63f677  mov ecx, esi
63f679  call 0x63b640 <.text+0x214640>
63f67e  mov dword ptr [edi + 24], eax
63f681  mov ecx, esi
63f683  call 0x63b5e0 <.text+0x2145e0>
63f688  mov word ptr [edi + 20], ax
63f68c  mov edx, dword ptr [edi + 16]
63f68f  test edx, edx
63f691  je 0x63f69e <.text+0x21869e>
63f693  mov ecx, esi
63f695  call 0x63b5e0 <.text+0x2145e0>
63f69a  mov word ptr [edi + 28], ax
63f69e  mov ecx, esi
63f6a0  call 0x63b5e0 <.text+0x2145e0>
63f6a5  mov word ptr [edi + 30], ax
63f6a9  mov edx, dword ptr [edi + 16]
63f6ac  test edx, edx
63f6ae  je 0x63f6c0 <.text+0x2186c0>
63f6b0  xor eax, eax
63f6b2  mov dword ptr [edi + 36], eax
63f6b5  mov dword ptr [edi + 32], eax
63f6b8  add esp, 4
63f6bb  pop esi
63f6bc  pop edi
63f6bd  ret 12
63f6c0  mov ecx, esi
63f6c2  call 0x63b5e0 <.text+0x2145e0>
; --- 0x29 처리: 번호 범위로 가르기
54acdd  movzx ebx, word ptr [eax + 20]
54ace1  mov dword ptr [ebp - 32], ebx
54ace4  test edx, edx
54ace6  je 0x54acec <.text+0x123cec>
54ace8  movzx ebx, word ptr [eax + 28]
54acec  movsx edi, word ptr [eax + 30]
54acf0  test edx, edx
54acf2  jne 0x54acfc <.text+0x123cfc>
54acf4  mov edx, dword ptr [eax + 32]
54acf7  mov eax, dword ptr [eax + 36]
54acfa  jmp 0x54ad00 <.text+0x123d00>
54acfc  xor eax, eax
54acfe  xor edx, edx
54ad00  mov ecx, dword ptr [ebp - 32]
54ad03  cmp ecx, 255
54ad09  jne 0x54ad9a <.text+0x123d9a>
54ad0f  mov eax, dword ptr [ebp - 28]
54ad12  test eax, eax
54ad14  je 0x54b03d <.text+0x12403d>
54ad1a  test esi, esi
54ad1c  je 0x54b03d <.text+0x12403d>
54ad22  mov eax, dword ptr [ebp - 36]
54ad25  mov ecx, dword ptr [eax + 624]
54ad9a  cmp ecx, 10000
54ada0  jl 0x54af2e <.text+0x123f2e>
54ada6  cmp ecx, 12000
54adac  jge 0x54af2e <.text+0x123f2e>
54adb2  mov eax, dword ptr [ebp - 36]
54adb5  mov ecx, dword ptr [eax + 624]
54adbb  push esi
54af2e  cmp ecx, 12000
54af34  jl 0x54afa9 <.text+0x123fa9>
54af36  cmp ecx, 13000
54af3c  jge 0x54af9b <.text+0x123f9b>
54af3e  mov eax, dword ptr [ebp - 36]
54af7e  mov edx, dword ptr [ebp - 32]
54af81  add edx, 4294955296
54af87  push 12
54af89  push 200
54af8e  push edx
54af8f  mov ecx, eax
54af91  call 0x51b840 <.text+0xf4840>
54afe7  mov eax, dword ptr [ebp - 28]
54afea  test eax, eax
54afec  je 0x54b03d <.text+0x12403d>
54afee  test esi, esi
54aff0  je 0x54b03d <.text+0x12403d>
54aff2  movzx eax, bx
54aff5  test eax, eax
54aff7  je 0x54b03d <.text+0x12403d>
54aff9  mov edx, dword ptr [ebp - 28]
54affc  cmp edx, esi
54affe  je 0x54b03d <.text+0x12403d>
54b000  xor edx, edx
54b002  mov dword ptr [ebp - 136], edx
; --- 날아가는 것: distance 방식 — 칸 수와 칸 간격
59ff18  mov ebp, dword ptr [esp + 192]
59ff1f  mov ebp, dword ptr [ebp + 16]
59ff22  mov eax, dword ptr [esp + 172]
59ff29  sub eax, dword ptr [esp + 164]
59ff30  mov edx, dword ptr [esp + 168]
59ff37  sub edx, dword ptr [esp + 160]
59ff3e  imul eax, eax
59ff41  imul edx, edx
59ff44  add eax, edx
59ff46  mov dword ptr [esp + 24], eax
59ff4a  fild dword ptr [esp + 24]
59ff4e  fsqrt
59ff50  fstp qword ptr [esp + 48]
59ff54  movsd xmm0, qword ptr [esp + 48] # xmm0 = mem[0],zero
59ff5a  cvttsd2si edi, xmm0
59ff5e  test ebp, ebp
59ff60  jle 0x59ff6b <.text+0x178f6b>
59ff62  mov eax, edi
59ff64  cdq
59ff65  idiv ebp
59ff67  mov ecx, eax
59ff69  jmp 0x59ff6d <.text+0x178f6d>
59ff6b  xor ecx, ecx
59ff6d  mov dword ptr [ebx + 528], ecx
59ff73  cmp ecx, 16
59ffc6  mov edx, dword ptr [esp + 192]
59ffcd  mov ebp, dword ptr [edx + 20]
59ffd0  mov dword ptr [ebx + 536], ebp
59ffd6  xor eax, eax
59ffd8  test ecx, ecx
59ffda  jle 0x5a0099 <.text+0x179099>
