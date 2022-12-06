using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime.Internal.Transform;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using S3;
using YellowDog.Thirdparty.Authentication;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace MS_TransactionWebhook;

public class Function
{

    /// <summary>
    /// Handles Processing of the request sent to the RevelWebhook.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
    {
        ILogger _logger = LoggerFactory.Create(builder =>
            builder.AddSimpleConsole()).CreateLogger("ConsoleLogger");
        
        var response = new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Accepted,
            Body = "Success"
        };
            
        try
        {
            var queryParameters = GetQueryParameters(input.QueryStringParameters);

            if (!queryParameters.HasValue)
            {
                response.StatusCode = (int) HttpStatusCode.BadRequest;
                response.Body = "Missing lookup or type.";

                return response;
            }

            AuthResponse authRepsonse = await AuthorizeAsync(queryParameters.Value.secret, queryParameters.Value.type);

            if (authRepsonse.errors != null && authRepsonse.errors.Length > 0)
            {
                response.StatusCode = (int) HttpStatusCode.Unauthorized;
                response.Body = JsonConvert.SerializeObject(authRepsonse.errors);
                return response;
            }
            
            if (!authRepsonse.result.HasValue)
            {
                response.StatusCode = (int) HttpStatusCode.Unauthorized;
                response.Body = "Empty token response";
                return response;
            }

            var validateTakenResponse = new YDAuth().ValidateToken(authRepsonse.result.Value.AccessToken);
            
            if (!validateTakenResponse.status)
            {
                response.StatusCode = (int) HttpStatusCode.Unauthorized;
                response.Body = validateTakenResponse.reason;
                return response;
            }

            await using (var thirdpartySales = new ThirdPartySales(_logger, authRepsonse.result.Value.ClientId,queryParameters.Value.type,queryParameters.Value.venueId))
            {
                var parsedObject = ParseBody(input.Body, queryParameters.Value.objectKeyToken);
                
                thirdpartySales.PutOrder(input.Body,parsedObject.key);
            }

            return response;
            

        }
        catch (Exception ex)
        {
            _logger.LogError(ex,input.Body,input.PathParameters,input.Headers);
            
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            response.Body = "BadRequest";
        }

        return response;
    }

    public async Task<AuthResponse> AuthorizeAsync(string secret,string type)
    {
        var auth = new YDAuth();

        return await auth.GetTokenFromTPSecret(secret, type);
    }

    public (JObject parsedBody,string key) ParseBody(string body,string? keyToken)
    {
        var jObject = JObject.Parse(body);
        
        var key = Guid.NewGuid().ToString();

        if (!string.IsNullOrWhiteSpace(keyToken))
            key = (string)jObject.SelectToken(keyToken);

        return (jObject, key);
    }

    public (string secret, string type, string venueId, string objectKeyToken)? GetQueryParameters(IDictionary<string,string> queryParameters)
    {
        (string secret, string type, string venueId, string objectKeyToken) values = (null, null, null, null);

        if (queryParameters.TryGetValue("lookup", out string secret))
            values.secret = secret;
        else
            return null;
        if (queryParameters.TryGetValue("type", out string type))
            values.type = type;
        else
            return null;
        if (queryParameters.TryGetValue("venueId", out string venueId))
            values.venueId = venueId;
        if (queryParameters.TryGetValue("objectKeyToken", out string objectKeyToken))
            values.objectKeyToken = objectKeyToken;

        return values;
    }
}