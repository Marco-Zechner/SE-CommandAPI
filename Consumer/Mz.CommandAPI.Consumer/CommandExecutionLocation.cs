namespace Mz.CommandApi
{
    /// <summary>
    /// Selects where CommandAPI executes a registered command.
    /// </summary>
    public enum CommandExecutionLocation
    {
        Client,
        Server,
        Either,
        Internal
    }
}
