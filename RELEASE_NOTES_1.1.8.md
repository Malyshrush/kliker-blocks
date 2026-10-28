# Kliker Blocks 1.1.8 — публичная бета

## Русский

- Добавлены блоки «Дождаться элемента» и «Действие с элементом» для доступных элементов окон Windows через UI Automation. Запись макросов получила режим кликов по элементам; если элемент недоступен, остаётся координатный клик.
- Блок «Ожидать событие» ждёт появления или закрытия окна либо изменения файла. Точки останова, подсветка активного блока, просмотр переменных, шаг и продолжение помогают отладить граф.
- Добавлены блоки HTTP POST и «Поле JSON». Отправляемые данные поступают по указанному пользователем адресу. Дочерний сценарий теперь может получать входные значения и возвращать выбранные переменные.
- Для каждой входной и выходной точки блока можно задать форму, размер, цвет и прозрачность. У связей отдельно задаются размер наконечника и расстояния изгибов от выхода и входа. При нулевых расстояниях и свободном пути связь прямая. Подпись «Далее» у выходной точки скрыта.
- Обновлены русская и английская справка и примеры. Python и модели перевода не входят в выпуск.

В папке выпуска находятся переносимые ZIP-сборки и установщики для Windows x64 и x86. Выберите архитектуру своей системы. Установщик предлагает папку, значок на рабочем столе, автозапуск и запуск после установки. Закрепление на панели задач пользователь выполняет средствами Windows.

## English

- Added Wait for element and Element action blocks for accessible Windows controls through UI Automation. Macro recording can capture semantic clicks and falls back to coordinates when a control has no usable selector.
- Wait for event handles window opening/closing and file changes. Breakpoints, active-block highlighting, a variable viewer, Step and Continue help debug graphs.
- Added HTTP POST and JSON field blocks. Requests send data to the URL chosen by the user. Child scenarios can now receive inputs and return selected variables.
- Each input and output port has configurable shape, size, color and opacity. Connections have independent end marker size and bend distances. Zero distances yield a straight connection when the path is clear. The Next caption beside output ports is hidden.
- Updated English and Russian Help with examples. Python and translation models are not bundled.

The release folder contains portable ZIP builds and installers for Windows x64 and x86. Choose the build matching your system. The installer offers a destination folder, desktop shortcut, autostart and launch-after-install options. Pinning to the taskbar requires a Windows user action.
