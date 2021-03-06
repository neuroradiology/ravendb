using System.Threading.Tasks;
using Raven.Server.Routing;
using Raven.Server.Web;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.Handlers.Debugging
{
    public class ServerInfoHandler : RequestHandler
    {
        [RavenAction("/debug/server-id", "GET", AuthorizationStatus.ValidUser, IsDebugInformationEndpoint = true)]
        public Task ServerId()
        {
            using (ServerStore.ContextPool.AllocateOperationContext(out JsonOperationContext context))
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                context.Write(writer, new DynamicJsonValue
                {
                    ["ServerId"] = ServerStore.GetServerId().ToString()
                });
            }

            return Task.CompletedTask;
        }
    }
}