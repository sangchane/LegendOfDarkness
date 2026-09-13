---
파일: "msg.tbl"
아카이브: "national.dat"
줄수: 130
바이트: 2942
인코딩: cp949
---

# msg.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 130줄)

```
Notification.
There is no more writings.
There is no more letters.
Error has occured while toggling.
User List
How much money do you wish to drop?
How much money will you give?
You cannot log on with the current client version. You need a new version of the client. Your connection with the server has been dropped. Exit the game, and then try the new version of the client.
Greetings, Aisling
You have my gratitude
Please teach me things
Adventure with me?
Group me, please
I'll group you
Help!
Be careful
Fair day
Farewell, Aisling
Character has been created. Choose "CONTINUE".
You have entered a wrong password. Please enter again
Your password cannot be confirmed. Check if your password is the same as the comfirmation number
Your password has been changed successfully.
Enter:
Conversation Mode
Animation Effect
You can exit the game now. Thank you.
NEXON Asia
--- Set your modem by clicking the SETUP button, and then use the CONNECT button to log on ---
a: add, d: delete, ?: see list >
ID of people you wish to reject whisper >
ID of people you wish to cancel rejection of whisper >
Waiting for the ending signal from the server. If you do not wait for the ending signal from the server, you character might be in danger
The connection has been dropped. Do you wish to log on again?
Would you like to delete?
Number of items to drop
No maps available
Awake
DoNotDisturb
DayDreaming
Need Group
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> msg
iconv -f CP949 -t UTF-8 <폴더>/…/msg.tbl
```
