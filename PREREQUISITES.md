# O'rnatish kerak bo'lgan dasturlar

Desktop Dashboard (C#, WinUI 3, Windows App SDK, .NET) loyihasini build qilish uchun kerakli dasturlar ro'yxati.

## Joriy holat (2026-10-20 dagi tekshiruv)

| Komponent | Holat |
|---|---|
| Windows 11 Pro, build 26300, x64 | ✅ Bor |
| Developer Mode | ✅ Yoqilgan |
| Windows App Runtime (1.5 – 1.8, 2.x) | ✅ Bor |
| git, winget | ✅ Bor |
| **.NET SDK** | ❌ **Yo'q, o'rnatish shart** |
| Visual Studio / Build Tools | ❌ Yo'q (ixtiyoriy) |
| Windows SDK (Windows Kits) | ❌ Yo'q (shart emas, NuGet orqali keladi) |
| Inno Setup | ❌ Yo'q (faqat 7-bosqichda kerak) |

---

## 1. Majburiy: .NET 10 SDK

Loyihani build qilish uchun faqat shu kerak. Hajmi taxminan 1 GB.

PowerShell'da bajaring:

```powershell
winget install Microsoft.DotNet.SDK.10
```

O'rnatgandan keyin **terminalni yopib, qaytadan oching** (PATH yangilanishi uchun) va tekshiring:

```powershell
dotnet --list-sdks
```

`10.0.xxx` ko'rinsa, hammasi joyida.

> Windows App SDK, Windows SDK Build Tools, CommunityToolkit.Mvvm va boshqa kutubxonalar
> NuGet paketlari sifatida build vaqtida avtomatik yuklab olinadi. Ularni alohida o'rnatish shart emas.

---

## 2. Ixtiyoriy: Visual Studio 2026 Community

Majburiy emas. Quyidagilar kerak bo'lsa o'rnating: to'liq debugger, XAML Hot Reload va Live Visual Tree.
Hajmi taxminan 10–20 GB.

1. https://visualstudio.microsoft.com/ saytidan **Visual Studio Community** ni yuklab oling.
2. Installer'da quyidagi workload'ni tanlang:
   - **WinUI application development**
3. O'rnatishni yakunlang.

Visual Studio bilan .NET SDK ham birga o'rnatiladi, shuning uchun bu variantni tanlasangiz, 1-bandni o'tkazib yuborsa bo'ladi.

---

## 3. Keyinroq (7-bosqich, installer uchun): Inno Setup

Hozir o'rnatish shart emas. Tayyor ilova uchun `.exe` installer yasash bosqichida kerak bo'ladi.

```powershell
winget install JRSoftware.InnoSetup
```

---

## Qisqacha

| Nima | Qachon | Buyruq |
|---|---|---|
| .NET 10 SDK | **Hozir** | `winget install Microsoft.DotNet.SDK.10` |
| Visual Studio 2026 | Ixtiyoriy | visualstudio.microsoft.com |
| Inno Setup | 7-bosqichda | `winget install JRSoftware.InnoSetup` |

.NET SDK o'rnatilgach, menga xabar bering. Shundan keyin 3-bosqichni boshlayman: minimal WinUI 3 ilovani yaratib build qilaman.
