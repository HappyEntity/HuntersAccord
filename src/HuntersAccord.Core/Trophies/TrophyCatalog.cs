namespace HuntersAccord.Core.Trophies;

/// <summary>A trophy's grade.</summary>
public enum TrophyGrade
{
    Bronze,
    Silver,
    Gold,
    Platinum,
}

/// <summary>
/// One trophy of Hunters' Accord's own list (Bloodborne Game of the Year Edition: the base game and The Old Hunters).
/// <see cref="Id"/> is the number the game itself gives the trophy; names are the game's trophy titles, descriptions our own
/// words.
/// </summary>
public sealed record TrophyInfo(
    int Id,
    TrophyGrade Grade,
    bool Hidden,
    bool Dlc,
    string NameEn,
    string NameRu,
    string DescriptionEn,
    string DescriptionRu)
{
    /// <summary>The title in the language asked for ("ru…" Russian, else English).</summary>
    public string Name(string? language) => IsRussian(language) ? NameRu : NameEn;

    public string Description(string? language) => IsRussian(language) ? DescriptionRu : DescriptionEn;

    private static bool IsRussian(string? language) => language?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>
/// The trophy list (40: 34 of the base game with its platinum, 6 of The Old Hunters). The platinum needs every other
/// trophy of the base game (not the DLC's), as on a PS4.
/// Titles, grades and hidden flags come from public trophy lists: the PlayStation Network's and the Bloodborne Wiki's
/// (English https://bloodborne.fandom.com/wiki/Trophies, Russian https://bloodborne.fandom.com/ru/wiki/Достижения).
/// The descriptions are our own words, not the game's.
/// </summary>
public static class TrophyCatalog
{
    public const int PlatinumId = 0;

    public static IReadOnlyList<TrophyInfo> All { get; } =
    [
        new(0, TrophyGrade.Platinum, false, false, "Bloodborne", "Bloodborne™",
            "Every other trophy of the base game earned.", "Получены все остальные трофеи основной игры."),

        new(1, TrophyGrade.Gold, true, false, "Yharnam Sunrise", "Ярнамский восход",
            "Accepted the end Gehrman offers and woke to a new morning.", "Принять конец, который предлагает Герман, и встретить новое утро."),
        new(2, TrophyGrade.Gold, true, false, "Honoring Wishes", "Исполнение желаний",
            "Refused Gehrman, defeated him and took his place in the Dream.", "Отказать Герману, победить его и занять его место во Сне."),
        new(3, TrophyGrade.Gold, true, false, "Childhood's Beginning", "Начало детства",
            "Ate three cords, faced what came for you after Gehrman and was reborn.",
            "Съесть три пуповины, одолеть того, кто пришёл после Германа, и переродиться."),
        new(4, TrophyGrade.Gold, true, false, "Yharnam, Pthumerian Queen", "Ярнам, Птумерианская Королева",
            "Defeated the queen of the Pthumerians deep in the Chalice Dungeons.", "Победить королеву птумерианцев в глубине подземелий Чаши."),
        new(5, TrophyGrade.Gold, false, false, "Hunter's Essence", "Сущность охотника",
            "Collected every hunter weapon of the base game.", "Собрать всё оружие охотника основной игры."),
        new(6, TrophyGrade.Gold, false, false, "Hunter's Craft", "Ремесло охотника",
            "Collected every special hunter tool.", "Собрать все особые инструменты охотника."),

        new(7, TrophyGrade.Silver, false, false, "Weapon Master", "Оружейник",
            "Brought a weapon to its highest level (+10).", "Улучшить оружие до наивысшего уровня (+10)."),
        new(8, TrophyGrade.Silver, false, false, "Blood Gem Master", "Мастер по кровавым самоцветам",
            "Obtained a blood gem of the very highest rank.", "Получить кровавый самоцвет высочайшего ранга."),
        new(9, TrophyGrade.Silver, false, false, "Rune Master", "Мастер рун",
            "Obtained a rare, powerful Caryll Rune.", "Получить редкую сильную руну Каррилла."),
        new(10, TrophyGrade.Silver, true, false, "Cainhurst", "Кейнхёрст",
            "Reached the forsaken castle across the lake.", "Попасть в заброшенный замок за озером."),
        new(11, TrophyGrade.Silver, true, false, "The Choir", "Хор",
            "Was let into the Choir's quarters high above the Cathedral Ward.", "Войти во владения Хора над Соборным округом."),
        new(12, TrophyGrade.Silver, true, false, "The Source of the Dream", "Источник сна",
            "Found the old workshop the Dream was born from.", "Найти старую мастерскую, из которой возник Сон."),
        new(13, TrophyGrade.Silver, true, false, "Nightmare Lecture Building", "Лекционный зал кошмара",
            "Entered the lecture building lost in the nightmare.", "Войти в лекционный корпус, затерянный в кошмаре."),

        new(14, TrophyGrade.Bronze, true, false, "Father Gascoigne", "Отец Гаскойн",
            "Slew the hunter who lost himself to the beast.", "Убить охотника, поддавшегося зверю в себе."),
        new(15, TrophyGrade.Bronze, true, false, "Vicar Amelia", "Викарий Амелия",
            "Slew the beast the vicar of the Healing Church became.", "Убить чудовище, в которое превратилась викарий Церкви исцеления."),
        new(16, TrophyGrade.Bronze, true, false, "Shadow of Yharnam", "Тень Ярнама",
            "Defeated the three shadows in the Forbidden Woods.", "Победить трёх теней в Запретном лесу."),
        new(17, TrophyGrade.Bronze, true, false, "Rom, the Vacuous Spider", "Ром, Праздный Паук",
            "Defeated the Great One hidden under the lake.", "Победить Великую, что прячется под озером."),
        new(18, TrophyGrade.Bronze, true, false, "The One Reborn", "Возродившийся",
            "Defeated the horror raised in Yahar'gul.", "Победить чудовище, поднятое в Яхаргуле."),
        new(19, TrophyGrade.Bronze, true, false, "Micolash, Host of the Nightmare", "Миколаш, Хозяин Кошмара",
            "Ran down and defeated the host of the nightmare.", "Догнать и победить хозяина кошмара."),
        new(20, TrophyGrade.Bronze, true, false, "Mergo's Wet Nurse", "Кормилица Мерго",
            "Defeated the Great One guarding Mergo.", "Победить Великую, охраняющую Мерго."),
        new(21, TrophyGrade.Bronze, true, false, "Cleric Beast", "Церковное чудовище",
            "Slew the beast on the great bridge.", "Убить чудовище на большом мосту."),
        new(22, TrophyGrade.Bronze, true, false, "Blood-starved Beast", "Чудовище-кровоглот",
            "Slew the beast in the chapel of Old Yharnam.", "Убить чудовище в церкви Старого Ярнама."),
        new(23, TrophyGrade.Bronze, true, false, "The Witch of Hemwick", "Ведьма Хемвика",
            "Defeated the witch who collects eyes.", "Победить ведьму, собирающую глаза."),
        new(24, TrophyGrade.Bronze, true, false, "Darkbeast Paarl", "Черное чудовище Паарл",
            "Defeated the crackling darkbeast under the bridge.", "Победить искрящееся чёрное чудовище под мостом."),
        new(25, TrophyGrade.Bronze, true, false, "Amygdala", "Амигдала",
            "Defeated the Great One of the Nightmare Frontier.", "Победить Великую с Границы кошмара."),
        new(26, TrophyGrade.Bronze, true, false, "Martyr Logarius", "Мученик Логариус",
            "Defeated the martyr on the castle's roof.", "Победить мученика на крыше замка."),
        new(27, TrophyGrade.Bronze, true, false, "Celestial Emissary", "Посланник Небес",
            "Defeated the emissary in the garden above the Choir's halls.", "Победить посланника в саду над залами Хора."),
        new(28, TrophyGrade.Bronze, true, false, "Ebrietas, Daughter of the Cosmos", "Ибраитас, дочь Космоса",
            "Defeated the Great One left behind at the altar of despair.", "Победить Великую, оставленную у алтаря отчаяния."),
        new(29, TrophyGrade.Bronze, false, false, "Blood Gem Contact", "Связь кровавого самоцвета",
            "Obtained a first blood gem for a hunter weapon.", "Получить первый кровавый самоцвет для оружия охотника."),
        new(30, TrophyGrade.Bronze, false, false, "Rune Contact", "Связь Руны",
            "Obtained a first Caryll Rune that grants special strength.", "Получить первую руну Каррилла, дающую особую силу."),
        new(31, TrophyGrade.Bronze, false, false, "Chalice of Pthumeru", "Чаша Птумеру",
            "Obtained a Pthumeru chalice for the dungeon rituals.", "Получить Чашу Птумеру для обряда подземелья."),
        new(32, TrophyGrade.Bronze, true, false, "Chalice of Ailing Loran", "Чаша Больного Лорана",
            "Obtained a chalice of Loran.", "Получить Чашу Лорана."),
        new(33, TrophyGrade.Bronze, true, false, "Chalice of Isz", "Чаша Исза",
            "Obtained the great chalice of Isz.", "Получить Великую Чашу Исза."),

        new(34, TrophyGrade.Gold, false, true, "Old Hunter's Essence", "Сущность старого охотника",
            "Collected every weapon of the old hunters.", "Собрать всё оружие старых охотников."),
        new(35, TrophyGrade.Silver, true, true, "Orphan of Kos", "Сирота Коса",
            "Defeated the orphan on the shore of the fishing hamlet.", "Победить сироту на берегу рыбацкой деревни."),
        new(36, TrophyGrade.Bronze, true, true, "Ludwig, the Holy Blade", "Людвиг Священный клинок",
            "Defeated the beast the first hunter of the Church became.", "Победить чудовище, в которое превратился первый охотник Церкви."),
        new(37, TrophyGrade.Bronze, true, true, "Lady Maria of the Astral Clocktower", "Леди Мария из Астральной часовой башни",
            "Defeated the hunter who keeps the clocktower.", "Победить охотницу, хранящую часовую башню."),
        new(38, TrophyGrade.Bronze, true, true, "Living Failures", "Живые неудачи",
            "Defeated the failed attempts to make Great Ones.", "Победить неудачные попытки сотворить Великих."),
        new(39, TrophyGrade.Bronze, true, true, "Laurence, the First Vicar", "Первый викарий Лоренс",
            "Defeated the beast the first vicar became.", "Победить чудовище, в которое превратился первый викарий."),
    ];

    private static readonly Dictionary<int, TrophyInfo> ById = All.ToDictionary(t => t.Id);

    public static TrophyInfo? Find(int id) => ById.GetValueOrDefault(id);

    /// <summary>The trophies the platinum asks for: every base-game trophy but the platinum itself.</summary>
    public static IEnumerable<int> PlatinumNeeds => All.Where(t => !t.Dlc && t.Id != PlatinumId).Select(t => t.Id);
}
