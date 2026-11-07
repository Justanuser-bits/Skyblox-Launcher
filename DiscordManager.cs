using DiscordRPC;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SkybloxLauncher;

public static class DiscordManager
{
    private static DiscordRpcClient? client;

    public static void Initialize(string placeId, string year, string ticket)
    {
        try
        {
            client = new DiscordRpcClient("1557847926125105152");
            client.Initialize();

            Task.Run(() => SetPresenceAsync(placeId, year, ticket));
        }
        catch (Exception ex)
        {
            Console.WriteLine("RPC Error: " + ex.Message);
        }
    }

    private static async Task SetPresenceAsync(string placeId, string year, string ticket)
    {
        string gameName = "A Skyblox Game";
        try
        {
            using var http = new HttpClient();
            string url = $"https://skyblox.co/apisite/games/v1/games/multiget-place-details?placeIds={placeId}";
            string res = await http.GetStringAsync(url);
            JArray arr = JArray.Parse(res);
            if (arr.Count > 0)
            {
                gameName = arr[0]["name"]?.ToString() ?? gameName;
            }
        }
        catch { }

        var presence = new RichPresence
        {
            Details = "Playing " + gameName,
            State = "Year: " + year,
            Assets = new Assets
            {
                LargeImageKey = "https://skyblox.co/img/logo_bb.png",
                LargeImageText = gameName,
                SmallImageKey = "https://skyblox.co/img/logo_bb.png",
                SmallImageText = "Skyblox " + year
            },
            Buttons = new[]
            {
                new Button { Label = "Play Skyblox", Url = "https://skyblox.co" }
            },
            Party = new Party
            {
                ID = placeId,
                Size = 1,
                Max = 100
            },
            Secrets = new Secrets
            {
                JoinSecret = $"sclient://join/?placeId={placeId}&year={year}"
            }
        };

        client?.SetPresence(presence);
    }

    public static void Shutdown()
    {
        try
        {
            client?.Dispose();
            client = null;
        }
        catch { }
    }
}
