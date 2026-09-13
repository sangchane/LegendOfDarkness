---
파일: "crehlp.txt"
아카이브: "national.dat"
줄수: 16
바이트: 1496
인코딩: cp949
---

# crehlp.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 16줄)

```
Help: At any time, you can receive help by pressing the (?) button. In the game, this will be the lowest button on the right.
Name: The name must contain 3 to 12 letters and may have no spaces or special characters. Name yourself appropriately to the theme of Dark Ages.  For example: Erin, McCloud, Argus, and Meaveen.  
Password: Create a password that no one else will guess.  Make it 7 to 8 characters long, with numbers and letters in it.  Never give anyone your password.  Anyone who asks for your password is trying to steal your account. Nexon never asks for your password.
Confirm: Retype your password exactly. Remember to keep your password where no one else will find it. 
Within the game press <Alt> - 1 to speak Phrase (1)
Man / Woman: The icons above the walking figure allow you to choose your gender. Men and women have the same physical and mental characteristics in Dark Ages.  They wear different clothing, have different hairstyles, and generally role-play differently.
Hair: Choose your hairstyle.  It will be a while before it may be changed.
Color: Choose your hair color.  It will be a while before it may be changed.
OK / Cancel: When you are satisfied, click [OK]. You will have a 5-day FREE trial beginning now.  Then register: www.darkages.com -> Click 'Register'
Your Legend Begins Here
Aisling library: 
www.darkages.com/library/temuair
Roleplaying 
www.darkages.com/library/rp
Community
www.darkages.com/community
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> crehlp
iconv -f CP949 -t UTF-8 <폴더>/…/crehlp.txt
```
