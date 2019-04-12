using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Common;

using GrainAbstractions;

using Orleans;
using Orleans.Streams;

namespace Grains
{
    public class Channel : Grain, IChannel
    {
        private readonly List<ChatMsg> _messages = new List<ChatMsg>(100);
        private readonly HashSet<string> _onlineMembers = new HashSet<string>();

        private IAsyncStream<ChatMsg> _stream;

        public override Task OnActivateAsync()
        {
            var streamProvider = GetStreamProvider(Constants.ChatRoomStreamProvider);

            _stream = streamProvider.GetStream<ChatMsg>(Guid.NewGuid(), Constants.CharRoomStreamNameSpace);
            return base.OnActivateAsync();
        }

        public Task<Guid> GetId()
        {
            return Task.FromResult(_stream.Guid);
        }

        public async Task Join(string nickname)
        {
            await _stream.OnNextAsync(new ChatMsg("System", $"{nickname} joins the chat '{this.GetPrimaryKeyString()}' ..."));
            _onlineMembers.Add(nickname);
        }

        public async Task Leave(string nickname)
        {
            await _stream.OnNextAsync(new ChatMsg("System", $"{nickname} leaves the chat..."));
            _onlineMembers.Remove(nickname);
        }

        public async Task<bool> Message(ChatMsg msg)
        {
            _messages.Add(msg);
            await _stream.OnNextAsync(msg);

            return true;
        }

        public Task<bool> RenameMember(string oldNickname, string newNickname)
        {
            return Task.FromResult(_onlineMembers.Add(newNickname) && _onlineMembers.Remove(oldNickname));
        }

        public Task<string[]> GetMembers()
        {
            return Task.FromResult(_onlineMembers.ToArray());
        }

        public Task<ChatMsg[]> ReadHistory(int numberOfMessages)
        {
            var response = _messages
                           .OrderByDescending(x => x.Created)
                           .Take(numberOfMessages)
                           .OrderBy(x => x.Created)
                           .ToArray();

            return Task.FromResult(response);
        }
    }
}