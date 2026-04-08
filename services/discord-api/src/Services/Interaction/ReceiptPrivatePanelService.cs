using Discord;

public sealed class ReceiptPrivatePanelService
{
    public async Task ReplaceExistingPanelAsync(
        ReceiptSessionState session,
        ulong userId,
        ReceiptSelectionMode mode)
    {
        var key = BuildPanelKey(userId, mode);
        if (!session.ActivePrivatePanelInteractions.TryGetValue(key, out var existingInteraction))
        {
            return;
        }

        try
        {
            await existingInteraction.DeleteOriginalResponseAsync();
        }
        catch
        {
            // Ignore cleanup failures for stale or expired interaction tokens.
        }

        session.ActivePrivatePanelInteractions.Remove(key);
    }

    public void RegisterPanel(
        ReceiptSessionState session,
        ulong userId,
        ReceiptSelectionMode mode,
        IDiscordInteraction interaction)
    {
        session.ActivePrivatePanelInteractions[BuildPanelKey(userId, mode)] = interaction;
    }

    public async Task CloseAllPanelsAsync(ReceiptSessionState session)
    {
        if (session.ActivePrivatePanelInteractions.Count == 0)
        {
            return;
        }

        foreach (var interaction in session.ActivePrivatePanelInteractions.Values.Distinct().ToArray())
        {
            try
            {
                await interaction.DeleteOriginalResponseAsync();
            }
            catch
            {
                // Ignore cleanup failures for stale or expired interaction tokens.
            }
        }

        session.ActivePrivatePanelInteractions.Clear();
    }

    private static string BuildPanelKey(ulong userId, ReceiptSelectionMode mode)
    {
        return $"{mode}:{userId}";
    }
}
