namespace MarcoZechner.CommandApi.Core
{
    public delegate CommandResult CommandHandler(
        CommandExecutionContext context,
        CommandInput input
    );
}
