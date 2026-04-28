namespace GeoSlayer.Domain.Enums;

/// <summary>
/// Game skills that can be trained at real-world Points of Interest.
/// </summary>
public enum SkillType
{
    /// <summary>Churches, cathedrals, mosques, temples, synagogues.</summary>
    Prayer,

    /// <summary>Libraries, bookshops, universities, schools, museums.</summary>
    Knowledge,

    /// <summary>Forests, woods, nature reserves, parks with trees.</summary>
    Woodcutting,

    /// <summary>Rivers, lakes, ponds, harbours, piers, fishing spots.</summary>
    Fishing,

    /// <summary>Hospitals, pharmacies, doctors, clinics.</summary>
    Healing,

    /// <summary>Gyms, sports centres, stadiums, athletics tracks.</summary>
    Athletics,

    /// <summary>Pubs, bars, taverns, restaurants, cafés.</summary>
    Tavern,

    /// <summary>Shops, markets, supermarkets, malls.</summary>
    Trading,

    /// <summary>Banks, ATMs, post offices.</summary>
    Banking,

    /// <summary>Castles, ruins, forts, monuments, memorials.</summary>
    Combat,

    /// <summary>Mines, quarries, caves.</summary>
    Mining,

    /// <summary>Farms, allotments, gardens, greenhouses.</summary>
    Farming,

    /// <summary>Blacksmiths, workshops, craft centres, forges.</summary>
    Smithing,

    /// <summary>Bakeries, kitchens, food markets.</summary>
    Cooking,

    /// <summary>Anything that doesn't fit the above.</summary>
    Exploration,
}
