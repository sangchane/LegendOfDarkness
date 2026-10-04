; 서버 시작 순서 — 0x40ead1 이 db 파일을 차례로 읽고(0x4022e0 = 매니페스트/설정 읽기, 앞 push 가 갈래 함수), main 0x4764fa 가 타이머를 건다(0x412980(함수, 0, 주기ms, 0))
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x40ead1-0x40ecf0 ----
40ead1  push	ebp
40ead2  mov	ebp, esp
40ead4  push	esi
40ead5  call	0x402ef6 <.text+0x1ef6>
40eada  push	4748992   ;; "db/lod_map_block.dat"
40eadf  call	0x403d15 <.text+0x2d15>
40eae4  add	esp, 4
40eae7  push	1
40eae9  push	4749016
40eaee  push	4749020   ;; "db/server/server.txt"
40eaf3  push	4204050
40eaf8  call	0x4022e0 <.text+0x12e0>
40eafd  add	esp, 16
40eb00  push	1
40eb02  push	4749044
40eb07  push	4749048   ;; "db/server/experience.txt"
40eb0c  push	4204809
40eb11  call	0x4022e0 <.text+0x12e0>
40eb16  add	esp, 16
40eb19  push	1
40eb1b  push	4749076
40eb20  push	4749080   ;; "db/server/server_list.txt"
40eb25  push	4205469
40eb2a  call	0x4022e0 <.text+0x12e0>
40eb2f  add	esp, 16
40eb32  call	0x40e61f <.text+0xd61f>
40eb37  push	1
40eb39  push	4749108
40eb3e  push	4749112   ;; "db/maps/map_db.txt"
40eb43  push	4249123
40eb48  call	0x4022e0 <.text+0x12e0>
40eb4d  add	esp, 16
40eb50  push	1
40eb52  push	4749132
40eb57  push	4749136   ;; "db/door/door_db.txt"
40eb5c  push	4249251
40eb61  call	0x4022e0 <.text+0x12e0>
40eb66  add	esp, 16
40eb69  push	1
40eb6b  push	4749156
40eb70  push	4749160   ;; "db/warp/warp_db.txt"
40eb75  push	4249327
40eb7a  call	0x4022e0 <.text+0x12e0>
40eb7f  add	esp, 16
40eb82  push	1
40eb84  push	4749180
40eb89  push	4749184   ;; "db/script/script_db.txt"
40eb8e  push	4249790
40eb93  call	0x4022e0 <.text+0x12e0>
40eb98  add	esp, 16
40eb9b  push	1
40eb9d  push	4749208
40eba2  push	4749212   ;; "db/item/item_db.txt"
40eba7  push	4249403
40ebac  call	0x4022e0 <.text+0x12e0>
40ebb1  add	esp, 16
40ebb4  push	1
40ebb6  push	4749232
40ebbb  push	4749236   ;; "db/mob/mob_db.txt"
40ebc0  push	4249479
40ebc5  call	0x4022e0 <.text+0x12e0>
40ebca  add	esp, 16
40ebcd  push	1
40ebcf  push	4749256
40ebd4  push	4749260   ;; "db/npc/npc_db.txt"
40ebd9  push	4249607
40ebde  call	0x4022e0 <.text+0x12e0>
40ebe3  add	esp, 16
40ebe6  push	1
40ebe8  push	4749280
40ebed  push	4749284   ;; "db/skill/skill_db.txt"
40ebf2  push	4249854
40ebf7  call	0x4022e0 <.text+0x12e0>
40ebfc  add	esp, 16
40ebff  push	1
40ec01  push	4749308
40ec06  push	4749312   ;; "db/spell/spell_db.txt"
40ec0b  push	4249930
40ec10  call	0x4022e0 <.text+0x12e0>
40ec15  add	esp, 16
40ec18  push	2536
40ec1d  push	0
40ec1f  push	5165668
40ec24  call	0x4770f0 <.text+0x760f0>
40ec29  add	esp, 12
40ec2c  push	1
40ec2e  push	4749336
40ec33  push	4749340   ;; "db/server/server_char.txt"
40ec38  push	4250288
40ec3d  call	0x4022e0 <.text+0x12e0>
40ec42  add	esp, 16
40ec45  cmp	dword ptr [5165756], 0
40ec4c  jne	0x40ec76 <.text+0xdc76>
40ec4e  push	4749368   ;; "db/server/server_char.txt: 시작맵이 입력되지 않았습니다."
40ec53  call	0x477504 <.text+0x76504>
40ec58  add	esp, 4
40ec5b  mov	esi, esp
40ec5d  push	3000
40ec62  call	dword ptr [4722744]   ;; Sleep
40ec68  cmp	esi, esp
40ec6f  push	1
40ec71  call	0x476f76 <.text+0x75f76>
40ec76  cmp	dword ptr [5165844], 0
40ec7d  jne	0x40ec89 <.text+0xdc89>
40ec7f  00	mov	dword ptr [5165844], 1
40ec89  cmp	dword ptr [5165848], 0
40ec90  jne	0x40ec9c <.text+0xdc9c>
40ec92  00	mov	dword ptr [5165848], 1
40ec9c  push	0
40ec9e  push	4749428   ;; "Save/User/*.*"
40eca3  push	4220624
40eca8  call	0x40255e <.text+0x155e>
40ecad  add	esp, 12
40ecb0  push	0
40ecb2  push	4749444
40ecb7  push	4749448   ;; "db/server/server_broadcast.txt"
40ecbc  push	4205235
40ecc1  call	0x4022e0 <.text+0x12e0>
40ecc6  add	esp, 16
40ecc9  push	0
40eccb  push	4749480
40ecd0  push	4749484   ;; "db/server/server_filter.txt"
40ecd5  push	4205001
40ecda  call	0x4022e0 <.text+0x12e0>
40ecdf  add	esp, 16
40ece2  call	0x40d996 <.text+0xc996>
40ece7  pop	esi
40ece8  cmp	ebp, esp
40ecef  pop	ebp
40ecf0  ret

; ---- 0x476647-0x476840 ----
476647  call	0x43ef1c <.text+0x3df1c>
47664c  call	0x40fa40 <.text+0xea40>
476651  call	0x40ead1 <.text+0xdad1>
476656  push	0
476658  push	60
47665a  push	0
47665c  push	4677841
476661  call	0x412980 <.text+0x11980>
476666  add	esp, 16
476669  push	0
47666b  push	250
476670  push	0
476672  push	4352018
476677  call	0x412980 <.text+0x11980>
47667c  add	esp, 16
47667f  push	0
476681  push	1000
476686  push	0
476688  push	4352534
47668d  call	0x412980 <.text+0x11980>
476692  add	esp, 16
476695  push	0
476697  push	10
476699  push	0
47669b  push	4642885
4766a0  call	0x412980 <.text+0x11980>
4766a5  add	esp, 16
4766a8  push	0
4766aa  push	21000
4766af  push	0
4766b1  push	4641189
4766b6  call	0x412980 <.text+0x11980>
4766bb  add	esp, 16
4766be  push	0
4766c0  push	4500
4766c5  push	0
4766c7  push	4651205
4766cc  call	0x412980 <.text+0x11980>
4766d1  add	esp, 16
4766d4  push	0
4766d6  push	3000
4766db  push	0
4766dd  push	4640426
4766e2  call	0x412980 <.text+0x11980>
4766e7  add	esp, 16
4766ea  push	0
4766ec  push	500000
4766f1  push	0
4766f3  push	4639847
4766f8  call	0x412980 <.text+0x11980>
4766fd  add	esp, 16
476700  push	0
476702  push	1000
476707  push	0
476709  push	4551151
47670e  call	0x412980 <.text+0x11980>
476713  add	esp, 16
476716  push	0
476718  push	1000
47671d  push	0
47671f  push	4560647
476724  call	0x412980 <.text+0x11980>
476729  add	esp, 16
47672c  push	0
47672e  push	1000
476733  push	0
476735  push	4650451
47673a  call	0x412980 <.text+0x11980>
47673f  add	esp, 16
476742  push	0
476744  push	15000
476749  push	0
47674b  push	4677018
476750  call	0x412980 <.text+0x11980>
476755  add	esp, 16
476758  push	0
47675a  push	1000
47675f  push	0
476761  push	4643490
476766  call	0x412980 <.text+0x11980>
47676b  add	esp, 16
47676e  push	0
476770  push	1000
476775  push	0
476777  push	4642140
47677c  call	0x412980 <.text+0x11980>
476781  add	esp, 16
476784  push	0
476786  push	1000
47678b  push	0
47678d  push	4648897
476792  call	0x412980 <.text+0x11980>
476797  add	esp, 16
47679a  push	0
47679c  push	10
47679e  push	0
4767a0  push	4650848
4767a5  call	0x412980 <.text+0x11980>
4767aa  add	esp, 16
4767ad  push	0
4767af  push	120000
4767b4  push	0
4767b6  push	4643041
4767bb  call	0x412980 <.text+0x11980>
4767c0  add	esp, 16
4767c3  push	0
4767c5  push	500
4767ca  push	0
4767cc  push	4651046
4767d1  call	0x412980 <.text+0x11980>
4767d6  add	esp, 16
4767d9  push	0
4767db  push	1800000
4767e0  push	0
4767e2  push	4676917
4767e7  call	0x412980 <.text+0x11980>
4767ec  add	esp, 16
4767ef  00	mov	dword ptr [4903076], 38
4767f9  00	mov	dword ptr [4903080], 1
476803  00	mov	dword ptr [4903084], 4
47680d  00	mov	dword ptr [4903088], 8
476817  mov	cx, word ptr [4903072]
47681e  push	ecx
47681f  call	0x410254 <.text+0xf254>
476824  add	esp, 4
476827  mov	dword ptr [4903064], eax
47682c  mov	edx, dword ptr [4903064]
476832  mov	eax, dword ptr [4*edx + 4903424]
476839  mov	dword ptr [ebp - 8], eax
47683c  mov	ecx, dword ptr [ebp - 8]
47683f  mov	dword ptr [ecx + 40], 4271088
