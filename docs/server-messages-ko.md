# 서버가 하는 말 — 한국어로 (2026-09-24)

하데스 서버가 플레이어에게 보내는 말(0x0A 시스템 줄 · 로그인 창 0x02 · NPC 대화 · 게시판·교환 알림)을 한국어로 바꾼 표다.
**5.99 서버 실행 파일(`~/Downloads/5.99 서버팩/Novaonline.exe`)에 같은 뜻의 말이 있으면 그 말을 그대로 쓴다** — CP949 문자열을 뽑아 찾았다(아래 「근거」 칸의 `5.99 …`).
없으면 게임 말투(합쇼체, 원작의 「~습니다」)에 맞춰 새로 적었다(「새로」). 이름·숫자는 그대로 끼워 넣고, 받침을 알 수 없는 조사는 `을(를)`·`이(가)` 로 적는다(`EquipmentManager.cs` 의 기존 꼴).

- 코드 속 말 **116가지 · 245곳** (5.99 47 · 팩 6 · 새로 63) — `src/Hades.Server.Base`, `database/server/scripts`(Spells·Skills·Items·Formulas·Mundanes/ClassChooser)
- 설정 말 **42칸** (5.99 22 · 새로 9 · 틀에 이미 한국어였던 상인 말 11) — `src/Lorule.Config/LoruleConfig.json` 과 `scripts/server-config/LoruleConfig.template.json` 에 똑같이
- 모바일이 말을 가르는 규칙: `mobile/src/Lod.Mobile.Core/World/MessageSort.cs` (영어 꼴도 같은 규칙 안에 남겨 두었다 — 옛 빌드 서버용)

## 영어로 남긴 것과 까닭

| 무엇 | 어디 | 까닭 |
|---|---|---|
| 사용자 설정 글자 `AUTO LOOT GOLD: Toggle` · `Loot Mode` · `PVP` | `LoruleConfig` `Settings` | 캐릭터 설정 창에 뜨는 글자일 뿐, 2026-09-25 부터 금전 자동 줍기는 하지 않는다(`ObjectComponent.cs` — 금화는 바닥에 보여 주고 앱이 줍는다). 캐릭터 저장에도 그 글자로 남는다 |
| `HandShakeMessage` `CAN WE ALL GET ALONG` | `LoruleConfig` | 접속 규약의 일부 |
| 운영자 명령 결과 (`Port: Success.` · `spawnMonster` · `OnLearnSpell: True` …) · `[System]:` 방송 | `GameClientAPI.cs` · `Commander.cs` · `PingComponent.cs` | 운영자·개발용 |
| 배우기 조건 문구 (`LearningPredicate.cs`) | `Types/LearningPredicate.cs` | 하데스 스승 스크립트(`LearnSkills` · `GrandMaster01`)만 쓰는데, 세상에 선 NPC 는 모두 팩 스크립트라 닿지 않는다 |
| 세상에 없는 NPC·예제 스크립트 (`Banker` · `BarrenLord` · `UserHelper` · `test` · `gos` · `Delta` · `Benson` · `Macronator` · `TowerDefense*` · `ArenaMaster` · `Hades.Script/Examples` …) | `database/server/scripts/Mundanes`, `Monsters` | 템플릿이 부르지 않는다(`templates/mundanes` 의 스크립트는 `pack_speaker` · `shop1` · `NPC_*` · `Class Chooser` 뿐) |
| 쓰지 않는 기술 스크립트의 따로 쓴 말 (`Inspect` · `LocatePlayer` · `Sachel`) | `Skills/Rogue`, `Items/Sachel.cs` | 템플릿이 부르지 않는다. 여러 파일에 같은 말(`failed.` · `you cast …`)은 쓰지 않는 파일까지 함께 바꿨다 |
| 기록·예외 (`ServerContext.Logger` · `Exception`) | 곳곳 | 플레이어에게 가지 않는다 |
| 길드말 `{=o이름> {=a말` · 귓속말 `이름" 말` / `이름> 말` 꼴 | `GameServerHandlers.cs` | 영어가 아니라 원작 클라이언트의 꼴. 5.99 는 `[길드말]%s` 였지만 모바일이 앞의 꼴로 길드말을 알아보므로 두었다 |

## 코드 속 말

| 영어 | 한국어 | 근거 | 곳 |
|---|---|---|---|
| `$"You received {exp} Experience!."` | `$"경험치가 {exp} 올랐습니다"` | 5.99 `경험치가 %lu 올랐습니다` | 2 |
| `$"Special Drop: {rolledItem.DisplayName}"` | `$"귀한 물건이 떨어졌습니다: {rolledItem.DisplayName}"` | 새로 | 1 |
| `$"You've Received {amount} coins."` | `$"금전 {amount}전을 주웠습니다."` | 새로 (금전·전 = 5.99 표기) | 1 |
| `$"You are awarded {GoldReward} gold."` | `$"금전 {GoldReward}전을 받았습니다."` | 새로 | 1 |
| `$"You have recovered {item.Template.Name}."` | `$"{item.Template.Name}을(를) 되찾았습니다."` | 새로 | 1 |
| `$"Received {DisplayName}, You now have ({(item.Stacks == 0 ? item.Stacks + 1 : item.Stacks)})"` | `$"{DisplayName}을(를) 얻었습니다. ({(item.Stacks == 0 ? item.Stacks + 1 : item.Stacks)}개)"` | 새로 | 1 |
| `$"{DisplayName} Received."` | `$"{DisplayName}을(를) 얻었습니다."` | 새로 | 1 |
| `$"E: {Template.Name}, AC: {client.Aisling.Ac}"` | `$"{Template.Name}: 갑옷 강도 {client.Aisling.Ac}"` | 5.99 `갑옷 강도 %d` | 2 |
| `"{0} is too heavy to hold."` | `"{0}: 너무 무거워서 들 수 없습니다."` | 5.99 `너무 무거워서 들 수 없습니다.` | 1 |
| `"You stumble and drop {0}"` | `"비틀거리다 {0}을(를) 떨어뜨렸습니다."` | 새로 | 1 |
| `"{0} has improved. (Lv. {1})"` | `"{0}의 숙련도가 올랐습니다. (Lv. {1})"` | 새로 | 1 |
| `"{0} has improved."` | `"{0}의 숙련도가 올랐습니다."` | 새로 | 1 |
| `" has been killed by " + (target as Aisling).Username);` | `"님이 " + (target as Aisling).Username + "님에게 죽었습니다.");` | 새로 | 1 |
| `" has been killed, somehow."` | `"님이 죽었습니다."` | 새로 | 1 |
| `"You are forbidden to wear that."` | `"감히 사용할 수 없습니다."` | 5.99 `감히 사용할 수 없습니다.` | 1 |
| `$"{item.Template.Name} is almost broken!. Please repair it soon (< 10%)"` | `$"{item.Template.Name}이(가) 곧 부서집니다. 어서 고치십시오. (10% 미만)"` | 새로 | 1 |
| `$"{item.Template.Name} is wearing out soon. Please repair it ASAP. (< 30%)"` | `$"{item.Template.Name}이(가) 많이 닳았습니다. 되도록 빨리 고치십시오. (30% 미만)"` | 새로 | 1 |
| `$"{item.Template.Name} will need a repair soon. (< 50%)"` | `$"{item.Template.Name}을(를) 곧 고쳐야 합니다. (50% 미만)"` | 새로 | 1 |
| `                    partyLeader.Client.SystemMessage(                         $"{playerToAdd.Username} belongs to another party, and was not able to join your party.");                     playerToAdd.Client.SystemMessage(                         $"{partyLeader.Username}'s requested you to join his party. However you belong to another party.");` | `                    partyLeader.Client.SystemMessage($"{playerToAdd.Username}님은 이미 그룹 중 입니다.");                     playerToAdd.Client.SystemMessage("이미 그룹 중 입니다.");` | 5.99 `이미 그룹 중 입니다.` | 1 |
| `                playerToAdd.Client.SystemMessage(                     $"{partyLeader.Username} belongs to another party, and was not able to join your party.");                  partyLeader.Client.SystemMessage(                     $"{playerToAdd}'s requested you to join his party. However you belong to another party.");` | `                // 전에는 두 말이 뒤바뀌어 갔다(그룹에 든 쪽은 청한 이가, 청한 이는 제 객체 이름을 들었다).                 playerToAdd.Client.SystemMessage("이미 그룹 중 입니다.");                  partyLeader.Client.SystemMessage($"{playerToAdd.Username}님은 이미 그룹 중 입니다.");` | 5.99 `이미 그룹 중 입니다.` | 1 |
| `$"{playerToAdd.Username} has joined your party."` | `$"{playerToAdd.Username}님 그룹에 참여"` | 5.99 `%s님 그룹에 참여` | 1 |
| `$"You have joined {partyLeader.Username}'s party."` | `$"{partyLeader.Username}님의 그룹에 참여"` | 5.99 `%s님 그룹에 참여` 변형 | 2 |
| `$"{playerToAdd.Username} has joined the party."` | `$"{playerToAdd.Username}님 그룹에 참여"` | 5.99 `%s님 그룹에 참여` | 1 |
| `"The party has now been disbanded."` | `"그룹 해체"` | 5.99 `그룹 해체` | 1 |
| `$"{playerToRemove.Username} has left the party."` | `$"{playerToRemove.Username}님 그룹 해체"` | 5.99 `%s님 그룹 해체` | 1 |
| `$"{nextPlayer.Username} is now the party leader."` | `$"{nextPlayer.Username}님이 그룹장이 되셨습니다"` | 5.99 `%s님이 그룹장이 되셨습니다` | 1 |
| `"{0} is nowhere to be found."` | `"{0}님은 마이소시아에 없습니다"` | 5.99 `%s님은 마이소시아에 없습니다` | 1 |
| `"You are not in a guild."` | `"길드가 없습니다."` | 5.99 `길드가 없습니다.` | 1 |
| `"You can't hold this."` | `"더 이상 가질 수 없습니다."` | 5.99 `더 이상 가질 수 없습니다.` | 1 |
| `"They can't hold that."` | `"상대가 더 이상 가질 수 없습니다."` | 5.99 변형 | 1 |
| `"Trade was completed."` | `"교환에 성공하였습니다."` | 5.99 `교환에 성공하였습니다.` | 2 |
| `"Trade was aborted."` | `"교환이 취소되었습니다."` | 5.99 `교환이 취소되었습니다.` | 2 |
| `"Your have has been corrupted. Please report this bug to lorule staff."` | `"캐릭터 자료가 손상되었습니다. 운영자에게 알려 주십시오."` | 새로 | 2 |
| `"Unable to retrieve more."` | `"더 불러올 글이 없습니다."` | 새로 | 1 |
| `"Message Delivered."` | `"편지를 보냈습니다."` | 새로 | 1 |
| `"Post Added."` | `"글을 올렸습니다."` | 새로 | 1 |
| `"Post Deleted."` | `"글을 지웠습니다."` | 새로 | 2 |
| `$"There is no map configured for {aisling.AreaId}\0"` | `$"{aisling.AreaId}번 맵이 준비되어 있지 않습니다.\0"` | 새로 | 1 |
| `"Character Already Exists.\0"` | `"이미 등록된 계정입니다.\0"` | 5.99 `이미 등록된 계정입니다.` | 1 |
| `"Sorry, Incorrect Password."` | `"비밀번호가 틀렸습니다."` | 5.99 `비밀번호가 틀렸 습니다.`(띄어쓰기만 고침) | 1 |
| `$"{format.Username} does not exist in this world. You can make this hero by clicking on 'Create'."` | `$"{format.Username}: 없는 계정 입니다."` | 5.99 `없는 계정 입니다.` | 1 |
| `$"{format.Username} is not supported by the new server. Please remake your character. This will not happen when the server goes to beta."` | `$"{format.Username}: 이 서버에서 읽을 수 없는 캐릭터입니다. 새로 만들어 주십시오."` | 새로 | 1 |
| `"A valid primary class must be selected."` | `"직업을 골라 주십시오."` | 새로 | 1 |
| `"The selected class outfit is not configured."` | `"고른 직업의 옷이 준비되어 있지 않습니다."` | 새로 | 1 |
| `"The Monk starter skills are not configured."` | `"무도가의 첫 기술이 준비되어 있지 않습니다."` | 새로 | 1 |
| `"Incorrect Information provided."` | `"계정을 바르게 적어주시길 바랍니다."` | 5.99 `계정을 바르게 적어주시길 바랍니다.` | 2 |
| `"new password not accepted."` | `"암호를 바르게 적어주시길 바랍니다."` | 5.99 `암호을 바르게 적어주시길 바랍니다.`(조사만 고침) | 1 |
| `"Your skin turns to stone."` | `"피부가 돌처럼 단단해집니다."` | 새로 | 2 |
| `"Your skin turns back to flesh."` | `"피부가 원래대로 돌아옵니다."` | 새로 | 2 |
| `"Aite! You are empowered. You glow like gold."` | `"아이테! 몸이 금빛으로 빛나며 힘이 솟습니다."` | 새로 | 1 |
| `"Aite is gone. Your armor returns to normal."` | `"아이테가 끝났습니다. 방어력이 원래대로 돌아옵니다."` | 새로 | 1 |
| `"Your armor has been increased."` | `"방어력이 올랐습니다."` | 새로 | 1 |
| `"Your armor returns to normal."` | `"방어력이 원래대로 돌아옵니다."` | 새로 | 1 |
| `"Your hands are empowered!"` | `"두 손에 힘이 깃듭니다!"` | 새로 | 1 |
| `"Your hands turn back to normal."` | `"두 손이 원래대로 돌아옵니다."` | 새로 | 1 |
| `"You blend in to the shadows."` | `"그림자 속으로 몸을 숨깁니다."` | 새로 | 2 |
| `"You emerge from the shadows."` | `"그림자 밖으로 모습을 드러냅니다."` | 새로 | 1 |
| `"Spells attacking you now stop reflecting."` | `"마법 반사가 끝났습니다."` | 새로 | 1 |
| `"You've been incapacitated."` | `"몸이 굳어 움직일 수 없습니다."` | 새로 | 1 |
| `"Your are free again."` | `"다시 움직일 수 있습니다."` | 새로 | 1 |
| `"Your body is frozen."` | `"몸이 얼어 움직일수 없습니다."` | 5.99 `몸이 얼어 움직일수 없습니다.` | 1 |
| `"Your body thaws out."` | `"동면 끝."` | 5.99 `동면 끝.` | 1 |
| `"You have been put to sleep."` | `"잠이 쏟아져 옵니다."` | 5.99 `잠이 쏟아져 옵니다.` | 1 |
| `"awake!"` | `"잠에서 깨어났습니다."` | 새로 | 1 |
| `"You are infected with poison."` | `"중독되었습니다."` | 새로 | 1 |
| `"you feel better now."` | `"중독 끝."` | 5.99 `중독 끝.` | 1 |
| `"You are blinded!"` | `"눈이 멀었습니다!"` | 새로 | 1 |
| `"You can see again."` | `"다시 앞이 보입니다."` | 새로 | 1 |
| `"You return to normal."` | `"원래대로 돌아왔습니다."` | 새로 | 2 |
| `"Your armor feels light..."` | `"갑옷이 가벼워진 듯합니다..."` | 새로 | 1 |
| `"The hurricane has passed."` | `"허리케인이 지나갔습니다."` | 새로 | 1 |
| `$"{Name} has ended."` | `$"{Name} 끝."` | 5.99 `%s 끝` | 1 |
| `"You have died."` | `"죽었습니다."` | 새로 | 1 |
| `"you cast " + spell.Template.Name + "."` | `$"{spell.Template.Name}을(를) 외웠습니다."` | 5.99 `창조를 외웠습니다.` | 1 |
| `"you cast " + Spell.Template.Name + "."` | `$"{Spell.Template.Name}을(를) 외웠습니다."` | 5.99 `창조를 외웠습니다.` | 7 |
| `$"you cast {Spell.Template.Name}"` | `$"{Spell.Template.Name}을(를) 외웠습니다."` | 5.99 `창조를 외웠습니다.` | 23 |
| `$"You Cast {Spell.Template.Name}"` | `$"{Spell.Template.Name}을(를) 외웠습니다."` | 5.99 `창조를 외웠습니다.` | 3 |
| `$"You cast {Spell.Template.Name}."` | `$"{Spell.Template.Name}을(를) 외웠습니다."` | 5.99 `창조를 외웠습니다.` | 1 |
| `$"{client.Aisling.Username} Attacks you with {Spell.Template.Name}."` | `$"{client.Aisling.Username}님이 {Spell.Template.Name}(으)로 공격합니다."` | 새로 | 14 |
| `?? "Monster"} Attacks you with {Spell.Template.Name}."` | `?? "괴물"}이(가) {Spell.Template.Name}(으)로 공격합니다."` | 새로 | 13 |
| `$"{client.Aisling.Username} Removes {Spell.Template.Name} from you."` | `$"{client.Aisling.Username}님이 {Spell.Template.Name}을(를) 풀어 주셨습니다."` | 새로 | 6 |
| `?? "Monster"} Removes {Spell.Template.Name} from you."` | `?? "괴물"}이(가) {Spell.Template.Name}을(를) 풀었습니다."` | 새로 | 6 |
| `$"{client.Aisling.Username} Casts {Spell.Template.Name} on you. Elements augmented."` | `$"{client.Aisling.Username}님이 {Spell.Template.Name}을(를) 외워주셨습니다. 속성이 강해집니다."` | 5.99 `%s님이 슈페이아움을 외워주셧습니다.` | 2 |
| `?? "Monster"} Casts {Spell.Template.Name} on you. Elements augmented."` | `?? "괴물"}이(가) {Spell.Template.Name}을(를) 걸었습니다. 속성이 강해집니다."` | 새로 | 2 |
| `$"{client.Aisling.Username} casts {Spell.Template.Name} on you."` | `$"{client.Aisling.Username}님이 {Spell.Template.Name}을(를) 외워주셨습니다."` | 5.99 `%s님이 슈페이아움을 외워주셧습니다.` | 1 |
| `"Your spell has been deflected."` | `"걸리지 않습니다."` | 5.99 `걸리지 않습니다.` | 12 |
| `"failed."` | `"실패했습니다."` | 5.99 `실패했습니다.` | 22 |
| `"something went wrong."` | `"할 수 없습니다."` | 5.99 `할 수 없습니다.` | 2 |
| `$"Another curse is afflicted [{c.Name}]."` | `$"이미 저주가 걸려있습니다. [{c.Name}]"` | 5.99 `이미 저주가 걸려있습니다. [%s]` | 4 |
| `$"Another poison is already applied. [{c.Name}]."` | `$"이미 중독되어 있습니다. [{c.Name}]"` | 5.99 `이미 … 걸려있습니다. [%s]` 꼴 | 4 |
| `$"A greater cure is required [{c.Name}]"` | `$"더 강한 해제 마법이 필요합니다. [{c.Name}]"` | 새로 | 4 |
| `"Your skin is already like stone."` | `"이미 걸려있습니다."` | 5.99 `이미 걸려있습니다.` | 2 |
| `"You are already hidden."` | `"이미 걸려있습니다."` | 5.99 `이미 걸려있습니다.` | 1 |
| `"That target is already empowered."` | `"이미 걸려있습니다."` | 5.99 `이미 걸려있습니다.` | 1 |
| `"Spells are already being reflected."` | `"이미 걸려있습니다."` | 5.99 `이미 걸려있습니다.` | 1 |
| `"They are sleeping already."` | `"이미 걸려있습니다."` | 5.99 `이미 걸려있습니다.` | 1 |
| `"You already cast this."` | `"이미 걸려있습니다."` | 5.99 `이미 걸려있습니다.` | 1 |
| `"You have already casted that spell."` | `"이미 걸려있습니다."` | 5.99 `이미 걸려있습니다.` | 4 |
| `"you invoke your will."` | `"의지를 모읍니다."` | 새로 | 1 |
| `"you failed to concretrate."` | `"정신을 모으지 못했습니다."` | 새로 | 1 |
| `"The enemy has made it through."` | `"빗나갔습니다."` | 새로 | 4 |
| `"you have embarrassed yourself."` | `"망신만 당했습니다."` | 새로 | 1 |
| `"You require both hands to equip such an item."` | `"두 손을 모두 써야 하는 물건입니다."` | 새로 | 2 |
| `"I'm ready to choose a Path,"` | `"직업을 고르겠습니다."` | 새로 | 1 |
| `"I'm not ready."` | `"아직 아닙니다."` | 새로 | 1 |
| `"Hm? You look weak. you are a peasant. You can't survive this world without a set of skills and discipline. You must make a choice. Now is the time."` | `"음? 약해 보이는군. 아직 아무 직업도 없는 몸이야. 기술과 수련 없이는 이 세상에서 살아남을 수 없네. 이제 길을 골라야 할 때일세."` | 새로 | 1 |
| `"You have already chosen your path."` | `"이미 길을 골랐군."` | 새로 | 1 |
| `new OptionsDataItem(0x01, "Warrior")` | `new OptionsDataItem(0x01, "전사")` | 팩 `초보자도우미1` 직업 이름 | 1 |
| `new OptionsDataItem(0x02, "Rogue")` | `new OptionsDataItem(0x02, "도적")` | 팩 `초보자도우미1` 직업 이름 | 1 |
| `new OptionsDataItem(0x03, "Wizard")` | `new OptionsDataItem(0x03, "마법사")` | 팩 `초보자도우미1` 직업 이름 | 1 |
| `new OptionsDataItem(0x04, "Priest")` | `new OptionsDataItem(0x04, "성직자")` | 팩 `초보자도우미1` 직업 이름 | 1 |
| `new OptionsDataItem(0x05, "Monk")` | `new OptionsDataItem(0x05, "무도가")` | 팩 `초보자도우미1` 직업 이름 | 1 |
| `"What do you seek?"` | `"어느 직업을 선택 하겠습니까?"` | 팩 `초보자도우미1` `어느 직업을 선택 하겠습니까?` | 1 |
| `$"Congratulations! You are now a {Convert.ToString(client.Aisling.Path)}"` | `$"축하하네! 이제 자네는 {PathName(client.Aisling.Path)}일세."` | 새로 | 1 |
| `$"Devoted to the path of {Convert.ToString(client.Aisling.Path)} "` | `$"{PathName(client.Aisling.Path)}의 길에 들어섬"` | 새로 | 1 |
| `$"Alpha Aisling - Endured the harsh winter of the beginning"` | `$"첫 아이슬링 - 처음의 혹독한 겨울을 견뎌 냄"` | 새로 | 1 |

## 설정 말 (`LoruleConfig`)

| 칸 | 영어 | 한국어 | 근거 |
|---|---|---|---|
| `BadRequestMessage` | (Invalid Request) | (잘못된 요청) | 새로 |
| `ServerWelcomeMessage` | Welcome to Lorule | 어둠의 전설에 오신 것을 환영합니다! | 5.99 `노바온라인에 오신것을 환영합니다!` 꼴 |
| `CantAttack` | You can't attack that. | 공격할 수 없습니다. | 새로 |
| `CantCarryMoreMsg` | You can't carry more. | 소지품이 꽉 찼습니다. | 5.99 `소지품이 꽉 찼습니다.` |
| `CantDoThat` | You can't do that. | 할 수 없습니다. | 5.99 `할 수 없습니다.` |
| `CantDropItemMsg` | You can't drop that. | 떨어뜨릴 수 없는 물건입니다. | 5.99 `떨어뜨릴 수 없는 물건입니다.` |
| `CantEquipThatMessage` | You can't wear that. | 입을 수 없는 물건입니다. | 5.99 `입을 수 없는 물건입니다.` |
| `CantUseThat` | You can't use that. | 사용할 수 없는 물건입니다. | 5.99 `사용할 수 없는 물건입니다.` |
| `CantWearYetMessage` | You can't equip this yet. | 감히 사용할 수 없습니다. | 5.99 `감히 사용할 수 없습니다.` |
| `StrAddedMessage` | Your muscles begin to build fibers. | 힘이 올랐습니다. | 5.99 `힘이 올랐습니다.` |
| `IntAddedMessage` | Synapses in your mind expand. | 지력이 올랐습니다. | 5.99 `지력이 올랐습니다.` |
| `WisAddedMessage` | You're becoming wise beyond your years. | 지혜가 올랐습니다. | 5.99 `지혜가 올랐습니다.` |
| `ConAddedMessage` | You've become more fit. | 지구력이 올랐습니다. | 5.99 `지구력이 올랐습니다.` |
| `DexAddedMessage` | You're starting to feel more flexible. | 민첩이 올랐습니다. | 5.99 `민첩이 올랐습니다.` |
| `CursedItemMessage` | That does not belong to you... yet. | 아직 당신의 물건이 아닙니다. | 새로 |
| `DoesNotFitMessage` | That does not fit you. | 입을 수 없는 옷 입니다. | 5.99 `입을 수 없는 옷 입니다.` |
| `GroupRequestDeclinedMsg` | noname does not wish to join your group. | noname님은 그룹 거부 상태입니다 | 5.99 `%s님은 그룹 거부 상태입니다` |
| `LevelUpMessage` | Your insight has increased! | 레벨이 올랐습니다! | 5.99 `레벨이 올랐습니다!` |
| `MerchantBuy` | Buy | 삽니다 | 기존(틀) |
| `MerchantBuyMessage` | What you looking for? | 무엇을 찾으십니까? | 기존(틀) |
| `MerchantCancelMessage` | No Thanks. | 그만두겠습니다 | 기존(틀) |
| `MerchantConfirmMessage` | Yes Please! | 그렇게 하지요 | 기존(틀) |
| `MerchantDefaultMessage` | Ok, Good Bye then. | 살펴 가십시오. | 기존(틀) |
| `MerchantRefuseTradeMessage` | I don't want to buy that. | 그것은 사지 않습니다. | 기존(틀) |
| `MerchantSell` | Sell | 팝니다 | 기존(틀) |
| `MerchantStackErrorMessage` | You don't even have that many. | 그만큼 가지고 있지 않습니다. | 기존(틀) |
| `MerchantTradeCompletedMessage` | Thanks. | 고맙습니다. | 기존(틀) |
| `MerchantTradeErrorMessage` | You should probably leave. | 거래할 수 없습니다. | 기존(틀) |
| `MerchantWarningMessage` | Hey you don't even have the money!! Don't waste my time. | 돈이 모자랍니다. | 기존(틀) |
| `NoManaMessage` | Your will is too weak. | 마력이 부족합니다. | 새로 |
| `NotEnoughGoldToDropMsg` | You don't have enough gold. | 돈이 부족합니다. | 5.99 `돈이 부족합니다.` |
| `ReapMessage` | You are dying.\|You cannot move nor raise your arms.\|Barron is going to take your soul.\|All things eventually come to an end. | 죽어 가고 있습니다.\|움직일 수도, 팔을 들 수도 없습니다.\|바론이 당신의 영혼을 거두러 옵니다.\|모든 것에는 끝이 있는 법입니다. | 새로 |
| `ReapMessageDuringAction` | You can't do that, you are about to die! | 죽음의 그림자가 드리웁니다. | 5.99 `죽음의 그림자가 드리웁니다.` |
| `RepairItemMessage` | You can't wear it anymore, it's Broken. | 부서져서 더 이상 입을 수 없습니다. | 새로 |
| `SomethingWentWrong` | Something went wrong. | 할 수 없습니다. | 5.99 `할 수 없습니다.` |
| `SpellFailedMessage` | Something backfired!. | 마법이 실패했습니다. | 5.99 `실패했습니다.` 꼴 |
| `ToWeakToLift` | You are to weak and pathetic to lift it. | 너무 무거워서 들 수 없습니다. | 5.99 `너무 무거워서 들 수 없습니다.` |
| `UserDroppedGoldMsg` | noname has dropped some money nearby. | noname님이 근처에 돈을 떨어뜨렸습니다. | 새로 |
| `WrongClassMessage` | This doesn't quite fit right. | 감히 사용할 수 없습니다. | 5.99 `감히 사용할 수 없습니다.` |
| `YouDroppedGoldMsg` | you've dropped some gold. | 돈을 버렸습니다. | 5.99 `돈을 버렸습니다.` |
| `ItemNotRequiredMsg` | Come back when you have the items required. \n{=q | 필요한 물건을 가지고 다시 오십시오. \n{=q | 새로 |
| `DeathReepingMessage` | we can't go back now... | 이제 되돌릴 수 없습니다... | 새로 |

`ReapMessage` 는 혼수 중에 1초마다 넷 중 하나를 고른다(`debuff_reeping.cs`). 첫 줄 「죽어 가고 있습니다.」가 모바일 배너 「혼수 상태」가 된다. 5.99 의 「죽음의 그림자가 드리웁니다.」는 5.99 에서 혼수 중에 무엇을 하려 할 때의 거절이라 `ReapMessageDuringAction` 에 썼다.

다시 만들기: 이 표는 바꾸기 스크립트가 쓴 목록에서 뽑았다. 새 말을 더하면 이 표에 한 줄, `MessageSort.cs` 규칙과 `MessageSortTests.cs` 에 한 줄씩 더한다.
