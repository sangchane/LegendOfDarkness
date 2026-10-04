; 원작 2005 Legend.exe — 한 칸 안의 그리기 순서
; 칸마다 오브젝트 연결 목록이 있고, 넣을 때 종류 바이트 [obj+0x1AC] 가 작은 것부터 오도록 끼운다(같으면 뒤에).
; 종류 값: 벽 4(0x5d31da), 정적 오브젝트 5·6·7(0x5d3a7d·0x5d3eab·0x5d4533), 이펙트 8(0x49e86f), 0·1 은 사람·물건 쪽.
; 칸끼리 순서(대각선 등)는 확인 못 함.

; 목록에 끼우기 (0x5cd960)
5cd9c7  lea eax, [esp + 8]
5cd9cb  push eax
5cd9cc  mov ecx, ebx
5cd9ce  call 0x5d2ca0
5cd9d3  mov edx, dword ptr [esp + 8]
5cd9d7  mov dword ptr [esp + 20], edx
5cd9db  mov ecx, dword ptr [esp + 12]
5cd9df  mov dword ptr [esp + 24], ecx
5cd9e3  lea eax, [esp + 16]
5cd9e7  push eax
5cd9e8  push edx
5cd9e9  push ecx
5cd9ea  mov ecx, ebp
5cd9ec  call 0x5cdad4
5cd9f1  movzx edx, al
5cd9f4  test edx, edx
5cd9f6  je 0x5cdac0
5cd9fc  mov ecx, dword ptr [esp + 16]
5cda00  mov eax, dword ptr [ebp + 52]
5cda03  mov dl, byte ptr [ebx + 428]
5cda09  mov dword ptr [esp + 32], esi
5cda0d  mov dword ptr [esp + 28], edi
5cda11  mov edi, eax
5cda13  lea esi, [ecx + ecx]
5cda16  add esi, esi
5cda18  lea eax, [esi + esi]
5cda1b  add eax, esi
5cda1d  mov esi, dword ptr [edi + eax + 8]
5cda21  cmp dl, byte ptr [esi + 428]
5cda27  jl 0x5cda54
5cda29  mov esi, ecx
5cda2b  test ebp, ebp
5cda2d  je 0x5cdab0
5cda33  mov eax, dword ptr [edi + eax + 4]
5cda37  cmp eax, -1
5cda3a  je 0x5cdaaa
5cda3c  mov ecx, eax
5cda3e  mov dword ptr [esp + 16], eax
5cda42  mov al, 1
5cda44  test al, al
5cda46  jne 0x5cda13
5cda48  mov eax, esi
5cda4a  mov esi, dword ptr [esp + 32]
5cda4e  mov edi, dword ptr [esp + 28]
5cda52  jmp 0x5cda63
5cda54  mov edx, eax
5cda56  mov eax, edi
5cda58  mov esi, dword ptr [esp + 32]
5cda5c  mov edi, dword ptr [esp + 28]
5cda60  mov eax, dword ptr [eax + edx]
5cda63  mov edx, dword ptr [ebp + 60]

; 종류 값 쓰는 곳
5d31da  mov byte ptr [ebx + 428], 4
5d3a7d  mov byte ptr [ebx + 428], 5
5d3eab  mov byte ptr [ebx + 428], 6
5d4533  mov byte ptr [ebx + 428], 7
49e86f  mov byte ptr [esi + 428], 8
