using System;
using System.Threading;
using System.Threading.Tasks;

using Common;

using GrainAbstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;

namespace OrleansClient
{
    internal static class Program
    {
        private const string DefaultChannel = "general";
        //To make this sample simple
        //In this sample, one client can only join one channel, hence we have a static variable of one channel name.
        //client can send messages to the channel , and receive messages sent to the channel/stream from other clients. 
        private static string _joinedChannel;
        private static string _userName = $"user-{Guid.NewGuid()}";

        private static async Task Main(string[] args)
        {
            var clientInstance = await InitializeClient(args);

            PrettyConsole.Line("==== CLIENT: Initialized ====", ConsoleColor.Cyan);
            PrettyConsole.Line("CLIENT: Write commands:", ConsoleColor.Cyan);

            PrintHints();
            await JoinChannel(clientInstance, DefaultChannel);
            await Interact(clientInstance);
            await LeaveChannel(clientInstance);

            PrettyConsole.Line("==== CLIENT: Shutting down ====", ConsoleColor.DarkRed);
        }

        private static async Task<IClusterClient> InitializeClient(string[] args)
        {
            int initializeCounter = 0;

            var initSucceed = false;
            while (!initSucceed)
            {
                try
                {
                    var client = new ClientBuilder().Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = Constants.ClusterId;
                            options.ServiceId = Constants.ServiceId;
                        })
                        .UseLocalhostClustering()
                        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(IChannel).Assembly).WithReferences())
                        .ConfigureLogging(logging => logging.AddConsole())
                        //Depends on your application requirements, you can configure your client with other stream providers, which can provide other features, 
                        //such as persistence or recoverability. For more information, please see http://dotnet.github.io/orleans/Documentation/Orleans-Streams/Stream-Providers.html
                        .AddSimpleMessageStreamProvider(Constants.ChatRoomStreamProvider)
                        .Build();

                    await client.Connect();
                    initSucceed = client.IsInitialized;

                    if (initSucceed)
                    {
                        return client;
                    }
                }
                catch (Exception exc)
                {
                    PrettyConsole.Line(exc.Message, ConsoleColor.Cyan);
                    initSucceed = false;
                }

                if (initializeCounter++ > 10)
                {
                    return null;
                }

                PrettyConsole.Line("Client Init Failed. Sleeping 5s...", ConsoleColor.Red);
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            return null;
        }

        private static void PrintHints()
        {
            const ConsoleColor MenuColor = ConsoleColor.Magenta;
            PrettyConsole.Line("Type '/j <channel>' to join specific channel", MenuColor);
            PrettyConsole.Line("Type '/n <username>' to set your user name", MenuColor);
            PrettyConsole.Line("Type '/l' to leave specific channel", MenuColor);
            PrettyConsole.Line("Type '<any text>' to send a message", MenuColor);
            PrettyConsole.Line("Type '/h' to re-read channel history", MenuColor);
            PrettyConsole.Line("Type '/m' to query members in the channel", MenuColor);
            PrettyConsole.Line("Type '/exit' to exit client.", MenuColor);
        }

        private static async Task Interact(IClusterClient client)
        {
            string input;
            do
            {
                input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) continue;

                if (input.StartsWith("/j"))
                {
                    await JoinChannel(client, input.Replace("/j", "").Trim());
                }
                else if (input.StartsWith("/n"))
                {
                    await RenameMember(client, input.Replace("/n", "").Trim());
                }
                else if (input.StartsWith("/l"))
                {
                    await LeaveChannel(client);
                }
                else if (input.StartsWith("/h"))
                {
                    await ShowCurrentChannelHistory(client);
                }
                else if (input.StartsWith("/m"))
                {
                    await ShowChannelMembers(client);
                }
                else if (!input.StartsWith("/exit"))
                {
                    await SendMessage(client, input);
                }
            } while (input != "/exit");
        }

        private static async Task ShowChannelMembers(IGrainFactory client)
        {
            if (!EnsureChannelMembership())
            {
                return;
            }

            var room = client.GetGrain<IChannel>(_joinedChannel);
            var members = await room.GetMembers();

            PrettyConsole.Line($"====== Members for '{_joinedChannel}' Channel ======", ConsoleColor.DarkGreen);
            foreach (var member in members)
            {
                PrettyConsole.Line(member, ConsoleColor.DarkGreen);
            }

            PrettyConsole.Line("============", ConsoleColor.DarkGreen);
        }

        private static async Task RenameMember(IGrainFactory client, string nickname)
        {
            if (!EnsureChannelMembership())
            {
                return;
            }

            var room = client.GetGrain<IChannel>(_joinedChannel);
            if (await room.RenameMember(_userName, nickname))
            {
                _userName = nickname;
                PrettyConsole.Line($"Your user name is set to be {_userName}", ConsoleColor.DarkGreen);
            }
            else
            {
                PrettyConsole.Line($"Something went wrong during the renaming", ConsoleColor.DarkRed);
            }
        }

        private static async Task ShowCurrentChannelHistory(IGrainFactory client)
        {
            if (!EnsureChannelMembership())
            {
                return;
            }

            var room = client.GetGrain<IChannel>(_joinedChannel);
            var history = await room.ReadHistory(1000);

            PrettyConsole.Line($"====== History for '{_joinedChannel}' Channel ======", ConsoleColor.DarkGreen);
            foreach (var chatMsg in history)
            {
                PrettyConsole.Line($" ({chatMsg.Created:g}) {chatMsg.Author}> {chatMsg.Text}", ConsoleColor.DarkGreen);
            }

            PrettyConsole.Line("============", ConsoleColor.DarkGreen);
        }

        private static async Task SendMessage(IGrainFactory client, string messageText)
        {
            if (!EnsureChannelMembership())
            {
                return;
            }

            var room = client.GetGrain<IChannel>(_joinedChannel);
            await room.Message(new ChatMsg(_userName, messageText));
        }

        private static async Task JoinChannel(IClusterClient client, string channelName)
        {
            if (_joinedChannel == channelName)
            {
                PrettyConsole.Line($"You already joined channel {channelName}. Double joining a channel, which is implemented as a stream, would result in double subscription to the same stream, " +
                                   $"which would result in receiving duplicated messages. For more information, please refer to Orleans streaming documentation.");
                return;
            }

            PrettyConsole.Line($"Joining to channel {channelName}");

            var room = client.GetGrain<IChannel>(channelName);
            var roomId = await room.GetId();
            var stream = client.GetStreamProvider(Constants.ChatRoomStreamProvider).GetStream<ChatMsg>(roomId, Constants.CharRoomStreamNameSpace);

            var logger = client.ServiceProvider.GetService<ILoggerFactory>().CreateLogger($"{channelName} channel");

            //subscribe to the stream to receiver farther messages sent to the chatroom
            await stream.SubscribeAsync(new StreamObserver(logger));

            await room.Join(_userName);

            _joinedChannel = channelName;
        }

        private static async Task LeaveChannel(IClusterClient client)
        {
            if (!EnsureChannelMembership())
            {
                return;
            }

            PrettyConsole.Line($"Leaving channel {_joinedChannel}");

            var room = client.GetGrain<IChannel>(_joinedChannel);
            var roomId = await room.GetId();

            var stream = client.GetStreamProvider(Constants.ChatRoomStreamProvider).GetStream<ChatMsg>(roomId, Constants.CharRoomStreamNameSpace);

            //unsubscribe from the channel/stream since client left, so that client won't
            //receive future messages from this channel/stream
            var subscriptionHandles = await stream.GetAllSubscriptionHandles();
            foreach (var handle in subscriptionHandles)
            {
                await handle.UnsubscribeAsync();
            }

            await room.Leave(_userName);

            _joinedChannel = null;
        }

        private static bool EnsureChannelMembership()
        {
            if (string.IsNullOrEmpty(_joinedChannel))
            {
                PrettyConsole.Line($"You're not a member of a channel");
                return false;
            }

            return true;
        }
    }
}