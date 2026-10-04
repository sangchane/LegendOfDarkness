; manual-client-engine-ui — 원작 2005(=5.99) Legend.exe, 채팅창·글꼴·체력바·NPC 대화창
; 채팅창(ChattingPane): 바탕 생성자에 432·96·408·96, 글 색 255·테두리 31, 줄 높이 12(+0x224), 한 줄 68바이트(+0x24C = 408px/6).
; 글꼴: eng%02d.fnt 1128바이트 = 94자(0x21~0x7E)×12바이트(8×12, 1비트), han%02d.fnt 통째(57624 = 2401자×24바이트, 16×12). 번호는 Legend.cfg EngFont/HanFont.
; 체력바(0x13): [id u32][? u8][퍼센트 u8][소리 u8]. 퍼센트/4 단계, 사람이면 2000ms 타이머로 띄움. 소리≠-1 이면 소리 재생.
; NPC: 0x2F 는 MerchantSession 이 종류 바이트(0~11, 표 0x87dcf0)로 창을 고르고, 0x30 은 0x5661f0(DialogPane).
; --- 채팅창 생성자
46742a  mov eax, 96
46742f  push 2
467431  push 1
467433  push 0
467435  push 31
467437  push 255
46743c  push eax
46743d  push 408
467442  push eax
467443  push 432
467448  call 0x6236b0 <.text+0x1fc6b0>
46744d  mov dword ptr [ebp - 4], 0
467454  mov ecx, dword ptr [ebp - 44]
467457  mov dword ptr [ecx], 8716996   ;; "`qF"
46745d  mov dword ptr [ecx + 308], 8717188   ;; "릗F"
467467  push 0
467469  call 0x623cb0 <.text+0x1fccb0>
46746e  mov ebx, dword ptr [ebp - 44]
467471  xor edx, edx
467473  mov byte ptr [ebx + 576], dl
467479  mov byte ptr [ebx + 577], dl
46747f  mov dword ptr [ebx + 580], edx
467485  mov dword ptr [ebx + 588], 68
46748f  mov al, 10
467491  mov byte ptr [ebp - 40], al
467494  mov byte ptr [ebp - 39], al
467497  mov byte ptr [ebp - 38], al
6239db  mov edx, 30000
6239e0  mov word ptr [esi + 532], dx
6239e7  mov word ptr [esi + 534], dx
6239ee  mov ecx, dword ptr [7841380]
6239f4  test ecx, ecx
623ae6  mov dword ptr [esi + 548], 12
623af0  mov dword ptr [ebp - 4], 4294967295
; --- 영문·한글 글꼴 읽기
4c48d8  mov dword ptr [esp + 4], 8796128   ;; "eng%02d.fnt"
4c48e0  mov edx, dword ptr [7556680]
4c48e6  mov ebp, dword ptr [edx + 1068]
4c48ec  mov dword ptr [esp + 8], ebp
4c48f0  call 0x66ee00 <.text+0x247e00>
4c48f5  call 0x4bddb0 <.text+0x96db0>
4c48fa  lea edx, [esp + 16]
4c48fe  push edx
4c48ff  mov ecx, eax
4c4901  call 0x4bd600 <.text+0x96600>
4c4906  mov ebp, eax
4c4908  test ebp, ebp
4c490a  je 0x4c4955 <.text+0x9d955>
4c490c  mov ecx, dword ptr [6847040]
4c4912  push 3072
4c4917  call 0x554690 <.text+0x12d690>
4c491c  mov dword ptr [esi + 24], eax
4c491f  call 0x4bddb0 <.text+0x96db0>
4c4924  mov esi, dword ptr [esi + 24]
4c4927  add esi, 396
4c492d  push 1128
4c4932  push esi
4c4933  push ebp
4c4934  mov ecx, eax
4c4936  call 0x4bd800 <.text+0x96800>
4c493b  call 0x4bddb0 <.text+0x96db0>
4c4940  push ebp
4c4941  mov ecx, eax
4c4943  call 0x4bd7d0 <.text+0x967d0>
4c4948  mov eax, 1
4c494d  add esp, 52
4c4989  mov dword ptr [esp + 4], 8796160   ;; "han%02d.fnt"
4c4991  mov edx, dword ptr [7556680]
4c4997  mov ebx, dword ptr [edx + 1064]
4c499d  mov dword ptr [esp + 8], ebx
4c49a1  call 0x66ee00 <.text+0x247e00>
4c49a6  call 0x4bddb0 <.text+0x96db0>
4c49ab  lea edx, [esp + 16]
4c49af  push edx
4c49b0  mov ecx, eax
4c49b2  call 0x4bd600 <.text+0x96600>
4c49b7  mov esi, eax
4c49b9  test esi, esi
4c49bb  je 0x4c4a08 <.text+0x9da08>
4c49bd  call 0x4bddb0 <.text+0x96db0>
4c49c2  push esi
4c49c3  mov ecx, eax
4c49c5  call 0x4bdb60 <.text+0x96b60>
4c49ca  mov ebx, eax
4c49cc  mov ecx, dword ptr [6847040]
4c49d2  push ebx
4c49d3  call 0x554690 <.text+0x12d690>
4c49d8  mov dword ptr [edi + 28], eax
4c49db  call 0x4bddb0 <.text+0x96db0>
4c49e0  mov edi, dword ptr [edi + 28]
4c49e3  push ebx
4c49e4  push edi
4c49e5  push esi
; --- 0x13 체력바
54c244  push edi
54c245  push esi
54c246  push ebx
54c247  mov ebx, ecx
54c249  mov eax, dword ptr [esp + 16]
54c24d  mov esi, dword ptr [eax + 16]
54c250  movsx edi, byte ptr [eax + 21]
54c254  movsx eax, byte ptr [eax + 22]
54c258  cmp eax, -1
54c25b  je 0x54c269 <.text+0x125269>
54c25d  mov ecx, dword ptr [6863616]
54c263  push eax
54c264  call 0x603330 <.text+0x1dc330>
54c269  test edi, edi
54c26b  jl 0x54c28a <.text+0x12528a>
54c26d  cmp edi, 100
54c270  jg 0x54c28a <.text+0x12528a>
54c272  mov eax, edi
54c274  sar eax
54c276  shr eax, 30
54c279  add eax, edi
51b64d  mov eax, dword ptr [esp + 20]
51b651  mov byte ptr [ebp + 566], 1
51b658  mov dword ptr [ebp + 568], eax
51b65e  call dword ptr [8666000]   ;; timeGetTime
51b664  add eax, 1900
51b669  mov dword ptr [ebp + 572], eax
51b66f  mov ecx, dword ptr [7556692]
51b675  xor edx, edx
51b677  push edx
51b678  push edx
51b679  push 2000
51b67e  push 33554433
51b683  push ebp
; --- 0x2F / 0x30 갈래
5572b0  push esi
5572b1  mov eax, dword ptr [esp + 8]
5572b5  mov edx, dword ptr [eax + 20]
5572b8  test edx, edx
5572ba  je 0x5572c6 <.text+0x1302c6>
5572bc  mov al, byte ptr [edx]
5572be  cmp al, 47
5572c0  je 0x5572db <.text+0x1302db>
5572c2  cmp al, 48
5572c4  je 0x5572ce <.text+0x1302ce>
5572c6  xor eax, eax
5572c8  add esp, 4
5572cb  ret 4
5572ce  call 0x5661f0 <.text+0x13f1f0>
5572d3  xor eax, eax
55731b  mov ebx, dword ptr [esp + 24]
55731f  lea eax, [ebx + 1]
557322  mov dword ptr [esp], eax
557325  call 0x5fe080 <.text+0x1d7080>
55732a  movzx eax, al
55732d  cmp eax, 11
557330  ja 0x5573bf <.text+0x1303bf>
557336  mov eax, dword ptr [4*eax + 8904176]
55733d  jmp eax
55733f  lea eax, [ebx + 2]
557342  push eax
