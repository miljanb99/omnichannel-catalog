namespace OmniChannel.Catalog.Host.Realtime;

public class CatalogHub : Hub
{
    public const string Group = "catalog";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Group);
        await base.OnConnectedAsync();
    }
}