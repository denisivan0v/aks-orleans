using System;
using System.Threading.Tasks;

using Orleans;

namespace GrainAbstractions
{
    public interface IChannel : IGrainWithStringKey
    {
        Task<Guid> GetId();
        Task Join(string nickname);
        Task Leave(string nickname);
        Task<bool> Message(ChatMsg msg);
        Task<ChatMsg[]> ReadHistory(int numberOfMessages);
        Task<bool> RenameMember(string oldNickname, string newNickname);
        Task<string[]> GetMembers();
    }
}