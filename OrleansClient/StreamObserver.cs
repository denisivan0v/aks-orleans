using System;
using System.Threading.Tasks;

using GrainAbstractions;

using Microsoft.Extensions.Logging;

using Orleans.Streams;

namespace OrleansClient
{
    public class StreamObserver : IAsyncObserver<ChatMsg>
    {
        private readonly ILogger _logger;

        public StreamObserver(ILogger logger)
        {
            _logger = logger;
        }

        public Task OnCompletedAsync()
        {
            _logger.LogInformation("Chatroom message stream received stream completed event");
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            _logger.LogInformation($"Chatroom is experiencing message delivery failure, ex :{ex}");
            return Task.CompletedTask;
        }

        public Task OnNextAsync(ChatMsg item, StreamSequenceToken token = null)
        {
            _logger.LogInformation($"=={item.Created}==         {item.Author} said: {item.Text}");
            return Task.CompletedTask;
        }
    }
}