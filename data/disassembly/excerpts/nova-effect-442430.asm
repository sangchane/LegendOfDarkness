; 노바 서버 Legend.exe (md5 b909a155253918fde0ce7cd5e28bbb3a) — effect 명령
; 명령표 0x47c73c: {핸들러, 이름, 인자} 12바이트씩. effect = {0x442430, "effect"(0x47d8fc), "iiii"(0x47d904)}
; 스크립트 꼴: effect @myid, <번호>, 0, <속도>

; --- 0x442430 effect 핸들러 — 인자 넷을 손대지 않고 0x45cd18 로 넘긴다 ---
  442430:	push   ebp
  442431:	mov    ebp,esp
  442433:	sub    esp,0x8
  442436:	mov    DWORD PTR [ebp-0x8],0xcccccccc
  44243d:	mov    DWORD PTR [ebp-0x4],0xcccccccc
  442444:	mov    eax,DWORD PTR [ebp+0x8]
  442447:	mov    ecx,DWORD PTR [eax+0x14]
  44244a:	cmp    DWORD PTR [ecx*4+0x49c560],0x0
  442452:	je     0x442469
  442454:	mov    edx,DWORD PTR [ebp+0x8]
  442457:	mov    eax,DWORD PTR [edx+0x14]
  44245a:	mov    ecx,DWORD PTR [eax*4+0x49c560]
  442461:	mov    edx,DWORD PTR [ecx+0x20]
  442464:	mov    DWORD PTR [ebp-0x8],edx
  442467:	jmp    0x442470
  442469:	mov    DWORD PTR [ebp-0x8],0x0
  442470:	mov    eax,DWORD PTR [ebp-0x8]
  442473:	mov    DWORD PTR [ebp-0x4],eax
  442476:	mov    ecx,DWORD PTR [ebp+0x8]
  442479:	mov    edx,DWORD PTR [ecx]
  44247b:	mov    eax,DWORD PTR [ebp+0x8]
  44247e:	mov    ecx,DWORD PTR [eax+0x4]
  442481:	add    ecx,0x5
  442484:	imul   ecx,ecx,0xc
  442487:	mov    edx,DWORD PTR [edx+0xc]
  44248a:	add    edx,ecx
  44248c:	push   edx
  44248d:	mov    eax,DWORD PTR [ebp+0x8]
  442490:	push   eax
  442491:	call   0x4390c4
  442496:	add    esp,0x8
  442499:	push   eax
  44249a:	mov    ecx,DWORD PTR [ebp+0x8]
  44249d:	mov    edx,DWORD PTR [ecx]
  44249f:	mov    eax,DWORD PTR [ebp+0x8]
  4424a2:	mov    ecx,DWORD PTR [eax+0x4]
  4424a5:	add    ecx,0x4
  4424a8:	imul   ecx,ecx,0xc
  4424ab:	mov    edx,DWORD PTR [edx+0xc]
  4424ae:	add    edx,ecx
  4424b0:	push   edx
  4424b1:	mov    eax,DWORD PTR [ebp+0x8]
  4424b4:	push   eax
  4424b5:	call   0x4390c4
  4424ba:	add    esp,0x8
  4424bd:	push   eax
  4424be:	mov    ecx,DWORD PTR [ebp+0x8]
  4424c1:	mov    edx,DWORD PTR [ecx]
  4424c3:	mov    eax,DWORD PTR [ebp+0x8]
  4424c6:	mov    ecx,DWORD PTR [eax+0x4]
  4424c9:	add    ecx,0x3
  4424cc:	imul   ecx,ecx,0xc
  4424cf:	mov    edx,DWORD PTR [edx+0xc]
  4424d2:	add    edx,ecx
  4424d4:	push   edx
  4424d5:	mov    eax,DWORD PTR [ebp+0x8]
  4424d8:	push   eax
  4424d9:	call   0x4390c4
  4424de:	add    esp,0x8
  4424e1:	push   eax
  4424e2:	mov    ecx,DWORD PTR [ebp+0x8]
  4424e5:	mov    edx,DWORD PTR [ecx]
  4424e7:	mov    eax,DWORD PTR [ebp+0x8]
  4424ea:	mov    ecx,DWORD PTR [eax+0x4]
  4424ed:	add    ecx,0x2
  4424f0:	imul   ecx,ecx,0xc
  4424f3:	mov    edx,DWORD PTR [edx+0xc]
  4424f6:	add    edx,ecx
  4424f8:	push   edx
  4424f9:	mov    eax,DWORD PTR [ebp+0x8]
  4424fc:	push   eax
  4424fd:	call   0x4390c4
  442502:	add    esp,0x8
  442505:	push   eax
  442506:	mov    ecx,DWORD PTR [ebp-0x4]
  442509:	mov    edx,DWORD PTR [ecx+0x10]
  44250c:	add    edx,0x44
  44250f:	push   edx
  442510:	mov    eax,DWORD PTR [ebp-0x4]
  442513:	mov    ecx,DWORD PTR [eax+0x10]
  442516:	mov    edx,DWORD PTR [ecx+0x4]
  442519:	push   edx
  44251a:	call   0x45cd18
  44251f:	add    esp,0x18
  442522:	xor    eax,eax
  442524:	add    esp,0x8
  442527:	cmp    ebp,esp
  442529:	call   0x467374
  44252e:	mov    esp,ebp
  442530:	pop    ebp
  442531:	ret

; --- 0x45cd18 0x29 패킷 만들기 — 번호는 1바이트로 그대로 들어간다 ---
  45cd18:	push   ebp
  45cd19:	mov    ebp,esp
  45cd1b:	mov    BYTE PTR ds:0x48c3e0,0xaa
  45cd22:	mov    BYTE PTR ds:0x48c3e1,0x0
  45cd29:	mov    BYTE PTR ds:0x48c3e2,0xe
  45cd30:	mov    BYTE PTR ds:0x48c3e3,0x29
  45cd37:	mov    BYTE PTR ds:0x48c3e4,0x0
  45cd3e:	mov    BYTE PTR ds:0x48c3e5,0x0
  45cd45:	mov    BYTE PTR ds:0x48c3e6,0x0
  45cd4c:	mov    eax,DWORD PTR [ebp+0x8]
  45cd4f:	shr    eax,0x8
  45cd52:	mov    ds:0x48c3e7,al
  45cd57:	mov    ecx,DWORD PTR [ebp+0x8]
  45cd5a:	and    ecx,0xff
  45cd60:	mov    BYTE PTR ds:0x48c3e8,cl
  45cd66:	mov    eax,DWORD PTR [ebp+0x10]
  45cd69:	shr    eax,0x18
  45cd6c:	xor    edx,edx
  45cd6e:	mov    ecx,0x100
  45cd73:	div    ecx
  45cd75:	mov    BYTE PTR ds:0x48c3e9,dl
  45cd7b:	mov    eax,DWORD PTR [ebp+0x10]
  45cd7e:	shr    eax,0x10
  45cd81:	xor    edx,edx
  45cd83:	mov    ecx,0x100
  45cd88:	div    ecx
  45cd8a:	mov    BYTE PTR ds:0x48c3ea,dl
  45cd90:	mov    eax,DWORD PTR [ebp+0x10]
  45cd93:	xor    edx,edx
  45cd95:	mov    ecx,0x10000
  45cd9a:	div    ecx
  45cd9c:	mov    eax,edx
  45cd9e:	shr    eax,0x8
  45cda1:	xor    edx,edx
  45cda3:	mov    ecx,0x100
  45cda8:	div    ecx
  45cdaa:	mov    BYTE PTR ds:0x48c3eb,dl
  45cdb0:	mov    eax,DWORD PTR [ebp+0x10]
  45cdb3:	xor    edx,edx
  45cdb5:	mov    ecx,0x100
  45cdba:	div    ecx
  45cdbc:	mov    BYTE PTR ds:0x48c3ec,dl
  45cdc2:	mov    dl,BYTE PTR [ebp+0x14]
  45cdc5:	mov    BYTE PTR ds:0x48c3ed,dl
  45cdcb:	mov    al,BYTE PTR [ebp+0x18]
  45cdce:	mov    ds:0x48c3ee,al
  45cdd3:	mov    ecx,DWORD PTR [ebp+0x1c]
  45cdd6:	sar    ecx,0x8
  45cdd9:	mov    BYTE PTR ds:0x48c3ef,cl
  45cddf:	mov    edx,DWORD PTR [ebp+0x1c]
  45cde2:	and    edx,0xff
  45cde8:	mov    BYTE PTR ds:0x48c3f0,dl
  45cdee:	mov    eax,DWORD PTR [ebp+0xc]
  45cdf1:	push   eax
  45cdf2:	call   0x416ba7
  45cdf7:	add    esp,0x4
  45cdfa:	cmp    ebp,esp
  45cdfc:	call   0x467374
  45ce01:	pop    ebp
  45ce02:	ret
  45ce03:	push   ebp
