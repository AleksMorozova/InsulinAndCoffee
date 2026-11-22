using System.Text.RegularExpressions;

namespace InsulinAndCoffee.Application.Services;

internal static class AssistantLanguage
{
    internal static string Detect(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            if (Regex.IsMatch(message, "[іїєґІЇЄҐ]|борщу|хліб|дві|прибери|додай|знайди|скільки|вуглевод|замовлення", RegexOptions.IgnoreCase)) return "uk";
            if (Regex.IsMatch(message, "[а-яёА-ЯЁ]")) return "ru";
            if (Regex.IsMatch(message, "[a-zA-Z]")) return "en";
        }
        return "en";
    }

    internal static string Reply(string language, string status, bool draft, bool found) => (language, status, draft, found) switch
    {
        ("ru", "PartiallyCompleted", _, _) => "Приём пищи собран частично. Уточни недостающие продукты.",
        ("uk", "PartiallyCompleted", _, _) => "Прийом їжі зібрано частково. Уточни відсутні продукти.",
        (_, "PartiallyCompleted", _, _) => "Meal partially assembled. Clarify the unresolved items.",
        ("ru", "NeedsClarification", _, _) => "Уточни продукт, сохранённый заказ или порцию.",
        ("uk", "NeedsClarification", _, _) => "Уточни продукт, збережене замовлення або порцію.",
        (_, "NeedsClarification", _, _) => "Choose a saved item or clarify the food and quantity.",
        ("ru", _, true, true) => "Приём пищи обновлён.", ("uk", _, true, true) => "Прийом їжі оновлено.", (_, _, true, true) => "Meal updated.",
        ("ru", _, _, true) => "Вот что нашлось в сохранённых записях.", ("uk", _, _, true) => "Ось що знайшлося у збережених записах.", (_, _, _, true) => "Here’s what I found in your saved records.",
        ("ru", _, _, _) => "Подходящих сохранённых записей не найдено.", ("uk", _, _, _) => "Відповідних збережених записів не знайдено.", _ => "No matching saved records found."
    };

    internal static string Detail(string text, string language)
    {
        if (language == "en") return text;
        var uk = language == "uk";
        var patterns = new (string Pattern, string Ru, string Uk)[] {
            (@"^No saved food matched (.+)\.$", "Не найден сохранённый продукт: $1.", "Не знайдено збережений продукт: $1."),
            (@"^More than one saved food matched (.+)\.$", "Найдено несколько продуктов: $1. Выбери нужный.", "Знайдено кілька продуктів: $1. Обери потрібний."),
            (@"^More than one saved item matched (.+)\. Choose a specific saved food or order item\.$", "Найдено несколько вариантов: $1. Выбери продукт или позицию заказа.", "Знайдено кілька варіантів: $1. Обери продукт або позицію замовлення."),
            (@"^Specify a compatible quantity for (.+) \((.+)\)\.$", "Уточни количество для $1 ($2).", "Уточни кількість для $1 ($2)."),
            (@"^Specify a quantity in (.+) for (.+)\.$", "Уточни количество ($1) для $2.", "Уточни кількість ($1) для $2."),
            (@"^Saved carbohydrate values are unavailable for (.+)\.$", "Нет сохранённых углеводов для $1.", "Немає збережених вуглеводів для $1."),
            (@"^Choose one complete saved order for (.+) from (.+)\.$", "Выбери сохранённый заказ $1 из $2.", "Обери збережене замовлення $1 із $2.")
        };
        foreach (var pattern in patterns)
            if (Regex.IsMatch(text, pattern.Pattern)) return Units(Regex.Replace(text, pattern.Pattern, uk ? pattern.Uk : pattern.Ru), uk);
        var messages = new Dictionary<string, (string Ru, string Uk)> {
            ["Change carbohydrate overrides in Calculator."] = ("Измени углеводы в калькуляторе.", "Зміни вуглеводи в калькуляторі."),
            ["That food is not in your draft. Ask to add it first."] = ("Этого продукта нет в текущем приёме пищи. Сначала добавь его.", "Цього продукту немає в поточному прийомі їжі. Спочатку додай його."),
            ["Specify a positive quantity in the saved unit."] = ("Укажи положительное количество в сохранённых единицах.", "Укажи додатну кількість у збережених одиницях."),
            ["That quantity is incompatible with the saved item."] = ("Количество не соответствует сохранённым единицам.", "Кількість не відповідає збереженим одиницям."),
            ["Several foods still need clarification. Resend the complete meal with the intended foods and quantities."] = ("Несколько продуктов требуют уточнения. Укажи продукты и количества.", "Кілька продуктів потребують уточнення. Укажи продукти та кількості."),
            ["Clarify the unresolved food before replacing another item."] = ("Уточни недостающий продукт перед заменой другого.", "Уточни відсутній продукт перед заміною іншого."),
            ["Which item should I replace?"] = ("Какой продукт заменить?", "Який продукт замінити?"),
            ["The saved order item changed. Choose it again before changing quantities."] = ("Сохранённая позиция изменилась. Выбери её снова.", "Збережена позиція змінилася. Обери її знову.")
        };
        if (messages.TryGetValue(text, out var message)) return uk ? message.Uk : message.Ru;
        if (text.StartsWith("Which item should I change: ")) return (uk ? "Який продукт змінити: " : "Какой продукт изменить: ") + text[28..];
        return text;
    }
    private static string Units(string value, bool uk) => value.Replace("Grams", uk ? "грами" : "граммы").Replace("Portion", uk ? "порції" : "порции").Replace("Piece", uk ? "штуки" : "штуки");
}
