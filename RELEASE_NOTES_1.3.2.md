# Kliker Blocks 1.3.2

Добавлены отдельные блоки «НЕ», «И», «ИЛИ» и «ТОЛЬКО» для объединения существующих блоков «Если». «ТОЛЬКО» означает, что истинно ровно одно из двух условий (XOR). Соедините условие с логическим блоком, а выход «Условие» блока И, ИЛИ или ТОЛЬКО — со вторым блоком «Если».

Исправлены копируемые примеры логических блоков: теперь они проверяют Shift и Ctrl. Обновление журнала ограничено по объёму и выполняется порциями; при перегрузке пропуски отображаются в окне. Причина сообщения Windows «Не отвечает» в пользовательском сценарии пока не подтверждена; нагрузочный UI-тест в текущей среде не прошёл, требуется дополнительная проверка.

Добавлен блок Telegram для отправки сообщения или файла с подписью в группу, канал или личный чат, куда бот имеет право писать. Токен задаётся в «Настройки → Telegram» и зашифрован Windows для текущей учётной записи. Telegram Bot API отправляет данные по HTTPS; получатель должен запустить бота командой `/start`, чтобы бот мог писать ему лично.

Ограничения: токен находится в пути HTTPS-запроса к Telegram и может быть виден включённой внешней HTTP-диагностике. x86 проверена на 64-разрядной Windows через совместимость. Открытые проверки и регрессии перечислены в BUG_REGISTRY.md; не все ошибки объявлены исправленными.

# Kliker Blocks 1.3.2

Added separate NOT, AND, OR and ONLY blocks to combine existing If conditions. ONLY means exactly one of two conditions is true (XOR). Connect an If block to the logic block and connect the Condition output of AND, OR or ONLY to the second If block.

Fixed copyable logic block examples to check Shift and Ctrl. Journal updates use a bounded queue and batches; skipped on-screen entries are reported. The cause of the reported Windows unresponsive state remains unconfirmed; the UI load test did not pass in this environment and needs further checking.

Added a Telegram block to send a message or a file with a caption to a group, channel or private chat where the bot can post. Set the token in Settings → Telegram; Windows encrypts it for the current account. The Telegram Bot API sends data over HTTPS. A recipient must start the bot with `/start` before it can message them privately.

Limitations: the bot token is in the HTTPS request path to Telegram and may be visible to enabled external HTTP diagnostics. The x86 build was checked on 64-bit Windows using compatibility mode. Open issues and remaining checks are listed in BUG_REGISTRY.md.
