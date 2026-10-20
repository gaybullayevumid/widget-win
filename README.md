# Desktop Dashboard

C#, WinUI 3 va Windows App SDK'da yozilgan, Windows 11 uchun ixcham va doim ochiq turadigan tizim monitori.

- Soat va sana (12/24 soat, sekundlar)
- CPU yuklamasi (Task Manager'dagi kabi `% Processor Utility`)
- RAM
- GPU: yuklama, VRAM va harorat (drayver bergan taqdirda)
- Batareya: foiz va zaryadlanish holati
- Tarmoq: yuklab olish va yuborish tezligi

Har bir qiymat haqiqiy OS yoki drayver manbasidan olinadi. Metrikani o'qib bo'lmasa, kartada uydirma raqam emas, **"—"** va sababi ko'rsatiladi.

## Imkoniyatlar

- Acrylic, Mica yoki Solid fon; shaffoflik, accent rang, yorug'/qorong'i mavzu, Small/Medium/Large o'lcham
- Har bir kartani alohida yoqish va o'chirish mumkin
- Oynani surish va o'lchamini o'zgartirish; "Keep on top" rejimi
- Tray ikonkasi: chap tugma ko'rsatadi/yashiradi, o'ng tugma menyu ochadi. Alt+F4 oynani tray'ga yashiradi
- Windows bilan birga ishga tushish (HKCU Run kaliti, admin huquqi talab qilinmaydi)
- Oyna joylashuvi va sozlamalar `%LOCALAPPDATA%\DesktopDashboard\settings.json` faylida saqlanadi. Monitor uzilgan bo'lsa, oyna ko'rinadigan joyga qaytariladi
- Faqat bitta nusxa ishlaydi: qayta ishga tushirilganda mavjud oyna ko'rsatiladi

## Metrika manbalari

| Metrika | Manba |
|---|---|
| CPU | PDH `\Processor Information(_Total)\% Processor Utility` (zaxira: `GetSystemTimes`) |
| RAM | `GlobalMemoryStatusEx` |
| GPU yuklamasi | PDH `\GPU Engine(*)\Utilization Percentage`: har bir engine bo'yicha yig'indi, eng band engine olinadi |
| VRAM | PDH `\GPU Adapter Memory(*)\Dedicated Usage` + DXGI `DedicatedVideoMemory` |
| GPU harorati | D3DKMT `KMTQAITYPE_ADAPTERPERFDATA` (Task Manager ishlatadigan manba) |
| Batareya | `GetSystemPowerStatus` |
| Tarmoq | `GetIfTable2`: faqat fizik, ulangan, filter bo'lmagan adapterlar |

## Tuzilishi

```
src/DesktopDashboard.Core/   UI'siz kutubxona: modellar, metrika manbalari, sozlamalar, interop
src/DesktopDashboard/        WinUI 3 ilova: Views, ViewModels (MVVM), Controls, Services
tests/                       xUnit v3 testlari (unit + shu kompyuterdagi jonli manbalar)
installer/                   build.ps1 + Inno Setup skripti
```

## Build va ishga tushirish

Talablar: .NET 10 SDK. Visual Studio ixtiyoriy.

```powershell
dotnet build src\DesktopDashboard\DesktopDashboard.csproj
dotnet test --project tests\DesktopDashboard.Core.Tests
.\src\DesktopDashboard\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\DesktopDashboard.exe
```

Testlar uchun alohida sozlamalar fayli: `$env:DESKTOPDASHBOARD_SETTINGS = "C:\path\settings.json"`.

## Installer

```powershell
.\installer\build.ps1 -Version 0.1.0
```

Skript testlarni ishga tushiradi va trimmed + ReadyToRun self-contained build'ni `artifacts\publish\win-x64` papkasiga (≈91 MB) publish qiladi. Inno Setup 6 o'rnatilgan bo'lsa, `artifacts\installer\` papkasida installer ham yaratiladi.

Installer foydalanuvchi uchun o'rnatadi (admin kerak emas): `%LOCALAPPDATA%\Programs\Desktop Dashboard`. U Start menyu yorlig'ini, ixtiyoriy desktop yorlig'i va startup'ni qo'shadi. O'chirishda startup yozuvi ham olib tashlanadi.
