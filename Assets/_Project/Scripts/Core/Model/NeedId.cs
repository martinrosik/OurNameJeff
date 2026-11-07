namespace ResortEmpire.Core
{
    /// <summary>
    /// Guest need types (Build doc §4.4). Rooms, Drinks and Food exist from the start;
    /// Relax arrives with the Pool unlock and Ocean View with the Bungalows unlock.
    /// </summary>
    public enum NeedId
    {
        Rooms = 0,
        Drinks = 1,
        Food = 2,
        Relax = 3,
        OceanView = 4,
    }
}
