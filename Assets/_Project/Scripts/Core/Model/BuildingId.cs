namespace ResortEmpire.Core
{
    /// <summary>
    /// Buildings of the prototype (Build doc §4.5). Each one serves exactly one need:
    /// Reception → Rooms, Restaurant → Food, Beach Bar → Drinks, Pool → Relax, Bungalows → Ocean View.
    /// </summary>
    public enum BuildingId
    {
        Reception = 0,
        Restaurant = 1,
        BeachBar = 2,
        Pool = 3,
        Bungalows = 4,
    }
}
