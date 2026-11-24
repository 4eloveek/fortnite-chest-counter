# Fortnite Chest Counter

C# / .NET 10, Windows Forms, Windows x64. Исходники собственного приложения — MIT.

## Устройство

- `src/FortniteChestCounter.Core` — классификация уведомлений, gate, журнал событий,
  дневная статистика и атомарная запись JSON. Не зависит от WinForms.
- `src/FortniteChestCounter.Windows` — захват GDI, Tesseract, UI, график и оверлей.
- `tests/FortniteChestCounter.Tests` — 73 теста чистой логики.
- `assets/tessdata` — модели eng/rus; лицензии — `assets/licenses` и `LICENSE`.
- `tools/verify-release` — исходники независимой проверки однофайлового EXE.

## Работа

Сохраняйте разделение Core/Windows и существующую пользовательскую историю.
Данные приложения находятся в `%LOCALAPPDATA%/FortniteChestCounter`, не в репозитории.
Для изменения OCR нужны настоящие уведомления и отрицательные примеры; новые
пороги нельзя обосновывать только синтетическими картинками. Тесты Core и успешная
сборка не доказывают точность полного матча или отсутствие повторов во всех режимах.

Скрипты поддерживают Windows PowerShell 5.1, пробелы и кириллицу в путях.
Не включайте в Git личную статистику, видеозаписи, диагностические отчёты и кэш.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

`-Offline` использует локальные `.nupkg`: они присутствуют в подготовленном
комплекте, но игнорируются Git. После клонирования используйте обычную онлайн-сборку.
При изменении зависимостей обновляйте lock-файлы через `dotnet restore --force-evaluate`.
Чистые тесты Core работают и на Linux; готовый GUI/EXE предназначен для Windows.
