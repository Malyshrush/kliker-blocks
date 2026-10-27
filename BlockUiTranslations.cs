namespace KlikerBlocks;

// Supplemental translations for block captions, field labels and choices.
// Format: original Russian | English | French | Spanish | Simplified Chinese.
internal static class BlockUiTranslations
{
    private static readonly Dictionary<string, string[]> Rows = Parse();

    public static IEnumerable<KeyValuePair<string, string>> For(string code)
    {
        var index = code switch { "en" => 0, "fr" => 1, "es" => 2, "zh" => 3, _ => -1 };
        if (index < 0) yield break;
        foreach (var (key, values) in Rows) yield return new KeyValuePair<string, string>(key, values[index]);
    }

    private static Dictionary<string, string[]> Parse()
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var line in Data.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var columns = line.Split('|');
            if (columns.Length != 5 || !result.TryAdd(columns[0].Replace("\\n", "\n"),
                    columns[1..].Select(value => value.Replace("\\n", "\n")).ToArray()))
                throw new InvalidOperationException("Invalid block localization row: " + line);
        }
        return result;
    }

    private const string Data = """
О программе…|About…|À propos…|Acerca de…|关于…
Что я умею|What I can do|Ce que je sais faire|Lo que puedo hacer|我的功能
Демонстрация Что я умею|What I can do demo|Démonstration Ce que je sais faire|Demo Lo que puedo hacer|我的功能演示
Окна и точность демонстрации|Demo windows and text accuracy|Fenêtres et précision de la démo|Ventanas y precisión de la demo|演示窗口与文字准确性
Что я умею — интерактивная демонстрация|What I can do — interactive demo|Ce que je sais faire — démonstration interactive|Lo que puedo hacer — demostración interactiva|我的功能——互动演示
Шаг демонстрации|Demo step|Étape de démonstration|Paso de demostración|演示步骤
Приветствие голосом|Spoken welcome|Accueil vocal|Bienvenida hablada|语音欢迎
▶ Начать демонстрацию|▶ Start demo|▶ Démarrer la démo|▶ Iniciar demo|▶ 开始演示
■ Остановить демонстрацию|■ Stop demo|■ Arrêter la démo|■ Detener demo|■ 停止演示
Сохранить граф…|Save graph…|Enregistrer le graphe…|Guardar grafo…|保存图表…
Граф загружен. Нажмите «Начать», чтобы управлять Блокнотом, Paint и буфером обмена.|Graph loaded. Press Start to use Notepad, Paint and the clipboard.|Graphe chargé. Lancez pour utiliser le Bloc-notes, Paint et le presse-papiers.|Grafo cargado. Inicie para usar Bloc de notas, Paint y portapapeles.|图表已加载。点击开始以操作记事本、画图和剪贴板。
Журнал демонстрации|Demo journal|Journal de démonstration|Registro de demostración|演示日志
Kliker Blocks откроет Блокнот и Paint, введёт текст, изменит буфер обмена и сохранит рисунок, отчёт и снимок в своей папке. Продолжить? Остановить можно кнопкой или Esc.|Kliker Blocks will open Notepad and Paint, type text, change the clipboard, and save a drawing, report and screenshot in its folder. Continue? Use Stop or Esc to interrupt.|Kliker Blocks ouvrira Bloc-notes et Paint, saisira du texte, modifiera le presse-papiers et enregistrera dessin, rapport et capture dans son dossier. Continuer ? Arrêt ou Échap interrompt.|Kliker Blocks abrirá Bloc de notas y Paint, escribirá texto, cambiará el portapapeles y guardará un dibujo, informe y captura en su carpeta. ¿Continuar? Use Detener o Esc.|Kliker Blocks 将打开记事本和画图、输入文字、更改剪贴板，并在其文件夹中保存绘图、报告和截图。继续吗？可用停止按钮或 Esc 中断。
Демонстрация выполняется; Esc или «Остановить» прервёт её.|Demo running; Esc or Stop will interrupt it.|Démo en cours ; Échap ou Arrêter l’interrompt.|Demo en curso; Esc o Detener la interrumpe.|演示正在运行；按 Esc 或停止可中断。
Демонстрация завершена. Граф можно изучить и сохранить.|Demo finished. You can inspect and save the graph.|Démo terminée. Vous pouvez examiner et enregistrer le graphe.|Demo terminada. Puede examinar y guardar el grafo.|演示已完成。可查看并保存图表。
Демонстрация завершена. Рисунок в Paint, снимок и отчёт сохранены; пути есть в журнале.|Demo finished. The Paint drawing, screenshot and report were saved; see paths in the journal.|Démo terminée. Le dessin Paint, la capture et le rapport sont enregistrés ; voir les chemins dans le journal.|Demo terminada. Se guardaron el dibujo de Paint, la captura y el informe; consulte las rutas en el registro.|演示完成。画图中的绘图、截图和报告已保存；路径见日志。
Демонстрация прервана: {0}|Demo interrupted: {0}|Démo interrompue : {0}|Demo interrumpida: {0}|演示中断：{0}
Демонстрация остановлена пользователем.|Demo stopped by user.|Démo arrêtée par l’utilisateur.|Demo detenida por el usuario.|用户已停止演示。
Демонстрация остановлена.|Demo stopped.|Démo arrêtée.|Demo detenida.|演示已停止。
Демонстрация прервана; подробности в журнале.|Demo interrupted; see journal for details.|Démo interrompue ; détails dans le journal.|Demo interrumpida; vea el registro.|演示中断；详情见日志。
Сначала остановите обычный сценарий Kliker Blocks.|Stop the regular Kliker Blocks scenario first.|Arrêtez d’abord le scénario Kliker Blocks normal.|Detenga primero el escenario normal de Kliker Blocks.|请先停止常规 Kliker Blocks 场景。
Сначала остановите демонстрацию «Что я умею».|Stop the What I can do demo first.|Arrêtez d’abord la démonstration Ce que je sais faire.|Detenga primero la demo Lo que puedo hacer.|请先停止“我的功能”演示。
Останавливаем демонстрацию; подождите…|Stopping demo; please wait…|Arrêt de la démo ; veuillez patienter…|Deteniendo demo; espere…|正在停止演示，请稍候…
Ошибка демонстрации: {0}|Demo error: {0}|Erreur de démonstration : {0}|Error de demostración: {0}|演示错误：{0}
Граф сохранён: {0}|Graph saved: {0}|Graphe enregistré : {0}|Grafo guardado: {0}|图表已保存：{0}
Демонстрация ещё не открывалась|Demo has not been opened yet|Démo pas encore ouverte|La demo aún no se ha abierto|演示尚未打开
Первый показ: {0}|First shown: {0}|Première ouverture : {0}|Primera apertura: {0}|首次显示：{0}
Первый запуск демонстрации: {0}|First demo run: {0}|Première exécution de la démo : {0}|Primera ejecución de la demo: {0}|首次运行演示：{0}
Демонстрация показана, ещё не запускалась|Demo shown, not run yet|Démo affichée, pas encore lancée|Demo mostrada, aún no ejecutada|已显示演示，但尚未运行
Не удалось сохранить отметку первого запуска демонстрации: {0}|Could not save first demo run: {0}|Impossible d’enregistrer la première exécution : {0}|No se pudo guardar la primera ejecución: {0}|无法保存首次运行记录：{0}
Первый запуск: открыта демонстрация «Что я умею».|First launch: What I can do demo opened.|Premier lancement : démonstration ouverte.|Primer inicio: demo abierta.|首次启动：已打开功能演示。
Не удалось сохранить отметку первого показа: {0}|Could not save first-show state: {0}|Impossible de mémoriser la première ouverture : {0}|No se pudo guardar la primera apertura: {0}|无法保存首次显示状态：{0}
Демонстрационный граф откроет Блокнот и Paint, изменит буфер обмена и создаст файлы демонстрации. Продолжить?|The demo graph will open Notepad and Paint, change the clipboard and create demo files. Continue?|Le graphe ouvrira Bloc-notes et Paint, modifiera le presse-papiers et créera des fichiers. Continuer ?|El grafo abrirá Bloc de notas y Paint, cambiará el portapapeles y creará archivos. ¿Continuar?|演示图表将打开记事本和画图、更改剪贴板并创建演示文件。继续吗？
Модель перевода для этой пары пока не установлена.|No translation model is installed for this pair.|Aucun modèle de traduction n’est installé pour cette paire.|No hay modelo de traducción para este par.|此语言对尚未安装翻译模型。
Модель перевода не найдена; этот шаг честно пропущен.|Translation model missing; this step was skipped.|Modèle de traduction absent ; cette étape est ignorée.|Falta el modelo de traducción; se omitió este paso.|缺少翻译模型；已跳过此步骤。
Отчёт демонстрации: {0}|Demo report: {0}|Rapport de démonstration : {0}|Informe de demo: {0}|演示报告：{0}
Рисунок демонстрации: {0}|Demo drawing: {0}|Dessin de la démo : {0}|Dibujo de demo: {0}|演示绘图：{0}
Снимок демонстрации: {0}|Demo screenshot: {0}|Capture de démonstration : {0}|Captura de demo: {0}|演示截图：{0}
Неизвестный шаг демонстрации: {0}|Unknown demo step: {0}|Étape inconnue : {0}|Paso de demo desconocido: {0}|未知演示步骤：{0}
Здравствуйте, {0}! Это действительно печатает граф Kliker Blocks.|Hello, {0}! The Kliker Blocks graph is really typing this.|Bonjour, {0} ! C’est bien le graphe Kliker Blocks qui écrit cela.|¡Hola, {0}! El grafo Kliker Blocks escribe esto de verdad.|你好，{0}！这段文字确实由 Kliker Blocks 图表输入。
Сейчас я переведу вашу собственную фразу.|Now I will translate your own phrase.|Je vais maintenant traduire votre phrase.|Ahora traduciré su propia frase.|现在我将翻译您写的句子。
Блокнот открыт; ввод выполнен только в окне демонстрации.|Notepad opened; text was entered only in the demo window.|Bloc-notes ouvert ; texte saisi uniquement dans la fenêtre de démo.|Bloc de notas abierto; texto introducido solo en la ventana de demo.|已打开记事本；文字仅输入到演示窗口。
Не удалось разместить Блокнот рядом с вопросом: {0}|Could not position Notepad beside the question: {0}|Impossible de placer le Bloc-notes à côté de la question : {0}|No se pudo situar Bloc de notas junto a la pregunta: {0}|无法将记事本放在问题旁：{0}
Блокнот пропустил часть печати; вставляю точный текст через буфер обмена.|Notepad missed some typed characters; pasting exact text via clipboard.|Le Bloc-notes a perdu des caractères ; insertion du texte exact par le presse-papiers.|Bloc de notas omitió caracteres; se pega el texto exacto mediante el portapapeles.|记事本漏掉了部分输入；正在通过剪贴板粘贴准确文本。
Блокнот не подтвердил точность вводного текста.|Notepad could not verify the introduction text.|Le Bloc-notes n’a pas confirmé le texte d’introduction.|Bloc de notas no pudo verificar el texto de introducción.|记事本无法确认介绍文字是否准确。
Блокнот пропустил часть фразы; исправляю весь документ.|Notepad missed part of the phrase; correcting the whole document.|Le Bloc-notes a perdu une partie de la phrase ; correction de tout le document.|Bloc de notas omitió parte de la frase; se corrige todo el documento.|记事本漏掉了部分句子；正在修正整个文档。
Привет, {0}! Посмотрите, как мой граф управляет Блокнотом, переводом и рисованием.|Hello, {0}! Watch my graph work with Notepad, translation and drawing.|Bonjour, {0} ! Regardez mon graphe agir dans le Bloc-notes, traduire et dessiner.|¡Hola, {0}! Vea cómo mi grafo usa Bloc de notas, traducción y dibujo.|你好，{0}！看看我的图表如何操作记事本、翻译和绘画。
Подходящий голос Windows не найден; приветствие осталось текстовым.|No matching Windows voice; the welcome remains text-only.|Aucune voix Windows adaptée ; l’accueil reste textuel.|No hay voz de Windows adecuada; el saludo será solo texto.|未找到匹配的 Windows 语音；欢迎语仅以文字显示。
Озвучивание пропущено: {0}|Speech skipped: {0}|Lecture vocale ignorée : {0}|Voz omitida: {0}|跳过语音：{0}
Блокнот не подтвердил точность текста; демонстрация остановлена.|Notepad could not verify the text; demo stopped.|Le Bloc-notes n’a pas confirmé le texte ; démo arrêtée.|Bloc de notas no pudo verificar el texto; la demo se detuvo.|记事本无法确认文本准确性；演示已停止。
Весь текст Блокнота проверен через буфер обмена; фраза готова к переводу.|The entire Notepad text was verified via clipboard; the phrase is ready for translation.|Tout le texte du Bloc-notes a été vérifié via le presse-papiers ; la phrase est prête à traduire.|Se verificó todo el texto de Bloc de notas mediante el portapapeles; la frase está lista para traducir.|已通过剪贴板验证记事本全文；句子已准备好翻译。
Перевод не выполнен: нет модели ru → {0}. Установите её через меню «Языки».|Translation skipped: no ru → {0} model. Install it from the Languages menu.|Traduction ignorée : modèle ru → {0} absent. Installez-le via le menu Langues.|Traducción omitida: falta el modelo ru → {0}. Instálelo desde el menú Idiomas.|未翻译：缺少 ru → {0} 模型。请在“语言”菜单中安装。
Локальный перевод недоступен: {0}|Local translation unavailable: {0}|Traduction locale indisponible : {0}|Traducción local no disponible: {0}|本地翻译不可用：{0}
Результат показан в Блокноте: {0}|Result shown in Notepad: {0}|Résultat affiché dans le Bloc-notes : {0}|Resultado mostrado en Bloc de notas: {0}|已在记事本中显示结果：{0}
Голоса для языка перевода нет; текст остаётся на экране.|No voice for target language; text remains on screen.|Pas de voix pour la langue cible ; le texte reste affiché.|No hay voz para el idioma de destino; el texto queda en pantalla.|目标语言没有语音；文字仍显示在屏幕上。
Рисунок пропущен; выбрана звезда по умолчанию.|Drawing skipped; star chosen by default.|Dessin ignoré ; étoile choisie par défaut.|Dibujo omitido; se eligió estrella por defecto.|已跳过绘画选择；默认选择星形。
Получен ответ: {0}|Answer received: {0}|Réponse reçue : {0}|Respuesta recibida: {0}|已收到回答：{0}
Стрелка открыта в Paint и указывает на вопрос.|Arrow opened in Paint and points toward the question.|La flèche ouverte dans Paint pointe vers la question.|La flecha abierta en Paint señala la pregunta.|已在画图中打开指向问题的箭头。
Paint нарисовал стрелку мышью в сторону вопроса.|Paint drew an arrow toward the question with the mouse.|Paint a dessiné à la souris une flèche vers la question.|Paint dibujó con el ratón una flecha hacia la pregunta.|画图已用鼠标绘制指向问题的箭头。
Напоминание Paint недоступно: {0}|Paint reminder unavailable: {0}|Rappel Paint indisponible : {0}|Recordatorio de Paint no disponible: {0}|画图提醒不可用：{0}
Я жду вашего ответа. Выберите звезду, сердце или дом в окне Kliker Blocks.|I’m waiting for your answer. Choose a star, heart or house in Kliker Blocks.|J’attends votre réponse. Choisissez étoile, cœur ou maison dans Kliker Blocks.|Espero su respuesta. Elija estrella, corazón o casa en Kliker Blocks.|我在等您的回答。请在 Kliker Blocks 中选择星形、爱心或房子。
Открылось второе окно Блокнота с напоминанием.|A second Notepad window opened with a reminder.|Une seconde fenêtre Bloc-notes s’est ouverte avec un rappel.|Se abrió otro Bloc de notas con un recordatorio.|已打开第二个记事本窗口显示提醒。
Напоминание Блокнота недоступно: {0}|Notepad reminder unavailable: {0}|Rappel Bloc-notes indisponible : {0}|Recordatorio de Bloc de notas no disponible: {0}|记事本提醒不可用：{0}
Фигура открыта в Paint: {0}|Shape opened in Paint: {0}|Forme ouverte dans Paint : {0}|Figura abierta en Paint: {0}|已在画图中打开形状：{0}
Фигура нарисована мышью в Paint: {0}|Shape drawn with the mouse in Paint: {0}|Forme dessinée à la souris dans Paint : {0}|Figura dibujada con el ratón en Paint: {0}|已在画图中用鼠标绘制形状：{0}
Paint: инструмент «Карандаш» не найден.|Paint: Pencil tool not found.|Paint : outil Crayon introuvable.|Paint: no se encontró la herramienta Lápiz.|画图：未找到铅笔工具。
Paint: красный цвет не найден в палитре.|Paint: red not found in the palette.|Paint : couleur rouge introuvable dans la palette.|Paint: no se encontró el rojo en la paleta.|画图：调色板中未找到红色。
Paint: инструмент недоступен на экране.|Paint: tool is not visible on screen.|Paint : l’outil n’est pas visible à l’écran.|Paint: la herramienta no está visible en pantalla.|画图：工具在屏幕上不可见。
Paint: пустой белый холст не найден.|Paint: blank white canvas not found.|Paint : toile blanche vide introuvable.|Paint: no se encontró un lienzo blanco vacío.|画图：未找到空白画布。
Paint: видимая часть холста слишком мала для рисунка.|Paint: visible canvas is too small for the drawing.|Paint : la zone visible est trop petite pour dessiner.|Paint: el lienzo visible es demasiado pequeño para dibujar.|画图：可见画布太小，无法绘图。
Paint: окно слишком мало для демонстрации рисования.|Paint: window is too small to demonstrate drawing.|Paint : fenêtre trop petite pour montrer le dessin.|Paint: la ventana es demasiado pequeña para mostrar el dibujo.|画图：窗口太小，无法演示绘图。
Paint не показал штрихи мыши на холсте; рисунок не подтверждён.|Paint did not show the mouse strokes on the canvas; drawing was not verified.|Paint n’a pas affiché les traits de souris sur la toile ; dessin non vérifié.|Paint no mostró los trazos del ratón en el lienzo; el dibujo no se verificó.|画图未在画布上显示鼠标笔迹；绘图未通过验证。
Рисование в Paint недоступно: {0}|Paint drawing unavailable: {0}|Dessin Paint indisponible : {0}|Dibujo de Paint no disponible: {0}|画图绘制不可用：{0}
Окно {0} не появилось за 4,5 секунды.|Window {0} did not appear within 4.5 seconds.|La fenêtre {0} n’est pas apparue en 4,5 secondes.|La ventana {0} no apareció en 4,5 segundos.|窗口 {0} 在 4.5 秒内未出现。
Окно демонстрации не найдено.|Demo window not found.|Fenêtre de démo introuvable.|No se encontró la ventana de demo.|未找到演示窗口。
Окно демонстрации потеряло фокус; ввод отменён.|Demo window lost focus; input canceled.|La fenêtre de démo a perdu le focus ; saisie annulée.|La ventana de demo perdió el foco; entrada cancelada.|演示窗口失去焦点；已取消输入。
Kliker Blocks — вопрос к вам|Kliker Blocks — a question for you|Kliker Blocks — une question|Kliker Blocks — una pregunta|Kliker Blocks——给您的问题
Что нарисовать в Paint? Напишите «звезда», «сердце» или «дом».|What should Paint draw? Enter star, heart or house.|Que dessiner dans Paint ? Saisissez étoile, cœur ou maison.|¿Qué dibujar en Paint? Escriba estrella, corazón o casa.|想在画图中画什么？请输入星形、爱心或房子。
Звезда|Star|Étoile|Estrella|星形
Сердце|Heart|Cœur|Corazón|爱心
Дом|House|Maison|Casa|房子
Ответить|Answer|Répondre|Responder|回答
Пропустить|Skip|Ignorer|Omitir|跳过
Kliker Blocks — ввод|Kliker Blocks — input|Kliker Blocks — saisie|Kliker Blocks — entrada|Kliker Blocks——输入
Kliker Blocks — выбор|Kliker Blocks — choice|Kliker Blocks — choix|Kliker Blocks — selección|Kliker Blocks——选择
Управляет встроенной демонстрацией «Что я умею»|Runs the built-in What I can do demo|Exécute la démonstration intégrée|Ejecuta la demostración integrada|运行内置功能演示
Действие демонстрации|Demo action|Action de démonstration|Acción de demostración|演示操作
Подпись на графе|Graph caption|Titre sur le graphe|Título en el grafo|图表标题
Демонстрация Kliker Blocks началась.|Kliker Blocks demo started.|La démonstration Kliker Blocks a commencé.|Comenzó la demostración de Kliker Blocks.|Kliker Blocks 演示已开始。
Как к вам обращаться?|What should I call you?|Comment puis-je vous appeler ?|¿Cómo debo llamarle?|我该如何称呼您？
друг|friend|ami|amigo|朋友
Открыть Блокнот|Open Notepad|Ouvrir le Bloc-notes|Abrir Bloc de notas|打开记事本
Напишите короткую фразу на русском для перевода.|Enter a short Russian phrase to translate.|Écrivez une courte phrase en russe à traduire.|Escriba una frase corta en ruso para traducir.|输入要翻译的简短俄语句子。
Возможности начинаются с одного клика.|Possibilities start with one click.|Les possibilités commencent par un clic.|Las posibilidades empiezan con un clic.|无限可能始于一次点击。
Напечатать и проверить текст|Type and verify text|Saisir et vérifier le texte|Escribir y verificar texto|输入并验证文本
На какой язык перевести фразу?|Translate the phrase into which language?|Dans quelle langue traduire la phrase ?|¿A qué idioma traducir la frase?|要把句子翻译成哪种语言？
Английский\nФранцузский\nИспанский\nКитайский|English\nFrench\nSpanish\nChinese|Anglais\nFrançais\nEspagnol\nChinois|Inglés\nFrancés\nEspañol\nChino|英语\n法语\n西班牙语\n中文
Проверить модель перевода|Check translation model|Vérifier le modèle de traduction|Comprobar modelo de traducción|检查翻译模型
Нет модели — объяснить|No model — explain|Pas de modèle — expliquer|Sin modelo — explicar|缺少模型——说明
Показать перевод|Show translation|Afficher la traduction|Mostrar traducción|显示译文
Озвучить результат|Speak the result|Lire le résultat|Leer el resultado|朗读结果
Спросить о рисунке|Ask about the drawing|Demander le dessin|Preguntar por el dibujo|询问绘画选择
Ждать бездействия 8 секунд|Wait for 8 idle seconds|Attendre 8 secondes d’inactivité|Esperar 8 segundos de inactividad|等待 8 秒无操作
Paint: стрелка к вопросу|Paint: arrow toward question|Paint : flèche vers la question|Paint: flecha hacia la pregunta|画图：指向问题的箭头
Ждать ещё 10 секунд|Wait another 10 seconds|Attendre encore 10 secondes|Esperar otros 10 segundos|再等待 10 秒
Блокнот: напоминание|Notepad: reminder|Bloc-notes : rappel|Bloc de notas: recordatorio|记事本：提醒
Нарисовать выбранную фигуру|Draw the selected shape|Dessiner la forme choisie|Dibujar la figura elegida|绘制所选图形
Показать результат и граф|Show result and graph|Afficher le résultat et le graphe|Mostrar resultado y grafo|显示结果与图表
Возможности компьютера и голоса…|Computer and voices…|Ordinateur et voix…|Equipo y voces…|计算机与语音…
Возможности компьютера и голоса|Computer capabilities and voices|Capacités de l’ordinateur et voix|Capacidades del equipo y voces|计算机功能与语音
Звук|Sound|Son|Sonido|声音
Закрыть|Close|Fermer|Cerrar|关闭
Обновить данные|Refresh data|Actualiser les données|Actualizar datos|刷新数据
Собрано (UTC): {0}|Collected (UTC): {0}|Collecté (UTC) : {0}|Recopilado (UTC): {0}|采集时间（UTC）：{0}
Архитектура ОС / процесса: {0} / {1}|OS / process architecture: {0} / {1}|Architecture système / processus : {0} / {1}|Arquitectura del SO / proceso: {0} / {1}|系统／进程架构：{0}／{1}
Платформа .NET: {0}|.NET platform: {0}|Plateforme .NET : {0}|Plataforma .NET: {0}|.NET 平台：{0}
Язык интерфейса Windows: {0}|Windows UI language: {0}|Langue de l’interface Windows : {0}|Idioma de interfaz de Windows: {0}|Windows 界面语言：{0}
Клавиатуры: {0}|Keyboard layouts: {0}|Claviers : {0}|Teclados: {0}|键盘布局：{0}
Голоса Windows:|Windows voices:|Voix Windows :|Voces de Windows:|Windows 语音：
Голоса не найдены.|No voices found.|Aucune voix trouvée.|No se encontraron voces.|未找到语音。
Экраны:|Displays:|Écrans :|Pantallas:|显示器：
основной|primary|principal|principal|主屏幕
Локальный файл: {0}|Local file: {0}|Fichier local : {0}|Archivo local: {0}|本地文件：{0}
Голос Windows (пусто = авто)|Windows voice (blank = automatic)|Voix Windows (vide = automatique)|Voz de Windows (vacío = automática)|Windows 语音（留空自动选择）
Выберите установленный голос Windows; список зависит от Windows.|Choose an installed Windows voice; the list depends on Windows.|Choisissez une voix Windows installée ; la liste dépend de Windows.|Elija una voz de Windows instalada; la lista depende de Windows.|选择已安装的 Windows 语音；列表取决于系统。
Автоматически по языку текста|Automatically by text language|Automatiquement selon la langue du texte|Automáticamente según el idioma del texto|按文本语言自动选择
В Windows не найден голос для языка {0}. Добавьте его в языковых настройках Windows.|No Windows voice found for {0}. Add one in Windows language settings.|Aucune voix Windows pour {0}. Ajoutez-en une dans les paramètres de langue Windows.|No se encontró una voz de Windows para {0}. Agréguela en la configuración de idioma.|未找到用于 {0} 的 Windows 语音。请在 Windows 语言设置中添加。
Голос «{0}» не найден. Выберите его из списка доступных голосов Windows.|Voice “{0}” was not found. Choose one from the Windows voice list.|La voix « {0} » est introuvable. Choisissez-en une dans la liste Windows.|No se encontró la voz «{0}». Elíjala en la lista de voces de Windows.|未找到语音“{0}”。请从 Windows 语音列表中选择。
Голос «{0}» ({1}) не подходит для текста на языке {2}. Выберите другой голос или оставьте поле пустым.|Voice “{0}” ({1}) cannot read {2} text. Choose another voice or leave the field blank.|La voix « {0} » ({1}) ne convient pas au texte {2}. Choisissez une autre voix ou laissez le champ vide.|La voz «{0}» ({1}) no sirve para texto {2}. Elija otra voz o deje el campo vacío.|语音“{0}”（{1}）不适合朗读 {2} 文本。请选择其他语音或留空。
Голос Windows не создал звуковые данные для этого текста. Выберите голос нужного языка.|Windows produced no audio for this text. Choose a voice for its language.|Windows n’a produit aucun son pour ce texte. Choisissez une voix adaptée à sa langue.|Windows no produjo audio para este texto. Elija una voz del idioma adecuado.|Windows 没有为此文本生成音频。请选择对应语言的语音。
Не удалось озвучить текст через голос Windows: {0}|Windows could not speak the text: {0}|Windows n’a pas pu lire le texte : {0}|Windows no pudo leer el texto: {0}|Windows 无法朗读文本：{0}
Файл журнала: {0}|Journal file: {0}|Fichier journal : {0}|Archivo de registro: {0}|日志文件：{0}
Файл сообщений сценария: {0}|Scenario message file: {0}|Fichier des messages du scénario : {0}|Archivo de mensajes del escenario: {0}|场景消息文件：{0}
Файл сообщений сценария отключён.|Scenario message file disabled.|Fichier des messages du scénario désactivé.|Archivo de mensajes del escenario desactivado.|场景消息文件已关闭。
Запись журнала включена: {0}. Файл создастся при запуске сценария.|Journal enabled: {0}. The file will be created when the scenario starts.|Journal activé : {0}. Le fichier sera créé au démarrage du scénario.|Registro activado: {0}. El archivo se creará al iniciar el escenario.|日志已启用：{0}。运行场景时将创建文件。
Запись журнала в файл выключена. Сохраните проект.|Journal file recording disabled. Save the project.|Enregistrement du journal désactivé. Enregistrez le projet.|Registro en archivo desactivado. Guarde el proyecto.|文件日志记录已关闭。请保存项目。
Текст для озвучивания длиннее 16000 символов.|Speech text exceeds 16000 characters.|Le texte à lire dépasse 16000 caractères.|El texto para voz supera 16000 caracteres.|朗读文本超过 16000 个字符。
Скорость речи должна быть от -10 до 10.|Speech rate must be between -10 and 10.|La vitesse doit être comprise entre -10 et 10.|La velocidad debe estar entre -10 y 10.|语速必须在 -10 到 10 之间。
Громкость речи должна быть от 0 до 100.|Speech volume must be between 0 and 100.|Le volume doit être compris entre 0 et 100.|El volumen debe estar entre 0 y 100.|音量必须在 0 到 100 之间。
Блокнот|Notepad|Bloc-notes|Bloc de notas|记事本
Введите значение|Enter a value|Saisissez une valeur|Introduzca un valor|输入一个值
Выберите вариант|Choose an option|Choisissez une option|Elija una opción|选择一个选项
Моя подпрограмма|My subroutine|Mon sous-programme|Mi subrutina|我的子程序
Первый\nВторой\nТретий|First\nSecond\nThird|Premier\nDeuxième\nTroisième|Primero\nSegundo\nTercero|第一\n第二\n第三
Привет!|Hello!|Bonjour !|¡Hola!|你好！
Пример|Example|Exemple|Ejemplo|示例
Продолжить?|Continue?|Continuer ?|¿Continuar?|继续？
Сценарий выполнил шаг|Scenario completed a step|Le scénario a terminé une étape|El escenario completó un paso|脚本完成了一个步骤
Шаг выполнен|Step completed|Étape terminée|Paso completado|步骤完成
один,два,три|one,two,three|un,deux,trois|uno,dos,tres|一,二,三
текст|text|texte|texto|文本
Argos локально|Local Argos|Argos local|Argos local|本地 Argos
AutoHotkey64.exe (пусто = поиск)|AutoHotkey64.exe (blank = search)|AutoHotkey64.exe (vide = rechercher)|AutoHotkey64.exe (vacío = buscar)|AutoHotkey64.exe（留空则搜索）
EXE, BAT или документ|EXE, BAT or document|EXE, BAT ou document|EXE, BAT o documento|EXE、BAT 或文档
HWND (число или переменная)|HWND (number or variable)|HWND (nombre ou variable)|HWND (número o variable)|HWND（数字或变量）
RADIOBOX: номер варианта или 0|RADIOBOX: choice number or 0|RADIOBOX : numéro du choix ou 0|RADIOBOX: número de opción o 0|RADIOBOX：选项编号或 0
SENDMESSAGE или POSTMESSAGE|SENDMESSAGE or POSTMESSAGE|SENDMESSAGE ou POSTMESSAGE|SENDMESSAGE o POSTMESSAGE|SENDMESSAGE 或 POSTMESSAGE
SETVAR: имя переменной берётся из строки|SETVAR: variable name comes from text|SETVAR : nom de variable fourni par texte|SETVAR: nombre de variable desde texto|SETVAR：变量名来自文本
STRSEPARATE: части строки в массив|STRSEPARATE: split string into array|STRSEPARATE : découper en tableau|STRSEPARATE: dividir cadena en matriz|STRSEPARATE：将字符串拆入数组
TFREADARR/TFWRITEARR: по одной строке на элемент|TFREADARR/TFWRITEARR: one line per item|TFREADARR/TFWRITEARR : une ligne par élément|TFREADARR/TFWRITEARR: una línea por elemento|TFREADARR/TFWRITEARR：每个元素一行
UNDEFINE: удалить переменную и одноимённый массив|UNDEFINE: remove variable and same-named array|UNDEFINE : supprimer variable et tableau homonyme|UNDEFINE: eliminar variable y matriz homónima|UNDEFINE：删除变量及同名数组
Unicode-текст в активное окно|Unicode text to active window|Texte Unicode vers la fenêtre active|Texto Unicode a la ventana activa|向活动窗口输入 Unicode 文本
WM_SETTEXT с таймаутом 500 мс|WM_SETTEXT with 500 ms timeout|WM_SETTEXT avec délai de 500 ms|WM_SETTEXT con espera máxima de 500 ms|WM_SETTEXT，超时 500 毫秒
X на экране|Screen X|X à l’écran|X en pantalla|屏幕 X
XOR или CRC32 области снимка|XOR or CRC32 of captured area|XOR ou CRC32 de la zone capturée|XOR o CRC32 del área capturada|截图区域的 XOR 或 CRC32
Y на экране|Screen Y|Y à l’écran|Y en pantalla|屏幕 Y
lParam (целое число)|lParam (integer)|lParam (entier)|lParam (entero)|lParam（整数）
wParam (целое число)|wParam (integer)|wParam (entier)|wParam (entero)|wParam（整数）
Активировать|Activate|Activer|Activar|激活
Алгоритм|Algorithm|Algorithme|Algoritmo|算法
Аргументы|Arguments|Arguments|Argumentos|参数
Аргументы скрипта|Script arguments|Arguments du script|Argumentos del script|脚本参数
Арифметика с числом|Numeric arithmetic|Calcul numérique|Aritmética numérica|数值运算
Асинхронно|Asynchronous|Asynchrone|Asíncrono|异步
Больше|Greater than|Supérieur à|Mayor que|大于
В текущей точке|At current position|À la position actuelle|En la posición actual|在当前位置
Варианты: каждый с новой строки|Choices: one per line|Choix : un par ligne|Opciones: una por línea|选项：每行一个
Вверх|Up|Haut|Arriba|向上
Верхний Y|Top Y|Y supérieur|Y superior|顶部 Y
Верхняя граница|Top bound|Limite supérieure|Límite superior|上边界
Весь рабочий стол|Entire desktop|Bureau entier|Escritorio completo|整个桌面
Ветка A|Branch A|Branche A|Rama A|分支 A
Ветка B|Branch B|Branche B|Rama B|分支 B
Ветка тела завершается блоком «Конец ветки»|End Body with Branch end|Terminer Corps par Fin de branche|Terminar Cuerpo con Fin de rama|用“结束分支”结束循环体
Вниз|Down|Bas|Abajo|向下
Вопрос|Question|Question|Pregunta|问题
Время плавного сдвига, мс|Smooth move duration, ms|Durée du déplacement progressif, ms|Duración del movimiento gradual, ms|平滑移动时长（毫秒）
Всплывающее сообщение в области уведомлений|Notification in system tray|Notification dans la zone système|Aviso en el área de notificación|系统托盘通知
Вставить строку|Insert line|Insérer une ligne|Insertar línea|插入行
Вызов по имени, затем возврат|Call by name, then return|Appel par nom, puis retour|Llamar por nombre y volver|按名称调用后返回
Выражение (начинается с =)|Expression (starts with =)|Expression (commence par =)|Expresión (empieza con =)|表达式（以 = 开头）
Вычесть|Subtract|Soustraire|Restar|减去
Геометрия|Geometry|Géométrie|Geometría|几何参数
Да: включено, Нет: выключено|Yes: on, No: off|Oui : actif, Non : inactif|Sí: activado, No: desactivado|是：开启，否：关闭
Да: выключено, Нет: включено|Yes: off, No: on|Oui : inactif, Non : actif|Sí: desactivado, No: activado|是：关闭，否：开启
Действие|Action|Action|Acción|操作
Длительность, мс (1–5000)|Duration, ms (1–5000)|Durée, ms (1–5000)|Duración, ms (1–5000)|时长（毫秒，1–5000）
Для полной записи макроса|For full macro recording|Pour l’enregistrement complet|Para grabación completa de macro|用于完整宏录制
До совпадения или таймаута|Until match or timeout|Jusqu’à correspondance ou délai|Hasta coincidencia o espera agotada|直到匹配或超时
Добавить|Append|Ajouter|Añadir|添加
Добавить или перезаписать строкой|Append or overwrite with line|Ajouter ou remplacer par une ligne|Añadir o sobrescribir con una línea|追加或以一行覆盖
Добавить строку|Append line|Ajouter une ligne|Añadir línea|追加行
Добавить, извлечь, размер или сортировка|Append, pop, size or sort|Ajouter, extraire, taille ou trier|Añadir, extraer, tamaño u ordenar|添加、弹出、获取大小或排序
Допуск 0–255|Tolerance 0–255|Tolérance 0–255|Tolerancia 0–255|容差 0–255
Дублировать сообщения в UTF-8 файл|Duplicate messages to UTF-8 file|Dupliquer les messages dans un fichier UTF-8|Duplicar mensajes en archivo UTF-8|将消息写入 UTF-8 文件
Ждать завершения|Wait for completion|Attendre la fin|Esperar a que termine|等待完成
Завершение сценария|Stop scenario|Arrêter le scénario|Detener escenario|停止场景
Завершение цикла или параллельной ветки|End loop or parallel branch|Fin de boucle ou branche parallèle|Fin de bucle o rama paralela|结束循环或并行分支
Заголовки: Имя: значение, каждый с новой строки (необязательно)|Headers: Name: value, one per line (optional)|En-têtes : Nom: valeur, un par ligne (facultatif)|Cabeceras: Nombre: valor, una por línea (opcional)|请求头：名称: 值，每行一个（可选）
Заголовок|Title|Titre|Título|标题
Заголовок или часть|Title or substring|Titre ou fragment|Título o fragmento|标题或片段
Задать значение по индексу|Set value by index|Définir la valeur par indice|Asignar valor por índice|按索引设置值
Задать начальное значение только если переменная отсутствует|Set initial value only if missing|Définir la valeur initiale si absente|Asignar valor inicial solo si falta|仅在变量不存在时赋初值
Задать область|Set region|Définir la zone|Definir región|设置区域
Задержка до сдвига, мс|Delay before move, ms|Délai avant déplacement, ms|Retraso antes del movimiento, ms|移动前延迟（毫秒）
Записать|Write|Écrire|Escribir|写入
Записать массив|Write array|Écrire le tableau|Escribir matriz|写入数组
Записать строку|Write line|Écrire une ligne|Escribir línea|写入行
Записать текст в буфер|Copy text to clipboard|Copier le texte dans le presse-papiers|Copiar texto al portapapeles|将文本复制到剪贴板
Записать текущий буфер в PNG|Save current buffer as PNG|Enregistrer le tampon en PNG|Guardar búfer actual como PNG|将当前缓冲区保存为 PNG
Записываемый текст|Text to write|Texte à écrire|Texto a escribir|要写入的文本
Запись в журнал|Write to journal|Écrire dans le journal|Escribir en el registro|写入日志
Запустить .ahk через v2|Run .ahk with v2|Exécuter .ahk avec v2|Ejecutar .ahk con v2|用 v2 运行 .ahk
Значение (выражение начинается с =)|Value (expression starts with =)|Valeur (expression commençant par =)|Valor (expresión empieza con =)|值（表达式以 = 开头）
Значение для добавления|Value to append|Valeur à ajouter|Valor a añadir|要添加的值
Значение по умолчанию|Default value|Valeur par défaut|Valor predeterminado|默认值
Игнорировать цвет (-1 = нет)|Ignore color (-1 = none)|Ignorer la couleur (-1 = aucune)|Ignorar color (-1 = ninguno)|忽略颜色（-1 = 无）
Извлечь|Pop|Extraire|Extraer|弹出
Имена файлов или папок в массив|File or folder names into array|Noms de fichiers/dossiers dans un tableau|Nombres de archivos/carpetas en matriz|将文件或文件夹名存入数组
Имя (буквы, цифры, _)|Name (letters, digits, _)|Nom (lettres, chiffres, _)|Nombre (letras, cifras, _)|名称（字母、数字、_）
Имя и текстовое значение|Name and text value|Nom et valeur texte|Nombre y valor de texto|名称和文本值
Имя или $имя (можно ${переменная} / =выражение)|Name or $name (${variable} / =expression allowed)|Nom ou $nom (${variable} / =expression admis)|Nombre o $nombre (se admite ${variable} / =expresión)|名称或 $name（可用 ${variable} / =表达式）
Имя массива|Array name|Nom du tableau|Nombre de matriz|数组名
Имя массива координат|Coordinate array name|Nom du tableau de coordonnées|Nombre de matriz de coordenadas|坐标数组名
Имя переменной|Variable name|Nom de variable|Nombre de variable|变量名
Имя переменной или массива|Variable or array name|Nom de variable ou tableau|Nombre de variable o matriz|变量或数组名
Имя подпрограммы|Subroutine name|Nom du sous-programme|Nombre de subrutina|子程序名
Имя файла в папке проекта; пусто — выключить|File name in project folder; blank disables|Nom du fichier dans le projet ; vide = désactiver|Nombre de archivo en carpeta del proyecto; vacío = desactivar|项目文件夹中的文件名；留空则关闭
Иначе|Otherwise|Sinon|Si no|否则
Индекс (0 и выше)|Index (0 or more)|Indice (0 ou plus)|Índice (0 o más)|索引（0 或更大）
Индикатор (только отображение)|Indicator (display only)|Indicateur (affichage seulement)|Indicador (solo visualización)|指示器（仅显示）
Интервал, мс|Interval, ms|Intervalle, ms|Intervalo, ms|间隔（毫秒）
Исходная строка|Source string|Chaîne source|Cadena de origen|源字符串
Исходный массив|Source array|Tableau source|Matriz de origen|源数组
Как END_SUB|Like END_SUB|Comme END_SUB|Como END_SUB|类似 END_SUB
Каталог|Directory|Dossier|Carpeta|目录
Ключ|Key|Clé|Clave|键
Код Windows VK (1–255)|Windows VK code (1–255)|Code VK Windows (1–255)|Código VK de Windows (1–255)|Windows VK 键码（1–255）
Код иконки (0–4)|Icon code (0–4)|Code de l’icône (0–4)|Código de icono (0–4)|图标代码（0–4）
Код кнопок (0–5)|Button set code (0–5)|Code du groupe de boutons (0–5)|Código de grupo de botones (0–5)|按钮组代码（0–5）
Кнопки в окне|Dialog buttons|Boutons de la boîte de dialogue|Botones del cuadro de diálogo|对话框按钮
Иконка окна|Dialog icon|Icône de la boîte de dialogue|Icono del cuadro de diálogo|对话框图标
Без иконки|No icon|Sans icône|Sin icono|无图标
Прервать|Abort|Abandonner|Anular|中止
Игнорировать|Ignore|Ignorer|Ignorar|忽略
Предупреждение|Warning|Avertissement|Advertencia|警告
Информация|Information|Information|Información|信息
Запустить сценарий|Run scenario|Exécuter un scénario|Ejecutar escenario|运行场景
Выполнить сохранённый .klb последовательно|Run a saved .klb sequentially|Exécuter un .klb enregistré en séquence|Ejecutar un .klb guardado secuencialmente|依次运行已保存的 .klb
Файл сценария .klb|Scenario .klb file|Fichier de scénario .klb|Archivo de escenario .klb|场景 .klb 文件
Повторений (1–1000)|Repetitions (1–1000)|Répétitions (1–1000)|Repeticiones (1–1000)|重复次数（1–1000）
Скорость сценария (0,1–5,0×)|Scenario speed (0.1–5.0×)|Vitesse du scénario (0,1–5,0×)|Velocidad del escenario (0,1–5,0×)|场景速度（0.1–5.0×）
Скорость выполнения (0,1–5,0×)|Playback speed (0.1–5.0×)|Vitesse d’exécution (0,1–5,0×)|Velocidad de ejecución (0,1–5,0×)|运行速度（0.1–5.0×）
Выбрать сценарий…|Choose scenario…|Choisir un scénario…|Elegir escenario…|选择场景…
Windows отклонила Unicode-ввод (SendInput={0}/2, Win32={1}).|Windows rejected Unicode input (SendInput={0}/2, Win32={1}).|Windows a refusé la saisie Unicode (SendInput={0}/2, Win32={1}).|Windows rechazó la entrada Unicode (SendInput={0}/2, Win32={1}).|Windows 拒绝了 Unicode 输入（SendInput={0}/2，Win32={1}）。
Unicode-ввод отклонён Windows; остаток текста вставляется через буфер обмена. Причина: {0}|Windows rejected Unicode typing; the remaining text is pasted from the clipboard. Cause: {0}|Windows a refusé la saisie Unicode ; le texte restant est collé depuis le presse-papiers. Cause : {0}|Windows rechazó la escritura Unicode; el texto restante se pega desde el portapapeles. Causa: {0}|Windows 拒绝了 Unicode 输入；剩余文本将通过剪贴板粘贴。原因：{0}
Режим выполнения сценария|Scenario playback mode|Mode d’exécution du scénario|Modo de ejecución del escenario|场景运行模式
Один проход|Single pass|Un passage|Una pasada|运行一次
Повторять до остановки|Repeat until stopped|Répéter jusqu’à l’arrêt|Repetir hasta detener|重复直到停止
Как продолжать после конца цепочки блоков|What to do at the end of the block chain|Que faire à la fin de la chaîne de blocs|Qué hacer al terminar la cadena de bloques|区块链结束后的处理方式
Пауза между проходами и при возврате по обратной связи, мс|Delay between passes and feedback loops, ms|Délai entre les passages et les boucles de retour, ms|Pausa entre pasadas y bucles de retorno, ms|运行间及返回循环延迟（毫秒）
«Повторять до остановки» снова начинает со «Старт», если путь закончился. Можно также соединить последний блок с предыдущим: явная обратная связь автоматически повторяет сценарий даже в режиме одного прохода. Блок «Стоп» и меню остановки завершают его.|Repeat until stopped starts again from Start when the path ends. You can also connect the last block to an earlier one to create a loop. Stop or the Stop menu command ends execution.|Répéter jusqu’à l’arrêt repart de Début quand le chemin se termine. Reliez aussi le dernier bloc à un bloc précédent pour créer une boucle. Le bloc Arrêt ou la commande Arrêter termine l’exécution.|Repetir hasta detener vuelve al Inicio cuando termina el camino. También puede conectar el último bloque con uno anterior para crear un bucle. El bloque Detener o el menú Detener termina la ejecución.|重复直到停止会在路径结束后从开始重新运行。也可将最后一个区块连接到前面的区块形成循环。停止区块或停止菜单命令可结束运行。
Код нажатой кнопки в переменную|Store selected button code|Enregistrer le code du bouton choisi|Guardar código del botón elegido|将所选按钮代码存入变量
Код языка и уверенность через LibreTranslate|Language code and confidence via LibreTranslate|Code langue et confiance via LibreTranslate|Código de idioma y confianza vía LibreTranslate|通过 LibreTranslate 获取语言代码和置信度
Количество|Count|Nombre|Cantidad|数量
Количество строк|Line count|Nombre de lignes|Número de líneas|行数
Координаты в массив X,Y,X,Y…|Coordinates into array X,Y,X,Y…|Coordonnées dans tableau X,Y,X,Y…|Coordenadas en matriz X,Y,X,Y…|将坐标存入数组 X,Y,X,Y…
Куда переместить|Move target|Destination|Destino del movimiento|移动目标
Левая|Left|Gauche|Izquierda|左键
Левая граница|Left bound|Limite gauche|Límite izquierdo|左边界
Левый X|Left X|X gauche|X izquierdo|左侧 X
Максимальное время группы, мс|Group maximum duration, ms|Durée maximale du groupe, ms|Duración máxima del grupo, ms|组最大时长（毫秒）
Максимум проходов (0 = без лимита)|Maximum iterations (0 = unlimited)|Itérations maximales (0 = illimité)|Iteraciones máximas (0 = ilimitado)|最大迭代次数（0 = 无限制）
Маска|Wildcard mask|Masque de recherche|Máscara comodín|通配符
Массив перевода|Translation array|Tableau de traduction|Matriz de traducción|翻译数组
Массив результата|Result array|Tableau de résultat|Matriz de resultado|结果数组
Массив частей|Parts array|Tableau des parties|Matriz de partes|分段数组
Меньше|Less than|Inférieur à|Menor que|小于
Меняет запомненный буфер, не настоящий экран|Changes stored buffer, not physical screen|Modifie le tampon, pas l’écran réel|Cambia el búfer, no la pantalla real|修改缓存，不修改真实屏幕
Миллисекунды|Milliseconds|Millisecondes|Milisegundos|毫秒
Нажать|Press|Appuyer|Pulsar|按下
Нажать или отпустить|Press or release|Appuyer ou relâcher|Pulsar o soltar|按下或松开
Найти HWND элемента под курсором/точкой|Find element HWND under pointer/point|Trouver le HWND sous le pointeur/point|Buscar HWND bajo cursor/punto|查找光标或指定点下的元素 HWND
Направление|Direction|Direction|Dirección|方向
Например A, ENTER, F1|For example A, ENTER, F1|Par exemple A, ENTER, F1|Por ejemplo A, ENTER, F1|例如 A、ENTER、F1
Начальное значение|Initial value|Valeur initiale|Valor inicial|初始值
Начать воспроизведение без ожидания|Start playback without waiting|Lancer la lecture sans attendre|Iniciar reproducción sin esperar|开始播放而不等待
Не больше|At most|Au plus|Como máximo|不大于
Не влияет|No effect|Sans effet|Sin efecto|无影响
Не меньше|At least|Au moins|Como mínimo|不小于
Не равно|Not equal|Différent de|No es igual|不等于
Ненулевой результат ведёт по ветке Да|Nonzero result takes Yes|Résultat non nul : branche Oui|Resultado no nulo: rama Sí|非零结果进入“是”分支
Нижняя граница|Bottom bound|Limite inférieure|Límite inferior|下边界
Нижняя граница (не включена)|Bottom bound (exclusive)|Limite inférieure (exclue)|Límite inferior (excluido)|下边界（不包含）
Новое значение|New value|Nouvelle valeur|Valor nuevo|新值
Новый текст|New text|Nouveau texte|Texto nuevo|新文本
Новый цвет|New color|Nouvelle couleur|Color nuevo|新颜色
Номер строки (от 1)|Line number (from 1)|Numéro de ligne (dès 1)|Número de línea (desde 1)|行号（从 1 开始）
Обе ветки завершаются блоком «Конец ветки»|End both branches with Branch end|Terminer les deux branches par Fin de branche|Terminar ambas ramas con Fin de rama|两个分支都以“结束分支”结束
Область|Region|Zone|Región|区域
Озвучить текст|Speak text|Lire le texte à voix haute|Leer texto en voz alta|朗读文本
Произнести текст голосом Windows|Speak with a Windows voice|Prononcer avec une voix Windows|Pronunciar con voz de Windows|使用 Windows 语音朗读
Текст для озвучивания|Text to speak|Texte à prononcer|Texto para leer|要朗读的文本
Скорость речи (-10…10)|Speech rate (-10…10)|Vitesse de parole (-10…10)|Velocidad de voz (-10…10)|语速（-10…10）
Громкость речи (0…100)|Speech volume (0…100)|Volume de la voix (0…100)|Volumen de voz (0…100)|语音音量（0…100）
Доступные голоса Windows…|Available Windows voices…|Voix Windows disponibles…|Voces de Windows disponibles…|可用的 Windows 语音…
Выбор голоса Windows|Choose Windows voice|Choisir une voix Windows|Elegir voz de Windows|选择 Windows 语音
Полный путь к журналу:|Full journal path:|Chemin complet du journal :|Ruta completa del registro:|日志完整路径：
Справка: коды клавиш Windows VK|Help: Windows VK key codes|Aide : codes VK Windows|Ayuda: códigos VK de Windows|帮助：Windows VK 键码
Поиск: код, буква или название клавиши|Search: code, letter or key name|Recherche : code, lettre ou touche|Buscar: código, letra o nombre de tecla|搜索：代码、字母或键名
Код (десятичный)|Code (decimal)|Code (décimal)|Código (decimal)|代码（十进制）
VK-коды 1–255. Для букв и цифр используйте коды из таблицы. Клавиши OEM зависят от раскладки и модели клавиатуры.|VK codes 1–255. Use the table for letters and digits. OEM keys depend on keyboard layout and model.|Codes VK 1–255. Utilisez le tableau pour lettres et chiffres. Les touches OEM dépendent du clavier.|Códigos VK 1–255. Use la tabla para letras y cifras. Las teclas OEM dependen de la distribución y modelo.|VK 键码 1–255。字母和数字请查表。OEM 键取决于键盘布局和型号。
Записывать журнал при запуске сценария|Write journal when scenario runs|Enregistrer le journal pendant le scénario|Guardar registro al ejecutar el escenario|运行场景时写入日志
Имя файла UTF-8 (.txt или .log):|UTF-8 filename (.txt or .log):|Nom du fichier UTF-8 (.txt ou .log) :|Nombre del archivo UTF-8 (.txt o .log):|UTF-8 文件名（.txt 或 .log）：
Файл находится рядом с сохранённым проектом; для несохранённого — рядом с программой. При повторных запусках записи дописываются. Максимальный размер — 8 МиБ.|The file is beside a saved project, or beside the program if unsaved. Later runs append entries. Maximum size: 8 MiB.|Le fichier est près du projet enregistré, ou du programme si le projet est non enregistré. Les exécutions suivantes ajoutent des lignes. Maximum : 8 Mio.|El archivo queda junto al proyecto guardado, o junto al programa si aún no se guardó. Las siguientes ejecuciones añaden entradas. Máximo: 8 MiB.|文件位于已保存项目旁；未保存时位于程序目录。后续运行会追加记录。最大 8 MiB。
Введите имя файла .txt или .log без пути, отличное от имени проекта (до 160 символов).|Enter a .txt or .log filename without a path, different from the project name (up to 160 characters).|Saisissez un nom .txt ou .log sans chemin, distinct du projet (160 caractères maximum).|Introduzca nombre .txt o .log sin ruta, distinto del proyecto (máximo 160 caracteres).|输入不带路径的 .txt 或 .log 文件名，且不能与项目同名（最多 160 字符）。
Ожидание в миллисекундах|Wait in milliseconds|Attente en millisecondes|Espera en milisegundos|按毫秒等待
Отдельная цепочка, как SUB|Separate chain, like SUB|Chaîne distincte, comme SUB|Cadena separada, como SUB|独立链，类似 SUB
Относительный ввод Windows|Windows relative input|Entrée relative Windows|Entrada relativa de Windows|Windows 相对输入
Отпустить|Release|Relâcher|Soltar|松开
Очистить|Clear|Effacer|Vaciar|清空
Очистить или удалить файл|Clear or delete file|Vider ou supprimer le fichier|Vaciar o eliminar archivo|清空或删除文件
Папки|Folders|Dossiers|Carpetas|文件夹
Первое совпадение из пяти случаев|First match of five cases|Première correspondance parmi cinq cas|Primera coincidencia de cinco casos|五个分支中首个匹配项
Первый пиксель в области|First pixel in region|Premier pixel de la zone|Primer píxel en la región|区域中的第一个像素
Перезаписать файл (только для STRWRITELN: 0 или 1)|Overwrite file (STRWRITELN only: 0 or 1)|Remplacer le fichier (STRWRITELN : 0 ou 1)|Sobrescribir archivo (solo STRWRITELN: 0 o 1)|覆盖文件（仅 STRWRITELN：0 或 1）
Переменная HWND (0 = не найдено)|HWND variable (0 = not found)|Variable HWND (0 = introuvable)|Variable HWND (0 = no encontrado)|HWND 变量（0 = 未找到）
Переменная HWND (0 = нет)|HWND variable (0 = none)|Variable HWND (0 = aucun)|Variable HWND (0 = ninguno)|HWND 变量（0 = 无）
Переменная X|X variable|Variable X|Variable X|X 变量
Переменная Y|Y variable|Variable Y|Variable Y|Y 变量
Переменная lParam|lParam variable|Variable lParam|Variable lParam|lParam 变量
Переменная wParam|wParam variable|Variable wParam|Variable wParam|wParam 变量
Переменная высоты|Height variable|Variable hauteur|Variable de altura|高度变量
Переменная заменённых|Replaced-count variable|Variable du nombre remplacé|Variable de cantidad sustituida|替换数量变量
Переменная кода языка|Language-code variable|Variable du code langue|Variable de código de idioma|语言代码变量
Переменная количества|Count variable|Variable de nombre|Variable de cantidad|数量变量
Переменная нового сообщения 1/0|New-message variable 1/0|Variable nouveau message 1/0|Variable mensaje nuevo 1/0|新消息变量 1/0
Переменная ответа (POST = 1)|Response variable (POST = 1)|Variable réponse (POST = 1)|Variable de respuesta (POST = 1)|响应变量（POST = 1）
Переменная уверенности|Confidence variable|Variable de confiance|Variable de confianza|置信度变量
Переменная числа точек|Point-count variable|Variable du nombre de points|Variable de cantidad de puntos|点数变量
Переменная ширины|Width variable|Variable largeur|Variable de ancho|宽度变量
По заголовку или его началу|By title or prefix|Par titre ou son début|Por título o su inicio|按标题或标题前缀
По части заголовка|By title substring|Par fragment de titre|Por parte del título|按标题片段
Повторений|Repetitions|Répétitions|Repeticiones|重复次数
Повторять тело до ложного результата или остановки|Repeat Body until false or stopped|Répéter le corps jusqu’à faux ou arrêt|Repetir cuerpo hasta falso o detención|重复循环体直到为假或停止
Позиция|Position|Position|Posición|位置
Позиция, размер, состояние и активация по HWND|Position, size, state and activation by HWND|Position, taille, état et activation par HWND|Posición, tamaño, estado y activación por HWND|按 HWND 设置位置、大小、状态和激活
Поиск по запомненному экрану|Search stored screen|Recherche dans la capture mémorisée|Buscar en pantalla guardada|搜索缓存截图
Поиск шаблона в запомненном экране|Search template in stored screen|Recherche de modèle dans la capture mémorisée|Buscar plantilla en pantalla guardada|在缓存截图中搜索模板
Показать или скрыть панель журнала|Show or hide journal panel|Afficher ou masquer le journal|Mostrar u ocultar panel del registro|显示或隐藏日志面板
Показать: 1, скрыть: 0|Show: 1, hide: 0|Afficher : 1, masquer : 0|Mostrar: 1, ocultar: 0|显示：1，隐藏：0
Получить X, Y, ширину, высоту по HWND|Get X, Y, width, height by HWND|Lire X, Y, largeur et hauteur par HWND|Obtener X, Y, ancho y alto por HWND|通过 HWND 获取 X、Y、宽和高
Получить текст или PID по HWND|Get text or PID by HWND|Lire texte ou PID par HWND|Obtener texto o PID por HWND|通过 HWND 获取文本或 PID
Получить текст по URL|Get text from URL|Obtenir le texte d’une URL|Obtener texto de URL|从 URL 获取文本
Последнее WM_USER (1024) окну Kliker Blocks|Latest WM_USER (1024) to Kliker Blocks window|Dernier WM_USER (1024) vers Kliker Blocks|Último WM_USER (1024) de Kliker Blocks|发送到 Kliker Blocks 窗口的最新 WM_USER (1024)
Последний цвет/изображение|Last found color/image|Dernière couleur/image trouvée|Último color/imagen encontrados|上次找到的颜色或图像
Последовательно перевести строки массива|Translate array strings in sequence|Traduire les chaînes du tableau successivement|Traducir cadenas de matriz en secuencia|依次翻译数组中的字符串
Правая|Right|Droite|Derecha|右键
Правая граница|Right bound|Limite droite|Límite derecho|右边界
Правая граница (не включена)|Right bound (exclusive)|Limite droite (exclue)|Límite derecho (excluido)|右边界（不包含）
Предел результатов|Result limit|Limite des résultats|Límite de resultados|结果上限
При новом нажатии|On new press|À chaque nouvel appui|Al pulsar de nuevo|每次新按下时
Проверка в один момент|Single check|Vérification ponctuelle|Comprobación puntual|单次检查
Проверка установленной пары на сервисе|Check language pair on service|Vérifier la paire sur le service|Comprobar par de idiomas en servicio|在服务端检查语言对
Проверяемое значение|Value to test|Valeur à tester|Valor a comprobar|待检查的值
Прокрутка вверх/вниз|Scroll up/down|Défilement haut/bas|Desplazar arriba/abajo|向上或向下滚动
Прочитать|Read|Lire|Leer|读取
Прочитать в массив|Read into array|Lire dans le tableau|Leer en matriz|读入数组
Прочитать один пиксель из запомненного экрана|Read one stored-screen pixel|Lire un pixel de la capture mémorisée|Leer píxel de pantalla guardada|读取缓存截图中的一个像素
Прочитать строку|Read line|Lire une ligne|Leer línea|读取行
Прочитать, записать, очистить или удалить файл|Read, write, clear or delete file|Lire, écrire, vider ou supprimer le fichier|Leer, escribir, vaciar o eliminar archivo|读取、写入、清空或删除文件
Путь к .ahk|Path to .ahk|Chemin vers .ahk|Ruta a .ahk|.ahk 路径
Путь к INI|Path to INI|Chemin du fichier INI|Ruta al INI|INI 文件路径
Путь к UTF-8 файлу|Path to UTF-8 file|Chemin du fichier UTF-8|Ruta al archivo UTF-8|UTF-8 文件路径
Путь к файлу|File path|Chemin du fichier|Ruta del archivo|文件路径
Путь с маской (необязательно; при заполнении заменяет два поля выше)|Wildcard path (optional; replaces two fields above)|Chemin avec masque (facultatif ; remplace les deux champs)|Ruta con comodín (opcional; sustituye los dos campos)|带通配符的路径（可选；替代上面两个字段）
Равно|Equal|Égal à|Igual|等于
Разделитель|Delimiter|Séparateur|Separador|分隔符
Разделить|Divide|Diviser|Dividir|除以
Размер|Size|Taille|Tamaño|大小
Разовое перемещение|Move once|Déplacement unique|Mover una vez|移动一次
Режим|Mode|Mode|Modo|模式
С чем сравнить|Compare with|Comparer avec|Comparar con|比较对象
Сдвиг X (+ вправо)|Delta X (+ right)|Décalage X (+ droite)|Desplazamiento X (+ derecha)|X 位移（正值向右）
Сдвиг Y (+ вниз)|Delta Y (+ down)|Décalage Y (+ bas)|Desplazamiento Y (+ abajo)|Y 位移（正值向下）
Секция|Section|Section|Sección|节
Синхронно|Synchronous|Synchrone|Síncrono|同步
Сложить|Add|Additionner|Sumar|加上
Случай 1|Case 1|Cas 1|Caso 1|情况 1
Случай 1 (пусто = пропуск)|Case 1 (blank = skip)|Cas 1 (vide = ignorer)|Caso 1 (vacío = omitir)|情况 1（留空则跳过）
Случай 2|Case 2|Cas 2|Caso 2|情况 2
Случай 3|Case 3|Cas 3|Caso 3|情况 3
Случай 4|Case 4|Cas 4|Caso 4|情况 4
Случай 5|Case 5|Cas 5|Caso 5|情况 5
Снимок для повторного анализа, аналог GETSCREEN|Snapshot for repeated analysis, like GETSCREEN|Capture réutilisable pour analyse, comme GETSCREEN|Captura para análisis repetido, como GETSCREEN|用于重复分析的截图，类似 GETSCREEN
Содержит|Contains|Contient|Contiene|包含
Сортировать автоматически|Sort automatically|Trier automatiquement|Ordenar automáticamente|自动排序
Сортировать текст|Sort as text|Trier comme texte|Ordenar como texto|按文本排序
Сортировать числа|Sort as numbers|Trier comme nombres|Ordenar como números|按数字排序
Состояние|State|État|Estado|状态
Состояние (0 = свернуть, 1 = восстановить)|State (0 = minimize, 1 = restore)|État (0 = réduire, 1 = restaurer)|Estado (0 = minimizar, 1 = restaurar)|状态（0 = 最小化，1 = 还原）
Сохранить PNG-файл|Save PNG file|Enregistrer un PNG|Guardar archivo PNG|保存 PNG 文件
Способ отправки|Send mode|Mode d’envoi|Modo de envío|发送方式
Сравнение|Comparison|Comparaison|Comparación|比较
Сравнить значение|Compare value|Comparer une valeur|Comparar valor|比较值
Сравнить цвет в X/Y|Compare color at X/Y|Comparer la couleur en X/Y|Comparar color en X/Y|比较 X/Y 处颜色
Средняя|Middle|Milieu|Central|中键
Старый цвет (-1 = любой)|Old color (-1 = any)|Ancienne couleur (-1 = toute)|Color anterior (-1 = cualquiera)|原颜色（-1 = 任意）
Строка в переменную; отмена = пусто|String into variable; cancel = empty|Texte dans variable ; annulation = vide|Texto en variable; cancelar = vacío|将字符串存入变量；取消 = 空
Таймаут без ввода, секунды (0 = ждать)|No-input timeout, seconds (0 = wait)|Délai sans saisie, secondes (0 = attendre)|Espera sin entrada, segundos (0 = esperar)|无输入超时（秒，0 = 一直等待）
Таймаут одной строки, мс|Per-line timeout, ms|Délai par ligne, ms|Espera por línea, ms|单行超时（毫秒）
Таймаут, мс (0 = без лимита)|Timeout, ms (0 = unlimited)|Délai, ms (0 = illimité)|Espera máxima, ms (0 = sin límite)|超时（毫秒，0 = 无限制）
Текст (поддерживает ${переменная})|Text (supports ${variable})|Texte (accepte ${variable})|Texto (admite ${variable})|文本（支持 ${variable}）
Текст из буфера в переменную|Clipboard text into variable|Texte du presse-papiers vers variable|Texto del portapapeles en variable|将剪贴板文本存入变量
Тело выполняется в общей очереди до «Конца ветки»; освобождение автоматическое|Body uses shared queue until Branch end; auto-release|Corps utilise la file commune jusqu’à Fin de branche ; libération automatique|Cuerpo usa cola compartida hasta Fin de rama; liberación automática|循环体占用共享队列直到“结束分支”；自动释放
Только начало заголовка|Title prefix only|Début du titre uniquement|Solo inicio del título|仅匹配标题开头
Удалить видимые записи|Clear visible entries|Effacer les entrées visibles|Borrar entradas visibles|清除可见记录
Удалить строку|Delete line|Supprimer une ligne|Eliminar línea|删除行
Удалить файл|Delete file|Supprimer le fichier|Eliminar archivo|删除文件
Удалить файл (только для TFCLEAR: 0 или 1)|Delete file (TFCLEAR only: 0 or 1)|Supprimer fichier (TFCLEAR : 0 ou 1)|Eliminar archivo (solo TFCLEAR: 0 o 1)|删除文件（仅 TFCLEAR：0 或 1）
Удержание, разовое нажатие или переключатель|Hold, one press or toggle|Maintien, appui unique ou bascule|Mantener, una pulsación o alternar|按住、单次按下或切换
Умножить|Multiply|Multiplier|Multiplicar|乘以
Файл .wav|.wav file|Fichier .wav|Archivo .wav|.wav 文件
Файл PNG|PNG file|Fichier PNG|Archivo PNG|PNG 文件
Файл шаблона|Template file|Fichier modèle|Archivo de plantilla|模板文件
Файлы|Files|Fichiers|Archivos|文件
Формат результата|Result format|Format du résultat|Formato de resultado|结果格式
Цвет #RRGGBB или COLORREF|Color #RRGGBB or COLORREF|Couleur #RRGGBB ou COLORREF|Color #RRGGBB o COLORREF|颜色 #RRGGBB 或 COLORREF
Частота и длительность|Frequency and duration|Fréquence et durée|Frecuencia y duración|频率和时长
Частота, Гц (37–32767)|Frequency, Hz (37–32767)|Fréquence, Hz (37–32767)|Frecuencia, Hz (37–32767)|频率，Hz（37–32767）
Часть заголовка|Title substring|Fragment du titre|Parte del título|标题片段
Число или ${имя}|Number or ${name}|Nombre ou ${nom}|Número o ${nombre}|数字或 ${name}
Число совпадающих пикселей в снимке|Matching pixel count in snapshot|Nombre de pixels correspondants dans la capture|Cantidad de píxeles coincidentes en captura|截图中匹配的像素数量
Числовой код сообщения|Numeric message code|Code numérique du message|Código numérico de mensaje|数字消息代码
Чтение или запись параметра|Read or write parameter|Lire ou écrire un paramètre|Leer o escribir parámetro|读取或写入参数
Что перечислить|What to list|Éléments à lister|Qué enumerar|列出内容
Что прочитать|What to read|Élément à lire|Qué leer|读取内容
Шаблон BMP/PNG|BMP/PNG template|Modèle BMP/PNG|Plantilla BMP/PNG|BMP/PNG 模板
Шагов|Steps|Étapes|Pasos|步数
Шагов (1 = одним движением)|Steps (1 = one movement)|Étapes (1 = un seul déplacement)|Pasos (1 = un movimiento)|步数（1 = 一次移动）
Экран X|Screen X|X écran|X de pantalla|屏幕 X
Экран Y|Screen Y|Y écran|Y de pantalla|屏幕 Y
Встроенный|Bundled|Intégré|Integrado|内置
Движок Argos: «Встроенный» или путь к своему Python (необязательно)|Argos engine: Bundled or your Python path (optional)|Moteur Argos : Intégré ou chemin vers votre Python (facultatif)|Motor Argos: Integrado o ruta de su Python (opcional)|Argos 引擎：内置或自定义 Python 路径（可选）
Встроенные модели готовы без установки Python. Дополнительные модели хранятся в {0}; устанавливайте пакеты только из доверенных источников.|Bundled models work without installing Python. Extra models are stored in {0}; install packages only from trusted sources.|Les modèles intégrés fonctionnent sans installer Python. Les modèles supplémentaires sont stockés dans {0} ; n'installez que des paquets fiables.|Los modelos integrados funcionan sin instalar Python. Los modelos adicionales se guardan en {0}; instale paquetes solo de fuentes fiables.|内置模型无需安装 Python。额外模型存储在 {0}；仅从可信来源安装模型包。
Встроенный Argos работает без Python на компьютере пользователя и без сервера. Дополнительный .argosmodel можно установить кнопкой выше.|Bundled Argos needs neither a system Python nor a server. Use the button above to install an extra .argosmodel.|Argos intégré ne nécessite ni Python système ni serveur. Le bouton ci-dessus installe un .argosmodel supplémentaire.|Argos integrado no requiere Python del sistema ni servidor. Use el botón de arriba para instalar otro .argosmodel.|内置 Argos 无需系统 Python 或服务器。使用上方按钮安装其他 .argosmodel。
Пакет {0} → {1} не используется блоками Kliker Blocks. Выберите пару ru/en/fr/zh/es/de.|Kliker Blocks blocks do not use {0} → {1}. Choose a pair among ru/en/fr/zh/es/de.|Les blocs Kliker Blocks n'utilisent pas {0} → {1}. Choisissez une paire parmi ru/en/fr/zh/es/de.|Los bloques Kliker Blocks no usan {0} → {1}. Elija un par entre ru/en/fr/zh/es/de.|Kliker Blocks 模块不使用 {0} → {1}。请选择 ru/en/fr/zh/es/de 中的语言对。
Проверьте адрес HTTP/HTTPS сервиса.|Check the HTTP/HTTPS service URL.|Vérifiez l'adresse HTTP/HTTPS du service.|Compruebe la URL HTTP/HTTPS del servicio.|请检查 HTTP/HTTPS 服务地址。
Английский\nФранцузский\nИспанский\nКитайский\nНемецкий|English\nFrench\nSpanish\nChinese\nGerman|Anglais\nFrançais\nEspagnol\nChinois\nAllemand|Inglés\nFrancés\nEspañol\nChino\nAlemán|英语\n法语\n西班牙语\n中文\n德语
Встроенные языки ru/en/fr/es/zh/de переводятся локально друг в друга без установки Python и без сервера. Выберите «Argos локально» в блоке перевода. Для других языков скачайте доверенный .argosmodel и установите его кнопкой выше. Поле движка оставьте «Встроенный»; свой Python нужен только для нестандартной конфигурации.|Built-in ru/en/fr/es/zh/de translate locally among one another without installing Python or a server. Choose Local Argos in the translation block. For other languages, download a trusted .argosmodel and install it above. Leave the engine set to Bundled; a custom Python is only for advanced setups.|Les langues ru/en/fr/es/zh/de intégrées se traduisent localement sans installer Python ni serveur. Choisissez Argos local dans le bloc de traduction. Pour d'autres langues, installez un .argosmodel fiable avec le bouton ci-dessus. Laissez le moteur sur Intégré ; un Python personnel est réservé aux configurations avancées.|Los idiomas integrados ru/en/fr/es/zh/de se traducen localmente sin instalar Python ni servidor. Elija Argos local en el bloque de traducción. Para otros idiomas, instale un .argosmodel fiable con el botón de arriba. Deje el motor en Integrado; un Python propio solo hace falta para configuraciones avanzadas.|内置的 ru/en/fr/es/zh/de 语言可在本地相互翻译，无需安装 Python 或服务器。请在翻译模块中选择本地 Argos。其他语言可用上方按钮安装可信的 .argosmodel。引擎保持“内置”；自定义 Python 仅用于高级配置。
Данные, условия и циклы|Data, conditions and loops|Données, conditions et boucles|Datos, condiciones y bucles|数据、条件与循环
Показать итоги обработки|Show processing results|Afficher les résultats du traitement|Mostrar resultados del procesamiento|显示处理结果
Глава 2: граф обрабатывает переменные, массивы и файлы в папке демонстрации.|Chapter 2: the graph processes variables, arrays and files in the demo folder.|Chapitre 2 : le graphe traite variables, tableaux et fichiers dans le dossier de démonstration.|Capítulo 2: el grafo procesa variables, matrices y archivos en la carpeta de demostración.|第二章：图表在演示文件夹中处理变量、数组和文件。
Итог обработки: слов {0}, файлов {1}, счётчик {2}.|Processing results: {0} words, {1} files, counter {2}.|Résultats : {0} mots, {1} fichiers, compteur {2}.|Resultados: {0} palabras, {1} archivos, contador {2}.|处理结果：{0} 个词、{1} 个文件、计数器 {2}。
""";
}
