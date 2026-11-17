namespace LANPUT.Core;
public static class ReceiverRecovery
{
    public static NetworkChoice? SelectForStartup(IReadOnlyList<NetworkChoice> choices, string? name, string? address)
        => string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address) ? null : SelectNetwork(choices, name, address);

    // Never switch to another interface or guess when the original interface has multiple IPv4s.
    public static NetworkChoice? SelectNetwork(IReadOnlyList<NetworkChoice> choices, string? name, string? address)
    {
        var sameInterface = choices.Where(n => n.Name == name).ToArray();
        return sameInterface.FirstOrDefault(n => n.Address.ToString() == address)
            ?? (sameInterface.Length == 1 ? sameInterface[0] : null);
    }
}
