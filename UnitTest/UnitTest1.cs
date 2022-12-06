using MS_TransactionWebhook;
using Snapshooter.Json;

namespace UnitTest;

public class UnitTest1
{
    [Fact]
    public async void AuthorizeTest()
    {
        var function = new Function();

        var auth = await function.AuthorizeAsync("Gqv6DaH4ezFRQZX8K7Y", "AppetizeOrders");

        var clientId = auth.result.Value.ClientId;

        Snapshot.Match(clientId,"Auth");
    }
    
    [Fact]
    public async void ParseBodyTest()
    {
        var function = new Function();

        var data = File.ReadAllText("MockData\\rollerOrder.json");

        var parsedBody = function.ParseBody(data,"data.booking.uniqueId");

        Snapshot.Match(parsedBody,"ParseBody");
    }
}
