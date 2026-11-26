# Работа с проектом на Windows

## Подготовка

1. Распакуйте `FortniteChestCounter-Workspace.zip` в доступную для записи папку,
   например `C:\Projects\fortnite-chest-counter`.
2. Для запуска готового приложения откройте `Run.cmd`.
3. Для правок установите **.NET SDK 10**, затем проверьте в терминале:

   ```powershell
   dotnet --version
   dotnet --list-sdks
   ```

4. Откройте папку в редакторе C# или `FortniteChestCounter.sln` в IDE
   с поддержкой .NET 10. В решении есть Core, Windows и Tests.

Готовое приложение уже содержит .NET Runtime. Компилятор и средства сборки
предоставляет отдельно устанавливаемый SDK.

Если команда `dotnet` не найдена, установите **SDK 10 для Windows x64**, а не
только Runtime, с <https://dotnet.microsoft.com/download/dotnet/10.0>.
После установки заново откройте терминал; запущенный ранее редактор или OpenCode
может также потребовать перезапуска, чтобы увидеть обновлённый `PATH`.

## Пересборка одной командой

Дважды нажмите `Build.cmd` или запустите в PowerShell из корня:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build.ps1
```

Скрипт проверяет модели, восстанавливает зависимости, запускает тесты,
публикует `dist\FortniteChestCounter`, обновляет локальные NuGet-пакеты
и создаёт два архива: программу и полный комплект разработки.

Скрипты обращаются к файлам через путь собственной папки. Пробелы и кириллица
в расположении проекта поддерживаются.

## Сборка с локальными пакетами

`assets\nuget` содержит `.nupkg` этой проверенной сборки: OCR, тестовые пакеты,
Windows Desktop и Windows x64 runtime-пакеты. Контрольные суммы перечислены
в `assets\nuget\packages.json`.

Для использования только этих пакетов:

```powershell
.\Build.cmd -Offline
.\Test.cmd -Offline
```

Исходная Linux-проверка использовала SDK **10.0.402**, Runtime **10.0.13**.
Опубликованная **0.2.0** проверена на Windows с SDK **10.0.404**, Runtime **10.0.15**;
`packages.json` перечисляет 17 пакетов этой сборки, а прежние `.nupkg` сохранены
в папке (всего 22 файла). Исходники **0.3.1** на Linux: 90 тестов Core и
кросс-компиляция Windows без предупреждений. Пользователь проверил исправленную
сборку в игре; количественная точность не измерялась. Другой выпуск
SDK 10 может запросить свою версию системных пакетов; обычная сборка получит
её с nuget.org и обновит локальный комплект. Сам .NET SDK не является частью ZIP.
В режиме `-Offline` сетевой аудит NuGet отключается для этой команды восстановления,
поскольку он требует доступа к серверу; обычная сборка использует штатные настройки.

После первой сборки распакованные пакеты находятся в `.packages` внутри проекта.

Если изменяете версии или состав NuGet-зависимостей, сначала обновите lock-файлы:

```powershell
dotnet restore .\FortniteChestCounter.sln --force-evaluate
```

Затем выполните `Build.cmd`: он проверит обновлённые lock-файлы и соберёт
новые `.nupkg` в локальный комплект для последующих переносов.

## Отдельные команды

```powershell
# Восстановить зафиксированные NuGet-зависимости
dotnet restore .\FortniteChestCounter.sln --locked-mode

# Проверить чистую логику
dotnet test .\tests\FortniteChestCounter.Tests\FortniteChestCounter.Tests.csproj -c Release

# Собрать Windows-приложение для отладки
dotnet build .\src\FortniteChestCounter.Windows\FortniteChestCounter.Windows.csproj -c Debug

# Опубликовать готовую самостоятельную сборку
dotnet publish .\src\FortniteChestCounter.Windows\FortniteChestCounter.Windows.csproj -c Release -o .\dist\FortniteChestCounter

# Упаковать всю папку после изменений
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-workspace.ps1
```

`Package.cmd` включает текущую опубликованную программу. Чтобы в ZIP попали
изменения исходников и соответствующая новая программа, используйте `Build.cmd`.

## Отладка

- Проект запуска: `src\FortniteChestCounter.Windows`.
- UI создаётся кодом в `MainForm.BuildContent`, отдельных Designer-файлов нет.
- Ошибки подготовки картинки ищите в `OcrReader.Read` / `Prepare`.
- Ошибки классификации — в `NotificationClassifier.Find`.
- Повторы и быстрые открытия — в `NotificationGate.Observe`.
- Дневная агрегация — в `CounterLedger.DailyCounts`; окно и график —
  в `StatisticsForm.cs` / `DailyChartControl.cs`.
- Поле диагностики окна показывает распознанные строки с уверенностью.
- Для воспроизведения вне игры можно открыть сохранённый кадр на экране,
  выбрать область и на время проверки выключить ограничение активного Fortnite.
  Счётчик тогда реагирует на изображение, а не на реальные события матча.

Добавляйте тест для подтверждённой ошибки в Core. Для качества изображения
сохраняйте реальный пример в `assets\samples` и описывайте ожидаемое событие.

## Отчёты и данные

- `Test.cmd` сохраняет вывод и TRX в `diagnostics\tests-...`.
- `Diagnostics.cmd` сохраняет описание ОС, SDK, комплектности файлов,
  хеши моделей и копии пользовательских `settings.json` / `state.json`, если они есть.
- Рабочие JSON-файлы программы находятся в `%LOCALAPPDATA%\FortniteChestCounter`.
- `docs\VERIFICATION.md` описывает проверку исходного переносимого комплекта.