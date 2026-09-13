---
이름: "방어 — AC 적용"
갈래: 식
근거수: 5
출처: "database/server/scripts/Formulas/ac.cs"
---

# 방어 — AC 적용

`database/server/scripts/Formulas/ac.cs` 에서 5줄.

```csharp
database/server/scripts/Formulas/ac.cs:33  var armor = obj.Ac;
database/server/scripts/Formulas/ac.cs:35  var calculatedDmg = value * (armor + 101) / 99;
database/server/scripts/Formulas/ac.cs:39  if (calculatedDmg < 1)
database/server/scripts/Formulas/ac.cs:40  calculatedDmg = 1;
database/server/scripts/Formulas/ac.cs:42  return calculatedDmg;
```

## 읽는 설정

이 식이 쓰는 값은 [[설정/LoruleConfig|LoruleConfig]] 에 있다.
