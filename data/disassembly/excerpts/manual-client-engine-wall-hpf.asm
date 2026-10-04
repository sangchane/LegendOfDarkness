; 원작 2005 Legend.exe — 벽 그림 stc%05d.hpf 읽기
; 맵 깃발(0x542af0)의 0x80 이 켜져 있으면 먼저 sts%05d.hpf(겨울판)를 찾고 없으면 stc 로. 둘 다 ia.dat 에서 찾는다.
; HPF 는 압축되어 있다(머리 55 AA 02 FF, 풀기 0x46db90). 푼 뒤 앞 8바이트를 건너뛰고, 너비 28px 고정으로 쓴다.

; 파일 이름 고르기·읽기·풀기
4e93f2  mov ecx, dword ptr [7556660]
4e93f8  call 0x542af0
4e93fd  movzx edx, al
4e9400  test edx, 128
4e9406  je 0x4e9482
4e9408  mov al, byte ptr [esi + 7565344]
4e940e  test al, al
4e9410  jne 0x4e944a
4e9412  lea eax, [esp + 28]
4e9416  mov dword ptr [esp], eax
4e9419  mov dword ptr [esp + 4], 8829920   ;; "sts%05d.hpf"
4e9421  mov dword ptr [esp + 8], esi
4e9425  call 0x66ee00
4e942a  call 0x4bdb80
4e942f  lea edx, [esp + 28]
4e9433  push edx
4e9434  mov ecx, eax
4e9436  call 0x4bd600
4e943b  mov edi, eax
4e943d  test edi, edi
4e943f  je 0x4e944e
4e9441  mov byte ptr [esi + 7565344], 1
4e9448  jmp 0x4e94b5
4e944a  cmp al, 1
4e944c  je 0x4e9412
4e944e  mov byte ptr [esi + 7565344], 2
4e9455  lea eax, [esp + 28]
4e9459  mov dword ptr [esp], eax
4e945c  mov dword ptr [esp + 4], 8829952   ;; "stc%05d.hpf"
4e9464  mov dword ptr [esp + 8], esi
4e9468  call 0x66ee00
4e946d  call 0x4bdb80
4e9472  lea edx, [esp + 28]
4e9476  push edx
4e9477  mov ecx, eax
4e9479  call 0x4bd600
4e947e  mov edi, eax
4e9480  jmp 0x4e94ad
4e9482  lea eax, [esp + 28]
4e9486  mov dword ptr [esp], eax
4e9489  mov dword ptr [esp + 4], 8829952   ;; "stc%05d.hpf"
4e9491  mov dword ptr [esp + 8], esi
4e9495  call 0x66ee00
4e949a  call 0x4bdb80
4e949f  lea edx, [esp + 28]
4e94a3  push edx
4e94a4  mov ecx, eax
4e94a6  call 0x4bd600
4e94ab  mov edi, eax
4e94ad  test edi, edi
4e94af  je 0x4e961e
4e94b5  call 0x4bdb80
4e94ba  push edi
4e94bb  mov ecx, eax
4e94bd  call 0x4bdb60
4e94c2  mov ebp, eax
4e94c4  call 0x4bdb80
4e94c9  push ebp
4e94ca  push 7586336
4e94cf  push edi
4e94d0  mov ecx, eax
4e94d2  call 0x4bd800
4e94d7  call 0x4bdb80
4e94dc  push edi
4e94dd  mov ecx, eax
4e94df  call 0x4bd7d0
4e94e4  xor edx, edx
4e94e6  mov dword ptr [esp + 60], edx
4e94ea  mov dword ptr [esp + 64], edx
4e94ee  mov ecx, dword ptr [6864416]
4e94f4  lea edx, [esp + 60]
4e94f8  lea edi, [esp + 64]
4e94fc  push edi
4e94fd  push edx
4e94fe  push ebp
4e94ff  push 7586336
4e9504  call 0x46db90
4e9509  mov dword ptr [esp], 16

; 머리 8바이트 건너뛰고 너비 28
4e951b  mov edx, dword ptr [esp + 64]
4e951f  add edx, -8
4e9522  mov word ptr [ebp + 2], dx
4e9526  mov ecx, dword ptr [6847040]
4e952c  mov edi, dword ptr [esp + 64]
4e9530  add edi, -8
4e9533  push edi
4e9534  call 0x5545c0
4e9539  mov dword ptr [ebp + 4], eax
4e953c  mov ecx, dword ptr [7556660]
4e9542  call 0x542af0
4e9547  and al, -128
4e9549  mov byte ptr [ebp + 8], al
4e954c  mov edx, dword ptr [6870976]
4e9552  mov dword ptr [esp + 72], edx
4e9556  mov ecx, dword ptr [7556660]
4e955c  call 0x542af0
4e9561  movzx edx, al
4e9564  and edx, 128
4e956a  push edx
4e956b  push esi
4e956c  mov ecx, dword ptr [esp + 80]
4e9570  call 0x5de3f0
4e9575  mov dword ptr [ebp + 12], eax
4e9578  mov edi, dword ptr [ebp + 4]
4e957b  mov esi, dword ptr [esp + 60]
4e957f  add esi, 8
4e9582  mov ecx, dword ptr [esp + 64]
4e9586  add ecx, -8
4e9589  rep  movsb byte ptr es:[edi], byte ptr [esi]
4e958b  mov edx, dword ptr [esp + 76]
4e958f  mov ecx, dword ptr [edx + 12]
4e9592  push ebp
4e9593  push 1
4e9595  push 0
4e9597  mov esi, dword ptr [ecx]
4e9599  mov edx, dword ptr [esi + 16]
4e959c  call edx
4e959e  test bl, bl
4e95a0  jne 0x4e95aa
4e95a2  mov dword ptr [esp], ebp
4e95a5  call 0x66e5a8
4e95aa  mov eax, dword ptr [esp + 76]
4e95ae  mov ecx, dword ptr [eax + 12]
4e95b1  push 0
4e95b3  mov edx, dword ptr [ecx]
4e95b5  mov eax, dword ptr [edx + 12]
4e95b8  call eax
4e95ba  mov ebx, dword ptr [eax + 4]
4e95bd  mov edx, dword ptr [esp + 108]
4e95c1  mov dword ptr [edx], ebx
4e95c3  mov ebx, 28
4e95c8  mov dword ptr [edx + 4], ebx
