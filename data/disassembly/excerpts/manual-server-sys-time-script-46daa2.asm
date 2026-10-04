; 1초 타이머 0x46daa2 — __TIME_SCRIPT__ 를 1초마다 한 번(전역 객체 [5173612]) 돌리고, 접속자마다 맵 칸 +32 가 있으면 +0x83(클리어 시간)을 1 올리며 Dungeon__Script 를 돌린다. localtime 명령 0x440060: 1 년(+1900) 2 월(+1) 3 일 4 시 5 분 6 초 7 요일
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x46daa2-0x46dd05 ----
46daa2  push	ebp
46daa3  mov	ebp, esp
46daa5  sub	esp, 308
46daab  push	esi
46daac  push	edi
46daad  lea	edi, [ebp - 308]
46dab3  mov	ecx, 77
46dab8  mov	eax, 3435973836
46dabd  rep		stosd	dword ptr es:[edi], eax
46dabf  push	4789988   ;; "콜라마"
46dac4  call	0x40ef76 <.text+0xdf76>
46dac9  add	esp, 4
46dacc  mov	dword ptr [ebp - 8], eax
46dacf  push	4789996   ;; "가호타임"
46dad4  call	0x40ef76 <.text+0xdf76>
46dad9  add	esp, 4
46dadc  mov	dword ptr [ebp - 300], eax
46dae2  push	4790008   ;; "Dungeon__Script"
46dae7  call	0x40ef76 <.text+0xdf76>
46daec  add	esp, 4
46daef  mov	dword ptr [ebp - 20], eax
46daf2  push	4790024   ;; "__TIME_SCRIPT__"
46daf7  call	0x40ef76 <.text+0xdf76>
46dafc  add	esp, 4
46daff  mov	dword ptr [ebp - 12], eax
46db02  mov	byte ptr [ebp - 280], 0
46db09  mov	ecx, 63
46db0e  xor	eax, eax
46db10  lea	edi, [ebp - 279]
46db16  rep		stosd	dword ptr es:[edi], eax
46db18  stosw	word ptr es:[edi], ax
46db1a  stosb	byte ptr es:[edi], al
46db1b  lea	eax, [ebp - 296]
46db21  push	eax
46db22  call	0x477bae <.text+0x76bae>
46db27  add	esp, 4
46db2a  lea	ecx, [ebp - 296]
46db30  push	ecx
46db31  call	0x477a4e <.text+0x76a4e>
46db36  add	esp, 4
46db39  mov	dword ptr [ebp - 292], eax
46db3f  mov	edx, dword ptr [5173612]
46db45  mov	eax, dword ptr [4*edx + 4903424]
46db4c  mov	dword ptr [ebp - 24], eax
46db4f  cmp	dword ptr [ebp - 24], 0
46db53  je	0x46db6c <.text+0x6cb6c>
46db55  push	0
46db57  mov	ecx, dword ptr [ebp - 24]
46db5a  push	ecx
46db5b  push	0
46db5d  mov	edx, dword ptr [ebp - 12]
46db60  mov	eax, dword ptr [edx + 20]
46db63  push	eax
46db64  call	0x43eb01 <.text+0x3db01>
46db69  add	esp, 16
46db6c  00	mov	dword ptr [ebp - 288], 0
46db76  jmp	0x46db87 <.text+0x6cb87>
46db78  mov	ecx, dword ptr [ebp - 288]
46db7e  add	ecx, 1
46db81  mov	dword ptr [ebp - 288], ecx
46db87  mov	edx, dword ptr [ebp - 288]
46db8d  cmp	edx, dword ptr [4903136]
46db93  jae	0x46efae <.text+0x6dfae>
46db99  mov	eax, dword ptr [ebp - 288]
46db9f  mov	ecx, dword ptr [4*eax + 4903140]
46dba6  mov	edx, dword ptr [4*ecx + 4903424]
46dbad  mov	dword ptr [ebp - 24], edx
46dbb0  cmp	dword ptr [ebp - 24], 0
46dbb4  je	0x46efa9 <.text+0x6dfa9>
46dbba  mov	eax, dword ptr [ebp - 24]
46dbbd  xor	ecx, ecx
46dbbf  mov	cl, byte ptr [eax + 61]
46dbc2  test	ecx, ecx
46dbc4  je	0x46efa9 <.text+0x6dfa9>
46dbca  mov	edx, dword ptr [ebp - 24]
46dbcd  xor	eax, eax
46dbcf  mov	al, byte ptr [edx + 60]
46dbd2  test	eax, eax
46dbd4  je	0x46efa9 <.text+0x6dfa9>
46dbda  mov	ecx, dword ptr [ebp - 24]
46dbdd  mov	edx, dword ptr [ecx + 32]
46dbe0  mov	dword ptr [ebp - 284], edx
46dbe6  cmp	dword ptr [ebp - 284], 0
46dbed  je	0x46efa9 <.text+0x6dfa9>
46dbf3  mov	eax, dword ptr [ebp - 284]
46dbf9  mov	ecx, dword ptr [eax + 16]
46dbfc  cmp	dword ptr [ecx + 388], 0
46dc03  je	0x46dca1 <.text+0x6cca1>
46dc09  mov	edx, dword ptr [ebp - 284]
46dc0f  mov	eax, dword ptr [edx + 16]
46dc12  mov	ecx, dword ptr [eax + 388]
46dc18  mov	edx, dword ptr [ecx + 12]
46dc1b  xor	eax, eax
46dc1d  mov	ax, word ptr [edx + 52]
46dc21  test	eax, eax
46dc23  je	0x46dca1 <.text+0x6cca1>
46dc25  mov	ecx, dword ptr [ebp - 284]
46dc2b  mov	edx, dword ptr [ecx + 16]
46dc2e  xor	eax, eax
46dc30  mov	al, byte ptr [edx + 96]
46dc33  cmp	eax, 1
46dc36  jne	0x46dc63 <.text+0x6cc63>
46dc38  push	100
46dc3a  push	0
46dc3c  push	125
46dc3e  push	0
46dc40  mov	ecx, dword ptr [ebp - 284]
46dc46  mov	edx, dword ptr [ecx + 16]
46dc49  add	edx, 88
46dc4c  push	edx
46dc4d  mov	eax, dword ptr [ebp - 288]
46dc53  mov	ecx, dword ptr [4*eax + 4903140]
46dc5a  push	ecx
46dc5b  call	0x46b66a <.text+0x6a66a>
46dc60  add	esp, 24
46dc63  mov	edx, dword ptr [ebp - 284]
46dc69  mov	eax, dword ptr [edx + 16]
46dc6c  xor	ecx, ecx
46dc6e  mov	cl, byte ptr [eax + 96]
46dc71  cmp	ecx, 1
46dc74  je	0x46dca1 <.text+0x6cca1>
46dc76  push	100
46dc78  push	0
46dc7a  push	126
46dc7c  push	0
46dc7e  mov	edx, dword ptr [ebp - 284]
46dc84  mov	eax, dword ptr [edx + 16]
46dc87  add	eax, 88
46dc8a  push	eax
46dc8b  mov	ecx, dword ptr [ebp - 288]
46dc91  mov	edx, dword ptr [4*ecx + 4903140]
46dc98  push	edx
46dc99  call	0x46b66a <.text+0x6a66a>
46dc9e  add	esp, 24
46dca1  mov	eax, dword ptr [ebp - 284]
46dca7  mov	ecx, dword ptr [eax + 16]
46dcaa  mov	edx, dword ptr [ecx + 88]
46dcad  xor	eax, eax
46dcaf  mov	ax, word ptr [edx + 32]
46dcb3  test	eax, eax
46dcb5  jle	0x46dd05 <.text+0x6cd05>
46dcb7  mov	ecx, dword ptr [ebp - 284]
46dcbd  mov	edx, dword ptr [ecx + 16]
46dcc0  mov	al, byte ptr [edx + 131]
46dcc6  add	al, 1
46dcc8  mov	ecx, dword ptr [ebp - 284]
46dcce  mov	edx, dword ptr [ecx + 16]
46dcd1  mov	byte ptr [edx + 131], al
46dcd7  mov	eax, dword ptr [ebp - 284]
46dcdd  cmp	dword ptr [eax + 632], 0
46dce4  jne	0x46dd03 <.text+0x6cd03>
46dce6  push	0
46dce8  mov	ecx, dword ptr [ebp - 284]
46dcee  mov	edx, dword ptr [ecx + 4]
46dcf1  push	edx
46dcf2  push	0
46dcf4  mov	eax, dword ptr [ebp - 20]
46dcf7  mov	ecx, dword ptr [eax + 20]
46dcfa  push	ecx
46dcfb  call	0x43e65f <.text+0x3d65f>
46dd00  add	esp, 16
46dd03  jmp	0x46dd4f <.text+0x6cd4f>
46dd05  mov	edx, dword ptr [ebp - 284]

; ---- 0x440060-0x4401a3 ----
440060  push	ebp
440061  mov	ebp, esp
440063  sub	esp, 12
440066  mov	dword ptr [ebp - 12], 3435973836
44006d  mov	dword ptr [ebp - 8], 3435973836
440074  mov	dword ptr [ebp - 4], 3435973836
44007b  mov	eax, dword ptr [ebp + 8]
44007e  mov	ecx, dword ptr [eax]
440080  mov	edx, dword ptr [ebp + 8]
440083  mov	eax, dword ptr [edx + 4]
440086  add	eax, 2
440089  imul	eax, eax, 12
44008c  mov	ecx, dword ptr [ecx + 12]
44008f  add	ecx, eax
440091  push	ecx
440092  mov	edx, dword ptr [ebp + 8]
440095  push	edx
440096  call	0x43d110 <.text+0x3c110>
44009b  add	esp, 8
44009e  mov	dword ptr [ebp - 4], eax
4400a1  lea	eax, [ebp - 12]
4400a4  push	eax
4400a5  call	0x477bae <.text+0x76bae>
4400aa  add	esp, 4
4400ad  lea	ecx, [ebp - 12]
4400b0  push	ecx
4400b1  call	0x477a4e <.text+0x76a4e>
4400b6  add	esp, 4
4400b9  mov	dword ptr [ebp - 8], eax
4400bc  cmp	dword ptr [ebp - 4], 1
4400c0  jne	0x4400e3 <.text+0x3f0e3>
4400c2  mov	edx, dword ptr [ebp - 8]
4400c5  mov	eax, dword ptr [edx + 20]
4400c8  add	eax, 1900
4400cd  push	eax
4400ce  push	2
4400d0  mov	ecx, dword ptr [ebp + 8]
4400d3  mov	edx, dword ptr [ecx]
4400d5  push	edx
4400d6  call	0x43d187 <.text+0x3c187>
4400db  add	esp, 12
4400de  jmp	0x4401a0 <.text+0x3f1a0>
4400e3  cmp	dword ptr [ebp - 4], 2
4400e7  jne	0x440108 <.text+0x3f108>
4400e9  mov	eax, dword ptr [ebp - 8]
4400ec  mov	ecx, dword ptr [eax + 16]
4400ef  add	ecx, 1
4400f2  push	ecx
4400f3  push	2
4400f5  mov	edx, dword ptr [ebp + 8]
4400f8  mov	eax, dword ptr [edx]
4400fa  push	eax
4400fb  call	0x43d187 <.text+0x3c187>
440100  add	esp, 12
440103  jmp	0x4401a0 <.text+0x3f1a0>
440108  cmp	dword ptr [ebp - 4], 3
44010c  jne	0x440127 <.text+0x3f127>
44010e  mov	ecx, dword ptr [ebp - 8]
440111  mov	edx, dword ptr [ecx + 12]
440114  push	edx
440115  push	2
440117  mov	eax, dword ptr [ebp + 8]
44011a  mov	ecx, dword ptr [eax]
44011c  push	ecx
44011d  call	0x43d187 <.text+0x3c187>
440122  add	esp, 12
440125  jmp	0x4401a0 <.text+0x3f1a0>
440127  cmp	dword ptr [ebp - 4], 4
44012b  jne	0x440146 <.text+0x3f146>
44012d  mov	edx, dword ptr [ebp - 8]
440130  mov	eax, dword ptr [edx + 8]
440133  push	eax
440134  push	2
440136  mov	ecx, dword ptr [ebp + 8]
440139  mov	edx, dword ptr [ecx]
44013b  push	edx
44013c  call	0x43d187 <.text+0x3c187>
440141  add	esp, 12
440144  jmp	0x4401a0 <.text+0x3f1a0>
440146  cmp	dword ptr [ebp - 4], 5
44014a  jne	0x440165 <.text+0x3f165>
44014c  mov	eax, dword ptr [ebp - 8]
44014f  mov	ecx, dword ptr [eax + 4]
440152  push	ecx
440153  push	2
440155  mov	edx, dword ptr [ebp + 8]
440158  mov	eax, dword ptr [edx]
44015a  push	eax
44015b  call	0x43d187 <.text+0x3c187>
440160  add	esp, 12
440163  jmp	0x4401a0 <.text+0x3f1a0>
440165  cmp	dword ptr [ebp - 4], 6
440169  jne	0x440183 <.text+0x3f183>
44016b  mov	ecx, dword ptr [ebp - 8]
44016e  mov	edx, dword ptr [ecx]
440170  push	edx
440171  push	2
440173  mov	eax, dword ptr [ebp + 8]
440176  mov	ecx, dword ptr [eax]
440178  push	ecx
440179  call	0x43d187 <.text+0x3c187>
44017e  add	esp, 12
440181  jmp	0x4401a0 <.text+0x3f1a0>
440183  cmp	dword ptr [ebp - 4], 7
440187  jne	0x4401a0 <.text+0x3f1a0>
440189  mov	edx, dword ptr [ebp - 8]
44018c  mov	eax, dword ptr [edx + 24]
44018f  push	eax
440190  push	2
440192  mov	ecx, dword ptr [ebp + 8]
440195  mov	edx, dword ptr [ecx]
440197  push	edx
440198  call	0x43d187 <.text+0x3c187>
44019d  add	esp, 12
4401a0  add	esp, 12
4401a3  cmp	ebp, esp
