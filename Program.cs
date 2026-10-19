
// ספרייה לבניית כלי שורת פקודה (CLI): פקודות, אפשרויות, ניתוח קלט
using System.CommandLine;

// ===================== הפקודה bundle =====================

// יצירת תת-הפקודה bundle: שם + תיאור שמופיע ב-help
var bundleCommand = new Command("bundle", "zip code files to one file");

// אפשרות --output (קיצור -o): שם או נתיב מלא לקובץ הפלט
var outputOption = new Option<FileInfo>("--output", "-o")
{
    Description = "Bundle file name, or a full path. A name alone is saved in the current folder",
    // ערך ברירת מחדל אם המשתמש לא כתב --output
    DefaultValueFactory = _ => new FileInfo("AllCodesCombined.txt")
};
// חיבור האפשרות לפקודה bundle
bundleCommand.Options.Add(outputOption);

// מילון: שם שפה -> סיומות הקבצים שלה. מתעלם מאותיות גדולות/קטנות
var languageExtensions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["csharp"] = [".cs"],
    ["java"] = [".java"],
    ["python"] = [".py"],
    ["javascript"] = [".js"],
    ["typescript"] = [".ts"],
    ["html"] = [".html", ".htm"],
    ["css"] = [".css"],
    ["c"] = [".c", ".h"],
    ["sql"] = [".sql"],
};

// תיקיות שלא נכלול מהן קבצים (קבצים שנוצרים אוטומטית, הגדרות וכו')
var excludedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "bin", "obj", "debug", "release", ".vs", ".git", ".idea", "node_modules", "packages", "publish"
};

// אפשרות --language (קיצור -l): רשימת שפות. אם לא נכתבה - יוצג תפריט
var languageOption = new Option<string[]>("--language", "-l")
{
    Description = "Programming languages to include (e.g. csharp java), or 'all'. If omitted, a selection menu is shown",
    // מאפשר לכתוב כמה ערכים ברצף: -l csharp python
    AllowMultipleArgumentsPerToken = true
};
bundleCommand.Options.Add(languageOption);

// אפשרות --note (קיצור -n): דגל בוליאני. אם נכתב - יירשם מקור כל קובץ כהערה
var noteOption = new Option<bool>("--note", "-n")
{
    Description = "Write the source file name and relative path as a comment before each file"
};
bundleCommand.Options.Add(noteOption);

// אפשרות --sort (קיצור -s): סדר הקבצים, לפי שם או לפי סוג
var sortOption = new Option<string>("--sort", "-s")
{
    Description = "Order of files: 'name' (A-Z by file name) or 'type' (by code type)",
    DefaultValueFactory = _ => "name"
};
// ולידציה: מותר רק name או type, אחרת הספרייה מציגה שגיאה
sortOption.AcceptOnlyFromAmong("name", "type");
bundleCommand.Options.Add(sortOption);

// אפשרות --remove-empty-lines (קיצור -r): דגל למחיקת שורות ריקות
var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "-r")
{
    Description = "Remove empty lines from the code before bundling"
};
bundleCommand.Options.Add(removeEmptyLinesOption);

// אפשרות --author (קיצור -a): שם יוצר הקובץ
var authorOption = new Option<string>("--author", "-a")
{
    Description = "Name of the bundle's author, written as a comment at the top of the file"
};
// ולידציה: שם היוצר לא יכול להיות ריק
authorOption.Validators.Add(result =>
{
    if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
        result.AddError("Author name cannot be empty.");
});
bundleCommand.Options.Add(authorOption);

// הקוד שירוץ כשהמשתמש מריץ: fid bundle ...
// output הוא ה-parseResult - דרכו שולפים את ערכי האפשרויות
bundleCommand.SetAction((output) =>
{
    // התיקייה שממנה אוספים קבצים: "." = התיקייה הנוכחית
    string rootPath = ".";

    // שליפת קובץ הפלט והמרתו לנתיב מלא
    FileInfo outputFile = output.GetValue(outputOption)!;
    string outputFilePath = outputFile.FullName;

    // שליפת ערכי שאר האפשרויות
    string[] languages = output.GetValue(languageOption) ?? [];
    bool note = output.GetValue(noteOption);
    string sort = output.GetValue(sortOption)!;
    bool removeEmptyLines = output.GetValue(removeEmptyLinesOption);
    string? author = output.GetValue(authorOption);

    // אם לא נבחרו שפות - מציגים תפריט ממוספר ונותנים למשתמש לבחור
    if (languages.Length == 0)
    {
        List<string> menu = languageExtensions.Keys.ToList();

        Console.WriteLine("Choose languages to include:");
        Console.WriteLine("  0. all");
        for (int i = 0; i < menu.Count; i++)
            Console.WriteLine($"  {i + 1}. {menu[i]}");
        Console.Write("Enter numbers separated by spaces (e.g. 1 3): ");

        string input = ReadInput();
        var chosen = new List<string>();
        // מפרקים את הקלט למספרים, וממירים כל מספר לשם השפה
        foreach (string part in input.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out int num) && num == 0)
                chosen.Add("all");
            else if (int.TryParse(part, out num) && num >= 1 && num <= menu.Count)
                chosen.Add(menu[num - 1]);
            else
                Console.WriteLine($"Invalid choice: {part}");
        }
        languages = chosen.ToArray();
    }

    // בניית רשימת הסיומות שנחפש. HashSet = בלי כפילויות
    HashSet<string> extensions;
    if (languages.Any(l => l.Equals("all", StringComparison.OrdinalIgnoreCase)))
    {
        // all = כל הסיומות מכל השפות במילון
        extensions = languageExtensions.Values.SelectMany(e => e).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    else
    {
        // מוסיפים את הסיומות של כל שפה שנבחרה
        extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string language in languages)
        {
            if (languageExtensions.TryGetValue(language, out string[]? exts))
                extensions.UnionWith(exts);
            else
                Console.WriteLine($"Unknown language: {language}");
        }
    }

    // אם אין אף סיומת תקינה - עוצרים בלי ליצור קובץ
    if (extensions.Count == 0)
    {
        Console.WriteLine("No valid languages were given.");
        return;
    }

    // הנתיב המלא של תיקיית המקור, לחישוב נתיבים יחסיים
    string fullRoot = Path.GetFullPath(rootPath);

    // איסוף כל הקבצים מהתיקייה ומתתי-התיקיות, ואז סינון:
    var codeFiles = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
        // 1. רק קבצים עם סיומת מהשפות שנבחרו
        .Where(f => extensions.Contains(Path.GetExtension(f)))
        // 2. לא את קובץ הפלט עצמו
        .Where(f => !Path.GetFullPath(f).Equals(outputFilePath, StringComparison.OrdinalIgnoreCase))
        // 3. לא קבצים שנמצאים בתוך תיקייה מוחרגת (bin, obj וכו')
        .Where(f => !Path.GetRelativePath(fullRoot, Path.GetFullPath(f))
            .Split(Path.DirectorySeparatorChar)
            .SkipLast(1)
            .Any(folder => excludedFolders.Contains(folder)));

    // מיון: type = לפי סיומת ואז לפי שם, name = לפי א"ב של שם הקובץ
    codeFiles = sort == "type"
        ? codeFiles.OrderBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                   .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
        : codeFiles.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

    // try/catch: אם השמירה נכשלת - מציגים הודעת שגיאה מסודרת במקום קריסה
    try
    {
        // פתיחת קובץ הפלט לכתיבה. using סוגר אותו אוטומטית בסוף
        using (StreamWriter writer = new StreamWriter(outputFilePath))
        {
            // אם סופק שם יוצר - כותבים אותו כהערה בראש הקובץ
            if (!string.IsNullOrWhiteSpace(author))
            {
                writer.WriteLine($"// Author: {author}");
                writer.WriteLine();
            }

            foreach (string filePath in codeFiles)
            {
                // אם נבחר --note - כותבים הערה עם שם הקובץ והנתיב היחסי שלו
                if (note)
                {
                    string relativePath = Path.GetRelativePath(fullRoot, Path.GetFullPath(filePath));
                    writer.WriteLine($"// Source: {Path.GetFileName(filePath)} ({relativePath})");
                }

                // קריאת שורות הקובץ, ואם נבחר -r - סינון השורות הריקות
                IEnumerable<string> lines = File.ReadLines(filePath);
                if (removeEmptyLines)
                    lines = lines.Where(line => !string.IsNullOrWhiteSpace(line));

                // העתקת השורות לקובץ הפלט
                foreach (string line in lines)
                    writer.WriteLine(line);

                // שורה ריקה בין קובץ לקובץ
                writer.WriteLine();
            }
        }
        Console.WriteLine($"Bundle saved to: {outputFilePath}");
    }
    // התיקייה שבנתיב לא קיימת
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine($"Error: the folder '{outputFile.DirectoryName}' does not exist.");
    }
    // אין הרשאה לכתוב במיקום הזה
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine($"Error: no permission to save to '{outputFilePath}'.");
    }
    // כל שגיאת קובץ אחרת (למשל קובץ פתוח בתוכנה אחרת)
    catch (IOException ex)
    {
        Console.WriteLine($"Error: could not save the file '{outputFilePath}'. {ex.Message}");
    }

});

// ===================== הפקודה create-rsp =====================

// פקודה שיוצרת קובץ תגובה (response file) עם פקודת bundle מוכנה
var createRspCommand = new Command("create-rsp", "Create a response file with a ready bundle command");
createRspCommand.SetAction(_ =>
{
    // השורות שייכתבו לקובץ ה-rsp. השורה הראשונה היא שם הפקודה
    var rspLines = new List<string> { "bundle" };

    // שאלה 1: שפות. הבדיקה מוודאת שהקלט לא ריק ושכל שפה מוכרת
    Console.WriteLine($"Available languages: {string.Join(", ", languageExtensions.Keys)}");
    string[] languages = Ask(
        "Languages to include, separated by spaces (or 'all'): ",
        input =>
        {
            string[] parts = input.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "Please enter at least one language.";
            string[] unknown = parts.Where(p => !p.Equals("all", StringComparison.OrdinalIgnoreCase) && !languageExtensions.ContainsKey(p)).ToArray();
            return unknown.Length > 0 ? $"Unknown language(s): {string.Join(", ", unknown)}" : null;
        }).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
    rspLines.Add($"--language {string.Join(" ", languages)}");

    // שאלה 2: קובץ פלט. בודקים תווים אסורים ושהתיקייה קיימת. Enter = ברירת מחדל
    string outputPath = Ask(
        "Bundle file name or full path (Enter for AllCodesCombined.txt): ",
        input =>
        {
            if (input.Length == 0)
                return null;
            if (input.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || Path.GetFileName(input).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return "The path contains invalid characters.";
            if (Path.GetFileName(input).Length == 0)
                return "Please enter a file name, not just a folder.";
            string? folder = Path.GetDirectoryName(Path.GetFullPath(input));
            if (folder != null && !Directory.Exists(folder))
                return $"The folder '{folder}' does not exist.";
            return null;
        });
    // מירכאות סביב הנתיב, למקרה שיש בו רווחים
    if (outputPath.Length > 0)
        rspLines.Add($"--output \"{outputPath}\"");

    // שאלה 3: האם לרשום את מקור הקוד
    if (AskYesNo("Add the source file path as a comment? (y/n): "))
        rspLines.Add("--note");

    // שאלה 4: סדר המיון. מותר רק name, type או Enter
    string sort = Ask(
        "Sort files by 'name' or 'type' (Enter for name): ",
        input => input.Length == 0 || input.Equals("name", StringComparison.OrdinalIgnoreCase) || input.Equals("type", StringComparison.OrdinalIgnoreCase)
            ? null
            : "Please enter 'name' or 'type'.");
    if (sort.Length > 0)
        rspLines.Add($"--sort {sort.ToLower()}");

    // שאלה 5: האם למחוק שורות ריקות
    if (AskYesNo("Remove empty lines? (y/n): "))
        rspLines.Add("--remove-empty-lines");

    // שאלה 6: שם היוצר. אסור מירכאות כי הן ישברו את הפקודה בקובץ
    string author = Ask(
        "Author name (Enter to skip): ",
        input => input.Contains('"') ? "The name cannot contain quotation marks." : null);
    if (author.Length > 0)
        rspLines.Add($"--author \"{author}\"");

    // שם קובץ ה-rsp עצמו. ברירת מחדל fid.rsp, ומוסיפים סיומת אם חסרה
    string rspName = Ask(
        "Response file name (Enter for fid.rsp): ",
        input => input.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "The file name contains invalid characters." : null);
    if (rspName.Length == 0)
        rspName = "fid.rsp";
    if (!Path.HasExtension(rspName))
        rspName += ".rsp";

    // כתיבת כל השורות לקובץ, עם הודעת שגיאה אם השמירה נכשלת
    try
    {
        File.WriteAllLines(rspName, rspLines);
        Console.WriteLine($"Response file saved to: {Path.GetFullPath(rspName)}");
        Console.WriteLine($"Run it with: fid @{rspName}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.WriteLine($"Error: could not save the response file. {ex.Message}");
    }
});

// ===================== פקודת השורש והרצה =====================

// פקודת השורש = התוכנה fid עצמה
var rootCommand = new RootCommand("root command for fid");

// חיבור תתי-הפקודות לפקודת השורש
rootCommand.Subcommands.Add(bundleCommand);
rootCommand.Subcommands.Add(createRspCommand);

// ניתוח מה שהמשתמש הקליד והרצת הפקודה המתאימה
return await rootCommand.Parse(args).InvokeAsync();

// ===================== פונקציות עזר =====================

// קורא שורה מהמשתמש ומנקה אותה. אם הקלט נגמר (null) - יוצא מהתוכנה
static string ReadInput()
{
    string? line = Console.ReadLine();
    if (line == null)
    {
        Console.WriteLine();
        Console.WriteLine("Input ended. Exiting.");
        Environment.Exit(1);
    }
    // מסיר תו BOM בלתי נראה ורווחים מההתחלה ומהסוף
    return line.Trim((char)0xFEFF).Trim();
}

// שואל שאלה שוב ושוב עד שהתשובה עוברת את הבדיקה
// validate מחזירה null אם הקלט תקין, או הודעת שגיאה אם לא
static string Ask(string question, Func<string, string?> validate)
{
    while (true)
    {
        Console.Write(question);
        string input = ReadInput();
        string? error = validate(input);
        if (error == null)
            return input;
        Console.WriteLine($"Invalid input: {error}");
    }
}

// שאלת כן/לא. Enter ריק נחשב "לא"
static bool AskYesNo(string question)
{
    string answer = Ask(question, input =>
        input.ToLower() is "y" or "yes" or "n" or "no" or "" ? null : "Please answer 'y' or 'n'.");
    return answer.ToLower() is "y" or "yes";
}
