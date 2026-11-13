# דעת אהרן — מערכת Windows חדשה

יישום Windows חדש שנבנה מאפס.

## טכנולוגיות
- C# / .NET 10 LTS
- WPF עם XAML
- MVVM-ready modular architecture
- SQLite / Entity Framework Core
- Microsoft Edge WebView2
- Windows x64

## בנייה מקומית
דרישות: Windows 10/11 ו-.NET 10 SDK.

```powershell
dotnet restore DaatAharon.sln
dotnet build DaatAharon.sln -c Release -p:Platform=x64
dotnet run --project src/DaatAharon.Desktop
```

קובצי אבחון נשמרים תחת:
`%LOCALAPPDATA%\DaatAharon\Logs\startup-errors.log`

## עקרונות
יציבות לפני תוספות, הפרדת אחריות, שמירת נתונים מקומית, בדיקות אוטומטיות ופרסום גרסאות רק לאחר Build מוצלח.
