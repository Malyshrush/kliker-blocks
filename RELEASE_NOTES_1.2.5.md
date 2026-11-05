# Kliker Blocks 1.2.5

Выпуск для Windows x64 и x86.

- Экранное меню открывается сочетанием Ctrl+Alt+T; клавишу и папку файлов можно изменить через «Захват → Экранное меню: настройки». Меню копирует выделение командой Ctrl+C, меняя буфер обмена. Текст можно посмотреть, озвучить голосом Windows, сохранить в TXT или перенести в заметку.
- Из меню можно сохранить выбранную прямоугольную область в PNG и распознать её локально через Tesseract. Языки OCR задаются в меню, а недостающий движок и модели предлагается установить после согласия. Некоторые приложения и окна с повышенными правами могут не отдавать выделение.
- В меню можно добавить сохранённые графы `.klb` и создать примеры. Им передаются переменные `selected_text`, `screenshot_path` и `output_folder`. Граф можно остановить из меню.
- Экранные заметки содержат текст и одно изображение. Заголовок и кнопки-значки с подсказками появляются по щелчку и скрываются через три секунды; за заголовок заметку можно передвинуть. Синий разделитель меняет высоту текста и изображения; части можно удалить и добавить снова. Заметки сворачиваются, сохраняются в `.klnote`, открываются и удаляются. Блок «Заметка» управляет открытыми окнами и файлами по идентификатору.
- В заметках выделенный текст можно форматировать: шрифт, размер, цвет, жирный, курсив и подчёркивание. Ctrl+плюс/минус меняет масштаб изображения, мышью его можно перетащить; положение и форматирование сохраняются. Контекстное меню изображения сбрасывает масштаб и положение.
- Индикатор графа можно перетащить мышью; новые координаты сохраняются вместе с проектом. В визуальном редакторе можно удалить лишние блоки «Старт», сохранив хотя бы один.

Сборка x86 проверялась на 64-разрядной Windows в режиме совместимости; отдельная 32-разрядная Windows и установка Tesseract с реальным запросом UAC не проверялись.

# Kliker Blocks 1.2.5

Release for Windows x64 and x86.

- Open the screen menu with Ctrl+Alt+T; configure its hotkey and output folder under Capture → Screen menu settings. It copies the selection with Ctrl+C, changing the clipboard. View it, speak it with a Windows voice, save a TXT file or create a note.
- Capture a rectangular area as PNG and recognize it locally with Tesseract. Choose OCR languages in the menu; missing components are offered for installation after consent. Some applications and elevated windows may not expose a selection.
- Add saved `.klb` graphs or create examples in settings. Graphs receive `selected_text`, `screenshot_path` and `output_folder`. The menu can stop the running graph.
- Screen notes hold text and one image. Click a note to show its title and icon buttons with tooltips; the header hides after three seconds. Drag the header to move the note or the blue divider to resize its sections. Remove and re-add sections, minimize, save to `.klnote`, reopen or delete notes. The Note block manages open notes and saved files by ID.
- Format selected note text with font, size, color, bold, italic and underline. Ctrl+plus/minus zooms the image; drag it to reposition it. Formatting and view are saved. The image context menu resets the view.
- Drag the scenario indicator with the mouse and save the project to retain its position. Extra Start blocks can be deleted in the visual editor while preserving at least one.

The x86 build was tested on 64-bit Windows using compatibility mode. A separate 32-bit Windows machine and Tesseract installation with a live UAC prompt were not tested.
