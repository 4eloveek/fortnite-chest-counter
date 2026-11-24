# Компоненты приложения

Сам Fortnite Chest Counter распространяется по MIT License; текст находится
в `LICENSE` в исходниках и `licenses/FortniteChestCounter-MIT.txt` в приложении.
Лицензия собственного кода не заменяет лицензии компонентов ниже.

- **Tesseract .NET 5.2.0**, Charles Weld и участники проекта:
  <https://github.com/charlesw/tesseract>, Apache License 2.0.
- **Tesseract OCR 5**, участники tesseract-ocr:
  <https://github.com/tesseract-ocr/tesseract>, Apache License 2.0.
- **Модели eng/rus tessdata_fast**, участники tesseract-ocr:
  <https://github.com/tesseract-ocr/tessdata_fast>, Apache License 2.0.
- **Leptonica**, Dan Bloomberg и участники проекта:
  <https://github.com/DanBloomberg/leptonica>, BSD-style license.
- **.NET 10 и Windows Forms**, .NET Foundation и участники проекта:
  <https://github.com/dotnet/runtime>, <https://github.com/dotnet/winforms>, MIT.

В обычной сборке тексты лицензий находятся в папке `licenses` рядом с EXE.
В однофайловом релизе эти тексты включены внутрь EXE и при запуске извлекаются
в его служебный каталог `%TEMP%\.net\<имя EXE без расширения>\<идентификатор сборки>\licenses`.
Туда же извлекается этот документ. При заданном `DOTNET_BUNDLE_EXTRACT_BASE_DIR`
служебный каталог располагается под указанным путём, а не под `%TEMP%\.net`.
В полном комплекте разработки есть также NuGet-пакеты Microsoft Test Platform,
xUnit и Newtonsoft.Json; их лицензии включены в соответствующие `.nupkg`
в `assets/nuget`. Точный состав и версии перечислены в `assets/nuget/packages.json`.
Счётчик — независимый проект; товарные знаки Fortnite принадлежат их владельцам.
