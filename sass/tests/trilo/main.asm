	;.verbose

; -----------------------------
; TT-replayer example
; 
; This example plays compiled PSG+SCC songs
; ------------------------------
/*
	;.rom 
	;.page 1
	;db "MegaROM",1ah
    .org 04000h
    .db "AB"             ; ID bytes
    .dw initmain       	 ; cartridge initialization
    .dw 0                ; statement handler (not used)
    .dw 0                ; device handler (not used)
    .dw 0                ; BASIC program in ROM (not used, especially not in page 1)
    .dw 0,0,0            ; reserved
 	.dw	0,0,0,0,0,0	
*/

;	.bios
	.page 1
	.rom 
;  	.MEGAROM	KonamiSCC
	.start	initmain
	;.dw	0,0,0,0,0,0	
	;.ds	12

initmain:

	ei
	halt
	di
		
;; set pages and subslot
;;
	call    0x138
	rrca
	rrca
	and     0x03
	ld      c,a
	ld      b,0
	ld      hl,0xfcc1
	add     hl,bc
	or      [hl]
	ld      b,a
	inc     hl
	inc     hl
	inc     hl
	inc     hl
	ld      a,[hl]
	and     0x0c
	or      b
    
	ld      h,0x80
	call    0x24        

/*
	SEARCH_SLOTSET:
	call	SEARCH_SLOT
	;;ld		[SLOTVAR], a

;
; SEARCH_SLOT
; Busca slot de nuestro rom siempre que se ejecute en 04000h-07FFFh (pagina1)
;----------------------------------------------------------
SEARCH_SLOT:
	call	0x138	;RSLREG			; =$0138
	rrca
	rrca
	and		3				; a = page1-slot-config (=xxxxxxPP)
	ld		c, a			; c = page1-slot-config (=xxxxxxPP)
	ld		b, 0			; bc = a
	ld		hl, 0xfcc1; EXPTBL		; =$FCC1
	add		hl, bc			; hl = EXPTBL + bc
	ld		a, [hl]			; a = primary slot selection register value
	and		$80				; a =$80 -> expanded slot 
	jr		z, SEARCH_SLOT0	; go to not expanded slot 
	; expanded slot
	or		c				; a = $80 or page1-slot-config (=ExxxxxPP)
	ld		c, a			; c = (=ExxxxxPP)
	inc		hl
	inc		hl
	inc		hl
	inc		hl
	ld		a, [hl]			; get secondary slot selection register value
	and		$0C				; a = secondary slot selection register value xxxxSSxx
SEARCH_SLOT0:
	or		c				; a = a or c (=ExxxSSPP) // SS=00 if not expanded slot
	ld		h, $80
	call	0x24        ;	ENASLT
*/		



	;clear RAM [first kb only]
	ld	bc,1024
	ld	hl,0xc000
	ld	de,0xc001

	ld	[hl],0
	ldir	

megarom_bank0	.equ		05000h
megarom_bank1	.equ		07000h
megarom_bank2	.equ		09000h
megarom_bank3	.equ		0B000h

	ld	a,0
	ld [megarom_bank0],a
	inc a
	ld [megarom_bank1],a
	ld	a,0x3F
	ld [megarom_bank2],a
	inc a
	ld [megarom_bank3],a

	;--- place replayer on hook
	ld	a,0xc3
	ld	hl,isr
	ld	[0xFD9A],a
	ld	[0xFD9B],hl	
	
	;--- initialise replayer
	call	replay_init
	
	;--- initialise demo song
	ld	hl,demo_song
	call	replay_loadsong
	
	ei
	
	xor	a
	ld	[pattern],a

	
infinite:
	halt
	;---- Test for space
	xor	a
	call	$00D8
	and	a
	jp	z,infinite

;	ld	a,0
;	call	replay_set_SCC_balance
;	ld	de,-2
;	call	replay_transpose	
	ld	a,32
	call	replay_fade_out
;	call	replay_pause
	; wait_key_release
@@loop:	
	xor	a
	call	$00D8
	and	a
	jp	nz,@@loop
	
	jp	infinite

	
	
isr:
	in	a,[0x99]
	call	replay_route		; first outout data
	call	replay_play			; calculate next output
	ret
	
	.INCLUDE	".\tests\trilo\ttreplay.asm"
	.INCLUDE	".\tests\trilo\ttreplayDAT.asm"

; Cesc test per fer que generi un rom de 32KB	
;	.PAGE 2
	.org 0x6000
	.subpage 1 at $6000	
demo_song:
	.INCLUDE	".\tests\trilo\andorogy.asm"

	.subpage 15 at $8000	

;;	map	0xc000
;	.org 0xc000 ; Cesc test
	.PAGE 3

	.INCLUDE	".\tests\trilo\ttreplayRAM.asm"
pattern:	.ds 1

